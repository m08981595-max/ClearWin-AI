using System.Collections.Generic;

namespace ClearWin
{
    /// <summary>
    /// Public contract of the reinforcement-learning subsystem so callers and
    /// tests can depend on the interface instead of the concrete SmartAgent
    /// partial class (enables mocking / fakes).
    /// </summary>
    public interface IRLEngine
    {
        double GetExplorationRate();
        double GetCumulativeReward();
        int GetTotalEpisodes();
        List<RLEpisode> GetEpisodeHistory();
        Dictionary<string, RLActionValue> GetActionValues();
        void InitializeRL();
        RLState GetCurrentRLState();
        string SelectRLAction();
        void UpdateRL(double reward, bool success);
        void DecayRLValues();
        double CalculateReward(bool success, double healthDelta, double tempDelta);
        string BuildStateKey(RLState state);
        string GetRLSummary();
        Dictionary<string, double> GetActionSuccessRates();

        // State-aware RL2 surface
        int GetStateAwareStateCount();
        int GetRL2UpdateCount();
        string SelectBestStateAction(string stateKey);
        void RecordRLExperience(string stateKey, string action, double reward, string nextStateKey);
        void RunRL2Maintenance();
        void SeedStateQValue(string stateKey, string action, double value);
        string GetRL2Summary();
    }

    /// <summary>
    /// Public contract of the objective reward / outcome-evaluation subsystem
    /// (difference-in-differences reward, source trust, calibrated abstention).
    /// Instance-only members; static helpers (ComputeObjectiveReward,
    /// IsMeasurableAction) stay on the concrete class.
    /// </summary>
    public interface IRewardEngine
    {
        int GetEvaluationsCompleted();
        double GetAverageObjectiveReward();
        double GetMeasuredFpsDrift();
        double GetMeasuredTempDrift();
        double GetMeasuredRamDrift();
        string GetCounterfactualStatus();
        double GetExpectedReward(string action);
        int GetActionRewardSampleCount(string action);
        double GetSourceTrust(string source);
        int GetTrustedSourceCount();
        void StartActionEvaluation(string action, bool success);
        void TickOutcomeEvaluation();
        bool ShouldAbstain(AgentDecision d);
        string GetCurrentRLStateKey();
        string GetRewardEngineStatus();
    }

    /// <summary>
    /// Marker: declares that the agent exposes both the RL and the reward
    /// engine contracts. Useful for feature-detection (is rl = x as IRLEngine).
    /// </summary>
    public interface IAgentEngines : IRLEngine, IRewardEngine
    {
    }
}
