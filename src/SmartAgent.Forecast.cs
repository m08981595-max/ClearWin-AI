using System;
using System.Collections.Generic;
using System.Linq;

namespace ClearWin
{
    public partial class SmartAgent
    {
        // === Forecast Fields ===
        private readonly object _forecastLock = new object();
        private List<PerformanceForecast> _forecasts = new List<PerformanceForecast>();
        private DateTime _lastForecastTime = DateTime.MinValue;
        private const int FORECAST_INTERVAL_SECONDS = 30;
        private double[] _forecastCpuHistory = new double[120];
        private double[] _forecastRamHistory = new double[120];
        private double[] _forecastTempHistory = new double[120];
        private double[] _forecastDiskHistory = new double[120];
        private int _forecastIndex = 0;
        private int _forecastCount = 0;
        private PerformanceForecast _activeForecast;

        // === NEW: Seasonal Patterns (24 hours × 4 quarters = 96 slots) ===
        private double[] _forecastSeasonalCpuPattern = new double[96];
        private double[] _forecastSeasonalRamPattern = new double[96];
        private double[] _forecastSeasonalTempPattern = new double[96];
        private int[] _forecastSeasonalSampleCount = new int[96];
        private DateTime _lastForecastSeasonalUpdate = DateTime.MinValue;
        private const int FORECAST_SEASONAL_UPDATE_INTERVAL_SECONDS = 60;

        // === NEW: Holt-Winters Exponential Smoothing ===
        private double _hwLevel = 0;
        private double _hwTrend = 0;
        private double[] _hwSeasonal = new double[96];
        private bool _hwInitialized = false;
        private const double HW_ALPHA = 0.3;  // Level smoothing
        private const double HW_BETA = 0.1;   // Trend smoothing
        private const double HW_GAMMA = 0.2;  // Seasonal smoothing
        private const int HW_SEASON_LENGTH = 96; // 24 hours * 4 quarters

        // === NEW: Day-of-Week Patterns ===
        private double[] _dayOfWeekCpu = new double[7];
        private double[] _dayOfWeekRam = new double[7];
        private int[] _dayOfWeekSamples = new int[7];

        // === Public Accessors ===
        public PerformanceForecast GetActiveForecast() { return _activeForecast; }
        public List<PerformanceForecast> GetForecastHistory() { return new List<PerformanceForecast>(_forecasts); }

