using System;
using System.Collections.Generic;

namespace ClearWin.RuleEngine
{
    /// <summary>
    /// Expert System Core: orchestrates all ten engines into a single,
    /// well-defined pipeline:
    ///
    ///   Memory    -> capture device + session context
    ///   Diagnostic-> deep problem analysis (symptom -> root cause)
    ///   Reasoning -> inference + causal hypothesis ranking
    ///   Decision  -> generate + score candidate solutions
    ///   Validator -> verify every action before it may run
    ///   Safety    -> block anything that could damage the system
    ///   Planner   -> build an ordered, conflict-free plan
    ///   Explain   -> explain every recommendation to the user
    ///   Learning  -> feed outcomes back into the knowledge base
    ///
    /// All engines live in separate files/classes and are injectable,
    /// making the whole system testable and extensible.
    /// </summary>
    public class RuleEngineCore
    {
        public RuleKnowledgeBase Knowledge { get; private set; }
        public ReasoningEngine Reasoning { get; private set; }
        public OptimizationPlanner Planner { get; private set; }
        public DecisionEngine Decision { get; private set; }
        public ActionValidator Validator { get; private set; }
        public SafetyEngine Safety { get; private set; }
        public LearningEngine Learning { get; private set; }
        public MemoryEngine Memory { get; private set; }
        public DiagnosticEngine Diagnostic { get; private set; }
        public ExplainEngine Explain { get; private set; }

        private readonly object _lock = new object();
        private RuleEngineResult _lastResult;
        private readonly Dictionary<string, DateTime> _recentlyBlocked = new Dictionary<string, DateTime>();

        // How long a blocked action stays "remembered" before re-evaluation.
        private const double BlockedRetryMinutes = 10;

        public Action<string> OnLog;

        public RuleEngineCore(string dataDir)
        {
            Knowledge = new RuleKnowledgeBase(dataDir);
            Reasoning = new ReasoningEngine(Knowledge);
            Planner = new OptimizationPlanner();
            Decision = new DecisionEngine(Knowledge);
            Validator = new ActionValidator(Knowledge);
            Safety = new SafetyEngine();
            Learning = new LearningEngine(Knowledge);
            Memory = new MemoryEngine(dataDir);
            Diagnostic = new DiagnosticEngine(Knowledge, Reasoning);
            Explain = new ExplainEngine();

            Knowledge.Load();
            if (Knowledge.RuleCount == 0)
            {
                Knowledge.SeedDefaults();
                Knowledge.Save();
            }
            Memory.BeginSession("AutoPilot");
        }

        private void Log(string msg)
        {
            if (OnLog != null)
            {
                try { OnLog(msg); }
                catch { }
            }
        }

        // ============================ LIVE OBSERVATION ============================

        /// <summary>Cheap hook called every tick so the system keeps session context.</summary>
        public void ObserveSystemState(SystemState state)
        {
            if (state == null) return;
            try
            {
                if (Memory.Device == null) Memory.CaptureDevice(state);
                Memory.CaptureState(state);
            }
            catch { /* observation must never break the host */ }
        }

        public void SetSessionMode(string mode)
        {
            Memory.RecordEvent("ModeChange", "Mode set to " + (mode ?? "AutoPilot"), 0);
        }

        // ============================ ANALYSIS PIPELINE ============================

