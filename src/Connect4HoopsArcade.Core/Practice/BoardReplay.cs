using Connect4HoopsArcade.Core.Board;
using Connect4HoopsArcade.Core.Primitives;

namespace Connect4HoopsArcade.Core.Practice;

/// <summary>Rebuilds a board from a sequence of dropped columns, alternating players each move
/// starting from <paramref name="starter"/>. Used by practice undo/redo (board is recreated from the log).</summary>
public static class BoardReplay
{
    public static GameBoard FromColumns(IReadOnlyList<int> columns, Cell starter)
    {
        var other = starter == Cell.Player1 ? Cell.Player2 : Cell.Player1;
        var board = new GameBoard();
        for (int i = 0; i < columns.Count; i++)
            board.Drop(columns[i], i % 2 == 0 ? starter : other);
        return board;
    }
}
