using Wynil.Media;

namespace Wynil.Tests;

[TestClass]
public sealed class BrowserMessageValidatorTests
{
    [TestMethod]
    public void ValidMessageIsSanitizedAndConverted()
    {
        var valid = BrowserMessageValidator.TryCreateTrack(new BrowserMediaMessage
        {
            Title = "A\0 Song",
            Artist = "Artist",
            IsPlaying = true,
            DurationSeconds = 200
        }, out var track);

        Assert.IsTrue(valid);
        Assert.AreEqual("A Song", track.Title);
        Assert.IsTrue(track.IsPlaying);
    }

    [TestMethod]
    public void OversizedArtworkIsRejected()
    {
        var valid = BrowserMessageValidator.TryCreateTrack(new BrowserMediaMessage
        {
            Title = "Song",
            ArtworkDataUrl = new string('x', BrowserMessageValidator.MaximumMessageBytes + 1)
        }, out _);
        Assert.IsFalse(valid);
    }
}
