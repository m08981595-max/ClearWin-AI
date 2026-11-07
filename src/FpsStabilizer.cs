using System;
using System.Threading;

namespace ClearWin
{
    /// <summary>Optimization the AI FPS Stabilizer recommends when a frame drop is predicted.</summary>
    public enum FpsStabilizerAction
    {
        None = 0,
        BoostPriority,
        CleanMemory,
        PurgeStandby,
        LimitBackground,
        DeferWork
    }

    /// <summary>Raw per-tick telemetry fed into the stabilizer (UI-free, testable).</summary>
    public class FpsStabilizerInput
    {
        public double Fps;
        public double FrameTimeMs;
        public double Fps1Low;
        public double CpuUsage;
        public double RamUsage;
        public double GpuUsage;
        public double CpuTemp;
        public bool IsGaming;
        // True when the frame telemetry is live (the app is actually rendering).
        // False when ClearWin is hidden/minimized/behind a fullscreen game: the
        // FPS value is stale and must never drive drop prediction or the FPS floor.
        public bool FrameDataReliable = true;
    }

    /// <summary>Computed stats handed to the predictor (modular seam for a future local AI model).</summary>
    public class FpsStabilizerContext
    {
        public double CurrentFps;
        public double AvgFps5;
        public double AvgFps10;
        public double Fps1Low;
        public double FrameTimeMs;
        public double FrameTimeTrend;   // ms/sample (positive = frame times climbing = risky)
        public double FrameTimeJitter;  // average abs delta between consecutive frame times
        public double CpuUsage, RamUsage, GpuUsage, CpuTemp;
        public bool IsGaming;
    }

    /// <summary>Prediction engine contract. Swap in a local AI model later without touching the core.</summary>
    public interface IFpsDropPredictor
    {
        double PredictRisk(FpsStabilizerContext ctx);
        string Explain(FpsStabilizerContext ctx);
    }

    /// <summary>Rule-based weighted predictor (fast, always available).</summary>
    public class HeuristicFpsDropPredictor : IFpsDropPredictor
    {
        public double PredictRisk(FpsStabilizerContext ctx)
        {
            double risk = 0;
            if (ctx.FrameTimeTrend > 0.6) risk += 0.25;
            if (ctx.FrameTimeTrend > 1.5) risk += 0.2;
            if (ctx.FrameTimeJitter > 6) risk += 0.15;
            if (ctx.AvgFps10 > 0 && ctx.CurrentFps > 0 && ctx.CurrentFps < ctx.AvgFps10 * 0.75) risk += 0.15;
            if (ctx.CpuUsage > 80) risk += 0.15;
            if (ctx.RamUsage > 85) risk += 0.15;
            if (ctx.GpuUsage > 85) risk += 0.1;
            if (ctx.CpuTemp > 75) risk += 0.1;
            return Math.Max(0, Math.Min(1, risk));
        }

        public string Explain(FpsStabilizerContext ctx)
        {
            string why = "frame-time " + ctx.FrameTimeTrend.ToString("F1") + "ms/s, jitter " + ctx.FrameTimeJitter.ToString("F1") + "ms";
            why += ", cpu " + ctx.CpuUsage.ToString("F0") + "%, ram " + ctx.RamUsage.ToString("F0") + "%, gpu " + ctx.GpuUsage.ToString("F0") + "%, temp " + ctx.CpuTemp.ToString("F0") + "C";
            return why;
        }
    }

    /// <summary>Regression-based predictor: extrapolates frame-time slope and rewards pressure.</summary>
    public class RegressionFpsDropPredictor : IFpsDropPredictor
    {
        public double PredictRisk(FpsStabilizerContext ctx)
        {
            double risk = 0;
            if (ctx.FrameTimeTrend > 0.5) risk += 0.3;
            if (ctx.FrameTimeTrend > 2.0) risk += 0.2;
            if (ctx.FrameTimeJitter > 6) risk += 0.15;
            if (ctx.AvgFps10 > 0 && ctx.CurrentFps > 0 && ctx.CurrentFps < ctx.AvgFps10 * 0.7) risk += 0.2;
            if (ctx.CpuUsage > 80) risk += 0.1;
            if (ctx.RamUsage > 85) risk += 0.1;
            if (ctx.GpuUsage > 85) risk += 0.1;
            if (ctx.CpuTemp > 75) risk += 0.1;
            return Math.Max(0, Math.Min(1, risk));
        }

