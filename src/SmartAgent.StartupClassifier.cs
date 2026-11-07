using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace ClearWin
{
    /// <summary>
    /// Startup item intelligence modeled on Smart Game Booster's approach: SGB
    /// ships MD5 + publisher-signature black/white lists (startupBlack.db,
    /// startupSignature.db) to decide which autostart entries are safe to
    /// disable. Here we classify entries from the command line (exe/path +
    /// publisher signature keywords) into "essential" vs "safe to disable"
    /// (third-party updaters, schedulers, adware/coupon vendors), and provide a
    /// real disable/restore that never loses data (backup hive first).
    /// </summary>
    public enum StartupRecommendation
    {
        KeepEssential,   // Windows / security / input — never disable
        UnknownKeep,     // no signal — keep by default
        SafeToDisable    // bloat / third-party updater / adware family
    }

    public class StartupItem
    {
        public string Hive;      // "HKCU" or "HKLM"
        public string Name;
        public string Command;
        public string Publisher;
        public StartupRecommendation Recommendation;
        public string Reason;
        public bool IsEnabled = true;
    }

    public static class StartupClassifier
    {
        internal static string TestRunSubKeyOverride = null;

        internal static string GetRunSubKeyPath()
        {
            if (!string.IsNullOrEmpty(TestRunSubKeyOverride)) return TestRunSubKeyOverride;
            string envPrefix = Environment.GetEnvironmentVariable("CLEARWIN_TEST_REG_PREFIX");
            if (!string.IsNullOrEmpty(envPrefix)) return envPrefix.TrimEnd('\\') + @"\Run";
            return @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        }
        // Publisher signatures associated with adware/coupon toolbars that SGB's
        // signature database also treats as junk (extracted families).
        private static readonly string[] BLOAT_SIGNATURES = new[] {
            "coupon", "spigot", "chengdu", "orange view", "wonbo", "qi wang",
            "vtools", "slick savings", "search settings", "application updater",
            "couponshelper", "deal", "offers", "toolbar"
        };

        private static readonly string[] BLOAT_WORD_MARKERS = new[] {
            "updater", "update", "scheduler", "sync", "checker", "helper",
            "startup manager", "downloader"
        };

        private static readonly string[] ESSENTIAL_SIGNATURES = new[] {
            "microsoft", "windows defender", "malwarebytes", "avast", "avg",
            "norton", "kaspersky", "eset", "mcafee", "bitdefender", "crowdstrike",
            "intel", "amd", "nvidia", "realtek", "synaptics", "google update"
        };

        private static readonly string[] ESSENTIAL_NAMES = new[] {
            "ctfmon", "securityhealth", "sihost", "taskhost", "onedrive",
            "securityhealthsystray", "runtimebroker", "shell"
        };

        /// <summary>Classifies a startup entry from its command line only.</summary>
        public static StartupItem Classify(string hive, string name, string command)
        {
            var item = new StartupItem { Hive = hive, Name = name, Command = command ?? "" };
            if (string.IsNullOrEmpty(name)) { item.Recommendation = StartupRecommendation.UnknownKeep; item.Reason = "no name"; return item; }
            string lower = command.ToLowerInvariant();
            string lowerName = name.ToLowerInvariant();

            foreach (string ess in ESSENTIAL_NAMES) if (lowerName.Contains(ess)) { item.Recommendation = StartupRecommendation.KeepEssential; item.Reason = "essential Windows/input component"; return item; }
            if (lower.Contains("\\windows\\") || lower.Contains("\\system32\\") || lower.Contains("\\syswow64\\"))
            {
                item.Recommendation = StartupRecommendation.KeepEssential; item.Reason = "Windows system entry";
                return item;
            }
            foreach (string sig in ESSENTIAL_SIGNATURES) if (lower.Contains(sig)) { item.Recommendation = StartupRecommendation.KeepEssential; item.Reason = "security/driver publisher: " + sig; return item; }
            foreach (string sig in BLOAT_SIGNATURES) if (lower.Contains(sig)) { item.Recommendation = StartupRecommendation.SafeToDisable; item.Reason = "adware/coupon family: " + sig; return item; }
            foreach (string sig in BLOAT_WORD_MARKERS)
            {
                if (lower.Contains(sig) && !lower.Contains("\\windows\\") && !lower.Contains("\\microsoft"))
                {
                    item.Recommendation = StartupRecommendation.SafeToDisable;
                    item.Reason = "third-party updater/scheduler: " + sig;
                    return item;
                }
            }
            item.Recommendation = StartupRecommendation.UnknownKeep;
            item.Reason = "no strong signal";
            return item;
        }

        /// <summary>Scans the standard Run/RunOnce hives on the current machine.</summary>
        public static List<StartupItem> EnumerateStartupItems()
        {
            var items = new List<StartupItem>();
            try
            {
                AddHive(items, "HKLM", Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                AddHive(items, "HKLM", Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", true);
                AddHive(items, "HKCU", Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
                AddHive(items, "HKCU", Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", true);
                // Also enumerate disabled items backed up under HKCU\Software\ClearWin\StartupBackup so users can re-enable them
                AddHive(items, "HKCU", Registry.CurrentUser, @"Software\ClearWin\StartupBackup\HKCU", false);
                AddHive(items, "HKLM", Registry.CurrentUser, @"Software\ClearWin\StartupBackup\HKLM", false);
            }
            catch (Exception ex) { CWLog.Error("SmartAgent.StartupClassifier", ex); }
            return items;
        }

        private static void AddHive(List<StartupItem> items, string hive, RegistryKey root, string path, bool isEnabled = true)
        {
            using (RegistryKey key = root.OpenSubKey(path, false))
            {
                if (key == null) return;
                foreach (string n in key.GetValueNames())
                {
                    string val;
                    try { val = Convert.ToString(key.GetValue(n)); } catch { continue; }
                    if (string.IsNullOrEmpty(val)) continue;
                    var item = Classify(hive, n, val);
                    item.IsEnabled = isEnabled;
                    items.Add(item);
                }
            }
        }

        /// <summary>
        /// Disables one startup entry by moving it into a backup hive under
        /// HKCU\Software\ClearWin\StartupBackup so it can always be restored.
        /// Returns false when the entry does not exist.
        /// </summary>
        public static bool ExecuteStartupDisable(string hive, string name, string command)
        {
            if (string.IsNullOrEmpty(name)) return false;
            try
            {
                RegistryKey key = null;
                string runSubKey = GetRunSubKeyPath();
                if (hive == "HKLM") key = Registry.LocalMachine.OpenSubKey(runSubKey, true);
                else key = Registry.CurrentUser.OpenSubKey(runSubKey, true);
                if (key == null) return false;
                try
                {
                    if (key.GetValue(name) == null) return false;
                    using (RegistryKey backup = Registry.CurrentUser.CreateSubKey(@"Software\ClearWin\StartupBackup\" + hive))
                    {
                        if (backup != null) backup.SetValue(name, command ?? Convert.ToString(key.GetValue(name)));
                    }
                    key.DeleteValue(name, false);
                    return true;
                }
                finally { key.Close(); }
            }
            catch { return false; }
        }

        /// <summary>Restores a previously backed-up startup entry. Returns false when nothing was saved.</summary>
        public static bool ExecuteStartupRestore(string hive, string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            try
            {
                string backupVal;
                using (RegistryKey backup = Registry.CurrentUser.OpenSubKey(@"Software\ClearWin\StartupBackup\" + hive, false))
                {
                    if (backup == null || backup.GetValue(name) == null) return false;
                    backupVal = Convert.ToString(backup.GetValue(name));
                }
                string runSubKey = GetRunSubKeyPath();
                RegistryKey key = hive == "HKLM"
                    ? Registry.LocalMachine.OpenSubKey(runSubKey, true)
                    : Registry.CurrentUser.OpenSubKey(runSubKey, true);
                if (key == null) return false;
                try { key.SetValue(name, backupVal); }
                finally { key.Close(); }
                using (RegistryKey backup = Registry.CurrentUser.OpenSubKey(@"Software\ClearWin\StartupBackup\" + hive, true))
                {
                    if (backup != null) backup.DeleteValue(name, false);
                }
                return true;
            }
            catch { return false; }
        }

        /// <summary>
        /// Finds the classified "safe to disable" entry and disables it (with
        /// backup). Used by the StartupDisable AI action so the action is real.
        /// </summary>
        public static bool DisableWorstStartupBloat()
        {
            List<StartupItem> items = EnumerateStartupItems();
            StartupItem worst = null;
            foreach (StartupItem it in items)
            {
                if (it.Recommendation != StartupRecommendation.SafeToDisable) continue;
                if (worst == null || (it.Hive == "HKCU" && worst.Hive != "HKCU")) worst = it;
            }
            if (worst == null) return false;
            return ExecuteStartupDisable(worst.Hive, worst.Name, worst.Command);
        }

        public static string GetStartupReport()
        {
            List<StartupItem> items = EnumerateStartupItems();
            int bloat = 0, essential = 0, unknown = 0;
            foreach (StartupItem it in items)
            {
                if (it.Recommendation == StartupRecommendation.SafeToDisable) bloat++;
                else if (it.Recommendation == StartupRecommendation.KeepEssential) essential++;
                else unknown++;
            }
            return string.Format("Startup: {0} total ({1} safe-to-disable, {2} essential, {3} unknown)", items.Count, bloat, essential, unknown);
        }
    }
}
