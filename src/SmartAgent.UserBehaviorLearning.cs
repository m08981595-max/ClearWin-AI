using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ClearWin
{
    // === USER BEHAVIOR LEARNING ===
    // Learns user patterns and preferences over time
    // Adapts optimization strategies based on what user cares about most
    public partial class SmartAgent
    {
        private Dictionary<string, UserPreference> _userPreferences = new Dictionary<string, UserPreference>();
        private Dictionary<string, DailyUsagePattern> _dailyPatterns = new Dictionary<string, DailyUsagePattern>();
        private List<UserActionRecord> _userActions = new List<UserActionRecord>();
        private DateTime _lastBehaviorUpdate = DateTime.MinValue;
        private const int BEHAVIOR_UPDATE_INTERVAL = 300;
        private const int MAX_USER_ACTIONS = 200;

        public void InitializeUserBehaviorLearning()
        {
            _userPreferences.Clear();
            _dailyPatterns.Clear();
            _userActions.Clear();

            _userPreferences["Performance"] = new UserPreference { Category = "Performance", Weight = 1.0, Priority = 3 };
            _userPreferences["Battery"] = new UserPreference { Category = "Battery", Weight = 0.5, Priority = 2 };
            _userPreferences["Thermal"] = new UserPreference { Category = "Thermal", Weight = 0.7, Priority = 2 };
            _userPreferences["Memory"] = new UserPreference { Category = "Memory", Weight = 0.8, Priority = 2 };
            _userPreferences["Stability"] = new UserPreference { Category = "Stability", Weight = 0.9, Priority = 3 };
            _userPreferences["Gaming"] = new UserPreference { Category = "Gaming", Weight = 0.6, Priority = 1 };

            string[] days = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
            for (int d = 0; d < days.Length; d++)
            {
                _dailyPatterns[days[d]] = new DailyUsagePattern { DayOfWeek = days[d] };
            }
        }

        public UserBehaviorResult UpdateUserBehaviorLearning()
        {
            UserBehaviorResult result = new UserBehaviorResult();

            try
            {
                if ((DateTime.Now - _lastBehaviorUpdate).TotalSeconds < BEHAVIOR_UPDATE_INTERVAL)
                    return result;
                _lastBehaviorUpdate = DateTime.Now;

                UpdateDailyPatterns();
                UpdateUserPreferences();
                result.Preferences = GetUserPreferencesSummary();
                result.Patterns = GetDailyPatternsSummary();
                result.Recommendations = GenerateBehaviorRecommendations();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[UserBehaviorLearning] Error: " + ex.Message);
            }

            return result;
        }

        private void UpdateDailyPatterns()
        {
            try
            {
                string dayOfWeek = DateTime.Now.DayOfWeek.ToString();
                if (!_dailyPatterns.ContainsKey(dayOfWeek))
                    return;

                DailyUsagePattern pattern = _dailyPatterns[dayOfWeek];
                int hour = DateTime.Now.Hour;

                pattern.UsageHours[hour] = Math.Min(1.0, pattern.UsageHours[hour] + 0.1);

                double cpuUsage = GetCurrentCpuUsage();
                double ramUsage = GetCurrentRamUsage();
                bool isGaming = IsGameRunning();

                pattern.AvgCpuByHour[hour] = (pattern.AvgCpuByHour[hour] + cpuUsage) / 2.0;
                pattern.AvgRamByHour[hour] = (pattern.AvgRamByHour[hour] + ramUsage) / 2.0;
                pattern.GamingByHour[hour] = (pattern.GamingByHour[hour] + (isGaming ? 1.0 : 0.0)) / 2.0;
            }
            catch (Exception ex) { LogError("UpdateDailyPatterns", ex); }
        }

        private void UpdateUserPreferences()
        {
            try
            {
                List<AgentDecision> recentDecisions = GetRecentDecisions(10);

                for (int i = 0; i < recentDecisions.Count; i++)
                {
                    AgentDecision decision = recentDecisions[i];
                    RecordUserAction(decision.Category.ToString(), decision.WasAccepted);
                }

                foreach (var pref in _userPreferences.Values)
                {
                    double acceptanceRate = GetPreferenceAcceptanceRate(pref.Category);
                    pref.Weight = pref.Weight * 0.9 + acceptanceRate * 0.1 * pref.Priority;
                }
            }
            catch (Exception ex) { LogError("UpdateUserPreferences", ex); }
        }

        private void RecordUserAction(string category, bool accepted)
        {
            UserActionRecord record = new UserActionRecord
            {
                Category = category,
                Accepted = accepted,
                Timestamp = DateTime.Now
            };

            _userActions.Add(record);
            if (_userActions.Count > MAX_USER_ACTIONS)
                _userActions.RemoveAt(0);
        }

        private double GetPreferenceAcceptanceRate(string category)
        {
            int total = 0, accepted = 0;
            for (int i = _userActions.Count - 1; i >= Math.Max(0, _userActions.Count - 20); i--)
            {
                if (_userActions[i].Category == category)
                {
                    total++;
                    if (_userActions[i].Accepted) accepted++;
                }
            }
            return total > 0 ? (double)accepted / total : 0.5;
        }

        private List<AgentDecision> GetRecentDecisions(int count)
        {
            return new List<AgentDecision>();
        }

        private List<UserBehaviorRecommendation> GenerateBehaviorRecommendations()
        {
            List<UserBehaviorRecommendation> recs = new List<UserBehaviorRecommendation>();

            foreach (var pref in _userPreferences.Values)
            {
                if (pref.Weight > 0.8 && pref.Priority >= 2)
                {
                    recs.Add(new UserBehaviorRecommendation
                    {
                        Category = pref.Category,
                        Recommendation = string.Format("User strongly prefers {0} optimizations (weight: {1:F2})",
                            pref.Category, pref.Weight),
                        Priority = pref.Priority
                    });
                }
            }

            string dayOfWeek = DateTime.Now.DayOfWeek.ToString();
            if (_dailyPatterns.ContainsKey(dayOfWeek))
            {
                DailyUsagePattern pattern = _dailyPatterns[dayOfWeek];
                int hour = DateTime.Now.Hour;
                if (pattern.GamingByHour[hour] > 0.5)
                {
                    recs.Add(new UserBehaviorRecommendation
                    {
                        Category = "Gaming",
                        Recommendation = string.Format("User typically games at {0}:00 ({1:P0} probability)",
                            hour, pattern.GamingByHour[hour]),
                        Priority = 3
                    });
                }
            }

            return recs;
        }

        public string GetUserPreferencesSummary()
        {
            string summary = "User Preferences:\n";
            foreach (var pref in _userPreferences.Values)
            {
                summary += string.Format("  {0}: Weight={1:F2}, Priority={2}\n",
                    pref.Category, pref.Weight, pref.Priority);
            }
            return summary;
        }

        public string GetDailyPatternsSummary()
        {
            string summary = "Daily Patterns:\n";
            string[] days = new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
            for (int d = 0; d < days.Length; d++)
            {
                if (_dailyPatterns.ContainsKey(days[d]))
                {
                    DailyUsagePattern pattern = _dailyPatterns[days[d]];
                    double avgUsage = 0;
                    for (int h = 0; h < 24; h++)
                        avgUsage += pattern.UsageHours[h];
                    avgUsage /= 24;
                    summary += string.Format("  {0}: Avg usage={1:P0}\n", days[d], avgUsage);
                }
            }
            return summary;
        }

        public double GetUserPreferenceWeight(string category)
        {
            if (_userPreferences.ContainsKey(category))
                return _userPreferences[category].Weight;
            return 0.5;
        }

        public bool IsPreferredTimeForAction(string category)
        {
            if (!_userPreferences.ContainsKey(category)) return true;
            return _userPreferences[category].Weight > 0.6;
        }
    }

    public class UserPreference
    {
        public string Category;
        public double Weight;
        public int Priority;
    }

    public class DailyUsagePattern
    {
        public string DayOfWeek;
        public double[] UsageHours = new double[24];
        public double[] AvgCpuByHour = new double[24];
        public double[] AvgRamByHour = new double[24];
        public double[] GamingByHour = new double[24];
    }

    public class UserActionRecord
    {
        public string Category;
        public bool Accepted;
        public DateTime Timestamp;
    }

    public class UserBehaviorResult
    {
        public string Preferences;
        public string Patterns;
        public List<UserBehaviorRecommendation> Recommendations = new List<UserBehaviorRecommendation>();
    }

    public class UserBehaviorRecommendation
    {
        public string Category;
        public string Recommendation;
        public int Priority;
    }
}
