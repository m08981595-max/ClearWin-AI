using System;
using System.Collections.Generic;

namespace ClearWin.RuleEngine
{
    /// <summary>
    /// Reasoning Engine: forward chaining over production rules, backward
    /// chaining to prove a hypothesis, and causal inference that connects
    /// symptoms (effects) to their underlying causes.
    /// </summary>
    public class ReasoningEngine
    {
        private readonly RuleKnowledgeBase _kb;
        private const int MaxInferenceIterations = 8;

        public ReasoningEngine(RuleKnowledgeBase kb)
        {
            _kb = kb;
        }

        public class InferenceResult
        {
            public List<ExpertFact> DerivedFacts;
            public List<string> RulesFired;
            public List<string> Chain;    // human readable trace
            public List<string> RecommendedActions; // action ids suggested by fired rules

            public InferenceResult()
            {
                DerivedFacts = new List<ExpertFact>();
                RulesFired = new List<string>();
                Chain = new List<string>();
                RecommendedActions = new List<string>();
            }
        }

        /// <summary>
        /// Forward chaining: repeatedly fires enabled rules whose conditions
        /// are satisfied by current facts, deriving new derived facts.
        /// Stale derived facts (whose rule no longer fires) are retracted first.
        /// </summary>
        public InferenceResult ForwardChain()
        {
            RetractStaleDerived();

            var result = new InferenceResult();
            var rules = _kb.GetRules();
            var derived = new Dictionary<string, double>();
            var used = new HashSet<string>();

            for (int iter = 0; iter < MaxInferenceIterations; iter++)
            {
                bool fired = false;
                for (int i = 0; i < rules.Count; i++)
                {
                    ExpertRule rule = rules[i];
                    if (!rule.Enabled) continue;
                    if (used.Contains(rule.Id)) continue;
                    if (!IsRuleSatisfied(rule)) continue;

                    used.Add(rule.Id);
                    fired = true;
                    rule.MatchCount++;

                    // Parse conclusion "var=value" or "var"
                    string conclusion = rule.Conclusion;
                    double value = 1.0;
                    int eq = conclusion == null ? -1 : conclusion.IndexOf('=');
                    string variable = conclusion;
                    if (eq > 0)
                    {
                        variable = conclusion.Substring(0, eq);
                        double.TryParse(conclusion.Substring(eq + 1), out value);
                    }
                    double conf = rule.GetEffectiveConfidence();
                    _kb.SetFact(variable, value, conf, "rule:" + rule.Id);
                    if (!derived.ContainsKey(variable)) derived[variable] = value;
                    result.RulesFired.Add(rule.Id);
                    if (!string.IsNullOrEmpty(rule.RecommendedAction) &&
                        !result.RecommendedActions.Contains(rule.RecommendedAction))
                        result.RecommendedActions.Add(rule.RecommendedAction);
                    result.Chain.Add(string.Format("Rule '{0}': {1} (confidence {2:F2})",
                        rule.Name, rule.Description ?? "", conf));
                }
                if (!fired) break;
            }

            foreach (var kv in derived)
            {
                result.DerivedFacts.Add(new ExpertFact { Variable = kv.Key, Value = kv.Value, Confidence = 1.0, Source = "inference" });
            }
            return result;
        }

        /// <summary>
        /// Retracts facts derived by rules whose conditions are no longer met,
        /// so the knowledge base never carries stale conclusions.
        /// </summary>
        private void RetractStaleDerived()
        {
            List<ExpertFact> facts = _kb.GetAllFacts();
            const string prefix = "rule:";
            for (int i = 0; i < facts.Count; i++)
            {
                ExpertFact f = facts[i];
                if (f.Source == null || !f.Source.StartsWith(prefix)) continue;
                string ruleId = f.Source.Substring(prefix.Length);
                ExpertRule rule = _kb.GetRule(ruleId);
                if (rule == null || !rule.Enabled || !IsRuleSatisfied(rule))
                    _kb.RemoveFact(f.Variable);
            }
        }

        /// <summary>Checks whether a rule's conditions are met by known facts.</summary>
        public bool IsRuleSatisfied(ExpertRule rule)
        {
            if (rule == null || rule.Conditions == null || rule.Conditions.Count == 0) return true;
            for (int i = 0; i < rule.Conditions.Count; i++)
            {
                RuleCondition c = rule.Conditions[i];
                ExpertFact fact = _kb.GetFact(c.Variable);
                if (fact == null || fact.Confidence < RuleEngineConstants.FactMinConfidence) return false;
                if (!c.Satisfied(fact.Value)) return false;
            }
            return true;
        }

