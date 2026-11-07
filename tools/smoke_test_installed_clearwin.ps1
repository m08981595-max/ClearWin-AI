# Smoke Test Installed ClearWin.exe
$installedExe = "D:\ClearWin_Local_Test\Install\ClearWin.exe"
$resultJson = "tools\smoke_test_installed_result.json"

Write-Host "Testing installed binary: $installedExe"
if (-not (Test-Path $installedExe)) {
    Write-Error "Executable not found at $installedExe"
    exit 1
}

try {
    Write-Host "Launching ClearWin.exe..."
    $proc = Start-Process -FilePath $installedExe -PassThru
    Write-Host "ClearWin started with PID: $($proc.Id)"

    # Wait 8 seconds for WebView2 initialization and UI stabilization
    Start-Sleep -Seconds 8

    # Refresh process state
    $proc.Refresh()
    $hasExited = $proc.HasExited
    $exitCode = if ($hasExited) { $proc.ExitCode } else { $null }
    $wsMB = if (-not $hasExited) { [math]::Round($proc.WorkingSet64 / 1MB, 2) } else { 0 }
    $mainWindowTitle = if (-not $hasExited) { $proc.MainWindowTitle } else { "" }

    Write-Host "Process HasExited: $hasExited"
    Write-Host "Process WorkingSet: $wsMB MB"
    Write-Host "MainWindowTitle: '$mainWindowTitle'"

    # Check child/associated processes (e.g. msedgewebview2.exe)
    $wvProcesses = Get-Process -Name "msedgewebview2" -ErrorAction SilentlyContinue
    Write-Host "Active WebView2 renderer/utility processes: $($wvProcesses.Count)"

    # Cleanly terminate the test process
    if (-not $hasExited) {
        Write-Host "Closing ClearWin.exe cleanly..."
        $proc.CloseMainWindow() | Out-Null
        Start-Sleep -Seconds 3
        if (-not $proc.HasExited) {
            Write-Host "Process did not close on CloseMainWindow, stopping process..."
            $proc.Kill()
        }
        Write-Host "ClearWin.exe closed successfully."
    }

    $smokeResult = [PSCustomObject]@{
        ExecutablePath      = $installedExe
        ProcessId           = $proc.Id
        StartedSuccessfully = ($proc.Id -gt 0)
        StableAfter8Sec     = (-not $hasExited)
        WorkingSetMB        = $wsMB
        MainWindowTitle     = $mainWindowTitle
        WebView2Processes   = $wvProcesses.Count
        Timestamp           = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    }
    $smokeResult | ConvertTo-Json -Depth 4 | Out-File -FilePath $resultJson -Encoding utf8
    Write-Host "Smoke test results saved to $resultJson"
} catch {
    Write-Error "Smoke test failed: $($_.Exception.Message)"
}
