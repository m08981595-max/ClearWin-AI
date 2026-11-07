param([uint32]$TargetPid = 10216)
Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public class PidWinCheck {
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

    public static void Check(uint pid) {
        Console.WriteLine("Scanning windows for PID: " + pid);
        EnumWindows((hWnd, lParam) => {
            uint p;
            GetWindowThreadProcessId(hWnd, out p);
            if (p == pid) {
                var sb = new StringBuilder(256);
                GetWindowText(hWnd, sb, 256);
                bool vis = IsWindowVisible(hWnd);
                RECT r;
                GetWindowRect(hWnd, out r);
                Console.WriteLine(string.Format("HWND: 0x{0:X}, Visible: {1}, Title: '{2}', Rect: [{3},{4} - {5},{6}]",
                    hWnd.ToInt64(), vis, sb.ToString(), r.Left, r.Top, r.Right, r.Bottom));
            }
            return true;
        }, IntPtr.Zero);
    }
}
"@

[PidWinCheck]::Check($TargetPid)
