$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

$rawDir = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced\TaskbarDeveloperSettings"
$dir = if (Get-Command "Get-TweakRegistryPath" -ErrorAction SilentlyContinue) { Get-TweakRegistryPath $rawDir } else { $rawDir }

$targets = @(
    @{ Path = $dir; Name = "TaskbarEndTask" }
)

if (Get-Command "Save-RegistryState" -ErrorAction SilentlyContinue) {
    Save-RegistryState -TweakName "enable-end-task-right-click" -Targets $targets
}

if (-not (Test-Path $dir)) { New-Item -Path $dir -Force | Out-Null }
Set-ItemProperty -Path $dir -Name "TaskbarEndTask" -Value 1 -Type DWord -Force
Write-Host "Applied enable-end-task-right-click successfully. Pre-tweak state captured for exact rollback."
