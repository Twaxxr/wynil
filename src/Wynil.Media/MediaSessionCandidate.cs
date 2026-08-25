using Wynil.Core.Models;

namespace Wynil.Media;

public sealed record MediaSessionCandidate(MediaTrack Track, DateTimeOffset LastUpdated);
