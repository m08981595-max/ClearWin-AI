using System;
using System.Collections.Generic;
using System.Linq;

namespace ClearWin
{
    public partial class SmartAgent
    {
        // === Smart Goals Fields ===
        private SmartGoal _activeGoal;
        private List<SmartGoal> _availableGoals = new List<SmartGoal>();
        private DateTime _lastGoalUpdate = DateTime.MinValue;
        private const int GOAL_UPDATE_INTERVAL_SECONDS = 30;
        private Dictionary<string, double> _goalMetrics = new Dictionary<string, double>();
        private List<GoalProgress> _goalProgressHistory = new List<GoalProgress>();

        // === Public Accessors ===
        public SmartGoal GetActiveGoal() { return _activeGoal; }
        public List<SmartGoal> GetAvailableGoals() { return new List<SmartGoal>(_availableGoals); }
        public void SetActiveGoal(SmartGoal goal)
        {
            if (goal != null)
            {
                _activeGoal = goal;
                _goalProgressHistory.Clear();
                AddLog("INFO", "Goals", string.Format("Goal changed to: {0}", goal.Name));
            }
        }
        public GoalProgress GetGoalProgress()
        {
            if (_activeGoal == null) return null;
            return CalculateGoalProgress(_activeGoal);
        }

        // === Initialize Available Goals ===
        public void InitializeSmartGoals()
        {
            _availableGoals.Clear();
            _availableGoals.Add(new SmartGoal
            {
                Id = "max_fps",
                Name = "Maximum FPS",
                Description = "Prioritize highest possible frame rate",
                Icon = "\uE7F4",
                TargetMetrics = new Dictionary<string, double> { { "Fps", 120 }, { "FpsStability", 95 } },
                Aggressiveness = 0.9,
                PriorityCategories = new List<string> { "Game", "GPU", "CPU", "Memory" }
            });
            _availableGoals.Add(new SmartGoal
            {
                Id = "low_temp",
                Name = "Lowest Temperature",
                Description = "Keep system cool and quiet",
                Icon = "\uE9CA",
                TargetMetrics = new Dictionary<string, double> { { "Temp", 50 }, { "CpuUsage", 60 } },
                Aggressiveness = 0.4,
                PriorityCategories = new List<string> { "Thermal", "Power", "Background" }
            });
            _availableGoals.Add(new SmartGoal
            {
                Id = "low_power",
                Name = "Least Power Usage",
                Description = "Maximize battery life and efficiency",
                Icon = "\uE945",
                TargetMetrics = new Dictionary<string, double> { { "Battery", 100 }, { "CpuUsage", 40 } },
                Aggressiveness = 0.3,
                PriorityCategories = new List<string> { "Power", "Battery", "Background" }
            });
            _availableGoals.Add(new SmartGoal
            {
                Id = "best_stability",
                Name = "Best Stability",
                Description = "Maximum system stability and reliability",
                Icon = "\uE73E",
                TargetMetrics = new Dictionary<string, double> { { "FpsStability", 98 }, { "HealthScore", 90 } },
                Aggressiveness = 0.6,
                PriorityCategories = new List<string> { "Memory", "CPU", "Disk", "Security" }
            });
            _availableGoals.Add(new SmartGoal
            {
                Id = "balanced",
                Name = "Balanced",
                Description = "Best balance of performance, temperature, and power",
                Icon = "\uE964",
                TargetMetrics = new Dictionary<string, double> { { "Fps", 60 }, { "Temp", 65 }, { "CpuUsage", 70 } },
                Aggressiveness = 0.5,
                PriorityCategories = new List<string> { "Performance", "Thermal", "Power" }
            });
            _availableGoals.Add(new SmartGoal
            {
                Id = "quiet",
                Name = "Least Noise",
                Description = "Minimize fan noise by reducing heat",
                Icon = "\uE767",
                TargetMetrics = new Dictionary<string, double> { { "Temp", 55 }, { "FanSpeed", 30 } },
                Aggressiveness = 0.35,
                PriorityCategories = new List<string> { "Thermal", "Power", "Background" }
            });

            // Default goal
            _activeGoal = _availableGoals[4]; // Balanced
            AddLog("GOALS", "SmartGoals", "Initialized " + _availableGoals.Count + " goals, default: Balanced");
        }

        // === Set Active Goal ===
        public void SetGoal(string goalId)
        {
            foreach (var goal in _availableGoals)
            {
                if (goal.Id == goalId)
                {
                    _activeGoal = goal;
                    AddLog("GOALS", "SmartGoals", "Goal changed to: " + goal.Name);
                    return;
                }
            }
        }

