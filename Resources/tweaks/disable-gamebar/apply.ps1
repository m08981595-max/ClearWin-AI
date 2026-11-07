$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

$path1 = if (Get-Command "Get-TweakRegistryPath" -ErrorAction SilentlyContinue) { Get-TweakRegistryPath "HKCU:\Software\Microsoft\Windows\CurrentVersion\GameDVR" } else { "HKCU:\Software\Microsoft\Windows\CurrentVersion\GameDVR" }
$path2 = if (Get-Command "Get-TweakRegistryPath" -ErrorAction SilentlyContinue) { Get-TweakRegistryPath "HKCU:\System\GameConfigStore" } else { "HKCU:\System\GameConfigStore" }

$targets = @(
    @{ Path = $path1; Name = "AppCaptureEnabled" },
    @{ Path = $path2; Name = "GameDVR_Enabled" }
)
if (Get-Command "Save-RegistryState" -ErrorAction SilentlyContinue) {
    Save-RegistryState -TweakName "disable-gamebar" -Targets $targets
}
foreach ($t in $targets) {
    if (-not (Test-Path $t.Path)) { New-Item -Path $t.Path -Force -ErrorAction SilentlyContinue | Out-Null }
    Set-ItemProperty -Path $t.Path -Name $t.Name -Type DWord -Value 0 -Force -ErrorAction SilentlyContinue
}
Write-Host "Xbox Game Bar disabled via Registry without app deletion. Pre-tweak state captured."
