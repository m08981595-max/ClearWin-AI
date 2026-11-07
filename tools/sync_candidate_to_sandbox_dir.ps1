# Copy candidate installer to isolated test folder on D:
$source = "release\ClearWin-Setup-2.7.2-Offline-Candidate.exe"
$dest = "D:\ClearWin_Sandbox_Test\ClearWin-Setup-2.7.2-Offline-Candidate.exe"

Write-Host "Copying candidate installer to D:\ClearWin_Sandbox_Test..."
Copy-Item -Path $source -Destination $dest -Force

$srcHash = (Get-FileHash -Path $source -Algorithm SHA256).Hash
$dstHash = (Get-FileHash -Path $dest -Algorithm SHA256).Hash

Write-Host "Source Hash : $srcHash"
Write-Host "Dest Hash   : $dstHash"
Write-Host "Match       : $($srcHash -eq $dstHash)"

if ($srcHash -ne $dstHash) {
    Write-Error "Hash mismatch between source and destination!"
    exit 1
}
Write-Host "Copy and verification succeeded."
