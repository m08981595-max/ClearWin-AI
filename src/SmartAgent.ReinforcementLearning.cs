using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ClearWin
{
    public partial class SmartAgent
    {
        private RLState _currentRLState;
        private Dictionary<string, RLActionValue> _actionValues = new Dictionary<string, RLActionValue>();
        private DateTime _lastRLUpdate = DateTime.MinValue;
        private const int RL_UPDATE_INTERVAL_SECONDS = 15;
        private double _explorationRate = 0.15;
        private const double MIN_EXPLORATION = 0.02;
        private const double EXPLORATION_DECAY = 0.998;
        private List<RLEpisode> _episodes = new List<RLEpisode>();
        private RLAction _pendingRLAction;
        private double _cumulativeReward = 0;
        private int _totalEpisodes = 0;

        // === Double Q-Learning ===
        private Dictionary<string, double> _actionValuesB = new Dictionary<string, double>();

        // === Target Networks (for stable training) ===
        private Dictionary<string, RLActionValue> _targetActionValues = new Dictionary<string, RLActionValue>();
        private Dictionary<string, double> _targetActionValuesB = new Dictionary<string, double>();
        private const double TAU = 0.005;
        private int _targetUpdateTick = 0;
        private const int TARGET_UPDATE_INTERVAL = 5;

        // === Experience Replay for RL ===
        private List<RLExperience> _rlReplayBuffer = new List<RLExperience>();
        private const int RL_REPLAY_MAX = 500;
        private const int RL_REPLAY_BATCH = 16;
        private const int RL_REPLAY_MIN = 32;

        // === NEW: Expanded State Space ===
        private const int CPU_BINS = 6;
        private const int RAM_BINS = 6;
        private const int TEMP_BINS = 5;
        private const int FPS_BINS = 4;
        private const int BATTERY_BINS = 3;
        private const int PROCESS_BINS = 4;
        // Context-aware state: GPU load, disk load, time-of-day
        private const int GPU_BINS = 3;
        private const int DISK_BINS = 2;
        private const int HOUR_BINS = 3;

        // === NEW: Adaptive Learning Rate ===
        private double _adaptiveLearningRate = 0.15;
        private const double MIN_LEARNING_RATE = 0.05;
        private const double MAX_LEARNING_RATE = 0.3;

        // === NEW: Success/Failure Tracking ===
        private Dictionary<string, int> _rlFailureCount = new Dictionary<string, int>();

        public double GetExplorationRate() { return _explorationRate; }
        public double GetCumulativeReward() { return _cumulativeReward; }
        public int GetTotalEpisodes() { return _totalEpisodes; }
        public List<RLEpisode> GetEpisodeHistory() { return new List<RLEpisode>(_episodes); }
        public Dictionary<string, RLActionValue> GetActionValues() { return new Dictionary<string, RLActionValue>(_actionValues); }

        public void InitializeRL()
        {
            _currentRLState = new RLState();
            _currentRLState.CpuBin = 0;
            _currentRLState.RamBin = 0;
            _currentRLState.TempBin = 0;
            _currentRLState.FpsBin = 0;
            _currentRLState.Mode = "Desktop";

            string[] actions = { "AggressiveClean", "LightClean", "PreemptiveClean", "PurgeStandbyList",
                "LimitBackground", "BoostGamePriority", "EmergencyThrottle", "ReduceThermal",
                "StabilizeFrames", "ReduceMicroStutter", "BatterySaver", "BalancedPerformance",
                "SetGameAffinity", "UnparkAllCores", "SuggestClose", "NONE" };

            foreach (string action in actions)
            {
                _actionValues[action] = new RLActionValue();
                _actionValues[action].Action = action;
                _actionValues[action].QValue = 0.5;
                _actionValues[action].VisitCount = 0;
                _actionValues[action].AvgReward = 0;
                _actionValuesB[action] = 0.5;

                _targetActionValues[action] = new RLActionValue();
                _targetActionValues[action].Action = action;
                _targetActionValues[action].QValue = 0.5;
                _targetActionValuesB[action] = 0.5;
            }

            AddLog("RL", "ReinforcementLearning", "Initialized with " + _actionValues.Count + " actions, Double Q-Learning + Target Networks + Replay");
        }

        public RLState GetCurrentRLState()
        {
            var state = new RLState();
            state.CpuBin = Discretize(_smoothCpu, CPU_BINS);
            state.RamBin = Discretize(_smoothRam, RAM_BINS);
            state.TempBin = Discretize(_smoothTemp, TEMP_BINS);
            state.FpsBin = Discretize(_smoothFps, FPS_BINS);
            state.GpuBin = Discretize(_smoothGpu, GPU_BINS);
            state.DiskBin = Discretize(_currentState != null ? _currentState.DiskUsage : 50.0, DISK_BINS);
            state.HourBin = GetHourBin();
            state.Mode = _appMode.ToString();
            state.Timestamp = DateTime.Now;
            return state;
        }

        private int GetHourBin()
        {
            int h = DateTime.Now.Hour;
            if (h < 8) return 0;
            if (h < 17) return 1;
            return 2;
        }

        private int Discretize(double value, int bins)
        {
            double step = 100.0 / bins;
            int bin = (int)(value / step);
            return Math.Max(0, Math.Min(bins - 1, bin));
        }

        public string SelectRLAction()
        {
            _currentRLState = GetCurrentRLState();
            string stateKey = GetStateKey(_currentRLState);

            if (_rnd.NextDouble() < _explorationRate)
            {
                // === NEW: Intelligent exploration - prefer less-tried actions ===
                string randomAction = SelectExplorationAction();
                _pendingRLAction = new RLAction();
                _pendingRLAction.Action = randomAction;
                _pendingRLAction.State = stateKey;
                _pendingRLAction.IsExploration = true;
                return randomAction;
            }
            else
            {
                // === NEW: Double Q-Learning selection ===
                string bestAction = SelectBestActionDoubleQ();
                _pendingRLAction = new RLAction();
                _pendingRLAction.Action = bestAction;
                _pendingRLAction.State = stateKey;
                _pendingRLAction.IsExploration = false;
                return bestAction;
            }
        }

        private string SelectExplorationAction()
        {
            // Intelligent exploration: prefer actions with fewer visits
            var candidates = new List<string>();
            var weights = new List<double>();

            foreach (var kv in _actionValues)
            {
                candidates.Add(kv.Key);
                // Weight inversely proportional to visit count
                double weight = 1.0 / (1.0 + kv.Value.VisitCount * 0.1);
                weights.Add(weight);
            }

            // Weighted random selection
            double totalWeight = 0;
            for (int i = 0; i < weights.Count; i++) totalWeight += weights[i];

            double rand = _rnd.NextDouble() * totalWeight;
            double cumulative = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                cumulative += weights[i];
                if (rand <= cumulative) return candidates[i];
            }

            return candidates[candidates.Count - 1];
        }

        private string SelectBestActionDoubleQ()
        {
            string bestAction = "NONE";
            double bestValue = double.MinValue;

            foreach (var kv in _actionValues)
            {
                double qA = kv.Value.QValue;
                double qB;
                _actionValuesB.TryGetValue(kv.Key, out qB);
                double combinedValue = (qA + qB) / 2.0;

                if (combinedValue > bestValue)
                {
                    bestValue = combinedValue;
                    bestAction = kv.Key;
                }
            }

            return bestAction;
        }

        private string SelectBestActionTarget()
        {
            string bestAction = "NONE";
            double bestValue = double.MinValue;

            foreach (var kv in _targetActionValues)
            {
                double qA = kv.Value.QValue;
                double qB;
                _targetActionValuesB.TryGetValue(kv.Key, out qB);
                double combinedValue = (qA + qB) / 2.0;

                if (combinedValue > bestValue)
                {
                    bestValue = combinedValue;
                    bestAction = kv.Key;
                }
            }

            return bestAction;
        }

        public void UpdateRL(double reward, bool success)
        {
            if (_pendingRLAction == null) return;

            string stateKey = _pendingRLAction.State;
            string action = _pendingRLAction.Action;

            // === NEW: Double Q-Learning update ===
            RLActionValue av;
            if (_actionValues.TryGetValue(action, out av))
            {
                double oldQ = av.QValue;

                // Randomly update either Q_A or Q_B
                if (_rnd.NextDouble() < 0.5)
                {
                    // Update Q_A
                    av.QValue = oldQ + _adaptiveLearningRate * (reward - oldQ);
                }
                else
                {
                    // Update Q_B
                    double oldQB;
                    _actionValuesB.TryGetValue(action, out oldQB);
                    _actionValuesB[action] = oldQB + _adaptiveLearningRate * (reward - oldQB);
                }

                av.VisitCount++;
                // Time-weighted AvgReward
                double hoursSinceUpdate = (DateTime.Now - av.LastUpdated).TotalHours;
                double decayFactor = Math.Exp(-hoursSinceUpdate / 12.0);
                av.AvgReward = av.AvgReward * decayFactor + reward * (1.0 - decayFactor);
                av.LastUpdated = DateTime.Now;
            }

            // === NEW: Update success/failure counts ===
            int successCount, failureCount;
            _actionSuccessCount.TryGetValue(action, out successCount);
            _rlFailureCount.TryGetValue(action, out failureCount);
            if (success) successCount++; else failureCount++;
            _actionSuccessCount[action] = successCount;
            _rlFailureCount[action] = failureCount;

            // === NEW: Adaptive learning rate based on confidence ===
            AdaptLearningRate(action);

            _explorationRate = Math.Max(MIN_EXPLORATION, _explorationRate * EXPLORATION_DECAY);

            _cumulativeReward += reward;

            var rlEpisode = new RLEpisode();
            rlEpisode.State = stateKey;
            rlEpisode.Action = action;
            rlEpisode.Reward = reward;
            rlEpisode.Timestamp = DateTime.Now;
            _episodes.Add(rlEpisode);
            if (_episodes.Count > 300) _episodes.RemoveAt(0);
            _totalEpisodes++;

            // === Store experience in replay buffer ===
            var exp = new RLExperience();
            exp.StateKey = stateKey;
            exp.Action = action;
            exp.Reward = reward;
            exp.Success = success;
            exp.Timestamp = DateTime.Now;
            _rlReplayBuffer.Add(exp);
            if (_rlReplayBuffer.Count > RL_REPLAY_MAX) _rlReplayBuffer.RemoveAt(0);

            // === Sample from replay and train ===
            if (_rlReplayBuffer.Count >= RL_REPLAY_MIN) ReplayTrainRL();

            // === Soft update target networks ===
            _targetUpdateTick++;
            if (_targetUpdateTick % TARGET_UPDATE_INTERVAL == 0) UpdateTargetNetworks();

            _pendingRLAction = null;
        }

        // === NEW: Adaptive Learning Rate ===
        private void AdaptLearningRate(string action)
        {
            int successCount, failureCount;
            _actionSuccessCount.TryGetValue(action, out successCount);
            _rlFailureCount.TryGetValue(action, out failureCount);
            int total = successCount + failureCount;

            if (total > 5)
            {
                double successRate = (double)successCount / total;
                // High success rate -> lower learning rate (exploit)
                // Low success rate -> higher learning rate (explore)
                _adaptiveLearningRate = MIN_LEARNING_RATE + (MAX_LEARNING_RATE - MIN_LEARNING_RATE) * (1.0 - successRate);
            }
        }

        // === Target Network soft update ===
        private void UpdateTargetNetworks()
        {
            foreach (var kv in _actionValues)
            {
                RLActionValue target;
                if (_targetActionValues.TryGetValue(kv.Key, out target))
                {
                    target.QValue = target.QValue * (1.0 - TAU) + kv.Value.QValue * TAU;
                    target.VisitCount = kv.Value.VisitCount;
                    target.AvgReward = target.AvgReward * (1.0 - TAU) + kv.Value.AvgReward * TAU;
                }
                double qB;
                if (_actionValuesB.TryGetValue(kv.Key, out qB))
                {
                    double tB;
                    _targetActionValuesB.TryGetValue(kv.Key, out tB);
                    _targetActionValuesB[kv.Key] = tB * (1.0 - TAU) + qB * TAU;
                }
            }
        }

        // === Experience Replay Training ===
        private void ReplayTrainRL()
        {
            int count = _rlReplayBuffer.Count;
            for (int s = 0; s < RL_REPLAY_BATCH; s++)
            {
                int idx = _rnd.Next(count);
                RLExperience exp = _rlReplayBuffer[idx];

                RLActionValue av;
                if (_actionValues.TryGetValue(exp.Action, out av))
                {
                    double tdError = exp.Reward - av.QValue;
                    av.QValue += _adaptiveLearningRate * tdError * 0.5;
                }
                double oldQB;
                if (_actionValuesB.TryGetValue(exp.Action, out oldQB))
                {
                    double tdErrorB = exp.Reward - oldQB;
                    _actionValuesB[exp.Action] = oldQB + _adaptiveLearningRate * tdErrorB * 0.5;
                }
            }
        }

        // Q-value decay: pull all Q-values toward neutral (0.5) over time
        public void DecayRLValues()
        {
            double decayRate = 0.02;
            foreach (var kv in _actionValues)
            {
                kv.Value.QValue = kv.Value.QValue + (0.5 - kv.Value.QValue) * decayRate;
            }
            var keysB = _actionValuesB.Keys.ToList();
            foreach (var key in keysB)
            {
                _actionValuesB[key] = _actionValuesB[key] + (0.5 - _actionValuesB[key]) * decayRate;
            }
        }

        public double CalculateReward(bool success, double healthDelta, double tempDelta)
        {
            double reward = 0;

            // Base reward
            reward += success ? 1.0 : -0.5;

            // Health improvement reward
            reward += healthDelta * 0.3;

            // Temperature penalty
            reward += -tempDelta * 0.1;

            // === NEW: Additional reward signals ===

            // FPS improvement reward
            if (_prevFps > 0 && _smoothFps > _prevFps)
                reward += 0.2;

            // Stability reward (low variance)
            double cpuVariance = Math.Abs(_smoothCpu - _currentState.CpuUsage);
            if (cpuVariance < 5) reward += 0.1;

            // Penalty for no-op when system needs help
            if (_smoothCpu > 80 || _smoothTemp > 80 || _smoothRam > 85)
                reward -= 0.1;

            // Bonus for balanced system
            if (_smoothCpu < 60 && _smoothRam < 60 && _smoothTemp < 65)
                reward += 0.15;

            return Math.Max(-2.0, Math.Min(2.0, reward));
        }

        private string GetStateKey(RLState state)
        {
            return BuildStateKey(state);
        }

        // Canonical state-key builder shared by the live agent, the prior
        // pretraining, and the reward engine so learned Q-values always match
        // the states they are queried with.
        public string BuildStateKey(RLState state)
        {
            return string.Format("C{0}R{1}T{2}F{3}G{4}D{5}H{6}_{7}",
                state.CpuBin, state.RamBin, state.TempBin, state.FpsBin,
                state.GpuBin, state.DiskBin, state.HourBin, state.Mode);
        }

        public string GetRLSummary()
        {
            return string.Format("Episodes: {0} | Reward: {1:F2} | Explore: {2:F1}% | Best: {3} (Q={4:F3}) | LR: {5:F3}",
                _totalEpisodes, _cumulativeReward, _explorationRate * 100,
                GetBestRLAction(), GetBestRLQValue(), _adaptiveLearningRate);
        }

        private string GetBestRLAction()
        {
            string best = "NONE";
            double bestQ = double.MinValue;
            foreach (var kv in _actionValues)
            {
                if (kv.Value.QValue > bestQ) { bestQ = kv.Value.QValue; best = kv.Key; }
            }
            return best;
        }

        private double GetBestRLQValue()
        {
            double bestQ = double.MinValue;
            foreach (var kv in _actionValues)
            {
                if (kv.Value.QValue > bestQ) bestQ = kv.Value.QValue;
            }
            return bestQ;
        }

        // === NEW: Get action success rates for UI ===
        public Dictionary<string, double> GetActionSuccessRates()
        {
            var rates = new Dictionary<string, double>();
            foreach (var kv in _actionValues)
            {
                int success, failure;
                _actionSuccessCount.TryGetValue(kv.Key, out success);
                _rlFailureCount.TryGetValue(kv.Key, out failure);
                int total = success + failure;
                rates[kv.Key] = total > 0 ? (double)success / total : 0.5;
            }
            return rates;
        }
    }
}
