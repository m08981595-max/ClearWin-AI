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
        private List<AuditEntry> _auditLog = new List<AuditEntry>();
        private SystemKPIs _kpis;
        private List<PerformanceReport> _reports = new List<PerformanceReport>();
        private DateTime _lastReportTime = DateTime.MinValue;
        private const int REPORT_INTERVAL_SECONDS = 3600;
        private DateTime _lastAuditSave = DateTime.MinValue;
        private const int AUDIT_SAVE_INTERVAL = 120;
        private List<DegradationRecord> _degradationRecords = new List<DegradationRecord>();
        private int _auditEntryCount = 0;

        public List<AuditEntry> GetAuditLog() { return new List<AuditEntry>(_auditLog); }
        public SystemKPIs GetKPIs() { return _kpis; }
        public List<PerformanceReport> GetReports() { return new List<PerformanceReport>(_reports); }
        public int GetAuditEntryCount() { return _auditEntryCount; }

        public void InitializeEnterpriseManager()
        {
            _kpis = new SystemKPIs();
            _kpis.LastUpdated = DateTime.Now;
            _kpis.UptimeHours = 0;
            _kpis.AvgHealthScore = 50;
            _kpis.TotalOptimizations = 0;
            _kpis.SuccessRate = 0;
            _kpis.AvgCpuUsage = 50;
            _kpis.AvgRamUsage = 50;
            _kpis.AvgTemp = 45;
            _kpis.AvgFps = 60;
            _kpis.DegradationRate = 0;
            AddLog("ENTERPRISE", "EnterpriseManager", "Enterprise manager initialized");
        }

        public void RecordAuditEntry(string action, string category, string detail, bool success, double impact)
        {
            var entry = new AuditEntry();
            entry.Timestamp = DateTime.Now;
            entry.Action = action;
            entry.Category = category;
            entry.Detail = detail;
            entry.Success = success;
            entry.Impact = impact;
            if (_kpis == null) return;
            entry.SessionUptime = (DateTime.Now - _kpis.LastUpdated).TotalHours;
            _auditLog.Add(entry);
            _auditEntryCount++;
            if (_auditLog.Count > 1000) _auditLog.RemoveAt(0);
        }

        public void UpdateKPIs()
        {
            if (_kpis == null) return;

            _kpis.LastUpdated = DateTime.Now;
            _kpis.UptimeHours = (DateTime.Now - _kpis.StartTime).TotalHours;

            double alpha = 0.05;
            _kpis.AvgCpuUsage = _kpis.AvgCpuUsage * (1 - alpha) + _smoothCpu * alpha;
            _kpis.AvgRamUsage = _kpis.AvgRamUsage * (1 - alpha) + _smoothRam * alpha;
            _kpis.AvgTemp = _kpis.AvgTemp * (1 - alpha) + _smoothTemp * alpha;
            if (_smoothFps > 0) _kpis.AvgFps = _kpis.AvgFps * (1 - alpha) + _smoothFps * alpha;
            _kpis.AvgHealthScore = _kpis.AvgHealthScore * (1 - alpha) + SystemHealthScore * alpha;

            _kpis.TotalOptimizations = _stats.TotalDecisions;
            _kpis.SuccessRate = _stats.TotalDecisions > 0
                ? (double)_stats.SuccessfulDecisions / _stats.TotalDecisions : 0;

            if (_degradationRecords.Count > 10)
            {
                var first = _degradationRecords[0];
                var last = _degradationRecords[_degradationRecords.Count - 1];
                double hoursDiff = (last.Timestamp - first.Timestamp).TotalHours;
                if (hoursDiff > 0)
                {
                    _kpis.DegradationRate = (last.HealthScore - first.HealthScore) / hoursDiff;
                }
            }

            var degradation = new DegradationRecord();
            degradation.Timestamp = DateTime.Now;
            degradation.HealthScore = SystemHealthScore;
            degradation.CpuUsage = _smoothCpu;
            degradation.RamUsage = _smoothRam;
            degradation.Temp = _smoothTemp;
            degradation.Fps = _smoothFps;
            _degradationRecords.Add(degradation);
            if (_degradationRecords.Count > 500) _degradationRecords.RemoveAt(0);
        }

        public void GeneratePerformanceReport()
        {
            if ((DateTime.Now - _lastReportTime).TotalSeconds < REPORT_INTERVAL_SECONDS) return;

            var report = new PerformanceReport();
            report.Timestamp = DateTime.Now;
            report.ReportType = "Hourly";
            report.AvgCpu = _kpis.AvgCpuUsage;
            report.AvgRam = _kpis.AvgRamUsage;
            report.AvgTemp = _kpis.AvgTemp;
            report.AvgFps = _kpis.AvgFps;
            report.HealthScore = SystemHealthScore;
            report.TotalOptimizations = _stats.TotalDecisions;
            report.SuccessRate = _kpis.SuccessRate;
            report.UptimeHours = _kpis.UptimeHours;
            report.TopActions = GetTopActions(5);
            report.Issues = GetRecentIssues(5);

            _reports.Add(report);
            if (_reports.Count > 168) _reports.RemoveAt(0);

            _lastReportTime = DateTime.Now;
            AddLog("ENTERPRISE", "EnterpriseManager",
                string.Format("Report generated: Health={0:F0} CPU={1:F0} RAM={2:F0} Temp={3:F0}",
                report.HealthScore, report.AvgCpu, report.AvgRam, report.AvgTemp));
        }

        private List<string> GetTopActions(int count)
        {
            var actions = new Dictionary<string, int>();
            foreach (var entry in _auditLog)
            {
                int c;
                if (!actions.TryGetValue(entry.Action, out c)) c = 0;
                actions[entry.Action] = c + 1;
            }
            var sorted = new List<KeyValuePair<string, int>>(actions);
            sorted.Sort(delegate(KeyValuePair<string, int> a, KeyValuePair<string, int> b) { return b.Value.CompareTo(a.Value); });
            var result = new List<string>();
            for (int i = 0; i < Math.Min(count, sorted.Count); i++)
            {
                result.Add(sorted[i].Key + " (" + sorted[i].Value + "x)");
            }
            return result;
        }

        private List<string> GetRecentIssues(int count)
        {
            var issues = new List<string>();
            int found = 0;
            for (int i = _auditLog.Count - 1; i >= 0 && found < count; i--)
            {
                if (!_auditLog[i].Success)
                {
                    issues.Add(_auditLog[i].Action + ": " + _auditLog[i].Detail);
                    found++;
                }
            }
            return issues;
        }

        public string GetKPIsSummary()
        {
            if (_kpis == null) return "KPIs not initialized";
            return string.Format("Uptime: {0:F1}h | Health: {1:F0} | CPU: {2:F0}% | RAM: {3:F0}% | Temp: {4:F0}C | FPS: {5:F0} | Success: {6:F0}% | Degradation: {7:F2}/hr",
                _kpis.UptimeHours, _kpis.AvgHealthScore, _kpis.AvgCpuUsage, _kpis.AvgRamUsage,
                _kpis.AvgTemp, _kpis.AvgFps, _kpis.SuccessRate * 100, _kpis.DegradationRate);
        }

        private void SaveAuditLog()
        {
            if ((DateTime.Now - _lastAuditSave).TotalSeconds < AUDIT_SAVE_INTERVAL) return;
            try
            {
                string path = Path.Combine(_dataDir, "audit_log.txt");
                var sb = new StringBuilder();
                int start = Math.Max(0, _auditLog.Count - 200);
                for (int i = start; i < _auditLog.Count; i++)
                {
                    var e = _auditLog[i];
                    sb.AppendLine(string.Format("{0}|{1}|{2}|{3}|{4}|{5}",
                        e.Timestamp.Ticks, e.Action, e.Category, e.Detail, e.Success ? 1 : 0, e.Impact));
                }
                File.WriteAllText(path, sb.ToString());
                _lastAuditSave = DateTime.Now;
            }
            catch (Exception ex) { LogError("SaveAuditLog", ex); }
        }
    }
}
