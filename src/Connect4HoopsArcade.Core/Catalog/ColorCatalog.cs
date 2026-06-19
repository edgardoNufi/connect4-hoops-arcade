namespace Connect4HoopsArcade.Core.Catalog;

public static class ColorCatalog
{
    public static readonly IReadOnlyList<TokenColor> All = new[]
    {
        new TokenColor("pink",   "#ff2d6f", "Rosa",     340),
        new TokenColor("cyan",   "#22d3ee", "Cian",     190),
        new TokenColor("yellow", "#ffd23f", "Amarillo", 48),
        new TokenColor("green",  "#2ee86e", "Verde",    140),
        new TokenColor("orange", "#ff8a00", "Naranja",  32),
        new TokenColor("purple", "#b14bff", "Morado",   275),
        new TokenColor("blue",   "#3b82f6", "Azul",     217),
        new TokenColor("red",    "#ff3b3b", "Rojo",     0),
    };

    public static TokenColor ById(string id) =>
        All.FirstOrDefault(c => c.Id == id) ?? All[^1];

    public static string HexOf(string id) => ById(id).Hex;

    /// <summary>Shortest distance between two hues on the 0-359 wheel.</summary>
    public static int HueDistance(int a, int b)
    {
        int d = Math.Abs(a - b) % 360;
        return Math.Min(d, 360 - d);
    }
}
