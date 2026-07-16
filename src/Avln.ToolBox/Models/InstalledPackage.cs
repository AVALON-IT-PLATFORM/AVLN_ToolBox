namespace Avln.ToolBox.Models;

public sealed class InstalledPackage
{
    public string Version { get; set; } = string.Empty;

    public string InstallPath { get; set; } = string.Empty;

    public DateTimeOffset InstalledAtUtc { get; set; }

    public List<string> Files { get; set; } = [];
}
