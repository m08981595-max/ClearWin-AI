using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ClearWin
{
    /// <summary>
    /// One persistent record of something the AI agent did:
    ///   kind = "session"  -> app run marker
    ///   kind = "decision" -> a decision the agent made (reason, confidence, risk)
    ///   kind = "executed" -> a decision that was actually applied (success/failure)
    ///   kind = "reward"   -> a measured positive reward for an action
    ///   kind = "penalty"  -> a measured negative reward (penalty) for an action
    /// </summary>
    public class AiMemoryRecord
    {
        public DateTime Timestamp;
        public string Kind;
        public string Action;
        public string Detail;
        public double Value;
        public bool Success;

        public AiMemoryRecord()
        {
            Timestamp = DateTime.Now;
            Kind = "";
            Action = "";
            Detail = "";
            Value = 0;
            Success = false;
        }
    }

    /// <summary>
    /// Persistent, human-inspectable store of every AI decision / outcome /
    /// reward / penalty across app runs. Distinct from the agent log
    /// (transient, 500-entry ring) and from LongTermMemory (RL state -> score
    /// lookup): this is a human-facing audit trail for the Web UI settings page.
    ///
    /// File format: plain-text, one line per record, '|' delimited:
    ///   kind|yyyy-MM-dd HH:mm:ss|value|0|1|action|detail
    /// (action/detail are JSON-escaped so '|' cannot corrupt the record)
    ///
    /// C# 5 compatible (the build may fall back to the legacy csc.exe).
    /// </summary>
    public sealed class AiPersistentMemory
    {
        private const int MAX_RECORDS = 3000;
        private const double SAVE_THROTTLE_SECONDS = 15.0;

        private readonly object _sync = new object();
        private readonly object _fileLock = new object();
        private readonly string _path;
        private readonly List<AiMemoryRecord> _records = new List<AiMemoryRecord>();
        private DateTime _lastSave = DateTime.MinValue;

        public AiPersistentMemory(string path)
        {
            _path = path;
        }

        public string FilePath { get { return _path; } }

        /// <summary>Loads the store from disk. Missing file = empty store, not an error.</summary>
        public bool Load()
        {
            try
            {
                if (!File.Exists(_path)) return false;
                string[] lines = File.ReadAllLines(_path);
                List<AiMemoryRecord> parsed = new List<AiMemoryRecord>(lines.Length);
                for (int i = 0; i < lines.Length; i++)
                {
                    AiMemoryRecord r = ParseLine(lines[i]);
                    if (r != null) parsed.Add(r);
                }
                lock (_sync) { _records.Clear(); _records.AddRange(parsed); }
                return true;
            }
            catch (Exception ex) { CWLog.Error("AiMemory.Load", ex); return false; }
        }

        /// <summary>Appends a record and persists the store (throttled on-disk writes).</summary>
        public void Record(string kind, string action, string detail, double value, bool success)
        {
            try
            {
                var r = new AiMemoryRecord();
                r.Timestamp = DateTime.Now;
                r.Kind = kind ?? "";
                r.Action = action ?? "";
                r.Detail = detail ?? "";
                r.Value = (double.IsNaN(value) || double.IsInfinity(value)) ? 0.0 : value;
                r.Success = success;

                bool force = false;
                lock (_sync)
                {
                    _records.Add(r);
                    if (_records.Count > MAX_RECORDS) _records.RemoveAt(0);
                    if (_records.Count == MAX_RECORDS) force = true;
                }
                Save(force);
            }
            catch (Exception ex) { CWLog.Error("AiMemory.Record", ex); }
        }

        /// <summary>Returns the most recent <paramref name="count"/> records in chronological order.</summary>
        public AiMemoryRecord[] GetRecent(int count)
        {
            if (count <= 0) return new AiMemoryRecord[0];
            lock (_sync)
            {
                int n = _records.Count;
                if (count > n) count = n;
                var outList = new AiMemoryRecord[count];
                for (int i = 0; i < count; i++) outList[i] = _records[n - count + i];
                return outList;
            }
        }
        /// <summary>Aggregate counters for the summary chips on the settings page.</summary>
        public void GetSummary(out int total, out int decisions, out int executedOk, out int executedFail,
            out int rewards, out int penalties, out double bestReward, out double worstPenalty,
            out DateTime first, out DateTime last)
        {
            total = 0; decisions = 0; executedOk = 0; executedFail = 0;
            rewards = 0; penalties = 0; bestReward = 0; worstPenalty = 0;
            first = DateTime.MinValue; last = DateTime.MinValue;
            lock (_sync)
            {
                total = _records.Count;
                for (int i = 0; i < _records.Count; i++)
                {
                    AiMemoryRecord r = _records[i];
                    switch (r.Kind)
                    {
                        case "decision": decisions++; break;
                        case "executed": if (r.Success) executedOk++; else executedFail++; break;
                        case "reward": rewards++; if (r.Value > bestReward) bestReward = r.Value; break;
                        case "penalty": penalties++; if (r.Value < worstPenalty) worstPenalty = r.Value; break;
                    }
                    if (i == 0 || r.Timestamp < first) first = r.Timestamp;
                    if (r.Timestamp > last) last = r.Timestamp;
                }
            }
        }

        /// <summary>Wipes the store and persists the empty state immediately.</summary>
        public void Clear()
        {
            lock (_sync) { _records.Clear(); }
            Save(true);
        }

        /// <summary>Writes the full store as a JSON payload (the Web UI settings view).</summary>
        public string ToJson(int recentCount)
        {
            try
            {
                int total, decisions, execOk, execFail, rewards, penalties;
                double best, worst;
                DateTime first, last;
                GetSummary(out total, out decisions, out execOk, out execFail, out rewards, out penalties,
                    out best, out worst, out first, out last);

                AiMemoryRecord[] recs = GetRecent(recentCount);
                StringBuilder sb = new StringBuilder(2048 + recs.Length * 220);
                sb.Append("{\"type\":\"aimemory\",\"file\":\"").Append(Escape(_path)).Append("\",")
                  .Append("\"total\":").Append(total.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append("\"decisions\":").Append(decisions.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append("\"executedOk\":").Append(execOk.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append("\"executedFail\":").Append(execFail.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append("\"rewards\":").Append(rewards.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append("\"penalties\":").Append(penalties.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append("\"bestReward\":").Append(Sanitized(best).ToString("F3", CultureInfo.InvariantCulture)).Append(',')
                  .Append("\"worstPenalty\":").Append(Sanitized(worst).ToString("F3", CultureInfo.InvariantCulture)).Append(',')
                  .Append("\"first\":").Append(first == DateTime.MinValue ? "null" : "\"" + first.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "\",")
                  .Append("\"last\":").Append(last == DateTime.MinValue ? "null" : "\"" + last.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "\"");

                sb.Append(",\"entries\":[");
                for (int i = 0; i < recs.Length; i++)
                {
                    AiMemoryRecord r = recs[i];
                    if (i > 0) sb.Append(',');
                    sb.Append("{\"ts\":\"").Append(r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append("\",")
                      .Append("\"kind\":\"").Append(Escape(r.Kind)).Append("\",")
                      .Append("\"action\":\"").Append(Escape(r.Action)).Append("\",")
                      .Append("\"detail\":\"").Append(Escape(r.Detail)).Append("\",")
                      .Append("\"value\":").Append(Sanitized(r.Value).ToString("F3", CultureInfo.InvariantCulture)).Append(',')
                      .Append("\"success\":").Append(r.Success ? "true" : "false").Append('}');
                }
                sb.Append("]}");
                return sb.ToString();
            }
            catch (Exception ex) { CWLog.Error("AiMemory.ToJson", ex); return "{\"type\":\"aimemory\",\"entries\":[]}"; }
        }

        /// <summary>Number of records currently held by the store.</summary>
        public int TotalCount
        {
            get { lock (_sync) { return _records.Count; } }
        }

        // ------------------------------------------------ file persistence

        private void Save(bool force)
        {
            try
            {
                lock (_fileLock)
                {
                    bool needWrite = force || (DateTime.Now - _lastSave).TotalSeconds >= SAVE_THROTTLE_SECONDS;
                    if (!needWrite) return;

                    string[] snapshot;
                    lock (_sync)
                    {
                        snapshot = new string[_records.Count];
                        for (int i = 0; i < _records.Count; i++)
                        {
                            AiMemoryRecord r = _records[i];
                            snapshot[i] = Escape(r.Kind) + "|" + r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                                + "|" + Sanitized(r.Value).ToString("F3", CultureInfo.InvariantCulture)
                                + "|" + (r.Success ? "1" : "0")
                                + "|" + Escape(r.Action) + "|" + Escape(r.Detail);
                        }
                    }

                    string dir = Path.GetDirectoryName(_path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    string tmp = _path + ".tmp";
                    File.WriteAllLines(tmp, snapshot, new UTF8Encoding(false));
                    if (File.Exists(_path)) File.Delete(_path);
                    File.Move(tmp, _path);
                    _lastSave = DateTime.Now;
                }
            }
            catch (Exception ex) { CWLog.Error("AiMemory.Save", ex); }
        }

        private static AiMemoryRecord ParseLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return null;
            string[] p = line.Split('|');
            if (p.Length < 6) return null;
            var r = new AiMemoryRecord();
            r.Kind = Unescape(p[0]);
            double v;
            if (double.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out v)) r.Value = v;
            r.Success = p[3] == "1";
            r.Action = Unescape(p[4]);
            r.Detail = Unescape(p[5]);
            DateTime ts;
            if (DateTime.TryParse(p[1], CultureInfo.InvariantCulture, DateTimeStyles.None, out ts)) r.Timestamp = ts;
            else r.Timestamp = DateTime.Now;
            return r;
        }

        // ------------------------------------------------ json/text helpers

        private static double Sanitized(double value)
        {
            return (double.IsNaN(value) || double.IsInfinity(value)) ? 0.0 : value;
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            StringBuilder sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '|': sb.Append("%7C"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        private static string Unescape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            string s = value.Replace("%7C", "|").Replace("%7c", "|");
            StringBuilder sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                if (ch == '\\' && i + 1 < s.Length)
                {
                    char n = s[++i];
                    switch (n)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case '|': sb.Append('|'); break;
                        default:
                            sb.Append('\\');
                            sb.Append(n);
                            break;
                    }
                }
                else sb.Append(ch);
            }
            return sb.ToString();
        }
    }
}
