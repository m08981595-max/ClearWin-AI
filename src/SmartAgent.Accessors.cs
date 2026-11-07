using System;
using System.Collections.Generic;

namespace ClearWin
{
    public partial class SmartAgent
    {
        // === Level 3: Elite AI Accessors ===
        public string GetDNASummary()
        {
            if (_performanceDNA == null) return "Learning...";
            return string.Format("Tier:{0} Samples:{1}", _performanceDNA.DeviceTier, _performanceDNA.TotalSamples);
        }
        public string GetActiveGoalName()
        {
            if (_activeGoal != null) return _activeGoal.Name ?? "None";
            return "None";
        }
        public double GetActiveGoalProgress()
        {
            if (_activeGoal == null || _activeGoal.TargetMetrics == null) return 0;
            var vals = new List<double>(_activeGoal.TargetMetrics.Values);
            if (vals.Count == 0) return 0;
            double sum = 0;
            for (int i = 0; i < vals.Count; i++) sum += vals[i];
            return sum / vals.Count;
        }
        public int GetTotalSimulations()
        {
            lock (_decisionLock)
            {
                var sims = new List<int>(_actionSimCount.Values);
                int total = 0;
                for (int i = 0; i < sims.Count; i++) total += sims[i];
                return total;
            }
        }
        public double GetAvgSimSuccessRate()
        {
            lock (_decisionLock)
            {
                var rates = new List<double>(_actionSuccessRates.Values);
                if (rates.Count == 0) return 0;
                double sum = 0;
                for (int i = 0; i < rates.Count; i++) sum += rates[i];
                return (sum / rates.Count) * 100;
            }
        }
        public double GetAvgSimPerformanceGain()
        {
            lock (_decisionLock)
            {
                var gains = new List<double>(_actionAvgGain.Values);
                if (gains.Count == 0) return 0;
                double sum = 0;
                for (int i = 0; i < gains.Count; i++) sum += gains[i];
                return sum / gains.Count;
            }
        }
        public PerformanceForecast GetLatestForecast()
        {
            if (_forecasts.Count > 0) return _forecasts[_forecasts.Count - 1];
            return null;
        }
        public double GetLastEliteRisk()
        {
            if (_riskHistory.Count > 0) return _riskHistory[_riskHistory.Count - 1].RiskScore;
            return 0;
        }
        public int GetTotalRiskBlocked()
        {
            return _blockedActions;
        }
        public double GetAvgEliteRisk()
        {
            if (_riskHistory.Count == 0) return 0;
            double sum = 0;
            for (int i = 0; i < _riskHistory.Count; i++) sum += _riskHistory[i].RiskScore;
            return sum / _riskHistory.Count;
        }
        public int GetKnowledgeBasePatternCount()
        {
            return _learnedPatterns.Count;
        }
        public double GetKnowledgeBaseSuccessRate()
        {
            var patterns = new List<KnowledgePattern>(_learnedPatterns.Values);
            if (patterns.Count == 0) return 0;
            double totalRate = 0;
            int count = 0;
            for (int i = 0; i < patterns.Count; i++)
            {
                if (patterns[i].TotalTrials > 0)
                {
                    totalRate += (double)patterns[i].Successes / patterns[i].TotalTrials;
                    count++;
                }
            }
            if (count == 0) return 0;
            return totalRate / count;
        }
        public int GetKnowledgeBaseEntryCount()
        {
            lock (_kbLock) { return _knowledgeBase.Count; }
        }
        public int GetActiveBoosterCount()
        {
            int active = 0;
            for (int i = 0; i < _adaptiveBoosters.Count; i++)
            {
                if (_adaptiveBoosters[i].CurrentStrength > _adaptiveBoosters[i].MinStrength) active++;
            }
            return active;
        }
        public double GetAvgBoosterStrength()
        {
            if (_adaptiveBoosters.Count == 0) return 0;
            double sum = 0;
            for (int i = 0; i < _adaptiveBoosters.Count; i++) sum += _adaptiveBoosters[i].CurrentStrength;
            return sum / _adaptiveBoosters.Count;
        }
        public string GetPeakBoosterName()
        {
            string peak = null;
            double peakStr = 0;
            for (int i = 0; i < _adaptiveBoosters.Count; i++)
            {
                if (_adaptiveBoosters[i].CurrentStrength > peakStr)
                {
                    peakStr = _adaptiveBoosters[i].CurrentStrength;
                    peak = _adaptiveBoosters[i].Name;
                }
            }
            return peak;
        }

