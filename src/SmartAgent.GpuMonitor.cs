using System;
using System.Runtime.InteropServices;

namespace ClearWin
{
    /// <summary>
    /// GPU hardware monitor modeled on Smart Game Booster's approach: it reads the
    /// real GPU temperature straight from the vendor SDK instead of relying on
    /// ACPI thermal-zone counters (which often do not exist, leaving GpuTemp at 0).
    ///
    /// Provider order (each guarded, never throws):
    ///   1) NVIDIA NVAPI  (nvapi.dll)          - GPU core sensor
    ///   2) AMD ADL       (atiadlxx/atiadlxy)  - Overdrive5 temperature
    ///   3) Fallback      (-1 = unavailable)   -> callers keep their old path
    ///
    /// The SDKs are loaded lazily once per process and cached, so the hot tick
    /// loop pays only for the marshalled temperature query, never for the load.
    /// </summary>
    public static class GpuMonitor
    {
        private static readonly object _lock = new object();
        private static bool _initAttempted;
        private static IntPtr _nvDll;
        private static IntPtr _adlDll;
        private static IntPtr[] _nvGpus;
        private static int _adlContext;
        private static int _adlAdapterCount;

        // ---- NVAPI ----
        private const uint NVAPI_INITIALIZE_ID = 0x0150E828;
        private const uint NVAPI_ENUM_PHYSICAL_GPUS_ID = 0xE5AC921F;
        private const uint NVAPI_GPU_GET_THERMAL_SETTINGS_ID = 0xE6540D80;

