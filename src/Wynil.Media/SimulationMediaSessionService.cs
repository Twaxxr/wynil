using Wynil.Core.Models;

namespace Wynil.Media;

public sealed class SimulationMediaSessionService : IMediaSessionService
{
    private readonly MediaTrack[] _tracks =
    [
        new("Golden Hour", "The Paper Suns", "Rooms of Light", "Simulation", "simulation", null, true,
            TimeSpan.FromSeconds(42), TimeSpan.FromMinutes(3.7), true, true, true),
        new("Night Drive", "Violet Transit", "After Images", "Simulation", "simulation", null, true,
            TimeSpan.FromSeconds(18), TimeSpan.FromMinutes(4.1), true, true, true)
    ];
    private int _index;

    public MediaTrack CurrentTrack { get; private set; } = MediaTrack.Empty;
    public event EventHandler<MediaTrack>? CurrentTrackChanged;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CurrentTrack = _tracks[0];
        CurrentTrackChanged?.Invoke(this, CurrentTrack);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<bool> TogglePlayPauseAsync()
    {
        CurrentTrack = CurrentTrack with { IsPlaying = !CurrentTrack.IsPlaying };
        CurrentTrackChanged?.Invoke(this, CurrentTrack);
        return Task.FromResult(true);
    }

    public Task<bool> SkipNextAsync()
    {
        _index = (_index + 1) % _tracks.Length;
        CurrentTrack = _tracks[_index];
        CurrentTrackChanged?.Invoke(this, CurrentTrack);
        return Task.FromResult(true);
    }

    public Task<bool> SkipPreviousAsync()
    {
        _index = (_index + _tracks.Length - 1) % _tracks.Length;
        CurrentTrack = _tracks[_index];
        CurrentTrackChanged?.Invoke(this, CurrentTrack);
        return Task.FromResult(true);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
