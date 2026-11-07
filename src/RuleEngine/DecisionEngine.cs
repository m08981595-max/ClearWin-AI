using System;
using System.Collections.Generic;

namespace ClearWin.RuleEngine
{
    /// <summary>
    /// Decision Engine: scores and compares multiple candidate solutions using
    /// multi-criteria analysis (impact, urgency, confidence, risk, cost) plus
    /// knowledge-base evidence, then selects the best-fitting alternative.
    /// </summary>
    public class DecisionEngine
    {
        private readonly RuleKnowledgeBase _kb;

        public DecisionEngine(RuleKnowledgeBase kb)
        {
            _kb = kb;
        }

        /// <summary>Computes a 0..100 score for one candidate in the current context.</summary>
        public double Evaluate(CandidateAction action, SystemState state, DeviceSnapshot device)
        {
            if (action == null) return 0;

            double wImpact = RuleEngineConstants.WeightImpact;
            double wUrgency = RuleEngineConstants.WeightUrgency;
            double wConfidence = RuleEngineConstants.WeightConfidence;
            double wRisk = RuleEngineConstants.WeightRisk;
            double wCost = RuleEngineConstants.WeightCost;

            // Impact score: ExpectedImpact scaled 0..1, boosted by metric relevance.
            double impact = Math.Max(0, Math.Min(1, action.ExpectedImpact / 100.0));
            impact *= 0.7 + 0.3 * MetricRelevance(action, state);

            double urgency = Math.Max(0, Math.Min(1, action.Urgency));
            double confidence = Math.Max(0, Math.Min(1, action.Confidence));
            double riskPenalty = Math.Max(0, Math.Min(1, action.Risk));
            double costPenalty = Math.Max(0, Math.Min(1, action.Cost / 10.0));

            // Feasibility from knowledge base: does history say this action works here?
            double kbBoost = 0;
            ActionStatistics stats = _kb != null ? _kb.GetStatistics(action.Id) : null;
            if (stats != null && stats.TotalUses >= RuleEngineConstants.LearningMinSamples)
            {
                kbBoost = (stats.SuccessRate - 0.5) * 0.5; // -0.25..+0.25
            }

            double score = (impact * wImpact + urgency * wUrgency + confidence * wConfidence
                - riskPenalty * wRisk - costPenalty * wCost) * 100.0 + kbBoost * 100.0;
            action.Score = Math.Max(0, Math.Min(100, score));
            return action.Score;
        }

        /// <summary>How relevant this action's affected metrics are to the current state.</summary>
        private double MetricRelevance(CandidateAction action, SystemState state)
        {
            if (state == null || action.AffectsMetrics == null || action.AffectsMetrics.Count == 0) return 0.5;
            double total = 0;
            for (int i = 0; i < action.AffectsMetrics.Count; i++)
            {
                string m = action.AffectsMetrics[i];
                if (m == null) continue;
                if (m.IndexOf("cpu", StringComparison.OrdinalIgnoreCase) >= 0)
                    total += Math.Min(1, state.CpuUsage / 100.0);
                else if (m.IndexOf("ram", StringComparison.OrdinalIgnoreCase) >= 0 || m.IndexOf("memory", StringComparison.OrdinalIgnoreCase) >= 0)
                    total += Math.Min(1, state.RamUsage / 100.0);
                else if (m.IndexOf("temp", StringComparison.OrdinalIgnoreCase) >= 0 || m.IndexOf("thermal", StringComparison.OrdinalIgnoreCase) >= 0)
                    total += Math.Min(1, state.CpuTemp / 100.0);
                else if (m.IndexOf("fps", StringComparison.OrdinalIgnoreCase) >= 0 || m.IndexOf("game", StringComparison.OrdinalIgnoreCase) >= 0)
                    total += state.Fps <= 0 ? 0.3 : (1 - Math.Min(1, state.Fps / 120.0));
                else if (m.IndexOf("disk", StringComparison.OrdinalIgnoreCase) >= 0)
                    total += Math.Min(1, state.DiskUsage / 100.0);
                else if (m.IndexOf("battery", StringComparison.OrdinalIgnoreCase) >= 0)
                    total += state.IsCharging ? 0.2 : (1 - Math.Min(1, state.BatteryLevel / 100.0));
                else
                    total += 0.5;
            }
            return total / action.AffectsMetrics.Count;
        }

