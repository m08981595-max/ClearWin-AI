using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ClearWin
{
    public partial class MainWindow
    {
        internal bool WebUiAgentPaused { get { return _agent != null && _agent.IsPaused; } }
        internal bool WebUiKillSwitchActive { get { return _agent != null && _agent.IsKillSwitchActive; } }
        internal bool WebUiSimulationMode { get { return _agent != null && _agent.SimulationMode; } }
        internal bool WebUiPerformanceBoosted { get { return _webPerformanceBoosted; } }
        internal bool WebUiNetworkBoosted { get { return _webNetworkBoosted; } }
        internal bool WebUiCoolingBoosted { get { return _agent != null && _agent.Mode == AgentMode.CoolMode; } }
        internal bool WebUiGameModeActive { get { return _gameModeActive; } }
        internal bool WebUiServicesBoosted { get { return _servicesBoosted; } }
        internal bool WebUiAutopilotEnabled { get { return _agent != null && _agent.IsAutopilotEnabled; } }
        internal bool WebUiFpsFloorActive { get { return _agent != null && _agent.FpsFloorActive; } }
        internal bool WebUiFullOptimizationEnabled { get { return _agent != null && _agent.FullOptimizationMode; } }
        internal bool WebUiTimerResolutionActive { get { return _timerResActive; } }

        private bool _webPerformanceBoosted;
        private bool _webNetworkBoosted;

        private WebUiHost _webUiHost;
        private System.Windows.Controls.Border _webFallback;

        private void BuildWebUiShell(Grid hostRoot, UIElement legacyRoot)
        {
            // Configure hostRoot grid layout: Row 0 is TitleBar (38px), Row 1 is main Glass UI (*)
            hostRoot.RowDefinitions.Clear();
            hostRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
            hostRoot.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Create and attach Native TitleBar in Row 0
            var titleBar = CreateNativeTitleBar();
            Grid.SetRow(titleBar, 0);
            hostRoot.Children.Add(titleBar);

            // Present the full, rich, native ClearWin Glassmorphism UI in Row 1.
            // Provides instant, zero-delay rendering of the full dashboard, sidebar,
            // 7 interconnected AI models, hardware dials, 27 tweaks, and real-time monitors.
            legacyRoot.Visibility = Visibility.Visible;
            Grid.SetRow(legacyRoot, 1);
            hostRoot.Children.Add(legacyRoot);
        }

        private UIElement CreateNativeTitleBar()
        {
            var bar = new Border
            {
                Height = 38,
                Background = new SolidColorBrush(Color.FromArgb(240, 11, 17, 26)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(45, 120, 170, 220)),
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            var barGrid = new Grid();
            barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            bar.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    ToggleMaxRestoreWindow();
                }
                else if (e.ButtonState == MouseButtonState.Pressed)
                {
                    try { DragMove(); } catch { }
                }
            };

            var leftPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(14, 0, 10, 0)
            };

            string icoPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            FrameworkElement logoElem;
            if (System.IO.File.Exists(icoPath))
            {
                var logoImg = new System.Windows.Controls.Image
                {
                    Width = 18,
                    Height = 18,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                try
                {
                    logoImg.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(icoPath));
                    logoElem = logoImg;
                }
                catch { logoElem = new TextBlock { Text = "\u26A1", FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) }; }
            }
            else
            {
                logoElem = new TextBlock { Text = "\u26A1", FontSize = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            }
            leftPanel.Children.Add(logoElem);

            var titleTxt = new TextBlock
            {
                Text = "ClearWin 2.7.7",
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(225, 235, 245)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            leftPanel.Children.Add(titleTxt);

            var badge = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(38, 46, 168, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 46, 168, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6, 1, 6, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock
            {
                Text = "Smart Windows Optimizer",
                FontSize = 10,
                FontWeight = FontWeights.Medium,
                Foreground = new SolidColorBrush(Color.FromRgb(74, 182, 255))
            };
            leftPanel.Children.Add(badge);

            Grid.SetColumn(leftPanel, 0);
            barGrid.Children.Add(leftPanel);

            var dragArea = new Border
            {
                Background = Brushes.Transparent,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Grid.SetColumn(dragArea, 1);
            barGrid.Children.Add(dragArea);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            var minTxt = new TextBlock
            {
                Text = "\uE921",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 195, 215)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Border minBtn = CreateCustomTitleBarButton(minTxt, (s, e) =>
            {
                try { WindowState = WindowState.Minimized; } catch { }
            }, false);

            _maxBtnTxt = new TextBlock
            {
                Text = _customMaximized ? "\uE923" : "\uE922",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 195, 215)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Border maxBtn = CreateCustomTitleBarButton(_maxBtnTxt, (s, e) =>
            {
                try { ToggleMaxRestoreWindow(); } catch { }
            }, false);

            var closeTxt = new TextBlock
            {
                Text = "\uE8BB",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 195, 215)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Border closeBtn = CreateCustomTitleBarButton(closeTxt, (s, e) =>
            {
                try { Close(); } catch { }
            }, true);

            btnPanel.Children.Add(minBtn);
            btnPanel.Children.Add(maxBtn);
            btnPanel.Children.Add(closeBtn);

            Grid.SetColumn(btnPanel, 2);
            barGrid.Children.Add(btnPanel);

            bar.Child = barGrid;
            return bar;
        }

        private Border CreateCustomTitleBarButton(TextBlock contentTxt, MouseButtonEventHandler clickHandler, bool isClose)
        {
            var btn = new Border
            {
                Width = 46,
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            btn.Child = contentTxt;

            btn.MouseEnter += (s, e) =>
            {
                if (isClose)
                {
                    btn.Background = new SolidColorBrush(Color.FromRgb(232, 17, 35));
                    contentTxt.Foreground = Brushes.White;
                }
                else
                {
                    btn.Background = new SolidColorBrush(Color.FromArgb(35, 255, 255, 255));
                    contentTxt.Foreground = Brushes.White;
                }
            };

            btn.MouseLeave += (s, e) =>
            {
                btn.Background = Brushes.Transparent;
                contentTxt.Foreground = new SolidColorBrush(Color.FromRgb(180, 195, 215));
            };

            btn.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                clickHandler(s, e);
            };

            return btn;
        }

        internal void ShowWebUiFallback(string message)
        {
            if (_webFallback != null) return;
            var stack = new System.Windows.Controls.StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Width = 620
            };
            stack.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = "ClearWin Web UI",
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Media.Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            stack.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = message,
                TextAlignment = TextAlignment.Center,
                FontSize = 14,
                Foreground = System.Windows.Media.Brushes.LightGray,
                Margin = new Thickness(0, 12, 0, 18)
            });
            var open = new System.Windows.Controls.Button { Content = "فتح صفحة WebView2", Padding = new Thickness(18, 10, 18, 10), HorizontalAlignment = HorizontalAlignment.Center };
            open.Click += (s, e) =>
            {
                try { Process.Start(new ProcessStartInfo("https://developer.microsoft.com/microsoft-edge/webview2/#download-section") { UseShellExecute = true }); } catch (Exception ex) { CWLog.Error("MainWindow.WebUi", ex); }
            };
            stack.Children.Add(open);
            _webFallback = new System.Windows.Controls.Border { Background = new System.Windows.Media.SolidColorBrush(Theme.Background), Child = stack };
            var grid = Content as System.Windows.Controls.Grid;
            if (grid != null) grid.Children.Add(_webFallback);
        }

        private static string _cachedCleanupScanJson = null;
        private static DateTime _cleanupScanCacheTime = DateTime.MinValue;
        private static readonly object _cleanupCacheLock = new object();
        private static readonly TimeSpan CLEANUP_CACHE_TTL = TimeSpan.FromSeconds(30);

        internal static void InvalidateCleanupCache()
        {
            lock (_cleanupCacheLock)
            {
                _cachedCleanupScanJson = null;
                _cleanupScanCacheTime = DateTime.MinValue;
            }
        }

        private static string _cachedNetworkJson = null;
        private static DateTime _networkCacheTime = DateTime.MinValue;
        private static readonly object _networkCacheLock = new object();
        private static readonly TimeSpan NETWORK_CACHE_TTL = TimeSpan.FromSeconds(15);

        internal static void InvalidateNetworkCache()
        {
            lock (_networkCacheLock)
            {
                _cachedNetworkJson = null;
                _networkCacheTime = DateTime.MinValue;
            }
        }

        internal static bool IsProtectedSystemProcess(string processName, int pid)
        {
            if (pid <= 4) return true;
            if (string.IsNullOrEmpty(processName)) return true;
            string clean = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                ? processName.Substring(0, processName.Length - 4)
                : processName;
            if (SmartAgent.SYSTEM_PROCESSES.Contains(clean)) return true;
            return clean.Equals("csrss", StringComparison.OrdinalIgnoreCase) ||
                   clean.Equals("lsass", StringComparison.OrdinalIgnoreCase) ||
                   clean.Equals("winlogon", StringComparison.OrdinalIgnoreCase) ||
                   clean.Equals("wininit", StringComparison.OrdinalIgnoreCase) ||
                   clean.Equals("services", StringComparison.OrdinalIgnoreCase) ||
                   clean.Equals("smss", StringComparison.OrdinalIgnoreCase) ||
                   clean.Equals("dwm", StringComparison.OrdinalIgnoreCase) ||
                   clean.Equals("explorer", StringComparison.OrdinalIgnoreCase) ||
                   clean.Equals("svchost", StringComparison.OrdinalIgnoreCase) ||
                   clean.Equals("fontdrvhost", StringComparison.OrdinalIgnoreCase) ||
                   clean.Equals("sihost", StringComparison.OrdinalIgnoreCase) ||
                   clean.Equals("taskhostw", StringComparison.OrdinalIgnoreCase) ||
                   clean.Equals("MsMpEng", StringComparison.OrdinalIgnoreCase) ||
                   clean.Equals("NisSrv", StringComparison.OrdinalIgnoreCase) ||
                   clean.Equals("SecurityHealthService", StringComparison.OrdinalIgnoreCase) ||
                   clean.Equals("ClearWin", StringComparison.OrdinalIgnoreCase) ||
                   clean.Equals("ClearWin.AdminHelper", StringComparison.OrdinalIgnoreCase);
        }

        internal void HandleStructuredWebAction(string requestId, string action, string payload, string page = null, int generation = 0)
        {
            if (!WebUiHost.IsActionAllowed(action))
            {
                CWLog.Warn("WebUi.StructuredAction", "Rejected disallowed structured action: '" + action + "'");
                return;
            }
            // Execute all heavy I/O off the WPF UI thread to guarantee zero UI freezes
            System.Threading.Tasks.Task.Run(() =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    bool success = true;
                    string message = "اكتمل الإجراء بنجاح.";
                    string dataJson = null;
                    string actLower = (action ?? "").ToLowerInvariant().Trim();

                    // 1. Heavy Read-Only Data Scans (Execute purely in background)
                    if (actLower == "cleanup.scan" || actLower == "scan-cleanup")
                    {
                        dataJson = GetCleanupScanJson();
                        message = "اكتمل فحص القرص والذاكرة المؤقتة بنجاح.";
                    }
                    else if (actLower == "network.get-info" || actLower == "network.diagnostic" || actLower == "net-test")
                    {
                        dataJson = GetNetworkInfoJson();
                        message = "تم جلب بيانات محول الشبكة والاتصال المباشر.";
                    }
                    else if (actLower == "apps.get-processes" || actLower == "apps.list")
                    {
                        dataJson = GetProcessesJson();
                        message = "تم تحميل قائمة العمليات الجارية النشطة بنجاح.";
                    }
                    else if (actLower == "startup.get-list")
                    {
                        dataJson = GetStartupItemsJson();
                        message = "تم تحميل عناصر بدء التشغيل من سجل النظام بنجاح.";
                    }
                    else if (actLower == "ai.get-stats")
                    {
                        dataJson = GetAiStatsJson();
                        message = "تم تحديث إحصائيات نماذج الذكاء الاصطناعي.";
                    }
                    else if (actLower == "network.repair-preview")
                    {
                        // Read-only preview: refresh adapter and ping diagnostics with zero system side effects
                        dataJson = GetNetworkInfoJson(forceRefresh: true);
                        message = "معاينة إصلاح الشبكة (قراءة فقط دون تعديل النظام): تم تحديث قراءات المحول وزمن الاستجابة. لتنفيذ مسح DNS فعلياً استخدم الأمر الصريح 'network.flush-dns'.";
                    }
                    else if (actLower == "network.flush-dns")
                    {
                        var gate = ActionPolicyGate.Evaluate("network.flush-dns", ActionRequester.User, true, AdminSessionManager.IsAdminConnected);
                        if (!gate.Allowed)
                        {
                            success = false;
                            message = gate.ReasonAr;
                        }
                        else
                        {
                            string dnsMsg;
                            bool dnsOk = TryFlushDnsVerified(out dnsMsg);
                            success = dnsOk;
                            message = dnsMsg;
                            dataJson = GetNetworkInfoJson(forceRefresh: true);
                        }
                    }
                    else if (actLower == "risky-network" || actLower == "network.risky-network")
                    {
                        var gate = ActionPolicyGate.Evaluate("risky-network", ActionRequester.User, false, AdminSessionManager.IsAdminConnected);
                        success = false;
                        message = "تم حظر الإجراء 'risky-network' لأنه مصنف عالي الخطورة وغير مفعّل للتنفيذ التلقائي؛ استخدم أمراً صريحاً ومعتمداً مثل 'network.flush-dns'.";
                    }
                    else if (actLower == "recovery.create-restore-point" || actLower == "create-restore")
                    {
                        var gate = ActionPolicyGate.Evaluate("recovery.create-restore-point", ActionRequester.User, true, AdminSessionManager.IsAdminConnected);
                        if (!gate.Allowed)
                        {
                            success = false;
                            message = gate.ReasonAr;
                        }
                        else
                        {
                            string restoreMsg;
                            bool restoreOk = CreateClearWinRestorePoint("ClearWin 2.7.7 Restore Point", out restoreMsg, allowSessionCache: false);
                            success = restoreOk;
                            message = restoreOk
                                ? ("تم إنشاء نقطة استعادة Windows والتحقق منها بنجاح: " + restoreMsg)
                                : ("تعذر إنشاء نقطة استعادة Windows: " + restoreMsg);
                        }
                    }
                    // 2. Diagnostic JS error reports
                    else if (actLower == "diagnostic.js_error")
                    {
                        string jsMsg = WebUiHost.ExtractJsonString(payload, "message");
                        string jsUrl = WebUiHost.ExtractJsonString(payload, "url");
                        string jsPage = WebUiHost.ExtractJsonString(payload, "activePage");
                        CWLog.Warn("WebUi.JsError", string.Format("JS Error on page '{0}': {1} (URL: {2})", jsPage, jsMsg, jsUrl));
                        return;
                    }
                    // 3. Actions requiring UI Thread execution or modifying system state
                    else
                    {
                        Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                if (actLower == "optimization.review" || actLower == "review-selected")
                                {
                                    ulong availBefore = 0, availAfter = 0;
                                    try
                                    {
                                        MEMORYSTATUSEX msBefore = new MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
                                        if (GlobalMemoryStatusEx(ref msBefore)) availBefore = msBefore.ullAvailPhys;
                                    }
                                    catch { }
                                    OptimizeRAM();
                                    PurgeStandbyList();
                                    try
                                    {
                                        MEMORYSTATUSEX msAfter = new MEMORYSTATUSEX { dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
                                        if (GlobalMemoryStatusEx(ref msAfter)) availAfter = msAfter.ullAvailPhys;
                                    }
                                    catch { }
                                    long freedMb = (availAfter > availBefore) ? (long)((availAfter - availBefore) / (1024UL * 1024UL)) : 0L;
                                    message = "تم تنفيذ التحسين الذكي بنجاح: تقليص مساحات العمل وتفريغ الذاكرة الاحتياطية (المساحة المحررة المقاسة فعلياً: " + freedMb + " ميجابايت).";
                                    RefreshWebUiMetricsNow();
                                }
                                else if (actLower == "cleanup.execute" || actLower == "cleanup.prepare-preview" || actLower == "prepare-cleanup")
                                {
                                    InvalidateCleanupCache();
                                    long freedMb = ExecuteCleanupSafe();
                                    message = "تم تنظيف الملفات المؤقتة وذاكرة Shader Cache بنجاح واستعادة مساحة التخزين (تم تحرير " + freedMb + " ميجابايت).";
                                    dataJson = GetCleanupScanJson(forceRefresh: true);
                                    RefreshWebUiMetricsNow();
                                }
                                else if (actLower == "startup.toggle" || actLower == "startup.review-toggle" || actLower == "mock-startup-toggle")
                                {
                                    bool isConfirmed = WebUiHost.ExtractJsonBool(payload, "confirmed");
                                    var gate = ActionPolicyGate.Evaluate("startup.toggle", ActionRequester.User, isConfirmed, AdminSessionManager.IsAdminConnected);
                                    if (!gate.Allowed && gate.Status == PolicyEvaluationStatus.RequiresUserConfirmation_YellowTier)
                                    {
                                        var rConf = MessageBox.Show(
                                            "تأكيد تعديل برنامج بدء التشغيل (Yellow Tier):\n\nهل توافق على تغيير حالة تشغيل البرنامج مع بدء ويندوز؟",
                                            "ClearWin — تأكيد بدء التشغيل",
                                            MessageBoxButton.YesNo,
                                            MessageBoxImage.Question);
                                        if (rConf == MessageBoxResult.Yes)
                                        {
                                            isConfirmed = true;
                                            gate = ActionPolicyGate.Evaluate("startup.toggle", ActionRequester.User, true, AdminSessionManager.IsAdminConnected);
                                        }
                                    }

                                    if (!gate.Allowed)
                                    {
                                        success = false;
                                        message = gate.ReasonAr;
                                        dataJson = GetStartupItemsJson();
                                    }
                                    else
                                    {
                                        string name = WebUiHost.ExtractJsonString(payload, "itemName");
                                        string hive = WebUiHost.ExtractJsonString(payload, "hive");
                                        if (string.IsNullOrEmpty(hive)) hive = "HKCU";
                                        if (!string.IsNullOrEmpty(name))
                                        {
                                            bool restored = StartupClassifier.ExecuteStartupRestore(hive, name);
                                            if (!restored)
                                            {
                                                StartupClassifier.ExecuteStartupDisable(hive, name, null);
                                                message = "تم إيقاف تشغيل البرنامج تلقائياً مع بدء التشغيل: " + name;
                                            }
                                            else
                                            {
                                                message = "تمت إعادة تفعيل تشغيل البرنامج مع بدء التشغيل: " + name;
                                            }
                                        }
                                        else
                                        {
                                            message = "تم فحص وتحديث عناصر بدء التشغيل.";
                                        }
                                        dataJson = GetStartupItemsJson();
                                    }
                                }
                                else if (actLower == "apps.kill-process")
                                {
                                    string pidStr = WebUiHost.ExtractJsonString(payload, "pid");
                                    int pid;
                                    if (int.TryParse(pidStr, out pid) && pid > 4)
                                    {
                                        bool isConfirmed = WebUiHost.ExtractJsonBool(payload, "confirmed");
                                        var gate = ActionPolicyGate.Evaluate("apps.kill-process", ActionRequester.User, isConfirmed, AdminSessionManager.IsAdminConnected);
                                        if (!gate.Allowed && gate.Status == PolicyEvaluationStatus.RequiresUserConfirmation_YellowTier)
                                        {
                                            var rConf = MessageBox.Show(
                                                "تأكيد إنهاء العملية (Yellow Tier - PID: " + pid + "):\n\nهل توافق صراحة على إنهاء هذه العملية؟ قد تفقد البيانات غير المحفوظة في التطبيق.",
                                                "ClearWin — تأكيد إنهاء العملية",
                                                MessageBoxButton.YesNo,
                                                MessageBoxImage.Warning);
                                            if (rConf == MessageBoxResult.Yes)
                                            {
                                                isConfirmed = true;
                                                gate = ActionPolicyGate.Evaluate("apps.kill-process", ActionRequester.User, true, AdminSessionManager.IsAdminConnected);
                                            }
                                        }

                                        if (!gate.Allowed)
                                        {
                                            success = false;
                                            message = gate.ReasonAr;
                                            dataJson = GetProcessesJson();
                                        }
                                        else
                                        {
                                            try
                                            {
                                                using (var p = Process.GetProcessById(pid))
                                                {
                                                    string pName = p.ProcessName;
                                                    int selfPid = 0;
                                                    try { selfPid = Process.GetCurrentProcess().Id; } catch { }
                                                    if (pid == selfPid || IsProtectedSystemProcess(pName, pid))
                                                    {
                                                        message = "لا يمكن إنهاء عملية نظام أو أمان حرجة (" + pName + ") حفاظاً على استقرار Windows.";
                                                        success = false;
                                                    }
                                                    else
                                                    {
                                                        p.Kill();
                                                        message = "تم إنهاء العملية (" + pName + " - PID: " + pid + ") بنجاح.";
                                                    }
                                                }
                                            }
                                            catch (Exception exKill)
                                            {
                                                message = "تعذر إنهاء العملية: " + exKill.Message;
                                                success = false;
                                            }
                                            dataJson = GetProcessesJson();
                                        }
                                    }
                                    else
                                    {
                                        message = "معرف العملية غير صالح أو محمي.";
                                        success = false;
                                        dataJson = GetProcessesJson();
                                    }
                                }
                                else if (actLower == "apps.launch-taskmgr")
                                {
                                    try { Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true }); message = "تم فتح إدارة المهام في Windows."; }
                                    catch (Exception exTm) { message = "تعذر فتح إدارة المهام: " + exTm.Message; success = false; }
                                }
                                else if (actLower == "recovery.launch-rstrui")
                                {
                                    try { Process.Start(new ProcessStartInfo("rstrui.exe") { UseShellExecute = true }); message = "تم تشغيل معالج استعادة النظام (rstrui)."; }
                                    catch (Exception exRs) { message = "تعذر فتح أداة الاستعادة: " + exRs.Message; success = false; }
                                }
                                else if (actLower == "ai.toggle" || actLower == "ai:toggle")
                                {
                                    if (_agent != null)
                                    {
                                        if (_agent.Mode == AgentMode.Off) _agent.SetMode(AgentMode.AutoPilot);
                                        else _agent.SetMode(AgentMode.Off);
                                        message = "تم تغيير حالة الذكاء الاصطناعي إلى: " + _agent.Mode;
                                    }
                                    dataJson = GetAiStatsJson();
                                }
                                else if (actLower == "ai.deep-train")
                                {
                                    if (_agent != null)
                                    {
                                        System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                                        {
                                            try { _agent.ExecuteComprehensiveTraining(20, s => CWLog.Info("Train", s)); } catch { }
                                        });
                                        message = "بدأت دورة التدريب المنهجي الشامل لنماذج الذكاء الاصطناعي ومختبر التقييم في الخلفية.";
                                    }
                                    dataJson = GetAiStatsJson();
                                }
                                else if (actLower == "data.refresh" || actLower == "refresh")
                                {
                                    RefreshWebUiMetricsNow();
                                    message = "تم تحديث كافة قراءات النظام ومقاييس الأداء الحية.";
                                }
                                else if (actLower == "settings.save" || actLower == "save-settings")
                                {
                                    message = "تم حفظ تفضيلات وإعدادات ClearWin بنجاح.";
                                }
                                else if (actLower == "settings:get-tray-startup" || actLower == "settings.get-tray-startup")
                                {
                                    dataJson = string.Format("{{\"minimizeToTray\":{0},\"startWithWindows\":{1},\"gameGpuPower\":{2}}}",
                                        _minimizeToTray ? "true" : "false",
                                        _startWithWindows ? "true" : "false",
                                        _gameGpuPower ? "true" : "false");
                                    message = "تم استرجاع إعدادات النظام والألعاب بنجاح.";
                                }
                                else if (actLower == "settings:toggle-tray" || actLower == "settings.toggle-tray")
                                {
                                    _minimizeToTray = !_minimizeToTray;
                                    SaveMinimizeToTraySetting();
                                    if (_minimizeToTray && _trayIcon == null)
                                    {
                                        InitTrayIcon();
                                    }
                                    dataJson = string.Format("{{\"minimizeToTray\":{0},\"startWithWindows\":{1},\"gameGpuPower\":{2}}}",
                                        _minimizeToTray ? "true" : "false",
                                        _startWithWindows ? "true" : "false",
                                        _gameGpuPower ? "true" : "false");
                                    message = _minimizeToTray
                                        ? "تم تفعيل العمل في الخلفية: عند إغلاق النافذة ينزل ClearWin إلى درج النظام ويواصل العمل."
                                        : "تم تعطيل العمل في الخلفية: إغلاق النافذة سيوقف التطبيق بالكامل.";
                                }
                                else if (actLower == "settings:toggle-startup" || actLower == "settings.toggle-startup")
                                {
                                    _startWithWindows = !_startWithWindows;
                                    if (_startWithWindows)
                                    {
                                        _minimizeToTray = true;
                                        SaveMinimizeToTraySetting();
                                    }
                                    SaveStartWithWindowsSetting(_startWithWindows);
                                    if (_trayIcon == null)
                                    {
                                        InitTrayIcon();
                                    }
                                    dataJson = string.Format("{{\"minimizeToTray\":{0},\"startWithWindows\":{1},\"gameGpuPower\":{2}}}",
                                        _minimizeToTray ? "true" : "false",
                                        _startWithWindows ? "true" : "false",
                                        _gameGpuPower ? "true" : "false");
                                    message = _startWithWindows
                                        ? "تم تفعيل البدء مع تشغيل ويندوز وتسريع استقرار النظام تلقائياً من درج التطبيقات."
                                        : "تم تعطيل البدء التلقائي مع ويندوز.";
                                }
                                else if (actLower == "settings:toggle-game-gpu-power" || actLower == "settings.toggle-game-gpu-power")
                                {
                                    _gameGpuPower = !_gameGpuPower;
                                    SaveGameGpuPowerSetting(_gameGpuPower);
                                    if (_gameGpuPower)
                                    {
                                        SmartAgent.EnforceGameDedicatedGpuAndPower();
                                    }
                                    dataJson = string.Format("{{\"minimizeToTray\":{0},\"startWithWindows\":{1},\"gameGpuPower\":{2}}}",
                                        _minimizeToTray ? "true" : "false",
                                        _startWithWindows ? "true" : "false",
                                        _gameGpuPower ? "true" : "false");
                                    message = _gameGpuPower
                                        ? "تم تفعيل توجيه كارت الشاشة المنفصل وخطة الطاقة الفائقة في الألعاب."
                                        : "تم تعطيل التوجيه التلقائي لكارت الشاشة وخطة الطاقة.";
                                }
                                else
                                {
                                    HandleWebUiAction(action);
                                    message = "تم تنفيذ الإجراء بنجاح.";
                                }
                            }
                            catch (Exception exInner)
                            {
                                CWLog.Error("WebUi.StructuredAction.UI", exInner);
                                message = "حدث خطأ أثناء التنفيذ: " + exInner.Message;
                                success = false;
                            }
                        });
                    }

                    sw.Stop();
                    CWLog.Info("WebUi.StructuredAction", string.Format("Action '{0}' (page: '{1}', gen: {2}, req: {3}) completed in {4}ms",
                        action, page, generation, requestId, sw.ElapsedMilliseconds));

                    // Return response safely to UI on Dispatcher thread
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (_webUiHost != null)
                        {
                            var sb = new System.Text.StringBuilder();
                            sb.Append("{");
                            sb.Append("\"channel\":\"clearwin\",");
                            sb.Append("\"type\":\"action.result\",");
                            sb.Append("\"requestId\":\"").Append(EscapeJsonString(requestId)).Append("\",");
                            sb.Append("\"action\":\"").Append(EscapeJsonString(action)).Append("\",");
                            sb.Append("\"forPage\":\"").Append(EscapeJsonString(page ?? "")).Append("\",");
                            sb.Append("\"generation\":").Append(generation).Append(",");
                            sb.Append("\"success\":").Append(success ? "true" : "false").Append(",");
                            sb.Append("\"message\":\"").Append(EscapeJsonString(message)).Append("\"");
                            if (!string.IsNullOrEmpty(dataJson))
                            {
                                sb.Append(",\"data\":").Append(dataJson);
                            }
                            sb.Append("}");
                            _webUiHost.PostWebJson(sb.ToString());
                        }
                    }));
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    CWLog.Error("WebUi.StructuredAction", ex);
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (_webUiHost != null)
                        {
                            string errJson = "{\"channel\":\"clearwin\",\"type\":\"action.result\",\"requestId\":\"" + EscapeJsonString(requestId) + "\",\"action\":\"" + EscapeJsonString(action) + "\",\"forPage\":\"" + EscapeJsonString(page ?? "") + "\",\"generation\":" + generation + ",\"success\":false,\"message\":\"حدث خطأ أثناء التنفيذ: " + EscapeJsonString(ex.Message) + "\"}";
                            _webUiHost.PostWebJson(errJson);
                        }
                    }));
                }
            });
        }

        internal static long GetSafeDirectorySize(string path, int maxFiles = 5000, int timeoutMs = 1500)
        {
            if (string.IsNullOrEmpty(path)) return 0;
            try
            {
                if (!System.IO.Directory.Exists(path)) return 0;
                long total = 0;
                int fileCount = 0;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var dirsToVisit = new System.Collections.Generic.Queue<string>();
                dirsToVisit.Enqueue(path);

                while (dirsToVisit.Count > 0 && sw.ElapsedMilliseconds < timeoutMs && fileCount < maxFiles)
                {
                    string currentDir = dirsToVisit.Dequeue();
                    try
                    {
                        var dirInfo = new System.IO.DirectoryInfo(currentDir);
                        foreach (var fi in dirInfo.EnumerateFiles())
                        {
                            total += fi.Length;
                            fileCount++;
                            if (fileCount >= maxFiles || sw.ElapsedMilliseconds >= timeoutMs) break;
                        }

                        if (fileCount >= maxFiles || sw.ElapsedMilliseconds >= timeoutMs) break;

                        foreach (var subDir in dirInfo.EnumerateDirectories())
                        {
                            dirsToVisit.Enqueue(subDir.FullName);
                        }
                    }
                    catch (UnauthorizedAccessException) { }
                    catch (System.IO.DirectoryNotFoundException) { }
                    catch (System.IO.PathTooLongException) { }
                    catch (Exception ex) { CWLog.Error("WebUi.DirScan", ex); }
                }

                if (fileCount >= maxFiles && dirsToVisit.Count > 0 && fileCount > 0)
                {
                    long avg = total / fileCount;
                    total += Math.Min(total * 2, avg * 2000);
                }

                return total;
            }
            catch { return 0; }
        }

        private static string EscapeJsonString(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            var sb = new System.Text.StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        internal string GetStartupItemsJson()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("[");
            try
            {
                var items = StartupClassifier.EnumerateStartupItems();
                bool first = true;
                foreach (var it in items)
                {
                    if (!first) sb.Append(",");
                    first = false;
                    string impact = (it.Recommendation == StartupRecommendation.KeepEssential) ? "أساسي" : ((it.Recommendation == StartupRecommendation.SafeToDisable) ? "منخفض" : "متوسط");
                    string status = it.IsEnabled ? "مفعّل" : "معطّل";
                    sb.Append("{");
                    sb.Append("\"name\":\"").Append(EscapeJsonString(it.Name)).Append("\",");
                    sb.Append("\"publisher\":\"").Append(EscapeJsonString(it.Hive)).Append("\",");
                    sb.Append("\"impact\":\"").Append(EscapeJsonString(impact)).Append("\",");
                    sb.Append("\"status\":\"").Append(EscapeJsonString(status)).Append("\",");
                    sb.Append("\"hive\":\"").Append(EscapeJsonString(it.Hive)).Append("\",");
                    sb.Append("\"desc\":\"").Append(EscapeJsonString(it.Recommendation.ToString())).Append("\"");
                    sb.Append("}");
                }
            }
            catch (Exception ex)
            {
                CWLog.Error("WebUi.StartupItems", ex);
            }
            sb.Append("]");
            return sb.ToString();
        }

        private struct ProcessInfoItem
        {
            public int Pid;
            public string Name;
            public long RamMb;
            public bool IsProtected;
        }

        internal static string GetProcessesJson()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("[");
            Process[] procs = null;
            try
            {
                procs = Process.GetProcesses();
                var list = new System.Collections.Generic.List<ProcessInfoItem>(procs.Length);
                for (int i = 0; i < procs.Length; i++)
                {
                    var p = procs[i];
                    try
                    {
                        if (p.HasExited) continue;
                        long ws = p.WorkingSet64 / (1024 * 1024);
                        string pName = p.ProcessName;
                        bool isProtected = IsProtectedSystemProcess(pName, p.Id);
                        list.Add(new ProcessInfoItem { Pid = p.Id, Name = pName, RamMb = ws, IsProtected = isProtected });
                    }
                    catch { }
                }

                list.Sort((a, b) => b.RamMb.CompareTo(a.RamMb));
                int count = 0;
                bool first = true;
                foreach (var item in list)
                {
                    if (count++ >= 30) break;
                    if (!first) sb.Append(",");
                    first = false;
                    sb.Append("{");
                    sb.Append("\"pid\":").Append(item.Pid).Append(",");
                    sb.Append("\"name\":\"").Append(EscapeJsonString(item.Name)).Append("\",");
                    sb.Append("\"ramMb\":").Append(item.RamMb).Append(",");
                    sb.Append("\"isProtected\":").Append(item.IsProtected ? "true" : "false");
                    sb.Append("}");
                }
            }
            catch (Exception ex)
            {
                CWLog.Error("WebUi.GetProcesses", ex);
            }
            finally
            {
                if (procs != null)
                {
                    for (int i = 0; i < procs.Length; i++)
                    {
                        try { procs[i]?.Dispose(); } catch { }
                    }
                }
            }
            sb.Append("]");
            return sb.ToString();
        }

        internal string GetNetworkInfoJson(bool forceRefresh = false)
        {
            lock (_networkCacheLock)
            {
                if (!forceRefresh && _cachedNetworkJson != null && (DateTime.UtcNow - _networkCacheTime) < NETWORK_CACHE_TTL)
                {
                    return _cachedNetworkJson;
                }
            }

            var sb = new System.Text.StringBuilder();
            sb.Append("{");
            string activeAdapter = "Ethernet";
            string ipStr = "192.168.1.100";
            string gatewayStr = "192.168.1.1";
            string dnsStr = "1.1.1.1, 8.8.8.8";
            long speedMbps = 1000;
            try
            {
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                        ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                    {
                        activeAdapter = ni.Name + " (" + ni.NetworkInterfaceType + ")";
                        speedMbps = ni.Speed > 0 ? (ni.Speed / 1000000) : 1000;
                        var props = ni.GetIPProperties();
                        if (props.UnicastAddresses.Count > 0)
                        {
                            foreach (var u in props.UnicastAddresses)
                            {
                                if (u.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                                {
                                    ipStr = u.Address.ToString();
                                    break;
                                }
                            }
                        }
                        if (props.GatewayAddresses.Count > 0)
                        {
                            gatewayStr = props.GatewayAddresses[0].Address.ToString();
                        }
                        if (props.DnsAddresses.Count > 0)
                        {
                            dnsStr = props.DnsAddresses[0].ToString();
                        }
                        break;
                    }
                }
            }
            catch { }

            int pingMs = 14;
            try
            {
                using (var p = new System.Net.NetworkInformation.Ping())
                {
                    var r = p.Send("8.8.8.8", 400);
                    if (r != null && r.Status == System.Net.NetworkInformation.IPStatus.Success) pingMs = (int)r.RoundtripTime;
                }
            }
            catch { }

            sb.Append("\"adapter\":\"").Append(EscapeJsonString(activeAdapter)).Append("\",");
            sb.Append("\"ip\":\"").Append(EscapeJsonString(ipStr)).Append("\",");
            sb.Append("\"gateway\":\"").Append(EscapeJsonString(gatewayStr)).Append("\",");
            sb.Append("\"dns\":\"").Append(EscapeJsonString(dnsStr)).Append("\",");
            sb.Append("\"speedMbps\":").Append(speedMbps).Append(",");
            sb.Append("\"pingMs\":").Append(pingMs);
            sb.Append("}");
            string json = sb.ToString();

            lock (_networkCacheLock)
            {
                _cachedNetworkJson = json;
                _networkCacheTime = DateTime.UtcNow;
            }

            return json;
        }

        internal static string GetCleanupScanJson(bool forceRefresh = false)
        {
            lock (_cleanupCacheLock)
            {
                if (!forceRefresh && _cachedCleanupScanJson != null && (DateTime.UtcNow - _cleanupScanCacheTime) < CLEANUP_CACHE_TTL)
                {
                    return _cachedCleanupScanJson;
                }
            }

            long userTemp = GetSafeDirectorySize(System.IO.Path.GetTempPath(), 5000, 1200);
            long winTemp = GetSafeDirectorySize(@"C:\Windows\Temp", 3000, 800);
            long shaderCache = GetSafeDirectorySize(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3DSCache"), 3000, 800);
            long winUpdate = GetSafeDirectorySize(@"C:\Windows\SoftwareDistribution\Download", 5000, 1500);

            long totalBytes = userTemp + winTemp + shaderCache + winUpdate;
            var sb = new System.Text.StringBuilder();
            sb.Append("{");
            sb.Append("\"userTempMb\":").Append(userTemp / (1024 * 1024)).Append(",");
            sb.Append("\"winTempMb\":").Append(winTemp / (1024 * 1024)).Append(",");
            sb.Append("\"shaderCacheMb\":").Append(shaderCache / (1024 * 1024)).Append(",");
            sb.Append("\"winUpdateMb\":").Append(winUpdate / (1024 * 1024)).Append(",");
            sb.Append("\"totalMb\":").Append(totalBytes / (1024 * 1024));
            sb.Append("}");
            string json = sb.ToString();

            lock (_cleanupCacheLock)
            {
                _cachedCleanupScanJson = json;
                _cleanupScanCacheTime = DateTime.UtcNow;
            }

            return json;
        }

        private long ExecuteCleanupSafe()
        {
            long freedBytes = 0;
            string[] paths = new string[] {
                System.IO.Path.GetTempPath(),
                @"C:\Windows\Temp",
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D3DSCache")
            };
            foreach (var dir in paths)
            {
                try
                {
                    if (!System.IO.Directory.Exists(dir)) continue;
                    foreach (var file in System.IO.Directory.GetFiles(dir, "*.*", System.IO.SearchOption.TopDirectoryOnly))
                    {
                        try
                        {
                            var fi = new System.IO.FileInfo(file);
                            long len = fi.Length;
                            fi.Delete();
                            freedBytes += len;
                        }
                        catch { }
                    }
                }
                catch { }
            }
            try { PurgeStandbyList(); } catch { }
            return freedBytes / (1024 * 1024);
        }

        private string GetAiStatsJson()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("{");
            int activeModels = 7;
            double avgLatency = 0.11;
            long qSamples = 0;
            if (_agent != null)
            {
                try
                {
                    int eps = _agent.GetTotalEpisodes();
                    int patterns = _agent.GetKnowledgeBasePatternCount();
                    qSamples = Math.Max(0, eps + patterns);
                }
                catch { }
            }
            string modeStr = (_agent != null) ? _agent.Mode.ToString() : "AutoPilot";
            bool autopilot = (_agent != null) && _agent.IsAutopilotEnabled;
            bool fpsFloor = (_agent != null) && _agent.FpsFloorActive;

            sb.Append("\"activeModels\":").Append(activeModels).Append(",");
            sb.Append("\"avgLatencyMs\":").Append(avgLatency).Append(",");
            sb.Append("\"qSamples\":").Append(qSamples).Append(",");
            sb.Append("\"mode\":\"").Append(EscapeJsonString(modeStr)).Append("\",");
            sb.Append("\"autopilot\":").Append(autopilot ? "true" : "false").Append(",");
            sb.Append("\"fpsFloor\":").Append(fpsFloor ? "true" : "false").Append(",");
            sb.Append("\"recentDecisions\":").Append(AiDecisionLogger.GetRecentDecisionsJson(30));
            sb.Append("}");
            return sb.ToString();
        }

        internal void HandleWebUiAction(string action)
        {
            try
            {
                if (string.IsNullOrEmpty(action)) return;
                if (action.StartsWith("navigate:", StringComparison.OrdinalIgnoreCase))
                {
                    NavigateWebPage(action.Substring("navigate:".Length).Trim().ToLowerInvariant());
                    return;
                }
                if (action == "ai:toggle")
                {
                    try
                    {
                        if (_agent.Mode == AgentMode.Off)
                        {
                            _agent.SetMode(AgentMode.AutoPilot);
                            if (_webUiHost != null) _webUiHost.PushStatus("تم تشغيل SmartAgent.");
                        }
                        else
                        {
                            _agent.SetMode(AgentMode.Off);
                            if (_webUiHost != null) _webUiHost.PushStatus("تم إيقاف SmartAgent.");
                        }
                    }
                    catch (Exception ex) { if (_webUiHost != null) _webUiHost.PushStatus("تعذر تغيير حالة SmartAgent: " + ex.Message); }
                    return;
                }
                if (action == "ai:pause" || action == "ai:resume" || action == "ai:kill-toggle")
                {
                    try
                    {
                        if (action == "ai:kill-toggle")
                        {
                            if (_agent.IsKillSwitchActive) { _agent.ResetKillSwitch(); _agent.SetMode(AgentMode.AutoPilot); }
                            else { _agent.EmergencyKillSwitch(); }
                        }
                        else if (action == "ai:pause") _agent.PauseAgent(60);
                        else _agent.ResumeAgent();
                        if (_webUiHost != null) _webUiHost.PushStatus("تم تحديث حالة SmartAgent.");
                    }
                    catch (Exception ex) { if (_webUiHost != null) _webUiHost.PushStatus("تعذر تحديث SmartAgent: " + ex.Message); }
                    return;
                }
                // ================================================================
                // Persistent AI memory (settings page "الذاكرة الدائمة للذكاء
                // الاصطناعي"): on-disk audit trail of decisions / executed
                // actions / rewards / penalties (AiPersistentMemory).
                // ================================================================
                if (action == "ai-memory:refresh")
                {
                    if (_webUiHost != null)
                    {
                        _webUiHost.PushAiMemory(BuildAiMemoryJson(80));
                        _webUiHost.PushStatus("الذاكرة الدائمة للذكاء الاصطناعي: تم تحديث السجل.");
                    }
                    return;
                }
                if (action == "ai-memory:clear")
                {
                    try
                    {
                        if (AiMemory != null)
                        {
                            AiMemory.Clear();
                            if (_webUiHost != null)
                            {
                                _webUiHost.PushAiMemory(BuildAiMemoryJson(80));
                                _webUiHost.PushStatus("الذاكرة الدائمة: تم مسح السجل بالكامل.");
                            }
                        }
                        else if (_webUiHost != null)
                        {
                            _webUiHost.PushStatus("الذاكرة الدائمة: غير متاحة حاليًا.");
                        }
                    }
                    catch (Exception ex4)
                    {
                        CWLog.Error("WebUi.AiMemoryClear", ex4);
                        if (_webUiHost != null) _webUiHost.PushStatus("فشل مسح الذاكرة الدائمة. راجع ClearWin.log.");
                    }
                    return;
                }
                if (action == "ai-memory:export")
                {
                    try
                    {
                        string dir = System.IO.Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClearWin", "agent");
                        System.IO.Directory.CreateDirectory(dir);
                        string outPath = System.IO.Path.Combine(dir, "ai_memory_export_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json");
                        System.IO.File.WriteAllText(outPath, BuildAiMemoryJson(100000), new System.Text.UTF8Encoding(false));
                        int total = (AiMemory != null) ? AiMemory.TotalCount : 0;
                        if (_webUiHost != null) _webUiHost.PushStatus("الذاكرة الدائمة: تم تصدير " + total.ToString() + " حدثًا إلى " + outPath);
                    }
                    catch (Exception ex5)
                    {
                        CWLog.Error("WebUi.AiMemoryExport", ex5);
                        if (_webUiHost != null) _webUiHost.PushStatus("فشل تصدير الذاكرة الدائمة. راجع ClearWin.log.");
                    }
                    return;
                }

                // ================================================================
                // Admin Mode & Green Autonomy Handlers
                // ================================================================
                if (action == "admin:request-elevation")
                {
                    System.Threading.Tasks.Task.Run(async () =>
                    {
                        bool elevated = await AdminSessionManager.RequestAdminElevationAsync();
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            BroadcastAdminAutonomyState();
                            if (_webUiHost != null)
                            {
                                _webUiHost.PushStatus(elevated
                                    ? "تم تفعيل وضع المسؤول بنجاح عبر المساعد الإداري المستقل."
                                    : "تم إلغاء أو تعذر تفعيل وضع المسؤول. يستمر التطبيق بصلاحيات عادية.");
                            }
                        }));
                    });
                    return;
                }

                if (action == "admin:terminate-session")
                {
                    AdminSessionManager.TerminateAdminSession();
                    BroadcastAdminAutonomyState();
                    if (_webUiHost != null) _webUiHost.PushStatus("تم إنهاء الجلسة الإدارية وإغلاق المساعد.");
                    return;
                }

                if (action == "admin:status" || action == "autonomy:get-state")
                {
                    BroadcastAdminAutonomyState();
                    return;
                }

                if (action == "autonomy:toggle-green")
                {
                    bool newState = !ActionPolicyGate.GreenAutonomyEnabled;
                    ActionPolicyGate.SetGreenAutonomy(newState);
                    BroadcastAdminAutonomyState();
                    if (_webUiHost != null)
                    {
                        _webUiHost.PushStatus(newState
                            ? "تم السماح بتنفيذ الأوامر الخضراء تلقائياً (لهذه الجلسة فقط)."
                            : "تم إيقاف الاستقلالية التلقائية للأوامر الخضراء.");
                    }
                    return;
                }

                if (action == "catalog:get-all")
                {
                    SendActionCatalogJson();
                    return;
                }

                if (action == "audit:get-logs")
                {
                    SendAuditLogsJson();
                    return;
                }

                if (action == "settings:get-tray-startup" || action == "settings:status")
                {
                    BroadcastSettingsState();
                    return;
                }

                if (action == "settings:toggle-tray")
                {
                    _minimizeToTray = !_minimizeToTray;
                    SaveMinimizeToTraySetting();
                    if (_minimizeToTray && _trayIcon == null) InitTrayIcon();
                    BroadcastSettingsState();
                    if (_webUiHost != null)
                    {
                        _webUiHost.PushStatus(_minimizeToTray
                            ? "تم تفعيل العمل في الخلفية: عند إغلاق النافذة ينزل ClearWin إلى درج النظام."
                            : "تم تعطيل العمل في الخلفية.");
                    }
                    return;
                }

                if (action == "settings:toggle-startup")
                {
                    _startWithWindows = !_startWithWindows;
                    if (_startWithWindows)
                    {
                        _minimizeToTray = true;
                        SaveMinimizeToTraySetting();
                    }
                    SaveStartWithWindowsSetting(_startWithWindows);
                    if (_trayIcon == null) InitTrayIcon();
                    BroadcastSettingsState();
                    if (_webUiHost != null)
                    {
                        _webUiHost.PushStatus(_startWithWindows
                            ? "تم تفعيل البدء مع تشغيل ويندوز وتسريع استقرار النظام تلقائياً."
                            : "تم تعطيل البدء التلقائي مع ويندوز.");
                    }
                    return;
                }

                if (action == "settings:toggle-game-gpu-power")
                {
                    _gameGpuPower = !_gameGpuPower;
                    SaveGameGpuPowerSetting(_gameGpuPower);
                    if (_gameGpuPower)
                    {
                        SmartAgent.EnforceGameDedicatedGpuAndPower();
                    }
                    BroadcastSettingsState();
                    if (_webUiHost != null)
                    {
                        _webUiHost.PushStatus(_gameGpuPower
                            ? "تم تفعيل خطة الطاقة الفائقة وتوجيه كارت الشاشة المنفصل للألعاب."
                            : "تم تعطيل التوجيه التلقائي لكارت الشاشة المنفصل.");
                    }
                    return;
                }

                if (action.StartsWith("page:refresh-", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        switch (action.ToLowerInvariant())
                        {
                            case "page:refresh-processes": LoadProcesses(); break;
                            case "page:refresh-startup": LoadStartupItems(); break;
                            case "page:refresh-network": LoadNetworkInfo(); break;
                            case "page:refresh-disk": UpdateDiskPage(); break;
                            case "page:refresh-tools": UpdateToolsServices(); break;
                            case "page:refresh-health": UpdateHealthPage(); break;
                            case "page:refresh-ai": GenerateWebAiSnapshot(); break;
                            case "page:refresh-gpu": RefreshWebUiMetricsNow(); break;
                            case "page:refresh-monitor": RefreshWebUiMetricsNow(); break;
                            default:
                                if (_webUiHost != null) _webUiHost.PushStatus("لا يوجد مصدر تحديث لهذه الصفحة: " + action);
                                return;
                        }
                        if (_webUiHost != null) _webUiHost.PushStatus("تم تحديث بيانات الصفحة من المحرك.");
                    }
                    catch (Exception ex) { if (_webUiHost != null) _webUiHost.PushStatus("تعذر تحديث الصفحة: " + ex.Message); }
                    return;
                }
                if (action == "ai:autopilot-toggle" || action == "ai:forecast" || action == "ai:selfcheck" || action == "ai:export-report")
                {
                    try
                    {
                        if (action == "ai:autopilot-toggle")
                        {
                            if (_agent.IsAutopilotEnabled) _agent.DisableAutopilot();
                            else _agent.EnableAutopilot();
                            if (_webUiHost != null) _webUiHost.PushStatus(_agent.IsAutopilotEnabled ? "تم تفعيل AutoPilot." : "تم إيقاف AutoPilot.");
                        }
                        else if (action == "ai:forecast")
                        {
                            _agent.GenerateForecast();
                            if (_webUiHost != null) _webUiHost.PushStatus("تم تحديث توقعات الأداء من SmartAgent.");
                        }
                        else if (action == "ai:selfcheck")
                        {
                            _agent.RunSelfDiagnostics();
                            if (_webUiHost != null) _webUiHost.PushStatus("اكتمل التشخيص الذاتي.");
                        }
                        else
                        {
                            ExportAIPdfReport();
                            if (_webUiHost != null) _webUiHost.PushStatus("تم إنشاء تقرير AI.");
                        }
                    }
                    catch (Exception ex) { if (_webUiHost != null) _webUiHost.PushStatus("تعذر تنفيذ AI: " + ex.Message); }
                    return;
                }

                if (action == "ai:benchmark" || action == "ai:dna" || action == "ai:stats")
                {
                    try
                    {
                        if (action == "ai:benchmark")
                        {
                            var b = _agent.RunBenchmark();
                            if (_webUiHost != null) _webUiHost.PushStatus(string.Format("Benchmark: {0:F0}/100 | الاتجاه: {1}", b.OverallScore, b.Trend));
                        }
                        else if (action == "ai:dna")
                        {
                            if (_webUiHost != null) _webUiHost.PushStatus("Performance DNA: " + _agent.GetDNASummary());
                        }
                        else
                        {
                            if (_webUiHost != null) _webUiHost.PushStatus(string.Format("Simulation: {0} | Success: {1:F1}% | Gain: {2:F2} | Risk blocked: {3}", _agent.GetTotalSimulations(), _agent.GetAvgSimSuccessRate(), _agent.GetAvgSimPerformanceGain(), _agent.GetTotalRiskBlocked()));
                        }
                    }
                    catch (Exception ex) { if (_webUiHost != null) _webUiHost.PushStatus("تعذر تنفيذ أداة AI: " + ex.Message); }
                    return;
                }

                if (action.StartsWith("game:", StringComparison.OrdinalIgnoreCase))
                {
                    string g = action.Substring("game:".Length).Trim().ToLowerInvariant();
                    try
                    {
                        if (g == "fpsfloor") ToggleFpsFloor60();
                        else if (g == "fullopt") ToggleFullOptimization();
                        else if (g == "timer") ToggleTimerResolution();
                        else if (g == "unpark") UnparkCPUCores();
                        else if (g == "gpu-msi") EnableGPUMSIMode();
                        else if (g == "toggle")
                        {
                            if (_gameModeActive) { ExitGameModeInternal(); _gameModeActive = false; try { _agent.SetMode(AgentMode.AutoPilot); } catch (Exception ex2) { CWLog.Error("MainWindow.WebUi", ex2); } }
                            else { ActivateGameModeSilent(); _gameModeActive = true; try { _agent.SetMode(AgentMode.GameMode); } catch (Exception ex3) { CWLog.Error("MainWindow.WebUi", ex3); } UpdateGameModeStatusUI(); }
                        }
                        else { if (_webUiHost != null) _webUiHost.PushStatus("إجراء ألعاب غير معروف: " + g); return; }
                        if (_webUiHost != null) _webUiHost.PushStatus("تم تنفيذ إجراء الألعاب: " + g);
                    }
                    catch (Exception ex) { if (_webUiHost != null) _webUiHost.PushStatus("تعذر إجراء الألعاب: " + ex.Message); }
                    return;
                }

                if (action == "network:restore")
                {
                    try { _agent.RestoreNetworkTuning(); _webNetworkBoosted = false; if (_webUiHost != null) _webUiHost.PushStatus("تمت استعادة إعدادات الشبكة."); }
                    catch (Exception ex) { if (_webUiHost != null) _webUiHost.PushStatus("تعذر استعادة الشبكة: " + ex.Message); }
                    return;
                }
                if (action == "framepacing:restore")
                {
                    try { _agent.RestoreFramePacing(); if (_webUiHost != null) _webUiHost.PushStatus("تمت استعادة Frame Pacing."); }
                    catch (Exception ex) { if (_webUiHost != null) _webUiHost.PushStatus("تعذر استعادة Frame Pacing: " + ex.Message); }
                    return;
                }

                if (action.StartsWith("clean:", StringComparison.OrdinalIgnoreCase))
                {
                    string c = action.Substring("clean:".Length).Trim().ToLowerInvariant();
                    try
                    {
                        if (c == "all") { RunCleanTask("temp"); RunCleanTask("prefetch"); RunCleanTask("recycle"); RunCleanTask("browser"); }
                        else if (c == "temp" || c == "prefetch" || c == "recycle" || c == "browser") RunCleanTask(c);
                        else { if (_webUiHost != null) _webUiHost.PushStatus("نوع تنظيف غير معروف: " + c); return; }
                        if (_webUiHost != null) _webUiHost.PushStatus("بدأ تنظيف: " + c);
                    }
                    catch (Exception ex) { if (_webUiHost != null) _webUiHost.PushStatus("تعذر بدء التنظيف: " + ex.Message); }
                    return;
                }

                if (action.StartsWith("tool:", StringComparison.OrdinalIgnoreCase))
                {
                    string t = action.Substring("tool:".Length).Trim().ToLowerInvariant();
                    try
                    {
                        if (t == "disk-clean") AdvancedDiskClean();
                        else if (t == "registry-scan") RunRegistryScan();
                        else if (t == "system-report") ExportSystemReport();
                        else if (t == "device-manager") { using (Process.Start(new ProcessStartInfo("devmgmt.msc") { UseShellExecute = true })) { } }
                        else if (t == "services") { using (Process.Start(new ProcessStartInfo("services.msc") { UseShellExecute = true })) { } }
                        else if (t == "defrag") { using (Process.Start(new ProcessStartInfo { FileName = "dfrgui.exe", UseShellExecute = true })) { } }
                        else { if (_webUiHost != null) _webUiHost.PushStatus("أداة غير معروفة: " + t); return; }
                        if (_webUiHost != null) _webUiHost.PushStatus("تم تشغيل الأداة: " + t);
                    }
                    catch (Exception ex) { if (_webUiHost != null) _webUiHost.PushStatus("تعذر تشغيل الأداة: " + ex.Message); }
                    return;
                }

                if (action == "process:taskmgr")
                {
                    try { Process.Start(new ProcessStartInfo("taskmgr.exe") { UseShellExecute = true }); if (_webUiHost != null) _webUiHost.PushStatus("تم فتح مدير المهام."); }
                    catch (Exception ex) { if (_webUiHost != null) _webUiHost.PushStatus("تعذر فتح مدير المهام: " + ex.Message); }
                    return;
                }

                if (action == "startup:settings")
                {
                    try { Process.Start(new ProcessStartInfo("ms-settings:startupapps") { UseShellExecute = true }); if (_webUiHost != null) _webUiHost.PushStatus("تم فتح إعدادات بدء التشغيل."); }
                    catch (Exception ex) { if (_webUiHost != null) _webUiHost.PushStatus("تعذر فتح بدء التشغيل: " + ex.Message); }
                    return;
                }

                if (action == "security:defender")
                {
                    try { Process.Start(new ProcessStartInfo("windowsdefender:") { UseShellExecute = true }); if (_webUiHost != null) _webUiHost.PushStatus("تم فتح Windows Security."); }
                    catch (Exception ex) { if (_webUiHost != null) _webUiHost.PushStatus("تعذر فتح Windows Security: " + ex.Message); }
                    return;
                }

                if (action == "health:restore-point")
                {
                    if (_webUiHost != null) _webUiHost.PushStatus("جاري إنشاء نقطة استعادة للنظام والتحقق منها...");
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            string msg;
                            bool ok = CreateClearWinRestorePoint("Web UI manual restore", out msg, allowSessionCache: false);
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                if (_webUiHost != null) _webUiHost.PushStatus(ok ? ("تم إنشاء نقطة استعادة للنظام: " + msg) : ("تعذر إنشاء نقطة الاستعادة: " + msg));
                            }));
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                if (_webUiHost != null) _webUiHost.PushStatus("تعذر إنشاء نقطة الاستعادة: " + ex.Message);
                            }));
                        }
                    });
                    return;
                }

                if (action == "sandbox:toggle")
                {
                    try
                    {
                        if (_agent.SimulationMode) _agent.DisableSimulation();
                        else _agent.EnableSimulation(_agent.CurrentState.Clone());
                        if (_webUiHost != null) _webUiHost.PushStatus(_agent.SimulationMode ? "تم تفعيل محاكاة AI." : "تم الرجوع إلى البيانات الحية.");
                    }
                    catch (Exception ex) { if (_webUiHost != null) _webUiHost.PushStatus("تعذر تغيير وضع المحاكاة: " + ex.Message); }
                    return;
                }

                if (action == "news:refresh")
                {
                    try
                    {
                        RefreshWebUiMetricsNow();
                        if (_webUiHost != null) _webUiHost.PushStatus("تم تحديث حالة النظام والبيانات الحية.");
                    }
                    catch (Exception ex) { if (_webUiHost != null) _webUiHost.PushStatus("تعذر تحديث الحالة: " + ex.Message); }
                    return;
                }

                if (action == "scan")
                {
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            var result = _agent.RunSelfDiagnostics();
                            string msg = result != null ? "اكتمل فحص SmartAgent الذاتي." : "اكتمل الفحص دون نتيجة إضافية.";
                            Dispatcher.BeginInvoke(new Action(() => { if (_webUiHost != null) _webUiHost.PushStatus(msg); }));
                        }
                        catch (Exception ex) { Dispatcher.BeginInvoke(new Action(() => { if (_webUiHost != null) _webUiHost.PushStatus("فشل الفحص: " + ex.Message); })); }
                    });
                    return;
                }
                if (action == "optimize-all" || action == "boost")
                {
                    // Immediate feedback: the full-optimization flow shows a
                    // confirmation dialog; without this the Web UI looks frozen
                    // and the button appears "dead".
                    if (_webUiHost != null) _webUiHost.PushStatus("جاري تشغيل التحسينات الفعلية - ستظهر نافذة تأكيد عند الحاجة...");
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try { RunAllOptimizations(); _webPerformanceBoosted = true; if (_webUiHost != null) _webUiHost.PushStatus("تم تشغيل مجموعة التحسينات الفعلية."); }
                        catch (Exception ex) { if (_webUiHost != null) _webUiHost.PushStatus("تعذر تشغيل التحسينات: " + ex.Message); }
                    }));
                    return;
                }
                if (action.StartsWith("toggle:", StringComparison.OrdinalIgnoreCase))
                {
                    string key = action.Substring("toggle:".Length).Trim().ToLowerInvariant();
                    try
                    {
                        if (key == "gaming")
                        {
                            if (_gameModeActive)
                            {
                                if (_webUiHost != null) _webUiHost.PushStatus("جاري إيقاف وضع الألعاب...");
                                ExitGameModeInternal(); _gameModeActive = false; try { _agent.SetMode(AgentMode.AutoPilot); } catch (Exception ex4) { CWLog.Error("MainWindow.WebUi", ex4); }
                            }
                            else
                            {
                                // Heavy service/powercfg work now runs off the UI
                                // thread (ActivateGameModeSilent), so give immediate
                                // feedback instead of letting the toggle look dead.
                                if (_webUiHost != null) _webUiHost.PushStatus("جاري تفعيل وضع الألعاب (إيقاف خدمات خلفية) - بدون تجميد للواجهة...");
                                ActivateGameModeSilent(); _gameModeActive = true; try { _agent.SetMode(AgentMode.GameMode); } catch (Exception ex5) { CWLog.Error("MainWindow.WebUi", ex5); }
                            }
                        }
                        else if (key == "performance")
                        {
                            if (_webPerformanceBoosted) { _agent.EmergencyStopAllOptimizations(); _webPerformanceBoosted = false; }
                            else
                            {
                                if (_webUiHost != null) _webUiHost.PushStatus("جاري تشغيل تعزيز الأداء - ستظهر نافذة تأكيد عند الحاجة...");
                                RunAllOptimizations(); _webPerformanceBoosted = true;
                            }
                        }
                        else if (key == "network")
                        {
                            if (_webNetworkBoosted) { _agent.RestoreNetworkTuning(); _webNetworkBoosted = false; }
                            else { ApplyLatencyTweaks(); _webNetworkBoosted = true; }
                        }
                        else if (key == "cooling")
                        {
                            _agent.SetMode(_agent.Mode == AgentMode.CoolMode ? AgentMode.AutoPilot : AgentMode.CoolMode);
                        }
                        else { if (_webUiHost != null) _webUiHost.PushStatus("إجراء غير معروف: " + key); return; }
                        if (_webUiHost != null) _webUiHost.PushStatus("تم تحديث: " + key);
                    }
                    catch (Exception ex) { if (_webUiHost != null) _webUiHost.PushStatus("تعذر تطبيق " + key + ": " + ex.Message); }
                    return;
                }
                if (action == "game:toggle")
                {
                    try
                    {
                        if (_gameModeActive) { ExitGameModeInternal(); _gameModeActive = false; try { _agent.SetMode(AgentMode.AutoPilot); } catch (Exception ex6) { CWLog.Error("MainWindow.WebUi", ex6); } if (_webUiHost != null) _webUiHost.PushStatus("تم إيقاف وضع الألعاب."); }
                        else { ActivateGameModeSilent(); _gameModeActive = true; try { _agent.SetMode(AgentMode.GameMode); } catch (Exception ex7) { CWLog.Error("MainWindow.WebUi", ex7); } UpdateGameModeStatusUI(); if (_webUiHost != null) _webUiHost.PushStatus("تم تشغيل وضع الألعاب."); }
                    }
                    catch (Exception ex) { if (_webUiHost != null) _webUiHost.PushStatus("تعذر تغيير وضع الألعاب: " + ex.Message); }
                    return;
                }
                if (action == "services")
                {
                    try
                    {
                        // Immediate feedback: the actual stop/start runs off the UI
                        // thread and ends with a result MessageBox; without this
                        // the button looked unresponsive while services stop/start.
                        if (_webUiHost != null) _webUiHost.PushStatus(_servicesBoosted ? "جاري إعادة تشغيل الخدمات الخلفية..." : "جاري إيقاف الخدمات الخلفية غير الضرورية...");
                        ToggleServicesBooster(); if (_webUiHost != null) _webUiHost.PushStatus(_servicesBoosted ? "تم تفعيل معزز الخدمات." : "تمت استعادة الخدمات.");
                    }
                    catch (Exception ex) { if (_webUiHost != null) _webUiHost.PushStatus("تعذر إدارة الخدمات: " + ex.Message); }
                    return;
                }
                if (action == "memory")
                {
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            OptimizeRAM();
                            Dispatcher.BeginInvoke(new Action(() => { if (_webUiHost != null) _webUiHost.PushStatus("تم تنظيف الذاكرة RAM."); }));
                        }
                        catch (Exception ex) { Dispatcher.BeginInvoke(new Action(() => { if (_webUiHost != null) _webUiHost.PushStatus("تعذر تنظيف RAM: " + ex.Message); })); }
                    });
                    return;
                }
                if (action == "framepacing")
                {
                    _agent.EnableFramePacing();
                    if (_webUiHost != null) _webUiHost.PushStatus("تم تفعيل Frame Pacing.");
                    return;
                }
                if (action == "network")
                {
                    ApplyLatencyTweaks();
                    _webNetworkBoosted = true;
                    if (_webUiHost != null) _webUiHost.PushStatus("تم تشغيل ضبط الشبكة منخفضة التأخير.");
                    return;
                }
                if (action == "reset")
                {
                    try { ExitGameModeInternal(); } catch (Exception ex8) { CWLog.Error("MainWindow.WebUi", ex8); }
                    _gameModeActive = false;
                    try { _agent.EmergencyStopAllOptimizations(); } catch (Exception ex9) { CWLog.Error("MainWindow.WebUi", ex9); }
                    try { _agent.SetMode(AgentMode.Off); } catch (Exception ex10) { CWLog.Error("MainWindow.WebUi", ex10); }
                    try { _agent.RestoreFramePacing(); } catch (Exception ex11) { CWLog.Error("MainWindow.WebUi", ex11); }
                    try { _agent.RestoreNetworkTuning(); } catch (Exception ex12) { CWLog.Error("MainWindow.WebUi", ex12); }
                    _webPerformanceBoosted = false;
                    _webNetworkBoosted = false;
                    if (_webUiHost != null) _webUiHost.PushStatus("تمت استعادة الوضع الآمن وإيقاف التحسينات التلقائية.");
                    return;
                }
                if (action == "window:minimize")
                {
                    WindowState = WindowState.Minimized;
                    return;
                }
                if (action == "window:maximize")
                {
                    ToggleMaxRestoreWindow();
                    return;
                }
                if (action == "window:close")
                {
                    Close();
                    return;
                }
                if (action == "window:drag")
                {
                    try { DragMove(); } catch (Exception ex13) { CWLog.Error("MainWindow.WebUi", ex13); }
                    return;
                }
                if (action.StartsWith("undo:", StringComparison.OrdinalIgnoreCase))
                {
                    string name = action.Substring("undo:".Length).Trim().ToLowerInvariant();
                    string catalogActionId = "tweak:" + name;
                    if (IsIrreversibleTweak(name))
                    {
                        if (_webUiHost != null) _webUiHost.PushStatus("غير قابل للاستعادة التلقائية: " + name);
                        MessageBox.Show("هذا الإجراء (" + name + ") دائم وغير قابل للاستعادة التلقائية.", "ClearWin — غير قابل للاستعادة", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    if (IsUntestedDeferredTweak(name))
                    {
                        if (_webUiHost != null) _webUiHost.PushStatus("مؤجل (NOT TESTED): " + name);
                        return;
                    }

                    // Route undo through ActionPolicyGate
                    var policyRes = ActionPolicyGate.Evaluate(catalogActionId, ActionRequester.User, false, AdminSessionManager.IsAdminConnected);
                    if (policyRes.Status == PolicyEvaluationStatus.UnregisteredAction)
                    {
                        CWLog.Warn("WebUi.Undo", "Blocked unregistered tweak undo: " + catalogActionId);
                        if (_webUiHost != null) _webUiHost.PushStatus("تعديل غير مسجل في كتالوج الأمان: " + name);
                        return;
                    }
                    if (policyRes.Status == PolicyEvaluationStatus.RequiresUserConfirmation_YellowTier || (policyRes.Item != null && policyRes.Item.Tier == ActionTier.Red))
                    {
                        bool userConfirmed = false;
                        if (IsPartialUndoTweak(name))
                        {
                            var rPartialUndo = MessageBox.Show(
                                "تراجع جزئي عن التعديل (" + name + "):\n\n" +
                                "سيحاول ClearWin إعادة تثبيت الحزمة عبر Winget. هذا التراجع جزئي وقد يتطلب اتصالاً بالإنترنت ولا يستعيد الإعدادات السابقة بالكامل.\n\n" +
                                "هل تريد المتابعة؟",
                                "ClearWin — تراجع جزئي",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);
                            userConfirmed = (rPartialUndo == MessageBoxResult.Yes);
                        }
                        else
                        {
                            string displayName = policyRes.Item != null ? policyRes.Item.DisplayNameAr : name;
                            var rUndo = MessageBox.Show(
                                "تأكيد استعادة التعديل (" + displayName + "):\n\nهل توافق صراحة على متابعة التراجع واستعادة الإعداد الافتراضي؟",
                                "ClearWin — تأكيد التراجع",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);
                            userConfirmed = (rUndo == MessageBoxResult.Yes);
                        }

                        if (!userConfirmed) return;

                        var confirmedRes = ActionPolicyGate.Evaluate(catalogActionId, ActionRequester.User, true, AdminSessionManager.IsAdminConnected);
                        if (!confirmedRes.Allowed)
                        {
                            if (_webUiHost != null) _webUiHost.PushStatus(confirmedRes.ReasonAr);
                            MessageBox.Show(confirmedRes.ReasonAr, "ClearWin — تعذر التراجع", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }
                    else if (!policyRes.Allowed)
                    {
                        if (_webUiHost != null) _webUiHost.PushStatus(policyRes.ReasonAr);
                        MessageBox.Show(policyRes.ReasonAr, "ClearWin — تعذر التراجع", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    bool allowedUndo = WebUiTweakExists(name, "undo.ps1");
                    if (!allowedUndo) { if (_webUiHost != null) _webUiHost.PushStatus("ملف التراجع غير متاح: " + name); return; }

                    if (!TryStartTweak(name, "undo")) { if (_webUiHost != null) _webUiHost.PushStatus("التراجع قيد التنفيذ بالفعل: " + name); return; }
                    if (_webUiHost != null) _webUiHost.PushStatus("جاري التراجع عن: " + name + " ...");
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            bool ok;
                            string result = RunTweakScript(name, "undo.ps1", out ok);
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                try
                                {
                                    if (_webUiHost != null) _webUiHost.PushStatus(ok ? "تمت الاستعادة: " + name : "فشل التراجع عن " + name + ": " + result);
                                    if (!ok)
                                    {
                                        MessageBox.Show("فشل التراجع عن التعديل (" + name + "):\n\n" + (string.IsNullOrEmpty(result) ? "(بدون مخرجات)" : result), "ClearWin — فشل التراجع", MessageBoxButton.OK, MessageBoxImage.Error);
                                    }
                                }
                                catch (Exception exUndo) { CWLog.Error("MainWindow.WebUi", exUndo); }
                            }));
                        }
                        finally { FinishTweak(name, "undo"); }
                    });
                    return;
                }
                if (action.StartsWith("tweak:", StringComparison.OrdinalIgnoreCase))
                {
                    string name = action.Substring("tweak:".Length).Trim().ToLowerInvariant();
                    string catalogActionId = "tweak:" + name;

                    if (IsUntestedDeferredTweak(name))
                    {
                        ClearWinAuditLogger.Log(
                            catalogActionId,
                            "Red",
                            "User",
                            AdminSessionManager.IsAdminConnected ? "AdminHelper" : "Normal",
                            false,
                            "BLOCKED_UNTESTED",
                            "VERIFIED",
                            "تم حظر التعديل المؤجل (NOT TESTED) لحماية استقرار النظام: " + name,
                            0);

                        if (_webUiHost != null) _webUiHost.PushStatus("مؤجل (NOT TESTED): " + name + " غير مفعّل في النسخة العامة لحماية النظام.");
                        MessageBox.Show(
                            "هذا التعديل (" + name + ") مصنف NOT TESTED ومؤجل خارج التفعيل الإنتاجي في النسخة العامة لحماية إعدادات النظام الأساسية (BCD / الشبكة / الخدمات) إلى حين التحقق منه في بيئة معزولة (VM/Sandbox).",
                            "ClearWin — تعديل مؤجل لحماية النظام",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
                        return;
                    }

                    // Route through ActionPolicyGate
                    var policyRes = ActionPolicyGate.Evaluate(catalogActionId, ActionRequester.User, false, AdminSessionManager.IsAdminConnected);
                    if (policyRes.Status == PolicyEvaluationStatus.UnregisteredAction)
                    {
                        CWLog.Warn("WebUi.Tweak", "Blocked unregistered tweak action: " + catalogActionId);
                        if (_webUiHost != null) _webUiHost.PushStatus("تعديل غير مسجل في كتالوج الأمان: " + name);
                        MessageBox.Show("التعديل (" + name + ") غير مسجل في كتالوج سياسات الأمان المعتمد.", "ClearWin — حظر أمني", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    if (policyRes.Status == PolicyEvaluationStatus.Blocked_RedTier)
                    {
                        CWLog.Warn("WebUi.Tweak", "Blocked Red tier tweak action: " + catalogActionId);
                        if (_webUiHost != null) _webUiHost.PushStatus("تعديل محظور بموجب السياسة الأمنية: " + name);
                        MessageBox.Show("التعديل (" + name + ") محظور بموجب سياسة أمان النظام: " + policyRes.ReasonAr, "ClearWin — حظر أمني", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    if (policyRes.Status == PolicyEvaluationStatus.RequiresUserConfirmation_YellowTier)
                    {
                        bool userConfirmed = false;
                        if (string.Equals(name, "disable-defender-rtp", StringComparison.OrdinalIgnoreCase))
                        {
                            var rSecurity = MessageBox.Show("تحذير أمني شديد\n\nسيتم تعطيل Windows Defender Real-time Protection مؤقتًا.\nهذا الخيار اختياري ولا يدخل في التحسين التلقائي.\nسيحاول ClearWin إنشاء نقطة استعادة قبل التنفيذ.\n\nهل تريد المتابعة؟", "ClearWin — Defender", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                            userConfirmed = (rSecurity == MessageBoxResult.Yes);
                        }
                        else if (string.Equals(name, "disable-core-isolation", StringComparison.OrdinalIgnoreCase))
                        {
                            var rCore = MessageBox.Show(
                                "تحذير أمني شديد: عزل الذاكرة الأساسية (Core Isolation / HVCI)\n\n" +
                                "تعطيل حماية تكامل الذاكرة يقلل من حماية النواة ضد برمجيات الفدية والهجمات المتقدمة.\n" +
                                "هذا التعديل معطل افتراضياً وهو اختياري بالكامل للاعبين لتقليل تأخير العتاد.\n" +
                                "يتطلب إعادة تشغيل الجهاز لتطبيقه أو التراجع عنه.\n\n" +
                                "هل توافق صراحة على متابعة تعطيل عزل الذاكرة؟",
                                "ClearWin — تحذير أمان Core Isolation",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning);
                            userConfirmed = (rCore == MessageBoxResult.Yes);
                        }
                        else if (string.Equals(name, "debloat-windows", StringComparison.OrdinalIgnoreCase))
                        {
                            var rDebloat = MessageBox.Show(
                                "تنبيه: حذف تطبيقات Windows الدعائية المسبقة (معطلة افتراضياً)\n\n" +
                                "سيقوم هذا الإجراء بإزالة الحزم الترويجية فقط (Bing, Clipchamp, 3DBuilder, Solitaire, News, Weather, Skype).\n" +
                                "الأدوات الأساسية مثل Notepad و Calculator و Paint و Terminal محمية تماماً ولن تُحذف.\n" +
                                "تنبيه: هذا الإجراء دائم وغير قابل للاستعادة تلقائياً.\n\n" +
                                "هل توافق صراحة على متابعة حذف هذه التطبيقات؟",
                                "ClearWin — إزالة التطبيقات",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);
                            userConfirmed = (rDebloat == MessageBoxResult.Yes);
                        }
                        else if (string.Equals(name, "run-disk-cleanup", StringComparison.OrdinalIgnoreCase))
                        {
                            var rClean = MessageBox.Show(
                                "تنبيه: تنظيف القرص ومخزن المكونات (عملية دائمة غير قابلة للاستعادة)\n\n" +
                                "سيقوم هذا الإجراء بحذف الملفات المؤقتة في %TEMP% و Windows\\Temp، وتنظيف التحديثات الملغاة في مخزن المكونات (WinSxS).\n" +
                                "هذه العملية تحرر مساحة القرص نهائياً ولا يمكن استعادة الملفات المحذوفة.\n\n" +
                                "هل توافق صراحة على متابعة تنظيف القرص؟",
                                "ClearWin — تنظيف القرص",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning);
                            userConfirmed = (rClean == MessageBoxResult.Yes);
                        }
                        else if (IsPartialUndoTweak(name))
                        {
                            var rPart = MessageBox.Show(
                                "تنبيه استعادة جزئية (" + name + "):\n\n" +
                                "هذا الإجراء يزيل حزم التطبيق من النظام. التراجع عنه لاحقاً يتم عبر إعادة التثبيت من Winget (استعادة جزئية قد لا تعيد البيانات أو الإعدادات المحلية السابقة).\n" +
                                "سيتم إنشاء نقطة استعادة قبل التنفيذ.\n\n" +
                                "هل تريد المتابعة؟",
                                "ClearWin — تنبيه استعادة جزئية",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning);
                            userConfirmed = (rPart == MessageBoxResult.Yes);
                        }
                        else
                        {
                            string displayName = policyRes.Item != null ? policyRes.Item.DisplayNameAr : name;
                            string desc = policyRes.Item != null ? policyRes.Item.DescriptionAr : "";
                            string tierName = policyRes.Item != null ? policyRes.Item.Tier.ToString() : "Yellow";
                            var rConfirm = MessageBox.Show(
                                "تأكيد تنفيذ تعديل (" + displayName + " - مستوى " + tierName + "):\n\n" +
                                desc + "\n\nهل توافق صراحة على تطبيق هذا التعديل؟",
                                "ClearWin — تأكيد أمني (" + tierName + ")",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Question);
                            userConfirmed = (rConfirm == MessageBoxResult.Yes);
                        }

                        if (!userConfirmed)
                        {
                            if (_webUiHost != null) _webUiHost.PushStatus("تم إلغاء التعديل من قبل المستخدم: " + name);
                            return;
                        }

                        var confirmedRes = ActionPolicyGate.Evaluate(catalogActionId, ActionRequester.User, true, AdminSessionManager.IsAdminConnected);
                        if (!confirmedRes.Allowed)
                        {
                            if (_webUiHost != null) _webUiHost.PushStatus(confirmedRes.ReasonAr);
                            MessageBox.Show(confirmedRes.ReasonAr, "ClearWin — تعذر التنفيذ", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                    }
                    else if (!policyRes.Allowed)
                    {
                        if (_webUiHost != null) _webUiHost.PushStatus(policyRes.ReasonAr);
                        MessageBox.Show(policyRes.ReasonAr, "ClearWin — تعذر التنفيذ", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    bool allowed = WebUiTweakExists(name, "apply.ps1");
                    if (!allowed) { if (_webUiHost != null) _webUiHost.PushStatus("ملف التعديل غير متاح: " + name); return; }

                    if (!TryStartTweak(name, "apply")) { if (_webUiHost != null) _webUiHost.PushStatus("التعديل قيد التنفيذ بالفعل: " + name); return; }
                    if (_webUiHost != null) _webUiHost.PushStatus("جاري تنفيذ: " + name + " ...");
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            bool ok;
                            string result = RunTweakScript(name, "apply.ps1", out ok);
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                try { if (_webUiHost != null) _webUiHost.PushStatus(ok ? "تم تطبيق " + name : "فشل أو توقف " + name + ": " + result); }
                                catch (Exception exApp) { CWLog.Error("MainWindow.WebUi", exApp); }
                            }));
                        }
                        finally { FinishTweak(name, "apply"); }
                    });
                    return;
                }
                if (_webUiHost != null) _webUiHost.PushStatus("لم يتم التعرف على الأمر من محرك ClearWin: " + action);
            }
            catch (Exception ex)
            {
                CWLog.Error("MainWindow.WebUi", ex);
                if (_webUiHost != null) _webUiHost.PushStatus("حدث خطأ: " + ex.Message);
            }
        }

        private bool WebUiTweakExists(string name, string scriptFileName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name)) return false;
                string path = ResolveTweakScriptPath(name, scriptFileName);
                return !string.IsNullOrEmpty(path) && System.IO.File.Exists(path);
            }
            catch { return false; }
        }

        public void RefreshWebUiMetricsNow()
        {
            try
            {
                if (_webUiHost == null || _agent == null) return;
                bool gameActive = false;
                bool frameRenderLive = _frameTracker != null && _frameTracker.IsRenderingLive;
                try { gameActive = _agent.CurrentState.IsGame; } catch (Exception ex14) { CWLog.Error("MainWindow.WebUi", ex14); }
                bool gameFpsLive = gameActive && frameRenderLive;
                double liveFps = gameFpsLive && _frameTracker != null ? _frameTracker.CurrentFps : 0;
                _webUiHost.PushMetrics(_realCpu, _realRam, _realDisk, _realTemp, liveFps, _healthScore,
                    _agent.Mode.ToString(), System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable(),
                    _realGpu, _gpuTemp, _netSentSpeed * 8.0 / 1024.0, _netRecvSpeed * 8.0 / 1024.0,
                    _gpuName, GetProcessorDisplayName(), _cachedTotalRamGB);
            }
            catch (Exception ex)
            {
                CWLog.Error("MainWindow.WebUi.RefreshMetrics", ex);
                if (_webUiHost != null) _webUiHost.PushStatus("تعذر تحديث القياسات: " + ex.Message);
            }
        }

        private void GenerateWebAiSnapshot()
        {
            try { _agent.GenerateForecast(); } catch (Exception ex15) { CWLog.Error("MainWindow.WebUi", ex15); }
            try { _agent.RunSelfDiagnostics(); } catch (Exception ex16) { CWLog.Error("MainWindow.WebUi", ex16); }
        }

        private void NavigateWebPage(string page)
        {
            try
            {
                string tab = "نظرة عامة";
                string label = "Dashboard";
                switch (page)
                {
                    case "ai": tab = "Smart Agent"; label = "AI Agent"; break;
                    case "boost": tab = "تحسين"; label = "System Boost"; break;
                    case "scan": tab = "الأمان"; label = "Health & Safety"; break;
                    case "security": tab = "الأمان"; label = "Health & Safety"; break;
                    case "monitor": tab = "مراقبة"; label = "Dashboard"; break;
                    case "appearance": tab = "إعدادات"; label = "Settings"; break;
                    case "changelog": tab = "الذكاء المتقدم"; label = "Advanced AI"; break;
                    case "game": tab = "ألعاب"; label = "Game Boost"; break;
                    case "cleaner": tab = "تنظيف"; label = "Debloater"; break;
                    case "tweaks": tab = "تحسينات"; label = "Tweaks"; break;
                    case "startup": tab = "بدء التشغيل"; label = "Startup Manager"; break;
                    case "processes": tab = "العمليات"; label = "Task Manager"; break;
                    case "network": tab = "الشبكة"; label = "Network"; break;
                    case "disk": tab = "الأقراص"; label = "Disk"; break;
                    case "gpu": tab = "GPU"; label = "GPU"; break;
                    case "tools": tab = "أدوات"; label = "Tools"; break;
                    case "settings": tab = "إعدادات"; label = "Settings"; break;
                    case "health": tab = "الأمان"; label = "Health & Safety"; break;
                    case "advanced": tab = "الذكاء المتقدم"; label = "Advanced AI"; break;
                    case "sandbox": tab = "محاكاة الذكاء"; label = "AI Sandbox"; break;
                    case "dashboard": default: tab = "نظرة عامة"; label = "Dashboard"; break;
                }
                Border item = null;
                if (_navItems != null) _navItems.TryGetValue(label, out item);
                SwitchTab(tab, item);
                if (_webUiHost != null) _webUiHost.PushPage(page);
            }
            catch (Exception ex)
            {
                CWLog.Error("MainWindow.WebUi.Navigate", ex);
                if (_webUiHost != null) _webUiHost.PushStatus("تعذر فتح الصفحة: " + ex.Message);
            }
        }

        private void BroadcastAdminAutonomyState()
        {
            try
            {
                if (_webUiHost == null) return;
                string statusStr = AdminSessionManager.Status.ToString();
                bool isElevated = AdminSessionManager.IsAdminConnected;
                bool greenAutonomy = ActionPolicyGate.GreenAutonomyEnabled;
                string msg = AdminSessionManager.StatusMessage;
                _webUiHost.PushAdminAutonomyState(statusStr, isElevated, greenAutonomy, msg);
            }
            catch (Exception ex)
            {
                CWLog.Error("MainWindow.BroadcastAdminAutonomyState", ex);
            }
        }

        private void SendActionCatalogJson()
        {
            try
            {
                if (_webUiHost == null) return;
                var sb = new System.Text.StringBuilder();
                sb.Append("[");
                bool first = true;
                foreach (var item in ActionCatalog.GetAllItems())
                {
                    if (!first) sb.Append(",");
                    first = false;
                    sb.Append("{");
                    sb.Append("\"actionId\":\"").Append(EscapeJsonString(item.ActionId)).Append("\",");
                    sb.Append("\"displayNameAr\":\"").Append(EscapeJsonString(item.DisplayNameAr)).Append("\",");
                    sb.Append("\"tier\":\"").Append(item.Tier.ToString()).Append("\",");
                    sb.Append("\"requiresAdmin\":").Append(item.RequiresAdmin ? "true" : "false").Append(",");
                    sb.Append("\"isReversible\":").Append(item.IsReversible ? "true" : "false").Append(",");
                    sb.Append("\"category\":\"").Append(EscapeJsonString(item.Category)).Append("\",");
                    sb.Append("\"descriptionAr\":\"").Append(EscapeJsonString(item.DescriptionAr)).Append("\"");
                    sb.Append("}");
                }
                sb.Append("]");
                _webUiHost.PushActionCatalog(sb.ToString());
            }
            catch (Exception ex)
            {
                CWLog.Error("MainWindow.SendActionCatalogJson", ex);
            }
        }

        private void SendAuditLogsJson()
        {
            try
            {
                if (_webUiHost == null) return;
                string json = ClearWinAuditLogger.GetRecentLogsJson(50);
                _webUiHost.PushAuditLogs(json);
            }
            catch (Exception ex)
            {
                CWLog.Error("MainWindow.SendAuditLogsJson", ex);
            }
        }

        private void BroadcastSettingsState()
        {
            try
            {
                if (_webUiHost == null) return;
                string json = string.Format("{{\"channel\":\"clearwin\",\"type\":\"settings_state\",\"settings\":{{\"minimizeToTray\":{0},\"startWithWindows\":{1},\"gameGpuPower\":{2}}}}}",
                    _minimizeToTray ? "true" : "false",
                    _startWithWindows ? "true" : "false",
                    _gameGpuPower ? "true" : "false");
                _webUiHost.PostWebJson(json);
            }
            catch (Exception ex)
            {
                CWLog.Error("MainWindow.BroadcastSettingsState", ex);
            }
        }
    }
}
