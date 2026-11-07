# apply_update_bypass.ps1
$logFile = "$PSScriptRoot\bypass_result.txt"
"Starting bypass script at $(Get-Date)" | Out-File $logFile -Encoding utf8

try {
    # 1. Enable and start BITS service
    Set-Service -Name bits -StartupType Automatic
    Start-Service -Name bits
    "BITS service set to Automatic and started" | Out-File $logFile -Append -Encoding utf8
} catch {
    "Error setting BITS: $($_.Exception.Message)" | Out-File $logFile -Append -Encoding utf8
}

try {
    # 2. Ensure Windows Update service is Automatic or Manual
    Set-Service -Name wuauserv -StartupType Manual
    Start-Service -Name wuauserv
    "wuauserv service checked" | Out-File $logFile -Append -Encoding utf8
} catch {
    "Error setting wuauserv: $($_.Exception.Message)" | Out-File $logFile -Append -Encoding utf8
}

try {
    # 3. Remove TargetReleaseVersion blocks
    $wuPath = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate"
    if (Test-Path $wuPath) {
        Remove-ItemProperty -Path $wuPath -Name "TargetReleaseVersion" -ErrorAction SilentlyContinue
        Remove-ItemProperty -Path $wuPath -Name "ProductVersion" -ErrorAction SilentlyContinue
        Set-ItemProperty -Path $wuPath -Name "DisableWUfBSafeguards" -Value 1 -Type DWord
        Set-ItemProperty -Path $wuPath -Name "ManagePreviewBuildsPolicyValue" -Value 1 -Type DWord -ErrorAction SilentlyContinue
        "WindowsUpdate policies updated (Safeguards disabled, TargetRelease removed)" | Out-File $logFile -Append -Encoding utf8
    }
} catch {
    "Error updating WU policies: $($_.Exception.Message)" | Out-File $logFile -Append -Encoding utf8
}

try {
    # 4. MoSetup Bypass
    $moPath = "HKLM:\SYSTEM\Setup\MoSetup"
    if (-not (Test-Path $moPath)) { New-Item -Path $moPath -Force | Out-Null }
    Set-ItemProperty -Path $moPath -Name "AllowUpgradesWithUnsupportedTPMOrCPU" -Value 1 -Type DWord
    "MoSetup AllowUpgradesWithUnsupportedTPMOrCPU set" | Out-File $logFile -Append -Encoding utf8
} catch {
    "Error setting MoSetup: $($_.Exception.Message)" | Out-File $logFile -Append -Encoding utf8
}

try {
    # 5. LabConfig Bypasses
    $labPath = "HKLM:\SYSTEM\Setup\LabConfig"
    if (-not (Test-Path $labPath)) { New-Item -Path $labPath -Force | Out-Null }
    Set-ItemProperty -Path $labPath -Name "BypassTPMCheck" -Value 1 -Type DWord
    Set-ItemProperty -Path $labPath -Name "BypassCPUCheck" -Value 1 -Type DWord
    Set-ItemProperty -Path $labPath -Name "BypassSecureBootCheck" -Value 1 -Type DWord
    Set-ItemProperty -Path $labPath -Name "BypassRAMCheck" -Value 1 -Type DWord
    Set-ItemProperty -Path $labPath -Name "BypassStorageCheck" -Value 1 -Type DWord
    "LabConfig bypass keys set" | Out-File $logFile -Append -Encoding utf8
} catch {
    "Error setting LabConfig: $($_.Exception.Message)" | Out-File $logFile -Append -Encoding utf8
}

try {
    # 6. Override TargetVersionUpgradeExperienceIndicators to Green
    $indicatorBase = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\TargetVersionUpgradeExperienceIndicators"
    $subkeys = @("GE26H2", "GE25H2")
    foreach ($k in $subkeys) {
        $p = "$indicatorBase\$k"
        if (Test-Path $p) {
            Set-ItemProperty -Path $p -Name "UpgEx" -Value "Green"
            Set-ItemProperty -Path $p -Name "UpgExU" -Value "Green"
            Set-ItemProperty -Path $p -Name "GStatus" -Value 0 -Type DWord
            Set-ItemProperty -Path $p -Name "RedReason" -Value @()
            "Indicator for $k set to Green" | Out-File $logFile -Append -Encoding utf8
        }
    }
} catch {
    "Error updating indicators: $($_.Exception.Message)" | Out-File $logFile -Append -Encoding utf8
}

# 7. Trigger Windows Update Scan
try {
    $au = New-Object -ComObject Microsoft.Update.AutoUpdate
    $au.DetectNow()
    "DetectNow called successfully" | Out-File $logFile -Append -Encoding utf8
} catch {
    "Error triggering DetectNow: $($_.Exception.Message)" | Out-File $logFile -Append -Encoding utf8
}

"Completed bypass script at $(Get-Date)" | Out-File $logFile -Append -Encoding utf8
