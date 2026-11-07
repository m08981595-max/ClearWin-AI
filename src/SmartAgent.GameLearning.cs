using System;
using System.Collections.Generic;

namespace ClearWin
{
    public partial class SmartAgent
    {
        // ===================== AI GAME LEARNING =====================
        private Dictionary<string, GameLearningProfile> _gameProfiles = new Dictionary<string, GameLearningProfile>();
        private DateTime _lastGameLearningUpdate = DateTime.MinValue;
        private const int GAME_LEARNING_INTERVAL_SECONDS = 30;

        public void UpdateGameLearning()
        {
            if ((DateTime.Now - _lastGameLearningUpdate).TotalSeconds < GAME_LEARNING_INTERVAL_SECONDS) return;
            _lastGameLearningUpdate = DateTime.Now;

            lock (_stateLock)
            {
                if (_currentState == null || !_currentState.IsGame) return;

                string gameName = _currentState.ActiveProcess;
                if (string.IsNullOrEmpty(gameName)) return;

                GameLearningProfile profile;
                if (!_gameProfiles.ContainsKey(gameName))
                {
                    profile = new GameLearningProfile();
                    profile.GameName = gameName;
                    profile.FirstPlayed = DateTime.Now;
                    _gameProfiles[gameName] = profile;
                }
                else
                {
                    profile = _gameProfiles[gameName];
                }

                // Confirm this process in the game/app discrimination memory so
                // sustained games are recognized instantly on relaunch.
                string gameKey = gameName.ToLower();
                double mem;
                if (_gameMemory.TryGetValue(gameKey, out mem))
                    _gameMemory[gameKey] = Math.Min(1.0, mem + 0.25);
                else
                    _gameMemory[gameKey] = 0.8;

                profile.LastPlayed = DateTime.Now;
                profile.PlayCount++;
                profile.TotalPlayTimeMinutes += GAME_LEARNING_INTERVAL_SECONDS / 60.0;

                // Record performance metrics
                profile.AvgFps = (profile.AvgFps * (profile.PlayCount - 1) + _currentState.Fps) / profile.PlayCount;
                profile.AvgCpuTemp = (profile.AvgCpuTemp * (profile.PlayCount - 1) + _currentState.CpuTemp) / profile.PlayCount;
                profile.AvgCpuUsage = (profile.AvgCpuUsage * (profile.PlayCount - 1) + _currentState.CpuUsage) / profile.PlayCount;
                profile.AvgRamUsage = (profile.AvgRamUsage * (profile.PlayCount - 1) + _currentState.RamUsage) / profile.PlayCount;

                // Record best settings (highest FPS session)
                if (_currentState.Fps > profile.BestFps)
                {
                    profile.BestFps = _currentState.Fps;
                    profile.BestSettingsSnapshot = string.Format("CPU:{0:F0}% RAM:{1:F0}% Temp:{2:F0}C",
                        _currentState.CpuUsage, _currentState.RamUsage, _currentState.CpuTemp);
                }

                // Record issues encountered
                if (_currentState.Fps > 0 && _currentState.Fps < 30 && !profile.Issues.Contains("Low FPS"))
                    profile.Issues.Add("Low FPS");
                if (_currentState.CpuTemp > _deviceProfile.ThermalWarningTemp && !profile.Issues.Contains("High Temperature"))
                    profile.Issues.Add("High Temperature");
                if (_currentState.RamUsage > 85 && !profile.Issues.Contains("High RAM Usage"))
                    profile.Issues.Add("High RAM Usage");

                // Learn which optimizations worked
                if (_decisionHistory.Count > 0)
                {
                    var lastDecision = _decisionHistory[_decisionHistory.Count - 1];
                    if (lastDecision.Success && lastDecision.Timestamp > profile.LastPlayed.AddSeconds(-GAME_LEARNING_INTERVAL_SECONDS))
                    {
                        if (!profile.SuccessfulOptimizations.Contains(lastDecision.Action))
                            profile.SuccessfulOptimizations.Add(lastDecision.Action);
                    }
                }

                // Auto-generate game-specific suggestions
                if (profile.PlayCount >= 3 && profile.Issues.Count > 0)
                {
                    string issueSummary = string.Join(", ", profile.Issues.ToArray());
                    if (!profile.SuggestionsGenerated)
                    {
                        AddSuggestion(
                            string.Format("Optimize {0}", gameName),
                            string.Format("This game has recurring issues: {0}", issueSummary),
                            string.Format("Consider applying game-specific optimizations. Best recorded FPS: {0:F0}", profile.BestFps),
                            SuggestionPriority.Medium, "Gaming", 15, false);
                        profile.SuggestionsGenerated = true;
                    }
                }
            }
        }

