using Avln.ToolBox.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Avln.ToolBox.ViewModels;

public partial class RevitVersionItemViewModel : ObservableObject
{
    private readonly Func<RevitVersionItemViewModel, Task> _action;
    private readonly Func<RevitVersionItemViewModel, Task> _delete;

    public RevitVersionItemViewModel(
        int year,
        Func<RevitVersionItemViewModel, Task> action,
        Func<RevitVersionItemViewModel, Task> delete)
    {
        Year = year;
        _action = action;
        _delete = delete;
    }

    public int Year { get; }

    [ObservableProperty]
    private PackageStatus status;

    [ObservableProperty]
    private string statusText = string.Empty;

    [ObservableProperty]
    private string actionText = string.Empty;

    [ObservableProperty]
    private bool hasAction;

    [ObservableProperty]
    private bool isInstalled;

    [ObservableProperty]
    private bool isBusy;

    [RelayCommand]
    private async Task ExecuteActionAsync()
    {
        if (!HasAction || IsBusy)
        {
            return;
        }

        await _action(this);
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (!IsInstalled || IsBusy)
        {
            return;
        }

        await _delete(this);
    }

    public void SetState(
        PackageStatus packageStatus,
        string text,
        string? action = null,
        bool installed = false)
    {
        Status = packageStatus;
        StatusText = text;
        ActionText = action ?? string.Empty;
        HasAction = !string.IsNullOrWhiteSpace(action);
        IsInstalled = installed;
        IsBusy = packageStatus == PackageStatus.Busy;
    }
}
