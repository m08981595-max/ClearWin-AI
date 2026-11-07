using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace ClearWin
{
    public partial class SmartAgent
    {
        // =====================================================================
        // UI FRAME PACING / SMOOTHNESS SUBSYSTEM (ATLASX-inspired)
        // Adapted from the Android "ATLASX UI FRAME PACING" module to Windows:
        //   1. Refresh-rate policy per profile (clamped to real monitor capability)
        //   2. UI animation / visual-effects policy per profile
        //   3. Conservative + only-if-changed writes with backup & restore
        //   4. Mode-switch cooldown (anti-oscillation, like ATLASX 15min gate)
        //   5. Idle + AC gated lightweight maintenance (ATLASX "UI compile" analog)
        // Activation is explicit (EnableFramePacing) so tests/simulation never
        // touch the real display.
        // =====================================================================

        // --- Config (mirrors ATLASX config.sh, Windows-tuned) ---
        private static readonly bool FP_ENABLE = true;
        private const int FP_INTERVAL_TICKS = 60;             // ~30s check cadence (Desktop)
        private const int FP_HZ_SCAN_INTERVAL_HOURS = 24;
        private const int FP_MODE_SWITCH_COOLDOWN_MINUTES = 10;
        private const int FP_IDLE_MAINT_INTERVAL_HOURS = 6;
        private const int FP_IDLE_REQUIRED_SECONDS = 900;     // 15 min without input
        private const int FP_BATTERY_LOW_PERCENT = 20;
        private const double FP_THERMAL_HOT_C = 88;
        private static readonly bool FP_ENABLE_IDLE_MAINT = true;

        // Per-profile max refresh (min is fixed 60 for compatibility).
        private static readonly Dictionary<string, int> FP_HZ = new Dictionary<string, int>
        {
            {"balanced", 60}, {"smooth", 90}, {"gaming", 90}, {"battery", 60}, {"thermal", 60}
        };

        // Smoothness level per profile (0 = full UI, 1 = reduced, 2 = max performance).
        private static readonly Dictionary<string, int> FP_ANIM = new Dictionary<string, int>
        {
            {"balanced", 0}, {"smooth", 1}, {"gaming", 1}, {"battery", 2}, {"thermal", 2}
        };

        private bool _fpActive = false;
        private int _fpTick = 0;
        private int _fpDeviceMaxHz = 0;
        private DateTime _fpLastScan = DateTime.MinValue;
        private DateTime _fpLastModeChange = DateTime.MinValue;
        private DateTime _fpLastIdleMaint = DateTime.MinValue;
        private int _fpAppliedCount = 0;
        private int _fpSkippedCount = 0;
        private string _fpEffectiveProfile = "balanced";
        private string _fpRefreshNote = "";
        private string _fpBackupFile = "";
        private bool _fpBackupLoaded = false;
        private int _fpBackupRefresh = 0;
        private bool _fpAnimBackupValid = false;
        private int _fpBackupMenu = 0;
        private int _fpBackupTooltip = 0;
        private int _fpBackupTaskbar = 0;
        private int _fpBackupClientArea = 0;

        private string FpBackupPath
        {
            get
            {
                if (string.IsNullOrEmpty(_fpBackupFile))
                {
                    try { _fpBackupFile = Path.Combine(_dataDir, "fp_backup.txt"); }
                    catch { _fpBackupFile = ""; }
                }
                return _fpBackupFile;
            }
        }

        // --- DEVMODE (64-bit display layout) ---
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public ushort dmSpecVersion;
            public ushort dmDriverVersion;
            public ushort dmSize;
            public ushort dmDriverExtra;
            public uint dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public uint dmDisplayOrientation;
            public uint dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public ushort dmLogPixels;
            public uint dmBitsPerPel;
            public uint dmPelsWidth;
            public uint dmPelsHeight;
            public uint dmDisplayFlags;
            public uint dmDisplayFrequency;
            public uint dmICMMethod;
            public uint dmICMIntent;
            public uint dmMediaType;
            public uint dmDitherType;
            public uint dmReserved1;
            public uint dmReserved2;
            public uint dmPanningWidth;
            public uint dmPanningHeight;
        }

        private const uint ENUM_CURRENT_SETTINGS = 0xFFFFFFFF;
        private const int DISP_CHANGE_SUCCESSFUL = 0;
        private const uint CDS_TEST = 0x2;
        private const uint CDS_UPDATEREGISTRY = 0x1;
        private const uint DM_PELSWIDTH = 0x00080000;
        private const uint DM_PELSHEIGHT = 0x00100000;
        private const uint DM_DISPLAYFREQUENCY = 0x00400000;

        private const uint SPI_GETMENUANIMATION = 0x1001;
        private const uint SPI_SETMENUANIMATION = 0x1002;
        private const uint SPI_GETKEYBOARDCUES = 0x100A;
        private const uint SPI_SETKEYBOARDCUES = 0x100B;
        private const uint SPI_GETTASKBARANIMATION = 0x100C;
        private const uint SPI_SETTASKBARANIMATION = 0x100D;
        private const uint SPI_GETTOOLTIPANIMATION = 0x1016;
        private const uint SPI_SETTOOLTIPANIMATION = 0x1017;
        private const uint SPI_GETCLIENTAREAANIMATION = 0x1042;
        private const uint SPI_SETCLIENTAREAANIMATION = 0x1043;
        private const uint SPIF_UPDATEINIFILE = 0x01;
        private const uint SPIF_SENDCHANGE = 0x02;

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern bool EnumDisplaySettings(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);
        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern int ChangeDisplaySettingsEx(string lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref int pvParam, uint fWinIni);

        // LASTINPUTINFO and GetLastInputInfo are already defined in SmartAgent.cs

        // --- Public control ---
        public void EnableFramePacing()
        {
            if (!FP_ENABLE) return;
            _fpActive = true;
            AddLogOpt("FRAME_PACING", "UI Frame Pacing subsystem enabled (ATLASX-inspired)");
            _fpTick = FP_INTERVAL_TICKS - 1; // apply on next tick
        }

        public int GetDeviceMaxHz()
        {
            TryScanDeviceHz();
            return _fpDeviceMaxHz;
        }

        public string GetFramePacingProfile() { return _fpEffectiveProfile; }

        public int GetFramePacingAppliedCount() { return _fpAppliedCount; }
        public int GetFramePacingSkippedCount() { return _fpSkippedCount; }

        public string GetFramePacingStatus()
        {
            return string.Format(
                "Enabled: {0}\nProfile: {1}\nDeviceMaxHz: {2}\nRefresh: {3}\nApplied: {4} | Skipped: {5}",
                _fpActive ? "Yes" : "No", _fpEffectiveProfile,
                _fpDeviceMaxHz > 0 ? _fpDeviceMaxHz.ToString() : "scanning...",
                string.IsNullOrEmpty(_fpRefreshNote) ? "no change" : _fpRefreshNote,
                _fpAppliedCount, _fpSkippedCount);
        }

        // --- Pure decision helpers (unit-testable, mirror ATLASX resolve_effective_profile) ---
        public static string ResolveFramePacingProfile(bool isGame, bool isCharging, bool onBatteryLow, bool thermalHot)
        {
            if (thermalHot) return "thermal";
            if (onBatteryLow) return "battery";
            if (isGame) return "gaming";
            if (isCharging) return "smooth";
            return "balanced";
        }

        public static int GetFramePacingMaxHz(string profile)
        {
            if (profile == null) return 60;
            int hz;
            if (!FP_HZ.TryGetValue(profile, out hz)) hz = 60;
            return hz;
        }

        public static int GetFramePacingTargetHz(string profile, int deviceMaxHz)
        {
            int target = GetFramePacingMaxHz(profile);
            if (target < 60) target = 60;
            if (deviceMaxHz >= 60 && target > deviceMaxHz) target = deviceMaxHz;
            return target;
        }

        public static int GetFramePacingSmoothnessLevel(string profile)
        {
            if (profile == null) return 0;
            int lvl;
            if (!FP_ANIM.TryGetValue(profile, out lvl)) lvl = 0;
            return lvl;
        }

        // --- Scan monitor capability (max refresh once/day, like ATLASX device_max_hz) ---
        private void TryScanDeviceHz()
        {
            try
            {
                if (_fpDeviceMaxHz > 0 && (DateTime.Now - _fpLastScan).TotalHours < FP_HZ_SCAN_INTERVAL_HOURS) return;
                int max = 60;
                for (int i = 0; i < 256; i++)
                {
                    DEVMODE dm = new DEVMODE();
                    dm.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));
                    if (!EnumDisplaySettings(null, i, ref dm)) break;
                    if (dm.dmDisplayFrequency >= 30 && dm.dmDisplayFrequency <= 300 && dm.dmDisplayFrequency > max)
                        max = (int)dm.dmDisplayFrequency;
                }
                _fpDeviceMaxHz = max;
                _fpLastScan = DateTime.Now;
                AddLogOpt("FRAME_PACING", "Device max refresh detected: " + max + "Hz");
            }
            catch (Exception ex) { LogError("FramePacing.Scan", ex); }
        }

        private bool CanSetRefreshHz(int targetHz)
        {
            try
            {
                for (int i = 0; i < 256; i++)
                {
                    DEVMODE dm = new DEVMODE();
                    dm.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));
                    if (!EnumDisplaySettings(null, i, ref dm)) break;
                    if ((int)dm.dmDisplayFrequency == targetHz) return true;
                }
            }
            catch (Exception ex) { CWLog.Error("SmartAgent.FramePacing", ex); }
            return false;
        }

        private int GetCurrentRefreshHz()
        {
            try
            {
                DEVMODE dm = new DEVMODE();
                dm.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));
                if (EnumDisplaySettings(null, -1, ref dm)) return (int)dm.dmDisplayFrequency;
            }
            catch (Exception ex2) { CWLog.Error("SmartAgent.FramePacing", ex2); }
            return 0;
        }

        private void LoadFpBackup()
        {
            if (_fpBackupLoaded) return;
            _fpBackupLoaded = true;
            try
            {
                string path = FpBackupPath;
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string l = lines[i];
                    if (l == null) continue;
                    string[] kv = l.Split('=');
                    if (kv.Length != 2) continue;
                    int v;
                    if (!int.TryParse(kv[1], out v)) continue;
                    if (kv[0] == "refresh") _fpBackupRefresh = v;
                    else if (kv[0] == "menu") { _fpBackupMenu = v; _fpAnimBackupValid = true; }
                    else if (kv[0] == "tooltip") { _fpBackupTooltip = v; _fpAnimBackupValid = true; }
                    else if (kv[0] == "taskbar") { _fpBackupTaskbar = v; _fpAnimBackupValid = true; }
                    else if (kv[0] == "clientarea") { _fpBackupClientArea = v; _fpAnimBackupValid = true; }
                }
            }
            catch (Exception ex3) { CWLog.Error("SmartAgent.FramePacing", ex3); }
        }

        private void PersistFpBackup()
        {
            try
            {
                string path = FpBackupPath;
                if (string.IsNullOrEmpty(path)) return;
                try { Directory.CreateDirectory(Path.GetDirectoryName(path)); } catch (Exception ex4) { CWLog.Error("SmartAgent.FramePacing", ex4); }
                File.WriteAllText(path, string.Format(
                    "refresh={0}\nmenu={1}\ntooltip={2}\ntaskbar={3}\nclientarea={4}\n",
                    _fpBackupRefresh, _fpBackupMenu, _fpBackupTooltip, _fpBackupTaskbar, _fpBackupClientArea));
            }
            catch (Exception ex5) { CWLog.Error("SmartAgent.FramePacing", ex5); }
        }

        // --- Refresh-rate application (guarded: conservative, only-if-changed, cooldown, backup) ---
        private void ApplyFpRefresh(int targetHz)
        {
            if (targetHz < 60) return;
            int currentHz = GetCurrentRefreshHz();
            if (currentHz == targetHz)
            {
                _fpRefreshNote = "already " + targetHz + "Hz";
                _fpSkippedCount++;
                return;
            }
            // Cooldown: avoid oscillation (ATLASX 15min gate). Lowering for thermal/battery is urgent.
            if ((DateTime.Now - _fpLastModeChange).TotalMinutes < FP_MODE_SWITCH_COOLDOWN_MINUTES && targetHz > currentHz)
            {
                _fpRefreshNote = "cooldown (skip " + targetHz + "Hz)";
                _fpSkippedCount++;
                return;
            }
            // Conservative: only apply a mode the monitor actually supports.
            if (!CanSetRefreshHz(targetHz))
            {
                _fpRefreshNote = "unsupported " + targetHz + "Hz";
                _fpSkippedCount++;
                return;
            }
            try
            {
                LoadFpBackup();
                DEVMODE dm = new DEVMODE();
                dm.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));
                if (!EnumDisplaySettings(null, -1, ref dm)) return;
                dm.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY;
                dm.dmDisplayFrequency = (uint)targetHz;

                int test = ChangeDisplaySettingsEx(null, ref dm, IntPtr.Zero, CDS_TEST, IntPtr.Zero);
                if (test != DISP_CHANGE_SUCCESSFUL)
                {
                    _fpRefreshNote = "test failed (" + test + ")";
                    _fpSkippedCount++;
                    return;
                }
                if (_fpBackupRefresh == 0) _fpBackupRefresh = currentHz;
                int res = ChangeDisplaySettingsEx(null, ref dm, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero);
                if (res == DISP_CHANGE_SUCCESSFUL)
                {
                    _fpLastModeChange = DateTime.Now;
                    _fpAppliedCount++;
                    _fpRefreshNote = "set " + targetHz + "Hz";
                    PersistFpBackup();
                    AddLogOpt("FRAME_PACING", "Refresh rate -> " + targetHz + "Hz");
                }
                else
                {
                    _fpRefreshNote = "apply failed (" + res + ")";
                    _fpSkippedCount++;
                }
            }
            catch (Exception ex) { LogError("FramePacing.Refresh", ex); _fpRefreshNote = "error"; }
        }

        // --- Visual effects (SPI) - safe per-user settings with backup + only-if-changed ---
        private void ApplyFpVisualEffects(int level)
        {
            try
            {
                LoadFpBackup();
                ApplyFpSpi(SPI_GETMENUANIMATION, SPI_SETMENUANIMATION, "menu", ref _fpBackupMenu, level >= 2 ? 0 : 1);
                ApplyFpSpi(SPI_GETTOOLTIPANIMATION, SPI_SETTOOLTIPANIMATION, "tooltip", ref _fpBackupTooltip, level >= 1 ? 0 : 1);
                ApplyFpSpi(SPI_GETTASKBARANIMATION, SPI_SETTASKBARANIMATION, "taskbar", ref _fpBackupTaskbar, level >= 2 ? 0 : 1);
                ApplyFpSpi(SPI_GETCLIENTAREAANIMATION, SPI_SETCLIENTAREAANIMATION, "clientarea", ref _fpBackupClientArea, level >= 2 ? 0 : 1);
                if (_fpAnimBackupValid) PersistFpBackup();
            }
            catch (Exception ex) { LogError("FramePacing.Visual", ex); }
        }

        private void ApplyFpSpi(uint getAction, uint setAction, string name, ref int backup, int desired)
        {
            int cur = 1;
            if (SystemParametersInfo(getAction, 0, ref cur, 0))
            {
                if (!_fpAnimBackupValid) backup = cur;
                if (cur == desired)
                {
                    _fpSkippedCount++;
                    return;
                }
                int v = desired;
                if (SystemParametersInfo(setAction, 0, ref v, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE))
                {
                    _fpAnimBackupValid = true;
                    _fpAppliedCount++;
                    AddLogOpt("FRAME_PACING", "Visual '" + name + "' -> " + desired);
                }
                else _fpSkippedCount++;
            }
            else _fpSkippedCount++;
        }

        // --- Idle + AC maintenance (ATLASX "UI compile at screen-off + charging" analog) ---
        private void RunFpIdleMaintenance()
        {
            if (!FP_ENABLE_IDLE_MAINT) return;
            try
            {
                if (_isOnBattery) return;                 // needs AC (charging)
                if (!_currentState.IsCharging) return;
                if ((DateTime.Now - _fpLastIdleMaint).TotalHours < FP_IDLE_MAINT_INTERVAL_HOURS) return;

                long idleMs = GetSystemIdleMs();
                if (idleMs < FP_IDLE_REQUIRED_SECONDS * 1000L) return;

                _fpLastIdleMaint = DateTime.Now;
                bool ok = ExecutePurgeStandby();
                _fpAppliedCount++;
                AddLogOpt("FRAME_PACING", "Idle maintenance (AC + idle " + (idleMs / 1000) + "s): standby purge " + (ok ? "ok" : "no-op"));
            }
            catch (Exception ex) { LogError("FramePacing.Idle", ex); }
        }

        internal long GetSystemIdleMs()
        {
            try
            {
                LASTINPUTINFO lii = new LASTINPUTINFO();
                lii.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
                if (GetLastInputInfo(ref lii))
                {
                    uint now = (uint)Environment.TickCount;
                    uint diff = now - lii.dwTime;
                    return diff;
                }
            }
            catch (Exception ex6) { CWLog.Error("SmartAgent.FramePacing", ex6); }
            return 0;
        }

        // --- Main cycle (called from Tick) ---
        private void ApplyFramePacing()
        {
            if (!_fpActive || _simulationMode) return;
            try
            {
                TryScanDeviceHz();

                bool thermalHot = _smoothTemp >= FP_THERMAL_HOT_C || _thermalThrottled;
                bool batteryLow = _isOnBattery && _batteryPercent <= FP_BATTERY_LOW_PERCENT;
                bool game = _currentState != null && _currentState.IsGame;
                bool charging = _currentState != null && _currentState.IsCharging;
                string profile = ResolveFramePacingProfile(game, charging, batteryLow, thermalHot);

                int targetHz = GetFramePacingTargetHz(profile, _fpDeviceMaxHz);
                ApplyFpRefresh(targetHz);
                ApplyFpVisualEffects(GetFramePacingSmoothnessLevel(profile));
                RunFpIdleMaintenance();

                if (profile != _fpEffectiveProfile)
                {
                    AddLogOpt("FRAME_PACING", "Profile -> " + profile + " (target " + targetHz + "Hz)");
                    _fpEffectiveProfile = profile;
                }
            }
            catch (Exception ex) { LogError("FramePacing", ex); }
        }

        // --- Restore everything to the backed-up state (called on Stop / rollback) ---
        public bool RestoreFramePacing()
        {
            bool ok = true;
            try
            {
                LoadFpBackup();
                if (_fpBackupRefresh > 0 && CanSetRefreshHz(_fpBackupRefresh))
                {
                    int currentHz = GetCurrentRefreshHz();
                    if (currentHz != _fpBackupRefresh)
                    {
                        DEVMODE dm = new DEVMODE();
                        dm.dmSize = (ushort)Marshal.SizeOf(typeof(DEVMODE));
                        if (EnumDisplaySettings(null, -1, ref dm))
                        {
                            dm.dmFields = DM_PELSWIDTH | DM_PELSHEIGHT | DM_DISPLAYFREQUENCY;
                            dm.dmDisplayFrequency = (uint)_fpBackupRefresh;
                            int res = ChangeDisplaySettingsEx(null, ref dm, IntPtr.Zero, CDS_UPDATEREGISTRY, IntPtr.Zero);
                            if (res != DISP_CHANGE_SUCCESSFUL) ok = false;
                        }
                    }
                }
                if (_fpAnimBackupValid)
                {
                    int v;
                    v = _fpBackupMenu; SystemParametersInfo(SPI_SETMENUANIMATION, 0, ref v, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                    v = _fpBackupTooltip; SystemParametersInfo(SPI_SETTOOLTIPANIMATION, 0, ref v, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                    v = _fpBackupTaskbar; SystemParametersInfo(SPI_SETTASKBARANIMATION, 0, ref v, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                    v = _fpBackupClientArea; SystemParametersInfo(SPI_SETCLIENTAREAANIMATION, 0, ref v, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                }
                try { RestoreAllBackedUpRegistrySettings(); } catch (Exception exReg) { CWLog.Error("FramePacing.RestoreRegistry", exReg); }
                AddLogOpt("FRAME_PACING", "Restored UI frame pacing and GPU registry settings");
            }
            catch (Exception ex) { LogError("FramePacing.Restore", ex); ok = false; }
            return ok;
        }
    }
}
