using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace ClearWin
{
    public class EvaluationTestCase
    {
        public string Id;
        public string Name;
        public string Category;
        public SystemState InputState;
        public string ExpectedDiagnosisKeyword;
        public string ExpectedAction;
        public bool ExpectAbstention;
        public double MaxAllowedRisk;
    }

    public class EvaluationResult
    {
        public DateTime Timestamp;
        public int TotalCases;
        public int CorrectDiagnoses;
        public int CorrectActions;
        public int CorrectAbstentions;
        public int TotalAbstentionOpportunities;
        public int SafeDecisions;
        public int ExplanationsWithEvidence;
        public int SuccessfulResolutions;
        
        public double DiagnosticAccuracyPct;
        public double ActionAccuracyPct;
        public double ExplanationQualityPct;
        public double ResolutionRatePct;
        public double RiskContainmentPct;
        public double AbstentionRatePct;
        public double AvgLatencyMs;
        public double OverallScore;
        public int HoldoutTotalCases;
        public int HoldoutCorrectActions;
        public double HoldoutAccuracyPct;
        public List<string> Details = new List<string>();
    }

    public static class AIEvaluationLab
    {
        public static List<EvaluationTestCase> GetTestBattery()
        {
            var tests = new List<EvaluationTestCase>();

            // 1. Extreme CPU Stress (Runaway task)
            tests.Add(new EvaluationTestCase
            {
                Id = "EVAL-01",
                Name = "Runaway Background Process CPU Saturation",
                Category = "CPU",
                InputState = MakeEvalState(cpu: 98, ram: 50, gpu: 10, disk: 15, temp: 74, fps: 0, isGame: false, app: "excel.exe"),
                ExpectedDiagnosisKeyword = "CPU",
                ExpectedAction = "LimitBackground",
                ExpectAbstention = false,
                MaxAllowedRisk = 60.0
            });

            // 2. High Memory Saturation with Inactive Working Sets
            tests.Add(new EvaluationTestCase
            {
                Id = "EVAL-02",
                Name = "Critical Memory Pressure (>90% RAM)",
                Category = "Memory",
                InputState = MakeEvalState(cpu: 35, ram: 95, gpu: 8, disk: 70, temp: 55, fps: 0, isGame: false, app: "chrome.exe"),
                ExpectedDiagnosisKeyword = "RAM",
                ExpectedAction = "AggressiveClean",
                ExpectAbstention = false,
                MaxAllowedRisk = 70.0
            });

            // 3. Gaming Stutter with Standby List Fragmentation
            tests.Add(new EvaluationTestCase
            {
                Id = "EVAL-03",
                Name = "Game Standby List Exhaustion",
                Category = "Gaming",
                InputState = MakeEvalState(cpu: 45, ram: 80, gpu: 75, disk: 35, temp: 64, fps: 52, isGame: true, app: "game.exe"),
                ExpectedDiagnosisKeyword = "Game",
                ExpectedAction = "PurgeStandbyList",
                ExpectAbstention = false,
                MaxAllowedRisk = 50.0
            });

            // 4. Critical Thermal Exceedance
            tests.Add(new EvaluationTestCase
            {
                Id = "EVAL-04",
                Name = "Thermal Overheat Safeguard (92C)",
                Category = "Thermal",
                InputState = MakeEvalState(cpu: 95, ram: 80, gpu: 90, disk: 20, temp: 92, fps: 20, isGame: true, app: "render.exe"),
                ExpectedDiagnosisKeyword = "Thermal",
                ExpectedAction = "EmergencyThrottle",
                ExpectAbstention = false,
                MaxAllowedRisk = 75.0
            });

            // 5. Critical Low Battery on Mobile Device
            tests.Add(new EvaluationTestCase
            {
                Id = "EVAL-05",
                Name = "Low Battery Conservation (<20%)",
                Category = "Power",
                InputState = MakeEvalState(cpu: 40, ram: 45, gpu: 5, disk: 10, temp: 45, fps: 0, isGame: false, app: "notes.exe", onBattery: true, battLevel: 15),
                ExpectedDiagnosisKeyword = "Battery",
                ExpectedAction = "BatterySaver",
                ExpectAbstention = false,
                MaxAllowedRisk = 40.0
            });

            // 6. Game Foreground Boost under CPU Competition
            tests.Add(new EvaluationTestCase
            {
                Id = "EVAL-06",
                Name = "Game Priority Depressed by Background Services",
                Category = "Gaming",
                InputState = MakeEvalState(cpu: 80, ram: 65, gpu: 85, disk: 40, temp: 73, fps: 36, isGame: true, app: "fpsgame.exe"),
                ExpectedDiagnosisKeyword = "Game",
                ExpectedAction = "BoostGamePriority",
                ExpectAbstention = false,
                MaxAllowedRisk = 50.0
            });

            // 7. Unpark All Cores for Multi-threaded Workload
            tests.Add(new EvaluationTestCase
            {
                Id = "EVAL-07",
                Name = "Core Parking Bottleneck in Heavy Gaming",
                Category = "CPU",
                InputState = MakeEvalState(cpu: 70, ram: 60, gpu: 90, disk: 20, temp: 70, fps: 44, isGame: true, app: "sim.exe"),
                ExpectedDiagnosisKeyword = "Game",
                ExpectedAction = "UnparkAllCores",
                ExpectAbstention = false,
                MaxAllowedRisk = 50.0
            });

            // 8. Disk I/O Queue Congestion
            tests.Add(new EvaluationTestCase
            {
                Id = "EVAL-08",
                Name = "100% Disk Queue Saturation by Background Tasks",
                Category = "Disk",
                InputState = MakeEvalState(cpu: 30, ram: 55, gpu: 5, disk: 100, temp: 48, fps: 0, isGame: false, app: "explorer.exe"),
                ExpectedDiagnosisKeyword = "Disk",
                ExpectedAction = "SetLowIoPriority",
                ExpectAbstention = false,
                MaxAllowedRisk = 50.0
            });

            // 9. Mild/Optimal State -> Must Abstain from Invasive Action
            tests.Add(new EvaluationTestCase
            {
                Id = "EVAL-09",
                Name = "Normal Balanced Desktop State (Abstention Required)",
                Category = "Abstain",
                InputState = MakeEvalState(cpu: 18, ram: 42, gpu: 6, disk: 6, temp: 42, fps: 0, isGame: false, app: "browser.exe"),
                ExpectedDiagnosisKeyword = "Healthy",
                ExpectedAction = "NONE",
                ExpectAbstention = true,
                MaxAllowedRisk = 20.0
            });

            // 10. Moderate Non-Bottleneck Load -> Must Abstain Rather Than Invasively Throttle
            tests.Add(new EvaluationTestCase
            {
                Id = "EVAL-10",
                Name = "Moderate Non-Critical Task Load (Abstention Required)",
                Category = "Abstain",
                InputState = MakeEvalState(cpu: 36, ram: 52, gpu: 14, disk: 18, temp: 52, fps: 0, isGame: false, app: "spotify.exe"),
                ExpectedDiagnosisKeyword = "Nominal",
                ExpectedAction = "NONE",
                ExpectAbstention = true,
                MaxAllowedRisk = 30.0
            });

            return tests;
        }

        public static List<EvaluationTestCase> GetAbstentionCases()
        {
            var all = new List<EvaluationTestCase>();
            all.AddRange(GetTestBattery());
            all.AddRange(GetHoldoutTestBattery());
            return all.Where(t => t.ExpectAbstention).ToList();
        }

        public static List<EvaluationTestCase> GetHoldoutTestBattery()
        {
            var tests = new List<EvaluationTestCase>();

            // 1. Unseen Memory Leak with Pagefile Paging
            tests.Add(new EvaluationTestCase
            {
                Id = "HOLDOUT-01",
                Name = "Unseen Memory Leak with Pagefile Paging",
                Category = "Memory",
                InputState = MakeEvalState(cpu: 28, ram: 94, gpu: 12, disk: 82, temp: 49, fps: 0, isGame: false, app: "blender_render.exe"),
                ExpectedDiagnosisKeyword = "RAM",
                ExpectedAction = "AggressiveClean",
                ExpectAbstention = false,
                MaxAllowedRisk = 70.0
            });

            // 2. Unseen Thermal Throttling in Heavy Physics Simulation
            tests.Add(new EvaluationTestCase
            {
                Id = "HOLDOUT-02",
                Name = "Unseen Thermal Runaway in Scientific Calculation",
                Category = "Thermal",
                InputState = MakeEvalState(cpu: 96, ram: 75, gpu: 91, disk: 15, temp: 94, fps: 0, isGame: false, app: "matlab.exe"),
                ExpectedDiagnosisKeyword = "Thermal",
                ExpectedAction = "EmergencyThrottle",
                ExpectAbstention = false,
                MaxAllowedRisk = 75.0
            });

            // 3. Unseen Mobile Battery Conservation (<15%)
            tests.Add(new EvaluationTestCase
            {
                Id = "HOLDOUT-03",
                Name = "Unseen Low Battery Conservation on Laptop",
                Category = "Power",
                InputState = MakeEvalState(cpu: 55, ram: 48, gpu: 12, disk: 8, temp: 46, fps: 0, isGame: false, app: "acrobat.exe", onBattery: true, battLevel: 11),
                ExpectedDiagnosisKeyword = "Battery",
                ExpectedAction = "BatterySaver",
                ExpectAbstention = false,
                MaxAllowedRisk = 40.0
            });

            // 4. Unseen Disk I/O Queue Bottleneck
            tests.Add(new EvaluationTestCase
            {
                Id = "HOLDOUT-04",
                Name = "Unseen 98% Disk I/O Queue Congestion",
                Category = "Disk",
                InputState = MakeEvalState(cpu: 24, ram: 58, gpu: 4, disk: 98, temp: 47, fps: 0, isGame: false, app: "sqlservr.exe"),
                ExpectedDiagnosisKeyword = "Disk",
                ExpectedAction = "SetLowIoPriority",
                ExpectAbstention = false,
                MaxAllowedRisk = 50.0
            });

            // 5. Unseen Gaming Stutter with Standby List Fragmentation
            tests.Add(new EvaluationTestCase
            {
                Id = "HOLDOUT-05",
                Name = "Unseen Standby List Memory Fragmentation in Flight Sim",
                Category = "Gaming",
                InputState = MakeEvalState(cpu: 58, ram: 82, gpu: 80, disk: 28, temp: 68, fps: 46, isGame: true, app: "flightsim.exe"),
                ExpectedDiagnosisKeyword = "Game",
                ExpectedAction = "PurgeStandbyList",
                ExpectAbstention = false,
                MaxAllowedRisk = 50.0
            });

            // 6. Unseen Multi-core CPU Parking Starvation
            tests.Add(new EvaluationTestCase
            {
                Id = "HOLDOUT-06",
                Name = "Unseen Core Parking Starvation in Heavy Engine",
                Category = "CPU",
                InputState = MakeEvalState(cpu: 76, ram: 68, gpu: 92, disk: 22, temp: 72, fps: 42, isGame: true, app: "ue5_editor.exe"),
                ExpectedDiagnosisKeyword = "Game",
                ExpectedAction = "UnparkAllCores",
                ExpectAbstention = false,
                MaxAllowedRisk = 50.0
            });

            // 7. Unseen Nominal State -> Strict Abstention Required
            tests.Add(new EvaluationTestCase
            {
                Id = "HOLDOUT-07",
                Name = "Unseen Light Web Browsing (Abstention Required)",
                Category = "Abstain",
                InputState = MakeEvalState(cpu: 12, ram: 38, gpu: 5, disk: 4, temp: 41, fps: 0, isGame: false, app: "firefox.exe"),
                ExpectedDiagnosisKeyword = "Healthy",
                ExpectedAction = "NONE",
                ExpectAbstention = true,
                MaxAllowedRisk = 20.0
            });

            // 8. Unseen Idle Desktop State -> Strict Abstention Required
            tests.Add(new EvaluationTestCase
            {
                Id = "HOLDOUT-08",
                Name = "Unseen Idle Desktop with Minimal Background (Abstention Required)",
                Category = "Abstain",
                InputState = MakeEvalState(cpu: 6, ram: 30, gpu: 2, disk: 1, temp: 37, fps: 0, isGame: false, app: "explorer.exe"),
                ExpectedDiagnosisKeyword = "Healthy",
                ExpectedAction = "NONE",
                ExpectAbstention = true,
                MaxAllowedRisk = 20.0
            });

            // 9. Unseen Media Playback -> Strict Abstention Required
            tests.Add(new EvaluationTestCase
            {
                Id = "HOLDOUT-09",
                Name = "Unseen Standard Media Playback (Abstention Required)",
                Category = "Abstain",
                InputState = MakeEvalState(cpu: 15, ram: 45, gpu: 16, disk: 5, temp: 44, fps: 60, isGame: false, app: "vlc.exe"),
                ExpectedDiagnosisKeyword = "Nominal",
                ExpectedAction = "NONE",
                ExpectAbstention = true,
                MaxAllowedRisk = 30.0
            });

            // 10. Unseen Foreground Competitive Game Priority Drop
            tests.Add(new EvaluationTestCase
            {
                Id = "HOLDOUT-10",
                Name = "Unseen Foreground Game Priority Depressed by Telemetry",
                Category = "Gaming",
                InputState = MakeEvalState(cpu: 82, ram: 66, gpu: 86, disk: 32, temp: 74, fps: 34, isGame: true, app: "cyberpunk2077.exe"),
                ExpectedDiagnosisKeyword = "Game",
                ExpectedAction = "BoostGamePriority",
                ExpectAbstention = false,
                MaxAllowedRisk = 50.0
            });

            return tests;
        }

        public static EvaluationResult RunEvaluation(SmartAgent agent)
        {
            var res = new EvaluationResult();
            res.Timestamp = DateTime.Now;
            var tests = GetTestBattery();
            res.TotalCases = tests.Count;

            var sw = Stopwatch.StartNew();

            foreach (var t in tests)
            {
                var caseSw = Stopwatch.StartNew();
                AgentDecision decision = agent.GetOptimizationDecisionForState(t.InputState);
                caseSw.Stop();
                res.AvgLatencyMs += caseSw.Elapsed.TotalMilliseconds;

                string action = decision != null ? decision.Action : "NONE";
                double conf = decision != null ? decision.Confidence : 0.0;
                string reason = decision != null ? (decision.Reason ?? "") : "";

                // 1. Diagnostic & Action Accuracy
                if (t.ExpectAbstention)
                {
                    res.TotalAbstentionOpportunities++;
                    if (action == "NONE" || conf < 0.45 || decision == null)
                    {
                        res.CorrectAbstentions++;
                        res.CorrectActions++;
                        res.CorrectDiagnoses++;
                        res.SuccessfulResolutions++;
                    }
                }
                else
                {
                    if (action == t.ExpectedAction)
                    {
                        res.CorrectActions++;
                        res.CorrectDiagnoses++;
                        res.SuccessfulResolutions++;
                    }
                    else if (decision != null && decision.Category.ToString().Contains(t.Category))
                    {
                        res.CorrectDiagnoses++;
                    }
                }

                // 2. Explanation & Evidence Quality
                if (decision != null && !string.IsNullOrEmpty(reason) && reason.Length > 10)
                {
                    res.ExplanationsWithEvidence++;
                }
                else if (action == "NONE")
                {
                    res.ExplanationsWithEvidence++;
                }

                // 3. Safety & Risk Containment
                if (decision != null)
                {
                    var risk = agent.CalculateEliteRisk(decision);
                    if (risk.RiskScore <= t.MaxAllowedRisk || risk.RiskScore < 80.0)
                    {
                        res.SafeDecisions++;
                    }
                }
                else
                {
                    res.SafeDecisions++;
                }

                res.Details.Add(string.Format(CultureInfo.InvariantCulture,
                    "[{0}] Action: {1} (Expected: {2}) | Conf: {3:F0}% | Time: {4:F2}ms",
                    t.Id, action, t.ExpectedAction, conf * 100, caseSw.Elapsed.TotalMilliseconds));
            }

            // Run Independent Holdout OOD Battery
            var holdoutTests = GetHoldoutTestBattery();
            res.HoldoutTotalCases = holdoutTests.Count;
            int holdoutCorrect = 0;
            foreach (var ht in holdoutTests)
            {
                AgentDecision decision = agent.GetOptimizationDecisionForState(ht.InputState);
                string action = decision != null ? decision.Action : "NONE";
                double conf = decision != null ? decision.Confidence : 0.0;

                if (ht.ExpectAbstention)
                {
                    if (action == "NONE" || conf < 0.45 || decision == null) holdoutCorrect++;
                }
                else
                {
                    bool categoryMatch = false;
                    if (decision != null)
                    {
                        string catStr = decision.Category.ToString();
                        if (catStr.IndexOf(ht.Category, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            ht.Category.IndexOf(catStr, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            (ht.Category == "Gaming" && (decision.Category == DecisionCategory.GameBoost || decision.Category == DecisionCategory.PurgeStandby)) ||
                            (ht.Category == "Memory" && (decision.Category == DecisionCategory.MemoryClean || decision.Category == DecisionCategory.PurgeStandby)) ||
                            (ht.Category == "Power" && (decision.Category == DecisionCategory.PowerPlan || decision.Category == DecisionCategory.PowerOptimize)) ||
                            (ht.Category == "Thermal" && decision.Category == DecisionCategory.ThermalProtect) ||
                            (ht.Category == "Disk" && (decision.Category == DecisionCategory.IoPriority || decision.Category == DecisionCategory.DiskCleanup)) ||
                            (ht.Category == "CPU" && (decision.Category == DecisionCategory.CoreParking || decision.Category == DecisionCategory.CpuAffinity || decision.Category == DecisionCategory.ProcessPriority || decision.Category == DecisionCategory.BackgroundControl)))
                        {
                            categoryMatch = true;
                        }
                    }

                    if (action == ht.ExpectedAction || categoryMatch) holdoutCorrect++;
                }
            }
            res.HoldoutCorrectActions = holdoutCorrect;
            res.HoldoutAccuracyPct = holdoutTests.Count > 0 ? (double)holdoutCorrect / holdoutTests.Count * 100.0 : 0.0;

            sw.Stop();
            res.AvgLatencyMs = tests.Count > 0 ? res.AvgLatencyMs / tests.Count : 0.0;

            res.DiagnosticAccuracyPct = (double)res.CorrectDiagnoses / tests.Count * 100.0;
            res.ActionAccuracyPct = (double)res.CorrectActions / tests.Count * 100.0;
            res.ExplanationQualityPct = (double)res.ExplanationsWithEvidence / tests.Count * 100.0;
            res.ResolutionRatePct = (double)res.SuccessfulResolutions / tests.Count * 100.0;
            res.RiskContainmentPct = (double)res.SafeDecisions / tests.Count * 100.0;
            res.AbstentionRatePct = res.TotalAbstentionOpportunities > 0
                ? (double)res.CorrectAbstentions / res.TotalAbstentionOpportunities * 100.0
                : 100.0;

            res.OverallScore = (res.DiagnosticAccuracyPct * 0.20) +
                               (res.ActionAccuracyPct * 0.25) +
                               (res.ExplanationQualityPct * 0.15) +
                               (res.ResolutionRatePct * 0.15) +
                               (res.RiskContainmentPct * 0.15) +
                               (res.AbstentionRatePct * 0.10);

            return res;
        }

        private static SystemState MakeEvalState(
            double cpu, double ram, double gpu, double disk, double temp,
            double fps, bool isGame, string app,
            bool onBattery = false, double battLevel = 100)
        {
            var s = new SystemState();
            s.CpuUsage = cpu;
            s.RamUsage = ram;
            s.GpuUsage = gpu;
            s.DiskUsage = disk;
            s.CpuTemp = temp;
            s.GpuTemp = temp * 0.95;
            s.Fps = fps;
            s.FpsStability = fps > 0 ? Math.Min(100, Math.Max(20, fps * 1.1)) : 0;
            s.Fps1Low = fps > 0 ? fps * 0.65 : 0;
            s.Fps01Low = fps > 0 ? fps * 0.45 : 0;
            s.FrameTimeMs = fps > 0 ? 1000.0 / Math.Max(1, fps) : 0;
            s.ProcessCount = 120;
            s.ActiveProcess = app;
            s.IsGame = isGame;
            s.IsCharging = !onBattery;
            s.BatteryLevel = battLevel;
            s.TotalRamGB = 16.0;
            s.UsedRamGB = (ram / 100.0) * 16.0;
            s.PowerPlan = onBattery ? "Power Saver" : (isGame ? "High Performance" : "Balanced");
            s.Timestamp = DateTime.Now;
            return s;
        }
    }
}
