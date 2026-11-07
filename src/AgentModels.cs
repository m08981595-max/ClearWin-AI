using System;
using System.Collections.Generic;
using System.Text;

namespace ClearWin
{
    public enum AgentMode { Off, AutoPilot, Assisted, GameMode, BalanceMode, CoolMode, Custom }
    public enum DecisionSafety { Safe, NeedsApproval, Dangerous }
    public enum DecisionCategory { MemoryClean, ProcessPriority, PowerPlan, BackgroundControl, PurgeStandby, GameBoost, ThermalProtect, CpuAffinity, IoPriority, CoreParking, NetworkOptimize, PowerOptimize, SystemStatus, DiskCleanup }
    public enum AgentCategory { Memory, CPU, GPU, Disk, Network, Thermal, Power, Game, Background, Security }
    public enum ExperienceResult { Unknown, Improved, Degraded, Neutral }
    public enum WorkloadType { Desktop, Gaming, Work, Montage, Browsing, Idle, Charging, Streaming }
    public enum SuggestionPriority { Critical, High, Medium, Low, Info }
    public enum PredictionType { Temperature, RamShortage, FpsDrop, BootSlowdown, DiskFull, BatteryDrain, NetworkSpike, ThermalThrottle }
    public enum HealthDimension { Performance, Stability, Gaming, Battery, Security, Overall }
    public enum AgentRole { Memory, CPU, GPU, Disk, Network, Thermal, Battery, Security }
    public enum UsageSeason { Unknown, MorningStudy, AfternoonWork, EveningGaming, WeekendHeavy, NightIdle }
    public enum ConflictType { DirectConflict, ResourceConflict, GoalConflict, PriorityConflict }
    public enum ProcessCategory { Game, Productivity, WindowsSystem, BackgroundService }

    public class SystemState
    {
        public double CpuUsage;
        public double RamUsage;
        public double GpuUsage;
        public double DiskUsage;
        public double CpuTemp;
        public double GpuTemp;
        public double Fps;
        public double FrameTimeMs;
        public double Fps1Low;
        public double Fps01Low;
        public int ProcessCount;
        public int ThreadCount;
        public long SentBytes;
        public long RecvBytes;
        public string ActiveProcess;
        public string ActiveProcessPath;
        public bool IsGame;
        public bool IsEmulator;
        public ProcessCategory ActiveCategory;
        public bool IsCharging;
        public double BatteryLevel;
        public string PowerPlan;
        public int CpuCoreCount;
        public double CpuFreqGHz;
        public double TotalRamGB;
        public double UsedRamGB;
        public DateTime Timestamp;
        public double FpsStability;
        public double FrameTimeJitter;

        public SystemState Clone()
        {
            return (SystemState)MemberwiseClone();
        }
    }

    public class AgentDecision
    {
        public DecisionCategory Category;
        public string Action;
        public string Description;
        public string Reason;
        public string Source;
        public double Confidence;
        public double RiskScore;
        public double Risk;
        public double Impact;
        public DecisionSafety Safety;
        public Dictionary<string, string> Parameters;
        public DateTime Timestamp;
        public string Mode;
        public bool Executed;
        public bool Success;
        public bool WasAccepted;
        // True for advisory/informational actions (suggestions, health reads)
        // that do not mutate the system. They never count as "successful
        // system actions" and are excluded from the learning loop - a
        // suggestion that merely returned is not an outcome.
        public bool Informational;
        public long ExecutionTimeMs;

        public AgentDecision()
        {
            Parameters = new Dictionary<string, string>();
            Timestamp = DateTime.Now;
            Executed = false;
            Success = false;
            ExecutionTimeMs = 0;
            Risk = 0.5;
            Impact = 0;
        }

        public AgentDecision Clone()
        {
            var c = new AgentDecision
            {
                Category = this.Category,
                Action = this.Action,
                Description = this.Description,
                Reason = this.Reason,
                Source = this.Source,
                Confidence = this.Confidence,
                RiskScore = this.RiskScore,
                Risk = this.Risk,
                Impact = this.Impact,
                Safety = this.Safety,
                Timestamp = this.Timestamp,
                Mode = this.Mode,
                Executed = this.Executed,
                Success = this.Success,
                WasAccepted = this.WasAccepted,
                Informational = this.Informational,
                ExecutionTimeMs = this.ExecutionTimeMs
            };
            if (this.Parameters != null)
            {
                foreach (var kv in this.Parameters) c.Parameters[kv.Key] = kv.Value;
            }
            return c;
        }
    }

    public class AgentExperience
    {
        public string Id;
        public string GameName;
        public DecisionCategory Category;
        public string Action;
        public string StateBefore;
        public string StateAfter;
        public double FpsBefore;
        public double FpsAfter;
        public double StabilityBefore;
        public double StabilityAfter;
        public double TempBefore;
        public double TempAfter;
        public ExperienceResult Result;
        public double Score;
        public int TimesUsed;
        public int TimesSuccessful;
        public DateTime LastUsed;
        public DateTime Created;

        public AgentExperience()
        {
            Id = Guid.NewGuid().ToString("N").Substring(0, 8);
            Created = DateTime.Now;
            LastUsed = DateTime.Now;
        }

        public double SuccessRate()
        {
            return TimesUsed > 0 ? (double)TimesSuccessful / TimesUsed : 0.5;
        }
    }

    public class RLExperience
    {
        public string StateKey;
        public string Action;
        public double Reward;
        public bool Success;
        public DateTime Timestamp;
    }

    // === NEW: Real RL transition (state -> action -> reward -> next state) ===
    public class RLTransition
    {
        public string StateKey;
        public string Action;
        public double Reward;
        public string NextStateKey;
        public DateTime Timestamp;

        public RLTransition() { Timestamp = DateTime.Now; }
    }

    public class AgentProfile
    {
        public string Name;
        public string ProcessName;
        public string ProcessPath;
        public AgentMode PreferredMode;
        public bool AutoMemoryClean;
        public bool AutoProcessPriority;
        public bool AutoPowerPlan;
        public bool AutoBackgroundControl;
        public bool AutoThrottle;
        public int MaxCpuTemp;
        public int MaxGpuTemp;
        public int PreferredFpsCap;
        public DateTime LastUsed;
        public int UseCount;

