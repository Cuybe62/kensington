Set-StrictMode -Version Latest

function Read-PortableConfiguration {
    param([Parameter(Mandatory = $true)][string]$Path)
    try {
        $configuration = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop
        if ($configuration.schemaVersion -ne 1) { throw 'Expected schemaVersion 1.' }
        $interval = $configuration.diagnosticsRefreshMs
        if ($interval -isnot [int] -and $interval -isnot [long]) { throw 'diagnosticsRefreshMs must be an integer.' }
        if ($interval -lt 50 -or $interval -gt 1000) { throw 'diagnosticsRefreshMs must be between 50 and 1000.' }
        if ($null -ne $configuration.selectedDevice) {
            foreach ($name in @('path', 'vid', 'pid', 'label')) {
                if ($configuration.selectedDevice.$name -isnot [string]) { throw "selectedDevice.$name must be a string." }
            }
            if ([string]::IsNullOrWhiteSpace($configuration.selectedDevice.path)) { throw 'selectedDevice.path cannot be empty.' }
        }
        return $configuration
    } catch { throw "Invalid configuration '$Path': $($_.Exception.Message)" }
}

function Save-PortableDevice {
    param([Parameter(Mandatory = $true)][string]$Path, [Parameter(Mandatory = $true)]$Configuration, [Parameter(Mandatory = $true)]$Device)
    $Configuration.selectedDevice = [pscustomobject]@{ path = $Device.Path; vid = $Device.Vid; pid = $Device.Pid; label = $Device.Label }
    Write-PortableConfiguration -Path $Path -Configuration $Configuration
}

function Write-PortableConfiguration {
    param([Parameter(Mandatory = $true)][string]$Path, [Parameter(Mandatory = $true)]$Configuration)
    # Replace atomically in the same directory; never truncate the previous config on write failure.
    # Resolve against the PowerShell location, which may differ from the process cwd.
    $target = (Resolve-Path -LiteralPath $Path -ErrorAction Stop).ProviderPath
    $temporary = $target + '.' + [Guid]::NewGuid().ToString('N') + '.tmp'
    try {
        $json = $Configuration | ConvertTo-Json -Depth 5
        [IO.File]::WriteAllText($temporary, $json, (New-Object Text.UTF8Encoding($true)))
        if ([IO.File]::Exists($target)) { [IO.File]::Replace($temporary, $target, [NullString]::Value) }
        else { [IO.File]::Move($temporary, $target) }
    } finally {
        if ([IO.File]::Exists($temporary)) { [IO.File]::Delete($temporary) }
    }
}

function Read-RotationSettings {
    param([Parameter(Mandatory = $true)]$Configuration)
    $settings = New-Object TrackballCAD.Portable.RotationSettings
    # Older diagnostic configs remain usable, with conservative defaults.
    if ($null -ne $Configuration.PSObject.Properties['rotation']) {
        $rotation = $Configuration.rotation
        if ($rotation.updateHz -isnot [int] -and $rotation.updateHz -isnot [long]) { throw 'rotation.updateHz must be an integer.' }
        if ($rotation.activationKey -isnot [string]) { throw 'rotation.activationKey must be a string.' }
        if ($rotation.radiansPerCount -isnot [double] -and $rotation.radiansPerCount -isnot [decimal] -and $rotation.radiansPerCount -isnot [int] -and $rotation.radiansPerCount -isnot [long]) { throw 'rotation.radiansPerCount must be a number.' }
        if ($rotation.invertX -isnot [bool] -or $rotation.invertY -isnot [bool]) { throw 'rotation.invertX and invertY must be JSON booleans.' }
        $settings.UpdateHz = $rotation.updateHz
        $settings.ActivationKey = $rotation.activationKey
        if ($null -ne $rotation.PSObject.Properties['returnCursorOnStop']) {
            if ($rotation.returnCursorOnStop -isnot [bool]) { throw 'rotation.returnCursorOnStop must be a JSON boolean.' }
            $settings.ReturnCursorOnStop = $rotation.returnCursorOnStop
        }
        if ($null -ne $rotation.PSObject.Properties['activationMode']) {
            if ($rotation.activationMode -isnot [string]) { throw 'rotation.activationMode must be a string.' }
            $settings.ActivationMode = $rotation.activationMode
        }
        $settings.RadiansPerCount = $rotation.radiansPerCount
        $settings.InvertX = $rotation.invertX
        $settings.InvertY = $rotation.invertY
    }
    $settings.Validate()
    return $settings
}

function Save-RotationSettings {
    param([Parameter(Mandatory = $true)][string]$Path, [Parameter(Mandatory = $true)]$Configuration,
        [Parameter(Mandatory = $true)]$Settings, $Device)
    $Settings.Validate()
    $rotation = [pscustomobject]@{
        activationMode = $Settings.ActivationMode
        returnCursorOnStop = $Settings.ReturnCursorOnStop
        updateHz = $Settings.UpdateHz; activationKey = $Settings.ActivationKey
        radiansPerCount = $Settings.RadiansPerCount; invertX = $Settings.InvertX; invertY = $Settings.InvertY
    }
    $Configuration | Add-Member -MemberType NoteProperty -Name rotation -Value $rotation -Force
    if ($null -ne $Device) { $Configuration.selectedDevice = [pscustomobject]@{ path = $Device.Path; vid = $Device.Vid; pid = $Device.Pid; label = $Device.Label } }
    Write-PortableConfiguration -Path $Path -Configuration $Configuration
}

Export-ModuleMember -Function Read-PortableConfiguration, Save-PortableDevice, Read-RotationSettings, Save-RotationSettings
