namespace Avln.ToolBox.Models;

public enum PackageStatus
{
    VersionUnavailable,
    NotInstalled,
    UpToDate,
    UpdateAvailable,
    BrokenInstallation,
    InstalledWithoutRelease,
    Busy,
    Error
}