        public AgentProfile()
        {
            // Conservative-by-default: a fresh profile must NOT auto-clean,
            // auto-switch power plans or auto-control background processes.
            // Each automation class is an explicit opt-in (the central
            // execution gate still governs whether they actually run).
            AutoMemoryClean = false;
            AutoProcessPriority = false;
            AutoPowerPlan = false;
            AutoBackgroundControl = false;
            MaxCpuTemp = 90;
            MaxGpuTemp = 90;
            PreferredFpsCap = 0;
            LastUsed = DateTime.Now;
        }
    }

    public class AgentLog
    {
        public DateTime Timestamp;
        public string Level;
        public string Category;
        public string Message;
        public string Details;

        public AgentLog() { Timestamp = DateTime.Now; }
    }

    public class AgentStats
    {
        public int TotalDecisions;
        public int SuccessfulDecisions;
        public int FailedDecisions;
        public int TotalExperiences;
        public int ActiveProfiles;
        public DateTime StartTime;
        public DateTime LastDecisionTime;

        public AgentStats() { StartTime = DateTime.Now; }
    }

    public class StateJournalEntry
    {
        public string Id;
        public string Action;
        public string Category;
        public string GameName;
        public string WhatChanged;
        public string OriginalValue;
        public string NewValue;
        public DateTime AppliedAt;
        public bool Reverted;
        public DateTime? RevertedAt;
        public int State; // 0=Pending, 1=Applied, 2=Verified, 3=Completed

        public StateJournalEntry()
        {
            Id = Guid.NewGuid().ToString("N").Substring(0, 8);
            AppliedAt = DateTime.Now;
            Reverted = false;
            State = 0; // Pending
        }
    }

    public class PersistentRule
    {
        public string ProcessName;
        public string Action;
        public string Value;
        public bool Enabled;
        public DateTime Created;

        public PersistentRule()
        {
            Enabled = true;
            Created = DateTime.Now;
        }
    }

    // ===================== MULTI-DIMENSION HEALTH SCORES =====================
    public class HealthScores
    {
        public double Performance;    // CPU/RAM/Disk efficiency
        public double Stability;      // FPS consistency, crash-free, low jitter
        public double Gaming;         // FPS, frame time, GPU utilization
        public double Battery;        // Battery health, drain rate, efficiency
        public double Security;       // Process safety, service integrity
        public double Overall;        // Weighted composite
        public DateTime ComputedAt;

        public HealthScores() { ComputedAt = DateTime.Now; }

        public double GetScore(HealthDimension dim)
        {
            switch (dim)
            {
                case HealthDimension.Performance: return Performance;
                case HealthDimension.Stability: return Stability;
                case HealthDimension.Gaming: return Gaming;
                case HealthDimension.Battery: return Battery;
                case HealthDimension.Security: return Security;
                default: return Overall;
            }
        }

        public string GetGrade(double score)
        {
            if (score >= 90) return "Excellent";
            if (score >= 75) return "Good";
            if (score >= 60) return "Fair";
            if (score >= 40) return "Poor";
            return "Critical";
        }

        public string GetGradeForDimension(HealthDimension dim)
        {
            return GetGrade(GetScore(dim));
        }
    }

    // ===================== SMART SUGGESTIONS =====================
    public class SmartSuggestion
    {
        public string Id;
        public string Title;
        public string Description;
        public string DetailedExplanation;
        public SuggestionPriority Priority;
        public string Category;
        public double EstimatedImpact;   // 0-100 expected improvement
        public double Confidence;        // 0-1 how sure we are
        public bool AutoActionable;      // Can be executed automatically
        public bool Executed;
        public DateTime Created;
        public DateTime? DismissedAt;
        public string DismissReason;

        public SmartSuggestion()
        {
            Id = Guid.NewGuid().ToString("N").Substring(0, 8);
            Created = DateTime.Now;
            Executed = false;
            AutoActionable = false;
        }
    }

    // ===================== PREDICTIVE ANALYTICS =====================
    public class PredictionResult
    {
        public PredictionType Type;
        public string MetricName;
        public double CurrentValue;
        public double PredictedValue;
        public double Confidence;         // 0-1
        public string Timeframe;          // e.g., "in 5 minutes", "within 1 hour"
        public DateTime PredictedAt;
        public string Reason;
        public bool ActionRequired;

        public PredictionResult()
        {
            PredictedAt = DateTime.Now;
            ActionRequired = false;
        }
    }

    // ===================== WORKLOAD DETECTION =====================
    public class WorkloadInfo
    {
        public WorkloadType DetectedWorkload;
        public double Confidence;          // 0-1
        public WorkloadType PreviousWorkload;
        public DateTime WorkloadStarted;
        public TimeSpan Duration;
        public string ActiveApp;
        public string ActiveProcess;
        public int ProcessCount;
        public double CpuIntensity;        // 0-1
        public double GpuIntensity;        // 0-1
        public double MemoryIntensity;     // 0-1
        public double DiskIntensity;       // 0-1
        public bool IsTransitioning;

        public WorkloadInfo()
        {
            DetectedWorkload = WorkloadType.Desktop;
            PreviousWorkload = WorkloadType.Desktop;
            WorkloadStarted = DateTime.Now;
            Confidence = 0.5;
        }
    }

    // ===================== HARDWARE THERMAL PROFILE (form-factor aware) =====================
    // Cooling capacity is a property of the chassis, not of the storage media.
    // SSDs run hotter than HDDs, but that has no causal effect on CPU/GPU
    // thermal limits - so thresholds are derived from cooling class only.
    public enum CoolingClass { Standard, HighAirflow, Laptop }

    public class HardwareThermalProfile
    {
        public CoolingClass Cooling;
        public double CpuWarningTemp;
        public double CpuThrottleTemp;
        public double GpuWarningTemp;   // GPUs tolerate a few degrees more
        public double GpuThrottleTemp;

        public HardwareThermalProfile()
        {
            CpuWarningTemp = 82;
            CpuThrottleTemp = 92;
            GpuWarningTemp = 85;
            GpuThrottleTemp = 95;
        }
    }