        public string Explain(FpsStabilizerContext ctx)
        {
            string why = "slope " + ctx.FrameTimeTrend.ToString("F2") + "ms/sample";
            if (ctx.FrameTimeJitter > 6) why += ", jitter high (" + ctx.FrameTimeJitter.ToString("F1") + "ms)";
            if (ctx.CpuUsage > 80) why += ", cpu " + ctx.CpuUsage.ToString("F0") + "%";
            if (ctx.RamUsage > 85) why += ", ram " + ctx.RamUsage.ToString("F0") + "%";
            if (ctx.CpuTemp > 75) why += ", temp " + ctx.CpuTemp.ToString("F0") + "C";
            return why;
        }
    }

    /// <summary>
    /// Mobile Workstation FPS Drop Predictor — tuned for devices like HP EliteBook w8570
    /// running Android emulators (GameLoop/LDPlayer) at 30-60fps targets.
    /// Key differences from RegressionFpsDropPredictor:
    ///   - Lower thresholds (mobile CPUs thermal-throttle faster)
    ///   - Higher jitter tolerance (emulator overhead creates natural frame-time variation)
    ///   - Earlier intervention (thin laptops can't sustain high CPU without throttling)
    ///   - Aggressive temperature sensitivity (thermal throttling kills emulator perf)
    /// </summary>
    public class MobileWorkstationFpsDropPredictor : IFpsDropPredictor
    {
        // Slightly more aggressive thresholds for mobile workstation scenarios
        private const double MOBILE_FRAME_TREND_THRESHOLD = 0.4;
        private const double MOBILE_FRAME_TREND_SEVERE = 1.5;
        private const double MOBILE_JITTER_THRESHOLD = 5.0;
        private const double MOBILE_CPU_THRESHOLD = 70;
        private const double MOBILE_TEMP_THRESHOLD = 68;
        private const double MOBILE_RAMDROP_THRESHOLD = 75;

        public double PredictRisk(FpsStabilizerContext ctx)
        {
            double risk = 0;

            // Frame-time trend — mobile workstations show faster degradation
            if (ctx.FrameTimeTrend > MOBILE_FRAME_TREND_THRESHOLD) risk += 0.35;
            if (ctx.FrameTimeTrend > MOBILE_FRAME_TREND_SEVERE) risk += 0.25;

            // Jitter — emulator workloads have higher natural jitter, so
            // we trigger slightly earlier to prevent cascading stutters
            if (ctx.FrameTimeJitter > MOBILE_JITTER_THRESHOLD) risk += 0.20;

            // FPS drop below average — more sensitive on mobile (emulator can't recover as fast)
            if (ctx.AvgFps10 > 0 && ctx.CurrentFps > 0 && ctx.CurrentFps < ctx.AvgFps10 * 0.75)
                risk += 0.20;

            // CPU — mobile CPUs hit thermal wall faster than desktop
            if (ctx.CpuUsage > MOBILE_CPU_THRESHOLD) risk += 0.15;

            // Temperature — the critical differentiator for EliteBook w8570
            // Thin chassis + mobile i7 = thermal throttling starts ~68C
            if (ctx.CpuTemp > MOBILE_TEMP_THRESHOLD) risk += 0.20;

            // RAM — moderate threshold (emulator + Windows = memory pressure)
            if (ctx.RamUsage > MOBILE_RAMDROP_THRESHOLD) risk += 0.10;

            return Math.Max(0, Math.Min(1, risk));
        }

        public string Explain(FpsStabilizerContext ctx)
        {
            string why = "slope " + ctx.FrameTimeTrend.ToString("F2") + "ms/sample";
            if (ctx.FrameTimeJitter > MOBILE_JITTER_THRESHOLD)
                why += ", jitter " + ctx.FrameTimeJitter.ToString("F1") + "ms";
            if (ctx.CpuUsage > MOBILE_CPU_THRESHOLD)
                why += ", cpu " + ctx.CpuUsage.ToString("F0") + "%";
            if (ctx.CpuTemp > MOBILE_TEMP_THRESHOLD)
                why += ", TEMP-ALERT " + ctx.CpuTemp.ToString("F0") + "C";
            if (ctx.RamUsage > MOBILE_RAMDROP_THRESHOLD)
                why += ", ram " + ctx.RamUsage.ToString("F0") + "%";
            return why;
        }
    }

