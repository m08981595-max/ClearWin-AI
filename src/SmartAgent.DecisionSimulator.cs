using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ClearWin
{
    public partial class SmartAgent
    {
        // === Decision Simulator Fields ===
        private List<SimulationResult> _simulationHistory = new List<SimulationResult>();
        private DateTime _lastSimulationTime = DateTime.MinValue;
        private const int SIMULATION_INTERVAL_SECONDS = 10;
        private const int MAX_SIMULATION_HISTORY = 200;
        private Dictionary<string, double> _actionSuccessRates = new Dictionary<string, double>();
        private Dictionary<string, double> _actionAvgGain = new Dictionary<string, double>();
        private Dictionary<string, int> _actionSimCount = new Dictionary<string, int>();
        private Dictionary<string, int> _actionSimSuccess = new Dictionary<string, int>();
        private double _virtualCpu = 50, _virtualRam = 50, _virtualTemp = 45, _virtualFps = 60;
        private DateTime _lastSimSnapshot = DateTime.MinValue;

        // === Public Accessors ===
        public List<SimulationResult> GetSimulationHistory() { return new List<SimulationResult>(_simulationHistory); }
        public SimulationResult GetLastSimulation() { return _simulationHistory.Count > 0 ? _simulationHistory[_simulationHistory.Count - 1] : null; }
        public double GetActionSuccessRate(string action) { double rate; return _actionSuccessRates.TryGetValue(action, out rate) ? rate : 0.5; }
        public double GetActionAvgGain(string action) { double gain; return _actionAvgGain.TryGetValue(action, out gain) ? gain : 0; }

        // === Core Simulation Method ===
        public SimulationResult SimulateDecisionFull(AgentDecision decision)
        {
            if (decision == null) return null;
            var result = new SimulationResult();
            result.Action = decision.Action;
            result.Description = decision.Description;
            result.Timestamp = DateTime.Now;

            UpdateVirtualState();

            result.ExpectedSuccessRate = CalculateSuccessRate(decision);
            result.ExpectedPerformanceGain = EstimatePerformanceGain(decision);
            result.PotentialRisks = AssessPotentialRisks(decision);
            result.CanRollback = CanRollbackDecision(decision);

            result.SimulationScore = result.ExpectedSuccessRate * 0.4
                + Math.Min(100, result.ExpectedPerformanceGain * 2) * 0.3
                + (100 - result.PotentialRisks) * 0.2
                + (result.CanRollback ? 10.0 : 0) * 0.1;

            SimulateEffect(decision, result);

            _simulationHistory.Add(result);
            if (_simulationHistory.Count > MAX_SIMULATION_HISTORY) _simulationHistory.RemoveAt(0);

            UpdateSimStats(decision.Action, result.ExpectedSuccessRate > 0.5);

            _lastSimulationTime = DateTime.Now;
            AddLog("SIM", "DecisionSimulator",
                string.Format("Simulated {0}: Success={1:F0}% Gain={2:F1}% Risk={3:F0} Rollback={4}",
                decision.Action, result.ExpectedSuccessRate * 100, result.ExpectedPerformanceGain,
                result.PotentialRisks, result.CanRollback ? "YES" : "NO"));

            return result;
        }

        // === Update Virtual State (mirror real system) ===
        private void UpdateVirtualState()
        {
            if ((DateTime.Now - _lastSimSnapshot).TotalSeconds < 5) return;
            _virtualCpu = _smoothCpu;
            _virtualRam = _smoothRam;
            _virtualTemp = _smoothTemp;
            _virtualFps = _smoothFps > 0 ? _smoothFps : 60;
            _lastSimSnapshot = DateTime.Now;
        }

        // === Calculate Success Rate ===
        private double CalculateSuccessRate(AgentDecision decision)
        {
            double baseRate = 0.7;

            double histRate;
            if (_actionSuccessRates.TryGetValue(decision.Action, out histRate))
            {
                baseRate = histRate;
            }

            baseRate *= (0.5 + decision.Confidence * 0.5);
            baseRate *= (1.0 - decision.Risk * 0.3);

            if (_smoothTemp > 80) baseRate *= 0.7;
            if (_smoothRam > 90) baseRate *= 0.8;
            if (_smoothCpu > 95) baseRate *= 0.75;

            if (decision.Category == DecisionCategory.ThermalProtect && _smoothTemp > 75)
                baseRate = Math.Min(0.95, baseRate * 1.2);

            return Math.Max(0.05, Math.Min(0.98, baseRate));
        }

        // === Estimate Performance Gain ===
        private double EstimatePerformanceGain(AgentDecision decision)
        {
            double gain = 0;

            switch (decision.Action)
            {
                case "AggressiveClean":
                case "LightClean":
                case "PreemptiveClean":
                    gain = Math.Max(0, (_smoothRam - 60) * 0.5);
                    break;
                case "PurgeStandbyList":
                    gain = Math.Max(0, (_smoothRam - 50) * 0.3);
                    break;
                case "BoostGamePriority":
                    gain = _currentState.IsGame ? 8.0 : 2.0;
                    break;
                case "EmergencyThrottle":
                case "ReduceThermal":
                    gain = Math.Max(0, (_smoothTemp - 65) * 0.4);
                    break;
                case "StabilizeFrames":
                case "ReduceMicroStutter":
                    gain = _currentState.FpsStability < 80 ? 5.0 : 2.0;
                    break;
                case "BatterySaver":
                    gain = _isOnBattery ? 3.0 : 0.5;
                    break;
                case "BalancedPerformance":
                    gain = 2.0;
                    break;
                case "SetGameAffinity":
                    gain = _currentState.IsGame ? 4.0 : 1.0;
                    break;
                case "UnparkAllCores":
                    gain = 3.0;
                    break;
                default:
                    gain = 1.0;
                    break;
            }

            double learnedBoost;
            if (_categoryLearnedBoost.TryGetValue(decision.Category.ToString(), out learnedBoost))
                gain *= (1.0 + learnedBoost);

            return Math.Max(0, Math.Min(25, gain));
        }

        // === Assess Potential Risks ===
        private double AssessPotentialRisks(AgentDecision decision)
        {
            double risk = decision.Risk * 100;

            if (decision.Action == "EmergencyThrottle" && _smoothTemp < 60)
                risk += 15;

            if (decision.Action == "AggressiveClean" && _smoothRam < 40)
                risk += 10;

            if (decision.Action.Contains("Throttle") && _currentState.IsGame)
                risk += 5;

            if (_isOnBattery && decision.Action == "UnparkAllCores")
                risk += 20;

            if (_smoothTemp > 85 && decision.Action != "EmergencyThrottle" && decision.Action != "ReduceThermal")
                risk += 10;

            return Math.Max(0, Math.Min(100, risk));
        }

        // === Check if Decision Can Be Rolled Back ===
        private bool CanRollbackDecision(AgentDecision decision)
        {
            string[] reversibleActions = new[] {
                "BatterySaver", "BalancedPerformance", "BoostGamePriority",
                "SetGameAffinity", "LimitBackgroundAffinity", "UnparkAllCores"
            };
            for (int i = 0; i < reversibleActions.Length; i++)
            {
                if (decision.Action == reversibleActions[i]) return true;
            }
            return false;
        }

        // === Simulate Effect on Virtual State ===
        private void SimulateEffect(AgentDecision decision, SimulationResult result)
        {
            double perfGain = result.ExpectedPerformanceGain;
            double successRate = result.ExpectedSuccessRate;

            if (successRate > 0.5)
            {
                switch (decision.Action)
                {
                    case "AggressiveClean":
                    case "LightClean":
                    case "PreemptiveClean":
                    case "PurgeStandbyList":
                        _virtualRam = Math.Max(20, _virtualRam - perfGain * 0.8);
                        break;
                    case "BoostGamePriority":
                    case "SetGameAffinity":
                        _virtualFps = Math.Min(144, _virtualFps + perfGain * 0.5);
                        break;
                    case "EmergencyThrottle":
                    case "ReduceThermal":
                        _virtualTemp = Math.Max(35, _virtualTemp - perfGain * 0.6);
                        _virtualFps = Math.Max(20, _virtualFps - perfGain * 0.2);
                        break;
                    case "BatterySaver":
                        _virtualCpu = Math.Max(20, _virtualCpu - 5);
                        break;
                    case "UnparkAllCores":
                        _virtualCpu = Math.Min(100, _virtualCpu + 3);
                        _virtualFps = Math.Min(144, _virtualFps + 2);
                        break;
                }
            }
        }

        // === Update Simulation Statistics ===
        private void UpdateSimStats(string action, bool success)
        {
            int count;
            if (!_actionSimCount.TryGetValue(action, out count)) count = 0;
            _actionSimCount[action] = count + 1;

            int successes;
            if (!_actionSimSuccess.TryGetValue(action, out successes)) successes = 0;
            if (success) _actionSimSuccess[action] = successes + 1;
            else _actionSimSuccess[action] = successes;

            double newRate = (double)_actionSimSuccess[action] / _actionSimCount[action];
            _actionSuccessRates[action] = newRate;
        }

        // === Run Simulation Check (called from Tick) ===
        public void RunSimulationCheck()
        {
            if ((DateTime.Now - _lastSimulationTime).TotalSeconds < SIMULATION_INTERVAL_SECONDS) return;
            if (_pendingDecisions.Count == 0) return;

            var topDecision = _pendingDecisions[0];
            var simulation = SimulateDecisionFull(topDecision);
            if (simulation != null && simulation.ExpectedSuccessRate < 0.3)
            {
                AddLog("WARN", "DecisionSimulator",
                    "Low success rate predicted for " + topDecision.Action + ": " +
                    string.Format("{0:F0}%", simulation.ExpectedSuccessRate * 100));
            }
        }
    }
}