    // ===================== DEVICE PROFILE (hardware-aware) =====================
    public class DeviceProfile
    {
        public string DeviceId;
        public string CpuName;
        public int CpuCoreCount;
        public int CpuLogicalProcessors;
        public double CpuMaxFreqGHz;
        public double TotalRamGB;
        public string GpuName;
        public double GpuVRAMBs;
        public string DiskType;           // "SSD", "HDD", "NVMe"
        public double DiskTotalGB;
        public bool IsLaptop;
        public int PerformanceTier;       // 1=Low, 2=Medium, 3=High, 4=Enthusiast
        public DateTime ProfiledAt;
        public DateTime LastUpdated;

        // Adapted parameters based on hardware
        public double RamCleanThreshold;  // % RAM usage to trigger clean
        public double ThermalWarningTemp; // Temperature to start worrying
        public double ThermalThrottleTemp; // Temperature to take emergency action
        public int MaxBackgroundProcesses; // Max processes before cleanup
        public double AggressiveCleanRamThreshold; // When to do aggressive clean
        // Configurable overrides (null = use auto-detected defaults)
        public double? OverrideRamCleanThreshold;
        public double? OverrideThermalWarningTemp;
        public double? OverrideThermalThrottleTemp;
        public int? OverrideMaxBackgroundProcesses;
        public double? OverrideAggressiveCleanRamThreshold;

        // Form-factor-based thermal profile (independent of DiskType)
        public HardwareThermalProfile Thermal;

        public DeviceProfile()
        {
            DeviceId = Guid.NewGuid().ToString("N").Substring(0, 8);
            Thermal = new HardwareThermalProfile();
            ProfiledAt = DateTime.Now;
            LastUpdated = DateTime.Now;
        }

        public void ComputeAdaptedParameters()
        {
            // Auto-detect base thresholds from hardware
            double baseRamClean, baseAggressiveClean;
            int baseMaxBg;
            double baseThermalWarn, baseThermalThrottle;

            if (TotalRamGB <= 4)
            {
                baseRamClean = 65;
                baseAggressiveClean = 75;
                baseMaxBg = 80;
            }
            else if (TotalRamGB <= 8)
            {
                baseRamClean = 72;
                baseAggressiveClean = 82;
                baseMaxBg = 120;
            }
            else if (TotalRamGB <= 16)
            {
                baseRamClean = 80;
                baseAggressiveClean = 88;
                baseMaxBg = 180;
            }
            else
            {
                baseRamClean = 85;
                baseAggressiveClean = 92;
                baseMaxBg = 250;
            }

            // Performance tier (computed before thermal: cooling class depends on it)
            if (CpuCoreCount >= 8 && TotalRamGB >= 16 && (GpuVRAMBs >= 4e9))
                PerformanceTier = 4;
            else if (CpuCoreCount >= 6 && TotalRamGB >= 8)
                PerformanceTier = 3;
            else if (CpuCoreCount >= 4 && TotalRamGB >= 4)
                PerformanceTier = 2;
            else
                PerformanceTier = 1;

            // Hardware thermal profile: cooling class from FORM FACTOR + tier,
            // NOT from DiskType (storage media has no causal effect on CPU/GPU
            // thermal limits).
            CoolingClass cooling = IsLaptop
                ? CoolingClass.Laptop
                : (PerformanceTier >= 4 ? CoolingClass.HighAirflow : CoolingClass.Standard);
            switch (cooling)
            {
                case CoolingClass.Laptop:
                    baseThermalWarn = 77;
                    baseThermalThrottle = 87;
                    break;
                case CoolingClass.HighAirflow:
                    baseThermalWarn = 85;
                    baseThermalThrottle = 95;
                    break;
                default:
                    baseThermalWarn = 82;
                    baseThermalThrottle = 92;
                    break;
            }
            Thermal = new HardwareThermalProfile
            {
                Cooling = cooling,
                CpuWarningTemp = baseThermalWarn,
                CpuThrottleTemp = baseThermalThrottle,
                GpuWarningTemp = baseThermalWarn + 3,
                GpuThrottleTemp = baseThermalThrottle + 3
            };

            // Apply user overrides if set, otherwise use auto-detected
            RamCleanThreshold = OverrideRamCleanThreshold ?? baseRamClean;
            AggressiveCleanRamThreshold = OverrideAggressiveCleanRamThreshold ?? baseAggressiveClean;
            MaxBackgroundProcesses = OverrideMaxBackgroundProcesses ?? baseMaxBg;
            ThermalWarningTemp = OverrideThermalWarningTemp ?? baseThermalWarn;
            ThermalThrottleTemp = OverrideThermalThrottleTemp ?? baseThermalThrottle;
        }
    }

    // ===================== PROCESS IMPACT SCORE =====================
    public class ProcessImpact
    {
        public string ProcessName;
        public string ProcessPath;
        public int Pid;
        public double CpuUsage;       // avg CPU over sampling window
        public double RamUsageMB;     // current RAM in MB
        public double GpuUsage;       // GPU utilization if available
        public int ThreadCount;
        public double ImpactScore;    // 0-100 composite impact
        public string ImpactCategory; // "High", "Medium", "Low", "Negligible"
        public bool IsSystemProcess;
        public bool IsUserProtected;
        public DateTime FirstSeen;
        public DateTime LastSeen;

        // === NEW: Extended fields for World Model learning ===
        public double AvgCpu;
        public double AvgRamMB;
        public double PeakCpu;
        public double PeakRamMB;
        public double CpuVolatility;
        public string RiskLevel;
        public string Recommendation;

        public ProcessImpact()
        {
            FirstSeen = DateTime.Now;
            LastSeen = DateTime.Now;
        }
    }

    // ===================== PROCESS PROFILE (World Model Learning) =====================
    public class ProcessProfile
    {
        public string Name;
        public double[] CpuHistory;
        public double[] RamHistory;
        public int HistoryIndex;
        public int SampleCount;
        public DateTime FirstSeen;
        public DateTime LastSeen;
        public double AvgCpu;
        public double AvgRamMB;
        public double PeakCpu;
        public double PeakRamMB;
        public double TotalCpuSamples;
        public double TotalRamSamples;
        public long TotalSamples;
    }

    // ===================== SYSTEM TREND (for predictions) =====================
    public class SystemTrend
    {
        public string MetricName;
        public List<double> History;
        public double Slope;           // rate of change per second
        public double Intercept;
        public double RSquared;        // confidence in the trend
        public double CurrentStdDev;
        public double CurrentMean;
        public bool IsAnomaly;
        public double AnomalyZScore;
        public DateTime LastUpdated;

