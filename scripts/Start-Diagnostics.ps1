# No administrator privileges or SolidWorks SDK needed for the standalone diagnostic.
param([ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug')
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT -or ![Environment]::Is64BitOperatingSystem) {
    throw 'TrackballCAD requires Windows x64.'
}
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$command = Get-Command MSBuild.exe -ErrorAction SilentlyContinue
$msbuild = $null
if ($command) { $msbuild = $command.Source }
else {
    $programFiles = [Environment]::GetFolderPath([Environment+SpecialFolder]::ProgramFilesX86)
    $vswhere = Join-Path $programFiles 'Microsoft Visual Studio/Installer/vswhere.exe'
    if (Test-Path -LiteralPath $vswhere -PathType Leaf) {
        $candidates = @(& $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe')
        if ($LASTEXITCODE -ne 0) { throw 'Visual Studio discovery failed.' }
        if ($candidates.Count -gt 0) { $msbuild = $candidates[0] }
    }
}
if (!$msbuild) {
    throw 'MSBuild not found. Install Visual Studio with .NET desktop development and the .NET Framework 4.8 targeting pack, then run this script in Developer PowerShell for Visual Studio.'
}
foreach ($project in @('src/TrackballCAD.UI/TrackballCAD.UI.csproj', 'tests/TrackballCAD.Tests/TrackballCAD.Tests.csproj')) {
    & $msbuild (Join-Path $root $project) /restore "/p:Configuration=$Configuration" /p:Platform=x64 /nologo
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $project. See MSBuild errors above." }
}
$tests = Join-Path $root "tests/TrackballCAD.Tests/bin/$Configuration/net48/TrackballCAD.Tests.exe"
& $tests
if ($LASTEXITCODE -ne 0) { throw 'Core tests failed. Diagnostic launch cancelled.' }
$diagnostic = Join-Path $root "src/TrackballCAD.UI/bin/$Configuration/net48/TrackballCAD.UI.exe"
if (!(Test-Path -LiteralPath $diagnostic -PathType Leaf)) { throw "Diagnostic executable not found: $diagnostic" }
Write-Host 'Move each mouse separately, compare handles and paths, then select the trackball.'
Start-Process -FilePath $diagnostic
