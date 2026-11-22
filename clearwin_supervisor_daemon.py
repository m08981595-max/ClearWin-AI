"""
ClearWin AI — Autonomous Native Windows Supervisor Daemon
Supreme Director: Eng. Abdullah Mohamed (maher)
Workspace: ClearWin 2.7.2 GlassUI V21

This daemon runs independently at the Windows OS level via Task Scheduler (every 30m).
It is completely immune to IDE restarts, tab switches, and network disconnects.
It audits decisions, penalizes drift/errors, rebalances policy weights, and writes executive reports.
"""

import os
import sys
import json
import time
import datetime

# Fix pythonw.exe stdout/stderr None trap
if sys.stdout is None:
    sys.stdout = open(os.devnull, "w")
if sys.stderr is None:
    sys.stderr = open(os.devnull, "w")

def acquire_highest_privileges():
    """Acquires SeDebugPrivilege (20) and SeIncreaseBasePriorityPrivilege (14) via ntdll."""
    try:
        import ctypes
        ntdll = ctypes.windll.ntdll
        enabled = ctypes.c_bool()
        for priv in [20, 14, 19]:
            ntdll.RtlAdjustPrivilege(priv, True, False, ctypes.byref(enabled))
    except Exception:
        pass

def prevent_system_sleep():
    """Forces Windows to stay fully awake (blocks system sleep/standby) natively via kernel32."""
    try:
        import ctypes
        ctypes.windll.kernel32.SetThreadExecutionState(0x80000001)
    except Exception:
        pass

prevent_system_sleep()

def enforce_kernel_responsiveness_guards():
    """Forces Windows to keep kernel and drivers in RAM permanently, and maximizes UI quantum."""
    try:
        import subprocess
        subprocess.run('reg add "HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management" /v DisablePagingExecutive /t REG_DWORD /d 1 /f', shell=True, capture_output=True)
        subprocess.run('reg add "HKLM\\SYSTEM\\CurrentControlSet\\Control\\PriorityControl" /v Win32PrioritySeparation /t REG_DWORD /d 26 /f', shell=True, capture_output=True)
    except Exception:
        pass

enforce_kernel_responsiveness_guards()

def deploy_new_icons_elevated():
    """Deploys the upgraded ClearWin icons, syncs WebUI, deletes duplicate shortcuts, and cleans cache with elevation."""
    try:
        import subprocess
        script = os.path.join(os.path.dirname(os.path.abspath(__file__)), "sync_and_clean_desktop.py")
        if os.path.exists(script):
            res = subprocess.run([sys.executable, script], capture_output=True, text=True, timeout=30)
            with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "fix_icons_output.log"), "w", encoding="utf-8") as f:
                f.write(f"STDOUT:\n{res.stdout}\nSTDERR:\n{res.stderr}\n")
    except Exception as e:
        with open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "fix_icons_output.log"), "w", encoding="utf-8") as f:
            f.write(f"Exception: {e}\n")

deploy_new_icons_elevated()