        public SystemTrend()
        {
            History = new List<double>();
            LastUpdated = DateTime.Now;
        }

        public void AddSample(double value, int maxSamples)
        {
            History.Add(value);
            if (History.Count > maxSamples) History.RemoveAt(0);
        }

        public void ComputeLinearRegression()
        {
            int n = History.Count;
            if (n < 3) { Slope = 0; Intercept = History.Count > 0 ? History[n - 1] : 0; RSquared = 0; return; }

            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0, sumY2 = 0;
            for (int i = 0; i < n; i++)
            {
                sumX += i; sumY += History[i];
                sumXY += i * History[i]; sumX2 += i * i; sumY2 += History[i] * History[i];
            }
            double denom = n * sumX2 - sumX * sumX;
            if (Math.Abs(denom) < 1e-10) { Slope = 0; Intercept = sumY / n; RSquared = 0; return; }

            Slope = (n * sumXY - sumX * sumY) / denom;
            Intercept = (sumY - Slope * sumX) / n;

            CurrentMean = sumY / n;
            double ssRes = 0, ssTot = 0;
            for (int i = 0; i < n; i++)
            {
                double predicted = Slope * i + Intercept;
                ssRes += (History[i] - predicted) * (History[i] - predicted);
                ssTot += (History[i] - CurrentMean) * (History[i] - CurrentMean);
            }
            RSquared = ssTot > 0 ? Math.Max(0, 1 - ssRes / ssTot) : 0;

            double variance = n > 1 ? ssTot / (n - 1) : 0;
            CurrentStdDev = Math.Sqrt(Math.Abs(variance));
        }

        public void DetectAnomaly(double value, double threshold)
        {
            if (CurrentStdDev < 0.01) { IsAnomaly = false; AnomalyZScore = 0; return; }
            AnomalyZScore = (value - CurrentMean) / CurrentStdDev;
            IsAnomaly = Math.Abs(AnomalyZScore) > threshold;
        }

        public double PredictNext(int stepsAhead)
        {
            return Slope * (History.Count + stepsAhead - 1) + Intercept;
        }
    }

    // ===================== DECISION EXPLANATION =====================
    public class LegacyDecisionExplanation
    {
        public string DecisionId;
        public string ActionName;
        public string Category;
        public string WhatWillHappen;     // e.g., "Clean 1.2GB standby memory"
        public string WhyNeeded;          // e.g., "RAM usage is 92%, causing slowdown"
        public string ExpectedBenefit;    // e.g., "Free up ~1.2GB, reduce RAM to ~78%"
        public double ExpectedImprovement;// 0-100
        public string RiskAssessment;     // e.g., "Low risk - only cleaning standby memory"
        public double Confidence;         // 0-1
        public string[] AlternativesConsidered; // Other options the AI evaluated
        public string ChosenReason;       // Why this option was chosen over alternatives
        public DateTime Timestamp;

        public LegacyDecisionExplanation()
        {
            DecisionId = Guid.NewGuid().ToString("N").Substring(0, 8);
            Timestamp = DateTime.Now;
            AlternativesConsidered = new string[0];
        }
    }

    // ===================== SELF-TUNING WEIGHTS =====================
    public class TuningWeights
    {
        public double ThermalWeight;
        public double MemoryWeight;
        public double CpuWeight;
        public double FpsWeight;
        public double BatteryWeight;
        public double BackgroundWeight;
        public double PowerWeight;
        public double AffinityWeight;
        public DateTime LastTuned;
        public int TuningCycles;

        public TuningWeights()
        {
            ThermalWeight = 1.0;
            MemoryWeight = 1.0;
            CpuWeight = 1.0;
            FpsWeight = 1.0;
            BatteryWeight = 1.0;
            BackgroundWeight = 1.0;
            PowerWeight = 1.0;
            AffinityWeight = 1.0;
            LastTuned = DateTime.Now;
            TuningCycles = 0;
        }

        public double GetWeight(DecisionCategory cat)
        {
            switch (cat)
            {
                case DecisionCategory.ThermalProtect: return ThermalWeight;
                case DecisionCategory.MemoryClean:
                case DecisionCategory.PurgeStandby: return MemoryWeight;
                case DecisionCategory.ProcessPriority:
                case DecisionCategory.GameBoost: return CpuWeight;
                case DecisionCategory.BackgroundControl: return BackgroundWeight;
                case DecisionCategory.PowerPlan: return PowerWeight;
                case DecisionCategory.CpuAffinity:
                case DecisionCategory.IoPriority: return AffinityWeight;
                case DecisionCategory.CoreParking: return PowerWeight;
                default: return 1.0;
            }
        }

        public void AdjustWeight(DecisionCategory cat, double factor)
        {
            double delta = (factor - 1.0) * 0.05; // Slow adjustment
            switch (cat)
            {
                case DecisionCategory.ThermalProtect:
                    ThermalWeight = Clamp(ThermalWeight + delta, 0.3, 3.0); break;
                case DecisionCategory.MemoryClean:
                case DecisionCategory.PurgeStandby:
                    MemoryWeight = Clamp(MemoryWeight + delta, 0.3, 3.0); break;
                case DecisionCategory.ProcessPriority:
                case DecisionCategory.GameBoost:
                    CpuWeight = Clamp(CpuWeight + delta, 0.3, 3.0); break;
                case DecisionCategory.BackgroundControl:
                    BackgroundWeight = Clamp(BackgroundWeight + delta, 0.3, 3.0); break;
                case DecisionCategory.PowerPlan:
                    PowerWeight = Clamp(PowerWeight + delta, 0.3, 3.0); break;
                case DecisionCategory.CpuAffinity:
                case DecisionCategory.IoPriority:
                    AffinityWeight = Clamp(AffinityWeight + delta, 0.3, 3.0); break;
            }
            TuningCycles++;
            LastTuned = DateTime.Now;
        }

        private static double Clamp(double v, double min, double max)
        {
            return v < min ? min : v > max ? max : v;
        }
    }

    // ===================== ELITE AI: DECISION SIMULATOR =====================
    public class SimulationResult
    {
        public string Action;
        public string Description;
        public double ExpectedSuccessRate;
        public double ExpectedPerformanceGain;
        public double PotentialRisks;
        public bool CanRollback;
        public double SimulationScore;
        public DateTime Timestamp;
        public Dictionary<string, double> SimulatedEffects;
        public SimulationResult() { SimulatedEffects = new Dictionary<string, double>(); }
    }

