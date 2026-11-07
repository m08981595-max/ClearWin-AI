using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ClearWin
{
    /// <summary>
    /// Persistent AI memory wiring: subscribes to the agent's decision /
    /// execution / reward events and records them in an on-disk audit trail
    /// (<see cref="AiPersistentMemory"/>) that the Web UI settings page reads.
    /// </summary>
    public partial class MainWindow
    {
        private AiPersistentMemory _aiMemory;
        private List<IDisposable> _aiMemorySubs;

        /// <summary>Creates + loads the persistent store and hooks the agent event bus.</summary>
        public void InitializePersistentMemory()
        {
            try
            {
                if (_aiMemory != null) return;
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ClearWin", "agent");
                Directory.CreateDirectory(dir);
                _aiMemory = new AiPersistentMemory(Path.Combine(dir, "ai_memory.dat"));
                _aiMemory.Load();
                _aiMemory.Record("session", "ClearWin",
                    "جلسة تشغيل جديدة - بدء تخزين الذاكرة الدائمة للذكاء الاصطناعي", 0, true);

                if (_agent != null && _agent.Events != null)
                {
                    _aiMemorySubs = new List<IDisposable>();
                    _aiMemorySubs.Add(_agent.Events.Subscribe<DecisionExecutedEvent>(
                        e => OnPersistentDecisionExecuted(e)));
                    _aiMemorySubs.Add(_agent.Events.Subscribe<RewardRecordedEvent>(
                        e => OnPersistentReward(e)));
                }
                CWLog.Info("Memory", "persistent AI memory initialized");
            }
            catch (Exception ex) { CWLog.Error("Memory.Init", ex); }
        }

        /// <summary>Persists a decision the agent actually applied (success/failure).</summary>
        private void OnPersistentDecisionExecuted(DecisionExecutedEvent e)
        {
            try
            {
                if (_aiMemory == null || e == null) return;
                string action = (e.Decision != null && !string.IsNullOrEmpty(e.Decision.Action))
                    ? e.Decision.Action : "?";
                _aiMemory.Record("executed", action,
                    e.Success ? "تم تطبيق القرار بنجاح" : "فشل تطبيق القرار (سُجِّلت عقوبة)",
                    0, e.Success);
                
                // Update UI penalty counter on failure
                if (!e.Success)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            UpdatePenaltyCounters();
                        }
                        catch (Exception ex2) { CWLog.Error("Memory.UpdatePenaltyUI", ex2); }
                    }));
                }
            }
            catch (Exception ex) { CWLog.Error("Memory.DecisionExecuted", ex); }
        }

        /// <summary>Persists a measured outcome: positive = reward, negative = penalty.</summary>
        private void OnPersistentReward(RewardRecordedEvent e)
        {
            try
            {
                if (_aiMemory == null || e == null) return;
                bool positive = e.Reward >= 0;
                _aiMemory.Record(positive ? "reward" : "penalty", e.Action ?? "?",
                    (positive ? "مكافأة مقاسة: " : "عقوبة مقاسة: ")
                        + e.Reward.ToString("F3", CultureInfo.InvariantCulture),
                    e.Reward, positive);
                
                // Update UI penalty counter for negative rewards
                if (!positive)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            UpdatePenaltyCounters();
                        }
                        catch (Exception ex2) { CWLog.Error("Memory.UpdateRewardUI", ex2); }
                    }));
                }
            }
            catch (Exception ex) { CWLog.Error("Memory.Reward", ex); }
        }

        /// <summary>Updates penalty/abstention counters in the Agent page UI.</summary>
        private void UpdatePenaltyCounters()
        {
            try
            {
                if (_aiMemory == null || !_agentPageActive) return;
                
                int total, decisions, execOk, execFail, rewards, penalties;
                double bestReward, worstPenalty;
                DateTime first, last;
                _aiMemory.GetSummary(out total, out decisions, out execOk, out execFail,
                    out rewards, out penalties, out bestReward, out worstPenalty,
                    out first, out last);
                
                // Update abstention counter (execution failures)
                if (_penaltyAbstainTxt != null)
                {
                    _penaltyAbstainTxt.Text = execFail.ToString(CultureInfo.InvariantCulture);
                }
                
                // Update penalty counter (negative rewards)
                if (_penaltyRiskBlockedTxt != null)
                {
                    _penaltyRiskBlockedTxt.Text = string.Format("{0} عقوبة | أسوأ: {1:F2}",
                        penalties, worstPenalty);
                }
                
                // Update status
                if (_penaltyStatusTxt != null)
                {
                    double failRate = execOk + execFail > 0 ? (execFail * 100.0 / (execOk + execFail)) : 0;
                    _penaltyStatusTxt.Text = failRate < 5 ? "ممتاز" : failRate < 15 ? "جيد" : "يحتاج مراجعة";
                }
            }
            catch (Exception ex) { CWLog.Error("Memory.UpdatePenaltyCounters", ex); }
        }

        /// <summary>Persists a decision the agent made (reason + confidence + risk).</summary>
        public void RecordPersistentDecision(AgentDecision d)
        {
            try
            {
                if (_aiMemory == null || d == null) return;
                string why = !string.IsNullOrEmpty(d.Reason) ? d.Reason
                    : (!string.IsNullOrEmpty(d.Description) ? d.Description : "");
                string detail = "قرار [" + d.Category + "] ثقة="
                    + d.Confidence.ToString("F2", CultureInfo.InvariantCulture)
                    + " مخاطر=" + d.Risk.ToString("F2", CultureInfo.InvariantCulture);
                if (why.Length > 0) detail += " — " + why;
                _aiMemory.Record("decision", d.Action ?? "?", detail, d.Risk, d.Success);
            }
            catch (Exception ex) { CWLog.Error("Memory.Decision", ex); }
        }

        /// <summary>Full store as a JSON payload for the Web UI settings page.</summary>
        public string BuildAiMemoryJson(int recentCount)
        {
            if (_aiMemory == null) return "{\"type\":\"aimemory\",\"total\":0,\"entries\":[]}";
            return _aiMemory.ToJson(recentCount);
        }

        public bool HasAiMemory
        {
            get { return _aiMemory != null; }
        }

        public AiPersistentMemory AiMemory
        {
            get { return _aiMemory; }
        }
    }
}
