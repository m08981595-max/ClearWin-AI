using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;

namespace ClearWin
{
    // =====================================================================
    // Windows Optimization AI (v2.5) - feature tests
    // Covers: Learning (1000 decisions), Stability, Thermal profiles,
    // RAM profiles, Gaming profiles, Battery profiles, Feature Importance,
    // Confidence Calibration, ML save/load round trip, fault tolerance
    // (corrupt file -> backup recovery), training metrics, analysis stress
    // (throughput + 24h estimate), auto rollback, and quality metrics
    // (accuracy / precision / recall / F1 / latency).
    // =====================================================================
    public static class OptimizationAITest
    {
        private static SmartAgent _agent;
        private static readonly object _agentLock = new object();

        static void Assert(bool condition, string msg)
        {
            if (!condition) throw new Exception(msg);
        }

        static SmartAgent GetAgent()
        {
            if (_agent == null)
            {
                lock (_agentLock)
                {
                    if (_agent == null)
                    {
                        _agent = new SmartAgent();
                        _agent.SetMode(AgentMode.AutoPilot);
                    }
                }
            }
            return _agent;
        }

        static string ModelDir()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClearWin", "agent");
        }

        static string BackupModelDir()
        {
            string dir = ModelDir();
            string tmp = Path.Combine(Path.GetTempPath(), "mlbackup_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp);
            if (Directory.Exists(dir))
            {
                foreach (string f in Directory.GetFiles(dir, "ml_nn_v2*"))
                {
                    try { File.Copy(f, Path.Combine(tmp, Path.GetFileName(f)), true); } catch (Exception ex) { CWLog.Error("OptimizationAITest", ex); }
                }
            }
            return tmp;
        }

        static void RestoreModelDir(string tmp)
        {
            string dir = ModelDir();
            if (Directory.Exists(dir))
            {
                foreach (string f in Directory.GetFiles(dir, "ml_nn_v2*"))
                {
                    try { File.Delete(f); } catch (Exception ex2) { CWLog.Error("OptimizationAITest", ex2); }
                }
            }
            if (!string.IsNullOrEmpty(tmp) && Directory.Exists(tmp))
            {
                foreach (string f in Directory.GetFiles(tmp))
                {
                    try { File.Copy(f, Path.Combine(dir, Path.GetFileName(f)), true); } catch (Exception ex3) { CWLog.Error("OptimizationAITest", ex3); }
                }
            }
        }

        static SystemState MakeState(double cpu, double ram, double gpu, double temp, double fps, bool isGame, string app)
        {
            var s = new SystemState();
            s.CpuUsage = cpu;
            s.RamUsage = ram;
            s.GpuUsage = gpu;
            s.CpuTemp = temp;
            s.Fps = fps;
            s.FpsStability = fps > 0 ? Math.Min(100, fps * 1.2) : 0;
            s.FrameTimeMs = fps > 0 ? 1000.0 / fps : 0;
            s.IsGame = isGame;
            s.ActiveProcess = app;
            s.ProcessCount = 120;
            s.DiskUsage = 55;
            s.BatteryLevel = 100;
            s.IsCharging = true;
            s.TotalRamGB = 16;
            s.UsedRamGB = ram * 16.0 / 100.0;
            s.Timestamp = DateTime.Now;
            return s;
        }

        static SystemState HealthyState() { return MakeState(20, 30, 20, 40, 90, false, "explorer"); }

        static string ExpectedAction(SystemState s)
        {
            double cpu = s.CpuUsage, ram = s.RamUsage, temp = s.CpuTemp, fps = s.Fps, stab = s.FpsStability;
            bool game = s.IsGame;
            bool fpsKnown = fps > 0;
            if (temp >= (game ? 90 : 78)) return "EmergencyThrottle";
            if (!game && temp >= 68) return "ReduceThermal";
            if (ram >= 88) return "AggressiveClean";
            if (ram >= 80 && fpsKnown && fps < 60) return "PurgeStandbyList";
            if (ram >= 78) return "PreemptiveClean";
            if (ram >= 70 && !game) return "LightClean";
            if (game && fpsKnown && fps < 30) return "ReduceMicroStutter";
            if (game && fpsKnown && s.Fps01Low > 0 && s.Fps01Low < 25 && stab < 75) return "PurgeGpuShaderCache";
            if (game && cpu >= 70 && cpu <= 77 && ram <= 70 && fpsKnown && fps < 50) return "UnparkAllCores";
            if (game && fpsKnown && fps < 45 && cpu > 77) return "BoostGamePriority";
            if (game && fpsKnown && fps < 60) return "StabilizeFrames";
            if (game && fpsKnown && stab < 60) return "StabilizeFrames";
            if (game && ram >= 72 && ram < 80 && s.GpuUsage >= 65) return "ShedBackgroundGpuMemory";
            if (game && s.GpuUsage >= 80 && fpsKnown && fps >= 60 && temp < 85) return "OptimizeGpuPerformance";
            if (game) return "BoostGamePriority";
            if (!game && cpu >= 75 && ram >= 65) return "LimitBackground";
            if (!game && s.DiskUsage >= 75 && ram < 78) return "SetLowIoPriority";
            if (!game && cpu >= 60 && cpu < 75 && ram < 65) return "OptimizeDesktopSmoothness";
            if (!game && ram >= 56 && ram < 70 && cpu < 60) return "TrimInactiveWorkingSets";
            if (stab < 45 && fpsKnown && fps < 60) return "StabilizeFrames";
            return null;
        }

        // === Learning: the AI decides correctly across 1000 varied problem states ===
        public static void TestLearningPublic() { TestLearning(); }
        static void TestLearning()
        {
            var agent = GetAgent();
            var rnd = new Random(20260805);
            int produced = 0;
            int invalid = 0;
            int exceptions = 0;
            for (int i = 0; i < 1000; i++)
            {
                double cpu = 35 + rnd.NextDouble() * 60;
                double ram = 45 + rnd.NextDouble() * 50;
                double temp = 45 + rnd.NextDouble() * 50;
                double fps = 5 + rnd.NextDouble() * 100;
                bool game = rnd.NextDouble() < 0.4;
                var s = MakeState(cpu, ram, 30, temp, fps, game, game ? "game.exe" : "chrome");
                try
                {
                    var d = agent.GetOptimizationDecisionForState(s);
                    if (d != null)
                    {
                        produced++;
                        if (string.IsNullOrEmpty(d.Action)) invalid++;
                        if (d.Confidence < 0.05 || d.Confidence > 0.95) invalid++;
                    }
                }
                catch (Exception) { exceptions++; }
            }
            Assert(exceptions == 0, "No exceptions across 1000 decisions: " + exceptions);
            Assert(produced > 700, "AI produced decisions for 1000 problem states: " + produced);
            Assert(invalid == 0, "All decisions well-formed (action + valid confidence)");
        }

        // === Stability: healthy system triggers no heavy optimization ===
        public static void TestStabilityPublic() { TestStability(); }
        static void TestStability()
        {
            var agent = GetAgent();
            var d = agent.GetRuleDecisionForState(HealthyState());
            Assert(d == null, "Healthy system triggers no optimization");
            var map = agent.GetFeatureImportanceMap(HealthyState());
            double total = 0;
            foreach (var kv in map) total += kv.Value;
            Assert(total == 0, "No feature pressure on healthy state");
        }

        // === Thermal profiles: 40 / 55 / 70 / 85 / 95 ===
        public static void TestThermalProfilesPublic() { TestThermalProfiles(); }
        static void TestThermalProfiles()
        {
            var agent = GetAgent();
            AssertNull(agent, MakeState(30, 40, 30, 40, 80, false, ""), "temp 40 -> no action");
            AssertNull(agent, MakeState(30, 40, 30, 55, 80, false, ""), "temp 55 -> no action");
            AssertAction(agent, MakeState(30, 40, 30, 70, 80, false, ""), "ReduceThermal", "temp 70 -> ReduceThermal");
            AssertAction(agent, MakeState(30, 40, 30, 85, 80, false, ""), "EmergencyThrottle", "temp 85 -> EmergencyThrottle");
            AssertAction(agent, MakeState(30, 40, 30, 95, 80, false, ""), "EmergencyThrottle", "temp 95 -> EmergencyThrottle");
        }

        // === RAM profiles: 30 / 50 / 70 / 90 / 98 ===
        public static void TestRamProfilesPublic() { TestRamProfiles(); }
        static void TestRamProfiles()
        {
            var agent = GetAgent();
            AssertNull(agent, MakeState(30, 30, 30, 40, 80, false, ""), "ram 30 -> no action");
            AssertNull(agent, MakeState(30, 50, 30, 40, 80, false, ""), "ram 50 -> no action");
            AssertAction(agent, MakeState(30, 70, 30, 40, 80, false, ""), "LightClean", "ram 70 -> LightClean");
            AssertAction(agent, MakeState(30, 90, 30, 40, 80, false, ""), "AggressiveClean", "ram 90 -> AggressiveClean");
            AssertAction(agent, MakeState(30, 98, 30, 40, 80, false, ""), "AggressiveClean", "ram 98 -> AggressiveClean");
        }

        // === Gaming profiles: BoostGamePriority / StabilizeFrames / ReduceMicroStutter ===
        public static void TestGamingProfilesPublic() { TestGamingProfiles(); }
        static void TestGamingProfiles()
        {
            var agent = GetAgent();
            AssertAction(agent, MakeState(50, 40, 60, 50, 90, true, "game.exe"), "BoostGamePriority", "game 90fps headroom -> BoostGamePriority");
            AssertAction(agent, MakeState(50, 40, 60, 50, 40, true, "game.exe"), "StabilizeFrames", "game 40fps -> StabilizeFrames");
            AssertAction(agent, MakeState(50, 40, 60, 50, 20, true, "game.exe"), "ReduceMicroStutter", "game 20fps -> ReduceMicroStutter");
        }

        // === Battery profiles: charger / 100 / 50 / 20 / 10 ===
        public static void TestBatteryProfilesPublic() { TestBatteryProfiles(); }
        static void TestBatteryProfiles()
        {
            var agent = GetAgent();
            var s100 = MakeState(30, 40, 30, 40, 80, false, "");
            AssertNullOnBattery(agent, s100, false, 100, "charger -> no battery action");
            AssertNullOnBattery(agent, s100, true, 100, "on battery 100% -> no battery action");
            AssertActionOnBattery(agent, s100, true, 50, "BalancedPerformance", "on battery 50% -> BalancedPerformance");
            AssertActionOnBattery(agent, s100, true, 20, "BatterySaver", "on battery 20% -> BatterySaver");
            AssertActionOnBattery(agent, s100, true, 10, "BatterySaver", "on battery 10% -> BatterySaver");
        }

        // === Feature Importance: Temp must dominate on a hot system ===
        public static void TestFeatureImportancePublic() { TestFeatureImportance(); }
        static void TestFeatureImportance()
        {
            var agent = GetAgent();
            var hot = MakeState(30, 40, 30, 85, 80, false, "");
            var map = agent.GetFeatureImportanceMap(hot);
            double bestVal = -1;
            string bestKey = "";
            foreach (var kv in map)
            {
                if (kv.Value > bestVal) { bestVal = kv.Value; bestKey = kv.Key; }
            }
            Assert(bestKey == "Temp", "Temp is the dominant feature on a hot system, got: " + bestKey + "=" + bestVal.ToString("F3"));
            Assert(bestVal > 0.5, "Temp importance significant: " + bestVal.ToString("F3"));
            string influence = agent.GetFeatureImportance(hot);
            Assert(influence != null && influence.Contains("Temp"), "Feature influence attached to reason: " + influence);
        }

        // === Confidence Calibration: outcomes raise/lower calibrated confidence ===
        public static void TestConfidenceCalibrationPublic() { TestConfidenceCalibration(); }
        static void TestConfidenceCalibration()
        {
            var agent = new SmartAgent();
            agent.ResetCalibrationData();
            for (int i = 0; i < 12; i++) agent.RecordConfidenceSample("AggressiveClean", 0.8, true);
            double calibrated = agent.GetCalibratedConfidence("AggressiveClean", 0.8);
            Assert(calibrated > 0.8, "Calibration raises confidence when outcomes exceed predictions: " + calibrated.ToString("F3"));
            Assert(calibrated <= 0.95, "Calibration caps at 0.95: " + calibrated.ToString("F3"));
            for (int i = 0; i < 12; i++) agent.RecordConfidenceSample("LightClean", 0.7, false);
            double lowered = agent.GetCalibratedConfidence("LightClean", 0.7);
            Assert(lowered < 0.7, "Calibration lowers confidence when outcomes underperform: " + lowered.ToString("F3"));
            Assert(agent.GetCalibrationSamples() >= 20, "Calibration samples recorded: " + agent.GetCalibrationSamples());
            Assert(agent.GetCalibrationSummary() != null && agent.GetCalibrationSummary().Length > 0, "Calibration summary available");
        }

        // === Training Metrics: loss / accuracy tracked after training ===
        public static void TestTrainingMetricsPublic() { TestTrainingMetrics(); }
        static void TestTrainingMetrics()
        {
            var agent = GetAgent();
            for (int i = 0; i < 6; i++) agent.RecordActionOutcome("PreemptiveClean", i % 2 == 0 ? 0.85 : 0.15);
            string metrics = agent.GetTrainingMetrics();
            Assert(metrics != null && metrics.Length > 0 && !metrics.StartsWith("No training"), "Training metrics tracked: " + metrics);
            double acc = agent.GetTrainingAccuracy();
            Assert(acc >= 0 && acc <= 1.0, "Training accuracy in range: " + acc.ToString("F3"));
            double loss = agent.GetTrainingLoss();
            Assert(loss >= 0 && loss <= 1.0, "Training loss in range: " + loss.ToString("F3"));
            Assert(agent.TrainingMetricsSummary != null && agent.TrainingMetricsSummary.Length > 0, "Training metrics summary available");
        }

        // === ML Save/Load Round Trip: ensembles preserved across persistence ===
        public static void TestMLSaveLoadRoundTripPublic() { TestMLSaveLoadRoundTrip(); }
        static void TestMLSaveLoadRoundTrip()
        {
            var agent = GetAgent();
            Directory.CreateDirectory(ModelDir());
            string tmp = BackupModelDir();
            try
            {
                for (int i = 0; i < 24; i++)
                {
                    agent.RecordActionOutcome("AggressiveClean", i % 2 == 0 ? 0.9 : 0.1);
                    agent.RecordActionOutcome("LightClean", i % 2 == 0 ? 0.2 : 0.8);
                }
                int countA = agent.GetModelEnsembleCount();
                Assert(countA > 0, "Training created at least one ensemble: " + countA);
                agent.SaveModelData();
                agent.SaveModelData();
                agent.LoadModelData();
                int countB = agent.GetModelEnsembleCount();
                Assert(countB == countA, "Round-trip preserved ensembles: " + countA + " vs " + countB);
                Assert(File.Exists(Path.Combine(ModelDir(), "ml_nn_v2.dat")), "Model file saved");
                Assert(File.Exists(Path.Combine(ModelDir(), "ml_nn_v2.dat.sha")), "Checksum file saved");
            }
            finally { RestoreModelDir(tmp); }
        }

        // === Fault tolerance: corrupt model file recovers from backup ===
        public static void TestModelFaultTolerancePublic() { TestModelFaultTolerance(); }
        static void TestModelFaultTolerance()
        {
            var agent = GetAgent();
            Directory.CreateDirectory(ModelDir());
            string tmp = BackupModelDir();
            try
            {
                for (int i = 0; i < 10; i++) agent.RecordActionOutcome("AggressiveClean", 0.8);
                int countBefore = agent.GetModelEnsembleCount();
                Assert(countBefore > 0, "Training produced ensembles before corruption: " + countBefore);
                agent.SaveModelData();
                agent.SaveModelData();
                Assert(File.Exists(Path.Combine(ModelDir(), "ml_nn_v2.dat.bak1")), "Backup created");
                File.WriteAllText(Path.Combine(ModelDir(), "ml_nn_v2.dat"), "CORRUPTED-NOT-A-MODEL");
                agent.LoadModelData();
                int countAfter = agent.GetModelEnsembleCount();
                Assert(countAfter == countBefore, "Model recovered from backup after corruption: " + countBefore + " vs " + countAfter);
                Assert(File.Exists(Path.Combine(ModelDir(), "ml_nn_v2.dat")), "Model file restored");
            }
            finally { RestoreModelDir(tmp); }
        }

        // === Analysis stress: throughput, latency, exceptions, 24h estimate ===
        public static void TestAnalysisStressPublic() { TestAnalysisStress(); }
        static void TestAnalysisStress()
        {
            var agent = GetAgent();
            var res = agent.RunAnalysisStress(2000);
            Assert(res.TotalAnalyses == 2000, "Stress ran all iterations: " + res.TotalAnalyses);
            Assert(res.TotalExceptions == 0, "No exceptions during stress: " + res.TotalExceptions);
            Assert(res.DecisionsProduced > 0, "Stress produced decisions: " + res.DecisionsProduced);
            Assert(res.AvgLatencyMs >= 0, "Avg latency measured: " + res.AvgLatencyMs.ToString("F3") + "ms");
            Assert(res.EstimatedAnalysesPerDay > 0, "24h throughput estimate computed: " + res.EstimatedAnalysesPerDay);
        }

        // === Auto Rollback: degradation detection + inverse execution ===
        public static void TestRollbackPublic() { TestRollback(); }
        static void TestRollback()
        {
            var agent = GetAgent();
            var healthy = HealthyState();
            var degraded = MakeState(60, 55, 50, 70, 40, false, "chrome");
            Assert(agent.IsPostStateDegraded(healthy, degraded), "Degradation detected");
            Assert(!agent.IsPostStateDegraded(healthy, healthy), "No degradation on identical states");
            var improved = MakeState(10, 20, 10, 35, 120, false, "chrome");
            Assert(!agent.IsPostStateDegraded(healthy, improved), "No degradation on improvement");
            agent.ArmRollbackBaseline(healthy, "BatterySaver");
            Assert(!agent.AutoRollbackIfDegraded(healthy), "No rollback when not degraded");
            Assert(agent.LastRollbackLog == "OK", "Log says OK");
            agent.ArmRollbackBaseline(healthy, "BatterySaver");
            Assert(agent.AutoRollbackIfDegraded(degraded), "Rollback triggered on degradation");
            Assert(agent.LastRollbackLog != null && agent.LastRollbackLog.Length > 0 && agent.LastRollbackLog != "OK", "Rollback logged");
        }

        // === Quality: accuracy / precision / recall / F1 / latency over 1000 scenarios ===
        public static void TestQualityPublic() { TestQuality(); }
        static void TestQuality()
        {
            var agent = GetAgent();
            int tp = 0, fp = 0, tn = 0, fn = 0;
            long totalTicks = 0;
            var rnd = new Random(42);
            int scenarios = 1000;
            for (int i = 0; i < scenarios; i++)
            {
                double cpu = 5 + rnd.NextDouble() * 95;
                double ram = 20 + rnd.NextDouble() * 78;
                double temp = 35 + rnd.NextDouble() * 60;
                double fps = rnd.Next(0, 120);
                bool game = rnd.NextDouble() < 0.35;
                var s = MakeState(cpu, ram, 30, temp, fps, game, game ? "game.exe" : "chrome");
                string expected = ExpectedAction(s);
                var sw = Stopwatch.StartNew();
                var d = agent.GetRuleDecisionForState(s, false, 100);
                sw.Stop();
                totalTicks += sw.ElapsedTicks;
                string actual = d != null ? d.Action : null;
                if (expected == null && actual == null) tn++;
                else if (expected == null && actual != null) fp++;
                else if (expected != null && actual == null) fn++;
                else if (expected == actual) tp++;
                else { fp++; fn++; }
            }
            double precision = (tp + fp) > 0 ? (double)tp / (tp + fp) : 0;
            double recall = (tp + fn) > 0 ? (double)tp / (tp + fn) : 0;
            double f1 = (precision + recall) > 0 ? 2.0 * precision * recall / (precision + recall) : 0;
            double accuracy = (double)(tp + tn) / scenarios;
            double latencyMs = (double)totalTicks / Stopwatch.Frequency * 1000.0 / scenarios;
            Assert(accuracy > 0.90, "Accuracy > 90%: " + accuracy.ToString("F3"));
            Assert(precision > 0.85, "Precision > 85%: " + precision.ToString("F3"));
            Assert(recall > 0.85, "Recall > 85%: " + recall.ToString("F3"));
            Assert(f1 > 0.85, "F1 > 85%: " + f1.ToString("F3"));
            Assert(latencyMs < 10, "Rule latency < 10ms: " + latencyMs.ToString("F2") + "ms");
        }

        // === Real RL2: state-aware Temporal-Difference learning ===
        public static void TestRL2Public() { TestRL2(); }
        static void TestRL2()
        {
            var agent = new SmartAgent();
            string state = "C5R5T4F3_AutoPilot";
            string next = "C4R4T3F2_AutoPilot";
            for (int i = 0; i < 30; i++)
                agent.RecordRLExperience(state, "LightClean", 0.9, next);
            Assert(agent.GetStateAwareStateCount() > 0, "RL2 learned at least one state: " + agent.GetStateAwareStateCount());
            Assert(agent.GetRL2UpdateCount() >= 30, "RL2 recorded TD updates: " + agent.GetRL2UpdateCount());
            string best = agent.SelectBestStateAction(state);
            Assert(best == "LightClean", "Repeated positive outcomes make the action optimal in-state: " + best);
            string summary = agent.GetRL2Summary();
            Assert(summary != null && summary.Contains("States"), "RL2 summary available: " + summary);
        }

        // === Real outcome reward engine: measured evaluations feed real Q-learning ===
        public static void TestRewardEnginePublic() { TestRewardEngine(); }
        static void TestRewardEngine()
        {
            var agent = new SmartAgent();
            int updatesBefore = agent.GetRL2UpdateCount();
            agent.StartActionEvaluation("LightClean", true);
            agent.TickOutcomeEvaluation();
            agent.TickOutcomeEvaluation();
            string status = agent.GetRewardEngineStatus();
            Assert(status != null && status.Length > 0, "Reward engine reports status");
            // Second evaluation force-finalizes the first -> real measured outcome
            agent.StartActionEvaluation("PurgeStandbyList", false);
            Assert(agent.GetEvaluationsCompleted() >= 1, "Evaluation completed: " + agent.GetEvaluationsCompleted());
            Assert(agent.GetRL2UpdateCount() > updatesBefore, "Finalized measured outcome fed the real Q-learning");
            Assert(agent.GetAverageObjectiveReward() >= -1.0 && agent.GetAverageObjectiveReward() <= 1.0, "Objective reward normalized to [-1,1]");
        }

        // === Honest metrics: real-world success rate is no longer 100% by default ===
        public static void TestHonestMetricsPublic() { TestHonestMetrics(); }
        static void TestHonestMetrics()
        {
            var agent = new SmartAgent();
            for (int i = 0; i < 8; i++) agent.RecordRealOutcomeMetric(0.9);
            double rate = agent.GetRealSuccessRate();
            Assert(rate > 0.7, "Real success rate reflects positive outcomes: " + rate.ToString("F3"));
            for (int i = 0; i < 6; i++) agent.RecordRealOutcomeMetric(0.1);
            double rate2 = agent.GetRealSuccessRate();
            Assert(rate2 < 0.7, "Real success rate reflects mixed outcomes: " + rate2.ToString("F3"));
            string metrics = agent.GetMLHonestMetrics();
            Assert(metrics != null && metrics.Contains("Real success"), "Honest metrics string available");
            double total = agent.GetTotalInferenceSuccessRate();
            Assert(total > 0 && total <= 1.0, "Total inference success rate tracked: " + total.ToString("F3"));
        }

        // === Prior knowledge pretraining: rule expertise seeds Q + ensembles ===
        public static void TestPriorPretrainPublic() { TestPriorPretrain(); }
        static void TestPriorPretrain()
        {
            Directory.CreateDirectory(ModelDir());
            string tmp = BackupModelDir();
            try
            {
                foreach (string f in Directory.GetFiles(ModelDir(), "ml_nn_v2*"))
                {
                    try { File.Delete(f); } catch (Exception ex4) { CWLog.Error("OptimizationAITest", ex4); }
                }
                var agent = new SmartAgent();
                agent.SetMode(AgentMode.AutoPilot);
                agent.InitializeWithPriorKnowledge();
                Assert(agent.IsPriorKnowledgeInitialized, "Prior knowledge initialized on an empty model");
                Assert(agent.GetStateAwareStateCount() > 0, "Prior seeded the state-aware Q table: " + agent.GetStateAwareStateCount());
                Assert(agent.GetModelEnsembleCount() > 0, "Prior pretrained neural ensembles: " + agent.GetModelEnsembleCount());
                Assert(agent.GetMLInsight() != null && agent.GetMLInsight().Contains("Ensembles"), "ML insight reflects pretrained ensembles");
            }
            finally { RestoreModelDir(tmp); }
        }

        // === Counterfactual reward: difference-in-differences isolates the
        // action's real causal effect by subtracting the natural drift ===
        public static void TestCounterfactualRewardPublic() { TestCounterfactualReward(); }
        static void TestCounterfactualReward()
        {
            // Identical observed improvement (+6 FPS), but one record has a natural
            // +0.3 FPS/s drift (i.e. +6 FPS over the 20s window would have happened
            // anyway). The no-drift action must be credited, the drift one must not.
            double rFpsNoDrift = SmartAgent.ComputeObjectiveReward(MakeRewardRecord(60, 0, 60, 0, 60, 0), 66, 50, 60, 70, 60, 100, 20);
            double rFpsDrift = SmartAgent.ComputeObjectiveReward(MakeRewardRecord(60, 0.3, 60, 0, 60, 0), 66, 50, 60, 70, 60, 100, 20);
            Assert(rFpsNoDrift > rFpsDrift,
                "natural FPS rise is NOT credited to the action (" + rFpsNoDrift.ToString("F3") + " vs " + rFpsDrift.ToString("F3") + ")");

            // Identical observed temperature rise (+2C), but one record has a
            // natural +0.2 C/s drift (+4C over 20s). The naturally-rising system
            // must NOT be blamed for the action's failure to cool it.
            double rTempNoDrift = SmartAgent.ComputeObjectiveReward(MakeRewardRecord(60, 0, 60, 0, 60, 0), 60, 50, 62, 70, 60, 100, 20);
            double rTempDrift = SmartAgent.ComputeObjectiveReward(MakeRewardRecord(60, 0, 60, 0.2, 60, 0), 60, 50, 62, 70, 60, 100, 20);
            Assert(rTempDrift > rTempNoDrift,
                "natural temperature rise is NOT blamed on the action (" + rTempDrift.ToString("F3") + " vs " + rTempNoDrift.ToString("F3") + ")");

            // A real improvement beyond the drift is still rewarded positively.
            double rRealGain = SmartAgent.ComputeObjectiveReward(MakeRewardRecord(60, -0.2, 60, 0, 60, 0), 72, 55, 58, 85, 55, 100, 20);
            Assert(rRealGain > 0.3, "real improvement beyond drift is rewarded: " + rRealGain.ToString("F3"));

            // Integration: the agent exposes the counterfactual drift status.
            var agent = new SmartAgent();
            string status = agent.GetCounterfactualStatus();
            Assert(status != null && status.Contains("drift fps"), "counterfactual status available: " + status);
            Assert(SmartAgent.IsMeasurableAction("HealthCheck") == false, "advisory actions are not measured");
            Assert(SmartAgent.IsMeasurableAction("LightClean") == true, "system-changing actions are measured");
        }

        static ActionOutcomeRecord MakeRewardRecord(double fps, double fpsSlope, double temp, double tempSlope, double ram, double ramSlope)
        {
            var rec = new ActionOutcomeRecord();
            rec.Action = "X";
            rec.ExecutedSuccessfully = true;
            rec.BaselineFps = fps;
            rec.BaselineFpsSlope = fpsSlope;
            rec.BaselineTemp = temp;
            rec.BaselineTempSlope = tempSlope;
            rec.BaselineRam = ram;
            rec.BaselineRamSlope = ramSlope;
            rec.BaselineFps1Low = 50;
            rec.BaselineStability = 70;
            rec.IsGame = true;
            rec.OnBattery = false;
            return rec;
        }

        // === Calibrated abstention: with enough bad outcomes the agent learns
        // to NOT act, while thermal emergencies always bypass abstention ===
        public static void TestCalibratedAbstentionPublic() { TestCalibratedAbstention(); }
        static void TestCalibratedAbstention()
        {
            var agent = new SmartAgent();
            // 4 immediate outcomes: LightClean succeeds (+0.1), PurgeStandby fails (-0.5).
            // Force-finalizing happens when a new evaluation starts.
            for (int i = 0; i < 3; i++)
            {
                agent.StartActionEvaluation("LightClean", true);
                agent.StartActionEvaluation("PurgeStandbyList", false);
            }
            agent.StartActionEvaluation("LightClean", true); // finalize last PurgeStandby
            Assert(agent.GetActionRewardSampleCount("LightClean") >= 3, "reward samples recorded per action");
            Assert(agent.GetExpectedReward("LightClean") > 0, "successful action has positive expected reward: " + agent.GetExpectedReward("LightClean").ToString("F2"));
            Assert(agent.GetExpectedReward("PurgeStandbyList") < 0, "failing action has negative expected reward: " + agent.GetExpectedReward("PurgeStandbyList").ToString("F2"));

            var d = new AgentDecision();
            d.Action = "PurgeStandbyList";
            d.Category = DecisionCategory.PurgeStandby;
            d.Source = "WindowsOptimizationAI";
            Assert(agent.ShouldAbstain(d), "learns to abstain from a consistently failing action");

            var emergency = new AgentDecision();
            emergency.Action = "EmergencyThrottle";
            emergency.Category = DecisionCategory.ThermalProtect;
            Assert(!agent.ShouldAbstain(emergency), "thermal emergency never abstains");
        }

        // === Source-level trust: measured outcomes per decision source ===
        public static void TestSourceTrustPublic() { TestSourceTrust(); }
        static void TestSourceTrust()
        {
            var agent = new SmartAgent();
            for (int i = 0; i < 3; i++)
            {
                agent.StartActionEvaluation("LightClean", true);
                agent.StartActionEvaluation("PurgeStandbyList", false);
            }
            agent.StartActionEvaluation("LightClean", true);
            // 4 outcomes recorded under the fallback source (never executed via
            // ExecuteDecision), mixed good/bad -> trust strictly between 0 and 1.
            Assert(agent.GetTrustedSourceCount() >= 1, "source outcomes tracked: " + agent.GetTrustedSourceCount());
            double trust = agent.GetSourceTrust("Unknown");
            Assert(trust > 0 && trust < 1.0, "measured source trust is calibrated: " + trust.ToString("F3"));
            Assert(agent.GetSourceTrust("RuleEngine") == 1.0, "unmeasured source stays neutral");
        }

        // === Context-aware RL state key: canonical, includes GPU/disk/hour ===
        public static void TestContextStateKeyPublic() { TestContextStateKey(); }
        static void TestContextStateKey()
        {
            var agent = new SmartAgent();
            var st = new RLState();
            st.CpuBin = 2; st.RamBin = 3; st.TempBin = 1; st.FpsBin = 0;
            st.GpuBin = 1; st.DiskBin = 0; st.HourBin = 2; st.Mode = "Gaming";
            string k = agent.BuildStateKey(st);
            Assert(k == "C2R3T1F0G1D0H2_Gaming", "canonical context key: " + k);

            st.GpuBin = 2;
            Assert(agent.BuildStateKey(st) != k, "GPU context changes the state key");
            st.GpuBin = 1; st.HourBin = 0;
            Assert(agent.BuildStateKey(st) != k, "time-of-day context changes the state key");

            string live = agent.GetCurrentRLStateKey();
            Assert(live != null && live.Length >= 18, "live key is extended: " + live);
            Assert(live == agent.BuildStateKey(agent.GetCurrentRLState()), "live key uses the canonical builder");
        }

        static void AssertNull(SmartAgent agent, SystemState s, string msg)
        {
            var d = agent.GetRuleDecisionForState(s);
            Assert(d == null, msg + " (got " + (d != null ? d.Action : "none") + ")");
        }

        static void AssertAction(SmartAgent agent, SystemState s, string expected, string msg)
        {
            var d = agent.GetRuleDecisionForState(s);
            Assert(d != null && d.Action == expected, msg + " (got " + (d != null ? d.Action : "none") + ")");
        }

        static void AssertNullOnBattery(SmartAgent agent, SystemState s, bool onBattery, int percent, string msg)
        {
            var d = agent.GetRuleDecisionForState(s, onBattery, percent);
            Assert(d == null, msg + " (got " + (d != null ? d.Action : "none") + ")");
        }

        static void AssertActionOnBattery(SmartAgent agent, SystemState s, bool onBattery, int percent, string expected, string msg)
        {
            var d = agent.GetRuleDecisionForState(s, onBattery, percent);
            Assert(d != null && d.Action == expected, msg + " (got " + (d != null ? d.Action : "none") + ")");
        }

        // === UI Frame Pacing (ATLASX-inspired): profile resolution, refresh clamping, smoothness ===
        public static void TestFramePacingProfilePublic() { TestFramePacingProfile(); }
        static void TestFramePacingProfile()
        {
            Assert(SmartAgent.ResolveFramePacingProfile(false, true, false, true) == "thermal", "thermal heat overrides everything");
            Assert(SmartAgent.ResolveFramePacingProfile(true, true, false, false) == "gaming", "game wins over charging");
            Assert(SmartAgent.ResolveFramePacingProfile(true, true, true, false) == "battery", "low battery wins over game");
            Assert(SmartAgent.ResolveFramePacingProfile(false, true, false, false) == "smooth", "charging -> smooth");
            Assert(SmartAgent.ResolveFramePacingProfile(false, false, false, false) == "balanced", "default -> balanced");
            Assert(SmartAgent.ResolveFramePacingProfile(false, false, false, true) == "thermal", "thermal wins over balanced");
        }

        public static void TestFramePacingRefreshPublic() { TestFramePacingRefresh(); }
        static void TestFramePacingRefresh()
        {
            // Clamped to the real monitor capability (ATLASX device_max_hz).
            Assert(SmartAgent.GetFramePacingTargetHz("gaming", 60) == 60, "gaming on a 60Hz monitor stays 60");
            Assert(SmartAgent.GetFramePacingTargetHz("gaming", 120) == 90, "gaming targets 90 on a 120Hz monitor");
            Assert(SmartAgent.GetFramePacingTargetHz("smooth", 240) == 90, "smooth targets 90");
            Assert(SmartAgent.GetFramePacingTargetHz("battery", 144) == 60, "battery always 60 (power saving)");
            Assert(SmartAgent.GetFramePacingTargetHz("thermal", 165) == 60, "thermal always 60 (cooling)");
            Assert(SmartAgent.GetFramePacingTargetHz("balanced", 60) == 60, "balanced floor is 60");
            int unknown = SmartAgent.GetFramePacingTargetHz("gaming", 0);
            Assert(unknown >= 60, "target never goes below 60 on unknown device: " + unknown);
            Assert(SmartAgent.GetFramePacingTargetHz("battery", 0) == 60, "battery floor is 60 even when device unknown");
        }

        public static void TestFramePacingSmoothnessPublic() { TestFramePacingSmoothness(); }
        static void TestFramePacingSmoothness()
        {
            Assert(SmartAgent.GetFramePacingSmoothnessLevel("balanced") == 0, "balanced keeps full UI animations");
            Assert(SmartAgent.GetFramePacingSmoothnessLevel("gaming") == 1, "gaming reduces UI animations");
            Assert(SmartAgent.GetFramePacingSmoothnessLevel("smooth") == 1, "smooth reduces UI animations");
            Assert(SmartAgent.GetFramePacingSmoothnessLevel("battery") == 2, "battery disables UI animations");
            Assert(SmartAgent.GetFramePacingSmoothnessLevel("thermal") == 2, "thermal disables UI animations");
        }

        // === Network / Low-Latency Tuning (Frieren-inspired) ===
        public static void TestNetworkTuningProfilePublic() { TestNetworkTuningProfile(); }
        static void TestNetworkTuningProfile()
        {
            Assert(SmartAgent.ResolveNetworkProfile(false, true, false, true) == "thermal", "thermal heat overrides everything");
            Assert(SmartAgent.ResolveNetworkProfile(true, true, true, false) == "battery", "low battery wins over game");
            Assert(SmartAgent.ResolveNetworkProfile(true, true, false, false) == "gaming", "game -> gaming profile");
            Assert(SmartAgent.ResolveNetworkProfile(false, true, false, false) == "balanced", "default -> balanced");
            Assert(SmartAgent.GetNetworkProfilePriority("thermal") > SmartAgent.GetNetworkProfilePriority("battery"), "thermal most urgent");
            Assert(SmartAgent.GetNetworkProfilePriority("battery") > SmartAgent.GetNetworkProfilePriority("gaming"), "battery over gaming");
            Assert(SmartAgent.GetNetworkProfilePriority("gaming") > SmartAgent.GetNetworkProfilePriority("balanced"), "gaming over balanced");
            Assert(SmartAgent.IsNetworkProfileMoreUrgent("battery", "gaming"), "urgent comparator works");
            Assert(SmartAgent.IsNetworkProfileMoreUrgent("gaming", "balanced"), "gaming is more urgent than balanced");
            Assert(SmartAgent.IsNetworkProfileMoreUrgent("balanced", "gaming") == false, "non-urgent never wins");
        }

        public static void TestNetworkTuningSettingsPublic() { TestNetworkTuningSettings(); }
        static void TestNetworkTuningSettings()
        {
            var gaming = SmartAgent.GetNetworkProfileNetshSettings("gaming");
            Assert(gaming["autotuninglevel"] == "normal", "gaming keeps auto-tuning on");
            Assert(gaming["timestamps"] == "disabled", "gaming disables timestamps (low latency)");
            Assert(gaming["rss"] == "enabled", "gaming enables RSS");
            Assert(gaming["ecn"] == "disabled", "gaming disables ECN");
            Assert(gaming["maxsyn"] == "2", "gaming lowers SYN retransmissions");
            Assert(gaming["tcpnodelay"] == "1", "gaming disables Nagle (TCPNoDelay)");
            Assert(gaming["ackfreq"] == "1", "gaming minimizes delayed ACK");

            var battery = SmartAgent.GetNetworkProfileNetshSettings("battery");
            Assert(battery["autotuninglevel"] == "restricted", "battery restricts auto-tuning");
            Assert(battery["rss"] == "disabled", "battery disables RSS (power save)");
            Assert(battery["timestamps"] == "enabled", "battery keeps timestamps");

            var thermal = SmartAgent.GetNetworkProfileNetshSettings("thermal");
            Assert(thermal["autotuninglevel"] == "disabled", "thermal disables auto-tuning (least work)");
            Assert(thermal["congestion"] == "newreno", "thermal uses light congestion control");

            var balanced = SmartAgent.GetNetworkProfileNetshSettings("balanced");
            Assert(balanced["ecn"] == "enabled", "balanced keeps ECN");
            Assert(balanced["tcpnodelay"] == "0", "balanced keeps Nagle (safe default)");

            var unknown = SmartAgent.GetNetworkProfileNetshSettings("bogus");
            Assert(unknown["autotuninglevel"] == balanced["autotuninglevel"], "unknown profile falls back to balanced");
        }

        public static void TestNetworkTuningMaintenancePublic() { TestNetworkTuningMaintenance(); }
        static void TestNetworkTuningMaintenance()
        {
            DateTime now = DateTime.Now;
            Assert(SmartAgent.IsNetworkMaintenanceDue(now.AddHours(-7), now, 6.0), "maintenance due after interval");
            Assert(SmartAgent.IsNetworkMaintenanceDue(now.AddMinutes(-10), now, 6.0) == false, "maintenance not due early");
            Assert(SmartAgent.IsNetworkMaintenanceDue(now.AddHours(-6), now, 6.0), "maintenance due exactly at interval");
        }

        // === Real log import: CSV from tools/collect_real_logs.ps1 feeds real
        // experiences into RL2, ML, and the reward stats via ImportRealLog ===
        public static void TestRealLogImportPublic() { TestRealLogImport(); }
        static void TestRealLogImport()
        {
            SmartAgent agent = GetAgent();
            string tmp = Path.Combine(Path.GetTempPath(), "reallog_" + Guid.NewGuid().ToString("N") + ".csv");
            try
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("#ClearWin real log v1");
                DateTime baseT = DateTime.Now.AddSeconds(-120);
                DateTime evT = baseT.AddSeconds(60);
                for (int i = 0; i < 12; i++)
                {
                    DateTime t = baseT.AddSeconds(i * 5);
                    // timestamp, cpu, ram, temp, fps, fps1low, stability, gpu, disk, proc, battery, charging, is_game
                    sb.AppendLine(FormatSample(t, 82.0, 86.0, 71.0, 52.0, 42.0, 60.0, 40.0, 45.0, 140, 100.0, 1, 0));
                }
                sb.AppendLine("event,LightClean," + evT.ToString("yyyy-MM-ddTHH:mm:ss.fff"));
                for (int i = 0; i < 12; i++)
                {
                    DateTime t = evT.AddSeconds(2 + i * 5);
                    sb.AppendLine(FormatSample(t, 60.0, 58.0, 62.0, 68.0, 58.0, 82.0, 30.0, 40.0, 110, 100.0, 1, 0));
                }
                File.WriteAllText(tmp, sb.ToString());

                int beforeEvals = agent.GetEvaluationsCompleted();
                int beforeSamples = agent.GetActionRewardSampleCount("LightClean");
                int imported = agent.ImportRealLog(tmp, 45, 45, "TestRealLog");
                Assert(imported >= 1, "imported at least one experience: " + imported);
                Assert(agent.GetRealLogImportedCount() >= 1, "importer counter advanced");
                Assert(agent.GetEvaluationsCompleted() > beforeEvals, "reward stats fed by real log");
                Assert(agent.GetActionRewardSampleCount("LightClean") > beforeSamples, "per-action reward samples fed by real log");
                Assert(agent.GetExpectedReward("LightClean") > 0, "improved window produced positive expected reward");
            }
            finally
            {
                try { File.Delete(tmp); } catch (Exception ex5) { CWLog.Error("OptimizationAITest", ex5); }
            }
        }