    // ===================== ELITE AI: PERFORMANCE DNA =====================
    public class PerformanceDNAProfile
    {
        public DateTime LastUpdated;
        public int TotalSamples;
        public double[] HourlyScore;
        public int PeakThermalHour;
        public double AvgPeakTemp;
        public List<string> TopApps;
        public List<string> TopGames;
        public int MorningUsage;
        public int AfternoonUsage;
        public int EveningUsage;
        public int NightUsage;
        public string DeviceTier;
        public PerformanceDNAProfile() { TopApps = new List<string>(); TopGames = new List<string>(); DeviceTier = "Mid"; }
    }

    public class DNASample
    {
        public DateTime Timestamp;
        public int Hour;
        public int DayOfWeek;
        public double CpuUsage;
        public double RamUsage;
        public double Temp;
        public double GpuUsage;
        public double Fps;
        public bool IsGame;
        public string ActiveProcess;
        public int BatteryPercent;
        public bool IsOnBattery;
    }

    // ===================== ELITE AI: SEASONAL LEARNING =====================
    public class SeasonalProfile
    {
        public UsageSeason Season;
        public DateTime DetectedAt;
        public int Hour;
        public int DayOfWeek;
        public bool IsGame;
        public double CpuLoad;
        public SeasonalStrategy Strategy;
    }

    public class SeasonalStrategy
    {
        public UsageSeason Season;
        public string Name;
        public string Description;
        public double Aggressiveness;
        public bool PreferBatterySaver;
        public bool PreferQuiet;
        public int MaxActionsPerHour;
        public List<string> PriorityCategories;
        public SeasonalStrategy() { PriorityCategories = new List<string>(); }
    }

    // ===================== ELITE AI: MULTI-AGENT =====================
    public class SpecializedAgent
    {
        public string Name;
        public AgentRole Role;
        public double Weight;
        public string Description;
    }

    public class AgentVote
    {
        public string AgentName;
        public AgentRole Role;
        public DateTime Timestamp;
        public string Action;
        public int Priority;
        public double Confidence;
        public string Reason;
        public double Weight;
    }

    public class AgentConsensus
    {
        public DateTime Timestamp;
        public int TotalVotes;
        public string RecommendedAction;
        public int VotesForAction;
        public double ConsensusStrength;
        public double TotalScore;
        public List<string> DissentingAgents;
        public AgentConsensus() { DissentingAgents = new List<string>(); }
    }

    public class AgentPerformance
    {
        public string AgentName;
        public int TotalVotes;
        public int AcceptedVotes;
    }

    // ===================== ELITE AI: CONFLICT RESOLVER =====================
    public class ConflictRecord
    {
        public DateTime Timestamp;
        public AgentConflict Conflict;
        public string Resolution;
        public bool WasResolved;
    }

    public class AgentConflict
    {
        public ConflictType Type;
        public string Action1;
        public string Action2;
        public string Agent1;
        public string Agent2;
        public double Severity;
    }

    // ===================== ELITE AI: RISK ENGINE =====================
    public class RiskAssessment
    {
        public string Action;
        public DateTime Timestamp;
        public List<RiskFactor> Factors;
        public double RiskScore;
        public bool ShouldBlock;
        public bool ShouldWarn;
        public bool RequiresApproval;
        public List<string> Mitigations;
        public RiskAssessment() { Factors = new List<RiskFactor>(); Mitigations = new List<string>(); }
    }

    public class RiskFactor
    {
        public string Name;
        public double Score;
        public double Weight;
    }

    // ===================== ELITE AI: FORECAST =====================
    public class PerformanceForecast
    {
        public DateTime Timestamp;
        public List<ForecastPrediction> Predictions;
        public string OverallTrend;
        public string NextActionRecommendation;
        public PerformanceForecast() { Predictions = new List<ForecastPrediction>(); }
    }

    public class ForecastPrediction
    {
        public string Metric;
        public string Trend;
        public double CurrentValue;
        public double PredictedValue1h;
        public double PredictedValue6h;
        public double PredictedValue24h;
        public string Severity;
        public string Description;
    }

    // ===================== ELITE AI: SMART GOALS =====================
    public class SmartGoal
    {
        public string Id;
        public string Name;
        public string Description;
        public string Icon;
        public Dictionary<string, double> TargetMetrics;
        public double Aggressiveness;
        public List<string> PriorityCategories;
        public SmartGoal() { TargetMetrics = new Dictionary<string, double>(); PriorityCategories = new List<string>(); }
    }

    public class GoalProgress
    {
        public string GoalName;
        public DateTime Timestamp;
        public Dictionary<string, double> Metrics;
        public double OverallProgress;
        public GoalProgress() { Metrics = new Dictionary<string, double>(); }
    }

    // ===================== ELITE AI: ADAPTIVE BOOSTERS =====================
    public class AdaptiveBooster
    {
        public string Name;
        public string Description;
        public string Category;
        public double BaseStrength;
        public double CurrentStrength;
        public double MinStrength;
        public double MaxStrength;
        public double AdaptRate;
        public double TriggerThreshold;
        public List<string> Actions;
        public DateTime LastAdjustment;
        public int AdjustmentCount;
        public int TotalActivations;
        public AdaptiveBooster() { Actions = new List<string>(); LastAdjustment = DateTime.MinValue; }
    }

    public class BoosterAdjustment
    {
        public string BoosterName;
        public double OldStrength;
        public double NewStrength;
        public string Reason;
        public DateTime Timestamp;
    }

    // ===================== ELITE AI: KNOWLEDGE BASE =====================
    public class KnowledgeEntry
    {
        public DateTime Timestamp;
        public string Action;
        public string Category;
        public bool Success;
        public double BeforeScore;
        public double AfterScore;
        public double ScoreDelta;
        public string SystemContext;
        public double Confidence;
    }

    public class KnowledgePattern
    {
        public string Action;
        public string Category;
        public int TotalTrials;
        public int Successes;
        public double AvgScoreDelta;
        public string BestContext;
        public string WorstContext;
        public double Confidence;
        public DateTime LastUpdated;
    }

