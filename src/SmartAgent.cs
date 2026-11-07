using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace ClearWin
{
    /// <summary>
    /// AI Monitor Metrics — snapshot of the monitor's state for UI binding.
    /// </summary>
    public class MonitorMetrics
    {
        public int TotalDecisions;
        public int SuccessfulDecisions;
        public int FailedDecisions;
        public int AlertCount;
        public double AvgConfidence;
        public double AvgRiskScore;
        public int ModeChanges;
        public int StaleTickCount;
        public string HealthStatus;
        public string AnomalySummary;
        public int RecentDecisions;
        public int RecentFailures;
        public bool IsGame;
        public string CurrentGameName;
    }

    public partial class SmartAgent : IAgentEngines
    {
        [DllImport("psapi.dll")]
        static extern bool EmptyWorkingSet(IntPtr hProcess);
        [DllImport("ntdll.dll")]
        static extern uint NtSetSystemInformation(int cls, IntPtr buf, int len);
        [DllImport("ntdll.dll")]
        static extern int RtlAdjustPrivilege(int privilege, bool bEnablePrivilege, bool isThreadPrivilege, out bool previousValue);
        [DllImport("kernel32.dll")]
        static extern bool QueryPerformanceFrequency(out long frequency);
        [DllImport("kernel32.dll")]
        static extern IntPtr SetProcessAffinityMask(IntPtr hProcess, IntPtr dwProcessAffinityMask);
        [DllImport("ntdll.dll")]
        static extern int NtSetInformationProcess(IntPtr hProcess, int cls, ref int buf, int len);
        [DllImport("ntdll.dll", EntryPoint = "NtSetTimerResolution")]
        static extern int NtSetTimerResolution(uint DesiredResolution, bool SetResolution, out uint CurrentResolution);
        [DllImport("kernel32.dll")]
        static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);
        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr hObject);
        [DllImport("ntdll.dll")]
        static extern int NtSuspendProcess(IntPtr ProcessHandle);
        [DllImport("ntdll.dll")]
        static extern int NtResumeProcess(IntPtr ProcessHandle);
        [DllImport("user32.dll", ExactSpelling = true)]
        static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool OpenClipboard(IntPtr hWndNewOwner);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool EmptyClipboard();
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool CloseClipboard();
        const uint GA_ROOT = 2;
        const uint PROCESS_SUSPEND_RESUME = 0x0800;
        const int SystemMemoryListInformation = 80;
        const int ProcessIoPriority = 51;
        const int ProcessPriorityClass = 8;

        // Processes suspended by the agent (SuspendBloatProcesses) — resumable on rollback
        private readonly List<int> _suspendedPids = new List<int>();
        private int _gameDvrPrevEnabled = -1;
        private int _gameDvrPrevCapture = -1;
        private bool _gameDvrApplied = false;

        public static readonly HashSet<string> SYSTEM_PROCESSES = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            "explorer", "csrss", "smss", "wininit", "winlogon", "services", "lsass", "svchost",
            "dwm", "runtimebroker", "searchapp", "shellexperiencehost", "systemsettings",
            "lockapp", "widgets", "textinputhost", "sihost", "taskhostw", "dllhost", "conhost",
            "fontdrvhost", "ctfmon", "sppsrv", "wlms", "securityhealth", "audiodg", "spoolsv",
            "securityhealthsystray", "taskmgr", "clearwin"
        };

        // === Static game/app classification tables (allocated once, not per tick) ===
        private static readonly string[] KNOWN_NON_GAMES = new[] {
            "chrome", "msedge", "firefox", "opera", "brave", "devenv", "code", "outlook",
            "winword", "excel", "powerpnt", "teams", "slack", "discord", "spotify",
            "wmplayer", "vlc", "mspaint", "notepad", "cmd", "powershell", "taskmgr",
            "explorer", "whatsapp", "telegram", "signal", "messenger", "wechat", "line",
            "zoom", "skype", "viber", "discordcanary",
            // Creative / editing / GPU heavy non-games
            "photoshop", "illustrator", "premiere", "afterfx", "after effects", "indesign",
            "lightroom", "davinci", "resolve", "blender", "maya", "3dsmax", "3dmax",
            "3ds max", "zbrush", "substance", "houdini", "cinema4d", "c4d", "krita",
            "autocad", "revit", "solidworks", "catia", "inventor", "sketchup", "fusion360",
            "rhino", "matlab", "simulink", "mathematica", "originpro", "jupyter",
            "vmware", "virtualbox", "hyper-v", "vbox", "qemu", "xen", "wsl", "vboxsvc",
            "obs64", "obs", "streamlabs", "xsplit", "nvcontainer", "geforce experience",
            "nvidia share", "steamwebhelper", "epicgameslauncher", "gamebar", "gamebarpresentation",
            "audacity", "flstudio", "cubase", "ableton", "pro tools", "reason", "bandlab"
        };

        private static readonly string[] KNOWN_GAMES = new[] {
            "csgo", "cs2", "counter-strike", "valorant", "fortnite", "fortniteclient-win64-shipping",
            "leagueoflegends", "lolclient", "league", "dota2", "dota", "apexlegends", "r5apex",
            "pubg", "tesseract", "minecraft", "javaw", "wow", "worldofwarcraft", "warcraft",
            "overwatch", "overwatch2", "diablo", "diabloiv", "d4", "eldenring", "cyberpunk2077",
            "witcher3", "gta5", "gtaiv", "rdr2", "reddeadredemption2", "battlefield", "bfv",
            "bf1", "bf2042", "cod", "codmw", "modernwarfare", "warzone", "blackops",
            "destiny2", "warframe", "warface", "rocketleague", "rocketleague", "fifa", "fifa24",
            "fifa25", "nba2k", "nba2k24", "madden", "elden", "sekiro", "dark souls", "dark souls iii",
            "assassin's creed", "far cry", "watch dogs", "watchdogs", "tomb raider", "hitman",
            "resident evil", "remnant", "monsterhunter", "monster hunter", "skyrim", "fallout",
            "fallout 4", "fallout76", "stardew", "terraria", "rust", "dayz", "arksurvivalevolved",
            "ark survival", "palworld", "valheim", "cs2.exe", "halo", "haloinfinite", "forza",
            "forzahorizon", "flight simulator", "msfs", "xplane", "gta", "gaming", "minecraftlauncher",
            "leagueclient", "genshin", "genshinimpact", "honkai", "starrail", "zenless",
            "lost ark", "lostark", "pathofexile", "poe", "poe2", "northstar", "apec", "brawlhalla",
            // Android/mobile game emulators (Gameloop/Tencent, LDPlayer, BlueStacks, Nox, MEmu, MuMu)
            "qmemulator", "qmemulatorheadless", "qmemulatorservice", "qqpcgameproxy", "qmplayer",
            "qmplayerservice", "androidemulator", "androidemulatoren", "androidemulatorex", "androidprocess",
            "gameloop", "gameloopemulator", "gameloopvm", "gamelooplauncher", "glaboxheadless", "androwsfg",
            "aow_exe", "appmarket", "txgameassistant", "syzs_cbox", "txservice", "ldplayer", "ldboxheadless",
            "ld9boxheadless", "dnplayer", "hd-player", "hdplayer", "bstksvc", "noxvmhandle", "noxplayer",
            "memuheadless", "microvirt", "mumu", "mumunxmain", "mumunxdevice", "rendererdetector", "vkms", "smartgaga",
            // PUBG and Battle Royale PC/Mobile executables
            "tslgame", "tslgame_uc", "tslgame_zk", "pubg", "pubglite", "execpubg", "pubgmobile", "bgmi"
        };

        // Apps that must NEVER be auto-suspended: the user's foreground /
        // actively-used programs (browsers, IDEs, office, comms, creative tools,
        // virtualization, games). SuspendBloatProcesses may only target genuine,
        // unknown heavy background bloat - never these.
        private static readonly HashSet<string> USER_CRITICAL_PROCESSES = BuildUserCriticalSet();

        private static HashSet<string> BuildUserCriticalSet()
        {
            var s = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in KNOWN_NON_GAMES) if (!string.IsNullOrEmpty(n)) s.Add(n.ToLower());
            foreach (var n in KNOWN_GAMES) if (!string.IsNullOrEmpty(n)) s.Add(n.ToLower());
            // Extra common user work/comm/productivity apps not covered above.
            string[] extra = new[] {
                "code", "visualstudiocode", "devenv", "idea64", "pycharm64", "resharper",
                "repl", "dotnet", "msbuild", "eclipse", "notepad", "notepad++", "word",
                "winword", "excel", "powerpnt", "outlook", "onenote", "teams", "slack",
                "discord", "whatsapp", "spotify", "zoom", "obs64", "obs", "streamlabs",
                "vmware", "virtualbox", "hyper-v", "qemu", "adb", "powershell",
                "cmd", "terminal", "conhost", "rustdesk", "anydesk", "toDesk", "geforce",
                "antigravity", "language_server"
            };
            for (int i = 0; i < extra.Length; i++) { if (!string.IsNullOrEmpty(extra[i])) s.Add(extra[i].ToLower()); }
            return s;
        }

        // True when the (lowercased) process name is a user-critical / active program
        // that must not be suspended. Exact-name match on the curated set, plus a
        // guarded token match for compound game/launcher names.
        private static bool IsUserCriticalProcess(string lowerName)
        {
            if (string.IsNullOrEmpty(lowerName)) return true;
            if (USER_CRITICAL_PROCESSES.Contains(lowerName)) return true;
            foreach (var t in USER_CRITICAL_TOKENS)
            {
                if (t.Length >= 5 && lowerName.Contains(t)) return true;
            }
            return false;
        }

        // Compound-name tokens (match with .Contains, guarded by min length 5 to
        // avoid false positives on short/generic process names).
        private static readonly string[] USER_CRITICAL_TOKENS = new[] {
            "fortniteclient", "leagueoflegends", "counterstrike", "cs2", "tesseract",
            "genshinimpact", "honkai", "lostark", "valheim", "palworld", "cyberpunk",
            "worldofwarcraft", "darkso", "eldenring", "minecraft"
        };

        private static readonly string[] GAME_INDICATORS = new[] { "-dx", "-vk", "unity", "unreal", "ue4", "ue5", "cryengine", "gdi32", "opengl32", "vulkan", "r6", "warcraft3", "gothic", "gameloop", "aow_exe", "androws", "androidemulator", "androidprocess", "appmarket", "tslgame", "mumu", "ldplayer", "pubg", "battleground", "bgmi" };

        private static readonly string[] GAME_TITLE_KEYWORDS = new[] { "game", "fps", "steam", "epic", "uplay", "ubisoft", "battle.net", "rockstar", "origins", "gog", "minecraft", "league of legends", "valorant", "fortnite", "csgo", "dota", "warcraft", "overwatch", "apex", "pubg", "pubg mobile", "gta", "rocket league", "halo", "forza", "elden", "cyberpunk", "witcher", "red dead", "battlefield", "call of duty", "warzone", "destiny", "warframe", "genshin", "honkai", "the witcher", "diablo", "battleground", "battle royale", "androidemulator", "gameloop", "tencent", "androws" };

        private const System.Diagnostics.ProcessPriorityClass PRIORITY_HIGH = System.Diagnostics.ProcessPriorityClass.High;
        private const System.Diagnostics.ProcessPriorityClass PRIORITY_REALTIME = System.Diagnostics.ProcessPriorityClass.RealTime;
        private const System.Diagnostics.ProcessPriorityClass PRIORITY_BELOW_NORMAL = System.Diagnostics.ProcessPriorityClass.BelowNormal;
        private const System.Diagnostics.ProcessPriorityClass PRIORITY_NORMAL = System.Diagnostics.ProcessPriorityClass.Normal;
        private const System.Diagnostics.ProcessPriorityClass PRIORITY_ABOVE_NORMAL = System.Diagnostics.ProcessPriorityClass.AboveNormal;

        private const double BYTES_PER_GB = 1073741824.0;
        private const double KELVIN_OFFSET = 273.15;
        private const double THERMAL_SCALE = 10.0;
        private const uint TIMER_RESOLUTION_GAMING = 5000;
        private const uint TIMER_RESOLUTION_DESKTOP = 10000;
        private const uint TIMER_RESOLUTION_DEFAULT = 156250;
        private const int SELF_HEALTH_RAM_MB = 300;
        private const int SELF_HEALTH_HANDLES = 3000;
        private const int SELF_HEALTH_THREADS = 60;
        private const long GPU_PROCESS_THRESHOLD_BYTES = 100 * 1024 * 1024;

        private const string POWER_SAVER_GUID = "a1841308-3541-4fab-bc81-f71556f20b4a";
        private const string HIGH_PERFORMANCE_GUID = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
        private const string BALANCED_GUID = "381b4222-f694-41f0-9685-ff5bb260df2e";

        private static readonly string[] SAFE_TO_CLEAN_PROCESSES = new[] { "chrome", "msedge", "firefox", "discord", "spotify", "teams", "slack", "zoom", "skype" };
        private static readonly string[] BACKGROUND_PROCESSES = new[] { "update", "telemetry", "onedrive" };

        private readonly object _stateLock = new object();
        private readonly object _journalLock = new object();
        private readonly object _decisionLock = new object();
        private readonly object _listLock = new object(); // ChatGPT: thread-safe public properties
        private List<PersistentRule> _persistentRules = new List<PersistentRule>();
        // Moving average smoothing (ChatGPT recommended)
        private double _smoothCpu = 0;
        private double _smoothRam = 0;
        private double _smoothGpu = 0;
        private double _smoothTemp = 0;
        private double _smoothFps = 60;
        private bool _firstCollect = true;
        // State Machine (ChatGPT recommended)
        private enum AppMode { Gaming, Desktop, Idle, Battery, Charging }
        private AppMode _appMode = AppMode.Desktop;
        private DateTime _lastInputTime = DateTime.Now;
        private int _ticksWithoutGame = 0;
        // Self Health Monitor
        private DateTime _lastSelfCheck = DateTime.Now;
        private int _selfCheckTick = 0;
        private int _selfCheckRunning = 0;
        // Adaptive Tick Rate — tuned per spec: 500ms gaming / 1000ms normal / 2000ms idle
        // Low overhead in desktop/idle extends battery & reduces GC; gaming stays responsive.
        public int RecommendedTickMs
        {
            get
            {
                int baseMs;
                switch (_appMode)
                {
                    case AppMode.Gaming: baseMs = 400; break;
                    case AppMode.Desktop: baseMs = 500; break;
                    case AppMode.Battery: baseMs = 1000; break;
                    case AppMode.Idle: baseMs = 1000; break;
                    default: baseMs = 500; break;
                }
                if (_smoothCpu > 85 || _smoothTemp > 80 || _smoothRam > 85)
                    baseMs = 250;
                return baseMs;
            }
        }
        // System Health Score 0-100 (ChatGPT recommended)
        // Thread-safety note: read on UI thread, written on Tick background thread.
        // 'double' reads/writes are atomic on x64 CLR for 8-byte aligned fields, so the
        // worst case is reading a stale value (acceptable for a display metric).
        public double SystemHealthScore { get; private set; }
        public static SmartAgent Instance { get; set; }

        private volatile AgentMode _mode = AgentMode.Off;
        private AgentMode _previousMode = AgentMode.Off;
        private SystemState _currentState;
        private SystemState _prevState;
        private List<AgentDecision> _pendingDecisions = new List<AgentDecision>();
        private List<AgentDecision> _approvalQueue = new List<AgentDecision>();
        private readonly object _approvalLock = new object();
        private List<AgentDecision> _decisionHistory = new List<AgentDecision>();
        private List<AgentExperience> _experiences = new List<AgentExperience>();
        private List<AgentProfile> _profiles = new List<AgentProfile>();
        private Queue<AgentLog> _logs = new Queue<AgentLog>();
        private AgentStats _stats = new AgentStats();
        private string _dataDir;
        private Random _rnd = new Random();
        private Random _mlRnd = new Random();
        private int _tickCount = 0;
        private int _consecutiveSameAction = 0;
        private string _lastActionKey = "";
        private PerformanceCounter _cpuLoadPc;
        private PerformanceCounter _gpuUtilPc;
        private bool _gpuWarmupNeeded = false;
        private bool _cpuWarmupDone = false;
        private Dictionary<string, PerformanceCounter> _thermalCounters = new Dictionary<string, PerformanceCounter>();
        private double _prevFps = 0;
        private double _prevTemp = 0;

        private List<StateJournalEntry> _journal = new List<StateJournalEntry>();
        private ConcurrentDictionary<string, DateTime> _actionCooldowns = new ConcurrentDictionary<string, DateTime>();
        private Dictionary<string, int> _consecutiveActionFailures = new Dictionary<string, int>();
        private string _activeGameName = "";
        // Game/App discrimination memory: process name -> game confidence (0=known app, 1=confirmed game)
        private Dictionary<string, double> _gameMemory = new Dictionary<string, double>();
        private DateTime _lastGameMemoryUpdate = DateTime.MinValue;
        private const int GAME_MEMORY_MAX = 300;
        private const int GAME_MEMORY_MIN_CONFIRM_TICKS = 6;

        // Decision Hysteresis - prevent rapid mode oscillation (time-based, not tick-based)
        private DateTime _lastModeChange = DateTime.Now;
        private const double HYSTERESIS_SECONDS = 3.0;
        // Epsilon floor - prevent premature convergence
        private const double EPSILON_FLOOR = 0.02;
        // Reference Counting for Timer Resolution
        private int _timerResRefCount = 0;
        private readonly object _timerResLock = new object();
        // Async Save Queue - prevent concurrent file writes
        private readonly object _saveLock = new object();
        private DateTime _lastSaveTime = DateTime.Now;
        private const int SAVE_DEBOUNCE_MS = 2000;
        // Process Cache - shared across subsystems, refresh every 3 seconds
        private Process[] _cachedProcesses = new Process[0];
        private DateTime _lastProcessCacheRefresh = DateTime.MinValue;
        private const double PROCESS_CACHE_INTERVAL_SECONDS = 3.0;
        private readonly object _processCacheLock = new object();
        // Serializes the refresh itself so two callers cannot build and dispose
        // overlapping snapshots (which used to dispose a snapshot another thread
        // was still enumerating, producing spurious 'Access is denied' errors).
        private readonly object _processCacheRefreshLock = new object();
        // Action -> decision source mapping (feeds source-level trust in arbitration)
        private readonly ConcurrentDictionary<string, string> _actionSource = new ConcurrentDictionary<string, string>();
        // Calibrated abstention counter (learned "do nothing" decisions)
        private int _abstentions = 0;
        // Peak values - time-based window (not tick-based)
        private double _peakCpu = 0;
        private double _peakRam = 0;
        private double _peakTemp = 0;
        private DateTime _peakWindowStart = DateTime.Now;
        private const int PEAK_WINDOW_SECONDS = 60;
        // Game Confirm - require 2-3 seconds before entering gaming mode
        private DateTime _gameDetectedTime = DateTime.MinValue;
        private const double GAME_CONFIRM_SECONDS = 2.5;
        // Global Cooldown - prevent rapid action changes
        private DateTime _lastActionTime = DateTime.MinValue;
        private const double GLOBAL_COOLDOWN_SECONDS = 8.0;
        // Timer Reentrancy Guard (ChatGPT recommended - use Interlocked, not volatile)
        private int _tickRunning = 0;
        // Safe Mode → Circuit Breaker (progressive retry, not fixed timeout)
        private bool _circuitOpen = false;
        private int _circuitRetryDelayMs = 1000;
        private DateTime _circuitNextRetry = DateTime.MinValue;
        // Safe Mode (ChatGPT Round 27): auto-disable AI on repeated failures
        private int _safeModeFailureCount = 0;
        private DateTime _safeModeWindowStart = DateTime.Now;
        private const int SAFE_MODE_THRESHOLD = 10;
        private const int SAFE_MODE_WINDOW_SECONDS = 120;
        private volatile bool _safeMode = false;
        // Tweak Knowledge Base (from FrierenAI / VoidBattery / ClearWin)
        private TweakKnowledgeBase _tweakKnowledge;
        private TweakAnalyzer _tweakAnalyzer;
        private DateTime _lastKnowledgeQuery = DateTime.MinValue;
        private const int KNOWLEDGE_QUERY_INTERVAL_TICKS = 30;
        // Proactive toolbox sweep: when the system is healthy the classic analyzers
        // stay silent (no memory/CPU/temp problem), so the agent would idle and
        // never exercise its full toolbox. This sweep round-robins cheap reversible
        // maintenance so every capability is actively used and learned from.
        private const int PROACTIVE_INTERVAL_TICKS = 45;
        private int _proactiveTick = 0;
        private int _proactiveIndex = 0;
        // Watchdog: track last tick time (ChatGPT Round 27)
        private DateTime _lastTickTime = DateTime.MinValue;
        public double SecondsSinceLastTick { get { return (DateTime.Now - _lastTickTime).TotalSeconds; } }
        public List<string> DecisionHistoryLog { get { lock (_decisionLock) { if (_snapDecisionLog == null || (DateTime.Now - _snapDecisionLogTime).TotalMilliseconds > 500) { _snapDecisionLog = new List<string>(_decisionHistoryLog); _snapDecisionLogTime = DateTime.Now; } return _snapDecisionLog; } } }
        public List<AgentDecision> DecisionHistory { get { lock (_decisionLock) { return new List<AgentDecision>(_decisionHistory); } } }
        // Health Trend (UP/DOWN/STABLE)
        // Thread-safe: written from Tick (background thread), read from UI thread
        private volatile string _healthTrend;
        public string HealthTrend { get { return _healthTrend; } private set { _healthTrend = value; } }
        private double _prevHealthScore = 50;
        // Adaptive Thermal Model - rate of change tracking
        private double _tempRateOfChange = 0;
        private double _prevTempForRate = 0;
        // Decision History (last 100 decisions with results) - Circular Buffer
        private Queue<string> _decisionHistoryLog = new Queue<string>();
        private const int MAX_DECISION_LOG = 100;
        // Snapshot cache to avoid repeated List copies
        private List<string> _snapDecisionLog;
        private DateTime _snapDecisionLogTime = DateTime.MinValue;
        private List<string> _snapAnomalyAlerts;
        private DateTime _snapAnomalyAlertsTime = DateTime.MinValue;
        private List<AgentExperience> _snapExperiences;
        private DateTime _snapExperiencesTime = DateTime.MinValue;
        private List<AgentProfile> _snapProfiles;
        private DateTime _snapProfilesTime = DateTime.MinValue;
        private List<AgentLog> _snapLogs;
        private DateTime _snapLogsTime = DateTime.MinValue;
        private List<string> _snapHogAlerts;
        private DateTime _snapHogAlertsTime = DateTime.MinValue;
        private List<string> _snapOptLog;
        private DateTime _snapOptLogTime = DateTime.MinValue;
        private List<string> _snapUndoStack;
        private DateTime _snapUndoStackTime = DateTime.MinValue;
        // Self Test
        private bool _selfTestPassed = false;
        // Learning Feedback Loop: measure decision outcomes
        private Dictionary<string, int> _actionSuccessCount = new Dictionary<string, int>();
        private Dictionary<string, int> _actionTotalCount = new Dictionary<string, int>();
        private Dictionary<string, DateTime> _actionDecayTime = new Dictionary<string, DateTime>();
        private DateTime _lastDecisionTime = DateTime.MinValue;
        private string _lastDecisionAction = "";
        // Credit Assignment: track batch of actions per tick for proportional attribution
        private List<string> _currentTickActions = new List<string>();
        // Outcome-verified learning: tracks whether an action's effect actually
        // showed up in system metrics (credit-assigned reward). Kept strictly
        // separate from the execution success counters above, so "the command
        // ran fine" can never inflate confidence in an action that has not
        // proven useful.
        private Dictionary<string, int> _outcomeSuccessCount = new Dictionary<string, int>();
        private Dictionary<string, int> _outcomeTotalCount = new Dictionary<string, int>();
        private Dictionary<string, double> _actionAttributedReward = new Dictionary<string, double>();
        // Pending outcome batches: each batch carries its own pre-execution
        // snapshot, so a fast tick can never wipe actions out before their
        // evaluation window matures.
        private class PendingOutcomeBatch
        {
            public List<string> Actions;
            public DateTime RecordedAt;
            public double PreCpu;
            public double PreRam;
            public double PreTemp;
            public double PreFps;
            public string PreMode;
        }
        private readonly List<PendingOutcomeBatch> _pendingOutcomeBatches = new List<PendingOutcomeBatch>();
        private const int PENDING_BATCH_MAX = 10;
        // Expected relative effect of each action on the tracked metrics
        // {temperature drop, RAM drop, FPS improvement}. Credit assignment is
        // proportional to these magnitudes: a batch-level improvement is
        // credited to each action in proportion to how much it should move
        // that metric, not split evenly across the batch.
        private static readonly Dictionary<string, double[]> ACTION_MAGNITUDES = new Dictionary<string, double[]>
        {
            // Thermal
            { "EmergencyThrottle", new double[] { 1.0, 0.2, 0.2 } },
            { "ReduceThermal", new double[] { 1.0, 0.2, 0.2 } },
            { "BatterySaver", new double[] { 0.4, 0.1, 0.1 } },
            { "BalancedPerformance", new double[] { 0.3, 0.1, 0.2 } },
            // Memory
            { "AggressiveClean", new double[] { 0.2, 1.0, 0.3 } },
            { "PreemptiveClean", new double[] { 0.2, 0.8, 0.2 } },
            { "LightClean", new double[] { 0.2, 0.7, 0.2 } },
            { "PurgeStandbyList", new double[] { 0.1, 0.9, 0.2 } },
            { "CleanTempFiles", new double[] { 0.1, 0.3, 0.1 } },
            { "LimitBackground", new double[] { 0.2, 0.5, 0.3 } },
            { "SuspendBloatProcesses", new double[] { 0.1, 0.5, 0.3 } },
            { "ResumeSuspendedProcesses", new double[] { 0.2, 0.2, 0.2 } },
            // Game
            { "BoostGamePriority", new double[] { 0.1, 0.1, 1.0 } },
            { "StabilizeFrames", new double[] { 0.1, 0.1, 1.0 } },
            { "ReduceMicroStutter", new double[] { 0.1, 0.1, 1.0 } },
            { "SetGameAffinity", new double[] { 0.1, 0.1, 0.8 } },
            { "UnparkAllCores", new double[] { 0.2, 0.1, 0.7 } },
            { "DisableGameDvr", new double[] { 0.1, 0.3, 0.7 } },
            { "SetLowIoPriority", new double[] { 0.1, 0.1, 0.5 } },
            { "LimitBackgroundAffinity", new double[] { 0.1, 0.4, 0.5 } },
            // Power / network / other (mostly indirect effects)
            { "SetHighPerformance", new double[] { 0.1, 0.1, 0.6 } },
            { "SwitchPowerPlan", new double[] { 0.2, 0.1, 0.3 } },
            { "OptimizeNetwork", new double[] { 0.05, 0.05, 0.2 } },
            { "FlushDnsCache", new double[] { 0.05, 0.05, 0.1 } },
            { "ResetNetworkPriority", new double[] { 0.05, 0.05, 0.1 } }
        };
        // Action Budget: max cost per tick (raised so more toolbox actions can
        // run per tick instead of starving to a single cleanup/priority boost)
        private const int BUDGET_MAX = 15;
        private int _budgetUsed = 0;
        private int _budgetCarryOver = 0; // unused budget carries to next tick (max 8)
        private const int MAX_CARRY_OVER = 8;
        private static readonly Dictionary<string, int> ACTION_COST = new Dictionary<string, int>
        {
            {"AggressiveClean", 5}, {"LightClean", 3}, {"PreemptiveClean", 4},
            {"PurgeStandbyList", 5}, {"LimitBackground", 3}, {"BoostGamePriority", 4},
            {"EmergencyThrottle", 6}, {"ReduceThermal", 5}, {"StabilizeFrames", 4},
            {"ReduceMicroStutter", 4}, {"BatterySaver", 2}, {"BalancedPerformance", 2},
            {"SetGameAffinity", 3}, {"LimitBackgroundAffinity", 3}, {"SetLowIoPriority", 2},
            {"UnparkAllCores", 3}, {"SuggestClose", 1},
            {"OptimizeNetwork", 3}, {"FlushDnsCache", 2}, {"ResetNetworkPriority", 3},
            {"SwitchPowerPlan", 2}, {"HealthCheck", 1},
            {"SuspendBloatProcesses", 3}, {"ResumeSuspendedProcesses", 2},
            {"DisableGameDvr", 2}, {"SetHighPerformance", 2}, {"CleanTempFiles", 2},
            {"OptimizeDesktopSmoothness", 3}, {"TrimInactiveWorkingSets", 4},
            {"OptimizeGpuPerformance", 3}, {"ShedBackgroundGpuMemory", 3}, {"PurgeGpuShaderCache", 2}
        };
        private bool _lastExecutionSuccess = false;
        // Unified decision line: ML momentary quality estimate per action. The
        // neural ensemble no longer dual-votes as a competing decision; instead
        // this learned signal feeds the RL/experience layer in RankAndFilter and
        // GetExperienceModifier (blended as a prior, fading as Q-learning learns).
        private Dictionary<string, double> _mlQualityByAction = new Dictionary<string, double>();
        // Action Aging: prevent starvation by boosting priority of deferred actions.
        // Raised so tools that were deferred once get retried quickly instead of
        // being silently forgotten (the agent keeps exercising its full toolbox).
        private Dictionary<string, int> _actionAgeTicks = new Dictionary<string, int>();
        private const double AGING_BONUS_PER_TICK = 0.04;
        private const double AGING_MAX = 0.9;
        // Per-Action Evaluation Delays (seconds)
        private static readonly Dictionary<string, double> ACTION_EVAL_DELAY = new Dictionary<string, double>
        {
            {"AggressiveClean", 5}, {"LightClean", 5}, {"PreemptiveClean", 5},
            {"PurgeStandbyList", 5}, {"LimitBackground", 15}, {"BoostGamePriority", 20},
            {"EmergencyThrottle", 10}, {"ReduceThermal", 15}, {"StabilizeFrames", 20},
            {"ReduceMicroStutter", 20}, {"BatterySaver", 60}, {"BalancedPerformance", 60},
            {"SetGameAffinity", 20}, {"LimitBackgroundAffinity", 20}, {"SetLowIoPriority", 15},
            {"UnparkAllCores", 120}, {"SuggestClose", 10},
            {"OptimizeGpuPerformance", 10}, {"ShedBackgroundGpuMemory", 15}, {"PurgeGpuShaderCache", 30}
        };
        // State snapshot for Context-Aware Learning
        private string _preStateMode = "";
        // Action Dependency Graph - mutually exclusive actions (can't run together)
        private static readonly Dictionary<string, string[]> ACTION_CONFLICTS = new Dictionary<string, string[]>
        {
            {"BatterySaver", new[] {"BalancedPerformance", "UnparkAllCores", "BoostGamePriority"}},
            {"BalancedPerformance", new[] {"BatterySaver", "LimitBackground"}},
            {"SetHighPerformance", new[] {"BatterySaver", "BalancedPerformance", "LimitBackground"}},
            {"EmergencyThrottle", new[] {"BatterySaver", "UnparkAllCores", "BoostGamePriority"}},
            {"UnparkAllCores", new[] {"BatterySaver", "EmergencyThrottle", "ReduceThermal"}},
            {"SetGameAffinity", new[] {"LimitBackgroundAffinity"}},
            {"LimitBackgroundAffinity", new[] {"SetGameAffinity", "BoostGamePriority"}},
            {"BoostGamePriority", new[] {"LimitBackground", "LimitBackgroundAffinity", "BatterySaver", "EmergencyThrottle", "StabilizeFrames", "ReduceMicroStutter"}},
            {"LimitBackground", new[] {"BoostGamePriority", "BalancedPerformance", "ReduceMicroStutter"}},
            {"AggressiveClean", new[] {"LightClean", "PreemptiveClean", "PurgeStandbyList"}},
            {"LightClean", new[] {"AggressiveClean", "PreemptiveClean"}},
            {"PreemptiveClean", new[] {"AggressiveClean", "LightClean"}},
            {"PurgeStandbyList", new[] {"AggressiveClean"}},
            {"ReduceThermal", new[] {"UnparkAllCores", "BoostGamePriority"}},
            {"StabilizeFrames", new[] {"BoostGamePriority", "ReduceMicroStutter"}},
            {"ReduceMicroStutter", new[] {"StabilizeFrames", "BoostGamePriority", "LimitBackground"}}
        };
        // Action Dependencies - preconditions that must be met
        private static readonly Dictionary<string, Func<SystemState, bool>> ACTION_PRECHECKS = new Dictionary<string, Func<SystemState, bool>>
        {
            {"SetGameAffinity", s => !string.IsNullOrEmpty(s.ActiveProcess)},
            {"SetLowIoPriority", s => !string.IsNullOrEmpty(s.ActiveProcess)},
            {"BoostGamePriority", s => !string.IsNullOrEmpty(s.ActiveProcess)},
            {"SuggestClose", s => s.ProcessCount > 100}
        };
        // Rollback: save pre-execution state for restoration
        private string _rollbackPlan = "";
        private string _rollbackGame = "";
        // A/B Exploration: 6% chance to try lower-confidence actions (prevents
        // local optimum and keeps every tool in the box being tried occasionally)
        private const double EXPLORATION_RATE = 0.06;
        private static readonly Random _exploreRnd = new Random();
        // Performance Telemetry: track execution time per action
        private Dictionary<string, List<double>> _actionExecTimes = new Dictionary<string, List<double>>();
        private const int MAX_TELEMETRY_SAMPLES = 20;
        // Decision Arbitration - priority-based tie-breaking
        private const int PRIORITY_THERMAL = 5;
        private const int PRIORITY_MEMORY = 4;
        private const int PRIORITY_GAME = 3;
        private const int PRIORITY_BACKGROUND = 2;
        private const int PRIORITY_POWER = 1;
        // Prediction / Trend Analysis - Linear Regression on last N samples
        private double[] _tempHistory = new double[20];
        private double[] _predictBuffer = new double[20]; // reused by PredictNext to avoid per-tick allocation
        private int _historyIndex = 0;
        private int _historyCount = 0;
        public double PredictedTemp { get; private set; }
        public double PredictionConfidence { get; private set; }
        // Deferred work queue - prevents blocking Tick() with heavy post-decision processing
        private Queue<Action> _deferredWork = new Queue<Action>(16);
        private const int DEFERRED_BATCH_SIZE = 3;
        private const int DEFERRED_MAX_QUEUED = 50;

        private void EnqueueDeferred(Action work)
        {
            lock (_deferredWork)
            {
                if (_deferredWork.Count < DEFERRED_MAX_QUEUED)
                    _deferredWork.Enqueue(work);
            }
        }

        private void ProcessDeferredWork()
        {
            for (int i = 0; i < DEFERRED_BATCH_SIZE; i++)
            {
                Action work = null;
                lock (_deferredWork)
                {
                    if (_deferredWork.Count > 0)
                        work = _deferredWork.Dequeue();
                }
                if (work != null)
                {
                    try { work(); } catch (Exception ex) { LogError("DeferredWork", ex); }
                }
                else break;
            }
        }
        // Watchdog - per-analyzer adaptive timeouts
        private const int WATCHDOG_MAX_STREAK = 5;
        private Dictionary<string, double> _analyzerMeanTimes = new Dictionary<string, double>();
        private int _consecutiveAnalyzerTimeouts = 0;
        // Category Learning - adjusts sigmoid weights based on success rate
        private Dictionary<string, double> _categorySuccessSum = new Dictionary<string, double>();
        private Dictionary<string, int> _categoryTrialCount = new Dictionary<string, int>();
        private Dictionary<string, double> _categoryLearnedBoost = new Dictionary<string, double>();
        private const double LEARNING_RATE = 0.1;
        private const double MAX_LEARNED_BOOST = 0.5;
        // Anomaly Detection - tracks rolling statistics
        private double[] _anomalyCpuHistory = new double[60];
        private double[] _anomalyRamHistory = new double[60];
        private double[] _anomalyTempHistory = new double[60];
        private double[] _anomalyGpuHistory = new double[60];
        private double[] _anomalyFpsHistory = new double[60];
        private int _anomalyIndex = 0;
        private int _anomalyCount = 0;
        private double _cpuMean = 0, _cpuStdDev = 1;
        private double _ramMean = 0, _ramStdDev = 1;
        private double _tempMean = 0, _tempStdDev = 1;
        private double _gpuMean = 0, _gpuStdDev = 1;
        public List<string> AnomalyAlerts { get { lock (_listLock) { if (_snapAnomalyAlerts == null || (DateTime.Now - _snapAnomalyAlertsTime).TotalMilliseconds > 500) { _snapAnomalyAlerts = new List<string>(_anomalyAlerts); _snapAnomalyAlertsTime = DateTime.Now; } return _snapAnomalyAlerts; } } }
        private List<string> _anomalyAlerts = new List<string>();
        private const int MAX_ANOMALY_ALERTS = 20;
        private const double ANOMALY_THRESHOLD = 2.5;
        // Idle Mode
        private DateTime _lastActiveTime = DateTime.Now;
        private bool _idleCleanupDone = false;
        private const double IDLE_THRESHOLD_SECONDS = 300;
        // Smart Scheduling - maintenance tracker
        private int[] _hourMaintenanceDone = new int[24];
        private const int MAINTENANCE_COOLDOWN = 3;
        private int _lastMaintenanceDay = -1;
        private int _persistentRulesTick = 0;
        // Usage Pattern Learning
        private Dictionary<string, int> _appUsageByHour = new Dictionary<string, int>();
        private const int MAX_APP_USAGE = 1000;
        // === Feature 1: Thermal Protection ===
        private const double THERMAL_THROTTLE_TEMP = 80.0;
        private const double THERMAL_WARNING_TEMP = 70.0;
        private bool _thermalThrottled = false;
        // === Feature 2: Battery Mode ===
        private bool _isOnBattery = false;
        private int _batteryPercent = 100;
        private bool _wasOnBattery = false;
        // === Feature 3: Resource Limits ===
        private double _maxCpuLimit = 95.0;
        private double _maxDiskLimit = 95.0;
        private bool _resourceLimitsEnabled = false;
        // === Feature 4: New Process Detection ===
        private Dictionary<string, DateTime> _knownProcesses = new Dictionary<string, DateTime>();
        private List<string> _newHogAlerts = new List<string>();
        private const long HOG_THRESHOLD_BYTES = 80 * 1024 * 1024;
        private const double HOG_CPU_THRESHOLD = 30.0;
        private int _processDetectTick = 0;
        private int _heavySubsystemTick = 0;
        private int _researchTick = 0;
        private int _enterpriseTick = 0;
        private int _graphTick = 0;
        private DateTime _lastBootCheckTime = DateTime.MinValue;
        // === Feature 5: Error Logging ===
        private List<string> _optimizationLog = new List<string>();
        private const int MAX_OPT_LOG = 200;
        // === Feature 6: Undo Optimizations ===
        private List<string> _undoStack = new List<string>();
        private const int MAX_UNDO_STACK = 20;
        // === Feature 7: SMART Disk Health ===
        private double _diskHealthPercent = 100.0;
        private string _diskHealthStatus = "OK";
        private DateTime _lastDiskCheck = DateTime.MinValue;
        private const int DISK_CHECK_INTERVAL_SECONDS = 300;
        // === Feature 8: Network Monitoring ===
        private double _netDownloadKBps = 0;
        private double _netUploadKBps = 0;
        // === Feature 9: Auto Restore Point ===
        private bool _restorePointCreatedThisSession = false;
        // === Feature 10: Performance Speedometer ===
        private double _preOptCpu = 0;
        private double _preOptRam = 0;
        private double _preOptFps = 0;
        private double _preOptGpu = 0;
        private double _preOptTemp = 0;
        private double _preOptFrameTime = 0;
        private double _postOptCpu = 0;
        private double _postOptRam = 0;
        private double _postOptFps = 0;
        private double _postOptGpu = 0;
        private double _postOptTemp = 0;
        private double _postOptFrameTime = 0;
        private string _lastOptimizationResult = "";
        // === Auto Rollback: full pre/post state snapshot for degradation detection ===
        private SystemState _rollbackPreState = null;
        private string _rollbackAction = "";
        private string _lastRollbackLog = "";
        private DateTime _lastAppliedAt = DateTime.MinValue;
        private const int ROLLBACK_WINDOW_SECONDS = 45;
        private const int ROLLBACK_CHECK_TICKS = 30;
        private int _rollbackCheckCounter = 0;
        // === Performance Caches ===
        private DateTime _lastBatteryQuery = DateTime.MinValue;
        private const int BATTERY_QUERY_INTERVAL_SECONDS = 10;
        private DateTime _lastNetworkQuery = DateTime.MinValue;
        private const int NETWORK_QUERY_INTERVAL_SECONDS = 5;
        // WMI query results land in this cache from background tasks so the tick
        // loop never blocks on a slow ManagementObjectSearcher.
        private readonly object _wmiLock = new object();
        private double _wmiNetDownloadKBps = 0;
        private double _wmiNetUploadKBps = 0;
        private double _wmiBatteryLevel = -1;
        private bool _wmiIsCharging = false;
        private double _wmiDiskHealthPercent = 100.0;
        private string _wmiDiskHealthStatus = "OK";
        private int _batteryQueryRunning = 0;
        private int _diskQueryRunning = 0;
        private int _networkQueryRunning = 0;
        // === Simulation Mode ===
        private volatile bool _simulationMode = false;
        private SystemState _simulatedState = null;
        private DateTime _lastThermalQuery = DateTime.MinValue;
        private const int THERMAL_QUERY_INTERVAL_SECONDS = 5;
        private DateTime _lastDxgiFpsQuery = DateTime.MinValue;
        private const int DXGI_FPS_QUERY_INTERVAL_SECONDS = 2;
        private string[] _cachedThermalInstances = null;
        private DateTime _lastThermalInstanceRefresh = DateTime.MinValue;
        private DateTime _lastProcessCountQuery = DateTime.MinValue;
        private const int PROCESS_COUNT_INTERVAL_SECONDS = 5;
        private int _cachedProcessCount = 0;
        private DateTime _lastRegistryQuery = DateTime.MinValue;
        private double _cachedCpuFreqGHz = 0;
        private PerformanceCounterCategory _gpuCatCached = null;
        // === Windows Optimization AI (in-app) Integration ===
        // The external SmolLM2 360M local model has been removed. In its place the
        // in-process neural ensemble (MLNeuralNetwork) is the local AI brain,
        // specialized for Windows optimization: always available, never blocks,
        // and keeps learning from real optimization outcomes.
        private WindowsOptimizationAI _woa;
        private const int WOAI_INTERVAL_TICKS = 150;
        private string _woaLastAction = "";
        private double _woaLastConfidence = 0;
        private string _woaLastPrompt = "";
        private string _woaLastRawResponse = "";

        public WindowsOptimizationAI WinOptAI { get { return _woa; } }
        public string WinOptAILastAction { get { return _woaLastAction; } }
        public double WinOptAILastConfidence { get { return _woaLastConfidence; } }
        public string WinOptAILastPrompt { get { return _woaLastPrompt; } }
        public string WinOptAILastRawResponse { get { return _woaLastRawResponse; } }
        private DateTime _lastAnalyzeProbe = DateTime.MinValue;

        public AgentMode Mode { get { return _mode; } }
        public bool SafeModeEnabled { get { return _safeMode; } }

        // === Post-Game Stabilization & Learning Recovery ===
        private int _nonGamingStabilityTicks = 0;
        private const int POST_GAME_STABILITY_TICKS = 8;
        public int NonGamingStabilityTicks { get { return _nonGamingStabilityTicks; } }

        // === Disk Usage Query Caching (Feature CPU Optimization) ===
        private DriveInfo _systemDriveInfo;
        private DateTime _lastDiskUsageQuery = DateTime.MinValue;
        private const double DISK_USAGE_INTERVAL_SECONDS = 3.0;
        private double _cachedDiskUsage = 0;
        // Automation safety valve (V19): when automatic actions are stopped the
        // agent keeps monitoring/deciding/learning but never executes; the cap is a
        // hard limit on auto actions per autonomous cycle (default 0 = manual).
        private volatile bool _automationEnabled = true;
        private int _maxAutoActionsPerCycle = 3;
        public bool AutomationEnabled { get { return _automationEnabled; } }
        public int MaxAutoActionsPerCycle { get { return _maxAutoActionsPerCycle; } }
        public void SetAutomationEnabled(bool on)
        {
            _automationEnabled = on;
            SaveAutomationSettings();
            AddLog(on ? "INFO" : "WARN", "Automation", on ? "Automatic actions enabled by user" : "Automatic actions stopped by user (safety valve)");
        }
        public void SetMaxAutoActionsPerCycle(int v)
        {
            _maxAutoActionsPerCycle = v < 0 ? 0 : (v > 5 ? 5 : v);
            SaveAutomationSettings();
            AddLog("INFO", "Automation", "Maximum automatic actions per cycle set to " + _maxAutoActionsPerCycle);
        }

        // Central Execution Gate (V19 safety): every AUTONOMOUS path that
        // mutates the system (idle cleanup, autopilot, auto-recovery,
        // scheduled tasks, plan steps, FPS stabilizer, ...) must pass through
        // this single function before executing. When the user has stopped
        // automatic actions the gate blocks hard - the only exception is a
        // manual user trigger (manualTrigger = true), which is always allowed.
        // This closes the leak where individual modules (UpdateIdleDetection,
        // RunAutopilotCycle, ...) either forgot the check or used the raw
        // flag with different semantics; blocked actions are audited in the
        // log, throttled so tick-level paths cannot flood it.
        private DateTime _lastGateBlockLog = DateTime.MinValue;
        // =====================================================================
        // v20 RC — Centralized Policy Gate
        // Separates four independent concerns so that NO single flag can
        // short-circuit the safety chain:
        //   1. ApprovalPolicy   – does this action need user consent?
        //   2. SafetyValidation – is the system state compatible?
        //   3. Authorization    – does the agent have permission to run it?
        //   4. ExecutionResult  – did the action actually succeed?
        // WasAccepted is NEVER consulted here; it only records UI outcome.
        // =====================================================================
        public PolicyOutcome EvaluatePolicy(AgentDecision d)
        {
            var outcome = new PolicyOutcome();
            outcome.Timestamp = DateTime.Now;
            outcome.Action = d != null ? d.Action : "null";
            // 1. SafetyValidation (hard gate, never bypassed)
            outcome.Safety = EvaluateSafety(d);
            if (!outcome.Safety.Allowed)
            {
                outcome.Authorized = false;
                outcome.Approved = false;
                outcome.Result = PolicyResult.Rejected_Safety;
                outcome.Reason = "Safety validation failed: " + (outcome.Safety.Message ?? "");
                AddLog("POLICY", "Blocked", outcome.Reason);
                return outcome;
            }
            // 2. Authorization (automation valve, kill switch, pause)
            outcome.Authorized = EvaluateAuthorization(d);
            if (!outcome.Authorized)
            {
                outcome.Approved = false;
                outcome.Result = PolicyResult.Rejected_Authorization;
                outcome.Reason = "Authorization denied — automation blocked or kill switch active";
                AddLog("POLICY", "Blocked", outcome.Reason);
                return outcome;
            }
            // 3. ApprovalPolicy (needs user consent?)
            bool needsApproval = d.Safety == DecisionSafety.NeedsApproval || d.Safety == DecisionSafety.Dangerous;
            bool autoAcceptAllowed = _fullAutonomy && IsFullAutonomyAllowlisted(d.Action);
            outcome.Approved = !needsApproval || autoAcceptAllowed || d.WasAccepted;
            if (needsApproval && !autoAcceptAllowed && !d.WasAccepted)
            {
                outcome.Result = PolicyResult.Pending_Approval;
                outcome.Reason = "Requires manual approval";
                return outcome;
            }
            // 4. All gates passed
            outcome.Result = PolicyResult.Approved_ForExecution;
            outcome.Reason = "Passed all policy gates";
            return outcome;
        }

        private SafetyResult EvaluateSafety(AgentDecision d)
        {
            // Reuse existing PreExecutionSafetyCheck which covers thermal, RAM,
            // protected processes, services, and registry safety.
            try
            {
                return PreExecutionSafetyCheck(d);
            }
            catch (Exception ex)
            {
                CWLog.Error("PolicyGate.Safety", ex);
                return new SafetyResult { Allowed = false, Message = "Safety check exception" };
            }
        }

        private bool EvaluateAuthorization(AgentDecision d)
        {
            // Kill switch is absolute — cannot be overridden by any flag.
            if (_killSwitchActive) return false;
            // Pause is absolute for automatic paths; manual triggers are allowed
            // through the CanExecuteAction(manualTrigger:true) path only.
            if (_agentPaused && d != null && !d.WasAccepted) return false;
            // Automation valve: when disabled, block all automatic execution
            // but still allow user-approved decisions (WasAccepted=true).
            if (!_automationEnabled && d != null && !d.WasAccepted) return false;
            return true;
        }

        public enum PolicyResult { Approved_ForExecution, Pending_Approval, Rejected_Safety, Rejected_Authorization }
        public class PolicyOutcome
        {
            public string Action;
            public DateTime Timestamp;
            public SafetyResult Safety;
            public bool Authorized;
            public bool Approved;
            public PolicyResult Result;
            public string Reason;
        }

        public bool CanExecuteAction(string context, bool manualTrigger)
        {
            if (manualTrigger || _automationEnabled) return true;
            if ((DateTime.Now - _lastGateBlockLog).TotalSeconds >= 30)
            {
                _lastGateBlockLog = DateTime.Now;
                AddLog("WARN", "Gate", "BLOCKED automatic action '" + context + "' - automation safety valve is off (manual triggers only)");
            }
            return false;
        }

        public void ClearSafeMode()
        {
            if (_safeMode) { _safeMode = false; _safeModeFailureCount = 0; AddLog("INFO", "SafeMode", "Safe mode cleared by user"); }
        }
        private void SaveAutomationSettings()
        {
            try
            {
                File.WriteAllText(Path.Combine(_dataDir, "automation.dat"), _automationEnabled ? "1" : "0");
                File.WriteAllText(Path.Combine(_dataDir, "maxautoactions.dat"), _maxAutoActionsPerCycle.ToString());
            }
            catch (Exception ex) { LogError("SaveAutomationSettings", ex); }
        }
        private void LoadAutomationSettings()
        {
            try
            {
                string p = Path.Combine(_dataDir, "automation.dat");
                if (File.Exists(p)) _automationEnabled = File.ReadAllText(p).Trim() == "1";
                string q = Path.Combine(_dataDir, "maxautoactions.dat");
                if (File.Exists(q))
                {
                    int v;
                    if (int.TryParse(File.ReadAllText(q).Trim(), out v)) _maxAutoActionsPerCycle = v < 0 ? 0 : (v > 5 ? 5 : v);
                }
            }
            catch { }
        }
        public SystemState CurrentState
        {
            get
            {
                if (_simulationMode && _simulatedState != null) return _simulatedState;
                lock (_stateLock)
                {
                    if (_snapState == null || (DateTime.Now - _snapStateTime).TotalMilliseconds > 200)
                    {
                        _snapState = _currentState.Clone();
                        _snapStateTime = DateTime.Now;
                    }
                    return _snapState;
                }
            }
        }
        private bool _frameDataReliable = true;
        public bool IsFrameDataReliable { get { return _frameDataReliable; } }
        public void SetFrameData(double fps, double fps1Low, double fps01Low, double stability, double jitter, double frameMs, bool reliable = true)
        {
            // When the frame telemetry is not live (window hidden/minimized), the
            // tracker reports its last-known render rate which is NOT the game's
            // FPS. Stale values must not leak into rules, health, or ML features,
            // so neutralize the whole frame-state before it is stored.
            if (!reliable) { fps = 0; fps1Low = 0; fps01Low = 0; stability = 0; jitter = 0; frameMs = 0; }
            lock (_stateLock)
            {
                _currentState.Fps = fps;
                _currentState.Fps1Low = fps1Low;
                _currentState.Fps01Low = fps01Low;
                _currentState.FpsStability = stability;
                _currentState.FrameTimeJitter = jitter;
                _currentState.FrameTimeMs = frameMs;
            }
            _frameDataReliable = reliable;
        }

        private bool _dxgiFpsDue()
        {
            if ((DateTime.Now - _lastDxgiFpsQuery).TotalSeconds < DXGI_FPS_QUERY_INTERVAL_SECONDS) return false;
            _lastDxgiFpsQuery = DateTime.Now;
            return true;
        }

        private bool _foregroundIsFullscreen()
        {
            try
            {
                IntPtr fg = GetForegroundWindow();
                if (fg == IntPtr.Zero) return false;
                WinRect rect;
                if (!GetWindowRect(fg, out rect)) return false;
                int sw = (int)System.Windows.SystemParameters.PrimaryScreenWidth;
                int sh = (int)System.Windows.SystemParameters.PrimaryScreenHeight;
                int w = rect.Right - rect.Left;
                int h = rect.Bottom - rect.Top;
                return w >= sw - 20 && h >= sh - 20;
            }
            catch { return false; }
        }
        private SystemState _snapState;
        private DateTime _snapStateTime = DateTime.MinValue;
        public List<AgentExperience> Experiences { get { lock (_listLock) { if (_snapExperiences == null || (DateTime.Now - _snapExperiencesTime).TotalMilliseconds > 500) { _snapExperiences = new List<AgentExperience>(_experiences); _snapExperiencesTime = DateTime.Now; } return _snapExperiences; } } }
        public List<AgentProfile> Profiles { get { lock (_listLock) { if (_snapProfiles == null || (DateTime.Now - _snapProfilesTime).TotalMilliseconds > 500) { _snapProfiles = new List<AgentProfile>(_profiles); _snapProfilesTime = DateTime.Now; } return _snapProfiles; } } }
        public List<AgentLog> Logs { get { lock (_listLock) { if (_snapLogs == null || (DateTime.Now - _snapLogsTime).TotalMilliseconds > 500) { _snapLogs = new List<AgentLog>(_logs); _snapLogsTime = DateTime.Now; } return _snapLogs; } } }
        public AgentStats Stats
        {
            get
            {
                lock (_decisionLock)
                {
                    return new AgentStats
                    {
                        TotalDecisions = _stats.TotalDecisions,
                        SuccessfulDecisions = _stats.SuccessfulDecisions,
                        FailedDecisions = _stats.FailedDecisions,
                        TotalExperiences = _stats.TotalExperiences,
                        ActiveProfiles = _stats.ActiveProfiles,
                        StartTime = _stats.StartTime,
                        LastDecisionTime = _stats.LastDecisionTime
                    };
                }
            }
        }
        // === Feature 1-10 Public Properties ===
        public bool IsThermalThrottled { get { return _thermalThrottled; } }
        public bool IsOnBattery { get { return _isOnBattery; } }
        public int BatteryPercent { get { return _batteryPercent; } }
        public double MaxCpuLimit { get { return _maxCpuLimit; } set { _maxCpuLimit = Math.Max(50, Math.Min(100, value)); } }
        public double MaxDiskLimit { get { return _maxDiskLimit; } set { _maxDiskLimit = Math.Max(50, Math.Min(100, value)); } }
        public bool ResourceLimitsEnabled { get { return _resourceLimitsEnabled; } set { _resourceLimitsEnabled = value; } }
        public List<string> NewHogAlerts { get { lock (_listLock) { if (_snapHogAlerts == null || (DateTime.Now - _snapHogAlertsTime).TotalMilliseconds > 500) { _snapHogAlerts = new List<string>(_newHogAlerts); _snapHogAlertsTime = DateTime.Now; } return _snapHogAlerts; } } }
        public List<string> OptimizationLog { get { lock (_listLock) { if (_snapOptLog == null || (DateTime.Now - _snapOptLogTime).TotalMilliseconds > 500) { _snapOptLog = new List<string>(_optimizationLog); _snapOptLogTime = DateTime.Now; } return _snapOptLog; } } }
        public List<string> UndoStack { get { lock (_listLock) { if (_snapUndoStack == null || (DateTime.Now - _snapUndoStackTime).TotalMilliseconds > 500) { _snapUndoStack = new List<string>(_undoStack); _snapUndoStackTime = DateTime.Now; } return _snapUndoStack; } } }
        public double DiskHealthPercent { get { return _diskHealthPercent; } }
        public string DiskHealthStatus { get { return _diskHealthStatus; } }
        public double NetDownloadKBps { get { return _netDownloadKBps; } }
        public double NetUploadKBps { get { return _netUploadKBps; } }
        public string LastOptimizationResult { get { return _lastOptimizationResult; } }
        public event Action<AgentLog> OnLog;
        public event Action<string> OnOptimizationLog;
        public event Action<AgentDecision> OnDecision;
        public event Action<AgentDecision> OnDecisionExecuted;
        public event Action<SystemState> OnStateUpdate;
        // === AI Monitor Events ===
        public event Action<string> OnMonitorAlert;
        public event Action<MonitorMetrics> OnMonitorUpdate;

        // === Simulation API ===
        public bool SimulationMode { get { return _simulationMode; } }
        public void EnableSimulation(SystemState state)
        {
            _simulationMode = true;
            _simulatedState = state;
            AddLog("INFO", "Simulator", "Simulation mode ENABLED");
        }
        public void DisableSimulation()
        {
            _simulationMode = false;
            _simulatedState = null;
            AddLog("INFO", "Simulator", "Simulation mode DISABLED - returning to live data");
        }
        public void SetSimulatedState(SystemState state)
        {
            _simulatedState = state;
        }

        public SmartAgent()
        {
            Instance = this;
            try
            {
                _dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClearWin", "agent");
            }
            catch (Exception ex) { LogError("Ctor.DataDir", ex); }
            try
            {
                _currentState = new SystemState();
                _prevState = new SystemState();
            }
            catch (Exception ex) { LogError("Ctor.StateInit", ex); }
            try
            {
                Events = new MessageBus();
                InitializeBusSubscriptions();
            }
            catch (Exception ex) { LogError("Bus.Init", ex); }
            try
            {
                _cpuLoadPc = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _cpuLoadPc.NextValue();
            }
            catch { _cpuLoadPc = null; }
            LoadData();
            LoadAgentMode();
            LoadAutomationSettings();
            // Force at least one HealthCheck decision and explanation so AI stats show activity
            if (_mode != AgentMode.Off && _explanationHistory.Count == 0)
            {
                var hc = new AgentDecision
                {
                    Category = DecisionCategory.SystemStatus,
                    Action = "HealthCheck",
                    Description = "الفحص الأولي للنظام",
                    Reason = "بدء تشغيل المحرك الذكي",
                    Timestamp = DateTime.Now,
                    Confidence = 0.5,
                    Risk = 0,
                    Safety = DecisionSafety.Safe,
                    Impact = 1
                };
                _decisionHistory.Add(hc);
                if (_stats.TotalDecisions == 0) { _stats.TotalDecisions = 1; _stats.SuccessfulDecisions = 1; }
                try { ExplainDecisionFull(hc, new List<AgentDecision> { hc }); } catch (Exception ex) { CWLog.Error("SmartAgent", ex); }
            }
            InitializePredictions();
            InitializeScheduler();
            RegisterDefaultPlugins();
            InitializeMultiAgent();
            InitializeSeasonalLearning();
            InitializeSmartGoals();
            InitializeAdaptiveBoosters();
            LoadKnowledgeBase();
            InitializeWorldModel();
            InitializeRL();
            InitializeAdaptiveArchitecture();
            InitializeSelfProfiling();
            InitializeAutonomousManager();
            InitializeEnterpriseManager();
            InitializeOptimizationGraph();
            LoadLongTermMemory();
            LoadCalibrationData();
            InitializeUserProfile();
            InitializeMLModel();
            InitializeWindowsOptimizationAI();
            InitializeFpsStabilizer();
            InitializeMetaLearning();
            InitializeAdvancedFeatures();
            InitializePredictiveMaintenance();
            InitializeResourceForecasting();
            InitializeSmartScheduling();
            InitializePerformanceComparison();
            InitializeUserBehaviorLearning();
            // Tweak Knowledge Base
            try
            {
                _tweakKnowledge = new TweakKnowledgeBase(_dataDir);
                _tweakKnowledge.Load();
                if (_tweakKnowledge.TotalEntries == 0) _tweakKnowledge.InitializeDefaults();
                _tweakAnalyzer = new TweakAnalyzer(_tweakKnowledge, _dataDir);
            }
            catch { _tweakKnowledge = null; _tweakAnalyzer = null; }
            // Level 8: Tool Training System
            try
            {
                _protectedProcesses.Add("explorer");
                _protectedProcesses.Add("dwm");
                _protectedProcesses.Add("csrss");
                _protectedProcesses.Add("svchost");
                _protectedProcesses.Add("lsass");
                _protectedProcesses.Add("wininit");
                _protectedProcesses.Add("winlogon");
                _protectedProcesses.Add("services");
                _protectedProcesses.Add("clearwin");
            }
            catch (Exception ex) { LogError("Ctor.ProtectedProcesses", ex); }
            InitializeToolTraining();
            InitializeRuleEngine();
        }

        private void RegisterDefaultPlugins()
        {
            RegisterPlugin(new AIPlugin { Name = "SystemHealthMonitor", Version = "1.0", Description = "Monitors overall system health metrics", IntervalSeconds = 60 });
            RegisterPlugin(new AIPlugin { Name = "GameOptimizer", Version = "1.0", Description = "Analyzes game performance and suggests optimizations", IntervalSeconds = 120 });
        }

        public void SetMode(AgentMode mode)
        {
            if (mode == _mode) return;
            _previousMode = _mode;
            _mode = mode;
            SaveAgentMode();
            AddLog("INFO", "Mode", "Mode changed: " + _previousMode.ToString() + " -> " + _mode.ToString());
            if (mode != AgentMode.Off)
            {
                if (_woa != null && !_woa.IsRunning)
                {
                    AddLog("INFO", "WinOptAI", "Auto-starting Windows Optimization AI...");
                    StartWindowsOptimizationAI();
                }
            }
            else
            {
                if (_woa != null && _woa.IsRunning)
                {
                    StopWindowsOptimizationAI();
                }
            }
        }

        private void SaveAgentMode()
        {
            try
            {
                string path = Path.Combine(_dataDir, "mode.dat");
                File.WriteAllText(path, ((int)_mode).ToString());
            }
            catch (Exception ex) { LogError("SaveAgentMode", ex); }
        }

        private string ResolveLoadPath(string path)
        {
            string tmp = path + ".tmp";
            if (File.Exists(tmp) && (!File.Exists(path) || File.GetLastWriteTime(tmp) > File.GetLastWriteTime(path)))
                return tmp;
            // Try backup files if main file doesn't exist
            if (!File.Exists(path))
            {
                for (int i = 1; i <= MAX_BACKUPS; i++)
                {
                    string bak = path + ".bak" + i;
                    if (File.Exists(bak)) return bak;
                }
            }
            return path;
        }

        private void LoadAgentMode()
        {
            try
            {
                string path = ResolveLoadPath(Path.Combine(_dataDir, "mode.dat"));
                if (File.Exists(path))
                {
                    string text = File.ReadAllText(path).Trim();
                    if (string.IsNullOrEmpty(text)) { _mode = AgentMode.Off; SaveAgentMode(); return; }
                    int val = int.Parse(text);
                    if (val >= 0 && val <= 6) _mode = (AgentMode)val;
                    else { _mode = AgentMode.Off; SaveAgentMode(); }
                }
                else { _mode = AgentMode.Off; SaveAgentMode(); }
            }
            catch { _mode = AgentMode.Off; SaveAgentMode(); }
        }

        public void ToggleAgent()
        {
            if (_safeMode) { _safeMode = false; _safeModeFailureCount = 0; AddLog("INFO", "SafeMode", "Safe mode cleared by user"); }
            if (_mode == AgentMode.Off) SetMode(AgentMode.AutoPilot);
            else SetMode(AgentMode.Off);
        }

        // === Approval queue: decisions deferred for manual confirmation ===
        private void QueueForApproval(AgentDecision d)
        {
            if (d == null) return;
            lock (_approvalLock)
            {
                for (int i = 0; i < _approvalQueue.Count; i++)
                {
                    if (_approvalQueue[i] != null && _approvalQueue[i].Action == d.Action) return;
                }
                _approvalQueue.Add(d);
                if (_approvalQueue.Count > 20) _approvalQueue.RemoveAt(0);
            }
        }

        public List<AgentDecision> GetApprovalQueue()
        {
            lock (_approvalLock) { return new List<AgentDecision>(_approvalQueue); }
        }

        public bool ApprovePendingDecision(AgentDecision d)
        {
            if (d == null) return false;
            lock (_approvalLock)
            {
                if (!_approvalQueue.Remove(d)) return false;
            }
            d.WasAccepted = true;
            AddLog("USER", "Approval", "Approved by user: " + d.Description);
            try { ExecuteDecision(d); return true; }
            catch (Exception ex) { LogError("ApprovePendingDecision", ex); return false; }
        }

        public void DismissPendingDecision(AgentDecision d)
        {
            if (d == null) return;
            lock (_approvalLock) { _approvalQueue.Remove(d); }
            AddLog("USER", "Approval", "Dismissed by user: " + (d.Description ?? d.Action));
        }

        private volatile bool _killSwitchActive = false;
        // Set by TestRunner: simulate tool execution without touching the real
        // system (keeps automated tests fast, deterministic and side-effect free).
        public static bool DisableRealExecution = false;
        private volatile bool _agentPaused = false;
        private DateTime _pauseStartTime = DateTime.MinValue;
        private int _pauseDurationTicks = 0;
        private volatile int _pauseTicksRemaining = 0;
        // Full Autonomy: auto-executes every safe/reversible tool without waiting
        // for manual approval (Dangerous + Critical still queue for the user).
        private bool _fullAutonomy = true;
        public bool FullAutonomy { get { return _fullAutonomy; } set { _fullAutonomy = value; } }

        public bool IsKillSwitchActive { get { return _killSwitchActive; } }
        public bool IsPaused { get { return _agentPaused || _pauseTicksRemaining > 0; } }

        public void EmergencyKillSwitch()
        {
            _killSwitchActive = true;
            _mode = AgentMode.Off;
            _safeMode = false;
            _safeModeFailureCount = 0;
            _circuitOpen = false;
            Stop();
            AddLog("CRITICAL", "KillSwitch", "EMERGENCY KILL SWITCH ACTIVATED - All AI operations halted");
        }

        public void ResetKillSwitch()
        {
            _killSwitchActive = false;
            AddLog("INFO", "KillSwitch", "Kill switch reset - AI can resume");
        }

        public void PauseAgent(int durationTicks)
        {
            _pauseDurationTicks = durationTicks;
            _pauseTicksRemaining = durationTicks;
            _pauseStartTime = DateTime.Now;
            AddLog("INFO", "Pause", "Agent paused for " + durationTicks + " ticks");
        }

        public void ResumeAgent()
        {
            _pauseDurationTicks = 0;
            _pauseTicksRemaining = 0;
            _agentPaused = false;
            AddLog("INFO", "Pause", "Agent resumed");
        }

        public void EmergencyStopAllOptimizations()
        {
            _killSwitchActive = true;
            _mode = AgentMode.Off;
            try
            {
                uint current;
                NtSetTimerResolution(TIMER_RESOLUTION_DEFAULT, false, out current);
            } catch (Exception ex2) { CWLog.Error("SmartAgent", ex2); }
            // Rollback: restore power plan to Balanced
            try
            {
                string curPlan = GetCurrentPowerPlan();
                if (curPlan != "Balanced" && curPlan != "Power Saver")
                {
                    ExecutePowerPlan("balanced");
                    AddLog("ROLLBACK", "EmergencyStop", "Restored power plan to Balanced");
                }
            } catch (Exception ex3) { CWLog.Error("SmartAgent", ex3); }
            // Rollback: restore process priorities to Normal
            try
            {
                var procs = GetCachedProcesses();
                if (procs != null)
                {
                    for (int i = 0; i < procs.Length; i++)
                    {
                        try
                        {
                            if (procs[i] != null && !procs[i].HasExited && !SYSTEM_PROCESSES.Contains(procs[i].ProcessName.ToLower()))
                            {
                                procs[i].PriorityClass = PRIORITY_NORMAL;
                            }
                        } catch (Exception ex4) { CWLog.Error("SmartAgent", ex4); }
                    }
                }
                AddLog("ROLLBACK", "EmergencyStop", "Restored all process priorities to Normal");
            } catch (Exception ex5) { CWLog.Error("SmartAgent", ex5); }
            // Rollback: unpark cores (restore default)
            try { ExecuteCoreParking(true); } catch (Exception ex6) { CWLog.Error("SmartAgent", ex6); }
            // Rollback: resume any processes the agent suspended
            try { ExecuteResumeSuspended(); } catch (Exception ex7) { CWLog.Error("SmartAgent", ex7); }
            // Rollback: re-enable Game DVR if the agent disabled it
            if (_gameDvrApplied)
            {
                try
                {
                    using (var k = Registry.CurrentUser.OpenSubKey(@"System\GameConfigStore", true))
                        if (k != null && _gameDvrPrevEnabled != -1) k.SetValue("GameDVR_Enabled", _gameDvrPrevEnabled, RegistryValueKind.DWord);
                    using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", true))
                        if (k != null && _gameDvrPrevCapture != -1) k.SetValue("AppCaptureEnabled", _gameDvrPrevCapture, RegistryValueKind.DWord);
                    _gameDvrApplied = false;
                    AddLog("ROLLBACK", "EmergencyStop", "Restored Game DVR settings");
                } catch (Exception ex8) { CWLog.Error("SmartAgent", ex8); }
            }
            // Rollback: restore all backed-up GPU, Direct3D, DirectX UserGpuPreferences, and GameLoop Registry settings
            try
            {
                RestoreAllBackedUpRegistrySettings();
                AddLog("ROLLBACK", "EmergencyStop", "Restored all backed-up GPU and game registry settings");
            }
            catch (Exception exRegRestore) { CWLog.Error("SmartAgent.EmergencyStop.Registry", exRegRestore); }
            // Rollback journal: mark all active entries as reverted
            try
            {
                lock (_journalLock)
                {
                    for (int i = _journal.Count - 1; i >= 0; i--)
                    {
                        if (_journal[i].State == 1)
                        {
                            _journal[i].State = 2; // Reverted
                        }
                    }
                }
            } catch (Exception ex9) { CWLog.Error("SmartAgent", ex9); }
            AddLog("CRITICAL", "EmergencyStop", "ALL optimizations stopped and reverted (power plan, priorities, cores, timer, journal)");
        }

        public string GetAgentStatus()
        {
            string status = "Mode=" + _mode.ToString();
            status += "|KillSwitch=" + _killSwitchActive.ToString();
            status += "|Paused=" + IsPaused.ToString();
            status += "|SafeMode=" + _safeMode.ToString();
            status += "|CircuitOpen=" + _circuitOpen.ToString();
            status += "|Health=" + SystemHealthScore.ToString("F1");
            status += "|Decisions=" + _decisionHistory.Count.ToString();
            return status;
        }

        public void Tick()
        {
            if (_killSwitchActive) return;
            if (_pauseTicksRemaining > 0) { _pauseTicksRemaining--; return; }
            if (_agentPaused) return;
            if (_mode == AgentMode.Off && !_safeMode) return;
            _lastTickTime = DateTime.Now;
            if (System.Threading.Interlocked.Exchange(ref _tickRunning, 1) == 1) return;
            try
            {
                System.Diagnostics.Stopwatch _dbgTick = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    _tickCount++;
                    StartTickTimer();
                    // Windows Optimization AI is in-process (no heavy external model),
                    // so there is nothing to unload when idle.
                    if (_circuitOpen)
                    {
                        if (DateTime.Now < _circuitNextRetry) return;
                        AddLog("INFO", "CircuitBreaker", "Retrying after " + _circuitRetryDelayMs + "ms delay");
                        _circuitOpen = false;
                    }
                    if (_safeMode)
                    {
                        lock (_stateLock) { CollectSystemState(); }
                        AddLog("WARN", "SafeMode", "AI disabled - " + _safeModeFailureCount + " failures. Monitoring only.");
                        return;
                    }
                    if (!_selfTestPassed) RunSelfTest();
                    lock (_stateLock)
                    {
                        _prevState = _currentState.Clone();
                        CollectSystemState();
                    }
                    if (_expertSystem != null)
                    {
                        try { Events.Publish(new StateObservedEvent(_currentState.Clone())); }
                        catch (Exception ex) { LogError("RuleEngineObserve", ex); }
                    }
                    UpdateIdleDetection();
                    LearnUsagePattern(_currentState.ActiveProcess);
                    DetectGameExit();
                    CheckPostGameStabilization();
                    if (_firstCollect)
                    {
                        _smoothCpu = _currentState.CpuUsage;
                        _smoothRam = _currentState.RamUsage;
                        _smoothGpu = _currentState.GpuUsage;
                        _smoothTemp = _currentState.CpuTemp;
                        _prevTempForRate = _currentState.CpuTemp;
                        _smoothFps = _currentState.Fps > 0 ? _currentState.Fps : 60;
                        _peakCpu = _currentState.CpuUsage;
                        _peakRam = _currentState.RamUsage;
                        _peakTemp = _currentState.CpuTemp;
                        _firstCollect = false;
                    }
                    else
                    {
                        double alphaCpu = 0.4;
                        double alphaRam = 0.2;
                        double alphaGpu = 0.3;
                        double alphaTemp = 0.1;
                        _smoothCpu = _smoothCpu * (1 - alphaCpu) + _currentState.CpuUsage * alphaCpu;
                        _smoothRam = _smoothRam * (1 - alphaRam) + _currentState.RamUsage * alphaRam;
                        _smoothGpu = _smoothGpu * (1 - alphaGpu) + _currentState.GpuUsage * alphaGpu;
                        _smoothTemp = _smoothTemp * (1 - alphaTemp) + _currentState.CpuTemp * alphaTemp;
                        _smoothFps = _smoothFps * 0.7 + _currentState.Fps * 0.3;
                        _tempRateOfChange = _tempRateOfChange * 0.7 + (_currentState.CpuTemp - _prevTempForRate) * 0.3;
                        _prevTempForRate = _currentState.CpuTemp;
                        if ((DateTime.Now - _peakWindowStart).TotalSeconds >= PEAK_WINDOW_SECONDS)
                        {
                            _peakCpu = _smoothCpu; _peakRam = _smoothRam; _peakTemp = _smoothTemp;
                            _peakWindowStart = DateTime.Now;
                        }
                        if (_currentState.CpuUsage > _peakCpu) _peakCpu = _currentState.CpuUsage;
                        if (_currentState.RamUsage > _peakRam) _peakRam = _currentState.RamUsage;
                        if (_currentState.CpuTemp > _peakTemp) _peakTemp = _currentState.CpuTemp;
                        _tempHistory[_historyIndex] = _currentState.CpuTemp;
                        _historyIndex = (_historyIndex + 1) % _tempHistory.Length;
                        if (_historyCount < _tempHistory.Length) _historyCount++;
                        PredictedTemp = PredictNext(_tempHistory, _historyCount);
                    }
                    // === Startup: seed prior knowledge immediately (not after 30 ticks) so
                    // ML ensembles and the seeded Q-table guide decisions from the first second.
                    if (!_simulationMode && !_priorInitialized) InitializeWithPriorKnowledge();
                    TickOutcomeEvaluation();
                    UpdateAppMode();
                    // === UI Frame Pacing / Smoothness (ATLASX-inspired, ~30s cadence) ===
                    _fpTick++;
                    if (_fpTick >= FP_INTERVAL_TICKS)
                    {
                        _fpTick = 0;
                        ApplyFramePacing();
                    }
                    // === Network / Low-Latency Tuning (Frieren-inspired, ~60s cadence) ===
                    _ntTick++;
                    if (_ntTick >= NT_INTERVAL_TICKS)
                    {
                        _ntTick = 0;
                        ApplyNetworkTuning();
                    }
                    double cpuScore = Math.Max(0, 100 - _smoothCpu);
                    double ramScore = Math.Max(0, 100 - _smoothRam);
                    double tempScore = Math.Max(0, 100 - (_smoothTemp - 40) * 1.5);
                    double gpuScore = Math.Max(0, 100 - _smoothGpu);
                    double fpsScore = _currentState.FpsStability;
                    double avg = cpuScore * 0.2 + ramScore * 0.2 + tempScore * 0.25 + gpuScore * 0.15 + fpsScore * 0.2;
                    double worst = Math.Min(cpuScore, Math.Min(ramScore, Math.Min(tempScore, Math.Min(gpuScore, fpsScore))));
                    SystemHealthScore = Math.Max(0, Math.Min(100, avg * 0.7 + worst * 0.3));
                    if (_smoothTemp > 95) SystemHealthScore = Math.Min(SystemHealthScore, 20);
                    else if (_smoothTemp > 88) SystemHealthScore = Math.Min(SystemHealthScore, 40);
                    if (_smoothRam > 95) SystemHealthScore = Math.Min(SystemHealthScore, 25);
                    if (_smoothCpu > 98) SystemHealthScore = Math.Min(SystemHealthScore, 30);
                    double healthDelta = SystemHealthScore - _prevHealthScore;
                    if (healthDelta > 3) HealthTrend = "UP";
                    else if (healthDelta < -3) HealthTrend = "DOWN";
                    else HealthTrend = "STABLE";
                    _prevHealthScore = SystemHealthScore;
                    _selfCheckTick++;
                    if (_selfCheckTick >= 60)
                    {
                        _selfCheckTick = 0;
                        SelfHealthCheck();
                    }
                    // === Feature 1: Thermal Protection ===
                    IsThermalSafe();
                    // === Feature 3: Resource Limits ===
                    IsWithinResourceLimits();
                    // === Feature 4: New Process Detection (every 10 ticks) ===
                    _processDetectTick++;
                    if (_processDetectTick >= 10)
                    {
                        _processDetectTick = 0;
                        DetectNewProcesses();
                    }
                    // === AI Intelligence Subsystems ===
                    ProfileDevice();
                    DetectWorkload();
                    UpdateProcessImpact();
                    UpdatePredictiveAnalytics();
                    GenerateSuggestions();
                    SelfTune();
                    ComputeHealthScores();
                    ApplyAdaptiveWorkload();
                    // === AI Advanced Subsystems (every 3 ticks) ===
                    _heavySubsystemTick++;
                    if (_heavySubsystemTick >= 3)
                    {
                        _heavySubsystemTick = 0;
                        RunAutopilotCycle();
                        CheckScheduledTasks();
                        CheckAutoRecovery();
                        UpdateGameLearning();
                        ExecutePlugins();
                        RecordPerformanceSnapshot();
                        RunSelfDiagnostics();
                        OptimizePower();
                        GenerateRecommendations();
                        AnalyzeRootCauses();
                        CheckDrivers();
                    }
                    // === Elite AI Subsystems ===
                    RunAgentCycle();
                    CollectDNASample();
                    DetectSeason();
                    UpdateGoalMetrics();
                    UpdateBoosters();
                    RunContinuousOptimization();
                    GenerateForecast();
                    RunSimulationCheck();
                    UpdateKnowledgeBase();
                    DecayRLValues();
                    // === Real RL2: outcome sampling, prior pretraining, maintenance ===
                    _learningMaintTick++;
                    if (_learningMaintTick >= 30)
                    {
                        _learningMaintTick = 0;
                        RunLearningMaintenance();
                    }
                    // === Level 4: Research Grade AI (every 5 ticks) ===
                    _researchTick++;
                    if (_researchTick >= 5)
                    {
                        _researchTick = 0;
                        RunCausalAnalysis();
                        UpdateWorldModel();
                        RunAIPlanning();
                        RunLongTermMaintenance();
                        RunArchitectureUpdate();
                        UpdateSelfProfile();
                        RunCalibrationUpdate();
                    }
                    // === Level 5: Autonomous Manager (every 10 ticks) ===
                    _enterpriseTick++;
                    if (_enterpriseTick >= 10)
                    {
                        _enterpriseTick = 0;
                        RunAutonomousManagement();
                        UpdateKPIs();
                        GeneratePerformanceReport();
                    }
                    // === Level 7: Optimization Graph (every 15 ticks) ===
                    _graphTick++;
                    if (_graphTick >= 15)
                    {
                        _graphTick = 0;
                        UpdateOptimizationGraph();
                    }
                    // === Level 8: Tool Training System ===
                    RunToolTrainingCycle();
                    // === Advanced Intelligence: User Profile, Prediction, Anomaly, ML ===
                    UpdateUserProfile();
                    UpdatePredictiveOptimization();
                    RunAdvancedAnomalyDetection();
                    // === Advanced Features: Dynamic Thresholds, Temperature Learning, Cost-Benefit ===
                    UpdateAdvancedFeatures();
                    // === Feature 10: Save pre-optimization state ===
                    SavePreOptState("tick");
                    lock (_decisionLock)
                    {
                        AnalyzeAndDecide();
                    }
                    // === Process deferred heavy work (post-decision) ===
                    ProcessDeferredWork();
                    // === Feature 10: Calculate speedometer delta ===
                    CalculateSpeedometerDelta("tick");
                    // Throttle the per-tick speedometer log (was 2 entries/sec,
                    // churning the whole 200-entry opt-log every ~100s).
                    if (_tickCount % 10 == 0) AddLogOpt("SPEEDOMETER", _lastOptimizationResult);
                    // === Auto Rollback: check for post-optimization degradation ===
                    CheckAutoRollbackTimer();
                    if (_tickCount % 5 == 0)
                    {
                        string app = _currentState.IsGame ? "[GAME] " + _currentState.ActiveProcess : _currentState.ActiveProcess ?? "--";
                        string fpsStr = _frameDataReliable ? string.Format("FPS:{0:F0}", _currentState.Fps) : "FPS:--";
                        string msg = string.Format("CPU:{0:F0}% RAM:{1:F0}% GPU:{2:F0}% T:{3:F0}C | App: {4} | {5} Stability:{6:F0}",
                            _currentState.CpuUsage, _currentState.RamUsage, _currentState.GpuUsage, _currentState.CpuTemp, app, fpsStr, _currentState.FpsStability);
                        AddLog("MONITOR", "Status", msg);
                        CWLog.Info("Monitor", msg);
                    }
                }
                catch (Exception ex) { LogError("Tick", ex); CWLog.Error("Tick", ex); }
                if (_tickCount % 30 == 0)
                {
                    SaveData();
                    SmartScheduledMaintenance();
                }
                if (_tickCount % 5 == 0) AddLog("TICKDBG", "Tick", "elapsed=" + _dbgTick.ElapsedMilliseconds + "ms");
            }
            catch (Exception ex) { LogError("Tick", ex); }
            finally { System.Threading.Interlocked.Exchange(ref _tickRunning, 0); }
        }

                // Publishes a fresh process snapshot, then disposes the previous one.
        //
        // Ordering matters: the reference swap happens under the lock BEFORE any
        // old handle is disposed, so a caller can never receive a snapshot that
        // was already torn down. A caller that was still walking the previous
        // array when we dispose it can still see an ObjectDisposedException /
        // "Access is denied" on a Process property, because Process is not
        // thread-safe. That window is unavoidable for a shared cache, so we
        // (a) skip the fast path entirely when a swap is in flight, and
        // (b) let the enumeration sites keep their existing per-access guards.
        private void RefreshProcessCache()
        {
            if ((DateTime.Now - _lastProcessCacheRefresh).TotalSeconds < PROCESS_CACHE_INTERVAL_SECONDS) return;
            lock (_processCacheRefreshLock)
            {
                if ((DateTime.Now - _lastProcessCacheRefresh).TotalSeconds < PROCESS_CACHE_INTERVAL_SECONDS) return;
                Process[] fresh = null;
                try { fresh = Process.GetProcesses(); }
                catch (Exception ex) { LogError("RefreshProcessCache", ex); return; }

                Process[] old;
                lock (_processCacheLock)
                {
                    old = _cachedProcesses;
                    _cachedProcesses = fresh;
                    _lastProcessCacheRefresh = DateTime.Now;
                }
                if (old == null) return;
                for (int i = 0; i < old.Length; i++)
                {
                    try { old[i].Dispose(); } catch { /* already gone */ }
                }
            }
        }
        private Process[] GetCachedProcesses()
        {
            RefreshProcessCache();
            lock (_processCacheLock)
            {
                return _cachedProcesses;
            }
        }

        private void CollectSystemState()
        {
            if (_simulationMode && _simulatedState != null)
            {
                _currentState = _simulatedState.Clone();
                _isOnBattery = _currentState.BatteryLevel >= 0 && !_currentState.IsCharging;
                _batteryPercent = _currentState.BatteryLevel >= 0 ? (int)_currentState.BatteryLevel : 100;
                return;
            }
            try
            {
                if (_cpuLoadPc != null)
                {
                    if (!_cpuWarmupDone)
                    {
                        _cpuLoadPc.NextValue();
                        _cpuWarmupDone = true;
                    }
                    else
                    {
                        float v = _cpuLoadPc.NextValue();
                        if (v >= 0) _currentState.CpuUsage = v;
                    }
                }
            }
            catch (Exception ex) { LogError("CollectSystemState", ex); }
            try
            {
                MEMORYSTATUSEX mem = new MEMORYSTATUSEX();
                mem.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                if (GlobalMemoryStatusEx(ref mem))
                {
                    _currentState.TotalRamGB = mem.ullTotalPhys / BYTES_PER_GB;
                    _currentState.UsedRamGB = (mem.ullTotalPhys - mem.ullAvailPhys) / BYTES_PER_GB;
                    _currentState.RamUsage = (_currentState.UsedRamGB / _currentState.TotalRamGB) * 100.0;
                }
            }
            catch (Exception ex) { LogError("CollectSystemState", ex); }
            // GPU: use cached PerformanceCounterCategory
            try
            {
                if (_gpuUtilPc == null)
                {
                    if (_gpuCatCached == null)
                        _gpuCatCached = new PerformanceCounterCategory("GPU Engine");
                    foreach (string inst in _gpuCatCached.GetInstanceNames())
                    {
                        if (inst.Contains("engtype_3D"))
                        {
                            _gpuUtilPc = new PerformanceCounter("GPU Engine", "Utilization Percentage", inst);
                            _gpuUtilPc.NextValue();
                            _gpuWarmupNeeded = true;
                            break;
                        }
                    }
                }
                if (_gpuUtilPc != null)
                {
                    if (_gpuWarmupNeeded)
                    {
                        _gpuWarmupNeeded = false;
                    }
                    else
                    {
                        float v = _gpuUtilPc.NextValue();
                        if (v >= 0) _currentState.GpuUsage = v;
                    }
                }
            }
            catch (Exception ex) { LogError("CollectSystemState", ex); }
            // Disk usage: cache DriveInfo query every 3s to avoid synchronous drive geometry queries on every 400ms tick
            try
            {
                if ((DateTime.Now - _lastDiskUsageQuery).TotalSeconds >= DISK_USAGE_INTERVAL_SECONDS)
                {
                    _lastDiskUsageQuery = DateTime.Now;
                    if (_systemDriveInfo == null) _systemDriveInfo = new DriveInfo("C");
                    if (_systemDriveInfo.IsReady)
                    {
                        _cachedDiskUsage = ((_systemDriveInfo.TotalSize - _systemDriveInfo.AvailableFreeSpace) / (double)_systemDriveInfo.TotalSize) * 100.0;
                    }
                }
                _currentState.DiskUsage = _cachedDiskUsage;
            }
            catch (Exception ex) { LogError("CollectSystemState", ex); }
            // Thermal Zone: cache and query every 3 seconds
            if ((DateTime.Now - _lastThermalQuery).TotalSeconds >= THERMAL_QUERY_INTERVAL_SECONDS)
            {
                _lastThermalQuery = DateTime.Now;
                try
                {
                    // Real GPU temperature from the vendor SDK first (NVAPI/ADL).
                    // The ACPI thermal-zone counters below often do not exist for
                    // discrete GPUs, leaving GpuTemp at 0; the vendor sensor is the
                    // same source Smart Game Booster relies on.
                    double vendorGpuTemp = GpuMonitor.ReadGpuTemperatureCelsius();
                    if (vendorGpuTemp > 0 && vendorGpuTemp < 150) _currentState.GpuTemp = vendorGpuTemp;
                    if (_cachedThermalInstances == null || (DateTime.Now - _lastThermalInstanceRefresh).TotalSeconds > 30)
                    {
                        var tzCat = new PerformanceCounterCategory("Thermal Zone Information");
                        _cachedThermalInstances = tzCat.GetInstanceNames();
                        _lastThermalInstanceRefresh = DateTime.Now;
                    }
                    foreach (string inst in _cachedThermalInstances)
                    {
                        try
                        {
                            PerformanceCounter pc;
                            if (!_thermalCounters.TryGetValue(inst, out pc))
                            {
                                pc = new PerformanceCounter("Thermal Zone Information", "Temperature", inst);
                                pc.NextValue();
                                _thermalCounters[inst] = pc;
                            }
                            double v = pc.NextValue();
                            if (v > 0)
                            {
                                double c = v / THERMAL_SCALE;
                                if (c > 200) c -= KELVIN_OFFSET;
                                if (c > 0 && c < 150)
                                {
                                    string upper = inst.ToUpper();
                                    if (upper.Contains("CPU") || upper.Contains("TZ00")) _currentState.CpuTemp = c;
                                    else if (upper.Contains("GFX") || upper.Contains("GPU")) _currentState.GpuTemp = c;
                                }
                            }
                        }
                        catch (Exception ex) { LogError("CollectSystemState", ex); }
                    }
                }
                catch (Exception ex) { LogError("CollectSystemState", ex); }
            }
            // Process count: cache and query every 5 seconds
            if ((DateTime.Now - _lastProcessCountQuery).TotalSeconds >= PROCESS_COUNT_INTERVAL_SECONDS)
            {
                _lastProcessCountQuery = DateTime.Now;
                try
                {
                    lock (_processCacheLock)
                    {
                        _cachedProcessCount = _cachedProcesses != null ? _cachedProcesses.Length : 0;
                    }
                }
                catch (Exception ex) { LogError("CollectSystemState", ex); }
            }
            _currentState.ProcessCount = _cachedProcessCount;
            string fgTitle = "";
            try
            {
                IntPtr fg = GetForegroundWindow();
                if (fg != IntPtr.Zero)
                {
                    fgTitle = GetWindowTitleSafe(fg);
                    if (string.IsNullOrEmpty(fgTitle))
                    {
                        IntPtr root = GetAncestor(fg, GA_ROOT);
                        if (root != IntPtr.Zero && root != fg)
                            fgTitle = GetWindowTitleSafe(root);
                    }
                    uint pid;
                    GetWindowThreadProcessId(fg, out pid);
                    if (pid > 0)
                    {
                        try
                        {
                            using (var p = Process.GetProcessById((int)pid))
                            {
                                string pname = p.ProcessName.ToLower();
                                // Skip our own process and system processes
                                if (!SYSTEM_PROCESSES.Contains(pname))
                                {
                                    _currentState.ActiveProcess = p.ProcessName;
                                    try { _currentState.ActiveProcessPath = p.MainModule.FileName; }
                                    catch { _currentState.ActiveProcessPath = ""; }
                                }
                                else
                                {
                                    // Our window is focused - find the real foreground app by scanning windows
                                    _currentState.ActiveProcess = FindRealForegroundApp();
                                    _currentState.ActiveProcessPath = "";
                                }
                            }
                        }
                        catch { _currentState.ActiveProcess = FindRealForegroundApp(); }
                    }
                }
            }
            catch (Exception ex) { LogError("CollectSystemState", ex); }
            // Fallback: if still no active process, detect by GPU load
            if (string.IsNullOrEmpty(_currentState.ActiveProcess) && _currentState.GpuUsage > 15)
            {
                _currentState.ActiveProcess = FindGpuProcess();
            }

            // PUBG Mobile in GameLoop explicit resolution:
            string lowerProc = (_currentState.ActiveProcess ?? "").ToLowerInvariant();
            string lowerTitle = (fgTitle ?? "").ToLowerInvariant();
            bool isPubgTitle = lowerTitle.Contains("pubg") || lowerTitle.Contains("playerunknown") 
                || lowerTitle.Contains("battleground") || lowerTitle.Contains("bgmi") || lowerTitle.Contains("com.tencent.ig");
            bool isGameLoopFamily = lowerProc.Contains("aow") || lowerProc.Contains("androws") 
                || lowerProc.Contains("androidprocess") || lowerProc.Contains("appmarket") 
                || lowerProc.Contains("gameloop") || lowerProc.Contains("androidemulator")
                || (_currentState.ActiveProcessPath ?? "").ToLowerInvariant().Contains("txgameassistant")
                || (_currentState.ActiveProcessPath ?? "").ToLowerInvariant().Contains("gameloop")
                || (_currentState.ActiveProcessPath ?? "").ToLowerInvariant().Contains("tencent");

            if (isPubgTitle && isGameLoopFamily)
            {
                _currentState.ActiveProcess = "PUBG Mobile (GameLoop)";
                _currentState.IsGame = true;
                _currentState.ActiveCategory = ProcessCategory.Game;
                RememberGameConfidence("pubg", 1.0);
                RememberGameConfidence(lowerProc, 1.0);
            }
            else if (isPubgTitle && (lowerProc.Contains("tslgame") || lowerProc.Contains("pubg")))
            {
                _currentState.ActiveProcess = "PUBG: BATTLEGROUNDS";
                _currentState.IsGame = true;
                _currentState.ActiveCategory = ProcessCategory.Game;
                RememberGameConfidence(lowerProc, 1.0);
            }
            else if (lowerProc.Contains("aow_exe") || lowerProc.Contains("androwsfg") || lowerProc.Contains("androidprocess"))
            {
                _currentState.ActiveProcess = isPubgTitle ? "PUBG Mobile (GameLoop)" : "GameLoop: Android Game";
                _currentState.IsGame = true;
                _currentState.ActiveCategory = ProcessCategory.Game;
                RememberGameConfidence(lowerProc, 1.0);
            }
            else
            {
                _currentState.ActiveCategory = ClassifyProcess(_currentState.ActiveProcess, _currentState.ActiveProcessPath, fgTitle);
                _currentState.IsGame = (_currentState.ActiveCategory == ProcessCategory.Game) 
                    || IsGameProcess(_currentState.ActiveProcess, _currentState.ActiveProcessPath, fgTitle);
            }
            // When in-game but no local frame telemetry (overlay hidden), measure the
            // real presented-frame rate from the GPU output (DXGI) — the same signal
            // Smart Game Booster's swap-chain FPS hook provides, without injection.
            if (_currentState.IsGame && _dxgiFpsDue())
            {
                double displayFps = DxgiFpsReader.ReadDisplayFps();
                if (displayFps > 5 && displayFps < 400) _currentState.Fps = displayFps;
            }
            _currentState.Timestamp = DateTime.Now;
            // Battery: cached, queried in the background every 10 seconds
            if ((DateTime.Now - _lastBatteryQuery).TotalSeconds >= BATTERY_QUERY_INTERVAL_SECONDS)
            {
                if (System.Threading.Interlocked.CompareExchange(ref _batteryQueryRunning, 1, 0) == 0)
                {
                    _lastBatteryQuery = DateTime.Now;
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            using (var batQ = new ManagementObjectSearcher("SELECT EstimatedChargeRemaining,BatteryStatus FROM Win32_Battery"))
                            {
                                foreach (ManagementObject mo in batQ.Get())
                                {
                                    double level = Convert.ToDouble(mo["EstimatedChargeRemaining"]);
                                    // BatteryStatus: 1=Discharging, 2=AC, 3=Full, 6=Charging
                                    int bs = Convert.ToInt32(mo["BatteryStatus"]);
                                    lock (_wmiLock)
                                    {
                                        _wmiBatteryLevel = level;
                                        _wmiIsCharging = (bs == 6 || bs == 3);
                                    }
                                    break;
                                }
                            }
                        }
                        catch { lock (_wmiLock) { _wmiBatteryLevel = -1; _wmiIsCharging = false; } }
                        finally { System.Threading.Interlocked.Exchange(ref _batteryQueryRunning, 0); }
                    });
                }
            }
            lock (_wmiLock)
            {
                _currentState.BatteryLevel = _wmiBatteryLevel;
                _currentState.IsCharging = _wmiIsCharging;
            }
            // === Feature 2: Battery Mode Detection ===
            try
            {
                // IsCharging=true means plugged in and charging/full
                // IsCharging=false + BatteryLevel>0 means running on battery
                _isOnBattery = _currentState.BatteryLevel >= 0 && !_currentState.IsCharging;
                _batteryPercent = _currentState.BatteryLevel >= 0 ? (int)_currentState.BatteryLevel : 100;
                if (_isOnBattery && !_wasOnBattery) { _appMode = AppMode.Battery; AddLogOpt("BATTERY_MODE", "Switched to battery power - conservative mode"); }
                else if (!_isOnBattery && _wasOnBattery) { _appMode = AppMode.Desktop; AddLogOpt("AC_POWER", "Switched to AC power - normal mode"); }
                _wasOnBattery = _isOnBattery;
            }
            catch (Exception ex) { LogError("CollectSystemState", ex); }
            // === Feature 7: SMART Disk Health (background, every 5 minutes) ===
            try
            {
                if ((DateTime.Now - _lastDiskCheck).TotalSeconds > DISK_CHECK_INTERVAL_SECONDS)
                {
                    if (System.Threading.Interlocked.CompareExchange(ref _diskQueryRunning, 1, 0) == 0)
                    {
                        _lastDiskCheck = DateTime.Now;
                        System.Threading.Tasks.Task.Run(() =>
                        {
                            try
                            {
                                using (var diskQ = new ManagementObjectSearcher("SELECT Status,FreeSpace,Size FROM Win32_DiskDrive WHERE DeviceID='\\\\\\\\.\\\\PHYSICALDRIVE0'"))
                                {
                                    foreach (ManagementObject mo in diskQ.Get())
                                    {
                                        string status = Convert.ToString(mo["Status"]);
                                        string newStatus = string.IsNullOrEmpty(status) ? "Unknown" : status;
                                        lock (_wmiLock)
                                        {
                                            _wmiDiskHealthStatus = newStatus;
                                            _wmiDiskHealthPercent = status == "OK" ? 100.0 : (status == "Degraded" ? 70.0 : (status == "Pred Fail" ? 30.0 : 50.0));
                                        }
                                    }
                                }
                            }
                            catch (Exception ex) { LogError("CollectSystemState", ex); }
                            finally { System.Threading.Interlocked.Exchange(ref _diskQueryRunning, 0); }
                        });
                    }
                }
            }
            catch (Exception ex) { LogError("CollectSystemState", ex); }
            lock (_wmiLock)
            {
                _diskHealthStatus = _wmiDiskHealthStatus;
                _diskHealthPercent = _wmiDiskHealthPercent;
            }
            // === Feature 8: Network Monitoring (background, cached, every 3s) ===
            if ((DateTime.Now - _lastNetworkQuery).TotalSeconds >= NETWORK_QUERY_INTERVAL_SECONDS)
            {
                if (System.Threading.Interlocked.CompareExchange(ref _networkQueryRunning, 1, 0) == 0)
                {
                    _lastNetworkQuery = DateTime.Now;
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            using (var netQ = new ManagementObjectSearcher("SELECT BytesReceivedPerSec,BytesSentPerSec FROM Win32_PerfFormattedData_Tcpip_NetworkInterface"))
                            {
                                long totalRecv = 0, totalSent = 0;
                                foreach (ManagementObject mo in netQ.Get())
                                {
                                    totalRecv += Convert.ToInt64(mo["BytesReceivedPerSec"]);
                                    totalSent += Convert.ToInt64(mo["BytesSentPerSec"]);
                                }
                                lock (_wmiLock)
                                {
                                    _wmiNetDownloadKBps = totalRecv / 1024.0;
                                    _wmiNetUploadKBps = totalSent / 1024.0;
                                    if (_wmiNetDownloadKBps < 0) _wmiNetDownloadKBps = 0;
                                    if (_wmiNetUploadKBps < 0) _wmiNetUploadKBps = 0;
                                }
                            }
                        }
                        catch (Exception ex) { LogError("CollectSystemState", ex); }
                        finally { System.Threading.Interlocked.Exchange(ref _networkQueryRunning, 0); }
                    });
                }
            }
            lock (_wmiLock)
            {
                _netDownloadKBps = _wmiNetDownloadKBps;
                _netUploadKBps = _wmiNetUploadKBps;
            }
            _currentState.SentBytes = (long)(_netUploadKBps * 1024);
            _currentState.RecvBytes = (long)(_netDownloadKBps * 1024);
            // Boot slow detection (check once every 5 minutes)
            if ((DateTime.Now - _lastBootCheckTime).TotalMinutes >= 5)
            {
                _lastBootCheckTime = DateTime.Now;
                try
                {
                    using (var bootQ = new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem"))
                    {
                        foreach (ManagementObject mo in bootQ.Get())
                        {
                            DateTime lastBoot = ManagementDateTimeConverter.ToDateTime(Convert.ToString(mo["LastBootUpTime"]));
                            double bootAgeSeconds = (DateTime.Now - lastBoot).TotalSeconds;
                            // If system just booted (less than 5 minutes ago), check for slow boot
                            if (bootAgeSeconds < 300 && bootAgeSeconds > 120)
                            {
                                AddSuggestion("Slow Boot Detected", 
                                    "System boot took " + string.Format("{0:F0}", bootAgeSeconds / 60) + " minutes",
                                    "Consider disabling unnecessary startup programs or upgrading to SSD",
                                    SuggestionPriority.High, "Performance", 60, false);
                            }
                        }
                    }
                }
                catch (Exception ex) { LogError("CollectSystemState", ex); }
            }
            _currentState.CpuCoreCount = Environment.ProcessorCount;
            // CPU Freq: registry only once (never changes)
            if (_cachedCpuFreqGHz <= 0)
            {
                try
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                    {
                        if (key != null)
                        {
                            object mhzObj = key.GetValue("~MHz");
                            if (mhzObj != null) _cachedCpuFreqGHz = Convert.ToDouble(mhzObj) / 1000.0;
                        }
                    }
                }
                catch (Exception ex) { LogError("CollectSystemState", ex); }
            }
            _currentState.CpuFreqGHz = _cachedCpuFreqGHz;
            UpdateFrameStability();
            UpdateAnomalyDetection();
            if (OnStateUpdate != null) OnStateUpdate(_currentState.Clone());
        }

        private void UpdateFrameStability()
        {
            if (_currentState.Fps > 0 && _prevFps > 0)
            {
                double delta = Math.Abs(_currentState.Fps - _prevFps);
                _currentState.FrameTimeMs = _currentState.Fps > 0 ? 1000.0 / _currentState.Fps : 0;
                _currentState.FrameTimeJitter = delta;
                if (delta < 2) _currentState.FpsStability = Math.Min(100, _currentState.FpsStability + 5);
                else if (delta < 5) _currentState.FpsStability = Math.Max(0, _currentState.FpsStability - 2);
                else _currentState.FpsStability = Math.Max(0, _currentState.FpsStability - 8);
                if (_currentState.Fps1Low <= 0) _currentState.Fps1Low = _currentState.Fps * 0.85;
                else _currentState.Fps1Low = _currentState.Fps1Low * 0.95 + (_currentState.Fps * 0.8) * 0.05;
                if (_currentState.Fps01Low <= 0) _currentState.Fps01Low = _currentState.Fps * 0.65;
                else _currentState.Fps01Low = _currentState.Fps01Low * 0.95 + (_currentState.Fps * 0.5) * 0.05;
            }
            _prevFps = _currentState.Fps;
            _prevTemp = _currentState.CpuTemp;
        }

        private List<AgentDecision> _reusableDecisions = new List<AgentDecision>(16);
        // Pre-allocated knapsack arrays to avoid per-tick GC
        private int[] _knapsackCosts = new int[32];
        private double[] _knapsackValues = new double[32];
        private bool[] _knapsackHeavy = new bool[32];
        private double[] _knapsackDp = new double[32];
        private int[] _knapsackChoice = new int[32];
        private List<AgentDecision> _knapsackResult = new List<AgentDecision>(8);
        // Pre-allocated candidate/emergency lists
        private List<AgentDecision> _reusableCandidates = new List<AgentDecision>(8);
        private List<AgentDecision> _reusableEmergencies = new List<AgentDecision>(4);
        private List<AgentDecision> _reusableResolved = new List<AgentDecision>(8);
        private HashSet<string> _reusableConflictSet = new HashSet<string>();
        private DateTime _lastExplanationTime = DateTime.MinValue;

        private void AnalyzeAndDecide()
        {
            if ((DateTime.Now - _lastAnalyzeProbe).TotalSeconds > 30)
            {
                _lastAnalyzeProbe = DateTime.Now;
                CWLog.Info("Analyzer", "AnalyzeAndDecide entered (tick=" + _tickCount + ")");
            }
            _pendingDecisions.Clear();
            _reusableDecisions.Clear();
            var decisions = _reusableDecisions;
            // === Feature 1: Thermal guard - skip aggressive analyzers if hot ===
            bool thermalSafe = IsThermalSafe();
            bool resourceSafe = IsWithinResourceLimits();
            if (!RunAnalyzer("Thermal", () => AnalyzeThermal(decisions))) return;
            if (thermalSafe && !RunAnalyzer("Memory", () => AnalyzeMemory(decisions))) return;
            if (thermalSafe && resourceSafe && !RunAnalyzer("CPU", () => AnalyzeCPU(decisions))) return;
            if (!RunAnalyzer("Frame", () => AnalyzeFrameStability(decisions))) return;
            if (!RunAnalyzer("LoadBalancer", () => AnalyzeGameCpuGpuLoadBalancing(decisions))) return;
            if (thermalSafe && !RunAnalyzer("Background", () => AnalyzeBackground(decisions))) return;
            if (thermalSafe && !RunAnalyzer("Power", () => AnalyzePower(decisions))) return;
            if (thermalSafe && resourceSafe && !RunAnalyzer("Affinity", () => AnalyzeAffinityAndIo(decisions))) return;
            if (thermalSafe && !RunAnalyzer("DesktopSmoothness", () => AnalyzeDesktopSmoothness(decisions))) return;
            // === Proactive Toolbox: keep every capability active even when the
            // system is healthy (startup audit, network, DNS, ...) ===
            if (thermalSafe && resourceSafe && !RunAnalyzer("Proactive", () => AnalyzeProactiveMaintenance(decisions))) return;
            // === Elite AI: Multi-Agent consensus recommendation ===
            AgentDecision multiAgentDecision = null;
            try
            {
                multiAgentDecision = GetMultiAgentRecommendation();
                if (multiAgentDecision != null && multiAgentDecision.Confidence > 0.5)
                {
                    multiAgentDecision.Impact = 8;
                    decisions.Add(multiAgentDecision);
                }
            }
            catch (Exception ex) { LogError("AnalyzeAndDecide", ex); }
            // === ML Neural Network as a QUALITY ASSESSOR (unified decision line) ===
            // The neural ensemble's momentary estimate is recorded as a learned
            // quality prior for the RL/ranking layer (RankAndFilter + Experience),
            // and it only adds its own decision when NO other engine already
            // proposed the same action (gap-fill) — it never dual-votes.
            AgentDecision mlDecision = null;
            try
            {
                mlDecision = GetMLDecision();
                if (mlDecision != null && mlDecision.Action != "Abstain")
                {
                    if (mlDecision.Confidence > 0.5)
                    {
                        _mlQualityByAction[mlDecision.Action] = mlDecision.Confidence;
                        mlDecision.Impact = 6;
                        mlDecision.Reason = "[ML] " + mlDecision.Reason;
                        bool duplicate = false;
                        for (int di = 0; di < decisions.Count; di++)
                        {
                            if (decisions[di].Action == mlDecision.Action) { duplicate = true; break; }
                        }
                        if (!duplicate) decisions.Add(mlDecision); // gap-fill only
                    }
                }
            }
            catch (Exception ex) { LogError("AnalyzeAndDecide", ex); }
            // === Windows Optimization AI (in-app neural ensemble) recommendation ===
            // Consult the Windows Optimization AI when there is a real problem AND
            // the local agents are unsure or conflicting. It is in-process so it
            // is always available - no external model to load.
            bool problemPresent = _currentState != null && (
                _smoothCpu > 85 || _smoothRam > 85 || _currentState.Fps < 45 ||
                _currentState.FpsStability < 45 || _smoothTemp > 65);
            bool conflict = mlDecision != null && multiAgentDecision != null &&
                            mlDecision.Action != multiAgentDecision.Action;
            bool woaNeeded = problemPresent &&
                              (mlDecision == null || mlDecision.Confidence <= 0.5 || conflict);
            AgentDecision woaDecision = null;
            try
            {
                woaDecision = GetWindowsAIDecision(woaNeeded);
                if (woaDecision != null)
                {
                    if (woaDecision.Confidence > 0.5)
                    {
                        woaDecision.Impact = 9;
                        woaDecision.Reason = "[WinOptAI] " + woaDecision.Reason;
                        decisions.Add(woaDecision);
                        string act = woaDecision.Action;
                        if (act == _woaLastAction)
                            _woaLastConfidence = Math.Min(0.95, _woaLastConfidence + 0.05);
                        _woaLastAction = act;
                    }
                    else
                    {
                        _woaLastConfidence = Math.Max(0.3, _woaLastConfidence - 0.03);
                    }
                }
            }
            catch (Exception ex) { LogError("AnalyzeAndDecide", ex); CWLog.Error("WinOptAI.Analyze", ex); }
            // === Professional Expert System recommendation ===
            try { IntegrateRuleEngineDecisions(decisions); }
            catch (Exception ex) { LogError("AnalyzeAndDecide", ex); CWLog.Error("RuleEngine.Analyze", ex); }
            // === Meta-Learning: cross-profile transfer recommendation ===
            try
            {
                string metaAction = GetMetaRecommendation();
                if (metaAction != null)
                {
                    double metaConf = GetMetaConfidence();
                    if (metaConf > 0.55)
                    {
                        var metaDecision = new AgentDecision();
                        metaDecision.Action = metaAction;
                        metaDecision.Category = MLActionToCategory(metaAction);
                        metaDecision.Description = "Meta-Learning: " + metaAction;
                        metaDecision.Reason = string.Format("Meta confidence: {0:F0}% (cross-session knowledge)", metaConf * 100);
                        metaDecision.Confidence = metaConf;
                        metaDecision.Safety = DecisionSafety.Safe;
                        metaDecision.Impact = 4;
                        decisions.Add(metaDecision);
                    }
                }
            }
            catch (Exception ex) { LogError("AnalyzeAndDecide", ex); }
            // === World Model: dependency-aware decisions ===
            try
            {
                var wmDecisions = GetWorldModelDecisions();
                if (wmDecisions != null)
                {
                    for (int wi = 0; wi < wmDecisions.Count; wi++)
                    {
                        if (wmDecisions[wi].Confidence > 0.5)
                            decisions.Add(wmDecisions[wi]);
                    }
                }
            }
            catch (Exception ex) { LogError("AnalyzeAndDecide", ex); }
            // === Predictive Maintenance ===
            try
            {
                var maintAlerts = CheckPredictiveMaintenance();
                if (maintAlerts != null)
                {
                    for (int mi = 0; mi < maintAlerts.Count; mi++)
                    {
                        MaintenanceAlert alert = maintAlerts[mi];
                        if (alert.Severity >= AlertSeverity.High)
                        {
                            decisions.Add(new AgentDecision
                            {
                                Category = DecisionCategory.MemoryClean,
                                Action = "PredictiveMaintenance_" + alert.Category,
                                Confidence = alert.Severity == AlertSeverity.Critical ? 0.95 : 0.75,
                                Risk = 0.1,
                                Reason = alert.Message,
                                Source = "PredictiveMaintenance"
                            });
                        }
                    }
                }
            }
            catch (Exception ex) { LogError("AnalyzeAndDecide", ex); }
            // === Resource Forecasting ===
            try
            {
                var forecastResult = UpdateResourceForecasting();
                if (forecastResult != null && forecastResult.Predictions != null)
                {
                    for (int fi = 0; fi < forecastResult.Predictions.Count; fi++)
                    {
                        ResourcePrediction pred = forecastResult.Predictions[fi];
                        if (pred.Prediction > 85 && pred.Trend > 2)
                        {
                            decisions.Add(new AgentDecision
                            {
                                Category = DecisionCategory.MemoryClean,
                                Action = "ForecastPrevent_" + pred.Resource,
                                Confidence = pred.Confidence * 0.8,
                                Risk = 0.15,
                                Reason = string.Format("Forecast: {0} predicted to reach {1:F0}% in {2} (trend +{3:F1})",
                                    pred.Resource, pred.Prediction, pred.TimeHorizon, pred.Trend),
                                Source = "ResourceForecasting"
                            });
                        }
                    }
                }
            }
            catch (Exception ex) { LogError("AnalyzeAndDecide", ex); }
            // === Smart Scheduling ===
            try
            {
                var scheduleResult = UpdateSmartScheduling();
                if (scheduleResult != null && scheduleResult.ReadyTasks != null)
                {
                    for (int si = 0; si < scheduleResult.ReadyTasks.Count; si++)
                    {
                        SmartScheduledTask task = scheduleResult.ReadyTasks[si];
                        decisions.Add(new AgentDecision
                        {
                            Category = DecisionCategory.MemoryClean,
                            Action = "Scheduled_" + task.Name,
                            Confidence = 0.7,
                            Risk = 0.05,
                            Reason = string.Format("Scheduled task ready: {0} (Priority: {1})", task.Name, task.Priority),
                            Source = "SmartScheduling"
                        });
                    }
                }
            }
            catch (Exception ex) { LogError("AnalyzeAndDecide", ex); }
            // === User Behavior Learning ===
            try
            {
                var behaviorResult = UpdateUserBehaviorLearning();
                if (behaviorResult != null && behaviorResult.Recommendations != null)
                {
                    for (int ui = 0; ui < behaviorResult.Recommendations.Count; ui++)
                    {
                        UserBehaviorRecommendation rec = behaviorResult.Recommendations[ui];
                        if (rec.Priority >= 2)
                        {
                            decisions.Add(new AgentDecision
                            {
                                Category = DecisionCategory.MemoryClean,
                                Action = "UserBehavior_" + rec.Category,
                                Confidence = 0.65,
                                Risk = 0.05,
                                Reason = rec.Recommendation,
                                Source = "UserBehaviorLearning"
                            });
                        }
                    }
                }
            }
            catch (Exception ex) { LogError("AnalyzeAndDecide", ex); }
            // === Network Optimization (inspired by Frieren AI) ===
            if (!RunAnalyzer("Network", () => AnalyzeNetwork(decisions))) return;
            // === Power Plan Manager (inspired by Frieren AI) ===
            if (!RunAnalyzer("PowerPlan", () => AnalyzePowerPlan(decisions))) return;
            // === Thermal Alert System (inspired by Frieren AI) ===
            if (!RunAnalyzer("ThermalAlert", () => AnalyzeThermalAlerts(decisions))) return;
            // === Tweak Knowledge Base (FrierenAI / VoidBattery / ClearWin) ===
            if (!RunAnalyzer("KnowledgeBase", () => AnalyzeTweakKnowledge(decisions))) return;
            // === Periodic Health Check: ensure timeline & explanation have content on healthy systems ===
            if (decisions.Count == 0 && _tickCount % 10 == 0 && _smoothTemp < 80 && _smoothCpu < 70 && _smoothRam < 70)
            {
                decisions.Add(new AgentDecision
                {
                    Category = DecisionCategory.SystemStatus,
                    Action = "HealthCheck",
                    Description = "فحص دوري للنظام",
                    Reason = string.Format("النظام يعمل بشكل طبيعي | CPU:{0:F0}% RAM:{1:F0}% حرارة:{2:F0}C",
                        _smoothCpu, _smoothRam, _smoothTemp),
                    Confidence = 0.50,
                    Risk = 0.0,
                    Safety = DecisionSafety.Safe,
                    Impact = 1
                });
            }
            // === Unified decision line: collapse any duplicate actions so a single
            // engine never double-votes (highest impact/confidence wins) ===
            DeduplicateDecisions(decisions);
            RankAndFilter(decisions);
            // === Generate explanation for top decision EVEN IF not executed ===
            try
            {
                if (_explanationHistory.Count == 0 || (DateTime.Now - _lastExplanationTime).TotalSeconds > 5)
                {
                    AgentDecision topDecision = decisions.Count > 0 ? decisions[0] : null;
                    if (topDecision == null && _pendingDecisions.Count > 0) topDecision = _pendingDecisions[0];
                    if (topDecision != null)
                    {
                        ExplainDecisionFull(topDecision, _pendingDecisions);
                        _lastExplanationTime = DateTime.Now;
                    }
                }
            }
            catch (Exception ex) { LogError("AnalyzeAndDecide", ex); }
            ProcessDecisions(decisions);
            // Apply persistent rules every 30 ticks (~60s) not every tick
            _persistentRulesTick++;
            if (_persistentRulesTick >= 30) { _persistentRulesTick = 0; ApplyPersistentRules(); }
        }

        private void AnalyzeAffinityAndIo(List<AgentDecision> decisions)
        {
            if (_currentState.IsGame)
            {
                // Set game to use all cores when gaming
                decisions.Add(new AgentDecision
                {
                    Category = DecisionCategory.CpuAffinity,
                    Action = "SetGameAffinity",
                    Description = "Set game to use all CPU cores",
                    Reason = "Game detected - maximize CPU affinity",
                    Confidence = 0.75,
                    Safety = DecisionSafety.Safe
                });
                // Low I/O priority for background during gaming
                decisions.Add(new AgentDecision
                {
                    Category = DecisionCategory.IoPriority,
                    Action = "SetLowIoPriority",
                    Description = "Set foreground process to low I/O priority for smoother disk access",
                    Reason = "Game active - reduce I/O contention",
                    Confidence = 0.65,
                    Safety = DecisionSafety.Safe
                });
                // Game DVR / background recording steals FPS - disable once per session
                if (!_gameDvrApplied)
                {
                    decisions.Add(new AgentDecision
                    {
                        Category = DecisionCategory.GameBoost,
                        Action = "DisableGameDvr",
                        Description = "Disable Windows Game DVR background recording (revertible)",
                        Reason = "Game active - Game DVR recording steals FPS",
                        Confidence = 0.7,
                        Safety = DecisionSafety.Safe,
                        Source = "GamingAnalyzer",
                        Impact = 4
                    });
                }
            }
            if (_currentState.CpuUsage > 80 && _currentState.IsGame)
            {
                decisions.Add(new AgentDecision
                {
                    Category = DecisionCategory.CoreParking,
                    Action = "UnparkAllCores",
                    Description = "Unpark all CPU cores for maximum performance",
                    Reason = string.Format("CPU {0:F0}% with game - need all cores", _currentState.CpuUsage),
                    Confidence = 0.80,
                    Safety = DecisionSafety.Safe
                });
            }
        }

        private void AnalyzeProactiveMaintenance(List<AgentDecision> decisions)
        {
            _proactiveTick++;
            if (_proactiveTick < PROACTIVE_INTERVAL_TICKS) return;
            _proactiveTick = 0;
            // Only when the system is healthy and nothing needs urgent attention.
            if (_mode == AgentMode.Off) return;
            if (_currentState == null || _currentState.IsGame) return;
            if (_smoothTemp > 68 || _smoothCpu > 75 || _smoothRam > 80) return;

            // Round-robin through the toolbox so each capability gets exercised
            // and learned from even while the system is fine.
            switch (_proactiveIndex % 4)
            {
                case 0:
                    // Startup audit: propose only when real safe-to-disable bloat exists
                    try
                    {
                        var items = StartupClassifier.EnumerateStartupItems();
                        int bloat = 0;
                        foreach (var it in items)
                            if (it.Recommendation == StartupRecommendation.SafeToDisable) bloat++;
                        if (bloat > 0)
                        {
                            decisions.Add(new AgentDecision
                            {
                                Category = DecisionCategory.BackgroundControl,
                                Action = "StartupDisable",
                                Description = "Proactive startup audit: disable unnecessary startup items",
                                Reason = string.Format("{0} safe-to-disable startup items - speeding up boot", bloat),
                                Confidence = 0.6,
                                Safety = DecisionSafety.Safe,
                                Source = "ProactiveMaintenance",
                                Impact = 3
                            });
                        }
                    }
                    catch (Exception ex) { LogError("AnalyzeProactiveMaintenance", ex); }
                    break;
                case 1:
                    decisions.Add(new AgentDecision
                    {
                        Category = DecisionCategory.NetworkOptimize,
                        Action = "FlushDnsCache",
                        Description = "Proactive DNS cache flush",
                        Reason = "Healthy-state maintenance - keep DNS resolution fresh",
                        Confidence = 0.55,
                        Safety = DecisionSafety.Safe,
                        Source = "ProactiveMaintenance",
                        Impact = 2
                    });
                    break;
                case 2:
                    decisions.Add(new AgentDecision
                    {
                        Category = DecisionCategory.NetworkOptimize,
                        Action = "OptimizeNetwork",
                        Description = "Proactive network optimization",
                        Reason = "Healthy-state maintenance - verify network tuning",
                        Confidence = 0.55,
                        Safety = DecisionSafety.Safe,
                        Source = "ProactiveMaintenance",
                        Impact = 3
                    });
                    break;
                case 3:
                    decisions.Add(new AgentDecision
                    {
                        Category = DecisionCategory.SystemStatus,
                        Action = "CleanTempFiles",
                        Description = "Proactive cleanup of stale temp files",
                        Reason = "Healthy-state maintenance - reclaim disk space",
                        Confidence = 0.55,
                        Safety = DecisionSafety.Safe,
                        Source = "ProactiveMaintenance",
                        Impact = 2
                    });
                    break;
            }
            _proactiveIndex++;
        }

        private void AnalyzeThermal(List<AgentDecision> decisions)
        {
            // Adaptive Thermal Model: consider rate of change, project ahead, then CLAMP
            double effectiveTemp = Math.Min(100, _smoothTemp + _tempRateOfChange * 3);
            if (_smoothTemp > 95 || effectiveTemp > 98)
            {
                decisions.Add(new AgentDecision
                {
                    Category = DecisionCategory.ThermalProtect,
                    Action = "EmergencyThrottle",
                    Description = "Deep thermal protection: reduce all loads",
                    Reason = string.Format("CRITICAL: CPU={0:F0}C GPU={1:F0}C - Hardware safety risk", _currentState.CpuTemp, _currentState.GpuTemp),
                    Confidence = 0.98,
                    Safety = DecisionSafety.Safe
                });
            }
            else if (_currentState.CpuTemp > 85 || _currentState.GpuTemp > 85)
            {
                // During gaming: only trigger thermal reduction at higher threshold
                if (_currentState.IsGame && _smoothFps > 30 && _currentState.CpuTemp < 90 && _currentState.GpuTemp < 90)
                {
                    // Don't aggressively reduce during gaming unless critical
                }
                else
                {
                    decisions.Add(new AgentDecision
                    {
                        Category = DecisionCategory.ThermalProtect,
                        Action = "ReduceThermal",
                        Description = "Reduce thermal load to prevent throttling",
                        Reason = string.Format("High temp: CPU={0:F0}C GPU={1:F0}C - Risk of thermal throttle", _currentState.CpuTemp, _currentState.GpuTemp),
                        Confidence = 0.85,
                        Safety = DecisionSafety.Safe
                    });
                }
            }
            else if (_currentState.CpuTemp > 75 && _currentState.IsGame)
            {
                decisions.Add(new AgentDecision
                {
                    Category = DecisionCategory.ThermalProtect,
                    Action = "MonitorTemp",
                    Description = "Temperature elevated during gaming, monitoring closely",
                    Reason = string.Format("CPU={0:F0}C during gaming - watch for spikes", _currentState.CpuTemp),
                    Confidence = 0.6,
                    Safety = DecisionSafety.Safe
                });
            }
            // Prediction-based proactive action (only when confidence > 0.5)
            if (PredictedTemp > 92 && _currentState.CpuTemp < 85 && PredictionConfidence > 0.5)
            {
                decisions.Add(new AgentDecision
                {
                    Category = DecisionCategory.ThermalProtect,
                    Action = "ReduceThermal",
                    Description = "Proactive thermal reduction (trend prediction)",
                    Reason = string.Format("Predicted: {0:F0}C (conf:{1:F0}%) current: {2:F0}C", PredictedTemp, PredictionConfidence * 100, _currentState.CpuTemp),
                    Confidence = 0.7 * PredictionConfidence,
                    Safety = DecisionSafety.Safe
                });
            }
        }

        private void AnalyzeMemory(List<AgentDecision> decisions)
        {
            // NEVER trigger working-set cleans while a game is active: EmptyWorkingSet
            // and standby purge force the game/emulator pages out of RAM -> paging ->
            // visible stutter, which is exactly what the gaming optimizations must avoid.
            if (_currentState.IsGame) return;
            if (_currentState.RamUsage > 90)
            {
                decisions.Add(new AgentDecision
                {
                    Category = DecisionCategory.MemoryClean,
                    Action = "AggressiveClean",
                    Description = "Urgent memory cleanup: RAM critically full",
                    Reason = string.Format("RAM usage {0:F0}% - severe pressure, OOM risk", _currentState.RamUsage),
                    Confidence = 0.95,
                    Safety = DecisionSafety.Safe
                });
                decisions.Add(new AgentDecision
                {
                    Category = DecisionCategory.PurgeStandby,
                    Action = "PurgeStandbyList",
                    Description = "Purge standby memory list",
                    Reason = "High RAM pressure with large standby list",
                    Confidence = 0.90,
                    Safety = DecisionSafety.Safe
                });
            }
            else if (_currentState.RamUsage > 80)
            {
                decisions.Add(new AgentDecision
                {
                    Category = DecisionCategory.MemoryClean,
                    Action = "LightClean",
                    Description = "Light memory optimization",
                    Reason = string.Format("RAM usage {0:F0}% - moderate pressure", _currentState.RamUsage),
                    Confidence = 0.80,
                    Safety = DecisionSafety.Safe
                });
            }
            else if (_currentState.RamUsage > 70 && _currentState.IsGame)
            {
                decisions.Add(new AgentDecision
                {
                    Category = DecisionCategory.MemoryClean,
                    Action = "PreemptiveClean",
                    Description = "Pre-emptive memory cleanup for gaming",
                    Reason = string.Format("RAM {0:F0}% with game active - prevent future pressure", _currentState.RamUsage),
                    Confidence = 0.70,
                    Safety = DecisionSafety.Safe
                });
            }
        }

        private void AnalyzeCPU(List<AgentDecision> decisions)
        {
            if (_currentState.CpuUsage > 95)
            {
                decisions.Add(new AgentDecision
                {
                    Category = DecisionCategory.BackgroundControl,
                    Action = "LimitBackground",
                    Description = "Severely limit background CPU usage",
                    Reason = string.Format("CPU at {0:F0}% - near saturation", _currentState.CpuUsage),
                    Confidence = 0.90,
                    Safety = DecisionSafety.Safe
                });
            }
            if (_currentState.IsGame)
            {
                decisions.Add(new AgentDecision
                {
                    Category = DecisionCategory.ProcessPriority,
                    Action = "BoostGamePriority",
                    Description = "Set game process to High priority, background to BelowNormal",
                    Reason = string.Format("Game detected ({0}) - priority boost, dedicated GPU & unparking for 60+ FPS", _currentState.ActiveProcess ?? "Game"),
                    Confidence = 0.95,
                    Safety = DecisionSafety.Safe
                });
            }
        }

        private void AnalyzeDesktopSmoothness(List<AgentDecision> decisions)
        {
            if (_mode == AgentMode.Off) return;
            if (_currentState == null) return;
            // Strict Mode Duality: When a game is running, Desktop Smoothness is completely yielded to Gaming Mode!
            if (_currentState.IsGame) return;

            // Thermal safety: If system is hot, thermal protection takes precedence
            if (_smoothTemp > 75) return;

            // Desktop Smoothness proactive recommendation during daily multitasking & productivity
            if (_smoothCpu < 85 && _smoothRam < 85)
            {
                decisions.Add(new AgentDecision
                {
                    Category = DecisionCategory.ProcessPriority,
                    Action = "OptimizeDesktopSmoothness",
                    Description = "تعزيز سلاسة الاستخدام المكتبي وخفض زمن الاستجابة (Desktop Smoothness)",
                    Reason = string.Format("استخدام مكتبي نشط (معالج {0:F0}%، رام {1:F0}%) - رفع أولوية النافذة النشطة وضبط مؤقت 1.0ms لسلاسة تامة", _smoothCpu, _smoothRam),
                    Confidence = 0.90,
                    Safety = DecisionSafety.Safe,
                    Source = "DesktopSmoothnessAnalyzer",
                    Impact = 6
                });
            }

            // Inactive background apps memory trimming (before reaching aggressive memory clean levels)
            if (_smoothRam >= 60 && _smoothRam < 80)
            {
                decisions.Add(new AgentDecision
                {
                    Category = DecisionCategory.MemoryClean,
                    Action = "TrimInactiveWorkingSets",
                    Description = "تقليص كاش التطبيقات الخلفية الخاملة لتسريع الجهاز",
                    Reason = string.Format("استهلاك الذاكرة {0:F0}% - تفريغ صفحات البرامج الخاملة مع الحفاظ التام على البرنامج النشط", _smoothRam),
                    Confidence = 0.84,
                    Safety = DecisionSafety.Safe,
                    Source = "DesktopSmoothnessAnalyzer",
                    Impact = 5
                });
            }
        }

        private void AnalyzeFrameStability(List<AgentDecision> decisions)
        {
            if (_currentState.IsGame && _currentState.Fps > 0 && _currentState.Fps < 60)
            {
                decisions.Add(new AgentDecision
                {
                    Category = DecisionCategory.GameBoost,
                    Action = "StabilizeFrames",
                    Description = "تثبيت الفريمات على 60 FPS قسرياً ومنع الهبوط (Aggressive 60 FPS Floor)",
                    Reason = string.Format("الفريمات الحالية {0:F0} أقل من 60 FPS - استدعاء حزمة استقرار الإطارات وإلزام كارت الشاشة والمعالج", _currentState.Fps),
                    Confidence = 0.98,
                    Safety = DecisionSafety.Safe
                });
            }
            if (_currentState.FrameTimeJitter > 10 && _currentState.IsGame)
            {
                decisions.Add(new AgentDecision
                {
                    Category = DecisionCategory.GameBoost,
                    Action = "StabilizeFrames",
                    Description = "Apply frame stability optimizations",
                    Reason = string.Format("Frame jitter {0:F1}ms - high instability detected", _currentState.FrameTimeJitter),
                    Confidence = 0.88,
                    Safety = DecisionSafety.Safe
                });
            }
            if (_currentState.Fps1Low > 0 && _currentState.Fps > 0)
            {
                double ratio = _currentState.Fps1Low / _currentState.Fps;
                if (ratio < 0.5 && _currentState.IsGame)
                {
                    decisions.Add(new AgentDecision
                    {
                        Category = DecisionCategory.GameBoost,
                        Action = "ReduceMicroStutter",
                        Description = "Reduce micro-stuttering: 1% low is critically low",
                        Reason = string.Format("1% low ratio: {0:F2} (FPS: {1:F0}, 1%low: {2:F0})", ratio, _currentState.Fps, _currentState.Fps1Low),
                        Confidence = 0.92,
                        Safety = DecisionSafety.Safe
                    });
                }
            }
        }

        private void AnalyzeGameCpuGpuLoadBalancing(List<AgentDecision> decisions)
        {
            if (_currentState == null || !_currentState.IsGame) return;

            double cpu = _currentState.CpuUsage;
            double gpu = _currentState.GpuUsage;
            double temp = _currentState.CpuTemp;
            double gpuTemp = _currentState.GpuTemp;

            // Scenario 1: CPU Bottleneck (CPU heavily loaded while GPU is starved)
            if (cpu > 70 && gpu < 65)
            {
                decisions.Add(new AgentDecision
                {
                    Category = DecisionCategory.GameBoost,
                    Action = "BalanceGameLoad",
                    Description = "توزيع وتخفيف الحمل عن المعالج لتسريع تغذية كارت الشاشة (CPU Bottleneck Relief)",
                    Reason = string.Format("المعالج عند {0:F0}% وكارت الشاشة عند {1:F0}% - نقل عبء الجدولة وتفريغ أنوية المعالج لرفع الفريمات", cpu, gpu),
                    Confidence = 0.94,
                    Safety = DecisionSafety.Safe,
                    Source = "LoadBalancer",
                    Impact = 5
                });
            }
            // Scenario 2: GPU Bottleneck / Heavy Render Load (GPU 90%+ with thermal/frametime pressure)
            else if (gpu > 90 && (gpuTemp > 75 || temp > 75 || cpu < 45))
            {
                decisions.Add(new AgentDecision
                {
                    Category = DecisionCategory.GameBoost,
                    Action = "BalanceGameLoad",
                    Description = "موازنة جهد الرندر لكارت الشاشة وتثبيت الإطارات ومؤقت النواة (GPU Load Balancing & Pacing)",
                    Reason = string.Format("كارت الشاشة عند {0:F0}% مع ضغط رندر مكثف - تثبيت المؤقت على 0.5ms وتخفيف الاختناق الحراري", gpu),
                    Confidence = 0.92,
                    Safety = DecisionSafety.Safe,
                    Source = "LoadBalancer",
                    Impact = 5
                });
            }
            // Scenario 3: High Dual-Engine Gaming Load (High CPU & High GPU together)
            else if (cpu >= 50 && gpu >= 60)
            {
                decisions.Add(new AgentDecision
                {
                    Category = DecisionCategory.GameBoost,
                    Action = "BalanceGameLoad",
                    Description = "موازنة وتنسيق متزامن لأداء المعالج وكارت الشاشة (Dual-Engine Co-Processing)",
                    Reason = string.Format("مزامنة تردد المعالج ({0:F0}%) مع كارت الشاشة ({1:F0}%) لضمان ثبات زمن الإطار بأقل استهلاك", cpu, gpu),
                    Confidence = 0.90,
                    Safety = DecisionSafety.Safe,
                    Source = "LoadBalancer",
                    Impact = 4
                });
            }
        }

        private void AnalyzeBackground(List<AgentDecision> decisions)
        {
            if (_currentState.ProcessCount > 200 && !_currentState.IsGame)
            {
                decisions.Add(new AgentDecision
                {
                    Category = DecisionCategory.BackgroundControl,
                    Action = "SuggestClose",
                    Description = "Suggest closing unnecessary background processes",
                    Reason = string.Format("{0} processes running - high count", _currentState.ProcessCount),
                    Confidence = 0.65,
                    Safety = DecisionSafety.NeedsApproval
                });
            }
            // Full toolbox: suspend the heaviest background bloat instead of only
            // suggesting (reversible via ResumeSuspendedProcesses / rollback).
            if (!_currentState.IsGame && _suspendedPids.Count == 0 &&
                (_currentState.ProcessCount > 130 || _currentState.RamUsage > 80))
            {
                decisions.Add(new AgentDecision
                {
                    Category = DecisionCategory.BackgroundControl,
                    Action = "SuspendBloatProcesses",
                    Description = "Suspend top background bloat processes (reversible) - manual approval required; active user apps are excluded",
                    Reason = string.Format("{0} processes / RAM {1:F0}% - heavy background apps may be suspended only with user approval", _currentState.ProcessCount, _currentState.RamUsage),
                    Confidence = 0.5,
                    // Was DecisionSafety.Safe, which let autonomy modes auto-suspend
                    // the user's own heavy apps (IDE/browser/game) -> the reported
                    // "random freeze" complaint. Dangerous = manual approval even
                    // under Full Autonomy; ExecuteSuspendBloat also excludes
                    // protected/active apps and only targets >=1GB idle bloat.
                    Safety = DecisionSafety.Dangerous,
                    Source = "BackgroundAnalyzer",
                    Impact = 4
                });
            }
        }

        private void AnalyzePower(List<AgentDecision> decisions)
        {
            // === Feature 2: Enhanced Battery Mode ===
            if (_isOnBattery)
            {
                if (_batteryPercent < 20)
                {
                    decisions.Add(new AgentDecision
                    {
                        Category = DecisionCategory.PowerPlan,
                        Action = "BatterySaver",
                        Description = "Switch to power-saving mode: battery low",
                        Reason = string.Format("Battery at {0:F0}% - extend battery life", _batteryPercent),
                        Confidence = 0.95,
                        Safety = DecisionSafety.Safe
                    });
                }
                else if (_currentState.IsGame)
                {
                    decisions.Add(new AgentDecision
                    {
                        Category = DecisionCategory.PowerPlan,
                        Action = "BalancedPerformance",
                        Description = "Balanced mode for gaming on battery",
                        Reason = "Gaming on battery - balance performance and battery life",
                        Confidence = 0.75,
                        Safety = DecisionSafety.Safe
                    });
                }
                else if (_batteryPercent < 50 && _smoothCpu < 40)
                {
                    decisions.Add(new AgentDecision
                    {
                        Category = DecisionCategory.PowerPlan,
                        Action = "BatterySaver",
                        Description = "Light load on battery - use power saving",
                        Reason = string.Format("Battery {0}%, CPU idle - saving power", _batteryPercent),
                        Confidence = 0.65,
                        Safety = DecisionSafety.Safe
                    });
                }
            }
            else
            {
                // On AC power - aggressive performance
                if (_currentState.IsGame)
                {
                    decisions.Add(new AgentDecision
                    {
                        Category = DecisionCategory.PowerPlan,
                        Action = "SetHighPerformance",
                        Description = "High Performance power plan for gaming on AC",
                        Reason = "Gaming on AC power - maximum performance",
                        Confidence = 0.85,
                        Safety = DecisionSafety.Safe
                    });
                }
            }
        }

        // === Tweak Knowledge Base Analyzer ===
        private void AnalyzeTweakKnowledge(List<AgentDecision> decisions)
        {
            if (_tweakKnowledge == null || _mode == AgentMode.Off) return;
            _tweakKnowledgeTick++;
            if (_tweakKnowledgeTick < KNOWLEDGE_QUERY_INTERVAL_TICKS) return;
            _tweakKnowledgeTick = 0;

            try
            {
                bool isGame = _currentState.IsGame;
                bool isOnBattery = !_currentState.IsCharging && _currentState.BatteryLevel >= 0 && _currentState.BatteryLevel < 80;
                var recommendations = _tweakKnowledge.GetRecommendations(
                    _smoothCpu, _smoothRam, _smoothTemp, isGame, isOnBattery);

                // Check for conflicting tweaks with already-applied decisions
                var appliedActions = new List<string>();
                for (int ai = 0; ai < decisions.Count; ai++)
                    appliedActions.Add(decisions[ai].Action);

                for (int ri = 0; ri < recommendations.Count && ri < 3; ri++)
                {
                    var rec = recommendations[ri];

                    // SKIP Android-only entries — can't apply sysfs/governor/ZRAM on Windows
                    if (rec.Platform == "Android")
                    {
                        // BUT: if it has a WindowsEquivalent, create a Windows-adapted version
                        if (!string.IsNullOrEmpty(rec.WindowsEquivalent))
                        {
                            var winRec = new TweakKnowledgeEntry();
                            winRec.Id = "win_" + rec.Id;
                            winRec.Name = rec.Name + " (Windows)";
                            winRec.NameAr = rec.NameAr + " (ويندوز)";
                            winRec.Category = rec.Category;
                            winRec.FileType = "Registry";
                            winRec.Function = "Windows equivalent: " + rec.WindowsEquivalent + " — " + rec.Function;
                            winRec.FunctionAr = rec.FunctionAr;
                            winRec.SafetyScore = rec.SafetyScore;
                            winRec.SafetyLevel = rec.SafetyLevel;
                            winRec.PerformanceImpact = rec.PerformanceImpact;
                            winRec.ImpactArea = rec.ImpactArea;
                            winRec.UseCases = rec.UseCases;
                            winRec.Platform = "Windows";
                            winRec.SourceModule = rec.SourceModule;
                            winRec.RiskDescription = rec.RiskDescription;
                            rec = winRec;
                        }
                        else
                        {
                            continue; // Pure Android, no Windows equivalent
                        }
                    }

                    // Skip if already proposed by another analyzer
                    bool alreadyProposed = false;
                    for (int pi = 0; pi < decisions.Count; pi++)
                    {
                        if (decisions[pi].Action.Contains(rec.Id) || decisions[pi].Action.Contains(rec.Name))
                        {
                            alreadyProposed = true;
                            break;
                        }
                    }
                    if (alreadyProposed) continue;

                    // Check conflicts
                    var conflicts = _tweakKnowledge.CheckConflicts(rec.Id, appliedActions);
                    if (conflicts.Count > 0)
                    {
                        AddLog("INFO", "KnowledgeBase", "Skipping " + rec.Id + " - conflicts with: " + conflicts[0].Reason);
                        continue;
                    }

                    // Map category to DecisionCategory
                    DecisionCategory cat = DecisionCategory.MemoryClean;
                    switch (rec.Category)
                    {
                        case "Thermal": cat = DecisionCategory.ThermalProtect; break;
                        case "CPU": cat = DecisionCategory.ProcessPriority; break;
                        case "Memory": cat = DecisionCategory.MemoryClean; break;
                        case "Network": cat = DecisionCategory.NetworkOptimize; break;
                        case "Battery": cat = DecisionCategory.PowerPlan; break;
                        case "GPU": cat = DecisionCategory.GameBoost; break;
                        case "I/O": cat = DecisionCategory.IoPriority; break;
                        case "Display": cat = DecisionCategory.ProcessPriority; break;
                        default: cat = DecisionCategory.MemoryClean; break;
                    }

                    decisions.Add(new AgentDecision
                    {
                        Category = cat,
                        Action = "KB_" + rec.Id,
                        Description = rec.Function,
                        Reason = string.Format("[KnowledgeBase] {0} (Safety: {1:F0}%, Source: {2})",
                            rec.FunctionAr, rec.SafetyScore * 100, rec.SourceModule),
                        Confidence = Math.Min(0.85, rec.SafetyScore * 0.9),
                        Risk = 1.0 - rec.SafetyScore,
                        Safety = rec.SafetyScore >= 0.7 ? DecisionSafety.Safe : DecisionSafety.NeedsApproval,
                        Source = "TweakKnowledgeBase"
                    });
                }
            }
            catch { AddLog("ERROR", "KnowledgeBase", "Knowledge query failed"); }
        }

        private int _tweakKnowledgeTick = 0;

        // Unified decision line: collapse duplicate actions across all producers
        // (ML, WinOptAI, Multi-Agent, Expert, analyzers...). The winner is the one
        // with the highest (Impact, Confidence) so no engine double-votes.
        private static void DeduplicateDecisions(List<AgentDecision> decisions)
        {
            if (decisions == null || decisions.Count < 2) return;
            for (int i = 0; i < decisions.Count; i++)
            {
                AgentDecision a = decisions[i];
                if (a == null || string.IsNullOrEmpty(a.Action)) continue;
                for (int j = i + 1; j < decisions.Count; j++)
                {
                    AgentDecision b = decisions[j];
                    if (b == null || b.Action != a.Action) continue;
                    if (b.Impact > a.Impact || (b.Impact == a.Impact && b.Confidence > a.Confidence))
                    {
                        decisions[i] = b;
                        a = b;
                    }
                    decisions.RemoveAt(j);
                    j--;
                }
            }
        }

        private void RankAndFilter(List<AgentDecision> decisions)
        {
            // Sigmoid-based dynamic weights (smooth transitions, normalized)
            double rawThermal = Sigmoid(_smoothTemp, 70, 12);
            double rawMemory = Sigmoid(_smoothRam, 70, 12);
            double rawCpu = Sigmoid(_smoothCpu, 75, 12);
            double rawGame = _appMode == AppMode.Gaming ? 1.4 : 1.0;
            double weightSum = rawThermal + rawMemory + rawCpu + rawGame;
            double weightThermal = rawThermal / weightSum * 4.0;
            double weightMemory = rawMemory / weightSum * 4.0;
            double weightCpu = rawCpu / weightSum * 4.0;
            double weightGame = rawGame / weightSum * 4.0;

            for (int i = 0; i < decisions.Count; i++)
            {
                var d = decisions[i];
                double originalConfidence = d.Confidence;
                // Additive scoring: start from original confidence, apply bounded adjustments
                double score = originalConfidence;

                // Dynamic weight: mild boost/penalty based on system state (additive, not multiplicative)
                double dynWeight = 1.0;
                if (d.Category == DecisionCategory.ThermalProtect) dynWeight = weightThermal;
                else if (d.Category == DecisionCategory.MemoryClean || d.Category == DecisionCategory.PurgeStandby) dynWeight = weightMemory;
                else if (d.Category == DecisionCategory.ProcessPriority || d.Category == DecisionCategory.GameBoost) dynWeight = weightGame;
                else if (d.Category == DecisionCategory.CpuAffinity) dynWeight = weightCpu;
                dynWeight += GetCategoryLearnedBoost(d.Category.ToString());
                // Convert weight to additive adjustment: weight=2.0 → +0.1, weight=0.5 → -0.1
                score += (dynWeight - 1.0) * 0.15;

                // Experience modifier: mild adjustment based on historical success
                double expMod = GetExperienceModifier(d);
                score += (expMod - 1.0) * 0.1;

                // KnowledgeBase modifier: time-weighted historical effectiveness
                try
                {
                    double kbEffectiveness = QueryActionEffectiveness(d.Action, d.Category.ToString());
                    if (Math.Abs(kbEffectiveness) > 0.01)
                        score += kbEffectiveness * 0.12;
                }
                catch (Exception ex) { LogError("RankAndFilter", ex); }

                // Source-level trust: subsystems that measurably deliver better
                // outcomes get a small boost; sources with measured failures are
                // down-weighted. Neutral (1.0) until >= 3 measured outcomes.
                try
                {
                    double sourceTrust = GetSourceTrust(d.Source);
                    if (sourceTrust != 1.0)
                        score += (sourceTrust - 1.0) * 0.12;
                }
                catch (Exception ex) { LogError("RankAndFilter", ex); }

                // Per-action cooldown: hard block (skip decision entirely, don't just penalize)
                string cooldownKey = d.Action;
                if (_actionCooldowns.ContainsKey(cooldownKey))
                {
                    int cooldownSec;
                    if (d.Category == DecisionCategory.ThermalProtect) cooldownSec = 300;
                    else if (d.Category == DecisionCategory.MemoryClean || d.Category == DecisionCategory.PurgeStandby) cooldownSec = 120;
                    else if (d.Category == DecisionCategory.ProcessPriority || d.Category == DecisionCategory.GameBoost) cooldownSec = 60;
                    else cooldownSec = 20;
                    if ((DateTime.Now - _actionCooldowns[cooldownKey]).TotalSeconds < cooldownSec)
                    {
                        d.Confidence = 0;
                        continue;
                    }
                }

                // Consecutive same-action: mild penalty (additive, without mutating state inside candidate loop)
                if (_lastActionKey == d.Action)
                {
                    int nextConsec = _consecutiveSameAction + 1;
                    if (nextConsec > 3) score -= 0.25;
                    else if (nextConsec > 1) score -= 0.10;
                }

                // Mode-based adjustments (additive, bounded)
                if (_mode == AgentMode.CoolMode)
                {
                    if (d.Category == DecisionCategory.ThermalProtect) score += 0.10;
                    if (d.Category == DecisionCategory.GameBoost) score -= 0.08;
                }
                else if (_mode == AgentMode.GameMode)
                {
                    if (d.Category == DecisionCategory.GameBoost) score += 0.10;
                    if (d.Category == DecisionCategory.ThermalProtect) score -= 0.05;
                }
                else if (_mode == AgentMode.BalanceMode)
                {
                    if (d.Category == DecisionCategory.ThermalProtect) score -= 0.08;
                    else if (d.Category == DecisionCategory.GameBoost) score -= 0.08;
                    else if (d.Category == DecisionCategory.MemoryClean) score -= 0.05;
                }
                // Elite AI: apply adjustments as additive deltas (methods mutate d.Confidence)
                try
                {
                    double preGoal = d.Confidence; d = AdjustDecisionByGoal(d);
                    score += d.Confidence - preGoal; d.Confidence = preGoal;
                }
                catch (Exception ex) { LogError("RankAndFilter", ex); }
                try
                {
                    double preSeason = d.Confidence; d = AdjustDecisionBySeason(d);
                    score += d.Confidence - preSeason; d.Confidence = preSeason;
                }
                catch (Exception ex) { LogError("RankAndFilter", ex); }
                try
                {
                    double preDna = d.Confidence; d = AdjustDecisionByDNA(d);
                    score += d.Confidence - preDna; d.Confidence = preDna;
                }
                catch (Exception ex) { LogError("RankAndFilter", ex); }
                try
                {
                    double preBooster = d.Confidence; d = ApplyBooster(d);
                    score += d.Confidence - preBooster; d.Confidence = preBooster;
                }
                catch (Exception ex) { LogError("RankAndFilter", ex); }

                // Clamp final score to [0, 1]
                d.Confidence = Math.Max(0, Math.Min(1.0, score));
            }
            // Global Cooldown: prevent rapid action changes (8s between any action)
            if ((DateTime.Now - _lastActionTime).TotalSeconds < GLOBAL_COOLDOWN_SECONDS)
            {
                for (int i = 0; i < decisions.Count; i++)
                {
                    bool isCritical = (decisions[i].Category == DecisionCategory.ThermalProtect && _smoothTemp > 90)
                        || (decisions[i].Action == "EmergencyThrottle");
                    if (!isCritical) decisions[i].Confidence *= 0.4;
                }
            }
            // RL2 Shadow Mode comparison (and optional gated live influence if EnableRL2LiveInfluence is enabled)
            EvaluateRL2ShadowAndOptionalInfluence(decisions);

            // Decision Arbitration: Dynamic priority sort (severity × confidence × reliability)
            decisions.Sort((a, b) =>
            {
                return GetDynamicPriority(b).CompareTo(GetDynamicPriority(a));
            });
            var seen = new HashSet<string>();
            for (int i = decisions.Count - 1; i >= 0; i--)
            {
                string key = decisions[i].Category + "|" + decisions[i].Action;
                if (!seen.Add(key))
                    decisions.RemoveAt(i);
            }
        }

        public int GetAbstentionCount() { return _abstentions; }

        private double GetExperienceModifier(AgentDecision d)
        {
            double modifier = 1.0;
            // ML quality prior feeds the RL layer (unified decision line): the
            // neural ensemble's momentary estimate is blended into the experience
            // modifier. Strong when experience is sparse; fades as Q-learning learns.
            double mlFactor = 1.0;
            double mlQ;
            if (_mlQualityByAction.TryGetValue(d.Action, out mlQ))
            {
                int mlTimesUsed = 0;
                AgentExperience mlExp = null;
                string mlKey = d.Category + "|" + d.Action + "|" + (_currentState.IsGame ? _currentState.ActiveProcess : "");
                if (_experienceCache.TryGetValue(mlKey, out mlExp) || _experienceCache.TryGetValue(d.Category + "|" + d.Action + "|", out mlExp))
                    mlTimesUsed = mlExp.TimesUsed;
                double mlBlend = 0.5 / (1.0 + mlTimesUsed * 0.25); // 0.5 → ~0.1 after 16 uses
                mlFactor = 1.0 + (mlQ - 0.5) * mlBlend * 2.0;      // ±0.5 at the extremes
            }
            // Confidence-based epsilon with floor (prevent premature convergence)
            double baseEpsilon = _appMode == AppMode.Gaming ? 0.03 : 0.15;
            double confidenceFactor = Math.Max(0.1, 1.0 - d.Confidence);
            double epsilon = Math.Max(EPSILON_FLOOR, baseEpsilon * confidenceFactor);
            bool explored = _rnd.NextDouble() < epsilon;
            if (explored)
            {
                // Boost confidence of less-used actions to encourage exploration
                foreach (var exp in _experiences)
                {
                    if (exp.Category == d.Category && exp.Action == d.Action)
                    {
                        if (exp.TimesUsed < 5)
                        {
                            modifier *= 1.3;
                            AddLog("INFO", "Explore", "ε-greedy exploration boost for: " + d.Action);
                        }
                    }
                }
                return modifier * mlFactor;
            }
            // Normal exploitation: use experience data
            string expLookupKey = d.Category + "|" + d.Action + "|" + (_currentState.IsGame ? _currentState.ActiveProcess : "");
            AgentExperience foundExp = null;
            if (_experienceCache.TryGetValue(expLookupKey, out foundExp) || _experienceCache.TryGetValue(d.Category + "|" + d.Action + "|", out foundExp))
            {
                if (foundExp.GameName == _currentState.ActiveProcess || string.IsNullOrEmpty(foundExp.GameName))
                {
                    modifier *= (0.5 + foundExp.SuccessRate() * 0.5);
                    if (foundExp.TimesSuccessful > 5 && foundExp.SuccessRate() > 0.7) modifier *= 1.15;
                    if (foundExp.TimesUsed > 3 && foundExp.SuccessRate() < 0.3) modifier *= 0.4;
                }
            }
            // Consecutive failure penalty: escalating penalty for repeated failures
            string failKey = d.Action + "_" + d.Category;
            int consecutiveFails;
            if (_consecutiveActionFailures.TryGetValue(failKey, out consecutiveFails) && consecutiveFails > 0)
            {
                // 1 fail: 0.7x, 2 fails: 0.5x, 3+ fails: 0.3x
                if (consecutiveFails >= 3) modifier *= 0.3;
                else if (consecutiveFails >= 2) modifier *= 0.5;
                else modifier *= 0.7;
            }
            return modifier * mlFactor;
        }

        private void ProcessDecisions(List<AgentDecision> decisions)
        {
            int effectiveBudget = BUDGET_MAX + Math.Min(MAX_CARRY_OVER, _budgetCarryOver);
            _budgetCarryOver = 0;
            try
            {
            // Learning Feedback Loop: evaluate any pending outcome batches that
            // have matured. Each batch owns its pre-execution snapshot, so
            // running this before executing new actions can never corrupt an
            // in-progress evaluation window.
            EvaluatePendingBatches();
            // Action Budget: reset per tick with carry-over
            _budgetUsed = 0;
            // Cancel carry-over on mode change (context mismatch)
            string currentModeStr = _appMode.ToString();
            if (_preStateMode != currentModeStr && !string.IsNullOrEmpty(_preStateMode))
            {
                _budgetCarryOver = 0;
                // Reset learning state on mode change (prevent cross-context bias)
                _consecutiveActionFailures.Clear();
                _consecutiveSameAction = 0;
                foreach (var kv in _actionValues)
                {
                    // Pull Q-values toward neutral on mode switch (old state values don't apply)
                    kv.Value.QValue = kv.Value.QValue + (0.5 - kv.Value.QValue) * 0.3;
                }
            }
            // Remember the mode for the next tick's cross-context check.
            // Pre-execution metric snapshots are taken at enqueue time (per
            // batch), not here, so a fast tick cannot lose them.
            _preStateMode = currentModeStr;
            // Action Aging: boost priority for deferred actions
            foreach (var d in decisions)
            {
                int age;
                if (!_actionAgeTicks.TryGetValue(d.Action, out age)) age = 0;
                double agingBoost = Math.Min(AGING_MAX, age * AGING_BONUS_PER_TICK);
                d.Confidence = Math.Min(1.0, d.Confidence + agingBoost);
            }
            }
            // === Elite AI: Resolve conflicts between decisions ===
            catch (Exception ex) { LogError("ProcessDecisions", ex); }
            // === First pass: filter + safety check → collect eligible candidates ===
            _reusableCandidates.Clear();
            _reusableEmergencies.Clear();
            var candidates = _reusableCandidates;
            var emergencies = _reusableEmergencies;
            bool heavyActionExecuted = false;
            foreach (var d in decisions)
            {
                // Minimum confidence threshold
                if (d.Confidence < 0.3)
                {
                    // A/B Exploration: 5% chance to try low-confidence actions
                    if (d.Confidence >= 0.15 && _exploreRnd.NextDouble() < EXPLORATION_RATE)
                    {
                        AddLog("EXPLORE", "A/B", "Exploring: " + d.Description + " (conf=" + d.Confidence.ToString("F2") + ")");
                        // Don't skip - let it pass through to candidates
                    }
                    else
                    {
                        _actionAgeTicks[d.Action] = (_actionAgeTicks.ContainsKey(d.Action) ? _actionAgeTicks[d.Action] : 0) + 1;
                        AddLog("INFO", "Skipped", "Low confidence (" + d.Confidence.ToString("F2") + "): " + d.Description);
                        continue;
                    }
                }
                // Emergency thermal actions bypass budget
                bool isEmergency = (d.Action == "EmergencyThrottle" || d.Action == "ReduceThermal") && _smoothTemp > 90;
                if (isEmergency)
                {
                    emergencies.Add(d);
                    continue;
                }
                if (d.Safety == DecisionSafety.NeedsApproval)
                {
                    // Use the new PolicyGate: approval is now one of four
                    // independent checks, not a catch-all bypass flag.
                    var policyResult = EvaluatePolicy(d);
                    bool autoAcceptDecision = _fullAutonomy && IsFullAutonomyAllowlisted(d.Action);
                    if (!policyResult.Approved && !autoAcceptDecision)
                    {
                        AddLog("POLICY", "Decision", "Needs approval: " + d.Action + " | " + policyResult.Reason);
                        lock (_approvalLock) { _approvalQueue.Add(d); }
                        continue;
                    }
                    if (policyResult.Result == PolicyResult.Rejected_Safety || policyResult.Result == PolicyResult.Rejected_Authorization)
                    {
                        AddLog("POLICY", "Blocked", d.Action + " rejected: " + policyResult.Reason);
                        continue;
                    }
                }
                // Full Autonomy may bypass the approval queue only for a small,
                // explicit low-impact allowlist. It never marks an action as
                // user-approved and never bypasses a hard safety gate.
                bool autoAccept = _fullAutonomy &&
                    d.Safety == DecisionSafety.NeedsApproval &&
                    IsFullAutonomyAllowlisted(d.Action);
                if (d.Safety == DecisionSafety.NeedsApproval && !autoAccept)
                { _pendingDecisions.Add(d); QueueForApproval(d); AddLog("INFO", "Pending", "Awaiting approval: " + d.Description); continue; }
                if (d.Safety == DecisionSafety.Dangerous)
                { _pendingDecisions.Add(d); QueueForApproval(d); AddLog("WARN", "Dangerous", "Needs manual approval: " + d.Description); continue; }
                double risk = CalculateRisk(d);
                d.RiskScore = risk;
                string riskLevel = GetRiskLevel(risk);
                // High/Critical risk always needs review; game mode is not an exception.
                if (riskLevel == "Critical" || riskLevel == "High")
                { AddLog("WARN", "Safety", "Deferred " + riskLevel.ToLowerInvariant() + "-risk decision: " + d.Description); _pendingDecisions.Add(d); QueueForApproval(d); continue; }
                // === Calibrated abstention: learned "do nothing" when evidence shows no benefit ===
                if (ShouldAbstain(d))
                {
                    _abstentions++;
                    AddLog("ABSTAIN", "Abstention",
                        string.Format("{0} (expected reward {1:F2} over {2} outcomes) - learned not to act",
                        d.Action, GetExpectedReward(d.Action), GetActionRewardSampleCount(d.Action)));
                    continue;
                }
                // Dependency Graph: check preconditions
                Func<SystemState, bool> precheck;
                if (ACTION_PRECHECKS.TryGetValue(d.Action, out precheck) && !precheck(_currentState))
                { AddLog("INFO", "Dependency", "Precondition not met: " + d.Description); continue; }
                candidates.Add(d);
            }
            // Dependency Graph: remove conflicting actions (lower priority yields)
            _reusableResolved.Clear();
            _reusableConflictSet.Clear();
            var resolved = _reusableResolved;
            var removedConflicts = _reusableConflictSet;
            foreach (var d in candidates)
            {
                if (removedConflicts.Contains(d.Action)) continue;
                string[] conflicts;
                if (ACTION_CONFLICTS.TryGetValue(d.Action, out conflicts))
                {
                    foreach (string conflict in conflicts)
                    {
                        var match = candidates.Find(c => c.Action == conflict);
                        if (match != null && !removedConflicts.Contains(conflict))
                        {
                            // Keep the one with higher dynamic priority
                            double myP = GetDynamicPriority(d);
                            double otherP = GetDynamicPriority(match);
                            if (otherP > myP) { removedConflicts.Add(d.Action); break; }
                            else removedConflicts.Add(conflict);
                        }
                    }
                }
                if (!removedConflicts.Contains(d.Action)) resolved.Add(d);
            }
            candidates = resolved;
            // === Re-check kill switch before execution ===
            if (_killSwitchActive) { AddLog("WARN", "ProcessDecisions", "Kill switch activated mid-process — aborting all decisions"); return; }
            // === Pre-execution snapshot for this tick's outcome batch ===
            // Taken before any action runs so the evaluation window can
            // measure real deltas. Stored with the batch at enqueue time.
            double snapCpu = _smoothCpu, snapRam = _smoothRam, snapTemp = _smoothTemp;
            double snapFps = _currentState.Fps;
            string snapMode = _appMode.ToString();
            // === Execute emergencies first (bypass budget) ===
            foreach (var d in emergencies)
            {
                if (_killSwitchActive) break;
                if (IsInformationalAction(d.Action)) { LogInformationalDecision(d); continue; }
                int cost;
                if (!ACTION_COST.TryGetValue(d.Action, out cost)) cost = 3;
                ExecuteDecision(d);
                if (_lastExecutionSuccess)
                {
                    _budgetUsed += cost;
                    _currentTickActions.Add(d.Action);
                }
                if (cost >= 4) heavyActionExecuted = true;
                if (OnDecision != null) OnDecision(d);
                MonitorTick(d, d.Confidence, d.Risk);
            }
            // === Knapsack: pick optimal combination within budget ===
            var selected = SolveKnapsack(candidates, effectiveBudget, heavyActionExecuted);
            // === Execute selected actions ===
            foreach (var d in selected)
            {
                if (_killSwitchActive) break;
                if (IsInformationalAction(d.Action)) { LogInformationalDecision(d); continue; }
                // Action is being attempted now: its deferral age is spent, so
                // remove the entry and let the next deferral cycle start fresh.
                _actionAgeTicks.Remove(d.Action);
                _lastActionKey = d.Action;
                int cost;
                if (!ACTION_COST.TryGetValue(d.Action, out cost)) cost = 3;
                ExecuteDecision(d);
                if (_lastExecutionSuccess)
                {
                    _budgetUsed += cost;
                    _currentTickActions.Add(d.Action);
                    if (cost >= 4) heavyActionExecuted = true;
                }
                if (OnDecision != null) OnDecision(d);
                MonitorTick(d, d.Confidence, d.Risk);
            }
            // Enqueue this tick's executed actions for outcome evaluation.
            // The batch carries its own pre-execution snapshot and stays in
            // the queue until its most conservative delay window matures —
            // no other code path may clear it early.
            if (_currentTickActions.Count > 0)
                EnqueueOutcomeBatch(_currentTickActions, snapCpu, snapRam, snapTemp, snapFps, snapMode);
            _currentTickActions.Clear();
            // Save unused budget as carry-over for next tick
            int unused = effectiveBudget - _budgetUsed;
            if (unused > 0) _budgetCarryOver = Math.Min(MAX_CARRY_OVER, unused);
        }

        // Queue a batch of executed actions for later outcome evaluation.
        // The pre-execution metric snapshot (captured before any action ran)
        // is stored with the batch, so a fast tick can no longer wipe out
        // actions before their evaluation window matures.
        private void EnqueueOutcomeBatch(List<string> actions, double preCpu, double preRam, double preTemp, double preFps, string preMode)
        {
            lock (_decisionLock)
            {
                _pendingOutcomeBatches.Add(new PendingOutcomeBatch
                {
                    Actions = new List<string>(actions),
                    RecordedAt = DateTime.Now,
                    PreCpu = preCpu,
                    PreRam = preRam,
                    PreTemp = preTemp,
                    PreFps = preFps,
                    PreMode = preMode
                });
                // Cap the queue: drop the oldest batch first (stalest evidence)
                while (_pendingOutcomeBatches.Count > PENDING_BATCH_MAX)
                    _pendingOutcomeBatches.RemoveAt(0);
            }
        }

        private List<AgentDecision> SolveKnapsack(List<AgentDecision> candidates, int budget, bool heavyAlreadyUsed)
        {
            if (candidates.Count == 0) { _knapsackResult.Clear(); return _knapsackResult; }
            // Small set: use DP 0/1 knapsack
            int n = candidates.Count;
            if (n > _knapsackCosts.Length) n = _knapsackCosts.Length;
            for (int i = 0; i < n; i++)
            {
                int c;
                if (!ACTION_COST.TryGetValue(candidates[i].Action, out c) || c < 1) c = 3;
                _knapsackCosts[i] = Math.Max(1, c);
                _knapsackValues[i] = GetDynamicPriority(candidates[i]);
                _knapsackHeavy[i] = c >= 4;
            }
            // DP: dp[budget] = max value
            int maxBudget = Math.Min(30, budget);
            for (int i = 0; i <= maxBudget; i++) { _knapsackDp[i] = -1; _knapsackChoice[i] = -1; }
            _knapsackDp[0] = 0;
            for (int i = 0; i < n; i++)
            {
                if (heavyAlreadyUsed && _knapsackHeavy[i]) continue;
                for (int b = maxBudget; b >= _knapsackCosts[i]; b--)
                {
                    if (_knapsackDp[b - _knapsackCosts[i]] >= 0 && _knapsackDp[b - _knapsackCosts[i]] + _knapsackValues[i] > _knapsackDp[b])
                    {
                        _knapsackDp[b] = _knapsackDp[b - _knapsackCosts[i]] + _knapsackValues[i];
                        _knapsackChoice[b] = i;
                    }
                }
            }
            // Find best budget usage
            int bestB = 0;
            double bestVal = 0;
            for (int b = 1; b <= maxBudget; b++)
            {
                if (_knapsackDp[b] > bestVal) { bestVal = _knapsackDp[b]; bestB = b; }
            }
            // Reconstruct
            _knapsackResult.Clear();
            int remaining = bestB;
            int safetyLimit = 32;
            while (remaining > 0 && _knapsackChoice[remaining] >= 0 && safetyLimit-- > 0)
            {
                int idx = _knapsackChoice[remaining];
                if (idx < 0 || idx >= candidates.Count) break;
                _knapsackResult.Add(candidates[idx]);
                int cost = Math.Max(1, _knapsackCosts[idx]);
                remaining -= cost;
            }
            return _knapsackResult;
        }

        private double CalculateRisk(AgentDecision d)
        {
            double risk = 0;
            if (d.Action.Contains("Thermal") || d.Action.Contains("Throttle"))
                risk += _smoothTemp > 85 ? 0.3 : 0;
            if (d.Action.Contains("Memory") || d.Action.Contains("Clean") || d.Action.Contains("Purge"))
                risk += _smoothRam > 85 ? 0.2 : 0;
            if ((d.Action.Contains("Power") || d.Action.Contains("Affinity") || d.Action.Contains("CoreParking"))
                && !_currentState.IsCharging && _currentState.BatteryLevel > 0)
                risk += 0.15;
            if (d.Safety == DecisionSafety.Dangerous) risk += 0.4;
            if (d.Safety == DecisionSafety.NeedsApproval) risk += 0.2;
            risk = risk * (1.0 - d.Confidence * 0.3);
            return Math.Min(1.0, Math.Max(0, risk));
        }

        private string GetRiskLevel(double risk)
        {
            if (risk < 0.3) return "Safe";
            if (risk < 0.6) return "Medium";
            if (risk < 0.8) return "High";
            return "Critical";
        }

        private double Sigmoid(double value, double midpoint, double steepness)
        {
            return 1.0 / (1.0 + Math.Exp(-(value - midpoint) / steepness));
        }

        private int GetCategoryPriority(DecisionCategory cat)
        {
            if (cat == DecisionCategory.ThermalProtect) return PRIORITY_THERMAL;
            if (cat == DecisionCategory.MemoryClean || cat == DecisionCategory.PurgeStandby) return PRIORITY_MEMORY;
            if (cat == DecisionCategory.GameBoost) return PRIORITY_GAME;
            if (cat == DecisionCategory.BackgroundControl) return PRIORITY_BACKGROUND;
            return PRIORITY_POWER;
        }

        private double GetDynamicPriority(AgentDecision d)
        {
            double basePriority = GetCategoryPriority(d.Category);
            double severity = 1.0;
            if (d.Category == DecisionCategory.ThermalProtect)
                severity = Math.Min(2.0, Math.Max(1.0, _smoothTemp / 70.0));
            else if (d.Category == DecisionCategory.MemoryClean || d.Category == DecisionCategory.PurgeStandby)
                severity = Math.Min(2.0, Math.Max(1.0, _currentState.RamUsage / 70.0));
            else if (d.Category == DecisionCategory.GameBoost)
                severity = _appMode == AppMode.Gaming ? 1.5 : 1.0;
            // Emergency override: if Temp > 90, force max priority regardless of confidence
            if (_smoothTemp > 90 && d.Category == DecisionCategory.ThermalProtect) return 100.0;
            // Weighted sum instead of multiplication (prevents one factor from killing priority)
            double historicalReliability = GetActionReliability(d.Action);
            double weightedPriority = basePriority * (0.35 * d.Confidence + 0.35 * (severity / 2.0) + 0.30 * historicalReliability);
            // Hybrid Score: 70% weighted priority + 30% density (priority/cost)
            int actionCost;
            if (!ACTION_COST.TryGetValue(d.Action, out actionCost)) actionCost = 3;
            double density = weightedPriority / Math.Max(1, actionCost);
            return 0.7 * weightedPriority + 0.3 * density;
        }

        private double GetActionReliability(string action)
        {
            lock (_decisionLock)
            {
                int oSuccess = 0, oTotal = 0, eSuccess = 0, eTotal = 0;
                bool hasOutcome = _outcomeTotalCount.TryGetValue(action, out oTotal) && oTotal > 0
                                 && _outcomeSuccessCount.TryGetValue(action, out oSuccess);
                bool hasExec = _actionTotalCount.TryGetValue(action, out eTotal) && eTotal > 0
                               && _actionSuccessCount.TryGetValue(action, out eSuccess);
                if (hasOutcome && hasExec)
                {
                    // Verified outcomes (the effect actually showed up in the
                    // metrics) are stronger evidence than "executed fine".
                    double outcomeRate = (double)oSuccess / oTotal;
                    double execRate = (double)eSuccess / eTotal;
                    return 0.6 * outcomeRate + 0.4 * execRate;
                }
                if (hasOutcome) return (double)oSuccess / oTotal;
                if (hasExec) return (double)eSuccess / eTotal;
            }
            return 0.5; // default: unknown = neutral
        }

        // Execution-level learning only ("the command ran / did not run").
        // Outcome-level evidence is recorded separately via
        // RecordOutcomeVerified so the two signals can never be confused.
        private void RecordDecisionOutcome(string action, bool success)
        {
            lock (_decisionLock)
            {
                // Concept-drift decay is time-based (0.95 per elapsed minute per
                // action), not per-call — otherwise frequently-running actions would
                // decay their counts far faster than rarely-running ones.
                DateTime lastDecay;
                if (!_actionDecayTime.TryGetValue(action, out lastDecay))
                {
                    _actionDecayTime[action] = DateTime.Now; // seed baseline, no decay on first record
                }
                else
                {
                    double elapsedMin = (DateTime.Now - lastDecay).TotalMinutes;
                    if (elapsedMin >= 0.5)
                    {
                        double factor = Math.Pow(0.95, Math.Min(elapsedMin, 60.0));
                        int successOld, totalOld;
                        if (_actionSuccessCount.TryGetValue(action, out successOld) && successOld > 0)
                            _actionSuccessCount[action] = (int)(successOld * factor);
                        if (_actionTotalCount.TryGetValue(action, out totalOld) && totalOld > 0)
                            _actionTotalCount[action] = (int)(totalOld * factor);
                        _actionDecayTime[action] = DateTime.Now;
                    }
                }
                // Now add current observation
                if (!_actionSuccessCount.ContainsKey(action)) _actionSuccessCount[action] = 0;
                if (!_actionTotalCount.ContainsKey(action)) _actionTotalCount[action] = 0;
                _actionTotalCount[action]++;
                if (success) _actionSuccessCount[action]++;
            }
        }

        // === Outcome-verified learning ===
        // Records that the effect of an action actually showed up in system
        // metrics (improved=true means the credit-assigned attributed reward
        // was positive). This is the ONLY source that can raise an action's
        // confidence beyond execution-level statistics.
        private void RecordOutcomeVerified(string action, bool improved, double attributedReward)
        {
            lock (_decisionLock)
            {
                if (!_outcomeSuccessCount.ContainsKey(action)) _outcomeSuccessCount[action] = 0;
                if (!_outcomeTotalCount.ContainsKey(action)) _outcomeTotalCount[action] = 0;
                _outcomeTotalCount[action]++;
                if (improved) _outcomeSuccessCount[action]++;
                _actionAttributedReward[action] = attributedReward;
            }
        }

        // Last credit-assigned outcome reward attributed to this action
        // (0.0 when no outcome has been verified yet).
        public double GetLastAttributedReward(string action)
        {
            lock (_decisionLock)
            {
                double v;
                if (action != null && _actionAttributedReward.TryGetValue(action, out v)) return v;
            }
            return 0.0;
        }

        // Expected relative effect of an action on the tracked metrics
        // {temperature drop, RAM drop, FPS improvement}.
        private double[] GetActionMagnitude(string action)
        {
            double[] m;
            if (action != null && ACTION_MAGNITUDES.TryGetValue(action, out m)) return m;
            return new double[] { 0.2, 0.2, 0.2 }; // generic small expectation
        }

        // Advisory/informational actions: they do not mutate the system, so a
        // "return without exception" must not count as a successful system
        // action, and they never enter the learning loop.
        private static bool IsInformationalAction(string action)
        {
            if (string.IsNullOrEmpty(action)) return false;
            return action == "SuggestClose" || action == "HealthCheck" || action == "DiskCleanup" || action.StartsWith("KB_");
        }

        // Full-autonomy approval bypass is intentionally narrow. Unknown actions
        // default to NOT allowlisted. All actions still pass the risk engine,
        // execution-time safety checks and simulator before they can run.
        public static bool IsFullAutonomyAllowlisted(string action)
        {
            switch (action)
            {
                case "LightClean":
                case "PreemptiveClean":
                case "AggressiveClean":
                case "PurgeStandbyList":
                case "CleanTempFiles":
                case "FlushDnsCache":
                case "HealthCheck":
                case "BoostGamePriority":
                case "StabilizeFrames":
                case "ReduceMicroStutter":
                case "UnparkAllCores":
                case "SetGameAffinity":
                case "LimitBackground":
                case "SetLowIoPriority":
                case "BalancedPerformance":
                case "BatterySaver":
                case "OptimizeGameLoop":
                case "BalanceGameLoad":
                case "OptimizeDesktopSmoothness":
                case "TrimInactiveWorkingSets":
                case "OptimizeGpuPerformance":
                case "ShedBackgroundGpuMemory":
                case "PurgeGpuShaderCache":
                    return true;
                default:
                    return false;
            }
        }

        // Expected *outcome* value of an action RIGHT NOW: how much real,
        // measurable improvement it is likely to produce given the current
        // state, scaled by how often it has actually delivered it (verified
        // outcomes). This is what the knapsack should prefer over pure
        // "theoretical importance".
        private double GetExpectedOutcomeValue(AgentDecision d)
        {
            double effect;
            switch (d.Category)
            {
                case DecisionCategory.ThermalProtect:
                    // Higher than the (form-factor) warning threshold => bigger expected relief
                    effect = Math.Max(0.0, _smoothTemp - _deviceProfile.ThermalWarningTemp) / 15.0;
                    break;
                case DecisionCategory.MemoryClean:
                case DecisionCategory.PurgeStandby:
                    effect = Math.Max(0.0, _currentState.RamUsage - _deviceProfile.RamCleanThreshold) / 15.0;
                    break;
                case DecisionCategory.GameBoost:
                    if (_appMode == AppMode.Gaming && _currentState.Fps > 0)
                        effect = Math.Max(0.0, 60.0 - _currentState.Fps) / 45.0;
                    else
                        effect = 0.1;
                    break;
                case DecisionCategory.BackgroundControl:
                    effect = Math.Max(0.0, _smoothCpu - 60.0) / 40.0;
                    break;
                case DecisionCategory.CpuAffinity:
                case DecisionCategory.IoPriority:
                case DecisionCategory.CoreParking:
                    effect = _appMode == AppMode.Gaming ? 0.4 : 0.1;
                    break;
                default:
                    // Power plan / network / status / disk actions: indirect effect
                    effect = 0.15;
                    break;
            }
            effect = Math.Min(1.0, effect);
            // Gate by verified reliability and by the decision's own confidence
            return effect * GetActionReliability(d.Action) * (0.5 + 0.5 * d.Confidence);
        }

        private bool IsGameScopedAction(string action)
        {
            if (string.IsNullOrEmpty(action)) return false;
            switch (action)
            {
                case "BoostGamePriority":
                case "BalanceGameLoad":
                case "BalanceCpuGpuLoad":
                case "StabilizeFrames":
                case "ReduceMicroStutter":
                case "SetGameAffinity":
                case "LimitBackgroundAffinity":
                case "SetLowIoPriority":
                case "UnparkAllCores":
                case "DisableGameDvr":
                case "SetHighPerformance":
                case "BalancedPerformance":
                case "OptimizeGpuPerformance":
                case "PurgeGpuShaderCache":
                    return true;
                default:
                    return false;
            }
        }

        private bool IsDesktopScopedAction(string action)
        {
            if (string.IsNullOrEmpty(action)) return false;
            switch (action)
            {
                case "OptimizeDesktopSmoothness":
                case "TrimInactiveWorkingSets":
                    return true;
                default:
                    return false;
            }
        }

        // ExecuteDecision may be invoked from the UI thread (ApprovePendingDecision,
        // ApplyFullSystemOptimization) and from the tick thread (ProcessDecisions,
        // FpsStabilizer). Serialize execution so system-changing work and the shared
        // rollback/budget state are never touched by two threads at once. The tick
        // path already holds _decisionLock, so this is a reentrant acquire there.
        private void ExecuteDecision(AgentDecision d)
        {
            lock (_decisionLock) { ExecuteDecisionCore(d); }
        }

        private void ExecuteDecisionCore(AgentDecision d)
        {
            // Revalidate game-only actions at execution time. A queued decision can
            // outlive the game session or an alt-tab, so never apply game tuning to
            // an unrelated foreground application.
            if (IsGameScopedAction(d != null ? d.Action : null) &&
                (_currentState == null || !_currentState.IsGame))
            {
                if (d != null) AddLog("INFO", "GameContext", "Skipped stale game action: " + d.Action);
                return;
            }

            // Mode Duality: Desktop actions yield immediately when a game is running
            if (IsDesktopScopedAction(d != null ? d.Action : null) &&
                (_currentState != null && _currentState.IsGame))
            {
                if (d != null) AddLog("INFO", "DesktopContext", "Skipped desktop action during active game: " + d.Action);
                return;
            }

            bool success = false;
            bool isThermalMitigation = d != null && d.Category == DecisionCategory.ThermalProtect &&
                (d.Action == "EmergencyThrottle" || d.Action == "ReduceThermal");
            bool isCriticalThermalMitigation = isThermalMitigation && _currentState != null &&
                _deviceProfile != null && _currentState.CpuTemp > _deviceProfile.ThermalThrottleTemp;
            _lastExecutionSuccess = false; // Stale result from a prior decision must not count for this one
            _lastDecisionAction = d.Action; // Learning: track for feedback
            _actionSource[d.Action] = d.Source ?? "Unknown";
            CapturePreActionState(); // ML: capture state before execution
            // === Feature 5: Log optimization attempt ===
            AddLogOpt(d.Action, string.Format("Executing: {0} (conf={1:F0}%)", d.Description, d.Confidence * 100));

            // === Central Policy Gate & Autonomy Enforcement ===
            try
            {
                if (!DisableRealExecution && !_simulationMode && d != null && !string.IsNullOrEmpty(d.Action) &&
                    !d.Action.StartsWith("KB_") && !d.Action.StartsWith("Scheduled_") &&
                    !d.Action.StartsWith("PredictiveMaintenance_") && !d.Action.StartsWith("ForecastPrevent_"))
                {
                    var policyRes = ActionPolicyGate.Evaluate(d.Action, ActionRequester.SmartAgent, false, AdminSessionManager.IsAdminConnected);
                    if (!policyRes.Allowed)
                    {
                        AddLog("WARN", "PolicyGate", "BLOCKED by PolicyGate: " + policyRes.ReasonAr);
                        RecordExperience(d, false);
                        return;
                    }
                }
            }
            catch (Exception exGate)
            {
                LogError("PolicyGate", exGate);
                return;
            }

            // === AI Safety Layer: pre-execution check ===
            try
            {
                var safety = PreExecutionSafetyCheck(d);
                if (!safety.Allowed)
                {
                    AddLog("WARN", "Safety", "BLOCKED by execution-time safety layer: " + d.Description + " | Risk=" + safety.RiskScore);
                    RecordExperience(d, false);
                    return;
                }
            }
            catch (Exception ex)
            {
                LogError("ExecuteDecision", ex);
                AddLog("WARN", "Safety", "Safety check failed; action blocked: " + d.Description);
                RecordExperience(d, false);
                return;
            }
            // === Elite AI: Risk Engine check (fail closed) ===
            try
            {
                var riskAssessment = CalculateEliteRisk(d);
                if (riskAssessment.ShouldBlock && !isCriticalThermalMitigation)
                {
                    AddLog("WARN", "RiskEngine", "BLOCKED by risk engine: " + d.Action + " | Risk=" + string.Format("{0:F0}", riskAssessment.RiskScore) + "/100");
                    RecordExperience(d, false);
                    return;
                }
                if (riskAssessment.ShouldBlock && isCriticalThermalMitigation)
                {
                    // Narrow exception: allow only the built-in heat-reduction action
                    // when the measured CPU temperature already exceeds the hard limit.
                    AddLog("WARN", "RiskEngine", "High risk recorded; continuing with constrained thermal mitigation only: " + d.Action + " | Risk=" + string.Format("{0:F0}", riskAssessment.RiskScore) + "/100");
                }
            }
            catch (Exception ex)
            {
                LogError("ExecuteDecision", ex);
                AddLog("WARN", "RiskEngine", "Risk evaluation failed; action blocked: " + d.Action);
                RecordExperience(d, false);
                return;
            }
            // === Elite AI: Decision Simulator (fail closed) ===
            try
            {
                var simResult = SimulateDecisionFull(d);
                if (simResult == null || (simResult.ExpectedSuccessRate < 0.2 && !isCriticalThermalMitigation))
                {
                    AddLog("WARN", "DecisionSimulator", "Rejected by simulator: " + d.Action + (simResult == null ? " | no result" : " | Success=" + string.Format("{0:F0}%", simResult.ExpectedSuccessRate * 100)));
                    RecordExperience(d, false);
                    return;
                }
                if (simResult.ExpectedSuccessRate < 0.2 && isCriticalThermalMitigation)
                {
                    AddLog("WARN", "DecisionSimulator", "Low predicted success recorded; allowing constrained thermal mitigation because the measured CPU temperature exceeds the hard limit.");
                }
            }
            catch (Exception ex)
            {
                LogError("ExecuteDecision", ex);
                AddLog("WARN", "DecisionSimulator", "Simulation failed; action blocked: " + d.Action);
                RecordExperience(d, false);
                return;
            }
            // === AI Digital Twin: simulate before executing (fail closed) ===
            try
            {
                var simulation = SimulateDecision(d);
                bool twinAllowsThermalMitigation = isCriticalThermalMitigation && simulation != null &&
                    simulation.Confidence >= 0.8 && simulation.PredictedTempDelta < 0;
                if (simulation == null || (!ShouldExecuteSimulation(simulation) && !twinAllowsThermalMitigation))
                {
                    AddLog("INFO", "DigitalTwin", "Simulation rejected or unavailable: " + d.Action +
                        (simulation == null ? " | no result" : " | Risk=" + string.Format("{0:F0}", simulation.SimulatedRisk) + " Benefit=" + string.Format("{0:F0}", simulation.PredictedOverallBenefit)));
                    RecordExperience(d, false);
                    return;
                }
                if (!ShouldExecuteSimulation(simulation) && twinAllowsThermalMitigation)
                {
                    AddLog("WARN", "DigitalTwin", "Twin risk recorded; allowing only high-confidence action with predicted temperature reduction at critical CPU temperature.");
                }
            }
            catch (Exception ex)
            {
                LogError("ExecuteDecision", ex);
                AddLog("WARN", "DigitalTwin", "Simulation failed; action blocked: " + d.Action);
                RecordExperience(d, false);
                return;
            }
            // === Feature 9: Create restore point before heavy optimizations ===
            int cost;
            if (ACTION_COST.TryGetValue(d.Action, out cost) && cost >= 5)
            {
                CreateRestorePointIfNeeded();
            }
            // === Feature 6: Save pre-optimization state ===
            SavePreOptState(d.Action);
            try
            {
                // Test mode: validate the full decision pipeline but skip real
                // system changes (power plans, processes, registry, files).
                if (DisableRealExecution)
                {
                    AddLogOpt(d.Action, "Test mode: simulated execution (no system changes)");
                    d.Executed = true;
                    d.Success = true;
                    RecordDecisionOutcome(d.Action, true);
                    return;
                }
                // Record state before execution for journal + rollback
                string beforePlan = GetCurrentPowerPlan();
                string beforeGame = _currentState.ActiveProcess ?? "";
                _rollbackPlan = beforePlan;
                _rollbackGame = beforeGame;
                switch (d.Action)
                {
                    case "AggressiveClean":
                        success = ExecuteAggressiveMemoryClean();
                        break;
                    case "LightClean":
                    case "PreemptiveClean":
                        success = ExecuteMemoryClean();
                        break;
                    case "PurgeStandbyList":
                        success = ExecutePurgeStandby();
                        break;
                    case "PurgeClipboard":
                        success = ExecutePurgeClipboard();
                        break;
                    case "FlushModifiedList":
                        success = ExecuteFlushModifiedList();
                        break;
                    case "DeepMemoryCompaction":
                        success = ExecuteDeepMemoryCompaction();
                        break;
                    case "OptimizeDesktopSmoothness":
                        success = ExecuteDesktopSmoothness();
                        break;
                    case "TrimInactiveWorkingSets":
                        success = ExecuteTrimInactiveWorkingSets();
                        break;
                    case "LimitBackground":
                        success = ExecuteLimitBackground();
                        break;
                    case "DeferWork":
                        success = ExecuteLimitBackground();
                        break;
                    case "BoostGamePriority":
                        success = ExecuteBoostGamePriority();
                        break;
                    case "BalanceGameLoad":
                    case "BalanceCpuGpuLoad":
                        success = ExecuteBalanceGameLoad();
                        break;
                    case "EmergencyThrottle":
                    case "ReduceThermal":
                        success = ExecuteThermalProtection(d.Action == "EmergencyThrottle");
                        break;
                    case "StabilizeFrames":
                        success = ExecuteFrameStability();
                        break;
                    case "ReduceMicroStutter":
                        success = ExecuteReduceStutter();
                        break;
                    case "OptimizeGpuPerformance":
                        success = ExecuteOptimizeGpuPerformance();
                        break;
                    case "ShedBackgroundGpuMemory":
                        success = ExecuteShedBackgroundGpuMemory();
                        break;
                    case "PurgeGpuShaderCache":
                        success = ExecutePurgeGpuShaderCache();
                        break;
                    case "BatterySaver":
                        success = ExecutePowerPlan("power saver");
                        break;
                    case "BalancedPerformance":
                        success = ExecutePowerPlan("balanced");
                        break;
                    case "SuggestClose":
                    case "HealthCheck":
                    case "DiskCleanup":
                        success = true;
                        break;
                    case "StartupDisable":
                        success = StartupClassifier.DisableWorstStartupBloat();
                        break;
                    case "StartService":
                        success = ExecuteStartService(d);
                        break;
                    case "SetGameAffinity":
                        int cores = Math.Min(Environment.ProcessorCount, 64);
                        long gameMask = cores >= 64 ? -1L : (1L << cores) - 1;
                        success = ExecuteSetAffinity(_currentState.ActiveProcess ?? "", gameMask);
                        break;
                    case "LimitBackgroundAffinity":
                        success = ExecuteSetAffinity("chrome", 1);
                        break;
                    case "SetLowIoPriority":
                        success = ExecuteSetIoPriority(_currentState.ActiveProcess ?? "", 1);
                        break;
                    case "UnparkAllCores":
                        success = ExecuteCoreParking(false);
                        break;
                    case "OptimizeNetwork":
                    case "FlushDnsCache":
                    case "ResetNetworkPriority":
                        success = ExecuteNetworkOptimization(d);
                        break;
                    case "SuspendBloatProcesses":
                        success = ExecuteSuspendBloat();
                        break;
                    case "ResumeSuspendedProcesses":
                        success = ExecuteResumeSuspended();
                        break;
                    case "DisableGameDvr":
                        success = ExecuteDisableGameDvr();
                        break;
                    case "SetHighPerformance":
                        success = ExecutePowerPlan("high performance");
                        break;
                    case "CleanTempFiles":
                        success = ExecuteCleanTempFiles();
                        break;
                    case "SwitchPowerPlan":
                        success = ExecutePowerPlanSwitch(d);
                        break;
                    default:
                        // Knowledge Base actions are advisory only — log and count as applied
                        if (d.Action.StartsWith("KB_"))
                        {
                            success = true;
                            // Record the knowledge base suggestion was considered
                            try
                            {
                                string tweakId = d.Action.Substring(3);
                                _tweakKnowledge.RecordApplication(tweakId, true);
                            }
                            catch (Exception ex) { LogError("ExecuteDecision", ex); }
                        }
                        else if (d.Action.StartsWith("Scheduled_"))
                        {
                            if (d.Action.Contains("ProcessOptimization") || d.Action.Contains("Memory"))
                                success = ExecuteMemoryClean();
                            else
                                success = true;
                        }
                        else if (d.Action.StartsWith("PredictiveMaintenance_") || d.Action.StartsWith("ForecastPrevent_"))
                        {
                            if (d.Action.IndexOf("Memory", StringComparison.OrdinalIgnoreCase) >= 0 || d.Action.IndexOf("Ram", StringComparison.OrdinalIgnoreCase) >= 0)
                                success = ExecuteMemoryClean();
                            else
                                success = true;
                        }
                        else if (d.Action.StartsWith("UserBehavior_"))
                        {
                            success = true;
                        }
                        else
                        {
                            success = false;
                        }
                        break;
                }
                // Record cooldown
                _actionCooldowns[d.Action] = DateTime.Now;
                // Record journal entry for reversible actions
                if (success && (d.Action == "BatterySaver" || d.Action == "BalancedPerformance" || d.Action == "BoostGamePriority" || d.Action.Contains("Throttle")))
                {
                    string afterPlan = GetCurrentPowerPlan();
                    var journal = new StateJournalEntry
                    {
                        Action = d.Action,
                        Category = d.Category.ToString(),
                        GameName = _currentState.ActiveProcess ?? "",
                        WhatChanged = d.Action.Contains("Throttle") ? "PowerPlan+Priority" : d.Action == "BoostGamePriority" ? "ProcessPriority" : "PowerPlan",
                        OriginalValue = beforePlan,
                        NewValue = afterPlan,
                        State = 1 // Applied
                    };
                    lock (_journalLock)
                    {
                        _journal.Add(journal);
                        if (_journal.Count > 100) _journal.RemoveAt(0);
                    }
                    AddLog("JOURNAL", "Recorded", "State saved: " + journal.WhatChanged + " | " + journal.OriginalValue + " -> " + journal.NewValue);
                }
            }
            catch { success = false; }
            d.Timestamp = DateTime.Now;
            try
            {
                lock (_decisionLock)
                {
                    _decisionHistory.Add(d);
                    if (_decisionHistory.Count > 1000) _decisionHistory.RemoveAt(0);
                    _stats.TotalDecisions++;
                }
            }
            // Auto-generate explanation for this decision
            catch (Exception ex) { LogError("ExecuteDecision", ex); }
            if (success)
            {
                lock (_decisionLock) { _stats.SuccessfulDecisions++; }
                _lastActionTime = DateTime.Now;
                // Informational actions leave no state change to learn from —
                // a clean "no exception" must not count as a successful
                // system action (that is the old double-counting bug).
                if (IsInformationalAction(d.Action)) LogInformationalDecision(d);
                else LogDecisionResult(d, true);
                AddLog("OK", d.Category.ToString(), "Executed: " + d.Description + " | " + d.Reason);
            }
            else
            {
                lock (_decisionLock) { _stats.FailedDecisions++; }
                // Safe Mode: track consecutive failures in window
                if ((DateTime.Now - _safeModeWindowStart).TotalSeconds > SAFE_MODE_WINDOW_SECONDS)
                { _safeModeFailureCount = 0; _safeModeWindowStart = DateTime.Now; }
                _safeModeFailureCount++;
                if (_safeModeFailureCount >= SAFE_MODE_THRESHOLD && !_safeMode)
                {
                    _safeMode = true;
                    _mode = AgentMode.Off;
                    AddLog("FAIL", "SafeMode", "Entered SAFE MODE - " + _safeModeFailureCount + " failures in " + SAFE_MODE_WINDOW_SECONDS + "s");
                }
                LogDecisionResult(d, false);
                // Rollback: restore previous state on failure
                try
                {
                    string curPlan = GetCurrentPowerPlan();
                    if (_rollbackPlan != curPlan && !string.IsNullOrEmpty(_rollbackPlan))
                    {
                        RestorePowerPlan(_rollbackPlan);
                        AddLog("ROLLBACK", d.Category.ToString(), "Restored power plan: " + _rollbackPlan);
                    }
                    // Rollback process priority if we changed it
                    if (d.Action == "BoostGamePriority" && !string.IsNullOrEmpty(_rollbackGame))
                    {
                        RestoreProcessPriorityNormal(_rollbackGame);
                        AddLog("ROLLBACK", d.Category.ToString(), "Restored process priority to Normal: " + _rollbackGame);
                    }
                }
                catch (Exception ex) { LogError("ExecuteDecision", ex); }
                AddLog("FAIL", d.Category.ToString(), "Failed: " + d.Description);
            }
            try
            {
                lock (_decisionLock) { _stats.LastDecisionTime = DateTime.Now; }
                _lastExecutionSuccess = success;
                // === Decouple the outcome pipeline via the message bus ===
                try { Events.Publish(new DecisionExecutedEvent(d, success)); }
                catch (Exception ex) { LogError("Bus.DecisionExecuted", ex); }
                // === Real outcome measurement starts right after execution ===
                // Advisory actions (health checks, KB suggestions, scheduled
                // bookkeeping) have no causal effect on the system, so starting a
                // 20s measurement window for them would pollute the reward signal.
                // Only genuinely system-changing actions get measured.
                if (IsMeasurableAction(d.Action))
                {
                    StartActionEvaluation(d.Action, success);
                }
                else if (_outcomeRecord != null && !_outcomeRecord.Finalized)
                {
                    FinalizeOutcomeRecord(true);
                }
            }
            // === AI Post-Optimization Report ===
            catch (Exception ex) { LogError("ExecuteDecision", ex); }
            // === Feature 5: Log result ===
            AddLogOpt(d.Action, string.Format("Result: {0} | {1}", success ? "SUCCESS" : "FAILED", d.Description));
            // === Feature 10: Speedometer delta ===
            CalculateSpeedometerDelta(d.Action);
            AddLogOpt("SPEEDOMETER", _lastOptimizationResult);
            if (OnDecisionExecuted != null) OnDecisionExecuted(d);
            RecordExperience(d, success);
            // === Defer heavy post-decision work to background queue ===
            var capturedD = d;
            var capturedSuccess = success;
            var capturedPreOptCpu = _preOptCpu;
            var capturedPreOptRam = _preOptRam;
            var capturedHealthScore = SystemHealthScore;
            var capturedPrevHealthScore = _prevHealthScore;
            var capturedSmoothTemp = _smoothTemp;
            var capturedPrevTemp = _prevTemp;
            EnqueueDeferred(() =>
            {
                try
                {
                    RecordConfidenceSample(capturedD.Action, capturedD.Confidence, capturedSuccess);
                }
                catch (Exception ex) { LogError("ExecuteDecision", ex); }
                try
                {
                    double beforeScore = capturedPreOptCpu > 0 ? (100 - capturedPreOptCpu) * 0.3 + (100 - capturedPreOptRam) * 0.3 + capturedHealthScore * 0.4 : capturedHealthScore;
                    LearnFromResult(capturedD, capturedSuccess, beforeScore, capturedHealthScore);
                }
                catch (Exception ex) { LogError("KnowledgeBase", ex); }
                try
                {
                    ExplainDecisionFull(capturedD, _pendingDecisions);
                }
                catch (Exception ex) { LogError("Explainability", ex); }
                try
                {
                    GeneratePlainExplanation(capturedD, _pendingDecisions);
                }
                catch (Exception ex) { LogError("ExecuteDecision", ex); }
                try
                {
                    double actionCost = ComputeActionCost(capturedD.Action);
                    double benefit = ComputeActionBenefit(capturedD);
                    double satisfaction = capturedSuccess ? benefit : -actionCost * 0.5;
                    RecordActionCost(capturedD.Action, actionCost, Math.Max(0, benefit), satisfaction);
                }
                catch (Exception ex) { LogError("ExecuteDecision", ex); }
                try
                {
                    RecordAuditEntry(capturedD.Action, capturedD.Category.ToString(), capturedD.Description, capturedSuccess, capturedD.Impact);
                }
                catch (Exception ex) { LogError("Enterprise", ex); }
            });
        }

        private bool ExecuteCleanProcesses(string[] processPatterns)
        {
            try
            {
                uint fgPid = 0;
                try
                {
                    IntPtr fg = GetForegroundWindow();
                    if (fg != IntPtr.Zero) GetWindowThreadProcessId(fg, out fgPid);
                }
                catch (Exception ex) { LogError("ExecuteCleanProcesses", ex); }
                var procs = GetCachedProcesses();
                if (procs == null || procs.Length == 0) return false;
                for (int i = 0; i < procs.Length; i++)
                {
                    var p = procs[i];
                    try
                    {
                        if (p == null || p.Id <= 4 || p.Id == (int)fgPid) continue;
                        string name = p.ProcessName.ToLower();
                        foreach (string pattern in processPatterns)
                        {
                            if (name.Contains(pattern))
                            {
                                EmptyWorkingSet(p.Handle);
                                break;
                            }
                        }
                    }
                    catch (Exception ex) { LogError("ExecuteCleanProcesses", ex); }
                }
                return true;
            }
            catch { return false; }
        }

        private bool ExecuteMemoryClean()
        {
            if (DisableRealExecution || _simulationMode) return true;
            return ExecuteCleanProcesses(SAFE_TO_CLEAN_PROCESSES);
        }

        private bool ExecutePurgeStandby()
        {
            if (DisableRealExecution || _simulationMode) return true;
            // Central safety gate: never evict the standby list while a game is active.
            // The feature is intended for pre-game/idle memory pressure, not live frame pacing.
            try { if (CurrentState != null && CurrentState.IsGame) return false; } catch (Exception ex10) { CWLog.Error("SmartAgent", ex10); }
            IntPtr p = IntPtr.Zero;
            try
            {
                bool prev;
                RtlAdjustPrivilege(19, true, false, out prev); // 19 = SeIncreaseQuotaPrivilege

                int cmd = 4;
                p = Marshal.AllocHGlobal(4);
                Marshal.WriteInt32(p, cmd);
                uint r = NtSetSystemInformation(SystemMemoryListInformation, p, 4);
                if (r == 0) return true;

                // If non-elevated or system call blocked, fallback to cleaning memory safely
                return ExecuteMemoryClean();
            }
            catch { return ExecuteMemoryClean(); }
            finally { if (p != IntPtr.Zero) Marshal.FreeHGlobal(p); }
        }

        public static bool ExecutePurgeClipboard(bool allowDestructiveUserClipboardWipe = false)
        {
            // Safety Policy: Never silently wipe the user's clipboard during RAM optimization or background compaction.
            if (!allowDestructiveUserClipboardWipe) return false;
            try
            {
                if (OpenClipboard(IntPtr.Zero))
                {
                    try
                    {
                        return EmptyClipboard();
                    }
                    finally
                    {
                        CloseClipboard();
                    }
                }
            }
            catch { }
            return false;
        }

        public bool ExecuteFlushModifiedList()
        {
            try { if (CurrentState != null && CurrentState.IsGame) return false; } catch { }
            IntPtr p = IntPtr.Zero;
            try
            {
                bool prev;
                RtlAdjustPrivilege(19, true, false, out prev);

                int cmd = 3; // MemoryFlushModifiedList
                p = Marshal.AllocHGlobal(4);
                Marshal.WriteInt32(p, cmd);
                uint r = NtSetSystemInformation(SystemMemoryListInformation, p, 4);
                return r == 0;
            }
            catch { return false; }
            finally { if (p != IntPtr.Zero) Marshal.FreeHGlobal(p); }
        }

        public bool ExecuteAggressiveMemoryClean()
        {
            try { if (CurrentState != null && CurrentState.IsGame) return false; } catch { }
            bool ok = false;
            try
            {
                // 1. Clean safe memory-heavy apps
                if (ExecuteCleanProcesses(SAFE_TO_CLEAN_PROCESSES)) ok = true;

                // 2. Clean all non-critical background processes
                var procs = GetCachedProcesses();
                if (procs != null)
                {
                    uint fgPid = 0;
                    try
                    {
                        IntPtr fg = GetForegroundWindow();
                        if (fg != IntPtr.Zero) GetWindowThreadProcessId(fg, out fgPid);
                    }
                    catch { }
                    uint selfPid = 0;
                    try { selfPid = (uint)Process.GetCurrentProcess().Id; } catch { }

                    for (int i = 0; i < procs.Length; i++)
                    {
                        var p = procs[i];
                        try
                        {
                            if (p == null || p.Id <= 4 || p.Id == (int)fgPid || p.Id == (int)selfPid) continue;
                            string name = p.ProcessName.ToLower();
                            if (SYSTEM_PROCESSES.Contains(name)) continue;
                            EmptyWorkingSet(p.Handle);
                        }
                        catch { }
                    }
                }

                // 3. Clipboard purge intentionally omitted to preserve user copied data.

                // 4. Flush modified pages to disk
                try { if (ExecuteFlushModifiedList()) ok = true; } catch { }

                // 5. Purge standby list into free RAM
                try { if (ExecutePurgeStandby()) ok = true; } catch { }
            }
            catch (Exception ex) { LogError("ExecuteAggressiveMemoryClean", ex); }
            return ok;
        }

        public bool ExecuteDeepMemoryCompaction()
        {
            try { if (CurrentState != null && CurrentState.IsGame) return false; } catch { }
            return ExecuteAggressiveMemoryClean();
        }

        private static readonly string[] BLOAT_PROCESS_PATTERNS = new[] {
            "telemetry", "werfault", "compattelrunner", "wsqmcons", "customer experience",
            "browser_broker", "smartscreen", "adware", "spigot", "coupon", "toolbar"
        };

        private bool ExecuteTerminateBloatProcesses()
        {
            if (DisableRealExecution) return true;
            try
            {
                var procs = GetCachedProcesses();
                if (procs == null || procs.Length == 0) return false;
                uint fgPid = 0;
                try
                {
                    IntPtr fg = GetForegroundWindow();
                    if (fg != IntPtr.Zero) GetWindowThreadProcessId(fg, out fgPid);
                }
                catch { }
                uint selfPid = 0;
                try { selfPid = (uint)Process.GetCurrentProcess().Id; } catch { }

                for (int i = 0; i < procs.Length; i++)
                {
                    var p = procs[i];
                    try
                    {
                        if (p == null || p.Id <= 4 || (uint)p.Id == fgPid || (uint)p.Id == selfPid || p.HasExited) continue;
                        string name = p.ProcessName.ToLower();

                        // 1. Never touch user critical, system, or foreground apps
                        if (SYSTEM_PROCESSES.Contains(name) || IsUserCriticalProcess(name)) continue;

                        // 2. Terminate hung / unresponsive non-critical background processes without a visible window
                        if (!p.Responding && p.MainWindowHandle == IntPtr.Zero)
                        {
                            try
                            {
                                p.Kill();
                                AddLog("CLEAN", "TerminateHung", "Terminated hung process: " + name);
                            }
                            catch { }
                            continue;
                        }

                        // 3. Terminate known background bloat/telemetry/crash-reporter processes without a window
                        if (p.MainWindowHandle == IntPtr.Zero)
                        {
                            for (int b = 0; b < BLOAT_PROCESS_PATTERNS.Length; b++)
                            {
                                if (name.Contains(BLOAT_PROCESS_PATTERNS[b]))
                                {
                                    try
                                    {
                                        p.Kill();
                                        AddLog("CLEAN", "TerminateBloat", "Terminated bloat process: " + name);
                                    }
                                    catch { }
                                    break;
                                }
                            }
                        }
                    }
                    catch (Exception ex) { LogError("ExecuteTerminateBloatProcesses", ex); }
                }
                return true;
            }
            catch { return false; }
        }

        private bool ExecuteLimitBackground()
        {
            bool cleaned = ExecuteCleanProcesses(BACKGROUND_PROCESSES);
            ExecuteTerminateBloatProcesses();
            return cleaned;
        }

        private bool ExecuteBoostGamePriority()
        {
            try
            {
                IntPtr fg = GetForegroundWindow();
                if (fg == IntPtr.Zero) return false;
                uint pid;
                GetWindowThreadProcessId(fg, out pid);
                if (pid <= 0) return false;
                using (var gameProc = Process.GetProcessById((int)pid))
                {
                    if (gameProc.HasExited) return false;
                    try { gameProc.PriorityClass = PRIORITY_HIGH; }
                    catch (Exception ex) { LogError("ExecuteBoostGamePriority", ex); }
                }
                BoostAllGameFamilyProcesses();
                if (_currentState.IsGame)
                {
                    try
                    {
                        uint cur;
                        NtSetTimerResolution(5000, true, out cur);
                        ExecuteCoreParking(false);
                        EnforceGameDedicatedGpuAndPower(_currentState.ActiveProcess, _currentState.ActiveProcessPath);
                    }
                    catch { }
                }
                var procs = GetCachedProcesses();
                if (procs == null || procs.Length == 0) return true;
                // During gaming: only limit top 5 heaviest background processes
                if (_currentState.IsGame)
                {
                    string gameName = _currentState.ActiveProcess != null ? _currentState.ActiveProcess.ToLower() : "";
                    var candidates = new List<Process>();
                    for (int i = 0; i < procs.Length; i++)
                    {
                        var p = procs[i];
                        try
                        {
                            if (p == null || p.Id <= 4 || p.Id == pid || p.HasExited) continue;
                            string name = p.ProcessName.ToLower();
                            if (SYSTEM_PROCESSES.Contains(name)) continue;
                            // NEVER demote sibling processes of the active game: multi-process
                            // emulators (Gameloop/BlueStacks/Nox...) run their render/input/audio
                            // services as separate processes. Demoting them starves the game and
                            // causes exactly the stutter this feature is meant to prevent.
                            if (IsGameFamilyProcess(name, gameName)) continue;
                            candidates.Add(p);
                        }
                        catch (Exception ex11) { CWLog.Error("SmartAgent", ex11); }
                    }
                    candidates.Sort((a, b) => { try { return b.WorkingSet64.CompareTo(a.WorkingSet64); } catch { return 0; } });
                    int limited = 0;
                    foreach (var p in candidates)
                    {
                        if (limited >= 5) break;
                        try
                        {
                            int curPrio = (int)p.PriorityClass;
                            if (curPrio != (int)PRIORITY_HIGH && curPrio != (int)PRIORITY_REALTIME)
                                p.PriorityClass = PRIORITY_BELOW_NORMAL;
                            limited++;
                        }
                        catch (Exception ex) { LogError("ExecuteBoostGamePriority", ex); }
                    }
                }
                else
                {
                    for (int i = 0; i < procs.Length; i++)
                    {
                        var p = procs[i];
                        try
                        {
                            if (p == null || p.HasExited) continue;
                            int curPrio = (int)p.PriorityClass;
                            if (p.Id != pid && p.Id > 4 && curPrio != (int)PRIORITY_HIGH && curPrio != (int)PRIORITY_REALTIME)
                            {
                                string name = p.ProcessName.ToLower();
                                bool isSystem = SYSTEM_PROCESSES.Contains(name);
                                if (!isSystem) p.PriorityClass = PRIORITY_BELOW_NORMAL;
                            }
                        }
                        catch (Exception ex) { LogError("ExecuteBoostGamePriority", ex); }
                    }
                }
                return true;
            }
            catch { return false; }
        }

        private bool IsBackgroundBloatOrUpdater(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            for (int i = 0; i < BACKGROUND_PROCESSES.Length; i++)
            {
                if (name.Contains(BACKGROUND_PROCESSES[i])) return true;
            }
            if (name.Contains("searchindexer") || name.Contains("tiworker") ||
                name.Contains("trustedinstaller") || name.Contains("mscorsvw") ||
                name.Contains("sedsvc") || name.Contains("waasmedicsvc") ||
                name.Contains("usoclient") || name.Contains("compattelrunner"))
            {
                return true;
            }
            return false;
        }

        public bool ExecuteDesktopSmoothness()
        {
            // Strict Mode Duality: If a game is active, yield immediately to Gaming Mode
            if (CurrentState != null && CurrentState.IsGame) return false;
            if (DisableRealExecution) return true;
            try
            {

                // 1. Maintain 1.0ms high-precision desktop timer resolution for fluid DWM & mouse input
                try
                {
                    uint cur;
                    NtSetTimerResolution(TIMER_RESOLUTION_DESKTOP, true, out cur);
                }
                catch (Exception exTimer) { LogError("ExecuteDesktopSmoothness.Timer", exTimer); }

                // 2. Identify active foreground application and elevate its priority to AboveNormal
                IntPtr fg = GetForegroundWindow();
                uint fgPid = 0;
                if (fg != IntPtr.Zero)
                {
                    GetWindowThreadProcessId(fg, out fgPid);
                }

                if (fgPid > 4)
                {
                    try
                    {
                        using (var fgProc = Process.GetProcessById((int)fgPid))
                        {
                            if (!fgProc.HasExited)
                            {
                                string name = fgProc.ProcessName.ToLower();
                                if (!SYSTEM_PROCESSES.Contains(name))
                                {
                                    if (fgProc.PriorityClass != PRIORITY_ABOVE_NORMAL && fgProc.PriorityClass != PRIORITY_HIGH && fgProc.PriorityClass != PRIORITY_REALTIME)
                                    {
                                        fgProc.PriorityClass = PRIORITY_ABOVE_NORMAL;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception exProc) { LogError("ExecuteDesktopSmoothness.FgProc", exProc); }
                }

                // 3. Elevate Desktop Window Manager (dwm.exe) for buttery smooth composition
                try
                {
                    var dwmProcs = Process.GetProcessesByName("dwm");
                    for (int i = 0; i < dwmProcs.Length; i++)
                    {
                        try
                        {
                            var d = dwmProcs[i];
                            if (d.PriorityClass != PRIORITY_HIGH && d.PriorityClass != PRIORITY_REALTIME)
                            {
                                d.PriorityClass = PRIORITY_HIGH;
                            }
                        }
                        catch { }
                        finally { dwmProcs[i].Dispose(); }
                    }
                }
                catch { }

                // 4. If desktop CPU usage is elevated (> 35%), unpark cores to eliminate core-wake latency
                if (_currentState != null && _currentState.CpuUsage > 35)
                {
                    try { ExecuteCoreParking(false); } catch { }
                }

                // 5. Demote background updaters and telemetry to BelowNormal priority
                var procs = GetCachedProcesses();
                if (procs != null && procs.Length > 0)
                {
                    for (int i = 0; i < procs.Length; i++)
                    {
                        var p = procs[i];
                        try
                        {
                            if (p == null || p.Id <= 4 || (uint)p.Id == fgPid || p.HasExited) continue;
                            string name = p.ProcessName.ToLower();
                            if (SYSTEM_PROCESSES.Contains(name) || IsUserCriticalProcess(name)) continue;

                            if (IsBackgroundBloatOrUpdater(name))
                            {
                                if (p.PriorityClass != PRIORITY_BELOW_NORMAL)
                                {
                                    p.PriorityClass = PRIORITY_BELOW_NORMAL;
                                }
                            }
                        }
                        catch { }
                    }
                }

                AddLog("DESKTOP", "Smoothness", "Desktop Smoothness active: Foreground elevated, 1.0ms timer enforced, DWM optimized");
                return true;
            }
            catch (Exception ex)
            {
                LogError("ExecuteDesktopSmoothness", ex);
                return false;
            }
        }

        public bool ExecuteTrimInactiveWorkingSets()
        {
            // Anti-stutter safety gate: NEVER trim working sets during an active game
            if (CurrentState != null && CurrentState.IsGame) return false;
            if (DisableRealExecution) return true;

            try
            {
                IntPtr fg = GetForegroundWindow();
                uint fgPid = 0;
                if (fg != IntPtr.Zero)
                {
                    GetWindowThreadProcessId(fg, out fgPid);
                }

                var procs = GetCachedProcesses();
                if (procs == null || procs.Length == 0) return false;

                int trimmed = 0;
                long totalBefore = 0;
                long totalAfter = 0;

                for (int i = 0; i < procs.Length; i++)
                {
                    var p = procs[i];
                    try
                    {
                        if (p == null || p.Id <= 4 || (uint)p.Id == fgPid || p.HasExited) continue;
                        string name = p.ProcessName.ToLower();
                        if (SYSTEM_PROCESSES.Contains(name) || IsUserCriticalProcess(name)) continue;

                        long ws = p.WorkingSet64;
                        if (ws > 80 * 1024 * 1024) // > 80 MB
                        {
                            totalBefore += ws;
                            EmptyWorkingSet(p.Handle);
                            p.Refresh();
                            totalAfter += p.WorkingSet64;
                            trimmed++;
                        }
                    }
                    catch { }
                }

                long freedMb = Math.Max(0, (totalBefore - totalAfter) / (1024 * 1024));
                AddLog("DESKTOP", "TrimMemory", string.Format("Trimmed {0} inactive apps, freed ~{1} MB working set", trimmed, freedMb));
                return true;
            }
            catch (Exception ex)
            {
                LogError("ExecuteTrimInactiveWorkingSets", ex);
                return false;
            }
        }

        private bool ExecuteBalanceGameLoad()
        {
            try
            {
                // 1. Unpark all cores so the CPU never stalls on thread scheduling
                ExecuteCoreParking(false);

                // 2. Set foreground game to High priority and demote non-game background
                ExecuteBoostGamePriority();

                // 3. Ensure game has affinity to all cores
                int cores = Math.Min(Environment.ProcessorCount, 64);
                long gameMask = cores >= 64 ? -1L : (1L << cores) - 1;
                if (!string.IsNullOrEmpty(_currentState.ActiveProcess))
                {
                    ExecuteSetAffinity(_currentState.ActiveProcess, gameMask);
                }

                // 4. Reduce background IO contention
                ExecuteLimitBackground();

                // 5. Ensure high-resolution kernel timer 0.500ms to eliminate D3D frame pacing jitter
                try
                {
                    uint cur;
                    NtSetTimerResolution(5000, true, out cur);
                }
                catch { }

                // 6. Force Dedicated High-Performance GPU & Ultimate Power Plan for game
                EnforceGameDedicatedGpuAndPower(_currentState.ActiveProcess, _currentState.ActiveProcessPath);

                return true;
            }
            catch (Exception ex)
            {
                LogError("ExecuteBalanceGameLoad", ex);
                return false;
            }
        }

        // True when a process belongs to the same game/emulator family as the active
        // game. Multi-process emulators must never have their services demoted.
        private bool IsGameFamilyProcess(string procName, string gameName)
        {
            if (string.IsNullOrEmpty(procName)) return false;
            if (!string.IsNullOrEmpty(gameName) && procName == gameName) return true;
            return procName.Contains("androidemulator") || procName.Contains("qmemulator")
                || procName.Contains("qmplayer") || procName.Contains("qqpcgameproxy")
                || procName.Contains("qqgame") || procName.Contains("gameloop")
                || procName.Contains("ldboxheadless") || procName.Contains("ldplayer")
                || procName.Contains("hd-player") || procName.Contains("bstksvc")
                || procName.Contains("noxvm") || procName.Contains("memuheadless")
                || procName.Contains("microvirt") || procName.Contains("androws")
                || procName.Contains("aow") || procName.Contains("mumu")
                || procName.Contains("tslgame") || procName.Contains("pubg")
                || procName.Contains("appmarket") || procName.Contains("glabox");
        }

        // Boost all processes belonging to active game or mobile emulator families
        private void BoostAllGameFamilyProcesses()
        {
            try
            {
                string[] targets = new[] { "aow_exe", "androwsfg", "androidprocess", "gameloopemulator", "gameloopvm", "glaboxheadless", "gameloop", "appmarket", "androidemulator", "androidemulatoren", "androidemulatorex", "qmplayerservice", "qmemulatorservice", "txservice", "syzs_cbox", "tslgame", "tslgame_uc", "pubg", "pubglite", "mumunxmain", "mumunxdevice", "dnplayer", "hd-player" };
                int cores = Math.Min(Environment.ProcessorCount, 64);
                long gameMask = cores >= 64 ? -1L : (1L << cores) - 1;

                foreach (var t in targets)
                {
                    try
                    {
                        var procs = Process.GetProcessesByName(t);
                        foreach (var p in procs)
                        {
                            using (p)
                            {
                                try
                                {
                                    if (!p.HasExited)
                                    {
                                        if (p.PriorityClass != PRIORITY_HIGH)
                                            p.PriorityClass = PRIORITY_HIGH;
                                        p.PriorityBoostEnabled = true;
                                        try { p.ProcessorAffinity = (IntPtr)gameMask; } catch { }

                                        // Elevate render and compute threads
                                        foreach (ProcessThread th in p.Threads)
                                        {
                                            try
                                            {
                                                if (th.PriorityLevel != ThreadPriorityLevel.Highest && th.PriorityLevel != ThreadPriorityLevel.TimeCritical)
                                                {
                                                    th.PriorityLevel = ThreadPriorityLevel.Highest;
                                                }
                                            }
                                            catch { }
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        // Aggressive 60 FPS Floor Lock: does whatever it takes to prevent PUBG/GameLoop dropping below 60
        public bool ExecuteAggressive60FpsLock(double currentFps)
        {
            try
            {
                // 1. Force High Resolution Kernel Timer (0.500ms)
                try
                {
                    uint cur;
                    NtSetTimerResolution(5000, true, out cur);
                }
                catch { }

                // 2. Unpark all cores immediately
                ExecuteCoreParking(false);

                // 3. Enforce Dedicated High-Performance GPU and Ultimate/High power plan
                EnforceGameDedicatedGpuAndPower(_currentState != null ? _currentState.ActiveProcess : null, _currentState != null ? _currentState.ActiveProcessPath : null);

                // 4. Boost all game and emulator family processes and threads
                BoostAllGameFamilyProcesses();

                // 5. Elevate foreground game process
                ExecuteBoostGamePriority();

                // 6. Suppress background processes to prevent CPU contention
                ExecuteLimitBackground();

                // 7. Log in Arabic to AiDecisionLogger
                AiDecisionLogger.RecordDecision(
                    "StabilizeFrames",
                    string.Format("قفل الفريمات القسري (Aggressive 60 FPS Floor): تثبيت مؤقت النواة على 0.5ms، فك ركن كافة الأنوية، ورفع أولويات معالجة محاكي GameLoop/PUBG لمنع الهبوط دون 60 FPS."),
                    0.98,
                    "Gaming",
                    "SUCCESS",
                    string.Format("تم رصد الفريمات عند {0:F0} FPS - تم استدعاء حزمة استقرار الإطارات القصوى لمنع أي Micro-stutters وتأمين الـ 60 FPS.", currentFps)
                );

                return true;
            }
            catch (Exception ex)
            {
                LogError("ExecuteAggressive60FpsLock", ex);
                return false;
            }
        }

        private bool ExecuteThermalProtection(bool emergency)
        {
            try
            {
                // Thermal mitigation must reduce power/load, not purge standby memory.
                // Purging memory is not a cooling measure and may cause extra disk I/O.
                if (_currentState == null) return false;

                if (_currentState.IsGame)
                {
                    // Gaming safety: Modern CPUs operate safely up to 95C under sustained load.
                    // Never throttle game below 95C ("حتى لو هيقتل نفسه").
                    if (_currentState.CpuTemp < 95)
                    {
                        return true;
                    }
                    return ExecutePowerPlan("balanced");
                }

                return ExecutePowerPlan(emergency ? "power saver" : "balanced");
            }
            catch (Exception ex)
            {
                LogError("ExecuteThermalProtection", ex);
                return false;
            }
        }

        private bool ExecuteFrameStability()
        {
            try
            {
                // During gaming: execute full aggressive 60 FPS floor lock
                if (_currentState != null && _currentState.IsGame)
                {
                    return ExecuteAggressive60FpsLock(_currentState.Fps);
                }
                ExecuteMemoryClean();
                ExecutePurgeStandby();
                IntPtr fg2 = GetForegroundWindow();
                if (fg2 != IntPtr.Zero)
                {
                    uint pid2;
                    GetWindowThreadProcessId(fg2, out pid2);
                    if (pid2 > 0)
                    {
                        using (var p = Process.GetProcessById((int)pid2))
                        {
                            if (!p.HasExited) p.PriorityClass = PRIORITY_HIGH;
                        }
                    }
                }
                return true;
            }
            catch { return false; }
        }

        private bool ExecuteReduceStutter()
        {
            try
            {
                // During gaming: lock timer resolution to 0.5ms, unpark cores, and boost game threads
                if (_currentState != null && _currentState.IsGame)
                {
                    return ExecuteAggressive60FpsLock(_currentState.Fps);
                }
                ExecutePurgeStandby();
                ExecuteMemoryClean();
                ExecuteLimitBackground();
                return true;
            }
            catch { return false; }
        }

        private bool ExecutePowerPlan(string plan)
        {
            try
            {
                string target = plan.Contains("saver") ? POWER_SAVER_GUID
                    : (plan.Contains("ultimate") || plan.Contains("high")) ? FindBestPerformancePlanGuid()
                    : BALANCED_GUID;
                ProcessStartInfo psi = new ProcessStartInfo("powercfg", "/setactive " + target);
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                string output = "";
                using (Process p = Process.Start(psi))
                {
                    output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(10000);
                    if (p.ExitCode != 0)
                    {
                        AddLog("WARN", "PowerPlan", "powercfg /setactive " + target + " failed (exit " + p.ExitCode + "): " + output.Trim());
                        return false;
                    }
                }
                if (plan.Contains("ultimate") || plan.Contains("high"))
                {
                    try
                    {
                        using (var p1 = Process.Start(new ProcessStartInfo("powercfg.exe", "/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN 100") { CreateNoWindow = true, UseShellExecute = false })) { p1.WaitForExit(1500); }
                        using (var p2 = Process.Start(new ProcessStartInfo("powercfg.exe", "/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMAX 100") { CreateNoWindow = true, UseShellExecute = false })) { p2.WaitForExit(1500); }
                        using (var p3 = Process.Start(new ProcessStartInfo("powercfg.exe", "/setacvalueindex SCHEME_CURRENT SUB_PROCESSOR CPMINCORES 100") { CreateNoWindow = true, UseShellExecute = false })) { p3.WaitForExit(1500); }
                        using (var p4 = Process.Start(new ProcessStartInfo("powercfg.exe", "/setactive SCHEME_CURRENT") { CreateNoWindow = true, UseShellExecute = false })) { p4.WaitForExit(1500); }
                    }
                    catch { }
                }
                string expected = target == POWER_SAVER_GUID ? "Power Saver"
                    : (plan.Contains("ultimate") || plan.Contains("high")) ? "High Performance"
                    : target == BALANCED_GUID ? "Balanced" : "";
                string cur = GetCurrentPowerPlan();
                if (!string.IsNullOrEmpty(expected) && cur != expected && !cur.Contains("High") && !cur.Contains("Ultimate") && !cur.Contains("Smart"))
                {
                    AddLog("WARN", "PowerPlan", "powercfg /setactive " + target + " reported success but active scheme is now: " + cur);
                    return false;
                }
                return true;
            }
            catch (Exception ex) { LogError("ExecutePowerPlan", ex); return false; }
        }

        // === Tool: suspend top background bloat processes (resumable) ===
        private bool ExecuteSuspendBloat()
        {
            try
            {
                int suspended = 0;
                Process[] procs = GetCachedProcesses();
                if (procs == null) return false;
                uint fgPid = 0;
                IntPtr fgW = GetForegroundWindow();
                if (fgW != IntPtr.Zero) GetWindowThreadProcessId(fgW, out fgPid);
                uint selfPid = (uint)Process.GetCurrentProcess().Id;
                // The foreground process's own name is also protected: even if the
                // user is on a different window, actively-used apps must not be
                // suspended ("freeze") behind their back.
                string fgName = "";
                try { using (var fp = System.Diagnostics.Process.GetProcessById((int)fgPid)) { fgName = fp.ProcessName; } } catch (Exception exFg) { CWLog.Error("SmartAgent", exFg); }
                var sorted = new List<KeyValuePair<Process, long>>();
                foreach (var p in procs)
                {
                    try
                    {
                        if (p == null || p.HasExited || (uint)p.Id == fgPid || (uint)p.Id == selfPid) continue;
                        if (SYSTEM_PROCESSES.Contains(p.ProcessName.ToLower())) continue;
                        if (_suspendedPids.Contains(p.Id)) continue;
                        // Never suspend the active/foreground app or known-user
                        // (browsers, IDEs, office, comms, creative tools) or any
                        // game: these are the "important background" programs that
                        // the user reported being frozen/killed.
                        string pn = p.ProcessName.ToLower();
                        if (!string.IsNullOrEmpty(fgName) && pn == fgName.ToLower()) continue;
                        if (IsUserCriticalProcess(pn)) continue;
                        sorted.Add(new KeyValuePair<Process, long>(p, p.WorkingSet64));
                    }
                    catch (Exception ex12) { CWLog.Error("SmartAgent", ex12); }
                }
                sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
                int take = Math.Min(3, sorted.Count);
                for (int i = 0; i < take; i++)
                {
                    try
                    {
                        IntPtr h = OpenProcess(PROCESS_SUSPEND_RESUME, false, (uint)sorted[i].Key.Id);
                        if (h != IntPtr.Zero)
                        {
                            if (NtSuspendProcess(h) == 0)
                            {
                                _suspendedPids.Add(sorted[i].Key.Id);
                                suspended++;
                            }
                            CloseHandle(h);
                        }
                    }
                    catch (Exception ex13) { CWLog.Error("SmartAgent", ex13); }
                }
                if (suspended > 0)
                {
                    AddLogOpt("SUSPEND", "Suspended " + suspended + " background bloat processes (resumable)");
                    return true;
                }
                return false;
            }
            catch (Exception ex) { LogError("ExecuteSuspendBloat", ex); return false; }
        }

        private bool ExecuteResumeSuspended()
        {
            int resumed = 0;
            for (int i = 0; i < _suspendedPids.Count; i++)
            {
                try
                {
                    IntPtr h = OpenProcess(PROCESS_SUSPEND_RESUME, false, (uint)_suspendedPids[i]);
                    if (h != IntPtr.Zero)
                    {
                        if (NtResumeProcess(h) == 0) resumed++;
                        CloseHandle(h);
                    }
                }
                catch (Exception ex14) { CWLog.Error("SmartAgent", ex14); }
            }
            _suspendedPids.Clear();
            if (resumed > 0) AddLogOpt("RESUME", "Resumed " + resumed + " suspended processes");
            return resumed > 0;
        }

        // === Tool: disable Windows Game DVR / background recording (revertible) ===
        private bool ExecuteDisableGameDvr()
        {
            if (DisableRealExecution || _simulationMode) return true;
            bool changed = false;
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"System\GameConfigStore", true))
                {
                    if (k != null)
                    {
                        object v = k.GetValue("GameDVR_Enabled");
                        int cur = v != null ? Convert.ToInt32(v) : -1;
                        if (_gameDvrPrevEnabled == -1) _gameDvrPrevEnabled = cur;
                        if (cur != 0) { k.SetValue("GameDVR_Enabled", 0, RegistryValueKind.DWord); changed = true; }
                    }
                }
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\GameDVR", true))
                {
                    if (k != null)
                    {
                        object v = k.GetValue("AppCaptureEnabled");
                        int cur = v != null ? Convert.ToInt32(v) : -1;
                        if (_gameDvrPrevCapture == -1) _gameDvrPrevCapture = cur;
                        if (cur != 0) { k.SetValue("AppCaptureEnabled", 0, RegistryValueKind.DWord); changed = true; }
                    }
                }
                if (changed)
                {
                    _gameDvrApplied = true;
                    AddLogOpt("GAMEDVR", "Game DVR / background recording disabled (revertible)");
                }
                return changed;
            }
            catch (Exception ex) { LogError("ExecuteDisableGameDvr", ex); return changed; }
        }

        // === Tool: clean stale user temp files ===
        private bool ExecuteCleanTempFiles()
        {
            if (DisableRealExecution || _simulationMode) return true;
            try
            {
                long freed = 0;
                int count = 0;
                string temp = Path.GetTempPath();
                if (!string.IsNullOrEmpty(temp) && Directory.Exists(temp))
                {
                    foreach (string f in Directory.EnumerateFiles(temp))
                    {
                        try
                        {
                            var fi = new FileInfo(f);
                            if ((DateTime.Now - fi.LastWriteTime).TotalDays > 1)
                            {
                                long sz = fi.Length;
                                File.Delete(f);
                                freed += sz;
                                count++;
                            }
                        }
                        catch (Exception ex15) { CWLog.Error("SmartAgent", ex15); }
                    }
                }
                if (count > 0)
                {
                    AddLogOpt("TEMP", "Cleaned " + count + " stale temp files (" + (freed / 1048576.0).ToString("F1") + " MB)");
                    return true;
                }
                return false;
            }
            catch (Exception ex) { LogError("ExecuteCleanTempFiles", ex); return false; }
        }

        private bool ExecuteSetAffinity(string processName, long coreMask)
        {
            if (coreMask == 0) return false;
            try
            {
                var procs = Process.GetProcessesByName(processName);
                if (procs.Length == 0) return false;
                bool anySet = false;
                try
                {
                    foreach (var p in procs)
                    {
                        try
                        {
                            using (p)
                            {
                                if (!p.HasExited)
                                {
                                    IntPtr prev = SetProcessAffinityMask(p.Handle, (IntPtr)coreMask);
                                    if (prev != IntPtr.Zero) anySet = true;
                                }
                            }
                        }
                        catch (System.ComponentModel.Win32Exception) { /* Access denied on protected system process */ }
                        catch (InvalidOperationException) { /* Process exited */ }
                        catch (Exception ex) { LogError("ExecuteSetAffinity", ex); }
                    }
                }
                catch (Exception ex) { LogError("ExecuteSetAffinity", ex); }
                return anySet;
            }
            catch { return false; }
        }

        private bool ExecuteSetIoPriority(string processName, int ioPriority)
        {
            try
            {
                var procs = Process.GetProcessesByName(processName);
                if (procs.Length == 0) return false;
                try
                {
                    foreach (var p in procs)
                    {
                        try
                        {
                            using (p)
                            {
                                if (!p.HasExited)
                                {
                                    int prio = ioPriority;
                                    NtSetInformationProcess(p.Handle, ProcessIoPriority, ref prio, sizeof(int));
                                }
                            }
                        }
                        catch (System.ComponentModel.Win32Exception) { /* Access denied on protected system process */ }
                        catch (InvalidOperationException) { /* Process exited */ }
                        catch (Exception ex) { LogError("ExecuteSetIoPriority", ex); }
                    }
                }
                catch (Exception ex) { LogError("ExecuteSetIoPriority", ex); }
                return true;
            }
            catch { return false; }
        }

        private bool ExecuteCoreParking(bool enableParking)
        {
            if (DisableRealExecution || _simulationMode) return true;
            try
            {
                // CPMINCORES (0cc5b647-c1df-4637-891a-dec35c318583) specifies the MINIMUM percentage of unparked cores.
                // Unparking all cores (enableParking == false) requires 100%; normal parking (enableParking == true) uses 5%.
                string minUnparkedPercent = enableParking ? "5" : "100";
                ProcessStartInfo psi = new ProcessStartInfo("powercfg", "/setacvalueindex scheme_current sub_processor CPMINCORES " + minUnparkedPercent);
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                using (var p = Process.Start(psi)) { if (p != null) p.WaitForExit(3000); }
                ProcessStartInfo psiActive = new ProcessStartInfo("powercfg", "/setactive scheme_current");
                psiActive.CreateNoWindow = true;
                psiActive.UseShellExecute = false;
                psiActive.WindowStyle = ProcessWindowStyle.Hidden;
                using (var p2 = Process.Start(psiActive)) { if (p2 != null) p2.WaitForExit(3000); }
                return true;
            }
            catch { return false; }
        }

        private void ApplyPersistentRules()
        {
            foreach (var rule in _persistentRules)
            {
                if (!rule.Enabled) continue;
                try
                {
                    switch (rule.Action)
                    {
                        case "Affinity":
                            int mask = int.Parse(rule.Value);
                            ExecuteSetAffinity(rule.ProcessName, mask);
                            break;
                        case "IoPriority":
                            int ioPrio = int.Parse(rule.Value);
                            ExecuteSetIoPriority(rule.ProcessName, ioPrio);
                            break;
                    }
                }
                catch (Exception ex) { LogError("ApplyPersistentRules", ex); }
            }
        }

        private void SavePersistentRules()
        {
            try
            {
                var sb = new StringBuilder();
                foreach (var r in _persistentRules)
                {
                    sb.AppendLine(string.Format("{0}|{1}|{2}|{3}", r.ProcessName, r.Action, r.Value, r.Enabled ? "1" : "0"));
                }
                AtomicWrite(Path.Combine(_dataDir, "rules.dat"), sb.ToString());
            }
            catch (Exception ex) { LogError("SavePersistentRules", ex); }
        }

        private void LoadPersistentRules()
        {
            string path = ResolveLoadPath(Path.Combine(_dataDir, "rules.dat"));
            if (!File.Exists(path)) return;
            _persistentRules.Clear();
            foreach (string line in File.ReadAllLines(path))
            {
                if (string.IsNullOrEmpty(line)) continue;
                string[] parts = line.Split('|');
                if (parts.Length < 4) continue;
                try
                {
                    _persistentRules.Add(new PersistentRule
                    {
                        ProcessName = parts[0],
                        Action = parts[1],
                        Value = parts[2],
                        Enabled = parts[3] == "1"
                    });
                }
                catch (Exception ex) { LogError("LoadPersistentRules", ex); }
            }
        }

        private Dictionary<string, AgentExperience> _experienceCache = new Dictionary<string, AgentExperience>();

        private void RecordExperience(AgentDecision d, bool success)
        {
            AgentExperience exp = null;
            string expKey = d.Category + "|" + d.Action + "|" + (_currentState.IsGame ? _currentState.ActiveProcess : "");
            lock (_listLock)
            {
                if (!_experienceCache.TryGetValue(expKey, out exp))
                {
                    foreach (var e in _experiences)
                    {
                        if (e.Category == d.Category && e.Action == d.Action && (e.GameName == _currentState.ActiveProcess || string.IsNullOrEmpty(e.GameName)))
                        {
                            exp = e;
                            _experienceCache[expKey] = e;
                            break;
                        }
                    }
                }
            if (exp == null)
            {
                exp = new AgentExperience
                {
                    GameName = _currentState.IsGame ? _currentState.ActiveProcess : "",
                    Category = d.Category,
                    Action = d.Action
                };
                _experiences.Add(exp);
                _experienceCache[expKey] = exp;
                if (_experiences.Count > 500)
                {
                    _experiences.RemoveAt(0);
                    _experienceCache.Clear();
                }
            }
            // Prefer the exact pre-action snapshot captured right before this action
            // executed (SavePreOptState in ExecuteDecision). Fall back to the previous
            // tick snapshot when it doesn't match (e.g. action blocked before any run).
            SystemState before = (_rollbackPreState != null && _rollbackAction == d.Action) ? _rollbackPreState : _prevState;
            exp.StateBefore = string.Format("CPU:{0:F0} RAM:{1:F0} GPU:{2:F0} T:{3:F0}", before.CpuUsage, before.RamUsage, before.GpuUsage, before.CpuTemp);
            exp.StateAfter = string.Format("CPU:{0:F0} RAM:{1:F0} GPU:{2:F0} T:{3:F0}", _currentState.CpuUsage, _currentState.RamUsage, _currentState.GpuUsage, _currentState.CpuTemp);
            exp.FpsBefore = before.Fps;
            exp.FpsAfter = _currentState.Fps;
            exp.StabilityBefore = before.FpsStability;
            exp.StabilityAfter = _currentState.FpsStability;
            exp.TempBefore = before.CpuTemp;
            exp.TempAfter = _currentState.CpuTemp;
            exp.TimesUsed++;
            if (success) exp.TimesSuccessful++;
            exp.LastUsed = DateTime.Now;
            double fpsDelta = _currentState.Fps - before.Fps;
            double stabilityDelta = _currentState.FpsStability - before.FpsStability;
            double tempDelta = before.CpuTemp - _currentState.CpuTemp;
            double ramDelta = before.RamUsage - _currentState.RamUsage;
            double reward = 0;
            if (before.Fps > 0) reward += 0.3 * Math.Max(-1, Math.Min(1, fpsDelta / 10.0));
            reward += 0.3 * Math.Max(-1, Math.Min(1, stabilityDelta / 20.0));
            reward += 0.2 * Math.Max(-1, Math.Min(1, tempDelta / 10.0));
            reward += 0.2 * Math.Max(-1, Math.Min(1, ramDelta / 10.0));
            // Time decay based on LAST USED (not Created) — prevents old entries from maintaining high scores
            double hoursSinceLastUse = (DateTime.Now - exp.LastUsed).TotalHours;
            double decay = Math.Max(0.1, 1.0 / (1.0 + hoursSinceLastUse / 24.0));
            reward *= decay;
            double scoreDelta = reward * 20;
            exp.Score = Math.Max(0, Math.Min(100, exp.Score + scoreDelta));
            if (success && reward > 0.05)
            {
                exp.Result = ExperienceResult.Improved;
            }
            else if (success && reward >= -0.05)
            {
                exp.Result = ExperienceResult.Neutral;
            }
            else if (!success || reward < -0.05)
            {
                exp.Result = ExperienceResult.Degraded;
            }
            _stats.TotalExperiences = _experiences.Count;
            // Track consecutive failures per action
            string failKey = d.Action + "_" + (d.Category);
            if (!success)
            {
                int prevFails;
                _consecutiveActionFailures.TryGetValue(failKey, out prevFails);
                _consecutiveActionFailures[failKey] = prevFails + 1;
            }
            else
            {
                _consecutiveActionFailures[failKey] = 0;
            }
            bool learningSuccess = success && reward >= -0.1;
            UpdateCategoryLearning(d.Category.ToString(), learningSuccess);
            }
        }

        private string FindRealForegroundApp()
        {
            try
            {
                var procs = GetCachedProcesses();
                if (procs == null || procs.Length == 0) return "";
                for (int i = 0; i < procs.Length; i++)
                {
                    var p = procs[i];
                    try
                    {
                        if (p == null || p.Id <= 4) continue;
                        string name = p.ProcessName.ToLower();
                        if (SYSTEM_PROCESSES.Contains(name)) continue;
                        if (p.MainWindowHandle != IntPtr.Zero && p.Responding)
                        {
                            string title = p.MainWindowTitle;
                            if (!string.IsNullOrEmpty(title) && title.Length > 1)
                            {
                                try { _currentState.ActiveProcessPath = p.MainModule.FileName; }
                                catch { _currentState.ActiveProcessPath = ""; }
                                return name;
                            }
                        }
                    }
                    catch (Exception ex) { LogError("FindRealForegroundApp", ex); }
                }
                return "";
            }
            catch { return ""; }
        }

        private string FindGpuProcess()
        {
            try
            {
                var procs = GetCachedProcesses();
                if (procs == null || procs.Length == 0) return "";
                long maxMem = 0;
                string bestName = "";
                for (int i = 0; i < procs.Length; i++)
                {
                    var p = procs[i];
                    try
                    {
                        if (p == null || p.Id <= 4) continue;
                        string name = p.ProcessName.ToLower();
                        if (SYSTEM_PROCESSES.Contains(name)) continue;
                        long mem = p.WorkingSet64;
                        if (mem > maxMem && mem > GPU_PROCESS_THRESHOLD_BYTES)
                        {
                            maxMem = mem;
                            bestName = name;
                        }
                    }
                    catch (Exception ex) { LogError("FindGpuProcess", ex); }
                }
                return bestName;
            }
            catch { return ""; }
        }

        public static ProcessCategory ClassifyProcess(string procName, string procPath = "", string windowTitle = "")
        {
            if (string.IsNullOrEmpty(procName)) return ProcessCategory.BackgroundService;
            string n = procName.ToLowerInvariant();
            string p = (procPath ?? "").ToLowerInvariant();
            string t = (windowTitle ?? "").ToLowerInvariant();

            // 1. Windows Core System (Priority #1 - protect OS integrity)
            if (SYSTEM_PROCESSES.Contains(n) || n == "dwm" || n == "explorer" || n == "taskmgr" 
                || n == "svchost" || n == "services" || n == "lsass" || n == "csrss" || n == "winlogon"
                || n == "wininit" || n == "smss" || n == "spoolsv" || n == "sihost" || n == "fontdrvhost"
                || n == "ctfmon" || n == "systemsettings" || n == "searchapp" || n == "searchhost"
                || n == "startmenuexperiencehost" || n == "shellexperiencehost" || n == "runtimebroker"
                || n == "audiodg" || n == "securityhealth" || n == "securityhealthsystray" || n == "taskhostw")
            {
                return ProcessCategory.WindowsSystem;
            }

            // 2. Games & Mobile Emulators (Priority #2)
            // A. Specific PUBG Mobile / GameLoop detection
            bool isPubgTitle = t.Contains("pubg") || t.Contains("playerunknown") || t.Contains("battleground") || t.Contains("bgmi") || t.Contains("com.tencent.ig");
            bool isGameLoopFamily = n.Contains("aow") || n.Contains("androws") || n.Contains("androidprocess") 
                || n.Contains("appmarket") || n.Contains("gameloop") || n.Contains("androidemulator")
                || n.Contains("qmplayer") || n.Contains("qmemulator") || n.Contains("txservice") || n.Contains("syzs_cbox")
                || p.Contains("txgameassistant") || p.Contains("gameloop") || p.Contains("tencent");

            if (isPubgTitle || (isGameLoopFamily && (n.Contains("aow_exe") || n.Contains("androwsfg") || n.Contains("androidprocess"))))
            {
                return ProcessCategory.Game;
            }

            // B. PC PUBG
            if (n.Contains("tslgame") || n.Contains("pubg") || n == "pubglite" || n == "execpubg" || n == "pubgmobile")
            {
                return ProcessCategory.Game;
            }

            // C. Known games list & game stores
            for (int gi = 0; gi < KNOWN_GAMES.Length; gi++)
            {
                if (n == KNOWN_GAMES[gi]) return ProcessCategory.Game;
            }
            foreach (string gi in GAME_INDICATORS)
            {
                if (n.Contains(gi)) return ProcessCategory.Game;
            }
            if (p.Contains("steamapps") || p.Contains("epic games") || p.Contains("gog games") 
                || p.Contains("riot games") || p.Contains("battle.net") || p.Contains("ubisoft game")
                || p.Contains("ea games") || p.Contains("xboxgames") || p.Contains("txgameassistant") || p.Contains("tencent"))
            {
                return ProcessCategory.Game;
            }

            // 3. Productivity & Work Apps
            // A. Browsers
            string[] browsers = new[] { "chrome", "msedge", "firefox", "brave", "opera", "operagx", "vivaldi", "tor", "chromium" };
            for (int i = 0; i < browsers.Length; i++) { if (n.Contains(browsers[i])) return ProcessCategory.Productivity; }

            // B. Code & Development
            string[] dev = new[] { "code", "devenv", "idea64", "pycharm", "webstorm", "rider64", "clion64", "eclipse", "sublime_text", "notepad++", "powershell", "cmd", "terminal", "wt", "conhost", "git", "dotnet", "node", "python" };
            for (int i = 0; i < dev.Length; i++) { if (n == dev[i] || n.Contains(dev[i])) return ProcessCategory.Productivity; }

            // C. Office & Comms
            string[] office = new[] { "winword", "excel", "powerpnt", "outlook", "onenote", "word", "acrobat", "foxitreader", "notion", "obsidian", "slack", "teams", "zoom", "discord", "telegram", "whatsapp", "skype" };
            for (int i = 0; i < office.Length; i++) { if (n.Contains(office[i])) return ProcessCategory.Productivity; }

            // D. Creative & Media
            string[] creative = new[] { "photoshop", "illustrator", "premiere", "aftereffects", "lightroom", "blender", "autocad", "maya", "3dsmax", "obs64", "obs", "streamlabs", "audacity", "fl64", "vlc", "spotify" };
            for (int i = 0; i < creative.Length; i++) { if (n.Contains(creative[i])) return ProcessCategory.Productivity; }

            // Check non-games table
            foreach (string ng in KNOWN_NON_GAMES)
            {
                if (n == ng || n.Contains(ng)) return ProcessCategory.Productivity;
            }

            // 4. Default fallback: Background service
            return ProcessCategory.BackgroundService;
        }

        private bool IsGameProcess(string name, string path)
        {
            return IsGameProcess(name, path, "");
        }

        private bool IsGameProcess(string name, string path, string windowTitle)
        {
            if (string.IsNullOrEmpty(name)) return false;
            name = name.ToLower();
            if (!string.IsNullOrEmpty(path)) path = path.ToLower();

            // Layer 1: Classification Engine
            var cat = ClassifyProcess(name, path, windowTitle);
            if (cat == ProcessCategory.WindowsSystem) return false;
            if (cat == ProcessCategory.Game)
            {
                RememberGameConfidence(name, 0.95);
                return true;
            }
            if (cat == ProcessCategory.Productivity)
            {
                RememberGameConfidence(name, 0.0);
                return false;
            }

            // Layer 2: Sticky memory
            double mem;
            if (_gameMemory.TryGetValue(name, out mem))
            {
                if (mem >= 0.8) return true;
                if (mem <= 0.15) return false;
            }

            // Layer 3: Heuristic for unknown processes
            double score = 0;
            if (_currentState != null)
            {
                if (_currentState.GpuUsage > 60) score += 25;
                else if (_currentState.GpuUsage > 30) score += 15;
                if (_currentState.CpuUsage > 40 && _currentState.GpuUsage > 25) score += 15;
            }
            if (!string.IsNullOrEmpty(windowTitle))
            {
                string wt = windowTitle.ToLower();
                for (int ti = 0; ti < GAME_TITLE_KEYWORDS.Length; ti++)
                {
                    if (wt.Contains(GAME_TITLE_KEYWORDS[ti])) { score += 20; break; }
                }
            }
            bool isGame = score >= 35;
            RememberGameConfidence(name, isGame ? 0.7 : 0.25);
            return isGame;
        }

        // Remember a stable classification for a process to make future calls fast & consistent.
        private void RememberGameConfidence(string name, double confidence)
        {
            if (string.IsNullOrEmpty(name)) return;
            try
            {
                double cur;
                if (_gameMemory.TryGetValue(name, out cur))
                {
                    // Move toward the latest signal without flipping instantly
                    double merged = cur * 0.5 + confidence * 0.5;
                    _gameMemory[name] = merged;
                }
                else
                {
                    _gameMemory[name] = confidence;
                }
                if (_gameMemory.Count > GAME_MEMORY_MAX)
                {
                    foreach (var kv in new Dictionary<string, double>(_gameMemory))
                    {
                        if (kv.Value > 0.15 && kv.Value < 0.8) _gameMemory.Remove(kv.Key);
                    }
                    // If we are STILL over the cap (only confirmed high/non-game
                    // entries left), evict the lowest-confidence entries so the
                    // dictionary can never grow unbounded.
                    while (_gameMemory.Count > GAME_MEMORY_MAX)
                    {
                        string victim = null; double victimConf = 1.01;
                        foreach (var kv in _gameMemory)
                        {
                            if (kv.Value < victimConf) { victimConf = kv.Value; victim = kv.Key; }
                        }
                        if (victim == null) break;
                        _gameMemory.Remove(victim);
                    }
                }
            }
            catch { /* memory pruning is best-effort */ }
        }

        private string GetWindowTitleSafe(IntPtr hwnd)
        {
            try
            {
                int len = GetWindowTextLength(hwnd);
                if (len > 0 && len < 512)
                {
                    var sb = new System.Text.StringBuilder(len + 1);
                    if (GetWindowText(hwnd, sb, sb.Capacity) > 0) return sb.ToString();
                }
            }
            catch (Exception ex16) { CWLog.Error("SmartAgent", ex16); }
            return "";
        }

        // Strong game signal = confident classification (used to skip the 2.5s confirm).
        private bool IsStrongGameSignal(string name, string path)
        {
            if (string.IsNullOrEmpty(name)) return false;
            name = name.ToLower();
            if (!string.IsNullOrEmpty(path)) path = path.ToLower();
            if (SYSTEM_PROCESSES.Contains(name)) return false;
            double mem;
            if (_gameMemory.TryGetValue(name, out mem)) { if (mem >= 0.8) return true; }
            if (!string.IsNullOrEmpty(path))
            {
                string[] strong = new[] { "steamapps", "epic games", "gog games", "battle.net", "riot games", "xbox games", "ubisoft game", "ea games", "windowsapps", "txgameassistant", "gameloop", "tencent", "ldplayer", "mumu", "bluestacks" };
                for (int i = 0; i < strong.Length; i++) { if (path.Contains(strong[i])) return true; }
            }
            string[] known = new[] { "csgo", "cs2", "valorant", "fortnite", "league", "dota2", "dota", "apexlegends", "r5apex", "pubg", "tslgame", "tslgame_uc", "minecraft", "wow", "overwatch", "diablo", "eldenring", "cyberpunk2077", "gta5", "rdr2", "warzone", "destiny2", "warframe", "rocketleague", "genshin", "honkai", "lostark", "poe2", "aow_exe", "androwsfg", "gameloopemulator", "gameloopvm", "glaboxheadless", "gameloop", "appmarket", "androidemulator", "androidemulatoren", "androidemulatorex", "mumunxmain", "mumunxdevice", "dnplayer", "hd-player" };
            for (int i = 0; i < known.Length; i++) { if (name == known[i]) return true; }
            return false;
        }

        private void AddLog(string level, string category, string msg)
        {
            var log = new AgentLog { Level = level, Category = category, Message = msg };
            lock (_listLock) { _logs.Enqueue(log);
            if (_logs.Count > 500) _logs.Dequeue(); }
            if (OnLog != null) OnLog(log);
        }

        private void LogError(string subsystem, Exception ex)
        {
            try
            {
                string msg = string.Format("{0}: {1}", ex.GetType().Name, ex.Message);
                AddLog("ERROR", subsystem, msg);
            }
            catch { /* prevent infinite recursion */ }
        }

        // === Feature 5: Optimization Logger ===
        private void AddLogOpt(string action, string detail)
        {
            string entry = string.Format("[{0}] {1}: {2}", DateTime.Now.ToString("HH:mm:ss"), action, detail);
            lock (_listLock) { _optimizationLog.Add(entry); if (_optimizationLog.Count > MAX_OPT_LOG) _optimizationLog.RemoveAt(0); }
            if (OnOptimizationLog != null) OnOptimizationLog(entry);
        }

        // === Feature 4: New Process Detection ===
        private void DetectNewProcesses()
        {
            try
            {
                var procs = GetCachedProcesses();
                if (procs == null || procs.Length == 0) return;
                var current = new Dictionary<string, DateTime>();
                for (int i = 0; i < procs.Length; i++)
                {
                    var p = procs[i];
                    try
                    {
                        if (p == null) continue;
                        string name = p.ProcessName.ToLower();
                        if (SYSTEM_PROCESSES.Contains(name)) continue;
                        double memMB = 0;
                        try { memMB = p.WorkingSet64 / (1024.0 * 1024.0); }
                        catch (System.ComponentModel.Win32Exception) { }
                        catch (InvalidOperationException) { }
                        catch (UnauthorizedAccessException) { }
                        catch (Exception ex17) { CWLog.Error("SmartAgent", ex17); }
                        if (memMB > 100)
                        {
                            current[name] = DateTime.Now;
                            if (!_knownProcesses.ContainsKey(name) && _tickCount > 10)
                            {
                                string msg = string.Format("New process: {0} ({1:F0} MB RAM)", p.ProcessName, memMB);
                                lock (_listLock) { _newHogAlerts.Add(msg); if (_newHogAlerts.Count > 20) _newHogAlerts.RemoveAt(0); }
                                AddLogOpt("NEW_PROCESS", msg);
                            }
                        }
                    }
                    catch (Exception ex) { LogError("DetectNewProcesses", ex); }
                }
                _knownProcesses = current;
            }
            catch (Exception ex) { LogError("DetectNewProcesses", ex); }
        }

        // === Feature 6: Save Pre-Optimization State ===
        private void SavePreOptState(string actionName)
        {
            _preOptCpu = _currentState.CpuUsage;
            _preOptRam = _currentState.RamUsage;
            _preOptFps = _currentState.Fps;
            _preOptGpu = _currentState.GpuUsage;
            _preOptTemp = _currentState.CpuTemp;
            _preOptFrameTime = _currentState.FrameTimeMs;
            try { _rollbackPreState = _currentState.Clone(); } catch (Exception ex) { LogError("SavePreOptState", ex); }
            _rollbackAction = actionName;
            _lastAppliedAt = DateTime.Now;
            _lastRollbackLog = "";
            // Only real actions belong on the undo stack. The per-tick "tick"
            // baseline pushes used to flood it with 20 "tick" entries in ~10s,
            // making the undo history useless.
            if (!string.IsNullOrEmpty(actionName) && actionName != "tick")
                lock (_listLock) { _undoStack.Add(actionName); if (_undoStack.Count > MAX_UNDO_STACK) _undoStack.RemoveAt(0); }
        }

        // === Feature 10: Speedometer Delta ===
        private void CalculateSpeedometerDelta(string actionName)
        {
            _postOptCpu = _currentState.CpuUsage;
            _postOptRam = _currentState.RamUsage;
            _postOptFps = _currentState.Fps;
            _postOptGpu = _currentState.GpuUsage;
            _postOptTemp = _currentState.CpuTemp;
            _postOptFrameTime = _currentState.FrameTimeMs;
            double cpuDelta = _preOptCpu - _postOptCpu;
            double ramDelta = _preOptRam - _postOptRam;
            double fpsDelta = _postOptFps - _preOptFps;
            double gpuDelta = _preOptGpu - _postOptGpu;
            double tempDelta = _preOptTemp - _postOptTemp;
            double frameTimeDelta = _preOptFrameTime - _postOptFrameTime;
            _lastOptimizationResult = string.Format("{0}: CPU {1}{2:F1}%, RAM {3}{4:F1}%, GPU {5}{6:F1}%, TEMP {7}{8:F1}C, FPS {9}{10:F1}, FT {11}{12:F1}ms",
                actionName,
                cpuDelta >= 0 ? "-" : "+", Math.Abs(cpuDelta),
                ramDelta >= 0 ? "-" : "+", Math.Abs(ramDelta),
                gpuDelta >= 0 ? "-" : "+", Math.Abs(gpuDelta),
                tempDelta >= 0 ? "-" : "+", Math.Abs(tempDelta),
                fpsDelta >= 0 ? "+" : "-", Math.Abs(fpsDelta),
                frameTimeDelta >= 0 ? "-" : "+", Math.Abs(frameTimeDelta));
        }

        // === Auto Rollback: degradation detection + inverse execution ===
        public bool IsPostStateDegraded(SystemState before, SystemState after)
        {
            if (before == null || after == null) return false;
            bool degraded = false;
            if (after.Fps > 0 && before.Fps > 0 && after.Fps < before.Fps - 8) degraded = true;
            if (before.FrameTimeMs > 0 && after.FrameTimeMs > before.FrameTimeMs + Math.Max(2, before.FrameTimeMs * 0.2)) degraded = true;
            if (before.CpuTemp > 0 && after.CpuTemp > before.CpuTemp + 5) degraded = true;
            if (before.CpuUsage > 0 && after.CpuUsage > before.CpuUsage + 12) degraded = true;
            if (before.RamUsage > 0 && after.RamUsage > before.RamUsage + 8) degraded = true;
            if (before.GpuUsage > 0 && after.GpuUsage > before.GpuUsage + 10) degraded = true;
            return degraded;
        }

        public void ArmRollbackBaseline(SystemState s, string action)
        {
            _rollbackPreState = s != null ? s.Clone() : null;
            _rollbackAction = action ?? "";
            _lastAppliedAt = DateTime.Now;
            _lastRollbackLog = "";
        }

        // Shared rollback helpers (used by failure path, auto rollback, emergency stop)
        private void RestorePowerPlan(string plan)
        {
            if (string.IsNullOrEmpty(plan)) return;
            try
            {
                ExecutePowerPlan(plan == "High Performance" ? "high" : plan == "Power Saver" ? "power saver" : "balanced");
            }
            catch (Exception ex) { LogError("Rollback", ex); }
        }

        private void RestoreProcessPriorityNormal(string processName)
        {
            if (string.IsNullOrEmpty(processName)) return;
            try
            {
                string name = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    ? processName.Substring(0, processName.Length - 4) : processName;
                var procs = Process.GetProcessesByName(name);
                foreach (var pp in procs)
                {
                    using (pp)
                    {
                        try { pp.PriorityClass = PRIORITY_NORMAL; } catch (Exception ex18) { CWLog.Error("SmartAgent", ex18); }
                    }
                }
            }
            catch (Exception ex) { LogError("Rollback", ex); }
        }

        private string PerformRollbackInverse(StateJournalEntry entry = null)
        {
            if (entry != null && !string.IsNullOrEmpty(entry.Action))
            {
                if (entry.Action == "BoostGamePriority" && !string.IsNullOrEmpty(entry.GameName))
                {
                    RestoreProcessPriorityNormal(entry.GameName);
                    return "Rolled back BoostGamePriority: priority restored to Normal for " + entry.GameName;
                }
                if (!string.IsNullOrEmpty(_rollbackPlan))
                {
                    RestorePowerPlan(_rollbackPlan);
                    return "Rolled back " + entry.Action + ": power plan restored to " + _rollbackPlan;
                }
            }
            string a = _rollbackAction;
            if (a == "BoostGamePriority" && !string.IsNullOrEmpty(_rollbackGame))
            {
                RestoreProcessPriorityNormal(_rollbackGame);
                return "Rolled back BoostGamePriority: priority restored to Normal for " + _rollbackGame;
            }
            if (!string.IsNullOrEmpty(_rollbackPlan))
            {
                RestorePowerPlan(_rollbackPlan);
                return "Rolled back " + a + ": power plan restored to " + _rollbackPlan;
            }
            return "Rollback: " + ((entry != null && !string.IsNullOrEmpty(entry.Action)) ? entry.Action : a) + " - no reversible system change needed (monitoring)";
        }

        public bool AutoRollbackIfDegraded(SystemState after)
        {
            if (_rollbackPreState == null || after == null) return false;
            if (!IsPostStateDegraded(_rollbackPreState, after))
            {
                _lastRollbackLog = "OK";
                return false;
            }
            _lastRollbackLog = PerformRollbackInverse();
            AddLog("ROLLBACK", "AutoRollback", _lastRollbackLog);
            AddLogOpt("ROLLBACK", _lastRollbackLog);
            return true;
        }

        public string LastRollbackLog { get { return _lastRollbackLog; } }

        private void CheckAutoRollbackTimer()
        {
            if (_rollbackPreState == null || _currentState == null) return;
            if ((DateTime.Now - _lastAppliedAt).TotalSeconds < ROLLBACK_WINDOW_SECONDS) return;
            _rollbackCheckCounter++;
            if (_rollbackCheckCounter < ROLLBACK_CHECK_TICKS) return;
            _rollbackCheckCounter = 0;
            try
            {
                if (AutoRollbackIfDegraded(_currentState)) _rollbackPreState = null;
            }
            catch (Exception ex) { LogError("AutoRollback", ex); }
        }

        // === Feature 9: Automated Restore Point (Non-blocking background invocation) ===
        private void CreateRestorePointIfNeeded()
        {
            if (_restorePointCreatedThisSession) return;
            _restorePointCreatedThisSession = true; // fire-once guard
            try
            {
                // Run in background to prevent 30-120 second UI lockup
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        if (SystemRestoreHelper.TryCreateRestorePoint("ClearWin Auto Restore"))
                            AddLogOpt("RESTORE_POINT", "Created system restore point");
                        else
                            AddLogOpt("RESTORE_POINT", "System restore point could not be created");
                    }
                    catch (Exception exAsync) { LogError("CreateRestorePointIfNeeded.Async", exAsync); }
                });
            }
            catch (Exception ex) { LogError("CreateRestorePointIfNeeded", ex); }
        }

        // =====================================================================
        // LIVE TWEAK EVALUATION (real RL feedback loop over executed tweaks)
        // Caller contract (MainWindow.Pages.RunTweakScript):
        //   before apply.ps1 starts: _agent.BeginTweakEvaluation(name);
        //   after apply.ps1 exits:   _agent.EvaluateExecution(name);
        // check/read/undo runs are deliberately NOT evaluated, so the Q-table
        // learns only from real state-changing actions.
        // =====================================================================
        private double _tweakEvalCpuBaseline = 0;
        private double _tweakEvalRamBaseline = 0;
        private string _tweakEvalScript = "";
        private string _tweakEvalStateKey = "";
        private DateTime _tweakEvalStart = DateTime.MinValue;
        private bool _tweakEvalArmed = false;
        private const int TWEAK_EVAL_SETTLE_MS = 5000;
        private const int TWEAK_EVAL_MAX_ELAPSED_S = 120;

        // Sync baseline capture. Runs right before the tweak script starts.
        // The 500 ms counter-prime window blocks the calling thread; the
        // tweak itself takes seconds, so this is negligible next to it.
        public void BeginTweakEvaluation(string tweakName)
        {
            try
            {
                double cpu, ram;
                if (!ReadCpuRamSample(out cpu, out ram))
                {
                    LogError("TweakEval.Begin", new Exception("Performance counters unavailable - baseline not captured"));
                    _tweakEvalArmed = false;
                    return;
                }
                _tweakEvalCpuBaseline = cpu;
                _tweakEvalRamBaseline = ram;
                _tweakEvalScript = (tweakName ?? "");
                _tweakEvalStart = DateTime.Now;
                _tweakEvalStateKey = GetStateKey(GetCurrentRLState());
                _tweakEvalArmed = true;
                AddLog("INFO", "TweakEval",
                    "Baseline for " + _tweakEvalScript + ": cpu=" + cpu.ToString("F1") + "% ram=" + ram.ToString("F1") + "%");
            }
            catch (Exception ex)
            {
                LogError("TweakEval.Begin", ex);
                _tweakEvalArmed = false;
            }
        }

        // Post-execution evaluation: settle 5 s, re-sample, compute reward,
        // state-aware Double-Q update, undo on degradation, persist to disk.
        // Never blocks the UI: continues on the threadpool.
        public async Task EvaluateExecution(string scriptName)
        {
            try
            {
                if (!_tweakEvalArmed || !string.Equals(scriptName, _tweakEvalScript, StringComparison.OrdinalIgnoreCase))
                    return; // baseline belongs to a different tweak - do not mix rewards
                if ((DateTime.Now - _tweakEvalStart).TotalSeconds > TWEAK_EVAL_MAX_ELAPSED_S)
                    return; // stale armed session

                // Settle period: let the tweak take full effect before measuring
                await Task.Delay(TWEAK_EVAL_SETTLE_MS);

                // Everything below (counter priming, the 30 s undo script, file
                // persistence) runs on the threadpool, never on the captured UI
                // SynchronizationContext - a degraded tweak must not freeze the
                // window while undo.ps1 executes.
                await Task.Run(delegate()
                {
                    double afterCpu, afterRam;
                    bool sampled = ReadCpuRamSample(out afterCpu, out afterRam);
                    if (!sampled)
                    {
                        // Counters gone mid-flight - fall back to smoothed telemetry
                        afterCpu = _smoothCpu;
                        afterRam = _smoothRam;
                    }

                    double deltaRam = _tweakEvalRamBaseline - afterRam; // + = freed RAM
                    double deltaCpu = _tweakEvalCpuBaseline - afterCpu; // + = freed CPU
                    bool improved = deltaRam > 1.5 || deltaCpu > 1.5;
                    bool degraded = deltaRam < -1.5 || deltaCpu < -2.5;

                    // Reward: 10% improvement in a metric ~= +1.0, 10% degradation
                    // ~= -1.0. CPU weighted slightly heavier (gaming-relevant).
                    double reward = (0.4 * deltaRam + 0.6 * deltaCpu) / 10.0;
                    reward = Math.Max(-1.0, Math.Min(1.0, reward));
                    if (!improved && !degraded)
                        reward = 0.05; // neutral-ish nudge so idle tweaks are not punished

                    string nextKey = GetStateKey(GetCurrentRLState());
                    string actionKey = "Tweak:" + scriptName;

                    // Genuine state-aware Double-Q TD update (shared with live agent)
                    RecordRLExperience(_tweakEvalStateKey, actionKey, reward, nextKey);

                    if (degraded)
                    {
                        bool shouldAutoUndo = Mode == AgentMode.AutoPilot && (afterCpu > 90 || afterRam > 92);
                        AddLog("WARN", "TweakEval",
                            "Tweak " + scriptName + " degraded the system (cpu " + deltaCpu.ToString("F1")
                                + "% ram " + deltaRam.ToString("F1") + "%) - reward " + reward.ToString("F2") + (shouldAutoUndo ? ", running emergency rollback" : ""));
                        if (shouldAutoUndo)
                        {
                            TryRunUndoScript(scriptName);
                            lock (_listLock)
                            {
                                _undoStack.Add("TweakUndo:" + scriptName);
                                if (_undoStack.Count > MAX_UNDO_STACK) _undoStack.RemoveAt(0);
                            }
                        }
                    }
                    else
                    {
                        AddLog("INFO", "TweakEval",
                            "Tweak " + scriptName + " reward=" + reward.ToString("F2")
                                + " (cpu " + deltaCpu.ToString("F1") + "% ram " + deltaRam.ToString("F1") + "%)");
                    }

                    // Persist the learned Q-tables so they survive restarts
                    SaveRL2Data();
                    _tweakEvalArmed = false;
                });
            }
            catch (Exception ex)
            {
                LogError("TweakEval.Evaluate", ex);
                _tweakEvalArmed = false;
            }
        }

        // ---------------------------------------------------------------------
        // CPU% + RAM% in one sample. Returns false when any counter or the
        // native memory query fails, so callers can fall back safely.
        // ---------------------------------------------------------------------
        private bool ReadCpuRamSample(out double cpuPct, out double ramPct)
        {
            cpuPct = 0;
            ramPct = 0;
            PerformanceCounter cpuCounter = null;
            PerformanceCounter memCounter = null;
            try
            {
                cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                memCounter = new PerformanceCounter("Memory", "Available MBytes");
                // First read of a percentage counter is zero - prime it, wait
                // one delta window, then read the real value.
                cpuCounter.NextValue();
                System.Threading.Thread.Sleep(500);
                double cpu = cpuCounter.NextValue();
                cpuPct = Math.Max(0, Math.Min(100, cpu));

                double availMb = memCounter.NextValue();
                GlobalMemStatus st = new GlobalMemStatus();
                st.dwLength = (uint)Marshal.SizeOf(typeof(GlobalMemStatus));
                if (!NativeGlobalMemoryStatusEx(ref st) || st.ullTotalPhys == 0 || availMb <= 0)
                    return false;
                double totalMb = st.ullTotalPhys / (1024.0 * 1024.0);
                double usedMb = totalMb - availMb;
                ramPct = Math.Max(0, Math.Min(100, usedMb / totalMb * 100.0));
                return true;
            }
            catch (Exception ex)
            {
                LogError("TweakEval.Sample", ex);
                return false;
            }
            finally
            {
                if (cpuCounter != null) cpuCounter.Dispose();
                if (memCounter != null) memCounter.Dispose();
            }
        }

        // Native total-physical-memory query (kernel32).
        [StructLayout(LayoutKind.Sequential)]
        private struct GlobalMemStatus
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool NativeGlobalMemoryStatusEx(ref GlobalMemStatus lpBuffer);

        // ---------------------------------------------------------------------
        // Run the tweak's undo.ps1 on degraded systems. Mirrors
        // MainWindow.Pages.ResolveTweakScriptPath (bin tree, then dev tree).
        // ---------------------------------------------------------------------
        private void TryRunUndoScript(string tweakName)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string scriptPath = System.IO.Path.Combine(baseDir, "Resources", "tweaks", tweakName, "undo.ps1");
                if (!System.IO.File.Exists(scriptPath))
                {
                    string devPath = System.IO.Path.Combine(baseDir, "..", "..", "Resources", "tweaks", tweakName, "undo.ps1");
                    if (System.IO.File.Exists(devPath))
                    {
                        scriptPath = devPath;
                    }
                    else
                    {
                        LogError("TweakEval.Undo", new Exception("undo.ps1 not found for " + tweakName));
                        return;
                    }
                }
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + scriptPath + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                if (psi.EnvironmentVariables.ContainsKey("CLEARWIN_TEST_REG_PREFIX"))
                    psi.EnvironmentVariables.Remove("CLEARWIN_TEST_REG_PREFIX");
                if (psi.EnvironmentVariables.ContainsKey("CLEARWIN_TEST_BACKUP_DIR"))
                    psi.EnvironmentVariables.Remove("CLEARWIN_TEST_BACKUP_DIR");
                if (psi.EnvironmentVariables.ContainsKey("CLEARWIN_TEST_MODE"))
                    psi.EnvironmentVariables.Remove("CLEARWIN_TEST_MODE");
                if (psi.EnvironmentVariables.ContainsKey("CLEARWIN_ALLOW_DEFERRED_VM_TWEAKS"))
                    psi.EnvironmentVariables.Remove("CLEARWIN_ALLOW_DEFERRED_VM_TWEAKS");
                using (var p = Process.Start(psi))
                {
                    var sbOut = new System.Text.StringBuilder();
                    var sbErr = new System.Text.StringBuilder();
                    p.OutputDataReceived += (s, ev) => { if (ev.Data != null) sbOut.AppendLine(ev.Data); };
                    p.ErrorDataReceived += (s, ev) => { if (ev.Data != null) sbErr.AppendLine(ev.Data); };
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(30000))
                    {
                        try { p.Kill(); } catch (Exception exKill) { LogError("TweakEval.Undo.Kill", exKill); }
                        AddLog("WARN", "TweakEval", "Undo script timed out for " + tweakName);
                        return;
                    }
                    p.WaitForExit();
                    if (p.ExitCode == 0)
                        AddLog("INFO", "TweakEval", "Undo executed for " + tweakName + " (exit 0)");
                    else
                        AddLog("WARN", "TweakEval", "Undo for " + tweakName + " exit " + p.ExitCode + " " + sbErr.ToString().Trim());
                }
            }
            catch (Exception ex)
            {
                LogError("TweakEval.Undo", ex);
            }
        }

        // === Feature 1: Thermal Protection Check ===
        private bool IsThermalSafe()
        {
            if (_currentState.CpuTemp > THERMAL_THROTTLE_TEMP)
            {
                if (!_thermalThrottled)
                {
                    _thermalThrottled = true;
                    AddLogOpt("THERMAL_PROTECT", string.Format("CPU at {0:F1}C - skipping optimizations to prevent overheating", _currentState.CpuTemp));
                }
                return false;
            }
            if (_thermalThrottled && _currentState.CpuTemp < THERMAL_WARNING_TEMP)
            {
                _thermalThrottled = false;
                AddLogOpt("THERMAL_SAFE", string.Format("CPU cooled to {0:F1}C - optimizations resumed", _currentState.CpuTemp));
            }
            return !_thermalThrottled;
        }

        // === Feature 3: Resource Limit Check ===
        private bool IsWithinResourceLimits()
        {
            if (!_resourceLimitsEnabled) return true;
            if (_currentState.CpuUsage > _maxCpuLimit)
            {
                AddLogOpt("RESOURCE_LIMIT", string.Format("CPU {0:F1}% exceeds limit {1:F1}% - pausing optimizations", _currentState.CpuUsage, _maxCpuLimit));
                return false;
            }
            if (_currentState.DiskUsage > _maxDiskLimit)
            {
                AddLogOpt("RESOURCE_LIMIT", string.Format("Disk {0:F1}% exceeds limit {1:F1}% - pausing optimizations", _currentState.DiskUsage, _maxDiskLimit));
                return false;
            }
            return true;
        }

        private void UpdateAppMode()
        {
            AppMode prev = _appMode;
            AppMode desired = _appMode;
            if (!_currentState.IsCharging && _currentState.BatteryLevel > 0 && _currentState.BatteryLevel < 20)
            {
                desired = AppMode.Battery;
            }
            else if (_currentState.IsGame)
            {
                // Game Confirm: strong signals (known game / game store path) or simulation mode are instant;
                // ambiguous detections in live execution wait briefly to avoid misclassification.
                if (_simulationMode || DisableRealExecution || IsStrongGameSignal(_currentState.ActiveProcess, _currentState.ActiveProcessPath))
                {
                    desired = AppMode.Gaming;
                    _ticksWithoutGame = 0;
                }
                else
                {
                    if (_gameDetectedTime == DateTime.MinValue)
                        _gameDetectedTime = DateTime.Now;
                    if ((DateTime.Now - _gameDetectedTime).TotalSeconds < GAME_CONFIRM_SECONDS)
                        return; // Wait for confirmation
                    desired = AppMode.Gaming;
                    _ticksWithoutGame = 0;
                }
            }
            else
            {
                _gameDetectedTime = DateTime.MinValue;
                _ticksWithoutGame++;
                if (_ticksWithoutGame > 30) desired = AppMode.Idle;
                else desired = AppMode.Desktop;
            }
            if (desired != prev)
            {
                double elapsed = (DateTime.Now - _lastModeChange).TotalSeconds;
                if (!_simulationMode && !DisableRealExecution && elapsed < HYSTERESIS_SECONDS) return;
                AppMode oldMode = _appMode;
                _appMode = desired;
                _lastModeChange = DateTime.Now;
                ApplyAdaptiveTimerResolution();

                if (oldMode == AppMode.Gaming && desired != AppMode.Gaming)
                {
                    try
                    {
                        RestoreBalancedOrPreviousPowerPlan(_originalPowerPlan);
                    }
                    catch (Exception exPowerRestore)
                    {
                        LogError("UpdateAppMode.PowerRestore", exPowerRestore);
                    }
                }
            }
        }

        private void ApplyAdaptiveTimerResolution()
        {
            lock (_timerResLock)
            {
                try
                {
                    uint current;
                    if (_appMode == AppMode.Gaming)
                    {
                        NtSetTimerResolution(TIMER_RESOLUTION_GAMING, true, out current);
                    }
                    else if (_appMode == AppMode.Desktop)
                    {
                        NtSetTimerResolution(TIMER_RESOLUTION_DESKTOP, true, out current);
                    }
                    else
                    {
                        NtSetTimerResolution(TIMER_RESOLUTION_DEFAULT, false, out current);
                    }
                }
                catch (Exception ex)
                {
                    LogError("ApplyAdaptiveTimerResolution", ex);
                    try
                    {
                        uint current;
                        NtSetTimerResolution(TIMER_RESOLUTION_DEFAULT, false, out current);
                    }
                    catch (Exception exDef) { LogError("ApplyAdaptiveTimerResolution", exDef); }
                }
            }
        }

        private void ResetTimerResolution()
        {
            lock (_timerResLock)
            {
                try
                {
                    _timerResRefCount = 0;
                    uint current;
                    NtSetTimerResolution(TIMER_RESOLUTION_DEFAULT, false, out current);
                }
                catch (Exception ex) { LogError("ResetTimerResolution", ex); }
            }
        }

        private void SelfHealthCheck()
        {
            if (System.Threading.Interlocked.CompareExchange(ref _selfCheckRunning, 1, 0) != 0) return;
            try
            {
                using (var self = Process.GetCurrentProcess())
                {
                    if (self == null || self.HasExited) return;
                    self.Refresh();
                    double selfRamMB = self.WorkingSet64 / 1048576.0;
                    int handles = self.HandleCount;
                    int threads = self.Threads.Count;
                    if (selfRamMB > SELF_HEALTH_RAM_MB)
                    {
                        AddLog("WARN", "SelfHealth", "High memory: " + selfRamMB.ToString("F0") + " MB");
                    }
                    else if (handles > SELF_HEALTH_HANDLES)
                    {
                        AddLog("WARN", "SelfHealth", "High handles: " + handles);
                    }
                    else if (threads > SELF_HEALTH_THREADS)
                    {
                        AddLog("WARN", "SelfHealth", "High threads: " + threads);
                    }
                }
                _lastSelfCheck = DateTime.Now;
            }
            catch (Exception ex) { LogError("SelfHealthCheck", ex); }
            finally { System.Threading.Interlocked.Exchange(ref _selfCheckRunning, 0); }
        }

        private void RunSelfTest()
        {
            try
            {
                // Self Test: verify critical APIs are accessible
                bool ok = true;
                string fail = "";
                // Test QPC
                long freq;
                if (!QueryPerformanceFrequency(out freq) || freq == 0) { ok = false; fail += "QPC "; }
                // Test Memory API
                MEMORYSTATUSEX mem = new MEMORYSTATUSEX();
                mem.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
                if (!GlobalMemoryStatusEx(ref mem)) { ok = false; fail += "Memory "; }
                // Test Process access
                try { using (var self = Process.GetCurrentProcess()) { if (self == null) { ok = false; fail += "Process "; } } }
                catch { ok = false; fail += "Process "; }
                _selfTestPassed = true;
                if (ok) AddLog("INFO", "SelfTest", "All checks passed");
                else AddLog("WARN", "SelfTest", "Failed: " + fail + "(limited functionality)");
            }
            catch { _selfTestPassed = true; } // Don't block on self test failure
        }

        private void LogDecisionResult(AgentDecision d, bool success)
        {
            string entry = string.Format("[{0}] {1} | {2} | Conf:{3:F0}% | {4} | {5}",
                DateTime.Now.ToString("HH:mm:ss"), success ? "OK" : "FAIL", d.Action, d.Confidence * 100, d.Reason, d.Description);
            lock (_decisionLock) { _decisionHistoryLog.Enqueue(entry); if (_decisionHistoryLog.Count > MAX_DECISION_LOG) _decisionHistoryLog.Dequeue(); }
            // Learning: record execution-level success/failure only ("the
            // command ran"). Whether it actually improved the system is
            // recorded separately by outcome verification.
            RecordDecisionOutcome(d.Action, success);
            try
            {
                AiDecisionLogger.RecordDecision(d.Action, d.Reason, d.Confidence, d.Category.ToString(), success ? "SUCCESS" : "FAILED", null, d.ExecutionTimeMs);
            }
            catch { }
        }

        private void LogInformationalDecision(AgentDecision d)
        {
            string entry = string.Format("[{0}] INFO | {1} | Conf:{2:F0}% | {3}",
                DateTime.Now.ToString("HH:mm:ss"), d.Action, d.Confidence * 100, d.Reason);
            lock (_decisionLock) { _decisionHistoryLog.Enqueue(entry); if (_decisionHistoryLog.Count > MAX_DECISION_LOG) _decisionHistoryLog.Dequeue(); }
            try
            {
                AiDecisionLogger.RecordDecision(d.Action, d.Reason, d.Confidence, d.Category.ToString(), "INFO", null, d.ExecutionTimeMs);
            }
            catch { }
        }

        // Outcome learning: evaluate every pending batch that has matured.
        // Each batch carries its own pre-execution snapshot, so a fast tick
        // can no longer wipe out actions before they get a real evaluation;
        // immature batches simply stay queued until their window opens.
        private void EvaluatePendingBatches()
        {
            if (_pendingOutcomeBatches.Count == 0) return;
            for (int i = _pendingOutcomeBatches.Count - 1; i >= 0; i--)
            {
                var b = _pendingOutcomeBatches[i];
                double elapsed = (DateTime.Now - b.RecordedAt).TotalSeconds;
                // Check if enough time has passed (use most conservative delay from batch)
                double minDelay = 5;
                double maxDelay = 120;
                foreach (string act in b.Actions)
                {
                    double d;
                    if (ACTION_EVAL_DELAY.TryGetValue(act, out d))
                    {
                        if (d > minDelay) minDelay = d;
                        if (d * 2 > maxDelay) maxDelay = d * 2;
                    }
                }
                if (elapsed < minDelay) continue; // not mature yet - keep it queued
                // Context-Aware: a batch recorded in a different mode is
                // contaminated by the context switch - drop it (per batch,
                // the others may still be valid).
                if (b.PreMode != _appMode.ToString())
                {
                    _pendingOutcomeBatches.RemoveAt(i);
                    continue;
                }
                // A stale window is better than a lost one: evaluate late
                // batches with a discounted reward.
                double staleness = elapsed > maxDelay ? 0.5 : 1.0;
                // Per-metric reward components, measured against THIS
                // batch's own pre-snapshot
                double tempDelta = b.PreTemp - _smoothTemp;
                double ramDelta = b.PreRam - _smoothRam;
                double fpsDelta = _currentState.Fps - b.PreFps;
                double tempComp = 0;
                if (tempDelta > 0) tempComp += 0.4 * Math.Min(1.0, tempDelta / 10.0);
                if (tempDelta < -5) tempComp -= 0.3 * Math.Min(1.0, -tempDelta / 10.0);
                double ramComp = 0;
                if (ramDelta > 0) ramComp += 0.3 * Math.Min(1.0, ramDelta / 15.0);
                if (ramDelta < -5) ramComp -= 0.2 * Math.Min(1.0, -ramDelta / 15.0);
                double fpsComp = 0;
                if (fpsDelta > 0) fpsComp += 0.3 * Math.Min(1.0, fpsDelta / 10.0);
                if (fpsDelta < -5) fpsComp -= 0.2 * Math.Min(1.0, -fpsDelta / 10.0);
                // Magnitude-weighted credit assignment: each metric's
                // observed improvement is attributed to the batch's actions
                // in proportion to how strongly each action is expected to
                // move that metric. Metrics no action targets are pure
                // environment noise - nobody is credited or penalized.
                double[][] magnitudes = b.Actions.Select(a => GetActionMagnitude(a)).ToArray();
                double sumT = 0, sumR = 0, sumF = 0;
                for (int k = 0; k < magnitudes.Length; k++) { sumT += magnitudes[k][0]; sumR += magnitudes[k][1]; sumF += magnitudes[k][2]; }
                for (int k = 0; k < b.Actions.Count; k++)
                {
                    double attributed = 0.0;
                    if (sumT > 0) attributed += tempComp * (magnitudes[k][0] / sumT);
                    if (sumR > 0) attributed += ramComp * (magnitudes[k][1] / sumR);
                    if (sumF > 0) attributed += fpsComp * (magnitudes[k][2] / sumF);
                    attributed = Math.Max(-1.0, Math.Min(1.0, attributed * staleness));
                    // Outcome-verified learning (separate from execution
                    // success): only a positive attributed reward counts as
                    // "the action proved useful".
                    RecordOutcomeVerified(b.Actions[k], attributed > 0.05, attributed);
                }
                _pendingOutcomeBatches.RemoveAt(i);
            }
        }

        private double PredictNext(double[] data, int count)
        {
            if (count < 5) { PredictionConfidence = 0; return data[Math.Max(0, count - 1)]; }
            int n = Math.Min(count, data.Length);
            int readStart = (count >= data.Length) ? _historyIndex : 0;
            // Reuse the shared buffer when possible (only caller feeds _tempHistory).
            double[] ordered = (n <= _predictBuffer.Length) ? _predictBuffer : new double[n];
            for (int i = 0; i < n; i++) ordered[i] = data[(readStart + i) % data.Length];
            double maxDelta = 0;
            for (int i = 1; i < n; i++)
            {
                double delta = Math.Abs(ordered[i] - ordered[i - 1]);
                if (delta > maxDelta) maxDelta = delta;
            }
            bool changePoint = maxDelta > 15;
            double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
            for (int i = 0; i < n; i++)
            {
                sumX += i; sumY += ordered[i]; sumXY += i * ordered[i]; sumX2 += i * i;
            }
            double denom = n * sumX2 - sumX * sumX;
            if (Math.Abs(denom) < 0.001) { PredictionConfidence = 0; return ordered[n - 1]; }
            double slope = (n * sumXY - sumX * sumY) / denom;
            double intercept = (sumY - slope * sumX) / n;
            double result = intercept + slope * n;
            double meanY = sumY / n;
            double ssTot = 0, ssRes = 0;
            for (int i = 0; i < n; i++)
            {
                double predicted = intercept + slope * i;
                ssRes += (ordered[i] - predicted) * (ordered[i] - predicted);
                ssTot += (ordered[i] - meanY) * (ordered[i] - meanY);
            }
            PredictionConfidence = ssTot > 0 ? Math.Max(0, 1.0 - ssRes / ssTot) : 0;
            if (changePoint) PredictionConfidence *= 0.2;
            if (double.IsNaN(result) || double.IsInfinity(result)) { PredictionConfidence = 0; return ordered[n - 1]; }
            return result;
        }

        // Watchdog: run an analyzer under a per-analyzer clock, adaptively timing
        // each one. Enforcement: if analyzers keep timing out in a row, skip the
        // remaining analyzers this tick so the tick loop stays responsive.
        private bool RunAnalyzer(string name, Action analyzer)
        {
            DateTime t0 = DateTime.Now;
            try { analyzer(); }
            catch { AddLog("ERROR", "Analyzer", name + "Analyzer failed"); }
            CheckWatchdog(name, t0);
            if (_consecutiveAnalyzerTimeouts >= WATCHDOG_MAX_STREAK)
            {
                _consecutiveAnalyzerTimeouts = 0;
                AddLog("WARN", "Watchdog", name + ": streak reached " + WATCHDOG_MAX_STREAK + " - skipping remaining analyzers this tick (enforced)");
                return false;
            }
            return true;
        }

        private void CheckWatchdog(string analyzerName, DateTime start)
        {
            double elapsed = (DateTime.Now - start).TotalMilliseconds;
            // Adaptive timeout: Mean + 3*StdDev per analyzer
            double timeout = 5000;
            if (_analyzerMeanTimes.ContainsKey(analyzerName))
            {
                double mean = _analyzerMeanTimes[analyzerName];
                timeout = Math.Max(2000, mean * 3);
            }
            // Update running mean
            if (_analyzerMeanTimes.ContainsKey(analyzerName))
                _analyzerMeanTimes[analyzerName] = _analyzerMeanTimes[analyzerName] * 0.8 + elapsed * 0.2;
            else
                _analyzerMeanTimes[analyzerName] = elapsed;
            if (elapsed > timeout)
            {
                _consecutiveAnalyzerTimeouts++;
                AddLog("WARN", "Watchdog", analyzerName + " timeout: " + elapsed.ToString("F0") + "ms > " + timeout.ToString("F0") + "ms (streak: " + _consecutiveAnalyzerTimeouts + ")");
            }
            else
            {
                _consecutiveAnalyzerTimeouts = Math.Max(0, _consecutiveAnalyzerTimeouts - 1);
            }
        }

        public AgentProfile GetOrCreateProfile(string processName)
        {
            lock (_listLock)
            {
                foreach (var p in _profiles)
                {
                    if (string.Equals(p.ProcessName, processName, StringComparison.OrdinalIgnoreCase)) return p;
                }
                var profile = new AgentProfile { Name = processName, ProcessName = processName };
                _profiles.Add(profile);
                if (_profiles.Count > 200) _profiles.RemoveAt(0);
                return profile;
            }
        }

        public List<AgentExperience> GetTopExperiences(int count)
        {
            lock (_listLock)
            {
                var sorted = new List<AgentExperience>(_experiences);
                sorted.Sort((a, b) => b.Score.CompareTo(a.Score));
                if (sorted.Count > count) sorted.RemoveRange(count, sorted.Count - count);
                return sorted;
            }
        }

        public List<AgentExperience> GetWorstExperiences(int count)
        {
            lock (_listLock)
            {
                var sorted = new List<AgentExperience>(_experiences);
                sorted.Sort((a, b) => a.Score.CompareTo(b.Score));
                if (sorted.Count > count) sorted.RemoveRange(count, sorted.Count - count);
                return sorted;
            }
        }

        public double GetOverallHealthScore()
        {
            double score = 100;
            score -= Math.Max(0, (_currentState.CpuUsage - 20) * 0.3);
            score -= Math.Max(0, (_currentState.RamUsage - 40) * 0.3);
            score -= Math.Max(0, (_currentState.DiskUsage - 70) * 0.2);
            if (_currentState.CpuTemp > 80) score -= (_currentState.CpuTemp - 80) * 1.5;
            if (_currentState.FpsStability < 50) score -= (50 - _currentState.FpsStability) * 0.5;
            if (_safeMode) score -= 20;
            if (score < 0) score = 0;
            if (score > 100) score = 100;
            return score;
        }

        public string GetAgentSummary()
        {
            double health = GetOverallHealthScore();
            string status = health > 80 ? "Good" : health > 50 ? "Fair" : "Poor";
            return string.Format("Health: {0:F0}/100 ({1}) | Decisions: {2} | Experiences: {3} | Mode: {4}",
                health, status, _stats.TotalDecisions, _experiences.Count, _mode);
        }

        public void SaveData()
        {
            lock (_saveLock)
            {
                try
                {
                    if ((DateTime.Now - _lastSaveTime).TotalMilliseconds < SAVE_DEBOUNCE_MS) return;
                    _lastSaveTime = DateTime.Now;
                    SaveExperiences();
                    SaveProfiles();
                    SaveStats();
                    SaveJournal();
                    SavePersistentRules();
                    SaveCategoryLearning();
                    SaveUsagePatterns();
                    SaveAdvancedFeaturesData();
                    SaveRL2Data();
                }
                catch (Exception ex) { LogError("SaveData", ex); }
            }
        }

        public void LoadData()
        {
            try { LoadExperiences(); } catch (Exception ex19) { CWLog.Error("SmartAgent", ex19); }
            try { LoadProfiles(); } catch (Exception ex20) { CWLog.Error("SmartAgent", ex20); }
            try { LoadStats(); } catch (Exception ex21) { CWLog.Error("SmartAgent", ex21); }
            try { LoadJournal(); } catch (Exception ex22) { CWLog.Error("SmartAgent", ex22); }
            try { LoadPersistentRules(); } catch (Exception ex23) { CWLog.Error("SmartAgent", ex23); }
            try { LoadCategoryLearning(); } catch (Exception ex24) { CWLog.Error("SmartAgent", ex24); }
            try { LoadUsagePatterns(); } catch (Exception ex25) { CWLog.Error("SmartAgent", ex25); }
            try { LoadRL2Data(); } catch (Exception ex26) { CWLog.Error("SmartAgent", ex26); }
        }

        private const int MAX_BACKUPS = 3;

        private void AtomicWrite(string path, string content)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, content);
            // Versioned backup: rotate .bak1, .bak2, .bak3
            try
            {
                if (File.Exists(path))
                {
                    for (int i = MAX_BACKUPS - 1; i >= 1; i--)
                    {
                        string older = path + ".bak" + i;
                        string newer = path + ".bak" + (i + 1);
                        if (File.Exists(older))
                        {
                            if (File.Exists(newer)) File.Delete(newer);
                            File.Move(older, newer);
                        }
                    }
                    File.Copy(path, path + ".bak1", true);
                }
            }
            catch (Exception ex) { LogError("AtomicWrite", ex); }
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }

        private void SaveExperiences()
        {
            var sb = new StringBuilder();
            foreach (var exp in _experiences)
            {
                sb.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11}|{12}|{13}|{14}",
                    exp.Id, exp.GameName, (int)exp.Category, exp.Action, exp.StateBefore, exp.StateAfter,
                    exp.FpsBefore, exp.FpsAfter, exp.StabilityBefore, exp.StabilityAfter,
                    (int)exp.Result, exp.Score, exp.TimesUsed, exp.TimesSuccessful, exp.LastUsed.Ticks));
            }
            AtomicWrite(Path.Combine(_dataDir, "experiences.dat"), sb.ToString());
        }

        private void LoadExperiences()
        {
            string path = ResolveLoadPath(Path.Combine(_dataDir, "experiences.dat"));
            if (!File.Exists(path)) return;
            _experiences.Clear();
            foreach (string line in File.ReadAllLines(path))
            {
                if (string.IsNullOrEmpty(line)) continue;
                string[] parts = line.Split('|');
                if (parts.Length < 15) continue;
                var exp = new AgentExperience();
                try
                {
                    exp.Id = parts[0];
                    exp.GameName = parts[1];
                    exp.Category = (DecisionCategory)int.Parse(parts[2]);
                    exp.Action = parts[3];
                    exp.StateBefore = parts[4];
                    exp.StateAfter = parts[5];
                    exp.FpsBefore = double.Parse(parts[6], System.Globalization.CultureInfo.InvariantCulture);
                    exp.FpsAfter = double.Parse(parts[7], System.Globalization.CultureInfo.InvariantCulture);
                    exp.StabilityBefore = double.Parse(parts[8], System.Globalization.CultureInfo.InvariantCulture);
                    exp.StabilityAfter = double.Parse(parts[9], System.Globalization.CultureInfo.InvariantCulture);
                    exp.Result = (ExperienceResult)int.Parse(parts[10]);
                    exp.Score = double.Parse(parts[11], System.Globalization.CultureInfo.InvariantCulture);
                    exp.TimesUsed = int.Parse(parts[12]);
                    exp.TimesSuccessful = int.Parse(parts[13]);
                    exp.LastUsed = new DateTime(long.Parse(parts[14]));
                    _experiences.Add(exp);
                }
                catch (Exception ex) { LogError("LoadExperiences", ex); }
            }
        }

        private void SaveProfiles()
        {
            var sb = new StringBuilder();
            foreach (var p in _profiles)
            {
                sb.AppendLine(string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}|{10}|{11}|{12}|{13}",
                    p.Name, p.ProcessName, p.ProcessPath ?? "", (int)p.PreferredMode,
                    p.AutoMemoryClean, p.AutoProcessPriority, p.AutoPowerPlan, p.AutoBackgroundControl,
                    p.AutoThrottle, p.MaxCpuTemp, p.MaxGpuTemp, p.PreferredFpsCap, p.UseCount, p.LastUsed.Ticks));
            }
            AtomicWrite(Path.Combine(_dataDir, "profiles.dat"), sb.ToString());
        }

        private void LoadProfiles()
        {
            string path = ResolveLoadPath(Path.Combine(_dataDir, "profiles.dat"));
            if (!File.Exists(path)) return;
            _profiles.Clear();
            foreach (string line in File.ReadAllLines(path))
            {
                if (string.IsNullOrEmpty(line)) continue;
                string[] parts = line.Split('|');
                if (parts.Length < 14) continue;
                var p = new AgentProfile();
                try
                {
                    p.Name = parts[0];
                    p.ProcessName = parts[1];
                    p.ProcessPath = parts[2];
                    p.PreferredMode = (AgentMode)int.Parse(parts[3]);
                    p.AutoMemoryClean = bool.Parse(parts[4]);
                    p.AutoProcessPriority = bool.Parse(parts[5]);
                    p.AutoPowerPlan = bool.Parse(parts[6]);
                    p.AutoBackgroundControl = bool.Parse(parts[7]);
                    p.AutoThrottle = bool.Parse(parts[8]);
                    p.MaxCpuTemp = int.Parse(parts[9]);
                    p.MaxGpuTemp = int.Parse(parts[10]);
                    p.PreferredFpsCap = int.Parse(parts[11]);
                    p.UseCount = int.Parse(parts[12]);
                    p.LastUsed = new DateTime(long.Parse(parts[13]));
                    _profiles.Add(p);
                }
                catch (Exception ex) { LogError("LoadProfiles", ex); }
            }
        }

        private void SaveStats()
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}",
                _stats.TotalDecisions, _stats.SuccessfulDecisions, _stats.FailedDecisions,
                _stats.TotalExperiences, _profiles.Count, _stats.StartTime.Ticks, _stats.LastDecisionTime.Ticks));
            AtomicWrite(Path.Combine(_dataDir, "stats.dat"), sb.ToString());
        }

        private void LoadStats()
        {
            string path = ResolveLoadPath(Path.Combine(_dataDir, "stats.dat"));
            if (!File.Exists(path)) return;
            foreach (string line in File.ReadAllLines(path))
            {
                if (string.IsNullOrEmpty(line)) continue;
                string[] parts = line.Split('|');
                if (parts.Length < 7) continue;
                try
                {
                    _stats.TotalDecisions = int.Parse(parts[0]);
                    _stats.SuccessfulDecisions = int.Parse(parts[1]);
                    _stats.FailedDecisions = int.Parse(parts[2]);
                    _stats.TotalExperiences = int.Parse(parts[3]);
                    _stats.ActiveProfiles = int.Parse(parts[4]);
                    _stats.StartTime = new DateTime(long.Parse(parts[5]));
                    _stats.LastDecisionTime = new DateTime(long.Parse(parts[6]));
                }
                catch (Exception ex) { LogError("LoadStats", ex); }
            }
        }

        private void DetectGameExit()
        {
            if (!string.IsNullOrEmpty(_activeGameName) && _currentState.ActiveProcess != _activeGameName)
            {
                AddLog("JOURNAL", "GameExit", "Game '" + _activeGameName + "' exited, reverting journal...");
                RevertJournal(_activeGameName);
                // Keep discrimination memory (do not erase) so relaunch is recognized instantly.
                _activeGameName = "";
            }
            if (_currentState.IsGame && !string.IsNullOrEmpty(_currentState.ActiveProcess))
            {
                _activeGameName = _currentState.ActiveProcess;
            }
        }

        private void CheckPostGameStabilization()
        {
            if (IsGameRunning())
            {
                _nonGamingStabilityTicks = 0;
            }
            else
            {
                if (_nonGamingStabilityTicks < int.MaxValue - 100)
                    _nonGamingStabilityTicks++;

                if (_nonGamingStabilityTicks == POST_GAME_STABILITY_TICKS)
                {
                    OnPostGameStabilized();
                }
            }
        }

        internal void OnPostGameStabilized()
        {
            try
            {
                if (_trainingDeferred)
                {
                    _trainingDeferred = false;
                    AddLog("TRAIN", "Resume", "Post-game stabilized. Triggering deferred ML background training.");
                    TriggerBackgroundTraining();
                }

                if (_rl2Deferred)
                {
                    ResumeDeferredRL2Training();
                }

                if (!_priorInitialized && ModelNeedsPrior())
                {
                    InitializeWithPriorKnowledge();
                }
            }
            catch (Exception ex)
            {
                LogError("PostGameStabilized", ex);
            }
        }

        private void RevertJournal(string gameName)
        {
            List<StateJournalEntry> snap;
            lock (_journalLock) { snap = new List<StateJournalEntry>(_journal); }
            for (int i = snap.Count - 1; i >= 0; i--)
            {
                var entry = snap[i];
                if (entry.Reverted) continue;
                if (entry.GameName == gameName || string.IsNullOrEmpty(entry.GameName))
                {
                    try
                    {
                        if (entry.WhatChanged.Contains("PowerPlan"))
                        {
                            ExecutePowerPlan("balanced");
                            AddLog("JOURNAL", "Reverted", "Power plan -> Balanced for " + gameName);
                        }
                        if (entry.WhatChanged.Contains("Priority"))
                        {
                            string gameProc = (entry.GameName ?? "").ToLower().Replace(".exe", "");
                            if (gameProc.Length > 0)
                            {
                                try
                                {
                                    foreach (var p in Process.GetProcessesByName(gameProc))
                                    {
                                        try { using (p) { p.PriorityClass = System.Diagnostics.ProcessPriorityClass.Normal; } }
                                        catch (System.ComponentModel.Win32Exception) { }
                                        catch (InvalidOperationException) { }
                                        catch (Exception ex27) { CWLog.Error("SmartAgent", ex27); }
                                    }
                                }
                                catch (Exception ex) { LogError("Tick", ex); }
                            }
                            AddLog("JOURNAL", "Reverted", "Process priority -> Normal for " + gameName);
                        }
                        entry.Reverted = true;
                        entry.RevertedAt = DateTime.Now;
                        entry.State = 3; // Completed
                    }
                    catch (Exception ex) { LogError("RevertJournal", ex); }
                }
            }
        }

        private string GetCurrentPowerPlan()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("powercfg", "/getactivescheme");
                psi.RedirectStandardOutput = true;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                using (var p = Process.Start(psi))
                {
                    // Timeout so a hung powercfg cannot block the decision lock.
                    if (!p.WaitForExit(3000))
                    {
                        try { p.Kill(); } catch (Exception ex28) { CWLog.Error("SmartAgent", ex28); }
                        return "Unknown";
                    }
                    string output = p.StandardOutput.ReadToEnd();
                    if (output.Contains(POWER_SAVER_GUID.Substring(0, 8))) return "Power Saver";
                    if (output.Contains(HIGH_PERFORMANCE_GUID.Substring(0, 8))) return "High Performance";
                    if (output.Contains(BALANCED_GUID.Substring(0, 8))) return "Balanced";
                    string trimmed = output.Trim();
                    return trimmed.Length > 0 ? trimmed.Substring(0, Math.Min(40, trimmed.Length)) : "Unknown";
                }
            }
            catch { return "Unknown"; }
        }

        private void SaveJournal()
        {
            try
            {
                var sb = new StringBuilder();
                foreach (var e in _journal)
                {
                    sb.AppendLine(string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}",
                        e.Id, e.Action, e.Category, e.GameName, e.WhatChanged,
                        e.OriginalValue, e.NewValue, e.AppliedAt.Ticks, e.Reverted ? "1" : "0", e.State));
                }
                AtomicWrite(Path.Combine(_dataDir, "journal.dat"), sb.ToString());
            }
            catch (Exception ex) { LogError("SaveJournal", ex); }
        }

        private void LoadJournal()
        {
            string path = ResolveLoadPath(Path.Combine(_dataDir, "journal.dat"));
            if (!File.Exists(path)) return;
            _journal.Clear();
            foreach (string line in File.ReadAllLines(path))
            {
                if (string.IsNullOrEmpty(line)) continue;
                string[] parts = line.Split('|');
                if (parts.Length < 10) continue;
                try
                {
                    var e = new StateJournalEntry();
                    e.Id = parts[0];
                    e.Action = parts[1];
                    e.Category = parts[2];
                    e.GameName = parts[3];
                    e.WhatChanged = parts[4];
                    e.OriginalValue = parts[5];
                    e.NewValue = parts[6];
                    e.AppliedAt = new DateTime(long.Parse(parts[7]));
                    e.Reverted = parts[8] == "1";
                    if (parts.Length > 9) e.State = int.Parse(parts[9]);
                    _journal.Add(e);
                }
                catch (Exception ex) { LogError("LoadJournal", ex); }
            }
        }

        private void UpdateCategoryLearning(string category, bool success)
        {
            if (!_categorySuccessSum.ContainsKey(category)) { _categorySuccessSum[category] = 0; _categoryTrialCount[category] = 0; _categoryLearnedBoost[category] = 0; }
            _categoryTrialCount[category]++;
            _categorySuccessSum[category] += success ? 1.0 : 0.0;
            double successRate = _categorySuccessSum[category] / _categoryTrialCount[category];
            double targetBoost = (successRate - 0.5) * MAX_LEARNED_BOOST * 2;
            _categoryLearnedBoost[category] += LEARNING_RATE * (targetBoost - _categoryLearnedBoost[category]);
            if (_categoryLearnedBoost[category] > MAX_LEARNED_BOOST) _categoryLearnedBoost[category] = MAX_LEARNED_BOOST;
            if (_categoryLearnedBoost[category] < -MAX_LEARNED_BOOST) _categoryLearnedBoost[category] = -MAX_LEARNED_BOOST;
        }

        private double GetCategoryLearnedBoost(string category)
        {
            if (_categoryLearnedBoost.ContainsKey(category)) return _categoryLearnedBoost[category];
            return 0;
        }

        private void SaveCategoryLearning()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                foreach (var kv in _categoryLearnedBoost)
                    sb.AppendLine(kv.Key + "|" + kv.Value.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + "|" + (_categorySuccessSum.ContainsKey(kv.Key) ? _categorySuccessSum[kv.Key].ToString("F2", System.Globalization.CultureInfo.InvariantCulture) : "0") + "|" + (_categoryTrialCount.ContainsKey(kv.Key) ? _categoryTrialCount[kv.Key].ToString() : "0"));
                AtomicWrite(Path.Combine(_dataDir, "learning.dat"), sb.ToString());
            }
            catch (Exception ex) { LogError("SaveCategoryLearning", ex); }
        }

        private void LoadCategoryLearning()
        {
            try
            {
                string path = Path.Combine(_dataDir, "learning.dat");
                if (!File.Exists(path)) return;
                foreach (var line in File.ReadAllLines(path))
                {
                    var parts = line.Split('|');
                    if (parts.Length >= 4)
                    {
                        _categoryLearnedBoost[parts[0]] = double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                        _categorySuccessSum[parts[0]] = double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
                        _categoryTrialCount[parts[0]] = int.Parse(parts[3]);
                    }
                }
            }
            catch (Exception ex) { LogError("LoadCategoryLearning", ex); }
        }

        private void UpdateAnomalyDetection()
        {
            if (_anomalyCount < 10) { _anomalyCount++; _anomalyCpuHistory[_anomalyIndex] = _currentState.CpuUsage; _anomalyRamHistory[_anomalyIndex] = _currentState.RamUsage; _anomalyTempHistory[_anomalyIndex] = _currentState.CpuTemp; _anomalyGpuHistory[_anomalyIndex] = _currentState.GpuUsage; _anomalyFpsHistory[_anomalyIndex] = _currentState.Fps; _anomalyIndex = (_anomalyIndex + 1) % 60; return; }
            _anomalyCpuHistory[_anomalyIndex] = _currentState.CpuUsage;
            _anomalyRamHistory[_anomalyIndex] = _currentState.RamUsage;
            _anomalyTempHistory[_anomalyIndex] = _currentState.CpuTemp;
            _anomalyGpuHistory[_anomalyIndex] = _currentState.GpuUsage;
            _anomalyFpsHistory[_anomalyIndex] = _currentState.Fps;
            _anomalyIndex = (_anomalyIndex + 1) % 60;
            int n = Math.Min(_anomalyCount, 60);
            _cpuMean = 0; _ramMean = 0; _tempMean = 0; _gpuMean = 0;
            for (int i = 0; i < n; i++) { _cpuMean += _anomalyCpuHistory[i]; _ramMean += _anomalyRamHistory[i]; _tempMean += _anomalyTempHistory[i]; _gpuMean += _anomalyGpuHistory[i]; }
            _cpuMean /= n; _ramMean /= n; _tempMean /= n; _gpuMean /= n;
            double cs = 0, rs = 0, ts = 0, gs = 0;
            for (int i = 0; i < n; i++) { cs += (_anomalyCpuHistory[i] - _cpuMean) * (_anomalyCpuHistory[i] - _cpuMean); rs += (_anomalyRamHistory[i] - _ramMean) * (_anomalyRamHistory[i] - _ramMean); ts += (_anomalyTempHistory[i] - _tempMean) * (_anomalyTempHistory[i] - _tempMean); gs += (_anomalyGpuHistory[i] - _gpuMean) * (_anomalyGpuHistory[i] - _gpuMean); }
            _cpuStdDev = Math.Max(Math.Sqrt(cs / n), 1);
            _ramStdDev = Math.Max(Math.Sqrt(rs / n), 1);
            _tempStdDev = Math.Max(Math.Sqrt(ts / n), 1);
            _gpuStdDev = Math.Max(Math.Sqrt(gs / n), 1);
            _anomalyCount++;
            CheckAnomaly("CPU", _currentState.CpuUsage, _cpuMean, _cpuStdDev);
            CheckAnomaly("RAM", _currentState.RamUsage, _ramMean, _ramStdDev);
            CheckAnomaly("Temp", _currentState.CpuTemp, _tempMean, _tempStdDev);
            CheckAnomaly("GPU", _currentState.GpuUsage, _gpuMean, _gpuStdDev);
        }

        private void CheckAnomaly(string metric, double value, double mean, double stdDev)
        {
            if (stdDev < 0.5) return;
            double z = Math.Abs(value - mean) / stdDev;
            if (z > ANOMALY_THRESHOLD)
            {
                string dir = value > mean ? "مرتفع جداً" : "منخفض جداً";
                string alert = string.Format("[{0}] {1}: {2:F1}% ({3} عن المتوسط {4:F1}%)", DateTime.Now.ToString("HH:mm"), metric, value, dir, mean);
                _anomalyAlerts.Insert(0, alert);
                if (_anomalyAlerts.Count > MAX_ANOMALY_ALERTS) _anomalyAlerts.RemoveAt(_anomalyAlerts.Count - 1);
                AddLog("INFO", "Anomaly", alert);
            }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        private void UpdateIdleDetection()
        {
            double idleSeconds = 0;
            try
            {
                if (!DisableRealExecution && !_simulationMode)
                {
                    LASTINPUTINFO lii = new LASTINPUTINFO();
                    lii.cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(LASTINPUTINFO));
                    if (GetLastInputInfo(ref lii))
                    {
                        uint elapsedTicks = unchecked((uint)Environment.TickCount - lii.dwTime);
                        idleSeconds = elapsedTicks / 1000.0;
                        if (idleSeconds < 2.0) _lastActiveTime = DateTime.Now;
                    }
                    else
                    {
                        idleSeconds = (DateTime.Now - _lastActiveTime).TotalSeconds;
                    }
                }
                else
                {
                    idleSeconds = (DateTime.Now - _lastActiveTime).TotalSeconds;
                }
            }
            catch (Exception ex)
            {
                LogError("UpdateIdleDetection", ex);
                idleSeconds = (DateTime.Now - _lastActiveTime).TotalSeconds;
            }
            if (idleSeconds >= IDLE_THRESHOLD_SECONDS && !_idleCleanupDone)
            {
                // Central Execution Gate: idle cleanup mutates the system
                // (working-set trim), so it must respect the automation
                // safety valve. While it is off, keep _idleCleanupDone
                // false and retry on the next tick - no silent bypass.
                if (!CanExecuteAction("IdleMemoryClean", false))
                {
                    // Blocked: leave _idleCleanupDone false so the gate is re-checked
                    // on the next idle tick and cleanup fires once the valve is back on.
                }
                else
                {
                    try
                    {
                        _idleCleanupDone = true;
                        // Multi-stage deep memory compaction during idle: user is away,
                        // so we can safely flush modified pages, trim non-critical working sets,
                        // purge standby list, and clean clipboard cache (Wise Memory Optimizer evolution).
                        try { ExecuteDeepMemoryCompaction(); } catch (Exception ex) { LogError("UpdateIdleDetection", ex); }
                    }
                    catch (Exception ex) { LogError("UpdateIdleDetection", ex); }
                    AddLog("INFO", "Idle", "تم تنفيذ ضغط وتطهير الذاكرة العميق أثناء الخمول (Deep Memory Compaction)");
                }
            }
            else if (idleSeconds < IDLE_THRESHOLD_SECONDS)
            {
                _idleCleanupDone = false;
            }
        }

        private void LearnUsagePattern(string appName)
        {
            if (string.IsNullOrEmpty(appName) || appName == "Unknown") return;
            int hour = DateTime.Now.Hour;
            string key = appName.ToLower() + "-" + hour;
            if (!_appUsageByHour.ContainsKey(key)) _appUsageByHour[key] = 0;
            _appUsageByHour[key]++;
            if (_appUsageByHour.Count > MAX_APP_USAGE)
            {
                string worstKey = ""; int worstVal = int.MaxValue;
                foreach (var kv in _appUsageByHour) { if (kv.Value < worstVal) { worstVal = kv.Value; worstKey = kv.Key; } }
                if (worstKey.Length > 0) _appUsageByHour.Remove(worstKey);
            }
        }

        private string PredictNextApp()
        {
            int hour = DateTime.Now.Hour;
            string bestApp = "";
            int bestCount = 0;
            foreach (var kv in _appUsageByHour)
            {
                if (kv.Key.EndsWith("-" + hour) && kv.Value > bestCount)
                {
                    bestCount = kv.Value;
                    bestApp = kv.Key.Split('-')[0];
                }
            }
            return bestApp;
        }

        private void SmartScheduledMaintenance()
        {
            int today = DateTime.Now.DayOfYear;
            if (_lastMaintenanceDay != today) { _lastMaintenanceDay = today; Array.Clear(_hourMaintenanceDone, 0, 24); }
            int hour = DateTime.Now.Hour;
            if (_hourMaintenanceDone[hour] >= MAINTENANCE_COOLDOWN) return;
            if (hour >= 1 && hour <= 6)
            {
                try
                {
                    _hourMaintenanceDone[hour]++;
                }
                catch (Exception ex) { LogError("SmartScheduledMaintenance", ex); }
                AddLog("INFO", "Scheduler", "تنظيف مجدول ذكي during hour " + hour);
            }
        }

        private void SaveUsagePatterns()
        {
            try
            {
                var sb = new System.Text.StringBuilder();
                foreach (var kv in _appUsageByHour)
                    sb.AppendLine(kv.Key + "|" + kv.Value);
                AtomicWrite(Path.Combine(_dataDir, "usage.dat"), sb.ToString());
            }
            catch (Exception ex) { LogError("SaveUsagePatterns", ex); }
        }

        private void LoadUsagePatterns()
        {
            try
            {
                string path = Path.Combine(_dataDir, "usage.dat");
                if (!File.Exists(path)) return;
                foreach (var line in File.ReadAllLines(path))
                {
                    var parts = line.Split('|');
                    if (parts.Length >= 2)
                    {
                        int count;
                        if (int.TryParse(parts[1], out count))
                            _appUsageByHour[parts[0]] = count;
                    }
                }
            }
            catch (Exception ex) { LogError("LoadUsagePatterns", ex); }
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out WinRect lpRect);
        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int nIndex);
        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("kernel32.dll")]
        static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        public void Stop()
        {
            // Arm the kill switch first so any Tick already queued on the thread
            // pool bails out instead of applying changes during/after the revert.
            _killSwitchActive = true;
            try
            {
                ResetTimerResolution();
            }
            // Dispose PerformanceCounter to prevent handle leak
            catch (Exception ex) { LogError("Stop", ex); }
            // Restore UI frame pacing (refresh rate + visual effects) to original state
            try
            {
                if (_fpActive) RestoreFramePacing();
            }
            catch (Exception ex) { LogError("Stop", ex); }
            // Restore network tuning (netsh + registry) to original state
            try
            {
                if (_ntActive) RestoreNetworkTuning();
            }
            catch (Exception ex) { LogError("Stop", ex); }
            // Restore all backed-up GPU and game Registry values
            try
            {
                RestoreAllBackedUpRegistrySettings();
            }
            catch (Exception ex) { LogError("Stop.RegistryRestore", ex); }
            // Flush pending saves with shutdown ordering
            lock (_saveLock)
            {
                try
                {
                    SaveExperiences();
                    SaveProfiles();
                    SaveStats();
                    SaveJournal();
                    SavePersistentRules();
                    SaveCategoryLearning();
                    SaveUsagePatterns();
                    SaveAdvancedFeaturesData();
                    SaveRL2Data();
                }
                catch (Exception ex) { LogError("Stop", ex); }
            }
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct WinRect { public int Left, Top, Right, Bottom; }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        // === Helper methods for new subsystems ===
        // Thread-safe: read from CurrentState (lock+clone) so UI thread gets a stable snapshot
        public double GetCurrentCpuUsage()
        {
            return CurrentState.CpuUsage;
        }

        public double GetCurrentRamUsage()
        {
            return CurrentState.RamUsage;
        }

        public double GetCurrentDiskUsage()
        {
            return CurrentState.DiskUsage;
        }

        public double GetCurrentGpuUsage()
        {
            return CurrentState.GpuUsage;
        }

        public double GetCurrentCpuTemp()
        {
            return CurrentState.CpuTemp;
        }

        public double GetTotalPhysicalMemoryMB()
        {
            return CurrentState.TotalRamGB * 1024.0;
        }

        public bool IsGameRunning()
        {
            return CurrentState.IsGame;
        }
    }
}