    /// <summary>
    /// AI FPS Stabilizer core. Modular, UI-free, very low overhead (pre-allocated
    /// ring buffers, one cheap pass per tick). Detects sudden frame drops, predicts
    /// upcoming drops from CPU/GPU/RAM/temp/frame-time trends and reports which
    /// optimization to apply. A pluggable IFpsDropPredictor is the seam for a future
    /// local AI model.
    /// </summary>
    public class FpsStabilizer
    {
        private const int WINDOW = 10;
        private const double HOLD_COOLDOWN_SECONDS = 20.0;
        private readonly double[] _fpsHistory = new double[WINDOW];
        private readonly double[] _frameMsHistory = new double[WINDOW];
        private readonly DateTime[] _actionTimes = new DateTime[20];
        private int _actionTimesCount = 0;
        private readonly object _lock = new object();
        private readonly IFpsDropPredictor _predictor;
        private readonly double _cooldownSeconds;
        private readonly int _maxActionsPerMinute;
        private readonly double _sampleIntervalSeconds;
        private readonly double _preventionWindowSeconds;

        private int _head = 0;
        private int _count = 0;
        private DateTime _lastSampleTime = DateTime.MinValue;
        // Lock-free read fields. Written only inside Update()/Reset() under
        // _lock; read by UI/agent threads through volatile reads so the hot
        // per-tick property polls never take the lock (see properties below).
        private double _currentFps;
        private double _currentFrameMs;
        private double _fps1Low;
        private double _jitter;
        private double _stabilityScore = 100;
        private double _predictionRisk;
        private double _avgFps5;
        private double _avgFps10;
        private volatile string _predictionReason = "";
        private volatile FpsStabilizerAction _lastAction = FpsStabilizerAction.None;
        private long _lastActionTimeTicks = DateTime.MinValue.Ticks;
        private int _preventedDrops = 0;
        private int _dropCount = 0;
        private volatile bool _armed = false;
        private DateTime _armedTime = DateTime.MinValue;
        private double _armedBaseline = 0;
        private int _holdsBelowTarget = 0;
        private volatile bool _aggressiveHold = false;
        private double _targetFps = 0;

        public FpsStabilizer() : this(new RegressionFpsDropPredictor(), 25, 2) { }

        public FpsStabilizer(IFpsDropPredictor predictor, double cooldownSeconds, int maxActionsPerMinute)
            : this(predictor, cooldownSeconds, maxActionsPerMinute, 1.0, 8.0) { }

        public FpsStabilizer(IFpsDropPredictor predictor, double cooldownSeconds, int maxActionsPerMinute,
            double sampleIntervalSeconds, double preventionWindowSeconds)
        {
            _predictor = predictor ?? new RegressionFpsDropPredictor();
            _cooldownSeconds = Math.Max(0, cooldownSeconds);
            _maxActionsPerMinute = Math.Max(1, maxActionsPerMinute);
            _sampleIntervalSeconds = Math.Max(0.0001, sampleIntervalSeconds);
            _preventionWindowSeconds = Math.Max(0.05, preventionWindowSeconds);
        }

        // All reads below are lock-free (volatile/Interlocked). Update() is the
        // only writer and serializes on _lock, so a reader always sees a
        // complete value; AvgFps5/AvgFps10 use snapshots cached during Update.
        public double CurrentFps { get { return Thread.VolatileRead(ref _currentFps); } }
        public double AvgFps5 { get { return Thread.VolatileRead(ref _avgFps5); } }
        public double AvgFps10 { get { return Thread.VolatileRead(ref _avgFps10); } }
        public double Fps1Low { get { return Thread.VolatileRead(ref _fps1Low); } }
        public double FrameTimeMs { get { return Thread.VolatileRead(ref _currentFrameMs); } }
        public double FrameTimeJitter { get { return Thread.VolatileRead(ref _jitter); } }
        public double StabilityScore { get { return Thread.VolatileRead(ref _stabilityScore); } }
        public double PredictionRisk { get { return Thread.VolatileRead(ref _predictionRisk); } }
        public string PredictionReason { get { return _predictionReason; } }
        public int PreventedDrops { get { return Thread.VolatileRead(ref _preventedDrops); } }
        public int DropCount { get { return Thread.VolatileRead(ref _dropCount); } }
        public FpsStabilizerAction LastAction { get { return _lastAction; } }
        public DateTime LastActionTime { get { return new DateTime(Interlocked.Read(ref _lastActionTimeTicks)); } }
        public bool PredictionArmed { get { return _armed; } }