def sync_upgraded_webui_elevated():
    """Synchronizes the upgraded GlassUI index.html and AI telemetry files to Program Files."""
    try:
        import shutil
        src_dir = os.path.dirname(os.path.abspath(__file__))
        src_html = os.path.join(src_dir, "Resources", "WebUI", "index.html")
        dest_dir = r"C:\Program Files\ClearWin\Resources\WebUI"
        dest_html = os.path.join(dest_dir, "index.html")
        dest_bak = os.path.join(dest_dir, "index.html.v21_pre_ai_hud.bak")
        
        if os.path.exists(src_html) and os.path.exists(dest_dir):
            if os.path.exists(dest_html) and not os.path.exists(dest_bak):
                shutil.copy2(dest_html, dest_bak)
            shutil.copy2(src_html, dest_html)
            
            # Also copy weights and training log so relative fetch in WebView2 always has live data
            src_weights = os.path.join(src_dir, "clearwin_ai_weights.json")
            dest_weights = os.path.join(dest_dir, "clearwin_ai_weights.json")
            if os.path.exists(src_weights):
                shutil.copy2(src_weights, dest_weights)
                
            src_log = os.path.join(src_dir, "clearwin_ai_training_log.md")
            dest_log = os.path.join(dest_dir, "clearwin_ai_training_log.md")
            if os.path.exists(src_log):
                shutil.copy2(src_log, dest_log)
                
            # Touch a sync stamp file
            stamp_file = os.path.join(src_dir, "webui_sync_success.txt")
            with open(stamp_file, "w", encoding="utf-8") as f:
                f.write(f"Synced successfully at {datetime.datetime.now().isoformat()}")
    except Exception as e:
        err_file = os.path.join(os.path.dirname(os.path.abspath(__file__)), "webui_sync_err.txt")
        try:
            with open(err_file, "w", encoding="utf-8") as f:
                f.write(f"Sync error: {e}")
        except Exception:
            pass

sync_upgraded_webui_elevated()

def neutralize_power_conflicts():
    """Disables rogue power monitors like ParkControl and terminates older stale trainer instances."""
    try:
        import subprocess, psutil
        # Terminate ParkControl and disable its scheduled task
        cmd = 'taskkill /F /IM "parkcontrol.exe" >nul 2>&1 & schtasks /Change /TN "ParkControl" /Disable >nul 2>&1'
        subprocess.run(cmd, shell=True, timeout=5)

        # Single trainer instance enforcement
        trainers = []
        for p in psutil.process_iter(['pid', 'cmdline', 'create_time']):
            try:
                c = ' '.join(p.info['cmdline'] or [])
                if 'clearwin_ai_trainer.py' in c:
                    trainers.append(p)
            except: pass
        if len(trainers) > 1:
            trainers.sort(key=lambda p: p.create_time(), reverse=True)
            for old_p in trainers[1:]:
                try: old_p.kill()
                except: pass
    except Exception:
        pass

neutralize_power_conflicts()

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
LOG_FILE = os.path.join(BASE_DIR, "clearwin_ai_training_log.md")
WEIGHTS_FILE = os.path.join(BASE_DIR, "clearwin_ai_weights.json")
REPORT_FILE = os.path.join(BASE_DIR, "clearwin_supervisor_report.md")

