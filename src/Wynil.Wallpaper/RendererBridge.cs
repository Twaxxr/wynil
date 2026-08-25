using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using Microsoft.Web.WebView2.Core;
using Wynil.Core.Configuration;
using Wynil.Core.Models;

namespace Wynil.Wallpaper;

internal sealed class RendererBridge
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly Dictionary<string, TaskCompletionSource<bool>> _pendingSettings = [];
    private CoreWebView2? _webView;
    private MediaTrack? _sentTrack;
    private MediaTrack _latestTrack = MediaTrack.Empty;
    private WallpaperSettings _latestSettings = new();
    private float _latestAudioLevel;
    private bool _ready;
    private bool _interactionEnabled;
    private bool _runtimePaused;
    private double _pointerX;
    private double _pointerY;

    public event EventHandler<string>? CommandReceived;

    public void Attach(CoreWebView2 webView) => _webView = webView;

    public async Task SetReadyAsync()
    {
        _ready = true;
        _sentTrack = null;
        await UpdateTrackAsync(_latestTrack).ConfigureAwait(true);
        await UpdateSettingsAsync(_latestSettings, requireAcknowledgement: false).ConfigureAwait(true);
        UpdateInteraction(_interactionEnabled);
        UpdateRuntimePaused(_runtimePaused);
        UpdatePointer(_pointerX, _pointerY);
        UpdateAudioLevel(_latestAudioLevel);
    }

    public void UpdateInteraction(bool enabled)
    {
        _interactionEnabled = enabled;
        if (_ready && _webView is not null) Post("interaction.update", new { enabled });
    }

    public void UpdateRuntimePaused(bool paused)
    {
        _runtimePaused = paused;
        if (_ready && _webView is not null) Post("runtime.pause", new { paused });
    }

    public void UpdatePointer(double x, double y)
    {
        _pointerX = Math.Clamp(x, -1, 1);
        _pointerY = Math.Clamp(y, -1, 1);
        if (_ready && _webView is not null) Post("pointer.update", new { x = _pointerX, y = _pointerY });
    }

    public void UpdateAudioLevel(float level)
    {
        _latestAudioLevel = float.IsFinite(level) ? Math.Clamp(level, 0, 1) : 0;
        if (_ready && _webView is not null) Post("audio.level", new { level = _latestAudioLevel });
    }

    public Task UpdateTrackAsync(MediaTrack track)
    {
        _latestTrack = track;
        if (!_ready || _webView is null) return Task.CompletedTask;

        var diff = MediaStateReducer.Compare(_sentTrack, track);
        if (diff.Changes == MediaChangeKind.None) return Task.CompletedTask;
        if (_sentTrack is null || diff.Has(MediaChangeKind.Track | MediaChangeKind.Artwork | MediaChangeKind.Source))
        {
            Post("media.track", new
            {
                identity = diff.TrackIdentity, track.Title, track.Artist, track.Album,
                sourceApplication = track.SourceApplication,
                artworkUrl = ArtworkUrl(track.ArtworkPath)
            });
        }
        if (_sentTrack is null || diff.Has(MediaChangeKind.Playback))
        {
            Post("media.playback", new { track.IsPlaying, track.CanPlayPause, track.CanSkipNext, track.CanSkipPrevious });
        }
        if (_sentTrack is null || diff.Has(MediaChangeKind.Timeline))
        {
            Post("media.timeline", new { positionSeconds = track.Position.TotalSeconds, durationSeconds = track.Duration.TotalSeconds });
        }
        _sentTrack = track;
        return Task.CompletedTask;
    }

    public async Task<bool> UpdateSettingsAsync(WallpaperSettings settings, bool requireAcknowledgement = true, CancellationToken cancellationToken = default)
    {
        _latestSettings = settings.Clone();
        if (!_ready || _webView is null) return !requireAcknowledgement;
        var requestId = Guid.NewGuid().ToString("N");
        TaskCompletionSource<bool>? completion = null;
        if (requireAcknowledgement)
        {
            completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingSettings[requestId] = completion;
        }
        Post("settings.update", _latestSettings, requestId);
        if (completion is null) return true;
        try
        {
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(true);
        }
        catch (TimeoutException) { return false; }
        finally { _pendingSettings.Remove(requestId); }
    }

    public bool HandleWebMessage(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("version", out var version) || version.GetInt32() != 1 ||
            !root.TryGetProperty("type", out var typeElement)) return false;
        var type = typeElement.GetString();
        if (type == "settings.applied" && root.TryGetProperty("requestId", out var requestElement))
        {
            var requestId = requestElement.GetString();
            var success = !root.TryGetProperty("success", out var successElement) || successElement.GetBoolean();
            if (requestId is not null && _pendingSettings.Remove(requestId, out var pending)) pending.TrySetResult(success);
            return true;
        }
        if (type == "command" && root.TryGetProperty("command", out var commandElement))
        {
            var command = commandElement.GetString();
            if (command is "toggle" or "next" or "previous") CommandReceived?.Invoke(this, command);
            return true;
        }
        return type == "ready";
    }

    private void Post(string type, object payload, string? requestId = null) =>
        _webView!.PostWebMessageAsJson(JsonSerializer.Serialize(new { version = 1, type, requestId, payload }, JsonOptions));

    private static string? ArtworkUrl(string? path) => path is null
        ? null : $"https://artwork.wynil.local/{Uri.EscapeDataString(Path.GetFileName(path))}";
}
