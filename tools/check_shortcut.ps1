$w = New-Object -ComObject WScript.Shell
$lnkPath = "C:\Users\maher\Desktop\ClearWin.lnk"
if (Test-Path $lnkPath) {
    $s = $w.CreateShortcut($lnkPath)
    Write-Host "TargetPath: $($s.TargetPath)"
    Write-Host "WorkingDirectory: $($s.WorkingDirectory)"
    Write-Host "Arguments: $($s.Arguments)"
    Write-Host "IconLocation: $($s.IconLocation)"
} else {
    Write-Host "Shortcut does not exist: $lnkPath"
}
