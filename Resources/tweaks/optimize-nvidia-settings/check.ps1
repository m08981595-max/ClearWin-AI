# ============================================================================
# Read-only status check for the "Optimize Nvidia Settings" tweak.
# No admin, no modifications: just reports the current value + undo state.
# ============================================================================

$base   = 'HKLM:\SYSTEM\CurrentControlSet\Services\nvlddmkg'
$custom = Join-Path $base 'Parameters\Custom'
$state  = Join-Path $PSScriptRoot 'nvidia-state.json'

if (-not (Test-Path $base)) {
    Write-Host "NVIDIA driver service (nvlddmkg) not found. No NVIDIA GPU/driver present."
    Write-Host "Tweak status: NOT APPLICABLE"
    exit 0
}

$labels = @{
    '0' = 'Prefer adaptive (default)'
    '1' = 'Prefer normal power saving'
    '2' = 'Prefer maximum performance'
}

$prop = $null
if (Test-Path $custom) {
    $prop = Get-ItemProperty -Path $custom -Name 'PowerManagementMode' -ErrorAction SilentlyContinue
}

if ($null -eq $prop) {
    Write-Host "PowerManagementMode: <not set>  ->  using driver default (Adaptive)."
    Write-Host "Tweak status: NOT APPLIED"
}
else {
    $v    = [int]$prop.PowerManagementMode
    $desc = if ($labels.ContainsKey([string]$v)) { $labels[[string]$v] } else { "unknown value ($v)" }
    Write-Host ("PowerManagementMode: " + $v + "  ->  " + $desc)
    if ($v -eq 2) { Write-Host "Tweak status: APPLIED (maximum performance)" }
    else          { Write-Host "Tweak status: PARTIALLY APPLIED / not at maximum performance" }
}

if (Test-Path $state) {
    Write-Host ("Snapshot present: " + $state + "  ->  in-app undo is available.")
}
else {
    Write-Host "No snapshot present. Undo will fall back to the driver default (adaptive)."
}
exit 0
