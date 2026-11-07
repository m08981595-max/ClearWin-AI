using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ClearWin
{
    public class Program
    {
        private static readonly string _crashDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClearWin", "crashes");
        private static readonly string _markerFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClearWin", "clean_exit.mrk");
        private static System.Threading.Mutex _singleInstanceMutex;
        private static bool _previousRunWasClean = true;
        public static bool IsSafeStartup { get; private set; }

        [STAThread]
        public static void Main(string[] args)
        {
            // Sanitize process environment to prevent any test redirection from affecting production
            try
            {
                Environment.SetEnvironmentVariable("CLEARWIN_TEST_REG_PREFIX", null);
                Environment.SetEnvironmentVariable("CLEARWIN_TEST_BACKUP_DIR", null);
                Environment.SetEnvironmentVariable("CLEARWIN_TEST_MODE", null);
                Environment.SetEnvironmentVariable("CLEARWIN_ALLOW_DEFERRED_VM_TWEAKS", null);
            }
            catch { }

            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (string.Equals(args[i], "--deep-train", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("ClearWin Deep AI Training Mode initiated...");
                        var agent = new SmartAgent();
                        agent.ForceDeepTraining(25, msg => Console.WriteLine("[TRAIN] " + msg));
                        Console.WriteLine("Deep training finished successfully.");
                        return;
                    }
                }
            }

            bool forcedSafe = false;
            try
            {
                if (args != null)
                {
                    for (int i = 0; i < args.Length; i++)
                    {
                        if (string.Equals(args[i], "--safe-mode", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(args[i], "/safe-mode", StringComparison.OrdinalIgnoreCase))
                        { forcedSafe = true; break; }
                    }
                }
                string env = Environment.GetEnvironmentVariable("CLEARWIN_SAFE_MODE");
                if (string.Equals(env, "1", StringComparison.OrdinalIgnoreCase) || string.Equals(env, "true", StringComparison.OrdinalIgnoreCase)) forcedSafe = true;
            }
            catch (Exception ex) { CWLog.Error("Program", ex); }
            WaitForDesktop();

            // Single-instance guard
            try
            {
                bool createdNew;
                _singleInstanceMutex = new System.Threading.Mutex(true, "Global\\ClearWin_SingleInstance", out createdNew);
                if (!createdNew)
                {
                    bool activated = ActivateExistingWindow();
                    if (activated)
                    {
                        try { _singleInstanceMutex = null; } catch (Exception ex3) { CWLog.Error("Program", ex3); }
                        return;
                    }
                    System.Threading.Thread.Sleep(400);
                    try
                    {
                        _singleInstanceMutex = new System.Threading.Mutex(true, "Global\\ClearWin_SingleInstance", out createdNew);
                    }
                    catch { }
                }
            }
            catch
            {
                try
                {
                    bool createdNew;
                    _singleInstanceMutex = new System.Threading.Mutex(true, "ClearWin_SingleInstance", out createdNew);
                    if (!createdNew)
                    {
                        bool activated = ActivateExistingWindow();
                        if (activated)
                        {
                            try { _singleInstanceMutex = null; } catch (Exception ex4) { CWLog.Error("Program", ex4); }
                            return;
                        }
                    }
                }
                catch (Exception ex5) { CWLog.Error("Program", ex5); }
            }
            // Crash Reporting: global exception handlers
            try { Directory.CreateDirectory(_crashDir); } catch (Exception ex6) { CWLog.Error("Program", ex6); }
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            // Capture the previous run state before removing the marker.
            // If the last run was unclean and a crash report exists, the next launch
            // automatically enters a conservative safe-startup mode.
            try
            {
                _previousRunWasClean = File.Exists(_markerFile);
                IsSafeStartup = forcedSafe;
                if (File.Exists(_markerFile)) File.Delete(_markerFile);
            }
            catch { _previousRunWasClean = false; IsSafeStartup = forcedSafe; }

            var app = Application.Current ?? new Application();
            // MainWindow manages its own explicit shutdown lifecycle via Closing/Closed
            // (Application.Current.Shutdown + Environment.Exit). Using OnExplicitShutdown
            // ensures auxiliary native handles (WebView2, IME, Tray) do not trigger early exit.
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            Application.Current.DispatcherUnhandledException += OnDispatcherException;
            TaskScheduler.UnobservedTaskException += OnTaskException;
            app.Exit += (s, e) =>
            {
                try { File.WriteAllText(_markerFile, DateTime.Now.ToString("O")); } catch (Exception ex7) { CWLog.Error("Program", ex7); }
            };
            try
            {
                var mainWin = new MainWindow();
                app.MainWindow = mainWin;

                bool startMinimized = false;
                if (args != null)
                {
                    for (int i = 0; i < args.Length; i++)
                    {
                        if (string.Equals(args[i], "--minimized", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(args[i], "-minimized", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(args[i], "/minimized", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(args[i], "--tray", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(args[i], "-tray", StringComparison.OrdinalIgnoreCase))
                        {
                            startMinimized = true;
                            break;
                        }
                    }
                }

                if (startMinimized)
                {
                    mainWin.WindowState = WindowState.Minimized;
                    mainWin.ShowInTaskbar = false;
                    mainWin.Hide();
                    CWLog.Info("Program", "ClearWin started minimized in System Tray via command line flag.");
                    mainWin.ApplyPostBootStabilization();
                }
                else
                {
                    mainWin.Show();
                    var helper = new System.Windows.Interop.WindowInteropHelper(mainWin);
                    if (helper.Handle != IntPtr.Zero)
                    {
                        ShowWindow(helper.Handle, SW_SHOWNORMAL);
                        SetForegroundWindow(helper.Handle);
                    }
                    mainWin.Activate();
                    mainWin.ApplyPostBootStabilization();
                }
                app.Run();
            }
            catch (Exception ex)
            {
                // A constructor/native initialization failure should be logged and
                // reported instead of escaping as an opaque process crash.
                SaveCrash("Program.Main", ex);
                try { MessageBox.Show("ClearWin could not start safely. A crash report was saved in LocalAppData\\ClearWin\\crashes.", "ClearWin - Startup Error", MessageBoxButton.OK, MessageBoxImage.Error); } catch (Exception ex8) { CWLog.Error("Program", ex8); }
            }
            // If we get here, clean exit
            try { File.WriteAllText(_markerFile, DateTime.Now.ToString("O")); } catch (Exception ex9) { CWLog.Error("Program", ex9); }
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            SaveCrash("AppDomain", ex);
        }

        private static void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            SaveCrash("Dispatcher", e.Exception);
            e.Handled = true;
        }

        private static void OnTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            SaveCrash("Task", e.Exception);
            e.SetObserved();
        }

        private static void SaveCrash(string source, Exception ex)
        {
            try
            {
                string path = Path.Combine(_crashDir, string.Format("crash_{0:yyyyMMdd_HHmmss}.log", DateTime.Now));
                using (var w = new StreamWriter(path, false))
                {
                    w.WriteLine("=== ClearWin Crash Report ===");
                    w.WriteLine("Source: " + source);
                    w.WriteLine("Time: " + DateTime.Now.ToString("O"));
                    w.WriteLine("OS: " + Environment.OSVersion);
                    w.WriteLine("CLR: " + Environment.Version);
                    using (var self = Process.GetCurrentProcess())
                    {
                        w.WriteLine("Process: " + self.ProcessName);
                        w.WriteLine("Memory: " + (self.WorkingSet64 / 1024 / 1024) + " MB");
                        w.WriteLine("CPU: " + self.TotalProcessorTime.TotalSeconds + "s");
                        w.WriteLine("Threads: " + self.Threads.Count);
                        w.WriteLine("Handles: " + self.HandleCount);
                    }
                    w.WriteLine();
                    while (ex != null)
                    {
                        w.WriteLine("Type: " + ex.GetType().FullName);
                        w.WriteLine("Message: " + ex.Message);
                        w.WriteLine("Stack: " + ex.StackTrace);
                        w.WriteLine();
                        ex = ex.InnerException;
                    }
                }
            } catch (Exception ex2) { CWLog.Error("Program", ex2); }
        }

        private static void WaitForDesktop()
        {
            try
            {
                var started = Process.GetCurrentProcess().StartTime;
                var uptime = DateTime.Now - started;
                if (uptime.TotalSeconds > 60) return;

                for (int i = 0; i < 60; i++)
                {
                    var explorer = Process.GetProcessesByName("explorer");
                    if (explorer.Length > 0)
                    {
                        if (i > 0)
                        {
                            // Explorer was absent on the first checks and just re-appeared
                            // (crash / early boot). Give the shell a moment to settle.
                            System.Threading.Thread.Sleep(5000);
                        }
                        // Explorer was already present from the start: the desktop is
                        // healthy, so do not pay a 5s startup penalty on normal launches.
                        return;
                    }
                    System.Threading.Thread.Sleep(2000);
                }
            }
            catch (Exception ex10) { CWLog.Error("Program", ex10); }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);
        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern uint RegisterWindowMessage(string lpString);
        private static readonly uint WM_CLEARWIN_RESTORE = RegisterWindowMessage("CLEARWIN_RESTORE_WINDOW");
        private const int SW_RESTORE = 9;
        private const int SW_SHOWNORMAL = 1;

        private static bool ActivateExistingWindow()
        {
            try
            {
                var current = Process.GetCurrentProcess();
                var procs = Process.GetProcessesByName(current.ProcessName);

                // Broadcast message to wake up and restore any existing instance running in system tray
                try { PostMessage(HWND_BROADCAST, WM_CLEARWIN_RESTORE, IntPtr.Zero, IntPtr.Zero); } catch { }

                IntPtr hWnd = IntPtr.Zero;

                // 1. Try finding by title
                hWnd = FindWindow(null, "ClearWin");
                if (hWnd == IntPtr.Zero) hWnd = FindWindow(null, "ClearWin 2.7.7");
                if (hWnd == IntPtr.Zero) hWnd = FindWindow(null, "ClearWin 2.7.7");
                if (hWnd == IntPtr.Zero) hWnd = FindWindow(null, "ClearWin 2.7.4");

                // 2. Try finding by MainWindowHandle of other instances
                if (hWnd == IntPtr.Zero)
                {
                    foreach (var p in procs)
                    {
                        if (p.Id != current.Id && p.MainWindowHandle != IntPtr.Zero)
                        {
                            hWnd = p.MainWindowHandle;
                            break;
                        }
                    }
                }

                // 3. If a window handle exists, bring it to front
                if (hWnd != IntPtr.Zero)
                {
                    uint targetPid;
                    uint winThread = GetWindowThreadProcessId(hWnd, out targetPid);
                    uint curThread = GetCurrentThreadId();
                    try { AttachThreadInput(curThread, winThread, true); } catch { }
                    ShowWindow(hWnd, SW_RESTORE);
                    SetForegroundWindow(hWnd);
                    try { AttachThreadInput(curThread, winThread, false); } catch { }
                    return true;
                }

                // 4. If an existing responsive process is running, the broadcast already signaled it
                foreach (var p in procs)
                {
                    if (p.Id != current.Id && p.Responding)
                    {
                        CWLog.Info("Program.SingleInstance", string.Format("Signaled existing background tray instance PID {0}", p.Id));
                        return true;
                    }
                }

                // 5. Clean up dead/unresponsive zombie instances only
                foreach (var p in procs)
                {
                    if (p.Id != current.Id && !p.Responding)
                    {
                        try
                        {
                            CWLog.Warn("Program.SingleInstance", string.Format("Terminating unresponsive ghost instance PID {0}", p.Id));
                            p.Kill();
                            p.WaitForExit(1500);
                        }
                        catch { }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                CWLog.Error("Program.SingleInstance", ex);
                return false;
            }
        }

        public static bool WasCleanExit()
        {
            return _previousRunWasClean;
        }
    }
}
