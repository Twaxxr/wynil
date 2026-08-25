using Wynil.Core.Models;

namespace Wynil.Media;

public static class BrowserMessageValidator
{
    public const int MaximumMessageBytes = 6 * 1024 * 1024;

    public static bool TryCreateTrack(BrowserMediaMessage? message, out MediaTrack track)
    {
        track = MediaTrack.Empty;
        if (message is null || string.IsNullOrWhiteSpace(message.Title) || message.Title.Length > 500) return false;
        if (message.Artist?.Length > 500 || message.Album?.Length > 500 || message.SourceApplication?.Length > 100) return false;
        if (!double.IsFinite(message.PositionSeconds) || !double.IsFinite(message.DurationSeconds)) return false;
        if (message.PositionSeconds < 0 || message.DurationSeconds < 0 || message.PositionSeconds > 86_400 || message.DurationSeconds > 86_400) return false;
        if (message.ArtworkDataUrl is { Length: > MaximumMessageBytes }) return false;

        track = new MediaTrack(
            Clean(message.Title), Clean(message.Artist), Clean(message.Album),
            Clean(message.SourceApplication, "Browser"), Clean(message.SourceId, "browser-extension"),
            null, message.IsPlaying, TimeSpan.FromSeconds(message.PositionSeconds),
            TimeSpan.FromSeconds(message.DurationSeconds), false, false, false);
        return true;
    }

    private static string Clean(string? value, string fallback = "") =>
        string.IsNullOrWhiteSpace(value)
            ? fallback
            : new string(value.Where(character => !char.IsControl(character) || character is '\t').ToArray()).Trim();
}