        /// <summary>Ranks all candidates by score (descending) and fills in Score values.</summary>
        public List<CandidateAction> Rank(List<CandidateAction> candidates, SystemState state, DeviceSnapshot device)
        {
            if (candidates == null) return new List<CandidateAction>();
            for (int i = 0; i < candidates.Count; i++)
                Evaluate(candidates[i], state, device);
            candidates.Sort(delegate(CandidateAction a, CandidateAction b) { return b.Score.CompareTo(a.Score); });
            return candidates;
        }

        /// <summary>Selects the single best alternative for a given purpose.</summary>
        public CandidateAction SelectBest(List<CandidateAction> candidates, SystemState state, DeviceSnapshot device)
        {
            var ranked = Rank(candidates, state, device);
            return ranked.Count > 0 ? ranked[0] : null;
        }

        /// <summary>Head-to-head comparison used by the explain engine.</summary>
        public string Compare(CandidateAction a, CandidateAction b)
        {
            if (a == null && b == null) return "No alternatives to compare.";
            if (a == null) return "Only " + b.Name + " is feasible.";
            if (b == null) return "Only " + a.Name + " is feasible.";
            return string.Format("'{0}' scores {1:F0}/100 vs '{2}' at {3:F0}/100",
                a.Name, a.Score, b.Name, b.Score);
        }

        /// <summary>
        /// Generates the candidate solution set for the current context.
        /// This is the Decision Engine's "idea space" before ranking.
        /// </summary>
        public List<CandidateAction> GenerateCandidates(ExpertDiagnosis diagnosis, SystemState state,
            Dictionary<string, DateTime> safetyBlocked)
        {
            return GenerateCandidates(diagnosis, state, safetyBlocked, null);
        }