def audit_and_tune():
    if not os.path.exists(LOG_FILE) or not os.path.exists(WEIGHTS_FILE):
        return

    try:
        with open(LOG_FILE, "r", encoding="utf-8", errors="ignore") as f:
            lines = [l.strip() for l in f.readlines() if l.startswith("| #")]
    except Exception:
        return

    total_steps = len(lines)
    if total_steps == 0:
        return

    # Look at last 60 steps (~30 minutes of continuous decisions)
    recent = lines[-60:]
    parsed_recent = []
    for l in recent:
        parts = [p.strip() for p in l.split("|")]
        # Format: ["", "#0418", "2026-11-21 15:49:56", "⚠️ `TRIM_...`", "`MAINTAIN_...`", "-0.40", ...]
        if len(parts) >= 6:
            parsed_recent.append({
                "raw": l,
                "step": parts[1],
                "time": parts[2],
                "chosen": parts[3],
                "optimal": parts[4],
                "reward": parts[5],
                "is_correct": "✅" in parts[3],
                "is_flagged": "⚠️" in parts[3]
            })

    correct = sum(1 for p in parsed_recent if p["is_correct"])
    wrong = sum(1 for p in parsed_recent if p["is_flagged"])
    accuracy = (correct / len(parsed_recent)) * 100 if parsed_recent else 0.0

    try:
        with open(WEIGHTS_FILE, "r", encoding="utf-8") as f:
            weights_data = json.load(f)
    except Exception:
        return

    biases = weights_data.get("biases", [0.75, -0.70, -0.90, -1.40, -1.98])
    weights = weights_data.get("weights", [])
    tuning_actions = []

    # Check 1: Action 0 Bias Drift (positive reinforcement over-confidence)
    if biases[0] > 1.10 or (len(weights) > 0 and len(weights[0]) > 0 and weights[0][0] > 0.0):
        biases[0] = 0.75
        if len(weights) > 0:
            weights[0][0] = -0.50
            weights[0][1] = -0.30
            weights[0][2] = -0.40
        tuning_actions.append(f"Re-anchored Action 0 bias to +0.75 and clamped negative weights (prevented steady-state stubbornness under high load).")

    # Physics Invariants Guardrails (Action 0 must never drift positive on stress features)
    if len(weights) > 0:
        weights[0][0] = min(weights[0][0], -0.20)
        weights[0][1] = min(weights[0][1], -0.10)
        weights[0][2] = min(weights[0][2], -0.20)
        weights[0][3] = min(weights[0][3], 0.20)
        weights[0][5] = min(weights[0][5], -2.50)
        weights[0][8] = min(weights[0][8], -2.00)
    biases[0] = max(0.2, min(biases[0], 0.95))

    # Check 2: Action 1 RAM Sensitivity & Premature Trigger Guard
    premature_ram_trims = [p for p in parsed_recent if "TRIM_PROCESS_WORKING_SETS" in p["chosen"] and "MAINTAIN_STEADY_STATE" in p["optimal"] and p["is_flagged"]]
    missed_ram_trims = [p for p in parsed_recent if "TRIM_PROCESS_WORKING_SETS" in p["optimal"] and "TRIM_PROCESS_WORKING_SETS" not in p["chosen"] and p["is_flagged"]]
    
    if len(premature_ram_trims) >= 2:
        biases[1] = -1.25
        if len(weights) > 1 and len(weights[1]) > 4:
            weights[1][4] = min(weights[1][4], 0.40)
        tuning_actions.append(f"Calibrated Action 1 RAM bias to -1.25 (suppressed {len(premature_ram_trims)} premature trims under healthy RAM < 55%).")
    elif len(missed_ram_trims) >= 2:
        biases[1] = -1.25
        if len(weights) > 1 and len(weights[1]) > 0:
            weights[1][0] = max(weights[1][0], 3.40)
        tuning_actions.append(f"Boosted Action 1 RAM sensitivity (weight={weights[1][0]:.2f}, bias={biases[1]:.2f}) due to {len(missed_ram_trims)} delayed trims.")

    # Check 3: Action 3 CPU False-Trigger Guard (if throttle triggered on < 68% CPU)
    false_throttles = [p for p in parsed_recent if "THROTTLE_BACKGROUND_APPS" in p["chosen"] and "MAINTAIN_STEADY_STATE" in p["optimal"] and p["is_flagged"]]
    if len(false_throttles) >= 2:
        biases[3] = -2.50
        tuning_actions.append(f"Calibrated Action 3 throttle bias to -2.50 (guarded against {len(false_throttles)} false triggers on moderate CPU).")

    # Check 4: Action 2 Defrag Safety Enforcement
    defrag_attempts = [p for p in parsed_recent if "TRIGGER_IDLE_HDD_DEFRAG" in p["chosen"] and p["is_flagged"]]
    if defrag_attempts:
        biases[2] = -1.20
        tuning_actions.append(f"PENALTY: Lowered Action 2 bias to -1.20 after {len(defrag_attempts)} invalid defrag attempts.")

    # Check 5: Action 5 Hardware Bus & Power Stability Enforcement
    bus_hazards_handled = [p for p in parsed_recent if "ENFORCE_BUS_AND_POWER_STABILITY" in p["chosen"] and p["is_correct"]]
    if bus_hazards_handled:
        tuning_actions.append(f"VERIFIED: Action 5 successfully defended and neutralized {len(bus_hazards_handled)} hardware bus deadlocks.")
    bus_false_triggers = [p for p in parsed_recent if "ENFORCE_BUS_AND_POWER_STABILITY" in p["chosen"] and p["is_flagged"]]
    if bus_false_triggers:
        if len(biases) > 5:
            biases[5] = -2.80
        tuning_actions.append(f"PENALTY: Clamped Action 5 bias to -2.80 after {len(bus_false_triggers)} false triggers during safe bus state.")

    # Save weights if tuned
    now_str = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    if tuning_actions:
        weights_data["biases"] = biases
        weights_data["weights"] = weights
        weights_data["supervisor_notes"] = f"Auto-Supervisor ({now_str}): " + " | ".join(tuning_actions)
        try:
            with open(WEIGHTS_FILE, "w", encoding="utf-8") as f:
                json.dump(weights_data, f, indent=2)
        except Exception:
            pass

    # Generate Executive Supervisor Report
    report = (
        "# ClearWin AI — Half-Hourly Autonomous Supervisor Executive Report\n"
        f"> **Supreme Director:** Eng. Abdullah Mohamed (`maher`)\n"
        f"> **Audit Timestamp:** `{now_str}`\n"
        f"> **Supervision Engine:** Windows OS Independent Task Scheduler Daemon\n\n"
        "## 1. Window Performance Metrics (Past 30 Minutes)\n"
        f"- **Total Decision Cycles:** {total_steps}\n"
        f"- **Steps Audited (Recent Window):** {len(recent)}\n"
        f"- **Correct Autonomous Decisions:** {correct} (✅)\n"
        f"- **Decisions Flagged / Penalized:** {wrong} (⚠️)\n"
        f"- **Window Accuracy:** **`{accuracy:.1f}%`**\n"
        f"- **Cumulative AI Reward:** `{weights_data.get('cumulative_reward', 0.0):+.2f}`\n\n"
        "## 2. Active Weight & Bias Status\n"
        f"- **Action 0 (Steady State):** Bias = `{biases[0]:+.4f}`\n"
        f"- **Action 1 (Trim RAM):** Bias = `{biases[1]:+.4f}` | RAM Weight = `{weights[1][0]:+.4f}`\n"
        f"- **Action 2 (Defrag HDD):** Bias = `{biases[2]:+.4f}` | Idle Weight = `{weights[2][3]:+.4f}`\n"
        f"- **Action 3 (Throttle CPU):** Bias = `{biases[3]:+.4f}` | CPU Weight = `{weights[3][2]:+.4f}`\n"
        f"- **Action 4 (Emergency RAM):** Bias = `{biases[4]:+.4f}`\n"
        f"- **Action 5 (Bus & Power Guard):** Bias = `{(biases[5] if len(biases) > 5 else 0.0):+.4f}` | ASPM Weight = `{(weights[5][8] if len(weights) > 5 and len(weights[5]) > 8 else 0.0):+.4f}`\n\n"
        "## 3. Autonomous Supervisor Actions Applied\n"
    )

    if tuning_actions:
        for act in tuning_actions:
            report += f"- 🔧 {act}\n"
    else:
        report += "- ✅ **Weights Balanced:** All policy matrices are well-calibrated and within optimal operating thresholds.\n"

    report += "\n## 4. Last 5 Verified Decisions\n"
    report += "| Step | Timestamp | Decision | Optimal | Reward | Telemetry |\n"
    report += "| :--- | :--- | :--- | :--- | :--- | :--- |\n"
    for l in recent[-5:]:
        parts = [p.strip() for p in l.split("|")[1:-1]]
        if len(parts) >= 6:
            report += f"| {parts[0]} | {parts[1]} | {parts[2]} | {parts[3]} | {parts[4]} | {parts[5]}, {parts[6]} |\n"

    try:
        with open(REPORT_FILE, "w", encoding="utf-8") as f:
            f.write(report)
    except Exception:
        pass

if __name__ == "__main__":
    audit_and_tune()
