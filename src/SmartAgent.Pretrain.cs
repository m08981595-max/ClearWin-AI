using System;
using System.Collections.Generic;

namespace ClearWin
{
    // =====================================================================
    // PRIOR KNOWLEDGE / OFFLINE PRETRAINING
    // The rule expert already encodes correct Windows optimization behavior.
    // We turn that expertise into a training set: thousands of realistic
    // system states are generated, labelled by BuildRuleDecisionFrom, and
    // used to pretrain every neural ensemble plus seed the state-aware Q
    // table. The shipped AI is therefore smart on day one and then keeps
    // adapting from real measured outcomes. Pretraining only runs when the
    // learned model is still weak (fresh install), so real user data always
    // overrides the prior over time.
    // =====================================================================
    public partial class SmartAgent
    {
        private bool _priorInitialized = false;
        private int _learningMaintTick = 0;
        private const int PRIOR_MAX_SAMPLES_PER_ACTION = 400;
        private const int PRIOR_EPOCHS = 15;
        private const int PRIOR_MAX_TB = 900;

        public bool IsPriorKnowledgeInitialized { get { return _priorInitialized; } }

        public void ForceDeepTraining(int epochs = 25, Action<string> progressCallback = null)
        {
            try
            {
                if (progressCallback != null) progressCallback("Starting comprehensive deep training (" + epochs + " epochs)...");
                AddLog("TRAIN", "DeepTrain", "Starting forced deep training across all modules...");

                var byAction = new Dictionary<string, List<MLTrainingSample>>();
                var qSeeds = new Dictionary<string, Dictionary<string, double>>();

                GeneratePriorGrid(byAction, qSeeds);

                if (progressCallback != null) progressCallback("Seeding state Q-values (" + qSeeds.Count + " state scenarios)...");
                foreach (var st in qSeeds)
                {
                    foreach (var kv in st.Value)
                    {
                        SeedStateQValue(st.Key, kv.Key, kv.Value);
                    }
                }

                int trained = 0;
                lock (_mlLock)
                {
                    foreach (var kv in byAction)
                    {
                        if (kv.Value.Count < 4) continue;
                        MLEnsemble e;
                        if (!_mlEns.TryGetValue(kv.Key, out e))
                        {
                            e = new MLEnsemble(kv.Key);
                            _mlEns[kv.Key] = e;
                        }
                        int n = kv.Value.Count;
                        int bs = Math.Min(64, n);
                        for (int ep = 0; ep < epochs; ep++)
                        {
                            for (int i = 0; i < bs; i++)
                            {
                                MLTrainingSample s = kv.Value[_mlRnd.Next(n)];
                                e.Train(_fn.NormalizeReadOnly(s.Features.ToArray()), s.OutcomeScore, 0.08);
                            }
                        }
                        trained++;
                        string msg = string.Format("Ensemble trained: {0} ({1} samples, {2} epochs)", kv.Key, n, epochs);
                        AddLog("TRAIN", "DeepTrain", msg);
                        if (progressCallback != null) progressCallback(msg);
                    }

                    SaveMLModel();
                }

                try { RunCausalAnalysis(); } catch { }
                try { UpdateToolImpactScores(); } catch { }
                try { SaveRL2Data(); } catch { }
                try { SaveData(); } catch { }

                _priorInitialized = true;
                string finishMsg = string.Format("Deep training complete: {0} ensembles trained, {1} Q-states seeded.", trained, qSeeds.Count);
                AddLog("TRAIN", "DeepTrain", finishMsg);
                if (progressCallback != null) progressCallback(finishMsg);
            }
            catch (Exception ex)
            {
                LogError("ForceDeepTraining", ex);
                if (progressCallback != null) progressCallback("Training error: " + ex.Message);
            }
        }

