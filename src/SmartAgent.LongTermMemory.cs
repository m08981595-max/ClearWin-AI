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
        private List<LongTermEntry> _longTermMemory = new List<LongTermEntry>();
        private DateTime _lastLTMaintenance = DateTime.MinValue;
        private const int LTM_INTERVAL_SECONDS = 600;
        private const int MAX_LT_ENTRIES = 2000;
        private Dictionary<string, GameOptHistory> _gameOptHistory = new Dictionary<string, GameOptHistory>();
        private List<DriverPerfRecord> _driverRecords = new List<DriverPerfRecord>();
        private List<UpdateImpactRecord> _updateRecords = new List<UpdateImpactRecord>();

        public List<LongTermEntry> GetLongTermMemory() { return new List<LongTermEntry>(_longTermMemory); }
        public List<GameOptHistory> GetGameOptHistory() { return new List<GameOptHistory>(_gameOptHistory.Values); }
        public List<DriverPerfRecord> GetDriverRecords() { return new List<DriverPerfRecord>(_driverRecords); }
        public List<UpdateImpactRecord> GetUpdateRecords() { return new List<UpdateImpactRecord>(_updateRecords); }

        public void RecordLongTermEvent(string category, string description, double impact)
        {
            var entry = new LongTermEntry();
            entry.Timestamp = DateTime.Now;
            entry.Category = category;
            entry.Description = description;
            entry.Impact = impact;
            entry.SystemState = string.Format("CPU={0:F0} RAM={1:F0} Temp={2:F0} FPS={3:F0}",
                _smoothCpu, _smoothRam, _smoothTemp, _smoothFps);

            _longTermMemory.Add(entry);
            if (_longTermMemory.Count > MAX_LT_ENTRIES) _longTermMemory.RemoveAt(0);
        }

        public void RecordGameOptimization(string gameName, string action, bool success, double fpsBefore, double fpsAfter)
        {
            GameOptHistory history;
            if (!_gameOptHistory.TryGetValue(gameName, out history))
            {
                history = new GameOptHistory();
                history.GameName = gameName;
                history.Optimizations = new List<GameOptAction>();
                _gameOptHistory[gameName] = history;
            }

            var opt = new GameOptAction();
            opt.Action = action;
            opt.Success = success;
            opt.FpsBefore = fpsBefore;
            opt.FpsAfter = fpsAfter;
            opt.Timestamp = DateTime.Now;
            opt.FpsDelta = fpsAfter - fpsBefore;

            history.Optimizations.Add(opt);
            history.TotalOptimizations++;
            if (success) history.SuccessfulOptimizations++;
            history.LastPlayed = DateTime.Now;
            history.AvgFpsBefore = history.AvgFpsBefore * 0.9 + fpsBefore * 0.1;
            history.AvgFpsAfter = history.AvgFpsAfter * 0.9 + fpsAfter * 0.1;
            history.BestAction = GetBestActionForGame(gameName);
        }

        private string GetBestActionForGame(string gameName)
        {
            GameOptHistory history;
            if (!_gameOptHistory.TryGetValue(gameName, out history)) return "";
            string bestAction = "";
            double bestDelta = 0;
            foreach (var opt in history.Optimizations)
            {
                if (opt.FpsDelta > bestDelta)
                {
                    bestDelta = opt.FpsDelta;
                    bestAction = opt.Action;
                }
            }
            return bestAction;
        }

        public void RecordDriverPerformance(string driverName, string category, double version, double perfImpact)
        {
            var record = new DriverPerfRecord();
            record.DriverName = driverName;
            record.Category = category;
            record.Version = version;
            record.PerformanceImpact = perfImpact;
            record.Timestamp = DateTime.Now;
            _driverRecords.Add(record);
            if (_driverRecords.Count > 200) _driverRecords.RemoveAt(0);
        }

        public void RecordUpdateImpact(string updateName, double perfBefore, double perfAfter)
        {
            var record = new UpdateImpactRecord();
            record.UpdateName = updateName;
            record.PerformanceBefore = perfBefore;
            record.PerformanceAfter = perfAfter;
            record.Impact = perfAfter - perfBefore;
            record.Timestamp = DateTime.Now;
            _updateRecords.Add(record);
            if (_updateRecords.Count > 100) _updateRecords.RemoveAt(0);
        }

        public void RunLongTermMaintenance()
        {
            if ((DateTime.Now - _lastLTMaintenance).TotalSeconds < LTM_INTERVAL_SECONDS) return;

            DateTime cutoff = DateTime.Now.AddMonths(-6);
            _longTermMemory.RemoveAll(e => e.Timestamp < cutoff);
            _driverRecords.RemoveAll(r => r.Timestamp < cutoff);
            _updateRecords.RemoveAll(r => r.Timestamp < cutoff);

            RecordLongTermEvent("Periodic",
                string.Format("Health={0:F0} CPU={1:F0} RAM={2:F0} Temp={3:F0}",
                SystemHealthScore, _smoothCpu, _smoothRam, _smoothTemp),
                SystemHealthScore);

            AddLog("LTM", "LongTermMemory",
                string.Format("LT entries: {0} | Games: {1} | Drivers: {2} | Updates: {3}",
                _longTermMemory.Count, _gameOptHistory.Count, _driverRecords.Count, _updateRecords.Count));

            SaveLongTermMemory();
            _lastLTMaintenance = DateTime.Now;
        }

        public string GetBestSettingsForGame(string gameName)
        {
            GameOptHistory history;
            if (!_gameOptHistory.TryGetValue(gameName, out history)) return "No data";
            return string.Format("Best: {0} | Avg FPS: {1:F0}->{2:F0} | Success: {3}/{4}",
                history.BestAction, history.AvgFpsBefore, history.AvgFpsAfter,
                history.SuccessfulOptimizations, history.TotalOptimizations);
        }

        public double GetUpdateImpactSummary()
        {
            if (_updateRecords.Count == 0) return 0;
            double totalImpact = 0;
            foreach (var r in _updateRecords) totalImpact += r.Impact;
            return totalImpact / _updateRecords.Count;
        }

        private void SaveLongTermMemory()
        {
            try
            {
                string path = Path.Combine(_dataDir, "longterm_memory.txt");
                var sb = new StringBuilder();
                foreach (var entry in _longTermMemory)
                {
                    sb.AppendLine(string.Format("{0}|{1}|{2}|{3}|{4}",
                        entry.Timestamp.Ticks, entry.Category, entry.Description, entry.Impact, entry.SystemState));
                }
                File.WriteAllText(path, sb.ToString());
            }
            catch (Exception ex) { LogError("SaveLongTermMemory", ex); }
        }

        public void LoadLongTermMemory()
        {
            try
            {
                string path = Path.Combine(_dataDir, "longterm_memory.txt");
                if (!File.Exists(path)) return;
                string[] lines = File.ReadAllLines(path);
                foreach (string line in lines)
                {
                    string[] parts = line.Split('|');
                    if (parts.Length >= 4)
                    {
                        var entry = new LongTermEntry();
                        entry.Timestamp = new DateTime(long.Parse(parts[0]));
                        entry.Category = parts[1];
                        entry.Description = parts[2];
                        entry.Impact = double.Parse(parts[3]);
                        if (parts.Length >= 5) entry.SystemState = parts[4];
                        _longTermMemory.Add(entry);
                    }
                }
                AddLog("LTM", "LongTermMemory", "Loaded " + _longTermMemory.Count + " long-term entries");
            }
            catch (Exception ex) { LogError("LoadLongTermMemory", ex); }
        }
    }
}
