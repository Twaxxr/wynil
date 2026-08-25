using Wynil.Media;

namespace Wynil.Tests;

[TestClass]
public sealed class PlaybackStateTests
{
    [TestMethod]
    public async Task SimulationPreservesTrackWhileTogglingPlayback()
    {
        await using var service = new SimulationMediaSessionService();
        await service.StartAsync();
        var title = service.CurrentTrack.Title;
        await service.TogglePlayPauseAsync();
        Assert.AreEqual(title, service.CurrentTrack.Title);
        Assert.IsFalse(service.CurrentTrack.IsPlaying);
    }

    [TestMethod]
    public async Task SimulationPublishesSongChangeOnSkip()
    {
        await using var service = new SimulationMediaSessionService();
        await service.StartAsync();
        var title = service.CurrentTrack.Title;
        await service.SkipNextAsync();
        Assert.AreNotEqual(title, service.CurrentTrack.Title);
    }
}
