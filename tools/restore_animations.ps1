Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public class WinEffects {
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref int pvParam, uint fWinIni);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);

    [StructLayout(LayoutKind.Sequential)]
    public struct ANIMATIONINFO {
        public uint cbSize;
        public int iMinAnimate;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref ANIMATIONINFO pvParam, uint fWinIni);
}
"@

Write-Host "Restoring Windows Animation Registry Settings..." -ForegroundColor Cyan

# 1. MenuShowDelay back to 400ms (Windows default)
Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "MenuShowDelay" -Value "400"

# 2. Window Minimize/Maximize animations back to 1
Set-ItemProperty -Path "HKCU:\Control Panel\Desktop\WindowMetrics" -Name "MinAnimate" -Value "1"

# 3. Taskbar Animations & visual flags
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" -Name "TaskbarAnimations" -Value 1 -Type DWord
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" -Name "ListviewAlphaSelect" -Value 1 -Type DWord
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" -Name "ListviewShadow" -Value 1 -Type DWord
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" -Name "IconsOnly" -Value 0 -Type DWord
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" -Name "ShowInfoTip" -Value 1 -Type DWord

# 4. VisualFXSetting: 1 = Best Appearance / Default
if (-not (Test-Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects")) {
    New-Item -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects" -Force | Out-Null
}
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects" -Name "VisualFXSetting" -Value 1 -Type DWord

# 5. UserPreferencesMask with full animation bits enabled
# Default Windows appearance mask
$mask = [byte[]](0x9E, 0x3E, 0x07, 0x80, 0x12, 0x00, 0x00, 0x00)
Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "UserPreferencesMask" -Value $mask -Type Binary

# 6. DWM settings
if (Test-Path "HKCU:\Software\Microsoft\Windows\DWM") {
    Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\DWM" -Name "EnableAeroPeek" -Value 1 -Type DWord -ErrorAction SilentlyContinue
    Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\DWM" -Name "AlwaysHibernateThumbnails" -Value 0 -Type DWord -ErrorAction SilentlyContinue
}

# 7. DragFullWindows & FontSmoothing
Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "DragFullWindows" -Value "1"
Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "FontSmoothing" -Value "2"

# 8. ClearWin internal AnimSpeed = 1 (Normal)
Set-ItemProperty -Path "HKCU:\Software\ClearWin" -Name "AnimSpeed" -Value 1 -Type DWord -ErrorAction SilentlyContinue

Write-Host "Broadcasting SystemParametersInfo live..." -ForegroundColor Cyan
$SPIF = 0x03 # SPIF_UPDATEINIFILE | SPIF_SENDCHANGE

# AnimationInfo (MinAnimate)
$anim = New-Object WinEffects+ANIMATIONINFO
$anim.cbSize = 8
$anim.iMinAnimate = 1
[WinEffects]::SystemParametersInfo(0x0049, 8, [ref]$anim, $SPIF) | Out-Null

# SPI_SETCLIENTAREAANIMATION (Windows 11 / 10 Animation effects toggle)
[WinEffects]::SystemParametersInfo(0x1043, 0, [IntPtr]1, $SPIF) | Out-Null

# SPI_SETMENUANIMATION
[WinEffects]::SystemParametersInfo(0x1003, 0, [IntPtr]1, $SPIF) | Out-Null

# SPI_SETTOOLTIPANIMATION
[WinEffects]::SystemParametersInfo(0x1017, 0, [IntPtr]1, $SPIF) | Out-Null

# SPI_SETCOMBOBOXANIMATION
[WinEffects]::SystemParametersInfo(0x1005, 0, [IntPtr]1, $SPIF) | Out-Null

# SPI_SETLISTBOXSMOOTHSCROLLING
[WinEffects]::SystemParametersInfo(0x1007, 0, [IntPtr]1, $SPIF) | Out-Null

# SPI_SETMENUFADE
[WinEffects]::SystemParametersInfo(0x1013, 0, [IntPtr]1, $SPIF) | Out-Null

# SPI_SETSELECTIONFADE
[WinEffects]::SystemParametersInfo(0x1015, 0, [IntPtr]1, $SPIF) | Out-Null

# SPI_SETTOOLTIPFADE
[WinEffects]::SystemParametersInfo(0x1019, 0, [IntPtr]1, $SPIF) | Out-Null

# SPI_SETCURSORSHADOW
[WinEffects]::SystemParametersInfo(0x101B, 0, [IntPtr]1, $SPIF) | Out-Null

# SPI_SETUIEFFECTS
[WinEffects]::SystemParametersInfo(0x103F, 0, [IntPtr]1, $SPIF) | Out-Null

# SPI_SETDRAGFULLWINDOWS
[WinEffects]::SystemParametersInfo(0x0025, 1, [IntPtr]0, $SPIF) | Out-Null

# SPI_SETDROPSHADOW
[WinEffects]::SystemParametersInfo(0x1025, 0, [IntPtr]1, $SPIF) | Out-Null

Write-Host "Restarting Windows Explorer to immediately refresh visual shell..." -ForegroundColor Yellow
Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
if (-not (Get-Process -Name explorer -ErrorAction SilentlyContinue)) {
    Start-Process explorer.exe
}

Write-Host "Done! All Windows visual effects and animations restored to default smoothly." -ForegroundColor Green
