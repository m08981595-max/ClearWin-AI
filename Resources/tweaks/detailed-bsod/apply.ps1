$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

$rawDir = "HKLM:\SYSTEM\CurrentControlSet\Control\CrashControl"
$dir = if (Get-Command "Get-TweakRegistryPath" -ErrorAction SilentlyContinue) { Get-TweakRegistryPath $rawDir } else { $rawDir }

$targets = @(
    @{ Path = $dir; Name = "DisplayParameters" }
)

if (Get-Command "Save-RegistryState" -ErrorAction SilentlyContinue) {
    Save-RegistryState -TweakName "detailed-bsod" -Targets $targets
}

if (-not (Test-Path $dir)) { New-Item -Path $dir -Force | Out-Null }
Set-ItemProperty -Path $dir -Name "DisplayParameters" -Value 1 -Type DWord -Force
Write-Host "Applied detailed-bsod successfully. Pre-tweak state captured for exact rollback."
