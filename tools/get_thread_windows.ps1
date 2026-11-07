Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public class ThreadWinFinder {
    [DllImport("user32.dll")]
    private static extern bool EnumThreadWindows(int dwThreadId, EnumThreadDelegate lpfn, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    private delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);

    public static List<string> GetWindowsForThread(int threadId) {
        var list = new List<string>();
        EnumThreadWindows(threadId, (hWnd, lParam) => {
            var sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, 256);
            bool vis = IsWindowVisible(hWnd);
            list.Add(string.Format("HWND: 0x{0:X}, Visible: {1}, Title: '{2}'", hWnd.ToInt64(), vis, sb.ToString()));
            return true;
        }, IntPtr.Zero);
        return list;
    }
}
"@

$cw = Get-Process -Name ClearWin -ErrorAction SilentlyContinue | Select-Object -First 1
if ($cw) {
    Write-Host "Checking threads for PID $($cw.Id)..."
    foreach ($t in $cw.Threads) {
        $wins = [ThreadWinFinder]::GetWindowsForThread($t.Id)
        if ($wins.Count -gt 0) {
            Write-Host "Thread $($t.Id) has $($wins.Count) windows:"
            $wins | ForEach-Object { Write-Host "   $_" }
        }
    }
}