        /// <summary>Runs the full expert-system cycle and returns a complete result.</summary>
        public RuleEngineResult RunAnalysis(SystemState state, string mode)
        {
            lock (_lock)
            {
                var result = new RuleEngineResult { Mode = mode ?? "AutoPilot", State = state != null ? state.Clone() : null };
                try
                {
                    ObserveSystemState(state);
                    if (state == null)
                    {
                        Log("ES: no state to analyze");
                        return result;
                    }

                    // 1) Device + session context.
                    DeviceSnapshot device = Memory.CaptureDevice(state);
                    result.Device = device;
                    if (!string.IsNullOrEmpty(mode)) Memory.RecordEvent("Analysis", "Analysis cycle in " + mode, 0);

                    // 2) Deep diagnosis.
                    result.Diagnosis = Diagnostic.Diagnose(state, device);
                    Log(string.Format("ES: diagnosis = {0} (root cause {1}, confidence {2:F2})",
                        result.Diagnosis.Symptom, result.Diagnosis.RootCause, result.Diagnosis.Confidence));

                    // 3) Reasoning: derive additional facts + causal hypotheses.
                    ReasoningEngine.InferenceResult inference = Reasoning.ForwardChain();
                    result.Facts = Knowledge.GetAllFacts();

                    // 4) Candidate generation + scoring (Decision Engine).
                    PurgeStaleBlocked();
                    result.Candidates = Decision.GenerateCandidates(result.Diagnosis, state, _recentlyBlocked, inference.RecommendedActions);
                    result.Candidates = Decision.Rank(result.Candidates, state, device);

                    // 5) Validate + gate every candidate.
                    var recommended = new List<CandidateAction>();
                    var blocked = new List<CandidateAction>();
                    for (int i = 0; i < result.Candidates.Count; i++)
                    {
                        CandidateAction c = result.Candidates[i];
                        ValidationResult validation = Validator.Validate(c, state, device);
                        SafetyCheckResult safety = Safety.Assess(c, state, device);

                        if (validation.Valid && safety.Decision != SafetyDecision.Blocked)
                        {
                            if (safety.Decision == SafetyDecision.NeedsApproval)
                                Log("ES: " + c.Id + " needs approval (risk " + c.Risk.ToString("F2") + ")");
                            recommended.Add(c);
                            result.ValidatedCount++;
                        }
                        else
                        {
                            result.BlockedCount++;
                            blocked.Add(c);
                            if (c.Id != null) _recentlyBlocked[c.Id] = DateTime.Now;
                            Log("ES: " + (c.Id ?? "?") + " blocked (" +
                                (validation != null && validation.Errors.Count > 0 ? validation.Errors[0] : "safety") + ")");
                        }
                    }
                    result.Recommended = recommended;
                    result.Blocked = blocked;

                    // 6) Plan before any execution.
                    string goal = result.Diagnosis != null ? result.Diagnosis.Symptom : "general optimization";
                    result.Plan = Planner.BuildPlan(recommended, blocked, result.Diagnosis, goal);

                    // 7) Explain every recommendation (alternatives never include blocked ones).
                    foreach (CandidateAction c in recommended)
                    {
                        var alternatives = new List<CandidateAction>();
                        for (int j = 0; j < result.Candidates.Count; j++)
                        {
                            if (result.Candidates[j].Id == c.Id) continue;
                            bool isBlocked = false;
                            for (int b = 0; b < blocked.Count; b++)
                            {
                                if (blocked[b].Id == result.Candidates[j].Id) { isBlocked = true; break; }
                            }
                            if (!isBlocked) alternatives.Add(result.Candidates[j]);
                        }
                        var explanation = Explain.Explain(c, result.Diagnosis, inference.Chain, alternatives);
                        result.Explanations.Add(explanation);
                    }

                    _lastResult = result;
                    Memory.Save();
                    Log(string.Format("ES: {0} candidates, {1} recommended, {2} blocked, plan has {3} steps",
                        result.Candidates.Count, recommended.Count, blocked.Count,
                        result.Plan != null ? result.Plan.Steps.Count : 0));
                    return result;
                }
                catch (Exception ex)
                {
                    Log("ES: analysis error - " + ex.Message);
                    _lastResult = result;
                    return result;
                }
            }
        }

        /// <summary>Builds the top recommendation (diagnosis + action + explanation).</summary>
        public ExpertRecommendation GetTopRecommendation()
        {
            lock (_lock)
            {
                if (_lastResult == null || _lastResult.Recommended.Count == 0) return null;
                CandidateAction best = _lastResult.Recommended[0];
                ExpertExplanation explanation = null;
                for (int i = 0; i < _lastResult.Explanations.Count; i++)
                {
                    if (_lastResult.Explanations[i].ActionId == best.Id)
                    {
                        explanation = _lastResult.Explanations[i];
                        break;
                    }
                }
                return new ExpertRecommendation
                {
                    Diagnosis = _lastResult.Diagnosis,
                    Action = best,
                    Explanation = explanation
                };
            }
        }

        public RuleEngineResult GetLastResult()
        {
            lock (_lock) { return _lastResult; }
        }

        // ============================ LEARNING ============================

        /// <summary>Forgets actions blocked long enough ago so they can be re-evaluated.</summary>
        private void PurgeStaleBlocked()
        {
            var expired = new List<string>();
            DateTime cutoff = DateTime.Now.AddMinutes(-BlockedRetryMinutes);
            foreach (var kv in _recentlyBlocked)
            {
                if (kv.Value < cutoff) expired.Add(kv.Key);
            }
            for (int i = 0; i < expired.Count; i++)
                _recentlyBlocked.Remove(expired[i]);
        }

        /// <summary>Feeds a real outcome back so the knowledge base improves.</summary>
        public void RecordOutcome(string actionId, bool success, double beforeScore, double afterScore, double risk, string category)
        {
            if (string.IsNullOrEmpty(actionId)) return;
            Learning.RecordOutcome(actionId, success, beforeScore, afterScore, risk, category);
            Learning.ReinforceCausalLinks(category, success, 1.0);
            if (success)
            {
                // Learning success weakens the memory of blocking; allow re-evaluation.
                if (_recentlyBlocked.ContainsKey(actionId)) _recentlyBlocked.Remove(actionId);
            }
            Knowledge.Save();
            Memory.Save();
            Memory.RecordEvent("Learning", string.Format("{0} outcome recorded ({1})", actionId, success ? "success" : "fail"), afterScore - beforeScore);
            Log("ES: learned outcome for " + actionId + " (" + (success ? "OK" : "FAIL") + ")");
        }

        // ============================ SUMMARY ============================

        public string GetSystemSummary()
        {
            string kb = string.Format("Knowledge: {0} facts, {1} rules, {2} causal links, {3} action stats",
                Knowledge.FactCount, Knowledge.RuleCount, Knowledge.LinkCount, Knowledge.StatCount);
            string lr = Learning.GetInsights();
            string ctx = Memory.GetSessionContext();
            return kb + Environment.NewLine + "--- Learning ---" + Environment.NewLine + lr + "--- Context ---" + Environment.NewLine + ctx;
        }
    }
}
