using System;
using System.Collections.Generic;

namespace ClearWin
{
    // =====================================================================
    // HONEST METRICS + VALIDATION + DEGRADATION MONITOR
    // The legacy SuccessRate always reported 100% because TotalInferences was
    // never incremented. This module:
    //   1. Feeds real measured outcomes into the success-rate counters.
    //   2. Computes a held-out validation accuracy per ensemble (a real
    //      generalization estimate, reported alongside the training acc).
    //   3. Snapshots the best-seen weights and, if the real-world success
    //      rate collapses or validation accuracy degrades, automatically
    //      rolls back to the last good weights and lowers the learning rate.
    // =====================================================================
    public partial class SmartAgent
    {
        private double _valLossLast = 0;
        private double _valAccLast = 0;
        private double _valAccBest = 0;
        private double _genGapLast = 0;
        private readonly Queue<double> _recentRealOutcomes = new Queue<double>();
        private const int ROLLING_WINDOW = 50;
        private List<KeyValuePair<string, MLNeuralNetwork[]>> _lastGoodWeights;
        private bool _degraded = false;
        private int _degradeCount = 0;
        private int _degradeCooldownTicks = 0;
        private DateTime _lastValTime = DateTime.MinValue;
        private int _totalRealInferences = 0;
        private int _totalRealFailures = 0;

        public bool IsDegraded { get { return _degraded; } }
        public int GetDegradeCount() { return _degradeCount; }
        public double GetValidationAccuracy() { return _valAccLast; }
        public double GetValidationBest() { return _valAccBest; }
        public double GetGeneralizationGap() { return _genGapLast; }

        public void RecordRealOutcomeMetric(double score)
        {
            _totalRealInferences++;
            if (score < 0.45) _totalRealFailures++;
            _recentRealOutcomes.Enqueue(score);
            if (_recentRealOutcomes.Count > ROLLING_WINDOW) _recentRealOutcomes.Dequeue();
        }

        // Honest real-world success rate over the last ROLLING_WINDOW outcomes.
        // Returns -1 when there is not enough evidence yet.
        public double GetRealSuccessRate()
        {
            if (_recentRealOutcomes.Count < 5) return -1.0;
            int pos = 0;
            foreach (double s in _recentRealOutcomes) if (s >= 0.5) pos++;
            return (double)pos / _recentRealOutcomes.Count;
        }

        public double GetTotalInferenceSuccessRate()
        {
            if (_totalRealInferences == 0) return -1.0;
            return (double)(_totalRealInferences - _totalRealFailures) / _totalRealInferences;
        }

        public string GetMLHonestMetrics()
        {
            double real = GetRealSuccessRate();
            return string.Format(
                "Honest metrics -> Train acc: {0:F1}% | Val acc: {1:F1}% (best {2:F1}%) | Generalization gap: {3:F3} | Real success (last {4}): {5} | Degraded: {6}",
                _trainAccLast * 100, _valAccLast * 100, _valAccBest * 100, _genGapLast,
                _recentRealOutcomes.Count, real < 0 ? "n/a" : (real * 100).ToString("F0") + "%",
                _degraded ? "YES (" + _degradeCount + " rollbacks)" : "No");
        }

        private void ComputeValidationMetrics()
        {
            if ((DateTime.Now - _lastValTime).TotalSeconds < 15) return;
            if (_mlTB.Count < 12) return;
            _lastValTime = DateTime.Now;
            try
            {
                int n = _mlTB.Count;
                int valN = Math.Max(2, n / 5);
                double loss = 0;
                int count = 0;
                for (int i = n - valN; i < n; i++)
                {
                    MLTrainingSample s = _mlTB[i];
                    MLEnsemble e;
                    if (!_mlEns.TryGetValue(s.ActionTaken, out e) || e.TS < 3) continue;
                    double pred = e.Predict(_fn.NormalizeReadOnly(s.Features.ToArray()));
                    loss += Math.Abs(pred - s.OutcomeScore);
                    count++;
                }
                if (count == 0) return;
                double avgLoss = loss / count;
                double acc = 1.0 - Math.Min(1.0, avgLoss);
                _valLossLast = avgLoss;
                _valAccLast = acc;
                _genGapLast = Math.Abs(_trainAccLast - acc);
                if (acc >= 0.55 && acc >= _valAccBest)
                {
                    _valAccBest = acc;
                    SnapshotGoodWeights();
                }
            }
            catch (Exception ex) { LogError("ML.Validation", ex); }
        }

        private void SnapshotGoodWeights()
        {
            try
            {
                var snap = new List<KeyValuePair<string, MLNeuralNetwork[]>>();
                foreach (var kv in _mlEns)
                {
                    MLNeuralNetwork[] nets = new MLNeuralNetwork[kv.Value.Nets.Length];
                    for (int i = 0; i < nets.Length; i++) nets[i] = CloneNet(kv.Value.Nets[i]);
                    snap.Add(new KeyValuePair<string, MLNeuralNetwork[]>(kv.Key, nets));
                }
                _lastGoodWeights = snap;
            }
            catch (Exception ex) { LogError("ML.Snapshot", ex); }
        }

