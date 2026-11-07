using System;
using System.Collections.Generic;

namespace ClearWin
{
    // =====================================================================
    // REAL OUTCOME MEASUREMENT ENGINE
    // Replaces the old self-referential reward (agent guessing its own score)
    // with a measured reward: after an action executes we sample the real
    // system for a window, then compute an objective reward from measured
    // deltas (FPS, 1% low, temperature, RAM, stability, battery drain).
    // The measured outcome feeds both the neural ensemble (ML) and the real
    // state-aware Q-learning (RL2).
    // =====================================================================
    public class ActionOutcomeRecord
    {        public string Action;
        public bool ExecutedSuccessfully;
        public DateTime StartTime;
        public double BaselineCpu;
        public double BaselineRam;
        public double BaselineTemp;
        public double BaselineFps;
        public double BaselineFps1Low;
        public double BaselineStability;
        public double BaselineBattery;
        public bool IsGame;
        public bool OnBattery;
        public string StartStateKey;
        // Feature vector captured right before THIS action executed. Frozen here
        // so a burst of actions (full-opt sweep) can't overwrite each other's
        // ML training sample features via the shared _preAF.
        public MLFeatureVector PreFeatures;
        // Counterfactual baselines: the pre-action drift the system was ALREADY
        // heading towards. Reward uses the excess change beyond this drift.
        public double BaselineFpsSlope;
        public double BaselineTempSlope;
        public double BaselineRamSlope;
        public double WindowSeconds;
        public int SampleCount;
        public double SumFps;
        public double SumFps2;
        public int FpsSamples;
        public double SumFps1Low;
        public double SumCpu;
        public int CpuSamples;
        public double SumTemp;
        public double SumStability;
        public double SumRam;
        public double SumBattery;
        public bool Finalized;
        public bool Unreliable;
    }

    // Measured per-metric effect of an action across real outcome windows.
    // Means of these sums become the digital twin's calibrated predictions.
    public class TwinCalibration
    {
        public int Count;
        public double SumRamFreedGB;    // positive = freed memory
        public double SumCpuReduced;    // positive = CPU went down
        public double SumTempCooled;    // positive = cooler
        public double SumFpsGain;       // positive = FPS went up
        public double SumStabilityGain; // positive = stability went up
    }

    public partial class SmartAgent
    {
        private const double REWARD_WINDOW_SECONDS = 20.0;
        // A reward is only attributable when the measurement window was calm:
        // high frame-to-frame variance (FPS coefficient of variation) means the
        // observed delta is dominated by noise, not by the action, so we refuse
        // to credit or blame the action for it.
        private const double REWARD_FPS_CV_LIMIT = 0.45;
        private ActionOutcomeRecord _outcomeRecord;
        private double _objectiveRewardTotal;
        private int _objectiveRewardCount;
        private int _evaluationsCompleted;

        // Per-action measured reward history (drives calibrated abstention).
        private readonly Dictionary<string, double> _actionRewardSum = new Dictionary<string, double>();
        private readonly Dictionary<string, int> _actionRewardCount = new Dictionary<string, int>();
        // Per-source measured reliability (drives adaptive arbitration trust).
        private readonly Dictionary<string, int> _sourceTotal = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _sourceGood = new Dictionary<string, int>();
        private const int ABSTAIN_MIN_SAMPLES = 3;
        private const double ABSTAIN_REWARD_THRESHOLD = 0.02;

        // ===== Digital Twin calibration (measured effect per action) =====
        // Real outcome windows (FinalizeOutcomeRecord) accumulate the measured
        // per-metric effect of each action. SimulateDecision blends these
        // calibrated means with its linear heuristics, so predictions converge
        // on the machine's actual behavior instead of hard-coded guesses.
        private readonly Dictionary<string, TwinCalibration> _twinCalibration = new Dictionary<string, TwinCalibration>();

        public int GetTwinCalibrationSamples(string action)
        {
            TwinCalibration c;
            return _twinCalibration.TryGetValue(action, out c) ? c.Count : 0;
        }

