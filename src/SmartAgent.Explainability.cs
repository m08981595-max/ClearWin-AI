using System;
using System.Collections.Generic;
using System.Linq;

namespace ClearWin
{
    public partial class SmartAgent
    {
        // === Explainability Fields ===
        private List<EliteDecisionExplanation> _explanationHistory = new List<EliteDecisionExplanation>();
        private DateTime _lastExplanation = DateTime.MinValue;
        private const int MAX_EXPLANATIONS = 100;
        private DecisionTree _activeDecisionTree;

        // === Public Accessors ===
        public List<EliteDecisionExplanation> GetExplanationHistory() { return new List<EliteDecisionExplanation>(_explanationHistory); }
        public DecisionTree GetActiveDecisionTree() { return _activeDecisionTree; }
        public EliteDecisionExplanation GetLastEliteExplanation()
        {
            return _explanationHistory.Count > 0 ? _explanationHistory[_explanationHistory.Count - 1] : null;
        }

        // === Generate Full Explanation for a Decision ===
        public EliteDecisionExplanation ExplainDecisionFull(AgentDecision chosen, List<AgentDecision> alternatives)
        {
            var explanation = new EliteDecisionExplanation();
            explanation.Timestamp = DateTime.Now;
            explanation.ChosenAction = chosen;
            explanation.Alternatives = alternatives != null ? new List<AgentDecision>(alternatives) : new List<AgentDecision>();

            // 1. Data sources used (Arabic)
            explanation.DataSources = new List<DataSource>();
            explanation.DataSources.Add(new DataSource
            {
                Name = "CPU Usage",
                Value = string.Format("{0:F1}%", _smoothCpu),
                Relevant = chosen.Category == DecisionCategory.ProcessPriority || _smoothCpu > 70
            });
            explanation.DataSources.Add(new DataSource
            {
                Name = "RAM Usage",
                Value = string.Format("{0:F1}%", _smoothRam),
                Relevant = chosen.Category == DecisionCategory.MemoryClean || _smoothRam > 60
            });
            explanation.DataSources.Add(new DataSource
            {
                Name = "Temperature",
                Value = string.Format("{0:F0}C", _smoothTemp),
                Relevant = chosen.Category == DecisionCategory.ThermalProtect || _smoothTemp > 60
            });
            explanation.DataSources.Add(new DataSource
            {
                Name = "GPU Usage",
                Value = string.Format("{0:F1}%", _smoothGpu),
                Relevant = chosen.Category == DecisionCategory.GameBoost || _smoothGpu > 70
            });
            explanation.DataSources.Add(new DataSource
            {
                Name = "FPS",
                Value = string.Format("{0:F0}", _smoothFps),
                Relevant = _currentState.IsGame
            });
            explanation.DataSources.Add(new DataSource
            {
                Name = "FPS Stability",
                Value = string.Format("{0:F0}%", _currentState.FpsStability),
                Relevant = _currentState.IsGame
            });
            explanation.DataSources.Add(new DataSource
            {
                Name = "Process Count",
                Value = _currentState.ProcessCount.ToString(),
                Relevant = _currentState.ProcessCount > 150
            });
            explanation.DataSources.Add(new DataSource
            {
                Name = "Battery",
                Value = _batteryPercent + "% " + (_isOnBattery ? "(بطارية)" : "(شاحن)"),
                Relevant = _isOnBattery
            });
            explanation.DataSources.Add(new DataSource
            {
                Name = "App Mode",
                Value = _appMode.ToString(),
                Relevant = true
            });

            // 2. Decision path (why chosen)
            explanation.DecisionPath = new List<DecisionStep>();
            explanation.DecisionPath.Add(new DecisionStep
            {
                Step = 1,
                Description = "System state analyzed",
                Detail = string.Format("CPU={0:F0}% RAM={1:F0}% Temp={2:F0}C FPS={3:F0}",
                _smoothCpu, _smoothRam, _smoothTemp, _smoothFps)
            });
            explanation.DecisionPath.Add(new DecisionStep
            {
                Step = 2,
                Description = "Multiple agents analyzed the system",
                Detail = string.Format("{0} agents voted", _lastVotes != null ? _lastVotes.Count : 0)
            });
            explanation.DecisionPath.Add(new DecisionStep
            {
                Step = 3,
                Description = "Conflicts resolved",
                Detail = "Best balance between competing goals found"
            });
            explanation.DecisionPath.Add(new DecisionStep
            {
                Step = 4,
                Description = "Risk assessed",
                Detail = string.Format("Risk score calculated, confidence={0:F0}%", chosen.Confidence * 100)
            });
            explanation.DecisionPath.Add(new DecisionStep
            {
                Step = 5,
                Description = "Action selected",
                Detail = chosen.Action + " - " + chosen.Reason
            });

            // 3. Why alternatives were rejected
            explanation.RejectedAlternatives = new List<RejectedAlternative>();
            if (alternatives != null)
            {
                foreach (var alt in alternatives)
                {
                    if (alt.Action == chosen.Action) continue;
                    var rejected = new RejectedAlternative();
                    rejected.Action = alt.Action;
                    rejected.Reason = string.Format("Confidence {0:F0}% vs chosen {1:F0}%",
                        alt.Confidence * 100, chosen.Confidence * 100);
                    if (alt.Risk > chosen.Risk)
                        rejected.Reason += " | Higher risk: " + string.Format("{0:F0}%", alt.Risk * 100);
                    if (alt.Confidence < chosen.Confidence)
                        rejected.Reason += " | Lower confidence";
                    explanation.RejectedAlternatives.Add(rejected);
                }
            }

            // 4. Confidence breakdown
            explanation.ConfidenceBreakdown = new ConfidenceBreakdown();
            explanation.ConfidenceBreakdown.BaseConfidence = chosen.Confidence;
            explanation.ConfidenceBreakdown.ThermalFactor = _smoothTemp > 75 ? 0.9 : 1.0;
            explanation.ConfidenceBreakdown.ResourceFactor = _smoothRam > 80 ? 0.85 : 1.0;
            explanation.ConfidenceBreakdown.HistoricalFactor = GetHistoricalConfidenceFactor(chosen.Action);
            explanation.ConfidenceBreakdown.FinalConfidence = chosen.Confidence;

            // 5. Risk summary (Arabic)
            explanation.RiskSummary = string.Format("المخاطرة: {0:F0}% | التراجع ممكن: {1}",
                chosen.Risk * 100, chosen.Action.Contains("Battery") || chosen.Action.Contains("Priority") ? "نعم" : "محدود");

            // Store
            _explanationHistory.Add(explanation);
            if (_explanationHistory.Count > MAX_EXPLANATIONS) _explanationHistory.RemoveAt(0);

            // Build decision tree
            _activeDecisionTree = BuildDecisionTree(chosen, alternatives);

            _lastExplanation = DateTime.Now;
            return explanation;
        }

