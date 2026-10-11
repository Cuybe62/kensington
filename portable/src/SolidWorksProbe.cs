using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace TrackballCAD.Portable
{
    public sealed class ComProbeResult
    {
        public bool ObjectAcquired { get; internal set; }
        public bool RevisionRead { get; internal set; }
        public string Revision { get; internal set; }
        public string Stage { get; internal set; }
        public string HResult { get; internal set; }
        public string Error { get; internal set; }
        public string CleanupError { get; internal set; }
    }
    public static class SolidWorksProbe
    {
        // Keep the RCW in C#: PowerShell receives only plain diagnostic values.
        // Avoids PowerShell member discovery, not operating system permissions.
        public static ComProbeResult Run()
        {
            ComProbeResult result = new ComProbeResult { Stage = "Host", Error = "", CleanupError = "" };
            object application = null;
            try
            {
                if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
                    throw new InvalidOperationException("COM probe requires STA.");
                result.Stage = "GetActiveObject";
                application = Marshal.GetActiveObject("SldWorks.Application");
                if (application == null) throw new InvalidOperationException("COM returned no object.");
                result.ObjectAcquired = true;
                result.Stage = "RevisionNumber.InvokeMember";
                object revision = application.GetType().InvokeMember("RevisionNumber",
                    BindingFlags.InvokeMethod, null, application, new object[0]);
                if (!(revision is string) || string.IsNullOrWhiteSpace((string)revision))
                    throw new InvalidOperationException("RevisionNumber returned no revision string.");
                result.Revision = (string)revision;
                result.RevisionRead = true;
                result.Stage = "Complete";
            }
            catch (Exception ex)
            {
                while (ex.InnerException != null) ex = ex.InnerException;
                result.HResult = "0x" + ex.HResult.ToString("X8");
                result.Error = ex.GetType().Name + ": " + ex.Message;
            }
            finally
            {
                if (application != null && Marshal.IsComObject(application))
                {
                    try { Marshal.ReleaseComObject(application); }
                    catch (Exception ex) { result.CleanupError = ex.Message; }
                }
            }
            return result;
        }
    }
}
