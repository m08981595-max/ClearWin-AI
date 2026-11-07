$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

if (Get-Command "Save-HibernationState" -ErrorAction SilentlyContinue) {
    Save-HibernationState -TweakName "disable-hibernation"
}
if ($env:CLEARWIN_TEST_REG_PREFIX -and ($env:CLEARWIN_TEST_MODE -eq "1" -or $env:CLEARWIN_TEST_MODE -eq "true")) {
    $targetKey = Get-TweakRegistryPath "HKLM:\SYSTEM\CurrentControlSet\Control\Power"
    if (-not (Test-Path $targetKey)) { New-Item -Path $targetKey -ItemType Directory -Force | Out-Null }
    Set-ItemProperty -Path $targetKey -Name "HibernateEnabled" -Value 0 -Type DWord -Force
} else {
    powercfg.exe /hibernate off
}
Write-Host "Hibernation disabled. Pre-tweak hibernation state captured for exact rollback."