    // ===================== LEVEL 4: CAUSAL AI =====================
    public class CausalChain
    {
        public DateTime Timestamp;
        public string Symptom;
        public double Severity;
        public string Category;
        public List<CausalNode> Nodes;
        public double Confidence;
        public string RecommendedAction;
    }

    public class CausalNode
    {
        public string Label;
        public string Value;
        public string Type;
        public List<CausalNode> Children;
    }

    // ===================== CAUSAL RULE (Learned) =====================
    public class CausalRule
    {
        public string Id;
        public string Cause;
        public string Effect;
        public string Category;
        public string ConditionType;
        public double Threshold;
        public double SecondThreshold;
        public double CurrentSeverity;
        public double BaseConfidence;
        public string RecommendedAction;
        public bool IsEnabled;
        public DateTime DiscoveredAt;
        public int ObservationCount;
    }

    // ===================== CAUSAL OBSERVATION =====================
    public class CausalObservation
    {
        public DateTime Timestamp;
        public double Cpu;
        public double Ram;
        public double Temp;
        public double Fps;
        public int ProcessCount;
        public double DiskUsage;
    }

    // ===================== LEVEL 4: WORLD MODEL =====================
    public class WorldModel
    {
        public DateTime LastUpdated;
        public List<WorldNode> Nodes;
        public List<WorldEdge> Edges;
        public Dictionary<string, WorldNode> ServiceMap;
    }

    public class WorldNode
    {
        public string Name;
        public string Type;
        public string Description;
        public double CurrentLoad;
        public double Health;
        public DateTime LastSeen;
    }

    public class WorldEdge
    {
        public string From;
        public string To;
        public string Relationship;
        public double Weight;
        public double Impact;
    }

    // ===================== LEVEL 4: REINFORCEMENT LEARNING =====================
    public class RLState
    {
        public int CpuBin;
        public int RamBin;
        public int TempBin;
        public int FpsBin;
        public int GpuBin;
        public int DiskBin;
        public int HourBin;
        public string Mode;
        public DateTime Timestamp;
    }

    public class RLActionValue
    {
        public string Action;
        public double QValue;
        public int VisitCount;
        public double AvgReward;
        public DateTime LastUpdated;
    }

    public class RLAction
    {
        public string Action;
        public string State;
        public double Reward;
        public bool Success;
        public bool IsExploration;
        public DateTime Timestamp;
    }

    public class RLEpisode
    {
        public string State;
        public string Action;
        public double Reward;
        public DateTime Timestamp;
    }

    // ===================== LEVEL 4: AI PLANNING =====================
    public class AIPlan
    {
        public string Name;
        public string Goal;
        public int Priority;
        public List<PlanStep> Steps;
        public string EstimatedDuration;
        public double Confidence;
        public string Status;
        public int CurrentStep;
        public DateTime CompletedAt;
        public string SuccessCondition;
        public string RollbackAction;
    }

    public class PlanStep
    {
        public int Order;
        public string Action;
        public string Description;
        public string EstimatedTime;
    }

    // ===================== LEVEL 4: LONG-TERM MEMORY =====================
    public class LongTermEntry
    {
        public DateTime Timestamp;
        public string Category;
        public string Description;
        public double Impact;
        public string SystemState;
    }

    public class GameOptHistory
    {
        public string GameName;
        public List<GameOptAction> Optimizations;
        public int TotalOptimizations;
        public int SuccessfulOptimizations;
        public DateTime LastPlayed;
        public double AvgFpsBefore;
        public double AvgFpsAfter;
        public string BestAction;
    }

    public class GameOptAction
    {
        public string Action;
        public bool Success;
        public double FpsBefore;
        public double FpsAfter;
        public DateTime Timestamp;
        public double FpsDelta;
    }

    public class DriverPerfRecord
    {
        public string DriverName;
        public string Category;
        public double Version;
        public double PerformanceImpact;
        public DateTime Timestamp;
    }

    public class UpdateImpactRecord
    {
        public string UpdateName;
        public double PerformanceBefore;
        public double PerformanceAfter;
        public double Impact;
        public DateTime Timestamp;
    }

    // ===================== LEVEL 4: ADAPTIVE ARCHITECTURE =====================
    public class AnalyzerFitness
    {
        public string Name;
        public double Weight;
        public int TotalRuns;
        public int UsefulRuns;
        public double AvgImpact;
        public double SuccessRate;
        public bool Enabled;
        public DateTime LastRun;
    }

    public class ArchitectureChange
    {
        public DateTime Timestamp;
        public string Analyzer;
        public string Reason;
        public double OldValue;
        public double NewValue;
    }

    // ===================== LEVEL 4: SELF PROFILING =====================
    public class SelfProfile
    {
        public DateTime StartTime;
        public DateTime LastUpdated;
        public long MemoryUsageMB;
        public int ThreadCount;
        public int HandleCount;
        public double AvgTickTimeMs;
        public long TotalTickTimeMs;
        public int TotalTicks;
        public double CpuOverheadPercent;
        public string SlowestAnalyzer;
        public string FastestAnalyzer;
        public int TotalAnalyzerRuns;
    }

    public class AnalyzerProfile
    {
        public string Name;
        public int TotalRuns;
        public long TotalTimeMs;
        public double AvgTimeMs;
        public long MaxTimeMs;
        public long MinTimeMs;
        public DateTime LastRun;
    }

    // ===================== LEVEL 4: CONFIDENCE CALIBRATION =====================
    public class CalibrationBucket
    {
        public string Action;
        public int BucketIndex;
        public double PredictedConfidence;
        public double ActualSuccessRate;
        public int TotalSamples;
        public int SuccessCount;
        public double CalibrationError;
        public DateTime LastUpdated;

        public CalibrationBucket()
        {
            LastUpdated = DateTime.Now;
        }
    }

    public class CalibrationRecord
    {
        public DateTime Timestamp;
        public string Action;
        public double PredictedConfidence;
        public bool ActualSuccess;
        public double BucketError;
    }

    // ===================== LEVEL 5: AUTONOMOUS MANAGER =====================
    public class AutonomousState
    {
        public DateTime StartTime;
        public bool IsActive;
        public DateTime LastCycle;
        public int TotalCycles;
        public double SystemHealth;
        public double OverallHealth;
        public int SubsystemCount;
        public int ActiveSubsystems;
        public Dictionary<string, string> SubsystemStates;
    }

