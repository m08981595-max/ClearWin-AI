using System;
using System.Collections.Generic;
using System.Linq;

namespace ClearWin
{
    public partial class SmartAgent
    {
        // === Conflict Resolver Fields ===
        private List<ConflictRecord> _conflictHistory = new List<ConflictRecord>();
        private DateTime _lastConflictCheck = DateTime.MinValue;
        private const int CONFLICT_CHECK_INTERVAL_SECONDS = 10;
        private int _totalConflicts = 0;
        private int _resolvedConflicts = 0;

        // === Public Accessors ===
        public List<ConflictRecord> GetConflictHistory() { return new List<ConflictRecord>(_conflictHistory); }
        public int GetTotalConflicts() { return _totalConflicts; }
        public int GetResolvedConflicts() { return _resolvedConflicts; }

        // === Detect and Resolve Conflicts ===
        public List<AgentDecision> ResolveConflicts(List<AgentDecision> decisions)
        {
            if (decisions == null || decisions.Count <= 1) return decisions;

            var conflicts = DetectConflicts(decisions);
            if (conflicts.Count == 0) return decisions;

            var resolved = new List<AgentDecision>(decisions);
            foreach (var conflict in conflicts)
            {
                var resolution = ResolveSingleConflict(conflict, resolved);
                if (resolution != null)
                {
                    resolved = resolution;
                    _resolvedConflicts++;
                }
            }

            return resolved;
        }

        // === Detect Conflicts Between Decisions ===
        private List<AgentConflict> DetectConflicts(List<AgentDecision> decisions)
        {
            var conflicts = new List<AgentConflict>();

            for (int i = 0; i < decisions.Count; i++)
            {
                for (int j = i + 1; j < decisions.Count; j++)
                {
                    var d1 = decisions[i];
                    var d2 = decisions[j];

                    // Check direct action conflicts
                    string[] conflicting1;
                    if (ACTION_CONFLICTS.TryGetValue(d1.Action, out conflicting1))
                    {
                        for (int k = 0; k < conflicting1.Length; k++)
                        {
                            if (conflicting1[k] == d2.Action)
                            {
                                conflicts.Add(new AgentConflict
                                {
                                    Type = ConflictType.DirectConflict,
                                    Action1 = d1.Action,
                                    Action2 = d2.Action,
                                    Agent1 = d1.Category.ToString(),
                                    Agent2 = d2.Category.ToString(),
                                    Severity = 0.8
                                });
                            }
                        }
                    }

                    // Check resource conflicts (both want heavy CPU)
                    if (d1.Category == DecisionCategory.ProcessPriority && d2.Category == DecisionCategory.ProcessPriority)
                    {
                        if (d1.Confidence > 0.5 && d2.Confidence > 0.5)
                        {
                            conflicts.Add(new AgentConflict
                            {
                                Type = ConflictType.ResourceConflict,
                                Action1 = d1.Action,
                                Action2 = d2.Action,
                                Agent1 = "CPU",
                                Agent2 = "CPU",
                                Severity = 0.5
                            });
                        }
                    }

                    // Check goal conflicts (performance vs thermal vs battery)
                    if ((d1.Category == DecisionCategory.ThermalProtect && d2.Category == DecisionCategory.GameBoost) ||
                        (d1.Category == DecisionCategory.GameBoost && d2.Category == DecisionCategory.ThermalProtect))
                    {
                        conflicts.Add(new AgentConflict
                        {
                            Type = ConflictType.GoalConflict,
                            Action1 = d1.Action,
                            Action2 = d2.Action,
                            Agent1 = d1.Category.ToString(),
                            Agent2 = d2.Category.ToString(),
                            Severity = 0.6
                        });
                    }

                    if ((d1.Category == DecisionCategory.PowerPlan && d2.Category == DecisionCategory.GameBoost) ||
                        (d1.Category == DecisionCategory.GameBoost && d2.Category == DecisionCategory.PowerPlan))
                    {
                        conflicts.Add(new AgentConflict
                        {
                            Type = ConflictType.GoalConflict,
                            Action1 = d1.Action,
                            Action2 = d2.Action,
                            Agent1 = d1.Category.ToString(),
                            Agent2 = d2.Category.ToString(),
                            Severity = 0.5
                        });
                    }

                    // Priority conflicts (both high priority)
                    if (d1.Confidence > 0.7 && d2.Confidence > 0.7 && d1.Action != d2.Action)
                    {
                        conflicts.Add(new AgentConflict
                        {
                            Type = ConflictType.PriorityConflict,
                            Action1 = d1.Action,
                            Action2 = d2.Action,
                            Agent1 = d1.Category.ToString(),
                            Agent2 = d2.Category.ToString(),
                            Severity = 0.4
                        });
                    }
                }
            }

            _totalConflicts += conflicts.Count;
            return conflicts;
        }

