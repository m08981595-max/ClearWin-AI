# Build Offline Candidate Installer
$projectRoot = Split-Path -Parent $PSScriptRoot
$issCandidate = Join-Path $projectRoot "installer-offline-candidate.iss"
$originalSetup = Join-Path $projectRoot "release\ClearWin-Setup-2.7.2.exe"
$candidateSetup = Join-Path $projectRoot "release\ClearWin-Setup-2.7.2-Offline-Candidate.exe"

# Record original setup SHA-256 before doing anything
if (Test-Path $originalSetup) {
    $origHashPre = (Get-FileHash -Path $originalSetup -Algorithm SHA256).Hash
    Write-Host "Original Setup Pre-Build SHA-256: $origHashPre"
} else {
    Write-Error "Original setup not found at $originalSetup"
    exit 1
}

# Locate ISCC
$isccPaths = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\iscc.exe",
    "C:\Users\maher\AppData\Local\Programs\Inno Setup 6\iscc.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\iscc.exe",
    "${env:ProgramFiles}\Inno Setup 6\iscc.exe"
)
$iscc = $isccPaths | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    Write-Error "Inno Setup compiler (iscc.exe) not found."
    exit 2
}
Write-Host "Found Inno Setup Compiler: $iscc"

Write-Host "Compiling Offline Candidate installer..."
$buildProcess = Start-Process -FilePath $iscc -ArgumentList @("""$issCandidate""") -NoNewWindow -Wait -PassThru
if ($buildProcess.ExitCode -ne 0) {
    Write-Error "ISCC compilation failed with exit code $($buildProcess.ExitCode)"
    exit $buildProcess.ExitCode
}

# Verify Candidate Exists
if (-not (Test-Path $candidateSetup)) {
    Write-Error "Candidate setup not generated at $candidateSetup"
    exit 3
}

$candidateItem = Get-Item $candidateSetup
$candidateSizeMB = [math]::Round($candidateItem.Length / 1MB, 2)
$candidateHash = (Get-FileHash -Path $candidateSetup -Algorithm SHA256).Hash

Write-Host "Candidate Setup built successfully!"
Write-Host "Path: $($candidateItem.FullName)"
Write-Host "Size: $($candidateItem.Length) bytes ($candidateSizeMB MB)"
Write-Host "SHA-256: $candidateHash"

# Verify Original Setup was completely untouched
$origHashPost = (Get-FileHash -Path $originalSetup -Algorithm SHA256).Hash
Write-Host "Original Setup Post-Build SHA-256: $origHashPost"
if ($origHashPre -ne $origHashPost) {
    Write-Error "CRITICAL: Original release installer was modified during candidate build!"
    exit 4
}
Write-Host "Integrity Confirmed: Original installer is 100% unchanged."

# Save candidate metadata
$meta = [PSCustomObject]@{
    CandidatePath       = $candidateItem.FullName
    CandidateSizeBytes  = $candidateItem.Length
    CandidateSizeMB     = $candidateSizeMB
    CandidateSha256     = $candidateHash
    OriginalSetupSha256 = $origHashPost
    IsccPath            = $iscc
    Timestamp           = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
}
$meta | ConvertTo-Json -Depth 4 | Out-File -FilePath (Join-Path $projectRoot "release\candidate-offline-metadata.json") -Encoding utf8
Write-Host "Candidate metadata recorded."
exit 0
