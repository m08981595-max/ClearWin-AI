Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public class WinFinder {
    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left, Top, Right, Bottom;
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    public static List<string> FindWindowsForPid(uint targetPid) {
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
$cw = Get-Process -Name ClearWin -ErrorAction SilentlyContinue | Select-Object -First 1
if ($cw) {
    $hwnds = [WinFinder]::FindWindowsForPid($cw.Id)
    Write-Host "Windows found for PID $($cw.Id): $($hwnds.Count)"
    $hwnds | ForEach-Object { Write-Host "  $_" }
} else {
    Write-Warning "ClearWin is not running."
}
