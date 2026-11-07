using System;
using System.Collections.Generic;

namespace ClearWin.RuleEngine
{
    /// <summary>
    /// Diagnostic Engine: deep analysis of Windows performance problems.
    /// Gathers evidence from the live state, computes a health picture,
    /// ranks root-cause hypotheses via the reasoning engine, and produces a
    /// prioritized diagnosis with the most probable root cause.
    /// </summary>
    public class DiagnosticEngine
    {
        private readonly RuleKnowledgeBase _kb;
        private readonly ReasoningEngine _reasoning;

        public DiagnosticEngine(RuleKnowledgeBase kb, ReasoningEngine reasoning)
        {
            _kb = kb;
            _reasoning = reasoning;
        }

        /// <summary>Runs a full diagnosis cycle over a system state snapshot.</summary>
        public ExpertDiagnosis Diagnose(SystemState state, DeviceSnapshot device)
        {
            var diagnosis = new ExpertDiagnosis();
            if (state == null)
            {
                diagnosis.Symptom = "No system data available";
                diagnosis.Evidence.Add("System state was null; cannot diagnose.");
                return diagnosis;
            }

            // 1) Ground all facts for the reasoning engine.
            GroundFacts(state, device);

            // 2) Collect evidence by probing each subsystem.
            CollectEvidence(state, device, diagnosis);

            // 3) Determine the primary symptom (worst subsystem).
            DeterminePrimarySymptom(state, device, diagnosis);

            // 4) Let the reasoning engine rank root-cause hypotheses.
            RankHypotheses(state, diagnosis);

            // 5) Select the root cause.
            SelectRootCause(diagnosis);

            return diagnosis;
        }

        private void GroundFacts(SystemState state, DeviceSnapshot device)
        {
            _kb.SetFact(RuleEngineConstants.Vars.CpuUsage, state.CpuUsage, 1.0, "state");
            _kb.SetFact(RuleEngineConstants.Vars.RamUsage, state.RamUsage, 1.0, "state");
            _kb.SetFact(RuleEngineConstants.Vars.GpuUsage, state.GpuUsage, 1.0, "state");
            _kb.SetFact(RuleEngineConstants.Vars.DiskUsage, state.DiskUsage, 1.0, "state");
            _kb.SetFact(RuleEngineConstants.Vars.CpuTemp, state.CpuTemp, 1.0, "state");
            _kb.SetFact(RuleEngineConstants.Vars.GpuTemp, state.GpuTemp, 1.0, "state");
            _kb.SetFact(RuleEngineConstants.Vars.Fps, state.Fps, 1.0, "state");
            _kb.SetFact(RuleEngineConstants.Vars.FpsStability, state.FpsStability, 1.0, "state");
            _kb.SetFact(RuleEngineConstants.Vars.FrameTimeMs, state.FrameTimeMs, 1.0, "state");
            _kb.SetFact(RuleEngineConstants.Vars.ProcessCount, state.ProcessCount, 1.0, "state");
            _kb.SetFact(RuleEngineConstants.Vars.FreeRamGb, Math.Max(0, state.TotalRamGB - state.UsedRamGB), 1.0, "state");
            _kb.SetFact(RuleEngineConstants.Vars.BatteryLevel, state.BatteryLevel, 1.0, "state");
            _kb.SetFact(RuleEngineConstants.Vars.IsCharging, state.IsCharging ? 1 : 0, 1.0, "state");
            _kb.SetFact(RuleEngineConstants.Vars.IsGame, state.IsGame ? 1 : 0, 1.0, "state");
            _kb.SetFact(RuleEngineConstants.Vars.NetworkThroughput, state.RecvBytes / (1024.0 * 1024.0), 1.0, "state");
            _kb.SetFact(RuleEngineConstants.Vars.PowerPlanHighPerf, IsHighPerfPlan(state.PowerPlan) ? 1 : 0, 1.0, "state");
            _kb.SetFact(RuleEngineConstants.Vars.PowerPlanPowerSaver, IsPowerSaverPlan(state.PowerPlan) ? 1 : 0, 1.0, "state");
            if (device != null)
            {
                _kb.SetFact(RuleEngineConstants.Vars.FreeDiskGb, device.DiskFreeGB, 1.0, "device");
                _kb.SetFact(RuleEngineConstants.Vars.StartupCount, device.StartupItems, 1.0, "device");
                if (device.ServicesScanned)
                {
                    _kb.SetFact(RuleEngineConstants.Vars.ServicesStopped, device.StoppedServices, 1.0, "device");
                    _kb.SetFact(RuleEngineConstants.Vars.ServicesDisabled, device.DisabledServices, 1.0, "device");
                }
            }
            _kb.SetFact(RuleEngineConstants.Vars.SysHealth, ComputeHealth(state), 1.0, "state");
        }

