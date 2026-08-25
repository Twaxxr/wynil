using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using NowSpinning.Core.Models;
using NowSpinning.Core.Configuration;

namespace NowSpinning.Wallpaper;

internal sealed class WallpaperWindow : Window, IDisposable
{
    private readonly WebView2 _webView = new();
    private readonly string _frontendDirectory;
    private readonly string _artworkDirectory;
    private readonly RendererBridge _bridge = new();
    private bool _rendererReady;
    private bool _runtimePaused;
    private int _runtimeStateVersion;
    private bool _disposed;

    public WallpaperWindow(string frontendDirectory, string artworkDirectory)
    {
        _frontendDirectory = frontendDirectory;
        _artworkDirectory = artworkDirectory;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        AllowsTransparency = false;
        Background = System.Windows.Media.Brushes.Black;
        Content = _webView;
        _bridge.CommandReceived += (_, command) => CommandReceived?.Invoke(this, command);
    }

    public event EventHandler<string>? CommandReceived;

    public async Task InitializeAsync()
    {
        if (!File.Exists(Path.Combine(_frontendDirectory, "index.html")))
            throw new FileNotFoundException("The wallpaper frontend has not been built.", Path.Combine(_frontendDirectory, "index.html"));

        var userData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NowSpinning", "WebView2");
        var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userData);
        await _webView.EnsureCoreWebView2Async(environment);

        _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "app.nowspinning.local", _frontendDirectory, CoreWebView2HostResourceAccessKind.DenyCors);
        if (Directory.Exists(_artworkDirectory))
        {
            _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "artwork.nowspinning.local", _artworkDirectory, CoreWebView2HostResourceAccessKind.DenyCors);
        }

        _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
        _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
        _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        _bridge.Attach(_webView.CoreWebView2);
        _webView.Source = new Uri("https://app.nowspinning.local/index.html");
    }

    public async Task SetTrackAsync(MediaTrack track)
    {
        await _bridge.UpdateTrackAsync(track);
    }

    public Task<bool> SetSettingsAsync(WallpaperSettings settings, bool requireAcknowledgement, CancellationToken cancellationToken) =>
        _bridge.UpdateSettingsAsync(settings, requireAcknowledgement, cancellationToken);

    public void SetRuntimePaused(bool paused)
    {
        _runtimePaused = paused;
        var version = Interlocked.Increment(ref _runtimeStateVersion);
        if (_rendererReady) _ = ApplyRuntimeStateAsync(paused, version);
    }

    private async Task ApplyRuntimeStateAsync(bool paused, int version)
    {
        if (_webView.CoreWebView2 is null || _disposed) return;
        if (!paused)
        {
            if (_webView.CoreWebView2.IsSuspended) _webView.CoreWebView2.Resume();
            _bridge.UpdateRuntimePaused(false);
            return;
        }

        _bridge.UpdateRuntimePaused(true);
        await Task.Delay(80);
        if (version != Volatile.Read(ref _runtimeStateVersion) || !_runtimePaused || _disposed) return;
        _ = await _webView.CoreWebView2.TrySuspendAsync();
    }

    public void SetPointer(double x, double y) => _bridge.UpdatePointer(x, y);

    public void SetInteraction(bool enabled)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != nint.Zero) WorkerWInterop.SetClickThrough(handle, !enabled);
        _bridge.UpdateInteraction(enabled);
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        using var json = JsonDocument.Parse(args.WebMessageAsJson);
        if (json.RootElement.TryGetProperty("type", out var type) && type.GetString() == "ready")
        {
            _rendererReady = true;
            _ = _bridge.SetReadyAsync();
            SetRuntimePaused(_runtimePaused);
            return;
        }
        _bridge.HandleWebMessage(args.WebMessageAsJson);
    }

    protected override void OnClosed(EventArgs e)
    {
        Dispose();
        base.OnClosed(e);
    }

    public void Dispose()
    {
        if (_disposed) return;
        if (_webView.CoreWebView2 is not null)
            _webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
        _webView.Dispose();
        _disposed = true;
    }
}
