namespace Connect4HoopsArcade.Core.Primitives;

/// <summary>Occupant of a board cell. Player1/Player2 map to player index 0/1.</summary>
public enum Cell
{
    Empty = 0,
    Player1 = 1,
    Player2 = 2,
}

public static class CellExtensions
{
    /// <summary>Cell for a 0-based player index (0 → Player1, 1 → Player2).</summary>
    public static Cell ForPlayer(int playerIndex) => playerIndex == 0 ? Cell.Player1 : Cell.Player2;
}
