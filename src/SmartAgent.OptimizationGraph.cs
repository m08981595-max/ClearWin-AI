using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ClearWin
{
    public partial class SmartAgent
    {
        private OptGraph _optimizationGraph;
        private DateTime _lastGraphUpdate = DateTime.MinValue;
        private const int GRAPH_UPDATE_INTERVAL_SECONDS = 60;
        private List<OptGraphSnapshot> _graphSnapshots = new List<OptGraphSnapshot>();
        private List<OptimalChangeSet> _optimalChangeSets = new List<OptimalChangeSet>();

        public OptGraph GetOptimizationGraph() { return _optimizationGraph; }
        public List<OptGraphSnapshot> GetGraphSnapshots() { return new List<OptGraphSnapshot>(_graphSnapshots); }
        public List<OptimalChangeSet> GetOptimalChangeSets() { return new List<OptimalChangeSet>(_optimalChangeSets); }

        public void InitializeOptimizationGraph()
        {
            _optimizationGraph = new OptGraph();
            _optimizationGraph.Nodes = new List<OptGraphNode>();
            _optimizationGraph.Edges = new List<OptGraphEdge>();
            _optimizationGraph.NodeMap = new Dictionary<string, OptGraphNode>();

            AddOptNode("CPU", "Hardware", _smoothCpu, 0.2);
            AddOptNode("RAM", "Hardware", _smoothRam, 0.2);
            AddOptNode("GPU", "Hardware", _smoothGpu, 0.15);
            AddOptNode("Disk", "Hardware", _currentState.DiskUsage, 0.1);
            AddOptNode("Network", "Hardware", 0, 0.05);
            AddOptNode("Power", "Hardware", _isOnBattery ? (100 - _currentState.BatteryLevel) : 0, 0.1);
            AddOptNode("Temperature", "Sensor", _smoothTemp, 0.25);

            AddOptNode("Processes", "Software", _currentState.ProcessCount / 10.0, 0.15);
            AddOptNode("Services", "Software", 50, 0.1);
            AddOptNode("Drivers", "Software", 50, 0.05);
            AddOptNode("Applications", "Software", 60, 0.1);

            AddOptEdge("CPU", "Temperature", "Heats", 0.8);
            AddOptEdge("GPU", "Temperature", "Heats", 0.7);
            AddOptEdge("RAM", "CPU", "Consumes", 0.3);
            AddOptEdge("Processes", "CPU", "Loads", 0.6);
            AddOptEdge("Processes", "RAM", "Loads", 0.5);
            AddOptEdge("Applications", "Processes", "Spawns", 0.7);
            AddOptEdge("Services", "CPU", "Uses", 0.3);
            AddOptEdge("Services", "RAM", "Uses", 0.2);
            AddOptEdge("Drivers", "CPU", "Uses", 0.15);
            AddOptEdge("Disk", "CPU", "IO", 0.2);
            AddOptEdge("Temperature", "CPU", "Throttles", 0.9);
            AddOptEdge("Power", "CPU", "Limits", 0.4);
            AddOptEdge("Power", "GPU", "Limits", 0.3);
            AddOptEdge("Network", "CPU", "Uses", 0.1);

            AddLog("GRAPH", "OptimizationGraph",
                string.Format("Initialized: {0} nodes, {1} edges",
                _optimizationGraph.Nodes.Count, _optimizationGraph.Edges.Count));
        }

        private void AddOptNode(string name, string type, double load, double costWeight)
        {
            var node = new OptGraphNode();
            node.Name = name;
            node.Type = type;
            node.CurrentLoad = load;
            node.CostWeight = costWeight;
            node.Available = true;
            _optimizationGraph.Nodes.Add(node);
            _optimizationGraph.NodeMap[name] = node;
        }

        private void AddOptEdge(string from, string to, string relationship, double weight)
        {
            var edge = new OptGraphEdge();
            edge.From = from;
            edge.To = to;
            edge.Relationship = relationship;
            edge.Weight = weight;
            _optimizationGraph.Edges.Add(edge);
        }

        public void UpdateOptimizationGraph()
        {
            if ((DateTime.Now - _lastGraphUpdate).TotalSeconds < GRAPH_UPDATE_INTERVAL_SECONDS) return;

            UpdateOptNodeLoad("CPU", _smoothCpu);
            UpdateOptNodeLoad("RAM", _smoothRam);
            UpdateOptNodeLoad("GPU", _smoothGpu);
            UpdateOptNodeLoad("Disk", _currentState.DiskUsage);
            UpdateOptNodeLoad("Temperature", _smoothTemp);
            UpdateOptNodeLoad("Power", _isOnBattery ? (100 - _currentState.BatteryLevel) : 0);
            UpdateOptNodeLoad("Processes", _currentState.ProcessCount / 10.0);

            FindOptimalChangeSets();

            var snapshot = new OptGraphSnapshot();
            snapshot.Timestamp = DateTime.Now;
            snapshot.AvgLoad = 0;
            int loadCount = 0;
            foreach (var node in _optimizationGraph.Nodes)
            {
                snapshot.AvgLoad += node.CurrentLoad;
                loadCount++;
            }
            snapshot.AvgLoad = loadCount > 0 ? snapshot.AvgLoad / loadCount : 0;
            snapshot.NodeCount = _optimizationGraph.Nodes.Count;
            snapshot.EdgeCount = _optimizationGraph.Edges.Count;
            snapshot.CriticalNodes = GetCriticalNodes().Count;
            _graphSnapshots.Add(snapshot);
            if (_graphSnapshots.Count > 100) _graphSnapshots.RemoveAt(0);

            _optimizationGraph.LastUpdated = DateTime.Now;
            _lastGraphUpdate = DateTime.Now;
        }

        private void UpdateOptNodeLoad(string name, double load)
        {
            OptGraphNode node;
            if (_optimizationGraph.NodeMap.TryGetValue(name, out node))
            {
                node.CurrentLoad = load;
                node.Health = Math.Max(0, 100 - load);
            }
        }

        private void FindOptimalChangeSets()
        {
            _optimalChangeSets.Clear();

            var criticalNodes = GetCriticalNodes();
            if (criticalNodes.Count == 0) return;

            var candidates = new List<OptimalChangeSet>();
            foreach (var node in criticalNodes)
            {
                var actions = GetActionsForNode(node);
                foreach (var action in actions)
                {
                    double cost = CalculateChangeCost(action, node);
                    double benefit = CalculateChangeBenefit(action, node);
                    double risk = CalculateChangeRisk(action, node);

                    if (benefit > cost && risk < 0.7)
                    {
                        var changeSet = new OptimalChangeSet();
                        changeSet.Name = action;
                        changeSet.TargetNode = node.Name;
                        changeSet.ExpectedBenefit = benefit;
                        changeSet.Cost = cost;
                        changeSet.Risk = risk;
                        changeSet.NetScore = benefit - cost - risk * 0.5;
                        changeSet.Timestamp = DateTime.Now;
                        candidates.Add(changeSet);
                    }
                }
            }

            candidates.Sort(delegate(OptimalChangeSet a, OptimalChangeSet b) { return b.NetScore.CompareTo(a.NetScore); });

            for (int i = 0; i < Math.Min(5, candidates.Count); i++)
            {
                _optimalChangeSets.Add(candidates[i]);
            }
        }

        private List<OptGraphNode> GetCriticalNodes()
        {
            var critical = new List<OptGraphNode>();
            foreach (var node in _optimizationGraph.Nodes)
            {
                if (node.CurrentLoad > 70 || (node.Name == "Temperature" && node.CurrentLoad > 65))
                {
                    critical.Add(node);
                }
            }
            return critical;
        }

        private List<string> GetActionsForNode(OptGraphNode node)
        {
            var actions = new List<string>();
            switch (node.Name)
            {
                case "CPU":
                    actions.Add("LimitBackground");
                    actions.Add("SetLowIoPriority");
                    break;
                case "RAM":
                    actions.Add("LightClean");
                    actions.Add("AggressiveClean");
                    actions.Add("PurgeStandbyList");
                    break;
                case "GPU":
                    actions.Add("StabilizeFrames");
                    actions.Add("ReduceMicroStutter");
                    break;
                case "Temperature":
                    actions.Add("ReduceThermal");
                    actions.Add("EmergencyThrottle");
                    break;
                case "Power":
                    actions.Add("BatterySaver");
                    actions.Add("BalancedPerformance");
                    break;
                case "Processes":
                    actions.Add("LimitBackground");
                    actions.Add("SuggestClose");
                    break;
                case "Disk":
                    actions.Add("PurgeStandbyList");
                    break;
            }
            return actions;
        }

        private double CalculateChangeCost(string action, OptGraphNode node)
        {
            int cost;
            if (ACTION_COST.TryGetValue(action, out cost))
                return cost * node.CostWeight;
            return 3 * node.CostWeight;
        }

        private double CalculateChangeBenefit(string action, OptGraphNode node)
        {
            return Math.Max(0, (node.CurrentLoad - 50) * 0.02);
        }

        private double CalculateChangeRisk(string action, OptGraphNode node)
        {
            if (action == "EmergencyThrottle") return 0.6;
            if (action == "AggressiveClean") return 0.4;
            if (action.Contains("Suggest")) return 0.1;
            return 0.2;
        }

        public string GetGraphSummary()
        {
            int critical = GetCriticalNodes().Count;
            int optimal = _optimalChangeSets.Count;
            return string.Format("Nodes: {0} | Edges: {1} | Critical: {2} | Optimal changes: {3}",
                _optimizationGraph.Nodes.Count, _optimizationGraph.Edges.Count, critical, optimal);
        }
    }
}
