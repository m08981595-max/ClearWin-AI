using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ClearWin
{
    // =====================================================================
    // REAL STATE-AWARE DOUBLE Q-LEARNING (RL2) + SHADOW MODE (Phase 5)
    // Implements genuine symmetric Hasselt (2010) Double Q-Learning across
    // the full 20-action ML_AC + _actionValues space, visit-aware seeding,
    // and Shadow Mode telemetry (with optional gated live influence).
    // =====================================================================
    public partial class SmartAgent
    {
        private Dictionary<string, Dictionary<string, double>> _stateQ = new Dictionary<string, Dictionary<string, double>>();
        private Dictionary<string, Dictionary<string, double>> _stateQB = new Dictionary<string, Dictionary<string, double>>();
        private Dictionary<string, Dictionary<string, double>> _stateQTarget = new Dictionary<string, Dictionary<string, double>>();
        private Dictionary<string, Dictionary<string, int>> _stateVisits = new Dictionary<string, Dictionary<string, int>>();
        private List<RLTransition> _rl2Replay = new List<RLTransition>();
        private const int RL2_REPLAY_MAX = 800;
        private const int RL2_REPLAY_MIN = 24;
        private const int RL2_MAX_STATES = 400;
        private const double GAMMA = 0.9;
        private const double TAU2 = 0.01;
        private const double Q_DEFAULT = 0.5;
        private const double Q_SEED_BAD = 0.35;
        private int _rl2ReplayTick = 0;
        private int _rl2TotalUpdates = 0;
        private volatile bool _rl2Deferred = false;
        public bool IsRL2Deferred { get { return _rl2Deferred; } set { _rl2Deferred = value; } }

        // Shadow Mode & Gated Optional Live Influence (Phase 5)
        // Disabled by default so RL2 runs in safe Shadow Mode (observing and comparing against
        // RankAndFilter without mutating live Windows decisions unless explicitly enabled).
        private volatile bool _enableRL2LiveInfluence = false;
        private long _rl2ShadowComparisons = 0;
        private long _rl2ShadowAgreements = 0;
        private string _rl2LastShadowAction = "NONE";
        private string _rl2LastLiveAction = "NONE";

        public bool EnableRL2LiveInfluence
        {
            get { return _enableRL2LiveInfluence; }
            set { _enableRL2LiveInfluence = value; }
        }
        public long RL2ShadowComparisons { get { lock (_rl2Lock) return _rl2ShadowComparisons; } }
        public long RL2ShadowAgreements { get { lock (_rl2Lock) return _rl2ShadowAgreements; } }
        public string RL2LastShadowAction { get { lock (_rl2Lock) return _rl2LastShadowAction; } }
        public string RL2LastLiveAction { get { lock (_rl2Lock) return _rl2LastLiveAction; } }
        public double RL2ShadowAgreementRate
        {
            get
            {
                lock (_rl2Lock)
                {
                    return _rl2ShadowComparisons > 0
                        ? (double)_rl2ShadowAgreements / _rl2ShadowComparisons
                        : 0.0;
                }
            }
        }

        // Guards the RL2 tables (Q, QB, target, visits, replay) against the
        // agent tick thread and the live tweak-evaluation thread.
        private readonly object _rl2Lock = new object();

        public int GetStateAwareStateCount() { lock (_rl2Lock) return _stateQ.Count; }
        public int GetRL2UpdateCount() { lock (_rl2Lock) return _rl2TotalUpdates; }

        /// <summary>
        /// Builds the complete candidate action set for a state across ML_AC (all 20 actions),
        /// _actionValues, and any actions already stored in _stateQ / _stateQB for stateKey.
        /// Fixes the legacy bug where only _actionValues (14 actions) was iterated.
        /// </summary>
        private List<string> GetCandidateActionsForStateLocked(string stateKey)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var list = new List<string>(24);

            if (ML_AC != null)
            {
                for (int i = 0; i < ML_AC.Length; i++)
                {
                    if (!string.IsNullOrEmpty(ML_AC[i]) && seen.Add(ML_AC[i]))
                        list.Add(ML_AC[i]);
                }
            }

            if (_actionValues != null)
            {
                foreach (var kv in _actionValues)
                {
                    if (!string.IsNullOrEmpty(kv.Key) && seen.Add(kv.Key))
                        list.Add(kv.Key);
                }
            }

            if (!string.IsNullOrEmpty(stateKey))
            {
                Dictionary<string, double> rowA;
                if (_stateQ.TryGetValue(stateKey, out rowA))
                {
                    foreach (var kv in rowA)
                    {
                        if (!string.IsNullOrEmpty(kv.Key) && seen.Add(kv.Key))
                            list.Add(kv.Key);
                    }
                }
                Dictionary<string, double> rowB;
                if (_stateQB.TryGetValue(stateKey, out rowB))
                {
                    foreach (var kv in rowB)
                    {
                        if (!string.IsNullOrEmpty(kv.Key) && seen.Add(kv.Key))
                            list.Add(kv.Key);
                    }
                }
            }

            return list;
        }

        public string SelectBestStateAction(string stateKey)
        {
            return SelectBestStateAction(stateKey, null);
        }

        public string SelectBestStateAction(string stateKey, SystemState stateConstraint)
        {
            lock (_rl2Lock)
            {
                string best = "NONE";
                double bestQ = double.MinValue;
                var candidates = GetCandidateActionsForStateLocked(stateKey);
                for (int i = 0; i < candidates.Count; i++)
                {
                    string action = candidates[i];
                    if (stateConstraint != null && !IsActionApplicableToState(action, stateConstraint))
                        continue;
                    double v = GetCombinedStateQValueLocked(stateKey, action);
                    if (v > bestQ)
                    {
                        bestQ = v;
                        best = action;
                    }
                }
                return best;
            }
        }

        internal double GetCombinedStateQValue(string stateKey, string action)
        {
            lock (_rl2Lock)
            {
                return GetCombinedStateQValueLocked(stateKey, action);
            }
        }

        internal int GetStateActionVisits(string stateKey, string action)
        {
            lock (_rl2Lock)
            {
                Dictionary<string, int> vRow;
                int v;
                if (!string.IsNullOrEmpty(stateKey) &&
                    _stateVisits.TryGetValue(stateKey, out vRow) &&
                    vRow.TryGetValue(action, out v))
                {
                    return v;
                }
                return 0;
            }
        }

        private double GetCombinedStateQValueLocked(string stateKey, string action)
        {
            double qa = GetStateQValue(stateKey, action);
            double qb = GetStateQBValue(stateKey, action);
            return 0.5 * (qa + qb);
        }

        private double GetStateQValue(string stateKey, string action)
        {
            Dictionary<string, double> row;
            if (!string.IsNullOrEmpty(stateKey) && _stateQ.TryGetValue(stateKey, out row))
            {
                double v;
                if (row.TryGetValue(action, out v)) return v;
            }
            return Q_DEFAULT;
        }

        private void SetStateQValue(string stateKey, string action, double value)
        {
            Dictionary<string, double> row;
            if (!_stateQ.TryGetValue(stateKey, out row))
            {
                if (_stateQ.Count >= RL2_MAX_STATES) EvictColdestState();
                row = new Dictionary<string, double>();
                _stateQ[stateKey] = row;
            }
            row[action] = value;
        }

        // Bounds the state table: drops the least-visited state (and its target
        // / double-Q / visit rows) so continuous feature states cannot grow the
        // tables without limit.
        private void EvictColdestState()
        {
            string coldest = null; long minV = long.MaxValue;
            foreach (var kv in _stateQ)
            {
                long tot = 0;
                Dictionary<string, int> vr;
                if (_stateVisits.TryGetValue(kv.Key, out vr)) foreach (var v in vr) tot += v.Value;
                if (tot < minV) { minV = tot; coldest = kv.Key; }
            }
            if (coldest == null) return;
            _stateQ.Remove(coldest);
            _stateQB.Remove(coldest);
            _stateQTarget.Remove(coldest);
            _stateVisits.Remove(coldest);
        }

        private double GetStateQBValue(string stateKey, string action)
        {
            Dictionary<string, double> row;
            if (!string.IsNullOrEmpty(stateKey) && _stateQB.TryGetValue(stateKey, out row))
            {
                double v;
                if (row.TryGetValue(action, out v)) return v;
            }
            return Q_DEFAULT;
        }

        private void SetStateQBValue(string stateKey, string action, double value)
        {
            Dictionary<string, double> row;
            if (!_stateQB.TryGetValue(stateKey, out row))
            {
                row = new Dictionary<string, double>();
                _stateQB[stateKey] = row;
            }
            row[action] = value;
        }

        private double GetStateQTargetValue(string stateKey, string action)
        {
            Dictionary<string, double> row;
            if (!string.IsNullOrEmpty(stateKey) && _stateQTarget.TryGetValue(stateKey, out row))
            {
                double v;
                if (row.TryGetValue(action, out v)) return v;
            }
            return GetCombinedStateQValueLocked(stateKey, action);
        }

        private double GetMaxTargetValue(string stateKey)
        {
            double best = double.MinValue;
            var candidates = GetCandidateActionsForStateLocked(stateKey);
            for (int i = 0; i < candidates.Count; i++)
            {
                double v = GetStateQTargetValue(stateKey, candidates[i]);
                if (v > best) best = v;
            }
            return best == double.MinValue ? Q_DEFAULT : best;
        }

        // Returns the argmax action of online Q_A for a state (Double-Q selection for updating Q_A).
        private string GetArgMaxActionA(string stateKey)
        {
            string best = "NONE";
            double bestQ = double.MinValue;
            var candidates = GetCandidateActionsForStateLocked(stateKey);
            for (int i = 0; i < candidates.Count; i++)
            {
                string act = candidates[i];
                double v = GetStateQValue(stateKey, act);
                if (v > bestQ) { bestQ = v; best = act; }
            }
            return best;
        }

        // Returns the argmax action of online Q_B for a state (Double-Q selection for updating Q_B).
        private string GetArgMaxActionB(string stateKey)
        {
            string best = "NONE";
            double bestQ = double.MinValue;
            var candidates = GetCandidateActionsForStateLocked(stateKey);
            for (int i = 0; i < candidates.Count; i++)
            {
                string act = candidates[i];
                double v = GetStateQBValue(stateKey, act);
                if (v > bestQ) { bestQ = v; best = act; }
            }
            return best;
        }

        // Main entry: store (s, a, r, s') and perform symmetric Double Q-Learning TD update.
        public void RecordRLExperience(string stateKey, string action, double reward, string nextStateKey)
        {
            if (string.IsNullOrEmpty(stateKey) || string.IsNullOrEmpty(action)) return;
            lock (_rl2Lock)
            {
                var t = new RLTransition();
                t.StateKey = stateKey;
                t.Action = action;
                t.Reward = reward;
                t.NextStateKey = nextStateKey;
                _rl2Replay.Add(t);
                if (_rl2Replay.Count > RL2_REPLAY_MAX) _rl2Replay.RemoveAt(0);

                double lr = _adaptiveLearningRate;
                ApplyDoubleQUpdateLocked(stateKey, action, reward, nextStateKey, lr, (_rl2TotalUpdates & 1) == 0);

                Dictionary<string, int> vRow;
                if (!_stateVisits.TryGetValue(stateKey, out vRow))
                {
                    vRow = new Dictionary<string, int>();
                    _stateVisits[stateKey] = vRow;
                }
                int vc;
                vRow.TryGetValue(action, out vc);
                vRow[action] = vc + 1;

                _rl2TotalUpdates++;
                _rl2ReplayTick++;
                if (_rl2Replay.Count >= RL2_REPLAY_MIN && _rl2ReplayTick % 4 == 0)
                {
                    if (IsGamingActive())
                    {
                        _rl2Deferred = true;
                    }
                    else
                    {
                        ReplayTrainRL2();
                    }
                }
                if (_rl2TotalUpdates % 25 == 0) UpdateStateQTargets();
            }
        }

        /// <summary>
        /// Genuine Hasselt (2010) Double Q-Learning update:
        /// - When updateA is true: selects argmax_a' Q_A(s', a') and evaluates it using Q_B(s', a') (blended with target net), updating Q_A(s, a).
        /// - When updateA is false: selects argmax_a' Q_B(s', a') and evaluates it using Q_A(s', a') (blended with target net), updating Q_B(s, a).
        /// </summary>
        private void ApplyDoubleQUpdateLocked(string stateKey, string action, double reward, string nextStateKey, double lr, bool updateA)
        {
            if (updateA)
            {
                double nextVal = 0.0;
                if (!string.IsNullOrEmpty(nextStateKey))
                {
                    string argMaxA = GetArgMaxActionA(nextStateKey);
                    double evalB = GetStateQBValue(nextStateKey, argMaxA);
                    double evalTgt = GetStateQTargetValue(nextStateKey, argMaxA);
                    nextVal = 0.5 * (evalB + evalTgt);
                }
                double tdTarget = ClampQ(reward + GAMMA * nextVal * 0.25);
                double curA = GetStateQValue(stateKey, action);
                SetStateQValue(stateKey, action, ClampQ(curA + lr * (tdTarget - curA)));
            }
            else
            {
                double nextVal = 0.0;
                if (!string.IsNullOrEmpty(nextStateKey))
                {
                    string argMaxB = GetArgMaxActionB(nextStateKey);
                    double evalA = GetStateQValue(nextStateKey, argMaxB);
                    double evalTgt = GetStateQTargetValue(nextStateKey, argMaxB);
                    nextVal = 0.5 * (evalA + evalTgt);
                }
                double tdTarget = ClampQ(reward + GAMMA * nextVal * 0.25);
                double curB = GetStateQBValue(stateKey, action);
                SetStateQBValue(stateKey, action, ClampQ(curB + lr * (tdTarget - curB)));
            }
        }

        internal void ResumeDeferredRL2Training()
        {
            lock (_rl2Lock)
            {
                if (_rl2Deferred)
                {
                    _rl2Deferred = false;
                    if (_rl2Replay.Count >= RL2_REPLAY_MIN)
                    {
                        ReplayTrainRL2();
                    }
                }
            }
        }

        private void ReplayTrainRL2()
        {
            int n = _rl2Replay.Count;
            if (n == 0) return;
            int batch = Math.Min(16, n);
            for (int i = 0; i < batch; i++)
            {
                RLTransition t = _rl2Replay[_rnd.Next(n)];
                ApplyDoubleQUpdateLocked(t.StateKey, t.Action, t.Reward, t.NextStateKey, _adaptiveLearningRate * 0.5, (i & 1) == 0);
            }
            _rl2ReplayTick = 0;
        }

        private void UpdateStateQTargets()
        {
            foreach (var st in _stateQ)
            {
                Dictionary<string, double> tRow;
                if (!_stateQTarget.TryGetValue(st.Key, out tRow))
                {
                    tRow = new Dictionary<string, double>();
                    _stateQTarget[st.Key] = tRow;
                }
                foreach (var kv in st.Value)
                {
                    double cur = GetCombinedStateQValueLocked(st.Key, kv.Key);
                    double tgt = tRow.ContainsKey(kv.Key) ? tRow[kv.Key] : cur;
                    tRow[kv.Key] = tgt * (1.0 - TAU2) + cur * TAU2;
                }
            }
        }

        // Slow decay toward neutral prevents runaway optimistic Q-values.
        public void RunRL2Maintenance()
        {
            lock (_rl2Lock)
            {
                foreach (var st in _stateQ)
                {
                    var keys = new List<string>(st.Value.Keys);
                    for (int i = 0; i < keys.Count; i++)
                    {
                        double v = st.Value[keys[i]];
                        st.Value[keys[i]] = v + (Q_DEFAULT - v) * 0.005;
                    }
                }
                foreach (var stB in _stateQB)
                {
                    var keys = new List<string>(stB.Value.Keys);
                    for (int i = 0; i < keys.Count; i++)
                    {
                        double v = stB.Value[keys[i]];
                        stB.Value[keys[i]] = v + (Q_DEFAULT - v) * 0.005;
                    }
                }
            }
        }

        /// <summary>
        /// Seeding used by prior-knowledge pretraining.
        /// Seeds both Q_A and Q_B tables and initializes visit count >= 1 so seeded prior states
        /// are not prematurely evicted before unvisited transient states.
        /// </summary>
        public void SeedStateQValue(string stateKey, string action, double value)
        {
            if (string.IsNullOrEmpty(stateKey) || string.IsNullOrEmpty(action)) return;
            lock (_rl2Lock)
            {
                SetStateQValue(stateKey, action, value);
                SetStateQBValue(stateKey, action, value);

                Dictionary<string, double> tRow;
                if (!_stateQTarget.TryGetValue(stateKey, out tRow))
                {
                    tRow = new Dictionary<string, double>();
                    _stateQTarget[stateKey] = tRow;
                }
                tRow[action] = value;

                Dictionary<string, int> vRow;
                if (!_stateVisits.TryGetValue(stateKey, out vRow))
                {
                    vRow = new Dictionary<string, int>();
                    _stateVisits[stateKey] = vRow;
                }
                int existingVisits;
                if (!vRow.TryGetValue(action, out existingVisits) || existingVisits < 1)
                {
                    vRow[action] = 1;
                }
            }
        }

        /// <summary>
        /// Evaluates RL2 in Shadow Mode against the candidate decisions in RankAndFilter.
        /// Always logs/tracks agreement without mutating decisions when EnableRL2LiveInfluence == false.
        /// When EnableRL2LiveInfluence == true, applies a bounded confidence adjustment (+/- 0.08 max)
        /// ONLY for actions that are applicable to the state and have >= 3 state-action visits.
        /// </summary>
        internal void EvaluateRL2ShadowAndOptionalInfluence(List<AgentDecision> decisions)
        {
            if (decisions == null || decisions.Count == 0) return;
            try
            {
                string stateKey = GetCurrentRLStateKey();
                if (string.IsNullOrEmpty(stateKey)) return;

                SystemState curState = _currentState;
                string shadowBest = SelectBestStateAction(stateKey, curState);

                // Find top live candidate before RL2 influence
                string liveBest = "NONE";
                double liveMaxConf = double.MinValue;
                for (int i = 0; i < decisions.Count; i++)
                {
                    if (decisions[i] != null && decisions[i].Confidence > liveMaxConf)
                    {
                        liveMaxConf = decisions[i].Confidence;
                        liveBest = decisions[i].Action ?? "NONE";
                    }
                }

                lock (_rl2Lock)
                {
                    _rl2ShadowComparisons++;
                    _rl2LastShadowAction = shadowBest ?? "NONE";
                    _rl2LastLiveAction = liveBest;
                    if (string.Equals(_rl2LastShadowAction, _rl2LastLiveAction, StringComparison.Ordinal))
                    {
                        _rl2ShadowAgreements++;
                    }
                }

                if (!_enableRL2LiveInfluence)
                {
                    return; // Shadow Mode only: zero mutation of live decisions
                }

                // Optional gated live influence (only when explicitly enabled and visits >= 3)
                for (int i = 0; i < decisions.Count; i++)
                {
                    var d = decisions[i];
                    if (d == null || string.IsNullOrEmpty(d.Action)) continue;
                    if (curState != null && !IsActionApplicableToState(d.Action, curState)) continue;

                    int visits = GetStateActionVisits(stateKey, d.Action);
                    if (visits < 3) continue;

                    double qVal = GetCombinedStateQValue(stateKey, d.Action);
                    double delta = Math.Max(-0.08, Math.Min(0.08, (qVal - 0.5) * 0.20));
                    d.Confidence = Math.Max(0.0, Math.Min(1.0, d.Confidence + delta));
                }
            }
            catch (Exception ex)
            {
                CWLog.Error("RL2.ShadowMode", ex);
            }
        }

        public string GetRL2Summary()
        {
            lock (_rl2Lock)
            {
                return string.Format(
                    "States: {0} | Updates: {1} | Replay: {2} | Gamma: {3:F2} | ShadowAgree: {4}/{5} | LiveInfluence: {6} | Best-in-state: {7}",
                    _stateQ.Count,
                    _rl2TotalUpdates,
                    _rl2Replay.Count,
                    GAMMA,
                    _rl2ShadowAgreements,
                    _rl2ShadowComparisons,
                    _enableRL2LiveInfluence ? "ON" : "SHADOW_ONLY",
                    _stateQ.Count > 0 ? SelectBestStateAction(GetCurrentRLStateKey()) : "NONE");
            }
        }

        // =====================================================================
        // PERSISTENCE: the per-state double-Q tables are real learned experience
        // and must survive restarts, otherwise RL2 resets to priors every boot.
        // Wired into SaveData()/LoadData()/Stop() alongside the other models.
        // =====================================================================
        private void SaveRL2Data()
        {
            lock (_rl2Lock)
            {
                try
                {
                    var sb = new StringBuilder();
                    sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "#V1|{0}|{1}", _rl2TotalUpdates, _stateQ.Count));
                    foreach (var st in _stateQ)
                    {
                        Dictionary<string, double> qbRow, tgtRow;
                        Dictionary<string, int> vRow;
                        _stateQB.TryGetValue(st.Key, out qbRow);
                        _stateQTarget.TryGetValue(st.Key, out tgtRow);
                        _stateVisits.TryGetValue(st.Key, out vRow);
                        foreach (var kv in st.Value)
                        {
                            double qb = Q_DEFAULT, tgt = kv.Value; int vis = 1;
                            if (qbRow != null && !qbRow.TryGetValue(kv.Key, out qb)) qb = kv.Value;
                            if (tgtRow != null && !tgtRow.TryGetValue(kv.Key, out tgt)) tgt = kv.Value;
                            if (vRow != null && !vRow.TryGetValue(kv.Key, out vis)) vis = 1;
                            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0}|{1}|{2:R}|{3:R}|{4:R}|{5}",
                                st.Key, kv.Key, kv.Value, qb, tgt, vis));
                        }
                    }
                    AtomicWrite(Path.Combine(_dataDir, "rl2.dat"), sb.ToString());
                }
                catch (Exception ex) { LogError("SaveRL2Data", ex); }
            }
        }

        private void LoadRL2Data()
        {
            lock (_rl2Lock)
            {
                try
                {
                    string path = ResolveLoadPath(Path.Combine(_dataDir, "rl2.dat"));
                    if (!File.Exists(path)) return;
                    _stateQ.Clear();
                    _stateQB.Clear();
                    _stateQTarget.Clear();
                    _stateVisits.Clear();
                    bool hasHeader = false;
                    foreach (string line in File.ReadAllLines(path))
                    {
                        if (string.IsNullOrEmpty(line)) continue;
                        string[] p = line.Split('|');
                        if (p[0] == "#V1")
                        {
                            hasHeader = true;
                            if (p.Length >= 2) { int u; if (int.TryParse(p[1], out u)) _rl2TotalUpdates = u; }
                            continue;
                        }
                        if (p.Length < 6) continue;
                        try
                        {
                            string state = p[0];
                            string action = p[1];
                            double q = double.Parse(p[2], CultureInfo.InvariantCulture);
                            double qb = double.Parse(p[3], CultureInfo.InvariantCulture);
                            double tgt = double.Parse(p[4], CultureInfo.InvariantCulture);
                            int vis = int.Parse(p[5], CultureInfo.InvariantCulture);
                            if (_stateQ.Count >= RL2_MAX_STATES) break;
                            SetStateQValue(state, action, q);
                            SetStateQBValue(state, action, qb);
                            Dictionary<string, double> tr;
                            if (!_stateQTarget.TryGetValue(state, out tr)) { tr = new Dictionary<string, double>(); _stateQTarget[state] = tr; }
                            tr[action] = tgt;
                            Dictionary<string, int> vr;
                            if (!_stateVisits.TryGetValue(state, out vr)) { vr = new Dictionary<string, int>(); _stateVisits[state] = vr; }
                            vr[action] = Math.Max(1, vis);
                        }
                        catch (Exception ex) { CWLog.Error("SmartAgent.RL2", ex); }
                    }
                    if (!hasHeader)
                    {
                        // Legacy/partial file: derive target table from the online table.
                        UpdateStateQTargets();
                    }
                    AddLog("INFO", "RL2", "Loaded RL2 Q-tables: " + _stateQ.Count + " states, " + _rl2TotalUpdates + " updates");
                }
                catch (Exception ex) { LogError("LoadRL2Data", ex); }
            }
        }
    }
}
