using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;

namespace ClearWin
{
    public partial class SmartAgent
    {
        // ===================== INTELLIGENCE ENGINE =====================
        private DeviceProfile _deviceProfile = new DeviceProfile();
        private WorkloadInfo _workload = new WorkloadInfo();
        private TuningWeights _tuningWeights = new TuningWeights();
        private List<SmartSuggestion> _suggestions = new List<SmartSuggestion>();
        private List<ProcessImpact> _processImpacts = new List<ProcessImpact>();
        private List<string> _protectedProcesses = new List<string>();
        private List<double> _batteryHistory = new List<double>();
        private List<DateTime> _batteryTimestamps = new List<DateTime>();
        private int _lastSuggestionCheck = 0;
        private int _lastTuningTime = 0;
        private const int WORKLOAD_CHECK_INTERVAL = 5;
        private const int SUGGESTION_CHECK_INTERVAL = 30;
        private const int DEVICE_PROFILE_INTERVAL = 300;
        private const int TUNING_INTERVAL = 60;
        private const int PROCESS_IMPACT_SAMPLES = 10;
        private Dictionary<string, int> _processCpuSamples = new Dictionary<string, int>();

        // ===================== DEVICE PROFILING =====================
        private bool _deviceProfiled = false;

        public void SetDeviceProfile(DeviceProfile profile)
        {
            if (profile != null)
            {
                _deviceProfile = profile;
                _deviceProfiled = true;
            }
        }

        public DeviceProfile GetDeviceProfile()
        {
            return _deviceProfile;
        }

        private void ProfileDevice()
        {
            if (_deviceProfiled) return;
            _deviceProfiled = true;

            try
            {
                using (var cpuQuery = new ManagementObjectSearcher("SELECT Name,NumberOfCores,NumberOfLogicalProcessors,MaxClockSpeed FROM Win32_Processor"))
                {
                    foreach (ManagementObject mo in cpuQuery.Get())
                    {
                        _deviceProfile.CpuName = Convert.ToString(mo["Name"]);
                        _deviceProfile.CpuCoreCount = Convert.ToInt32(mo["NumberOfCores"]);
                        _deviceProfile.CpuLogicalProcessors = Convert.ToInt32(mo["NumberOfLogicalProcessors"]);
                        _deviceProfile.CpuMaxFreqGHz = Convert.ToDouble(mo["MaxClockSpeed"]) / 1000.0;
                        break;
                    }
                }
            }
            catch (Exception ex) { LogError("ProfileDevice", ex); }

            try
            {
                using (var memQuery = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementObject mo in memQuery.Get())
                    {
                        _deviceProfile.TotalRamGB = Convert.ToDouble(mo["TotalPhysicalMemory"]) / 1073741824.0;
                        break;
                    }
                }
            }
            catch (Exception ex) { LogError("ProfileDevice", ex); }