    public class AutoAction
    {
        public string Subsystem;
        public string Action;
        public string Reason;
        public int Priority;
        public DateTime Timestamp;
        public string Status;
        public DateTime CompletedAt;
    }

    public class AutoSubsystem
    {
        public string Name;
        public string Description;
        public string Status;
        public DateTime LastAction;
        public int ActionCount;
        public double Health;
    }

    // ===================== LEVEL 6: ENTERPRISE MANAGER =====================
    public class AuditEntry
    {
        public DateTime Timestamp;
        public string Action;
        public string Category;
        public string Detail;
        public bool Success;
        public double Impact;
        public double SessionUptime;
    }

    public class SystemKPIs
    {
        public DateTime LastUpdated;
        public DateTime StartTime;
        public double UptimeHours;
        public double AvgHealthScore;
        public int TotalOptimizations;
        public double SuccessRate;
        public double AvgCpuUsage;
        public double AvgRamUsage;
        public double AvgTemp;
        public double AvgFps;
        public double DegradationRate;
    }

    public class PerformanceReport
    {
        public DateTime Timestamp;
        public string ReportType;
        public double AvgCpu;
        public double AvgRam;
        public double AvgTemp;
        public double AvgFps;
        public double HealthScore;
        public int TotalOptimizations;
        public double SuccessRate;
        public double UptimeHours;
        public List<string> TopActions;
        public List<string> Issues;
    }

    public class DegradationRecord
    {
        public DateTime Timestamp;
        public double HealthScore;
        public double CpuUsage;
        public double RamUsage;
        public double Temp;
        public double Fps;
    }

    // ===================== LEVEL 7: OPTIMIZATION GRAPH =====================
    public class OptGraph
    {
        public List<OptGraphNode> Nodes;
        public List<OptGraphEdge> Edges;
        public Dictionary<string, OptGraphNode> NodeMap;
        public DateTime LastUpdated;
    }

    public class OptGraphNode
    {
        public string Name;
        public string Type;
        public double CurrentLoad;
        public double CostWeight;
        public bool Available;
        public double Health;
    }

    public class OptGraphEdge
    {
        public string From;
        public string To;
        public string Relationship;
        public double Weight;
    }

    public class OptGraphSnapshot
    {
        public DateTime Timestamp;
        public double AvgLoad;
        public int NodeCount;
        public int EdgeCount;
        public int CriticalNodes;
    }

    public class OptimalChangeSet
    {
        public string Name;
        public string TargetNode;
        public double ExpectedBenefit;
        public double Cost;
        public double Risk;
        public double NetScore;
        public DateTime Timestamp;
    }

    // ===================== TOOL TRAINING SYSTEM (Level 8) =====================
    public enum ToolCategory { Registry, Service, Process, Power, Network, Disk, Memory, GPU, CPU, Script, Custom }
    public enum ToolComplexity { Safe, Moderate, Aggressive, Critical }
    public enum ToolOutcome { Unknown, Success, Partial, Failed, Harmful }

    public class ToolDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public string DetailedExplanation;
        public ToolCategory Category;
        public ToolComplexity Complexity;
        public DecisionCategory AiCategory;
        public string Command;           // The actual command/script to execute
        public string RevertCommand;     // How to undo this tool
        public Dictionary<string, string> Parameters; // Dynamic parameters
        public Dictionary<string, double> PreConditions; // CPU>70, RAM>80, etc.
        public Dictionary<string, double> PostEffects;   // Expected changes: CPU-10, RAM-15, etc.
        public double ConfidenceScore;   // 0-1 learned confidence
        public int TotalUses;
        public int SuccessCount;
        public int FailCount;
        public int HarmCount;
        public double AvgImpactScore;    // Average measured improvement
        public DateTime Created;
        public DateTime LastUsed;
        public bool Enabled;
        public string CreatedBy;         // "User", "AI_Discovered", "Imported"
        public string Tags;              // Comma-separated: "gaming,low-ram,thermal"

        public ToolDefinition()
        {
            Id = Guid.NewGuid().ToString("N").Substring(0, 8);
            Parameters = new Dictionary<string, string>();
            PreConditions = new Dictionary<string, double>();
            PostEffects = new Dictionary<string, double>();
            Created = DateTime.Now;
            LastUsed = DateTime.Now;
            Enabled = true;
            CreatedBy = "User";
            ConfidenceScore = 0.5;
            Tags = "";
        }

        public double GetSuccessRate()
        {
            return TotalUses > 0 ? (double)SuccessCount / TotalUses : 0.5;
        }