        public void InitializeWithPriorKnowledge()
        {
            if (_priorInitialized) return;
            if (IsGamingActive())
            {
                AddLog("PRIOR", "Pretrain", "Prior knowledge pretraining deferred during active game");
                return;
            }
            _priorInitialized = true; // attempt once
            if (!ModelNeedsPrior())
            {
                AddLog("PRIOR", "Pretrain", "Prior skipped - model already has learned data");
                return;
            }
            try
            {
                AddLog("PRIOR", "Pretrain", "Generating Windows optimization prior knowledge...");
                var byAction = new Dictionary<string, List<MLTrainingSample>>();
                var qSeeds = new Dictionary<string, Dictionary<string, double>>();

                GeneratePriorGrid(byAction, qSeeds);

                foreach (var st in qSeeds)
                {
                    foreach (var kv in st.Value)
                    {
                        SeedStateQValue(st.Key, kv.Key, kv.Value);
                    }
                }

                int trained = 0;
                lock (_mlLock)
                {
                    foreach (var kv in byAction)
                    {
                        if (kv.Value.Count < 4) continue;
                        MLEnsemble e;
                        if (!_mlEns.TryGetValue(kv.Key, out e))
                        {
                            e = new MLEnsemble(kv.Key);
                            _mlEns[kv.Key] = e;
                        }
                        int n = kv.Value.Count;
                        int bs = Math.Min(64, n);
                        for (int ep = 0; ep < PRIOR_EPOCHS; ep++)
                        {
                            for (int i = 0; i < bs; i++)
                            {
                                MLTrainingSample s = kv.Value[_mlRnd.Next(n)];
                                e.Train(_fn.NormalizeReadOnly(s.Features.ToArray()), s.OutcomeScore, 0.08);
                            }
                        }
                        trained++;
                        AddLog("PRIOR", "Pretrain", "Ensemble pretrained: " + kv.Key + " (" + n + " samples)");
                    }

                    SaveMLModel();
                }
                _priorInitialized = true;
                AddLog("PRIOR", "Pretrain",
                    string.Format("Prior knowledge initialized: {0} ensembles trained, {1} Q-states seeded, {2} total ensembles",
                    trained, qSeeds.Count, _mlEns.Count));
            }
            catch (Exception ex)
            {
                LogError("InitializeWithPriorKnowledge", ex);
                _priorInitialized = false;
            }
        }

        private bool ModelNeedsPrior()
        {
            bool anyLearned = false;
            foreach (var kv in _mlEns)
            {
                if (kv.Value.TS >= 5) { anyLearned = true; break; }
            }
            if (anyLearned) return false;
            if (_mlTB.Count >= 40) return false;
            return true;
        }

        private void GeneratePriorGrid(
            Dictionary<string, List<MLTrainingSample>> byAction,
            Dictionary<string, Dictionary<string, double>> qSeeds)
        {
            double[] cpus = new double[] { 15, 35, 55, 70, 85, 95 };
            double[] rams = new double[] { 35, 55, 70, 80, 90 };
            double[] temps = new double[] { 45, 60, 70, 78, 88 };
            double[] fpss = new double[] { 0, 25, 45, 70, 95 };
            bool[] games = new bool[] { false, true };

            int total = 0;
            for (int ci = 0; ci < cpus.Length; ci++)
            {
                for (int ri = 0; ri < rams.Length; ri++)
                {
                    for (int ti = 0; ti < temps.Length; ti++)
                    {
                        for (int fi = 0; fi < fpss.Length; fi++)
                        {
                            for (int gi = 0; gi < games.Length; gi++)
                            {
                                SystemState s = BuildSyntheticState(cpus[ci], rams[ri], temps[ti], fpss[fi], games[gi]);
                                AgentDecision d = BuildRuleDecisionFrom(s, s.CpuUsage, s.RamUsage, s.CpuTemp, false, 100);
                                if (d == null) continue;

                                MLFeatureVector fv = BuildFVFrom(s);
                                string stateKey = BuildStateKeyFor(s);

                                Dictionary<string, double> qRow;
                                if (!qSeeds.TryGetValue(stateKey, out qRow))
                                {
                                    qRow = new Dictionary<string, double>();
                                    qSeeds[stateKey] = qRow;
                                }
                                qRow[d.Action] = ClampQ(0.35 + d.Confidence * 0.55);
                                for (int a = 0; a < ML_AC.Length; a++)
                                {
                                    string act = ML_AC[a];
                                    if (act == d.Action) continue;
                                    if (!qRow.ContainsKey(act)) qRow[act] = Q_SEED_BAD;
                                }

                                if (_fn != null) _fn.Observe(fv.ToArray());
                                double target = Clamp01(0.35 + d.Confidence * 0.6);
                                AddPriorSample(byAction, d.Action, fv, target);

                                // H2 Fix: Also inject calibrated negative counter-examples (target=0.08) for competing/inapplicable
                                // actions so ensembles learn genuine state discrimination rather than all-positive bias.
                                int negAdded = 0;
                                for (int aIdx = 0; aIdx < ML_AC.Length && negAdded < 2; aIdx++)
                                {
                                    string cand = ML_AC[(total + aIdx) % ML_AC.Length];
                                    if (cand == d.Action) continue;
                                    if (!IsActionApplicableToState(cand, s))
                                    {
                                        AddPriorSample(byAction, cand, fv, 0.08);
                                        negAdded++;
                                    }
                                }
                                total++;
                            }
                        }
                    }
                }
            }
            AddLog("PRIOR", "Pretrain", "Generated " + total + " prior scenarios");
        }

