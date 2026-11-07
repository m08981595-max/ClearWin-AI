using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ClearWin
{
    // === RESOURCE FORECASTING ===
    // Predicts future resource usage using trend analysis
    // Forecasts: CPU, RAM, Disk, GPU usage for next 5/15/30 minutes
    public partial class SmartAgent
    {
        private List<ResourceForecastResult> _forecastHistory2 = new List<ResourceForecastResult>();
        private Dictionary<string, double[]> _resourceHistory2 = new Dictionary<string, double[]>();
        private Dictionary<string, int> _resourceHistoryIndex2 = new Dictionary<string, int>();
        private DateTime _lastForecastUpdate2 = DateTime.MinValue;
        private const int FORECAST_UPDATE_INTERVAL2 = 60;
        private const int FORECAST_HISTORY_SIZE2 = 60;

        public void InitializeResourceForecasting()
        {
            _forecastHistory2.Clear();
            _resourceHistory2.Clear();
            _resourceHistoryIndex2.Clear();

            string[] resources = new[] { "CPU", "RAM", "Disk", "GPU", "FPS", "Temp" };
            for (int i = 0; i < resources.Length; i++)
            {
                _resourceHistory2[resources[i]] = new double[FORECAST_HISTORY_SIZE2];
                _resourceHistoryIndex2[resources[i]] = 0;
            }
        }

        public ResourceForecastResult UpdateResourceForecasting()
        {
            ResourceForecastResult result = new ResourceForecastResult();
            result.Forecasts = new List<ResourceForecastAdvanced>();
            result.Predictions = new List<ResourcePrediction>();
            result.Alerts = new List<ForecastAlert>();

            try
            {
                UpdateResourceHistories2();

                if ((DateTime.Now - _lastForecastUpdate2).TotalSeconds >= FORECAST_UPDATE_INTERVAL2)
                {
                    _lastForecastUpdate2 = DateTime.Now;
                    result.Forecasts = GenerateForecasts2();
                    result.Predictions = GenerateResourcePredictions2();
                    result.Alerts = CheckForecastAlerts2();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[ResourceForecasting] Error: " + ex.Message);
            }

            return result;
        }

        private void UpdateResourceHistories2()
        {
            try
            {
                AddToHistory2("CPU", _currentState.CpuUsage);
                AddToHistory2("RAM", _currentState.RamUsage);
                AddToHistory2("Disk", _currentState.DiskUsage);
                AddToHistory2("GPU", _currentState.GpuUsage);
                AddToHistory2("FPS", _currentState.Fps);
                AddToHistory2("Temp", _currentState.CpuTemp);
            }
            catch (Exception ex) { LogError("UpdateResourceHistories2", ex); }
        }

        private void AddToHistory2(string resource, double value)
        {
            if (!_resourceHistory2.ContainsKey(resource)) return;
            double[] history = _resourceHistory2[resource];
            int idx = _resourceHistoryIndex2[resource];
            history[idx] = value;
            _resourceHistoryIndex2[resource] = (idx + 1) % FORECAST_HISTORY_SIZE2;
        }

        private List<ResourceForecastAdvanced> GenerateForecasts2()
        {
            List<ResourceForecastAdvanced> forecasts = new List<ResourceForecastAdvanced>();
            string[] resources = new[] { "CPU", "RAM", "GPU", "Temp" };
            for (int i = 0; i < resources.Length; i++)
            {
                ResourceForecastAdvanced f = GenerateSingleForecast2(resources[i]);
                if (f != null) forecasts.Add(f);
            }
            return forecasts;
        }

        private ResourceForecastAdvanced GenerateSingleForecast2(string resource)
        {
            if (!_resourceHistory2.ContainsKey(resource)) return null;
            double[] history = _resourceHistory2[resource];
            int count = FORECAST_HISTORY_SIZE2;

            double sum = 0, sumSq = 0;
            for (int i = 0; i < count; i++)
            {
                sum += history[i];
                sumSq += history[i] * history[i];
            }
            double mean = sum / count;
            double variance = sumSq / count - mean * mean;
            double stdDev = Math.Sqrt(Math.Max(0, variance));
            double trend = CalcLinearTrend2(history, count);
            double current = history[(_resourceHistoryIndex2[resource] - 1 + FORECAST_HISTORY_SIZE2) % FORECAST_HISTORY_SIZE2];

            ResourceForecastAdvanced f = new ResourceForecastAdvanced();
            f.Resource = resource;
            f.CurrentValue = current;
            f.Mean = mean;
            f.StdDev = stdDev;
            f.Trend = trend;
            f.Forecast5Min = Math.Max(0, Math.Min(100, mean + trend * 2.5));
            f.Forecast15Min = Math.Max(0, Math.Min(100, mean + trend * 7.5));
            f.Forecast30Min = Math.Max(0, Math.Min(100, mean + trend * 15));
            f.Confidence = Math.Max(0.2, 1.0 - stdDev / (mean + 0.01) * 0.5);
            f.Timestamp = DateTime.Now;
            return f;
        }

        private double CalcLinearTrend2(double[] data, int count)
        {
            if (count < 10) return 0;
            int n = Math.Min(count, 30);
            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            for (int i = 0; i < n; i++)
            {
                double x = i;
                double y = data[(count - n + i) % FORECAST_HISTORY_SIZE2];
                sumX += x; sumY += y; sumXY += x * y; sumX2 += x * x;
            }
            double denom = n * sumX2 - sumX * sumX;
            if (Math.Abs(denom) < 0.001) return 0;
            return (n * sumXY - sumX * sumY) / denom;
        }

        private List<ResourcePrediction> GenerateResourcePredictions2()
        {
            List<ResourcePrediction> predictions = new List<ResourcePrediction>();
            string[] resources = new[] { "CPU", "RAM" };
            for (int i = 0; i < resources.Length; i++)
            {
                ResourceForecastAdvanced f = GenerateSingleForecast2(resources[i]);
                if (f != null)
                {
                    predictions.Add(new ResourcePrediction
                    {
                        Resource = f.Resource,
                        Prediction = f.Forecast5Min,
                        Trend = f.Trend,
                        Confidence = f.Confidence,
                        TimeHorizon = "5min"
                    });
                }
            }
            return predictions;
        }

        private List<ForecastAlert> CheckForecastAlerts2()
        {
            List<ForecastAlert> alerts = new List<ForecastAlert>();
            ResourceForecastAdvanced cpuF = GenerateSingleForecast2("CPU");
            ResourceForecastAdvanced ramF = GenerateSingleForecast2("RAM");

            if (cpuF != null && cpuF.Trend > 3)
                alerts.Add(new ForecastAlert { Resource = "CPU", Message = "CPU usage rising rapidly (+" + cpuF.Trend.ToString("F1") + "/sample)", Severity = AlertSeverity.High, Trend = cpuF.Trend });
            else if (cpuF != null && cpuF.Trend > 1.5)
                alerts.Add(new ForecastAlert { Resource = "CPU", Message = "CPU trending upward (+" + cpuF.Trend.ToString("F1") + "/sample)", Severity = AlertSeverity.Medium, Trend = cpuF.Trend });

            if (ramF != null && ramF.Trend > 3)
                alerts.Add(new ForecastAlert { Resource = "RAM", Message = "RAM usage rising rapidly (+" + ramF.Trend.ToString("F1") + "/sample)", Severity = AlertSeverity.High, Trend = ramF.Trend });
            else if (ramF != null && ramF.Trend > 1.5)
                alerts.Add(new ForecastAlert { Resource = "RAM", Message = "RAM trending upward (+" + ramF.Trend.ToString("F1") + "/sample)", Severity = AlertSeverity.Medium, Trend = ramF.Trend });

            return alerts;
        }

        public string GetResourceForecastSummary()
        {
            ResourceForecastResult result = UpdateResourceForecasting();
            if (result.Forecasts.Count == 0)
                return "Collecting data...";

            string summary = "Forecasts: ";
            for (int i = 0; i < result.Forecasts.Count; i++)
            {
                ResourceForecastAdvanced f = result.Forecasts[i];
                summary += string.Format("{0}:{1:F0}%→{2:F0}%(5m) ",
                    f.Resource, f.CurrentValue, f.Forecast5Min);
            }
            return summary;
        }
    }

    public class ResourceForecastAdvanced
    {
        public string Resource;
        public double CurrentValue;
        public double Mean;
        public double StdDev;
        public double Trend;
        public double Forecast5Min;
        public double Forecast15Min;
        public double Forecast30Min;
        public double Confidence;
        public DateTime Timestamp;
    }

    public class ResourceForecastResult
    {
        public List<ResourceForecastAdvanced> Forecasts = new List<ResourceForecastAdvanced>();
        public List<ResourcePrediction> Predictions = new List<ResourcePrediction>();
        public List<ForecastAlert> Alerts = new List<ForecastAlert>();
    }

    public class ResourcePrediction
    {
        public string Resource;
        public double Prediction;
        public double Trend;
        public double Confidence;
        public string TimeHorizon;
    }

    public class ForecastAlert
    {
        public string Resource;
        public string Message;
        public AlertSeverity Severity;
        public double Trend;
    }
}
