using NowSpinning.Core.Models;
using NowSpinning.Core.Configuration;

namespace NowSpinning.Wallpaper;

public interface IWallpaperHost : IAsyncDisposable
{
    bool IsRunning { get; }
    bool InteractionEnabled { get; set; }
    event EventHandler<string>? CommandReceived;
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task UpdateTrackAsync(MediaTrack track, CancellationToken cancellationToken = default);
    Task<bool> UpdateSettingsAsync(WallpaperSettings settings, bool requireAcknowledgement = true, CancellationToken cancellationToken = default);
}
