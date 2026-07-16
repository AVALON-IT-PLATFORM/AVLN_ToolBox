using System.IO.Compression;
using System.Xml.Linq;
using Avln.ToolBox.Infrastructure;
using Avln.ToolBox.Models;
using Microsoft.Extensions.Logging;

namespace Avln.ToolBox.Services;

public sealed class PackageInstallService
{
    private const string AddinFileName = "Avalon.External.addin";
    private const string PluginDirectoryName = "AvalonPluginsExternal";
    private const string ExternalDllFileName = "Avalon.External.dll";

    private readonly HttpClient _httpClient;
    private readonly AppPaths _paths;
    private readonly InstalledStateService _installedStateService;
    private readonly ProcessDetectionService _processDetectionService;
    private readonly FileSystemService _fileSystem;
    private readonly ILogger<PackageInstallService> _logger;
    private readonly SemaphoreSlim _operationGate = new(1, 1);

    public PackageInstallService(
        HttpClient httpClient,
        AppPaths paths,
        InstalledStateService installedStateService,
        ProcessDetectionService processDetectionService,
        FileSystemService fileSystem,
        ILogger<PackageInstallService> logger)
    {
        _httpClient = httpClient;
        _paths = paths;
        _installedStateService = installedStateService;
        _processDetectionService = processDetectionService;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public async Task<PackageOperationResult> InstallAsync(
        ReleasePackage package,
        ToolBoxSettings settings,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            if (_processDetectionService.IsRevitRunning())
            {
                return PackageOperationResult.Failure(
                    "Закройте Autodesk Revit перед установкой или обновлением.");
            }

            var operationDirectory = Path.Combine(
                _paths.TempDirectory,
                $"{package.RevitYear}-{Guid.NewGuid():N}");
            var archivePath = Path.Combine(operationDirectory, package.AssetName);
            var stagingDirectory = Path.Combine(operationDirectory, "staging");
            var backupDirectory = Path.Combine(operationDirectory, "backup");
            var installDirectory = Path.Combine(settings.InstallBasePath, package.RevitYear.ToString());
            var copiedRelativeFiles = new List<string>();

            Directory.CreateDirectory(operationDirectory);
            Directory.CreateDirectory(stagingDirectory);
            Directory.CreateDirectory(backupDirectory);
            Directory.CreateDirectory(installDirectory);

            try
            {
                await DownloadAsync(package.DownloadUri, archivePath, cancellationToken);
                if (!File.Exists(archivePath) || new FileInfo(archivePath).Length == 0)
                {
                    return PackageOperationResult.Failure("Не удалось скачать пакет.");
                }

                ZipFile.ExtractToDirectory(archivePath, stagingDirectory, true);
                var packageRoot = ResolvePackageRoot(stagingDirectory);
                ValidatePackage(packageRoot);
                PatchAddinManifest(packageRoot, installDirectory);
                var packageFiles = GetPackageFiles(packageRoot);

                var state = await _installedStateService.LoadAsync(cancellationToken);
                state.Packages.TryGetValue(package.RevitYear.ToString(), out var previousPackage);

                EnsureNoForeignConflicts(packageFiles, installDirectory, previousPackage);
                BackupAndRemovePreviousFiles(previousPackage, backupDirectory);

                try
                {
                    CopyPackageFiles(packageRoot, installDirectory, packageFiles, copiedRelativeFiles);
                }
                catch
                {
                    DeleteFiles(installDirectory, copiedRelativeFiles);
                    RestoreBackup(backupDirectory, installDirectory);
                    throw;
                }

                state.Packages[package.RevitYear.ToString()] = new InstalledPackage
                {
                    Version = package.Version,
                    InstallPath = installDirectory,
                    InstalledAtUtc = DateTimeOffset.UtcNow,
                    Files = copiedRelativeFiles
                };

                await _installedStateService.SaveAsync(state, cancellationToken);
                return PackageOperationResult.Success("Пакет установлен.");
            }
            catch (InvalidDataException exception)
            {
                _logger.LogError(exception, "Пакет R{RevitYear} повреждён", package.RevitYear);
                TryRollback(backupDirectory, installDirectory, copiedRelativeFiles);
                return PackageOperationResult.Failure("Пакет повреждён.");
            }
            catch (HttpRequestException exception)
            {
                _logger.LogError(exception, "Не удалось скачать пакет R{RevitYear}", package.RevitYear);
                TryRollback(backupDirectory, installDirectory, copiedRelativeFiles);
                return PackageOperationResult.Failure("Не удалось скачать пакет.");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Ошибка установки пакета R{RevitYear}", package.RevitYear);
                var rollbackSucceeded = TryRollback(backupDirectory, installDirectory, copiedRelativeFiles);
                return PackageOperationResult.Failure(rollbackSucceeded
                    ? "Не удалось установить пакет."
                    : "Не удалось восстановить предыдущую версию.");
            }
            finally
            {
                TryDeleteDirectory(operationDirectory);
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public async Task<PackageOperationResult> UninstallAsync(
        int revitYear,
        CancellationToken cancellationToken = default)
    {
        await _operationGate.WaitAsync(cancellationToken);
        try
        {
            if (_processDetectionService.IsRevitRunning())
            {
                return PackageOperationResult.Failure(
                    "Закройте Autodesk Revit перед удалением.");
            }

            var state = await _installedStateService.LoadAsync(cancellationToken);
            if (!state.Packages.TryGetValue(revitYear.ToString(), out var package))
            {
                return PackageOperationResult.Success("Пакет уже удалён.");
            }

            try
            {
                DeleteFiles(package.InstallPath, package.Files);
                state.Packages.Remove(revitYear.ToString());
                await _installedStateService.SaveAsync(state, cancellationToken);
                return PackageOperationResult.Success("Пакет удалён.");
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Ошибка удаления пакета R{RevitYear}", revitYear);
                return PackageOperationResult.Failure("Не удалось удалить пакет.");
            }
        }
        finally
        {
            _operationGate.Release();
        }
    }

    private async Task DownloadAsync(Uri downloadUri, string archivePath, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(
            downloadUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = File.Create(archivePath);
        await source.CopyToAsync(destination, cancellationToken);
    }

    private static string ResolvePackageRoot(string stagingDirectory)
    {
        var files = Directory.GetFiles(stagingDirectory);
        var directories = Directory.GetDirectories(stagingDirectory);
        return files.Length == 0 && directories.Length == 1
            ? directories[0]
            : stagingDirectory;
    }

    private static void ValidatePackage(string packageRoot)
    {
        var addinPath = Path.Combine(packageRoot, AddinFileName);
        var dllPath = Path.Combine(packageRoot, PluginDirectoryName, ExternalDllFileName);

        if (!File.Exists(addinPath) || !File.Exists(dllPath))
        {
            throw new InvalidDataException(
                $"Package root must contain {AddinFileName} and {PluginDirectoryName}\\{ExternalDllFileName}.");
        }
    }

    private static void PatchAddinManifest(string packageRoot, string installDirectory)
    {
        var addinPath = Path.Combine(packageRoot, AddinFileName);
        var targetAssemblyPath = Path.Combine(installDirectory, PluginDirectoryName, ExternalDllFileName);

        XDocument document;
        try
        {
            document = XDocument.Load(addinPath, LoadOptions.PreserveWhitespace);
        }
        catch (Exception exception)
        {
            throw new InvalidDataException($"{AddinFileName} is not a valid XML file.", exception);
        }

        var assemblyElement = document
            .Descendants()
            .FirstOrDefault(element =>
                string.Equals(element.Name.LocalName, "Assembly", StringComparison.OrdinalIgnoreCase));
        if (assemblyElement is null)
        {
            throw new InvalidDataException($"{AddinFileName} must contain Assembly element.");
        }

        assemblyElement.Value = targetAssemblyPath;
        document.Save(addinPath);
    }

    private static IReadOnlyList<string> GetPackageFiles(string packageRoot)
    {
        return Directory
            .EnumerateFiles(packageRoot, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(packageRoot, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void EnsureNoForeignConflicts(
        IEnumerable<string> packageFiles,
        string installDirectory,
        InstalledPackage? previousPackage)
    {
        var ownedFiles = previousPackage?.Files.ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var relativePath in packageFiles)
        {
            var destinationPath = GetSafePath(
                installDirectory,
                relativePath,
                "Package contains an invalid path.");

            if (File.Exists(destinationPath) && !ownedFiles.Contains(relativePath))
            {
                throw new IOException($"Destination file is not owned by ToolBox: {relativePath}");
            }
        }
    }

    private void BackupAndRemovePreviousFiles(InstalledPackage? package, string backupDirectory)
    {
        if (package is null)
        {
            return;
        }

        foreach (var relativePath in package.Files)
        {
            var sourcePath = GetSafePath(
                package.InstallPath,
                relativePath,
                "Installed state contains an invalid path.");
            if (!File.Exists(sourcePath))
            {
                continue;
            }

            var backupPath = GetSafePath(
                backupDirectory,
                relativePath,
                "Installed state contains an invalid path.");
            _fileSystem.CopyFile(sourcePath, backupPath);
            File.Delete(sourcePath);
        }
    }

    private void CopyPackageFiles(
        string packageRoot,
        string installDirectory,
        IEnumerable<string> packageFiles,
        ICollection<string> copiedFiles)
    {
        foreach (var relativePath in packageFiles)
        {
            var sourcePath = GetSafePath(
                packageRoot,
                relativePath,
                "Package contains an invalid path.");
            var destinationPath = GetSafePath(
                installDirectory,
                relativePath,
                "Package contains an invalid path.");

            _fileSystem.CopyFile(sourcePath, destinationPath);
            copiedFiles.Add(relativePath);
        }
    }

    private void RestoreBackup(string backupDirectory, string installDirectory)
    {
        if (!Directory.Exists(backupDirectory))
        {
            return;
        }

        foreach (var backupPath in Directory.EnumerateFiles(backupDirectory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(backupDirectory, backupPath);
            var destinationPath = GetSafePath(
                installDirectory,
                relativePath,
                "Backup contains an invalid path.");
            _fileSystem.CopyFile(backupPath, destinationPath);
        }
    }

    private bool TryRollback(string backupDirectory, string installDirectory, IReadOnlyCollection<string> copiedFiles)
    {
        try
        {
            DeleteFiles(installDirectory, copiedFiles);
            RestoreBackup(backupDirectory, installDirectory);
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogCritical(exception, "Rollback пакета завершился ошибкой");
            return false;
        }
    }

    private void DeleteFiles(string installDirectory, IEnumerable<string> relativeFiles)
    {
        foreach (var relativePath in relativeFiles.OrderByDescending(path => path.Length))
        {
            var fullPath = GetSafePath(
                installDirectory,
                relativePath,
                "Installed state contains an invalid path.");

            _fileSystem.DeleteFileIfExists(fullPath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                _fileSystem.DeleteEmptyDirectories(directory, installDirectory);
            }
        }
    }

    private static string GetSafePath(string rootDirectory, string relativePath, string errorMessage)
    {
        var root = Path.GetFullPath(rootDirectory).TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(rootDirectory, relativePath));

        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(errorMessage);
        }

        return fullPath;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
            // Temporary cleanup failure must not hide the operation result.
        }
    }
}
