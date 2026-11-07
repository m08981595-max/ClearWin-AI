using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using Microsoft.Win32;

namespace ClearWin
{
    public partial class SmartAgent
    {
        // =====================================================================
        // NETWORK / LOW-LATENCY TUNING SUBSYSTEM (Frieren-inspired)
        // Adapted from the Android "Frieren AI" module (TerX) to Windows:
        //   1. Profile resolution: thermal > battery > gaming > balanced
        //   2. netsh TCP tuning (autotuning, congestion, timestamps, RSS, ECN)
        //   3. Per-interface registry TCP tuning (TCPNoDelay / TcpAckFrequency)
        //   4. AI-DNS analog: negative DNS cache TTL + flush on profile switch
        //   5. Backup-original / restore discipline (terx.sh "BAK" pattern)
        //   6. Idle + AC gated maintenance (periodic DNS flush)
        // Activation is explicit (EnableNetworkTuning) so tests/simulation never
        // touch the real network stack. Admin is required to apply (graceful skip).
        // =====================================================================

        // --- Config (mirrors Frieren config/terx.sh, Windows-tuned) ---
        private static readonly bool NT_ENABLE = true;
        private const int NT_INTERVAL_TICKS = 120;              // ~60s check cadence
        private const int NT_MODE_SWITCH_COOLDOWN_MINUTES = 10; // anti-oscillation gate
        private const int NT_IDLE_MAINT_INTERVAL_HOURS = 6;
        private const int NT_IDLE_REQUIRED_SECONDS = 900;       // 15 min without input
        private const int NT_BATTERY_LOW_PERCENT = 20;
        private const double NT_THERMAL_HOT_C = 88;
        private static readonly bool NT_ENABLE_IDLE_MAINT = true;

        private bool _ntActive = false;
        private int _ntTick = 0;
        private string _ntEffectiveProfile = "balanced";
        private int _ntAppliedCount = 0;
        private int _ntSkippedCount = 0;
        private string _ntRefreshNote = "";
        private DateTime _ntLastModeChange = DateTime.MinValue;
        private DateTime _ntLastDnsFlush = DateTime.MinValue; private bool _ntIfaceRegEnabled = false; private DateTime _ntLastIdleMaint = DateTime.MinValue;
        private string _ntBackupFile = "";
        private bool _ntBackupLoaded = false;
        private bool _ntAdminChecked = false;
        private bool _ntIsAdmin = false;

        private string NtBackupPath
        {
            get
            {
                if (string.IsNullOrEmpty(_ntBackupFile))
                {
                    try { _ntBackupFile = Path.Combine(_dataDir, "nt_backup.txt"); }
                    catch { _ntBackupFile = ""; }
                }
                return _ntBackupFile;
            }
        }

        // --- Pure decision helpers (unit-testable, mirror Frieren profile logic) ---
        public static string ResolveNetworkProfile(bool isGame, bool isCharging, bool onBatteryLow, bool thermalHot)
        {
            if (thermalHot) return "thermal";
            if (onBatteryLow) return "battery";
            if (isGame) return "gaming";
            return "balanced";
        }

        // Priority order for cooldown: thermal(3) > battery(2) > gaming(1) > balanced(0)
        public static int GetNetworkProfilePriority(string profile)
        {
            if (profile == null) return 0;
            if (profile == "thermal") return 3;
            if (profile == "battery") return 2;
            if (profile == "gaming") return 1;
            return 0;
        }

        public static bool IsNetworkProfileMoreUrgent(string a, string b)
        {
            return GetNetworkProfilePriority(a) > GetNetworkProfilePriority(b);
        }

