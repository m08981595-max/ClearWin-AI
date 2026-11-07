"""
ClearWin AI v2.7.7 — Reproducible Mathematical Reference Verification Suite (Python)
====================================================================================
IMPORTANT DISCLOSURE:
- This script executes an independent mathematical reference model in Python 3 that
  mirrors the exact equations of `src/SmartAgent.MLModel.cs` and `src/SmartAgent.RL2.cs`.
- It does NOT execute the compiled C# binary (`ClearWin.exe` / `ClearWinTests.exe`),
  because the current audit container is Linux (`x86_64 GNU/Linux`) without `.NET Framework 4.x`.
- The equivalent production C# unit tests (including `ComputeAnalyticalGradients` finite-difference
  checking and `RL2` isolation checks) are written in `src/UnitTests.cs`
  (`TestV277IndependentVerificationSuitePublic`) and registered in `src/TestEntry.cs`
  for execution on Windows via `powershell -ExecutionPolicy Bypass -File .\build-tests.ps1`.
"""

import glob
import math
import random
import subprocess

INPUT, H1, H2 = 18, 32, 16
DR = 0.2
ACAP = 4.0
GRAD_CLIP = 0.25
BNE = 1e-5


class ReferenceMLNeuralNetwork:
    def __init__(self, seed=42, use_exact_layernorm_grad=True):
        self.rnd = random.Random(seed)
        self.use_exact_layernorm_grad = use_exact_layernorm_grad
        s1, s2, s3 = math.sqrt(2.0 / INPUT), math.sqrt(2.0 / H1), math.sqrt(2.0 / H2)
        self.W1 = [(self.rnd.random() * 2 - 1) * s1 for _ in range(INPUT * H1)]
        self.B1 = [(self.rnd.random() * 2 - 1) * 0.05 for _ in range(H1)]
        self.W2 = [(self.rnd.random() * 2 - 1) * s2 for _ in range(H1 * H2)]
        self.B2 = [(self.rnd.random() * 2 - 1) * 0.05 for _ in range(H2)]
        self.W3 = [(self.rnd.random() * 2 - 1) * s3 for _ in range(H2)]
        self.B3 = 0.0
        self.BN1G, self.BN1B = [1.0] * H1, [0.0] * H1
        self.BN2G, self.BN2B = [1.0] * H2, [0.0] * H2

        self.MW1, self.VW1 = [0.0] * (INPUT * H1), [0.0] * (INPUT * H1)
        self.MB1, self.VB1 = [0.0] * H1, [0.0] * H1
        self.MW2, self.VW2 = [0.0] * (H1 * H2), [0.0] * (H1 * H2)
        self.MB2, self.VB2 = [0.0] * H2, [0.0] * H2
        self.MW3, self.VW3 = [0.0] * H2, [0.0] * H2
        self.MB3, self.VB3 = 0.0, 0.0
        self.MBN1G, self.VBN1G = [0.0] * H1, [0.0] * H1
        self.MBN1B, self.VBN1B = [0.0] * H1, [0.0] * H1
        self.MBN2G, self.VBN2G = [0.0] * H2, [0.0] * H2
        self.MBN2B, self.VBN2B = [0.0] * H2, [0.0] * H2
        self.TS = 0

    @staticmethod
    def lrelu(x):
        return x if x > 0 else 0.01 * x

    @staticmethod
    def lrelud(x):
        return 1.0 if x > 0 else 0.01

    @staticmethod
    def sigm(x):
        if math.isnan(x):
            return 0.5
        if x < -500.0:
            return 0.0
        if x > 500.0:
            return 1.0
        return 1.0 / (1.0 + math.exp(-x))

    @staticmethod
    def sigmd(o):
        if math.isnan(o):
            return 0.0
        return o * (1.0 - o)

    def forward(self, x, train=False):
        self.Z1 = [self.B1[h] + sum(x[i] * self.W1[i * H1 + h] for i in range(INPUT)) for h in range(H1)]
        m1 = sum(self.Z1) / H1
        v1 = sum((z - m1) ** 2 for z in self.Z1) / H1
        self.invStd1 = 1.0 / math.sqrt(v1 + BNE)
        self.zHat1 = [(z - m1) * self.invStd1 for z in self.Z1]
        self.bn1 = [self.zHat1[i] * self.BN1G[i] + self.BN1B[i] for i in range(H1)]
        self.rawA1 = [self.lrelu(self.bn1[h]) for h in range(H1)]
        self.A1 = [min(ACAP, a) for a in self.rawA1]
        self.dm1 = [True] * H1
        if train:
            for h in range(H1):
                self.dm1[h] = self.rnd.random() >= DR
                if not self.dm1[h]:
                    self.A1[h] = 0.0

        scale = 1.0 if train else (1.0 - DR)
        self.Z2 = [self.B2[h] + sum((self.A1[j] * scale) * self.W2[j * H2 + h] for j in range(H1)) for h in range(H2)]
        m2 = sum(self.Z2) / H2
        v2 = sum((z - m2) ** 2 for z in self.Z2) / H2
        self.invStd2 = 1.0 / math.sqrt(v2 + BNE)
        self.zHat2 = [(z - m2) * self.invStd2 for z in self.Z2]
        self.bn2 = [self.zHat2[i] * self.BN2G[i] + self.BN2B[i] for i in range(H2)]
        self.rawA2 = [self.lrelu(self.bn2[h]) for h in range(H2)]
        self.A2 = [min(ACAP, a) for a in self.rawA2]
        self.dm2 = [True] * H2
        if train:
            for h in range(H2):
                self.dm2[h] = self.rnd.random() >= DR
                if not self.dm2[h]:
                    self.A2[h] = 0.0

        o = self.B3 + sum((self.A2[h] * scale) * self.W3[h] for h in range(H2))
        self.A3 = self.sigm(o)
        return self.A3

    @staticmethod
    def layernorm_backward(d_out, zhat, gamma, inv_std, sz):
        d_gamma = [d_out[i] * zhat[i] for i in range(sz)]
        d_beta = list(d_out)
        d_zhat = [d_out[i] * gamma[i] for i in range(sz)]
        sum_dzhat = sum(d_zhat)
        sum_dzhat_zhat = sum(d_zhat[i] * zhat[i] for i in range(sz))
        inv_n = 1.0 / sz
        d_z = [inv_std * (d_zhat[i] - sum_dzhat * inv_n - zhat[i] * (sum_dzhat_zhat * inv_n)) for i in range(sz)]
        return d_z, d_gamma, d_beta

    def compute_analytical_gradients_eval(self, x, target):
        o = self.forward(x, train=False)
        scale = 1.0 - DR
        oe = (o - target) * self.sigmd(o)
        dW3 = [oe * (self.A2[h] * scale) for h in range(H2)]
        dB3 = oe
        d_out2 = [0.0 if (self.use_exact_layernorm_grad and self.rawA2[h] > ACAP) else (oe * self.W3[h] * scale * self.lrelud(self.bn2[h])) for h in range(H2)]
        if self.use_exact_layernorm_grad:
            dZ2, dBN2G, dBN2B = self.layernorm_backward(d_out2, self.zHat2, self.BN2G, self.invStd2, H2)
        else:
            dZ2, dBN2G, dBN2B = list(d_out2), [0.0] * H2, [0.0] * H2
        dW2 = [0.0] * (H1 * H2)
        for j in range(H1):
            a1s = self.A1[j] * scale
            for h in range(H2):
                dW2[j * H2 + h] = dZ2[h] * a1s
        dB2 = list(dZ2)
        d_out1 = [0.0] * H1
        for j in range(H1):
            if self.use_exact_layernorm_grad and self.rawA1[j] > ACAP:
                d_out1[j] = 0.0
            else:
                s = sum(dZ2[h] * self.W2[j * H2 + h] * scale for h in range(H2))
                d_out1[j] = s * self.lrelud(self.bn1[j])
        if self.use_exact_layernorm_grad:
            dZ1, dBN1G, dBN1B = self.layernorm_backward(d_out1, self.zHat1, self.BN1G, self.invStd1, H1)
        else:
            dZ1, dBN1G, dBN1B = list(d_out1), [0.0] * H1, [0.0] * H1
        dW1 = [0.0] * (INPUT * H1)
        for i in range(INPUT):
            for h in range(H1):
                dW1[i * H1 + h] = dZ1[h] * x[i]
        dB1 = list(dZ1)
        return {
            "W3": dW3, "B3": [dB3], "W2": dW2, "B2": dB2,
            "BN2G": dBN2G, "BN2B": dBN2B, "W1": dW1, "B1": dB1,
            "BN1G": dBN1G, "BN1B": dBN1B
        }

    @staticmethod
    def clip(v):
        if math.isnan(v) or math.isinf(v):
            return 0.0
        return max(-GRAD_CLIP, min(GRAD_CLIP, v))

    @staticmethod
    def adamw(w, g, m, v, lr, l2, t):
        b1, b2, eps = 0.9, 0.999, 1e-8
        bc1 = max(1e-5, 1.0 - (b1 ** max(1, t)))
        bc2 = max(1e-5, 1.0 - (b2 ** max(1, t)))
        for i in range(len(w)):
            grad = 0.0 if (math.isnan(g[i]) or math.isinf(g[i])) else g[i]
            m[i] = b1 * m[i] + (1.0 - b1) * grad
            v[i] = b2 * v[i] + (1.0 - b2) * (grad * grad)
            mh = m[i] / bc1
            vh = v[i] / bc2
            w[i] = w[i] * (1.0 - lr * l2) - (lr * mh) / (math.sqrt(max(0.0, vh)) + eps)
            if math.isnan(w[i]) or math.isinf(w[i]):
                w[i] = 0.0

    def train(self, x, target, lr=0.12, importance_weight=1.0):
        o = self.forward(x, train=True)
        oe = (o - target) * self.sigmd(o)
        dW3 = [self.clip(oe * self.A2[h]) for h in range(H2)]
        dB3 = self.clip(oe)
        if self.use_exact_layernorm_grad:
            dA2 = [0.0 if (not self.dm2[h] or self.rawA2[h] > ACAP) else (oe * self.W3[h] * self.lrelud(self.bn2[h])) for h in range(H2)]
            dBN2, dBN2G, dBN2B = self.layernorm_backward(dA2, self.zHat2, self.BN2G, self.invStd2, H2)
            dBN2G = [self.clip(g) for g in dBN2G]
            dBN2B = [self.clip(b) for b in dBN2B]
        else:
            dA2 = [0.0 if not self.dm2[h] else (oe * self.W3[h] * self.lrelud(self.bn2[h])) for h in range(H2)]
            dBN2 = list(dA2)
            dBN2G, dBN2B = [0.0] * H2, [0.0] * H2

        dW2 = [0.0] * (H1 * H2)
        for j in range(H1):
            a1 = self.A1[j]
            for h in range(H2):
                dW2[j * H2 + h] = self.clip(dBN2[h] * a1)
        dB2 = [self.clip(dBN2[h]) for h in range(H2)]

        dA1 = [0.0] * H1
        for j in range(H1):
            if not self.dm1[j] or (self.use_exact_layernorm_grad and self.rawA1[j] > ACAP):
                dA1[j] = 0.0
                continue
            s = sum(dBN2[h] * self.W2[j * H2 + h] for h in range(H2))
            dA1[j] = s * self.lrelud(self.bn1[j])

        if self.use_exact_layernorm_grad:
            dBN1, dBN1G, dBN1B = self.layernorm_backward(dA1, self.zHat1, self.BN1G, self.invStd1, H1)
            dBN1G = [self.clip(g) for g in dBN1G]
            dBN1B = [self.clip(b) for b in dBN1B]
        else:
            dBN1 = list(dA1)
            dBN1G, dBN1B = [0.0] * H1, [0.0] * H1

        dW1 = [0.0] * (INPUT * H1)
        for i in range(INPUT):
            for h in range(H1):
                dW1[i * H1 + h] = self.clip(dBN1[h] * x[i])
        dB1 = [self.clip(dBN1[h]) for h in range(H1)]

        eff_lr = lr * max(0.01, min(1.0, importance_weight))
        t = self.TS + 1
        self.adamw(self.W3, dW3, self.MW3, self.VW3, eff_lr, 0.001, t)
        self.adamw(self.W2, dW2, self.MW2, self.VW2, eff_lr, 0.001, t)
        self.adamw(self.B2, dB2, self.MB2, self.VB2, eff_lr, 0.0, t)
        if self.use_exact_layernorm_grad:
            self.adamw(self.BN2G, dBN2G, self.MBN2G, self.VBN2G, eff_lr, 0.0, t)
            self.adamw(self.BN2B, dBN2B, self.MBN2B, self.VBN2B, eff_lr, 0.0, t)
        self.adamw(self.W1, dW1, self.MW1, self.VW1, eff_lr, 0.001, t)
        self.adamw(self.B1, dB1, self.MB1, self.VB1, eff_lr, 0.0, t)
        if self.use_exact_layernorm_grad:
            self.adamw(self.BN1G, dBN1G, self.MBN1G, self.VBN1G, eff_lr, 0.0, t)
            self.adamw(self.BN1B, dBN1B, self.MBN1B, self.VBN1B, eff_lr, 0.0, t)

        b1, b2, eps = 0.9, 0.999, 1e-8
        grad_b3 = 0.0 if (math.isnan(dB3) or math.isinf(dB3)) else dB3
        self.MB3 = b1 * self.MB3 + (1.0 - b1) * grad_b3
        self.VB3 = b2 * self.VB3 + (1.0 - b2) * (grad_b3 * grad_b3)
        self.B3 -= (eff_lr * (self.MB3 / (1.0 - b1 ** t))) / (math.sqrt(max(0.0, self.VB3 / (1.0 - b2 ** t))) + eps)
        self.TS += 1


