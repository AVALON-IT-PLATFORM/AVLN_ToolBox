using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
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

        _brandIcon = CreateBrandIcon();
        ApplyWindowIcon(mainWindow, _brandIcon);
        TryPersistBrandIcon(_brandIcon);
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

    private static void TryPersistBrandIcon(Icon icon)
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, BrandIconFileName);
            using var stream = File.Create(iconPath);
            icon.Save(stream);
        }
        catch
        {
            // A missing shortcut icon must not prevent ToolBox startup.
        }
    }

    private static Icon CreateBrandIcon()
    {
        const int size = 256;
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        using var backgroundPath = CreateRoundedRectangle(
            new RectangleF(1, 1, size - 2, size - 2),
            52);
        using var backgroundBrush = new SolidBrush(Color.FromArgb(0xB3, 0x95, 0x72));
        graphics.FillPath(backgroundBrush, backgroundPath);

        using var font = new System.Drawing.Font(
            "Segoe UI",
            58,
            System.Drawing.FontStyle.Bold,
            System.Drawing.GraphicsUnit.Pixel);
        using var foregroundBrush = new SolidBrush(Color.White);
        using var format = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        graphics.DrawString(
            "AVLN",
            font,
            foregroundBrush,
            new RectangleF(0, 0, size, size - 4),
            format);

        var iconHandle = bitmap.GetHicon();
        try
        {
            using var temporaryIcon = Icon.FromHandle(iconHandle);
            return (Icon)temporaryIcon.Clone();
        }
        finally
        {
            DestroyIcon(iconHandle);
        }
    }

    private static GraphicsPath CreateRoundedRectangle(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        var arc = new RectangleF(bounds.Location, new SizeF(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();

        return path;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
