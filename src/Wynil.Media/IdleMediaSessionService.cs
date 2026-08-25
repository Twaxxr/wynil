using Wynil.Core.Models;

namespace Wynil.Media;

public sealed class IdleMediaSessionService : IMediaSessionService
{
    public MediaTrack CurrentTrack { get; } = MediaTrack.Empty;
    public event EventHandler<MediaTrack>? CurrentTrackChanged;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CurrentTrackChanged?.Invoke(this, CurrentTrack);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task<bool> TogglePlayPauseAsync() => Task.FromResult(false);
    public Task<bool> SkipNextAsync() => Task.FromResult(false);
    public Task<bool> SkipPreviousAsync() => Task.FromResult(false);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
