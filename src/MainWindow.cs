using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using System.Management;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Shapes;
using System.IO;
using Microsoft.Win32;
using System.Security.Principal;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Interop;
using ShapePath = System.Windows.Shapes.Path;

namespace ClearWin
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _mainTimer;
        private DispatcherTimer _animTimer;
        private DispatcherTimer _monitorTimer;
        private int _mainTickCounter = 0;
        private bool _procAutoRefreshActive = false;
        private bool _powerPlanQueryBusy = false;
        private int _aiProcsBusy = 0;
        // 1 = a Tick is still running on the threadpool; overlapping Ticks are skipped
        // instead of racing on the RL tables / decision queue / shared state.
        private int _agentTickBusy = 0;
        // Tweaks currently executing off-thread (WPF + WebUI share this guard so a
        // double-click / double-post of the same tweak cannot start two PowerShell runs).
        private readonly object _tweakInFlightLock = new object();
        private readonly HashSet<string> _tweaksInFlight = new HashSet<string>();
        private Dictionary<string, Border> _navItems = new Dictionary<string, Border>();
        private TextBlock _maxBtnTxt;
        private Border _activeNav;
        private ContentControl _mainContent;
        private ScrollViewer _scrollViewer;
        private TextBlock _cpuSub, _cpuDetail, _ramSub, _ramDetail, _diskSub, _diskDetail, _batSub, _batDetail;
        private TextBlock _netSub;
        private TextBlock _osInfo;
        private TextBlock _storageUsed, _storageFree;
        private TextBlock _hzInfo, _statusTxt, _powerPlanInfo;
        private Border _optHealthBarFill;
        private TextBlock _optHealthTxt;
        private Grid _monCpuGraph, _monRamGraph;
        private double _animCpu = 0, _animRam = 0, _animDisk = 0, _animGpu = 0, _animScore = 100;
        private double _animCpuShown = double.MinValue, _animRamShown = double.MinValue;
        private string _animCpuTxt = null, _animRamTxt = null, _animDiskTxt = null, _animGpuTxt = null;
        private double _realCpu = 0, _realRam = 0, _realDisk = 0, _realGpu = 0, _realTemp = 0;
        private double _healthScore = 100;
        private Color _accentColor = Color.FromRgb(46, 168, 255);
        private string _gpuName = "", _gpuVRAM = "";
        private int _refreshHz = 0;
        private int _tickCounter = 0;
        private RingBuffer<double> _cpuHistory = new RingBuffer<double>(50);
        private RingBuffer<double> _diskHistory = new RingBuffer<double>(50);
        private Random _rnd = new Random();
        private PerformanceCounter _cpuTempPc, _gpuTempPc, _diskUtilPc;
        private List<PerformanceCounter> _gpuUtilPcs = new List<PerformanceCounter>();
        private bool _gpuCounterInit = false;
        private bool _gpuCounterWarmedUp = false;
        private ListBox _processList;
        private TextBox _processFilter;
        private ListBox _startupList;
        private TextBlock _netInfo;
        private TextBlock _overviewNetTxt;
        // Disk page dynamic refs
        private List<Border> _diskBarFills = new List<Border>();
        private List<TextBlock> _diskSizeTxts = new List<TextBlock>();
        private List<TextBlock> _diskFreeTxts = new List<TextBlock>();
        // Health & Safety page dynamic refs
        private TextBlock _hsThermalStatus, _hsTempVal;
        private Border _hsTempBar;
        private TextBlock _hsBatVal, _hsBatMode;
        private Border _hsBatBar;
        private TextBlock _hsDiskVal, _hsDiskStatus;
        private Border _hsDiskBar;
        private TextBlock _hsNetDown, _hsNetUp;
        private TextBlock _hsSpeedResult;
        private StackPanel _hsHogStack;
        // Tools/Services page dynamic ref
        private ListBox _toolsSrvList;
        private string _toolsSrvFilter = "";
        private CircularProgress _monCpuRing, _monRamRing;
        private TextBlock _gpuSub, _gpuDetail;
        private TextBlock _gpuVRAMTxt;
        private TextBlock _gpuUsageTxt;
        private Grid _gpuPageGraph;
        private string _gpuTempStr = "";
        private double _gpuTemp = 0;
        private TextBlock _monCpuPct, _monCpuFreq, _monRamPct, _monRamGB, _monProcTxt, _monThreadTxt, _monCpuPerCore, _monCoreTxt;
        private RingBuffer<double> _ramHistory = new RingBuffer<double>(50);
        private RingBuffer<double> _gpuHistory = new RingBuffer<double>(50);
        private RingBuffer<double> _cpuTempHistory = new RingBuffer<double>(50);
        private RingBuffer<double> _netHistory = new RingBuffer<double>(30);
        private int _langMode = 0; // 0=Arabic, 1=English, 2=Mixed
        private string _currentTab = "شفافية الذكاء";

        private OverlayWindow _overlayWindow;
        private bool _overlayEnabled = false;
        private bool _overlayShowFPS = true, _overlayShowCPU = true, _overlayShowRAM = true, _overlayShowGPU = true, _overlayShowTemp = true, _overlayShowClock = true;
        private bool _gameModeActive = false;
        // Tracks what AutoPilot turned on so game exit never disables user-owned settings.
        private bool _gameModeStartedAutomatically = false;
        private bool _gameModeAutoEnabledFpsFloor = false;
        private bool _gameModeAutoEnabledFullOptimization = false;
        private bool _gameModeRecoveryBlocked = false;
        private readonly object _gameModeRecoveryLock = new object();
        // Preserve original process priorities before ClearWin temporarily boosts a detected game.
        private readonly object _gamePriorityLock = new object();
        private readonly Dictionary<int, ProcessPriorityClass> _gamePriorityBaseline = new Dictionary<int, ProcessPriorityClass>();
        private System.Windows.Controls.TextBlock _gmStatusTxt;
        private System.Windows.Controls.TextBlock _fpsFloorStatusTxt;
        private System.Windows.Controls.TextBlock _fullOptStatusTxt;
        private string _gameModePrevPlanGuid = null;
        private string _gameModePrevParkMin = null;
        private string _gameModePrevParkMax = null;
        private bool _gameModePrevTimerResActive = false;
        private List<string> _gameModeServicesToRestart = null;
        private readonly object _gameModeServicesLock = new object();
        private string _startupPlanGuid = null;
        private bool _autoRamClean = false;
        private bool _autoPriority = false;
        private bool _autoScheduler = false;
        private int _animSpeed = 1;
        private int _defaultTab = 0;
        private OverlayPosition _overlayPosition = OverlayPosition.TopRight;
        private double _overlayOpacity = 0.78;
        private Color _overlayTextColor = Colors.White;
        private const int HOTKEY_ID = 9001;
        private const int HOTKEY_ID_KILLSWITCH = 9002;
        private const int HOTKEY_ID_PAUSE = 9003;
        private const int HOTKEY_ID_EMERGENCY = 9004;
        private const int VK_F9 = 0x78;
        private const int VK_F8 = 0x77;
        private const int VK_F10 = 0x79;
        private const int VK_F11 = 0x7A;
        private const int WM_HOTKEY = 0x0312;
        private HwndSourceHook _hookDelegate;
        private bool _autoPilot = false;
        private bool _autoPilotActive = false;
        private TextBlock _autoPilotStatus;
        private StackPanel _aiProcessPanel;
        private bool _aiMonitorEnabled = true;
        private object _trayIcon;
        private bool _minimizeToTray = false;
        private bool _startWithWindows = false;
        private bool _gameGpuPower = true;

        public bool MinimizeToTray
        {
            get { return _minimizeToTray; }
            set { _minimizeToTray = value; SaveMinimizeToTraySetting(); }
        }

        public bool StartWithWindows
        {
            get { return _startWithWindows; }
            set { _startWithWindows = value; SaveStartWithWindowsSetting(value); }
        }

        public bool GameGpuPower
        {
            get { return _gameGpuPower; }
            set { _gameGpuPower = value; SaveGameGpuPowerSetting(value); }
        }
        private TextBlock _autoSaveStatus, _nextTickStatus, _safeModeStatus;
        private TextBlock _modeCardTitle, _modeCardSub;
        private bool _fpsLockBackedUp = false;
        private bool _hadNvMaxFrameRate = false;
        private object _prevNvMaxFrameRate = null;
        private Microsoft.Win32.RegistryValueKind _nvMaxFrameRateKind = Microsoft.Win32.RegistryValueKind.DWord;
        private bool _hadD3DFrameRateLimit = false;
        private object _prevD3DFrameRateLimit = null;
        private Microsoft.Win32.RegistryValueKind _d3DFrameRateKind = Microsoft.Win32.RegistryValueKind.DWord;
        private readonly System.Text.StringBuilder _diagBuf = new System.Text.StringBuilder();
        private int _diagCounter = 0;
        private const int DIAG_FLUSH_EVERY = 30;
        private int _autoSaveCounter = 0, _nextTickCounter = 0;
        private long _prevNetSent = 0, _prevNetRecv = 0;
        private double _netSentSpeed = 0, _netRecvSpeed = 0;
        private DateTime _netSampleTime = DateTime.MinValue;
        private Dictionary<int, TimeSpan> _procPrevCpuTimes = new Dictionary<int, TimeSpan>();
        private int _procPrevElapsedTick = 0;
        private bool _topProcTaskRunning = false;
        private TextBlock _netSpeedTxt;
        private PerformanceCounter _cpuLoadPc;
        private double _cachedTotalRamGB = 0;
        private bool _batteryChecked = false;
        private double _lastTemp = -1, _lastGpu = -1, _lastScore = 100;

        private SmartAgent _agent;
        private FrameTimeTracker _frameTracker;
        private DashboardView _dashboardView;
        private Dictionary<string, UIElement> _pageCache = new Dictionary<string, UIElement>();
        private TextBlock _topBarAgentStatus;
        private TextBlock _statusDot;
        private ListBox _learningBestList, _learningWorstList;
        private TextBlock _learningStatsTxt;
        private TextBlock _dashAgentStatus;
        private TextBlock _gameCenterActive;
        private ListBox _gameProfileList;
        private ListBox _aiLogList;
        private ListBox _aiExperienceList;
        private bool _agentPageActive = false;
        private bool _gameCenterPageActive = false;
        private bool _aiLogPageActive = false;
        private bool _learningPageActive = false;
        private TextBlock _healthTrendTxt;
        private bool _enginePageActive = false;

        private TextBlock[] _agentStatVals = new TextBlock[4];
        private TextBlock _advBenchScore, _advBenchTrend, _advSelfDiagStatus, _advSelfDiagRam;
        private TextBlock _advAutopilotStatus, _advAutopilotActions, _advPowerPlan, _advPowerScore;
        private StackPanel _advRootCausesPanel, _advRecoveryPanel, _advRecommendPanel;
        private StackPanel _advGameProfilesPanel, _advDriversPanel;
        // === Level 3: Elite AI ===
        private TextBlock _advSimStatus, _advDNAStatus, _advSeasonStatus, _advForecastStatus;
        private TextBlock _advMultiAgentStatus, _advRiskStatus, _advConflictStatus;
        private TextBlock _advExplainStatus, _advGoalStatus, _advBoosterStatus, _advKBStatus;
        // === Level 4: Research Grade AI ===
        private TextBlock _advCausalStatus, _advWorldModelStatus, _advRLStatus, _advPlanStatus;
        private TextBlock _advLongTermStatus, _advArchStatus, _advProfileStatus, _advCalibStatus;
        // === Level 5-7: Autonomous, Enterprise, Optimization Graph ===
        private TextBlock _advAutoMStatus, _advEnterpriseStatus, _advGraphStatus;
        // === New subsystems: PredictiveMaintenance, SmartScheduling, PerformanceComparison, UserBehaviorLearning ===
        private TextBlock _advMaintStatus, _advScheduleStatus, _advComparisonStatus, _advBehaviorStatus;
        private bool _advancedAIPageActive = false;
        private bool _startupPageActive = false;
        private bool _networkPageActive = false;
        private bool _diskPageActive = false;
        private bool _toolsPageActive = false;
        private bool _healthPageActive = false;
        private StackPanel _timelineStack;
        private int _lastTimelineRebuildCount = -1;
        // === New AI Dashboard fields ===
        private TextBlock[] _scoreCardVals = new TextBlock[5];
        private Grid[] _scoreCardSparks = new Grid[5];
        private TextBlock[] _agentCardStatus = new TextBlock[8];
        private TextBlock[] _agentCardImpact = new TextBlock[8];
        private TextBlock _explainDecisionTxt;
        private TextBlock _explainRiskTxt;
        private TextBlock _explainDataTxt;
        private TextBlock _explainBenefitTxt;
        private TextBlock _explainAltTxt;
        private StackPanel _explainActivityStack;
        private StackPanel _approvalStack;
        private string _approvalSignature = "";
        private TextBlock _aiEngineDecisions;
        private TextBlock _aiEnginePredictions;
        private TextBlock _aiEngineConfidence;
        private TextBlock _aiEngineAnalyzers;
        private TextBlock _agentToggleNewText;
        private Border _agentToggleNew;
        private TextBlock _goalProgressTxt;

        // === Phase 7-14: Enhanced AI Dashboard ===
        private StackPanel _decisionTreePanel;
        private StackPanel _terminalLogStack;
        private List<string> _terminalLogs = new List<string>();
        private TextBlock[] _liveMonitorVals = new TextBlock[8];
        private Border[] _liveMonitorBarFills = new Border[8];

        // === V19 Phase 1: Last-event banner + Reward/Penalty cards ===
        private TextBlock _lastEventBanner;
        private TextBlock _lastEventTime;
        private TextBlock _rewardAvgTxt;
        private TextBlock _rewardEvalsTxt;
        private TextBlock _rewardSimTxt;
        private TextBlock _penaltyAbstainTxt;
        private TextBlock _penaltyRiskBlockedTxt;
        private TextBlock _penaltyStatusTxt;
        private StackPanel _forecastPointsPanel;
        private TextBlock[] _forecastValTxts = new TextBlock[3];
        private Grid[] _forecastSparkPanels = new Grid[3];
        private StackPanel _radarPanel;
        private TextBlock _radarStatusTxt;
        private StackPanel _smartGoalsPanel;
        private TextBlock _brainStatusTxt;

        // === AI Monitor Fields ===
        private TextBlock _monitorHealthTxt;
        private TextBlock _monitorDecisionsTxt;
        private TextBlock _monitorAlertsTxt;
        private TextBlock _monitorConfidenceTxt;
        private TextBlock _monitorAnomalyTxt;
        private TextBlock _monitorGameTxt;
        private StackPanel _monitorAlertStack;

        private bool _timerResActive = false;
        private bool _servicesBoosted = false;
        private bool _shutdownPending = false;
        private string[] _targetServices = new[] { "Spooler", "RemoteRegistry", "WbioSrvc", "MapsBroker", "DiagTrack", "WSearch" };
        private StackPanel _aiPredictionsPanel;
        private StackPanel _aiSuggestionsPanel;

        // === New Dashboard Fields (15-Phase) ===
        // Phase 5: Welcome Card
        private TextBlock _dashWelcomeTxt;
        private TextBlock _dashHealthBriefTxt;
        // Phase 6: 6 KPI Cards (Performance, Gaming, Security, Power, Temperature, AI Score)
        private TextBlock[] _kpiVals = new TextBlock[6];
        private TextBlock[] _kpiTrends = new TextBlock[6];
        private Grid[] _kpiSparks = new Grid[6];
        // Phase 7: System Overview (CPU/RAM/GPU/Disk/Network/Temp/Battery rings)
        private CircularProgress[] _overviewRings = new CircularProgress[8];
        private TextBlock[] _overviewLabels = new TextBlock[8];
        private TextBlock[] _overviewSubs = new TextBlock[8];
        // Phase 8: Live Monitor (real-time graphs)
        private Grid[] _liveGraphs = new Grid[5];
        private TextBlock[] _liveLabels = new TextBlock[5];
        private TextBlock[] _liveVals = new TextBlock[5];
        private Polyline[] _livePolylines = new Polyline[5];
        private Ellipse[] _liveDots = new Ellipse[5];
        // Phase 10: AI Summary
        private TextBlock _dashAiStatus;
        private TextBlock _dashAiConfidence;
        private TextBlock _dashAiLastAction;
        // Phase 11: Recent Activity
        private StackPanel _dashActivityStack;
        // Phase 12: Top Resource Consumers
        private TextBlock[] _topProcNames = new TextBlock[5];
        private TextBlock[] _topProcPcts = new TextBlock[5];
        private Border[] _topProcBars = new Border[5];
        // Phase 13: Storage Overview
        private TextBlock[] _storLabels = new TextBlock[3];
        private Border[] _storBars = new Border[3];
        private TextBlock[] _storVals = new TextBlock[3];
        // Phase 14: Quick Health
        private Border[] _healthDots = new Border[6];
        private TextBlock[] _healthLabels = new TextBlock[6];
        private Border _sysProtDot;
        private TextBlock _sysProtTxt;

        // Windows Optimization AI Dashboard fields (unified view)
        private TextBlock _woaStatusTxt, _woaDecisionTxt, _woaConfidenceTxt;
        private TextBlock _woaPromptTxt, _woaResponseTxt;
        private StackPanel _woaFeedStack;
        private Border _woaStatusDot;
        private Border _woaActivateBtn;
        private bool _lastWoaBtnRunning;
        private string _lastSeenWoaAction = "";
        private string _lastWoaStatus = "";
        private DateTime _lastModelNotify = DateTime.MinValue;
        private DateTime _lastModelStartAttempt = DateTime.MinValue;
        private int _lastDashActCount = -1;
        private TextBlock[] _dashMetricTxts = new TextBlock[6];
        // Legacy dashboard fields kept for compatibility
        private Border _toastBorder;
        private Border _searchOverlay;
        private TextBox _searchInput;
        private ListBox _searchResults;
        private TextBlock _pageTitleText;
        private TextBlock _pageSubtitleText;
        public MainWindow()
        {
            Width = 1360; Height = 900;
            MinWidth = 1120; MinHeight = 760;
            WindowStyle = WindowStyle.None;
            Topmost = false;
            WindowState = WindowState.Normal;
            Background = new SolidColorBrush(Theme.Background);
            FontFamily = new System.Windows.Media.FontFamily("Segoe UI");
            ResizeMode = ResizeMode.CanResizeWithGrip;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Title = "ClearWin";
            ShowInTaskbar = true;
            AllowsTransparency = false;
            if (System.IO.File.Exists(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico")))
            {
                try { Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico"))); } catch (Exception ex) { CWLog.Error("MainWindow", ex); }
            }
            _agent = new SmartAgent();
            CWLog.Info("Boot", "startup phase 1: SmartAgent created");
            _frameTracker = new FrameTimeTracker();
            try
            {
                if (_agent.WinOptAI != null)
                {
                    _agent.WinOptAI.ManualModelControl = true;
                    _agent.WinOptAI.IntentionallyStopped = false;
                }
            }
            catch (Exception ex2) { CWLog.Error("MainWindow", ex2); }
            BuildUI();
            try { LocalWebBridge.Start(this); } catch (Exception exBridge) { CWLog.Error("MainWindow.WebBridge", exBridge); }
            // Crash Recovery check (ChatGPT Round 27) - NON-BLOCKING: shown via
            // Dispatcher AFTER startup so a modal box can never stall the UI
            // thread during boot (it previously froze the app for minutes when
            // hidden behind a fullscreen game).
            try
            {
                if (!Program.WasCleanExit())
                {
                    CWLog.Warn("MainWindow.Recovery", "Previous run was not cleanly closed. Resuming normal operations.");
                }
            } catch (Exception ex4) { CWLog.Error("MainWindow", ex4); }
            // Dummy TextBlocks for fields NOT assigned by BuildLegacyDashboard
            // (prevents NullReferenceException in OnTick Dispatcher.InvokeAsync lambdas)
            _osInfo = _osInfo ?? new TextBlock();
            _cpuSub = _cpuSub ?? new TextBlock(); _cpuDetail = _cpuDetail ?? new TextBlock();
            _ramSub = _ramSub ?? new TextBlock(); _ramDetail = _ramDetail ?? new TextBlock();
            _diskSub = _diskSub ?? new TextBlock(); _diskDetail = _diskDetail ?? new TextBlock();
            _netSub = _netSub ?? new TextBlock();
            _gpuDetail = _gpuDetail ?? new TextBlock();
            _gpuSub = _gpuSub ?? new TextBlock();
            _batSub = _batSub ?? new TextBlock(); _batDetail = _batDetail ?? new TextBlock();
            _storageUsed = _storageUsed ?? new TextBlock(); _storageFree = _storageFree ?? new TextBlock();
            _hzInfo = _hzInfo ?? new TextBlock(); _powerPlanInfo = _powerPlanInfo ?? new TextBlock(); _overviewNetTxt = _overviewNetTxt ?? new TextBlock();
            _netSpeedTxt = _netSpeedTxt ?? new TextBlock();
            _monCpuPct = _monCpuPct ?? new TextBlock(); _monCpuFreq = _monCpuFreq ?? new TextBlock(); _monRamPct = _monRamPct ?? new TextBlock();
            _monRamGB = _monRamGB ?? new TextBlock(); _monProcTxt = _monProcTxt ?? new TextBlock(); _monThreadTxt = _monThreadTxt ?? new TextBlock();
            _monCpuPerCore = _monCpuPerCore ?? new TextBlock(); _monCoreTxt = _monCoreTxt ?? new TextBlock();
            _statusTxt = _statusTxt ?? new TextBlock();
            _topBarAgentStatus = _topBarAgentStatus ?? new TextBlock();
            _statusDot = _statusDot ?? new TextBlock();
            _autoSaveStatus = _autoSaveStatus ?? new TextBlock();
            _nextTickStatus = _nextTickStatus ?? new TextBlock();
            _safeModeStatus = _safeModeStatus ?? new TextBlock();
            if (_agentStatVals == null) { _agentStatVals = new TextBlock[4]; for (int i = 0; i < 4; i++) _agentStatVals[i] = new TextBlock(); }
            for (int di = 0; di < 6; di++) { if (_kpiVals[di] == null) _kpiVals[di] = new TextBlock(); if (_kpiTrends[di] == null) _kpiTrends[di] = new TextBlock(); }
            for (int di = 0; di < 8; di++) { if (_overviewRings[di] == null) _overviewRings[di] = new CircularProgress(100, 8); if (_overviewLabels[di] == null) _overviewLabels[di] = new TextBlock(); if (_overviewSubs[di] == null) _overviewSubs[di] = new TextBlock(); }
            for (int di = 0; di < 5; di++) { if (_liveVals[di] == null) _liveVals[di] = new TextBlock(); if (_topProcNames[di] == null) _topProcNames[di] = new TextBlock(); if (_topProcPcts[di] == null) _topProcPcts[di] = new TextBlock(); }
            for (int di = 0; di < 3; di++) { if (_storVals[di] == null) _storVals[di] = new TextBlock(); if (_storLabels[di] == null) _storLabels[di] = new TextBlock(); }
            for (int di = 0; di < 6; di++) { if (_healthLabels[di] == null) _healthLabels[di] = new TextBlock(); }
            if (_dashActivityStack == null) _dashActivityStack = new StackPanel();
            if (_dashAiStatus == null) _dashAiStatus = new TextBlock();
            if (_dashAiConfidence == null) _dashAiConfidence = new TextBlock();
            if (_dashAiLastAction == null) _dashAiLastAction = new TextBlock();
            if (_dashWelcomeTxt == null) _dashWelcomeTxt = new TextBlock();
            if (_dashHealthBriefTxt == null) _dashHealthBriefTxt = new TextBlock();
            _agent.OnLog += OnAgentLog;
            _agent.OnOptimizationLog += OnAgentOptLog;
            _agent.OnDecision += OnAgentDecision;
            _agent.OnMonitorAlert += OnMonitorAlert;
            InitializePersistentMemory();
            CWLog.Info("Boot", "startup phase 3: agent wired, frame tracker starting");
            if (!Program.IsSafeStartup) _frameTracker.Start();
            CWLog.Info("Boot", "startup phase 4: LoadSystemInfo starting");
            LoadSystemInfo();
            CWLog.Info("Boot", "startup phase 5: LoadSystemInfo done");
            _mainTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _mainTimer.Tick += OnTick;
            _mainTimer.Start();
            _animTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
            _animTimer.Tick += OnAnimTick;
            _animTimer.Start();
            _monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2000) };
            _monitorTimer.Tick += OnMonitorTick;
            _monitorTimer.Start();

            SourceInitialized += OnMainSourceInitialized;
            Loaded += (s2, e2) => { try { EnsureNormalWindowZOrder(); } catch (Exception ex5) { CWLog.Error("MainWindow", ex5); } };
            KeyDown += (sk, ek) => { if (ek.Key == Key.K && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control) { ShowSearchPalette(); ek.Handled = true; } };
            StateChanged += (s, e) =>
            {
                try { EnsureNormalWindowZOrder(); } catch (Exception ex6) { CWLog.Error("MainWindow", ex6); }
                if (WindowState == WindowState.Minimized)
                {
                    MinimizeMemory();
                    if (_minimizeToTray)
                    {
                        Hide();
                        ShowInTaskbar = false;
                    }
                }
            };
            if (!Program.IsSafeStartup) InitTrayIcon();

            LoadOverlaySettings();
            LoadColorSetting();
            LoadAutoRamCleanSetting();
            LoadMinimizeToTraySetting();
            LoadStartWithWindowsSetting();
            LoadGameGpuPowerSetting();
            SyncStartWithWindowsRegistry();
            if (_autoScheduler) InitScheduler();
            // Active startup policy: Respect user's saved automation and autopilot configuration.
            // When AutoPilot is configured, automatically initialize AutoPilot mode so ClearWin
            // actively optimizes RAM, manages process priorities, and boosts game performance.
            try
            {
                if (_autoPilot)
                {
                    _agent.SetMode(AgentMode.AutoPilot);
                    _agent.EnableAutopilot();
                    _agent.SetFpsFloor60(true);
                    _agent.FullOptimizationMode = true;
                    CWLog.Info("MainWindow", "AutoPilot mode activated with 60 FPS Floor & Full Optimization from user configuration.");
                }
                else
                {
                    _agent.SetMode(AgentMode.Off);
                }
            } catch (Exception ex8) { CWLog.Error("MainWindow.StartupMode", ex8); }
            UpdateWoaActivateButton();

            // Recover any leftover Registry modifications from an interrupted prior session on startup.
            // Dedicated GPU & GameLoop Registry bindings are applied on-demand when a game actually runs.
            try
            {
                SmartAgent.RestoreAllBackedUpRegistrySettings();
            }
            catch (Exception exGpuInit) { CWLog.Error("MainWindow.GpuInit", exGpuInit); }

            Closed += (s2, e2) =>
            {
                // Stop the per-500ms and per-15ms timers so no work continues after
                // the window closes (previously both kept firing forever).
                try { if (_mainTimer != null) _mainTimer.Stop(); } catch (Exception ex12) { CWLog.Error("MainWindow", ex12); }
                try { if (_animTimer != null) _animTimer.Stop(); } catch (Exception ex13) { CWLog.Error("MainWindow", ex13); }
                try { if (_monitorTimer != null) _monitorTimer.Stop(); } catch (Exception ex14) { CWLog.Error("MainWindow", ex14); }
                HideOverlay();
                try { RestoreGameProcessPriorities(); } catch (Exception exPriorityRestore) { CWLog.Error("MainWindow.PriorityRestore", exPriorityRestore); }

                // Always stop AI and restore transient tuning before the process exits.
                try { _agent.SetMode(AgentMode.Off); } catch (Exception ex14) { CWLog.Error("MainWindow", ex14); }
                try { _agent.RestoreFramePacing(); } catch (Exception ex15) { CWLog.Error("MainWindow", ex15); }
                try { _agent.RestoreNetworkTuning(); } catch (Exception ex16) { CWLog.Error("MainWindow", ex16); }
                try { SmartAgent.RestoreAllBackedUpRegistrySettings(); } catch (Exception exRegClose) { CWLog.Error("MainWindow.RegistryRestore", exRegClose); }

                if (_trayIcon != null) try { _trayIcon.GetType().GetProperty("Visible").SetValue(_trayIcon, false, null); } catch (Exception ex17) { CWLog.Error("MainWindow", ex17); }
                try
                {
                    var helper = new System.Windows.Interop.WindowInteropHelper(this);
                    if (helper.Handle != IntPtr.Zero)
                    {
                        UnregisterHotKey(helper.Handle, HOTKEY_ID);
                        UnregisterHotKey(helper.Handle, HOTKEY_ID_KILLSWITCH);
                        UnregisterHotKey(helper.Handle, HOTKEY_ID_PAUSE);
                        UnregisterHotKey(helper.Handle, HOTKEY_ID_EMERGENCY);
                    }
                } catch (Exception ex18) { CWLog.Error("MainWindow", ex18); }
            };
            Closing += (s3, e3) =>
            {
                if (_minimizeToTray && !_shutdownPending)
                {
                    e3.Cancel = true;
                    Hide();
                    ShowInTaskbar = false;
                    CWLog.Info("MainWindow", "Closing intercepted: minimized to system tray, background engine running.");
                    ShowNotification("ClearWin 2.7.7", "يعمل ClearWin الآن في درج النظام لمواصلة تسريع وحماية الأداء.");
                    return;
                }
                _shutdownPending = true;
                CWLog.Info("MainWindow", "Closing requested: shutdownPending=" + _shutdownPending);
            };
            Closed += (s4, e4) =>
            {
                CWLog.Info("MainWindow", "Window closed - application shutting down (shutdownPending=" + _shutdownPending + ")");
                // Revert global timer resolution to Windows default (15.625ms) on exit.
                // MainWindow.Logic sets 0.5ms at lines ~1099/1288 with no prior revert.
                try { if (_timerResActive) { uint cur; NtSetTimerResolution(5000, false, out cur); _timerResActive = false; } } catch (Exception ex19) { CWLog.Error("MainWindow", ex19); }
                // Full system restore on exit so the app never leaves the machine in a
                // modified state: game-mode changes (plan, core parking, services),
                // then agent-level revert (power plan, process priorities, cores,
                // journal), then restore the exact power plan that was active at
                // startup (covers custom plans and the autonomous "power saver" case).
                try { RevertGameMode(); } catch (Exception ex20) { CWLog.Error("MainWindow", ex20); }
                try { _agent.EmergencyStopAllOptimizations(); } catch (Exception ex21) { CWLog.Error("MainWindow", ex21); }
                try
                {
                    SmartAgent.RestoreBalancedOrPreviousPowerPlan(_startupPlanGuid);
                }
                catch (Exception ex22) { CWLog.Error("MainWindow", ex22); }
                try { _frameTracker.Stop(); _frameTracker.Dispose(); } catch (Exception ex23) { CWLog.Error("MainWindow", ex23); }
                try { _agent.Stop(); } catch (Exception ex24) { CWLog.Error("MainWindow", ex24); }
                try { if (_cpuLoadPc != null) _cpuLoadPc.Dispose(); } catch (Exception ex25) { CWLog.Error("MainWindow", ex25); }
                try { if (_cpuTempPc != null) _cpuTempPc.Dispose(); } catch (Exception ex26) { CWLog.Error("MainWindow", ex26); }
                try { if (_gpuTempPc != null) _gpuTempPc.Dispose(); } catch (Exception ex27) { CWLog.Error("MainWindow", ex27); }
                try { foreach (var pc in _gpuUtilPcs) pc.Dispose(); } catch (Exception ex28) { CWLog.Error("MainWindow", ex28); }
                try
                {
                    if (_trayNi != null)
                    {
                        var t = _trayNi.GetType();
                        t.GetProperty("Visible").SetValue(_trayNi, false, null);
                    }
                } catch (Exception ex29) { CWLog.Error("MainWindow", ex29); }
                try
                {
                    if (_trayForm != null)
                    {
                        var ft = _trayForm.GetType();
                        var closeM = ft.GetMethod("Close", new Type[] { });
                        if (closeM != null) closeM.Invoke(_trayForm, new object[] { });
                        var disposeM = ft.GetMethod("Dispose", new Type[] { });
                        if (disposeM != null) disposeM.Invoke(_trayForm, null);
                    }
                } catch (Exception ex30) { CWLog.Error("MainWindow", ex30); }
                try
                {
                    if (_trayNi != null)
                    {
                        var disposeM = _trayNi.GetType().GetMethod("Dispose", new Type[] { });
                        if (disposeM != null) disposeM.Invoke(_trayNi, null);
                    }
                } catch (Exception ex31) { CWLog.Error("MainWindow", ex31); }
                _trayForm = null;
                _trayNi = null;
                _trayIcon = null;
                try
                {
                    var helper = new System.Windows.Interop.WindowInteropHelper(this);
                    if (helper.Handle != IntPtr.Zero)
                    {
                        var source = System.Windows.Interop.HwndSource.FromHwnd(helper.Handle);
                        if (source != null && _hookDelegate != null) source.RemoveHook(_hookDelegate);
                    }
                } catch (Exception ex32) { CWLog.Error("MainWindow", ex32); }
                try { if (_webUiHost != null) _webUiHost.Dispose(); } catch (Exception ex33) { CWLog.Error("MainWindow", ex33); }
                try { LocalWebBridge.Stop(); } catch (Exception exBridgeStop) { CWLog.Error("MainWindow.WebBridge", exBridgeStop); }
                try { CWLog.Flush(); } catch (Exception ex34) { CWLog.Error("MainWindow", ex34); }
                try { FlushDiagLog(); } catch (Exception ex35) { CWLog.Error("MainWindow", ex35); }
                // Force the WPF dispatcher/application message loop to terminate.
                // This is the final guard against auxiliary tray/WebView2 resources
                // keeping ClearWin.exe alive after the visible window is closed.
                try
                {
                    if (Application.Current != null) Application.Current.Shutdown();
                }
                catch (Exception ex36) { CWLog.Error("MainWindow.Shutdown", ex36); }
                try { Environment.Exit(0); } catch { }
            };
        }

        internal void ShowFromTray()
        {
            try
            {
                Show();
                ShowInTaskbar = true;
                WindowState = WindowState.Normal;
                var helper = new System.Windows.Interop.WindowInteropHelper(this);
                ShowWindow(helper.Handle, SW_RESTORE);
                SetForegroundWindow(helper.Handle);
                EnsureNormalWindowZOrder();
                Activate();
            } catch (Exception ex37) { CWLog.Error("MainWindow", ex37); }
        }

        public void ApplyPostBootStabilization()
        {
            if (Program.IsSafeStartup)
            {
                CWLog.Info("MainWindow.BootStabilization", "Skipped post-boot stabilization because SafeStartup (--safe-mode) is active.");
                return;
            }
            try
            {
                CWLog.Info("MainWindow.BootStabilization", "Starting post-boot system stabilization and performance burst...");

                // 1. Unpark CPU cores to prevent early micro-stutters
                try
                {
                    UnparkCPUCores(true);
                }
                catch (Exception exCores) { CWLog.Error("BootStabilization.Cores", exCores); }

                // 2. Set high-precision timer resolution (0.500ms)
                try
                {
                    uint cur;
                    int hr = NtSetTimerResolution(5000, true, out cur);
                    if (hr == 0) _timerResActive = true;
                }
                catch (Exception exTimer) { CWLog.Error("BootStabilization.Timer", exTimer); }

                // 3. Trim post-boot bloated memory working sets & purge standby list
                try
                {
                    OptimizeRAM();
                    PurgeStandbyList();
                }
                catch (Exception exRam) { CWLog.Error("BootStabilization.RAM", exRam); }

                // 4. Temporarily activate Highest Performance Power Plan (CPU/GPU Performance Burst)
                try
                {
                    SmartAgent.ActivateHighestPerformancePowerPlan();
                    AiDecisionLogger.RecordDecision(
                        "BootPerformanceBurst",
                        "تسريع إقلاع واستقرار النظام: تم إطلاق أقصى طاقة للمعالج وكارت الشاشة مؤقتاً لتسريع إقلاع ويندوز والتخلص من بطء وتجميد بداية التشغيل.",
                        0.98,
                        "Startup",
                        "SUCCESS",
                        "رفع تردد أنوية المعالج وإلغاء الركن وكبح تأخير الرندر في بداية تشغيل ويندوز لتسريع استقرار سطح المكتب."
                    );
                }
                catch (Exception exPower) { CWLog.Error("BootStabilization.Power", exPower); }

                // 5. Pre-enforce dedicated high-performance GPU bindings and GameLoop optimizations
                try
                {
                    SmartAgent.EnforceGameDedicatedGpuAndPower();
                }
                catch (Exception exGpu) { CWLog.Error("BootStabilization.Gpu", exGpu); }

                // 6. Ensure AutoPilot AI mode is active
                try
                {
                    if (_agent != null)
                    {
                        _agent.SetMode(AgentMode.AutoPilot);
                        _agent.EnableAutopilot();
                        _agent.SetFpsFloor60(true);
                        _agent.FullOptimizationMode = true;
                    }
                }
                catch (Exception exAgent) { CWLog.Error("BootStabilization.Agent", exAgent); }

                // 7. Launch Smart Settling Watcher: after initial stabilization window, transition back to Balanced if idle
                StartPostBootSmartSettler();

                CWLog.Info("MainWindow.BootStabilization", "Post-boot stabilization completed successfully.");
                ShowNotification("ClearWin 2.7.7", "تم تسريع استقرار النظام وإطلاق أقصى طاقة للمعالج وكارت الشاشة لإنهاء ثقل الإقلاع.");
            }
            catch (Exception ex)
            {
                CWLog.Error("MainWindow.BootStabilization", ex);
            }
        }

        private void StartPostBootSmartSettler()
        {
            Task.Run(async () =>
            {
                try
                {
                    // Allow the boot performance burst to run for 40 seconds so all startup services and apps finish their initialization smoothly
                    await Task.Delay(40000);

                    // Wait until system stabilizes or check if gaming started
                    for (int i = 0; i < 15; i++)
                    {
                        if (_agent != null && _agent.IsGameRunning())
                        {
                            // A game is active, maintain gaming power plan
                            return;
                        }
                        await Task.Delay(2000);
                    }

                    // If no game is running, smoothly transition back to Balanced to save power and keep temperatures cool
                    if (_agent == null || !_agent.IsGameRunning())
                    {
                        SmartAgent.RestoreBalancedOrPreviousPowerPlan();
                        AiDecisionLogger.RecordDecision(
                            "BootSettledBalanced",
                            "استقرار النظام بعد الإقلاع: تم الانتقال إلى وضع الموازنة الذكي وتبريد المعالج بعد اكتمال استقرار خدمات ويندوز بنجاح.",
                            0.95,
                            "PowerPlan",
                            "SUCCESS",
                            "انتهاء نافذة تسريع الإقلاع واستقرار استهلاك المعالج، والعودة إلى التردد الديناميكي المتوازن."
                        );
                        CWLog.Info("MainWindow.BootStabilization", "System stabilized post-boot; transitioned smoothly to Balanced power plan.");
                    }
                }
                catch (Exception ex)
                {
                    CWLog.Error("StartPostBootSmartSettler", ex);
                }
            });
        }

        internal void ShowNotification(string title, string msg)
        {
            try
            {
                if (_trayNi == null) return;
                var niType = _trayNi.GetType();
                var tipIconType = niType.Assembly.GetType("System.Windows.Forms.ToolTipIcon");
                var showBalloon = niType.GetMethod("ShowBalloonTip", new[] { typeof(int), typeof(string), typeof(string), tipIconType });
                if (showBalloon != null)
                    showBalloon.Invoke(_trayNi, new object[] { 2000, title, msg, tipIconType != null ? Enum.ToObject(tipIconType, 1) : 1 });
            } catch (Exception ex38) { CWLog.Error("MainWindow", ex38); }
        }

        private object _trayForm;
        private object _trayNi;
        private const int WM_TRAYICON = 0x800;

        internal void InitTrayIcon()
        {
            try
            {
                var wfAsm = System.Reflection.Assembly.Load("System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
                var niType = wfAsm.GetType("System.Windows.Forms.NotifyIcon");
                if (niType == null) return;
                _trayNi = Activator.CreateInstance(niType);
                var iconAsm = System.Reflection.Assembly.Load("System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                var iconType = iconAsm.GetType("System.Drawing.Icon");
                if (iconType != null)
                {
                    var extractM = iconType.GetMethod("ExtractAssociatedIcon", new[] { typeof(string) });
                    if (extractM != null)
                    {
                        var icon = extractM.Invoke(null, new[] { System.Reflection.Assembly.GetExecutingAssembly().Location });
                        if (icon != null) niType.GetProperty("Icon").SetValue(_trayNi, icon, null);
                    }
                }
                niType.GetProperty("Text").SetValue(_trayNi, "ClearWin 2.7.7 - نشط في الخلفية", null);
                niType.GetProperty("Visible").SetValue(_trayNi, true, null);
                niType.GetProperty("BalloonTipTitle").SetValue(_trayNi, "ClearWin 2.7.7", null);
                niType.GetProperty("BalloonTipText").SetValue(_trayNi, "يعمل ClearWin في درج النظام لمواصلة تسريع وحماية الأداء.", null);

                var dcEvent = niType.GetEvent("DoubleClick");
                if (dcEvent != null)
                {
                    dcEvent.AddEventHandler(_trayNi, new EventHandler((ts, te) =>
                    {
                        Dispatcher.BeginInvoke(new Action(() => ShowFromTray()));
                    }));
                }
                var clickEvent = niType.GetEvent("Click");
                if (clickEvent != null)
                {
                    clickEvent.AddEventHandler(_trayNi, new EventHandler((ts, te) =>
                    {
                        Dispatcher.BeginInvoke(new Action(() => ShowFromTray()));
                    }));
                }
                var menuType = wfAsm.GetType("System.Windows.Forms.ContextMenuStrip");
                var miType = wfAsm.GetType("System.Windows.Forms.ToolStripMenuItem");
                if (menuType != null && miType != null)
                {
                    var menu = Activator.CreateInstance(menuType);
                    var itemsProp = menuType.GetProperty("Items");
                    var itemsColl = itemsProp.GetValue(menu, null);
                    var addM = itemsColl.GetType().GetMethod("Add", new[] { typeof(object) });
                    var showItem = Activator.CreateInstance(miType, new object[] { "\u0625\u0638\u0647\u0627\u0631 ClearWin" });
                    miType.GetEvent("Click").AddEventHandler(showItem, new EventHandler((ts, te) => { Dispatcher.BeginInvoke(new Action(() => ShowFromTray())); }));
                    addM.Invoke(itemsColl, new[] { showItem });
                    var boostItem = Activator.CreateInstance(miType, new object[] { "Boost \u0633\u0631\u064A\u0639" });
                    miType.GetEvent("Click").AddEventHandler(boostItem, new EventHandler((ts, te) => { Dispatcher.BeginInvoke(new Action(() => { OptimizeRAM(); PurgeStandbyList(); })); }));
                    addM.Invoke(itemsColl, new[] { boostItem });
                    var sep1 = Activator.CreateInstance(miType);
                    miType.GetProperty("Text").SetValue(sep1, "-", null);
                    addM.Invoke(itemsColl, new[] { sep1 });
                    var killItem = Activator.CreateInstance(miType, new object[] { "Kill Switch" });
                    miType.GetEvent("Click").AddEventHandler(killItem, new EventHandler((ts, te) => { Dispatcher.BeginInvoke(new Action(() => { if (_agent.IsKillSwitchActive) { _agent.ResetKillSwitch(); _agent.SetMode(AgentMode.AutoPilot); ShowNotification("Kill Switch OFF", "AI resumed"); } else { _agent.EmergencyKillSwitch(); ShowNotification("KILL SWITCH ON", "AI stopped"); } })); }));
                    addM.Invoke(itemsColl, new[] { killItem });
                    var pauseItem = Activator.CreateInstance(miType, new object[] { "Pause / Resume" });
                    miType.GetEvent("Click").AddEventHandler(pauseItem, new EventHandler((ts, te) => { Dispatcher.BeginInvoke(new Action(() => { if (_agent.IsPaused) { _agent.ResumeAgent(); ShowNotification("AI Resumed", "Agent unpaused"); } else { _agent.PauseAgent(60); ShowNotification("AI Paused", "60 ticks (~2 min)"); } })); }));
                    addM.Invoke(itemsColl, new[] { pauseItem });
                    var emergencyItem = Activator.CreateInstance(miType, new object[] { "Emergency Stop All" });
                    miType.GetEvent("Click").AddEventHandler(emergencyItem, new EventHandler((ts, te) => { Dispatcher.BeginInvoke(new Action(() => { _agent.EmergencyStopAllOptimizations(); ShowNotification("EMERGENCY STOP", "All optimizations reverted"); })); }));
                    addM.Invoke(itemsColl, new[] { emergencyItem });
                    var sep2 = Activator.CreateInstance(miType);
                    miType.GetProperty("Text").SetValue(sep2, "-", null);
                    addM.Invoke(itemsColl, new[] { sep2 });
                    var exitItem = Activator.CreateInstance(miType, new object[] { "\u062E\u0631\u0648\u062C" });
                    miType.GetEvent("Click").AddEventHandler(exitItem, new EventHandler((ts, te) => { Dispatcher.BeginInvoke(new Action(() => { _shutdownPending = true; try { niType.GetProperty("Visible").SetValue(_trayNi, false, null); } catch (Exception ex39) { CWLog.Error("MainWindow", ex39); } Application.Current.Shutdown(); try { Environment.Exit(0); } catch { } })); }));
                    addM.Invoke(itemsColl, new[] { exitItem });
                    niType.GetProperty("ContextMenuStrip").SetValue(_trayNi, menu, null);
                }
                _trayIcon = _trayNi;
            }
            catch
            {
                try
                {
                    var wfAsm2 = System.Reflection.Assembly.Load("System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
                    var niType2 = wfAsm2.GetType("System.Windows.Forms.NotifyIcon");
                    if (niType2 == null) return;
                    _trayIcon = Activator.CreateInstance(niType2);
                    var iconAsm2 = System.Reflection.Assembly.Load("System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
                    var iconType2 = iconAsm2.GetType("System.Drawing.Icon");
                    if (iconType2 != null)
                    {
                        var extractM2 = iconType2.GetMethod("ExtractAssociatedIcon", new[] { typeof(string) });
                        if (extractM2 != null)
                        {
                            var icon2 = extractM2.Invoke(null, new[] { System.Reflection.Assembly.GetExecutingAssembly().Location });
                            if (icon2 != null) niType2.GetProperty("Icon").SetValue(_trayIcon, icon2, null);
                        }
                    }
                    niType2.GetProperty("Text").SetValue(_trayIcon, "ClearWin", null);
                    niType2.GetProperty("Visible").SetValue(_trayIcon, true, null);
                    var dc2 = niType2.GetEvent("DoubleClick");
                    if (dc2 != null) dc2.AddEventHandler(_trayIcon, new EventHandler((ts, te) => { Dispatcher.BeginInvoke(new Action(() => ShowFromTray())); }));
                    var cl2 = niType2.GetEvent("Click");
                    if (cl2 != null) cl2.AddEventHandler(_trayIcon, new EventHandler((ts, te) => { Dispatcher.BeginInvoke(new Action(() => ShowFromTray())); }));
                } catch (Exception ex41) { CWLog.Error("MainWindow", ex41); }
            }
        }

        private void BuildSearchPalette()
        {
            _searchOverlay = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 10, 14, 23)),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Visibility = Visibility.Collapsed
            };
            Panel.SetZIndex(_searchOverlay, 9999);
            var outerGrid = new Grid();
            var centerPanel = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 500,
                Margin = new Thickness(0, -100, 0, 0)
            };

            var searchCard = new Border
            {
                Background = UI.GlassFill(),
                BorderBrush = UI.GlassBorder(),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(4)
            };
            var inputGrid = new Grid();
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
            inputGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var searchIcon = new TextBlock
            {
                Text = "\uE721",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 18,
                Foreground = new SolidColorBrush(Theme.TextSecondary),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(8, 0, 4, 0)
            };
            Grid.SetColumn(searchIcon, 0);
            inputGrid.Children.Add(searchIcon);

            _searchInput = new TextBox
            {
                Background = new SolidColorBrush(Colors.Transparent),
                Foreground = new SolidColorBrush(Theme.TextPrimary),
                FontSize = 16,
                FontFamily = new FontFamily("Segoe UI"),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8, 10, 8, 10),
                VerticalContentAlignment = VerticalAlignment.Center,
                CaretBrush = new SolidColorBrush(Theme.Accent)
            };
            _searchInput.TextChanged += SearchInput_TextChanged;
            _searchInput.KeyDown += SearchInput_KeyDown;
            Grid.SetColumn(_searchInput, 1);
            inputGrid.Children.Add(_searchInput);

            searchCard.Child = inputGrid;
            centerPanel.Children.Add(searchCard);

            _searchResults = new ListBox
            {
                Background = new SolidColorBrush(Color.FromArgb(160, 14, 30, 58)),
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 6, 0, 0),
                MaxHeight = 300,
                Padding = new Thickness(4)
            };
            var resultsBorder = new Border
            {
                Background = UI.GlassFill(),
                BorderBrush = UI.GlassBorder(),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Margin = new Thickness(0, 6, 0, 0),
                MaxHeight = 304,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 16, ShadowDepth = 4, Opacity = 0.15, Direction = 315 }
            };
            resultsBorder.Child = _searchResults;
            _searchResults.KeyDown += SearchResults_KeyDown;

            centerPanel.Children.Add(resultsBorder);

            var hint = new TextBlock
            {
                Text = "Esc to close  |  Enter to select  |  Arrow keys to navigate",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(139, 147, 168)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0)
            };
            centerPanel.Children.Add(hint);

            outerGrid.Children.Add(centerPanel);
            _searchOverlay.Child = outerGrid;

            _searchOverlay.MouseDown += (s, e) => { if (s == _searchOverlay) HideSearchPalette(); };
        }

        private void PopulateSearchResults(string query)
        {
            _searchResults.Items.Clear();
            string q = query.Trim().ToLower();
            if (string.IsNullOrEmpty(q)) { _searchOverlay.Visibility = Visibility.Collapsed; return; }

            string[][] pages = new string[][] {
                new[] { "\uE80F", "\u0646\u0638\u0631\u0629 \u0639\u0627\u0645\u0629", "Dashboard" },
                new[] { "\uE99A", "Smart Agent", "AI Agent" },
                new[] { "\uE945", "\u0623\u0644\u0639\u0627\u0628", "Gaming" },
                new[] { "\uEC4A", "\u062a\u062d\u0633\u064a\u0646", "Optimizer" },
                new[] { "\uE9D9", "\u0645\u0631\u0627\u0642\u0628\u0629", "Monitor" },
                new[] { "\uE714", "GPU", "GPU" },
                new[] { "\uE8A6", "\u062a\u062d\u0633\u064a\u0646\u0627\u062a", "Tweaks" },
                new[] { "\uE943", "\u0623\u062f\u0648\u0627\u062a", "Tools & Logs" },
                new[] { "\uE713", "\u0625\u0639\u062f\u0627\u062f\u0627\u062a", "Settings" }
            };

            foreach (var p in pages)
            {
                string name = p[1];
                string eng = p[2];
                if (name.ToLower().Contains(q) || eng.ToLower().Contains(q))
                {
                    var item = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 6, 8, 6) };
                    var icon = new TextBlock
                    {
                        Text = p[0],
                        FontFamily = new FontFamily("Segoe MDL2 Assets"),
                        FontSize = 16,
                        Foreground = new SolidColorBrush(Theme.Accent),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 12, 0)
                    };
                    item.Children.Add(icon);
                    var txt = new TextBlock
                    {
                        Text = name + "  (" + eng + ")",
                        FontSize = 14,
                        Foreground = new SolidColorBrush(Theme.TextPrimary),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    item.Children.Add(txt);
                    var resultItem = new ListBoxItem
                    {
                        Content = item,
                        Tag = name,
                        Background = new SolidColorBrush(Colors.Transparent),
                        BorderThickness = new Thickness(0),
                        Padding = new Thickness(4, 2, 4, 2)
                    };
                    _searchResults.Items.Add(resultItem);
                }
            }

            string[][] actions = new string[][] {
                new[] { "\uE922", "Optimize RAM", "optimize ram" },
                new[] { "\uE9F9", "Clean Shader Cache", "shader cache" },
                new[] { "\uE945", "Toggle Game Mode", "game mode" },
                new[] { "\uE714", "Toggle Auto Priority", "priority auto" },
                new[] { "\uE945", "Run All Optimizations", "all optimize" },
                new[] { "\uE74E", "Export System Report", "report export" },
                new[] { "\uE8A6", "Run Tweaks", "tweaks run" },
                new[] { "\uE74E", "Advanced Disk Clean", "disk clean" },
                new[] { "\uE945", "Unpark CPU Cores", "cpu unpark" },
                new[] { "\uE945", "Enable GPU MSI Mode", "gpu msi" },
                new[] { "\uE945", "Toggle Timer Resolution", "timer resolution" },
                new[] { "\uE945", "Apply Latency Tweaks", "latency" },
                new[] { "\uE945", "Apply CPU Priorities", "cpu priority" },
                new[] { "\uE945", "Run Debloater", "debloater bloat" },
                new[] { "\uE945", "Toggle Services Booster", "services boost" },
                new[] { "\uE7BA", "AI Kill Switch", "kill switch stop emergency" },
                new[] { "\uE769", "AI Pause / Resume", "pause resume stop agent" },
                new[] { "\uE711", "Emergency Stop All", "emergency stop all revert" }
            };

            foreach (var a in actions)
            {
                if (a[2].Contains(q) || a[1].ToLower().Contains(q))
                {
                    var item = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 6, 8, 6) };
                    var icon = new TextBlock
                    {
                        Text = a[0],
                        FontFamily = new FontFamily("Segoe MDL2 Assets"),
                        FontSize = 16,
                        Foreground = new SolidColorBrush(Theme.Green),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 12, 0)
                    };
                    item.Children.Add(icon);
                    var txt = new TextBlock
                    {
                        Text = a[1],
                        FontSize = 14,
                        Foreground = new SolidColorBrush(Theme.TextPrimary),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    item.Children.Add(txt);
                    var resultItem = new ListBoxItem
                    {
                        Content = item,
                        Tag = "ACTION:" + a[1],
                        Background = new SolidColorBrush(Colors.Transparent),
                        BorderThickness = new Thickness(0),
                        Padding = new Thickness(4, 2, 4, 2)
                    };
                    _searchResults.Items.Add(resultItem);
                }
            }

            if (_searchResults.Items.Count > 0) _searchResults.SelectedIndex = 0;
        }

        private void SearchInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            PopulateSearchResults(_searchInput.Text);
            if (_searchResults.Items.Count > 0) _searchOverlay.Visibility = Visibility.Visible;
        }

        private void SearchInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { HideSearchPalette(); e.Handled = true; }
            else if (e.Key == Key.Enter) { ExecuteSearchResult(); e.Handled = true; }
        }

        private void SearchResults_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { HideSearchPalette(); e.Handled = true; }
            else if (e.Key == Key.Enter) { ExecuteSearchResult(); e.Handled = true; }
        }



        private void ExecuteSearchResult()
        {
            if (_searchResults.SelectedItem == null) return;
            var item = _searchResults.SelectedItem as ListBoxItem;
            if (item == null) return;
            string tag = (string)item.Tag;

            HideSearchPalette();

            if (tag.StartsWith("ACTION:"))
            {
                string action = tag.Substring(7);
                if (action == "Optimize RAM") OptimizeRAM();
                else if (action == "Clean Shader Cache") CleanShaderCache();
                else if (action == "Toggle Game Mode") ActivateGameMode();
                else if (action == "Run All Optimizations") RunAllOptimizations();
                else if (action == "Export System Report") ExportSystemReport();
                else if (action == "Advanced Disk Clean") AdvancedDiskClean();
                else if (action == "Unpark CPU Cores") UnparkCPUCores();
                else if (action == "Enable GPU MSI Mode") EnableGPUMSIMode();
                else if (action == "Toggle Timer Resolution") ToggleTimerResolution();
                else if (action == "Apply Latency Tweaks") ApplyLatencyTweaks();
                else if (action == "Apply CPU Priorities") ApplySmartCPUPriorities();
                else if (action == "Run Debloater") RunDebloater();
                else if (action == "Toggle Services Booster") ToggleServicesBooster();
                else if (action == "AI Kill Switch") { if (_agent.IsKillSwitchActive) { _agent.ResetKillSwitch(); _agent.SetMode(AgentMode.AutoPilot); } else { _agent.EmergencyKillSwitch(); } }
                else if (action == "AI Pause / Resume") { if (_agent.IsPaused) _agent.ResumeAgent(); else _agent.PauseAgent(60); }
                else if (action == "Emergency Stop All") { _agent.EmergencyStopAllOptimizations(); }
            }
            else
            {
                SwitchTab(tag, _navItems.ContainsKey(GetNavKeyForTab(tag)) ? _navItems[GetNavKeyForTab(tag)] : null);
            }
        }

        private string GetNavKeyForTab(string tab)
        {
            if (tab == "\u0646\u0638\u0631\u0629 \u0639\u0627\u0645\u0629") return "Dashboard";
            if (tab == "شفافية الذكاء" || tab == "AI Transparency" || tab == "شفافية الذكاء الاصطناعي") return "AI Transparency";
            if (tab == "Smart Agent") return "AI Agent";
            if (tab == "\u0623\u0644\u0639\u0627\u0628") return "Game Boost";
            if (tab == "\u062a\u062d\u0633\u064a\u0646") return "System Boost";
            if (tab == "\u0645\u0631\u0627\u0642\u0628\u0629") return "Monitor";
            if (tab == "GPU") return "GPU";
            if (tab == "\u062a\u062d\u0633\u064a\u0646\u0627\u062a") return "Tweaks";
            if (tab == "\u0623\u062f\u0648\u0627\u062a") return "Tools";
            if (tab == "\u0625\u0639\u062f\u0627\u062f\u0627\u062a") return "Settings";
            if (tab == "\u0627\u0644\u0634\u0628\u0643\u0629") return "Network";
            if (tab == "\u0627\u0644\u0623\u0642\u0631\u0627\u0635") return "Disk";
            if (tab == "\u0627\u0644\u0623\u0645\u0627\u0646") return "Health & Safety";
            if (tab == "\u0627\u0644\u0630\u0643\u0627\u0621 \u0627\u0644\u0645\u062a\u0642\u062f\u0645") return "Advanced AI";
            if (tab == "\u0645\u062d\u0627\u0643\u0627\u0629 \u0627\u0644\u0630\u0643\u0627\u0621") return "AI Sandbox";
            return tab;
        }

        public void ShowSearchPalette()
        {
            if (_searchOverlay == null) BuildSearchPalette();
            if (_mainContent != null)
            {
                var parent = _mainContent.Parent;
                while (parent != null && !(parent is Grid) && parent is FrameworkElement) parent = ((FrameworkElement)parent).Parent;
                if (parent is Grid)
                {
                    var g = (Grid)parent;
                    if (!g.Children.Contains(_searchOverlay)) g.Children.Add(_searchOverlay);
                }
            }
            _searchOverlay.Visibility = Visibility.Visible;
            _searchInput.Text = "";
            _searchResults.Items.Clear();
            _searchInput.Focus();
            Keyboard.Focus(_searchInput);
        }

        private void HideSearchPalette()
        {
        if (_searchOverlay != null) _searchOverlay.Visibility = Visibility.Collapsed;
        }

        private Border MakeCard(int radius, Color? bg)
        {
            double rr = Math.Max(24, Math.Min(32, radius));
            var card = new Border {
                Background = UI.GlassFill(),
                CornerRadius = new CornerRadius(rr),
                Margin = new Thickness(10),
                BorderBrush = UI.GlassBorder(),
                BorderThickness = new Thickness(1),
                ClipToBounds = true
            };
            card.MouseEnter += (s, e) =>
            {
                card.RenderTransform = new TranslateTransform(0, -2);
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(170, 255, 255, 255));
            };
            card.MouseLeave += (s, e) =>
            {
                card.RenderTransform = new TranslateTransform(0, 0);
                card.BorderBrush = UI.GlassBorder();
            };
            return card;
        }
        private TextBlock MakeText(string text, double size, Color color, bool bold)
        {
            return new TextBlock { Text = text, FontSize = size, Foreground = new SolidColorBrush(color), FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal };
        }

        private string GetModeDisplayName(AgentMode mode)
        {
            switch (mode)
            {
                case AgentMode.Off: return "متوقف";
                case AgentMode.AutoPilot: return "الطيار الآلي";
                case AgentMode.Assisted: return "الوضع المساعد";
                case AgentMode.GameMode: return "وضع الألعاب";
                case AgentMode.BalanceMode: return "متوازن";
                case AgentMode.CoolMode: return "تبريد";
                case AgentMode.Custom: return "مخصص";
                default: return "متوقف";
            }
        }

        private string GetModeDisplaySub(AgentMode mode)
        {
            switch (mode)
            {
                case AgentMode.Off: return "الوكيل الذكي غير مفعّل";
                case AgentMode.AutoPilot: return "أداء كامل بقيادة الذكاء الاصطناعي";
                case AgentMode.Assisted: return "اقتراحات دون تنفيذ تلقائي";
                case AgentMode.GameMode: return "أقصى أداء للألعاب";
                case AgentMode.BalanceMode: return "توازن بين الأداء والاستقرار";
                case AgentMode.CoolMode: return "تخفيض الحرارة وهدوء النظام";
                case AgentMode.Custom: return "إعدادات مخصصة";
                default: return "";
            }
        }

        private void UpdateModeCard()
        {
            try
            {
                AgentMode mode = _agent != null ? _agent.Mode : AgentMode.Off;
                if (_modeCardTitle != null) _modeCardTitle.Text = GetModeDisplayName(mode);
                if (_modeCardSub != null) _modeCardSub.Text = GetModeDisplaySub(mode);
            }
            catch (Exception ex42) { CWLog.Error("MainWindow", ex42); }
        }

        private Border MakeToggle(bool isOn, Action<bool> onToggle)
        {
            Color trackOn = Theme.Accent;
            var track = new Border {
                Width = 44, Height = 24, CornerRadius = new CornerRadius(12),
                Background = new SolidColorBrush(isOn ? trackOn : Theme.BorderClr),
                Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 8, 0),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                Effect = isOn ? new DropShadowEffect { Color = trackOn, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.4 } : null
            };
            var thumb = new Border {
                Width = 18, Height = 18, CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(Colors.White),
                Margin = new Thickness(isOn ? 23 : 3, 0, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center,
                Effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 6, ShadowDepth = 1, Opacity = 0.5 }
            };
            track.Child = thumb;
            track.MouseDown += (s, e) =>
            {
                bool newState = !(track.Background is SolidColorBrush && ((SolidColorBrush)track.Background).Color == trackOn);
                track.Background = new SolidColorBrush(newState ? trackOn : Theme.BorderClr);
                thumb.Margin = new Thickness(newState ? 23 : 3, 0, 0, 0);
                track.Effect = newState ? new DropShadowEffect { Color = trackOn, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.4 } : null;
                onToggle(newState);
            };
            return track;
        }

        private Border MakeSectionHeader(string icon, string title, Color accentColor)
        {
            var header = new Border {
                Background = new LinearGradientBrush {
                    StartPoint = new Point(0, 0), EndPoint = new Point(1, 0),
                    GradientStops = {
                        new GradientStop(Color.FromArgb(35, accentColor.R, accentColor.G, accentColor.B), 0),
                        new GradientStop(Color.FromArgb(8, accentColor.R, accentColor.G, accentColor.B), 1)
                    }
                },
                CornerRadius = new CornerRadius(12), Padding = new Thickness(18, 12, 18, 12), Margin = new Thickness(10, 8, 10, 4),
                BorderBrush = new SolidColorBrush(Color.FromArgb(30, accentColor.R, accentColor.G, accentColor.B)),
                BorderThickness = new Thickness(1)
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var iconBorder = new Border {
                Width = 32, Height = 32, CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(25, accentColor.R, accentColor.G, accentColor.B)),
                Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center
            };
            iconBorder.Child = MakeText(icon, 16, accentColor, false);
            row.Children.Add(iconBorder);
            row.Children.Add(MakeText(title, 15, Theme.TextPrimary, true));
            header.Child = row;
            return header;
        }

        private ListBox MakeStyledListBox(double height)
        {
            var lb = new ListBox {
                Background = new SolidColorBrush(Color.FromArgb(180, Theme.SubCard.R, Theme.SubCard.G, Theme.SubCard.B)),
                Foreground = new SolidColorBrush(Theme.TextPrimary),
                BorderBrush = new SolidColorBrush(Color.FromArgb(30, Theme.BorderClr.R, Theme.BorderClr.G, Theme.BorderClr.B)),
                BorderThickness = new Thickness(1),
                FontSize = 12, Height = height,
                Padding = new Thickness(4)
            };
            var style = new Style(typeof(ListBoxItem));
            style.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, new SolidColorBrush(Colors.Transparent)));
            style.Setters.Add(new Setter(ListBoxItem.ForegroundProperty, new SolidColorBrush(Theme.TextPrimary)));
            style.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(10, 6, 10, 6)));
            style.Setters.Add(new Setter(ListBoxItem.MarginProperty, new Thickness(2, 1, 2, 1)));
            style.Setters.Add(new Setter(ListBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch));
            lb.ItemContainerStyle = style;
            return lb;
        }

        private void BuildUI()
        {
            CWLog.Info("UI", "Building ClearWin Modern Web Glass UI");
            try
            {
                _mainContent = new ContentControl { Visibility = Visibility.Collapsed };
                var root = new Grid { Background = new SolidColorBrush(Theme.Background) };
                root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
                root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                // Top Integrated Glass TitleBar spanning full width
                var titleBar = CreateNativeTitleBar();
                Grid.SetRow(titleBar, 0);
                root.Children.Add(titleBar);

                // Main workspace: Embedded modern WebView2 UI
                _webUiHost = new WebUiHost(this);
                Grid.SetRow(_webUiHost, 1);
                root.Children.Add(_webUiHost);

                Content = root;
                CWLog.Info("UI", "ClearWin Modern Web Glass UI loaded successfully as primary interface");
            }
            catch (Exception ex)
            {
                CWLog.Error("UI.Build", ex);
                try
                {
                    ShowWebUiFallback("تعذر تحميل واجهة WebView2: " + ex.Message);
                }
                catch (Exception ex2)
                {
                    CWLog.Error("UI.Fallback", ex2);
                }
            }
        }

        private void BuildSidebar(Grid root)
        {
            var sidebar = new Border {
                Background = Theme.SidebarGrad,
                Width = 260
            };
            Grid.SetColumn(sidebar, 0);
            root.Children.Add(sidebar);
            var sideGrid = new Grid();
            sidebar.Child = sideGrid;
            // Top accent glow
            var glow = new RadialGradientBrush { Center = new Point(0.5, 0), GradientOrigin = new Point(0.5, 0), RadiusX = 0.9, RadiusY = 0.9 };
            glow.GradientStops.Add(new GradientStop(Color.FromArgb(30, 96, 200, 255), 0));
            glow.GradientStops.Add(new GradientStop(Colors.Transparent, 0.62));
            sideGrid.Children.Add(new Border { Height = 300, VerticalAlignment = VerticalAlignment.Top, Background = glow, IsHitTestVisible = false });
            // Right edge accent line
            sideGrid.Children.Add(new Border { Width = 1, HorizontalAlignment = HorizontalAlignment.Right, Background = Theme.EdgeAccent, IsHitTestVisible = false });
            var outerGrid = new Grid { Margin = new Thickness(0) };
            outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            outerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            sideGrid.Children.Add(outerGrid);
            var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            var stack = new StackPanel { Margin = new Thickness(16, 0, 16, 0) };
            sv.Content = stack;
            // (GlassScrollRail removed: its 22px hit strip intercepted wheel/clicks
            //  over the standard scrollbar zone and blocked scrolling to the bottom.
            //  Standard Auto scrollbar restored — function over glass look.)
            // Brand: gradient "C" icon + "CLEARWIN" + version pill
            var brandStack = new StackPanel { Margin = new Thickness(8, 20, 8, 20) };
            var brandRow = new StackPanel { Orientation = Orientation.Horizontal };
            var logoGrad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            logoGrad.GradientStops.Add(new GradientStop(Theme.Accent, 0));
            logoGrad.GradientStops.Add(new GradientStop(Theme.Green, 1));
            var logoBg = new Border { Width = 42, Height = 42, CornerRadius = new CornerRadius(12), Background = logoGrad,
                Effect = new DropShadowEffect { Color = Theme.Accent, BlurRadius = 16, ShadowDepth = 0, Opacity = 0.35 } };
            var logoChar = MakeText("C", 21, Colors.White, true);
            logoChar.HorizontalAlignment = HorizontalAlignment.Center; logoChar.VerticalAlignment = VerticalAlignment.Center;
            logoBg.Child = logoChar;
            brandRow.Children.Add(logoBg);
            var nameStack = new StackPanel { Margin = new Thickness(12, 1, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var nameRow = new StackPanel { Orientation = Orientation.Horizontal };
            nameRow.Children.Add(MakeText("CLEAR", 17, Theme.TextPrimary, true));
            nameRow.Children.Add(MakeText("WIN", 17, Theme.Accent, true));
            var verPill = new Border { Margin = new Thickness(8, 0, 0, 0), Padding = new Thickness(6, 2, 6, 2), CornerRadius = new CornerRadius(9),
                Background = new SolidColorBrush(Color.FromArgb(22, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(70, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B)), BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center };
            var asmVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            verPill.Child = MakeText("v" + asmVersion.Major + "." + asmVersion.Minor, 8.5, Theme.Accent, true);
            nameRow.Children.Add(verPill);
            nameStack.Children.Add(nameRow);
            nameStack.Children.Add(MakeText("أذكى. أنظف. أسرع.", 9, Theme.TextMuted, false));
            brandRow.Children.Add(nameStack);
            brandStack.Children.Add(brandRow);
            // Status dot row under brand
            var statusRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            var liveDot = new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(Theme.Green), Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
            statusRow.Children.Add(liveDot);
            statusRow.Children.Add(MakeText("المراقبة نشطة", 10.5, Theme.TextSecondary, false));
            brandStack.Children.Add(statusRow);
            stack.Children.Add(brandStack);
            // Nav items grouped into sections
            object[] navDefs = new object[] {
                "مركز القيادة والشفافية",
                new string[] {"\uE9CE", "AI Transparency", "شفافية الذكاء"},
                "محرك التحسين والألعاب",
                new string[] {"\uE99A", "AI Agent", "Smart Agent"},
                new string[] {"\uE7AC", "Game Boost", "ألعاب"},
                new string[] {"\uE768", "System Boost", "تحسين"},
                "الصيانة والتسريع",
                new string[] {"\uE74D", "Debloater", "تنظيف"},
                new string[] {"\uE713", "Tweaks", "تحسينات"},
                new string[] {"\uE7E8", "Startup Manager", "بدء التشغيل"},
                "المراقبة وإدارة الموارد",
                new string[] {"\uE9D9", "Task Manager", "العمليات"},
                new string[] {"\uE968", "Network", "الشبكة"},
                new string[] {"\uE74E", "Disk", "الأقراص"},
                new string[] {"\uE7F4", "GPU", "GPU"},
                new string[] {"\uE774", "Tools", "أدوات"},
                "النظام والأمان",
                new string[] {"\uE946", "Health & Safety", "الأمان"},
                new string[] {"\uE713", "Settings", "إعدادات"},
                "الذكاء المتقدم والتعلم",
                new string[] {"\uE9D9", "Advanced AI", "الذكاء المتقدم"},
                new string[] {"\uE9F5", "AI Sandbox", "محاكاة الذكاء"},
                new string[] {"\uE9A3", "Learning", "التعلم"},
                new string[] {"\uE784", "AI Activity", "نشاط الذكاء"},
                new string[] {"\uE757", "Game Center", "مركز الألعاب"},
                new string[] {"\uEA1C", "Engine", "المحرك"}
            };
            foreach (var nd in navDefs)
            {
                string sec = nd as string;
                if (sec != null) { stack.Children.Add(CreateSectionHeader(sec)); continue; }
                var arr = (string[])nd;
                string label = arr[1]; string tab = arr[2];
                bool ia = label == "AI Transparency";
                var item = CreateNavItem(arr[0], label, tab, ia);
                _navItems[label] = item;
                stack.Children.Add(item);
                if (ia) _activeNav = item;
            }
            // Bottom: Current Mode card
            var modeCard = new Border { Margin = new Thickness(10, 8, 10, 14), Padding = new Thickness(14, 12, 14, 12), CornerRadius = new CornerRadius(14),
                Background = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1), GradientStops = { new GradientStop(Color.FromArgb(28, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B), 0), new GradientStop(Color.FromArgb(12, Theme.CardClr.R, Theme.CardClr.G, Theme.CardClr.B), 1) } },
                BorderBrush = new SolidColorBrush(Color.FromArgb(90, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B)), BorderThickness = new Thickness(1),
                Effect = new DropShadowEffect { Color = Theme.Accent, BlurRadius = 18, ShadowDepth = 0, Opacity = 0.18 } };
            var modeStack = new StackPanel();
            var modeHead = new StackPanel { Orientation = Orientation.Horizontal };
            modeHead.Children.Add(MakeText("الوضع الحالي", 10.5, Theme.TextMuted, false));
            var modeLive = new Border { Width = 6, Height = 6, CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(Theme.Green), Margin = new Thickness(8, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            modeHead.Children.Add(modeLive);
            modeStack.Children.Add(modeHead);
            var modeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            var modeIcon = MakeText("\uE7AC", 16, Theme.Accent, false);
            modeIcon.FontFamily = new FontFamily("Segoe MDL2 Assets"); modeRow.Children.Add(modeIcon);
            var modeNameStack = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
            _modeCardTitle = MakeText(GetModeDisplayName(_agent != null ? _agent.Mode : AgentMode.Off), 13, Theme.Accent, true);
            _modeCardSub = MakeText(GetModeDisplaySub(_agent != null ? _agent.Mode : AgentMode.Off), 10, Theme.TextMuted, false);
            modeNameStack.Children.Add(_modeCardTitle);
            modeNameStack.Children.Add(_modeCardSub);
            modeRow.Children.Add(modeNameStack);
            modeStack.Children.Add(modeRow);
            modeCard.Child = modeStack;
            Grid.SetRow(modeCard, 1); outerGrid.Children.Add(modeCard);
        }

        private Border CreateSectionHeader(string title)
        {
            var b = new Border { Margin = new Thickness(26, 14, 8, 6) };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var dot = new Border { Width = 4, Height = 4, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Theme.Accent), Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(dot);
            row.Children.Add(MakeText(title, 10, Color.FromArgb(140, 255, 255, 255), true));
            b.Child = row;
            return b;
        }

        private Border CreateNavItem(string icon, string label, string tab, bool active)
        {
            var border = new Border { CornerRadius = new CornerRadius(12), Margin = new Thickness(8, 2, 8, 2), Padding = new Thickness(14, 0, 14, 0), Height = 46, Cursor = Cursors.Hand, ToolTip = tab };
            if (active)
            {
                border.Background = Theme.NavActiveGradient;
                border.BorderBrush = new SolidColorBrush(Color.FromArgb(90, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B));
                border.BorderThickness = new Thickness(1);
                border.Effect = new DropShadowEffect { Color = Theme.Accent, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.25 };
            }
            var dock = new DockPanel();
            var iconCol = active ? Theme.Accent : Color.FromArgb(150, 255, 255, 255);
            var iconText = new TextBlock { Text = icon, FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 15, Foreground = new SolidColorBrush(iconCol), Width = 20, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            dock.Children.Add(iconText);
            var labelText = MakeText(label, 13.5, active ? Theme.TextPrimary : Color.FromArgb(200, 255, 255, 255), active);
            labelText.Margin = new Thickness(10, 0, 0, 0); dock.Children.Add(labelText);
            border.Child = dock;
            string capturedLabel = label; string capturedTab = tab; Border capturedBorder = border;
            border.MouseEnter += (s2, e2) =>
            {
                if (capturedBorder != _activeNav)
                {
                    capturedBorder.Background = new SolidColorBrush(Color.FromArgb(18, 255, 255, 255));
                    capturedBorder.RenderTransform = new TranslateTransform(0, -1);
                }
            };
            border.MouseLeave += (s2, e2) =>
            {
                if (capturedBorder != _activeNav)
                {
                    capturedBorder.Background = null; capturedBorder.BorderThickness = new Thickness(0); capturedBorder.Effect = null;
                    capturedBorder.RenderTransform = new TranslateTransform(0, 0);
                }
            };
            border.MouseDown += (s2, e2) => SwitchTab(capturedTab, capturedBorder);
            return border;
        }

        private void SwitchTab(string tab, Border item)
        {
            _currentTab = tab;
            // Reset all AI page live-update flags
            _agentPageActive = false;
            _gameCenterPageActive = false;
            _aiLogPageActive = false;
            _learningPageActive = false;
            _advancedAIPageActive = false;
            _enginePageActive = false;
            _aiTransparencyPageActive = false;
            _startupPageActive = false;
            _networkPageActive = false;
            _diskPageActive = false;
            _toolsPageActive = false;
            _healthPageActive = false;

            if (tab == "شفافية الذكاء" || tab == "AI Transparency" || tab == "شفافية الذكاء الاصطناعي") _aiTransparencyPageActive = true;
            else if (tab == "Smart Agent") _agentPageActive = true;
            else if (tab == "Game Center") _gameCenterPageActive = true;
            else if (tab == "AI Activity") _aiLogPageActive = true;
            else if (tab == "Learning") _learningPageActive = true;
            else if (tab == "Advanced AI" || tab == "الذكاء المتقدم") _advancedAIPageActive = true;
            else if (tab == "Engine") _enginePageActive = true;
            else if (tab == "بدء التشغيل") _startupPageActive = true;
            else if (tab == "الشبكة") _networkPageActive = true;
            else if (tab == "الأقراص") _diskPageActive = true;
            else if (tab == "أدوات") _toolsPageActive = true;
            else if (tab == "Health & Safety" || tab == "الأمان") _healthPageActive = true;
            if (_activeNav != null)
            {
                _activeNav.Background = null;
                _activeNav.BorderBrush = null;
                _activeNav.BorderThickness = new Thickness(0);
                _activeNav.Effect = null;
                _activeNav.RenderTransform = new TranslateTransform(0, 0);
                // Remove active indicator bar from old nav
                var dockOld = _activeNav.Child as DockPanel;
                if (dockOld != null && dockOld.Children.Count > 0 && dockOld.Children[0] is Border && ((Border)dockOld.Children[0]).Width == 3)
                    dockOld.Children.RemoveAt(0);
                // Reset icon color and label style
                var tbOld = dockOld != null ? dockOld.Children[0] as TextBlock : null;
                if (dockOld != null && dockOld.Children.Count >= 2)
                {
                    var it = dockOld.Children[0] as TextBlock; if (it != null) it.Foreground = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255));
                    var lt = dockOld.Children[1] as TextBlock; if (lt != null) { lt.Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)); lt.FontWeight = FontWeights.Normal; }
                }
            }
            if (item != null)
            {
                item.Background = Theme.NavActiveGradient;
                item.BorderBrush = new SolidColorBrush(Color.FromArgb(90, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B));
                item.BorderThickness = new Thickness(1);
                item.Effect = new DropShadowEffect { Color = Theme.Accent, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.25 };
                // Add active indicator bar to new nav
                var dock = item.Child as DockPanel;
                if (dock != null)
                {
                    var activeBar = new Border { Width = 3, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Theme.Accent), Margin = new Thickness(0, 0, 10, 0) };
                    DockPanel.SetDock(activeBar, Dock.Left);
                    dock.Children.Insert(0, activeBar);
                    var iconTb = dock.Children[1] as TextBlock; if (iconTb != null) iconTb.Foreground = new SolidColorBrush(Theme.Accent);
                    var labelTb = dock.Children[2] as TextBlock; if (labelTb != null) { labelTb.Foreground = new SolidColorBrush(Theme.TextPrimary); labelTb.FontWeight = FontWeights.SemiBold; }
                }
                _activeNav = item;
            }
            UpdatePageHeader(tab);
            var newContent = BuildPage(tab);
            // Instant swap + quick fade-in only. The old fade-out-then-swap waited
            // ~150ms showing a blank page before loading, which felt sluggish on slow
            // GPUs. Swapping immediately then fading the new page in feels instant.
            if (_mainContent.Content != null && _animSpeed > 0)
            {
                _mainContent.Content = newContent;
                newContent.Opacity = 0;
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(_animSpeed == 2 ? 60 : 100));
                newContent.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
            else { _mainContent.Content = newContent; }
            if (_scrollViewer != null) _scrollViewer.ScrollToTop();
        }

        private void BuildMainContent(Grid root)
        {
            var outer = new Grid { Background = new SolidColorBrush(Colors.Transparent) };
            Grid.SetColumn(outer, 1);
            root.Children.Add(outer);
            outer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(56) }); // Top nav (Row 0)
            outer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Main content (Row 1)

            _statusTxt = _statusTxt ?? new TextBlock { Text = "ClearWin Active" };

            // === Top Navigation (56px) ===
            var topBar = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(160, 11, 17, 26)),
                Height = 56,
                BorderBrush = new SolidColorBrush(Theme.BorderClr),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
            var topDock = new DockPanel { Margin = new Thickness(24, 0, 24, 0) };

            // Left: current page context
            var logoStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var logoGrad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            logoGrad.GradientStops.Add(new GradientStop(Theme.Accent, 0));
            logoGrad.GradientStops.Add(new GradientStop(Theme.Green, 1));
            var logoBg = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(9), Background = logoGrad, Margin = new Thickness(0, 0, 10, 0) };
            var logoChar = MakeText("C", 17, Colors.White, true);
            logoChar.HorizontalAlignment = HorizontalAlignment.Center; logoChar.VerticalAlignment = VerticalAlignment.Center;
            logoBg.Child = logoChar; logoStack.Children.Add(logoBg);
            var nameStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _pageTitleText = MakeText("نظرة عامة", 15, Theme.TextPrimary, true);
            _pageSubtitleText = MakeText("ملخص الأداء والحالة", 9, Theme.TextMuted, false);
            nameStack.Children.Add(_pageTitleText);
            nameStack.Children.Add(_pageSubtitleText);
            logoStack.Children.Add(nameStack);
            DockPanel.SetDock(logoStack, Dock.Left); topDock.Children.Add(logoStack);

            // Right: Quick Actions (AI Transparency, Instant Boost, Smart Agent, Settings)
            var rightStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(rightStack, Dock.Right);

            // Quick AI Transparency button
            var aiTransBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(28, 56, 189, 248)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, 56, 189, 248)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(12, 6, 12, 6),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0),
                Effect = new DropShadowEffect { Color = Theme.Accent, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.2 }
            };
            var aiTransRow = new StackPanel { Orientation = Orientation.Horizontal };
            aiTransRow.Children.Add(MakeText("\uE9CE", 12, Theme.Accent, false));
            aiTransRow.Children.Add(MakeText(" شفافية الذكاء", 11, Theme.Accent, true));
            aiTransBtn.Child = aiTransRow;
            aiTransBtn.MouseEnter += (s2, e2) => { aiTransBtn.Background = new SolidColorBrush(Color.FromArgb(50, 56, 189, 248)); };
            aiTransBtn.MouseLeave += (s2, e2) => { aiTransBtn.Background = new SolidColorBrush(Color.FromArgb(28, 56, 189, 248)); };
            aiTransBtn.MouseDown += (s2, e2) => SwitchTab("شفافية الذكاء", _navItems.ContainsKey("AI Transparency") ? _navItems["AI Transparency"] : _activeNav);
            rightStack.Children.Add(aiTransBtn);

            // Quick Instant Boost button
            var boostBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(28, 16, 185, 129)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, 16, 185, 129)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(12, 6, 12, 6),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0),
                Effect = new DropShadowEffect { Color = Theme.Green, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.2 }
            };
            var boostRow = new StackPanel { Orientation = Orientation.Horizontal };
            boostRow.Children.Add(MakeText("\uE768", 12, Theme.Green, false));
            boostRow.Children.Add(MakeText(" تسريع فوري", 11, Theme.Green, true));
            boostBtn.Child = boostRow;
            boostBtn.MouseEnter += (s2, e2) => { boostBtn.Background = new SolidColorBrush(Color.FromArgb(50, 16, 185, 129)); };
            boostBtn.MouseLeave += (s2, e2) => { boostBtn.Background = new SolidColorBrush(Color.FromArgb(28, 16, 185, 129)); };
            boostBtn.MouseDown += (s2, e2) =>
            {
                try
                {
                    OptimizeRAM();
                    PurgeStandbyList();
                    ShowToast("تم تنظيف الرام وتسريع النظام فوراً!", Theme.Green);
                }
                catch { }
            };
            rightStack.Children.Add(boostBtn);

            // Smart Agent shortcut
            var aiBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(25, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B)),
                BorderBrush = new SolidColorBrush(Theme.Accent),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(12, 6, 12, 6),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0),
                Effect = new DropShadowEffect { Color = Theme.Accent, BlurRadius = 10, ShadowDepth = 0, Opacity = 0.15 }
            };
            var aiRow = new StackPanel { Orientation = Orientation.Horizontal };
            aiRow.Children.Add(MakeText("\uE99A", 12, Theme.Accent, false));
            aiRow.Children.Add(MakeText(" الوكيل", 11, Theme.Accent, true));
            aiBtn.Child = aiRow;
            aiBtn.MouseEnter += (s2, e2) => { aiBtn.Background = new SolidColorBrush(Color.FromArgb(45, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B)); };
            aiBtn.MouseLeave += (s2, e2) => { aiBtn.Background = new SolidColorBrush(Color.FromArgb(25, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B)); };
            aiBtn.MouseDown += (s2, e2) => SwitchTab("Smart Agent", _navItems.ContainsKey("AI Agent") ? _navItems["AI Agent"] : _activeNav);
            rightStack.Children.Add(aiBtn);

            // Settings button
            var settingsBtn = new Border
            {
                Background = new SolidColorBrush(Theme.SearchBg),
                BorderBrush = new SolidColorBrush(Theme.BorderClr),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(18),
                Padding = new Thickness(12, 6, 12, 6),
                Cursor = Cursors.Hand
            };
            var settRow = new StackPanel { Orientation = Orientation.Horizontal };
            settRow.Children.Add(MakeText("\uE713", 12, Theme.TextMuted, false));
            settRow.Children.Add(MakeText(" الإعدادات", 11, Theme.TextMuted, false));
            settingsBtn.Child = settRow;
            settingsBtn.MouseEnter += (s2, e2) => { settingsBtn.Background = new SolidColorBrush(Color.FromArgb(16, 255, 255, 255)); };
            settingsBtn.MouseLeave += (s2, e2) => { settingsBtn.Background = new SolidColorBrush(Theme.SearchBg); };
            settingsBtn.MouseDown += (s2, e2) => SwitchTab("إعدادات", _navItems.ContainsKey("Settings") ? _navItems["Settings"] : _activeNav);
            rightStack.Children.Add(settingsBtn);

            topDock.Children.Add(rightStack);

            // Center: Search Bar
            var searchStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            var searchBorder = new Border { Background = new SolidColorBrush(Theme.SearchBg), BorderBrush = new SolidColorBrush(Theme.BorderClr), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(18), Padding = new Thickness(14, 6, 8, 6), Width = 260, Cursor = Cursors.Hand };
            var searchRow = new StackPanel { Orientation = Orientation.Horizontal };
            searchRow.Children.Add(MakeText("\uE721", 12, Theme.TextMuted, false));
            searchRow.Children.Add(MakeText("  بحث", 11, Color.FromArgb(160, 255, 255, 255), false));
            var searchHint = new Border
            {
                Margin = new Thickness(12, 0, 0, 0),
                Padding = new Thickness(6, 2, 6, 2),
                CornerRadius = new CornerRadius(6),
                Background = new SolidColorBrush(Color.FromArgb(14, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center
            };
            searchHint.Child = MakeText("Ctrl K", 9.0, Color.FromArgb(140, 255, 255, 255), false);
            searchRow.Children.Add(searchHint);
            searchBorder.Child = searchRow;
            searchBorder.MouseEnter += (s, e) => searchBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(160, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B));
            searchBorder.MouseLeave += (s, e) => searchBorder.BorderBrush = new SolidColorBrush(Theme.BorderClr);
            searchBorder.MouseDown += (s, e) => ShowSearchPalette();
            searchStack.Children.Add(searchBorder);
            DockPanel.SetDock(searchStack, Dock.Top); topDock.Children.Add(searchStack);

            topBar.Child = topDock;
            Grid.SetRow(topBar, 0);
            outer.Children.Add(topBar);

            // ScrollViewer
            _scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Margin = new Thickness(0), Background = new SolidColorBrush(Colors.Transparent) };
            string startupTab = "شفافية الذكاء";
            _mainContent = new ContentControl { Content = BuildPage(startupTab), Margin = new Thickness(24, 12, 24, 0) };
            UpdatePageHeader(startupTab);
            _scrollViewer.Content = _mainContent;
            Grid.SetRow(_scrollViewer, 1);
            outer.Children.Add(_scrollViewer);
        }

        private void UpdatePageHeader(string tab)
        {
            if (_pageTitleText == null || _pageSubtitleText == null) return;
            string title = tab;
            string subtitle = "ClearWin";
            if (tab == "نظرة عامة") { title = "نظرة عامة"; subtitle = "ملخص الأداء والحالة"; }
            else if (tab == "شفافية الذكاء" || tab == "AI Transparency" || tab == "شفافية الذكاء الاصطناعي") { title = "شفافية الذكاء الاصطناعي"; subtitle = "رؤية حية ومباشرة لقرارات النماذج الـ 7 المترابطة بدون أي غموض"; }
            else if (tab == "مراقبة") { title = "المراقبة"; subtitle = "تابع الموارد لحظة بلحظة"; }
            else if (tab == "Smart Agent") { title = "المساعد الذكي"; subtitle = "تحسينات تلقائية مبنية على حالة جهازك"; }
            else if (tab == "تحسين") { title = "تحسين النظام"; subtitle = "إجراءات آمنة لتحسين الاستجابة"; }
            else if (tab == "ألعاب") { title = "وضع الألعاب"; subtitle = "جهّز جهازك لأفضل أداء"; }
            else if (tab == "تنظيف") { title = "تنظيف النظام"; subtitle = "استعد مساحة وحافظ على النظام مرتبًا"; }
            else if (tab == "تحسينات") { title = "تحسينات"; subtitle = "تخصيصات متقدمة للنظام"; }
            else if (tab == "بدء التشغيل") { title = "بدء التشغيل"; subtitle = "إدارة البرامج عند تشغيل الويندوز"; }
            else if (tab == "العمليات") { title = "العمليات"; subtitle = "مراقبة وإدارة العمليات النشطة"; }
            else if (tab == "الشبكة") { title = "معلومات الشبكة"; subtitle = "عرض وحل مشاكل الاتصال"; }
            else if (tab == "الأقراص") { title = "تحليل الأقراص"; subtitle = "استخدام المساحة وتحسين الأداء"; }
            else if (tab == "أدوات") { title = "الأدوات"; subtitle = "أدوات مساعدة للنظام"; }
            else if (tab == "إعدادات") { title = "الإعدادات"; subtitle = "خصّص تجربة ClearWin"; }
            else if (tab == "Health & Safety" || tab == "الأمان") { title = "الصحة والأمان"; subtitle = "تابع حالة العتاد والحماية"; }
            else if (tab == "Advanced AI" || tab == "الذكاء المتقدم") { title = "الذكاء المتقدم"; subtitle = "مستويات التحليل والتنبؤ"; }
            else if (tab == "Game Center") { title = "مركز الألعاب"; subtitle = "تحسين الألعاب"; }
            else if (tab == "AI Activity") { title = "نشاط الذكاء"; subtitle = "سجل القرارات"; }
            else if (tab == "Learning") { title = "التعلم"; subtitle = "قاعدة المعرفة"; }
            else if (tab == "GPU") { title = "كرت الشاشة"; subtitle = "معلومات ومعالجة GPU"; }
            else if (tab == "AI Sandbox" || tab == "محاكاة الذكاء") { title = "محاكاة الذكاء"; subtitle = "محاكاة واختبار قرارات الذكاء الاصطناعي"; }
            else if (tab == "Engine") { title = "Engine"; subtitle = "المحرك: مؤشرات المكونات ومستويات الثقة والقرار الحالي"; }
            _pageTitleText.Text = title;
            _pageSubtitleText.Text = subtitle;
        }

        private void ToggleMaxRestore()
        {
            ToggleMaxRestoreWindow();
        }

        private UIElement BuildPage(string tab)
        {
            UIElement cached;
            if (_pageCache.TryGetValue(tab, out cached)) return cached;
            UIElement page;
            if (tab == "نظرة عامة") page = BuildDashboard();
            else if (tab == "شفافية الذكاء" || tab == "AI Transparency" || tab == "شفافية الذكاء الاصطناعي") page = BuildAiTransparencyPage();
            else if (tab == "مراقبة") page = BuildMonitor();
            else if (tab == "تحسين") page = BuildOptimizer();
            else if (tab == "تنظيف") page = BuildCleaner();
            else if (tab == "ألعاب") page = BuildGaming();
            else if (tab == "بدء التشغيل") page = BuildStartupPage();
            else if (tab == "العمليات") page = BuildProcessesPage();
            else if (tab == "الشبكة") page = BuildNetworkPage();
            else if (tab == "الأقراص") page = BuildDiskPage();
            else if (tab == "تحسينات") page = BuildTweaksPage();
            else if (tab == "أدوات") page = BuildToolsPage();
            else if (tab == "إعدادات") page = BuildSettings();
            else if (tab == "Smart Agent") page = BuildAgentPage();
            else if (tab == "Game Center") page = BuildGameCenterPage();
            else if (tab == "AI Activity") page = BuildAIActivityPage();
            else if (tab == "Learning") page = BuildLearningPage();
            else if (tab == "GPU") page = BuildGPUPage();
            else if (tab == "Health & Safety" || tab == "الأمان") page = BuildHealthSafetyPage();
            else if (tab == "Advanced AI" || tab == "الذكاء المتقدم") page = BuildAdvancedAIPage();
            else if (tab == "AI Sandbox" || tab == "محاكاة الذكاء") page = BuildSimulationPage();
            else if (tab == "Engine") page = BuildEnginePage();
            else page = BuildDashboard();
            _pageCache[tab] = page;
            return page;
        }

        private string TranslateAction(string action)
        {
            if (string.IsNullOrEmpty(action)) return action;
            string a = action.ToLower();
            if (a.Contains("aggressiveclean") || a.Contains("lightclean") || a.Contains("preemptiveclean")) return "تنظيف الذاكرة";
            if (a.Contains("purgestandby")) return "تطهير الذاكرة";
            if (a.Contains("limitbackground")) return "تقييد العمليات الخلفية";
            if (a.Contains("boostgame")) return "تعزيز الألعاب";
            if (a.Contains("emergency") || a.Contains("throttle")) return "تخفيض الحمل";
            if (a.Contains("thermal") || a.Contains("reduce")) return "الحماية الحرارية";
            if (a.Contains("stabilize") || a.Contains("frame")) return "تحسين الإطارات";
            if (a.Contains("battery")) return " توفير البطارية";
            if (a.Contains("affinity") || a.Contains("setgame")) return "تعيين أنوية المعالج";
            if (a.Contains("unpark")) return "تفعيل الأنوية";
            if (a.Contains("priority")) return "تحسين الأولوية";
            if (a.Contains("power")) return "خطة الطاقة";
            if (a.Contains("core")) return "ركن الأنوية";
            if (a.Contains("io")) return "أولوية الإدخال/الإخراج";
            if (a.Contains("scheduled")) return "مهلة مجدولة";
            if (a.Contains("predictive")) return "صيانة تنبؤية";
            if (a.Contains("forecast")) return "تنبؤ الموارد";
            if (a.Contains("userbehavior")) return "سلوك المستخدم";
            return action;
        }
    }
}