        // === FPS floor (target) mode: never drop below TargetFps ===
        // TargetFps = 0 disables the floor. AggressiveHold turns on proactive
        // mitigation the moment FPS dips below the target (not waiting for a
        // >=0.6 drop-risk prediction), reinforcing every HOLD_COOLDOWN_SECONDS.
        public double TargetFps
        {
            get { return Thread.VolatileRead(ref _targetFps); }
            set { Interlocked.Exchange(ref _targetFps, value); }
        }
        public bool AggressiveHold { get { return _aggressiveHold; } set { _aggressiveHold = value; } }
        public int HoldsBelowTarget { get { return Thread.VolatileRead(ref _holdsBelowTarget); } }
        public bool BelowTarget
        {
            get
            {
                double tf = Thread.VolatileRead(ref _targetFps);
                double fps = Thread.VolatileRead(ref _currentFps);
                return tf > 0 && fps > 0 && fps < tf;
            }
        }
        public double TargetFpsDeficit
        {
            get
            {
                double tf = Thread.VolatileRead(ref _targetFps);
                double fps = Thread.VolatileRead(ref _currentFps);
                if (tf <= 0 || fps <= 0) return 0;
                return Math.Max(0, tf - fps);
            }
        }

        /// <summary>
        /// Feed one tick of telemetry. Returns the action to apply right now
        /// (None = nothing needed). Extremely cheap: O(10) fixed-buffer work.
        /// </summary>
        public FpsStabilizerAction Update(FpsStabilizerInput input)
        {
            lock (_lock)
            {
                double fps = input.Fps;
                if (fps < 0) fps = 0;
                if (fps > 1000) fps = 1000;
                double frameMs = input.FrameTimeMs;
                if (frameMs < 0) frameMs = 0;
                if (frameMs > 500) frameMs = 500;

                DateTime now = DateTime.Now;
                if ((now - _lastSampleTime).TotalSeconds >= _sampleIntervalSeconds || _count == 0)
                {
                    _lastSampleTime = now;
                    _fpsHistory[_head] = fps;
                    _frameMsHistory[_head] = frameMs;
                    _head = (_head + 1) % WINDOW;
                    if (_count < WINDOW) _count++;
                }
                else
                {
                    int last = (_head - 1 + WINDOW) % WINDOW;
                    _fpsHistory[last] = fps;
                    _frameMsHistory[last] = frameMs;
                }

                _currentFps = fps;
                _currentFrameMs = frameMs;
                _fps1Low = input.Fps1Low;

                // === No live frame data: never act on a stale value ===
                // The system metrics are still tracked below, but FPS-based risk,
                // sudden-drop detection and the 60 FPS floor all require a real,
                // live frame stream. Exception: during gaming the stabilizer may
                // still act on live system metrics (CPU/GPU/RAM/temp) even when
                // ClearWin itself is not rendering.
                if (!input.FrameDataReliable && !input.IsGaming)
                {
                    _predictionRisk = 0;
                    _predictionReason = "no live frame data (ClearWin not rendering)";
                    return FpsStabilizerAction.None;
                }

                double avg10 = Average(WINDOW);
                double jitter = ComputeJitter();
                double trend = FrameTimeTrend();
                _jitter = jitter;
                // Cache average snapshots so the lock-free readers (AvgFps5/10)
                // never touch the ring buffer without the lock.
                _avgFps5 = Average(5);
                _avgFps10 = avg10;

                // === Stability score (0-100) ===
                double score = 100;
                if (fps > 0 && avg10 > 0 && fps < avg10)
                    score -= (avg10 - fps) / avg10 * 100;
                if (jitter > 4)
                    score -= (jitter - 4) * 6;
                if (fps > 0 && fps < 30)
                    score -= (30 - fps) * 1.2;
                _stabilityScore = Math.Max(0, Math.Min(100, score));

                // === Sudden drop detection (not just low averages) ===
                if (IsSuddenDrop(fps, avg10))
                {
                    _dropCount++;
                    _armed = false; // a real drop happened - the prediction did not hold
                }
                else if (_armed && (now - _armedTime).TotalSeconds >= _preventionWindowSeconds)
                {
                    if (fps >= _armedBaseline * 0.8)
                        _preventedDrops++; // the predicted drop never materialized
                    _armed = false;
                }

                // === Prediction ===
                var ctx = new FpsStabilizerContext
                {
                    CurrentFps = fps,
                    AvgFps5 = Average(5),
                    AvgFps10 = avg10,
                    Fps1Low = _fps1Low,
                    FrameTimeMs = frameMs,
                    FrameTimeTrend = trend,
                    FrameTimeJitter = jitter,
                    CpuUsage = input.CpuUsage,
                    RamUsage = input.RamUsage,
                    GpuUsage = input.GpuUsage,
                    CpuTemp = input.CpuTemp,
                    IsGaming = input.IsGaming
                };
                _predictionRisk = _predictor.PredictRisk(ctx);
                _predictionReason = _predictor.Explain(ctx);

                FpsStabilizerAction action = FpsStabilizerAction.None;
                double targetFps = Thread.VolatileRead(ref _targetFps);
                bool belowTarget = targetFps > 0 && fps > 0 && fps < targetFps;
                if (belowTarget && AggressiveHold)
                {
                    // Floor mode: proactive hold - act immediately and keep
                    // reinforcing so FPS never dips below the target.
                    double effectiveHoldCooldown = (targetFps - fps >= 10) ? 3.0 : ((targetFps - fps >= 5) ? 5.0 : HOLD_COOLDOWN_SECONDS);
                    int holdAllowance = (targetFps - fps >= 10) ? 15 : 6;
                    if ((now - new DateTime(Interlocked.Read(ref _lastActionTimeTicks))).TotalSeconds >= effectiveHoldCooldown && !RateLimited(now, holdAllowance))
                    {
                        action = SelectHoldAction(ctx);
                        if (action != FpsStabilizerAction.None)
                        {
                            _lastAction = action;
                            _lastActionTimeTicks = now.Ticks;
                            RecordActionTime(now);
                            _holdsBelowTarget++;
                        }
                    }
                }
                // During gaming the prediction relies more on live system pressure
                // (stale/no game FPS is common when ClearWin is behind a fullscreen
                // game), so a lower threshold lets real resource stress trigger the
                // same safe mitigations.
                else if (_predictionRisk >= (input.IsGaming ? 0.4 : 0.6) && !_armed && CanAct(now))
                {
                    action = SelectAction(ctx);
                    if (action != FpsStabilizerAction.None)
                    {
                        _lastAction = action;
                        _lastActionTimeTicks = now.Ticks;
                        RecordActionTime(now);
                        _armed = true;
                        _armedTime = now;
                        _armedBaseline = avg10 > 0 ? avg10 : fps;
                    }
                }
                return action;
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _head = 0; _count = 0;
                _currentFps = 0; _currentFrameMs = 0; _fps1Low = 0; _jitter = 0;
                _stabilityScore = 100; _predictionRisk = 0; _predictionReason = "";
                _avgFps5 = 0; _avgFps10 = 0;
                _preventedDrops = 0; _dropCount = 0;
                _armed = false; _actionTimesCount = 0;
                _lastAction = FpsStabilizerAction.None;
                _lastActionTimeTicks = DateTime.MinValue.Ticks;
                _lastSampleTime = DateTime.MinValue;
            }
        }

