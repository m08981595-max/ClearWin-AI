using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Management;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Shapes;
using Microsoft.Win32;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ClearWin
{
    public enum OverlayPosition
    {
        TopRight, TopLeft, BottomRight, BottomLeft
    }

    public class OverlayWindow : Window
    {
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")]
        static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);
        [DllImport("kernel32.dll")]
        static extern bool QueryPerformanceFrequency(out long frequency);
        [DllImport("kernel32.dll")]
        static extern bool QueryPerformanceCounter(out long count);
        [DllImport("psapi.dll")]
        static extern bool EmptyWorkingSet(IntPtr hProcess);
        [DllImport("ntdll.dll")]
        static extern uint NtSetSystemInformation(int SystemInformationClass, IntPtr SystemInformation, int SystemInformationLength);
        [DllImport("kernel32.dll")]
        static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential)]
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

        enum AccentState { ACCENT_DISABLED = 0, ACCENT_ENABLE_GRADIENT = 1, ACCENT_ENABLE_TRANSPARENTGRADIENT = 2, ACCENT_ENABLE_BLURBEHIND = 3, ACCENT_ENABLE_ACRYLICBLURBEHIND = 4 }

        [StructLayout(LayoutKind.Sequential)]
        struct AccentPolicy { public AccentState AccentState; public uint AccentFlags; public uint GradientColor; public uint AnimationId; }

        [StructLayout(LayoutKind.Sequential)]
        struct WindowCompositionAttributeData { public int Attribute; public IntPtr Data; public int SizeOfData; }

        const int GWL_EXSTYLE = -20;
        const int WS_EX_TOOLWINDOW = 0x80;
        const int WS_EX_NOACTIVATE = 0x08000000;
        const int WS_EX_TRANSPARENT = 0x20;
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        const uint SWP_NOMOVE = 0x0002, SWP_NOSIZE = 0x0001, SWP_SHOWWINDOW = 0x0040;
        const int SystemMemoryListInformation = 80;

        private RingBuffer<double> _fpsHistory = new RingBuffer<double>(MAX_HISTORY);
        private RingBuffer<double> _cpuHistory = new RingBuffer<double>(MAX_HISTORY);
        private RingBuffer<double> _gpuHistory = new RingBuffer<double>(MAX_HISTORY);
        private RingBuffer<double> _ramHistory = new RingBuffer<double>(MAX_HISTORY);
        private const int MAX_HISTORY = 40;

        private Border[] _metricCards;
        private TextBlock[] _metricValues;
        private Polyline[] _sparklines;
        private Color[] _metricColors;
        private Color[] _baseMetricColors;
        private string[] _metricNames;
        private bool[] _metricVisible = new bool[] { true, true, true, true, true, true };
        private TextBlock _statusMsg;

        private void UpdateMetricVisibility()
        {
            if (_metricCards == null) return;
            for (int i = 0; i < _metricCards.Length && i < _metricVisible.Length; i++)
                _metricCards[i].Visibility = _metricVisible[i] ? Visibility.Visible : Visibility.Collapsed;
            if (_statusMsg != null)
                _statusMsg.Visibility = _metricVisible[5] ? Visibility.Visible : Visibility.Collapsed;
        }

        public void SetMetricVisible(int index, bool visible)
        {
            if (index >= 0 && index < _metricVisible.Length)
            {
                _metricVisible[index] = visible;
                UpdateMetricVisibility();
            }
        }

        private TextBlock _headerTitle;
        private Border _closeBtn;
        private StackPanel _buttonPanel;
        private Border[] _btnBorders;
        private Grid _mainGrid;

        private double _lastCpu = 0, _lastRam = 0, _lastGpu = 0, _lastVramMB = 0, _lastFps = 0;
        private double _fpsMin = 999, _fpsMax = 0, _fpsSum = 0, _fpsCount = 0;
        private double _cpuTemp = 0, _gpuTemp = 0;
        private Queue<double> _fps60 = new Queue<double>(60);
        private string _gpuName = "";
        private PerformanceCounter[] _gpuCounters = new PerformanceCounter[0];
        private PerformanceCounter _vramCounter = null;
        private PerformanceCounter _cpuTempPc = null, _gpuTempPc = null;
        private PerformanceCounter _overlayCpuPc = null;
        private long _qpcFrequency = 0;
        private long _lastFrameQpc = 0;
        private double _smoothFps = 60;
        private bool _buttonsVisible = false;
        private bool _clickThrough = true;
        private OverlayPosition _position = OverlayPosition.TopRight;
        private double _panelOpacity = 0.78;
        private DispatcherTimer _statsTimer;
        private DispatcherTimer _keyTimer;
        private IntPtr _hwnd = IntPtr.Zero;

        public OverlayWindow()
        {
            Topmost = true; ShowInTaskbar = false; WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = null;
            ResizeMode = ResizeMode.NoResize;
            Width = 570; Height = 155;
            WindowStartupLocation = WindowStartupLocation.Manual;
            ApplyPosition();

            _metricNames = new[] { "FPS", "CPU", "GPU", "RAM" };
            _metricColors = new[] {
                Color.FromRgb(34, 197, 94), Color.FromRgb(59, 130, 246),
                Color.FromRgb(236, 72, 153), Color.FromRgb(139, 92, 246)
            };
            _baseMetricColors = (Color[])_metricColors.Clone();
            _metricCards = new Border[4];
            _metricValues = new TextBlock[4];
            _sparklines = new Polyline[4];

            _mainGrid = new Grid { Margin = new Thickness(0) };
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var headerGrid = new Grid { Cursor = Cursors.SizeAll };
            headerGrid.Background = new SolidColorBrush(Color.FromArgb(12, 255, 255, 255));
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var dot = new Ellipse { Width = 8, Height = 8, Fill = new SolidColorBrush(Color.FromRgb(59, 130, 246)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 6, 0) };
            Grid.SetColumn(dot, 0); headerGrid.Children.Add(dot);
            _headerTitle = new TextBlock { Text = "ClearWin", FontSize = 11, Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(_headerTitle, 0); headerGrid.Children.Add(_headerTitle);

            _closeBtn = new Border { Width = 28, Height = 28, Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)), Cursor = Cursors.Hand };
            var closeX = new TextBlock { Text = "\u2715", FontSize = 12, Foreground = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            _closeBtn.Child = closeX;
            _closeBtn.MouseEnter += (s, e) => { _closeBtn.Background = new SolidColorBrush(Color.FromArgb(60, 255, 60, 60)); closeX.Foreground = Brushes.White; };
            _closeBtn.MouseLeave += (s, e) => { _closeBtn.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0)); closeX.Foreground = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)); };
            _closeBtn.MouseDown += (s, e) => Close();
            Grid.SetColumn(_closeBtn, 2); headerGrid.Children.Add(_closeBtn);

            headerGrid.MouseDown += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed && !_clickThrough) try { DragMove(); } catch (Exception ex) { CWLog.Error("OverlayWindow", ex); } };
            Grid.SetRow(headerGrid, 0);
            _mainGrid.Children.Add(headerGrid);

            var metricsRow = new Grid { Margin = new Thickness(6, 4, 6, 2) };
            for (int i = 0; i < 4; i++)
                metricsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var card = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(8, 255, 255, 255)),
                    CornerRadius = new CornerRadius(6),
                    Margin = new Thickness(2),
                    Padding = new Thickness(4, 2, 4, 2),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(18, _metricColors[i].R, _metricColors[i].G, _metricColors[i].B)),
                    BorderThickness = new Thickness(1)
                };
                var inner = new Grid();
                inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                inner.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24) });
                inner.RowDefinitions.Add(new RowDefinition { Height = new GridLength(20) });

                var label = new TextBlock
                {
                    Text = _metricNames[i], FontSize = 8,
                    Foreground = new SolidColorBrush(Color.FromArgb(180, _metricColors[i].R, _metricColors[i].G, _metricColors[i].B)),
                    FontWeight = FontWeights.SemiBold, Margin = new Thickness(2, 0, 0, 0)
                };
                Grid.SetRow(label, 0); inner.Children.Add(label);

                _metricValues[i] = new TextBlock
                {
                    Text = "--",
                    FontSize = i == 0 ? 18 : 14,
                    Foreground = new SolidColorBrush(_metricColors[i]),
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(2, -2, 0, 0)
                };
                Grid.SetRow(_metricValues[i], 1); inner.Children.Add(_metricValues[i]);

                _sparklines[i] = new Polyline
                {
                    Stroke = new SolidColorBrush(Color.FromArgb(180, _metricColors[i].R, _metricColors[i].G, _metricColors[i].B)),
                    StrokeThickness = 1.2, StrokeLineJoin = PenLineJoin.Round,
                    StrokeEndLineCap = PenLineCap.Round, Width = 70, Height = 18,
                    HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(2, 0, 0, 0)
                };
                Grid.SetRow(_sparklines[i], 2); inner.Children.Add(_sparklines[i]);

                card.Child = inner;
                _metricCards[i] = card;
                Grid.SetColumn(card, i);
                metricsRow.Children.Add(card);
            }

            Grid.SetRow(metricsRow, 1);
            _mainGrid.Children.Add(metricsRow);

            var bottomStack = new StackPanel { Margin = new Thickness(8, 0, 8, 4) };

            _statusMsg = new TextBlock { Text = "", FontSize = 9, Foreground = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)), Margin = new Thickness(0, 0, 0, 2), Height = 14, TextTrimming = TextTrimming.CharacterEllipsis };

            var btnRow = new Grid();
            for (int i = 0; i < 3; i++) btnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            string[][] btnData = new[] { new[]{"RAM","34,197,94"}, new[]{"Boost","59,130,246"}, new[]{"DNS","139,92,246"} };
            _btnBorders = new Border[8];
            for (int i = 0; i < 3; i++)
            {
                int ii = i; var c = ParseColor(btnData[i][1]);
                var b = MakeBtn(btnData[i][0], c);
                _btnBorders[i] = b;
                b.MouseDown += (s, e) =>
                {
                    FlashBtn(b);
                    if (ii == 0)
                    {
                        _statusMsg.Text = "\u231B \u062C\u0627\u0631\u064A...";
                        Task.Factory.StartNew(() =>
                        {
                            try { CleanAllWorkingSets(); PurgeStandbyList(); } catch (Exception ex2) { CWLog.Error("OverlayWindow", ex2); }
                            Dispatcher.BeginInvoke(new Action(() => { _statusMsg.Text = "\u2713 RAM + Standby"; }));
                        });
                    }
                    else if (ii == 1)
                    {
                        _statusMsg.Text = "\u231B \u062C\u0627\u0631\u064A...";
                        Task.Factory.StartNew(() =>
                        {
                            try
                            {
                                CleanAllWorkingSets(); PurgeStandbyList();
                                IntPtr fg = GetForegroundWindow(); uint pid; GetWindowThreadProcessId(fg, out pid);
                                if (pid > 0) { using (var pp = Process.GetProcessById((int)pid)) { if (!pp.HasExited) pp.PriorityClass = ProcessPriorityClass.High; } }
                            } catch (Exception ex3) { CWLog.Error("OverlayWindow", ex3); }
                            Dispatcher.BeginInvoke(new Action(() => { _statusMsg.Text = "\u2713 Boost \u062A\u0645 \u0628\u0646\u062C\u0627\u062D"; }));
                        });
                    }
                    else if (ii == 2) { _statusMsg.Text = "\u231B DNS..."; Task.Factory.StartNew(() => { string dnsMsgOvw; bool dnsOkOvw = MainWindow.TryFlushDnsVerified(out dnsMsgOvw); Dispatcher.BeginInvoke(new Action(() => { _statusMsg.Text = dnsOkOvw ? "\u2713 DNS" : "\u2717 DNS"; })); }); }
                    ClearStatusAfter(3);
                };
                Grid.SetColumn(b, i); btnRow.Children.Add(b);
            }

            var fpsBtnRow = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            for (int i = 0; i < 5; i++) fpsBtnRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            int[] fpsVals = new[] { 30, 60, 90, 120, 0 };
            string[] fpsLbls = new[] { "30", "60", "90", "120", "MAX" };
            Color[] fpsCols = new[] { Color.FromRgb(251,146,60), Color.FromRgb(236,72,153), Color.FromRgb(168,85,247), Color.FromRgb(59,130,246), Color.FromRgb(34,197,94) };
            for (int i = 0; i < 5; i++)
            {
                int fi = i; var c2 = fpsCols[i];
                var b2 = MakeBtn(fpsLbls[i], c2);
                _btnBorders[3 + i] = b2;
                b2.MouseDown += (s, e) =>
                {
                    FlashBtn(b2);
                    int limit = fpsVals[fi];
                    if (limit > 0)
                    {
                        try { using (var rk = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\NVIDIA Corporation\Global\NVTweak", true)) { rk.SetValue("MaxFrameRate", limit, RegistryValueKind.DWord); } } catch (Exception ex5) { CWLog.Error("OverlayWindow", ex5); }
                        try { using (var rk2 = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\DirectX\GraphicsSettings", true)) { rk2.SetValue("D3D11FrameRateLimit", limit, RegistryValueKind.DWord); } } catch (Exception ex6) { CWLog.Error("OverlayWindow", ex6); }
                        _statusMsg.Text = string.Format("\u2713 FPS \u0645\u062D\u062F\u0648\u062F \u0625\u0644\u0649 {0}", limit);
                    }
                    else
                    {
                        try { using (var r = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\NVIDIA Corporation\Global\NVTweak", true)) { if (r != null) { r.DeleteValue("MaxFrameRate"); } } } catch (Exception ex7) { CWLog.Error("OverlayWindow", ex7); }
                        try { using (var r = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\DirectX\GraphicsSettings", true)) { if (r != null) { r.DeleteValue("D3D11FrameRateLimit"); } } } catch (Exception ex8) { CWLog.Error("OverlayWindow", ex8); }
                        _statusMsg.Text = "\u2713 FPS \u063A\u064A\u0631 \u0645\u062D\u062F\u0648\u062F";
                    }
                    ClearStatusAfter(3);
                };
                Grid.SetColumn(b2, i); fpsBtnRow.Children.Add(b2);
            }

            _buttonPanel = new StackPanel { Visibility = Visibility.Collapsed };
            _buttonPanel.Children.Add(btnRow);
            _buttonPanel.Children.Add(fpsBtnRow);

            bottomStack.Children.Add(_statusMsg);
            bottomStack.Children.Add(_buttonPanel);

            Grid.SetRow(bottomStack, 2);
            _mainGrid.Children.Add(bottomStack);

            Content = _mainGrid;

            if (!QueryPerformanceFrequency(out _qpcFrequency)) _qpcFrequency = 0;
            QueryPerformanceCounter(out _lastFrameQpc);
            CompositionTarget.Rendering += OnFrame;

            InitGpuCounters();
            InitTempCounters();
            try { _overlayCpuPc = new PerformanceCounter("Processor", "% Processor Time", "_Total"); _overlayCpuPc.NextValue(); } catch (Exception ex9) { CWLog.Error("OverlayWindow", ex9); }

            _statsTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000) };
            _statsTimer.Tick += UpdateStats;
            _statsTimer.Start();

            _keyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _keyTimer.Tick += (sk, ek) =>
            {
                if (IsVisible && (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) && Keyboard.IsKeyDown(Key.F12))
                {
                    _buttonsVisible = !_buttonsVisible;
                    _buttonPanel.Visibility = _buttonsVisible ? Visibility.Visible : Visibility.Collapsed;
                    Height = _buttonsVisible ? 200 : 155;
                    ApplyPosition();
                    ToggleClickThrough(!_buttonsVisible);
                    _keyTimer.Stop();
                    var restore = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                    restore.Tick += (sr, er) => { _keyTimer.Start(); restore.Stop(); };
                    restore.Start();
                }
            };
            _keyTimer.Start();

            Closed += OnClosed;
            SourceInitialized += OnSourceInitialized;
        }

        private void ToggleClickThrough(bool enable)
        {
            _clickThrough = enable;
            if (_hwnd == IntPtr.Zero) return;
            try
            {
                int exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
                if (enable) exStyle |= WS_EX_TRANSPARENT;
                else exStyle &= ~WS_EX_TRANSPARENT;
                SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle);
            } catch (Exception ex10) { CWLog.Error("OverlayWindow", ex10); }
        }

        private void UpdateSparkline(int idx, Polyline line, RingBuffer<double> history, double scale)
        {
            if (history.Count < 2) return;
            double w = 70, h = 18, pad = 1;
            double aw = w - pad * 2, ah = h - pad * 2;
            int count = history.Count;
            var pts = new PointCollection(count);
            for (int i = 0; i < count; i++)
            {
                double x = pad + (double)i / (count - 1) * aw;
                double val = Math.Min(history[i], scale);
                pts.Add(new Point(x, pad + ah - (val / scale * ah)));
            }
            line.Points = pts;
        }

        private int _displayHz = 60;

        private void OnFrame(object sender, EventArgs e)
        {
            if (_qpcFrequency == 0) return;
            long now;
            QueryPerformanceCounter(out now);
            double elapsed = (double)(now - _lastFrameQpc) / _qpcFrequency;
            _lastFrameQpc = now;
            if (elapsed > 0.001 && elapsed < 0.5)
            {
                double fps = 1.0 / elapsed;
                if (fps > 10 && fps < 500)
                {
                    _smoothFps = _smoothFps * 0.8 + fps * 0.2;
                    // Update display Hz estimate only when FPS is close to a common refresh rate
                    int measured = (int)Math.Round(_smoothFps);
                    if (measured >= 110 && measured <= 150) _displayHz = 144;
                    else if (measured >= 55 && measured <= 65) _displayHz = 60;
                    else if (measured >= 28 && measured <= 35) _displayHz = 30;
                    else if (measured >= 140 && measured <= 165) _displayHz = 165;
                    else if (measured >= 220 && measured <= 260) _displayHz = 240;
                    else if (_displayHz < 30) _displayHz = 60;
                }
            }
        }

        private void InitGpuCounters()
        {
            try
            {
                var cat = new PerformanceCounterCategory("GPU Engine");
                var list = cat.GetInstanceNames().Where(n => n.Contains("engtype_3D")).ToArray();
                if (list.Length > 0)
                    _gpuCounters = list.Select(inst => { var pc = new PerformanceCounter("GPU Engine", "Utilization Percentage", inst); pc.NextValue(); return pc; }).ToArray();
            } catch (Exception ex11) { CWLog.Error("OverlayWindow", ex11); }
            try
            {
                using (var gq = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController WHERE NOT Name LIKE '%RDP%' AND NOT Name LIKE '%Basic%'"))
                    foreach (ManagementObject mo in gq.Get()) { _gpuName = Convert.ToString(mo["Name"]); break; }
            } catch (Exception ex12) { CWLog.Error("OverlayWindow", ex12); }
            try
            {
                var memCat = new PerformanceCounterCategory("GPU Local Adapter Memory");
                foreach (string inst in memCat.GetInstanceNames())
                    if (!inst.Contains("00018F14"))
                    { _vramCounter = new PerformanceCounter("GPU Local Adapter Memory", "Local Usage", inst); _vramCounter.NextValue(); break; }
            } catch (Exception ex13) { CWLog.Error("OverlayWindow", ex13); }
        }

        private void InitTempCounters()
        {
            string[] cpuZones = new[] { @"\_TZ.CPUZ", @"\_TZ.CPU0", @"\_TZ.TZ00", @"\_TZ.TZ01" };
            foreach (string z in cpuZones)
                try { _cpuTempPc = new PerformanceCounter("Thermal Zone Information", "Temperature", z); _cpuTempPc.NextValue(); break; } catch { _cpuTempPc = null; }
            string[] gpuZones = new[] { @"\_TZ.GFXZ", @"\_TZ.GFX0", @"\_TZ.GPUZ", @"\_TZ.GPU0" };
            foreach (string z in gpuZones)
                try { _gpuTempPc = new PerformanceCounter("Thermal Zone Information", "Temperature", z); _gpuTempPc.NextValue(); break; } catch { _gpuTempPc = null; }
        }

        private string ReadTemp(PerformanceCounter pc)
        {
            if (pc == null) return "--";
            try
            {
                float r = pc.NextValue();
                if (r <= 0) return "--";
                double c = r / 10.0;
                if (c > 200) c -= 273.15;
                if (c > 0 && c < 150) return string.Format("{0:F0}\u00B0", c);
            } catch (Exception ex14) { CWLog.Error("OverlayWindow", ex14); }
            return "--";
        }

        private double GetGpuUtilization()
        {
            double maxGpu = 0;
            foreach (var pc in _gpuCounters)
                try { float v = pc.NextValue(); if (v >= 0 && v <= 100 && v > maxGpu) maxGpu = v; } catch (Exception ex15) { CWLog.Error("OverlayWindow", ex15); }
            return maxGpu > 0 ? maxGpu : 0;
        }

        void UpdateStats(object s, EventArgs e)
        {
            try { if (_overlayCpuPc != null) { float v = _overlayCpuPc.NextValue(); if (v >= 0) _lastCpu = v; } } catch (Exception ex16) { CWLog.Error("OverlayWindow", ex16); }
            try { MEMORYSTATUSEX mem = new MEMORYSTATUSEX(); mem.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)); if (GlobalMemoryStatusEx(ref mem)) { _lastRam = Math.Round(((double)(mem.ullTotalPhys - mem.ullAvailPhys) / mem.ullTotalPhys) * 100.0); } } catch (Exception ex17) { CWLog.Error("OverlayWindow", ex17); }

            _lastGpu = Math.Min(100, Math.Max(0, GetGpuUtilization()));
            if (_vramCounter != null) { try { _lastVramMB = Math.Round(_vramCounter.NextValue() / (1024.0 * 1024.0)); } catch (Exception ex18) { CWLog.Error("OverlayWindow", ex18); } }
            _cpuTemp = 0; _gpuTemp = 0;
            string cpuTxt = ReadTemp(_cpuTempPc), gpuTxt = ReadTemp(_gpuTempPc);
            double.TryParse(cpuTxt.Replace("\u00B0", ""), out _cpuTemp);
            double.TryParse(gpuTxt.Replace("\u00B0", ""), out _gpuTemp);

            // Use real measured FPS from OnFrame QPC timing
            double fpsDisp = _smoothFps > 0 ? Math.Round(_smoothFps) : 0;

            _metricValues[0].Text = string.Format("{0:F0}", fpsDisp);
            _fpsCount++; _fpsSum += fpsDisp;
            if (fpsDisp < _fpsMin) _fpsMin = fpsDisp;
            if (fpsDisp > _fpsMax) _fpsMax = fpsDisp;
            if (_fpsCount > 500) { _fpsMin = fpsDisp; _fpsMax = fpsDisp; _fpsSum = fpsDisp; _fpsCount = 1; }
            _lastFps = fpsDisp;
            _fpsHistory.Add(fpsDisp);
            UpdateSparkline(0, _sparklines[0], _fpsHistory, _displayHz + 20);

            // CPU / GPU / RAM
            _metricValues[1].Text = string.Format("{0:F0}%", _lastCpu);
            _cpuHistory.Add(_lastCpu);
            UpdateSparkline(1, _sparklines[1], _cpuHistory, 100);

            _metricValues[2].Text = _lastGpu > 0 ? string.Format("{0:F0}%", _lastGpu) : "--";
            _gpuHistory.Add(_lastGpu);
            UpdateSparkline(2, _sparklines[2], _gpuHistory, 100);

            _metricValues[3].Text = string.Format("{0:F0}%", _lastRam);
            _ramHistory.Add(_lastRam);
            UpdateSparkline(3, _sparklines[3], _ramHistory, 100);

            // 60-second rolling average
            _fps60.Enqueue(fpsDisp);
            if (_fps60.Count > 60) _fps60.Dequeue();
            double avg60 = _fps60.Average();

            // Status bar
            string vramText = _vramCounter != null ? string.Format("VRAM: {0:F0}MB", _lastVramMB) : "";
            string statusLine = string.Format("avg: {0:F0}  |  CPU: {1}  GPU: {2}  {3}", avg60, cpuTxt, gpuTxt, vramText);
            _statusMsg.Text = statusLine.Trim(' ', '|');

            // Tooltip
            string mn = _fpsMin < 999 ? string.Format("{0:F0}", _fpsMin) : "--";
            string mx2 = _fpsMax > 0 ? string.Format("{0:F0}", _fpsMax) : "--";
            string lf = _fpsCount > 0 ? string.Format("{0:F0}", _fpsSum / _fpsCount) : "--";
            _metricCards[0].ToolTip = string.Format("FPS: {0} / {1} / {2}  (60s: {3:F0})\n{4}  @ {5}Hz", mn, lf, mx2, avg60, _gpuName, _displayHz);
        }

        void OnSourceInitialized(object s, EventArgs e)
        {
            try
            {
                _hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                int exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
                exStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT;
                SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle);
                SetWindowPos(_hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                try
                {
                    var accent = new AccentPolicy { AccentState = AccentState.ACCENT_ENABLE_BLURBEHIND, AccentFlags = 0, GradientColor = 0 };
                    var d = new WindowCompositionAttributeData { Attribute = 19, Data = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(AccentPolicy))), SizeOfData = Marshal.SizeOf(typeof(AccentPolicy)) };
                    try
                    {
                        Marshal.StructureToPtr(accent, d.Data, false);
                        SetWindowCompositionAttribute(_hwnd, ref d);
                    }
                    finally { Marshal.FreeHGlobal(d.Data); }
                } catch (Exception ex19) { CWLog.Error("OverlayWindow", ex19); }
            } catch (Exception ex20) { CWLog.Error("OverlayWindow", ex20); }
        }

        public void SetPosition(OverlayPosition pos) { _position = pos; ApplyPosition(); }
        public void SetOpacity(double opacity) { _panelOpacity = opacity; ApplyOpacity(); }
        public void SetTextTint(Color tint)
        {
            try
            {
                if (_baseMetricColors == null || _metricValues == null) return;
                for (int i = 0; i < _baseMetricColors.Length; i++)
                {
                    Color c = Blend(_baseMetricColors[i], tint, 0.35);
                    _metricColors[i] = c;
                    if (_metricValues[i] != null) _metricValues[i].Foreground = new SolidColorBrush(c);
                }
            }
            catch (Exception ex21) { CWLog.Error("OverlayWindow", ex21); }
        }

        private static Color Blend(Color a, Color b, double t)
        {
            byte r = (byte)(a.R + (b.R - a.R) * t);
            byte g = (byte)(a.G + (b.G - a.G) * t);
            byte bl = (byte)(a.B + (b.B - a.B) * t);
            return Color.FromRgb(r, g, bl);
        }

        private void ApplyOpacity()
        {
            // With AllowsTransparency=true, panel opacity is per-element
        }

        private void ApplyPosition()
        {
            double sw = SystemParameters.PrimaryScreenWidth, sh = SystemParameters.PrimaryScreenHeight, m = 8;
            switch (_position)
            {
                case OverlayPosition.TopRight: Left = sw - Width - m; Top = m; break;
                case OverlayPosition.TopLeft: Left = m; Top = m; break;
                case OverlayPosition.BottomRight: Left = sw - Width - m; Top = sh - Height - m; break;
                case OverlayPosition.BottomLeft: Left = m; Top = sh - Height - m; break;
            }
        }

        private void ClearStatusAfter(int seconds)
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
            t.Tick += (s, e) => { _statusMsg.Text = ""; t.Stop(); }; t.Start();
        }

        private void FlashBtn(Border btn)
        {
            try
            {
                var orig = btn.Background;
                btn.Background = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
                var restore = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                restore.Tick += (s, e) => { btn.Background = orig; restore.Stop(); }; restore.Start();
            } catch (Exception ex22) { CWLog.Error("OverlayWindow", ex22); }
        }

        private Color ParseColor(string s) { var p = s.Split(','); return Color.FromRgb(byte.Parse(p[0]), byte.Parse(p[1]), byte.Parse(p[2])); }

        private Border MakeBtn(string text, Color color)
        {
            var btn = new Border { Background = new SolidColorBrush(Color.FromArgb(25, color.R, color.G, color.B)), CornerRadius = new CornerRadius(4), Padding = new Thickness(2, 1, 2, 1), Margin = new Thickness(1), Cursor = Cursors.Hand };
            btn.Child = new TextBlock { Text = text, FontSize = 9, Foreground = new SolidColorBrush(color), FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center };
            return btn;
        }

        private bool IsLikelyGameForeground()
        {
            try
            {
                IntPtr fg = GetForegroundWindow();
                if (fg == IntPtr.Zero) return false;
                uint pid; GetWindowThreadProcessId(fg, out pid);
                if (pid == 0) return false;
                using (var p = Process.GetProcessById((int)pid))
                {
                    string n = (p.ProcessName ?? "").ToLowerInvariant();
                    return n.Contains("gameloop") || n.Contains("gameloop") || n.Contains("ldplayer") ||
                           n.Contains("nox") || n.Contains("memu") || n.Contains("bluestacks") ||
                           n.Contains("freefire") || n.Contains("pubg") || n.Contains("steam") && !n.Contains("steamwebhelper");
                }
            }
            catch { return false; }
        }

        private void PurgeStandbyList()
        {
            if (IsLikelyGameForeground())
            {
                try { if (_statusMsg != null) _statusMsg.Text = "تنظيف RAM مؤجل أثناء اللعب لحماية سلاسة الإطارات"; } catch (Exception ex23) { CWLog.Error("OverlayWindow", ex23); }
                return;
            }
            IntPtr p = IntPtr.Zero;
            try
            {
                int cmd = 4;
                p = Marshal.AllocHGlobal(4);
                Marshal.WriteInt32(p, cmd);
                NtSetSystemInformation(SystemMemoryListInformation, p, 4);
            } catch (Exception ex24) { CWLog.Error("OverlayWindow", ex24); }
            finally { if (p != IntPtr.Zero) Marshal.FreeHGlobal(p); }
        }

        private void CleanAllWorkingSets()
        {
            Process[] procs = Process.GetProcesses();
            try
            {
                foreach (var p in procs)
                {
                    try { if (!p.HasExited) EmptyWorkingSet(p.Handle); } catch (Exception ex25) { CWLog.Error("OverlayWindow", ex25); }
                }
            }
            finally
            {
                foreach (var p in procs) { try { p.Dispose(); } catch (Exception ex26) { CWLog.Error("OverlayWindow", ex26); } }
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        void OnClosed(object s, EventArgs e)
        {
            if (_statsTimer != null) { _statsTimer.Stop(); _statsTimer = null; }
            if (_keyTimer != null) { _keyTimer.Stop(); _keyTimer = null; }
            CompositionTarget.Rendering -= OnFrame;
            foreach (var pc in _gpuCounters) { try { pc.Dispose(); } catch (Exception ex27) { CWLog.Error("OverlayWindow", ex27); } }
            _gpuCounters = new PerformanceCounter[0];
            if (_vramCounter != null) { try { _vramCounter.Dispose(); } catch (Exception ex28) { CWLog.Error("OverlayWindow", ex28); } _vramCounter = null; }
            if (_cpuTempPc != null) { try { _cpuTempPc.Dispose(); } catch (Exception ex29) { CWLog.Error("OverlayWindow", ex29); } _cpuTempPc = null; }
            if (_gpuTempPc != null) { try { _gpuTempPc.Dispose(); } catch (Exception ex30) { CWLog.Error("OverlayWindow", ex30); } _gpuTempPc = null; }
            if (_overlayCpuPc != null) { try { _overlayCpuPc.Dispose(); } catch (Exception ex31) { CWLog.Error("OverlayWindow", ex31); } _overlayCpuPc = null; }
        }
    }
}