        public string GetTwinCalibrationStatus()
        {
            if (_twinCalibration.Count == 0) return "Digital Twin: no measured calibration yet (heuristics active)";
            int bestAction = 0;
            foreach (var kv in _twinCalibration) if (kv.Value.Count > bestAction) bestAction = kv.Value.Count;
            return string.Format("Digital Twin: {0} actions calibrated (max {1} samples)", _twinCalibration.Count, bestAction);
        }

        private void RecordCalibratedTwinEffect(string action,
            double ramFreedGB, double cpuReduced, double tempCooled, double fpsGain, double stabilityGain)
        {
            if (string.IsNullOrEmpty(action)) return;
            TwinCalibration c;
            if (!_twinCalibration.TryGetValue(action, out c))
            {
                c = new TwinCalibration();
                _twinCalibration[action] = c;
            }
            c.Count++;
            c.SumRamFreedGB += ramFreedGB;
            c.SumCpuReduced += cpuReduced;
            c.SumTempCooled += tempCooled;
            c.SumFpsGain += fpsGain;
            c.SumStabilityGain += stabilityGain;
        }

        // Blend a calibrated mean toward the linear heuristic. With zero samples
        // the heuristic is used verbatim; trust grows ~linearly, reaching ~50%
        // weight at 3 samples and ~90% at 27 samples (calibration = ground truth).
        private static double BlendTwinCalibration(TwinCalibration c, double sum, double heuristic)
        {
            if (c == null || c.Count <= 0) return heuristic;
            double cal = sum / c.Count;
            double blend = c.Count / (c.Count + 3.0);
            return heuristic * (1.0 - blend) + cal * blend;
        }

        // Counterfactual trend baseline: EMA of the system's natural drift in
        // fps / temperature / RAM per second, captured while no action is being
        // measured. Difference-in-differences uses it to subtract what would
        // have happened anyway, isolating the action's real causal effect.
        private double _trendFpsSlope = 0;
        private double _trendTempSlope = 0;
        private double _trendRamSlope = 0;
        private bool _trendHasSample = false;
        private double _trendLastFps = 0;
        private double _trendLastTemp = 0;
        private double _trendLastRam = 0;
        private DateTime _trendLastTime = DateTime.MinValue;
        private const double TREND_EMA_ALPHA = 0.15;
        private const double TREND_MAX_GAP_SECONDS = 3.0;

        public int GetEvaluationsCompleted() { return _evaluationsCompleted; }
        public double GetAverageObjectiveReward()
        {
            return _objectiveRewardCount > 0 ? _objectiveRewardTotal / _objectiveRewardCount : 0;
        }

        // Measured natural-drift accessors (transparency + diagnostics).
        public double GetMeasuredFpsDrift() { return _trendFpsSlope; }
        public double GetMeasuredTempDrift() { return _trendTempSlope; }
        public double GetMeasuredRamDrift() { return _trendRamSlope; }

        public string GetCounterfactualStatus()
        {
            return string.Format("drift fps={0:F2}/s temp={1:F3}C/s ram={2:F3}%/s | measured sources: {3} | abstentions: {4}",
                _trendFpsSlope, _trendTempSlope, _trendRamSlope, _sourceTotal.Count, _abstentions);
        }

        public double GetExpectedReward(string action)
        {
            int c;
            if (!_actionRewardCount.TryGetValue(action, out c) || c == 0) return 0;
            double s;
            _actionRewardSum.TryGetValue(action, out s);
            return s / c;
        }

        public int GetActionRewardSampleCount(string action)
        {
            int c;
            return _actionRewardCount.TryGetValue(action, out c) ? c : 0;
        }

        public double GetSourceTrust(string source)
        {
            string src = string.IsNullOrEmpty(source) ? "Unknown" : source;
            int t, g;
            if (_sourceTotal.TryGetValue(src, out t) && t >= ABSTAIN_MIN_SAMPLES)
            {
                _sourceGood.TryGetValue(src, out g);
                return (double)g / t;
            }
            return 1.0; // neutral until enough measured evidence
        }

        public int GetTrustedSourceCount() { return _sourceTotal.Count; }

