using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ClearWin
{
    // === PREDICTIVE MAINTENANCE ===
    // Predicts when system needs maintenance based on degradation trends
    // Detects early warning signs before problems occur
    public partial class SmartAgent
    {
        private List<MaintenanceRecord> _maintenanceHistory = new List<MaintenanceRecord>();
        private Dictionary<string, DegradationTracker> _degradationTrackers = new Dictionary<string, DegradationTracker>();
        private DateTime _lastMaintenanceCheck = DateTime.MinValue;
        private const int MAINTENANCE_CHECK_INTERVAL = 300;
        private const int MAX_MAINTENANCE_HISTORY = 200;

        public void InitializePredictiveMaintenance()
        {
            _maintenanceHistory.Clear();
            _degradationTrackers.Clear();
            _degradationTrackers["CPU"] = new DegradationTracker("CPU", 0.85);
            _degradationTrackers["RAM"] = new DegradationTracker("RAM", 0.80);
            _degradationTrackers["Disk"] = new DegradationTracker("Disk", 0.90);
            _degradationTrackers["GPU"] = new DegradationTracker("GPU", 0.85);
            _degradationTrackers["Thermal"] = new DegradationTracker("Thermal", 0.75);
        }

        public List<MaintenanceAlert> CheckPredictiveMaintenance()
        {
            if ((DateTime.Now - _lastMaintenanceCheck).TotalSeconds < MAINTENANCE_CHECK_INTERVAL)
                return new List<MaintenanceAlert>();
            _lastMaintenanceCheck = DateTime.Now;

            List<MaintenanceAlert> alerts = new List<MaintenanceAlert>();

            try
            {
                // Update trackers with current data
                UpdateDegradationTrackers();

                // Check each tracker for warnings
                foreach (var tracker in _degradationTrackers.Values)
                {
                    MaintenanceAlert alert = tracker.CheckDegradation();
                    if (alert != null)
                    {
                        alerts.Add(alert);
                        LogMaintenanceAlert(alert);
                    }
                }

                // Check disk health
                DiskHealthInfo diskInfo = CheckDiskHealth();
                if (diskInfo != null && diskInfo.RequiresMaintenance)
                    alerts.Add(diskInfo.ToAlert());

                // Check memory health
                MemoryHealthInfo memInfo = CheckMemoryHealth();
                if (memInfo != null && memInfo.RequiresMaintenance)
                    alerts.Add(memInfo.ToAlert());

                // Check thermal health
                ThermalHealthInfo thermalInfo = CheckThermalHealth();
                if (thermalInfo != null && thermalInfo.RequiresMaintenance)
                    alerts.Add(thermalInfo.ToAlert());

                // Record maintenance check
                MaintenanceRecord record = new MaintenanceRecord
                {
                    Timestamp = DateTime.Now,
                    Alerts = alerts,
                    SystemHealth = CalculateOverallHealth()
                };
                _maintenanceHistory.Add(record);
                if (_maintenanceHistory.Count > MAX_MAINTENANCE_HISTORY)
                    _maintenanceHistory.RemoveAt(0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[PredictiveMaintenance] Error: " + ex.Message);
            }

            return alerts;
        }

        private void UpdateDegradationTrackers()
        {
            try
            {
                if (_degradationTrackers.ContainsKey("CPU"))
                    _degradationTrackers["CPU"].AddDataPoint(GetCurrentCpuUsage());

                if (_degradationTrackers.ContainsKey("RAM"))
                    _degradationTrackers["RAM"].AddDataPoint(GetCurrentRamUsage());

                if (_degradationTrackers.ContainsKey("Disk"))
                    _degradationTrackers["Disk"].AddDataPoint(GetCurrentDiskUsage());

                if (_degradationTrackers.ContainsKey("GPU"))
                    _degradationTrackers["GPU"].AddDataPoint(GetCurrentGpuUsage());

                if (_degradationTrackers.ContainsKey("Thermal"))
                    _degradationTrackers["Thermal"].AddDataPoint(GetCurrentCpuTemp());
            }
            catch (Exception ex) { LogError("UpdateDegradationTrackers", ex); }
        }

        private DiskHealthInfo CheckDiskHealth()
        {
            try
            {
                var drive = new System.IO.DriveInfo("C:");
                double freePercent = (double)drive.AvailableFreeSpace / drive.TotalSize;
                double usedPercent = 1.0 - freePercent;

                DiskHealthInfo info = new DiskHealthInfo
                {
                    UsedPercent = usedPercent,
                    FreeSpaceGB = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024),
                    TotalSpaceGB = drive.TotalSize / (1024.0 * 1024 * 1024)
                };

                if (usedPercent > 0.95) info.Severity = AlertSeverity.Critical;
                else if (usedPercent > 0.90) info.Severity = AlertSeverity.High;
                else if (usedPercent > 0.85) info.Severity = AlertSeverity.Medium;
                else info.Severity = AlertSeverity.Low;

                info.RequiresMaintenance = info.Severity >= AlertSeverity.Medium;
                return info;
            }
            catch { return null; }
        }

        private MemoryHealthInfo CheckMemoryHealth()
        {
            try
            {
                double totalMB = GetTotalPhysicalMemoryMB();
                if (totalMB <= 0) return null;
                double usedPercent = GetCurrentRamUsage() / 100.0;
                double availableMB = totalMB * (1.0 - usedPercent);

                MemoryHealthInfo info = new MemoryHealthInfo
                {
                    UsedPercent = usedPercent,
                    AvailableMB = availableMB,
                    TotalMB = totalMB
                };

                if (usedPercent > 0.95) info.Severity = AlertSeverity.Critical;
                else if (usedPercent > 0.90) info.Severity = AlertSeverity.High;
                else if (usedPercent > 0.85) info.Severity = AlertSeverity.Medium;
                else info.Severity = AlertSeverity.Low;

                info.RequiresMaintenance = info.Severity >= AlertSeverity.Medium;
                return info;
            }
            catch { return null; }
        }

        private ThermalHealthInfo CheckThermalHealth()
        {
            try
            {
                double temp = GetCurrentCpuTemp();
                ThermalHealthInfo info = new ThermalHealthInfo { Temperature = temp };

                if (temp > 90) info.Severity = AlertSeverity.Critical;
                else if (temp > 85) info.Severity = AlertSeverity.High;
                else if (temp > 80) info.Severity = AlertSeverity.Medium;
                else info.Severity = AlertSeverity.Low;

                info.RequiresMaintenance = info.Severity >= AlertSeverity.Medium;
                return info;
            }
            catch { return null; }
        }

        private double CalculateOverallHealth()
        {
            double cpuHealth = 1.0 - (_degradationTrackers.ContainsKey("CPU") ? _degradationTrackers["CPU"].GetDegradationRate() : 0);
            double ramHealth = 1.0 - (_degradationTrackers.ContainsKey("RAM") ? _degradationTrackers["RAM"].GetDegradationRate() : 0);
            double diskHealth = 1.0 - (_degradationTrackers.ContainsKey("Disk") ? _degradationTrackers["Disk"].GetDegradationRate() : 0);
            double gpuHealth = 1.0 - (_degradationTrackers.ContainsKey("GPU") ? _degradationTrackers["GPU"].GetDegradationRate() : 0);
            double thermalHealth = 1.0 - (_degradationTrackers.ContainsKey("Thermal") ? _degradationTrackers["Thermal"].GetDegradationRate() : 0);

            return (cpuHealth + ramHealth + diskHealth + gpuHealth + thermalHealth) / 5.0;
        }

        private void LogMaintenanceAlert(MaintenanceAlert alert)
        {
            Debug.WriteLine("[PredictiveMaintenance] Alert: " + alert.Category + " - " + alert.Message + " (Severity: " + alert.Severity + ")");
        }

        public double GetPredictiveMaintenanceScore()
        {
            return CalculateOverallHealth();
        }

        public string GetPredictiveMaintenanceSummary()
        {
            List<MaintenanceAlert> alerts = CheckPredictiveMaintenance();
            if (alerts.Count == 0)
                return "System healthy - no maintenance needed";

            int critical = 0, high = 0, medium = 0;
            foreach (var alert in alerts)
            {
                if (alert.Severity == AlertSeverity.Critical) critical++;
                else if (alert.Severity == AlertSeverity.High) high++;
                else if (alert.Severity == AlertSeverity.Medium) medium++;
            }

            return string.Format("{0} alerts: {1} critical, {2} high, {3} medium",
                alerts.Count, critical, high, medium);
        }
    }

    public class MaintenanceRecord
    {
        public DateTime Timestamp;
        public List<MaintenanceAlert> Alerts;
        public double SystemHealth;
    }

    public class MaintenanceAlert
    {
        public string Category;
        public string Message;
        public AlertSeverity Severity;
        public DateTime DetectedAt;
        public bool RequiresAction;
    }

    public class DegradationTracker
    {
        private string _category;
        private double _warningThreshold;
        private List<double> _dataPoints = new List<double>();
        private const int MAX_DATA_POINTS = 100;

        public DegradationTracker(string category, double warningThreshold)
        {
            _category = category;
            _warningThreshold = warningThreshold;
        }

        public void AddDataPoint(double value)
        {
            _dataPoints.Add(value);
            if (_dataPoints.Count > MAX_DATA_POINTS)
                _dataPoints.RemoveAt(0);
        }

        public double GetDegradationRate()
        {
            if (_dataPoints.Count < 10) return 0;

            double firstAvg = 0, lastAvg = 0;
            int half = _dataPoints.Count / 2;
            for (int i = 0; i < half; i++) firstAvg += _dataPoints[i];
            for (int i = half; i < _dataPoints.Count; i++) lastAvg += _dataPoints[i];
            firstAvg /= half;
            lastAvg /= (_dataPoints.Count - half);

            return Math.Max(0, lastAvg - firstAvg);
        }

        public MaintenanceAlert CheckDegradation()
        {
            if (_dataPoints.Count < 20) return null;

            double degradation = GetDegradationRate();
            double currentAvg = 0;
            for (int i = _dataPoints.Count - 10; i < _dataPoints.Count; i++)
                currentAvg += _dataPoints[i];
            currentAvg /= 10;

            AlertSeverity severity = AlertSeverity.Low;
            string message = "";

            if (degradation > 0.3 && currentAvg > _warningThreshold)
            {
                severity = AlertSeverity.Critical;
                message = string.Format("{0} degrading rapidly ({1:F2}/sample), current avg: {2:F1}%",
                    _category, degradation, currentAvg * 100);
            }
            else if (degradation > 0.15 && currentAvg > _warningThreshold * 0.9)
            {
                severity = AlertSeverity.High;
                message = string.Format("{0} showing degradation ({1:F2}/sample)", _category, degradation);
            }
            else if (degradation > 0.05 && currentAvg > _warningThreshold * 0.8)
            {
                severity = AlertSeverity.Medium;
                message = string.Format("{0} minor degradation detected ({1:F2}/sample)", _category, degradation);
            }

            if (severity >= AlertSeverity.Medium)
            {
                return new MaintenanceAlert
                {
                    Category = _category,
                    Message = message,
                    Severity = severity,
                    DetectedAt = DateTime.Now,
                    RequiresAction = severity >= AlertSeverity.High
                };
            }

            return null;
        }
    }

    public enum AlertSeverity { Low, Medium, High, Critical }

    public class DiskHealthInfo
    {
        public double UsedPercent;
        public double FreeSpaceGB;
        public double TotalSpaceGB;
        public AlertSeverity Severity;
        public bool RequiresMaintenance;
        public MaintenanceAlert ToAlert()
        {
            return new MaintenanceAlert
            {
                Category = "Disk",
                Message = string.Format("Disk {0:P0} used ({1:F1} GB free of {2:F1} GB)",
                    UsedPercent, FreeSpaceGB, TotalSpaceGB),
                Severity = Severity,
                DetectedAt = DateTime.Now,
                RequiresAction = Severity >= AlertSeverity.High
            };
        }
    }

    public class MemoryHealthInfo
    {
        public double UsedPercent;
        public double AvailableMB;
        public double TotalMB;
        public AlertSeverity Severity;
        public bool RequiresMaintenance;
        public MaintenanceAlert ToAlert()
        {
            return new MaintenanceAlert
            {
                Category = "Memory",
                Message = string.Format("Memory {0:P0} used ({1:F0} MB available)", UsedPercent, AvailableMB),
                Severity = Severity,
                DetectedAt = DateTime.Now,
                RequiresAction = Severity >= AlertSeverity.High
            };
        }
    }

    public class ThermalHealthInfo
    {
        public double Temperature;
        public AlertSeverity Severity;
        public bool RequiresMaintenance;
        public MaintenanceAlert ToAlert()
        {
            return new MaintenanceAlert
            {
                Category = "Thermal",
                Message = string.Format("CPU temperature {0:F1}°C", Temperature),
                Severity = Severity,
                DetectedAt = DateTime.Now,
                RequiresAction = Severity >= AlertSeverity.High
            };
        }
    }
}
