using System;
using System.Collections.Generic;

namespace ClearWin
{
    public partial class SmartAgent
    {
        // ===================== EXPLAINABLE AI =====================
        private List<LegacyDecisionExplanation> _explanations = new List<LegacyDecisionExplanation>();
        private HealthScores _healthScores = new HealthScores();
        private int _lastHealthScoreTime = 0;
        private const int HEALTH_SCORE_INTERVAL = 3;

        // ===================== MULTI-DIMENSION HEALTH SCORES =====================
        private void ComputeHealthScores()
        {
            if (_tickCount - _lastHealthScoreTime < HEALTH_SCORE_INTERVAL) return;
            _lastHealthScoreTime = _tickCount;

            lock (_stateLock)
            {
                if (_currentState == null) return;

                _healthScores.Performance = ComputePerformanceScore();
                _healthScores.Stability = ComputeStabilityScore();
                _healthScores.Gaming = ComputeGamingScore();
                _healthScores.Battery = ComputeBatteryScore();
                _healthScores.Security = ComputeSecurityScore();
                _healthScores.Overall = (_healthScores.Performance * 0.30 +
                                         _healthScores.Stability * 0.25 +
                                         _healthScores.Gaming * 0.20 +
                                         _healthScores.Battery * 0.10 +
                                         _healthScores.Security * 0.15);
                _healthScores.ComputedAt = DateTime.Now;
            }
        }

        private double ComputePerformanceScore()
        {
            double score = 100;

            score -= Math.Max(0, (_currentState.CpuUsage - 30) * 0.8);
            score -= Math.Max(0, (_currentState.RamUsage - 50) * 0.7);
            score -= Math.Max(0, (_currentState.DiskUsage - 70) * 0.5);

            if (_currentState.CpuFreqGHz > 0)
            {
                double cpuRatio = _currentState.CpuUsage / (_currentState.CpuFreqGHz * 1000);
                if (cpuRatio > 0.9) score -= 10;
            }

            double freeGB = _currentState.TotalRamGB - _currentState.UsedRamGB;
            if (freeGB < 1) score -= 15;
            else if (freeGB < 2) score -= 8;

            return Math.Max(0, Math.Min(100, score));
        }

        private double ComputeStabilityScore()
        {
            double score = 100;

            if (_currentState.Fps > 0)
            {
                score = _currentState.FpsStability;

                if (_currentState.FrameTimeJitter > 15) score -= 15;
                else if (_currentState.FrameTimeJitter > 8) score -= 8;

                if (_currentState.Fps01Low > 0 && _currentState.Fps > 0)
                {
                    double lowRatio = _currentState.Fps01Low / _currentState.Fps;
                    if (lowRatio < 0.3) score -= 20;
                    else if (lowRatio < 0.5) score -= 10;
                }
            }

            if (_stats.FailedDecisions > 3)
            {
                double failRate = (double)_stats.FailedDecisions / Math.Max(1, _stats.TotalDecisions);
                score -= failRate * 30;
            }

            if (_safeMode) score -= 20;

            return Math.Max(0, Math.Min(100, score));
        }

        private double ComputeGamingScore()
        {
            if (!_currentState.IsGame && _workload.DetectedWorkload != WorkloadType.Gaming)
            {
                return Math.Max(70, _healthScores.Performance);
            }

            double score = 100;

            if (_currentState.Fps > 0)
            {
                if (_currentState.Fps >= 60) score = 95;
                else if (_currentState.Fps >= 45) score = 80;
                else if (_currentState.Fps >= 30) score = 60;
                else if (_currentState.Fps >= 20) score = 40;
                else score = 20;

                if (_currentState.FpsStability > 90) score += 5;
                else if (_currentState.FpsStability < 60) score -= 15;

                if (_currentState.FrameTimeMs > 0 && _currentState.FrameTimeMs < 16.67) score += 3;
                else if (_currentState.FrameTimeMs > 33) score -= 10;
            }
            else
            {
                score = 80;
            }

            if (_currentState.CpuTemp > 85) score -= 15;
            else if (_currentState.CpuTemp > 75) score -= 5;

            if (_currentState.GpuUsage > 95) score -= 5;

            return Math.Max(0, Math.Min(100, score));
        }

