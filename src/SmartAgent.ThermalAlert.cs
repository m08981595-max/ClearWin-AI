using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ClearWin
{
    public partial class SmartAgent
    {
        // === Thermal Alert Fields ===
        private DateTime _lastThermalAlertCheck = DateTime.MinValue;
        private const int THERMAL_ALERT_INTERVAL_SECONDS = 10;
        private double _thermalAlertThreshold = 75;
        private double _thermalCriticalThreshold = 90;
        private double _thermalWarningThreshold = 85;
        private bool _thermalAlertActive = false;
        private bool _thermalCoolingMode = false;
        private DateTime _lastThermalAlert = DateTime.MinValue;
        private const int THERMAL_ALERT_COOLDOWN_SECONDS = 120;
        private int _thermalAlertCount = 0;
        private double _maxTempReached = 0;
        private double _avgTempLast5Min = 0;
        private List<double> _recentTemps = new List<double>();
        private const int MAX_RECENT_TEMPS = 30; // 5 min at 10s intervals

        // Thermal state machine
        private ThermalState _thermalState = ThermalState.Normal;
        private ThermalState _prevThermalState = ThermalState.Normal;
        private DateTime _thermalStateChangeTime = DateTime.MinValue;
        private int _thermalStateTicks = 0;

        // === Public Accessors ===
        public ThermalState CurrentThermalState { get { return _thermalState; } }
        public double ThermalAlertThreshold { get { return _thermalAlertThreshold; } set { _thermalAlertThreshold = value; } }
        public double MaxTempReached { get { return _maxTempReached; } }
        public double AvgTempLast5Min { get { return _avgTempLast5Min; } }
        public bool IsThermalCooling { get { return _thermalCoolingMode; } }

        public enum ThermalState
        {
            Normal,     // < 70C
            Warm,       // 70-80C
            Hot,        // 80-85C
            Critical,   // 85-90C
            Emergency   // > 90C
        }

        public class ThermalAlert
        {
            public DateTime Timestamp;
            public ThermalState State;
            public double Temperature;
            public double RateOfChange;
            public string Message;
            public string Recommendation;
            public bool RequiresAction;
        }

        // === Thermal Alert: Analyze and generate decisions ===
        public void AnalyzeThermalAlerts(List<AgentDecision> decisions)
        {
            if ((DateTime.Now - _lastThermalAlertCheck).TotalSeconds < THERMAL_ALERT_INTERVAL_SECONDS) return;
            _lastThermalAlertCheck = DateTime.Now;

            try
            {
                // Track temperature history
                _recentTemps.Add(_smoothTemp);
                if (_recentTemps.Count > MAX_RECENT_TEMPS) _recentTemps.RemoveAt(0);

                // Calculate average
                double sum = 0;
                for (int i = 0; i < _recentTemps.Count; i++) sum += _recentTemps[i];
                _avgTempLast5Min = _recentTemps.Count > 0 ? sum / _recentTemps.Count : _smoothTemp;

                // Track max
                if (_smoothTemp > _maxTempReached) _maxTempReached = _smoothTemp;

                // Determine thermal state
                ThermalState newState = DetermineThermalState(_smoothTemp);

                // State machine transitions
                if (newState != _thermalState)
                {
                    _prevThermalState = _thermalState;
                    _thermalState = newState;
                    _thermalStateChangeTime = DateTime.Now;
                    _thermalStateTicks = 0;
                }
                _thermalStateTicks++;

                // Generate decisions based on thermal state
                switch (_thermalState)
                {
                    case ThermalState.Warm:
                        if (_thermalStateTicks == 1) // First tick in this state
                        {
                            decisions.Add(new AgentDecision
                            {
                                Category = DecisionCategory.ThermalProtect,
                                Action = "ReduceThermal",
                                Description = "System warming up - monitoring temperature",
                                Reason = string.Format("Temperature rising: {0:F1}C (avg: {1:F1}C)", _smoothTemp, _avgTempLast5Min),
                                Confidence = 0.55,
                                Safety = DecisionSafety.Safe
                            });
                        }
                        break;

                    case ThermalState.Hot:
                        if (!_thermalAlertActive || (DateTime.Now - _lastThermalAlert).TotalSeconds > THERMAL_ALERT_COOLDOWN_SECONDS)
                        {
                            decisions.Add(new AgentDecision
                            {
                                Category = DecisionCategory.ThermalProtect,
                                Action = "ReduceThermal",
                                Description = "Temperature elevated - reducing load",
                                Reason = string.Format("Hot: {0:F1}C (threshold: {1:F0}C)", _smoothTemp, _thermalAlertThreshold),
                                Confidence = 0.78,
                                Safety = DecisionSafety.Safe
                            });
                            _thermalAlertActive = true;
                            _lastThermalAlert = DateTime.Now;
                            _thermalAlertCount++;
                        }
                        break;

                    case ThermalState.Critical:
                        decisions.Add(new AgentDecision
                        {
                            Category = DecisionCategory.ThermalProtect,
                            Action = "EmergencyThrottle",
                            Description = "Critical temperature - emergency throttling",
                            Reason = string.Format("CRITICAL: {0:F1}C (limit: {1:F0}C) - Rate of change: {2:F1}C/min",
                                _smoothTemp, _thermalCriticalThreshold, _tempRateOfChange),
                            Confidence = 0.92,
                            Safety = DecisionSafety.Safe
                        });
                        _thermalCoolingMode = true;
                        break;

                    case ThermalState.Emergency:
                        decisions.Add(new AgentDecision
                        {
                            Category = DecisionCategory.ThermalProtect,
                            Action = "EmergencyThrottle",
                            Description = "EMERGENCY: Maximum cooling - throttling everything",
                            Reason = string.Format("EMERGENCY: {0:F1}C exceeds critical limit - reducing all performance immediately", _smoothTemp),
                            Confidence = 0.98,
                            Safety = DecisionSafety.Dangerous
                        });
                        _thermalCoolingMode = true;
                        break;

                    case ThermalState.Normal:
                        if (_thermalCoolingMode && _smoothTemp < 70)
                        {
                            _thermalCoolingMode = false;
                            AddLog("INFO", "Thermal", "Temperature normalized - exiting cooling mode");
                        }
                        break;
                }
            }
            catch (Exception ex) { LogError("AnalyzeThermalAlerts", ex); }
        }

        // === Determine thermal state from temperature ===
        private ThermalState DetermineThermalState(double temp)
        {
            if (temp >= _thermalCriticalThreshold + 5) return ThermalState.Emergency;
            if (temp >= _thermalCriticalThreshold) return ThermalState.Critical;
            if (temp >= _thermalWarningThreshold) return ThermalState.Hot;
            if (temp >= _thermalAlertThreshold) return ThermalState.Warm;
            return ThermalState.Normal;
        }

        // === Get thermal alert info ===
        public ThermalAlert GetLatestThermalAlert()
        {
            if (_thermalState == ThermalState.Normal) return null;

            var alert = new ThermalAlert
            {
                Timestamp = DateTime.Now,
                State = _thermalState,
                Temperature = _smoothTemp,
                RateOfChange = _tempRateOfChange,
                RequiresAction = _thermalState >= ThermalState.Hot
            };

            switch (_thermalState)
            {
                case ThermalState.Warm:
                    alert.Message = string.Format("Temperature warm: {0:F1}C", _smoothTemp);
                    alert.Recommendation = "Monitoring temperature - no action needed yet";
                    break;
                case ThermalState.Hot:
                    alert.Message = string.Format("Temperature hot: {0:F1}C", _smoothTemp);
                    alert.Recommendation = "Reducing system load to prevent overheating";
                    break;
                case ThermalState.Critical:
                    alert.Message = string.Format("CRITICAL: {0:F1}C - Throttling active", _smoothTemp);
                    alert.Recommendation = "Emergency throttling - close unnecessary applications";
                    break;
                case ThermalState.Emergency:
                    alert.Message = string.Format("EMERGENCY: {0:F1}C - Maximum cooling", _smoothTemp);
                    alert.Recommendation = "Stop intensive tasks immediately to prevent hardware damage";
                    break;
            }

            return alert;
        }

        // === Get thermal stats for UI ===
        public string GetThermalStatsArabic()
        {
            string stateArabic;
            switch (_thermalState)
            {
                case ThermalState.Normal: stateArabic = "طبيعي"; break;
                case ThermalState.Warm: stateArabic = "دافئ"; break;
                case ThermalState.Hot: stateArabic = "ساخن"; break;
                case ThermalState.Critical: stateArabic = "حرج"; break;
                case ThermalState.Emergency: stateArabic = "طوارئ"; break;
                default: stateArabic = "غير معروف"; break;
            }
            return string.Format("الحالة: {0} | الحالي: {1:F1}C | المتوسط: {2:F1}C | الأقصى: {3:F1}C | التنبيهات: {4}",
                stateArabic, _smoothTemp, _avgTempLast5Min, _maxTempReached, _thermalAlertCount);
        }

        // === Reset thermal stats ===
        public void ResetThermalStats()
        {
            _maxTempReached = 0;
            _thermalAlertCount = 0;
            _recentTemps.Clear();
            _thermalAlertActive = false;
            _thermalCoolingMode = false;
            _thermalState = ThermalState.Normal;
            AddLog("INFO", "Thermal", "Thermal stats reset");
        }
    }
}
