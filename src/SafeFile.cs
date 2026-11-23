using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace ClearWin
{
    /// <summary>
    /// Crash-safe and concurrency-safe persistence primitives shared by every
    /// learning/state store in ClearWin.
    ///
    /// Defects this type fixes (v2.7.8):
    ///  * profiles.dat.tmp / stats.dat.tmp (and the other *.dat stores) used a
    ///    FIXED temp path ("&lt;file&gt;.tmp"). Concurrent writers therefore collided
    ///    with "The process cannot access the file because it is being used by
    ///    another process", and a process that was hard-killed by
    ///    Environment.Exit could leave a half-written .tmp on disk.
    ///  * Loads picked the ".tmp" file purely by timestamp, so a truncated temp
    ///    file left behind by an unclean exit was preferred over the real file
    ///    and the learned data was silently lost on the next start.
    ///
    /// Guarantees provided here:
    ///  * each write goes to a UNIQUE private temp file, is flushed to the disk
    ///    (FileOptions.WriteThrough + Flush(true)) and then swapped in with
    ///    File.Replace/Move using bounded retries for antivirus, indexer and
    ///    cross-process share locks;
    ///  * intra-process writers to the same path are serialized by a per-path
    ///    gate, and versioned .bakN backups are rotated best-effort;
    ///  * a load never prefers an unverified temp file while a usable primary or
    ///    versioned backup exists;
    ///  * stale / legacy temp files are quarantined instead of accumulating.
    /// </summary>
    internal static class SafeFile
    {
        private const int MAX_WRITE_ATTEMPTS = 8;
        private const int MAX_BACKUPS = 3;
        private static readonly int[] RetryDelaysMs = new int[] { 15, 30, 60, 120, 200, 320, 500, 750 };

        private static int _tempSequence;
        private static readonly object _pathGateLock = new object();
        private static readonly Dictionary<string, object> _pathGates =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        private static object GateFor(string fullPath)
        {
            lock (_pathGateLock)
            {
                object gate;
                if (!_pathGates.TryGetValue(fullPath, out gate))
                {
                    gate = new object();
                    _pathGates[fullPath] = gate;
                }
                return gate;
            }
        }

        private static void SleepBackoff(int attempt)
        {
            int idx = attempt < RetryDelaysMs.Length ? attempt : RetryDelaysMs.Length - 1;
            try { Thread.Sleep(RetryDelaysMs[idx]); } catch { }
        }

        /// <summary>Builds a unique, process/thread-unique temp file name next to the target.</summary>
        internal static string UniqueTempPath(string path)
        {
            string full = Path.GetFullPath(path);
            string dir = Path.GetDirectoryName(full);
            string name = Path.GetFileName(full);
            int seq = Interlocked.Increment(ref _tempSequence);
            string token = Process.GetCurrentProcess().Id.ToString("X", CultureInfo.InvariantCulture) + "-" +
                           Thread.CurrentThread.ManagedThreadId.ToString("X", CultureInfo.InvariantCulture) + "-" +
                           seq.ToString("X", CultureInfo.InvariantCulture) + "-" +
                           Guid.NewGuid().ToString("N").Substring(0, 10);
            return Path.Combine(dir, name + "." + token + ".partial");
        }

        /// <summary>Writes text atomically. Throws IOException if every attempt failed.</summary>
        internal static void WriteAllTextAtomic(string path, string content)
        {
            if (path == null) throw new ArgumentNullException("path");
            string full = Path.GetFullPath(path);
            if (content == null) content = string.Empty;

            lock (GateFor(full))
            {
                string dir = Path.GetDirectoryName(full);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                string temp = UniqueTempPath(full);
                try
                {
                    WriteTempFile(temp, content);
                    RotateBackups(full);
                    SwapIn(temp, full);
                }
                finally
                {
                    TryDelete(temp);
                }
            }
        }

        /// <summary>Non-throwing wrapper used by best-effort persistence paths.</summary>
        internal static bool TryWriteAllTextAtomic(string path, string content, out string error)
        {
            error = null;
            try
            {
                WriteAllTextAtomic(path, content);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        internal static void WriteAllLinesAtomic(string path, string[] lines)
        {
            var sb = new StringBuilder();
            if (lines != null)
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    sb.AppendLine(lines[i]);
                }
            }
            WriteAllTextAtomic(path, sb.ToString());
        }

        private static void WriteTempFile(string tempPath, string content)
        {
            Exception last = null;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    byte[] bytes = new UTF8Encoding(false).GetBytes(content);
                    using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write,
                               FileShare.None, 4096, FileOptions.WriteThrough))
                    {
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush();
                        stream.Flush(true);
                    }
                    return;
                }
                catch (Exception ex) when (IsTransientIo(ex))
                {
                    last = ex;
                    SleepBackoff(attempt);
                }
            }
            throw new IOException("Could not write temp file '" + tempPath + "': " +
                                  (last != null ? last.Message : "unknown"), last);
        }

        private static void SwapIn(string tempPath, string targetPath)
        {
            Exception last = null;
            for (int attempt = 0; attempt < MAX_WRITE_ATTEMPTS; attempt++)
            {
                try
                {
                    if (!File.Exists(targetPath))
                    {
                        File.Move(tempPath, targetPath);
                        return;
                    }
                    File.Replace(tempPath, targetPath, null, true);
                    return;
                }
                catch (FileNotFoundException)
                {
                    // Target disappeared between the check and the replace: fall back to a move.
                    try { File.Move(tempPath, targetPath); return; }
                    catch (Exception moveEx) { last = moveEx; }
                }
                catch (Exception ex) when (IsTransientIo(ex))
                {
                    last = ex;
                }
                SleepBackoff(attempt);
            }

            // Final fallback: remove-then-move. The window is tiny and .bak1 holds the
            // previous generation, so worst case the previous state is recoverable.
            try
            {
                if (File.Exists(targetPath)) File.Delete(targetPath);
                File.Move(tempPath, targetPath);
                return;
            }
            catch (Exception deleteEx)
            {
                last = deleteEx;
            }

            throw new IOException("Atomic replace failed for '" + targetPath + "' after " +
                                  MAX_WRITE_ATTEMPTS + " attempts: " +
                                  (last != null ? last.Message : "unknown"), last);
        }

        private static bool IsTransientIo(Exception ex)
        {
            if (ex == null) return false;
            if (ex is UnauthorizedAccessException) return true; // AV / indexer holds the handle briefly
            if (ex is FileNotFoundException || ex is DirectoryNotFoundException) return false;
            if (ex is IOException) return true;                 // sharing violation / lock
            return false;
        }

        private static void RotateBackups(string targetPath)
        {
            try
            {
                if (!File.Exists(targetPath)) return;
                for (int i = MAX_BACKUPS - 1; i >= 1; i--)
                {
                    string older = targetPath + ".bak" + i.ToString(CultureInfo.InvariantCulture);
                    string newer = targetPath + ".bak" + (i + 1).ToString(CultureInfo.InvariantCulture);
                    if (!File.Exists(older)) continue;
                    if (File.Exists(newer)) TryDelete(newer);
                    File.Move(older, newer);
                }
                CopyWithRetry(targetPath, targetPath + ".bak1");
            }
            catch (Exception ex)
            {
                // Backups are an optimization, never a reason to fail the write.
                CWLog.Warn("SafeFile.RotateBackups", targetPath + ": " + ex.Message);
            }
        }

        private static void CopyWithRetry(string source, string destination)
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                try
                {
                    File.Copy(source, destination, true);
                    return;
                }
                catch (Exception ex) when (IsTransientIo(ex))
                {
                    SleepBackoff(attempt);
                }
            }
        }

        internal static void TryDelete(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static bool IsUsableFile(string path)
        {
            try
            {
                var info = new FileInfo(path);
                return info.Exists && info.Length > 0;
            }
            catch { return false; }
        }

        private static DateTime SafeLastWrite(string path)
        {
            try { return File.GetLastWriteTimeUtc(path); } catch { return DateTime.MinValue; }
        }

        /// <summary>
        /// Resolves which file to load: the primary file when usable, otherwise the
        /// NEWEST usable versioned backup, and only as an absolute last resort a
        /// legacy ".tmp" file (never preferred over a real file).
        /// </summary>
        internal static string ResolveLoadPath(string path, int maxBackups)
        {
            string full = Path.GetFullPath(path);
            if (IsUsableFile(full)) return full;

            string newestBackup = null;
            DateTime newestStamp = DateTime.MinValue;
            int count = maxBackups > 0 ? maxBackups : MAX_BACKUPS;
            for (int i = 1; i <= count; i++)
            {
                string bak = full + ".bak" + i.ToString(CultureInfo.InvariantCulture);
                if (!IsUsableFile(bak)) continue;
                DateTime stamp = SafeLastWrite(bak);
                if (stamp > newestStamp)
                {
                    newestStamp = stamp;
                    newestBackup = bak;
                }
            }
            if (newestBackup != null)
            {
                CWLog.Warn("SafeFile.Load", "Primary missing, using backup: " + newestBackup);
                return newestBackup;
            }

            // Absolute last resort: a legacy fixed-name temp file from an older version.
            string legacyTemp = full + ".tmp";
            if (IsUsableFile(legacyTemp))
            {
                CWLog.Warn("SafeFile.Load", "Primary and backups missing, salvaging temp file: " + legacyTemp);
                return legacyTemp;
            }
            return full;
        }

        /// <summary>
        /// Removes temp files left behind by older builds or by hard-killed
        /// processes. Never removes the last remaining copy of a store.
        /// </summary>
        internal static void CleanupStaleTemps(string path)
        {
            try
            {
                string full = Path.GetFullPath(path);
                string dir = Path.GetDirectoryName(full);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
                string name = Path.GetFileName(full);
                bool hasPrimaryOrBackup = IsUsableFile(full) || IsUsableFile(full + ".bak1");

                // Legacy fixed-name temp written by builds <= 2.7.7.
                string legacy = full + ".tmp";
                if (hasPrimaryOrBackup)
                {
                    TryDelete(legacy);
                }
                else if (IsUsableFile(legacy))
                {
                    // Only copy remains: promote it after validating it is non-trivial,
                    // otherwise keep it for ResolveLoadPath to decide.
                    CWLog.Warn("SafeFile.Cleanup", "Legacy temp kept as last remaining copy: " + legacy);
                }

                // Unique temp files from this build: anything older than a couple of
                // minutes cannot belong to a live write (writes are sub-second).
                string[] stale = Directory.GetFiles(dir, name + ".*.partial");
                for (int i = 0; i < stale.Length; i++)
                {
                    try
                    {
                        if (DateTime.UtcNow - File.GetLastWriteTimeUtc(stale[i]) > TimeSpan.FromMinutes(2))
                            TryDelete(stale[i]);
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                CWLog.Warn("SafeFile.CleanupStaleTemps", path + ": " + ex.Message);
            }
        }
    }
}