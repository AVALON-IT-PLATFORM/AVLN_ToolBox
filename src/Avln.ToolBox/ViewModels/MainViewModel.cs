using System.Collections.ObjectModel;
using Avln.ToolBox.Infrastructure;
using Avln.ToolBox.Models;
using Avln.ToolBox.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Avln.ToolBox.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;
    private readonly InstalledStateService _installedStateService;
    private readonly RevitDetectionService _revitDetectionService;
    private readonly GitHubReleaseService _releaseService;
    private readonly PackageInstallService _packageInstallService;
    private readonly ProcessDetectionService _processDetectionService;
    private readonly ILogger<MainViewModel> _logger;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    private ToolBoxSettings _settings = new();
    private InstalledPackageState _installedState = new();
    private IReadOnlyDictionary<int, ReleasePackage> _releases = new Dictionary<int, ReleasePackage>();

    public MainViewModel(
        SettingsService settingsService,
        InstalledStateService installedStateService,
        RevitDetectionService revitDetectionService,
        GitHubReleaseService releaseService,
        PackageInstallService packageInstallService,
        ProcessDetectionService processDetectionService,
        ILogger<MainViewModel> logger)
    {
        _settingsService = settingsService;
        _installedStateService = installedStateService;
        _revitDetectionService = revitDetectionService;
        _releaseService = releaseService;
        _packageInstallService = packageInstallService;
        _processDetectionService = processDetectionService;
        _logger = logger;
    }

    public ObservableCollection<RevitVersionItemViewModel> Versions { get; } = [];

    public event EventHandler? SettingsRequested;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool hasVersions;

    [RelayCommand]
    private void OpenSettings() => SettingsRequested?.Invoke(this, EventArgs.Empty);

    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        RefreshAsync(runAutoUpdate: true, cancellationToken);

    public async Task RefreshAsync(bool runAutoUpdate, CancellationToken cancellationToken = default)
    {
        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            IsLoading = true;
            _settings = await _settingsService.LoadAsync(cancellationToken);
            _installedState = await _installedStateService.LoadAsync(cancellationToken);
            _releases = await _releaseService.GetLatestPackagesAsync(cancellationToken);

            var installations = await _revitDetectionService.DetectAsync(
                _settings,
                _installedState,
                cancellationToken);

            Versions.Clear();
            foreach (var installation in installations)
            {
                var item = new RevitVersionItemViewModel(
                    installation.Year,
                    HandleActionAsync,
                    HandleDeleteAsync);
                ApplyState(item);
                Versions.Add(item);
            }

            HasVersions = Versions.Count > 0;

            if (runAutoUpdate && _settings.AutoUpdate)
            {
                await RunAutoUpdateAsync(cancellationToken);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Не удалось загрузить состояние ToolBox");
        }
        finally
        {
            IsLoading = false;
            _refreshGate.Release();
        }
    }

    private async Task HandleActionAsync(RevitVersionItemViewModel item)
    {
        if (!_releases.TryGetValue(item.Year, out var package))
        {
            item.SetState(PackageStatus.Error, "Версия пока недоступна", installed: item.IsInstalled);
            return;
        }

        var retryAction = item.ActionText;
        var operationText = item.Status == PackageStatus.UpdateAvailable ? "Обновление…" : "Установка…";
        item.SetState(PackageStatus.Busy, operationText, installed: item.IsInstalled);

        var result = await _packageInstallService.InstallAsync(package, _settings);
        if (!result.IsSuccess)
        {
            item.SetState(PackageStatus.Error, result.Message, retryAction, item.IsInstalled);
            return;
        }

        _installedState = await _installedStateService.LoadAsync();
        ApplyState(item);
    }

    private async Task HandleDeleteAsync(RevitVersionItemViewModel item)
    {
        item.SetState(PackageStatus.Busy, "Удаление…", installed: true);
        var result = await _packageInstallService.UninstallAsync(item.Year);
        if (!result.IsSuccess)
        {
            item.SetState(PackageStatus.Error, result.Message, installed: true);
            return;
        }

        await RefreshAsync(runAutoUpdate: false);
    }

    private async Task RunAutoUpdateAsync(CancellationToken cancellationToken)
    {
        var updateItems = Versions
            .Where(item => item.Status == PackageStatus.UpdateAvailable)
            .ToArray();

        if (updateItems.Length == 0)
        {
            return;
        }

        if (_processDetectionService.IsRevitRunning())
        {
            foreach (var item in updateItems)
            {
                item.SetState(
                    PackageStatus.UpdateAvailable,
                    "Обновление будет установлено после закрытия Revit",
                    "Обновить",
                    installed: true);
            }

            return;
        }

        foreach (var item in updateItems)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await HandleActionAsync(item);
        }
    }

    private void ApplyState(RevitVersionItemViewModel item)
    {
        _installedState.Packages.TryGetValue(item.Year.ToString(), out var installedPackage);
        _releases.TryGetValue(item.Year, out var releasePackage);

        if (installedPackage is null)
        {
            if (releasePackage is null)
            {
                item.SetState(PackageStatus.VersionUnavailable, "Версия пока недоступна");
            }
            else
            {
                item.SetState(PackageStatus.NotInstalled, "Не установлено", "Установить");
            }

            return;
        }

        if (!_installedStateService.IsHealthy(installedPackage))
        {
            item.SetState(
                PackageStatus.BrokenInstallation,
                "Установка повреждена",
                releasePackage is null ? null : "Восстановить",
                installed: true);
            return;
        }

        if (releasePackage is null)
        {
            item.SetState(
                PackageStatus.InstalledWithoutRelease,
                $"Установлена версия · {installedPackage.Version}",
                installed: true);
            return;
        }

        if (VersionComparer.IsNewer(releasePackage.Version, installedPackage.Version))
        {
            item.SetState(
                PackageStatus.UpdateAvailable,
                $"Доступно обновление · {releasePackage.Version}",
                "Обновить",
                installed: true);
            return;
        }

        item.SetState(
            PackageStatus.UpToDate,
            $"Актуальная версия · {installedPackage.Version}",
            installed: true);
    }

}
