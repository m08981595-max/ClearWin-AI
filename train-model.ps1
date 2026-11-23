<#
.SYNOPSIS
    ClearWin v2.7.7 - SmartAgent ML offline training PREFLIGHT (Zero Fake).

.DESCRIPTION
    This script deliberately does NOT train on invented data and does NOT call
    "ClearWin.exe --deep-train": that entry point distils a RULE-BASED SYNTHETIC
    prior grid (SmartAgent.Pretrain.cs / GeneratePriorGrid), i.e. it is not
    training on measured outcomes and would violate the Zero Fake rule.

    It inspects ONLY real telemetry that exists on disk:
      - <engine>/reports/real-world-baseline/*.csv and *.json
      - <engine>/logs/** (if present)
      - %LOCALAPPDATA%\ClearWin\logs (runtime logs, informational only)
      - an explicitly supplied, genuinely labelled CSV via -LabelledCsv

    A usable supervised row requires:
      - 18 numeric features (the MLFeatureVector.ToArray() schema):
        CpuUsage, RamUsage, GpuUsage, DiskUsage, CpuTemp, GpuTemp,
        Fps, Fps01Low, FpsStability, ProcessCount, HourOfDay, MinuteOfHour,
        IsGaming, IsOnBattery, BatteryLevel, CpuTrend, RamTrend, TempTrend
      - a non-empty ActionTaken label
      - a numeric OutcomeScore in [0,1] measured from a REAL optimization outcome

    If no qualifying labelled dataset is found the script prints the exact
    requirement and exits with code 2. It never fabricates samples, never
    creates/overwrites/deletes a model checkpoint, and never starts a build.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\train-model.ps1

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\train-model.ps1 -LabelledCsv 'C:\data\real_outcomes.csv'
#>
[CmdletBinding()]
param(
    [string] $LabelledCsv = ''
)

$ErrorActionPreference = 'Stop'

function Write-Step {
    param([string] $Message)
    Write-Host $Message -ForegroundColor Cyan
}
function Write-OkMsg {
    param([string] $Message)
    Write-Host $Message -ForegroundColor Green
}
function Write-WarnMsg {
    param([string] $Message)
    Write-Host $Message -ForegroundColor Yellow
}

function Test-FiniteNumber {
    param([string] $Text)
    $value = [double]0
    if ([string]::IsNullOrWhiteSpace($Text)) { return $false }
    if (-not [double]::TryParse($Text,
            [System.Globalization.NumberStyles]::Float,
            [System.Globalization.CultureInfo]::InvariantCulture,
            [ref] $value)) { return $false }
    return (-not [double]::IsNaN($value) -and -not [double]::IsInfinity($value))
}

$engineRoot = $PSScriptRoot
$reportsRoot = Join-Path -Path $engineRoot -ChildPath 'reports'
$baselineDir = Join-Path -Path $reportsRoot -ChildPath 'real-world-baseline'
$logsDir = Join-Path -Path $engineRoot -ChildPath 'logs'
$runtimeLogsDir = ''
if (-not [string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
    $runtimeLogsDir = Join-Path -Path $env:LOCALAPPDATA -ChildPath 'ClearWin\logs'
}

$featureColumns = @(
    'CpuUsage', 'RamUsage', 'GpuUsage', 'DiskUsage', 'CpuTemp', 'GpuTemp',
    'Fps', 'Fps01Low', 'FpsStability', 'ProcessCount', 'HourOfDay', 'MinuteOfHour',
    'IsGaming', 'IsOnBattery', 'BatteryLevel', 'CpuTrend', 'RamTrend', 'TempTrend'
)
# SmartAgent.MLModel.cs: private const int ML_MIN = 15
$minimumLabelledRows = 15

Write-Step '=== ClearWin v2.7.7 / SmartAgent ML training preflight (Zero Fake) ==='
Write-Step "Engine root : $engineRoot"
Write-Step "Checkpoint  : (managed by SmartAgent.MLModel.cs at %LOCALAPPDATA%\ClearWin\agent\ml_nn_v2.dat)"
Write-Host ''

# --- 1) Inventory the REAL telemetry actually shipped in this repository. -----
Write-Step '--- [1/3] Real telemetry present in this repository ---'
$telemetryFiles = @()
if (Test-Path -LiteralPath $baselineDir -PathType Container) {
    $telemetryFiles = @(Get-ChildItem -LiteralPath $baselineDir -File |
        Where-Object { $_.Extension -in @('.csv', '.json', '.jsonl') })
}

if ($telemetryFiles.Count -eq 0) {
    Write-WarnMsg '  No telemetry files found under reports\real-world-baseline.'
}
else {
    foreach ($file in $telemetryFiles) {
        $recordCount = 0
        if ($file.Extension -eq '.csv') {
            $recordCount = @(Import-Csv -LiteralPath $file.FullName).Count
        }
        else {
            # A baseline .json is a single point-in-time machine snapshot.
            $recordCount = 1
        }
        Write-Host ("  {0}  ->  {1} record(s), {2} bytes" -f $file.Name, $recordCount, $file.Length)
    }
    Write-WarnMsg '  These captures contain machine counters only (CPU / RAM / disk / processes).'
    Write-WarnMsg '  They carry NO ActionTaken label and NO measured OutcomeScore, so they cannot'
    Write-WarnMsg '  supervise the 18->32->16->1 outcome predictor.'
}

foreach ($dir in @($logsDir, $runtimeLogsDir)) {
    if (Test-Path -LiteralPath $dir -PathType Container) {
        $logCount = @(Get-ChildItem -LiteralPath $dir -File -Recurse -ErrorAction SilentlyContinue).Count
        Write-Host ("  Log folder {0} : {1} file(s) (informational only; not a labelled dataset)" -f $dir, $logCount)
    }
    else {
        Write-Host ("  Log folder {0} : not present" -f $dir)
    }
}
Write-Host ''

# --- 2) Optional explicitly supplied labelled dataset. -----------------------
Write-Step '--- [2/3] Optional labelled dataset (-LabelledCsv) ---'
if ([string]::IsNullOrWhiteSpace($LabelledCsv)) {
    Write-Host '  No -LabelledCsv supplied.'
}
elseif (-not (Test-Path -LiteralPath $LabelledCsv -PathType Leaf)) {
    Write-WarnMsg "  Labelled CSV not found: $LabelledCsv"
}
else {
    $rows = @(Import-Csv -LiteralPath $LabelledCsv)
    $columns = @()
    if ($rows.Count -gt 0) { $columns = @($rows[0].PSObject.Properties.Name) }

    $missing = @()
    foreach ($name in ($featureColumns + @('ActionTaken', 'OutcomeScore'))) {
        if ($columns -notcontains $name) { $missing += $name }
    }

    $validRows = 0
    if ($missing.Count -eq 0) {
        foreach ($row in $rows) {
            $rowOk = $true
            foreach ($name in $featureColumns) {
                if (-not (Test-FiniteNumber ([string] $row.$name))) { $rowOk = $false; break }
            }
            if (-not $rowOk) { continue }
            if ([string]::IsNullOrWhiteSpace([string] $row.ActionTaken)) { continue }
            $score = [double]0
            if (-not [double]::TryParse([string] $row.OutcomeScore,
                    [System.Globalization.NumberStyles]::Float,
                    [System.Globalization.CultureInfo]::InvariantCulture,
                    [ref] $score)) { continue }
            if ($score -lt 0 -or $score -gt 1) { continue }
            $validRows++
        }
    }

    Write-Host ("  File      : {0}" -f $LabelledCsv)
    Write-Host ("  Rows      : {0}   Valid labelled rows: {1}" -f $rows.Count, $validRows)
    if ($missing.Count -gt 0) {
        Write-WarnMsg ("  Missing required columns: {0}" -f ($missing -join ', '))
    }

    if ($missing.Count -eq 0 -and $validRows -ge $minimumLabelledRows) {
        Write-OkMsg '  A genuine labelled dataset IS available.'
        Write-OkMsg ('  {0} valid rows satisfy the minimum of {1}.' -f $validRows, $minimumLabelledRows)
        Write-WarnMsg '  NOTE: this repository has no production C# entry point that imports an'
        Write-WarnMsg '  external labelled CSV into MLEnsemble. Implementing that importer is a'
        Write-WarnMsg '  separate, explicit change; this preflight will NOT substitute a parallel'
        Write-WarnMsg '  hand-written trainer or invent data to fill the gap.'
        Write-Host ''
        Write-Step '--- [3/3] Result: READY FOR A REAL IMPORTER (no data fabricated) ---'
        exit 0
    }
}
Write-Host ''

# --- 3) No sufficient real labelled data: state the requirement, do nothing. -
Write-Step '--- [3/3] Result: INSUFFICIENT REAL TRAINING DATA ---'
$requirement = @'

المطلوب لتدريب حقيقي (Zero Fake / لا بيانات صناعية):
  1) تصدير عينات مُلصقة من تشغيل ClearWin الفعلي: 18 ميزة رقمية (مخطط
     MLFeatureVector.ToArray) مع ActionTaken و OutcomeScore مقاس من نتيجة
     تحسين حقيقية داخل [0,1].
  2) الحد الأدنى 15 صفاً صالحاً (ML_MIN في SmartAgent.MLModel.cs)، ويُفضّل
     ≥ 200 موزّعة على أكثر من إجراء لتجنّب انحياز إجراء واحد.
  3) ضع الملف تحت reports/ أو مرّره عبر -LabelledCsv <path>.
  4) لا يُولّد هذا السكربت أي بيانات صناعية، ولا يستدعي --deep-train، لأن
     الدالة GeneratePriorGrid في SmartAgent.Pretrain.cs تبني شبكة من
     معرفة مسبقة قائمة على القواعد وليس من نتائج مقاسة.

REQUIREMENT (English):
  1) Export genuinely labelled rows from a real ClearWin run: the 18 numeric
     MLFeatureVector.ToArray() features + ActionTaken + an OutcomeScore in
     [0,1] measured from an actual optimization outcome.
  2) At least 15 valid rows (ML_MIN); preferably >= 200 spanning multiple
     actions to avoid single-action bias.
  3) Place the export under reports/ or pass it with -LabelledCsv <path>.
  4) No synthetic samples are generated here and --deep-train is NOT invoked:
     GeneratePriorGrid distils a rule-based prior, not measured outcomes.

This script exits without creating, modifying or deleting any model file.
'@
Write-WarnMsg $requirement
exit 2
