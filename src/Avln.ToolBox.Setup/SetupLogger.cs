using System.Text;

namespace Avln.ToolBox.Setup;

internal sealed class SetupLogger
{
    private readonly object _syncRoot = new();

    private SetupLogger(string logPath)
    {
        LogPath = logPath;
    }

    public string LogPath { get; }

    public static string DefaultLogPath
    {
        get
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "AVLN", "ToolBox", "Logs", "setup.log");
        }
    }

    public static SetupLogger CreateDefault()
    {
        var logPath = DefaultLogPath;
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        return new SetupLogger(logPath);
    }

    public void Info(string message) => Write("INFO", message);

    public void Warn(string message) => Write("WARN", message);

    public void Error(Exception exception) => Write("ERROR", exception.ToString());

    private void Write(string level, string message)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} | {level} | {message}{Environment.NewLine}";

        lock (_syncRoot)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, line, Encoding.UTF8);
        }
    }
}
