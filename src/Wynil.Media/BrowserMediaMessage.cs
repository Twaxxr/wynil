namespace Wynil.Media;

public sealed class BrowserMediaMessage
{
    public string? Title { get; set; }
    public string? Artist { get; set; }
    public string? Album { get; set; }
    public string? SourceApplication { get; set; }
    public string? SourceId { get; set; }
    public bool IsPlaying { get; set; }
    public double PositionSeconds { get; set; }
    public double DurationSeconds { get; set; }
    public string? ArtworkDataUrl { get; set; }
}
