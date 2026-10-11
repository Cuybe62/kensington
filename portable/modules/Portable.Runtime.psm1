Set-StrictMode -Version Latest

function Assert-PortableHost {
    if ($ExecutionContext.SessionState.LanguageMode -ne 'FullLanguage') {
        throw 'FullLanguage is required for Add-Type and Win32. School policy may prohibit this prototype; no workaround is attempted.'
    }
    if ($PSVersionTable.PSVersion.Major -ne 5 -or $PSVersionTable.PSVersion.Minor -ne 1 -or $PSVersionTable.PSEdition -ne 'Desktop') {
        throw 'Use Windows PowerShell 5.1 (powershell.exe), not PowerShell 7 / pwsh.'
    }
    if (![Environment]::Is64BitProcess) { throw 'Use 64-bit Windows PowerShell from Windows/System32, not SysWOW64.' }
    if ([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') { throw 'Start powershell.exe with -STA.' }
    if (![Environment]::UserInteractive) { throw 'An interactive Windows desktop session is required.' }
}

function Import-PortableBridge {
    Assert-PortableHost
    if ('TrackballCAD.Portable.RawInputBridge' -as [type]) { return }
    Add-Type -AssemblyName System.Windows.Forms -ErrorAction Stop
    Add-Type -AssemblyName System.Drawing -ErrorAction Stop
    $sourceRoot = Join-Path $PSScriptRoot '../src'
    $sources = @('RawInputBridge.cs', 'DiagnosticsWindow.cs', 'RotationEngine.cs', 'SolidWorksRotationAdapter.cs', 'NavigationWindow.cs') |
        ForEach-Object { Join-Path $sourceRoot $_ }
    Add-Type -Path $sources -ReferencedAssemblies System.dll,System.Core.dll,System.Windows.Forms.dll,System.Drawing.dll -ErrorAction Stop
}

Export-ModuleMember -Function Assert-PortableHost, Import-PortableBridge
