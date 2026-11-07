using System;
using System.Collections.Generic;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace ClearWin.RuleEngine
{
    /// <summary>
    /// Memory Engine: maintains session context and device state across the run.
    /// Keeps a time-series of system states, a session event log and a device
    /// snapshot so every decision has full context. Persists between sessions.
    /// </summary>
    public class MemoryEngine
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_POWER_STATUS
        {
            public byte ACLineStatus;
            public byte BatteryFlag;
            public byte BatteryLifePercent;
            public byte SystemStatusFlag;
            public uint BatteryLifeTime;
            public uint BatteryFullLifeTime;
        }

        [DllImport("kernel32.dll")]
        private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);

        private readonly object _lock = new object();
        private readonly string _dataDir;

        // Cached WMI service scan so the 20s analysis loop never pays for a
        // full Win32_Service enumeration more than once a minute.
        private static readonly object _serviceScanLock = new object();
        private static DateTime _serviceScanCacheTime = DateTime.MinValue;
        private const int ServiceScanCacheSeconds = 60;
        private static int _cachedStopped;
        private static int _cachedDisabled;
        private static int _cachedAutoStopped;
        private static bool _cachedScanned;
        private static List<string> _cachedProblemNames = new List<string>();

        private DateTime _sessionStart = DateTime.Now;
        private string _sessionMode = "AutoPilot";
        private readonly List<SessionEvent> _events = new List<SessionEvent>();
        private readonly List<SystemState> _history = new List<SystemState>();
        private readonly List<double> _healthHistory = new List<double>();
        private DeviceSnapshot _device;
        private DateTime _lastCapture = DateTime.MinValue;
        private double _captureIntervalSeconds = 5.0;

        /// <summary>Minimum seconds between automatic captures (tunable for tests).</summary>
        public double CaptureIntervalSeconds
        {
            get { lock (_lock) { return _captureIntervalSeconds; } }
            set { lock (_lock) { _captureIntervalSeconds = value; } }
        }

        public MemoryEngine(string dataDir)
        {
            _dataDir = dataDir;
            LoadDevice();
        }

        public DateTime SessionStart { get { return _sessionStart; } }
        public string SessionMode { get { return _sessionMode; } }
        public int HistoryCount { get { lock (_lock) { return _history.Count; } } }
        public int EventCount { get { lock (_lock) { return _events.Count; } } }
        public DeviceSnapshot Device { get { lock (_lock) { return _device; } } }

        public void BeginSession(string mode)
        {
            lock (_lock)
            {
                _sessionStart = DateTime.Now;
                _sessionMode = mode ?? "AutoPilot";
                _events.Clear();
                _history.Clear();
                _healthHistory.Clear();
            }
            RecordEvent("SessionStart", "Expert System session started in " + _sessionMode, 0);
        }

        public void RecordEvent(string type, string message, double value)
        {
            lock (_lock)
            {
                _events.Add(new SessionEvent { Type = type, Message = message, Value = value });
                if (_events.Count > RuleEngineConstants.MaxSessionEvents) _events.RemoveAt(0);
            }
        }

        /// <summary>Records a system state sample (throttled). Cheap, safe, thread-friendly.</summary>
        public void CaptureState(SystemState state)
        {
            CaptureState(state, false);
        }

        /// <summary>Records a system state sample; force bypasses the interval throttle.</summary>
        public void CaptureState(SystemState state, bool force)
        {
            if (state == null) return;
            lock (_lock)
            {
                if (!force && (DateTime.Now - _lastCapture).TotalSeconds < _captureIntervalSeconds) return;
                _lastCapture = DateTime.Now;
                _history.Add(state.Clone());
                if (_history.Count > RuleEngineConstants.MaxStateHistory) _history.RemoveAt(0);
                double health = 100 - (state.CpuUsage * 0.3 + state.RamUsage * 0.3 +
                    Math.Max(0, (state.CpuTemp - 50) * 0.8) + state.GpuUsage * 0.2);
                _healthHistory.Add(Math.Max(0, Math.Min(100, health)));
                if (_healthHistory.Count > 100) _healthHistory.RemoveAt(0);
            }
        }

        /// <summary>Builds a device snapshot from the OS and current state (best effort).</summary>
        public DeviceSnapshot CaptureDevice(SystemState state)
        {
            var device = new DeviceSnapshot
            {
                OsVersion = Environment.OSVersion.VersionString + " (" + ReadRegistryString(
                    @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName") + ")",
                CpuName = ReadRegistryString(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString"),
                CpuCores = Environment.ProcessorCount,
                CpuLogical = Environment.ProcessorCount,
                GpuName = ReadRegistryString(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000", "DriverDesc"),
                CapturedAt = DateTime.Now
            };

            try
            {
                DriveInfo drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
                device.DiskTotalGB = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                device.DiskFreeGB = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
            }
            catch { /* best effort */ }

            device.StartupItems = CountStartupItems();

            int stopped, disabled, autoStopped;
            List<string> problemNames;
            if (TryScanServices(out stopped, out disabled, out autoStopped, out problemNames))
            {
                device.ServicesScanned = true;
                device.StoppedServices = stopped;
                device.DisabledServices = disabled;
                device.AutoStoppedServices = autoStopped;
                device.ProblemServiceNames = problemNames;
            }

            SYSTEM_POWER_STATUS power;
            if (GetSystemPowerStatus(out power))
            {
                device.IsBatteryPowered = power.ACLineStatus == 0;
                device.BatteryLevel = power.BatteryLifePercent != 255 ? power.BatteryLifePercent : -1;
                device.IsLaptop = power.BatteryFlag != 255;
            }

            if (state != null)
            {
                device.TotalRamGB = state.TotalRamGB;
                device.FreeRamGB = state.TotalRamGB - state.UsedRamGB;
                if (device.BatteryLevel < 0) device.BatteryLevel = state.BatteryLevel;
                if (device.BatteryLevel >= 0 && !device.IsBatteryPowered) device.IsBatteryPowered = !state.IsCharging;
            }

            lock (_lock) { _device = device; }
            return device;
        }

        /// <summary>Counts registered startup entries (Run/RunOnce keys + Startup folders).</summary>
        private static int CountStartupItems()
        {
            int count = 0;
            try
            {
                string[] keys =
                {
                    @"Software\Microsoft\Windows\CurrentVersion\Run",
                    @"Software\Microsoft\Windows\CurrentVersion\RunOnce"
                };
                foreach (string sub in keys)
                {
                    RegistryKey cu = Registry.CurrentUser.OpenSubKey(sub);
                    if (cu != null) { count += cu.GetValueNames().Length; cu.Close(); }
                    RegistryKey lm = Registry.LocalMachine.OpenSubKey(sub);
                    if (lm != null) { count += lm.GetValueNames().Length; lm.Close(); }
                }
                string[] folders =
                {
                    System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        @"Microsoft\Windows\Start Menu\Programs\Startup"),
                    System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        @"Microsoft\Windows\Start Menu\Programs\Startup")
                };
                foreach (string folder in folders)
                {
                    if (System.IO.Directory.Exists(folder))
                        count += System.IO.Directory.GetFiles(folder).Length;
                }
            }
            catch { /* best effort */ }
            return count;
        }

        private static string ReadRegistryString(string subKey, string name)
        {
            try
            {
                RegistryKey key = Registry.LocalMachine.OpenSubKey(subKey);
                if (key == null) return "";
                object value = key.GetValue(name);
                key.Close();
                if (value == null) return "";
                return value.ToString();
            }
            catch { return ""; }
        }

        // ============================ SERVICES (WMI) ============================
        // Best-effort, exception-safe, time-cached. Never blocks the live loop.

        /// <summary>Scans Windows services once per cache window and reports health counters.</summary>
        internal static bool TryScanServices(out int stopped, out int disabled, out int autoStopped,
            out List<string> problemNames)
        {
            stopped = 0; disabled = 0; autoStopped = 0;
            problemNames = new List<string>();
            lock (_serviceScanLock)
            {
                if ((DateTime.Now - _serviceScanCacheTime).TotalSeconds < ServiceScanCacheSeconds && _cachedScanned)
                {
                    stopped = _cachedStopped;
                    disabled = _cachedDisabled;
                    autoStopped = _cachedAutoStopped;
                    problemNames = new List<string>(_cachedProblemNames);
                    return true;
                }
                try
                {
                    int s = 0, d = 0, a = 0;
                    var problems = new List<string>();
                    using (var searcher = new ManagementObjectSearcher("SELECT Name, State, StartMode FROM Win32_Service"))
                    {
                        foreach (ManagementObject mo in searcher.Get())
                        {
                            string state = Convert.ToString(mo["State"]);
                            string mode = Convert.ToString(mo["StartMode"]);
                            if (state == "Stopped") s++;
                            if (mode == "Disabled") d++;
                            if (mode == "Auto" && state == "Stopped")
                            {
                                a++;
                                if (problems.Count < 10)
                                    problems.Add(Convert.ToString(mo["Name"]));
                            }
                        }
                    }
                    _cachedStopped = s;
                    _cachedDisabled = d;
                    _cachedAutoStopped = a;
                    _cachedProblemNames = problems;
                    _cachedScanned = true;
                    _serviceScanCacheTime = DateTime.Now;
                    stopped = s; disabled = d; autoStopped = a;
                    problemNames = problems;
                    return true;
                }
                catch
                {
                    // WMI unavailable or blocked: report "not scanned" so engines
                    // never mistake zeroes for a healthy system.
                    _cachedScanned = false;
                    return false;
                }
            }
        }

        /// <summary>Name of the first auto-start service that is unexpectedly stopped.</summary>
        public static string GetFirstAutoStoppedService()
        {
            int stopped, disabled, autoStopped;
            List<string> problems;
            if (!TryScanServices(out stopped, out disabled, out autoStopped, out problems))
                return null;
            return problems.Count > 0 ? problems[0] : null;
        }

        /// <summary>Starts a single Windows service via WMI (admin required).</summary>
        public static bool TryStartService(string serviceName)
        {
            if (string.IsNullOrEmpty(serviceName)) return false;
            try
            {
                using (var searcher = new ManagementObjectSearcher(string.Format(
                    "SELECT Name, State FROM Win32_Service WHERE Name='{0}'", serviceName.Replace("'", "''"))))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        if (Convert.ToString(mo["State"]) == "Running") return true;
                        ManagementBaseObject ret = mo.InvokeMethod("StartService", null) as ManagementBaseObject;
                        if (ret != null)
                        {
                            try { return Convert.ToUInt32(ret["ReturnValue"]) == 0; }
                            catch { return false; }
                        }
                        return false;
                    }
                }
            }
            catch { return false; }
            return false;
        }

        /// <summary>Average and trend for a metric over the retained history.</summary>
        public bool GetTrend(string metric, out double latest, out double average, out double delta)
        {
            latest = 0; average = 0; delta = 0;
            List<SystemState> snap;
            lock (_lock) { snap = new List<SystemState>(_history); }
            if (snap.Count < 2) return false;

            double sum = 0;
            for (int i = 0; i < snap.Count; i++)
            {
                double v = GetMetric(snap[i], metric);
                sum += v;
                if (i == snap.Count - 1) latest = v;
            }
            average = sum / snap.Count;
            delta = latest - GetMetric(snap[0], metric);
            return true;
        }

        private static double GetMetric(SystemState s, string metric)
        {
            if (metric == "cpu") return s.CpuUsage;
            if (metric == "ram") return s.RamUsage;
            if (metric == "gpu") return s.GpuUsage;
            if (metric == "temp") return s.CpuTemp;
            if (metric == "fps") return s.Fps;
            if (metric == "disk") return s.DiskUsage;
            return 0;
        }

        public double AvgHealth
        {
            get
            {
                lock (_lock)
                {
                    if (_healthHistory.Count == 0) return 100;
                    double sum = 0;
                    for (int i = 0; i < _healthHistory.Count; i++) sum += _healthHistory[i];
                    return sum / _healthHistory.Count;
                }
            }
        }

        /// <summary>Serializable session context summary.</summary>
        public string GetSessionContext()
        {
            var sb = new StringBuilder();
            lock (_lock)
            {
                sb.AppendLine("Session mode: " + _sessionMode);
                sb.AppendLine("Session started: " + _sessionStart.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine("States captured: " + _history.Count);
                sb.AppendLine("Avg health: " + AvgHealth.ToString("F1"));
                sb.AppendLine("Events: " + _events.Count);
                if (_device != null)
                {
                    sb.AppendLine("OS: " + _device.OsVersion);
                    sb.AppendLine("CPU: " + _device.CpuName);
                    sb.AppendLine("GPU: " + _device.GpuName);
                    sb.AppendLine("RAM total: " + _device.TotalRamGB.ToString("F1") + " GB");
                    sb.AppendLine("Battery: " + (_device.IsBatteryPowered ? "on battery" : "plugged in"));
                }
            }
            return sb.ToString();
        }

        public void Save()
        {
            try
            {
                if (string.IsNullOrEmpty(_dataDir)) return;
                string dir = Path.Combine(_dataDir, "expert");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "expert_mem.txt"), GetSessionContext());

                DeviceSnapshot dev;
                lock (_lock) { dev = _device; }
                if (dev != null)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("CLEARWIN_EXPERT_DEVICE_V1");
                    sb.AppendLine("OS|" + dev.OsVersion);
                    sb.AppendLine("CPU|" + dev.CpuName);
                    sb.AppendLine("CORES|" + dev.CpuCores);
                    sb.AppendLine("LOGICAL|" + dev.CpuLogical);
                    sb.AppendLine("RAM|" + dev.TotalRamGB.ToString("R"));
                    sb.AppendLine("GPU|" + dev.GpuName);
                    sb.AppendLine("DISKTYPE|" + dev.DiskType);
                    sb.AppendLine("DISKTOTAL|" + dev.DiskTotalGB.ToString("R"));
                    sb.AppendLine("DISKFREE|" + dev.DiskFreeGB.ToString("R"));
                    sb.AppendLine("LAPTOP|" + (dev.IsLaptop ? "1" : "0"));
                    sb.AppendLine("BATTERY|" + (dev.IsBatteryPowered ? "1" : "0"));
                    sb.AppendLine("BATLEVEL|" + dev.BatteryLevel.ToString("R"));
                    sb.AppendLine("STARTUP|" + dev.StartupItems);
                    File.WriteAllText(Path.Combine(dir, "expert_device.dat"), sb.ToString());
                }
            }
            catch { /* never break the app */ }
        }

        private void LoadDevice()
        {
            try
            {
                if (string.IsNullOrEmpty(_dataDir)) return;
                string path = Path.Combine(_dataDir, "expert", "expert_device.dat");
                if (!File.Exists(path)) return;
                string[] lines = File.ReadAllLines(path);
                if (lines.Length == 0 || lines[0] != "CLEARWIN_EXPERT_DEVICE_V1") return;
                var dev = new DeviceSnapshot();
                for (int i = 1; i < lines.Length; i++)
                {
                    string[] p = lines[i].Split('|');
                    if (p.Length < 2) continue;
                    try
                    {
                        switch (p[0])
                        {
                            case "OS": dev.OsVersion = p[1]; break;
                            case "CPU": dev.CpuName = p[1]; break;
                            case "CORES": dev.CpuCores = int.Parse(p[1]); break;
                            case "LOGICAL": dev.CpuLogical = int.Parse(p[1]); break;
                            case "RAM": dev.TotalRamGB = double.Parse(p[1]); break;
                            case "GPU": dev.GpuName = p[1]; break;
                            case "DISKTYPE": dev.DiskType = p[1]; break;
                            case "DISKTOTAL": dev.DiskTotalGB = double.Parse(p[1]); break;
                            case "DISKFREE": dev.DiskFreeGB = double.Parse(p[1]); break;
                            case "LAPTOP": dev.IsLaptop = p[1] == "1"; break;
                            case "BATTERY": dev.IsBatteryPowered = p[1] == "1"; break;
                            case "BATLEVEL": dev.BatteryLevel = double.Parse(p[1]); break;
                            case "STARTUP": dev.StartupItems = int.Parse(p[1]); break;
                        }
                    }
                    catch { /* skip malformed */ }
                }
                lock (_lock) { _device = dev; }
            }
            catch { /* never break the app */ }
        }
    }
}