        private const int NVAPI_THERMAL_TARGET_GPU = 1;
        private const int NVAPI_OK = 0;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr NvapiQueryInterface(uint functionId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int NvapiInitialize();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int NvapiEnumPhysicalGpus(IntPtr[] handles, out int gpuCount);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int NvapiGetThermalSettings(IntPtr gpuHandle, int sensorIndex, ref NvGpuThermalSettings settings);

        [StructLayout(LayoutKind.Sequential)]
        private struct NvThermalSensor
        {
            public int Controller;
            public uint DefaultMinTemp;
            public uint DefaultMaxTemp;
            public uint CurrentTemp;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NvGpuThermalSettings
        {
            public uint Version;
            public uint Count;
            public NvThermalSensor Sensors0;
            public NvThermalSensor Sensors1;
            public NvThermalSensor Sensors2;
        }

        // ---- AMD ADL ----
        private const int ADL_OK = 0;

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int AdlMainControlCreate(IntPtr callback, out int numberOfAdapters, out int context);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int AdlAdapterAdapterInfoGet(int context, IntPtr info, int size);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int AdlAdapterActiveGet(int context, int adapterIndex, out int isActive);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int AdlOverdrive5TemperatureGet(int adapterIndex, int thermalControllerIndex, ref AdlTemperature temp);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate int AdlMainControlDestroy(int context);

        [StructLayout(LayoutKind.Sequential)]
        private struct AdlTemperature
        {
            public int Size;
            public int Temperature; // 0.001 C
            public int FanSpeed;
        }

        private static void EnsureInit()
        {
            if (_initAttempted) return;
            lock (_lock)
            {
                if (_initAttempted) return;
                _initAttempted = true;
                try { InitNvapi(); } catch { _nvDll = IntPtr.Zero; }
                try { InitAdl(); } catch { _adlDll = IntPtr.Zero; }
            }
        }

        private static void InitNvapi()
        {
            _nvDll = LoadLibrary("nvapi.dll");
            if (_nvDll == IntPtr.Zero) return;
            IntPtr queryProc = GetProcAddress(_nvDll, "nvapi_QueryInterface");
            if (queryProc == IntPtr.Zero) { FreeLibrary(_nvDll); _nvDll = IntPtr.Zero; return; }
            var query = (NvapiQueryInterface)Marshal.GetDelegateForFunctionPointer(queryProc, typeof(NvapiQueryInterface));

            IntPtr initPtr = query(NVAPI_INITIALIZE_ID);
            if (initPtr == IntPtr.Zero) return;
            var init = (NvapiInitialize)Marshal.GetDelegateForFunctionPointer(initPtr, typeof(NvapiInitialize));
            if (init() != NVAPI_OK) return;

            IntPtr enumPtr = query(NVAPI_ENUM_PHYSICAL_GPUS_ID);
            IntPtr thermalPtr = query(NVAPI_GPU_GET_THERMAL_SETTINGS_ID);
            if (enumPtr == IntPtr.Zero || thermalPtr == IntPtr.Zero) return;

            var enumGpus = (NvapiEnumPhysicalGpus)Marshal.GetDelegateForFunctionPointer(enumPtr, typeof(NvapiEnumPhysicalGpus));
            // NVAPI writes up to NVAPI_MAX_PHYSICAL_GPUS (64) handles; a smaller
            // managed array would be overrun by the native write.
            IntPtr[] gpus = new IntPtr[64];
            int count;
            if (enumGpus(gpus, out count) != NVAPI_OK || count <= 0) return;
            _nvGpus = new IntPtr[count];
            Array.Copy(gpus, _nvGpus, count);
        }

        private static void InitAdl()
        {
            _adlDll = LoadLibrary("atiadlxx.dll");
            if (_adlDll == IntPtr.Zero) _adlDll = LoadLibrary("atiadlxy.dll");
            if (_adlDll == IntPtr.Zero) return;

            IntPtr createPtr = GetProcAddress(_adlDll, "ADL_Main_Control_Create");
            IntPtr infoPtr = GetProcAddress(_adlDll, "ADL_Adapter_AdapterInfo_Get");
            IntPtr activePtr = GetProcAddress(_adlDll, "ADL_Adapter_Active_Get");
            IntPtr tempPtr = GetProcAddress(_adlDll, "ADL_Overdrive5_Temperature_Get");
            if (createPtr == IntPtr.Zero || infoPtr == IntPtr.Zero || activePtr == IntPtr.Zero || tempPtr == IntPtr.Zero) return;

            var create = (AdlMainControlCreate)Marshal.GetDelegateForFunctionPointer(createPtr, typeof(AdlMainControlCreate));
            int adapters;
            int context;
            if (create(IntPtr.Zero, out adapters, out context) != ADL_OK) return;
            _adlContext = context;
            _adlAdapterCount = adapters;
        }

        /// <summary>Current GPU core temperature in degrees Celsius, or -1 when unavailable.</summary>
        public static double ReadGpuTemperatureCelsius()
        {
            EnsureInit();
            double nv = ReadNvapiTemperature();
            if (nv > 0) return nv;
            double adl = ReadAdlTemperature();
            if (adl > 0) return adl;
            return -1;
        }

        /// <summary>Human-readable GPU vendor/status line for diagnostics.</summary>
        public static string GetStatus()
        {
            EnsureInit();
            if (_nvGpus != null && _nvGpus.Length > 0) return "NVAPI (NVIDIA GPU sensors)";
            if (_adlDll != IntPtr.Zero) return "ADL (AMD GPU sensors)";
            return "Unavailable (no vendor SDK; thermal-zone fallback in use)";
        }

        private static double ReadNvapiTemperature()
        {
            if (_nvGpus == null || _nvGpus.Length == 0) return -1;
            try
            {
                IntPtr thermalPtr = GetNvFunction(NVAPI_GPU_GET_THERMAL_SETTINGS_ID);
                if (thermalPtr == IntPtr.Zero) return -1;
                var getThermal = (NvapiGetThermalSettings)Marshal.GetDelegateForFunctionPointer(thermalPtr, typeof(NvapiGetThermalSettings));
                for (int version = 5; version >= 1; version--)
                {
                    NvGpuThermalSettings s = new NvGpuThermalSettings();
                    s.Version = ((uint)version << 16); // NVAPI NV_MAKE_VERSION(major, minor=0): (major<<16)|minor
                    if (getThermal(_nvGpus[0], 0, ref s) == NVAPI_OK && s.Count > 0)
                    {
                        if (s.Sensors0.CurrentTemp > 0 && s.Sensors0.Controller == NVAPI_THERMAL_TARGET_GPU)
                            return s.Sensors0.CurrentTemp;
                        if (s.Sensors1.CurrentTemp > 0 && s.Sensors1.Controller == NVAPI_THERMAL_TARGET_GPU)
                            return s.Sensors1.CurrentTemp;
                        if (s.Sensors0.CurrentTemp > 0) return s.Sensors0.CurrentTemp;
                        if (s.Sensors1.CurrentTemp > 0) return s.Sensors1.CurrentTemp;
                    }
                }
                return -1;
            }
            catch { return -1; }
        }

        private static double ReadAdlTemperature()
        {
            if (_adlDll == IntPtr.Zero || _adlAdapterCount <= 0) return -1;
            try
            {
                IntPtr infoPtr = GetProcAddress(_adlDll, "ADL_Adapter_AdapterInfo_Get");
                IntPtr activePtr = GetProcAddress(_adlDll, "ADL_Adapter_Active_Get");
                IntPtr tempPtr = GetProcAddress(_adlDll, "ADL_Overdrive5_Temperature_Get");
                if (infoPtr == IntPtr.Zero || activePtr == IntPtr.Zero || tempPtr == IntPtr.Zero) return -1;

                var infoGet = (AdlAdapterAdapterInfoGet)Marshal.GetDelegateForFunctionPointer(infoPtr, typeof(AdlAdapterAdapterInfoGet));
                var activeGet = (AdlAdapterActiveGet)Marshal.GetDelegateForFunctionPointer(activePtr, typeof(AdlAdapterActiveGet));
                var tempGet = (AdlOverdrive5TemperatureGet)Marshal.GetDelegateForFunctionPointer(tempPtr, typeof(AdlOverdrive5TemperatureGet));

                IntPtr buf = Marshal.AllocHGlobal(8192);
                try
                {
                    if (infoGet(_adlContext, buf, 8192) != ADL_OK) return -1;
                    // ADLAdapterInfo layout: iSize is at offset 0 and iAdapterIndex
                    // at offset +4. The driver writes the real struct size into
                    // iSize, so it is the correct per-adapter stride (the ADL SDK
                    // struct is ~1836 bytes, not a fixed 1024).
                    int structSize = Marshal.ReadInt32(buf, 0);
                    int stride = structSize > 0 ? structSize : 1836;
                    for (int i = 0; i < _adlAdapterCount; i++)
                    {
                        int off = i * stride + 4;
                        if (off < 0 || off + 4 > 8192) break;
                        int adapterIndex = Marshal.ReadInt32(buf, off);
                        int active;
                        if (activeGet(_adlContext, adapterIndex, out active) != ADL_OK) continue;
                        if (active != 1) continue;
                        AdlTemperature t = new AdlTemperature();
                        t.Size = Marshal.SizeOf(typeof(AdlTemperature));
                        if (tempGet(adapterIndex, 0, ref t) == ADL_OK && t.Temperature > 0)
                        {
                            double c = t.Temperature / 1000.0;
                            if (c > 0 && c < 150) return c;
                        }
                    }
                    return -1;
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            catch { return -1; }
        }

        private static IntPtr GetNvFunction(uint id)
        {
            if (_nvDll == IntPtr.Zero) return IntPtr.Zero;
            IntPtr queryProc = GetProcAddress(_nvDll, "nvapi_QueryInterface");
            if (queryProc == IntPtr.Zero) return IntPtr.Zero;
            var query = (NvapiQueryInterface)Marshal.GetDelegateForFunctionPointer(queryProc, typeof(NvapiQueryInterface));
            return query(id);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
        [DllImport("kernel32.dll")]
        private static extern bool FreeLibrary(IntPtr hModule);
    }
}
