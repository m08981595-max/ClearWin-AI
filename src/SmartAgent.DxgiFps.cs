using System;
using System.Runtime.InteropServices;

namespace ClearWin
{
    /// <summary>
    /// Real presented-frame rate measured from the GPU output itself via DXGI
    /// frame statistics — the non-injection equivalent of Smart Game Booster's
    /// D3D swap-chain FPS hook. It counts the frames actually presented on the
    /// active display output (IDXGIOutput::GetFrameStatistics), which for a
    /// fullscreen game equals the game's frame rate.
    ///
    /// Fully defensive: every COM step is guarded; returns 0 when unavailable.
    /// The reader is polled on a slow cadence and smooths the rate, so it is
    /// cheap enough for the agent tick loop.
    ///
    /// vtable slots (DXGI ABI): IUnknown 0..2, then per-interface members.
    ///   IDXGIFactory1.EnumAdapters1      -> slot 12 (IUnknown 0-2, IDXGIObject
    ///                                       3-6, IDXGIFactory 7-11, then 12)
    ///   IDXGIAdapter1.EnumOutputs        -> slot 7  (IDXGIObject 3-6, then 7)
    ///   IDXGIOutput.GetFrameStatistics   -> slot 10 (GetDesc 7, ... FrameStats 10)
    /// </summary>
    public static class DxgiFpsReader
    {
        private static readonly object _lock = new object();
        private static readonly Guid IID_IDXGIFactory1 = new Guid("770aae78-f26f-4dba-a829-253c83d1b387");

        private static ulong _lastPresentCount;
        private static double _lastSeconds;
        private static bool _haveBaseline;
        private static double _smoothFps;
        private static int _sampleCounter;

        [StructLayout(LayoutKind.Sequential)]
        private struct DxgiFrameStatistics
        {
            public uint PresentCount;
            public uint PresentRefreshCount;
            public uint SyncRefreshCount;
            public long SyncQpcTime;
            public long SyncGpuTime;
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int EnumAdapters1(IntPtr factory, uint adapterIndex, out IntPtr adapter);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int EnumOutputs(IntPtr adapter, uint outputIndex, out IntPtr output);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int GetFrameStatistics(IntPtr output, ref DxgiFrameStatistics stats);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int ReleaseCom(IntPtr com);

        [DllImport("dxgi.dll")]
        private static extern int CreateDXGIFactory1(ref Guid riid, out IntPtr factory);

        /// <summary>
        /// Display frame rate in FPS (smoothed), or 0 when the reader cannot
        /// obtain a sample (no active GPU output, remote session, etc.).
        /// </summary>
        public static double ReadDisplayFps()
        {
            try
            {
                Guid factoryGuid = IID_IDXGIFactory1;
                IntPtr factory;
                if (CreateDXGIFactory1(ref factoryGuid, out factory) != 0 || factory == IntPtr.Zero) return _smoothFps;
                try
                {
                    IntPtr adapter;
                    if (EnumAdapters1Del(factory)(factory, 0, out adapter) != 0 || adapter == IntPtr.Zero) return _smoothFps;
                    try
                    {
                        IntPtr output;
                        if (EnumOutputsDel(adapter)(adapter, 0, out output) != 0 || output == IntPtr.Zero) return _smoothFps;
                        try
                        {
                            DxgiFrameStatistics stats = new DxgiFrameStatistics();
                            int hr = GetFrameStatisticsDel(output)(output, ref stats);
                            if (hr != 0) return _smoothFps; // DISJOINT / not yet presented -> keep last
                            double seconds = StopwatchSeconds();
                            lock (_lock)
                            {
                                if (_haveBaseline)
                                {
                                    double dt = seconds - _lastSeconds;
                                    ulong dc = stats.PresentCount - _lastPresentCount;
                                    if (dt > 0.1 && dt < 5.0 && dc > 0)
                                    {
                                        double inst = dc / dt;
                                        if (inst > 0 && inst < 1000)
                                        {
                                            _smoothFps = _sampleCounter == 0 ? inst : _smoothFps * 0.7 + inst * 0.3;
                                            _sampleCounter++;
                                        }
                                    }
                                }
                                _lastPresentCount = stats.PresentCount;
                                _lastSeconds = seconds;
                                _haveBaseline = true;
                            }
                            return _smoothFps;
                        }
                        finally { SafeRelease(output); }
                    }
                    finally { SafeRelease(adapter); }
                }
                finally { SafeRelease(factory); }
            }
            catch { return _smoothFps; }
        }

        private static void SafeRelease(IntPtr com)
        {
            if (com != IntPtr.Zero)
            {
                try { Marshal.Release(com); } catch {}
            }
        }

        private static T Method<T>(IntPtr com, int vtableIndex) where T : class
        {
            IntPtr vtable = Marshal.ReadIntPtr(com);
            IntPtr fn = Marshal.ReadIntPtr(vtable, IntPtr.Size * vtableIndex);
            return (T)(object)Marshal.GetDelegateForFunctionPointer(fn, typeof(T));
        }

        private static EnumAdapters1 EnumAdapters1Del(IntPtr com) { return Method<EnumAdapters1>(com, 12); }
        private static EnumOutputs EnumOutputsDel(IntPtr com) { return Method<EnumOutputs>(com, 7); }
        private static GetFrameStatistics GetFrameStatisticsDel(IntPtr com) { return Method<GetFrameStatistics>(com, 10); }
        private static ReleaseCom ReleaseDel(IntPtr com) { return Method<ReleaseCom>(com, 2); }

        private static double StopwatchSeconds()
        {
            return System.Diagnostics.Stopwatch.GetTimestamp() / (double)System.Diagnostics.Stopwatch.Frequency;
        }
    }
}
