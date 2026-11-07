using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace ClearWin
{
    public class TweakAnalyzer
    {
        private TweakKnowledgeBase _knowledge;
        private string _scanDir;

        // Keyword → category mapping for content-based detection
        private static readonly Dictionary<string, string> CATEGORY_KEYWORDS = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            { "thermal", "Thermal" }, { "temperature", "Thermal" }, { "temp", "Thermal" }, { "throttle", "Thermal" }, { "heat", "Thermal" },
            { "cpu", "CPU" }, { "processor", "CPU" }, { "clock", "CPU" }, { "frequency", "CPU" }, { "governor", "CPU" }, { "affinity", "CPU" }, { "core", "CPU" },
            { "memory", "Memory" }, { "ram", "Memory" }, { "standby", "Memory" }, { "cache", "Memory" }, { "workingset", "Memory" }, { "pagefile", "Memory" },
            { "network", "Network" }, { "tcp", "Network" }, { "dns", "Network" }, { "wifi", "Network" }, { "latency", "Network" }, { "congestion", "Network" },
            { "battery", "Battery" }, { "power", "Battery" }, { "doze", "Battery" }, { "sleep", "Battery" }, { "hibernate", "Battery" }, { "wakelock", "Battery" },
            { "gpu", "GPU" }, { "render", "GPU" }, { "vulkan", "GPU" }, { "display", "GPU" }, { "refresh", "GPU" },
            { "io", "I/O" }, { "disk", "I/O" }, { "fstrim", "I/O" }, { "ssd", "I/O" }, { "scheduler", "I/O" },
            { "animation", "Display" }, { "screen", "Display" }, { "brightness", "Display" },
            { "security", "Security" }, { "firewall", "Security" }, { "permission", "Security" }, { "protect", "Security" },
            { "zygote", "Memory" }, { "trim", "Memory" }, { "freeze", "Memory" }, { "hibernate", "Battery" }
        };

        // Dangerous keywords → safety penalty
        private static readonly string[] DANGEROUS_KEYWORDS = new[] {
            "rm -rf", "format", "del /s", "reg delete", "shutdown", "reboot",
            "kernel panic", "disable firewall", "disable uac", "takeown", "icacls",
            "bcdedit", "bootrec", "diskpart", "clean", "override"
        };

        // Safety keywords → safety bonus
        private static readonly string[] SAFE_KEYWORDS = new[] {
            "monitor", "alert", "log", "read", "get", "query", "status",
            "info", "display", "show", "list", "query", "check"
        };

        public TweakAnalyzer(TweakKnowledgeBase knowledge, string scanDir)
        {
            _knowledge = knowledge;
            _scanDir = scanDir;
        }

        // === Main analysis entry point ===
        public TweakAnalysisResult AnalyzeFile(string filePath)
        {
            var result = new TweakAnalysisResult();
            result.FilePath = filePath;
            result.AnalyzedAt = DateTime.Now;
            result.Warnings = new List<string>();
            result.Tags = new List<string>();

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                result.Warnings.Add("File not found");
                result.SafetyScore = 0;
                result.Confidence = 0;
                return result;
            }

            string ext = Path.GetExtension(filePath).ToLower();
            result.FileType = DetectFileType(ext, filePath);
            result.DetectedType = result.FileType;

            // Read content for analysis (limited to first 64KB for scripts)
            string content = ReadFileContent(filePath, result.FileType);

            // Detect category from content
            result.Category = DetectCategory(content, filePath);
            result.Function = DetectFunction(content, filePath, result.FileType);
            result.FunctionAr = TranslateFunction(result.Function);

            // Calculate safety score
            result.SafetyScore = CalculateSafetyScore(content, filePath, result.FileType);

            // Match against knowledge base
            var matched = MatchKnowledgeBase(content, filePath, result.FileType);
            if (matched != null)
            {
                result.SafetyScore = Math.Max(result.SafetyScore, matched.SafetyScore);
                result.Category = matched.Category;
                if (string.IsNullOrEmpty(result.Function) || result.Function == "Unknown")
                {
                    result.Function = matched.Function;
                    result.FunctionAr = matched.FunctionAr;
                }
                result.Tags.Add("KB:" + matched.Id);
                result.Confidence = 0.9;
            }
            else
            {
                result.Confidence = 0.5;
            }

            // Add warnings for risky files
            if (result.SafetyScore < 0.3)
                result.Warnings.Add("High risk file - manual review recommended");
            if (result.SafetyScore < 0.6 && result.SafetyScore >= 0.3)
                result.Warnings.Add("Moderate risk - proceed with caution");
            if (ext == ".exe" || ext == ".dll")
                result.Warnings.Add("Binary file - limited analysis possible");

            return result;
        }

        // === Analyze a registry key path ===
        public TweakAnalysisResult AnalyzeRegistryKey(string keyPath)
        {
            var result = new TweakAnalysisResult();
            result.FilePath = keyPath;
            result.FileType = "Registry";
            result.DetectedType = "Registry";
            result.AnalyzedAt = DateTime.Now;
            result.Warnings = new List<string>();
            result.Tags = new List<string>();

            try
            {
                string valueName = Path.GetFileName(keyPath);
                string parentPath = Path.GetDirectoryName(keyPath);
                string content = keyPath.ToLower();

                result.Category = DetectCategory(content, keyPath);
                result.Function = "Registry modification: " + valueName;
                result.FunctionAr = "تعديل registry: " + valueName;
                result.SafetyScore = CalculateRegistrySafety(keyPath);
                result.Confidence = 0.7;

                // Match against knowledge base
                var matched = MatchKnowledgeBase(content, keyPath, "Registry");
                if (matched != null)
                {
                    result.SafetyScore = Math.Max(result.SafetyScore, matched.SafetyScore);
                    result.Category = matched.Category;
                    result.Function = matched.Function;
                    result.FunctionAr = matched.FunctionAr;
                    result.Tags.Add("KB:" + matched.Id);
                    result.Confidence = 0.9;
                }
            }
            catch (Exception ex)
            {
                result.Warnings.Add("Registry read error: " + ex.Message);
                result.SafetyScore = 0.5;
            }

            return result;
        }

        // === Analyze a command/script string ===
        public TweakAnalysisResult AnalyzeCommand(string command)
        {
            var result = new TweakAnalysisResult();
            result.FilePath = "<command>";
            result.FileType = "Command";
            result.DetectedType = DetectCommandType(command);
            result.AnalyzedAt = DateTime.Now;
            result.Warnings = new List<string>();
            result.Tags = new List<string>();

            result.Category = DetectCategory(command, "");
            result.Function = DetectFunction(command, "", "Command");
            result.FunctionAr = TranslateFunction(result.Function);
            result.SafetyScore = CalculateCommandSafety(command);
            result.Confidence = 0.75;

            // Match against knowledge base
            var matched = MatchKnowledgeBase(command, "", "Command");
            if (matched != null)
            {
                result.SafetyScore = Math.Max(result.SafetyScore, matched.SafetyScore);
                result.Category = matched.Category;
                result.Function = matched.Function;
                result.FunctionAr = matched.FunctionAr;
                result.Tags.Add("KB:" + matched.Id);
                result.Confidence = 0.9;
            }

            return result;
        }

        // === Batch scan a directory ===
        public List<TweakAnalysisResult> ScanDirectory(string dir, bool recursive)
        {
            var results = new List<TweakAnalysisResult>();
            if (!Directory.Exists(dir)) return results;

            string[] extensions = new[] { ".reg", ".bat", ".cmd", ".ps1", ".sh", ".exe", ".dll", ".inf", ".cat", ".sys" };

            foreach (string file in SafeEnumerateFiles(dir, extensions, recursive))
            {
                try { results.Add(AnalyzeFile(file)); } catch (Exception ex) { CWLog.Error("TweakAnalyzer", ex); }
            }
            return results;
        }

        private IEnumerable<string> SafeEnumerateFiles(string dir, string[] extensions, bool recursive)
        {
            var pending = new Stack<string>();
            pending.Push(dir);
            while (pending.Count > 0)
            {
                string current = pending.Pop();
                string[] files = null;
                try { files = Directory.GetFiles(current); }
                catch (Exception ex2) { CWLog.Error("TweakAnalyzer", ex2); }
                if (files != null)
                {
                    foreach (var f in files)
                    {
                        foreach (var p in extensions)
                        {
                            if (f.EndsWith(p, StringComparison.OrdinalIgnoreCase)) { yield return f; break; }
                        }
                    }
                }
                if (recursive)
                {
                    string[] subs = null;
                    try { subs = Directory.GetDirectories(current); }
                    catch (Exception ex3) { CWLog.Error("TweakAnalyzer", ex3); }
                    if (subs != null) { foreach (var s in subs) pending.Push(s); }
                }
            }
        }

        // === Get safety summary for a scan ===
        public string GetSafetySummary(List<TweakAnalysisResult> results)
        {
            if (results == null || results.Count == 0) return "No files scanned.";

            int safe = 0, moderate = 0, risky = 0, dangerous = 0;
            foreach (var r in results)
            {
                if (r.SafetyScore >= 0.8) safe++;
                else if (r.SafetyScore >= 0.6) moderate++;
                else if (r.SafetyScore >= 0.3) risky++;
                else dangerous++;
            }

            return string.Format("Scan complete: {0} files. Safe: {1}, Moderate: {2}, Risky: {3}, Dangerous: {4}",
                results.Count, safe, moderate, risky, dangerous);
        }

        // === Detect file type from extension ===
        private string DetectFileType(string ext, string path)
        {
            switch (ext)
            {
                case ".reg": return "Registry";
                case ".bat": case ".cmd": return "BatchScript";
                case ".ps1": return "PowerShell";
                case ".sh": return "ShellScript";
                case ".exe": return "EXE";
                case ".dll": case ".sys": return "DLL";
                case ".inf": return "DriverInf";
                case ".cat": return "Catalog";
                default:
                    // Try to detect by content (header bytes only, never the whole file)
                    try
                    {
                        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            byte[] header = new byte[4];
                            int n = fs.Read(header, 0, header.Length);
                            if (n >= 2 && header[0] == 0xFF && header[1] == 0xFE) return "TextUnicode";
                            if (n >= 3 && header[0] == 0xEF && header[1] == 0xBB && header[2] == 0xBF) return "TextUTF8";
                        }
                    } catch (Exception ex4) { CWLog.Error("TweakAnalyzer", ex4); }
                    return "Unknown";
            }
        }

        // === Detect command type ===
        private string DetectCommandType(string command)
        {
            if (command == null) return "Command";
            string cmd = command.Trim().ToLower();
            if (cmd.StartsWith("reg ")) return "Registry";
            if (cmd.StartsWith("powercfg")) return "PowerShell";
            if (cmd.StartsWith("netsh")) return "Command";
            if (cmd.StartsWith("sc ")) return "Command";
            if (cmd.StartsWith("taskkill") || cmd.StartsWith("tasklist")) return "Command";
            if (cmd.Contains("Set-ItemProperty") || cmd.Contains("Get-ItemProperty")) return "PowerShell";
            if (cmd.Contains("New-Item") || cmd.Contains("Remove-Item")) return "PowerShell";
            return "Command";
        }

        // === Read file content (limited) ===
        private string ReadFileContent(string path, string fileType)
        {
            if (fileType == "EXE" || fileType == "DLL" || fileType == "DriverInf" || fileType == "Catalog" || fileType == "Unknown")
                return "";

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                int maxBytes = Math.Min(bytes.Length, 65536); // 64KB max
                return Encoding.UTF8.GetString(bytes, 0, maxBytes);
            }
            catch
            {
                return "";
            }
        }

        // === Detect category from content keywords ===
        private string DetectCategory(string content, string path)
        {
            if (string.IsNullOrEmpty(content)) return "General";

            string lower = content.ToLower();
            Dictionary<string, int> scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var kv in CATEGORY_KEYWORDS)
            {
                int count = CountOccurrences(lower, kv.Key);
                if (count > 0)
                {
                    if (!scores.ContainsKey(kv.Value)) scores[kv.Value] = 0;
                    scores[kv.Value] += count;
                }
            }

            string bestCategory = "General";
            int bestScore = 0;
            foreach (var kv in scores)
            {
                if (kv.Value > bestScore)
                {
                    bestScore = kv.Value;
                    bestCategory = kv.Key;
                }
            }

            return bestCategory;
        }

        // === Detect function description from content ===
        private string DetectFunction(string content, string path, string fileType)
        {
            if (string.IsNullOrEmpty(content)) return "Unknown function";

            string lower = content.ToLower();

            // Pattern-based detection
            if (lower.Contains("scaling_governor") || lower.Contains("performance mode") || lower.Contains("powerscheme"))
                return "CPU performance governor";
            if (lower.Contains("thermal") && (lower.Contains("throttle") || lower.Contains("cap") || lower.Contains("limit")))
                return "Thermal throttling control";
            if (lower.Contains("tcp") && (lower.Contains("optimize") || lower.Contains("tuning") || lower.Contains("buffer")))
                return "TCP network optimization";
            if (lower.Contains("dns") && (lower.Contains("cache") || lower.Contains("flush") || lower.Contains("preload")))
                return "DNS cache optimization";
            if (lower.Contains("memory") && (lower.Contains("clean") || lower.Contains("purge") || lower.Contains("trim")))
                return "Memory cleanup/trim";
            if (lower.Contains("powercfg") || lower.Contains("power plan") || lower.Contains("powerscheme"))
                return "Power plan configuration";
            if (lower.Contains("gpu") && (lower.Contains("performance") || lower.Contains("max") || lower.Contains("frequency")))
                return "GPU performance mode";
            if (lower.Contains("animation") && (lower.Contains("disable") || lower.Contains("reduce") || lower.Contains("off")))
                return "UI animation reduction";
            if (lower.Contains("battery") && (lower.Contains("saver") || lower.Contains("optimize") || lower.Contains("health")))
                return "Battery optimization";
            if (lower.Contains("network") && (lower.Contains("sleep") || lower.Contains("scan") || lower.Contains("interval")))
                return "Network power management";
            if (lower.Contains("fstrim") || lower.Contains("ssd") && lower.Contains("trim"))
                return "SSD TRIM optimization";
            if (lower.Contains("io") && (lower.Contains("scheduler") || lower.Contains("priority") || lower.Contains("read-ahead")))
                return "I/O scheduler optimization";
            if (lower.Contains("wakelock") || lower.Contains("wake lock"))
                return "Wakelock management";
            if (lower.Contains("freeze") || lower.Contains("frozen") || lower.Contains("hibernate"))
                return "App freeze/hibernate";
            if (lower.Contains("priority") && (lower.Contains("process") || lower.Contains("class")))
                return "Process priority adjustment";

            // File-type based fallback
            if (fileType == "Registry")
                return "Registry configuration";
            if (fileType == "PowerShell")
                return "PowerShell optimization script";
            if (fileType == "BatchScript")
                return "Batch optimization script";

            return "Unknown function";
        }

        // === Translate function to Arabic ===
        private string TranslateFunction(string function)
        {
            if (string.IsNullOrEmpty(function)) return "دالة غير معروفة";

            string lower = function.ToLower();
            if (lower.Contains("cpu") && lower.Contains("governor")) return "محدد أداء المعالج";
            if (lower.Contains("thermal")) return "تحكم الحرارة";
            if (lower.Contains("tcp") || lower.Contains("network optimization")) return "تحسين الشبكة";
            if (lower.Contains("dns")) return "تحسين ذاكرة DNS";
            if (lower.Contains("memory") || lower.Contains("clean") || lower.Contains("trim")) return "تنظيف الذاكرة";
            if (lower.Contains("power")) return "تحسين الطاقة";
            if (lower.Contains("gpu")) return "تحسين كرت الشاشة";
            if (lower.Contains("animation")) return "تقليل الرسوم المتحركة";
            if (lower.Contains("battery")) return "تحسين البطارية";
            if (lower.Contains("io") || lower.Contains("ssd") || lower.Contains("fstrim")) return "تحسين الإدخال/الإخراج";
            if (lower.Contains("wakelock")) return "إدارة Wake Lock";
            if (lower.Contains("freeze") || lower.Contains("hibernate")) return "إسبات التطبيقات";
            if (lower.Contains("priority")) return "تحديد أولوية العمليات";
            if (lower.Contains("registry")) return "تعديل Registry";
            if (lower.Contains("powershell") || lower.Contains("script")) return "سكربت تحسين";
            return function;
        }

        // === Calculate safety score from content ===
        private double CalculateSafetyScore(string content, string path, string fileType)
        {
            double score = 0.7; // baseline

            if (string.IsNullOrEmpty(content))
            {
                // Binary or unreadable
                if (fileType == "EXE" || fileType == "DLL") score = 0.4;
                else if (fileType == "DriverInf") score = 0.35;
                else score = 0.5;
                return score;
            }

            string lower = content.ToLower();

            // Penalty for dangerous keywords
            foreach (string dangerous in DANGEROUS_KEYWORDS)
            {
                if (lower.Contains(dangerous))
                    score -= 0.15;
            }

            // Bonus for safe keywords
            foreach (string safe in SAFE_KEYWORDS)
            {
                if (lower.Contains(safe))
                    score += 0.05;
            }

            // Registry modifications are riskier
            if (fileType == "Registry") score -= 0.05;

            // PowerShell is moderate risk
            if (fileType == "PowerShell") score -= 0.02;

            // Driver files are risky
            if (fileType == "DriverInf" || fileType == "Catalog") score -= 0.1;

            return Math.Max(0.05, Math.Min(1.0, score));
        }

        // === Calculate registry key safety ===
        private double CalculateRegistrySafety(string keyPath)
        {
            double score = 0.65;
            string lower = keyPath.ToLower();

            // System-critical registry paths
            if (lower.Contains("hklm\\system") || lower.Contains("hklm\\security")) score -= 0.3;
            if (lower.Contains("hklm\\software\\microsoft\\windows nt\\currentversion\\winlogon")) score -= 0.2;
            if (lower.Contains("hklm\\software\\microsoft\\windows\\currentversion\\policies")) score -= 0.15;

            // Safer registry paths
            if (lower.Contains("hku\\control panel")) score += 0.1;
            if (lower.Contains("hklm\\software\\microsoft\\windows\\currentversion\\explorer")) score += 0.05;
            if (lower.Contains("hklm\\system\\currentcontrolset\\control\\prioritycontrol")) score += 0.05;

            return Math.Max(0.1, Math.Min(1.0, score));
        }

        // === Calculate command safety ===
        private double CalculateCommandSafety(string command)
        {
            if (string.IsNullOrEmpty(command)) return 0.5;

            double score = 0.7;
            string lower = command.ToLower();

            // Dangerous commands
            if (lower.Contains("reg delete")) score -= 0.25;
            if (lower.Contains("bcdedit")) score -= 0.2;
            if (lower.Contains("diskpart")) score -= 0.3;
            if (lower.Contains("format")) score -= 0.3;
            if (lower.Contains("del /s") || lower.Contains("del /q")) score -= 0.2;
            if (lower.Contains("shutdown") || lower.Contains("reboot")) score -= 0.15;
            if (lower.Contains("takeown")) score -= 0.15;
            if (lower.Contains("icacls")) score -= 0.1;
            if (lower.Contains("net user") || lower.Contains("net localgroup")) score -= 0.2;
            if (lower.Contains("sc delete")) score -= 0.2;

            // Safe commands
            if (lower.Contains("powercfg")) score += 0.1;
            if (lower.Contains("netsh")) score += 0.05;
            if (lower.Contains("tasklist") || lower.Contains("taskkill")) score += 0.0;
            if (lower.Contains("ipconfig /flushdns")) score += 0.1;
            if (lower.Contains("fsutil")) score += 0.0;

            return Math.Max(0.05, Math.Min(1.0, score));
        }

        // === Match content against knowledge base entries ===
        private TweakKnowledgeEntry MatchKnowledgeBase(string content, string path, string fileType)
        {
            if (_knowledge == null) return null;

            string lower = (content + " " + path).ToLower();
            var all = _knowledge.GetAll();
            TweakKnowledgeEntry bestMatch = null;
            int bestScore = 0;

            foreach (var entry in all)
            {
                int score = 0;

                // Match by function keywords
                if (!string.IsNullOrEmpty(entry.Function))
                {
                    string[] words = entry.Function.ToLower().Split(' ');
                    foreach (string word in words)
                    {
                        if (word.Length > 3 && lower.Contains(word)) score++;
                    }
                }

                // Match by category
                if (!string.IsNullOrEmpty(entry.Category) && lower.Contains(entry.Category.ToLower())) score += 2;

                // Match by Windows equivalent command
                if (!string.IsNullOrEmpty(entry.WindowsEquivalent) && lower.Contains(entry.WindowsEquivalent.ToLower())) score += 5;

                // Match by Android equivalent
                if (!string.IsNullOrEmpty(entry.AndroidEquivalent) && lower.Contains(entry.AndroidEquivalent.ToLower())) score += 3;

                // Match by ID keywords
                if (!string.IsNullOrEmpty(entry.Id))
                {
                    string idLower = entry.Id.ToLower().Replace("_", "").Replace("-", "");
                    string pathLower = lower.Replace("_", "").Replace("-", "").Replace(" ", "");
                    if (pathLower.Contains(idLower)) score += 4;
                }

                // Match by file type compatibility
                if (entry.FileType == fileType) score += 1;

                if (score > bestScore && score >= 3)
                {
                    bestScore = score;
                    bestMatch = entry;
                }
            }

            return bestMatch;
        }

        // === Count occurrences of a substring ===
        private int CountOccurrences(string text, string pattern)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern)) return 0;
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(pattern, index, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                count++;
                index += pattern.Length;
            }
            return count;
        }

        // === Get safety level from score ===
        public static string GetSafetyLevel(double score)
        {
            if (score >= 0.8) return "Safe";
            if (score >= 0.6) return "Moderate";
            if (score >= 0.3) return "Risky";
            return "Dangerous";
        }

        // === Get safety level in Arabic ===
        public static string GetSafetyLevelArabic(double score)
        {
            if (score >= 0.8) return "آمن";
            if (score >= 0.6) return "متوسط";
            if (score >= 0.3) return "محفوظ";
            return "خطير";
        }
    }
}
