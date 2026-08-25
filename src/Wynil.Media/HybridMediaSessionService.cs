using System.Net;
using Wynil.Core.Models;

namespace Wynil.Media;

public sealed class HybridMediaSessionService : IMediaSessionService
{
    private readonly IMediaSessionService _primary;
    private readonly BrowserFallbackServer _fallback;
    private MediaTrack _fallbackTrack = MediaTrack.Empty;

    public HybridMediaSessionService(IMediaSessionService primary, BrowserFallbackServer fallback)
    {
        _primary = primary;
        _fallback = fallback;
    }

    public MediaTrack CurrentTrack { get; private set; } = MediaTrack.Empty;
    public event EventHandler<MediaTrack>? CurrentTrackChanged;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        _primary.CurrentTrackChanged += OnPrimaryTrackChanged;
        _fallback.TrackReceived += OnFallbackTrackReceived;
        await _primary.StartAsync(cancellationToken).ConfigureAwait(false);
        try { await _fallback.StartAsync().ConfigureAwait(false); }
        catch (HttpListenerException) { }
        PublishBest();
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _primary.CurrentTrackChanged -= OnPrimaryTrackChanged;
        _fallback.TrackReceived -= OnFallbackTrackReceived;
        await _primary.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> TogglePlayPauseAsync() => _primary.TogglePlayPauseAsync();
    public Task<bool> SkipNextAsync() => _primary.SkipNextAsync();
    public Task<bool> SkipPreviousAsync() => _primary.SkipPreviousAsync();

    private void OnPrimaryTrackChanged(object? sender, MediaTrack track) => PublishBest();

    private void OnFallbackTrackReceived(object? sender, MediaTrack track)
    {
        _fallbackTrack = track;
        PublishBest();
    }

    private void PublishBest()
    {
        var primary = _primary.CurrentTrack;
        var selected = primary.IsPlaying || _fallbackTrack == MediaTrack.Empty ? primary : _fallbackTrack;
        if (selected == CurrentTrack) return;
        CurrentTrack = selected;
        CurrentTrackChanged?.Invoke(this, selected);
        _ = _fallback.BroadcastTrackAsync(selected);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        await _fallback.DisposeAsync().ConfigureAwait(false);
        await _primary.DisposeAsync().ConfigureAwait(false);
    }
}
