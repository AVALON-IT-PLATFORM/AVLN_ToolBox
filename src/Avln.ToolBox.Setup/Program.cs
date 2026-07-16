using System.Windows.Forms;

namespace Avln.ToolBox.Setup;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        SetupLogger? logger = null;

        try
        {
            logger = SetupLogger.CreateDefault();
            logger.Info("AVLN ToolBox setup started.");

            var installer = new SetupInstaller(logger);
            var result = installer.Install();

            logger.Info($"AVLN ToolBox setup finished. Installed to: {result.AppExecutablePath}");
            return 0;
        }
        catch (Exception exception)
        {
            logger?.Error(exception);

            var logPath = logger?.LogPath ?? SetupLogger.DefaultLogPath;
            MessageBox.Show(
                $"Не удалось установить AVLN ToolBox.\n\n{exception.Message}\n\nЛог: {logPath}",
                "AVLN ToolBox Setup",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

            return 1;
        }
    }
}
