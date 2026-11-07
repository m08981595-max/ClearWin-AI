using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ClearWin
{
    // === Static application logger ===
    // Replaces silent `catch { }` blocks across MainWindow.* / Overlay* / Program.cs
    // so failures (native calls, WMI, registry, etc.) are no longer swallowed silently.
    // Writes to %LOCALAPPDATA%\ClearWin\logs\clearwin.log (rotated, capped at 2 MB).
    // All methods are safe to call from any thread and never throw.
    // Lines are buffered in memory and flushed to disk at most every 500 ms to
    // avoid an open/append/close cycle per log call (frequent in tick loops).
    internal static class CWLog
    {
        private static readonly object _lock = new object();
        private static string _logDir;
        private static string _logPath;
        private const long MAX_LOG_BYTES = 2 * 1024 * 1024; // 2 MB
        private const int MAX_BUFFER_BYTES = 64 * 1024;
        private static readonly StringBuilder _buf = new StringBuilder();
        private static DateTime _lastFlush = DateTime.MinValue;

        // === Duplicate suppression ===
        // The tick loop retries the same privileged OS calls (EmptyWorkingSet,
        // PriorityClass, WMI, native window text, ...) every cycle. Where those
        // calls are denied the unguarded logger wrote the identical line
        // thousands of times (6158 copies of one "Access is denied" in a single
        // session), filling the log, burning disk I/O and burying every other
        // diagnostic. We now write the first occurrence verbatim and fold the
        // repeats into an occasional summary that always carries the running
        // count, so no information is lost and the log stays readable.
        private const int DUP_REPORT_AFTER = 25;      // first summary after N repeats
        private const int DUP_REPORT_INTERVAL = 200;  // then every N repeats
        private const int MAX_TRACKED_DUPES = 512;    // bounded memory

        // Recency-based eviction: a key must be quiet for this many ticks before it
        // may be dropped. 1 tick ~ 1 second, so ~2 minutes of silence.
        private const long STALE_EVICT_TICKS = 120;
        private static long _tick;

        private sealed class DupCounter
        {
            public int Occurrences;   // how many times this exact line was requested
            public int Written;      // how many of those produced an actual log line
            public long LastTouched; // monotonic tick of the most recent occurrence
        }

        private static readonly Dictionary<string, DupCounter> _dups =
            new Dictionary<string, DupCounter>(StringComparer.Ordinal);

        private static string LogPath
        {
            get
            {
                if (_logPath != null) return _logPath;
                try
                {
                    string baseDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "ClearWin", "logs");
                    if (!Directory.Exists(baseDir)) Directory.CreateDirectory(baseDir);
                    _logDir = baseDir;
                    _logPath = Path.Combine(baseDir, "clearwin.log");
                }
                catch
                {
                    _logPath = null; // fall back to in-memory no-op
                }
                return _logPath;
            }
        }

        // Force buffered log lines to disk (called on app shutdown).
        public static void Flush()
        {
            string path = LogPath;
            if (path == null) return;
            lock (_lock)
            {
                try { FlushLocked(path); } catch { }
            }
        }

        // Primary error logger. Mirrors SmartAgent.LogError signature so catch-block
        // migration is a mechanical search/replace: catch { } -> catch (Exception ex) { CWLog.Error("ctx", ex); }
        public static void Error(string subsystem, Exception ex)
        {
            try
            {
                string msg;
                if (ex != null)
                    msg = string.Format("{0}: {1}", ex.GetType().Name, ex.Message);
                else
                    msg = "(null exception)";
                Write("ERROR", subsystem, msg);
            }
#if DEBUG
            catch (Exception ex2)
            {
                lock (_lock) { _diagLastSwallowed = ex2.ToString(); }
            }
#else
            catch
            {
            }
#endif
        }

        public static void Warn(string subsystem, string message)
        {
            try { Write("WARN", subsystem, message); } catch { }
        }

        public static void Info(string subsystem, string message)
        {
            try { Write("INFO", subsystem, message); } catch { }
        }

        public static void Log(string subsystem, string message)
        {
            try { Write("INFO", subsystem, message); } catch { }
        }

        // Test hook: start duplicate accounting from a clean slate.
        public static void ResetDuplicateTracking()
        {
            lock (_lock)
            {
                _dups.Clear();
                #if DEBUG
                _diagResets++;
                #endif
            }
        }

        // How many times this exact line was requested but did NOT get its own
        // log line because it was folded into an earlier one. This is the
        // quantity the limiter is supposed to bound, and it is what the unit
        // tests assert against (Occurrences - Written).
        public static int SuppressedCount(string level, string subsystem, string message)
        {
            lock (_lock)
            {
                DupCounter dc;
                if (_dups.TryGetValue(Key(level, subsystem, message), out dc))
                {
                    int n = dc.Occurrences - dc.Written;
                    return n > 0 ? n : 0;
                }
                return 0;
            }
        }

        // How many lines actually reached the file for this key. For N identical
        // requests this is 1 plus the number of periodic summaries (N=500 with the
        // default thresholds yields 3: the original plus the 25 and 225 marks).
        public static int WrittenCount(string level, string subsystem, string message)
        {
            lock (_lock)
            {
                DupCounter dc;
                if (_dups.TryGetValue(Key(level, subsystem, message), out dc)) return dc.Written;
                return 0;
            }
        }

        // Total requests seen for a key, regardless of folding. Lets the tests
        // assert the written+suppressed invariant without guessing.
        public static int OccurrenceCount(string level, string subsystem, string message)
        {
            lock (_lock)
            {
                DupCounter dc;
                if (_dups.TryGetValue(Key(level, subsystem, message), out dc)) return dc.Occurrences;
                return 0;
            }
        }

        // Monotonic ~1s tick used for recency-based eviction. Computed from the
        // wall clock so it needs no background thread.
        private static void AdvanceTick()
        {
            lock (_lock)
            {
                long t = (long)DateTime.UtcNow.Subtract(_tickEpoch).TotalSeconds;
                if (t > _tick) _tick = t;
            }
        }

        private static readonly DateTime _tickEpoch = DateTime.UtcNow;

        private static string Key(string level, string subsystem, string message)
        {
            return level + "|" + (subsystem ?? "?") + "|" + (message ?? "");
        }

#if DEBUG
        // Diagnostics: total Write() entries and eviction activity. Used to prove
        // the de-dup accounting is exact under a storm.
        private static int _diagWriteCalls;
        private static string _diagLastSwallowed;
        public static string DiagnosticLastSwallowed { get { lock (_lock) return _diagLastSwallowed; } }
        private static int _diagEvictions;
        private static int _diagResets;

        public static int DiagnosticWriteCalls { get { lock (_lock) return _diagWriteCalls; } }
        public static int DiagnosticEvictions { get { lock (_lock) return _diagEvictions; } }
        public static int DiagnosticResets { get { lock (_lock) return _diagResets; } }

        // Dumps the live de-dup table for debugging. Returns "key => occ/written" pairs.
        public static string DiagnosticDumpKeys()
        {
            lock (_lock)
            {
                StringBuilder sb = new StringBuilder();
                foreach (var kv in _dups)
                    sb.Append("[").Append(kv.Key).Append(" occ=").Append(kv.Value.Occurrences)
                      .Append(" w=").Append(kv.Value.Written).Append("] ");
                return sb.ToString();
            }
        }
#endif

        private static void Write(string level, string subsystem, string message)
        {
            AdvanceTick();
            #if DEBUG
            lock (_lock) _diagWriteCalls++;
            #endif
            string sub = subsystem ?? "?";
            string msg = message ?? "";
            string key = Key(level, sub, msg);
            int repeats;

            lock (_lock)
            {
                DupCounter dc;
                if (_dups.TryGetValue(key, out dc))
                {
                    dc.Occurrences++;
                    dc.LastTouched = _tick;
                }
                else
                {
                    // Bounded memory. See EvictStaleLocked for why the victim must be
                    // chosen by recency (then by fewest occurrences) rather than by
                    // wiping the table.
                    if (_dups.Count >= MAX_TRACKED_DUPES) EvictStaleLocked(key);
                    dc = new DupCounter();
                    dc.LastTouched = _tick;
                    dc.Occurrences = 1; // the first occurrence counts too, so a fresh
                                        // key's totals never start one short
                    _dups[key] = dc;
                }

                // repeats = number of times this message arrived BEFORE this one.
                repeats = dc.Occurrences - 1;

                if (repeats > 0)
                {
                    bool report = (repeats == DUP_REPORT_AFTER) ||
                                  (repeats > DUP_REPORT_AFTER &&
                                   ((repeats - DUP_REPORT_AFTER) % DUP_REPORT_INTERVAL) == 0);
                    if (!report) return; // folded away: no line written
                }
            }

            // De-duplication is decided first and independently of the file system,
            // so a record is never lost just because the log directory was briefly
            // unavailable. Written is only incremented once the line is actually
            // queued, keeping Occurrences == Written + Suppressed an exact identity.
            string path = LogPath;
            if (path == null) return;
            string ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            lock (_lock)
            {
                DupCounter counter;
                if (_dups.TryGetValue(key, out counter)) counter.Written++;
            }

            StringBuilder sb = new StringBuilder(160);
            sb.AppendFormat("{0} [{1}] {2}: {3}{4}", ts, level, sub, msg, Environment.NewLine);
            if (repeats > 0)
            {
                sb.AppendFormat("{0} [INFO] {1}: ({2} identical occurrences of the previous message folded into it){3}",
                    ts, sub, repeats, Environment.NewLine);
            }

            lock (_lock)
            {
                _buf.Append(sb.ToString());
                if (_buf.Length >= MAX_BUFFER_BYTES || (DateTime.Now - _lastFlush).TotalMilliseconds >= 500)
                {
                    try { FlushLocked(path); } catch { }
                }
            }
        }

        // Keeps the table bounded without destroying the accounting of messages
        // that are actively being logged.
        //
        // Two earlier attempts were wrong:
        //  1) Dictionary.Clear() on overflow reset Occurrences/Written to zero
        //     mid-storm, so a flooding key forgot it was flooding and started
        //     logging every occurrence again.
        //  2) Evicting the "least repeating" key was also wrong: at the very
        //     start of a storm the flooding key has Occurrences == 1, identical to
        //     genuine one-shot noise, so it was evicted on the first insert and
        //     its totals were silently lost.
        //
        // Correct policy: eviction must be based on recency, not volume. A key is
        // only a candidate once it has been quiet for STALE_EVICT_MS, so anything
        // the application is currently logging (no matter how noisy) is immune.
        // That preserves the accounting of live keys and still frees memory,
        // because keys that stopped arriving age out on their own.
        private static void EvictStaleLocked(string protectKey)
        {
            long now = _tick;
            string victim = null;
            long oldest = long.MaxValue;
            foreach (var kv in _dups)
            {
                if (kv.Key == protectKey) continue;
                long age = now - kv.Value.LastTouched;
                if (age < STALE_EVICT_TICKS) continue; // still active: never evict
                if (kv.Value.LastTouched < oldest)
                {
                    oldest = kv.Value.LastTouched;
                    victim = kv.Key;
                }
            }
            // Emergency path: every key is still active (a genuine flood of
            // distinct messages), so recency cannot separate them. Then volume
            // decides, but only in the safe direction: evict the key with the
            // FEWEST total occurrences. Picking by LastTouched here would be
            // wrong because all ticks are equal within a burst and dictionary
            // enumeration order is insertion order, so it would consistently
            // evict the oldest inserted key - which, for a long-running storm,
            // is exactly the key we must not forget.
            if (victim == null)
            {
                int fewest = int.MaxValue;
                foreach (var kv in _dups)
                {
                    if (kv.Key == protectKey) continue;
                    if (kv.Value.Occurrences < fewest)
                    {
                        fewest = kv.Value.Occurrences;
                        victim = kv.Key;
                    }
                }
            }
            if (victim != null)
            {
                _dups.Remove(victim);
                #if DEBUG
                _diagEvictions++;
                #endif
            }
        }

        private static void FlushLocked(string path)
        {
            if (_buf.Length == 0) return;
            try
            {
                // Rotate if oversized
                try
                {
                    FileInfo fi = new FileInfo(path);
                    if (fi.Exists && fi.Length > MAX_LOG_BYTES)
                    {
                        string bak = path + ".bak";
                        if (File.Exists(bak)) File.Delete(bak);
                        File.Move(path, bak);
                    }
                }
                catch { /* rotation failure is non-fatal */ }
                File.AppendAllText(path, _buf.ToString());
                _buf.Clear();
                _lastFlush = DateTime.Now;
            }
            catch { /* give up silently */ }
        }
    }
}
