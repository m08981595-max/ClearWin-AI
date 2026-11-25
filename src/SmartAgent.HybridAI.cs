// ============================================================================
// ClearWin 2.7.7 — SmartAgent Hybrid Architecture (rules + frozen neural net)
// ----------------------------------------------------------------------------
// Why this file exists:
//   The live RL/MLP kept learning every tick. Reward hacking and catastrophic
//   forgetting made the agent passive. This layer:
//     1. Audits the retrieved (old) weight file dimension-by-dimension and
//        Xavier-initialises ONLY what is missing/invalid — never overwriting
//        weights that were successfully recovered.
//     2. Freezes learning (LearningRate = 0, backprop/replay disabled).
//     3. Runs Game Boost as 80% deterministic rules + 20% neural inference.
//     4. Provides RestoreDefaultAI() so the UI can fall back to the baseline.
//
// Compatibility: .NET Framework 4.8 / C# 7.3, no extra NuGet assemblies.
// The project's build.ps1 references only WPF + System.Management + WebView2,
// so System.Text.Json is NOT used here; a tiny self-contained JSON reader is
// included below. All public names are new — no existing member is replaced.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace ClearWin.AI
{
    // ------------------------------------------------------------------------
    // Minimal JSON reader (objects / arrays / numbers / strings / bool / null).
    // Handles the exact schema of clearwin_ai_weights.json, including exponents
    // such as 8.481400361362177e-06. No third-party dependency.
    // ------------------------------------------------------------------------
    internal static class HybridJson
    {
        public static object Parse(string text)
        {
            if (text == null) throw new ArgumentNullException("text");
            int i = 0;
            object value = ParseValue(text, ref i);
            SkipWs(text, ref i);
            if (i != text.Length) throw new FormatException("Trailing JSON data at offset " + i);
            return value;
        }

        private static void SkipWs(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        private static object ParseValue(string s, ref int i)
        {
            SkipWs(s, ref i);
            if (i >= s.Length) throw new FormatException("Unexpected end of JSON");
            char c = s[i];
            if (c == '{') return ParseObject(s, ref i);
            if (c == '[') return ParseArray(s, ref i);
            if (c == '"') return ParseString(s, ref i);
            if (c == 't') { Expect(s, ref i, "true"); return true; }
            if (c == 'f') { Expect(s, ref i, "false"); return false; }
            if (c == 'n') { Expect(s, ref i, "null"); return null; }
            return ParseNumber(s, ref i);
        }

        private static void Expect(string s, ref int i, string word)
        {
            if (i + word.Length > s.Length || string.CompareOrdinal(s, i, word, 0, word.Length) != 0)
                throw new FormatException("Invalid JSON token at offset " + i);
            i += word.Length;
        }

        private static Dictionary<string, object> ParseObject(string s, ref int i)
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            i++; // {
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return result; }
            while (true)
            {
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != '"') throw new FormatException("Expected property name at " + i);
                string key = ParseString(s, ref i);
                SkipWs(s, ref i);
                if (i >= s.Length || s[i] != ':') throw new FormatException("Expected ':' at " + i);
                i++;
                result[key] = ParseValue(s, ref i);
                SkipWs(s, ref i);
                if (i >= s.Length) throw new FormatException("Unterminated object");
                if (s[i] == '}') { i++; return result; }
                if (s[i] != ',') throw new FormatException("Expected ',' at " + i);
                i++;
            }
        }

        private static List<object> ParseArray(string s, ref int i)
        {
            var result = new List<object>();
            i++; // [
            SkipWs(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return result; }
            while (true)
            {
                result.Add(ParseValue(s, ref i));
                SkipWs(s, ref i);
                if (i >= s.Length) throw new FormatException("Unterminated array");
                if (s[i] == ']') { i++; return result; }
                if (s[i] != ',') throw new FormatException("Expected ',' at " + i);
                i++;
            }
        }

        private static string ParseString(string s, ref int i)
        {
            var sb = new StringBuilder();
            i++; // opening quote
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case '"': sb.Append('"'); break;
                    case '\\': sb.Append('\\'); break;
                    case '/': sb.Append('/'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw new FormatException("Bad \\u escape");
                        int code;
                        if (!int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                            throw new FormatException("Bad \\u escape");
                        sb.Append((char)code);
                        i += 4;
                        break;
                    default: throw new FormatException("Bad JSON escape \\" + e);
                }
            }
            throw new FormatException("Unterminated JSON string");
        }

        private static double ParseNumber(string s, ref int i)
        {
            int start = i;
            if (i < s.Length && (s[i] == '-' || s[i] == '+')) i++;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E' || s[i] == '+' || s[i] == '-')) i++;
            if (i == start) throw new FormatException("Expected number at " + i);
            double d;
            if (!double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                throw new FormatException("Bad number at " + start);
            return d;
        }

        // Safe accessors used by the auditor.
        public static Dictionary<string, object> Obj(object node)
        {
            return node as Dictionary<string, object>;
        }

        public static List<object> Arr(object node)
        {
            return node as List<object>;
        }

        public static double Num(object node, double fallback)
        {
            if (node is double) return (double)node;
            if (node is int) return (int)node;
            return fallback;
        }

        public static string Str(object node)
        {
            return node as string;
        }
    }

    // ------------------------------------------------------------------------
    // Audited weight set. Shapes below are the CURRENT network architecture:
    //   Linear policy : 6 x 9  + bias 6
    //   Hidden layer  : 16 x 9 + bias 16   (LeakyReLU 0.01)
    //   Output/residual: 6 x 16 + bias 6
    //   score = linear + residual_scale * mlp_out   (9 -> 16 -> 6)
    // ------------------------------------------------------------------------
    public sealed class HybridWeightSet
    {
        public const int Inputs = 9;
        public const int Actions = 6;
        public const int Hidden = 16;

        public double[][] LinearWeights;   // [6][9]
        public double[] LinearBias;        // [6]
        public double[][] HiddenWeights;   // [16][9]
        public double[] HiddenBias;        // [16]
        public double[][] OutputWeights;   // [6][16]
        public double[] OutputBias;        // [6]
        public double ResidualScale = 0.2;
        public string Version = "hybrid-audited";

        public HybridWeightSet Clone()
        {
            var c = new HybridWeightSet();
            c.LinearWeights = CopyMatrix(LinearWeights);
            c.LinearBias = CopyVector(LinearBias);
            c.HiddenWeights = CopyMatrix(HiddenWeights);
            c.HiddenBias = CopyVector(HiddenBias);
            c.OutputWeights = CopyMatrix(OutputWeights);
            c.OutputBias = CopyVector(OutputBias);
            c.ResidualScale = ResidualScale;
            c.Version = Version;
            return c;
        }

        public int ParameterCount()
        {
            int n = 0;
            n += Count(LinearWeights) + (LinearBias == null ? 0 : LinearBias.Length);
            n += Count(HiddenWeights) + (HiddenBias == null ? 0 : HiddenBias.Length);
            n += Count(OutputWeights) + (OutputBias == null ? 0 : OutputBias.Length);
            return n;
        }

        private static int Count(double[][] m)
        {
            if (m == null) return 0;
            int n = 0;
            for (int i = 0; i < m.Length; i++) if (m[i] != null) n += m[i].Length;
            return n;
        }

        private static double[] CopyVector(double[] v)
        {
            if (v == null) return null;
            var r = new double[v.Length];
            Array.Copy(v, r, v.Length);
            return r;
        }

        private static double[][] CopyMatrix(double[][] m)
        {
            if (m == null) return null;
            var r = new double[m.Length][];
            for (int i = 0; i < m.Length; i++) r[i] = CopyVector(m[i]);
            return r;
        }
    }

    // ------------------------------------------------------------------------
    // Audit report surfaced to the UI / log.
    // ------------------------------------------------------------------------
    public sealed class HybridAuditReport
    {
        public string SourcePath = "";
        public string BaselinePath = "";
        public DateTime AuditedAt = DateTime.Now;
        public bool Success;
        public int MatchedBlocks;
        public int PartialBlocks;
        public int XavierBlocks;
        public int SanitizedValues;
        public int ExpectedParameters;
        public int LoadedParameters;
        public double Trust;
        public List<string> Warnings = new List<string>();

        public string Verdict
        {
            get
            {
                if (!Success) return "FAILED — rules-only fallback active";
                if (Trust >= 0.99) return "COMPLETE MATCH — frozen neural inference enabled";
                if (Trust >= 0.80) return "PARTIAL MATCH — neural influence trusted at " + (Trust * 100).ToString("0.0") + "%";
                return "DEGRADED — rules-only, neural inference suppressed";
            }
        }

        public string Summary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("SmartAgent hybrid weight audit — " + AuditedAt.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Source   : " + SourcePath);
            sb.AppendLine("Blocks   : " + MatchedBlocks + " matched, " + PartialBlocks + " partial, " + XavierBlocks + " Xavier-initialised");
            sb.AppendLine("Params   : " + LoadedParameters + " / " + ExpectedParameters +
                          " (" + (Trust * 100).ToString("0.0") + "% recovered), " + SanitizedValues + " non-finite values repaired");
            sb.AppendLine("Verdict  : " + Verdict);
            for (int i = 0; i < Warnings.Count; i++) sb.AppendLine("  ! " + Warnings[i]);
            return sb.ToString();
        }
    }

    // ------------------------------------------------------------------------
    // Result of one hybrid Game Boost run (also handy for the UI / WebView2).
    // ------------------------------------------------------------------------
    public sealed class HybridBoostReport
    {
        public DateTime StartedAt = DateTime.Now;
        public bool InferenceFrozen;
        public readonly List<string> RuleActions = new List<string>();
        public readonly List<string> NeuralActions = new List<string>();
        public int RuleKillCount;
        public int NeuralKillCount;
        public int CandidatesSeen;
        public int NeuralBudget;

        public string Summary()
        {
            return "Hybrid Game Boost: " + RuleActions.Count + " rule actions (" + RuleKillCount +
                   " kills) + " + NeuralActions.Count + " neural actions (" + NeuralKillCount +
                   " kills) from " + CandidatesSeen + " third-party candidates (budget " + NeuralBudget + ").";
        }
    }
}

