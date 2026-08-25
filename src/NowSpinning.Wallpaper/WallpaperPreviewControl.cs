using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using NowSpinning.Core.Models;
using NowSpinning.Core.Configuration;

namespace NowSpinning.Wallpaper;

public sealed class WallpaperPreviewControl : System.Windows.Controls.UserControl, IDisposable
{
    public static readonly DependencyProperty TrackProperty = DependencyProperty.Register(
        nameof(Track), typeof(MediaTrack), typeof(WallpaperPreviewControl),
        new PropertyMetadata(MediaTrack.Empty, OnTrackChanged));

    public static readonly DependencyProperty SettingsProperty = DependencyProperty.Register(
        nameof(Settings), typeof(WallpaperSettings), typeof(WallpaperPreviewControl),
        new PropertyMetadata(new WallpaperSettings(), OnSettingsChanged));

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly WebView2 _webView = new();
    private readonly RendererBridge _bridge = new();
    private bool _ready;
    private bool _initializing;
    private bool _disposed;

    public WallpaperPreviewControl()
    {
        ClipToBounds = true;
        Focusable = false;
        IsTabStop = false;
        Content = _webView;
        Loaded += OnLoaded;
    }

    public MediaTrack Track
    {
        get => (MediaTrack)GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    public WallpaperSettings Settings
    {
        get => (WallpaperSettings)GetValue(SettingsProperty);
        set => SetValue(SettingsProperty, value);
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_ready || _initializing || _disposed) return;
        _initializing = true;
        try
        {
            var frontend = Path.Combine(AppContext.BaseDirectory, "Frontend");
            if (!File.Exists(Path.Combine(frontend, "index.html"))) return;
            var userData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NowSpinning", "PreviewWebView2");
            var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userData);
            await _webView.EnsureCoreWebView2Async(environment);
            _webView.CoreWebView2.SetVirtualHostNameToFolderMapping("app.nowspinning.local", frontend, CoreWebView2HostResourceAccessKind.DenyCors);
            var artwork = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NowSpinning", "Artwork");
            Directory.CreateDirectory(artwork);
            _webView.CoreWebView2.SetVirtualHostNameToFolderMapping("artwork.nowspinning.local", artwork, CoreWebView2HostResourceAccessKind.DenyCors);
            _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
            _webView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            _webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;
            _bridge.Attach(_webView.CoreWebView2);
            _webView.Source = new Uri("https://app.nowspinning.local/index.html");
        }
        finally { _initializing = false; }
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (!e.IsSuccess) return;
        _ready = true;
        _ = _bridge.SetReadyAsync();
        _ = PushTrackAsync();
        _ = PushSettingsAsync();
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        _bridge.HandleWebMessage(e.WebMessageAsJson);
    }

    private static void OnSettingsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is WallpaperPreviewControl preview) _ = preview.PushSettingsAsync();
    }

    private static void OnTrackChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is WallpaperPreviewControl preview) _ = preview.PushTrackAsync();
    }

    private async Task PushTrackAsync()
    {
        if (!_ready || _webView.CoreWebView2 is null || _disposed) return;
        await _bridge.UpdateTrackAsync(Track ?? MediaTrack.Empty);
    }

    private async Task PushSettingsAsync()
    {
        if (!_ready || _disposed) return;
        await _bridge.UpdateSettingsAsync(Settings ?? new WallpaperSettings(), false);
    }

    public void Dispose()
    {
        if (_disposed) return;
        Loaded -= OnLoaded;
        if (_webView.CoreWebView2 is not null)
        {
            _webView.CoreWebView2.WebMessageReceived -= OnWebMessageReceived;
            _webView.CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
        }
        _webView.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
