if ($env:CLEARWIN_ALLOW_DEFERRED_VM_TWEAKS -ne 'YES') {
    Write-Warning "BLOCKED (NOT TESTED): set-services-to-manual modifies system services startup configurations and is deferred from production execution until verified in an isolated VM."
    exit 3
}

$enginePath = Join-Path $PSScriptRoot "..\TweakBackupEngine.ps1"
if (-not (Test-Path $enginePath)) { $enginePath = Join-Path $PSScriptRoot "TweakBackupEngine.ps1" }
if (Test-Path $enginePath) { . $enginePath }

$safeServices = @(
    "DiagTrack",
    "dmwappushservice",
    "WerSvc",
    "RemoteRegistry",
    "RetailDemo",
    "MapsBroker",
    "SharedAccess",
    "Fax",
    "XblAuthManager",
    "XblGameSave",
    "XboxNetApiSvc",
    "AJRouter",
    "AssignedAccessManagerSvc",
    "shpamsvc"
)

Write-Host "ClearWin: Setting non-essential background services to Manual (safe narrow list of $($safeServices.Count) services)..."
if (Get-Command "Save-ServicesState" -ErrorAction SilentlyContinue) {
    Save-ServicesState -TweakName "set-services-to-manual" -ServiceNames $safeServices
}

$modified = 0
foreach ($name in $safeServices) {
    try {
        $svc = Get-Service -Name $name -ErrorAction SilentlyContinue
        if ($svc) {
            Set-Service -Name $name -StartupType Manual -ErrorAction Stop
            $modified++
            Write-Host "  [OK] $name set to Manual"
        } else {
            Write-Host "  [SKIP] $name (not present on this edition of Windows)"
        }
    } catch {
        Write-Warning "Failed to set ${name}: $($_.Exception.Message)"
    }
}

Write-Host "Configured $modified service(s) to Manual. Pre-tweak state captured for exact rollback."