        public double GetFailureRate()
        {
            return TotalUses > 0 ? (double)FailCount / TotalUses : 0.0;
        }
    }

    public class ToolExecution
    {
        public string Id;
        public string ToolId;
        public string ToolName;
        public DateTime ExecutedAt;
        public double CpuBefore, CpuAfter;
        public double RamBefore, RamAfter;
        public double TempBefore, TempAfter;
        public double FpsBefore, FpsAfter;
        public double HealthBefore, HealthAfter;
        public string ActiveProcess;
        public bool IsGame;
        public double ImpactScore;        // Measured improvement (-100 to +100)
        public ToolOutcome Outcome;
        public string Notes;
        public double ExecutionTimeMs;
        public bool Success;
        public string SystemContextSnapshot; // JSON-like context at execution time

        public ToolExecution()
        {
            Id = Guid.NewGuid().ToString("N").Substring(0, 8);
            ExecutedAt = DateTime.Now;
            Outcome = ToolOutcome.Unknown;
            Success = true;
        }

        public double CalculateImpact()
        {
            double cpuDelta = CpuBefore - CpuAfter;   // positive = good
            double ramDelta = RamBefore - RamAfter;   // positive = good
            double tempDelta = TempBefore - TempAfter; // positive = good
            double fpsDelta = FpsAfter - FpsBefore;    // positive = good
            double healthDelta = HealthAfter - HealthBefore;

            double score = 0;
            score += cpuDelta * 0.25;
            score += ramDelta * 0.25;
            score += tempDelta * 0.15;
            score += fpsDelta * 0.1;
            score += healthDelta * 0.25;
            ImpactScore = Math.Max(-100, Math.Min(100, score));
            return ImpactScore;
        }
    }

    public class ToolFeedback
    {
        public string Id;
        public string ExecutionId;
        public string ToolId;
        public DateTime FeedbackAt;
        public int Rating;               // 1-5 stars
        public string Comment;
        public bool WasHelpful;
        public bool CausedIssues;
        public ToolOutcome FinalOutcome;

        public ToolFeedback()
        {
            Id = Guid.NewGuid().ToString("N").Substring(0, 8);
            FeedbackAt = DateTime.Now;
            Rating = 3;
            WasHelpful = true;
            CausedIssues = false;
        }
    }

    public class ToolLearningPattern
    {
        public string ToolId;
        public string SituationKey;      // e.g., "CPU>70_RAM>80_Game=true"
        public int MatchesInSituation;
        public int SuccessesInSituation;
        public double SituationalConfidence;
        public double BestMeasuredImpact;
        public DateTime LastMatch;
    }

    public class ToolRecommendation
    {
        public string ToolId;
        public string ToolName;
        public double Confidence;
        public double ExpectedImpact;
        public string Reason;
        public string MatchedSituation;
        public DateTime Timestamp;
    }

    public class TrainingSession
    {
        public string Id;
        public DateTime StartedAt;
        public DateTime EndedAt;
        public int ToolsAdded;
        public int ToolsExecuted;
        public int FeedbacksReceived;
        public double AvgImpactBefore;
        public double AvgImpactAfter;
        public string Notes;

        public TrainingSession()
        {
            Id = Guid.NewGuid().ToString("N").Substring(0, 8);
            StartedAt = DateTime.Now;
        }
    }

    // ===================== AGENT CORE ARCHITECTURE =====================
    // OBSERVE → UNDERSTAND → PLAN → RISK_CHECK → ACT → VERIFY → LEARN

    public enum AgentGoal
    {
        None,
        ImprovePerformance,
        ReduceMemoryPressure,
        ReduceBackgroundLoad,
        ImproveGamingPerformance,
        ImproveStartup,
        InvestigateBottleneck,
        RecoverFromFailedOptimization,
        ReduceThermalLoad,
        OptimizePowerUsage
    }

    public enum VerificationResult { Unknown, Improved, Unchanged, Worsened, Failed }

    public enum ActionRiskLevel { Low, Medium, High, Critical }

    public class AgentRecentEvent
    {
        public string Type;
        public string Detail;
        public DateTime When;
    }

    public class AgentRecentAction
    {
        public string Action;
        public bool Success;
        public double ImpactScore;
        public DateTime When;
    }

    public class AgentObservation
    {
        public double CpuUsage, RamUsage, GpuUsage, DiskUsage;
        public double CpuTemp, GpuTemp;
        public double Fps, FpsStability, Fps1Low;
        public int ProcessCount;
        public bool IsCharging;
        public double BatteryLevel;
        public bool IsGame;
        public string ActiveProcess;
        public string PowerPlan;
        public double HealthScore;
        public bool SafeModeActive;
        public bool KillSwitchActive;
        public bool SystemStable;
        public AgentGoal ActiveGoal;
        public List<AgentRecentEvent> RecentEvents = new List<AgentRecentEvent>();
        public List<AgentRecentAction> RecentActions = new List<AgentRecentAction>();
        public DateTime Timestamp;
    }

    public class AgentReasoningResult
    {
        public string Diagnosis;
        public List<string> Observations = new List<string>();
        public List<string> Insights = new List<string>();
        public double Confidence;
        public AgentGoal SuggestedGoal;
        public bool NeedsMoreData;
    }

    public class AgentPlanStep
    {
        public int Order;
        public string ToolName;
        public string Description;
        public string ExpectedOutcome;
        public ActionRiskLevel Risk;
        public bool HasRollback;
        public string RollbackTool;
    }

    public class AgentPlan
    {
        public string PlanId;
        public AgentGoal Goal;
        public List<AgentPlanStep> Steps = new List<AgentPlanStep>();
        public double Confidence;
        public double EstimatedRisk;
        public bool CanRollback;
        public DateTime CreatedAt;
        public string ReasoningSummary;
    }

    public class AgentSafetyResult
    {
        public bool Allowed;
        public ActionRiskLevel RiskLevel;
        public double RiskScore;
        public string Reason;
        public List<string> SafetyChecks = new List<string>();
        public bool RequiresApproval;
        public string BlockReason;
    }

    public class AgentActionResult
    {
        public string ToolName;
        public bool Success;
        public string Message;
        public bool RollbackPerformed;
        public double ExecutionTimeMs;
        public Dictionary<string, string> Metadata = new Dictionary<string, string>();
    }

    public class AgentVerificationResult
    {
        public VerificationResult Result;
        public string Message;
        public double CpuBefore, CpuAfter;
        public double RamBefore, RamAfter;
        public double TempBefore, TempAfter;
        public double FpsBefore, FpsAfter;
        public double HealthBefore, HealthAfter;
        public double ImprovementScore;
        public bool ShouldRollback;
    }

    public class AgentMemoryEntry
    {
        public string Id;
        public DateTime Timestamp;
        public AgentGoal Goal;
        public string Diagnosis;
        public string PlannedAction;
        public ActionRiskLevel Risk;
        public double ExpectedImpact;
        public double ActualImpact;
        public VerificationResult Outcome;
        public bool RollbackTriggered;
        public string Notes;
    }

    public class AgentReasoning
    {
        public string CycleId;
        public DateTime Timestamp;
        public AgentObservation Observation;
        public AgentReasoningResult Reasoning;
        public AgentPlan Plan;
        public AgentSafetyResult Safety;
        public List<AgentActionResult> ExecutedActions = new List<AgentActionResult>();
        public AgentVerificationResult Verification;
        public double TotalDurationMs;
        public bool Completed;
        public string BlockedAt;
    }

    public class AgentTool
    {
        public string Name;
        public string DisplayName;
        public string Description;
        public ToolCategory Category;
        public ActionRiskLevel DefaultRisk;
        public bool NeedsApproval;
        public bool IsReadOnly;
        public bool IsReversible;
        public string RollbackTool;
        public List<string> ValidArgs = new List<string>();
        public int MinCooldownSeconds;
        public int SuccessCount;
        public int FailureCount;
        public DateTime LastExecuted;

        public double SuccessRate => SuccessCount + FailureCount > 0 ? (double)SuccessCount / (SuccessCount + FailureCount) : 0.5;
    }
}