        // === Level 4: Research Grade AI Accessors ===
        public double GetWorldModelAnomalyCount()
        {
            int count = 0;
            if (_worldModel != null && _worldModel.Nodes != null)
            {
                for (int i = 0; i < _worldModel.Nodes.Count; i++)
                {
                    if (_worldModel.Nodes[i].Health < 0.5) count++;
                }
            }
            return count;
        }
        public string GetBestRLActionName()
        {
            lock (_decisionLock)
            {
                string best = "N/A";
                double bestQ = double.MinValue;
                var keys = new List<string>(_actionValues.Keys);
                for (int i = 0; i < keys.Count; i++)
                {
                    if (_actionValues[keys[i]].QValue > bestQ)
                    {
                        bestQ = _actionValues[keys[i]].QValue;
                        best = keys[i];
                    }
                }
                return best;
            }
        }
        public double GetBestRLQValueSafe()
        {
            lock (_decisionLock)
            {
                double bestQ = 0;
                var keys = new List<string>(_actionValues.Keys);
                for (int i = 0; i < keys.Count; i++)
                {
                    if (_actionValues[keys[i]].QValue > bestQ)
                        bestQ = _actionValues[keys[i]].QValue;
                }
                return bestQ;
            }
        }
        public int GetEpisodeCount()
        {
            lock (_decisionLock) { return _episodes.Count; }
        }
        public string GetActivePlanNameSafe()
        {
            lock (_decisionLock)
            {
                if (_activePlans.Count > 0) return _activePlans[0].Name ?? "None";
                return "None";
            }
        }
        public int GetActivePlanStep()
        {
            lock (_decisionLock)
            {
                if (_activePlans.Count > 0) return _activePlans[0].CurrentStep;
                return 0;
            }
        }
        public int GetActivePlanTotalSteps()
        {
            lock (_decisionLock)
            {
                if (_activePlans.Count > 0) return _activePlans[0].Steps.Count;
                return 0;
            }
        }
        public int GetCompletedPlanCount()
        {
            return _plansSucceeded;
        }
        public int GetLongTermRecordCount()
        {
            int count = _longTermMemory.Count + _gameOptHistory.Count + _driverRecords.Count + _updateRecords.Count;
            return count;
        }
        public int GetLongTermGameCount()
        {
            return _gameOptHistory.Count;
        }
        public int GetLongTermDriverCount()
        {
            return _driverRecords.Count;
        }
        public int GetLongTermUpdateCount()
        {
            return _updateRecords.Count;
        }
        public int GetArchFitnessCount()
        {
            return _analyzerFitness.Count;
        }
        public int GetDisabledAnalyzerCount()
        {
            if (_analyzerFitness == null) return 0;
            int count = 0;
            var vals = new List<AnalyzerFitness>(_analyzerFitness.Values);
            for (int i = 0; i < vals.Count; i++)
            {
                if (!vals[i].Enabled) count++;
            }
            return count;
        }
        public int GetActiveAnalyzerCount()
        {
            if (_analyzerFitness == null) return 0;
            int count = 0;
            var vals = new List<AnalyzerFitness>(_analyzerFitness.Values);
            for (int i = 0; i < vals.Count; i++)
            {
                if (vals[i].Enabled) count++;
            }
            return count;
        }
        public long GetSelfProfileRamMB()
        {
            if (_selfProfile == null) return 0;
            return _selfProfile.MemoryUsageMB;
        }
        public int GetSelfProfileThreads()
        {
            if (_selfProfile == null) return 0;
            return _selfProfile.ThreadCount;
        }
        public double GetSelfProfileCpuOverhead()
        {
            if (_selfProfile == null) return 0;
            return _selfProfile.CpuOverheadPercent;
        }
        public double GetSelfProfileTickMs()
        {
            if (_selfProfile == null) return 0;
            return _selfProfile.AvgTickTimeMs;
        }
        public int GetCalibrationBucketCount()
        {
            if (_calibrationBuckets == null) return 0;
            int count = 0;
            lock (_decisionLock)
            {
                var keys = new List<string>(_calibrationBuckets.Keys);
                for (int i = 0; i < keys.Count; i++)
                {
                    count += _calibrationBuckets[keys[i]].TotalSamples;
                }
            }
            return count;
        }
        public int GetCalibrationActionCount()
        {
            if (_calibrationBuckets == null) return 0;
            lock (_decisionLock) { return _calibrationBuckets.Count; }
        }

        // === Level 5-7: Enterprise, Graph Accessors ===
        public int GetReportCount()
        {
            return _reports.Count;
        }
        public int GetAuditEntryCountSafe()
        {
            return _auditLog.Count;
        }
        public double GetDegradationRate()
        {
            if (_kpis != null) return _kpis.DegradationRate;
            return 0;
        }
        public int GetGraphSnapshotCount()
        {
            return _graphSnapshots.Count;
        }
        public string GetTopChangeSetName()
        {
            var sets = GetOptimalChangeSets();
            if (sets.Count > 0) return sets[0].Name;
            return null;
        }

        // Test seam for the shared process-snapshot cache. Returns the current
        // snapshot (refreshing it first) so the unit tests can exercise the
        // concurrency contract without reaching into private members.
        internal System.Diagnostics.Process[] GetProcessesForTest()
        {
            return GetCachedProcesses();
        }
    }
}
