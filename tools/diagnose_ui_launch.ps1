# tools/diagnose_ui_launch.ps1
$exe = "D:\open code\optomize\ClearWin-2.7.2-GlassUI-V21-AI-EXECUTION-CENTER\clearwin_v19_scrollbar_control\bin\ClearWin.exe"
$bin = "D:\open code\optomize\ClearWin-2.7.2-GlassUI-V21-AI-EXECUTION-CENTER\clearwin_v19_scrollbar_control\bin"

Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public class WinCheck {
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

Get-Process -Name ClearWin -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

Write-Host "Starting ClearWin..."
$proc = Start-Process -FilePath $exe -WorkingDirectory $bin -PassThru
Write-Host "Started PID: $($proc.Id)"

for ($i = 1; $i -le 6; $i++) {
    Start-Sleep -Seconds 2
    $p = Get-Process -Id $proc.Id -ErrorAction SilentlyContinue
    if (-not $p) {
        $p = Get-Process -Name ClearWin -ErrorAction SilentlyContinue | Select-Object -First 1
    }
    if ($p) {
        Write-Host "[$i] PID $($p.Id) is running (WorkingSet: $([math]::Round($p.WorkingSet64/1MB,1)) MB, Responding: $($p.Responding), MainWindowHandle: $($p.MainWindowHandle))"
        $wins = [WinCheck]::FindWindows($p.Id)
        Write-Host "    Windows count: $($wins.Count)"
        foreach ($w in $wins) {
            Write-Host "      $w"
        }
    } else {
        Write-Warning "[$i] ClearWin is not running!"
    }
}
