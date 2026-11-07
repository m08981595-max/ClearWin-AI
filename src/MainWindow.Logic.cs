using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace ClearWin
{
    public partial class MainWindow
    {
        private string _cpuDisplayNameCache;
        private void LoadSystemInfo()
        {
            try
            {
                if (_startupPlanGuid == null)
                {
                    try
                    {
                        string cur = GetActivePowerPlanGuid();
                        string bestPerf = SmartAgent.FindBestPerformancePlanGuid();
                        if (!string.IsNullOrEmpty(cur) &&
                            !string.Equals(cur, bestPerf, StringComparison.OrdinalIgnoreCase) &&
                            cur.IndexOf("cd89731b", StringComparison.OrdinalIgnoreCase) < 0 &&
                            cur.IndexOf("d06f5da7", StringComparison.OrdinalIgnoreCase) < 0 &&
                            cur.IndexOf("8c5e7fda", StringComparison.OrdinalIgnoreCase) < 0)
                        {
                            _startupPlanGuid = cur;
                        }
                        else
                        {
                            _startupPlanGuid = SmartAgent.FindBalancedPlanGuid();
                        }
                    }
                    catch (Exception ex) { CWLog.Error("LoadSystemInfo.PowerPlan", ex); }
                }
                // Restore power settings left behind by a prior crash before any
                // automatic optimization path is allowed to run.
                try { _gameModeRecoveryBlocked = !TryRecoverInterruptedGameMode(); }
                catch (Exception exRecovery) { _gameModeRecoveryBlocked = true; CWLog.Error("LoadSystemInfo.Recovery", exRecovery); }
                try
                {
                    MEMORYSTATUSEX mem = new MEMORYSTATUSEX();
                    mem.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                    if (GlobalMemoryStatusEx(ref mem))
                    {
                        _cachedTotalRamGB = mem.ullTotalPhys / 1073741824.0;
                        _refreshHz = 60;
                    }
                } catch (Exception ex2) { CWLog.Error("MainWindow.Logic", ex2); }
                try
                {
                    _cpuLoadPc = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                    _cpuLoadPc.NextValue();
                } catch (Exception ex3) { CWLog.Error("MainWindow.Logic", ex3); }
                // Heavy WMI off the UI thread â€” Task.Run + Dispatcher.BeginInvoke
                System.Threading.Tasks.Task.Run(() =>
                {
                    // OS caption (was blocking UI thread for ~800ms)
                    try
                    {
                        string osCaption = null;
                        using (var osQuery = new ManagementObjectSearcher("SELECT Caption FROM Win32_OperatingSystem"))
                        {
                            foreach (ManagementObject mo in osQuery.Get()) { osCaption = Convert.ToString(mo["Caption"]); break; }
                        }
                        if (!string.IsNullOrEmpty(osCaption))
                        {
                            string capt = osCaption;
                            var d0 = Dispatcher;
                            d0.BeginInvoke(new Action(() =>
                            {
                                try { if (_osInfo != null) _osInfo.Text = "[تم] " + capt; } catch (Exception ex4) { CWLog.Error("LoadSystemInfo.OSUI", ex4); }
                            }));
                        }
                    } catch (Exception ex5) { CWLog.Error("LoadSystemInfo.OS", ex5); }
                    try
                    {
                        using (var gpuQuery = new ManagementObjectSearcher("SELECT Name, AdapterRAM, CurrentRefreshRate FROM Win32_VideoController"))
                        {
                            foreach (ManagementObject mo in gpuQuery.Get())
                            {
                                string gn = Convert.ToString(mo["Name"]);
                                if (!string.IsNullOrEmpty(gn) && !gn.Contains("RDP") && !gn.Contains("Basic"))
                                {
                                    _gpuName = gn;
                                    long vram = mo["AdapterRAM"] != null ? Convert.ToInt64(mo["AdapterRAM"]) : 0;
                                    _gpuVRAM = string.Format("{0:F0} MB", vram / 1048576.0);
                                    object hz = mo["CurrentRefreshRate"];
                                    if (hz != null) _refreshHz = Convert.ToInt32(hz);
                                    var disp2 = Dispatcher;
                                    disp2.BeginInvoke(new Action(() =>
                                    {
                                        try
                                        {
                                            string shortName = _gpuName.Length > 25 ? _gpuName.Substring(0, 23) + ".." : _gpuName;
                                            if (_gpuSub != null) _gpuSub.Text = shortName;
                                            if (_gpuVRAMTxt != null) _gpuVRAMTxt.Text = _gpuVRAM;
                                        } catch (Exception ex6) { CWLog.Error("MainWindow.Logic", ex6); }
                                    }));
                                    break;
                                }
                            }
                        }
                    } catch (Exception ex7) { CWLog.Error("MainWindow.Logic", ex7); }
                });
            } catch (Exception ex8) { CWLog.Error("MainWindow.Logic", ex8); }
        }

        private string GetProcessorDisplayName()
        {
            if (!string.IsNullOrEmpty(_cpuDisplayNameCache)) return _cpuDisplayNameCache;
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                {
                    if (key != null)
                    {
                        var value = key.GetValue("ProcessorNameString") as string;
                        if (!string.IsNullOrEmpty(value))
                        {
                            _cpuDisplayNameCache = value.Trim();
                            return _cpuDisplayNameCache;
                        }
                    }
                }
            }
            catch (Exception ex9) { CWLog.Error("MainWindow.Logic", ex9); }
            _cpuDisplayNameCache = "CPU";
            return _cpuDisplayNameCache;
        }

        // Light smoothing for live readings so single-tick samples don't jump around.
        // Adopts the first valid sample immediately (current==0) and recovers fast
        // from a pinned 100% reading, so the displayed value stays honest and stable.
        private static double Smooth(double current, double sample, double alpha)
        {
            if (current <= 0 || current >= 100) return sample;
            return current * (1.0 - alpha) + sample * alpha;
        }

        private void OnTick(object s, EventArgs e)
        {
            try
            {
                _mainTickCounter++;
                if (_mainTickCounter > 10000) _mainTickCounter = 0;
                System.Diagnostics.Stopwatch _dbgUi = System.Diagnostics.Stopwatch.StartNew();
                try { OnGameWatch(s, e); } catch (Exception ex10) { CWLog.Error("OnTick.GameWatch", ex10); }
                if (_procAutoRefreshActive && _mainTickCounter % 2 == 0) { try { LoadProcesses(); } catch (Exception ex11) { CWLog.Error("OnTick.LoadProcesses", ex11); } }
                // Auto-priority is handled only after OnGameWatch confirms the foreground process is a game.
                // Scheduler check (every 15 ticks â‰ˆ30s, was _schedulerTimer)
                if (_autoScheduler && _agent != null && _agent.Mode != AgentMode.Off && _agent.CurrentState != null && !_agent.CurrentState.IsGame && _mainTickCounter % 15 == 0) { try { MEMORYSTATUSEX memSch = new MEMORYSTATUSEX(); memSch.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)); if (GlobalMemoryStatusEx(ref memSch)) { double totalSch = memSch.ullTotalPhys / 1048576.0; double freeSch = memSch.ullAvailPhys / 1048576.0; double usedPctSch = ((totalSch - freeSch) / totalSch) * 100.0; if (usedPctSch > 85) OptimizeRAM(); } } catch (Exception ex14) { CWLog.Error("MainWindow.Logic", ex14); } }
                // Do not purge browser working sets while a game is active; postpone automatic cleans until desktop use resumes.
                if (_autoRamClean && _agent != null && _agent.Mode != AgentMode.Off && _agent.CurrentState != null && !_agent.CurrentState.IsGame && _mainTickCounter % 300 == 0) { try { OptimizeRAM(); } catch (Exception ex15) { CWLog.Error("OnTick.OptimizeRAM", ex15); } }
                // Auto-pilot (every 15 ticks â‰ˆ30s, reduced from every 6s to avoid thrashing)
                // Auto-pilot state is unified with OnGameWatch (single Game Mode owner with hysteresis).
                if ((!_autoPilot || _agent == null || _agent.Mode == AgentMode.Off) && _autoPilotActive)
                {
                    _autoPilotActive = false;
                    if (_gameModeActive && _gameModeStartedAutomatically)
                    {
                        ExitGameModeInternal();
                    }
                    RestoreGameProcessPriorities();
                    UpdateAutoPilotUI();
                }
                // 1) CPU load via PerformanceCounter
                try
                {
                    if (_cpuLoadPc == null)
                    {
                        try { _cpuLoadPc = new PerformanceCounter("Processor", "% Processor Time", "_Total"); _cpuLoadPc.NextValue(); } catch (Exception ex22) { CWLog.Error("MainWindow.Logic", ex22); }
                    }
                    if (_cpuLoadPc != null)
                    {
                        float v = _cpuLoadPc.NextValue();
                        if (v >= 0)
                        {
                            double weight = Math.Abs(v - _realCpu) > 7 ? 0.85 : 0.70;
                            _realCpu = Smooth(_realCpu, v, weight);
                        }
                    }
                } catch (Exception ex23) { CWLog.Error("MainWindow.Logic", ex23); }
                // 2) RAM via GlobalMemoryStatusEx (native, zero WMI)
                try
                {
                    MEMORYSTATUSEX mem = new MEMORYSTATUSEX();
                    mem.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                    if (GlobalMemoryStatusEx(ref mem))
                    {
                        double totalGB = mem.ullTotalPhys / 1073741824.0;
                        double freeGB = mem.ullAvailPhys / 1073741824.0;
                        double usedGB = totalGB - freeGB;
                        _realRam = (usedGB / totalGB) * 100.0;
                        _cachedTotalRamGB = totalGB;
                        Dispatcher.InvokeAsync(() =>
                        {
                            try
                            {
                                if (_ramSub != null) _ramSub.Text = string.Format("{0:F0}%", _realRam);
                                if (_ramDetail != null) _ramDetail.Text = string.Format("{0:F0} جيجابايت RAM", totalGB);
                                if (_monRamGB != null) _monRamGB.Text = string.Format("{0:F1} / {1:F0} GB", usedGB, totalGB);
                            } catch (Exception ex24) { CWLog.Error("MainWindow.Logic", ex24); }
                        });
                    }
                } catch (Exception ex25) { CWLog.Error("MainWindow.Logic", ex25); }
                // 3) Disk usage (activity + capacity)
                try
                {
                    // Disk activity via PerformanceCounter (matches Task Manager)
                    if (_diskUtilPc == null)
                    {
                        try { _diskUtilPc = new PerformanceCounter("PhysicalDisk", "% Idle Time", "_Total"); _diskUtilPc.NextValue(); } catch { _diskUtilPc = null; }
                    }
                    if (_diskUtilPc != null)
                    {
                        double idleVal = _diskUtilPc.NextValue();
                        if (idleVal >= 0 && idleVal <= 100)
                        {
                            double diskActive = Math.Max(0, 100.0 - idleVal);
                            double dWeight = Math.Abs(diskActive - _realDisk) > 10 ? 0.85 : 0.70;
                            _realDisk = Smooth(_realDisk, diskActive, dWeight);
                        }
                    }
                    // Storage capacity text (used/free space) - Direct check on C: to avoid blocking on network/unready drives
                    try
                    {
                        var drive = new DriveInfo("C");
                        if (drive.IsReady)
                        {
                            double totalGB = drive.TotalSize / 1073741824.0;
                            double freeGB = drive.AvailableFreeSpace / 1073741824.0;
                            if (_diskSub != null) _diskSub.Text = string.Format("{0:F0}%", _realDisk);
                            if (_diskDetail != null) _diskDetail.Text = string.Format("متبقي: {0:F1} جيجابايت", freeGB);
                            if (_storageUsed != null) _storageUsed.Text = string.Format("مستخدم: {0:F1} جيجابايت", totalGB - freeGB);
                            if (_storageFree != null) _storageFree.Text = string.Format("حر: {0:F1} جيجابايت", freeGB);
                        }
                    } catch { }
                } catch (Exception ex26) { CWLog.Error("MainWindow.Logic", ex26); }
                // 4) Battery (only once or every 60 ticks) - WMI in background
                if (!_batteryChecked || _tickCounter % 60 == 0)
                {
                    _batteryChecked = true;
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            using (var batQuery = new ManagementObjectSearcher("SELECT EstimatedChargeRemaining,BatteryStatus FROM Win32_Battery"))
                            {
                                foreach (ManagementObject mo in batQuery.Get())
                                {
                                    double pct = Convert.ToDouble(mo["EstimatedChargeRemaining"]);
                                    int status = Convert.ToInt32(mo["BatteryStatus"]);
                                    Dispatcher.BeginInvoke(new Action(() =>
                                    {
                                        if (_batSub != null) _batSub.Text = string.Format("{0:F0}%", pct);
                                        if (_batDetail != null) _batDetail.Text = (status == 2) ? "موصل بالشاحن" : "يعمل على البطارية";
                                    }));
                                    break;
                                }
                            }
                        }
                        catch
                        {
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                if (_batSub != null) _batSub.Text = "N/A";
                                if (_batDetail != null) _batDetail.Text = "سطح مكتب";
                            }));
                        }
                    });
                }
                // 5) Process/thread count (every 5 ticks ~2.5s, background)
                if (_tickCounter % 5 == 0)
                {
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            var procs = Process.GetProcesses();
                            int pCount = procs.Length;
                            int tc = 0;
                            try { foreach (var p in procs) { try { tc += p.Threads.Count; } catch (Exception ex27) { CWLog.Error("MainWindow.Logic", ex27); } } }
                            finally { foreach (var p in procs) { try { p.Dispose(); } catch (Exception ex28) { CWLog.Error("MainWindow.Logic", ex28); } } }
                            var disp3 = Dispatcher;
                            disp3.BeginInvoke(new Action(() =>
                            {
                                if (_monCpuPct != null) _monCpuPct.Text = string.Format("{0:F0}%", _realCpu);
                                if (_monRamPct != null) _monRamPct.Text = string.Format("{0:F0}%", _realRam);
                                if (_monProcTxt != null) _monProcTxt.Text = string.Format("العمليات: {0}", pCount);
                                if (_monThreadTxt != null) _monThreadTxt.Text = string.Format("الخيوط: {0}", tc);
                            }));
                        } catch (Exception ex29) { CWLog.Error("MainWindow.Logic", ex29); }
                    });
                }
                // 6) Per-core CPU (every 4 ticks only, ~8 sec) - WMI in background
                if (_tickCounter % 4 == 0)
                {
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            using (var coreQuery = new ManagementObjectSearcher("SELECT Name, PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor"))
                            {
                                var coreSb = new StringBuilder();
                                int coreCount = 0;
                                foreach (ManagementObject mo in coreQuery.Get())
                                {
                                    string n = Convert.ToString(mo["Name"]);
                                    if (n != "_Total") { coreSb.AppendFormat(" {0}:{1:F0}%", n, Convert.ToDouble(mo["PercentProcessorTime"])); coreCount++; }
                                }
                                Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    if (_monCpuPerCore != null) _monCpuPerCore.Text = coreSb.ToString();
                                    if (_monCoreTxt != null) _monCoreTxt.Text = string.Format("النوى: {0}", coreCount);
                                }));
                            }
                        } catch (Exception ex30) { CWLog.Error("MainWindow.Logic", ex30); }
                    });
                }
                // 7) Temperature (PerformanceCounter, already native)
                try
                {
                    if ((_realTemp < 1 && _cpuTempPc == null) || (_realTemp < 1 && _cpuTempPc != null && _tickCounter < 20))
                    {
                        if (_cpuTempPc != null) { try { _cpuTempPc.Dispose(); } catch (Exception ex31) { CWLog.Error("MainWindow.Logic", ex31); } _cpuTempPc = null; }
                        var tzCat = new PerformanceCounterCategory("Thermal Zone Information");
                        foreach (string inst in tzCat.GetInstanceNames())
                        {
                            if (inst.ToUpper().Contains("CPU") || inst.ToUpper().Contains("TZ00"))
                            {
                                _cpuTempPc = new PerformanceCounter("Thermal Zone Information", "Temperature", inst);
                                _cpuTempPc.NextValue();
                                break;
                            }
                        }
                        if (_cpuTempPc == null)
                        {
                            try
                            {
                                _cpuTempPc = new PerformanceCounter("Thermal Zone Information", "Temperature", @"\_TZ.CPUZ");
                                _cpuTempPc.NextValue();
                            }
                            catch
                            {
                                try { _cpuTempPc.Dispose(); } catch (Exception ex32) { CWLog.Error("MainWindow.Logic", ex32); }
                                _cpuTempPc = null;
                            }
                        }
                        if (_cpuTempPc == null)
                        {
                            try
                            {
                                _cpuTempPc = new PerformanceCounter("Thermal Zone Information", "Temperature", @"\_TZ.TZ00");
                                _cpuTempPc.NextValue();
                            }
                            catch
                            {
                                try { _cpuTempPc.Dispose(); } catch (Exception ex33) { CWLog.Error("MainWindow.Logic", ex33); }
                                _cpuTempPc = null;
                            }
                        }
                    }
                    if (_cpuTempPc != null)
                    {
                        double r = _cpuTempPc.NextValue();
                        if (r > 0) { double c = r / 10.0; if (c > 200) c -= 273.15; if (c > 0 && c < 150) _realTemp = c; }
                    }
                } catch (Exception ex34) { CWLog.Error("MainWindow.Logic", ex34); }
                if (Math.Abs(_realTemp - _lastTemp) > 0.5) { _lastTemp = _realTemp; }
                // 8) GPU temperature: try GPU Adapter counter (Win10 2004+), fallback ACPI
                try
                {
                    if (_gpuTempPc == null || (_gpuTemp < 1 && _tickCounter < 20))
                    {
                        if (_gpuTempPc != null) { try { _gpuTempPc.Dispose(); } catch (Exception ex35) { CWLog.Error("MainWindow.Logic", ex35); } _gpuTempPc = null; }
                        // First try modern GPU Adapter\Temperature (Win10 2004+)
                        try
                        {
                            var gpuTempCat = new PerformanceCounterCategory("GPU Adapter");
                            foreach (string inst in gpuTempCat.GetInstanceNames())
                            {
                                try
                                {
                                    _gpuTempPc = new PerformanceCounter("GPU Adapter", "Temperature", inst);
                                    _gpuTempPc.NextValue();
                                    break;
                                }
                                catch { _gpuTempPc = null; }
                            }
                        }
                        catch { _gpuTempPc = null; }
                        // Fallback: ACPI thermal zone
                        if (_gpuTempPc == null)
                        {
                            try
                            {
                                var tzCat2 = new PerformanceCounterCategory("Thermal Zone Information");
                                foreach (string inst2 in tzCat2.GetInstanceNames())
                                {
                                    if (inst2.ToUpper().Contains("GFX") || inst2.ToUpper().Contains("GPU"))
                                    {
                                        _gpuTempPc = new PerformanceCounter("Thermal Zone Information", "Temperature", inst2);
                                        try { _gpuTempPc.NextValue(); }
                                        catch { try { _gpuTempPc.Dispose(); } catch (Exception ex36) { CWLog.Error("MainWindow.Logic", ex36); } _gpuTempPc = null; }
                                        break;
                                    }
                                }
                            }
                            catch (Exception ex37) { CWLog.Error("MainWindow.Logic", ex37); }
                            if (_gpuTempPc == null)
                            {
                                try
                                {
                                    _gpuTempPc = new PerformanceCounter("Thermal Zone Information", "Temperature", @"\_TZ.GFXZ");
                                    _gpuTempPc.NextValue();
                                }
                                catch { try { _gpuTempPc.Dispose(); } catch (Exception ex38) { CWLog.Error("MainWindow.Logic", ex38); } _gpuTempPc = null; }
                            }
                            if (_gpuTempPc == null)
                            {
                                try
                                {
                                    _gpuTempPc = new PerformanceCounter("Thermal Zone Information", "Temperature", @"\_TZ.GFX0");
                                    _gpuTempPc.NextValue();
                                }
                                catch { try { _gpuTempPc.Dispose(); } catch (Exception ex39) { CWLog.Error("MainWindow.Logic", ex39); } _gpuTempPc = null; }
                            }
                        }
                    }
                    if (_gpuTempPc != null)
                    {
                        double g = _gpuTempPc.NextValue();
                        if (g > 0) { double gc = g / 10.0; if (gc > 200) gc -= 273.15; if (gc > 0 && gc < 150) { _gpuTempStr = string.Format("{0:F0}\u00B0C", gc); _gpuTemp = gc; } else { _gpuTemp = 0; } }
                    }
                } catch (Exception ex40) { CWLog.Error("MainWindow.Logic", ex40); }
                // 9) GPU utilization (PerformanceCounter - sum all 3D engines)
                try
                {
                    if (!_gpuCounterInit)
                    {
                        _gpuUtilPcs = new List<PerformanceCounter>();
                        try
                        {
                            var gpuCat = new PerformanceCounterCategory("GPU Engine");
                            foreach (string inst in gpuCat.GetInstanceNames())
                            {
                                if (inst.Contains("engtype_3D"))
                                {
                                    try
                                    {
                                        var pc = new PerformanceCounter("GPU Engine", "Utilization Percentage", inst);
                                        pc.NextValue();
                                        _gpuUtilPcs.Add(pc);
                                    }
                                    catch (Exception ex41) { CWLog.Error("MainWindow.Logic", ex41); }
                                }
                            }
                        }
                        catch (Exception ex42) { CWLog.Error("MainWindow.Logic", ex42); }
                        _gpuCounterInit = true;
                        _gpuCounterWarmedUp = false;
                    }
                    if (!_gpuCounterWarmedUp)
                    {
                        List<PerformanceCounter> deadCounters = null;
                        foreach (var pc in _gpuUtilPcs)
                        {
                            try { pc.NextValue(); }
                            catch (Exception)
                            {
                                if (deadCounters == null) deadCounters = new List<PerformanceCounter>();
                                deadCounters.Add(pc);
                            }
                        }
                        if (deadCounters != null)
                        {
                            foreach (var d in deadCounters) { try { _gpuUtilPcs.Remove(d); d.Dispose(); } catch { } }
                        }
                        _gpuCounterWarmedUp = true;
                    }
                    else
                    {
                        // On dual-GPU laptops (iGPU + dGPU) all adapters share the "GPU Engine"
                        // counters, so summing every 3D engine over-reports (shows ~100% while
                        // each GPU is at ~50%). Group engines by adapter LUID and keep the
                        // busiest adapter - matches Task Manager's per-GPU 3D graph.
                        var adapterSums = new Dictionary<string, double>();
                        List<PerformanceCounter> deadCounters = null;
                        foreach (var pc in _gpuUtilPcs)
                        {
                            try
                            {
                                float v = pc.NextValue();
                                if (v <= 0) continue;
                                string key = "default";
                                string inst = pc.InstanceName;
                                if (!string.IsNullOrEmpty(inst))
                                {
                                    int idx = inst.IndexOf("luid_", StringComparison.Ordinal);
                                    if (idx >= 0)
                                    {
                                        int start = idx + 5;
                                        int end = start;
                                        int underscores = 0;
                                        while (end < inst.Length && underscores < 2)
                                        {
                                            if (inst[end] == '_') underscores++;
                                            if (underscores < 2) end++;
                                        }
                                        if (end > start) key = inst.Substring(start, end - start);
                                    }
                                }
                                double cur;
                                adapterSums.TryGetValue(key, out cur);
                                adapterSums[key] = cur + v;
                            }
                            catch (InvalidOperationException)
                            {
                                if (deadCounters == null) deadCounters = new List<PerformanceCounter>();
                                deadCounters.Add(pc);
                            }
                            catch (System.ComponentModel.Win32Exception)
                            {
                                if (deadCounters == null) deadCounters = new List<PerformanceCounter>();
                                deadCounters.Add(pc);
                            }
                            catch (UnauthorizedAccessException)
                            {
                                if (deadCounters == null) deadCounters = new List<PerformanceCounter>();
                                deadCounters.Add(pc);
                            }
                            catch (Exception ex43) { CWLog.Error("MainWindow.Logic", ex43); }
                        }
                        if (deadCounters != null)
                        {
                            foreach (var d in deadCounters)
                            {
                                try { _gpuUtilPcs.Remove(d); d.Dispose(); } catch { }
                            }
                        }
                        double best = 0;
                        foreach (var kv in adapterSums) if (kv.Value > best) best = kv.Value;
                        _realGpu = Smooth(_realGpu, Math.Min(100, best), 0.4);
                    }
                } catch (Exception ex44) { CWLog.Error("MainWindow.Logic", ex44); }
                // Don't fabricate GPU data - use 0 if counter unavailable
                _cpuHistory.Add(_realCpu);
                _ramHistory.Add(_realRam);
                _cpuTempHistory.Add(_realTemp);
                _diskHistory.Add(_realDisk);
                _gpuHistory.Add(_realGpu);
                DrawMonitorGraphs();
                // 10) CPU frequency via registry (fast, no WMI)
                if (_cpuSub != null) _cpuSub.Text = string.Format("{0:F0}%", _realCpu);
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                    {
                        if (key != null)
                        {
                            object mhzObj = key.GetValue("~MHz");
                            if (mhzObj != null)
                            {
                                double freq = Convert.ToDouble(mhzObj) / 1000.0;
                                if (_cpuDetail != null) _cpuDetail.Text = string.Format("{0:F1} GHz", freq);
                                if (_monCpuFreq != null) _monCpuFreq.Text = string.Format("{0:F1} GHz", freq);
                            }
                        }
                    }
                }
                catch (Exception ex45) { CWLog.Error("MainWindow.Logic", ex45); }
                _healthScore = 100
                    - Math.Max(0, (_realCpu - 20) * 0.3)
                    - Math.Max(0, (_realRam - 40) * 0.3)
                    - Math.Max(0, (_realDisk - 70) * 0.2);
                if (_healthScore < 0) _healthScore = 0;
                if (_healthScore > 100) _healthScore = 100;
                if (_statusTxt != null)
                {
                    string agentTag = _agent.Mode != AgentMode.Off ? string.Format(" | AI:{0}", _agent.Mode) : "";
                    TimeSpan up = TimeSpan.FromMilliseconds((uint)Environment.TickCount);
                    string gpuT = _gpuTemp > 1 ? string.Format("{0:F0}C", _gpuTemp) : "--";
                    _statusTxt.Text = string.Format("CPU:{0:F0}% RAM:{1:F0}% GPU:{2:F0}% Disk:{3:F0}% T:{4} GT:{5} Net:{6:F1} | {7:D2}:{8:D2}:{9:D2}", _realCpu, _realRam, _realGpu, _realDisk, _realTemp > 1 ? string.Format("{0:F0}C", _realTemp) : "--", gpuT, _netRecvSpeed * 8 / 1024.0, up.Hours, up.Minutes, up.Seconds);
                }
                if (_hzInfo != null) _hzInfo.Text = string.Format("التردد: {0} Hz", _refreshHz > 0 ? _refreshHz.ToString() : "?");
                // 11) Power plan (every 20 ticks, ~10s) - via powercfg (WMI Win32_PowerPlan is unreliable).
                // Spawned on a background task: powercfg can block for seconds on
                // OEM systems and previously froze the whole UI thread on a 3s wait.
                if (_tickCounter % 20 == 0 && !_powerPlanQueryBusy)
                {
                    _powerPlanQueryBusy = true;
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        string activeScheme = null;
                        try
                        {
                            using (var ps = new Process())
                            {
                                ps.StartInfo.FileName = "powercfg.exe";
                                ps.StartInfo.Arguments = "/getactivescheme";
                                ps.StartInfo.UseShellExecute = false;
                                ps.StartInfo.RedirectStandardOutput = true;
                                ps.StartInfo.CreateNoWindow = true;
                                ps.Start();
                                string outStr = ps.StandardOutput.ReadToEnd();
                                ps.WaitForExit(3000);
                                if (!string.IsNullOrEmpty(outStr))
                                {
                                    int eq = outStr.IndexOf('(');
                                    int close = outStr.IndexOf(')');
                                    if (eq >= 0 && close > eq) activeScheme = outStr.Substring(eq + 1, close - eq - 1);
                                }
                            }
                        }
                        catch (Exception ex46) { CWLog.Error("MainWindow.Logic", ex46); }
                        finally { _powerPlanQueryBusy = false; }
                        string scheme = activeScheme ?? "غير معروفة";
                        try
                        {
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                try { if (_powerPlanInfo != null) _powerPlanInfo.Text = "خطة الطاقة: " + scheme; } catch (Exception ex47) { CWLog.Error("MainWindow.Logic", ex47); }
                            }));
                        }
                        catch (Exception ex48) { CWLog.Error("MainWindow.Logic", ex48); }
                    });
                }
                try
                {
                    TimeSpan up = TimeSpan.FromMilliseconds((uint)Environment.TickCount);
                    if (_osInfo != null) _osInfo.Text = string.Format("النظام: {0} | مدة التشغيل: {1:D2}:{2:D2}:{3:D2}", Environment.OSVersion.VersionString, up.Hours, up.Minutes, up.Seconds);
                } catch (Exception ex49) { CWLog.Error("MainWindow.Logic", ex49); }
                // Update bottom status bar timers
                try
                {
                    _autoSaveCounter++;
                    if (_autoSaveCounter >= 60) _autoSaveCounter = 0;
                    int saveRemaining = 28 - _autoSaveCounter;
                    if (saveRemaining < 0) saveRemaining = 0;
                    if (_autoSaveStatus != null) _autoSaveStatus.Text = string.Format("{0:D2}:{1:D2}", saveRemaining / 60, saveRemaining % 60);
                    _nextTickCounter++;
                    if (_nextTickCounter >= 8) _nextTickCounter = 0;
                    if (_nextTickStatus != null) _nextTickStatus.Text = string.Format("00:{0:D2}", 8 - _nextTickCounter);
                    if (_safeModeStatus != null)
                    {
                        bool isSafe = _agent != null && _agent.SafeModeEnabled;
                        _safeModeStatus.Text = isSafe ? "ON" : "OFF";
                        _safeModeStatus.Foreground = (isSafe ? Theme.WarningBrush : Theme.TextSecBrush);
                    }
                    UpdateModeCard();
                } catch (Exception ex50) { CWLog.Error("MainWindow.Logic", ex50); }
                if (_gpuName.Length > 0)
                {
                    string shortName = _gpuName.Length > 25 ? _gpuName.Substring(0, 23) + ".." : _gpuName;
                    if (_gpuSub != null) _gpuSub.Text = shortName;
                }
                if (_gpuVRAMTxt != null && _gpuVRAM.Length > 0) _gpuVRAMTxt.Text = _gpuVRAM;
                if (_gpuUsageTxt != null) _gpuUsageTxt.Text = string.Format("{0:F0}%", _realGpu);
                if (_gpuDetail != null) _gpuDetail.Text = _gpuTempStr.Length > 0 ? _gpuTempStr : "--";
                _lastGpu = _realGpu;
                try
                {
                    long sent = 0, recv = 0;
                    var netOverview = new StringBuilder();
                    bool overviewTarget = _overviewNetTxt != null;
                    // Single adapter enumeration per tick (previously two separate
                    // GetAllNetworkInterfaces() passes) also feeds the overview.
                    foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if (ni.OperationalStatus == OperationalStatus.Up)
                        {
                            var stat = ni.GetIPv4Statistics();
                            sent += stat.BytesSent; recv += stat.BytesReceived;
                            if (overviewTarget)
                            {
                                var ip = ni.GetIPProperties();
                                netOverview.AppendLine(ni.Name + ": " + (ip.UnicastAddresses.Count > 0 ? ip.UnicastAddresses[0].Address.ToString() : "N/A"));
                            }
                        }
                    }
                    if (_prevNetSent > 0)
                    {
                        double netSecs = _netSampleTime != DateTime.MinValue ? (DateTime.Now - _netSampleTime).TotalSeconds : 0.5;
                        if (netSecs <= 0) netSecs = 0.5;
                        _netSentSpeed = (sent - _prevNetSent) / (netSecs * 1024.0);
                        _netRecvSpeed = (recv - _prevNetRecv) / (netSecs * 1024.0);
                    }
                    _prevNetSent = sent; _prevNetRecv = recv; _netSampleTime = DateTime.Now;
                    if (_netSpeedTxt != null) _netSpeedTxt.Text = string.Format("رفع: {0:F1} KB/s | تنزيل: {1:F1} KB/s", _netSentSpeed, _netRecvSpeed);
                    if (_netSub != null) _netSub.Text = string.Format("{0:F0} Mbps", _netRecvSpeed * 8 / 1024.0);
                    _netHistory.Add(_netRecvSpeed * 8 / 1024.0);
                    if (overviewTarget) _overviewNetTxt.Text = netOverview.Length > 0 ? netOverview.ToString() : "لا توجد شبكة";
                } catch (Exception ex51) { CWLog.Error("MainWindow.Logic", ex51); }
                _tickCounter++;
                if (_tickCounter % 5 == 0) CheckAIProcesses();
                bool frameRenderLive = _frameTracker != null && _frameTracker.IsRenderingLive;
                // FPS is only meaningful while a real game is the foreground app.
                // ClearWin's own WPF render rate (â‰ˆ monitor refresh, ~60) must never
                // be reported as "FPS" on the desktop or in browsers; a fullscreen
                // game's real rate is supplied by the agent's DXGI reader instead.
                bool gameActive = _agent.CurrentState != null && _agent.CurrentState.IsGame;
                double activeGameFps = 0;
                if (gameActive)
                {
                    if (frameRenderLive && _frameTracker != null && _frameTracker.CurrentFps > 10)
                        activeGameFps = _frameTracker.CurrentFps;
                    else
                    {
                        activeGameFps = DxgiFpsReader.ReadDisplayFps();
                        if (activeGameFps <= 0 && _agent.CurrentState != null && _agent.CurrentState.Fps > 0)
                            activeGameFps = _agent.CurrentState.Fps;
                    }
                }
                double fpsVal = gameActive ? (activeGameFps > 0 ? activeGameFps : 30.0) : 0;
                double fps1LowVal = gameActive ? (activeGameFps > 0 ? activeGameFps * 0.75 : 20.0) : 0;
                double fps01LowVal = gameActive ? (activeGameFps > 0 ? activeGameFps * 0.55 : 15.0) : 0;
                double stabVal = gameActive ? (activeGameFps >= 60 ? 100 : Math.Max(20, activeGameFps * 1.5)) : 0;
                double jitterVal = gameActive ? (activeGameFps >= 60 ? 1.5 : 8.0) : 0;
                double frameMsVal = fpsVal > 0 ? (1000.0 / fpsVal) : 0;

                var fpsSnapshot = new { Fps = fpsVal, Fps1Low = fps1LowVal, Fps01Low = fps01LowVal, Stability = stabVal, Jitter = jitterVal, FrameMs = frameMsVal, Cpu = _realCpu, Ram = _realRam, Gpu = _realGpu, Temp = _realTemp, IsGame = gameActive, Reliable = gameActive };
                // One Tick at a time: under load the full decision cycle can exceed the
                // 500 ms tick interval. Overlapping Ticks raced on the RL tables, the
                // decision queue and the shared state - skip instead of piling up.
                if (System.Threading.Interlocked.CompareExchange(ref _agentTickBusy, 1, 0) == 1)
                {
                    // A previous Tick is still running; the next tick will run it.
                }
                else
                {
                    System.Threading.Tasks.Task.Run(() => {
                        try
                        {
                            _agent.SetFrameData(fpsSnapshot.Fps, fpsSnapshot.Fps1Low, fpsSnapshot.Fps01Low, fpsSnapshot.Stability, fpsSnapshot.Jitter, fpsSnapshot.FrameMs, fpsSnapshot.Reliable);
                            _agent.FeedFpsStabilizer(fpsSnapshot.Fps, fpsSnapshot.FrameMs, fpsSnapshot.Fps1Low, fpsSnapshot.Cpu, fpsSnapshot.Ram, fpsSnapshot.Gpu, fpsSnapshot.Temp, fpsSnapshot.IsGame, fpsSnapshot.Reliable);
                            _agent.Tick();
                        } catch (Exception ex52) { CWLog.Error("MainWindow.Logic", ex52); }
                        finally { System.Threading.Interlocked.Exchange(ref _agentTickBusy, 0); }
                    });
                }
                // Update top bar agent status
                if (_topBarAgentStatus != null)
                {
                    string mode = _agent.Mode == AgentMode.Off ? "Off" : _agent.Mode == AgentMode.AutoPilot ? "Auto" : _agent.Mode == AgentMode.GameMode ? "Game" : "Active";
                    _topBarAgentStatus.Text = mode;
                    Color dotClr = _agent.Mode == AgentMode.Off ? Theme.Error : Theme.Green;
                    _topBarAgentStatus.Foreground = (dotClr==Theme.Error ? Theme.ErrorBrush : Theme.GreenBrush);
                    if (_statusDot != null) _statusDot.Foreground = (dotClr==Theme.Error ? Theme.ErrorBrush : Theme.GreenBrush);
                }
                // === REAL-TIME Dashboard Updates (always, no tab gate) ===
                // Section 1: Health Score + Trend (always, cheap)
                try
                {
                    double hs = _agent.SystemHealthScore;
                    _lastScore += (hs - _lastScore) * 0.1;
                    if (_lastScore < 0) _lastScore = 0; if (_lastScore > 100) _lastScore = 100;
                    if (_healthTrendTxt != null)
                    {
                        string trend = _agent.HealthTrend;
                        _healthTrendTxt.Text = trend == "UP" ? "\u2191" : trend == "DOWN" ? "\u2193" : "\u2192";
                        _healthTrendTxt.Foreground = (trend=="UP"?Theme.GreenBrush:trend=="DOWN"?Theme.ErrorBrush:Theme.MutedWhiteBrush);
                    }
                    if (_optHealthBarFill != null)
                    {
                        _optHealthBarFill.Width = hs * 2.4;
                        _optHealthBarFill.Background = (hs > 70 ? Theme.GreenBrush : hs > 40 ? Theme.WarningBrush : Theme.ErrorBrush);
                    }
                    if (_optHealthTxt != null) _optHealthTxt.Text = string.Format("النقاط: {0:F0}/100", hs);
                } catch (Exception ex53) { CWLog.Error("MainWindow.Logic", ex53); }
                // Section 1b: Welcome + Health Brief (every 5 ticks)
                try
                {
                    if (_tickCounter % 5 == 0)
                    {
                        if (_dashWelcomeTxt != null)
                        {
                            int hour = DateTime.Now.Hour;
                            string greeting = hour < 6 ? "!تصبح على خير" : hour < 12 ? "!صباح الخير" : hour < 18 ? "!مساء الخير" : "!مساء الخير";
                            _dashWelcomeTxt.Text = greeting;
                        }
                        if (_dashHealthBriefTxt != null)
                        {
                            double hs2 = _agent.SystemHealthScore;
                            string brief = hs2 >= 80 ? "النظام يعمل بكفاءة عالية — AI يراقب باستمرار" :
                                           hs2 >= 50 ? "الأداء جيد — بعض التحسينات ممكنة" :
                                           "الأداء يحتاج تحسين — ننصح بتشغيل التحسين الكامل";
                            _dashHealthBriefTxt.Text = brief;
                            _dashHealthBriefTxt.Foreground = (hs2 >= 80 ? Theme.GreenBrush : hs2 >= 50 ? Theme.WarningBrush : Theme.ErrorBrush);
                        }
                    }
                } catch (Exception ex54) { CWLog.Error("MainWindow.Logic", ex54); }
                // Section 2: KPI Cards (6 cards, always, cheap text updates)
                try
                {
                    double perfScore = Math.Max(0, 100 - (_realCpu * 0.3 + _realRam * 0.3 + _realDisk * 0.2 + _realGpu * 0.2));
                    if (_kpiVals[0] != null) _kpiVals[0].Text = string.Format("{0:F0}%", perfScore);
                    if (_kpiTrends[0] != null) { _kpiTrends[0].Text = perfScore > 70 ? "Good" : "Needs work"; _kpiTrends[0].Foreground = (perfScore > 70 ? Theme.GreenBrush : Theme.WarningBrush); }
                    double fps = _frameTracker != null ? _frameTracker.CurrentFps : 0;
                    if (_kpiVals[1] != null) _kpiVals[1].Text = _agent.CurrentState.IsGame ? string.Format("{0:F0} FPS", fps) : "Idle";
                    if (_kpiTrends[1] != null) { _kpiTrends[1].Text = _agent.CurrentState.IsGame ? (fps > 55 ? "Smooth" : fps > 30 ? "Fair" : "Low") : "Not gaming"; _kpiTrends[1].Foreground = (fps > 55 ? Theme.GreenBrush : fps > 30 ? Theme.WarningBrush : Theme.TextMutedBrush); }
                    if (_kpiVals[2] != null) _kpiVals[2].Text = string.Format("{0:F0}%", _healthScore);
                    if (_kpiTrends[2] != null) { _kpiTrends[2].Text = _healthScore >= 80 ? "ممتاز" : _healthScore >= 50 ? "جيد" : "يحتاج تحسين"; _kpiTrends[2].Foreground = (_healthScore >= 80 ? Theme.GreenBrush : Theme.WarningBrush); }
                    double battLevel = _agent.CurrentState.BatteryLevel;
                    bool isCharging = _agent.CurrentState.IsCharging;
                    if (_kpiVals[3] != null) _kpiVals[3].Text = battLevel > 0 ? string.Format("{0}%", battLevel) : "N/A";
                    if (_kpiTrends[3] != null) { _kpiTrends[3].Text = isCharging ? "Charging" : (battLevel > 20 ? "OK" : "Low"); _kpiTrends[3].Foreground = isCharging ? Theme.GreenBrush : battLevel > 20 ? Theme.TextMutedBrush : Theme.WarningBrush; }
                    if (_kpiVals[4] != null) _kpiVals[4].Text = _realTemp > 1 ? string.Format("{0:F0}\u00B0C", _realTemp) : "--\u00B0C";
                    if (_kpiTrends[4] != null) { _kpiTrends[4].Text = _realTemp > 80 ? "Hot" : _realTemp > 60 ? "Warm" : _realTemp > 1 ? "Cool" : "--"; _kpiTrends[4].Foreground = _realTemp > 80 ? Theme.ErrorBrush : _realTemp > 60 ? Theme.WarningBrush : Theme.GreenBrush; }
                    double aiScore = _agent.Mode != AgentMode.Off ? _agent.SystemHealthScore : 0;
                    if (_kpiVals[5] != null) _kpiVals[5].Text = _agent.Mode != AgentMode.Off ? string.Format("{0:F0}%", aiScore) : "Off";
                    if (_kpiTrends[5] != null) { string trend5 = _agent.HealthTrend; _kpiTrends[5].Text = _agent.Mode == AgentMode.Off ? "Disabled" : (trend5 == "UP" ? "Improving" : trend5 == "DOWN" ? "Declining" : "Stable"); _kpiTrends[5].Foreground = _agent.Mode == AgentMode.Off ? Theme.TextMutedBrush : (trend5 == "UP" ? Theme.GreenBrush : trend5 == "DOWN" ? Theme.ErrorBrush : Theme.TextSecBrush); }
                } catch (Exception ex55) { CWLog.Error("MainWindow.Logic", ex55); }
                // Section 3: Overview Rings (8 rings, always, cheap)
                try
                {
                    if (_overviewRings[0] != null) { _overviewRings[0].SetProgress(_realCpu, Theme.Blue); if (_overviewLabels[0] != null) _overviewLabels[0].Text = string.Format("{0:F0}%", _realCpu); }
                    if (_overviewRings[1] != null) { _overviewRings[1].SetProgress(_realRam, Theme.Purple); if (_overviewLabels[1] != null) _overviewLabels[1].Text = string.Format("{0:F0}%", _realRam); }
                    if (_overviewRings[2] != null) { _overviewRings[2].SetProgress(_realGpu, Theme.Green); if (_overviewLabels[2] != null) _overviewLabels[2].Text = string.Format("{0:F0}%", _realGpu); }
                    if (_overviewRings[3] != null) { _overviewRings[3].SetProgress(_realDisk, Theme.Warning); if (_overviewLabels[3] != null) _overviewLabels[3].Text = string.Format("{0:F0}%", _realDisk); }
                    double netMbpsOv = _netRecvSpeed * 8 / 1024.0;
                    if (_overviewRings[4] != null) { _overviewRings[4].SetProgress(Math.Min(100, netMbpsOv * 10), Theme.Cyan); if (_overviewLabels[4] != null) _overviewLabels[4].Text = string.Format("{0:F0}", netMbpsOv); }
                    double cpuTempPct = _realTemp > 1 ? Math.Min(100, _realTemp) : 0;
                    if (_overviewRings[5] != null) { _overviewRings[5].SetProgress(cpuTempPct, Theme.Error); if (_overviewLabels[5] != null) _overviewLabels[5].Text = _realTemp > 1 ? string.Format("{0:F0}\u00B0C", _realTemp) : "--"; }
                    double gpuTempPct = _gpuTemp > 1 ? Math.Min(100, _gpuTemp) : 0;
                    if (_overviewRings[6] != null) { _overviewRings[6].SetProgress(gpuTempPct, Colors.OrangeRed); if (_overviewLabels[6] != null) _overviewLabels[6].Text = _gpuTemp > 1 ? string.Format("{0:F0}\u00B0C", _gpuTemp) : "--"; }
                    double battOv = _agent.CurrentState.BatteryLevel;
                    if (_overviewRings[7] != null) { _overviewRings[7].SetProgress(battOv > 0 ? battOv : 100, Theme.Green); if (_overviewLabels[7] != null) _overviewLabels[7].Text = battOv > 0 ? string.Format("{0}%", battOv) : "N/A"; }
                } catch (Exception ex56) { CWLog.Error("MainWindow.Logic", ex56); }
                // Section 4: Live Graphs + Values (always, cheap)
                try
                {
                    DrawLiveGraphs();
                    double netMbpsLive = _netRecvSpeed * 8 / 1024.0;
                    if (_liveVals[0] != null) _liveVals[0].Text = string.Format("{0:F0}%", _realCpu);
                    if (_liveVals[1] != null) _liveVals[1].Text = string.Format("{0:F0}%", _realRam);
                    if (_liveVals[2] != null) _liveVals[2].Text = string.Format("{0:F0}%", _realGpu);
                    if (_liveVals[3] != null) _liveVals[3].Text = string.Format("{0:F1} Mbps", netMbpsLive);
                    if (_liveVals[4] != null) _liveVals[4].Text = string.Format("{0:F0}%", _realDisk);
                    // Dashboard unified metrics (new fields) - show -- until data arrives
                    if (_dashMetricTxts[0] != null) { double v0 = Math.Max(0, Math.Min(100, _realCpu)); if (v0 < 0.5 && _tickCounter < 5) { _dashMetricTxts[0].Text = "--"; } else { _dashMetricTxts[0].Text = string.Format("{0:F0}%", v0); } if (_dashMetricBars[0] != null) _dashMetricBars[0].Width = Math.Max(1, Math.Min(120, v0 * 1.2)); }
                    if (_dashMetricTxts[1] != null) { double v1 = Math.Max(0, Math.Min(100, _realRam)); if (v1 < 0.5 && _tickCounter < 5) { _dashMetricTxts[1].Text = "--"; } else { _dashMetricTxts[1].Text = string.Format("{0:F0}%", v1); } if (_dashMetricBars[1] != null) _dashMetricBars[1].Width = Math.Max(1, Math.Min(120, v1 * 1.2)); }
                    if (_dashMetricTxts[2] != null) { double v2 = Math.Max(0, Math.Min(100, _realGpu)); if (v2 < 0.5 && _tickCounter < 5) { _dashMetricTxts[2].Text = "--"; } else { _dashMetricTxts[2].Text = string.Format("{0:F0}%", v2); } if (_dashMetricBars[2] != null) _dashMetricBars[2].Width = Math.Max(1, Math.Min(120, v2 * 1.2)); }
                    if (_dashMetricTxts[3] != null) { double t = Math.Max(0, _realTemp); if (t < 0.5) { _dashMetricTxts[3].Text = "N/A"; _dashMetricTxts[3].FontSize = 18; } else { _dashMetricTxts[3].FontSize = 26; _dashMetricTxts[3].Text = string.Format("{0:F0}\xB0C", t); } if (_dashMetricBars[3] != null) _dashMetricBars[3].Width = Math.Max(1, Math.Min(120, (t / 100.0) * 120)); }
                    double dashFps = _frameTracker != null && _frameTracker.CurrentFps > 0 ? _frameTracker.CurrentFps : fpsVal;
                    if (_dashMetricTxts[4] != null) { if (dashFps < 1 && _tickCounter < 5) { _dashMetricTxts[4].Text = "--"; } else { _dashMetricTxts[4].Text = string.Format("{0:F0}", dashFps); } if (_dashMetricBars[4] != null) _dashMetricBars[4].Width = Math.Max(1, Math.Min(120, dashFps * 0.8)); }
                    if (_dashMetricTxts[5] != null) { double v5 = Math.Max(0, Math.Min(100, _healthScore)); if (v5 < 0.5 && _tickCounter < 5) { _dashMetricTxts[5].Text = "--"; } else { _dashMetricTxts[5].Text = string.Format("{0:F0}%", v5); } if (_dashMetricBars[5] != null) _dashMetricBars[5].Width = Math.Max(1, Math.Min(120, v5 * 1.2)); }
                    if (!_valuesTestAutoRun && _tickCounter >= 24)
                    {
                        _valuesTestAutoRun = true;
                        RunValuesTest();
                    }
                } catch (Exception ex57) { CWLog.Error("MainWindow.Logic", ex57); }
                // Section 4b: AI FPS Stabilizer (always, cheap)
                try
                {
                    if (_fpsStabVals[0] != null)
                    {
                        double fsCur = _agent.FpsStabilizerCurrentFps;
                        double fsAvg = _agent.FpsStabilizerAvgFps10;
                        double fsLow = _agent.FpsStabilizer1Low;
                        double fsFt = _agent.FpsStabilizerFrameTimeMs;
                        double fsStab = _agent.FpsStabilizerStability;
                        int fsPrev = _agent.FpsStabilizerPreventedDrops;
                        _fpsStabVals[0].Text = fsCur > 0.5 ? string.Format("{0:F0}", fsCur) : "--";
                        _fpsStabVals[1].Text = fsAvg > 0.5 ? string.Format("{0:F0}", fsAvg) : "--";
                        _fpsStabVals[2].Text = fsLow > 0.5 ? string.Format("{0:F0}", fsLow) : "--";
                        _fpsStabVals[3].Text = fsFt > 0 ? string.Format("{0:F1}", fsFt) : "--";
                        _fpsStabVals[4].Text = string.Format("{0:F0}", fsStab);
                        _fpsStabVals[5].Text = fsPrev.ToString();
                        if (_fpsStabStatus != null)
                        {
                            double fsRisk = _agent.FpsStabilizerPredictionRisk;
                            string fsLast = _agent.FpsStabilizerLastAction;
                            if (fsCur > 0.5)
                            {
                                _fpsStabStatus.Text = fsRisk > 0
                                    ? string.Format("مخاطر الهبوط التنبؤية: {0:F0}% | آخر إجراء: {1}", fsRisk * 100, fsLast)
                                    : "لا مخاطر هبوط متوقعة حالياً | آخر إجراء: " + fsLast;
                                _fpsStabStatus.Foreground = (fsRisk >= 0.6 ? Theme.WarningBrush : Theme.TextSecBrush);
                            }
                            else
                            {
                                _fpsStabStatus.Text = "بانتظار بيانات FPS...";
                                _fpsStabStatus.Foreground = Theme.TextMutedBrush;
                            }
                        }
                    }
                } catch (Exception ex58) { CWLog.Error("MainWindow.Logic", ex58); }
                // Section 4c: Premium DashboardView (real data, every tick)
                try
                {
                    if (_dashboardView != null)
                    {
                        // Always REAL live machine values from MainWindow's own counters
                        // (PerformanceCounter + GlobalMemoryStatusEx + real frame tracker).
                        // These update every tick and are never affected by the AI sandbox
                        // simulation mode, so the dashboard stays real-time and truthful.
                        double fpsNow = _frameTracker != null ? _frameTracker.CurrentFps : 0;
                        _dashboardView.UpdateMetrics(_realCpu, _realRam, _realGpu, _realTemp, fpsNow, _healthScore);
                        _dashboardView.UpdateFpsStabilizer(
                            _agent.FpsStabilizerCurrentFps,
                            _agent.FpsStabilizerAvgFps10,
                            _agent.FpsStabilizer1Low,
                            _agent.FpsStabilizerFrameTimeMs,
                            _agent.FpsStabilizerStability,
                            _agent.FpsStabilizerPreventedDrops);
                        _dashboardView.UpdateIntegrity(
                            FormatOrigValue(0, _realCpu),
                            FormatOrigValue(1, _realRam),
                            FormatOrigValue(2, _realGpu),
                            FormatOrigValue(3, _realTemp),
                            FormatOrigValue(4, fpsNow),
                            FormatOrigValue(5, _healthScore));
                        string woaAction = _agent.WinOptAILastAction;
                        double woaConfPct = _agent.WinOptAILastConfidence * 100.0;
                        string aiStatus = _agent.Mode == AgentMode.Off ? "Off"
                            : (_agent.WinOptAI != null && _agent.WinOptAI.IsRunning && _agent.WinOptAI.TotalInferences > 0) ? "Active"
                            : (_agent.WinOptAI != null && _agent.WinOptAI.IsRunning) ? "Active - waiting" : "Off (enable AI Agent)";
                        var ast = _agent.Stats;
                        int decisions = ast != null ? ast.TotalDecisions : 0;
                        double latencyMs = _agent.GetSelfProfileTickMs();
                        double accuracy = _agent.GetTrainingAccuracy() * 100.0;
                        _dashboardView.UpdateAiState(aiStatus, woaAction, woaConfPct, decisions, _agent.GetActiveBoosterCount(), latencyMs / 1000.0, accuracy);
                        string lastAction = null;
                        double lastConf = 0;
                        ParseLastDecision(ref lastAction, ref lastConf);
                        if (lastAction != null)
                        {
                            _dashboardView.UpdateRecommendation(lastAction, TranslateAction(lastAction), lastConf * 100.0);
                            DateTime ts = DateTime.Now;
                            if (ast != null && ast.LastDecisionTime != default(DateTime)) ts = ast.LastDecisionTime;
                            _dashboardView.UpdateActivity(TranslateAction(lastAction), string.Format("الثقة: {0:F0}%", lastConf * 100.0), ts);
                        }
                        string modeStr = _agent.Mode == AgentMode.Off ? "الوضع المتوقف" : _agent.Mode == AgentMode.AutoPilot ? "الطيار الآلي" : _agent.Mode == AgentMode.GameMode ? "وضع الألعاب" : "الوضع الذكي";
                        _dashboardView.UpdateFooter(modeStr, (_agent.WinOptAI != null && _agent.WinOptAI.IsRunning) ? "محرك التحسين نشط" : "بانتظار تفعيل محرك التحسين");
                    }
                } catch (Exception ex59) { CWLog.Error("MainWindow.Logic", ex59); }
                // Section 5: AI Summary + Agent Mode (always, cheap)
                try
                {
                    if (_dashAiStatus != null) { bool aiOn = _agent.Mode != AgentMode.Off; _dashAiStatus.Text = aiOn ? "Active" : "Off"; _dashAiStatus.Foreground = aiOn ? Theme.GreenBrush : Theme.ErrorBrush; }
                    var dLog = _agent.DecisionHistoryLog;
                    if (dLog != null && dLog.Count > 0)
                    {
                        string last = dLog[dLog.Count - 1];
                        int bIdx = last.IndexOf(']');
                        string confStr = "--%";
                        int confIdx = last.IndexOf("Conf:");
                        if (confIdx >= 0)
                        {
                            int pctIdx = last.IndexOf('%', confIdx);
                            if (pctIdx > confIdx)
                            {
                                string raw = last.Substring(confIdx + 5, pctIdx - confIdx - 5).Trim();
                                double cval;
                                if (double.TryParse(raw, out cval)) confStr = string.Format("{0:F0}%", cval);
                            }
                        }
                        if (_dashAiConfidence != null) _dashAiConfidence.Text = "Confidence: " + confStr;
                        if (_dashAiLastAction != null)
                        {
                            string act = bIdx > 0 && bIdx + 2 < last.Length ? last.Substring(bIdx + 2) : last;
                            int pipeIdx = act.IndexOf(" | ");
                            if (pipeIdx > 0 && pipeIdx + 3 < act.Length) act = act.Substring(pipeIdx + 3);
                            int pipe2 = act.IndexOf(" | ");
                            if (pipe2 > 0) act = act.Substring(0, pipe2);
                            if (act.Length > 35) act = act.Substring(0, 33) + "..";
                            _dashAiLastAction.Text = "Last: " + act;
                        }
                    }
                    else
                    {
                        if (_dashAiConfidence != null) _dashAiConfidence.Text = "Confidence: --%";
                        if (_dashAiLastAction != null) _dashAiLastAction.Text = "Last: --";
                    }
                    if (_dashAgentStatus != null)
                    {
                        string modeStr = _agent.Mode == AgentMode.Off ? "Off" : _agent.Mode == AgentMode.AutoPilot ? "AutoPilot" : _agent.Mode == AgentMode.GameMode ? "Game Mode" : "Active";
                        _dashAgentStatus.Text = "Mode: " + modeStr;
                    }
                    var astats = _agent.Stats;
                    if (astats != null && _agentStatVals[0] != null)
                    {
                        _agentStatVals[0].Text = astats.TotalDecisions.ToString();
                        double sr = astats.TotalDecisions > 0 ? (astats.SuccessfulDecisions / (double)astats.TotalDecisions) * 100.0 : 0;
                        if (_agentStatVals[1] != null) _agentStatVals[1].Text = string.Format("{0:F0}%", sr);
                        if (_agentStatVals[2] != null) _agentStatVals[2].Text = astats.FailedDecisions.ToString();
                        if (_agentStatVals[3] != null) _agentStatVals[3].Text = astats.TotalExperiences.ToString();
                    }
                } catch (Exception ex60) { CWLog.Error("MainWindow.Logic", ex60); }
                // Section 5b: Windows Optimization AI Console + Feed (every tick, cheap)
                try
                {
                    string woaAction = _agent.WinOptAILastAction;
                    double woaConf = _agent.WinOptAILastConfidence;
                    if (_woaStatusTxt != null)
                    {
                        var ai = _agent.WinOptAI;
                        bool running = ai != null && ai.IsRunning;
                        bool modeOn = _agent.Mode != AgentMode.Off;
                        string status;
                        Color clr;
                        if (running && ai.TotalInferences > 0) { status = "Active"; clr = Theme.Green; }
                        else if (running && modeOn) { status = "Active - waiting"; clr = Theme.Warning; }
                        else if (ai != null && ai.ManualModelControl)
                        { status = "متوقف يدويًا"; clr = Theme.TextMuted; }
                        else { status = "Off (enable AI Agent)"; clr = Theme.Error; }
                        _woaStatusTxt.Text = status;
                        _woaStatusTxt.Foreground = Theme.BrushFor(clr);
                        if (_woaStatusDot != null) _woaStatusDot.Background = Theme.BrushFor(clr);
                        if (status != _lastWoaStatus)
                        {
                            string prev = _lastWoaStatus;
                            _lastWoaStatus = status;
                            if ((DateTime.Now - _lastModelNotify).TotalSeconds > 10)
                            {
                                _lastModelNotify = DateTime.Now;
                                if ((status == "Active") && prev.Length > 0 && prev != "Active")
                                    ShowNotification("WinOptAI", "نموذج تحسين الويندوز نشط الآن");
                            }
                        }
                        UpdateWoaActivateButton();
                    }
                    if (!string.IsNullOrEmpty(woaAction) && _woaDecisionTxt != null)
                        _woaDecisionTxt.Text = woaAction;
                    if (_woaConfidenceTxt != null)
                        _woaConfidenceTxt.Text = string.Format("{0:F0}%", woaConf * 100);
                    string prompt = _agent.WinOptAILastPrompt;
                    if (!string.IsNullOrEmpty(prompt) && _woaPromptTxt != null)
                        _woaPromptTxt.Text = prompt.Length > 200 ? prompt.Substring(0, 197) + "..." : prompt;
                    string rawResp = _agent.WinOptAILastRawResponse;
                    if (!string.IsNullOrEmpty(rawResp) && _woaResponseTxt != null)
                        _woaResponseTxt.Text = rawResp.Length > 150 ? rawResp.Substring(0, 147) + "..." : rawResp;
                    // Neural network learning metrics (cheap counters, read every tick)
                    // NOTE: these TextBlocks belong to the legacy BuildModernDashboard
                    // fallback UI; when the new DashboardView is active they stay null,
                    // so guard the elements (the array itself is always non-null).
                    if (_mlLearnVals != null && _mlLearnVals[0] != null && _mlLearnVals[1] != null && _mlLearnVals[2] != null && _mlLearnVals[3] != null)
                    {
                        try
                        {
                            int samples = _agent.GetModelTrainingSampleCount();
                            _mlLearnVals[0].Text = samples.ToString();
                            _mlLearnVals[1].Text = _agent.GetModelEnsembleCount().ToString();
                            if (samples == 0)
                            {
                                _mlLearnVals[2].Text = "--";
                                _mlLearnVals[3].Text = "--";
                            }
                            else
                            {
                                _mlLearnVals[2].Text = string.Format("{0:F4}", _agent.GetTrainingLoss());
                                _mlLearnVals[3].Text = string.Format("{0:F1}%", _agent.GetTrainingAccuracy() * 100);
                            }
                        } catch (Exception ex61) { CWLog.Error("MainWindow.ML.Metrics", ex61); }
                    }
                    // Feed: detect new action
                    if (!string.IsNullOrEmpty(woaAction) && woaAction != _lastSeenWoaAction && _woaFeedStack != null)
                    {
                        _lastSeenWoaAction = woaAction;
                        if (_woaFeedStack.Children.Count == 1 && (_woaFeedStack.Children[0] is TextBlock) && ((TextBlock)_woaFeedStack.Children[0]).Text == "لا يوجد نشاط بعد — فعّل الوكيل الذكي للبدء.")
                            _woaFeedStack.Children.Clear();
                        var item = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
                        var dot = new Border { Width = 6, Height = 6, CornerRadius = new CornerRadius(3), Background = Theme.GreenBrush, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
                        item.Children.Add(dot);
                        var info = new StackPanel();
                        info.Children.Add(new TextBlock { Text = woaAction, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Theme.GreenBrush });
                        info.Children.Add(new TextBlock { Text = string.Format("الثقة: {0:F0}% | {1:HH:mm:ss}", woaConf * 100, DateTime.Now), FontSize = 9, Foreground = Theme.TextMutedBrush });
                        item.Children.Add(info);
                        _woaFeedStack.Children.Insert(0, item);
                        while (_woaFeedStack.Children.Count > 20) _woaFeedStack.Children.RemoveAt(_woaFeedStack.Children.Count - 1);
                     }
                     // Live agent last-action card (Row 3.5) - update if built
                     UpdateAgentDashboardCard();
                 } catch (Exception ex62) { CWLog.Error("MainWindow.Logic", ex62); }
                // Section 6: Health Indicators (every 10 ticks to reduce WMI load).
                // The WMI queries (Win32_PnPEntity is one of the slowest classes)
                // now run on a background task; only the final UI update is
                // marshalled back to the dispatcher.
                if (_tickCounter % 10 == 0)
                {
                    double hRam = _realRam;
                    double hDisk = _realDisk;
                    bool hAgentOff = _agent.Mode == AgentMode.Off;
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        string[] healthStatus = new[] { "Good", "Good", "Good", "Good", "Good", "Good" };
                        Color[] healthClr = new[] { Theme.Green, Theme.Green, Theme.Green, Theme.Green, Theme.Green, Theme.Green };
                        // [0] Drivers: check if any device has problem code via WMI
                        try
                        {
                            int problemCount = 0;
                            using (var wmi = new ManagementObjectSearcher("SELECT ConfigManagerErrorCode FROM Win32_PnPEntity WHERE ConfigManagerErrorCode != 0"))
                            {
                                foreach (ManagementObject mo in wmi.Get()) problemCount++;
                            }
                            if (problemCount > 0) { healthStatus[0] = "Warning"; healthClr[0] = Theme.Warning; }
                        } catch (Exception ex63) { CWLog.Error("MainWindow.Logic", ex63); }
                        // [1] RAM: use real memory pressure
                        if (hRam > 95) { healthStatus[1] = "Critical"; healthClr[1] = Theme.Error; }
                        else if (hRam > 85) { healthStatus[1] = "Warning"; healthClr[1] = Theme.Warning; }
                        // [2] Security: check Windows Defender real-time protection via WMI
                        try
                        {
                            using (var wmi = new ManagementObjectSearcher("root\\SecurityCenter2", "SELECT RealTimeProtectionEnabled FROM AntiVirusProduct"))
                            {
                                bool anyProtected = false;
                                foreach (ManagementObject mo in wmi.Get())
                                {
                                    object val = mo["RealTimeProtectionEnabled"];
                                    if (val != null && (bool)val) { anyProtected = true; break; }
                                }
                                if (!anyProtected) { healthStatus[2] = "Warning"; healthClr[2] = Theme.Warning; }
                            }
                        }
                        catch { healthStatus[2] = "Unknown"; healthClr[2] = Theme.TextMuted; }
                        // [3] Startup: count startup registry items
                        try
                        {
                            int startupCount = 0;
                            string[] startupPaths = new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce" };
                            foreach (string sp in startupPaths)
                            {
                                using (var key = Registry.CurrentUser.OpenSubKey(sp, false))
                                {
                                    if (key != null) startupCount += key.ValueCount;
                                }
                            }
                            if (startupCount > 15) { healthStatus[3] = "Warning"; healthClr[3] = Theme.Warning; }
                        } catch (Exception ex64) { CWLog.Error("MainWindow.Logic", ex64); }
                        // [4] Disk: use real disk health
                        if (hDisk > 90) { healthStatus[4] = "Critical"; healthClr[4] = Theme.Error; }
                        else if (hDisk > 80) { healthStatus[4] = "Warning"; healthClr[4] = Theme.Warning; }
                        // [5] Services: check if AI agent is active
                        if (hAgentOff) { healthStatus[5] = "Off"; healthClr[5] = Theme.TextMuted; }
                        try
                        {
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                try
                                {
                                    for (int hi = 0; hi < 6; hi++)
                                    {
                                        if (_healthDots[hi] != null) _healthDots[hi].Background = Theme.BrushFor(healthClr[hi]);
                                        if (_healthLabels[hi] != null) _healthLabels[hi].Foreground = Theme.TextSecBrush;
                                    }
                                    // Update status bar protection indicator
                                    if (_sysProtDot != null && _sysProtTxt != null)
                                    {
                                        bool isProtected = healthStatus[2] == "Good";
                                        Color protClr = isProtected ? Theme.Green : Theme.Warning;
                                        _sysProtDot.Background = Theme.BrushFor(protClr);
                                        _sysProtTxt.Text = isProtected ? "النظام محمي" : "يحتاج مراجعة";
                                        _sysProtTxt.Foreground = Theme.BrushFor(protClr);
                                    }
                                }
                                catch (Exception ex65) { CWLog.Error("MainWindow.Logic", ex65); }
                            }));
                        }
                        catch (Exception ex66) { CWLog.Error("MainWindow.Logic", ex66); }
                    });
                }
                // Section 7: Storage Overview (every 10 ticks ~20s, moderate cost)
                if (_tickCounter % 10 == 0)
                {
                    try
                    {
                        int storIdx = 0;
                        string[] targetDrives = new string[] { "C", "D" };
                        foreach (var dl in targetDrives)
                        {
                            if (storIdx >= 3) break;
                            try
                            {
                                var drive = new DriveInfo(dl);
                                if (drive.IsReady)
                                {
                                    double totalGB = drive.TotalSize / 1073741824.0;
                                    double freeGB = drive.AvailableFreeSpace / 1073741824.0;
                                    double usedPct = ((totalGB - freeGB) / totalGB) * 100.0;
                                    if (_storLabels[storIdx] != null) _storLabels[storIdx].Text = drive.Name.TrimEnd('\\');
                                    if (_storVals[storIdx] != null) _storVals[storIdx].Text = string.Format("{0:F0}% used | {1:F0}/{2:F0} GB", usedPct, totalGB - freeGB, totalGB);
                                    if (_storBars[storIdx] != null) _storBars[storIdx].Width = Math.Max(4, Math.Min(200, 200 * (usedPct / 100.0)));
                                    storIdx++;
                                }
                            } catch { }
                        }
                    } catch (Exception ex67) { CWLog.Error("MainWindow.Logic", ex67); }
                }
                // Section 8: Top Processes (every 5 ticks, background thread, current CPU% via two samples)
                if (_tickCounter % 5 == 0 && !_topProcTaskRunning)
                {
                    _topProcTaskRunning = true;
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            var topProcs = Process.GetProcesses();
                            var procList = new List<KeyValuePair<string, double>>();
                            int nowTick = Environment.TickCount;
                            int elapsedWall = (_procPrevElapsedTick > 0 && nowTick >= _procPrevElapsedTick) ? nowTick - _procPrevElapsedTick : 2500;
                            if (elapsedWall <= 0) elapsedWall = 2500;
                            foreach (var p in topProcs)
                            {
                                try
                                {
                                    TimeSpan tpt = p.TotalProcessorTime;
                                    double cpuPct = 0;
                                    TimeSpan prev;
                                    if (_procPrevCpuTimes.TryGetValue(p.Id, out prev))
                                        cpuPct = Math.Max(0, (tpt - prev).TotalMilliseconds) / elapsedWall * 100.0;
                                    _procPrevCpuTimes[p.Id] = tpt;
                                    if (cpuPct < 0) cpuPct = 0;
                                    if (cpuPct > 100) cpuPct = 100;
                                    string pName = p.ProcessName;
                                    if (pName.Length > 16) pName = pName.Substring(0, 14) + "..";
                                    procList.Add(new KeyValuePair<string, double>(pName, cpuPct));
                                }
                                catch (System.ComponentModel.Win32Exception) { }
                                catch (InvalidOperationException) { }
                                catch (UnauthorizedAccessException) { }
                                catch (Exception ex68) { CWLog.Error("MainWindow.Logic", ex68); }
                                finally { try { p.Dispose(); } catch { } }
                            }
                            if (_procPrevCpuTimes.Count > 4000) _procPrevCpuTimes.Clear();
                            _procPrevElapsedTick = nowTick;
                            procList.Sort(delegate(KeyValuePair<string, double> a, KeyValuePair<string, double> b) { return b.Value.CompareTo(a.Value); });
                            int count = Math.Min(5, procList.Count);
                            var names = new string[5]; var pcts = new double[5];
                            for (int pi = 0; pi < 5; pi++) { names[pi] = "--"; pcts[pi] = 0; }
                            for (int pi = 0; pi < count; pi++) { names[pi] = procList[pi].Key; pcts[pi] = procList[pi].Value; }
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                for (int pi = 0; pi < 5; pi++)
                                {
                                    if (_topProcNames[pi] != null) _topProcNames[pi].Text = names[pi];
                                    if (_topProcPcts[pi] != null) _topProcPcts[pi].Text = pcts[pi] > 0 ? string.Format("{0:F1}%", pcts[pi]) : "";
                                    if (_topProcBars[pi] != null) _topProcBars[pi].Width = Math.Max(2, Math.Min(120, pcts[pi] * 1.2));
                                }
                            }));
                        }
                        catch (Exception ex70) { CWLog.Error("MainWindow.Logic", ex70); }
                        finally { _topProcTaskRunning = false; }
                    });
                }
                // Section 9: AI Timeline + Activity (every 5 ticks ~10s, expensive UI rebuild)
                if (_tickCounter % 5 == 0 && _agentPageActive)
                {
                    try
                    {
                        var decisions = _agent.DecisionHistoryLog;
                        if (decisions != null && decisions.Count > 0 && _timelineStack != null && decisions.Count != _lastTimelineRebuildCount)
                        {
                            _lastTimelineRebuildCount = decisions.Count;
                            _timelineStack.Children.Clear();
                            int start = Math.Max(0, decisions.Count - 10);
                            for (int i = start; i < decisions.Count; i++)
                            {
                                string entry = decisions[i];
                                bool isOk = entry.Contains("] OK |");
                                string decisionTime = "";
                                int bracketEnd = entry.IndexOf(']');
                                if (bracketEnd > 1) decisionTime = entry.Substring(1, bracketEnd - 1);
                                string body = entry;
                                if (bracketEnd > 0 && bracketEnd + 2 < entry.Length) body = entry.Substring(bracketEnd + 2);
                                string[] parts = body.Split(new[] { " | " }, StringSplitOptions.RemoveEmptyEntries);
                                string action = parts.Length > 1 ? parts[1] : "";
                                string reason = parts.Length > 3 ? parts[3] : "";
                                Color iconClr, iconBgClr;
                                string icon;
                                string actionLower = action.ToLower();
                                if (actionLower.Contains("clean") || actionLower.Contains("temp") || actionLower.Contains("unpark"))
                                { icon = "\u2714"; iconClr = Theme.Green; iconBgClr = Color.FromArgb(38, Theme.Green.R, Theme.Green.G, Theme.Green.B); }
                                else if (actionLower.Contains("memory") || actionLower.Contains("ram") || actionLower.Contains("optimize"))
                                { icon = "\uE946"; iconClr = Theme.Accent; iconBgClr = Color.FromArgb(38, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B); }
                                else if (actionLower.Contains("power") || actionLower.Contains("plan") || actionLower.Contains("throttle"))
                                { icon = "\uE713"; iconClr = Theme.Purple; iconBgClr = Color.FromArgb(38, Theme.Purple.R, Theme.Purple.G, Theme.Purple.B); }
                                else if (actionLower.Contains("telemetry") || actionLower.Contains("disable") || actionLower.Contains("track"))
                                { icon = "\uE7BA"; iconClr = Theme.Warning; iconBgClr = Color.FromArgb(38, Theme.Warning.R, Theme.Warning.G, Theme.Warning.B); }
                                else
                                { icon = isOk ? "\u2714" : "\u2716"; iconClr = isOk ? Theme.Green : Theme.Error; iconBgClr = isOk ? Color.FromArgb(38, Theme.Green.R, Theme.Green.G, Theme.Green.B) : Color.FromArgb(38, Theme.Error.R, Theme.Error.G, Theme.Error.B); }
                                var item = new Border { Margin = new Thickness(0, 0, 0, 6), Padding = new Thickness(8, 6, 8, 6), CornerRadius = new CornerRadius(8) };
                                var dock = new DockPanel();
                                var iconBg = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(7), Background = Theme.BrushFor(iconBgClr), Margin = new Thickness(0, 0, 10, 0) };
                                var iconTxt = MakeText(icon, 13, iconClr, false);
                                iconTxt.FontFamily = new FontFamily("Segoe MDL2 Assets");
                                iconTxt.HorizontalAlignment = HorizontalAlignment.Center; iconTxt.VerticalAlignment = VerticalAlignment.Center;
                                iconBg.Child = iconTxt; dock.Children.Add(iconBg);
                                var bodyStack = new StackPanel();
                                string shortAction = TranslateAction(action);
                                if (shortAction.Length > 25) shortAction = shortAction.Substring(0, 23) + "..";
                                bodyStack.Children.Add(MakeText(shortAction, 12.5, Theme.TextPrimary, true));
                                if (reason.Length > 0) { string shortReason = reason.Length > 40 ? reason.Substring(0, 38) + ".." : reason; bodyStack.Children.Add(MakeText(shortReason, 11, Theme.TextSecondary, false)); }
                                dock.Children.Add(bodyStack);
                                var right = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };
                                right.Children.Add(MakeText(decisionTime.Length > 0 ? decisionTime : "--:--", 11, Theme.TextSecondary, false));
                                var statusRow = new StackPanel { Orientation = Orientation.Horizontal };
                                statusRow.Children.Add(MakeText(isOk ? "نجح " : "فشل ", 11, iconClr, true));
                                statusRow.Children.Add(MakeText("\u25CF", 11, iconClr, true));
                                right.Children.Add(statusRow);
                                DockPanel.SetDock(right, Dock.Right); dock.Children.Add(right);
                                item.Child = dock;
                                _timelineStack.Children.Add(item);
                            }
                        }
                        else if (decisions != null && decisions.Count == 0 && _timelineStack != null && _lastTimelineRebuildCount != 0)
                        {
                            _lastTimelineRebuildCount = 0;
                            _timelineStack.Children.Clear();
                            bool agentOff = _agent == null || _agent.Mode == AgentMode.Off;
                            _timelineStack.Children.Add(MakeText(agentOff ? "الوكيل الذكي متوقف — لا توجد قرارات" : "في انتظار القرارات من الوكيل الذكي...", 11, Theme.TextMuted, false));
                        }
                    } catch (Exception ex71) { CWLog.Error("MainWindow.Logic", ex71); }
                }
                // Recent Activity (every 5 ticks) - live on any page, not just agent page
                if (_tickCounter % 5 == 0)
                {
                    try
                    {
                        if (_dashActivityStack != null)
                        {
                            var dLog2 = _agent.DecisionHistoryLog;
                            int actCount = dLog2 != null ? dLog2.Count : 0;
                            if (_lastDashActCount != actCount)
                            {
                                _lastDashActCount = actCount;
                                _dashActivityStack.Children.Clear();
                                if (dLog2 != null && dLog2.Count > 0)
                                {
                                    int start2 = Math.Max(0, dLog2.Count - 8);
                                    for (int ai2 = start2; ai2 < dLog2.Count; ai2++)
                                    {
                                        string entry2 = dLog2[ai2];
                                        bool ok2 = entry2.Contains("] OK |");
                                        string time2 = "";
                                        int bEnd2 = entry2.IndexOf(']');
                                        if (bEnd2 > 1) time2 = entry2.Substring(1, bEnd2 - 1);
                                        string body2 = entry2;
                                        if (bEnd2 > 0 && bEnd2 + 2 < entry2.Length) body2 = entry2.Substring(bEnd2 + 2);
                                        string[] parts2 = body2.Split(new[] { " | " }, StringSplitOptions.RemoveEmptyEntries);
                                        string act2 = parts2.Length > 1 ? TranslateAction(parts2[1]) : body2;
                                        if (act2.Length > 30) act2 = act2.Substring(0, 28) + "..";
                                        Color ic2 = ok2 ? Theme.Green : Theme.Error;
                                        string icGlyph = ok2 ? "\u2714" : "\u2716";
                                        var actItem = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
                                        var actIc = MakeText(icGlyph, 11, ic2, false);
                                        actIc.Margin = new Thickness(0, 0, 8, 0);
                                        actItem.Children.Add(actIc);
                                        var actInfo = new StackPanel();
                                        actInfo.Children.Add(MakeText(act2, 10, Theme.TextPrimary, false));
                                        var actMeta = new StackPanel { Orientation = Orientation.Horizontal };
                                        actMeta.Children.Add(MakeText(ok2 ? "نجح" : "فشل", 8.5, ic2, true));
                                        actMeta.Children.Add(MakeText("  " + time2, 8.5, Theme.TextMuted, false));
                                        actInfo.Children.Add(actMeta);
                                        actItem.Children.Add(actInfo);
                                        _dashActivityStack.Children.Add(actItem);
                                    }
                                }
                                else
                                {
                                    bool agentOff = _agent == null || _agent.Mode == AgentMode.Off;
                                    _dashActivityStack.Children.Add(MakeText(agentOff ? "الوكيل الذكي متوقف — لا توجد قرارات" : "في انتظار القرارات من الوكيل الذكي...", 10, Theme.TextMuted, false));
                                }
                            }
                        }
                    } catch (Exception ex72) { CWLog.Error("MainWindow.Logic", ex72); }
                }
                // Adaptive Tick Rate - adjust timer based on agent mode
                int recommended = _agent.RecommendedTickMs;
                if (_mainTimer != null && (int)_mainTimer.Interval.TotalMilliseconds != recommended)
                    _mainTimer.Interval = TimeSpan.FromMilliseconds(recommended);
                if (_agentPageActive) UpdateAgentPage();
                if (_aiTransparencyPageActive) UpdateAiTransparencyPage();
                if (_gameCenterPageActive) UpdateGameCenterPage();
                if (_aiLogPageActive) UpdateAILogPage();
                if (_learningPageActive) UpdateLearningPage();
                if (_advancedAIPageActive) UpdateAdvancedAIPage();
                if (_enginePageActive) UpdateEnginePage();
                try
                {
                    if (_webUiHost != null)
                    {
                        double liveFps = (_frameTracker != null) ? _frameTracker.CurrentFps : 0;
                        _webUiHost.PushMetrics(_realCpu, _realRam, _realDisk, _realTemp, liveFps, _healthScore, _agent != null ? _agent.Mode.ToString() : "AutoPilot", System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable(), _realGpu, _gpuTemp, _netSentSpeed * 8.0 / 1024.0, _netRecvSpeed * 8.0 / 1024.0, _gpuName, GetProcessorDisplayName(), _cachedTotalRamGB);
                    }
                }
                catch (Exception ex73) { CWLog.Error("OnTick.WebUi", ex73); }
                if (_startupPageActive && _tickCounter % 5 == 0) LoadStartupItems();
                if (_networkPageActive && _tickCounter % 3 == 0) LoadNetworkInfo();
                if (_diskPageActive && _tickCounter % 5 == 0) UpdateDiskPage();
                if (_toolsPageActive && _tickCounter % 5 == 0) UpdateToolsServices();
                if (_healthPageActive) UpdateHealthPage();
                try { if (_tickCounter % 10 == 0) WriteDiagLog(); } catch (Exception ex74) { CWLog.Error("MainWindow.Logic", ex74); }
                if (_mainTickCounter % 5 == 0) CWLog.Info("TickUI", "OnTick elapsed=" + _dbgUi.ElapsedMilliseconds + "ms");
            } catch (Exception ex75) { CWLog.Error("MainWindow.Logic", ex75); try { System.IO.File.AppendAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cw_catch.txt"), "OnTick exception: " + ex75.ToString() + "\n"); } catch (Exception ex76) { CWLog.Error("MainWindow.Logic", ex76); } }
        }

        private void DrawMonitorGraphs()
        {
            if (_monCpuGraph == null || _monRamGraph == null) return;
            bool monVis = _monCpuGraph.IsVisible || _monRamGraph.IsVisible;
            if (!monVis && (_gpuPageGraph == null || !_gpuPageGraph.IsVisible)) return;
            if (monVis)
            {
            _monCpuGraph.Children.Clear();
            _monRamGraph.Children.Clear();
            double cw = _monCpuGraph.ActualWidth > 10 ? _monCpuGraph.ActualWidth : 200;
            double ch = _monCpuGraph.ActualHeight > 10 ? _monCpuGraph.ActualHeight : 50;
            if (_cpuHistory.Count >= 2)
            {
                var pts = new PointCollection();
                for (int i = 0; i < _cpuHistory.Count; i++)
                    pts.Add(new Point((i / (double)(_cpuHistory.Count - 1)) * cw, ch - (_cpuHistory[i] / 100.0) * ch));
                _monCpuGraph.Children.Add(new Polyline { Points = pts, Stroke = Theme.AccentBrush, StrokeThickness = 1.5 });
            }
            if (_ramHistory.Count >= 2)
            {
                var pts = new PointCollection();
                for (int i = 0; i < _ramHistory.Count; i++)
                    pts.Add(new Point((i / (double)(_ramHistory.Count - 1)) * cw, ch - (_ramHistory[i] / 100.0) * ch));
                _monRamGraph.Children.Add(new Polyline { Points = pts, Stroke = Theme.PurpleBrush, StrokeThickness = 1.5 });
            }
            }
            if (_gpuPageGraph != null && _gpuPageGraph.IsVisible && _gpuHistory.Count >= 2)
            {
                _gpuPageGraph.Children.Clear();
                double gw = _gpuPageGraph.ActualWidth > 10 ? _gpuPageGraph.ActualWidth : 200;
                double gh = _gpuPageGraph.ActualHeight > 10 ? _gpuPageGraph.ActualHeight : 50;
                var pts = new PointCollection();
                for (int i = 0; i < _gpuHistory.Count; i++)
                    pts.Add(new Point((i / (double)(_gpuHistory.Count - 1)) * gw, gh - (_gpuHistory[i] / 100.0) * gh));
                _gpuPageGraph.Children.Add(new Polyline { Points = pts, Stroke = Theme.CyanBrush, StrokeThickness = 1.5 });
                if (pts.Count > 0)
                {
                    var last = pts[pts.Count - 1];
                    _gpuPageGraph.Children.Add(new Ellipse { Width = 6, Height = 6, Fill = Theme.CyanBrush, Margin = new Thickness(last.X - 3, last.Y - 3, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top });
                }
            }
        }

        private void DrawMiniSparklines()
        {
            if (_scoreCardSparks == null) return;
            Color[] spColors = new[] { Theme.Accent, Theme.Green, Theme.Purple, Theme.Cyan, Theme.Warning };
            RingBuffer<double>[] histories = new[] { _cpuHistory, _ramHistory, _gpuHistory, _diskHistory, _netHistory };
            for (int si = 0; si < 5; si++)
            {
                if (_scoreCardSparks[si] == null) continue;
                _scoreCardSparks[si].Children.Clear();
                var hist = histories[si];
                if (hist == null || hist.Count < 2) continue;
                double sw = _scoreCardSparks[si].ActualWidth > 10 ? _scoreCardSparks[si].ActualWidth : 80;
                double sh = _scoreCardSparks[si].ActualHeight > 5 ? _scoreCardSparks[si].ActualHeight : 30;
                var pts = new PointCollection();
                for (int pi = 0; pi < hist.Count; pi++)
                {
                    double x = (pi / (double)(hist.Count - 1)) * sw;
                    double y = sh - (Math.Min(100, Math.Max(0, hist[pi])) / 100.0) * sh;
                    pts.Add(new Point(x, y));
                }
                _scoreCardSparks[si].Children.Add(new Polyline { Points = pts, Stroke = Theme.BrushFor(spColors[si]), StrokeThickness = 2, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round });
                if (pts.Count > 0)
                {
                    var last = pts[pts.Count - 1];
                    var dot = new Ellipse { Width = 6, Height = 6, Fill = Theme.BrushFor(spColors[si]), Margin = new Thickness(last.X - 3, last.Y - 3, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
                    _scoreCardSparks[si].Children.Add(dot);
                }
            }
        }

        private void DrawLiveGraphs()
        {
            if (_liveGraphs == null) return;
            Color[] gColors = new[] { Theme.Blue, Theme.Purple, Theme.Green, Theme.Cyan, Theme.Warning };
            RingBuffer<double>[] histArr = new[] { _cpuHistory, _ramHistory, _gpuHistory, _netHistory, _diskHistory };
            for (int gi = 0; gi < 5; gi++)
            {
                if (_liveGraphs[gi] == null) continue;
                var hist = histArr[gi];
                if (hist == null || hist.Count < 2) continue;
                double gw = _liveGraphs[gi].ActualWidth > 10 ? _liveGraphs[gi].ActualWidth : 100;
                double gh = _liveGraphs[gi].ActualHeight > 5 ? _liveGraphs[gi].ActualHeight : 30;
                var pts = new PointCollection();
                int count = Math.Min(hist.Count, 30);
                int offset = Math.Max(0, hist.Count - count);
                for (int pi = 0; pi < count; pi++)
                {
                    double x = (pi / (double)(count - 1)) * gw;
                    double y = gh - (Math.Min(100, Math.Max(0, hist[offset + pi])) / 100.0) * gh;
                    pts.Add(new Point(x, y));
                }
                if (_livePolylines[gi] == null)
                {
                    _livePolylines[gi] = new Polyline { Stroke = Theme.BrushFor(gColors[gi]), StrokeThickness = 1.5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round };
                    _liveDots[gi] = new Ellipse { Width = 5, Height = 5, Fill = Theme.BrushFor(gColors[gi]), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
                    _liveGraphs[gi].Children.Add(_livePolylines[gi]);
                    _liveGraphs[gi].Children.Add(_liveDots[gi]);
                }
                _livePolylines[gi].Points = pts;
                if (pts.Count > 0)
                {
                    var lastPt = pts[pts.Count - 1];
                    _liveDots[gi].Margin = new Thickness(lastPt.X - 2, lastPt.Y - 2, 0, 0);
                }
            }
        }

        private void OnAnimTick(object s, EventArgs e)
        {
            double speed = _animSpeed == 0 ? 0.15 : _animSpeed == 2 ? 0.6 : 0.45;
            _animCpu += (_realCpu - _animCpu) * speed;
            _animRam += (_realRam - _animRam) * speed;
            _animDisk += (_realDisk - _animDisk) * speed;
            _animGpu += (_realGpu - _animGpu) * speed;
            if (_monCpuRing != null && Math.Abs(_animCpu - _animCpuShown) > 0.01) { _monCpuRing.SetProgress(_animCpu, Theme.Accent); _animCpuShown = _animCpu; }
            if (_monRamRing != null && Math.Abs(_animRam - _animRamShown) > 0.01) { _monRamRing.SetProgress(_animRam, Theme.Purple); _animRamShown = _animRam; }
            _animScore += (_healthScore - _animScore) * 0.15;
            if (_cpuSub != null) { string t = string.Format("{0:F0}%", _realCpu); if (t != _animCpuTxt) { _animCpuTxt = t; _cpuSub.Text = t; } }
            if (_ramSub != null) { string t = string.Format("{0:F0}%", _realRam); if (t != _animRamTxt) { _animRamTxt = t; _ramSub.Text = t; } }
            if (_diskSub != null) { string t = string.Format("{0:F0}%", _realDisk); if (t != _animDiskTxt) { _animDiskTxt = t; _diskSub.Text = t; } }
            if (_gpuUsageTxt != null) { string t = string.Format("{0:F0}%", _realGpu); if (t != _animGpuTxt) { _animGpuTxt = t; _gpuUsageTxt.Text = t; } }
            _lastGpu = _realGpu;
            if (_tickCounter % 33 == 0) DrawMiniSparklines();
        }

        private void RunAllOptimizations()
        {
            var confirmResult = MessageBox.Show(
                "هل تريد تشغيل التحسين الكامل؟\n\nسيتم:\n- حذف الملفات المؤقتة\n- تفريغ سلة المهملات\n- ضبط خطة الطاقة الأعلى\n- تعطيل Cortana\n- مسح DNS\n\nهل أنت متأكد؟",
                "ClearWin - تأكيد التحسين",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirmResult != MessageBoxResult.Yes) return;

            // Heavy file deletion + system commands run off the UI thread; only the
            // result message is marshalled back.
            System.Threading.Tasks.Task.Run(() =>
            {
                int cleaned = 0, failed = 0;
                Action<string> cleanDir = (dir) =>
                {
                    if (!Directory.Exists(dir)) return;
                    try
                    {
                        var dirs = new Stack<string>();
                        dirs.Push(dir);
                        while (dirs.Count > 0)
                        {
                            string current = dirs.Pop();
                            try
                            {
                                foreach (string sub in Directory.GetDirectories(current)) { try { dirs.Push(sub); } catch (Exception ex77) { CWLog.Error("MainWindow.Logic", ex77); } }
                                foreach (string f in Directory.GetFiles(current)) { try { File.Delete(f); cleaned++; } catch { failed++; } }
                            }
                            catch { failed++; }
                        }
                    }
                    catch { failed++; }
                };
                cleanDir(Environment.GetEnvironmentVariable("TEMP"));
                cleanDir(Environment.GetEnvironmentVariable("WINDIR") + "\\Prefetch");
                try { SHEmptyRecycleBin(IntPtr.Zero, null, 0); cleaned++; } catch { failed++; }
                try { using (Process.Start(new ProcessStartInfo { FileName = "powercfg.exe", Arguments = "/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", UseShellExecute = true, Verb = "runas" })) { } cleaned++; } catch { failed++; }
                try { using (var key = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", true)) { key.SetValue("AllowCortana", 0, RegistryValueKind.DWord); } cleaned++; } catch { failed++; }
                try { string dnsMsgOpt; if (TryFlushDnsVerified(out dnsMsgOpt)) cleaned++; else failed++; } catch { failed++; }
                string msg = string.Format("نتائج التحسين:\n\n[تم] {0} عنصر تم تنظيفه\n[!] {1} عنصر تم تخطيه (يحتاج صلاحيات)\n\nشغّل كمسؤول للتحسين الكامل", cleaned, failed);
                try
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try { MessageBox.Show(msg, "ClearWin", MessageBoxButton.OK, MessageBoxImage.Information); }
                        catch (Exception ex78) { CWLog.Error("MainWindow.Logic", ex78); }
                    }));
                }
                catch (Exception ex79) { CWLog.Error("MainWindow.Logic", ex79); }
            });
        }

        private void ExportSystemReport()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== تقرير النظام - ClearWin ===");
                sb.AppendLine("التاريخ: " + DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
                sb.AppendLine();
                sb.AppendLine("-- النظام --");
                sb.AppendLine("الإصدار: " + Environment.OSVersion.VersionString);
                sb.AppendLine("64 بت: " + Environment.Is64BitOperatingSystem);
                sb.AppendLine("المعالجات: " + Environment.ProcessorCount);
                sb.AppendLine("مدة التشغيل: " + TimeSpan.FromMilliseconds((uint)Environment.TickCount).ToString(@"d\ أيام\ hh\:mm\:ss"));
                sb.AppendLine();
                sb.AppendLine("-- الذاكرة --");
                try
                {
                    MEMORYSTATUSEX memRpt = new MEMORYSTATUSEX();
                    memRpt.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                    if (GlobalMemoryStatusEx(ref memRpt))
                    {
                        double total = memRpt.ullTotalPhys / 1073741824.0;
                        double free = memRpt.ullAvailPhys / 1073741824.0;
                        sb.AppendLine(string.Format("الإجمالي: {0:F1} GB", total));
                        sb.AppendLine(string.Format("المستخدم: {0:F1} GB", total - free));
                        sb.AppendLine(string.Format("الحر: {0:F1} GB", free));
                    }
                } catch (Exception ex80) { CWLog.Error("MainWindow.Logic", ex80); }
                sb.AppendLine();
                sb.AppendLine("-- القرص C: --");
                try
                {
                    var d = new DriveInfo("C");
                    if (d.IsReady)
                    {
                        double t = d.TotalSize / 1073741824.0;
                        double f = d.AvailableFreeSpace / 1073741824.0;
                        sb.AppendLine(string.Format("الإجمالي: {0:F1} GB", t));
                        sb.AppendLine(string.Format("المستخدم: {0:F1} GB", t - f));
                        sb.AppendLine(string.Format("الحر: {0:F1} GB", f));
                    }
                } catch (Exception ex81) { CWLog.Error("MainWindow.Logic", ex81); }
                sb.AppendLine();
                sb.AppendLine("-- المعالج --");
                try
                {
                    using (var cpuQ = new ManagementObjectSearcher("SELECT Name,NumberOfCores,NumberOfLogicalProcessors FROM Win32_Processor"))
                    {
                        foreach (ManagementObject mo in cpuQ.Get())
                        {
                            sb.AppendLine("الاسم: " + Convert.ToString(mo["Name"]));
                            sb.AppendLine("النوى: " + Convert.ToString(mo["NumberOfCores"]));
                            sb.AppendLine("المعاملات: " + Convert.ToString(mo["NumberOfLogicalProcessors"]));
                            break;
                        }
                    }
                } catch (Exception ex82) { CWLog.Error("MainWindow.Logic", ex82); }
                sb.AppendLine();
                sb.AppendLine("-- كارت الشاشة --");
                try
                {
                    using (var gpuQ = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController"))
                    {
                        foreach (ManagementObject mo in gpuQ.Get())
                        {
                            string g = Convert.ToString(mo["Name"]);
                            if (!g.Contains("RDP") && !g.Contains("Basic"))
                            {
                                sb.AppendLine("الاسم: " + g);
                                long vram = mo["AdapterRAM"] != null ? Convert.ToInt64(mo["AdapterRAM"]) : 0;
                                sb.AppendLine("الذاكرة: " + (vram / 1048576.0).ToString("F0") + " MB");
                            }
                        }
                    }
                } catch (Exception ex83) { CWLog.Error("MainWindow.Logic", ex83); }
                sb.AppendLine();
                sb.AppendLine("-- الشبكة --");
                try
                {
                    foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                    {
                        if (ni.OperationalStatus == OperationalStatus.Up)
                        {
                            var ip = ni.GetIPProperties();
                            sb.AppendLine(ni.Name + ": " + (ip.UnicastAddresses.Count > 0 ? ip.UnicastAddresses[0].Address.ToString() : "N/A"));
                        }
                    }
                } catch (Exception ex84) { CWLog.Error("MainWindow.Logic", ex84); }
                string path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), string.Format("SystemReport_{0:yyyyMMdd_HHmmss}.txt", DateTime.Now));
                System.IO.File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
                using (Process.Start("notepad.exe", path)) { }
                MessageBox.Show("تم حفظ التقرير!", "ClearWin");
            }
            catch (Exception ex) { MessageBox.Show("خطأ: " + ex.Message, "ClearWin"); }
        }

        private void UpdateGameModeStatusUI()
        {
            try
            {
                if (_gmStatusTxt == null) return;
                if (_gameModeActive)
                {
                    _gmStatusTxt.Text = "الحالة: مفعل";
                    _gmStatusTxt.Foreground = Theme.GreenBrush;
                }
                else
                {
                    _gmStatusTxt.Text = "الحالة: غير مفعل";
                    _gmStatusTxt.Foreground = Theme.TextMutedBrush;
                }
            }
            catch (Exception ex85) { CWLog.Error("MainWindow.Logic", ex85); }
        }

        private void ToggleFpsFloor60()
        {
            bool on = !_agent.FpsFloorActive;
            _agent.SetFpsFloor60(on);
            if (_fpsFloorStatusTxt != null)
            {
                _fpsFloorStatusTxt.Text = on
                    ? "الحالة: مفعل (الحد الأدنى 60 - صفر دروب)"
                    : "الحالة: معطل";
                _fpsFloorStatusTxt.Foreground = on ? Theme.GreenBrush : Theme.MutedWhiteBrush;
            }
            ShowToast(on ? "تثبيت 60 فريم مفعل - الحد الأدنى 60 تحت أي حمل" : "تثبيت 60 فريم معطل", on ? Theme.Green : Theme.Warning);
        }

        private void ToggleFullOptimization()
        {
            bool on = !_agent.FullOptimizationMode;
            bool restoreOk = true;
            string restoreMessage = "";
            if (on)
            {
                // Phase A: System Restore safety net (WMI Type 12 / Event 100,
                // PowerShell fallback). A failure is never swallowed: it is logged
                // and shown to the user in the toast below.
                try { restoreOk = CreateClearWinRestorePoint("Full System Optimization", out restoreMessage); }
                catch (Exception exRp) { restoreOk = false; restoreMessage = exRp.Message; CWLog.Error("MainWindow.Logic", exRp); }
                if (!restoreOk) CWLog.Error("MainWindow.Logic", new Exception("Full optimization enabled without a restore point: " + restoreMessage));
            }
            _agent.FullOptimizationMode = on;
            if (_fullOptStatusTxt != null)
            {
                _fullOptStatusTxt.Text = on
                    ? "الحالة: مفعل (تنظيم دوري كل 90 ثانية)"
                    : "الحالة: معطل";
                _fullOptStatusTxt.Foreground = on ? Theme.GreenBrush : Theme.MutedWhiteBrush;
            }
            if (on) _agent.ApplyFullSystemOptimization();
            if (on && !restoreOk)
                ShowToast("وضع الكفاءة الكامل مفعل بدون نقطة استعادة: " + restoreMessage, Theme.Warning);
            else
                ShowToast(on ? "وضع الكفاءة والاستقرار الكامل مفعل" : "وضع التنظيم الكامل معطل", on ? Theme.Green : Theme.Warning);
        }

        // === Auto stability modes: 60 FPS floor + full optimization TOGETHER ===
        // Fired automatically when a game is detected (OnGameWatch background
        // task). Runs off the UI thread; only touches Dispatcher for UI refresh.
        private void AutoEnableStabilityModes()
        {
            bool floorChanged = false, optChanged = false, restorePointFailed = false;
            try
            {
                if (!_agent.FpsFloorActive)
                {
                    _agent.SetFpsFloor60(true);
                    floorChanged = _agent.FpsFloorActive;
                    if (floorChanged) _gameModeAutoEnabledFpsFloor = true;
                }
            }
            catch (Exception ex86) { CWLog.Error("MainWindow.Logic", ex86); }

            try
            {
                if (!_agent.FullOptimizationMode)
                {
                    string restoreMessage;
                    bool restoreOk = CreateClearWinRestorePoint("AutoPilot game optimization", out restoreMessage);
                    if (restoreOk)
                    {
                        _agent.FullOptimizationMode = true;
                        optChanged = true;
                        _gameModeAutoEnabledFullOptimization = true;
                        _agent.ApplyFullSystemOptimization();
                    }
                    else
                    {
                        restorePointFailed = true;
                        CWLog.Warn("MainWindow.Logic", "AutoPilot skipped full optimization because a restore point could not be created: " + restoreMessage);
                    }
                }
            }
            catch (Exception ex87) { CWLog.Error("MainWindow.Logic", ex87); }

            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        UpdateFloorAndFullOptStatusUI();
                        if (floorChanged) ShowToast("تم تفعيل تثبيت الإطارات تلقائيًا", Theme.Green);
                        if (optChanged) ShowToast("تم تفعيل التحسين الكامل تلقائيًا", Theme.Green);
                        if (restorePointFailed) ShowToast("تخطّينا التحسين الكامل لعدم إنشاء نقطة استعادة", Theme.Warning);
                    }
                    catch (Exception ex89) { CWLog.Error("MainWindow.Logic", ex89); }
                }), System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (Exception ex90) { CWLog.Error("MainWindow.Logic", ex90); }
        }

        private void UpdateFloorAndFullOptStatusUI()
        {
            if (_fpsFloorStatusTxt != null)
            {
                bool on = _agent.FpsFloorActive;
                _fpsFloorStatusTxt.Text = on
                    ? "الحالة: مفعل (الحد الأدنى 60 - صفر دروب)"
                    : "الحالة: معطل";
                _fpsFloorStatusTxt.Foreground = on ? Theme.GreenBrush : Theme.MutedWhiteBrush;
            }
            if (_fullOptStatusTxt != null)
            {
                bool on = _agent.FullOptimizationMode;
                _fullOptStatusTxt.Text = on
                    ? "الحالة: مفعل (تنظيم دوري كل 90 ثانية)"
                    : "الحالة: معطل";
                _fullOptStatusTxt.Foreground = on ? Theme.GreenBrush : Theme.MutedWhiteBrush;
            }
        }

        private void ActivateGameMode()
        {
            try
            {
                if (!CaptureGameModeBaseline())
                {
                    ShowToast("تعذر حفظ حالة النظام السابقة؛ لم يتم تفعيل وضع الألعاب", Theme.Warning);
                    return;
                }
                _gameModeStartedAutomatically = false;
                _gameModeActive = true;
                try { using (Process.Start(new ProcessStartInfo { FileName = "powercfg.exe", Arguments = "/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", UseShellExecute = true, Verb = "runas" })) { } } catch (Exception ex91) { CWLog.Error("MainWindow.Logic", ex91); }
                // Game Mode does not change CPU core-parking settings. Keep
                // explicit core unparking available only via its dedicated action.
                try { uint cur; int hr = NtSetTimerResolution(5000, true, out cur); _timerResActive = (hr == 0); } catch (Exception ex93) { CWLog.Error("MainWindow.Logic", ex93); }
                // Deliberately do not stop Windows services or blanket-change process priorities.
                // The optional AutoPriority path only boosts a verified foreground game and records its prior value.
                UpdateGameModeStatusUI();
                MessageBox.Show("وضع الألعاب مفعل.\n\nتم تطبيق إعدادات الطاقة والاستقرار المتاحة.\nلم يتم إيقاف خدمات Windows.", "ClearWin");
            } catch (Exception ex96) { CWLog.Error("MainWindow.Logic", ex96); }
        }

        [DllImport("psapi.dll")]
        static extern bool EmptyWorkingSet(IntPtr hProcess);
        [DllImport("shell32.dll")]
        static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, int dwFlags);
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out WinRect lpRect);
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int count);
        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll", ExactSpelling = true)]
        static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);
        const uint GA_ROOT = 2;

        [DllImport("user32.dll")]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("ntdll.dll")]
        static extern uint NtSetSystemInformation(int SystemInformationClass, IntPtr SystemInformation, int SystemInformationLength);
        [DllImport("ntdll.dll")]
        static extern int RtlAdjustPrivilege(int privilege, bool bEnablePrivilege, bool isThreadPrivilege, out bool previousValue);
        const int SystemMemoryListInformation = 80;
        [DllImport("kernel32.dll", EntryPoint = "SetProcessWorkingSetSize", SetLastError = true)]
        static extern int SetProcessWorkingSetSize(IntPtr process, IntPtr min, IntPtr max);

        [DllImport("kernel32.dll")]
        static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);
        const int SW_RESTORE = 9;
        const uint NIM_ADD = 0x00000000;
        const uint NIM_MODIFY = 0x00000001;
        const uint NIM_DELETE = 0x00000002;
        const uint NIF_MESSAGE = 0x00000001;
        const uint NIF_ICON = 0x00000002;
        const uint NIF_TIP = 0x00000004;
        const uint NIF_INFO = 0x00000010;
        const uint WM_LBUTTONDBLCLK = 0x0203;
        const uint WM_LBUTTONUP = 0x0202;
        const uint WM_RBUTTONUP = 0x0205;
        const uint WM_LBUTTONDOWN = 0x0201;
        const uint WM_MOUSEMOVE = 0x0200;
        const int NOTIFYICON_VERSION_4 = 4;
        const uint NIN_SELECT = 0x400;
        const uint NIN_KEYSELECT = 0x403;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        struct NOTIFYICONDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uID;
            public uint uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public uint dwState;
            public uint dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public uint uTimeoutOrVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public uint dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        const uint MOD_NONE = 0x0000;
        const uint MOD_CONTROL = 0x0002;
        const uint MOD_SHIFT = 0x0004;

        struct WinRect { public int Left, Top, Right, Bottom; }

        private bool IsGameProcess(IntPtr hwnd)
        {
            try
            {
                uint pid;
                GetWindowThreadProcessId(hwnd, out pid);
                if (pid <= 0) return false;
                string procName = "";
                string procPath = "";
                try
                {
                    using (var p = Process.GetProcessById((int)pid))
                    {
                        try { procName = p.ProcessName.ToLowerInvariant(); } catch { }
                        try { var mm = p.MainModule; procPath = mm != null ? mm.FileName.ToLowerInvariant() : ""; } catch { }
                    }
                }
                catch { return false; }
                if (string.IsNullOrEmpty(procName)) return false;
                if (procName.Contains("clearwin")) return false;

                // Exclude system/browser/non-game processes
                string[] nonGames = new[] { "explorer", "chrome", "msedge", "firefox", "opera", "brave", "devenv",
                    "code", "outlook", "winword", "excel", "powerpnt", "teams", "slack", "discord",
                    "spotify", "wmplayer", "vlc", "mspaint", "notepad", "cmd", "powershell", "windows",
                    "taskmgr", "perfmon", "resmon", "mmc", "regedit", "control", "runtimebroker",
                    "searchapp", "shellexperiencehost", "systemsettings", "lockapp", "widgets",
                    "textinputhost", "windowsinternal", "sihost", "taskhost", "dllhost", "conhost",
                    "svchost", "services", "lsass", "csrss", "winlogon", "smss", "spoolsv",
                    "securityhealth", "audiodg", "fontdrvhost", "ctfmon", "sppsrv", "wlms",
                    "whatsapp", "telegram", "signal", "messenger", "wechat", "line", "zoom", "skype", "viber" };
                foreach (string ng in nonGames)
                {
                    if (procName.Contains(ng) || procPath.Contains(ng)) return false;
                }
                // Must have a window title or check root window title
                var sb = new StringBuilder(256);
                GetWindowText(hwnd, sb, 256);
                string title = sb.ToString().Trim();
                if (string.IsNullOrEmpty(title) || title.Length < 2)
                {
                    IntPtr root = GetAncestor(hwnd, GA_ROOT);
                    if (root != IntPtr.Zero && root != hwnd)
                    {
                        GetWindowText(root, sb, 256);
                        title = sb.ToString().Trim();
                    }
                }
                if (string.IsNullOrEmpty(title) || title.Length < 2)
                {
                    if (procName.Contains("aow_exe") || procName.Contains("androws") || procName.Contains("gameloop") || procName.Contains("androidprocess") || procName.Contains("pubg") || procName.Contains("tslgame"))
                        return true;
                    return false;
                }
                string lowerTitle = title.ToLowerInvariant();
                if (lowerTitle.Contains("pubg") || lowerTitle.Contains("playerunknown") || lowerTitle.Contains("battleground") || lowerTitle.Contains("bgmi"))
                    return true;
                // Quick check: known game indicators in process name
                string[] gameIndicators = new[] { "game", "launcher", "unity", "unreal", "ue4", "ue5", "cryengine", "vulkan", "opengl", "genshin", "valorant", "fortnite", "league", "wow", "csgo", "dota", "minecraft", "rockstar", "cod", "apex", "pubg", "tslgame", "elden", "cyberpunk", "witcher", "fifa", "gta", "reddead", "battlefield", "destiny", "warframe", "overwatch", "diablo", "halo", "forza", "simulator", "rpg", "gameloop", "aow_exe", "aow", "androws", "androidemulator", "androidprocess", "ldplayer", "dnplayer", "bluestacks", "hd-player", "nox", "mumu", "mumunx", "smartgaga", "qmplayerservice", "txservice", "syzs_cbox", "execpubg", "pubgmobile", "bgmi" };
                foreach (string gi in gameIndicators) { if (procName.Contains(gi)) return true; }
                // Score-based detection for non-gaming path processes
                int score = 0;
                // Known game store paths
                if (procPath.Contains("windowsapps") || procPath.Contains("steam") || procPath.Contains("epic") || procPath.Contains("ubisoft") || procPath.Contains("gog") || procPath.Contains("battle.net") || procPath.Contains("origin") || procPath.Contains("riot") || procPath.Contains("xbox") || procPath.Contains("ea games") || procPath.Contains("bethesda") || procPath.Contains("blizzard") || procPath.Contains("epicgames") || procPath.Contains("xboxgames") || procPath.Contains("txgameassistant") || procPath.Contains("gameloop") || procPath.Contains("tencent"))
                    score += 60;
                else if (procPath.Contains("games") || procPath.Contains("game") || procPath.Contains("program files"))
                    score += 40;
                // Window title length (games often have descriptive titles)
                if (title.Length > 3 && title.Length < 50) score += 10;
                if (title.Length > 10) score += 5;
                // GPU usage hint (use agent state if available)
                if (_agent != null && _agent.CurrentState != null && _agent.CurrentState.GpuUsage > 20) score += 15;
                else if (_agent != null && _agent.CurrentState != null && _agent.CurrentState.GpuUsage > 10) score += 8;
                // Process name is short (games typically have short exe names like "game.exe")
                if (procName.Length < 15 && !procName.Contains(" ")) score += 5;
                return score >= 40;
            }
            catch { return false; }
        }

        private bool IsKnownGameOrEmulator(IntPtr hwnd)
        {
            try
            {
                uint pid;
                GetWindowThreadProcessId(hwnd, out pid);
                if (pid <= 0) return false;
                string name = "";
                try { using (var p = Process.GetProcessById((int)pid)) { name = p.ProcessName.ToLowerInvariant(); } } catch { }
                if (string.IsNullOrEmpty(name)) return false;
                if (name.Contains("clearwin")) return false;
                string[] emus = new[] { "aow_exe", "androws", "gameloop", "appmarket", "androidemulator", "androidprocess", "txgameassistant", "mumu", "mumunx", "tslgame", "pubg", "ldplayer", "dnplayer", "hd-player", "bluestacks", "nox", "smartgaga", "qmplayerservice", "txservice", "syzs_cbox", "execpubg", "pubgmobile", "bgmi" };
                foreach (var emu in emus) { if (name.Contains(emu)) return true; }

                // Check window text or root ancestor window text
                var sb = new StringBuilder(256);
                GetWindowText(hwnd, sb, 256);
                string title = sb.ToString().ToLowerInvariant();
                if (string.IsNullOrEmpty(title))
                {
                    IntPtr root = GetAncestor(hwnd, GA_ROOT);
                    if (root != IntPtr.Zero && root != hwnd)
                    {
                        GetWindowText(root, sb, 256);
                        title = sb.ToString().ToLowerInvariant();
                    }
                }
                if (title.Contains("pubg") || title.Contains("playerunknown") || title.Contains("battleground") || title.Contains("bgmi") || title.Contains("gameloop"))
                    return true;

                return false;
            }
            catch { return false; }
        }

        private int _gameExitGraceTicks = 0;
        private int _trackedGamePid = 0;
        internal const int GAME_EXIT_HYSTERESIS_TICKS = 10; // ~5 seconds grace period on Alt+Tab when game is still running

        private static bool IsTrackedGameProcessStillAlive(int pid)
        {
            if (pid <= 0) return false;
            try
            {
                using (var p = Process.GetProcessById(pid))
                {
                    return !p.HasExited;
                }
            }
            catch
            {
                return false;
            }
        }

        private void OnGameWatch(object s, EventArgs e)
        {
            try
            {
                IntPtr fg = GetForegroundWindow();
                bool isGame = false;
                if (fg != IntPtr.Zero)
                {
                    WinRect r;
                    if (GetWindowRect(fg, out r))
                    {
                        int sw = (int)SystemParameters.PrimaryScreenWidth;
                        int sh = (int)SystemParameters.PrimaryScreenHeight;
                        int w = r.Right - r.Left;
                        int h = r.Bottom - r.Top;
                        bool isFullscreen = w >= sw - 20 && h >= sh - 20;
                        isGame = (IsKnownGameOrEmulator(fg) || (isFullscreen && IsGameProcess(fg)));
                        var sbFg = new StringBuilder(256);
                        GetWindowText(fg, sbFg, 256);
                        if (sbFg.ToString().Contains("ClearWin")) isGame = false;
                    }
                }

                bool autoGameModeAllowed = _autoPilot && _agent != null && _agent.Mode != AgentMode.Off;
                if (isGame)
                {
                    _gameExitGraceTicks = 0;
                    // If AutoPilot is disabled while a game remains open, revert only
                    // the modes enabled automatically by ClearWin.
                    if (_gameModeActive && _gameModeStartedAutomatically && !autoGameModeAllowed)
                    {
                        _autoPilotActive = false;
                        ExitGameModeInternal();
                        UpdateAutoPilotUI();
                    }
                    if (!_gameModeActive && autoGameModeAllowed)
                    {
                        if (!CaptureGameModeBaseline())
                        {
                            CWLog.Warn("MainWindow.Logic", "AutoPilot did not enable Game Mode because the recovery baseline could not be saved.");
                            UpdateGameModeStatusUI();
                            return;
                        }
                        _gameModeStartedAutomatically = true;
                        _gameModeActive = true;
                        _autoPilotActive = true;

                        uint pidG = 0;
                        GetWindowThreadProcessId(fg, out pidG);
                        _trackedGamePid = (int)pidG;

                        try
                        {
                            System.Threading.Tasks.Task.Factory.StartNew(() =>
                            {
                                try { AutoEnableStabilityModes(); } catch (Exception ex97) { CWLog.Error("MainWindow.Logic", ex97); }
                                try { ActivateGameModeSilent(); } catch (Exception ex98) { CWLog.Error("MainWindow.Logic", ex98); }
                                try
                                {
                                    string gp = "";
                                    string gpath = "";
                                    if (pidG > 0)
                                    {
                                        try { using (var p = Process.GetProcessById((int)pidG)) { gp = p.ProcessName; var mm = p.MainModule; if (mm != null) gpath = mm.FileName; } } catch { }
                                    }
                                    // Single unified power plan + GPU preference switch (no duplicate powercfg calls)
                                    SmartAgent.EnforceGameDedicatedGpuAndPower(gp, gpath, switchPowerPlan: true);
                                }
                                catch (Exception exGpu) { CWLog.Error("MainWindow.Logic.GpuPower", exGpu); }
                            });
                        }
                        catch (Exception ex99) { CWLog.Error("MainWindow.Logic", ex99); }
                        UpdateGameModeStatusUI();
                        UpdateAutoPilotUI();
                    }
                    if (_overlayEnabled && _overlayWindow == null) ShowOverlay();
                    if (_autoPriority || _autoPilot)
                    {
                        try
                        {
                            uint pid;
                            GetWindowThreadProcessId(fg, out pid);
                            if (pid > 0)
                            {
                                _trackedGamePid = (int)pid;
                                SetGameProcessHighPriority((int)pid);
                            }
                        }
                        catch (Exception ex100) { CWLog.Error("MainWindow.Logic", ex100); }
                    }
                    else RestoreGameProcessPriorities();
                }
                else
                {
                    if (_overlayWindow != null) HideOverlay();
                    if (_gameModeActive)
                    {
                        // Hysteresis: if the game process is still alive (e.g. user pressed Alt+Tab),
                        // wait GAME_EXIT_HYSTERESIS_TICKS (~5 seconds) before reverting power plan and priorities.
                        bool gameStillRunning = IsTrackedGameProcessStillAlive(_trackedGamePid);
                        _gameExitGraceTicks++;
                        if (!gameStillRunning || _gameExitGraceTicks >= GAME_EXIT_HYSTERESIS_TICKS)
                        {
                            _gameExitGraceTicks = 0;
                            _trackedGamePid = 0;
                            _autoPilotActive = false;
                            ExitGameModeInternal();
                            RestoreGameProcessPriorities();
                            UpdateAutoPilotUI();
                        }
                    }
                    else
                    {
                        _gameExitGraceTicks = 0;
                        RestoreGameProcessPriorities();
                    }
                }
            } catch (Exception ex102) { CWLog.Error("MainWindow.Logic", ex102); }
        }

        private void ActivateGameModeSilent()
        {
            try
            {
                if (!CaptureGameModeBaseline())
                {
                    CWLog.Warn("MainWindow.Logic", "Silent Game Mode was cancelled because the recovery baseline was not saved.");
                    return;
                }
                // Keep timer work off the UI thread. Power plan switching is unified in
                // SmartAgent.ActivateHighestPerformancePowerPlan() without UAC Verb="runas" prompts.
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        try { SmartAgent.ActivateHighestPerformancePowerPlan(); } catch (Exception ex103) { CWLog.Error("MainWindow.Logic", ex103); }
                        try { uint cur; int hr = NtSetTimerResolution(5000, true, out cur); _timerResActive = (hr == 0); } catch (Exception ex105) { CWLog.Error("MainWindow.Logic", ex105); }
                    }
                    catch (Exception ex108) { CWLog.Error("MainWindow.Logic", ex108); }
                });
            }
            catch (Exception ex109) { CWLog.Error("MainWindow.Logic", ex109); }
        }

        private string GetGameModeJournalPath()
        {
            return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClearWin", "game-mode-recovery.ini");
        }

        // Persist the original plan and core-parking indexes BEFORE changing them.
        private bool CaptureGameModeBaseline()
        {
            try
            {
                if (_gameModeRecoveryBlocked)
                {
                    CWLog.Warn("MainWindow.Logic", "Game Mode is blocked because a prior recovery journal has not been restored.");
                    return false;
                }
                string existingJournal = GetGameModeJournalPath();
                if (File.Exists(existingJournal))
                {
                    if (!string.IsNullOrEmpty(_gameModePrevPlanGuid))
                        return true; // Existing journal belongs to this active session; preserve its original baseline.
                    _gameModeRecoveryBlocked = true;
                    CWLog.Warn("MainWindow.Logic", "Existing Game Mode recovery journal found; refusing to overwrite it.");
                    return false;
                }
                if (_gameModePrevPlanGuid == null)
                {
                    string curPlan = GetActivePowerPlanGuid();
                    string bestPerf = SmartAgent.FindBestPerformancePlanGuid();
                    if (!string.IsNullOrEmpty(curPlan) &&
                        !string.Equals(curPlan, bestPerf, StringComparison.OrdinalIgnoreCase) &&
                        curPlan.IndexOf("cd89731b", StringComparison.OrdinalIgnoreCase) < 0 &&
                        curPlan.IndexOf("d06f5da7", StringComparison.OrdinalIgnoreCase) < 0 &&
                        curPlan.IndexOf("8c5e7fda", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        _gameModePrevPlanGuid = curPlan;
                    }
                    else
                    {
                        _gameModePrevPlanGuid = SmartAgent.FindBalancedPlanGuid();
                    }
                }
                if (_gameModePrevParkMin == null) _gameModePrevParkMin = QueryPowerSettingIndex("0cc5b647-c1df-4637-891a-dec35c318583");
                if (_gameModePrevParkMax == null) _gameModePrevParkMax = QueryPowerSettingIndex("ea06c951-1d8c-4a23-8619-d15a3c0e5f20");
                _gameModePrevTimerResActive = _timerResActive;
                // The power-plan baseline is mandatory. Core-parking values are optional
                // because some Windows devices/power plans do not expose these indexes.
                if (string.IsNullOrWhiteSpace(_gameModePrevPlanGuid))
                {
                    CWLog.Warn("MainWindow.Logic", "Game Mode baseline is missing the active power plan; no system changes were made.");
                    return false;
                }
                if (string.IsNullOrWhiteSpace(_gameModePrevParkMin) || string.IsNullOrWhiteSpace(_gameModePrevParkMax))
                {
                    _gameModePrevParkMin = null;
                    _gameModePrevParkMax = null;
                }
                string path = GetGameModeJournalPath();
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                string tempPath = path + ".tmp";
                File.WriteAllLines(tempPath, new string[] {
                    "plan=" + _gameModePrevPlanGuid,
                    "parkMin=" + (_gameModePrevParkMin ?? ""),
                    "parkMax=" + (_gameModePrevParkMax ?? ""),
                    "timerWasActive=" + (_gameModePrevTimerResActive ? "1" : "0")
                });
                if (File.Exists(path)) { File.Delete(tempPath); return false; }
                File.Move(tempPath, path);
                return File.Exists(path);
            }
            catch (Exception ex109)
            {
                CWLog.Error("MainWindow.Logic.CaptureGameModeBaseline", ex109);
                return false;
            }
        }

        private bool TryRecoverInterruptedGameMode()
        {
            string path = GetGameModeJournalPath();
            if (!File.Exists(path)) return true;
            try
            {
                string plan = null, parkMin = null, parkMax = null;
                foreach (string line in File.ReadAllLines(path))
                {
                    int split = line.IndexOf('=');
                    if (split <= 0) continue;
                    string key = line.Substring(0, split).Trim();
                    string value = line.Substring(split + 1).Trim();
                    if (key == "plan") plan = value;
                    else if (key == "parkMin") parkMin = value;
                    else if (key == "parkMax") parkMax = value;
                }
                bool hasAnyParking = !string.IsNullOrWhiteSpace(parkMin) || !string.IsNullOrWhiteSpace(parkMax);
                bool hasValidParking =
                    System.Text.RegularExpressions.Regex.IsMatch(parkMin ?? "", @"^0x[0-9a-fA-F]+\|0x[0-9a-fA-F]+$") &&
                    System.Text.RegularExpressions.Regex.IsMatch(parkMax ?? "", @"^0x[0-9a-fA-F]+\|0x[0-9a-fA-F]+$");
                if (string.IsNullOrWhiteSpace(plan) ||
                    !System.Text.RegularExpressions.Regex.IsMatch(plan, @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$") ||
                    (hasAnyParking && !hasValidParking))
                {
                    CWLog.Warn("MainWindow.Logic.Recovery", "Invalid game-mode recovery journal; left untouched for inspection.");
                    return false;
                }
                _gameModePrevPlanGuid = plan;
                bool restored = RunPowerCfgAndWait("/setactive " + plan, 10000);
                _gameModePrevParkMin = hasValidParking ? parkMin : null;
                _gameModePrevParkMax = hasValidParking ? parkMax : null;
                if (hasValidParking && !RestoreCoreParking()) restored = false;
                try { SmartAgent.RestoreAllBackedUpRegistrySettings(); } catch (Exception exRegRec) { CWLog.Error("MainWindow.Logic.Recovery.Registry", exRegRec); }
                if (restored)
                {
                    File.Delete(path);
                    _gameModePrevPlanGuid = null;
                    _gameModePrevParkMin = null;
                    _gameModePrevParkMax = null;
                    _gameModeRecoveryBlocked = false;
                    CWLog.Warn("MainWindow.Logic.Recovery", "Recovered interrupted Game Mode power settings from journal.");
                }
                else CWLog.Warn("MainWindow.Logic.Recovery", "Game Mode recovery was incomplete; journal kept for the next startup.");
                return restored;
            }
            catch (Exception ex)
            {
                CWLog.Error("MainWindow.Logic.Recovery", ex);
                return false;
            }
        }

        private bool RunPowerCfgAndWait(string arguments, int timeoutMs)
        {
            try
            {
                var psi = new ProcessStartInfo("powercfg.exe", arguments) { CreateNoWindow = true, UseShellExecute = false };
                using (Process process = Process.Start(psi))
                {
                    if (process == null) return false;
                    if (!process.WaitForExit(timeoutMs)) { try { process.Kill(); } catch { } return false; }
                    return process.ExitCode == 0;
                }
            }
            catch (Exception ex) { CWLog.Error("MainWindow.Logic.PowerCfg", ex); return false; }
        }

        private void ExitGameModeInternal()
        {
            if (!_gameModeActive) return;
            _gameModeActive = false;
            // Revert (powercfg plan restore + service restarts) off the UI thread
            // for the same freeze-prevention as activation.
            try { System.Threading.Tasks.Task.Factory.StartNew(() => { try { RevertGameMode(); } catch (Exception ex110) { CWLog.Error("MainWindow.Logic", ex110); } }); }
            catch (Exception ex111) { CWLog.Error("MainWindow.Logic", ex111); }
            // Auto: 60 FPS floor + full optimization turn OFF together when the
            // gaming session ends (they were auto-enabled as a pair on game start).
            try { if (_gameModeAutoEnabledFpsFloor && _agent != null && _agent.FpsFloorActive) _agent.SetFpsFloor60(false); } catch (Exception ex112) { CWLog.Error("MainWindow.Logic", ex112); }
            try { if (_gameModeAutoEnabledFullOptimization && _agent != null && _agent.FullOptimizationMode) _agent.FullOptimizationMode = false; } catch (Exception ex113) { CWLog.Error("MainWindow.Logic", ex113); }
            _gameModeAutoEnabledFpsFloor = false;
            _gameModeAutoEnabledFullOptimization = false;
            _gameModeStartedAutomatically = false;
            try { UpdateFloorAndFullOptStatusUI(); } catch (Exception ex114) { CWLog.Error("MainWindow.Logic", ex114); }
            UpdateGameModeStatusUI();
        }

        private void RevertGameMode()
        {
            lock (_gameModeRecoveryLock)
            {
            bool hasJournal = false;
            string journalPath = GetGameModeJournalPath();
            try { hasJournal = File.Exists(journalPath); } catch { }
            bool hasPlan = !string.IsNullOrEmpty(_gameModePrevPlanGuid);
            bool hasParking = !string.IsNullOrEmpty(_gameModePrevParkMin) || !string.IsNullOrEmpty(_gameModePrevParkMax);
            bool hasServices;
            lock (_gameModeServicesLock) { hasServices = _gameModeServicesToRestart != null && _gameModeServicesToRestart.Count > 0; }
            if (!hasPlan && !hasParking && !hasServices && !hasJournal) return;
            // A malformed journal must never be silently deleted by ordinary shutdown.
            if (hasJournal && !hasPlan && !hasParking)
            {
                CWLog.Warn("MainWindow.Logic.Recovery", "Recovery journal exists but no validated baseline is loaded; keeping it for inspection.");
                return;
            }
            bool restored = true;
            try
            {
                if (!string.IsNullOrEmpty(_gameModePrevPlanGuid) || hasJournal)
                {
                    if (!SmartAgent.RestoreBalancedOrPreviousPowerPlan(_gameModePrevPlanGuid)) restored = false;
                    else _gameModePrevPlanGuid = null;
                }
                if (hasParking && !RestoreCoreParking()) restored = false;
                if (hasPlan || hasParking)
                {
                    try
                    {
                        uint cur;
                        // Preserve an existing timer-resolution request; otherwise release the 0.5ms request we added.
                        if (_gameModePrevTimerResActive) _timerResActive = true;
                        else { NtSetTimerResolution(5000, false, out cur); _timerResActive = false; }
                    } catch (Exception ex116) { restored = false; CWLog.Error("MainWindow.Logic", ex116); }
                    _gameModePrevTimerResActive = false;
                }
                List<string> toRestart = null;
                lock (_gameModeServicesLock)
                {
                    toRestart = _gameModeServicesToRestart;
                    _gameModeServicesToRestart = null;
                }
                if (toRestart != null)
                {
                    foreach (string s in toRestart)
                    {
                        try { using (Process.Start(new ProcessStartInfo { FileName = "sc.exe", Arguments = "start " + s, UseShellExecute = false, CreateNoWindow = true })) { } }
                        catch (Exception ex117) { restored = false; CWLog.Error("MainWindow.Logic", ex117); }
                    }
                }
                try { if (!SmartAgent.RestoreAllBackedUpRegistrySettings()) restored = false; } catch (Exception exRegRev) { restored = false; CWLog.Error("MainWindow.Logic.Revert.Registry", exRegRev); }
                string journal = GetGameModeJournalPath();
                if (restored && File.Exists(journal))
                {
                    File.Delete(journal);
                    _gameModeRecoveryBlocked = false;
                }
                else if (!restored) CWLog.Warn("MainWindow.Logic.Recovery", "Game Mode restore incomplete; recovery journal retained.");
            }
            catch (Exception ex118) { CWLog.Error("MainWindow.Logic", ex118); }
            }
        }

        private string RunPowerCfgGet(string args)
        {
            try
            {
                var psi = new ProcessStartInfo("powercfg.exe", args) { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true };
                using (var p = Process.Start(psi))
                {
                    string outp = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(5000);
                    return outp;
                }
            }
            catch { return ""; }
        }

        private string RunScQuery(string name)
        {
            try
            {
                var psi = new ProcessStartInfo("sc.exe", "query " + name) { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true };
                using (var p = Process.Start(psi))
                {
                    string outp = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(5000);
                    return outp;
                }
            }
            catch { return ""; }
        }

        private string GetActivePowerPlanGuid()
        {
            string outp = RunPowerCfgGet("/getactivescheme");
            if (string.IsNullOrEmpty(outp)) return null;
            var m = System.Text.RegularExpressions.Regex.Match(outp, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
            return m.Success ? m.Value : null;
        }

        private string QueryPowerSettingIndex(string settingGuid)
        {
            string outp = RunPowerCfgGet("/query");
            if (string.IsNullOrEmpty(outp)) return null;
            string[] lines = outp.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string ac = "", dc = "";
            string marker = "Power Setting GUID: " + settingGuid;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].IndexOf(marker, StringComparison.OrdinalIgnoreCase) < 0) continue;
                for (int j = i; j < lines.Length && j < i + 8; j++)
                {
                    if (ac == "")
                    {
                        var mAC = System.Text.RegularExpressions.Regex.Match(lines[j], @"Current AC Power Setting Index:\s*(0x[0-9a-fA-F]+)");
                        if (mAC.Success) ac = mAC.Groups[1].Value;
                    }
                    if (dc == "")
                    {
                        var mDC = System.Text.RegularExpressions.Regex.Match(lines[j], @"Current DC Power Setting Index:\s*(0x[0-9a-fA-F]+)");
                        if (mDC.Success) dc = mDC.Groups[1].Value;
                    }
                }
                break;
            }
            if (ac == "" && dc == "") return null;
            return ac + "|" + dc;
        }

        private bool RestoreCoreParking()
        {
            bool minOk = ApplyParkValue("0cc5b647-c1df-4637-891a-dec35c318583", _gameModePrevParkMin);
            bool maxOk = ApplyParkValue("ea06c951-1d8c-4a23-8619-d15a3c0e5f20", _gameModePrevParkMax);
            bool queryOk = RunPowerCfgAndWait("/requeryindex scheme_current", 8000);
            if (minOk && maxOk && queryOk)
            {
                _gameModePrevParkMin = null;
                _gameModePrevParkMax = null;
                return true;
            }
            return false;
        }

        private bool ApplyParkValue(string settingGuid, string acdc)
        {
            if (string.IsNullOrEmpty(acdc)) return false;
            string[] parts = acdc.Split('|');
            string ac = parts.Length > 0 ? parts[0] : "";
            string dc = parts.Length > 1 ? parts[1] : "";
            int acInt = 0, dcInt = 0;
            bool acOk = ac.Length > 2 && int.TryParse(ac.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out acInt);
            bool dcOk = dc.Length > 2 && int.TryParse(dc.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out dcInt);
            if (!acOk || !dcOk) return false;
            bool acSet = RunPowerCfgAndWait("/setacvalueindex scheme_current sub_processor " + settingGuid + " " + acInt, 8000);
            bool dcSet = RunPowerCfgAndWait("/setdcvalueindex scheme_current sub_processor " + settingGuid + " " + dcInt, 8000);
            return acSet && dcSet;
        }

        private bool IsServiceRunning(string name)
        {
            string outp = RunScQuery(name);
            return !string.IsNullOrEmpty(outp) && outp.Contains("RUNNING");
        }

        private void OptimizeRAM()
        {
            try
            {
                string[] safeNames = new[] { "chrome", "msedge", "firefox", "discord", "spotify", "teams", "slack", "zoom", "skype" };
                var procs = Process.GetProcesses();
                try
                {
                    foreach (var p in procs)
                    {
                        try
                        {
                            if (p.HasExited) continue;
                            string name = p.ProcessName.ToLower();
                            bool safe = false;
                            for (int i = 0; i < safeNames.Length; i++)
                            {
                                if (name.Contains(safeNames[i])) { safe = true; break; }
                            }
                            if (safe) EmptyWorkingSet(p.Handle);
                        } catch (Exception ex122) { CWLog.Error("MainWindow.Logic", ex122); }
                    }
                }
                finally { foreach (var p in procs) { try { p.Dispose(); } catch (Exception ex123) { CWLog.Error("MainWindow.Logic", ex123); } } }
            } catch (Exception ex124) { CWLog.Error("MainWindow.Logic", ex124); }
        }

        private void CleanShaderCache()
        {
            // Directory.GetFiles(AllDirectories) + delete on a shader cache can
            // take seconds; run off the UI thread to avoid freezing the window.
            System.Threading.Tasks.Task.Run(() =>
            {
                int cleaned = 0, failed = 0;
                Action<string> cleanDir = (dir) =>
                {
                    if (!Directory.Exists(dir)) return;
                    try
                    {
                        foreach (string f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                        { try { File.Delete(f); cleaned++; } catch { failed++; } }
                    }
                    catch { failed++; }
                };
                string[] paths = new[] {
                    Environment.GetEnvironmentVariable("LOCALAPPDATA") + "\\AMD\\GpuCache",
                    Environment.GetEnvironmentVariable("LOCALAPPDATA") + "\\AMD\\ShaderCache",
                    Environment.GetEnvironmentVariable("LOCALAPPDATA") + "\\NVIDIA\\GpuCache",
                    Environment.GetEnvironmentVariable("LOCALAPPDATA") + "\\NVIDIA\\ShaderCache",
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Temp\\NVIDIA Corporation\\NV_Cache",
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\D3DSCache",
                    Environment.GetEnvironmentVariable("WINDIR") + "\\ServiceProfiles\\LocalService\\AppData\\Local\\Temp\\NV_Cache",
                    Environment.GetEnvironmentVariable("WINDIR") + "\\Temp\\NVIDIA Corporation\\NV_Cache"
                };
                foreach (var p in paths) cleanDir(p);
                string msg = string.Format("تنظيف كاش الكارت:\n\n[تم] {0} ملف\n[!] {1} فشل", cleaned, failed);
                try
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try { MessageBox.Show(msg, "ClearWin"); }
                        catch (Exception ex125) { CWLog.Error("MainWindow.Logic", ex125); }
                    }));
                }
                catch (Exception ex126) { CWLog.Error("MainWindow.Logic", ex126); }
            });
        }

        private bool PurgeStandbyList()
        {
            // Purging the standby list while a game is actively rendering can
            // evict useful game data and cause avoidable disk-backed stutter.
            try { if (_agent != null && _agent.CurrentState != null && _agent.CurrentState.IsGame) return false; } catch (Exception ex127) { CWLog.Error("MainWindow.Logic", ex127); }
            IntPtr p = IntPtr.Zero;
            try
            {
                bool prev;
                RtlAdjustPrivilege(19, true, false, out prev); // 19 = SeIncreaseQuotaPrivilege

                // First flush modified list (cmd=3) so modified dirty pages transition to standby
                try
                {
                    IntPtr pFlush = Marshal.AllocHGlobal(4);
                    Marshal.WriteInt32(pFlush, 3);
                    NtSetSystemInformation(SystemMemoryListInformation, pFlush, 4);
                    Marshal.FreeHGlobal(pFlush);
                }
                catch { }

                int cmd = 4;
                p = Marshal.AllocHGlobal(4);
                Marshal.WriteInt32(p, cmd);
                uint r = NtSetSystemInformation(SystemMemoryListInformation, p, 4);
                return r == 0;
            }
            catch { return false; }
            finally { if (p != IntPtr.Zero) Marshal.FreeHGlobal(p); }
        }

        private void WriteDiagLog()
        {
            try
            {
                _diagBuf.Append(string.Format("[{8:HH:mm:ss}] T{0} CPU={1:F1}% RAM={2:F1}% GPU={3:F1}% Disk={4:F1}% CTemp={5:F1}C GTemp={6:F1}C Net={7:F3}Mbps\n", _tickCounter, _realCpu, _realRam, _realGpu, _realDisk, _realTemp, _gpuTemp, _netRecvSpeed * 8 / 1024.0, DateTime.Now));
                _diagCounter++;
                if (_diagCounter >= DIAG_FLUSH_EVERY || _diagBuf.Length >= 65536)
                {
                    FlushDiagLog();
                }
            } catch (Exception ex128) { CWLog.Error("MainWindow.Logic", ex128); }
        }

        private void FlushDiagLog()
        {
            try
            {
                if (_diagBuf.Length == 0) return;
                string diagPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "clearwin_diag.txt");
                var fi = new System.IO.FileInfo(diagPath);
                if (fi.Exists && fi.Length > 512 * 1024) { try { fi.Delete(); } catch (Exception ex129) { CWLog.Error("MainWindow.Logic", ex129); } }
                System.IO.File.AppendAllText(diagPath, _diagBuf.ToString());
                _diagBuf.Clear();
                _diagCounter = 0;
            } catch (Exception ex130) { CWLog.Error("MainWindow.Logic", ex130); }
        }

        private void MinimizeMemory()
        {
            // Trim the working set only; forced full GC.Collect caused a blocking
            // pause on the UI thread at every minimize and bought nothing (the
            // pages are re-faulted on restore anyway).
            try { using (Process cp = Process.GetCurrentProcess()) SetProcessWorkingSetSize(cp.Handle, new IntPtr(-1), new IntPtr(-1)); } catch (Exception ex131) { CWLog.Error("MainWindow.Logic", ex131); }
        }

        [DllImport("ntdll.dll", EntryPoint = "NtSetTimerResolution")]
        public static extern int NtSetTimerResolution(uint DesiredResolution, bool SetResolution, out uint CurrentResolution);

        private void ToggleTimerResolution()
        {
            try
            {
                _timerResActive = !_timerResActive;
                uint current;
                if (_timerResActive)
                {
                    int hr = NtSetTimerResolution(5000, true, out current);
                    if (hr == 0)
                        MessageBox.Show(string.Format("✓ تم خفض زمن استجابة النظام بنجاح!\nالزمن الحالي: {0:F1}ms (أسرع استجابة استجابة لحركة الماوس وأزرار اللعب).", (double)current / 10000.0), "معزز الألعاب");
                    else
                    {
                        _timerResActive = false;
                        MessageBox.Show("فشل خفض زمن استجابة النظام.", "معزز الألعاب");
                    }
                }
                else
                {
                    NtSetTimerResolution(5000, false, out current);
                    MessageBox.Show("تمت استعادة طلب ClearWin لزمن المؤقّت.", "معزز الألعاب");
                }
            }
            catch (Exception ex) { MessageBox.Show("خطأ: " + ex.Message, "معزز الألعاب"); }
        }

        private void SetGameProcessHighPriority(int processId)
        {
            if (_shutdownPending) return;
            try
            {
                using (Process process = Process.GetProcessById(processId))
                {
                    lock (_gamePriorityLock)
                    {
                        if (!_gamePriorityBaseline.ContainsKey(processId))
                            _gamePriorityBaseline[processId] = process.PriorityClass;
                    }
                    if (process.PriorityClass != ProcessPriorityClass.High)
                        process.PriorityClass = ProcessPriorityClass.High;
                }
            }
            catch (System.ComponentModel.Win32Exception) { /* Access denied on protected game process, ignore safely */ }
            catch (InvalidOperationException) { /* Process already exited */ }
            catch (Exception ex) { CWLog.Error("MainWindow.GamePriority", ex); }
        }

        private void RestoreGameProcessPriorities()
        {
            KeyValuePair<int, ProcessPriorityClass>[] saved;
            lock (_gamePriorityLock)
            {
                saved = new KeyValuePair<int, ProcessPriorityClass>[_gamePriorityBaseline.Count];
                int i = 0;
                foreach (KeyValuePair<int, ProcessPriorityClass> pair in _gamePriorityBaseline)
                    saved[i++] = pair;
                _gamePriorityBaseline.Clear();
            }
            foreach (KeyValuePair<int, ProcessPriorityClass> pair in saved)
            {
                try
                {
                    using (Process process = Process.GetProcessById(pair.Key))
                    {
                        // Restore only if the process still has the priority ClearWin assigned.
                        // Preserve any different priority another application/user has set.
                        if (process.PriorityClass == ProcessPriorityClass.High)
                            process.PriorityClass = pair.Value;
                    }
                }
                catch (ArgumentException) { /* Process has exited. */ }
                catch (System.ComponentModel.Win32Exception) { /* Access denied, ignore */ }
                catch (InvalidOperationException) { /* Process exited */ }
                catch (Exception ex) { CWLog.Error("MainWindow.GamePriorityRestore", ex); }
            }
        }

        private void UnparkCPUCores(bool silent = false)
        {
            try
            {
                // 0cc5b647-c1df-4637-891a-dec35c318583 = CPMINCORES (Minimum % unparked cores -> 100 for full unparking)
                using (Process.Start(new ProcessStartInfo("powercfg.exe", "/setacvalueindex scheme_current sub_processor 0cc5b647-c1df-4637-891a-dec35c318583 100") { CreateNoWindow = true, UseShellExecute = false })) { }
                using (Process.Start(new ProcessStartInfo("powercfg.exe", "/setdcvalueindex scheme_current sub_processor 0cc5b647-c1df-4637-891a-dec35c318583 100") { CreateNoWindow = true, UseShellExecute = false })) { }
                using (Process.Start(new ProcessStartInfo("powercfg.exe", "/setacvalueindex scheme_current sub_processor ea06c951-1d8c-4a23-8619-d15a3c0e5f20 100") { CreateNoWindow = true, UseShellExecute = false })) { }
                using (Process.Start(new ProcessStartInfo("powercfg.exe", "/setdcvalueindex scheme_current sub_processor ea06c951-1d8c-4a23-8619-d15a3c0e5f20 100") { CreateNoWindow = true, UseShellExecute = false })) { }
                using (Process.Start(new ProcessStartInfo("powercfg.exe", "/setactive scheme_current") { CreateNoWindow = true, UseShellExecute = false })) { }
                if (!silent)
                    MessageBox.Show("✓ تم إلغاء فرملة جميع الأنوية (Core Unparking) بنجاح!\nستعمل جميع الأنوية الآن بأقصى طاقتها لضمان ثبات الفريمات ومنع الـ micro-stutter.", "معزز الألعاب");
            }
            catch (Exception ex)
            {
                CWLog.Error("MainWindow.UnparkCPUCores", ex);
                if (!silent) MessageBox.Show("فشل إلغاء الفرملة: " + ex.Message, "معزز الألعاب");
            }
        }

        private void EnableGPUMSIMode()
        {
            try
            {
                using (var rk = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\PCI", true))
                {
                    if (rk == null) return;
                    int count = 0;
                    foreach (string device in rk.GetSubKeyNames())
                    {
                        using (var sub = rk.OpenSubKey(device, true))
                        {
                            if (sub == null) continue;
                            foreach (string instance in sub.GetSubKeyNames())
                            {
                                using (var inst = sub.OpenSubKey(instance, true))
                                {
                                    if (inst == null) continue;
                                    string classGuid = Convert.ToString(inst.GetValue("ClassGUID"));
                                    if (classGuid == "{4d36e968-e325-11ce-bfc1-08002be10318}")
                                    {
                                        using (var paramsKey = inst.CreateSubKey(@"Device Parameters\Interrupt Management\MessageSignaledInterruptsProperties", RegistryKeyPermissionCheck.ReadWriteSubTree))
                                        {
                                            if (paramsKey != null)
                                            {
                                                paramsKey.SetValue("MSISupported", 1, RegistryValueKind.DWord);
                                                count++;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    if (count > 0)
                        MessageBox.Show("✓ تم تفعيل نمط MSI لكرت الشاشة بنجاح!\nيرجى إعادة تشغيل الجهاز لتطبيق التغييرات لضمان أفضل سلاسة فريمات.", "معزز الألعاب");
                    else
                        MessageBox.Show("لم يتم العثور على كروت شاشة متوافقة في السجل.", "معزز الألعاب");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("فشل تفعيل نمط MSI: " + ex.Message, "معزز الألعاب");
            }
        }

        private void ToggleServicesBooster()
        {
            try
            {
                _servicesBoosted = !_servicesBoosted;
                if (_servicesBoosted)
                {
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        int stopped = 0;
                        foreach (string svc in _targetServices)
                        {
                            try
                            {
                                var psi = new ProcessStartInfo("sc.exe", "stop " + svc) { CreateNoWindow = true, UseShellExecute = false };
                                using (var p = Process.Start(psi)) { if (p != null) p.WaitForExit(); }
                                stopped++;
                                // Track it so exit-time RevertGameMode restores the
                                // service again: the booster must never leave the
                                // machine with the services stopped after ClearWin closes.
                                lock (_gameModeServicesLock) { if (_gameModeServicesToRestart == null) _gameModeServicesToRestart = new List<string>(); if (!_gameModeServicesToRestart.Contains(svc)) _gameModeServicesToRestart.Add(svc); }
                            } catch (Exception ex132) { CWLog.Error("MainWindow.Logic", ex132); }
                        }
                        var disp4 = Dispatcher;
                        disp4.BeginInvoke(new Action(() => {
                            MessageBox.Show(string.Format("✓ تم إيقاف {0} خدمات خلفية غير ضرورية بنجاح!\nتم تحرير موارد المعالج والذاكرة بالكامل لخدمة ألعابك.", stopped), "معزز الألعاب");
                        }));
                    });
                }
                else
                {
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        int started = 0;
                        foreach (string svc in _targetServices)
                        {
                            try
                            {
                                var psi = new ProcessStartInfo("sc.exe", "start " + svc) { CreateNoWindow = true, UseShellExecute = false };
                                using (var p = Process.Start(psi)) { if (p != null) p.WaitForExit(); }
                                started++;
                                // The service is running again: drop it from the
                                // exit-time restart queue so RevertGameMode does not
                                // double-start it.
                                lock (_gameModeServicesLock) { if (_gameModeServicesToRestart != null) _gameModeServicesToRestart.Remove(svc); }
                            } catch (Exception ex133) { CWLog.Error("MainWindow.Logic", ex133); }
                        }
                        var disp5 = Dispatcher;
                        disp5.BeginInvoke(new Action(() => {
                            MessageBox.Show(string.Format("✓ تمت إعادة تشغيل {0} خدمات خلفية بأمان.", started), "معزز الألعاب");
                        }));
                    });
                }
            }
            catch (Exception ex) { MessageBox.Show("خطأ: " + ex.Message, "معزز الألعاب"); }
        }

        private void ApplySmartCPUPriorities(bool showResult = true)
        {
            try
            {
                int boosted = 0, throttled = 0;
                IntPtr fg = GetForegroundWindow();
                uint fgPid;
                GetWindowThreadProcessId(fg, out fgPid);
                
                string[] backgroundHelpers = new[] { "chrome", "msedge", "firefox", "teams", "slack", "discord", "spotify", "onedrive", "dropbox" };

                var procs = Process.GetProcesses();
                try
                {
                    foreach (var p in procs)
                    {
                        try
                        {
                            if (fgPid > 0 && p.Id == (int)fgPid)
                            {
                                p.PriorityClass = ProcessPriorityClass.High;
                                boosted++;
                            }
                            else if (Array.IndexOf(backgroundHelpers, p.ProcessName.ToLowerInvariant()) >= 0)
                            {
                                lock (_gamePriorityLock)
                                {
                                    if (!_gamePriorityBaseline.ContainsKey(p.Id))
                                        _gamePriorityBaseline[p.Id] = p.PriorityClass;
                                }
                                p.PriorityClass = ProcessPriorityClass.BelowNormal;
                                throttled++;
                            }
                        } catch (Exception ex134) { CWLog.Error("MainWindow.Logic", ex134); }
                    }
                }
                finally { foreach (var p in procs) { try { p.Dispose(); } catch (Exception ex135) { CWLog.Error("MainWindow.Logic", ex135); } } }
                if (showResult) MessageBox.Show(string.Format("✓ تم تطبيق نظام الجدولة الذكي!\n- رفع أولوية اللعبة الأمامية ({0})\n- تخفيض أولوية {1} من العمليات الخلفية المسببة للبطء.", boosted, throttled), "معزز الألعاب");
            }
            catch (Exception ex) { MessageBox.Show("خطأ: " + ex.Message, "معزز الألعاب"); }
        }

        private void ApplyLatencyTweaks()
        {
            try
            {
                using (var rk = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces", true))
                {
                    foreach (var sub in rk.GetSubKeyNames())
                    {
                        try
                        {
                            using (var intf = rk.OpenSubKey(sub, true))
                            {
                                if (intf != null) { intf.SetValue("TcpAckFrequency", 1, RegistryValueKind.DWord); intf.SetValue("TCPNoDelay", 1, RegistryValueKind.DWord); }
                            }
                        } catch (Exception ex136) { CWLog.Error("MainWindow.Logic", ex136); }
                    }
                }
                using (var mouse = Registry.CurrentUser.CreateSubKey(@"Control Panel\Mouse", true))
                {
                    mouse.SetValue("MouseSpeed", "0", RegistryValueKind.String);
                    mouse.SetValue("MouseThreshold1", "0", RegistryValueKind.String);
                    mouse.SetValue("MouseThreshold2", "0", RegistryValueKind.String);
                }
                MessageBox.Show("تم تطبيق تحسينات زمن الاستجابة!\n- تعطيل Nagle's Algorithm\n- تعطيل Mouse Acceleration\n(TcpAckFrequency, TCPNoDelay)", "Latency Optimizer");
            }
            catch { MessageBox.Show("فشل تطبيق التحسينات", "خطأ"); }
        }

        private void AdvancedDiskClean()
        {
            var confirmResult = MessageBox.Show(
                "هل تريد التنظيف الشامل للقرص؟\n\nسيتم حذف:\n- الملفات المؤقتة\n- كاش المتصفحات\n- كاش كرت الشاشة\n- ملفات Windows Update القديمة\n- ملفات WinSxS\n\nهل أنت متأكد؟",
                "ClearWin - تنظيف شامل",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirmResult != MessageBoxResult.Yes) return;

            try
            {
                long freed = 0;
                string[] targets = new[] {
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Temp",
                    Environment.GetEnvironmentVariable("WINDIR") + "\\Temp",
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Microsoft\\Windows\\WER",
                    Environment.GetEnvironmentVariable("WINDIR") + "\\Prefetch",
                    Environment.GetEnvironmentVariable("WINDIR") + "\\SoftwareDistribution\\Download",
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\D3DSCache",
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Microsoft\\Edge\\User Data\\Default\\Cache",
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + "\\Google\\Chrome\\User Data\\Default\\Cache"
                    // Safety Guard: Windows\Installer\$PatchCache$ and System32\Winevt\Logs are preserved to protect MSI repair and Event Viewer audit trails.
                };
                foreach (string dir in targets)
                {
                    if (!Directory.Exists(dir)) continue;
                    try
                    {
                        foreach (string f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                        {
                            try { long len = new FileInfo(f).Length; File.Delete(f); freed += len; } catch (Exception ex137) { CWLog.Error("MainWindow.Logic", ex137); }
                        }
                    } catch (Exception ex138) { CWLog.Error("MainWindow.Logic", ex138); }
                }
                // WinSxS cleanup via DISM
                try
                {
                    var psi = new ProcessStartInfo("dism.exe", "/online /cleanup-image /StartComponentCleanup /Quiet") { CreateNoWindow = true, UseShellExecute = false };
                    using (var p = Process.Start(psi)) { p.WaitForExit(120000); }
                } catch (Exception ex139) { CWLog.Error("MainWindow.Logic", ex139); }
                freed /= 1048576;
                MessageBox.Show(string.Format("✓ اكتمل التنظيف الشامل بنجاح!\nتم تحرير مساحة {0:F0} MB من الملفات المؤقتة والمهملات وكاش المتصفحات والكرت.", freed), "منظف القرص الفائق");
            }
            catch { MessageBox.Show("فشل التنظيف", "خطأ"); }
        }

        private void InitScheduler()
        {
            _autoScheduler = true;
        }

        private void UpdateAutoPilotUI()
        {
            if (_autoPilotStatus != null)
                _autoPilotStatus.Text = _autoPilot ? (_autoPilotActive ? "نشط 🎮" : "مراقب") : "معطل";
        }

        private void RunDebloater()
        {
            var confirmResult = MessageBox.Show(
                "تنبيه: إزالة تطبيقات Windows المسبقة التثبيت (معطلة افتراضياً)\n\n" +
                "سيتم إزالة الحزم الترويجية وغير الضرورية فقط:\n" +
                "- تطبيقات Bing (News, Weather, Sports, Finance)\n" +
                "- ملحقات Xbox الدعائية\n" +
                "- 3DBuilder, Clipchamp, Solitaire, Skype, Zune\n\n" +
                "ملاحظة: الأدوات الأساسية (Notepad, Calculator, Paint, Terminal) محمية ولن تُحذف.\n\n" +
                "هل أنت متأكد من المتابعة وحذف هذه التطبيقات؟",
                "ClearWin - إزالة التطبيقات المسبقة",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (confirmResult != MessageBoxResult.Yes) return;

            // Protected user utilities (Camera, Alarms, SoundRecorder, OneNote, Todos, YourPhone, XboxGameBar) are excluded
            string[] bloat = new[] {
                "Microsoft.3DBuilder", "Microsoft.BingWeather", "Microsoft.BingNews", "Microsoft.BingSports",
                "Microsoft.Getstarted", "Microsoft.Messaging", "Microsoft.Microsoft3DViewer",
                "Microsoft.MicrosoftOfficeHub", "Microsoft.MicrosoftSolitaireCollection", "Microsoft.MixedReality.Portal",
                "Microsoft.OneConnect", "Microsoft.People", "Microsoft.Print3D",
                "Microsoft.SkypeApp", "Microsoft.Wallet",
                "Microsoft.WindowsFeedbackHub", "Microsoft.WindowsMaps",
                "Microsoft.ZuneMusic", "Microsoft.ZuneVideo", "Microsoft.Cortana",
                "Microsoft.Clipchamp"
            };
            int removed = 0, failed = 0;
            foreach (string app in bloat)
            {
                try
                {
                    var psi = new ProcessStartInfo("powershell.exe", string.Format("-Command \"Get-AppxPackage -Name '{0}' | Remove-AppxPackage -ErrorAction SilentlyContinue; Get-AppxProvisionedPackage -Online | Where-Object {{$_.PackageName -like '*{0}*'}} | Remove-AppxProvisionedPackage -Online -ErrorAction SilentlyContinue\"", app))
                    { CreateNoWindow = true, UseShellExecute = false };
                    using (var p = Process.Start(psi)) { p.WaitForExit(15000); if (p.ExitCode == 0) removed++; else failed++; }
                }
                catch { failed++; }
            }
            // Disable telemetry & Cortana via registry
            try
            {
                using (var rk = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", true))
                {
                    rk.SetValue("AllowTelemetry", 0, RegistryValueKind.DWord);
                }
            } catch (Exception ex140) { CWLog.Error("MainWindow.Logic", ex140); }
            try
            {
                using (var rk2 = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Windows Search", true))
                {
                    rk2.SetValue("AllowCortana", 0, RegistryValueKind.DWord);
                }
            } catch (Exception ex141) { CWLog.Error("MainWindow.Logic", ex141); }
            MessageBox.Show(string.Format("تم إزالة {0} تطبيق!\nفشل {1}", removed, failed), "Windows Debloater");
        }

        private void UpdateDiskPage()
        {
            try
            {
                int idx = 0;
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;
                    if (idx >= _diskBarFills.Count || idx >= _diskSizeTxts.Count || idx >= _diskFreeTxts.Count) break;

                    double totalGB = drive.TotalSize / 1073741824.0;
                    double freeGB = drive.AvailableFreeSpace / 1073741824.0;
                    double usedGB = totalGB - freeGB;
                    double pct = (usedGB / totalGB) * 100;

                    _diskSizeTxts[idx].Text = string.Format("{0} ({1}) - {2:F1} GB / {3:F1} GB ({4:F0}%)", drive.Name, drive.VolumeLabel, usedGB, totalGB, pct);
                    _diskBarFills[idx].Width = pct * 2.5;
                    _diskFreeTxts[idx].Text = string.Format("حر: {0:F1} جيجابايت", freeGB);
                    idx++;
                }
            } catch (Exception ex142) { CWLog.Error("MainWindow.Logic", ex142); }
        }

        private void UpdateHealthPage()
        {
            try
            {
                // Thermal
                bool thermalOk = !_agent.IsThermalThrottled;
                double cpuTemp = _agent.CurrentState.CpuTemp;
                if (_hsThermalStatus != null)
                {
                    _hsThermalStatus.Text = thermalOk ? "النظام آمن" : "⚠️ التبريد نشط";
                    _hsThermalStatus.Foreground = thermalOk ? Theme.GreenBrush : Theme.WarningBrush;
                }
                if (_hsTempVal != null)
                {
                    _hsTempVal.Text = string.Format("درجة حرارة CPU: {0:F1}°C", cpuTemp);
                }
                if (_hsTempBar != null)
                {
                    double tempPercent = Math.Min(100, cpuTemp);
                    _hsTempBar.Width = tempPercent * 2.2;
                    _hsTempBar.Background = cpuTemp > 80 ? (Brush)Theme.TempErrorGradient : cpuTemp > 65 ? (Brush)Theme.TempWarningGradient : (Brush)Theme.TempGreenGradient;
                }

                // Battery
                if (_hsBatVal != null && _agent.BatteryPercent >= 0)
                {
                    _hsBatVal.Text = string.Format("{0}%", _agent.BatteryPercent);
                    Color batColor = _agent.IsOnBattery ? Theme.Warning : Theme.Green;
                    _hsBatVal.Foreground = Theme.BrushFor(batColor);
                }
                if (_hsBatMode != null)
                {
                    _hsBatMode.Text = _agent.IsOnBattery ? "وضع توفير الطاقة" : "وضع الأداء الكامل";
                }
                if (_hsBatBar != null && _agent.BatteryPercent >= 0)
                {
                    _hsBatBar.Width = _agent.BatteryPercent * 2.2;
                    Color batBarColor = _agent.BatteryPercent > 50 ? Theme.Green : _agent.BatteryPercent > 20 ? Theme.Warning : Theme.Error;
                    _hsBatBar.Background = Theme.BrushFor(batBarColor);
                }

                // Disk
                if (_hsDiskVal != null)
                {
                    _hsDiskVal.Text = string.Format("{0:F0}%", _agent.DiskHealthPercent);
                    Color diskColor = _agent.DiskHealthPercent > 80 ? Theme.Green : _agent.DiskHealthPercent > 50 ? Theme.Warning : Theme.Error;
                    _hsDiskVal.Foreground = Theme.BrushFor(diskColor);
                }
                if (_hsDiskStatus != null)
                {
                    _hsDiskStatus.Text = _agent.DiskHealthStatus;
                }
                if (_hsDiskBar != null)
                {
                    _hsDiskBar.Width = _agent.DiskHealthPercent * 2.2;
                    _hsDiskBar.Background = Theme.DiskHealthGradient;
                }

                // Network
                if (_hsNetDown != null)
                {
                    _hsNetDown.Text = string.Format("{0:F1}", _agent.NetDownloadKBps);
                }
                if (_hsNetUp != null)
                {
                    _hsNetUp.Text = string.Format("{0:F1}", _agent.NetUploadKBps);
                }

                // Speed
                if (_hsSpeedResult != null)
                {
                    _hsSpeedResult.Text = string.IsNullOrEmpty(_agent.LastOptimizationResult) ? "لا توجد بيانات بعد" : _agent.LastOptimizationResult;
                }

                // Process alerts (every 5 ticks)
                if (_tickCounter % 5 == 0 && _hsHogStack != null)
                {
                    _hsHogStack.Children.Clear();
                    var hogAlerts = _agent.NewHogAlerts;
                    if (hogAlerts.Count == 0)
                    {
                        _hsHogStack.Children.Add(MakeText("لا توجد عمليات جديدة مكتشفة", 13, Theme.TextMuted, false));
                    }
                    else
                    {
                        for (int i = hogAlerts.Count - 1; i >= Math.Max(0, hogAlerts.Count - 5); i--)
                        {
                            var alertRow = new Border { Background = Theme.WarningAlpha15Brush, CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 3, 0, 3), BorderBrush = Theme.WarningAlpha30Brush, BorderThickness = new Thickness(1) };
                            alertRow.Child = MakeText("âڑ  " + hogAlerts[i], 11, Theme.Warning, false);
                            _hsHogStack.Children.Add(alertRow);
                        }
                    }
                }
            } catch (Exception ex143) { CWLog.Error("MainWindow.Logic", ex143); }
        }

        private void UpdateToolsServices()
        {
            if (_toolsSrvList != null)
            {
                LoadServices(_toolsSrvList, _toolsSrvFilter);
            }
        }
    }
}