        // Called every Tick while idle: learns the natural drift the system was
        // following BEFORE the next action executes. Frozen at StartActionEvaluation.
        private void UpdateTrendBaseline()
        {
            // Simulated state must not poison the natural-drift counterfactual
            // that real reward computation relies on.
            if (_simulationMode) return;
            double fps = _smoothFps;
            double temp = _smoothTemp;
            double ram = _smoothRam;
            DateTime now = DateTime.Now;
            if (_trendHasSample)
            {
                double dt = (now - _trendLastTime).TotalSeconds;
                if (dt > 0.001 && dt <= TREND_MAX_GAP_SECONDS)
                {
                    double fpsSlope = (fps - _trendLastFps) / dt;
                    double tempSlope = (temp - _trendLastTemp) / dt;
                    double ramSlope = (ram - _trendLastRam) / dt;
                    _trendFpsSlope = _trendFpsSlope * (1.0 - TREND_EMA_ALPHA) + fpsSlope * TREND_EMA_ALPHA;
                    _trendTempSlope = _trendTempSlope * (1.0 - TREND_EMA_ALPHA) + tempSlope * TREND_EMA_ALPHA;
                    _trendRamSlope = _trendRamSlope * (1.0 - TREND_EMA_ALPHA) + ramSlope * TREND_EMA_ALPHA;
                }
                else if (dt > TREND_MAX_GAP_SECONDS)
                {
                    _trendFpsSlope = 0;
                    _trendTempSlope = 0;
                    _trendRamSlope = 0;
                }
            }
            _trendHasSample = true;
            _trendLastFps = fps;
            _trendLastTemp = temp;
            _trendLastRam = ram;
            _trendLastTime = now;
        }

        // Called when a decision actually executes. Begins measuring the real
        // effect of the action over the next REWARD_WINDOW_SECONDS.
        public void StartActionEvaluation(string action, bool success)
        {
            // Simulation mode uses a synthetic state: opening a reward window here
            // would pollute the real Q-tables / ML replay buffer / reward stats.
            if (_simulationMode) return;
            try
            {
                if (_outcomeRecord != null && !_outcomeRecord.Finalized)
                {
                    FinalizeOutcomeRecord(true);
                }
                var rec = new ActionOutcomeRecord();
                rec.Action = action;
                rec.ExecutedSuccessfully = success;
                rec.StartTime = DateTime.Now;
                rec.BaselineCpu = _smoothCpu;
                rec.BaselineRam = _smoothRam;
                rec.BaselineTemp = _smoothTemp;
                rec.BaselineFps = _smoothFps;
                rec.BaselineStability = _currentState != null ? _currentState.FpsStability : 0;
                rec.BaselineFps1Low = _currentState != null ? _currentState.Fps1Low : 0;
                rec.BaselineBattery = _batteryPercent;
                rec.IsGame = _currentState != null && _currentState.IsGame;
                rec.OnBattery = _isOnBattery;
                rec.StartStateKey = GetCurrentRLStateKey();
                rec.BaselineFpsSlope = _trendFpsSlope;
                rec.BaselineTempSlope = _trendTempSlope;
                rec.BaselineRamSlope = _trendRamSlope;
                try { rec.PreFeatures = _preAF != null ? _preAF : BuildFV(); }
                catch { rec.PreFeatures = null; }
                _outcomeRecord = rec;
                AddLog("REWARD", "Outcome", "Measuring real outcome of " + action + " (trend fps=" + _trendFpsSlope.ToString("F2") + "/s temp=" + _trendTempSlope.ToString("F3") + "/s)");
            }
            catch (Exception ex) { LogError("StartActionEvaluation", ex); }
        }