        /// <summary>
        /// Generates candidates, honoring reasoning hints (recommended actions)
        /// and skipping recently-blocked advisory actions within the cooldown
        /// window. Safety-critical actions are never skipped.
        /// </summary>
        public List<CandidateAction> GenerateCandidates(ExpertDiagnosis diagnosis, SystemState state,
            Dictionary<string, DateTime> safetyBlocked, List<string> hintedActions)
        {
            var candidates = new List<CandidateAction>();
            if (state == null) return candidates;

            bool thermal = state.CpuTemp >= RuleEngineConstants.HighThermal;
            bool criticalThermal = state.CpuTemp >= RuleEngineConstants.CriticalThermal;
            bool ramHigh = state.RamUsage >= RuleEngineConstants.HighRam;
            bool ramCritical = state.RamUsage >= RuleEngineConstants.CriticalRam;
            bool cpuHigh = state.CpuUsage >= RuleEngineConstants.HighCpu;
            bool game = state.IsGame;
            bool lowFps = game && state.Fps > 0 && state.Fps < RuleEngineConstants.LowFps;
            bool batteryLow = !state.IsCharging && state.BatteryLevel <= RuleEngineConstants.BatteryLow;
            bool freeRamLow = (state.TotalRamGB - state.UsedRamGB) < 1.5;

            DateTime now = DateTime.Now;
            DateTime blockWindowStart = now.AddMinutes(-10);

            // Thermal responses (safety-critical - never skipped).
            if (criticalThermal)
                candidates.Add(MakeCandidate("EmergencyThrottle", ActionClass.Thermal, "Forced power saver at critical temperature",
                    "يخفض استهلاك الطاقة فورًا عند درجة حرارة خطرة", 15, 0.10, 6, 0.95, 1.0, true, "Restore power plan", "cpu,temp,ram,fps",
                    string.Format("CPU temperature {0:F0}C exceeds critical threshold", state.CpuTemp), "emergency.throttle"));
            else if (thermal)
                candidates.Add(MakeCandidate("ReduceThermal", ActionClass.Thermal, "Lower system load to reduce heat",
                    "يخفض حمل النظام لتقليل الحرارة", 18, 0.20, 5, 0.80, 0.7, true, "Restore load targets", "cpu,temp,ram,fps",
                    string.Format("CPU temperature {0:F0}C above warning threshold", state.CpuTemp), "reduce.thermal"));

            // Memory responses.
            if (ramCritical || freeRamLow)
                candidates.Add(MakeCandidate("AggressiveClean", ActionClass.Memory, "Deep clean of memory-heavy working sets",
                    "تنظيف عميق لذواكر العمليات الثقيلة", 20, 0.30, 5, 0.70, 0.9, true, "Reallocate memory naturally", "ram,fps",
                    ramCritical ? string.Format("RAM usage {0:F0}% is critical", state.RamUsage) : "Free RAM below 1.5GB", "aggressive.clean"));
            else if (ramHigh)
                candidates.Add(MakeCandidate("LightClean", ActionClass.Memory, "Gentle clean of standby memory lists",
                    "تنظيف خفيف لقوائم الذاكرة الاحتياطية", 12, 0.20, 3, 0.80, 0.5, true, "Reallocate memory naturally", "ram,fps",
                    string.Format("RAM usage {0:F0}% is high", state.RamUsage), "light.clean"));
            if (state.RamUsage >= 60 && !ramHigh)
                candidates.Add(MakeCandidate("PurgeStandbyList", ActionClass.Memory, "Purge cached standby pages",
                    "تفريغ الصفحات المخزنة مؤقتًا", 8, 0.15, 5, 0.75, 0.3, true, "Standby list refills automatically", "ram",
                    "Standby cache is large and RAM pressure is moderate", "purge.standby"));

            // CPU / game responses.
            if (game && (lowFps || cpuHigh))
            {
                candidates.Add(MakeCandidate("BoostGamePriority", ActionClass.Process, "Raise game process priority to High",
                    "رفع أولوية عملية اللعبة إلى عالية", 14, 0.20, 4, 0.85, 0.7, true, "Restore normal priority",                     "cpu,fps",
                    lowFps ? string.Format("FPS {0:F0} below target while gaming", state.Fps) : "CPU contention while gaming", "game.priority"));
                candidates.Add(MakeCandidate("SetGameAffinity", ActionClass.Process, "Pin game to performance cores",
                    "تثبيت اللعبة على الأنوية عالية الأداء", 10, 0.15, 3, 0.80, 0.5, true, "Restore full affinity", "cpu,fps",
                    "Game running alongside background load", "game.affinity"));
            }
            if (cpuHigh)
                candidates.Add(MakeCandidate("LimitBackground", ActionClass.Process, "Reduce CPU priority of background apps",
                    "تقليل أولوية تطبيقات الخلفية", 13, 0.20, 3, 0.75, 0.6, true, "Restore background priority", "cpu,ram",
                    cpuHigh ? string.Format("CPU usage {0:F0}% with many background apps", state.CpuUsage) : "CPU contention", "limit.background"));
            if (state.ProcessCount > RuleEngineConstants.HighProcessCount)
                candidates.Add(MakeCandidate("SuggestClose", ActionClass.Process, "Suggest closing unnecessary processes",
                    "اقتراح إغلاق العمليات غير الضرورية", 6, 0.10, 1, 0.60, 0.3, true, "User reopens apps", "cpu,ram",
                    state.ProcessCount + " processes running", "suggest.close"));

            // Disk responses.
            if (state.DiskUsage >= RuleEngineConstants.HighDisk)
                candidates.Add(MakeCandidate("SetLowIoPriority", ActionClass.Disk, "Lower I/O priority of background processes",
                    "تقليل أولوية الإدخال/الإخراج للخلفية", 8, 0.15, 2, 0.75, 0.4, true, "Restore I/O priority", "disk,fps",
                    state.DiskUsage.ToString("F0") + "% disk activity", "disk.io"));

            // Power responses.
            if (batteryLow)
                candidates.Add(MakeCandidate("BatterySaver", ActionClass.Power, "Enable battery-saving power plan",
                    "تفعيل خطة توفير البطارية", 12, 0.10, 2, 0.85, 0.8, true, "Restore balanced plan", "battery,temp,cpu",
                    batteryLow ? string.Format("Battery at {0:F0}% while unplugged", state.BatteryLevel) : "Low battery", "battery.saver"));
            else if (!batteryLow && cpuHigh && !game)
                candidates.Add(MakeCandidate("BalancedPerformance", ActionClass.Power, "Apply balanced power profile",
                    "تطبيق ملف طاقة متوازن", 7, 0.10, 2, 0.75, 0.3, true, "Restore previous plan", "cpu,temp",
                    "Balanced power profile for mixed workload", "power.balanced"));
            else if (!batteryLow && game && IsPowerSaverPlan(state.PowerPlan))
                candidates.Add(MakeCandidate("SwitchPowerPlan", ActionClass.Power, "Switch to high performance power plan",
                    "التبديل إلى خطة الطاقة عالية الأداء", 11, 0.15, 3, 0.75, 0.7, true, "Restore previous plan", "cpu,fps,temp",
                    "Power saver plan active while gaming caps performance", "power.high_perf"));

            // Network responses.
            if (diagnosis != null && diagnosis.Category != null &&
                diagnosis.Category.IndexOf("network", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                candidates.Add(MakeCandidate("OptimizeNetwork", ActionClass.Network, "Reset TCP/IP + Winsock for clean networking",
                    "إعادة ضبط إعدادات الشبكة لتحسين الأداء", 9, 0.20, 4, 0.65, 0.6, true, "Network config restored on reboot", "network,fps",
                    "Network saturation detected while connected", "network.optimize"));
                candidates.Add(MakeCandidate("FlushDnsCache", ActionClass.Network, "Flush DNS resolver cache",
                    "مسح ذاكرة DNS المؤقتة", 5, 0.05, 2, 0.70, 0.3, true, "Cache refills automatically", "network",
                    "Network category diagnosis", "network.dns"));
            }

            // Startup responses.
            if (diagnosis != null && diagnosis.Category != null &&
                diagnosis.Category.IndexOf("startup", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                candidates.Add(MakeCandidate("StartupDisable", ActionClass.Startup, "Disable non-essential startup programs",
                    "تعطيل برامج بدء التشغيل غير الضرورية", 9, 0.15, 3, 0.65, 0.5, true, "Re-enable from Task Manager", "cpu,ram",
                    "Startup bloat detected on this system", "startup.disable"));
                candidates.Add(MakeCandidate("SuggestClose", ActionClass.Process, "Suggest closing unnecessary processes",
                    "اقتراح إغلاق العمليات غير الضرورية", 6, 0.10, 1, 0.60, 0.3, true, "User reopens apps", "cpu,ram",
                    "Startup bloat leaves many background apps", "suggest.close"));
            }

            // Disk cleanup responses (moderate free disk, not just critical).
            if (diagnosis != null && diagnosis.Category != null &&
                diagnosis.Category.IndexOf("disk", StringComparison.OrdinalIgnoreCase) >= 0 &&
                deviceDiskLow(state))
            {
                candidates.Add(MakeCandidate("DiskCleanup", ActionClass.Disk, "Free up system drive space",
                    "تحرير مساحة على قرص النظام", 8, 0.10, 3, 0.70, 0.4, true, "Space reused by new files", "disk,fps",
                    "System drive free space is getting low", "disk.cleanup"));
            }
            else if (hintedActions != null && ContainsAction(hintedActions, "DiskCleanup"))
            {
                candidates.Add(MakeCandidate("DiskCleanup", ActionClass.Disk, "Free up system drive space",
                    "تحرير مساحة على قرص النظام", 8, 0.10, 3, 0.70, 0.4, true, "Space reused by new files", "disk,fps",
                    "System drive free space is getting low", "disk.cleanup"));
            }

            // Services responses.
            if (diagnosis != null && diagnosis.Category != null &&
                diagnosis.Category.IndexOf("service", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                candidates.Add(MakeCandidate("StartService", ActionClass.Service, "Start critical Windows services that stopped",
                    "تشغيل الخدمات الحرجة المتوقفة", 8, 0.10, 2, 0.65, 0.6, true, "Services can be stopped again", "cpu,ram,sys",
                    "Critical system services are unexpectedly stopped", "service.restart"));
            }

            // Apply KB priors (confidence, impact) to every candidate.
            var seenIds = new HashSet<string>();
            for (int i = 0; i < candidates.Count; i++)
            {
                CandidateAction c = candidates[i];
                if (c.Id == null || seenIds.Contains(c.Id))
                {
                    candidates.RemoveAt(i);
                    i--;
                    continue;
                }
                if (c.Id != null) seenIds.Add(c.Id);
                ActionStatistics stats = _kb != null ? _kb.GetStatistics(c.Id) : null;
                if (stats != null)
                {
                    c.Confidence = stats.Confidence;
                    c.ExpectedImpact = Math.Max(c.ExpectedImpact, stats.AvgImpact * 2.0 + 2.0);
                }
                // Reasoning hints (rule RecommendedAction / diagnosis hint) raise urgency.
                if (hintedActions != null && hintedActions.Count > 0 &&
                    ContainsAction(hintedActions, c.Id))
                {
                    c.Urgency = Math.Min(1.0, c.Urgency + 0.3);
                }
                if (diagnosis != null && !string.IsNullOrEmpty(diagnosis.RecommendedHint) &&
                    string.Equals(diagnosis.RecommendedHint, c.Id, StringComparison.OrdinalIgnoreCase))
                {
                    c.Urgency = Math.Min(1.0, c.Urgency + 0.4);
                }
                // Skip recently-blocked advisory actions (never safety-critical).
                if (safetyBlocked != null && safetyBlocked.ContainsKey(c.Id))
                {
                    DateTime blockedAt;
                    if (safetyBlocked.TryGetValue(c.Id, out blockedAt) && blockedAt >= blockWindowStart &&
                        !IsSafetyCritical(c.Id))
                    {
                        candidates.RemoveAt(i);
                        i--;
                        continue;
                    }
                }
            }

            return candidates;
        }

        private static bool IsPowerSaverPlan(string powerPlan)
        {
            if (string.IsNullOrEmpty(powerPlan)) return false;
            string p = powerPlan.ToLowerInvariant();
            return p.IndexOf("power saver", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   p.IndexOf("توفير", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool deviceDiskLow(SystemState state)
        {
            // Free disk is only known from the device snapshot, so approximate
            // with disk usage; the diagnostic engine passes DiskCleanupNeeded
            // via its recommended hint in most cases.
            return state.DiskUsage >= 70;
        }

        private static bool IsSafetyCritical(string id)
        {
            return id == "EmergencyThrottle" || id == "ReduceThermal" || id == "BatterySaver";
        }

        private static bool ContainsAction(List<string> list, string id)
        {
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i], id, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private CandidateAction MakeCandidate(string id, ActionClass cls, string desc, string descAr,
            double impact, double risk, double cost, double confidence, double urgency, bool reversible,
            string revert, string metrics, string evidence, string conflictGroup)
        {
            var c = new CandidateAction
            {
                Id = id,
                Name = id,
                NameAr = descAr,
                Class = cls,
                Category = cls.ToString(),
                Description = desc,
                ExpectedImpact = impact,
                Risk = risk,
                Cost = cost,
                Confidence = confidence,
                Urgency = urgency,
                Reversible = reversible,
                RevertMethod = revert,
                Evidence = evidence,
                WhyItHelps = desc,
                WhyItHelpsAr = descAr,
                Score = 0
            };
            c.AffectsMetrics.AddRange(SplitMetrics(metrics));
            if (conflictGroup != null)
            {
                foreach (string other in GroupMembers(conflictGroup))
                {
                    if (other != id) c.ConflictsWith.Add(other);
                }
            }
            return c;
        }

        private static string[] SplitMetrics(string metrics)
        {
            if (string.IsNullOrEmpty(metrics)) return new string[0];
            return metrics.Split(',');
        }

        /// <summary>Mutual-exclusion groups: actions in the same group conflict with each other.</summary>
        private string[] GroupMembers(string group)
        {
            switch (group)
            {
                case "emergency.throttle":
                    return new[] { "EmergencyThrottle", "BatterySaver", "UnparkAllCores", "BalancedPerformance" };
                case "battery.saver":
                    return new[] { "BatterySaver", "BalancedPerformance", "EmergencyThrottle" };
                case "power.balanced":
                    return new[] { "BalancedPerformance", "BatterySaver" };
                case "game.priority":
                    return new[] { "BoostGamePriority", "LimitBackgroundAffinity", "SetGameAffinity" };
                case "game.affinity":
                    return new[] { "SetGameAffinity", "BoostGamePriority" };
                default:
                    return new string[0];
            }
        }
    }
}
