using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ClearWin
{
    public class SecurityAuditEntry
    {
        public string Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string ActionId { get; set; }
        public string Tier { get; set; }
        public string Requester { get; set; }
        public string AdminMode { get; set; }
        public bool PreconditionsMet { get; set; }
        public string ExecutionStatus { get; set; }
        public string VerificationStatus { get; set; }
        public string Details { get; set; }
        public long DurationMs { get; set; }

        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{");
            sb.Append("\"id\":\"").Append(Id).Append("\",");
            sb.Append("\"timestamp\":\"").Append(Timestamp.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")).Append("\",");
            sb.Append("\"actionId\":\"").Append(Escape(ActionId)).Append("\",");
            sb.Append("\"tier\":\"").Append(Escape(Tier)).Append("\",");
            sb.Append("\"requester\":\"").Append(Escape(Requester)).Append("\",");
            sb.Append("\"adminMode\":\"").Append(Escape(AdminMode)).Append("\",");
            sb.Append("\"preconditionsMet\":").Append(PreconditionsMet ? "true" : "false").Append(",");
            sb.Append("\"executionStatus\":\"").Append(Escape(ExecutionStatus)).Append("\",");
            sb.Append("\"verificationStatus\":\"").Append(Escape(VerificationStatus)).Append("\",");
            sb.Append("\"details\":\"").Append(Escape(Details)).Append("\",");
            sb.Append("\"durationMs\":").Append(DurationMs);
            sb.Append("}");
            return sb.ToString();
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ");
        }
    }

    /// <summary>
    /// Thread-safe local security and autonomy audit logger.
    /// Writes append-only structured JSONL records into %LOCALAPPDATA%\ClearWin\audit\audit_log.jsonl
    /// </summary>
    public static class ClearWinAuditLogger
    {
        private static readonly object _fileLock = new object();
        private static readonly List<SecurityAuditEntry> _recentMemoryLogs = new List<SecurityAuditEntry>();
        private const int MAX_MEMORY_LOGS = 200;
        private const long MAX_AUDIT_LOG_BYTES = 5 * 1024 * 1024; // 5 MB

        internal static void RotateIfNeeded(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var fi = new FileInfo(path);
                    if (fi.Length >= MAX_AUDIT_LOG_BYTES)
                    {
                        string bak = path + ".bak";
                        if (File.Exists(bak))
                        {
                            try { File.Delete(bak); } catch { }
                        }
                        File.Move(path, bak);
                    }
                }
            }
            catch (Exception ex)
            {
                CWLog.Error("ClearWinAuditLogger.Rotate", ex);
            }
        }

        private static string GetAuditFilePath()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClearWin", "audit");
            if (!Directory.Exists(dir))
            {
                try { Directory.CreateDirectory(dir); } catch { }
            }
            return Path.Combine(dir, "audit_log.jsonl");
        }

        public static void Log(
            string actionId,
            string tier,
            string requester,
            string adminMode,
            bool preconditionsMet,
            string executionStatus,
            string verificationStatus,
            string details,
            long durationMs)
        {
            var entry = new SecurityAuditEntry
            {
                Id = Guid.NewGuid().ToString("N"),
                Timestamp = DateTime.UtcNow,
                ActionId = actionId ?? "unknown",
                Tier = tier ?? "Unknown",
                Requester = requester ?? "Unknown",
                AdminMode = adminMode ?? "Normal",
                PreconditionsMet = preconditionsMet,
                ExecutionStatus = executionStatus ?? "UNKNOWN",
                VerificationStatus = verificationStatus ?? "UNKNOWN",
                Details = Sanitize(details),
                DurationMs = durationMs
            };

            lock (_recentMemoryLogs)
            {
                _recentMemoryLogs.Add(entry);
                if (_recentMemoryLogs.Count > MAX_MEMORY_LOGS)
                {
                    _recentMemoryLogs.RemoveAt(0);
                }
            }

            try
            {
                string jsonLine = entry.ToJson();
                lock (_fileLock)
                {
                    string path = GetAuditFilePath();
                    RotateIfNeeded(path);
                    File.AppendAllText(path, jsonLine + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                CWLog.Error("ClearWinAuditLogger", ex);
            }
        }

        public static List<SecurityAuditEntry> GetRecentLogs(int limit)
        {
            lock (_recentMemoryLogs)
            {
                int take = Math.Min(limit, _recentMemoryLogs.Count);
                int start = _recentMemoryLogs.Count - take;
                var list = new List<SecurityAuditEntry>(take);
                for (int i = _recentMemoryLogs.Count - 1; i >= start; i--)
                {
                    list.Add(_recentMemoryLogs[i]);
                }
                return list;
            }
        }

        public static string GetRecentLogsJson(int limit)
        {
            var logs = GetRecentLogs(limit);
            var sb = new StringBuilder();
            sb.Append("[");
            for (int i = 0; i < logs.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(logs[i].ToJson());
            }
            sb.Append("]");
            return sb.ToString();
        }

        private static string Sanitize(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            // Redact potential passwords, tokens or private keys if any
            string cleaned = text;
            if (cleaned.IndexOf("token=", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"token=[a-zA-Z0-9_\-]+", "token=[REDACTED]");
            }
            if (cleaned.IndexOf("password=", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"password=[^\s;]+", "password=[REDACTED]");
            }
            return cleaned;
        }
    }
}
