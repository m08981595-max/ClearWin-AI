using System;

namespace ClearWin
{
    /// <summary>
    /// Integrates the AI FPS Stabilizer into the SmartAgent (the in-app AI brain).
    /// The stabilizer watches real-time FPS telemetry every tick, predicts imminent
    /// frame drops, and routes mitigation actions through the same decision/safety
    /// pipeline as the Windows Optimization AI so real system changes are applied
    /// with full logging, journaling and failure protection.
    /// </summary>
    public partial class SmartAgent
    {
        private FpsStabilizer _fpsStab;
        private readonly object _fpsStabLock = new object();

        private FpsStabilizer FpsStab
        {
            get
            {
                if (_fpsStab == null)
                {
                    lock (_fpsStabLock)
                    {
                        if (_fpsStab == null)
                            _fpsStab = new FpsStabilizer(new RegressionFpsDropPredictor(), 25, 2);
                    }
                }
                return _fpsStab;
            }
        }

        public void InitializeFpsStabilizer()
        {
            lock (_fpsStabLock)
            {
                _fpsStab = new FpsStabilizer(new RegressionFpsDropPredictor(), 25, 2);
            }
            CWLog.Info("FpsStabilizer", "AI FPS Stabilizer ready (modular predictor, low overhead)");
            AddLog("INFO", "FpsStabilizer", "AI FPS Stabilizer ready (modular predictor, low overhead)");
        }

        public double FpsStabilizerCurrentFps { get { var s = FpsStab; return s != null ? s.CurrentFps : 0; } }
        public double FpsStabilizerAvgFps5 { get { var s = FpsStab; return s != null ? s.AvgFps5 : 0; } }
        public double FpsStabilizerAvgFps10 { get { var s = FpsStab; return s != null ? s.AvgFps10 : 0; } }
        public double FpsStabilizer1Low { get { var s = FpsStab; return s != null ? s.Fps1Low : 0; } }
        public double FpsStabilizerFrameTimeMs { get { var s = FpsStab; return s != null ? s.FrameTimeMs : 0; } }
        public double FpsStabilizerStability { get { var s = FpsStab; return s != null ? s.StabilityScore : 100; } }
        public double FpsStabilizerPredictionRisk { get { var s = FpsStab; return s != null ? s.PredictionRisk : 0; } }
        public string FpsStabilizerPredictionReason { get { var s = FpsStab; return s != null ? s.PredictionReason : ""; } }
        public int FpsStabilizerPreventedDrops { get { var s = FpsStab; return s != null ? s.PreventedDrops : 0; } }
        public int FpsStabilizerDropCount { get { var s = FpsStab; return s != null ? s.DropCount : 0; } }
        public string FpsStabilizerLastAction
        {
            get { var s = FpsStab; return s != null ? s.LastAction.ToString() : "None"; }
        }

        // === FPS floor mode (never drop below target) ===
        public double FpsLockTarget { get { var s = FpsStab; return s != null ? s.TargetFps : 0; } }
        public bool FpsFloorActive { get { var s = FpsStab; return s != null && s.TargetFps > 0; } }
        public bool FpsFloorBelowTarget { get { var s = FpsStab; return s != null && s.BelowTarget; } }
        public double FpsFloorDeficit { get { var s = FpsStab; return s != null ? s.TargetFpsDeficit : 0; } }
        public int FpsFloorHolds { get { var s = FpsStab; return s != null ? s.HoldsBelowTarget : 0; } }

        /// <summary>
        /// Enable/disable the 60 FPS floor. When enabled the stabilizer becomes
        /// proactive: the moment FPS dips below 60 it applies mitigations every
        /// 20s (up to 6/min) instead of waiting for a drop-risk prediction, so
        /// dips below 60 are prevented even under heavy load.
        /// </summary>
        public void SetFpsFloor60(bool enabled)
        {
            try
            {
                var s = FpsStab;
                if (s == null) return;
                if (enabled) { s.TargetFps = 60; s.AggressiveHold = true; }
                else { s.TargetFps = 0; s.AggressiveHold = false; }
                AddLog("INFO", "FpsFloor",
                    enabled ? "60 FPS floor ENABLED (proactive hold, zero-drop under load)"
                            : "60 FPS floor DISABLED");
                CWLog.Info("FpsFloor", enabled
                    ? "60 FPS floor enabled (proactive hold)"
                    : "60 FPS floor disabled");
            }
            catch (Exception ex) { LogError("FpsFloor.Set", ex); }
        }

        // === Full system optimization / stability mode ===
        private bool _fullOptimizationMode = false;
        private DateTime _lastFullOptimization = DateTime.MinValue;
        public bool FullOptimizationMode
        {
            get { return _fullOptimizationMode; }
            set
            {
                _fullOptimizationMode = value;
                AddLog("INFO", "FullOpt",
                    value ? "Full system optimization mode ENABLED" : "Full system optimization mode DISABLED");
            }
        }

