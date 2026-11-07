# tools/track_launch_extended.ps1
$exe = "D:\open code\optomize\ClearWin-2.7.2-GlassUI-V21-AI-EXECUTION-CENTER\clearwin_v19_scrollbar_control\bin\ClearWin.exe"
$bin = "D:\open code\optomize\ClearWin-2.7.2-GlassUI-V21-AI-EXECUTION-CENTER\clearwin_v19_scrollbar_control\bin"

Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public class WinCheckEx {
    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    public static List<string> FindWindows(uint targetPid) {
        var list = new List<string>();
        EnumWindows((hWnd, lParam) => {
            uint pid;
            GetWindowThreadProcessId(hWnd, out pid);
            if (pid == targetPid) {
                var sb = new StringBuilder(256);
                GetWindowText(hWnd, sb, 256);
                bool vis = IsWindowVisible(hWnd);
                RECT r;
                GetWindowRect(hWnd, out r);
                list.Add(string.Format("HWND: 0x{0:X}, Visible: {1}, Title: '{2}', Rect: [{3},{4} - {5},{6}]", 
                    hWnd.ToInt64(), vis, sb.ToString(), r.Left, r.Top, r.Right, r.Bottom));
            }
            return true;
        }, IntPtr.Zero);
        return list;
    }
}
"@

Write-Host "Killing previous instances..."
Get-Process -Name ClearWin -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 600

Write-Host "Launching ClearWin via Shell.Application (runas)..."
$shell = New-Object -ComObject "Shell.Application"
$shell.ShellExecute($exe, "", $bin, "runas", 1)

Start-Sleep -Milliseconds 2000
$p = Get-Process -Name ClearWin -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $p) {
    Write-Warning "ClearWin not detected yet, waiting another 2s..."
    Start-Sleep -Seconds 2
    $p = Get-Process -Name ClearWin -ErrorAction SilentlyContinue | Select-Object -First 1
}

if (-not $p) {
    Write-Error "ClearWin failed to launch."
    exit 1
}

Write-Host "Tracking ClearWin PID $($p.Id)..."
for ($i = 0; $i -lt 50; $i++) {
    Start-Sleep -Milliseconds 500
    if ($p.HasExited) {
        Write-Host ">>> Process EXITED at step $($i) with ExitCode $($p.ExitCode)!"
        break
    }
    $p.Refresh()
    if ($i % 5 -eq 0) {
        Write-Host "[$($i)] Responding: $($p.Responding), Handle: $($p.MainWindowHandle), WS: $([math]::Round($p.WorkingSet64/1MB,1)) MB"
        $wins = [WinCheckEx]::FindWindows($p.Id)
        foreach ($w in $wins) {
            Write-Host "      $w"
        }
    }
}

if (-not $p.HasExited) {
    Write-Host "SUCCESS: ClearWin is actively running and stable! PID: $($p.Id)"
}
