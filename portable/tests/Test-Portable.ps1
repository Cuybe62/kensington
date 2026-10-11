# Run with Windows PowerShell 5.1 x64 -STA. No SolidWorks, admin or physical input needed.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
Import-Module (Join-Path $root 'modules/Portable.Runtime.psm1') -ErrorAction Stop
Import-Module (Join-Path $root 'modules/Portable.Configuration.psm1') -ErrorAction Stop
Assert-PortableHost
Import-PortableBridge
Add-Type -LiteralPath (Join-Path $root 'src/SolidWorksProbe.cs') -ErrorAction Stop
Add-Type -LiteralPath (Join-Path $root 'src/SolidWorksViewTest.cs') -ReferencedAssemblies System.dll,System.Core.dll,System.Windows.Forms.dll,System.Drawing.dll -ErrorAction Stop
$passed = 0
function Assert-Result {
    param([bool]$Condition, [string]$Name)
    if (!$Condition) { throw "FAIL: $Name" }
    $script:passed++
    Write-Host "PASS: $Name"
}

# The native layouts are checked without calling a Win32 function.
$assembly = [TrackballCAD.Portable.RawInputBridge].Assembly
$header = $assembly.GetType('TrackballCAD.Portable.Native+Header')
$mouse = $assembly.GetType('TrackballCAD.Portable.Native+Mouse')
Assert-Result ([Runtime.InteropServices.Marshal]::SizeOf($header) -eq 24) 'RAWINPUTHEADER is 24 bytes on x64'
Assert-Result ([Runtime.InteropServices.Marshal]::SizeOf($mouse) -eq 24) 'RAWMOUSE is 24 bytes'
Assert-Result ([Runtime.InteropServices.Marshal]::OffsetOf($mouse, 'ButtonFlags').ToInt32() -eq 4) 'Aligned button union'
Assert-Result ([Runtime.InteropServices.Marshal]::OffsetOf($mouse, 'X').ToInt32() -eq 12) 'X offset'

$first = New-Object TrackballCAD.Portable.MouseAccumulator
$second = New-Object TrackballCAD.Portable.MouseAccumulator
$first.Add(7, -4, 1, 0, $false)
$first.Add(-2, 8, 0, 0, $false)
$second.Add(10, 20, 0, 0, $false)
$sample = $first.Consume()
Assert-Result ($sample.DeltaX -eq 5 -and $sample.DeltaY -eq 4 -and $sample.IntervalEvents -eq 2) 'Relative deltas accumulated'
Assert-Result ($sample.HeldButtons -eq 1 -and $sample.IntervalButtonFlags -eq 1) 'Button-down state survives movement-only packets'
Assert-Result ($second.Consume().DeltaX -eq 10) 'Independent accumulators do not mix devices'
$empty = $first.Consume()
Assert-Result ($empty.DeltaX -eq 0 -and $empty.IntervalEvents -eq 0 -and $empty.Events -eq 2) 'Consume resets interval, preserves total'
$first.Add(0, 0, 2, 0, $false)
Assert-Result ($first.Consume().HeldButtons -eq 0) 'Button-up releases state'
$first.Add(50000, 10000, 0, 0, $true)
$absolute = $first.Consume()
Assert-Result ($absolute.Absolute -and $absolute.LastX -eq 50000 -and $absolute.DeltaX -eq 0) 'Absolute coordinates never accumulate as relative deltas'
$first.Add(0, 0, 0x400, -30, $false)
$first.Add(0, 0, 0x800, 15, $false)
$wheel = $first.Consume()
Assert-Result ($wheel.Wheel -eq -30 -and $wheel.HorizontalWheel -eq 15) 'Signed high-resolution wheel values preserved'
Assert-Result ($wheel.WheelObserved -and $wheel.HorizontalWheelObserved) 'Wheel support is based on observed events'
$first.Add(0, 0, 1, 0, $false)
$first.Add(0, 0, 2, 0, $false)
$click = $first.Consume()
Assert-Result ($click.HeldButtons -eq 0 -and $click.IntervalButtonFlags -eq 3) 'Short click remains visible between UI ticks'

$temporaryDirectory = Join-Path ([IO.Path]::GetTempPath()) ('TrackballCAD-test-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($temporaryDirectory)
try {
    $path = Join-Path $temporaryDirectory 'config.json'
    Copy-Item -LiteralPath (Join-Path $root 'config.json') -Destination $path
    $configuration = Read-PortableConfiguration -Path $path
    Assert-Result ($configuration.diagnosticsRefreshMs -ge 50) 'Configuration parses'
    $device = [pscustomobject]@{ Path = '\\?\HID#VID_1234&PID_ABCD#instance'; Vid = '1234'; Pid = 'ABCD'; Label = 'test' }
    Save-PortableDevice -Path $path -Configuration $configuration -Device $device
    $restored = Read-PortableConfiguration -Path $path
    Assert-Result ($restored.selectedDevice.path -eq $device.Path) 'Device path JSON round trip'
    $bad = '{"schemaVersion":1,"diagnosticsRefreshMs":0,"selectedDevice":null}'
    Set-Content -LiteralPath $path -Value $bad -Encoding UTF8
    $rejected = $false
    try { Read-PortableConfiguration -Path $path | Out-Null } catch { $rejected = $true }
    Assert-Result $rejected 'Out-of-range refresh interval rejected'
} finally {
    [IO.Directory]::Delete($temporaryDirectory, $true)
}
Write-Host "$passed checks passed. This does not validate physical Raw Input or SolidWorks."