        private double Average(int n)
        {
            if (_count == 0) return 0;
            int take = Math.Min(n, _count);
            int start = (_head - take + WINDOW) % WINDOW;
            double sum = 0; int valid = 0;
            for (int i = 0; i < take; i++)
            {
                int idx = (start + i) % WINDOW;
                if (_fpsHistory[idx] > 0) { sum += _fpsHistory[idx]; valid++; }
            }
            return valid > 0 ? sum / valid : 0;
        }

        private bool IsSuddenDrop(double fps, double avg10)
        {
            if (_count < 4 || fps <= 0) return false;
            if (avg10 >= 20 && fps < avg10 * 0.6) return true;
            double avg5 = Average(5);
            if (avg5 >= 20 && fps < avg5 * 0.55) return true;
            double avgFt = AverageFrameMs();
            if (avgFt > 3 && _currentFrameMs > avgFt * 2.0 && _currentFrameMs > 30) return true;
            return false;
        }

        private double AverageFrameMs()
        {
            if (_count == 0) return 0;
            int take = Math.Min(5, _count);
            int start = (_head - take + WINDOW) % WINDOW;
            double sum = 0; int valid = 0;
            for (int i = 0; i < take; i++)
            {
                int idx = (start + i) % WINDOW;
                if (_frameMsHistory[idx] > 0) { sum += _frameMsHistory[idx]; valid++; }
            }
            return valid > 0 ? sum / valid : 0;
        }

