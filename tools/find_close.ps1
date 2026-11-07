Get-ChildItem src\MainWindow*.cs | ForEach-Object {
    $file = $_.Name
    $lines = Get-Content $_.FullName
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '\bClose\(\)') {
            Write-Host "$file : $($i+1) : $($lines[$i].Trim())"
        }
    }
}
