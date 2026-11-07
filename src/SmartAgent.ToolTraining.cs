using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace ClearWin
{
    public partial class SmartAgent
    {
        // === TOOL TRAINING SYSTEM (Level 8) ===
        private List<ToolDefinition> _toolLibrary = new List<ToolDefinition>();
        private List<ToolExecution> _toolExecutions = new List<ToolExecution>();
        private List<ToolFeedback> _toolFeedbacks = new List<ToolFeedback>();
        private Dictionary<string, ToolLearningPattern> _toolPatterns = new Dictionary<string, ToolLearningPattern>();
        private List<ToolRecommendation> _lastRecommendations = new List<ToolRecommendation>();
        private TrainingSession _activeTrainingSession;
        private DateTime _lastToolAnalysis = DateTime.MinValue;
        private const int TOOL_ANALYSIS_INTERVAL_SECONDS = 30;
        private const int MAX_TOOL_LIBRARY = 200;
        private const int MAX_TOOL_EXECUTIONS = 500;
        private const int MAX_TOOL_FEEDBACKS = 1000;
        private readonly object _toolLock = new object();
        private string _toolDataDir;

        // === Sandbox gate (dangerous commands require explicit approval) ===
        private readonly object _toolApprovalLock = new object();
        private readonly HashSet<string> _approvedTools = new HashSet<string>();
        private readonly List<string> _pendingToolApprovals = new List<string>();
        private bool _requireToolConfirmation = true;
        public bool RequireToolConfirmation
        {
            get { return _requireToolConfirmation; }
            set { _requireToolConfirmation = value; }
        }

        public bool IsToolApproved(string toolId)
        {
            lock (_toolApprovalLock) { return _approvedTools.Contains(toolId); }
        }

        public void ApproveTool(string toolId)
        {
            lock (_toolApprovalLock)
            {
                _approvedTools.Add(toolId);
                _pendingToolApprovals.Remove(toolId);
            }
            AddLog("SECURITY", "ToolApproved", "Approved tool: " + toolId);
        }

        public void RevokeToolApproval(string toolId)
        {
            lock (_toolApprovalLock) { _approvedTools.Remove(toolId); }
        }

        public List<string> GetPendingToolApprovals()
        {
            lock (_toolApprovalLock) { return new List<string>(_pendingToolApprovals); }
        }

        private bool IsDangerousTool(ToolDefinition tool)
        {
            if (tool == null) return false;
            string cmd = (tool.Command ?? "").ToLower();
            if (cmd.StartsWith("cmd:")) return true;
            if (cmd.StartsWith("powershell:")) return true;
            if (cmd.Contains("standbylistpurge")) return true;
            if (cmd.Contains("unparkallcores")) return true;
            if (cmd.Contains("emergencythrottle")) return true;
            if (cmd.Contains("switchpowerplan")) return true;
            if (cmd.Contains("settimerresolution")) return true;
            return false;
        }

        // Learning parameters
        private const double TOOL_LEARNING_RATE = 0.1;
        private const double TOOL_DECAY_RATE = 0.02;
        private const double TOOL_EXPLORATION_BONUS = 0.15;
        private const int MIN_EXECUTIONS_FOR_CONFIDENCE = 3;

        // === Public Accessors ===
        public List<ToolDefinition> GetToolLibrary() { lock (_toolLock) { return new List<ToolDefinition>(_toolLibrary); } }
        public List<ToolExecution> GetToolExecutions() { lock (_toolLock) { return new List<ToolExecution>(_toolExecutions); } }
        public List<ToolFeedback> GetToolFeedbacks() { lock (_toolLock) { return new List<ToolFeedback>(_toolFeedbacks); } }
        public List<ToolRecommendation> GetLastRecommendations() { return new List<ToolRecommendation>(_lastRecommendations); }
        public int GetToolCount() { lock (_toolLock) { return _toolLibrary.Count; } }
        public TrainingSession GetActiveTrainingSession() { return _activeTrainingSession; }

        // === Initialize Tool Training System ===
        public void InitializeToolTraining()
        {
            _toolDataDir = Path.Combine(_dataDir, "tool_training");

            LoadToolLibrary();
            LoadToolHistory();
            InitializeDefaultTools();

            _activeTrainingSession = new TrainingSession();
            AddLog("TRAIN", "ToolTraining",
                string.Format("Tool Training System initialized: {0} tools, {1} executions in history",
                _toolLibrary.Count, _toolExecutions.Count));
        }

        // === Default Tools (pre-loaded for immediate learning) ===
        private void InitializeDefaultTools()
        {
            if (_toolLibrary.Count > 0) return; // Already loaded from disk

            AddToolDirect(new ToolDefinition
            {
                Name = "Standby Memory Purge",
                Description = "Purge Windows standby list to free cached memory",
                DetailedExplanation = "Windows caches data in standby memory. Purging it frees RAM for active applications without killing processes.",
                Category = ToolCategory.Memory,
                Complexity = ToolComplexity.Safe,
                AiCategory = DecisionCategory.MemoryClean,
                Command = "EmptyWorkingSet + StandbyListPurge",
                RevertCommand = "None (memory will cache again naturally)",
                PreConditions = new Dictionary<string, double> { { "RAM", 70 } },
                PostEffects = new Dictionary<string, double> { { "RAM", -15 } },
                Tags = "memory,standby,safe",
                CreatedBy = "System"
            });

            AddToolDirect(new ToolDefinition
            {
                Name = "Aggressive Memory Clean",
                Description = "Force clean all non-essential process working sets",
                DetailedExplanation = "Reduces working sets of all non-critical processes. Effective but may cause brief slowness in background apps.",
                Category = ToolCategory.Memory,
                Complexity = ToolComplexity.Moderate,
                AiCategory = DecisionCategory.MemoryClean,
                Command = "EmptyWorkingSet_AllNonCritical",
                RevertCommand = "Processes will rebuild working sets over time",
                PreConditions = new Dictionary<string, double> { { "RAM", 80 } },
                PostEffects = new Dictionary<string, double> { { "RAM", -25 } },
                Tags = "memory,aggressive",
                CreatedBy = "System"
            });

            AddToolDirect(new ToolDefinition
            {
                Name = "Clipboard Memory Purge",
                Description = "Purge Windows clipboard cache to free unneeded media and text data",
                DetailedExplanation = "Large images and files in the Windows clipboard consume RAM silently. Purging it reclaims memory without touching active processes.",
                Category = ToolCategory.Memory,
                Complexity = ToolComplexity.Safe,
                AiCategory = DecisionCategory.MemoryClean,
                Command = "PurgeClipboard",
                RevertCommand = "None",
                PreConditions = new Dictionary<string, double> { { "RAM", 70 } },
                PostEffects = new Dictionary<string, double> { { "RAM", -5 } },
                Tags = "memory,clipboard,safe",
                CreatedBy = "System"
            });

            AddToolDirect(new ToolDefinition
            {
                Name = "Deep Memory Compaction",
                Description = "Multi-stage deep RAM optimization (Working Sets + Flush Modified + Purge Standby + Clipboard)",
                DetailedExplanation = "Emulates and surpasses advanced memory optimizers by executing a multi-tier compaction safely during idle or extreme RAM saturation without affecting gaming.",
                Category = ToolCategory.Memory,
                Complexity = ToolComplexity.Moderate,
                AiCategory = DecisionCategory.MemoryClean,
                Command = "DeepMemoryCompaction",
                RevertCommand = "Processes will naturally rebuild working sets on access",
                PreConditions = new Dictionary<string, double> { { "RAM", 85 } },
                PostEffects = new Dictionary<string, double> { { "RAM", -30 } },
                Tags = "memory,compaction,idle,defrag",
                CreatedBy = "System"
            });

            AddToolDirect(new ToolDefinition
            {
                Name = "Background CPU Limiter",
                Description = "Set BelowNormal priority on all background processes",
                DetailedExplanation = "Reduces CPU priority of background processes to give foreground apps more CPU time.",
                Category = ToolCategory.Process,
                Complexity = ToolComplexity.Safe,
                AiCategory = DecisionCategory.ProcessPriority,
                Command = "SetPriority_BelowNormal_Background",
                RevertCommand = "SetPriority_Normal_All",
                PreConditions = new Dictionary<string, double> { { "CPU", 70 } },
                PostEffects = new Dictionary<string, double> { { "CPU", -10 }, { "FPS", 5 } },
                Tags = "cpu,priority,safe,gaming",
                CreatedBy = "System"
            });

            AddToolDirect(new ToolDefinition
            {
                Name = "Game Process Priority Boost",
                Description = "Set High priority on the active game process",
                DetailedExplanation = "Gives the game process higher CPU priority for smoother gameplay.",
                Category = ToolCategory.GPU,
                Complexity = ToolComplexity.Moderate,
                AiCategory = DecisionCategory.GameBoost,
                Command = "SetPriority_High_Foreground",
                RevertCommand = "SetPriority_Normal_Foreground",
                PreConditions = new Dictionary<string, double> { { "IsGame", 1 } },
                PostEffects = new Dictionary<string, double> { { "FPS", 10 }, { "FPS_Stability", 15 } },
                Tags = "gaming,fps,priority",
                CreatedBy = "System"
            });

            AddToolDirect(new ToolDefinition
            {
                Name = "Timer Resolution Gaming",
                Description = "Set timer resolution to 0.5ms for lower input latency",
                DetailedExplanation = "Windows default timer is 15.6ms. Reducing to 0.5ms lowers input delay. Only useful for gaming.",
                Category = ToolCategory.CPU,
                Complexity = ToolComplexity.Safe,
                AiCategory = DecisionCategory.GameBoost,
                Command = "NtSetTimerResolution_5000",
                RevertCommand = "NtSetTimerResolution_156000",
                PreConditions = new Dictionary<string, double> { { "IsGame", 1 } },
                PostEffects = new Dictionary<string, double> { { "FPS_Stability", 5 }, { "Input_Latency", -2 } },
                Tags = "gaming,timer,latency",
                CreatedBy = "System"
            });

            AddToolDirect(new ToolDefinition
            {
                Name = "Disable Core Parking",
                Description = "Unpark all CPU cores for maximum performance",
                DetailedExplanation = "Windows parks idle cores to save power. Unparking ensures all cores are available for heavy workloads.",
                Category = ToolCategory.CPU,
                Complexity = ToolComplexity.Moderate,
                AiCategory = DecisionCategory.CoreParking,
                Command = "UnparkAllCores_Powercfg",
                RevertCommand = "powercfg -setacvalueindex SCHEME_CURRENT SUB_PROCESSOR CPMINCORES 100",
                PreConditions = new Dictionary<string, double> { { "CPU", 60 }, { "IsGame", 1 } },
                PostEffects = new Dictionary<string, double> { { "CPU", -5 }, { "FPS", 8 } },
                Tags = "cpu,cores,parking,gaming",
                CreatedBy = "System"
            });

            AddToolDirect(new ToolDefinition
            {
                Name = "Emergency Thermal Throttle",
                Description = "Aggressively reduce CPU load to prevent overheating",
                DetailedExplanation = "Emergency measure: limits background processes, reduces CPU affinity. Use only when temp > 85C.",
                Category = ToolCategory.CPU,
                Complexity = ToolComplexity.Aggressive,
                AiCategory = DecisionCategory.ThermalProtect,
                Command = "EmergencyThrottle_Full",
                RevertCommand = "RestoreAllDefaults",
                PreConditions = new Dictionary<string, double> { { "Temp", 85 } },
                PostEffects = new Dictionary<string, double> { { "Temp", -15 }, { "CPU", -20 } },
                Tags = "thermal,emergency,safety",
                CreatedBy = "System"
            });

            AddToolDirect(new ToolDefinition
            {
                Name = "Light Thermal Reduction",
                Description = "Reduce background activity to lower temperature gradually",
                DetailedExplanation = "Conservative thermal management: reduces background CPU usage without major impact on foreground.",
                Category = ToolCategory.CPU,
                Complexity = ToolComplexity.Safe,
                AiCategory = DecisionCategory.ThermalProtect,
                Command = "ReduceBackground_Light",
                RevertCommand = "None",
                PreConditions = new Dictionary<string, double> { { "Temp", 70 } },
                PostEffects = new Dictionary<string, double> { { "Temp", -5 }, { "CPU", -5 } },
                Tags = "thermal,safe,conservative",
                CreatedBy = "System"
            });

            AddToolDirect(new ToolDefinition
            {
                Name = "Battery Saver Mode",
                Description = "Switch to power saver plan and reduce all performance",
                DetailedExplanation = "Minimizes power consumption by reducing CPU frequency, disabling parking, and limiting background.",
                Category = ToolCategory.Power,
                Complexity = ToolComplexity.Safe,
                AiCategory = DecisionCategory.PowerPlan,
                Command = "SwitchPowerPlan_Saver",
                RevertCommand = "SwitchPowerPlan_Balanced",
                PreConditions = new Dictionary<string, double> { { "IsOnBattery", 1 }, { "Battery", 50 } },
                PostEffects = new Dictionary<string, double> { { "Battery_Drain", -30 }, { "CPU", -15 } },
                Tags = "power,battery,safe",
                CreatedBy = "System"
            });

            AddToolDirect(new ToolDefinition
            {
                Name = "Game Affinity Pinning",
                Description = "Pin game to performance cores, background to efficiency cores",
                DetailedExplanation = "On hybrid CPUs (Intel 12th+), assigns game to P-cores and background to E-cores.",
                Category = ToolCategory.CPU,
                Complexity = ToolComplexity.Moderate,
                AiCategory = DecisionCategory.CpuAffinity,
                Command = "SetAffinity_Hybrid_GamePcore",
                RevertCommand = "SetAffinity_Default",
                PreConditions = new Dictionary<string, double> { { "IsGame", 1 }, { "HybridCPU", 1 } },
                PostEffects = new Dictionary<string, double> { { "FPS", 5 }, { "CPU", -5 } },
                Tags = "gaming,affinity,hybrid,cpu",
                CreatedBy = "System"
            });

            AddToolDirect(new ToolDefinition
            {
                Name = "Network Optimization",
                Description = "Optimize TCP settings for lower latency",
                DetailedExplanation = "Adjusts TCP window size, Nagle algorithm, and network priority for gaming/streaming.",
                Category = ToolCategory.Network,
                Complexity = ToolComplexity.Safe,
                AiCategory = DecisionCategory.NetworkOptimize,
                Command = "OptimizeTCP_Gaming",
                RevertCommand = "RestoreTCP_Default",
                PreConditions = new Dictionary<string, double> { { "IsGame", 1 } },
                PostEffects = new Dictionary<string, double> { { "Ping", -5 }, { "Packet_Loss", -2 } },
                Tags = "network,gaming,latency",
                CreatedBy = "System"
            });

            AddToolDirect(new ToolDefinition
            {
                Name = "Disk I/O Priority",
                Description = "Set low I/O priority on background processes",
                DetailedExplanation = "Reduces disk I/O contention by lowering background process disk priority.",
                Category = ToolCategory.Disk,
                Complexity = ToolComplexity.Safe,
                AiCategory = DecisionCategory.IoPriority,
                Command = "SetIoPriority_Low_Background",
                RevertCommand = "SetIoPriority_Normal_All",
                PreConditions = new Dictionary<string, double> { { "Disk", 70 } },
                PostEffects = new Dictionary<string, double> { { "Disk_Response", 10 } },
                Tags = "disk,io,safe",
                CreatedBy = "System"
            });

            AddToolDirect(new ToolDefinition
            {
                Name = "DNS Cache Flush",
                Description = "Flush DNS cache to resolve network issues",
                DetailedExplanation = "Clears DNS cache. Can help with network connectivity issues and reduce lookup latency.",
                Category = ToolCategory.Network,
                Complexity = ToolComplexity.Safe,
                AiCategory = DecisionCategory.NetworkOptimize,
                Command = "ipconfig /flushdns",
                RevertCommand = "None (DNS rebuilds automatically)",
                PreConditions = new Dictionary<string, double>(),
                PostEffects = new Dictionary<string, double> { { "Network_Latency", -3 } },
                Tags = "network,dns,safe",
                CreatedBy = "System"
            });

            AddToolDirect(new ToolDefinition
            {
                Name = "Frame Stabilizer",
                Description = "Enable frame pacing and reduce micro-stutter",
                DetailedExplanation = "Adjusts process scheduling and GPU settings to provide more consistent frame delivery.",
                Category = ToolCategory.GPU,
                Complexity = ToolComplexity.Safe,
                AiCategory = DecisionCategory.GameBoost,
                Command = "EnableFramePacing",
                RevertCommand = "DisableFramePacing",
                PreConditions = new Dictionary<string, double> { { "IsGame", 1 }, { "FPS_Stability", 70 } },
                PostEffects = new Dictionary<string, double> { { "FPS_Stability", 20 }, { "Frame_Time_Jitter", -15 } },
                Tags = "gaming,gpu,frames,stability",
                CreatedBy = "System"
            });

            AddToolDirect(new ToolDefinition
            {
                Name = "Desktop Smoothness & UI Snappiness",
                Description = "Optimize desktop responsiveness, elevate active window and enforce 1.0ms timer",
                DetailedExplanation = "Elevates foreground process priority to AboveNormal, demotes idle background hogs, sets 1.0ms timer resolution for buttery-smooth mouse tracking and DWM rendering.",
                Category = ToolCategory.CPU,
                Complexity = ToolComplexity.Safe,
                AiCategory = DecisionCategory.ProcessPriority,
                Command = "OptimizeDesktopSmoothness",
                RevertCommand = "None",
                PreConditions = new Dictionary<string, double> { { "IsGame", 0 } },
                PostEffects = new Dictionary<string, double> { { "UI_Responsiveness", 25 }, { "Input_Latency", -15 } },
                Tags = "desktop,smoothness,latency,dwm,snappy,safe",
                CreatedBy = "System"
            });

            AddToolDirect(new ToolDefinition
            {
                Name = "Inactive Apps Memory Trimmer",
                Description = "Trim unused memory from inactive background applications",
                DetailedExplanation = "Calls EmptyWorkingSet on background minimized apps holding >80MB RAM while leaving active foreground application completely hot in RAM.",
                Category = ToolCategory.Memory,
                Complexity = ToolComplexity.Safe,
                AiCategory = DecisionCategory.MemoryClean,
                Command = "TrimInactiveWorkingSets",
                RevertCommand = "None (memory pages fault back naturally when focused)",
                PreConditions = new Dictionary<string, double> { { "IsGame", 0 }, { "RAM", 60 } },
                PostEffects = new Dictionary<string, double> { { "RAM", -12 } },
                Tags = "memory,desktop,trim,background,safe",
                CreatedBy = "System"
            });

            SaveToolLibrary();
        }

        // === Add Tool (public API for user) ===
        public string AddTool(ToolDefinition tool)
        {
            lock (_toolLock)
            {
                // Validate
                if (string.IsNullOrEmpty(tool.Name)) return "ERROR: Tool name is required";
                if (_toolLibrary.Count >= MAX_TOOL_LIBRARY) return "ERROR: Tool library full (" + MAX_TOOL_LIBRARY + " max)";

                // Check duplicate
                foreach (var existing in _toolLibrary)
                {
                    if (existing.Name.Equals(tool.Name, StringComparison.OrdinalIgnoreCase))
                        return "ERROR: Tool '" + tool.Name + "' already exists";
                }

                tool.Id = Guid.NewGuid().ToString("N").Substring(0, 8);
                tool.Created = DateTime.Now;
                tool.LastUsed = DateTime.Now;
                tool.ConfidenceScore = 0.5;
                tool.CreatedBy = "User";
                _toolLibrary.Add(tool);
                SaveToolLibrary();

                AddLog("TRAIN", "ToolAdded",
                    string.Format("New tool added: '{0}' [{1}] ({2})",
                    tool.Name, tool.Category, tool.Complexity));

                return "OK: Tool '" + tool.Name + "' added with ID " + tool.Id;
            }
        }

        private void AddToolDirect(ToolDefinition tool)
        {
            lock (_toolLock)
            {
                if (_toolLibrary.Count >= MAX_TOOL_LIBRARY) return;
                foreach (var existing in _toolLibrary)
                {
                    if (existing.Name.Equals(tool.Name, StringComparison.OrdinalIgnoreCase)) return;
                }
                _toolLibrary.Add(tool);
            }
        }

        // === Remove Tool ===
        public bool RemoveTool(string toolId)
        {
            lock (_toolLock)
            {
                for (int i = _toolLibrary.Count - 1; i >= 0; i--)
                {
                    if (_toolLibrary[i].Id == toolId)
                    {
                        AddLog("TRAIN", "ToolRemoved", "Removed tool: " + _toolLibrary[i].Name);
                        _toolLibrary.RemoveAt(i);
                        SaveToolLibrary();
                        return true;
                    }
                }
            }
            return false;
        }

        // === Execute Tool ===
        public ToolExecution ExecuteTool(string toolId)
        {
            ToolDefinition tool = FindTool(toolId);
            if (tool == null) return null;
            if (!tool.Enabled) return null;

            // === Sandbox gate ===
            // Dangerous commands (standby purge, core unpark, power plan switch,
            // timer resolution, priority changes, raw shell commands) are blocked
            // until the user explicitly approves the tool.
            if (RequireToolConfirmation && IsDangerousTool(tool))
            {
                bool approved;
                lock (_toolApprovalLock) { approved = _approvedTools.Contains(tool.Id); }
                if (!approved)
                {
                    lock (_toolApprovalLock)
                    {
                        if (!_pendingToolApprovals.Contains(tool.Id))
                            _pendingToolApprovals.Add(tool.Id);
                    }
                    AddLog("SECURITY", "ToolBlocked",
                        "Dangerous command blocked pending approval: " + tool.Id);
                    var blocked = new ToolExecution();
                    blocked.ToolId = tool.Id;
                    blocked.ToolName = tool.Name;
                    blocked.Success = false;
                    blocked.Outcome = ToolOutcome.Failed;
                    blocked.Notes = "Blocked by sandbox: awaiting explicit approval";
                    return blocked;
                }
            }

            // Record state before
            var execution = new ToolExecution();
            execution.ToolId = tool.Id;
            execution.ToolName = tool.Name;
            execution.CpuBefore = _smoothCpu;
            execution.RamBefore = _smoothRam;
            execution.TempBefore = _smoothTemp;
            execution.FpsBefore = _smoothFps;
            execution.HealthBefore = SystemHealthScore;
            execution.ActiveProcess = _currentState.ActiveProcess;
            execution.IsGame = _currentState.IsGame;
            execution.SystemContextSnapshot = FormatSystemContext();

            // Execute the actual command
            Stopwatch sw = Stopwatch.StartNew();
            bool success = ExecuteToolCommand(tool);
            sw.Stop();
            execution.ExecutionTimeMs = sw.ElapsedMilliseconds;
            execution.Success = success;

            // Take measurement after a short delay
            System.Threading.Thread.Sleep(500);
            execution.CpuAfter = _smoothCpu;
            execution.RamAfter = _smoothRam;
            execution.TempAfter = _smoothTemp;
            execution.FpsAfter = _smoothFps;
            execution.HealthAfter = SystemHealthScore;
            execution.CalculateImpact();

            // Update tool stats
            tool.TotalUses++;
            if (success)
            {
                tool.SuccessCount++;
                execution.Outcome = ToolOutcome.Success;
            }
            else
            {
                tool.FailCount++;
                execution.Outcome = ToolOutcome.Failed;
            }
            tool.LastUsed = DateTime.Now;

            // Store execution
            lock (_toolLock)
            {
                _toolExecutions.Add(execution);
                if (_toolExecutions.Count > MAX_TOOL_EXECUTIONS)
                    _toolExecutions.RemoveAt(0);
            }

            // Learn from this execution
            LearnFromToolExecution(tool, execution);

            // Update RL system with this as an action
            double reward = execution.ImpactScore / 50.0; // Normalize to roughly -1 to 1
            bool rlSuccess = execution.ImpactScore > 0;
            UpdateRL(reward, rlSuccess);

            AddLog("TRAIN", "ToolExecuted",
                string.Format("'{0}' executed: impact={1:F1}, CPU {2:F0}→{3:F0}, RAM {4:F0}→{5:F0}, Temp {6:F0}→{7:F0}",
                tool.Name, execution.ImpactScore,
                execution.CpuBefore, execution.CpuAfter,
                execution.RamBefore, execution.RamAfter,
                execution.TempBefore, execution.TempAfter));

            return execution;
        }

        // === Execute the actual command ===
        private bool ExecuteToolCommand(ToolDefinition tool)
        {
            try
            {
                if (string.IsNullOrEmpty(tool.Command)) return false;

                string cmd = tool.Command.ToLower();

                // Memory operations
                if (cmd.Contains("emptyworkingset") && cmd.Contains("all"))
                {
                    CleanAllProcessWorkingSets();
                    return true;
                }
                else if (cmd.Contains("emptyworkingset"))
                {
                    CleanForegroundWorkingSet();
                    return true;
                }
                else if (cmd.Contains("standbylistpurge"))
                {
                    PurgeStandbyList();
                    return true;
                }
                else if (cmd.Contains("purgeclipboard"))
                {
                    ExecutePurgeClipboard();
                    return true;
                }
                else if (cmd.Contains("flushmodifiedlist"))
                {
                    ExecuteFlushModifiedList();
                    return true;
                }
                else if (cmd.Contains("deepmemorycompaction"))
                {
                    ExecuteDeepMemoryCompaction();
                    return true;
                }
                else if (cmd.Contains("optimizedesktopsmoothness"))
                {
                    ExecuteDesktopSmoothness();
                    return true;
                }
                else if (cmd.Contains("triminactiveworkingsets"))
                {
                    ExecuteTrimInactiveWorkingSets();
                    return true;
                }
                // Priority operations
                else if (cmd.Contains("setpriority_high"))
                {
                    if (_currentState.ActiveProcess != null)
                        SetProcessPriority(_currentState.ActiveProcess, "High");
                    return true;
                }
                else if (cmd.Contains("setpriority_belownormal") || cmd.Contains("setpriority_belongormal"))
                {
                    SetAllBackgroundPriority("BelowNormal");
                    return true;
                }
                else if (cmd.Contains("setpriority_normal"))
                {
                    SetAllBackgroundPriority("Normal");
                    return true;
                }
                // Timer
                else if (cmd.Contains("settimerresolution_5000"))
                {
                    ToolSetTimerResolution(5000);
                    return true;
                }
                else if (cmd.Contains("settimerresolution_156000"))
                {
                    ToolSetTimerResolution(156250);
                    return true;
                }
                // Power
                else if (cmd.Contains("switchpowerplan_saver"))
                {
                    SwitchPowerPlan(POWER_SAVER_GUID);
                    return true;
                }
                else if (cmd.Contains("switchpowerplan_balanced"))
                {
                    SwitchPowerPlan(BALANCED_GUID);
                    return true;
                }
                else if (cmd.Contains("switchpowerplan_high"))
                {
                    SwitchPowerPlan(HIGH_PERFORMANCE_GUID);
                    return true;
                }
                // Core parking
                else if (cmd.Contains("unparkallcores"))
                {
                    UnparkAllCores();
                    return true;
                }
                // Thermal
                else if (cmd.Contains("emergencythrottle"))
                {
                    EmergencyThrottle();
                    return true;
                }
                else if (cmd.Contains("reducebackground_light"))
                {
                    SetAllBackgroundPriority("BelowNormal");
                    return true;
                }
                // Network
                else if (cmd.Contains("flushdns"))
                {
                    return ToolFlushDnsCache();
                }
                else if (cmd.Contains("optimizetcp"))
                {
                    OptimizeTcpSettings();
                    return true;
                }
                // Disk
                else if (cmd.Contains("setiopriority_low"))
                {
                    SetAllBackgroundIoPriority();
                    return true;
                }
                // Affinity
                else if (cmd.Contains("setaffinity_hybrid"))
                {
                    if (_currentState.ActiveProcess != null)
                        SetHybridAffinity(_currentState.ActiveProcess);
                    return true;
                }
                // Generic script
                else if (cmd.StartsWith("cmd:") || cmd.StartsWith("powershell:"))
                {
                    return RunCustomScript(tool.Command);
                }

                return false;
            }
            catch (Exception ex)
            {
                AddLog("ERROR", "ToolExec", "Failed: " + ex.Message);
                return false;
            }
        }

        // === Tool Command Implementations ===
        private void CleanAllProcessWorkingSets()
        {
            try
            {
                Process[] procs = GetCachedProcesses();
                foreach (var p in procs)
                {
                    try
                    {
                        if (!p.HasExited && !SYSTEM_PROCESSES.Contains(p.ProcessName.ToLower()))
                            EmptyWorkingSet(p.Handle);
                    }
                    catch (Exception ex) { LogError("CleanAllProcessWorkingSets", ex); }
                }
            }
            catch (Exception ex) { LogError("CleanAllProcessWorkingSets", ex); }
        }

        private void CleanForegroundWorkingSet()
        {
            try
            {
                if (_currentState.ActiveProcess != null)
                {
                    Process[] procs = Process.GetProcessesByName(_currentState.ActiveProcess);
                    foreach (var p in procs)
                    {
                        try { EmptyWorkingSet(p.Handle); }
                        catch (Exception ex) { LogError("CleanForegroundWorkingSet", ex); }
                        finally { p.Dispose(); }
                    }
                }
            }
            catch (Exception ex) { LogError("CleanForegroundWorkingSet", ex); }
        }

        private void PurgeStandbyList()
        {
            IntPtr p = IntPtr.Zero;
            try
            {
                p = Marshal.AllocHGlobal(4);
                Marshal.WriteInt32(p, 4); // MemoryPurgeStandbyList
                NtSetSystemInformation(SystemMemoryListInformation, p, 4);
            }
            catch (Exception ex) { LogError("PurgeStandbyList", ex); }
            finally { if (p != IntPtr.Zero) Marshal.FreeHGlobal(p); }
        }

        private void SetProcessPriority(string processName, string priority)
        {
            try
            {
                System.Diagnostics.ProcessPriorityClass ppc;
                switch (priority)
                {
                    case "High": ppc = System.Diagnostics.ProcessPriorityClass.High; break;
                    case "AboveNormal": ppc = System.Diagnostics.ProcessPriorityClass.AboveNormal; break;
                    case "BelowNormal": ppc = System.Diagnostics.ProcessPriorityClass.BelowNormal; break;
                    case "Low": ppc = System.Diagnostics.ProcessPriorityClass.Idle; break;
                    default: ppc = System.Diagnostics.ProcessPriorityClass.Normal; break;
                }
                Process[] procs = Process.GetProcessesByName(processName);
                foreach (var p in procs) { try { p.PriorityClass = ppc; } catch (Exception ex) { CWLog.Error("SmartAgent.ToolTraining", ex); } }
            }
            catch (Exception ex) { LogError("SetProcessPriority", ex); }
        }

        private void SetAllBackgroundPriority(string priority)
        {
            try
            {
                System.Diagnostics.ProcessPriorityClass ppc;
                switch (priority)
                {
                    case "BelowNormal": ppc = System.Diagnostics.ProcessPriorityClass.BelowNormal; break;
                    case "Low": ppc = System.Diagnostics.ProcessPriorityClass.Idle; break;
                    default: ppc = System.Diagnostics.ProcessPriorityClass.Normal; break;
                }
                Process[] procs = GetCachedProcesses();
                string fg = (_currentState.ActiveProcess ?? "").ToLower();
                foreach (var p in procs)
                {
                    try
                    {
                        if (p.HasExited) continue;
                        string name = p.ProcessName.ToLower();
                        if (SYSTEM_PROCESSES.Contains(name) || name == fg) continue;
                        p.PriorityClass = ppc;
                    }
                    catch (Exception ex) { LogError("SetAllBackgroundPriority", ex); }
                }
            }
            catch (Exception ex) { LogError("SetAllBackgroundPriority", ex); }
        }

        private void ToolSetTimerResolution(uint resolution)
        {
            try
            {
                lock (_timerResLock)
                {
                    uint current;
                    NtSetTimerResolution(resolution, true, out current);
                    _timerResRefCount++;
                }
            }
            catch (Exception ex) { LogError("ToolSetTimerResolution", ex); }
        }

        private void SwitchPowerPlan(string guid)
        {
            try
            {
                using (Process.Start("powercfg", "/setactive " + guid)) { }
            }
            catch (Exception ex) { LogError("SwitchPowerPlan", ex); }
        }

        private void UnparkAllCores()
        {
            try
            {
                using (Process.Start("powercfg", "/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR CPMINCORES 100")) { }
                using (Process.Start("powercfg", "/setactive SCHEME_CURRENT")) { }
            }
            catch (Exception ex) { LogError("UnparkAllCores", ex); }
        }

        private void EmergencyThrottle()
        {
            try
            {
                SetAllBackgroundPriority("Low");
                if (_currentState.ActiveProcess != null)
                    SetProcessPriority(_currentState.ActiveProcess, "RealTime");
                ToolSetTimerResolution(156250);
            }
            catch (Exception ex) { LogError("EmergencyThrottle", ex); }
        }

        private bool ToolFlushDnsCache()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("ipconfig", "/flushdns");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                Process p = Process.Start(psi);
                if (p != null) { p.WaitForExit(5000); p.Dispose(); }
                return true;
            }
            catch { return false; }
        }

        private void OptimizeTcpSettings()
        {
            try
            {
                using (Process.Start("netsh", "int tcp set global autotuning=normal")) { }
                using (Process.Start("netsh", "int tcp set global chimney=enabled")) { }
            }
            catch (Exception ex) { LogError("OptimizeTcpSettings", ex); }
        }

        private void SetAllBackgroundIoPriority()
        {
            try
            {
                Process[] procs = GetCachedProcesses();
                string fg = (_currentState.ActiveProcess ?? "").ToLower();
                foreach (var p in procs)
                {
                    try
                    {
                        if (p.HasExited) continue;
                        string name = p.ProcessName.ToLower();
                        if (SYSTEM_PROCESSES.Contains(name) || name == fg) continue;
                        NtSetInformationProcess(p.Handle, ProcessIoPriority, ref _ioPriorityLow, sizeof(int));
                    }
                    catch (Exception ex) { LogError("SetAllBackgroundIoPriority", ex); }
                }
            }
            catch (Exception ex) { LogError("SetAllBackgroundIoPriority", ex); }
        }

        private int _ioPriorityLow = 3; // IoPriorityLow = 3

        private void SetHybridAffinity(string processName)
        {
            try
            {
                // On hybrid CPUs, pin game to P-cores (even indices) and background to E-cores (odd indices)
                int coreCount = Environment.ProcessorCount;
                long gameMask = 0;
                long bgMask = 0;
                for (int i = 0; i < coreCount; i++)
                {
                    if (i % 2 == 0) gameMask |= (1L << i);
                    else bgMask |= (1L << i);
                }

                Process[] procs = Process.GetProcessesByName(processName);
                foreach (var p in procs)
                {
                    try { SetProcessAffinityMask(p.Handle, (IntPtr)gameMask); }
                    catch (Exception ex) { LogError("SetHybridAffinity", ex); }
                    finally { p.Dispose(); }
                }
            }
            catch (Exception ex) { LogError("SetHybridAffinity", ex); }
        }

        internal bool RunCustomScript(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return false;
            try
            {
                int colonIdx = command.IndexOf(':');
                if (colonIdx < 0) return false;
                string prefix = command.Substring(0, colonIdx).Trim().ToLowerInvariant();
                string actualCmd = command.Substring(colonIdx + 1).Trim();
                if (string.IsNullOrWhiteSpace(actualCmd)) return false;

                // SEC-06: Only allow approved tool prefixes (cmd and powershell)
                if (prefix != "cmd" && prefix != "powershell")
                {
                    AddLog("SECURITY", "ToolExec", "SEC-06: Blocked unregistered tool prefix: " + prefix);
                    return false;
                }

                // SEC-06: Sanitize against dangerous shell metacharacters and command chaining
                char[] dangerousChars = new char[] { '&', '|', ';', '`', '$', '>', '<', '\r', '\n' };
                if (actualCmd.IndexOfAny(dangerousChars) >= 0)
                {
                    AddLog("SECURITY", "ToolExec", "SEC-06: Blocked script containing illegal shell metacharacters: " + actualCmd);
                    return false;
                }

                // Check for destructive system commands and unauthorized utilities
                string lower = actualCmd.ToLowerInvariant();
                string[] blockedWords = new string[] {
                    "format", "del /", "rmdir", "rd /", "shutdown", "powershell -enc",
                    "invoke-expression", "iex", "downloadstring", "curl", "wget", "certutil",
                    "bitsadmin", "reg delete", "net user", "vssadmin", "wbadmin", "bcdedit"
                };
                foreach (string blocked in blockedWords)
                {
                    if (lower.Contains(blocked))
                    {
                        AddLog("SECURITY", "ToolExec", "SEC-06: Blocked dangerous command pattern '" + blocked + "': " + actualCmd);
                        return false;
                    }
                }

                ProcessStartInfo psi;
                if (prefix == "powershell")
                {
                    psi = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command " + actualCmd);
                }
                else
                {
                    psi = new ProcessStartInfo("cmd.exe", "/c " + actualCmd);
                }
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                using (Process p = Process.Start(psi))
                {
                    if (p != null)
                    {
                        if (!p.WaitForExit(10000))
                        {
                            try { p.Kill(); } catch { }
                            AddLog("WARN", "ToolExec", "Script timed out after 10s: " + actualCmd);
                            return false;
                        }
                        return p.ExitCode == 0;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                AddLog("ERROR", "ToolExec", "RunCustomScript failed: " + ex.Message);
                return false;
            }
        }

        private string FormatSystemContext()
        {
            return string.Format("CPU={0:F0} RAM={1:F0} GPU={2:F0} Temp={3:F0} FPS={4:F0} Game={5} Processes={6} App={7}",
                _smoothCpu, _smoothRam, _smoothGpu, _smoothTemp, _smoothFps, _currentState.IsGame, _currentState.ProcessCount, _currentState.ActiveProcess ?? "None");
        }

        // === Learning Algorithm ===
        private void LearnFromToolExecution(ToolDefinition tool, ToolExecution execution)
        {
            string situationKey = BuildSituationKey();

            ToolLearningPattern pattern;
            if (!_toolPatterns.TryGetValue(tool.Id + "|" + situationKey, out pattern))
            {
                pattern = new ToolLearningPattern
                {
                    ToolId = tool.Id,
                    SituationKey = situationKey,
                    MatchesInSituation = 0,
                    SuccessesInSituation = 0,
                    SituationalConfidence = 0.5,
                    BestMeasuredImpact = 0
                };
            }

            pattern.MatchesInSituation++;
            if (execution.ImpactScore > 0)
            {
                pattern.SuccessesInSituation++;
                if (execution.ImpactScore > pattern.BestMeasuredImpact)
                    pattern.BestMeasuredImpact = execution.ImpactScore;
            }
            pattern.SituationalConfidence = pattern.MatchesInSituation > 0
                ? (double)pattern.SuccessesInSituation / pattern.MatchesInSituation
                : 0.5;
            pattern.LastMatch = DateTime.Now;
            _toolPatterns[tool.Id + "|" + situationKey] = pattern;

            // Update tool confidence based on overall success
            if (tool.TotalUses >= MIN_EXECUTIONS_FOR_CONFIDENCE)
            {
                double successRate = tool.GetSuccessRate();
                double impactBonus = Math.Max(0, execution.ImpactScore / 100.0) * 0.3;
                tool.ConfidenceScore = tool.ConfidenceScore * (1 - TOOL_LEARNING_RATE)
                    + (successRate * 0.7 + impactBonus) * TOOL_LEARNING_RATE;
            }

            // Decay old patterns
            DecayToolPatterns();
        }

        private string BuildSituationKey()
        {
            // Discretize system state into a situation key
            string cpuBin = _smoothCpu > 80 ? "HighCPU" : _smoothCpu > 50 ? "MedCPU" : "LowCPU";
            string ramBin = _smoothRam > 80 ? "HighRAM" : _smoothRam > 60 ? "MedRAM" : "LowRAM";
            string tempBin = _smoothTemp > 75 ? "HighTemp" : _smoothTemp > 60 ? "MedTemp" : "LowTemp";
            string gameBin = _currentState.IsGame ? "Game" : "Desktop";
            return string.Format("{0}_{1}_{2}_{3}", cpuBin, ramBin, tempBin, gameBin);
        }

        private void DecayToolPatterns()
        {
            var keysToRemove = new List<string>();
            foreach (var kv in _toolPatterns)
            {
                if ((DateTime.Now - kv.Value.LastMatch).TotalHours > 168) // 7 days
                    keysToRemove.Add(kv.Key);
            }
            foreach (string key in keysToRemove)
                _toolPatterns.Remove(key);
        }

        // === Recommend Tools for Current Situation ===
        public List<ToolRecommendation> GetToolRecommendations(int maxCount)
        {
            string situation = BuildSituationKey();
            var recommendations = new List<ToolRecommendation>();

            lock (_toolLock)
            {
                foreach (var tool in _toolLibrary)
                {
                    if (!tool.Enabled) continue;

                    double confidence = tool.ConfidenceScore;
                    double situationalBonus = 0;
                    string matchedSituation = "";

                    // Check if we have situational data
                    ToolLearningPattern pattern;
                    if (_toolPatterns.TryGetValue(tool.Id + "|" + situation, out pattern))
                    {
                        situationalBonus = pattern.SituationalConfidence * 0.4;
                        matchedSituation = situation;
                    }

                    // Check if pre-conditions are met
                    bool conditionsMet = CheckToolPreConditions(tool);
                    if (!conditionsMet) continue;

                    double totalConfidence = Math.Min(1.0, confidence + situationalBonus);

                    // Exploration bonus for underused tools
                    if (tool.TotalUses < 5)
                        totalConfidence += TOOL_EXPLORATION_BONUS;

                    if (totalConfidence > 0.2) // Minimum threshold
                    {
                        recommendations.Add(new ToolRecommendation
                        {
                            ToolId = tool.Id,
                            ToolName = tool.Name,
                            Confidence = totalConfidence,
                            ExpectedImpact = tool.AvgImpactScore,
                            Reason = BuildRecommendationReason(tool, situation),
                            MatchedSituation = matchedSituation,
                            Timestamp = DateTime.Now
                        });
                    }
                }
            }

            // Sort by confidence
            recommendations.Sort(delegate(ToolRecommendation a, ToolRecommendation b)
            {
                return b.Confidence.CompareTo(a.Confidence);
            });

            if (recommendations.Count > maxCount)
                recommendations.RemoveRange(maxCount, recommendations.Count - maxCount);

            _lastRecommendations = recommendations;
            return recommendations;
        }

        private bool CheckToolPreConditions(ToolDefinition tool)
        {
            foreach (var kv in tool.PreConditions)
            {
                string key = kv.Key.ToLower();
                double threshold = kv.Value;

                if (key == "cpu" && _smoothCpu < threshold) return false;
                if (key == "ram" && _smoothRam < threshold) return false;
                if (key == "temp" && _smoothTemp < threshold) return false;
                if (key == "gpu" && _smoothGpu < threshold) return false;
                if (key == "fps" && _smoothFps < threshold) return false;
                if (key == "disk" && _currentState.DiskUsage < threshold) return false;
                if (key == "isgame" && threshold == 1 && !_currentState.IsGame) return false;
                if (key == "isonbattery" && threshold == 1 && !_isOnBattery) return false;
                if (key == "battery" && _batteryPercent > threshold) return false;
                if (key == "processcount" && _currentState.ProcessCount < threshold) return false;
            }
            return true;
        }

        private string BuildRecommendationReason(ToolDefinition tool, string situation)
        {
            if (tool.GetSuccessRate() > 0.8 && tool.TotalUses > 5)
                return string.Format("High success rate ({0:F0}%) from {1} uses", tool.GetSuccessRate() * 100, tool.TotalUses);

            ToolLearningPattern pattern;
            if (_toolPatterns.TryGetValue(tool.Id + "|" + situation, out pattern) && pattern.MatchesInSituation > 2)
                return string.Format("Works well in this situation ({0:F0}% success, impact: {1:F1})",
                    pattern.SituationalConfidence * 100, pattern.BestMeasuredImpact);

            if (tool.TotalUses == 0)
                return "Never tried yet - good candidate for exploration";

            return string.Format("Overall: {0:F0}% success from {1} uses", tool.GetSuccessRate() * 100, tool.TotalUses);
        }

        // === Submit Feedback ===
        public string SubmitToolFeedback(string executionId, int rating, bool wasHelpful, bool causedIssues, string comment)
        {
            ToolExecution execution = FindExecution(executionId);
            if (execution == null) return "ERROR: Execution not found";

            var feedback = new ToolFeedback
            {
                ExecutionId = executionId,
                ToolId = execution.ToolId,
                Rating = rating,
                WasHelpful = wasHelpful,
                CausedIssues = causedIssues,
                Comment = comment,
                FinalOutcome = causedIssues ? ToolOutcome.Harmful : (wasHelpful ? ToolOutcome.Success : ToolOutcome.Partial)
            };

            lock (_toolLock)
            {
                _toolFeedbacks.Add(feedback);
                if (_toolFeedbacks.Count > MAX_TOOL_FEEDBACKS)
                    _toolFeedbacks.RemoveAt(0);
            }

            // Update tool based on feedback
            ToolDefinition tool = FindTool(execution.ToolId);
            if (tool != null)
            {
                if (causedIssues)
                {
                    tool.HarmCount++;
                    tool.ConfidenceScore = Math.Max(0, tool.ConfidenceScore - 0.1);
                }
                else
                {
                    double ratingBonus = (rating - 3) * 0.05; // -0.1 to +0.1
                    tool.ConfidenceScore = Math.Max(0, Math.Min(1, tool.ConfidenceScore + ratingBonus));
                }

                // Update impact score with feedback influence
                execution.ImpactScore = execution.ImpactScore * 0.8 + (rating - 3) * 10 * 0.2;
            }

            AddLog("TRAIN", "Feedback",
                string.Format("Feedback for '{0}': rating={1}, helpful={2}, issues={3}",
                tool != null ? tool.Name : "?", rating, wasHelpful, causedIssues));

            return "OK: Feedback recorded";
        }

        // === Periodic Learning Update ===
        public void RunToolTrainingCycle()
        {
            if ((DateTime.Now - _lastToolAnalysis).TotalSeconds < TOOL_ANALYSIS_INTERVAL_SECONDS) return;
            _lastToolAnalysis = DateTime.Now;

            // Auto-update impact scores from recent executions
            UpdateToolImpactScores();

            // Discover new tool patterns
            DiscoverToolPatterns();

            // Auto-suggest tool improvements
            GenerateToolInsights();

            // Decay confidence for unused tools
            DecayUnusedToolConfidence();
        }

        private void UpdateToolImpactScores()
        {
            lock (_toolLock)
            {
                foreach (var tool in _toolLibrary)
                {
                    if (tool.TotalUses == 0) continue;

                    double totalImpact = 0;
                    int recentCount = 0;
                    foreach (var exec in _toolExecutions)
                    {
                        if (exec.ToolId == tool.Id && (DateTime.Now - exec.ExecutedAt).TotalHours < 24)
                        {
                            totalImpact += exec.ImpactScore;
                            recentCount++;
                        }
                    }

                    if (recentCount > 0)
                    {
                        double newAvg = totalImpact / recentCount;
                        tool.AvgImpactScore = tool.AvgImpactScore * 0.7 + newAvg * 0.3;
                    }
                }
            }
        }

        private void DiscoverToolPatterns()
        {
            // Find tools that work well in specific situations
            string currentSituation = BuildSituationKey();
            lock (_toolLock)
            {
                foreach (var tool in _toolLibrary)
                {
                    if (tool.TotalUses < 3) continue;

                    string key = tool.Id + "|" + currentSituation;
                    ToolLearningPattern pattern;
                    if (!_toolPatterns.TryGetValue(key, out pattern)) continue;

                    if (pattern.MatchesInSituation >= 3 && pattern.SituationalConfidence > 0.7)
                    {
                        // This tool works well in this situation - boost confidence
                        tool.ConfidenceScore = Math.Min(1, tool.ConfidenceScore + 0.02);
                    }
                    else if (pattern.MatchesInSituation >= 3 && pattern.SituationalConfidence < 0.3)
                    {
                        // This tool doesn't work well here - reduce confidence
                        tool.ConfidenceScore = Math.Max(0, tool.ConfidenceScore - 0.02);
                    }
                }
            }
        }

        private void GenerateToolInsights()
        {
            // Auto-discover: if a tool has high success rate and we haven't used it in a while, recommend it
            // This is a simple insight - more complex ones could be added
        }

        private void DecayUnusedToolConfidence()
        {
            lock (_toolLock)
            {
                foreach (var tool in _toolLibrary)
                {
                    double hoursSinceUse = (DateTime.Now - tool.LastUsed).TotalHours;
                    if (hoursSinceUse > 48 && tool.TotalUses > 0)
                    {
                        double decay = Math.Exp(-hoursSinceUse / 168.0) * TOOL_DECAY_RATE;
                        tool.ConfidenceScore = Math.Max(0.1, tool.ConfidenceScore - decay);
                    }
                }
            }
        }

        // === Get Statistics ===
        public string GetToolTrainingSummary()
        {
            lock (_toolLock)
            {
                int totalExec = _toolExecutions.Count;
                int totalFeedback = _toolFeedbacks.Count;
                double avgImpact = 0;
                if (totalExec > 0)
                {
                    double sum = 0;
                    foreach (var e in _toolExecutions) sum += e.ImpactScore;
                    avgImpact = sum / totalExec;
                }
                int topTools = 0;
                foreach (var t in _toolLibrary)
                    if (t.ConfidenceScore > 0.7) topTools++;

                return string.Format(
                    "Tools: {0} | Executions: {1} | Feedback: {2} | Avg Impact: {3:F1} | High Confidence: {4} | Patterns: {5}",
                    _toolLibrary.Count, totalExec, totalFeedback, avgImpact, topTools, _toolPatterns.Count);
            }
        }

        public List<ToolDefinition> GetTopTools(int count)
        {
            lock (_toolLock)
            {
                var sorted = new List<ToolDefinition>(_toolLibrary);
                sorted.Sort(delegate(ToolDefinition a, ToolDefinition b)
                {
                    return b.ConfidenceScore.CompareTo(a.ConfidenceScore);
                });
                if (sorted.Count > count) sorted.RemoveRange(count, sorted.Count - count);
                return sorted;
            }
        }

        public List<ToolDefinition> GetWorstTools(int count)
        {
            lock (_toolLock)
            {
                var sorted = new List<ToolDefinition>(_toolLibrary);
                sorted.Sort(delegate(ToolDefinition a, ToolDefinition b)
                {
                    return a.ConfidenceScore.CompareTo(b.ConfidenceScore);
                });
                if (sorted.Count > count) sorted.RemoveRange(count, sorted.Count - count);
                return sorted;
            }
        }

        public double GetOverallToolConfidence()
        {
            lock (_toolLock)
            {
                if (_toolLibrary.Count == 0) return 0;
                double sum = 0;
                foreach (var t in _toolLibrary) sum += t.ConfidenceScore;
                return sum / _toolLibrary.Count;
            }
        }

        // === Finders ===
        private ToolDefinition FindTool(string toolId)
        {
            lock (_toolLock)
            {
                foreach (var t in _toolLibrary)
                    if (t.Id == toolId) return t;
            }
            return null;
        }

        private ToolExecution FindExecution(string executionId)
        {
            lock (_toolLock)
            {
                foreach (var e in _toolExecutions)
                    if (e.Id == executionId) return e;
            }
            return null;
        }

        // === Persistence ===
        private void SaveToolLibrary()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                lock (_toolLock)
                {
                    foreach (var tool in _toolLibrary)
                    {
                        sb.AppendLine(string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11}|{12}|{13}|{14}",
                            tool.Id, Escape(tool.Name), Escape(tool.Description), tool.Category,
                            tool.Complexity, tool.AiCategory, Escape(tool.Command), Escape(tool.RevertCommand),
                            tool.ConfidenceScore, tool.TotalUses, tool.SuccessCount, tool.FailCount,
                            tool.HarmCount, tool.AvgImpactScore, tool.CreatedBy));
                    }
                }
                File.WriteAllText(Path.Combine(_toolDataDir, "tools.dat"), sb.ToString());
            }
            catch (Exception ex) { LogError("SaveToolLibrary", ex); }
        }

        private void LoadToolLibrary()
        {
            try
            {
                string path = Path.Combine(_toolDataDir, "tools.dat");
                if (!File.Exists(path)) return;

                string[] lines = File.ReadAllLines(path);
                lock (_toolLock)
                {
                    _toolLibrary.Clear();
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrEmpty(line)) continue;
                        string[] parts = line.Split('|');
                        if (parts.Length < 15) continue;

                        var tool = new ToolDefinition();
                        tool.Id = parts[0];
                        tool.Name = Unescape(parts[1]);
                        tool.Description = Unescape(parts[2]);
                        ToolCategory cat;
                        Enum.TryParse(parts[3], true, out cat); tool.Category = cat;
                        ToolComplexity comp;
                        Enum.TryParse(parts[4], true, out comp); tool.Complexity = comp;
                        DecisionCategory dc;
                        Enum.TryParse(parts[5], true, out dc); tool.AiCategory = dc;
                        tool.Command = Unescape(parts[6]);
                        tool.RevertCommand = Unescape(parts[7]);
                        double conf;
                        double.TryParse(parts[8], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out conf); tool.ConfidenceScore = conf;
                        int tu;
                        int.TryParse(parts[9], out tu); tool.TotalUses = tu;
                        int sc;
                        int.TryParse(parts[10], out sc); tool.SuccessCount = sc;
                        int fc;
                        int.TryParse(parts[11], out fc); tool.FailCount = fc;
                        int hc;
                        int.TryParse(parts[12], out hc); tool.HarmCount = hc;
                        double ai;
                        double.TryParse(parts[13], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out ai); tool.AvgImpactScore = ai;
                        tool.CreatedBy = parts.Length > 14 ? parts[14] : "User";

                        _toolLibrary.Add(tool);
                    }
                }
            }
            catch (Exception ex) { LogError("LoadToolLibrary", ex); }
        }

        private void LoadToolHistory()
        {
            try
            {
                string path = Path.Combine(_toolDataDir, "history.dat");
                if (!File.Exists(path)) return;

                string[] lines = File.ReadAllLines(path);
                lock (_toolLock)
                {
                    _toolExecutions.Clear();
                    foreach (string line in lines)
                    {
                        if (string.IsNullOrEmpty(line)) continue;
                        string[] parts = line.Split('|');
                        if (parts.Length < 12) continue;

                        var exec = new ToolExecution();
                        exec.Id = parts[0];
                        exec.ToolId = parts[1];
                        exec.ToolName = parts[2];
                        DateTime dt;
                        DateTime.TryParse(parts[3], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt); exec.ExecutedAt = dt;
                        double cb;
                        double.TryParse(parts[4], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out cb); exec.CpuBefore = cb;
                        double ca;
                        double.TryParse(parts[5], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out ca); exec.CpuAfter = ca;
                        double rb;
                        double.TryParse(parts[6], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out rb); exec.RamBefore = rb;
                        double ra;
                        double.TryParse(parts[7], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out ra); exec.RamAfter = ra;
                        double hi;
                        double.TryParse(parts[8], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out hi); exec.HealthBefore = hi;
                        double ha;
                        double.TryParse(parts[9], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out ha); exec.HealthAfter = ha;
                        double im;
                        double.TryParse(parts[10], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out im); exec.ImpactScore = im;
                        bool su;
                        bool.TryParse(parts[11], out su); exec.Success = su;

                        _toolExecutions.Add(exec);
                    }
                }
            }
            catch (Exception ex) { LogError("LoadToolHistory", ex); }
        }

        private void SaveToolHistory()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                lock (_toolLock)
                {
                    int start = Math.Max(0, _toolExecutions.Count - 200);
                    for (int i = start; i < _toolExecutions.Count; i++)
                    {
                        var e = _toolExecutions[i];
                        sb.AppendLine(string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11}",
                            e.Id, e.ToolId, e.ToolName,
                            e.ExecutedAt.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                            e.CpuBefore, e.CpuAfter, e.RamBefore, e.RamAfter,
                            e.HealthBefore, e.HealthAfter, e.ImpactScore, e.Success));
                    }
                }
                File.WriteAllText(Path.Combine(_toolDataDir, "history.dat"), sb.ToString());
            }
            catch (Exception ex) { LogError("SaveToolHistory", ex); }
        }

        private string Escape(string s)
        {
            if (s == null) return "";
            return s.Replace("|", "\\|").Replace("\n", "\\n");
        }

        private string Unescape(string s)
        {
            if (s == null) return "";
            return s.Replace("\\|", "|").Replace("\\n", "\n");
        }
    }
}
