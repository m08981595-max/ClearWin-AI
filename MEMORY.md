# Project Working Memory (Persistent State)

## 📌 Project Overview
* **Workspace:** `ClearWin-2.7.2-GlassUI-V21-AI-EXECUTION-CENTER\clearwin_v19_scrollbar_control\_ClearWin_Engine`
* **Status:** 100% Production Ready, Locally Tested & Monitored (ClearWin AI v2.7.7).
* **Test Suite Status:** 173 / 173 Passed (100% Rate).
* **Release Artifacts:**
  * `release\v2.7.7\ClearWin-Setup-2.7.7.exe` (4.53 MB, Inno Setup Engine).
  * `ClearWin-Setup.exe` / `ClearWin-WebInstaller.exe` (227 KB, C# Roslyn 4.8.0 Web Bootstrapper).
* **Live System Test:**
  * Application installed to `C:\Program Files\ClearWin` and verified.
  * Active Process: `ClearWin.exe` (PID: 10588), WebView2 processes active, Glass UI responding.
  * Stability Daemon (`StabilityDaemon.ps1`) actively logging health metrics to `STABILITY_MONITOR_REPORT.md` every 30 seconds.

---

## 🛡️ Completed Hardening & Engineering Fixes (v2.7.7)
1. **SEC-01 & SEC-07 (Named Pipe & Process Arguments):**
   - Eliminated token and pipe name exposure in command-line arguments.
   - Implemented Win32 client PID verification (`GetNamedPipeClientProcessId`) inside `AdminHelperProgram.cs`.
   - Added path validation and reparse point rejection (`FileAttributes.ReparsePoint`) in `AdminSessionManager.cs`.
2. **SEC-02 (PowerShell Script Integrity in AdminHelper):**
   - Strict validation of tweak script names (`apply.ps1` and `undo.ps1` only).
   - Known tweaks whitelist enforcement; canonical path verification against directory traversal and symlink hijacking.
3. **SEC-03 (Tweak Backup Deserialization Guard):**
   - Implemented `Test-ValidTweakRegistryPath` in `TweakBackupEngine.ps1` blocking persistence/hijack vectors (IFEO, Run/RunOnce, Winlogon, AppInit_DLLs, etc.).
4. **SEC-04 & SEC-08 (LocalWebBridge Hardening & Path Traversal):**
   - Added ephemeral cryptographic session token (`X-ClearWin-Token`) and origin/referer validation on POST `/api/action`.
   - Enforced trailing directory separator normalization on `webRoot` to prevent partial path traversal.
5. **SEC-05 (ActionPolicyGate Integration):**
   - WebUI actions and WebBridge requests now strictly route through `ActionPolicyGate.Evaluate()`.
   - Interactive confirmation dialogs for Yellow Tier actions; explicit blocks on unconfirmed Red Tier actions.
6. **SEC-06 (RunCustomScript Prefix Whitelist):**
   - Added strict whitelist for `cmd` and `powershell` prefixes in `SmartAgent.ToolTraining.cs`.
7. **SEC-09 (UI Thread Freezing / Lock Decoupling):**
   - Decoupled `_pipeLock` from `_sync` in `AdminSessionManager.cs`, eliminating 130-second UI freezes during elevated commands.
8. **SEC-10 (AiPersistentMemory Delimiter & Field Fix):**
   - Corrected parsing condition (`p.Length < 6`) and pipe escaping in `AiPersistentMemory.cs`, restoring persistent AI learning across reboots.
9. **SEC-11 (Audit Log Rotation):**
   - Implemented 5MB log rotation (`audit_log.jsonl.bak`) in `ClearWinAuditLogger.cs`.
10. **Compilation & Neural Test Suite (173/173):**
    - Fixed duplicate `LASTINPUTINFO` definitions across `SmartAgent.cs` and `SmartAgent.FramePacing.cs`.
    - Fixed `TestGetMLDecision` test isolation and simulation state in `NeuralNetworkTest.cs`.
    - Added stale process cleanup in `build-tests.ps1` to prevent `error CS2012` file locking.
11. **UI-01 (BiDi & Mixed Arabic/English Layout Isolation):**
    - Configured dedicated `.bidi-en` and `.bidi-parens` isolation, tree walker, and font stack for zero BiDi corruption.
12. **BOOT-01 (Single-File Web Bootstrapper / Installer EXE):**
    - Developed and compiled `src\WebInstaller\WebInstallerProgram.cs` into `ClearWin-Setup.exe` (227 KB).
    - Features: Admin self-elevation, WebView2 Evergreen check/install, SHA-256 integrity verification, remote cloud repository download with fallback to local payload, silent installation, and auto-launch.
13. **MON-01 (Autonomous Stability Daemon):**
    - Deployed `StabilityDaemon.ps1` running continuously as a background watchdog.
    - Appends real-time metrics (RAM, CPU, thread count, handles, responsiveness, WebView2 status) to `STABILITY_MONITOR_REPORT.md`.
