using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Avln.ToolBox.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Avln.ToolBox.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly IServiceProvider _serviceProvider;
    private bool _initialized;

    public MainWindow(MainViewModel viewModel, IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _serviceProvider = serviceProvider;
        DataContext = viewModel;

        Loaded += MainWindow_OnLoaded;
        Closing += MainWindow_OnClosing;
        MouseLeftButtonDown += MainWindow_OnMouseLeftButtonDown;
        _viewModel.SettingsRequested += ViewModel_OnSettingsRequested;
    }

    private async void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await _viewModel.InitializeAsync();
    }

    private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        var app = (global::Avln.ToolBox.App)Application.Current;
        if (app.IsExitRequested)
        {
            return;
        }

        e.Cancel = true;
        app.HideMainWindow();
    }

    private void MainWindow_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private async void ViewModel_OnSettingsRequested(object? sender, EventArgs e)
    {
        var window = _serviceProvider.GetRequiredService<SettingsWindow>();
        window.Owner = this;
        window.ShowDialog();
        await _viewModel.RefreshAsync(runAutoUpdate: false);
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        var app = (global::Avln.ToolBox.App)Application.Current;
        app.HideMainWindow();
    }

    private void MoreButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.ContextMenu is not null)
        {
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.IsOpen = true;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        Closing -= MainWindow_OnClosing;
        _viewModel.SettingsRequested -= ViewModel_OnSettingsRequested;
        base.OnClosed(e);
    }
}
