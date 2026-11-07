using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Collections.Generic;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace ClearWin
{
    // Lightweight WPF host for the WebView2 dashboard. The existing C# engine
    // remains the source of truth; the browser layer is presentation + input only.
    public sealed class WebUiHost : Grid, IDisposable
    {
        private readonly MainWindow _owner;
        private readonly WebView2 _webView;
        public static string LatestMetricsJson { get; internal set; } = "{\"type\":\"metrics\",\"cpu\":0,\"ram\":0,\"gpu\":0,\"disk\":0,\"temp\":0,\"health\":100,\"mode\":\"AutoPilot\",\"online\":true}";
        private bool _ready;
        private bool _disposed;
        private bool _isInitializing;
        private string _pendingStatus;
        private string _pendingMetrics;
        private DateTime _lastMetricsPost = DateTime.MinValue;
        private long _metricsSequence = 0;
        private long _lastMetricsAck = 0;
        private double _storageUsedGb;
        private double _storageTotalGb;
        private DateTime _lastStorageRead = DateTime.MinValue;
        private string _pendingAgentLog;
        private DateTime _lastAgentLogPost = DateTime.MinValue;
        private DispatcherTimer _agentLogFlushTimer;
        private Action<AiDecisionRecord> _aiDecisionHandler;

        // =====================================================================
        // v20 RC — WebView2 Security: explicit allowlist for all UI actions.
        // Unknown or malicious commands are dropped before they ever reach
        // MainWindow.HandleWebUiAction, closing the bypass that existed in v19.
        // Supports both exact matches (HashSet) and prefix wildcards (*).
        // =====================================================================
        private static readonly HashSet<string> ALLOWED_WEB_ACTIONS = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Core optimizations
            "optimize-all", "boost", "memory", "reset", "services", "scan",
            "framepacing", "framepacing:restore", "network", "network:restore",
            // Game mode
            "game:toggle", "game:fpsfloor", "game:fullopt", "game:timer",
            "game:unpark", "game:gpu-msi",
            // AI / SmartAgent
            "ai:toggle", "ai:pause", "ai:resume", "ai:kill-toggle",
            "ai:autopilot-toggle", "ai:forecast", "ai:selfcheck",
            "ai:export-report", "ai:benchmark", "ai:dna", "ai:stats",
            "ai-memory:refresh", "ai-memory:clear", "ai-memory:export",
            // Theme
            "theme:dark", "theme:light", "theme:toggle",
            // Window controls
            "window:drag", "window:minimize", "window:maximize", "window:close",
            // Sandbox & news
            "sandbox:toggle", "news:refresh",
            // Security & health
            "security:defender", "health:restore-point",
            // Process & startup
            "process:taskmgr", "startup:settings",
            // Admin Mode & Green Autonomy
            "admin:request-elevation", "admin:terminate-session", "admin:status",
            "autonomy:toggle-green", "autonomy:get-state",
            "catalog:get-all", "audit:get-logs"
        };

        // Prefix wildcards: any action starting with these prefixes is allowed.
        private static readonly string[] ALLOWED_ACTION_PREFIXES = {
            "navigate:",    // JS page navigation (navigate:dashboard, navigate:ai, …)
            "page:refresh-", // JS page refresh requests
            "toggle:",      // Toggle states (toggle:gaming, toggle:performance, …)
            "clean:",       // Cleanup actions
            "tool:",        // Tool actions
            "undo:",        // Undo tweaks
            "tweak:",       // Apply/undo tweak scripts
            "startup.", "cleanup.", "network.", "apps.", "recovery.", "ai.", "settings.", "settings:", "optimization.", "data.", "diagnostic."
        };

        private const int MAX_ACTION_LENGTH = 128;
        private DateTime _lastActionDenialLog = DateTime.MinValue;
        private const int ACTION_DENIAL_LOG_COOLDOWN_SECONDS = 10;

        internal static bool IsActionAllowed(string action)
        {
            if (string.IsNullOrEmpty(action)) return false;
            if (action.Length > MAX_ACTION_LENGTH) return false;
            if (ALLOWED_WEB_ACTIONS.Contains(action)) return true;
            foreach (var prefix in ALLOWED_ACTION_PREFIXES)
            {
                if (action.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private bool IsAlive
        {
            get
            {
                if (_disposed || !_ready || _webView == null) return false;
                try { return _webView.CoreWebView2 != null; }
                catch { return false; }
            }
        }

        public WebUiHost(MainWindow owner)
        {
            _owner = owner;
            Background = System.Windows.Media.Brushes.Transparent;
            _webView = new WebView2();
            _webView.HorizontalAlignment = HorizontalAlignment.Stretch;
            _webView.VerticalAlignment = VerticalAlignment.Stretch;
            Children.Add(_webView);
            _webView.CoreWebView2InitializationCompleted += OnInitialized;
            Loaded += OnLoaded;

            _agentLogFlushTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _agentLogFlushTimer.Tick += (s, e) => {
                string pending = System.Threading.Interlocked.Exchange(ref _pendingAgentLog, null);
                if (!string.IsNullOrEmpty(pending) && IsAlive)
                    PostWebJson(pending);
            };
            _agentLogFlushTimer.Start();

            _aiDecisionHandler = (rec) =>
            {
                try
                {
                    Dispatcher.BeginInvoke(new Action(() => PushAiDecision(rec.ToJson())));
                }
                catch { }
            };
            AiDecisionLogger.OnDecisionRecorded += _aiDecisionHandler;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_ready || _isInitializing || _disposed) return;
            _isInitializing = true;
            try
            {
                await InitializeAsync();
                Loaded -= OnLoaded;
            }
            catch (System.Runtime.InteropServices.COMException comEx) when (comEx.HResult == unchecked((int)0x80070578))
            {
                // Native window handle not ready yet; defer initialization until Dispatcher Loaded priority
                Dispatcher.BeginInvoke(new Action(async () =>
                {
                    try
                    {
                        if (!_ready && !_disposed)
                        {
                            await InitializeAsync();
                            Loaded -= OnLoaded;
                        }
                    }
                    catch (Exception ex2) { CWLog.Error("WebUiHost", ex2); }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                CWLog.Error("WebUiHost", ex);
                _owner.ShowWebUiFallback("WebView2 لم يبدأ على هذا الجهاز. ثبّت Microsoft Edge WebView2 Runtime ثم أعد التشغيل.");
            }
            finally
            {
                _isInitializing = false;
            }
        }

        private async System.Threading.Tasks.Task InitializeAsync()
        {
            try
            {
                var win = Window.GetWindow(this) ?? _owner;
                if (win != null)
                {
                    var helper = new System.Windows.Interop.WindowInteropHelper(win);
                    helper.EnsureHandle();
                }
            }
            catch { }

            int retries = 5;
            while (retries > 0)
            {
                try
                {
                    if (_webView.CoreWebView2 == null)
                    {
                        string userDataDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClearWin", "WebView2");
                        System.IO.Directory.CreateDirectory(userDataDir);
                        var env = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateAsync(null, userDataDir);
                        await _webView.EnsureCoreWebView2Async(env);
                    }
                    break;
                }
                catch (System.Runtime.InteropServices.COMException comEx) when ((comEx.HResult == unchecked((int)0x80070578) || comEx.ErrorCode == unchecked((int)0x80070578)) && retries > 1)
                {
                    retries--;
                    await System.Threading.Tasks.Task.Delay(300);
                }
            }

            if (_webView.CoreWebView2 == null) return;

            try
            {
                _webView.CoreWebView2.WebMessageReceived -= OnWebMessage;
                _webView.CoreWebView2.WebMessageReceived += OnWebMessage;
                _webView.NavigationCompleted -= OnNavigationCompleted;
                _webView.NavigationCompleted += OnNavigationCompleted;
                _webView.CoreWebView2.ProcessFailed -= OnProcessFailed;
                _webView.CoreWebView2.ProcessFailed += OnProcessFailed;
            }
            catch (Exception ex) { CWLog.Error("WebUiHost.InitEvents", ex); }
            _ready = true;

            string devWebRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Resources", "WebUI"));
            string binWebRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "WebUI"));
            string webRoot = (Directory.Exists(devWebRoot) && File.Exists(Path.Combine(devWebRoot, "index.html"))) ? devWebRoot : binWebRoot;
            string index = Path.Combine(webRoot, "index.html");
            if (!File.Exists(index)) throw new FileNotFoundException("ClearWin Web UI is missing.", index);
            try
            {
                _webView.Source = new Uri(index);
            }
            catch (System.Runtime.InteropServices.COMException comEx) when (comEx.HResult == unchecked((int)0x80070578))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { _webView.Source = new Uri(index); }
                    catch (Exception exNav) { CWLog.Error("WebUiHost.NavRetry", exNav); }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            try
            {
                CWLog.Info("WebUiHost.NavCompleted", string.Format("Navigation completed: Success={0}, WebErrorStatus={1}, Id={2}",
                    e.IsSuccess, e.WebErrorStatus, e.NavigationId));

                if (e.IsSuccess && IsAlive)
                {
                    if (!string.IsNullOrEmpty(LocalWebBridge.SessionToken))
                    {
                        _webView.CoreWebView2.ExecuteScriptAsync(string.Format("window.__clearWinToken = '{0}';", LocalWebBridge.SessionToken));
                    }
                    _webView.CoreWebView2.ExecuteScriptAsync("window.clearWinHostReady = true; if(typeof window.onClearWinHostReady==='function') window.onClearWinHostReady();");
                    PostWebJson("{\"channel\":\"clearwin\",\"type\":\"runtime\",\"status\":\"connected\",\"token\":\"" + (LocalWebBridge.SessionToken ?? "") + "\"}");
                    _owner.RefreshWebUiMetricsNow();
                }
                else if (!e.IsSuccess)
                {
                    CWLog.Warn("WebUiHost.NavFailed", "Navigation failed with WebErrorStatus: " + e.WebErrorStatus);
                }
            }
            catch (Exception ex)
            {
                CWLog.Error("WebUiHost.NavCompleted", ex);
            }
        }

        private void OnInitialized(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (!e.IsSuccess || _webView.CoreWebView2 == null)
            {
                _owner.ShowWebUiFallback("فشل تهيئة WebView2. تأكد من وجود WebView2 Runtime.");
                return;
            }
            try
            {
                _webView.CoreWebView2.WebMessageReceived -= OnWebMessage;
                _webView.CoreWebView2.WebMessageReceived += OnWebMessage;
                _webView.NavigationCompleted -= OnNavigationCompleted;
                _webView.NavigationCompleted += OnNavigationCompleted;
                _webView.CoreWebView2.ProcessFailed -= OnProcessFailed;
                _webView.CoreWebView2.ProcessFailed += OnProcessFailed;
            }
            catch (Exception ex)
            {
                CWLog.Error("WebUiHost.Init", ex);
            }
            _ready = true;
        }

        private int _renderCrashCount = 0;
        private DateTime _lastRenderCrash = DateTime.MinValue;

        private void OnProcessFailed(object sender, CoreWebView2ProcessFailedEventArgs e)
        {
            try
            {
                CWLog.Warn("WebUiHost.ProcessFailed", string.Format("WebView2 process event: Kind={0}, Reason={1}, ExitCode={2}, Desc='{3}'",
                    e.ProcessFailedKind, e.Reason, e.ExitCode, e.ProcessDescription));

                // When GPU process exits (e.g. after powercfg scheme changes or DWM restart),
                // WebView2 automatically recovers its DirectX GPU rendering surface. Do not terminate or reload.
                if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.GpuProcessExited)
                {
                    return;
                }

                // If renderer is temporarily unresponsive, DO NOT reload!
                // Reloading during transient I/O bursts causes compounding crashes and aborts in-flight operations.
                if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.RenderProcessUnresponsive)
                {
                    CWLog.Warn("WebUiHost.ProcessFailed", "Render process reported unresponsive; awaiting recovery without reloading.");
                    return;
                }

                if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.RenderProcessExited)
                {
                    DateTime now = DateTime.UtcNow;
                    if ((now - _lastRenderCrash).TotalSeconds > 30)
                    {
                        _renderCrashCount = 0;
                    }
                    _lastRenderCrash = now;
                    _renderCrashCount++;

                    if (_renderCrashCount <= 2)
                    {
                        CWLog.Warn("WebUiHost.ProcessFailed", string.Format("Renderer crashed (occurrence {0}/2). Attempting clean reload.", _renderCrashCount));
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                if (IsAlive && _webView != null && _webView.CoreWebView2 != null)
                                    _webView.Reload();
                            }
                            catch (Exception exRel) { CWLog.Error("WebUiHost.ReloadAfterCrash", exRel); }
                        }));
                    }
                    else
                    {
                        CWLog.Warn("WebUiHost.ProcessFailed", "Renderer crashed repeatedly (>2 in 30s). Displaying fallback UI.");
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            _owner.ShowWebUiFallback("تعذر استرداد محرك الواجهة بعد عدة محاولات. يمكنك إعادة تشغيل ClearWin أو المتابعة في الوضع الآمن.");
                        }));
                    }
                }
            }
            catch (Exception exFail) { CWLog.Error("WebUiHost.OnProcessFailed", exFail); }
        }

        private void OnWebMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                if (!IsTrustedWebUiUri(e.Source))
                {
                    CWLog.Warn("WebUiHost.Message", "Ignored message from untrusted source: " + e.Source);
                    return;
                }
                string msg = null;
                try { msg = e.TryGetWebMessageAsString(); } catch { }
                if (string.IsNullOrEmpty(msg))
                {
                    try { msg = e.WebMessageAsJson; } catch { }
                }
                if (string.IsNullOrEmpty(msg)) return;

                if (string.Equals(msg, "ui:ready", StringComparison.OrdinalIgnoreCase) || (msg.StartsWith("{") && msg.Contains("\"ui:ready\"")))
                {
                    try
                    {
                        _webView.CoreWebView2.ExecuteScriptAsync("window.clearWinHostReady = true; if(typeof window.onClearWinHostReady==='function') window.onClearWinHostReady();");
                    }
                    catch { }
                    PostWebJson("{\"channel\":\"clearwin\",\"type\":\"runtime\",\"status\":\"connected\"}");
                    _owner.RefreshWebUiMetricsNow();
                    if (!string.IsNullOrEmpty(_pendingMetrics)) PostPendingMetrics();
                    if (!string.IsNullOrEmpty(_pendingStatus)) PostStatus(_pendingStatus);
                    if (!string.IsNullOrEmpty(_pendingAgentLog))
                    {
                        _lastAgentLogPost = DateTime.Now;
                        PostWebJson(_pendingAgentLog);
                    }
                    return;
                }
                if (msg.StartsWith("ui:metrics-ack:", StringComparison.OrdinalIgnoreCase))
                {
                    long ack;
                    if (long.TryParse(msg.Substring("ui:metrics-ack:".Length), out ack)) _lastMetricsAck = ack;
                    return;
                }
                if (msg.StartsWith("{") && msg.Contains("\"clearwin\""))
                {
                    string reqId = ExtractJsonString(msg, "requestId");
                    string act = ExtractJsonString(msg, "action");
                    string page = ExtractJsonString(msg, "page");
                    int generation = ExtractJsonInt(msg, "generation", 0);
                    if (!string.IsNullOrEmpty(act))
                    {
                        if (!IsActionAllowed(act))
                        {
                            if ((DateTime.Now - _lastActionDenialLog).TotalSeconds >= ACTION_DENIAL_LOG_COOLDOWN_SECONDS)
                            {
                                _lastActionDenialLog = DateTime.Now;
                                CWLog.Warn("WebUiHost.Message", "BLOCKED unregistered structured action: '" + act + "'");
                            }
                            return;
                        }
                        _owner.HandleStructuredWebAction(reqId, act, msg, page, generation);
                        return;
                    }
                }
                if (!msg.StartsWith("action:", StringComparison.OrdinalIgnoreCase)) return;
                string action = msg.Substring("action:".Length).Trim();
                if (string.IsNullOrEmpty(action)) return;
                if (!IsActionAllowed(action))
                {
                    if ((DateTime.Now - _lastActionDenialLog).TotalSeconds >= ACTION_DENIAL_LOG_COOLDOWN_SECONDS)
                    {
                        _lastActionDenialLog = DateTime.Now;
                        CWLog.Warn("WebUiHost.Message", "BLOCKED unregistered action: '" + action + "'");
                    }
                    return;
                }
                _owner.Dispatcher.BeginInvoke(new Action(() => _owner.HandleWebUiAction(action)), DispatcherPriority.Normal);
            }
            catch (Exception ex3) { CWLog.Error("WebUiHost.Message", ex3); }
        }

        internal static string ExtractJsonString(string json, string key)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return "";
            return AdminSessionManager.ExtractTopLevelJsonString(json, key);
        }

        internal static int ExtractJsonInt(string json, string key, int defaultValue = 0)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return defaultValue;
            return AdminSessionManager.ExtractTopLevelJsonInt(json, key, defaultValue);
        }

        internal static bool ExtractJsonBool(string json, string key, bool defaultValue = false)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key)) return defaultValue;
            string str = ExtractJsonString(json, key);
            if (!string.IsNullOrEmpty(str))
            {
                bool bVal;
                if (bool.TryParse(str, out bVal)) return bVal;
            }
            int idx = json.IndexOf("\"" + key + "\"", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                int colon = json.IndexOf(':', idx);
                if (colon >= 0)
                {
                    string rest = json.Substring(colon + 1).TrimStart();
                    if (rest.StartsWith("true", StringComparison.OrdinalIgnoreCase)) return true;
                    if (rest.StartsWith("false", StringComparison.OrdinalIgnoreCase)) return false;
                }
            }
            return defaultValue;
        }

        internal static bool IsTrustedWebUiUri(string rawUri)
        {
            try
            {
                if (string.IsNullOrEmpty(rawUri)) return false;
                Uri uri;
                if (!Uri.TryCreate(rawUri, UriKind.Absolute, out uri)) return false;
                if (string.Equals(uri.Scheme, "https", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(uri.Host, "appassets.html", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                if (!uri.IsFile) return false;

                string actualPath = Path.GetFullPath(uri.LocalPath);
                string binWebRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "WebUI"));
                string devWebRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Resources", "WebUI"));

                if (!binWebRoot.EndsWith(Path.DirectorySeparatorChar.ToString()))
                    binWebRoot += Path.DirectorySeparatorChar;
                if (!devWebRoot.EndsWith(Path.DirectorySeparatorChar.ToString()))
                    devWebRoot += Path.DirectorySeparatorChar;

                if (actualPath.StartsWith(binWebRoot, StringComparison.OrdinalIgnoreCase) ||
                    actualPath.StartsWith(devWebRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return false;
            }
            catch { return false; }
        }

        public void PushStatus(string message)
        {
            _pendingStatus = message ?? "";
            if (!IsAlive) return;
            try { PostStatus(_pendingStatus); } catch (Exception ex4) { CWLog.Error("WebUiHost.Status", ex4); }
        }

        private void PostStatus(string message)
        {
            string json = "{\"type\":\"status\",\"message\":\"" + EscapeJson(message) + "\"}";
            PostWebJson(json);
        }

        private void PostPendingMetrics()
        {
            if (string.IsNullOrEmpty(_pendingMetrics) || !IsAlive) return;
            try { PostWebJson(_pendingMetrics); } catch (Exception ex) { CWLog.Error("WebUiHost.PendingMetrics", ex); }
        }

        public void PushMetrics(double cpu, double ram, double disk, double temp, double fps, double health, string mode, bool online,
            double gpu, double gpuTemp, double netUp, double netDown, string gpuName, string cpuName, double ramTotalGb)
        {
            try
            {
                if ((DateTime.Now - _lastStorageRead).TotalSeconds >= 10.0)
                {
                    try
                    {
                        string sys = Environment.GetEnvironmentVariable("SystemDrive");
                        if (string.IsNullOrEmpty(sys)) sys = "C:";
                        var di = new DriveInfo(sys);
                        if (di.IsReady)
                        {
                            double total = di.TotalSize / 1073741824.0;
                            double free = di.AvailableFreeSpace / 1073741824.0;
                            _storageTotalGb = total;
                            _storageUsedGb = Math.Max(0, total - free);
                        }
                    }
                    catch {}
                    _lastStorageRead = DateTime.Now;
                }
                double ramUsedGb = ramTotalGb > 0 ? (ramTotalGb * Math.Max(0.0, Math.Min(100.0, ram)) / 100.0) : 0;
                string json = "{\"channel\":\"clearwin\",\"type\":\"metrics\",\"cpu\":" + Sanitize(cpu) + ",\"ram\":" + Sanitize(ram) + ",\"gpu\":" + Sanitize(gpu) + ",\"disk\":" + Sanitize(disk) + ",\"temp\":" + Sanitize(temp) + ",\"gpuTemp\":" + Sanitize(gpuTemp) + ",\"fps\":" + Sanitize(fps) + ",\"health\":" + Sanitize(health) + ",\"mode\":\"" + EscapeJson(mode) + "\",\"online\":" + online.ToString().ToLower() + ",\"netUp\":" + Sanitize(netUp) + ",\"netDown\":" + Sanitize(netDown) + ",\"gpuName\":\"" + EscapeJson(gpuName) + "\",\"cpuName\":\"" + EscapeJson(cpuName) + "\",\"ramTotalGb\":" + Sanitize(ramTotalGb) + ",\"ramUsedGb\":" + Sanitize(ramUsedGb) + ",\"diskTotalGb\":" + Sanitize(_storageTotalGb) + ",\"diskUsedGb\":" + Sanitize(_storageUsedGb) + ",\"sequence\":" + _metricsSequence++ + "}";
                _pendingMetrics = json;
                LatestMetricsJson = json;
                PostWebJson(json);
                _lastMetricsPost = DateTime.Now;
            }
            catch (Exception ex5) { CWLog.Error("WebUiHost.Metrics", ex5); }
        }

        internal void PostWebJson(string json)
        {
            if (!IsAlive) return;
            try
            {
                _webView.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch (System.Runtime.InteropServices.COMException) { }
            catch
            {
                try
                {
                    _webView.CoreWebView2.PostWebMessageAsString(json);
                }
                catch (System.Runtime.InteropServices.COMException) { }
                catch (Exception ex6) { CWLog.Error("WebUiHost.PostWebJson", ex6); }
            }
        }

        private void PostWebJsonFallback(string json)
        {
            if (!IsAlive) return;
            try
            {
                _webView.CoreWebView2.ExecuteScriptAsync("window.__cwLastMetrics = " + json + ";")
                    .ContinueWith(t => { if (t.Exception != null) CWLog.Error("WebUiHost.PostWebJsonFallback", t.Exception); });
            }
            catch (Exception ex2) { CWLog.Error("WebUiHost.PostWebJsonFallback", ex2); }
        }

        private static string Sanitize(double value)
        {
            double clean = (double.IsNaN(value) || double.IsInfinity(value)) ? 0.0 : value;
            return clean.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            StringBuilder sb = new StringBuilder(value.Length);
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

        public void PushAgentLog(string level, string category, string message, string time)
        {
            try
            {
                string json = "{\"type\":\"agentlog\",\"level\":\"" + EscapeJson(level)
                    + "\",\"category\":\"" + EscapeJson(category)
                    + "\",\"message\":\"" + EscapeJson(message)
                    + "\",\"time\":\"" + EscapeJson(time) + "\"}";
                System.Threading.Interlocked.Exchange(ref _pendingAgentLog, json);
            }
            catch (Exception ex7) { CWLog.Error("WebUiHost.AgentLog", ex7); }
        }

        public void PushPage(string page)
        {
            try
            {
                if (string.IsNullOrEmpty(page)) page = "dashboard";
                string json = "{\"type\":\"page\",\"page\":\"" + EscapeJson(page) + "\"}";
                if (!IsAlive) return;
                PostWebJson(json);
            }
            catch (Exception ex7) { CWLog.Error("WebUiHost.Page", ex7); }
        }

        public void PushAiMemory(string json)
        {
            try
            {
                if (string.IsNullOrEmpty(json)) return;
                if (!IsAlive) return;
                PostWebJson(json);
            }
            catch (Exception ex12) { CWLog.Error("WebUiHost.AiMemory", ex12); }
        }

        public void PushAdminAutonomyState(string adminStatus, bool isElevated, bool greenAutonomy, string message)
        {
            try
            {
                if (!IsAlive) return;
                string json = string.Format(
                    "{{\"type\":\"admin_autonomy_state\",\"adminStatus\":\"{0}\",\"isElevated\":{1},\"greenAutonomy\":{2},\"message\":\"{3}\"}}",
                    EscapeJson(adminStatus),
                    isElevated ? "true" : "false",
                    greenAutonomy ? "true" : "false",
                    EscapeJson(message));
                PostWebJson(json);
            }
            catch (Exception exAdmin) { CWLog.Error("WebUiHost.AdminAutonomy", exAdmin); }
        }

        public void PushAuditLogs(string jsonLogs)
        {
            try
            {
                if (!IsAlive || string.IsNullOrEmpty(jsonLogs)) return;
                string json = string.Format("{{\"type\":\"audit_logs\",\"logs\":{0}}}", jsonLogs);
                PostWebJson(json);
            }
            catch (Exception exAudit) { CWLog.Error("WebUiHost.PushAuditLogs", exAudit); }
        }

        public void PushActionCatalog(string jsonCatalog)
        {
            try
            {
                if (!IsAlive || string.IsNullOrEmpty(jsonCatalog)) return;
                string json = string.Format("{{\"type\":\"action_catalog\",\"catalog\":{0}}}", jsonCatalog);
                PostWebJson(json);
            }
            catch (Exception exCat) { CWLog.Error("WebUiHost.PushActionCatalog", exCat); }
        }

        public void PushAiDecision(string jsonDecision)
        {
            try
            {
                if (!IsAlive || string.IsNullOrEmpty(jsonDecision)) return;
                string json = string.Format("{{\"type\":\"ai_decision\",\"decision\":{0}}}", jsonDecision);
                PostWebJson(json);
            }
            catch (Exception exAi) { CWLog.Error("WebUiHost.PushAiDecision", exAi); }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _ready = false;
            if (_aiDecisionHandler != null)
            {
                try { AiDecisionLogger.OnDecisionRecorded -= _aiDecisionHandler; } catch { }
                _aiDecisionHandler = null;
            }
            try { if (_agentLogFlushTimer != null) _agentLogFlushTimer.Stop(); } catch { }
            try
            {
                if (_webView != null)
                {
                    _webView.NavigationCompleted -= OnNavigationCompleted;
                    if (_webView.CoreWebView2 != null)
                    {
                        _webView.CoreWebView2.WebMessageReceived -= OnWebMessage;
                        _webView.CoreWebView2.ProcessFailed -= OnProcessFailed;
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException) { }
            catch (Exception ex8) { CWLog.Error("WebUiHost.Dispose", ex8); }
            try { if (_webView != null) _webView.Dispose(); }
            catch (System.Runtime.InteropServices.COMException) { }
            catch (Exception ex9) { CWLog.Error("WebUiHost.Dispose", ex9); }
        }
    }

    /// <summary>
    /// Validates page navigation keys and prevents stale results or invalid routing in WebUI.
    /// </summary>
    public static class WebUiPageValidator
    {
        private static readonly HashSet<string> _allowedPages = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "dashboard", "quick", "startup", "cleanup", "network", "apps", "ai", "restore", "settings"
        };

        public static bool IsValidPage(string page)
        {
            return !string.IsNullOrEmpty(page) && _allowedPages.Contains(page.Trim());
        }

        public static string NormalizePage(string page)
        {
            if (string.IsNullOrEmpty(page)) return "dashboard";
            string p = page.Trim().ToLowerInvariant();
            return _allowedPages.Contains(p) ? p : "dashboard";
        }

        public static string GetExpectedPageForAction(string action)
        {
            if (string.IsNullOrEmpty(action)) return "dashboard";
            string a = action.ToLowerInvariant().Trim();
            if (a.StartsWith("startup.")) return "startup";
            if (a.StartsWith("cleanup.")) return "cleanup";
            if (a.StartsWith("network.")) return "network";
            if (a.StartsWith("apps.")) return "apps";
            if (a.StartsWith("ai.") || a.StartsWith("ai-memory:") || a.StartsWith("ai:")) return "ai";
            if (a.StartsWith("admin:") || a.StartsWith("autonomy:") || a.StartsWith("catalog:") || a.StartsWith("audit:") || a.StartsWith("settings.")) return "settings";
            if (a.StartsWith("recovery.")) return "restore";
            if (a.StartsWith("quick.") || a.StartsWith("game:")) return "quick";
            return "dashboard";
        }

        public static bool IsStaleResult(string activePage, int currentGeneration, string responsePage, int responseGeneration)
        {
            if (responseGeneration > 0 && currentGeneration > 0 && responseGeneration != currentGeneration)
                return true;
            if (!string.IsNullOrEmpty(responsePage) && !string.IsNullOrEmpty(activePage) &&
                !string.Equals(activePage.Trim(), responsePage.Trim(), StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }
    }
}

