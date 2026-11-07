using System;
using System.Collections.Generic;
using System.Management;

namespace ClearWin
{
    public partial class SmartAgent
    {
        // ===================== PERFORMANCE AUTOPILOT =====================
        private bool _autopilotEnabled = false;
        private DateTime _lastAutopilotAction = DateTime.MinValue;
        private const int AUTOPILOT_MIN_INTERVAL_SECONDS = 10;
        private AutopilotState _autopilotState = new AutopilotState();

        public void EnableAutopilot()
        {
            _autopilotEnabled = true;
            _autopilotState.EnabledAt = DateTime.Now;
            _autopilotState.TotalActions = 0;
            AddLog("AUTOPILOT", "Enabled", "Performance Autopilot activated - full autonomous management");
        }

        public void DisableAutopilot()
        {
            _autopilotEnabled = false;
            AddLog("AUTOPILOT", "Disabled", "Performance Autopilot deactivated");
        }

        public void RunAutopilotCycle()
        {
            if (!_autopilotEnabled) return;
            if (DisableRealExecution || _simulationMode) return;
            // Central Execution Gate: the autopilot is fully autonomous - when
            // the user stops automatic actions it must not switch power plans,
            // throttle thermally, trim memory or reprioritize processes.
            if (!CanExecuteAction("PerformanceAutopilot", false)) return;
            if ((DateTime.Now - _lastAutopilotAction).TotalSeconds < AUTOPILOT_MIN_INTERVAL_SECONDS) return;
            _lastAutopilotAction = DateTime.Now;

            lock (_stateLock)
            {
                if (_currentState == null) return;

                // 1. Power management
                string optimalPlan = DetermineOptimalPowerPlan();
                string currentPlan = GetCurrentPowerPlan();
                if (!string.Equals(optimalPlan, currentPlan, StringComparison.OrdinalIgnoreCase))
                {
                    ExecutePowerPlan(optimalPlan);
                    _autopilotState.PowerPlanChanges++;
                    _autopilotState.TotalActions++;
                    AddLog("AUTOPILOT", "PowerPlan", "Auto-switched to: " + optimalPlan);
                }

                // 2. Thermal management
                if (_currentState.CpuTemp > _deviceProfile.ThermalWarningTemp)
                {
                    if (_currentState.CpuTemp > _deviceProfile.ThermalThrottleTemp)
                    {
                        ExecuteThermalProtection(true);
                        _autopilotState.EmergencyThrottles++;
                    }
                    else
                    {
                        ExecuteThermalProtection(false);
                    }
                    _autopilotState.ThermalActions++;
                    _autopilotState.TotalActions++;
                }

                // 3. Memory management (never EmptyWorkingSet while a game is active:
                // it forces the game pages out of RAM -> paging -> stutter)
                if (_currentState.RamUsage > _deviceProfile.RamCleanThreshold && !_currentState.IsGame)
                {
                    ExecuteMemoryClean();
                    _autopilotState.MemoryCleans++;
                    _autopilotState.TotalActions++;
                }

                // 4. Process priority management
                if (_currentState.IsGame)
                {
                    // Boost game, limit background
                    ExecuteBoostGamePriority();
                    ExecuteLimitBackground();
                    _autopilotState.ProcessAdjustments++;
                    _autopilotState.TotalActions++;
                }
                else
                {
                    // Desktop Smoothness: Elevate active window priority, maintain 1.0ms timer & reduce UI latency
                    ExecuteDesktopSmoothness();
                    _autopilotState.ProcessAdjustments++;
                    _autopilotState.TotalActions++;
                }

                // 5. Background control
                if (_currentState.ProcessCount > _deviceProfile.MaxBackgroundProcesses)
                {
                    ExecuteLimitBackground();
                    _autopilotState.BackgroundControls++;
                    _autopilotState.TotalActions++;
                }

                // 6. Standby purge when RAM is high (skipped during gaming: purge
                // forces disk re-reads that stutter the running game)
                if (_currentState.RamUsage > _deviceProfile.AggressiveCleanRamThreshold && !_currentState.IsGame)
                {
                    ExecutePurgeStandby();
                    _autopilotState.MemoryCleans++;
                    _autopilotState.TotalActions++;
                }

                // Update state
                _autopilotState.LastAction = DateTime.Now;
                _autopilotState.CurrentMode = _mode.ToString();
                _autopilotState.HealthScore = SystemHealthScore;
            }
        }

        private string DetermineOptimalPowerPlan()
        {
            if (_currentState.IsGame) return "ultimate";
            if (_currentState.IsCharging && _currentState.CpuUsage > 60) return "high performance";
            if (!_currentState.IsCharging && _currentState.BatteryLevel < 20) return "power saver";
            if (_currentState.CpuTemp > _deviceProfile.ThermalWarningTemp) return "balanced";
            if (_currentState.CpuUsage < 20 && !_currentState.IsCharging) return "power saver";
            return "balanced";
        }

        public AutopilotState GetAutopilotState() { return _autopilotState; }
        public bool IsAutopilotEnabled { get { return _autopilotEnabled; } }

