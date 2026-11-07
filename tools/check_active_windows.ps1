Add-Type @"
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public class WinInspector {
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

    public static void InspectAllClearWin() {
        EnumWindows((hWnd, lParam) => {
            uint pid;
            GetWindowThreadProcessId(hWnd, out pid);
            var sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, 256);
            string title = sb.ToString();
            bool vis = IsWindowVisible(hWnd);
            RECT r;
            GetWindowRect(hWnd, out r);
            int w = r.Right - r.Left;
            int h = r.Bottom - r.Top;
            if (w > 200 && h > 200 && vis) {
                try {
                    var p = System.Diagnostics.Process.GetProcessById((int)pid);
                    if (p.ProcessName.IndexOf("ClearWin", StringComparison.OrdinalIgnoreCase) >= 0) {
                        Console.WriteLine(string.Format("FOUND CLEARWIN WINDOW! PID: {0}, HWND: 0x{1:X}, Title: '{2}', Visible: {3}, Rect: [{4},{5} - {6},{7}] ({8}x{9})",
                            pid, hWnd.ToInt64(), title, vis, r.Left, r.Top, r.Right, r.Bottom, w, h));
                    }
                } catch {}
            }
            return true;
        }, IntPtr.Zero);
    }
}
"@

[WinInspector]::InspectAllClearWin()
