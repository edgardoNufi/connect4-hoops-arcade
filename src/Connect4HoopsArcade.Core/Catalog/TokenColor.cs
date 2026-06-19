namespace Connect4HoopsArcade.Core.Catalog;

/// <summary>A selectable token color. Hue (0-359) drives the "too similar" validation.</summary>
public sealed record TokenColor(string Id, string Hex, string Name, int Hue);