        // ===================== AI DRIVER INTELLIGENCE =====================
        private List<DriverInfo> _driverInfo = new List<DriverInfo>();
        private DateTime _lastDriverCheck = DateTime.MinValue;
        private const int DRIVER_CHECK_INTERVAL = 3600; // check every hour

        public List<DriverInfo> CheckDrivers()
        {
            if ((DateTime.Now - _lastDriverCheck).TotalSeconds < DRIVER_CHECK_INTERVAL) return _driverInfo;
            _lastDriverCheck = DateTime.Now;

            _driverInfo.Clear();

            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name,DriverVersion,DriverDate,Manufacturer,Status FROM Win32_PnPSignedDriver"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        var driver = new DriverInfo();
                        driver.Name = Convert.ToString(mo["Name"]);
                        driver.Version = Convert.ToString(mo["DriverVersion"]);
                        driver.Manufacturer = Convert.ToString(mo["Manufacturer"]);
                        driver.Status = Convert.ToString(mo["Status"]);

                        string dateStr = Convert.ToString(mo["DriverDate"]);
                        if (!string.IsNullOrEmpty(dateStr) && dateStr.Length >= 8)
                        {
                            try
                            {
                                int year = int.Parse(dateStr.Substring(0, 4));
                                int month = int.Parse(dateStr.Substring(4, 2));
                                int day = int.Parse(dateStr.Substring(6, 2));
                                driver.Date = new DateTime(year, month, day);
                                driver.AgeDays = (DateTime.Now - driver.Date).Days;
                            }
                            catch { driver.AgeDays = 0; }
                        }

                        // Categorize driver
                        string nameLower = driver.Name.ToLower();
                        if (nameLower.Contains("display") || nameLower.Contains("video") || nameLower.Contains("nvidia") || nameLower.Contains("amd") || nameLower.Contains("intel"))
                        {
                            driver.Category = "GPU";
                            driver.UpdateImpact = "High - GPU drivers affect gaming and rendering performance";
                            driver.UpdateRisk = "Low - GPU driver updates are generally safe";
                            driver.Recommendation = driver.AgeDays > 90 ? "Update recommended - driver is over 3 months old" : "Up to date";
                        }
                        else if (nameLower.Contains("chipset") || nameLower.Contains("system"))
                        {
                            driver.Category = "Chipset";
                            driver.UpdateImpact = "Medium - affects system stability";
                            driver.UpdateRisk = "Medium - chipset updates can occasionally cause issues";
                            driver.Recommendation = driver.AgeDays > 180 ? "Consider updating" : "Acceptable";
                        }
                        else if (nameLower.Contains("network") || nameLower.Contains("wifi") || nameLower.Contains("ethernet"))
                        {
                            driver.Category = "Network";
                            driver.UpdateImpact = "Low-Medium - affects connectivity";
                            driver.UpdateRisk = "Low";
                            driver.Recommendation = driver.AgeDays > 90 ? "Consider updating" : "Acceptable";
                        }
                        else if (nameLower.Contains("audio") || nameLower.Contains("sound"))
                        {
                            driver.Category = "Audio";
                            driver.UpdateImpact = "Low - affects audio quality";
                            driver.UpdateRisk = "Low";
                            driver.Recommendation = driver.AgeDays > 180 ? "Consider updating" : "Acceptable";
                        }
                        else
                        {
                            driver.Category = "Other";
                            driver.UpdateImpact = "Varies";
                            driver.UpdateRisk = "Low";
                            driver.Recommendation = "Monitor";
                        }

                        _driverInfo.Add(driver);
                    }
                }
            }
            catch (Exception ex) { LogError("CheckDrivers", ex); }

            // Summary log
            int gpuDrivers = 0, outdatedDrivers = 0;
            foreach (var d in _driverInfo)
            {
                if (d.Category == "GPU") gpuDrivers++;
                if (d.AgeDays > 90) outdatedDrivers++;
            }

            if (outdatedDrivers > 0)
            {
                AddSuggestion("Driver Updates Available",
                    string.Format("{0} drivers are over 3 months old", outdatedDrivers),
                    "Check GPU and chipset drivers for updates",
                    SuggestionPriority.Low, "Maintenance", 5, false);
            }

            return _driverInfo;
        }

        public List<DriverInfo> GetDriverInfo() { return _driverInfo; }
    }

    // ===================== MODELS =====================
    public class AutopilotState
    {
        public bool IsEnabled;
        public DateTime EnabledAt;
        public DateTime LastAction;
        public int TotalActions;
        public int PowerPlanChanges;
        public int ThermalActions;
        public int EmergencyThrottles;
        public int MemoryCleans;
        public int ProcessAdjustments;
        public int BackgroundControls;
        public string CurrentMode;
        public double HealthScore;

        public AutopilotState()
        {
            CurrentMode = "Off";
            HealthScore = 100;
        }
    }

    public class DriverInfo
    {
        public string Name;
        public string Version;
        public string Manufacturer;
        public string Status;
        public DateTime Date;
        public int AgeDays;
        public string Category;
        public string UpdateImpact;
        public string UpdateRisk;
        public string Recommendation;
    }
}
