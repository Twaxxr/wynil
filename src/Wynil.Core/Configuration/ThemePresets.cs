namespace Wynil.Core.Configuration;

public sealed record ThemePreset(string Name, WallpaperSettings Settings);

public static class ThemePresetCatalog
{
    public static IReadOnlyList<ThemePreset> BuiltIn { get; } =
    [
        Preset("Warm Walnut", DeskMaterial.WarmWalnut, AlbumSleeveStyle.VintageVinyl, true, true, .18, .65),
        Preset("Midnight Black", DeskMaterial.BlackWood, AlbumSleeveStyle.BlackLuxury, true, false, .12, .45),
        Preset("White Studio", DeskMaterial.WhiteStudio, AlbumSleeveStyle.WhiteMinimal, false, false, 0, .35),
        Preset("Vintage Vinyl", DeskMaterial.DarkWalnut, AlbumSleeveStyle.VintageVinyl, true, true, .28, .55),
        Preset("Luxury Marble", DeskMaterial.DarkMarble, AlbumSleeveStyle.BlackLuxury, true, false, .1, .7),
        Preset("Cozy Desk", DeskMaterial.NaturalOak, AlbumSleeveStyle.ArtworkAdaptive, true, true, .16, .72),
        Preset("Minimal", DeskMaterial.WhiteStudio, AlbumSleeveStyle.OriginalArtwork, false, false, 0, .25),
        Preset("Artwork Adaptive", DeskMaterial.DarkWalnut, AlbumSleeveStyle.ArtworkAdaptive, true, false, .12, .85)
    ];

    public static bool TryApply(string name, WallpaperSettings target)
    {
        var preset = BuiltIn.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        if (preset is null) return false;
        Copy(preset.Settings, target);
        return true;
    }

    private static ThemePreset Preset(string name, DeskMaterial desk, AlbumSleeveStyle sleeve, bool grain, bool dust, double grainIntensity, double ambient) =>
        new(name, new WallpaperSettings
        {
            DeskMaterial = desk, AlbumSleeveStyle = sleeve, FilmGrainEnabled = grain,
            DustParticlesEnabled = dust, FilmGrainIntensity = grainIntensity,
            ArtworkAmbientLightingEnabled = true, AmbientLightingIntensity = ambient
        });

    private static void Copy(WallpaperSettings source, WallpaperSettings target)
    {
        target.DeskMaterial = source.DeskMaterial; target.AlbumSleeveStyle = source.AlbumSleeveStyle;
        target.FilmGrainEnabled = source.FilmGrainEnabled; target.DustParticlesEnabled = source.DustParticlesEnabled;
        target.MouseParallaxEnabled = source.MouseParallaxEnabled; target.ArtworkAmbientLightingEnabled = source.ArtworkAmbientLightingEnabled;
        target.ShowSongInformation = source.ShowSongInformation; target.FilmGrainIntensity = source.FilmGrainIntensity;
        target.DustIntensity = source.DustIntensity; target.ParallaxStrength = source.ParallaxStrength;
        target.AmbientLightingIntensity = source.AmbientLightingIntensity; target.VinylSpeed = source.VinylSpeed;
        target.TransitionSeconds = source.TransitionSeconds; target.TonearmAnimation = source.TonearmAnimation;
        target.ReduceMotion = source.ReduceMotion; target.TargetFps = source.TargetFps;
        target.PauseDuringFullscreenApps = source.PauseDuringFullscreenApps; target.LowPowerMode = source.LowPowerMode;
    }
}
