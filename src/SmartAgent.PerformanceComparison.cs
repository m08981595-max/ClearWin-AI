using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ClearWin
{
    // === PERFORMANCE COMPARISON ===
    // Tracks before/after optimization metrics
    // Provides concrete evidence of improvement
    public partial class SmartAgent
    {
        private List<OptimizationSnapshot> _snapshots = new List<OptimizationSnapshot>();
        private DateTime _lastSnapshotTime = DateTime.MinValue;
        private const int SNAPSHOT_INTERVAL = 60;
        private const int MAX_SNAPSHOTS = 100;

        public void InitializePerformanceComparison()
        {
            _snapshots.Clear();
            RecordSnapshot("Baseline");
        }

        public void RecordSnapshot(string label)
        {
            try
            {
                OptimizationSnapshot snapshot = new OptimizationSnapshot
                {
                    Label = label,
                    Timestamp = DateTime.Now,
                    CpuUsage = GetCurrentCpuUsage(),
                    RamUsage = GetCurrentRamUsage(),
                    DiskUsage = GetCurrentDiskUsage(),
                    GpuUsage = GetCurrentGpuUsage(),
                    CpuTemp = GetCurrentCpuTemp(),
                    Fps = _currentState.Fps,
                    ProcessCount = _currentState.ProcessCount,
                    ThreadCount = _currentState.ThreadCount
                };

                snapshot.OverallScore = CalculatePerformanceScore(snapshot);
                _snapshots.Add(snapshot);

                if (_snapshots.Count > MAX_SNAPSHOTS)
                    _snapshots.RemoveAt(1); // keep index 0 (Baseline) intact, evict oldest after it
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[PerformanceComparison] Error: " + ex.Message);
            }
        }

        private double CalculatePerformanceScore(OptimizationSnapshot s)
        {
            double cpuScore = Math.Max(0, 100 - s.CpuUsage);
            double ramScore = Math.Max(0, 100 - s.RamUsage);
            double tempScore = Math.Max(0, 100 - (s.CpuTemp - 40));
            double fpsScore = Math.Min(100, s.Fps * 2);

            return (cpuScore * 0.3 + ramScore * 0.3 + tempScore * 0.2 + fpsScore * 0.2);
        }

        public PerformanceComparisonResult ComparePerformance(string label = null)
        {
            PerformanceComparisonResult result = new PerformanceComparisonResult();

            if (_snapshots.Count < 2)
            {
                result.HasEnoughData = false;
                return result;
            }

            result.HasEnoughData = true;

            OptimizationSnapshot baseline = _snapshots[0];
            OptimizationSnapshot current = _snapshots[_snapshots.Count - 1];

            if (label != null)
            {
                for (int i = _snapshots.Count - 1; i >= 0; i--)
                {
                    if (_snapshots[i].Label == label)
                    {
                        baseline = _snapshots[i];
                        break;
                    }
                }
            }

            result.Baseline = baseline;
            result.Current = current;
            result.CpuImprovement = baseline.CpuUsage - current.CpuUsage;
            result.RamImprovement = baseline.RamUsage - current.RamUsage;
            result.TempImprovement = baseline.CpuTemp - current.CpuTemp;
            result.FpsImprovement = current.Fps - baseline.Fps;
            result.ScoreImprovement = current.OverallScore - baseline.OverallScore;
            result.TimeSpan = current.Timestamp - baseline.Timestamp;

            return result;
        }

        public string GetPerformanceComparisonSummary()
        {
            PerformanceComparisonResult result = ComparePerformance();
            if (!result.HasEnoughData)
                return "Not enough data for comparison";

            string summary = string.Format(
                "Performance Comparison ({0:F1} min span):\n" +
                "  CPU: {1:F1}% → {2:F1}% ({3}{4:F1}%)\n" +
                "  RAM: {5:F1}% → {6:F1}% ({7}{8:F1}%)\n" +
                "  Temp: {9:F1}°C → {10:F1}°C ({11}{12:F1}°C)\n" +
                "  FPS: {13:F0} → {14:F0} ({15}{16:F0})\n" +
                "  Score: {17:F1} → {18:F1} ({19}{20:F1})",
                result.TimeSpan.TotalMinutes,
                result.Baseline.CpuUsage, result.Current.CpuUsage,
                result.CpuImprovement >= 0 ? "↓" : "↑", Math.Abs(result.CpuImprovement),
                result.Baseline.RamUsage, result.Current.RamUsage,
                result.RamImprovement >= 0 ? "↓" : "↑", Math.Abs(result.RamImprovement),
                result.Baseline.CpuTemp, result.Current.CpuTemp,
                result.TempImprovement >= 0 ? "↓" : "↑", Math.Abs(result.TempImprovement),
                result.Baseline.Fps, result.Current.Fps,
                result.FpsImprovement >= 0 ? "↑" : "↓", Math.Abs(result.FpsImprovement),
                result.Baseline.OverallScore, result.Current.OverallScore,
                result.ScoreImprovement >= 0 ? "↑" : "↓", Math.Abs(result.ScoreImprovement));

            return summary;
        }

        public double GetOverallImprovementPercent()
        {
            PerformanceComparisonResult result = ComparePerformance();
            if (!result.HasEnoughData) return 0;
            return result.ScoreImprovement;
        }
    }

    public class OptimizationSnapshot
    {
        public string Label;
        public DateTime Timestamp;
        public double CpuUsage;
        public double RamUsage;
        public double DiskUsage;
        public double GpuUsage;
        public double CpuTemp;
        public double Fps;
        public int ProcessCount;
        public int ThreadCount;
        public double OverallScore;
    }

    public class PerformanceComparisonResult
    {
        public bool HasEnoughData;
        public OptimizationSnapshot Baseline;
        public OptimizationSnapshot Current;
        public double CpuImprovement;
        public double RamImprovement;
        public double TempImprovement;
        public double FpsImprovement;
        public double ScoreImprovement;
        public TimeSpan TimeSpan;
    }
}
