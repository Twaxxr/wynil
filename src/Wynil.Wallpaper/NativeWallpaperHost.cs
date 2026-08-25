using System.IO;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using Wynil.Core.Models;
using Wynil.Core.Configuration;
using Wynil.Core.Logging;
using Forms = System.Windows.Forms;

namespace Wynil.Wallpaper;

public sealed class NativeWallpaperHost : IWallpaperHost
{
    private readonly List<WallpaperWindow> _windows = [];
    private readonly string _frontendDirectory;
    private readonly string _artworkDirectory;
    private readonly DispatcherTimer _hotkeyTimer = new() { Interval = TimeSpan.FromMilliseconds(60) };
    private readonly DispatcherTimer _healthTimer = new() { Interval = TimeSpan.FromSeconds(20) };
    private readonly DispatcherTimer _runtimeTimer = new() { Interval = TimeSpan.FromMilliseconds(750) };
    private readonly DispatcherTimer _pointerTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly Dictionary<WallpaperWindow, System.Drawing.Rectangle> _bounds = [];
    private bool _interactionEnabled;
    private WallpaperSettings _settings = new();
    private bool _runtimePaused;

    public NativeWallpaperHost(string? frontendDirectory = null, string? artworkDirectory = null)
    {
        _frontendDirectory = frontendDirectory ?? Path.Combine(AppContext.BaseDirectory, "Frontend");
        _artworkDirectory = artworkDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Wynil", "Artwork");
        _hotkeyTimer.Tick += OnHotkeyTick;
        _healthTimer.Tick += OnHealthTick;
        _runtimeTimer.Tick += OnRuntimeTick;
        _pointerTimer.Tick += OnPointerTick;
    }

    public bool IsRunning => _windows.Count > 0;
    public event EventHandler<string>? CommandReceived;

    public bool InteractionEnabled
    {
        get => _interactionEnabled;
        set
        {
            _interactionEnabled = value;
            foreach (var window in _windows) window.SetInteraction(value);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning) return;
        cancellationToken.ThrowIfCancellationRequested();

        var targets = GetTargetBounds();
        foreach (var bounds in targets)
        {
            var window = new WallpaperWindow(_frontendDirectory, _artworkDirectory);
            window.CommandReceived += OnCommandReceived;
            var attached = false;
            window.SourceInitialized += (_, _) =>
            {
                var handle = new WindowInteropHelper(window).Handle;
                attached = WorkerWInterop.AttachBehindDesktopIcons(handle, bounds.X, bounds.Y, bounds.Width, bounds.Height);
            };
            window.Show();
            if (!attached)
            {
                window.Close();
                window.Dispose();
                throw new InvalidOperationException("Windows desktop WorkerW could not be validated. The wallpaper was not shown above desktop icons.");
            }
            await window.InitializeAsync();
            window.SetInteraction(_interactionEnabled);
            window.SetRuntimePaused(_runtimePaused);
            _windows.Add(window);
            _bounds[window] = bounds;
        }
        _hotkeyTimer.Start();
        _healthTimer.Start();
        _runtimeTimer.Start();
        _pointerTimer.Start();
        EvaluateRuntimeState();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _hotkeyTimer.Stop();
        _healthTimer.Stop();
        _runtimeTimer.Stop();
        _pointerTimer.Stop();
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        foreach (var window in _windows.ToArray())
        {
            window.CommandReceived -= OnCommandReceived;
            window.Close();
            window.Dispose();
        }
        _windows.Clear();
        _bounds.Clear();
        return Task.CompletedTask;
    }

