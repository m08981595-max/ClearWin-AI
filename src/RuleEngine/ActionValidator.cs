using System;
using System.Collections.Generic;

namespace ClearWin.RuleEngine
{
    /// <summary>
    /// Validator: verifies the correctness and safety of every action BEFORE
    /// execution. Checks prerequisites, protected targets, value ranges,
    /// reversibility and resource sanity.
    /// </summary>
    public class ActionValidator
    {
        // Processes that must never be touched by any optimization.
        private static readonly HashSet<string> ProtectedProcesses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "system", "registry", "smss", "csrss", "wininit", "winlogon", "services", "lsass",
            "svchost", "dwm", "explorer", "winlogon", "audiodg", "fontdrvhost", "dllhost",
            "sihost", "taskhostw", "conhost", "ctfmon", "runtimebroker", "clearwin"
        };

        private readonly RuleKnowledgeBase _kb;

        public ActionValidator(RuleKnowledgeBase kb)
        {
            _kb = kb;
        }

        public ValidationResult Validate(CandidateAction action, SystemState state, DeviceSnapshot device)
        {
            var result = new ValidationResult { ActionId = action != null ? action.Id : "" };
            if (action == null)
            {
                result.Valid = false;
                result.Errors.Add("Action is null.");
                return result;
            }

            // 1) Identity checks.
            if (string.IsNullOrEmpty(action.Id))
                result.Errors.Add("Action id is empty.");
            if (action.Name == null || action.Name.Length == 0)
                result.Errors.Add("Action name is empty.");

            // 2) Value sanity.
            if (action.ExpectedImpact < -20 || action.ExpectedImpact > 100)
                result.Errors.Add(string.Format("ExpectedImpact {0:F1} out of plausible range.", action.ExpectedImpact));
            if (action.Risk < 0 || action.Risk > 1)
                result.Errors.Add(string.Format("Risk {0:F2} out of [0,1].", action.Risk));
            if (action.Cost < 0 || action.Cost > 10)
                result.Errors.Add(string.Format("Cost {0:F1} out of [0,10].", action.Cost));
            if (action.Confidence < 0 || action.Confidence > 1)
                result.Errors.Add(string.Format("Confidence {0:F2} out of [0,1].", action.Confidence));

            // 3) Protected target check.
            if (!string.IsNullOrEmpty(action.TargetProcess) &&
                ProtectedProcesses.Contains(action.TargetProcess))
            {
                result.Errors.Add(string.Format("Target process '{0}' is system-critical and protected.", action.TargetProcess));
            }

            // 4) Prerequisites (facts) must be present with meaningful confidence.
            if (action.Prerequisites != null)
            {
                for (int i = 0; i < action.Prerequisites.Count; i++)
                {
                    string variable = action.Prerequisites[i];
                    var fact = _kb != null ? _kb.GetFact(variable) : null;
                    if (fact == null || fact.Confidence < RuleEngineConstants.FactMinConfidence)
                        result.Errors.Add(string.Format("Prerequisite fact '{0}' is missing or too uncertain.", variable));
                }
            }

            // 5) Resource sanity guards.
            if (state != null)
            {
                // Cleaning memory when there is plenty free is wasteful and risky.
                if ((action.Id == "AggressiveClean" || action.Id == "LightClean" || action.Id == "PurgeStandbyList") &&
                    (state.TotalRamGB - state.UsedRamGB) >= 2.0 && state.RamUsage < 60)
                {
                    result.Warnings.Add(string.Format("RAM pressure is low ({0:F0}%); memory cleaning may not help.", state.RamUsage));
                }
                // Throttling when the system is cool makes no sense.
                if ((action.Id == "EmergencyThrottle" || action.Id == "ReduceThermal") && state.CpuTemp < 70)
                {
                    result.Warnings.Add(string.Format("Temperature is only {0:F0}C; thermal action may be unnecessary.", state.CpuTemp));
                }
                // Game boosts require an active game.
                if ((action.Id == "BoostGamePriority" || action.Id == "SetGameAffinity") && !state.IsGame)
                {
                    result.Errors.Add("Game-boost action requested while no game is active.");
                }
            }

            // 6) Reversibility: system-level irreversible actions are rejected unless approved.
            if (!action.Reversible && action.Class == ActionClass.Registry)
            {
                result.Errors.Add("Irreversible registry modification is not allowed.");
            }

            result.Valid = result.Errors.Count == 0;
            return result;
        }
    }
}