        // Called every Tick while an outcome is being measured.
        public void TickOutcomeEvaluation()
        {
            try
            {
                // Never sample or finalize measured outcomes from a simulated state.
                if (_simulationMode) { _outcomeRecord = null; return; }
                if (_outcomeRecord == null)
                {
                    UpdateTrendBaseline();
                    return;
                }
                if (_outcomeRecord.Finalized) return;
                ActionOutcomeRecord rec = _outcomeRecord;
                rec.SampleCount++;
                rec.SumFps += _smoothFps;
                rec.SumFps2 += _smoothFps * _smoothFps;
                rec.FpsSamples++;
                rec.SumFps1Low += _currentState != null ? _currentState.Fps1Low : 0;
                rec.SumCpu += _smoothCpu;
                rec.CpuSamples++;
                rec.SumTemp += _smoothTemp;
                rec.SumStability += _currentState != null ? _currentState.FpsStability : 0;
                rec.SumRam += _smoothRam;
                rec.SumBattery += _batteryPercent;
                if ((DateTime.Now - rec.StartTime).TotalSeconds >= REWARD_WINDOW_SECONDS)
                {
                    FinalizeOutcomeRecord(false);
                }
            }
            catch (Exception ex) { LogError("TickOutcomeEvaluation", ex); }
        }

        private void FinalizeOutcomeRecord(bool force)
        {
            ActionOutcomeRecord rec = _outcomeRecord;
            if (rec == null || rec.Finalized) return;
            // Defense in depth: never let a simulated window write real learning.
            if (_simulationMode) { _outcomeRecord = null; return; }
            rec.Finalized = true;
            try
            {
                double winSec = (DateTime.Now - rec.StartTime).TotalSeconds;
                rec.WindowSeconds = winSec > 0.001 ? winSec : REWARD_WINDOW_SECONDS;
                if (rec.SampleCount < 2)
                {
                    RecordImmediateOutcome(rec);
                }
                else
                {
                    double fpsAfter = rec.SumFps / rec.SampleCount;
                    double fps1LowAfter = rec.SumFps1Low / rec.SampleCount;
                    double tempAfter = rec.SumTemp / rec.SampleCount;
                    double stabAfter = rec.SumStability / rec.SampleCount;
                    double ramAfter = rec.SumRam / rec.SampleCount;
                    double batteryAfter = rec.SumBattery / rec.SampleCount;

                    // Reliability gate: during a game the 20s window is only
                    // attributable if FPS stayed steady. A noisy window (high CV)
                    // means the delta is noise, not effect -- skip the reward.
                    double fpsCv = 0;
                    if (rec.IsGame && rec.FpsSamples >= 4 && fpsAfter > 5)
                    {
                        double mean = fpsAfter;
                        double vari = rec.SumFps2 / rec.FpsSamples - mean * mean;
                        if (vari < 0) vari = 0;
                        fpsCv = Math.Sqrt(vari) / mean;
                        if (fpsCv > REWARD_FPS_CV_LIMIT) rec.Unreliable = true;
                    }
                    if (rec.Unreliable)
                    {
                        AddLog("REWARD", "Outcome",
                            string.Format("{0}: window unreliable (FPS CV {1:F2} > {2:F2}), reward skipped", rec.Action, fpsCv, REWARD_FPS_CV_LIMIT));
                        return;
                    }

                    double r = ComputeObjectiveReward(rec, fpsAfter, fps1LowAfter, tempAfter, stabAfter, ramAfter, batteryAfter, rec.WindowSeconds);
                    string nextKey = GetCurrentRLStateKey();
                    RecordRLExperience(rec.StartStateKey, rec.Action, r, nextKey);

                    double score = Clamp01(0.5 + r * 0.5);
                    // Single Source of Truth: the learning evidence is stored via
                    // the sample-only recorder (no metric counter hit); the
                    // measured outcome metric below is THIS outcome's one and
                    // only RecordRealOutcomeMetric call. Using the public
                    // RecordActionOutcome here would count the outcome twice.
                    RecordActionOutcomeSample(rec.Action, score, rec.PreFeatures);
                    RecordRealOutcomeMetric(score);

                    // Calibrate the digital twin with the real measured effect.
                    // Deltas are corrected by the counterfactual drift so the twin
                    // learns the action's causal effect, not ambient system noise.
                    double cpuAfter = rec.CpuSamples > 0 ? rec.SumCpu / rec.CpuSamples : rec.BaselineCpu;
                    double totalRamGB = _currentState != null && _currentState.TotalRamGB > 0 ? _currentState.TotalRamGB : 16.0;
                    double ramPctFreed = (rec.BaselineRam - ramAfter) + rec.BaselineRamSlope * rec.WindowSeconds;
                    double ramFreedGB = Math.Max(0, ramPctFreed / 100.0 * totalRamGB);
                    double cpuReduced = rec.BaselineCpu - cpuAfter;
                    double tempCooled = (rec.BaselineTemp - tempAfter) + rec.BaselineTempSlope * rec.WindowSeconds;
                    double fpsGain = (fpsAfter - rec.BaselineFps) - rec.BaselineFpsSlope * rec.WindowSeconds;
                    double stabilityGain = stabAfter - rec.BaselineStability;
                    RecordCalibratedTwinEffect(rec.Action, ramFreedGB, cpuReduced, tempCooled, fpsGain, stabilityGain);

                    RecordActionReward(rec.Action, r);
                    RecordSourceOutcome(GetSourceForAction(rec.Action), r);

                    _objectiveRewardTotal += r;
                    _objectiveRewardCount++;
                    _evaluationsCompleted++;
                    try { Events.Publish(new RewardRecordedEvent(rec.Action, r, r > 0)); }
                    catch (Exception ex) { LogError("Bus.Reward", ex); }
                    AddLog("REWARD", "Outcome",
                        string.Format("{0}: reward={1:F3} samples={2} fps {3:F0}->{4:F0} (drift {5:F2}/s) temp {6:F0}->{7:F0} (drift {8:F3}/s) ram {9:F0}->{10:F0}",
                        rec.Action, r, rec.SampleCount, rec.BaselineFps, fpsAfter, rec.BaselineFpsSlope,
                        rec.BaselineTemp, tempAfter, rec.BaselineTempSlope, rec.BaselineRam, ramAfter));
                }
            }
            catch (Exception ex) { LogError("FinalizeOutcomeRecord", ex); }
            finally { _outcomeRecord = null; }
        }

