Write-Host "=========================================================="
Write-Host "ClearWin - Two-Phase Disk & Component Store Cleanup"
Write-Host "Classification: IRREVERSIBLE OPERATION"
Write-Host "=========================================================="
Write-Host "Target Operations:"
Write-Host "  Phase 1: Temporary files in User Profile: $env:TEMP"
Write-Host "           Temporary files in Windows System: $env:WINDIR\Temp"
Write-Host "  Phase 2: Windows Component Store (WinSxS superseded updates via DISM)"
Write-Host ""
Write-Host "CRITICAL NOTICE: Deleted temporary files and superseded WinSxS components"
Write-Host "are PERMANENTLY PURGED from the drive. Automatic rollback is IMPOSSIBLE."
Write-Host "=========================================================="

if ($env:CLEARWIN_CLEANUP_OPT_IN -ne 'YES') {
    Write-Warning "BLOCKED: run-disk-cleanup permanently deletes files from disk."
    Write-Warning "To proceed, explicit opt-in confirmation is required (CLEARWIN_CLEANUP_OPT_IN='YES')."
    exit 2
}

# Phase 1: Clean Temporary Files safely
Write-Host "`n[Phase 1/2] Cleaning temporary files (User Confirmed)..."
$tempPaths = @($env:TEMP, "$env:WINDIR\Temp")
$freedCount = 0
foreach ($tp in $tempPaths) {
    if (Test-Path $tp) {
        Write-Host "  Scanning: $tp"
        Get-ChildItem -Path $tp -Force -ErrorAction SilentlyContinue | ForEach-Object {
            try {
                Remove-Item -Path $_.FullName -Recurse -Force -ErrorAction Stop
                $freedCount++
            } catch {
                # In-use files are skipped safely
            }
        }
    }
}
Write-Host "  Purged $freedCount temporary item(s). In-use files were safely preserved."

# Phase 2: Clean Component Store (WinSxS)
Write-Host "`n[Phase 2/2] Running DISM Component Store cleanup (WinSxS)..."
try {
    Start-Process -FilePath "Dism.exe" -ArgumentList "/online /Cleanup-Image /StartComponentCleanup /Quiet /NoRestart" -Wait -NoNewWindow
    Write-Host "  DISM Component Store cleanup completed."
} catch {
    Write-Warning "DISM cleanup encountered an issue: $($_.Exception.Message)"
}

Write-Host ""
Write-Host "=========================================================="
Write-Host "IRREVERSIBLE CLEANUP COMPLETE:"
Write-Host "Disk space permanently reclaimed. No backup was created because"
Write-Host "temporary and superseded files cannot and should not be restored."
Write-Host "=========================================================="
exit 0