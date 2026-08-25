namespace NowSpinning.Core.Artwork;

public sealed record ArtworkPalette(string Primary, string Secondary, string Accent, string Text);

public static class PaletteExtractor
{
    public static ArtworkPalette Extract(ReadOnlySpan<byte> rgbPixels)
    {
        if (rgbPixels.Length < 3 || rgbPixels.Length % 3 != 0)
            throw new ArgumentException("RGB data must contain complete pixels.", nameof(rgbPixels));

        long red = 0, green = 0, blue = 0;
        for (var index = 0; index < rgbPixels.Length; index += 3)
        {
            red += rgbPixels[index];
            green += rgbPixels[index + 1];
            blue += rgbPixels[index + 2];
        }

        var count = rgbPixels.Length / 3;
        var r = (byte)(red / count);
        var g = (byte)(green / count);
        var b = (byte)(blue / count);
        var primary = Hex(r, g, b);
        var secondary = Hex((byte)(r * .58), (byte)(g * .58), (byte)(b * .58));
        var accent = Hex((byte)Math.Min(255, r * 1.28 + 24), (byte)Math.Min(255, g * 1.28 + 24), (byte)Math.Min(255, b * 1.28 + 24));
        var luminance = .2126 * r + .7152 * g + .0722 * b;
        return new ArtworkPalette(primary, secondary, accent, luminance > 145 ? "#111111" : "#FFFFFF");
    }

    private static string Hex(byte red, byte green, byte blue) => $"#{red:X2}{green:X2}{blue:X2}";
}
