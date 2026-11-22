#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
ClearWin 2.7.2 GlassUI V21 — Sync WebUI & Single Desktop Shortcut Engine
Supreme Director: Eng. Abdullah Mohamed (maher)
"""

import os
import sys
import glob
import shutil
import ctypes
import subprocess
import win32com.client

print("Executing Sync & Desktop Cleanup...")

SRC_DIR = r"d:\open code\optomize\ClearWin-2.7.2-GlassUI-V21-AI-EXECUTION-CENTER\clearwin_v19_scrollbar_control"
SRC_HTML = os.path.join(SRC_DIR, "Resources", "WebUI", "index.html")
SRC_ICO = os.path.join(SRC_DIR, "Resources", "app.ico")

PROG_DIR = r"C:\Program Files\ClearWin"
REPAIRED_ENGINE_DIR = r"D:\open code\optomize\ClearWin-2.7.2-GlassUI-V21-AI-EXECUTION-CENTER\ClearWin_AI_v2.7.7_Repaired\clearwin_v19_scrollbar_control\_ClearWin_Engine\bin"

# 1. Terminate running ClearWin.exe to free handles and cache
subprocess.run('taskkill /F /IM "ClearWin.exe"', shell=True, capture_output=True)

# 1.5 Update ClearWin.exe with embedded new PE Icon
patched_exe = os.path.join(SRC_DIR, "ClearWin_test.exe")
dest_exe = os.path.join(PROG_DIR, "ClearWin.exe")
dest_exe_bak = os.path.join(PROG_DIR, "ClearWin.exe.v21_pre_pe_icon.bak")
if os.path.exists(patched_exe):
    try:
        if os.path.exists(dest_exe) and not os.path.exists(dest_exe_bak):
            shutil.copy2(dest_exe, dest_exe_bak)
        shutil.copy2(patched_exe, dest_exe)
        print(f"Updated {dest_exe} with new embedded PE icon ({os.path.getsize(dest_exe)} bytes)")
    except Exception as e:
        print(f"Error copying patched exe: {e}")

# 2. Copy the new index.html & icons to all execution engines
targets = [
    PROG_DIR,
    REPAIRED_ENGINE_DIR,
    r"D:\open code\optomize\ClearWin-2.7.2-GlassUI-V21-AI-EXECUTION-CENTER\ClearWin_AI_v2.7.7_Repaired\clearwin_v19_scrollbar_control",
    os.path.expanduser("~") + r"\AppData\Local\ClearWin"
]

for t in targets:
    if os.path.exists(t):
        webui_dir = os.path.join(t, "Resources", "WebUI")
        os.makedirs(webui_dir, exist_ok=True)
        dest_html = os.path.join(webui_dir, "index.html")
        try:
            shutil.copy2(SRC_HTML, dest_html)
            print(f"Copied index.html ({os.path.getsize(SRC_HTML)} bytes) to {dest_html}")
        except Exception as e:
            print(f"Error copying HTML to {dest_html}: {e}")

        # Copy icons
        try:
            shutil.copy2(SRC_ICO, os.path.join(t, "app.ico"))
            shutil.copy2(SRC_ICO, os.path.join(t, "clearwin_v21.ico"))
            shutil.copy2(SRC_ICO, os.path.join(webui_dir, "app.ico"))
            shutil.copy2(SRC_ICO, os.path.join(webui_dir, "clearwin_v21.ico"))
            print(f"Copied icons to {t}")
        except Exception as e:
            print(f"Error copying icons to {t}: {e}")

# 3. Purge WebView2 Caches (EBWebView)
for base in [PROG_DIR, REPAIRED_ENGINE_DIR, os.path.expanduser("~") + r"\AppData\Local\ClearWin"]:
    if os.path.exists(base):
        for eb in glob.glob(os.path.join(base, "**", "EBWebView"), recursive=True):
            try:
                shutil.rmtree(eb, ignore_errors=True)
                print(f"Purged WebView2 cache: {eb}")
            except Exception as e:
                print(f"Error purging cache {eb}: {e}")

# 4. Clean Desktop: REMOVE DUPLICATE SHORTCUTS
pub_desktop = r"C:\Users\Public\Desktop"
user_desktop = os.path.expanduser("~") + r"\Desktop"

# Remove duplicate ClearWin.lnk and Uninstall ClearWin.lnk from Public Desktop
for doomed in ["ClearWin.lnk", "Uninstall ClearWin.lnk"]:
    dp = os.path.join(pub_desktop, doomed)
    if os.path.exists(dp):
        try:
            os.remove(dp)
            print(f"REMOVED duplicate public shortcut: {dp}")
        except Exception as e:
            print(f"Error removing {dp}: {e}")

# Ensure ONLY ONE clean shortcut on User Desktop
user_lnk = os.path.join(user_desktop, "ClearWin.lnk")
shell = win32com.client.Dispatch("WScript.Shell")
sc = shell.CreateShortcut(user_lnk)
sc.TargetPath = os.path.join(PROG_DIR, "ClearWin.exe")
sc.WorkingDirectory = PROG_DIR
sc.IconLocation = os.path.join(PROG_DIR, "clearwin_v21.ico,0")
sc.Save()
print(f"Unified single desktop shortcut saved: {user_lnk}")
print(f"  Target: {sc.TargetPath}")
print(f"  IconLocation: {sc.IconLocation}")

# 5. Delete Old Icon Files
old_icons = [
    os.path.join(REPAIRED_ENGINE_DIR, "ClearWinIcon.ico"),
    os.path.join(PROG_DIR, "ClearWin.exe.v21_icon_backup"),
    os.path.join(SRC_DIR, "test_icon.exe")
]
for oi in old_icons:
    if os.path.exists(oi):
        try:
            os.remove(oi)
            print(f"DELETED old icon file: {oi}")
        except Exception as e:
            print(f"Error deleting {oi}: {e}")

# 6. Notify Windows Shell
ctypes.windll.shell32.SHChangeNotify(0x08000000, 0x1000, None, None) # SHCNE_ASSOCCHANGED | SHCNF_FLUSH

# 7. Relaunch ClearWin.exe from Program Files
subprocess.Popen([os.path.join(PROG_DIR, "ClearWin.exe")], cwd=PROG_DIR)
print("ClearWin.exe launched from official installation folder.")
