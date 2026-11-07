using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ClearWin
{
    public partial class SmartAgent
    {
        // ===================== AI AUTO RECOVERY =====================
        private List<RecoveryEvent> _recoveryHistory = new List<RecoveryEvent>();
        private DateTime _lastRecoveryCheck = DateTime.MinValue;
        private int _consecutiveDegradation = 0;
        private double _baselineHealthScore = 100;
        private DateTime _baselineTime = DateTime.MinValue;

        public void CheckAutoRecovery()
        {
            if ((DateTime.Now - _lastRecoveryCheck).TotalSeconds < 20) return;
            _lastRecoveryCheck = DateTime.Now;

            lock (_stateLock)
            {
                if (_currentState == null) return;

                // Establish baseline
                if (_baselineTime == DateTime.MinValue || (DateTime.Now - _baselineTime).TotalMinutes > 30)
                {
                    _baselineHealthScore = SystemHealthScore;
                    _baselineTime = DateTime.Now;
                }

                // Detect degradation
                double degradation = _baselineHealthScore - SystemHealthScore;
                if (degradation > 15)
                {
                    _consecutiveDegradation++;

                    if (_consecutiveDegradation >= 3)
                    {
                        // Auto recovery needed
                        var recovery = new RecoveryEvent();
                        recovery.Timestamp = DateTime.Now;
                        recovery.DegradationAmount = degradation;
                        recovery.BaselineScore = _baselineHealthScore;
                        recovery.CurrentScore = SystemHealthScore;

                        // Try recovery strategies in order
                        bool recovered = false;

                        // Central Execution Gate: auto recovery mutates the
                        // system (undo / power plan / memory clean), so it must
                        // not run while the user has stopped automatic actions.
                        if (CanExecuteAction("AutoRecovery", false))
                        {
                            // Strategy 1: Undo last optimization via journal inverse rollback
                            if (!recovered && _journal.Count > 0)
                            {
                                try
                                {
                                    var lastEntry = _journal[_journal.Count - 1];
                                    if (lastEntry != null && !lastEntry.Reverted)
                                    {
                                        PerformRollbackInverse(lastEntry);
                                        lastEntry.Reverted = true;
                                        recovered = true;
                                        recovery.Strategy = "UndoLastOptimization:" + lastEntry.Action;
                                    }
                                }
                                catch (Exception exUndo)
                                {
                                    LogError("Recovery.UndoLastOptimization", exUndo);
                                }
                            }

                            // Strategy 2: Switch to balanced power plan
                            if (!recovered)
                            {
                                string currentPlan = GetCurrentPowerPlan();
                                if (currentPlan != "balanced")
                                {
                                    ExecutePowerPlan("balanced");
                                    recovery.Strategy = "SwitchToBalanced";
                                    recovered = true;
                                }
                            }

                            // Strategy 3: Memory clean
                            if (!recovered)
                            {
                                ExecuteMemoryClean();
                                recovery.Strategy = "MemoryClean";
                                recovered = true;
                            }
                        }
                        else
                        {
                            recovery.Strategy = "BlockedBySafetyValve";
                        }

                        recovery.Success = recovered;
                        _recoveryHistory.Add(recovery);
                        if (_recoveryHistory.Count > 50) _recoveryHistory.RemoveAt(0);

                        AddLog("RECOVERY", "Auto", string.Format("Degradation {0:F0}% detected. Strategy: {1}. Result: {2}",
                            degradation, recovery.Strategy, recovery.Success ? "SUCCESS" : "FAILED"));

                        if (recovered)
                        {
                            _consecutiveDegradation = 0;
                            _baselineHealthScore = SystemHealthScore;
                            _baselineTime = DateTime.Now;
                        }
                    }
                }
                else
                {
                    _consecutiveDegradation = Math.Max(0, _consecutiveDegradation - 1);
                }
            }
        }

        public List<RecoveryEvent> GetRecoveryHistory()
        {
            lock (_stateLock) { return new List<RecoveryEvent>(_recoveryHistory); }
        }

        // ===================== AI SAFETY LAYER =====================
        private List<SafetyCheck> _safetyChecks = new List<SafetyCheck>();

        public SafetyResult PreExecutionSafetyCheck(AgentDecision decision)
        {
            var result = new SafetyResult();
            result.Allowed = true;
            result.Checks = new List<SafetyCheck>();
            result.RiskScore = 0;
            result.RequiresConfirmation = false;
            result.ShouldCreateRestorePoint = false;

            // Check 1: Kill switch
            if (_killSwitchActive)
            {
                result.Allowed = false;
                result.Checks.Add(new SafetyCheck { Name = "KillSwitch", Passed = false, Message = "Kill switch is active", Severity = "Critical" });
                result.RiskScore += 100;
                return result;
            }

            // Check 2: Automation valve
            if (!_automationEnabled && decision != null && !decision.WasAccepted)
            {
                result.Allowed = false;
                result.Checks.Add(new SafetyCheck { Name = "AutomationValve", Passed = false, Message = "Automatic actions are disabled", Severity = "High" });
                result.RiskScore += 50;
                return result;
            }

            // Check 3: Thermal safety
            if (_currentState != null && _currentState.CpuTemp > 95)
            {
                result.Checks.Add(new SafetyCheck { Name = "ThermalCritical", Passed = false, Message = "CPU temperature critical at " + _currentState.CpuTemp.ToString("F0") + "C", Severity = "Critical" });
                result.RiskScore += 80;
            }
            else if (_currentState != null && _currentState.CpuTemp > 85)
            {
                result.Checks.Add(new SafetyCheck { Name = "ThermalWarning", Passed = true, Message = "High temperature: " + _currentState.CpuTemp.ToString("F0") + "C", Severity = "Warning" });
                result.RiskScore += 30;
            }

            // Check 4: RAM safety
            if (_currentState != null && _currentState.RamUsage > 95)
            {
                result.Checks.Add(new SafetyCheck { Name = "RAMCritical", Passed = false, Message = "RAM usage critical at " + _currentState.RamUsage.ToString("F0") + "%", Severity = "Critical" });
                result.RiskScore += 60;
            }

            // Check 5: Game mode protection
            if (_currentState != null && _currentState.IsGame)
            {
                // Never kill critical game-related processes during gaming
                if (decision != null && (decision.Action == "KillProcess" || decision.Action == "AggressiveClean"))
                {
                    result.Allowed = false;
                    result.Checks.Add(new SafetyCheck { Name = "GameModeProtection", Passed = false, Message = "Cannot kill processes during active game session", Severity = "High" });
                    result.RiskScore += 70;
                }
            }

            // Check 6: Protected services
            if (decision != null && decision.Parameters != null)
            {
                foreach (var kv in decision.Parameters)
                {
                    if (kv.Key == "ServiceName" && kv.Value != null)
                    {
                        string svc = kv.Value.ToLower();
                        if (svc.Contains("defender") || svc.Contains("firewall") || svc.Contains("update"))
                        {
                            result.Allowed = false;
                            result.Checks.Add(new SafetyCheck { Name = "ProtectedService", Passed = false, Message = "Cannot modify protected service: " + kv.Value, Severity = "Critical" });
                            result.RiskScore += 90;
                            break;
                        }
                    }
                }
            }

            result.RequiresConfirmation = result.RiskScore > 40;
            result.ShouldCreateRestorePoint = decision != null && decision.RiskScore > 50;

            return result;
        }

        // ===================== AUTO ROLLBACK =====================
    }

    // ===================== MODELS =====================
    public class RecoveryEvent
    {
        public DateTime Timestamp;
        public double DegradationAmount;
        public double BaselineScore;
        public double CurrentScore;
        public string Strategy;
        public bool Success;

        public RecoveryEvent() { Timestamp = DateTime.Now; }
    }

    public class SafetyResult
    {
        public bool Allowed;
        public string Message;
        public List<SafetyCheck> Checks;
        public int RiskScore;
        public bool RequiresConfirmation;
        public bool ShouldCreateRestorePoint;

        public SafetyResult() { Checks = new List<SafetyCheck>(); }
    }

    public class SafetyCheck
    {
        public string Name;
        public bool Passed;
        public string Message;
        public string Severity;
    }
}
