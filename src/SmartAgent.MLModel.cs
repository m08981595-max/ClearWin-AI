using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace ClearWin
{
    public class MLFeatureVector
    {
        public double CpuUsage, RamUsage, GpuUsage, DiskUsage, CpuTemp, GpuTemp;
        public double Fps, Fps01Low, FpsStability;
        public int ProcessCount;
        public double HourOfDay, MinuteOfHour;
        public double IsGaming, IsOnBattery, BatteryLevel;
        public double CpuTrend, RamTrend, TempTrend;

        public double[] ToArray()
        {
            return new double[] { CpuUsage, RamUsage, GpuUsage, DiskUsage, CpuTemp, GpuTemp,
                Fps, Fps01Low, FpsStability, ProcessCount, HourOfDay, MinuteOfHour,
                IsGaming, IsOnBattery, BatteryLevel, CpuTrend, RamTrend, TempTrend };
        }
        public static int FeatureCount { get { return 18; } }
    }

    public class MLNeuralNetwork
    {
        public string Action;
        private const int INPUT = 18, H1 = 32, H2 = 16;
        public double[] W1, B1, W2, B2, W3; public double B3;
        public double[] BN1G, BN1B, BN2G, BN2B;
        public double[] BN1RM, BN1RV, BN2RM, BN2RV;
        private const double BNM = 0.99, BNE = 1e-5;
        public double[] MW1, MB1, MW2, MB2, MW3; public double MB3;
        public double[] VW1, VB1, VW2, VB2, VW3; public double VB3;
        public double[] MBN1G, VBN1G, MBN1B, VBN1B, MBN2G, VBN2G, MBN2B, VBN2B;
        private bool[] _dm1, _dm2;
        private const double DR = 0.2;
        private const double ACAP = 4.0;
        private const double GRAD_CLIP = 0.25;
        public double[] Z1, A1, Z2, A2; public double A3;
        private double[] _bn1, _bn2;
        private double[] _zHat1, _zHat2;
        private double _invStd1, _invStd2;
        private double _bn1M, _bn1V, _bn2M, _bn2V;
        private double[] _dW1, _dB1, _dW2, _dB2, _dW3;
        private double[] _dA2, _dBN2, _dA1, _dBN1;
        private double[] _dBN1G, _dBN1B, _dBN2G, _dBN2B;
        public double Acc; public int TS; public DateTime LU; public double LR;
        private Random _rnd;

        public MLNeuralNetwork() : this(Environment.TickCount) { }
        public MLNeuralNetwork(int seed)
        {
            _rnd = new Random(seed); LR = 0.12;
            W1 = new double[INPUT * H1]; B1 = new double[H1];
            W2 = new double[H1 * H2]; B2 = new double[H2];
            W3 = new double[H2]; B3 = 0;
            BN1G = new double[H1]; BN1B = new double[H1];
            BN2G = new double[H2]; BN2B = new double[H2];
            BN1RM = new double[H1]; BN1RV = new double[H1];
            BN2RM = new double[H2]; BN2RV = new double[H2];
            for (int i = 0; i < H1; i++) BN1RV[i] = 1.0;
            for (int i = 0; i < H2; i++) BN2RV[i] = 1.0;
            MW1 = new double[INPUT * H1]; MB1 = new double[H1];
            MW2 = new double[H1 * H2]; MB2 = new double[H2];
            MW3 = new double[H2]; MB3 = 0;
            VW1 = new double[INPUT * H1]; VB1 = new double[H1];
            VW2 = new double[H1 * H2]; VB2 = new double[H2];
            VW3 = new double[H2]; VB3 = 0;
            MBN1G = new double[H1]; VBN1G = new double[H1];
            MBN1B = new double[H1]; VBN1B = new double[H1];
            MBN2G = new double[H2]; VBN2G = new double[H2];
            MBN2B = new double[H2]; VBN2B = new double[H2];
            _dm1 = new bool[H1]; _dm2 = new bool[H2];
            Z1 = new double[H1]; A1 = new double[H1];
            Z2 = new double[H2]; A2 = new double[H2];
            _bn1 = new double[H1]; _bn2 = new double[H2];
            _zHat1 = new double[H1]; _zHat2 = new double[H2];
            _dW1 = new double[INPUT * H1]; _dB1 = new double[H1];
            _dW2 = new double[H1 * H2]; _dB2 = new double[H2];
            _dW3 = new double[H2]; _dA2 = new double[H2];
            _dBN2 = new double[H2]; _dA1 = new double[H1];
            _dBN1 = new double[H1];
            _dBN1G = new double[H1]; _dBN1B = new double[H1];
            _dBN2G = new double[H2]; _dBN2B = new double[H2];
            double s1 = Math.Sqrt(2.0 / INPUT), s2 = Math.Sqrt(2.0 / H1), s3 = Math.Sqrt(2.0 / H2);
            for (int i = 0; i < W1.Length; i++) W1[i] = (_rnd.NextDouble() * 2 - 1) * s1;
            for (int i = 0; i < B1.Length; i++) B1[i] = 0.0; // Pre-LayerNorm bias is translation-invariant; BN1B is the active bias
            for (int i = 0; i < W2.Length; i++) W2[i] = (_rnd.NextDouble() * 2 - 1) * s2;
            for (int i = 0; i < B2.Length; i++) B2[i] = 0.0; // Pre-LayerNorm bias is translation-invariant; BN2B is the active bias
            for (int i = 0; i < W3.Length; i++) W3[i] = (_rnd.NextDouble() * 2 - 1) * s3;
            for (int i = 0; i < H1; i++) { BN1G[i] = 1.0; BN1B[i] = 0.0; }
            for (int i = 0; i < H2; i++) { BN2G[i] = 1.0; BN2B[i] = 0.0; }
        }

        private static double LReLU(double x) { return x > 0 ? x : 0.01 * x; }
        private static double LReLUD(double x) { return x > 0 ? 1.0 : 0.01; }
        private static double Sigm(double x) { if (double.IsNaN(x)) return 0.5; if (x < -500) return 0; if (x > 500) return 1; return 1.0 / (1.0 + Math.Exp(-x)); }
        private static double SigmD(double o) { if (double.IsNaN(o)) return 0.0; return o * (1.0 - o); }
        public static double SigmStatic(double x) { if (double.IsNaN(x)) return 0.5; if (x < -500) return 0; if (x > 500) return 1; return 1.0 / (1.0 + Math.Exp(-x)); }

        // Layer Normalization: per-sample normalization across the layer activations,
        // perfectly suited for streaming inference / batch size 1. Applies learned gain (gamma)
        // and bias (beta) parameters with numerical epsilon.
        private void BN(double[] x, double[] n, double[] zHat, double[] g, double[] b, int sz, ref double curM, ref double curV, ref double invStdOut)
        {
            double mean = 0;
            for (int i = 0; i < sz; i++) mean += x[i];
            mean /= sz;
            curM = mean;

            double variance = 0;
            for (int i = 0; i < sz; i++)
            {
                double diff = x[i] - mean;
                variance += diff * diff;
            }
            variance /= sz;
            curV = variance;

            double invStd = 1.0 / Math.Sqrt(variance + BNE);
            invStdOut = invStd;
            for (int i = 0; i < sz; i++)
            {
                double normalized = (x[i] - mean) * invStd;
                zHat[i] = normalized;
                n[i] = normalized * (g != null && i < g.Length ? g[i] : 1.0) + (b != null && i < b.Length ? b[i] : 0.0);
            }
        }

        private static void LayerNormBackward(double[] dOut, double[] zHat, double[] gamma, double invStd, int sz, double[] dZ, double[] dGamma, double[] dBeta)
        {
            double sumDzHat = 0.0;
            double sumDzHatZHat = 0.0;
            for (int i = 0; i < sz; i++)
            {
                double g = (gamma != null && i < gamma.Length) ? gamma[i] : 1.0;
                dGamma[i] = dOut[i] * zHat[i];
                dBeta[i] = dOut[i];
                double dzh = dOut[i] * g;
                dZ[i] = dzh; // temporary store for dZHat[i]
                sumDzHat += dzh;
                sumDzHatZHat += dzh * zHat[i];
            }
            double invN = 1.0 / sz;
            for (int i = 0; i < sz; i++)
            {
                dZ[i] = invStd * (dZ[i] - sumDzHat * invN - zHat[i] * (sumDzHatZHat * invN));
            }
        }

        public double Forward(double[] input) { return Fwd(input, false); }
        public double ForwardTrain(double[] input) { return Fwd(input, true); }

        private double Fwd(double[] input, bool train)
        {
            for (int h = 0; h < H1; h++) { double s = B1[h]; for (int i = 0; i < INPUT; i++) s += input[i] * W1[i * H1 + h]; Z1[h] = s; }
            BN(Z1, _bn1, _zHat1, BN1G, BN1B, H1, ref _bn1M, ref _bn1V, ref _invStd1);
            for (int h = 0; h < H1; h++) { A1[h] = LReLU(_bn1[h]); if (A1[h] > ACAP) A1[h] = ACAP; if (train) { _dm1[h] = _rnd.NextDouble() >= DR; if (!_dm1[h]) A1[h] = 0; } }
            for (int h = 0; h < H2; h++) { double s = B2[h]; for (int j = 0; j < H1; j++) s += (train ? A1[j] : A1[j] * (1.0 - DR)) * W2[j * H2 + h]; Z2[h] = s; }
            BN(Z2, _bn2, _zHat2, BN2G, BN2B, H2, ref _bn2M, ref _bn2V, ref _invStd2);
            for (int h = 0; h < H2; h++) { A2[h] = LReLU(_bn2[h]); if (A2[h] > ACAP) A2[h] = ACAP; if (train) { _dm2[h] = _rnd.NextDouble() >= DR; if (!_dm2[h]) A2[h] = 0; } }
            double o = B3; for (int h = 0; h < H2; h++) o += (train ? A2[h] : A2[h] * (1.0 - DR)) * W3[h];
            A3 = Sigm(o); return A3;
        }

        /// <summary>
        /// Computes exact analytical gradients (without dropout or gradient clipping) for finite-difference verification.
        /// Note: In inference mode (train=false), hidden activations are scaled by (1 - DR) at the linear layer input.
        /// </summary>
        public void ComputeAnalyticalGradients(double[] input, double target,
            out double[] dW1, out double[] dB1, out double[] dW2, out double[] dB2, out double[] dW3, out double dB3,
            out double[] dBN1G, out double[] dBN1B, out double[] dBN2G, out double[] dBN2B)
        {
            double o = Forward(input);
            double scale = 1.0 - DR;
            double oe = (o - target) * SigmD(o);
            dW3 = new double[H2];
            for (int h = 0; h < H2; h++) dW3[h] = oe * (A2[h] * scale);
            dB3 = oe;

            double[] dOut2 = new double[H2];
            for (int h = 0; h < H2; h++)
            {
                double rawA2 = LReLU(_bn2[h]);
                dOut2[h] = (rawA2 > ACAP) ? 0.0 : (oe * W3[h] * scale * LReLUD(_bn2[h]));
            }
            double[] dZ2 = new double[H2];
            dBN2G = new double[H2];
            dBN2B = new double[H2];
            LayerNormBackward(dOut2, _zHat2, BN2G, _invStd2, H2, dZ2, dBN2G, dBN2B);

            dW2 = new double[H1 * H2];
            dB2 = new double[H2];
            for (int j = 0; j < H1; j++)
            {
                double a1Scaled = A1[j] * scale;
                for (int h = 0; h < H2; h++) dW2[j * H2 + h] = dZ2[h] * a1Scaled;
            }
            for (int h = 0; h < H2; h++) dB2[h] = dZ2[h];

            double[] dOut1 = new double[H1];
            for (int j = 0; j < H1; j++)
            {
                double rawA1 = LReLU(_bn1[j]);
                if (rawA1 > ACAP) { dOut1[j] = 0.0; continue; }
                double s = 0.0;
                for (int h = 0; h < H2; h++) s += dZ2[h] * W2[j * H2 + h] * scale;
                dOut1[j] = s * LReLUD(_bn1[j]);
            }
            double[] dZ1 = new double[H1];
            dBN1G = new double[H1];
            dBN1B = new double[H1];
            LayerNormBackward(dOut1, _zHat1, BN1G, _invStd1, H1, dZ1, dBN1G, dBN1B);

            dW1 = new double[INPUT * H1];
            dB1 = new double[H1];
            for (int i = 0; i < INPUT; i++)
                for (int h = 0; h < H1; h++)
                    dW1[i * H1 + h] = dZ1[h] * input[i];
            for (int h = 0; h < H1; h++) dB1[h] = dZ1[h];
        }

        public void Train(double[] input, double target, double lr, double importanceWeight = 1.0)
        {
            double o = ForwardTrain(input), oe = (o - target) * SigmD(o);
            double[] dW3 = _dW3; for (int h = 0; h < H2; h++) dW3[h] = ClipGrad(oe * A2[h]);
            double dB3 = ClipGrad(oe);
            double[] dA2 = _dA2;
            for (int h = 0; h < H2; h++)
            {
                double rawA2 = LReLU(_bn2[h]);
                dA2[h] = (!_dm2[h] || rawA2 > ACAP) ? 0.0 : (oe * W3[h] * LReLUD(_bn2[h]));
            }
            double[] dBN2 = _dBN2;
            LayerNormBackward(dA2, _zHat2, BN2G, _invStd2, H2, dBN2, _dBN2G, _dBN2B);
            for (int h = 0; h < H2; h++) { _dBN2G[h] = ClipGrad(_dBN2G[h]); _dBN2B[h] = ClipGrad(_dBN2B[h]); }

            double[] dW2 = _dW2, dB2 = _dB2;
            for (int j = 0; j < H1; j++) { double a1 = A1[j]; for (int h = 0; h < H2; h++) dW2[j * H2 + h] = ClipGrad(dBN2[h] * a1); }
            for (int h = 0; h < H2; h++) dB2[h] = 0.0; // Zero pre-LayerNorm bias gradient (BN2B handles post-norm shift)
            double[] dA1 = _dA1;
            for (int j = 0; j < H1; j++)
            {
                double rawA1 = LReLU(_bn1[j]);
                if (!_dm1[j] || rawA1 > ACAP) { dA1[j] = 0.0; continue; }
                double s = 0;
                for (int h = 0; h < H2; h++) s += dBN2[h] * W2[j * H2 + h];
                dA1[j] = s * LReLUD(_bn1[j]);
            }
            double[] dBN1 = _dBN1;
            LayerNormBackward(dA1, _zHat1, BN1G, _invStd1, H1, dBN1, _dBN1G, _dBN1B);
            for (int h = 0; h < H1; h++) { _dBN1G[h] = ClipGrad(_dBN1G[h]); _dBN1B[h] = ClipGrad(_dBN1B[h]); }

            double[] dW1 = _dW1, dB1 = _dB1;
            for (int i = 0; i < INPUT; i++) for (int h = 0; h < H1; h++) dW1[i * H1 + h] = ClipGrad(dBN1[h] * input[i]);
            for (int h = 0; h < H1; h++) dB1[h] = 0.0; // Zero pre-LayerNorm bias gradient (BN1B handles post-norm shift)
            double l2 = 0.001;

            // Apply Importance Sampling weight (Schaul et al. 2015) to counteract sampling bias
            double effectiveLr = lr * Math.Max(0.01, Math.Min(1.0, importanceWeight));

            // True AdamW optimizer with first moment (momentum) and second moment (RMS)
            // with bias correction and decoupled weight decay, including LayerNorm scale/bias.
            int stepNum = TS + 1;
            AdamWStep(W3, dW3, MW3, VW3, effectiveLr, l2, stepNum);
            AdamWScalarStep(ref B3, dB3, ref MB3, ref VB3, effectiveLr, stepNum);
            AdamWStep(W2, dW2, MW2, VW2, effectiveLr, l2, stepNum);
            AdamWStep(B2, dB2, MB2, VB2, effectiveLr, 0, stepNum);
            AdamWStep(BN2G, _dBN2G, MBN2G, VBN2G, effectiveLr, 0, stepNum);
            AdamWStep(BN2B, _dBN2B, MBN2B, VBN2B, effectiveLr, 0, stepNum);
            AdamWStep(W1, dW1, MW1, VW1, effectiveLr, l2, stepNum);
            AdamWStep(B1, dB1, MB1, VB1, effectiveLr, 0, stepNum);
            AdamWStep(BN1G, _dBN1G, MBN1G, VBN1G, effectiveLr, 0, stepNum);
            AdamWStep(BN1B, _dBN1B, MBN1B, VBN1B, effectiveLr, 0, stepNum);
            TS++;
        }

        private static double ClipGrad(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return 0;
            if (v > GRAD_CLIP) return GRAD_CLIP;
            if (v < -GRAD_CLIP) return -GRAD_CLIP;
            return v;
        }

        private static void AdamWStep(double[] w, double[] g, double[] m, double[] v, double lr, double l2, int t)
        {
            const double beta1 = 0.9;
            const double beta2 = 0.999;
            const double eps = 1e-8;

            double biasCorrection1 = 1.0 - Math.Pow(beta1, Math.Max(1, t));
            double biasCorrection2 = 1.0 - Math.Pow(beta2, Math.Max(1, t));
            if (biasCorrection1 <= 0) biasCorrection1 = 1e-5;
            if (biasCorrection2 <= 0) biasCorrection2 = 1e-5;

            for (int i = 0; i < w.Length; i++)
            {
                double grad = g[i];
                if (double.IsNaN(grad) || double.IsInfinity(grad)) grad = 0;

                m[i] = beta1 * m[i] + (1.0 - beta1) * grad;
                v[i] = beta2 * v[i] + (1.0 - beta2) * (grad * grad);

                double mHat = m[i] / biasCorrection1;
                double vHat = v[i] / biasCorrection2;

                w[i] = w[i] * (1.0 - lr * l2) - (lr * mHat) / (Math.Sqrt(Math.Max(0, vHat)) + eps);
                if (double.IsNaN(w[i]) || double.IsInfinity(w[i])) w[i] = 0.0;
            }
        }

        private static void AdamWScalarStep(ref double w, double g, ref double m, ref double v, double lr, int t)
        {
            const double beta1 = 0.9;
            const double beta2 = 0.999;
            const double eps = 1e-8;

            double biasCorrection1 = 1.0 - Math.Pow(beta1, Math.Max(1, t));
            double biasCorrection2 = 1.0 - Math.Pow(beta2, Math.Max(1, t));
            if (biasCorrection1 <= 0) biasCorrection1 = 1e-5;
            if (biasCorrection2 <= 0) biasCorrection2 = 1e-5;

            double grad = g;
            if (double.IsNaN(grad) || double.IsInfinity(grad)) grad = 0;

            m = beta1 * m + (1.0 - beta1) * grad;
            v = beta2 * v + (1.0 - beta2) * (grad * grad);

            double mHat = m / biasCorrection1;
            double vHat = v / biasCorrection2;

            w -= (lr * mHat) / (Math.Sqrt(Math.Max(0, vHat)) + eps);
            if (double.IsNaN(w) || double.IsInfinity(w)) w = 0.0;
        }
    }

    public class MLEnsemble
    {
        public MLNeuralNetwork[] Nets; public string Action;
        private const int NC = 3;
        public MLEnsemble(string a) { Action = a; Nets = new MLNeuralNetwork[NC]; for (int i = 0; i < NC; i++) Nets[i] = new MLNeuralNetwork(a.GetHashCode() + i * 9973); }
        public double Predict(double[] input) { double s = 0; for (int i = 0; i < NC; i++) s += Nets[i].Forward(input); return s / NC; }
        public double Variance(double[] input) { double m = Predict(input), v = 0; for (int i = 0; i < NC; i++) { double d = Nets[i].Forward(input) - m; v += d * d; } return v / NC; }
        public double GetConf(double[] input) { double m = Predict(input), v = Variance(input); double u = Math.Min(0.89, v * 5.0); return Math.Max(0.1, Math.Min(0.99, m * (1.0 - u) + 0.1)); }
        public void Train(double[] input, double target, double lr, double importanceWeight = 1.0) { for (int i = 0; i < NC; i++) Nets[i].Train(input, target, lr, importanceWeight); }
        public int TS { get { int s = 0; for (int i = 0; i < NC; i++) s += Nets[i].TS; return s / NC; } }
        public double Acc { get { double s = 0; for (int i = 0; i < NC; i++) s += Nets[i].Acc; return s / NC; } }
    }

    public class PrioritizedReplayBuffer
    {
        private List<MLTrainingSample> _buf; private List<double> _pri;
        private int _max, _idx; private Random _rnd;
        private const double ALPHA = 0.6, EPS = 1e-5;
        private int _sampleStepCount = 0;

        public PrioritizedReplayBuffer(int max) { _max = max; _buf = new List<MLTrainingSample>(max); _pri = new List<double>(max); _rnd = new Random(); _idx = 0; }
        public void Add(MLTrainingSample s, double p) { p = Math.Max(p, EPS); if (_buf.Count < _max) { _buf.Add(s); _pri.Add(p); } else { _buf[_idx] = s; _pri[_idx] = p; } _idx = (_idx + 1) % _max; }
        public void Add(MLTrainingSample s) { Add(s, 1.0); }
        public int Count { get { return _buf.Count; } }
        public int SampleStepCount { get { return _sampleStepCount; } set { _sampleStepCount = value; } }

        // Importance Sampling Annealing Beta: beta_0 = 0.4 -> beta_max = 1.0 (Schaul et al. 2015)
        public double Beta
        {
            get
            {
                return Math.Min(1.0, 0.4 + 0.6 * (_sampleStepCount / 5000.0));
            }
        }

        public List<MLTrainingSample> SampleWithWeights(int bs, out double[] isWeights)
        {
            if (_buf.Count == 0)
            {
                isWeights = new double[0];
                return new List<MLTrainingSample>();
            }

            int n = Math.Min(bs, _buf.Count);
            double[] probs = new double[_buf.Count]; double tp = 0;
            for (int i = 0; i < _buf.Count; i++) { probs[i] = Math.Pow(_pri[i], ALPHA); tp += probs[i]; }
            for (int i = 0; i < _buf.Count; i++) probs[i] /= tp;

            double beta = Beta;
            double countN = _buf.Count;
            var res = new List<MLTrainingSample>();
            isWeights = new double[n];
            double[] rawWeights = new double[n];
            double maxWeight = 0;

            for (int s = 0; s < n; s++)
            {
                double r = _rnd.NextDouble(), cum = 0;
                int selectedIdx = 0;
                for (int i = 0; i < _buf.Count; i++)
                {
                    cum += probs[i];
                    if (r <= cum || i == _buf.Count - 1)
                    {
                        selectedIdx = i;
                        break;
                    }
                }

                var sample = _buf[selectedIdx];
                res.Add(sample);

                // Schaul et al.: w_i = (N * P(i))^(-beta)
                double p_i = Math.Max(probs[selectedIdx], EPS);
                double w_i = Math.Pow(countN * p_i, -beta);
                rawWeights[s] = w_i;
                if (w_i > maxWeight) maxWeight = w_i;
            }

            if (maxWeight <= 0) maxWeight = 1.0;
            for (int s = 0; s < n; s++)
            {
                double normW = rawWeights[s] / maxWeight;
                normW = Math.Max(0.01, Math.Min(1.0, normW));
                isWeights[s] = normW;
                res[s].ImportanceWeight = normW;
            }

            _sampleStepCount++;
            return res;
        }

        public List<MLTrainingSample> Sample(int bs)
        {
            double[] weights;
            return SampleWithWeights(bs, out weights);
        }
    }

    public class RunningNormalizer
    {
        private readonly object _lock = new object();
        private double[] _m, _s; private int[] _c; private const int F = 18;
        public RunningNormalizer() { _m = new double[F]; _s = new double[F]; _c = new int[F]; for (int i = 0; i < F; i++) { _m[i] = 50; _s[i] = 25; } }

        public bool CheckSensorAnomaly(double[] raw, out string faultReason)
        {
            if (raw == null || raw.Length < F)
            {
                faultReason = "Incomplete or null feature vector";
                return true;
            }
            for (int i = 0; i < F; i++)
            {
                if (double.IsNaN(raw[i]) || double.IsInfinity(raw[i]))
                {
                    faultReason = string.Format(CultureInfo.InvariantCulture, "Non-finite value (NaN/Infinity) at feature #{0}", i);
                    return true;
                }
            }
            if (raw[0] < 0 || raw[0] > 100) { faultReason = string.Format(CultureInfo.InvariantCulture, "Physical CPU usage out of bounds: {0:F1}%", raw[0]); return true; }
            if (raw[1] < 0 || raw[1] > 100) { faultReason = string.Format(CultureInfo.InvariantCulture, "Physical RAM usage out of bounds: {0:F1}%", raw[1]); return true; }
            if (raw[2] < 0 || raw[2] > 100) { faultReason = string.Format(CultureInfo.InvariantCulture, "Physical GPU usage out of bounds: {0:F1}%", raw[2]); return true; }
            if (raw[3] < 0 || raw[3] > 100) { faultReason = string.Format(CultureInfo.InvariantCulture, "Physical Disk usage out of bounds: {0:F1}%", raw[3]); return true; }
            if (raw[4] < -10 || raw[4] > 115) { faultReason = string.Format(CultureInfo.InvariantCulture, "Physical CPU temperature out of bounds: {0:F1}C", raw[4]); return true; }
            if (raw[5] < -10 || raw[5] > 115) { faultReason = string.Format(CultureInfo.InvariantCulture, "Physical GPU temperature out of bounds: {0:F1}C", raw[5]); return true; }
            if (raw[6] < 0 || raw[6] > 1000) { faultReason = string.Format(CultureInfo.InvariantCulture, "Physical FPS out of bounds: {0:F1}", raw[6]); return true; }
            if (raw[7] < 0 || raw[7] > 1000) { faultReason = string.Format(CultureInfo.InvariantCulture, "Physical FPS 0.1% low out of bounds: {0:F1}", raw[7]); return true; }
            if (raw[8] < 0 || raw[8] > 100) { faultReason = string.Format(CultureInfo.InvariantCulture, "Physical FPS stability out of bounds: {0:F1}%", raw[8]); return true; }
            if (raw[9] < 0 || raw[9] > 5000) { faultReason = string.Format(CultureInfo.InvariantCulture, "Physical process count out of bounds: {0}", (int)raw[9]); return true; }
            if (raw[14] < 0 || raw[14] > 100) { faultReason = string.Format(CultureInfo.InvariantCulture, "Physical battery level out of bounds: {0:F1}%", raw[14]); return true; }
            faultReason = null;
            return false;
        }

        public bool CheckStatisticalAnomaly(double[] raw, out string faultReason)
        {
            if (raw == null || raw.Length < F)
            {
                faultReason = "Incomplete or null feature vector";
                return true;
            }
            lock (_lock)
            {
                // Inspect physical signals (indices 0..9) with sufficient sample history (N > 20)
                for (int i = 0; i <= 9; i++)
                {
                    if (_c[i] > 20 && _s[i] > 0.1)
                    {
                        double z = Math.Abs((raw[i] - _m[i]) / _s[i]);
                        if (z > 4.5)
                        {
                            faultReason = string.Format(CultureInfo.InvariantCulture, "Statistical anomaly on feature #{0}: Z={1:F2} exceeds 4.5 sigma limit", i, z);
                            return true;
                        }
                    }
                }
            }
            faultReason = null;
            return false;
        }

        public bool IsAnomaly(double[] raw, out string faultReason)
        {
            if (CheckSensorAnomaly(raw, out faultReason)) return true;
            if (CheckStatisticalAnomaly(raw, out faultReason)) return true;
            faultReason = null;
            return false;
        }

        /// <summary>
        /// Updates running mean/std statistics with a single genuine observation and returns normalized features.
        /// Should ONLY be called once when a new sample is observed, never inside training epochs or read-only inference.
        /// </summary>
        public double[] Observe(double[] raw)
        {
            lock (_lock)
            {
                double[] n = new double[F];
                for (int i = 0; i < F; i++)
                {
                    if (raw == null || i >= raw.Length || double.IsNaN(raw[i]) || double.IsInfinity(raw[i])) { n[i] = 0; continue; }
                    _c[i]++; double d = raw[i] - _m[i]; _m[i] += d / _c[i]; double d2 = raw[i] - _m[i];
                    if (_c[i] > 1) _s[i] = Math.Max(0.1, Math.Sqrt((_s[i] * _s[i] * (_c[i] - 1) + d * d2) / _c[i]));
                    n[i] = _s[i] > 0.01 ? (raw[i] - _m[i]) / _s[i] : 0;
                    n[i] = Math.Max(-3, Math.Min(3, n[i]));
                }
                return n;
            }
        }

        /// <summary>
        /// Pure read-only z-score normalization using current running statistics without mutating _m, _s, or _c.
        /// Used during TrainNN epochs, validation evaluation, and live inference.
        /// </summary>
        public double[] NormalizeReadOnly(double[] raw)
        {
            lock (_lock)
            {
                double[] n = new double[F];
                for (int i = 0; i < F; i++)
                {
                    if (raw == null || i >= raw.Length || double.IsNaN(raw[i]) || double.IsInfinity(raw[i])) { n[i] = 0; continue; }
                    n[i] = _s[i] > 0.01 ? (raw[i] - _m[i]) / _s[i] : 0;
                    n[i] = Math.Max(-3, Math.Min(3, n[i]));
                }
                return n;
            }
        }

        public double[] Norm(double[] raw)
        {
            return Observe(raw);
        }
        public double[] GetMeans() { lock (_lock) return (double[])_m.Clone(); }
        public double[] GetStds() { lock (_lock) return (double[])_s.Clone(); }
        public int[] GetCounts() { lock (_lock) return (int[])_c.Clone(); }
        public void SetStats(double[] m, double[] s, int[] c)
        {
            lock (_lock)
            {
                for (int i = 0; i < F; i++)
                {
                    if (i < m.Length && m[i] > 0) _m[i] = m[i];
                    if (i < s.Length && s[i] >= 0.1) _s[i] = s[i];
                    if (i < c.Length && c[i] >= 0) _c[i] = c[i];
                }
            }
        }
    }

    public class MLTrainingSample
    {
        private static long _nextSampleId = 1;
        public long SampleId;
        public MLFeatureVector Features;
        public string ActionTaken;
        public double OutcomeScore;
        public DateTime Timestamp;
        public double ImportanceWeight = 1.0;

        public MLTrainingSample()
        {
            SampleId = Interlocked.Increment(ref _nextSampleId);
        }

        public MLTrainingSample Clone()
        {
            return new MLTrainingSample
            {
                SampleId = this.SampleId,
                Features = this.Features,
                ActionTaken = this.ActionTaken,
                OutcomeScore = this.OutcomeScore,
                Timestamp = this.Timestamp,
                ImportanceWeight = this.ImportanceWeight
            };
        }
    }

    public class AnalysisStressResult
    {
        public long TotalAnalyses;
        public long DecisionsProduced;
        public long TotalExceptions;
        public double AvgLatencyMs;
        public double MaxLatencyMs;
        public double P50LatencyMs;
        public double P95LatencyMs;
        public double P99LatencyMs;
        public double JitterMs;
        public double ElapsedSeconds;
        public long EstimatedAnalysesPerDay;
        public double StartRamMB;
        public double EndRamMB;
    }

    // Population Stability Index (PSI) Drift Detector across all 18 features
    public class PSIDriftDetector
    {
        private const int BINS = 10;
        private const int MAX_WINDOW = 500;
        private const double EPS = 1e-4;
        private const double DRIFT_THRESHOLD = 0.20;

        private readonly object _lock = new object();
        private List<double[]> _baseline = new List<double[]>();
        private RingBuffer<double[]> _current = new RingBuffer<double[]>(MAX_WINDOW);

        public int BaselineCount { get { lock (_lock) return _baseline.Count; } }
        public int CurrentCount { get { lock (_lock) return _current.Count; } }

        public void SetBaseline(IEnumerable<double[]> samples)
        {
            if (samples == null) return;
            lock (_lock)
            {
                _baseline.Clear();
                foreach (var s in samples)
                {
                    if (s != null && s.Length >= 18 && _baseline.Count < MAX_WINDOW)
                        _baseline.Add((double[])s.Clone());
                }
            }
        }

        public void ResetCurrent()
        {
            lock (_lock)
            {
                _current.Clear();
            }
        }

        public void AddObservation(double[] features)
        {
            if (features == null || features.Length < 18) return;
            lock (_lock)
            {
                if (_baseline.Count < MAX_WINDOW)
                {
                    _baseline.Add((double[])features.Clone());
                }
                _current.Add((double[])features.Clone());
            }
        }

        public double CalculateFeaturePSI(int featureIndex)
        {
            if (featureIndex < 0 || featureIndex >= 18) return 0.0;
            double[] baseVals;
            double[] actVals;
            lock (_lock)
            {
                if (_baseline.Count < 20 || _current.Count < 20) return 0.0;
                baseVals = new double[_baseline.Count];
                for (int i = 0; i < _baseline.Count; i++) baseVals[i] = _baseline[i][featureIndex];

                actVals = new double[_current.Count];
                for (int i = 0; i < _current.Count; i++) actVals[i] = _current[i][featureIndex];
            }

            Array.Sort(baseVals);
            int nBase = baseVals.Length;
            int nAct = actVals.Length;

            // Determine bin boundaries from sorted baseline (deciles)
            double[] cuts = new double[BINS - 1];
            for (int b = 1; b < BINS; b++)
            {
                int idx = (int)Math.Round((double)b * (nBase - 1) / BINS);
                cuts[b - 1] = baseVals[Math.Max(0, Math.Min(nBase - 1, idx))];
            }

            int[] baseCounts = new int[BINS];
            int[] actCounts = new int[BINS];

            for (int i = 0; i < nBase; i++)
            {
                double v = baseVals[i];
                int bin = 0;
                while (bin < BINS - 1 && v > cuts[bin]) bin++;
                baseCounts[bin]++;
            }

            for (int i = 0; i < nAct; i++)
            {
                double v = actVals[i];
                int bin = 0;
                while (bin < BINS - 1 && v > cuts[bin]) bin++;
                actCounts[bin]++;
            }

            // Sum PSI_b = (A_b - E_b) * ln(A_b / E_b)
            double psi = 0.0;
            for (int b = 0; b < BINS; b++)
            {
                double e_b = (double)baseCounts[b] / nBase;
                double a_b = (double)actCounts[b] / nAct;

                double e_smooth = Math.Max(e_b, EPS);
                double a_smooth = Math.Max(a_b, EPS);

                psi += (a_smooth - e_smooth) * Math.Log(a_smooth / e_smooth);
            }

            return Math.Max(0.0, psi);
        }

        public double[] CalculateAllPSI()
        {
            double[] res = new double[18];
            for (int i = 0; i < 18; i++)
            {
                res[i] = CalculateFeaturePSI(i);
            }
            return res;
        }

        public double GetMaxPSI(out int worstFeatureIndex)
        {
            double[] all = CalculateAllPSI();
            double max = 0.0;
            worstFeatureIndex = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] > max)
                {
                    max = all[i];
                    worstFeatureIndex = i;
                }
            }
            return max;
        }

        public bool CheckDrift(out string driftSummary)
        {
            int worstIdx;
            double maxPsi = GetMaxPSI(out worstIdx);
            if (maxPsi >= DRIFT_THRESHOLD)
            {
                driftSummary = string.Format(CultureInfo.InvariantCulture,
                    "DRIFT_DETECTED: Feature #{0} PSI={1:F4} exceeds threshold 0.20 (Significant Distribution Shift)",
                    worstIdx, maxPsi);
                return true;
            }
            driftSummary = string.Format(CultureInfo.InvariantCulture,
                "STABLE: Max PSI={0:F4} (Feature #{1}) within safe bounds (< 0.20)",
                maxPsi, worstIdx);
            return false;
        }
    }

    // In-app Windows Optimization AI. Replaces the removed SmolLM2 360M local
    // model: no Python, no external process, always available. The real brain is
    // the neural ensemble (MLNeuralNetwork/MLEnsemble) specialized for Windows
    // optimization, and it learns continuously from real optimization outcomes.
    public class WindowsOptimizationAI
    {
        // Lazy AI: set to true when deliberately deactivated (idle / user stop).
        public volatile bool IntentionallyStopped = false;
        // Manual control: when the user activates/stops the AI from the UI it
        // stays on until stopped and stays off until reactivated.
        public volatile bool ManualModelControl = false;
        public DateTime LastActivity = DateTime.MinValue;
        public string LastPrompt = "";
        public string LastError = "";
        public DateTime LastStartAttempt = DateTime.Now;
        public int TotalInferences = 0;
        public int FailedInferences = 0;

        public bool IsRunning { get { return !IntentionallyStopped; } }
        public bool ModelReady { get { return !IntentionallyStopped; } }
        public double SuccessRate
        {
            get
            {
                int total = TotalInferences;
                if (total == 0) return 1.0;
                return (double)(total - FailedInferences) / (double)total;
            }
        }
        public string GetScriptPath() { return "In-app (neural ensemble)"; }
        public string PythonPath { get { return "In-app"; } }

        public bool Start() { IntentionallyStopped = false; ManualModelControl = false; LastActivity = DateTime.Now; return true; }
        public void Stop() { IntentionallyStopped = true; }
        public bool Restart() { IntentionallyStopped = false; LastActivity = DateTime.Now; return true; }
    }

    public partial class SmartAgent
    {
        private Dictionary<string, MLEnsemble> _mlEns = new Dictionary<string, MLEnsemble>();
        private PrioritizedReplayBuffer _mlRP;
        private RingBuffer<MLTrainingSample> _mlTB = new RingBuffer<MLTrainingSample>(ML_MAX);
        private DateTime _lastML = DateTime.MinValue;
        // Guards _mlEns / _mlTB / _mlRP / _fn / training metrics: training runs on
        // a background thread while the UI thread reads predictions + metrics.
        private readonly object _mlLock = new object();
        private const int ML_INT = 10, ML_MAX = 1200, ML_MIN = 15, ML_EP = 2, ML_BS = 32;
        private MLFeatureVector _preAF; private double _preAHS;
        private RunningNormalizer _fn;
        private PSIDriftDetector _driftDetector = new PSIDriftDetector();
        public PSIDriftDetector DriftDetector { get { return _driftDetector; } }
        public PrioritizedReplayBuffer ReplayBuffer { get { return _mlRP; } }
        public RunningNormalizer Normalizer { get { return _fn; } }
        private Dictionary<string, double> _aLR = new Dictionary<string, double>();
        private Dictionary<string, int> _aTC = new Dictionary<string, int>();
        // === Training Metrics: loss / accuracy / improvement tracking ===
        private int _trainCallCount = 0;
        private int _trainEpochsTotal = 0;
        private int _trainBatchesTotal = 0;
        private double _trainLossLast = 0;
        private double _trainAccLast = 0;
        private double _trainAccBest = 0;
        private static readonly string[] ML_AC = new[] { 
            "AggressiveClean", "LightClean", "PreemptiveClean", "PurgeStandbyList", "LimitBackground", 
            "BoostGamePriority", "EmergencyThrottle", "ReduceThermal", "StabilizeFrames", "ReduceMicroStutter", 
            "BatterySaver", "BalancedPerformance", "SetGameAffinity", "UnparkAllCores", "SetLowIoPriority", 
            "OptimizeDesktopSmoothness", "TrimInactiveWorkingSets",
            "OptimizeGpuPerformance", "ShedBackgroundGpuMemory", "PurgeGpuShaderCache"
        };

        // === Training Concurrency & Gaming Deferral Controls ===
        private int _isTrainingInProgress = 0;
        private volatile bool _trainingDeferred = false;
        private AgentDecision _cachedMlDecision = null;
        private long _cachedMlDecisionTick = -1;

        public bool IsTrainingInProgress { get { return _isTrainingInProgress == 1; } }
        public bool IsTrainingDeferred { get { return _trainingDeferred; } set { _trainingDeferred = value; } }

        public void InitializeMLModel() { _fn = new RunningNormalizer(); _mlRP = new PrioritizedReplayBuffer(2000); _driftDetector = new PSIDriftDetector(); LoadMLModel(); }
        public void CapturePreActionState() { _preAF = BuildFV(); _preAHS = SystemHealthScore; }

        private MLFeatureVector BuildFV()
        {
            return new MLFeatureVector
            {
                CpuUsage = _smoothCpu, RamUsage = _smoothRam, GpuUsage = _smoothGpu,
                DiskUsage = _currentState.DiskUsage, CpuTemp = _smoothTemp, GpuTemp = _currentState.GpuTemp,
                Fps = _currentState.Fps, Fps01Low = _currentState.Fps01Low, FpsStability = _currentState.FpsStability,
                ProcessCount = _currentState.ProcessCount,
                HourOfDay = DateTime.Now.Hour + DateTime.Now.Minute / 60.0, MinuteOfHour = DateTime.Now.Minute,
                IsGaming = _currentState.IsGame ? 1.0 : 0.0, IsOnBattery = _isOnBattery ? 1.0 : 0.0, BatteryLevel = _batteryPercent,
                CpuTrend = _smoothCpu - _prevState.CpuUsage, RamTrend = _smoothRam - _prevState.RamUsage, TempTrend = _tempRateOfChange
            };
        }

        // State-explicit feature vector: prediction input is derived from the
        // passed snapshot s instead of the live _currentState global. This is
        // what makes the simulation/test harness (GetOptimizationDecisionForState
        // fed synthetic states) match runtime behavior 100% - previously the
        // ensemble scored the live global state even when a synthetic state was
        // under evaluation, a blind spot for the test harness. Trends are
        // neutral (0) for an explicit snapshot because no live previous state
        // exists to diff against.
        private MLFeatureVector BuildFV(SystemState s)
        {
            if (s == null) return BuildFV();
            return new MLFeatureVector
            {
                CpuUsage = s.CpuUsage, RamUsage = s.RamUsage, GpuUsage = s.GpuUsage,
                DiskUsage = s.DiskUsage, CpuTemp = s.CpuTemp, GpuTemp = s.GpuTemp,
                Fps = s.Fps, Fps01Low = s.Fps01Low, FpsStability = s.FpsStability,
                ProcessCount = s.ProcessCount,
                HourOfDay = DateTime.Now.Hour + DateTime.Now.Minute / 60.0, MinuteOfHour = DateTime.Now.Minute,
                IsGaming = s.IsGame ? 1.0 : 0.0,
                IsOnBattery = (!s.IsCharging || _isOnBattery) ? 1.0 : 0.0,
                BatteryLevel = s.BatteryLevel > 0 ? s.BatteryLevel : _batteryPercent,
                CpuTrend = 0, RamTrend = 0, TempTrend = 0
            };
        }

        public void RecordActionOutcome(string action, double outcomeScore)
        {
            RecordActionOutcome(action, outcomeScore, null);
        }

        public void RecordActionOutcome(string action, double outcomeScore, MLFeatureVector preFeatures)
        {
            // Single Source of Truth for the real-outcome counter: direct
            // callers (real log importer, manual recordings) hit
            // RecordRealOutcomeMetric exactly once, here. The finalize path
            // (FinalizeOutcomeRecord / RecordImmediateOutcome) must use
            // RecordActionOutcomeSample instead, otherwise every outcome is
            // counted twice and the success-rate / degrade statistics inflate.
            RecordRealOutcomeMetric(Math.Max(0, Math.Min(1, outcomeScore)));
            RecordActionOutcomeSample(action, outcomeScore, preFeatures);
        }

        // Sample-only recorder: stores the learning evidence (Q-table sample,
        // replay buffer, training trigger) WITHOUT touching the
        // real-outcome metric counter. Used exclusively by the outcome
        // finalize pipeline, which is the single place that measures each
        // outcome and increments RecordRealOutcomeMetric exactly once.
        internal void RecordActionOutcomeSample(string action, double outcomeScore, MLFeatureVector preFeatures)
        {
            // preFeatures is the state captured right BEFORE this action executed
            // (frozen in StartActionEvaluation), so a burst of actions (e.g. the
            // full-optimization sweep) no longer pollutes each other's samples.
            MLFeatureVector fv = preFeatures ?? _preAF ?? BuildFV();
            double[] rawArr = fv != null ? fv.ToArray() : null;
            if (rawArr != null)
            {
                if (_fn != null) _fn.Observe(rawArr);
                if (_driftDetector != null) _driftDetector.AddObservation(rawArr);
            }
            double clampedScore = Math.Max(0, Math.Min(1, outcomeScore));
            var s = new MLTrainingSample { Features = fv, ActionTaken = action, OutcomeScore = clampedScore, Timestamp = DateTime.Now };
            bool due = false;
            lock (_mlLock)
            {
                _mlTB.Add(s);
                _mlRP.Add(s, Math.Abs(clampedScore - 0.5) * 2.0 + 0.1);

                // H2: If this action succeeded strongly (>= 0.72), record calibrated negative counter-examples
                // for mutually exclusive conflicting actions so ensembles learn negative boundaries as well.
                if (clampedScore >= 0.72 && ACTION_CONFLICTS != null)
                {
                    string[] conflicts;
                    if (ACTION_CONFLICTS.TryGetValue(action, out conflicts) && conflicts != null)
                    {
                        for (int ci = 0; ci < conflicts.Length && ci < 2; ci++)
                        {
                            var negSample = new MLTrainingSample
                            {
                                Features = fv,
                                ActionTaken = conflicts[ci],
                                OutcomeScore = 0.12,
                                Timestamp = DateTime.Now,
                                ImportanceWeight = 0.5
                            };
                            _mlTB.Add(negSample);
                            _mlRP.Add(negSample, 0.35);
                        }
                    }
                }

                due = (DateTime.Now - _lastML).TotalSeconds >= ML_INT;
            }
            if (due)
            {
                TriggerBackgroundTraining();
            }
        }

        internal void TriggerBackgroundTraining()
        {
            if (IsGamingActive())
            {
                _trainingDeferred = true;
                AddLog("TRAIN", "Deferred", "ML background training deferred during active game");
                return;
            }

            if (DisableRealExecution)
            {
                try { TrainNN(); }
                catch (Exception ex) { LogError("ML.TrainSync", ex); }
                return;
            }

            // Deduplication guard: strictly at most 1 background training pass in-flight
            if (System.Threading.Interlocked.CompareExchange(ref _isTrainingInProgress, 1, 0) != 0)
            {
                return;
            }

            try
            {
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        TrainNN();
                    }
                    catch (Exception ex)
                    {
                        LogError("ML.TrainBG", ex);
                    }
                    finally
                    {
                        System.Threading.Interlocked.Exchange(ref _isTrainingInProgress, 0);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Threading.Interlocked.Exchange(ref _isTrainingInProgress, 0);
                LogError("ML.TrainBG.Queue", ex);
            }
        }

        private bool IsGamingActive()
        {
            try
            {
                if (IsGameRunning()) return true;
                if (_currentState != null && _currentState.IsGame) return true;
                if (!string.IsNullOrEmpty(_activeGameName)) return true;
            }
            catch { }
            return false;
        }

        public string GetMLRecommendation()
        {
            lock (_mlLock)
            {
                if (_mlEns.Count == 0 || _mlTB.Count < ML_MIN) return null;
                double[] input = _fn.NormalizeReadOnly(BuildFV().ToArray());
                string best = null; double bestS = double.MinValue;
                foreach (string a in ML_AC) { MLEnsemble e; if (!_mlEns.TryGetValue(a, out e) || e.TS < 3) continue; double s = e.Predict(input); if (s > bestS) { bestS = s; best = a; } }
                return best;
            }
        }

        public double GetMLActionScore(string action)
        {
            lock (_mlLock)
            {
                MLEnsemble e; if (!_mlEns.TryGetValue(action, out e) || e.TS < 3) return 0.5;
                return e.Predict(_fn.NormalizeReadOnly(BuildFV().ToArray()));
            }
        }

        public double GetMLActionConfidence(string action)
        {
            lock (_mlLock)
            {
                MLEnsemble e; if (!_mlEns.TryGetValue(action, out e) || e.TS < 3) return 0.5;
                return e.GetConf(_fn.NormalizeReadOnly(BuildFV().ToArray()));
            }
        }

        // No-arg convenience overload: clones the live state and defers to the
        // state-explicit overload below. The clone avoids holding _stateLock
        // across the ML inference and matches exactly what the live runtime
        // path (AnalyzeAndDecide) sees.
        public AgentDecision GetMLDecision()
        {
            if (_cachedMlDecision != null && _cachedMlDecisionTick == _tickCount)
            {
                return _cachedMlDecision.Clone();
            }
            SystemState s;
            lock (_stateLock) { s = _currentState != null ? _currentState.Clone() : null; }
            if (s == null) s = new SystemState { CpuUsage = 50, RamUsage = 70, GpuUsage = 50, CpuTemp = 50, Fps = 60, IsGame = true, ActiveProcess = "game.exe" };
            var decision = GetMLDecision(s);
            if (decision != null)
            {
                _cachedMlDecision = decision.Clone();
                _cachedMlDecisionTick = _tickCount;
            }
            return decision;
        }

        // State-explicit ML decision: the ensemble is scored on features built
        // from the passed snapshot, never on the live _currentState global.
        // This is what the simulation/test harness (GetOptimizationDecisionForState)
        // must use, so the ML half of the pipeline sees the same state as the
        // rule half - blind spot closed. The lock ordering here is
        // _mlLock only; callers holding _stateLock pass a clone, so no
        // deadlock on the non-reentrant state lock.
        internal static bool IsActionApplicableToState(string a, SystemState s)
        {
            if (s == null) return true;
            switch (a)
            {
                case "BoostGamePriority":
                case "SetGameAffinity":
                case "ReduceMicroStutter":
                    return s.IsGame && s.CpuTemp < 95 && s.RamUsage < 95;

                case "StabilizeFrames":
                    return (s.IsGame && (s.Fps < 60 || s.FpsStability < 80) && s.CpuTemp < 95) || (s.Fps > 0 && s.FpsStability < 60 && s.CpuTemp < 95);

                case "EmergencyThrottle":
                    return s.IsGame ? s.CpuTemp >= 95 : s.CpuTemp >= 88;
                case "ReduceThermal":
                    return s.IsGame ? (s.CpuTemp >= 90 && s.Fps >= 60) : s.CpuTemp >= 78;

                case "AggressiveClean":
                    return !s.IsGame && s.RamUsage >= 80;
                case "PurgeStandbyList":
                    return !s.IsGame && s.RamUsage >= 70;
                case "PreemptiveClean":
                    return !s.IsGame && s.RamUsage >= 72;
                case "LightClean":
                    return !s.IsGame && s.RamUsage >= 65;

                case "LimitBackground":
                    return s.CpuUsage >= 60 || (s.CpuUsage >= 50 && s.RamUsage >= 60);

                case "BatterySaver":
                    return (!s.IsCharging) && s.BatteryLevel <= 35;
                case "BalancedPerformance":
                    return (!s.IsCharging) && s.BatteryLevel <= 60;

                case "SetLowIoPriority":
                    return s.DiskUsage >= 60 && s.RamUsage < 85;

                case "UnparkAllCores":
                    return (s.CpuUsage >= 40 || s.IsGame) && s.CpuTemp < 95;

                case "OptimizeDesktopSmoothness":
                    return !s.IsGame && s.CpuUsage >= 55 && s.CpuUsage < 90 && s.CpuTemp < 88;

                case "TrimInactiveWorkingSets":
                    return !s.IsGame && s.RamUsage >= 50;

                case "OptimizeGpuPerformance":
                    return (s.IsGame || s.GpuUsage >= 60) && s.GpuTemp < 92;

                case "ShedBackgroundGpuMemory":
                    return (s.IsGame && (s.GpuUsage >= 60 || s.RamUsage >= 70)) || (s.GpuUsage >= 75 && s.RamUsage >= 75);

                case "PurgeGpuShaderCache":
                    return s.IsGame && (s.Fps01Low < 35 || s.FpsStability < 75);

                default:
                    return true;
            }
        }

        public AgentDecision GetMLDecision(SystemState state)
        {
            if (state == null) return null;
            if (_cachedMlDecision != null && _cachedMlDecisionTick == _tickCount && state == _currentState)
            {
                return _cachedMlDecision.Clone();
            }

            // Anomaly Rejection Guard: reject physically impossible or statistical outliers (> 4.5 sigma)
            // instead of clamping to 3 sigma and gambling with system stability.
            double[] rawFv = BuildFV(state).ToArray();
            string faultReason;
            if (_fn != null && _fn.IsAnomaly(rawFv, out faultReason))
            {
                var refusedDecision = new AgentDecision
                {
                    Action = "Abstain",
                    Category = DecisionCategory.BackgroundControl,
                    Description = "ML Ensemble: Sensor Anomaly Guard Active",
                    Reason = "REFUSED: Sensor fault detected (" + faultReason + ") - Falling back to safe baseline",
                    Confidence = 0.0,
                    Safety = DecisionSafety.Safe,
                    Impact = 0
                };
                AddLog("ML", "SENSOR_FAULT_REFUSAL", faultReason);
                return refusedDecision;
            }

            lock (_mlLock)
            {
                if (_mlEns.Count == 0 || _mlTB.Count < ML_MIN) return null;
                double[] input = _fn.NormalizeReadOnly(rawFv);
                string best = null;
                double bestScore = double.MinValue;
                foreach (string a in ML_AC)
                {
                    if (!IsActionApplicableToState(a, state)) continue;
                    MLEnsemble e;
                    if (!_mlEns.TryGetValue(a, out e) || e.TS < 3) continue;
                    double sc = e.Predict(input);
                    if (sc > bestScore) { bestScore = sc; best = a; }
                }
                if (best == null) return null;
                MLEnsemble be;
                if (!_mlEns.TryGetValue(best, out be)) return null;
                double conf = be.GetConf(input);
                conf = GetCalibratedConfidence(best, conf);
                if (bestScore < 0.45 - conf * 0.15) return null;
                var d = new AgentDecision();
                d.Action = best; d.Category = MLActionToCategory(best);
                d.Description = "ML Ensemble: " + best;
                d.Reason = string.Format("Ensemble confidence: {0:F0}%, consensus: {1:F0}%", conf * 100, bestScore * 100);
                d.Confidence = Math.Max(0.3, Math.Min(0.95, conf)); d.Safety = DecisionSafety.Safe;
                d.Impact = bestScore > 0.8 ? 7 : bestScore > 0.6 ? 5 : 3;
                if (state == _currentState)
                {
                    _cachedMlDecision = d.Clone();
                    _cachedMlDecisionTick = _tickCount;
                }
                return d;
            }
        }

        private DecisionCategory MLActionToCategory(string a)
        {
            switch (a) {
                case "AggressiveClean": case "LightClean": case "PreemptiveClean": case "TrimInactiveWorkingSets": case "ShedBackgroundGpuMemory": return DecisionCategory.MemoryClean;
                case "PurgeStandbyList": return DecisionCategory.PurgeStandby;
                case "LimitBackground": return DecisionCategory.BackgroundControl;
                case "BoostGamePriority": case "OptimizeGpuPerformance": case "PurgeGpuShaderCache": return DecisionCategory.GameBoost;
                case "OptimizeDesktopSmoothness": return DecisionCategory.ProcessPriority;
                case "EmergencyThrottle": case "ReduceThermal": return DecisionCategory.ThermalProtect;
                case "StabilizeFrames": case "ReduceMicroStutter": return DecisionCategory.BackgroundControl;
                case "BatterySaver": case "BalancedPerformance": return DecisionCategory.PowerPlan;
                case "SetGameAffinity": case "UnparkAllCores": return DecisionCategory.CpuAffinity;
                case "SetLowIoPriority": return DecisionCategory.IoPriority;
                default: return DecisionCategory.BackgroundControl;
            }
        }

        private void TrainNN()
        {
            lock (_mlLock)
            {
                if (IsGamingActive())
                {
                    _trainingDeferred = true;
                    return;
                }
                if (!DisableRealExecution && (DateTime.Now - _lastML).TotalSeconds < ML_INT) return;
                _lastML = DateTime.Now;
                // H1 Fix: Partition _mlTB into strictly disjoint Train (70%) and Validation (30%) sets
                // BEFORE mixing in any PrioritizedReplayBuffer samples, and exclude any validation SampleId
                // from the replay draw so TrainIds intersect ValIds is provably empty.
                var tbByAction = new Dictionary<string, List<MLTrainingSample>>();
                var seenTbIds = new HashSet<long>();
                foreach (var s in _mlTB)
                {
                    if (s == null || string.IsNullOrEmpty(s.ActionTaken) || s.Features == null) continue;
                    if (!seenTbIds.Add(s.SampleId)) continue;
                    List<MLTrainingSample> list;
                    if (!tbByAction.TryGetValue(s.ActionTaken, out list))
                    {
                        list = new List<MLTrainingSample>();
                        tbByAction[s.ActionTaken] = list;
                    }
                    list.Add(s);
                }

                var replayDraw = _mlRP.Sample(ML_BS);
                var replayByAction = new Dictionary<string, List<MLTrainingSample>>();
                foreach (var rs in replayDraw)
                {
                    if (rs == null || string.IsNullOrEmpty(rs.ActionTaken) || rs.Features == null) continue;
                    List<MLTrainingSample> rList;
                    if (!replayByAction.TryGetValue(rs.ActionTaken, out rList))
                    {
                        rList = new List<MLTrainingSample>();
                        replayByAction[rs.ActionTaken] = rList;
                    }
                    rList.Add(rs);
                }

                foreach (var kv in tbByAction)
                {
                    if (kv.Value.Count < 4) continue;
                    MLEnsemble e; if (!_mlEns.TryGetValue(kv.Key, out e)) { e = new MLEnsemble(kv.Key); _mlEns[kv.Key] = e; }
                    int n = kv.Value.Count;
                    if (!_aLR.ContainsKey(kv.Key)) _aLR[kv.Key] = 0.12;
                    if (!_aTC.ContainsKey(kv.Key)) _aTC[kv.Key] = 0;
                    int tc = _aTC[kv.Key];
                    double lr = _aLR[kv.Key];
                    double lrD = Math.Cos(Math.PI * Math.Min(1.0, tc / 2000.0)) * 0.5 + 0.5;
                    lr = lr * (0.1 + 0.9 * lrD);

                    int trainBaseCount = (int)Math.Max(1, Math.Round(n * 0.7));
                    int valCount = n - trainBaseCount;
                    if (valCount < 1) { valCount = 1; trainBaseCount = Math.Max(1, n - 1); }

                    var trainPool = new List<MLTrainingSample>(trainBaseCount + 16);
                    var valSet = new List<MLTrainingSample>(valCount);
                    var valSampleIds = new HashSet<long>();

                    for (int i = 0; i < trainBaseCount; i++) trainPool.Add(kv.Value[i]);
                    for (int i = trainBaseCount; i < n; i++)
                    {
                        valSet.Add(kv.Value[i]);
                        valSampleIds.Add(kv.Value[i].SampleId);
                    }

                    // Only add replay samples that do NOT belong to the held-out validation set
                    List<MLTrainingSample> actionReplays;
                    if (replayByAction.TryGetValue(kv.Key, out actionReplays))
                    {
                        for (int ri = 0; ri < actionReplays.Count; ri++)
                        {
                            var rs = actionReplays[ri];
                            if (!valSampleIds.Contains(rs.SampleId))
                            {
                                trainPool.Add(rs);
                            }
                        }
                    }

                    int trainCount = trainPool.Count;
                    int bs = Math.Min(48, trainCount);
                    double tl = 0;
                    for (int ep = 0; ep < ML_EP; ep++)
                    {
                        for (int i = 0; i < bs; i++)
                        {
                            int idx = _mlRnd.Next(trainCount);
                            var s = trainPool[idx];
                            double[] inp = _fn.NormalizeReadOnly(s.Features.ToArray());
                            e.Train(inp, s.OutcomeScore, lr, s.ImportanceWeight);
                            tl += Math.Abs(e.Predict(inp) - s.OutcomeScore);
                        }
                    }
                    _aTC[kv.Key] = tc + 1;
                    double al = tl / (ML_EP * bs);
                    if (al > 0.3) _aLR[kv.Key] = Math.Min(0.25, lr * 1.05);
                    else if (al < 0.1) _aLR[kv.Key] = Math.Max(0.01, lr * 0.95);
                    // Held-out validation accuracy on strictly disjoint valSet using NormalizeReadOnly
                    double valErr = 0;
                    for (int i = 0; i < valSet.Count; i++)
                    {
                        var s = valSet[i];
                        double[] inp = _fn.NormalizeReadOnly(s.Features.ToArray());
                        valErr += Math.Abs(e.Predict(inp) - s.OutcomeScore);
                    }
                    double acc = 1.0 - Math.Min(1.0, valErr / Math.Max(1, valCount));
                    for (int i = 0; i < 3; i++) e.Nets[i].Acc = acc;
                    // Training metrics accumulation
                    _trainCallCount++;
                    _trainEpochsTotal += ML_EP;
                    _trainBatchesTotal += bs;
                    _trainLossLast = al;
                    _trainAccLast = acc;
                    if (acc > _trainAccBest) _trainAccBest = acc;
                }
                if (_trainCallCount > 0 && _trainCallCount % 10 == 0)
                {
                    WriteTrainingMetrics();
                    string driftSummary;
                    if (_driftDetector.CheckDrift(out driftSummary))
                    {
                        AddLog("ML", "DRIFT_ALERT", driftSummary);
                    }
                }
                if (_tickCount % 600 == 0) { SaveMLModel(); SaveMetaProfiles(); }
            }
        }

        private void WriteTrainingMetrics()
        {
            try
            {
                double avgAcc = _trainCallCount > 0 ? (_trainAccLast + _trainAccBest) * 0.5 : 0;
                string line = string.Format("call={0} epochs={1} batches={2} loss={3:F4} accLast={4:F4} accBest={5:F4}",
                    _trainCallCount, _trainEpochsTotal, _trainBatchesTotal, _trainLossLast, _trainAccLast, _trainAccBest);
                AddLog("ML", "TRAIN_METRICS", line);
                AtomicWrite(Path.Combine(_dataDir, "ml_metrics.dat"), line + Environment.NewLine);
            }
            catch (Exception ex) { LogError("ML.TrainMetrics", ex); }
        }

        public string GetTrainingMetrics()
        {
            lock (_mlLock)
            {
                if (_trainCallCount == 0) return "No training batches yet";
                return string.Format("Training batches: {0} | Epochs: {1} | Last loss: {2:F4} | Accuracy (last): {3:F1}% | Accuracy (best): {4:F1}%",
                    _trainBatchesTotal, _trainEpochsTotal, _trainLossLast, _trainAccLast * 100, _trainAccBest * 100);
            }
        }

        public string TrainingMetricsSummary { get { return GetTrainingMetrics(); } }
        public double GetTrainingAccuracy() { lock (_mlLock) return _trainAccLast; }
        public double GetTrainingLoss() { lock (_mlLock) return _trainLossLast; }

        public string GetMLInsight()
        {
            lock (_mlLock)
            {
                if (_mlEns.Count == 0) return "Ensemble not trained yet. Needs " + ML_MIN + " samples.";
                var sb = new StringBuilder();
                sb.AppendLine(string.Format("Ensembles: {0} | Samples: {1} | Replay: {2}", _mlEns.Count, _mlTB.Count, _mlRP.Count));
                foreach (var kv in _mlEns) sb.AppendLine(string.Format("  {0}: acc={1:F1}% samples={2} lr={3:F4}", kv.Key, kv.Value.Acc * 100, kv.Value.TS, _aLR.ContainsKey(kv.Key) ? _aLR[kv.Key] : 0.12));
                string r = GetMLRecommendation();
                if (r != null) sb.AppendLine(string.Format("Recommended: {0} (score={1:F3}, conf={2:F1}%)", r, GetMLActionScore(r), GetMLActionConfidence(r) * 100));
                return sb.ToString();
            }
        }

        private void SaveMLModel()
        {
            try
            {
                lock (_mlLock)
                {
                    string path = Path.Combine(_dataDir, "ml_nn_v2.dat");
                    var sb = new StringBuilder();
                    sb.AppendLine("V4"); sb.AppendLine(_mlEns.Count.ToString());
                    foreach (var kv in _mlEns) { var e = kv.Value; sb.AppendLine(e.Action); for (int ni = 0; ni < 3; ni++) { var n = e.Nets[ni]; sb.AppendLine(n.TS.ToString()); sb.AppendLine(n.LU.Ticks.ToString()); WA(sb, n.W1); WA(sb, n.B1); WA(sb, n.W2); WA(sb, n.B2); WA(sb, n.W3); sb.AppendLine(n.B3.ToString("F8", CultureInfo.InvariantCulture)); WA(sb, n.BN1G); WA(sb, n.BN1B); WA(sb, n.BN2G); WA(sb, n.BN2B); WA(sb, n.BN1RM); WA(sb, n.BN1RV); WA(sb, n.BN2RM); WA(sb, n.BN2RV); WA(sb, n.MW1); WA(sb, n.MB1); WA(sb, n.MW2); WA(sb, n.MB2); WA(sb, n.MW3); sb.AppendLine(n.MB3.ToString("F8", CultureInfo.InvariantCulture)); WA(sb, n.VW1); WA(sb, n.VB1); WA(sb, n.VW2); WA(sb, n.VB2); WA(sb, n.VW3); sb.AppendLine(n.VB3.ToString("F8", CultureInfo.InvariantCulture)); WA(sb, n.MBN1G); WA(sb, n.VBN1G); WA(sb, n.MBN1B); WA(sb, n.VBN1B); WA(sb, n.MBN2G); WA(sb, n.VBN2G); WA(sb, n.MBN2B); WA(sb, n.VBN2B); } }
                    sb.AppendLine("BUF:" + _mlTB.Count);
                    int ns = Math.Min(200, _mlTB.Count), st = Math.Max(0, _mlTB.Count - ns);
                    for (int i = st; i < _mlTB.Count; i++) { var s = _mlTB[i]; double[] fv = s.Features.ToArray(); sb.Append("S:" + s.ActionTaken + ":" + s.OutcomeScore.ToString("F4")); for (int f = 0; f < fv.Length; f++) sb.Append(":" + fv[f].ToString("F2")); sb.AppendLine(); }
                    // Persist the feature normalizer so predictions stay consistent
                    // across restarts (same input -> same normalized features).
                    try
                    {
                        var im = string.Join(",", _fn.GetMeans().Select(x => x.ToString("F4", CultureInfo.InvariantCulture)));
                        var isd = string.Join(",", _fn.GetStds().Select(x => x.ToString("F4", CultureInfo.InvariantCulture)));
                        var ic = string.Join(",", _fn.GetCounts());
                        sb.AppendLine("NORM:" + im + "|" + isd + "|" + ic);
                    }
                    catch (Exception ex) { LogError("ML.Norm.Save", ex); }
                    AtomicWrite(path, sb.ToString());
                    WriteChecksum(path);
                }
            }
            catch (Exception ex) { LogError("ML.Save", ex); }
        }

        private void WA(StringBuilder sb, double[] arr) { for (int i = 0; i < arr.Length; i++) sb.AppendLine(arr[i].ToString("F8", CultureInfo.InvariantCulture)); }

        private void WriteChecksum(string path)
        {
            try
            {
                using (var fs = File.OpenRead(path))
                using (var sha = new System.Security.Cryptography.SHA256Managed())
                {
                    byte[] hash = sha.ComputeHash(fs);
                    var sb = new StringBuilder();
                    for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("X2"));
                    File.WriteAllText(path + ".sha", sb.ToString());
                }
            }
            catch (Exception ex) { LogError("ML.Checksum", ex); }
        }

        private bool VerifyChecksum(string path)
        {
            try
            {
                string shaPath = path + ".sha";
                if (!File.Exists(shaPath)) return true; // legacy file: accept without checksum
                string expected = File.ReadAllText(shaPath).Trim();
                using (var fs = File.OpenRead(path))
                using (var sha = new System.Security.Cryptography.SHA256Managed())
                {
                    byte[] hash = sha.ComputeHash(fs);
                    var sb = new StringBuilder();
                    for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("X2"));
                    return sb.ToString() == expected;
                }
            }
            catch { return false; }
        }

        private void LoadMLModel()
        {
            lock (_mlLock)
            {
                string path = Path.Combine(_dataDir, "ml_nn_v2.dat");
                if (!File.Exists(path)) return;
                bool ok = false;
                try { ok = LoadMLModelFile(path); }
                catch (Exception ex) { LogError("ML.Load", ex); ok = false; }
                if (!ok)
                {
                    // Corrupt or unreadable model: fall back to the last known-good backup.
                    string bak = Path.Combine(_dataDir, "ml_nn_v2.dat.bak1");
                    if (File.Exists(bak))
                    {
                        try
                        {
                            File.Copy(bak, path, true);
                            WriteChecksum(path);
                            bool restored = LoadMLModelFile(path);
                            if (restored) AddLog("WARN", "WinOptAI", "Restored ML model from backup after corruption");
                            else AddLog("WARN", "WinOptAI", "Backup model also unreadable - starting fresh");
                        }
                        catch (Exception ex) { LogError("ML.LoadBackup", ex); }
                    }
                    else
                    {
                        AddLog("WARN", "WinOptAI", "ML model file corrupt and no backup found - starting fresh");
                    }
                }
            }
        }

        private bool LoadMLModelFile(string path)
        {
            if (!VerifyChecksum(path)) return false;
            string[] lines = File.ReadAllLines(path); int idx = 0;
            var tempEns = new Dictionary<string, MLEnsemble>();
            var tempTB = new List<MLTrainingSample>();
            string ver = lines[idx++]; bool hasV = (ver == "V3" || ver == "V4"); bool v4 = (ver == "V4");
            if (ver != "V2" && ver != "V3" && ver != "V4") return false;
            int mc = int.Parse(lines[idx++], CultureInfo.InvariantCulture);
            for (int m = 0; m < mc && idx < lines.Length; m++)
            {
                string action = lines[idx++];
                var ens = new MLEnsemble(action);
                for (int ni = 0; ni < 3 && idx < lines.Length; ni++)
                {
                    var n = ens.Nets[ni]; n.TS = int.Parse(lines[idx++], CultureInfo.InvariantCulture); n.LU = new DateTime(long.Parse(lines[idx++], CultureInfo.InvariantCulture));
                    RA(n.W1, lines, ref idx); RA(n.B1, lines, ref idx); RA(n.W2, lines, ref idx); RA(n.B2, lines, ref idx); RA(n.W3, lines, ref idx); if (idx < lines.Length) n.B3 = double.Parse(lines[idx++], CultureInfo.InvariantCulture);
                    RA(n.BN1G, lines, ref idx); RA(n.BN1B, lines, ref idx); RA(n.BN2G, lines, ref idx); RA(n.BN2B, lines, ref idx);
                    RA(n.BN1RM, lines, ref idx); RA(n.BN1RV, lines, ref idx); RA(n.BN2RM, lines, ref idx); RA(n.BN2RV, lines, ref idx);
                    RA(n.MW1, lines, ref idx); RA(n.MB1, lines, ref idx); RA(n.MW2, lines, ref idx); RA(n.MB2, lines, ref idx); RA(n.MW3, lines, ref idx); if (idx < lines.Length) n.MB3 = double.Parse(lines[idx++], CultureInfo.InvariantCulture);
                    if (hasV)
                    {
                        RA(n.VW1, lines, ref idx); RA(n.VB1, lines, ref idx); RA(n.VW2, lines, ref idx); RA(n.VB2, lines, ref idx); RA(n.VW3, lines, ref idx); if (idx < lines.Length) n.VB3 = double.Parse(lines[idx++], CultureInfo.InvariantCulture);
                    }
                    if (v4)
                    {
                        RA(n.MBN1G, lines, ref idx); RA(n.VBN1G, lines, ref idx); RA(n.MBN1B, lines, ref idx); RA(n.VBN1B, lines, ref idx);
                        RA(n.MBN2G, lines, ref idx); RA(n.VBN2G, lines, ref idx); RA(n.MBN2B, lines, ref idx); RA(n.VBN2B, lines, ref idx);
                    }
                }
                tempEns[action] = ens;
            }
            while (idx < lines.Length)
            {
                string line = lines[idx++];
                if (line.StartsWith("NORM:"))
                {
                    try
                    {
                        string[] parts = line.Substring(5).Split('|');
                        if (parts.Length == 3)
                        {
                            double[] m = parts[0].Split(',').Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();
                            double[] s = parts[1].Split(',').Select(x => double.Parse(x, CultureInfo.InvariantCulture)).ToArray();
                            int[] c = parts[2].Split(',').Select(x => int.Parse(x, CultureInfo.InvariantCulture)).ToArray();
                            if (_fn != null) _fn.SetStats(m, s, c);
                        }
                    }
                    catch (Exception ex) { LogError("ML.Norm.Load", ex); }
                }
                else if (line.StartsWith("S:"))
                {
                    string[] p = line.Substring(2).Split(':'); if (p.Length >= 3)
                    {
                        var fv = new MLFeatureVector(); string a = p[0]; double sc = double.Parse(p[1], CultureInfo.InvariantCulture);
                        fv.CpuUsage = p.Length > 2 ? double.Parse(p[2], CultureInfo.InvariantCulture) : 50; fv.RamUsage = p.Length > 3 ? double.Parse(p[3], CultureInfo.InvariantCulture) : 50;
                        fv.GpuUsage = p.Length > 4 ? double.Parse(p[4], CultureInfo.InvariantCulture) : 50; fv.DiskUsage = p.Length > 5 ? double.Parse(p[5], CultureInfo.InvariantCulture) : 50;
                        fv.CpuTemp = p.Length > 6 ? double.Parse(p[6], CultureInfo.InvariantCulture) : 50; fv.GpuTemp = p.Length > 7 ? double.Parse(p[7], CultureInfo.InvariantCulture) : 40;
                        fv.Fps = p.Length > 8 ? double.Parse(p[8], CultureInfo.InvariantCulture) : 60; fv.Fps01Low = p.Length > 9 ? double.Parse(p[9], CultureInfo.InvariantCulture) : 30;
                        fv.FpsStability = p.Length > 10 ? double.Parse(p[10], CultureInfo.InvariantCulture) : 80; fv.ProcessCount = p.Length > 11 ? (int)double.Parse(p[11], CultureInfo.InvariantCulture) : 100;
                        fv.HourOfDay = p.Length > 12 ? double.Parse(p[12], CultureInfo.InvariantCulture) : 12; fv.MinuteOfHour = p.Length > 13 ? double.Parse(p[13], CultureInfo.InvariantCulture) : 30;
                        fv.IsGaming = p.Length > 14 ? double.Parse(p[14], CultureInfo.InvariantCulture) : 0; fv.IsOnBattery = p.Length > 15 ? double.Parse(p[15], CultureInfo.InvariantCulture) : 0;
                        fv.BatteryLevel = p.Length > 16 ? double.Parse(p[16], CultureInfo.InvariantCulture) : 100; fv.CpuTrend = p.Length > 17 ? double.Parse(p[17], CultureInfo.InvariantCulture) : 0;
                        fv.RamTrend = p.Length > 18 ? double.Parse(p[18], CultureInfo.InvariantCulture) : 0; fv.TempTrend = p.Length > 19 ? double.Parse(p[19], CultureInfo.InvariantCulture) : 0;
                        tempTB.Add(new MLTrainingSample { Features = fv, ActionTaken = a, OutcomeScore = sc, Timestamp = DateTime.Now });
                    }
                }
            }

            // Commit loaded state atomically only after successful parse
            _mlEns.Clear();
            foreach (var kv in tempEns) _mlEns[kv.Key] = kv.Value;
            _mlTB.Clear();
            foreach (var s in tempTB) _mlTB.Add(s);
            return true;
        }

        private void RA(double[] arr, string[] lines, ref int idx) { for (int i = 0; i < arr.Length && idx < lines.Length; i++) arr[i] = double.Parse(lines[idx++], CultureInfo.InvariantCulture); }

        // ===================== Windows Optimization AI (in-app) =====================
        // The external SmolLM2 360M local model has been removed. In its place the
        // in-process neural ensemble (MLNeuralNetwork) is the local AI brain,
        // specialized for Windows optimization: always available, never blocks,
        // needs no Python, and keeps learning from real optimization outcomes.

        public void InitializeWindowsOptimizationAI()
        {
            if (_woa != null) return; // idempotent: never re-create or re-log
            try
            {
                _woa = new WindowsOptimizationAI();
                CWLog.Info("WinOptAI", "Windows Optimization AI ready (in-app neural ensemble, no external model)");
                AddLog("INFO", "WinOptAI", "Windows Optimization AI ready (in-app neural ensemble)");
            }
            catch (Exception ex)
            {
                CWLog.Error("InitializeWindowsOptimizationAI", ex);
                LogError("InitializeWindowsOptimizationAI", ex);
            }
        }

        public void StartWindowsOptimizationAI()
        {
            if (_woa != null)
            {
                _woa.IntentionallyStopped = false;
                CWLog.Info("WinOptAI", "Windows Optimization AI activated");
                AddLog("INFO", "WinOptAI", "Windows Optimization AI activated");
            }
        }

        public void StopWindowsOptimizationAI()
        {
            if (_woa != null)
            {
                _woa.IntentionallyStopped = true;
                AddLog("INFO", "WinOptAI", "Windows Optimization AI stopped");
            }
        }

        public AgentDecision GetWindowsAIDecision(bool woaNeeded)
        {
            try
            {
                if (_woa == null) return null;
                if (!woaNeeded) return null;
                if (_tickCount % WOAI_INTERVAL_TICKS != 0) return null;
                if (_woa.IntentionallyStopped) return null;
                _woa.LastActivity = DateTime.Now;
                AgentDecision d = ComputeWindowsOptimizationDecision();
                if (d == null) return null;
                d.Confidence = GetCalibratedConfidence(d.Action, d.Confidence);
                _woa.TotalInferences++;
                _woaLastAction = d.Action;
                _woaLastConfidence = d.Confidence;
                _woaLastPrompt = BuildWoaPrompt(d);
                _woaLastRawResponse = BuildWoaResponse(d);
                return d;
            }
            catch (Exception ex)
            {
                CWLog.Error("WinOptAI.Gate", ex);
                return null;
            }
        }

        // The Windows Optimization AI brain: a rule expert specialized in Windows
        // optimization (always available - the strength of the old local model)
        // merged with the trained neural ensemble (our model - gets stronger over
        // time from real outcomes recorded via RecordActionOutcome).
        private AgentDecision ComputeWindowsOptimizationDecision()
        {
            lock (_stateLock)
            {
                if (_currentState == null) return null;
                AgentDecision rule = BuildWindowsOptimizationRuleDecision();
                // State-explicit: the ensemble is scored on the same live
                // state snapshot the rule engine just used (cloned so the
                // ML lock does not nest over the state lock).
                AgentDecision ml = GetMLDecision(_currentState.Clone());
                if (ml != null && ml.Action == "Abstain") ml = null;
                if (rule == null) return ml;
                if (ml == null) return rule;
                if (ml.Confidence > rule.Confidence)
                {
                    ml.Reason = rule.Reason + " | " + ml.Reason;
                    return ml;
                }
                return rule;
            }
        }

        private AgentDecision BuildWindowsOptimizationRuleDecision()
        {
            if (_currentState == null) return null;
            return BuildRuleDecisionFrom(_currentState, _smoothCpu, _smoothRam, _smoothTemp, _isOnBattery, _batteryPercent);
        }

        // Pure rule decision for an explicit state (used by the live agent and by
        // the test harness so decision quality is verifiable without side effects).
        private AgentDecision BuildRuleDecisionFrom(SystemState s, double cpu, double ram, double temp, bool onBattery, int batteryPercent)
        {
            double fps = s.Fps, fpsStab = s.FpsStability;
            bool isGame = s.IsGame;
            // FPS-based branches are only meaningful when the frame telemetry is
            // actually live (the app is rendering). Without it, a stale/captured
            // value must never be mistaken for the game's real FPS.
            bool fpsKnown = (_frameDataReliable || s != _currentState || isGame) && fps > 0;
            string action = null; string why = ""; double conf = 0;
            // Thermal first: protect the hardware from silicon damage (90C in gaming, 78C on desktop)
            if (temp >= (isGame ? 90 : 78)) { action = "EmergencyThrottle"; why = "CPU temp at " + temp.ToString("F0") + "C - thermal protection required"; conf = 0.95; }
            else if (!isGame && temp >= 68) { action = "ReduceThermal"; why = "CPU temp at " + temp.ToString("F0") + "C - reducing thermals"; conf = 0.85; }
            // Memory pressure (including game standby list pressure)
            else if (ram >= 88) { action = "AggressiveClean"; why = "RAM at " + ram.ToString("F0") + "% - aggressive clean needed"; conf = 0.88; }
            else if (ram >= 80 && fpsKnown && fps < 60) { action = "PurgeStandbyList"; why = "RAM at " + ram.ToString("F0") + "% with low FPS - purging standby list"; conf = 0.80; }
            else if (ram >= 78) { action = "PreemptiveClean"; why = "RAM at " + ram.ToString("F0") + "% - preemptive clean"; conf = 0.80; }
            else if (ram >= 70 && !isGame) { action = "LightClean"; why = "RAM at " + ram.ToString("F0") + "% - light clean"; conf = 0.72; }
            // Gaming: 60 FPS Floor is priority #1
            else if (isGame && fpsKnown && fps < 30) { action = "ReduceMicroStutter"; why = "Game FPS critically low (" + fps.ToString("F0") + " FPS) - micro-stutter elimination & core unparking"; conf = 0.95; }
            else if (isGame && fpsKnown && s.Fps01Low > 0 && s.Fps01Low < 25 && fpsStab < 75) { action = "PurgeGpuShaderCache"; why = "0.1% low FPS micro-stutter (" + s.Fps01Low.ToString("F0") + " FPS) - purging corrupt shader cache"; conf = 0.91; }
            else if (isGame && cpu >= 70 && cpu <= 77 && ram <= 70 && fpsKnown && fps < 50) { action = "UnparkAllCores"; why = "Game CPU scheduling pressure - unparking all CPU cores"; conf = 0.85; }
            else if (isGame && fpsKnown && fps < 45 && cpu > 77) { action = "BoostGamePriority"; why = "Game priority depressed by CPU load (" + fps.ToString("F0") + " FPS) - boosting priority"; conf = 0.90; }
            else if (isGame && fpsKnown && fps < 60) { action = "StabilizeFrames"; why = "Game below 60 FPS floor (" + fps.ToString("F0") + " FPS) - aggressive 60 FPS hold"; conf = 0.95; }
            else if (isGame && fpsKnown && fpsStab < 60) { action = "StabilizeFrames"; why = "FPS stability at " + fpsStab.ToString("F0") + "% - stabilizing frame times"; conf = 0.90; }
            else if (isGame && ram >= 72 && ram < 80 && s.GpuUsage >= 65) { action = "ShedBackgroundGpuMemory"; why = "Game VRAM contention with background apps - shedding background video memory"; conf = 0.91; }
            else if (isGame && s.GpuUsage >= 80 && fpsKnown && fps >= 60 && temp < 85) { action = "OptimizeGpuPerformance"; why = "Active 3D gaming load - enforcing High-Performance GPU routing, D3D GPU Priority=8 and HAGS"; conf = 0.94; }
            else if (isGame) { action = "BoostGamePriority"; why = "Active game session - elevating thread priority, locking 0.5ms timer and enforcing 60+ FPS"; conf = 0.92; }
            // Background load
            else if (cpu >= 75 && ram >= 65) { action = "LimitBackground"; why = "CPU " + cpu.ToString("F0") + "% / RAM " + ram.ToString("F0") + "% - limiting background load"; conf = 0.75; }
            // Storage saturation on Low-End / I/O heavy system
            else if (!isGame && s.DiskUsage >= 75 && ram < 78) { action = "SetLowIoPriority"; why = "Heavy background disk I/O saturation - demoting background I/O to low priority"; conf = 0.88; }
            // Battery
            else if (onBattery && batteryPercent <= 25) { action = "BatterySaver"; why = "Battery at " + batteryPercent + "% - switching to power saver"; conf = 0.80; }
            else if (onBattery && batteryPercent <= 60) { action = "BalancedPerformance"; why = "On battery at " + batteryPercent + "% - balanced power profile"; conf = 0.65; }
            // Desktop Smoothness: Active productivity workload below background limit threshold
            else if (!isGame && !onBattery && cpu >= 60 && cpu < 75 && ram < 65) { action = "OptimizeDesktopSmoothness"; why = "Active desktop multitasking - boosting foreground window priority & maintaining 1.0ms timer"; conf = 0.95; }
            // Inactive background apps trimming for low-to-mid memory systems
            else if (!isGame && !onBattery && ram >= 56 && ram < 70 && cpu < 60) { action = "TrimInactiveWorkingSets"; why = "Inactive background processes accumulating working sets - trimming to keep system lightweight"; conf = 0.90; }
            if (action == null) return null;
            var d = new AgentDecision();
            d.Action = action;
            d.Category = MLActionToCategory(action);
            d.Description = "Windows Optimization AI: " + action;
            d.Reason = "[WindowsOptRule] " + why + " | " + BuildFeatureInfluence(s, cpu, ram, temp, fps, fpsStab);
            d.Confidence = conf;
            d.Safety = DecisionSafety.Safe;
            d.Impact = conf > 0.8 ? 8 : 6;
            d.Source = "WindowsOptimizationAI";
            return d;
        }

        // Feature Importance: how much each metric contributed to the decision,
        // derived from each metric's distance from its healthy baseline.
        private string BuildFeatureInfluence(SystemState s, double cpu, double ram, double temp, double fps, double fpsStab)
        {
            string[] names = new[] { "Temp", "RAM", "CPU", "GPU", "FPS", "FPSStab" };
            double[] values = new double[6];
            values[0] = temp > 50 ? Math.Min(1, (temp - 50) / 45.0) : 0;
            values[1] = ram > 40 ? Math.Min(1, (ram - 40) / 60.0) : 0;
            values[2] = cpu > 40 ? Math.Min(1, (cpu - 40) / 60.0) : 0;
            values[3] = s.GpuUsage > 40 ? Math.Min(1, (s.GpuUsage - 40) / 60.0) : 0;
            values[4] = fps > 0 ? Math.Max(0, Math.Min(1, (60 - fps) / 60.0)) : 0;
            values[5] = fpsStab > 0 ? Math.Max(0, Math.Min(1, (60 - fpsStab) / 60.0)) : 0;
            double total = 0;
            for (int i = 0; i < 6; i++) total += values[i];
            if (total <= 0) return "no abnormal pressure";
            var sb = new StringBuilder();
            sb.Append("Feature influence: ");
            bool first = true;
            for (int i = 0; i < 6; i++)
            {
                if (values[i] <= 0) continue;
                if (!first) sb.Append(", ");
                first = false;
                sb.Append(string.Format("{0} {1:F0}%", names[i], values[i] / total * 100));
            }
            return sb.ToString();
        }

        public Dictionary<string, double> GetFeatureImportanceMap()
        {
            return GetFeatureImportanceMap(_currentState);
        }

        public Dictionary<string, double> GetFeatureImportanceMap(SystemState s)
        {
            var map = new Dictionary<string, double>();
            if (s == null) return map;
            double cpu = s.CpuUsage, ram = s.RamUsage, gpu = s.GpuUsage, temp = s.CpuTemp;
            double fps = s.Fps, stab = s.FpsStability;
            map["Temp"] = temp > 50 ? Math.Min(1, (temp - 50) / 45.0) : 0;
            map["RAM"] = ram > 40 ? Math.Min(1, (ram - 40) / 60.0) : 0;
            map["CPU"] = cpu > 40 ? Math.Min(1, (cpu - 40) / 60.0) : 0;
            map["GPU"] = gpu > 40 ? Math.Min(1, (gpu - 40) / 60.0) : 0;
            map["FPS"] = fps > 0 ? Math.Max(0, Math.Min(1, (60 - fps) / 60.0)) : 0;
            map["FPSStab"] = stab > 0 ? Math.Max(0, Math.Min(1, (60 - stab) / 60.0)) : 0;
            return map;
        }

        public string GetFeatureImportance()
        {
            if (_currentState == null) return "";
            return BuildFeatureInfluence(_currentState, _smoothCpu, _smoothRam, _smoothTemp, _currentState.Fps, _currentState.FpsStability);
        }

        public string GetFeatureImportance(SystemState s)
        {
            if (s == null) return "";
            return BuildFeatureInfluence(s, s.CpuUsage, s.RamUsage, s.CpuTemp, s.Fps, s.FpsStability);
        }

        // Test/analysis API: evaluate a decision for an explicit state without any
        // side effects (no execution, no system changes).
        public AgentDecision GetRuleDecisionForState(SystemState s) { return GetRuleDecisionForState(s, _isOnBattery, _batteryPercent); }
        public AgentDecision GetRuleDecisionForState(SystemState s, bool onBattery, int batteryPercent)
        {
            if (s == null) return null;
            lock (_stateLock)
            {
                return BuildRuleDecisionFrom(s, s.CpuUsage, s.RamUsage, s.CpuTemp, onBattery, batteryPercent);
            }
        }

        public AgentDecision GetOptimizationDecisionForState(SystemState s)
        {
            if (s == null) return null;
            lock (_stateLock)
            {
                bool onBatt = !s.IsCharging && s.BatteryLevel >= 0;
                int battPct = s.BatteryLevel >= 0 ? (int)s.BatteryLevel : 100;
                AgentDecision rule = BuildRuleDecisionFrom(s, s.CpuUsage, s.RamUsage, s.CpuTemp, onBatt, battPct);
                // State-explicit: pass the SAME synthetic state the rule engine
                // scored, so the ML half of the pipeline is deterministic for
                // the test harness (no _currentState global blind spot).
                AgentDecision ml = GetMLDecision(s.Clone());
                if (ml != null && ml.Action == "Abstain") ml = null;
                if (rule == null) return ml;
                if (ml == null) return rule;
                if (rule.Action == "EmergencyThrottle") return rule;
                if (ml.Confidence >= rule.Confidence - 0.10)
                {
                    ml.Reason = rule.Reason + " | " + ml.Reason;
                    return ml;
                }
                return rule;
            }
        }

        // Analysis throughput benchmark used by the stress tests and perf review.
        public AnalysisStressResult RunAnalysisStress(int iterations)
        {
            var res = new AnalysisStressResult();
            res.TotalAnalyses = iterations;
            res.StartRamMB = Environment.WorkingSet / 1048576.0;
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();
            double sum = 0; int produced = 0; int exceptions = 0;
            double maxMs = 0;
            double[] latencies = new double[iterations];
            lock (_stateLock)
            {
                if (_currentState == null || (!_currentState.IsGame && _currentState.CpuUsage <= 0))
                {
                    _currentState = new SystemState { CpuUsage = 85, RamUsage = 80, GpuUsage = 80, CpuTemp = 75, IsGame = true, ActiveProcess = "game.exe" };
                }
                for (int i = 0; i < iterations; i++)
                {
                    try
                    {
                        sw.Restart();
                        AgentDecision d = ComputeWindowsOptimizationDecision();
                        sw.Stop();
                        if (d != null) produced++;
                        double ms = sw.Elapsed.TotalMilliseconds;
                        latencies[i] = ms;
                        sum += ms;
                        if (ms > maxMs) maxMs = ms;
                    }
                    catch
                    {
                        exceptions++;
                        latencies[i] = 0;
                    }
                }
            }
            sw.Stop();
            res.DecisionsProduced = produced;
            res.TotalExceptions = exceptions;
            res.ElapsedSeconds = sw.Elapsed.TotalSeconds;
            res.AvgLatencyMs = iterations > 0 ? sum / iterations : 0;
            res.MaxLatencyMs = maxMs;

            if (iterations > 0)
            {
                double[] sorted = (double[])latencies.Clone();
                Array.Sort(sorted);
                res.P50LatencyMs = sorted[(int)(iterations * 0.50)];
                res.P95LatencyMs = sorted[Math.Min(iterations - 1, (int)(iterations * 0.95))];
                res.P99LatencyMs = sorted[Math.Min(iterations - 1, (int)(iterations * 0.99))];

                double avg = res.AvgLatencyMs;
                double sumSq = 0.0;
                for (int i = 0; i < iterations; i++)
                {
                    double diff = latencies[i] - avg;
                    sumSq += diff * diff;
                }
                res.JitterMs = Math.Sqrt(sumSq / iterations);
            }

            res.EstimatedAnalysesPerDay = res.ElapsedSeconds > 0 ? (long)(iterations / res.ElapsedSeconds * 86400.0) : 0;
            res.EndRamMB = Environment.WorkingSet / 1048576.0;
            return res;
        }

        public int GetModelEnsembleCount() { lock (_mlLock) return _mlEns.Count; }
        public int GetModelTrainingSampleCount() { lock (_mlLock) return _mlTB.Count; }
        public void SaveModelData() { SaveMLModel(); }
        public void LoadModelData() { LoadMLModel(); }

        private string BuildWoaPrompt(AgentDecision d)
        {
            if (_currentState == null) return "";
            return string.Format("state: cpu={0:F0}% ram={1:F0}% gpu={2:F0}% disk={3:F0}% temp={4:F0}C fps={5:F0} stab={6:F0}% game={7} battery={8}",
                _smoothCpu, _smoothRam, _smoothGpu, _currentState.DiskUsage, _smoothTemp,
                _currentState.Fps, _currentState.FpsStability, _currentState.IsGame,
                _isOnBattery ? _batteryPercent + "%" : "-");
        }

        private string BuildWoaResponse(AgentDecision d)
        {
            if (d == null) return "";
            return string.Format("recommend {0} (conf {1:F0}%) - {2}", d.Action, d.Confidence * 100, d.Reason);
        }

        public string GetWindowsAIStatus()
        {
            if (_woa == null) return "Windows Opt AI: Not initialized";
            if (!_woa.IsRunning) return "Windows Opt AI: Stopped";
            return string.Format("Windows Opt AI: Active | Inferences: {0} | Success: {1:F0}% | Last action: {2} (conf: {3:F0}%)",
                _woa.TotalInferences, _woa.SuccessRate * 100,
                _woaLastAction, _woaLastConfidence * 100);
        }
    }
}