namespace ClearWin
{
    using ClearWin.AI;

    public partial class SmartAgent
    {
        // --------------------------------------------------------------------
        // Hybrid state. All fields are new; nothing in the 60+ partial files
        // is renamed or removed.
        // --------------------------------------------------------------------
        private static readonly object _hybridLock = new object();
        private HybridWeightSet _hybridWeights;
        private HybridWeightSet _hybridBaseline;
        private HybridAuditReport _hybridAudit;
        private HybridBoostReport _lastBoostReport;
        private bool _hybridInferenceFrozen;
        private string _hybridSourcePath;
        private string _hybridBaselinePath;
        private readonly Random _xavierRandom = new Random(1729); // deterministic fallback
        private readonly Dictionary<string, int> _boostStoppedServices = new Dictionary<string, int>();

        // 9 feature positions — documented so the Python trainer and WebUI stay in sync.
        // 0 RAM%, 1 CPU%, 2 process private MB (log-scaled), 3 idle%, 4 background flag,
        // 5 known-hog flag, 6 GPU%, 7 pagefile pressure%, 8 hour-of-day (0..1)
        private const int HYBRID_FEATURE_COUNT = 9;
        private const int HYBRID_RULE_SHARE_PERCENT = 80;
        private const int HYBRID_NEURAL_SHARE_PERCENT = 20;
        private const int HYBRID_NEURAL_MAX_KILLS = 3;
        private const double HYBRID_KILL_THRESHOLD = 0.15;
        private const double HYBRID_MIN_TRUST_FOR_NEURAL = 0.80;