        private SystemState BuildSyntheticState(double cpu, double ram, double temp, double fps, bool game)
        {
            SystemState s = new SystemState();
            s.CpuUsage = cpu;
            s.RamUsage = ram;
            s.GpuUsage = game ? 70 : 30;
            s.DiskUsage = 55;
            s.CpuTemp = temp;
            s.GpuTemp = temp;
            s.Fps = fps;
            s.FpsStability = fps > 0 ? Math.Min(100, fps * 1.2) : 0;
            s.Fps1Low = fps > 0 ? fps * 0.7 : 0;
            s.FrameTimeMs = fps > 0 ? 1000.0 / fps : 0;
            s.ProcessCount = 110;
            s.ActiveProcess = game ? "game.exe" : "chrome";
            s.IsGame = game;
            s.BatteryLevel = 100;
            s.IsCharging = true;
            s.TotalRamGB = 16;
            s.UsedRamGB = ram * 0.16;
            s.Timestamp = DateTime.Now;
            return s;
        }

        private MLFeatureVector BuildFVFrom(SystemState s)
        {
            MLFeatureVector fv = new MLFeatureVector();
            fv.CpuUsage = s.CpuUsage;
            fv.RamUsage = s.RamUsage;
            fv.GpuUsage = s.GpuUsage;
            fv.DiskUsage = s.DiskUsage;
            fv.CpuTemp = s.CpuTemp;
            fv.GpuTemp = s.GpuTemp;
            fv.Fps = s.Fps;
            fv.Fps01Low = s.Fps1Low;
            fv.FpsStability = s.FpsStability;
            fv.ProcessCount = s.ProcessCount;
            fv.HourOfDay = 14;
            fv.MinuteOfHour = 0;
            fv.IsGaming = s.IsGame ? 1.0 : 0.0;
            fv.IsOnBattery = (!s.IsCharging && s.BatteryLevel >= 0) ? 1.0 : 0.0;
            fv.BatteryLevel = s.BatteryLevel;
            fv.CpuTrend = 0;
            fv.RamTrend = 0;
            fv.TempTrend = 0;
            return fv;
        }

        private string BuildStateKeyFor(SystemState s)
        {
            RLState st = new RLState();
            st.CpuBin = Discretize(s.CpuUsage, CPU_BINS);
            st.RamBin = Discretize(s.RamUsage, RAM_BINS);
            st.TempBin = Discretize(s.CpuTemp, TEMP_BINS);
            st.FpsBin = Discretize(s.Fps, FPS_BINS);
            st.GpuBin = Discretize(s.GpuUsage, GPU_BINS);
            st.DiskBin = Discretize(s.DiskUsage, DISK_BINS);
            st.HourBin = 1;
            st.Mode = "AutoPilot";
            return BuildStateKey(st);
        }

        private void AddPriorSample(Dictionary<string, List<MLTrainingSample>> byAction,
            string action, MLFeatureVector fv, double target)
        {
            List<MLTrainingSample> list;
            if (!byAction.TryGetValue(action, out list))
            {
                list = new List<MLTrainingSample>();
                byAction[action] = list;
            }
            if (list.Count >= PRIOR_MAX_SAMPLES_PER_ACTION) return;
            MLTrainingSample s = new MLTrainingSample();
            s.Features = fv;
            s.ActionTaken = action;
            s.OutcomeScore = target;
            s.Timestamp = DateTime.Now;
            list.Add(s);

            // Also expose prior samples to the live training buffer so the ML
            // recommendation gate (>= 15 samples) is satisfied immediately.
            if (_mlTB.Count < PRIOR_MAX_TB)
            {
                _mlTB.Add(s);
            }
        }

        // Called from Tick: pretrain once (live mode only) + RL maintenance.
        private void RunLearningMaintenance()
        {
            try
            {
                if (IsGamingActive()) return;
                if (!_simulationMode) InitializeWithPriorKnowledge();
                RunRL2Maintenance();
                ComputeValidationMetrics();
                RunDegradationMonitor();
            }
            catch (Exception ex) { LogError("LearningMaintenance", ex); }
        }

        private static double ClampQ(double v)
        {
            return v < 0.1 ? 0.1 : v > 0.95 ? 0.95 : v;
        }
    }
}
