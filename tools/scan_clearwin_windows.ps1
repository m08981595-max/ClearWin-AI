# tools/scan_clearwin_windows.ps1
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public class WinScan {
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

    public static void Scan() {
        EnumWindows((hWnd, lParam) => {
            var sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, 256);
            string title = sb.ToString();
            if (title.IndexOf("ClearWin", StringComparison.OrdinalIgnoreCase) >= 0) {
                uint pid;
                GetWindowThreadProcessId(hWnd, out pid);
                bool vis = IsWindowVisible(hWnd);
                RECT r;
                GetWindowRect(hWnd, out r);
                Console.WriteLine(string.Format("PID: {0}, HWND: 0x{1:X}, Visible: {2}, Title: '{3}', Rect: [{4},{5} - {6},{7}]",
                    pid, hWnd.ToInt64(), vis, title, r.Left, r.Top, r.Right, r.Bottom));
            }
            return true;
        }, IntPtr.Zero);
    }
}
"@

[WinScan]::Scan()