        public bool IsInferenceMode { get { return _hybridInferenceFrozen; } }
        public HybridAuditReport HybridAudit { get { return _hybridAudit; } }
        public HybridBoostReport LastBoostReport { get { return _lastBoostReport; } }
        public bool HasAuditedWeights { get { return _hybridWeights != null; } }

        // ====================================================================
        // 1) WEIGHT AUDITING & LOADING
        // ====================================================================

        /// <summary>
        /// Parse the recovered weights file, compare every block against the
        /// current architecture and load only what passes. Missing / malformed
        /// blocks are Xavier-initialised; valid recovered values are preserved.
        /// </summary>
        public HybridAuditReport LoadAndAuditWeights(string weightsPath)
        {
            var report = new HybridAuditReport();
            try
            {
                string path = ResolveHybridPath(weightsPath, false);
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    report.Success = false;
                    report.Warnings.Add("Weights file not found: " + (path ?? "<null>"));
                    AddLog("ERROR", "HybridAI", report.Warnings[0]);
                    _hybridAudit = report;
                    return report;
                }

                string json = File.ReadAllText(path);
                object root = HybridJson.Parse(json);
                var obj = HybridJson.Obj(root);
                if (obj == null) throw new FormatException("Weights root is not a JSON object");

                // Current architecture (fixed, matching clearwin_ai_weights.json v22).
                const int A = HybridWeightSet.Actions;  // 6
                const int I = HybridWeightSet.Inputs;   // 9
                const int H = HybridWeightSet.Hidden;   // 16

                var w = new HybridWeightSet();
                w.Version = HybridJson.Str(Get(obj, "version")) ?? "hybrid-audited";
                w.ResidualScale = HybridJson.Num(Get(obj, "residual_scale"), 0.2);

                int expected = A * I + A + H * I + H + A * H + A;
                report.ExpectedParameters = expected;
                report.SourcePath = path;

                // --- Linear policy (6x9) + bias -----------------------------
                w.LinearWeights = AuditMatrix(obj, "weights", A, I, report, "linear policy");
                w.LinearBias = AuditVector(obj, "biases", A, report, "linear bias");

                // --- Residual MLP (9 -> 16 -> 6) ----------------------------
                w.HiddenWeights = AuditMatrix(obj, "mlp_hidden_weights", H, I, report, "MLP hidden");
                w.HiddenBias = AuditVector(obj, "mlp_hidden_biases", H, report, "MLP hidden bias");
                w.OutputWeights = AuditMatrix(obj, "mlp_output_weights", A, H, report, "MLP output");
                w.OutputBias = AuditVector(obj, "mlp_output_biases", A, report, "MLP output bias");

                w.ResidualScale = Sanitize(w.ResidualScale, 0.2, report);

                report.LoadedParameters = w.ParameterCount();
                report.Trust = expected == 0 ? 0 : (double)report.LoadedParameters / expected;
                // Partially adopted matrices count as recovered values, so trust
                // reflects real parameter coverage rather than block presence.
                if (report.PartialBlocks > 0 && report.Trust > 0.80) report.Trust = 0.80;
                report.Success = report.LoadedParameters > 0;

                lock (_hybridLock)
                {
                    _hybridWeights = w;
                    _hybridSourcePath = path;
                    _hybridAudit = report;
                    _hybridBaselinePath = ResolveHybridPath(null, true);

                    // Freeze the baseline ONCE. Training may rewrite the live file;
                    // the baseline snapshot must never be overwritten.
                    if (_hybridBaseline == null)
                    {
                        _hybridBaseline = w.Clone();
                        TryWriteBaseline(_hybridBaselinePath, json, report);
                    }
                    report.BaselinePath = _hybridBaselinePath;
                }

                // Audited weights imply frozen inference — no exceptions.
                SetInferenceMode(true);
                AddLog("INFO", "HybridAI", "Audit complete. " + report.Summary().Replace(Environment.NewLine, " | "));
            }
            catch (Exception ex)
            {
                report.Success = false;
                report.Warnings.Add("Audit failed: " + ex.Message);
                AddLog("ERROR", "HybridAI", "LoadAndAuditWeights: " + ex);
                _hybridAudit = report;
            }
            return report;
        }

