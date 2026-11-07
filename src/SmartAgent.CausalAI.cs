using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ClearWin
{
    public partial class SmartAgent
    {
        private List<CausalChain> _causalChains = new List<CausalChain>();
        private DateTime _lastCausalAnalysis = DateTime.MinValue;
        private const int CAUSAL_INTERVAL_SECONDS = 15;
        private List<CausalNode> _knownCauses = new List<CausalNode>();
        private int _causalAnalysisCount = 0;

        // CPU telemetry for root-cause detection: a process is only blamed when
        // its measured CPU% (from TotalProcessorTime deltas) actually exceeds the
        // threshold, so a crowded process list can no longer false-trigger.
        private Dictionary<int, double> _procCpuPrev = new Dictionary<int, double>();
        private DateTime _procCpuPrevTime = DateTime.MinValue;
        private const double RUNAWAY_CPU_THRESHOLD = 30.0;
        private const double BACKGROUND_CPU_THRESHOLD = 15.0;
        private const int BACKGROUND_CPU_MIN_COUNT = 3;

        // === NEW: Causal Learning ===
        private Dictionary<string, CausalRule> _learnedCauses = new Dictionary<string, CausalRule>();
        private DateTime _lastCausalLearning = DateTime.MinValue;
        private const int CAUSAL_LEARNING_INTERVAL_SECONDS = 30;
        private const int MAX_CAUSAL_RULES = 500;
        private const int CAUSAL_HISTORY_SIZE = 100;
        private List<CausalObservation> _causalObservations = new List<CausalObservation>();

        // === NEW: Bayesian Confidence Tracking ===
        private Dictionary<string, double> _causeConfidence = new Dictionary<string, double>();
        private Dictionary<string, int> _causeCorrectCount = new Dictionary<string, int>();
        private Dictionary<string, int> _causeTotalCount = new Dictionary<string, int>();

        public List<CausalChain> GetCausalChains() { return new List<CausalChain>(_causalChains); }
        public CausalChain GetLastCausalChain()
        {
            return _causalChains.Count > 0 ? _causalChains[_causalChains.Count - 1] : null;
        }

        public void RunCausalAnalysis()
        {
            if ((DateTime.Now - _lastCausalAnalysis).TotalSeconds < CAUSAL_INTERVAL_SECONDS) return;
            _causalAnalysisCount++;

            // Sample CPU deltas ONCE per analysis so every detector reads the same
            // measured picture instead of each counting processes blindly.
            Process[] cachedProcs;
            lock (_processCacheLock) { cachedProcs = _cachedProcesses; }
            var cpuMap = MeasureProcessCpuPercent(cachedProcs);

            // === EXISTING: Rule-based detection ===
            if (_smoothCpu > 70)
            {
                var chain = BuildCausalChain("High CPU", _smoothCpu, "CPU", cpuMap);
                if (chain != null) _causalChains.Add(chain);
            }

            if (_smoothFps < 45 && _smoothFps > 0)
            {
                var chain = BuildCausalChain("Low FPS", _smoothFps, "GPU", cpuMap);
                if (chain != null) _causalChains.Add(chain);
            }

            if (_smoothTemp > 70)
            {
                var chain = BuildCausalChain("High Temperature", _smoothTemp, "Thermal", cpuMap);
                if (chain != null) _causalChains.Add(chain);
            }

            if (_smoothRam > 80)
            {
                var chain = BuildCausalChain("Memory Pressure", _smoothRam, "Memory", cpuMap);
                if (chain != null) _causalChains.Add(chain);
            }

            // === NEW: Learned cause detection ===
            DetectLearnedCauses();

            // === NEW: Correlation-based discovery ===
            DiscoverCausalCorrelations();

            while (_causalChains.Count > 50) _causalChains.RemoveAt(0);

            _lastCausalAnalysis = DateTime.Now;
        }

        private CausalChain BuildCausalChain(string symptom, double severity, string category, Dictionary<int, double> cpuMap)
        {
            var chain = new CausalChain();
            chain.Timestamp = DateTime.Now;
            chain.Symptom = symptom;
            chain.Severity = severity;
            chain.Category = category;
            chain.Nodes = new List<CausalNode>();
            chain.Confidence = 0.5;

            var root = new CausalNode();
            root.Label = symptom;
            root.Value = string.Format("{0:F1}", severity);
            root.Type = "Symptom";
            root.Children = new List<CausalNode>();

            if (category == "CPU")
            {
                var defenderNode = DetectDefenderScan();
                if (defenderNode != null)
                {
                    root.Children.Add(defenderNode);
                    chain.RecommendedAction = "Temporarily pause Windows Defender scan";
                    chain.Confidence = GetLearnedConfidence("DefenderScan", 0.8);
                }

                var updateNode = DetectWindowsUpdate();
                if (updateNode != null)
                {
                    root.Children.Add(updateNode);
                    chain.RecommendedAction = "Wait for Windows Update to complete";
                    chain.Confidence = GetLearnedConfidence("WindowsUpdate", 0.75);
                }

                var bgNode = DetectBackgroundCPUPressure(cpuMap);
                if (bgNode != null)
                {
                    root.Children.Add(bgNode);
                    chain.RecommendedAction = "Reduce background process priority";
                    chain.Confidence = GetLearnedConfidence("BackgroundCPU", 0.7);
                }

                var runawayNode = DetectRunawayProcess(cpuMap);
                if (runawayNode != null)
                {
                    root.Children.Add(runawayNode);
                    chain.RecommendedAction = "Investigate runaway process: " + runawayNode.Label;
                    chain.Confidence = GetLearnedConfidence("RunawayProcess", 0.85);
                }
            }
            else if (category == "GPU")
            {
                var thermalNode = new CausalNode();
                thermalNode.Label = "Temperature";
                thermalNode.Value = string.Format("{0:F0}C", _smoothTemp);
                thermalNode.Type = "Cause";
                thermalNode.Children = new List<CausalNode>();
                if (_smoothTemp > 75)
                {
                    thermalNode.Children.Add(new CausalNode { Label = "Thermal Throttling", Value = "Active", Type = "RootCause", Children = new List<CausalNode>() });
                    chain.RecommendedAction = "Reduce thermal load before addressing GPU";
                    chain.Confidence = GetLearnedConfidence("ThermalThrottle", 0.85);
                }
                root.Children.Add(thermalNode);

                var cpuNode = new CausalNode();
                cpuNode.Label = "CPU Bottleneck";
                cpuNode.Value = string.Format("{0:F0}%", _smoothCpu);
                cpuNode.Type = "Cause";
                cpuNode.Children = new List<CausalNode>();
                if (_smoothCpu > 85)
                {
                    cpuNode.Children.Add(new CausalNode { Label = "CPU-bound rendering", Value = "Detected", Type = "RootCause", Children = new List<CausalNode>() });
                    chain.RecommendedAction = "CPU is bottlenecking GPU, optimize CPU usage first";
                    chain.Confidence = GetLearnedConfidence("CPUBottleneck", 0.8);
                }
                root.Children.Add(cpuNode);
            }
            else if (category == "Thermal")
            {
                var cpuLoadNode = new CausalNode();
                cpuLoadNode.Label = "CPU Load";
                cpuLoadNode.Value = string.Format("{0:F0}%", _smoothCpu);
                cpuLoadNode.Type = "Cause";
                cpuLoadNode.Children = new List<CausalNode>();
                root.Children.Add(cpuLoadNode);

                var gpuLoadNode = new CausalNode();
                gpuLoadNode.Label = "GPU Load";
                gpuLoadNode.Value = string.Format("{0:F0}%", _smoothGpu);
                gpuLoadNode.Type = "Cause";
                gpuLoadNode.Children = new List<CausalNode>();
                root.Children.Add(gpuLoadNode);

                if (_smoothCpu > 80 && _smoothGpu > 80)
                {
                    chain.RecommendedAction = "Both CPU and GPU are loading system heavily, reduce one";
                    chain.Confidence = GetLearnedConfidence("DualLoad", 0.9);
                }
                else if (_smoothCpu > 80)
                {
                    chain.RecommendedAction = "CPU load is the primary heat source";
                    chain.Confidence = GetLearnedConfidence("CPUHeat", 0.85);
                }
                else if (_smoothGpu > 80)
                {
                    chain.RecommendedAction = "GPU load is the primary heat source";
                    chain.Confidence = GetLearnedConfidence("GPUHeat", 0.85);
                }
            }
            else if (category == "Memory")
            {
                var procNode = new CausalNode();
                procNode.Label = "Process Count";
                procNode.Value = _currentState.ProcessCount.ToString();
                procNode.Type = "Cause";
                procNode.Children = new List<CausalNode>();
                root.Children.Add(procNode);

                var leakNode = DetectMemoryLeak();
                if (leakNode != null)
                {
                    root.Children.Add(leakNode);
                    chain.RecommendedAction = "Possible memory leak detected in " + leakNode.Label;
                    chain.Confidence = GetLearnedConfidence("MemoryLeak", 0.75);
                }
            }

            if (string.IsNullOrEmpty(chain.RecommendedAction))
            {
                chain.RecommendedAction = "Monitor and gather more data";
                chain.Confidence = 0.4;
            }

            chain.Nodes.Add(root);
            return chain;
        }

        // ============================================================
        // === NEW: Causal Learning System ===
        // ============================================================
        private void DetectLearnedCauses()
        {
            if ((DateTime.Now - _lastCausalLearning).TotalSeconds < CAUSAL_LEARNING_INTERVAL_SECONDS) return;
            _lastCausalLearning = DateTime.Now;

            // Check each learned rule
            foreach (var kv in _learnedCauses)
            {
                CausalRule rule = kv.Value;
                if (rule.IsEnabled && EvaluateCausalRule(rule))
                {
                    var chain = new CausalChain();
                    chain.Timestamp = DateTime.Now;
                    chain.Symptom = rule.Effect;
                    chain.Severity = rule.CurrentSeverity;
                    chain.Category = rule.Category;
                    chain.Confidence = GetLearnedConfidence(rule.Id, rule.BaseConfidence);
                    chain.RecommendedAction = rule.RecommendedAction;
                    chain.Nodes = new List<CausalNode>();

                    var root = new CausalNode();
                    root.Label = rule.Effect;
                    root.Value = string.Format("{0:F1}", rule.CurrentSeverity);
                    root.Type = "LearnedEffect";
                    root.Children = new List<CausalNode>();

                    // Add cause node
                    var causeNode = new CausalNode();
                    causeNode.Label = rule.Cause;
                    causeNode.Value = string.Format("{0} > {1}", rule.Cause, rule.Threshold);
                    causeNode.Type = "LearnedCause";
                    causeNode.Children = new List<CausalNode>();
                    root.Children.Add(causeNode);

                    chain.Nodes.Add(root);
                    _causalChains.Add(chain);
                }
            }
        }

        private bool EvaluateCausalRule(CausalRule rule)
        {
            // Evaluate the condition based on current system state
            switch (rule.ConditionType)
            {
                case "CpuAbove":
                    return _smoothCpu > rule.Threshold;
                case "RamAbove":
                    return _smoothRam > rule.Threshold;
                case "TempAbove":
                    return _smoothTemp > rule.Threshold;
                case "FpsBelow":
                    return _smoothFps < rule.Threshold && _smoothFps > 0;
                case "ProcessCountAbove":
                    return _currentState.ProcessCount > (int)rule.Threshold;
                case "DiskAbove":
                    return _currentState.DiskUsage > rule.Threshold;
                case "CombinedCpuTemp":
                    return _smoothCpu > rule.Threshold && _smoothTemp > rule.SecondThreshold;
                case "CombinedRamProcess":
                    return _smoothRam > rule.Threshold && _currentState.ProcessCount > (int)rule.SecondThreshold;
                default:
                    return false;
            }
        }

        // ============================================================
        // === NEW: Learn from Causal Outcomes ===
        // ============================================================
        public void LearnCausalOutcome(string causeKey, bool wasCorrect, double actualImpact)
        {
            // Update Bayesian confidence
            int total, correct;
            _causeTotalCount.TryGetValue(causeKey, out total);
            _causeCorrectCount.TryGetValue(causeKey, out correct);

            total++;
            if (wasCorrect) correct++;

            _causeTotalCount[causeKey] = total;
            _causeCorrectCount[causeKey] = correct;

            // Bayesian update: P(cause|evidence) ∝ P(evidence|cause) * P(cause)
            double prior = 0.5;
            _causeConfidence.TryGetValue(causeKey, out prior);

            double likelihood = wasCorrect ? 0.8 : 0.2;
            double posterior = (likelihood * prior) / (likelihood * prior + (1 - likelihood) * (1 - prior));

            // Smooth update
            _causeConfidence[causeKey] = prior * 0.7 + posterior * 0.3;
        }

        private double GetLearnedConfidence(string causeKey, double baseConfidence)
        {
            double learned;
            if (_causeConfidence.TryGetValue(causeKey, out learned))
            {
                // Blend learned confidence with base
                return baseConfidence * 0.4 + learned * 0.6;
            }
            return baseConfidence;
        }

        // ============================================================
        // === NEW: Auto-discover Causal Relationships ===
        // ============================================================
        private void DiscoverCausalCorrelations()
        {
            // Track observations for pattern discovery
            var obs = new CausalObservation();
            obs.Timestamp = DateTime.Now;
            obs.Cpu = _smoothCpu;
            obs.Ram = _smoothRam;
            obs.Temp = _smoothTemp;
            obs.Fps = _smoothFps;
            obs.ProcessCount = _currentState.ProcessCount;
            obs.DiskUsage = _currentState.DiskUsage;
            _causalObservations.Add(obs);

            if (_causalObservations.Count > CAUSAL_HISTORY_SIZE)
                _causalObservations.RemoveAt(0);

            if (_causalObservations.Count < 20) return;

            // Look for patterns: "When X was high, Y followed after N seconds"
            // CAUSAL_INTERVAL_SECONDS = 15s per sample. lag 1 = 15s, lag 2 = 30s, lag 4 = 60s (1m), lag 8 = 120s (2m)
            int[] lagSteps = new int[] { 1, 2, 4, 8 };
            for (int li = 0; li < lagSteps.Length; li++)
            {
                int lag = lagSteps[li];
                CheckCausalPattern("CpuHigh", "TempRise",
                    o => o.Cpu > 70, o => o.Temp > 70, lag);
                CheckCausalPattern("RamHigh", "CpuRise",
                    o => o.Ram > 80, o => o.Cpu > 70, lag);
                CheckCausalPattern("TempHigh", "FpsDrop",
                    o => o.Temp > 75, o => o.Fps < 45, lag);
                CheckCausalPattern("ProcessCountHigh", "RamHigh",
                    o => o.ProcessCount > 150, o => o.Ram > 75, lag);
            }
        }

        private void CheckCausalPattern(string causeName, string effectName,
            Func<CausalObservation, bool> causeCondition,
            Func<CausalObservation, bool> effectCondition,
            int lagSteps)
        {
            if (_causalObservations.Count < lagSteps + 5) return;

            int causeCount = 0;
            int effectAfterCause = 0;

            for (int i = 0; i < _causalObservations.Count - lagSteps; i++)
            {
                if (causeCondition(_causalObservations[i]))
                {
                    causeCount++;
                    if (effectCondition(_causalObservations[i + lagSteps]))
                    {
                        effectAfterCause++;
                    }
                }
            }

            if (causeCount >= 3)
            {
                double probability = (double)effectAfterCause / causeCount;
                if (probability > 0.6)
                {
                    int elapsedSeconds = lagSteps * CAUSAL_INTERVAL_SECONDS;
                    string ruleId = causeName + "->" + effectName + "_Lag" + elapsedSeconds + "s";
                    if (!_learnedCauses.ContainsKey(ruleId) && _learnedCauses.Count < MAX_CAUSAL_RULES)
                    {
                        var rule = new CausalRule();
                        rule.Id = ruleId;
                        rule.Cause = causeName;
                        rule.Effect = effectName;
                        rule.Category = "CorrelationObserved";
                        rule.ConditionType = GetConditionType(causeName);
                        rule.Threshold = GetThreshold(causeName);
                        rule.BaseConfidence = probability;
                        rule.RecommendedAction = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            "Observed correlation: {0} followed by {1} after ~{2}s (requires confounder verification)",
                            causeName, effectName, elapsedSeconds);
                        rule.IsEnabled = true;
                        rule.DiscoveredAt = DateTime.Now;
                        rule.ObservationCount = causeCount;
                        _learnedCauses[ruleId] = rule;

                        AddLog("CAUSAL", "Correlation",
                            string.Format(System.Globalization.CultureInfo.InvariantCulture,
                            "Observed lag pattern: {0} -> {1} (lag: {2}s, empirical freq: {3:F0}%, n={4})",
                            causeName, effectName, elapsedSeconds, probability * 100, causeCount));
                    }
                }
            }
        }

        private string GetConditionType(string causeName)
        {
            switch (causeName)
            {
                case "CpuHigh": return "CpuAbove";
                case "RamHigh": return "RamAbove";
                case "TempHigh": return "TempAbove";
                case "ProcessCountHigh": return "ProcessCountAbove";
                default: return "CpuAbove";
            }
        }

        private double GetThreshold(string causeName)
        {
            switch (causeName)
            {
                case "CpuHigh": return 70;
                case "RamHigh": return 80;
                case "TempHigh": return 75;
                case "ProcessCountHigh": return 150;
                default: return 70;
            }
        }

        // ============================================================
        // === EXISTING: Detection Methods (unchanged) ===
        // ============================================================
        private CausalNode DetectDefenderScan()
        {
            try
            {
                var procs = Process.GetProcessesByName("MsMpEng");
                if (procs.Length > 0)
                {
                    var node = new CausalNode();
                    node.Label = "Windows Defender Scan";
                    node.Value = "Running";
                    node.Type = "RootCause";
                    node.Children = new List<CausalNode>();
                    node.Children.Add(new CausalNode { Label = "Impact", Value = "High CPU usage", Type = "Effect", Children = new List<CausalNode>() });
                    return node;
                }
            }
            catch (Exception ex) { LogError("DetectDefenderScan", ex); }
            return null;
        }

        private CausalNode DetectWindowsUpdate()
        {
            try
            {
                var procs = Process.GetProcessesByName("TrustedInstaller");
                if (procs.Length > 0)
                {
                    var node = new CausalNode();
                    node.Label = "Windows Update";
                    node.Value = "Installing";
                    node.Type = "RootCause";
                    node.Children = new List<CausalNode>();
                    node.Children.Add(new CausalNode { Label = "Impact", Value = "High CPU + Disk usage", Type = "Effect", Children = new List<CausalNode>() });
                    return node;
                }
                var bits = Process.GetProcessesByName("bits");
                if (bits.Length > 0)
                {
                    var node = new CausalNode();
                    node.Label = "Windows Update (BITS)";
                    node.Value = "Downloading";
                    node.Type = "RootCause";
                    node.Children = new List<CausalNode>();
                    return node;
                }
            }
            catch (Exception ex) { LogError("DetectWindowsUpdate", ex); }
            return null;
        }

        private CausalNode DetectBackgroundCPUPressure(Dictionary<int, double> cpuMap)
        {
            try
            {
                Process[] procs;
                lock (_processCacheLock) { procs = _cachedProcesses; }
                if (procs == null || procs.Length == 0) return null;
                int highCpuCount = 0;
                string topProcess = "";
                double topCpu = 0;
                for (int i = 0; i < procs.Length; i++)
                {
                    try
                    {
                        Process p = procs[i];
                        if (p.HasExited) continue;
                        double cpu;
                        if (!cpuMap.TryGetValue(p.Id, out cpu)) continue;
                        if (cpu < BACKGROUND_CPU_THRESHOLD) continue;
                        if (IsSystemProcess(p.ProcessName)) continue;
                        highCpuCount++;
                        if (cpu > topCpu) { topCpu = cpu; topProcess = p.ProcessName; }
                    }
                    catch (Exception ex) { LogError("DetectBackgroundCPUPressure", ex); }
                }
                if (highCpuCount >= BACKGROUND_CPU_MIN_COUNT)
                {
                    var node = new CausalNode();
                    node.Label = "Multiple Background Processes";
                    node.Value = highCpuCount + " high-CPU processes (top: " + topProcess + " @ " + topCpu.ToString("F0") + "%)";
                    node.Type = "Cause";
                    node.Children = new List<CausalNode>();
                    return node;
                }
            }
            catch (Exception ex) { LogError("DetectBackgroundCPUPressure", ex); }
            return null;
        }

        private CausalNode DetectRunawayProcess(Dictionary<int, double> cpuMap)
        {
            try
            {
                Process[] procs;
                lock (_processCacheLock) { procs = _cachedProcesses; }
                if (procs == null || procs.Length == 0) return null;
                string best = null;
                double bestCpu = 0;
                for (int i = 0; i < procs.Length; i++)
                {
                    try
                    {
                        Process p = procs[i];
                        if (p.HasExited) continue;
                        double cpu;
                        if (!cpuMap.TryGetValue(p.Id, out cpu)) continue;
                        if (cpu < RUNAWAY_CPU_THRESHOLD) continue;
                        if (IsSystemProcess(p.ProcessName)) continue;
                        if (cpu > bestCpu) { bestCpu = cpu; best = p.ProcessName; }
                    }
                    catch (Exception ex) { LogError("DetectRunawayProcess", ex); }
                }
                if (best == null) return null;
                var node = new CausalNode();
                node.Label = best;
                node.Value = "Runaway process @ " + bestCpu.ToString("F0") + "% CPU";
                node.Type = "RootCause";
                node.Children = new List<CausalNode>();
                return node;
            }
            catch (Exception ex) { LogError("DetectRunawayProcess", ex); }
            return null;
        }

        // Measures per-process CPU% from TotalProcessorTime deltas between causal
        // analysis passes. First pass only records a baseline and returns an empty
        // map, so a fresh analysis never false-triggers on a busy process list.
        private Dictionary<int, double> MeasureProcessCpuPercent(Process[] procs)
        {
            var result = new Dictionary<int, double>();
            try
            {
                DateTime now = DateTime.Now;
                double elapsedSec = _procCpuPrevTime == DateTime.MinValue
                    ? 0
                    : Math.Max(0.001, (now - _procCpuPrevTime).TotalSeconds);
                var cur = new Dictionary<int, double>();
                if (procs != null)
                {
                    for (int i = 0; i < procs.Length; i++)
                    {
                        try
                        {
                            Process p = procs[i];
                            if (p.HasExited) continue;
                            int pid = p.Id;
                            double ticks = p.TotalProcessorTime.Ticks;
                            cur[pid] = ticks;
                            double prev;
                            if (elapsedSec > 0 && _procCpuPrev.TryGetValue(pid, out prev) && ticks >= prev)
                            {
                                double cpu = (ticks - prev) / TimeSpan.TicksPerSecond / elapsedSec / Environment.ProcessorCount * 100.0;
                                result[pid] = cpu;
                            }
                        }
                        catch (Exception ex) { LogError("MeasureProcessCpuPercent", ex); }
                    }
                }
                _procCpuPrev = cur;
                _procCpuPrevTime = now;
            }
            catch (Exception ex) { LogError("MeasureProcessCpuPercent", ex); }
            return result;
        }

        private CausalNode DetectMemoryLeak()
        {
            double ramRisePerTick = _smoothRam - _prevState.RamUsage;
            if (ramRisePerTick > 0.5 && _smoothRam > 75)
            {
                var node = new CausalNode();
                node.Label = "Memory Leak Suspect";
                node.Value = string.Format("RAM rising at {0:F1}%/cycle", ramRisePerTick);
                node.Type = "SuspectedCause";
                node.Children = new List<CausalNode>();
                return node;
            }
            return null;
        }

        private bool IsSystemProcess(string name)
        {
            return SYSTEM_PROCESSES.Contains(name.ToLower());
        }

        public string GetCausalSummary()
        {
            if (_causalChains.Count == 0) return "No causal analysis yet";
            var last = _causalChains[_causalChains.Count - 1];
            return string.Format("Last: {0} | Cause: {1} | Action: {2} | Confidence: {3:F0}%",
                last.Symptom, last.Category, last.RecommendedAction, last.Confidence * 100);
        }

        // === NEW: Get learned rules for UI ===
        public List<CausalRule> GetLearnedCausalRules()
        {
            return new List<CausalRule>(_learnedCauses.Values);
        }

        public int GetCausalRuleCount()
        {
            return _learnedCauses.Count;
        }
    }
}