            try
            {
                using (var gpuQuery = new ManagementObjectSearcher("SELECT Name,AdapterRAM FROM Win32_VideoController"))
                {
                    foreach (ManagementObject mo in gpuQuery.Get())
                    {
                        string gn = Convert.ToString(mo["Name"]);
                        if (!string.IsNullOrEmpty(gn) && !gn.Contains("RDP") && !gn.Contains("Basic"))
                        {
                            _deviceProfile.GpuName = gn;
                            _deviceProfile.GpuVRAMBs = mo["AdapterRAM"] != null ? Convert.ToDouble(mo["AdapterRAM"]) : 0;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex) { LogError("ProfileDevice", ex); }

            try
            {
                foreach (var drive in System.IO.DriveInfo.GetDrives())
                {
                    if (drive.IsReady && drive.Name == @"C:\")
                    {
                        _deviceProfile.DiskTotalGB = drive.TotalSize / 1073741824.0;
                        break;
                    }
                }
            }
            catch (Exception ex) { LogError("ProfileDevice", ex); }

            try
            {
                using (var diskQuery = new ManagementObjectSearcher("SELECT Model FROM Win32_DiskDrive WHERE Index=0"))
                {
                    foreach (ManagementObject mo in diskQuery.Get())
                    {
                        string model = Convert.ToString(mo["Model"]).ToUpper();
                        if (model.Contains("SSD") || model.Contains("NVME") || model.Contains("M.2"))
                            _deviceProfile.DiskType = "NVMe";
                        else if (model.Contains("SATA") || model.Contains("HDD"))
                            _deviceProfile.DiskType = "HDD";
                        else
                            _deviceProfile.DiskType = model.Contains("SSD") ? "SSD" : "HDD";
                        break;
                    }
                }
            }
            catch { _deviceProfile.DiskType = "Unknown"; }

            try
            {
                using (var sysQuery = new ManagementObjectSearcher("SELECT SystemType FROM Win32_ComputerSystem"))
                {
                    foreach (ManagementObject mo in sysQuery.Get())
                    {
                        string sysType = Convert.ToString(mo["SystemType"]);
                        _deviceProfile.IsLaptop = false;
                        break;
                    }
                }
                if (!_deviceProfile.IsLaptop)
                {
                    try
                    {
                        using (var batQuery = new ManagementObjectSearcher("SELECT BatteryStatus FROM Win32_Battery"))
                        {
                            foreach (ManagementObject mo in batQuery.Get())
                            {
                                _deviceProfile.IsLaptop = true;
                                break;
                            }
                        }
                    }
                    catch (Exception ex) { LogError("ProfileDevice", ex); }
                }
            }
            catch (Exception ex) { LogError("ProfileDevice", ex); }

            _deviceProfile.ComputeAdaptedParameters();
            _deviceProfile.LastUpdated = DateTime.Now;
        }

        // ===================== WORKLOAD DETECTION =====================
        private void DetectWorkload()
        {
            if (_tickCount % WORKLOAD_CHECK_INTERVAL != 0) return;

            lock (_stateLock)
            {
                if (_currentState == null) return;

                WorkloadType prev = _workload.DetectedWorkload;
                WorkloadType detected = WorkloadType.Desktop;
                double confidence = 0.5;

                if (_currentState.IsGame)
                {
                    detected = WorkloadType.Gaming;
                    confidence = 0.9;
                }
                else if (_currentState.IsCharging && _currentState.CpuUsage < 15 && _currentState.RamUsage < 40)
                {
                    detected = WorkloadType.Idle;
                    confidence = 0.8;
                }
                else if (_currentState.IsCharging && _currentState.CpuUsage > 50)
                {
                    detected = WorkloadType.Work;
                    confidence = 0.6;
                }
                else
                {
                    double cpu = _currentState.CpuUsage;
                    double gpu = _currentState.GpuUsage;
                    double ram = _currentState.RamUsage;

                    if (gpu > 60 && cpu > 40 && _currentState.Fps > 15)
                    {
                        detected = WorkloadType.Streaming;
                        confidence = 0.65;
                    }
                    else if (cpu > 70 && ram > 60)
                    {
                        detected = WorkloadType.Montage;
                        confidence = 0.55;
                    }
                    else if (cpu > 30 && ram > 50)
                    {
                        detected = WorkloadType.Work;
                        confidence = 0.5;
                    }
                    else if (cpu < 20 && ram < 40)
                    {
                        detected = WorkloadType.Browsing;
                        confidence = 0.6;
                    }
                    else
                    {
                        detected = WorkloadType.Desktop;
                        confidence = 0.4;
                    }
                }

                if (detected != prev)
                {
                    if (_workload.IsTransitioning && detected == _workload.PreviousWorkload)
                    {
                        _workload.IsTransitioning = false;
                    }
                    else if (!_workload.IsTransitioning)
                    {
                        _workload.PreviousWorkload = prev;
                        _workload.IsTransitioning = true;
                        _workload.WorkloadStarted = DateTime.Now;
                    }
                    else
                    {
                        TimeSpan transDuration = DateTime.Now - _workload.WorkloadStarted;
                        if (transDuration.TotalSeconds > 10)
                        {
                            _workload.PreviousWorkload = _workload.DetectedWorkload;
                            _workload.DetectedWorkload = detected;
                            _workload.Confidence = confidence;
                            _workload.IsTransitioning = false;
                            _workload.WorkloadStarted = DateTime.Now;
                            _workload.Duration = TimeSpan.Zero;
                        }
                    }
                }
                else
                {
                    _workload.IsTransitioning = false;
                    _workload.Confidence = Math.Min(1.0, _workload.Confidence + 0.05);
                }

                _workload.ActiveApp = _currentState.ActiveProcess;
                _workload.ActiveProcess = _currentState.ActiveProcess;
                _workload.ProcessCount = _currentState.ProcessCount;
                _workload.CpuIntensity = _currentState.CpuUsage / 100.0;
                _workload.GpuIntensity = _currentState.GpuUsage / 100.0;
                _workload.MemoryIntensity = _currentState.RamUsage / 100.0;
                _workload.DiskIntensity = _currentState.DiskUsage / 100.0;
                _workload.Duration = DateTime.Now - _workload.WorkloadStarted;
            }
        }

        // ===================== PROCESS IMPACT SCORING =====================
        private void UpdateProcessImpact()
        {
            if (_tickCount % 10 != 0) return;

            try
            {
                Process[] procs;
                lock (_processCacheLock) { procs = _cachedProcesses; }
                if (procs == null || procs.Length == 0) return;
                var impacts = new Dictionary<string, ProcessImpact>();

                foreach (var p in procs)
                {
                    try
                    {
                        if (p.HasExited) continue;
                        string name = p.ProcessName;
                        if (string.IsNullOrEmpty(name) || name.Length < 2) continue;
                        if (SYSTEM_PROCESSES.Contains(name.ToLower())) continue;

                        ProcessImpact impact;
                        if (!impacts.TryGetValue(name, out impact))
                        {
                            impact = new ProcessImpact
                            {
                                ProcessName = name,
                                Pid = p.Id,
                                IsSystemProcess = false,
                                IsUserProtected = _protectedProcesses.Contains(name)
                            };
                            impacts[name] = impact;
                        }

                        try
                        {
                            impact.RamUsageMB = p.WorkingSet64 / 1048576.0;
                            impact.ThreadCount = p.Threads.Count;
                        }
                        catch (Exception ex) { LogError("UpdateProcessImpact", ex); }

                        impact.CpuUsage = Math.Min(100, impact.RamUsageMB / 50.0 + impact.ThreadCount * 0.3);

                        double score = 0;
                        score += Math.Min(40, impact.CpuUsage * 0.4);
                        score += Math.Min(30, impact.RamUsageMB / 100.0);
                        score += Math.Min(15, impact.ThreadCount * 0.5);
                        if (impact.IsUserProtected) score *= 0.2;

                        impact.ImpactScore = Math.Min(100, score);
                        impact.ImpactCategory = score > 50 ? "High" : score > 20 ? "Medium" : score > 5 ? "Low" : "Negligible";
                        impact.LastSeen = DateTime.Now;
                    }
                    catch (Exception ex) { LogError("UpdateProcessImpact", ex); }
                }

                var sorted = new List<ProcessImpact>(impacts.Values);
                sorted.Sort((a, b) => b.ImpactScore.CompareTo(a.ImpactScore));
                if (sorted.Count > 30) sorted.RemoveRange(30, sorted.Count - 30);
                lock (_listLock) { _processImpacts = sorted; }
            }
            catch (Exception ex) { LogError("UpdateProcessImpact", ex); }
        }

        // ===================== SMART SUGGESTIONS =====================
        private void GenerateSuggestions()
        {
            if (_tickCount - _lastSuggestionCheck < SUGGESTION_CHECK_INTERVAL) return;
            _lastSuggestionCheck = _tickCount;

            lock (_stateLock)
            {
                if (_currentState == null) return;

                if (_currentState.RamUsage > 85 && _deviceProfile.TotalRamGB <= 8)
                {
                    AddSuggestion("Upgrade RAM", "Your system has " + _deviceProfile.TotalRamGB.ToString("F0") + "GB RAM which is frequently maxed out",
                        "Upgrading to 16GB would significantly reduce slowdowns and improve multitasking",
                        SuggestionPriority.High, "Hardware", 30, true);
                }

                if (_currentState.DiskUsage > 90)
                {
                    AddSuggestion("Disk Space Critical", "Your disk is " + _currentState.DiskUsage.ToString("F0") + "% full",
                        "Low disk space causes Windows to slow down significantly. Consider cleaning temp files or moving large files.",
                        SuggestionPriority.Critical, "Storage", 25, true);
                }
                else if (_currentState.DiskUsage > 80)
                {
                    AddSuggestion("Disk Space Warning", "Your disk is " + _currentState.DiskUsage.ToString("F0") + "% full",
                        "Consider cleaning up unnecessary files to maintain optimal performance.",
                        SuggestionPriority.Medium, "Storage", 15, false);
                }

                if (_currentState.CpuTemp > _deviceProfile.ThermalWarningTemp && !_currentState.IsGame)
                {
                    AddSuggestion("High Temperature", "CPU temperature is " + _currentState.CpuTemp.ToString("F0") + "C",
                        "Consider cleaning dust from fans, improving airflow, or applying new thermal paste.",
                        SuggestionPriority.High, "Thermal", 20, false);
                }

                if (_currentState.ProcessCount > _deviceProfile.MaxBackgroundProcesses)
                {
                    AddSuggestion("Too Many Processes", _currentState.ProcessCount.ToString() + " processes running",
                        "Running too many processes consumes RAM and CPU. Close unused applications.",
                        SuggestionPriority.Medium, "Performance", 15, true);
                }

                if (_deviceProfile.PerformanceTier <= 2 && _deviceProfile.TotalRamGB < 8)
                {
                    AddSuggestion("Low-End System Detected", "This system may benefit from lightweight optimizations",
                        "Consider using a lighter browser, disabling visual effects, and reducing startup programs.",
                        SuggestionPriority.Low, "Advice", 10, false);
                }

                if (_trends.ContainsKey("temp"))
                {
                    var tempTrend = _trends["temp"];
                    if (tempTrend.Slope > 0.5 && tempTrend.RSquared > 0.3)
                    {
                        double predicted = tempTrend.PredictNext(20);
                        if (predicted > _deviceProfile.ThermalThrottleTemp)
                        {
                            AddSuggestion("Temperature Rising", "CPU temperature is increasing steadily",
                                string.Format("Current trend projects {0:F0}C in 20 ticks. Consider reducing load.", predicted),
                                SuggestionPriority.High, "Thermal", 20, true);
                        }
                    }
                }

                if (_currentState.Fps > 0 && _currentState.Fps < 30 && _currentState.IsGame)
                {
                    AddSuggestion("Low FPS Detected", "Frame rate is " + _currentState.Fps.ToString("F0") + " FPS",
                        "Try closing background applications, lowering game settings, or enabling Game Mode.",
                        SuggestionPriority.High, "Gaming", 25, true);
                }

                if (_currentState.FpsStability < 50 && _currentState.Fps > 15)
                {
                    AddSuggestion("Unstable Frame Rate", "FPS stability is " + _currentState.FpsStability.ToString("F0") + "%",
                        "High frame time variation causes stuttering. Game Mode can help stabilize performance.",
                        SuggestionPriority.Medium, "Gaming", 20, false);
                }

                if (_trends.ContainsKey("disk"))
                {
                    var diskTrend = _trends["disk"];
                    if (diskTrend.Slope > 0.1 && diskTrend.RSquared > 0.3)
                    {
                        double daysToFull = (100 - _currentState.DiskUsage) / (diskTrend.Slope * 3600);
                        if (daysToFull < 30 && daysToFull > 0)
                        {
                            AddSuggestion("Disk Filling Up", string.Format("At current rate, disk will be full in {0:F0} days", daysToFull),
                                "Review large files, clear downloads folder, or run disk cleanup.",
                                SuggestionPriority.Medium, "Storage", 15, false);
                        }
                    }
                }
            }
        }

        private void AddSuggestion(string title, string description, string detailed, SuggestionPriority priority, string category, double impact, bool autoActionable)
        {
            lock (_listLock)
            {
                foreach (var s in _suggestions)
                {
                    if (s.Title == title && !s.Executed && s.DismissedAt == null) return;
                }

                _suggestions.Add(new SmartSuggestion
                {
                    Title = title,
                    Description = description,
                    DetailedExplanation = detailed,
                    Priority = priority,
                    Category = category,
                    EstimatedImpact = impact,
                    AutoActionable = autoActionable,
                    Confidence = 0.7
                });

                if (_suggestions.Count > 50) _suggestions.RemoveAt(0);
            }
        }

        // ===================== SELF-TUNING AI =====================
        private void SelfTune()
        {
            if (_tickCount - _lastTuningTime < TUNING_INTERVAL) return;
            _lastTuningTime = _tickCount;

            if (_stats.TotalDecisions < 20) return;

            var recentExperiences = GetRecentDecisionOutcomes(30);
            if (recentExperiences.Count < 10) return;

            var categorySuccess = new Dictionary<DecisionCategory, Tuple<int, int>>();
            foreach (var exp in recentExperiences)
            {
                Tuple<int, int> pair;
                if (!categorySuccess.TryGetValue(exp.Category, out pair))
                {
                    pair = new Tuple<int, int>(0, 0);
                    categorySuccess[exp.Category] = pair;
                }
                int successes = pair.Item1 + (exp.Result == ExperienceResult.Improved ? 1 : 0);
                int total = pair.Item2 + 1;
                categorySuccess[exp.Category] = new Tuple<int, int>(successes, total);
            }

            foreach (var kvp in categorySuccess)
            {
                double successRate = kvp.Value.Item2 > 0 ? (double)kvp.Value.Item1 / kvp.Value.Item2 : 0.5;
                double factor = successRate > 0.6 ? 1.1 : successRate < 0.3 ? 0.9 : 1.0;
                _tuningWeights.AdjustWeight(kvp.Key, factor);
            }
        }

        private List<AgentExperience> GetRecentDecisionOutcomes(int count)
        {
            lock (_listLock)
            {
                int start = Math.Max(0, _experiences.Count - count);
                int capacity = _experiences.Count - start;
                var result = new List<AgentExperience>(capacity);
                for (int i = start; i < _experiences.Count; i++)
                {
                    if (_experiences[i].Result != ExperienceResult.Unknown)
                        result.Add(_experiences[i]);
                }
                return result;
            }
        }

        // ===================== ADAPTIVE PROFILES (auto-switch) =====================
        private void ApplyAdaptiveWorkload()
        {
            if (_workload.Confidence < 0.6) return;

            switch (_workload.DetectedWorkload)
            {
                case WorkloadType.Gaming:
                    if (_mode != AgentMode.GameMode && _mode != AgentMode.CoolMode)
                    {
                        SetMode(AgentMode.GameMode);
                        AddLog("INFO", "Workload", "Auto-switched to Game Mode - gaming workload detected (confidence: " + (_workload.Confidence * 100).ToString("F0") + "%)");
                    }
                    break;
                case WorkloadType.Idle:
                    if (_mode == AgentMode.GameMode)
                    {
                        SetMode(AgentMode.AutoPilot);
                        AddLog("INFO", "Workload", "Auto-switched to AutoPilot - idle workload detected");
                    }
                    break;
                case WorkloadType.Montage:
                case WorkloadType.Streaming:
                    if (_mode == AgentMode.Off)
                    {
                        SetMode(AgentMode.BalanceMode);
                        AddLog("INFO", "Workload", "Auto-switched to Balance Mode - intensive workload detected");
                    }
                    break;
            }
        }

        // ===================== PUBLIC ACCESSORS =====================
        public DeviceProfile Device { get { return _deviceProfile; } }
        public WorkloadInfo Workload { get { return _workload; } }
        public TuningWeights Weights { get { return _tuningWeights; } }

        public List<SmartSuggestion> GetSuggestions()
        {
            lock (_listLock)
            {
                var result = new List<SmartSuggestion>(_suggestions.Count);
                for (int i = 0; i < _suggestions.Count; i++)
                {
                    if (_suggestions[i].DismissedAt == null && !_suggestions[i].Executed)
                        result.Add(_suggestions[i]);
                }
                return result;
            }
        }

        public List<SmartSuggestion> GetAllSuggestions()
        {
            lock (_listLock) { return new List<SmartSuggestion>(_suggestions); }
        }

        public List<ProcessImpact> GetTopProcessImpacts(int count)
        {
            int n = Math.Min(count, _processImpacts.Count);
            var result = new List<ProcessImpact>(n);
            for (int i = 0; i < n; i++)
                result.Add(_processImpacts[i]);
            return result;
        }

        public void DismissSuggestion(string id, string reason)
        {
            lock (_listLock)
            {
                foreach (var s in _suggestions)
                {
                    if (s.Id == id)
                    {
                        s.DismissedAt = DateTime.Now;
                        s.DismissReason = reason;
                        break;
                    }
                }
            }
        }

        public void ProtectProcess(string name)
        {
            if (!_protectedProcesses.Contains(name))
                _protectedProcesses.Add(name);
        }

        public void UnprotectProcess(string name)
        {
            _protectedProcesses.Remove(name);
        }
    }
}