        // The full per-profile tuning map (netsh keys + interface registry + DNS cache).
        public static Dictionary<string, string> GetNetworkProfileNetshSettings(string profile)
        {
            if (profile == null) profile = "balanced";
            switch (profile)
            {
                case "gaming":
                    return BuildNtSettings("normal", "ctcp", "disabled", "enabled", "disabled", "2", "1", "1", "1");
                case "battery":
                    return BuildNtSettings("restricted", "ctcp", "enabled", "disabled", "enabled", "4", "0", "3", "5");
                case "thermal":
                    return BuildNtSettings("disabled", "newreno", "enabled", "disabled", "enabled", "4", "0", "2", "5");
                default: // balanced
                    return BuildNtSettings("normal", "ctcp", "disabled", "enabled", "enabled", "3", "0", "2", "5");
            }
        }

        private static Dictionary<string, string> BuildNtSettings(
            string autotune, string congestion, string timestamps, string rss, string ecn,
            string maxsyn, string tcpNoDelay, string ackFreq, string dnsNegative)
        {
            return new Dictionary<string, string>
            {
                {"autotuninglevel", autotune},
                {"congestion", congestion},
                {"timestamps", timestamps},
                {"rss", rss},
                {"ecn", ecn},
                {"maxsyn", maxsyn},
                {"tcpnodelay", tcpNoDelay},
                {"ackfreq", ackFreq},
                {"dnsnegative", dnsNegative}
            };
        }

        public static bool IsNetworkMaintenanceDue(DateTime last, DateTime now, double intervalHours)
        {
            return (now - last).TotalHours >= intervalHours;
        }

        // netsh "set global" parameter names for our logical keys.
        private static string NtNetshName(string key)
        {
            switch (key)
            {
                case "autotuninglevel": return "autotuninglevel";
                case "congestion": return "congestionprovider";
                case "timestamps": return "timestamps";
                case "rss": return "rss";
                case "ecn": return "ecncapability";
                case "maxsyn": return "maxsynretransmissions";
                default: return "";
            }
        }

        private static bool NtIsNetshKey(string key)
        {
            return NtNetshName(key) != "";
        }

        // --- Public control ---
        public void EnableNetworkTuning()
        {
            if (!NT_ENABLE) return;
            _ntActive = true;
            AddLogOpt("NETWORK_TUNING", "Network / low-latency tuning subsystem enabled (Frieren-inspired)");
            _ntTick = NT_INTERVAL_TICKS - 1; // apply on next tick
        }

        public string GetNetworkTuningProfile() { return _ntEffectiveProfile; }
        public int GetNetworkTuningAppliedCount() { return _ntAppliedCount; }
        public int GetNetworkTuningSkippedCount() { return _ntSkippedCount; }

        public string GetNetworkTuningStatus()
        {
            return string.Format(
                "Enabled: {0}\nProfile: {1}\nAdmin: {2}\nApplied: {3} | Skipped: {4}",
                _ntActive ? "Yes" : "No", _ntEffectiveProfile,
                NtIsAdmin() ? "Yes" : "No (limited)",
                _ntAppliedCount, _ntSkippedCount);
        }

        private bool NtIsAdmin()
        {
            if (_ntAdminChecked) return _ntIsAdmin;
            _ntAdminChecked = true;
            try
            {
                var identity = WindowsIdentity.GetCurrent();
                if (identity == null) return false;
                _ntIsAdmin = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { _ntIsAdmin = false; }
            return _ntIsAdmin;
        }

        // --- Backup (save original values once, terx.sh "BAK" pattern) ---
        private void LoadNtBackup()
        {
            if (_ntBackupLoaded) return;
            _ntBackupLoaded = true;
            try
            {
                string path = NtBackupPath;
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
                // Backup file exists -> we already own the values; just note it.
                AddLogOpt("NETWORK_TUNING", "Loaded existing backup: " + path);
            }
            catch (Exception ex) { CWLog.Error("SmartAgent.NetworkTuning", ex); }
        }

        private void PersistNtBackup(List<string> lines)
        {
            try
            {
                string path = NtBackupPath;
                if (string.IsNullOrEmpty(path)) return;
                try { Directory.CreateDirectory(Path.GetDirectoryName(path)); } catch (Exception ex2) { CWLog.Error("SmartAgent.NetworkTuning", ex2); }
                File.WriteAllLines(path, lines.ToArray());
            }
            catch (Exception ex) { LogError("NetworkTuning.Backup", ex); }
        }

        // Capture current netsh values for the keys we manage.
        private Dictionary<string, string> ReadCurrentNtNetshValues()
        {
            var current = new Dictionary<string, string>();
            try
            {
                string output = RunNetShQuiet("int tcp show global");
                if (string.IsNullOrEmpty(output)) return current;
                string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    int colon = line.IndexOf(':');
                    if (colon < 0) continue;
                    string keyPart = line.Substring(0, colon).Trim();
                    string value = line.Substring(colon + 1).Trim();
                    if (string.IsNullOrEmpty(keyPart) || string.IsNullOrEmpty(value)) continue;
                    string logical = NtKeyFromNetshLabel(keyPart);
                    if (logical != "" && !current.ContainsKey(logical)) current[logical] = value;
                }
            }
            catch (Exception ex) { LogError("NetworkTuning.ReadCurrent", ex); }
            return current;
        }

