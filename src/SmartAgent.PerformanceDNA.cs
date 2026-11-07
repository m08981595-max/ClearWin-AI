using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ClearWin
{
    public partial class SmartAgent
    {
        // === Performance DNA Fields ===
        private PerformanceDNAProfile _performanceDNA;
        private DateTime _lastDNAUpdate = DateTime.MinValue;
        private const int DNA_UPDATE_INTERVAL_SECONDS = 120;
        private const int MAX_DNA_SAMPLES = 5000;
        private List<DNASample> _dnaSamples = new List<DNASample>();
        private Dictionary<int, double[]> _hourlyPerfAvg = new Dictionary<int, double[]>();
        private Dictionary<string, int> _appFrequency = new Dictionary<string, int>();
        private Dictionary<string, double> _appAvgFps = new Dictionary<string, double>();
        private Dictionary<string, double> _appAvgCpu = new Dictionary<string, double>();
        private Dictionary<string, double> _appAvgTemp = new Dictionary<string, double>();
        private int _dnaSampleCount = 0;
        private DateTime _dnaFirstSample = DateTime.MinValue;

        // === Public Accessors ===
        public PerformanceDNAProfile GetPerformanceDNA() { return _performanceDNA; }
        public int GetBestPerformanceHour()
        {
            if (_performanceDNA == null || _performanceDNA.HourlyScore == null) return 12;
            int bestHour = 0;
            double bestScore = 0;
            for (int i = 0; i < _performanceDNA.HourlyScore.Length && i < 24; i++)
            {
                if (_performanceDNA.HourlyScore[i] > bestScore)
                {
                    bestScore = _performanceDNA.HourlyScore[i];
                    bestHour = i;
                }
            }
            return bestHour;
        }
        public int GetWorstPerformanceHour()
        {
            if (_performanceDNA == null || _performanceDNA.HourlyScore == null) return 14;
            int worstHour = 0;
            double worstScore = 100;
            for (int i = 0; i < _performanceDNA.HourlyScore.Length && i < 24; i++)
            {
                if (_performanceDNA.HourlyScore[i] < worstScore)
                {
                    worstScore = _performanceDNA.HourlyScore[i];
                    worstHour = i;
                }
            }
            return worstHour;
        }
        public string GetMostUsedApp()
        {
            string topApp = "";
            int topCount = 0;
            foreach (var kv in _appFrequency)
            {
                if (kv.Value > topCount) { topCount = kv.Value; topApp = kv.Key; }
            }
            return topApp;
        }

        // === Collect DNA Sample ===
        public void CollectDNASample()
        {
            if ((DateTime.Now - _lastDNAUpdate).TotalSeconds < DNA_UPDATE_INTERVAL_SECONDS) return;

            if (_dnaFirstSample == DateTime.MinValue) _dnaFirstSample = DateTime.Now;

            var sample = new DNASample();
            sample.Timestamp = DateTime.Now;
            sample.Hour = DateTime.Now.Hour;
            sample.DayOfWeek = (int)DateTime.Now.DayOfWeek;
            sample.CpuUsage = _smoothCpu;
            sample.RamUsage = _smoothRam;
            sample.Temp = _smoothTemp;
            sample.GpuUsage = _smoothGpu;
            sample.Fps = _smoothFps;
            sample.IsGame = _currentState.IsGame;
            sample.ActiveProcess = _currentState.ActiveProcess ?? "";
            sample.BatteryPercent = (int)_currentState.BatteryLevel;
            sample.IsOnBattery = !_currentState.IsCharging;

            _dnaSamples.Add(sample);
            if (_dnaSamples.Count > MAX_DNA_SAMPLES) _dnaSamples.RemoveAt(0);
            _dnaSampleCount++;

            int hour = sample.Hour;
            if (!_hourlyPerfAvg.ContainsKey(hour))
                _hourlyPerfAvg[hour] = new double[5];
            double[] avg = _hourlyPerfAvg[hour];
            double alpha = 0.1;
            avg[0] = avg[0] * (1 - alpha) + sample.CpuUsage * alpha;
            avg[1] = avg[1] * (1 - alpha) + sample.RamUsage * alpha;
            avg[2] = avg[2] * (1 - alpha) + sample.Temp * alpha;
            avg[3] = avg[3] * (1 - alpha) + sample.Fps * alpha;
            avg[4] = avg[4] * (1 - alpha) + sample.GpuUsage * alpha;

            if (!string.IsNullOrEmpty(sample.ActiveProcess))
            {
                string app = sample.ActiveProcess.ToLower();
                int freq;
                if (!_appFrequency.TryGetValue(app, out freq)) freq = 0;
                _appFrequency[app] = freq + 1;

                double avgFps;
                if (!_appAvgFps.TryGetValue(app, out avgFps)) avgFps = sample.Fps;
                _appAvgFps[app] = avgFps * 0.95 + sample.Fps * 0.05;

                double avgCpu;
                if (!_appAvgCpu.TryGetValue(app, out avgCpu)) avgCpu = sample.CpuUsage;
                _appAvgCpu[app] = avgCpu * 0.95 + sample.CpuUsage * 0.05;

                double avgTemp;
                if (!_appAvgTemp.TryGetValue(app, out avgTemp)) avgTemp = sample.Temp;
                _appAvgTemp[app] = avgTemp * 0.95 + sample.Temp * 0.05;
            }

            _lastDNAUpdate = DateTime.Now;

            if (_dnaSampleCount % 50 == 0) RebuildDNAProfile();
        }

        // === Rebuild DNA Profile ===
        private void RebuildDNAProfile()
        {
            if (_performanceDNA == null) _performanceDNA = new PerformanceDNAProfile();
            _performanceDNA.LastUpdated = DateTime.Now;
            _performanceDNA.TotalSamples = _dnaSampleCount;

            _performanceDNA.HourlyScore = new double[24];
            for (int h = 0; h < 24; h++)
            {
                double[] avg;
                if (_hourlyPerfAvg.TryGetValue(h, out avg))
                {
                    double fpsScore = Math.Min(100, avg[3]);
                    double tempPenalty = Math.Max(0, (avg[2] - 45) * 2);
                    double cpuPenalty = avg[0] * 0.3;
                    _performanceDNA.HourlyScore[h] = Math.Max(0, fpsScore - tempPenalty - cpuPenalty);
                }
                else
                {
                    _performanceDNA.HourlyScore[h] = 50;
                }
            }

            _performanceDNA.PeakThermalHour = 0;
            double maxTemp = 0;
            for (int h = 0; h < 24; h++)
            {
                double[] avg;
                if (_hourlyPerfAvg.TryGetValue(h, out avg) && avg[2] > maxTemp)
                {
                    maxTemp = avg[2];
                    _performanceDNA.PeakThermalHour = h;
                }
            }
            _performanceDNA.AvgPeakTemp = maxTemp;

            _performanceDNA.TopApps = new List<string>();
            var sorted = _appFrequency.OrderByDescending(kv => kv.Value).ToList();
            for (int i = 0; i < Math.Min(10, sorted.Count); i++)
                _performanceDNA.TopApps.Add(sorted[i].Key);

            _performanceDNA.TopGames = new List<string>();
            foreach (var kv in _appAvgFps)
            {
                if (kv.Value > 30)
                    _performanceDNA.TopGames.Add(kv.Key);
            }

            int morningSamples = 0, afternoonSamples = 0, eveningSamples = 0, nightSamples = 0;
            foreach (var s in _dnaSamples)
            {
                if (s.Hour >= 6 && s.Hour < 12) morningSamples++;
                else if (s.Hour >= 12 && s.Hour < 18) afternoonSamples++;
                else if (s.Hour >= 18 && s.Hour < 23) eveningSamples++;
                else nightSamples++;
            }
            _performanceDNA.MorningUsage = morningSamples;
            _performanceDNA.AfternoonUsage = afternoonSamples;
            _performanceDNA.EveningUsage = eveningSamples;
            _performanceDNA.NightUsage = nightSamples;

            double avgFps = 0;
            if (_dnaSamples.Count > 0)
            {
                double totalFps = 0;
                int gameCount = 0;
                foreach (var s in _dnaSamples)
                {
                    if (s.IsGame && s.Fps > 0) { totalFps += s.Fps; gameCount++; }
                }
                avgFps = gameCount > 0 ? totalFps / gameCount : 60;
            }
            if (avgFps >= 90) _performanceDNA.DeviceTier = "High";
            else if (avgFps >= 50) _performanceDNA.DeviceTier = "Mid";
            else _performanceDNA.DeviceTier = "Low";

            AddLog("DNA", "PerformanceDNA",
                string.Format("DNA rebuilt: Tier={0} Samples={1} BestHour={2} PeakTempHour={3} TopApps={4}",
                _performanceDNA.DeviceTier, _dnaSampleCount, GetBestPerformanceHour(),
                _performanceDNA.PeakThermalHour, _performanceDNA.TopApps.Count));
        }

        // === Adjust Decision Based on DNA ===
        public AgentDecision AdjustDecisionByDNA(AgentDecision decision)
        {
            if (_performanceDNA == null) return decision;

            int currentHour = DateTime.Now.Hour;
            double hourlyScore = _performanceDNA.HourlyScore != null && currentHour < 24
                ? _performanceDNA.HourlyScore[currentHour] : 50;

            if (hourlyScore > 70)
            {
                decision.Confidence = Math.Min(1.0, decision.Confidence * 1.15);
            }
            else if (hourlyScore < 30)
            {
                decision.Confidence = Math.Max(0.1, decision.Confidence * 0.8);
                decision.Risk = Math.Min(1.0, decision.Risk * 1.2);
            }

            if (_performanceDNA.DeviceTier == "Low")
            {
                decision.Confidence = Math.Max(0.1, decision.Confidence * 0.85);
            }
            else if (_performanceDNA.DeviceTier == "High")
            {
                decision.Confidence = Math.Min(1.0, decision.Confidence * 1.1);
            }

            return decision;
        }
    }
}
