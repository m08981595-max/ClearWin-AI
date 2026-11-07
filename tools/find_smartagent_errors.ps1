$files = Get-ChildItem -Path "src\*.cs"
foreach ($f in $files) {
    $content = Get-Content $f.FullName
    for ($i = 0; $i -lt $content.Count; $i++) {
        if ($content[$i] -like '*SmartAgent*' -and $content[$i] -like '*CWLog.Error*') {
            [PSCustomObject]@{
                File = $f.Name
                LineNumber = $i + 1
                Line = $content[$i].Trim()
            }
        }
    }
}
