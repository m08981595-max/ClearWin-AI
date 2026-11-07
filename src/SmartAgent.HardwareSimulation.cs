using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace ClearWin
{
    public enum HardwareTier
    {
        LowEnd,          // 2 cores, 4GB RAM, Integrated GPU, 5400 RPM HDD
        MidRange,        // 6 cores / 12 threads, 16GB RAM, RTX 3060 (6GB VRAM), SATA/NVMe SSD
        UltraEnthusiast  // 16 cores / 32 threads, 64GB RAM, RTX 4090 (24GB VRAM), PCIe 4.0 NVMe
    }

    public class SimulationTickResult
    {
        public int Tick;
        public string PhaseName;
        public double CpuUsage;
        public double RamUsage;
        public double GpuUsage;
        public double DiskUsage;
        public double CpuTemp;
        public double Fps;
        public double Fps01Low;
        public double FpsStability;
        public bool IsGame;
        public string Action;
        public double LatencyMs;
        public double HealthScore;
    }

    public class OneHourSimulationReport
    {
        public HardwareTier Tier;
        public int TotalTicks;
        public int TotalDecisions;
        public int UnhandledExceptions;
        public int PolicyViolations;
        public Dictionary<string, int> ActionDistribution = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public double AvgLatencyMs;
        public double P50LatencyMs;
        public double P95LatencyMs;
        public double P99LatencyMs;
        public double JitterMs;
        public double InitialHealthScore;
        public double FinalHealthScore;
        public double MinHealthScore;
        public List<string> PhaseLogs = new List<string>();
        public bool Passed;
        public string SummaryMessage;
    }

    /// <summary>
    /// Multi-Tier Hardware Simulation Engine:
    /// Simulates 1 hour (3,600 seconds/ticks) of realistic system lifecycle across:
    /// 1. Low-End Hardware (RAM & disk starvation, weak iGPU)
    /// 2. Mid-Range Hardware (Productivity -> 3D Gaming -> VRAM contention)
    /// 3. Ultra-Enthusiast Hardware (144+ FPS high-end GPU, core unparking, zero-stutter)
    /// </summary>
    public static class HardwareSimulationHarness
    {
        public static OneHourSimulationReport RunOneHourTrace(SmartAgent agent, HardwareTier tier)
        {
            if (agent == null) throw new ArgumentNullException("agent");

            var report = new OneHourSimulationReport
            {
                Tier = tier,
                TotalTicks = 3600,
                Passed = false
            };

            var tickResults = new List<SimulationTickResult>(3600);
            var latencies = new double[3600];
            var sw = new Stopwatch();

            // Set up device profile on the agent
            var profile = new DeviceProfile();
            switch (tier)
            {
                case HardwareTier.LowEnd:
                    profile.IsLaptop = false;
                    profile.TotalRamGB = 4.0;
                    profile.CpuCoreCount = 2;
                    profile.CpuLogicalProcessors = 2;
                    profile.PerformanceTier = 1;
                    profile.DiskType = "HDD";
                    profile.RamCleanThreshold = 65.0;
                    profile.ThermalWarningTemp = 75.0;
                    profile.ThermalThrottleTemp = 85.0;
                    break;

                case HardwareTier.MidRange:
                    profile.IsLaptop = false;
                    profile.TotalRamGB = 16.0;
                    profile.CpuCoreCount = 6;
                    profile.CpuLogicalProcessors = 12;
                    profile.PerformanceTier = 2;
                    profile.DiskType = "SSD";
                    profile.RamCleanThreshold = 75.0;
                    profile.ThermalWarningTemp = 80.0;
                    profile.ThermalThrottleTemp = 90.0;
                    break;

                case HardwareTier.UltraEnthusiast:
                    profile.IsLaptop = false;
                    profile.TotalRamGB = 64.0;
                    profile.CpuCoreCount = 16;
                    profile.CpuLogicalProcessors = 32;
                    profile.PerformanceTier = 4;
                    profile.DiskType = "NVMe";
                    profile.RamCleanThreshold = 85.0;
                    profile.ThermalWarningTemp = 85.0;
                    profile.ThermalThrottleTemp = 95.0;
                    break;
            }
            profile.ComputeAdaptedParameters();
            agent.SetDeviceProfile(profile);

            // Simulation state variables
            double baseTemp = tier == HardwareTier.LowEnd ? 48.0 : tier == HardwareTier.MidRange ? 42.0 : 38.0;
            double currentTemp = baseTemp;
            double currentHealth = 85.0;
            report.InitialHealthScore = currentHealth;
            report.MinHealthScore = currentHealth;
            string lastAction = "NONE";

            var rnd = new Random((int)tier * 1000 + 42);

            for (int t = 0; t < 3600; t++)
            {
                string phaseName;
                double targetCpu, targetRam, targetGpu, targetDisk, targetFps, targetFps01, targetFpsStab;
                bool isGame = false;
                string activeProc;

                // 5-Phase Dynamic Lifecycle (1 hour = 3600 ticks)
                if (t < 600)
                {
                    // Phase 1: Boot & Idle (0..10 min)
                    phaseName = "1. Boot & Idle";
                    activeProc = "explorer.exe";
                    targetCpu = 5.0 + rnd.NextDouble() * 8.0;
                    targetRam = tier == HardwareTier.LowEnd ? 42.0 : tier == HardwareTier.MidRange ? 32.0 : 18.0;
                    targetGpu = 2.0 + rnd.NextDouble() * 5.0;
                    targetDisk = 5.0 + rnd.NextDouble() * 10.0;
                    targetFps = 0;
                    targetFps01 = 0;
                    targetFpsStab = 0;
                }
                else if (t < 1500)
                {
                    // Phase 2: Heavy Office & Multitasking (10..25 min)
                    phaseName = "2. Office Multitasking";
                    activeProc = "chrome.exe";
                    if (tier == HardwareTier.LowEnd)
                    {
                        targetCpu = 55.0 + rnd.NextDouble() * 15.0;
                        targetRam = 68.0 + rnd.NextDouble() * 10.0; // RAM pressure
                        targetGpu = 15.0 + rnd.NextDouble() * 10.0;
                        targetDisk = 78.0 + rnd.NextDouble() * 15.0; // Slow HDD queue
                    }
                    else if (tier == HardwareTier.MidRange)
                    {
                        targetCpu = 62.0 + rnd.NextDouble() * 10.0; // Smoothness target range (60-75%)
                        targetRam = 54.0 + rnd.NextDouble() * 8.0;
                        targetGpu = 22.0 + rnd.NextDouble() * 10.0;
                        targetDisk = 18.0 + rnd.NextDouble() * 12.0;
                    }
                    else
                    {
                        targetCpu = 35.0 + rnd.NextDouble() * 10.0;
                        targetRam = 38.0 + rnd.NextDouble() * 6.0;
                        targetGpu = 18.0 + rnd.NextDouble() * 8.0;
                        targetDisk = 8.0 + rnd.NextDouble() * 8.0;
                    }
                    targetFps = 0;
                    targetFps01 = 0;
                    targetFpsStab = 0;
                }
                else if (t < 2700)
                {
                    // Phase 3: Active 3D Gaming Session (25..45 min)
                    phaseName = "3. Active 3D Gaming";
                    isGame = true;
                    activeProc = "cybergame.exe";
                    if (tier == HardwareTier.LowEnd)
                    {
                        targetCpu = 74.0 + rnd.NextDouble() * 12.0;
                        targetRam = 78.0 + rnd.NextDouble() * 10.0;
                        targetGpu = 92.0 + rnd.NextDouble() * 6.0;
                        targetDisk = 35.0 + rnd.NextDouble() * 15.0;
                        targetFps = 38.0 + rnd.NextDouble() * 10.0; // Sub-60 FPS struggle
                        targetFps01 = 15.0 + rnd.NextDouble() * 6.0;
                        targetFpsStab = 58.0 + rnd.NextDouble() * 10.0;
                    }
                    else if (tier == HardwareTier.MidRange)
                    {
                        targetCpu = 58.0 + rnd.NextDouble() * 12.0;
                        targetRam = 74.0 + rnd.NextDouble() * 5.0; // Background VRAM contention range
                        targetGpu = 86.0 + rnd.NextDouble() * 8.0;
                        targetDisk = 20.0 + rnd.NextDouble() * 10.0;
                        targetFps = 65.0 + rnd.NextDouble() * 10.0;
                        targetFps01 = 38.0 + rnd.NextDouble() * 8.0;
                        targetFpsStab = 78.0 + rnd.NextDouble() * 10.0;
                    }
                    else
                    {
                        targetCpu = 52.0 + rnd.NextDouble() * 10.0;
                        targetRam = 55.0 + rnd.NextDouble() * 6.0;
                        targetGpu = 88.0 + rnd.NextDouble() * 8.0; // High-end GPU saturation
                        targetDisk = 12.0 + rnd.NextDouble() * 8.0;
                        targetFps = 135.0 + rnd.NextDouble() * 20.0;
                        // Occasional micro-stutter spike
                        if (t >= 2000 && t < 2100)
                        {
                            targetFps01 = 16.0; // Stale shader cache drop
                            targetFpsStab = 52.0;
                        }
                        else
                        {
                            targetFps01 = 85.0 + rnd.NextDouble() * 15.0;
                            targetFpsStab = 92.0 + rnd.NextDouble() * 6.0;
                        }
                    }
                }
                else if (t < 3100)
                {
                    // Phase 4: Thermal & Ambient Heat Surge (45..51 min)
                    phaseName = "4. Thermal Surge";
                    isGame = true;
                    activeProc = "cybergame.exe";
                    targetCpu = 92.0 + rnd.NextDouble() * 6.0;
                    targetRam = 80.0 + rnd.NextDouble() * 8.0;
                    targetGpu = 95.0 + rnd.NextDouble() * 4.0;
                    targetDisk = 30.0 + rnd.NextDouble() * 10.0;
                    targetFps = 45.0 + rnd.NextDouble() * 10.0;
                    targetFps01 = 20.0 + rnd.NextDouble() * 8.0;
                    targetFpsStab = 60.0 + rnd.NextDouble() * 10.0;
                }
                else
                {
                    // Phase 5: Recovery, Game Exit & Desktop Smoothness (51..60 min)
                    phaseName = "5. Recovery & Desktop Return";
                    activeProc = "Code.exe";
                    targetCpu = 28.0 + rnd.NextDouble() * 15.0;
                    targetRam = tier == HardwareTier.LowEnd ? 48.0 : tier == HardwareTier.MidRange ? 40.0 : 26.0;
                    targetGpu = 8.0 + rnd.NextDouble() * 6.0;
                    targetDisk = 10.0 + rnd.NextDouble() * 8.0;
                    targetFps = 0;
                    targetFps01 = 0;
                    targetFpsStab = 0;
                }

                // Thermal inertia differential equation: dT/dt = (Load - Cooling) / C
                double thermalLoadFactor = (targetCpu * 0.5 + targetGpu * 0.5) / 100.0;
                if (lastAction == "EmergencyThrottle") thermalLoadFactor *= 0.60;
                else if (lastAction == "ReduceThermal") thermalLoadFactor *= 0.75;

                double coolingCapacity = tier == HardwareTier.UltraEnthusiast ? 0.85 : tier == HardwareTier.MidRange ? 0.65 : 0.45;
                if (phaseName.Contains("Thermal Surge")) thermalLoadFactor += 0.40; // Ambient heat spike
                double targetEquilibriumTemp = baseTemp + (thermalLoadFactor / coolingCapacity) * 35.0;
                currentTemp += (targetEquilibriumTemp - currentTemp) * 0.05; // Smooth exponential convergence

                var syntheticState = new SystemState
                {
                    CpuUsage = targetCpu,
                    RamUsage = targetRam,
                    GpuUsage = targetGpu,
                    DiskUsage = targetDisk,
                    CpuTemp = currentTemp,
                    GpuTemp = currentTemp * 0.95,
                    Fps = targetFps,
                    Fps01Low = targetFps01,
                    FpsStability = targetFpsStab,
                    ProcessCount = tier == HardwareTier.LowEnd ? 90 : tier == HardwareTier.MidRange ? 135 : 185,
                    ActiveProcess = activeProc,
                    IsGame = isGame,
                    IsCharging = true,
                    BatteryLevel = 100,
                    TotalRamGB = profile.TotalRamGB,
                    UsedRamGB = (targetRam / 100.0) * profile.TotalRamGB,
                    Timestamp = DateTime.Now
                };

                // Time inference strictly
                sw.Restart();
                AgentDecision decision = null;
                try
                {
                    decision = agent.GetOptimizationDecisionForState(syntheticState);
                }
                catch (Exception)
                {
                    report.UnhandledExceptions++;
                }
                sw.Stop();

                double latMs = sw.Elapsed.TotalMilliseconds;
                latencies[t] = latMs;

                string actionTaken = decision != null ? decision.Action : "NONE";
                lastAction = actionTaken;
                if (!string.IsNullOrEmpty(actionTaken))
                {
                    int cnt;
                    report.ActionDistribution.TryGetValue(actionTaken, out cnt);
                    report.ActionDistribution[actionTaken] = cnt + 1;
                    if (actionTaken != "NONE" && actionTaken != "Abstain")
                    {
                        report.TotalDecisions++;
                    }

                    // Check for forbidden actions under the policy
                    if (ActionCatalog.IsRegistered(actionTaken))
                    {
                        var tierLevel = ActionCatalog.GetTier(actionTaken);
                        // Red tier actions are strictly forbidden from autonomous issuance without approval
                        if (tierLevel == ActionTier.Red && actionTaken != "hardware.emergency_cool")
                        {
                            report.PolicyViolations++;
                        }
                    }
                }

                // Health dynamics
                if (currentTemp > 88.0) currentHealth = Math.Max(40.0, currentHealth - 0.15);
                else if (actionTaken == "EmergencyThrottle" || actionTaken == "ReduceThermal") currentHealth = Math.Min(98.0, currentHealth + 0.6);
                else if (actionTaken == "OptimizeGpuPerformance" || actionTaken == "ShedBackgroundGpuMemory") currentHealth = Math.Min(98.0, currentHealth + 0.4);
                else if (actionTaken == "OptimizeDesktopSmoothness" || actionTaken == "TrimInactiveWorkingSets") currentHealth = Math.Min(98.0, currentHealth + 0.3);
                else currentHealth = Math.Min(95.0, currentHealth + 0.15);

                if (currentHealth < report.MinHealthScore) report.MinHealthScore = currentHealth;

                if (t % 600 == 0)
                {
                    report.PhaseLogs.Add(string.Format(CultureInfo.InvariantCulture,
                        "[{0}] Tick #{1}: CPU={2:F0}% RAM={3:F0}% GPU={4:F0}% Temp={5:F1}C Action='{6}' Health={7:F1}",
                        phaseName, t, targetCpu, targetRam, targetGpu, currentTemp, actionTaken, currentHealth));
                }
            }

            report.FinalHealthScore = currentHealth;

            // Quantile Latency Calculation
            double[] sorted = (double[])latencies.Clone();
            Array.Sort(sorted);
            report.AvgLatencyMs = latencies.Average();
            report.P50LatencyMs = sorted[(int)(3600 * 0.50)];
            report.P95LatencyMs = sorted[(int)(3600 * 0.95)];
            report.P99LatencyMs = sorted[(int)(3600 * 0.99)];

            double avg = report.AvgLatencyMs;
            double sumSq = 0.0;
            for (int i = 0; i < 3600; i++)
            {
                double d = latencies[i] - avg;
                sumSq += d * d;
            }
            report.JitterMs = Math.Sqrt(sumSq / 3600.0);

            // Pass Criteria: Zero exceptions, zero policy violations, p99 latency < 2.5ms, healthy recovery
            report.Passed = (report.UnhandledExceptions == 0) &&
                            (report.PolicyViolations == 0) &&
                            (report.P99LatencyMs < 2.5) &&
                            (report.FinalHealthScore >= 75.0 || report.FinalHealthScore >= report.InitialHealthScore - 8.0);

            report.SummaryMessage = string.Format(CultureInfo.InvariantCulture,
                "Tier={0} | Ticks=3600 | Decisions={1} | Exceptions={2} | Violations={3} | p50={4:F3}ms | p99={5:F3}ms | Health: {6:F1}->{7:F1}",
                tier, report.TotalDecisions, report.UnhandledExceptions, report.PolicyViolations,
                report.P50LatencyMs, report.P99LatencyMs, report.InitialHealthScore, report.FinalHealthScore);

            return report;
        }
    }
}
