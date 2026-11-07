using System;
using System.Collections.Generic;
using System.Linq;

namespace ClearWin
{
    public partial class SmartAgent
    {
        // === Risk Engine Fields ===
        private List<RiskAssessment> _riskHistory = new List<RiskAssessment>();
        private DateTime _lastRiskCheck = DateTime.MinValue;
        private const int RISK_CHECK_INTERVAL_SECONDS = 5;
        private const double RISK_BLOCK_THRESHOLD = 80.0;
        private const double RISK_WARNING_THRESHOLD = 50.0;
        private Dictionary<string, double> _actionRiskHistory = new Dictionary<string, double>();
        private int _blockedActions = 0;
        private int _warnedActions = 0;
        private int _totalRiskAssessments = 0;

        private readonly object _riskLock = new object();

        // === Public Accessors ===
        public List<RiskAssessment> GetRiskHistory() { lock (_riskLock) { return new List<RiskAssessment>(_riskHistory); } }
        public int GetBlockedActions() { return _blockedActions; }
        public int GetTotalRiskAssessments() { return _totalRiskAssessments; }
        public double GetActionRisk(string action)
        {
            lock (_riskLock)
            {
                double risk;
                return _actionRiskHistory.TryGetValue(action, out risk) ? risk : 50;
            }
        }

        // === Calculate Risk Score (0-100) ===
        public RiskAssessment CalculateEliteRisk(AgentDecision decision)
        {
            var assessment = new RiskAssessment();
            assessment.Action = decision.Action;
            assessment.Timestamp = DateTime.Now;
            assessment.Factors = new List<RiskFactor>();

            double totalRisk = 0;

            // 1. Base risk from action type
            double baseRisk = GetBaseActionRisk(decision.Action);
            totalRisk += baseRisk;
            assessment.Factors.Add(new RiskFactor { Name = "BaseActionRisk", Score = baseRisk, Weight = 0.2 });

            // 2. System state risk
            double stateRisk = GetSystemStateRisk();
            totalRisk += stateRisk;
            assessment.Factors.Add(new RiskFactor { Name = "SystemState", Score = stateRisk, Weight = 0.25 });

            // 3. Thermal risk
            double thermalRisk = GetThermalRisk();
            totalRisk += thermalRisk;
            assessment.Factors.Add(new RiskFactor { Name = "Thermal", Score = thermalRisk, Weight = 0.2 });

            // 4. Resource exhaustion risk
            double resourceRisk = GetResourceExhaustionRisk();
            totalRisk += resourceRisk;
            assessment.Factors.Add(new RiskFactor { Name = "ResourceExhaustion", Score = resourceRisk, Weight = 0.15 });

            // 5. Historical failure risk
            double histRisk = GetHistoricalRisk(decision.Action);
            totalRisk += histRisk;
            assessment.Factors.Add(new RiskFactor { Name = "HistoricalFailure", Score = histRisk, Weight = 0.1 });

            // 6. Conflict risk
            double conflictRisk = GetConflictRisk(decision);
            totalRisk += conflictRisk;
            assessment.Factors.Add(new RiskFactor { Name = "ConflictRisk", Score = conflictRisk, Weight = 0.1 });

            // Calculate weighted final score (0-100)
            double weightedScore = 0;
            double totalWeight = 0;
            foreach (var factor in assessment.Factors)
            {
                weightedScore += factor.Score * factor.Weight;
                totalWeight += factor.Weight;
            }
            assessment.RiskScore = totalWeight > 0 ? weightedScore / totalWeight : 50;
            assessment.RiskScore = Math.Max(0, Math.Min(100, assessment.RiskScore));

            // Determine action
            assessment.ShouldBlock = assessment.RiskScore >= RISK_BLOCK_THRESHOLD;
            assessment.ShouldWarn = assessment.RiskScore >= RISK_WARNING_THRESHOLD && !assessment.ShouldBlock;
            assessment.RequiresApproval = assessment.RiskScore >= RISK_BLOCK_THRESHOLD;

            // Mitigations
            assessment.Mitigations = new List<string>();
            if (thermalRisk > 60) assessment.Mitigations.Add("Wait for temperature to decrease");
            if (resourceRisk > 60) assessment.Mitigations.Add("Free up resources first");
            if (histRisk > 60) assessment.Mitigations.Add("Consider alternative action");
            if (conflictRisk > 60) assessment.Mitigations.Add("Resolve conflicts first");
            if (assessment.RiskScore > 70) assessment.Mitigations.Add("Create restore point before execution");

            // Store thread-safely
            lock (_riskLock)
            {
                _riskHistory.Add(assessment);
                if (_riskHistory.Count > 200) _riskHistory.RemoveAt(0);
                _actionRiskHistory[decision.Action] = assessment.RiskScore;
                _totalRiskAssessments++;
            }

            if (assessment.ShouldBlock)
            {
                _blockedActions++;
                AddLog("RISK", "RiskEngine",
                    string.Format("BLOCKED {0}: Risk={1:F0}/100 (threshold={2})", decision.Action, assessment.RiskScore, RISK_BLOCK_THRESHOLD));
            }
            else if (assessment.ShouldWarn)
            {
                _warnedActions++;
                AddLog("WARN", "RiskEngine",
                    string.Format("WARNING {0}: Risk={1:F0}/100", decision.Action, assessment.RiskScore));
            }

            return assessment;
        }

