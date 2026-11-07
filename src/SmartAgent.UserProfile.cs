using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ClearWin
{
    public class UserProfileData
    {
        public string DeviceId;
        public string DeviceName;
        public string DeviceType;
        public int SessionCount;
        public DateTime FirstSeen;
        public DateTime LastSeen;
        public double TotalUsageHours;
        public double[] HourlyActivity = new double[24];
        public double[] DayOfWeekActivity = new double[7];
        public Dictionary<string, double> AppUsageMinutes = new Dictionary<string, double>();
        public Dictionary<string, double> AppCpuAvg = new Dictionary<string, double>();
        public Dictionary<string, double> AppRamAvg = new Dictionary<string, double>();
        public double AvgCpuUsage;
        public double AvgRamUsage;
        public double AvgGpuUsage;
        public double AvgTemp;
        public double PeakCpuUsage;
        public double PeakRamUsage;
        public double PeakTemp;
        public double AvgBatteryAtEnd;
        public int TotalDecisionsMade;
        public int SuccessfulOptimizations;
        public int FailedOptimizations;
        public string PreferredPowerPlan;
        public bool IsLaptop;
        public string WorkStyle;
        public int SampleCount;
        public List<string> TopApps = new List<string>();
        public List<string> TopGames = new List<string>();
        public UserProfileData()
        {
            DeviceId = GetDeviceFingerprint();
            DeviceName = Environment.MachineName;
            FirstSeen = DateTime.Now;
            LastSeen = DateTime.Now;
            DeviceType = "Desktop";
        }
        public static string GetDeviceFingerprint()
        {
            try
            {
                string raw = Environment.MachineName + "_" + Environment.ProcessorCount + "_" + Environment.UserName;
                return raw.GetHashCode().ToString("X");
            }
            catch { return "unknown"; }
        }
    }

    public class UsagePattern
    {
        public double HourCpuAvg;
        public double HourRamAvg;
        public double HourGpuAvg;
        public int SampleCount;
        public string DominantWorkload;
        public List<string> FrequentApps = new List<string>();
    }

    public partial class SmartAgent
    {
        private UserProfileData _userProfile;
        private Dictionary<string, UsagePattern> _hourlyPatterns = new Dictionary<string, UsagePattern>();
        private DateTime _lastProfileUpdate = DateTime.MinValue;
        private const int PROFILE_UPDATE_INTERVAL_SECONDS = 30;
        private DateTime _sessionStart;
        private double _sessionMinutes;
        private Dictionary<string, double> _sessionAppMinutes = new Dictionary<string, double>();
        private string _lastProfiledApp = "";
        private DateTime _lastAppSwitch = DateTime.Now;
        private readonly object _profileLock = new object();

        public UserProfileData GetUserProfile() { return _userProfile; }
        public string GetUserWorkStyle() { return _userProfile != null ? _userProfile.WorkStyle : "Unknown"; }

        public void InitializeUserProfile()
        {
            _sessionStart = DateTime.Now;
            _userProfile = new UserProfileData();
            LoadUserProfile();
        }

        public void UpdateUserProfile()
        {
            if (_userProfile == null) return;
            if ((DateTime.Now - _lastProfileUpdate).TotalSeconds < PROFILE_UPDATE_INTERVAL_SECONDS) return;
            _lastProfileUpdate = DateTime.Now;

            lock (_profileLock)
            {
                _userProfile.LastSeen = DateTime.Now;
                _sessionMinutes = (DateTime.Now - _sessionStart).TotalMinutes;
                _userProfile.TotalUsageHours += _sessionMinutes / 60.0;
                _sessionStart = DateTime.Now;

            int hour = DateTime.Now.Hour;
            int day = (int)DateTime.Now.DayOfWeek;
            _userProfile.HourlyActivity[hour] += 1.0;
            _userProfile.DayOfWeekActivity[day] += 1.0;

            int n = _userProfile.SampleCount + 1;
            _userProfile.AvgCpuUsage = (_userProfile.AvgCpuUsage * _userProfile.SampleCount + _currentState.CpuUsage) / n;
            _userProfile.AvgRamUsage = (_userProfile.AvgRamUsage * _userProfile.SampleCount + _currentState.RamUsage) / n;
            _userProfile.AvgGpuUsage = (_userProfile.AvgGpuUsage * _userProfile.SampleCount + _currentState.GpuUsage) / n;
            _userProfile.AvgTemp = (_userProfile.AvgTemp * _userProfile.SampleCount + _currentState.CpuTemp) / n;
            if (_currentState.CpuUsage > _userProfile.PeakCpuUsage) _userProfile.PeakCpuUsage = _currentState.CpuUsage;
            if (_currentState.RamUsage > _userProfile.PeakRamUsage) _userProfile.PeakRamUsage = _currentState.RamUsage;
            if (_currentState.CpuTemp > _userProfile.PeakTemp) _userProfile.PeakTemp = _currentState.CpuTemp;
            _userProfile.SampleCount = n;
            _userProfile.TotalDecisionsMade = Stats.TotalDecisions;
            _userProfile.SuccessfulOptimizations = Stats.TotalDecisions - Stats.FailedDecisions;
            _userProfile.FailedOptimizations = Stats.FailedDecisions;

            TrackAppUsage();

            string power = GetCurrentPowerPlan();
            _userProfile.PreferredPowerPlan = power;

            _userProfile.IsLaptop = IsOnBattery || _batteryPercent > 0;
            _userProfile.DeviceType = _userProfile.IsLaptop ? "Laptop" : "Desktop";

            DetectWorkStyle();

            string key = hour.ToString();
            if (!_hourlyPatterns.ContainsKey(key))
                _hourlyPatterns[key] = new UsagePattern();
            var hp = _hourlyPatterns[key];
            int hpN = hp.SampleCount + 1;
            hp.HourCpuAvg = (hp.HourCpuAvg * hp.SampleCount + _currentState.CpuUsage) / hpN;
            hp.HourRamAvg = (hp.HourRamAvg * hp.SampleCount + _currentState.RamUsage) / hpN;
            hp.HourGpuAvg = (hp.HourGpuAvg * hp.SampleCount + _currentState.GpuUsage) / hpN;
            hp.SampleCount = hpN;

            if (_tickCount % 300 == 0)
            {
                SaveUserProfile();
            }
            }
        }

        private void TrackAppUsage()
        {
            string currentApp = _currentState.ActiveProcess ?? "";
            if (string.IsNullOrEmpty(currentApp)) return;
            if (currentApp == _lastProfiledApp) { TrimAppDicts(); return; }

            if (!string.IsNullOrEmpty(_lastProfiledApp))
            {
                double mins = (DateTime.Now - _lastAppSwitch).TotalMinutes;
                if (!_sessionAppMinutes.ContainsKey(_lastProfiledApp))
                    _sessionAppMinutes[_lastProfiledApp] = 0;
                _sessionAppMinutes[_lastProfiledApp] += mins;
                if (!_userProfile.AppUsageMinutes.ContainsKey(_lastProfiledApp))
                    _userProfile.AppUsageMinutes[_lastProfiledApp] = 0;
                _userProfile.AppUsageMinutes[_lastProfiledApp] += mins;
            }
            _lastProfiledApp = currentApp;
            _lastAppSwitch = DateTime.Now;

            double cpuAvg = _userProfile.AppCpuAvg.ContainsKey(currentApp) ? _userProfile.AppCpuAvg[currentApp] : 0;
            double ramAvg = _userProfile.AppRamAvg.ContainsKey(currentApp) ? _userProfile.AppRamAvg[currentApp] : 0;
            _userProfile.AppCpuAvg[currentApp] = cpuAvg * 0.9 + _currentState.CpuUsage * 0.1;
            _userProfile.AppRamAvg[currentApp] = ramAvg * 0.9 + _currentState.RamUsage * 0.1;

            UpdateTopApps();
            TrimAppDicts();
        }

        private const int APP_USAGE_MAX = 150;

        private void TrimAppDicts()
        {
            TrimToCap(_userProfile.AppUsageMinutes);
            TrimToCap(_sessionAppMinutes);
            TrimToCap(_userProfile.AppCpuAvg);
            TrimToCap(_userProfile.AppRamAvg);
        }

        private static void TrimToCap(Dictionary<string, double> dict)
        {
            while (dict.Count > APP_USAGE_MAX)
            {
                string smallest = null; double min = double.MaxValue;
                foreach (var kv in dict)
                {
                    if (kv.Value < min) { min = kv.Value; smallest = kv.Key; }
                }
                if (smallest == null) break;
                dict.Remove(smallest);
            }
        }

        private void UpdateTopApps()
        {
            var sorted = _userProfile.AppUsageMinutes.OrderByDescending(kv => kv.Value).ToList();
            _userProfile.TopApps.Clear();
            int limit = Math.Min(10, sorted.Count);
            for (int i = 0; i < limit; i++)
                _userProfile.TopApps.Add(sorted[i].Key);
            _userProfile.TopGames.Clear();
            string[] gameKeywords = new[] { "game", "steam", "epic", "valorant", "cs2", "fortnite", "pubg", "apex", "gta", "minecraft", "roblox", "league", "dota", "overwatch" };
            foreach (var kv in sorted)
            {
                string lower = kv.Key.ToLower();
                foreach (string kw in gameKeywords)
                {
                    if (lower.Contains(kw)) { _userProfile.TopGames.Add(kv.Key); break; }
                }
            }
        }

        private void DetectWorkStyle()
        {
            if (_userProfile.SampleCount < 10) { _userProfile.WorkStyle = "Learning"; return; }

            double avgGpu = _userProfile.AvgGpuUsage;
            double avgCpu = _userProfile.AvgCpuUsage;
            double avgRam = _userProfile.AvgRamUsage;
            double gamingRatio = _userProfile.TopGames.Count > 0 ? (double)_userProfile.TopGames.Count / Math.Max(1, _userProfile.TopApps.Count) : 0;

            if (gamingRatio > 0.3 || avgGpu > 60)
                _userProfile.WorkStyle = "Gaming";
            else if (avgCpu > 60 && avgRam > 50)
                _userProfile.WorkStyle = "Developer";
            else if (avgGpu > 40 && _userProfile.AppUsageMinutes.ContainsKey("AdobePhotoshop.exe"))
                _userProfile.WorkStyle = "Creative";
            else if (avgCpu < 25 && avgRam < 40)
                _userProfile.WorkStyle = "Office";
            else
                _userProfile.WorkStyle = "General";
        }

        public UsagePattern GetPatternForHour(int hour)
        {
            string key = hour.ToString();
            UsagePattern p;
            if (_hourlyPatterns.TryGetValue(key, out p)) return p;
            return null;
        }

        public double GetPredictedLoad(int futureHour)
        {
            UsagePattern p = GetPatternForHour(futureHour);
            if (p == null || p.SampleCount < 5) return 50;
            return (p.HourCpuAvg * 0.4 + p.HourRamAvg * 0.3 + p.HourGpuAvg * 0.3);
        }

        public string GetUserInsight()
        {
            if (_userProfile == null || _userProfile.SampleCount < 5)
                return "Collecting usage data...";

            var sb = new StringBuilder();
            sb.AppendLine("Device: " + _userProfile.DeviceName + " (" + _userProfile.DeviceType + ")");
            sb.AppendLine("Work Style: " + _userProfile.WorkStyle);
            sb.AppendLine(string.Format("Avg CPU: {0:F0}% | RAM: {1:F0}% | GPU: {2:F0}% | Temp: {3:F0}C",
                _userProfile.AvgCpuUsage, _userProfile.AvgRamUsage, _userProfile.AvgGpuUsage, _userProfile.AvgTemp));
            sb.AppendLine(string.Format("Peak CPU: {0:F0}% | RAM: {1:F0}% | Temp: {2:F0}C",
                _userProfile.PeakCpuUsage, _userProfile.PeakRamUsage, _userProfile.PeakTemp));
            sb.AppendLine(string.Format("Decisions: {0} | Success: {1} | Failed: {2}",
                _userProfile.TotalDecisionsMade, _userProfile.SuccessfulOptimizations, _userProfile.FailedOptimizations));

            if (_userProfile.TopApps.Count > 0)
            {
                sb.Append("Top Apps: ");
                int show = Math.Min(5, _userProfile.TopApps.Count);
                for (int i = 0; i < show; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(_userProfile.TopApps[i]);
                }
                sb.AppendLine();
            }
            if (_userProfile.TopGames.Count > 0)
            {
                sb.Append("Games: ");
                for (int i = 0; i < _userProfile.TopGames.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(_userProfile.TopGames[i]);
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private void SaveUserProfile()
        {
            try
            {
                string path = Path.Combine(_dataDir, "user_profile.dat");
                var sb = new StringBuilder();
                sb.AppendLine(_userProfile.DeviceId);
                sb.AppendLine(_userProfile.DeviceName);
                sb.AppendLine(_userProfile.DeviceType);
                sb.AppendLine(_userProfile.WorkStyle);
                sb.AppendLine(_userProfile.SessionCount.ToString());
                sb.AppendLine(_userProfile.FirstSeen.Ticks.ToString());
                sb.AppendLine(_userProfile.LastSeen.Ticks.ToString());
                sb.AppendLine(_userProfile.TotalUsageHours.ToString("F2"));
                sb.AppendLine(_userProfile.AvgCpuUsage.ToString("F1"));
                sb.AppendLine(_userProfile.AvgRamUsage.ToString("F1"));
                sb.AppendLine(_userProfile.AvgGpuUsage.ToString("F1"));
                sb.AppendLine(_userProfile.AvgTemp.ToString("F1"));
                sb.AppendLine(_userProfile.PeakCpuUsage.ToString("F1"));
                sb.AppendLine(_userProfile.PeakRamUsage.ToString("F1"));
                sb.AppendLine(_userProfile.PeakTemp.ToString("F1"));
                sb.AppendLine(_userProfile.TotalDecisionsMade.ToString());
                sb.AppendLine(_userProfile.SuccessfulOptimizations.ToString());
                sb.AppendLine(_userProfile.FailedOptimizations.ToString());
                sb.AppendLine(_userProfile.PreferredPowerPlan ?? "Balanced");
                sb.AppendLine(_userProfile.IsLaptop.ToString());
                sb.AppendLine(_userProfile.SampleCount.ToString());
                foreach (var kv in _userProfile.AppUsageMinutes)
                    sb.AppendLine("APP:" + kv.Key + ":" + kv.Value.ToString("F1"));
                foreach (var kv in _userProfile.AppCpuAvg)
                    sb.AppendLine("CPU:" + kv.Key + ":" + kv.Value.ToString("F1"));
                foreach (var kv in _userProfile.AppRamAvg)
                    sb.AppendLine("RAM:" + kv.Key + ":" + kv.Value.ToString("F1"));
                for (int i = 0; i < 24; i++)
                    sb.AppendLine("HOUR:" + i + ":" + _userProfile.HourlyActivity[i].ToString("F1"));
                foreach (var kv in _hourlyPatterns)
                    sb.AppendLine("PAT:" + kv.Key + ":" + kv.Value.HourCpuAvg.ToString("F1") + ":" + kv.Value.HourRamAvg.ToString("F1") + ":" + kv.Value.HourGpuAvg.ToString("F1") + ":" + kv.Value.SampleCount);
                File.WriteAllText(path, sb.ToString());
            }
            catch (Exception ex) { LogError("SaveUserProfile", ex); }
        }

        private void LoadUserProfile()
        {
            try
            {
                string path = Path.Combine(_dataDir, "user_profile.dat");
                if (!File.Exists(path)) return;
                string[] lines = File.ReadAllLines(path);
                if (lines.Length < 20) return;
                int idx = 0;
                _userProfile.DeviceId = lines[idx++];
                _userProfile.DeviceName = lines[idx++];
                _userProfile.DeviceType = lines[idx++];
                _userProfile.WorkStyle = lines[idx++];
                _userProfile.SessionCount = int.Parse(lines[idx++]) + 1;
                _userProfile.FirstSeen = new DateTime(long.Parse(lines[idx++]));
                _userProfile.LastSeen = new DateTime(long.Parse(lines[idx++]));
                _userProfile.TotalUsageHours = double.Parse(lines[idx++]);
                _userProfile.AvgCpuUsage = double.Parse(lines[idx++]);
                _userProfile.AvgRamUsage = double.Parse(lines[idx++]);
                _userProfile.AvgGpuUsage = double.Parse(lines[idx++]);
                _userProfile.AvgTemp = double.Parse(lines[idx++]);
                _userProfile.PeakCpuUsage = double.Parse(lines[idx++]);
                _userProfile.PeakRamUsage = double.Parse(lines[idx++]);
                _userProfile.PeakTemp = double.Parse(lines[idx++]);
                _userProfile.TotalDecisionsMade = int.Parse(lines[idx++]);
                _userProfile.SuccessfulOptimizations = int.Parse(lines[idx++]);
                _userProfile.FailedOptimizations = int.Parse(lines[idx++]);
                _userProfile.PreferredPowerPlan = lines[idx++];
                _userProfile.IsLaptop = bool.Parse(lines[idx++]);
                _userProfile.SampleCount = int.Parse(lines[idx++]);
                while (idx < lines.Length)
                {
                    string line = lines[idx++];
                    if (line.StartsWith("APP:"))
                    {
                        string[] parts = line.Substring(4).Split(':');
                        if (parts.Length >= 2) _userProfile.AppUsageMinutes[parts[0]] = double.Parse(parts[1]);
                    }
                    else if (line.StartsWith("CPU:"))
                    {
                        string[] parts = line.Substring(4).Split(':');
                        if (parts.Length >= 2) _userProfile.AppCpuAvg[parts[0]] = double.Parse(parts[1]);
                    }
                    else if (line.StartsWith("RAM:"))
                    {
                        string[] parts = line.Substring(4).Split(':');
                        if (parts.Length >= 2) _userProfile.AppRamAvg[parts[0]] = double.Parse(parts[1]);
                    }
                    else if (line.StartsWith("HOUR:"))
                    {
                        string[] parts = line.Substring(5).Split(':');
                        if (parts.Length >= 2)
                        {
                            int h = int.Parse(parts[0]);
                            if (h >= 0 && h < 24) _userProfile.HourlyActivity[h] = double.Parse(parts[1]);
                        }
                    }
                    else if (line.StartsWith("PAT:"))
                    {
                        string[] parts = line.Substring(4).Split(':');
                        if (parts.Length >= 5)
                        {
                            var hp = new UsagePattern();
                            hp.HourCpuAvg = double.Parse(parts[1]);
                            hp.HourRamAvg = double.Parse(parts[2]);
                            hp.HourGpuAvg = double.Parse(parts[3]);
                            hp.SampleCount = int.Parse(parts[4]);
                            _hourlyPatterns[parts[0]] = hp;
                        }
                    }
                }
                UpdateTopApps();
                DetectWorkStyle();
            }
            catch (Exception ex) { LogError("LoadUserProfile", ex); }
        }
    }
}
