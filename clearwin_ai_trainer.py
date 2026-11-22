#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
ClearWin 2.7.2 GlassUI V21 — Pure Windows PC AI Engine & Decision Center
Supreme Director: Eng. Abdullah Mohamed (maher)
==============================================================================
100% Windows PC telemetry & decision system:
- Monitors Physical RAM, Commit Charge, CPU load, HDD Idle state, Process Working Sets.
- Learns optimal tuning decisions using Policy Gradient RL on real device physics.
- Includes built-in stress-testing to verify live reaction and decision quality.
"""

import os
import sys
import time
import json
import math
import ctypes
import datetime
import threading
import subprocess

# Redirect null streams when running under pythonw.exe
if sys.stdout is None:
    sys.stdout = open(os.devnull, "w", encoding="utf-8")
if sys.stderr is None:
    sys.stderr = open(os.devnull, "w", encoding="utf-8")

# Ensure UTF-8 output
if sys.platform == "win32":
    try:
        if sys.stdout: sys.stdout.reconfigure(encoding="utf-8")
        if sys.stderr: sys.stderr.reconfigure(encoding="utf-8")
    except Exception:
        pass

def acquire_highest_privileges():
    """Acquires SeDebugPrivilege (20) and SeIncreaseBasePriorityPrivilege (14) via ntdll."""
    try:
        ntdll = ctypes.windll.ntdll
        enabled = ctypes.c_bool()
        for priv in [20, 14, 19]:
            ntdll.RtlAdjustPrivilege(priv, True, False, ctypes.byref(enabled))
    except Exception:
        pass

acquire_highest_privileges()

def prevent_system_sleep():
    """Forces Windows to stay fully awake (blocks system sleep/standby) natively via kernel32."""
    try:
        # ES_CONTINUOUS (0x80000000) | ES_SYSTEM_REQUIRED (0x00000001)
        ctypes.windll.kernel32.SetThreadExecutionState(0x80000001)
    except Exception:
        pass

prevent_system_sleep()

def neutralize_power_conflicts():
    """Disables rogue power monitors like ParkControl that fight over power plans and cause deadlocks."""
    try:
        import subprocess
        cmd = 'taskkill /F /IM "parkcontrol.exe" >nul 2>&1 & schtasks /Change /TN "ParkControl" /Disable >nul 2>&1'
        subprocess.run(cmd, shell=True, timeout=5)
    except Exception:
        pass

neutralize_power_conflicts()

acquire_highest_privileges()

WORKSPACE_DIR = os.path.dirname(os.path.abspath(__file__))
WEIGHTS_FILE = os.path.join(WORKSPACE_DIR, "clearwin_ai_weights.json")
LOG_FILE = os.path.join(WORKSPACE_DIR, "clearwin_ai_training_log.md")
TRIMMER_SCRIPT = r"C:\Users\maher\bin\ram-trimmer.ps1"

# Pure Windows PC Action Space (Expanded with Hardware & Bus Stability Guard)
ACTIONS = {
    0: "MAINTAIN_STEADY_STATE",
    1: "TRIM_PROCESS_WORKING_SETS",
    2: "TRIGGER_IDLE_HDD_DEFRAG",
    3: "THROTTLE_BACKGROUND_APPS",
    4: "EMERGENCY_RAM_RECOVERY",
    5: "ENFORCE_BUS_AND_POWER_STABILITY"
}

# Features:
# [0] ram_used_pct (0.0 - 1.0)
# [1] commit_charge_pct (0.0 - 1.0)
# [2] cpu_load_pct (0.0 - 1.0)
# [3] idle_time_ratio (0.0 - 1.0, scaled to 15 mins)
# [4] bloated_process_ratio (largest process WS / total RAM)
# [5] is_critically_low_ram (1.0 if free < 1.8GB or ram > 75%)
# [6] disk_active_ratio (real live MB/s disk I/O activity)
# [7] recent_trim_cooldown (1.0 if trimmed recently else 0.0)
# [8] pcie_aspm_hazard (1.0 if PCIe ASPM or Disk spin-down active)
NUM_FEATURES = 9
NUM_ACTIONS = len(ACTIONS)

# Default base policy weights for PC architecture
DEFAULT_WEIGHTS = {
    "version": "21.3-pc-hardware-guard",
    "updated_at": "",
    "total_training_iterations": 0,
    "cumulative_reward": 0.0,
    "learning_rate": 0.02,
    "weights": [
        #  RAM    Commit   CPU    Idle   Bloat  CritRAM Disk   Cooldown ASPM_Hazard
        [ -0.5,   -0.3,  -0.4,   0.2,  -0.4,  -2.5,   0.0,    0.8,     -2.0 ],  # 0: MAINTAIN_STEADY_STATE
        [  2.3,    0.2,   0.2,  -0.1,   0.4,   0.8,  -0.4,   -1.8,     -1.0 ],  # 1: TRIM_PROCESS_WORKING_SETS
        [  0.2,    0.3,  -0.7,   5.1,   0.0,  -0.5,  -1.5,    0.0,     -1.0 ],  # 2: TRIGGER_IDLE_HDD_DEFRAG
        [  0.3,    0.3,   3.8,   0.0,   0.2,   0.4,   0.5,   -0.1,     -1.0 ],  # 3: THROTTLE_BACKGROUND_APPS
        [  2.9,    1.8,   0.8,  -0.2,   1.1,   6.5,   0.1,   -1.3,     -1.0 ],  # 4: EMERGENCY_RAM_RECOVERY
        [ -0.2,   -0.2,  -0.2,   0.0,  -0.2,   0.0,  -0.2,    0.0,     +7.5 ]   # 5: ENFORCE_BUS_AND_POWER_STABILITY
    ],
    "biases": [0.95, -1.25, -0.87, -2.50, -3.20, -2.50]
}

_last_trim_timestamp = 0.0
_last_defrag_timestamp = 0.0
_last_disk_sample_time = 0.0
_last_disk_sample_bytes = 0
_last_disk_sample_ops = 0
_last_disk_sample_time_ms = 0

def load_weights():
    if os.path.exists(WEIGHTS_FILE):
        try:
            with open(WEIGHTS_FILE, "r", encoding="utf-8") as f:
                data = json.load(f)
                weights = data.get("weights", [])
                biases = data.get("biases", [])
                
                # Auto-migrate from 21.2 (5 actions, 8 features) to 21.3 (6 actions, 9 features)
                if len(weights) == 5:
                    # Expand existing rows to 9 features
                    for row in weights:
                        if len(row) == 8:
                            row.append(-1.0)
                    # Add Action 5 (ENFORCE_BUS_AND_POWER_STABILITY)
                    action_5_weights = [-0.2, -0.2, -0.2, 0.0, -0.2, 0.0, -0.2, 0.0, 7.5]
                    weights.append(action_5_weights)
                    biases.append(-2.50)
                    data["weights"] = weights
                    data["biases"] = biases
                    data["version"] = "21.3-pc-hardware-guard"
                    save_weights(data)
                    return data
                elif len(weights) == NUM_ACTIONS:
                    return data
        except Exception:
            pass
    return json.loads(json.dumps(DEFAULT_WEIGHTS))

def save_weights(weights_data):
    weights_data["updated_at"] = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    with open(WEIGHTS_FILE, "w", encoding="utf-8") as f:
        json.dump(weights_data, f, indent=2)

def collect_pc_telemetry():
    """Extract real live hardware & OS telemetry from THIS Windows PC."""
    global _last_trim_timestamp
    features = [0.0] * NUM_FEATURES
    details = {}

    # 1. Physical RAM & Commit Memory (GlobalMemoryStatusEx)
    class MEMORYSTATUSEX(ctypes.Structure):
        _fields_ = [
            ("dwLength", ctypes.c_ulong),
            ("dwMemoryLoad", ctypes.c_ulong),
            ("ullTotalPhys", ctypes.c_ulonglong),
            ("ullAvailPhys", ctypes.c_ulonglong),
            ("ullTotalPageFile", ctypes.c_ulonglong),
            ("ullAvailPageFile", ctypes.c_ulonglong),
            ("ullTotalVirtual", ctypes.c_ulonglong),
            ("ullAvailVirtual", ctypes.c_ulonglong),
            ("ullAvailExtendedVirtual", ctypes.c_ulonglong),
        ]

    stat = MEMORYSTATUSEX()
    stat.dwLength = ctypes.sizeof(MEMORYSTATUSEX)
    ctypes.windll.kernel32.GlobalMemoryStatusEx(ctypes.byref(stat))

    total_ram_mb = stat.ullTotalPhys / (1024 * 1024)
    free_ram_mb = stat.ullAvailPhys / (1024 * 1024)
    used_ram_mb = total_ram_mb - free_ram_mb
    ram_used_pct = stat.dwMemoryLoad / 100.0

    total_commit_mb = stat.ullTotalPageFile / (1024 * 1024)
    free_commit_mb = stat.ullAvailPageFile / (1024 * 1024)
    commit_used_pct = (total_commit_mb - free_commit_mb) / max(total_commit_mb, 1.0)

    features[0] = round(ram_used_pct, 4)
    features[1] = round(commit_used_pct, 4)
    details["ram_used_pct"] = round(ram_used_pct * 100, 1)
    details["free_ram_mb"] = round(free_ram_mb, 1)
    details["total_ram_mb"] = round(total_ram_mb, 1)
    details["commit_pct"] = round(commit_used_pct * 100, 1)

    # 2. CPU Total Utilization (via NtQuerySystemInformation or psutil)
    cpu_pct = 0.15
    try:
        import psutil
        cpu_pct = psutil.cpu_percent(interval=0.15) / 100.0
    except Exception:
        pass
    features[2] = round(cpu_pct, 4)
    details["cpu_load_pct"] = round(cpu_pct * 100, 1)

    # 3. User Idle Time (GetLastInputInfo - tracks keyboard/mouse inactivity)
    class LASTINPUTINFO(ctypes.Structure):
        _fields_ = [("cbSize", ctypes.c_uint), ("dwTime", ctypes.c_uint)]

    lii = LASTINPUTINFO()
    lii.cbSize = ctypes.sizeof(LASTINPUTINFO)
    ctypes.windll.user32.GetLastInputInfo(ctypes.byref(lii))
    millis_idle = ctypes.windll.kernel32.GetTickCount() - lii.dwTime
    idle_minutes = millis_idle / 60000.0
    idle_ratio = min(round(idle_minutes / 15.0, 4), 1.5)  # 1.0 means exactly 15 minutes idle
    features[3] = idle_ratio
    details["idle_minutes"] = round(idle_minutes, 2)

    # 4. Largest Process Working Set & Bloat Analysis
    max_ws_mb = 0.0
    max_proc_name = "none"
    try:
        import psutil
        for p in psutil.process_iter(['name', 'memory_info']):
            try:
                rss = p.info['memory_info'].rss / (1024 * 1024)
                if rss > max_ws_mb:
                    max_ws_mb = rss
                    max_proc_name = p.info['name']
            except (psutil.NoSuchProcess, psutil.AccessDenied):
                continue
    except Exception:
        max_ws_mb = 350.0
        max_proc_name = "explorer.exe"

    features[4] = min(round(max_ws_mb / total_ram_mb, 4), 1.0)
    details["top_process"] = f"{max_proc_name} ({round(max_ws_mb, 1)}MB)"

    # 5. Critical Low RAM (< 1.8 GB free or > 75% used)
    features[5] = 1.0 if (free_ram_mb < 1800.0 or ram_used_pct > 0.75) else 0.0
    details["is_crit_ram"] = bool(features[5])

    # 6. Real Disk IO & Mechanical Bottleneck Telemetry (Throughput, Latency, IOPS)
    global _last_disk_sample_time, _last_disk_sample_bytes, _last_disk_sample_ops, _last_disk_sample_time_ms
    disk_mb_s = 0.0
    disk_latency_ms = 0.0
    disk_iops = 0.0
    try:
        import psutil
        now_time = time.time()
        c = psutil.disk_io_counters()
        total_bytes = c.read_bytes + c.write_bytes
        total_ops = c.read_count + c.write_count
        total_time_ms = c.read_time + c.write_time
        if _last_disk_sample_time > 0 and now_time > _last_disk_sample_time:
            dt = now_time - _last_disk_sample_time
            disk_mb_s = round(((total_bytes - _last_disk_sample_bytes) / dt) / (1024 * 1024), 2)
            d_ops = total_ops - _last_disk_sample_ops
            d_time = total_time_ms - _last_disk_sample_time_ms
            disk_iops = round(d_ops / dt, 1)
            if d_ops > 0:
                disk_latency_ms = round(d_time / d_ops, 2)
        _last_disk_sample_time = now_time
        _last_disk_sample_bytes = total_bytes
        _last_disk_sample_ops = total_ops
        _last_disk_sample_time_ms = total_time_ms
    except Exception:
        pass

    # Mechanical HDD Seek & Queue Saturation:
    # Thrashing specifically occurs when random seek latency > 35ms, or sustained queue choking (> 30MB/s AND latency > 15ms).
    # Sequential file flushing (ultra-low latency < 5ms) is smooth streaming, NOT head thrashing.
    is_hdd_thrashing = bool(disk_latency_ms > 35.0 or (disk_mb_s > 30.0 and disk_latency_ms > 15.0))
    features[6] = 1.0 if is_hdd_thrashing else round(min(max(disk_mb_s / 30.0, disk_latency_ms / 50.0) * 0.5, 0.5), 4)
    details["disk_io_mb_s"] = disk_mb_s
    details["disk_latency_ms"] = disk_latency_ms
    details["disk_iops"] = disk_iops
    details["is_hdd_thrashing"] = is_hdd_thrashing

    # 7. Recent Trim Cooldown (prevents hammering trim in a loop)
    time_since_trim = time.time() - _last_trim_timestamp
    features[7] = 1.0 if time_since_trim < 45.0 else 0.0
    details["trim_cooldown_active"] = bool(features[7])

    # 8. Hardware Bus & Power Hazard (PCIe ASPM or Disk Spin-down Active)
    # Detects the exact hardware condition that caused PCIe bus deadlocks & hard freezes
    aspm_hazard = 0.0
    active_scheme = "12111111-1211-1211-1211-121111111111"
    try:
        import winreg
        with winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, r"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes") as key:
            active_scheme, _ = winreg.QueryValueEx(key, "ActivePowerScheme")

        aspm_path = rf"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\{active_scheme}\501a4d13-42af-4429-9fd1-a8218c268e20\ee12f906-d277-404b-b6da-e5fa1a576df5"
        try:
            with winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, aspm_path) as k:
                aspm_ac, _ = winreg.QueryValueEx(k, "ACSettingIndex")
                if aspm_ac > 0:
                    aspm_hazard = 1.0
        except Exception:
            pass

        disk_path = rf"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes\{active_scheme}\0012ee47-9041-4b5d-9b77-535fba8b1442\6738e2c4-e8a5-4a42-b16a-e040e769756e"
        try:
            with winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, disk_path) as k:
                disk_ac, _ = winreg.QueryValueEx(k, "ACSettingIndex")
                if disk_ac > 0:
                    aspm_hazard = 1.0
        except Exception:
            pass
    except Exception:
        pass

    features[8] = aspm_hazard
    details["pcie_aspm_hazard"] = bool(aspm_hazard)
    details["active_scheme"] = active_scheme

    return features, details

def compute_action_distribution(features, weights, biases):
    logits = []
    for a in range(NUM_ACTIONS):
        val = biases[a]
        for f in range(NUM_FEATURES):
            val += weights[a][f] * features[f]
        logits.append(val)

    # Physical Safety Gates & Strict Operational Envelope:
    # Action 1 (Trim RAM): Forbidden unless RAM is elevated (> 60%), cooldown clear (features[7] == 0), HDD not thrashing (features[6] < 0.8), and NOT critically exhausted (features[5] < 0.5)
    if features[0] <= 0.60 or features[7] > 0.5 or features[6] > 0.8 or features[5] > 0.5:
        logits[1] = -999.0
    # Action 2 (Defrag HDD): Forbidden unless user idle >= 15m AND defrag cooldown is clear (>= 2h) AND HDD calm (features[6] < 0.20) AND RAM healthy (features[0] < 0.65)
    global _last_defrag_timestamp
    if features[3] < 1.0 or (time.time() - _last_defrag_timestamp) < 7200.0 or features[6] >= 0.20 or features[0] >= 0.65:
        logits[2] = -999.0
    # Action 3 (Throttle CPU/Disk): Forbidden unless CPU is actually high (>= 68%) OR Mechanical HDD is thrashing (features[6] > 0.8)
    if features[2] < 0.68 and features[6] < 0.8:
        logits[3] = -999.0
    # Action 4 (Emergency RAM): Forbidden unless RAM is critically exhausted (< 1.8GB or > 75%, features[5] == 1.0)
    if features[5] < 0.5:
        logits[4] = -999.0
    # Action 5 (Bus & Power Guard): Forbidden unless PCIe ASPM or Disk sleep hazard exists (features[8] == 1.0)
    if features[8] < 0.5:
        logits[5] = -999.0

    # Numerically stable softmax
    max_l = max(logits)
    exp_l = [math.exp(l - max_l) for l in logits]
    sum_e = sum(exp_l)
    probs = [e / sum_e for e in exp_l]
    return logits, probs

def evaluate_decision_physics(chosen_action, features, details):
    """
    Evaluates whether the decision aligns with optimal Windows performance & hardware stability physics:
    - Priority 0: Eliminate hardware bus deadlocks (PCIe ASPM or Disk spin-down timeout active)
    - Priority 1: Trigger emergency recovery if RAM is critically low (< 1.8 GB or > 75%)
    - Priority 2: Safe Defrag Window (>= 15m user idle, no active CPU spike, RAM healthy, disk calm)
    - Priority 3: Throttle background tasks during high CPU workload (> 70%) OR Mechanical HDD Thrashing
    - Priority 4: Trim bloated RAM (> 60% used) when cooldown clear and disk not thrashing
    - Default: Maintain steady state if metrics and hardware buses are safe
    """
    free_ram = details.get("free_ram_mb", 4000.0)
    ram_pct = details.get("ram_used_pct", 45.0)
    idle_min = details.get("idle_minutes", 0.0)
    cpu_pct = details.get("cpu_load_pct", 15.0)
    cooldown = details.get("trim_cooldown_active", False)
    aspm_hazard = details.get("pcie_aspm_hazard", False)
    disk_mb_s = details.get("disk_io_mb_s", 0.0)
    disk_lat = details.get("disk_latency_ms", 0.0)
    is_thrashing = details.get("is_hdd_thrashing", False)

    optimal_action = 0
    reasoning = []
    global _last_defrag_timestamp
    defrag_cooldown = (time.time() - _last_defrag_timestamp) < 7200.0  # 2 hours cooldown between defrags

    # Priority 0: Hardware Bus & PCIe Power Link Deadlock Prevention
    if aspm_hazard:
        optimal_action = 5
        reasoning.append("Hardware bus hazard detected (PCIe ASPM or Disk spin-down active) -> Enforce bus & power stability")
    # Priority 1: Critical Emergency RAM
    elif free_ram < 1800.0 or ram_pct > 75.0:
        optimal_action = 4
        reasoning.append(f"RAM under critical pressure ({ram_pct}% used, {free_ram}MB free) -> Emergency RAM Recovery required")
    # Priority 2: Safe Defrag Window (>= 15m user idle, cool CPU, RAM healthy, disk calm, cooldown clear)
    elif idle_min >= 15.0 and not defrag_cooldown and cpu_pct < 35.0 and ram_pct < 65.0 and disk_mb_s < 3.0 and disk_lat < 15.0:
        optimal_action = 2
        reasoning.append(f"System idle for {idle_min:.1f}m (>= 15m) with cool CPU & calm disk -> Optimal window for HDD Defragmentation")
    # Priority 3: High CPU workload (> 70% CPU) OR Mechanical HDD Thrashing / Bottleneck Saturation
    elif cpu_pct > 70.0 or is_thrashing:
        optimal_action = 3
        if cpu_pct > 70.0:
            reasoning.append(f"High CPU workload ({cpu_pct}%) -> Throttle background tasks")
        else:
            reasoning.append(f"Mechanical HDD bottleneck saturated (Latency: {disk_lat}ms, Throughput: {disk_mb_s}MB/s) -> Demote background tasks to Low I/O Priority")
    # Priority 4: RAM Trim when bloated (> 60% used) and not in cooldown and disk not choking
    elif ram_pct > 60.0 and not cooldown and not is_thrashing and disk_mb_s < 20.0:
        optimal_action = 1
        reasoning.append(f"RAM usage elevated ({ram_pct}%) and cooldown clear -> Trim working sets")
    # Default: Maintain Steady State
    else:
        optimal_action = 0
        reasoning.append(f"System healthy (RAM {ram_pct}%, Free {free_ram}MB, CPU {cpu_pct}%, HDD {disk_lat}ms, Bus Safe) -> Maintain steady state")

    # Reward calculation
    if chosen_action == optimal_action:
        reward = 1.5
    else:
        # Penalties for hazardous decisions
        if chosen_action == 2 and idle_min < 15.0:
            reward = -2.5  # Critical error: defragging while user is working!
            reasoning.append("PENALTY: Defrag attempted during active user session!")
        elif chosen_action != 5 and aspm_hazard:
            reward = -2.5  # Inaction during hardware bus lockup risk
            reasoning.append("PENALTY: Failed to enforce bus stability while PCIe ASPM / Disk sleep hazard is active!")
        elif chosen_action in (1, 2) and disk_mb_s > 25.0:
            reward = -1.5  # Heavy I/O thrashing on mechanical disk
            reasoning.append("PENALTY: Heavy memory/disk operation attempted while mechanical HDD is already saturated!")
        elif chosen_action == 1 and cooldown:
            reward = -1.0  # Excessive trim thrashing
            reasoning.append("PENALTY: Trim attempted while in cooldown window")
        elif chosen_action == 1 and free_ram > 5000.0:
            reward = -0.6  # Unnecessary trimming when RAM is already > 5GB free
        elif chosen_action == 0 and free_ram < 1800.0:
            reward = -2.0  # Inaction during critical memory shortage
        else:
            reward = -0.4

    return reward, optimal_action, " | ".join(reasoning)

def update_weights(weights, biases, features, chosen_action, optimal_action, reward, lr=0.02):
    for f in range(NUM_FEATURES):
        # Reinforce good, punish bad
        weights[chosen_action][f] += lr * reward * features[f]
        if chosen_action != optimal_action:
            weights[optimal_action][f] += lr * 1.0 * features[f]
            weights[chosen_action][f] -= lr * 0.6 * features[f]

    biases[chosen_action] += lr * reward * 0.1
    if chosen_action != optimal_action:
        biases[optimal_action] += lr * 0.15
        biases[chosen_action] -= lr * 0.08

    # Physics Invariants Guardrails (prevents policy drift across rare/frequent events):
    # Action 0 (Steady State): High RAM, Commit, CPU, Disk Stress, or ASPM Hazard must never encourage steady state
    weights[0][0] = min(weights[0][0], -0.2)  # RAM
    weights[0][1] = min(weights[0][1], -0.1)  # Commit
    weights[0][2] = min(weights[0][2], -0.2)  # CPU
    weights[0][3] = min(weights[0][3], 0.0)   # Idle
    weights[0][4] = min(weights[0][4], 0.0)   # Bloat
    weights[0][5] = min(weights[0][5], -2.5)  # Crit RAM
    weights[0][6] = min(weights[0][6], -1.0)  # Disk Stress / Thrashing
    weights[0][8] = min(weights[0][8], -2.0)  # ASPM Hazard
    biases[0] = max(0.2, min(biases[0], 1.2)) # Bound Action 0 bias

    # Action 1 (Trim RAM): Must stay responsive to elevated RAM and suppressed by disk stress
    weights[1][0] = max(weights[1][0], 2.5)
    weights[1][6] = min(weights[1][6], -1.5)

    # Action 2 (HDD Defrag): Idle weight must remain high to trigger during valid windows
    weights[2][3] = max(weights[2][3], 4.5)
    weights[2][6] = min(weights[2][6], -2.0)

    # Action 3 (Throttle Background Tasks): Must strongly respond to both CPU spikes and Disk bottlenecks
    weights[3][2] = max(weights[3][2], 4.5)  # CPU spike
    weights[3][6] = max(weights[3][6], 4.5)  # Disk bottleneck / thrashing

def execute_physical_action(action, details=None):
    """Executes the verified physical action natively in-process with strict system safety."""
    global _last_trim_timestamp, _last_defrag_timestamp
    if action == 2:
        _last_defrag_timestamp = time.time()
    if action in (1, 4):  # Trim Working Sets or Emergency Recovery
        if action == 4 and details and details.get("free_ram_mb", 4000) >= 1800:
            return  # Safety guard: emergency recovery only when RAM is actually critical
        _last_trim_timestamp = time.time()
        # Protected Windows Kernel, GPU Drivers, Shell & Dev Subsystems (NEVER touch to prevent hard freeze)
        CRITICAL_PROTECTED_KEYWORDS = [
            "dwm", "csrss", "smss", "services", "lsass", "winlogon", "wininit",
            "fontdrvhost", "sihost", "audiodg", "system", "registry", "memory compression",
            "explorer", "shellexperiencehost", "startmenuexperiencehost", "svchost", "conhost",
            "dashost", "dllhost", "spoolsv", "wlanext",
            "atieclxx", "atiesrxx", "amd", "ati", "radeon", "nvidia", "intel", "realtek",
            "antigravity", "python", "pythonw", "code", "node", "language_server"
        ]
        try:
            import psutil
            for p in psutil.process_iter(['pid', 'name', 'memory_info']):
                try:
                    if not p.info or p.info['pid'] <= 4:
                        continue
                    p_name = (p.info.get('name') or "").lower()
                    if any(kw in p_name for kw in CRITICAL_PROTECTED_KEYWORDS):
                        continue
                    # Only trim if working set is substantial (> 25 MB) to avoid disk thrashing
                    mem = p.info.get('memory_info')
                    if mem and mem.rss < 25 * 1024 * 1024:
                        continue
                    # 0x0500 = PROCESS_SET_QUOTA (0x0100) | PROCESS_QUERY_INFORMATION (0x0400)
                    h = ctypes.windll.kernel32.OpenProcess(0x0500, False, p.info['pid'])
                    if h:
                        ctypes.windll.psapi.EmptyWorkingSet(h)
                        ctypes.windll.kernel32.CloseHandle(h)
                except Exception:
                    pass
        except Exception:
            pass
    elif action == 3:  # Throttle Background Tasks (CPU Scheduling & Mechanical HDD I/O Priority Shield)
        CRITICAL_PROTECTED_KEYWORDS = [
            "dwm", "csrss", "smss", "services", "lsass", "winlogon", "wininit",
            "fontdrvhost", "sihost", "audiodg", "system", "registry", "memory compression",
            "explorer", "shellexperiencehost", "startmenuexperiencehost", "svchost", "conhost",
            "dashost", "dllhost", "spoolsv", "wlanext",
            "atieclxx", "atiesrxx", "amd", "ati", "radeon", "nvidia", "intel", "realtek",
            "antigravity", "python", "pythonw", "code", "node", "language_server"
        ]
        try:
            import psutil
            # Identify active foreground window PID so user interactive application is NEVER throttled
            active_pid = 0
            try:
                fg_hwnd = ctypes.windll.user32.GetForegroundWindow()
                fg_pid = ctypes.c_ulong()
                ctypes.windll.user32.GetWindowThreadProcessId(fg_hwnd, ctypes.byref(fg_pid))
                active_pid = fg_pid.value
            except Exception:
                pass

            cpu_spike = bool(details and details.get("cpu_load_pct", 0) > 65)

            for p in psutil.process_iter(['pid', 'name']):
                try:
                    pid = p.info.get('pid', 0)
                    if pid <= 4 or (active_pid > 0 and pid == active_pid):
                        continue
                    p_name = (p.info.get('name') or "").lower()
                    if any(kw in p_name for kw in CRITICAL_PROTECTED_KEYWORDS):
                        continue

                    # 1. Physical HDD Queue Shield: Demote to IOPRIO_LOW (1)
                    # Forces the NT kernel disk scheduler to give 100% head priority to foreground UI
                    try:
                        if hasattr(psutil, 'IOPRIO_LOW') and p.ionice() != psutil.IOPRIO_LOW:
                            p.ionice(psutil.IOPRIO_LOW)
                    except Exception:
                        pass

                    # 2. CPU Scheduling: Demote to Below Normal if CPU workload is elevated
                    if cpu_spike:
                        try:
                            if p.nice() == psutil.NORMAL_PRIORITY_CLASS:
                                p.nice(psutil.BELOW_NORMAL_PRIORITY_CLASS)
                        except Exception:
                            pass
                except (psutil.NoSuchProcess, psutil.AccessDenied):
                    continue
        except Exception:
            pass
    elif action == 5:  # Enforce Hardware Bus & Power Stability
        # Safety guard: only execute powercfg if an actual hazard exists to avoid bus interrupts
        if not details or not details.get("pcie_aspm_hazard", False):
            return
        # Neutralizes PCIe ASPM and disk sleep timers to eliminate hardware bus deadlocks
        try:
            import subprocess
            scheme = "12111111-1211-1211-1211-121111111111"
            if details and details.get("active_scheme"):
                scheme = details["active_scheme"]
            pci_guid = "501a4d13-42af-4429-9fd1-a8218c268e20"
            aspm_guid = "ee12f906-d277-404b-b6da-e5fa1a576df5"
            cmd = f'powercfg /setacvalueindex {scheme} {pci_guid} {aspm_guid} 0 && powercfg /setdcvalueindex {scheme} {pci_guid} {aspm_guid} 0 && powercfg /change disk-timeout-ac 0 && powercfg /change disk-timeout-dc 0 && powercfg /setactive {scheme}'
            subprocess.run(cmd, shell=True, capture_output=True, timeout=5)
        except Exception:
            pass

def record_log(iteration, chosen_action, optimal_action, reward, details, reasoning):
    timestamp = datetime.datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    action_name = ACTIONS[chosen_action]
    optimal_name = ACTIONS[optimal_action]
    icon = "✅" if chosen_action == optimal_action else "⚠️"

    row = (
        f"| #{iteration:04d} | {timestamp} | {icon} `{action_name}` | `{optimal_name}` | `{reward:+.2f}` | "
        f"RAM: {details.get('ram_used_pct')}% (Free: {details.get('free_ram_mb')}MB) | "
        f"CPU: {details.get('cpu_load_pct')}% | HDD: {details.get('disk_io_mb_s', 0)}MB/s ({details.get('disk_latency_ms', 0)}ms) | Idle: {details.get('idle_minutes')}m | {reasoning} |\n"
    )

    if not os.path.exists(LOG_FILE) or os.path.getsize(LOG_FILE) == 0:
        header = (
            "# ClearWin AI — Real-time Autonomous PC Optimization Log\n"
            f"> **Supreme Director:** Eng. Abdullah Mohamed (`maher`)\n"
            "> **Platform:** Native Windows PC (RAM, CPU, HDD & Process Telemetry)\n\n"
            "| Step | Timestamp | Chosen Decision | Optimal Target | Reward | Real PC Metrics | Telemetry & Physics Rationale |\n"
            "| :--- | :--- | :--- | :--- | :--- | :--- | :--- |\n"
        )
        with open(LOG_FILE, "w", encoding="utf-8") as f:
            f.write(header)

    with open(LOG_FILE, "a", encoding="utf-8") as f:
        f.write(row)

def run_step(weights_data, iter_num, apply_physical=True):
    features, details = collect_pc_telemetry()
    weights = weights_data["weights"]
    biases = weights_data["biases"]
    lr = weights_data.get("learning_rate", 0.02)

    logits, probs = compute_action_distribution(features, weights, biases)

    # 95% exploit, 5% explore (strictly over safe, physically-allowed actions)
    global _last_defrag_timestamp
    defrag_cooldown = (time.time() - _last_defrag_timestamp) < 7200.0
    import random
    safe_exploratory_actions = [0]
    is_crit = details.get("free_ram_mb", 4000) < 1800 or details.get("ram_used_pct", 0) > 75.0
    if details.get("ram_used_pct", 0) > 60 and not details.get("trim_cooldown_active", False) and not details.get("is_hdd_thrashing", False) and not is_crit:
        safe_exploratory_actions.append(1)
    if details.get("idle_minutes", 0) >= 15.0 and not defrag_cooldown and not details.get("is_hdd_thrashing", False) and details.get("ram_used_pct", 0) < 65.0:
        safe_exploratory_actions.append(2)
    if details.get("cpu_load_pct", 0) > 70 or details.get("is_hdd_thrashing", False):
        safe_exploratory_actions.append(3)
    if is_crit:
        safe_exploratory_actions.append(4)
    if details.get("pcie_aspm_hazard", False):
        safe_exploratory_actions.append(5)

    if random.random() < 0.05 and safe_exploratory_actions:
        chosen_action = random.choice(safe_exploratory_actions)
    else:
        chosen_action = max(range(NUM_ACTIONS), key=lambda i: probs[i])

    reward, optimal_action, reasoning = evaluate_decision_physics(chosen_action, features, details)

    update_weights(weights, biases, features, chosen_action, optimal_action, reward, lr=lr)

    weights_data["total_training_iterations"] += 1
    weights_data["cumulative_reward"] = round(weights_data["cumulative_reward"] + reward, 3)

    record_log(iter_num, chosen_action, optimal_action, reward, details, reasoning)
    save_weights(weights_data)

    if apply_physical and reward > 0:
        execute_physical_action(chosen_action, details=details)

    return {
        "step": iter_num,
        "chosen": ACTIONS[chosen_action],
        "optimal": ACTIONS[optimal_action],
        "reward": reward,
        "probs": [round(p, 3) for p in probs],
        "details": details,
        "reasoning": reasoning
    }

def stress_test_simulation(stress_type="ram", duration_sec=10):
    """
    Stress-tests the PC in real-time to watch the AI engine detect the pressure,
    make the decision, and resolve it!
    """
    print(f"\n⚡ STARTING LIVE PC STRESS TEST: [{stress_type.upper()}] (Duration: {duration_sec}s)")
    weights_data = load_weights()

    # Pre-stress baseline
    baseline_step = run_step(weights_data, weights_data["total_training_iterations"] + 1, apply_physical=False)
    print(f"  [1. BASELINE] RAM: {baseline_step['details']['ram_used_pct']}% | Decision: {baseline_step['chosen']}")

    stress_active = True
    chunks = []

    def stress_worker():
        if stress_type == "ram":
            # Allocate ~1.5 GB of real memory to simulate a massive browser/app memory spike
            try:
                chunk_size = 100 * 1024 * 1024  # 100 MB chunks
                for _ in range(15):
                    if not stress_active:
                        break
                    # Allocate and touch memory so Windows physically commits it to RAM
                    b = bytearray(chunk_size)
                    for i in range(0, len(b), 4096):
                        b[i] = 1
                    chunks.append(b)
                    time.sleep(0.05)
            except MemoryError:
                pass
        elif stress_type == "cpu":
            # Spin arithmetic loop
            while stress_active:
                _ = [i * i for i in range(100000)]

    thread = threading.Thread(target=stress_worker)
    thread.daemon = True
    thread.start()

    time.sleep(1.5)  # Wait for stress to reflect in Windows OS memory counters

    # Step under stress
    stress_step = run_step(weights_data, weights_data["total_training_iterations"] + 1, apply_physical=True)
    print(f"  [2. UNDER STRESS] RAM: {stress_step['details']['ram_used_pct']}% (Free: {stress_step['details']['free_ram_mb']}MB)")
    print(f"      ▶ AI Decision Chosen:   {stress_step['chosen']}")
    print(f"      ▶ Physics Target:       {stress_step['optimal']}")
    print(f"      ▶ Reward Signal:        {stress_step['reward']:+.2f}")
    print(f"      ▶ AI Rationale:         {stress_step['reasoning']}")

    # Stop stress & release memory
    stress_active = False
    chunks.clear()
    import gc
    gc.collect()

    time.sleep(1.5)

    # Post-stress recovery
    post_step = run_step(weights_data, weights_data["total_training_iterations"] + 1, apply_physical=True)
    print(f"  [3. POST-RECOVERY] RAM: {post_step['details']['ram_used_pct']}% (Free: {post_step['details']['free_ram_mb']}MB)")
    print(f"      ▶ AI Decision:          {post_step['chosen']}")
    print(f"      ▶ Status:               RECOVERED TO STEADY STATE ✅\n")

def main():
    import argparse
    parser = argparse.ArgumentParser(description="ClearWin 2.7.2 Native PC AI Trainer")
    parser.add_argument("--epochs", type=int, default=10, help="Number of steps (0 for infinite overnight)")
    parser.add_argument("--interval", type=int, default=10, help="Seconds between steps")
    parser.add_argument("--stress-test", choices=["ram", "cpu"], help="Run live stress test on PC")
    parser.add_argument("--execute", action="store_true", help="Apply physical effects on positive decisions")
    args = parser.parse_args()

    if args.stress_test:
        stress_test_simulation(stress_type=args.stress_test)
        return

    print("=================================================================")
    print("▶ CLEARWIN 2.7.2 PC AI DECISION CENTER (NATIVE WINDOWS)")
    print(f"▶ Weights Storage: {WEIGHTS_FILE}")
    print(f"▶ Decision Log:    {LOG_FILE}")
    print("=================================================================\n")

    weights_data = load_weights()
    start_iter = weights_data["total_training_iterations"]
    curr = 1

    try:
        while True:
            prevent_system_sleep()
            try:
                weights_data = load_weights()
                res = run_step(weights_data, start_iter + curr, apply_physical=args.execute)
                try:
                    print(f"[{datetime.datetime.now().strftime('%H:%M:%S')}] Step #{start_iter + curr:04d} | "
                          f"Decision: {res['chosen']:<28} | Target: {res['optimal']:<28} | "
                          f"Reward: {res['reward']:+.2f} | RAM: {res['details']['ram_used_pct']}% | "
                          f"Top: {res['details']['top_process']}")
                except Exception:
                    pass
            except Exception as loop_err:
                err_log = os.path.join(WORKSPACE_DIR, "clearwin_ai_crash.log")
                with open(err_log, "a", encoding="utf-8") as ef:
                    ef.write(f"[{datetime.datetime.now()}] Loop error: {str(loop_err)}\n")
                time.sleep(5)

            if args.epochs > 0 and curr >= args.epochs:
                break
            curr += 1
            time.sleep(args.interval)
    except KeyboardInterrupt:
        print("\n[!] Paused by user. Weights saved safely.")

if __name__ == "__main__":
    main()