        private double ComputeBatteryScore()
        {
            if (_currentState.IsCharging)
            {
                return _currentState.BatteryLevel > 50 ? 95 : 80 + _currentState.BatteryLevel * 0.15;
            }

            double score = _currentState.BatteryLevel;

            if (_batteryHistory.Count >= 2)
            {
                double first = _batteryHistory[0];
                double last = _batteryHistory[_batteryHistory.Count - 1];
                double elapsedMinutes = (_batteryTimestamps.Count >= 2) ?
                    (_batteryTimestamps[_batteryTimestamps.Count - 1] - _batteryTimestamps[0]).TotalMinutes : 1;
                if (elapsedMinutes > 0)
                {
                    double drainRate = (first - last) / elapsedMinutes;
                    if (drainRate > 3) score -= 15;
                    else if (drainRate > 2) score -= 8;
                    else if (drainRate > 1) score -= 3;
                }
            }

            if (_mode == AgentMode.CoolMode) score += 5;
            if (_currentState.CpuUsage > 70) score -= 5;

            return Math.Max(0, Math.Min(100, score));
        }

        private double ComputeSecurityScore()
        {
            double score = 100;

            int highImpactNonProtected = 0;
            foreach (var p in _processImpacts)
            {
                if (p.ImpactScore > 60 && !p.IsUserProtected && !p.IsSystemProcess)
                    highImpactNonProtected++;
            }
            if (highImpactNonProtected > 5) score -= 15;
            else if (highImpactNonProtected > 2) score -= 8;

            if (_stats.FailedDecisions > _stats.SuccessfulDecisions && _stats.TotalDecisions > 10)
                score -= 10;

            if (_safeMode) score -= 15;

            if (_stats.TotalDecisions > 0)
            {
                double successRate = (double)_stats.SuccessfulDecisions / _stats.TotalDecisions;
                if (successRate > 0.9) score += 5;
                else if (successRate < 0.5) score -= 10;
            }

            return Math.Max(0, Math.Min(100, score));
        }

