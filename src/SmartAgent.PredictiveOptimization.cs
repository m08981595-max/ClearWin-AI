using System;
using System.Collections.Generic;
using System.Text;

namespace ClearWin
{
    public class PredictionEvent
    {
        public string Type;
        public string Description;
        public double Confidence;
        public int MinutesUntil;
        public string RecommendedAction;
        public double Severity;
        public DateTime Created;
        public bool Acknowledged;
    }

    public class ResourceForecast
    {
        public string Resource;
        public double CurrentValue;
        public double Predicted5Min;
        public double Predicted15Min;
        public double Predicted60Min;
        public double TrendPerMinute;
        public string Direction;
    }

    public partial class SmartAgent
    {
        private List<PredictionEvent> _predictionEvents = new List<PredictionEvent>();
        private List<ResourceForecast> _resourceForecasts = new List<ResourceForecast>();
        private DateTime _lastPredictionUpdate = DateTime.MinValue;
        private const int PREDICTION_INTERVAL_SECONDS = 30;
        private Queue<double> _cpuTrendWindow = new Queue<double>();
        private Queue<double> _ramTrendWindow = new Queue<double>();
        private Queue<double> _tempTrendWindow = new Queue<double>();
        private Queue<double> _diskTrendWindow = new Queue<double>();
        private const int TREND_WINDOW_SIZE = 20;

        public List<PredictionEvent> GetPredictionEvents() { return new List<PredictionEvent>(_predictionEvents); }
        public List<ResourceForecast> GetForecasts() { return new List<ResourceForecast>(_resourceForecasts); }

        public void UpdatePredictiveOptimization()
        {
            if ((DateTime.Now - _lastPredictionUpdate).TotalSeconds < PREDICTION_INTERVAL_SECONDS) return;
            _lastPredictionUpdate = DateTime.Now;

            UpdateTrendWindows();
            ComputeForecasts();
            GeneratePredictions();
            RemoveStalePredictions();
        }

        private void UpdateTrendWindows()
        {
            _cpuTrendWindow.Enqueue(_currentState.CpuUsage);
            if (_cpuTrendWindow.Count > TREND_WINDOW_SIZE) _cpuTrendWindow.Dequeue();
            _ramTrendWindow.Enqueue(_currentState.RamUsage);
            if (_ramTrendWindow.Count > TREND_WINDOW_SIZE) _ramTrendWindow.Dequeue();
            _tempTrendWindow.Enqueue(_currentState.CpuTemp);
            if (_tempTrendWindow.Count > TREND_WINDOW_SIZE) _tempTrendWindow.Dequeue();
            _diskTrendWindow.Enqueue(_currentState.DiskUsage);
            if (_diskTrendWindow.Count > TREND_WINDOW_SIZE) _diskTrendWindow.Dequeue();
        }

        private double ComputeTrend(Queue<double> window)
        {
            if (window.Count < 3) return 0;
            double[] arr = window.ToArray();
            int n = arr.Length;
            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            for (int i = 0; i < n; i++)
            {
                sumX += i;
                sumY += arr[i];
                sumXY += i * arr[i];
                sumX2 += i * i;
            }
            double denom = n * sumX2 - sumX * sumX;
            if (Math.Abs(denom) < 0.001) return 0;
            return (n * sumXY - sumX * sumY) / denom;
        }

        private double Extrapolate(Queue<double> window, int minutesAhead, double trendPerSample)
        {
            if (window.Count == 0) return 50;
            double current = 0;
            double[] arr = window.ToArray();
            for (int i = 0; i < arr.Length; i++) current += arr[i];
            current /= arr.Length;
            double trendPerMinute = trendPerSample * (60.0 / PREDICTION_INTERVAL_SECONDS);
            double predicted = current + trendPerMinute * minutesAhead;
            return Math.Max(0, Math.Min(100, predicted));
        }

