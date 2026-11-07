$proc = Get-Process -Id 5432 -ErrorAction SilentlyContinue
if ($proc) {
    [PSCustomObject]@{
        Id = $proc.Id
        Name = $proc.ProcessName
        StartTime = $proc.StartTime
        Path = $proc.Path
        WorkingSetMB = [Math]::Round($proc.WorkingSet64 / 1MB, 2)
        Responding = $proc.Responding
        HasExited = $proc.HasExited
        MainWindowTitle = $proc.MainWindowTitle
        MainWindowHandle = $proc.MainWindowHandle
    } | Format-List
} else {
    Write-Host "Process 5432 not found."
    Get-Process -Name ClearWin -ErrorAction SilentlyContinue | Select-Object Id, ProcessName, StartTime, MainWindowTitle, MainWindowHandle | Format-Table -AutoSize
}
