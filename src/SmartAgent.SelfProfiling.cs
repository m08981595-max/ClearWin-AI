using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace ClearWin
{
    public partial class SmartAgent
    {
        private Dictionary<string, AnalyzerProfile> _analyzerProfiles = new Dictionary<string, AnalyzerProfile>();
        private SelfProfile _selfProfile;
        private DateTime _lastSelfProfile = DateTime.MinValue;
        private const int SELF_PROFILE_INTERVAL_SECONDS = 60;
        private Stopwatch _tickStopwatch = new Stopwatch();
        private Stopwatch _analyzerStopwatch = new Stopwatch();
        private long _totalTickTimeMs = 0;
        private int _tickTimeSamples = 0;
        private Process _currentProcess;

        public SelfProfile GetSelfProfile() { return _selfProfile; }
        public Dictionary<string, AnalyzerProfile> GetAnalyzerProfiles() { return new Dictionary<string, AnalyzerProfile>(_analyzerProfiles); }

        public void InitializeSelfProfiling()
        {
            _selfProfile = new SelfProfile();
            _selfProfile.StartTime = DateTime.Now;
            try { _currentProcess = Process.GetCurrentProcess(); } catch (Exception ex) { CWLog.Error("SmartAgent.SelfProfiling", ex); }
        }

        public void StartTickTimer()
        {
            _tickStopwatch.Restart();
        }

        public void EndTickTimer()
        {
            _tickStopwatch.Stop();
            _totalTickTimeMs += _tickStopwatch.ElapsedMilliseconds;
            _tickTimeSamples++;
        }

        public void StartAnalyzerTimer(string analyzerName)
        {
            _analyzerStopwatch.Restart();
        }

        public void EndAnalyzerTimer(string analyzerName)
        {
            _analyzerStopwatch.Stop();
            AnalyzerProfile profile;
            if (!_analyzerProfiles.TryGetValue(analyzerName, out profile))
            {
                profile = new AnalyzerProfile();
                profile.Name = analyzerName;
                profile.TotalRuns = 0;
                profile.TotalTimeMs = 0;
                profile.MaxTimeMs = 0;
                profile.MinTimeMs = long.MaxValue;
                _analyzerProfiles[analyzerName] = profile;
            }
            long elapsed = _analyzerStopwatch.ElapsedMilliseconds;
            profile.TotalRuns++;
            profile.TotalTimeMs += elapsed;
            profile.AvgTimeMs = (double)profile.TotalTimeMs / profile.TotalRuns;
            if (elapsed > profile.MaxTimeMs) profile.MaxTimeMs = elapsed;
            if (elapsed < profile.MinTimeMs) profile.MinTimeMs = elapsed;
            profile.LastRun = DateTime.Now;
        }

        public void UpdateSelfProfile()
        {
            if ((DateTime.Now - _lastSelfProfile).TotalSeconds < SELF_PROFILE_INTERVAL_SECONDS) return;

            _selfProfile.LastUpdated = DateTime.Now;

            try
            {
                if (_currentProcess != null)
                {
                    _currentProcess.Refresh();
                    _selfProfile.MemoryUsageMB = _currentProcess.WorkingSet64 / (1024 * 1024);
                    _selfProfile.ThreadCount = _currentProcess.Threads.Count;
                    _selfProfile.HandleCount = _currentProcess.HandleCount;
                }
            }
            catch (Exception ex) { LogError("UpdateSelfProfile", ex); }

            _selfProfile.AvgTickTimeMs = _tickTimeSamples > 0 ? (double)_totalTickTimeMs / _tickTimeSamples : 0;
            _selfProfile.TotalTickTimeMs = _totalTickTimeMs;
            _selfProfile.TotalTicks = _tickTimeSamples;

            _selfProfile.CpuOverheadPercent = _tickTimeSamples > 0
                ? (_selfProfile.AvgTickTimeMs / 1000.0) * 100.0 : 0;

            _selfProfile.SlowestAnalyzer = "";
            _selfProfile.FastestAnalyzer = "";
            double maxAvg = 0;
            double minAvg = double.MaxValue;
            foreach (var kv in _analyzerProfiles)
            {
                if (kv.Value.AvgTimeMs > maxAvg) { maxAvg = kv.Value.AvgTimeMs; _selfProfile.SlowestAnalyzer = kv.Key; }
                if (kv.Value.AvgTimeMs < minAvg && kv.Value.TotalRuns > 5) { minAvg = kv.Value.AvgTimeMs; _selfProfile.FastestAnalyzer = kv.Key; }
            }

            _selfProfile.TotalAnalyzerRuns = 0;
            foreach (var kv in _analyzerProfiles) _selfProfile.TotalAnalyzerRuns += kv.Value.TotalRuns;

            AddLog("PROFILE", "SelfProfiling",
                string.Format("Memory={0}MB Threads={1} Handles={2} AvgTick={3:F1}ms Overhead={4:F3}% Analyzers={5}",
                _selfProfile.MemoryUsageMB, _selfProfile.ThreadCount, _selfProfile.HandleCount,
                _selfProfile.AvgTickTimeMs, _selfProfile.CpuOverheadPercent, _selfProfile.TotalAnalyzerRuns));

            _lastSelfProfile = DateTime.Now;
        }

        public void SaveSelfProfile()
        {
            try
            {
                string path = Path.Combine(_dataDir, "self_profile.txt");
                var sb = new StringBuilder();
                sb.AppendLine("=== Self Profile ===");
                sb.AppendLine("Memory: " + _selfProfile.MemoryUsageMB + " MB");
                sb.AppendLine("Threads: " + _selfProfile.ThreadCount);
                sb.AppendLine("Handles: " + _selfProfile.HandleCount);
                sb.AppendLine("Avg Tick: " + string.Format("{0:F1}", _selfProfile.AvgTickTimeMs) + " ms");
                sb.AppendLine("Overhead: " + string.Format("{0:F3}", _selfProfile.CpuOverheadPercent) + "%");
                sb.AppendLine("Total Ticks: " + _selfProfile.TotalTicks);
                sb.AppendLine("");
                sb.AppendLine("=== Analyzer Profiles ===");
                foreach (var kv in _analyzerProfiles)
                {
                    sb.AppendLine(string.Format("{0}: Runs={1} Avg={2:F1}ms Max={3}ms Min={4}ms",
                        kv.Key, kv.Value.TotalRuns, kv.Value.AvgTimeMs, kv.Value.MaxTimeMs, kv.Value.MinTimeMs));
                }
                File.WriteAllText(path, sb.ToString());
            }
            catch (Exception ex) { LogError("SaveSelfProfile", ex); }
        }
    }
}
