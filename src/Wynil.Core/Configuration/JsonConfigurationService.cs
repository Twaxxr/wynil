using System.Text.Json;
using System.Text.Json.Serialization;
using Wynil.Core.Logging;

namespace Wynil.Core.Configuration;

public static class JsonConfigurationService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true
    };

    static JsonConfigurationService() => SerializerOptions.Converters.Add(new JsonStringEnumConverter());

    public static async Task<AppOptions> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            var defaults = new AppOptions();
            MigrateAndNormalize(defaults);
            return defaults;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            var options = await JsonSerializer.DeserializeAsync<AppOptions>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false) ?? new AppOptions();
            MigrateAndNormalize(options);
            AppLog.Write("settings.loaded", new { path, options.ConfigurationVersion });
            return options;
        }
        catch (JsonException)
        {
            var corruptPath = $"{path}.corrupt-{DateTime.UtcNow:yyyyMMddHHmmss}";
            File.Copy(path, corruptPath, true);
            AppLog.Write("settings.corrupt", new { path, corruptPath });
            var defaults = new AppOptions();
            MigrateAndNormalize(defaults);
            return defaults;
        }
    }

    public static async Task SaveAsync(string path, AppOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(options);
        MigrateAndNormalize(options);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        await using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
        {
            await JsonSerializer.SerializeAsync(stream, options, SerializerOptions, cancellationToken).ConfigureAwait(false);
        }
        File.Move(temporaryPath, path, true);
        AppLog.Write("settings.saved", new { path, options.ConfigurationVersion });
    }

    public static Task ExportAsync(string path, AppOptions options, CancellationToken cancellationToken = default) =>
        SaveAsync(path, options, cancellationToken);

    public static Task<AppOptions> ImportAsync(string path, CancellationToken cancellationToken = default) =>
        LoadAsync(path, cancellationToken);

    public static AppOptions ResetToDefaults()
    {
        var defaults = new AppOptions();
        MigrateAndNormalize(defaults);
        return defaults;
    }

    private static void MigrateAndNormalize(AppOptions options)
    {
        options.Scene ??= new WallpaperSettings();
        if (options.ConfigurationVersion < 2)
        {
            options.Scene.DeskMaterial = ParseDesk(options.Appearance.DeskMaterial);
            options.Scene.AlbumSleeveStyle = ParseSleeve(options.Appearance.SleeveStyle);
            options.Scene.FilmGrainEnabled = options.Appearance.FilmGrain;
            options.Scene.DustParticlesEnabled = options.Appearance.DustParticles;
            options.Scene.MouseParallaxEnabled = options.Appearance.MouseParallax;
            options.Scene.ArtworkAmbientLightingEnabled = options.Appearance.ArtworkAmbientLighting;
            options.Scene.ShowSongInformation = options.Appearance.ShowSongInformation;
            options.Scene.VinylSpeed = options.Animation.VinylSpeed;
            options.Scene.TransitionSeconds = options.Animation.TransitionSeconds;
            options.Scene.TonearmAnimation = options.Animation.TonearmAnimation;
            options.Scene.ReduceMotion = options.Animation.ReduceMotion;
            options.Scene.ParallaxStrength = options.Animation.MouseParallaxStrength;
            options.Scene.TargetFps = options.Wallpaper.TargetFramesPerSecond;
            options.Scene.PauseDuringFullscreenApps = options.Wallpaper.PauseForFullScreenApplications;
            options.Scene.LowPowerMode = options.Performance.LowPowerMode;
            options.Scene.SpanAcrossMonitors = options.General.SpanAcrossMonitors;
            options.Scene.SelectedMonitor = options.General.SelectedMonitor;
        }
        if (options.ConfigurationVersion < 3)
            options.Scene.AudioReactiveEnabled = options.Animation.AudioReactive;
        _ = WallpaperSettingsValidator.ValidateAndNormalize(options.Scene);
        options.ConfigurationVersion = AppOptions.CurrentConfigurationVersion;
    }

    private static DeskMaterial ParseDesk(string value) => value switch
    {
        "DarkOak" => DeskMaterial.DarkWalnut,
        "NaturalAsh" => DeskMaterial.NaturalOak,
        _ when Enum.TryParse<DeskMaterial>(value.Replace(" ", ""), true, out var parsed) => parsed,
        _ => DeskMaterial.WarmWalnut
    };

    private static AlbumSleeveStyle ParseSleeve(string value) =>
        Enum.TryParse<AlbumSleeveStyle>(value.Replace(" ", "").Replace("-", ""), true, out var parsed)
            ? parsed : AlbumSleeveStyle.Automatic;
}
