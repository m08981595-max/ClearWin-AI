using System;

namespace ClearWin
{
    /// <summary>
    /// Message-bus wiring for the agent. Decouples producers from consumers:
    ///
    ///   Tick                       → Events.Publish(StateObservedEvent)
    ///   ExecuteDecision            → Events.Publish(DecisionExecutedEvent)
    ///   RewardEngine               → Events.Publish(RewardRecordedEvent)
    ///
    /// The Expert System subscribes to state observations (replacing the old
    /// direct method call), and a lightweight bus-relay mirrors decisions and
    /// rewards into the agent log so the event flow is observable.
    /// </summary>
    public partial class SmartAgent
    {
        /// <summary>Typed publish/subscribe bus for inter-subsystem events.</summary>
        public MessageBus Events { get; private set; }

        private void InitializeBusSubscriptions()
        {
            try
            {
                // StateObservedEvent subscription is registered with the Expert
                // System in InitializeRuleEngine (kept with its lifetime).
                Events.Subscribe<DecisionExecutedEvent>(e =>
                {
                    string action = e.Decision != null ? e.Decision.Action : "?";
                    AddLog("INFO", "Bus", "decision executed: " + action + " ok=" + e.Success);
                });
                Events.Subscribe<RewardRecordedEvent>(e =>
                {
                    AddLog("INFO", "Bus", "reward recorded: " + (e.Action ?? "?") + " r=" + e.Reward.ToString("F3"));
                });
            }
            catch (Exception ex) { LogError("Bus.Init", ex); }
        }

        private void OnStateObservedEvent(StateObservedEvent e)
        {
            try
            {
                if (_expertSystem != null) _expertSystem.ObserveSystemState(e.State);
            }
            catch (Exception ex) { LogError("Bus.StateObserved", ex); }
        }
    }
}
