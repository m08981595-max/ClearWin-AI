using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace ClearWin
{
    public partial class SmartAgent
    {
        // =====================================================================
        // REAL LOG IMPORTER
        // Turns telemetry captured by tools/collect_real_logs.ps1 (real system
        // samples + optimization event markers) into genuine learning
        // experiences: it feeds the measured before/after effect into the real
        // state-aware double Q-learning (RL2), the ML replay buffer, and the
        // reward statistics — exactly like a live measured outcome window.
        // =====================================================================
        private readonly object _realLogLock = new object();
        private int _realLogImported;

        public int GetRealLogImportedCount() { return _realLogImported; }

        public string GetRealLogStatus()
        {
            return string.Format("Real-log imports: {0} | Evaluations: {1} | Avg reward: {2:F3}",
                _realLogImported, _evaluationsCompleted, GetAverageObjectiveReward());
        }

        private class RealLogSample
        {
            public DateTime T;
            public double Cpu, Ram, Temp, Fps, Fps1Low, Stability, Gpu, Disk, Proc, Battery;
            public bool Charging, IsGame;
        }

        private class RealLogEvent
        {
            public DateTime T;
            public string Action;
        }

        private class RealLogWindow
        {
            public double Cpu, Ram, Temp, Fps, Fps1Low, Stability, Gpu, Disk, Proc, Battery;
            public bool Charging, IsGame;
            public int Count;
            public double FpsSlope, TempSlope, RamSlope, FpsCv;
            public DateTime First, Last;
        }

        /// <summary>
        /// Imports a real-log CSV captured by the collector into the learning
        /// system. Every "event,Action" row produces one experience: the effect
        /// is measured as the delta between the baseline window (before the
        /// event) and the outcome window (after it), drift-corrected like the
        /// live reward engine (difference-in-differences).
        /// Returns the number of experiences imported.
        /// </summary>
        public int ImportRealLog(string csvPath, double baselineSeconds = 45, double outcomeSeconds = 45, string source = "RealLog")
        {
            lock (_realLogLock)
            {
                if (string.IsNullOrEmpty(csvPath) || !File.Exists(csvPath)) return 0;
                try
                {
                    var samples = new List<RealLogSample>();
                    var events = new List<RealLogEvent>();
                    ParseRealLogCsv(csvPath, samples, events);
                    if (events.Count == 0)
                    {
                        AddLog("IMPORT", "RealLog", "No events found in " + Path.GetFileName(csvPath));
                        return 0;
                    }
                    if (samples.Count == 0)
                    {
                        AddLog("IMPORT", "RealLog", "No telemetry samples found in " + Path.GetFileName(csvPath));
                        return 0;
                    }

                    int imported = 0;
                    foreach (var ev in events)
                    {
                        if (!IsMeasurableAction(ev.Action))
                        {
                            AddLog("IMPORT", "RealLog", "Skipped advisory action: " + ev.Action);
                            continue;
                        }
                        RealLogWindow before = WindowFor(samples, ev.T.AddSeconds(-baselineSeconds), ev.T, false);
                        RealLogWindow after = WindowFor(samples, ev.T, ev.T.AddSeconds(outcomeSeconds), true);
                        if (before == null || after == null || before.Count < 2 || after.Count < 2)
                        {
                            AddLog("IMPORT", "RealLog", "Insufficient samples around event: " + ev.Action);
                            continue;
                        }

                        double windowSec = (after.Last - before.First).TotalSeconds;
                        if (windowSec <= 0) windowSec = outcomeSeconds;

                        // Reliability gate (same rule as the live engine): during
                        // a game the window is only attributable if FPS stayed
                        // steady. Noisy windows are dropped, not credited.
                        if (after.IsGame && after.Fps > 5 && after.FpsCv > 0.45)
                        {
                            AddLog("IMPORT", "RealLog", string.Format("Unreliable window for {0} (FPS CV {1:F2})", ev.Action, after.FpsCv));
                            continue;
                        }

                        var rec = new ActionOutcomeRecord();
                        rec.Action = ev.Action;
                        rec.ExecutedSuccessfully = true;
                        rec.StartTime = ev.T;
                        rec.BaselineCpu = before.Cpu;
                        rec.BaselineRam = before.Ram;
                        rec.BaselineTemp = before.Temp;
                        rec.BaselineFps = before.Fps;
                        rec.BaselineFps1Low = before.Fps1Low;
                        rec.BaselineStability = before.Stability;
                        rec.BaselineBattery = before.Battery;
                        rec.IsGame = before.IsGame;
                        rec.OnBattery = !before.Charging;
                        rec.BaselineFpsSlope = before.FpsSlope;
                        rec.BaselineTempSlope = before.TempSlope;
                        rec.BaselineRamSlope = before.RamSlope;

                        double r = ComputeObjectiveReward(rec, after.Fps, after.Fps1Low, after.Temp,
                            after.Stability, after.Ram, after.Battery, windowSec);
                        string s0 = BuildRealLogStateKey(before);
                        string s1 = BuildRealLogStateKey(after);

                        RecordRLExperience(s0, ev.Action, r, s1);
                        RecordActionOutcome(ev.Action, Clamp01(0.5 + r * 0.5), BuildRealLogFeatures(before, after));
                        RecordActionReward(ev.Action, r);
                        RecordSourceOutcome(source, r);
                        _objectiveRewardTotal += r;
                        _objectiveRewardCount++;
                        _evaluationsCompleted++;
                        _realLogImported++;
                        imported++;

                        AddLog("IMPORT", "RealLog",
                            string.Format("{0}: imported reward={1:F3} ({2} -> {3}, fps {4:F0}->{5:F0})",
                            ev.Action, r, s0, s1, before.Fps, after.Fps));
                    }
                    AddLog("IMPORT", "RealLog",
                        string.Format("Imported {0} real experiences from {1}", imported, Path.GetFileName(csvPath)));
                    return imported;
                }
                catch (Exception ex) { LogError("ImportRealLog", ex); return 0; }
            }
        }

        private void ParseRealLogCsv(string path, List<RealLogSample> samples, List<RealLogEvent> events)
        {
            foreach (string raw in File.ReadAllLines(path))
            {
                if (string.IsNullOrEmpty(raw)) continue;
                string line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                string[] p = line.Split(',');
                if (p.Length >= 2 && string.Equals(p[0].Trim(), "event", StringComparison.OrdinalIgnoreCase))
                {
                    var ev = new RealLogEvent();
                    if (p.Length >= 3 && !DateTime.TryParseExact(p[2].Trim(), "yyyy-MM-ddTHH:mm:ss.fff",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out ev.T))
                    {
                        ev.T = DateTime.Now;
                    }
                    ev.Action = p[1].Trim();
                    events.Add(ev);
                    continue;
                }
                if (p.Length < 13) continue;
                double[] v = new double[12];
                bool ok = true;
                for (int i = 1; i < 13; i++)
                {
                    if (!double.TryParse(p[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v[i - 1])) { ok = false; break; }
                }
                if (!ok) continue;
                var s = new RealLogSample();
                if (!DateTime.TryParseExact(p[0].Trim(), "yyyy-MM-ddTHH:mm:ss.fff",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out s.T))
                {
                    if (!DateTime.TryParse(p[0].Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out s.T)) continue;
                }
                s.Cpu = v[0];
                s.Ram = v[1];
                s.Temp = v[2];
                s.Fps = v[3];
                s.Fps1Low = v[4];
                s.Stability = v[5];
                s.Gpu = v[6];
                s.Disk = v[7];
                s.Proc = v[8];
                s.Battery = v[9];
                s.Charging = v[10] >= 0.5;
                s.IsGame = v[11] >= 0.5;
                samples.Add(s);
            }
        }

        private RealLogWindow WindowFor(List<RealLogSample> samples, DateTime start, DateTime end, bool afterWindow)
        {
            RealLogWindow w = new RealLogWindow();
            w.First = DateTime.MaxValue;
            w.Last = DateTime.MinValue;
            int gameCount = 0;
            double fpsSum = 0, fpsSum2 = 0, fpsN = 0;
            double fpsPrev = double.NaN; DateTime fpsPrevT = DateTime.MinValue;
            double tempPrev = double.NaN, ramPrev = double.NaN;
            for (int i = 0; i < samples.Count; i++)
            {
                RealLogSample s = samples[i];
                bool inWindow;
                if (afterWindow) inWindow = s.T > start && s.T <= end;
                else inWindow = s.T >= start && s.T < end;
                if (!inWindow) continue;

                w.Cpu += s.Cpu; w.Ram += s.Ram; w.Temp += s.Temp; w.Fps += s.Fps;
                w.Fps1Low += s.Fps1Low; w.Stability += s.Stability; w.Gpu += s.Gpu;
                w.Disk += s.Disk; w.Proc += s.Proc; w.Battery += s.Battery;
                if (s.Charging) { w.Charging = true; } else { w.Charging = false; }
                if (s.IsGame) gameCount++;
                w.Count++;
                if (s.T < w.First) w.First = s.T;
                if (s.T > w.Last) w.Last = s.T;

                if (s.Fps > 5) { fpsSum += s.Fps; fpsSum2 += s.Fps * s.Fps; fpsN++; }
                double dt = fpsPrevT != DateTime.MinValue ? (s.T - fpsPrevT).TotalSeconds : 0;
                if (dt >= 0.001 && dt <= 10)
                {
                    if (!double.IsNaN(fpsPrev)) w.FpsSlope += (s.Fps - fpsPrev) / dt;
                    if (!double.IsNaN(tempPrev)) w.TempSlope += (s.Temp - tempPrev) / dt;
                    if (!double.IsNaN(ramPrev)) w.RamSlope += (s.Ram - ramPrev) / dt;
                }
                fpsPrev = s.Fps; tempPrev = s.Temp; ramPrev = s.Ram; fpsPrevT = s.T;
            }
            if (w.Count == 0) return null;
            int n = w.Count;
            w.Cpu /= n; w.Ram /= n; w.Temp /= n; w.Fps /= n; w.Fps1Low /= n;
            w.Stability /= n; w.Gpu /= n; w.Disk /= n; w.Proc /= n; w.Battery /= n;
            w.IsGame = gameCount >= n - gameCount;
            double slopeN = n - 1;
            if (slopeN > 0) { w.FpsSlope /= slopeN; w.TempSlope /= slopeN; w.RamSlope /= slopeN; }
            if (w.IsGame && fpsN >= 4 && w.Fps > 5)
            {
                double mean = fpsSum / fpsN;
                double vari = fpsSum2 / fpsN - mean * mean;
                if (vari < 0) vari = 0;
                w.FpsCv = Math.Sqrt(vari) / mean;
            }
            return w;
        }

        private string BuildRealLogStateKey(RealLogWindow w)
        {
            RLState st = new RLState();
            st.CpuBin = Discretize(w.Cpu, CPU_BINS);
            st.RamBin = Discretize(w.Ram, RAM_BINS);
            st.TempBin = Discretize(w.Temp, TEMP_BINS);
            st.FpsBin = Discretize(w.Fps, FPS_BINS);
            st.GpuBin = Discretize(w.Gpu, GPU_BINS);
            st.DiskBin = Discretize(w.Disk, DISK_BINS);
            st.HourBin = RealLogHourBin(w.First);
            st.Mode = _appMode.ToString();
            return BuildStateKey(st);
        }

        private static int RealLogHourBin(DateTime t)
        {
            int h = t.Hour;
            if (h < 8) return 0;
            if (h < 17) return 1;
            return 2;
        }

        private static MLFeatureVector BuildRealLogFeatures(RealLogWindow before, RealLogWindow after)
        {
            var fv = new MLFeatureVector();
            fv.CpuUsage = before.Cpu;
            fv.RamUsage = before.Ram;
            fv.GpuUsage = before.Gpu;
            fv.DiskUsage = before.Disk;
            fv.CpuTemp = before.Temp;
            fv.GpuTemp = 0;
            fv.Fps = before.Fps;
            fv.Fps01Low = before.Fps1Low;
            fv.FpsStability = before.Stability;
            fv.ProcessCount = (int)before.Proc;
            fv.HourOfDay = before.First.Hour;
            fv.MinuteOfHour = before.First.Minute;
            fv.IsGaming = before.IsGame ? 1 : 0;
            fv.IsOnBattery = before.Charging ? 0 : 1;
            fv.BatteryLevel = before.Battery;
            fv.CpuTrend = after.Cpu - before.Cpu;
            fv.RamTrend = after.Ram - before.Ram;
            fv.TempTrend = after.Temp - before.Temp;
            return fv;
        }
    }
}
