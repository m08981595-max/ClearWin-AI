# Test Inno Setup Uninstaller
$uninstaller = "D:\ClearWin_Local_Test\Install\unins000.exe"
$targetDir = "D:\ClearWin_Local_Test\Install"
$logFile = "D:\ClearWin_Local_Test\uninstall.log"
$resultJson = "tools\uninstaller_test_result.json"

Write-Host "Uninstaller: $uninstaller"
if (-not (Test-Path $uninstaller)) {
    Write-Error "Uninstaller not found at $uninstaller"
    exit 1
}

$argList = @(
    "/VERYSILENT",
    "/SUPPRESSMSGBOXES",
    "/NORESTART",
    "/LOG=""$logFile"""
)

try {
    Write-Host "Launching uninstaller..."
    $proc = Start-Process -FilePath $uninstaller -ArgumentList $argList -Wait -PassThru
    Write-Host "Uninstaller finished with ExitCode: $($proc.ExitCode)"

    # Give OS a few seconds to finish deleting files and uninstaller cleanup
    Start-Sleep -Seconds 5

    $mainExeStillExists = Test-Path (Join-Path $targetDir "ClearWin.exe")
    $remainingItems = Get-ChildItem -Path $targetDir -Recurse -ErrorAction SilentlyContinue

    Write-Host "ClearWin.exe still exists: $mainExeStillExists"
    Write-Host "Remaining files/folders: $(if ($remainingItems) { $remainingItems.Count } else { 0 })"

    $res = [PSCustomObject]@{
        UninstallerPath    = $uninstaller
        ExitCode           = $proc.ExitCode
        MainExeRemoved     = (-not $mainExeStillExists)
        RemainingItemCount = if ($remainingItems) { $remainingItems.Count } else { 0 }
        CleanUninstall     = (-not $mainExeStillExists)
        Timestamp          = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    }
    $res | ConvertTo-Json -Depth 4 | Out-File -FilePath $resultJson -Encoding utf8
    Write-Host "Uninstallation results saved to $resultJson"
} catch {
    Write-Error "Uninstaller test failed: $($_.Exception.Message)"
}