    public async Task UpdateTrackAsync(MediaTrack track, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var window in _windows) await window.SetTrackAsync(track);
    }

    public void UpdateAudioLevel(float level)
    {
        var normalized = float.IsFinite(level) ? Math.Clamp(level, 0, 1) : 0;
        foreach (var window in _windows) window.SetAudioLevel(normalized);
    }

    public async Task<bool> UpdateSettingsAsync(WallpaperSettings settings, bool requireAcknowledgement = true, CancellationToken cancellationToken = default)
    {
        var layoutChanged = _settings.SpanAcrossMonitors != settings.SpanAcrossMonitors ||
            !string.Equals(_settings.SelectedMonitor, settings.SelectedMonitor, StringComparison.Ordinal);
        _settings = settings.Clone();
        EvaluateRuntimeState();
        if (layoutChanged && IsRunning)
        {
            await StopAsync(cancellationToken);
            await StartAsync(cancellationToken);
        }
        var accepted = true;
        foreach (var window in _windows)
            accepted &= await window.SetSettingsAsync(settings, requireAcknowledgement, cancellationToken);
        return accepted;
    }

    private System.Drawing.Rectangle[] GetTargetBounds()
    {
        if (_settings.SpanAcrossMonitors) return [Forms.SystemInformation.VirtualScreen];
        var screens = Forms.Screen.AllScreens;
        if (string.Equals(_settings.SelectedMonitor, "Primary monitor", StringComparison.OrdinalIgnoreCase))
            return [screens.First(screen => screen.Primary).Bounds];
        if (!string.Equals(_settings.SelectedMonitor, "All monitors", StringComparison.OrdinalIgnoreCase))
        {
            var selected = screens.FirstOrDefault(screen => string.Equals(screen.DeviceName, _settings.SelectedMonitor, StringComparison.OrdinalIgnoreCase));
            if (selected is not null) return [selected.Bounds];
        }
        return screens.Select(screen => screen.Bounds).ToArray();
    }

    private void OnCommandReceived(object? sender, string command) => CommandReceived?.Invoke(this, command);

    private void OnHotkeyTick(object? sender, EventArgs e) => InteractionEnabled = WorkerWInterop.IsAltPressed();

    private void OnHealthTick(object? sender, EventArgs e)
    {
        ReattachAll();
    }

    private void OnRuntimeTick(object? sender, EventArgs e) => EvaluateRuntimeState();

    private void EvaluateRuntimeState()
    {
        var shouldPause = _settings.PauseDuringFullscreenApps && WorkerWInterop.IsFullscreenApplicationActive();
        if (_runtimePaused == shouldPause) return;
        _runtimePaused = shouldPause;
        foreach (var window in _windows) window.SetRuntimePaused(shouldPause);
        AppLog.Write(shouldPause ? "wallpaper.runtime_paused" : "wallpaper.runtime_resumed", new { reason = shouldPause ? "fullscreen" : "fullscreen-ended" });
    }

    private void OnPointerTick(object? sender, EventArgs e)
    {
        if (_runtimePaused || !_settings.MouseParallaxEnabled || !WorkerWInterop.TryGetCursorPosition(out var cursor)) return;
        foreach (var pair in _bounds)
        {
            var bounds = pair.Value;
            var x = bounds.Width <= 0 ? 0 : ((cursor.X - bounds.Left) / (double)bounds.Width - .5) * 2;
            var y = bounds.Height <= 0 ? 0 : ((cursor.Y - bounds.Top) / (double)bounds.Height - .5) * 2;
            pair.Key.SetPointer(x, y);
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        System.Windows.Application.Current.Dispatcher.BeginInvoke(async () =>
        {
            await StopAsync();
            await StartAsync();
        });

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(ReattachAll));
    }

    private void ReattachAll()
    {
        foreach (var pair in _bounds.ToArray())
        {
            var handle = new WindowInteropHelper(pair.Key).Handle;
            var bounds = pair.Value;
            if (!WorkerWInterop.AttachBehindDesktopIcons(handle, bounds.X, bounds.Y, bounds.Width, bounds.Height))
                pair.Key.Hide();
            else if (!pair.Key.IsVisible) pair.Key.Show();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _hotkeyTimer.Tick -= OnHotkeyTick;
        _healthTimer.Tick -= OnHealthTick;
        _runtimeTimer.Tick -= OnRuntimeTick;
        _pointerTimer.Tick -= OnPointerTick;
    }
}
