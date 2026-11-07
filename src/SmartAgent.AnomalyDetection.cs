using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClearWin
{
    public class AnomalyEvent
    {
        public string Type;
        public string Resource;
        public double Value;
        public double ExpectedValue;
        public double Deviation;
        public double Severity;
        public DateTime DetectedAt;
        public string Description;
        public string RecommendedAction;
        public bool Resolved;
    }

    public class BaselineStats
    {
        public double Mean;
        public double StdDev;
        public double M2;
        public int SampleCount;
        public double Min;
        public double Max;
        public double[] HourlyMeans = new double[24];
        public int[] HourlyCounts = new int[24];
    }

    public partial class SmartAgent
    {
        private List<AnomalyEvent> _advancedAnomalies = new List<AnomalyEvent>();
        private Dictionary<string, BaselineStats> _anomalyBaselines = new Dictionary<string, BaselineStats>();
        private DateTime _lastAdvancedAnomalyCheck = DateTime.MinValue;
        private const int ADVANCED_ANOMALY_INTERVAL = 15;
        private double _advancedAnomalyThreshold = 2.5;
        private Queue<double> _advCpuAnomalyWindow = new Queue<double>();
        private Queue<double> _advRamAnomalyWindow = new Queue<double>();
        private Queue<double> _advGpuAnomalyWindow = new Queue<double>();
        private Queue<double> _advTempAnomalyWindow = new Queue<double>();
        private Queue<double> _advNetAnomalyWindow = new Queue<double>();
        private const int ADV_ANOMALY_WINDOW = 30;

        public List<AnomalyEvent> GetAdvancedAnomalies() { return new List<AnomalyEvent>(_advancedAnomalies); }
        public int GetActiveAdvancedAnomalyCount() { return _advancedAnomalies.Count(a => !a.Resolved); }

        public void RunAdvancedAnomalyDetection()
        {
            if ((DateTime.Now - _lastAdvancedAnomalyCheck).TotalSeconds < ADVANCED_ANOMALY_INTERVAL) return;
            _lastAdvancedAnomalyCheck = DateTime.Now;

            UpdateAdvAnomalyWindows();
            DetectAdvAnomaly("CPU", _currentState.CpuUsage, _advCpuAnomalyWindow);
            DetectAdvAnomaly("RAM", _currentState.RamUsage, _advRamAnomalyWindow);
            DetectAdvAnomaly("GPU", _currentState.GpuUsage, _advGpuAnomalyWindow);
            DetectAdvAnomaly("Temperature", _currentState.CpuTemp, _advTempAnomalyWindow);
            DetectAdvAnomaly("Network", _netDownloadKBps + _netUploadKBps, _advNetAnomalyWindow);
            ResolveOldAdvAnomalies();
            CleanupAdvAnomalies();
        }

        private void UpdateAdvAnomalyWindows()
        {
            _advCpuAnomalyWindow.Enqueue(_currentState.CpuUsage);
            if (_advCpuAnomalyWindow.Count > ADV_ANOMALY_WINDOW) _advCpuAnomalyWindow.Dequeue();
            _advRamAnomalyWindow.Enqueue(_currentState.RamUsage);
            if (_advRamAnomalyWindow.Count > ADV_ANOMALY_WINDOW) _advRamAnomalyWindow.Dequeue();
            _advGpuAnomalyWindow.Enqueue(_currentState.GpuUsage);
            if (_advGpuAnomalyWindow.Count > ADV_ANOMALY_WINDOW) _advGpuAnomalyWindow.Dequeue();
            _advTempAnomalyWindow.Enqueue(_currentState.CpuTemp);
            if (_advTempAnomalyWindow.Count > ADV_ANOMALY_WINDOW) _advTempAnomalyWindow.Dequeue();
            _advNetAnomalyWindow.Enqueue(_netDownloadKBps + _netUploadKBps);
            if (_advNetAnomalyWindow.Count > ADV_ANOMALY_WINDOW) _advNetAnomalyWindow.Dequeue();
        }

        private void DetectAdvAnomaly(string resource, double currentValue, Queue<double> window)
        {
            if (window.Count < 10) return;

            string blKey = "adv_" + resource + "_" + DateTime.Now.Hour;
            BaselineStats bl;
            if (!_anomalyBaselines.TryGetValue(blKey, out bl))
            {
                bl = new BaselineStats();
                _anomalyBaselines[blKey] = bl;
            }

            bl.SampleCount++;
            double delta = currentValue - bl.Mean;
            bl.Mean += delta / bl.SampleCount;
            double delta2 = currentValue - bl.Mean;
            bl.M2 += delta * delta2;
            if (bl.SampleCount > 1)
                bl.StdDev = Math.Sqrt(bl.M2 / (bl.SampleCount - 1));
            if (currentValue < bl.Min || bl.SampleCount == 1) bl.Min = currentValue;
            if (currentValue > bl.Max) bl.Max = currentValue;
            bl.HourlyMeans[DateTime.Now.Hour] = bl.Mean;
            bl.HourlyCounts[DateTime.Now.Hour]++;

            if (bl.SampleCount < 20) return;

            double deviation = bl.StdDev > 0 ? Math.Abs(currentValue - bl.Mean) / bl.StdDev : 0;

            if (deviation > _advancedAnomalyThreshold)
            {
                double severity = Math.Min(1.0, deviation / 5.0);
                string existingKey = resource + "_" + DateTime.Now.Minute;
                foreach (var a in _advancedAnomalies)
                {
                    if (a.Type == existingKey && !a.Resolved) return;
                }

                var anomaly = new AnomalyEvent
                {
                    Type = existingKey,
                    Resource = resource,
                    Value = currentValue,
                    ExpectedValue = bl.Mean,
                    Deviation = deviation,
                    Severity = severity,
                    DetectedAt = DateTime.Now,
                    Description = string.Format("{0} anomaly: {1:F1}% (expected {2:F1}%, {3:F1} sigma)",
                        resource, currentValue, bl.Mean, deviation),
                    RecommendedAction = GetAdvAnomalyAction(resource, currentValue, bl.Mean),
                    Resolved = false
                };
                _advancedAnomalies.Add(anomaly);
                AddLog("WARN", "AdvAnomalyDetector", anomaly.Description);
            }
        }

        private string GetAdvAnomalyAction(string resource, double current, double baseline)
        {
            bool isSpike = current > baseline;
            switch (resource)
            {
                case "CPU": return isSpike ? "ThrottleHeavy" : "None";
                case "RAM": return isSpike ? "PurgeStandby" : "None";
                case "GPU": return isSpike ? "ThermalProtect" : "None";
                case "Temperature": return isSpike ? "ThermalProtect" : "None";
                case "Network": return isSpike ? "InvestigateNetwork" : "None";
                default: return "None";
            }
        }

        private void ResolveOldAdvAnomalies()
        {
            foreach (var a in _advancedAnomalies)
            {
                if (!a.Resolved && (DateTime.Now - a.DetectedAt).TotalMinutes > 10)
                    a.Resolved = true;
            }
        }

        private void CleanupAdvAnomalies()
        {
            _advancedAnomalies.RemoveAll(a => (DateTime.Now - a.DetectedAt).TotalMinutes > 60);
        }

        public string GetAdvancedAnomalySummary()
        {
            int active = GetActiveAdvancedAnomalyCount();
            if (active == 0) return "No anomalies detected";

            var sb = new StringBuilder();
            sb.AppendLine(string.Format("{0} active anomal{1}", active, active == 1 ? "y" : "ies"));
            foreach (var a in _advancedAnomalies.Where(a => !a.Resolved).Take(5))
            {
                sb.AppendLine(string.Format("  [{0}] {1} (sigma={2:F1}, severity={3:F0}%)",
                    a.Resource, a.Description, a.Deviation, a.Severity * 100));
            }
            return sb.ToString();
        }
    }
}
