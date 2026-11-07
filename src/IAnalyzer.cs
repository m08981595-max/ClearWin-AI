using System;

namespace ClearWin
{
    public sealed class AnalysisResult
    {
        public string Action;
        public string Reason;
        public double Confidence;
        public int Priority;
        public DecisionCategory Category;
        public DecisionSafety Safety;
        public bool IsHeavy;
    }

    public sealed class SystemSnapshot
    {
        public double CpuUsage, RamUsage, GpuUsage, CpuTemp, GpuTemp;
        public double Fps, FpsStability;
        public double DiskUsage, BatteryLevel;
        public string ActiveProcess;
        public int ProcessCount;
        public bool IsCharging;
        public string Mode;
    }

    public interface IAnalyzer
    {
        string Name { get; }
        bool CanRun(SystemSnapshot snapshot);
        AnalysisResult Analyze(SystemSnapshot snapshot);
    }
}
