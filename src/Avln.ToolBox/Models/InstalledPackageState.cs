namespace Avln.ToolBox.Models;

public sealed class InstalledPackageState
{
    public Dictionary<string, InstalledPackage> Packages { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
