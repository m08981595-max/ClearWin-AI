using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ClearWin
{
    public class SimulationScenario
    {
        public string Name;
        public string NameAr;
        public string Description;
        public double CpuUsage;
        public double RamUsage;
        public double GpuUsage;
        public double DiskUsage;
        public double CpuTemp;
        public double GpuTemp;
        public double Fps;
        public double FrameTimeMs;
        public int ProcessCount;
        public int ThreadCount;
        public long SentBytes;
        public long RecvBytes;
        public string ActiveProcess;
        public bool IsGame;
        public bool IsCharging;
        public double BatteryLevel;
        public string PowerPlan;
        public int CpuCoreCount;
        public double CpuFreqGHz;
        public double TotalRamGB;
        public double UsedRamGB;

        public SystemState ToSystemState()
        {
            return new SystemState
            {
                CpuUsage = CpuUsage,
                RamUsage = RamUsage,
                GpuUsage = GpuUsage,
                DiskUsage = DiskUsage,
                CpuTemp = CpuTemp,
                GpuTemp = GpuTemp,
                Fps = Fps,
                FrameTimeMs = FrameTimeMs,
                Fps1Low = Fps > 0 ? Fps * 0.85 : 0,
                Fps01Low = Fps > 0 ? Fps * 0.70 : 0,
                ProcessCount = ProcessCount,
                ThreadCount = ThreadCount,
                SentBytes = SentBytes,
                RecvBytes = RecvBytes,
                ActiveProcess = ActiveProcess,
                ActiveProcessPath = "",
                IsGame = IsGame,
                IsCharging = IsCharging,
                BatteryLevel = BatteryLevel,
                PowerPlan = PowerPlan ?? "High Performance",
                CpuCoreCount = CpuCoreCount,
                CpuFreqGHz = CpuFreqGHz,
                TotalRamGB = TotalRamGB,
                UsedRamGB = UsedRamGB,
                Timestamp = DateTime.Now,
                FpsStability = Fps > 0 ? Math.Max(0, 100 - FrameTimeMs * 5) : 0,
                FrameTimeJitter = FrameTimeMs * 0.15
            };
        }
    }

    public class SimTestResult
    {
        public string ScenarioName;
        public DateTime Timestamp;
        public int TickCount;
        public double DurationMs;
        public int DecisionsMade;
        public int ActionsExecuted;
        public double HealthScoreBefore;
        public double HealthScoreAfter;
        public List<string> ActionsLog = new List<string>();
        public List<string> SuggestionsLog = new List<string>();
        public List<string> WarningsLog = new List<string>();
        public string Summary;
    }

    public static class SimulatorEngine
    {
        public static List<SimulationScenario> GetPresetScenarios()
        {
            return new List<SimulationScenario>
            {
                new SimulationScenario
                {
                    Name = "High CPU Load",
                    NameAr = "حمل معالج عالي",
                    Description = "CPU at 95% with normal temps - testing CPU throttling decisions",
                    CpuUsage = 95, RamUsage = 55, GpuUsage = 30, DiskUsage = 20,
                    CpuTemp = 72, GpuTemp = 45, Fps = 0, FrameTimeMs = 0,
                    ProcessCount = 180, ThreadCount = 2200, SentBytes = 100000, RecvBytes = 50000,
                    ActiveProcess = "devenv.exe", IsGame = false, IsCharging = true,
                    BatteryLevel = 100, PowerPlan = "High Performance",
                    CpuCoreCount = 8, CpuFreqGHz = 3.6, TotalRamGB = 16, UsedRamGB = 9
                },
                new SimulationScenario
                {
                    Name = "Low RAM Critical",
                    NameAr = "ذاكرة عشوائية منخفضة حرجة",
                    Description = "RAM at 92% with Chrome consuming 4GB - testing memory cleanup",
                    CpuUsage = 45, RamUsage = 92, GpuUsage = 20, DiskUsage = 35,
                    CpuTemp = 58, GpuTemp = 40, Fps = 0, FrameTimeMs = 0,
                    ProcessCount = 250, ThreadCount = 3100, SentBytes = 200000, RecvBytes = 500000,
                    ActiveProcess = "chrome.exe", IsGame = false, IsCharging = false,
                    BatteryLevel = 65, PowerPlan = "Balanced",
                    CpuCoreCount = 8, CpuFreqGHz = 3.2, TotalRamGB = 8, UsedRamGB = 7.4
                },
                new SimulationScenario
                {
                    Name = "Thermal Throttling",
                    NameAr = "تخفيض حراري",
                    Description = "CPU at 92C, GPU at 88C - testing thermal protection",
                    CpuUsage = 88, RamUsage = 70, GpuUsage = 95, DiskUsage = 25,
                    CpuTemp = 92, GpuTemp = 88, Fps = 45, FrameTimeMs = 22,
                    ProcessCount = 200, ThreadCount = 2500, SentBytes = 50000, RecvBytes = 30000,
                    ActiveProcess = "Game.exe", IsGame = true, IsCharging = true,
                    BatteryLevel = 80, PowerPlan = "High Performance",
                    CpuCoreCount = 8, CpuFreqGHz = 3.8, TotalRamGB = 32, UsedRamGB = 22
                },
                new SimulationScenario
                {
                    Name = "Gaming Session",
                    NameAr = "جلسة ألعاب",
                    Description = "Game running, GPU 90%, good FPS - testing GameBoost",
                    CpuUsage = 65, RamUsage = 75, GpuUsage = 90, DiskUsage = 15,
                    CpuTemp = 75, GpuTemp = 78, Fps = 120, FrameTimeMs = 8.3,
                    ProcessCount = 150, ThreadCount = 1800, SentBytes = 10000, RecvBytes = 5000,
                    ActiveProcess = "Cyberpunk2077.exe", IsGame = true, IsCharging = true,
                    BatteryLevel = 100, PowerPlan = "High Performance",
                    CpuCoreCount = 12, CpuFreqGHz = 4.2, TotalRamGB = 32, UsedRamGB = 24
                },
                new SimulationScenario
                {
                    Name = "Battery Critical",
                    NameAr = "بطارية حرجة",
                    Description = "Laptop at 8% battery - testing power saving decisions",
                    CpuUsage = 35, RamUsage = 50, GpuUsage = 15, DiskUsage = 10,
                    CpuTemp = 42, GpuTemp = 38, Fps = 0, FrameTimeMs = 0,
                    ProcessCount = 120, ThreadCount = 1400, SentBytes = 50000, RecvBytes = 100000,
                    ActiveProcess = "explorer.exe", IsGame = false, IsCharging = false,
                    BatteryLevel = 8, PowerPlan = "Balanced",
                    CpuCoreCount = 4, CpuFreqGHz = 2.4, TotalRamGB = 8, UsedRamGB = 4
                },
                new SimulationScenario
                {
                    Name = "SSD Full - Disk Critical",
                    NameAr = "قرص ممتلئ",
                    Description = "Disk usage 97% - testing disk cleanup and purge decisions",
                    CpuUsage = 30, RamUsage = 45, GpuUsage = 10, DiskUsage = 97,
                    CpuTemp = 45, GpuTemp = 35, Fps = 0, FrameTimeMs = 0,
                    ProcessCount = 100, ThreadCount = 1200, SentBytes = 5000, RecvBytes = 10000,
                    ActiveProcess = "explorer.exe", IsGame = false, IsCharging = true,
                    BatteryLevel = 100, PowerPlan = "Balanced",
                    CpuCoreCount = 8, CpuFreqGHz = 3.0, TotalRamGB = 16, UsedRamGB = 7
                },
                new SimulationScenario
                {
                    Name = "Video Editing Workload",
                    NameAr = "حمل فيديو تحرير",
                    Description = "High CPU+GPU+RAM all at once - testing multi-resource optimization",
                    CpuUsage = 85, RamUsage = 82, GpuUsage = 78, DiskUsage = 65,
                    CpuTemp = 80, GpuTemp = 76, Fps = 30, FrameTimeMs = 33,
                    ProcessCount = 170, ThreadCount = 2000, SentBytes = 500000, RecvBytes = 1000000,
                    ActiveProcess = "AdobePremiere.exe", IsGame = false, IsCharging = true,
                    BatteryLevel = 100, PowerPlan = "High Performance",
                    CpuCoreCount = 16, CpuFreqGHz = 3.5, TotalRamGB = 64, UsedRamGB = 52
                },
                new SimulationScenario
                {
                    Name = "Windows Update Storm",
                    NameAr = "عاصفة تحديث ويندوز",
                    Description = "Update consuming resources while user is active",
                    CpuUsage = 70, RamUsage = 60, GpuUsage = 5, DiskUsage = 80,
                    CpuTemp = 62, GpuTemp = 35, Fps = 0, FrameTimeMs = 0,
                    ProcessCount = 220, ThreadCount = 2800, SentBytes = 50000, RecvBytes = 2000000,
                    ActiveProcess = "TiWorker.exe", IsGame = false, IsCharging = true,
                    BatteryLevel = 90, PowerPlan = "Balanced",
                    CpuCoreCount = 8, CpuFreqGHz = 3.2, TotalRamGB = 16, UsedRamGB = 10
                },
                new SimulationScenario
                {
                    Name = "Malware-like Resource Spike",
                    NameAr = "ارتفاع موارد مشبوه",
                    Description = "Unknown process hogging CPU+RAM+Network - testing security agent",
                    CpuUsage = 90, RamUsage = 85, GpuUsage = 50, DiskUsage = 45,
                    CpuTemp = 78, GpuTemp = 55, Fps = 0, FrameTimeMs = 0,
                    ProcessCount = 300, ThreadCount = 4000, SentBytes = 5000000, RecvBytes = 3000000,
                    ActiveProcess = "svchost_update.exe", IsGame = false, IsCharging = false,
                    BatteryLevel = 40, PowerPlan = "Balanced",
                    CpuCoreCount = 8, CpuFreqGHz = 3.0, TotalRamGB = 16, UsedRamGB = 14
                },
                new SimulationScenario
                {
                    Name = "Idle Desktop",
                    NameAr = "مكتب خامل",
                    Description = "System idle - testing whether AI stays quiet",
                    CpuUsage = 3, RamUsage = 25, GpuUsage = 1, DiskUsage = 2,
                    CpuTemp = 32, GpuTemp = 30, Fps = 0, FrameTimeMs = 0,
                    ProcessCount = 80, ThreadCount = 900, SentBytes = 500, RecvBytes = 1000,
                    ActiveProcess = "explorer.exe", IsGame = false, IsCharging = true,
                    BatteryLevel = 100, PowerPlan = "Balanced",
                    CpuCoreCount = 8, CpuFreqGHz = 2.0, TotalRamGB = 16, UsedRamGB = 4
                },
                new SimulationScenario
                {
                    Name = "Streaming Session",
                    NameAr = "جلسة بث",
                    Description = "OBS streaming + gaming - testing network + GPU balance",
                    CpuUsage = 72, RamUsage = 68, GpuUsage = 85, DiskUsage = 40,
                    CpuTemp = 74, GpuTemp = 80, Fps = 60, FrameTimeMs = 16.6,
                    ProcessCount = 180, ThreadCount = 2200, SentBytes = 8000000, RecvBytes = 200000,
                    ActiveProcess = "obs64.exe", IsGame = true, IsCharging = true,
                    BatteryLevel = 100, PowerPlan = "High Performance",
                    CpuCoreCount = 12, CpuFreqGHz = 4.0, TotalRamGB = 32, UsedRamGB = 22
                },
                new SimulationScenario
                {
                    Name = "Night Background",
                    NameAr = "خلفية ليلية",
                    Description = "System running overnight backups/updates - testing scheduling",
                    CpuUsage = 20, RamUsage = 40, GpuUsage = 0, DiskUsage = 70,
                    CpuTemp = 38, GpuTemp = 30, Fps = 0, FrameTimeMs = 0,
                    ProcessCount = 90, ThreadCount = 1000, SentBytes = 100000, RecvBytes = 500000,
                    ActiveProcess = "BackupSvc.exe", IsGame = false, IsCharging = true,
                    BatteryLevel = 100, PowerPlan = "Power saver",
                    CpuCoreCount = 8, CpuFreqGHz = 2.0, TotalRamGB = 16, UsedRamGB = 6
                }
            };
        }

        public static SimTestResult RunScenario(SmartAgent agent, SimulationScenario scenario, int tickCount = 20, int delayMs = 50)
        {
            var result = new SimTestResult
            {
                ScenarioName = scenario.Name,
                Timestamp = DateTime.Now,
                TickCount = tickCount
            };

            var sw = Stopwatch.StartNew();

            agent.EnableSimulation(scenario.ToSystemState());
            if (agent.Mode == AgentMode.Off)
                agent.SetMode(AgentMode.AutoPilot);

            var healthBefore = agent.GetOverallHealthScore();
            result.HealthScoreBefore = healthBefore;
            int decisionsBefore = agent.Stats.TotalDecisions;

            for (int i = 0; i < tickCount; i++)
            {
                result.TickCount = i + 1;
                agent.Tick();

                if (delayMs > 0)
                    System.Threading.Thread.Sleep(delayMs);
            }

            sw.Stop();
            result.DurationMs = sw.ElapsedMilliseconds;
            result.HealthScoreAfter = agent.GetOverallHealthScore();
            result.DecisionsMade = agent.Stats.TotalDecisions - decisionsBefore;

            var decisions = agent.DecisionHistory;
            int startIdx = Math.Max(0, decisions.Count - result.DecisionsMade);
            for (int i = startIdx; i < decisions.Count; i++)
            {
                var d = decisions[i];
                result.ActionsLog.Add(string.Format("[{0}] {1} (cat={2}, confidence={3:F0}%)",
                    d.Timestamp.ToString("HH:mm:ss"), d.Action, d.Category, d.Confidence * 100));
            }

            var suggestions = agent.GetSuggestions();
            foreach (var s in suggestions)
            {
                result.SuggestionsLog.Add(string.Format("[{0}] {1} (impact={2:F0}%)",
                    s.Priority, s.Title, s.EstimatedImpact));
            }

            result.Summary = BuildReport(result, scenario);
            agent.DisableSimulation();
            return result;
        }

        public static List<SimTestResult> RunAllScenarios(SmartAgent agent, int ticksPerScenario = 20, int delayMs = 50)
        {
            var results = new List<SimTestResult>();
            var scenarios = GetPresetScenarios();
            foreach (var s in scenarios)
            {
                results.Add(RunScenario(agent, s, ticksPerScenario, delayMs));
            }
            return results;
        }

        private static string BuildReport(SimTestResult result, SimulationScenario scenario)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════");
            sb.AppendLine("  SIMULATION REPORT - " + result.ScenarioName);
            sb.AppendLine("═══════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine("Scenario: " + scenario.Description);
            sb.AppendLine("Timestamp: " + result.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine();
            sb.AppendLine("── INPUT STATE ──");
            sb.AppendLine(string.Format("  CPU: {0:F0}%  |  RAM: {1:F0}%  |  GPU: {2:F0}%  |  Disk: {3:F0}%",
                scenario.CpuUsage, scenario.RamUsage, scenario.GpuUsage, scenario.DiskUsage));
            sb.AppendLine(string.Format("  CPU Temp: {0:F0}°C  |  GPU Temp: {1:F0}°C  |  Battery: {2:F0}%",
                scenario.CpuTemp, scenario.GpuTemp, scenario.BatteryLevel));
            sb.AppendLine(string.Format("  FPS: {0:F0}  |  Active: {1}  |  Game: {2}  |  Power: {3}",
                scenario.Fps, scenario.ActiveProcess, scenario.IsGame ? "Yes" : "No", scenario.PowerPlan));
            sb.AppendLine();
            sb.AppendLine("── PERFORMANCE ──");
            sb.AppendLine(string.Format("  Ticks: {0}  |  Duration: {1:F0}ms  |  Avg tick: {2:F1}ms",
                result.TickCount, result.DurationMs, result.DurationMs / Math.Max(1, result.TickCount)));
            sb.AppendLine(string.Format("  Health Before: {0:F1}/100  |  Health After: {1:F1}/100  |  Delta: {2:+F1;-F1}",
                result.HealthScoreBefore, result.HealthScoreAfter, result.HealthScoreAfter - result.HealthScoreBefore));
            sb.AppendLine(string.Format("  Decisions Made: {0}", result.DecisionsMade));
            sb.AppendLine();
            sb.AppendLine("── ACTIONS TAKEN ──");
            if (result.ActionsLog.Count == 0)
            {
                sb.AppendLine("  (No actions taken)");
            }
            else
            {
                foreach (var a in result.ActionsLog)
                    sb.AppendLine("  " + a);
            }
            sb.AppendLine();
            sb.AppendLine("── SUGGESTIONS ──");
            if (result.SuggestionsLog.Count == 0)
            {
                sb.AppendLine("  (No suggestions generated)");
            }
            else
            {
                foreach (var s in result.SuggestionsLog)
                    sb.AppendLine("  " + s);
            }
            sb.AppendLine();

            double healthDelta = result.HealthScoreAfter - result.HealthScoreBefore;
            string verdict;
            if (healthDelta > 5) verdict = "EXCELLENT - AI successfully optimized the system";
            else if (healthDelta > 0) verdict = "GOOD - AI made minor improvements";
            else if (healthDelta > -5) verdict = "NEUTRAL - AI maintained current state";
            else verdict = "INVESTIGATE - Health dropped, check decision logic";

            sb.AppendLine("── VERDICT ──");
            sb.AppendLine("  " + verdict);
            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════");
            return sb.ToString();
        }

        public static string BuildBatchReport(List<SimTestResult> results)
        {
            var sb = new StringBuilder();
            sb.AppendLine("╔═══════════════════════════════════════════╗");
            sb.AppendLine("║    AI SANDBOX - BATCH TEST REPORT         ║");
            sb.AppendLine("╚═══════════════════════════════════════════╝");
            sb.AppendLine();
            sb.AppendLine(string.Format("Total Scenarios: {0}", results.Count));
            sb.AppendLine(string.Format("Generated: {0}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")));
            sb.AppendLine();

            int passed = 0, neutral = 0, failed = 0;
            foreach (var r in results)
            {
                double delta = r.HealthScoreAfter - r.HealthScoreBefore;
                string status;
                if (delta > 0) { passed++; status = "✓ PASS"; }
                else if (delta > -5) { neutral++; status = "~ NEUTRAL"; }
                else { failed++; status = "✗ FAIL"; }

                sb.AppendLine(string.Format("  {0,-35} {1}  (Δ={2:+F1;-F1}, {3} decisions)",
                    r.ScenarioName, status, delta, r.DecisionsMade));
            }

            sb.AppendLine();
            sb.AppendLine("── SUMMARY ──");
            sb.AppendLine(string.Format("  Passed:  {0}/{1}", passed, results.Count));
            sb.AppendLine(string.Format("  Neutral: {0}/{1}", neutral, results.Count));
            sb.AppendLine(string.Format("  Failed:  {0}/{1}", failed, results.Count));
            sb.AppendLine(string.Format("  Pass Rate: {0:F0}%", passed * 100.0 / Math.Max(1, results.Count)));
            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════");
            return sb.ToString();
        }
    }
}
