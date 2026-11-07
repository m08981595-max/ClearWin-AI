$appIds = @("Microsoft.GamingApp", "Microsoft.XboxGameOverlay", "Microsoft.XboxGamingOverlay")
foreach ($id in $appIds) {
    try {
        winget install $id --silent --accept-source-agreements --accept-package-agreements
        if ($LASTEXITCODE -eq 0) { Write-Output "Installed $id" }
        else { Write-Warning "$id finished with exit code $LASTEXITCODE" }
    } catch {
        Write-Warning "$id failed: $_"
    }
}
Write-Host "Gaming apps reinstall attempted"
