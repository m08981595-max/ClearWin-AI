using System;
using System.Collections.Generic;
using System.Linq;

namespace ClearWin
{
    public partial class SmartAgent
    {
        // === Adaptive Boosters Fields ===
        private List<AdaptiveBooster> _adaptiveBoosters = new List<AdaptiveBooster>();
        private DateTime _lastBoosterUpdate = DateTime.MinValue;
        private const int BOOSTER_UPDATE_INTERVAL_SECONDS = 30;
        private Dictionary<string, BoosterAdjustment> _boosterHistory = new Dictionary<string, BoosterAdjustment>();

        // === Public Accessors ===
        public List<AdaptiveBooster> GetAdaptiveBoosters() { return new List<AdaptiveBooster>(_adaptiveBoosters); }

        // === Initialize Adaptive Boosters ===
        public void InitializeAdaptiveBoosters()
        {
            _adaptiveBoosters.Clear();

            _adaptiveBoosters.Add(new AdaptiveBooster
            {
                Name = "Memory Optimizer",
                Description = "Adaptive memory cleaning based on current usage",
                Category = "Memory",
                BaseStrength = 0.5,
                CurrentStrength = 0.5,
                MinStrength = 0.1,
                MaxStrength = 1.0,
                AdaptRate = 0.05,
                TriggerThreshold = 60,
                Actions = new List<string> { "LightClean", "PreemptiveClean", "AggressiveClean" }
            });
            _adaptiveBoosters.Add(new AdaptiveBooster
            {
                Name = "CPU Manager",
                Description = "Adaptive CPU load management",
                Category = "CPU",
                BaseStrength = 0.5,
                CurrentStrength = 0.5,
                MinStrength = 0.1,
                MaxStrength = 1.0,
                AdaptRate = 0.05,
                TriggerThreshold = 70,
                Actions = new List<string> { "LimitBackground", "SetLowIoPriority", "LimitBackgroundAffinity" }
            });
            _adaptiveBoosters.Add(new AdaptiveBooster
            {
                Name = "Thermal Guardian",
                Description = "Adaptive thermal protection",
                Category = "Thermal",
                BaseStrength = 0.6,
                CurrentStrength = 0.6,
                MinStrength = 0.2,
                MaxStrength = 1.0,
                AdaptRate = 0.08,
                TriggerThreshold = 65,
                Actions = new List<string> { "ReduceThermal", "EmergencyThrottle" }
            });
            _adaptiveBoosters.Add(new AdaptiveBooster
            {
                Name = "Game Booster",
                Description = "Adaptive game performance optimization",
                Category = "Game",
                BaseStrength = 0.5,
                CurrentStrength = 0.5,
                MinStrength = 0.1,
                MaxStrength = 1.0,
                AdaptRate = 0.06,
                TriggerThreshold = 0,
                Actions = new List<string> { "BoostGamePriority", "SetGameAffinity", "StabilizeFrames" }
            });
            _adaptiveBoosters.Add(new AdaptiveBooster
            {
                Name = "Power Saver",
                Description = "Adaptive power consumption management",
                Category = "Power",
                BaseStrength = 0.4,
                CurrentStrength = 0.4,
                MinStrength = 0.1,
                MaxStrength = 0.9,
                AdaptRate = 0.04,
                TriggerThreshold = 0,
                Actions = new List<string> { "BatterySaver", "BalancedPerformance" }
            });
            _adaptiveBoosters.Add(new AdaptiveBooster
            {
                Name = "Disk Cleaner",
                Description = "Adaptive disk I/O and storage management",
                Category = "Disk",
                BaseStrength = 0.3,
                CurrentStrength = 0.3,
                MinStrength = 0.1,
                MaxStrength = 0.8,
                AdaptRate = 0.03,
                TriggerThreshold = 75,
                Actions = new List<string> { "PurgeStandbyList", "LightClean" }
            });

            AddLog("BOOST", "AdaptiveBoosters", "Initialized " + _adaptiveBoosters.Count + " adaptive boosters");
        }

        // === Update Booster Strengths ===
        public void UpdateBoosters()
        {
            if ((DateTime.Now - _lastBoosterUpdate).TotalSeconds < BOOSTER_UPDATE_INTERVAL_SECONDS) return;

            foreach (var booster in _adaptiveBoosters)
            {
                double metricValue = GetBoosterMetric(booster.Category);
                bool isTriggered = metricValue > booster.TriggerThreshold;

                // Adapt strength based on conditions
                if (isTriggered)
                {
                    // Increase strength when threshold is exceeded
                    booster.CurrentStrength = Math.Min(booster.MaxStrength,
                        booster.CurrentStrength + booster.AdaptRate);
                }
                else
                {
                    // Decrease strength when conditions are good
                    booster.CurrentStrength = Math.Max(booster.MinStrength,
                        booster.CurrentStrength - booster.AdaptRate * 0.5);
                }

                // Temperature-based override: if hot, boost thermal guardian
                if (booster.Category == "Thermal" && _smoothTemp > 75)
                {
                    booster.CurrentStrength = Math.Min(booster.MaxStrength,
                        booster.CurrentStrength + booster.AdaptRate * 2);
                }

                // Game mode override: boost game booster during games
                if (booster.Category == "Game" && _currentState.IsGame)
                {
                    booster.CurrentStrength = Math.Min(booster.MaxStrength,
                        booster.CurrentStrength + booster.AdaptRate);
                }

                // Battery override: boost power saver on battery
                if (booster.Category == "Power" && _isOnBattery)
                {
                    booster.CurrentStrength = Math.Min(booster.MaxStrength,
                        booster.CurrentStrength + booster.AdaptRate);
                }

                // Record adjustment
                booster.LastAdjustment = DateTime.Now;
                booster.AdjustmentCount++;
            }

            _lastBoosterUpdate = DateTime.Now;
        }

        // === Get Booster Metric Value ===
        private double GetBoosterMetric(string category)
        {
            switch (category)
            {
                case "Memory": return _smoothRam;
                case "CPU": return _smoothCpu;
                case "Thermal": return _smoothTemp;
                case "Game": return _currentState.IsGame ? _smoothGpu : 0;
                case "Power": return _isOnBattery ? (100 - _batteryPercent) : 0;
                case "Disk": return _currentState.DiskUsage;
                default: return 0;
            }
        }

        // === Apply Booster to Decision ===
        public AgentDecision ApplyBooster(AgentDecision decision)
        {
            foreach (var booster in _adaptiveBoosters)
            {
                if (booster.Actions.Contains(decision.Action))
                {
                    // Scale confidence by booster strength
                    decision.Confidence = Math.Min(1.0,
                        decision.Confidence * (0.5 + booster.CurrentStrength * 0.5));

                    // Record that this booster influenced the decision
                    booster.TotalActivations++;
                    break;
                }
            }
            return decision;
        }

        // === Get Booster Status Summary ===
        public string GetBoosterStatus()
        {
            if (_adaptiveBoosters.Count == 0) return "No boosters initialized";
            int activeCount = 0;
            foreach (var b in _adaptiveBoosters)
            {
                if (b.CurrentStrength > b.BaseStrength) activeCount++;
            }
            return string.Format("{0}/{1} boosters above base strength", activeCount, _adaptiveBoosters.Count);
        }
    }
}
