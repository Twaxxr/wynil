namespace NowSpinning.Core.Layout;

public readonly record struct PixelRectangle(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
}

public static class MonitorLayout
{
    public static PixelRectangle Span(IEnumerable<PixelRectangle> monitors)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        var items = monitors.ToArray();
        if (items.Length == 0) throw new ArgumentException("At least one monitor is required.", nameof(monitors));

        var left = items.Min(item => item.X);
        var top = items.Min(item => item.Y);
        var right = items.Max(item => item.Right);
        var bottom = items.Max(item => item.Bottom);
        return new PixelRectangle(left, top, right - left, bottom - top);
    }
}