print("==========================================================================")
print("CLEARWIN AI v2.7.7 — PYTHON MATHEMATICAL REFERENCE VERIFICATION SUITE")
print("NOTE: This runs the standalone Python mathematical reference model.")
print("Production C# execution requires Windows + .NET Framework 4.x (build-tests.ps1).")
print("==========================================================================\n")

print("=== TEST 1: FINITE-DIFFERENCE GRADIENT CHECK ON FULL 18->32->16->1 NETWORK ===")
fd_net_old = ReferenceMLNeuralNetwork(seed=77, use_exact_layernorm_grad=False)
fd_net_new = ReferenceMLNeuralNetwork(seed=77, use_exact_layernorm_grad=True)
x_fd = [0.7, -1.1, 0.4, 1.3, -0.6, 0.2, -0.5, 0.8, -0.3, 1.1, -0.4, 0.6, -0.9, 0.3, 0.5, -0.7, 0.9, -0.2]
y_fd = 1.0
eps = 1e-6

g_old = fd_net_old.compute_analytical_gradients_eval(x_fd, y_fd)
g_new = fd_net_new.compute_analytical_gradients_eval(x_fd, y_fd)

for pname in ["W3", "W2", "W1", "BN2G", "BN2B", "BN1G", "BN1B", "B2", "B1"]:
    arr = getattr(fd_net_new, pname)
    max_abs_old = 0.0
    max_abs_new = 0.0
    for idx in range(len(arr)):
        orig = arr[idx]
        arr[idx] = orig + eps
        o_p = fd_net_new.forward(x_fd, train=False)
        l_p = 0.5 * ((o_p - y_fd) ** 2)
        arr[idx] = orig - eps
        o_m = fd_net_new.forward(x_fd, train=False)
        l_m = 0.5 * ((o_m - y_fd) ** 2)
        arr[idx] = orig
        num_g = (l_p - l_m) / (2.0 * eps)
        max_abs_old = max(max_abs_old, abs(g_old[pname][idx] - num_g))
        max_abs_new = max(max_abs_new, abs(g_new[pname][idx] - num_g))
    print(f"  {pname:5s} ({len(arr):3d} params) | Old Identity-BN MaxAbsErr={max_abs_old:.3e} | Exact LayerNorm MaxAbsErr={max_abs_new:.3e}")
    assert max_abs_new < 1e-8, f"Gradient check failed on {pname}: {max_abs_new}"

