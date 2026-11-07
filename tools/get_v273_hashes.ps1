$installers = @(
    "release\v2.7.3\ClearWin-Setup-2.7.3.exe",
    "release\v2.7.3\ClearWin-Setup-2.7.3-Offline.exe"
)

$results = foreach ($f in $installers) {
    if (Test-Path $f) {
        $item = Get-Item $f
        [PSCustomObject]@{
            File = $f
            Bytes = $item.Length
            SizeMB = [Math]::Round($item.Length / 1MB, 2)
            SHA256 = (Get-FileHash -Path $f -Algorithm SHA256).Hash
        }
    } else {
        [PSCustomObject]@{
            File = $f
            Bytes = 0
            SizeMB = 0
            SHA256 = "NOT_FOUND"
        }
    }
}

$results | Format-List
$results | ConvertTo-Json -Depth 3 | Out-File -FilePath "tools\v273_release_packages.json" -Encoding utf8
