using System;
using System.Collections.Generic;
using System.Linq;

namespace ClearWin
{
    public partial class SmartAgent
    {
        // ===================== PREDICTIVE ANALYTICS ENGINE =====================
        private Dictionary<string, SystemTrend> _trends = new Dictionary<string, SystemTrend>();
        private List<PredictionResult> _activePredictions = new List<PredictionResult>();
        private List<PredictionResult> _predictionHistory = new List<PredictionResult>();
        private DateTime _lastPredictionTime = DateTime.MinValue;
        private const int PREDICTION_INTERVAL_TICKS = 3;
        private const int TREND_MAX_SAMPLES = 60;
        private const double ANOMALY_Z_THRESHOLD = 2.5;

        private void InitializePredictions()
        {
            _trends["cpu"] = new SystemTrend { MetricName = "CPU Usage" };
            _trends["ram"] = new SystemTrend { MetricName = "RAM Usage" };
            _trends["temp"] = new SystemTrend { MetricName = "CPU Temperature" };
            _trends["gpu"] = new SystemTrend { MetricName = "GPU Usage" };
            _trends["fps"] = new SystemTrend { MetricName = "Frame Rate" };
            _trends["disk"] = new SystemTrend { MetricName = "Disk Usage" };
            _trends["net"] = new SystemTrend { MetricName = "Network Usage" };
        }

        private void UpdatePredictiveAnalytics()
        {
            if (_tickCount % PREDICTION_INTERVAL_TICKS != 0) return;

            lock (_stateLock)
            {
                if (_currentState == null) return;

                UpdateTrend("cpu", _currentState.CpuUsage);
                UpdateTrend("ram", _currentState.RamUsage);
                UpdateTrend("temp", _currentState.CpuTemp);
                UpdateTrend("gpu", _currentState.GpuUsage);
                UpdateTrend("disk", _currentState.DiskUsage);
                if (_currentState.Fps > 0) UpdateTrend("fps", _currentState.Fps);
            }

            _activePredictions.Clear();

            PredictRamShortage();
            PredictTemperature();
            PredictFpsDrop();
            PredictDiskFull();
            PredictBatteryDrain();
            DetectAnomalies();

            foreach (var pred in _activePredictions)
            {
                _predictionHistory.Add(pred);
                if (_predictionHistory.Count > 100) _predictionHistory.RemoveAt(0);
            }

            _lastPredictionTime = DateTime.Now;
        }

        private void UpdateTrend(string key, double value)
        {
            if (!_trends.ContainsKey(key)) _trends[key] = new SystemTrend { MetricName = key };
            var trend = _trends[key];
            trend.AddSample(value, TREND_MAX_SAMPLES);
            trend.ComputeLinearRegression();
            trend.DetectAnomaly(value, ANOMALY_Z_THRESHOLD);
            trend.LastUpdated = DateTime.Now;
        }

        private void PredictRamShortage()
        {
            if (!_trends.ContainsKey("ram")) return;
            var trend = _trends["ram"];
            if (trend.History.Count < 10) return;

            double currentRam = trend.History[trend.History.Count - 1];
            double predicted = trend.PredictNext(10);

            if (currentRam < 60 && predicted < 70) return;

            var pred = new PredictionResult
            {
                Type = PredictionType.RamShortage,
                MetricName = "RAM Usage",
                CurrentValue = currentRam,
                PredictedValue = Math.Min(100, predicted),
                Confidence = Math.Min(1.0, trend.RSquared * 1.2),
                Timeframe = "within 5 minutes"
            };

            if (currentRam > 85)
            {
                pred.ActionRequired = true;
                pred.Reason = string.Format("RAM critically high at {0:F0}%", currentRam);
            }
            else if (predicted > 85 && trend.RSquared > 0.4)
            {
                pred.ActionRequired = true;
                pred.Reason = string.Format("RAM trending toward {0:F0}% (currently {1:F0}%)", predicted, currentRam);
            }
            else
            {
                pred.Reason = string.Format("RAM at {0:F0}%, trending upward", currentRam);
            }

            _activePredictions.Add(pred);
        }

