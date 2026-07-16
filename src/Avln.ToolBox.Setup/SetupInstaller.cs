using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http.Headers;

namespace Avln.ToolBox.Setup;

internal sealed class SetupInstaller
{
    private const string AppArchiveFileName = "AVLN.ToolBox.App.win-x64.zip";
    private const string ToolBoxReleaseTag = "toolbox-v0.1.0";
    private const string AppExecutableName = "AVLN.ToolBox.exe";
    private const string AppProcessName = "AVLN.ToolBox";
    private const int CloseTimeoutMilliseconds = 5000;

    private static readonly Uri AppArchiveDownloadUri = new(
        $"https://github.com/AVALON-IT-PLATFORM/AVLN_ToolBox/releases/download/{ToolBoxReleaseTag}/{AppArchiveFileName}");

    private readonly SetupLogger _logger;
    private readonly ShortcutService _shortcutService;

    public SetupInstaller(SetupLogger logger)
    {
        _logger = logger;
        _shortcutService = new ShortcutService(logger);
    }

    public SetupInstallResult Install()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var baseDirectory = Path.Combine(localAppData, "AVLN", "ToolBox");
        var appDirectory = Path.Combine(baseDirectory, "App");
        var tempRoot = Path.Combine(baseDirectory, "Temp", $"setup-{DateTime.UtcNow:yyyyMMddHHmmssfff}");
        var tempExtractDirectory = Path.Combine(tempRoot, "payload");
        var tempZipPath = Path.Combine(tempRoot, AppArchiveFileName);

        Directory.CreateDirectory(baseDirectory);
        Directory.CreateDirectory(tempRoot);

        try
        {
            DownloadPayload(tempZipPath);
            ZipFile.ExtractToDirectory(tempZipPath, tempExtractDirectory);

            var stagedExecutable = Path.Combine(tempExtractDirectory, AppExecutableName);
            if (!File.Exists(stagedExecutable))
            {
                throw new InvalidOperationException($"Архив {AppArchiveFileName} не содержит {AppExecutableName}.");
            }

            StopRunningToolBox();
            ReplaceAppDirectory(appDirectory, tempExtractDirectory);

            var appExecutablePath = Path.Combine(appDirectory, AppExecutableName);
            if (!File.Exists(appExecutablePath))
            {
                throw new InvalidOperationException($"После установки не найден {appExecutablePath}.");
            }

            CreateShortcuts(appExecutablePath);
            StartToolBox(appExecutablePath);

            return new SetupInstallResult(appExecutablePath);
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private void DownloadPayload(string targetZipPath)
    {
        _logger.Info($"Downloading ToolBox payload from {AppArchiveDownloadUri}.");

        Directory.CreateDirectory(Path.GetDirectoryName(targetZipPath)!);

        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AVLN.ToolBox.Setup", "1.0"));

        using var response = httpClient
            .GetAsync(AppArchiveDownloadUri, HttpCompletionOption.ResponseHeadersRead)
            .GetAwaiter()
            .GetResult();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Не удалось скачать {AppArchiveFileName}: HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
        }

        using var httpStream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
        using var fileStream = File.Create(targetZipPath);
        httpStream.CopyTo(fileStream);

        var fileInfo = new FileInfo(targetZipPath);
        if (!fileInfo.Exists || fileInfo.Length == 0)
        {
            throw new InvalidOperationException($"Скачанный архив пустой: {targetZipPath}.");
        }

        _logger.Info($"Downloaded ToolBox payload: {targetZipPath}, {fileInfo.Length} bytes.");
    }

    private void StopRunningToolBox()
    {
        var processes = Process.GetProcessesByName(AppProcessName);
        if (processes.Length == 0)
        {
            return;
        }

        _logger.Info($"Found running ToolBox processes: {processes.Length}.");

        foreach (var process in processes)
        {
            try
            {
                if (process.HasExited)
                {
                    continue;
                }

                _logger.Info($"Closing ToolBox process PID {process.Id}.");
                if (process.CloseMainWindow() && process.WaitForExit(CloseTimeoutMilliseconds))
                {
                    continue;
                }

                if (!process.HasExited)
                {
                    _logger.Warn($"Killing ToolBox process PID {process.Id}.");
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(CloseTimeoutMilliseconds);
                }
            }
            catch (Exception exception)
            {
                _logger.Warn($"Failed to stop ToolBox process PID {process.Id}: {exception.Message}");
            }
        }
    }

    private void ReplaceAppDirectory(string appDirectory, string stagedDirectory)
    {
        var backupDirectory = appDirectory + ".backup-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");

        try
        {
            if (Directory.Exists(appDirectory))
            {
                _logger.Info($"Moving current app directory to backup: {backupDirectory}");
                Directory.Move(appDirectory, backupDirectory);
            }

            _logger.Info($"Installing app directory: {appDirectory}");
            Directory.CreateDirectory(Path.GetDirectoryName(appDirectory)!);
            Directory.Move(stagedDirectory, appDirectory);

            TryDeleteDirectory(backupDirectory);
        }
        catch
        {
            if (!Directory.Exists(appDirectory) && Directory.Exists(backupDirectory))
            {
                _logger.Warn("Restoring previous app directory from backup.");
                Directory.Move(backupDirectory, appDirectory);
            }

            throw;
        }
    }

    private void CreateShortcuts(string appExecutablePath)
    {
        var iconPath = Path.Combine(Path.GetDirectoryName(appExecutablePath)!, "AVLN.ToolBox.ico");
        _shortcutService.CreateToolBoxShortcuts(appExecutablePath, iconPath);
    }

    private void StartToolBox(string appExecutablePath)
    {
        _logger.Info($"Starting ToolBox: {appExecutablePath}");

        var startInfo = new ProcessStartInfo
        {
            FileName = appExecutablePath,
            WorkingDirectory = Path.GetDirectoryName(appExecutablePath)!,
            UseShellExecute = true
        };

        Process.Start(startInfo);
    }

    private void TryDeleteDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return;
        }

        try
        {
            Directory.Delete(directoryPath, recursive: true);
        }
        catch (Exception exception)
        {
            _logger.Warn($"Failed to delete directory {directoryPath}: {exception.Message}");
        }
    }
}

internal sealed record SetupInstallResult(string AppExecutablePath);
