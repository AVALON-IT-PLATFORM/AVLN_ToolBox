using System.Text.Json;
using Avln.ToolBox.Infrastructure;
using Avln.ToolBox.Models;
using Microsoft.Extensions.Logging;

namespace Avln.ToolBox.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly AppPaths _paths;
    private readonly ILogger<SettingsService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SettingsService(AppPaths paths, ILogger<SettingsService> logger)
    {
        _paths = paths;
        _logger = logger;
    }

    public async Task<ToolBoxSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_paths.SettingsFile))
            {
                return CreateDefault();
            }

            await using var stream = File.OpenRead(_paths.SettingsFile);
            var settings = await JsonSerializer.DeserializeAsync<ToolBoxSettings>(stream, JsonOptions, cancellationToken)
                ?? CreateDefault();

            if (string.IsNullOrWhiteSpace(settings.InstallBasePath))
            {
                settings.InstallBasePath = _paths.DefaultInstallBasePath;
            }

            return settings;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось прочитать settings.json");
            return CreateDefault();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(ToolBoxSettings settings, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(_paths.RootDirectory);
            var tempFile = _paths.SettingsFile + ".tmp";
            await using (var stream = File.Create(tempFile))
            {
                await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
            }

            File.Move(tempFile, _paths.SettingsFile, true);
        }
        finally
        {
            _gate.Release();
        }
    }

    private ToolBoxSettings CreateDefault() => new()
    {
        AutoUpdate = true,
        InstallBasePath = _paths.DefaultInstallBasePath
    };
}
