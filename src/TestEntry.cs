using System;
using System.Diagnostics;

namespace ClearWin
{
    public static class TestRunner
    {
        static int _passed = 0;
        static int _failed = 0;
        static int _total = 0;
        static Stopwatch _sw = new Stopwatch();
        static string _filter = null;

        public static void Main(string[] args)
        {
            if (args != null && args.Length > 0) _filter = args[0];
            string envFilter = Environment.GetEnvironmentVariable("CW_TEST_FILTER");
            if (!string.IsNullOrEmpty(envFilter)) _filter = envFilter;

            // Never touch the real system during automated tests
            SmartAgent.DisableRealExecution = true;
            Environment.SetEnvironmentVariable("CLEARWIN_TEST_REG_PREFIX", @"SOFTWARE\ClearWin\TestSandbox");
            Console.WriteLine("╔══════════════════════════════════════╗");
            Console.WriteLine("║       ClearWin Unit Tests v2.6      ║");
            Console.WriteLine("╚══════════════════════════════════════╝");
            Console.WriteLine();
            _sw.Start();

            Run("Log: Duplicate Suppression", UnitTests.TestCWLogDuplicateSuppressionPublic);
            Run("Log: Bounded Table Eviction", UnitTests.TestCWLogEvictionPublic);
            Run("Perf: Process Cache Concurrency", UnitTests.TestProcessCacheConcurrencyPublic);
            Run("Budget", UnitTests.TestBudgetPublic);
            Run("Confidence Range", UnitTests.TestConfidencePublic);
            Run("Cost Calculations", UnitTests.TestCostsPublic);
            Run("Preconditions", UnitTests.TestPreconditionsPublic);
            Run("Conflict Detection", UnitTests.TestConflictsPublic);
            Run("Telemetry Tracking", UnitTests.TestTelemetryPublic);
            Run("Analyzer Interface", UnitTests.TestAnalyzerInterfacePublic);
            Run("Mock Collector", UnitTests.TestMockCollectorPublic);
            Run("Cancellation Token", UnitTests.TestCancellationPublic);
            Run("Rollback Logic", UnitTests.TestRollbackPublic);
            Run("Dependency Graph", UnitTests.TestDependencyGraphPublic);
            Run("Memory Caps", UnitTests.TestMemoryCapsPublic);
            Run("Aging Logic", UnitTests.TestAgingPublic);
            Run("Lock Ordering", UnitTests.TestLockOrderingPublic);
            Run("Save/Load Round Trip", UnitTests.TestSaveLoadRoundTripPublic);
            Run("Empty State", UnitTests.TestEmptyStatePublic);
            Run("Extreme Values", UnitTests.TestExtremeValuesPublic);
            Run("Null Safety", UnitTests.TestNullSafetyPublic);
            Run("Mode Transitions", UnitTests.TestModeTransitionsPublic);
            Run("Cooldown Logic", UnitTests.TestCooldownPublic);
            Run("Decision Priority", UnitTests.TestDecisionPriorityPublic);
            Run("Knapsack Budget", UnitTests.TestKnapsackBudgetPublic);
            Run("Health Score", UnitTests.TestHealthScorePublic);
            Run("Action Exec Times Cap", UnitTests.TestActionExecTimesCapPublic);
            Run("SystemState Clone", UnitTests.TestSystemStateClonePublic);
            Run("FrameTime Tracker", UnitTests.TestFrameTimeTrackerPreallocPublic);
            Run("Outcome-Verified Learning", UnitTests.TestOutcomeVerifiedLearningPublic);
            Run("FPS: Stabilizer Core", UnitTests.TestFpsStabilizerCorePublic);
            Run("FPS: Drop Detection", UnitTests.TestFpsStabilizerDropDetectionPublic);
            Run("FPS: Drop Prediction", UnitTests.TestFpsStabilizerPredictionPublic);
            Run("FPS: Prevention Counting", UnitTests.TestFpsStabilizerPreventionPublic);
            Run("FPS: Drop Not Prevented", UnitTests.TestFpsStabilizerDropNotPreventedPublic);
            Run("FPS: Predictors", UnitTests.TestFpsStabilizerPredictorsPublic);
            Run("FPS: Cooldown Cap", UnitTests.TestFpsStabilizerCooldownPublic);
            Run("FPS: SmartAgent Integration", UnitTests.TestSmartAgentFpsStabilizerPublic);
            Run("FPS: 60 Floor Mode", UnitTests.TestFpsStabilizerFloorModePublic);
            Run("FPS: Agent Floor/FullOpt", UnitTests.TestSmartAgentFloorModesPublic);
            Run("FPS: Decision Demo (easy->brutal->recover)", UnitTests.TestFpsStabilizerDecisionDemoPublic);
            Run("AnalyzerManager Thread Safety", UnitTests.TestAnalyzerManagerThreadSafetyPublic);
            Run("Adaptive Thresholds", UnitTests.TestAdaptiveThresholdsPublic);
            Run("AI: Agent Init", UnitTests.TestSmartAgentInitPublic);
            Run("AI: High CPU Response", UnitTests.TestSmartAgentHighCPUPublic);
            Run("AI: High RAM Response", UnitTests.TestSmartAgentHighRAMPublic);
            Run("AI: Thermal Throttle", UnitTests.TestSmartAgentThermalThrottlePublic);
            Run("AI: Gaming Scenario", UnitTests.TestSmartAgentGamingPublic);
            Run("AI: Reinforcement Learning", UnitTests.TestSmartAgentRLPublic);
            Run("AI: Causal AI", UnitTests.TestSmartAgentCausalAIPublic);
            Run("AI: Multi-Agent Voting", UnitTests.TestSmartAgentMultiAgentPublic);
            Run("AI: World Model", UnitTests.TestSmartAgentWorldModelPublic);
            Run("AI: Risk Engine", UnitTests.TestSmartAgentRiskEnginePublic);
            Run("AI: Tool Training", UnitTests.TestToolTrainingPublic);
            Run("AI: Tool Execution", UnitTests.TestToolExecutionPublic);
            Run("AI: Health Score", UnitTests.TestSmartAgentHealthScorePublic);
            Run("AI: Decision Pipeline", UnitTests.TestSmartAgentDecisionPipelinePublic);
            Run("AI: KillSwitch Emergency", UnitTests.TestKillSwitchPublic);
            Run("AI: Pause/Resume", UnitTests.TestPauseResumePublic);
            Run("AI: Extreme Thermal Cascade", UnitTests.TestExtremeThermalCascadePublic);
            Run("AI: Budget Exhaustion", UnitTests.TestBudgetExhaustionPublic);
            Run("AI: Safe Mode Trigger", UnitTests.TestSafeModeTriggerPublic);
            Run("AI: Mode Storm", UnitTests.TestModeStormPublic);
            Run("AI: Null State Fields", UnitTests.TestNullStateFieldsPublic);
            Run("AI: All Preconditions Fail", UnitTests.TestAllPreconditionsFailPublic);
            Run("AI: Tool Edge Cases", UnitTests.TestToolEdgeCasesPublic);
            Run("Arch: RingBuffer", UnitTests.TestRingBufferPublic);
            Run("Arch: MessageBus", UnitTests.TestMessageBusPublic);
            Run("Arch: RL/Reward Interfaces", UnitTests.TestAgentInterfacesPublic);
            Run("AI: World Model Stress", UnitTests.TestWorldModelStressPublic);
            Run("AI: RL Exploration Floor", UnitTests.TestRLExplorationFloorPublic);
            Run("AI: Conflict Cascade", UnitTests.TestConflictCascadePublic);
            Run("AI: Game Exit Mid-Opt", UnitTests.TestGameExitMidOptPublic);
            Run("AI: Concurrent Tick Stress", UnitTests.TestConcurrentTickStressPublic);
            Run("AI: Emergency Stop All", UnitTests.TestEmergencyStopAllPublic);
            Run("AI: Rapid Health Swing", UnitTests.TestRapidHealthSwingPublic);
            Run("AI: Zero Resource State", UnitTests.TestZeroResourceStatePublic);
            Run("ES: Core Init", UnitTests.TestRuleEngineInitPublic);
            Run("ES: Knowledge Base", UnitTests.TestRuleKnowledgeBasePublic);
            Run("ES: Reasoning Engine", UnitTests.TestReasoningEnginePublic);
            Run("ES: Planner", UnitTests.TestPlannerPublic);
            Run("ES: Decision Engine", UnitTests.TestDecisionEnginePublic);
            Run("ES: Action Validator", UnitTests.TestActionValidatorPublic);
            Run("ES: Safety Engine", UnitTests.TestSafetyEnginePublic);
            Run("ES: Learning Engine", UnitTests.TestLearningEnginePublic);
            Run("ES: Memory Engine", UnitTests.TestMemoryEnginePublic);
            Run("ES: Diagnostic Engine", UnitTests.TestDiagnosticEnginePublic);
            Run("ES: Explain Engine", UnitTests.TestExplainEnginePublic);
            Run("ES: Full Pipeline", UnitTests.TestRuleEnginePipelinePublic);
            Run("ES: Persistence", UnitTests.TestRuleEnginePersistencePublic);
            Run("ES: SmartAgent Integration", UnitTests.TestSmartAgentExpertIntegrationPublic);
            Run("NN: Neural Network Hard Tests", NeuralNetworkHardTests);
            Run("OptAI: Learning 1000 Decisions", OptimizationAITest.TestLearningPublic);
            Run("OptAI: Stability", OptimizationAITest.TestStabilityPublic);
            Run("OptAI: Thermal Profiles", OptimizationAITest.TestThermalProfilesPublic);
            Run("OptAI: RAM Profiles", OptimizationAITest.TestRamProfilesPublic);
            Run("OptAI: Gaming Profiles", OptimizationAITest.TestGamingProfilesPublic);
            Run("OptAI: Battery Profiles", OptimizationAITest.TestBatteryProfilesPublic);
            Run("OptAI: Feature Importance", OptimizationAITest.TestFeatureImportancePublic);
            Run("OptAI: Confidence Calibration", OptimizationAITest.TestConfidenceCalibrationPublic);
            Run("OptAI: Training Metrics", OptimizationAITest.TestTrainingMetricsPublic);
            Run("OptAI: ML Save/Load Round Trip", OptimizationAITest.TestMLSaveLoadRoundTripPublic);
            Run("OptAI: Fault Tolerance", OptimizationAITest.TestModelFaultTolerancePublic);
            Run("OptAI: Analysis Stress", OptimizationAITest.TestAnalysisStressPublic);
            Run("OptAI: Auto Rollback", OptimizationAITest.TestRollbackPublic);
            Run("OptAI: Quality Metrics", OptimizationAITest.TestQualityPublic);
            Run("OptAI: Real RL2 State-Aware TD", OptimizationAITest.TestRL2Public);
            Run("OptAI: Real Outcome Reward Engine", OptimizationAITest.TestRewardEnginePublic);
            Run("OptAI: Honest Metrics", OptimizationAITest.TestHonestMetricsPublic);
            Run("OptAI: Prior Knowledge Pretraining", OptimizationAITest.TestPriorPretrainPublic);
            Run("OptAI: Counterfactual Reward (DiD)", OptimizationAITest.TestCounterfactualRewardPublic);
            Run("OptAI: Calibrated Abstention", OptimizationAITest.TestCalibratedAbstentionPublic);
            Run("OptAI: Source-Level Trust", OptimizationAITest.TestSourceTrustPublic);
            Run("OptAI: Context-Aware State Key", OptimizationAITest.TestContextStateKeyPublic);
            Run("OptAI: Frame Pacing Profiles", OptimizationAITest.TestFramePacingProfilePublic);
            Run("OptAI: Frame Pacing Refresh Targets", OptimizationAITest.TestFramePacingRefreshPublic);
            Run("OptAI: Frame Pacing Smoothness", OptimizationAITest.TestFramePacingSmoothnessPublic);
            Run("OptAI: Network Tuning Profiles", OptimizationAITest.TestNetworkTuningProfilePublic);
            Run("OptAI: Network Tuning Settings", OptimizationAITest.TestNetworkTuningSettingsPublic);
            Run("OptAI: Network Tuning Maintenance", OptimizationAITest.TestNetworkTuningMaintenancePublic);
            Run("OptAI: Real Log Import", OptimizationAITest.TestRealLogImportPublic);
            Run("HW: Vendor GPU Temperature (NVAPI/ADL)", OptimizationAITest.TestGpuMonitorPublic);
            Run("HW: DXGI Displayed FPS Reader", OptimizationAITest.TestDxgiFpsPublic);
            Run("HW: Startup Classifier + Safe Disable/Restore", OptimizationAITest.TestStartupClassifierPublic);
            Run("LocalWebBridge: Security & Integrity", UnitTests.TestLocalWebBridgeSecurityPublic);
            Run("Tweaks: State-Preserving Lifecycle (Apply/Undo/Integrity)", UnitTests.TestTweakLifecyclePublic);
            Run("Tweaks: Backup Engine Multi-Type & 27 Tweaks Integrity", UnitTests.TestBackupEngineExtendedCapabilitiesPublic);
            Run("AI: Specialized Windows Diagnostic Curriculum", UnitTests.TestCurriculumDatasetPublic);
            Run("AI: Training Pipeline & Independent Evaluation Lab", UnitTests.TestAIEvaluationLabPublic);
            Run("Autonomy: Action Catalog Classification & Risk Tiers", UnitTests.TestActionCatalogIntegrityPublic);
            Run("Autonomy: Policy Gate Red Autonomous Block", UnitTests.TestPolicyGateRedAutonomousBlockPublic);
            Run("Autonomy: Policy Gate Yellow Confirmation", UnitTests.TestPolicyGateYellowConfirmationPublic);
            Run("Autonomy: Policy Gate Green Default-Off", UnitTests.TestPolicyGateGreenDefaultOffPublic);
            Run("Autonomy: Policy Gate Green Session Opt-In", UnitTests.TestPolicyGateGreenSessionOptInPublic);
            Run("Autonomy: Audit Logger Serialization & Redaction", UnitTests.TestAuditLoggerIntegrityPublic);
            Run("Autonomy: Admin Helper Execution & Shell Denial", UnitTests.TestAdminHelperSecurityPublic);
            Run("Autonomy: Admin Helper Handshake Security", UnitTests.TestAdminHelperHandshakeSecurityPublic);
            Run("WebUI: Page Key Validation & Fallback", UnitTests.TestWebUiPageKeyValidationPublic);
            Run("WebUI: Action Expected Page Mapping", UnitTests.TestWebUiActionPageMappingPublic);
            Run("WebUI: Stale Result & Generation Suppression", UnitTests.TestWebUiStaleResultSuppressionAndGenerationPublic);
            Run("WebUI: Cleanup Scan Bounded & Cache TTL", UnitTests.TestWebUiCleanupScanCachingAndBoundedExecutionPublic);
            Run("WebUI: Safe Process Listing & Resource Cleanup", UnitTests.TestWebUiProcessListingSafetyPublic);
            Run("WebUI: 100-Navigation Stress Harness", UnitTests.TestWebUiNavigationStressHarnessPublic);
            Run("AI: Decision Logger & Persistent Formatting", UnitTests.TestAiDecisionLoggerPersistenceAndFormattingPublic);
            Run("AI: Load Reduction & Memory Impact Verification", UnitTests.TestAiLoadReductionVerificationPublic);
            Run("AI: GameLoop & Emulator Auto-Detection", UnitTests.TestGameLoopAndEmulatorDetectionPublic);
            Run("AI: Game CPU/GPU Load Balancing", UnitTests.TestGameLoadBalancingDecisionsPublic);
            Run("Settings: System Tray & Startup Stabilization", UnitTests.TestTrayAndStartupSettingsPublic);
            Run("Settings: Dedicated GPU Routing & Ultimate Power", UnitTests.TestGameDedicatedGpuAndUltimatePowerPublic);
            Run("AI: Gaming 60 FPS Floor & Aggressive Lock", UnitTests.TestSmartAgentGaming60FpsFloorPublic);
            Run("AI: Process Classification & PUBG GameLoop Detection", UnitTests.TestAppClassificationAndGameLoopPubgDetectionPublic);
            Run("AI: Autonomy Allowlist & Deadlock-Free Safety Gate", UnitTests.TestAiAutonomyAndDeadlockFreeSafetyPublic);
            Run("AI: Neural Weights & Decision Consistency", UnitTests.TestNeuralEnsembleWeightsAndConfidencePublic);
            Run("AI Stability: Training Deferral During Active Game", UnitTests.TestTrainingDeferredDuringActiveGamePublic);
            Run("AI Stability: Post-Game Stabilization & Training Resume", UnitTests.TestPostGameStabilizationAndTrainingResumePublic);
            Run("AI Stability: Training Deduplication & Exception Recovery", UnitTests.TestTrainingDeduplicationAndExceptionRecoveryPublic);
            Run("AI Stability: Prior Knowledge Skip & Checksum Integrity", UnitTests.TestPriorKnowledgeSkipAndModelPersistencePublic);
            Run("AI Performance: ML Prediction Caching & CPU Optimization", UnitTests.TestTickPredictionCachingAndCpuOptimizationPublic);
            Run("AI Memory: Deep Compaction & Multi-Stage Purge", UnitTests.TestDeepMemoryCompactionAndMultiStagePurgePublic);
            Run("AI: Desktop Smoothness & Gaming Mode Duality", UnitTests.TestDesktopSmoothnessAndGamingDualityPublic);
            Run("AI Core: Replay Buffer Importance Sampling", UnitTests.TestReplayBufferImportanceSamplingPublic);
            Run("AI Core: Sensor Anomaly Rejection Guard", UnitTests.TestSensorAnomalyRejectionPublic);
            Run("AI Core: Quantile Latency p50/p95/p99 & Jitter", UnitTests.TestQuantileLatencyP99Public);
            Run("AI Core: PSI Distribution Drift Detector", UnitTests.TestPSIDriftDetectorPublic);
            Run("AI GPU: Performance Routing & VRAM Shedding", UnitTests.TestGpuBoostAndVramTrimExecutionPublic);
            Run("AI GPU: Shader Cache Micro-Stutter Purge", UnitTests.TestShaderCachePurgeSafetyPublic);
            Run("AI Multi-Tier: Hardware Curriculum Adaptability", UnitTests.TestMultiTierHardwareCurriculumPublic);
            Run("AI Stress: 1-Hour Telemetry Trace (10,800 Ticks)", UnitTests.TestOneHourHardwareSimulationTracePublic);
            Run("v2.7.7 Security & Engineering Audit Fixes", UnitTests.TestV277SecurityAndEngineeringAuditFixesPublic);
            Run("v2.7.7 Independent Verification Suite (ML/RL2/GPU)", UnitTests.TestV277IndependentVerificationSuitePublic);
            Run("v2.7.7 Phase 1: Privileges, Restore/DNS & Depth JSON Parser", UnitTests.TestV277Phase1PrivilegesAndJsonParserPublic);
            Run("v2.7.7 Phase 2: Registry Journal & Power Plan State Machine", UnitTests.TestV277Phase2RegistryAndPowerStatePublic);
            Run("v2.7.7 Phase 3 & 5: ML Leakage/Normalizer/V4 & RL2 Double-Q", UnitTests.TestV277Phase3And5MLAndRL2FixesPublic);

            _sw.Stop();
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine(string.Format("  Results: {0} passed, {1} failed, {2} total", _passed, _failed, _total));
            Console.WriteLine(string.Format("  Time: {0}ms", _sw.ElapsedMilliseconds));
            Console.WriteLine("═══════════════════════════════════════");

            if (_failed > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  STATUS: FAILED");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("  STATUS: ALL PASSED");
                Console.ResetColor();
            }

            Environment.Exit(_failed > 0 ? 1 : 0);
        }

        static void NeuralNetworkHardTests()
        {
            string result = NeuralNetworkTest.RunAllTests();
            Debug.WriteLine(result);
            if (result.Contains("Failed: 0") || result.Contains("100%"))
                return;
            throw new Exception("Neural network hard tests failed:\n" + result);
        }

        static void Run(string name, Action test)
        {
            if (!string.IsNullOrEmpty(_filter) && name.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0) return;
            _total++;
            Stopwatch t = new Stopwatch();
            try
            {
                t.Start();
                test();
                t.Stop();
                _passed++;
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("  ✓ ");
                Console.ResetColor();
                Console.WriteLine(string.Format("{0,-35} {1}ms", name, t.ElapsedMilliseconds));
            }
            catch (Exception ex)
            {
                t.Stop();
                _failed++;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("  ✗ ");
                Console.ResetColor();
                Console.WriteLine(string.Format("{0,-35} FAIL: {1}", name, ex.Message));
            }
        }
    }
}
