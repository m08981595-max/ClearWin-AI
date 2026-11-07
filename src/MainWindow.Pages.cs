using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ClearWin
{
    public partial class MainWindow
    {
        private UIElement BuildLegacyDashboard()
        {
            var root = new Grid { Margin = new Thickness(0, 0, 0, 20) };
            try
            {
            // 8 rows: Welcome, 6 KPI, Overview+AI, LiveMonitor, QuickActions, Activity+Resources, Storage+Health, StatusBar
            for (int ri = 0; ri < 8; ri++) root.RowDefinitions.Add(new RowDefinition { Height = ri == 7 ? new GridLength(38) : GridLength.Auto });

            // === Row 0: Welcome Card ===
            var welcomeCard = MakeCard(16, Theme.Accent);
            var welcomeGrid = new Grid { Margin = new Thickness(20, 16, 20, 16) };
            welcomeGrid.ColumnDefinitions.Add(new ColumnDefinition());
            welcomeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var welcomeLeft = new StackPanel();
            _dashWelcomeTxt = MakeText("مرحباً بعودتك!", 24, Theme.TextPrimary, true);
            welcomeLeft.Children.Add(_dashWelcomeTxt);
            _dashHealthBriefTxt = MakeText("الذكاء الاصطناعي يراقب نظامك.", 13, Theme.TextSecondary, false);
            _dashHealthBriefTxt.Margin = new Thickness(0, 4, 0, 0);
            welcomeLeft.Children.Add(_dashHealthBriefTxt);
            _healthTrendTxt = MakeText("صحة النظام: جارٍ التحميل...", 12, Theme.TextMuted, false);
            _healthTrendTxt.Margin = new Thickness(0, 4, 0, 0);
            welcomeLeft.Children.Add(_healthTrendTxt);
            Grid.SetColumn(welcomeLeft, 0); welcomeGrid.Children.Add(welcomeLeft);
            var welcomeRight = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var dashAiBrief = MakeText("\uE99A", 28, Theme.Accent, false);
            dashAiBrief.FontFamily = new FontFamily("Segoe MDL2 Assets");
            dashAiBrief.Margin = new Thickness(0, 0, 12, 0);
            welcomeRight.Children.Add(dashAiBrief);
            var dashBriefTxt = new StackPanel();
            dashBriefTxt.Children.Add(MakeText("محرك الذكاء الاصطناعي", 14, Theme.TextPrimary, true));
            dashBriefTxt.Children.Add(MakeText("Active", 11, Theme.Green, true));
            welcomeRight.Children.Add(dashBriefTxt);
            Grid.SetColumn(welcomeRight, 1); welcomeGrid.Children.Add(welcomeRight);
            welcomeCard.Child = welcomeGrid;
            AddHoverLift(welcomeCard);
            Grid.SetRow(welcomeCard, 0); root.Children.Add(welcomeCard);

            // === Row 1: 6 KPI Cards ===
            var kpiGrid = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            for (int ki = 0; ki < 6; ki++) kpiGrid.ColumnDefinitions.Add(new ColumnDefinition());
            string[] kpiTitles = new[] { "Performance", "Gaming", "Health", "Power", "Temperature", "AI Score" };
            string[] kpiIcons = new[] { "\uE946", "\uE7FC", "\uE730", "\uE850", "\uE9CA", "\uE99A" };
            Color[] kpiColors = new[] { Theme.Blue, Theme.Green, Theme.Accent, Theme.Warning, Theme.Error, Theme.Purple };
            for (int ki = 0; ki < 6; ki++)
            {
                var kCard = MakeCard(12, kpiColors[ki]);
                var kStack = new StackPanel { Margin = new Thickness(10, 10, 10, 8) };
                var kHdr = new StackPanel { Orientation = Orientation.Horizontal };
                var kIcon = MakeText(kpiIcons[ki], 13, kpiColors[ki], false);
                kIcon.FontFamily = new FontFamily("Segoe MDL2 Assets");
                kHdr.Children.Add(kIcon);
                kHdr.Children.Add(MakeText("  " + kpiTitles[ki], 10, Theme.TextMuted, false));
                kStack.Children.Add(kHdr);
                _kpiVals[ki] = MakeText("--", 20, Theme.TextPrimary, true);
                _kpiVals[ki].Margin = new Thickness(0, 6, 0, 0);
                _kpiVals[ki].HorizontalAlignment = HorizontalAlignment.Center;
                kStack.Children.Add(_kpiVals[ki]);
                _kpiTrends[ki] = MakeText("--", 9, Theme.TextMuted, false);
                _kpiTrends[ki].HorizontalAlignment = HorizontalAlignment.Center;
                kStack.Children.Add(_kpiTrends[ki]);
                _kpiSparks[ki] = new Grid { Height = 24, Margin = new Thickness(0, 4, 0, 0) };
                kStack.Children.Add(_kpiSparks[ki]);
                kCard.Child = kStack;
                AddHoverLift(kCard);
                Grid.SetColumn(kCard, ki); kpiGrid.Children.Add(kCard);
            }
            Grid.SetRow(kpiGrid, 1); root.Children.Add(kpiGrid);

            // === Row 2: System Overview + AI Summary ===
            var row2 = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.2, GridUnitType.Star) });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // System Overview card (8 rings)
            var overviewCard = MakeCard(14, null);
            var overviewStack = new StackPanel { Margin = new Thickness(16) };
            overviewStack.Children.Add(MakeText("نظرة عامة على النظام", 15, Theme.TextPrimary, true));
            var overviewGrid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            for (int oi = 0; oi < 8; oi++) overviewGrid.ColumnDefinitions.Add(new ColumnDefinition());
            string[] ovLabels = new[] { "CPU", "RAM", "GPU", "Disk", "Net", "CPU Temp", "GPU Temp", "Batt" };
            Color[] ovColors = new[] { Theme.Blue, Theme.Purple, Theme.Green, Theme.Warning, Theme.Cyan, Theme.Error, Colors.OrangeRed, Theme.Green };
            for (int oi = 0; oi < 8; oi++)
            {
                var ovStack = new StackPanel { Margin = new Thickness(2) };
                _overviewRings[oi] = new CircularProgress(44, 4);
                _overviewRings[oi].ShowCenterText = false;
                _overviewRings[oi].SetProgress(0, ovColors[oi]);
                _overviewRings[oi].HorizontalAlignment = HorizontalAlignment.Center;
                ovStack.Children.Add(_overviewRings[oi]);
                _overviewLabels[oi] = MakeText("--", 11, Theme.TextPrimary, true);
                _overviewLabels[oi].HorizontalAlignment = HorizontalAlignment.Center;
                ovStack.Children.Add(_overviewLabels[oi]);
                _overviewSubs[oi] = MakeText(ovLabels[oi], 8, Theme.TextMuted, false);
                _overviewSubs[oi].HorizontalAlignment = HorizontalAlignment.Center;
                ovStack.Children.Add(_overviewSubs[oi]);
                Grid.SetColumn(ovStack, oi); overviewGrid.Children.Add(ovStack);
            }
            overviewStack.Children.Add(overviewGrid);
            overviewCard.Child = overviewStack;
            AddHoverLift(overviewCard);
            Grid.SetColumn(overviewCard, 0); row2.Children.Add(overviewCard);

            // AI Summary card
            var aiSumCard = MakeCard(14, Theme.Accent);
            var aiSumStack = new StackPanel { Margin = new Thickness(16) };
            aiSumStack.Children.Add(MakeText("ملخص الذكاء الاصطناعي", 15, Theme.TextPrimary, true));
            _dashAiStatus = MakeText("مراقبة", 13, Theme.Green, true);
            _dashAiStatus.Margin = new Thickness(0, 8, 0, 4);
            aiSumStack.Children.Add(_dashAiStatus);
            _dashAiConfidence = MakeText("الثقة: --%", 12, Theme.TextSecondary, false);
            aiSumStack.Children.Add(_dashAiConfidence);
            _dashAiLastAction = MakeText("آخر إجراء: --", 12, Theme.TextSecondary, false);
            _dashAiLastAction.Margin = new Thickness(0, 8, 0, 0);
            aiSumStack.Children.Add(_dashAiLastAction);
            _dashAgentStatus = MakeText("الوضع: مفعّل", 12, Theme.TextMuted, false);
            _dashAgentStatus.Margin = new Thickness(0, 4, 0, 0);
            aiSumStack.Children.Add(_dashAgentStatus);
            var openAiBtn = new Border { Background = new SolidColorBrush(Theme.Accent), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 12, 0, 0), Cursor = Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Stretch };
            openAiBtn.Child = MakeText("Open AI Agent", 11, Colors.White, true);
            openAiBtn.MouseDown += (s, e) => SwitchTab("Smart Agent", _navItems.ContainsKey("AI Agent") ? _navItems["AI Agent"] : _activeNav);
            AddHoverLift(openAiBtn);
            aiSumStack.Children.Add(openAiBtn);
            aiSumCard.Child = aiSumStack;
            AddHoverLift(aiSumCard);
            Grid.SetColumn(aiSumCard, 1); row2.Children.Add(aiSumCard);
            Grid.SetRow(row2, 2); root.Children.Add(row2);

            // === Row 3: Live Monitor (5 mini graphs) ===
            var liveCard = MakeCard(14, null);
            var liveStack = new StackPanel { Margin = new Thickness(16) };
            liveStack.Children.Add(MakeText("Live Monitor", 15, Theme.TextPrimary, true));
            var liveGrid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            for (int li = 0; li < 5; li++) liveGrid.ColumnDefinitions.Add(new ColumnDefinition());
            string[] liveNames = new[] { "CPU", "RAM", "GPU", "Network", "Disk" };
            Color[] liveColors = new[] { Theme.Blue, Theme.Purple, Theme.Green, Theme.Cyan, Theme.Warning };
            for (int li = 0; li < 5; li++)
            {
                var lvCard = new Border { Background = new SolidColorBrush(Theme.SubCard), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(3) };
                var lvStack = new StackPanel();
                var lvHdr = new StackPanel { Orientation = Orientation.Horizontal };
                var lvDot = new Border { Width = 6, Height = 6, CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(liveColors[li]), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
                lvHdr.Children.Add(lvDot);
                lvHdr.Children.Add(MakeText(liveNames[li], 10, Theme.TextMuted, false));
                lvStack.Children.Add(lvHdr);
                _liveVals[li] = MakeText("--%", 16, Theme.TextPrimary, true);
                _liveVals[li].Margin = new Thickness(0, 4, 0, 0);
                lvStack.Children.Add(_liveVals[li]);
                _liveGraphs[li] = new Grid { Height = 40, Margin = new Thickness(0, 6, 0, 0) };
                lvStack.Children.Add(_liveGraphs[li]);
                _liveLabels[li] = MakeText("--", 9, Theme.TextMuted, false);
                lvStack.Children.Add(_liveLabels[li]);
                lvCard.Child = lvStack;
                Grid.SetColumn(lvCard, li); liveGrid.Children.Add(lvCard);
            }
            liveStack.Children.Add(liveGrid);
            liveCard.Child = liveStack;
            Grid.SetRow(liveCard, 3); root.Children.Add(liveCard);

            // === Row 4: Quick Actions ===
            var qaCard = MakeCard(14, null);
            var qaStack = new StackPanel { Margin = new Thickness(16) };
            qaStack.Children.Add(MakeText("Quick Actions", 15, Theme.TextPrimary, true));
            var qaGrid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            for (int qi = 0; qi < 7; qi++) qaGrid.ColumnDefinitions.Add(new ColumnDefinition());
            string[] qaNames = new[] { "Optimize", "Game Boost", "Clean", "Repair", "Drivers", "DNS", "Restore" };
            string[] qaIcons = new[] { "\uE946", "\uE7FC", "\uE74D", "\uE777", "\uE9D9", "\uE968", "\uE777" };
            Color[] qaColors = new[] { Theme.Accent, Theme.Green, Theme.Warning, Theme.Error, Theme.Blue, Theme.Cyan, Theme.Purple };
            for (int qi = 0; qi < 7; qi++)
            {
                int idx = qi;
                var qBtn = new StackPanel { Margin = new Thickness(4), VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand };
                var qIcon = MakeText(qaIcons[qi], 18, qaColors[qi], false);
                qIcon.FontFamily = new FontFamily("Segoe MDL2 Assets");
                qIcon.HorizontalAlignment = HorizontalAlignment.Center;
                qBtn.Children.Add(qIcon);
                var qName = MakeText(qaNames[qi], 9, Theme.TextPrimary, false);
                qName.HorizontalAlignment = HorizontalAlignment.Center;
                qName.Margin = new Thickness(0, 4, 0, 0);
                qBtn.Children.Add(qName);
                var qBorder = new Border { Background = new SolidColorBrush(Theme.CardClr), CornerRadius = new CornerRadius(10), Padding = new Thickness(6, 10, 6, 10), Margin = new Thickness(3) };
                qBorder.Child = qBtn;
                AddHoverLift(qBorder);
                qBorder.MouseDown += (s, e) =>
                {
                    qBorder.Background = new SolidColorBrush(qaColors[idx]);
                    var fadeTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
                    fadeTimer.Tick += (ts, te) => { fadeTimer.Stop(); qBorder.Background = new SolidColorBrush(Theme.CardClr); };
                    fadeTimer.Start();
                    if (idx == 0) { RunAllOptimizations(); ShowToast("Optimization started", Theme.Accent); }
                    else if (idx == 1)
                    {
                        try { OptimizeRAM(); } catch (Exception ex) { CWLog.Error("MainWindow.Pages", ex); }
                        try { PurgeStandbyList(); } catch (Exception ex2) { CWLog.Error("MainWindow.Pages", ex2); }
                        System.Threading.Tasks.Task.Run(() =>
                        {
                            try { SmartAgent.ActivateHighestPerformancePowerPlan(); }
                            catch (Exception ex3) { CWLog.Error("MainWindow.Pages", ex3); }
                        });
                        ShowToast("Game Boost activated", Theme.Green);
                    }
                    else if (idx == 2) { RunAllOptimizations(); ShowToast("Clean completed", Theme.Warning); }
                    else if (idx == 3)
                    {
                        ShowToast("Flushing DNS cache...", Theme.Accent);
                        System.Threading.Tasks.Task.Run(() =>
                        {
                            string dnsMsg;
                            bool dnsOk = TryFlushDnsVerified(out dnsMsg);
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                ShowToast(dnsOk ? "Network reset done" : ("DNS flush failed: " + dnsMsg), dnsOk ? Theme.Error : Theme.Warning);
                            }));
                        });
                    }
                    else if (idx == 4)
                    {
                        try { Process.Start(new ProcessStartInfo("devmgmt.msc") { UseShellExecute = true }); } catch (Exception ex5) { CWLog.Error("MainWindow.Pages", ex5); }
                        ShowToast("Device Manager opened", Theme.Blue);
                    }
                    else if (idx == 5)
                    {
                        ShowToast("Refreshing DNS resolver cache...", Theme.Cyan);
                        System.Threading.Tasks.Task.Run(() =>
                        {
                            string dnsRefreshMsg;
                            bool dnsOk = TryFlushDnsVerified(out dnsRefreshMsg);
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                ShowToast(dnsOk ? "DNS cache refreshed safely" : ("DNS action failed: " + dnsRefreshMsg), dnsOk ? Theme.Cyan : Theme.Warning);
                            }));
                        });
                    }
                    else if (idx == 6)
                    {
                        ShowToast("Creating restore point...", Theme.Purple);
                        System.Threading.Tasks.Task.Run(() =>
                        {
                            string restoreMessage;
                            bool restoreOk = CreateClearWinRestorePoint("Dashboard manual restore", out restoreMessage, allowSessionCache: false);
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                ShowToast(restoreOk ? "Restore point created" : "Restore point creation failed: " + restoreMessage, restoreOk ? Theme.Purple : Theme.Error);
                            }));
                        });
                    }
                };
                Grid.SetColumn(qBorder, qi); qaGrid.Children.Add(qBorder);
            }
            qaStack.Children.Add(qaGrid);
            qaCard.Child = qaStack;
            Grid.SetRow(qaCard, 4); root.Children.Add(qaCard);

            // === Row 5: Recent Activity + Top Resource Consumers ===
            var row5 = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            row5.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) });
            row5.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Recent Activity
            var actCard = MakeCard(14, null);
            var actStack = new StackPanel { Margin = new Thickness(16) };
            actStack.Children.Add(MakeText("Recent Activity", 15, Theme.TextPrimary, true));
            _dashActivityStack = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            actStack.Children.Add(_dashActivityStack);
            actCard.Child = actStack;
            AddHoverLift(actCard);
            Grid.SetColumn(actCard, 0); row5.Children.Add(actCard);

            // Top Resource Consumers
            var topCard = MakeCard(14, null);
            var topStack = new StackPanel { Margin = new Thickness(16) };
            topStack.Children.Add(MakeText("Top Consumers", 15, Theme.TextPrimary, true));
            for (int pi = 0; pi < 5; pi++)
            {
                var pRow = new Grid { Margin = new Thickness(0, 6, 0, 0) };
                pRow.ColumnDefinitions.Add(new ColumnDefinition());
                pRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                _topProcNames[pi] = MakeText("--", 11, Theme.TextPrimary, false);
                Grid.SetColumn(_topProcNames[pi], 0); pRow.Children.Add(_topProcNames[pi]);
                _topProcPcts[pi] = MakeText("--", 11, Theme.Accent, true);
                Grid.SetColumn(_topProcPcts[pi], 1); pRow.Children.Add(_topProcPcts[pi]);
                var barTrack = new Border { Height = 3, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Theme.SubCard), Margin = new Thickness(0, 4, 0, 0) };
                _topProcBars[pi] = new Border { Height = 3, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Theme.Accent), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
                barTrack.Child = _topProcBars[pi];
                topStack.Children.Add(pRow);
                topStack.Children.Add(barTrack);
            }
            topCard.Child = topStack;
            AddHoverLift(topCard);
            Grid.SetColumn(topCard, 1); row5.Children.Add(topCard);
            Grid.SetRow(row5, 5); root.Children.Add(row5);

            // === Row 6: Storage Overview + Quick Health ===
            var row6 = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            row6.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            row6.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Storage Overview
            var storCard = MakeCard(14, null);
            var storStack = new StackPanel { Margin = new Thickness(16) };
            storStack.Children.Add(MakeText("Storage", 15, Theme.TextPrimary, true));
            string[] storDriveNames = new[] { "C:", "D:", "Free Space" };
            Color[] storDriveColors = new[] { Theme.Blue, Theme.Green, Theme.Cyan };
            for (int si = 0; si < 3; si++)
            {
                var sRow = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
                var sHdr = new Grid();
                sHdr.ColumnDefinitions.Add(new ColumnDefinition());
                sHdr.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                _storLabels[si] = MakeText(storDriveNames[si], 11, Theme.TextPrimary, true);
                Grid.SetColumn(_storLabels[si], 0); sHdr.Children.Add(_storLabels[si]);
                _storVals[si] = MakeText("--", 11, Theme.TextMuted, false);
                Grid.SetColumn(_storVals[si], 1); sHdr.Children.Add(_storVals[si]);
                sRow.Children.Add(sHdr);
                var sTrack = new Border { Height = 6, CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(Theme.SubCard), Margin = new Thickness(0, 4, 0, 0) };
                _storBars[si] = new Border { Width = 0, HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(storDriveColors[si]) };
                sTrack.Child = _storBars[si];
                sRow.Children.Add(sTrack);
                storStack.Children.Add(sRow);
            }
            storCard.Child = storStack;
            AddHoverLift(storCard);
            Grid.SetColumn(storCard, 0); row6.Children.Add(storCard);

            // Quick Health
            var healthCard = MakeCard(14, null);
            var healthStack = new StackPanel { Margin = new Thickness(16) };
            healthStack.Children.Add(MakeText("System Health", 15, Theme.TextPrimary, true));
            string[] healthNames = new[] { "Drivers", "Updates", "Security", "Startup", "Registry", "Services" };
            for (int hi = 0; hi < 6; hi++)
            {
                var hRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
                _healthDots[hi] = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(Theme.Green), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center,
                    Effect = new DropShadowEffect { Color = Theme.Green, BlurRadius = 4, ShadowDepth = 0, Opacity = 0.4 } };
                hRow.Children.Add(_healthDots[hi]);
                _healthLabels[hi] = MakeText(healthNames[hi], 11, Theme.TextSecondary, false);
                hRow.Children.Add(_healthLabels[hi]);
                healthStack.Children.Add(hRow);
            }
            healthCard.Child = healthStack;
            AddHoverLift(healthCard);
            Grid.SetColumn(healthCard, 1); row6.Children.Add(healthCard);
            Grid.SetRow(row6, 6); root.Children.Add(row6);

            // === Row 7: Status Bar ===
            var statusBar = new Border { Background = new SolidColorBrush(Theme.CardClr), BorderBrush = new SolidColorBrush(Theme.BorderClr), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(20, 0, 20, 0) };
            var statusDock = new DockPanel();
            _osInfo = MakeText("Windows", 11, Theme.TextMuted, false);
            DockPanel.SetDock(_osInfo, Dock.Right); statusDock.Children.Add(_osInfo);
            _statusTxt = MakeText("Uptime: --", 11, Theme.TextMuted, false);
            DockPanel.SetDock(_statusTxt, Dock.Right); statusDock.Children.Add(_statusTxt);
            var sysProtRow = new StackPanel { Orientation = Orientation.Horizontal };
            _sysProtDot = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(Theme.Green), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center,
                Effect = new DropShadowEffect { Color = Theme.Green, BlurRadius = 6, ShadowDepth = 0, Opacity = 0.5 } };
            sysProtRow.Children.Add(_sysProtDot);
            _sysProtTxt = MakeText("جارٍ الفحص...", 11, Theme.TextMuted, true);
            sysProtRow.Children.Add(_sysProtTxt);
            DockPanel.SetDock(sysProtRow, Dock.Left); statusDock.Children.Add(sysProtRow);
            statusBar.Child = statusDock;
            Grid.SetRow(statusBar, 7); root.Children.Add(statusBar);
            }
            catch (Exception ex) { try { var diagPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClearWin", "crashes", "dash_diag.txt"); var msg = ex.ToString(); if (ex.InnerException != null) msg += "\n--- INNER ---\n" + ex.InnerException.ToString(); System.IO.File.WriteAllText(diagPath, msg); } catch (Exception ex8) { CWLog.Error("MainWindow.Pages", ex8); } return new TextBlock { Text = "Dashboard Error: " + (ex.InnerException != null ? ex.InnerException.Message : ex.Message), Foreground = new SolidColorBrush(Colors.Red), Margin = new Thickness(20), TextWrapping = TextWrapping.Wrap }; }
            return root;
        }

        private UIElement BuildDashboard()
        {
            return BuildAiTransparencyPage();
        }

        private UIElement BuildMonitor()
        {
            var stack = new StackPanel();
            stack.Children.Add(MakeText("مراقبة", 28, Colors.White, true));
            var _ms = MakeText("مراقبة النظام في الوقت الفعلي", 13, Color.FromArgb(136, 255, 255, 255), false); _ms.Margin = new Thickness(0, 0, 0, 20); stack.Children.Add(_ms);

            var row1 = new Grid(); row1.ColumnDefinitions.Add(new ColumnDefinition()); row1.ColumnDefinitions.Add(new ColumnDefinition());

            _monCpuRing = new CircularProgress(120, 8) { Margin = new Thickness(0, 12, 0, 12), HorizontalAlignment = HorizontalAlignment.Center };
            _monRamRing = new CircularProgress(120, 8) { Margin = new Thickness(0, 12, 0, 12), HorizontalAlignment = HorizontalAlignment.Center };

            var cpuCard = MakeCard(18, null); var cpuStack = new StackPanel { Margin = new Thickness(20) };
            cpuStack.Children.Add(MakeText("المعالج", 20, Colors.White, true));
            cpuStack.Children.Add(_monCpuRing);
            _monCpuPct = MakeText("0%", 14, Colors.White, true); _monCpuPct.HorizontalAlignment = HorizontalAlignment.Center; cpuStack.Children.Add(_monCpuPct);
            _monCpuFreq = MakeText("0.0 GHz", 11, Color.FromArgb(136, 255, 255, 255), false); _monCpuFreq.HorizontalAlignment = HorizontalAlignment.Center; cpuStack.Children.Add(_monCpuFreq);
            _monCpuPerCore = MakeText("", 10, Color.FromArgb(119, 255, 255, 255), false); _monCpuPerCore.HorizontalAlignment = HorizontalAlignment.Center; cpuStack.Children.Add(_monCpuPerCore);
            cpuCard.Child = cpuStack;

            var ramCard = MakeCard(18, null); var ramStack = new StackPanel { Margin = new Thickness(20) };
            ramStack.Children.Add(MakeText("الذاكرة", 20, Colors.White, true));
            ramStack.Children.Add(_monRamRing);
            _monRamPct = MakeText("0%", 14, Colors.White, true); _monRamPct.HorizontalAlignment = HorizontalAlignment.Center; ramStack.Children.Add(_monRamPct);
            _monRamGB = MakeText("0.0 / 0.0 GB", 11, Color.FromArgb(136, 255, 255, 255), false); _monRamGB.HorizontalAlignment = HorizontalAlignment.Center; ramStack.Children.Add(_monRamGB);
            ramCard.Child = ramStack;

            Grid.SetColumn(cpuCard, 0); Grid.SetColumn(ramCard, 1); row1.Children.Add(cpuCard); row1.Children.Add(ramCard); stack.Children.Add(row1);

            var graphCard = MakeCard(18, null); var graphStack = new StackPanel { Margin = new Thickness(20) };
            graphStack.Children.Add(MakeText("الرسم البياني", 16, Colors.White, true));
            _monCpuGraph = new Grid { Height = 60, Background = new SolidColorBrush(Theme.CardClr), Margin = new Thickness(0, 8, 0, 4) };
            graphStack.Children.Add(_monCpuGraph);
            _monRamGraph = new Grid { Height = 60, Background = new SolidColorBrush(Theme.CardClr), Margin = new Thickness(0, 4, 0, 0) };
            graphStack.Children.Add(_monRamGraph);
            graphCard.Child = graphStack; stack.Children.Add(graphCard);

            var statCard = MakeCard(18, null); var statStack = new StackPanel { Margin = new Thickness(20) };
            statStack.Children.Add(MakeText("معلومات النظام", 16, Colors.White, true));
            _monProcTxt = MakeText("العمليات: 0", 12, Color.FromArgb(204, 255, 255, 255), false); _monProcTxt.Margin = new Thickness(0, 8, 0, 2); statStack.Children.Add(_monProcTxt);
            _monThreadTxt = MakeText("الخيوط: 0", 12, Color.FromArgb(204, 255, 255, 255), false); statStack.Children.Add(_monThreadTxt);
            _monCoreTxt = MakeText("", 11, Color.FromArgb(170, 255, 255, 255), false); statStack.Children.Add(_monCoreTxt);
            statCard.Child = statStack; stack.Children.Add(statCard);

            var aiCard = MakeCard(18, null); var aiStack = new StackPanel { Margin = new Thickness(20) };
            aiStack.Children.Add(MakeText("مراقب العمليات الذكي", 16, Colors.White, true));
            var _aid = MakeText("يكشف العمليات اللي تستهلك موارد بشكل غير طبيعي", 11, Color.FromArgb(136, 255, 255, 255), false); _aid.Margin = new Thickness(0, 4, 0, 10); aiStack.Children.Add(_aid);
            _aiProcessPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
            _aiProcessPanel.Children.Add(MakeText("جارٍ التحليل...", 12, Color.FromArgb(100, 255, 255, 255), false));
            aiStack.Children.Add(_aiProcessPanel);
            var aiRefreshBtn = new Border { Background = new SolidColorBrush(Color.FromArgb(24, _accentColor.R, _accentColor.G, _accentColor.B)), CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 5, 12, 5), Width = 120, Cursor = Cursors.Hand };
            aiRefreshBtn.Child = MakeText("تحديث", 11, _accentColor, true);
            aiRefreshBtn.MouseDown += (sai, eai) => CheckAIProcesses();
            aiStack.Children.Add(aiRefreshBtn);
            aiCard.Child = aiStack; stack.Children.Add(aiCard);

            return stack;
        }

        private UIElement BuildOptimizer()
        {
            var stack = new StackPanel();
            stack.Children.Add(MakeText("تحسين", 28, Colors.White, true));
            var _os = MakeText("تحسين أداء النظام بضغطة واحدة", 13, Color.FromArgb(136, 255, 255, 255), false); _os.Margin = new Thickness(0, 0, 0, 20); stack.Children.Add(_os);

            var card = MakeCard(18, null); var inner = new StackPanel { Margin = new Thickness(20) };
            inner.Children.Add(MakeText("ما الذي سيتم تحسينه؟", 16, Colors.White, true));

            string[] items = new[] { "تنظيف الملفات المؤقتة", "تفريغ سلة المحذوفات", "تحسين الطاقة (Ultimate Performance)", "تعطيل كورتانا", "مسح DNS", "إزالة ملفات Prefetch" };
            var checkList = new StackPanel { Margin = new Thickness(0, 10, 0, 12) };
            foreach (var item in items)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
                row.Children.Add(MakeText(" ✓ ", 11, Theme.Green, true));
                row.Children.Add(MakeText(item, 12, Color.FromArgb(170, 255, 255, 255), false));
                checkList.Children.Add(row);
            }
            inner.Children.Add(checkList);

            var optBtn = new Border { Background = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1), GradientStops = { new GradientStop(Theme.Accent, 0), new GradientStop(Theme.Purple, 1) } }, CornerRadius = new CornerRadius(12), Padding = new Thickness(24, 16, 24, 16), Width = 280, HorizontalAlignment = HorizontalAlignment.Left, Cursor = Cursors.Hand, BorderBrush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0), GradientStops = { new GradientStop(Color.FromArgb(80, 47, 139, 255), 0), new GradientStop(Color.FromArgb(60, 139, 92, 246), 1) } }, BorderThickness = new Thickness(1), Effect = new DropShadowEffect { Color = Theme.Accent, BlurRadius = 20, ShadowDepth = 0, Opacity = 0.35 } };
            var ot = MakeText("تشغيل التحسين الكامل", 15, Colors.White, true); ot.HorizontalAlignment = HorizontalAlignment.Center; optBtn.Child = ot;
            optBtn.MouseDown += (s2, e2) => RunAllOptimizations();
            inner.Children.Add(optBtn);

            var restoreBtn = new Border { Background = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0), GradientStops = { new GradientStop(Color.FromArgb(40, 47, 139, 255), 0), new GradientStop(Color.FromArgb(25, 47, 139, 255), 1) } }, CornerRadius = new CornerRadius(10), Padding = new Thickness(18, 10, 18, 10), Margin = new Thickness(0, 12, 0, 0), Width = 260, HorizontalAlignment = HorizontalAlignment.Left, Cursor = Cursors.Hand, BorderBrush = new SolidColorBrush(Color.FromArgb(40, 47, 139, 255)), BorderThickness = new Thickness(1) };
            restoreBtn.Child = MakeText("إنشاء نقطة استعادة", 12, Theme.Accent, true);
            restoreBtn.MouseDown += (s2, e2) =>
            {
                System.Threading.Tasks.Task.Run(() =>
                {
                    string restoreMessage;
                    bool restoreOk = CreateClearWinRestorePoint("Optimizer manual restore", out restoreMessage, allowSessionCache: false);
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        MessageBox.Show(restoreOk ? "تم إنشاء نقطة استعادة النظام!" : "تعذر إنشاء نقطة الاستعادة: " + restoreMessage, "ClearWin");
                    }));
                });
            };
            inner.Children.Add(restoreBtn);

            var debloatBtn = new Border { Background = new SolidColorBrush(Color.FromArgb(32, 236, 72, 153)), CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 8, 16, 8), Margin = new Thickness(0, 10, 0, 0), Width = 260, HorizontalAlignment = HorizontalAlignment.Left, Cursor = Cursors.Hand };
            debloatBtn.Child = MakeText("إزالة برامج ويندوز الزائدة", 12, Theme.Pink, true);
            debloatBtn.MouseDown += (sdb, edb) => RunDebloater();
            inner.Children.Add(debloatBtn);

            card.Child = inner; stack.Children.Add(card);

            var healthCard = MakeCard(18, null); var healthInner = new StackPanel { Margin = new Thickness(20) };
            healthInner.Children.Add(MakeText("صحة النظام", 16, Colors.White, true));
            var healthBarOuter = new Border { Height = 12, CornerRadius = new CornerRadius(6), Background = new SolidColorBrush(Theme.CardClr), Margin = new Thickness(0, 10, 0, 6) };
            _optHealthBarFill = new Border { Width = _healthScore * 2.4, Height = 12, CornerRadius = new CornerRadius(6), Background = new SolidColorBrush(_healthScore > 70 ? Theme.Green : _healthScore > 40 ? Theme.Warning : Theme.Error), HorizontalAlignment = HorizontalAlignment.Left };
            healthBarOuter.Child = _optHealthBarFill; healthInner.Children.Add(healthBarOuter);
            _optHealthTxt = MakeText(string.Format("النقاط: {0:F0}/100", _healthScore), 11, Color.FromArgb(136, 255, 255, 255), false);
            healthInner.Children.Add(_optHealthTxt);
            healthCard.Child = healthInner; stack.Children.Add(healthCard);

            return stack;
        }

        private UIElement BuildCleaner()
        {
            var stack = new StackPanel();
            stack.Children.Add(MakeText("تنظيف", 28, Colors.White, true));
            var _cs = MakeText("تنظيف ملفات النظام غير المرغوب فيها", 13, Color.FromArgb(136, 255, 255, 255), false); _cs.Margin = new Thickness(0, 0, 0, 20); stack.Children.Add(_cs);
            string[][] items = new string[][] { new[] {"ملفات مؤقتة", "ملفات ويندوز المؤقتة"}, new[] {"Prefetch", "Prefetch"}, new[] {"سلة المحذوفات", "الملفات المحذوفة"}, new[] {"ذاكرة المتصفح", "كروم، إيدج"} };
            string[] keys = new[] {"temp", "prefetch", "recycle", "browser"};
            for (int ci = 0; ci < items.Length; ci++)
            {
                string ckey = keys[ci];
                var card = MakeCard(12, null); card.Margin = new Thickness(0, 0, 0, 8);
                var dock2 = new DockPanel { Margin = new Thickness(16, 12, 16, 12) };
                dock2.Children.Add(MakeText(items[ci][0], 14, Colors.White, true));
                var cleanBtn = new Border { Background = new SolidColorBrush(Color.FromArgb(32, _accentColor.R, _accentColor.G, _accentColor.B)), CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 5, 12, 5), Cursor = Cursors.Hand };
                DockPanel.SetDock(cleanBtn, Dock.Right);
                cleanBtn.Child = MakeText("تنظيف", 11, _accentColor, true);
                cleanBtn.MouseDown += (s2, e2) => RunCleanTask(ckey);
                dock2.Children.Add(cleanBtn); card.Child = dock2; stack.Children.Add(card);
            }
            var allBtn = new Border { Background = new LinearGradientBrush(_accentColor, Theme.Purple, 0), CornerRadius = new CornerRadius(10), Padding = new Thickness(20, 10, 20, 10), Margin = new Thickness(0, 12, 0, 0), Width = 260, HorizontalAlignment = HorizontalAlignment.Left, Cursor = Cursors.Hand };
            allBtn.Child = MakeText("تنظيف الكل", 14, Colors.White, true);
            allBtn.MouseDown += (s2, e2) => { RunCleanTask("temp"); RunCleanTask("prefetch"); RunCleanTask("recycle"); RunCleanTask("browser"); };
            stack.Children.Add(allBtn);
            return stack;
        }

        private void RunCleanTask(string key)
        {
            // Recursive file deletion can take seconds; run off the UI thread and
            // marshal only the result message back to the dispatcher.
            System.Threading.Tasks.Task.Run(() =>
            {
                int cleaned = 0, failed = 0;
                Action<string> cleanDir = (dir) =>
                {
                    if (!Directory.Exists(dir)) return;
                    try
                    {
                        var dirs = new Stack<string>(); dirs.Push(dir);
                        while (dirs.Count > 0)
                        {
                            string cur = dirs.Pop();
                            try
                            {
                                foreach (string sub in Directory.GetDirectories(cur)) { try { dirs.Push(sub); } catch (Exception ex9) { CWLog.Error("MainWindow.Pages", ex9); } }
                                foreach (string f in Directory.GetFiles(cur)) { try { File.Delete(f); cleaned++; } catch { failed++; } }
                            }
                            catch { failed++; }
                        }
                    }
                    catch { failed++; }
                };
                try
                {
                    switch (key)
                    {
                        case "temp":
                            cleanDir(Environment.GetEnvironmentVariable("TEMP"));
                            cleanDir(Environment.GetEnvironmentVariable("WINDIR") + "\\Temp");
                            break;
                        case "prefetch":
                            cleanDir(Environment.GetEnvironmentVariable("WINDIR") + "\\Prefetch");
                            break;
                        case "recycle":
                            int hr = SHEmptyRecycleBin(IntPtr.Zero, null, 0);
                            if (hr == 0) cleaned++; else failed++;
                            break;
                        case "browser":
                            cleanDir(Environment.GetEnvironmentVariable("LOCALAPPDATA") + "\\Google\\Chrome\\User Data\\Default\\Cache");
                            cleanDir(Environment.GetEnvironmentVariable("LOCALAPPDATA") + "\\Microsoft\\Edge\\User Data\\Default\\Cache");
                            cleanDir(Environment.GetEnvironmentVariable("LOCALAPPDATA") + "\\BraveSoftware\\Brave-Browser\\User Data\\Default\\Cache");
                            break;
                    }
                    string label = key == "temp" ? "الملفات المؤقتة" : key == "prefetch" ? "Prefetch" : key == "recycle" ? "سلة المحذوفات" : "ذاكرة المتصفح";
                    string msg = string.Format("تم تنظيف {0}:\n\n[تم] {1} ملف تم حذفه\n[!] {2} ملف تم تخطيه", label, cleaned, failed);
                    try
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try { MessageBox.Show(msg, "ClearWin", MessageBoxButton.OK, MessageBoxImage.Information); }
                            catch (Exception ex10) { CWLog.Error("MainWindow.Pages", ex10); }
                        }));
                    }
                    catch (Exception ex11) { CWLog.Error("MainWindow.Pages", ex11); }
                }
                catch (Exception ex12) { CWLog.Error("MainWindow.Pages", ex12); }
            });
        }

        private UIElement BuildGaming()
        {
            var root = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var stack = new StackPanel();
            root.Content = stack;
            stack.Children.Add(MakeText("ألعاب", 28, Colors.White, true));
            var _gs = MakeText("تحسين أداء الألعاب", 13, Color.FromArgb(136, 255, 255, 255), false); _gs.Margin = new Thickness(0, 0, 0, 20); stack.Children.Add(_gs);

            // Game Mode card
            var card = MakeCard(18, null); var inner = new StackPanel { Margin = new Thickness(20) };
            inner.Children.Add(MakeText("وضع الألعاب", 18, Colors.White, true));
            var _gd = MakeText("زيادة الإطارات بتعطيل الخدمات الخلفية وتفريغ الذاكرة", 12, Color.FromArgb(136, 255, 255, 255), false); _gd.Margin = new Thickness(0, 6, 0, 14); inner.Children.Add(_gd);
            var btn = new Border { Background = new LinearGradientBrush(_accentColor, Theme.Purple, 0), CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 10, 16, 10), Width = 200, Cursor = Cursors.Hand };
            var _gbt = MakeText("تفعيل وضع الألعاب", 13, Colors.White, true); _gbt.HorizontalAlignment = HorizontalAlignment.Center;
            btn.Child = _gbt;
            btn.MouseDown += (s2, e2) => ActivateGameMode(); inner.Children.Add(btn);
            _gmStatusTxt = MakeText("الحالة: غير مفعل", 11, Color.FromArgb(136, 255, 255, 255), false);
            _gmStatusTxt.Margin = new Thickness(0, 8, 0, 0); inner.Children.Add(_gmStatusTxt);
            UpdateGameModeStatusUI();
            card.Child = inner; stack.Children.Add(card);

            // 60 FPS floor card (zero drops under load)
            var floorCard = MakeCard(18, null); var floorInner = new StackPanel { Margin = new Thickness(20) };
            floorInner.Children.Add(MakeText("تثبيت 60 فريم (حد أدنى)", 18, Colors.White, true));
            var _fd = MakeText("يمنع نزول الفريمات عن 60 تحت أي حمل - تثبيت استباقي وصفر دروب", 12, Color.FromArgb(136, 255, 255, 255), false); _fd.Margin = new Thickness(0, 6, 0, 14); floorInner.Children.Add(_fd);
            var floorBtn = new Border { Background = new LinearGradientBrush(Color.FromArgb(255, 16, 185, 129), Theme.Purple, 0), CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 10, 16, 10), Width = 200, Cursor = Cursors.Hand };
            var _floorBt = MakeText("تفعيل تثبيت 60 فريم", 13, Colors.White, true); _floorBt.HorizontalAlignment = HorizontalAlignment.Center;
            floorBtn.Child = _floorBt;
            floorBtn.MouseDown += (s2, e2) => ToggleFpsFloor60(); floorInner.Children.Add(floorBtn);
            _fpsFloorStatusTxt = MakeText("الحالة: معطل", 11, Color.FromArgb(136, 255, 255, 255), false);
            _fpsFloorStatusTxt.Margin = new Thickness(0, 8, 0, 0); floorInner.Children.Add(_fpsFloorStatusTxt);
            floorCard.Child = floorInner; stack.Children.Add(floorCard);

            // Full optimization & stability card
            var optCard = MakeCard(18, null); var optInner = new StackPanel { Margin = new Thickness(20) };
            optInner.Children.Add(MakeText("وضع الكفاءة والاستقرار الكامل", 18, Colors.White, true));
            var _od = MakeText("تنظيم كامل للجهاز: تنظيف الذاكرة + تقييد الخلفية + أولوية اللعبة - تحديث دوري كل 90 ثانية", 12, Color.FromArgb(136, 255, 255, 255), false); _od.Margin = new Thickness(0, 6, 0, 14); optInner.Children.Add(_od);
            var fullOptBtn = new Border { Background = new LinearGradientBrush(Color.FromArgb(255, 139, 92, 246), Theme.Purple, 0), CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 10, 16, 10), Width = 200, Cursor = Cursors.Hand };
            var _optBt = MakeText("تفعيل التنظيم الكامل", 13, Colors.White, true); _optBt.HorizontalAlignment = HorizontalAlignment.Center;
            fullOptBtn.Child = _optBt;
            fullOptBtn.MouseDown += (s2, e2) => ToggleFullOptimization(); optInner.Children.Add(fullOptBtn);
            _fullOptStatusTxt = MakeText("الحالة: معطل", 11, Color.FromArgb(136, 255, 255, 255), false);
            _fullOptStatusTxt.Margin = new Thickness(0, 8, 0, 0); optInner.Children.Add(_fullOptStatusTxt);
            optCard.Child = optInner; stack.Children.Add(optCard);

            // Game Booster card
            var boostCard = MakeCard(18, null); var boostInner = new StackPanel { Margin = new Thickness(20) };
            boostInner.Children.Add(MakeText("معزز الألعاب", 18, Colors.White, true));
            var _bd = MakeText("تحسينات سريعة للألعاب", 12, Color.FromArgb(136, 255, 255, 255), false); _bd.Margin = new Thickness(0, 6, 0, 14); boostInner.Children.Add(_bd);

            var boostGrid = new Grid();
            for (int bc = 0; bc < 3; bc++) boostGrid.ColumnDefinitions.Add(new ColumnDefinition());
            for (int br = 0; br < 4; br++) boostGrid.RowDefinitions.Add(new RowDefinition());

            string[][] boosters = new string[][] {
                new[] {"تفريغ RAM", "تفريغ الذاكرة العشوائية بالكامل", "34,197,94"},
                new[] {"الأداء البصري", "إيقاف تأثيرات الويندوز والـ Aero", "59,130,246"},
                new[] {"إلغاء فرملة المعالج", "إبقاء الأنوية نشطة Core Unpark", "139,92,246"},
                new[] {"خطة الطاقة القصوى", "تفعيل Ultimate Power Plan", "251,146,60"},
                new[] {"كاش الكارت", "تنظيف كاش Shader الكارت", "236,72,153"},
                new[] {"زمن الاستجابة 0.5ms", "تقليل الـ Input Lag للنظام", "99,102,241"},
                new[] {"تفريغ Standby", "تطهير الذاكرة المخبأة Standby", "168,85,247"},
                new[] {"نمط MSI للكرت", "تحسين مقاطعات GPU وسلاسة اللعب", "239,68,68"},
                new[] {"تحسين الشبكة", "TCP Ack & NoDelay Tweaks", "20,184,166"},
                new[] {"الجدولة الذكية", "Process Lasso Priorities للماوس", "217,70,239"},
                new[] {"الأولوية التلقائية", "رفع أولوية اللعبة النشطة تلقائياً", "14,165,233"},
                new[] {"معزز الخدمات", "إيقاف الخدمات الخلفية أثناء اللعب", "244,63,94"}
            };
            for (int bi = 0; bi < 12; bi++)
            {
                int bIdx = bi;
                var pts = boosters[bi][2].Split(','); Color pc = Color.FromRgb(byte.Parse(pts[0]), byte.Parse(pts[1]), byte.Parse(pts[2]));
                var bBtn = new Border { Background = new SolidColorBrush(Theme.CardClr), CornerRadius = new CornerRadius(10), Margin = new Thickness(3), Padding = new Thickness(8, 10, 8, 10), Cursor = Cursors.Hand };
                bBtn.ToolTip = boosters[bi][0] + " — " + boosters[bi][1];
                var bStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                bStack.Children.Add(MakeText(boosters[bi][0], 13, Colors.White, true));
                bStack.Children.Add(MakeText(boosters[bi][1], 10, Color.FromArgb(136, 255, 255, 255), false));
                bBtn.Child = bStack;
                bBtn.MouseDown += (s3, e3) =>
                {
                    if (bIdx == 0) { OptimizeRAM(); MessageBox.Show("تم تفريغ الذاكرة بنجاح!", "معزز الألعاب"); }
                    else if (bIdx == 1)
                    {
                        try
                        {
                            var psi = new ProcessStartInfo
                            {
                                FileName = "powershell.exe",
                                Arguments = "-Command \"& { Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VisualEffects' -Name 'VisualFXSetting' -Value 2 -Type DWord -ErrorAction SilentlyContinue; SystemParametersInfo(0x2001, 0, 0, 0x02) } \"",
                                UseShellExecute = false, CreateNoWindow = true
                            };
                            using (Process.Start(psi)) { }
                            MessageBox.Show("تم إيقاف التأثيرات البصرية للويندوز لتوفير الفريمات!", "معزز الألعاب");
                        }
                        catch { MessageBox.Show("خطأ أثناء تعطيل التأثيرات البصرية", "معزز الألعاب"); }
                    }
                    else if (bIdx == 2) { UnparkCPUCores(); }
                    else if (bIdx == 3)
                    {
                        try 
                        { 
                            using (Process.Start(new ProcessStartInfo { FileName = "powercfg.exe", Arguments = "/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", UseShellExecute = true, Verb = "runas" })) { } 
                            try { UnparkCPUCores(); } catch (Exception ex13) { CWLog.Error("MainWindow.Pages", ex13); }
                            MessageBox.Show("تم تفعيل خطة الطاقة القصوى (Ultimate Performance) وإلغاء فرملة المعالج!", "معزز الألعاب"); 
                        } catch (Exception ex14) { CWLog.Error("MainWindow.Pages", ex14); }
                    }
                    else if (bIdx == 4) { CleanShaderCache(); }
                    else if (bIdx == 5) { ToggleTimerResolution(); }
                    else if (bIdx == 6) { bool ok = PurgeStandbyList(); MessageBox.Show(ok ? "تم تفريغ Standby List بنجاح وتحرير المساحة!" : "فشل تفريغ Standby List", "معزز الألعاب"); }
                    else if (bIdx == 7) { EnableGPUMSIMode(); }
                    else if (bIdx == 8) { ApplyLatencyTweaks(); }
                    else if (bIdx == 9) { ApplySmartCPUPriorities(); }
                    else if (bIdx == 10)
                    {
                        _autoPriority = !_autoPriority;
                        string st = _autoPriority ? "تفعيل" : "تعطيل";
                        MessageBox.Show(string.Format("الأولوية التلقائية للألعاب {0}!", st == "تفعيل" ? "مفعلة" : "معطلة"), "معزز الألعاب");
                    }
                    else if (bIdx == 11) { ToggleServicesBooster(); }
                };
                Grid.SetRow(bBtn, bi / 3); Grid.SetColumn(bBtn, bi % 3); boostGrid.Children.Add(bBtn);
            }
            boostInner.Children.Add(boostGrid);
            boostCard.Child = boostInner; stack.Children.Add(boostCard);

            // Overlay card
            var ovCard = MakeCard(18, null); var ovInner = new StackPanel { Margin = new Thickness(20) };
            ovInner.Children.Add(MakeText("تراكب الألعاب", 18, Colors.White, true));
            var _ovd = MakeText("عرض FPS ودرجة الحرارة فوق الألعاب", 12, Color.FromArgb(136, 255, 255, 255), false); _ovd.Margin = new Thickness(0, 6, 0, 14); ovInner.Children.Add(_ovd);
            var ovBtn = new Border { Background = new LinearGradientBrush(Theme.Purple, _accentColor, 0), CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 10, 16, 10), Width = 200, Cursor = Cursors.Hand };
            var _ovbt = MakeText(_overlayEnabled ? "إيقاف التراكب" : "تشغيل التراكب", 13, Colors.White, true); _ovbt.HorizontalAlignment = HorizontalAlignment.Center;
            ovBtn.Child = _ovbt;
            ovBtn.MouseDown += (s2, e2) =>
            {
                _overlayEnabled = !_overlayEnabled;
                _ovbt.Text = _overlayEnabled ? "إيقاف التراكب" : "تشغيل التراكب";
                SaveOverlaySettings();
                if (_overlayEnabled) ShowOverlay(); else HideOverlay();
            };
            ovInner.Children.Add(ovBtn);
            ovCard.Child = ovInner; stack.Children.Add(ovCard);

            // Overlay customization card
            var custCard = MakeCard(18, null); var custInner = new StackPanel { Margin = new Thickness(20) };
            custInner.Children.Add(MakeText("تخصيص التراكب", 16, Colors.White, true));
            var _custD = MakeText("اختر العناصر التي تظهر في التراكب", 11, Color.FromArgb(136, 255, 255, 255), false); _custD.Margin = new Thickness(0, 4, 0, 10); custInner.Children.Add(_custD);

            var toggleGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            for (int tc = 0; tc < 2; tc++) toggleGrid.ColumnDefinitions.Add(new ColumnDefinition());
            string[][] toggles = new string[][] {
                new[] {"FPS", "_overlayShowFPS"}, new[] {"CPU", "_overlayShowCPU"},
                new[] {"RAM", "_overlayShowRAM"}, new[] {"GPU", "_overlayShowGPU"},
                new[] {"الحرارة", "_overlayShowTemp"}, new[] {"الساعة", "_overlayShowClock"}
            };
            // We can't use field refs from strings, so let's just make simple toggle buttons
            for (int ti = 0; ti < 6; ti++)
            {
                int tIdx = ti;
                bool[] initStates = new bool[] { _overlayShowFPS, _overlayShowCPU, _overlayShowRAM, _overlayShowGPU, _overlayShowTemp, _overlayShowClock };
                var tBtn = new Border { Background = new SolidColorBrush(initStates[ti] ? Color.FromArgb(60, _accentColor.R, _accentColor.G, _accentColor.B) : Theme.CardClr), CornerRadius = new CornerRadius(6), Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(3), Cursor = Cursors.Hand, Width = 100, HorizontalAlignment = HorizontalAlignment.Center };
                var tBtnStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                tBtnStack.Children.Add(MakeText(toggles[ti][0], 11, Colors.White, true));
                var tBtnState = MakeText(initStates[ti] ? "ON" : "OFF", 9, initStates[ti] ? Theme.Green : Theme.TextMuted, true);
                tBtnState.HorizontalAlignment = HorizontalAlignment.Center;
                tBtnStack.Children.Add(tBtnState);
                tBtn.Child = tBtnStack;
                tBtn.MouseDown += (s4, e4) =>
                {
                    bool isOn = tBtn.Background is SolidColorBrush && ((SolidColorBrush)tBtn.Background).Color == Color.FromArgb(60, _accentColor.R, _accentColor.G, _accentColor.B);
                    tBtn.Background = isOn ? new SolidColorBrush(Theme.CardClr) : new SolidColorBrush(Color.FromArgb(60, _accentColor.R, _accentColor.G, _accentColor.B));
                    tBtnState.Text = isOn ? "OFF" : "ON";
                    tBtnState.Foreground = new SolidColorBrush(isOn ? Theme.TextMuted : Theme.Green);
                    bool newVal = !isOn;
                    if (tIdx == 0) _overlayShowFPS = newVal;
                    else if (tIdx == 1) _overlayShowCPU = newVal;
                    else if (tIdx == 2) _overlayShowRAM = newVal;
                    else if (tIdx == 3) _overlayShowGPU = newVal;
                    else if (tIdx == 4) _overlayShowTemp = newVal;
                    else if (tIdx == 5) _overlayShowClock = newVal;
                    if (_overlayWindow != null) _overlayWindow.SetMetricVisible(tIdx, newVal);
                    SaveOverlaySettings();
                };
                Grid.SetRow(tBtn, ti / 2); Grid.SetColumn(tBtn, ti % 2); toggleGrid.Children.Add(tBtn);
            }
            custInner.Children.Add(toggleGrid);

            var overlayColorLabel = MakeText("لون النص", 12, Colors.White, true);
            overlayColorLabel.Margin = new Thickness(0, 6, 0, 4); custInner.Children.Add(overlayColorLabel);
            Color[] overlayColors = new[] { Colors.White, Theme.Accent, Theme.Green, Theme.Warning, Theme.Pink };
            var ovColorGrid = new Grid();
            for (int oi = 0; oi < 5; oi++) ovColorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int oi = 0; oi < overlayColors.Length; oi++)
            {
                int oIdx = oi;
                var dot = new Ellipse { Width = 22, Height = 22, Fill = new SolidColorBrush(overlayColors[oi]), Margin = new Thickness(2), Cursor = Cursors.Hand, StrokeThickness = 2 };
                dot.Stroke = new SolidColorBrush(Colors.Transparent);
                if (overlayColors[oi].R == _overlayTextColor.R && overlayColors[oi].G == _overlayTextColor.G && overlayColors[oi].B == _overlayTextColor.B)
                    dot.Stroke = new SolidColorBrush(Colors.White);
                dot.MouseDown += (s5, e5) =>
                {
                    foreach (var child in ovColorGrid.Children) { var e = child as Ellipse; if (e != null) e.Stroke = new SolidColorBrush(Colors.Transparent); }
                    dot.Stroke = new SolidColorBrush(Colors.White);
                    _overlayTextColor = overlayColors[oIdx];
                    if (_overlayWindow != null) _overlayWindow.SetTextTint(_overlayTextColor);
                    SaveOverlaySettings();
                };
                Grid.SetColumn(dot, oi); ovColorGrid.Children.Add(dot);
            }
            custInner.Children.Add(ovColorGrid);

            custCard.Child = custInner; stack.Children.Add(custCard);

            // Overlay position control inline
            var posCard = MakeCard(18, null); var posInner = new StackPanel { Margin = new Thickness(20) };
            posInner.Children.Add(MakeText("موضع التراكب", 16, Colors.White, true));
            var posGrid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            for (int pc = 0; pc < 2; pc++) posGrid.ColumnDefinitions.Add(new ColumnDefinition());
            for (int pr = 0; pr < 2; pr++) posGrid.RowDefinitions.Add(new RowDefinition());
            string[] posLabels = new[] { "أعلى يمين", "أعلى يسار", "أسفل يمين", "أسفل يسار" };
            OverlayPosition[] posVals = new[] { OverlayPosition.TopRight, OverlayPosition.TopLeft, OverlayPosition.BottomRight, OverlayPosition.BottomLeft };
            for (int pi = 0; pi < 4; pi++)
            {
                int pIdx = pi;
                var pBtn = new Border
                {
                    Background = new SolidColorBrush(_overlayPosition == posVals[pi] ? Color.FromArgb(60, _accentColor.R, _accentColor.G, _accentColor.B) : Theme.CardClr),
                    CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(3), Cursor = Cursors.Hand,
                    Width = 120, HorizontalAlignment = HorizontalAlignment.Center
                };
                pBtn.Child = MakeText(posLabels[pi], 11, Colors.White, true);
                pBtn.MouseDown += (s6, e6) =>
                {
                    _overlayPosition = posVals[pIdx];
                    if (_overlayWindow != null) _overlayWindow.SetPosition(_overlayPosition);
                    SaveOverlaySettings();
                    foreach (var child in posGrid.Children) { var b = child as Border; if (b != null) b.Background = new SolidColorBrush(Theme.CardClr); }
                    pBtn.Background = new SolidColorBrush(Color.FromArgb(60, _accentColor.R, _accentColor.G, _accentColor.B));
                };
                Grid.SetRow(pBtn, pi / 2); Grid.SetColumn(pBtn, pi % 2); posGrid.Children.Add(pBtn);
            }
            posInner.Children.Add(posGrid);
            posCard.Child = posInner; stack.Children.Add(posCard);

            // Auto-Pilot card
            var pilotCard = MakeCard(18, null); var pilotInner = new StackPanel { Margin = new Thickness(20) };
            pilotInner.Children.Add(MakeText("الطيار الآلي", 16, Colors.White, true));
            var _pd = MakeText("يكتشف الألعاب تلقائياً وينفذ Boost كامل", 11, Color.FromArgb(136, 255, 255, 255), false); _pd.Margin = new Thickness(0, 4, 0, 10); pilotInner.Children.Add(_pd);
            var pilotBtn = new Border { Background = new SolidColorBrush(Color.FromArgb(24, _accentColor.R, _accentColor.G, _accentColor.B)), CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 8, 16, 8), Width = 140, Cursor = Cursors.Hand };
            _autoPilotStatus = MakeText(_autoPilot ? "مراقب" : "معطل", 12, _accentColor, true); _autoPilotStatus.HorizontalAlignment = HorizontalAlignment.Center;
            pilotBtn.Child = _autoPilotStatus;
            pilotBtn.MouseDown += (sp, ep) =>
            {
                _autoPilot = !_autoPilot;
                UpdateAutoPilotUI();
            };
            pilotInner.Children.Add(pilotBtn);
            pilotCard.Child = pilotInner; stack.Children.Add(pilotCard);

            // FPS Limiter card
            var fpsCard = MakeCard(18, null); var fpsInner = new StackPanel { Margin = new Thickness(20) };
            fpsInner.Children.Add(MakeText("محدد الإطارات", 16, Colors.White, true));
            var _fpsd = MakeText("تحديد عدد الإطارات في الثانية للألعاب", 11, Color.FromArgb(136, 255, 255, 255), false); _fpsd.Margin = new Thickness(0, 4, 0, 10); fpsInner.Children.Add(_fpsd);
            var fpsGrid = new Grid(); fpsGrid.ColumnDefinitions.Add(new ColumnDefinition()); fpsGrid.ColumnDefinitions.Add(new ColumnDefinition()); fpsGrid.ColumnDefinitions.Add(new ColumnDefinition());
            fpsGrid.RowDefinitions.Add(new RowDefinition()); fpsGrid.RowDefinitions.Add(new RowDefinition());
            int[] fpsLimits = new[] { 30, 60, 90, 120, 144, 0 };
            string[] fpsLabels = new[] { "30 FPS", "60 FPS", "90 FPS", "120 FPS", "144 FPS", "غير محدود" };
            var _currentFpsLock = 0;
            var fpsStatus = MakeText("الحالة: غير محدود", 11, Color.FromArgb(160, 255, 255, 255), false); fpsInner.Children.Add(fpsStatus);
            for (int fi = 0; fi < fpsLimits.Length; fi++)
            {
                int fIdx = fi;
                var fBtn = new Border { Background = new SolidColorBrush(Theme.CardClr), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(3), Cursor = Cursors.Hand, Width = 90, HorizontalAlignment = HorizontalAlignment.Center };
                fBtn.Child = MakeText(fpsLabels[fi], 11, Colors.White, true);
                fBtn.MouseDown += (sf, ef) =>
                {
                    try
                    {
                        int limit = fpsLimits[fIdx];
                        if (limit > 0)
                        {
                            var ok = MessageBox.Show(string.Format("سيتم تحديد عدد الإطارات بـ {0} FPS عبر سجل NVIDIA/DirectX.\n\nمتابعة؟", limit), "محدد الإطارات",
                                MessageBoxButton.YesNo, MessageBoxImage.Question);
                            if (ok != MessageBoxResult.Yes) return;
                            // Capture original values once before the first apply so unlock can restore them
                            if (!_fpsLockBackedUp)
                            {
                                try
                                {
                                    using (var rkB = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\NVIDIA Corporation\Global\NVTweak", false))
                                    {
                                        if (rkB != null)
                                        {
                                            _hadNvMaxFrameRate = rkB.GetValue("MaxFrameRate", null, RegistryValueOptions.None) != null;
                                            if (_hadNvMaxFrameRate) { _prevNvMaxFrameRate = rkB.GetValue("MaxFrameRate", null, RegistryValueOptions.None); try { _nvMaxFrameRateKind = rkB.GetValueKind("MaxFrameRate"); } catch (Exception ex15) { CWLog.Error("MainWindow.Pages", ex15); } }
                                        }
                                    }
                                } catch (Exception ex16) { CWLog.Error("MainWindow.Pages", ex16); }
                                try
                                {
                                    using (var rkB2 = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\DirectX\GraphicsSettings", false))
                                    {
                                        if (rkB2 != null)
                                        {
                                            _hadD3DFrameRateLimit = rkB2.GetValue("D3D11FrameRateLimit", null, RegistryValueOptions.None) != null;
                                            if (_hadD3DFrameRateLimit) { _prevD3DFrameRateLimit = rkB2.GetValue("D3D11FrameRateLimit", null, RegistryValueOptions.None); try { _d3DFrameRateKind = rkB2.GetValueKind("D3D11FrameRateLimit"); } catch (Exception ex17) { CWLog.Error("MainWindow.Pages", ex17); } }
                                        }
                                    }
                                } catch (Exception ex18) { CWLog.Error("MainWindow.Pages", ex18); }
                                _fpsLockBackedUp = true;
                            }
                            _currentFpsLock = limit;
                            // Apply FPS limit via NVIDIA registry (works for NVIDIA GPUs)
                            try
                            {
                                using (var rk = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\NVIDIA Corporation\Global\NVTweak", true))
                                {
                                    rk.SetValue("MaxFrameRate", limit, RegistryValueKind.DWord);
                                }
                            } catch (Exception ex19) { CWLog.Error("MainWindow.Pages", ex19); }
                            // Apply via DWM
                            try
                            {
                                using (var rk2 = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\DirectX\GraphicsSettings", true))
                                {
                                    rk2.SetValue("D3D11FrameRateLimit", limit, RegistryValueKind.DWord);
                                }
                            } catch (Exception ex20) { CWLog.Error("MainWindow.Pages", ex20); }
                            fpsStatus.Text = string.Format("الحالة: {0} FPS", limit);
                            MessageBox.Show(string.Format("تم تحديد {0}", fpsLabels[fIdx]), "محدد الإطارات");
                        }
                        else
                        {
                            var ok = MessageBox.Show("إلغاء تحديد الإطارات واستعادة القيم السابقة؟", "محدد الإطارات",
                                MessageBoxButton.YesNo, MessageBoxImage.Question);
                            if (ok != MessageBoxResult.Yes) return;
                            _currentFpsLock = 0;
                            if (_fpsLockBackedUp)
                            {
                                // Restore original values (or delete if they did not exist)
                                try
                                {
                                    using (var rkR = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\NVIDIA Corporation\Global\NVTweak", true))
                                    {
                                        if (_hadNvMaxFrameRate && _prevNvMaxFrameRate != null)
                                            rkR.SetValue("MaxFrameRate", _prevNvMaxFrameRate, _nvMaxFrameRateKind);
                                        else
                                            rkR.DeleteValue("MaxFrameRate", false);
                                    }
                                } catch (Exception ex21) { CWLog.Error("MainWindow.Pages", ex21); }
                                try
                                {
                                    using (var rkR2 = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\DirectX\GraphicsSettings", true))
                                    {
                                        if (_hadD3DFrameRateLimit && _prevD3DFrameRateLimit != null)
                                            rkR2.SetValue("D3D11FrameRateLimit", _prevD3DFrameRateLimit, _d3DFrameRateKind);
                                        else
                                            rkR2.DeleteValue("D3D11FrameRateLimit", false);
                                    }
                                } catch (Exception ex22) { CWLog.Error("MainWindow.Pages", ex22); }
                                _fpsLockBackedUp = false;
                            }
                            else
                            {
                                try { using (var rk3 = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\NVIDIA Corporation\Global\NVTweak", true)) { if (rk3 != null) { rk3.DeleteValue("MaxFrameRate"); } } } catch (Exception ex23) { CWLog.Error("MainWindow.Pages", ex23); }
                                try { using (var rk4 = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\DirectX\GraphicsSettings", true)) { if (rk4 != null) { rk4.DeleteValue("D3D11FrameRateLimit"); } } } catch (Exception ex24) { CWLog.Error("MainWindow.Pages", ex24); }
                            }
                            fpsStatus.Text = "الحالة: غير محدود";
                            MessageBox.Show("تم إلغاء التحديد", "محدد الإطارات");
                        }
                    }
                    catch { MessageBox.Show("فشل تطبيق التحديد", "خطأ"); }
                };
                Grid.SetRow(fBtn, fi / 3); Grid.SetColumn(fBtn, fi % 3); fpsGrid.Children.Add(fBtn);
            }
            fpsInner.Children.Add(fpsGrid);
            fpsCard.Child = fpsInner; stack.Children.Add(fpsCard);

            // One-Click Optimize button
            var optBtn = new Border { Background = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1), GradientStops = { new GradientStop(Theme.Accent, 0), new GradientStop(Theme.Purple, 1) } }, CornerRadius = new CornerRadius(14), Padding = new Thickness(24, 16, 24, 16), Margin = new Thickness(0, 0, 0, 16), Cursor = Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Stretch, BorderBrush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0), GradientStops = { new GradientStop(Color.FromArgb(80, 47, 139, 255), 0), new GradientStop(Color.FromArgb(60, 139, 92, 246), 1) } }, BorderThickness = new Thickness(1), Effect = new DropShadowEffect { Color = Theme.Accent, BlurRadius = 24, ShadowDepth = 0, Opacity = 0.35 } };
            var optBtnStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            optBtnStack.Children.Add(MakeText("\u2728 ", 16, Colors.White, false));
            optBtnStack.Children.Add(MakeText("تحسين سريع — كل شيء", 15, Colors.White, true));
            optBtn.Child = optBtnStack;
            AddHoverLift(optBtn);
            optBtn.MouseDown += (sopt, eopt) =>
            {
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        OptimizeRAM(); PurgeStandbyList();
                        try { var fg = GetForegroundWindow(); uint pid; GetWindowThreadProcessId(fg, out pid); if (pid > 0) { using (var pp = Process.GetProcessById((int)pid)) { pp.PriorityClass = ProcessPriorityClass.High; } } } catch (Exception ex25) { CWLog.Error("MainWindow.Pages", ex25); }
                        try { using (Process.Start(new ProcessStartInfo("powercfg.exe", "/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c") { CreateNoWindow = true, UseShellExecute = false })) { } } catch (Exception ex26) { CWLog.Error("MainWindow.Pages", ex26); }
                        string dnsMsgOpt;
                        bool dnsOkOpt = TryFlushDnsVerified(out dnsMsgOpt);
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (_agent != null && _agent.Mode == AgentMode.Off) _agent.SetMode(AgentMode.BalanceMode);
                            MessageBox.Show("تم التحسين بنجاح!\n- تنظيف RAM + Standby\n- ترقية أولوية العملية الأمامية\n- خطة الطاقة الأعلى\n- " + (dnsOkOpt ? "فلترة DNS (تم التحقق)" : ("تعذر فلترة DNS: " + dnsMsgOpt)) + "\n- AI Agent: Balanced", "✅ ClearWin Optimize");
                        }));
                    }
                    catch
                    {
                        Dispatcher.BeginInvoke(new Action(() => MessageBox.Show("حدث خطأ أثناء التحسين", "خطأ")));
                    }
                });
            };
            stack.Children.Add(optBtn);

            // Quick Actions card
            var qaCard = MakeCard(18, null); var qaInner = new StackPanel { Margin = new Thickness(20) };
            qaInner.Children.Add(MakeText("إجراءات سريعة", 16, Colors.White, true));
            var _qad = MakeText("أدوات سريعة للتحكم في أداء اللعبة", 11, Color.FromArgb(136, 255, 255, 255), false); _qad.Margin = new Thickness(0, 4, 0, 10); qaInner.Children.Add(_qad);
            var qaGrid = new Grid();
            qaGrid.ColumnDefinitions.Add(new ColumnDefinition()); qaGrid.ColumnDefinitions.Add(new ColumnDefinition()); qaGrid.ColumnDefinitions.Add(new ColumnDefinition());
            string[][] qa = new string[][] {
                new[] {"تنظيف RAM", "34,197,94"},
                new[] {"Boost", "59,130,246"},
                new[] {"فلترة DNS", "139,92,246"}
            };
            for (int qi = 0; qi < 3; qi++)
            {
                int qIdx = qi;
                var qpts = qa[qi][1].Split(','); var qc = Color.FromRgb(byte.Parse(qpts[0]), byte.Parse(qpts[1]), byte.Parse(qpts[2]));
                var qBtn = new Border { Background = new SolidColorBrush(Theme.CardClr), CornerRadius = new CornerRadius(10), Margin = new Thickness(3), Padding = new Thickness(10), Cursor = Cursors.Hand };
                qBtn.Child = MakeText(qa[qi][0], 12, qc, true);
                qBtn.MouseDown += (sq, eq) =>
                {
                    if (qIdx == 0) { OptimizeRAM(); PurgeStandbyList(); MessageBox.Show("تم تنظيف RAM!", "إجراء سريع"); }
                    else if (qIdx == 1)
                    {
                        OptimizeRAM(); PurgeStandbyList();
                        try { var fg = GetForegroundWindow(); uint pid; GetWindowThreadProcessId(fg, out pid); if (pid > 0) { using (var pp = Process.GetProcessById((int)pid)) { pp.PriorityClass = ProcessPriorityClass.High; } } } catch (Exception ex28) { CWLog.Error("MainWindow.Pages", ex28); }
                        try { using (Process.Start(new ProcessStartInfo("powercfg.exe", "/setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c") { CreateNoWindow = true, UseShellExecute = false })) { } } catch (Exception ex29) { CWLog.Error("MainWindow.Pages", ex29); }
                        MessageBox.Show("تم تطبيق Boost!", "إجراء سريع");
                    }
                    else if (qIdx == 2)
                    {
                        System.Threading.Tasks.Task.Run(() =>
                        {
                            string dnsMsgQa;
                            bool dnsOkQa = TryFlushDnsVerified(out dnsMsgQa);
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                MessageBox.Show(dnsOkQa ? "تم فلترة DNS!" : ("فشل فلترة DNS: " + dnsMsgQa), "إجراء سريع");
                            }));
                        });
                    }
                };
                Grid.SetColumn(qBtn, qi); qaGrid.Children.Add(qBtn);
            }
            qaInner.Children.Add(qaGrid);
            qaCard.Child = qaInner; stack.Children.Add(qaCard);

            return root;
        }

        private UIElement BuildStartupPage()
        {
            var stack = new StackPanel();
            stack.Children.Add(MakeText("مدير بدء التشغيل", 28, Colors.White, true));
            var _ss = MakeText("إدارة البرامج التي تعمل عند بدء التشغيل", 13, Color.FromArgb(136, 255, 255, 255), false); _ss.Margin = new Thickness(0, 0, 0, 16); stack.Children.Add(_ss);

            var card = MakeCard(18, null);
            var innerStack = new StackPanel { Margin = new Thickness(20) };
            _startupList = new ListBox
            {
                Height = 350,
                Background = new SolidColorBrush(Theme.CardClr),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0)
            };
            innerStack.Children.Add(_startupList);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            var refreshBtn = new Border { Background = new SolidColorBrush(Color.FromArgb(32, _accentColor.R, _accentColor.G, _accentColor.B)), CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 10, 0), Cursor = Cursors.Hand };
            refreshBtn.Child = MakeText("تحديث", 12, Theme.Accent, true);
            refreshBtn.MouseDown += (s2, e2) => LoadStartupItems();
            btnRow.Children.Add(refreshBtn);

            var disableBtn = new Border { Background = new SolidColorBrush(Theme.Error), CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 8, 14, 8), Cursor = Cursors.Hand };
            disableBtn.Child = MakeText("تعطيل المحدد", 12, Colors.White, true);
            disableBtn.MouseDown += (s2, e2) => DisableStartupItem();
            btnRow.Children.Add(disableBtn);

            var restoreBtn = new Border { Background = new SolidColorBrush(Color.FromArgb(32, 92, 216, 139)), CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(10, 0, 0, 0), Cursor = Cursors.Hand };
            restoreBtn.Child = MakeText("استعادة المحفوظات", 12, Color.FromArgb(230, 92, 216, 139), true);
            restoreBtn.MouseDown += (s3, e3) => RestoreStartupItems();
            btnRow.Children.Add(restoreBtn);

            innerStack.Children.Add(btnRow);
            card.Child = innerStack;
            stack.Children.Add(card);
            LoadStartupItems();
            return stack;
        }

        private void LoadStartupItems()
        {
            if (_startupList == null) return;
            _startupList.Items.Clear();
            try
            {
                var items = StartupClassifier.EnumerateStartupItems();
                foreach (var it in items)
                {
                    string label;
                    if (it.Recommendation == StartupRecommendation.SafeToDisable) label = "آمن للإيقاف";
                    else if (it.Recommendation == StartupRecommendation.KeepEssential) label = "أساسي";
                    else label = "غير معروف";
                    _startupList.Items.Add(string.Format("[{0}][{1}]|{2}", it.Hive, label, it.Name));
                }
            }
            catch { _startupList.Items.Add("(خطأ في قراءة السجل - شغّل كمسؤول)"); }
            if (_startupList.Items.Count == 0) _startupList.Items.Add("(لا توجد عناصر بدء تشغيل)");
        }

        private void DisableStartupItem()
        {
            var sel = _startupList.SelectedItem as string;
            if (sel == null || !sel.Contains("|")) { MessageBox.Show("اختر عنصراً أولاً", "ClearWin"); return; }
            try
            {
                string hive = sel.StartsWith("[HKLM]") ? "HKLM" : "HKCU";
                string name = sel.Substring(sel.IndexOf('|') + 1).Trim();
                if (StartupClassifier.ExecuteStartupDisable(hive, name, null))
                {
                    LoadStartupItems();
                    MessageBox.Show(string.Format("تم إيقاف: {0} (مع نسخة احتياطية)", name), "ClearWin");
                }
                else
                {
                    MessageBox.Show("تعذر إيقاف العنصر (غير موجود أو صلاحيات)", "ClearWin");
                }
            }
            catch (Exception ex) { MessageBox.Show("خطأ: " + ex.Message, "ClearWin"); }
        }

        private void RestoreStartupItems()
        {
            try
            {
                int restored = 0;
                using (var root = Registry.CurrentUser.OpenSubKey(@"Software\ClearWin\StartupBackup", false))
                {
                    if (root == null) { MessageBox.Show("لا توجد عناصر محفوظة", "ClearWin"); return; }
                    foreach (var hive in root.GetSubKeyNames())
                    {
                        using (var hiveKey = root.OpenSubKey(hive, false))
                        {
                            if (hiveKey == null) continue;
                            foreach (var n in hiveKey.GetValueNames())
                            {
                                if (StartupClassifier.ExecuteStartupRestore(hive, n)) restored++;
                            }
                        }
                    }
                }
                LoadStartupItems();
                MessageBox.Show(string.Format("تمت استعادة {0} عنصراً", restored), "ClearWin");
            }
            catch (Exception ex) { MessageBox.Show("خطأ: " + ex.Message, "ClearWin"); }
        }

        private UIElement BuildProcessesPage()
        {
            var stack = new StackPanel();
            stack.Children.Add(MakeText("مدير العمليات", 28, Colors.White, true));
            var _ps = MakeText("عرض وإدارة العمليات الجارية", 13, Color.FromArgb(136, 255, 255, 255), false); _ps.Margin = new Thickness(0, 0, 0, 16); stack.Children.Add(_ps);

            var card = MakeCard(18, null); var innerStack = new StackPanel { Margin = new Thickness(20) };

            var filterRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            filterRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            filterRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _processFilter = new TextBox
            {
                Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromArgb(64, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 6, 10, 6),
                FontSize = 12,
                Text = ""
            };
            var ph = MakeText("  بحث عن عملية...", 12, Color.FromArgb(119, 255, 255, 255), false);
            ph.VerticalAlignment = VerticalAlignment.Center; ph.IsHitTestVisible = false;
            var filterBorder = new Border { Child = _processFilter };
            var phLayer = new Grid(); phLayer.Children.Add(filterBorder); phLayer.Children.Add(ph);
            Grid.SetColumn(phLayer, 0); filterRow.Children.Add(phLayer);
            _processFilter.TextChanged += (s4, e4) => { ph.Visibility = _processFilter.Text == "" ? Visibility.Visible : Visibility.Collapsed; LoadProcesses(); };

            var autoRefBtn = new Border { Background = new SolidColorBrush(_procAutoRefreshActive ? Color.FromArgb(64, 139, 92, 246) : Color.FromArgb(32, 139, 92, 246)), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(6, 0, 0, 0), Cursor = Cursors.Hand };
            var autoRefTxt = MakeText(_procAutoRefreshActive ? "تلقائي: مفعل" : "تلقائي: معطل", 11, _procAutoRefreshActive ? Colors.White : Theme.Purple, true);
            autoRefBtn.Child = autoRefTxt;
            autoRefBtn.MouseDown += (s5, e5) =>
            {
                _procAutoRefreshActive = !_procAutoRefreshActive;
                autoRefBtn.Background = new SolidColorBrush(_procAutoRefreshActive ? Color.FromArgb(64, 139, 92, 246) : Color.FromArgb(32, 139, 92, 246));
                autoRefTxt.Text = _procAutoRefreshActive ? "تلقائي: مفعل" : "تلقائي: معطل";
                autoRefTxt.Foreground = new SolidColorBrush(_procAutoRefreshActive ? Colors.White : Theme.Purple);
            };
            Grid.SetColumn(autoRefBtn, 1); filterRow.Children.Add(autoRefBtn);
            innerStack.Children.Add(filterRow);

            _processList = new ListBox { Height = 340, Background = new SolidColorBrush(Theme.CardClr), Foreground = new SolidColorBrush(Colors.White), BorderThickness = new Thickness(0) };
            _processList.MouseDoubleClick += (s2, e2) =>
            {
                var sel = _processList.SelectedItem as string;
                if (sel == null) return;
                var m = System.Text.RegularExpressions.Regex.Match(sel, @"PID:(\d+)");
                if (m.Success)
                {
                    int pid = int.Parse(m.Groups[1].Value);
                    try
                    {
                        using (var p = Process.GetProcessById(pid))
                        {
                            string info = string.Format("الاسم: {0}\nPID: {1}\nالذاكرة: {2:N0} KB\nالخيوط: {3}\nأولوية: {4}\nوقت المعالج: {5}\nبدء: {6}",
                                p.ProcessName, p.Id, p.PrivateMemorySize64 / 1024, p.Threads.Count, p.PriorityClass,
                                p.TotalProcessorTime.ToString(@"mm\:ss"), p.StartTime.ToString("yyyy/MM/dd HH:mm"));
                            MessageBox.Show(info, "تفاصيل العملية", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    catch { MessageBox.Show("العملية غير متاحة", "خطأ"); }
                }
            };
            innerStack.Children.Add(_processList);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            var refBtn = new Border { Background = new SolidColorBrush(Color.FromArgb(32, _accentColor.R, _accentColor.G, _accentColor.B)), CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 10, 0), Cursor = Cursors.Hand };
            refBtn.Child = MakeText("تحديث", 12, _accentColor, true);
            refBtn.MouseDown += (s2, e2) => LoadProcesses();
            btnRow.Children.Add(refBtn);

            var killBtn = new Border { Background = new SolidColorBrush(Theme.Error), CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 8, 14, 8), Cursor = Cursors.Hand };
            killBtn.Child = MakeText("إنهاء العملية", 12, Colors.White, true);
            killBtn.MouseDown += (s2, e2) => KillProcess();
            btnRow.Children.Add(killBtn);

            innerStack.Children.Add(btnRow); card.Child = innerStack; stack.Children.Add(card);
            LoadProcesses();
            return stack;
        }

        private void LoadProcesses()
        {
            if (_processList == null) return;
            string filter = _processFilter != null ? _processFilter.Text.Trim().ToLower() : "";
            _processList.Items.Clear();
            try
            {
                var procs = Process.GetProcesses();
                try
                {
                    foreach (var p in procs)
                    {
                        try
                        {
                            if (filter == "" || p.ProcessName.ToLower().Contains(filter))
                                _processList.Items.Add(string.Format("{0,-30} PID:{1,7}  Mem:{2,8} KB", p.ProcessName, p.Id, p.PrivateMemorySize64 / 1024));
                        } catch (Exception ex31) { CWLog.Error("MainWindow.Pages", ex31); }
                    }
                }
                finally { foreach (var p in procs) { try { p.Dispose(); } catch (Exception ex32) { CWLog.Error("MainWindow.Pages", ex32); } } }
            }
            catch { _processList.Items.Add("(Error reading processes)"); }
        }

        private void KillProcess()
        {
            var sel = _processList.SelectedItem as string;
            if (sel == null) { MessageBox.Show("اختر عملية أولاً", "ClearWin"); return; }
            try
            {
                int pid = 0;
                var match = System.Text.RegularExpressions.Regex.Match(sel, @"PID:(\d+)");
                if (match.Success) pid = int.Parse(match.Groups[1].Value);
                if (pid > 0) { using (var pp = Process.GetProcessById(pid)) { pp.Kill(); } MessageBox.Show("تم إنهاء العملية", "ClearWin"); LoadProcesses(); }
            }
            catch (Exception ex) { MessageBox.Show("خطأ: " + ex.Message, "ClearWin"); }
        }

        private UIElement BuildNetworkPage()
        {
            var stack = new StackPanel();
            stack.Children.Add(MakeText("معلومات الشبكة", 28, Colors.White, true));
            var _ns = MakeText("عرض محولات الشبكة وحل المشكلات", 13, Color.FromArgb(136, 255, 255, 255), false); _ns.Margin = new Thickness(0, 0, 0, 16); stack.Children.Add(_ns);

            var card = MakeCard(18, null); var innerStack = new StackPanel { Margin = new Thickness(20) };
            _netInfo = MakeText("جارٍ التحميل...", 13, Color.FromArgb(204, 255, 255, 255), false);
            _netInfo.TextWrapping = TextWrapping.Wrap;
            _netInfo.MaxHeight = 300;
            innerStack.Children.Add(_netInfo);

            var btnRow = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
            var refreshNet = new Border { Background = new SolidColorBrush(Color.FromArgb(32, _accentColor.R, _accentColor.G, _accentColor.B)), CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 10, 0), Cursor = Cursors.Hand };
            refreshNet.Child = MakeText("تحديث", 12, Theme.Accent, true);
            refreshNet.MouseDown += (s2, e2) => LoadNetworkInfo();
            btnRow.Children.Add(refreshNet);

            var flushBtn = new Border { Background = new SolidColorBrush(Theme.Accent), CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 10, 0), Cursor = Cursors.Hand };
            flushBtn.Child = MakeText("مسح DNS", 12, Colors.White, true);
            flushBtn.MouseDown += (s2, e2) =>
            {
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        string dnsMsgBtn;
                        bool dnsOkBtn = TryFlushDnsVerified(out dnsMsgBtn);
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            MessageBox.Show(dnsOkBtn ? "تم مسح DNS" : ("فشل مسح DNS: " + dnsMsgBtn), "ClearWin");
                        }));
                    }
                    catch (Exception ex33) { CWLog.Error("MainWindow.Pages", ex33); }
                });
            };
            btnRow.Children.Add(flushBtn);

            var netResetBtn = new Border { Background = new SolidColorBrush(Theme.Error), CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 8, 14, 8), Margin = new Thickness(0, 0, 10, 0), Cursor = Cursors.Hand };
            netResetBtn.Child = MakeText("إعادة تعيين الشبكة", 12, Colors.White, true);
            netResetBtn.MouseDown += (s2, e2) =>
            {
                var ok = MessageBox.Show("سيتم إعادة تعيين Winsock و TCP/IP.\nقد ينقطع الاتصال مؤقتاً وقد تحتاج لإعادة تشغيل الجهاز.\n\nهل تريد المتابعة؟", "إعادة تعيين الشبكة",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (ok != MessageBoxResult.Yes) return;
                System.Threading.Tasks.Task.Run(() =>
                {
                    string dnsMsgReset; TryFlushDnsVerified(out dnsMsgReset);
                    try { using (Process.Start(new ProcessStartInfo { FileName = "netsh.exe", Arguments = "winsock reset", CreateNoWindow = true, UseShellExecute = false })) { } } catch (Exception ex35) { CWLog.Error("MainWindow.Pages", ex35); }
                    try { using (Process.Start(new ProcessStartInfo { FileName = "netsh.exe", Arguments = "int ip reset", CreateNoWindow = true, UseShellExecute = false })) { } } catch (Exception ex36) { CWLog.Error("MainWindow.Pages", ex36); }
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        MessageBox.Show("اكتملت إعادة التعيين.\nأعد تشغيل الجهاز لتطبيق الإعدادات بالكامل.", "إعادة تعيين الشبكة");
                    }));
                });
            };
            btnRow.Children.Add(netResetBtn);

            var pingBtn = new Border { Background = new SolidColorBrush(Theme.Purple), CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 8, 14, 8), Cursor = Cursors.Hand };
            pingBtn.Child = MakeText("اختبار الاتصال بقوقل", 12, Colors.White, true);
            pingBtn.MouseDown += (s2, e2) => { try { var p = new Ping(); var r = p.Send("8.8.8.8", 3000); MessageBox.Show(string.Format("Google: {0}ms ({1})", r.RoundtripTime, r.Status), "ClearWin"); } catch (Exception ex) { MessageBox.Show("فشل الاتصال: " + ex.Message, "ClearWin"); } };
            btnRow.Children.Add(pingBtn);

            innerStack.Children.Add(btnRow); card.Child = innerStack; stack.Children.Add(card);
            LoadNetworkInfo();
            return stack;
        }

        private void LoadNetworkInfo()
        {
            if (_netInfo == null) return;
            try
            {
                var sb = new StringBuilder();
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up)
                    {
                        var ip = ni.GetIPProperties();
                        sb.AppendLine(string.Format("[{0}] {1}", ni.NetworkInterfaceType, ni.Name));
                        if (ip.UnicastAddresses.Count > 0)
                            sb.AppendLine(string.Format("  IP: {0}", ip.UnicastAddresses[0].Address));
                        if (ip.GatewayAddresses.Count > 0)
                            sb.AppendLine(string.Format("  Gateway: {0}", ip.GatewayAddresses[0].Address));
                        sb.AppendLine(string.Format("  Speed: {0} Mbps", ni.Speed / 1000000));
                        sb.AppendLine();
                    }
                }
                _netInfo.Text = sb.Length > 0 ? sb.ToString() : "لا توجد اتصالات شبكة نشطة";
            }
            catch (Exception ex) { _netInfo.Text = "خطأ: " + ex.Message; }
        }

        private UIElement BuildDiskPage()
        {
            var stack = new StackPanel();
            stack.Children.Add(MakeText("تحليل الأقراص", 28, Colors.White, true));
            var _ds = MakeText("تحليل استخدام مساحة الأقراص", 13, Color.FromArgb(136, 255, 255, 255), false); _ds.Margin = new Thickness(0, 0, 0, 16); stack.Children.Add(_ds);

            var card = MakeCard(18, null); var innerStack = new StackPanel { Margin = new Thickness(20) };
            _diskBarFills.Clear(); _diskSizeTxts.Clear(); _diskFreeTxts.Clear();

            foreach (var drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady) continue;
                var driveCard = MakeCard(12, null); driveCard.Margin = new Thickness(0, 4, 0, 4);
                var driveStack = new StackPanel { Margin = new Thickness(14, 10, 14, 10) };
                double totalGB = drive.TotalSize / 1073741824.0;
                double freeGB = drive.AvailableFreeSpace / 1073741824.0;
                double usedGB = totalGB - freeGB;
                double pct = (usedGB / totalGB) * 100;
                var sizeTxt = MakeText(string.Format("{0} ({1}) - {2:F1} GB / {3:F1} GB ({4:F0}%)", drive.Name, drive.VolumeLabel, usedGB, totalGB, pct), 13, Colors.White, true);
                driveStack.Children.Add(sizeTxt);
                var barBg = new Border { Height = 8, CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(Theme.CardClr), Margin = new Thickness(0, 6, 0, 0) };
                var barFill = new Border { Width = pct * 2.5, Height = 8, CornerRadius = new CornerRadius(4), Background = new LinearGradientBrush(_accentColor, Theme.Purple, 0), HorizontalAlignment = HorizontalAlignment.Left };
                barBg.Child = barFill; driveStack.Children.Add(barBg);
                var freeTxt = MakeText(string.Format("حر: {0:F1} جيجابايت", freeGB), 11, Color.FromArgb(136, 255, 255, 255), false);
                driveStack.Children.Add(freeTxt);
                driveCard.Child = driveStack; innerStack.Children.Add(driveCard);
                _diskBarFills.Add(barFill); _diskSizeTxts.Add(sizeTxt); _diskFreeTxts.Add(freeTxt);
            }

            var toolRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            var cleanBtn = new Border { Background = new LinearGradientBrush(_accentColor, Theme.Purple, 0), CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 8, 16, 8), Cursor = Cursors.Hand };
            cleanBtn.Child = MakeText("تشغيل تنظيف القرص", 12, Colors.White, true);
            cleanBtn.MouseDown += (s2, e2) => { try { using (Process.Start("cleanmgr.exe")) { } } catch (Exception ex37) { CWLog.Error("MainWindow.Pages", ex37); } };
            toolRow.Children.Add(cleanBtn);

            var defragBtn = new Border { Background = new SolidColorBrush(Color.FromArgb(32, _accentColor.R, _accentColor.G, _accentColor.B)), CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 8, 16, 8), Margin = new Thickness(8, 0, 0, 0), Cursor = Cursors.Hand };
            defragBtn.Child = MakeText("تحسين الأقراص", 12, _accentColor, true);
            defragBtn.MouseDown += (s2, e2) => { try { using (Process.Start("dfrgui.exe")) { } } catch (Exception ex38) { CWLog.Error("MainWindow.Pages", ex38); } };
            toolRow.Children.Add(defragBtn);

            var advCleanBtn = new Border { Background = new SolidColorBrush(Color.FromArgb(32, 236, 72, 153)), CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 8, 16, 8), Margin = new Thickness(8, 0, 0, 0), Cursor = Cursors.Hand };
                advCleanBtn.Child = MakeText("تنظيف عميق", 12, Theme.Pink, true);
            advCleanBtn.MouseDown += (s2, e2) => { AdvancedDiskClean(); };
            toolRow.Children.Add(advCleanBtn);

            innerStack.Children.Add(toolRow);
            card.Child = innerStack; stack.Children.Add(card);
            return stack;
        }

        private UIElement BuildTweaksPage()
        {
            var root = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 0, 0, 20) };
            var stack = new StackPanel();
            root.Content = stack;
            stack.Children.Add(MakeText("تحسينات", 28, Colors.White, true));
            var _ts = MakeText("تحسينات النظام من سباركل", 13, Color.FromArgb(136, 255, 255, 255), false); _ts.Margin = new Thickness(0, 0, 0, 6); stack.Children.Add(_ts);
            var safetyNote = MakeText("قبل أي Apply: ClearWin يحاول إنشاء نقطة استعادة تلقائيًا. تغييرات الأمان لا تُطبّق تلقائيًا.", 11, Theme.Warning, false); safetyNote.Margin = new Thickness(0, 0, 0, 14); stack.Children.Add(safetyNote);

            string[][] tweaks = new string[][] {
                new[] {"ultimate-performance-plan", "أقصى أداء", "تفعيل خطة الطاقة القصوى للحصول على أفضل أداء"},
                new[] {"disable-hpet", "إزالة فرض HPET", "إزالة إجبار platform clock والعودة لاختيار Windows الافتراضي"},
                new[] {"disable-dynamic-ticking", "تعطيل المؤقت الديناميكي", "تعطيل خاصية المؤقت الديناميكي لتقليل زمن الاستجابة"},
                new[] {"disable-core-isolation", "تعطيل عزل النواة", "تعطيل تكامل الذاكرة لتحسين الأداء"},
                new[] {"optimize-network-settings", "تحسين الشبكة", "تحسينات TCP/IP لتقليل زمن الاستجابة وزيادة السرعة"},
                new[] {"optimize-nvidia-settings", "تحسين NVIDIA", "إطفاء GPU في الخلفية وتقليل عتبات الطاقة"},
                new[] {"disable-gamebar", "تعطيل شريط الألعاب", "إزالة شريط الألعاب وتراكب إكس بوكس"},
                new[] {"disable-mouse-acceleration", "تعطيل تسريع الفأرة", "تعطيل تسريع الفأرة للتصويب الدقيق"},
                new[] {"remove-gaming-apps", "إزالة تطبيقات إكس بوكس", "إزالة تطبيق إكس بوكس وشريط الألعاب"},
                new[] {"disable-copilot", "تعطيل كوبايلوت", "إزالة ميزة كوبايلوت من مايكروسوفت"},
                new[] {"disable-defender-rtp", "تعطيل Defender (اختياري)", "تعديل أمني حساس — غير موجود في التحسين التلقائي"},
                new[] {"disable-background-ms-store-apps", "تعطيل خلفية المتجر", "منع تطبيقات المتجر من العمل في الخلفية"},
                new[] {"set-services-to-manual", "الخدمات إلى يدوي", "Sets 150+ services to Manual startup for better perf"},
                new[] {"disable-hibernation", "تعطيل السبات", "توفير مساحة القرص وتسريع الإقلاع"},
                new[] {"disable-fast-startup", "تعطيل بدء التشغيل السريع", "تعطيل الإغلاق الهجين لتحسين الاستقرار"},
                new[] {"debloat-windows", "تنظيف ويندوز", "إزالة الميزات والتطبيقات غير الضرورية"},
                new[] {"remove-onedrive", "إزالة ون درايف", "إزالة ون درايف من النظام"},
                new[] {"align-taskbar-left", "محاذاة الشريط لليسار", "محاذاة شريط المهام إلى الجهة اليسرى"},
                new[] {"enable-dark-mode", "تفعيل الوضع الداكن", "تفعيل الوضع الداكن لويندوز"},
                new[] {"enable-end-task-right-click", "تفعيل إنهاء المهمة", "إضافة خيار إنهاء المهمة لقائمة شريط المهام"},
                new[] {"revert-context-menu", "استعادة القائمة القديمة", "العودة لقائمة السياق لنظام ويندوز 10"},
                new[] {"detailed-bsod", "شاشة الموت المفصلة", "عرض معلومات مفصلة عند شاشة الموت الزرقاء"},
                new[] {"disable-location-tracking", "تعطيل تتبع الموقع", "تعطيل تتبع الموقع في ويندوز"},
                new[] {"disable-lockscreen-tips", "تعطيل نصائح شاشة القفل", "تعطيل النصائح على شاشة القفل"},
                new[] {"disable-wifi-sense", "تعطيل Wi-Fi Sense", "منع مشاركة شبكات الواي فاي مع جهات الاتصال"},
                new[] {"set-time-utc", "ضبط التوقيت UTC", "ضبط وقت النظام على UTC مناسب لأنظمة التشغيل المزدوجة"},
                new[] {"run-disk-cleanup", "تشغيل تنظيف القرص", "Cleans C: drive, removes old Windows update cache"}
            };

            string[] categories = new[] {"الأداء", "الألعاب", "النظام", "الخصوصية", "UI", "أدوات"};
            string[][] catTweaks = new string[][] {
                new[] {"ultimate-performance-plan", "disable-hpet", "disable-dynamic-ticking", "disable-core-isolation", "optimize-network-settings", "optimize-nvidia-settings"},
                new[] {"disable-gamebar", "disable-mouse-acceleration", "remove-gaming-apps"},
                new[] {"disable-copilot", "disable-background-ms-store-apps", "set-services-to-manual", "disable-hibernation", "disable-fast-startup", "debloat-windows", "remove-onedrive", "disable-defender-rtp"},
                new[] {"disable-location-tracking", "disable-lockscreen-tips", "disable-wifi-sense"},
                new[] {"align-taskbar-left", "enable-dark-mode", "enable-end-task-right-click", "revert-context-menu", "detailed-bsod"},
                new[] {"set-time-utc", "run-disk-cleanup"}
            };

            for (int ci = 0; ci < categories.Length; ci++)
            {
                var catHeader = MakeText(categories[ci], 16, Theme.Accent, true);
                catHeader.Margin = new Thickness(0, 12, 0, 8);
                stack.Children.Add(catHeader);
                var wrap = new WrapPanel { Orientation = Orientation.Horizontal };
                foreach (string tn in catTweaks[ci])
                {
                    string tweakName = tn;
                    string displayName = "";
                    string desc = "";
                    foreach (var t in tweaks) { if (t[0] == tweakName) { displayName = t[1]; desc = t[2]; break; } }
                    var card = new Border
                    {
                        Background = new SolidColorBrush(Theme.CardClr),
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(14, 10, 14, 10),
                        Margin = new Thickness(4),
                        Width = 310
                    };
                    var cd = new StackPanel();
                    cd.Children.Add(MakeText(displayName, 13, Colors.White, true));
                    var _dd = MakeText(desc, 10, Color.FromArgb(136, 255, 255, 255), false);
                    _dd.Margin = new Thickness(0, 2, 0, 8); cd.Children.Add(_dd);
                    bool critical = IsCriticalTweak(tweakName);
                    bool deferred = IsUntestedDeferredTweak(tweakName);
                    bool irreversible = IsIrreversibleTweak(tweakName);
                    bool partialUndo = IsPartialUndoTweak(tweakName);
                    var applyBtn = new Border
                    {
                        Background = new SolidColorBrush(deferred
                            ? Color.FromArgb(30, 100, 116, 139)
                            : Color.FromArgb(40, critical ? (byte)220 : (byte)59, critical ? (byte)38 : (byte)130, critical ? (byte)38 : (byte)246)),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(12, 5, 12, 5),
                        Cursor = deferred ? Cursors.No : Cursors.Hand,
                        MinWidth = 80,
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    applyBtn.Child = MakeText(deferred ? "مؤجل (غير مفعّل)" : (critical ? "تطبيق (حساس)" : "تطبيق"), 11, deferred ? Color.FromArgb(160, 255, 255, 255) : Colors.White, true);
                    if (deferred) applyBtn.ToolTip = "مؤجل خارج التفعيل الإنتاجي (NOT TESTED) لحين التحقق منه في بيئة معزولة (VM/Sandbox)";
                    else if (critical) applyBtn.ToolTip = "تعديل حساس — قد يؤثر على أمان أو استقرار النظام";
                    applyBtn.MouseDown += (s3, e3) => RunTweak(tweakName);
                    if (!deferred)
                    {
                        applyBtn.MouseEnter += (s3, e3) => applyBtn.Background = new SolidColorBrush(Color.FromArgb(70, critical ? (byte)220 : (byte)59, critical ? (byte)38 : (byte)130, critical ? (byte)38 : (byte)246));
                        applyBtn.MouseLeave += (s3, e3) => applyBtn.Background = new SolidColorBrush(Color.FromArgb(40, critical ? (byte)220 : (byte)59, critical ? (byte)38 : (byte)130, critical ? (byte)38 : (byte)246));
                    }
                    if (!deferred && !irreversible && System.IO.File.Exists(ResolveTweakScriptPath(tweakName, "undo.ps1")))
                    {
                        var undoBtn = new Border
                        {
                            Background = new SolidColorBrush(Color.FromArgb(40, 148, 163, 184)),
                            CornerRadius = new CornerRadius(6),
                            Padding = new Thickness(12, 5, 12, 5),
                            Cursor = Cursors.Hand,
                            MinWidth = 80,
                            HorizontalAlignment = HorizontalAlignment.Left
                        };
                        undoBtn.Child = MakeText(partialUndo ? "إعادة تثبيت (جزئي)" : "تراجع", 11, Colors.White, true);
                        if (partialUndo) undoBtn.ToolTip = "تراجع جزئي عبر إعادة التثبيت من Winget — لا يستعيد الإعدادات السابقة بالكامل";
                        undoBtn.MouseDown += (s4, e4) => RunUndoTweak(tweakName);
                        undoBtn.MouseEnter += (s4, e4) => undoBtn.Background = new SolidColorBrush(Color.FromArgb(70, 148, 163, 184));
                        undoBtn.MouseLeave += (s4, e4) => undoBtn.Background = new SolidColorBrush(Color.FromArgb(40, 148, 163, 184));
                        var row = new StackPanel { Orientation = Orientation.Horizontal };
                        row.Children.Add(applyBtn);
                        var _sp = new Border { Width = 8 }; row.Children.Add(_sp);
                        row.Children.Add(undoBtn);
                        cd.Children.Add(row);
                    }
                    else
                    {
                        cd.Children.Add(applyBtn);
                    }
                    card.Child = cd;
                    wrap.Children.Add(card);
                }
                stack.Children.Add(wrap);
            }
            return root;
        }

        private string ResolveTweakScriptPath(string name, string scriptFileName)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(scriptFileName))
                return null;

            // Security: Prevent directory traversal (e.g. "..", "/", "\")
            if (name.Contains("..") || name.Contains("/") || name.Contains("\\") ||
                scriptFileName.Contains("..") || scriptFileName.Contains("/") || scriptFileName.Contains("\\"))
            {
                CWLog.Warn("Security.PathTraversal", "Blocked attempt with invalid characters in tweak name: " + name);
                return null;
            }

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string tweaksRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, "Resources", "tweaks"));
            string scriptPath = System.IO.Path.Combine(tweaksRoot, name, scriptFileName);

            if (!System.IO.File.Exists(scriptPath))
            {
                string altRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, "..", "..", "Resources", "tweaks"));
                string altPath = System.IO.Path.Combine(altRoot, name, scriptFileName);
                if (System.IO.File.Exists(altPath))
                {
                    scriptPath = altPath;
                    tweaksRoot = altRoot;
                }
            }

            // Verify canonical path is strictly inside tweaksRoot
            try
            {
                string fullPath = System.IO.Path.GetFullPath(scriptPath);
                if (!fullPath.StartsWith(tweaksRoot + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    CWLog.Warn("Security.PathTraversal", "Blocked traversal out of tweaks directory: " + fullPath);
                    return null;
                }
                return fullPath;
            }
            catch (Exception ex)
            {
                CWLog.Error("Security.PathTraversal", ex);
                return null;
            }
        }

        public static bool IsUntestedDeferredTweak(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            string n = name.Trim().ToLowerInvariant();
            return n == "disable-dynamic-ticking" ||
                   n == "disable-hpet" ||
                   n == "optimize-network-settings" ||
                   n == "set-services-to-manual";
        }

        public static bool IsPartialUndoTweak(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            string n = name.Trim().ToLowerInvariant();
            return n == "remove-gaming-apps" ||
                   n == "remove-onedrive";
        }

        public static bool IsIrreversibleTweak(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            string n = name.Trim().ToLowerInvariant();
            return n == "debloat-windows" ||
                   n == "run-disk-cleanup";
        }

        private bool IsCriticalTweak(string name)
        {
            string[] critical = new string[] {
                "disable-defender-rtp", "set-services-to-manual", "disable-core-isolation",
                "disable-hibernation", "disable-fast-startup", "remove-onedrive", "debloat-windows",
                "disable-copilot", "disable-gamebar", "remove-gaming-apps", "disable-background-ms-store-apps",
                "disable-dynamic-ticking", "disable-hpet", "optimize-network-settings",
                "ultimate-performance-plan", "set-time-utc"
            };
            foreach (string c in critical) { if (c == name) return true; }
            return false;
        }

        private static DateTime _lastSessionRestorePoint = DateTime.MinValue;
        private static readonly object _restorePointSync = new object();
        private static int _dnsFlushInFlight = 0;
        private static int _restorePointInFlight = 0;

        internal static bool TryEnterRestorePointFlight()
        {
            return System.Threading.Interlocked.CompareExchange(ref _restorePointInFlight, 1, 0) == 0;
        }

        internal static void ExitRestorePointFlight()
        {
            System.Threading.Interlocked.Exchange(ref _restorePointInFlight, 0);
        }

        internal static bool TryEnterDnsFlushFlight()
        {
            return System.Threading.Interlocked.CompareExchange(ref _dnsFlushInFlight, 1, 0) == 0;
        }

        internal static void ExitDnsFlushFlight()
        {
            System.Threading.Interlocked.Exchange(ref _dnsFlushInFlight, 0);
        }

        internal static bool TryFlushDnsVerified(out string message)
        {
            message = "";
            if (System.Threading.Interlocked.CompareExchange(ref _dnsFlushInFlight, 1, 0) != 0)
            {
                message = "عملية تفريغ كاش DNS قيد التنفيذ بالفعل، يرجى الانتظار.";
                return false;
            }
            try
            {
            if (AdminSessionManager.IsAdminConnected)
            {
                var helperRes = AdminSessionManager.ExecutePrivilegedAction("system.flush_dns", "");
                if (helperRes != null && helperRes.Success && string.Equals(helperRes.Status, "SUCCESS", StringComparison.OrdinalIgnoreCase))
                {
                    message = "تم تفريغ كاش DNS (Flush DNS) والتحقق من نجاحه عبر المساعد الإداري.";
                    ClearWinAuditLogger.Log("system.flush_dns", "Green", "User", "AdminHelper", true, "SUCCESS", "VERIFIED", message, helperRes.DurationMs);
                    return true;
                }
            }
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "ipconfig.exe",
                    Arguments = "/flushdns",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var p = Process.Start(psi))
                {
                    if (p == null)
                    {
                        message = "تعذر بدء عملية ipconfig.exe.";
                        return false;
                    }
                    var sbOut = new System.Text.StringBuilder();
                    var sbErr = new System.Text.StringBuilder();
                    p.OutputDataReceived += (s, e) => { if (e.Data != null) sbOut.AppendLine(e.Data); };
                    p.ErrorDataReceived += (s, e) => { if (e.Data != null) sbErr.AppendLine(e.Data); };
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();

                    if (!p.WaitForExit(10000))
                    {
                        try { p.Kill(); } catch { }
                        message = "انتهت مهلة تنفيذ ipconfig /flushdns دون استجابة.";
                        return false;
                    }
                    p.WaitForExit();
                    if (p.ExitCode != 0)
                    {
                        string errDetail = sbErr.ToString().Trim();
                        if (string.IsNullOrEmpty(errDetail)) errDetail = sbOut.ToString().Trim();
                        message = "فشل تفريغ كاش DNS (رمز الخروج " + p.ExitCode + ")" + (string.IsNullOrEmpty(errDetail) ? "." : (": " + errDetail));
                        return false;
                    }
                    message = "تم تفريغ كاش DNS (Flush DNS) والتحقق من رمز الخروج (0) بنجاح.";
                    return true;
                }
            }
            catch (Exception ex)
            {
                CWLog.Error("MainWindow.Pages.FlushDns", ex);
                message = "تعذر تنفيذ ipconfig /flushdns: " + ex.Message;
                return false;
            }
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _dnsFlushInFlight, 0);
            }
        }

        private bool CreateClearWinRestorePoint(string tweakName, out string message, bool allowSessionCache = true)
        {
            message = "";
            if (!allowSessionCache)
            {
                if (System.Threading.Interlocked.CompareExchange(ref _restorePointInFlight, 1, 0) != 0)
                {
                    message = "عملية إنشاء نقطة الاستعادة قيد التنفيذ بالفعل، يرجى الانتظار حتى تكتمل.";
                    return false;
                }
            }
            try
            {
            lock (_restorePointSync)
            {
                // Safety cache: For automatic pre-tweak checks (allowSessionCache == true),
                // reuse a verified restore point created within the last 30 minutes.
                // Explicit manual user requests pass allowSessionCache = false so creation is always executed and verified.
                if (allowSessionCache && (DateTime.Now - _lastSessionRestorePoint).TotalMinutes < 30.0)
                {
                    message = "تم اعتماد نقطة الاستعادة النشطة للجلسة الحالية.";
                    return true;
                }
                if (allowSessionCache)
                {
                    System.Threading.Interlocked.Exchange(ref _restorePointInFlight, 1);
                }

                // Security: strictly whitelist alphanumeric, dashes, underscores, and dots to prevent PowerShell command/expression injection
                string safeName = tweakName ?? "tweak";
                safeName = System.Text.RegularExpressions.Regex.Replace(safeName, @"[^a-zA-Z0-9_\-\.\s]", "");
                if (string.IsNullOrWhiteSpace(safeName)) safeName = "tweak";
                string desc = "ClearWin before " + safeName;

                // Phase A: If AdminHelper is connected, delegate to the privileged helper channel and strictly verify outcome
                if (AdminSessionManager.IsAdminConnected)
                {
                    var helperRes = AdminSessionManager.ExecutePrivilegedAction("system.create_restore_point", desc);
                    if (helperRes != null && helperRes.Success && string.Equals(helperRes.Status, "SUCCESS", StringComparison.OrdinalIgnoreCase))
                    {
                        _lastSessionRestorePoint = DateTime.Now;
                        message = !string.IsNullOrWhiteSpace(helperRes.Message) ? helperRes.Message : "Restore Point created and verified via AdminHelper.";
                        ClearWinAuditLogger.Log("system.create_restore_point", "Yellow", "User", "AdminHelper", true, "SUCCESS", "VERIFIED", message, helperRes.DurationMs);
                        return true;
                    }
                    string helperErr = (helperRes != null && !string.IsNullOrWhiteSpace(helperRes.Message))
                        ? helperRes.Message
                        : (helperRes != null && !string.IsNullOrWhiteSpace(helperRes.Error) ? helperRes.Error : "AdminHelper failed to create a Restore Point.");
                    message = helperErr;
                    ClearWinAuditLogger.Log("system.create_restore_point", "Yellow", "User", "AdminHelper", true, "FAILED", "FAILED", message, helperRes != null ? helperRes.DurationMs : 0);
                    return false;
                }

                // Phase B: If the current process is already elevated, try in-process WMI SystemRestore (Type 12 / Event 100)
                if (IsProcessElevated())
                {
                    try
                    {
                        if (SystemRestoreHelper.TryCreateRestorePoint(desc))
                        {
                            _lastSessionRestorePoint = DateTime.Now;
                            message = "Restore Point created (WMI).";
                            return true;
                        }
                    }
                    catch (Exception exWmi)
                    {
                        CWLog.Error("MainWindow.Pages.RestorePoint", exWmi);
                    }
                }

                // Phase C: Elevated PowerShell (Verb = "runas") with strict marker-file and exit-code verification
                string marker = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "clearwin_restore_result_" + Guid.NewGuid().ToString("N") + ".txt");
                string command = string.Format(
                    "$ErrorActionPreference = 'Stop'; try {{ Checkpoint-Computer -Description \"{0}\" -RestorePointType MODIFY_SETTINGS -ErrorAction Stop; 'OK' | Set-Content -LiteralPath '{1}' -Encoding UTF8; exit 0 }} catch {{ $_.Exception.Message | Set-Content -LiteralPath '{1}' -Encoding UTF8; exit 1 }}",
                    desc, marker.Replace("'", "''"));
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" + command.Replace("\"", "\\\"") + "\"",
                        UseShellExecute = true,
                        Verb = "runas",
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    using (var p = Process.Start(psi))
                    {
                        if (p == null)
                        {
                            message = "Failed to start elevated PowerShell process.";
                            return false;
                        }
                        if (!p.WaitForExit(180000))
                        {
                            try { p.Kill(); } catch (Exception ex39) { CWLog.Error("MainWindow.Pages", ex39); }
                            message = "Restore Point timeout.";
                            return false;
                        }
                        string result = File.Exists(marker) ? File.ReadAllText(marker, System.Text.Encoding.UTF8).Trim() : "";
                        try { if (File.Exists(marker)) File.Delete(marker); } catch (Exception ex40) { CWLog.Error("MainWindow.Pages", ex40); }
                        if (p.ExitCode == 0 && string.Equals(result, "OK", StringComparison.Ordinal))
                        {
                            _lastSessionRestorePoint = DateTime.Now;
                            message = "Restore Point created.";
                            return true;
                        }
                        message = string.IsNullOrEmpty(result)
                            ? ("Windows could not create a Restore Point (exit code " + p.ExitCode + ").")
                            : result;
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    try { if (File.Exists(marker)) File.Delete(marker); } catch (Exception ex41) { CWLog.Error("MainWindow.Pages", ex41); }
                    message = ex.Message;
                    return false;
                }
            }
            }
            finally
            {
                System.Threading.Interlocked.Exchange(ref _restorePointInFlight, 0);
            }
        }

        internal static bool IsProcessElevated()
        {
            try
            {
                using (var identity = System.Security.Principal.WindowsIdentity.GetCurrent())
                {
                    var principal = new System.Security.Principal.WindowsPrincipal(identity);
                    return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
                }
            }
            catch { return false; }
        }

        private string RunTweakScript(string name, string scriptFileName, out bool ok)
        {
            ok = false;
            bool isApply = string.Equals(scriptFileName, "apply.ps1", StringComparison.OrdinalIgnoreCase);

            // Release Gate: Prevent production execution of untested tweaks (BCD, TCP global stack, Services)
            if (isApply && IsUntestedDeferredTweak(name))
            {
                return "BLOCKED (NOT TESTED): التعديل '" + name + "' مصنف NOT TESTED ومؤجل خارج التفعيل الإنتاجي لحماية إعدادات النظام الأساسية (BCD / الشبكة / الخدمات) إلى حين التحقق منه في بيئة معزولة.";
            }

            if (!isApply && IsIrreversibleTweak(name))
            {
                return "BLOCKED (IRREVERSIBLE): التعديل '" + name + "' عملية دائمة غير قابلة للاستعادة التلقائية.";
            }

            string scriptPath = ResolveTweakScriptPath(name, scriptFileName);
            if (!System.IO.File.Exists(scriptPath)) return "Script not found:\n" + scriptPath;

            if (isApply)
            {
                string restoreMessage;
                if (!CreateClearWinRestorePoint(name, out restoreMessage))
                    return "تم إيقاف التعديل لأن ClearWin لم يتمكن من إنشاء نقطة استعادة للنظام.\n\n" + restoreMessage;
            }

            // RL live evaluation: capture baseline before the real tweak runs.
            // Only apply.ps1 is evaluated - check/read/undo runs must not pollute the Q-table.
            if (isApply && _agent != null)
            {
                try { _agent.BeginTweakEvaluation(name); }
                catch (Exception exBe) { CWLog.Error("MainWindow.Pages", exBe); }
            }

            // Privilege Dispatch: If AdminHelper is connected, delegate execution to privileged helper process
            if (AdminSessionManager.IsAdminConnected)
            {
                var helperRes = AdminSessionManager.ExecutePrivilegedAction(isApply ? "tweak.apply" : "tweak.undo", name);
                ok = helperRes.Success;
                if (isApply && ok && _agent != null)
                {
                    #pragma warning disable 4014
                    try { _agent.EvaluateExecution(name); } catch (Exception exEv) { CWLog.Error("MainWindow.Pages", exEv); }
                    #pragma warning restore 4014
                }
                ClearWinAuditLogger.Log(
                    "tweak:" + name,
                    ActionCatalog.GetTier(name).ToString(),
                    "User",
                    "AdminHelper",
                    true,
                    ok ? "SUCCESS" : "FAILED",
                    ok ? "VERIFIED" : "FAILED",
                    ok ? "تم تنفيذ التعديل بنجاح عبر المساعد الإداري المستقل." : ("فشل التنفيذ: " + helperRes.Error),
                    helperRes.DurationMs);
                return (helperRes.Output ?? "") + (string.IsNullOrEmpty(helperRes.Error) ? "" : "\n" + helperRes.Error);
            }

            var catalogItem = ActionCatalog.GetItem(name);
            if (catalogItem != null && catalogItem.RequiresAdmin && !IsProcessElevated())
            {
                return "يتطلب التعديل '" + name + "' صلاحيات المسؤول. اضغط على 'تفعيل وضع المسؤول' لطلب UAC الرسمي الآمن.";
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = string.Format("-ExecutionPolicy Bypass -NoProfile -WindowStyle Hidden -File \"{0}\"", scriptPath),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                // Security Gate: ensure no inherited test environment variable can hijack or redirect tweaks in production
                if (psi.EnvironmentVariables.ContainsKey("CLEARWIN_TEST_REG_PREFIX"))
                    psi.EnvironmentVariables.Remove("CLEARWIN_TEST_REG_PREFIX");
                if (psi.EnvironmentVariables.ContainsKey("CLEARWIN_TEST_BACKUP_DIR"))
                    psi.EnvironmentVariables.Remove("CLEARWIN_TEST_BACKUP_DIR");
                if (psi.EnvironmentVariables.ContainsKey("CLEARWIN_TEST_MODE"))
                    psi.EnvironmentVariables.Remove("CLEARWIN_TEST_MODE");
                if (psi.EnvironmentVariables.ContainsKey("CLEARWIN_ALLOW_DEFERRED_VM_TWEAKS"))
                    psi.EnvironmentVariables.Remove("CLEARWIN_ALLOW_DEFERRED_VM_TWEAKS");

                if (string.Equals(name, "disable-defender-rtp", StringComparison.OrdinalIgnoreCase) && isApply)
                    psi.EnvironmentVariables["CLEARWIN_DEFENDER_OPT_IN"] = "YES";
                if (string.Equals(name, "disable-core-isolation", StringComparison.OrdinalIgnoreCase) && isApply)
                    psi.EnvironmentVariables["CLEARWIN_CORE_ISOLATION_OPT_IN"] = "YES";
                if (string.Equals(name, "debloat-windows", StringComparison.OrdinalIgnoreCase) && isApply)
                    psi.EnvironmentVariables["CLEARWIN_DEBLOAT_OPT_IN"] = "YES";
                if (string.Equals(name, "run-disk-cleanup", StringComparison.OrdinalIgnoreCase) && isApply)
                    psi.EnvironmentVariables["CLEARWIN_CLEANUP_OPT_IN"] = "YES";

                using (var p = Process.Start(psi))
                {
                    // One builder per stream: the two data events fire on separate
                    // reader threads and must not append to a shared StringBuilder.
                    var sbOut = new System.Text.StringBuilder();
                    var sbErr = new System.Text.StringBuilder();
                    p.OutputDataReceived += (s, e) => { if (e.Data != null) sbOut.AppendLine(e.Data); };
                    p.ErrorDataReceived += (s, e) => { if (e.Data != null) sbErr.AppendLine(e.Data); };
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(120000)) { try { p.Kill(); } catch (Exception ex42) { CWLog.Error("MainWindow.Pages", ex42); } return "Timeout: the script did not finish in 120 seconds."; }
                    // Drain the async streams (the argumentless WaitForExit blocks until
                    // the BeginOutputReadLine readers have flushed) before reading.
                    p.WaitForExit();
                    ok = p.ExitCode == 0;
                    // RL live evaluation: fire-and-forget. The agent settles 5 s,
                    // re-samples, updates the Double-Q tables, undoes on degradation.
                    if (isApply && ok && _agent != null)
                    {
                        #pragma warning disable 4014 // intentional fire-and-forget
                        try { _agent.EvaluateExecution(name); }
                        catch (Exception exEv) { CWLog.Error("MainWindow.Pages", exEv); }
                        #pragma warning restore 4014
                    }
                    return sbOut.ToString() + sbErr.ToString();
                }
            }
            catch (Exception ex) { return "خطأ: " + ex.Message; }
        }

        // Shared off-thread guard: one PowerShell run per tweak+direction so a
        // double click (or WPF + WebUI simultaneously) cannot race two apply.ps1s.
        private bool TryStartTweak(string name, string direction)
        {
            string key = name.ToLowerInvariant() + "|" + direction.ToLowerInvariant();
            lock (_tweakInFlightLock)
            {
                if (_tweaksInFlight.Contains(key)) return false;
                _tweaksInFlight.Add(key);
                return true;
            }
        }

        private void FinishTweak(string name, string direction)
        {
            string key = name.ToLowerInvariant() + "|" + direction.ToLowerInvariant();
            lock (_tweakInFlightLock) { _tweaksInFlight.Remove(key); }
        }

        private void RunTweak(string name)
        {
            if (IsUntestedDeferredTweak(name))
            {
                MessageBox.Show(
                    "هذا التعديل (" + name + ") مصنف NOT TESTED ومؤجل خارج التفعيل الإنتاجي في النسخة العامة لحماية إعدادات النظام الأساسية (BCD / الشبكة / الخدمات) إلى حين التحقق منه في بيئة معزولة (VM/Sandbox).",
                    "ClearWin — تعديل مؤجل لحماية النظام",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            bool critical = IsCriticalTweak(name);
            string msg = critical
                ? string.Format("تحذير! هذا تعديل حساس للنظام وقد يؤثر على الأمان أو الاستقرار:\n\n{0}\n\nقبل التنفيذ سيحاول ClearWin إنشاء نقطة استعادة للنظام.\nهل تريد المتابعة؟", name)
                : string.Format("تطبيق التعديل:\n\n{0}؟", name);
            if (string.Equals(name, "disable-defender-rtp", StringComparison.OrdinalIgnoreCase))
                msg = "تحذير أمني شديد\n\nهذا الخيار سيعطل الحماية المباشرة لـ Windows Defender.\nلن يدخل في التحسين التلقائي، وسيحتاج تأكيدًا صريحًا منك.\nسيتم إنشاء نقطة استعادة قبل التنفيذ.\n\nهل تريد المتابعة؟";
            else if (string.Equals(name, "disable-core-isolation", StringComparison.OrdinalIgnoreCase))
                msg = "تحذير أمني شديد: عزل الذاكرة الأساسية (Core Isolation / HVCI)\n\nتعطيل حماية تكامل الذاكرة يقلل من حماية النواة ضد برمجيات الفدية والهجمات المتقدمة.\nهذا التعديل معطل افتراضياً وهو اختياري بالكامل للاعبين لتقليل تأخير العتاد.\nيتطلب إعادة تشغيل الجهاز لتطبيقه أو التراجع عنه.\n\nهل توافق صراحة على متابعة تعطيل عزل الذاكرة؟";
            else if (string.Equals(name, "debloat-windows", StringComparison.OrdinalIgnoreCase))
                msg = "تنبيه: حذف تطبيقات Windows الدعائية المسبقة (معطلة افتراضياً)\n\nسيقوم هذا الإجراء بإزالة الحزم الترويجية فقط (Bing, Clipchamp, 3DBuilder, Solitaire, News, Weather, Skype).\nالأدوات الأساسية مثل Notepad و Calculator و Paint و Terminal محمية تماماً ولن تُحذف.\nتنبيه: هذا الإجراء دائم وغير قابل للاستعادة تلقائياً.\n\nهل توافق صراحة على متابعة حذف هذه التطبيقات؟";
            else if (string.Equals(name, "run-disk-cleanup", StringComparison.OrdinalIgnoreCase))
                msg = "تنبيه: تنظيف القرص ومخزن المكونات (عملية دائمة غير قابلة للاستعادة)\n\nسيقوم هذا الإجراء بحذف الملفات المؤقتة في %TEMP% و Windows\\Temp، وتنظيف التحديثات الملغاة في مخزن المكونات (WinSxS).\nهذه العملية تحرر مساحة القرص نهائياً ولا يمكن استعادة الملفات المحذوفة.\n\nهل توافق صراحة على متابعة تنظيف القرص؟";
            else if (IsPartialUndoTweak(name))
                msg = string.Format("تنبيه استعادة جزئية ({0}):\n\nهذا الإجراء يزيل حزم التطبيق من النظام. التراجع عنه لاحقاً يتم عبر إعادة التثبيت من Winget (استعادة جزئية قد لا تعيد البيانات أو الإعدادات المحلية السابقة).\n\nهل تريد المتابعة؟", name);

            var confirm = MessageBox.Show(msg, critical ? "تعديل حساس" : "تأكيد",
                MessageBoxButton.YesNo, critical ? MessageBoxImage.Warning : MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;
            if (!TryStartTweak(name, "apply")) return;
            if (_webUiHost != null) _webUiHost.PushStatus("جاري تنفيذ: " + name + " ...");
            // The script runs for up to 120 s: execute off the UI thread so the
            // window stays responsive, and deliver the result back via the Dispatcher.
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    bool ok;
                    string result = RunTweakScript(name, "apply.ps1", out ok);
                    string body = string.IsNullOrEmpty(result) ? "(بدون مخرجات)" : result;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            MessageBox.Show(
                                string.Format("{0}\n\n{1}", ok ? "تم التطبيق بنجاح" : "فشل أو توقف تطبيق التعديل (راجع التفاصيل أدناه)", body),
                                "ClearWin",
                                MessageBoxButton.OK,
                                ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
                        }
                        catch (Exception exR) { CWLog.Error("MainWindow.Pages", exR); }
                    }));
                }
                finally { FinishTweak(name, "apply"); }
            });
        }

        private void RunUndoTweak(string name)
        {
            if (IsIrreversibleTweak(name))
            {
                MessageBox.Show(
                    "هذا الإجراء (" + name + ") دائم وغير قابل للاستعادة التلقائية.",
                    "ClearWin — غير قابل للاستعادة",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            string promptMsg = IsPartialUndoTweak(name)
                ? string.Format("تراجع جزئي عن التعديل ({0}):\n\nسيحاول ClearWin إعادة تثبيت التطبيق عبر Winget. هذا التراجع جزئي وقد يتطلب اتصالاً بالإنترنت ولا يستعيد الإعدادات السابقة بالكامل.\n\nهل تريد المتابعة؟", name)
                : string.Format("تراجع عن التعديل:\n\n{0}؟", name);

            var confirm = MessageBox.Show(promptMsg, IsPartialUndoTweak(name) ? "تأكيد تراجع جزئي" : "تأكيد",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;
            if (!TryStartTweak(name, "undo")) return;
            if (_webUiHost != null) _webUiHost.PushStatus("جاري التراجع عن: " + name + " ...");
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    bool ok;
                    string result = RunTweakScript(name, "undo.ps1", out ok);
                    string body = string.IsNullOrEmpty(result) ? "(بدون مخرجات)" : result;
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            MessageBox.Show(
                                string.Format("{0}\n\n{1}", ok ? "تم التراجع بنجاح" : "فشل التراجع عن التعديل (راجع سبب الفشل أدناه)", body),
                                "ClearWin",
                                MessageBoxButton.OK,
                                ok ? MessageBoxImage.Information : MessageBoxImage.Error);
                        }
                        catch (Exception exU) { CWLog.Error("MainWindow.Pages", exU); }
                    }));
                }
                finally { FinishTweak(name, "undo"); }
            });
        }

        private UIElement BuildToolsPage()
        {
            var root = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var stack = new StackPanel();
            root.Content = stack;
            stack.Children.Add(MakeText("أدوات", 28, Colors.White, true));
            var _ts = MakeText("أدوات النظام والمرافق", 13, Color.FromArgb(136, 255, 255, 255), false); _ts.Margin = new Thickness(0, 0, 0, 16); stack.Children.Add(_ts);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition());
            string[][] tools = new string[][] {
                new[] {"لقطة شاشة", "تصوير الشاشة"},
                new[] {"السجل", "فحص وتنظيف آمن للسجل مع نسخة احتياطية"},
                new[] {"الخدمات", "إدارة الخدمات"},
                new[] {"الأقراص", "تحليل المساحة"},
                new[] {"التعريفات", "فحص التعريفات"},
                new[] {"إلغاء التجزئة", "تحسين الأقراص"}
            };
            for (int i = 0; i < tools.Length; i++)
            {
                var btn = new Border { Background = new SolidColorBrush(Theme.CardClr), CornerRadius = new CornerRadius(10), Padding = new Thickness(16, 12, 16, 12), Margin = new Thickness(4), Cursor = Cursors.Hand };
                var bs = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                bs.Children.Add(MakeText(tools[i][0], 14, Colors.White, true));
                bs.Children.Add(MakeText(tools[i][1], 10, Color.FromArgb(136, 255, 255, 255), false));
                btn.Child = bs;
                int idx = i;
                btn.MouseDown += (s2, e2) => LaunchTool(idx);
                btn.MouseEnter += (s2, e2) => btn.Background = new SolidColorBrush(Theme.HoverBg);
                btn.MouseLeave += (s2, e2) => btn.Background = new SolidColorBrush(Theme.CardClr);
                Grid.SetRow(btn, i / 3); Grid.SetColumn(btn, i % 3); grid.Children.Add(btn);
            }
            stack.Children.Add(grid);

            // Service manager section
            var srvCard = MakeCard(18, null); var srvInner = new StackPanel { Margin = new Thickness(20) };
            srvInner.Children.Add(MakeText("مدير الخدمات", 16, Colors.White, true));
            var srvDesc = MakeText("تشغيل وإيقاف خدمات ويندوز", 11, Color.FromArgb(136, 255, 255, 255), false);
            srvDesc.Margin = new Thickness(0, 4, 0, 10);
            srvInner.Children.Add(srvDesc);

            var srvGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            srvGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            srvGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var srvFilter = new TextBox
            {
                Background = new SolidColorBrush(Color.FromArgb(32, 255, 255, 255)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Color.FromArgb(64, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 6, 10, 6),
                FontSize = 12,
                Text = ""
            };
            var srvPh = MakeText("  بحث عن خدمة...", 12, Color.FromArgb(119, 255, 255, 255), false);
            srvPh.VerticalAlignment = VerticalAlignment.Center; srvPh.IsHitTestVisible = false;
            var srvFilterBorder = new Border { Child = srvFilter };
            var srvPhLayer = new Grid(); srvPhLayer.Children.Add(srvFilterBorder); srvPhLayer.Children.Add(srvPh);
            Grid.SetColumn(srvPhLayer, 0); srvGrid.Children.Add(srvPhLayer);
            _toolsSrvList = new ListBox(); // declare before use
            _toolsSrvFilter = "";
            srvFilter.TextChanged += (s6, e6) => { _toolsSrvFilter = srvFilter.Text.Trim().ToLower(); srvPh.Visibility = srvFilter.Text == "" ? Visibility.Visible : Visibility.Collapsed; LoadServices(_toolsSrvList, _toolsSrvFilter); };

            var srvRefBtn = new Border { Background = new SolidColorBrush(Color.FromArgb(32, _accentColor.R, _accentColor.G, _accentColor.B)), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(6, 0, 0, 0), Cursor = Cursors.Hand };
            srvRefBtn.Child = MakeText("تحديث", 11, _accentColor, true);
            srvRefBtn.MouseDown += (s7, e7) => LoadServices(_toolsSrvList, _toolsSrvFilter);
            Grid.SetColumn(srvRefBtn, 1); srvGrid.Children.Add(srvRefBtn);
            srvInner.Children.Add(srvGrid);

            _toolsSrvList = new ListBox
            {
                Height = 220,
                Background = new SolidColorBrush(Theme.CardClr),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0)
            };
            srvInner.Children.Add(_toolsSrvList);

            var srvBtnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            var startBtn = new Border { Background = new SolidColorBrush(Theme.Green), CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 0), Cursor = Cursors.Hand };
            startBtn.Child = MakeText("تشغيل", 11, Colors.White, true);
            startBtn.MouseDown += (s8, e8) => ManageService(_toolsSrvList, "start");
            srvBtnRow.Children.Add(startBtn);

            var stopBtn = new Border { Background = new SolidColorBrush(Theme.Error), CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(0, 0, 8, 0), Cursor = Cursors.Hand };
            stopBtn.Child = MakeText("إيقاف", 11, Colors.White, true);
            stopBtn.MouseDown += (s9, e9) => ManageService(_toolsSrvList, "stop");
            srvBtnRow.Children.Add(stopBtn);

            var restartBtn = new Border { Background = new SolidColorBrush(Theme.Warning), CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 6, 12, 6), Cursor = Cursors.Hand };
            restartBtn.Child = MakeText("إعادة تشغيل", 11, Colors.White, true);
            restartBtn.MouseDown += (s10, e10) => ManageService(_toolsSrvList, "restart");
            srvBtnRow.Children.Add(restartBtn);

            srvInner.Children.Add(srvBtnRow);
            srvCard.Child = srvInner; stack.Children.Add(srvCard);

            LoadServices(_toolsSrvList, "");
            return root;
        }

        private void LoadServices(ListBox list, string filter)
        {
            if (list == null) return;
            list.Items.Clear();
            try
            {
                using (var sq = new ManagementObjectSearcher("SELECT Name, DisplayName, State, StartMode FROM Win32_Service"))
                {
                    foreach (ManagementObject mo in sq.Get())
                    {
                        string name = Convert.ToString(mo["Name"]);
                        string display = Convert.ToString(mo["DisplayName"]);
                        string state = Convert.ToString(mo["State"]);
                        string mode = Convert.ToString(mo["StartMode"]);
                        if (filter == "" || name.ToLower().Contains(filter) || display.ToLower().Contains(filter))
                            list.Items.Add(string.Format("{0}  [{1}]  ({2})", display, state, mode));
                    }
                }
            }
            catch { list.Items.Add("(خطأ - شغّل كمسؤول)"); }
        }

        private void ManageService(ListBox list, string action)
        {
            var sel = list.SelectedItem as string;
            if (sel == null) { MessageBox.Show("اختر خدمة أولاً", "ClearWin"); return; }
            string name = sel.Contains("  [") ? sel.Substring(0, sel.IndexOf("  [")).Trim() : sel;
            try
            {
                string wmiName = "";
                using (var sq = new ManagementObjectSearcher(string.Format("SELECT Name FROM Win32_Service WHERE DisplayName='{0}'", name.Replace("'", "''"))))
                {
                    foreach (ManagementObject mo in sq.Get()) { wmiName = Convert.ToString(mo["Name"]); break; }
                }
                if (wmiName == "")
                {
                    // Try by name directly
                    using (Process.Start(new ProcessStartInfo
                    {
                        FileName = "sc.exe",
                        Arguments = string.Format("{0} \"{1}\"", action, name),
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        Verb = "runas"
                    })) { }
                }
                else
                {
                    using (Process.Start(new ProcessStartInfo
                    {
                        FileName = "sc.exe",
                        Arguments = string.Format("{0} \"{1}\"", action, wmiName),
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        Verb = "runas"
                    })) { }
                }
                MessageBox.Show(string.Format("تم إرسال أمر {0} للخدمة", action), "ClearWin");
                LoadServices(list, "");
            }
            catch (Exception ex) { MessageBox.Show("خطأ: " + ex.Message, "ClearWin"); }
        }

        private void LaunchTool(int idx)
        {
            switch (idx)
            {
                case 0: // Screenshot
                    try
                    {
                        double w = SystemParameters.VirtualScreenWidth;
                        double h = SystemParameters.VirtualScreenHeight;
                        using (var bmp = new System.Drawing.Bitmap((int)w, (int)h))
                        {
                            using (var g = System.Drawing.Graphics.FromImage(bmp))
                            { g.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size((int)w, (int)h)); }
                            string path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), string.Format("Screenshot_{0:yyyyMMdd_HHmmss}.png", DateTime.Now));
                            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
                            try { using (Process.Start("explorer.exe", "/select,\"" + path + "\"")) { } } catch (Exception ex43) { CWLog.Error("MainWindow.Pages", ex43); }
                            MessageBox.Show(string.Format("تم الحفظ وفتح الموقع:\n{0}", path), "ClearWin");
                        }
                    }
                    catch (Exception ex) { MessageBox.Show("خطأ: " + ex.Message, "ClearWin"); }
                    break;
                case 1: // Registry Scan & Clean
                    RunRegistryScan();
                    break;
                case 2: // Services - open services.msc
                    try { using (Process.Start("services.msc")) { } } catch (Exception ex44) { CWLog.Error("MainWindow.Pages", ex44); }
                    break;
                case 3: // Disk - open disk management
                    SwitchTab("الأقراص", _activeNav);
                    break;
                case 4: // Driver - open device manager
                    try { using (Process.Start("devmgmt.msc")) { } } catch (Exception ex45) { CWLog.Error("MainWindow.Pages", ex45); }
                    break;
                case 5: // Defrag
                    try { using (Process.Start(new ProcessStartInfo { FileName = "dfrgui.exe", UseShellExecute = true })) { } } catch (Exception ex46) { CWLog.Error("MainWindow.Pages", ex46); }
                    break;
            }
        }

        private void RunRegistryScan()
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                string[] paths = new string[] {
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\ComDlg32",
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\ComDlg32\OpenSavePidlMRU",
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\ComDlg32\LastVisitedPidlMRU"
                };
                int emptyValues = 0;
                foreach (string p in paths)
                {
                    try
                    {
                        using (var key = Registry.CurrentUser.OpenSubKey(p, false))
                        {
                            if (key == null) continue;
                            foreach (var n in key.GetValueNames())
                            {
                                try
                                {
                                    var val = key.GetValue(n, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                                    if (val == null) { emptyValues++; }
                                    else
                                    {
                                        string s = val as string;
                                        if (s != null && s == "") emptyValues++;
                                    }
                                }
                                catch (Exception ex47) { CWLog.Error("MainWindow.Pages", ex47); }
                            }
                        }
                    }
                    catch (Exception ex48) { CWLog.Error("MainWindow.Pages", ex48); }
                }

                string summary = string.Format("تم فحص السجل.\n\nالمدخلات الفارغة: {0}", emptyValues);
                var dispatcher = System.Windows.Application.Current != null ? System.Windows.Application.Current.Dispatcher : null;

                if (emptyValues == 0)
                {
                    if (dispatcher != null) dispatcher.Invoke(() => MessageBox.Show(summary + "\n\nلا يوجد شيء لتنظيفه.", "فحص السجل"));
                    return;
                }

                bool confirmed = false;
                if (dispatcher != null)
                {
                    dispatcher.Invoke(() =>
                    {
                        confirmed = MessageBox.Show(summary + "\n\nهل تريد حذفها؟\n(سيتم إنشاء نسخة احتياطية قبل الحذف)", "فحص السجل",
                            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
                    });
                }
                if (!confirmed) return;

                string backupPath = "";
                try
                {
                    string backupDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ClearWin");
                    System.IO.Directory.CreateDirectory(backupDir);
                    backupPath = System.IO.Path.Combine(backupDir, string.Format("registry_clean_{0:yyyyMMdd_HHmmss}.reg", DateTime.Now));
                    var psi = new ProcessStartInfo("reg.exe", string.Format("export \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\ComDlg32\" \"{0}\" /y", backupPath));
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    using (Process.Start(psi)) { }
                }
                catch (Exception ex49) { CWLog.Error("MainWindow.Pages", ex49); }

                int deleted = 0;
                foreach (string p in paths)
                {
                    try
                    {
                        using (var key = Registry.CurrentUser.OpenSubKey(p, true))
                        {
                            if (key == null) continue;
                            foreach (var n in key.GetValueNames())
                            {
                                try
                                {
                                    var val = key.GetValue(n, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                                    bool empty = val == null || (val is string && (string)val == "");
                                    if (empty)
                                    {
                                        key.DeleteValue(n, false);
                                        deleted++;
                                    }
                                }
                                catch (Exception ex50) { CWLog.Error("MainWindow.Pages", ex50); }
                            }
                        }
                    }
                    catch (Exception ex51) { CWLog.Error("MainWindow.Pages", ex51); }
                }

                string doneMsg = string.Format("تم حذف {0} مدخل فارغ.", deleted);
                if (!string.IsNullOrEmpty(backupPath))
                    doneMsg += string.Format("\n\nالنسخة الاحتياطية:\n{0}", backupPath);
                if (dispatcher != null) dispatcher.Invoke(() => MessageBox.Show(doneMsg, "فحص السجل"));
            });
        }

        private UIElement BuildGPUPage()
        {
            var root = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var panel = new StackPanel { Margin = new Thickness(32, 16, 32, 16) };
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
            titleRow.Children.Add(new Border { Width = 4, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Theme.Accent), Margin = new Thickness(0, 4, 12, 4) });
            titleRow.Children.Add(MakeText("GPU", 28, Colors.White, true));
            panel.Children.Add(titleRow);
            panel.Children.Add(MakeText("معلومات بطاقة الرسوم والأداء", 13, Color.FromArgb(136, 255, 255, 255), false));
            panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Margin = new Thickness(0, 16, 0, 16) });

            // GPU Info Card
            var infoCard = MakeCard(18, null);
            var infoStack = new StackPanel { Margin = new Thickness(24) };
            infoStack.Children.Add(MakeText("Graphics Card", 18, Colors.White, true));
            var gpuNameLine = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 4) };
            gpuNameLine.Children.Add(MakeText("Name:", 12, Theme.TextSecondary, false));
            _gpuSub = MakeText("--", 12, Colors.White, true);
            _gpuSub.Margin = new Thickness(8, 0, 0, 0); gpuNameLine.Children.Add(_gpuSub);
            infoStack.Children.Add(gpuNameLine);

            var ramLine = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
            ramLine.Children.Add(MakeText("VRAM:", 12, Theme.TextSecondary, false));
            _gpuVRAMTxt = MakeText("--", 12, Colors.White, true);
            _gpuVRAMTxt.Margin = new Thickness(8, 0, 0, 0); ramLine.Children.Add(_gpuVRAMTxt);
            infoStack.Children.Add(ramLine);

            var tempLine = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
            tempLine.Children.Add(MakeText("Temperature:", 12, Theme.TextSecondary, false));
            _gpuDetail = MakeText("--", 12, Colors.White, true);
            _gpuDetail.Margin = new Thickness(8, 0, 0, 0); tempLine.Children.Add(_gpuDetail);
            infoStack.Children.Add(tempLine);

            var usageLine = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };
            usageLine.Children.Add(MakeText("Usage:", 12, Theme.TextSecondary, false));
            _gpuUsageTxt = MakeText("0%", 12, Theme.Accent, true);
            _gpuUsageTxt.Margin = new Thickness(8, 0, 0, 0); usageLine.Children.Add(_gpuUsageTxt);
            infoStack.Children.Add(usageLine);

            infoCard.Child = infoStack;
            panel.Children.Add(infoCard);

            // Graph card
            var graphCard = MakeCard(18, null);
            var graphStack = new StackPanel { Margin = new Thickness(24) };
            graphStack.Children.Add(MakeText("GPU Usage History", 16, Colors.White, true));
            var gpuGraph = new Grid { Height = 120, Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)), Margin = new Thickness(0, 8, 0, 0) };
            _gpuPageGraph = gpuGraph;
            graphStack.Children.Add(gpuGraph);
            graphCard.Child = graphStack;
            panel.Children.Add(graphCard);

            root.Content = panel;
            return root;
        }

        private UIElement BuildHealthSafetyPage()
        {
            var root = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var panel = new StackPanel { Margin = new Thickness(32, 16, 32, 16) };

            // Title
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
            titleRow.Children.Add(new Border { Width = 4, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Theme.Green), Margin = new Thickness(0, 4, 12, 4) });
            titleRow.Children.Add(MakeText("الصحة والأمان", 28, Colors.White, true));
            panel.Children.Add(titleRow);
            panel.Children.Add(MakeText("مراقبة حالة النظام والحماية من المشاكل", 13, Theme.TextSecondary, false));
            panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Margin = new Thickness(0, 16, 0, 16) });

            // === Thermal Protection ===
            panel.Children.Add(MakeSectionHeader("\uE9CA", "حماية الحرارة", Theme.Warning));
            var thermalCard = MakeCard(14, null);
            var thermalInner = new StackPanel { Margin = new Thickness(20) };
            bool thermalOk = !_agent.IsThermalThrottled;
            double cpuTemp = _agent.CurrentState.CpuTemp;
            _hsThermalStatus = MakeText(thermalOk ? "النظام آمن" : "⚠️ التبريد نشط", 16, thermalOk ? Theme.Green : Theme.Warning, true);
            thermalInner.Children.Add(_hsThermalStatus);
            _hsTempVal = MakeText(string.Format("درجة حرارة CPU: {0:F1}°C", cpuTemp), 13, Theme.TextSecondary, false);
            thermalInner.Children.Add(_hsTempVal);
            // Temp progress bar
            var tempBarOuter = new Border { Height = 10, CornerRadius = new CornerRadius(5), Background = new SolidColorBrush(Theme.SubCard), Margin = new Thickness(0, 12, 0, 4) };
            double tempPercent = Math.Min(100, cpuTemp);
            Color tempBarColor = cpuTemp > 80 ? Theme.Error : cpuTemp > 65 ? Theme.Warning : Theme.Green;
            _hsTempBar = new Border { Width = tempPercent * 2.2, Height = 10, CornerRadius = new CornerRadius(5), HorizontalAlignment = HorizontalAlignment.Left, Background = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0), GradientStops = { new GradientStop(tempBarColor, 0), new GradientStop(Color.FromArgb(180, tempBarColor.R, tempBarColor.G, tempBarColor.B), 1) } }, Effect = new DropShadowEffect { Color = tempBarColor, BlurRadius = 8, ShadowDepth = 0, Opacity = 0.4 } };
            tempBarOuter.Child = _hsTempBar; thermalInner.Children.Add(tempBarOuter);
            thermalInner.Children.Add(MakeText(string.Format("الحد الأقصى: 80°C — الحالة: {0}", thermalOk ? "آمن" : "محدود"), 11, Theme.TextMuted, false));
            thermalCard.Child = thermalInner; panel.Children.Add(thermalCard);

            // === Battery Status ===
            panel.Children.Add(MakeSectionHeader("\uE83F", "حالة البطارية", Theme.Cyan));
            var batteryCard = MakeCard(14, null);
            var batteryInner = new StackPanel { Margin = new Thickness(20) };
            if (_agent.BatteryPercent < 0)
            {
                batteryInner.Children.Add(MakeText("كمبيوتر ثابت", 16, Theme.TextSecondary, true));
                batteryInner.Children.Add(MakeText("لا توجد بطارية — يعمل على الكهرباء مباشرة", 13, Theme.TextMuted, false));
                _hsBatVal = null; _hsBatMode = null; _hsBatBar = null;
            }
            else
            {
                Color batColor = _agent.IsOnBattery ? Theme.Warning : Theme.Green;
                string batMode = _agent.IsOnBattery ? "وضع توفير الطاقة" : "وضع الأداء الكامل";
                _hsBatVal = MakeText(string.Format("{0}%", _agent.BatteryPercent), 28, batColor, true);
                batteryInner.Children.Add(_hsBatVal);
                _hsBatMode = MakeText(batMode, 13, Theme.TextSecondary, false);
                batteryInner.Children.Add(_hsBatMode);
                // Battery progress bar
                var batBarOuter = new Border { Height = 10, CornerRadius = new CornerRadius(5), Background = new SolidColorBrush(Theme.SubCard), Margin = new Thickness(0, 12, 0, 4) };
                Color batBarColor = _agent.BatteryPercent > 50 ? Theme.Green : _agent.BatteryPercent > 20 ? Theme.Warning : Theme.Error;
                _hsBatBar = new Border { Width = _agent.BatteryPercent * 2.2, Height = 10, CornerRadius = new CornerRadius(5), HorizontalAlignment = HorizontalAlignment.Left, Background = new SolidColorBrush(batBarColor), Effect = new DropShadowEffect { Color = batBarColor, BlurRadius = 8, ShadowDepth = 0, Opacity = 0.4 } };
                batBarOuter.Child = _hsBatBar; batteryInner.Children.Add(batBarOuter);
            }
            batteryCard.Child = batteryInner; panel.Children.Add(batteryCard);

            // === Disk Health ===
            panel.Children.Add(MakeSectionHeader("\uE74E", "صحة القرص", Theme.Purple));
            var diskCard = MakeCard(14, null);
            var diskInner = new StackPanel { Margin = new Thickness(20) };
            Color diskColor = _agent.DiskHealthPercent > 80 ? Theme.Green : _agent.DiskHealthPercent > 50 ? Theme.Warning : Theme.Error;
            _hsDiskVal = MakeText(string.Format("{0:F0}%", _agent.DiskHealthPercent), 28, diskColor, true);
            diskInner.Children.Add(_hsDiskVal);
            _hsDiskStatus = MakeText(_agent.DiskHealthStatus, 13, Theme.TextSecondary, false);
            diskInner.Children.Add(_hsDiskStatus);
            var diskBarOuter = new Border { Height = 10, CornerRadius = new CornerRadius(5), Background = new SolidColorBrush(Theme.SubCard), Margin = new Thickness(0, 12, 0, 4) };
            _hsDiskBar = new Border { Width = _agent.DiskHealthPercent * 2.2, Height = 10, CornerRadius = new CornerRadius(5), HorizontalAlignment = HorizontalAlignment.Left, Background = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 0), GradientStops = { new GradientStop(diskColor, 0), new GradientStop(Theme.Purple, 1) } }, Effect = new DropShadowEffect { Color = diskColor, BlurRadius = 8, ShadowDepth = 0, Opacity = 0.4 } };
            diskBarOuter.Child = _hsDiskBar; diskInner.Children.Add(diskBarOuter);
            diskCard.Child = diskInner; panel.Children.Add(diskCard);

            // === Network Monitor ===
            panel.Children.Add(MakeSectionHeader("\uE968", "مراقبة الشبكة", Theme.Accent));
            var netCard = MakeCard(14, null);
            var netInner = new StackPanel { Margin = new Thickness(20) };
            var netGrid = new Grid();
            netGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            netGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            // Download
            var dlStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            dlStack.Children.Add(MakeText("⬇", 20, Theme.Green, false));
            _hsNetDown = MakeText(string.Format("{0:F1}", _agent.NetDownloadKBps), 22, Colors.White, true);
            dlStack.Children.Add(_hsNetDown);
            dlStack.Children.Add(MakeText("KB/s تحميل", 11, Theme.TextMuted, false));
            Grid.SetColumn(dlStack, 0); netGrid.Children.Add(dlStack);
            // Upload
            var ulStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            ulStack.Children.Add(MakeText("⬆", 20, Theme.Purple, false));
            _hsNetUp = MakeText(string.Format("{0:F1}", _agent.NetUploadKBps), 22, Colors.White, true);
            ulStack.Children.Add(_hsNetUp);
            ulStack.Children.Add(MakeText("KB/s رفع", 11, Theme.TextMuted, false));
            Grid.SetColumn(ulStack, 1); netGrid.Children.Add(ulStack);
            netInner.Children.Add(netGrid);
            netCard.Child = netInner; panel.Children.Add(netCard);

            // === Speedometer ===
            panel.Children.Add(MakeSectionHeader("\uE9D9", "قياس الأداء", Theme.Green));
            var speedCard = MakeCard(14, null);
            var speedInner = new StackPanel { Margin = new Thickness(20) };
            string speedResult = string.IsNullOrEmpty(_agent.LastOptimizationResult) ? "لا توجد بيانات بعد" : _agent.LastOptimizationResult;
            _hsSpeedResult = MakeText(speedResult, 13, Theme.TextPrimary, false);
            speedInner.Children.Add(_hsSpeedResult);
            speedCard.Child = speedInner; panel.Children.Add(speedCard);

            // === Process Alerts ===
            panel.Children.Add(MakeSectionHeader("\uE7BA", "عمليات جديدة مكتشفة", Theme.Warning));
            var hogCard = MakeCard(14, null);
            _hsHogStack = new StackPanel { Margin = new Thickness(20) };
            var hogAlerts = _agent.NewHogAlerts;
            if (hogAlerts.Count == 0)
            {
                _hsHogStack.Children.Add(MakeText("لا توجد عمليات جديدة مكتشفة", 13, Theme.TextMuted, false));
            }
            else
            {
                for (int i = hogAlerts.Count - 1; i >= Math.Max(0, hogAlerts.Count - 5); i--)
                {
                    var alertRow = new Border { Background = new SolidColorBrush(Color.FromArgb(15, Theme.Warning.R, Theme.Warning.G, Theme.Warning.B)), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 3, 0, 3), BorderBrush = new SolidColorBrush(Color.FromArgb(30, Theme.Warning.R, Theme.Warning.G, Theme.Warning.B)), BorderThickness = new Thickness(1) };
                    alertRow.Child = MakeText("⚠ " + hogAlerts[i], 11, Theme.Warning, false);
                    _hsHogStack.Children.Add(alertRow);
                }
            }
            hogCard.Child = _hsHogStack; panel.Children.Add(hogCard);

            // === Optimization Log ===
            panel.Children.Add(MakeSectionHeader("\uE8D4", "سجل التحسينات", Theme.Accent));
            var logCard = MakeCard(14, null);
            var logInner = new StackPanel { Margin = new Thickness(20) };
            var optLogs = _agent.OptimizationLog;
            int logCount = Math.Min(8, optLogs.Count);
            if (optLogs.Count == 0)
            {
                logInner.Children.Add(MakeText("لا توجد سجلات بعد", 13, Theme.TextMuted, false));
            }
            else
            {
                for (int i = optLogs.Count - 1; i >= Math.Max(0, optLogs.Count - logCount); i--)
                {
                    var logEntry = MakeText("• " + optLogs[i], 11, Theme.TextSecondary, false);
                    logEntry.Margin = new Thickness(0, 3, 0, 0);
                    logInner.Children.Add(logEntry);
                }
            }
            logCard.Child = logInner; panel.Children.Add(logCard);

            // === Undo Stack ===
            panel.Children.Add(MakeSectionHeader("\uE749", "عمليات التحسين الأخيرة", Theme.Cyan));
            var undoCard = MakeCard(14, null);
            var undoInner = new StackPanel { Margin = new Thickness(20) };
            var undoStack = _agent.UndoStack;
            int undoCount = Math.Min(5, undoStack.Count);
            if (undoStack.Count == 0)
            {
                undoInner.Children.Add(MakeText("لا توجد عمليات", 13, Theme.TextMuted, false));
            }
            else
            {
                for (int i = undoStack.Count - 1; i >= Math.Max(0, undoStack.Count - undoCount); i--)
                {
                    var undoRow = new Border { Background = new SolidColorBrush(Color.FromArgb(12, Theme.Cyan.R, Theme.Cyan.G, Theme.Cyan.B)), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 3, 0, 3) };
                    undoRow.Child = MakeText("↻ " + undoStack[i], 11, Theme.Cyan, false);
                    undoInner.Children.Add(undoRow);
                }
            }
            undoCard.Child = undoInner; panel.Children.Add(undoCard);

            root.Content = panel;
            return root;
        }
    }
}
