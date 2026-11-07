Get-ChildItem -Path src,Resources,installer.iss,installer-offline-candidate.iss,build.ps1,CHANGELOG.md,README.md -Recurse -File -ErrorAction SilentlyContinue |
    Select-String -Pattern '2\.7\.2' |
    ForEach-Object {
        [PSCustomObject]@{
            File = $_.Path.Substring((Get-Location).Path.Length + 1)
            Line = $_.LineNumber
            Text = $_.Line.Trim()
        }
    } | Format-Table -AutoSize -Wrap