        internal static bool IsPowerSaverPlan(string powerPlan)
        {
            if (string.IsNullOrEmpty(powerPlan)) return false;
            string p = powerPlan.ToLowerInvariant();
            return p.IndexOf("power saver", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   p.IndexOf("توفير", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static bool IsHighPerfPlan(string powerPlan)
        {
            if (string.IsNullOrEmpty(powerPlan)) return false;
            string p = powerPlan.ToLowerInvariant();
            return p.IndexOf("high performance", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   p.IndexOf("ultimate", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   p.IndexOf("عالي الأداء", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static double ComputeHealth(SystemState s)
        {
            double cpu = Math.Max(0, 100 - s.CpuUsage);
            double ram = Math.Max(0, 100 - s.RamUsage);
            double gpu = Math.Max(0, 100 - s.GpuUsage);
            double temp = Math.Max(0, 100 - (s.CpuTemp - 40) * 1.5);
            double fps = s.FpsStability > 0 ? s.FpsStability : 60;
            double avg = cpu * 0.2 + ram * 0.2 + temp * 0.25 + gpu * 0.15 + fps * 0.2;
            double worst = Math.Min(cpu, Math.Min(ram, Math.Min(temp, Math.Min(gpu, fps))));
            return Math.Max(0, Math.Min(100, avg * 0.7 + worst * 0.3));
        }

        private void CollectEvidence(SystemState state, DeviceSnapshot device, ExpertDiagnosis diagnosis)
        {
            // --- Thermal ---
            if (state.CpuTemp >= RuleEngineConstants.CriticalThermal)
            {
                diagnosis.Evidence.Add(string.Format("CPU temperature {0:F0}C is critical (> {1:F0}C).", state.CpuTemp, RuleEngineConstants.CriticalThermal));
                _kb.SetFact(RuleEngineConstants.Vars.ThermalStress, 2, 1.0, "diagnostic");
            }
            else if (state.CpuTemp >= RuleEngineConstants.HighThermal)
            {
                diagnosis.Evidence.Add(string.Format("CPU temperature {0:F0}C is above the warning threshold.", state.CpuTemp));
                _kb.SetFact(RuleEngineConstants.Vars.ThermalStress, 1, 1.0, "diagnostic");
            }

            // --- Memory ---
            double freeRam = state.TotalRamGB - state.UsedRamGB;
            if (state.RamUsage >= RuleEngineConstants.CriticalRam)
            {
                diagnosis.Evidence.Add(string.Format("RAM usage {0:F0}% is critical; free memory only {1:F1}GB.", state.RamUsage, freeRam));
                _kb.SetFact(RuleEngineConstants.Vars.MemoryPressure, 2, 1.0, "diagnostic");
            }
            else if (state.RamUsage >= RuleEngineConstants.HighRam)
            {
                diagnosis.Evidence.Add(string.Format("RAM usage {0:F0}% is high.", state.RamUsage));
                _kb.SetFact(RuleEngineConstants.Vars.MemoryPressure, 1, 1.0, "diagnostic");
            }

            // --- CPU ---
            if (state.CpuUsage >= RuleEngineConstants.CriticalCpu)
            {
                diagnosis.Evidence.Add(string.Format("CPU is saturated at {0:F0}% across {1} threads.", state.CpuUsage, state.ThreadCount));
                _kb.SetFact(RuleEngineConstants.Vars.CpuSaturation, 2, 1.0, "diagnostic");
            }
            else if (state.CpuUsage >= RuleEngineConstants.HighCpu)
            {
                diagnosis.Evidence.Add(string.Format("CPU load {0:F0}% is high.", state.CpuUsage));
                _kb.SetFact(RuleEngineConstants.Vars.CpuSaturation, 1, 1.0, "diagnostic");
            }

            // --- Disk ---
            if (device != null && device.DiskFreeGB > 0 && device.DiskFreeGB < RuleEngineConstants.LowDiskGb)
            {
                diagnosis.Evidence.Add(string.Format("Only {0:F1}GB free on the system drive.", device.DiskFreeGB));
                _kb.SetFact(RuleEngineConstants.Vars.DiskLow, 1, 1.0, "diagnostic");
            }
            if (state.DiskUsage >= RuleEngineConstants.HighDisk)
                diagnosis.Evidence.Add(string.Format("Disk activity is high at {0:F0}%.", state.DiskUsage));

            // --- Processes ---
            if (state.ProcessCount > RuleEngineConstants.HighProcessCount)
            {
                diagnosis.Evidence.Add(string.Format("{0} processes running; the system is bloated.", state.ProcessCount));
                _kb.SetFact(RuleEngineConstants.Vars.ProcessBloat, 1, 1.0, "diagnostic");
            }

            // --- Network ---
            double networkMbps = state.RecvBytes / (1024.0 * 1024.0);
            if (networkMbps > RuleEngineConstants.HighNetworkMbps)
            {
                diagnosis.Evidence.Add(string.Format("Network receiving {0:F1} MB/s - likely saturating the link.", networkMbps));
                _kb.SetFact(RuleEngineConstants.Vars.NetworkSaturated, 1, 1.0, "diagnostic");
            }

            // --- Startup ---
            if (device != null && device.StartupItems > RuleEngineConstants.HighStartupCount)
            {
                diagnosis.Evidence.Add(string.Format("{0} startup programs registered; boot and background load increase.", device.StartupItems));
                _kb.SetFact(RuleEngineConstants.Vars.StartupBloat, 1, 1.0, "diagnostic");
            }

            // --- Services ---
            if (device != null && device.ServicesScanned &&
                device.AutoStoppedServices >= RuleEngineConstants.HighAutoStoppedServices)
            {
                string names = "";
                for (int i = 0; i < device.ProblemServiceNames.Count && i < 3; i++)
                    names += (names.Length == 0 ? "" : ", ") + device.ProblemServiceNames[i];
                diagnosis.Evidence.Add(string.Format(
                    "{0} auto-start service(s) are stopped ({1}); system services are degrading.",
                    device.AutoStoppedServices, names.Length == 0 ? "none listed" : names));
                _kb.SetFact(RuleEngineConstants.Vars.ServiceHealth, 1, 1.0, "diagnostic");
            }

            // --- Power plan ---
            if (IsPowerSaverPlan(state.PowerPlan) &&
                (state.IsGame || state.CpuUsage >= RuleEngineConstants.HighCpu || (state.Fps > 0 && state.Fps < RuleEngineConstants.LowFps)))
            {
                diagnosis.Evidence.Add("Power saver plan is active while performance is needed; clocks are limited.");
                _kb.SetFact(RuleEngineConstants.Vars.PowerPlanSuboptimal, 1, 1.0, "diagnostic");
            }
            if (IsHighPerfPlan(state.PowerPlan) && state.CpuTemp >= RuleEngineConstants.HighThermal)
            {
                diagnosis.Evidence.Add("High performance plan with elevated temperature - heat risk rising.");
            }

            // --- Disk cleanup ---
            if (device != null && device.DiskFreeGB > 0 && device.DiskFreeGB < RuleEngineConstants.DiskCleanupGb)
            {
                diagnosis.Evidence.Add(string.Format("Only {0:F1}GB free on the system drive - cleanup advisable.", device.DiskFreeGB));
                _kb.SetFact(RuleEngineConstants.Vars.DiskCleanupNeeded, 1, 1.0, "diagnostic");
            }

            // --- Gaming ---
            if (state.IsGame && state.Fps > 0)
            {
                if (state.Fps < RuleEngineConstants.CriticalFps)
                    diagnosis.Evidence.Add(string.Format("FPS is {0:F0} (< {1:F0}) while gaming.", state.Fps, RuleEngineConstants.CriticalFps));
                else if (state.Fps < RuleEngineConstants.LowFps)
                    diagnosis.Evidence.Add(string.Format("FPS is {0:F0} (< {1:F0}) while gaming.", state.Fps, RuleEngineConstants.LowFps));
                if (state.FpsStability > 0 && state.FpsStability < 50)
                    diagnosis.Evidence.Add(string.Format("Frame delivery is unstable (stability {0:F0}%, jitter {1:F1}ms).",
                        state.FpsStability, state.FrameTimeJitter));
                _kb.SetFact(RuleEngineConstants.Vars.GamingInstability, 1, 1.0, "diagnostic");
            }

            // --- Battery ---
            if (!state.IsCharging && state.BatteryLevel <= RuleEngineConstants.BatteryLow)
            {
                diagnosis.Evidence.Add(string.Format("Battery is at {0:F0}% and not charging.", state.BatteryLevel));
                _kb.SetFact(RuleEngineConstants.Vars.BatteryLow, 1, 1.0, "diagnostic");
            }
        }

        private void DeterminePrimarySymptom(SystemState state, DeviceSnapshot device, ExpertDiagnosis diagnosis)
        {
            double cpuScore = state.CpuUsage;
            double ramScore = state.RamUsage;
            double tempScore = state.CpuTemp / 1.2;
            double gpuScore = state.GpuUsage;
            double fpsScore = (state.Fps > 0 && state.IsGame) ? (100 - state.Fps) : 0;
            double diskScore = state.DiskUsage;
            double batteryScore = state.IsCharging ? 0 : (100 - state.BatteryLevel);
            double networkScore = (state.RecvBytes / (1024.0 * 1024.0)) > RuleEngineConstants.HighNetworkMbps ? 60 : 0;
            double startupScore = (device != null && device.StartupItems > RuleEngineConstants.HighStartupCount) ? 50 : 0;
            double servicesScore = (device != null && device.ServicesScanned &&
                device.AutoStoppedServices >= RuleEngineConstants.HighAutoStoppedServices) ? 55 : 0;
            double powerScore = (IsPowerSaverPlan(state.PowerPlan) && (state.IsGame || state.CpuUsage >= RuleEngineConstants.HighCpu)) ? 55 : 0;

            double worst = cpuScore;
            string symptom = "High CPU usage";
            string category = "CPU";
            if (tempScore > worst) { worst = tempScore; symptom = "Thermal stress"; category = "Thermal"; }
            if (ramScore > worst) { worst = ramScore; symptom = "Memory pressure"; category = "Memory"; }
            if (gpuScore > worst) { worst = gpuScore; symptom = "High GPU load"; category = "GPU"; }
            if (fpsScore > worst) { worst = fpsScore; symptom = "Unstable / low frame rate"; category = "Gaming"; }
            if (diskScore > worst) { worst = diskScore; symptom = "Disk bottleneck"; category = "Disk"; }
            if (batteryScore > worst) { worst = batteryScore; symptom = "Battery drain"; category = "Power"; }
            if (networkScore > worst) { worst = networkScore; symptom = "Network saturation"; category = "Network"; }
            if (startupScore > worst) { worst = startupScore; symptom = "Startup bloat"; category = "Startup"; }
            if (servicesScore > worst) { worst = servicesScore; symptom = "Service health problems"; category = "Services"; }
            if (powerScore > worst) { worst = powerScore; symptom = "Suboptimal power plan"; category = "Power"; }

            diagnosis.Symptom = symptom;
            diagnosis.Category = category;
            diagnosis.Severity = WorstSeverity(state, device);
        }

        private SeverityLevel WorstSeverity(SystemState state, DeviceSnapshot device)
        {
            if (state.CpuTemp >= RuleEngineConstants.CriticalThermal || state.RamUsage >= RuleEngineConstants.CriticalRam ||
                state.CpuUsage >= RuleEngineConstants.CriticalCpu)
                return SeverityLevel.Critical;
            if (state.CpuTemp >= RuleEngineConstants.HighThermal || state.RamUsage >= RuleEngineConstants.HighRam ||
                state.CpuUsage >= RuleEngineConstants.HighCpu)
                return SeverityLevel.High;
            if (device != null && device.DiskFreeGB > 0 && device.DiskFreeGB < RuleEngineConstants.LowDiskGb)
                return SeverityLevel.High;
            if (device != null && device.ServicesScanned &&
                device.AutoStoppedServices >= RuleEngineConstants.HighAutoStoppedServices)
                return SeverityLevel.Medium;
            if (state.Fps > 0 && state.Fps < RuleEngineConstants.LowFps)
                return SeverityLevel.Medium;
            return SeverityLevel.Info;
        }

        private void RankHypotheses(SystemState state, ExpertDiagnosis diagnosis)
        {
            double symptomSeverity = 0.5;
            switch (diagnosis.Severity)
            {
                case SeverityLevel.Critical: symptomSeverity = 1.0; break;
                case SeverityLevel.High: symptomSeverity = 0.8; break;
                case SeverityLevel.Medium: symptomSeverity = 0.6; break;
            }

            // Pick the effect variable that matches the primary symptom.
            string effect = EffectForCategory(diagnosis.Category);
            if (effect == null) return;

            var hypotheses = _reasoning.HypothesizeDeep(effect, symptomSeverity, 2);
            for (int i = 0; i < hypotheses.Count && i < 4; i++)
                diagnosis.Hypotheses.Add(hypotheses[i]);
        }

        private static string EffectForCategory(string category)
        {
            if (category == null) return null;
            switch (category)
            {
                case "Thermal": return RuleEngineConstants.Vars.CpuTemp;
                case "Memory": return RuleEngineConstants.Vars.Fps;
                case "CPU": return RuleEngineConstants.Vars.CpuUsage;
                case "GPU": return RuleEngineConstants.Vars.GpuUsage;
                case "Gaming": return RuleEngineConstants.Vars.Fps;
                case "Disk": return RuleEngineConstants.Vars.Fps;
                case "Power": return RuleEngineConstants.Vars.Fps;
                case "Network": return RuleEngineConstants.Vars.NetworkThroughput;
                case "Startup": return RuleEngineConstants.Vars.CpuUsage;
                case "Services": return RuleEngineConstants.Vars.CpuUsage;
                default: return null;
            }
        }

        private void SelectRootCause(ExpertDiagnosis diagnosis)
        {
            if (diagnosis.Hypotheses.Count == 0)
            {
                diagnosis.RootCause = diagnosis.Symptom;
                diagnosis.Confidence = 0.3;
                return;
            }

            ExpertHypothesis best = diagnosis.Hypotheses[0];
            diagnosis.RootCause = best.Name;
            diagnosis.Confidence = best.Probability;

            // Use the hypothesis's recommended action as a hint for the decision engine.
            if (!string.IsNullOrEmpty(best.RecommendedAction))
            {
                diagnosis.RecommendedHint = best.RecommendedAction;
                // When the system drive is nearly full, free-space cleanup is the
                // higher-value hint over generic disk I/O priority.
                if (string.Equals(best.RecommendedAction, "SetLowIoPriority", StringComparison.OrdinalIgnoreCase) &&
                    _kb.GetFact(RuleEngineConstants.Vars.DiskCleanupNeeded) != null)
                    diagnosis.RecommendedHint = "DiskCleanup";
            }
        }
    }
}