        // === Generate Forecast ===
        public void GenerateForecast()
        {
            if ((DateTime.Now - _lastForecastTime).TotalSeconds < FORECAST_INTERVAL_SECONDS) return;
            int count;
            lock (_forecastLock) { count = _forecastCount; }
            if (count < 10) { RecordForecastSample(); return; }

            RecordForecastSample();

            // === NEW: Update seasonal patterns ===
            UpdateForecastSeasonalPatterns();

            var forecast = new PerformanceForecast();
            forecast.Timestamp = DateTime.Now;
            forecast.Predictions = new List<ForecastPrediction>();

            // === NEW: Use Holt-Winters for predictions ===
            double[] hwCpuPrediction = HoltWintersPredict(_forecastCpuHistory, _forecastCount);
            double[] hwRamPrediction = HoltWintersPredict(_forecastRamHistory, _forecastCount);
            double[] hwTempPrediction = HoltWintersPredict(_forecastTempHistory, _forecastCount);
            double[] hwDiskPrediction = HoltWintersPredict(_forecastDiskHistory, _forecastCount);

            // Predict RAM trend with seasonal adjustment
            double ramTrend = CalculateTrend(_forecastRamHistory, _forecastCount);
            double seasonalRamAdjustment = GetForecastSeasonalAdjustment("Ram");
            if (ramTrend > 0.5 || seasonalRamAdjustment > 0.3)
            {
                var pred = new ForecastPrediction();
                pred.Metric = "RAM";
                pred.Trend = "Increasing";
                pred.CurrentValue = _smoothRam;
                pred.PredictedValue1h = Math.Min(100, hwRamPrediction[0]);
                pred.PredictedValue6h = Math.Min(100, hwRamPrediction[1]);
                pred.PredictedValue24h = Math.Min(100, hwRamPrediction[2]);
                pred.Severity = pred.PredictedValue1h > 90 ? "High" : pred.PredictedValue1h > 75 ? "Medium" : "Low";

                string seasonalNote = seasonalRamAdjustment > 0.3 ? " (seasonal pattern detected)" : "";
                pred.Description = string.Format("RAM trend: {0:F1}%/min{1}. {2}", ramTrend, seasonalNote,
                    pred.PredictedValue1h > 90 ? "Will likely need cleanup within 1 hour." : "Monitor closely.");
                forecast.Predictions.Add(pred);
            }

            // Predict Temperature trend
            double tempTrend = CalculateTrend(_forecastTempHistory, _forecastCount);
            double seasonalTempAdjustment = GetForecastSeasonalAdjustment("Temp");
            if (tempTrend > 0.3 || seasonalTempAdjustment > 0.2)
            {
                var pred = new ForecastPrediction();
                pred.Metric = "Temperature";
                pred.Trend = "Rising";
                pred.CurrentValue = _smoothTemp;
                pred.PredictedValue1h = Math.Min(100, hwTempPrediction[0]);
                pred.PredictedValue6h = Math.Min(100, hwTempPrediction[1]);
                pred.PredictedValue24h = Math.Min(100, hwTempPrediction[2]);
                pred.Severity = pred.PredictedValue1h > 85 ? "Critical" : pred.PredictedValue1h > 75 ? "High" : "Medium";
                pred.Description = string.Format("Temperature rising {0:F1}C/min. {1}", tempTrend,
                    pred.PredictedValue1h > 85 ? "WARNING: Will reach critical temperature within 1 hour!" : "Thermal management recommended.");
                forecast.Predictions.Add(pred);
            }

            // Predict Disk usage
            double diskTrend = CalculateTrend(_forecastDiskHistory, _forecastCount);
            if (diskTrend > 0.1)
            {
                var pred = new ForecastPrediction();
                pred.Metric = "Disk";
                pred.Trend = "Filling";
                pred.CurrentValue = _currentState.DiskUsage;
                pred.PredictedValue1h = Math.Min(100, hwDiskPrediction[0]);
                pred.PredictedValue6h = Math.Min(100, hwDiskPrediction[1]);
                pred.PredictedValue24h = Math.Min(100, hwDiskPrediction[2]);
                pred.Severity = pred.PredictedValue6h > 95 ? "Critical" : pred.PredictedValue24h > 90 ? "High" : "Low";
                pred.Description = string.Format("Disk filling at {0:F2}%/min. {1}", diskTrend,
                    pred.PredictedValue6h > 95 ? "Disk will be full within 6 hours!" : "Cleanup recommended soon.");
                forecast.Predictions.Add(pred);
            }

            // Predict CPU trend
            double cpuTrend = CalculateTrend(_forecastCpuHistory, _forecastCount);
            double seasonalCpuAdjustment = GetForecastSeasonalAdjustment("Cpu");
            if (Math.Abs(cpuTrend) > 1.0 || seasonalCpuAdjustment > 0.5)
            {
                var pred = new ForecastPrediction();
                pred.Metric = "CPU";
                pred.Trend = cpuTrend > 0 ? "Increasing" : "Decreasing";
                pred.CurrentValue = _smoothCpu;
                pred.PredictedValue1h = Math.Max(0, Math.Min(100, hwCpuPrediction[0]));
                pred.PredictedValue6h = Math.Max(0, Math.Min(100, hwCpuPrediction[1]));
                pred.PredictedValue24h = Math.Max(0, Math.Min(100, hwCpuPrediction[2]));
                pred.Severity = cpuTrend > 2 ? "High" : "Low";

                string dayNote = GetDayOfWeekPattern();
                pred.Description = string.Format("CPU trend: {0:F1}%/min ({1}){2}", cpuTrend, pred.Trend, dayNote);
                forecast.Predictions.Add(pred);
            }

            // === NEW: Anomaly Detection ===
            DetectForecastAnomalies(forecast);

            // Overall system health forecast
            forecast.OverallTrend = "Stable";
            if (forecast.Predictions.Count > 0)
            {
                int criticalCount = 0;
                foreach (var p in forecast.Predictions)
                {
                    if (p.Severity == "Critical") criticalCount++;
                }
                if (criticalCount > 0) forecast.OverallTrend = "Declining";
                else
                {
                    int warningCount = 0;
                    foreach (var p in forecast.Predictions)
                    {
                        if (p.Severity == "High" || p.Severity == "Medium") warningCount++;
                    }
                    if (warningCount > 1) forecast.OverallTrend = "Watch";
                }
            }
            forecast.NextActionRecommendation = GenerateForecastRecommendation(forecast);

            _activeForecast = forecast;
            _forecasts.Add(forecast);
            if (_forecasts.Count > 50) _forecasts.RemoveAt(0);
            _lastForecastTime = DateTime.Now;

            if (forecast.Predictions.Count > 0)
            {
                AddLog("FORECAST", "Performance",
                    string.Format("Forecast: {0} predictions, Trend={1}, Next: {2}",
                    forecast.Predictions.Count, forecast.OverallTrend, forecast.NextActionRecommendation));
            }
        }

