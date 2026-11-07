$files = Get-ChildItem -Path "src\*.cs"
foreach ($f in $files) {
    $content = Get-Content $f.FullName
    for ($i = 0; $i -lt $content.Count; $i++) {
        if ($content[$i] -match 'enum\s+AgentMode') {
            [PSCustomObject]@{
                File = $f.Name
                Line = $i + 1
                Content = ($content[$i..($i+8)] -join "`n")
            }
        }
    }
}
