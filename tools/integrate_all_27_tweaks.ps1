# ==============================================================================
# Script to integrate state-preserving backup into all remaining tweaks
# ==============================================================================

# 1. disable-background-ms-store-apps
$name = "disable-background-ms-store-apps"
$apply = @'
$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

$targets = @(
    @{ Path = "HKCU:\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications"; Name = "GlobalUserDisabled" }
)
if (Get-Command "Save-RegistryState" -ErrorAction SilentlyContinue) {
    Save-RegistryState -TweakName "disable-background-ms-store-apps" -Targets $targets
}
$path = $targets[0].Path
if (-not (Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
Set-ItemProperty -Path $path -Name "GlobalUserDisabled" -Type DWord -Value 1 -Force
Write-Host "Background apps access disabled. Pre-tweak state captured."
'@
$undo = @'
$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

if (Get-Command "Restore-RegistryState" -ErrorAction SilentlyContinue) {
    Restore-RegistryState -TweakName "disable-background-ms-store-apps"
} else {
    Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\BackgroundAccessApplications" -Name "GlobalUserDisabled" -Type DWord -Value 0 -Force
}
'@
Set-Content -Path "Resources\tweaks\$name\apply.ps1" -Value $apply -Encoding UTF8
Set-Content -Path "Resources\tweaks\$name\undo.ps1" -Value $undo -Encoding UTF8
Write-Host "Updated $name"

# 2. disable-copilot
$name = "disable-copilot"
$apply = @'
$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

$targets = @(
    @{ Path = "HKCU:\Software\Policies\Microsoft\Windows\WindowsCopilot"; Name = "TurnOffWindowsCopilot" },
    @{ Path = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot"; Name = "TurnOffWindowsCopilot" }
)
if (Get-Command "Save-RegistryState" -ErrorAction SilentlyContinue) {
    Save-RegistryState -TweakName "disable-copilot" -Targets $targets
}
foreach ($t in $targets) {
    if (-not (Test-Path $t.Path)) { New-Item -Path $t.Path -Force -ErrorAction SilentlyContinue | Out-Null }
    Set-ItemProperty -Path $t.Path -Name $t.Name -Type DWord -Value 1 -Force -ErrorAction SilentlyContinue
}
Write-Host "Windows Copilot disabled via system policy. Pre-tweak state captured."
'@
$undo = @'
$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

if (Get-Command "Restore-RegistryState" -ErrorAction SilentlyContinue) {
    Restore-RegistryState -TweakName "disable-copilot"
} else {
    Remove-ItemProperty -Path "HKCU:\Software\Policies\Microsoft\Windows\WindowsCopilot" -Name "TurnOffWindowsCopilot" -Force -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsCopilot" -Name "TurnOffWindowsCopilot" -Force -ErrorAction SilentlyContinue
}
'@
Set-Content -Path "Resources\tweaks\$name\apply.ps1" -Value $apply -Encoding UTF8
Set-Content -Path "Resources\tweaks\$name\undo.ps1" -Value $undo -Encoding UTF8
Write-Host "Updated $name"

# 3. disable-gamebar
$name = "disable-gamebar"
$apply = @'
$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

$targets = @(
    @{ Path = "HKCU:\Software\Microsoft\Windows\CurrentVersion\GameDVR"; Name = "AppCaptureEnabled" },
    @{ Path = "HKCU:\System\GameConfigStore"; Name = "GameDVR_Enabled" }
)
if (Get-Command "Save-RegistryState" -ErrorAction SilentlyContinue) {
    Save-RegistryState -TweakName "disable-gamebar" -Targets $targets
}
foreach ($t in $targets) {
    if (-not (Test-Path $t.Path)) { New-Item -Path $t.Path -Force -ErrorAction SilentlyContinue | Out-Null }
    Set-ItemProperty -Path $t.Path -Name $t.Name -Type DWord -Value 0 -Force -ErrorAction SilentlyContinue
}
Write-Host "Xbox Game Bar disabled via Registry without app deletion. Pre-tweak state captured."
'@
$undo = @'
$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

if (Get-Command "Restore-RegistryState" -ErrorAction SilentlyContinue) {
    Restore-RegistryState -TweakName "disable-gamebar"
} else {
    Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\GameDVR" -Name "AppCaptureEnabled" -Type DWord -Value 1 -Force -ErrorAction SilentlyContinue
    Set-ItemProperty -Path "HKCU:\System\GameConfigStore" -Name "GameDVR_Enabled" -Type DWord -Value 1 -Force -ErrorAction SilentlyContinue
}
'@
Set-Content -Path "Resources\tweaks\$name\apply.ps1" -Value $apply -Encoding UTF8
Set-Content -Path "Resources\tweaks\$name\undo.ps1" -Value $undo -Encoding UTF8
Write-Host "Updated $name"

# 4. disable-hibernation
$name = "disable-hibernation"
$apply = @'
$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

if (Get-Command "Save-HibernationState" -ErrorAction SilentlyContinue) {
    Save-HibernationState -TweakName "disable-hibernation"
}
powercfg.exe /hibernate off
Write-Host "Hibernation disabled. Pre-tweak hibernation state captured for exact rollback."
'@
$undo = @'
$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

if (Get-Command "Restore-HibernationState" -ErrorAction SilentlyContinue) {
    Restore-HibernationState -TweakName "disable-hibernation"
} else {
    powercfg.exe /hibernate on
}
'@
Set-Content -Path "Resources\tweaks\$name\apply.ps1" -Value $apply -Encoding UTF8
Set-Content -Path "Resources\tweaks\$name\undo.ps1" -Value $undo -Encoding UTF8
Write-Host "Updated $name"

# 5. disable-hpet
$name = "disable-hpet"
$apply = @'
$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Error "This tweak requires Administrator privileges."
    exit 1
}

if (Get-Command "Save-BcdeditSettingState" -ErrorAction SilentlyContinue) {
    Save-BcdeditSettingState -TweakName "disable-hpet" -SettingName "useplatformclock"
}

& bcdedit.exe /deletevalue useplatformclock 2>$null
Write-Host "Platform clock override removed; Windows selects default clock. Pre-tweak BCD state captured."
'@
$undo = @'
$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

if (Get-Command "Restore-BcdeditSettingState" -ErrorAction SilentlyContinue) {
    Restore-BcdeditSettingState -TweakName "disable-hpet"
} else {
    & bcdedit.exe /deletevalue useplatformclock 2>$null
    Write-Host "Platform clock override removed."
}
'@
Set-Content -Path "Resources\tweaks\$name\apply.ps1" -Value $apply -Encoding UTF8
Set-Content -Path "Resources\tweaks\$name\undo.ps1" -Value $undo -Encoding UTF8
Write-Host "Updated $name"

# 6. disable-location-tracking
$name = "disable-location-tracking"
$apply = @'
$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

$targets = @(
    @{ Path = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection"; Name = "Allow" },
    @{ Path = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location"; Name = "SensorPermissionState" },
    @{ Path = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Diagnostics\DiagTrack"; Name = "Status" }
)
if (Get-Command "Save-RegistryState" -ErrorAction SilentlyContinue) {
    Save-RegistryState -TweakName "disable-location-tracking" -Targets $targets
}

foreach ($t in $targets) {
    if (-not (Test-Path $t.Path)) { New-Item -Path $t.Path -Force -ErrorAction SilentlyContinue | Out-Null }
}
Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection" -Name "Allow" -Value "Deny" -Type String -Force -ErrorAction SilentlyContinue
Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location" -Name "SensorPermissionState" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Diagnostics\DiagTrack" -Name "Status" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
Write-Host "Location tracking disabled. Pre-tweak state captured."
'@
$undo = @'
$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

if (Get-Command "Restore-RegistryState" -ErrorAction SilentlyContinue) {
    Restore-RegistryState -TweakName "disable-location-tracking"
} else {
    Write-Warning "Cannot undo disable-location-tracking without backup state."
}
'@
Set-Content -Path "Resources\tweaks\$name\apply.ps1" -Value $apply -Encoding UTF8
Set-Content -Path "Resources\tweaks\$name\undo.ps1" -Value $undo -Encoding UTF8
Write-Host "Updated $name"

# 7. disable-lockscreen-tips
$name = "disable-lockscreen-tips"
$apply = @'
$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

$targets = @(
    @{ Path = "HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager"; Name = "RotatingLockScreenOverlayEnabled" },
    @{ Path = "HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager"; Name = "SubscribedContent-338387Enabled" }
)
if (Get-Command "Save-RegistryState" -ErrorAction SilentlyContinue) {
    Save-RegistryState -TweakName "disable-lockscreen-tips" -Targets $targets
}
$path = $targets[0].Path
if (-not (Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
Set-ItemProperty -Path $path -Name "RotatingLockScreenOverlayEnabled" -Value 0 -Type DWord -Force
Set-ItemProperty -Path $path -Name "SubscribedContent-338387Enabled" -Value 0 -Type DWord -Force
Write-Host "Lock screen tips disabled. Pre-tweak state captured."
'@
$undo = @'
$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

if (Get-Command "Restore-RegistryState" -ErrorAction SilentlyContinue) {
    Restore-RegistryState -TweakName "disable-lockscreen-tips"
} else {
    Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" -Name "RotatingLockScreenOverlayEnabled" -Type DWord -Value 1 -Force
    Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" -Name "SubscribedContent-338387Enabled" -Type DWord -Value 1 -Force
}
'@
Set-Content -Path "Resources\tweaks\$name\apply.ps1" -Value $apply -Encoding UTF8
Set-Content -Path "Resources\tweaks\$name\undo.ps1" -Value $undo -Encoding UTF8
Write-Host "Updated $name"

# 8. disable-mouse-acceleration
$name = "disable-mouse-acceleration"
$apply = @'
$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

$targets = @(
    @{ Path = "HKCU:\Control Panel\Mouse"; Name = "MouseSpeed" },
    @{ Path = "HKCU:\Control Panel\Mouse"; Name = "MouseThreshold1" },
    @{ Path = "HKCU:\Control Panel\Mouse"; Name = "MouseThreshold2" }
)
if (Get-Command "Save-RegistryState" -ErrorAction SilentlyContinue) {
    Save-RegistryState -TweakName "disable-mouse-acceleration" -Targets $targets
}
$path = "HKCU:\Control Panel\Mouse"
Set-ItemProperty -Path $path -Name "MouseSpeed" -Value "0" -Type String -Force
Set-ItemProperty -Path $path -Name "MouseThreshold1" -Value "0" -Type String -Force
Set-ItemProperty -Path $path -Name "MouseThreshold2" -Value "0" -Type String -Force
Write-Host "Mouse acceleration disabled (linear curve). Pre-tweak state captured."
'@
$undo = @'
$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

if (Get-Command "Restore-RegistryState" -ErrorAction SilentlyContinue) {
    Restore-RegistryState -TweakName "disable-mouse-acceleration"
} else {
    Set-ItemProperty -Path "HKCU:\Control Panel\Mouse" -Name "MouseSpeed" -Value "1" -Type String -Force
}
'@
Set-Content -Path "Resources\tweaks\$name\apply.ps1" -Value $apply -Encoding UTF8
Set-Content -Path "Resources\tweaks\$name\undo.ps1" -Value $undo -Encoding UTF8
Write-Host "Updated $name"

# 9. disable-wifi-sense
$name = "disable-wifi-sense"
$apply = @'
$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

$targets = @(
    @{ Path = "HKLM:\Software\Microsoft\PolicyManager\default\WiFi\AllowWiFiHotSpotReporting"; Name = "Value" },
    @{ Path = "HKLM:\Software\Microsoft\PolicyManager\default\WiFi\AllowAutoConnectToWiFiSenseHotspots"; Name = "Value" }
)
if (Get-Command "Save-RegistryState" -ErrorAction SilentlyContinue) {
    Save-RegistryState -TweakName "disable-wifi-sense" -Targets $targets
}
foreach ($t in $targets) {
    if (-not (Test-Path $t.Path)) { New-Item -Path $t.Path -Force -ErrorAction SilentlyContinue | Out-Null }
    Set-ItemProperty -Path $t.Path -Name $t.Name -Type DWord -Value 0 -Force -ErrorAction SilentlyContinue
}
Write-Host "Wi-Fi Sense disabled. Pre-tweak state captured."
'@
$undo = @'
$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

if (Get-Command "Restore-RegistryState" -ErrorAction SilentlyContinue) {
    Restore-RegistryState -TweakName "disable-wifi-sense"
} else {
    Write-Warning "Cannot undo disable-wifi-sense without backup state."
}
'@
Set-Content -Path "Resources\tweaks\$name\apply.ps1" -Value $apply -Encoding UTF8
Set-Content -Path "Resources\tweaks\$name\undo.ps1" -Value $undo -Encoding UTF8
Write-Host "Updated $name"

# 10. revert-context-menu
$name = "revert-context-menu"
$apply = @'
$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

$targets = @(
    @{ Path = "HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32"; Name = "" }
)
if (Get-Command "Save-RegistryState" -ErrorAction SilentlyContinue) {
    Save-RegistryState -TweakName "revert-context-menu" -Targets $targets
}
New-Item -Path "HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\InprocServer32" -Force -Value "" | Out-Null
Write-Host "Classic Windows 10 context menu enabled. Pre-tweak state captured."
'@
$undo = @'
$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

if (Get-Command "Restore-RegistryState" -ErrorAction SilentlyContinue) {
    Restore-RegistryState -TweakName "revert-context-menu"
} else {
    Remove-Item -Path "HKCU:\Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}" -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Host "Restored default Windows 11 context menu."
'@
Set-Content -Path "Resources\tweaks\$name\apply.ps1" -Value $apply -Encoding UTF8
Set-Content -Path "Resources\tweaks\$name\undo.ps1" -Value $undo -Encoding UTF8
Write-Host "Updated $name"

# 11. optimize-network-settings
$name = "optimize-network-settings"
$apply = @'
$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

if (Get-Command "Save-NetworkGlobalState" -ErrorAction SilentlyContinue) {
    Save-NetworkGlobalState -TweakName "optimize-network-settings"
}

Write-Host "Applying low-latency TCP network tweaks..."
netsh int tcp set global rss=enabled 2>$null
netsh int tcp set global ecncapability=enabled 2>$null
netsh int tcp set global timestamps=disabled 2>$null
netsh int tcp set global initialRto=2000 2>$null
netsh int tcp set global autotuninglevel=normal 2>$null
Write-Host "Network optimization applied. Pre-tweak TCP stack parameters captured."
'@
$undo = @'
$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

if (Get-Command "Restore-NetworkGlobalState" -ErrorAction SilentlyContinue) {
    Restore-NetworkGlobalState -TweakName "optimize-network-settings"
} else {
    netsh int tcp reset 2>$null
}
'@
Set-Content -Path "Resources\tweaks\$name\apply.ps1" -Value $apply -Encoding UTF8
Set-Content -Path "Resources\tweaks\$name\undo.ps1" -Value $undo -Encoding UTF8
Write-Host "Updated $name"

# 12. optimize-nvidia-settings
$name = "optimize-nvidia-settings"
$apply = @'
$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

$base = 'HKLM:\SYSTEM\CurrentControlSet\Services\nvlddmkg'
if (-not (Test-Path $base)) {
    Write-Host "NVIDIA driver service (nvlddmkg) not found; no NVIDIA GPU present. Skipping."
    exit 0
}

$targets = @(
    @{ Path = "HKLM:\SYSTEM\CurrentControlSet\Services\nvlddmkg\Parameters\Custom"; Name = "PowerMizerEnable" }
)
if (Get-Command "Save-RegistryState" -ErrorAction SilentlyContinue) {
    Save-RegistryState -TweakName "optimize-nvidia-settings" -Targets $targets
}

$custom = "HKLM:\SYSTEM\CurrentControlSet\Services\nvlddmkg\Parameters\Custom"
if (-not (Test-Path $custom)) { New-Item -Path $custom -Force | Out-Null }
Set-ItemProperty -Path $custom -Name "PowerMizerEnable" -Value 1 -Type DWord -Force
Write-Host "NVIDIA PowerMizer performance mode applied. Pre-tweak state captured."
'@
$undo = @'
$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

if (Get-Command "Restore-RegistryState" -ErrorAction SilentlyContinue) {
    Restore-RegistryState -TweakName "optimize-nvidia-settings"
} else {
    Write-Warning "Cannot undo optimize-nvidia-settings without backup state."
}
'@
Set-Content -Path "Resources\tweaks\$name\apply.ps1" -Value $apply -Encoding UTF8
Set-Content -Path "Resources\tweaks\$name\undo.ps1" -Value $undo -Encoding UTF8
Write-Host "Updated $name"

Write-Host "All tweaks integrated with TweakBackupEngine successfully."
