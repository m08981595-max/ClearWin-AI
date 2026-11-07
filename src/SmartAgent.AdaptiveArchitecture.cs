using System;
using System.Collections.Generic;
using System.Linq;

namespace ClearWin
{
    public partial class SmartAgent
    {
        private Dictionary<string, AnalyzerFitness> _analyzerFitness = new Dictionary<string, AnalyzerFitness>();
        private DateTime _lastArchitectureUpdate = DateTime.MinValue;
        private const int ARCHITECTURE_INTERVAL_SECONDS = 300;
        private List<ArchitectureChange> _architectureChanges = new List<ArchitectureChange>();

        public List<AnalyzerFitness> GetAnalyzerFitness() { return new List<AnalyzerFitness>(_analyzerFitness.Values); }
        public List<ArchitectureChange> GetArchitectureChanges() { return new List<ArchitectureChange>(_architectureChanges); }

        public void InitializeAdaptiveArchitecture()
        {
            string[] analyzers = { "Thermal", "Memory", "CPU", "Frame", "Background", "Power", "Affinity" };
            foreach (string name in analyzers)
            {
                _analyzerFitness[name] = new AnalyzerFitness();
                _analyzerFitness[name].Name = name;
                _analyzerFitness[name].Weight = 1.0;
                _analyzerFitness[name].TotalRuns = 0;
                _analyzerFitness[name].UsefulRuns = 0;
                _analyzerFitness[name].AvgImpact = 0;
                _analyzerFitness[name].Enabled = true;
            }
            AddLog("ARCH", "AdaptiveArchitecture", "Initialized " + analyzers.Length + " analyzer fitness trackers");
        }

        public void UpdateAnalyzerFitness(string analyzerName, bool producedDecision, double impact)
        {
            AnalyzerFitness fitness;
            if (!_analyzerFitness.TryGetValue(analyzerName, out fitness)) return;

            fitness.TotalRuns++;
            if (producedDecision && impact > 0)
            {
                fitness.UsefulRuns++;
                fitness.AvgImpact = fitness.AvgImpact * 0.9 + impact * 0.1;
            }
            fitness.LastRun = DateTime.Now;
            fitness.SuccessRate = fitness.TotalRuns > 0 ? (double)fitness.UsefulRuns / fitness.TotalRuns : 0;

            if (fitness.TotalRuns > 10)
            {
                if (fitness.SuccessRate < 0.1)
                {
                    double oldWeight = fitness.Weight;
                    fitness.Weight = Math.Max(0.1, fitness.Weight * 0.7);
                    if (oldWeight != fitness.Weight)
                    {
                        RecordArchitectureChange(analyzerName, "Weight reduced", oldWeight, fitness.Weight);
                    }
                }
                else if (fitness.SuccessRate > 0.5)
                {
                    fitness.Weight = Math.Min(2.0, fitness.Weight * 1.05);
                }
            }

            if (fitness.Weight < 0.15 && fitness.TotalRuns > 20 && !IsCriticalAnalyzer(analyzerName))
            {
                fitness.Enabled = false;
                RecordArchitectureChange(analyzerName, "Auto-disabled", fitness.Weight, 0);
                AddLog("ARCH", "AdaptiveArchitecture", "Auto-disabled analyzer: " + analyzerName + " (weight=" + string.Format("{0:F2}", fitness.Weight) + ")");
            }
        }

        private bool IsCriticalAnalyzer(string name)
        {
            return name == "Thermal" || name == "Memory";
        }

        private void RecordArchitectureChange(string analyzer, string reason, double oldValue, double newValue)
        {
            var change = new ArchitectureChange();
            change.Timestamp = DateTime.Now;
            change.Analyzer = analyzer;
            change.Reason = reason;
            change.OldValue = oldValue;
            change.NewValue = newValue;
            _architectureChanges.Add(change);
            if (_architectureChanges.Count > 50) _architectureChanges.RemoveAt(0);
        }

        public double GetAnalyzerWeight(string analyzerName)
        {
            AnalyzerFitness fitness;
            if (_analyzerFitness.TryGetValue(analyzerName, out fitness))
            {
                return fitness.Enabled ? fitness.Weight : 0;
            }
            return 1.0;
        }

        public void RunArchitectureUpdate()
        {
            if ((DateTime.Now - _lastArchitectureUpdate).TotalSeconds < ARCHITECTURE_INTERVAL_SECONDS) return;

            foreach (var kv in _analyzerFitness)
            {
                if (kv.Value.TotalRuns > 50 && kv.Value.SuccessRate < 0.05 && !IsCriticalAnalyzer(kv.Key))
                {
                    kv.Value.Weight = Math.Max(0.05, kv.Value.Weight * 0.5);
                    RecordArchitectureChange(kv.Key, "Periodic review - very low fitness", kv.Value.Weight, kv.Value.Weight);
                }
            }

            int enabledCount = 0;
            foreach (var kv in _analyzerFitness)
            {
                if (kv.Value.Enabled) enabledCount++;
            }
            AddLog("ARCH", "AdaptiveArchitecture",
                string.Format("Architecture review: {0}/{1} analyzers enabled", enabledCount, _analyzerFitness.Count));

            _lastArchitectureUpdate = DateTime.Now;
        }
    }
}
