namespace Avln.ToolBox.Infrastructure;

public sealed class AppPaths
{
    public AppPaths()
    {
        RootDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AVLN",
            "ToolBox");

        SettingsFile = Path.Combine(RootDirectory, "settings.json");
        InstalledStateFile = Path.Combine(RootDirectory, "installed.json");
        LogDirectory = Path.Combine(RootDirectory, "Logs");
        TempDirectory = Path.Combine(RootDirectory, "Temp");
        DefaultInstallBasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Autodesk",
            "Revit",
            "Addins");

        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(TempDirectory);
    }

    public string RootDirectory { get; }

    public string SettingsFile { get; }

    public string InstalledStateFile { get; }

    public string LogDirectory { get; }

    public string TempDirectory { get; }

    public string DefaultInstallBasePath { get; }
}
