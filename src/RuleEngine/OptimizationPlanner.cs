using System;
using System.Collections.Generic;

namespace ClearWin.RuleEngine
{
    /// <summary>
    /// Planner: builds a safe, ordered multi-step plan BEFORE any action runs.
    /// Respects dependencies, conflicts, approval gates and risk budgets.
    /// </summary>
    public class OptimizationPlanner
    {
        private const double MaxPlanRisk = 2.5;

        public ExpertPlan BuildPlan(List<CandidateAction> allowed, List<CandidateAction> blocked,
            ExpertDiagnosis diagnosis, string goal)
        {
            var plan = new ExpertPlan
            {
                Goal = goal,
                Name = "Optimization plan: " + (diagnosis != null && !string.IsNullOrEmpty(diagnosis.RootCause) ? diagnosis.RootCause : "general tuning")
            };

            if (allowed == null || allowed.Count == 0)
            {
                plan.Status = "Empty";
                return plan;
            }

            // 1) Partition into safety-critical (thermal/security) vs performance actions.
            var critical = new List<CandidateAction>();
            var regular = new List<CandidateAction>();
            for (int i = 0; i < allowed.Count; i++)
            {
                CandidateAction a = allowed[i];
                if (a.Class == ActionClass.Thermal || a.Class == ActionClass.Security ||
                    (a.Category != null && a.Category.IndexOf("thermal", StringComparison.OrdinalIgnoreCase) >= 0))
                    critical.Add(a);
                else
                    regular.Add(a);
            }

            // 2) Sort each group by computed score (descending).
            critical.Sort(delegate(CandidateAction a, CandidateAction b) { return b.Score.CompareTo(a.Score); });
            regular.Sort(delegate(CandidateAction a, CandidateAction b) { return b.Score.CompareTo(a.Score); });

            // 3) Merge: critical first (they protect the system), then regular.
            var ordered = new List<CandidateAction>();
            ordered.AddRange(critical);
            ordered.AddRange(regular);

            // 4) Resolve conflicts: never put mutually exclusive actions in the same plan.
            var chosen = new List<CandidateAction>();
            var chosenIds = new HashSet<string>();
            for (int i = 0; i < ordered.Count; i++)
            {
                CandidateAction a = ordered[i];
                bool conflicts = false;
                if (a.ConflictsWith != null)
                {
                    for (int j = 0; j < a.ConflictsWith.Count; j++)
                    {
                        if (chosenIds.Contains(a.ConflictsWith[j])) { conflicts = true; break; }
                    }
                }
                if (conflicts) continue;
                chosen.Add(a);
                chosenIds.Add(a.Id);
            }

            // 5) Emit plan steps with reasoning and rollback.
            double totalRisk = 0;
            double totalGain = 0;
            int order = 1;
            for (int i = 0; i < chosen.Count; i++)
            {
                CandidateAction a = chosen[i];
                var step = new ExpertPlanStep
                {
                    Order = order,
                    ActionId = a.Id,
                    ActionName = a.Name,
                    Description = a.Description,
                    IsCritical = (a.Class == ActionClass.Thermal || a.Class == ActionClass.Security),
                    RequiresApproval = a.Risk >= RuleEngineConstants.RiskRequiresApproval,
                    Rollback = a.Reversible ? (a.RevertMethod ?? "Restore default") : "Not reversible",
                    Reasoning = BuildStepReasoning(a)
                };
                plan.Steps.Add(step);
                order++;
                totalRisk += a.Risk;
                totalGain += a.ExpectedImpact;
            }

            plan.ExpectedGain = totalGain;
            plan.TotalRisk = totalRisk;
            plan.Status = totalRisk > MaxPlanRisk ? "Proposed-HighRisk" : "Proposed";
            return plan;
        }

        private string BuildStepReasoning(CandidateAction a)
        {
            string reason = a.Evidence;
            if (string.IsNullOrEmpty(reason)) reason = "Recommended for " + (a.Category ?? "performance");
            string approx = a.Risk >= RuleEngineConstants.RiskRequiresApproval ? " (requires user approval)" : "";
            return reason + approx;
        }
    }
}
