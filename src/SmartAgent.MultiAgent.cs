using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ClearWin
{
    public partial class SmartAgent
    {
        // === Multi-Agent Fields ===
        private List<SpecializedAgent> _specializedAgents = new List<SpecializedAgent>();
        private DateTime _lastAgentCycle = DateTime.MinValue;
        private const int AGENT_CYCLE_INTERVAL_SECONDS = 5;
        private List<AgentVote> _lastVotes = new List<AgentVote>();
        private AgentConsensus _lastConsensus;
        private Dictionary<string, AgentPerformance> _agentPerformance = new Dictionary<string, AgentPerformance>();
        private int _agentCycleCount = 0;

        // === Public Accessors ===
        public List<AgentVote> GetLastVotes() { return new List<AgentVote>(_lastVotes); }
        public AgentConsensus GetLastConsensus() { return _lastConsensus; }
        public List<SpecializedAgent> GetSpecializedAgents() { return new List<SpecializedAgent>(_specializedAgents); }

        // === Initialize Multi-Agent System ===
        public void InitializeMultiAgent()
        {
            _specializedAgents.Clear();

            _specializedAgents.Add(new SpecializedAgent
            {
                Name = "Memory Agent", Role = AgentRole.Memory, Weight = 1.0,
                Description = "Monitors RAM usage, detects memory leaks, manages cache"
            });
            _specializedAgents.Add(new SpecializedAgent
            {
                Name = "CPU Agent", Role = AgentRole.CPU, Weight = 1.0,
                Description = "Monitors CPU load, manages process priorities, detects CPU hogs"
            });
            _specializedAgents.Add(new SpecializedAgent
            {
                Name = "GPU Agent", Role = AgentRole.GPU, Weight = 1.0,
                Description = "Monitors GPU usage, optimizes graphics settings, manages frame pacing"
            });
            _specializedAgents.Add(new SpecializedAgent
            {
                Name = "Disk Agent", Role = AgentRole.Disk, Weight = 1.0,
                Description = "Monitors disk I/O, manages cleanup, optimizes storage"
            });
            _specializedAgents.Add(new SpecializedAgent
            {
                Name = "Network Agent", Role = AgentRole.Network, Weight = 1.0,
                Description = "Monitors network usage, detects bandwidth hogs, manages connections"
            });
            _specializedAgents.Add(new SpecializedAgent
            {
                Name = "Thermal Agent", Role = AgentRole.Thermal, Weight = 1.2,
                Description = "Monitors temperature, prevents throttling, manages cooling"
            });
            _specializedAgents.Add(new SpecializedAgent
            {
                Name = "Battery Agent", Role = AgentRole.Battery, Weight = 0.8,
                Description = "Manages power consumption, battery health, charging optimization"
            });
            _specializedAgents.Add(new SpecializedAgent
            {
                Name = "Security Agent", Role = AgentRole.Security, Weight = 0.9,
                Description = "Monitors suspicious processes, manages startup, protects system"
            });

            foreach (var agent in _specializedAgents)
            {
                _agentPerformance[agent.Name] = new AgentPerformance { AgentName = agent.Name };
            }

            AddLog("MULTI", "MultiAgent", "Initialized 8 specialized agents");
        }

        // === Run Agent Cycle (called from Tick) ===
        public void RunAgentCycle()
        {
            if ((DateTime.Now - _lastAgentCycle).TotalSeconds < AGENT_CYCLE_INTERVAL_SECONDS) return;
            _agentCycleCount++;

            _lastVotes.Clear();

            // Each agent analyzes the system and casts a vote
            foreach (var agent in _specializedAgents)
            {
                try
                {
                    var vote = AnalyzeAndVote(agent);
                    if (vote != null) _lastVotes.Add(vote);
                }
                catch (Exception ex) { LogError("RunAgentCycle", ex); }
            }

            // Build consensus from votes
            _lastConsensus = BuildConsensus(_lastVotes);

            // Update agent performance tracking
            UpdateAgentPerformance();

            if (_agentCycleCount % 12 == 0)
            {
                AddLog("MULTI", "MultiAgent",
                    string.Format("Agent cycle #{0}: {1} votes, Consensus={2}",
                    _agentCycleCount, _lastVotes.Count,
                    _lastConsensus != null ? _lastConsensus.RecommendedAction : "NONE"));
            }

            _lastAgentCycle = DateTime.Now;
        }

        // === Individual Agent Analysis ===
        private AgentVote AnalyzeAndVote(SpecializedAgent agent)
        {
            var vote = new AgentVote();
            vote.AgentName = agent.Name;
            vote.Role = agent.Role;
            vote.Timestamp = DateTime.Now;

            switch (agent.Role)
            {
                case AgentRole.Memory:
                    vote = AnalyzeMemoryAgent(vote);
                    break;
                case AgentRole.CPU:
                    vote = AnalyzeCPUAgent(vote);
                    break;
                case AgentRole.GPU:
                    vote = AnalyzeGPUAgent(vote);
                    break;
                case AgentRole.Disk:
                    vote = AnalyzeDiskAgent(vote);
                    break;
                case AgentRole.Network:
                    vote = AnalyzeNetworkAgent(vote);
                    break;
                case AgentRole.Thermal:
                    vote = AnalyzeThermalAgent(vote);
                    break;
                case AgentRole.Battery:
                    vote = AnalyzeBatteryAgent(vote);
                    break;
                case AgentRole.Security:
                    vote = AnalyzeSecurityAgent(vote);
                    break;
            }

            vote.Weight = agent.Weight;
            return vote;
        }

        private AgentVote AnalyzeMemoryAgent(AgentVote vote)
        {
            if (_smoothRam > 85) { vote.Action = "AggressiveClean"; vote.Priority = 9; vote.Confidence = 0.85; vote.Reason = "RAM critical at " + string.Format("{0:F0}", _smoothRam) + "%"; }
            else if (_smoothRam > 70) { vote.Action = "LightClean"; vote.Priority = 6; vote.Confidence = 0.7; vote.Reason = "RAM elevated at " + string.Format("{0:F0}", _smoothRam) + "%"; }
            else if (_smoothRam > 55) { vote.Action = "PreemptiveClean"; vote.Priority = 3; vote.Confidence = 0.5; vote.Reason = "RAM moderate, preemptive clean"; }
            else { vote.Action = "NONE"; vote.Priority = 0; vote.Confidence = 0.3; vote.Reason = "RAM healthy at " + string.Format("{0:F0}", _smoothRam) + "%"; }
            return vote;
        }

        private AgentVote AnalyzeCPUAgent(AgentVote vote)
        {
            if (_smoothCpu > 95) { vote.Action = "LimitBackground"; vote.Priority = 8; vote.Confidence = 0.8; vote.Reason = "CPU critical at " + string.Format("{0:F0}", _smoothCpu) + "%"; }
            else if (_smoothCpu > 80) { vote.Action = "SetLowIoPriority"; vote.Priority = 5; vote.Confidence = 0.6; vote.Reason = "CPU high at " + string.Format("{0:F0}", _smoothCpu) + "%"; }
            else { vote.Action = "NONE"; vote.Priority = 0; vote.Confidence = 0.3; vote.Reason = "CPU healthy at " + string.Format("{0:F0}", _smoothCpu) + "%"; }
            return vote;
        }

        private AgentVote AnalyzeGPUAgent(AgentVote vote)
        {
            if (_currentState.IsGame && _smoothGpu > 90)
            {
                vote.Action = "StabilizeFrames"; vote.Priority = 7; vote.Confidence = 0.75;
                vote.Reason = "GPU near max during game: " + string.Format("{0:F0}", _smoothGpu) + "%";
            }
            else if (_currentState.FpsStability < 60)
            {
                vote.Action = "ReduceMicroStutter"; vote.Priority = 6; vote.Confidence = 0.65;
                vote.Reason = "Frame instability: " + string.Format("{0:F0}", _currentState.FpsStability);
            }
            else { vote.Action = "NONE"; vote.Priority = 0; vote.Confidence = 0.3; vote.Reason = "GPU stable"; }
            return vote;
        }

        private AgentVote AnalyzeDiskAgent(AgentVote vote)
        {
            if (_currentState.DiskUsage > 90) { vote.Action = "PurgeStandbyList"; vote.Priority = 7; vote.Confidence = 0.7; vote.Reason = "Disk critically full"; }
            else if (_currentState.DiskUsage > 80) { vote.Action = "LightClean"; vote.Priority = 4; vote.Confidence = 0.5; vote.Reason = "Disk getting full"; }
            else { vote.Action = "NONE"; vote.Priority = 0; vote.Confidence = 0.3; vote.Reason = "Disk OK"; }
            return vote;
        }

        private AgentVote AnalyzeNetworkAgent(AgentVote vote)
        {
            // Network agent is conservative - rarely takes action
            vote.Action = "NONE"; vote.Priority = 0; vote.Confidence = 0.2;
            vote.Reason = "Network monitoring only";
            return vote;
        }

        private AgentVote AnalyzeThermalAgent(AgentVote vote)
        {
            if (_smoothTemp > 85) { vote.Action = "EmergencyThrottle"; vote.Priority = 10; vote.Confidence = 0.95; vote.Reason = "CRITICAL temp: " + string.Format("{0:F0}", _smoothTemp) + "C"; }
            else if (_smoothTemp > 75) { vote.Action = "ReduceThermal"; vote.Priority = 7; vote.Confidence = 0.8; vote.Reason = "High temp: " + string.Format("{0:F0}", _smoothTemp) + "C"; }
            else if (_smoothTemp > 65) { vote.Action = "NONE"; vote.Priority = 2; vote.Confidence = 0.4; vote.Reason = "Temp warm: " + string.Format("{0:F0}", _smoothTemp) + "C"; }
            else { vote.Action = "NONE"; vote.Priority = 0; vote.Confidence = 0.2; vote.Reason = "Temp normal: " + string.Format("{0:F0}", _smoothTemp) + "C"; }
            return vote;
        }

        private AgentVote AnalyzeBatteryAgent(AgentVote vote)
        {
            if (_isOnBattery && _batteryPercent < 20) { vote.Action = "BatterySaver"; vote.Priority = 8; vote.Confidence = 0.9; vote.Reason = "Battery critical: " + _batteryPercent + "%"; }
            else if (_isOnBattery && _batteryPercent < 50) { vote.Action = "BatterySaver"; vote.Priority = 4; vote.Confidence = 0.5; vote.Reason = "Battery moderate: " + _batteryPercent + "%"; }
            else { vote.Action = "NONE"; vote.Priority = 0; vote.Confidence = 0.2; vote.Reason = "Battery OK or plugged in"; }
            return vote;
        }

        private AgentVote AnalyzeSecurityAgent(AgentVote vote)
        {
            // Security agent is watchful - rarely takes action
            if (_currentState.ProcessCount > 200)
            { vote.Action = "SuggestClose"; vote.Priority = 3; vote.Confidence = 0.4; vote.Reason = "Too many processes: " + _currentState.ProcessCount; }
            else { vote.Action = "NONE"; vote.Priority = 0; vote.Confidence = 0.2; vote.Reason = "System secure"; }
            return vote;
        }

        // === Build Consensus ===
        private AgentConsensus BuildConsensus(List<AgentVote> votes)
        {
            var consensus = new AgentConsensus();
            consensus.Timestamp = DateTime.Now;
            consensus.TotalVotes = votes.Count;

            if (votes.Count == 0) return consensus;

            // Group votes by action
            var actionGroups = new Dictionary<string, List<AgentVote>>();
            foreach (var vote in votes)
            {
                if (vote.Action == "NONE" || string.IsNullOrEmpty(vote.Action)) continue;
                if (!actionGroups.ContainsKey(vote.Action))
                    actionGroups[vote.Action] = new List<AgentVote>();
                actionGroups[vote.Action].Add(vote);
            }

            if (actionGroups.Count == 0)
            {
                consensus.RecommendedAction = "NONE";
                consensus.ConsensusStrength = 0;
                return consensus;
            }

            // Find action with highest weighted score
            string bestAction = "";
            double bestScore = 0;
            foreach (var kv in actionGroups)
            {
                double score = 0;
                foreach (var vote in kv.Value)
                {
                    score += vote.Priority * vote.Confidence * vote.Weight;
                }
                if (score > bestScore) { bestScore = score; bestAction = kv.Key; }
            }

            consensus.RecommendedAction = bestAction;
            consensus.VotesForAction = actionGroups.ContainsKey(bestAction) ? actionGroups[bestAction].Count : 0;
            consensus.ConsensusStrength = consensus.VotesForAction > 0
                ? (double)consensus.VotesForAction / votes.Count : 0;
            consensus.TotalScore = bestScore;

            // Find dissenting agents
            consensus.DissentingAgents = new List<string>();
            foreach (var vote in votes)
            {
                if (vote.Action != bestAction && vote.Action != "NONE" && vote.Priority > 5)
                    consensus.DissentingAgents.Add(vote.AgentName);
            }

            return consensus;
        }

        // === Update Agent Performance ===
        private void UpdateAgentPerformance()
        {
            foreach (var perf in _agentPerformance.Values)
            {
                // Find if this agent's last vote was acted upon
                foreach (var vote in _lastVotes)
                {
                    if (vote.AgentName == perf.AgentName)
                    {
                        perf.TotalVotes++;
                        if (_lastConsensus != null && vote.Action == _lastConsensus.RecommendedAction)
                            perf.AcceptedVotes++;
                        break;
                    }
                }
            }
        }

        // === Get Multi-Agent Recommendation ===
        public AgentDecision GetMultiAgentRecommendation()
        {
            if (_lastConsensus == null || _lastConsensus.RecommendedAction == "NONE") return null;

            var decision = new AgentDecision();
            decision.Action = _lastConsensus.RecommendedAction;
            decision.Description = "Multi-Agent consensus: " + _lastConsensus.RecommendedAction;
            string actionLower = _lastConsensus.RecommendedAction.ToLower();
            if (actionLower.Contains("memory") || actionLower.Contains("ram") || actionLower.Contains("clean") || actionLower.Contains("purge"))
                decision.Category = DecisionCategory.MemoryClean;
            else if (actionLower.Contains("throttle") || actionLower.Contains("thermal") || actionLower.Contains("temp"))
                decision.Category = DecisionCategory.ThermalProtect;
            else if (actionLower.Contains("cpu") || actionLower.Contains("priority"))
                decision.Category = DecisionCategory.ProcessPriority;
            else if (actionLower.Contains("game") || actionLower.Contains("fps") || actionLower.Contains("gpu"))
                decision.Category = DecisionCategory.GameBoost;
            else if (actionLower.Contains("power") || actionLower.Contains("battery") || actionLower.Contains("plan"))
                decision.Category = DecisionCategory.PowerPlan;
            else if (actionLower.Contains("background") || actionLower.Contains("service"))
                decision.Category = DecisionCategory.BackgroundControl;
            else if (actionLower.Contains("affinity") || actionLower.Contains("core"))
                decision.Category = DecisionCategory.CoreParking;
            else if (actionLower.Contains("io"))
                decision.Category = DecisionCategory.IoPriority;
            else
                decision.Category = DecisionCategory.MemoryClean;
            decision.Confidence = Math.Min(1.0, _lastConsensus.ConsensusStrength * 0.8);
            decision.Risk = 1.0 - _lastConsensus.ConsensusStrength * 0.5;
            decision.Reason = string.Format("Consensus: {0}/{1} agents agree (strength={2:F0}%)",
                _lastConsensus.VotesForAction, _lastConsensus.TotalVotes,
                _lastConsensus.ConsensusStrength * 100);
            decision.Timestamp = DateTime.Now;

            return decision;
        }
    }
}
