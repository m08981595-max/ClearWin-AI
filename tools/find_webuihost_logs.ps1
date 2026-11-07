$files = Get-ChildItem -Path "src\*.cs"
foreach ($f in $files) {
    $content = Get-Content $f.FullName
    for ($i = 0; $i -lt $content.Count; $i++) {
        if ($content[$i] -match 'CWLog\.(Error|Warn)\(\s*"WebUiHost"') {
            [PSCustomObject]@{
                File = $f.Name
                Line = $i + 1
                Text = $content[$i].Trim()
            }
        }
    }
}
