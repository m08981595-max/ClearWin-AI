using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace HPBrightnessController
{
    static class Program
    {
        private static Mutex _mutex;
        private static NotifyIcon _trayIcon;
        private static ManagementEventWatcher _hpWatcher;
        private static HotkeyWindow _hotkeyWnd;
        private static OsdForm _osd;
        private static SynchronizationContext _syncContext;

        [STAThread]
        static void Main(string[] args)
        {
            // Global Exception handlers
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => LogEvent("CRITICAL UNHANDLED: " + e.ExceptionObject);
            Application.ThreadException += (s, e) => LogEvent("CRITICAL THREAD: " + e.Exception);

            Control.CheckForIllegalCrossThreadCalls = false;

            bool createdNew;
            _mutex = new Mutex(true, "HP_EliteBook_8570w_Brightness_Controller_SingleInstance_V2", out createdNew);
            if (!createdNew)
            {
                LogEvent("Another instance already running. Exiting.");
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            _syncContext = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

            LogEvent("Starting HP Brightness Controller V2...");

            // 1. Ensure startup with Windows
            try
            {
                string exePath = Application.ExecutablePath;
                using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (rk != null)
                    {
                        rk.SetValue("HPBrightnessController", "\"" + exePath + "\"");
                    }
                }
            }
            catch (Exception ex) { LogEvent("Startup reg error: " + ex.Message); }

            // 2. Initialize OSD Overlay
            _osd = new OsdForm();

            // 3. Setup System Tray Icon
            try
            {
                _trayIcon = new NotifyIcon
                {
                    Icon = CreateBrightnessIcon(GetBrightness()),
                    Text = "HP EliteBook - التحكم بالإضاءة (Fn / Ctrl + F9/F10)",
                    Visible = true
                };

                ContextMenu cm = new ContextMenu();
                cm.MenuItems.Add("زيادة الإضاءة (F10 / Ctrl+F10)", (s, e) => AdjustBrightness(+10));
                cm.MenuItems.Add("خفض الإضاءة (F9 / Ctrl+F9)", (s, e) => AdjustBrightness(-10));
                cm.MenuItems.Add("-");
                cm.MenuItems.Add("إضاءة 100%", (s, e) => SetBrightness(100));
                cm.MenuItems.Add("إضاءة 70%", (s, e) => SetBrightness(70));
                cm.MenuItems.Add("إضاءة 50%", (s, e) => SetBrightness(50));
                cm.MenuItems.Add("إضاءة 30%", (s, e) => SetBrightness(30));
                cm.MenuItems.Add("-");
                cm.MenuItems.Add("خروج", (s, e) => {
                    _trayIcon.Visible = false;
                    Application.Exit();
                });
                _trayIcon.ContextMenu = cm;
                _trayIcon.MouseClick += (s, e) =>
                {
                    if (e.Button == MouseButtons.Left)
                    {
                        int cur = GetBrightness();
                        int next = cur >= 90 ? 40 : (cur + 20);
                        SetBrightness(next);
                    }
                };
            }
            catch (Exception ex) { LogEvent("Tray icon error: " + ex.Message); }

            // 4. Start Hotkey Window (RegisterHotKey for Ctrl+F9, Ctrl+F10, Alt+F9, Alt+F10)
            try
            {
                _hotkeyWnd = new HotkeyWindow();
                LogEvent("Hotkey window registered.");
            }
            catch (Exception ex) { LogEvent("Hotkey window error: " + ex.Message); }

            // 5. Start HP WMI Event Watcher for native Fn+F9 and Fn+F10
            StartHpWmiWatcher();

            // Show initial notification
            SafeUiUpdate(() => {
                _osd.ShowOsd(GetBrightness(), "أداة الإضاءة نشطة (Fn / Ctrl + F9/F10)");
            });

            LogEvent("Entering Application.Run() message loop.");
            Application.Run();

            // Cleanup
            if (_hotkeyWnd != null) _hotkeyWnd.Dispose();
            if (_hpWatcher != null)
            {
                try { _hpWatcher.Stop(); _hpWatcher.Dispose(); } catch { }
            }
        }

        private static void StartHpWmiWatcher()
        {
            try
            {
                WqlEventQuery query = new WqlEventQuery("SELECT * FROM hpqBEvnt");
                _hpWatcher = new ManagementEventWatcher(new ManagementScope("root\\wmi"), query);
                _hpWatcher.EventArrived += (sender, e) =>
                {
                    try
                    {
                        uint eventId = 0;
                        uint eventData = 0;
                        if (e.NewEvent.Properties["EventID"] != null && e.NewEvent.Properties["EventID"].Value != null)
                            eventId = Convert.ToUInt32(e.NewEvent.Properties["EventID"].Value);
                        if (e.NewEvent.Properties["EventData"] != null && e.NewEvent.Properties["EventData"].Value != null)
                            eventData = Convert.ToUInt32(e.NewEvent.Properties["EventData"].Value);

                        LogEvent(string.Format("HP WMI Event RECEIVED: EventID={0}, EventData={1}", eventId, eventData));

                        // Brightness events on HP EliteBook
                        if (eventId == 4 || eventId == 0x04)
                        {
                            if (eventData == 2 || eventData == 4 || (eventData & 0xFF) == 2 || eventData == 0x24)
                            {
                                AdjustBrightness(-10);
                            }
                            else if (eventData == 3 || eventData == 5 || (eventData & 0xFF) == 3 || eventData == 0x25)
                            {
                                AdjustBrightness(+10);
                            }
                            else
                            {
                                if (eventData % 2 == 0) AdjustBrightness(-10);
                                else AdjustBrightness(+10);
                            }
                        }
                        else if (eventData == 2 || eventData == 0x02)
                        {
                            AdjustBrightness(-10);
                        }
                        else if (eventData == 3 || eventData == 0x03)
                        {
                            AdjustBrightness(+10);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogEvent("Error handling WMI EventArrived: " + ex);
                    }
                };
                _hpWatcher.Start();
                LogEvent("HP WMI Watcher started successfully on root\\wmi (hpqBEvnt).");
            }
            catch (Exception ex)
            {
                LogEvent("Failed to start HP WMI Watcher: " + ex);
            }
        }

        public static void AdjustBrightness(int delta)
        {
            int current = GetBrightness();
            int target = Math.Max(10, Math.Min(100, current + delta));
            SetBrightness(target);
        }

        public static void SetBrightness(int percent)
        {
            percent = Math.Max(5, Math.Min(100, percent));
            try
            {
                using (var mclass = new ManagementClass("root\\wmi", "WmiMonitorBrightnessMethods", null))
                {
                    foreach (ManagementObject instance in mclass.GetInstances())
                    {
                        instance.InvokeMethod("WmiSetBrightness", new object[] { 1, (byte)percent });
                    }
                }
                LogEvent("Brightness applied successfully: " + percent + "%");
            }
            catch (Exception ex)
            {
                LogEvent("SetBrightness WMI error: " + ex.Message);
            }

            SafeUiUpdate(() =>
            {
                try
                {
                    if (_trayIcon != null)
                    {
                        _trayIcon.Icon = CreateBrightnessIcon(percent);
                        _trayIcon.Text = string.Format("HP EliteBook - الإضاءة: {0}%", percent);
                    }
                    if (_osd != null)
                    {
                        _osd.ShowOsd(percent);
                    }
                }
                catch (Exception ex) { LogEvent("UI update error: " + ex.Message); }
            });
        }

        private static void SafeUiUpdate(Action action)
        {
            if (_syncContext != null)
            {
                _syncContext.Post(state => {
                    try { action(); } catch (Exception ex) { LogEvent("SafeUiUpdate failed: " + ex); }
                }, null);
            }
            else
            {
                try { action(); } catch { }
            }
        }

        public static int GetBrightness()
        {
            try
            {
                using (var mclass = new ManagementClass("root\\wmi", "WmiMonitorBrightness", null))
                {
                    foreach (ManagementObject instance in mclass.GetInstances())
                    {
                        return Convert.ToInt32(instance["CurrentBrightness"]);
                    }
                }
            }
            catch { }
            return 100;
        }

        private static Icon CreateBrightnessIcon(int brightness)
        {
            try
            {
                using (Bitmap bmp = new Bitmap(16, 16))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);

                    using (Brush b = new SolidBrush(Color.FromArgb(255, 215, 0)))
                    {
                        g.FillEllipse(b, 3, 3, 10, 10);
                    }
                    using (Pen p = new Pen(Color.FromArgb(255, 165, 0), 1.5f))
                    {
                        g.DrawEllipse(p, 3, 3, 10, 10);
                    }
                    return Icon.FromHandle(bmp.GetHicon());
                }
            }
            catch
            {
                return SystemIcons.Application;
            }
        }

        public static void LogEvent(string msg)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HPBrightnessController");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "service.log"), string.Format("[{0:yyyy-MM-dd HH:mm:ss}] {1}\r\n", DateTime.Now, msg));
            }
            catch { }
        }
    }

    // Reliable Win32 RegisterHotKey window (Handles Ctrl+F9, Ctrl+F10, Alt+F9, Alt+F10)
    public class HotkeyWindow : NativeWindow, IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_NOREPEAT = 0x4000;
        private const uint VK_F9 = 0x78;
        private const uint VK_F10 = 0x79;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public HotkeyWindow()
        {
            CreateParams cp = new CreateParams { Caption = "HP_Brightness_Hotkey_Sink" };
            this.CreateHandle(cp);

            // ID 1: Ctrl + F9 (Brightness Down)
            RegisterHotKey(this.Handle, 1, MOD_CONTROL | MOD_NOREPEAT, VK_F9);
            // ID 2: Ctrl + F10 (Brightness Up)
            RegisterHotKey(this.Handle, 2, MOD_CONTROL | MOD_NOREPEAT, VK_F10);
            // ID 3: Alt + F9 (Brightness Down)
            RegisterHotKey(this.Handle, 3, MOD_ALT | MOD_NOREPEAT, VK_F9);
            // ID 4: Alt + F10 (Brightness Up)
            RegisterHotKey(this.Handle, 4, MOD_ALT | MOD_NOREPEAT, VK_F10);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY)
            {
                int id = m.WParam.ToInt32();
                Program.LogEvent("Hotkey pressed! ID=" + id);
                if (id == 1 || id == 3) // F9
                {
                    Program.AdjustBrightness(-10);
                }
                else if (id == 2 || id == 4) // F10
                {
                    Program.AdjustBrightness(+10);
                }
            }
            base.WndProc(ref m);
        }

        public void Dispose()
        {
            for (int i = 1; i <= 4; i++)
            {
                UnregisterHotKey(this.Handle, i);
            }
            this.DestroyHandle();
        }
    }

    // Elegant Floating OSD Overlay
    public class OsdForm : Form
    {
        private System.Windows.Forms.Timer _timer;
        private int _currentBrightness = 100;
        private string _customText = null;

        public OsdForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;
            this.Size = new Size(320, 95);
            this.BackColor = Color.FromArgb(18, 24, 38);
            this.DoubleBuffered = true;

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 1500;
            _timer.Tick += (s, e) =>
            {
                _timer.Stop();
                this.Hide();
            };
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                return cp;
            }
        }

        public void ShowOsd(int brightness, string customText = null)
        {
            _currentBrightness = brightness;
            _customText = customText;

            var screen = Screen.PrimaryScreen.WorkingArea;
            this.Location = new Point(screen.Left + (screen.Width - this.Width) / 2, screen.Bottom - this.Height - 60);

            this.Show();
            this.Invalidate();

            _timer.Stop();
            _timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (Pen borderPen = new Pen(Color.FromArgb(80, 56, 189, 248), 1.5f))
            {
                g.DrawRectangle(borderPen, 1, 1, this.Width - 2, this.Height - 2);
            }

            using (Font titleFont = new Font("Segoe UI", 12f, FontStyle.Bold))
            using (Brush textBrush = new SolidBrush(Color.White))
            {
                string text = _customText ?? string.Format("☀️ إضاءة الشاشة: {0}%", _currentBrightness);
                g.DrawString(text, titleFont, textBrush, new PointF(18, 14));
            }

            int barX = 18;
            int barY = 48;
            int barW = this.Width - 36;
            int barH = 14;

            using (Brush bgBar = new SolidBrush(Color.FromArgb(40, 50, 70)))
            {
                g.FillRectangle(bgBar, barX, barY, barW, barH);
            }

            int fillW = (int)((_currentBrightness / 100.0) * barW);
            if (fillW > 0)
            {
                using (LinearGradientBrush fillBar = new LinearGradientBrush(
                    new Rectangle(barX, barY, Math.Max(10, fillW), barH),
                    Color.FromArgb(56, 189, 248),
                    Color.FromArgb(250, 204, 21),
                    LinearGradientMode.Horizontal))
                {
                    g.FillRectangle(fillBar, barX, barY, fillW, barH);
                }
            }
        }
    }
}
