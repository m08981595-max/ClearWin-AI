# Create snapshot backup before 2.7.3 changes
$backupDir = "backup\pre-2.7.3-snapshot"
if (-not (Test-Path $backupDir)) {
    New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
}

$dirsToCopy = @("src", "Resources")
foreach ($dir in $dirsToCopy) {
    if (Test-Path $dir) {
        $dest = Join-Path $backupDir $dir
        if (Test-Path $dest) { Remove-Item $dest -Recurse -Force }
        Copy-Item -Path $dir -Destination $backupDir -Recurse -Force
    }
}

$filesToCopy = @("build.ps1", "installer.iss", "installer-offline-candidate.iss", "app.manifest", "CHANGELOG.md", "README.md")
foreach ($file in $filesToCopy) {
    if (Test-Path $file) {
        Copy-Item -Path $file -Destination $backupDir -Force
    }
}

Write-Host "Snapshot backup created successfully at $backupDir"
Get-ChildItem -Path $backupDir | Select-Object Name, Length, LastWriteTime
