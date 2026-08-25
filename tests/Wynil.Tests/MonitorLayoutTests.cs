using Wynil.Core.Layout;

namespace Wynil.Tests;

[TestClass]
public sealed class MonitorLayoutTests
{
    [TestMethod]
    public void SpanIncludesNegativeAndPositiveMonitorCoordinates()
    {
        var result = MonitorLayout.Span([
            new PixelRectangle(-1920, 0, 1920, 1080),
            new PixelRectangle(0, -200, 2560, 1440)
        ]);

        Assert.AreEqual(new PixelRectangle(-1920, -200, 4480, 1440), result);
    }
}
