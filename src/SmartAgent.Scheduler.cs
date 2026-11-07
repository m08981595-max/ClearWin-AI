using System;
using System.Collections.Generic;

namespace ClearWin
{
    public partial class SmartAgent
    {
        // ===================== AI SCHEDULER =====================
        private List<ScheduledTask> _scheduledTasks = new List<ScheduledTask>();
        private DateTime _lastScheduleCheck = DateTime.MinValue;
        private const int SCHEDULE_CHECK_INTERVAL_SECONDS = 60; // check every 60 seconds

        public void InitializeScheduler()
        {
            _scheduledTasks.Add(new ScheduledTask { Name = "MemoryClean", Category = "Performance", IntervalMinutes = 30, Priority = "Medium", SkipDuringGaming = true });
            _scheduledTasks.Add(new ScheduledTask { Name = "StandbyPurge", Category = "Performance", IntervalMinutes = 60, Priority = "Low", SkipDuringGaming = true });
            _scheduledTasks.Add(new ScheduledTask { Name = "Benchmark", Category = "Diagnostics", IntervalMinutes = 60, Priority = "Low", SkipDuringGaming = true });
            _scheduledTasks.Add(new ScheduledTask { Name = "SelfDiag", Category = "Health", IntervalMinutes = 15, Priority = "Medium", SkipDuringGaming = false });
            _scheduledTasks.Add(new ScheduledTask { Name = "RootCauseAnalysis", Category = "Analysis", IntervalMinutes = 5, Priority = "High", SkipDuringGaming = false });
            _scheduledTasks.Add(new ScheduledTask { Name = "SnapshotTimeline", Category = "History", IntervalMinutes = 1, Priority = "Low", SkipDuringGaming = false });
            _scheduledTasks.Add(new ScheduledTask { Name = "SmartSuggestions", Category = "Intelligence", IntervalMinutes = 10, Priority = "Medium", SkipDuringGaming = true });
            _scheduledTasks.Add(new ScheduledTask { Name = "DeviceProfile", Category = "Hardware", IntervalMinutes = 30, Priority = "Low", SkipDuringGaming = false });
        }

        public void CheckScheduledTasks()
        {
            if ((DateTime.Now - _lastScheduleCheck).TotalSeconds < SCHEDULE_CHECK_INTERVAL_SECONDS) return;
            _lastScheduleCheck = DateTime.Now;

            foreach (var task in _scheduledTasks)
            {
                if (!task.Enabled) continue;
                if (task.SkipDuringGaming && _currentState != null && _currentState.IsGame) continue;
                if ((DateTime.Now - task.LastRun).TotalMinutes < task.IntervalMinutes) continue;

                task.LastRun = DateTime.Now;
                task.RunCount++;

                switch (task.Name)
                {
                    case "MemoryClean":
                        if (_currentState != null && _currentState.RamUsage > _deviceProfile.RamCleanThreshold)
                        {
                            // Central Execution Gate: scheduled memory clean is
                            // autonomous system mutation.
                            if (CanExecuteAction("ScheduledMemoryClean", false))
                                ExecuteMemoryClean();
                        }
                        break;
                    case "StandbyPurge":
                        // Central Execution Gate: autonomous system mutation.
                        if (CanExecuteAction("ScheduledStandbyPurge", false))
                            ExecutePurgeStandby();
                        break;
                    case "Benchmark":
                        RunBenchmark();
                        break;
                    case "SelfDiag":
                        RunSelfDiagnostics();
                        break;
                    case "RootCauseAnalysis":
                        AnalyzeRootCauses();
                        break;
                    case "SnapshotTimeline":
                        RecordPerformanceSnapshot();
                        break;
                    case "SmartSuggestions":
                        GenerateSuggestions();
                        break;
                    case "DeviceProfile":
                        ProfileDevice();
                        break;
                }
            }
        }

        // ===================== AI POWER OPTIMIZER =====================
        private PowerOptimizationResult _powerResult = new PowerOptimizationResult();
        private DateTime _lastPowerOptimize = DateTime.MinValue;
        private const int POWER_OPTIMIZE_INTERVAL_SECONDS = 30;

