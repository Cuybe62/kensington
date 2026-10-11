# Opening this tool does not rotate anything: the Arm button is required.
$ErrorActionPreference = 'Stop'
$window = $null
try {
    Import-Module (Join-Path $PSScriptRoot 'modules/Portable.Runtime.psm1') -ErrorAction Stop
    Assert-PortableHost
    Add-Type -AssemblyName System.Windows.Forms -ErrorAction Stop
    Add-Type -AssemblyName System.Drawing -ErrorAction Stop
    if (!('TrackballCAD.Portable.ViewTestWindow' -as [type])) {
        Add-Type -LiteralPath (Join-Path $PSScriptRoot 'src/SolidWorksViewTest.cs') -ReferencedAssemblies System.dll,System.Core.dll,System.Windows.Forms.dll,System.Drawing.dll -ErrorAction Stop
    }
    [Windows.Forms.Application]::EnableVisualStyles()
    $window = New-Object TrackballCAD.Portable.ViewTestWindow
    [Windows.Forms.Application]::Run($window)
    if ($null -ne $window.Result) { $window.Result | ConvertTo-Json -Depth 3 | Out-Host }
} catch {
    Write-Host "View test stopped: $($_.Exception.Message)"
    exit 1
} finally { if ($null -ne $window) { $window.Dispose() } }
