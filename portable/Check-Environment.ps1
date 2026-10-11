param([string]$ReportPath)
$ErrorActionPreference = 'Stop'

# Print policy information BEFORE using arbitrary .NET types or importing modules.
# If script execution itself is prohibited, Windows blocks this file before any of this runs.
Write-Host 'TrackballCAD Portable - read-only environment diagnostic'
Write-Host "PowerShell: $($PSVersionTable.PSVersion) / $($PSVersionTable.PSEdition)"
Write-Host "Language mode: $($ExecutionContext.SessionState.LanguageMode)"
Write-Host 'Execution policies (no changes):'
Get-ExecutionPolicy -List | Format-Table -AutoSize | Out-Host
Write-Host "Effective policy: $(Get-ExecutionPolicy)"
if ($ExecutionContext.SessionState.LanguageMode -ne 'FullLanguage') {
    Write-Host 'BLOCKED: FullLanguage is required for custom C# / Win32. Add-Type, Forms, Raw Input and COM checks are skipped. Contact school IT; no restrictions are changed.'
    exit 1
}

$checks = New-Object 'System.Collections.Generic.List[object]'
function Add-Check {
    param([string]$Name, [bool]$Passed, [string]$Detail)
    $checks.Add([pscustomobject]@{ Name = $Name; Passed = $Passed; Detail = $Detail })
    Write-Host "[$(if ($Passed) { 'OK' } else { 'BLOCKED' })] $Name : $Detail"
}

Write-Host "Process bits: $([IntPtr]::Size * 8); Apartment: $([Threading.Thread]::CurrentThread.GetApartmentState())"
$hostReady = $false
try {
    Import-Module (Join-Path $PSScriptRoot 'modules/Portable.Runtime.psm1') -ErrorAction Stop
    Assert-PortableHost
    $hostReady = $true
    Add-Check 'Host' $true 'Windows PowerShell 5.1 Desktop, x64, STA, interactive session.'
} catch { Add-Check 'Host' $false $_.Exception.Message }

$devices = @()
$com = $null
if ($hostReady) {
    try {
        if (!('TrackballCADPortableCompilerProbe' -as [type])) {
            Add-Type -TypeDefinition 'public static class TrackballCADPortableCompilerProbe { public static int Value() { return 51; } }' -ErrorAction Stop
        }
        if ([TrackballCADPortableCompilerProbe]::Value() -ne 51) { throw 'Unexpected compiler probe result.' }
        Add-Check 'Add-Type C#' $true 'Custom managed C# compiled and executed with the installed framework.'
    } catch { Add-Check 'Add-Type C#' $false $_.Exception.Message }
    $form = $null
    try {
        Add-Type -AssemblyName System.Windows.Forms -ErrorAction Stop
        $form = New-Object Windows.Forms.Form
        $handle = $form.Handle # Hidden handle creation tests actual GUI availability.
        Add-Check 'Windows Forms' ($handle -ne [IntPtr]::Zero) 'Assembly loaded; hidden window handle created. No window shown.'
    } catch { Add-Check 'Windows Forms' $false $_.Exception.Message }
    finally { if ($null -ne $form) { $form.Dispose() } }
    $bridge = $null
    try {
        Import-PortableBridge
        $devices = @([TrackballCAD.Portable.RawInputBridge]::EnumerateDevices())
        $bridge = New-Object TrackballCAD.Portable.RawInputBridge
        $bridge.Dispose()
        if ($bridge.LastError) { throw $bridge.LastError }
        Add-Check 'Raw Input' ($devices.Count -gt 0) "$($devices.Count) mouse collections. Registration/unregistration tested; actual movement still needs Start-TrackballCAD.ps1."
        $devices | Select-Object Handle, Label, Vid, Pid, Path | Format-List | Out-Host
    } catch { Add-Check 'Raw Input' $false $_.Exception.Message }
    finally { if ($null -ne $bridge) { $bridge.Dispose() } }
    try {
        Import-Module (Join-Path $PSScriptRoot 'modules/SolidWorks.Probe.psm1') -ErrorAction Stop
        $com = Test-ExistingSolidWorks
        Add-Check 'SolidWorks COM (read only)' $com.Connected ("Revision: $($com.Revision). " + $com.Detail)
    } catch { Add-Check 'SolidWorks COM (read only)' $false $_.Exception.Message }
} else {
    Write-Host 'Add-Type, Forms, Raw Input and COM checks skipped until the host requirements are met.'
}

if ($ReportPath) {
    try {
        [pscustomobject]@{
            PowerShell = [string]$PSVersionTable.PSVersion
            Bits = [IntPtr]::Size * 8
            Apartment = [string][Threading.Thread]::CurrentThread.GetApartmentState()
            LanguageMode = [string]$ExecutionContext.SessionState.LanguageMode
            EffectivePolicy = [string](Get-ExecutionPolicy)
            Policies = @(Get-ExecutionPolicy -List | Select-Object Scope, ExecutionPolicy)
            Checks = @($checks.ToArray())
            Devices = $devices
            SolidWorks = $com
        } | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $ReportPath -Encoding UTF8 -ErrorAction Stop
        Write-Host "Report written: $ReportPath (includes device paths)."
    } catch { Add-Check 'Report file' $false $_.Exception.Message }
}
Write-Host 'No SolidWorks instance created, no view/document changed. Raw Input does not isolate the Windows cursor.'
if (@($checks | Where-Object { !$_.Passed }).Count -gt 0) { exit 1 }
exit 0
