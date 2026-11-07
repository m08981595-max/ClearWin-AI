using System;
using System.Collections.Generic;
using System.Linq;

namespace ClearWin
{
    public partial class SmartAgent
    {
        private int _monitorTick = 0;
        private AgentMode _lastObservedMode = AgentMode.Off;
        private int _modeChangeCount = 0;
        private DateTime _lastModeChangeTime = DateTime.MinValue;
        private int _recentFailures = 0;
        private int _recentSuccesses = 0;
        private int _consecutiveFailures = 0;
        private readonly Queue<string> _monitorAlerts = new Queue<string>();
        private const int MAX_MONITOR_ALERTS = 50;
        private List<double> _monConfHistory = new List<double>();
        private List<double> _monRiskHistory = new List<double>();
        private const int CONFIDENCE_HISTORY_SIZE = 50;
        private readonly object _monitorLock = new object();
        private const double RAPID_MODE_CHANGE_SECONDS = 10.0;
        private const int RAPID_MODE_CHANGE_COUNT = 3;
        private const int CONSECUTIVE_FAILURE_ALERT = 3;
        private const int HIGH_RISK_THRESHOLD = 80;
        private const int LOW_CONFIDENCE_THRESHOLD = 30;
        private const int STALE_TICK_SECONDS = 30;

        public void MonitorTick(AgentDecision decision, double confidence, double riskScore)
        {
            lock (_monitorLock)
            {
                _monitorTick++;
                if (_mode != _lastObservedMode)
                {
                    _lastObservedMode = _mode;
                    _modeChangeCount++;
                    _lastModeChangeTime = DateTime.Now;
                    AddLog("INFO", "Monitor", $"Mode changed to {_mode} (change #{_modeChangeCount})");
                }
                if (decision != null)
                {
                    if (decision.Success)
                    {
                        _recentSuccesses++;
                        _consecutiveFailures = 0;
                    }
                    else if (decision.Executed && !decision.Success)
                    {
                        _recentFailures++;
                        _consecutiveFailures++;
                    }
                    _monConfHistory.Add(confidence);
                    _monRiskHistory.Add(riskScore);
                    if (_monConfHistory.Count > CONFIDENCE_HISTORY_SIZE) _monConfHistory.RemoveAt(0);
                    if (_monRiskHistory.Count > CONFIDENCE_HISTORY_SIZE) _monRiskHistory.RemoveAt(0);
                }
                // Game/Emulator detection alert
                if (_currentState != null && _currentState.IsGame && !string.IsNullOrEmpty(_currentState.ActiveProcess))
                {
                    string gameName = _currentState.ActiveProcess.ToLower();
                    bool isPubg = gameName.Contains("pubg") || gameName.Contains("battleground");
                    bool isEmulator = !isPubg && (gameName.Contains("emulator") || gameName.Contains("ldplayer")
                        || gameName.Contains("nox") || gameName.Contains("memu") || gameName.Contains("bluestack")
                        || gameName.Contains("gameloop") || gameName.Contains("tencent") || gameName.Contains("aow"));
                    if (isPubg)
                    {
                        string alert = gameName.Contains("pc") || gameName.Contains("battlegrounds")
                            ? $"لعبة مكتشفة: {_currentState.ActiveProcess}"
                            : $"لعبة مكتشفة: PUBG Mobile (محاكي GameLoop)";
                        FireMonitorAlert(alert);
                        AddLog("INFO", "Monitor", alert);
                    }
                    else if (isEmulator)
                    {
                        string alert = $"محاكي مكتشف: {_currentState.ActiveProcess}";
                        FireMonitorAlert(alert);
                        AddLog("INFO", "Monitor", alert);
                    }
                    else
                    {
                        string alert = $"لعبة مكتشفة: {_currentState.ActiveProcess}";
                        FireMonitorAlert(alert);
                        AddLog("INFO", "Monitor", alert);
                    }
                }
                DetectAnomalies(decision);
                var updateHandler = OnMonitorUpdate;
                if (updateHandler != null)
                {
                    try { updateHandler(GetMonitorSnapshot()); } catch { }
                }
            }
        }

