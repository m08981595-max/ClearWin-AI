using System;
using System.Collections.Generic;

namespace ClearWin
{
    public sealed class AnalyzerManager
    {
        private readonly List<IAnalyzer> _analyzers = new List<IAnalyzer>();
        private readonly object _lock = new object();
        private Action<string, string> _errorLogger;

        public AnalyzerManager() { }
        public AnalyzerManager(Action<string, string> errorLogger) { _errorLogger = errorLogger; }

        public void Register(IAnalyzer analyzer)
        {
            if (analyzer == null) return;
            lock (_lock) { _analyzers.Add(analyzer); }
        }

        public void RegisterAll(IEnumerable<IAnalyzer> analyzers)
        {
            if (analyzers == null) return;
            lock (_lock)
            {
                foreach (var a in analyzers)
                {
                    if (a != null) _analyzers.Add(a);
                }
            }
        }

        public List<AnalysisResult> Analyze(SystemSnapshot sysState)
        {
            var results = new List<AnalysisResult>();
            IAnalyzer[] analyzers;
            lock (_lock) { analyzers = _analyzers.ToArray(); }
            for (int i = 0; i < analyzers.Length; i++)
            {
                try
                {
                    if (analyzers[i].CanRun(sysState))
                    {
                        var r = analyzers[i].Analyze(sysState);
                        if (r != null) results.Add(r);
                    }
                }
                catch (Exception ex)
                {
                    if (_errorLogger != null)
                        _errorLogger(analyzers[i].Name, ex.Message);
                }
            }
            return results;
        }

        public int Count
        {
            get { lock (_lock) { return _analyzers.Count; } }
        }
    }
}
