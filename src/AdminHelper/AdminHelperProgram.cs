using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace ClearWin.AdminHelper
{
    public static class AdminHelperProgram
    {
        [System.Runtime.InteropServices.DllImport("ntdll.dll")]
        private static extern uint NtSetSystemInformation(int InfoClass, IntPtr Info, int Length);

        [System.Runtime.InteropServices.DllImport("ntdll.dll")]
        private static extern int RtlAdjustPrivilege(int Privilege, bool Enable, bool CurrentThread, out bool Enabled);

        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetNamedPipeClientProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle Pipe, out uint ClientProcessId);

        private const int SystemMemoryListInformation = 80;

        private static volatile bool _running = true;
        private static DateTime _lastActivity = DateTime.UtcNow;
        private static readonly TimeSpan IDLE_TIMEOUT = TimeSpan.FromMinutes(30);

        private static readonly System.Collections.Generic.HashSet<string> _knownTweaks =
            new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "align-taskbar-left",
            "debloat-windows",
            "detailed-bsod",
            "disable-background-ms-store-apps",
            "disable-copilot",
            "disable-core-isolation",
            "disable-defender-rtp",
            "disable-dynamic-ticking",
            "disable-fast-startup",
            "disable-gamebar",
            "disable-hibernation",
            "disable-hpet",
            "disable-location-tracking",
            "disable-lockscreen-tips",
            "disable-mouse-acceleration",
            "disable-wifi-sense",
            "enable-dark-mode",
            "enable-end-task-right-click",
            "optimize-network-settings",
            "optimize-nvidia-settings",
            "remove-gaming-apps",
            "remove-onedrive",
            "revert-context-menu",
            "run-disk-cleanup",
            "set-services-to-manual",
            "set-time-utc",
            "ultimate-performance-plan"
        };

        public static int Main(string[] args)
        {
            string pipeName = null;
            string expectedToken = null;
            int parentPid = -1;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals("--pipe", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    pipeName = args[++i];
                }
                else if (args[i].Equals("--token", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    expectedToken = args[++i];
                }
                else if (args[i].Equals("--parent", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    int.TryParse(args[++i], out parentPid);
                }
            }

            if (string.IsNullOrEmpty(pipeName) || string.IsNullOrEmpty(expectedToken))
            {
                Console.Error.WriteLine("Error: Missing required security arguments --pipe and --token.");
                return 1;
            }

            // Security hardening: clear any inherited test/sandbox override environment variables
            try
            {
                Environment.SetEnvironmentVariable("CLEARWIN_ALLOW_DEFERRED_VM_TWEAKS", null);
                Environment.SetEnvironmentVariable("CLEARWIN_TEST_MODE", null);
                Environment.SetEnvironmentVariable("CLEARWIN_TEST_REG_PREFIX", null);
                Environment.SetEnvironmentVariable("CLEARWIN_TEST_BACKUP_DIR", null);
            }
            catch { }

            // Monitor parent process if specified: auto-exit when parent terminates
            if (parentPid > 0)
            {
                StartParentWatchdog(parentPid);
            }

            // Start idle timeout watchdog
            StartIdleWatchdog();

            try
            {
                RunIpcServer(pipeName, expectedToken, parentPid);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Helper fatal error: " + ex.Message);
                return 2;
            }
        }

        private static void StartParentWatchdog(int parentPid)
        {
            var t = new Thread(() =>
            {
                try
                {
                    var parent = Process.GetProcessById(parentPid);
                    parent.WaitForExit();
                }
                catch { }
                finally
                {
                    _running = false;
                    Environment.Exit(0);
                }
            })
            {
                IsBackground = true,
                Name = "ParentProcessWatchdog"
            };
            t.Start();
        }

        private static void StartIdleWatchdog()
        {
            var t = new Thread(() =>
            {
                while (_running)
                {
                    Thread.Sleep(10000);
                    if (DateTime.UtcNow - _lastActivity > IDLE_TIMEOUT)
                    {
                        _running = false;
                        Environment.Exit(0);
                    }
                }
            })
            {
                IsBackground = true,
                Name = "IdleWatchdog"
            };
            t.Start();
        }

        private static void RunIpcServer(string pipeName, string expectedToken, int expectedParentPid)
        {
            // Set up restrictive PipeSecurity: Current User and Administrators only
            var pipeSecurity = new PipeSecurity();
            var currentUser = WindowsIdentity.GetCurrent().User;
            if (currentUser != null)
            {
                pipeSecurity.AddAccessRule(new PipeAccessRule(
                    currentUser,
                    PipeAccessRights.ReadWrite,
                    AccessControlType.Allow));
            }
            var adminSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
            pipeSecurity.AddAccessRule(new PipeAccessRule(
                adminSid,
                PipeAccessRights.FullControl,
                AccessControlType.Allow));

            while (_running)
            {
                NamedPipeServerStream server = null;
                try
                {
                    server = new NamedPipeServerStream(
                        pipeName,
                        PipeDirection.InOut,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous,
                        8192,
                        8192,
                        pipeSecurity);

                    server.WaitForConnection();
                    _lastActivity = DateTime.UtcNow;

                    // SEC-01 & SEC-07: Verify connecting client's process ID matches expected parent PID
                    if (expectedParentPid > 0)
                    {
                        uint clientPid;
                        if (!GetNamedPipeClientProcessId(server.SafePipeHandle, out clientPid) || clientPid != (uint)expectedParentPid)
                        {
                            Console.Error.WriteLine(string.Format("Security Alert: Connection rejected. Client PID {0} does not match expected parent PID {1}.", clientPid, expectedParentPid));
                            try
                            {
                                using (var errWriter = new StreamWriter(server, Encoding.UTF8) { AutoFlush = true })
                                {
                                    errWriter.WriteLine("{\"success\":false,\"error\":\"CLIENT_PID_MISMATCH\",\"message\":\"Unauthorized client process.\"}");
                                }
                            }
                            catch { }
                            try { server.Disconnect(); } catch { }
                            continue;
                        }
                    }

                    using (var reader = new StreamReader(server, Encoding.UTF8))
                    using (var writer = new StreamWriter(server, Encoding.UTF8) { AutoFlush = true })
                    {
                        // 1. Handshake verification within 5 seconds
                        string handshakeLine = ReadLineWithTimeout(reader, 5000);
                        if (string.IsNullOrEmpty(handshakeLine) || !ValidateHandshake(handshakeLine, expectedToken))
                        {
                            writer.WriteLine("{\"success\":false,\"error\":\"AUTHENTICATION_FAILED\"}");
                            server.Disconnect();
                            continue;
                        }

                        writer.WriteLine("{\"success\":true,\"status\":\"AUTHENTICATED\",\"message\":\"ClearWin Admin Helper Ready\"}");

                        // 2. Command processing loop
                        while (_running && server.IsConnected)
                        {
                            string requestJson = reader.ReadLine();
                            if (requestJson == null) break; // Client disconnected
                            _lastActivity = DateTime.UtcNow;

                            string responseJson = ProcessPrivilegedRequest(requestJson);
                            writer.WriteLine(responseJson);

                            if (requestJson.IndexOf("\"session.shutdown\"", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                _running = false;
                                break;
                            }
                        }
                    }
                }
                catch (IOException) { }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("IPC Error: " + ex.Message);
                }
                finally
                {
                    if (server != null)
                    {
                        try { server.Dispose(); } catch { }
                    }
                }

                if (!_running) break;
            }
        }

        private static string ReadLineWithTimeout(StreamReader reader, int timeoutMs)
        {
            var task = reader.ReadLineAsync();
            if (task.Wait(timeoutMs))
            {
                return task.Result;
            }
            return null;
        }

        public static bool ValidateHandshake(string line, string expectedToken)
        {
            if (string.IsNullOrEmpty(line)) return false;
            string token = ExtractJsonField(line, "token");
            return !string.IsNullOrEmpty(token) && string.Equals(token, expectedToken, StringComparison.Ordinal);
        }

        /// <summary>
        /// Validates and executes privileged actions against the Helper's own immutable security gate.
        /// Rejects arbitrary shell commands, invalid paths, and unapproved ActionIds.
        /// </summary>
        public static string ProcessPrivilegedRequest(string json)
        {
            string reqId = ExtractJsonField(json, "requestId");
            string actionId = ExtractJsonField(json, "actionId");
            string target = ExtractJsonField(json, "target");

            if (string.IsNullOrEmpty(actionId))
            {
                return BuildErrorResponse(reqId, "MISSING_ACTION_ID", "actionId field is required.");
            }

            // Security Gate 1: Check against Helper Allowlist
            switch (actionId.ToLowerInvariant())
            {
                case "ping":
                    return BuildSuccessResponse(reqId, "PONG", "ClearWin Admin Helper is connected and active.", null);

                case "session.shutdown":
                    return BuildSuccessResponse(reqId, "SHUTTING_DOWN", "Admin helper session closing gracefully.", null);

                case "system.flush_dns":
                    return ExecuteDnsFlush(reqId);

                case "system.purge_standby":
                    return ExecutePurgeStandby(reqId);

                case "system.create_restore_point":
                    return ExecuteCreateRestorePoint(reqId, target);

                case "tweak.apply":
                    return ExecuteTweak(reqId, target, "apply.ps1");

                case "tweak.undo":
                    return ExecuteTweak(reqId, target, "undo.ps1");

                default:
                    return BuildErrorResponse(reqId, "POLICY_VIOLATION_UNKNOWN_ACTION",
                        "Action '" + actionId + "' is not approved in the Admin Helper privileged allowlist.");
            }
        }

        private static string ExecuteDnsFlush(string reqId)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "ipconfig.exe",
                    Arguments = "/flushdns",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var p = Process.Start(psi))
                {
                    if (p == null)
                    {
                        return BuildErrorResponse(reqId, "DNS_FLUSH_START_FAILED", "Failed to start ipconfig.exe.");
                    }
                    var sbOut = new StringBuilder();
                    var sbErr = new StringBuilder();
                    p.OutputDataReceived += (s, e) => { if (e.Data != null) sbOut.AppendLine(e.Data); };
                    p.ErrorDataReceived += (s, e) => { if (e.Data != null) sbErr.AppendLine(e.Data); };
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();

                    if (!p.WaitForExit(10000))
                    {
                        try { p.Kill(); } catch { }
                        return BuildErrorResponse(reqId, "DNS_FLUSH_TIMEOUT", "ipconfig /flushdns timed out.");
                    }
                    p.WaitForExit();
                    if (p.ExitCode != 0)
                    {
                        string errDetail = sbErr.ToString().Trim();
                        if (string.IsNullOrEmpty(errDetail)) errDetail = sbOut.ToString().Trim();
                        if (string.IsNullOrEmpty(errDetail)) errDetail = "ipconfig exited with code " + p.ExitCode;
                        return BuildErrorResponse(reqId, "DNS_FLUSH_FAILED", errDetail);
                    }
                    return BuildSuccessResponse(reqId, "SUCCESS", "DNS cache flushed and verified (exit code 0).", null);
                }
            }
            catch (Exception ex)
            {
                return BuildErrorResponse(reqId, "DNS_FLUSH_FAILED", ex.Message);
            }
        }

        private static string ExecutePurgeStandby(string reqId)
        {
            IntPtr p = IntPtr.Zero;
            try
            {
                bool prev;
                RtlAdjustPrivilege(19, true, false, out prev); // SeProfileSingleProcessPrivilege / Quota

                try
                {
                    IntPtr pFlush = System.Runtime.InteropServices.Marshal.AllocHGlobal(4);
                    System.Runtime.InteropServices.Marshal.WriteInt32(pFlush, 3); // MemoryFlushModifiedList
                    NtSetSystemInformation(SystemMemoryListInformation, pFlush, 4);
                    System.Runtime.InteropServices.Marshal.FreeHGlobal(pFlush);
                }
                catch { }

                p = System.Runtime.InteropServices.Marshal.AllocHGlobal(4);
                System.Runtime.InteropServices.Marshal.WriteInt32(p, 4); // MemoryPurgeStandbyList
                uint status = NtSetSystemInformation(SystemMemoryListInformation, p, 4);
                if (status == 0)
                {
                    return BuildSuccessResponse(reqId, "SUCCESS", "Standby memory list purged via NtSetSystemInformation.", null);
                }
                return BuildErrorResponse(reqId, "STANDBY_PURGE_NTSTATUS_" + status.ToString("X"), "NtSetSystemInformation returned status 0x" + status.ToString("X"));
            }
            catch (Exception ex)
            {
                return BuildErrorResponse(reqId, "STANDBY_PURGE_FAILED", ex.Message);
            }
            finally
            {
                if (p != IntPtr.Zero) System.Runtime.InteropServices.Marshal.FreeHGlobal(p);
            }
        }

        private static string ExecuteCreateRestorePoint(string reqId, string description)
        {
            string desc = string.IsNullOrEmpty(description) ? "ClearWin Restore Point" : description;
            desc = Regex.Replace(desc, @"[^a-zA-Z0-9\s_\-\.]", ""); // sanitize
            if (string.IsNullOrWhiteSpace(desc)) desc = "ClearWin Restore Point";

            string marker = Path.Combine(Path.GetTempPath(), "clearwin_admin_rp_" + Guid.NewGuid().ToString("N") + ".txt");
            string escapedMarker = marker.Replace("'", "''");
            string psCommand = string.Format(
                "$ErrorActionPreference = 'Stop'; try {{ Checkpoint-Computer -Description '{0}' -RestorePointType 'MODIFY_SETTINGS' -ErrorAction Stop; 'OK' | Set-Content -LiteralPath '{1}' -Encoding UTF8; exit 0 }} catch {{ $_.Exception.Message | Set-Content -LiteralPath '{1}' -Encoding UTF8; exit 1 }}",
                desc, escapedMarker);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-ExecutionPolicy Bypass -NoProfile -NonInteractive -Command \"" + psCommand.Replace("\"", "\\\"") + "\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var p = Process.Start(psi))
                {
                    if (p == null)
                    {
                        return BuildErrorResponse(reqId, "RESTORE_POINT_START_FAILED", "Failed to start powershell.exe for Checkpoint-Computer.");
                    }
                    var sbOut = new StringBuilder();
                    var sbErr = new StringBuilder();
                    p.OutputDataReceived += (s, e) => { if (e.Data != null) sbOut.AppendLine(e.Data); };
                    p.ErrorDataReceived += (s, e) => { if (e.Data != null) sbErr.AppendLine(e.Data); };
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();

                    if (!p.WaitForExit(120000))
                    {
                        try { p.Kill(); } catch { }
                        return BuildErrorResponse(reqId, "RESTORE_POINT_TIMEOUT", "Checkpoint-Computer timed out after 120s.");
                    }
                    p.WaitForExit();

                    string markerResult = File.Exists(marker) ? File.ReadAllText(marker, Encoding.UTF8).Trim() : "";
                    if (p.ExitCode != 0 || !string.Equals(markerResult, "OK", StringComparison.Ordinal))
                    {
                        string errDetail = (!string.IsNullOrWhiteSpace(markerResult) && markerResult != "OK")
                            ? markerResult
                            : (!string.IsNullOrWhiteSpace(sbErr.ToString())
                                ? sbErr.ToString().Trim()
                                : ("Checkpoint-Computer exited with code " + p.ExitCode + " without verification marker."));
                        return BuildErrorResponse(reqId, "RESTORE_POINT_EXIT_" + p.ExitCode, errDetail);
                    }
                    return BuildSuccessResponse(reqId, "SUCCESS", "Restore point created and verified (" + desc + ").", null);
                }
            }
            catch (Exception ex)
            {
                return BuildErrorResponse(reqId, "RESTORE_POINT_FAILED", ex.Message);
            }
            finally
            {
                try { if (File.Exists(marker)) File.Delete(marker); } catch { }
            }
        }

        private static string ExecuteTweak(string reqId, string target, string scriptName)
        {
            // SEC-02: Strictly validate scriptName
            if (!string.Equals(scriptName, "apply.ps1", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(scriptName, "undo.ps1", StringComparison.OrdinalIgnoreCase))
            {
                return BuildErrorResponse(reqId, "INVALID_SCRIPT_NAME", "Only apply.ps1 and undo.ps1 scripts may be executed.");
            }

            if (string.IsNullOrEmpty(target) || !Regex.IsMatch(target, @"^[a-zA-Z0-9\-]+$") || target.Length > 64)
            {
                return BuildErrorResponse(reqId, "INVALID_TARGET", "Target tweak name contains invalid characters or exceeds maximum length.");
            }

            // SEC-02: Verify target is part of known tweak structure
            if (!_knownTweaks.Contains(target))
            {
                return BuildErrorResponse(reqId, "UNKNOWN_TWEAK_TARGET", "Target tweak '" + target + "' is not recognized in the approved tweaks catalog.");
            }

            // Safety Gate: Block deferred or permanently destructive tweaks on apply.ps1
            if (string.Equals(scriptName, "apply.ps1", StringComparison.OrdinalIgnoreCase))
            {
                string tLower = target.ToLowerInvariant();
                if (tLower == "disable-core-isolation" ||
                    tLower == "set-services-manual" || tLower == "set-services-to-manual" ||
                    tLower == "optimize-network" || tLower == "optimize-network-settings" ||
                    tLower == "remove-onedrive" || tLower == "remove-gaming-apps")
                {
                    return BuildErrorResponse(reqId, "BLOCKED_DEFERRED_HIGH_RISK_TWEAK",
                        "This high-risk or irreversible tweak ('" + target + "') is disabled by default for Windows stability protection.");
                }
            }

            // Path containment and anti-hijacking security checks (SEC-02):
            // 1. Locate known Resources\tweaks directory relative to application base directory
            string baseDir = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);
            string tweaksDir = Path.GetFullPath(Path.Combine(baseDir, "Resources", "tweaks"));
            if (!Directory.Exists(tweaksDir))
            {
                string parentTweaks = Path.GetFullPath(Path.Combine(baseDir, "..", "Resources", "tweaks"));
                if (Directory.Exists(parentTweaks))
                {
                    tweaksDir = parentTweaks;
                }
            }

            if (!Directory.Exists(tweaksDir))
            {
                return BuildErrorResponse(reqId, "TWEAKS_DIRECTORY_NOT_FOUND", "Known Resources\\tweaks structure not found.");
            }

            string canonicalTweaksDir = tweaksDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            // 2. Validate target directory containment and ensure it's not a reparse point (symlink/junction)
            string targetDir = Path.GetFullPath(Path.Combine(canonicalTweaksDir, target));
            string canonicalTargetDir = targetDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (!canonicalTargetDir.StartsWith(canonicalTweaksDir, StringComparison.OrdinalIgnoreCase))
            {
                return BuildErrorResponse(reqId, "PATH_TRAVERSAL_DETECTED", "Tweak directory escapes Resources\\tweaks.");
            }

            var dirInfo = new DirectoryInfo(targetDir);
            if (!dirInfo.Exists)
            {
                return BuildErrorResponse(reqId, "TARGET_DIRECTORY_NOT_FOUND", "Tweak directory does not exist: " + target);
            }

            if ((dirInfo.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return BuildErrorResponse(reqId, "REPARSE_POINT_DETECTED", "Target tweak directory is a reparse point (symlink hijack prevented).");
            }

            // 3. Validate script file containment and ensure it's not a reparse point
            string scriptPath = Path.GetFullPath(Path.Combine(targetDir, scriptName));

            if (!scriptPath.StartsWith(canonicalTargetDir, StringComparison.OrdinalIgnoreCase))
            {
                return BuildErrorResponse(reqId, "PATH_TRAVERSAL_DETECTED", "Script path escapes target directory.");
            }

            var fileInfo = new FileInfo(scriptPath);
            if (!fileInfo.Exists)
            {
                return BuildErrorResponse(reqId, "SCRIPT_NOT_FOUND", "Tweak script not found: " + target + "/" + scriptName);
            }

            if ((fileInfo.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                return BuildErrorResponse(reqId, "REPARSE_POINT_DETECTED", "Tweak script file is a reparse point (symlink hijack prevented).");
            }

            var sw = Stopwatch.StartNew();
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = string.Format("-ExecutionPolicy Bypass -NoProfile -NonInteractive -WindowStyle Hidden -File \"{0}\"", scriptPath),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var p = Process.Start(psi))
                {
                    var sbOut = new StringBuilder();
                    var sbErr = new StringBuilder();
                    p.OutputDataReceived += (s, e) => { if (e.Data != null) sbOut.AppendLine(e.Data); };
                    p.ErrorDataReceived += (s, e) => { if (e.Data != null) sbErr.AppendLine(e.Data); };
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();

                    if (!p.WaitForExit(120000))
                    {
                        try { p.Kill(); } catch { }
                        return BuildErrorResponse(reqId, "TIMEOUT", "Tweak execution timed out after 120s.");
                    }
                    p.WaitForExit();
                    sw.Stop();

                    bool ok = (p.ExitCode == 0);
                    string output = sbOut.ToString().Trim();
                    string error = sbErr.ToString().Trim();

                    return string.Format(
                        "{{\"requestId\":\"{0}\",\"success\":{1},\"exitCode\":{2},\"status\":\"{3}\",\"durationMs\":{4},\"output\":\"{5}\",\"error\":\"{6}\"}}",
                        Escape(reqId),
                        ok ? "true" : "false",
                        p.ExitCode,
                        ok ? "VERIFIED_SUCCESS" : "EXECUTION_FAILED",
                        sw.ElapsedMilliseconds,
                        Escape(output),
                        Escape(error));
                }
            }
            catch (Exception ex)
            {
                return BuildErrorResponse(reqId, "EXECUTION_EXCEPTION", ex.Message);
            }
        }

        public static string ExtractJsonField(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return "";
            try
            {
                int kIdx = json.IndexOf("\"" + key + "\"", StringComparison.OrdinalIgnoreCase);
                if (kIdx < 0) return "";
                int colon = json.IndexOf(':', kIdx);
                if (colon < 0) return "";
                int q1 = json.IndexOf('"', colon);
                if (q1 < 0) return "";
                var sb = new StringBuilder();
                for (int i = q1 + 1; i < json.Length; i++)
                {
                    char c = json[i];
                    if (c == '\\' && i + 1 < json.Length)
                    {
                        char next = json[++i];
                        if (next == '"' || next == '\\' || next == '/') sb.Append(next);
                        else if (next == 'n') sb.Append('\n');
                        else if (next == 'r') sb.Append('\r');
                        else if (next == 't') sb.Append('\t');
                        else sb.Append(next);
                    }
                    else if (c == '"')
                    {
                        return sb.ToString();
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                return "";
            }
            catch { return ""; }
        }

        private static string BuildSuccessResponse(string reqId, string status, string message, string dataJson)
        {
            return string.Format(
                "{{\"requestId\":\"{0}\",\"success\":true,\"status\":\"{1}\",\"message\":\"{2}\"{3}}}",
                Escape(reqId),
                Escape(status),
                Escape(message),
                string.IsNullOrEmpty(dataJson) ? "" : ",\"data\":" + dataJson);
        }

        private static string BuildErrorResponse(string reqId, string errorCode, string message)
        {
            return string.Format(
                "{{\"requestId\":\"{0}\",\"success\":false,\"status\":\"{1}\",\"error\":\"{1}\",\"message\":\"{2}\"}}",
                Escape(reqId),
                Escape(errorCode),
                Escape(message));
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ");
        }
    }
}
