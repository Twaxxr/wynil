using NowSpinning.Media;

namespace NowSpinning.Tests;

[TestClass]
public sealed class ArtworkCacheTests
{
    [TestMethod]
    public async Task SameIdentityAndContentReusesTheSameFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var cache = new ArtworkCache(directory);
            var bytes = new byte[] { 1, 2, 3, 4, 5 };
            var first = await cache.StoreAsync(new MemoryStream(bytes), "spotify|track");
            var second = await cache.StoreAsync(new MemoryStream(bytes), "spotify|track");
            Assert.AreEqual(first, second);
            Assert.HasCount(1, Directory.GetFiles(directory, "*.img"));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [TestMethod]
    public async Task ChangedArtworkContentCreatesAStableReplacement()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var cache = new ArtworkCache(directory);
            var first = await cache.StoreAsync(new MemoryStream(new byte[] { 1 }), "same-track");
            var second = await cache.StoreAsync(new MemoryStream(new byte[] { 2 }), "same-track");
            Assert.AreNotEqual(first, second);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
