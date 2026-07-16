using Avln.ToolBox.Models;
using Avln.ToolBox.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Avln.ToolBox.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [ObservableProperty]
    private bool autoUpdate;

    [ObservableProperty]
    private string installBasePath = string.Empty;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.LoadAsync(cancellationToken);
        AutoUpdate = settings.AutoUpdate;
        InstallBasePath = settings.InstallBasePath;
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var settings = new ToolBoxSettings
        {
            AutoUpdate = AutoUpdate,
            InstallBasePath = InstallBasePath
        };

        return _settingsService.SaveAsync(settings, cancellationToken);
    }
}
