using System;
using System.Collections.Generic;

namespace ClearWin
{
    public static class AIDecisionDemo
    {
        static int _passed = 0;
        static int _failed = 0;

        public static void Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("========================================================");
            Console.WriteLine("  ClearWin SmartAgent - 10 AI Decision Scenarios");
            Console.WriteLine("========================================================");
            Console.WriteLine();

            RunScenario("1. IDLE - Light browsing, plugged in",
                15, 30, 5, 42, 60, false, "chrome", 100, true);

            RunScenario("2. HIGH CPU - Chrome eating 92%",
                92, 55, 10, 68, 0, false, "chrome", 100, true);

            RunScenario("3. HIGH RAM - 91% used",
                40, 91, 5, 55, 0, false, "explorer", 100, true);

            RunScenario("4. GAMING - Valorant 120 FPS, plugged",
                70, 65, 85, 65, 120, true, "valorant", 100, true);

            RunScenario("5. GAMING OVERHEAT - Cyberpunk 92C",
                88, 78, 95, 92, 35, true, "cyberpunk2077", 100, true);

            RunScenario("6. BATTERY CRITICAL - 15% remaining",
                35, 45, 5, 50, 0, false, "chrome", 15, false);

            RunScenario("7. BATTERY MODERATE - 40% remaining",
                50, 55, 10, 58, 0, false, "vscode", 40, false);

            RunScenario("8. DEFENDER SCAN - msmpeng eating CPU",
                85, 40, 5, 72, 0, false, "msmpeng", 100, true);

            RunScenario("9. HEAVY MULTITASK - Fortnite+Discord+Chrome",
                75, 82, 70, 78, 45, true, "fortnite", 80, true);

            RunScenario("10. LOW BATTERY + GAMING - Fortnite on 18%",
                70, 60, 80, 75, 55, true, "fortnite", 18, false);

            Console.WriteLine();
            Console.WriteLine("========================================================");
            Console.WriteLine(string.Format("  Results: {0} passed, {1} failed", _passed, _failed));
            Console.WriteLine("========================================================");
        }

        static void RunScenario(string name,
            double cpu, double ram, double gpu, double temp, double fps,
            bool isGame, string process, int battery, bool charging)
        {
            Console.WriteLine("========================================================");
            Console.WriteLine("SCENARIO: " + name);
            Console.WriteLine("--------------------------------------------------------");
            Console.WriteLine("  INPUT VALUES:");
            Console.WriteLine(string.Format("    CPU={0}%  RAM={1}%  GPU={2}%  Temp={3}C  FPS={4}",
                cpu, ram, gpu, temp, fps));
            Console.WriteLine(string.Format("    Game={0}  Process={1}  Battery={2}%  Charging={3}",
                isGame, process, battery, charging));
            Console.WriteLine();

            var agent = new SmartAgent();
            agent.SetMode(AgentMode.AutoPilot);

            var state = new SystemState();
            state.CpuUsage = cpu;
            state.RamUsage = ram;
            state.GpuUsage = gpu;
            state.CpuTemp = temp;
            state.Fps = fps;
            state.FpsStability = fps > 0 ? Math.Min(100, fps * 1.1) : 0;
            state.IsGame = isGame;
            state.ActiveProcess = process;
            state.ProcessCount = 120;
            state.DiskUsage = 55;
            state.BatteryLevel = battery;
            state.IsCharging = charging;
            state.TotalRamGB = 16;
            state.UsedRamGB = ram * 16.0 / 100.0;
            state.Timestamp = DateTime.Now;

            agent.EnableSimulation(state);
            for (int i = 0; i < 10; i++) agent.Tick();

            Console.WriteLine("  AI RESPONSE:");
            Console.WriteLine(string.Format("    Health Score: {0:F1}/100", agent.SystemHealthScore));
            Console.WriteLine(string.Format("    Thermal Throttled: {0}", agent.IsThermalThrottled));

            var votes = agent.GetLastVotes();
            Console.WriteLine(string.Format("    Agents Voted: {0}", votes.Count));
            foreach (var v in votes)
            {
                if (v.Priority > 0)
                    Console.WriteLine(string.Format("      [{0,-8}] Priority={1}  -> {2}", v.Role, v.Priority, v.Reason));
            }

            var consensus = agent.GetLastConsensus();
            if (consensus != null && consensus.RecommendedAction != "NONE")
            {
                Console.WriteLine(string.Format("    CONSENSUS: {0}", consensus.RecommendedAction));
            }
            else
            {
                Console.WriteLine("    CONSENSUS: NONE (no action needed)");
            }

            var decisions = agent.DecisionHistoryLog;
            if (decisions.Count > 0)
            {
                Console.WriteLine(string.Format("    Decisions Made: {0}", decisions.Count));
                for (int i = decisions.Count - 1; i >= 0 && i >= decisions.Count - 3; i--)
                    Console.WriteLine("      " + decisions[i]);
            }

            agent.DisableSimulation();
            Console.WriteLine();
        }

        static void Assert(bool condition, string msg)
        {
            if (!condition) throw new Exception("Assertion failed: " + msg);
        }
    }
}