        public GameLearningProfile GetGameProfile(string gameName)
        {
            GameLearningProfile profile;
            if (_gameProfiles.TryGetValue(gameName, out profile)) return profile;
            return null;
        }

        public List<GameLearningProfile> GetAllGameProfiles()
        {
            var result = new List<GameLearningProfile>();
            foreach (var kvp in _gameProfiles) result.Add(kvp.Value);
            return result;
        }

        // ===================== AI PLUGIN SYSTEM =====================
        private List<AIPlugin> _plugins = new List<AIPlugin>();
        private Dictionary<string, PluginResult> _pluginResults = new Dictionary<string, PluginResult>();

        public void RegisterPlugin(AIPlugin plugin)
        {
            plugin.RegisteredAt = DateTime.Now;
            plugin.IsEnabled = true;
            _plugins.Add(plugin);
            AddLog("PLUGIN", "Registered", "Plugin: " + plugin.Name + " v" + plugin.Version);
        }

        public void UnregisterPlugin(string pluginName)
        {
            _plugins.RemoveAll(p => p.Name == pluginName);
            _pluginResults.Remove(pluginName);
            AddLog("PLUGIN", "Unregistered", "Plugin: " + pluginName);
        }

        public void ExecutePlugins()
        {
            foreach (var plugin in _plugins)
            {
                if (!plugin.IsEnabled) continue;
                if ((DateTime.Now - plugin.LastExecuted).TotalSeconds < plugin.IntervalSeconds) continue;

                plugin.LastExecuted = DateTime.Now;
                plugin.ExecutionCount++;

                try
                {
                    var result = plugin.Analyze(_currentState, _deviceProfile, _processImpacts);
                    if (result != null)
                    {
                        result.PluginName = plugin.Name;
                        result.ExecutedAt = DateTime.Now;
                        _pluginResults[plugin.Name] = result;

                        // Process plugin recommendations
                        if (result.Recommendations != null)
                        {
                            foreach (var rec in result.Recommendations)
                            {
                                AddSuggestion(rec.Title, rec.Description, rec.DetailedExplanation,
                                    rec.Priority, "Plugin:" + plugin.Name, rec.Impact, false);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    plugin.ErrorCount++;
                    AddLog("WARN", "Plugin", string.Format("Plugin '{0}' error: {1}", plugin.Name, ex.Message));
                }
            }
        }

        public List<AIPlugin> GetPlugins() { return _plugins; }
        public Dictionary<string, PluginResult> GetPluginResults() { return _pluginResults; }
    }

    // ===================== MODELS =====================
    public class GameLearningProfile
    {
        public string GameName;
        public DateTime FirstPlayed;
        public DateTime LastPlayed;
        public int PlayCount;
        public double TotalPlayTimeMinutes;
        public double AvgFps;
        public double AvgCpuTemp;
        public double AvgCpuUsage;
        public double AvgRamUsage;
        public double BestFps;
        public string BestSettingsSnapshot;
        public List<string> Issues;
        public List<string> SuccessfulOptimizations;
        public bool SuggestionsGenerated;

        public GameLearningProfile()
        {
            Issues = new List<string>();
            SuccessfulOptimizations = new List<string>();
            SuggestionsGenerated = false;
        }
    }

    public class AIPlugin
    {
        public string Name;
        public string Version;
        public string Description;
        public int IntervalSeconds;
        public bool IsEnabled;
        public DateTime RegisteredAt;
        public DateTime LastExecuted;
        public int ExecutionCount;
        public int ErrorCount;

        public AIPlugin()
        {
            Version = "1.0";
            IntervalSeconds = 60;
            LastExecuted = DateTime.MinValue;
        }

        public virtual PluginResult Analyze(SystemState state, DeviceProfile profile, List<ProcessImpact> impacts)
        {
            return null;
        }
    }

    public class PluginResult
    {
        public string PluginName;
        public DateTime ExecutedAt;
        public double Score;
        public string Summary;
        public List<PluginRecommendation> Recommendations;
        public Dictionary<string, string> Data;

        public PluginResult()
        {
            Recommendations = new List<PluginRecommendation>();
            Data = new Dictionary<string, string>();
        }
    }

    public class PluginRecommendation
    {
        public string Title;
        public string Description;
        public string DetailedExplanation;
        public SuggestionPriority Priority;
        public double Impact;
    }
}
