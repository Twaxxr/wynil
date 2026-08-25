using NowSpinning.Core.Models;

namespace NowSpinning.Tests;

[TestClass]
public sealed class MediaStateReducerTests
{
    private static MediaTrack Track(string artwork = "cover.img") => new(
        "Title", "Artist", "Album", "Spotify", "spotify", artwork, true,
        TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(3), true, true, true);

    [TestMethod]
    public void TimelineChangeDoesNotChangeTrackIdentity()
    {
        var previous = Track();
        var current = previous with { Position = TimeSpan.FromSeconds(20) };
        var diff = MediaStateReducer.Compare(previous, current);
        Assert.AreEqual(MediaChangeKind.Timeline, diff.Changes);
        Assert.AreEqual(MediaStateReducer.Identity(previous), MediaStateReducer.Identity(current));
    }

    [TestMethod]
    public void PauseDoesNotReplaceTrackOrArtwork()
    {
        var diff = MediaStateReducer.Compare(Track(), Track() with { IsPlaying = false });
        Assert.IsTrue(diff.Has(MediaChangeKind.Playback));
        Assert.IsFalse(diff.Has(MediaChangeKind.Track | MediaChangeKind.Artwork));
    }

    [TestMethod]
    public void ArtworkChangeIsCategorizedSeparately()
    {
        var diff = MediaStateReducer.Compare(Track(), Track("replacement.img"));
        Assert.AreEqual(MediaChangeKind.Artwork, diff.Changes);
    }

    [TestMethod]
    public void IdenticalUpdateIsSuppressed() =>
        Assert.AreEqual(MediaChangeKind.None, MediaStateReducer.Compare(Track(), Track()).Changes);
}
