$ErrorActionPreference = 'Stop'

# "Undo" means restore Windows default behavior by removing the override.
& bcdedit.exe /deletevalue useplatformclock
if ($LASTEXITCODE -eq 0) {
    Write-Host "Platform clock override removed; Windows default behavior restored."
    exit 0
}
$config = (& bcdedit.exe /enum '{current}') 2>&1 | Out-String
if ($config -notmatch 'useplatformclock') {
    Write-Host "Windows is already using the default clock-source selection."
    exit 0
}
Write-Error "Failed to restore default clock-source selection."
exit 1
