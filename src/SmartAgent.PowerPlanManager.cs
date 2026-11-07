using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace ClearWin
{
    public partial class SmartAgent
    {
        // === Power Plan Manager Fields ===
        private string _currentPowerPlan = "balanced";
        private string _originalPowerPlan = "balanced";
        private bool _powerPlanAutoSwitch = true;
        private DateTime _lastPowerPlanCheck = DateTime.MinValue;
        private const int POWER_PLAN_CHECK_INTERVAL_SECONDS = 30;
        private static DateTime _lastPowerPlanSwitchExecution = DateTime.MinValue;
        private static string _lastAppliedPowerPlanGuid = "";
        private static readonly object _powerPlanSwitchLock = new object();
        public const double POWER_SWITCH_COOLDOWN_SECONDS = 10.0;
        private DateTime _lastGamingStart = DateTime.MinValue;
        private DateTime _lastChargingCheck = DateTime.MinValue;

        // Power plan state tracking
        private List<PowerPlanSwitch> _powerPlanHistory = new List<PowerPlanSwitch>();
        private const int MAX_POWER_PLAN_HISTORY = 50;

        // === Public Accessors ===
        public string CurrentPowerPlan { get { return _currentPowerPlan; } }
        public bool PowerPlanAutoSwitchEnabled { get { return _powerPlanAutoSwitch; } set { _powerPlanAutoSwitch = value; } }
        public List<PowerPlanSwitch> PowerPlanHistory { get { return new List<PowerPlanSwitch>(_powerPlanHistory); } }

        public enum PowerPlanState
        {
            HighPerformance,
            Balanced,
            PowerSaver,
            Ultimate,
            Custom
        }

        public class PowerPlanSwitch
        {
            public DateTime Timestamp;
            public string FromPlan;
            public string ToPlan;
            public string Reason;
            public double CpuUsage;
            public double BatteryPercent;
            public bool IsOnBattery;
            public bool IsGame;
        }

        /// <summary>
        /// Checks whether the active power plan already satisfies the recommended target plan.
        /// Prevents repeated 30-second SwitchPowerPlan oscillation on systems where "ultimate"
        /// resolves to the High Performance plan GUID (8c5e7fda-...) because Ultimate Performance
        /// is not installed.
        /// </summary>
        internal static bool IsPowerPlanSatisfied(string currentPlan, string targetPlan, bool hasTrueUltimatePlan = false)
        {
            string cur = (currentPlan ?? "balanced").Trim().ToLowerInvariant();
            string tgt = (targetPlan ?? "balanced").Trim().ToLowerInvariant();
            if (cur == tgt) return true;

            // If target is "ultimate" or "high", and the system is already in "ultimate" or
            // (in "high" when no distinct Ultimate Performance scheme is installed), it is satisfied.
            if ((tgt == "ultimate" || tgt == "high") && (cur == "ultimate" || (cur == "high" && (!hasTrueUltimatePlan || tgt == "high"))))
            {
                return true;
            }
            return false;
        }

        internal static bool SystemHasDedicatedUltimatePlan()
        {
            string bestGuid = FindBestPerformancePlanGuid();
            if (string.IsNullOrEmpty(bestGuid)) return false;
            return !bestGuid.Equals("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", StringComparison.OrdinalIgnoreCase) &&
                   !bestGuid.Equals("381b4222-f694-41f0-9685-ff5bb260df2e", StringComparison.OrdinalIgnoreCase);
        }

        // === Power Plan: Analyze and generate decisions (single unified decision emitter) ===
        public void AnalyzePowerPlan(List<AgentDecision> decisions)
        {
            if (!_powerPlanAutoSwitch) return;
            if ((DateTime.Now - _lastPowerPlanCheck).TotalSeconds < POWER_PLAN_CHECK_INTERVAL_SECONDS) return;
            _lastPowerPlanCheck = DateTime.Now;

            try
            {
                // Detect power plan changes
                string detectedPlan = DetectCurrentPowerPlan();
                if (!string.IsNullOrEmpty(detectedPlan) && detectedPlan != _currentPowerPlan)
                {
                    // Only update original baseline when NOT in gaming mode and when detected plan is not ultimate/high
                    if (_appMode != AppMode.Gaming && detectedPlan != "ultimate" && detectedPlan != "high")
                    {
                        _originalPowerPlan = detectedPlan;
                    }
                    _currentPowerPlan = detectedPlan;
                }

                // Respect global power switch cooldown
                if ((DateTime.UtcNow - _lastPowerPlanSwitchExecution).TotalSeconds < POWER_SWITCH_COOLDOWN_SECONDS)
                {
                    return;
                }

                // Determine optimal plan based on context (single unified recommendation)
                PowerPlanRecommendation recommendation = GetPowerPlanRecommendation();
                bool hasUltimate = SystemHasDedicatedUltimatePlan();

                if (!IsPowerPlanSatisfied(_currentPowerPlan, recommendation.Plan, hasUltimate))
                {
                    // Normalize target plan name if Ultimate is not available on this machine
                    string effectiveTarget = (recommendation.Plan == "ultimate" && !hasUltimate) ? "high" : recommendation.Plan;
                    if (!IsPowerPlanSatisfied(_currentPowerPlan, effectiveTarget, hasUltimate))
                    {
                        decisions.Add(new AgentDecision
                        {
                            Category = DecisionCategory.PowerOptimize,
                            Action = "SwitchPowerPlan",
                            Description = "Switch power plan: " + FormatPowerPlanName(_currentPowerPlan) + " -> " + FormatPowerPlanName(effectiveTarget),
                            Reason = recommendation.Reason,
                            Confidence = recommendation.Confidence,
                            Safety = DecisionSafety.Safe
                        });
                    }
                }
            }
            catch (Exception ex) { LogError("AnalyzePowerPlan", ex); }
        }

        // === Determine optimal power plan ===
        private PowerPlanRecommendation GetPowerPlanRecommendation()
        {
            var rec = new PowerPlanRecommendation();
            rec.Timestamp = DateTime.Now;

            // Priority 1: Low battery → Power Saver
            if (_isOnBattery && _batteryPercent > 0 && _batteryPercent < 20)
            {
                rec.Plan = "power saver";
                rec.Reason = "Battery low (" + _batteryPercent + "%)";
                rec.Confidence = 0.95;
                return rec;
            }

            // Priority 2: Gaming → High Performance or Ultimate
            if (_appMode == AppMode.Gaming)
            {
                if (_smoothTemp < 80 && SystemHasDedicatedUltimatePlan())
                {
                    rec.Plan = "ultimate";
                    rec.Reason = "Gaming mode - maximum performance (temp safe)";
                }
                else
                {
                    rec.Plan = "high";
                    rec.Reason = "Gaming mode - high performance";
                }
                rec.Confidence = 0.88;
                return rec;
            }

            // Priority 3: Heavy workload → High Performance
            if (_smoothCpu > 80 || _smoothGpu > 80)
            {
                rec.Plan = "high";
                rec.Reason = string.Format("Heavy workload: CPU={0:F0}%, GPU={1:F0}%", _smoothCpu, _smoothGpu);
                rec.Confidence = 0.80;
                return rec;
            }

            // Priority 4: On battery + light work → Balanced or Power Saver
            if (_isOnBattery)
            {
                if (_smoothCpu < 30 && _smoothRam < 50)
                {
                    rec.Plan = "power saver";
                    rec.Reason = "Light workload on battery - saving power";
                    rec.Confidence = 0.72;
                }
                else
                {
                    rec.Plan = "balanced";
                    rec.Reason = "Moderate workload on battery";
                    rec.Confidence = 0.75;
                }
                return rec;
            }

            // Priority 5: Thermal concern → Balanced (not high)
            if (_smoothTemp > 80)
            {
                rec.Plan = "balanced";
                rec.Reason = "Temperature elevated (" + string.Format("{0:F0}", _smoothTemp) + "C) - reducing power to cool down";
                rec.Confidence = 0.82;
                return rec;
            }

            // Default: Restore original desktop plan or Balanced
            string targetDesktopPlan = !string.IsNullOrEmpty(_originalPowerPlan) &&
                                       _originalPowerPlan != "ultimate" &&
                                       _originalPowerPlan != "high"
                ? _originalPowerPlan
                : "balanced";
            rec.Plan = targetDesktopPlan;
            rec.Reason = "Normal desktop operation - balanced for efficiency";
            rec.Confidence = 0.75;
            return rec;
        }

        // === Execute power plan switch ===
        public bool ExecutePowerPlanSwitch(AgentDecision decision)
        {
            if (DisableRealExecution || _simulationMode)
            {
                string simTarget = decision != null ? ExtractTargetPlan(decision.Description) : "balanced";
                if (!string.IsNullOrEmpty(simTarget)) _currentPowerPlan = simTarget;
                return true;
            }

            try
            {
                string targetPlan = ExtractTargetPlan(decision != null ? decision.Description : "");
                if (string.IsNullOrEmpty(targetPlan)) return false;

                if (IsPowerPlanSatisfied(_currentPowerPlan, targetPlan, SystemHasDedicatedUltimatePlan()))
                {
                    return true;
                }

                string guid = GetPowerPlanGuid(targetPlan);
                if (string.IsNullOrEmpty(guid)) return false;

                lock (_powerPlanSwitchLock)
                {
                    if ((DateTime.UtcNow - _lastPowerPlanSwitchExecution).TotalSeconds < POWER_SWITCH_COOLDOWN_SECONDS &&
                        string.Equals(_lastAppliedPowerPlanGuid, guid, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    using (var proc = new Process())
                    {
                        proc.StartInfo = new ProcessStartInfo
                        {
                            FileName = "powercfg.exe",
                            Arguments = "/setactive " + guid,
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        proc.Start();
                        proc.WaitForExit(3000);
                    }

                    _lastPowerPlanSwitchExecution = DateTime.UtcNow;
                    _lastAppliedPowerPlanGuid = guid;
                }

                // Log the switch
                var switchEntry = new PowerPlanSwitch
                {
                    Timestamp = DateTime.Now,
                    FromPlan = _currentPowerPlan,
                    ToPlan = targetPlan,
                    Reason = decision != null ? decision.Reason : "Power plan switch",
                    CpuUsage = _smoothCpu,
                    BatteryPercent = _batteryPercent,
                    IsOnBattery = _isOnBattery,
                    IsGame = _appMode == AppMode.Gaming
                };
                _powerPlanHistory.Add(switchEntry);
                if (_powerPlanHistory.Count > MAX_POWER_PLAN_HISTORY)
                    _powerPlanHistory.RemoveAt(0);

                _currentPowerPlan = targetPlan;
                AddLog("INFO", "PowerPlan", "Switched: " + FormatPowerPlanName(switchEntry.FromPlan) + " -> " + FormatPowerPlanName(targetPlan));
                return true;
            }
            catch (Exception ex) { LogError("PowerPlan", ex); return false; }
        }

        // === Restore original power plan ===
        public bool RestoreOriginalPowerPlan()
        {
            string planToRestore = _originalPowerPlan;
            if (string.IsNullOrEmpty(planToRestore) || planToRestore == "ultimate" || planToRestore == "high")
            {
                planToRestore = "balanced";
            }
            if (planToRestore == _currentPowerPlan)
                return false;

            var decision = new AgentDecision
            {
                Category = DecisionCategory.PowerOptimize,
                Action = "SwitchPowerPlan",
                Description = "Restore power plan: " + FormatPowerPlanName(_currentPowerPlan) + " -> " + FormatPowerPlanName(planToRestore),
                Reason = "Restoring user's original power plan",
                Confidence = 0.85,
                Safety = DecisionSafety.Safe
            };

            return ExecutePowerPlanSwitch(decision);
        }

        // === Detect current power plan ===
        private string DetectCurrentPowerPlan()
        {
            try
            {
                using (var proc = new Process())
                {
                    proc.StartInfo = new ProcessStartInfo
                    {
                        FileName = "powercfg.exe",
                        Arguments = "/getactivescheme",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    };
                    proc.Start();
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(3000);

                    if (output.Contains("e9a42b02-d5df-448d-aa00-03f14749eb61") || output.IndexOf("Ultimate", StringComparison.OrdinalIgnoreCase) >= 0 || output.IndexOf("Ultra", StringComparison.OrdinalIgnoreCase) >= 0) return "ultimate";
                    if (output.Contains("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c") || output.IndexOf("High performance", StringComparison.OrdinalIgnoreCase) >= 0 || output.IndexOf("Smart Game Boost", StringComparison.OrdinalIgnoreCase) >= 0 || output.IndexOf("الأداء العالي", StringComparison.OrdinalIgnoreCase) >= 0) return "high";
                    if (output.Contains("a1841308-3541-4fab-bc81-f71556f20b4a") || output.IndexOf("Power saver", StringComparison.OrdinalIgnoreCase) >= 0 || output.IndexOf("توفير الطاقة", StringComparison.OrdinalIgnoreCase) >= 0) return "power saver";
                    return "balanced";
                }
            }
            catch { return "balanced"; }
        }

        // === Get power plan GUID ===
        private string GetPowerPlanGuid(string plan)
        {
            switch ((plan ?? "").ToLowerInvariant())
            {
                case "ultimate":
                case "high":
                    return FindBestPerformancePlanGuid();
                case "power saver": return "a1841308-3541-4fab-bc81-f71556f20b4a";
                case "balanced": return FindBalancedPlanGuid();
                default: return FindBalancedPlanGuid();
            }
        }

        // === Format power plan name to Arabic ===
        private string FormatPowerPlanName(string plan)
        {
            switch ((plan ?? "").ToLowerInvariant())
            {
                case "ultimate": return "الحد الأقصى";
                case "high": return "أداء عالي";
                case "power saver": return "توفير الطاقة";
                case "balanced": return "متوازن";
                default: return plan;
            }
        }

        // === Extract target plan from decision description ===
        private string ExtractTargetPlan(string description)
        {
            if (string.IsNullOrEmpty(description)) return "balanced";
            if (description.IndexOf("ultimate", StringComparison.OrdinalIgnoreCase) >= 0 || description.Contains("الحد الأقصى")) return "ultimate";
            if (description.IndexOf("High Performance", StringComparison.OrdinalIgnoreCase) >= 0 || description.IndexOf("high", StringComparison.OrdinalIgnoreCase) >= 0 || description.Contains("أداء عالي")) return "high";
            if (description.IndexOf("Power Saver", StringComparison.OrdinalIgnoreCase) >= 0 || description.Contains("توفير الطاقة")) return "power saver";
            if (description.IndexOf("Balanced", StringComparison.OrdinalIgnoreCase) >= 0 || description.Contains("متوازن")) return "balanced";
            return "balanced";
        }

        public class PowerPlanRecommendation
        {
            public DateTime Timestamp;
            public string Plan;
            public string Reason;
            public double Confidence;
        }

        // === Find best performance plan GUID dynamically from powercfg /list ===
        public static string FindBestPerformancePlanGuid()
        {
            if (DisableRealExecution) return "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
            try
            {
                using (var proc = new Process())
                {
                    proc.StartInfo = new ProcessStartInfo
                    {
                        FileName = "powercfg.exe",
                        Arguments = "/list",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    };
                    proc.Start();
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(3000);

                    var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    string ultimateGuid = null;
                    string ultraGuid = null;
                    string gameBoostGuid = null;
                    string highGuid = null;

                    foreach (var line in lines)
                    {
                        var m = System.Text.RegularExpressions.Regex.Match(line, @"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})");
                        if (!m.Success) continue;
                        string guid = m.Groups[1].Value;

                        if (line.IndexOf("Ultimate Performance", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            if (ultimateGuid == null) ultimateGuid = guid;
                        }
                        else if (line.IndexOf("Ultra Performance", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            if (ultraGuid == null) ultraGuid = guid;
                        }
                        else if (line.IndexOf("Smart Game Boost", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            if (gameBoostGuid == null) gameBoostGuid = guid;
                        }
                        else if (line.IndexOf("High performance", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 line.IndexOf("الأداء العالي", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                 guid.Equals("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", StringComparison.OrdinalIgnoreCase))
                        {
                            if (highGuid == null) highGuid = guid;
                        }
                    }

                    if (!string.IsNullOrEmpty(ultimateGuid)) return ultimateGuid;
                    if (!string.IsNullOrEmpty(ultraGuid)) return ultraGuid;
                    if (!string.IsNullOrEmpty(gameBoostGuid)) return gameBoostGuid;
                    if (!string.IsNullOrEmpty(highGuid)) return highGuid;
                }
            }
            catch (Exception ex)
            {
                CWLog.Error("FindBestPerformancePlanGuid", ex);
            }
            return FindBalancedPlanGuid();
        }

        // === Find balanced plan GUID dynamically from powercfg /list ===
        public static string FindBalancedPlanGuid()
        {
            if (DisableRealExecution) return "381b4222-f694-41f0-9685-ff5bb260df2e";
            try
            {
                using (var proc = new Process())
                {
                    proc.StartInfo = new ProcessStartInfo
                    {
                        FileName = "powercfg.exe",
                        Arguments = "/list",
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    };
                    proc.Start();
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(3000);

                    var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var m = System.Text.RegularExpressions.Regex.Match(line, @"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})");
                        if (!m.Success) continue;
                        string guid = m.Groups[1].Value;

                        if (line.IndexOf("Balanced", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            line.IndexOf("متوازن", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            guid.Equals("381b4222-f694-41f0-9685-ff5bb260df2e", StringComparison.OrdinalIgnoreCase))
                        {
                            return guid;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CWLog.Error("FindBalancedPlanGuid", ex);
            }
            return "381b4222-f694-41f0-9685-ff5bb260df2e";
        }

        // === Restore balanced or previous power plan on game exit ===
        public static bool RestoreBalancedOrPreviousPowerPlan(string previousPlanGuid = null)
        {
            if (DisableRealExecution)
            {
                AiDecisionLogger.RecordDecision(
                    "SwitchPowerPlan",
                    "الخروج من اللعبة: تمت استعادة خطة الطاقة المتوازنة (Balanced) تلقائياً لتبريد المعالج وترشيد الطاقة.",
                    0.96,
                    "PowerPlan",
                    "SIMULATED",
                    "محاكاة استعادة خطة الطاقة المتوازنة دون تغيير إعدادات النظام الحقيقية."
                );
                return true;
            }
            try
            {
                string bestPerfGuid = FindBestPerformancePlanGuid();
                string planToRestore = previousPlanGuid;

                // If previous plan is null/empty or is itself an ultimate/high performance plan, fallback to Balanced
                if (string.IsNullOrEmpty(planToRestore) ||
                    (!string.IsNullOrEmpty(bestPerfGuid) && string.Equals(planToRestore, bestPerfGuid, StringComparison.OrdinalIgnoreCase) &&
                     !string.Equals(bestPerfGuid, "381b4222-f694-41f0-9685-ff5bb260df2e", StringComparison.OrdinalIgnoreCase)) ||
                    planToRestore.IndexOf("cd89731b", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    planToRestore.IndexOf("d06f5da7", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    planToRestore.IndexOf("f65e7e34", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    planToRestore.IndexOf("6a93ec26", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    planToRestore.IndexOf("e9a42b02", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    planToRestore.IndexOf("8c5e7fda", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    planToRestore = FindBalancedPlanGuid();
                }

                lock (_powerPlanSwitchLock)
                {
                    using (var p = Process.Start(new ProcessStartInfo("powercfg.exe", "/setactive " + planToRestore) { CreateNoWindow = true, UseShellExecute = false }))
                    {
                        if (p != null) p.WaitForExit(3000);
                    }
                    _lastPowerPlanSwitchExecution = DateTime.UtcNow;
                    _lastAppliedPowerPlanGuid = planToRestore;
                }

                if (Instance != null)
                {
                    Instance._currentPowerPlan = "balanced";
                }

                AiDecisionLogger.RecordDecision(
                    "SwitchPowerPlan",
                    "الخروج من اللعبة: تمت استعادة خطة الطاقة الأصلية تلقائياً لتبريد المعالج وترشيد الطاقة.",
                    0.96,
                    "PowerPlan",
                    "SUCCESS",
                    "إعادة خطة الطاقة إلى الوضع الطبيعي على سطح المكتب."
                );

                CWLog.Info("PowerPlanManager", "Restored power plan to: " + planToRestore);
                return true;
            }
            catch (Exception ex)
            {
                CWLog.Error("RestoreBalancedOrPreviousPowerPlan", ex);
                return false;
            }
        }

        // === Activate Highest Performance Power Plan with Cooldown Guard ===
        public static bool ActivateHighestPerformancePowerPlan()
        {
            if (DisableRealExecution) return true;
            try
            {
                string bestGuid = FindBestPerformancePlanGuid();
                if (string.IsNullOrEmpty(bestGuid)) return false;

                lock (_powerPlanSwitchLock)
                {
                    if ((DateTime.UtcNow - _lastPowerPlanSwitchExecution).TotalSeconds < POWER_SWITCH_COOLDOWN_SECONDS &&
                        string.Equals(_lastAppliedPowerPlanGuid, bestGuid, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    using (var p = Process.Start(new ProcessStartInfo("powercfg.exe", "/setactive " + bestGuid) { CreateNoWindow = true, UseShellExecute = false }))
                    {
                        if (p != null) p.WaitForExit(3000);
                    }
                    _lastPowerPlanSwitchExecution = DateTime.UtcNow;
                    _lastAppliedPowerPlanGuid = bestGuid;
                }

                if (Instance != null)
                {
                    Instance._currentPowerPlan = bestGuid.IndexOf("e9a42b02", StringComparison.OrdinalIgnoreCase) >= 0 ? "ultimate" : "high";
                }
                return true;
            }
            catch (Exception ex)
            {
                CWLog.Error("ActivateHighestPerformancePowerPlan", ex);
                return false;
            }
        }

        // === Scan and get known emulator and game executable paths ===
        public static string[] GetKnownEmulatorAndGamePaths()
        {
            var paths = new List<string>();
            string[] commonPaths = new[]
            {
                // GameLoop 7.0 & Tencent components
                @"D:\Program Files\Tencent\GameLoop\Application\7.0.167.0\GameLoopEmulator.exe",
                @"D:\Program Files\Tencent\GameLoop\Application\7.0.167.0\renderer\AndrowsFG.exe",
                @"D:\Program Files\Tencent\GameLoop\Application\7.0.167.0\GameLoopVm.exe",
                @"D:\Program Files\Tencent\GameLoop\Application\7.0.167.0\GameLoopBox\GLABoxHeadless.exe",
                @"D:\Program Files\Tencent\GameLoop\Application\7.0.167.0\GameLoop.exe",
                @"D:\Program Files\Tencent\GameLoop\Application\GameLoopLauncher.exe",
                @"C:\Program Files\Tencent\GameLoop\Application\7.0.167.0\GameLoopEmulator.exe",
                @"C:\Program Files\Tencent\GameLoop\Application\7.0.167.0\renderer\AndrowsFG.exe",
                @"C:\Program Files\Tencent\GameLoop\Application\7.0.167.0\GameLoopVm.exe",
                @"C:\Program Files\Tencent\GameLoop\Application\7.0.167.0\GameLoop.exe",
                @"C:\Program Files\Tencent\GameLoop\Application\GameLoopLauncher.exe",
                @"D:\TxGameAssistant\ui\aow_exe.exe",
                @"C:\Program Files\TxGameAssistant\ui\aow_exe.exe",
                @"D:\TxGameAssistant\AppMarket\AppMarket.exe",
                @"C:\Program Files\TxGameAssistant\AppMarket\AppMarket.exe",
                @"D:\TxGameAssistant\androidemulator.exe",
                @"C:\Program Files\TxGameAssistant\androidemulator.exe",
                // MuMu Player
                @"D:\MuMuPlayerGlobal\nx_main\MuMuNxMain.exe",
                @"D:\MuMuPlayerGlobal\nx_device\15.0\shell\MuMuNxDevice.exe",
                @"D:\MuMuPlayerGlobal\nx_device\15.0\device\RendererDetector.exe",
                @"D:\MuMuPlayerGlobal\nx_device\15.0\device\vkms.exe",
                @"C:\Program Files\Netease\MuMuPlayerGlobal\nx_main\MuMuNxMain.exe",
                // LDPlayer
                @"D:\LDPlayer\LDPlayer9\dnplayer.exe",
                @"C:\LDPlayer\LDPlayer9\dnplayer.exe",
                @"C:\Program Files\ldplayer9box\Ld9BoxHeadless.exe",
                // BlueStacks
                @"C:\Program Files\BlueStacks_nxt\HD-Player.exe",
                @"C:\Program Files\BlueStacks_nxt\HD-GLCheck.exe",
                // PUBG PC
                @"C:\Program Files (x86)\Steam\steamapps\common\PUBG\TslGame\Binaries\Win64\TslGame.exe",
                @"D:\Steam\steamapps\common\PUBG\TslGame\Binaries\Win64\TslGame.exe",
                @"D:\SteamLibrary\steamapps\common\PUBG\TslGame\Binaries\Win64\TslGame.exe",
                @"E:\SteamLibrary\steamapps\common\PUBG\TslGame\Binaries\Win64\TslGame.exe"
            };

            foreach (var p in commonPaths)
            {
                if (System.IO.File.Exists(p)) paths.Add(p);
            }
            return paths.ToArray();
        }

        // === Optimize GameLoop configuration settings (with full Registry backup & restore) ===
        public static void OptimizeGameLoopSettings()
        {
            if (DisableRealExecution && !UseSimulatedRegistryBackend) return;
            try
            {
                const string commonSubKey = @"Software\Tencent\GameLoop\Settings\Common";
                SetRegistryValueWithBackup("HKCU", commonSubKey, "PerformanceMode", 2, RegistryValueKind.DWord, false);
                SetRegistryValueWithBackup("HKCU", commonSubKey, "PerformanceMemoryGb", 4, RegistryValueKind.DWord, false);
                int cores = Math.Min(Environment.ProcessorCount, 4);
                SetRegistryValueWithBackup("HKCU", commonSubKey, "PerformanceCpuCoreNum", cores, RegistryValueKind.DWord, false);
                SetRegistryValueWithBackup("HKCU", commonSubKey, "DisableFullscreenOptimization", 1, RegistryValueKind.DWord, false);
                SetRegistryValueWithBackup("HKCU", commonSubKey, "Vsync", 0, RegistryValueKind.DWord, false);
                SetRegistryValueWithBackup("HKCU", commonSubKey, "FrameRate", 90, RegistryValueKind.DWord, false);

                const string glSubKey = @"Software\Tencent\GameLoop\Settings\GameLoop";
                SetRegistryValueWithBackup("HKCU", glSubKey, "GameMode", 1, RegistryValueKind.DWord, false);
                SetRegistryValueWithBackup("HKCU", glSubKey, "Vsync", 0, RegistryValueKind.DWord, false);
                SetRegistryValueWithBackup("HKCU", glSubKey, "FrameRate", 90, RegistryValueKind.DWord, false);
                SetRegistryValueWithBackup("HKCU", glSubKey, "RenderQuality", 1, RegistryValueKind.DWord, false);

                const string mgSubKey = @"Software\Tencent\MobileGamePC";
                SetRegistryValueWithBackup("HKCU", mgSubKey, "Vsync", 0, RegistryValueKind.DWord, false);
                SetRegistryValueWithBackup("HKCU", mgSubKey, "MaxFps", 90, RegistryValueKind.DWord, false);
                SetRegistryValueWithBackup("HKCU", mgSubKey, "Fps", 90, RegistryValueKind.DWord, false);
            }
            catch (Exception ex)
            {
                CWLog.Error("OptimizeGameLoopSettings", ex);
            }
        }

        // === Enforce Dedicated GPU (GpuPreference=2) and Ultimate Power Plan (with full Registry backup) ===
        public static bool EnforceGameDedicatedGpuAndPower(string procName = null, string procPath = null, bool switchPowerPlan = true)
        {
            if (DisableRealExecution && !UseSimulatedRegistryBackend)
            {
                if (!string.IsNullOrEmpty(procName))
                {
                    AiDecisionLogger.RecordDecision(
                        "BalanceGameLoad",
                        string.Format("بدء اللعبة: إلزام تشغيل {0} على كارت الشاشة المنفصل (High-Performance GPU) وتفعيل خطة الطاقة القصوى.", procName),
                        0.96,
                        "Gaming",
                        "SIMULATED",
                        string.Format("محاكاة توجيه الرندر لكارت الشاشة المنفصل لـ {0} دون تعديل سجل النظام الحقيقي.", procName)
                    );
                }
                return true;
            }
            bool modified = false;
            try
            {
                const string gpuPrefSubKey = @"Software\Microsoft\DirectX\UserGpuPreferences";

                // A. Bind active executable in DirectX UserGpuPreferences (with backup)
                if (!string.IsNullOrEmpty(procPath) &&
                    System.IO.Path.IsPathRooted(procPath) &&
                    procPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                    (UseSimulatedRegistryBackend || System.IO.File.Exists(procPath)))
                {
                    if (SetRegistryValueWithBackup("HKCU", gpuPrefSubKey, procPath, "GpuPreference=2;", RegistryValueKind.String, true))
                    {
                        modified = true;
                    }
                }

                // Only scan & bind known emulator executables when a game/emulator session is active
                if (!string.IsNullOrEmpty(procName))
                {
                    string[] knownPaths = GetKnownEmulatorAndGamePaths();
                    foreach (string p in knownPaths)
                    {
                        if (System.IO.File.Exists(p))
                        {
                            if (SetRegistryValueWithBackup("HKCU", gpuPrefSubKey, p, "GpuPreference=2;", RegistryValueKind.String, true))
                            {
                                modified = true;
                            }
                        }
                    }

                    // B. Optimize GameLoop internals (backed up if keys exist)
                    OptimizeGameLoopSettings();

                    // C. Switch to Highest Performance Power Plan ONLY when requested and a game is active
                    if (switchPowerPlan)
                    {
                        ActivateHighestPerformancePowerPlan();
                    }

                    // D. Log the action in Arabic in AiDecisionLogger
                    AiDecisionLogger.RecordDecision(
                        "BalanceGameLoad",
                        string.Format("بدء اللعبة: إلزام تشغيل {0} على كارت الشاشة المنفصل (High-Performance GPU) وتفعيل خطة الطاقة القصوى.", procName),
                        0.96,
                        "Gaming",
                        "SUCCESS",
                        string.Format("توجيه الرندر لكارت الشاشة المنفصل مع حفظ نسخة استعادة آمنة لـ {0}.", procName)
                    );
                }
            }
            catch (Exception ex)
            {
                CWLog.Error("EnforceGameDedicatedGpuAndPower", ex);
            }
            return modified;
        }
    }
}
