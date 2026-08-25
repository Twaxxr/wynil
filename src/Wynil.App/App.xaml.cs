using System.Drawing;
using System.IO;
using System.Windows;
using Wynil.App.ViewModels;
using Wynil.Core.Configuration;
using Wynil.Media;
using Wynil.Wallpaper;
using Forms = System.Windows.Forms;

namespace Wynil.App;

public partial class App : System.Windows.Application, IDisposable
{
    private Forms.NotifyIcon? _trayIcon;
    private MainWindowViewModel? _viewModel;
    private MainWindow? _mainWindow;
    private bool _exiting;

    protected override async void OnStartup(StartupEventArgs e)
    {
        Environment.SetEnvironmentVariable("WEBVIEW2_DEFAULT_BACKGROUND_COLOR", "FF0B111A");
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        try
        {
            var defaultsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            var configurationPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Wynil", "settings.json");
            var options = File.Exists(configurationPath)
                ? await JsonConfigurationService.LoadAsync(configurationPath)
                : await JsonConfigurationService.LoadAsync(defaultsPath);
            IMediaSessionService mediaService;
            if (options.DeveloperSimulationMode)
            {
                mediaService = new SimulationMediaSessionService();
            }
            else
            {
                var windowsMedia = new WindowsMediaSessionService(new ArtworkCache());
                mediaService = options.Media.BrowserFallbackEnabled
                    ? new HybridMediaSessionService(windowsMedia, new BrowserFallbackServer())
                    : windowsMedia;
            }
            var wallpaperHost = new NativeWallpaperHost();
            var audioReactiveService = new AudioReactiveService();
            _viewModel = new MainWindowViewModel(options, mediaService, wallpaperHost, configurationPath, audioReactiveService);
            _mainWindow = new MainWindow(_viewModel);
            _mainWindow.Closing += OnMainWindowClosing;
            CreateTrayIcon(options.ProductName);
            _mainWindow.Show();
            await _viewModel.InitializeAsync();
        }
        catch (Exception exception)
        {
            var errorDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Wynil");
            Directory.CreateDirectory(errorDirectory);
            File.WriteAllText(Path.Combine(errorDirectory, "startup-error.log"), exception.ToString());
            System.Windows.MessageBox.Show(exception.Message, "Wynil could not start", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private void CreateTrayIcon(string productName)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open settings", null, (_, _) => ShowSettings());
        menu.Items.Add("Start wallpaper", null, async (_, _) => { if (_viewModel is not null) await _viewModel.StartWallpaperAsync(); });
        menu.Items.Add("Pause wallpaper", null, async (_, _) => { if (_viewModel is not null) await _viewModel.StopWallpaperAsync(); });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, async (_, _) => await ExitAsync());

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = CreateTrayIcon(),
            Text = productName,
            Visible = true,
            ContextMenuStrip = menu
        };
        _trayIcon.DoubleClick += (_, _) => ShowSettings();
    }

    private static Icon CreateTrayIcon()
    {
        var resource = System.Windows.Application.GetResourceStream(
            new Uri("pack://application:,,,/Wynil.App;component/Assets/logo.png"));
        if (resource is null) return SystemIcons.Application;
        using var stream = resource.Stream;
        using var bitmap = new Bitmap(stream);
        return Icon.FromHandle(bitmap.GetHicon());
    }

    private void ShowSettings()
    {
        if (_mainWindow is null) return;
        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void OnMainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_exiting) return;
        e.Cancel = true;
        _mainWindow?.Hide();
    }

    private async Task ExitAsync()
    {
        if (_exiting) return;
        _exiting = true;
        if (_viewModel is not null) await _viewModel.DisposeAsync();
        _viewModel = null;
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
        _mainWindow?.Close();
        Shutdown();
    }

    public void Dispose()
    {
        _trayIcon?.Dispose();
        _trayIcon = null;
        GC.SuppressFinalize(this);
    }
}
