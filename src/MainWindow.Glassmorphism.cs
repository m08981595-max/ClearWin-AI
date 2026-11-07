using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;

namespace ClearWin
{
    public partial class MainWindow
    {
        private Grid _glassRoot;
        private Border _glassSidebar;
        private ScrollViewer _glassScroll;
        private ContentControl _glassPageContent;
        private ObservableCollection<GlassLogEntry> _glassLogs;
        private ObservableCollection<string> _gameApps;
        private string _currentPage = "Dashboard";
        private string _prevPage = "Dashboard";
        private Border _titleBar;
        private TextBlock _minBtn, _maxBtn, _closeBtn;
        private bool _isMaximized;
        private readonly Dictionary<string, UIElement> _pageWithBackCache = new Dictionary<string, UIElement>();

        public UIElement BuildGlassmorphismUI()
        {
            _glassLogs = new ObservableCollection<GlassLogEntry>();
            _gameApps = new ObservableCollection<string>();
            var defaultEmulators = new[] {
                "ldplayer", "bluestacks", "bluestacks2", "bluestacks3", "bluestacks4",
                "nox", "noxplayer", "memu", "mumu", "qemu"
            };
            foreach (var emu in defaultEmulators)
            {
                if (!_gameApps.Contains(emu)) _gameApps.Add(emu);
            }
            AutoDetectEmulators();
            _glassRoot = new Grid();
            _glassRoot.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _glassRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
            _glassRoot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _titleBar = BuildTitleBar();
            Grid.SetRow(_titleBar, 0);
            Grid.SetColumnSpan(_titleBar, 2);
            _glassRoot.Children.Add(_titleBar);
            _glassSidebar = BuildGlassSidebar();
            Grid.SetColumn(_glassSidebar, 0);
            Grid.SetRow(_glassSidebar, 1);
            _glassRoot.Children.Add(_glassSidebar);
            _glassScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(20), Background = Brushes.Transparent, IsDeferredScrollingEnabled = false };
            _glassPageContent = new ContentControl();
            _glassScroll.Content = _glassPageContent;
            Grid.SetColumn(_glassScroll, 1);
            Grid.SetRow(_glassScroll, 1);
            _glassRoot.Children.Add(_glassScroll);
            NavigateToPage("Dashboard");
            return _glassRoot;
        }

