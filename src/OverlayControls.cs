using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ClearWin
{
    public class OverlayControls : Window
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

        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        const int GWL_EXSTYLE = -20;
        const int WS_EX_TOOLWINDOW = 0x80;
        const int WS_EX_NOACTIVATE = 0x08000000;
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_SHOWWINDOW = 0x0040;

        private bool _visible = true;

        public OverlayControls()
        {
            Topmost = true;
            ShowInTaskbar = false;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = null;
            ResizeMode = ResizeMode.NoResize;
            Width = 300; Height = 48;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = SystemParameters.PrimaryScreenWidth - Width - 12;
            Top = SystemParameters.PrimaryScreenHeight - Height - 12;

            var panel = new Border
            {
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromArgb(200, 8, 12, 28)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 59, 130, 246)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 4, 6, 4)
            };

            var grid = new Grid();
            for (int i = 0; i < 5; i++) grid.ColumnDefinitions.Add(new ColumnDefinition());

            string[] labels = new[] { "RAM", "Boost", "DNS", "30", "60" };
            Color[] colors = new[] { Color.FromRgb(34, 197, 94), Color.FromRgb(59, 130, 246), Color.FromRgb(139, 92, 246), Color.FromRgb(251, 146, 60), Color.FromRgb(236, 72, 153) };
            string[] tips = new[] { "تنظيف RAM", "Boost كامل", "DNS Flush", "قفل 30 FPS", "قفل 60 FPS" };

            for (int i = 0; i < 5; i++)
            {
                int idx = i;
                var btn = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(30, colors[i].R, colors[i].G, colors[i].B)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(4, 3, 4, 3),
                    Margin = new Thickness(2),
                    Cursor = Cursors.Hand,
                    ToolTip = tips[i]
                };
                btn.Child = new TextBlock
                {
                    Text = labels[i],
                    FontSize = 10,
                    Foreground = new SolidColorBrush(colors[i]),
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                btn.MouseDown += (s, e) =>
                {
                    if (idx == 0)
                    {
                        if (IsLikelyGameForeground()) return;
                        try { var procs = Process.GetProcesses(); try { foreach (var p in procs) { try { EmptyWorkingSet(p.Handle); } catch (Exception ex) { CWLog.Error("OverlayControls", ex); } } } finally { foreach (var p in procs) { try { p.Dispose(); } catch (Exception ex2) { CWLog.Error("OverlayControls", ex2); } } } } catch (Exception ex3) { CWLog.Error("OverlayControls", ex3); }
                        try { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); } catch (Exception ex4) { CWLog.Error("OverlayControls", ex4); }
                        PurgeStandbyList();
                    }
                    else if (idx == 1)
                    {
                        if (IsLikelyGameForeground()) return;
                        try { var procs2 = Process.GetProcesses(); try { foreach (var p in procs2) { try { EmptyWorkingSet(p.Handle); } catch (Exception ex5) { CWLog.Error("OverlayControls", ex5); } } } finally { foreach (var p in procs2) { try { p.Dispose(); } catch (Exception ex6) { CWLog.Error("OverlayControls", ex6); } } } } catch (Exception ex7) { CWLog.Error("OverlayControls", ex7); }
                        try { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); } catch (Exception ex8) { CWLog.Error("OverlayControls", ex8); }
                        PurgeStandbyList();
                        try { IntPtr fg2 = GetForegroundWindow(); uint pid2; GetWindowThreadProcessId(fg2, out pid2); if (pid2 > 0) { using (var pp = Process.GetProcessById((int)pid2)) { if (!pp.HasExited) pp.PriorityClass = ProcessPriorityClass.High; } } } catch (Exception ex9) { CWLog.Error("OverlayControls", ex9); }
                    }
                    else if (idx == 2)
                    {
                        System.Threading.Tasks.Task.Run(() => { string dnsMsgOvc; MainWindow.TryFlushDnsVerified(out dnsMsgOvc); });
                    }
                    else if (idx == 3)
                    {
                        try { using (var rk = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(@"SOFTWARE\NVIDIA Corporation\Global\NVTweak", true)) { rk.SetValue("MaxFrameRate", 30, Microsoft.Win32.RegistryValueKind.DWord); } } catch (Exception ex11) { CWLog.Error("OverlayControls", ex11); }
                        try { using (var rk2 = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\DirectX\GraphicsSettings", true)) { rk2.SetValue("D3D11FrameRateLimit", 30, Microsoft.Win32.RegistryValueKind.DWord); } } catch (Exception ex12) { CWLog.Error("OverlayControls", ex12); }
                    }
                    else if (idx == 4)
                    {
                        try { using (var rk = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(@"SOFTWARE\NVIDIA Corporation\Global\NVTweak", true)) { rk.SetValue("MaxFrameRate", 60, Microsoft.Win32.RegistryValueKind.DWord); } } catch (Exception ex13) { CWLog.Error("OverlayControls", ex13); }
                        try { using (var rk2 = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\DirectX\GraphicsSettings", true)) { rk2.SetValue("D3D11FrameRateLimit", 60, Microsoft.Win32.RegistryValueKind.DWord); } } catch (Exception ex14) { CWLog.Error("OverlayControls", ex14); }
                    }
                };
                Grid.SetColumn(btn, i); grid.Children.Add(btn);
            }

            panel.Child = grid;
            Content = panel;

            SourceInitialized += (s, e) =>
            {
                try
                {
                    var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                    int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                    exStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                    SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
                    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                } catch (Exception ex15) { CWLog.Error("OverlayControls", ex15); }
            };
        }

        [DllImport("psapi.dll")]
        static extern bool EmptyWorkingSet(IntPtr hProcess);
        [DllImport("ntdll.dll")]
        static extern uint NtSetSystemInformation(int SystemInformationClass, IntPtr SystemInformation, int SystemInformationLength);
        const int SystemMemoryListInformation = 80;

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
                    return n.Contains("gameloop") || n.Contains("ldplayer") || n.Contains("nox") ||
                           n.Contains("memu") || n.Contains("bluestacks") || n.Contains("freefire") ||
                           n.Contains("pubg") || (n.Contains("steam") && !n.Contains("steamwebhelper"));
                }
            }
            catch { return false; }
        }

        private bool PurgeStandbyList()
        {
            if (IsLikelyGameForeground()) return false;
            IntPtr p = IntPtr.Zero;
            try
            {
                int cmd = 4;
                p = Marshal.AllocHGlobal(4);
                Marshal.WriteInt32(p, cmd);
                uint r = NtSetSystemInformation(SystemMemoryListInformation, p, 4);
                return r == 0;
            }
            catch { return false; }
            finally { if (p != IntPtr.Zero) Marshal.FreeHGlobal(p); }
        }

        public new void Hide() { _visible = false; base.Hide(); }
        public new void Show() { _visible = true; base.Show(); }
        public bool IsControlsVisible { get { return _visible; } }
    }
}
