using System;
using System.Collections.Generic;

namespace ClearWin
{
    public partial class SmartAgent
    {
        // =====================================================================
        //  EXPERT SYSTEM INTEGRATION
        //  Bridges the standalone ClearWin.RuleEngine subsystem into the
        //  agent without altering any existing behavior.
        // =====================================================================

        private ClearWin.RuleEngine.RuleEngineCore _expertSystem;
        private DateTime _lastExpertAnalysis = DateTime.MinValue;
        private const int EXPERT_ANALYSIS_INTERVAL_SECONDS = 20;

        private void InitializeRuleEngine()
        {
            try
            {
                _expertSystem = new ClearWin.RuleEngine.RuleEngineCore(_dataDir);
                _expertSystem.OnLog = delegate(string msg) { AddLog("ES", "RuleEngine", msg); };
                _expertSystem.SetSessionMode(_mode.ToString());
                if (Events != null) Events.Subscribe<StateObservedEvent>(OnStateObservedEvent);
                AddLog("INFO", "RuleEngine", "Professional Expert System initialized");
            }
            catch (Exception ex)
            {
                LogError("InitializeRuleEngine", ex);
            }
        }

        /// <summary>Public handle to the standalone Expert System.</summary>
        public ClearWin.RuleEngine.RuleEngineCore GetRuleEngine()
        {
            return _expertSystem;
        }

        public bool IsRuleEngineActive()
        {
            return _expertSystem != null;
        }

        /// <summary>
        /// Runs a full expert analysis (diagnosis -> reasoning -> decision ->
        /// validation -> safety -> planning -> explanation) over the current
        /// system snapshot. Throttled so it never hogs the live loop.
        /// </summary>
        public ClearWin.RuleEngine.RuleEngineResult RunRuleEngineAnalysis(bool force)
        {
            if (_expertSystem == null) return null;
            if (!force && (DateTime.Now - _lastExpertAnalysis).TotalSeconds < EXPERT_ANALYSIS_INTERVAL_SECONDS)
                return _expertSystem.GetLastResult();
            _lastExpertAnalysis = DateTime.Now;
            try
            {
                return _expertSystem.RunAnalysis(_currentState != null ? _currentState.Clone() : null, _mode.ToString());
            }
            catch (Exception ex)
            {
                LogError("RunRuleEngineAnalysis", ex);
                return _expertSystem.GetLastResult();
            }
        }

        public ClearWin.RuleEngine.RuleEngineResult RunRuleEngineAnalysis()
        {
            return RunRuleEngineAnalysis(false);
        }

        public ClearWin.RuleEngine.ExpertDiagnosis GetExpertDiagnosis()
        {
            if (_expertSystem == null) return null;
            var last = _expertSystem.GetLastResult();
            return last != null ? last.Diagnosis : null;
        }

        public List<ClearWin.RuleEngine.CandidateAction> GetExpertRecommendations()
        {
            if (_expertSystem == null) return new List<ClearWin.RuleEngine.CandidateAction>();
            var last = _expertSystem.GetLastResult();
            return last != null ? last.Recommended : new List<ClearWin.RuleEngine.CandidateAction>();
        }

        public ClearWin.RuleEngine.ExpertPlan GetExpertPlan()
        {
            if (_expertSystem == null) return null;
            var last = _expertSystem.GetLastResult();
            return last != null ? last.Plan : null;
        }

        public List<ClearWin.RuleEngine.ExpertExplanation> GetExpertExplanations()
        {
            if (_expertSystem == null) return new List<ClearWin.RuleEngine.ExpertExplanation>();
            var last = _expertSystem.GetLastResult();
            return last != null ? last.Explanations : new List<ClearWin.RuleEngine.ExpertExplanation>();
        }

        public string GetRuleEngineSummary()
        {
            if (_expertSystem == null) return "Expert System not available.";
            return _expertSystem.GetSystemSummary();
        }