        /// <summary>
        /// Backward chaining: prove a target variable by finding rules whose
        /// conclusion matches it and recursively checking their conditions.
        /// Returns the proof chain (rule names used).
        /// </summary>
        public List<string> BackwardChain(string targetVariable, int depth)
        {
            var chain = new List<string>();
            if (depth <= 0) return chain;
            ExpertFact direct = _kb.GetFact(targetVariable);
            if (direct != null && direct.Confidence >= RuleEngineConstants.FactMinConfidence)
            {
                chain.Add(string.Format("Fact known: {0} = {1:F2} (confidence {2:F2})",
                    targetVariable, direct.Value, direct.Confidence));
                return chain;
            }

            var rules = _kb.RulesConcluding(targetVariable);
            for (int i = 0; i < rules.Count; i++)
            {
                ExpertRule rule = rules[i];
                if (!IsRuleSatisfied(rule)) continue;
                chain.Add(string.Format("Rule '{0}' concluded {1}",
                    rule.Name, rule.Conclusion));
                foreach (RuleCondition c in rule.Conditions)
                {
                    var sub = BackwardChain(c.Variable, depth - 1);
                    for (int j = 0; j < sub.Count; j++) chain.Add("  " + sub[j]);
                }
                break;
            }
            return chain;
        }

        /// <summary>
        /// Causal inference: given an observed effect (symptom), walk the causal
        /// links backwards to find the most probable root causes, ranked by
        /// strength * current severity of the cause.
        /// </summary>
        public List<ExpertHypothesis> Hypothesize(string effectVariable, double symptomSeverity)
        {
            return HypothesizeDeep(effectVariable, symptomSeverity, 1);
        }

        /// <summary>
        /// Multi-hop causal inference: walks the causal graph backwards up to
        /// maxDepth links from the observed effect, aggregating probability
        /// along each chain and merging duplicate root causes.
        /// </summary>
        public List<ExpertHypothesis> HypothesizeDeep(string effectVariable, double symptomSeverity, int maxDepth)
        {
            var paths = new List<HypothesisPath>();
            var visited = new HashSet<string>();
            visited.Add(effectVariable);
            WalkCausalChain(effectVariable, symptomSeverity, maxDepth, visited, paths);

            // Merge duplicate causes (reachable via multiple chains) by taking
            // the strongest path and combining evidence.
            var merged = new Dictionary<string, ExpertHypothesis>();
            for (int i = 0; i < paths.Count; i++)
            {
                HypothesisPath p = paths[i];
                ExpertHypothesis existing;
                if (!merged.TryGetValue(p.Cause, out existing) || p.Probability > existing.Probability)
                {
                    var h = new ExpertHypothesis
                    {
                        Name = p.Cause,
                        Description = p.Mechanism,
                        Probability = Math.Max(0.0, Math.Min(1.0, p.Probability)),
                        RecommendedAction = RecommendedForCategory(p.Category)
                    };
                    h.SupportingEvidence.Add(p.Evidence);
                    merged[p.Cause] = h;
                }
                else if (existing.SupportingEvidence.Count < 4)
                {
                    existing.SupportingEvidence.Add(p.Evidence);
                }
            }

            var hypotheses = new List<ExpertHypothesis>();
            foreach (var kv in merged) hypotheses.Add(kv.Value);
            hypotheses.Sort(delegate(ExpertHypothesis a, ExpertHypothesis b)
            {
                return b.Probability.CompareTo(a.Probability);
            });
            return hypotheses;
        }

        private class HypothesisPath
        {
            public string Cause;
            public string Mechanism;
            public string Category;
            public double Probability;
            public string Evidence;
        }

        private void WalkCausalChain(string effect, double probability, int depth,
            HashSet<string> visited, List<HypothesisPath> output)
        {
            if (depth <= 0) return;
            List<CausalLink> links = _kb.LinksByEffect(effect);
            for (int i = 0; i < links.Count; i++)
            {
                CausalLink link = links[i];
                if (!link.Enabled) continue;
                if (visited.Contains(link.Cause)) continue;

                ExpertFact causeFact = _kb.GetFact(link.Cause);
                double causeSeverity = causeFact != null ? causeFact.Value : 0;
                // Chain probability must never exceed the parent probability:
                // a deeper cause cannot be more probable than the evidence that
                // leads to it. Observed severity only scales within that bound.
                double p = probability * link.Strength;
                if (causeFact != null && causeFact.Confidence >= RuleEngineConstants.FactMinConfidence)
                {
                    double factor = 0.5 + Math.Min(1.0, causeSeverity / 100.0);
                    p = Math.Min(probability, p * factor);
                }

                string evidence = string.Format("Causal chain: {0} -> {1} (strength {2:F2})",
                    link.Cause, link.Effect, link.Strength);
                if (causeFact != null)
                    evidence += string.Format("; observed {0} = {1:F1}", link.Cause, causeFact.Value);

                output.Add(new HypothesisPath
                {
                    Cause = link.Cause,
                    Mechanism = link.Mechanism,
                    Category = link.Category,
                    Probability = p,
                    Evidence = evidence
                });

                if (depth > 1)
                {
                    visited.Add(link.Cause);
                    WalkCausalChain(link.Cause, p, depth - 1, visited, output);
                    visited.Remove(link.Cause);
                }
            }
        }

        /// <summary>Maps a causal category to a sensible default action id.</summary>
        public static string RecommendedForCategory(string category)
        {
            if (category == null) return null;
            switch (category)
            {
                case "Thermal": return "ReduceThermal";
                case "Memory": return "LightClean";
                case "CPU": return "LimitBackground";
                case "Disk": return "SetLowIoPriority";
                case "Power": return "BatterySaver";
                case "Gaming": return "BoostGamePriority";
                default: return null;
            }
        }
    }
}