        private void RecordImmediateOutcome(ActionOutcomeRecord rec)
        {
            double r = rec.ExecutedSuccessfully ? 0.1 : -0.5;
            RecordRLExperience(rec.StartStateKey, rec.Action, r, GetCurrentRLStateKey());
            double score = Clamp01(0.5 + r * 0.5);
            // Single Source of Truth: same as the measured branch above -
            // sample-only storage, then exactly one metric counter hit.
            RecordActionOutcomeSample(rec.Action, score, rec.PreFeatures);
            RecordRealOutcomeMetric(score);
            RecordActionReward(rec.Action, r);
            RecordSourceOutcome(GetSourceForAction(rec.Action), r);
            _objectiveRewardTotal += r;
            _objectiveRewardCount++;
            _evaluationsCompleted++;
            try { Events.Publish(new RewardRecordedEvent(rec.Action, r, r > 0)); }
            catch (Exception ex) { LogError("Bus.Reward", ex); }
        }

        private void RecordActionReward(string action, double r)
        {
            double s;
            _actionRewardSum.TryGetValue(action, out s);
            _actionRewardSum[action] = s + r;
            int c;
            _actionRewardCount.TryGetValue(action, out c);
            _actionRewardCount[action] = c + 1;
        }

        private void RecordSourceOutcome(string source, double reward)
        {
            string src = string.IsNullOrEmpty(source) ? "Unknown" : source;
            int t;
            _sourceTotal.TryGetValue(src, out t);
            _sourceTotal[src] = t + 1;
            if (reward > 0)
            {
                int g;
                _sourceGood.TryGetValue(src, out g);
                _sourceGood[src] = g + 1;
            }
        }

        private string GetSourceForAction(string action)
        {
            string src;
            if (_actionSource != null && _actionSource.TryGetValue(action, out src)) return src;
            return "Unknown";
        }

        // Calibrated abstention: with enough measured outcomes showing an action
        // does not improve the system (mean reward below threshold), the agent
        // learns to NOT act. Thermal emergencies always bypass abstention.
        public bool ShouldAbstain(AgentDecision d)
        {
            if (d == null) return false;
            if (d.Action == "EmergencyThrottle") return false;
            if (d.Category == DecisionCategory.ThermalProtect && _smoothTemp > 90) return false;
            if (d.Source == "SmartScheduling" || d.Source == "PredictiveMaintenance" || d.Source == "UserBehaviorLearning") return false;
            int samples = GetActionRewardSampleCount(d.Action);
            if (samples < ABSTAIN_MIN_SAMPLES) return false;
            return GetExpectedReward(d.Action) < ABSTAIN_REWARD_THRESHOLD;
        }