        private static string NtKeyFromNetshLabel(string label)
        {
            if (label.IndexOf("Auto-Tuning", StringComparison.OrdinalIgnoreCase) >= 0) return "autotuninglevel";
            if (label.IndexOf("Congestion Control Provider", StringComparison.OrdinalIgnoreCase) >= 0) return "congestion";
            if (label.Trim().StartsWith("Timestamps", StringComparison.OrdinalIgnoreCase)) return "timestamps";
            if (label.IndexOf("Receive-Side Scaling", StringComparison.OrdinalIgnoreCase) >= 0) return "rss";
            if (label.IndexOf("ECN Capability", StringComparison.OrdinalIgnoreCase) >= 0) return "ecn";
            if (label.IndexOf("Max Syn Retransmissions", StringComparison.OrdinalIgnoreCase) >= 0) return "maxsyn";
            return "";
        }

        private string RunNetShQuiet(string arguments)
        {
            try
            {
                using (var proc = new Process())
                {
                    proc.StartInfo = new ProcessStartInfo
                    {
                        FileName = "netsh.exe",
                        Arguments = arguments,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    proc.Start();
                    if (!proc.WaitForExit(5000)) { try { proc.Kill(); } catch (Exception ex3) { CWLog.Error("SmartAgent.NetworkTuning", ex3); } return ""; }
                    return proc.StandardOutput.ReadToEnd();
                }
            }
            catch { return ""; }
        }

        // Execute a netsh change; returns true only when exit code is 0.
        private bool RunNtSetGlobal(string key, string value)
        {
            string netshName = NtNetshName(key);
            if (netshName == "") return false;
            try
            {
                using (var proc = new Process())
                {
                    proc.StartInfo = new ProcessStartInfo
                    {
                        FileName = "netsh.exe",
                        Arguments = "int tcp set global " + netshName + "=" + value,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    proc.Start();
                    if (!proc.WaitForExit(5000)) { try { proc.Kill(); } catch (Exception ex4) { CWLog.Error("SmartAgent.NetworkTuning", ex4); } return false; }
                    return proc.ExitCode == 0;
                }
            }
            catch { return false; }
        }

        private void ApplyNtNetshSettings(Dictionary<string, string> desired, Dictionary<string, string> current)
        {
            foreach (var kv in desired)
            {
                string key = kv.Key;
                if (!NtIsNetshKey(key)) continue;
                string cur;
                if (!current.TryGetValue(key, out cur)) cur = "";
                if (cur == kv.Value) { _ntSkippedCount++; continue; }
                if (RunNtSetGlobal(key, kv.Value))
                {
                    _ntAppliedCount++;
                    AddLogOpt("NETWORK_TUNING", "TCP " + key + " -> " + kv.Value);
                }
                else
                {
                    _ntSkippedCount++;
                    AddLogOpt("NETWORK_TUNING", "TCP " + key + " skip (admin needed or failed)");
                }
            }
        }

        // --- Per-interface registry TCP tuning (Nagle / delayed ACK) ---
        private List<string> ReadNtIfaceBackupLines(out List<string> ifaceNames)
        {
            var lines = new List<string>();
            ifaceNames = new List<string>();
            try
            {
                using (RegistryKey baseKey = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces", false))
                {
                    if (baseKey == null) return lines;
                    foreach (string name in baseKey.GetSubKeyNames())
                    {
                        using (RegistryKey iface = baseKey.OpenSubKey(name, false))
                        {
                            if (iface == null) continue;
                            bool hasTcp = iface.GetValue("TCPNoDelay") != null || iface.GetValue("TcpAckFrequency") != null;
                            if (!hasTcp) continue;
                            ifaceNames.Add(name);
                            object noDelay = iface.GetValue("TCPNoDelay");
                            object ack = iface.GetValue("TcpAckFrequency");
                            if (noDelay != null) lines.Add("reg:" + name + ":tcpnodelay=" + noDelay);
                            else lines.Add("reg:" + name + ":tcpnodelay_absent=1");
                            if (ack != null) lines.Add("reg:" + name + ":ackfreq=" + ack);
                            else lines.Add("reg:" + name + ":ackfreq_absent=1");
                        }
                    }
                }
            }
            catch (Exception ex) { LogError("NetworkTuning.IfaceBackup", ex); }
            return lines;
        }

        private void ApplyNtInterfaceSettings(int tcpNoDelay, int ackFreq)
        {
            try
            {
                using (RegistryKey baseKey = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces", true))
                {
                    if (baseKey == null) return;
                    foreach (string name in baseKey.GetSubKeyNames())
                    {
                        using (RegistryKey iface = baseKey.OpenSubKey(name, true))
                        {
                            if (iface == null) continue;
                            if (iface.GetValue("TCPNoDelay") == null && iface.GetValue("TcpAckFrequency") == null) continue;
                            object curDelay = iface.GetValue("TCPNoDelay");
                            if (curDelay == null || Convert.ToInt32(curDelay) != tcpNoDelay)
                            {
                                iface.SetValue("TCPNoDelay", tcpNoDelay, RegistryValueKind.DWord);
                                _ntAppliedCount++;
                                AddLogOpt("NETWORK_TUNING", "IFACE " + name + " TCPNoDelay -> " + tcpNoDelay);
                            }
                            else _ntSkippedCount++;
                            object curAck = iface.GetValue("TcpAckFrequency");
                            if (curAck == null || Convert.ToInt32(curAck) != ackFreq)
                            {
                                iface.SetValue("TcpAckFrequency", ackFreq, RegistryValueKind.DWord);
                                _ntAppliedCount++;
                                AddLogOpt("NETWORK_TUNING", "IFACE " + name + " TcpAckFrequency -> " + ackFreq);
                            }
                            else _ntSkippedCount++;
                        }
                    }
                }
            }
            catch (Exception ex) { LogError("NetworkTuning.IfaceApply", ex); }
        }

        // --- AI-DNS analog: negative cache TTL tuning (Frieren AI DNS optimize) ---
        private void ApplyNtDnsSetting(int negativeTtl)
        {
            try
            {
                using (RegistryKey dns = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Services\\Dnscache\\Parameters", true))
                {
                    if (dns == null) return;
                    object cur = dns.GetValue("MaxNegativeCacheTtl");
                    if (cur != null && Convert.ToInt32(cur) == negativeTtl) { _ntSkippedCount++; return; }
                    dns.SetValue("MaxNegativeCacheTtl", negativeTtl, RegistryValueKind.DWord);
                    _ntAppliedCount++;
                    AddLogOpt("NETWORK_TUNING", "DNS MaxNegativeCacheTtl -> " + negativeTtl);
                }
            }
            catch (Exception ex) { LogError("NetworkTuning.Dns", ex); }
        }

        private void FlushNtDns() { if ((DateTime.Now - _ntLastDnsFlush).TotalMinutes < 10) return; _ntLastDnsFlush = DateTime.Now; FlushNtDnsCore(); } private void FlushNtDnsCore()
        {
            try
            {
                bool ok = false;
                using (var proc = new Process())
                {
                    proc.StartInfo = new ProcessStartInfo
                    {
                        FileName = "ipconfig.exe",
                        Arguments = "/flushdns",
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    proc.Start();
                    if (proc.WaitForExit(5000))
                    {
                        ok = (proc.ExitCode == 0);
                    }
                    else
                    {
                        try { proc.Kill(); } catch { }
                    }
                }
                if (ok) AddLogOpt("NETWORK_TUNING", "DNS cache flushed");
                else CWLog.Warn("SmartAgent.NetworkTuning", "ipconfig /flushdns did not exit with code 0");
            }
            catch (Exception ex5) { CWLog.Error("SmartAgent.NetworkTuning", ex5); }
        }

        // --- Idle + AC maintenance (Frieren "AI DNS & network upkeep" analog) ---
        private void RunNtIdleMaintenance()
        {
            if (!NT_ENABLE_IDLE_MAINT) return;
            try
            {
                if (_isOnBattery) return;
                if (_currentState == null || !_currentState.IsCharging) return;
                if (!IsNetworkMaintenanceDue(_ntLastIdleMaint, DateTime.Now, NT_IDLE_MAINT_INTERVAL_HOURS)) return;
                if (GetSystemIdleMs() < NT_IDLE_REQUIRED_SECONDS * 1000L) return;
                _ntLastIdleMaint = DateTime.Now;
                FlushNtDns();
                _ntAppliedCount++;
                AddLogOpt("NETWORK_TUNING", "Idle maintenance (AC + idle): DNS cache refreshed");
            }
            catch (Exception ex) { LogError("NetworkTuning.Idle", ex); }
        }

        // --- Main cycle (called from Tick, ~60s cadence) ---
        private void ApplyNetworkTuning()
        {
            if (!_ntActive || _simulationMode) return; if (IsWireless80211Active()) { _ntRefreshNote = "skipped (WiFi active)"; _ntSkippedCount++; AddLogOpt("NETWORK_TUNING","Skipped: wireless adapter active - network stack untouched"); return; }
            try
            {
                bool thermalHot = _smoothTemp >= NT_THERMAL_HOT_C || _thermalThrottled;
                bool batteryLow = _isOnBattery && _batteryPercent <= NT_BATTERY_LOW_PERCENT;
                bool game = _currentState != null && _currentState.IsGame;
                bool charging = _currentState != null && _currentState.IsCharging;
                string profile = ResolveNetworkProfile(game, charging, batteryLow, thermalHot);

                // Cooldown: allow urgent lowering (thermal/battery) immediately,
                // but hold profile upgrades for a few minutes (anti-oscillation).
                if (profile != _ntEffectiveProfile &&
                    IsNetworkProfileMoreUrgent(profile, _ntEffectiveProfile) == false &&
                    (DateTime.Now - _ntLastModeChange).TotalMinutes < NT_MODE_SWITCH_COOLDOWN_MINUTES)
                {
                    _ntRefreshNote = "cooldown (skip " + profile + ")";
                    _ntSkippedCount++;
                    RunNtIdleMaintenance();
                    return;
                }

                Dictionary<string, string> settings = GetNetworkProfileNetshSettings(profile);

                // Backup original values once, before first write.
                if (!_ntBackupLoaded)
                {
                    List<string> ifaceNames;
                    List<string> backupLines = ReadNtIfaceBackupLines(out ifaceNames);
                    Dictionary<string, string> current = ReadCurrentNtNetshValues();
                    foreach (var kv in current) backupLines.Add("netsh:" + kv.Key + "=" + kv.Value);
                    if (backupLines.Count > 0) PersistNtBackup(backupLines);
                    _ntBackupLoaded = true;
                }

                // Apply (netsh only actually works when elevated).
                if (NtIsAdmin())
                {
                    Dictionary<string, string> current = ReadCurrentNtNetshValues();
                    ApplyNtNetshSettings(settings, current);
                    int noDelay = settings["tcpnodelay"] == "1" ? 1 : 0;
                    int ackFreq = 0;
                    int.TryParse(settings["ackfreq"], out ackFreq);
                    if (ackFreq == 0) ackFreq = 2;
                    if (_ntIfaceRegEnabled) ApplyNtInterfaceSettings(noDelay, ackFreq); // off by default: never plant per-adapter registry values
                    int dnsNeg = 0;
                    int.TryParse(settings["dnsnegative"], out dnsNeg);
                    if (dnsNeg > 0) ApplyNtDnsSetting(dnsNeg);
                }
                else
                {
                    _ntSkippedCount++;
                }

                if (profile != _ntEffectiveProfile)
                {
                    if (profile == "gaming") FlushNtDns();
                    _ntLastModeChange = DateTime.Now;
                    AddLogOpt("NETWORK_TUNING", "Profile -> " + profile);
                    _ntEffectiveProfile = profile;
                }

                RunNtIdleMaintenance();
            }
            catch (Exception ex) { LogError("NetworkTuning", ex); }
        }

        // --- Restore everything to the backed-up state (called on Stop / rollback) ---
        public bool RestoreNetworkTuning()
        {
            bool ok = true;
            try
            {
                string path = NtBackupPath;
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return true; // nothing to restore

                string[] lines = File.ReadAllLines(path);
                var netshCurrent = new Dictionary<string, string>();
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (line == null) continue;
                    if (line.StartsWith("netsh:"))
                    {
                        int eq = line.IndexOf('=');
                        if (eq < 0) continue;
                        string key = line.Substring(6, eq - 6);
                        string value = line.Substring(eq + 1);
                        if (NtIsNetshKey(key)) netshCurrent[key] = value;
                    }
                    else if (line.StartsWith("reg:"))
                    {
                        // reg:<guid>:key=value
                        int c1 = line.IndexOf(':', 4);
                        if (c1 < 0) continue;
                        string guid = line.Substring(4, c1 - 4);
                        int eq = line.IndexOf('=', c1);
                        if (eq < 0) continue;
                        string key = line.Substring(c1 + 1, eq - c1 - 1);
                        string value = line.Substring(eq + 1);
                        RestoreNtIfaceValue(guid, key, value);
                    }
                }
                // Restore netsh values.
                foreach (var kv in netshCurrent)
                {
                    if (!RunNtSetGlobal(kv.Key, kv.Value)) ok = false;
                }
                try { File.Delete(path); } catch (Exception ex6) { CWLog.Error("SmartAgent.NetworkTuning", ex6); }
                _ntBackupLoaded = false;
                AddLogOpt("NETWORK_TUNING", "Restored original network tuning settings");
            }
            catch (Exception ex) { LogError("NetworkTuning.Restore", ex); ok = false; }
            return ok;
        }

        internal static bool IsWireless80211Active() { try { foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()) { if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211 && ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up) return true; } } catch { } return false; } public void SetInterfaceRegistryTuning(bool on) { _ntIfaceRegEnabled = on; } private void RestoreNtIfaceValue(string guid, string key, string value)
        {
            try
            {
                using (RegistryKey baseKey = Registry.LocalMachine.OpenSubKey("SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces", true))
                {
                    if (baseKey == null) return;
                    using (RegistryKey iface = baseKey.OpenSubKey(guid, true))
                    {
                        if (iface == null) return;
                        if (key.EndsWith("_absent"))
                        {
                            string realKey = key.Substring(0, key.Length - "_absent".Length);
                            iface.DeleteValue(realKey, false);
                        }
                        else
                        {
                            int v;
                            if (int.TryParse(value, out v))
                                iface.SetValue(key, v, RegistryValueKind.DWord);
                        }
                    }
                }
            }
            catch (Exception ex) { LogError("NetworkTuning.RestoreIface", ex); }
        }
    }
}