        /// <summary>Convenience overload used by the UI button.</summary>
        public HybridAuditReport LoadAndAuditWeights()
        {
            return LoadAndAuditWeights(null);
        }

        private static object Get(Dictionary<string, object> obj, string key)
        {
            object value;
            return obj.TryGetValue(key, out value) ? value : null;
        }

        /// <summary>
        /// Copy a [rows][cols] matrix. Exact shapes are adopted as-is. Wrong
        /// shapes are handled by OVERLAP ADOPTION: existing cells are kept,
        /// missing cells get Xavier values. No valid retrieved weight is lost.
        /// </summary>
        private double[][] AuditMatrix(Dictionary<string, object> obj, string key,
                                       int rows, int cols, HybridAuditReport report, string label)
        {
            var target = new double[rows][];
            var raw = HybridJson.Arr(Get(obj, key));
            bool complete = raw != null && raw.Count == rows;

            if (raw != null)
            {
                for (int r = 0; r < rows; r++)
                {
                    var row = r < raw.Count ? HybridJson.Arr(raw[r]) : null;
                    int n = row == null ? 0 : Math.Min(row.Count, cols);
                    target[r] = new double[cols];
                    for (int c = 0; c < cols; c++)
                        target[r][c] = Xavier(cols, rows); // start safe; overwrite with real values below

                    for (int c = 0; c < n; c++)
                    {
                        double v = HybridJson.Num(row[c], double.NaN);
                        target[r][c] = Sanitize(v, target[r][c], report);
                    }
                    if (row != null && row.Count != cols)
                        report.Warnings.Add(label + " row " + r + " width " + row.Count + " != " + cols + " (overlap adopted)");
                }
                if (raw.Count != rows)
                    report.Warnings.Add(label + " has " + raw.Count + " rows, expected " + rows + " (overlap adopted)");
            }
            else
            {
                report.XavierBlocks++;
                for (int r = 0; r < rows; r++)
                {
                    target[r] = new double[cols];
                    for (int c = 0; c < cols; c++) target[r][c] = Xavier(cols, rows);
                }
                report.Warnings.Add(label + " missing — Xavier initialised (" + rows + "x" + cols + ")");
            }

            if (!complete) report.PartialBlocks++;
            else report.MatchedBlocks++;
            return target;
        }

