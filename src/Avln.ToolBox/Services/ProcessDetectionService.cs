using System.Diagnostics;

namespace Avln.ToolBox.Services;

public sealed class ProcessDetectionService
{
    public bool IsRevitRunning()
    {
        try
        {
            return Process.GetProcessesByName("Revit").Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
