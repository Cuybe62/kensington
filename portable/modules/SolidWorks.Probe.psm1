Set-StrictMode -Version Latest

function Test-ExistingSolidWorks {
    # Read-only probe. No COM object crosses the PowerShell adapter boundary.
    $result = [ordered]@{
        Connected = $false; ObjectAcquired = $false; RevisionRead = $false
        Revision = $null; RunningProcessCount = 0; Stage = 'CompileProbe'
        HResult = $null; CleanupError = ''; Detail = ''
    }
    try {
        $result.RunningProcessCount = @(Get-Process -Name SLDWORKS -ErrorAction SilentlyContinue).Count
        if (!('TrackballCAD.Portable.SolidWorksProbe' -as [type])) {
            Add-Type -LiteralPath (Join-Path $PSScriptRoot '../src/SolidWorksProbe.cs') -ErrorAction Stop
        }
        $probe = [TrackballCAD.Portable.SolidWorksProbe]::Run()
        $result.ObjectAcquired = $probe.ObjectAcquired
        $result.RevisionRead = $probe.RevisionRead
        $result.Connected = $probe.ObjectAcquired -and $probe.RevisionRead
        $result.Revision = $probe.Revision
        $result.Stage = $probe.Stage
        $result.HResult = $probe.HResult
        $result.CleanupError = $probe.CleanupError
        if ($result.Connected) {
            $result.Detail = 'Existing COM object acquired and RevisionNumber read via C# InvokeMember. View methods not tested.'
        } else {
            $result.Detail = "Failure at $($probe.Stage) ($($probe.HResult)): $($probe.Error). ObjectAcquired=$($probe.ObjectAcquired). No installation or security settings changed."
        }
        if ($probe.CleanupError) { $result.Detail += " Reference cleanup: $($probe.CleanupError)" }
        if ($result.RunningProcessCount -gt 1) {
            $result.Detail += ' Multiple SolidWorks instances: GetActiveObject does not select a specific instance.'
        }
    } catch {
        $exception = $_.Exception
        while ($null -ne $exception.InnerException) { $exception = $exception.InnerException }
        $result.HResult = '0x{0:X8}' -f $exception.HResult
        $result.Detail = "Probe preparation failed: $($exception.Message). No workaround for school restrictions is attempted."
    }
    [pscustomobject]$result
}

Export-ModuleMember -Function Test-ExistingSolidWorks
