using Wynil.Core.Models;

namespace Wynil.Media;

public interface IMediaSessionService : IAsyncDisposable
{
    MediaTrack CurrentTrack { get; }
    event EventHandler<MediaTrack>? CurrentTrackChanged;
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task<bool> TogglePlayPauseAsync();
    Task<bool> SkipNextAsync();
    Task<bool> SkipPreviousAsync();
}
