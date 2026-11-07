# ClearWin 1-Hour Autonomous Training and Active Learning Loop
param(
    [int]$DurationMinutes = 60,
    [int]$IntervalSeconds = 30
)

$projectRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($projectRoot)) { $projectRoot = (Get-Location).Path }
$binExe = Join-Path $projectRoot "bin\ClearWin.exe"
$logPath = Join-Path $projectRoot "tools\training_1hour.log"
$jsonPath = Join-Path $projectRoot "tools\training_1hour_log.json"
$agentDir = Join-Path $env:LOCALAPPDATA "ClearWin\agent"

function Log-Message([string]$msg) {
    $timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    $line = "[$timestamp] $msg"
    Write-Host $line -ForegroundColor Cyan
    Add-Content -Path $logPath -Value $line -Encoding UTF8
}

"=== ClearWin 1-Hour Active AI Training & Load Reduction Session ===" | Out-File -FilePath $logPath -Encoding UTF8
Log-Message "Starting 1-Hour session: Duration = $DurationMinutes min, Interval = $IntervalSeconds sec"

# Step 1: Ensure ClearWin is running
$proc = Get-Process -Name ClearWin -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) {
    Log-Message "Launching ClearWin.exe..."
    $proc = Start-Process -FilePath $binExe -PassThru
    Start-Sleep -Seconds 4
}
Log-Message "ClearWin active with PID = $($proc.Id)"

$startTime = Get-Date
$endTime = $startTime.AddMinutes($DurationMinutes)
$iteration = 0
$records = @()

# Capture initial RAM
$osInit = Get-CimInstance Win32_OperatingSystem
$totalGB = [math]::Round($osInit.TotalVisibleMemorySize / 1MB, 2)
$initFreeGB = [math]::Round($osInit.FreePhysicalMemory / 1MB, 2)
$initUsedGB = [math]::Round(($osInit.TotalVisibleMemorySize - $osInit.FreePhysicalMemory) / 1MB, 2)
Log-Message "Initial System State: Total RAM = $totalGB GB | Used = $initUsedGB GB | Free = $initFreeGB GB"

while ((Get-Date) -lt $endTime) {
    $iteration++
    Start-Sleep -Seconds $IntervalSeconds
    
    # Check ClearWin process health
    $cw = Get-Process -Name ClearWin -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $cw) {
        Log-Message "ClearWin not detected. Relaunching..."
        $cw = Start-Process -FilePath $binExe -PassThru
        Start-Sleep -Seconds 3
    }
    
    $cwMemMB = if ($cw) { [math]::Round($cw.WorkingSet64 / 1MB, 1) } else { 0 }
    
    # Read system OS metrics
    $os = Get-CimInstance Win32_OperatingSystem
    $freeGB = [math]::Round($os.FreePhysicalMemory / 1MB, 2)
    $usedGB = [math]::Round(($os.TotalVisibleMemorySize - $os.FreePhysicalMemory) / 1MB, 2)
    $usedPct = [math]::Round(($usedGB / $totalGB) * 100, 1)
    
    # Check model file sizes
    $mlSize = if (Test-Path "$agentDir\ml_nn_v2.dat") { (Get-Item "$agentDir\ml_nn_v2.dat").Length } else { 0 }
    $rlSize = if (Test-Path "$agentDir\rl2.dat") { (Get-Item "$agentDir\rl2.dat").Length } else { 0 }
    
    $elapsed = [math]::Round(((Get-Date) - $startTime).TotalMinutes, 1)
    $remaining = [math]::Round(($endTime - (Get-Date)).TotalMinutes, 1)
    
    $record = [PSCustomObject]@{
        Iteration = $iteration
        ElapsedMinutes = $elapsed
        RemainingMinutes = [math]::Max(0, $remaining)
        ClearWinRAM_MB = $cwMemMB
        SystemUsedRAM_GB = $usedGB
        SystemFreeRAM_GB = $freeGB
        SystemUsedPct = $usedPct
        NeuralNetBytes = $mlSize
        RL2Bytes = $rlSize
        Timestamp = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    }
    $records += $record
    
    # Save JSON log periodically
    $records | ConvertTo-Json -Depth 3 | Set-Content -Path $jsonPath -Encoding UTF8
    
    # Log milestone every 10 iterations (~5 min) or final
    if ($iteration % 10 -eq 0 -or (Get-Date) -ge $endTime) {
        Log-Message "Milestone [Iteration $iteration | Elapsed: $elapsed min / Remaining: $remaining min]: System RAM Used: $usedGB GB ($usedPct%) | ClearWin Footprint: $cwMemMB MB | Neural Net Model: $mlSize bytes"
    }
}

Log-Message "=== 1-Hour Active AI Training & Load Reduction Session Completed Successfully ==="
$finalSummary = [PSCustomObject]@{
    TotalIterations = $iteration
    TotalDurationMinutes = [math]::Round(((Get-Date) - $startTime).TotalMinutes, 2)
    InitialFreeRAM_GB = $initFreeGB
    FinalFreeRAM_GB = $freeGB
    RAMDeltaGB = [math]::Round($freeGB - $initFreeGB, 2)
    FinalNeuralNetBytes = $mlSize
    FinalRLBytes = $rlSize
    Status = "COMPLETED"
}
$finalSummary | ConvertTo-Json | Add-Content -Path $jsonPath -Encoding UTF8
Log-Message "Final Summary: Initial Free RAM: $initFreeGB GB -> Final Free RAM: $freeGB GB | Delta: $([math]::Round($freeGB - $initFreeGB, 2)) GB"
