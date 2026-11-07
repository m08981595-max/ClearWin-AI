using System;
using System.Collections.Generic;
using System.Linq;

namespace ClearWin
{
    public partial class SmartAgent
    {
        private List<AIPlan> _activePlans = new List<AIPlan>();
        private List<AIPlan> _planHistory = new List<AIPlan>();
        private DateTime _lastPlanCheck = DateTime.MinValue;
        private const int PLAN_CHECK_INTERVAL_SECONDS = 30;
        private int _plansExecuted = 0;
        private int _plansSucceeded = 0;

        public List<AIPlan> GetActivePlans() { return new List<AIPlan>(_activePlans); }
        public List<AIPlan> GetPlanHistory() { return new List<AIPlan>(_planHistory); }
        public int GetPlansExecuted() { return _plansExecuted; }
        public int GetPlansSucceeded() { return _plansSucceeded; }

        public void RunAIPlanning()
        {
            if ((DateTime.Now - _lastPlanCheck).TotalSeconds < PLAN_CHECK_INTERVAL_SECONDS) return;

            ExecuteActivePlans();

            var newPlans = GeneratePlans();
            foreach (var plan in newPlans)
            {
                _activePlans.Add(plan);
            }

            while (_activePlans.Count > 5) _activePlans.RemoveAt(0);
            while (_planHistory.Count > 50) _planHistory.RemoveAt(0);

            _lastPlanCheck = DateTime.Now;
        }

        private List<AIPlan> GeneratePlans()
        {
            var plans = new List<AIPlan>();

            if (_smoothTemp > 70)
            {
                var plan = new AIPlan();
                plan.Name = "Temperature Reduction";
                plan.Goal = "Reduce system temperature below 65C";
                plan.Priority = 8;
                plan.Steps = new List<PlanStep>();
                plan.EstimatedDuration = "5-10 minutes";
                plan.Confidence = 0.7;

                plan.Steps.Add(new PlanStep { Order = 1, Action = "ReduceThermal", Description = "Reduce CPU/GPU thermal load", EstimatedTime = "30s" });
                plan.Steps.Add(new PlanStep { Order = 2, Action = "LimitBackground", Description = "Reduce background CPU usage", EstimatedTime = "10s" });
                plan.Steps.Add(new PlanStep { Order = 3, Action = "BalancedPerformance", Description = "Switch to balanced power plan", EstimatedTime = "5s" });
                plan.Steps.Add(new PlanStep { Order = 4, Action = "Evaluate", Description = "Re-evaluate temperature after changes", EstimatedTime = "60s" });

                plan.SuccessCondition = "Temperature < 65C";
                plan.RollbackAction = "UnparkAllCores";
                plans.Add(plan);
            }

            if (_currentState.IsGame && (_smoothFps < 55 || _currentState.FpsStability < 75))
            {
                var plan = new AIPlan();
                plan.Name = "Gaming Optimization";
                plan.Goal = "Achieve stable 60+ FPS";
                plan.Priority = 9;
                plan.Steps = new List<PlanStep>();
                plan.EstimatedDuration = "2-3 minutes";
                plan.Confidence = 0.75;

                plan.Steps.Add(new PlanStep { Order = 1, Action = "LightClean", Description = "Free up memory for game", EstimatedTime = "15s" });
                plan.Steps.Add(new PlanStep { Order = 2, Action = "BoostGamePriority", Description = "Boost game process priority", EstimatedTime = "5s" });
                plan.Steps.Add(new PlanStep { Order = 3, Action = "SetGameAffinity", Description = "Set optimal CPU affinity", EstimatedTime = "5s" });
                plan.Steps.Add(new PlanStep { Order = 4, Action = "LimitBackground", Description = "Reduce background activity", EstimatedTime = "10s" });
                plan.Steps.Add(new PlanStep { Order = 5, Action = "Evaluate", Description = "Monitor FPS improvement", EstimatedTime = "120s" });

                plan.SuccessCondition = "FPS > 55 and Stability > 80%";
                plan.RollbackAction = "BalancedPerformance";
                plans.Add(plan);
            }

            if (_smoothTemp > 85 || _smoothRam > 92 || _smoothCpu > 95)
            {
                var plan = new AIPlan();
                plan.Name = "Emergency Recovery";
                plan.Goal = "Stabilize system from critical state";
                plan.Priority = 10;
                plan.Steps = new List<PlanStep>();
                plan.EstimatedDuration = "1-2 minutes";
                plan.Confidence = 0.9;

                if (_smoothTemp > 85)
                {
                    plan.Steps.Add(new PlanStep { Order = 1, Action = "EmergencyThrottle", Description = "Emergency thermal throttle", EstimatedTime = "5s" });
                }
                if (_smoothRam > 92)
                {
                    plan.Steps.Add(new PlanStep { Order = 2, Action = "AggressiveClean", Description = "Aggressive memory cleanup", EstimatedTime = "15s" });
                }
                if (_smoothCpu > 95)
                {
                    plan.Steps.Add(new PlanStep { Order = 3, Action = "LimitBackground", Description = "Limit all background processes", EstimatedTime = "10s" });
                }
                plan.Steps.Add(new PlanStep { Order = 99, Action = "Evaluate", Description = "Full system re-evaluation", EstimatedTime = "30s" });

                plan.SuccessCondition = "CPU < 80% and RAM < 85% and Temp < 75C";
                plan.RollbackAction = "BalancedPerformance";
                plans.Add(plan);
            }

            int hour = DateTime.Now.Hour;
            if (hour >= 1 && hour < 5 && _smoothCpu < 20)
            {
                var plan = new AIPlan();
                plan.Name = "Night Maintenance";
                plan.Goal = "Deep clean and optimize during idle time";
                plan.Priority = 3;
                plan.Steps = new List<PlanStep>();
                plan.EstimatedDuration = "10-15 minutes";
                plan.Confidence = 0.6;

                plan.Steps.Add(new PlanStep { Order = 1, Action = "AggressiveClean", Description = "Deep memory cleanup", EstimatedTime = "30s" });
                plan.Steps.Add(new PlanStep { Order = 2, Action = "PurgeStandbyList", Description = "Purge standby memory", EstimatedTime = "15s" });
                plan.Steps.Add(new PlanStep { Order = 3, Action = "UnparkAllCores", Description = "Unpark all CPU cores for maintenance", EstimatedTime = "10s" });
                plan.Steps.Add(new PlanStep { Order = 4, Action = "Evaluate", Description = "Verify improvements", EstimatedTime = "60s" });

                plan.SuccessCondition = "System health > 85%";
                plans.Add(plan);
            }

            return plans;
        }

