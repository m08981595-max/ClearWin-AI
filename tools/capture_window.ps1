Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Stop-Process -Name ClearWin -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

$proc = Start-Process -FilePath 'bin\ClearWin.exe' -PassThru
Write-Host "Started ClearWin PID: $($proc.Id)"
Start-Sleep -Seconds 4

$allProc = Get-Process -Name ClearWin -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 }
if (-not $allProc) {
    Write-Host "No window handle found for ClearWin"
    exit 1
}

$target = $allProc[0]
$hWnd = $target.MainWindowHandle
Write-Host "Process PID: $($target.Id), HWND: 0x$($hWnd.ToString('X')), Title: '$($target.MainWindowTitle)'"

$typeDefinition = @"
using System;
using System.Runtime.InteropServices;
public class WinCaptureHelper {
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT {
        public int Left, Top, Right, Bottom;
    }
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);
}
"@

Add-Type -TypeDefinition $typeDefinition -ErrorAction SilentlyContinue

$rect = New-Object WinCaptureHelper+RECT
[WinCaptureHelper]::GetWindowRect($hWnd, [ref]$rect)
$w = $rect.Right - $rect.Left
$h = $rect.Bottom - $rect.Top
Write-Host "Window Rect: Left=$($rect.Left), Top=$($rect.Top), Width=$w, Height=$h"

if ($w -gt 50 -and $h -gt 50) {
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $gfx = [System.Drawing.Graphics]::FromImage($bmp)
    $hdc = $gfx.GetHdc()
    [WinCaptureHelper]::PrintWindow($hWnd, $hdc, 2)
    $gfx.ReleaseHdc($hdc)
    $gfx.Dispose()

    $outPath = Join-Path $pwd "tools\test_capture.png"
    $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "Saved screenshot to: $outPath"

    $checkBmp = [System.Drawing.Bitmap]::FromFile($outPath)
    # Sample various points
    $pCenter = $checkBmp.GetPixel([int]($w * 0.5), [int]($h * 0.5))
    $pSidebar = $checkBmp.GetPixel([int]($w * 0.1), [int]($h * 0.5))
    $pTopBar = $checkBmp.GetPixel([int]($w * 0.5), 18)
    Write-Host "TopBar pixel: R=$($pTopBar.R), G=$($pTopBar.G), B=$($pTopBar.B)"
    Write-Host "Sidebar pixel: R=$($pSidebar.R), G=$($pSidebar.G), B=$($pSidebar.B)"
    Write-Host "Center pixel: R=$($pCenter.R), G=$($pCenter.G), B=$($pCenter.B)"
    $checkBmp.Dispose()
}
