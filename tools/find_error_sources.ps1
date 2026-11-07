$lines = Get-Content -Path "src\MainWindow.Logic.cs"
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i] -like '*CWLog.Error("MainWindow.Logic"*') {
        [PSCustomObject]@{
            LineNumber = $i + 1
            Line = $lines[$i].Trim()
        }
    }
}
