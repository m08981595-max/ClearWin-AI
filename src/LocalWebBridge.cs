using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace ClearWin
{
    internal static class LocalWebBridge
    {
        private static HttpListener _listener;
        private static Thread _thread;
        private static volatile bool _running;
        private static MainWindow _mainWindow;
        public const int DEFAULT_PORT = 27219;
        public static int Port = DEFAULT_PORT;
        public const int PORT = 27219;
        private static string _sessionToken;

        public static string SessionToken
        {
            get { return _sessionToken; }
            internal set { _sessionToken = value; }
        }

        public static void Start(MainWindow owner)
        {
            _mainWindow = owner;
            if (_running) return;
            try
            {
                byte[] tokenBytes = new byte[32];
                using (var rng = new System.Security.Cryptography.RNGCryptoServiceProvider())
                {
                    rng.GetBytes(tokenBytes);
                }
                _sessionToken = BitConverter.ToString(tokenBytes).Replace("-", "").ToLowerInvariant();

                _listener = new HttpListener();
                _listener.Prefixes.Add("http://127.0.0.1:" + Port + "/");
                _listener.Prefixes.Add("http://localhost:" + Port + "/");
                _listener.Start();
                _running = true;
                _thread = new Thread(ListenLoop) { IsBackground = true, Name = "CW_WebBridge" };
                _thread.Start();
                CWLog.Info("WebBridge", "LocalWebBridge listening on port " + Port);
            }
            catch (Exception ex)
            {
                CWLog.Warn("WebBridge", "Could not bind local HTTP listener: " + ex.Message);
            }
        }

        public static void Stop()
        {
            _running = false;
            try
            {
                if (_listener != null && _listener.IsListening)
                {
                    _listener.Stop();
                    _listener.Close();
                }
            }
            catch { }
            _listener = null;
            _sessionToken = null;
        }

        private static void ListenLoop()
        {
            while (_running && _listener != null && _listener.IsListening)
            {
                try
                {
                    var ctx = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => HandleRequest(ctx));
                }
                catch
                {
                    if (!_running) break;
                }
            }
        }

        internal static bool IsTrustedOrigin(string origin, bool allowEmpty = false)
        {
            if (string.IsNullOrEmpty(origin)) return allowEmpty;
            if (origin.Equals("null", StringComparison.OrdinalIgnoreCase)) return false;
            string p1 = "http://127.0.0.1:" + Port;
            string p2 = "http://localhost:" + Port;
            return origin.Equals(p1, StringComparison.OrdinalIgnoreCase) ||
                   origin.StartsWith(p1 + "/", StringComparison.OrdinalIgnoreCase) ||
                   origin.Equals(p2, StringComparison.OrdinalIgnoreCase) ||
                   origin.StartsWith(p2 + "/", StringComparison.OrdinalIgnoreCase) ||
                   origin.Equals("https://appassets.html", StringComparison.OrdinalIgnoreCase) ||
                   origin.StartsWith("https://appassets.html/", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsTrustedReferer(string referer, bool allowEmpty = false)
        {
            if (string.IsNullOrEmpty(referer)) return allowEmpty;
            string p1 = "http://127.0.0.1:" + Port;
            string p2 = "http://localhost:" + Port;
            return referer.Equals(p1, StringComparison.OrdinalIgnoreCase) ||
                   referer.StartsWith(p1 + "/", StringComparison.OrdinalIgnoreCase) ||
                   referer.Equals(p2, StringComparison.OrdinalIgnoreCase) ||
                   referer.StartsWith(p2 + "/", StringComparison.OrdinalIgnoreCase) ||
                   referer.Equals("https://appassets.html", StringComparison.OrdinalIgnoreCase) ||
                   referer.StartsWith("https://appassets.html/", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsSensitiveBridgeAction(string action)
        {
            if (string.IsNullOrEmpty(action)) return true;
            string a = action.ToLowerInvariant().Trim();
            return a.Contains("disable-defender") ||
                   a.Contains("debloat") ||
                   a.Contains("disable-core-isolation") ||
                   a.Contains("remove-onedrive") ||
                   a.Contains("remove-gaming") ||
                   a.Contains("set-services") ||
                   a.Contains("clean") ||
                   a.Contains("kill-process") ||
                   a.Contains("restore-point") ||
                   a.Contains("create-restore") ||
                   a.Contains("flush-dns") ||
                   a.Contains("risky-network") ||
                   a == "services" ||
                   a == "reset" ||
                   a == "optimize-all" ||
                   a == "boost" ||
                   a.StartsWith("tweak:") ||
                   a.StartsWith("undo:") ||
                   a.StartsWith("clean:") ||
                   a.StartsWith("tool:") ||
                   a.StartsWith("admin:") ||
                   a.StartsWith("autonomy:") ||
                   a.StartsWith("startup.") ||
                   a.StartsWith("recovery.") ||
                   a.StartsWith("settings:") ||
                   a.StartsWith("settings.") ||
                   a == "ai:kill-toggle" ||
                   a == "ai.toggle" ||
                   a == "ai:toggle" ||
                   a == "ai.deep-train";
        }

        private static void SendJsonResponse(HttpListenerResponse res, int statusCode, string json)
        {
            res.StatusCode = statusCode;
            res.ContentType = "application/json; charset=utf-8";
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            res.ContentLength64 = bytes.Length;
            res.OutputStream.Write(bytes, 0, bytes.Length);
            res.Close();
        }

        private static void HandleRequest(HttpListenerContext ctx)
        {
            try
            {
                var req = ctx.Request;
                var res = ctx.Response;

                string method = (req.HttpMethod ?? "").ToUpperInvariant();
                if (method != "GET" && method != "POST" && method != "OPTIONS")
                {
                    res.AddHeader("Allow", "GET, POST, OPTIONS");
                    SendJsonResponse(res, 405, "{\"ok\":false,\"error\":\"Method Not Allowed\"}");
                    return;
                }

                string origin = req.Headers["Origin"];
                string referer = req.Headers["Referer"];
                string secFetchSite = req.Headers["Sec-Fetch-Site"];
                string path = req.Url.AbsolutePath.ToLowerInvariant();
                bool isStateAltering = (method == "POST" && path == "/api/action");

                if (string.Equals(secFetchSite, "cross-site", StringComparison.OrdinalIgnoreCase))
                {
                    CWLog.Warn("LocalWebBridge", "Blocked untrusted cross-origin request via Sec-Fetch-Site: " + secFetchSite);
                    SendJsonResponse(res, 403, "{\"ok\":false,\"error\":\"Forbidden: Untrusted cross-site request\"}");
                    return;
                }

                bool hasOrigin = !string.IsNullOrEmpty(origin);
                bool hasReferer = !string.IsNullOrEmpty(referer);

                if (hasOrigin && !IsTrustedOrigin(origin, false))
                {
                    CWLog.Warn("LocalWebBridge", "Blocked untrusted Origin: " + origin);
                    SendJsonResponse(res, 403, "{\"ok\":false,\"error\":\"Forbidden: Untrusted Origin\"}");
                    return;
                }

                if (hasReferer && !IsTrustedReferer(referer, false))
                {
                    CWLog.Warn("LocalWebBridge", "Blocked untrusted Referer: " + referer);
                    SendJsonResponse(res, 403, "{\"ok\":false,\"error\":\"Forbidden: Untrusted Referer\"}");
                    return;
                }

                // SEC-04: Do NOT allow empty origin/referer blindly for state-altering requests (POST /api/action)
                if (isStateAltering && !hasOrigin && !hasReferer)
                {
                    CWLog.Warn("LocalWebBridge", "Blocked state-altering request without Origin or Referer.");
                    SendJsonResponse(res, 403, "{\"ok\":false,\"error\":\"Forbidden: Origin or Referer header required for state-altering requests\"}");
                    return;
                }

                if (hasOrigin && IsTrustedOrigin(origin, false))
                {
                    res.AddHeader("Access-Control-Allow-Origin", origin);
                }
                else
                {
                    res.AddHeader("Access-Control-Allow-Origin", "http://127.0.0.1:" + Port);
                }
                res.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                res.AddHeader("Access-Control-Allow-Headers", "Content-Type, X-Requested-With, X-ClearWin-Token");

                if (method == "OPTIONS")
                {
                    res.StatusCode = 204;
                    res.Close();
                    return;
                }

                if (path == "/api/metrics")
                {
                    if (method != "GET")
                    {
                        res.AddHeader("Allow", "GET");
                        SendJsonResponse(res, 405, "{\"ok\":false,\"error\":\"Method Not Allowed: use GET\"}");
                        return;
                    }

                    string json = WebUiHost.LatestMetricsJson ?? "{\"type\":\"metrics\",\"cpu\":0,\"ram\":0,\"gpu\":0,\"disk\":0,\"temp\":0,\"health\":100,\"mode\":\"AutoPilot\",\"online\":true}";
                    SendJsonResponse(res, 200, json);
                    return;
                }

                if (path == "/api/action")
                {
                    if (method != "POST")
                    {
                        res.AddHeader("Allow", "POST");
                        SendJsonResponse(res, 405, "{\"ok\":false,\"error\":\"Method Not Allowed: use POST\"}");
                        return;
                    }

                    // SEC-04: Enforce X-ClearWin-Token header on POST /api/action
                    string tokenHeader = req.Headers["X-ClearWin-Token"];
                    if (string.IsNullOrEmpty(_sessionToken) || string.IsNullOrEmpty(tokenHeader) || !string.Equals(_sessionToken, tokenHeader, StringComparison.Ordinal))
                    {
                        CWLog.Warn("LocalWebBridge", "Blocked /api/action request due to missing or invalid X-ClearWin-Token.");
                        SendJsonResponse(res, 403, "{\"ok\":false,\"error\":\"Forbidden: Invalid or missing X-ClearWin-Token\"}");
                        return;
                    }

                    string contentType = req.ContentType ?? "";
                    if (!contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
                    {
                        SendJsonResponse(res, 415, "{\"ok\":false,\"error\":\"Unsupported Media Type: expected application/json\"}");
                        return;
                    }

                    if (req.ContentLength64 > 16384)
                    {
                        SendJsonResponse(res, 413, "{\"ok\":false,\"error\":\"Payload Too Large\"}");
                        return;
                    }
                    string body = "";
                    using (var reader = new StreamReader(req.InputStream, req.ContentEncoding))
                    {
                        char[] buf = new char[16384];
                        int read = reader.ReadBlock(buf, 0, buf.Length);
                        if (!reader.EndOfStream)
                        {
                            SendJsonResponse(res, 413, "{\"ok\":false,\"error\":\"Payload Too Large\"}");
                            return;
                        }
                        body = new string(buf, 0, read);
                    }

                    string action = null;
                    if (!string.IsNullOrEmpty(body))
                    {
                        int aIdx = body.IndexOf("\"action\"");
                        if (aIdx >= 0)
                        {
                            int colon = body.IndexOf(':', aIdx);
                            int quote1 = body.IndexOf('"', colon);
                            int quote2 = quote1 >= 0 ? body.IndexOf('"', quote1 + 1) : -1;
                            if (quote1 >= 0 && quote2 > quote1)
                            {
                                action = body.Substring(quote1 + 1, quote2 - quote1 - 1);
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(action))
                    {
                        SendJsonResponse(res, 400, "{\"ok\":false,\"error\":\"Missing action in JSON payload\"}");
                        return;
                    }

                    if (IsSensitiveBridgeAction(action))
                    {
                        CWLog.Warn("LocalWebBridge", "Blocked sensitive action via HTTP bridge: " + action);
                        SendJsonResponse(res, 403, "{\"ok\":false,\"error\":\"Sensitive system operations are forbidden via LocalWebBridge\"}");
                        return;
                    }

                    if (!WebUiHost.IsActionAllowed(action))
                    {
                        SendJsonResponse(res, 400, "{\"ok\":false,\"error\":\"Disallowed action\"}");
                        return;
                    }

                    if (_mainWindow != null)
                    {
                        _mainWindow.Dispatcher.BeginInvoke(new Action(() => _mainWindow.HandleWebUiAction(action)));
                    }

                    string safeAction = action.Replace("\"", "\\\"");
                    SendJsonResponse(res, 200, "{\"ok\":true,\"status\":\"ok\",\"action\":\"" + safeAction + "\"}");
                    return;
                }

                if (path.StartsWith("/api/"))
                {
                    SendJsonResponse(res, 404, "{\"ok\":false,\"error\":\"Unknown API endpoint\"}");
                    return;
                }

                if (method != "GET")
                {
                    res.AddHeader("Allow", "GET");
                    SendJsonResponse(res, 405, "{\"ok\":false,\"error\":\"Method Not Allowed: static assets require GET\"}");
                    return;
                }

                string devWebRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "Resources", "WebUI"));
                string binWebRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "WebUI"));
                string webRoot = (Directory.Exists(devWebRoot) && File.Exists(Path.Combine(devWebRoot, "index.html"))) ? devWebRoot : binWebRoot;

                // SEC-08: Ensure webRoot ends with DirectorySeparatorChar before StartsWith check
                if (!webRoot.EndsWith(Path.DirectorySeparatorChar.ToString()))
                {
                    webRoot += Path.DirectorySeparatorChar;
                }

                string localFile = (path == "/" || string.IsNullOrEmpty(path)) ? "index.html" : path.TrimStart('/');
                string filePath = Path.GetFullPath(Path.Combine(webRoot, localFile));

                if (!filePath.StartsWith(webRoot, StringComparison.OrdinalIgnoreCase))
                {
                    SendJsonResponse(res, 403, "{\"ok\":false,\"error\":\"Forbidden: Path traversal blocked\"}");
                    return;
                }

                if (File.Exists(filePath))
                {
                    byte[] bytes = File.ReadAllBytes(filePath);
                    if (filePath.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                    {
                        res.ContentType = "text/html; charset=utf-8";
                        if (!string.IsNullOrEmpty(_sessionToken))
                        {
                            string html = Encoding.UTF8.GetString(bytes);
                            string tokenScript = string.Format("<script>window.__clearWinToken = '{0}';</script>", _sessionToken);
                            int headIdx = html.IndexOf("<head>", StringComparison.OrdinalIgnoreCase);
                            if (headIdx >= 0)
                            {
                                html = html.Insert(headIdx + 6, tokenScript);
                                bytes = Encoding.UTF8.GetBytes(html);
                            }
                        }
                    }
                    else if (filePath.EndsWith(".js", StringComparison.OrdinalIgnoreCase)) res.ContentType = "application/javascript; charset=utf-8";
                    else if (filePath.EndsWith(".css", StringComparison.OrdinalIgnoreCase)) res.ContentType = "text/css; charset=utf-8";
                    else if (filePath.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)) res.ContentType = "image/svg+xml";
                    else res.ContentType = "application/octet-stream";
                    res.ContentLength64 = bytes.Length;
                    res.OutputStream.Write(bytes, 0, bytes.Length);
                    res.Close();
                    return;
                }

                SendJsonResponse(res, 404, "{\"ok\":false,\"error\":\"File Not Found\"}");
            }
            catch (Exception ex)
            {
                CWLog.Error("WebBridge.HandleRequest", ex);
                try { ctx.Response.Close(); } catch { }
            }
        }
    }
}
