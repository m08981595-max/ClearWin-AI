using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ClearWin
{
    public partial class SmartAgent
    {
        // === Seasonal Learning Fields ===
        private SeasonalProfile _currentSeasonalProfile;
        private List<SeasonalProfile> _seasonalProfiles = new List<SeasonalProfile>();
        private DateTime _lastSeasonalUpdate = DateTime.MinValue;
        private const int SEASONAL_UPDATE_INTERVAL_SECONDS = 300;
        private Dictionary<int, Dictionary<int, UsageSeason>> _dayHourSeasonMap = new Dictionary<int, Dictionary<int, UsageSeason>>();
        private Dictionary<UsageSeason, SeasonalStrategy> _seasonStrategies = new Dictionary<UsageSeason, SeasonalStrategy>();
        private int _seasonalSampleCount = 0;
        private UsageSeason _detectedSeason = UsageSeason.Unknown;

        // === Public Accessors ===
        public UsageSeason GetCurrentSeason() { return _detectedSeason; }
        public SeasonalStrategy GetCurrentStrategy()
        {
            SeasonalStrategy s;
            if (_seasonStrategies.TryGetValue(_detectedSeason, out s)) return s;
            return GetDefaultStrategy(_detectedSeason);
        }
        public List<SeasonalProfile> GetSeasonalProfiles() { return new List<SeasonalProfile>(_seasonalProfiles); }
        public SeasonalProfile GetCurrentSeasonalProfile() { return _currentSeasonalProfile; }

        // === Initialize Default Seasonal Strategies ===
        public void InitializeSeasonalLearning()
        {
            _seasonStrategies[UsageSeason.MorningStudy] = new SeasonalStrategy
            {
                Season = UsageSeason.MorningStudy,
                Name = "Morning Study",
                Description = "Study mode: minimize distractions, save battery",
                Aggressiveness = 0.3,
                PreferBatterySaver = true,
                PreferQuiet = true,
                MaxActionsPerHour = 3,
                PriorityCategories = new List<string> { "Battery", "Background", "Power" }
            };
            _seasonStrategies[UsageSeason.AfternoonWork] = new SeasonalStrategy
            {
                Season = UsageSeason.AfternoonWork,
                Name = "Afternoon Work",
                Description = "Work mode: balanced performance, stable system",
                Aggressiveness = 0.5,
                PreferBatterySaver = false,
                PreferQuiet = false,
                MaxActionsPerHour = 5,
                PriorityCategories = new List<string> { "Performance", "Memory", "CPU" }
            };
            _seasonStrategies[UsageSeason.EveningGaming] = new SeasonalStrategy
            {
                Season = UsageSeason.EveningGaming,
                Name = "Evening Gaming",
                Description = "Gaming mode: maximum performance, thermal awareness",
                Aggressiveness = 0.8,
                PreferBatterySaver = false,
                PreferQuiet = false,
                MaxActionsPerHour = 8,
                PriorityCategories = new List<string> { "Game", "Thermal", "Memory", "GPU" }
            };
            _seasonStrategies[UsageSeason.WeekendHeavy] = new SeasonalStrategy
            {
                Season = UsageSeason.WeekendHeavy,
                Name = "Weekend Heavy",
                Description = "Heavy usage: editing, streaming, multitasking",
                Aggressiveness = 0.7,
                PreferBatterySaver = false,
                PreferQuiet = false,
                MaxActionsPerHour = 7,
                PriorityCategories = new List<string> { "Memory", "CPU", "Disk", "Thermal" }
            };
            _seasonStrategies[UsageSeason.NightIdle] = new SeasonalStrategy
            {
                Season = UsageSeason.NightIdle,
                Name = "Night Idle",
                Description = "Night/idle: deep clean, maintenance, updates",
                Aggressiveness = 0.9,
                PreferBatterySaver = true,
                PreferQuiet = true,
                MaxActionsPerHour = 10,
                PriorityCategories = new List<string> { "Maintenance", "Disk", "Memory", "Background" }
            };
            _seasonStrategies[UsageSeason.Unknown] = GetDefaultStrategy(UsageSeason.Unknown);

            AddLog("SEASON", "SeasonalLearning", "Seasonal strategies initialized: 6 strategies");
        }

        // === Get Default Strategy ===
        private SeasonalStrategy GetDefaultStrategy(UsageSeason season)
        {
            return new SeasonalStrategy
            {
                Season = season,
                Name = "Default",
                Description = "Default balanced strategy",
                Aggressiveness = 0.5,
                PreferBatterySaver = false,
                PreferQuiet = false,
                MaxActionsPerHour = 5,
                PriorityCategories = new List<string> { "Performance", "Memory" }
            };
        }

        // === Detect Current Season ===
        public void DetectSeason()
        {
            if ((DateTime.Now - _lastSeasonalUpdate).TotalSeconds < SEASONAL_UPDATE_INTERVAL_SECONDS) return;

            int hour = DateTime.Now.Hour;
            int dayOfWeek = (int)DateTime.Now.DayOfWeek;
            bool isWeekend = (dayOfWeek == 0 || dayOfWeek == 6);

            UsageSeason detected = UsageSeason.Unknown;

            if (isWeekend)
            {
                detected = UsageSeason.WeekendHeavy;
            }
            else if (hour >= 6 && hour < 12)
            {
                detected = UsageSeason.MorningStudy;
            }
            else if (hour >= 12 && hour < 18)
            {
                detected = UsageSeason.AfternoonWork;
            }
            else if (hour >= 18 && hour < 23)
            {
                detected = UsageSeason.EveningGaming;
            }
            else
            {
                detected = UsageSeason.NightIdle;
            }

            if (_currentState.IsGame && detected != UsageSeason.EveningGaming)
            {
                detected = UsageSeason.EveningGaming;
            }
            else if (_currentState.CpuUsage > 80 && !_currentState.IsGame)
            {
                if (detected == UsageSeason.MorningStudy) detected = UsageSeason.AfternoonWork;
            }
            else if (_currentState.CpuUsage < 15 && _currentState.RamUsage < 30)
            {
                if (detected != UsageSeason.NightIdle && hour >= 22)
                    detected = UsageSeason.NightIdle;
            }

            if (detected != _detectedSeason)
            {
                AddLog("SEASON", "SeasonalLearning",
                    string.Format("Season changed: {0} -> {1} (Hour={2}, Day={3}, Game={4})",
                    _detectedSeason, detected, hour, dayOfWeek, _currentState.IsGame));
                _detectedSeason = detected;

                _currentSeasonalProfile = new SeasonalProfile
                {
                    Season = detected,
                    DetectedAt = DateTime.Now,
                    Hour = hour,
                    DayOfWeek = dayOfWeek,
                    IsGame = _currentState.IsGame,
                    CpuLoad = _smoothCpu,
                    Strategy = GetCurrentStrategy()
                };
                _seasonalProfiles.Add(_currentSeasonalProfile);
                if (_seasonalProfiles.Count > 50) _seasonalProfiles.RemoveAt(0);
            }

            if (!_dayHourSeasonMap.ContainsKey(dayOfWeek))
                _dayHourSeasonMap[dayOfWeek] = new Dictionary<int, UsageSeason>();
            _dayHourSeasonMap[dayOfWeek][hour] = detected;
            _seasonalSampleCount++;

            _lastSeasonalUpdate = DateTime.Now;
        }

        // === Adjust Decision Based on Season ===
        public AgentDecision AdjustDecisionBySeason(AgentDecision decision)
        {
            if (_detectedSeason == UsageSeason.Unknown) return decision;

            SeasonalStrategy strategy = GetCurrentStrategy();

            decision.Confidence = Math.Max(0.1, Math.Min(1.0,
                decision.Confidence * (0.5 + strategy.Aggressiveness)));

            string category = decision.Category.ToString();
            bool isPriority = strategy.PriorityCategories.Contains(category);

            if (!isPriority)
            {
                decision.Confidence = Math.Max(0.1, decision.Confidence * 0.7);
            }

            if (strategy.PreferBatterySaver && decision.Action != "BatterySaver")
            {
                if (decision.Category == DecisionCategory.PowerPlan)
                    decision.Confidence = Math.Min(1.0, decision.Confidence * 1.2);
            }

            if (strategy.PreferQuiet)
            {
                if (decision.Action == "AggressiveClean" || decision.Action == "EmergencyThrottle")
                {
                    decision.Confidence = Math.Max(0.1, decision.Confidence * 0.5);
                }
            }

            return decision;
        }

        // === Predict Next Season ===
        public UsageSeason PredictNextSeason()
        {
            int nextHour = (DateTime.Now.Hour + 1) % 24;
            int dayOfWeek = (int)DateTime.Now.DayOfWeek;
            bool isWeekend = (dayOfWeek == 0 || dayOfWeek == 6);

            if (isWeekend) return UsageSeason.WeekendHeavy;
            if (nextHour >= 6 && nextHour < 12) return UsageSeason.MorningStudy;
            if (nextHour >= 12 && nextHour < 18) return UsageSeason.AfternoonWork;
            if (nextHour >= 18 && nextHour < 23) return UsageSeason.EveningGaming;
            return UsageSeason.NightIdle;
        }
    }
}
