using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ClearWin.RuleEngine
{
    /// <summary>
    /// Knowledge Base: a large, extensible store of facts, production rules,
    /// causal links and per-action statistics used by every other engine.
    /// Supports dynamic registration (AddRule / AddCausalLink / SetFact),
    /// learned statistics with time decay, and persistence to disk.
    /// </summary>
    public class RuleKnowledgeBase
    {
        private readonly object _lock = new object();
        private readonly string _dataDir;

        private readonly Dictionary<string, ExpertFact> _facts = new Dictionary<string, ExpertFact>();
        private readonly Dictionary<string, ExpertRule> _rules = new Dictionary<string, ExpertRule>();
        private readonly List<CausalLink> _links = new List<CausalLink>();
        private readonly Dictionary<string, ActionStatistics> _stats = new Dictionary<string, ActionStatistics>();

        public int FactCount { get { lock (_lock) { return _facts.Count; } } }
        public int RuleCount { get { lock (_lock) { return _rules.Count; } } }
        public int LinkCount { get { lock (_lock) { return _links.Count; } } }
        public int StatCount { get { lock (_lock) { return _stats.Count; } } }

        public RuleKnowledgeBase(string dataDir)
        {
            _dataDir = dataDir;
        }

        /// <summary>Action ids the host agent can actually execute.</summary>
        public static readonly string[] KnownActions =
        {
            "AggressiveClean", "LightClean", "PreemptiveClean", "PurgeStandbyList",
            "LimitBackground", "BoostGamePriority", "EmergencyThrottle", "ReduceThermal",
            "StabilizeFrames", "ReduceMicroStutter", "BatterySaver", "BalancedPerformance",
            "SuggestClose", "HealthCheck", "SetGameAffinity", "LimitBackgroundAffinity",
            "SetLowIoPriority", "UnparkAllCores", "OptimizeNetwork", "FlushDnsCache",
            "ResetNetworkPriority", "SwitchPowerPlan", "DiskCleanup", "StartupDisable",
            "StartService"
        };

        public static bool IsKnownAction(string actionId)
        {
            if (string.IsNullOrEmpty(actionId)) return false;
            for (int i = 0; i < KnownActions.Length; i++)
                if (string.Equals(KnownActions[i], actionId, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        // ============================ FACTS ============================
        public void SetFact(string variable, double value, double confidence, string source)
        {
            lock (_lock)
            {
                if (string.IsNullOrEmpty(variable)) return;
                ExpertFact fact;
                bool isNew = !_facts.TryGetValue(variable, out fact);
                if (isNew)
                {
                    if (_facts.Count >= RuleEngineConstants.MaxFacts)
                        EvictOldestFact();
                    fact = new ExpertFact { Variable = variable };
                    _facts[variable] = fact;
                }
                // Blend new observation with old (temporal smoothing), but the
                // first observation is stored exactly as-is.
                if (isNew)
                    fact.Value = value;
                else
                    fact.Value = fact.Value * 0.7 + value * 0.3;
                fact.Confidence = Math.Max(0.0, Math.Min(1.0, confidence));
                fact.Source = source;
                fact.Timestamp = DateTime.Now;
            }
        }

        private void EvictOldestFact()
        {
            string oldestVar = null;
            DateTime oldest = DateTime.MaxValue;
            foreach (var kv in _facts)
            {
                if (kv.Value.Timestamp < oldest)
                {
                    oldest = kv.Value.Timestamp;
                    oldestVar = kv.Key;
                }
            }
            if (oldestVar != null) _facts.Remove(oldestVar);
        }

        /// <summary>Retracts a fact so stale derived conclusions never linger.</summary>
        public void RemoveFact(string variable)
        {
            lock (_lock) { if (_facts.ContainsKey(variable)) _facts.Remove(variable); }
        }

        public ExpertFact GetFact(string variable)
        {
            lock (_lock)
            {
                ExpertFact fact;
                if (_facts.TryGetValue(variable, out fact)) return fact;
            }
            return null;
        }

        public double GetFactValue(string variable, double fallback)
        {
            lock (_lock)
            {
                ExpertFact fact;
                if (_facts.TryGetValue(variable, out fact)) return fact.Value;
            }
            return fallback;
        }

        public List<ExpertFact> GetAllFacts()
        {
            lock (_lock) { return new List<ExpertFact>(_facts.Values); }
        }

        // ============================ RULES ============================
        public void AddRule(ExpertRule rule)
        {
            if (rule == null || string.IsNullOrEmpty(rule.Id)) return;
            lock (_lock)
            {
                if (_rules.Count >= RuleEngineConstants.MaxRules) return;
                _rules[rule.Id] = rule;
            }
        }

        public void RemoveRule(string id)
        {
            lock (_lock) { if (_rules.ContainsKey(id)) _rules.Remove(id); }
        }

        public ExpertRule GetRule(string id)
        {
            lock (_lock)
            {
                ExpertRule rule;
                if (_rules.TryGetValue(id, out rule)) return rule;
            }
            return null;
        }

        public List<ExpertRule> GetRules()
        {
            lock (_lock) { return new List<ExpertRule>(_rules.Values); }
        }

        /// <summary>Rules whose conclusion targets a specific variable.</summary>
        public List<ExpertRule> RulesConcluding(string variable)
        {
            var result = new List<ExpertRule>();
            lock (_lock)
            {
                foreach (var rule in _rules.Values)
                {
                    if (rule.Enabled && rule.Conclusion != null &&
                        rule.Conclusion.IndexOf(variable, StringComparison.OrdinalIgnoreCase) >= 0)
                        result.Add(rule);
                }
            }
            return result;
        }

        public void RecordRuleMatch(string ruleId, bool success)
        {
            lock (_lock)
            {
                ExpertRule rule;
                if (_rules.TryGetValue(ruleId, out rule))
                {
                    rule.MatchCount++;
                    if (success) rule.SuccessCount++;
                }
            }
        }

        // ============================ CAUSAL LINKS ============================
        public void AddCausalLink(CausalLink link)
        {
            if (link == null) return;
            lock (_lock)
            {
                if (_links.Count >= RuleEngineConstants.MaxCausalLinks) return;
                _links.Add(link);
            }
        }

        public List<CausalLink> LinksByEffect(string effect)
        {
            var result = new List<CausalLink>();
            lock (_lock)
            {
                for (int i = 0; i < _links.Count; i++)
                {
                    if (_links[i].Enabled &&
                        string.Equals(_links[i].Effect, effect, StringComparison.OrdinalIgnoreCase))
                        result.Add(_links[i]);
                }
            }
            return result;
        }

        public List<CausalLink> GetAllLinks()
        {
            lock (_lock) { return new List<CausalLink>(_links); }
        }

        public void AdjustLinkStrength(string linkId, double delta)
        {
            lock (_lock)
            {
                for (int i = 0; i < _links.Count; i++)
                {
                    if (_links[i].Id == linkId)
                    {
                        _links[i].Strength = Math.Max(0.0, Math.Min(1.0, _links[i].Strength + delta));
                        _links[i].Samples++;
                        return;
                    }
                }
            }
        }

        // ============================ ACTION STATISTICS ============================
        public ActionStatistics GetStatistics(string actionId)
        {
            lock (_lock)
            {
                ActionStatistics s;
                if (_stats.TryGetValue(actionId, out s)) return s;
            }
            return null;
        }

        /// <summary>Records an outcome with exponential time decay to avoid permanent bias.</summary>
        public void RecordOutcome(string actionId, bool success, double scoreDelta, double risk, string category)
        {
            if (string.IsNullOrEmpty(actionId)) return;
            lock (_lock)
            {
                ActionStatistics s;
                if (!_stats.TryGetValue(actionId, out s))
                {
                    if (_stats.Count >= RuleEngineConstants.MaxActionStats)
                        EvictLeastUsefulStat();
                    s = new ActionStatistics { ActionId = actionId, Category = category };
                    _stats[actionId] = s;
                }
                if (s.Category == null) s.Category = category;

                double hoursSinceLast = s.LastUsed == DateTime.MinValue ? 0 : (DateTime.Now - s.LastUsed).TotalHours;
                double decay = Math.Exp(-hoursSinceLast / RuleEngineConstants.LearningDecayHours);
                // Only decay stats with meaningful age; otherwise rapid consecutive
                // observations would erode counts through integer truncation.
                if (s.TotalUses > 0 && decay < 0.99)
                {
                    s.TotalUses = Math.Max(1, (int)(s.TotalUses * decay));
                    s.Successes = Math.Min(s.Successes, s.TotalUses);
                }

                s.TotalUses++;
                if (success) s.Successes++; else s.Failures++;
                s.AvgImpact = s.AvgImpact * 0.9 + scoreDelta * 0.1;
                s.AvgRisk = s.AvgRisk == 0 ? risk : s.AvgRisk * 0.9 + risk * 0.1;
                s.SuccessRate = (double)s.Successes / Math.Max(1, s.TotalUses);
                s.Confidence = s.SuccessRate * 0.7 + (s.TotalUses >= RuleEngineConstants.LearningMinSamples ? 1.0 : 0.4);
                s.LastScoreDelta = scoreDelta;
                s.LastUsed = DateTime.Now;
            }
        }

        private void EvictLeastUsefulStat()
        {
            string evict = null;
            double lowest = double.MaxValue;
            foreach (var kv in _stats)
            {
                double usefulness = kv.Value.SuccessRate * kv.Value.AvgImpact * kv.Value.TotalUses;
                if (usefulness < lowest)
                {
                    lowest = usefulness;
                    evict = kv.Key;
                }
            }
            if (evict != null) _stats.Remove(evict);
        }

        public string GetBestActionForCategory(string category)
        {
            string best = null;
            double bestScore = double.MinValue;
            lock (_lock)
            {
                foreach (var s in _stats.Values)
                {
                    if (s.TotalUses < RuleEngineConstants.LearningMinSamples) continue;
                    if (category != null && s.Category != null && !CategoryMatches(s.Category, category)) continue;
                    double score = s.SuccessRate * s.AvgImpact;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = s.ActionId;
                    }
                }
            }
            return best;
        }

        private static bool CategoryMatches(string statCategory, string requested)
        {
            if (string.Equals(statCategory, requested, StringComparison.OrdinalIgnoreCase)) return true;
            // Alias normalization (DecisionEngine uses ActionClass names, links use plain names).
            string a = statCategory.ToLowerInvariant();
            string b = requested.ToLowerInvariant();
            if (a == "process") a = "cpu";
            if (b == "process") b = "cpu";
            return a == b;
        }

        public List<ActionStatistics> GetAllStatistics()
        {
            lock (_lock) { return new List<ActionStatistics>(_stats.Values); }
        }

        // ============================ SEED KNOWLEDGE ============================
        /// <summary>Loads an initial, professional Windows-optimization knowledge base.</summary>
        public void SeedDefaults()
        {
            // --- Causal links (Cause -> Effect) ---
            AddCausalLink(MakeLink("c1", RuleEngineConstants.Vars.CpuUsage, RuleEngineConstants.Vars.CpuTemp, 0.7, "Heavy CPU load produces heat, raising temperature", "Thermal"));
            AddCausalLink(MakeLink("c2", RuleEngineConstants.Vars.CpuTemp, RuleEngineConstants.Vars.Fps, 0.6, "Thermal throttling reduces clock speed, dropping FPS", "Thermal"));
            AddCausalLink(MakeLink("c3", RuleEngineConstants.Vars.RamUsage, RuleEngineConstants.Vars.Fps, 0.65, "High memory pressure forces paging, causing stutter", "Memory"));
            AddCausalLink(MakeLink("c4", RuleEngineConstants.Vars.FreeRamGb, RuleEngineConstants.Vars.Fps, 0.5, "Very low free RAM forces swap file usage", "Memory"));
            AddCausalLink(MakeLink("c5", RuleEngineConstants.Vars.ProcessCount, RuleEngineConstants.Vars.CpuUsage, 0.55, "Many background processes compete for CPU time", "CPU"));
            AddCausalLink(MakeLink("c6", RuleEngineConstants.Vars.ProcessCount, RuleEngineConstants.Vars.RamUsage, 0.6, "Every background process consumes private memory", "Memory"));
            AddCausalLink(MakeLink("c7", RuleEngineConstants.Vars.GpuUsage, RuleEngineConstants.Vars.CpuTemp, 0.45, "GPU heat leaks into the chassis, raising CPU temperature", "Thermal"));
            AddCausalLink(MakeLink("c8", RuleEngineConstants.Vars.DiskUsage, RuleEngineConstants.Vars.Fps, 0.5, "Saturated disk I/O delays asset loading in games", "Disk"));
            AddCausalLink(MakeLink("c9", RuleEngineConstants.Vars.Fps, RuleEngineConstants.Vars.FpsStability, 0.7, "Low average FPS correlates with unstable frame delivery", "Gaming"));
            AddCausalLink(MakeLink("c10", RuleEngineConstants.Vars.BatteryLevel, RuleEngineConstants.Vars.Fps, 0.4, "Low battery triggers power-saving behavior that limits performance", "Power"));
            AddCausalLink(MakeLink("c11", RuleEngineConstants.Vars.CpuSaturation, RuleEngineConstants.Vars.Fps, 0.6, "CPU saturation starves the render thread, dropping frames", "CPU"));
            AddCausalLink(MakeLink("c12", RuleEngineConstants.Vars.FreeDiskGb, RuleEngineConstants.Vars.CpuUsage, 0.3, "Nearly-full system drive causes virtual-memory thrashing", "Disk"));
            AddCausalLink(MakeLink("c13", RuleEngineConstants.Vars.NetworkThroughput, RuleEngineConstants.Vars.Fps, 0.5, "Network saturation delays game data and updates, stalling frames", "Network"));
            AddCausalLink(MakeLink("c14", RuleEngineConstants.Vars.StartupCount, RuleEngineConstants.Vars.CpuUsage, 0.5, "Startup bloat adds background processes competing for CPU", "CPU"));
            AddCausalLink(MakeLink("c15", RuleEngineConstants.Vars.PowerPlanPowerSaver, RuleEngineConstants.Vars.Fps, 0.45, "Power saver plan limits clocks, capping FPS", "Power"));
            AddCausalLink(MakeLink("c16", RuleEngineConstants.Vars.PowerPlanHighPerf, RuleEngineConstants.Vars.CpuTemp, 0.4, "High performance plan raises clocks, increasing heat", "Thermal"));
            AddCausalLink(MakeLink("c17", RuleEngineConstants.Vars.StartupCount, RuleEngineConstants.Vars.RamUsage, 0.45, "Startup apps reserve private memory permanently", "Memory"));
            AddCausalLink(MakeLink("c18", RuleEngineConstants.Vars.DiskUsage, RuleEngineConstants.Vars.CpuUsage, 0.4, "Constant disk thrashing consumes CPU cycles on I/O waits", "Disk"));
            AddCausalLink(MakeLink("c19", RuleEngineConstants.Vars.ServicesStopped, RuleEngineConstants.Vars.CpuUsage, 0.35, "Stopped critical services force retries and degrade responsiveness", "Services"));

            // --- Production rules ---
            AddRule(MakeRule("r1", "High CPU load", "cpu.load.high", RuleEngineConstants.Vars.CpuUsage, ">", RuleEngineConstants.HighCpu, 0.9, "Thermal", null));
            AddRule(MakeRule("r2", "Critical CPU load", "cpu.load.critical", RuleEngineConstants.Vars.CpuUsage, ">", RuleEngineConstants.CriticalCpu, 0.95, "Thermal", "LimitBackground"));
            AddRule(MakeRule("r3", "High memory pressure", "memory.pressure.high", RuleEngineConstants.Vars.RamUsage, ">", RuleEngineConstants.HighRam, 0.9, "Memory", "LightClean"));
            AddRule(MakeRule("r4", "Critical memory pressure", "memory.pressure.critical", RuleEngineConstants.Vars.RamUsage, ">", RuleEngineConstants.CriticalRam, 0.95, "Memory", "AggressiveClean"));
            AddRule(MakeRule("r5", "High thermal stress", "thermal.stress.high", RuleEngineConstants.Vars.CpuTemp, ">", RuleEngineConstants.HighThermal, 0.9, "Thermal", "ReduceThermal"));
            AddRule(MakeRule("r6", "Critical thermal stress", "thermal.stress.critical", RuleEngineConstants.Vars.CpuTemp, ">", RuleEngineConstants.CriticalThermal, 0.98, "Thermal", "EmergencyThrottle"));
            AddRule(MakeRule2("r7", "Low FPS in game", "gaming.fps.low",
                new[] { RuleEngineConstants.Vars.Fps, RuleEngineConstants.Vars.IsGame },
                new[] { "<", "==" },
                new[] { RuleEngineConstants.LowFps, 1.0 },
                new[] { 1.0, 1.0 }, 0.8, "Gaming", "BoostGamePriority"));
            AddRule(MakeRule2("r8", "Critical low FPS", "gaming.fps.critical",
                new[] { RuleEngineConstants.Vars.Fps, RuleEngineConstants.Vars.IsGame },
                new[] { "<", "==" },
                new[] { RuleEngineConstants.CriticalFps, 1.0 },
                new[] { 1.0, 1.0 }, 0.9, "Gaming", "BoostGamePriority"));
            AddRule(MakeRule("r9", "Low free disk space", "disk.free.low", RuleEngineConstants.Vars.FreeDiskGb, "<", RuleEngineConstants.LowDiskGb, 0.85, "Disk", null));
            AddRule(MakeRule("r10", "Process bloat", "processes.bloated", RuleEngineConstants.Vars.ProcessCount, ">", RuleEngineConstants.HighProcessCount, 0.8, "CPU", "SuggestClose"));
            AddRule(MakeRule("r11", "Low battery", "battery.low", RuleEngineConstants.Vars.BatteryLevel, "<", RuleEngineConstants.BatteryLow, 0.85, "Power", "BatterySaver"));
            AddRule(MakeRule("r12", "Gaming detected", "workload.game", RuleEngineConstants.Vars.IsGame, "==", 1, 0.95, "Gaming", null));
            AddRule(MakeRule2("r13", "FPS instability", "gaming.instability",
                new[] { RuleEngineConstants.Vars.FpsStability, RuleEngineConstants.Vars.IsGame },
                new[] { "<", "==" },
                new[] { 50.0, 1.0 },
                new[] { 1.0, 1.0 }, 0.8, "Gaming", "StabilizeFrames"));
            AddRule(MakeRule("r14", "High GPU load", "gpu.load.high", RuleEngineConstants.Vars.GpuUsage, ">", RuleEngineConstants.HighGpu, 0.75, "GPU", null));
            AddRule(MakeRule("r15", "High disk activity", "disk.load.high", RuleEngineConstants.Vars.DiskUsage, ">", RuleEngineConstants.HighDisk, 0.75, "Disk", "SetLowIoPriority"));

            // --- New professional Windows-optimization domains ---
            AddRule(MakeRule("r16", "Network saturation", "network.saturated", RuleEngineConstants.Vars.NetworkThroughput, ">", RuleEngineConstants.HighNetworkMbps, 0.8, "Network", "OptimizeNetwork"));
            AddRule(MakeRule("r17", "Startup bloat", "startup.bloated", RuleEngineConstants.Vars.StartupCount, ">", RuleEngineConstants.HighStartupCount, 0.8, "CPU", "StartupDisable"));
            AddRule(MakeRule("r18", "Power saver active", "power.saver_active", RuleEngineConstants.Vars.PowerPlanPowerSaver, "==", 1, 0.9, "Power", "BalancedPerformance"));
            AddRule(MakeRule("r19", "Disk cleanup needed", "disk.cleanup_needed", RuleEngineConstants.Vars.FreeDiskGb, "<", RuleEngineConstants.DiskCleanupGb, 0.7, "Disk", "DiskCleanup"));
            AddRule(MakeRule2("r20", "High performance with thermal risk", "power.highperf_hot",
                new[] { RuleEngineConstants.Vars.PowerPlanHighPerf, RuleEngineConstants.Vars.CpuTemp },
                new[] { "==", ">" },
                new[] { 1.0, RuleEngineConstants.HighThermal },
                new[] { 1.0, 1.0 }, 0.85, "Thermal", "ReduceThermal"));
            AddRule(MakeRule("r21", "Heavy multi-tasking load", "workload.multitasking", RuleEngineConstants.Vars.ProcessCount, ">", 120, 0.7, "CPU", "LimitBackground"));
            AddRule(MakeRule("r22", "FPS above target", "gaming.fps.healthy", RuleEngineConstants.Vars.Fps, ">", 60, 0.7, "Gaming", null));

            // --- Windows services health ---
            AddRule(MakeRule("r23", "Many services stopped", "services.stopped.high", RuleEngineConstants.Vars.ServicesStopped, ">", RuleEngineConstants.HighStoppedServices, 0.75, "Services", "StartService"));

            // --- Initial statistics (priors so the system has useful early knowledge) ---
            SeedStat("PurgeStandbyList", 0.85, 12, 0.25, "Memory");
            SeedStat("LightClean", 0.80, 8, 0.20, "Memory");
            SeedStat("AggressiveClean", 0.70, 12, 0.30, "Memory");
            SeedStat("BoostGamePriority", 0.85, 9, 0.20, "Process");
            SeedStat("LimitBackground", 0.75, 7, 0.20, "Process");
            SeedStat("UnparkAllCores", 0.70, 5, 0.15, "Process");
            SeedStat("EmergencyThrottle", 0.90, 15, 0.10, "Thermal");
            SeedStat("ReduceThermal", 0.80, 10, 0.20, "Thermal");
            SeedStat("BatterySaver", 0.85, 6, 0.10, "Power");
            SeedStat("BalancedPerformance", 0.75, 5, 0.10, "Power");
            SeedStat("SwitchPowerPlan", 0.70, 4, 0.10, "Power");
            SeedStat("SetGameAffinity", 0.80, 8, 0.15, "Process");
            SeedStat("SetLowIoPriority", 0.75, 6, 0.15, "Disk");
            SeedStat("OptimizeNetwork", 0.65, 5, 0.20, "Network");
            SeedStat("FlushDnsCache", 0.70, 4, 0.05, "Network");
            SeedStat("StabilizeFrames", 0.80, 10, 0.20, "Process");
            SeedStat("SuggestClose", 0.60, 3, 0.10, "Process");
            SeedStat("DiskCleanup", 0.70, 4, 0.10, "Disk");
            SeedStat("StartupDisable", 0.65, 4, 0.15, "Startup");
            SeedStat("StartService", 0.60, 3, 0.10, "Services");
        }

        private static CausalLink MakeLink(string id, string cause, string effect, double strength, string mechanism, string category)
        {
            return new CausalLink { Id = id, Cause = cause, Effect = effect, Strength = strength, Mechanism = mechanism, Category = category };
        }

        private static ExpertRule MakeRule(string id, string name, string conclusion, string variable, string op, double value, double confidence, string category, string action)
        {
            return new ExpertRule
            {
                Id = id,
                Name = name,
                Description = name,
                Category = category,
                Conclusion = conclusion,
                Confidence = confidence,
                RecommendedAction = action,
                Conditions = new List<RuleCondition>
                {
                    new RuleCondition { Variable = variable, Operator = op, Value = value, Weight = 1.0 }
                }
            };
        }

        /// <summary>Builds a rule with an arbitrary set of AND conditions.</summary>
        private static ExpertRule MakeRule2(string id, string name, string conclusion,
            string[] variables, string[] ops, double[] values, double[] weights,
            double confidence, string category, string action)
        {
            var rule = new ExpertRule
            {
                Id = id,
                Name = name,
                Description = name,
                Category = category,
                Conclusion = conclusion,
                Confidence = confidence,
                RecommendedAction = action
            };
            for (int i = 0; i < variables.Length; i++)
            {
                rule.Conditions.Add(new RuleCondition
                {
                    Variable = variables[i],
                    Operator = ops[i],
                    Value = values[i],
                    Weight = weights[i]
                });
            }
            return rule;
        }

        private void SeedStat(string action, double successRate, int uses, double risk, string category)
        {
            var s = new ActionStatistics
            {
                ActionId = action,
                Category = category,
                SuccessRate = successRate,
                Confidence = Math.Min(1.0, successRate * 0.7 + 0.3),
                AvgRisk = risk,
                TotalUses = uses,
                Successes = (int)Math.Round(uses * successRate),
                LastUsed = DateTime.Now.AddHours(-1)
            };
            _stats[action] = s;
        }

        // ============================ PERSISTENCE ============================
        // V2 stores each rule's full condition list so rules survive restarts
        // with their guards intact (V1 silently dropped Conditions).
        private const string FileHeaderV2 = "CLEARWIN_EXPERT_KB_V2";

        public void Save()
        {
            try
            {
                if (string.IsNullOrEmpty(_dataDir)) return;
                string dir = Path.Combine(_dataDir, "expert");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "expert_kb.dat");
                var sb = new StringBuilder();
                sb.AppendLine(FileHeaderV2);
                lock (_lock)
                {
                    foreach (var f in _facts.Values)
                        sb.AppendLine("F|" + f.Variable + "|" + f.Value.ToString("R") + "|" + f.Confidence.ToString("R") + "|" + f.Source + "|" + f.Timestamp.Ticks);
                    foreach (var r in _rules.Values)
                    {
                        List<RuleCondition> conds = r.Conditions != null ? r.Conditions : new List<RuleCondition>();
                        string line = "R|" + r.Id + "|" + r.Name + "|" + r.Description + "|" + r.Category + "|" + r.Conclusion + "|" +
                            r.Confidence.ToString("R") + "|" + (r.RecommendedAction ?? "") + "|" + (r.Enabled ? "1" : "0") + "|" +
                            r.MatchCount + "|" + r.SuccessCount + "|" + conds.Count;
                        for (int i = 0; i < conds.Count; i++)
                        {
                            line += "|" + conds[i].Variable + "|" + conds[i].Operator + "|" +
                                conds[i].Value.ToString("R") + "|" + conds[i].Value2.ToString("R") + "|" +
                                conds[i].Weight.ToString("R");
                        }
                        sb.AppendLine(line);
                    }
                    foreach (var l in _links)
                        sb.AppendLine("L|" + l.Id + "|" + l.Cause + "|" + l.Effect + "|" + l.Strength.ToString("R") + "|" + l.Mechanism + "|" + l.Category + "|" + l.Samples);
                    foreach (var s in _stats.Values)
                        sb.AppendLine("S|" + s.ActionId + "|" + (s.Category ?? "") + "|" + s.SuccessRate.ToString("R") + "|" + s.TotalUses + "|" +
                            s.AvgRisk.ToString("R") + "|" + s.Confidence.ToString("R") + "|" + s.Successes + "|" + s.Failures + "|" + s.AvgImpact.ToString("R"));
                }
                File.WriteAllText(path, sb.ToString());
            }
            catch
            {
                // Persistence must never break the application.
            }
        }

        public void Load()
        {
            try
            {
                if (string.IsNullOrEmpty(_dataDir)) return;
                string path = Path.Combine(_dataDir, "expert", "expert_kb.dat");
                if (!File.Exists(path)) return;
                string[] lines = File.ReadAllLines(path);
                // V1 files were written without rule conditions (rules always
                // fired after restart) - treat as legacy and let the system
                // re-seed a correct knowledge base instead of loading them.
                if (lines.Length == 0 || lines[0] != FileHeaderV2) return;
                lock (_lock)
                {
                    for (int i = 1; i < lines.Length; i++)
                    {
                        string[] p = lines[i].Split('|');
                        if (p.Length < 2) continue;
                        try
                        {
                            switch (p[0])
                            {
                                case "F":
                                    if (p.Length >= 6)
                                        _facts[p[1]] = new ExpertFact { Variable = p[1], Value = double.Parse(p[2]), Confidence = double.Parse(p[3]), Source = p[4], Timestamp = new DateTime(long.Parse(p[5])) };
                                    break;
                                case "R":
                                    if (p.Length >= 8)
                                    {
                                        int nConds = p.Length > 11 ? int.Parse(p[11]) : 0;
                                        var rule = new ExpertRule
                                        {
                                            Id = p[1],
                                            Name = p[2],
                                            Description = p[3],
                                            Category = p[4],
                                            Conclusion = p[5],
                                            Confidence = double.Parse(p[6]),
                                            RecommendedAction = p[7],
                                            Enabled = p.Length > 8 && p[8] == "1",
                                            MatchCount = p.Length > 9 ? int.Parse(p[9]) : 0,
                                            SuccessCount = p.Length > 10 ? int.Parse(p[10]) : 0
                                        };
                                        int idx = 12;
                                        for (int c = 0; c < nConds && idx + 4 < p.Length; c++)
                                        {
                                            rule.Conditions.Add(new RuleCondition
                                            {
                                                Variable = p[idx],
                                                Operator = p[idx + 1],
                                                Value = double.Parse(p[idx + 2]),
                                                Value2 = double.Parse(p[idx + 3]),
                                                Weight = double.Parse(p[idx + 4])
                                            });
                                            idx += 5;
                                        }
                                        if (rule.Enabled && rule.Conditions.Count > 0)
                                            _rules[rule.Id] = rule;
                                    }
                                    break;
                                case "L":
                                    if (p.Length >= 6)
                                        _links.Add(new CausalLink { Id = p[1], Cause = p[2], Effect = p[3], Strength = double.Parse(p[4]), Mechanism = p[5], Category = p.Length >= 7 ? p[6] : "", Samples = p.Length >= 8 ? int.Parse(p[7]) : 0 });
                                    break;
                                case "S":
                                    if (p.Length >= 7)
                                        _stats[p[1]] = new ActionStatistics
                                        {
                                            ActionId = p[1],
                                            Category = p[2],
                                            SuccessRate = double.Parse(p[3]),
                                            TotalUses = int.Parse(p[4]),
                                            AvgRisk = double.Parse(p[5]),
                                            Confidence = double.Parse(p[6]),
                                            Successes = p.Length > 7 ? int.Parse(p[7]) : 0,
                                            Failures = p.Length > 8 ? int.Parse(p[8]) : 0,
                                            AvgImpact = p.Length > 9 ? double.Parse(p[9]) : 0,
                                            LastUsed = DateTime.Now
                                        };
                                    break;
                            }
                        }
                        catch
                        {
                            // Skip malformed lines.
                        }
                    }
                }
            }
            catch
            {
                // Corrupt file must never break the application.
            }
        }
    }
}
