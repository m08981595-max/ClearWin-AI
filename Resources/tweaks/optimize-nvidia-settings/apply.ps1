$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

$rawBase = 'HKLM:\SYSTEM\CurrentControlSet\Services\nvlddmkg'
$base = if (Get-Command "Get-TweakRegistryPath" -ErrorAction SilentlyContinue) { Get-TweakRegistryPath $rawBase } else { $rawBase }
if (-not (Test-Path $base) -and -not $env:CLEARWIN_TEST_REG_PREFIX) {
    Write-Host "NVIDIA driver service (nvlddmkg) not found; no NVIDIA GPU present. Skipping."
    exit 0
}

$rawCustom = "HKLM:\SYSTEM\CurrentControlSet\Services\nvlddmkg\Parameters\Custom"
$custom = if (Get-Command "Get-TweakRegistryPath" -ErrorAction SilentlyContinue) { Get-TweakRegistryPath $rawCustom } else { $rawCustom }

$targets = @(
    @{ Path = $custom; Name = "PowerMizerEnable" }
)
if (Get-Command "Save-RegistryState" -ErrorAction SilentlyContinue) {
    Save-RegistryState -TweakName "optimize-nvidia-settings" -Targets $targets
}

if (-not (Test-Path $custom)) { New-Item -Path $custom -Force | Out-Null }
Set-ItemProperty -Path $custom -Name "PowerMizerEnable" -Value 1 -Type DWord -Force
Write-Host "NVIDIA PowerMizer performance mode applied. Pre-tweak state captured."
