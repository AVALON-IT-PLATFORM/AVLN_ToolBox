namespace Avln.ToolBox.Models;

public sealed class ToolBoxSettings
{
    public bool AutoUpdate { get; set; } = true;

    public string InstallBasePath { get; set; } = string.Empty;
}
