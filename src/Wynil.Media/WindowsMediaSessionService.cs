using System.Runtime.InteropServices.WindowsRuntime;
using Wynil.Core.Models;
using Windows.Media.Control;
using Wynil.Core.Logging;

namespace Wynil.Media;

public sealed class WindowsMediaSessionService : IMediaSessionService
{
    private readonly ArtworkCache _artworkCache;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly Dictionary<GlobalSystemMediaTransportControlsSession, DateTimeOffset> _sessions = [];
    private readonly Dictionary<GlobalSystemMediaTransportControlsSession, MediaTrack> _cachedTracks = [];
    private readonly HashSet<GlobalSystemMediaTransportControlsSession> _metadataDirty = [];
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _activeSession;
    private bool _disposed;

    public WindowsMediaSessionService(ArtworkCache artworkCache)
    {
        _artworkCache = artworkCache;
    }

    public MediaTrack CurrentTrack { get; private set; } = MediaTrack.Empty;
    public event EventHandler<MediaTrack>? CurrentTrackChanged;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(cancellationToken);
        _manager.SessionsChanged += OnSessionsChanged;
        AttachSessions();
        await RefreshAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_manager is not null)
        {
            _manager.SessionsChanged -= OnSessionsChanged;
        }

        foreach (var session in _sessions.Keys.ToArray())
        {
            Detach(session);
        }

        _activeSession = null;
        return Task.CompletedTask;
    }

    public async Task<bool> TogglePlayPauseAsync()
    {
        var session = _activeSession;
        if (session is null) return false;
        return CurrentTrack.IsPlaying
            ? await session.TryPauseAsync()
            : await session.TryPlayAsync();
    }

    public async Task<bool> SkipNextAsync() =>
        _activeSession is not null && await _activeSession.TrySkipNextAsync();

    public async Task<bool> SkipPreviousAsync() =>
        _activeSession is not null && await _activeSession.TrySkipPreviousAsync();

    private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
    {
        AttachSessions();
        QueueRefresh();
    }

    private void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, object args)
    {
        _sessions[sender] = DateTimeOffset.UtcNow;
        _cachedTracks.Remove(sender);
        _metadataDirty.Add(sender);
        QueueMetadataRefresh();
    }

    private void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, object args)
    {
        _sessions[sender] = DateTimeOffset.UtcNow;
        if (ReferenceEquals(sender, _activeSession)) PublishPlayback(sender);
        else QueueMetadataRefresh();
    }

    private void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, object args)
    {
        if (!ReferenceEquals(sender, _activeSession)) return;
        var timeline = sender.GetTimelineProperties();
        var next = CurrentTrack with
        {
            Position = timeline.Position,
            Duration = timeline.EndTime > timeline.StartTime ? timeline.EndTime - timeline.StartTime : TimeSpan.Zero
        };
        if (!_metadataDirty.Contains(sender)) _cachedTracks[sender] = next;
        Publish(next);
    }

    private void AttachSessions()
    {
        if (_manager is null) return;
        var current = _manager.GetSessions().ToHashSet();
        foreach (var removed in _sessions.Keys.Where(session => !current.Contains(session)).ToArray())
        {
            Detach(removed);
        }

        foreach (var session in current.Where(session => !_sessions.ContainsKey(session)))
        {
            _sessions.Add(session, DateTimeOffset.UtcNow);
            _metadataDirty.Add(session);
            session.MediaPropertiesChanged += OnMediaPropertiesChanged;
            session.PlaybackInfoChanged += OnPlaybackInfoChanged;
            session.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
        }
    }

    private void Detach(GlobalSystemMediaTransportControlsSession session)
    {
        session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
        session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        session.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
        _sessions.Remove(session);
        _cachedTracks.Remove(session);
        _metadataDirty.Remove(session);
    }

    private void QueueRefresh() => _ = RefreshIgnoringErrorsAsync();

    private void QueueMetadataRefresh() => _ = DebouncedMetadataRefreshAsync();

    private int _metadataRefreshVersion;
    private async Task DebouncedMetadataRefreshAsync()
    {
        var version = Interlocked.Increment(ref _metadataRefreshVersion);
        await Task.Delay(150).ConfigureAwait(false);
        if (version == Volatile.Read(ref _metadataRefreshVersion)) await RefreshIgnoringErrorsAsync().ConfigureAwait(false);
    }

    private async Task RefreshIgnoringErrorsAsync()
    {
        try { await RefreshAsync(CancellationToken.None).ConfigureAwait(false); }
        catch (Exception) when (!_disposed) { }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var snapshots = new List<(GlobalSystemMediaTransportControlsSession Session, MediaSessionCandidate Candidate)>();
            foreach (var pair in _sessions.ToArray())
            {
                try
                {
                    if (_metadataDirty.Contains(pair.Key) || !_cachedTracks.TryGetValue(pair.Key, out var track))
                    {
                        track = await CreateTrackAsync(pair.Key, cancellationToken).ConfigureAwait(false);
                        _cachedTracks[pair.Key] = track;
                        _metadataDirty.Remove(pair.Key);
                    }
                    snapshots.Add((pair.Key, new MediaSessionCandidate(track, pair.Value)));
                }
                catch (Exception) { }
            }

            var selected = MediaSessionSelector.Select(snapshots.Select(item => item.Candidate));
            var match = selected is null ? default : snapshots.First(item => ReferenceEquals(item.Candidate, selected));
        _activeSession = match.Session;
            var nextTrack = selected?.Track ?? MediaTrack.Empty;
            Publish(nextTrack);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private void PublishPlayback(GlobalSystemMediaTransportControlsSession session)
    {
        var playback = session.GetPlaybackInfo();
        var controls = playback.Controls;
        var basis = _cachedTracks.TryGetValue(session, out var cached) ? cached : CurrentTrack;
        var next = basis with
        {
            IsPlaying = playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
            CanPlayPause = controls.IsPlayEnabled || controls.IsPauseEnabled,
            CanSkipNext = controls.IsNextEnabled,
            CanSkipPrevious = controls.IsPreviousEnabled
        };
        _cachedTracks[session] = next;
        Publish(next);
    }

    private void Publish(MediaTrack nextTrack)
    {
        var diff = MediaStateReducer.Compare(CurrentTrack, nextTrack);
        if (diff.Changes == MediaChangeKind.None) return;
        if (diff.Has(MediaChangeKind.Track | MediaChangeKind.Artwork | MediaChangeKind.Source))
            AppLog.Write("media.changed", new { diff.Changes, diff.TrackIdentity, nextTrack.SourceApplication });
        CurrentTrack = nextTrack;
        CurrentTrackChanged?.Invoke(this, nextTrack);
    }

    private async Task<MediaTrack> CreateTrackAsync(
        GlobalSystemMediaTransportControlsSession session,
        CancellationToken cancellationToken)
    {
        var properties = await session.TryGetMediaPropertiesAsync().AsTask(cancellationToken);
        var playback = session.GetPlaybackInfo();
        var timeline = session.GetTimelineProperties();
        string? artworkPath = null;

        if (properties.Thumbnail is not null)
        {
            using var randomAccessStream = await properties.Thumbnail.OpenReadAsync().AsTask(cancellationToken);
            await using var stream = randomAccessStream.AsStreamForRead();
            artworkPath = await _artworkCache.StoreAsync(
                stream, $"{session.SourceAppUserModelId}|{properties.Title}|{properties.Artist}|{properties.AlbumTitle}", cancellationToken)
                .ConfigureAwait(false);
        }

        var controls = playback.Controls;
        return new MediaTrack(
            properties.Title ?? string.Empty,
            properties.Artist ?? string.Empty,
            properties.AlbumTitle ?? string.Empty,
            FriendlySourceName(session.SourceAppUserModelId),
            session.SourceAppUserModelId,
            artworkPath,
            playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
            timeline.Position,
            timeline.EndTime > timeline.StartTime ? timeline.EndTime - timeline.StartTime : TimeSpan.Zero,
            controls.IsPlayEnabled || controls.IsPauseEnabled,
            controls.IsNextEnabled,
            controls.IsPreviousEnabled);
    }

    private static string FriendlySourceName(string sourceId)
    {
        if (sourceId.Contains("Spotify", StringComparison.OrdinalIgnoreCase)) return "Spotify";
        if (sourceId.Contains("Chrome", StringComparison.OrdinalIgnoreCase)) return "Chrome";
        if (sourceId.Contains("Edge", StringComparison.OrdinalIgnoreCase)) return "Edge";
        if (sourceId.Contains("Firefox", StringComparison.OrdinalIgnoreCase)) return "Firefox";
        return sourceId.Split('!')[0].Split('.').LastOrDefault() ?? sourceId;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await StopAsync().ConfigureAwait(false);
        _refreshLock.Dispose();
        _disposed = true;
    }
}
