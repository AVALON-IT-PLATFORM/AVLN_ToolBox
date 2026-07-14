namespace Avln.ToolBox.Models;

public sealed record ReleasePackage(
    int RevitYear,
    string Version,
    string AssetName,
    Uri DownloadUri);
