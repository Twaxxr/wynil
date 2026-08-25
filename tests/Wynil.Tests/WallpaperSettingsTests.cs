using System.Text.Json;
using Wynil.Core.Configuration;

namespace Wynil.Tests;

[TestClass]
public sealed class WallpaperSettingsTests
{
    [TestMethod]
    public async Task LegacySettingsAreMigratedToAuthoritativeScene()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, """{"appearance":{"deskMaterial":"DarkOak","sleeveStyle":"Vintage Vinyl","filmGrain":false},"wallpaper":{"targetFramesPerSecond":30}}""");
            var options = await JsonConfigurationService.LoadAsync(path);
            Assert.AreEqual(AppOptions.CurrentConfigurationVersion, options.ConfigurationVersion);
            Assert.AreEqual(DeskMaterial.DarkWalnut, options.Scene.DeskMaterial);
            Assert.AreEqual(AlbumSleeveStyle.VintageVinyl, options.Scene.AlbumSleeveStyle);
            Assert.IsFalse(options.Scene.FilmGrainEnabled);
            Assert.AreEqual(30, options.Scene.TargetFps);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public async Task AudioReactiveAnimationSettingIsMigratedToScene()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, """{"configurationVersion":2,"animation":{"audioReactive":true}}""");
            var options = await JsonConfigurationService.LoadAsync(path);
            Assert.AreEqual(3, options.ConfigurationVersion);
            Assert.IsTrue(options.Scene.AudioReactiveEnabled);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public async Task CorruptConfigurationFallsBackAndIsPreserved()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "settings.json");
        await File.WriteAllTextAsync(path, "{broken");
        try
        {
            var options = await JsonConfigurationService.LoadAsync(path);
            Assert.AreEqual(DeskMaterial.WarmWalnut, options.Scene.DeskMaterial);
            Assert.HasCount(1, Directory.GetFiles(directory, "settings.json.corrupt-*"));
        }
        finally { Directory.Delete(directory, true); }
    }

    [TestMethod]
    public void InvalidRendererValuesAreNormalized()
    {
        var settings = new WallpaperSettings { TargetFps = 17, DustIntensity = 8, ParallaxStrength = double.NaN };
        var errors = WallpaperSettingsValidator.ValidateAndNormalize(settings);
        Assert.HasCount(3, errors);
        Assert.AreEqual(60, settings.TargetFps);
        Assert.AreEqual(1, settings.DustIntensity);
        Assert.AreEqual(0, settings.ParallaxStrength);
    }

    [TestMethod]
    public async Task EnumMappingsRoundTripAsReadableJson()
    {
        var path = Path.GetTempFileName();
        try
        {
            var options = JsonConfigurationService.ResetToDefaults();
            options.Scene.DeskMaterial = DeskMaterial.DarkMarble;
            options.Scene.AlbumSleeveStyle = AlbumSleeveStyle.SpotifyInspired;
            await JsonConfigurationService.SaveAsync(path, options);
            var json = await File.ReadAllTextAsync(path);
            StringAssert.Contains(json, "DarkMarble");
            StringAssert.Contains(json, "SpotifyInspired");
            var restored = await JsonConfigurationService.LoadAsync(path);
            Assert.AreEqual(options.Scene.DeskMaterial, restored.Scene.DeskMaterial);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void BuiltInThemePresetUpdatesMultipleRendererSettings()
    {
        var settings = new WallpaperSettings();
        Assert.IsTrue(ThemePresetCatalog.TryApply("Luxury Marble", settings));
        Assert.AreEqual(DeskMaterial.DarkMarble, settings.DeskMaterial);
        Assert.AreEqual(AlbumSleeveStyle.BlackLuxury, settings.AlbumSleeveStyle);
        Assert.IsTrue(settings.ArtworkAmbientLightingEnabled);
    }
}
