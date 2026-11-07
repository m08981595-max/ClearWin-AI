$ErrorActionPreference = "SilentlyContinue"

Write-Host "=========================================================="
Write-Host "ClearWin - Windows Appx Debloat Engine"
Write-Host "Classification: IRREVERSIBLE OPERATION"
Write-Host "=========================================================="

$bloatPrefixes = @(
    "Microsoft.Clipchamp",
    "Microsoft.3DBuilder",
    "Microsoft.549981C3F5F10",
    "Microsoft.BingFinance",
    "Microsoft.BingFoodAndDrink",
    "Microsoft.BingHealthAndFitness",
    "Microsoft.BingNews",
    "Microsoft.BingSearch",
    "Microsoft.BingSports",
    "Microsoft.BingTranslator",
    "Microsoft.BingTravel",
    "Microsoft.BingWeather",
    "Microsoft.Copilot",
    "Microsoft.Getstarted",
    "Microsoft.Messaging",
    "Microsoft.Microsoft3DViewer",
    "Microsoft.MicrosoftJournal",
    "Microsoft.MicrosoftOfficeHub",
    "Microsoft.MicrosoftPowerBIForWindows",
    "Microsoft.MicrosoftSolitaireCollection",
    "Microsoft.MicrosoftStickyNotes",
    "Microsoft.MixedReality.Portal",
    "Microsoft.NetworkSpeedTest",
    "Microsoft.News",
    "Microsoft.Office.OneNote",
    "Microsoft.Office.Sway",
    "Microsoft.OneConnect",
    "Microsoft.Print3D",
    "Microsoft.SkypeApp",
    "Microsoft.Todos",
    "Microsoft.WindowsAlarms",
    "Microsoft.WindowsFeedbackHub",
    "Microsoft.WindowsMaps",
    "Microsoft.WindowsSoundRecorder",
    "Microsoft.XboxApp",
    "Microsoft.ZuneVideo",
    "MicrosoftCorporationII.MicrosoftFamily",
    "MicrosoftTeams",
    "MSTeams"
)

Write-Host "Scanning installed packages for third-party promotional bloat..."
$found = @()
foreach ($prefix in $bloatPrefixes) {
    $found += Get-AppxPackage -Name "$prefix*" -AllUsers -ErrorAction SilentlyContinue
}
$uniqueFound = $found | Sort-Object -Unique

Write-Host "Found $($uniqueFound.Count) promotional bloat package(s) on this system."
if ($uniqueFound.Count -gt 0) {
    Write-Host "Target Packages for Removal:"
    foreach ($p in $uniqueFound) {
        Write-Host "  - $($p.Name) (Version: $($p.Version))"
    }
} else {
    Write-Host "  [CLEAN] No promotional bloat apps found. System is clean."
}

# Require explicit opt-in confirmation
if ($env:CLEARWIN_DEBLOAT_OPT_IN -ne 'YES') {
    Write-Warning "BLOCKED: debloat-windows permanently removes app packages."
    Write-Warning "Automatic rollback is NOT possible once removed."
    Write-Warning "To proceed, explicit opt-in confirmation is required (CLEARWIN_DEBLOAT_OPT_IN='YES')."
    exit 2
}

if ($uniqueFound.Count -eq 0) {
    Write-Host "Nothing to remove. Exiting successfully."
    exit 0
}

Write-Host "`nProceeding with irreversible package removal (User Confirmed)..."
$removed = 0
$failed = 0
foreach ($pkg in $uniqueFound) {
    try {
        Remove-AppxPackage -Package $pkg -ErrorAction Stop
        $removed++
        Write-Host "  [REMOVED] $($pkg.Name)"
    } catch {
        $failed++
        Write-Host "  [SKIP] $($pkg.Name) (protected or in use)"
    }
}

Write-Host ""
Write-Host "=========================================================="
Write-Host "IRREVERSIBLE OPERATION COMPLETE:"
Write-Host "Permanently removed $removed package(s); $failed skipped (Windows-protected or in use)."
Write-Host "Protected Windows utilities (Notepad, Calculator, Paint, Terminal) were preserved."
Write-Host "Notice: These apps cannot be restored via undo.ps1. Reinstall from Microsoft Store if needed."
Write-Host "=========================================================="
exit 0
