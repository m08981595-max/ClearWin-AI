using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ClearWin
{
    public partial class SmartAgent
    {
        // ===================== DIGITAL TWIN =====================
        private Dictionary<string, double> _twinBaseline = new Dictionary<string, double>();
        private DateTime _lastTwinUpdate = DateTime.MinValue;
        private const int TWIN_UPDATE_INTERVAL = 10;

        // Simulate what would happen if we execute a decision
        public DigitalTwinResult SimulateDecision(AgentDecision decision)
        {
            var result = new DigitalTwinResult();
            result.DecisionAction = decision.Action;
            result.DecisionCategory = decision.Category.ToString();
            
            lock (_stateLock)
            {
                if (_currentState == null) return result;
                
                // Take current state as baseline
                result.BeforeCpu = _currentState.CpuUsage;
                result.BeforeRam = _currentState.RamUsage;
                result.BeforeTemp = _currentState.CpuTemp;
                result.BeforeFps = _currentState.Fps;
                result.BeforeStability = _currentState.FpsStability;

                // Measured effect from the RewardEngine for this exact action.
                // Predictions blend toward these real means as samples accumulate
                // (heuristic only while the action has no measured history).
                TwinCalibration cal;
                _twinCalibration.TryGetValue(decision.Action, out cal);

                // Simulate based on category
                switch (decision.Category)
                {
                    case DecisionCategory.MemoryClean:
                        result.PredictedRamDelta = -Math.Max(0.1,
                            BlendTwinCalibration(cal, cal != null ? cal.SumRamFreedGB : 0, _currentState.UsedRamGB * 0.08 * _tuningWeights.MemoryWeight));
                        result.PredictedRamDelta = Math.Max(-2.0, result.PredictedRamDelta);
                        result.PredictedCpuDelta = 2.0; // brief CPU spike during clean
                        result.Confidence = 0.85;
                        result.SimulatedDurationMs = 500;
                        break;
                    case DecisionCategory.PurgeStandby:
                        result.PredictedRamDelta = -Math.Max(0.05,
                            BlendTwinCalibration(cal, cal != null ? cal.SumRamFreedGB : 0, Math.Min(1.5, _currentState.UsedRamGB * 0.05)));
                        result.Confidence = 0.9;
                        result.SimulatedDurationMs = 200;
                        break;
                    case DecisionCategory.ThermalProtect:
                        result.PredictedTempDelta = -BlendTwinCalibration(cal, cal != null ? cal.SumTempCooled : 0, 8.0 * _tuningWeights.ThermalWeight);
                        result.PredictedCpuDelta = -BlendTwinCalibration(cal, cal != null ? cal.SumCpuReduced : 0, 15.0); // throttling reduces CPU
                        result.PredictedFpsDelta = -5.0; // slight FPS loss
                        result.Confidence = 0.95;
                        result.SimulatedDurationMs = 1000;
                        break;
                    case DecisionCategory.ProcessPriority:
                    case DecisionCategory.GameBoost:
                        result.PredictedFpsDelta = BlendTwinCalibration(cal, cal != null ? cal.SumFpsGain : 0, 5.0 * _tuningWeights.CpuWeight);
                        result.PredictedStabilityDelta = BlendTwinCalibration(cal, cal != null ? cal.SumStabilityGain : 0, 8.0);
                        result.PredictedCpuDelta = 3.0; // slightly higher CPU
                        result.Confidence = 0.75;
                        result.SimulatedDurationMs = 100;
                        break;
                    case DecisionCategory.BackgroundControl:
                        result.PredictedCpuDelta = -BlendTwinCalibration(cal, cal != null ? cal.SumCpuReduced : 0, 5.0 * _tuningWeights.BackgroundWeight);
                        result.PredictedRamDelta = -Math.Max(0.05,
                            BlendTwinCalibration(cal, cal != null ? cal.SumRamFreedGB : 0, 0.3));
                        result.Confidence = 0.8;
                        result.SimulatedDurationMs = 300;
                        break;
                    case DecisionCategory.PowerPlan:
                        if (decision.Action.Contains("Saver"))
                        {
                            result.PredictedCpuDelta = -10.0;
                            result.PredictedTempDelta = -5.0;
                            result.PredictedFpsDelta = -10.0;
                        }
                        else if (decision.Action.Contains("Balanced"))
                        {
                            result.PredictedCpuDelta = 0;
                            result.PredictedTempDelta = 0;
                        }
                        else // ultimate/performance
                        {
                            result.PredictedCpuDelta = 5.0;
                            result.PredictedTempDelta = 3.0;
                            result.PredictedFpsDelta = 8.0;
                        }
                        result.Confidence = 0.9;
                        result.SimulatedDurationMs = 50;
                        break;
                    case DecisionCategory.CpuAffinity:
                        result.PredictedFpsDelta = BlendTwinCalibration(cal, cal != null ? cal.SumFpsGain : 0, 3.0 * _tuningWeights.AffinityWeight);
                        result.PredictedStabilityDelta = BlendTwinCalibration(cal, cal != null ? cal.SumStabilityGain : 0, 5.0);
                        result.Confidence = 0.7;
                        result.SimulatedDurationMs = 50;
                        break;
                    case DecisionCategory.IoPriority:
                        result.PredictedStabilityDelta = BlendTwinCalibration(cal, cal != null ? cal.SumStabilityGain : 0, 3.0);
                        result.Confidence = 0.75;
                        result.SimulatedDurationMs = 50;
                        break;
                    case DecisionCategory.CoreParking:
                        result.PredictedCpuDelta = -BlendTwinCalibration(cal, cal != null ? cal.SumCpuReduced : 0, 2.0);
                        result.PredictedFpsDelta = BlendTwinCalibration(cal, cal != null ? cal.SumFpsGain : 0, 2.0);
                        result.Confidence = 0.65;
                        result.SimulatedDurationMs = 100;
                        break;
                }
                
                // Confidence rises as the twin accumulates real calibration data
                // for this action (measured beats heuristic).
                if (cal != null && cal.Count > 0)
                    result.Confidence = Math.Min(0.98, result.Confidence + cal.Count * 0.01);
                
                // Apply device profile adjustments
                if (_deviceProfile.PerformanceTier <= 2)
                {
                    // Low-end: effects are more pronounced
                    result.PredictedRamDelta *= 1.3;
                    result.PredictedCpuDelta *= 1.2;
                }
                
                // Calculate overall predicted benefit
                result.PredictedOverallBenefit = CalculateSimulatedBenefit(result);
                
                // Risk assessment from simulation
                result.SimulatedRisk = CalculateSimulatedRisk(result);
            }
            
            return result;
        }

        private double CalculateSimulatedBenefit(DigitalTwinResult result)
        {
            double benefit = 0;
            // Positive: RAM freed
            if (result.PredictedRamDelta < 0) benefit += Math.Abs(result.PredictedRamDelta) * 10;
            // Positive: FPS improved
            if (result.PredictedFpsDelta > 0) benefit += result.PredictedFpsDelta * 2;
            // Positive: Stability improved
            if (result.PredictedStabilityDelta > 0) benefit += result.PredictedStabilityDelta * 1.5;
            // Negative: CPU spike (temporary is ok)
            if (result.PredictedCpuDelta > 10) benefit -= (result.PredictedCpuDelta - 10) * 0.5;
            // Negative: Temperature rise
            if (result.PredictedTempDelta > 5) benefit -= (result.PredictedTempDelta - 5) * 2;
            // Negative: FPS drop
            if (result.PredictedFpsDelta < -5) benefit += result.PredictedFpsDelta * 3;
            return Math.Max(-100, Math.Min(100, benefit));
        }

        private double CalculateSimulatedRisk(DigitalTwinResult result)
        {
            double risk = 0;
            // Temperature risk
            double newTemp = _currentState.CpuTemp + result.PredictedTempDelta;
            if (newTemp > _deviceProfile.ThermalThrottleTemp) risk += 40;
            else if (newTemp > _deviceProfile.ThermalWarningTemp) risk += 20;
            // RAM risk: don't clean if already low
            double newRam = _currentState.RamUsage + (result.PredictedRamDelta / _currentState.TotalRamGB * 100);
            if (newRam > 90) risk += 30;
            else if (newRam > 80) risk += 10;
            // FPS risk
            double newFps = _currentState.Fps + result.PredictedFpsDelta;
            if (_currentState.Fps > 0 && newFps < 20) risk += 25;
            return Math.Max(0, Math.Min(100, risk));
        }

        // Should we execute based on simulation?
        public bool ShouldExecuteSimulation(DigitalTwinResult simulation)
        {
            // Never execute if simulated risk is too high
            if (simulation.SimulatedRisk > 60) return false;
            // Always execute if benefit is positive and risk is low
            if (simulation.PredictedOverallBenefit > 0 && simulation.SimulatedRisk < 30) return true;
            // Execute if confidence is high enough
            if (simulation.Confidence > 0.8 && simulation.PredictedOverallBenefit > -5) return true;
            return false;
        }

        // ===================== AI FPS PREDICTOR =====================
        private Dictionary<string, FPSPrediction> _gameFpsPredictions = new Dictionary<string, FPSPrediction>();

        public FPSPrediction PredictGameFPS(string gameName)
        {
            var prediction = new FPSPrediction();
            prediction.GameName = gameName;
            prediction.PredictedAt = DateTime.Now;
            
            lock (_stateLock)
            {
                if (_currentState == null) return prediction;
                
                // Base prediction from hardware tier
                switch (_deviceProfile.PerformanceTier)
                {
                    case 4: prediction.BaseFps = 120; break; // Enthusiast
                    case 3: prediction.BaseFps = 90; break;  // High
                    case 2: prediction.BaseFps = 60; break;  // Medium
                    case 1: prediction.BaseFps = 35; break;  // Low
                }
                
                // Adjust for current system load
                double loadFactor = 1.0 - (_currentState.CpuUsage / 200.0);
                double ramFactor = _currentState.RamUsage < 80 ? 1.0 : 0.85;
                double thermalFactor = _currentState.CpuTemp < 75 ? 1.0 : _currentState.CpuTemp < 85 ? 0.9 : 0.75;
                
                prediction.PredictedFps = prediction.BaseFps * loadFactor * ramFactor * thermalFactor;
                
                // Identify bottleneck
                if (_currentState.CpuUsage > 85) prediction.Bottleneck = "CPU";
                else if (_currentState.GpuUsage > 90) prediction.Bottleneck = "GPU";
                else if (_currentState.RamUsage > 85) prediction.Bottleneck = "RAM";
                else if (_currentState.DiskUsage > 90) prediction.Bottleneck = "Disk";
                else prediction.Bottleneck = "None";
                
                // Best settings suggestion
                if (prediction.PredictedFps < 30) prediction.RecommendedSettings = "Low settings, 720p";
                else if (prediction.PredictedFps < 60) prediction.RecommendedSettings = "Medium settings, 1080p";
                else if (prediction.PredictedFps < 90) prediction.RecommendedSettings = "High settings, 1080p";
                else prediction.RecommendedSettings = "Ultra settings, 1440p+";
                
                // Store prediction
                _gameFpsPredictions[gameName] = prediction;
            }
            
            return prediction;
        }

        // ===================== AI ROOT CAUSE ANALYSIS =====================
        private List<RootCause> _rootCauses = new List<RootCause>();
        private DateTime _lastRootCauseAnalysis = DateTime.MinValue;
        private const int ROOT_CAUSE_INTERVAL_SECONDS = 15;

        public List<RootCause> AnalyzeRootCauses()
        {
            if ((DateTime.Now - _lastRootCauseAnalysis).TotalSeconds < ROOT_CAUSE_INTERVAL_SECONDS) return _rootCauses;
            _lastRootCauseAnalysis = DateTime.Now;
            
            _rootCauses.Clear();
            
            lock (_stateLock)
            {
                if (_currentState == null) return _rootCauses;
                
                // CPU High Root Cause
                if (_currentState.CpuUsage > 70)
                {
                    var cause = new RootCause();
                    cause.Metric = "CPU Usage";
                    cause.CurrentValue = _currentState.CpuUsage;
                    cause.Threshold = 70;
                    cause.Severity = _currentState.CpuUsage > 90 ? "Critical" : _currentState.CpuUsage > 80 ? "High" : "Medium";
                    
                    // Find top CPU consumers
                    var topCpu = FindTopCpuProcesses(3);
                    if (topCpu.Count > 0)
                    {
                        cause.PrimaryCause = topCpu[0];
                        cause.AllCauses = topCpu;
                        cause.Explanation = string.Format("Top CPU consumer: {0} ({1}%)", topCpu[0], GetProcessCpuUsage(topCpu[0]));
                    }
                    else
                    {
                        cause.PrimaryCause = "System load";
                        cause.Explanation = "High system load without a single dominant process";
                    }
                    _rootCauses.Add(cause);
                }
                
                // RAM High Root Cause
                if (_currentState.RamUsage > 75)
                {
                    var cause = new RootCause();
                    cause.Metric = "RAM Usage";
                    cause.CurrentValue = _currentState.RamUsage;
                    cause.Threshold = 75;
                    cause.Severity = _currentState.RamUsage > 90 ? "Critical" : _currentState.RamUsage > 85 ? "High" : "Medium";
                    
                    var topRam = FindTopRamProcesses(3);
                    if (topRam.Count > 0)
                    {
                        cause.PrimaryCause = topRam[0];
                        cause.AllCauses = topRam;
                        cause.Explanation = string.Format("Top RAM consumer: {1} - {0}MB", GetProcessRamMB(topRam[0]), topRam[0]);
                    }
                    _rootCauses.Add(cause);
                }
                
                // Temperature Root Cause
                if (_currentState.CpuTemp > _deviceProfile.ThermalWarningTemp)
                {
                    var cause = new RootCause();
                    cause.Metric = "CPU Temperature";
                    cause.CurrentValue = _currentState.CpuTemp;
                    cause.Threshold = _deviceProfile.ThermalWarningTemp;
                    cause.Severity = _currentState.CpuTemp > _deviceProfile.ThermalThrottleTemp ? "Critical" : "High";
                    
                    // Temperature is usually caused by high CPU/GPU usage
                    string reason = "";
                    if (_currentState.CpuUsage > 80) reason += "High CPU usage (" + string.Format("{0:F0}", _currentState.CpuUsage) + "%)";
                    if (_currentState.GpuUsage > 80) reason += (reason.Length > 0 ? " + " : "") + "High GPU usage (" + string.Format("{0:F0}", _currentState.GpuUsage) + "%)";
                    if (reason.Length == 0) reason = "Sustained workload or inadequate cooling";
                    
                    cause.PrimaryCause = reason;
                    cause.Explanation = string.Format("Temperature {0:F0}C caused by: {1}", _currentState.CpuTemp, reason);
                    _rootCauses.Add(cause);
                }
                
                // Disk High Root Cause
                if (_currentState.DiskUsage > 85)
                {
                    var cause = new RootCause();
                    cause.Metric = "Disk Usage";
                    cause.CurrentValue = _currentState.DiskUsage;
                    cause.Threshold = 85;
                    cause.Severity = _currentState.DiskUsage > 95 ? "Critical" : "High";
                    
                    var topDisk = FindTopDiskProcesses(3);
                    if (topDisk.Count > 0)
                    {
                        cause.PrimaryCause = topDisk[0];
                        cause.AllCauses = topDisk;
                        cause.Explanation = string.Format("Top disk I/O: {0}", topDisk[0]);
                    }
                    else
                    {
                        cause.PrimaryCause = "Files accumulated";
                        cause.Explanation = "Disk is " + string.Format("{0:F0}", _currentState.DiskUsage) + "% full - consider cleanup";
                    }
                    _rootCauses.Add(cause);
                }
                
                // FPS Low Root Cause
                if (_currentState.Fps > 0 && _currentState.Fps < 30 && _currentState.IsGame)
                {
                    var cause = new RootCause();
                    cause.Metric = "Frame Rate";
                    cause.CurrentValue = _currentState.Fps;
                    cause.Threshold = 30;
                    cause.Severity = _currentState.Fps < 20 ? "Critical" : "High";
                    
                    // Identify FPS bottleneck
                    if (_currentState.CpuUsage > 90) { cause.PrimaryCause = "CPU bottleneck"; cause.Explanation = "CPU at " + string.Format("{0:F0}", _currentState.CpuUsage) + "% - CPU-bound game"; }
                    else if (_currentState.GpuUsage > 95) { cause.PrimaryCause = "GPU bottleneck"; cause.Explanation = "GPU at " + string.Format("{0:F0}", _currentState.GpuUsage) + "% - GPU-bound game"; }
                    else if (_currentState.RamUsage > 90) { cause.PrimaryCause = "RAM bottleneck"; cause.Explanation = "RAM at " + string.Format("{0:F0}", _currentState.RamUsage) + "% - insufficient memory"; }
                    else { cause.PrimaryCause = "Unknown bottleneck"; cause.Explanation = "Low FPS without clear resource bottleneck"; }
                    _rootCauses.Add(cause);
                }
            }
            
            return _rootCauses;
        }

        private List<string> FindTopCpuProcesses(int count)
        {
            var result = new List<string>();
            try
            {
                Process[] procs;
                lock (_processCacheLock) { procs = _cachedProcesses; }
                if (procs == null || procs.Length == 0) return result;
                var scored = new List<KeyValuePair<string, double>>();
                try
                {
                    foreach (var p in procs)
                    {
                        try
                        {
                            if (p.HasExited) continue;
                            string name = p.ProcessName;
                            if (SYSTEM_PROCESSES.Contains(name.ToLower())) continue;
                            if (name == "ClearWin" || name == "Idle") continue;
                            scored.Add(new KeyValuePair<string, double>(name, p.WorkingSet64));
                        }
                        catch (Exception ex) { LogError("FindTopCpuProcesses", ex); }
                    }
                }
                catch (Exception ex) { LogError("FindTopCpuProcesses", ex); }
                scored.Sort((a, b) => b.Value.CompareTo(a.Value));
                for (int i = 0; i < Math.Min(count, scored.Count); i++)
                    if (!result.Contains(scored[i].Key)) result.Add(scored[i].Key);
            }
            catch (Exception ex) { LogError("FindTopCpuProcesses", ex); }
            return result;
        }

        private List<string> FindTopRamProcesses(int count)
        {
            var result = new List<string>();
            try
            {
                Process[] procs;
                lock (_processCacheLock) { procs = _cachedProcesses; }
                if (procs == null || procs.Length == 0) return result;
                var scored = new List<KeyValuePair<string, long>>();
                try
                {
                    foreach (var p in procs)
                    {
                        try
                        {
                            if (p.HasExited) continue;
                            string name = p.ProcessName;
                            if (SYSTEM_PROCESSES.Contains(name.ToLower())) continue;
                            if (name == "ClearWin" || name == "Idle") continue;
                            long ram = p.WorkingSet64;
                            if (ram > 50 * 1024 * 1024) scored.Add(new KeyValuePair<string, long>(name, ram));
                        }
                        catch (Exception ex) { LogError("FindTopRamProcesses", ex); }
                    }
                }
                catch (Exception ex) { LogError("FindTopRamProcesses", ex); }
                scored.Sort((a, b) => b.Value.CompareTo(a.Value));
                for (int i = 0; i < Math.Min(count, scored.Count); i++)
                    if (!result.Contains(scored[i].Key)) result.Add(scored[i].Key);
            }
            catch (Exception ex) { LogError("FindTopRamProcesses", ex); }
            return result;
        }

        private List<string> FindTopDiskProcesses(int count)
        {
            // Disk I/O attribution is limited without ETW; return top RAM consumers as proxy
            return FindTopCpuProcesses(count);
        }

        private double GetProcessCpuUsage(string processName)
        {
            try
            {
                var procs = Process.GetProcessesByName(processName);
                double total = 0;
                foreach (var p in procs)
                {
                    try { total += p.TotalProcessorTime.TotalMilliseconds; }
                    catch (System.ComponentModel.Win32Exception) { }
                    catch (InvalidOperationException) { }
                    catch (UnauthorizedAccessException) { }
                    catch (Exception ex) { CWLog.Error("SmartAgent.DigitalTwin", ex); }
                    finally { try { p.Dispose(); } catch { } }
                }
                return total / Math.Max(1, procs.Length) / 1000.0;
            }
            catch { return 0; }
        }

        private double GetProcessRamMB(string processName)
        {
            try
            {
                var procs = Process.GetProcessesByName(processName);
                double total = 0;
                foreach (var p in procs)
                {
                    try { total += p.WorkingSet64 / 1024.0 / 1024.0; }
                    catch (System.ComponentModel.Win32Exception) { }
                    catch (InvalidOperationException) { }
                    catch (UnauthorizedAccessException) { }
                    catch (Exception ex2) { CWLog.Error("SmartAgent.DigitalTwin", ex2); }
                    finally { try { p.Dispose(); } catch { } }
                }
                return total;
            }
            catch { return 0; }
        }

        // Public accessors
        public List<RootCause> GetRootCauses() { return _rootCauses; }
        public Dictionary<string, FPSPrediction> GetFpsPredictions() { return _gameFpsPredictions; }
    }

    // ===================== DIGITAL TWIN MODELS =====================
    public class DigitalTwinResult
    {
        public string DecisionAction;
        public string DecisionCategory;
        public double BeforeCpu;
        public double BeforeRam;
        public double BeforeTemp;
        public double BeforeFps;
        public double BeforeStability;
        public double PredictedCpuDelta;
        public double PredictedRamDelta;
        public double PredictedTempDelta;
        public double PredictedFpsDelta;
        public double PredictedStabilityDelta;
        public double PredictedOverallBenefit; // -100 to +100
        public double SimulatedRisk; // 0-100
        public double Confidence; // 0-1
        public double SimulatedDurationMs;
        public DateTime SimulatedAt;

        public DigitalTwinResult()
        {
            SimulatedAt = DateTime.Now;
            Confidence = 0.5;
        }
    }

    public class FPSPrediction
    {
        public string GameName;
        public double BaseFps;
        public double PredictedFps;
        public string Bottleneck;
        public string RecommendedSettings;
        public DateTime PredictedAt;

        public FPSPrediction() { PredictedAt = DateTime.Now; }
    }

    public class RootCause
    {
        public string Metric;
        public double CurrentValue;
        public double Threshold;
        public string Severity;
        public string PrimaryCause;
        public string Explanation;
        public List<string> AllCauses;

        public RootCause() { AllCauses = new List<string>(); }
    }
}