        private void DetectAnomalies(AgentDecision decision)
        {
            if (_modeChangeCount >= RAPID_MODE_CHANGE_COUNT)
            {
                double elapsed = (DateTime.Now - _lastModeChangeTime).TotalSeconds;
                if (elapsed < RAPID_MODE_CHANGE_SECONDS)
                {
                    string alert = $"تحذير: تبديل سريع للوضع ({_modeChangeCount} مرات في {elapsed:F1} ثانية)";
                    FireMonitorAlert(alert);
                    AddLog("WARN", "Monitor", alert);
                    _modeChangeCount = 0;
                }
            }
            if (_consecutiveFailures >= CONSECUTIVE_FAILURE_ALERT)
            {
                string alert = $"تحذير: {_consecutiveFailures} قرارات فاشلة متتالية";
                FireMonitorAlert(alert);
                AddLog("WARN", "Monitor", alert);
                _consecutiveFailures = 0;
            }
            if (decision != null && decision.Risk > HIGH_RISK_THRESHOLD / 100.0 && decision.Executed)
            {
                string alert = $"خطر مرتفع: {decision.Action} (مخاطرة: {decision.Risk:P0})";
                FireMonitorAlert(alert);
                AddLog("WARN", "Monitor", alert);
            }
            if (decision != null && decision.Confidence < LOW_CONFIDENCE_THRESHOLD / 100.0 && decision.Executed)
            {
                string alert = $"ثقة منخفضة: {decision.Action} ({decision.Confidence:P0})";
                FireMonitorAlert(alert);
                AddLog("WARN", "Monitor", alert);
            }
            if (_mode != AgentMode.Off && SecondsSinceLastTick > STALE_TICK_SECONDS)
            {
                string alert = $"الوكيل متوقف: لم يحدث tick منذ {SecondsSinceLastTick:F0} ثانية";
                FireMonitorAlert(alert);
                AddLog("WARN", "Monitor", alert);
            }
        }

        private void FireMonitorAlert(string alert)
        {
            lock (_monitorLock)
            {
                _monitorAlerts.Enqueue(alert);
                if (_monitorAlerts.Count > MAX_MONITOR_ALERTS) _monitorAlerts.Dequeue();
                var handler = OnMonitorAlert;
                if (handler != null) handler(alert);
            }
        }

        public MonitorMetrics GetMonitorSnapshot()
        {
            lock (_monitorLock)
            {
                var m = new MonitorMetrics
                {
                    TotalDecisions = _tickCount,
                    ModeChanges = _modeChangeCount,
                    StaleTickCount = _mode != AgentMode.Off ? (int)SecondsSinceLastTick : 0,
                    AlertCount = _monitorAlerts.Count,
                    RecentDecisions = _recentSuccesses + _recentFailures,
                    RecentFailures = _recentFailures,
                };
                if (_currentState != null)
                {
                    m.IsGame = _currentState.IsGame;
                    m.CurrentGameName = !string.IsNullOrEmpty(_currentState.ActiveProcess)
                        ? _currentState.ActiveProcess : "";
                }
                if (_monConfHistory.Count > 0)
                {
                    double sumConf = _monConfHistory.Sum();
                    m.AvgConfidence = sumConf / _monConfHistory.Count;
                }
                if (_monRiskHistory.Count > 0)
                {
                    double sumRisk = _monRiskHistory.Sum();
                    m.AvgRiskScore = sumRisk / _monRiskHistory.Count;
                }
                double health = SystemHealthScore;
                if (health >= 75) m.HealthStatus = "صحي";
                else if (health >= 50) m.HealthStatus = "مستقر";
                else if (health >= 30) m.HealthStatus = "معتدل";
                else m.HealthStatus = "حرج";
                if (_consecutiveFailures >= CONSECUTIVE_FAILURE_ALERT)
                    m.AnomalySummary = $"{_consecutiveFailures} فشل متتالٍ";
                else if (_modeChangeCount >= RAPID_MODE_CHANGE_COUNT)
                    m.AnomalySummary = "تذبذب في الوضع";
                else if (m.StaleTickCount > STALE_TICK_SECONDS)
                    m.AnomalySummary = "الوكيل متوقف";
                else if (_mode == AgentMode.Off)
                    m.AnomalySummary = "الوكيل متوقف";
                else
                    m.AnomalySummary = "لا توجد شذوذ";
                return m;
            }
        }

        public List<string> GetMonitorAlerts()
        {
            lock (_monitorLock)
            {
                return new List<string>(_monitorAlerts);
            }
        }

        public void ClearMonitorAlerts()
        {
            lock (_monitorLock)
            {
                _monitorAlerts.Clear();
            }
        }
    }
}
