using System.Text.Json;
using Avln.ToolBox.Infrastructure;
using Avln.ToolBox.Models;
using Microsoft.Extensions.Logging;

namespace Avln.ToolBox.Services;

public sealed class InstalledStateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly AppPaths _paths;
    private readonly ILogger<InstalledStateService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public InstalledStateService(AppPaths paths, ILogger<InstalledStateService> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public async Task<InstalledPackageState> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_paths.InstalledStateFile))
            {
                return new InstalledPackageState();
            }

            await using var stream = File.OpenRead(_paths.InstalledStateFile);
            return await JsonSerializer.DeserializeAsync<InstalledPackageState>(stream, JsonOptions, cancellationToken)
                ?? new InstalledPackageState();
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось прочитать installed.json");
            return new InstalledPackageState();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(InstalledPackageState state, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_paths.RootDirectory);
            var tempFile = _paths.InstalledStateFile + ".tmp";
            await using (var stream = File.Create(tempFile))
            {
                await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken);
            }

            File.Move(tempFile, _paths.InstalledStateFile, true);
        }
        finally
        {
            _gate.Release();
        }
    }

    public bool IsHealthy(InstalledPackage package)
    {
        if (string.IsNullOrWhiteSpace(package.InstallPath) || package.Files.Count == 0)
        {
            return false;
        }

        return package.Files.All(relativePath =>
            File.Exists(Path.Combine(package.InstallPath, relativePath)));
    }
}
