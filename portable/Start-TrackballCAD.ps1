param([string]$ConfigPath = (Join-Path $PSScriptRoot 'config.json'))
$ErrorActionPreference = 'Stop'
if ($ExecutionContext.SessionState.LanguageMode -ne 'FullLanguage') {
    Write-Error 'Custom C# capture requires FullLanguage. No restriction will be bypassed; contact school IT.'
    exit 1
}
$window = $null
try {
    Import-Module (Join-Path $PSScriptRoot 'modules/Portable.Runtime.psm1') -ErrorAction Stop
    Import-Module (Join-Path $PSScriptRoot 'modules/Portable.Configuration.psm1') -ErrorAction Stop
    Assert-PortableHost
    $configuration = Read-PortableConfiguration -Path $ConfigPath
    Import-PortableBridge
    $savedPath = ''
    if ($null -ne $configuration.selectedDevice) { $savedPath = $configuration.selectedDevice.path }
    [Windows.Forms.Application]::EnableVisualStyles()
    $window = New-Object TrackballCAD.Portable.DiagnosticsWindow -ArgumentList ([int]$configuration.diagnosticsRefreshMs), $savedPath
    Write-Host 'Capture only. No SolidWorks commands; no suppression or cursor repositioning. Stop with Esc in the window or its Close button.'
    [Windows.Forms.Application]::Run($window)
    if ($window.CaptureError) { throw $window.CaptureError }
    if ($null -ne $window.SelectedDevice) {
        Save-PortableDevice -Path $ConfigPath -Configuration $configuration -Device $window.SelectedDevice
        Write-Host "Device identity saved to $ConfigPath. It will still require manual confirmation next time."
    }
} catch {
    Write-Host "STOPPED: $($_.Exception.Message)"
    Write-Host 'Run Check-Environment.ps1 in Windows PowerShell 5.1 x64 STA. Do not change school security policies.'
    exit 1
} finally { if ($null -ne $window) { $window.Dispose() } }