        private void ExecuteActivePlans()
        {
            var completedPlans = new List<AIPlan>();
            foreach (var plan in _activePlans)
            {
                if (plan.CurrentStep < plan.Steps.Count)
                {
                    var step = plan.Steps[plan.CurrentStep];
                    if (step.Action == "Evaluate")
                    {
                        plan.Status = "Evaluating...";
                        plan.CurrentStep++;
                    }
                    else
                    {
                        plan.Status = "Executing Step " + (plan.CurrentStep + 1) + ": " + step.Description;
                        // Central Execution Gate: plan steps are autonomous -
                        // while the valve is off, hold the step (do not advance
                        // CurrentStep) and retry once it is enabled again.
                        if (CanExecuteAction("PlanStep:" + step.Action, false))
                        {
                            bool stepOk = ExecutePlanStepAction(step.Action);
                            if (stepOk) _plansExecuted++;
                            plan.CurrentStep++;
                        }
                    }
                }
                else
                {
                    plan.Status = "Completed";
                    plan.CompletedAt = DateTime.Now;
                    completedPlans.Add(plan);
                    _plansSucceeded++;
                }
            }

            foreach (var plan in completedPlans)
            {
                _activePlans.Remove(plan);
                _planHistory.Add(plan);
            }
        }

        private bool ExecutePlanStepAction(string action)
        {
            try
            {
                switch (action)
                {
                    case "ReduceThermal":
                    case "EmergencyThrottle":
                        ExecuteThermalProtection(action == "EmergencyThrottle");
                        return true;
                    case "LimitBackground":
                        ExecuteLimitBackground();
                        return true;
                    case "BalancedPerformance":
                        ExecutePowerPlan("balanced");
                        return true;
                    case "LightClean":
                    case "AggressiveClean":
                    case "OptimizeRAM":
                        ExecuteMemoryClean();
                        return true;
                    case "BoostGamePriority":
                        ExecuteBoostGamePriority();
                        return true;
                    case "SetGameAffinity":
                        ExecuteSetAffinity(_currentState.ActiveProcess ?? "", 0xFF);
                        return true;
                    case "PurgeStandbyList":
                        ExecutePurgeStandby();
                        return true;
                    case "UnparkAllCores":
                        ExecuteCoreParking(false);
                        return true;
                    case "CleanTemp":
                        ExecutePurgeStandby();
                        return true;
                    default:
                        return false;
                }
            }
            catch { return false; }
        }

        public AIPlan GetMostCriticalPlan()
        {
            AIPlan critical = null;
            int maxPriority = 0;
            foreach (var plan in _activePlans)
            {
                if (plan.Priority > maxPriority)
                {
                    maxPriority = plan.Priority;
                    critical = plan;
                }
            }
            return critical;
        }
    }
}
