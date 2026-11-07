using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ClearWin
{
    public partial class SmartAgent
    {
        private WorldModel _worldModel;
        private DateTime _lastWorldModelUpdate = DateTime.MinValue;
        private const int WORLD_MODEL_INTERVAL_SECONDS = 30;
        private int _worldModelUpdateCount = 0;

        // === NEW: Process Learning ===
        private Dictionary<string, ProcessProfile> _processProfiles = new Dictionary<string, ProcessProfile>();
        private DateTime _lastProcessLearning = DateTime.MinValue;
        private const int PROCESS_LEARNING_INTERVAL_SECONDS = 10;
        private const int MAX_TRACKED_PROCESSES = 200;
        private const int PROCESS_HISTORY_SIZE = 60;

        // === NEW: Dependency Learning ===
        private Dictionary<string, Dictionary<string, double>> _correlationMatrix = new Dictionary<string, Dictionary<string, double>>();
        private DateTime _lastCorrelationUpdate = DateTime.MinValue;
        private const int CORRELATION_INTERVAL_SECONDS = 60;

        public WorldModel GetWorldModel() { return _worldModel; }
        public int GetWorldModelNodeCount()
        {
            return _worldModel != null && _worldModel.Nodes != null ? _worldModel.Nodes.Count : 0;
        }
        public int GetWorldModelEdgeCount()
        {
            return _worldModel != null && _worldModel.Edges != null ? _worldModel.Edges.Count : 0;
        }

        public void InitializeWorldModel()
        {
            _worldModel = new WorldModel();
            _worldModel.LastUpdated = DateTime.Now;
            _worldModel.Nodes = new List<WorldNode>();
            _worldModel.Edges = new List<WorldEdge>();
            _worldModel.ServiceMap = new Dictionary<string, WorldNode>();

            AddWorldNode("CPU", "Hardware", "Central Processing Unit");
            AddWorldNode("RAM", "Hardware", "System Memory");
            AddWorldNode("GPU", "Hardware", "Graphics Processing Unit");
            AddWorldNode("Disk", "Hardware", "Storage Device");
            AddWorldNode("Network", "Hardware", "Network Interface");
            AddWorldNode("Power", "Hardware", "Power Subsystem");

            AddWorldNode("explorer", "Service", "Windows Explorer Shell");
            AddWorldNode("csrss", "Service", "Client Server Runtime");
            AddWorldNode("svchost", "Service", "Service Host Process");
            AddWorldNode("dwm", "Service", "Desktop Window Manager");
            AddWorldNode("lsass", "Service", "Local Security Authority");
            AddWorldNode("services", "Service", "Service Control Manager");
            AddWorldNode("winlogon", "Service", "Windows Logon");
            AddWorldNode("spoolsv", "Service", "Print Spooler");
            AddWorldNode("audiodg", "Service", "Windows Audio");
            AddWorldNode("SearchIndexer", "Service", "Windows Search");

            AddWorldEdge("explorer", "dwm", "DependsOn", 0.9);
            AddWorldEdge("explorer", "csrss", "DependsOn", 0.8);
            AddWorldEdge("svchost", "services", "ManagedBy", 1.0);
            AddWorldEdge("dwm", "GPU", "UsesResource", 0.7);
            AddWorldEdge("csrss", "CPU", "UsesResource", 0.6);
            AddWorldEdge("lsass", "RAM", "UsesResource", 0.3);
            AddWorldEdge("spoolsv", "CPU", "UsesResource", 0.2);
            AddWorldEdge("audiodg", "CPU", "UsesResource", 0.15);
            AddWorldEdge("SearchIndexer", "Disk", "UsesResource", 0.5);
            AddWorldEdge("SearchIndexer", "CPU", "UsesResource", 0.3);

            AddLog("WORLD", "WorldModel", "Initialized with " + _worldModel.Nodes.Count + " nodes and " + _worldModel.Edges.Count + " edges");
        }

        private void AddWorldNode(string name, string type, string description)
        {
            var node = new WorldNode();
            node.Name = name;
            node.Type = type;
            node.Description = description;
            node.CurrentLoad = 0;
            node.Health = 100;
            node.LastSeen = DateTime.Now;
            _worldModel.Nodes.Add(node);
            _worldModel.ServiceMap[name] = node;
        }

        private void AddWorldEdge(string from, string to, string relationship, double weight)
        {
            var edge = new WorldEdge();
            edge.From = from;
            edge.To = to;
            edge.Relationship = relationship;
            edge.Weight = weight;
            edge.Impact = 0;
            _worldModel.Edges.Add(edge);
        }

        public void UpdateWorldModel()
        {
            if ((DateTime.Now - _lastWorldModelUpdate).TotalSeconds < WORLD_MODEL_INTERVAL_SECONDS) return;
            _worldModelUpdateCount++;

            UpdateNodeLoad("CPU", _smoothCpu);
            UpdateNodeLoad("RAM", _smoothRam);
            UpdateNodeLoad("GPU", _smoothGpu);
            UpdateNodeLoad("Disk", _currentState.DiskUsage);
            UpdateNodeLoad("Power", _isOnBattery ? (100 - _currentState.BatteryLevel) : 0);

            UpdateServiceHealth();
            DetectAndModelProcesses();
            CalculateEdgeImpacts();
            DetectDependencyIssues();

            // === NEW: Learn process patterns ===
            LearnProcessResourceUsage();

            // === NEW: Update correlations ===
            UpdateCorrelationMatrix();

            _worldModel.LastUpdated = DateTime.Now;
            _lastWorldModelUpdate = DateTime.Now;

            if (_worldModelUpdateCount % 5 == 0)
            {
                AddLog("WORLD", "WorldModel",
                    string.Format("Model updated: {0} nodes, {1} edges, {2} profiles, Health={3:F0}",
                    _worldModel.Nodes.Count, _worldModel.Edges.Count, _processProfiles.Count, GetWorldModelHealth()));
            }
        }

        private void UpdateNodeLoad(string name, double load)
        {
            WorldNode node;
            if (_worldModel.ServiceMap.TryGetValue(name, out node))
            {
                node.CurrentLoad = load;
                node.Health = Math.Max(0, 100 - load);
                node.LastSeen = DateTime.Now;
            }
        }

        private void UpdateServiceHealth()
        {
            try
            {
                Process[] procs = GetCachedProcesses();
                var activeProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < procs.Length; i++)
                {
                    try
                    {
                        string pname = procs[i].ProcessName.ToLower();
                        activeProcesses.Add(pname);
                        WorldNode node;
                        if (_worldModel.ServiceMap.TryGetValue(pname, out node))
                        {
                            node.LastSeen = DateTime.Now;
                            node.Health = 100;
                        }
                    }
                    catch (Exception ex) { LogError("UpdateServiceHealth", ex); }
                }

                foreach (var node in _worldModel.Nodes)
                {
                    if (node.Type == "Service")
                    {
                        if (!activeProcesses.Contains(node.Name.ToLower()))
                        {
                            node.Health = 0;
                            node.CurrentLoad = 0;
                        }
                    }
                }
            }
            catch (Exception ex) { LogError("UpdateServiceHealth", ex); }
        }

        private void DetectAndModelProcesses()
        {
            try
            {
                Process[] procs = GetCachedProcesses();
                for (int i = 0; i < procs.Length; i++)
                {
                    try
                    {
                        string name = procs[i].ProcessName;
                        WorldNode existing;
                        if (!_worldModel.ServiceMap.TryGetValue(name, out existing))
                        {
                            var node = new WorldNode();
                            node.Name = name;
                            node.Type = "Process";
                            node.Description = "Discovered process";
                            node.CurrentLoad = 0;
                            node.Health = 100;
                            node.LastSeen = DateTime.Now;
                            _worldModel.Nodes.Add(node);
                            _worldModel.ServiceMap[name] = node;
                        }
                    }
                    catch (Exception ex) { LogError("DetectAndModelProcesses", ex); }
                }

                var toRemove = new List<WorldNode>();
                foreach (var node in _worldModel.Nodes)
                {
                    if (node.Type == "Process" && (DateTime.Now - node.LastSeen).TotalMinutes > 5)
                        toRemove.Add(node);
                }
                foreach (var node in toRemove)
                {
                    _worldModel.Nodes.Remove(node);
                    _worldModel.ServiceMap.Remove(node.Name);
                }
            }
            catch (Exception ex) { LogError("DetectAndModelProcesses", ex); }
        }

        private void CalculateEdgeImpacts()
        {
            foreach (var edge in _worldModel.Edges)
            {
                WorldNode fromNode;
                WorldNode toNode;
                if (_worldModel.ServiceMap.TryGetValue(edge.From, out fromNode) &&
                    _worldModel.ServiceMap.TryGetValue(edge.To, out toNode))
                {
                    edge.Impact = fromNode.CurrentLoad * edge.Weight;
                }
            }
        }

        private void DetectDependencyIssues()
        {
            foreach (var edge in _worldModel.Edges)
            {
                if (edge.Relationship == "DependsOn" && edge.Weight > 0.7)
                {
                    WorldNode toNode;
                    if (_worldModel.ServiceMap.TryGetValue(edge.To, out toNode) && toNode.Health < 30)
                    {
                        WorldNode fromNode;
                        if (_worldModel.ServiceMap.TryGetValue(edge.From, out fromNode))
                        {
                            fromNode.Health = Math.Min(fromNode.Health, toNode.Health + 20);
                        }
                    }
                }
            }
        }

        // ============================================================
        // === NEW: Process Resource Learning ===
        // ============================================================
        private void LearnProcessResourceUsage()
        {
            if ((DateTime.Now - _lastProcessLearning).TotalSeconds < PROCESS_LEARNING_INTERVAL_SECONDS) return;
            _lastProcessLearning = DateTime.Now;

            try
            {
                Process[] procs = GetCachedProcesses();
                for (int i = 0; i < procs.Length; i++)
                {
                    try
                    {
                        if (procs[i].HasExited) continue;
                        string name = procs[i].ProcessName;

                        ProcessProfile profile;
                        if (!_processProfiles.TryGetValue(name, out profile))
                        {
                            if (_processProfiles.Count >= MAX_TRACKED_PROCESSES) continue;
                            profile = new ProcessProfile();
                            profile.Name = name;
                            profile.CpuHistory = new double[PROCESS_HISTORY_SIZE];
                            profile.RamHistory = new double[PROCESS_HISTORY_SIZE];
                            profile.FirstSeen = DateTime.Now;
                            profile.SampleCount = 0;
                            profile.HistoryIndex = 0;
                            _processProfiles[name] = profile;
                        }

                        // Sample CPU and RAM (using working set as RAM approximation)
                        double cpuSample = GetProcessCpu(procs[i]);
                        double ramMB = procs[i].WorkingSet64 / (1024.0 * 1024.0);

                        profile.CpuHistory[profile.HistoryIndex] = cpuSample;
                        profile.RamHistory[profile.HistoryIndex] = ramMB;
                        profile.HistoryIndex = (profile.HistoryIndex + 1) % PROCESS_HISTORY_SIZE;
                        if (profile.SampleCount < PROCESS_HISTORY_SIZE) profile.SampleCount++;
                        profile.LastSeen = DateTime.Now;
                        profile.TotalCpuSamples += cpuSample;
                        profile.TotalRamSamples += ramMB;
                        profile.TotalSamples++;

                        // Update averages
                        profile.AvgCpu = profile.TotalCpuSamples / profile.TotalSamples;
                        profile.AvgRamMB = profile.TotalRamSamples / profile.TotalSamples;

                        // Update peak values
                        if (cpuSample > profile.PeakCpu) profile.PeakCpu = cpuSample;
                        if (ramMB > profile.PeakRamMB) profile.PeakRamMB = ramMB;

                        // Update world model node
                        WorldNode node;
                        if (_worldModel.ServiceMap.TryGetValue(name, out node))
                        {
                            node.CurrentLoad = cpuSample;
                            node.Description = string.Format("CPU:{0:F1}% RAM:{1:F0}MB (avg:{2:F1}%/{3:F0}MB)",
                                cpuSample, ramMB, profile.AvgCpu, profile.AvgRamMB);
                        }
                    }
                    catch (Exception ex) { LogError("LearnProcessResourceUsage", ex); }
                }

                // Cleanup old profiles
                var oldProfiles = new List<string>();
                foreach (var kv in _processProfiles)
                {
                    if ((DateTime.Now - kv.Value.LastSeen).TotalMinutes > 10)
                        oldProfiles.Add(kv.Key);
                }
                foreach (string key in oldProfiles)
                {
                    _processProfiles.Remove(key);
                }
            }
            catch (Exception ex) { LogError("LearnProcessResourceUsage", ex); }
        }

        private double GetProcessCpu(Process proc)
        {
            try
            {
                if (proc.HasExited) return 0;
                double uptimeMs = (DateTime.Now - proc.StartTime).TotalMilliseconds;
                if (uptimeMs <= 0) return 0;
                return proc.TotalProcessorTime.TotalMilliseconds / uptimeMs * 100.0 / Environment.ProcessorCount;
            }
            catch { return 0; }
        }

        // ============================================================
        // === NEW: Correlation Learning ===
        // ============================================================
        private void UpdateCorrelationMatrix()
        {
            if ((DateTime.Now - _lastCorrelationUpdate).TotalSeconds < CORRELATION_INTERVAL_SECONDS) return;
            _lastCorrelationUpdate = DateTime.Now;

            // Track correlations between system metrics
            string[] metrics = { "CPU", "RAM", "GPU", "Temp", "FPS", "Disk" };
            double[] values = { _smoothCpu, _smoothRam, _smoothGpu, _smoothTemp, _smoothFps, _currentState.DiskUsage };

            for (int i = 0; i < metrics.Length; i++)
            {
                if (!_correlationMatrix.ContainsKey(metrics[i]))
                    _correlationMatrix[metrics[i]] = new Dictionary<string, double>();

                for (int j = 0; j < metrics.Length; j++)
                {
                    if (i == j) continue;
                    // Simple correlation: if both move in same direction, positive correlation
                    double correlation = CalculateMetricCorrelation(metrics[i], metrics[j]);
                    _correlationMatrix[metrics[i]][metrics[j]] = correlation;
                }
            }

            // Auto-discover edges from strong correlations
            DiscoverCorrelationEdges();
        }

        private double CalculateMetricCorrelation(string metric1, string metric2)
        {
            // Simplified correlation using recent history
            double[] history1 = GetMetricHistory(metric1);
            double[] history2 = GetMetricHistory(metric2);
            if (history1 == null || history2 == null) return 0;

            int n = Math.Min(history1.Length, history2.Length);
            if (n < 5) return 0;

            double sum1 = 0, sum2 = 0, sum12 = 0, sum1sq = 0, sum2sq = 0;
            for (int i = 0; i < n; i++)
            {
                sum1 += history1[i];
                sum2 += history2[i];
                sum12 += history1[i] * history2[i];
                sum1sq += history1[i] * history1[i];
                sum2sq += history2[i] * history2[i];
            }
            double denom = Math.Sqrt((n * sum1sq - sum1 * sum1) * (n * sum2sq - sum2 * sum2));
            if (Math.Abs(denom) < 0.001) return 0;
            return (n * sum12 - sum1 * sum2) / denom;
        }

        private double[] GetMetricHistory(string metric)
        {
            switch (metric)
            {
                case "CPU": return _forecastCpuHistory;
                case "RAM": return _forecastRamHistory;
                case "Temp": return _forecastTempHistory;
                case "Disk": return _forecastDiskHistory;
                case "GPU": return _anomalyGpuHistory;
                case "FPS": return _anomalyFpsHistory;
                default: return null;
            }
        }

        private void DiscoverCorrelationEdges()
        {
            foreach (var kv in _correlationMatrix)
            {
                string from = kv.Key;
                foreach (var kv2 in kv.Value)
                {
                    string to = kv2.Key;
                    double corr = kv2.Value;

                    // If strong correlation (>0.7), check if edge already exists
                    if (Math.Abs(corr) > 0.7)
                    {
                        bool edgeExists = false;
                        foreach (var edge in _worldModel.Edges)
                        {
                            if ((edge.From == from && edge.To == to) || (edge.From == to && edge.To == from))
                            {
                                edgeExists = true;
                                // Update weight based on correlation
                                edge.Weight = Math.Abs(corr);
                                break;
                            }
                        }

                        if (!edgeExists)
                        {
                            string relationship = corr > 0 ? "PositiveCorrelation" : "NegativeCorrelation";
                            AddWorldEdge(from, to, relationship, Math.Abs(corr));
                        }
                    }
                }
            }
        }

        // ============================================================
        // === NEW: Process Impact Prediction ===
        // ============================================================
        public ProcessImpact PredictProcessImpact(string processName)
        {
            ProcessProfile profile;
            if (!_processProfiles.TryGetValue(processName, out profile))
                return null;

            var impact = new ProcessImpact();
            impact.ProcessName = processName;
            impact.AvgCpu = profile.AvgCpu;
            impact.AvgRamMB = profile.AvgRamMB;
            impact.PeakCpu = profile.PeakCpu;
            impact.PeakRamMB = profile.PeakRamMB;

            // Calculate volatility (standard deviation)
            if (profile.SampleCount > 5)
            {
                double variance = 0;
                for (int i = 0; i < profile.SampleCount; i++)
                {
                    double diff = profile.CpuHistory[i] - profile.AvgCpu;
                    variance += diff * diff;
                }
                variance /= profile.SampleCount;
                impact.CpuVolatility = Math.Sqrt(variance);
            }

            // Risk assessment
            if (profile.PeakCpu > 50) impact.RiskLevel = "High";
            else if (profile.PeakCpu > 20) impact.RiskLevel = "Medium";
            else impact.RiskLevel = "Low";

            // Recommendation
            if (profile.AvgCpu > 30 && profile.PeakCpu > 80)
                impact.Recommendation = "Consider limiting this process - high CPU usage detected";
            else if (profile.AvgRamMB > 500)
                impact.Recommendation = "Memory-intensive process - monitor for leaks";
            else
                impact.Recommendation = "Normal resource usage";

            return impact;
        }

        public double GetWorldModelHealth()
        {
            if (_worldModel == null || _worldModel.Nodes.Count == 0) return 100;
            double total = 0;
            int count = 0;
            foreach (var node in _worldModel.Nodes)
            {
                if (node.Type == "Hardware" || node.Type == "Service")
                {
                    total += node.Health;
                    count++;
                }
            }
            return count > 0 ? total / count : 100;
        }

        public WorldNode PredictChangeImpact(string componentName, double newLoad)
        {
            WorldNode node;
            if (!_worldModel.ServiceMap.TryGetValue(componentName, out node)) return null;

            var impact = new WorldNode();
            impact.Name = componentName + " (predicted)";
            impact.Type = "Prediction";
            impact.CurrentLoad = newLoad;
            impact.Health = Math.Max(0, 100 - newLoad);

            foreach (var edge in _worldModel.Edges)
            {
                if (edge.From == componentName || edge.To == componentName)
                {
                    WorldNode affected;
                    if (_worldModel.ServiceMap.TryGetValue(
                        edge.From == componentName ? edge.To : edge.From, out affected))
                    {
                        impact.Health = Math.Min(impact.Health, affected.Health * (1 - edge.Weight * 0.3));
                    }
                }
            }

            return impact;
        }

        // === NEW: Get process profiles for UI ===
        public List<ProcessProfile> GetTopProcesses(int count)
        {
            var sorted = new List<ProcessProfile>(_processProfiles.Values);
            sorted.Sort((a, b) => b.AvgCpu.CompareTo(a.AvgCpu));
            if (sorted.Count > count) sorted.RemoveRange(count, sorted.Count - count);
            return sorted;
        }

        public Dictionary<string, double> GetCorrelationMatrix()
        {
            var result = new Dictionary<string, double>();
            foreach (var kv in _correlationMatrix)
            {
                foreach (var kv2 in kv.Value)
                {
                    result[kv.Key + "->" + kv2.Key] = kv2.Value;
                }
            }
            return result;
        }

        // ============================================================
        // === World Model → Decision Pipeline Connection ===
        // ============================================================
        public List<AgentDecision> GetWorldModelDecisions()
        {
            var decisions = new List<AgentDecision>();
            if (_worldModel == null || _processProfiles.Count == 0) return decisions;

            // 1) Detect resource-hogging processes via Process Profiles
            DetectHoggingProcesses(decisions);
            // 2) Detect critical service failures
            DetectServiceFailures(decisions);
            // 3) Predict cascading failures from correlations
            DetectCascadingRisk(decisions);
            return decisions;
        }

        private void DetectHoggingProcesses(List<AgentDecision> decisions)
        {
            foreach (var kv in _processProfiles)
            {
                ProcessProfile p = kv.Value;
                if (p.TotalSamples < 5) continue;
                // High average CPU process
                if (p.AvgCpu > 25 && p.PeakCpu > 60)
                {
                    // Don't flag games or the foreground app
                    string lower = p.Name.ToLower();
                    if (lower == (_currentState.ActiveProcess ?? "").ToLower()) continue;
                    bool isProtected = false;
                    for (int i = 0; i < _protectedProcesses.Count; i++)
                    {
                        if (_protectedProcesses[i].ToLower() == lower) { isProtected = true; break; }
                    }
                    if (isProtected) continue;
                    decisions.Add(new AgentDecision
                    {
                        Category = DecisionCategory.BackgroundControl,
                        Action = "LimitBackground",
                        Description = string.Format("World Model: process '{0}' avg CPU {1:F0}%, peak {2:F0}%", p.Name, p.AvgCpu, p.PeakCpu),
                        Reason = string.Format("[WorldModel] Tracked process '{0}' consuming excessive CPU over {1} samples", p.Name, p.TotalSamples),
                        Confidence = Math.Min(0.85, 0.4 + p.AvgCpu / 200.0),
                        Safety = DecisionSafety.Safe
                    });
                }
                // High memory process
                if (p.AvgRamMB > 400 && p.PeakRamMB > 800)
                {
                    string lower = p.Name.ToLower();
                    if (lower == (_currentState.ActiveProcess ?? "").ToLower()) continue;
                    decisions.Add(new AgentDecision
                    {
                        Category = DecisionCategory.MemoryClean,
                        Action = "LightClean",
                        Description = string.Format("World Model: process '{0}' using {1:F0}MB RAM (peak {2:F0}MB)", p.Name, p.AvgRamMB, p.PeakRamMB),
                        Reason = string.Format("[WorldModel] Memory-intensive process detected: {0}MB avg", p.Name),
                        Confidence = Math.Min(0.75, 0.35 + p.AvgRamMB / 2000.0),
                        Safety = DecisionSafety.Safe
                    });
                }
            }
        }

        private void DetectServiceFailures(List<AgentDecision> decisions)
        {
            int criticalDown = 0;
            foreach (var node in _worldModel.Nodes)
            {
                if (node.Type == "Service" && node.Health == 0)
                {
                    // Check if it's a critical service
                    if (node.Name == "csrss" || node.Name == "svchost" || node.Name == "dwm" || node.Name == "lsass")
                        criticalDown++;
                }
            }
            if (criticalDown > 0)
            {
                decisions.Add(new AgentDecision
                {
                    Category = DecisionCategory.BackgroundControl,
                    Action = "LightClean",
                    Description = string.Format("World Model: {0} critical services not running", criticalDown),
                    Reason = "[WorldModel] Critical Windows services are down - system stability at risk",
                    Confidence = 0.7,
                    Safety = DecisionSafety.Safe
                });
            }
            // DWM not healthy → frame issues
            WorldNode dwmNode;
            if (_worldModel.ServiceMap.TryGetValue("dwm", out dwmNode) && dwmNode.Health < 50)
            {
                    decisions.Add(new AgentDecision
                    {
                        Category = DecisionCategory.BackgroundControl,
                        Action = "StabilizeFrames",
                        Description = "World Model: Desktop Window Manager degraded",
                        Reason = string.Format("[WorldModel] DWM health={0:F0} - may cause visual stuttering", dwmNode.Health),
                        Confidence = 0.65,
                        Safety = DecisionSafety.Safe
                    });
            }
        }

        private void DetectCascadingRisk(List<AgentDecision> decisions)
        {
            // Check correlation: if CPU→Temp correlation is strong and both are rising
            double cpuTempCorr = 0;
            Dictionary<string, double> cpuCorrs;
            if (_correlationMatrix.TryGetValue("CPU", out cpuCorrs))
            {
                cpuCorrs.TryGetValue("Temp", out cpuTempCorr);
            }
            if (cpuTempCorr > 0.8 && _smoothCpu > 70 && _smoothTemp > 65)
            {
                decisions.Add(new AgentDecision
                {
                    Category = DecisionCategory.ThermalProtect,
                    Action = "ReduceThermal",
                    Description = "World Model: CPU→Temp cascade predicted (correlation=" + cpuTempCorr.ToString("F2") + ")",
                    Reason = string.Format("[WorldModel] Strong CPU-Temp correlation ({0:F2}) with both rising - cascade risk", cpuTempCorr),
                    Confidence = 0.7,
                    Safety = DecisionSafety.Safe
                });
            }
            // Check correlation: RAM→CPU correlation
            double ramCpuCorr = 0;
            Dictionary<string, double> ramCorrs;
            if (_correlationMatrix.TryGetValue("RAM", out ramCorrs))
            {
                ramCorrs.TryGetValue("CPU", out ramCpuCorr);
            }
            if (ramCpuCorr > 0.75 && _smoothRam > 75 && _smoothCpu > 65)
            {
                decisions.Add(new AgentDecision
                {
                    Category = DecisionCategory.MemoryClean,
                    Action = "LightClean",
                    Description = "World Model: RAM→CPU cascade risk (correlation=" + ramCpuCorr.ToString("F2") + ")",
                    Reason = string.Format("[WorldModel] RAM and CPU strongly correlated ({0:F2}) - memory pressure causing CPU load", ramCpuCorr),
                    Confidence = 0.65,
                    Safety = DecisionSafety.Safe
                });
            }
        }
    }
}