print("\n=== TEST 2: 2-SAMPLE CONVERGENCE & PROBABILITY COMPLEMENT TABLE (Seed=123) ===")
x_pos = [1.5, 1.8, -0.5, -0.3, 1.2, 0.5, -1.0, -1.0, -1.0, 0.8, 0.0, 0.0, -1.0, -1.0, 1.0, 0.8, 1.1, 0.4]
x_neg = [-1.2, -1.5, -0.8, -0.9, -1.0, -0.8, 1.0, 1.0, 1.0, -0.8, 0.0, 0.0, -1.0, -1.0, 1.0, -0.2, -0.3, -0.1]

for mode_label, use_exact in [("Legacy Identity-BN Backprop", False), ("Exact LayerNorm Backprop (v2.7.7)", True)]:
    n123 = ReferenceMLNeuralNetwork(seed=123, use_exact_layernorm_grad=use_exact)
    p0_pos = n123.forward(x_pos, train=False)
    p0_neg = n123.forward(x_neg, train=False)
    for _ in range(25):
        n123.train(x_pos, 1.0, lr=0.12)
        n123.train(x_neg, 0.0, lr=0.12)
    p25_pos = n123.forward(x_pos, train=False)
    p25_neg = n123.forward(x_neg, train=False)
    print(f"  [{mode_label}]")
    print(f"    Input x_pos (target=1.0): Pre p0={p0_pos:.4f} (complement 1-p0={1.0-p0_pos:.4f}) -> Post p25={p25_pos:.4f} (complement 1-p25={1.0-p25_pos:.4f})")
    print(f"    Input x_neg (target=0.0): Pre p0={p0_neg:.4f} (complement 1-p0={1.0-p0_neg:.4f}) -> Post p25={p25_neg:.4f} (complement 1-p25={1.0-p25_neg:.4f})")

