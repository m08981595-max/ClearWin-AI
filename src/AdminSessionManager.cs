using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClearWin
{
    public enum AdminSessionStatus
    {
        Disconnected,
        RequestingElevation,
        Connected,
        CancelledByUser,
        HelperFailed,
        Terminated
    }

    public class HelperResponse
    {
        public bool Success { get; set; }
        public int ExitCode { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
        public long DurationMs { get; set; }
    }

    /// <summary>
    /// Manages the lifecycle, elevation requests, and secure IPC communication
    /// with the dedicated, least-privilege ClearWin.AdminHelper.exe process.
    /// </summary>
    public static class AdminSessionManager
    {
        private static readonly object _sync = new object();
        private static readonly object _pipeLock = new object();
        private static int _elevationInFlight = 0;
        private static AdminSessionStatus _status = AdminSessionStatus.Disconnected;
        private static string _statusMessage = "صلاحيات المستخدم العادية (غير مرتفع)";
        private static Process _helperProcess;
        private static NamedPipeClientStream _pipeClient;
        private static StreamReader _pipeReader;
        private static StreamWriter _pipeWriter;

        public static event Action<AdminSessionStatus, string> StatusChanged;

        public static AdminSessionStatus Status
        {
            get { lock (_sync) return _status; }
        }

        public static string StatusMessage
        {
            get { lock (_sync) return _statusMessage; }
        }

        public static bool IsAdminConnected
        {
            get
            {
                lock (_sync)
                {
                    return _status == AdminSessionStatus.Connected &&
                           _pipeClient != null &&
                           _pipeClient.IsConnected &&
                           _helperProcess != null &&
                           !_helperProcess.HasExited;
                }
            }
        }

        private static void SetStatus(AdminSessionStatus newStatus, string message)
        {
            lock (_sync)
            {
                _status = newStatus;
                _statusMessage = message;
            }
            try
            {
                StatusChanged?.Invoke(newStatus, message);
            }
            catch (Exception ex)
            {
                CWLog.Error("AdminSessionManager.StatusChanged", ex);
            }
        }

        /// <summary>
        /// Requests UAC elevation through the official Windows consent dialog to launch
        /// the dedicated helper process ClearWin.AdminHelper.exe.
        /// </summary>
        public static async Task<bool> RequestAdminElevationAsync()
        {
            if (IsAdminConnected) return true;
            if (Interlocked.CompareExchange(ref _elevationInFlight, 1, 0) != 0)
            {
                return false;
            }

            SetStatus(AdminSessionStatus.RequestingElevation, "جاري طلب موافقة المسؤول عبر نافذة Windows UAC الرسمية...");

            return await Task.Run(() =>
            {
                string pipeName = "ClearWin_Admin_" + Guid.NewGuid().ToString("N");
                string token = GenerateSecureToken();
                int currentPid = Process.GetCurrentProcess().Id;

                string helperPath = ResolveAndValidateHelperExecutablePath();
                if (string.IsNullOrEmpty(helperPath) || !File.Exists(helperPath))
                {
                    Interlocked.Exchange(ref _elevationInFlight, 0);
                    SetStatus(AdminSessionStatus.HelperFailed, "تعذر العثور على ملف ClearWin.AdminHelper.exe أو فشل التحقق من مساره في التطبيق.");
                    return false;
                }

                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = helperPath,
                        Arguments = string.Format("--pipe {0} --token {1} --parent {2}", pipeName, token, currentPid),
                        Verb = "runas", // Triggers the official Windows UAC elevation prompt
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };

                    Process proc = null;
                    try
                    {
                        proc = Process.Start(psi);
                    }
                    catch (Win32Exception w32Ex) when (w32Ex.NativeErrorCode == 1223) // ERROR_CANCELLED (User clicked 'No')
                    {
                        SetStatus(AdminSessionStatus.CancelledByUser, "تم إلغاء طلب صلاحيات المسؤول من المستخدم. التطبيق يستمر في العمل بصلاحيات عادية.");
                        ClearWinAuditLogger.Log(
                            "admin.elevation_request",
                            "Yellow",
                            "User",
                            "Normal",
                            false,
                            "CANCELLED_BY_USER",
                            "VERIFIED",
                            "رفض المستخدم نافذة UAC؛ استمرار التطبيق بامتيازات المستخدم العادي.",
                            0);
                        return false;
                    }

                    if (proc == null)
                    {
                        SetStatus(AdminSessionStatus.HelperFailed, "تعذر تشغيل عملية المساعد الإداري.");
                        return false;
                    }

                    _helperProcess = proc;

                    // Connect to Named Pipe
                    var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                    try
                    {
                        client.Connect(8000); // 8 second timeout to connect
                    }
                    catch (TimeoutException)
                    {
                        SetStatus(AdminSessionStatus.HelperFailed, "انتهت مهلة اتصال المساعد الإداري عبر قناة IPC.");
                        try { proc.Kill(); } catch { }
                        return false;
                    }

                    var reader = new StreamReader(client, Encoding.UTF8);
                    var writer = new StreamWriter(client, Encoding.UTF8) { AutoFlush = true };

                    // Send authentication handshake
                    string handshake = string.Format("{{\"type\":\"handshake\",\"token\":\"{0}\"}}", token);
                    writer.WriteLine(handshake);

                    string response = reader.ReadLine();
                    var handshakeRes = ParseHelperResponse(response, null);
                    if (!handshakeRes.Success || !string.Equals(handshakeRes.Status, "AUTHENTICATED", StringComparison.OrdinalIgnoreCase))
                    {
                        SetStatus(AdminSessionStatus.HelperFailed, "فشل التحقق الأمني من هوية المساعد الإداري.");
                        try { client.Dispose(); } catch { }
                        try { proc.Kill(); } catch { }
                        return false;
                    }

                    lock (_pipeLock)
                    {
                        lock (_sync)
                        {
                            _helperProcess = proc;
                            _pipeClient = client;
                            _pipeReader = reader;
                            _pipeWriter = writer;
                        }
                    }

                    SetStatus(AdminSessionStatus.Connected, "المساعد الإداري متصل ونشط (صلاحيات مسؤول مؤمنة).");

                    ClearWinAuditLogger.Log(
                        "admin.session_started",
                        "Green",
                        "User",
                        "AdminHelper",
                        true,
                        "SUCCESS",
                        "VERIFIED",
                        "تم تفعيل جلسة المساعد الإداري المستقلة بنجاح عبر UAC.",
                        0);

                    return true;
                }
                catch (Exception ex)
                {
                    CWLog.Error("AdminSessionManager.Elevate", ex);
                    SetStatus(AdminSessionStatus.HelperFailed, "حدث خطأ أثناء تشغيل المساعد الإداري: " + ex.Message);
                    return false;
                }
                finally
                {
                    Interlocked.Exchange(ref _elevationInFlight, 0);
                }
            });
        }

        public static void TerminateAdminSession()
        {
            Process procToKill = null;
            StreamWriter writer = null;
            StreamReader reader = null;
            NamedPipeClientStream client = null;

            lock (_sync)
            {
                writer = _pipeWriter;
                reader = _pipeReader;
                client = _pipeClient;
                procToKill = _helperProcess;

                _pipeReader = null;
                _pipeWriter = null;
                _pipeClient = null;
                _helperProcess = null;
            }

            if (writer != null && client != null && client.IsConnected)
            {
                try
                {
                    writer.WriteLine("{\"actionId\":\"session.shutdown\"}");
                }
                catch { }
            }

            try { reader?.Dispose(); } catch { }
            try { writer?.Dispose(); } catch { }
            try { client?.Dispose(); } catch { }

            if (procToKill != null && !procToKill.HasExited)
            {
                try
                {
                    if (!procToKill.WaitForExit(1500))
                    {
                        procToKill.Kill();
                    }
                }
                catch { }
            }

            SetStatus(AdminSessionStatus.Disconnected, "تم إنهاء الجلسة الإدارية وإغلاق المساعد بنجاح.");

            ClearWinAuditLogger.Log(
                "admin.session_terminated",
                "Green",
                "User",
                "Normal",
                true,
                "SUCCESS",
                "VERIFIED",
                "تم إنهاء الجلسة الإدارية وإغلاق عملية المساعد المستقلة.",
                0);
        }

        /// <summary>
        /// Sends a structured, typed request to the elevated helper and waits for response.
        /// Arbitrary command lines are strictly disallowed.
        /// </summary>
        public static HelperResponse ExecutePrivilegedAction(string actionId, string target, int timeoutMs = 130000)
        {
            if (!IsAdminConnected)
            {
                return new HelperResponse
                {
                    Success = false,
                    ExitCode = -1,
                    Status = "NOT_CONNECTED",
                    Error = "المساعد الإداري غير متصل. يرجى تفعيل وضع المسؤول أولاً.",
                    Message = "المساعد الإداري غير متصل. يرجى تفعيل وضع المسؤول أولاً."
                };
            }

            if (timeoutMs <= 0) timeoutMs = 130000;
            string reqId = Guid.NewGuid().ToString("N");
            string requestJson = string.Format(
                "{{\"requestId\":\"{0}\",\"actionId\":\"{1}\",\"target\":\"{2}\"}}",
                reqId,
                Escape(actionId),
                Escape(target));

            // SEC-09: Decouple pipe I/O synchronization from status locking.
            // Status queries from UI thread query _sync and are never blocked during pipe waits (up to 130s).
            lock (_pipeLock)
            {
                StreamWriter writer;
                StreamReader reader;

                lock (_sync)
                {
                    if (_status != AdminSessionStatus.Connected ||
                        _pipeClient == null || !_pipeClient.IsConnected ||
                        _helperProcess == null || _helperProcess.HasExited)
                    {
                        return new HelperResponse
                        {
                            Success = false,
                            ExitCode = -1,
                            Status = "NOT_CONNECTED",
                            Error = "المساعد الإداري غير متصل. يرجى تفعيل وضع المسؤول أولاً.",
                            Message = "المساعد الإداري غير متصل. يرجى تفعيل وضع المسؤول أولاً."
                        };
                    }

                    writer = _pipeWriter;
                    reader = _pipeReader;
                }

                try
                {
                    writer.WriteLine(requestJson);
                    Task<string> readTask = reader.ReadLineAsync();
                    if (!readTask.Wait(timeoutMs))
                    {
                        CWLog.Warn("AdminSessionManager.ExecuteAction", "Timed out waiting for AdminHelper response (action=" + actionId + ", timeoutMs=" + timeoutMs + "). Resetting pipe session.");
                        lock (_sync)
                        {
                            try { _pipeClient?.Dispose(); } catch { }
                            _pipeReader = null;
                            _pipeWriter = null;
                            _pipeClient = null;
                            _status = AdminSessionStatus.HelperFailed;
                            _statusMessage = "انتهت مهلة استجابة المساعد الإداري.";
                        }
                        return new HelperResponse
                        {
                            Success = false,
                            ExitCode = -1,
                            Status = "TIMEOUT",
                            Error = "انتهت مهلة انتظار استجابة المساعد الإداري.",
                            Message = "انتهت مهلة انتظار استجابة المساعد الإداري."
                        };
                    }

                    string responseJson = readTask.Result;
                    if (string.IsNullOrWhiteSpace(responseJson))
                    {
                        return new HelperResponse
                        {
                            Success = false,
                            ExitCode = -1,
                            Status = "EMPTY_RESPONSE",
                            Error = "لم يستجب المساعد الإداري للطلب.",
                            Message = "لم يستجب المساعد الإداري للطلب."
                        };
                    }

                    return ParseHelperResponse(responseJson, reqId);
                }
                catch (Exception ex)
                {
                    CWLog.Error("AdminSessionManager.ExecuteAction", ex);
                    return new HelperResponse
                    {
                        Success = false,
                        ExitCode = -1,
                        Status = "COMMUNICATION_ERROR",
                        Error = ex.Message,
                        Message = ex.Message
                    };
                }
            }
        }

        internal enum JsonTokenKind
        {
            String,
            Primitive,
            Structured
        }

        internal struct JsonFieldToken
        {
            public JsonTokenKind Kind;
            public string Value;
        }

        internal static HelperResponse ParseHelperResponse(string json, string expectedRequestId = null)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new HelperResponse
                {
                    Success = false,
                    ExitCode = -1,
                    Status = "EMPTY_RESPONSE",
                    Error = "Empty response from AdminHelper.",
                    Message = "لم يستجب المساعد الإداري للطلب."
                };
            }

            Dictionary<string, JsonFieldToken> fields;
            string parseError;
            if (!TryParseTopLevelJsonObject(json, out fields, out parseError))
            {
                return new HelperResponse
                {
                    Success = false,
                    ExitCode = -1,
                    Status = "INVALID_RESPONSE",
                    Error = string.IsNullOrEmpty(parseError) ? "Malformed JSON response from AdminHelper." : parseError,
                    Message = "استجابة غير صالحة من المساعد الإداري."
                };
            }

            var res = new HelperResponse();
            string respReqId = GetTopLevelString(fields, "requestId");
            res.Status = GetTopLevelString(fields, "status");
            res.Message = GetTopLevelString(fields, "message");
            res.Output = GetTopLevelStringOrStructured(fields, "output");
            res.Error = GetTopLevelString(fields, "error");

            if (!string.IsNullOrEmpty(expectedRequestId) && !string.Equals(respReqId, expectedRequestId, StringComparison.Ordinal))
            {
                res.Success = false;
                res.ExitCode = -1;
                res.Status = "REQUEST_ID_MISMATCH";
                res.Error = "Mismatched requestId in AdminHelper response.";
                return res;
            }

            bool rawSuccess = GetTopLevelBool(fields, "success", false);

            int parsedExitCode;
            bool hasExitCode = TryGetTopLevelInt(fields, "exitCode", out parsedExitCode);
            res.ExitCode = hasExitCode ? parsedExitCode : (rawSuccess ? 0 : -1);

            long duration;
            if (TryGetTopLevelLong(fields, "durationMs", out duration))
            {
                res.DurationMs = duration;
            }

            // Fail-closed invariants: any non-zero exitCode, missing status, or error/timeout status forces Success = false
            if (hasExitCode && parsedExitCode != 0)
            {
                rawSuccess = false;
            }
            if (string.IsNullOrEmpty(res.Status))
            {
                rawSuccess = false;
                res.Status = string.IsNullOrEmpty(res.Error) ? "MISSING_STATUS" : res.Error;
            }
            else if (res.Status.IndexOf("FAIL", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     res.Status.IndexOf("ERROR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     res.Status.IndexOf("TIMEOUT", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     res.Status.StartsWith("RESTORE_POINT_EXIT_", StringComparison.OrdinalIgnoreCase) ||
                     res.Status.StartsWith("POLICY_VIOLATION", StringComparison.OrdinalIgnoreCase) ||
                     res.Status.StartsWith("MISSING_", StringComparison.OrdinalIgnoreCase) ||
                     res.Status.StartsWith("INVALID_", StringComparison.OrdinalIgnoreCase) ||
                     res.Status.StartsWith("BLOCKED_", StringComparison.OrdinalIgnoreCase))
            {
                rawSuccess = false;
            }

            res.Success = rawSuccess;
            return res;
        }

        internal static string ExtractTopLevelJsonString(string json, string key)
        {
            Dictionary<string, JsonFieldToken> fields;
            string err;
            if (!TryParseTopLevelJsonObject(json, out fields, out err)) return "";
            return GetTopLevelString(fields, key);
        }

        internal static int ExtractTopLevelJsonInt(string json, string key, int defaultValue = 0)
        {
            Dictionary<string, JsonFieldToken> fields;
            string err;
            if (!TryParseTopLevelJsonObject(json, out fields, out err)) return defaultValue;
            int val;
            return TryGetTopLevelInt(fields, key, out val) ? val : defaultValue;
        }

        private static string GetTopLevelString(Dictionary<string, JsonFieldToken> fields, string key)
        {
            JsonFieldToken tok;
            if (fields != null && fields.TryGetValue(key, out tok))
            {
                if (tok.Kind == JsonTokenKind.String) return tok.Value ?? "";
                if (tok.Kind == JsonTokenKind.Primitive && !string.Equals(tok.Value, "null", StringComparison.OrdinalIgnoreCase))
                    return tok.Value ?? "";
            }
            return "";
        }

        private static string GetTopLevelStringOrStructured(Dictionary<string, JsonFieldToken> fields, string key)
        {
            JsonFieldToken tok;
            if (fields != null && fields.TryGetValue(key, out tok))
            {
                if (tok.Kind == JsonTokenKind.Primitive && string.Equals(tok.Value, "null", StringComparison.OrdinalIgnoreCase))
                    return "";
                return tok.Value ?? "";
            }
            return "";
        }

        private static bool GetTopLevelBool(Dictionary<string, JsonFieldToken> fields, string key, bool defaultValue)
        {
            JsonFieldToken tok;
            if (fields != null && fields.TryGetValue(key, out tok) && tok.Kind == JsonTokenKind.Primitive)
            {
                if (string.Equals(tok.Value, "true", StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(tok.Value, "false", StringComparison.OrdinalIgnoreCase)) return false;
            }
            return defaultValue;
        }

        private static bool TryGetTopLevelInt(Dictionary<string, JsonFieldToken> fields, string key, out int value)
        {
            value = 0;
            JsonFieldToken tok;
            if (fields != null && fields.TryGetValue(key, out tok) &&
                (tok.Kind == JsonTokenKind.Primitive || tok.Kind == JsonTokenKind.String))
            {
                return int.TryParse(tok.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            }
            return false;
        }

        private static bool TryGetTopLevelLong(Dictionary<string, JsonFieldToken> fields, string key, out long value)
        {
            value = 0;
            JsonFieldToken tok;
            if (fields != null && fields.TryGetValue(key, out tok) &&
                (tok.Kind == JsonTokenKind.Primitive || tok.Kind == JsonTokenKind.String))
            {
                return long.TryParse(tok.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
            }
            return false;
        }

        /// <summary>
        /// Strictly parses a top-level JSON object while tracking '{' / '}' and '[' / ']' nesting depth
        /// and string escaping. Nested objects/arrays (depth > 1) are isolated as Structured tokens so
        /// inner keys (e.g. "output": {"success": true}) can never spoof top-level fields.
        /// Duplicate top-level keys and malformed syntax are rejected fail-closed.
        /// </summary>
        internal static bool TryParseTopLevelJsonObject(
            string json,
            out Dictionary<string, JsonFieldToken> fields,
            out string error)
        {
            fields = new Dictionary<string, JsonFieldToken>(StringComparer.OrdinalIgnoreCase);
            error = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Empty JSON input.";
                return false;
            }

            int n = json.Length;
            int i = 0;
            SkipWhitespace(json, ref i, n);
            if (i >= n || json[i] != '{')
            {
                error = "JSON does not start with '{'.";
                return false;
            }
            i++; // consume opening '{'

            SkipWhitespace(json, ref i, n);
            if (i < n && json[i] == '}')
            {
                i++;
                SkipWhitespace(json, ref i, n);
                if (i != n)
                {
                    error = "Trailing characters after root JSON object.";
                    return false;
                }
                return true;
            }

            while (i < n)
            {
                SkipWhitespace(json, ref i, n);
                if (i >= n || json[i] != '"')
                {
                    error = "Expected quoted property name at top level.";
                    return false;
                }

                string key;
                if (!TryReadJsonStringLiteral(json, ref i, n, out key))
                {
                    error = "Unterminated property key string literal.";
                    return false;
                }

                SkipWhitespace(json, ref i, n);
                if (i >= n || json[i] != ':')
                {
                    error = "Expected ':' after property name '" + key + "'.";
                    return false;
                }
                i++; // consume ':'

                SkipWhitespace(json, ref i, n);
                if (i >= n)
                {
                    error = "Unexpected end of JSON after ':' for key '" + key + "'.";
                    return false;
                }

                JsonFieldToken token;
                char valChar = json[i];
                if (valChar == '"')
                {
                    string strVal;
                    if (!TryReadJsonStringLiteral(json, ref i, n, out strVal))
                    {
                        error = "Unterminated string value for key '" + key + "'.";
                        return false;
                    }
                    token = new JsonFieldToken { Kind = JsonTokenKind.String, Value = strVal };
                }
                else if (valChar == '{' || valChar == '[')
                {
                    string rawStructured;
                    if (!TrySkipStructuredJsonValue(json, ref i, n, out rawStructured))
                    {
                        error = "Malformed nested object/array for key '" + key + "'.";
                        return false;
                    }
                    token = new JsonFieldToken { Kind = JsonTokenKind.Structured, Value = rawStructured };
                }
                else
                {
                    int start = i;
                    while (i < n && json[i] != ',' && json[i] != '}' &&
                           json[i] != ' ' && json[i] != '\t' && json[i] != '\r' && json[i] != '\n')
                    {
                        if (json[i] == '"' || json[i] == '{' || json[i] == '[' || json[i] == ']')
                        {
                            error = "Invalid character in primitive token for key '" + key + "'.";
                            return false;
                        }
                        i++;
                    }
                    if (i == start)
                    {
                        error = "Empty value token for key '" + key + "'.";
                        return false;
                    }
                    string rawPrim = json.Substring(start, i - start);
                    token = new JsonFieldToken { Kind = JsonTokenKind.Primitive, Value = rawPrim };
                }

                if (fields.ContainsKey(key))
                {
                    error = "Duplicate top-level JSON key: " + key;
                    return false;
                }
                fields[key] = token;

                SkipWhitespace(json, ref i, n);
                if (i < n && json[i] == ',')
                {
                    i++;
                    SkipWhitespace(json, ref i, n);
                    if (i >= n || json[i] == '}')
                    {
                        error = "Trailing comma in top-level JSON object.";
                        return false;
                    }
                    continue;
                }
                else if (i < n && json[i] == '}')
                {
                    i++; // consume closing '}'
                    SkipWhitespace(json, ref i, n);
                    if (i != n)
                    {
                        error = "Trailing characters after closing '}'.";
                        return false;
                    }
                    return true;
                }
                else
                {
                    error = "Expected ',' or '}' after property value for key '" + key + "'.";
                    return false;
                }
            }

            error = "Unterminated top-level JSON object.";
            return false;
        }

        private static void SkipWhitespace(string s, ref int i, int n)
        {
            while (i < n && (s[i] == ' ' || s[i] == '\t' || s[i] == '\r' || s[i] == '\n'))
            {
                i++;
            }
        }

        private static bool TryReadJsonStringLiteral(string s, ref int i, int n, out string result)
        {
            result = null;
            if (i >= n || s[i] != '"') return false;
            i++; // consume opening '"'
            var sb = new StringBuilder();
            while (i < n)
            {
                char c = s[i];
                if (c == '\\')
                {
                    if (i + 1 >= n) return false;
                    char esc = s[i + 1];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); i += 2; break;
                        case '\\': sb.Append('\\'); i += 2; break;
                        case '/': sb.Append('/'); i += 2; break;
                        case 'b': sb.Append('\b'); i += 2; break;
                        case 'f': sb.Append('\f'); i += 2; break;
                        case 'n': sb.Append('\n'); i += 2; break;
                        case 'r': sb.Append('\r'); i += 2; break;
                        case 't': sb.Append('\t'); i += 2; break;
                        case 'u':
                            if (i + 5 >= n) return false;
                            string hex = s.Substring(i + 2, 4);
                            int codePoint;
                            if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out codePoint))
                                return false;
                            sb.Append((char)codePoint);
                            i += 6;
                            break;
                        default:
                            sb.Append(esc);
                            i += 2;
                            break;
                    }
                }
                else if (c == '"')
                {
                    i++; // consume closing '"'
                    result = sb.ToString();
                    return true;
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }
            return false; // unclosed quote
        }

        private static bool TrySkipStructuredJsonValue(string s, ref int i, int n, out string rawSlice)
        {
            rawSlice = null;
            int start = i;
            int braceDepth = 0;
            int bracketDepth = 0;

            while (i < n)
            {
                char c = s[i];
                if (c == '"')
                {
                    string dummy;
                    if (!TryReadJsonStringLiteral(s, ref i, n, out dummy))
                        return false;
                    continue;
                }
                if (c == '{')
                {
                    braceDepth++;
                }
                else if (c == '}')
                {
                    braceDepth--;
                    if (braceDepth < 0) return false;
                }
                else if (c == '[')
                {
                    bracketDepth++;
                }
                else if (c == ']')
                {
                    bracketDepth--;
                    if (bracketDepth < 0) return false;
                }
                i++;

                if (braceDepth == 0 && bracketDepth == 0)
                {
                    rawSlice = s.Substring(start, i - start);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Validates that the helper executable exists and resides strictly within the application directory.
        /// Prevents path traversal, external binary spoofing, and symlink hijacking (SEC-01 & SEC-07).
        /// </summary>
        public static string ResolveAndValidateHelperExecutablePath()
        {
            try
            {
                string baseDir = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
                if (!baseDir.EndsWith(Path.DirectorySeparatorChar.ToString()))
                {
                    baseDir += Path.DirectorySeparatorChar;
                }

                string primary = Path.GetFullPath(Path.Combine(baseDir, "ClearWin.AdminHelper.exe"));
                string inBin = Path.GetFullPath(Path.Combine(baseDir, "bin", "ClearWin.AdminHelper.exe"));

                string resolved = null;
                if (File.Exists(primary))
                {
                    resolved = primary;
                }
                else if (File.Exists(inBin))
                {
                    resolved = inBin;
                }

                if (resolved == null)
                {
                    CWLog.Warn("AdminSessionManager.ResolveHelper", "ClearWin.AdminHelper.exe not found in application path: " + baseDir);
                    return null;
                }

                // SEC-01: Ensure canonical path resides strictly inside application base directory
                if (!resolved.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
                {
                    CWLog.Warn("AdminSessionManager.ResolveHelper", "Helper path escapes application directory: " + resolved);
                    return null;
                }

                // Verify exact executable file name
                string fileName = Path.GetFileName(resolved);
                if (!string.Equals(fileName, "ClearWin.AdminHelper.exe", StringComparison.OrdinalIgnoreCase))
                {
                    CWLog.Warn("AdminSessionManager.ResolveHelper", "Invalid helper file name: " + fileName);
                    return null;
                }

                // Verify file is not a symlink/reparse point
                var fileInfo = new FileInfo(resolved);
                if ((fileInfo.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    CWLog.Warn("AdminSessionManager.ResolveHelper", "Helper executable is a reparse point (possible symlink hijacking): " + resolved);
                    return null;
                }

                return resolved;
            }
            catch (Exception ex)
            {
                CWLog.Error("AdminSessionManager.ResolveHelper", ex);
                return null;
            }
        }

        private static string ResolveHelperExecutablePath()
        {
            return ResolveAndValidateHelperExecutablePath();
        }

        private static string GenerateSecureToken()
        {
            using (var rng = new RNGCryptoServiceProvider())
            {
                byte[] bytes = new byte[32];
                rng.GetBytes(bytes);
                var sb = new StringBuilder(64);
                for (int i = 0; i < bytes.Length; i++)
                {
                    sb.Append(bytes[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
