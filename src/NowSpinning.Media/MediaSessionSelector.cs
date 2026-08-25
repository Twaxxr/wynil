namespace NowSpinning.Media;

public static class MediaSessionSelector
{
    public static MediaSessionCandidate? Select(
        IEnumerable<MediaSessionCandidate> candidates,
        string? preferredSourceId = null,
        IReadOnlySet<string>? ignoredSourceIds = null)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var eligible = candidates
            .Where(candidate => ignoredSourceIds is null || !ignoredSourceIds.Contains(candidate.Track.SourceId))
            .ToArray();

        return eligible
            .OrderByDescending(candidate => !string.IsNullOrWhiteSpace(preferredSourceId) &&
                                            candidate.Track.SourceId.Equals(preferredSourceId, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(candidate => candidate.Track.IsPlaying)
            .ThenByDescending(candidate => candidate.LastUpdated)
            .FirstOrDefault();
    }
}
