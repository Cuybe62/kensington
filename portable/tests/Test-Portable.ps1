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

function New-RawPacket {
    param([long]$Device, [int]$X, [int]$Y, [int]$Tick, [bool]$Absolute = $false)
    $packet = New-Object TrackballCAD.Portable.RawMousePacket
    $packet.Device = $Device; $packet.X = $X; $packet.Y = $Y; $packet.MessageTick = $Tick; $packet.Absolute = $Absolute
    return $packet
}
$rotationSettings = New-Object TrackballCAD.Portable.RotationSettings
$engine = New-Object TrackballCAD.Portable.RotationEngine -ArgumentList $rotationSettings
$engine.Select(11)
Assert-Result (!$engine.UpdateGate($true, $true, 100)) 'Holding modifier while enabling cannot arm navigation'
[void]$engine.UpdateGate($true, $false, 101)
Assert-Result ($engine.UpdateGate($true, $true, 102)) 'Release and fresh modifier press activate rotation'
$engine.Add((New-RawPacket 12 400 400 102), 102)
Assert-Result (!$engine.Consume(102).HasMotion) 'Other mouse cannot rotate the view'
$engine.Add((New-RawPacket 11 400 400 101), 102)
Assert-Result (!$engine.Consume(102).HasMotion) 'Queued movement from before modifier press is discarded'
$engine.Add((New-RawPacket 11 10 -20 103), 103)
$command = $engine.Consume(104)
Assert-Result ([Math]::Abs($command.Horizontal - 0.02) -lt 0.000001 -and [Math]::Abs($command.Vertical + 0.04) -lt 0.000001) 'Linear sensitivity and X/Y mapping'
Assert-Result (!$engine.Consume(104).HasMotion) 'Rotation deltas are consumed only once'
$engine.Add((New-RawPacket 11 50000 50000 105 $true), 105)
Assert-Result (!$engine.Consume(105).HasMotion) 'Absolute device cannot generate rotation'
$engine.Add((New-RawPacket 11 20 20 105), 300)
Assert-Result (!$engine.Consume(300).HasMotion) 'Old Windows message timestamp is rejected'
$engine.Add((New-RawPacket 11 20 20 301), 301)
Assert-Result (!$engine.Consume(500).HasMotion) 'Motion cannot survive a stalled update loop'
$engine.Add((New-RawPacket 11 20 20 501), 501)
[void]$engine.UpdateGate($false, $true, 502)
Assert-Result (!$engine.Consume(502).HasMotion) 'Focus loss clears pending motion'
Assert-Result (!$engine.UpdateGate($true, $true, 503)) 'Focus regain with held modifier never re-arms'
[void]$engine.UpdateGate($true, $false, 504)
[void]$engine.UpdateGate($true, $true, 505)
$rotationSettings.InvertX = $true
$engine.Add((New-RawPacket 11 10 0 505), 505)
Assert-Result ([Math]::Abs($engine.Consume(505).Horizontal + 0.02) -lt 0.000001) 'Horizontal inversion'
$engine.Add((New-RawPacket 11 100000 100000 506), 506)
$limited = $engine.Consume(506)
Assert-Result ($limited.Horizontal -eq -0.15 -and $limited.Vertical -eq 0.15) 'Per-update angular cap prevents a huge jump'
Assert-Result ([TrackballCAD.Portable.RotationEngine]::IsFresh(2147483640, -2147483640)) 'Tick count wrap is handled'
$engine.Select(12)
Assert-Result (!$engine.Active -and !$engine.Consume(507).HasMotion) 'Device selection cancels gesture and motion'

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
    $settings = Read-RotationSettings -Configuration $restored
    $settings.UpdateHz = 45
    Save-RotationSettings -Path $path -Configuration $restored -Settings $settings -Device $device
    $roundTrip = Read-PortableConfiguration -Path $path
    Assert-Result ((Read-RotationSettings -Configuration $roundTrip).UpdateHz -eq 45) 'Rotation settings JSON round trip'
    $legacy = '{"schemaVersion":1,"diagnosticsRefreshMs":100,"selectedDevice":null}' | ConvertFrom-Json
    Assert-Result ((Read-RotationSettings -Configuration $legacy).ActivationKey -eq 'LeftShift') 'Older diagnostic config uses safe rotation defaults'
    $bad = '{"schemaVersion":1,"diagnosticsRefreshMs":0,"selectedDevice":null}'
    Set-Content -LiteralPath $path -Value $bad -Encoding UTF8
    $rejected = $false
    try { Read-PortableConfiguration -Path $path | Out-Null } catch { $rejected = $true }
    Assert-Result $rejected 'Out-of-range refresh interval rejected'
} finally {
    [IO.Directory]::Delete($temporaryDirectory, $true)
}
Write-Host "$passed checks passed. This does not validate physical Raw Input or SolidWorks."