print("\n=== TEST 3: DISJOINT TRAIN / TEST GENERALIZATION WITH EXACT LAYERNORM BACKPROP ===")
def make_raw_state(rng, is_pos):
    if is_pos:
        return [
            rng.uniform(25, 75), rng.uniform(82, 97), rng.uniform(10, 55), rng.uniform(15, 70),
            rng.uniform(45, 75), rng.uniform(40, 68), 0.0, 0.0, 100.0, rng.randint(110, 240),
            rng.uniform(8, 22), rng.uniform(0, 59), 0.0, 0.0, 100.0,
            rng.uniform(-3, 5), rng.uniform(1, 8), rng.uniform(-1, 2)
        ]
    else:
        is_game = rng.random() < 0.5
        return [
            rng.uniform(40, 90) if is_game else rng.uniform(5, 55),
            rng.uniform(60, 92) if is_game else rng.uniform(25, 72),
            rng.uniform(65, 98) if is_game else rng.uniform(5, 40),
            rng.uniform(10, 60),
            rng.uniform(60, 86) if is_game else rng.uniform(35, 60),
            rng.uniform(58, 84) if is_game else rng.uniform(35, 55),
            rng.uniform(55, 144) if is_game else 0.0,
            rng.uniform(40, 110) if is_game else 0.0,
            rng.uniform(75, 96) if is_game else 100.0,
            rng.randint(90, 190),
            rng.uniform(8, 23), rng.uniform(0, 59),
            1.0 if is_game else 0.0, 0.0, 100.0,
            rng.uniform(-4, 4), rng.uniform(-3, 3), rng.uniform(-1, 2)
        ]