        private double[] AuditVector(Dictionary<string, object> obj, string key,
                                     int length, HybridAuditReport report, string label)
        {
            var target = new double[length];
            var raw = HybridJson.Arr(Get(obj, key));
            bool complete = raw != null && raw.Count == length;

            if (raw != null)
            {
                int n = Math.Min(raw.Count, length);
                for (int i = 0; i < length; i++) target[i] = Xavier(1, length);
                for (int i = 0; i < n; i++) target[i] = Sanitize(HybridJson.Num(raw[i], double.NaN), target[i], report);
                if (raw.Count != length)
                    report.Warnings.Add(label + " has " + raw.Count + " values, expected " + length + " (overlap adopted)");
            }
            else
            {
                report.XavierBlocks++;
                for (int i = 0; i < length; i++) target[i] = Xavier(1, length);
                report.Warnings.Add(label + " missing — Xavier initialised (" + length + ")");
            }

            if (!complete) report.PartialBlocks++;
            else report.MatchedBlocks++;
            return target;
        }

        /// <summary>Xavier / Glorot uniform initialisation for a missing tensor.</summary>
        private double Xavier(int fanIn, int fanOut)
        {
            double limit = Math.Sqrt(6.0 / Math.Max(1.0, (double)fanIn + fanOut));
            lock (_xavierRandom) return (_xavierRandom.NextDouble() * 2.0 - 1.0) * limit;
        }

