using System;
using System.Collections.Generic;

namespace ClearWin.RuleEngine
{
    /// <summary>
    /// Safety Engine: the last line of defense. Blocks any action that could
    /// damage the system, regardless of how attractive it looks to the
    /// Decision Engine. Uses static blacklists plus dynamic guards that
    /// understand the current device state.
    /// </summary>
    public class SafetyEngine
    {
        public interface ISafetyGuard
        {
            string Name { get; }
            void Assess(CandidateAction action, SystemState state, DeviceSnapshot device, SafetyCheckResult result);
        }

        private readonly List<ISafetyGuard> _guards = new List<ISafetyGuard>();
        private readonly Dictionary<string, DateTime> _cooldowns = new Dictionary<string, DateTime>();
        private readonly object _lock = new object();

        // System-critical targets that must never be altered.
        private static readonly HashSet<string> BlacklistedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "csrss", "smss", "wininit", "winlogon", "services", "lsass", "svchost", "dwm",
            "explorer", "system", "registry", "win32k", "ntoskrnl"
        };

        // Never-touch registry paths.
        private static readonly string[] BlacklistedRegistry = new[]
        {
            "HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager",
            "HKLM\\SYSTEM\\CurrentControlSet\\Services\\",
            "HKCU\\Software\\Microsoft\\Windows NT\\CurrentVersion\\Winlogon"
        };

        public SafetyEngine()
        {
            RegisterGuard(new ThermalGuard());
            RegisterGuard(new PowerGuard());
            RegisterGuard(new ReversibilityGuard());
            RegisterGuard(new BlacklistGuard());
        }

        public void RegisterGuard(ISafetyGuard guard)
        {
            if (guard != null) _guards.Add(guard);
        }

        public bool IsOnCooldown(string actionId)
        {
            lock (_lock)
            {
                DateTime until;
                if (_cooldowns.TryGetValue(actionId, out until))
                    return DateTime.Now < until;
            }
            return false;
        }

        public void ApplyCooldown(string actionId, double seconds)
        {
            lock (_lock) { _cooldowns[actionId] = DateTime.Now.AddSeconds(seconds); }
        }

        public SafetyCheckResult Assess(CandidateAction action, SystemState state, DeviceSnapshot device)
        {
            var result = new SafetyCheckResult { ActionId = action != null ? action.Id : "", Decision = SafetyDecision.Allowed };

            if (action == null)
            {
                result.Decision = SafetyDecision.Blocked;
                result.Reasons.Add("Null action cannot be executed.");
                return result;
            }

            // Cooldown gate.
            if (IsOnCooldown(action.Id))
            {
                result.Reasons.Add("Action is on cooldown to avoid thrashing.");
                result.Decision = SafetyDecision.Blocked;
            }

            // Dynamic guards.
            for (int i = 0; i < _guards.Count && result.Decision != SafetyDecision.Blocked; i++)
            {
                try { _guards[i].Assess(action, state, device, result); }
                catch
                {
                    result.Decision = SafetyDecision.Blocked;
                    result.Reasons.Add(string.Format("Safety guard '{0}' threw an exception; blocking as precaution.", _guards[i].Name));
                }
            }

            result.RiskScore = action.Risk;
            if (result.Decision == SafetyDecision.Allowed && action.Risk >= RuleEngineConstants.RiskRequiresApproval)
                result.Decision = SafetyDecision.NeedsApproval;
            if (action.Risk >= RuleEngineConstants.RiskCritical)
                result.Decision = SafetyDecision.Blocked;

            return result;
        }

        // ============================ BUILT-IN GUARDS ============================

        private sealed class BlacklistGuard : ISafetyGuard
        {
            public string Name { get { return "Blacklist"; } }
            public void Assess(CandidateAction action, SystemState state, DeviceSnapshot device, SafetyCheckResult result)
            {
                if (!string.IsNullOrEmpty(action.TargetProcess) &&
                    BlacklistedTargets.Contains(action.TargetProcess))
                {
                    result.Decision = SafetyDecision.Blocked;
                    result.Reasons.Add(string.Format("Target process '{0}' is blacklisted (system-critical).", action.TargetProcess));
                }
                string desc = action.Description ?? "";
                for (int i = 0; i < BlacklistedRegistry.Length; i++)
                {
                    if (desc.IndexOf(BlacklistedRegistry[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        result.Decision = SafetyDecision.Blocked;
                        result.Reasons.Add("Action targets a protected registry path.");
                        break;
                    }
                }
            }
        }

        private sealed class ThermalGuard : ISafetyGuard
        {
            public string Name { get { return "Thermal"; } }
            public void Assess(CandidateAction action, SystemState state, DeviceSnapshot device, SafetyCheckResult result)
            {
                if (state == null) return;
                // Never run heat-generating optimizations while already hot.
                bool heatsSystem = action.Class == ActionClass.Process || action.Class == ActionClass.Power;
                if (heatsSystem && state.CpuTemp >= RuleEngineConstants.CriticalThermal)
                {
                    result.Reasons.Add(string.Format("CPU {0:F0}C is critical; only cooling actions are safe now.", state.CpuTemp));
                    result.Decision = SafetyDecision.Blocked;
                }
            }
        }

        private sealed class PowerGuard : ISafetyGuard
        {
            public string Name { get { return "Power"; } }
            public void Assess(CandidateAction action, SystemState state, DeviceSnapshot device, SafetyCheckResult result)
            {
                if (state == null) return;
                bool powerHungry = action.Class == ActionClass.Process;
                if (powerHungry && !state.IsCharging && state.BatteryLevel <= RuleEngineConstants.BatteryCritical)
                {
                    result.Reasons.Add(string.Format("Battery is critical ({0:F0}%); power-hungry actions blocked.", state.BatteryLevel));
                    result.Decision = SafetyDecision.Blocked;
                }
            }
        }

        private sealed class ReversibilityGuard : ISafetyGuard
        {
            public string Name { get { return "Reversibility"; } }
            public void Assess(CandidateAction action, SystemState state, DeviceSnapshot device, SafetyCheckResult result)
            {
                bool isSystemClass = action.Class == ActionClass.Service || action.Class == ActionClass.System ||
                                     action.Class == ActionClass.Security;
                if (isSystemClass && !action.Reversible)
                {
                    result.Reasons.Add("Irreversible change to a system component is not allowed.");
                    result.Decision = SafetyDecision.Blocked;
                }
            }
        }
    }
}