        private double ComputeJitter()
        {
            if (_count < 2) return 0;
            int take = Math.Min(_count, WINDOW);
            int start = (_head - take + WINDOW) % WINDOW;
            double prev = _frameMsHistory[start];
            double sum = 0; int pairs = 0;
            for (int i = 1; i < take; i++)
            {
                int idx = (start + i) % WINDOW;
                double d = Math.Abs(_frameMsHistory[idx] - prev);
                sum += d; pairs++; prev = _frameMsHistory[idx];
            }
            return pairs > 0 ? sum / pairs : 0;
        }

        private double FrameTimeTrend()
        {
            if (_count < 3) return 0;
            int take = Math.Min(5, _count);
            int start = (_head - take + WINDOW) % WINDOW;
            double sumX = 0, sumY = 0, sumXY = 0, sumXX = 0;
            int n = 0;
            for (int i = 0; i < take; i++)
            {
                int idx = (start + i) % WINDOW;
                double x = i, y = _frameMsHistory[idx];
                sumX += x; sumY += y; sumXY += x * y; sumXX += x * x;
                n++;
            }
            if (n == 0) return 0;
            double denom = n * sumXX - sumX * sumX;
            if (Math.Abs(denom) < 1e-9) return 0;
            return (n * sumXY - sumX * sumY) / denom;
        }

        private bool CanAct(DateTime now)
        {
            if (_cooldownSeconds > 0 && (now - new DateTime(Interlocked.Read(ref _lastActionTimeTicks))).TotalSeconds < _cooldownSeconds)
                return false;
            int recent = 0;
            for (int i = 0; i < _actionTimesCount; i++)
                if ((now - _actionTimes[i]).TotalSeconds < 60) recent++;
            return recent < _maxActionsPerMinute;
        }

        private void RecordActionTime(DateTime now)
        {
            if (_actionTimesCount < _actionTimes.Length)
            {
                _actionTimes[_actionTimesCount] = now;
                _actionTimesCount++;
            }
            else
            {
                for (int i = 0; i < _actionTimes.Length - 1; i++)
                    _actionTimes[i] = _actionTimes[i + 1];
                _actionTimes[_actionTimes.Length - 1] = now;
            }
        }

        private bool RateLimited(DateTime now, int allowancePerMinute)
        {
            int recent = 0;
            for (int i = 0; i < _actionTimesCount; i++)
                if ((now - _actionTimes[i]).TotalSeconds < 60) recent++;
            return recent >= allowancePerMinute;
        }

        /// <summary>Floor-mode action picker: the single strongest stability lever
        /// for the current pressure, repeated every hold cooldown.</summary>
        private FpsStabilizerAction SelectHoldAction(FpsStabilizerContext ctx)
        {
            if (ctx.IsGaming)
                return FpsStabilizerAction.BoostPriority;
            if (ctx.RamUsage >= 80)
                return FpsStabilizerAction.PurgeStandby;
            if (ctx.CpuUsage >= 70)
                return FpsStabilizerAction.LimitBackground;
            if (ctx.CpuTemp >= 70)
                return FpsStabilizerAction.DeferWork;
            return FpsStabilizerAction.PurgeStandby;
        }

        private FpsStabilizerAction SelectAction(FpsStabilizerContext ctx)
        {
            if (ctx.IsGaming && (ctx.CurrentFps < 40 || ctx.AvgFps5 < 45))
                return FpsStabilizerAction.BoostPriority;
            if (ctx.RamUsage >= 85)
                return FpsStabilizerAction.CleanMemory;
            if (ctx.CpuTemp >= 75 || (ctx.CpuUsage >= 80 && ctx.GpuUsage >= 70))
                return FpsStabilizerAction.DeferWork;
            if (ctx.CpuUsage >= 75 || ctx.RamUsage >= 70)
                return FpsStabilizerAction.LimitBackground;
            return FpsStabilizerAction.PurgeStandby;
        }
    }
}
