using System;
using System.Collections.Generic;

namespace ClearWin
{
    public partial class SmartAgent
    {
        /// <summary>
        /// Directly trains a specific action's neural ensemble on a provided state feature vector with a target outcome reward.
        /// </summary>
        public void TeachActionWeight(string action, SystemState state, double outcomeScore, int epochs = 25, double lr = 0.15, bool reset = false)
        {
            lock (_mlLock)
            {
                MLEnsemble e;
                if (reset || !_mlEns.TryGetValue(action, out e) || e == null)
                {
                    e = new MLEnsemble(action);
                    _mlEns[action] = e;
                }
                var fv = BuildFV(state);
                double[] inp = _fn.NormalizeReadOnly(fv.ToArray());
                for (int ep = 0; ep < epochs; ep++)
                {
                    e.Train(inp, outcomeScore, lr);
                }
            }
        }

        /// <summary>
        /// Evaluates and returns the exact predicted outcome score from the neural ensemble for an action on a specific state.
        /// </summary>
        public double EvaluateActionWeight(string action, SystemState state)
        {
            lock (_mlLock)
            {
                MLEnsemble e;
                if (!_mlEns.TryGetValue(action, out e)) return 0.5;
                var fv = BuildFV(state);
                double[] inp = _fn.NormalizeReadOnly(fv.ToArray());
                return e.Predict(inp);
            }
        }
    }
}
