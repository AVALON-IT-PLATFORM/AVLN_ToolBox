using System.Runtime.InteropServices;

namespace Avln.ToolBox.Setup;

internal sealed class ShortcutService
{
    private const string ShortcutName = "AVLN ToolBox.lnk";

    private readonly SetupLogger _logger;

    public ShortcutService(SetupLogger logger)
    {
        _logger = logger;
    }

    public void CreateToolBoxShortcuts(string appExecutablePath, string iconPath)
    {
        var desktopDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var programsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Programs);

        TryCreateShortcut(Path.Combine(desktopDirectory, ShortcutName), appExecutablePath, iconPath);
        TryCreateShortcut(Path.Combine(programsDirectory, ShortcutName), appExecutablePath, iconPath);
    }

    private void TryCreateShortcut(string shortcutPath, string targetPath, string iconPath)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);
            CreateShortcut(shortcutPath, targetPath, iconPath);
            _logger.Info($"Shortcut created: {shortcutPath}");
        }
        catch (Exception exception)
        {
            _logger.Warn($"Failed to create shortcut {shortcutPath}: {exception.Message}");
        }
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string iconPath)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell COM object is unavailable.");

        object? shell = null;
        object? shortcut = null;

        try
        {
            shell = Activator.CreateInstance(shellType)
                ?? throw new InvalidOperationException("Failed to create WScript.Shell COM object.");

            shortcut = shellType.InvokeMember(
                "CreateShortcut",
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                shell,
                [shortcutPath])
                ?? throw new InvalidOperationException("Failed to create shortcut COM object.");

            SetShortcutProperty(shortcut, "TargetPath", targetPath);
            SetShortcutProperty(shortcut, "WorkingDirectory", Path.GetDirectoryName(targetPath)!);
            SetShortcutProperty(shortcut, "Description", "AVLN ToolBox");
            SetShortcutProperty(shortcut, "IconLocation", File.Exists(iconPath) ? iconPath : targetPath);

            shortcut.GetType().InvokeMember(
                "Save",
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                shortcut,
                null);
        }
        finally
        {
            ReleaseComObject(shortcut);
            ReleaseComObject(shell);
        }
    }

    private static void SetShortcutProperty(object shortcut, string propertyName, string value)
    {
        shortcut.GetType().InvokeMember(
            propertyName,
            System.Reflection.BindingFlags.SetProperty,
            null,
            shortcut,
            [value]);
    }

    private static void ReleaseComObject(object? comObject)
    {
        if (comObject != null && Marshal.IsComObject(comObject))
        {
            Marshal.FinalReleaseComObject(comObject);
        }
    }
}
