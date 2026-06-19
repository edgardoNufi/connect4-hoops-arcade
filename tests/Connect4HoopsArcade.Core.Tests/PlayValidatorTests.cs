using Connect4HoopsArcade.Core.Rules;
using Xunit;

namespace Connect4HoopsArcade.Core.Tests;

public class PlayValidatorTests
{
    [Fact]
    public void Same_color_is_a_warning()
    {
        var w = PlayValidator.CheckColors("red", "red");
        Assert.Equal(ColorWarning.Same, w);
    }

    [Fact]
    public void Similar_hues_are_a_warning()
    {
        // yellow (48) vs orange (32) → hue distance 16 < 30
        Assert.Equal(ColorWarning.Similar, PlayValidator.CheckColors("yellow", "orange"));
    }

    [Fact]
    public void Distinct_colors_are_ok()
    {
        Assert.Equal(ColorWarning.None, PlayValidator.CheckColors("red", "cyan"));
    }
}