rng_tr = random.Random(2026)
rng_te = random.Random(99999)
train_raw = [(make_raw_state(rng_tr, True), 1.0) for _ in range(40)] + [(make_raw_state(rng_tr, False), 0.0) for _ in range(40)]
test_raw = [(make_raw_state(rng_te, True), 1.0) for _ in range(40)] + [(make_raw_state(rng_te, False), 0.0) for _ in range(40)]
rng_tr.shuffle(train_raw)

# RunningNormalizer fitted strictly on train_raw
means = [50.0] * 18
stds = [25.0] * 18
counts = [0] * 18
def norm_fit_transform(vec):
    out = [0.0] * 18
    for i in range(18):
        counts[i] += 1
        d = vec[i] - means[i]
        means[i] += d / counts[i]
        d2 = vec[i] - means[i]
        if counts[i] > 1:
            stds[i] = max(0.1, math.sqrt((stds[i] * stds[i] * (counts[i] - 1) + d * d2) / counts[i]))
        out[i] = max(-3.0, min(3.0, (vec[i] - means[i]) / stds[i]))
    return out

def norm_frozen_transform(vec):
    return [max(-3.0, min(3.0, (vec[i] - means[i]) / stds[i])) for i in range(18)]

train_norm = [(norm_fit_transform(x), y) for x, y in train_raw]
test_norm = [(norm_frozen_transform(x), y) for x, y in test_raw]

