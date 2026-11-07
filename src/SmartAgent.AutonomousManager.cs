using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ClearWin
{
    public partial class SmartAgent
    {
        private AutonomousState _autoState;
        private DateTime _lastAutoManage = DateTime.MinValue;
        private const int AUTO_MANAGE_INTERVAL_SECONDS = 10;
        private List<AutoAction> _autoActions = new List<AutoAction>();
        private int _autoActionsExecuted = 0;
        private Dictionary<string, AutoSubsystem> _subsystems = new Dictionary<string, AutoSubsystem>();
        // Tracks a power-plan switch made by the autonomous manager so it can be
        // restored when the trigger clears (battery recovers / AC / temp normal).
        private bool _autoPowerPlanSwitched = false;
        private string _autoPowerPlanPrev = null;

        public AutonomousState GetAutonomousState() { return _autoState; }
        public List<AutoAction> GetRecentAutoActions() { return new List<AutoAction>(_autoActions); }
        public int GetAutoActionsExecuted() { return _autoActionsExecuted; }

        public void InitializeAutonomousManager()
        {
            _autoState = new AutonomousState();
            _autoState.StartTime = DateTime.Now;
            _autoState.IsActive = true;
            _autoState.SubsystemStates = new Dictionary<string, string>();

            RegisterAutoSubsystem("Power", "Power management and optimization");
            RegisterAutoSubsystem("Thermal", "Temperature monitoring and control");
            RegisterAutoSubsystem("Memory", "RAM management and cleanup");
            RegisterAutoSubsystem("CPU", "CPU load management");
            RegisterAutoSubsystem("GPU", "Graphics optimization");
            RegisterAutoSubsystem("Disk", "Storage management");
            RegisterAutoSubsystem("Network", "Network monitoring");
            RegisterAutoSubsystem("Health", "Overall system health");
            RegisterAutoSubsystem("Processes", "Process management");

            AddLog("AUTO", "AutonomousManager", "Initialized autonomous system with " + _subsystems.Count + " subsystems");
        }

        private void RegisterAutoSubsystem(string name, string description)
        {
            var sub = new AutoSubsystem();
            sub.Name = name;
            sub.Description = description;
            sub.Status = "Active";
            sub.LastAction = DateTime.MinValue;
            sub.ActionCount = 0;
            sub.Health = 100;
            _subsystems[name] = sub;
            _autoState.SubsystemStates[name] = "Active";
        }

        public void RunAutonomousManagement()
        {
            if (_autoState == null) return;
            if (!_autoState.IsActive) return;
            // Respect explicit user pause and the emergency kill-switch: the
            // autonomous manager must not keep changing the system after the user
            // asked the agent to stop.
            if (IsPaused || _killSwitchActive) return;
            // Central Execution Gate: when the user stops automatic actions the
            // autonomous manager stops changing the system (monitoring/decisions
            // continue). Routed through CanExecuteAction so the block is
            // audited in one place instead of a silent raw flag check.
            if (!CanExecuteAction("AutonomousManagement", false)) return;
            if (_mode == AgentMode.Off) return;
            if ((DateTime.Now - _lastAutoManage).TotalSeconds < AUTO_MANAGE_INTERVAL_SECONDS) return;

            EvaluatePowerSubsystem();
            EvaluateThermalSubsystem();
            EvaluateMemorySubsystem();
            EvaluateCPUSubsystem();
            EvaluateDiskSubsystem();
            EvaluateProcessSubsystem();
            EvaluateNetworkSubsystem();

            UpdateAutonomousHealth();
            ExecuteAutoActions();

            _autoState.LastCycle = DateTime.Now;
            _autoState.TotalCycles++;
            _autoState.SystemHealth = SystemHealthScore;

            _lastAutoManage = DateTime.Now;
        }

        private void EvaluatePowerSubsystem()
        {
            var sub = _subsystems["Power"];
            if (_isOnBattery)
            {
                if (_batteryPercent < 20)
                {
                    // Never yank the power plan during an active game (would tank
                    // FPS); the thermal path still protects the machine separately.
                    if (!_currentState.IsGame)
                    {
                        QueueAutoAction("Power", "EmergencyPowerSave", "Battery critical, maximize power savings", 9);
                    }
                    sub.Health = Math.Max(20, _batteryPercent);
                }
                else if (_batteryPercent < 50)
                {
                    sub.Health = _batteryPercent;
                    RestoreAutoPowerPlan();
                }
                else
                {
                    sub.Health = 100;
                    RestoreAutoPowerPlan();
                }
            }
            else
            {
                sub.Health = 100;
                RestoreAutoPowerPlan();
            }
        }

        // Restore the power plan the autonomous manager switched away from, once
        // the trigger (low battery / high heat) has cleared.
        private void RestoreAutoPowerPlan()
        {
            if (!_autoPowerPlanSwitched) return;
            try
            {
                string prev = _autoPowerPlanPrev ?? "balanced";
                bool ok = ExecutePowerPlan(prev);
                AddLog("AUTO", "AutonomousManager", ok
                    ? "Restored power plan to " + prev
                    : "Failed to restore power plan " + prev);
                _autoPowerPlanSwitched = false;
                _autoPowerPlanPrev = null;
            }
            catch (Exception ex) { LogError("AutoPowerPlan.Restore", ex); }
        }

        private void EvaluateThermalSubsystem()
        {
            var sub = _subsystems["Thermal"];
            if (_smoothTemp > 85)
            {
                sub.Health = Math.Max(0, 100 - (int)((_smoothTemp - 60) * 4));
                QueueAutoAction("Thermal", "EmergencyCooling", "Temperature critical at " + string.Format("{0:F0}", _smoothTemp) + "C", 10);
            }
            else if (_smoothTemp > 70)
            {
                sub.Health = Math.Max(30, 100 - (int)((_smoothTemp - 60) * 3));
                QueueAutoAction("Thermal", "PreventiveCooling", "Temperature elevated at " + string.Format("{0:F0}", _smoothTemp) + "C", 6);
            }
            else
            {
                // Heat has cleared: restore any power plan the autonomous manager
                // switched to Power Saver for cooling.
                if (_autoPowerPlanSwitched) RestoreAutoPowerPlan();
                sub.Health = Math.Max(70, 100 - (int)((_smoothTemp - 40) * 1.5));
            }
        }

        private void EvaluateMemorySubsystem()
        {
            var sub = _subsystems["Memory"];
            if (_smoothRam > 85)
            {
                sub.Health = Math.Max(0, 100 - (int)((_smoothRam - 50) * 2.8));
                QueueAutoAction("Memory", "AggressiveCleanup", "RAM critical at " + string.Format("{0:F0}", _smoothRam) + "%", 8);
            }
            else if (_smoothRam > 65)
            {
                sub.Health = Math.Max(40, 100 - (int)((_smoothRam - 50) * 2));
                QueueAutoAction("Memory", "LightCleanup", "RAM elevated at " + string.Format("{0:F0}", _smoothRam) + "%", 4);
            }
            else
            {
                sub.Health = Math.Max(70, 100 - (int)(_smoothRam * 0.5));
            }
        }

        private void EvaluateCPUSubsystem()
        {
            var sub = _subsystems["CPU"];
            if (_smoothCpu > 90)
            {
                sub.Health = Math.Max(0, 100 - (int)((_smoothCpu - 50) * 2));
                QueueAutoAction("CPU", "ThrottleBackground", "CPU critical at " + string.Format("{0:F0}", _smoothCpu) + "%", 7);
            }
            else if (_smoothCpu > 70)
            {
                sub.Health = Math.Max(30, 100 - (int)((_smoothCpu - 50) * 1.5));
            }
            else
            {
                sub.Health = Math.Max(60, 100 - (int)(_smoothCpu * 0.5));
            }
        }

        private void EvaluateDiskSubsystem()
        {
            var sub = _subsystems["Disk"];
            if (_currentState.DiskUsage > 90)
            {
                sub.Health = Math.Max(0, 100 - (int)((_currentState.DiskUsage - 60) * 3.3));
                QueueAutoAction("Disk", "EmergencyCleanup", "Disk critically full at " + string.Format("{0:F0}", _currentState.DiskUsage) + "%", 7);
            }
            else if (_currentState.DiskUsage > 80)
            {
                sub.Health = Math.Max(30, 100 - (int)((_currentState.DiskUsage - 60) * 2));
            }
            else
            {
                sub.Health = Math.Max(60, 100 - (int)(_currentState.DiskUsage * 0.5));
            }
        }

        private void EvaluateProcessSubsystem()
        {
            var sub = _subsystems["Processes"];
            if (_currentState.ProcessCount > 300)
            {
                sub.Health = Math.Max(10, 100 - (_currentState.ProcessCount - 100) / 5);
                QueueAutoAction("Processes", "ReduceProcesses", "Too many processes: " + _currentState.ProcessCount, 5);
            }
            else
            {
                sub.Health = Math.Max(50, 100 - _currentState.ProcessCount / 10);
            }
        }

        private void EvaluateNetworkSubsystem()
        {
            var sub = _subsystems["Network"];
            sub.Health = 90;
            sub.Status = "Monitoring";
        }

        private void QueueAutoAction(string subsystem, string action, string reason, int priority)
        {
            foreach (var existing in _autoActions)
            {
                if (existing.Action == action && (DateTime.Now - existing.Timestamp).TotalSeconds < 30)
                    return;
            }

            var autoAction = new AutoAction();
            autoAction.Subsystem = subsystem;
            autoAction.Action = action;
            autoAction.Reason = reason;
            autoAction.Priority = priority;
            autoAction.Timestamp = DateTime.Now;
            autoAction.Status = "Queued";
            _autoActions.Add(autoAction);
            if (_autoActions.Count > 20) _autoActions.RemoveAt(0);
        }

        private void ExecuteAutoActions()
        {
            _autoActions.Sort(delegate(AutoAction a, AutoAction b) { return b.Priority.CompareTo(a.Priority); });

            int executed = 0;
            foreach (var action in _autoActions)
            {
                if (action.Status != "Queued") continue;
                // Hard cap per cycle. Default 0 keeps V19 fully manual: queued
                // decisions are visible but only run when the user raises the cap.
                if (executed >= _maxAutoActionsPerCycle) break;

                bool success = false;
                switch (action.Action)
                {
                    case "EmergencyCooling":
                        success = ExecuteThermalProtection(true);
                        // ExecuteThermalProtection switches to Power Saver only when
                        // NOT in a game; track it so it can be restored on normalize.
                        if (success && !_currentState.IsGame && !_autoPowerPlanSwitched)
                        {
                            _autoPowerPlanPrev = GetCurrentPowerPlan();
                            _autoPowerPlanSwitched = true;
                        }
                        break;
                    case "PreventiveCooling":
                        success = ExecuteThermalProtection(false);
                        break;
                    case "AggressiveCleanup":
                    case "LightCleanup":
                        success = ExecuteMemoryClean();
                        break;
                    case "ThrottleBackground":
                        success = ExecuteLimitBackground();
                        break;
                    case "EmergencyPowerSave":
                        if (!_autoPowerPlanSwitched && !_currentState.IsGame)
                        {
                            _autoPowerPlanPrev = GetCurrentPowerPlan();
                            success = ExecutePowerPlan("power saver");
                            if (success) _autoPowerPlanSwitched = true;
                        }
                        else
                        {
                            success = true; // already applied, or game active
                        }
                        break;
                    case "EmergencyCleanup":
                    case "ReduceProcesses":
                        success = ExecutePurgeStandby();
                        break;
                }

                action.Status = success ? "Completed" : "Failed";
                action.CompletedAt = DateTime.Now;
                _autoActionsExecuted++;
                executed++;

                AutoSubsystem sub;
                if (_subsystems.TryGetValue(action.Subsystem, out sub))
                {
                    sub.LastAction = DateTime.Now;
                    sub.ActionCount++;
                }
            }

            _autoActions.RemoveAll(delegate(AutoAction a) { return a.Status != "Queued"; });
        }

        private void UpdateAutonomousHealth()
        {
            double totalHealth = 0;
            int count = 0;
            foreach (var kv in _subsystems)
            {
                totalHealth += kv.Value.Health;
                count++;
            }
            _autoState.OverallHealth = count > 0 ? totalHealth / count : 100;
            _autoState.SubsystemCount = _subsystems.Count;
            _autoState.ActiveSubsystems = 0;
            foreach (var kv in _subsystems)
            {
                if (kv.Value.Health > 30) _autoState.ActiveSubsystems++;
            }
        }
    }
}