        // === Adjust Decision Based on Goal ===
        public AgentDecision AdjustDecisionByGoal(AgentDecision decision)
        {
            if (_activeGoal == null) return decision;

            // Check if this action's category is a priority for the current goal
            string category = decision.Category.ToString();
            bool isPriority = _activeGoal.PriorityCategories.Contains(category);

            if (isPriority)
            {
                decision.Confidence = Math.Min(1.0, decision.Confidence * (0.8 + _activeGoal.Aggressiveness * 0.4));
            }
            else
            {
                decision.Confidence = Math.Max(0.05, decision.Confidence * (1.0 - _activeGoal.Aggressiveness * 0.3));
            }

            // Goal-specific adjustments
            switch (_activeGoal.Id)
            {
                case "max_fps":
                    if (decision.Action == "StabilizeFrames" || decision.Action == "ReduceMicroStutter" ||
                        decision.Action == "BoostGamePriority")
                    {
                        decision.Confidence = Math.Min(1.0, decision.Confidence * 1.3);
                    }
                    break;
                case "low_temp":
                    if (decision.Action.Contains("Throttle") || decision.Action == "ReduceThermal" ||
                        decision.Action == "BatterySaver")
                    {
                        decision.Confidence = Math.Min(1.0, decision.Confidence * 1.4);
                    }
                    break;
                case "low_power":
                    if (decision.Action == "BatterySaver" || decision.Action == "BalancedPerformance")
                    {
                        decision.Confidence = Math.Min(1.0, decision.Confidence * 1.5);
                    }
                    if (decision.Action == "AggressiveClean" || decision.Action == "UnparkAllCores")
                    {
                        decision.Confidence = Math.Max(0.05, decision.Confidence * 0.3);
                    }
                    break;
                case "best_stability":
                    if (decision.Action == "LightClean" || decision.Action == "PreemptiveClean")
                    {
                        decision.Confidence = Math.Min(1.0, decision.Confidence * 1.2);
                    }
                    if (decision.Action == "AggressiveClean" || decision.Action == "EmergencyThrottle")
                    {
                        decision.Confidence = Math.Max(0.05, decision.Confidence * 0.4);
                    }
                    break;
            }

            return decision;
        }

        // === Calculate Goal Progress ===
        private GoalProgress CalculateGoalProgress(SmartGoal goal)
        {
            var progress = new GoalProgress();
            progress.GoalName = goal.Name;
            progress.Timestamp = DateTime.Now;
            progress.Metrics = new Dictionary<string, double>();

            foreach (var kv in goal.TargetMetrics)
            {
                double current = 0;
                switch (kv.Key)
                {
                    case "Fps": current = _smoothFps; break;
                    case "Temp": current = _smoothTemp; break;
                    case "CpuUsage": current = _smoothCpu; break;
                    case "RamUsage": current = _smoothRam; break;
                    case "Battery": current = _batteryPercent; break;
                    case "FpsStability": current = _currentState.FpsStability; break;
                    case "HealthScore": current = SystemHealthScore; break;
                }
                progress.Metrics[kv.Key] = current;
            }

            // Calculate overall progress (0-100%)
            double totalProgress = 0;
            int metricCount = 0;
            foreach (var kv in goal.TargetMetrics)
            {
                double current;
                if (progress.Metrics.TryGetValue(kv.Key, out current))
                {
                    double target = kv.Value;
                    double metricProgress;
                    // For "lower is better" metrics (Temp, CpuUsage)
                    if (kv.Key == "Temp" || kv.Key == "CpuUsage" || kv.Key == "RamUsage")
                    {
                        metricProgress = target > 0 ? Math.Max(0, Math.Min(100, (1 - current / target) * 100)) : 50;
                    }
                    else
                    {
                        metricProgress = target > 0 ? Math.Max(0, Math.Min(100, (current / target) * 100)) : 50;
                    }
                    totalProgress += metricProgress;
                    metricCount++;
                }
            }
            progress.OverallProgress = metricCount > 0 ? totalProgress / metricCount : 0;

            return progress;
        }

        // === Update Goal Metrics (called from Tick) ===
        public void UpdateGoalMetrics()
        {
            if ((DateTime.Now - _lastGoalUpdate).TotalSeconds < GOAL_UPDATE_INTERVAL_SECONDS) return;
            if (_activeGoal == null) return;

            var progress = CalculateGoalProgress(_activeGoal);
            _goalProgressHistory.Add(progress);
            if (_goalProgressHistory.Count > 100) _goalProgressHistory.RemoveAt(0);

            _lastGoalUpdate = DateTime.Now;
        }
    }
}
