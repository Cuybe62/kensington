Set-StrictMode -Version Latest

function Test-ExistingSolidWorks {
    # Read-only probe on the calling STA. Never creates a COM server or opens a document.
    $application = $null
    $result = [ordered]@{ Connected = $false; Revision = $null; RunningProcessCount = 0; Detail = '' }
    try {
        if ([Threading.Thread]::CurrentThread.GetApartmentState() -ne 'STA') { throw 'COM probe requires STA.' }
        $result.RunningProcessCount = @(Get-Process -Name SLDWORKS -ErrorAction SilentlyContinue).Count
        $application = [Runtime.InteropServices.Marshal]::GetActiveObject('SldWorks.Application')
        # Official signature: string ISldWorks.RevisionNumber(). No document/view mutation.
        $result.Revision = [string]$application.RevisionNumber()
        $result.Connected = $true
        $result.Detail = 'Existing COM instance accessible; view methods not tested.'
        if ($result.RunningProcessCount -gt 1) {
            $result.Detail += ' Multiple instances detected: GetActiveObject does not select a specific instance. Keep only the intended instance for later tests.'
        }
    } catch {
        $exception = $_.Exception
        while ($null -ne $exception.InnerException) { $exception = $exception.InnerException }
        $result.Detail = ('COM probe failed (HRESULT 0x{0:X8}): {1}. Open SolidWorks normally under the same Windows account, wait until it is ready and close modal dialogs. If access is prohibited, ask school IT; do not elevate or register anything.' -f $exception.HResult, $exception.Message)
    } finally {
        # Release only the reference acquired by this probe, never FinalRelease or ExitApp.
        if ($null -ne $application -and [Runtime.InteropServices.Marshal]::IsComObject($application)) {
            [void][Runtime.InteropServices.Marshal]::ReleaseComObject($application)
        }
    }
    [pscustomobject]$result
}

Export-ModuleMember -Function Test-ExistingSolidWorks
