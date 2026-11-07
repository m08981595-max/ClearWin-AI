using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Windows.Media.Effects;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace ClearWin
{
    public partial class MainWindow
    {
        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        const int GWL_EXSTYLE = -20;
        const int WS_EX_TOPMOST = 0x8;
        const int WS_EX_TOOLWINDOW = 0x00000080;
        const int WS_EX_APPWINDOW = 0x00040000;
        const uint SWP_NOMOVE = 0x0002, SWP_NOSIZE = 0x0001, SWP_NOACTIVATE = 0x0010;
        const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern uint RegisterWindowMessage(string lpString);
        internal static readonly uint WM_CLEARWIN_RESTORE = RegisterWindowMessage("CLEARWIN_RESTORE_WINDOW");

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MonitorInfo
        {
            public int CbSize;
            public NativeRect RcMonitor;
            public NativeRect RcWork;
            public uint DwFlags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

        private bool _customMaximized;
        private Rect _restoreBounds;
        private bool _restoreBoundsValid;

        private void EnsureNormalWindowZOrder()
        {
            try
            {
                Topmost = false;
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;
                int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
                ex &= ~WS_EX_TOPMOST;
                ex &= ~WS_EX_TOOLWINDOW;
                ex |= WS_EX_APPWINDOW;
                SetWindowLong(hwnd, GWL_EXSTYLE, ex);
                SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }
            catch (Exception ex2) { CWLog.Error("MainWindow.Window", ex2); }
        }

        private Rect GetCurrentMonitorWorkArea()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return SystemParameters.WorkArea;
            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            var mi = new MonitorInfo();
            mi.CbSize = Marshal.SizeOf(typeof(MonitorInfo));
            if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref mi)) return SystemParameters.WorkArea;

            var fromDevice = PresentationSource.FromVisual(this);
            if (fromDevice != null && fromDevice.CompositionTarget != null)
            {
                try
                {
                    var tx = fromDevice.CompositionTarget.TransformFromDevice;
                    Point tl = tx.Transform(new Point(mi.RcWork.Left, mi.RcWork.Top));
                    Point br = tx.Transform(new Point(mi.RcWork.Right, mi.RcWork.Bottom));
                    return new Rect(tl, br);
                }
                catch (Exception ex) { CWLog.Error("MainWindow.Overlay", ex); }
            }
            return new Rect(mi.RcWork.Left, mi.RcWork.Top, mi.RcWork.Right - mi.RcWork.Left, mi.RcWork.Bottom - mi.RcWork.Top);
        }

        private void MaximizeToWorkArea()
        {
            try
            {
                EnsureNormalWindowZOrder();
                if (!_customMaximized)
                {
                    if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
                    _restoreBounds = new Rect(Left, Top, Width, Height);
                    _restoreBoundsValid = _restoreBounds.Width > 0 && _restoreBounds.Height > 0;
                }
                var work = GetCurrentMonitorWorkArea();
                WindowState = WindowState.Normal;
                Left = work.Left;
                Top = work.Top;
                Width = work.Width;
                Height = work.Height;
                _customMaximized = true;
                if (_maxBtnTxt != null) _maxBtnTxt.Text = "\uE923";
            }
            catch (Exception ex2) { CWLog.Error("MainWindow.Window", ex2); }
        }

        private void RestoreFromWorkArea()
        {
            try
            {
                WindowState = WindowState.Normal;
                if (_restoreBoundsValid)
                {
                    Left = _restoreBounds.Left;
                    Top = _restoreBounds.Top;
                    Width = Math.Max(MinWidth, _restoreBounds.Width);
                    Height = Math.Max(MinHeight, _restoreBounds.Height);
                }
                _customMaximized = false;
                if (_maxBtnTxt != null) _maxBtnTxt.Text = "\uE922";
                EnsureNormalWindowZOrder();
            }
            catch (Exception ex3) { CWLog.Error("MainWindow.Window", ex3); }
        }

        private void ToggleMaxRestoreWindow()
        {
            if (_customMaximized) RestoreFromWorkArea();
            else MaximizeToWorkArea();
        }

        private void OnMainSourceInitialized(object s, EventArgs e)
        {
            try
            {
                var helper = new System.Windows.Interop.WindowInteropHelper(this);
                EnsureNormalWindowZOrder();
                _hookDelegate = new HwndSourceHook(WndProc);
                var source = System.Windows.Interop.HwndSource.FromHwnd(helper.Handle);
                if (source != null) source.AddHook(_hookDelegate);
                RegisterHotKey(helper.Handle, HOTKEY_ID, MOD_CONTROL | MOD_SHIFT, VK_F9);
                RegisterHotKey(helper.Handle, HOTKEY_ID_KILLSWITCH, MOD_CONTROL | MOD_SHIFT, VK_F10);
                RegisterHotKey(helper.Handle, HOTKEY_ID_PAUSE, MOD_CONTROL | MOD_SHIFT, VK_F11);
                RegisterHotKey(helper.Handle, HOTKEY_ID_EMERGENCY, MOD_CONTROL | MOD_SHIFT, VK_F8);
            } catch (Exception ex4) { CWLog.Error("MainWindow.Overlay", ex4); }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if ((uint)msg == WM_CLEARWIN_RESTORE)
            {
                ShowFromTray();
                handled = true;
                return IntPtr.Zero;
            }

            if (msg == WM_HOTKEY)
            {
                int id = (int)wParam;
                if (id == HOTKEY_ID)
                {
                    ToggleOverlayVisibility();
                    handled = true;
                }
                else if (id == HOTKEY_ID_KILLSWITCH)
                {
                    if (_agent.IsKillSwitchActive) { _agent.ResetKillSwitch(); _agent.SetMode(AgentMode.AutoPilot); ShowNotification("Kill Switch OFF", "AI resumed"); }
                    else { _agent.EmergencyKillSwitch(); ShowNotification("KILL SWITCH ON", "AI stopped"); }
                    handled = true;
                }
                else if (id == HOTKEY_ID_PAUSE)
                {
                    if (_agent.IsPaused) { _agent.ResumeAgent(); ShowNotification("AI Resumed", "Agent unpaused"); }
                    else { _agent.PauseAgent(60); ShowNotification("AI Paused", "60 ticks (~2 min)"); }
                    handled = true;
                }
                else if (id == HOTKEY_ID_EMERGENCY)
                {
                    _agent.EmergencyStopAllOptimizations();
                    ShowNotification("EMERGENCY STOP", "All optimizations reverted");
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        private void ToggleOverlayVisibility()
        {
            if (_overlayWindow != null)
            {
                HideOverlay();
            }
            else
            {
                if (!_overlayEnabled) _overlayEnabled = true;
                ShowOverlay();
            }
        }

        private void ShowOverlay()
        {
            if (_overlayWindow == null)
            {
                _overlayWindow = new OverlayWindow();
                _overlayWindow.SetPosition(_overlayPosition);
                _overlayWindow.SetOpacity(_overlayOpacity);
                _overlayWindow.SetMetricVisible(0, _overlayShowFPS);
                _overlayWindow.SetMetricVisible(1, _overlayShowCPU);
                _overlayWindow.SetMetricVisible(2, _overlayShowGPU);
                _overlayWindow.SetMetricVisible(3, _overlayShowRAM);
                _overlayWindow.SetMetricVisible(4, _overlayShowTemp);
                _overlayWindow.SetMetricVisible(5, _overlayShowClock);
                _overlayWindow.SetTextTint(_overlayTextColor);
                _overlayWindow.Show();
            }
        }

        private void HideOverlay()
        {
            if (_overlayWindow != null)
            {
                try { _overlayWindow.Close(); } catch (Exception ex5) { CWLog.Error("MainWindow.Overlay", ex5); }
                _overlayWindow = null;
            }
        }

        // ===================== TOAST NOTIFICATION =====================
        private void AddHoverLift(Border target)
        {
            var transform = new TranslateTransform();
            target.RenderTransform = transform;
            target.RenderTransformOrigin = new Point(0.5, 0.5);
            target.MouseEnter += (s, e) =>
            {
                var anim = new DoubleAnimation(0, -3, TimeSpan.FromMilliseconds(120), FillBehavior.HoldEnd);
                transform.BeginAnimation(TranslateTransform.YProperty, anim);
                if (target.Effect is DropShadowEffect) ((DropShadowEffect)target.Effect).Opacity = 0.5;
            };
            target.MouseLeave += (s, e) =>
            {
                var anim = new DoubleAnimation(-3, 0, TimeSpan.FromMilliseconds(120), FillBehavior.HoldEnd);
                transform.BeginAnimation(TranslateTransform.YProperty, anim);
                if (target.Effect is DropShadowEffect) ((DropShadowEffect)target.Effect).Opacity = 0.3;
            };
        }

        private void ShowToast(string msg, Color c)
        {
            try
            {
                if (_toastBorder == null)
                {
                    _toastBorder = new Border {
                        Background = new SolidColorBrush(Color.FromArgb(230, 15, 17, 21)),
                        CornerRadius = new CornerRadius(12),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(60, 46, 168, 255)),
                        BorderThickness = new Thickness(1),
                        Padding = new Thickness(20, 12, 20, 12),
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(0, 0, 24, 60),
                        Effect = new DropShadowEffect { BlurRadius = 24, Opacity = 0.25, ShadowDepth = 0, Color = Colors.Black }
                    };
                    var toastGrid = new Grid();
                    toastGrid.ColumnDefinitions.Add(new ColumnDefinition());
                    toastGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var toastTxt = new TextBlock { FontSize = 12, Foreground = new SolidColorBrush(Colors.White), VerticalAlignment = VerticalAlignment.Center };
                    toastGrid.Children.Add(toastTxt);
                    var closeBtn = new Border { Width = 20, Height = 20, CornerRadius = new CornerRadius(10), Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), Margin = new Thickness(12, 0, 0, 0), Cursor = Cursors.Hand, HorizontalAlignment = HorizontalAlignment.Right };
                    var closeTxt = new TextBlock { Text = "\uE711", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 10, Foreground = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255)), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    closeBtn.Child = closeTxt;
                    Grid.SetColumn(closeBtn, 1);
                    closeBtn.MouseDown += (s, e) => { if (_toastBorder != null && _toastBorder.Parent is Panel) ((Panel)_toastBorder.Parent).Children.Remove(_toastBorder); };
                    toastGrid.Children.Add(closeBtn);
                    _toastBorder.Child = toastGrid;
                    _toastBorder.Tag = toastTxt;
                }
                if (_toastBorder.Parent == null)
                {
                    var disp = Dispatcher;
                    disp.BeginInvoke(new Action(() =>
                    {
                        // Re-check on the UI thread: rapid ShowToast calls can both see
                        // Parent == null before the add runs, which would schedule a
                        // duplicate add and throw "already a child of another Visual".
                        if (_toastBorder == null || _toastBorder.Parent != null) return;
                        if (_scrollViewer != null && _scrollViewer.Parent is Panel)
                            ((Panel)_scrollViewer.Parent).Children.Add(_toastBorder);
                        else if (Content is Panel)
                            ((Panel)Content).Children.Add(_toastBorder);
                    }));
                }
                var txtBlock = _toastBorder.Tag as TextBlock;
                if (txtBlock != null) txtBlock.Text = msg;
                _toastBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(60, c.R, c.G, c.B));
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
                timer.Tick += (s2, e2) =>
                {
                    timer.Stop();
                    try
                    {
                        if (_toastBorder != null && _toastBorder.Parent is Panel)
                            ((Panel)_toastBorder.Parent).Children.Remove(_toastBorder);
                    } catch (Exception ex6) { CWLog.Error("MainWindow.Overlay", ex6); }
                };
                timer.Start();
            } catch (Exception ex7) { CWLog.Error("MainWindow.Overlay", ex7); }
        }
    }
}