namespace Wynil.Core.Configuration;

public sealed class AppOptions
{
    public const int CurrentConfigurationVersion = 3;
    public int ConfigurationVersion { get; set; }
    public string ProductName { get; set; } = "Wynil";
    public bool DeveloperSimulationMode { get; set; }
    public GeneralOptions General { get; set; } = new();
    public WallpaperOptions Wallpaper { get; set; } = new();
    public AppearanceOptions Appearance { get; set; } = new();
    public AnimationOptions Animation { get; set; } = new();
    public MediaOptions Media { get; set; } = new();
    public PerformanceOptions Performance { get; set; } = new();
    public WallpaperSettings Scene { get; set; } = new();
}

public enum DeskMaterial
{
    WarmWalnut, DarkWalnut, NaturalOak, BlackWood, WhiteStudio, DarkMarble, LightMarble, CustomImage
}

public enum AlbumSleeveStyle
{
    Automatic, OriginalArtwork, MinimalModern, BlackLuxury, WhiteMinimal, VintageVinyl,
    ArtworkAdaptive, SpotifyInspired, NoSleeve
}

/// <summary>The single renderer configuration consumed by both preview and desktop hosts.</summary>
public sealed class WallpaperSettings
{
    public DeskMaterial DeskMaterial { get; set; } = DeskMaterial.WarmWalnut;
    public AlbumSleeveStyle AlbumSleeveStyle { get; set; } = AlbumSleeveStyle.Automatic;
    public string? CustomDeskImagePath { get; set; }
    public bool FilmGrainEnabled { get; set; } = true;
    public bool DustParticlesEnabled { get; set; } = true;
    public bool MouseParallaxEnabled { get; set; } = true;
    public bool ArtworkAmbientLightingEnabled { get; set; } = true;
    public bool ShowSongInformation { get; set; } = true;
    public double FilmGrainIntensity { get; set; } = .2;
    public double DustIntensity { get; set; } = .35;
    public double ParallaxStrength { get; set; } = .25;
    public double AmbientLightingIntensity { get; set; } = .6;
    public double VinylSpeed { get; set; } = 1;
    public double TransitionSeconds { get; set; } = .8;
    public bool TonearmAnimation { get; set; } = true;
    public bool AudioReactiveEnabled { get; set; }
    public bool ReduceMotion { get; set; }
    public int TargetFps { get; set; } = 60;
    public bool PauseDuringFullscreenApps { get; set; } = true;
    public bool LowPowerMode { get; set; }
    public bool SpanAcrossMonitors { get; set; }
    public string SelectedMonitor { get; set; } = "All monitors";

    public WallpaperSettings Clone() => (WallpaperSettings)MemberwiseClone();
}

public static class WallpaperSettingsValidator
{
    public static IReadOnlyList<string> ValidateAndNormalize(WallpaperSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var errors = new List<string>();
        if (!Enum.IsDefined(settings.DeskMaterial)) { errors.Add("Unknown desk material."); settings.DeskMaterial = DeskMaterial.WarmWalnut; }
        if (!Enum.IsDefined(settings.AlbumSleeveStyle)) { errors.Add("Unknown album sleeve style."); settings.AlbumSleeveStyle = AlbumSleeveStyle.Automatic; }
        settings.FilmGrainIntensity = Clamp(settings.FilmGrainIntensity, 0, 1, nameof(settings.FilmGrainIntensity), errors);
        settings.DustIntensity = Clamp(settings.DustIntensity, 0, 1, nameof(settings.DustIntensity), errors);
        settings.ParallaxStrength = Clamp(settings.ParallaxStrength, 0, 2, nameof(settings.ParallaxStrength), errors);
        settings.AmbientLightingIntensity = Clamp(settings.AmbientLightingIntensity, 0, 1, nameof(settings.AmbientLightingIntensity), errors);
        settings.VinylSpeed = Clamp(settings.VinylSpeed, .25, 2, nameof(settings.VinylSpeed), errors);
        settings.TransitionSeconds = Clamp(settings.TransitionSeconds, 0, 3, nameof(settings.TransitionSeconds), errors);
        if (settings.TargetFps is not (30 or 60)) { errors.Add("Target FPS must be 30 or 60."); settings.TargetFps = 60; }
        return errors;
    }

    private static double Clamp(double value, double min, double max, string name, List<string> errors)
    {
        if (!double.IsFinite(value) || value < min || value > max) errors.Add($"{name} was outside its supported range.");
        return double.IsFinite(value) ? Math.Clamp(value, min, max) : min;
    }
}

public sealed class GeneralOptions
{
    public bool StartWithWindows { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public bool StartWallpaperAutomatically { get; set; }
    public bool SpanAcrossMonitors { get; set; }
    public string SelectedMonitor { get; set; } = "All monitors";
    public bool PauseOnBattery { get; set; } = true;
    public bool PauseWhenLocked { get; set; } = true;
    public bool ResumeAutomatically { get; set; } = true;
}

public sealed class WallpaperOptions
{
    public string Mode { get; set; } = "Native";
    public int TargetFramesPerSecond { get; set; } = 60;
    public bool PauseForFullScreenApplications { get; set; } = true;
}

public sealed class AppearanceOptions
{
    public string SleeveStyle { get; set; } = "Automatic";
    public string DeskMaterial { get; set; } = "WarmWalnut";
    public double DeskBrightness { get; set; } = 1;
    public double ShadowIntensity { get; set; } = .8;
    public bool FilmGrain { get; set; } = true;
    public bool DustParticles { get; set; } = true;
    public bool MouseParallax { get; set; } = true;
    public bool ArtworkAmbientLighting { get; set; } = true;
    public bool ShowSongInformation { get; set; } = true;
}

public sealed class AnimationOptions
{
    public string Quality { get; set; } = "High";
    public double VinylSpeed { get; set; } = 1;
    public double TransitionSeconds { get; set; } = .8;
    public bool TonearmAnimation { get; set; } = true;
    public bool AudioReactive { get; set; }
    public bool ReduceMotion { get; set; }
    public double MouseParallaxStrength { get; set; } = 1;
}

public sealed class MediaOptions
{
    public string PreferredSource { get; set; } = string.Empty;
    public string[] IgnoredApplications { get; set; } = [];
    public int ArtworkCacheMegabytes { get; set; } = 128;
    public bool BrowserFallbackEnabled { get; set; } = true;
}

public sealed class PerformanceOptions
{
    public bool LowPowerMode { get; set; }
    public bool DisableEffectsOnBattery { get; set; } = true;
    public bool HardwareAcceleration { get; set; } = true;
    public int MaximumGpuPercent { get; set; } = 50;
    public string GpuUsageMode { get; set; } = "Balanced";
}