        public PowerOptimizationResult OptimizePower()
        {
            if ((DateTime.Now - _lastPowerOptimize).TotalSeconds < POWER_OPTIMIZE_INTERVAL_SECONDS) return _powerResult;
            _lastPowerOptimize = DateTime.Now;

            lock (_stateLock)
            {
                if (_currentState == null) return _powerResult;

                _powerResult = new PowerOptimizationResult();
                _powerResult.Timestamp = DateTime.Now;

                // Calculate multi-factor score
                double performanceWeight = 0.3;
                double thermalWeight = 0.25;
                double batteryWeight = 0.25;
                double noiseWeight = 0.1;
                double longevityWeight = 0.1;

                // Performance factor (higher is better)
                double perfScore = 100 - _currentState.CpuUsage;
                if (_currentState.Fps > 0) perfScore = (perfScore + _currentState.Fps) / 2;

                // Thermal factor (lower temp is better)
                double thermalScore = Math.Max(0, 100 - (_currentState.CpuTemp - 40) * 2);

                // Battery factor
                double batteryScore = _currentState.IsCharging ? 100 : _currentState.BatteryLevel;

                // Noise factor (proxy via temperature - hotter = fans louder)
                double noiseScore = Math.Max(0, 100 - (_currentState.CpuTemp - 50) * 2.5);

                // Longevity factor (lower temp + lower usage = longer hardware life)
                double longevityScore = (thermalScore + (100 - _currentState.CpuUsage)) / 2;

                _powerResult.OverallScore = perfScore * performanceWeight + thermalScore * thermalWeight + batteryScore * batteryWeight + noiseScore * noiseWeight + longevityScore * longevityWeight;

                // Determine optimal power plan
                if (_currentState.IsGame)
                {
                    _powerResult.RecommendedPlan = "Ultimate Performance";
                    _powerResult.Reason = "Gaming detected - maximize performance";
                }
                else if (_currentState.IsCharging && _currentState.CpuUsage > 50)
                {
                    _powerResult.RecommendedPlan = "High Performance";
                    _powerResult.Reason = "Plugged in with active workload";
                }
                else if (!_currentState.IsCharging && _currentState.BatteryLevel < 30)
                {
                    _powerResult.RecommendedPlan = "Power Saver";
                    _powerResult.Reason = "Low battery - prioritize battery life";
                }
                else if (_currentState.CpuTemp > _deviceProfile.ThermalWarningTemp)
                {
                    _powerResult.RecommendedPlan = "Balanced";
                    _powerResult.Reason = "Temperature high - balance performance and cooling";
                }
                else
                {
                    _powerResult.RecommendedPlan = "Balanced";
                    _powerResult.Reason = "Normal usage - balanced approach";
                }

                // Apply if different from current
                string currentPlan = GetCurrentPowerPlan();
                if (_powerResult.RecommendedPlan != currentPlan)
                {
                    // Central Execution Gate: auto power plan switching is
                    // autonomous system mutation.
                    if (CanExecuteAction("AutoPowerPlan", false))
                    {
                        _powerResult.ActionTaken = true;
                        ExecutePowerPlan(_powerResult.RecommendedPlan.ToLower().Replace(" ", " "));
                    }
                }
            }

            return _powerResult;
        }

        // ===================== AI RECOMMENDATION ENGINE =====================
        private List<AIRecommendation> _recommendations = new List<AIRecommendation>();
        private DateTime _lastRecommendationTime = DateTime.MinValue;
        private const int RECOMMENDATION_INTERVAL_SECONDS = 120;