        /// <summary>
        /// Bridges the professional Expert System into the live decision loop.
        /// Runs a throttled full analysis and converts the top validated
        /// recommendations into AgentDecisions that flow through the normal
        /// ranking, cooldown and execution pipeline. Outcome feedback is fed
        /// back by the shared mirror in SmartAgent.KnowledgeBase.cs.
        /// </summary>
        private void IntegrateRuleEngineDecisions(List<AgentDecision> decisions)
        {
            if (_expertSystem == null || decisions == null) return;
            try
            {
                // Re-emit ES decisions only on a fresh analysis cycle; a stale
                // result would otherwise be re-injected every tick between
                // analyses (log spam and duplicate candidates).
                if ((DateTime.Now - _lastExpertAnalysis).TotalSeconds < EXPERT_ANALYSIS_INTERVAL_SECONDS)
                    return;
                var result = RunRuleEngineAnalysis(false);
                if (result == null || result.Recommended == null || result.Recommended.Count == 0) return;

                int added = 0;
                for (int i = 0; i < result.Recommended.Count && added < 2; i++)
                {
                    ClearWin.RuleEngine.CandidateAction c = result.Recommended[i];
                    if (c.Id == null || c.ExpectedImpact < 6) continue;

                    // Avoid duplicating an action another subsystem already proposed.
                    bool duplicate = false;
                    for (int j = 0; j < decisions.Count; j++)
                    {
                        if (decisions[j] != null &&
                            string.Equals(decisions[j].Action, c.Id, StringComparison.OrdinalIgnoreCase))
                        { duplicate = true; break; }
                    }
                    if (duplicate) continue;

                    string rootCause = result.Diagnosis != null
                        ? (result.Diagnosis.RootCause ?? result.Diagnosis.Symptom)
                        : (c.Evidence ?? c.Description);

                    var d = new AgentDecision();
                    d.Action = c.Id;
                    d.Category = MapExpertActionToCategory(c.Id);
                    d.Description = c.Description ?? c.Id;
                    d.Reason = "[RuleEngine] " + (c.Evidence ?? c.Description) +
                        " (root cause: " + rootCause + ")";
                    d.Source = "RuleEngine";
                    // Honest confidence: no artificial floor so weak recommendations
                    // fall through the normal 0.3 gate and learned abstention works.
                    d.Confidence = Math.Max(0, Math.Min(1, c.Confidence));
                    d.Risk = Math.Max(0.05, c.Risk);
                    d.RiskScore = c.Risk * 100;
                    d.Safety = (c.Risk >= ClearWin.RuleEngine.RuleEngineConstants.RiskRequiresApproval
                        || ExpertActionRequiresApproval(c.Id))
                        ? DecisionSafety.NeedsApproval : DecisionSafety.Safe;
                    d.Impact = Math.Max(1, Math.Min(10, (int)(c.ExpectedImpact / 2.0)));
                    d.Mode = _mode.ToString();
                    decisions.Add(d);
                    added++;
                    AddLog("ES", "RuleEngine", "Recommending " + c.Id + " | " + (c.Evidence ?? c.Description));
                }
            }
            catch (Exception ex) { LogError("IntegrateRuleEngineDecisions", ex); }
        }

        /// <summary>
        /// Executes the ES "StartService" recommendation. Prefers the service
        /// named in the decision parameters; otherwise re-scans and starts the
        /// first auto-start service that stopped unexpectedly. Never throws.
        /// </summary>
        private bool ExecuteStartService(AgentDecision d)
        {
            try
            {
                string name = null;
                if (d != null && d.Parameters != null && d.Parameters.ContainsKey("ServiceName"))
                    name = d.Parameters["ServiceName"];
                if (string.IsNullOrEmpty(name))
                    name = ClearWin.RuleEngine.MemoryEngine.GetFirstAutoStoppedService();
                if (string.IsNullOrEmpty(name))
                {
                    AddLog("ACTION", "StartService", "No unexpectedly stopped auto-start service to start (advisory)");
                    return true;
                }
                bool started = ClearWin.RuleEngine.MemoryEngine.TryStartService(name);
                AddLog("ACTION", "StartService", started
                    ? "Started service: " + name
                    : "Failed to start service: " + name + " (needs administrator)");
                return started;
            }
            catch (Exception ex) { LogError("ExecuteStartService", ex); return false; }
        }

        /// <summary>
        /// Application-level approval policy: actions that persistently change
        /// system-wide state (power plans, network stack, startup items, services,
        /// disk cleanup) always require explicit user consent regardless of the
        /// learned risk score. Supplements the risk-threshold gate.
        /// </summary>
        private static bool ExpertActionRequiresApproval(string actionId)
        {
            if (string.IsNullOrEmpty(actionId)) return false;
            switch (actionId)
            {
                case "SwitchPowerPlan":
                case "BatterySaver":
                case "BalancedPerformance":
                case "OptimizeNetwork":
                case "StartupDisable":
                case "DiskCleanup":
                case "StartService":
                    return true;
                default:
                    return false;
            }
        }

        private static DecisionCategory MapExpertActionToCategory(string actionId)
        {
            if (string.IsNullOrEmpty(actionId)) return DecisionCategory.SystemStatus;
            switch (actionId)
            {
                case "AggressiveClean":
                case "LightClean":
                case "PreemptiveClean": return DecisionCategory.MemoryClean;
                case "PurgeStandbyList": return DecisionCategory.PurgeStandby;
                case "LimitBackground": return DecisionCategory.BackgroundControl;
                case "BoostGamePriority": return DecisionCategory.GameBoost;
                case "SetGameAffinity":
                case "LimitBackgroundAffinity": return DecisionCategory.CpuAffinity;
                case "SetLowIoPriority": return DecisionCategory.IoPriority;
                case "UnparkAllCores": return DecisionCategory.CoreParking;
                case "EmergencyThrottle":
                case "ReduceThermal": return DecisionCategory.ThermalProtect;
                case "BatterySaver":
                case "BalancedPerformance":
                case "SwitchPowerPlan": return DecisionCategory.PowerPlan;
                case "OptimizeNetwork":
                case "FlushDnsCache":
                case "ResetNetworkPriority": return DecisionCategory.NetworkOptimize;
                case "StabilizeFrames":
                case "ReduceMicroStutter": return DecisionCategory.GameBoost;
                case "StartupDisable":
                case "SuggestClose":
                case "HealthCheck": return DecisionCategory.BackgroundControl;
                case "DiskCleanup": return DecisionCategory.SystemStatus;
                case "StartService": return DecisionCategory.SystemStatus;
                default: return DecisionCategory.SystemStatus;
            }
        }
    }
}
