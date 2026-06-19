using Connect4HoopsArcade.Core.Catalog;
using Xunit;

namespace Connect4HoopsArcade.Core.Tests;

public class ColorCatalogTests
{
    [Fact]
    public void Has_eight_colors_in_design_order()
    {
        Assert.Equal(8, ColorCatalog.All.Count);
        Assert.Equal("pink", ColorCatalog.All[0].Id);
        Assert.Equal("red", ColorCatalog.All[7].Id);
    }

    [Fact]
    public void ById_returns_matching_hex()
    {
        Assert.Equal("#ff3b3b", ColorCatalog.ById("red").Hex);
        Assert.Equal("#ffd23f", ColorCatalog.ById("yellow").Hex);
    }

    [Fact]
    public void ById_falls_back_to_last_color_for_unknown_id()
    {
        Assert.Equal(ColorCatalog.All[^1], ColorCatalog.ById("does-not-exist"));
    }

    [Theory]
    [InlineData(0, 0, 0)]      // identical
    [InlineData(340, 0, 20)]   // pink vs red wraps to 20
    [InlineData(48, 32, 16)]   // yellow vs orange
    public void HueDistance_handles_wraparound(int a, int b, int expected)
    {
        Assert.Equal(expected, ColorCatalog.HueDistance(a, b));
    }
}
