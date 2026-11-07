$lines = Get-Content "src\MainWindow.cs"
for ($i = 0; $i -lt $lines.Length; $i++) {
    if ($lines[$i] -match 'BuildUI\(') {
        Write-Host "Line $($i+1): $($lines[$i])"
        for ($j = [Math]::Max(0, $i-5); $j -lt [Math]::Min($lines.Length, $i+60); $j++) {
            Write-Host "$($j+1): $($lines[$j])"
        }
    }
}