        public List<AIRecommendation> GenerateRecommendations()
        {
            if ((DateTime.Now - _lastRecommendationTime).TotalSeconds < RECOMMENDATION_INTERVAL_SECONDS) return GetRecommendations();
            lock (_stateLock)
            {
                _lastRecommendationTime = DateTime.Now;
                _recommendations.Clear();
                if (_currentState == null) return new List<AIRecommendation>(_recommendations);

                // RAM upgrade recommendation
                if (_deviceProfile.TotalRamGB <= 8 && _currentState.RamUsage > 80)
                {
                    _recommendations.Add(new AIRecommendation
                    {
                        Type = "Hardware",
                        Title = "RAM Upgrade Recommended",
                        Description = string.Format("Your {0:F0}GB RAM is frequently at {1:F0}% usage. Upgrading to {2}GB would significantly improve performance.",
                            _deviceProfile.TotalRamGB, _currentState.RamUsage, _deviceProfile.TotalRamGB * 2),
                        Impact = "High",
                        Category = "Memory",
                        EstimatedImprovement = 25
                    });
                }

                // SSD recommendation
                if (_deviceProfile.DiskType == "HDD")
                {
                    _recommendations.Add(new AIRecommendation
                    {
                        Type = "Hardware",
                        Title = "SSD Upgrade Recommended",
                        Description = "You're using an HDD. Switching to an SSD would dramatically improve boot time, app loading, and overall responsiveness.",
                        Impact = "Critical",
                        Category = "Storage",
                        EstimatedImprovement = 40
                    });
                }

                // Disk cleanup recommendation
                if (_currentState.DiskUsage > 85)
                {
                    _recommendations.Add(new AIRecommendation
                    {
                        Type = "Maintenance",
                        Title = "Disk Cleanup Needed",
                        Description = string.Format("Your disk is {0:F0}% full. Running disk cleanup can free up space and improve performance.", _currentState.DiskUsage),
                        Impact = "Medium",
                        Category = "Storage",
                        EstimatedImprovement = 10
                    });
                }

                // Startup optimization
                if (_currentState.ProcessCount > _deviceProfile.MaxBackgroundProcesses)
                {
                    _recommendations.Add(new AIRecommendation
                    {
                        Type = "Optimization",
                        Title = "Too Many Background Processes",
                        Description = string.Format("{0} processes running (recommended max: {1}). Disabling unnecessary startup programs can help.",
                            _currentState.ProcessCount, _deviceProfile.MaxBackgroundProcesses),
                        Impact = "Medium",
                        Category = "Performance",
                        EstimatedImprovement = 15
                    });
                }

                // Temperature recommendation
                if (_currentState.CpuTemp > _deviceProfile.ThermalWarningTemp)
                {
                    _recommendations.Add(new AIRecommendation
                    {
                        Type = "Maintenance",
                        Title = "Thermal Maintenance Needed",
                        Description = "CPU temperature is high. Consider cleaning dust from fans, reapplying thermal paste, or improving airflow.",
                        Impact = "High",
                        Category = "Thermal",
                        EstimatedImprovement = 20
                    });
                }

                // Driver recommendation (placeholder - real implementation would check WMI)
                _recommendations.Add(new AIRecommendation
                {
                    Type = "Maintenance",
                    Title = "Driver Check Recommended",
                    Description = "Periodically check for GPU and chipset driver updates for optimal performance and stability.",
                    Impact = "Low",
                    Category = "Drivers",
                    EstimatedImprovement = 5
                });
            }

            return GetRecommendations();
        }

        public List<AIRecommendation> GetRecommendations()
        {
            lock (_stateLock) { return new List<AIRecommendation>(_recommendations); }
        }
        public List<ScheduledTask> GetScheduledTasks()
        {
            lock (_stateLock) { return new List<ScheduledTask>(_scheduledTasks); }
        }
    }

    // ===================== MODELS =====================
    public class ScheduledTask
    {
        public string Name;
        public string Category;
        public int IntervalMinutes;
        public string Priority;
        public bool SkipDuringGaming;
        public bool Enabled;
        public DateTime LastRun;
        public int RunCount;

        public ScheduledTask()
        {
            Enabled = true;
            LastRun = DateTime.MinValue;
            RunCount = 0;
        }
    }

    public class PowerOptimizationResult
    {
        public DateTime Timestamp;
        public double OverallScore;
        public string RecommendedPlan;
        public string Reason;
        public bool ActionTaken;

        public PowerOptimizationResult() { Timestamp = DateTime.Now; }
    }

    public class AIRecommendation
    {
        public string Type;
        public string Title;
        public string Description;
        public string Impact;
        public string Category;
        public double EstimatedImprovement;
        public DateTime Created;

        public AIRecommendation() { Created = DateTime.Now; }
    }
}
