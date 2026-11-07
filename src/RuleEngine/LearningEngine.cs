using System;
using System.Collections.Generic;
using System.Text;

namespace ClearWin.RuleEngine
{
    /// <summary>
    /// Learning Engine: improves the knowledge base from real outcomes.
    /// Records action success/failure, measures score deltas, reinforces or
    /// weakens causal links, and produces a concise learning summary.
    /// </summary>
    public class LearningEngine
    {
        private readonly RuleKnowledgeBase _kb;
        private readonly object _lock = new object();
        private int _totalLearnings;
        private int _totalSuccesses;
        private readonly Dictionary<string, int> _categoryHits = new Dictionary<string, int>();

        public LearningEngine(RuleKnowledgeBase kb)
        {
            _kb = kb;
        }

        /// <summary>Feeds one real-world outcome back into the knowledge base.</summary>
        public void RecordOutcome(string actionId, bool success, double beforeScore, double afterScore, double risk, string category)
        {
            if (string.IsNullOrEmpty(actionId)) return;
            double delta = afterScore - beforeScore;
            _kb.RecordOutcome(actionId, success, delta, risk, category);

            // Teach the production rules: any rule that recommended this action
            // learns from the actual outcome.
            List<ExpertRule> rules = _kb.GetRules();
            for (int i = 0; i < rules.Count; i++)
            {
                if (string.IsNullOrEmpty(rules[i].RecommendedAction)) continue;
                if (string.Equals(rules[i].RecommendedAction, actionId, StringComparison.OrdinalIgnoreCase))
                    _kb.RecordRuleMatch(rules[i].Id, success);
            }

            lock (_lock)
            {
                _totalLearnings++;
                if (success) _totalSuccesses++;
            }
        }

        /// <summary>
        /// Causal reinforcement: if an action improved the system, strengthen the
        /// causal links that support that outcome; if it hurt, weaken them.
        /// Scoped to the matching category (with aliases) and scaled by outcome size.
        /// </summary>
        public void ReinforceCausalLinks(string category, bool improved, double strength)
        {
            List<CausalLink> links = _kb.GetAllLinks();
            double magnitude = 0.5 + Math.Min(1.0, strength / 40.0);
            double delta = (improved ? 0.05 : -0.05) * magnitude;
            delta = Math.Max(-0.08, Math.Min(0.08, delta));
            for (int i = 0; i < links.Count; i++)
            {
                CausalLink link = links[i];
                if (link.Category == null || link.Category.Length == 0) continue;
                if (CategoryAliasMatches(link.Category, category))
                    _kb.AdjustLinkStrength(link.Id, delta);
            }
        }

        private static bool CategoryAliasMatches(string linkCategory, string requested)
        {
            if (requested == null) return false;
            if (string.Equals(linkCategory, requested, StringComparison.OrdinalIgnoreCase)) return true;
            string a = linkCategory.ToLowerInvariant();
            string b = requested.ToLowerInvariant();
            if (a == "process") a = "cpu";
            if (b == "process") b = "cpu";
            return a == b;
        }

        /// <summary>Records which diagnosis categories were acted upon (trend data).</summary>
        public void RecordDiagnosisHit(string category)
        {
            lock (_lock)
            {
                if (string.IsNullOrEmpty(category)) category = "General";
                int v;
                if (!_categoryHits.TryGetValue(category, out v)) v = 0;
                _categoryHits[category] = v + 1;
            }
        }

        public int TotalLearnings { get { lock (_lock) { return _totalLearnings; } } }
        public double OverallSuccessRate
        {
            get { lock (_lock) { return _totalLearnings > 0 ? (double)_totalSuccesses / _totalLearnings : 0.5; } }
        }

        /// <summary>Aggregated, human-readable learning summary.</summary>
        public string GetInsights()
        {
            var sb = new StringBuilder();
            List<ActionStatistics> stats = _kb.GetAllStatistics();
            sb.AppendLine(string.Format("Learnings: {0}, success rate: {1:F1}%",
                _totalLearnings, OverallSuccessRate * 100));
            int shown = 0;
            for (int i = 0; i < stats.Count && shown < 5; i++)
            {
                ActionStatistics s = stats[i];
                if (s.TotalUses < RuleEngineConstants.LearningMinSamples) continue;
                sb.AppendLine(string.Format("- {0}: {1:F1}% success, avg impact {2:F1}",
                    s.ActionId, s.SuccessRate * 100, s.AvgImpact));
                shown++;
            }
            return sb.ToString();
        }
    }
}
