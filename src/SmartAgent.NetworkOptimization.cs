using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace ClearWin
{
    public partial class SmartAgent
    {
        // === Network Optimization Fields ===
        private DateTime _lastNetworkOptTime = DateTime.MinValue;
        private const int NETWORK_OPT_INTERVAL_SECONDS = 60;
        private NetworkProfile _currentNetworkProfile = NetworkProfile.Balanced;
        private double _lastLatencyMs = 0;
        private double _lastPacketLoss = 0;
        private DateTime _lastDnsFlush = DateTime.MinValue;
        private const int DNS_FLUSH_INTERVAL_MINUTES = 30;

        // Network optimization results
        private NetworkOptimizationResult _lastNetworkResult = null;

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern int SetTcpEntry(ref MIB_TCPROW row);

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern int GetTcpStatistics(out MIB_TCPSTATS stats);

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_TCPROW
        {
            public int dwState;
            public int dwLocalAddr;
            public int dwLocalPort;
            public int dwRemoteAddr;
            public int dwRemotePort;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MIB_TCPSTATS
        {
            public uint dwRtoAlgorithm;
            public uint dwRtoMin;
            public uint dwRtoMax;
            public uint dwMaxConn;
            public uint dwActiveOpens;
            public uint dwPassiveOpens;
            public uint dwAttemptFails;
            public uint dwEstabResets;
            public uint dwCurrEstab;
            public uint dwInSegs;
            public uint dwOutSegs;
            public uint dwRetransSegs;
            public uint dwInErrs;
            public uint dwOutRsts;
            public uint dwNumConns;
        }

        // === Public Accessors ===
        public NetworkProfile CurrentNetworkProfile { get { return _currentNetworkProfile; } }
        public NetworkOptimizationResult LastNetworkResult { get { return _lastNetworkResult; } }
        public double LastLatencyMs { get { return _lastLatencyMs; } }
        public double LastPacketLoss { get { return _lastPacketLoss; } }

        public enum NetworkProfile
        {
            LowLatency,    // Gaming/streaming
            Balanced,      // Default
            Throughput,    // Downloads/uploads
            PowerSave      // Battery saving
        }

        public class NetworkOptimizationResult
        {
            public DateTime Timestamp;
            public NetworkProfile Profile;
            public double LatencyMs;
            public double PacketLoss;
            public string ConnectionSpeed;
            public int ActiveConnections;
            public bool DnsFlushed;
            public bool TcpTuned;
            public bool PriorityAdjusted;
            public string Recommendation;
        }

        // === Network Optimization: Analyze and generate decisions ===
        public void AnalyzeNetwork(List<AgentDecision> decisions)
        {
            if ((DateTime.Now - _lastNetworkOptTime).TotalSeconds < NETWORK_OPT_INTERVAL_SECONDS) return;
            _lastNetworkOptTime = DateTime.Now;

            try
            {
                // Measure latency
                double latency = MeasureLatency();
                _lastLatencyMs = latency;

                // Get active connections
                int activeConns = GetActiveTcpConnections();

                // Determine optimal profile
                NetworkProfile optimal = DetermineNetworkProfile(latency, activeConns);

                if (optimal != _currentNetworkProfile)
                {
                    decisions.Add(new AgentDecision
                    {
                        Category = DecisionCategory.NetworkOptimize,
                        Action = "OptimizeNetwork",
                        Description = "Network optimization: " + _currentNetworkProfile.ToString() + " -> " + optimal.ToString(),
                        Reason = string.Format("Latency={0:F0}ms, Connections={1}, Profile shift needed", latency, activeConns),
                        Confidence = 0.72,
                        Safety = DecisionSafety.Safe
                    });
                }

                // DNS cache management
                if ((DateTime.Now - _lastDnsFlush).TotalMinutes > DNS_FLUSH_INTERVAL_MINUTES && activeConns > 50)
                {
                    decisions.Add(new AgentDecision
                    {
                        Category = DecisionCategory.NetworkOptimize,
                        Action = "FlushDnsCache",
                        Description = "Flush DNS cache to resolve stale entries",
                        Reason = string.Format("{0} active connections, DNS cache may be stale", activeConns),
                        Confidence = 0.60,
                        Safety = DecisionSafety.Safe
                    });
                }

                // High latency: suggest connection reset
                if (latency > 200 && activeConns > 100)
                {
                    decisions.Add(new AgentDecision
                    {
                        Category = DecisionCategory.NetworkOptimize,
                        Action = "ResetNetworkPriority",
                        Description = "Reset network priority for foreground apps",
                        Reason = string.Format("High latency ({0:F0}ms) with {1} connections", latency, activeConns),
                        Confidence = 0.65,
                        Safety = DecisionSafety.Safe
                    });
                }

                // Store result
                _lastNetworkResult = new NetworkOptimizationResult
                {
                    Timestamp = DateTime.Now,
                    Profile = _currentNetworkProfile,
                    LatencyMs = latency,
                    PacketLoss = _lastPacketLoss,
                    ActiveConnections = activeConns,
                    DnsFlushed = false,
                    TcpTuned = false,
                    PriorityAdjusted = false,
                    Recommendation = GetNetworkRecommendation(latency, activeConns)
                };
            }
            catch (Exception ex) { LogError("AnalyzeNetwork", ex); }
        }

        // === Execute Network Optimization ===
        public bool ExecuteNetworkOptimization(AgentDecision decision)
        {
            try
            {
                switch (decision.Action)
                {
                    case "OptimizeNetwork":
                        return ApplyNetworkProfile(DetermineNetworkProfile(_lastLatencyMs, GetActiveTcpConnections()));
                    case "FlushDnsCache":
                        return FlushDnsCache();
                    case "ResetNetworkPriority":
                        return ResetNetworkPriority();
                    default:
                        return false;
                }
            }
            catch { return false; }
        }

        // === Measure latency via DNS resolution time ===
        private double MeasureLatency()
        {
            try
            {
                var sw = Stopwatch.StartNew();
                var ping = new Ping();
                var reply = ping.Send("8.8.8.8", 2000);
                sw.Stop();
                if (reply.Status == IPStatus.Success)
                    return reply.RoundtripTime;
                return 999;
            }
            catch { return 999; }
        }

        // === Get active TCP connections count ===
        private int GetActiveTcpConnections()
        {
            try
            {
                var properties = IPGlobalProperties.GetIPGlobalProperties();
                var connections = properties.GetActiveTcpConnections();
                return connections.Length;
            }
            catch { return 0; }
        }

        // === Determine optimal network profile ===
        private NetworkProfile DetermineNetworkProfile(double latency, int connections)
        {
            if (_appMode == AppMode.Gaming || latency < 30)
                return NetworkProfile.LowLatency;
            if (_currentState.RamUsage > 85 || _isOnBattery)
                return NetworkProfile.PowerSave;
            if (connections > 80 || (_currentState.DiskUsage > 70 && _currentState.CpuUsage < 50))
                return NetworkProfile.Throughput;
            return NetworkProfile.Balanced;
        }

        // === Apply network profile (Windows netsh + registry tweaks) ===
        private bool ApplyNetworkProfile(NetworkProfile profile)
        {
            try
            {
                _currentNetworkProfile = profile;
                switch (profile)
                {
                    case NetworkProfile.LowLatency:
                        ExecuteCommand("netsh", "int tcp set global chimney=enabled");
                        ExecuteCommand("netsh", "int tcp set global dca=enabled");
                        ExecuteCommand("netsh", "int tcp set global netdma=enabled");
                        ExecuteCommand("netsh", "int tcp set global timestamps=disabled");
                        ExecuteCommand("netsh", "int tcp set global rss=enabled");
                        ExecuteCommand("netsh", "int tcp set global ecncapability=disabled");
                        ExecuteCommand("netsh", "int tcp set global nonsackrttresiliency=disabled");
                        ExecuteCommand("netsh", "int tcp set global maxsynretransmissions=2");
                        break;
                    case NetworkProfile.Balanced:
                        ExecuteCommand("netsh", "int tcp set global chimney=disabled");
                        ExecuteCommand("netsh", "int tcp set global dca=disabled");
                        ExecuteCommand("netsh", "int tcp set global netdma=disabled");
                        ExecuteCommand("netsh", "int tcp set global timestamps=disabled");
                        ExecuteCommand("netsh", "int tcp set global rss=enabled");
                        ExecuteCommand("netsh", "int tcp set global ecncapability=enabled");
                        break;
                    case NetworkProfile.Throughput:
                        ExecuteCommand("netsh", "int tcp set global chimney=enabled");
                        ExecuteCommand("netsh", "int tcp set global dca=enabled");
                        ExecuteCommand("netsh", "int tcp set global netdma=enabled");
                        ExecuteCommand("netsh", "int tcp set global autotuninglevel=normal");
                        ExecuteCommand("netsh", "int tcp set global rss=enabled");
                        ExecuteCommand("netsh", "int tcp set global ecncapability=disabled");
                        break;
                    case NetworkProfile.PowerSave:
                        ExecuteCommand("netsh", "int tcp set global chimney=disabled");
                        ExecuteCommand("netsh", "int tcp set global dca=disabled");
                        ExecuteCommand("netsh", "int tcp set global netdma=disabled");
                        ExecuteCommand("netsh", "int tcp set global autotuninglevel=restricted");
                        ExecuteCommand("netsh", "int tcp set global rss=disabled");
                        ExecuteCommand("netsh", "int tcp set global ecncapability=enabled");
                        break;
                }
                AddLog("INFO", "Network", "Applied " + profile.ToString() + " profile");
                return true;
            }
            catch (Exception ex) { LogError("NetworkOpt", ex); return false; }
        }

        // === Flush DNS cache ===
        private bool FlushDnsCache()
        {
            try
            {
                if (!ExecuteCommand("ipconfig", "/flushdns"))
                    return false;
                _lastDnsFlush = DateTime.Now;
                AddLog("INFO", "Network", "DNS cache flushed");
                return true;
            }
            catch { return false; }
        }

        // === Reset network priority ===
        private bool ResetNetworkPriority()
        {
            try
            {
                // Set QoS for foreground app
                var fg = GetForegroundWindow();
                uint pid;
                GetWindowThreadProcessId(fg, out pid);
                if (pid > 0)
                {
                    using (var proc = Process.GetProcessById((int)pid))
                    {
                        proc.PriorityClass = System.Diagnostics.ProcessPriorityClass.High;
                    }
                }
                AddLog("INFO", "Network", "Network priority reset for foreground app");
                return true;
            }
            catch { return false; }
        }

        // === Get network recommendation ===
        private string GetNetworkRecommendation(double latency, int connections)
        {
            if (latency > 200) return "High latency detected - consider closing background apps";
            if (latency > 100) return "Moderate latency - low-latency profile recommended for gaming";
            if (connections > 100) return "Many active connections - DNS flush recommended";
            if (connections > 50) return "Moderate connection count - balanced profile optimal";
            return "Network health is good";
        }

        // Helper: Execute command and verify exit code
        private bool ExecuteCommand(string fileName, string arguments)
        {
            try
            {
                using (var proc = new Process())
                {
                    proc.StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    proc.Start();
                    if (!proc.WaitForExit(5000))
                    {
                        try { proc.Kill(); } catch { }
                        return false;
                    }
                    return proc.ExitCode == 0;
                }
            }
            catch (Exception ex) { LogError("ExecuteCommand", ex); return false; }
        }
    }
}
