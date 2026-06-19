using Connect4HoopsArcade.Core.Catalog;

namespace Connect4HoopsArcade.Core.Rules;

public static class PlayValidator
{
    public const int SimilarHueThreshold = 30;

    /// <summary>Validates two players' chosen color ids (only meaningful in 2-player mode).</summary>
    public static ColorWarning CheckColors(string colorA, string colorB)
    {
        if (colorA == colorB) return ColorWarning.Same;
        int dist = ColorCatalog.HueDistance(ColorCatalog.ById(colorA).Hue, ColorCatalog.ById(colorB).Hue);
        return dist < SimilarHueThreshold ? ColorWarning.Similar : ColorWarning.None;
    }
}
