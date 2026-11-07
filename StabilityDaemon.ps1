param(
    [string]$TargetProcessName = 'ClearWin',
    [string]$ReportPath = '',
    [int]$IntervalSeconds = 30
)

$ErrorActionPreference = 'SilentlyContinue'

if ([string]::IsNullOrWhiteSpace($ReportPath)) {
    $ReportPath = Join-Path $PSScriptRoot 'STABILITY_MONITOR_REPORT.md'
}

$headerLines = @(
    "# ClearWin AI v2.7.7 Stability and Performance Report",
    "",
    "> Autonomous Background Watchdog monitoring ClearWin health and performance in real-time.",
    "",
    "| Timestamp | Status | RAM | CPU | Threads | Handles | Responsive | WebView2 Processes |",
    "| :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: |"
)

if (-not (Test-Path -LiteralPath $ReportPath -PathType Leaf)) {
    $headerLines | Set-Content -LiteralPath $ReportPath -Encoding UTF8
}

Write-Host "Stability Daemon is running..."

while ($true) {
    $nowStr = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    $proc = Get-Process -Name $TargetProcessName -ErrorAction SilentlyContinue | Select-Object -First 1

    if ($null -ne $proc) {
        $ramMB = [Math]::Round($proc.WorkingSet64 / 1048576.0, 1)
        $threads = $proc.Threads.Count
        $handles = $proc.HandleCount
        $resp = if ($proc.Responding) { "Yes (Active)" } else { "No (Hung)" }
        $procId = $proc.Id

        # Count child processes
        $wv2Count = @(Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "*webview2*" -and $_.ParentProcessId -eq $procId }).Count

        # Sample CPU
        $c1 = $proc.TotalProcessorTime.TotalMilliseconds
        $t1 = [System.DateTime]::UtcNow
        Start-Sleep -Milliseconds 500
        $proc.Refresh()
        $c2 = $proc.TotalProcessorTime.TotalMilliseconds
        $t2 = [System.DateTime]::UtcNow
        $elapsedMs = ($t2 - $t1).TotalMilliseconds * [System.Environment]::ProcessorCount
        $cpuPct = if ($elapsedMs -gt 0) { [Math]::Max(0.0, [Math]::Min(100.0, [Math]::Round((($c2 - $c1) / $elapsedMs) * 100, 1))) } else { 0.0 }

        $row = [string]::Format("| {0} | Running (PID: {1}) | {2} MB | {3}% | {4} | {5} | {6} | {7} processes |", $nowStr, $procId, $ramMB, $cpuPct, $threads, $handles, $resp, $wv2Count)
        Add-Content -LiteralPath $ReportPath -Value $row -Encoding UTF8
    } else {
        $row = [string]::Format("| {0} | Stopped | -- | -- | -- | -- | No | 0 |", $nowStr)
        Add-Content -LiteralPath $ReportPath -Value $row -Encoding UTF8
    }

    Start-Sleep -Seconds ($IntervalSeconds - 1)
}
