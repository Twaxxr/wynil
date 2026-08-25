using System.Security.Cryptography;
using System.Text;

namespace Wynil.Core.Models;

[Flags]
public enum MediaChangeKind
{
    None = 0, Track = 1, Artwork = 2, Playback = 4, Timeline = 8, Source = 16
}

public sealed record MediaStateDiff(MediaChangeKind Changes, string TrackIdentity)
{
    public bool Has(MediaChangeKind kind) => (Changes & kind) != 0;
}

public static class MediaStateReducer
{
    public static MediaStateDiff Compare(MediaTrack? previous, MediaTrack current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (previous is null) return new(MediaChangeKind.Track | MediaChangeKind.Artwork | MediaChangeKind.Playback | MediaChangeKind.Timeline | MediaChangeKind.Source, Identity(current));
        var changes = MediaChangeKind.None;
        if (!StringEquals(previous.SourceId, current.SourceId)) changes |= MediaChangeKind.Source;
        if (!StringEquals(previous.Title, current.Title) || !StringEquals(previous.Artist, current.Artist) ||
            !StringEquals(previous.Album, current.Album) || previous.Duration != current.Duration) changes |= MediaChangeKind.Track;
        if (!StringEquals(previous.ArtworkPath, current.ArtworkPath)) changes |= MediaChangeKind.Artwork;
        if (previous.IsPlaying != current.IsPlaying || previous.CanPlayPause != current.CanPlayPause ||
            previous.CanSkipNext != current.CanSkipNext || previous.CanSkipPrevious != current.CanSkipPrevious) changes |= MediaChangeKind.Playback;
        if (previous.Position != current.Position || previous.Duration != current.Duration) changes |= MediaChangeKind.Timeline;
        return new(changes, Identity(current));
    }

    public static string Identity(MediaTrack track)
    {
        var value = string.Join('\u001f', track.SourceId, track.Title, track.Artist, track.Album,
            track.Duration.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture), track.ArtworkPath ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static bool StringEquals(string? left, string? right) => string.Equals(left, right, StringComparison.Ordinal);
}
