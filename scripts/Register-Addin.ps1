# Run from an elevated Windows PowerShell, with SolidWorks closed.
param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug', [switch]$Unregister)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT -or ![Environment]::Is64BitOperatingSystem) {
    throw 'TrackballCAD requires Windows x64.'
}
if (Get-Process -Name SLDWORKS -ErrorAction SilentlyContinue) {
    throw 'Close SolidWorks before registering or unregistering the add-in.'
}
$assembly = Join-Path $PSScriptRoot "../src/TrackballCAD.SolidWorks/bin/$Configuration/net48/TrackballCAD.SolidWorks.dll"
if (!(Test-Path -LiteralPath $assembly -PathType Leaf)) {
    throw "Add-in DLL not found: $assembly. Build TrackballCAD.sln in $Configuration | x64 with SolidWorksInteropPath set to the SDK api/redist folder. See README.md. Do not use Add-AppxPackage."
}
$assembly = (Resolve-Path $assembly).Path
foreach ($dependency in @('TrackballCAD.Navigation.dll', 'SolidWorks.Interop.sldworks.dll', 'SolidWorks.Interop.swpublished.dll')) {
    if (!(Test-Path -LiteralPath (Join-Path (Split-Path $assembly) $dependency) -PathType Leaf)) {
        throw "Missing dependency: $dependency. Rebuild the add-in and keep its output DLLs together."
    }
}
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
try {
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'Open Windows PowerShell as administrator: COM and SolidWorks registration writes to HKLM.'
    }
} finally {
    $identity.Dispose()
}
$regasm = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/RegAsm.exe'
if (!(Test-Path -LiteralPath $regasm -PathType Leaf)) {
    throw "RegAsm x64 not found: $regasm. Install .NET Framework 4.8."
}
Write-Host "Assembly: $assembly"
if ($Unregister) { & $regasm $assembly /unregister }
else { & $regasm $assembly /codebase }
if ($LASTEXITCODE -ne 0) { throw "RegAsm failed with exit code $LASTEXITCODE" }
if ($Unregister) { Write-Host 'TrackballCAD add-in unregistered.' }
else { Write-Host 'Registration complete. Open SolidWorks and enable TrackballCAD under Tools > Add-Ins.' }
