using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ClearWin
{
    public partial class SmartAgent
    {
        // ===================== AI BENCHMARK =====================
        private List<BenchmarkResult> _benchmarkHistory = new List<BenchmarkResult>();
        private DateTime _lastBenchmarkTime = DateTime.MinValue;
        private const int BENCHMARK_INTERVAL_MINUTES = 60;

        public BenchmarkResult RunBenchmark()
        {
            var result = new BenchmarkResult();
            result.Timestamp = DateTime.Now;
            
            // Snapshot state under lock (fast), then compute outside lock (slow)
            double cpuUsage, ramUsage, totalRamGB, usedRamGB, cpuTemp, cpuFreqGHz, diskUsage;
            int cpuCoreCount;
            string diskType;
            double gpuVRAMBs;
            double gpuUsage;
            double prevCpuScore = 0, prevRamScore = 0, prevDiskScore = 0, prevGpuScore = 0;
            bool hasPrev = false;
            
            lock (_stateLock)
            {
                if (_currentState == null) return result;
                cpuUsage = _currentState.CpuUsage;
                ramUsage = _currentState.RamUsage;
                totalRamGB = _currentState.TotalRamGB;
                usedRamGB = _currentState.UsedRamGB;
                cpuTemp = _currentState.CpuTemp;
                cpuFreqGHz = _currentState.CpuFreqGHz;
                cpuCoreCount = _currentState.CpuCoreCount;
                diskUsage = _currentState.DiskUsage;
                gpuUsage = _currentState.GpuUsage;
                diskType = _deviceProfile.DiskType;
                gpuVRAMBs = _deviceProfile.GpuVRAMBs;
                if (_benchmarkHistory.Count > 0)
                {
                    var prev = _benchmarkHistory[_benchmarkHistory.Count - 1];
                    prevCpuScore = prev.CpuScore;
                    prevRamScore = prev.RamScore;
                    prevDiskScore = prev.DiskScore;
                    prevGpuScore = prev.GpuScore;
                    hasPrev = true;
                }
            }
            
            // Compute outside lock (expensive)
            result.CpuScore = CalculateCpuBenchmark(cpuUsage, cpuTemp, cpuCoreCount, cpuFreqGHz);
            result.RamScore = CalculateRamBenchmark(ramUsage, totalRamGB, usedRamGB);
            result.DiskScore = CalculateDiskBenchmark(diskUsage, diskType);
            result.GpuScore = CalculateGpuBenchmark(gpuUsage, gpuVRAMBs);
            result.OverallScore = (result.CpuScore * 0.25 + result.RamScore * 0.25 + result.DiskScore * 0.25 + result.GpuScore * 0.25);
            
            if (hasPrev)
            {
                result.CpuDelta = result.CpuScore - prevCpuScore;
                result.RamDelta = result.RamScore - prevRamScore;
                result.DiskDelta = result.DiskScore - prevDiskScore;
                result.GpuDelta = result.GpuScore - prevGpuScore;
                result.OverallDelta = result.OverallScore - (prevCpuScore * 0.25 + prevRamScore * 0.25 + prevDiskScore * 0.25 + prevGpuScore * 0.25);
                result.Trend = result.OverallDelta > 3 ? "Improving" : result.OverallDelta < -3 ? "Degrading" : "Stable";
            }
            else
            {
                result.Trend = "Baseline";
            }
            
            lock (_stateLock)
            {
                _benchmarkHistory.Add(result);
                if (_benchmarkHistory.Count > 100) _benchmarkHistory.RemoveAt(0);
            }
            _lastBenchmarkTime = DateTime.Now;
            
            AddLog("BENCHMARK", "Complete", string.Format("Score: {0:F0} | Trend: {1}", result.OverallScore, result.Trend));
            
            return result;
        }

        private double CalculateCpuBenchmark(double cpuUsage, double cpuTemp, int cpuCoreCount, double cpuFreqGHz)
        {
            double score = 100;
            score -= cpuUsage * 0.5;
            score += Math.Min(20, cpuCoreCount * 3);
            score += Math.Min(15, cpuFreqGHz * 2);
            if (cpuTemp > 80) score -= (cpuTemp - 80) * 2;
            return Math.Max(0, Math.Min(100, score));
        }

        private double CalculateRamBenchmark(double ramUsage, double totalRamGB, double usedRamGB)
        {
            double score = 100;
            score -= ramUsage * 0.4;
            score += Math.Min(25, totalRamGB * 1.5);
            double freeGB = totalRamGB - usedRamGB;
            if (freeGB < 1) score -= 20;
            else if (freeGB < 2) score -= 10;
            return Math.Max(0, Math.Min(100, score));
        }

        private double CalculateDiskBenchmark(double diskUsage, string diskType)
        {
            double score = 100;
            score -= Math.Max(0, (diskUsage - 70) * 1.5);
            if (diskType == "NVMe") score += 15;
            else if (diskType == "SSD") score += 10;
            return Math.Max(0, Math.Min(100, score));
        }

        private double CalculateGpuBenchmark(double gpuUsage, double gpuVRAMBs)
        {
            double score = 80;
            if (gpuUsage > 0) score += 10;
            if (gpuVRAMBs >= 8e9) score += 15;
            else if (gpuVRAMBs >= 4e9) score += 10;
            else if (gpuVRAMBs >= 2e9) score += 5;
            return Math.Max(0, Math.Min(100, score));
        }

        public bool ShouldRunBenchmark()
        {
            return (DateTime.Now - _lastBenchmarkTime).TotalMinutes >= BENCHMARK_INTERVAL_MINUTES;
        }

        public BenchmarkResult GetLastBenchmark()
        {
            return _benchmarkHistory.Count > 0 ? _benchmarkHistory[_benchmarkHistory.Count - 1] : null;
        }

        public List<BenchmarkResult> GetBenchmarkHistory()
        {
            lock (_stateLock) { return new List<BenchmarkResult>(_benchmarkHistory); }
        }

        // ===================== AI PERFORMANCE TIMELINE =====================
        private RingBuffer<PerformanceSnapshot> _performanceTimeline = new RingBuffer<PerformanceSnapshot>(2880);
        private DateTime _lastTimelineSnapshot = DateTime.MinValue;
        private const int TIMELINE_INTERVAL_SECONDS = 30;

        public void RecordPerformanceSnapshot()
        {
            if ((DateTime.Now - _lastTimelineSnapshot).TotalSeconds < TIMELINE_INTERVAL_SECONDS) return;
            _lastTimelineSnapshot = DateTime.Now;
            
            lock (_stateLock)
            {
                if (_currentState == null) return;
                
                var snapshot = new PerformanceSnapshot();
                snapshot.Timestamp = DateTime.Now;
                snapshot.CpuUsage = _currentState.CpuUsage;
                snapshot.RamUsage = _currentState.RamUsage;
                snapshot.GpuUsage = _currentState.GpuUsage;
                snapshot.DiskUsage = _currentState.DiskUsage;
                snapshot.CpuTemp = _currentState.CpuTemp;
                snapshot.Fps = _currentState.Fps;
                snapshot.FpsStability = _currentState.FpsStability;
                snapshot.ProcessCount = _currentState.ProcessCount;
                snapshot.HealthScore = SystemHealthScore;
                snapshot.Mode = _mode.ToString();
                
                _performanceTimeline.Add(snapshot);
            }
        }

        public List<PerformanceSnapshot> GetTimelineLast24h()
        {
            var cutoff = DateTime.Now.AddHours(-24);
            var result = new List<PerformanceSnapshot>();
            lock (_stateLock)
            {
                foreach (var s in _performanceTimeline)
                    if (s.Timestamp >= cutoff) result.Add(s);
            }
            return result;
        }

        public List<PerformanceSnapshot> GetTimelineLast1h()
        {
            var cutoff = DateTime.Now.AddHours(-1);
            var result = new List<PerformanceSnapshot>();
            lock (_stateLock)
            {
                foreach (var s in _performanceTimeline)
                    if (s.Timestamp >= cutoff) result.Add(s);
            }
            return result;
        }

        // ===================== SELF DIAGNOSTICS =====================
        private SelfDiagnosticsResult _selfDiag = new SelfDiagnosticsResult();
        private DateTime _lastSelfDiagTime = DateTime.MinValue;
        private const int SELF_DIAG_INTERVAL_SECONDS = 30;

        public SelfDiagnosticsResult RunSelfDiagnostics()
        {
            if ((DateTime.Now - _lastSelfDiagTime).TotalSeconds < SELF_DIAG_INTERVAL_SECONDS) return _selfDiag;
            _lastSelfDiagTime = DateTime.Now;
            
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                
                _selfDiag.ClearWinRamMB = currentProcess.WorkingSet64 / 1048576.0;
                _selfDiag.ThreadCount = currentProcess.Threads.Count;
                _selfDiag.HandleCount = currentProcess.HandleCount;
                _selfDiag.UptimeMinutes = (DateTime.Now - _stats.StartTime).TotalMinutes;
                
                // Decision speed
                if (_stats.TotalDecisions > 0)
                {
                    double totalMs = 0;
                    int count = 0;
                    foreach (var kvp in _actionExecTimes)
                    {
                        foreach (double ms in kvp.Value) { totalMs += ms; count++; }
                    }
                    _selfDiag.AvgDecisionMs = count > 0 ? totalMs / count : 0;
                }
                
                // Memory leak detection
                _selfDiag.RamHistory.Add(_selfDiag.ClearWinRamMB);
                if (_selfDiag.RamHistory.Count > 60) _selfDiag.RamHistory.RemoveAt(0);
                if (_selfDiag.RamHistory.Count >= 30)
                {
                    double first10 = 0, last10 = 0;
                    for (int i = 0; i < 10; i++) { first10 += _selfDiag.RamHistory[i]; last10 += _selfDiag.RamHistory[_selfDiag.RamHistory.Count - 10 + i]; }
                    first10 /= 10; last10 /= 10;
                    _selfDiag.MemoryTrend = last10 - first10;
                    _selfDiag.HasMemoryLeak = _selfDiag.MemoryTrend > 50; // 50MB growth over window
                }
                
                // Health status
                _selfDiag.IsHealthy = _selfDiag.ClearWinRamMB < 300 && _selfDiag.ThreadCount < 60 && _selfDiag.HandleCount < 3000 && !_selfDiag.HasMemoryLeak;
                _selfDiag.Status = _selfDiag.IsHealthy ? "Healthy" : "Warning";
                
                if (_selfDiag.HasMemoryLeak)
                    AddLog("WARN", "SelfDiag", string.Format("Possible memory leak: RAM grew {0:F0}MB over monitoring window", _selfDiag.MemoryTrend));
                
                currentProcess.Dispose();
            }
            catch (Exception ex) { LogError("RunSelfDiagnostics", ex); }
            
            return _selfDiag;
        }

        public SelfDiagnosticsResult GetSelfDiagnostics() { return _selfDiag; }
    }

    // ===================== MODELS =====================
    public class BenchmarkResult
    {
        public DateTime Timestamp;
        public double CpuScore;
        public double RamScore;
        public double DiskScore;
        public double GpuScore;
        public double OverallScore;
        public double CpuDelta;
        public double RamDelta;
        public double DiskDelta;
        public double GpuDelta;
        public double OverallDelta;
        public string Trend;
        
        public BenchmarkResult() { Trend = "Unknown"; }
    }

    public class PerformanceSnapshot
    {
        public DateTime Timestamp;
        public double CpuUsage;
        public double RamUsage;
        public double GpuUsage;
        public double DiskUsage;
        public double CpuTemp;
        public double Fps;
        public double FpsStability;
        public int ProcessCount;
        public double HealthScore;
        public string Mode;
    }

    public class SelfDiagnosticsResult
    {
        public double ClearWinRamMB;
        public int ThreadCount;
        public int HandleCount;
        public double UptimeMinutes;
        public double AvgDecisionMs;
        public double MemoryTrend;
        public bool HasMemoryLeak;
        public bool IsHealthy;
        public string Status;
        public List<double> RamHistory;
        
        public SelfDiagnosticsResult()
        {
            RamHistory = new List<double>();
            Status = "Unknown";
        }
    }
}
