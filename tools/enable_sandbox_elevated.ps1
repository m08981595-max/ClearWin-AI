# tools\enable_sandbox_elevated.ps1
# Prepares and triggers official UAC elevation for Containers-DisposableClientVM

$toolsDir = $PSScriptRoot
$workerScript = Join-Path $toolsDir "worker_enable_sandbox.ps1"
$resultJson = Join-Path $toolsDir "enable_sandbox_result.json"
$transcriptLog = Join-Path $toolsDir "enable_sandbox_transcript.log"

# Clean previous result files
if (Test-Path $resultJson) { Remove-Item $resultJson -Force }
if (Test-Path $transcriptLog) { Remove-Item $transcriptLog -Force }

$workerCode = @"
`$ErrorActionPreference = 'Stop'
`$logPath = '$transcriptLog'
`$resPath = '$resultJson'
Start-Transcript -Path `$logPath -Force
try {
    Write-Host 'Executing: Enable-WindowsOptionalFeature -Online -FeatureName Containers-DisposableClientVM -All -NoRestart'
    `$res = Enable-WindowsOptionalFeature -Online -FeatureName 'Containers-DisposableClientVM' -All -NoRestart
    Write-Host 'Feature Enablement Command Completed.'
    Write-Host ('RestartNeeded: ' + `$res.RestartNeeded)
    `$outObj = [PSCustomObject]@{
        Success = `$true
        RestartNeeded = [bool]`$res.RestartNeeded
        ExitCode = 0
        Timestamp = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
    }
    `$outObj | ConvertTo-Json | Out-File -FilePath `$resPath -Encoding utf8
} catch {
    Write-Error ('Exception: ' + `$_.Exception.Message)
    `$outObj = [PSCustomObject]@{
        Success = `$false
        RestartNeeded = `$false
        ErrorMessage = `$_.Exception.Message
        ExitCode = 1
        Timestamp = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
    }
    `$outObj | ConvertTo-Json | Out-File -FilePath `$resPath -Encoding utf8
} finally {
    Stop-Transcript
}
"@

$workerCode | Out-File -FilePath $workerScript -Encoding utf8

Write-Host "Worker script written to: $workerScript"
Write-Host "Triggering official UAC prompt (Start-Process -Verb RunAs)..."

try {
    $proc = Start-Process -FilePath "powershell.exe" -ArgumentList @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", """$workerScript""") -Verb RunAs -PassThru
    Write-Host "Elevated process successfully launched with PID: $($proc.Id)"
    Write-Host "Waiting up to 45 seconds for completion..."
    $proc.WaitForExit(45000)
    Write-Host "Process finished or wait completed. HasExited: $($proc.HasExited)"
} catch {
    Write-Error ("Could not trigger UAC elevation: " + $_.Exception.Message)
    $failObj = [PSCustomObject]@{
        Success = $false
        SpawnError = $_.Exception.Message
        ExitCode = -1
        Timestamp = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
    }
    $failObj | ConvertTo-Json | Out-File -FilePath $resultJson -Encoding utf8
}
