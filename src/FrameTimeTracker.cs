using System;
using System.Diagnostics;
using System.Windows.Media;

namespace ClearWin
{
    public class FrameTimeTracker
    {
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private readonly double[] _ringBuffer;
        private readonly double[] _recentBuffer;
        private readonly double[] _sortedBuffer;
        private int _ringHead = 0;
        private int _ringCount = 0;
        private int _recentHead = 0;
        private int _recentCount = 0;
        private double _currentFps = 0;
        private double _currentFrameTimeMs = 0;
        private double _fps1Low = 0;
        private double _fps01Low = 0;
        private double _avgFrameTime = 0;
        private double _frameTimeJitter = 0;
        private double _stabilityScore = 100;
        private double _smoothFps = 60;
        private volatile bool _active = false;
        private bool _subscribed = false;
        private DateTime _lastUpdate = DateTime.Now;
        private DateTime _lastRenderTime = DateTime.MinValue;
        private readonly object _lock = new object();

        public double CurrentFps { get { return _currentFps; } }
        public double CurrentFrameTimeMs { get { return _currentFrameTimeMs; } }
        public double Fps1Low { get { return _fps1Low; } }
        public double Fps01Low { get { return _fps01Low; } }
        public double AvgFrameTime { get { return _avgFrameTime; } }
        public double FrameTimeJitter { get { return _frameTimeJitter; } }
        public double StabilityScore { get { return _stabilityScore; } }
        public bool Active { get { return _active; } }
        // True only when the app is actually rendering frames right now. When the
        // window is hidden/minimized/behind an exclusive-fullscreen game, WPF
        // stops rendering and the last FPS value is stale - never feed it to the
        // AI as if it were the game's live FPS.
        public bool IsRenderingLive { get { return _active && (DateTime.Now - _lastRenderTime).TotalMilliseconds < 600; } }

        public FrameTimeTracker()
        {
            _ringBuffer = new double[512];
            _recentBuffer = new double[120];
            _sortedBuffer = new double[512];
            _stopwatch.Start();
            CompositionTarget.Rendering += OnFrame;
            _subscribed = true;
        }

        public void Start()
        {
            _ringHead = 0; _ringCount = 0;
            _recentHead = 0; _recentCount = 0;
            _stopwatch.Restart();
            _lastRenderTime = DateTime.Now;
            if (!_subscribed)
            {
                CompositionTarget.Rendering += OnFrame;
                _subscribed = true;
            }
            _active = true;
        }

        public void Stop()
        {
            _active = false;
            _stopwatch.Stop();
            if (_subscribed)
            {
                CompositionTarget.Rendering -= OnFrame;
                _subscribed = false;
            }
        }

        public void Dispose()
        {
            _active = false;
            _stopwatch.Stop();
            if (_subscribed)
            {
                CompositionTarget.Rendering -= OnFrame;
                _subscribed = false;
            }
            _ringCount = 0;
            _recentCount = 0;
        }

        private void OnFrame(object sender, EventArgs e)
        {
            if (!_active) return;
            _lastRenderTime = DateTime.Now;
            long elapsedTicks = _stopwatch.ElapsedTicks;
            double elapsedMs = (double)elapsedTicks / Stopwatch.Frequency * 1000.0;
            _stopwatch.Restart();
            if (elapsedMs < 1.0 || elapsedMs > 200.0) return;

            _ringBuffer[_ringHead] = elapsedMs;
            _ringHead = (_ringHead + 1) % _ringBuffer.Length;
            if (_ringCount < _ringBuffer.Length) _ringCount++;
            _recentBuffer[_recentHead] = elapsedMs;
            _recentHead = (_recentHead + 1) % _recentBuffer.Length;
            if (_recentCount < _recentBuffer.Length) _recentCount++;

            if (elapsedMs > 0)
            {
                double instantFps = 1000.0 / elapsedMs;
                _smoothFps = _smoothFps * 0.85 + instantFps * 0.15;
                _currentFps = _smoothFps;
            }
            _currentFrameTimeMs = elapsedMs;

            DateTime now2 = DateTime.Now;
            if ((now2 - _lastUpdate).TotalSeconds >= 2.0)
            {
                _lastUpdate = now2;
                CalculateStatsLocked();
            }
        }

        private void CalculateStatsLocked()
        {
            if (_ringCount < 10) return;

            int count = _ringCount;
            for (int i = 0; i < count; i++)
                _sortedBuffer[i] = _ringBuffer[(_ringHead - count + i + _ringBuffer.Length) % _ringBuffer.Length];

            InsertionSort(_sortedBuffer, count);

            double sum = 0;
            for (int i = 0; i < count; i++) sum += _sortedBuffer[i];
            _avgFrameTime = sum / count;

            int p99Idx = (int)Math.Min(count * 0.99, count - 1);
            _fps1Low = 1000.0 / _sortedBuffer[p99Idx];
            int p999Idx = (int)Math.Min(count * 0.999, count - 1);
            _fps01Low = 1000.0 / _sortedBuffer[p999Idx];

            double sumSqDiff = 0;
            for (int i = 1; i < count; i++)
            {
                double diff = _sortedBuffer[i] - _sortedBuffer[i - 1];
                sumSqDiff += diff * diff;
            }
            _frameTimeJitter = Math.Sqrt(sumSqDiff / Math.Max(1, count - 1));

            if (_currentFps > 0 && _fps1Low > 0)
            {
                double ratio = _fps1Low / _currentFps;
                _stabilityScore = ratio * 100;
                if (_frameTimeJitter > 5) _stabilityScore -= _frameTimeJitter * 2;
            }
            _stabilityScore = Math.Max(0, Math.Min(100, _stabilityScore));
        }

        private static void InsertionSort(double[] arr, int len)
        {
            for (int i = 1; i < len; i++)
            {
                double key = arr[i];
                int j = i - 1;
                while (j >= 0 && arr[j] > key)
                {
                    arr[j + 1] = arr[j];
                    j--;
                }
                arr[j + 1] = key;
            }
        }

        public void Reset()
        {
            _ringHead = 0; _ringCount = 0;
            _recentHead = 0; _recentCount = 0;
            _currentFps = 0;
            _currentFrameTimeMs = 0;
            _fps1Low = 0;
            _fps01Low = 0;
            _avgFrameTime = 0;
            _frameTimeJitter = 0;
            _stabilityScore = 100;
        }
    }
}
