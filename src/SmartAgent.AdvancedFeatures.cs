using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace ClearWin
{
    // ===================== FEATURE: DEEP USER PATTERN LEARNING =====================
    public class SessionPattern
    {
        public int DayOfWeek;
        public int StartHour;
        public int EndHour;
        public double AvgCpu;
        public double AvgRam;
        public double AvgGpu;
        public double AvgFps;
        public string DominantWorkload;
        public int SampleCount;
        public DateTime LastSeen;
    }

    public class WorkloadTransition
    {
        public string FromWorkload;
        public string ToWorkload;
        public int TransitionCount;
        public double AvgMinutesBetween;
        public DateTime LastTransition;
    }

    public class ProductivityBlock
    {
        public int Hour;
        public double ProductivityScore;
        public double GamingScore;
        public double IdleScore;
        public int TotalSamples;
    }

    // ===================== FEATURE: DYNAMIC THRESHOLDS =====================
    public class DynamicThresholdSet
    {
        public double ThermalWarning;
        public double ThermalThrottle;
        public double RamCleanPercent;
        public double RamAggressivePercent;
        public int MaxProcessCount;
        public double CpuHighPercent;
        public double FpsLowThreshold;
        public int SamplesUsed;
        public DateTime LastAdapted;
    }

    // ===================== FEATURE: AUTO-DISCOVER NEW APPS =====================
    public class KnownApplication
    {
        public string ProcessName;
        public string FirstSeen;
        public int TotalRuns;
        public double AvgCpuCost;
        public double AvgRamCostMB;
        public double AvgGpuCost;
        public bool IsTrusted;
        public bool IsNew;
        public string Category;
        public DateTime LastSeen;
    }

    // ===================== FEATURE: NATURAL TEMPERATURE LEARNING =====================
    public class DeviceTemperatureProfile
    {
        public double IdleTempAvg;
        public double IdleTempStdDev;
        public double LoadTempAvg;
        public double LoadTempStdDev;
        public double DailyTempOffset;    // ambient effect
        public double NaturalBaseline;
        public double[] HourlyIdleTemps = new double[24];
        public double[] HourlyIdleCounts = new double[24];
        public int IdleSamples;
        public int LoadSamples;
        public DateTime LastUpdated;
    }

    // ===================== FEATURE: COST-BENEFIT ANALYSIS =====================
    public class ActionCostBenefit
    {
        public string Action;
        public int TimesExecuted;
        public double TotalResourceCost;     // normalized cost (CPU-seconds, RAM freed, disruption)
        public double TotalBenefit;          // normalized benefit (FPS gain, temp drop, responsiveness)
        public double AvgCostPerExecution;
        public double AvgBenefitPerExecution;
        public double RoiScore;              // benefit / cost
        public double AvgUserSatisfaction;   // based on post-action state
        public DateTime LastEvaluated;
    }

    public partial class SmartAgent
    {
        // === Deep User Pattern Learning ===
        private List<SessionPattern> _sessionPatterns = new List<SessionPattern>();
        private List<WorkloadTransition> _transitions = new List<WorkloadTransition>();
        private ProductivityBlock[] _productivityHours = new ProductivityBlock[24];
        private string _lastWorkloadType = "Desktop";
        private DateTime _lastWorkloadChange = DateTime.Now;
        private DateTime _lastPatternUpdate = DateTime.MinValue;
        private const int PATTERN_UPDATE_SECONDS = 60;

        // === Dynamic Thresholds ===
        private DynamicThresholdSet _dynamicThresholds = new DynamicThresholdSet();
        private Queue<double> _tempHistory60 = new Queue<double>();
        private Queue<double> _ramHistory60 = new Queue<double>();
        private Queue<int> _processCountHistory60 = new Queue<int>();
        private Queue<double> _cpuHistory60 = new Queue<double>();
        private Queue<double> _fpsHistory60 = new Queue<double>();
        private const int ADAPTIVE_WINDOW = 60;

        // === Auto-Discover New Apps ===
        private const int KNOWN_APPS_MAX = 400;
        private Dictionary<string, KnownApplication> _knownApps = new Dictionary<string, KnownApplication>(StringComparer.OrdinalIgnoreCase);
        private HashSet<string> _trustedApps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private DateTime _lastAppDiscovery = DateTime.MinValue;
        private const int APP_DISCOVERY_INTERVAL_SECONDS = 30;
        private List<string> _newAppAlerts = new List<string>();

        // === Natural Temperature Learning ===
        private DeviceTemperatureProfile _tempProfile = new DeviceTemperatureProfile();

        // === Cost-Benefit Analysis ===
        private Dictionary<string, ActionCostBenefit> _costBenefitData = new Dictionary<string, ActionCostBenefit>();
        private DateTime _lastCostBenefitUpdate = DateTime.MinValue;
        private const int COST_BENEFIT_INTERVAL_SECONDS = 120;
        private Queue<Tuple<double, double>> _preActionMetrics = new Queue<Tuple<double, double>>();

        // === Enhanced Explainability (plain-language) ===
        private List<string> _plainExplanations = new List<string>();
        private const int MAX_PLAIN_EXPLANATIONS = 50;

        // ===================== PUBLIC ACCESSORS =====================
        public DynamicThresholdSet GetDynamicThresholds() { return _dynamicThresholds; }
        public Dictionary<string, KnownApplication> GetKnownApps() { return new Dictionary<string, KnownApplication>(_knownApps); }
        public List<string> GetNewAppAlerts() { return new List<string>(_newAppAlerts); }
        public DeviceTemperatureProfile GetTempProfile() { return _tempProfile; }
        public Dictionary<string, ActionCostBenefit> GetCostBenefitData() { return new Dictionary<string, ActionCostBenefit>(_costBenefitData); }
        public List<string> GetPlainExplanations() { return new List<string>(_plainExplanations); }
        public List<ProductivityBlock> GetProductivityHours() { return new List<ProductivityBlock>(_productivityHours); }

        // ===================== INITIALIZATION =====================
        public void InitializeAdvancedFeatures()
        {
            for (int i = 0; i < 24; i++)
                _productivityHours[i] = new ProductivityBlock { Hour = i };

            InitTrustedApps();
            LoadDynamicThresholds();
            LoadTemperatureProfile();
            LoadCostBenefitData();
            LoadKnownApps();
        }

        // ===================== PERIODIC UPDATE (called from Tick) =====================
        public void UpdateAdvancedFeatures()
        {
            if (_tickCount % 5 == 0) UpdateAdaptiveThresholds();
            UpdateTemperatureProfile();
            DiscoverNewApps();
            if (_tickCount % 30 == 0) UpdateCostBenefitAnalysis();
            UpdateDeepPatterns();
        }

        // ======================================================================
        // 1. DEEP USER PATTERN LEARNING
        // ======================================================================
        private void UpdateDeepPatterns()
        {
            if ((DateTime.Now - _lastPatternUpdate).TotalSeconds < PATTERN_UPDATE_SECONDS) return;
            _lastPatternUpdate = DateTime.Now;

            int hour = DateTime.Now.Hour;
            int dow = (int)DateTime.Now.DayOfWeek;

            string currentWorkload = ClassifyCurrentWorkload();
            if (currentWorkload != _lastWorkloadType)
            {
                RecordTransition(_lastWorkloadType, currentWorkload);
                _lastWorkloadType = currentWorkload;
                _lastWorkloadChange = DateTime.Now;
            }

            ProductivityBlock pb = _productivityHours[hour];
            pb.TotalSamples++;
            double cpuNorm = _smoothCpu / 100.0;
            double gpuNorm = _smoothGpu / 100.0;
            double ramNorm = _smoothRam / 100.0;

            if (_currentState.IsGame || gpuNorm > 0.5)
                pb.GamingScore = pb.GamingScore * 0.95 + 1.0 * 0.05;
            else if (cpuNorm > 0.5 || ramNorm > 0.5)
                pb.ProductivityScore = pb.ProductivityScore * 0.95 + 1.0 * 0.05;
            else
                pb.IdleScore = pb.IdleScore * 0.95 + 1.0 * 0.05;

            if (_tickCount % 100 == 0)
            {
                SessionPattern sp = FindOrCreatePattern(dow, hour);
                int n = sp.SampleCount + 1;
                sp.AvgCpu = (sp.AvgCpu * sp.SampleCount + _smoothCpu) / n;
                sp.AvgRam = (sp.AvgRam * sp.SampleCount + _smoothRam) / n;
                sp.AvgGpu = (sp.AvgGpu * sp.SampleCount + _smoothGpu) / n;
                sp.AvgFps = (sp.AvgFps * sp.SampleCount + _smoothFps) / n;
                sp.DominantWorkload = currentWorkload;
                sp.SampleCount = n;
                sp.LastSeen = DateTime.Now;
            }
        }

        private string ClassifyCurrentWorkload()
        {
            if (_currentState.IsGame) return "Gaming";
            double cpu = _smoothCpu;
            double ram = _smoothRam;
            double gpu = _smoothGpu;
            if (cpu < 10 && ram < 30 && gpu < 5) return "Idle";
            if (cpu > 50 && ram > 50) return "Work";
            if (gpu > 30) return "Creative";
            if (cpu > 20) return "Browsing";
            return "Desktop";
        }

        private void RecordTransition(string from, string to)
        {
            WorkloadTransition wt = _transitions.Find(t => t.FromWorkload == from && t.ToWorkload == to);
            if (wt == null)
            {
                wt = new WorkloadTransition { FromWorkload = from, ToWorkload = to };
                _transitions.Add(wt);
            }
            double mins = (DateTime.Now - _lastWorkloadChange).TotalMinutes;
            wt.AvgMinutesBetween = (wt.AvgMinutesBetween * wt.TransitionCount + mins) / (wt.TransitionCount + 1);
            wt.TransitionCount++;
            wt.LastTransition = DateTime.Now;

            if (_transitions.Count > 200) _transitions.RemoveAt(0);
        }

        private SessionPattern FindOrCreatePattern(int dow, int hour)
        {
            string key = dow + "_" + hour;
            for (int i = 0; i < _sessionPatterns.Count; i++)
            {
                if (_sessionPatterns[i].DayOfWeek == dow && _sessionPatterns[i].StartHour == hour)
                    return _sessionPatterns[i];
            }
            var sp = new SessionPattern { DayOfWeek = dow, StartHour = hour, EndHour = hour };
            _sessionPatterns.Add(sp);
            return sp;
        }

        public string GetNextExpectedWorkload()
        {
            WorkloadTransition best = null;
            int bestCount = 0;
            foreach (var t in _transitions)
            {
                if (t.FromWorkload == _lastWorkloadType && t.TransitionCount > bestCount)
                {
                    best = t;
                    bestCount = t.TransitionCount;
                }
            }
            return best != null ? best.ToWorkload : "Desktop";
        }

        public double GetHourProductivityScore(int hour)
        {
            ProductivityBlock pb = _productivityHours[hour];
            if (pb.TotalSamples < 3) return 0.5;
            double total = pb.ProductivityScore + pb.GamingScore + pb.IdleScore;
            if (total < 0.001) return 0.5;
            return pb.ProductivityScore / total;
        }

        // ======================================================================
        // 2. DYNAMIC THRESHOLDS (replaces fixed thresholds)
        // ======================================================================
        private void UpdateAdaptiveThresholds()
        {
            _tempHistory60.Enqueue(_smoothTemp);
            if (_tempHistory60.Count > ADAPTIVE_WINDOW) _tempHistory60.Dequeue();
            _ramHistory60.Enqueue(_smoothRam);
            if (_ramHistory60.Count > ADAPTIVE_WINDOW) _ramHistory60.Dequeue();
            _processCountHistory60.Enqueue(_currentState.ProcessCount);
            if (_processCountHistory60.Count > ADAPTIVE_WINDOW) _processCountHistory60.Dequeue();
            _cpuHistory60.Enqueue(_smoothCpu);
            if (_cpuHistory60.Count > ADAPTIVE_WINDOW) _cpuHistory60.Dequeue();
            _fpsHistory60.Enqueue(_smoothFps);
            if (_fpsHistory60.Count > ADAPTIVE_WINDOW) _fpsHistory60.Dequeue();

            if (_tempHistory60.Count < 20) return;

            double tempMean = ComputeMean(_tempHistory60);
            double tempStd = ComputeStdDev(_tempHistory60, tempMean);
            _dynamicThresholds.ThermalWarning = Math.Max(65, Math.Min(95, tempMean + 2.0 * tempStd));
            _dynamicThresholds.ThermalThrottle = Math.Max(75, Math.Min(100, tempMean + 3.0 * tempStd));

            double ramMean = ComputeMean(_ramHistory60);
            double ramStd = ComputeStdDev(_ramHistory60, ramMean);
            _dynamicThresholds.RamCleanPercent = Math.Max(50, Math.Min(90, ramMean + 1.5 * ramStd));
            _dynamicThresholds.RamAggressivePercent = Math.Max(65, Math.Min(95, ramMean + 2.5 * ramStd));

            double cpuMean = ComputeMean(_cpuHistory60);
            double cpuStd = ComputeStdDev(_cpuHistory60, cpuMean);
            _dynamicThresholds.CpuHighPercent = Math.Max(50, Math.Min(95, cpuMean + 2.0 * cpuStd));

            double processMean = 0;
            foreach (int pc in _processCountHistory60) processMean += pc;
            processMean /= _processCountHistory60.Count;
            double processStd = 0;
            foreach (int pc in _processCountHistory60) processStd += (pc - processMean) * (pc - processMean);
            processStd = Math.Sqrt(processStd / Math.Max(1, _processCountHistory60.Count));
            _dynamicThresholds.MaxProcessCount = (int)(processMean + 2.0 * processStd);

            double fpsMean = ComputeMean(_fpsHistory60);
            double fpsStd = ComputeStdDev(_fpsHistory60, fpsMean);
            _dynamicThresholds.FpsLowThreshold = Math.Max(15, Math.Min(55, fpsMean - 2.0 * fpsStd));

            _dynamicThresholds.SamplesUsed = _tempHistory60.Count;
            _dynamicThresholds.LastAdapted = DateTime.Now;
        }

        private double ComputeMean(Queue<double> q)
        {
            double sum = 0;
            foreach (double v in q) sum += v;
            return sum / Math.Max(1, q.Count);
        }

        private double ComputeStdDev(Queue<double> q, double mean)
        {
            double sumSq = 0;
            foreach (double v in q) sumSq += (v - mean) * (v - mean);
            return Math.Sqrt(sumSq / Math.Max(1, q.Count));
        }

        public double GetAdaptiveThermalWarning() { return _dynamicThresholds.ThermalWarning > 0 ? _dynamicThresholds.ThermalWarning : 82; }
        public double GetAdaptiveRamCleanThreshold() { return _dynamicThresholds.RamCleanPercent > 0 ? _dynamicThresholds.RamCleanPercent : 80; }

        // ======================================================================
        // 3. AUTO-DISCOVER NEW APPS
        // ======================================================================
        private void DiscoverNewApps()
        {
            if ((DateTime.Now - _lastAppDiscovery).TotalSeconds < APP_DISCOVERY_INTERVAL_SECONDS) return;
            _lastAppDiscovery = DateTime.Now;

            Process[] procs = GetCachedProcesses();
            if (procs.Length == 0) return;
            foreach (Process p in procs)
            {
                string name = p.ProcessName;
                if (string.IsNullOrEmpty(name)) continue;

                KnownApplication ka;
                if (!_knownApps.TryGetValue(name, out ka))
                {
                    ka = new KnownApplication
                    {
                        ProcessName = name,
                        FirstSeen = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                        IsNew = true,
                        IsTrusted = IsKnownSystemProcess(name),
                        Category = ClassifyApp(name)
                    };
                    _knownApps[name] = ka;
                    if (_knownApps.Count > KNOWN_APPS_MAX)
                    {
                        string oldest = null; DateTime oldestT = DateTime.MaxValue;
                        foreach (var kv in _knownApps)
                        {
                            if (kv.Value.LastSeen < oldestT) { oldestT = kv.Value.LastSeen; oldest = kv.Key; }
                        }
                        if (oldest != null) _knownApps.Remove(oldest);
                    }

                    if (!ka.IsTrusted && !_newAppAlerts.Contains(name))
                    {
                        _newAppAlerts.Add(name);
                        if (_newAppAlerts.Count > 20) _newAppAlerts.RemoveAt(0);
                        AddLog("DISCOVER", "NewApp", string.Format("New application detected: {0} (Category: {1})", name, ka.Category));
                    }
                }

                ka.TotalRuns++;
                ka.LastSeen = DateTime.Now;
                double cpuDelta = _smoothCpu / Math.Max(1, procs.Length);
                ka.AvgCpuCost = ka.AvgCpuCost * 0.9 + cpuDelta * 0.1;
                ka.IsNew = false;
            }
        }

        private bool IsKnownSystemProcess(string name)
        {
            string lower = name.ToLower();
            return lower == "explorer" || lower == "csrss" || lower == "smss" || lower == "wininit"
                || lower == "winlogon" || lower == "services" || lower == "lsass" || lower == "svchost"
                || lower == "dwm" || lower == "system" || lower == "registry" || lower == "conhost"
                || lower == "fontdrvhost" || lower == "sihost" || lower == "taskhostw" || lower == "ctfmon"
                || lower == "audiodg" || lower == "spoolsv" || lower == "dllhost" || lower == "sppsvc";
        }

        private string ClassifyApp(string name)
        {
            string lower = name.ToLower();
            string[] gameKw = new[] { "game", "steam", "epic", "valorant", "cs2", "fortnite", "pubg", "apex", "gta", "minecraft", "roblox", "league", "dota", "overwatch" };
            foreach (string kw in gameKw) if (lower.Contains(kw)) return "Game";
            string[] browserKw = new[] { "chrome", "firefox", "msedge", "opera", "brave", "vivaldi" };
            foreach (string kw in browserKw) if (lower.Contains(kw)) return "Browser";
            string[] devKw = new[] { "code", "devenv", "visualstudio", "sublime", "notepad++", "intellij", "pycharm" };
            foreach (string kw in devKw) if (lower.Contains(kw)) return "Development";
            string[] mediaKw = new[] { "spotify", "vlc", "mpc", "audacity", "photoshop", "premiere", "afterfx" };
            foreach (string kw in mediaKw) if (lower.Contains(kw)) return "Media";
            string[] commKw = new[] { "discord", "teams", "slack", "zoom", "skype", "telegram" };
            foreach (string kw in commKw) if (lower.Contains(kw)) return "Communication";
            return "Other";
        }

        public List<KnownApplication> GetRecentlyDiscoveredApps()
        {
            var result = new List<KnownApplication>();
            foreach (var kv in _knownApps)
            {
                if (kv.Value.IsNew || (DateTime.Now - kv.Value.LastSeen).TotalMinutes < 30)
                    result.Add(kv.Value);
            }
            return result;
        }

        // ======================================================================
        // 4. NATURAL TEMPERATURE LEARNING
        // ======================================================================
        private void UpdateTemperatureProfile()
        {
            if (_tickCount % 10 != 0) return;

            int hour = DateTime.Now.Hour;
            bool isIdle = _smoothCpu < 15 && _smoothGpu < 5;

            if (isIdle && _tempProfile.IdleSamples < 10000)
            {
                double oldAvg = _tempProfile.IdleTempAvg;
                int n = _tempProfile.IdleSamples + 1;
                _tempProfile.IdleTempAvg = (oldAvg * _tempProfile.IdleSamples + _smoothTemp) / n;
                _tempProfile.IdleSamples = n;
                _tempProfile.HourlyIdleTemps[hour] = _tempProfile.HourlyIdleTemps[hour] * 0.95 + _smoothTemp * 0.05;
                _tempProfile.HourlyIdleCounts[hour]++;

                double variance = (_smoothTemp - _tempProfile.IdleTempAvg) * (_smoothTemp - _tempProfile.IdleTempAvg);
                _tempProfile.IdleTempStdDev = Math.Sqrt(_tempProfile.IdleTempStdDev * _tempProfile.IdleTempStdDev * 0.99 + variance * 0.01);
            }
            else if (!isIdle && _tempProfile.LoadSamples < 10000)
            {
                int n = _tempProfile.LoadSamples + 1;
                _tempProfile.LoadTempAvg = (_tempProfile.LoadTempAvg * _tempProfile.LoadSamples + _smoothTemp) / n;
                _tempProfile.LoadSamples = n;
                double variance = (_smoothTemp - _tempProfile.LoadTempAvg) * (_smoothTemp - _tempProfile.LoadTempAvg);
                _tempProfile.LoadTempStdDev = Math.Sqrt(_tempProfile.LoadTempStdDev * _tempProfile.LoadTempStdDev * 0.99 + variance * 0.01);
            }

            _tempProfile.NaturalBaseline = _tempProfile.IdleTempAvg;
            _tempProfile.DailyTempOffset = _tempProfile.HourlyIdleTemps[hour] - _tempProfile.IdleTempAvg;
            _tempProfile.LastUpdated = DateTime.Now;
        }

        public double GetNaturalTemperatureBaseline()
        {
            if (_tempProfile.IdleSamples < 20) return 45;
            int hour = DateTime.Now.Hour;
            double hourlyBase = _tempProfile.HourlyIdleTemps[hour];
            return _tempProfile.IdleSamples > 0 ? hourlyBase : _tempProfile.IdleTempAvg;
        }

        public bool IsTemperatureAbnormal(double currentTemp)
        {
            if (_tempProfile.IdleSamples < 30) return currentTemp > 85;
            double baseline = GetNaturalTemperatureBaseline() + _tempProfile.DailyTempOffset;
            double threshold = baseline + 3.0 * Math.Max(2, _tempProfile.IdleTempStdDev);
            return currentTemp > threshold;
        }

        public string GetTemperatureInsight()
        {
            if (_tempProfile.IdleSamples < 20) return "Learning device temperature profile...";
            double baseline = GetNaturalTemperatureBaseline();
            double current = _smoothTemp;
            double delta = current - baseline;
            if (Math.Abs(delta) < 3) return string.Format("Temperature normal ({0:F0}C baseline, {1:F0}C current)", baseline, current);
            if (delta > 0) return string.Format("Temperature {0:F0}C above natural baseline ({1:F0}C)", delta, baseline);
            return string.Format("Temperature {0:F0}C below natural baseline ({1:F0}C)", Math.Abs(delta), baseline);
        }

        // ======================================================================
        // 5. COST-BENEFIT ANALYSIS PER ACTION
        // ======================================================================
        private void UpdateCostBenefitAnalysis()
        {
            if ((DateTime.Now - _lastCostBenefitUpdate).TotalSeconds < COST_BENEFIT_INTERVAL_SECONDS) return;
            _lastCostBenefitUpdate = DateTime.Now;

            foreach (var kv in _costBenefitData)
            {
                ActionCostBenefit acb = kv.Value;
                if (acb.TimesExecuted > 0)
                {
                    acb.AvgCostPerExecution = acb.TotalResourceCost / acb.TimesExecuted;
                    acb.AvgBenefitPerExecution = acb.TotalBenefit / acb.TimesExecuted;
                    acb.RoiScore = acb.AvgCostPerExecution > 0.001
                        ? acb.AvgBenefitPerExecution / acb.AvgCostPerExecution
                        : acb.AvgBenefitPerExecution;
                }
                acb.LastEvaluated = DateTime.Now;
            }
        }

        public void RecordActionCost(string action, double resourceCost, double benefit, double userSatisfaction)
        {
            ActionCostBenefit acb;
            if (!_costBenefitData.TryGetValue(action, out acb))
            {
                acb = new ActionCostBenefit { Action = action };
                _costBenefitData[action] = acb;
            }
            acb.TimesExecuted++;
            acb.TotalResourceCost += resourceCost;
            acb.TotalBenefit += benefit;
            acb.AvgUserSatisfaction = (acb.AvgUserSatisfaction * (acb.TimesExecuted - 1) + userSatisfaction) / acb.TimesExecuted;
            acb.AvgCostPerExecution = acb.TotalResourceCost / acb.TimesExecuted;
            acb.AvgBenefitPerExecution = acb.TotalBenefit / acb.TimesExecuted;
            acb.RoiScore = acb.AvgCostPerExecution > 0.001
                ? acb.AvgBenefitPerExecution / acb.AvgCostPerExecution
                : acb.AvgBenefitPerExecution;
        }

        private double ComputeActionCost(string action)
        {
            double cpuCost = _smoothCpu / 100.0 * 0.3;
            double disruption = 0;
            if (action == "PurgeStandbyList") disruption = 0.1;
            else if (action == "AggressiveClean") disruption = 0.4;
            else if (action == "LightClean") disruption = 0.15;
            else if (action == "SetProcessPriority") disruption = 0.2;
            else if (action == "SetCpuAffinity") disruption = 0.3;
            else if (action == "SwitchPowerPlan") disruption = 0.25;
            else disruption = 0.2;
            return cpuCost + disruption;
        }

        private double ComputeActionBenefit(AgentDecision d)
        {
            double benefit = 0;
            if (d.Category == DecisionCategory.MemoryClean || d.Category == DecisionCategory.PurgeStandby)
                benefit = Math.Max(0, (90 - _smoothRam) / 100.0);
            else if (d.Category == DecisionCategory.ThermalProtect)
                benefit = Math.Max(0, (_smoothTemp - GetAdaptiveThermalWarning()) / 20.0);
            else if (d.Category == DecisionCategory.GameBoost || d.Category == DecisionCategory.ProcessPriority)
                benefit = _currentState.IsGame ? 0.6 : 0.2;
            else if (d.Category == DecisionCategory.BackgroundControl)
                benefit = _currentState.ProcessCount > _dynamicThresholds.MaxProcessCount ? 0.5 : 0.1;
            else if (d.Category == DecisionCategory.PowerPlan)
                benefit = _isOnBattery ? 0.5 : 0.3;
            else
                benefit = 0.2;
            return benefit * (d.Confidence * 0.5 + 0.5);
        }

        public string GetBestROIAction()
        {
            string best = "";
            double bestRoi = -1;
            foreach (var kv in _costBenefitData)
            {
                if (kv.Value.TimesExecuted >= 3 && kv.Value.RoiScore > bestRoi)
                {
                    bestRoi = kv.Value.RoiScore;
                    best = kv.Key;
                }
            }
            return best;
        }

        public string GetCostBenefitSummary()
        {
            if (_costBenefitData.Count == 0) return "No cost-benefit data yet.";
            var sb = new StringBuilder();
            var sorted = _costBenefitData.Values.OrderByDescending(a => a.RoiScore).ToList();
            int show = Math.Min(5, sorted.Count);
            for (int i = 0; i < show; i++)
            {
                var a = sorted[i];
                sb.AppendLine(string.Format("{0}: ROI={1:F2} (Cost={2:F3} Benefit={3:F3} N={4})",
                    a.Action, a.RoiScore, a.AvgCostPerExecution, a.AvgBenefitPerExecution, a.TimesExecuted));
            }
            return sb.ToString();
        }

        // ======================================================================
        // 6. ENHANCED EXPLAINABLE AI (plain-language for every decision)
        // ======================================================================
        public string GeneratePlainExplanation(AgentDecision chosen, List<AgentDecision> alternatives)
        {
            var sb = new StringBuilder();

            sb.Append(string.Format("I chose to {0} because ", chosen.Action.ToLower()));

            if (chosen.Category == DecisionCategory.MemoryClean || chosen.Category == DecisionCategory.PurgeStandby)
                sb.Append(string.Format("RAM usage was {0:F0}% (above the learned threshold of {1:F0}%). ", _smoothRam, GetAdaptiveRamCleanThreshold()));
            else if (chosen.Category == DecisionCategory.ThermalProtect)
                sb.Append(string.Format("CPU temperature was {0:F0}C (approaching the adaptive limit of {1:F0}C). ", _smoothTemp, GetAdaptiveThermalWarning()));
            else if (chosen.Category == DecisionCategory.GameBoost)
                sb.Append(string.Format("a game ({0}) was detected and needed more resources. ", _currentState.ActiveProcess ?? "unknown"));
            else if (chosen.Category == DecisionCategory.BackgroundControl)
                sb.Append(string.Format("{0} processes were running (typical for this device is {1}). ", _currentState.ProcessCount, _dynamicThresholds.MaxProcessCount));
            else if (chosen.Category == DecisionCategory.PowerPlan)
                sb.Append(string.Format("power state changed ({0}). ", _isOnBattery ? "on battery" : "plugged in"));
            else
                sb.Append(string.Format("system metrics indicated a need for optimization. "));

            if (chosen.Confidence > 0.8)
                sb.Append(string.Format("Confidence is high ({0:F0}%), ", chosen.Confidence * 100));
            else if (chosen.Confidence > 0.5)
                sb.Append(string.Format("Confidence is moderate ({0:F0}%), ", chosen.Confidence * 100));
            else
                sb.Append(string.Format("Confidence is low ({0:F0}%), ", chosen.Confidence * 100));

            sb.Append(string.Format("risk is {0:F0}%. ", chosen.Risk * 100));

            if (alternatives != null && alternatives.Count > 1)
            {
                int altCount = alternatives.Count - 1;
                sb.Append(string.Format("{0} other options were considered but ranked lower.", altCount));
            }

            string explanation = sb.ToString();
            _plainExplanations.Add(explanation);
            if (_plainExplanations.Count > MAX_PLAIN_EXPLANATIONS) _plainExplanations.RemoveAt(0);

            return explanation;
        }

        public string GetLastPlainExplanation()
        {
            if (_plainExplanations.Count == 0) return "No decisions explained yet.";
            return _plainExplanations[_plainExplanations.Count - 1];
        }

        // ======================================================================
        // 7. ENHANCED CONFIDENCE CALIBRATION (per-context)
        // ======================================================================
        public string GetContextualCalibration(string action)
        {
            string context = _appMode.ToString();
            string key = action + "_" + context;
            CalibrationBucket bucket;
            if (_calibrationBuckets.TryGetValue(key, out bucket) && bucket.TotalSamples >= 5)
            {
                return string.Format("{0} in {1}: predicted={2:F0}% actual={3:F0}% (N={4})",
                    action, context, bucket.PredictedConfidence * 100, bucket.ActualSuccessRate * 100, bucket.TotalSamples);
            }
            return string.Format("{0} in {1}: insufficient data", action, context);
        }

        // ======================================================================
        // PERSISTENCE
        // ======================================================================
        public void SaveAdvancedFeaturesData()
        {
            try
            {
                StringBuilder sb = new StringBuilder();

                sb.AppendLine("===DYNAMIC_THRESHOLDS===");
                sb.AppendLine(string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}",
                    _dynamicThresholds.ThermalWarning, _dynamicThresholds.ThermalThrottle,
                    _dynamicThresholds.RamCleanPercent, _dynamicThresholds.RamAggressivePercent,
                    _dynamicThresholds.MaxProcessCount, _dynamicThresholds.CpuHighPercent,
                    _dynamicThresholds.FpsLowThreshold));

                sb.AppendLine("===TEMP_PROFILE===");
                sb.AppendLine(string.Format("{0}|{1}|{2}|{3}|{4}|{5}",
                    _tempProfile.IdleTempAvg, _tempProfile.IdleTempStdDev,
                    _tempProfile.LoadTempAvg, _tempProfile.LoadTempStdDev,
                    _tempProfile.IdleSamples, _tempProfile.LoadSamples));

                sb.AppendLine("===COST_BENEFIT===");
                foreach (var kv in _costBenefitData)
                {
                    var a = kv.Value;
                    sb.AppendLine(string.Format("{0}|{1}|{2}|{3}|{4}|{5}",
                        a.Action, a.TimesExecuted, a.TotalResourceCost, a.TotalBenefit,
                        a.AvgUserSatisfaction, a.RoiScore));
                }

                sb.AppendLine("===KNOWN_APPS===");
                foreach (var kv in _knownApps)
                {
                    var a = kv.Value;
                    if (a.TotalRuns >= 2)
                        sb.AppendLine(string.Format("{0}|{1}|{2}|{3}|{4}|{5}",
                            a.ProcessName, a.Category, a.TotalRuns, a.AvgCpuCost, a.IsTrusted, a.FirstSeen));
                }

                sb.AppendLine("===PRODUCTIVITY===");
                for (int i = 0; i < 24; i++)
                {
                    var pb = _productivityHours[i];
                    sb.AppendLine(string.Format("{0}|{1}|{2}|{3}|{4}",
                        i, pb.ProductivityScore, pb.GamingScore, pb.IdleScore, pb.TotalSamples));
                }

                string path = Path.Combine(_dataDir, "advanced_features.dat");
                File.WriteAllText(path, sb.ToString());
            }
            catch (Exception ex) { LogError("SaveAdvancedFeaturesData", ex); }
        }

        public void LoadAdvancedFeaturesData()
        {
            try
            {
                string path = Path.Combine(_dataDir, "advanced_features.dat");
                if (!File.Exists(path)) return;
                string[] lines = File.ReadAllLines(path);
                string section = "";
                foreach (string rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (line.StartsWith("==="))
                    {
                        section = line;
                        continue;
                    }
                    if (string.IsNullOrEmpty(line)) continue;

                    if (section == "===DYNAMIC_THRESHOLDS===")
                    {
                        string[] p = line.Split('|');
                        if (p.Length >= 7)
                        {
                            double.TryParse(p[0], out _dynamicThresholds.ThermalWarning);
                            double.TryParse(p[1], out _dynamicThresholds.ThermalThrottle);
                            double.TryParse(p[2], out _dynamicThresholds.RamCleanPercent);
                            double.TryParse(p[3], out _dynamicThresholds.RamAggressivePercent);
                            int.TryParse(p[4], out _dynamicThresholds.MaxProcessCount);
                            double.TryParse(p[5], out _dynamicThresholds.CpuHighPercent);
                            double.TryParse(p[6], out _dynamicThresholds.FpsLowThreshold);
                        }
                    }
                    else if (section == "===TEMP_PROFILE===")
                    {
                        string[] p = line.Split('|');
                        if (p.Length >= 6)
                        {
                            double.TryParse(p[0], out _tempProfile.IdleTempAvg);
                            double.TryParse(p[1], out _tempProfile.IdleTempStdDev);
                            double.TryParse(p[2], out _tempProfile.LoadTempAvg);
                            double.TryParse(p[3], out _tempProfile.LoadTempStdDev);
                            int.TryParse(p[4], out _tempProfile.IdleSamples);
                            int.TryParse(p[5], out _tempProfile.LoadSamples);
                            _tempProfile.NaturalBaseline = _tempProfile.IdleTempAvg;
                        }
                    }
                    else if (section == "===COST_BENEFIT===")
                    {
                        string[] p = line.Split('|');
                        if (p.Length >= 6)
                        {
                            var acb = new ActionCostBenefit();
                            acb.Action = p[0];
                            int.TryParse(p[1], out acb.TimesExecuted);
                            double.TryParse(p[2], out acb.TotalResourceCost);
                            double.TryParse(p[3], out acb.TotalBenefit);
                            double.TryParse(p[4], out acb.AvgUserSatisfaction);
                            double.TryParse(p[5], out acb.RoiScore);
                            if (acb.TimesExecuted > 0)
                            {
                                acb.AvgCostPerExecution = acb.TotalResourceCost / acb.TimesExecuted;
                                acb.AvgBenefitPerExecution = acb.TotalBenefit / acb.TimesExecuted;
                            }
                            _costBenefitData[acb.Action] = acb;
                        }
                    }
                    else if (section == "===KNOWN_APPS===")
                    {
                        string[] p = line.Split('|');
                        if (p.Length >= 6)
                        {
                            var ka = new KnownApplication();
                            ka.ProcessName = p[0];
                            ka.Category = p[1];
                            int.TryParse(p[2], out ka.TotalRuns);
                            double.TryParse(p[3], out ka.AvgCpuCost);
                            bool.TryParse(p[4], out ka.IsTrusted);
                            ka.FirstSeen = p[5];
                            ka.IsNew = false;
                            _knownApps[ka.ProcessName] = ka;
                        }
                    }
                    else if (section == "===PRODUCTIVITY===")
                    {
                        string[] p = line.Split('|');
                        if (p.Length >= 5)
                        {
                            int h = int.Parse(p[0]);
                            if (h >= 0 && h < 24)
                            {
                                double.TryParse(p[1], out _productivityHours[h].ProductivityScore);
                                double.TryParse(p[2], out _productivityHours[h].GamingScore);
                                double.TryParse(p[3], out _productivityHours[h].IdleScore);
                                int.TryParse(p[4], out _productivityHours[h].TotalSamples);
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { LogError("LoadAdvancedFeaturesData", ex); }
        }

        private void InitTrustedApps()
        {
            string[] trusted = new[] {
                "explorer", "csrss", "smss", "wininit", "winlogon", "services", "lsass", "svchost",
                "dwm", "runtimebroker", "searchapp", "shellexperiencehost", "systemsettings",
                "lockapp", "widgets", "textinputhost", "sihost", "taskhostw", "dllhost", "conhost",
                "fontdrvhost", "ctfmon", "sppsrv", "wlms", "securityhealth", "audiodg", "spoolsv",
                "clearwin", "taskmgr", "perfmon", "resmon"
            };
            foreach (string t in trusted) _trustedApps.Add(t);
        }

        private void LoadDynamicThresholds()
        {
            _dynamicThresholds = new DynamicThresholdSet
            {
                ThermalWarning = 82,
                ThermalThrottle = 92,
                RamCleanPercent = 80,
                RamAggressivePercent = 88,
                MaxProcessCount = 180,
                CpuHighPercent = 85,
                FpsLowThreshold = 30
            };
        }

        private void LoadTemperatureProfile() { _tempProfile = new DeviceTemperatureProfile(); }

        private void LoadCostBenefitData() { _costBenefitData = new Dictionary<string, ActionCostBenefit>(); }

        private void LoadKnownApps() { _knownApps = new Dictionary<string, KnownApplication>(StringComparer.OrdinalIgnoreCase); }
    }
}
