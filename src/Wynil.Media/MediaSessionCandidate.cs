using NowSpinning.Core.Models;

namespace NowSpinning.Media;

public sealed record MediaSessionCandidate(MediaTrack Track, DateTimeOffset LastUpdated);
