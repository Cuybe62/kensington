param([string]$ConfigPath = (Join-Path $PSScriptRoot 'config.json'))
$ErrorActionPreference = 'Stop'
$window = $null
try {
    Import-Module (Join-Path $PSScriptRoot 'modules/Portable.Runtime.psm1') -ErrorAction Stop
    Import-Module (Join-Path $PSScriptRoot 'modules/Portable.Configuration.psm1') -ErrorAction Stop
    Assert-PortableHost
    $configuration = Read-PortableConfiguration -Path $ConfigPath
    Import-PortableBridge
    $settings = Read-RotationSettings -Configuration $configuration
    $savedPath = ''
    if ($null -ne $configuration.selectedDevice) { $savedPath = $configuration.selectedDevice.path }
    [Windows.Forms.Application]::EnableVisualStyles()
    $window = New-Object TrackballCAD.Portable.NavigationWindow -ArgumentList $settings, $savedPath
    Write-Host 'Rotation is initially disabled. Select the trackball, connect, then Enable rotation.'
    Write-Host 'Windows cursor and native clicks remain shared. Escape stops navigation while the UI responds.'
    [Windows.Forms.Application]::Run($window)
    if ($window.LastError) { Write-Host "Last navigation error: $($window.LastError)" }
    if ($window.SaveRequested) {
        Save-RotationSettings -Path $ConfigPath -Configuration $configuration -Settings $window.Settings -Device $window.SelectedDevice
        Write-Host "Configuration saved: $ConfigPath"
    }
} catch {
    Write-Host "STOPPED: $($_.Exception.Message)"
    exit 1
} finally { if ($null -ne $window) { $window.Dispose() } }
