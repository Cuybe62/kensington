# Run from an elevated Windows PowerShell, with SolidWorks closed.
param([string]$Configuration = 'Debug', [switch]$Unregister)
$ErrorActionPreference = 'Stop'
$assembly = Join-Path $PSScriptRoot "../src/TrackballCAD.SolidWorks/bin/$Configuration/net48/TrackballCAD.SolidWorks.dll"
$assembly = (Resolve-Path $assembly).Path
$regasm = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/RegAsm.exe'
if ($Unregister) { & $regasm $assembly /unregister }
else { & $regasm $assembly /codebase }
if ($LASTEXITCODE -ne 0) { throw "RegAsm failed with exit code $LASTEXITCODE" }
