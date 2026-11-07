Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

public class WinEffectsOpt {
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

Write-Host "Applying Balanced High-Performance + Smooth Motion Profile..." -ForegroundColor Cyan

# 1. Keep Window animations smooth (Prevents the stutter / choppy minimize-maximize feel)
Set-ItemProperty -Path "HKCU:\Control Panel\Desktop\WindowMetrics" -Name "MinAnimate" -Value "1"

# 2. Optimal MenuShowDelay: 150ms (Super fast & snappy, but eliminates 0ms abrupt glitches)
Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "MenuShowDelay" -Value "150"

# 3. Taskbar & Explorer: Smooth transitions enabled, heavy shadows disabled
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" -Name "TaskbarAnimations" -Value 1 -Type DWord
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" -Name "IconsOnly" -Value 0 -Type DWord
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" -Name "ListviewAlphaSelect" -Value 0 -Type DWord # Disabled (saves GPU compositing)
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" -Name "ListviewShadow" -Value 0 -Type DWord      # Disabled (saves text shadow draw calls)
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced" -Name "ShowInfoTip" -Value 1 -Type DWord

# 4. VisualFXSetting: 3 = Custom (Optimized balance)
if (-not (Test-Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects")) {
    New-Item -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects" -Force | Out-Null
}
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects" -Name "VisualFXSetting" -Value 3 -Type DWord

# 5. UserPreferencesMask:
# Keeps Window Animation, Controls Animation, Smooth Scrolling, Drag Full Windows, Font Smoothing
# Strips heavy GPU shadows (Cursor shadow, Window drop shadows, Menu fade delays)
# 90 12 03 80 10 00 00 00 with smoothness enabled
$balancedMask = [byte[]](0x90, 0x12, 0x03, 0x80, 0x10, 0x00, 0x00, 0x00)
Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "UserPreferencesMask" -Value $balancedMask -Type Binary

# 6. Smooth text & window dragging (Essential for visual smoothness without overhead)
Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "DragFullWindows" -Value "1"
Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "FontSmoothing" -Value "2"

# 7. Disable heavy DWM layers (Aero Peek hover lag)
if (Test-Path "HKCU:\Software\Microsoft\Windows\DWM") {
    Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\DWM" -Name "EnableAeroPeek" -Value 0 -Type DWord -ErrorAction SilentlyContinue
    Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\DWM" -Name "AlwaysHibernateThumbnails" -Value 0 -Type DWord -ErrorAction SilentlyContinue
}

Write-Host "Broadcasting SystemParametersInfo live..." -ForegroundColor Cyan
$SPIF = 0x03 # SPIF_UPDATEINIFILE | SPIF_SENDCHANGE

# AnimationInfo (MinAnimate) = ON (Smooth window transition)
$anim = New-Object WinEffectsOpt+ANIMATIONINFO
$anim.cbSize = 8
$anim.iMinAnimate = 1
[WinEffectsOpt]::SystemParametersInfo(0x0049, 8, [ref]$anim, $SPIF) | Out-Null

# Client Area Animation = ON (Controls & Windows animate smoothly)
[WinEffectsOpt]::SystemParametersInfo(0x1043, 0, [IntPtr]1, $SPIF) | Out-Null

# Listbox Smooth Scrolling = ON
[WinEffectsOpt]::SystemParametersInfo(0x1007, 0, [IntPtr]1, $SPIF) | Out-Null

# Menu Animation = OFF (Instant snappy opening, no fade delay)
[WinEffectsOpt]::SystemParametersInfo(0x1003, 0, [IntPtr]0, $SPIF) | Out-Null

# Menu Fade = OFF (Instant zero-delay response)
[WinEffectsOpt]::SystemParametersInfo(0x1013, 0, [IntPtr]0, $SPIF) | Out-Null

# Tooltip Animation / Fade = OFF (Instant tooltip without GPU overlay lag)
[WinEffectsOpt]::SystemParametersInfo(0x1017, 0, [IntPtr]0, $SPIF) | Out-Null
[WinEffectsOpt]::SystemParametersInfo(0x1019, 0, [IntPtr]0, $SPIF) | Out-Null

# Selection Fade = OFF (Snappy selection)
[WinEffectsOpt]::SystemParametersInfo(0x1015, 0, [IntPtr]0, $SPIF) | Out-Null

# Drop Shadow under windows = OFF (Reduces DWM GPU composition memory & redraws)
[WinEffectsOpt]::SystemParametersInfo(0x1025, 0, [IntPtr]0, $SPIF) | Out-Null

# Cursor Shadow = OFF (Removes cursor rendering pass overhead)
[WinEffectsOpt]::SystemParametersInfo(0x101B, 0, [IntPtr]0, $SPIF) | Out-Null

# Drag Full Windows = ON (Window contents visible while dragging)
[WinEffectsOpt]::SystemParametersInfo(0x0025, 1, [IntPtr]0, $SPIF) | Out-Null

Write-Host "Balanced performance profile applied successfully!" -ForegroundColor Green