        // ===================== DECISION EXPLANATIONS =====================
        private LegacyDecisionExplanation ExplainDecision(AgentDecision decision)
        {
            var explanation = new LegacyDecisionExplanation
            {
                ActionName = decision.Action,
                Category = decision.Category.ToString(),
                Confidence = decision.Confidence,
            };

            switch (decision.Category)
            {
                case DecisionCategory.MemoryClean:
                    explanation.WhatWillHappen = "Clean working set of memory-heavy processes";
                    explanation.WhyNeeded = string.Format("RAM usage is {0:F0}%", _currentState.RamUsage);
                    explanation.ExpectedBenefit = string.Format("Free ~{0:F0}MB RAM, reduce usage by ~{1:F0}%",
                        _currentState.UsedRamGB * 1024 * 0.1, 5 + _tuningWeights.MemoryWeight * 3);
                    explanation.ExpectedImprovement = 10 + _tuningWeights.MemoryWeight * 5;
                    explanation.RiskAssessment = "Low risk - memory can be re-allocated naturally";
                    explanation.AlternativesConsidered = new[] { "Aggressive clean (riskier)", "Wait for natural release", "Limit background processes instead" };
                    explanation.ChosenReason = "Light clean balances safety and effectiveness";
                    break;

                case DecisionCategory.PurgeStandby:
                    explanation.WhatWillHappen = "Purge standby memory list to free cached pages";
                    explanation.WhyNeeded = string.Format("Standby list is large and RAM pressure is high ({0:F0}%)", _currentState.RamUsage);
                    explanation.ExpectedBenefit = "Reclaim 500MB-2GB of cached memory";
                    explanation.ExpectedImprovement = 15;
                    explanation.RiskAssessment = "Low risk - standby memory is reclaimed automatically if needed";
                    explanation.AlternativesConsidered = new[] { "Memory clean (different approach)", "Do nothing" };
                    explanation.ChosenReason = "Standby purge is efficient for freeing cached memory";
                    break;

                case DecisionCategory.ThermalProtect:
                    explanation.WhatWillHappen = "Reduce system load to prevent thermal throttling";
                    explanation.WhyNeeded = string.Format("CPU temperature is {0:F0}C (limit: {1:F0}C)", _currentState.CpuTemp, _deviceProfile.ThermalThrottleTemp);
                    explanation.ExpectedBenefit = string.Format("Reduce temperature by {0:F0}C", 5 + _tuningWeights.ThermalWeight * 3);
                    explanation.ExpectedImprovement = 20;
                    explanation.RiskAssessment = "Medium - may temporarily reduce performance";
                    explanation.AlternativesConsidered = new[] { "Switch to balanced power plan", "Purge memory to reduce heat", "Emergency throttle (aggressive)" };
                    explanation.ChosenReason = "Moderate approach to prevent damage while maintaining usability";
                    break;

                case DecisionCategory.ProcessPriority:
                case DecisionCategory.GameBoost:
                    explanation.WhatWillHappen = "Boost foreground process priority, limit background";
                    explanation.WhyNeeded = string.Format("Active process '{0}' needs more CPU resources", _currentState.ActiveProcess);
                    explanation.ExpectedBenefit = "Improve responsiveness and reduce frame drops";
                    explanation.ExpectedImprovement = 12 + _tuningWeights.CpuWeight * 4;
                    explanation.RiskAssessment = "Low - background processes get BelowNormal, not disabled";
                    explanation.AlternativesConsidered = new[] { "Set CPU affinity", "Unpark cores", "Do nothing" };
                    explanation.ChosenReason = "Priority adjustment is the safest way to improve foreground performance";
                    break;

                case DecisionCategory.BackgroundControl:
                    explanation.WhatWillHappen = "Limit unnecessary background processes";
                    explanation.WhyNeeded = string.Format("{0} processes running, consuming resources", _currentState.ProcessCount);
                    explanation.ExpectedBenefit = "Reduce CPU and RAM usage by limiting background activity";
                    explanation.ExpectedImprovement = 10 + _tuningWeights.BackgroundWeight * 3;
                    explanation.RiskAssessment = "Low - only targets non-essential background processes";
                    explanation.AlternativesConsidered = new[] { "Suggest close to user", "Reduce process priority only" };
                    explanation.ChosenReason = "Automatic background control is unobtrusive and effective";
                    break;

                case DecisionCategory.PowerPlan:
                    explanation.WhatWillHappen = "Switch Windows power plan for optimal performance/battery";
                    explanation.WhyNeeded = "Current power plan is not optimal for the detected workload";
                    explanation.ExpectedBenefit = "Better performance or battery life depending on context";
                    explanation.ExpectedImprovement = 8;
                    explanation.RiskAssessment = "Low - power plan changes are fully reversible";
                    explanation.AlternativesConsidered = new[] { "Keep current plan", "Custom power settings" };
                    explanation.ChosenReason = "Power plan is the easiest optimization with no side effects";
                    break;

                case DecisionCategory.CpuAffinity:
                    explanation.WhatWillHappen = "Set CPU affinity to optimize core usage";
                    explanation.WhyNeeded = "Process would benefit from specific core assignment";
                    explanation.ExpectedBenefit = "Better cache utilization and reduced context switching";
                    explanation.ExpectedImprovement = 8 + _tuningWeights.AffinityWeight * 3;
                    explanation.RiskAssessment = "Low - affinity can be changed anytime";
                    explanation.AlternativesConsidered = new[] { "Priority boost only", "Core unparking" };
                    explanation.ChosenReason = "Affinity optimization complements priority changes";
                    break;

                case DecisionCategory.IoPriority:
                    explanation.WhatWillHappen = "Lower I/O priority for background processes";
                    explanation.WhyNeeded = "Disk I/O contention is causing slowdowns";
                    explanation.ExpectedBenefit = "Reduce disk latency for foreground applications";
                    explanation.ExpectedImprovement = 6;
                    explanation.RiskAssessment = "Very low - background tasks just wait slightly longer";
                    explanation.AlternativesConsidered = new[] { "Do nothing", "Limit background processes" };
                    explanation.ChosenReason = "I/O priority is invisible to the user but effective";
                    break;

                case DecisionCategory.CoreParking:
                    explanation.WhatWillHappen = "Unpark CPU cores for maximum performance";
                    explanation.WhyNeeded = "Some CPU cores are parked, reducing available processing power";
                    explanation.ExpectedBenefit = "Full CPU core availability for demanding tasks";
                    explanation.ExpectedImprovement = 5;
                    explanation.RiskAssessment = "Low - cores can be re-parked automatically";
                    explanation.AlternativesConsidered = new[] { "Priority boost", "Power plan change" };
                    explanation.ChosenReason = "Core unparking ensures all hardware is available";
                    break;

                default:
                    explanation.WhatWillHappen = "Execute optimization: " + decision.Action;
                    explanation.WhyNeeded = decision.Reason;
                    explanation.ExpectedBenefit = "Improve system performance";
                    explanation.ExpectedImprovement = 5;
                    explanation.RiskAssessment = "Low";
                    break;
            }

            _explanations.Add(explanation);
            if (_explanations.Count > 100) _explanations.RemoveAt(0);

            return explanation;
        }

        // ===================== PUBLIC ACCESSORS =====================
        public HealthScores GetHealthScores()
        {
            return _healthScores;
        }

        public List<LegacyDecisionExplanation> GetRecentExplanations(int count)
        {
            int start = Math.Max(0, _explanations.Count - count);
            return _explanations.GetRange(start, _explanations.Count - start);
        }

        public LegacyDecisionExplanation GetLastExplanation()
        {
            return _explanations.Count > 0 ? _explanations[_explanations.Count - 1] : null;
        }
    }
}
