using System;
using System.Collections.Generic;

namespace ClearWin.RuleEngine
{
    /// <summary>Central, tunable constants for the expert system.</summary>
    public static class RuleEngineConstants
    {
        // ================= Knowledge Base limits =================
        public const int MaxFacts = 200;
        public const int MaxRules = 400;
        public const int MaxCausalLinks = 200;
        public const int MaxStateHistory = 60;
        public const int MaxSessionEvents = 200;
        public const double FactMinConfidence = 0.2;
        public const double RuleMinConfidence = 0.3;

        // ================= Health thresholds =================
        public const double HighCpu = 85;
        public const double CriticalCpu = 95;
        public const double HighRam = 85;
        public const double CriticalRam = 95;
        public const double HighGpu = 90;
        public const double HighDisk = 90;
        public const double HighThermal = 82;
        public const double CriticalThermal = 92;
        public const double LowFps = 30;
        public const double CriticalFps = 20;
        public const double LowDiskGb = 5;
        public const double HighProcessCount = 200;
        public const double BatteryLow = 20;
        public const double BatteryCritical = 10;
        public const double HighNetworkMbps = 40;
        public const double HighStartupCount = 10;
        public const double DiskCleanupGb = 15;
        public const double PowerPlanFpsImpact = 8;

        // ================= Service health thresholds =================
        public const double HighStoppedServices = 8;
        public const double HighAutoStoppedServices = 2;
        public const double HighDisabledServices = 10;

        // ================= Decision scoring weights =================
        public const double WeightImpact = 0.30;
        public const double WeightUrgency = 0.20;
        public const double WeightConfidence = 0.15;
        public const double WeightRisk = 0.20;
        public const double WeightCost = 0.15;

        // ================= Safety engine =================
        public const double RiskRequiresApproval = 0.7;
        public const double RiskCritical = 0.9;

        // ================= Learning =================
        public const double LearningDecayHours = 48.0;
        public const double LearningMinSamples = 3;
        public const int MaxActionStats = 200;

        // ================= Common fact variables =================
        public static class Vars
        {
            public const string CpuUsage = "cpu.usage";
            public const string RamUsage = "ram.usage";
            public const string GpuUsage = "gpu.usage";
            public const string DiskUsage = "disk.usage";
            public const string CpuTemp = "temp.cpu";
            public const string GpuTemp = "temp.gpu";
            public const string Fps = "fps.current";
            public const string FpsStability = "fps.stability";
            public const string FrameTimeMs = "fps.frametime_ms";
            public const string ProcessCount = "processes.count";
            public const string FreeRamGb = "ram.free_gb";
            public const string FreeDiskGb = "disk.free_gb";
            public const string BatteryLevel = "battery.level";
            public const string IsCharging = "battery.charging";
            public const string IsGame = "workload.game";
            public const string MemoryPressure = "diagnosis.memory_pressure";
            public const string ThermalStress = "diagnosis.thermal_stress";
            public const string CpuSaturation = "diagnosis.cpu_saturation";
            public const string DiskLow = "diagnosis.disk_low";
            public const string ProcessBloat = "diagnosis.process_bloat";
            public const string GamingInstability = "diagnosis.gaming_instability";
            public const string BatteryLow = "diagnosis.battery_low";
            public const string SysHealth = "sys.health";
            public const string NetworkThroughput = "network.throughput_mbps";
            public const string NetworkSaturated = "diagnosis.network_saturated";
            public const string StartupCount = "startup.count";
            public const string StartupBloat = "diagnosis.startup_bloat";
            public const string PowerPlanHighPerf = "power.plan_high_perf";
            public const string PowerPlanPowerSaver = "power.plan_power_saver";
            public const string PowerPlanSuboptimal = "diagnosis.power_plan_suboptimal";
            public const string DiskCleanupNeeded = "diagnosis.disk_cleanup_needed";
            public const string ServicesStopped = "services.stopped";
            public const string ServicesDisabled = "services.disabled";
            public const string ServiceHealth = "diagnosis.services_health";
        }

        /// <summary>Builds a standard fact for a variable from a SystemState snapshot.</summary>
        public static double Extract(SystemState s, string variable, double fallback)
        {
            if (s == null) return fallback;
            switch (variable)
            {
                case Vars.CpuUsage: return s.CpuUsage;
                case Vars.RamUsage: return s.RamUsage;
                case Vars.GpuUsage: return s.GpuUsage;
                case Vars.DiskUsage: return s.DiskUsage;
                case Vars.CpuTemp: return s.CpuTemp;
                case Vars.GpuTemp: return s.GpuTemp;
                case Vars.Fps: return s.Fps;
                case Vars.FpsStability: return s.FpsStability;
                case Vars.FrameTimeMs: return s.FrameTimeMs;
                case Vars.ProcessCount: return s.ProcessCount;
                case Vars.FreeRamGb: return Math.Max(0, s.TotalRamGB - s.UsedRamGB);
                case Vars.BatteryLevel: return s.BatteryLevel;
                case Vars.IsCharging: return s.IsCharging ? 1 : 0;
                case Vars.IsGame: return s.IsGame ? 1 : 0;
                case Vars.NetworkThroughput: return s.RecvBytes / (1024.0 * 1024.0);
                default: return fallback;
            }
        }
    }
}
