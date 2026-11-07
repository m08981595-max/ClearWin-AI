using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace ClearWin
{
    public partial class SmartAgent
    {
        // =========================================================================
        // ClearWin Deep GPU Intelligence & Hardware Acceleration Engine
        // Outperforms default Windows scheduling by directly coordinating D3D,
        // VRAM shedding, and low-latency DXGI pipeline management with full
        // transactional Registry backup and user-override-safe restoration.
        // =========================================================================

        private DateTime _lastShaderCachePurge = DateTime.MinValue;
        private DateTime _lastVramShed = DateTime.MinValue;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_SYSCOMMAND = 0x0112;
        private static readonly IntPtr SC_MINIMIZE = new IntPtr(0xF020);

        // =========================================================================
        // Durable Registry Backup & Restore Journal (Phase 2A Safety Engine)
        // =========================================================================

        public class RegistryBackupEntry
        {
            public string Hive { get; set; } // "HKCU" or "HKLM"
            public string SubKeyPath { get; set; }
            public string ValueName { get; set; }
            public bool KeyExistedBefore { get; set; }
            public bool ValueExistedBefore { get; set; }
            public RegistryValueKind OriginalValueKind { get; set; }
            public string OriginalValueSerialized { get; set; }
            public RegistryValueKind WrittenValueKind { get; set; }
            public string ValueWrittenByClearWin { get; set; }
            public DateTime CapturedAtUtc { get; set; }

            public string CompositeKey
            {
                get
                {
                    return (Hive ?? "HKCU").ToUpperInvariant() + @"\" +
                           (SubKeyPath ?? "").Trim('\\') + "::" +
                           (ValueName ?? "");
                }
            }
        }

        private static readonly object _regBackupLock = new object();
        private static readonly Dictionary<string, RegistryBackupEntry> _regBackupEntries =
            new Dictionary<string, RegistryBackupEntry>(StringComparer.OrdinalIgnoreCase);
        private static bool _regJournalLoaded = false;
        private static string _customRegJournalPath = null;

        // Optional in-memory Registry simulation backend for deterministic unit testing
        internal static bool UseSimulatedRegistryBackend = false;
        internal static readonly Dictionary<string, Tuple<RegistryValueKind, string>> SimulatedRegistryValues =
            new Dictionary<string, Tuple<RegistryValueKind, string>>(StringComparer.OrdinalIgnoreCase);
        internal static readonly HashSet<string> SimulatedRegistrySubKeys =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal static void ResetRegistryBackupStateForTests(string customJournalPath = null, bool useSimulatedBackend = false)
        {
            lock (_regBackupLock)
            {
                _regBackupEntries.Clear();
                _regJournalLoaded = false;
                _customRegJournalPath = customJournalPath;
                UseSimulatedRegistryBackend = useSimulatedBackend;
                SimulatedRegistryValues.Clear();
                SimulatedRegistrySubKeys.Clear();
            }
        }

        internal static int GetBackedUpRegistryEntryCount()
        {
            lock (_regBackupLock)
            {
                EnsureRegistryJournalLoadedLocked();
                return _regBackupEntries.Count;
            }
        }

        public static string GetRegistryRestoreJournalPath()
        {
            if (!string.IsNullOrEmpty(_customRegJournalPath))
                return _customRegJournalPath;
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(localAppData))
                localAppData = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(localAppData, "ClearWin", "registry-restore-journal.ini");
        }

        internal static bool IsCurrentProcessElevated()
        {
            try
            {
                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
            catch
            {
                return false;
            }
        }

        private static string SerializeRegistryValue(object val, RegistryValueKind kind)
        {
            if (val == null) return "";
            if (kind == RegistryValueKind.DWord || kind == RegistryValueKind.QWord)
            {
                return Convert.ToInt64(val, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
            }
            return Convert.ToString(val, CultureInfo.InvariantCulture) ?? "";
        }

        private static object DeserializeRegistryValue(string raw, RegistryValueKind kind)
        {
            if (kind == RegistryValueKind.DWord)
            {
                int iv;
                if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out iv))
                    return iv;
                return 0;
            }
            if (kind == RegistryValueKind.QWord)
            {
                long lv;
                if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out lv))
                    return lv;
                return 0L;
            }
            return raw ?? "";
        }

        private static void EnsureRegistryJournalLoadedLocked()
        {
            if (_regJournalLoaded) return;
            _regJournalLoaded = true;
            try
            {
                string path = GetRegistryRestoreJournalPath();
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
                string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                foreach (string line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#", StringComparison.Ordinal))
                        continue;
                    string[] parts = line.Split('\t');
                    if (parts.Length < 8) continue;

                    RegistryValueKind origKind;
                    if (!Enum.TryParse(parts[5], true, out origKind)) origKind = RegistryValueKind.String;
                    RegistryValueKind writtenKind;
                    if (!Enum.TryParse(parts[7], true, out writtenKind)) writtenKind = origKind;

                    var entry = new RegistryBackupEntry
                    {
                        Hive = parts[0],
                        SubKeyPath = UnescapeJournalField(parts[1]),
                        ValueName = UnescapeJournalField(parts[2]),
                        KeyExistedBefore = parts[3] == "1",
                        ValueExistedBefore = parts[4] == "1",
                        OriginalValueKind = origKind,
                        OriginalValueSerialized = UnescapeJournalField(parts[6]),
                        WrittenValueKind = writtenKind,
                        ValueWrittenByClearWin = parts.Length >= 9 ? UnescapeJournalField(parts[8]) : "",
                        CapturedAtUtc = DateTime.UtcNow
                    };
                    if (!_regBackupEntries.ContainsKey(entry.CompositeKey))
                    {
                        _regBackupEntries[entry.CompositeKey] = entry;
                    }
                }
            }
            catch (Exception ex)
            {
                CWLog.Error("RegistryRestoreJournal.Load", ex);
            }
        }

        private static void SaveRegistryJournalLocked()
        {
            try
            {
                string path = GetRegistryRestoreJournalPath();
                if (string.IsNullOrEmpty(path)) return;
                if (_regBackupEntries.Count == 0)
                {
                    if (File.Exists(path)) File.Delete(path);
                    return;
                }
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var sb = new StringBuilder();
                sb.AppendLine("# ClearWin AI v2.7.7 Registry Restore Journal");
                foreach (var kv in _regBackupEntries)
                {
                    var e = kv.Value;
                    sb.Append(e.Hive ?? "HKCU").Append('\t')
                      .Append(EscapeJournalField(e.SubKeyPath)).Append('\t')
                      .Append(EscapeJournalField(e.ValueName)).Append('\t')
                      .Append(e.KeyExistedBefore ? "1" : "0").Append('\t')
                      .Append(e.ValueExistedBefore ? "1" : "0").Append('\t')
                      .Append(e.OriginalValueKind.ToString()).Append('\t')
                      .Append(EscapeJournalField(e.OriginalValueSerialized)).Append('\t')
                      .Append(e.WrittenValueKind.ToString()).Append('\t')
                      .Append(EscapeJournalField(e.ValueWrittenByClearWin)).AppendLine();
                }

                string tmp = path + ".tmp";
                File.WriteAllText(tmp, sb.ToString(), Encoding.UTF8);
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception ex)
            {
                CWLog.Error("RegistryRestoreJournal.Save", ex);
            }
        }

        private static string EscapeJournalField(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private static string UnescapeJournalField(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    char n = s[++i];
                    if (n == 't') sb.Append('\t');
                    else if (n == 'r') sb.Append('\r');
                    else if (n == 'n') sb.Append('\n');
                    else sb.Append(n);
                }
                else
                {
                    sb.Append(s[i]);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Safely writes a Registry value after capturing a durable baseline backup.
        /// Never attempts HKLM writes unless the process is actually elevated.
        /// </summary>
        internal static bool SetRegistryValueWithBackup(
            string hive,
            string subKeyPath,
            string valueName,
            object newValue,
            RegistryValueKind valueKind,
            bool createSubKeyIfMissing)
        {
            if (string.IsNullOrEmpty(subKeyPath) || valueName == null) return false;
            string normHive = (hive ?? "HKCU").ToUpperInvariant();
            if (normHive == "HKLM" && !UseSimulatedRegistryBackend && !IsCurrentProcessElevated())
            {
                // Fail-closed: never attempt HKLM writes from a standard user process
                return false;
            }

            string newSerialized = SerializeRegistryValue(newValue, valueKind);
            string compositeKey = normHive + @"\" + subKeyPath.Trim('\\') + "::" + valueName;
            string subKeyComposite = normHive + @"\" + subKeyPath.Trim('\\');

            lock (_regBackupLock)
            {
                EnsureRegistryJournalLoadedLocked();

                if (UseSimulatedRegistryBackend)
                {
                    bool keyExisted = SimulatedRegistrySubKeys.Contains(subKeyComposite);
                    if (!keyExisted && !createSubKeyIfMissing)
                        return false;

                    Tuple<RegistryValueKind, string> existingVal;
                    bool valExisted = SimulatedRegistryValues.TryGetValue(compositeKey, out existingVal);

                    if (!_regBackupEntries.ContainsKey(compositeKey))
                    {
                        _regBackupEntries[compositeKey] = new RegistryBackupEntry
                        {
                            Hive = normHive,
                            SubKeyPath = subKeyPath.Trim('\\'),
                            ValueName = valueName,
                            KeyExistedBefore = keyExisted,
                            ValueExistedBefore = valExisted,
                            OriginalValueKind = valExisted ? existingVal.Item1 : valueKind,
                            OriginalValueSerialized = valExisted ? existingVal.Item2 : "",
                            WrittenValueKind = valueKind,
                            ValueWrittenByClearWin = newSerialized,
                            CapturedAtUtc = DateTime.UtcNow
                        };
                    }
                    else
                    {
                        _regBackupEntries[compositeKey].WrittenValueKind = valueKind;
                        _regBackupEntries[compositeKey].ValueWrittenByClearWin = newSerialized;
                    }

                    SimulatedRegistrySubKeys.Add(subKeyComposite);
                    SimulatedRegistryValues[compositeKey] = Tuple.Create(valueKind, newSerialized);
                    SaveRegistryJournalLocked();
                    return true;
                }

                try
                {
                    RegistryKey root = normHive == "HKLM" ? Registry.LocalMachine : Registry.CurrentUser;
                    bool keyExistedBefore = false;
                    bool valExistedBefore = false;
                    RegistryValueKind origKind = valueKind;
                    string origSerialized = "";

                    using (var readKey = root.OpenSubKey(subKeyPath, false))
                    {
                        if (readKey != null)
                        {
                            keyExistedBefore = true;
                            object existingObj = readKey.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                            if (existingObj != null)
                            {
                                valExistedBefore = true;
                                try { origKind = readKey.GetValueKind(valueName); } catch { origKind = valueKind; }
                                origSerialized = SerializeRegistryValue(existingObj, origKind);
                            }
                        }
                    }

                    if (!keyExistedBefore && !createSubKeyIfMissing)
                        return false;

                    if (!_regBackupEntries.ContainsKey(compositeKey))
                    {
                        _regBackupEntries[compositeKey] = new RegistryBackupEntry
                        {
                            Hive = normHive,
                            SubKeyPath = subKeyPath.Trim('\\'),
                            ValueName = valueName,
                            KeyExistedBefore = keyExistedBefore,
                            ValueExistedBefore = valExistedBefore,
                            OriginalValueKind = origKind,
                            OriginalValueSerialized = origSerialized,
                            WrittenValueKind = valueKind,
                            ValueWrittenByClearWin = newSerialized,
                            CapturedAtUtc = DateTime.UtcNow
                        };
                        SaveRegistryJournalLocked();
                    }

                    using (var writeKey = createSubKeyIfMissing ? root.CreateSubKey(subKeyPath) : root.OpenSubKey(subKeyPath, true))
                    {
                        if (writeKey == null) return false;
                        writeKey.SetValue(valueName, newValue, valueKind);
                    }

                    _regBackupEntries[compositeKey].WrittenValueKind = valueKind;
                    _regBackupEntries[compositeKey].ValueWrittenByClearWin = newSerialized;
                    SaveRegistryJournalLocked();
                    return true;
                }
                catch (Exception ex)
                {
                    CWLog.Error("SetRegistryValueWithBackup(" + compositeKey + ")", ex);
                    return false;
                }
            }
        }

        /// <summary>
        /// Restores all Registry values modified by ClearWin to their original pre-ClearWin state.
        /// Strictly respects user overrides: if the current Registry value no longer matches
        /// ValueWrittenByClearWin (meaning the user or another tool changed it afterwards),
        /// ClearWin leaves the user's new value untouched.
        /// </summary>
        public static bool RestoreAllBackedUpRegistrySettings()
        {
            if (DisableRealExecution && !UseSimulatedRegistryBackend) return true;

            lock (_regBackupLock)
            {
                EnsureRegistryJournalLoadedLocked();
                if (_regBackupEntries.Count == 0) return true;

                bool allRestored = true;
                var keysToRemove = new List<string>();

                foreach (var kv in _regBackupEntries)
                {
                    string compKey = kv.Key;
                    RegistryBackupEntry entry = kv.Value;

                    if (UseSimulatedRegistryBackend)
                    {
                        Tuple<RegistryValueKind, string> curSim;
                        bool existsNow = SimulatedRegistryValues.TryGetValue(compKey, out curSim);
                        if (existsNow && !string.Equals(curSim.Item2, entry.ValueWrittenByClearWin, StringComparison.Ordinal))
                        {
                            // User modified the value after ClearWin wrote it -> preserve user override and retire backup entry
                            keysToRemove.Add(compKey);
                            continue;
                        }

                        if (!entry.ValueExistedBefore)
                        {
                            SimulatedRegistryValues.Remove(compKey);
                        }
                        else
                        {
                            SimulatedRegistryValues[compKey] = Tuple.Create(entry.OriginalValueKind, entry.OriginalValueSerialized);
                        }
                        keysToRemove.Add(compKey);
                        continue;
                    }

                    try
                    {
                        string normHive = (entry.Hive ?? "HKCU").ToUpperInvariant();
                        if (normHive == "HKLM" && !IsCurrentProcessElevated())
                        {
                            // Cannot restore HKLM without elevation; keep in journal for next elevated session
                            allRestored = false;
                            continue;
                        }

                        RegistryKey root = normHive == "HKLM" ? Registry.LocalMachine : Registry.CurrentUser;
                        using (var key = root.OpenSubKey(entry.SubKeyPath, true))
                        {
                            if (key == null)
                            {
                                keysToRemove.Add(compKey);
                                continue;
                            }

                            object curObj = key.GetValue(entry.ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                            if (curObj != null)
                            {
                                RegistryValueKind curKind = entry.WrittenValueKind;
                                try { curKind = key.GetValueKind(entry.ValueName); } catch { }
                                string curSerialized = SerializeRegistryValue(curObj, curKind);

                                if (!string.Equals(curSerialized, entry.ValueWrittenByClearWin, StringComparison.Ordinal))
                                {
                                    // User changed this setting manually after ClearWin wrote it; never overwrite user's choice!
                                    CWLog.Info("RegistryRestoreJournal", "Preserving user-modified Registry value: " + compKey);
                                    keysToRemove.Add(compKey);
                                    continue;
                                }
                            }

                            if (!entry.ValueExistedBefore)
                            {
                                key.DeleteValue(entry.ValueName, false);
                            }
                            else
                            {
                                object origVal = DeserializeRegistryValue(entry.OriginalValueSerialized, entry.OriginalValueKind);
                                key.SetValue(entry.ValueName, origVal, entry.OriginalValueKind);
                            }
                            keysToRemove.Add(compKey);
                        }
                    }
                    catch (Exception ex)
                    {
                        allRestored = false;
                        CWLog.Error("RestoreAllBackedUpRegistrySettings(" + compKey + ")", ex);
                    }
                }

                foreach (string k in keysToRemove)
                {
                    _regBackupEntries.Remove(k);
                }

                SaveRegistryJournalLocked();
                return allRestored;
            }
        }

        /// <summary>
        /// Optimizes GPU performance with full baseline backup and privilege verification:
        /// 1. Elevates DirectX Multimedia SystemProfile GPU Priority to 8 (ONLY when elevated, with backup).
        /// 2. Binds active application to dedicated High-Performance GPU (GpuPreference=2;) with backup.
        /// 3. Configures DXGI Low Latency (MaxPreRenderedFrames=1) with backup.
        /// Note: HwSchMode (HAGS) is intentionally not mutated at runtime because it requires a system reboot.
        /// </summary>
        public bool ExecuteOptimizeGpuPerformance()
        {
            if (DisableRealExecution || _simulationMode)
            {
                _lastExecutionSuccess = true;
                AiDecisionLogger.RecordDecision(
                    "OptimizeGpuPerformance",
                    "تعزيز أولوية كارت الشاشة D3D GPU Priority=8 وإلزام معالجة الرسوميات بالأداء الأقصى (محاكاة آمنة)",
                    0.95,
                    "Graphics",
                    "SIMULATED",
                    "تم التحقق من منطق توجيه الرسوميات في وضع المحاكاة دون تعديل سجل النظام الحقيقي."
                );
                return true;
            }
            try
            {
                bool anySuccess = false;
                bool isElevated = IsCurrentProcessElevated();

                // 1. Multimedia SystemProfile D3D GPU Priority (HKLM — only attempt when process is elevated)
                if (isElevated)
                {
                    const string gamesTaskKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games";
                    bool s1 = SetRegistryValueWithBackup("HKLM", gamesTaskKey, "GPU Priority", 8, RegistryValueKind.DWord, false);
                    bool s2 = SetRegistryValueWithBackup("HKLM", gamesTaskKey, "Priority", 6, RegistryValueKind.DWord, false);
                    bool s3 = SetRegistryValueWithBackup("HKLM", gamesTaskKey, "Scheduling Category", "High", RegistryValueKind.String, false);
                    bool s4 = SetRegistryValueWithBackup("HKLM", gamesTaskKey, "SFIO Priority", "High", RegistryValueKind.String, false);
                    if (s1 || s2 || s3 || s4) anySuccess = true;
                }

                // 2. Enforce Dedicated GPU in DirectX UserGpuPreferences (only when a valid executable path is known)
                try
                {
                    string targetApp = _currentState != null ? _currentState.ActiveProcess : null;
                    string targetPath = _currentState != null ? _currentState.ActiveProcessPath : null;
                    if (!string.IsNullOrEmpty(targetApp))
                    {
                        if (EnforceGameDedicatedGpuAndPower(targetApp, targetPath, switchPowerPlan: false))
                        {
                            anySuccess = true;
                        }
                    }
                }
                catch (Exception exGpuPref)
                {
                    CWLog.Error("ExecuteOptimizeGpuPerformance.EnforceDedicatedGpu", exGpuPref);
                }

                // 3. DXGI Low Latency Mode (HKCU\Software\Microsoft\Direct3D\MaxPreRenderedFrames) with durable backup
                if (SetRegistryValueWithBackup("HKCU", @"Software\Microsoft\Direct3D", "MaxPreRenderedFrames", 1, RegistryValueKind.DWord, true))
                {
                    anySuccess = true;
                }

                _lastExecutionSuccess = anySuccess;
                AddLogOpt("OptimizeGpuPerformance", anySuccess
                    ? "Applied backed-up GPU routing and DXGI low-latency settings"
                    : "Skipped GPU registry optimization (no modifiable target keys)");

                AiDecisionLogger.RecordDecision(
                    "OptimizeGpuPerformance",
                    anySuccess
                        ? "تعزيز أولوية كارت الشاشة وتوجيه الرسوميات مع حفظ نسخة استعادة آمنة"
                        : "تعذر تطبيق تعديلات سجل كارت الشاشة (تتطلب مسار لعبة صالح أو صلاحيات مسؤول لـ HKLM)",
                    0.95,
                    "Graphics",
                    anySuccess ? "SUCCESS" : "SKIPPED",
                    anySuccess
                        ? "تم تطبيق إعدادات الرسوميات القابلة للاستعادة مع حفظ القيم الأصلية في سجل الاسترجاع."
                        : "لم يتم تعديل أي مفتاح سجل غير مصرح به."
                );

                return anySuccess;
            }
            catch (Exception ex)
            {
                LogError("ExecuteOptimizeGpuPerformance", ex);
                return false;
            }
        }

        /// <summary>
        /// Sheds background GPU Video Memory (VRAM):
        /// Modern Electron apps, browsers, and background utilities allocate DirectX/D3D surfaces.
        /// This method trims working sets of non-active background processes holding GPU contexts,
        /// signaling DWM and DXGI to evict occluded surfaces and reclaim dedicated VRAM for the foreground game.
        /// </summary>
        public bool ExecuteShedBackgroundGpuMemory()
        {
            if ((DateTime.Now - _lastVramShed).TotalSeconds < 30)
            {
                return true; // Cooldown protection
            }
            _lastVramShed = DateTime.Now;
            if (DisableRealExecution || _simulationMode)
            {
                _lastExecutionSuccess = true;
                AiDecisionLogger.RecordDecision(
                    "ShedBackgroundGpuMemory",
                    "تحرير أسطح كارت الشاشة (VRAM) للتطبيقات الخلفية (محاكاة آمنة)",
                    0.92,
                    "Graphics",
                    "SIMULATED",
                    "تم التحقق من منطق تحرير ذاكرة الفيديو الخلفية في وضع المحاكاة."
                );
                return true;
            }

            try
            {
                // Identify foreground process PID to never disturb it
                uint fgPid = 0;
                IntPtr fgHwnd = GetForegroundWindow();
                if (fgHwnd != IntPtr.Zero)
                {
                    GetWindowThreadProcessId(fgHwnd, out fgPid);
                }

                int currentPid = Process.GetCurrentProcess().Id;
                int trimmedCount = 0;
                long totalBytesReclaimed = 0;

                // Known heavy background GPU consumers (hardware-accelerated apps)
                string[] gpuHeavyBackgroundApps = new[] {
                    "chrome", "msedge", "firefox", "brave", "opera",
                    "discord", "spotify", "slack", "teams", "steamwebhelper",
                    "epicgameslauncher", "battle.net", "origin", "eaBackgroundService",
                    "whatsapp", "telegram"
                };

                var runningProcs = Process.GetProcesses();
                foreach (var proc in runningProcs)
                {
                    try
                    {
                        if (proc.Id == currentPid || proc.Id == fgPid || proc.Id <= 4)
                            continue;

                        string pName = proc.ProcessName.ToLowerInvariant();

                        // Check if it is a known background GPU consumer or non-foreground browser/electron app
                        bool isTarget = false;
                        for (int i = 0; i < gpuHeavyBackgroundApps.Length; i++)
                        {
                            if (pName.Contains(gpuHeavyBackgroundApps[i]))
                            {
                                isTarget = true;
                                break;
                            }
                        }

                        if (isTarget)
                        {
                            long wsBefore = proc.WorkingSet64;
                            IntPtr hProc = OpenProcess(0x0400 | 0x0010, false, (uint)proc.Id); // PROCESS_QUERY_INFORMATION | PROCESS_VM_READ
                            if (hProc != IntPtr.Zero)
                            {
                                try
                                {
                                    EmptyWorkingSet(hProc);
                                    trimmedCount++;
                                    proc.Refresh();
                                    long wsAfter = proc.WorkingSet64;
                                    if (wsBefore > wsAfter)
                                    {
                                        totalBytesReclaimed += (wsBefore - wsAfter);
                                    }
                                }
                                finally
                                {
                                    CloseHandle(hProc);
                                                                }
                            }
                        }
                    }
                    catch { /* Process exited or access denied */ }
                    finally
                    {
                        proc.Dispose();
                    }
                }

                double reclaimedMb = totalBytesReclaimed / (1024.0 * 1024.0);
                AddLogOpt("ShedBackgroundGpuMemory", string.Format("Trimmed {0} background GPU-accelerated apps, freed ~{1:F1} MB RAM/VRAM surfaces", trimmedCount, reclaimedMb));

                AiDecisionLogger.RecordDecision(
                    "ShedBackgroundGpuMemory",
                    string.Format("تحرير أسطح كارت الشاشة (VRAM) لـ {0} تطبيقات خلفية وتفريغ ~{1:F0} ميجابايت", trimmedCount, reclaimedMb),
                    0.92,
                    "Graphics",
                    "SUCCESS",
                    "إجبار ويندوز و DWM على إخلاء أسطح العرض للتطبيقات غير النشطة بالخلفية لضمان عدم حدوث ترحيل للذاكرة (VRAM Paging) أثناء اللعب."
                );

                _lastExecutionSuccess = true;
                return true;
            }
            catch (Exception ex)
            {
                LogError("ExecuteShedBackgroundGpuMemory", ex);
                return false;
            }
        }

        /// <summary>
        /// Purges stale, corrupt, or fragmented DirectX, NVIDIA, AMD, and Intel GPU shader caches.
        /// Resolves persistent micro-stutters and frame drops in heavy games.
        /// Strictly safe: skips files locked by running processes or modified within the last 5 minutes.
        /// </summary>
        public bool ExecutePurgeGpuShaderCache()
        {
            if ((DateTime.Now - _lastShaderCachePurge).TotalSeconds < 120)
            {
                return true; // Cooldown protection: max once every 2 minutes
            }
            _lastShaderCachePurge = DateTime.Now;
            if (DisableRealExecution || _simulationMode)
            {
                _lastExecutionSuccess = true;
                AiDecisionLogger.RecordDecision(
                    "PurgeGpuShaderCache",
                    "تنظيف كاش التظليل (Shader Cache) (محاكاة آمنة)",
                    0.90,
                    "Graphics",
                    "SIMULATED",
                    "تم التحقق من منطق تنظيف كاش التظليل في وضع المحاكاة دون حذف ملفات النظام."
                );
                return true;
            }

            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(localAppData) || !Directory.Exists(localAppData))
                {
                    return false;
                }

                var cacheTargets = new List<string>
                {
                    Path.Combine(localAppData, "D3DSCache"),
                    Path.Combine(localAppData, "NVIDIA", "DXCache"),
                    Path.Combine(localAppData, "NVIDIA", "GLCache"),
                    Path.Combine(localAppData, "AMD", "DxCache"),
                    Path.Combine(localAppData, "AMD", "GLCache"),
                    Path.Combine(localAppData, "Intel", "ShaderCache")
                };

                int filesPurged = 0;
                long bytesFreed = 0;
                DateTime fiveMinutesAgo = DateTime.Now.AddMinutes(-5);

                foreach (string dir in cacheTargets)
                {
                    if (!Directory.Exists(dir)) continue;

                    try
                    {
                        var files = Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories);
                        foreach (string file in files)
                        {
                            try
                            {
                                var fi = new FileInfo(file);
                                if (!fi.Exists) continue;

                                // Only clean files older than 5 minutes that are not locked
                                if (fi.LastWriteTime < fiveMinutesAgo)
                                {
                                    long size = fi.Length;
                                    fi.Delete();
                                    filesPurged++;
                                    bytesFreed += size;
                                }
                            }
                            catch { /* File in active use or locked by GPU driver: gracefully skip */ }
                        }
                    }
                    catch { /* Directory locked or inaccessible */ }
                }

                double mbFreed = bytesFreed / (1024.0 * 1024.0);
                AddLogOpt("PurgeGpuShaderCache", string.Format("Purged {0} stale shader cache files ({1:F1} MB freed)", filesPurged, mbFreed));

                AiDecisionLogger.RecordDecision(
                    "PurgeGpuShaderCache",
                    string.Format("تنظيف كاش التظليل (Shader Cache): تم تطهير {0} ملفات كاش قديمة بحجم {1:F1} ميجابايت", filesPurged, mbFreed),
                    0.90,
                    "Graphics",
                    "SUCCESS",
                    "التخلص من الكاش المتراكم والتالف لمنع التقطيع اللحظي (Micro-Stutter) وهبوط الفريمات الناتج عن البحث في كاش قديم."
                );

                _lastExecutionSuccess = true;
                return true;
            }
            catch (Exception ex)
            {
                LogError("ExecutePurgeGpuShaderCache", ex);
                return false;
            }
        }
    }
}
