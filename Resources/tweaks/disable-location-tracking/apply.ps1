$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

$p1 = if (Get-Command "Get-TweakRegistryPath" -ErrorAction SilentlyContinue) { Get-TweakRegistryPath "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection" } else { "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection" }
$p2 = if (Get-Command "Get-TweakRegistryPath" -ErrorAction SilentlyContinue) { Get-TweakRegistryPath "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location" } else { "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location" }
$p3 = if (Get-Command "Get-TweakRegistryPath" -ErrorAction SilentlyContinue) { Get-TweakRegistryPath "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Diagnostics\DiagTrack" } else { "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Diagnostics\DiagTrack" }

$targets = @(
    @{ Path = $p1; Name = "Allow" },
    @{ Path = $p2; Name = "SensorPermissionState" },
    @{ Path = $p3; Name = "Status" }
)
if (Get-Command "Save-RegistryState" -ErrorAction SilentlyContinue) {
    Save-RegistryState -TweakName "disable-location-tracking" -Targets $targets
}

foreach ($t in $targets) {
    if (-not (Test-Path $t.Path)) { New-Item -Path $t.Path -Force -ErrorAction SilentlyContinue | Out-Null }
}
Set-ItemProperty -Path $targets[0].Path -Name "Allow" -Value "Deny" -Type String -Force -ErrorAction SilentlyContinue
Set-ItemProperty -Path $targets[1].Path -Name "SensorPermissionState" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
Set-ItemProperty -Path $targets[2].Path -Name "Status" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
Write-Host "Location tracking disabled. Pre-tweak state captured."
