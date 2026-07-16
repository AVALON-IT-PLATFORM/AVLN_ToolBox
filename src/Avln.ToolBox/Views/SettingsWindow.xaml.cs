using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Avln.ToolBox.ViewModels;
using Microsoft.Win32;

namespace Avln.ToolBox.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private bool _ready;

    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        Loaded += SettingsWindow_OnLoaded;
        MouseLeftButtonDown += SettingsWindow_OnMouseLeftButtonDown;
    }

    private async void SettingsWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
        _ready = true;
    }

    private void SettingsWindow_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private async void AutoUpdate_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_ready && sender is ToggleButton)
        {
            await _viewModel.SaveAsync();
        }
    }

    private async void ChangePathButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Выберите базовую папку установки",
            InitialDirectory = _viewModel.InstallBasePath,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            _viewModel.InstallBasePath = dialog.FolderName;
            await _viewModel.SaveAsync();
        }
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e) => Close();
}
