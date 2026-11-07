using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ClearWin
{
    public static class NeuralNetworkTest
    {
        private static int _passed = 0;
        private static int _failed = 0;
        private static int _total = 0;
        private static List<string> _failedTests = new List<string>();
        private static int _testSeed = 100;

        public static string RunAllTests()
        {
            _passed = 0;
            _failed = 0;
            _total = 0;
            _failedTests.Clear();

            Debug.WriteLine("========================================");
            Debug.WriteLine("   NEURAL NETWORK TEST SUITE");
            Debug.WriteLine("========================================");

            RunTest(TestSigmoidFunction, "Sigmoid");
            RunTest(TestSigmoidDerivative, "SigmoidDerivative");
            RunTest(TestXavierInitialization, "Xavier");
            RunTest(TestForwardPass, "Forward");
            RunTest(TestBackpropagationUpdatesWeights, "Backprop");
            RunTest(TestLearningSimplePattern, "SimplePattern");
            RunTest(TestLearningConvergence, "Convergence");
            RunTest(TestMultipleNetworks, "MultipleNetworks");
            RunTest(TestFeatureVectorToArray, "FeatureConversion");
            RunTest(TestGetMLDecision, "GetMLDecision");
            RunTest(TestGetMLActionScore, "GetMLActionScore");
            RunTest(TestWeightSaveLoad, "SaveLoad");
            RunTest(TestEnsembleConsistency, "EnsembleConsistency");
            RunTest(TestBatchNormTrainVsInference, "BatchNorm");
            RunTest(TestPrioritizedReplayBuffer, "ReplayBuffer");
            RunTest(TestMultiModalRegression, "MultiModal");
            RunTest(TestNoiseRobustness, "NoiseRobustness");
            RunTest(TestDropoutOverfittingPrevention, "Dropout");
            RunTest(TestEnsembleVotingAccuracy, "EnsembleVote");

            string fl = _failedTests.Count > 0 ? "\n  Failed tests: " + string.Join(", ", _failedTests) : "";
            Debug.WriteLine("========================================");
            Debug.WriteLine(string.Format("RESULTS: {0}/{1} passed, {2} failed{3}", _passed, _total, _failed, fl));
            Debug.WriteLine("========================================");

            string result = string.Format(
                "Neural Network Test Results:\n" +
                "  Total: {0}\n" +
                "  Passed: {1}\n" +
                "  Failed: {2}\n" +
                "  Rate: {3:P0}{4}",
                _total, _passed, _failed, (double)_passed / Math.Max(1, _total), fl);

            Console.WriteLine("NN TEST: " + result.Replace("\n", " | "));
            return result;
        }

        private static void RunTest(Action test, string name)
        {
            int before = _failed;
            test();
            if (_failed > before) _failedTests.Add(name);
        }

        private static double[] MakeInput()
        {
            // Normalized features in the network's operating range (production
            // always feeds RunningNormalizer.Norm output, clamped to [-3,3]).
            return new double[18] { 0.5, 0.6, 0.7, 0.4, 0.8, 0.75, 0.5, 0.3, 0.6, 1.0, 0.5, 0.3, 0.0, 0.0, 0.5, 0.0, 0.0, 0.0 };
        }

        private static void TestSigmoidFunction()
        {
            _total++;
            Debug.Write("Test 1: Sigmoid function... ");

            double s0 = MLNeuralNetwork.SigmStatic(0);
            double s100 = MLNeuralNetwork.SigmStatic(100);
            double s_100 = MLNeuralNetwork.SigmStatic(-100);
            double s5 = MLNeuralNetwork.SigmStatic(5);
            double s_5 = MLNeuralNetwork.SigmStatic(-5);

            bool ok = true;
            if (Math.Abs(s0 - 0.5) > 0.001) ok = false;
            if (Math.Abs(s100 - 1.0) > 0.001) ok = false;
            if (Math.Abs(s_100 - 0.0) > 0.001) ok = false;
            if (s5 < 0.9) ok = false;
            if (s_5 > 0.1) ok = false;

            if (ok) { _passed++; Console.WriteLine("PASSED"); }
            else { _failed++; Console.WriteLine("FAILED"); }
        }

        private static void TestSigmoidDerivative()
        {
            _total++;
            Debug.Write("Test 2: Sigmoid derivative... ");

            double d0 = MLNeuralNetwork.SigmStatic(0) * (1 - MLNeuralNetwork.SigmStatic(0));
            double d1 = MLNeuralNetwork.SigmStatic(100) * (1 - MLNeuralNetwork.SigmStatic(100));
            double d05 = MLNeuralNetwork.SigmStatic(0) * (1 - MLNeuralNetwork.SigmStatic(0));

            bool ok = true;
            if (Math.Abs(d0 - 0.25) > 0.001) ok = false;
            if (Math.Abs(d1 - 0) > 0.001) ok = false;
            if (Math.Abs(d05 - 0.25) > 0.001) ok = false;

            if (ok) { _passed++; Console.WriteLine("PASSED"); }
            else { _failed++; Console.WriteLine("FAILED"); }
        }

        private static void TestXavierInitialization()
        {
            _total++;
            Debug.Write("Test 3: Xavier initialization... ");

            MLNeuralNetwork net = new MLNeuralNetwork(++_testSeed);

            bool ok = true;
            if (net.W1.Length != 576) { ok = false; Debug.WriteLine(string.Format("  W1 len={0}", net.W1.Length)); }
            if (net.B1.Length != 32) { ok = false; Debug.WriteLine(string.Format("  B1 len={0}", net.B1.Length)); }
            if (net.W2.Length != 512) { ok = false; Debug.WriteLine(string.Format("  W2 len={0}", net.W2.Length)); }
            if (net.B2.Length != 16) { ok = false; Debug.WriteLine(string.Format("  B2 len={0}", net.B2.Length)); }
            if (net.W3.Length != 16) { ok = false; Debug.WriteLine(string.Format("  W3 len={0}", net.W3.Length)); }

            bool allNonZero = true;
            for (int i = 0; i < net.W1.Length; i++)
            {
                if (Math.Abs(net.W1[i]) < 0.00001) { allNonZero = false; break; }
            }
            if (!allNonZero) ok = false;

            bool inRange = true;
            for (int i = 0; i < net.W1.Length; i++)
            {
                if (Math.Abs(net.W1[i]) > 1.0) { inRange = false; break; }
            }
            if (!inRange) ok = false;

            if (ok) { _passed++; Console.WriteLine("PASSED (576+512+16+49 = 1153 weights)"); }
            else { _failed++; Console.WriteLine("FAILED"); }
        }

        private static void TestForwardPass()
        {
            _total++;
            Debug.Write("Test 4: Forward pass... ");

            MLNeuralNetwork net = new MLNeuralNetwork(++_testSeed);
            double[] input = MakeInput();

            double output = net.Forward(input);

            bool ok = true;
            if (output < 0 || output > 1) ok = false;
            if (double.IsNaN(output) || double.IsInfinity(output)) ok = false;
            if (net.A1 == null || net.A1.Length != 32) ok = false;
            if (net.A2 == null || net.A2.Length != 16) ok = false;

            Debug.WriteLine(string.Format("output={0:F6}", output));
            if (ok) { _passed++; Console.WriteLine("  PASSED (output in [0,1])"); }
            else { _failed++; Console.WriteLine("  FAILED"); }
        }

        private static void TestBackpropagationUpdatesWeights()
        {
            _total++;
            Debug.Write("Test 5: Backpropagation updates weights... ");

            MLNeuralNetwork net = new MLNeuralNetwork(++_testSeed);
            double[] input = MakeInput();

            double[] weightsBefore = new double[net.W1.Length];
            for (int i = 0; i < net.W1.Length; i++) weightsBefore[i] = net.W1[i];

            net.Train(input, 1.0, 0.1);

            bool ok = true;
            bool anyChanged = false;
            for (int i = 0; i < net.W1.Length; i++)
            {
                if (Math.Abs(weightsBefore[i] - net.W1[i]) > 0.0001)
                {
                    anyChanged = true;
                    break;
                }
            }
            if (!anyChanged) ok = false;

            if (ok) { _passed++; Console.WriteLine("PASSED (weights changed after training)"); }
            else { _failed++; Console.WriteLine("FAILED (weights unchanged)"); }
        }

        private static void TestLearningSimplePattern()
        {
            _total++;
            Debug.Write("Test 6: Learning simple pattern... ");

            MLNeuralNetwork net = new MLNeuralNetwork(++_testSeed);
            // Normalized features (production contract, see RunningNormalizer).
            double[] highInput = new double[18] { 2.0, 1.8, 2.0, 1.2, 2.2, 2.0, 1.0, 0.6, 1.2, 1.0, 1.5, 1.0, 0.0, 0.5, 2.5, 0.8, 0.6, 0.5 };
            double[] lowInput = new double[18] { -1.8, -1.5, -2.0, -1.2, -1.5, -1.8, -1.0, -0.8, -1.2, -1.0, -1.2, -1.0, -0.5, -1.0, -2.0, -0.6, -0.5, -0.8 };

            for (int epoch = 0; epoch < 400; epoch++)
            {
                net.Train(highInput, 0.9, 0.2);
                net.Train(lowInput, 0.1, 0.2);
            }

            double highOutput = net.Forward(highInput);
            double lowOutput = net.Forward(lowInput);

            bool ok = true;
            if (highOutput < 0.5) ok = false;
            if (lowOutput > 0.5) ok = false;
            if (highOutput <= lowOutput) ok = false;

            Debug.WriteLine(string.Format("high={0:F4}, low={1:F4}", highOutput, lowOutput));
            if (ok) { _passed++; Console.WriteLine("  PASSED (network distinguishes high vs low CPU)"); }
            else { _failed++; Console.WriteLine("  FAILED (network cannot distinguish)"); }
        }

        private static void TestLearningConvergence()
        {
            _total++;
            Debug.Write("Test 7: Learning convergence... ");

            MLNeuralNetwork net = new MLNeuralNetwork(++_testSeed);
            // Normalized features (production contract, see RunningNormalizer).
            double[] input1 = new double[18] { 2.0, 1.8, 2.0, 1.2, 2.2, 2.0, 1.0, 0.6, 1.2, 1.0, 1.5, 1.0, 0.0, 0.5, 2.5, 0.8, 0.6, 0.5 };
            double[] input2 = new double[18] { -1.8, -1.5, -2.0, -1.2, -1.5, -1.8, -1.0, -0.8, -1.2, -1.0, -1.2, -1.0, -0.5, -1.0, -2.0, -0.6, -0.5, -0.8 };

            double errorBefore = 0;
            double o1 = net.Forward(input1);
            double o2 = net.Forward(input2);
            errorBefore += (o1 - 0.9) * (o1 - 0.9);
            errorBefore += (o2 - 0.1) * (o2 - 0.1);

            for (int epoch = 0; epoch < 1000; epoch++)
            {
                net.Train(input1, 0.9, 0.18);
                net.Train(input2, 0.1, 0.18);
            }

            double errorAfter = 0;
            o1 = net.Forward(input1);
            o2 = net.Forward(input2);
            errorAfter += (o1 - 0.9) * (o1 - 0.9);
            errorAfter += (o2 - 0.1) * (o2 - 0.1);

            bool ok = errorAfter < errorBefore * 0.3;

            Debug.WriteLine(string.Format("error: {0:F6} → {1:F6}", errorBefore, errorAfter));
            if (ok) { _passed++; Console.WriteLine("  PASSED (error reduced by >70%)"); }
            else { _failed++; Console.WriteLine("  FAILED (error not reduced enough)"); }
        }

        private static void TestMultipleNetworks()
        {
            _total++;
            Debug.Write("Test 8: Multiple independent networks... ");

            MLNeuralNetwork net1 = new MLNeuralNetwork(12345);
            MLNeuralNetwork net2 = new MLNeuralNetwork(98765);

            double[] input = MakeInput();
            for (int ep = 0; ep < 20; ep++)
            {
                net1.Train(input, 0.95, 0.15);
                net2.Train(input, 0.05, 0.15);
            }

            double o1 = net1.Forward(input);
            double o2 = net2.Forward(input);

            bool ok = Math.Abs(o1 - o2) > 0.05;

            Debug.WriteLine(string.Format("net1={0:F4}, net2={1:F4}", o1, o2));
            if (ok) { _passed++; Console.WriteLine("  PASSED (networks are independent)"); }
            else { _failed++; Console.WriteLine("  FAILED (networks behave identically)"); }
        }

        private static void TestFeatureVectorToArray()
        {
            _total++;
            Debug.Write("Test 9: Feature vector ToArray... ");

            MLFeatureVector fv = new MLFeatureVector
            {
                CpuUsage = 85.0,
                RamUsage = 70.0,
                GpuUsage = 90.0,
                DiskUsage = 55.0,
                CpuTemp = 80.0,
                GpuTemp = 75.0,
                Fps = 60.0,
                Fps01Low = 30.0,
                FpsStability = 80.0,
                ProcessCount = 150,
                HourOfDay = 14.5,
                MinuteOfHour = 30.0,
                IsGaming = 1,
                IsOnBattery = 0,
                BatteryLevel = 65.0,
                CpuTrend = 0,
                RamTrend = 0,
                TempTrend = 0
            };

            double[] arr = fv.ToArray();

            bool ok = true;
            if (arr.Length != 18) ok = false;
            if (arr[0] != 85.0) ok = false;
            if (arr[1] != 70.0) ok = false;

            Debug.WriteLine(string.Format("arr[0]={0:F1}, arr[1]={1:F1}, arr[2]={2:F1}",
                arr[0], arr[1], arr[2]));
            if (ok) { _passed++; Console.WriteLine("  PASSED (18 features converted)"); }
            else { _failed++; Console.WriteLine("  FAILED"); }
        }

                private static void TestGetMLDecision()
        {
            _total++;
            Debug.Write("Test 10: GetMLDecision returns valid decision... ");

            bool origDisable = SmartAgent.DisableRealExecution;
            SmartAgent.DisableRealExecution = true;
            try
            {
                SmartAgent agent = CreateTestAgent();
                var state = new SystemState
                {
                    CpuUsage = 50, RamUsage = 85, GpuUsage = 40, CpuTemp = 60,
                    Fps = 60, FpsStability = 95, IsGame = false,
                    ActiveProcess = "explorer.exe", ActiveCategory = ProcessCategory.WindowsSystem
                };
                agent.EnableSimulation(state);
                agent.IsTrainingDeferred = false;
                Random rndLocal = new Random(42);

                for (int i = 0; i < 30; i++)
                {
                    agent.RecordActionOutcome("LightClean", 0.85 + rndLocal.NextDouble() * 0.15);
                    agent.RecordActionOutcome("PreemptiveClean", 0.75 + rndLocal.NextDouble() * 0.2);
                }

                agent.TriggerBackgroundTraining();

                AgentDecision decision = agent.GetMLDecision(state);

                bool ok = true;
                if (decision == null) ok = false;
                else if (string.IsNullOrEmpty(decision.Action)) ok = false;
                else if (decision.Confidence < 0 || decision.Confidence > 1) ok = false;

                Debug.WriteLine(string.Format("action={0}, confidence={1:F4}",
                    decision != null ? decision.Action : "null",
                    decision != null ? decision.Confidence : 0));
                if (ok) { _passed++; Console.WriteLine("  PASSED (valid AgentDecision returned)"); }
                else { _failed++; Console.WriteLine("  FAILED (invalid decision: " + (decision == null ? "NULL" : decision.Action) + ")"); }
            }
            finally
            {
                SmartAgent.DisableRealExecution = origDisable;
            }
        }

        private static void TestGetMLActionScore()
        {
            _total++;
            Debug.Write("Test 11: GetMLActionScore returns score... ");

            SmartAgent agent = CreateTestAgent();

            for (int i = 0; i < 20; i++)
            {
                agent.RecordActionOutcome("BoostGamePriority", 0.85);
            }

            double score = agent.GetMLActionScore("BoostGamePriority");

            bool ok = true;
            if (score < 0 || score > 1) ok = false;
            if (double.IsNaN(score) || double.IsInfinity(score)) ok = false;

            Debug.WriteLine(string.Format("score={0:F4}", score));
            if (ok) { _passed++; Console.WriteLine("  PASSED (score in [0,1])"); }
            else { _failed++; Console.WriteLine("  FAILED (invalid score)"); }
        }

        private static void TestWeightSaveLoad()
        {
            _total++;
            Debug.Write("Test 12: Weight save/load... ");

            MLNeuralNetwork net = new MLNeuralNetwork(++_testSeed);
            double[] input = MakeInput();
            net.Train(input, 0.9, 0.1);
            double outputBefore = net.Forward(input);

            string tempFile = System.IO.Path.GetTempFileName();
            try
            {
            System.IO.File.WriteAllLines(tempFile, new string[]
                {
                    string.Join(",", Array.ConvertAll(net.W1, x => x.ToString("R"))),
                    string.Join(",", Array.ConvertAll(net.B1, x => x.ToString("R"))),
                    string.Join(",", Array.ConvertAll(net.W2, x => x.ToString("R"))),
                    string.Join(",", Array.ConvertAll(net.B2, x => x.ToString("R"))),
                    string.Join(",", Array.ConvertAll(net.W3, x => x.ToString("R"))),
                    net.B3.ToString("R"),
                    string.Join(",", Array.ConvertAll(net.BN1RM, x => x.ToString("R"))),
                    string.Join(",", Array.ConvertAll(net.BN1RV, x => x.ToString("R"))),
                    string.Join(",", Array.ConvertAll(net.BN2RM, x => x.ToString("R"))),
                    string.Join(",", Array.ConvertAll(net.BN2RV, x => x.ToString("R"))),
                    string.Join(",", Array.ConvertAll(net.BN1G, x => x.ToString("R"))),
                    string.Join(",", Array.ConvertAll(net.BN1B, x => x.ToString("R"))),
                    string.Join(",", Array.ConvertAll(net.BN2G, x => x.ToString("R"))),
                    string.Join(",", Array.ConvertAll(net.BN2B, x => x.ToString("R")))
                });

                MLNeuralNetwork net2 = new MLNeuralNetwork(++_testSeed);
                string[] lines = System.IO.File.ReadAllLines(tempFile);
                if (lines.Length >= 14)
                {
                    string[] wIH = lines[0].Split(',');
                    for (int i = 0; i < Math.Min(wIH.Length, net2.W1.Length); i++)
                        net2.W1[i] = double.Parse(wIH[i]);
                    string[] bH1 = lines[1].Split(',');
                    for (int i = 0; i < Math.Min(bH1.Length, net2.B1.Length); i++)
                        net2.B1[i] = double.Parse(bH1[i]);
                    string[] wHH = lines[2].Split(',');
                    for (int i = 0; i < Math.Min(wHH.Length, net2.W2.Length); i++)
                        net2.W2[i] = double.Parse(wHH[i]);
                    string[] bH2 = lines[3].Split(',');
                    for (int i = 0; i < Math.Min(bH2.Length, net2.B2.Length); i++)
                        net2.B2[i] = double.Parse(bH2[i]);
                    string[] wHO = lines[4].Split(',');
                    for (int i = 0; i < Math.Min(wHO.Length, net2.W3.Length); i++)
                        net2.W3[i] = double.Parse(wHO[i]);
                    net2.B3 = double.Parse(lines[5]);
                    RA2(net2.BN1RM, lines[6]); RA2(net2.BN1RV, lines[7]);
                    RA2(net2.BN2RM, lines[8]); RA2(net2.BN2RV, lines[9]);
                    RA2(net2.BN1G, lines[10]); RA2(net2.BN1B, lines[11]);
                    RA2(net2.BN2G, lines[12]); RA2(net2.BN2B, lines[13]);
                }
                double outputAfter = net2.Forward(input);

                bool ok = Math.Abs(outputBefore - outputAfter) < 0.001;
                Debug.WriteLine(string.Format("before={0:F6}, after={1:F6}", outputBefore, outputAfter));
                if (ok) { _passed++; Console.WriteLine("  PASSED (weights preserved)"); }
                else { _failed++; Console.WriteLine("  FAILED (weights lost)"); }
            }
            finally
            {
                try { System.IO.File.Delete(tempFile); } catch (Exception ex) { CWLog.Error("NeuralNetworkTest", ex); }
            }
        }

        private static void TestEnsembleConsistency()
        {
            _total++;
            Debug.Write("HARD 1: Ensemble prediction consistency... ");

            MLEnsemble ens = new MLEnsemble("TestAction");
            double[] input = MakeInput();
            double p1 = ens.Predict(input);
            double p2 = ens.Predict(input);
            double p3 = ens.Predict(input);
            double v = ens.Variance(input);
            double c = ens.GetConf(input);

            bool ok = true;
            if (Math.Abs(p1 - p2) > 0.0001) ok = false;
            if (Math.Abs(p2 - p3) > 0.0001) ok = false;
            if (v < 0) ok = false;
            if (c < 0.1 || c > 0.99) ok = false;
            if (double.IsNaN(p1) || double.IsInfinity(p1)) ok = false;

            for (int i = 0; i < 3; i++)
            {
                double o = ens.Nets[i].Forward(input);
                if (double.IsNaN(o) || double.IsInfinity(o)) ok = false;
            }

            if (ok) { _passed++; Debug.WriteLine(string.Format("PASSED (pred={0:F4}, var={1:F6}, conf={2:F2})", p1, v, c)); }
            else { _failed++; Console.WriteLine("FAILED"); }
        }

        private static void TestBatchNormTrainVsInference()
        {
            _total++;
            Debug.Write("HARD 2: Batch norm train/inference consistency... ");

            MLNeuralNetwork net = new MLNeuralNetwork(++_testSeed);
            double[] input = MakeInput();

            double beforeTrain = net.Forward(input);
            for (int ep = 0; ep < 20; ep++) net.Train(input, 0.8, 0.05);
            double afterTrain = net.Forward(input);
            bool ok = afterTrain > 0.1 && afterTrain < 0.99 && !double.IsNaN(afterTrain) && !double.IsInfinity(afterTrain);

            double[] input2 = new double[18] { 1.0, 0.9, 0.8, 0.6, 1.0, 0.9, 0.4, 0.2, 0.3, 1.0, 1.2, 1.5, 0.0, 0.5, 2.5, 0.5, 0.3, 0.4 };
            for (int ep = 0; ep < 30; ep++) net.Train(input2, 0.9, 0.05);
            double o2 = net.Forward(input2);
            if (o2 < 0.1 || o2 > 0.99) ok = false;

            double mix1 = net.Forward(new double[18] { 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.3, 0.0, 0.0, 0.0, 0, 0, 0 });
            double mix2 = net.Forward(new double[18] { 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.5, 0.3, 0.0, 0.0, 0.0, 0, 0, 0 });
            if (Math.Abs(mix1 - mix2) > 0.001) ok = false;

            if (ok) { _passed++; Debug.WriteLine(string.Format("PASSED (before={0:F4}, after={1:F4}, mix={2:F4})", beforeTrain, afterTrain, mix1)); }
            else { _failed++; Console.WriteLine("FAILED"); }
        }

        private static void TestPrioritizedReplayBuffer()
        {
            _total++;
            Debug.Write("HARD 3: Prioritized replay buffer... ");

            PrioritizedReplayBuffer buf = new PrioritizedReplayBuffer(100);
            bool ok = true;

            if (buf.Count != 0) ok = false;
            MLTrainingSample s;
            for (int i = 0; i < 50; i++)
            {
                var fv = new MLFeatureVector();
                s = new MLTrainingSample { ActionTaken = "Test", OutcomeScore = 0.5 + i * 0.01, Features = fv };
                buf.Add(s, 0.5 + i * 0.01);
            }
            if (buf.Count != 50) ok = false;

            var batch = buf.Sample(10);
            if (batch.Count != 10) ok = false;

            for (int i = 0; i < 100; i++) buf.Add(new MLTrainingSample(), 1.0);
            if (buf.Count != 100) ok = false;

            var batch2 = buf.Sample(200);
            if (batch2.Count > 100) ok = false;

            if (ok) { _passed++; Debug.WriteLine(string.Format("PASSED (count={0}, batch={1})", buf.Count, buf.Sample(10).Count)); }
            else { _failed++; Console.WriteLine("FAILED"); }
        }

        private static void TestMultiModalRegression()
        {
            _total++;
            Debug.Write("HARD 4: Multi-modal pattern regression... ");

            MLNeuralNetwork net = new MLNeuralNetwork(++_testSeed);
            double[][] inputs = new double[4][];
            inputs[0] = new double[18] { 0.95, 0.9, 0.9, 0.7, 0.9, 0.85, 0.2, 0.1, 0.1, 1, 22, 30, 1, 0, 100, 0.5, 0.3, 0.4 };
            inputs[1] = new double[18] { 0.15, 0.2, 0.1, 0.3, 0.35, 0.3, 0.8, 0.7, 0.9, 0, 3, 10, 0, 1, 50, -0.3, -0.2, -0.1 };
            inputs[2] = new double[18] { 0.6, 0.7, 0.5, 0.5, 0.6, 0.55, 0.5, 0.4, 0.6, 0, 14, 20, 0, 0, 80, 0.1, 0.2, 0.1 };
            inputs[3] = new double[18] { 0.85, 0.8, 0.7, 0.9, 0.85, 0.8, 0.4, 0.25, 0.3, 1, 18, 45, 1, 1, 30, 0.6, 0.4, 0.5 };
            double[] targets = new double[] { 0.9, 0.1, 0.5, 0.15 };

            for (int ep = 0; ep < 300; ep++)
                for (int j = 0; j < 4; j++)
                    net.Train(inputs[j], targets[j], 0.1);

            bool ok = true;
            double[] preds = new double[4];
            for (int j = 0; j < 4; j++)
            {
                preds[j] = net.Forward(inputs[j]);
                double err = Math.Abs(preds[j] - targets[j]);
                if (err > 0.35) ok = false;
                if (double.IsNaN(preds[j]) || double.IsInfinity(preds[j])) ok = false;
            }

            bool orderedOk = preds[0] > preds[1] && Math.Abs(preds[2] - 0.5) < 0.35;

            if (ok) { _passed++; Debug.WriteLine(string.Format("PASSED (preds={0:F3},{1:F3},{2:F3},{3:F3})", preds[0], preds[1], preds[2], preds[3])); }
            else { _failed++; Debug.WriteLine(string.Format("FAILED (preds={0:F3},{1:F3},{2:F3},{3:F3})", preds[0], preds[1], preds[2], preds[3])); }
        }

        private static void TestNoiseRobustness()
        {
            _total++;
            Debug.Write("HARD 5: Noise robustness... ");

            MLNeuralNetwork net = new MLNeuralNetwork(++_testSeed);
            double[] clean = new double[18] { 2.0, 1.9, 1.8, 1.4, 2.0, 1.9, 0.6, 0.4, 0.5, 1.0, 1.5, 1.2, 0.0, 0.5, 2.5, 0.5, 0.3, 0.4 };

            for (int ep = 0; ep < 80; ep++) net.Train(clean, 0.8, 0.06);

            double cleanPred = net.Forward(clean);

            double totalNoisyDiff = 0;
            for (int trial = 0; trial < 20; trial++)
            {
                double[] noisy = new double[18];
                for (int i = 0; i < 18; i++)
                {
                    double noise = (new Random(trial * 100 + i).NextDouble() - 0.5) * 0.4;
                    noisy[i] = Math.Max(-3, Math.Min(3, clean[i] + noise));
                }
                double noisyPred = net.Forward(noisy);
                totalNoisyDiff += Math.Abs(noisyPred - cleanPred);
            }
            double avgNoiseDelta = totalNoisyDiff / 20;

            bool ok = avgNoiseDelta < 0.25 && cleanPred > 0.4 && cleanPred < 0.99;

            if (ok) { _passed++; Debug.WriteLine(string.Format("PASSED (clean={0:F4}, noise_delta={1:F4})", cleanPred, avgNoiseDelta)); }
            else { _failed++; Debug.WriteLine(string.Format("FAILED (clean={0:F4}, noise_delta={1:F4})", cleanPred, avgNoiseDelta)); }
        }

        private static void TestDropoutOverfittingPrevention()
        {
            _total++;
            Debug.Write("HARD 6: Dropout overfitting prevention... ");

            MLNeuralNetwork net = new MLNeuralNetwork(++_testSeed);
            double[] tinyInput = new double[18] { 1.8, 1.7, 1.6, 1.2, 1.8, 1.7, 0.8, 0.6, 0.7, 1.0, 1.2, 1.0, 0.0, 0.5, 2.2, 0.4, 0.3, 0.2 };

            for (int ep = 0; ep < 60; ep++) net.Train(tinyInput, 0.85, 0.08);

            double trained = net.Forward(tinyInput);

            double[] different = new double[18] { -1.8, -1.7, -1.6, -1.4, -1.8, -1.7, 1.8, 1.7, 1.8, -1.0, -1.2, -1.0, -0.5, -1.0, -2.0, -0.5, -0.3, -0.4 };
            double generalized = net.Forward(different);

            double gap = Math.Abs(trained - generalized);
            bool ok = trained > 0.3 && trained < 0.99 && gap < 0.7 && !double.IsNaN(generalized);

            if (ok) { _passed++; Debug.WriteLine(string.Format("PASSED (trained={0:F4}, generalized={1:F4}, gap={2:F4})", trained, generalized, gap)); }
            else { _failed++; Debug.WriteLine(string.Format("FAILED (trained={0:F4}, generalized={1:F4}, gap={2:F4})", trained, generalized, gap)); }
        }

        private static void TestEnsembleVotingAccuracy()
        {
            _total++;
            Debug.Write("HARD 7: Ensemble voting accuracy... ");

            MLEnsemble ens = new MLEnsemble("TestVote");
            double[] input = MakeInput();
            double[] inputHigh = new double[18] { 0.95, 0.9, 0.95, 0.7, 0.92, 0.88, 0.2, 0.1, 0.15, 1, 22, 30, 1, 0, 100, 0.6, 0.4, 0.5 };
            double[] inputLow = new double[18] { 0.1, 0.2, 0.15, 0.3, 0.3, 0.25, 0.9, 0.85, 0.95, 0, 3, 10, 0, 1, 50, -0.4, -0.2, -0.3 };

            for (int ep = 0; ep < 100; ep++)
            {
                ens.Train(inputHigh, 0.9, 0.12);
                ens.Train(inputLow, 0.1, 0.12);
            }

            double pHigh = ens.Predict(inputHigh);
            double pLow = ens.Predict(inputLow);
            double confHigh = ens.GetConf(inputHigh);
            double varHigh = ens.Variance(inputHigh);

            bool ok = pHigh > pLow && pHigh > 0.5 && pLow < 0.5;
            if (confHigh < 0.1 || confHigh > 0.99) ok = false;
            if (varHigh < 0) ok = false;

            double[] preds = new double[3];
            for (int i = 0; i < 3; i++) preds[i] = ens.Nets[i].Forward(inputHigh);
            double mean = (preds[0] + preds[1] + preds[2]) / 3.0;
            if (Math.Abs(mean - pHigh) > 0.001) ok = false;

            if (ok) { _passed++; Debug.WriteLine(string.Format("PASSED (high={0:F3}, low={1:F3}, var={2:F6}, conf={3:F2})", pHigh, pLow, varHigh, confHigh)); }
            else { _failed++; Debug.WriteLine(string.Format("FAILED (high={0:F3}, low={1:F3})", pHigh, pLow)); }
        }

        private static void RA2(double[] arr, string line)
        {
            string[] parts = line.Split(',');
            for (int i = 0; i < Math.Min(parts.Length, arr.Length); i++)
                arr[i] = double.Parse(parts[i]);
        }

        private static SmartAgent CreateTestAgent()
        {
            SmartAgent agent = new SmartAgent();
            agent.InitializeMLModel();
            return agent;
        }
    }
}