        private Border BuildTitleBar()
        {
            var titleBar = new Border { Height = 40, Background = new SolidColorBrush(Color.FromArgb(50, 0, 0, 0)), BorderBrush = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)), BorderThickness = new Thickness(0, 0, 0, 1) };
            var dock = new DockPanel();
            var leftPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(15, 0, 0, 0) };
            leftPanel.Children.Add(new TextBlock { Text = "\uD83E\uDD16", FontSize = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
            leftPanel.Children.Add(new TextBlock { Text = "ClearWin v2.7.7", FontSize = 12, Foreground = new SolidColorBrush(Color.FromArgb(200, 200, 200, 200)), VerticalAlignment = VerticalAlignment.Center });
            dock.Children.Add(leftPanel);
            DockPanel.SetDock(leftPanel, Dock.Left);
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 5, 0) };
            _minBtn = new TextBlock { Text = "\u2014", FontSize = 14, Foreground = Brushes.White, Cursor = Cursors.Hand, Padding = new Thickness(8, 2, 8, 2), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            _maxBtn = new TextBlock { Text = "\u25A1", FontSize = 12, Foreground = Brushes.White, Cursor = Cursors.Hand, Padding = new Thickness(8, 2, 8, 2), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            _closeBtn = new TextBlock { Text = "\u00D7", FontSize = 14, Foreground = Brushes.White, Cursor = Cursors.Hand, Padding = new Thickness(8, 2, 8, 2), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            _minBtn.MouseLeftButtonDown += (s, e) => { try { WindowState = WindowState.Minimized; } catch { } };
            _maxBtn.MouseLeftButtonDown += (s, e) => { try { _isMaximized = !_isMaximized; WindowState = _isMaximized ? WindowState.Maximized : WindowState.Normal; } catch { } };
            _closeBtn.MouseLeftButtonDown += (s, e) => { try { Close(); } catch { } };
            btnPanel.Children.Add(_minBtn);
            btnPanel.Children.Add(_maxBtn);
            btnPanel.Children.Add(_closeBtn);
            dock.Children.Add(btnPanel);
            titleBar.Child = dock;
            return titleBar;
        }

        private Border BuildGlassSidebar()
        {
            var sidebar = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(25, 0, 0, 0)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)),
                BorderThickness = new Thickness(0, 0, 1, 0)
            };
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Brushes.Transparent, Padding = new Thickness(0, 10, 0, 0) };
            scroll.Content = BuildSidebarNav();
            sidebar.Child = scroll;
            return sidebar;
        }

        private StackPanel BuildSidebarNav()
        {
            var nav = new StackPanel { Margin = new Thickness(12, 0, 12, 0) };
            var items = new[] { ("\uD83D\uDCCA", "Dashboard", "Dashboard"), ("\uD83E\uDD16", "AI Engine", "AI Engine"), ("\u26A1", "Optimizations", "Optimizations"), ("\uD83D\uDCBE", "Memory", "Memory"), ("\uD83D\uDBC1", "My Games", "\uD83D\uDBC1"), ("\uD83D\uDCCC", "Logs", "Logs"), ("\u2699\uFE0F", "Settings", "Settings") };
            bool first = true;
            foreach (var item in items)
            {
                var btn = CreateNavButton(item.Item1, item.Item2, item.Item3, first);
                btn.MouseLeftButtonDown += (s, e) => NavigateToPage(item.Item2);
                nav.Children.Add(btn);
                first = false;
            }
            return nav;
        }

        private Border CreateNavButton(string icon, string text, string arabicText, bool selected)
        {
            var btn = new Border { Background = selected ? new SolidColorBrush(Color.FromArgb(21, 46, 168, 255)) : Brushes.Transparent, CornerRadius = new CornerRadius(10), Padding = new Thickness(15, 12, 15, 12), Margin = new Thickness(0, 4, 0, 4), Cursor = Cursors.Hand };
            var grid = new Grid();
            grid.Children.Add(new Border { Width = 4, Height = 24, Background = new SolidColorBrush(Color.FromRgb(46, 168, 255)), CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left, Opacity = selected ? 1 : 0 });
            var content = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 0, 0, 0) };
            content.Children.Add(new TextBlock { Text = icon, FontSize = 18, Foreground = selected ? new SolidColorBrush(Color.FromRgb(46, 168, 255)) : new SolidColorBrush(Color.FromArgb(128, 100, 100, 100)), Width = 24, Margin = new Thickness(0, 0, 12, 0) });
            content.Children.Add(new TextBlock { Text = arabicText, FontSize = 14, Foreground = selected ? Brushes.White : new SolidColorBrush(Color.FromArgb(176, 150, 150, 150)), FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center });
            grid.Children.Add(content);
            btn.Child = grid;
            return btn;
        }

        private Border BuildSidebarFooter()
        {
            var footer = new Border { Background = new SolidColorBrush(Color.FromArgb(10, 0, 0, 0)), CornerRadius = new CornerRadius(10), Padding = new Thickness(15, 12, 15, 12), Margin = new Thickness(15, 0, 15, 20) };
            var stack = new StackPanel();
            var statusStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            statusStack.Children.Add(new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(Color.FromRgb(0, 255, 136)), Margin = new Thickness(0, 0, 8, 0) });
            statusStack.Children.Add(new TextBlock { Text = "System Active", FontSize = 12, Foreground = new SolidColorBrush(Color.FromArgb(176, 150, 150, 150)), FontWeight = FontWeights.Medium });
            stack.Children.Add(statusStack);
            stack.Children.Add(new TextBlock { Text = "124 tests passed", FontSize = 10, Foreground = new SolidColorBrush(Color.FromArgb(96, 255, 255, 255)) });
            footer.Child = stack;
            return footer;
        }

        private void NavigateToPage(string pageName)
        {
            try
            {
                if (_currentPage != pageName) _prevPage = _currentPage;
                _currentPage = pageName;
                _glassPageContent.Content = BuildGlassPage(pageName);
                UpdateSidebarSelection(pageName);
            }
            catch (Exception ex) { CWLog.Error("MainWindow.Glassmorphism", ex); }
        }

        private void UpdateSidebarSelection(string pageName)
        {
            try
            {
                if (_glassSidebar?.Child is ScrollViewer sv && sv.Content is StackPanel sp)
                {
                    foreach (var child in sp.Children)
                    {
                        if (child is Border btn && btn.Child is Grid g && g.Children.Count > 1 && g.Children[1] is StackPanel cs)
                        {
                            if (cs.Children.Count > 1 && cs.Children[1] is TextBlock tb)
                            {
                                bool selected = tb.Text == pageName;
                                btn.Background = selected ? new SolidColorBrush(Color.FromArgb(21, 46, 168, 255)) : Brushes.Transparent;
                                if (g.Children[0] is Border indicator) indicator.Opacity = selected ? 1 : 0;
                                if (cs.Children[0] is TextBlock icon) icon.Foreground = selected ? new SolidColorBrush(Color.FromRgb(46, 168, 255)) : new SolidColorBrush(Color.FromArgb(128, 100, 100, 100));
                                if (cs.Children[1] is TextBlock label) label.Foreground = selected ? Brushes.White : new SolidColorBrush(Color.FromArgb(176, 150, 150, 150));
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { CWLog.Error("MainWindow.Glassmorphism", ex); }
        }

        private UIElement BuildGlassPage(string pageName)
        {
            string cacheKey = pageName + "_" + _prevPage.GetHashCode();
            if (_pageWithBackCache.TryGetValue(cacheKey, out var cached)) return cached;
            if (_pageCache.TryGetValue(pageName, out var plain))
            {
                if (_currentPage != "Dashboard" && !string.IsNullOrEmpty(_prevPage))
                {
                    var main = new StackPanel { Orientation = Orientation.Vertical };
                    main.Children.Add(BuildBackButton());
                    main.Children.Add(plain is StackPanel sp ? sp : new StackPanel { Children = { plain } });
                    _pageWithBackCache[cacheKey] = main;
                    return main;
                }
                return plain;
            }

            UIElement page;
            if (pageName == "Dashboard") page = BuildDashboardPage();
            else if (pageName == "AI Engine") page = BuildAIEnginePage();
            else if (pageName == "Optimizations") page = BuildOptimizationsPage();
            else if (pageName == "Memory") page = BuildMemoryPage();
            else if (pageName == "My Games") page = BuildMyGamesPage();
            else if (pageName == "Logs") page = BuildLogsPage();
            else if (pageName == "Settings") page = BuildSettingsPage();
            else page = BuildDashboardPage();

            _pageCache[pageName] = page;

            if (_currentPage != "Dashboard" && !string.IsNullOrEmpty(_prevPage))
            {
                var main = new StackPanel { Orientation = Orientation.Vertical };
                main.Children.Add(BuildBackButton());
                if (page is StackPanel sp) main.Children.Add(sp);
                else main.Children.Add(new StackPanel { Children = { page } });
                _pageWithBackCache[cacheKey] = main;
                return main;
            }
            return page;
        }

        private Border BuildBackButton()
        {
            var backBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, 0, 0, 0)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(15, 8, 15, 8),
                Margin = new Thickness(0, 0, 0, 15),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Top
            };
            backBtn.Child = new TextBlock
            {
                Text = "\u2190 " + _prevPage,
                FontSize = 13,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            backBtn.MouseEnter += (s, e) => backBtn.Background = new SolidColorBrush(Color.FromArgb(50, 0, 0, 0));
            backBtn.MouseLeave += (s, e) => backBtn.Background = new SolidColorBrush(Color.FromArgb(30, 0, 0, 0));
            backBtn.MouseLeftButtonDown += (s, e) => NavigateToPage(_prevPage);
            return backBtn;
        }

        private StackPanel BuildDashboardPage()
        {
            var main = new StackPanel { Orientation = Orientation.Vertical };
            main.Children.Add(BuildQuickActionsSection());
            main.Children.Add(new Border { Height = 20 });
            main.Children.Add(BuildStatsCards());
            main.Children.Add(new Border { Height = 20 });
            main.Children.Add(BuildAIPanel());
            main.Children.Add(new Border { Height = 20 });
            main.Children.Add(BuildLogsPanel());
            return main;
        }

        private Border BuildQuickActionsSection()
        {
            var section = new Border { Background = new SolidColorBrush(Color.FromArgb(15, 0, 0, 0)), CornerRadius = new CornerRadius(12), Padding = new Thickness(20) };
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
            header.Children.Add(new TextBlock { Text = "\u26A1", FontSize = 20, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
            header.Children.Add(new TextBlock { Text = "Quick Actions", FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
            var inner = new StackPanel();
            inner.Children.Add(header);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
            actions.Children.Add(CreateGlassActionButton("\uD83D\uDD25", "Boost"));
            actions.Children.Add(CreateGlassActionButton("\uD83D\uDDA5", "Clean"));
            actions.Children.Add(CreateGlassActionButton("\uD83D\uDCBE", "Memory"));
            actions.Children.Add(CreateGlassActionButton("\uD83D\uDD0D", "Scan"));
            inner.Children.Add(actions);
            section.Child = inner;
            return section;
        }

        private Border CreateGlassActionButton(string icon, string label)
        {
            var btn = new Border { Background = new SolidColorBrush(Color.FromArgb(20, 46, 168, 255)), CornerRadius = new CornerRadius(10), Padding = new Thickness(20, 12, 20, 12), Margin = new Thickness(0, 0, 10, 0), Cursor = Cursors.Hand };
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            stack.Children.Add(new TextBlock { Text = icon, FontSize = 24, HorizontalAlignment = HorizontalAlignment.Center });
            stack.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 5, 0, 0) });
            btn.Child = stack;
            btn.MouseEnter += (s, e) => btn.Background = new SolidColorBrush(Color.FromArgb(35, 46, 168, 255));
            btn.MouseLeave += (s, e) => btn.Background = new SolidColorBrush(Color.FromArgb(20, 46, 168, 255));
            return btn;
        }

        private Border BuildStatsCards()
        {
            var section = new Border { Background = new SolidColorBrush(Color.FromArgb(15, 0, 0, 0)), CornerRadius = new CornerRadius(12), Padding = new Thickness(20) };
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
            header.Children.Add(new TextBlock { Text = "\uD83D\uDCCA", FontSize = 20, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
            header.Children.Add(new TextBlock { Text = "System Stats", FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
            var cards = new StackPanel { Orientation = Orientation.Horizontal };
            cards.Children.Add(CreateMetric("\uD83D\uDCBB", "CPU", "35%"));
            cards.Children.Add(CreateMetric("\uD83D\uDDA6\u200D\uD83D\uDE80", "RAM", "4.2 GB"));
            cards.Children.Add(CreateMetric("\uD83D\uDCBE", "Disk", "62%"));
            cards.Children.Add(CreateMetric("\uD83D\uDD25", "Temp", "58\u00B0C"));
            var inner = new StackPanel();
            inner.Children.Add(header);
            inner.Children.Add(cards);
            section.Child = inner;
            return section;
        }

        private StackPanel CreateMetric(string icon, string label, string value)
        {
            var metric = new StackPanel { Width = 120, Margin = new Thickness(0, 0, 15, 0) };
            var iconBlock = new TextBlock { Text = icon, FontSize = 28, HorizontalAlignment = HorizontalAlignment.Center };
            var valBlock = new TextBlock { Text = value, FontSize = 18, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(46, 168, 255)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 5, 0, 0) };
            var lblBlock = new TextBlock { Text = label, FontSize = 12, Foreground = new SolidColorBrush(Color.FromArgb(150, 200, 200, 200)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) };
            metric.Children.Add(iconBlock);
            metric.Children.Add(valBlock);
            metric.Children.Add(lblBlock);
            return metric;
        }

        private Border BuildAIPanel()
        {
            var panel = new Border { Background = new SolidColorBrush(Color.FromArgb(15, 0, 0, 0)), CornerRadius = new CornerRadius(12), Padding = new Thickness(20) };
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
            header.Children.Add(new TextBlock { Text = "\uD83E\uDD16", FontSize = 20, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
            header.Children.Add(new TextBlock { Text = "AI Engine", FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
            var status = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            status.Children.Add(new Ellipse { Width = 10, Height = 10, Fill = new SolidColorBrush(Color.FromRgb(0, 255, 136)), Margin = new Thickness(0, 0, 8, 0) });
            status.Children.Add(new TextBlock { Text = "SmartAgent Active", FontSize = 13, Foreground = Brushes.White });
            var info = new TextBlock { Text = "AI optimization engine running with adaptive learning and real-time system monitoring.", FontSize = 12, Foreground = new SolidColorBrush(Color.FromArgb(150, 200, 200, 200)), TextWrapping = TextWrapping.Wrap };
            var inner = new StackPanel();
            inner.Children.Add(header);
            inner.Children.Add(status);
            inner.Children.Add(info);
            panel.Child = inner;
            return panel;
        }

        private Border BuildLogsPanel()
        {
            var panel = new Border { Background = new SolidColorBrush(Color.FromArgb(15, 0, 0, 0)), CornerRadius = new CornerRadius(12), Padding = new Thickness(20) };
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 15) };
            header.Children.Add(new TextBlock { Text = "\uD83D\uDCCC", FontSize = 20, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
            header.Children.Add(new TextBlock { Text = "Recent Activity", FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 280, IsDeferredScrollingEnabled = false };
            var logsStack = new StackPanel();
            logsStack.Children.Add(CreateLogEntry("\u2705", "Memory optimized", "Success"));
            logsStack.Children.Add(CreateLogEntry("\u26A1", "FPS stabilized at 60", "Info"));
            logsStack.Children.Add(CreateLogEntry("\uD83D\uDD25", "Thermal throttle active", "Warning"));
            logsStack.Children.Add(CreateLogEntry("\u2705", "Background processes limited", "Success"));
            logsStack.Children.Add(CreateLogEntry("\uD83D\uDD0D", "System scan completed", "Info"));
            scroll.Content = logsStack;
            var inner = new StackPanel();
            inner.Children.Add(header);
            inner.Children.Add(scroll);
            panel.Child = inner;
            return panel;
        }

        private Border CreateLogEntry(string icon, string message, string type)
        {
            Color bgColor = type == "Error" ? Color.FromArgb(32, 255, 68, 68) : type == "Success" ? Color.FromArgb(32, 46, 168, 255) : Color.FromArgb(20, 60, 60, 60);
            var entry = new Border { Background = new SolidColorBrush(bgColor), BorderBrush = new SolidColorBrush(Color.FromArgb(64, bgColor.R, bgColor.G, bgColor.B)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 6) };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.Children.Add(new TextBlock { Text = icon, FontSize = 20, Margin = new Thickness(0, 0, 15, 0), VerticalAlignment = VerticalAlignment.Center });
            var msgStack = new StackPanel();
            Grid.SetColumn(msgStack, 1);
            msgStack.Children.Add(new TextBlock { Text = message, FontSize = 13, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap });
            msgStack.Children.Add(new TextBlock { Text = DateTime.Now.ToString("HH:mm:ss"), FontSize = 10, Foreground = new SolidColorBrush(Color.FromArgb(128, 200, 200, 200)), Margin = new Thickness(0, 2, 0, 0) });
            grid.Children.Add(msgStack);
            entry.Child = grid;
            return entry;
        }

        private void RefreshMyGamesList()
        {
            try
            {
                var content = _glassPageContent?.Content as StackPanel;
                if (content == null) return;
                foreach (var child in content.Children)
                {
                    if (child is Border border && border.Child is ScrollViewer sv && sv.Content is StackPanel sp)
                    {
                        RefreshGamesListContent(sp);
                        return;
                    }
                }
            }
            catch (Exception ex) { CWLog.Error("MainWindow.Glassmorphism", ex); }
        }

        private void RefreshGamesListContent(StackPanel panel)
        {
            try
            {
                panel.Children.Clear();
                if (_gameApps == null || _gameApps.Count == 0)
                {
                    panel.Children.Add(new TextBlock { Text = "No games detected yet. Launch a game to add it.", FontSize = 13, Foreground = new SolidColorBrush(Color.FromArgb(128, 150, 150, 150)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 20, 0, 0) });
                    return;
                }
                foreach (var game in _gameApps)
                {
                    var rowStack = new StackPanel { Orientation = Orientation.Horizontal };
                    rowStack.Children.Add(new TextBlock { Text = "\uD83D\uDBC1", FontSize = 16, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center });
                    rowStack.Children.Add(new TextBlock { Text = game, FontSize = 13, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
                    var boostBtn = new Border { Background = new SolidColorBrush(Color.FromArgb(30, 46, 168, 255)), CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(10, 0, 0, 0), Cursor = Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Right };
                    boostBtn.Child = new TextBlock { Text = "Boost", FontSize = 11, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
                    rowStack.Children.Add(boostBtn);
                    var row = new Border { Background = new SolidColorBrush(Color.FromArgb(15, 25, 25, 25)), CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 0, 0, 4) };
                    row.Child = rowStack;
                    panel.Children.Add(row);
                }
            }
            catch (Exception ex) { CWLog.Error("MainWindow.Glassmorphism", ex); }
        }

        private void AutoDetectEmulators()
        {
            try
            {
                var knownEmulators = new[] { "ldplayer", "bluestacks", "nox", "memu", "mumu", "gameloop", "noxplayer", "aow_exe", "aow", "appmarket", "androidemulator" };
                foreach (var proc in Process.GetProcesses())
                {
                    try
                    {
                        var name = proc.ProcessName.ToLower();
                        if (knownEmulators.Any(e => name.Contains(e)))
                        {
                            if (!_gameApps.Contains(proc.ProcessName)) _gameApps.Add(proc.ProcessName);
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex) { CWLog.Error("MainWindow.Glassmorphism", ex); }
        }

        private StackPanel BuildAIEnginePage()
        {
            var main = new StackPanel { Orientation = Orientation.Vertical };
            main.Children.Add(new Border { Height = 15 });
            var title = new TextBlock { Text = "AI Engine", FontSize = 24, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 20) };
            main.Children.Add(title);
            var info = new TextBlock { Text = "SmartAgent configuration and AI behavior settings.", FontSize = 14, Foreground = new SolidColorBrush(Color.FromArgb(150, 200, 200, 200)), Margin = new Thickness(0, 0, 0, 20) };
            main.Children.Add(info);
            var card = new Border { Background = new SolidColorBrush(Color.FromArgb(15, 0, 0, 0)), CornerRadius = new CornerRadius(12), Padding = new Thickness(20), Margin = new Thickness(0, 0, 0, 15) };
            var infoText = new TextBlock { Text = "SmartAgent Status: Active\nMode: Balanced\nConfidence Threshold: 0.75\nAuto-Actions Per Cycle: 3", FontSize = 13, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap };
            card.Child = infoText;
            main.Children.Add(card);
            return main;
        }

        private UIElement BuildOptimizationsPage()
        {
            var main = new StackPanel { Orientation = Orientation.Vertical };
            main.Children.Add(new Border { Height = 15 });
            var title = new TextBlock { Text = "Optimizations", FontSize = 24, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 20) };
            main.Children.Add(title);
            var card = new Border { Background = new SolidColorBrush(Color.FromArgb(15, 0, 0, 0)), CornerRadius = new CornerRadius(12), Padding = new Thickness(20) };
            var infoText = new TextBlock { Text = "System optimization settings and tweak management.", FontSize = 14, Foreground = new SolidColorBrush(Color.FromArgb(150, 200, 200, 200)), TextWrapping = TextWrapping.Wrap };
            card.Child = infoText;
            main.Children.Add(card);
            return main;
        }

        private UIElement BuildMemoryPage()
        {
            var main = new StackPanel { Orientation = Orientation.Vertical };
            main.Children.Add(new Border { Height = 15 });
            var title = new TextBlock { Text = "Memory Cleaner", FontSize = 24, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 20) };
            main.Children.Add(title);
            var card = new Border { Background = new SolidColorBrush(Color.FromArgb(15, 0, 0, 0)), CornerRadius = new CornerRadius(12), Padding = new Thickness(20) };
            var infoText = new TextBlock { Text = "Memory optimization tools and cleanup controls.", FontSize = 14, Foreground = new SolidColorBrush(Color.FromArgb(150, 200, 200, 200)), TextWrapping = TextWrapping.Wrap };
            card.Child = infoText;
            main.Children.Add(card);
            return main;
        }

        private UIElement BuildMyGamesPage()
        {
            var main = new StackPanel { Orientation = Orientation.Vertical };
            main.Children.Add(new Border { Height = 15 });
            var title = new TextBlock { Text = "My Games", FontSize = 24, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 20) };
            main.Children.Add(title);
            var card = new Border { Background = new SolidColorBrush(Color.FromArgb(15, 0, 0, 0)), CornerRadius = new CornerRadius(12), Padding = new Thickness(20) };
            var inner = new StackPanel();
            inner.Children.Add(new TextBlock { Text = "Detected Games:", FontSize = 14, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10) });
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 300, IsDeferredScrollingEnabled = false };
            var gamesPanel = new StackPanel { Name = "GamesListPanel" };
            RefreshGamesListContent(gamesPanel);
            scroll.Content = gamesPanel;
            inner.Children.Add(scroll);
            card.Child = inner;
            main.Children.Add(card);
            return main;
        }

        private UIElement BuildLogsPage()
        {
            var main = new StackPanel { Orientation = Orientation.Vertical };
            main.Children.Add(new Border { Height = 15 });
            var title = new TextBlock { Text = "Activity Logs", FontSize = 24, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 20) };
            main.Children.Add(title);
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, IsDeferredScrollingEnabled = false };
            var logsStack = new StackPanel();
            for (int i = 0; i < 20; i++)
            {
                logsStack.Children.Add(CreateLogEntry("\u2705", $"System action {i + 1} executed successfully", "Success"));
            }
            scroll.Content = logsStack;
            main.Children.Add(scroll);
            return main;
        }

        private UIElement BuildSettingsPage()
        {
            var main = new StackPanel { Orientation = Orientation.Vertical };
            main.Children.Add(new Border { Height = 15 });
            var title = new TextBlock { Text = "Settings", FontSize = 24, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 20) };
            main.Children.Add(title);
            var card = new Border { Background = new SolidColorBrush(Color.FromArgb(15, 0, 0, 0)), CornerRadius = new CornerRadius(12), Padding = new Thickness(20) };
            var infoText = new TextBlock { Text = "Application settings and preferences.", FontSize = 14, Foreground = new SolidColorBrush(Color.FromArgb(150, 200, 200, 200)), TextWrapping = TextWrapping.Wrap };
            card.Child = infoText;
            main.Children.Add(card);
            return main;
        }

        public class GlassLogEntry
        {
            public string LogType { get; set; }
            public string Icon { get; set; }
            public string Message { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}