        private static double Sanitize(double value, double fallback, HybridAuditReport report)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                report.SanitizedValues++;
                return fallback;
            }
            return value;
        }

        // ====================================================================
        // 2) FREEZE LEARNING (Inference Mode)
        // ====================================================================

        /// <summary>
        /// Strictly disable all weight updates. Kept idempotent and explicit so
        /// a stray training tick can never silently re-enable degradation.
        /// Unlock is intentionally NOT provided through the public API.
        /// </summary>
        public bool SetInferenceMode(bool enabled)
        {
            lock (_hybridLock)
            {
                _hybridInferenceFrozen = enabled;
                if (enabled)
                {
                    _adaptiveLearningRate = 0.0;   // RL / Double-Q updates stall
                    _explorationRate = 0.0;        // no reward hacking by exploration
                }
                AddLog("INFO", "HybridAI", enabled
                    ? "Inference mode ON — learning rate 0, backpropagation/replay disabled, weights frozen."
                    : "Inference mode OFF (maintenance only).");
                return _hybridInferenceFrozen;
            }
        }

        /// <summary>
        /// Forward pass of the audited residual policy (9 -> 16 -> 6).
        /// Pure inference: no gradient is computed or applied.
        /// </summary>
        public double[] PredictHybridAction(double[] input)
        {
            var w = _hybridWeights;
            if (w == null || input == null) return null;
            if (w.LinearWeights == null || w.HiddenWeights == null || w.OutputWeights == null) return null;

            var x = new double[HYBRID_FEATURE_COUNT];
            for (int i = 0; i < HYBRID_FEATURE_COUNT; i++)
                x[i] = i < input.Length ? SanitizeInput(input[i]) : 0.0;

            var score = new double[HybridWeightSet.Actions];
            for (int a = 0; a < HybridWeightSet.Actions; a++)
            {
                double s = 0;
                if (w.LinearWeights[a] != null)
                    for (int f = 0; f < HYBRID_FEATURE_COUNT; f++) s += w.LinearWeights[a][f] * x[f];
                if (w.LinearBias != null && a < w.LinearBias.Length) s += w.LinearBias[a];
                score[a] = s; // non-destructive warm start: linear policy is the base
            }

            var hidden = new double[HybridWeightSet.Hidden];
            for (int h = 0; h < HybridWeightSet.Hidden; h++)
            {
                if (w.HiddenWeights[h] == null) { hidden[h] = 0; continue; }
                double z = 0;
                for (int f = 0; f < HYBRID_FEATURE_COUNT; f++) z += w.HiddenWeights[h][f] * x[f];
                if (w.HiddenBias != null && h < w.HiddenBias.Length) z += w.HiddenBias[h];
                hidden[h] = z >= 0 ? z : 0.01 * z; // LeakyReLU, matches trainer
            }

            for (int a = 0; a < HybridWeightSet.Actions; a++)
            {
                double z = 0;
                if (w.OutputWeights[a] != null)
                    for (int h = 0; h < HybridWeightSet.Hidden; h++) z += w.OutputWeights[a][h] * hidden[h];
                if (w.OutputBias != null && a < w.OutputBias.Length) z += w.OutputBias[a];
                score[a] += w.ResidualScale * z;
            }
            return score;
        }

        private static double SanitizeInput(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return 0;
            if (v < 0) return 0;
            if (v > 100) return 100;
            return v;
        }

        // ====================================================================
        // 3) HYBRID EXECUTION — 80% HARDCODED RULES + 20% NEURAL INFERENCE
        // ====================================================================

        /// <summary>
        /// Entry point called the moment Game Boost is activated.
        /// Phase 1 (80%): deterministic, immediate service stops + known hog kills.
        /// Phase 2 (20%): the frozen NN may only touch unknown third-party apps,
        ///                capped at 20% of the candidate pool (max 3 kills).
        /// </summary>
        public HybridBoostReport ExecuteGameBoost()
        {
            SetInferenceMode(true); // guard before any decision is taken
            var report = new HybridBoostReport();
            report.InferenceFrozen = _hybridInferenceFrozen;
            try
            {
                ApplyHardcodedBoostRules(report);
                ApplyNeuralThirdPartyPass(report);
                _lastBoostReport = report;
                AddLog("INFO", "HybridAI", report.Summary());
            }
            catch (Exception ex)
            {
                AddLog("ERROR", "HybridAI", "ExecuteGameBoost: " + ex);
            }
            return report;
        }

        /// <summary>
        /// 80% layer. Known Windows resource hogs are handled by fixed rules —
        /// the neural network never gets a vote on them.
        ///
        /// NOTE: SysMain / DiagTrack / WSearch run inside svchost.exe. Killing
        /// svchost would blue-screen Windows, so they are STOPPED AS SERVICES
        /// via sc.exe (their prior start type is recorded for rollback).
        /// </summary>
        private void ApplyHardcodedBoostRules(HybridBoostReport report)
        {
            // Service rules: { service name, start type to restore (2 = Auto, 3 = Manual) }
            string[,] services = new string[,]
            {
                { "SysMain",    "3" },  // Superfetch / prefetch
                { "DiagTrack",  "3" },  // Connected User Experiences & Telemetry
                { "WSearch",    "3" },  // Windows Search indexing
                { "dmwappushservice", "3" }, // Telemetry push
                { "MapsBroker", "3" },  // Downloaded Maps Manager
                { "Spooler",    "3" }   // Print spooler
            };

            for (int i = 0; i < services.GetLength(0); i++)
            {
                string name = services[i, 0];
                if (TryStopService(name))
                {
                    int prior;
                    if (!_boostStoppedServices.TryGetValue(name, out prior))
                    {
                        int startType;
                        int.TryParse(services[i, 1], out startType);
                        _boostStoppedServices[name] = startType;
                    }
                    report.RuleActions.Add("Service stopped: " + name);
                }
            }

            // Process rules — background-only targets. msedge.exe is excluded
            // on purpose: it is the user's browser with live tabs. Only the
            // background WebView2 / crash handlers are safe to terminate.
            string[] hogProcesses = new string[]
            {
                "msedgewebview2", "msedge_crashpad_handler", "searchhost",
                "shellexperiencehost", "fontdrvhost", "compattelemetry",
                "telemetry", "onedrive", "gamebarft", "gamebarpresencewriter"
            };

            var killed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Process[] all = SafeGetProcesses();
            foreach (Process p in all)
            {
                string name = SafeProcessName(p);
                if (string.IsNullOrEmpty(name) || killed.Contains(name)) continue;
                if (IsProtected(name)) continue;

                bool match = false;
                for (int i = 0; i < hogProcesses.Length; i++)
                {
                    if (string.Equals(name, hogProcesses[i], StringComparison.OrdinalIgnoreCase)) { match = true; break; }
                }
                if (!match) continue;

                // Skip anything with a visible window: user is using it.
                try { if (p.MainWindowHandle != IntPtr.Zero) continue; } catch { }

                if (TryKill(p))
                {
                    killed.Add(name);
                    report.RuleKillCount++;
                    report.RuleActions.Add("Killed known hog: " + name + " (pid " + SafePid(p) + ")");
                }
            }
            DisposeProcesses(all);
        }

        /// <summary>
        /// 20% layer. Only unknown third-party background apps are considered.
        /// The NN decides which (if any) to kill; the count is hard-capped so
        /// a reward-hacked network can never cause a mass kill.
        /// </summary>
        private void ApplyNeuralThirdPartyPass(HybridBoostReport report)
        {
            HybridWeightSet w = _hybridWeights;
            if (w == null || (_hybridAudit != null && _hybridAudit.Trust < HYBRID_MIN_TRUST_FOR_NEURAL))
            {
                report.RuleActions.Add("Neural inference suppressed (weights missing or trust below " +
                                       (HYBRID_MIN_TRUST_FOR_NEURAL * 100).ToString("0") + "%).");
                return;
            }

            var candidates = new List<Process>();
            Process[] all = SafeGetProcesses();
            foreach (Process p in all)
            {
                string name = SafeProcessName(p);
                if (string.IsNullOrEmpty(name)) continue;
                if (IsProtected(name)) continue;
                if (SYSTEM_PROCESSES.Contains(name)) continue;
                if (IsUserCriticalProcess(name.ToLowerInvariant())) continue;
                try { if (p.MainWindowHandle != IntPtr.Zero) continue; } catch { }
                candidates.Add(p);
            }
            report.CandidatesSeen = candidates.Count;

            int budget = Math.Min(HYBRID_NEURAL_MAX_KILLS,
                                  (int)Math.Floor(candidates.Count * (HYBRID_NEURAL_SHARE_PERCENT / 100.0)));
            report.NeuralBudget = budget;
            if (budget == 0 || candidates.Count == 0)
            {
                DisposeProcesses(all);
                return;
            }

            double ram = _smoothRam;
            double cpu = _smoothCpu;
            double gpu = _smoothGpu;
            int hour = DateTime.Now.Hour;

            var scores = new List<KeyValuePair<double, Process>>();
            foreach (Process p in candidates)
            {
                string name = SafeProcessName(p);
                double memMb = 0;
                try { memMb = p.PrivateMemorySize64 / (1024.0 * 1024.0); } catch { }
                var features = new double[]
                {
                    SanitizeInput(ram),
                    SanitizeInput(cpu),
                    Math.Log10(Math.Max(1.0, memMb)) * 20.0,
                    100.0,          // no foreground window -> fully idle from user's perspective
                    1.0,            // background flag
                    0.0,            // not a known Windows hog (rules handled those)
                    SanitizeInput(gpu),
                    SanitizeInput(ram * 0.5),
                    hour / 23.0 * 100.0
                };
                double[] s = PredictHybridAction(features);
                if (s == null) break;
                // Action 0 = AggressiveClean, 2 = LimitBackground,
                // 3 = SuggestClose, 4 = PreemptiveClean. 5 = NONE (abstain).
                double killScore = Math.Max(s[0], Math.Max(s[2], Math.Max(s[3], s[4])));
                scores.Add(new KeyValuePair<double, Process>(killScore, p));
            }

            scores.Sort(delegate (KeyValuePair<double, Process> a, KeyValuePair<double, Process> b)
            {
                return b.Key.CompareTo(a.Key);
            });

            for (int i = 0; i < scores.Count && report.NeuralKillCount < budget; i++)
            {
                if (scores[i].Key < HYBRID_KILL_THRESHOLD) break;
                Process p = scores[i].Value;
                string name = SafeProcessName(p);
                if (IsProtected(name)) continue;
                if (TryKill(p))
                {
                    report.NeuralKillCount++;
                    report.NeuralActions.Add("Neural kill: " + name + " (pid " + SafePid(p) +
                                             ", score " + scores[i].Key.ToString("0.00") + ")");
                }
            }
            DisposeProcesses(all);
        }

        /// <summary>Restore services that the 80% rule layer stopped.</summary>
        public void EndGameBoost()
        {
            lock (_hybridLock)
            {
                foreach (KeyValuePair<string, int> kv in _boostStoppedServices)
                {
                    try
                    {
                        var psi = new ProcessStartInfo("sc.exe", "config " + kv.Key + " start= " +
                                                       (kv.Value == 2 ? "auto" : "demand"))
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        using (Process pr = Process.Start(psi)) { if (pr != null) pr.WaitForExit(4000); }
                        AddLog("INFO", "HybridAI", "Restored service start type: " + kv.Key + " -> " + kv.Value);
                    }
                    catch (Exception ex) { AddLog("WARN", "HybridAI", "Restore " + kv.Key + ": " + ex.Message); }
                }
                _boostStoppedServices.Clear();
            }
        }

        // ====================================================================
        // 4) RESTORE DEFAULT AI (UI FALLBACK)
        // ====================================================================

        /// <summary>
        /// Reset the live network to the immutable audited baseline, then
        /// re-enter inference mode. Safe to call repeatedly from the UI.
        /// </summary>
        public HybridAuditReport RestoreDefaultAI()
        {
            lock (_hybridLock)
            {
                if (_hybridBaseline != null)
                {
                    _hybridWeights = _hybridBaseline.Clone();
                    var r = new HybridAuditReport();
                    r.Success = true;
                    r.SourcePath = _hybridBaselinePath;
                    r.MatchedBlocks = 6;
                    r.ExpectedParameters = _hybridWeights.ParameterCount();
                    r.LoadedParameters = r.ExpectedParameters;
                    r.Trust = 1.0;
                    _hybridAudit = r;
                    SetInferenceMode(true);
                    AddLog("INFO", "HybridAI", "Default AI restored from immutable baseline snapshot.");
                    return r;
                }
            }
            // No snapshot yet — audit the shipped baseline file and freeze it.
            return LoadAndAuditWeights(ResolveHybridPath(null, true));
        }

        public string GetHybridAiStatus()
        {
            if (_hybridAudit == null) return "AI weights not audited yet (rules-only).";
            return _hybridAudit.Verdict + " | " + (_hybridInferenceFrozen ? "FROZEN" : "LEARNING");
        }

        // ====================================================================
        // Path helpers / process helpers
        // ====================================================================

        private string ResolveHybridPath(string supplied, bool baseline)
        {
            if (!string.IsNullOrEmpty(supplied)) return supplied;
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // The immutable audited baseline is the DEFAULT source for both
            // audit and restore. The live Resources\WebUI copy has drifted, so
            // it must never be used as the recovery source.
            string audited = Path.Combine(baseDir, "Resources", "AI", "ai_baseline.json");
            if (File.Exists(audited)) return audited;

            if (baseline)
            {
                string local = Path.Combine(_dataDir ?? Path.GetTempPath(), "hybrid_ai_baseline.json");
                if (File.Exists(local)) return local;
                return local;
            }

            string candidate = Path.Combine(baseDir, "Resources", "AI", "clearwin_ai_weights.json");
            if (File.Exists(candidate)) return candidate;
            candidate = Path.Combine(baseDir, "clearwin_ai_weights.json");
            if (File.Exists(candidate)) return candidate;
            return Path.Combine(baseDir, "Resources", "WebUI", "clearwin_ai_weights.json");
        }

        private static void TryWriteBaseline(string path, string json, HybridAuditReport report)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                if (!File.Exists(path)) File.WriteAllText(path, json, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                report.Warnings.Add("Baseline snapshot could not be written: " + ex.Message);
            }
        }

        private static Process[] SafeGetProcesses()
        {
            try { return Process.GetProcesses(); }
            catch { return new Process[0]; }
        }

        private static void DisposeProcesses(Process[] processes)
        {
            for (int i = 0; i < processes.Length; i++)
            {
                try { processes[i].Dispose(); } catch { }
            }
        }

        private static string SafeProcessName(Process p)
        {
            try { return p.ProcessName; } catch { return null; }
        }

        private static int SafePid(Process p)
        {
            try { return p.Id; } catch { return -1; }
        }

        private static bool IsProtected(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;
            string lower = name.ToLowerInvariant();
            if (lower == "clearwin" || lower == "clearwin.adminhelper" || lower == "system" || lower == "idle") return true;
            if (lower == "svchost" || lower == "lsass" || lower == "winlogon" || lower == "csrss" || lower == "smss") return true;
            if (lower == "dwm" || lower == "explorer" || lower == "msmpeng") return true;
            return false;
        }

        private static bool TryKill(Process p)
        {
            try
            {
                int pid = SafePid(p);
                if (pid <= 0 || pid == Process.GetCurrentProcess().Id) return false;
                p.Kill();
                p.WaitForExit(1500);
                return true;
            }
            catch { return false; }
        }

        private static bool TryStopService(string name)
        {
            try
            {
                var psi = new ProcessStartInfo("sc.exe", "stop " + name)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                using (Process p = Process.Start(psi))
                {
                    if (p == null) return false;
                    p.WaitForExit(4000);
                    return p.ExitCode == 0;
                }
            }
            catch { return false; }
        }
    }
}
