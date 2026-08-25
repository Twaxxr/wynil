namespace NowSpinning.Core.Models;

public sealed record MediaTrack(
    string Title,
    string Artist,
    string Album,
    string SourceApplication,
    string SourceId,
    string? ArtworkPath,
    bool IsPlaying,
    TimeSpan Position,
    TimeSpan Duration,
    bool CanPlayPause,
    bool CanSkipNext,
    bool CanSkipPrevious)
{
    public static MediaTrack Empty { get; } = new(
        "Play something to begin", string.Empty, string.Empty, string.Empty,
        string.Empty, null, false, TimeSpan.Zero, TimeSpan.Zero, false, false, false);
}
