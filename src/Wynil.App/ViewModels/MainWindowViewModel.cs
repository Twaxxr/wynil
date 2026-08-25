using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using Wynil.Core.Configuration;
using Wynil.Core.Models;
using Wynil.Core.Mvvm;
using Wynil.Core.Logging;
using Wynil.Media;
using Wynil.Settings;
using Wynil.Wallpaper;
using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;
using System.Globalization;

namespace Wynil.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IAsyncDisposable
{
    private readonly IMediaSessionService _mediaService;
    private readonly IWallpaperHost _wallpaperHost;
    private readonly string _configurationPath;
    private readonly AsyncRelayCommand _saveSettingsCommand;
    private readonly DispatcherTimer _toastTimer;
    private readonly AudioReactiveService? _audioReactiveService;
    private MediaTrack _currentTrack = MediaTrack.Empty;
    private string _status = "Starting media service…";
    private string _toastText = string.Empty;
    private bool _wallpaperRunning;
    private bool _isDirty;
    private bool _isToastVisible;
    private WallpaperSettings _previewSettings;
    private string _selectedThemePreset = "Warm Walnut";
    private DateTimeOffset _lastMediaChange = DateTimeOffset.MinValue;
    private string _lastError = "None";

    public MainWindowViewModel(AppOptions options, IMediaSessionService mediaService, IWallpaperHost wallpaperHost, string configurationPath, AudioReactiveService? audioReactiveService = null)
    {
        Options = options;
        _previewSettings = options.Scene.Clone();
        _mediaService = mediaService;
        _wallpaperHost = wallpaperHost;
        _configurationPath = configurationPath;
        _audioReactiveService = audioReactiveService;
        _mediaService.CurrentTrackChanged += OnTrackChanged;
        if (_audioReactiveService is not null) _audioReactiveService.LevelChanged += OnAudioLevelChanged;
        _wallpaperHost.CommandReceived += OnWallpaperCommand;

        ToggleWallpaperCommand = new AsyncRelayCommand(ToggleWallpaperAsync);
        _saveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync, () => IsDirty);
        SaveSettingsCommand = _saveSettingsCommand;
        TogglePlaybackCommand = new AsyncRelayCommand(async () => _ = await _mediaService.TogglePlayPauseAsync());
        NextCommand = new AsyncRelayCommand(async () => _ = await _mediaService.SkipNextAsync());
        PreviousCommand = new AsyncRelayCommand(async () => _ = await _mediaService.SkipPreviousAsync());
        ClearArtworkCacheCommand = new AsyncRelayCommand(ClearArtworkCacheAsync);
        ApplyThemePresetCommand = new AsyncRelayCommand(ApplyThemePresetAsync);
        RestartWallpaperCommand = new AsyncRelayCommand(RestartWallpaperAsync);
        TestSampleSongCommand = new AsyncRelayCommand(TestSampleSongAsync);
        ExportSettingsCommand = new AsyncRelayCommand(ExportSettingsAsync);
        ImportSettingsCommand = new AsyncRelayCommand(ImportSettingsAsync);
        ResetSettingsCommand = new AsyncRelayCommand(ResetSettingsAsync);
        OpenLogsCommand = new AsyncRelayCommand(OpenLogsAsync);

        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.4) };
        _toastTimer.Tick += OnToastTimerTick;
    }

    public AppOptions Options { get; }
    public WallpaperSettings PreviewSettings { get => _previewSettings; private set => SetProperty(ref _previewSettings, value); }
    public string ProductName => Options.ProductName;
    public string WallpaperButtonText => WallpaperRunning ? "Stop wallpaper" : "Start wallpaper";
    public string WallpaperButtonContent => $"{(WallpaperRunning ? "■" : "▶")}  {WallpaperButtonText}";
    public string PlaybackGlyph => CurrentTrack.IsPlaying ? "\uE769" : "\uE768";
    public Geometry PlaybackIconGeometry => Geometry.Parse(CurrentTrack.IsPlaying ? "M2,1 L5,1 5,11 2,11Z M7,1 L10,1 10,11 7,11Z" : "M2,1 L11,6 2,11Z");
    public string DisplayTitle => string.IsNullOrWhiteSpace(CurrentTrack.Title) || CurrentTrack == MediaTrack.Empty ? "Unknown track" : CurrentTrack.Title;
    public string DisplayArtist => string.IsNullOrWhiteSpace(CurrentTrack.Artist) ? "Unknown artist" : CurrentTrack.Artist;
    public string DisplaySource => string.IsNullOrWhiteSpace(CurrentTrack.SourceApplication) ? "Local media" : CurrentTrack.SourceApplication;
    public bool HasAlbum => !string.IsNullOrWhiteSpace(CurrentTrack.Album);
    public bool SignalActive => CurrentTrack.IsPlaying;
    public double ProgressPercent => CurrentTrack.Duration <= TimeSpan.Zero ? 0 : Math.Clamp(CurrentTrack.Position.TotalMilliseconds / CurrentTrack.Duration.TotalMilliseconds * 100, 0, 100);
    public string PositionText => FormatTime(CurrentTrack.Position);
    public string DurationText => FormatTime(CurrentTrack.Duration);
    public ICommand ToggleWallpaperCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand TogglePlaybackCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand PreviousCommand { get; }
    public ICommand ClearArtworkCacheCommand { get; }
    public ICommand ApplyThemePresetCommand { get; }
    public ICommand RestartWallpaperCommand { get; }
    public ICommand TestSampleSongCommand { get; }
    public ICommand ExportSettingsCommand { get; }
    public ICommand ImportSettingsCommand { get; }
    public ICommand ResetSettingsCommand { get; }
    public ICommand OpenLogsCommand { get; }
    public string SelectedThemePreset { get => _selectedThemePreset; set => SetProperty(ref _selectedThemePreset, value); }
    public string ApplicationVersion => $"{Options.ProductName} {typeof(MainWindowViewModel).Assembly.GetName().Version?.ToString() ?? "Unknown"}";
    public string WindowsVersion => $"{RuntimeInformation.OSDescription} · {Options.Wallpaper.Mode}";
    public string SettingsFilePath => _configurationPath;
    public string CurrentTrackIdentity => MediaStateReducer.Identity(CurrentTrack);
    public string LastMediaChangeText => _lastMediaChange == DateTimeOffset.MinValue ? "No media event yet" : _lastMediaChange.ToLocalTime().ToString("G", CultureInfo.CurrentCulture);
    public string LastError => _lastError;
    public string WallpaperProcessStatus => WallpaperRunning ? "Running and attached" : "Stopped";
    public string MemoryUsage => $"{Process.GetCurrentProcess().WorkingSet64 / 1_048_576d:F1} MB · {(WallpaperRunning ? "active" : "idle")}";
    public string BrowserExtensionStatus => Options.Media.BrowserFallbackEnabled ? "Enabled · authenticated localhost" : "Disabled";
    public string ActiveSessionDescription => CurrentTrack == MediaTrack.Empty ? "No active session" : $"{DisplaySource} · {CurrentTrack.SourceId}";
    public string IgnoredApplicationsText
    {
        get => string.Join(", ", Options.Media.IgnoredApplications);
        set => Options.Media.IgnoredApplications = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    public MediaTrack CurrentTrack
    {
        get => _currentTrack;
        private set
        {
            if (!SetProperty(ref _currentTrack, value)) return;
            OnPropertyChanged(nameof(PlaybackGlyph));
            OnPropertyChanged(nameof(PlaybackIconGeometry));
            OnPropertyChanged(nameof(DisplayTitle));
            OnPropertyChanged(nameof(DisplayArtist));
            OnPropertyChanged(nameof(DisplaySource));
            OnPropertyChanged(nameof(HasAlbum));
            OnPropertyChanged(nameof(SignalActive));
            OnPropertyChanged(nameof(ProgressPercent));
            OnPropertyChanged(nameof(PositionText));
            OnPropertyChanged(nameof(DurationText));
            OnPropertyChanged(nameof(ActiveSessionDescription));
        }
    }

    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string ToastText { get => _toastText; private set => SetProperty(ref _toastText, value); }
    public bool IsToastVisible { get => _isToastVisible; private set => SetProperty(ref _isToastVisible, value); }

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (!SetProperty(ref _isDirty, value)) return;
            _saveSettingsCommand.NotifyCanExecuteChanged();
        }
    }

    public bool WallpaperRunning
    {
        get => _wallpaperRunning;
        private set
        {
            if (!SetProperty(ref _wallpaperRunning, value)) return;
            OnPropertyChanged(nameof(WallpaperButtonText));
            OnPropertyChanged(nameof(WallpaperButtonContent));
            OnPropertyChanged(nameof(WallpaperProcessStatus));
        }
    }

    public async Task MarkSettingsDirtyAsync()
    {
        _ = WallpaperSettingsValidator.ValidateAndNormalize(Options.Scene);
        PreviewSettings = Options.Scene.Clone();
        IsDirty = true;
        if (WallpaperRunning) await _wallpaperHost.UpdateSettingsAsync(Options.Scene, false);
        await ConfigureAudioReactiveAsync();
    }

    public async Task InitializeAsync()
    {
        try
        {
            await _mediaService.StartAsync();
            CurrentTrack = _mediaService.CurrentTrack;
            Status = Options.DeveloperSimulationMode ? "Simulation mode active" : "Listening for Windows media sessions";
            if (Options.General.StartWallpaperAutomatically) await StartWallpaperAsync();
        }
        catch (Exception exception)
        {
            _lastError = exception.Message;
            OnPropertyChanged(nameof(LastError));
            Status = $"Media service unavailable: {exception.Message}";
            ShowToast("Unable to connect to media session");
        }
    }

    public async Task StartWallpaperAsync()
    {
        if (WallpaperRunning) return;
        try
        {
            _ = await _wallpaperHost.UpdateSettingsAsync(Options.Scene, false);
            await _wallpaperHost.StartAsync();
            await _wallpaperHost.UpdateTrackAsync(CurrentTrack);
            _ = await _wallpaperHost.UpdateSettingsAsync(Options.Scene);
            WallpaperRunning = true;
            await ConfigureAudioReactiveAsync();
            Status = "Live wallpaper running on all monitors";
            ShowToast("Wallpaper started");
        }
        catch (Exception exception)
        {
            _lastError = exception.Message;
            OnPropertyChanged(nameof(LastError));
            Status = $"Wallpaper could not start: {exception.Message}";
            ShowToast("Wallpaper could not start");
        }
    }

    public async Task StopWallpaperAsync()
    {
        await _wallpaperHost.StopAsync();
        WallpaperRunning = false;
        await ConfigureAudioReactiveAsync();
        Status = "Wallpaper paused";
        ShowToast("Wallpaper stopped");
    }

    private async Task ToggleWallpaperAsync()
    {
        if (WallpaperRunning) await StopWallpaperAsync();
        else await StartWallpaperAsync();
    }

    private async Task SaveSettingsAsync()
    {
        var validationErrors = WallpaperSettingsValidator.ValidateAndNormalize(Options.Scene);
        if (validationErrors.Count > 0)
        {
            Status = validationErrors[0];
            ShowToast("Review invalid settings");
            return;
        }
        await JsonConfigurationService.SaveAsync(_configurationPath, Options);
        if (WallpaperRunning && !await _wallpaperHost.UpdateSettingsAsync(Options.Scene))
        {
            Status = "Wallpaper did not acknowledge the settings";
            ShowToast("Wallpaper update failed");
            return;
        }
        StartupRegistrationService.SetEnabled(ProductName, Environment.ProcessPath ?? string.Empty, Options.General.StartWithWindows);
        IsDirty = false;
        Status = "Settings saved";
        ShowToast("Settings saved");
    }

    private Task ClearArtworkCacheAsync()
    {
        var removed = new ArtworkCache().Clear();
        ShowToast(removed == 1 ? "One artwork file cleared" : $"{removed} artwork files cleared");
        return Task.CompletedTask;
    }

    private async Task ApplyThemePresetAsync()
    {
        if (!ThemePresetCatalog.TryApply(SelectedThemePreset, Options.Scene)) return;
        OnPropertyChanged(nameof(Options));
        await MarkSettingsDirtyAsync();
        ShowToast($"{SelectedThemePreset} applied");
    }

    private async Task RestartWallpaperAsync()
    {
        await _wallpaperHost.StopAsync();
        WallpaperRunning = false;
        await StartWallpaperAsync();
    }

    private async Task TestSampleSongAsync()
    {
        CurrentTrack = new MediaTrack("A Very Long Sample Song Title for Layout Testing", "Wynil Studio Ensemble", "Desktop Sessions, Volume One", "Simulation", "simulation.sample", null, true, TimeSpan.FromSeconds(61), TimeSpan.FromSeconds(213), true, true, true);
        if (WallpaperRunning) await _wallpaperHost.UpdateTrackAsync(CurrentTrack);
        ShowToast("Sample song loaded");
    }

    private async Task ExportSettingsAsync()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Wynil settings (*.json)|*.json", FileName = "Wynil-settings.json" };
        if (dialog.ShowDialog() != true) return;
        await JsonConfigurationService.ExportAsync(dialog.FileName, Options);
        ShowToast("Settings exported");
    }

    private async Task ImportSettingsAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Wynil settings (*.json)|*.json" };
        if (dialog.ShowDialog() != true) return;
        var imported = await JsonConfigurationService.ImportAsync(dialog.FileName);
        Options.Scene = imported.Scene;
        OnPropertyChanged(nameof(Options));
        await MarkSettingsDirtyAsync();
        ShowToast("Settings imported");
    }

    private async Task ResetSettingsAsync()
    {
        Options.Scene = JsonConfigurationService.ResetToDefaults().Scene;
        OnPropertyChanged(nameof(Options));
        await MarkSettingsDirtyAsync();
        ShowToast("Wallpaper settings reset");
    }

    private Task OpenLogsAsync()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Wynil", "Logs");
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        return Task.CompletedTask;
    }

    private void OnTrackChanged(object? sender, MediaTrack track)
    {
        _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            CurrentTrack = track;
            _lastMediaChange = DateTimeOffset.Now;
            OnPropertyChanged(nameof(CurrentTrackIdentity));
            OnPropertyChanged(nameof(LastMediaChangeText));
            OnPropertyChanged(nameof(MemoryUsage));
            Status = track == MediaTrack.Empty ? "No active media session" : $"Receiving media from {DisplaySource}";
            if (WallpaperRunning) await _wallpaperHost.UpdateTrackAsync(track);
        });
    }

    private void OnAudioLevelChanged(object? sender, float level)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null) return;
        _ = dispatcher.BeginInvoke(() =>
        {
            if (ShouldCaptureAudio) _wallpaperHost.UpdateAudioLevel(level);
        });
    }

    private bool ShouldCaptureAudio => WallpaperRunning && Options.Scene.AudioReactiveEnabled &&
        !Options.Scene.ReduceMotion && !Options.Scene.LowPowerMode;

    private Task ConfigureAudioReactiveAsync()
    {
        if (_audioReactiveService is null) return Task.CompletedTask;

        var shouldRun = ShouldCaptureAudio;
        if (shouldRun)
        {
            try
            {
                _audioReactiveService.Start();
            }
            catch (Exception exception)
            {
                AppLog.Write("audio_reactive.start_failed", new { error = exception.Message });
                _lastError = exception.Message;
                OnPropertyChanged(nameof(LastError));
                ShowToast("Audio-reactive mode unavailable");
            }
        }
        else
        {
            _audioReactiveService.Stop();
            _wallpaperHost.UpdateAudioLevel(0);
        }

        return Task.CompletedTask;
    }

    private void OnWallpaperCommand(object? sender, string command)
    {
        _ = command switch
        {
            "toggle" => _mediaService.TogglePlayPauseAsync(),
            "next" => _mediaService.SkipNextAsync(),
            "previous" => _mediaService.SkipPreviousAsync(),
            _ => Task.FromResult(false)
        };
    }

    private void ShowToast(string message)
    {
        ToastText = message;
        IsToastVisible = true;
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private void OnToastTimerTick(object? sender, EventArgs e)
    {
        _toastTimer.Stop();
        IsToastVisible = false;
    }

    private static string FormatTime(TimeSpan value) => value <= TimeSpan.Zero ? "0:00" : $"{(int)value.TotalMinutes}:{value.Seconds:00}";

    public async ValueTask DisposeAsync()
    {
        _toastTimer.Stop();
        _toastTimer.Tick -= OnToastTimerTick;
        _mediaService.CurrentTrackChanged -= OnTrackChanged;
        _wallpaperHost.CommandReceived -= OnWallpaperCommand;
        if (_audioReactiveService is not null)
        {
            _audioReactiveService.LevelChanged -= OnAudioLevelChanged;
            _audioReactiveService.Dispose();
        }
        await _wallpaperHost.DisposeAsync();
        await _mediaService.DisposeAsync();
    }
}