        // Advisory / bookkeeping actions have no measurable causal effect, so we
        // never open a reward window for them (avoids polluting the reward signal).
        public static bool IsMeasurableAction(string action)
        {
            if (action == "HealthCheck" || action == "SuggestClose" || action == "StartupDisable" || action == "DiskCleanup") return false;
            if (action.StartsWith("KB_")) return false;
            if (action.StartsWith("Scheduled_")) return false;
            if (action.StartsWith("ForecastPrevent_")) return false;
            if (action.StartsWith("PredictiveMaintenance_")) return false;
            if (action.StartsWith("UserBehavior_")) return false;
            if (action.StartsWith("Meta_")) return false;
            return true;
        }

        // Objective reward from measured deltas, corrected by the counterfactual
        // drift the system was already following. Positive = real improvement.
        public static double ComputeObjectiveReward(ActionOutcomeRecord rec,
            double fpsAfter, double fps1LowAfter, double tempAfter,
            double stabAfter, double ramAfter, double batteryAfter, double windowSeconds)
        {
            double r = 0;
            if (rec.ExecutedSuccessfully) r += 0.3; else r -= 0.6;

            if (rec.IsGame && rec.BaselineFps > 5)
            {
                double expectedFpsDrift = rec.BaselineFpsSlope * windowSeconds;
                double fpsExcess = (fpsAfter - rec.BaselineFps) - expectedFpsDrift;
                double fpsGain = fpsExcess / rec.BaselineFps;
                r += Clamp(fpsGain, -0.6, 0.6) * 0.7;
                if (rec.BaselineFps1Low > 5 && fps1LowAfter > 5)
                {
                    double lowGain = (fps1LowAfter - rec.BaselineFps1Low) / rec.BaselineFps1Low;
                    r += Clamp(lowGain, -0.4, 0.4) * 0.3;
                }
            }

            double expectedTempDrift = rec.BaselineTempSlope * windowSeconds;
            double tempDelta = (rec.BaselineTemp - tempAfter) + expectedTempDrift; // positive = cooler than natural drift
            r += Clamp(tempDelta / 10.0, -0.5, 0.5) * 0.5;

            if (rec.BaselineRam > 75)
            {
                double expectedRamDrift = rec.BaselineRamSlope * windowSeconds;
                double ramDelta = (rec.BaselineRam - ramAfter) + expectedRamDrift; // positive = freed beyond natural drift
                r += Clamp(ramDelta / 20.0, -0.4, 0.4) * 0.4;
            }

            double stabDelta = stabAfter - rec.BaselineStability;
            r += Clamp(stabDelta / 40.0, -0.3, 0.3) * 0.3;

            if (rec.OnBattery && batteryAfter < rec.BaselineBattery - 1.5) r -= 0.1;

            return Clamp(r, -1.0, 1.0);
        }

        public string GetCurrentRLStateKey()
        {
            RLState s = GetCurrentRLState();
            return BuildStateKey(s);
        }

        public string GetRewardEngineStatus()
        {
            if (_outcomeRecord != null && !_outcomeRecord.Finalized)
            {
                int elapsed = (int)(DateTime.Now - _outcomeRecord.StartTime).TotalSeconds;
                return string.Format("Measuring outcome of {0}... {1}s/{2}s (fps drift {3:F2}/s)",
                    _outcomeRecord.Action, elapsed, (int)REWARD_WINDOW_SECONDS, _trendFpsSlope);
            }
            return string.Format("Evaluations: {0} | Avg objective reward: {1:F3} | Abstentions: {2}",
                _evaluationsCompleted, GetAverageObjectiveReward(), _abstentions);
        }

        private static double Clamp(double v, double lo, double hi)
        {
            return v < lo ? lo : v > hi ? hi : v;
        }

        private static double Clamp01(double v)
        {
            return v < 0.0 ? 0.0 : v > 1.0 ? 1.0 : v;
        }
    }
}
