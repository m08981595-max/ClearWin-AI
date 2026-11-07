# tools/inspect_load_reduction.ps1
$ErrorActionPreference = "Continue"

$modeFile = Join-Path $env:LOCALAPPDATA "ClearWin\agent\mode.dat"
$logFile = Join-Path $env:LOCALAPPDATA "ClearWin\logs\clearwin.log"
$agentDir = Join-Path $env:LOCALAPPDATA "ClearWin\agent"

Write-Host "=== 1. Agent Mode in mode.dat ==="
if (Test-Path $modeFile) {
    $modeContent = Get-Content $modeFile -Raw
    Write-Host "mode.dat content: '$modeContent'"
} else {
    Write-Host "mode.dat not found"
}

Write-Host "`n=== 2. Check for Optimization Actions in Logs ==="
if (Test-Path $logFile) {
    $allLines = Get-Content $logFile
    $optLines = $allLines | Where-Object { 
        $_ -like "*Action*" -or 
        $_ -like "*Optim*" -or 
        $_ -like "*Trim*" -or 
        $_ -like "*Clean*" -or 
        $_ -like "*Boost*" -or
        $_ -like "*Applied*" -or
        $_ -like "*Reduced*" -or
        $_ -like "*Freed*"
    }
    Write-Host "Total matching optimization lines: $($optLines.Count)"
    Write-Host "Sample of last 20 matching lines:"
    $optLines | Select-Object -Last 20 | ForEach-Object { Write-Host "  $_" }
}

Write-Host "`n=== 3. Check stats.dat and usage.dat ==="
$statsFile = Join-Path $agentDir "stats.dat"
if (Test-Path $statsFile) {
    Write-Host "stats.dat content:"
    Get-Content $statsFile | ForEach-Object { Write-Host "  $_" }
}