        private void PredictTemperature()
        {
            if (!_trends.ContainsKey("temp")) return;
            var trend = _trends["temp"];
            if (trend.History.Count < 8) return;

            double currentTemp = trend.History[trend.History.Count - 1];
            double predicted = trend.PredictNext(5);
            double rateOfChange = _tempRateOfChange;

            var pred = new PredictionResult
            {
                Type = PredictionType.Temperature,
                MetricName = "CPU Temperature",
                CurrentValue = currentTemp,
                PredictedValue = Math.Round(predicted, 1),
                Confidence = Math.Min(1.0, trend.RSquared),
                Timeframe = "within 2 minutes"
            };

            if (currentTemp > _deviceProfile.ThermalThrottleTemp)
            {
                pred.ActionRequired = true;
                pred.Reason = string.Format("Temperature CRITICAL at {0:F0}C (limit: {1:F0}C)", currentTemp, _deviceProfile.ThermalThrottleTemp);
            }
            else if (currentTemp > _deviceProfile.ThermalWarningTemp)
            {
                pred.ActionRequired = true;
                pred.Reason = string.Format("Temperature elevated at {0:F0}C, rising at {1:F1}C/tick", currentTemp, rateOfChange);
            }
            else if (predicted > _deviceProfile.ThermalThrottleTemp && trend.RSquared > 0.3)
            {
                pred.ActionRequired = true;
                pred.Reason = string.Format("Temperature projected to reach {0:F0}C (currently {1:F0}C)", predicted, currentTemp);
            }
            else
            {
                pred.Reason = string.Format("Temperature stable at {0:F0}C", currentTemp);
            }

            _activePredictions.Add(pred);
        }

        private void PredictFpsDrop()
        {
            if (!_trends.ContainsKey("fps")) return;
            var trend = _trends["fps"];
            if (trend.History.Count < 6) return;

            double currentFps = trend.History[trend.History.Count - 1];
            double predicted = trend.PredictNext(3);

            if (currentFps < 10) return; // not gaming

            var pred = new PredictionResult
            {
                Type = PredictionType.FpsDrop,
                MetricName = "Frame Rate",
                CurrentValue = currentFps,
                PredictedValue = Math.Max(0, predicted),
                Confidence = Math.Min(1.0, trend.RSquared * 1.1),
                Timeframe = "within 1 minute"
            };

            if (currentFps < 30)
            {
                pred.ActionRequired = true;
                pred.Reason = string.Format("FPS critically low at {0:F0}", currentFps);
            }
            else if (predicted < currentFps * 0.7 && trend.RSquared > 0.3)
            {
                pred.ActionRequired = true;
                pred.Reason = string.Format("FPS declining from {0:F0} toward {1:F0}", currentFps, predicted);
            }
            else
            {
                double stability = _currentState != null ? _currentState.FpsStability : 100;
                if (stability < 60)
                {
                    pred.Reason = string.Format("FPS unstable (stability: {0:F0}%)", stability);
                    pred.ActionRequired = true;
                }
                else
                {
                    pred.Reason = string.Format("Frame rate stable at {0:F0} FPS", currentFps);
                }
            }

            _activePredictions.Add(pred);
        }

        private void PredictDiskFull()
        {
            if (!_trends.ContainsKey("disk")) return;
            var trend = _trends["disk"];
            if (trend.History.Count < 12) return;

            double currentDisk = trend.History[trend.History.Count - 1];
            double predicted = trend.PredictNext(50);

            var pred = new PredictionResult
            {
                Type = PredictionType.DiskFull,
                MetricName = "Disk Usage",
                CurrentValue = currentDisk,
                PredictedValue = Math.Min(100, predicted),
                Confidence = Math.Min(1.0, trend.RSquared * 0.8),
                Timeframe = "within 1 week"
            };

            if (currentDisk > 92)
            {
                pred.ActionRequired = true;
                pred.Reason = string.Format("Disk critically full at {0:F0}%", currentDisk);
            }
            else if (currentDisk > 80 && predicted > 90 && trend.RSquared > 0.2)
            {
                pred.ActionRequired = true;
                pred.Reason = string.Format("Disk usage trending from {0:F0}% to {1:F0}%", currentDisk, predicted);
            }
            else
            {
                pred.Reason = string.Format("Disk at {0:F0}%", currentDisk);
            }

            _activePredictions.Add(pred);
        }