        // === Resolve Single Conflict ===
        private List<AgentDecision> ResolveSingleConflict(AgentConflict conflict, List<AgentDecision> current)
        {
            var record = new ConflictRecord();
            record.Timestamp = DateTime.Now;
            record.Conflict = conflict;

            var resolved = new List<AgentDecision>(current);

            // Find the two conflicting decisions
            AgentDecision d1 = null, d2 = null;
            foreach (var d in resolved)
            {
                if (d.Action == conflict.Action1) d1 = d;
                if (d.Action == conflict.Action2) d2 = d;
            }
            if (d1 == null || d2 == null) return null;

            switch (conflict.Type)
            {
                case ConflictType.DirectConflict:
                    // Keep the higher confidence one
                    if (d1.Confidence >= d2.Confidence)
                    {
                        resolved.Remove(d2);
                        record.Resolution = "Kept " + d1.Action + " (higher confidence: " + string.Format("{0:F0}%", d1.Confidence * 100) + ")";
                    }
                    else
                    {
                        resolved.Remove(d1);
                        record.Resolution = "Kept " + d2.Action + " (higher confidence: " + string.Format("{0:F0}%", d2.Confidence * 100) + ")";
                    }
                    break;

                case ConflictType.GoalConflict:
                    // Create compromise: moderate version of both
                    var compromise = CreateCompromise(d1, d2);
                    resolved.Remove(d1);
                    resolved.Remove(d2);
                    resolved.Add(compromise);
                    record.Resolution = "Compromise: " + compromise.Action + " (" + compromise.Description + ")";
                    break;

                case ConflictType.PriorityConflict:
                    // Keep the one with higher priority * confidence product
                    double score1 = d1.Confidence * (d1.Risk < 0.5 ? 1.2 : 0.8);
                    double score2 = d2.Confidence * (d2.Risk < 0.5 ? 1.2 : 0.8);
                    if (score1 >= score2) { resolved.Remove(d2); record.Resolution = "Kept " + d1.Action + " (better score)"; }
                    else { resolved.Remove(d1); record.Resolution = "Kept " + d2.Action + " (better score)"; }
                    break;

                case ConflictType.ResourceConflict:
                    // Keep the less resource-intensive one
                    int cost1, cost2;
                    bool hasCost1 = ACTION_COST.TryGetValue(d1.Action, out cost1);
                    bool hasCost2 = ACTION_COST.TryGetValue(d2.Action, out cost2);
                    if (hasCost1 && hasCost2 && cost1 <= cost2)
                    { resolved.Remove(d2); record.Resolution = "Kept " + d1.Action + " (lower resource cost)"; }
                    else
                    { resolved.Remove(d1); record.Resolution = "Kept " + d2.Action + " (lower resource cost)"; }
                    break;
            }

            record.WasResolved = true;
            _conflictHistory.Add(record);
            if (_conflictHistory.Count > 100) _conflictHistory.RemoveAt(0);

            AddLog("CONFLICT", "Resolver",
                string.Format("Resolved {0}: {1}", conflict.Type, record.Resolution));

            return resolved;
        }

        // === Create Compromise Decision ===
        private AgentDecision CreateCompromise(AgentDecision d1, AgentDecision d2)
        {
            var compromise = new AgentDecision();
            compromise.Timestamp = DateTime.Now;

            // Pick the less aggressive action
            if (d1.Confidence > d2.Confidence)
            {
                compromise.Action = d1.Action;
                compromise.Category = d1.Category;
                compromise.Description = "Balanced: " + d1.Description;
            }
            else
            {
                compromise.Action = d2.Action;
                compromise.Category = d2.Category;
                compromise.Description = "Balanced: " + d2.Description;
            }

            // Reduce confidence for compromise
            compromise.Confidence = Math.Min(d1.Confidence, d2.Confidence) * 0.8;
            compromise.Risk = Math.Max(d1.Risk, d2.Risk) * 0.9;
            compromise.Reason = string.Format("Compromise between {0} and {1}", d1.Action, d2.Action);

            return compromise;
        }
    }
}
