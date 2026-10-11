using System.Security.Principal;
namespace TrackballCAD.Navigation
{
    public static class PipeAddress
    {
        public static string ForProcess(int pid) => "TrackballCAD.v1." + WindowsIdentity.GetCurrent().User.Value + "." + pid;
    }
}