        private void PredictBatteryDrain()
        {
            if (_currentState == null || _currentState.IsCharging) return;
            if (_batteryHistory.Count < 6) return;

            double currentBattery = _currentState.BatteryLevel;
            if (currentBattery < 0 || currentBattery > 100) return;

            double drainRate = 0;
            if (_batteryHistory.Count >= 2)
            {
                double first = _batteryHistory[0];
                double last = _batteryHistory[_batteryHistory.Count - 1];
                double elapsedMinutes = (_batteryTimestamps.Count >= 2) ?
                    (_batteryTimestamps[_batteryTimestamps.Count - 1] - _batteryTimestamps[0]).TotalMinutes : 1;
                if (elapsedMinutes > 0) drainRate = (first - last) / elapsedMinutes;
            }

            var pred = new PredictionResult
            {
                Type = PredictionType.BatteryDrain,
                MetricName = "Battery Level",
                CurrentValue = currentBattery,
                PredictedValue = Math.Max(0, currentBattery - drainRate * 30),
                Confidence = _batteryHistory.Count >= 10 ? 0.7 : 0.4,
                Timeframe = "within 30 minutes"
            };

            if (currentBattery < 15)
            {
                pred.ActionRequired = true;
                pred.Reason = string.Format("Battery critically low at {0:F0}%", currentBattery);
            }
            else if (drainRate > 2 && currentBattery < 40)
            {
                pred.ActionRequired = true;
                pred.Reason = string.Format("Battery draining at {0:F1}%/min from {1:F0}%", drainRate, currentBattery);
            }
            else
            {
                pred.Reason = string.Format("Battery at {0:F0}% ({1:F1}%/min drain)", currentBattery, drainRate);
            }

            _activePredictions.Add(pred);
        }

        private void DetectAnomalies()
        {
            if (_currentState == null) return;

            foreach (var kvp in _trends)
            {
                if (kvp.Value.IsAnomaly)
                {
                    var pred = new PredictionResult
                    {
                        Type = PredictionType.Temperature,
                        MetricName = kvp.Value.MetricName,
                        CurrentValue = kvp.Value.History.Count > 0 ? kvp.Value.History[kvp.Value.History.Count - 1] : 0,
                        PredictedValue = 0,
                        Confidence = Math.Min(1.0, Math.Abs(kvp.Value.AnomalyZScore) / 4.0),
                        Timeframe = "detected now",
                        ActionRequired = Math.Abs(kvp.Value.AnomalyZScore) > 3.5,
                        Reason = string.Format("Anomalous {0}: z-score={1:F1} (mean={2:F1}, stddev={3:F1})",
                            kvp.Value.MetricName, kvp.Value.AnomalyZScore, kvp.Value.CurrentMean, kvp.Value.CurrentStdDev)
                    };
                    _activePredictions.Add(pred);
                }
            }
        }

        public List<PredictionResult> GetActivePredictions()
        {
            return new List<PredictionResult>(_activePredictions);
        }

        public List<PredictionResult> GetRecentPredictions(int count)
        {
            int start = Math.Max(0, _predictionHistory.Count - count);
            return _predictionHistory.GetRange(start, _predictionHistory.Count - start);
        }

        public SystemTrend GetTrend(string metric)
        {
            SystemTrend t;
            return _trends.TryGetValue(metric, out t) ? t : null;
        }
    }
}
