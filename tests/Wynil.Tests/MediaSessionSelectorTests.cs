using Wynil.Core.Models;
using Wynil.Media;

namespace Wynil.Tests;

[TestClass]
public sealed class MediaSessionSelectorTests
{
    [TestMethod]
    public void PlayingSessionWinsOverNewerPausedSession()
    {
        var now = DateTimeOffset.UtcNow;
        var playing = new MediaSessionCandidate(Track("spotify", true), now.AddMinutes(-1));
        var paused = new MediaSessionCandidate(Track("chrome", false), now);

        var selected = MediaSessionSelector.Select([paused, playing]);

        Assert.AreSame(playing, selected);
    }

    [TestMethod]
    public void PreferredSourceWinsWhenAvailable()
    {
        var now = DateTimeOffset.UtcNow;
        var preferred = new MediaSessionCandidate(Track("spotify", false), now.AddMinutes(-2));
        var other = new MediaSessionCandidate(Track("chrome", true), now);

        var selected = MediaSessionSelector.Select([other, preferred], "spotify");

        Assert.AreSame(preferred, selected);
    }

    private static MediaTrack Track(string source, bool playing) => new(
        "Title", "Artist", "Album", source, source, null, playing,
        TimeSpan.Zero, TimeSpan.FromMinutes(3), true, true, true);
}