        // === Record Forecast Sample ===
        private void RecordForecastSample()
        {
            lock (_forecastLock)
            {
                _forecastCpuHistory[_forecastIndex] = _smoothCpu;
                _forecastRamHistory[_forecastIndex] = _smoothRam;
                _forecastTempHistory[_forecastIndex] = _smoothTemp;
                _forecastDiskHistory[_forecastIndex] = _currentState.DiskUsage;
                _forecastIndex = (_forecastIndex + 1) % _forecastCpuHistory.Length;
                if (_forecastCount < _forecastCpuHistory.Length) _forecastCount++;
            }
        }

        // === Calculate Linear Trend (per minute) ===
        private double CalculateTrend(double[] history, int count)
        {
            if (count < 5) return 0;
            lock (_forecastLock)
            {
                int n = Math.Min(count, history.Length);
                double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
                for (int i = 0; i < n; i++)
                {
                    double x = i;
                    double y = history[(_forecastIndex - n + i + history.Length) % history.Length];
                    sumX += x; sumY += y; sumXY += x * y; sumX2 += x * x;
                }
                double denom = n * sumX2 - sumX * sumX;
                if (Math.Abs(denom) < 0.001) return 0;
                double slope = (n * sumXY - sumX * sumY) / denom;
                return slope;
            }
        }

        // ============================================================
        // === NEW: Holt-Winters Exponential Smoothing ===
        // ============================================================
        private double[] HoltWintersPredict(double[] history, int count)
        {
            // Returns [1h, 6h, 24h] predictions
            double[] predictions = new double[3];

            if (count < 20)
            {
                // Not enough data, use linear trend
                double trend = CalculateTrend(history, count);
                double lastValue = history[(_forecastIndex - 1 + history.Length) % history.Length];
                predictions[0] = lastValue + trend * 60;
                predictions[1] = lastValue + trend * 360;
                predictions[2] = lastValue + trend * 1440;
                return predictions;
            }

            // Initialize Holt-Winters if needed
            if (!_hwInitialized)
            {
                InitializeHoltWinters(history, count);
            }

            // Update level, trend, and seasonal components
            int n = Math.Min(count, history.Length);
            for (int i = 0; i < n; i++)
            {
                double value = history[(_forecastIndex - n + i + history.Length) % history.Length];
                int seasonIndex = i % HW_SEASON_LENGTH;

                // Level update
                double detrended = value - _hwSeasonal[seasonIndex];
                _hwLevel = HW_ALPHA * detrended + (1 - HW_ALPHA) * (_hwLevel + _hwTrend);

                // Trend update
                double prevLevel = _hwLevel;
                _hwTrend = HW_BETA * (_hwLevel - prevLevel) + (1 - HW_BETA) * _hwTrend;

                // Seasonal update
                _hwSeasonal[seasonIndex] = HW_GAMMA * (value - _hwLevel) + (1 - HW_GAMMA) * _hwSeasonal[seasonIndex];
            }

            // Generate predictions
            int currentSeasonIndex = (count) % HW_SEASON_LENGTH;
            predictions[0] = _hwLevel + _hwTrend + _hwSeasonal[(currentSeasonIndex + 1) % HW_SEASON_LENGTH];
            predictions[1] = _hwLevel + _hwTrend * 6 + _hwSeasonal[(currentSeasonIndex + 6) % HW_SEASON_LENGTH];
            predictions[2] = _hwLevel + _hwTrend * 24 + _hwSeasonal[(currentSeasonIndex + 24) % HW_SEASON_LENGTH];

            // Clamp to valid range
            for (int i = 0; i < 3; i++)
                predictions[i] = Math.Max(0, Math.Min(100, predictions[i]));

            return predictions;
        }

