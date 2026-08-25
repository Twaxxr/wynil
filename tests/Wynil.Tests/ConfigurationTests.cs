using Wynil.Core.Configuration;

namespace Wynil.Tests;

[TestClass]
public sealed class ConfigurationTests
{
    [TestMethod]
    public async Task MissingConfigurationUsesSafeDefaults()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"now-spinning-{Guid.NewGuid():N}.json");

        var options = await JsonConfigurationService.LoadAsync(missingPath);

        Assert.AreEqual("Wynil", options.ProductName);
        Assert.AreEqual("Native", options.Wallpaper.Mode);
        Assert.AreEqual(60, options.Wallpaper.TargetFramesPerSecond);
    }
}
