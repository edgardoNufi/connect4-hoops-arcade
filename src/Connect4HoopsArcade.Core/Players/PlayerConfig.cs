using Connect4HoopsArcade.Core.Primitives;

namespace Connect4HoopsArcade.Core.Players;

/// <summary>Immutable player setup. Mutate via `with`.</summary>
public sealed record PlayerConfig(
    string Name,
    string ColorId,
    FaceId Face,
    AccessoryId Accessory,
    bool IsCpu = false)
{
    public static PlayerConfig DefaultP1 => new("Jugador 1", "red",    FaceId.Happy,     AccessoryId.Cap);
    public static PlayerConfig DefaultP2 => new("Jugador 2", "yellow", FaceId.Confident, AccessoryId.Crown);
    public static PlayerConfig DefaultCpu => new("CPU",      "yellow", FaceId.Serious,   AccessoryId.None, IsCpu: true);
}
