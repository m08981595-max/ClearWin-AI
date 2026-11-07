$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

$p1 = if (Get-Command "Get-TweakRegistryPath" -ErrorAction SilentlyContinue) { Get-TweakRegistryPath "HKLM:\Software\Microsoft\PolicyManager\default\WiFi\AllowWiFiHotSpotReporting" } else { "HKLM:\Software\Microsoft\PolicyManager\default\WiFi\AllowWiFiHotSpotReporting" }
$p2 = if (Get-Command "Get-TweakRegistryPath" -ErrorAction SilentlyContinue) { Get-TweakRegistryPath "HKLM:\Software\Microsoft\PolicyManager\default\WiFi\AllowAutoConnectToWiFiSenseHotspots" } else { "HKLM:\Software\Microsoft\PolicyManager\default\WiFi\AllowAutoConnectToWiFiSenseHotspots" }

$targets = @(
    @{ Path = $p1; Name = "Value" },
    @{ Path = $p2; Name = "Value" }
)
if (Get-Command "Save-RegistryState" -ErrorAction SilentlyContinue) {
    Save-RegistryState -TweakName "disable-wifi-sense" -Targets $targets
}
foreach ($t in $targets) {
    if (-not (Test-Path $t.Path)) { New-Item -Path $t.Path -Force -ErrorAction SilentlyContinue | Out-Null }
    Set-ItemProperty -Path $t.Path -Name $t.Name -Type DWord -Value 0 -Force -ErrorAction SilentlyContinue
}
Write-Host "Wi-Fi Sense disabled. Pre-tweak state captured."
