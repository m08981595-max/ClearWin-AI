using System;
using System.Collections.Generic;

namespace ClearWin.RuleEngine
{
    // =====================================================================
    //  EXPERT SYSTEM - SHARED DATA MODELS
    //  Professional Windows-optimization expert system built on:
    //  Knowledge Base + Reasoning + Planning + Decision + Validation +
    //  Safety + Learning + Memory + Diagnostics + Explainability.
    // =====================================================================

    public enum SeverityLevel { Info, Low, Medium, High, Critical }
    public enum ActionClass { Registry, Process, Service, Power, Network, Disk, Memory, Thermal, Startup, Security, System }
    public enum SafetyDecision { Allowed, NeedsApproval, Blocked }
    public enum PlanStepStatus { Pending, Ready, Running, Done, Skipped, Failed, RolledBack }

    /// <summary>A single grounded fact used by the reasoning engine.</summary>
    public class ExpertFact
    {
        public string Variable;
        public double Value;
        public double Confidence;
        public string Source;
        public DateTime Timestamp;

        public ExpertFact()
        {
            Confidence = 1.0;
            Source = "system";
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>Weighted condition:  Variable <op> Value</summary>
    public class RuleCondition
    {
        public string Variable;
        public string Operator;      // > >= < <= == != between
        public double Value;
        public double Value2;        // upper bound for "between"
        public double Weight;

        public RuleCondition()
        {
            Operator = ">";
            Weight = 1.0;
        }

        public bool Satisfied(double actual)
        {
            switch (Operator)
            {
                case ">": return actual > Value;
                case ">=": return actual >= Value;
                case "<": return actual < Value;
                case "<=": return actual <= Value;
                case "==": return Math.Abs(actual - Value) < 0.0001;
                case "!=": return Math.Abs(actual - Value) >= 0.0001;
                case "between": return actual >= Value && actual <= Value2;
                default: return false;
            }
        }

        public override string ToString()
        {
            if (Operator == "between")
                return string.Format("{0} in [{1:F1}, {2:F1}]", Variable, Value, Value2);
            return string.Format("{0} {1} {2:F1}", Variable, Operator, Value);
        }
    }

    /// <summary>Production rule: IF (conditions) THEN conclusion [recommend action].</summary>
    public class ExpertRule
    {
        public string Id;
        public string Name;
        public string Description;
        public string Category;            // Memory / Thermal / CPU / Disk / ...
        public List<RuleCondition> Conditions;
        public string Conclusion;          // "memory.pressure=high"
        public double Confidence;
        public string RecommendedAction;   // optional candidate action id
        public int MatchCount;
        public int SuccessCount;
        public bool Enabled;

        public ExpertRule()
        {
            Conditions = new List<RuleCondition>();
            Confidence = 0.9;
            Enabled = true;
        }

        public double GetEffectiveConfidence()
        {
            if (Conditions.Count == 0) return Confidence;
            double product = Confidence;
            for (int i = 0; i < Conditions.Count; i++)
                product *= Conditions[i].Weight;
            return Math.Max(0.0, Math.Min(1.0, product));
        }
    }

    /// <summary>Learned causal relationship: Cause -> Effect.</summary>
    public class CausalLink
    {
        public string Id;
        public string Cause;          // fact variable
        public string Effect;         // fact variable
        public double Strength;       // 0..1 (base prior strength)
        public double Samples;
        public double AvgLatencySeconds;
        public bool Enabled;
        public string Mechanism;      // human readable explanation of why
        public string Category;

        public CausalLink()
        {
            Strength = 0.5;
            Samples = 0;
            Enabled = true;
        }
    }

    /// <summary>A proposed optimization action with full metadata.</summary>
    public class CandidateAction
    {
        public string Id;
        public string Name;
        public string NameAr;
        public ActionClass Class;
        public string Category;
        public string Description;
        public double ExpectedImpact;    // 0..100
        public double Risk;              // 0..1
        public double Cost;              // 0..10
        public double Confidence;        // 0..1
        public double Urgency;           // 0..1
        public bool Reversible;
        public string RevertMethod;
        public List<string> Prerequisites;    // fact variable names that must be true/known
        public List<string> ConflictsWith;    // candidate action ids
        public List<string> AffectsMetrics;   // metric names affected
        public string TargetProcess;
        public string Evidence;               // why it was proposed
        public double Score;                  // computed by DecisionEngine
        public string WhyItHelps;             // mechanism explanation
        public string WhyItHelpsAr;

        public CandidateAction()
        {
            Prerequisites = new List<string>();
            ConflictsWith = new List<string>();
            AffectsMetrics = new List<string>();
            Confidence = 0.5;
            Reversible = true;
        }
    }

    /// <summary>Multi-step execution plan produced before any action runs.</summary>
    public class ExpertPlan
    {
        public string Id;
        public string Name;
        public string Goal;
        public DateTime CreatedAt;
        public double ExpectedGain;
        public double TotalRisk;
        public string Status;
        public List<ExpertPlanStep> Steps;

        public ExpertPlan()
        {
            Id = Guid.NewGuid().ToString("N").Substring(0, 8);
            CreatedAt = DateTime.Now;
            Steps = new List<ExpertPlanStep>();
            Status = "Proposed";
        }
    }

    public class ExpertPlanStep
    {
        public int Order;
        public string ActionId;
        public string ActionName;
        public string Description;
        public PlanStepStatus Status;
        public bool IsCritical;
        public bool RequiresApproval;
        public string Rollback;
        public string Reasoning;

        public ExpertPlanStep()
        {
            Status = PlanStepStatus.Pending;
        }
    }

    /// <summary>Result of checking an action's correctness before execution.</summary>
    public class ValidationResult
    {
        public string ActionId;
        public bool Valid;
        public List<string> Errors;
        public List<string> Warnings;

        public ValidationResult()
        {
            Valid = true;
            Errors = new List<string>();
            Warnings = new List<string>();
        }
    }

    /// <summary>Result of the safety gate for an action.</summary>
    public class SafetyCheckResult
    {
        public string ActionId;
        public SafetyDecision Decision;
        public List<string> Reasons;
        public double RiskScore;

        public SafetyCheckResult()
        {
            Reasons = new List<string>();
        }
    }

    /// <summary>A ranked hypothesis produced by the diagnostic engine.</summary>
    public class ExpertHypothesis
    {
        public string Name;
        public string Description;
        public double Probability;
        public List<string> SupportingEvidence;
        public string RecommendedAction;

        public ExpertHypothesis()
        {
            SupportingEvidence = new List<string>();
        }
    }

    /// <summary>Deep system diagnosis: symptom -> evidence -> root cause.</summary>
    public class ExpertDiagnosis
    {
        public string Symptom;
        public string Category;
        public SeverityLevel Severity;
        public string RootCause;
        public double Confidence;
        public string RecommendedHint;
        public List<ExpertHypothesis> Hypotheses;
        public List<string> Evidence;
        public DateTime Timestamp;

        public ExpertDiagnosis()
        {
            Confidence = 0.5;
            Severity = SeverityLevel.Info;
            Hypotheses = new List<ExpertHypothesis>();
            Evidence = new List<string>();
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>Multi-language explanation for a decision.</summary>
    public class ExpertExplanation
    {
        public string ActionId;
        public string Title;
        public string TitleAr;
        public string WhyNeeded;
        public string WhyNeededAr;
        public string WhatWillHappen;
        public string WhatWillHappenAr;
        public string ExpectedBenefit;
        public string ExpectedBenefitAr;
        public string RiskAssessment;
        public string RiskAssessmentAr;
        public double Confidence;
        public List<string> AlternativesConsidered;
        public string ChosenReason;
        public string ChosenReasonAr;
        public List<string> ReasoningChain;
        public List<string> Evidence;
        public DateTime Timestamp;

        public ExpertExplanation()
        {
            AlternativesConsidered = new List<string>();
            ReasoningChain = new List<string>();
            Evidence = new List<string>();
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>A notable event in the current session.</summary>
    public class SessionEvent
    {
        public DateTime Timestamp;
        public string Type;
        public string Message;
        public double Value;

        public SessionEvent()
        {
            Timestamp = DateTime.Now;
        }
    }

    /// <summary>Snapshot of the physical device.</summary>
    public class DeviceSnapshot
    {
        public string OsVersion;
        public string CpuName;
        public int CpuCores;
        public int CpuLogical;
        public double TotalRamGB;
        public double FreeRamGB;
        public string GpuName;
        public string DiskType;
        public double DiskTotalGB;
        public double DiskFreeGB;
        public bool IsLaptop;
        public bool IsBatteryPowered;
        public double BatteryLevel;
        public int StartupItems;
        public DateTime CapturedAt;
        // Windows services health (collected via WMI, best effort).
        // ServicesScanned stays false when the scan failed so engines never
        // misread "0 services" as a healthy system.
        public bool ServicesScanned;
        public int StoppedServices;
        public int DisabledServices;
        public int AutoStoppedServices;
        public List<string> ProblemServiceNames;

        public DeviceSnapshot()
        {
            CapturedAt = DateTime.Now;
            DiskType = "Unknown";
            ProblemServiceNames = new List<string>();
        }
    }

    /// <summary>Learned statistics for a specific action.</summary>
    public class ActionStatistics
    {
        public string ActionId;
        public string Category;
        public int TotalUses;
        public int Successes;
        public int Failures;
        public double AvgImpact;
        public double AvgRisk;
        public double SuccessRate;
        public double Confidence;
        public DateTime LastUsed;
        public double LastScoreDelta;

        public ActionStatistics()
        {
            SuccessRate = 0.5;
            Confidence = 0.5;
            LastUsed = DateTime.MinValue;
        }
    }

    /// <summary>Complete output of one analysis cycle.</summary>
    public class RuleEngineResult
    {
        public DateTime Timestamp;
        public string Mode;
        public SystemState State;
        public DeviceSnapshot Device;
        public ExpertDiagnosis Diagnosis;
        public List<CandidateAction> Candidates;
        public List<CandidateAction> Recommended;
        public List<CandidateAction> Blocked;
        public ExpertPlan Plan;
        public List<ExpertExplanation> Explanations;
        public List<ExpertFact> Facts;
        public int ValidatedCount;
        public int BlockedCount;

        public RuleEngineResult()
        {
            Timestamp = DateTime.Now;
            Candidates = new List<CandidateAction>();
            Recommended = new List<CandidateAction>();
            Blocked = new List<CandidateAction>();
            Explanations = new List<ExpertExplanation>();
            Facts = new List<ExpertFact>();
        }
    }

    /// <summary>Per-item recommendation with explanation and safety gate.</summary>
    public class ExpertRecommendation
    {
        public ExpertDiagnosis Diagnosis;
        public CandidateAction Action;
        public ExpertExplanation Explanation;
        public ValidationResult Validation;
        public SafetyCheckResult Safety;
    }
}
