using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Avln.ToolBox.Infrastructure;
using Avln.ToolBox.Views;
using Microsoft.Extensions.DependencyInjection;
using Forms = System.Windows.Forms;

namespace Avln.ToolBox;

public partial class App : System.Windows.Application
{
    private const string BrandIconFileName = "AVLN.ToolBox.ico";

    private ServiceProvider? _serviceProvider;
    private Forms.NotifyIcon? _notifyIcon;
    private Forms.ContextMenuStrip? _trayMenu;
    private Icon? _brandIcon;
    private bool _isExitRequested;

    public bool IsExitRequested => _isExitRequested;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var services = new ServiceCollection();
        services.AddToolBox();
        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;

        _brandIcon = LoadBrandIcon();
        ApplyWindowIcon(mainWindow, _brandIcon);
        InitializeTrayIcon(_brandIcon);

        mainWindow.Show();
    }

    public void HideMainWindow()
    {
        MainWindow?.Hide();
    }

    public void ShowMainWindow()
    {
        if (MainWindow is null)
        {
            return;
        }

        if (!MainWindow.IsVisible)
        {
            MainWindow.Show();
        }

        if (MainWindow.WindowState == WindowState.Minimized)
        {
            MainWindow.WindowState = WindowState.Normal;
        }

        MainWindow.Activate();
        MainWindow.Focus();
    }

    public void ExitApplication()
    {
        if (_isExitRequested)
        {
            return;
        }

        _isExitRequested = true;
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
        }

        MainWindow?.Close();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        _trayMenu?.Dispose();
        _brandIcon?.Dispose();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private void InitializeTrayIcon(Icon icon)
    {
        _trayMenu = new Forms.ContextMenuStrip();
        _trayMenu.Items.Add(
            "Открыть AVLN ToolBox",
            null,
            (_, _) => Dispatcher.Invoke(ShowMainWindow));
        _trayMenu.Items.Add(new Forms.ToolStripSeparator());
        _trayMenu.Items.Add(
            "Выход",
            null,
            (_, _) => Dispatcher.Invoke(ExitApplication));

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = icon,
            Text = "AVLN ToolBox",
            Visible = true,
            ContextMenuStrip = _trayMenu
        };
        _notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowMainWindow);
    }

    private static void ApplyWindowIcon(Window window, Icon icon)
    {
        var imageSource = Imaging.CreateBitmapSourceFromHIcon(
            icon.Handle,
            Int32Rect.Empty,
            BitmapSizeOptions.FromWidthAndHeight(64, 64));
        imageSource.Freeze();
        window.Icon = imageSource;
    }

    private static Icon LoadBrandIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, BrandIconFileName);
        if (!File.Exists(iconPath))
        {
            throw new FileNotFoundException($"Brand icon file not found: {iconPath}", iconPath);
        }

        return new Icon(iconPath);
    }
}