        private void InitializeHoltWinters(double[] history, int count)
        {
            // Initialize level as average of first season
            int seasonLen = Math.Min(count, HW_SEASON_LENGTH);
            double sum = 0;
            for (int i = 0; i < seasonLen; i++)
            {
                sum += history[(_forecastIndex - count + i + history.Length) % history.Length];
            }
            _hwLevel = sum / seasonLen;

            // Initialize trend as difference between halves
            double firstHalf = 0, secondHalf = 0;
            int half = seasonLen / 2;
            for (int i = 0; i < half; i++)
                firstHalf += history[(_forecastIndex - count + i + history.Length) % history.Length];
            for (int i = half; i < seasonLen; i++)
                secondHalf += history[(_forecastIndex - count + i + history.Length) % history.Length];
            _hwTrend = (secondHalf - firstHalf) / (half * half);

            // Initialize seasonal components
            for (int i = 0; i < seasonLen; i++)
            {
                double value = history[(_forecastIndex - count + i + history.Length) % history.Length];
                _hwSeasonal[i] = value - _hwLevel;
            }

            _hwInitialized = true;
        }

        // ============================================================
        // === NEW: Seasonal Pattern Learning ===
        // ============================================================
        private void UpdateForecastSeasonalPatterns()
        {
            if ((DateTime.Now - _lastForecastSeasonalUpdate).TotalSeconds < FORECAST_SEASONAL_UPDATE_INTERVAL_SECONDS) return;
            _lastForecastSeasonalUpdate = DateTime.Now;

            // Get current time slot (0-95)
            int hour = DateTime.Now.Hour;
            int quarter = DateTime.Now.Minute / 15;
            int slot = hour * 4 + quarter;

            // Update CPU pattern
            UpdateSeasonalSlot(_forecastSeasonalCpuPattern, _forecastSeasonalSampleCount, slot, _smoothCpu);
            UpdateSeasonalSlot(_forecastSeasonalRamPattern, _forecastSeasonalSampleCount, slot, _smoothRam);
            UpdateSeasonalSlot(_forecastSeasonalTempPattern, _forecastSeasonalSampleCount, slot, _smoothTemp);

            // Update day-of-week patterns
            int dayOfWeek = (int)DateTime.Now.DayOfWeek;
            UpdateDayOfWeekPattern(_dayOfWeekCpu, _dayOfWeekSamples, dayOfWeek, _smoothCpu);
            UpdateDayOfWeekPattern(_dayOfWeekRam, _dayOfWeekSamples, dayOfWeek, _smoothRam);
        }

        private void UpdateSeasonalSlot(double[] pattern, int[] sampleCount, int slot, double value)
        {
            // Exponential moving average for seasonal pattern
            double alpha = 0.1;
            if (sampleCount[slot] == 0)
            {
                pattern[slot] = value;
            }
            else
            {
                pattern[slot] = pattern[slot] * (1 - alpha) + value * alpha;
            }
            sampleCount[slot]++;
        }

        private void UpdateDayOfWeekPattern(double[] pattern, int[] sampleCount, int day, double value)
        {
            double alpha = 0.05;
            if (sampleCount[day] == 0)
            {
                pattern[day] = value;
            }
            else
            {
                pattern[day] = pattern[day] * (1 - alpha) + value * alpha;
            }
            sampleCount[day]++;
        }

        private double GetForecastSeasonalAdjustment(string metric)
        {
            int hour = DateTime.Now.Hour;
            int quarter = DateTime.Now.Minute / 15;
            int slot = hour * 4 + quarter;
            int nextSlot = (slot + 1) % 96;

            double[] pattern;
            switch (metric)
            {
                case "Cpu": pattern = _forecastSeasonalCpuPattern; break;
                case "Ram": pattern = _forecastSeasonalRamPattern; break;
                case "Temp": pattern = _forecastSeasonalTempPattern; break;
                default: return 0;
            }

            // Compare current expected vs next expected
            double currentExpected = pattern[slot];
            double nextExpected = pattern[nextSlot];
            return nextExpected - currentExpected;
        }

