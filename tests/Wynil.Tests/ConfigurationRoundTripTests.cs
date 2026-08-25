using Wynil.Core.Configuration;

namespace Wynil.Tests;

[TestClass]
public sealed class ConfigurationRoundTripTests
{
    [TestMethod]
    public async Task ConfigurationRoundTripsToJson()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"wynil-test-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.json");
        try
        {
            var original = new AppOptions { ProductName = "Renamed", General = new GeneralOptions { StartWithWindows = true } };
            await JsonConfigurationService.SaveAsync(path, original);
            var loaded = await JsonConfigurationService.LoadAsync(path);
            Assert.AreEqual("Renamed", loaded.ProductName);
            Assert.IsTrue(loaded.General.StartWithWindows);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
