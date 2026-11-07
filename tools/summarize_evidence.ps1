# Summarize Evidence Results
$evPath = Join-Path $PSScriptRoot "evidence_results.json"
$items = Get-Content $evPath | ConvertFrom-Json

Write-Host "Total Tweaks Audited: $($items.Count)"
Write-Host ""
Write-Host "Status Distribution:"
$items | Group-Object Status | Select-Object Name, Count | Format-Table -AutoSize

Write-Host "Non-PASS Tweaks:"
$items | Where-Object { $_.Status -ne "PASS" } | Select-Object TweakName, Status, SettingType, ExecutionType | Format-Table -AutoSize
