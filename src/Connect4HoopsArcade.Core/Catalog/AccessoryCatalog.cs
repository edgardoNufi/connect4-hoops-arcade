using Connect4HoopsArcade.Core.Primitives;

namespace Connect4HoopsArcade.Core.Catalog;

public sealed record AccessoryOption(AccessoryId Id, string Label);

public static class AccessoryCatalog
{
    public static readonly IReadOnlyList<AccessoryOption> All = new[]
    {
        new AccessoryOption(AccessoryId.None,     "Ninguno"),
        new AccessoryOption(AccessoryId.Glasses,  "Lentes"),
        new AccessoryOption(AccessoryId.Cap,      "Gorra"),
        new AccessoryOption(AccessoryId.Headband, "Banda"),
        new AccessoryOption(AccessoryId.Crown,    "Corona"),
        new AccessoryOption(AccessoryId.Bowtie,   "Moño"),
        new AccessoryOption(AccessoryId.Earrings, "Aretes"),
    };
}
