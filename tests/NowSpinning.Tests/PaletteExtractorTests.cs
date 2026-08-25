using NowSpinning.Core.Artwork;

namespace NowSpinning.Tests;

[TestClass]
public sealed class PaletteExtractorTests
{
    [TestMethod]
    public void ExtractAveragesPixelsAndChoosesContrastingText()
    {
        var palette = PaletteExtractor.Extract([255, 255, 255, 0, 0, 0]);
        Assert.AreEqual("#7F7F7F", palette.Primary);
        Assert.AreEqual("#FFFFFF", palette.Text);
    }
}
