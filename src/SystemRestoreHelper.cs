using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace ClearWin
{
    /// <summary>Outcome of a System Restore point creation attempt.</summary>
    internal enum RestorePointOutcome
    {
        Verified = 0,
        AlreadyFresh,
        DeclinedByUser,
        NotElevated,
        TimedOut,
        Failed
    }

    /// <summary>Result returned by <see cref="SystemRestoreHelper"/>.</summary>
    internal sealed class RestorePointResult
    {
        internal bool Success;
        internal RestorePointOutcome Outcome = RestorePointOutcome.Failed;
        internal string Message = "";
        internal long SequenceBefore;
        internal long SequenceAfter;
        internal int DurationMs;
        internal string RawDetails = "";

        internal static RestorePointResult Fail(RestorePointOutcome outcome, string message)
        {
            return new RestorePointResult { Success = false, Outcome = outcome, Message = message };
        }
    }

    /// <summary>
    /// شبكة الأمان قبل أي تعديل للنظام - نقطة استعادة النظام.
    ///
    /// v2.7.8 hardening (three real defects fixed):
    ///  1. The previous implementation only checked the child exit code plus a
    ///     literal "OK" marker. On a default Windows install
    ///     "Checkpoint-Computer" can print success while Windows silently
    ///     throttles the point (24h frequency / disabled System Protection), so
    ///     ClearWin reported a success that did not exist. Creation is now
    ///     verified by comparing the restore-point SequenceNumber before/after
    ///     (inside the elevated child, which is the only place that can read it).
    ///  2. The elevated command line was built by string concatenation and
    ///     escaped quotes, which is fragile. It is now transported with
    ///     -EncodedCommand (UTF-16LE base64), so no quoting/escaping can corrupt
    ///     the payload.
    ///  3. The implementation never ensured System Protection was enabled and
    ///     never relaxed SystemRestorePointCreationFrequency, so it failed
    ///     forever on machines where protection was off or a point was created
    ///     in the last 24h. Both are repaired inside the elevated child.
    ///
    /// Elevation model: when ClearWin runs as a normal user the helper starts a
    /// single elevated PowerShell via the standard UAC prompt, and remembers a
    /// user refusal so the product does not re-prompt on every tweak.
    /// </summary>
    internal static class SystemRestoreHelper
    {
        private const int DEFAULT_TIMEOUT_MS = 240000;   // Checkpoint takes 20-30s on healthy systems
        private const int MIN_POINT_AGE_SECONDS = 60;

        private static readonly object _createGate = new object();
        private static int _createInFlight;
        private static volatile bool _elevationDeclined;
        private static DateTime _lastVerifiedUtc = DateTime.MinValue;
        private static string _lastVerifiedMessage = "";

        /// <summary>True when the current process token is elevated (Administrator).</summary>
        internal static bool IsProcessElevated
        {
            get
            {
                try
                {
                    using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                    {
                        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
                    }
                }
                catch { return false; }
            }
        }

        internal static bool WasElevationDeclined { get { return _elevationDeclined; } }

        internal static void ResetElevationDeclined()
        {
            _elevationDeclined = false;
        }

        /// <summary>True when a verified point was created within <paramref name="maxAge"/>.</summary>
        internal static bool HasVerifiedPoint(TimeSpan maxAge)
        {
            return _lastVerifiedUtc != DateTime.MinValue && (DateTime.UtcNow - _lastVerifiedUtc) <= maxAge;
        }

        internal static string LastVerifiedMessage { get { return _lastVerifiedMessage; } }

        /// <summary>
        /// Backward-compatible silent attempt: only created when the process is
        /// already elevated (never shows a UAC prompt). Used by background paths.
        /// </summary>
        internal static bool TryCreateRestorePoint(string description)
        {
            if (description == null) description = "ClearWin - Before Optimization";
            if (!IsProcessElevated)
            {
                CWLog.Warn("SystemRestoreHelper",
                    "Restore point skipped: process is not elevated (background path never prompts for UAC).");
                return false;
            }
            RestorePointResult result = CreateRestorePoint(description, false, DEFAULT_TIMEOUT_MS);
            return result.Success;
        }

        /// <summary>
        /// Creates (and independently verifies) a System Restore point.
        /// </summary>
        /// <param name="description">Human readable description; sanitized to ASCII letters/digits.</param>
        /// <param name="allowElevationPrompt">When true a UAC-elevated PowerShell may be started.</param>
        /// <param name="timeoutMs">Overall budget; must stay well above ~30s.</param>
        internal static RestorePointResult CreateRestorePoint(string description, bool allowElevationPrompt, int timeoutMs)
        {
            string desc = SanitizeDescription(description);
            if (timeoutMs < 60000) timeoutMs = DEFAULT_TIMEOUT_MS;

            if (Interlocked.CompareExchange(ref _createInFlight, 1, 0) != 0)
            {
                return RestorePointResult.Fail(RestorePointOutcome.Failed,
                    "A restore-point creation is already in progress.");
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                bool elevated = IsProcessElevated;
                if (!elevated && !allowElevationPrompt)
                {
                    return RestorePointResult.Fail(RestorePointOutcome.NotElevated,
                        "ClearWin is not running with administrator rights, and the background path does not prompt for UAC.");
                }
                if (!elevated && _elevationDeclined)
                {
                    return RestorePointResult.Fail(RestorePointOutcome.DeclinedByUser,
                        "The administrator (UAC) request was declined earlier in this session. Press 'تفعيل وضع المسؤول' once to connect the admin helper, then retry.");
                }

                lock (_createGate)
                {
                    string marker = Path.Combine(Path.GetTempPath(),
                        "clearwin_restore_result_" + Guid.NewGuid().ToString("N") + ".txt");
                    try
                    {
                        string script = BuildElevatedScript(desc, marker);
                        string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
                        string arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + encoded;

                        SpawnOutcome spawn = RunElevatedPowerShell(arguments, elevated, timeoutMs);
                        RestorePointResult parsed = EvaluateMarker(marker, desc, spawn, stopwatch.ElapsedMilliseconds);

                        if (parsed.Success) return parsed;

                        if (parsed.Outcome == RestorePointOutcome.DeclinedByUser)
                        {
                            _elevationDeclined = true;
                            CWLog.Warn("SystemRestoreHelper", "User declined the UAC elevation prompt for restore-point creation.");
                            return parsed;
                        }

                        // Last resort for elevated runs: legacy WMI CreateRestorePoint.
                        if (elevated)
                        {
                            RestorePointResult wmi = TryWmiRestorePoint(desc);
                            if (wmi.Success)
                            {
                                wmi.DurationMs = (int)stopwatch.ElapsedMilliseconds;
                                return wmi;
                            }
                            if (string.IsNullOrEmpty(parsed.Message)) parsed.Message = wmi.Message;
                        }
                        parsed.DurationMs = (int)stopwatch.ElapsedMilliseconds;
                        return parsed;
                    }
                    finally
                    {
                        try { if (File.Exists(marker)) File.Delete(marker); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                CWLog.Error("SystemRestoreHelper.CreateRestorePoint", ex);
                return RestorePointResult.Fail(RestorePointOutcome.Failed,
                    "Restore point creation failed: " + ex.Message);
            }
            finally
            {
                Interlocked.Exchange(ref _createInFlight, 0);
            }
        }

        private sealed class SpawnOutcome
        {
            internal int ExitCode = -1;
            internal bool TimedOut;
            internal bool Started;
            internal bool Declined;
            internal bool NotElevated;
            internal string StdOut = "";
            internal string StdErr = "";
            internal string StartError = "";
        }

        private static SpawnOutcome RunElevatedPowerShell(string arguments, bool alreadyElevated, int timeoutMs)
        {
            var outcome = new SpawnOutcome();
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                if (!alreadyElevated)
                {
                    // Standard Windows UAC prompt; the elevated child runs hidden and
                    // reports through the marker file (ShellExecute cannot redirect).
                    psi.UseShellExecute = true;
                    psi.RedirectStandardOutput = false;
                    psi.RedirectStandardError = false;
                    psi.Verb = "runas";
                }

                using (Process process = Process.Start(psi))
                {
                    if (process == null)
                    {
                        outcome.StartError = "Process.Start returned null for powershell.exe.";
                        return outcome;
                    }
                    outcome.Started = true;

                    if (!alreadyElevated)
                    {
                        if (!process.WaitForExit(timeoutMs))
                        {
                            outcome.TimedOut = true;
                            try { process.Kill(); } catch { }
                        }
                        else
                        {
                            outcome.ExitCode = process.ExitCode;
                        }
                        return outcome;
                    }

                    var stdout = new StringBuilder();
                    var stderr = new StringBuilder();
                    process.OutputDataReceived += (s, e) => { if (e.Data != null) stdout.AppendLine(e.Data); };
                    process.ErrorDataReceived += (s, e) => { if (e.Data != null) stderr.AppendLine(e.Data); };
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    if (!process.WaitForExit(timeoutMs))
                    {
                        outcome.TimedOut = true;
                        try { process.Kill(); } catch { }
                    }
                    else
                    {
                        try { process.WaitForExit(); } catch { }
                        outcome.ExitCode = process.ExitCode;
                        outcome.StdOut = stdout.ToString().Trim();
                        outcome.StdErr = stderr.ToString().Trim();
                    }
                }
            }
            catch (Win32Exception w32)
            {
                // 1223 = ERROR_CANCELLED (the user pressed "No"); 5 = access denied.
                if (w32.NativeErrorCode == 1223)
                {
                    outcome.Declined = true;
                }
                else if (w32.NativeErrorCode == 5)
                {
                    outcome.NotElevated = true;
                }
                outcome.StartError = w32.Message;
                CWLog.Warn("SystemRestoreHelper.Spawn", "Win32 " + w32.NativeErrorCode + ": " + w32.Message);
            }
            catch (Exception ex)
            {
                outcome.StartError = ex.Message;
                CWLog.Error("SystemRestoreHelper.Spawn", ex);
            }
            return outcome;
        }

        private static RestorePointResult EvaluateMarker(string marker, string desc, SpawnOutcome spawn, long elapsedMs)
        {
            var result = new RestorePointResult { DurationMs = (int)elapsedMs, Message = "" };

            if (spawn.Declined)
            {
                result.Outcome = RestorePointOutcome.DeclinedByUser;
                result.Message = "The administrator (UAC) elevation request was declined. " +
                                 "Windows does not allow a normal user to create a restore point.";
                return result;
            }
            if (spawn.NotElevated)
            {
                result.Outcome = RestorePointOutcome.NotElevated;
                result.Message = "Windows refused elevation (access denied) for the restore-point helper.";
                return result;
            }
            if (spawn.TimedOut)
            {
                result.Outcome = RestorePointOutcome.TimedOut;
                result.Message = "Restore point creation did not finish within the time budget (System Restore can be slow on some disks).";
                return result;
            }
            if (!spawn.Started)
            {
                result.Outcome = RestorePointOutcome.Failed;
                result.Message = "Could not start the elevated PowerShell helper: " + spawn.StartError;
                return result;
            }

            Dictionary<string, string> fields = ReadMarker(marker);
            string ok;
            fields.TryGetValue("ok", out ok);
            fields.TryGetValue("error", out var error);
            long beforeSeq = GetLong(fields, "beforeSeq");
            long afterSeq = GetLong(fields, "afterSeq");
            result.SequenceBefore = beforeSeq;
            result.SequenceAfter = afterSeq;
            result.RawDetails = string.IsNullOrEmpty(spawn.StdErr) ? spawn.StdOut : spawn.StdErr;

            bool markerSaysOk = string.Equals(ok, "1", StringComparison.OrdinalIgnoreCase);
            bool verifiedNewPoint = afterSeq > beforeSeq;

            if (markerSaysOk && verifiedNewPoint)
            {
                result.Success = true;
                result.Outcome = RestorePointOutcome.Verified;
                result.Message = "Restore point verified (sequence " + beforeSeq + " -> " + afterSeq + ").";
                _lastVerifiedUtc = DateTime.UtcNow;
                _lastVerifiedMessage = result.Message;
                CWLog.Info("SystemRestoreHelper", "Restore point created and verified: " + desc +
                          " (seq " + beforeSeq + " -> " + afterSeq + ", " + elapsedMs + " ms)");
                return result;
            }

            result.Outcome = RestorePointOutcome.Failed;
            if (!string.IsNullOrEmpty(error))
            {
                result.Message = "Windows could not create a restore point: " + error;
            }
            else if (markerSaysOk && !verifiedNewPoint)
            {
                // The exact false-success the old build reported as OK.
                result.Message = "Checkpoint-Computer reported success but Windows created no new restore point " +
                                 "(System Protection may be disabled for the system drive, or the point was throttled). " +
                                 "Enable System Protection for the system drive and retry.";
            }
            else if (!string.IsNullOrEmpty(result.RawDetails))
            {
                result.Message = "Restore point helper failed (exit code " + spawn.ExitCode + "): " + result.RawDetails;
            }
            else
            {
                result.Message = "Restore point helper failed (exit code " + spawn.ExitCode + ") without a result marker.";
            }
            CWLog.Warn("SystemRestoreHelper", result.Message);
            return result;
        }

        private static long GetLong(Dictionary<string, string> fields, string key)
        {
            string raw;
            if (!fields.TryGetValue(key, out raw) || string.IsNullOrEmpty(raw)) return 0;
            long value;
            return long.TryParse(raw, out value) ? value : 0;
        }

        private static Dictionary<string, string> ReadMarker(string path)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(path)) return map;
                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    int split = line.IndexOf('=');
                    if (split <= 0) continue;
                    map[line.Substring(0, split).Trim()] = line.Substring(split + 1).Trim();
                }
            }
            catch (Exception ex) { CWLog.Error("SystemRestoreHelper.ReadMarker", ex); }
            return map;
        }

        /// <summary>
        /// Builds the elevated payload. Runs in a completely separate, elevated
        /// PowerShell 5.1 process, so it may only use built-in cmdlets.
        /// </summary>
        private static string BuildElevatedScript(string description, string markerPath)
        {
            string desc = description.Replace("'", "''");
            string marker = markerPath.Replace("'", "''");

            var sb = new StringBuilder();
            sb.AppendLine("$ErrorActionPreference = 'Stop'");
            sb.AppendLine("$desc = '" + desc + "'");
            sb.AppendLine("$marker = '" + marker + "'");
            sb.AppendLine("$out = New-Object System.Collections.Hashtable");
            sb.AppendLine("$out['ok'] = 0");
            sb.AppendLine("$out['error'] = ''");
            sb.AppendLine("$out['beforeSeq'] = ''");
            sb.AppendLine("$out['afterSeq'] = ''");
            sb.AppendLine("$out['pointsBefore'] = 0");
            sb.AppendLine("$out['pointsAfter'] = 0");
            sb.AppendLine("$out['checkpointSeconds'] = 0");
            sb.AppendLine("function Get-LatestPoint {");
            sb.AppendLine("  try {");
            sb.AppendLine("    $pts = @(Get-ComputerRestorePoint -ErrorAction Stop)");
            sb.AppendLine("    if ($pts.Count -eq 0) { return $null }");
            sb.AppendLine("    $sorted = $pts | Sort-Object -Property SequenceNumber -Descending");
            sb.AppendLine("    return ($sorted | Select-Object -First 1)");
            sb.AppendLine("  } catch { return $null }");
            sb.AppendLine("}");
            sb.AppendLine("try {");
            // 1) Relax the 24h creation-frequency throttle so a point can be made on demand.
            sb.AppendLine("  try {");
            sb.AppendLine("    $srKey = 'HKLM:\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\SystemRestore'");
            sb.AppendLine("    New-ItemProperty -Path $srKey -Name 'SystemRestorePointCreationFrequency' -Value 0 -PropertyType DWord -Force -ErrorAction SilentlyContinue | Out-Null");
            sb.AppendLine("  } catch { }");
            // 2) Snapshot existing points before the change.
            sb.AppendLine("  $before = Get-LatestPoint");
            sb.AppendLine("  $beforeAll = @(Get-ComputerRestorePoint -ErrorAction SilentlyContinue)");
            sb.AppendLine("  if ($before -ne $null) { $out['beforeSeq'] = [string]$before.SequenceNumber }");
            sb.AppendLine("  $out['pointsBefore'] = $beforeAll.Count");
            // 3) Make sure System Protection is enabled for the system drive.
            sb.AppendLine("  $sysDrive = ($env:SystemDrive + '\\')");
            sb.AppendLine("  if ($before -eq $null) {");
            sb.AppendLine("    try { Enable-ComputerRestore -Drive $sysDrive -ErrorAction Stop } catch { $out['enableError'] = $_.Exception.Message }");
            sb.AppendLine("  }");
            // 4) Create the point.
            sb.AppendLine("  $sw = [System.Diagnostics.Stopwatch]::StartNew()");
            sb.AppendLine("  Checkpoint-Computer -Description $desc -RestorePointType 'MODIFY_SETTINGS' -ErrorAction Stop");
            sb.AppendLine("  $sw.Stop()");
            sb.AppendLine("  $out['checkpointSeconds'] = [math]::Round($sw.Elapsed.TotalSeconds, 1)");
            // 5) Verify: a NEW sequence number must appear (bounded polling).
            sb.AppendLine("  $deadline = (Get-Date).AddSeconds(90)");
            sb.AppendLine("  $after = $null");
            sb.AppendLine("  while ((Get-Date) -lt $deadline) {");
            sb.AppendLine("    $after = Get-LatestPoint");
            sb.AppendLine("    if ($after -ne $null) {");
            sb.AppendLine("      if ($before -eq $null) { break }");
            sb.AppendLine("      if ([int64]$after.SequenceNumber -gt [int64]$before.SequenceNumber) { break }");
            sb.AppendLine("    }");
            sb.AppendLine("    Start-Sleep -Seconds 2");
            sb.AppendLine("  }");
            sb.AppendLine("  $afterAll = @(Get-ComputerRestorePoint -ErrorAction SilentlyContinue)");
            sb.AppendLine("  $out['pointsAfter'] = $afterAll.Count");
            sb.AppendLine("  if ($after -ne $null) {");
            sb.AppendLine("    $out['afterSeq'] = [string]$after.SequenceNumber");
            sb.AppendLine("    if ($before -eq $null -or [int64]$after.SequenceNumber -gt [int64]$before.SequenceNumber) {");
            sb.AppendLine("      $out['ok'] = 1");
            sb.AppendLine("    } else {");
            sb.AppendLine("      $out['error'] = 'Checkpoint-Computer succeeded but no new restore point was created (System Protection disabled for the system drive, or Windows throttled the point).'");
            sb.AppendLine("    }");
            sb.AppendLine("  } else {");
            sb.AppendLine("    $out['error'] = 'Checkpoint-Computer succeeded but no restore point could be read back.'");
            sb.AppendLine("  }");
            sb.AppendLine("} catch {");
            sb.AppendLine("  $out['ok'] = 0");
            sb.AppendLine("  $out['error'] = $_.Exception.Message");
            sb.AppendLine("  try { $out['hresult'] = ('0x{0:X8}' -f $_.Exception.HResult) } catch { }");
            sb.AppendLine("}");
            // Marker written as key=value lines: trivially parseable and newline-free.
            sb.AppendLine("try {");
            sb.AppendLine("  $lines = @()");
            sb.AppendLine("  foreach ($k in @('ok','error','beforeSeq','afterSeq','pointsBefore','pointsAfter','checkpointSeconds','enableError','hresult')) {");
            sb.AppendLine("    if ($out.ContainsKey($k)) {");
            sb.AppendLine("      $v = [string]$out[$k]");
            sb.AppendLine("      if ($null -eq $v) { $v = '' }");
            sb.AppendLine("      $v = $v -replace \"`r|`n\", ' '");
            sb.AppendLine("      $lines += ($k + '=' + $v)");
            sb.AppendLine("    }");
            sb.AppendLine("  }");
            sb.AppendLine("  [System.IO.File]::WriteAllLines($marker, $lines, (New-Object System.Text.UTF8Encoding($false)))");
            sb.AppendLine("} catch { }");
            sb.AppendLine("if ($out['ok'] -eq 1) { exit 0 } else { exit 1 }");
            return sb.ToString();
        }

        private static string SanitizeDescription(string description)
        {
            string safe = description ?? "ClearWin - Before Optimization";
            var sb = new StringBuilder(safe.Length);
            foreach (char c in safe)
            {
                if (c >= 32 && c <= 126) sb.Append(c);
            }
            // Windows restore-point descriptions are limited to 256 characters.
            if (sb.Length > 200) return sb.ToString(0, 200);
            if (sb.Length == 0) return "ClearWin - Before Optimization";
            return sb.ToString();
        }

        /// <summary>Legacy in-process WMI creation, used only as an elevated last resort.</summary>
        private static RestorePointResult TryWmiRestorePoint(string description)
        {
            try
            {
                var scope = new System.Management.ManagementScope(@"\\.\root\default");
                var path = new System.Management.ManagementPath("SystemRestore");
                using (var cls = new System.Management.ManagementClass(scope, path, null))
                {
                    var parms = cls.GetMethodParameters("CreateRestorePoint");
                    parms["Description"] = description;
                    parms["RestorePointType"] = 12; // MODIFY_SETTINGS
                    parms["EventType"] = 100;       // BEGIN_SYSTEM_CHANGE
                    var result = cls.InvokeMethod("CreateRestorePoint", parms, null);
                    uint ret = (uint)result["ReturnValue"];
                    if (ret == 0)
                    {
                        CWLog.Info("SystemRestoreHelper.WMI", "Restore point created via WMI (verification limited): " + description);
                        return new RestorePointResult
                        {
                            Success = true,
                            Outcome = RestorePointOutcome.Verified,
                            Message = "Restore point created via WMI SystemRestore."
                        };
                    }
                    CWLog.Warn("SystemRestoreHelper.WMI", "CreateRestorePoint returned " + ret);
                    return RestorePointResult.Fail(RestorePointOutcome.Failed,
                        "WMI CreateRestorePoint returned " + ret + " (1058 = System Restore disabled, 1460 = already in progress).");
                }
            }
            catch (Exception ex)
            {
                CWLog.Error("SystemRestoreHelper.WMI", ex);
                return RestorePointResult.Fail(RestorePointOutcome.Failed, "WMI CreateRestorePoint failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Best-effort read-only status for the UI. Non-elevated callers can only
        /// inspect the registry, which is enough to explain the common failure.
        /// </summary>
        internal static bool IsSystemRestoreEnabled()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                           @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore"))
                {
                    if (key == null) return false;
                    object disable = key.GetValue("DisableSR");
                    if (disable is int && (int)disable == 1) return false;
                    object init = key.GetValue("SRInitDone");
                    if (init is int && (int)init == 0) return false;
                    return key.GetValue("RPSessionInterval") != null;
                }
            }
            catch (Exception ex)
            {
                CWLog.Error("SystemRestoreHelper.IsSystemRestoreEnabled", ex);
                return false;
            }
        }
    }
}