        private string GetDayOfWeekPattern()
        {
            int dayOfWeek = (int)DateTime.Now.DayOfWeek;
            double todayCpu = _dayOfWeekCpu[dayOfWeek];
            double avgCpu = 0;
            for (int i = 0; i < 7; i++) avgCpu += _dayOfWeekCpu[i];
            avgCpu /= 7;

            if (todayCpu > avgCpu * 1.2)
                return " (above average for " + DateTime.Now.DayOfWeek + ")";
            else if (todayCpu < avgCpu * 0.8)
                return " (below average for " + DateTime.Now.DayOfWeek + ")";
            return "";
        }

        // ============================================================
        // === NEW: Anomaly Detection in Forecasts ===
        // ============================================================
        private void DetectForecastAnomalies(PerformanceForecast forecast)
        {
            // Check if current values deviate significantly from seasonal expectations
            int hour = DateTime.Now.Hour;
            int quarter = DateTime.Now.Minute / 15;
            int slot = hour * 4 + quarter;

            // CPU anomaly
            if (_forecastSeasonalSampleCount[slot] > 5)
            {
                double expectedCpu = _forecastSeasonalCpuPattern[slot];
                double deviation = Math.Abs(_smoothCpu - expectedCpu);
                if (deviation > 20)
                {
                    var pred = new ForecastPrediction();
                    pred.Metric = "CPU";
                    pred.Trend = "Anomaly";
                    pred.CurrentValue = _smoothCpu;
                    pred.PredictedValue1h = _smoothCpu;
                    pred.PredictedValue6h = _smoothCpu;
                    pred.PredictedValue24h = expectedCpu;
                    pred.Severity = deviation > 30 ? "High" : "Medium";
                    pred.Description = string.Format("CPU anomaly: current {0:F0}% vs expected {1:F0}% ({2:F0}% deviation)",
                        _smoothCpu, expectedCpu, deviation);
                    forecast.Predictions.Add(pred);
                }
            }

            // RAM anomaly
            if (_forecastSeasonalSampleCount[slot] > 5)
            {
                double expectedRam = _forecastSeasonalRamPattern[slot];
                double deviation = Math.Abs(_smoothRam - expectedRam);
                if (deviation > 15)
                {
                    var pred = new ForecastPrediction();
                    pred.Metric = "RAM";
                    pred.Trend = "Anomaly";
                    pred.CurrentValue = _smoothRam;
                    pred.PredictedValue1h = _smoothRam;
                    pred.PredictedValue6h = _smoothRam;
                    pred.PredictedValue24h = expectedRam;
                    pred.Severity = deviation > 25 ? "High" : "Medium";
                    pred.Description = string.Format("RAM anomaly: current {0:F0}% vs expected {1:F0}%",
                        _smoothRam, expectedRam);
                    forecast.Predictions.Add(pred);
                }
            }
        }

        // === Generate Forecast Recommendation ===
        private string GenerateForecastRecommendation(PerformanceForecast forecast)
        {
            if (forecast.Predictions.Count == 0) return "System stable, no action needed";

            foreach (var p in forecast.Predictions)
            {
                if (p.Severity == "Critical")
                    return "Urgent: " + p.Metric + " needs immediate attention";
            }
            foreach (var p in forecast.Predictions)
            {
                if (p.Severity == "High")
                    return "Recommended: Address " + p.Metric + " trend soon";
            }
            return "Monitor: Minor trends detected";
        }

        // === NEW: Get seasonal data for UI ===
        public double[] GetSeasonalPattern(string metric)
        {
            switch (metric)
            {
                case "Cpu": return _forecastSeasonalCpuPattern;
                case "Ram": return _forecastSeasonalRamPattern;
                case "Temp": return _forecastSeasonalTempPattern;
                default: return new double[96];
            }
        }

        public double[] GetDayOfWeekPattern(string metric)
        {
            switch (metric)
            {
                case "Cpu": return _dayOfWeekCpu;
                case "Ram": return _dayOfWeekRam;
                default: return new double[7];
            }
        }
    }
}
