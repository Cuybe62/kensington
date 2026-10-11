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

Export-ModuleMember -Function Read-PortableConfiguration, Save-PortableDevice