        // === Get Base Action Risk ===
        private double GetBaseActionRisk(string action)
        {
            switch (action)
            {
                case "EmergencyThrottle": return 70;
                case "AggressiveClean": return 55;
                case "UnparkAllCores": return 50;
                case "ReduceThermal": return 45;
                case "BoostGamePriority": return 35;
                case "SetGameAffinity": return 30;
                case "PurgeStandbyList": return 40;
                case "LimitBackground": return 35;
                case "LightClean": return 25;
                case "PreemptiveClean": return 30;
                case "StabilizeFrames": return 35;
                case "ReduceMicroStutter": return 35;
                case "BatterySaver": return 15;
                case "BalancedPerformance": return 15;
                case "SetLowIoPriority": return 20;
                case "LimitBackgroundAffinity": return 25;
                case "SuggestClose": return 10;
                case "SuspendBloatProcesses": return 65;
                case "DisableGameDvr": return 55;
                case "StartupDisable": return 60;
                case "KillProcess": return 85;
                default: return 55;
            }
        }

        // === Get System State Risk ===
        private double GetSystemStateRisk()
        {
            double risk = 0;
            if (_smoothCpu > 90) risk += 30;
            else if (_smoothCpu > 75) risk += 15;
            if (_smoothRam > 90) risk += 25;
            else if (_smoothRam > 75) risk += 10;
            if (_smoothGpu > 90) risk += 20;
            return Math.Min(100, risk);
        }

        // === Get Thermal Risk ===
        private double GetThermalRisk()
        {
            if (_smoothTemp > 90) return 90;
            if (_smoothTemp > 80) return 70;
            if (_smoothTemp > 70) return 40;
            if (_smoothTemp > 60) return 20;
            return 5;
        }

        // === Get Resource Exhaustion Risk ===
        private double GetResourceExhaustionRisk()
        {
            double risk = 0;
            double availableRamMb = (_currentState.TotalRamGB - _currentState.UsedRamGB) * 1024;
            if (availableRamMb < 500) risk += 40;
            else if (availableRamMb < 1000) risk += 20;
            if (_currentState.DiskUsage > 95) risk += 30;
            else if (_currentState.DiskUsage > 85) risk += 15;
            if (_currentState.ProcessCount > 300) risk += 20;
            return Math.Min(100, risk);
        }

        // === Get Historical Risk ===
        private double GetHistoricalRisk(string action)
        {
            int total, success;
            if (!_actionTotalCount.TryGetValue(action, out total)) return 30;
            if (!_actionSuccessCount.TryGetValue(action, out success)) return 30;
            if (total < 3) return 30; // Not enough data
            double failRate = 1.0 - ((double)success / total);
            return failRate * 100;
        }

        // === Get Conflict Risk ===
        private double GetConflictRisk(AgentDecision decision)
        {
            if (_lastVotes == null || _lastVotes.Count == 0) return 10;
            int conflicting = 0;
            foreach (var vote in _lastVotes)
            {
                if (vote.Action != decision.Action && vote.Action != "NONE" && vote.Priority > 5)
                    conflicting++;
            }
            if (conflicting == 0) return 5;
            return Math.Min(80, conflicting * 20.0);
        }

        // === Check if Action Should Be Blocked ===
        public bool ShouldBlockAction(AgentDecision decision)
        {
            var assessment = CalculateEliteRisk(decision);
            return assessment.ShouldBlock;
        }

        // === Get Risk Summary ===
        public string GetRiskSummary()
        {
            return string.Format("Assessments: {0} | Blocked: {1} | Warned: {2}",
                _totalRiskAssessments, _blockedActions, _warnedActions);
        }
    }
}