ens = [ReferenceMLNeuralNetwork(seed=1000 + i * 9973, use_exact_layernorm_grad=True) for i in range(3)]
def ens_pred(x):
    return sum(n.forward(x, train=False) for n in ens) / 3.0

def eval_split(split):
    c = tp = fp = tn = fn = 0
    loss = 0.0
    for x, y in split:
        p = ens_pred(x)
        loss += 0.5 * ((p - y) ** 2)
        pr = 1.0 if p >= 0.5 else 0.0
        if pr == y: c += 1
        if y == 1.0 and pr == 1.0: tp += 1
        elif y == 1.0 and pr == 0.0: fn += 1
        elif y == 0.0 and pr == 1.0: fp += 1
        else: tn += 1
    return c / len(split), loss / len(split), tp, fp, tn, fn

pre_tr_a, pre_tr_l, *_ = eval_split(train_norm)
pre_te_a, pre_te_l, *_ = eval_split(test_norm)

for ep in range(12):
    for x, y in train_norm:
        for net in ens:
            net.train(x, y, lr=0.08)

post_tr_a, post_tr_l, tr_tp, tr_fp, tr_tn, tr_fn = eval_split(train_norm)
post_te_a, post_te_l, te_tp, te_fp, te_tn, te_fn = eval_split(test_norm)
print(f"  Train Set (N=80):        Pre Acc={pre_tr_a*100:.1f}% (Loss={pre_tr_l:.4f}) -> Post Acc={post_tr_a*100:.1f}% (Loss={post_tr_l:.4f}) [TP={tr_tp}, FP={tr_fp}, TN={tr_tn}, FN={tr_fn}]")
print(f"  Holdout Test Set (N=80): Pre Acc={pre_te_a*100:.1f}% (Loss={pre_te_l:.4f}) -> Post Acc={post_te_a*100:.1f}% (Loss={post_te_l:.4f}) [TP={te_tp}, FP={te_fp}, TN={te_tn}, FN={te_fn}]")
assert post_te_a >= 0.95
