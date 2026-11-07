# tools/run_monitor_daemon.ps1
# Long-running supervisor daemon for ClearWin 2.7.3

$ErrorActionPreference = "Continue"
$projectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($projectRoot)) { $projectRoot = (Get-Location).Path }

$binDir = Join-Path $projectRoot "bin"
$exePath = Join-Path $binDir "ClearWin.exe"
$logFile = Join-Path $projectRoot "tools\daemon_monitor_log.json"
$historyLog = Join-Path $projectRoot "tools\daemon_history.log"

Write-Host "=== ClearWin 2.7.3 Supervisor Daemon Initializing ==="
Add-Content -Path $historyLog -Value "[$(Get-Date -Format 'o')] Supervisor started. Target executable: $exePath"

# Clean any existing instances first
Get-Process -Name "ClearWin" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 600

# Launch initial process
Write-Host "Launching initial ClearWin instance..."
$proc = Start-Process -FilePath $exePath -WorkingDirectory $binDir -PassThru
Start-Sleep -Seconds 3

Add-Content -Path $historyLog -Value "[$(Get-Date -Format 'o')] Launched ClearWin PID $($proc.Id)"

$iteration = 0
while ($true) {
    Start-Sleep -Seconds 10
    $iteration++
    
    # Check if process is still running
    if (-not $proc -or $proc.HasExited) {
        $exitCode = if ($proc) { $proc.ExitCode } else { -1 }
        $restartMsg = "[$(Get-Date -Format 'o')] ClearWin exited with code $exitCode. Relaunching..."
        Write-Warning $restartMsg
        Add-Content -Path $historyLog -Value $restartMsg
        
        $proc = Start-Process -FilePath $exePath -WorkingDirectory $binDir -PassThru
        Start-Sleep -Seconds 3
        Add-Content -Path $historyLog -Value "[$(Get-Date -Format 'o')] Relaunched ClearWin under PID $($proc.Id)"
    }
    
    # Telemetry sampling
    try {
        $p = Get-Process -Id $proc.Id -ErrorAction SilentlyContinue
        if ($p) {
            $telemetry = [PSCustomObject]@{
                Timestamp = (Get-Date -Format 'o')
                Iteration = $iteration
                Pid = $p.Id
                WorkingSetMB = [math]::Round($p.WorkingSet64 / 1MB, 2)
                Threads = $p.Threads.Count
                Responding = $p.Responding
                CpuSeconds = [math]::Round($p.TotalProcessorTime.TotalSeconds, 2)
                IsActive = $true
            }
            $telemetry | ConvertTo-Json -Compress | Set-Content -Path $logFile -Force
        }
    } catch {
        # ignore sampling transient errors
    }
}
