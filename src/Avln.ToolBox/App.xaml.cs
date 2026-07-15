using System.Drawing;
using System.Threading;
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
    private const string SingleInstanceMutexName = @"Local\AVLN.ToolBox.SingleInstance";
    private const string ActivationEventName = @"Local\AVLN.ToolBox.Activate";

    private ServiceProvider? _serviceProvider;
    private Forms.NotifyIcon? _notifyIcon;
    private Forms.ContextMenuStrip? _trayMenu;
    private Icon? _brandIcon;
    private Mutex? _instanceMutex;
    private EventWaitHandle? _activationEvent;
    private RegisteredWaitHandle? _activationRegistration;
    private bool _ownsInstanceMutex;
    private bool _isExitRequested;

    public bool IsExitRequested => _isExitRequested;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _activationEvent = new EventWaitHandle(
            initialState: false,
            EventResetMode.AutoReset,
            ActivationEventName);
        _instanceMutex = new Mutex(
            initiallyOwned: true,
            SingleInstanceMutexName,
            out var createdNew);
        _ownsInstanceMutex = createdNew;

        if (!createdNew)
        {
            _activationEvent.Set();
            Shutdown();
            return;
        }

        var services = new ServiceCollection();
        services.AddToolBox();
        _serviceProvider = services.BuildServiceProvider();

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;

        _activationRegistration = ThreadPool.RegisterWaitForSingleObject(
            _activationEvent,
            (_, timedOut) =>
            {
                if (!timedOut && !_isExitRequested)
                {
                    Dispatcher.BeginInvoke(ShowMainWindow);
                }
            },
            state: null,
            Timeout.Infinite,
            executeOnlyOnce: false);

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
        _isExitRequested = true;

        if (_notifyIcon is not null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }

        _activationRegistration?.Unregister(null);
        _activationEvent?.Dispose();

        if (_ownsInstanceMutex)
        {
            _instanceMutex?.ReleaseMutex();
        }

        _instanceMutex?.Dispose();
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
