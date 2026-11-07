# ClearWin 2.7.7 — Smart Windows Optimizer & Glass UI

[![ClearWin Version](https://img.shields.io/badge/ClearWin-v2.7.7-blue.svg)](https://github.com)
[![Build Status](https://img.shields.io/badge/Build-Passing%20(173%2F173)-brightgreen.svg)](build-tests.ps1)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011%20x64-lightgrey.svg)](https://microsoft.com)
[![UI Engine](https://img.shields.io/badge/UI-WPF%20%2B%20WebView2-teal.svg)](Resources/WebUI)
[![Security Hardened](https://img.shields.io/badge/Security-SEC--01%20to%20SEC--11%20Hardened-success.svg)](ENGINEERING_AUDIT_REPORT_AR.md)

> **Arabic & English Documentation / توثيق ثنائي اللغة (العربية والإنجليزية)**

---

## 🌟 Overview / نظرة عامة

**ClearWin 2.7.7** is an advanced, AI-driven Windows optimization suite combining an asynchronous WPF core with a modern Glassmorphism WebView2 UI. Powered by Double Q-Learning (RL2), Causal AI, Bayesian inference, and Multi-Agent consensus, ClearWin continuously analyzes system metrics, predicts thermal/performance bottlenecks, and provides state-preserving system tweaks with safety rollback and least-privilege administrative process separation.

**ClearWin 2.7.7** هو برنامج هندسي متقدم لتحسين أداء نظام ويندوز، يجمع بين محرك WPF عالي الأداء مع واجهة زجاجية عصرية (Glassmorphism) عبر تقنية WebView2. يعتمد النظام على خوارزميات التعلم المعزز (Double Q-Learning)، والذكاء السببي (Causal AI)، والاستدلال البايزي، وتصويت الوكلاء المتعددين لتحليل النظام والتنبؤ بالاختناقات الحرارية والأداء، مع توفير تعديلات قابلة للاستعادة الكاملة وتأمين متقدم لصلاحيات المسؤول (Least-Privilege Process Separation).

---

## 📦 Packages & Distribution / حزم التوزيع والإصدار 2.7.7

All production release packages are located in `release\v2.7.7\`:

| Package / الحزمة | Description / الوصف | Size / الحجم | SHA-256 Hash |
| :--- | :--- | :--- | :--- |
| **`ClearWin-Setup-2.7.7.exe`** | Production Release Installer (Fully automated Inno Setup with self-contained runtime detection) | 4.53 MB (4,749,544 B) | Built with Roslyn 4.8.0 & Inno Setup 6 |

---

## 🚀 Installation & Usage / التثبيت والاستخدام

### English
1. **Interactive Installation**: Run `ClearWin-Setup-2.7.7.exe` from `release\v2.7.7\`.
2. **Silent Installation**:
   ```powershell
   .\ClearWin-Setup-2.7.7.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
   ```
3. **Launch**: Open from the desktop shortcut or `ClearWin.exe` in the installation directory.
4. **Uninstallation**:
   - Go to Windows Settings > Installed Apps > ClearWin > Uninstall.
   - Or silently run:
     ```powershell
     "{InstallDir}\unins000.exe" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
     ```

### العربية
1. **التثبيت التفاعلي**: شغّل `ClearWin-Setup-2.7.7.exe` من مجلد `release\v2.7.7\`.
2. **التثبيت الصامت**:
   ```powershell
   .\ClearWin-Setup-2.7.7.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
   ```
3. **التشغيل**: شغّل البرنامج من اختصار سطح المكتب أو ملف `ClearWin.exe` داخل مجلد التثبيت.
4. **إلغاء التثبيت**:
   - عبر إعدادات ويندوز > التطبيقات المثبتة > ClearWin > إلغاء التثبيت.
   - أو عبر التشغيل الصامت:
     ```powershell
     "{InstallDir}\unins000.exe" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
     ```

---

## 🛡️ Tweak Classification & Security Policy / تصنيف التعديلات وسياسة الأمان

ClearWin features 27 system tweaks categorized into 4 strict safety levels:

| Status / الحالة | Count / العدد | Tweak Names / التعديلات | Description / الوصف |
| :--- | :---: | :--- | :--- |
| **PASS (Safe & Reversible)** | 17 | `disable-telemetry`, `disable-cortana`, `disable-gamebar`, `disable-copilot`, `disable-bing-search`, `disable-windows-tips`, `disable-activity-history`, `disable-location-tracking`, `disable-diagnostic-data`, `disable-fast-startup`, `disable-hibernation`, `disable-background-ms-store-apps`, `enable-dark-mode`, `enable-end-task-right-click`, `revert-context-menu`, `set-time-utc`, `ultimate-performance-plan` | Fully tested, captures pre-tweak registry state via `TweakBackupEngine`, supports 100% clean undo. |
| **VERIFIED_PARTIAL** | 2 | `remove-gaming-apps`, `remove-onedrive` | Tested and safe; undo performs clean reinstall via Winget / Microsoft Store but cannot restore prior user app settings. |
| **BLOCKED (Opt-In Gate)** | 4 | `disable-core-isolation`, `disable-defender-rtp`, `debloat-windows`, `run-disk-cleanup` | High-impact actions gated by security prompts and mandatory System Restore point creation. |
| **NOT TESTED (Locked)** | 4 | `disable-dynamic-ticking`, `disable-hpet`, `optimize-network-settings`, `set-services-to-manual` | **Strictly locked and blocked** at 4 independent layers (UI, WebBridge, Environment sanitization, script code 3) to protect live BCD boot data, network stacks, and Windows services until isolated VM verification. |

---

## 🔒 Security Hardening (v2.7.7 Highlights)

- **Least-Privilege Process Separation (`SEC-01`, `SEC-07`):** GUI runs under Standard User context. Administrative elevation is delegated exclusively to an isolated helper process (`ClearWin.AdminHelper.exe`) with Win32 client PID authentication (`GetNamedPipeClientProcessId`) over a hardened Named Pipe.
- **Tweak Script Whitelist & Anti-Hijacking (`SEC-02`, `SEC-03`):** Strict verification of `apply.ps1` and `undo.ps1` script paths, blocking path traversal, symlink reparse points, and registry hijack patterns (IFEO, Run/RunOnce, Winlogon).
- **WebBridge CSRF & Ephemeral Token (`SEC-04`, `SEC-08`):** Ephemeral 32-byte cryptographic session token (`X-ClearWin-Token`), strict `Origin`/`Referer` checks, and directory separator normalization preventing partial path traversal.
- **Mandatory Policy Gating (`SEC-05`):** All UI and WebBridge operations route through `ActionPolicyGate.Evaluate()` with interactive confirmations for Yellow/Red tiers.
- **Thread Safety & UI Responsiveness (`SEC-09`):** Decoupled `_pipeLock` prevents long pipe calls from blocking the WPF UI thread.
- **Persistent AI Memory (`SEC-10`):** Fixed delimiter escaping and multi-field record loading in `AiPersistentMemory.cs`.
- **Audit Logging Quotas (`SEC-11`):** 5MB append-only rotating audit trail (`audit_log.jsonl.bak`).

---

## 🧪 Verification & Automated Testing / التحقق والاختبارات الآلية

The test suite can be compiled and executed locally via Roslyn:

```powershell
.\build-tests.ps1
```

**Results (173/173 Passed, 100% Rate):**
- **Core Engine & Budgeting**: Confidence range, dependency graph, memory caps, lock ordering, knapsack scheduling.
- **FPS Stabilizer**: Frame predictors, drop prevention, 60 FPS floor mode, multi-phase stress recovery.
- **Hardware & Metrics**: Vendor GPU Temperature (NVAPI/ADL), DXGI frame times, startup classifier.
- **WebBridge & UI Security**: Allowlist command filtering, URI anti-traversal, CSRF token validation, page generation guards.
- **AI & SmartAgent**: Symmetric Double Q-Learning (RL2), Causal AI, Risk Engine, Multi-Agent Consensus, KillSwitch.
- **Neural Networks (19/19)**: Forward pass, backpropagation weight updates, Xavier initialization, ensemble voting, calibrated abstention.
- **Security & Engineering Audit**: SEC-01 through SEC-11 automated compliance tests.

---

## 🛠️ Building From Source / البناء من المصدر

Requirements:
- Windows 10 or 11 (x64)
- .NET Framework 4.8 runtime (built-in to modern Windows)
- Roslyn 4.8.0 compiler (included in `tools\roslyn-4.8.0\`)
- Inno Setup 6 (included or installed, for compiling installers)

To build `ClearWin.exe` and release packages:
```powershell
# Clean build executable and package release
.\build.ps1
```

---

## ⚖️ License & Credits / الترخيص والمطور

- **Developer**: Abdullah Mohamed
- **License**: MIT License (see `LICENSE`)
- **Third-Party Libraries**: Microsoft.Web.WebView2 (BSD-style), Roslyn C# Compiler (Apache 2.0).