        private void ComputeForecasts()
        {
            _resourceForecasts.Clear();
            double cpuTrend = ComputeTrend(_cpuTrendWindow);
            double ramTrend = ComputeTrend(_ramTrendWindow);
            double tempTrend = ComputeTrend(_tempTrendWindow);
            double diskTrend = ComputeTrend(_diskTrendWindow);

            _resourceForecasts.Add(new ResourceForecast
            {
                Resource = "CPU",
                CurrentValue = _smoothCpu,
                Predicted5Min = Extrapolate(_cpuTrendWindow, 5, cpuTrend),
                Predicted15Min = Extrapolate(_cpuTrendWindow, 15, cpuTrend),
                Predicted60Min = Extrapolate(_cpuTrendWindow, 60, cpuTrend),
                TrendPerMinute = cpuTrend,
                Direction = cpuTrend > 0.5 ? "Rising" : cpuTrend < -0.5 ? "Falling" : "Stable"
            });
            _resourceForecasts.Add(new ResourceForecast
            {
                Resource = "RAM",
                CurrentValue = _smoothRam,
                Predicted5Min = Extrapolate(_ramTrendWindow, 5, ramTrend),
                Predicted15Min = Extrapolate(_ramTrendWindow, 15, ramTrend),
                Predicted60Min = Extrapolate(_ramTrendWindow, 60, ramTrend),
                TrendPerMinute = ramTrend,
                Direction = ramTrend > 0.3 ? "Rising" : ramTrend < -0.3 ? "Falling" : "Stable"
            });
            _resourceForecasts.Add(new ResourceForecast
            {
                Resource = "Temperature",
                CurrentValue = _smoothTemp,
                Predicted5Min = Extrapolate(_tempTrendWindow, 5, tempTrend),
                Predicted15Min = Extrapolate(_tempTrendWindow, 15, tempTrend),
                Predicted60Min = Extrapolate(_tempTrendWindow, 60, tempTrend),
                TrendPerMinute = tempTrend,
                Direction = tempTrend > 0.2 ? "Rising" : tempTrend < -0.2 ? "Falling" : "Stable"
            });
            _resourceForecasts.Add(new ResourceForecast
            {
                Resource = "Disk",
                CurrentValue = _currentState.DiskUsage,
                Predicted5Min = Extrapolate(_diskTrendWindow, 5, diskTrend),
                Predicted15Min = Extrapolate(_diskTrendWindow, 15, diskTrend),
                Predicted60Min = Extrapolate(_diskTrendWindow, 60, diskTrend),
                TrendPerMinute = diskTrend,
                Direction = diskTrend > 0.1 ? "Rising" : diskTrend < -0.1 ? "Falling" : "Stable"
            });
        }

        private void GeneratePredictions()
        {
            foreach (var fc in _resourceForecasts)
            {
                if (fc.Resource == "Temperature")
                {
                    if (fc.Predicted5Min > 90)
                        AddPrediction("Thermal Warning", string.Format("CPU temp predicted to reach {0:F0}C in 5 minutes", fc.Predicted5Min), 0.85, 5, "ThermalProtect", 0.9);
                    else if (fc.Predicted15Min > 85)
                        AddPrediction("Thermal Rising", string.Format("CPU temp trending toward {0:F0}C in 15 minutes", fc.Predicted15Min), 0.75, 15, "ReduceLoad", 0.6);
                }
                else if (fc.Resource == "RAM")
                {
                    if (fc.Predicted5Min > 90)
                        AddPrediction("RAM Pressure", string.Format("RAM predicted to hit {0:F0}% in 5 minutes", fc.Predicted5Min), 0.8, 5, "PurgeStandby", 0.85);
                    else if (fc.Predicted15Min > 85)
                        AddPrediction("RAM Trending High", string.Format("RAM trending toward {0:F0}% in 15 minutes", fc.Predicted15Min), 0.7, 15, "BackgroundControl", 0.5);
                }
                else if (fc.Resource == "CPU")
                {
                    if (fc.Predicted5Min > 95)
                        AddPrediction("CPU Spike Predicted", string.Format("CPU predicted to hit {0:F0}% in 5 minutes", fc.Predicted5Min), 0.75, 5, "ThrottleHeavy", 0.7);
                }
                else if (fc.Resource == "Disk")
                {
                    if (fc.Predicted60Min > 95)
                        AddPrediction("Disk Full Warning", string.Format("Disk predicted to reach {0:F0}% in 60 minutes", fc.Predicted60Min), 0.7, 60, "CleanDisk", 0.6);
                }
            }

            if (_userProfile != null && _userProfile.SampleCount > 50)
            {
                int nextHour = (DateTime.Now.Hour + 1) % 24;
                double predictedLoad = GetPredictedLoad(nextHour);
                if (predictedLoad > 75)
                    AddPrediction("Scheduled Load", string.Format("User typically generates {0:F0}% load at {1}:00", predictedLoad, nextHour), 0.65, 60, "PreOptimize", 0.4);
            }
        }

        private void AddPrediction(string type, string desc, double confidence, int minutes, string action, double severity)
        {
            foreach (var p in _predictionEvents)
            {
                if (p.Type == type && !p.Acknowledged && (DateTime.Now - p.Created).TotalMinutes < 30) return;
            }
            _predictionEvents.Add(new PredictionEvent
            {
                Type = type,
                Description = desc,
                Confidence = confidence,
                MinutesUntil = minutes,
                RecommendedAction = action,
                Severity = severity,
                Created = DateTime.Now
            });
            if (_predictionEvents.Count > 20) _predictionEvents.RemoveAt(0);
        }

        private void RemoveStalePredictions()
        {
            _predictionEvents.RemoveAll(p => (DateTime.Now - p.Created).TotalMinutes > 120);
        }
    }
}
