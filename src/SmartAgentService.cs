using System;
using System.Threading;
using System.Threading.Tasks;
using ClearWin.AI;

namespace ClearWin
{
    /// <summary>
    /// Thin UI-facing façade over SmartAgent. Kept separate from the agent so
    /// the WPF ViewModels never touch the 60+ partial engine files directly.
    /// All heavy work is off-loaded to the thread pool.
    /// </summary>
    public sealed class SmartAgentService
    {
        private static readonly Lazy<SmartAgentService> LazyInstance =
            new Lazy<SmartAgentService>(() => new SmartAgentService());

        public static SmartAgentService Instance { get { return LazyInstance.Value; } }

        private SmartAgentService() { }

        // MainWindow creates the real agent; fall back only if it has not yet.
        private static SmartAgent Agent
        {
            get { return SmartAgent.Instance ?? (SmartAgent.Instance = new SmartAgent()); }
        }

        /// <summary>Run the 80% rules + 20% frozen-neural Game Boost off-thread.</summary>
        public Task<HybridBoostReport> RunGameBoostAsync(bool aggressive,
                                                         IProgress<string> progress,
                                                         CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                if (progress != null) progress.Report("Starting Hybrid Game Boost (rules first, neural second)…");
                HybridBoostReport report = Agent.ExecuteGameBoost();
                if (progress != null)
                    progress.Report(report == null ? "Boost returned no report." : report.Summary());
                return report;
            }, cancellationToken);
        }

        /// <summary>Restore the immutable audited baseline and re-freeze learning.</summary>
        public HybridAuditReport RestoreDefaultAI()
        {
            return Agent.RestoreDefaultAI();
        }

        /// <summary>Stop a running boost and restore services changed by the rules layer.</summary>
        public void EndGameBoost()
        {
            Agent.EndGameBoost();
        }

        public string GetStatus()
        {
            return Agent.GetHybridAiStatus();
        }
    }
}