        private MLNeuralNetwork CloneNet(MLNeuralNetwork src)
        {
            MLNeuralNetwork c = new MLNeuralNetwork();
            c.TS = src.TS;
            c.Acc = src.Acc;
            c.LR = src.LR;
            c.LU = src.LU;
            Array.Copy(src.W1, c.W1, src.W1.Length);
            Array.Copy(src.B1, c.B1, src.B1.Length);
            Array.Copy(src.W2, c.W2, src.W2.Length);
            Array.Copy(src.B2, c.B2, src.B2.Length);
            Array.Copy(src.W3, c.W3, src.W3.Length);
            c.B3 = src.B3;
            Array.Copy(src.BN1G, c.BN1G, src.BN1G.Length);
            Array.Copy(src.BN1B, c.BN1B, src.BN1B.Length);
            Array.Copy(src.BN2G, c.BN2G, src.BN2G.Length);
            Array.Copy(src.BN2B, c.BN2B, src.BN2B.Length);
            Array.Copy(src.BN1RM, c.BN1RM, src.BN1RM.Length);
            Array.Copy(src.BN1RV, c.BN1RV, src.BN1RV.Length);
            Array.Copy(src.BN2RM, c.BN2RM, src.BN2RM.Length);
            Array.Copy(src.BN2RV, c.BN2RV, src.BN2RV.Length);
            Array.Copy(src.MW1, c.MW1, src.MW1.Length);
            Array.Copy(src.MB1, c.MB1, src.MB1.Length);
            Array.Copy(src.MW2, c.MW2, src.MW2.Length);
            Array.Copy(src.MB2, c.MB2, src.MB2.Length);
            Array.Copy(src.MW3, c.MW3, src.MW3.Length);
            c.MB3 = src.MB3;
            Array.Copy(src.VW1, c.VW1, src.VW1.Length);
            Array.Copy(src.VB1, c.VB1, src.VB1.Length);
            Array.Copy(src.VW2, c.VW2, src.VW2.Length);
            Array.Copy(src.VB2, c.VB2, src.VB2.Length);
            Array.Copy(src.VW3, c.VW3, src.VW3.Length);
            c.VB3 = src.VB3;
            Array.Copy(src.MBN1G, c.MBN1G, src.MBN1G.Length);
            Array.Copy(src.VBN1G, c.VBN1G, src.VBN1G.Length);
            Array.Copy(src.MBN1B, c.MBN1B, src.MBN1B.Length);
            Array.Copy(src.VBN1B, c.VBN1B, src.VBN1B.Length);
            Array.Copy(src.MBN2G, c.MBN2G, src.MBN2G.Length);
            Array.Copy(src.VBN2G, c.VBN2G, src.VBN2G.Length);
            Array.Copy(src.MBN2B, c.MBN2B, src.MBN2B.Length);
            Array.Copy(src.VBN2B, c.VBN2B, src.VBN2B.Length);
            return c;
        }

        private void RestoreGoodWeights()
        {
            if (_lastGoodWeights == null || _lastGoodWeights.Count == 0) return;
            try
            {
                _mlEns.Clear();
                foreach (var kv in _lastGoodWeights)
                {
                    MLEnsemble e = new MLEnsemble(kv.Key);
                    for (int i = 0; i < e.Nets.Length && i < kv.Value.Length; i++) e.Nets[i] = kv.Value[i];
                    _mlEns[kv.Key] = e;
                }
                _degraded = true;
                _degradeCount++;
                foreach (var key in new List<string>(_aLR.Keys)) _aLR[key] = 0.05;
                AddLog("WARN", "Degradation", "Rolled back to last good weights after degradation");
            }
            catch (Exception ex) { LogError("ML.Restore", ex); }
        }

        private void RunDegradationMonitor()
        {
            try
            {
                if (_degradeCooldownTicks > 0) { _degradeCooldownTicks--; return; }
                double real = GetRealSuccessRate();
                int evals = GetEvaluationsCompleted();
                if (evals >= 10 && real >= 0 && real < 0.4)
                {
                    RestoreGoodWeights();
                    _adaptiveLearningRate = Math.Max(0.02, _adaptiveLearningRate * 0.5);
                    _degradeCooldownTicks = 60;
                    AddLog("WARN", "Degradation",
                        string.Format("Real success rate {0:F0}% over {1} outcomes - lowered lr to {2:F3} and recovered",
                        real * 100, evals, _adaptiveLearningRate));
                }
                else if (_degraded && real >= 0.55)
                {
                    _degraded = false;
                    AddLog("INFO", "Degradation", "Recovered from degradation - real success back to " + (real * 100).ToString("F0") + "%");
                }
            }
            catch (Exception ex) { LogError("DegradationMonitor", ex); }
        }
    }
}
