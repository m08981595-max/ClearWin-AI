using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ClearWin
{
    public partial class SmartAgent
    {
        // === Continuous Optimization Fields ===
        private List<MicroOptimization> _microOptQueue = new List<MicroOptimization>();
        private DateTime _lastMicroOptTime = DateTime.MinValue;
        private const int MICRO_OPT_INTERVAL_SECONDS = 15;
        private const int MAX_MICRO_OPTS_PER_HOUR = 20;
        private int _microOptsThisHour = 0;
        private DateTime _microOptHourStart = DateTime.MinValue;
        private List<MicroOptimization> _appliedMicroOpts = new List<MicroOptimization>();
        private const int MAX_APPLIED_MICRO_OPTS = 100;
        private double _continuousHealthDelta = 0;
        private DateTime _lastContinuousSnapshot = DateTime.MinValue;

        // === Public Accessors ===
        public List<MicroOptimization> GetMicroOptQueue() { return new List<MicroOptimization>(_microOptQueue); }
        public List<MicroOptimization> GetAppliedMicroOpts() { return new List<MicroOptimization>(_appliedMicroOpts); }
        public int GetMicroOptsThisHour() { return _microOptsThisHour; }

        // === Run Continuous Micro-Optimizations ===
        public void RunContinuousOptimization()
        {
            if ((DateTime.Now - _lastMicroOptTime).TotalSeconds < MICRO_OPT_INTERVAL_SECONDS) return;
            // Central Execution Gate: micro-optimizations are autonomous system
            // mutations. While the valve is off, return WITHOUT touching the
            // throttle clock so they can fire again as soon as it re-enables.
            if (!CanExecuteAction("ContinuousMicroOptimization", false)) return;

            // Reset hourly counter
            if ((DateTime.Now - _microOptHourStart).TotalHours >= 1)
            {
                _microOptsThisHour = 0;
                _microOptHourStart = DateTime.Now;
            }

            // Don't exceed hourly limit
            if (_microOptsThisHour >= MAX_MICRO_OPTS_PER_HOUR) return;

            // Snapshot health before
            double healthBefore = SystemHealthScore;

            // Identify micro-optimization opportunities
            var opportunities = IdentifyMicroOptimizations();

            // Execute the highest priority micro-opt
            foreach (var opt in opportunities)
            {
                if (_microOptsThisHour >= MAX_MICRO_OPTS_PER_HOUR) break;

                bool success = ExecuteMicroOptimization(opt);
                if (success)
                {
                    opt.ExecutedAt = DateTime.Now;
                    opt.WasSuccessful = true;
                    _appliedMicroOpts.Add(opt);
                    if (_appliedMicroOpts.Count > MAX_APPLIED_MICRO_OPTS) _appliedMicroOpts.RemoveAt(0);
                    _microOptsThisHour++;

                    AddLog("MICRO", "Continuous",
                        string.Format("Applied: {0} (impact: {1:F2})", opt.Description, opt.ExpectedImpact));
                    break; // Execute strictly the single highest-priority micro-optimization per cycle
                }
            }

            // Track health delta
            if ((DateTime.Now - _lastContinuousSnapshot).TotalSeconds >= 60)
            {
                _continuousHealthDelta = SystemHealthScore - healthBefore;
                _lastContinuousSnapshot = DateTime.Now;
            }

            _lastMicroOptTime = DateTime.Now;
        }

        // === Identify Micro-Optimization Opportunities ===
        private List<MicroOptimization> IdentifyMicroOptimizations()
        {
            var opts = new List<MicroOptimization>();

            // Memory pressure micro-clean (never during gaming - EmptyWorkingSet
            // forces game pages out of RAM -> paging -> stutter)
            if (_smoothRam > 65 && _smoothRam < 85 && !_currentState.IsGame)
            {
                var opt = new MicroOptimization();
                opt.Type = MicroOptType.MemoryMicroClean;
                opt.Description = "Light memory pressure relief";
                opt.ExpectedImpact = 0.3;
                opt.Priority = 4;
                opt.Category = "Memory";
                opts.Add(opt);
            }

            // Background process micro-throttle
            if (_smoothCpu > 60 && _smoothCpu < 85 && !_currentState.IsGame)
            {
                var opt = new MicroOptimization();
                opt.Type = MicroOptType.BackgroundThrottle;
                opt.Description = "Reduce background CPU usage";
                opt.ExpectedImpact = 0.2;
                opt.Priority = 3;
                opt.Category = "CPU";
                opts.Add(opt);
            }

            // Timer resolution micro-adjust
            if (_currentState.IsGame && _smoothFps > 30 && _smoothFps < 58)
            {
                var opt = new MicroOptimization();
                opt.Type = MicroOptType.TimerResolution;
                opt.Description = "Optimize timer resolution for smoother frames";
                opt.ExpectedImpact = 0.15;
                opt.Priority = 5;
                opt.Category = "GPU";
                opts.Add(opt);
            }

            // Process priority micro-adjust
            if (_currentState.IsGame && _smoothCpu > 70)
            {
                var opt = new MicroOptimization();
                opt.Type = MicroOptType.PriorityMicroAdjust;
                opt.Description = "Boost game process priority slightly";
                opt.ExpectedImpact = 0.25;
                opt.Priority = 4;
                opt.Category = "Game";
                opts.Add(opt);
            }

            // Power plan micro-adjust
            if (_isOnBattery && _batteryPercent < 40)
            {
                var opt = new MicroOptimization();
                opt.Type = MicroOptType.PowerMicroAdjust;
                opt.Description = "Slightly reduce power consumption";
                opt.ExpectedImpact = 0.2;
                opt.Priority = 3;
                opt.Category = "Power";
                opts.Add(opt);
            }

            // Standby list micro-purge (skipped during gaming for the same reason)
            if (_smoothRam > 70 && _smoothTemp < 65 && !_currentState.IsGame)
            {
                var opt = new MicroOptimization();
                opt.Type = MicroOptType.StandbyMicroPurge;
                opt.Description = "Purge small amount from standby list";
                opt.ExpectedImpact = 0.35;
                opt.Priority = 4;
                opt.Category = "Memory";
                opts.Add(opt);
            }

            // Sort by priority
            opts.Sort(delegate(MicroOptimization a, MicroOptimization b) { return b.Priority.CompareTo(a.Priority); });

            return opts;
        }

        // === Execute Micro-Optimization ===
        private bool ExecuteMicroOptimization(MicroOptimization opt)
        {
            try
            {
                switch (opt.Type)
                {
                    case MicroOptType.MemoryMicroClean:
                        return ExecuteMemoryClean();
                    case MicroOptType.BackgroundThrottle:
                        return ExecuteLimitBackground();
                    case MicroOptType.TimerResolution:
                        return SetTimerResolution(TIMER_RESOLUTION_GAMING);
                    case MicroOptType.PriorityMicroAdjust:
                        return ExecuteBoostGamePriority();
                    case MicroOptType.PowerMicroAdjust:
                        return ExecutePowerPlan("power saver");
                    case MicroOptType.StandbyMicroPurge:
                        return (_currentState == null || !_currentState.IsGame) ? ExecutePurgeStandby() : true;
                    default:
                        return false;
                }
            }
            catch { return false; }
        }

        // === Set Timer Resolution (helper) ===
        // Thread-safe + tracked: routes through the same ref-counted system used by
        // ApplyAdaptiveTimerResolution / ResetTimerResolution so Stop() always reverts.
        private bool SetTimerResolution(uint resolution)
        {
            try
            {
                lock (_timerResLock)
                {
                    uint current;
                    int hr = NtSetTimerResolution(resolution, true, out current);
                    if (hr >= 0)
                    {
                        _timerResRefCount++;
                        return true;
                    }
                    return false;
                }
            }
            catch { return false; }
        }
    }

    // === Enums for Continuous Optimization ===
    public enum MicroOptType
    {
        MemoryMicroClean,
        BackgroundThrottle,
        TimerResolution,
        PriorityMicroAdjust,
        PowerMicroAdjust,
        StandbyMicroPurge
    }

    public class MicroOptimization
    {
        public MicroOptType Type;
        public string Description;
        public double ExpectedImpact;
        public int Priority;
        public string Category;
        public DateTime ExecutedAt;
        public bool WasSuccessful;
    }
}
