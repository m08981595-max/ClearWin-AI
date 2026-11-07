using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ClearWin
{
    public partial class SmartAgent
    {
        // === Knowledge Base Fields ===
        private List<KnowledgeEntry> _knowledgeBase = new List<KnowledgeEntry>();
        private DateTime _lastKBUpdate = DateTime.MinValue;
        private const int KB_UPDATE_INTERVAL_SECONDS = 120;
        private const int MAX_KNOWLEDGE_ENTRIES = 500;
        private Dictionary<string, KnowledgePattern> _learnedPatterns = new Dictionary<string, KnowledgePattern>();
        private int _kbTotalLearnings = 0;
        private int _kbTotalQueries = 0;
        private readonly object _kbLock = new object();

        // === Public Accessors ===
        public List<KnowledgeEntry> GetKnowledgeBase() { lock (_kbLock) { return new List<KnowledgeEntry>(_knowledgeBase); } }
        public int GetKnowledgeCount() { return _knowledgeBase.Count; }
        public int GetPatternCount() { return _learnedPatterns.Count; }

        // === Learn from Decision Result ===
        public void LearnFromResult(AgentDecision decision, bool success, double beforeScore, double afterScore)
        {
            if (decision == null) return;

            var entry = new KnowledgeEntry();
            entry.Timestamp = DateTime.Now;
            entry.Action = decision.Action;
            entry.Category = decision.Category.ToString();
            entry.Success = success;
            entry.BeforeScore = beforeScore;
            entry.AfterScore = afterScore;
            entry.ScoreDelta = afterScore - beforeScore;
            entry.SystemContext = string.Format("CPU={0:F0} RAM={1:F0} Temp={2:F0} Game={3}",
                _smoothCpu, _smoothRam, _smoothTemp, _currentState.IsGame);
            entry.Confidence = decision.Confidence;

            lock (_kbLock)
            {
                _knowledgeBase.Add(entry);
                if (_knowledgeBase.Count > MAX_KNOWLEDGE_ENTRIES) _knowledgeBase.RemoveAt(0);
                _kbTotalLearnings++;
                UpdateLearnedPattern(entry);
            }

            AddLog("KB", "KnowledgeBase",
                string.Format("Learned: {0} {1} (score delta: {2:F2})",
                decision.Action, success ? "SUCCESS" : "FAIL", entry.ScoreDelta));

            // Mirror the outcome into the Professional Expert System's learning engine.
            try
            {
                if (_expertSystem != null &&
                    ClearWin.RuleEngine.RuleKnowledgeBase.IsKnownAction(decision.Action))
                {
                    _expertSystem.RecordOutcome(decision.Action, success, beforeScore, afterScore,
                        decision.Risk, decision.Category.ToString());
                }
            }
            catch (Exception ex) { LogError("RuleEngineLearn", ex); }
        }

        // === Update Learned Pattern (with time-based decay) ===
        private void UpdateLearnedPattern(KnowledgeEntry entry)
        {
            string key = entry.Action + "_" + entry.Category;
            KnowledgePattern pattern;
            if (!_learnedPatterns.TryGetValue(key, out pattern))
            {
                pattern = new KnowledgePattern
                {
                    Action = entry.Action,
                    Category = entry.Category,
                    TotalTrials = 0,
                    Successes = 0,
                    AvgScoreDelta = 0,
                    BestContext = "",
                    WorstContext = "",
                    Confidence = 0.5,
                    LastUpdated = DateTime.Now
                };
            }

            // Exponential decay on old counts (prevents permanent bias from early data)
            double hoursSinceUpdate = (DateTime.Now - pattern.LastUpdated).TotalHours;
            double decayFactor = Math.Exp(-hoursSinceUpdate / 48.0); // half-life ~33 hours
            pattern.TotalTrials = (int)(pattern.TotalTrials * decayFactor);
            pattern.Successes = (int)(pattern.Successes * decayFactor);
            // Clamp to minimum 1 trial to avoid division issues
            if (pattern.TotalTrials < 1) pattern.TotalTrials = 1;
            if (pattern.Successes < 0) pattern.Successes = 0;
            if (pattern.Successes > pattern.TotalTrials) pattern.Successes = pattern.TotalTrials;

            // Add current observation
            pattern.TotalTrials++;
            if (entry.Success) pattern.Successes++;
            pattern.AvgScoreDelta = pattern.AvgScoreDelta * 0.9 + entry.ScoreDelta * 0.1;
            pattern.Confidence = (double)pattern.Successes / pattern.TotalTrials;
            pattern.LastUpdated = DateTime.Now;

            if (entry.ScoreDelta > 0 && (pattern.BestContext == "" || entry.ScoreDelta > pattern.AvgScoreDelta))
                pattern.BestContext = entry.SystemContext;
            if (entry.ScoreDelta < 0 && (pattern.WorstContext == "" || entry.ScoreDelta < pattern.AvgScoreDelta))
                pattern.WorstContext = entry.SystemContext;

            _learnedPatterns[key] = pattern;
        }

        // === Query Knowledge Base (time-weighted) ===
        public double QueryActionEffectiveness(string action, string context)
        {
            lock (_kbLock)
            {
                _kbTotalQueries++;
                double weightedScore = 0;
                double totalWeight = 0;
                double halfLifeHours = 24.0;

                foreach (var entry in _knowledgeBase)
                {
                    if (entry.Action == action)
                    {
                        double hoursAge = (DateTime.Now - entry.Timestamp).TotalHours;
                        double weight = Math.Exp(-hoursAge / halfLifeHours);
                        weightedScore += entry.ScoreDelta * weight;
                        totalWeight += weight;
                    }
                }

                return totalWeight > 0.001 ? weightedScore / totalWeight : 0;
            }
        }

        // === Get Best Action for Context ===
        public string GetBestActionForContext(string category)
        {
            lock (_kbLock)
            {
                string bestAction = "";
                double bestDelta = double.MinValue;

                foreach (var kv in _learnedPatterns)
                {
                    if (kv.Value.Category == category && kv.Value.TotalTrials >= 3)
                    {
                        if (kv.Value.AvgScoreDelta > bestDelta)
                        {
                            bestDelta = kv.Value.AvgScoreDelta;
                            bestAction = kv.Value.Action;
                        }
                    }
                }

                return bestAction;
            }
        }

        // === Get Pattern Confidence ===
        public double GetPatternConfidence(string action, string category)
        {
            lock (_kbLock)
            {
                string key = action + "_" + category;
                KnowledgePattern pattern;
                if (_learnedPatterns.TryGetValue(key, out pattern))
                {
                    return pattern.Confidence;
                }
                return 0.5;
            }
        }

        // === Update Knowledge Base (called from Tick) ===
        public void UpdateKnowledgeBase()
        {
            if ((DateTime.Now - _lastKBUpdate).TotalSeconds < KB_UPDATE_INTERVAL_SECONDS) return;

            // Prune old entries with low confidence
            if (_knowledgeBase.Count > MAX_KNOWLEDGE_ENTRIES * 0.8)
            {
                _knowledgeBase.Sort(delegate(KnowledgeEntry a, KnowledgeEntry b)
                {
                    return b.Timestamp.CompareTo(a.Timestamp);
                });
                int removeCount = _knowledgeBase.Count - (int)(MAX_KNOWLEDGE_ENTRIES * 0.6);
                if (removeCount > 0) _knowledgeBase.RemoveRange(0, removeCount);
            }

            // Log knowledge base stats
            AddLog("KB", "KnowledgeBase",
                string.Format("KB stats: {0} entries, {1} patterns, {2} learnings, {3} queries",
                _knowledgeBase.Count, _learnedPatterns.Count, _kbTotalLearnings, _kbTotalQueries));

            _lastKBUpdate = DateTime.Now;
        }

        // === Save Knowledge Base to Disk ===
        public void SaveKnowledgeBase()
        {
            try
            {
                string kbPath = Path.Combine(_dataDir, "knowledge_base.txt");
                var sb = new StringBuilder();
                foreach (var entry in _knowledgeBase)
                {
                    sb.AppendLine(string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}",
                        entry.Timestamp.Ticks, entry.Action, entry.Category,
                        entry.Success ? 1 : 0, entry.ScoreDelta, entry.Confidence, entry.SystemContext));
                }
                File.WriteAllText(kbPath, sb.ToString());
            }
            catch (Exception ex) { LogError("SaveKnowledgeBase", ex); }
        }

        // === Load Knowledge Base from Disk ===
        public void LoadKnowledgeBase()
        {
            try
            {
                string kbPath = Path.Combine(_dataDir, "knowledge_base.txt");
                if (!File.Exists(kbPath)) return;
                string[] lines = File.ReadAllLines(kbPath);
                foreach (string line in lines)
                {
                    string[] parts = line.Split('|');
                    if (parts.Length >= 5)
                    {
                        var entry = new KnowledgeEntry();
                        entry.Timestamp = new DateTime(long.Parse(parts[0]));
                        entry.Action = parts[1];
                        entry.Category = parts[2];
                        entry.Success = parts[3] == "1";
                        entry.ScoreDelta = double.Parse(parts[4]);
                        if (parts.Length >= 6) entry.Confidence = double.Parse(parts[5]);
                        if (parts.Length >= 7) entry.SystemContext = parts[6];
                        _knowledgeBase.Add(entry);
                    }
                }
                AddLog("KB", "KnowledgeBase", "Loaded " + _knowledgeBase.Count + " entries from disk");
            }
            catch (Exception ex) { LogError("LoadKnowledgeBase", ex); }
        }
    }
}
