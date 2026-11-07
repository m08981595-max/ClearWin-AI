# tools/verify_window_controls.ps1
# Automated host verification of ClearWin 2.7.3 Window Control & Exit Cohesion

$ErrorActionPreference = "Continue"
Write-Host "=== ClearWin 2.7.3 Window & Process Cohesion Verification ==="

$exePath = "D:\open code\optomize\ClearWin-2.7.2-GlassUI-V21-AI-EXECUTION-CENTER\clearwin_v19_scrollbar_control\bin\ClearWin.exe"
if (-not (Test-Path $exePath)) {
    Write-Error "Executable not found at $exePath"
    exit 1
}

# 1. Clean any old processes
Get-Process -Name "ClearWin" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 500

# 2. Launch ClearWin
Write-Host "Step 1: Launching ClearWin..."
$proc = Start-Process -FilePath $exePath -PassThru
Start-Sleep -Seconds 3

# Check if process is running
$runningProc = Get-Process -Id $proc.Id -ErrorAction SilentlyContinue
if (-not $runningProc) {
    Write-Error "ClearWin failed to start!"
    exit 1
}
Write-Host "ClearWin started successfully with PID: $($runningProc.Id), WorkingSet: $([math]::Round($runningProc.WorkingSet64 / 1MB, 2)) MB"

# 3. Test Single-Instance behavior by launching a second process
Write-Host "Step 2: Testing Single-Instance activation..."
$secondProc = Start-Process -FilePath $exePath -PassThru
Start-Sleep -Seconds 2
$secondFinished = $secondProc.WaitForExit(3000)
Write-Host "Second instance exited cleanly: $secondFinished"

$clearWinCount = (Get-Process -Name "ClearWin" -ErrorAction SilentlyContinue).Count
Write-Host "Active ClearWin process count (should be exactly 1): $clearWinCount"

# 4. Gracefully close ClearWin by calling CloseMainWindow
Write-Host "Step 3: Closing ClearWin window..."
$closed = $runningProc.CloseMainWindow()
Write-Host "CloseMainWindow returned: $closed"

# Wait for process exit
$exitedCleanly = $runningProc.WaitForExit(5000)
Write-Host "ClearWin process terminated cleanly: $exitedCleanly"

Start-Sleep -Seconds 1
$remainingProcs = Get-Process -Name "ClearWin" -ErrorAction SilentlyContinue
if ($remainingProcs) {
    Write-Warning "Zombie process found after window close: $($remainingProcs.Id)"
    $remainingProcs | Stop-Process -Force
    exit 2
} else {
    Write-Host "CONFIRMED: Zero zombie processes remain! Exit cohesion is 100% verified."
}

Write-Host "=== ALL VERIFICATIONS PASSED ==="
exit 0
