$ErrorActionPreference = 'Stop'
$logPath = 'D:\open code\optomize\ClearWin-2.7.2-GlassUI-V21-AI-EXECUTION-CENTER\clearwin_v19_scrollbar_control\tools\enable_sandbox_transcript.log'
$resPath = 'D:\open code\optomize\ClearWin-2.7.2-GlassUI-V21-AI-EXECUTION-CENTER\clearwin_v19_scrollbar_control\tools\enable_sandbox_result.json'
Start-Transcript -Path $logPath -Force
try {
    Write-Host 'Executing: Enable-WindowsOptionalFeature -Online -FeatureName Containers-DisposableClientVM -All -NoRestart'
    $res = Enable-WindowsOptionalFeature -Online -FeatureName 'Containers-DisposableClientVM' -All -NoRestart
    Write-Host 'Feature Enablement Command Completed.'
    Write-Host ('RestartNeeded: ' + $res.RestartNeeded)
    $outObj = [PSCustomObject]@{
        Success = $true
        RestartNeeded = [bool]$res.RestartNeeded
        ExitCode = 0
        Timestamp = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
    }
    $outObj | ConvertTo-Json | Out-File -FilePath $resPath -Encoding utf8
} catch {
    Write-Error ('Exception: ' + $_.Exception.Message)
    $outObj = [PSCustomObject]@{
        Success = $false
        RestartNeeded = $false
        ErrorMessage = $_.Exception.Message
        ExitCode = 1
        Timestamp = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
    }
    $outObj | ConvertTo-Json | Out-File -FilePath $resPath -Encoding utf8
} finally {
    Stop-Transcript
}