        /// <summary>
        /// Applies the full optimization battery (memory purge + clean + background
        /// limit + priority boost) through the same decision/safety pipeline, then
        /// the proactive floor keeps reinforcing. Throttled to once per 90s.
        /// </summary>
        public string ApplyFullSystemOptimization()
        {
            if (_mode == AgentMode.Off || _killSwitchActive || _agentPaused || _pauseTicksRemaining > 0)
                return "AI off - sweep skipped";
            if ((DateTime.Now - _lastFullOptimization).TotalSeconds < 90)
                return "Sweep throttled (90s)";
            try
            {
                _lastFullOptimization = DateTime.Now;
                AddLog("INFO", "FullOpt", "Full system optimization sweep started");
                FpsStabilizerAction[] battery = new FpsStabilizerAction[]
                {
                    FpsStabilizerAction.PurgeStandby,
                    FpsStabilizerAction.CleanMemory,
                    FpsStabilizerAction.LimitBackground,
                    FpsStabilizerAction.BoostPriority
                };
                foreach (FpsStabilizerAction a in battery)
                    ExecuteFpsStabilizationDecision(a, 0.9, "full optimization sweep (stability)");
                AddLog("INFO", "FullOpt", "Full system optimization sweep finished");
                return "OK";
            }
            catch (Exception ex) { LogError("FullOpt.Sweep", ex); return "Error"; }
        }

        /// <summary>
        /// Feed one tick of live telemetry into the AI FPS Stabilizer. Tracking is
        /// always on (the dashboard shows live numbers) but mitigation actions are
        /// only executed while the agent is active and the Windows Optimization AI
        /// is running - matching the rest of the safety model.
        /// </summary>
        public void FeedFpsStabilizer(double fps, double frameMs, double fps1Low, double cpu, double ram, double gpu, double temp, bool isGaming)
        {
            FeedFpsStabilizer(fps, frameMs, fps1Low, cpu, ram, gpu, temp, isGaming, true);
        }

        public void FeedFpsStabilizer(double fps, double frameMs, double fps1Low, double cpu, double ram, double gpu, double temp, bool isGaming, bool frameDataReliable)
        {
            try
            {
                var stab = FpsStab;
                if (stab == null) return;
                var input = new FpsStabilizerInput
                {
                    Fps = fps,
                    FrameTimeMs = frameMs,
                    Fps1Low = fps1Low,
                    CpuUsage = cpu,
                    RamUsage = ram,
                    GpuUsage = gpu,
                    CpuTemp = temp,
                    IsGaming = isGaming,
                    FrameDataReliable = frameDataReliable
                };
                FpsStabilizerAction action = stab.Update(input);
                // Full-optimization sweep (throttled to 90s internally)
                if (FullOptimizationMode && _mode != AgentMode.Off && !_killSwitchActive && !_agentPaused)
                    ApplyFullSystemOptimization();
                if (action == FpsStabilizerAction.None) return;
                // === AI gating: never act while the agent is off/paused/killed ===
                if (_mode == AgentMode.Off || _killSwitchActive || _agentPaused || _pauseTicksRemaining > 0)
                {
                    AddLog("INFO", "FpsStabilizer", "Drop predicted (risk " + stab.PredictionRisk.ToString("F0") + "%) but AI off - monitoring only");
                    return;
                }
                if (_woa != null && _woa.IntentionallyStopped && !_woa.ManualModelControl)
                    return;
                ExecuteFpsStabilizationDecision(action, stab.PredictionRisk, stab.PredictionReason);
            }
            catch (Exception ex)
            {
                LogError("FpsStabilizer.Feed", ex);
            }
        }

        private void ExecuteFpsStabilizationDecision(FpsStabilizerAction action, double risk, string reason)
        {
            // Central Execution Gate: stabilizer mitigations (and the full
            // optimization sweep battery that funnels through here) are
            // autonomous system mutations - blocked while the valve is off.
            if (!CanExecuteAction("FpsStabilizer:" + action, false)) return;

            string act = ActionForStabilizer(action);
            if (act == null) return;
            // Never page-trim (EmptyWorkingSet / standby purge) while a game is
            // active: it evicts the game working set and stutters the frame stream
            // this feature exists to protect.
            if (_currentState.IsGame
                && (action == FpsStabilizerAction.CleanMemory || action == FpsStabilizerAction.PurgeStandby))
                return;
            try
            {
                var d = new AgentDecision
                {
                    Action = act,
                    Category = CategoryForStabilizer(act),
                    Description = "AI FPS Stabilizer: " + action.ToString(),
                    Reason = "[FpsStabilizer] " + reason,
                    Confidence = Math.Max(0.5, Math.Min(0.9, 0.4 + risk * 0.5)),
                    Risk = 0.15,
                    Impact = 6,
                    Safety = DecisionSafety.Safe,
                    Source = "FpsStabilizer"
                };
                AddLog("INFO", "FpsStabilizer", "Drop predicted (risk " + risk.ToString("F0") + "%) - applying " + action.ToString() + " | " + reason);
                ExecuteDecision(d);
            }
            catch (Exception ex)
            {
                LogError("FpsStabilizer.Execute", ex);
            }
        }

        private string ActionForStabilizer(FpsStabilizerAction a)
        {
            switch (a)
            {
                case FpsStabilizerAction.BoostPriority: return "BoostGamePriority";
                case FpsStabilizerAction.CleanMemory: return "AggressiveClean";
                case FpsStabilizerAction.PurgeStandby: return "PurgeStandbyList";
                case FpsStabilizerAction.LimitBackground: return "LimitBackground";
                case FpsStabilizerAction.DeferWork: return "DeferWork";
                default: return null;
            }
        }

        private DecisionCategory CategoryForStabilizer(string act)
        {
            switch (act)
            {
                case "BoostGamePriority": return DecisionCategory.GameBoost;
                case "AggressiveClean": return DecisionCategory.MemoryClean;
                case "PurgeStandbyList": return DecisionCategory.PurgeStandby;
                case "LimitBackground":
                case "DeferWork": return DecisionCategory.BackgroundControl;
                default: return DecisionCategory.BackgroundControl;
            }
        }
    }
}
