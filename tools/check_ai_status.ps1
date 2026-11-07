# tools/check_ai_status.ps1
$ErrorActionPreference = "Continue"

$agentDir = Join-Path $env:LOCALAPPDATA "ClearWin\agent"
$logPath = Join-Path $env:LOCALAPPDATA "ClearWin\logs\clearwin.log"

$proc = Get-Process -Name ClearWin -ErrorAction SilentlyContinue | Select-Object -First 1

$agentFiles = @()
if (Test-Path $agentDir) {
    Get-ChildItem -Path $agentDir | ForEach-Object {
        $agentFiles += [PSCustomObject]@{
            Name = $_.Name
            SizeBytes = $_.Length
            LastModified = $_.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")
        }
    }
}

$recentErrors = @()
$recentEvents = @()
if (Test-Path $logPath) {
    $tailLines = Get-Content -Path $logPath -Tail 200 | ForEach-Object { $_.ToString() }
    foreach ($line in $tailLines) {
        if ($line -like '*[ERROR]*') {
            $recentErrors += $line
        } elseif ($line -like '*[INFO]*' -and ($line -like '*AI*' -or $line -like '*Memory*' -or $line -like '*Stabilizer*' -or $line -like '*Analyzer*')) {
            $recentEvents += $line
        }
    }
}

$status = [PSCustomObject]@{
    CurrentTime = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss K')
    IsRunning = ($null -ne $proc)
    Pid = if ($proc) { $proc.Id } else { 0 }
    WorkingSetMB = if ($proc) { [math]::Round($proc.WorkingSet64 / 1MB, 2) } else { 0 }
    Threads = if ($proc) { $proc.Threads.Count } else { 0 }
    Responding = if ($proc) { $proc.Responding } else { $false }
    AgentFiles = $agentFiles
    RecentErrorCount = $recentErrors.Count
    RecentErrors = @($recentErrors | Select-Object -Last 5)
    RecentAiEvents = @($recentEvents | Select-Object -Last 5)
}

$status | ConvertTo-Json -Depth 3