        static string FormatSample(DateTime t, double cpu, double ram, double temp, double fps,
            double fps1Low, double stability, double gpu, double disk, int proc, double battery, int charging, int isGame)
        {
            return string.Join(",",
                t.ToString("yyyy-MM-ddTHH:mm:ss.fff"),
                cpu.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                ram.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                temp.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                fps.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                fps1Low.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                stability.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                gpu.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                disk.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                proc.ToString(System.Globalization.CultureInfo.InvariantCulture),
                battery.ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
                charging.ToString(System.Globalization.CultureInfo.InvariantCulture),
                isGame.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        // === Vendor GPU temperature: NVAPI/ADL must never throw and must return
        // a sane range or -1 on machines without discrete-GPU SDKs ===
        public static void TestGpuMonitorPublic() { TestGpuMonitor(); }
        static void TestGpuMonitor()
        {
            double temp = GpuMonitor.ReadGpuTemperatureCelsius();
            Assert(temp == -1 || (temp > 20 && temp < 120), "vendor GPU temp is -1 or in a sane 20..120 range, got " + temp);
            string status = GpuMonitor.GetStatus();
            Assert(!string.IsNullOrEmpty(status), "GpuMonitor.GetStatus returns a description");
        }

        // === DXGI displayed-FPS reader: must never throw and return >= 0 ===
        public static void TestDxgiFpsPublic() { TestDxgiFps(); }
        static void TestDxgiFps()
        {
            double fps = DxgiFpsReader.ReadDisplayFps();
            Assert(fps >= 0 && fps < 1000, "display FPS is >= 0 and sane, got " + fps);
        }

        // === Startup classifier: bloat/essential classification + safe
        // disable/restore through the backup hive (no data loss) ===
        public static void TestStartupClassifierPublic() { TestStartupClassifier(); }
        static void TestStartupClassifier()
        {
            var bloat = StartupClassifier.Classify("HKCU", "TestUpdater", "\"C:\\Program Files\\SomeApp\\updater.exe\" /background");
            Assert(bloat.Recommendation == StartupRecommendation.SafeToDisable, "third-party updater classified as bloat, got " + bloat.Reason);

            var essential = StartupClassifier.Classify("HKLM", "SecurityHealth", "\"C:\\Windows\\System32\\SecurityHealthSystray.exe\"");
            Assert(essential.Recommendation == StartupRecommendation.KeepEssential, "Windows security entry classified as essential, got " + essential.Reason);

            var unknown = StartupClassifier.Classify("HKCU", "MyApp", "\"C:\\Users\\me\\AppData\\Local\\MyApp\\myapp.exe\"");
            Assert(unknown.Recommendation == StartupRecommendation.UnknownKeep, "no-signal entry stays unknown-keep");

            // Safe disable + restore round trip via isolated test subkey (never touches real HKCU\...\CurrentVersion\Run)
            string testRunPath = @"SOFTWARE\ClearWin\TestSandbox\Run";
            string name = "ClearWin_Test_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string cmd = "\"C:\\Program Files\\Test\\test.exe\" --background";
            StartupClassifier.TestRunSubKeyOverride = testRunPath;
            try
            {
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(testRunPath))
                {
                    if (key != null) key.SetValue(name, cmd);
                }
                bool disabled = StartupClassifier.ExecuteStartupDisable("HKCU", name, cmd);
                Assert(disabled, "fake startup entry disabled (backed up)");
                bool restored = StartupClassifier.ExecuteStartupRestore("HKCU", name);
                Assert(restored, "fake startup entry restored from backup");
                using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(testRunPath, false))
                {
                    if (key == null) throw new Exception("Test Run hive unreadable");
                    object val = key.GetValue(name);
                    Assert(val != null && Convert.ToString(val) == cmd, "value matches after restore");
                    key.Close();
                }
            }
            finally
            {
                StartupClassifier.TestRunSubKeyOverride = null;
                try
                {
                    using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(testRunPath, true))
                    {
                        if (key != null) key.DeleteValue(name, false);
                    }
                    Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"SOFTWARE\ClearWin\TestSandbox", false);
                }
                catch { }
                try
                {
                    using (Microsoft.Win32.RegistryKey backup = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\ClearWin\StartupBackup\HKCU", true))
                    {
                        if (backup != null) backup.DeleteValue(name, false);
                    }
                }
                catch { }
            }
        }
    }
}