        // === Get Historical Confidence Factor ===
        private double GetHistoricalConfidenceFactor(string action)
        {
            int total;
            if (!_actionTotalCount.TryGetValue(action, out total) || total < 3) return 1.0;
            int success;
            if (!_actionSuccessCount.TryGetValue(action, out success)) return 1.0;
            double successRate = (double)success / total;
            return 0.5 + successRate * 0.5;
        }

        // === Build Decision Tree ===
        private DecisionTree BuildDecisionTree(AgentDecision chosen, List<AgentDecision> alternatives)
        {
            var tree = new DecisionTree();
            tree.Root = new TreeNode
            {
                Label = "تحليل النظام",
                Value = string.Format("معالج={0:F0}% ذاكرة={1:F0}% حرارة={2:F0}C",
                _smoothCpu, _smoothRam, _smoothTemp),
                Children = new List<TreeNode>()
            };

            // Branch: Current state assessment
            var stateBranch = new TreeNode
            {
                Label = "تقييم الحالة",
                Value = _appMode.ToString() + " وضع",
                Children = new List<TreeNode>()
            };

            // Branch: Agent votes
            var votesBranch = new TreeNode
            {
                Label = "أصوات الوكلاء",
                Value = string.Format("{0} وكلاء محللون", _lastVotes != null ? _lastVotes.Count : 0),
                Children = new List<TreeNode>()
            };
            if (_lastVotes != null)
            {
                foreach (var vote in _lastVotes)
                {
                    if (vote.Action != "NONE")
                    {
                        votesBranch.Children.Add(new TreeNode
                        {
                            Label = vote.AgentName,
                            Value = vote.Action + " (conf=" + string.Format("{0:F0}%", vote.Confidence * 100) + ")",
                            Children = new List<TreeNode>()
                        });
                    }
                }
            }

            // Branch: Decision
            var decisionBranch = new TreeNode
            {
                Label = "القرار",
                Value = chosen.Action,
                Children = new List<TreeNode>()
            };
            decisionBranch.Children.Add(new TreeNode
            {
                Label = "الثقة",
                Value = string.Format("{0:F0}%", chosen.Confidence * 100),
                Children = new List<TreeNode>()
            });
            decisionBranch.Children.Add(new TreeNode
            {
                Label = "المخاطرة",
                Value = string.Format("{0:F0}%", chosen.Risk * 100),
                Children = new List<TreeNode>()
            });

            // Branch: Rejected
            if (alternatives != null && alternatives.Count > 1)
            {
                var rejectedBranch = new TreeNode
                {
                    Label = "البدائل المرفوضة",
                    Value = (alternatives.Count - 1) + " بدائل تم تقييمها",
                    Children = new List<TreeNode>()
                };
                foreach (var alt in alternatives)
                {
                    if (alt.Action != chosen.Action)
                    {
                        rejectedBranch.Children.Add(new TreeNode
                        {
                            Label = alt.Action,
                            Value = "ثقة=" + string.Format("{0:F0}%", alt.Confidence * 100) + " مخاطرة=" + string.Format("{0:F0}%", alt.Risk * 100),
                            Children = new List<TreeNode>()
                        });
                    }
                }
                tree.Root.Children.Add(rejectedBranch);
            }

            tree.Root.Children.Add(stateBranch);
            tree.Root.Children.Add(votesBranch);
            tree.Root.Children.Add(decisionBranch);

            return tree;
        }
    }

    // === Supporting Types for Explainability ===
    public class EliteDecisionExplanation
    {
        public DateTime Timestamp;
        public AgentDecision ChosenAction;
        public List<AgentDecision> Alternatives;
        public List<DataSource> DataSources;
        public List<DecisionStep> DecisionPath;
        public List<RejectedAlternative> RejectedAlternatives;
        public ConfidenceBreakdown ConfidenceBreakdown;
        public string RiskSummary;
    }

    public class DataSource
    {
        public string Name;
        public string Value;
        public bool Relevant;
    }

    public class DecisionStep
    {
        public int Step;
        public string Description;
        public string Detail;
    }

    public class RejectedAlternative
    {
        public string Action;
        public string Reason;
    }

    public class ConfidenceBreakdown
    {
        public double BaseConfidence;
        public double ThermalFactor;
        public double ResourceFactor;
        public double HistoricalFactor;
        public double FinalConfidence;
    }

    public class DecisionTree
    {
        public TreeNode Root;
    }

    public class TreeNode
    {
        public string Label;
        public string Value;
        public List<TreeNode> Children;
    }
}
