$files = Get-ChildItem -Path "src\*.cs"
foreach ($f in $files) {
    $content = Get-Content $f.FullName
    for ($i = 0; $i -lt $content.Count; $i++) {
        $line = $content[$i]
        if ($line -match 'FromSeconds\(\s*(8|9|10)' -or $line -match 'Delay\(\s*(8000|9000|10000)' -or $line -match 'WebUiHost') {
            [PSCustomObject]@{
                File = $f.Name
                LineNumber = $i + 1
                Line = $line.Trim()
            }
        }
    }
}
