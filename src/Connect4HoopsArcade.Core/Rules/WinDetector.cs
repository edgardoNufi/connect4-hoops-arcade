using Connect4HoopsArcade.Core.Board;
using Connect4HoopsArcade.Core.Primitives;

namespace Connect4HoopsArcade.Core.Rules;

public static class WinDetector
{
    private static readonly (int dc, int dr)[] Directions =
        { (1, 0), (0, 1), (1, 1), (1, -1) };

    /// <summary>
    /// If placing <paramref name="cell"/> at (col,row) completes 4+ in a row, returns exactly the 4
    /// winning positions (containing the placed cell); otherwise null.
    /// </summary>
    public static IReadOnlyList<BoardPosition>? FindWinningLine(GameBoard board, int col, int row, Cell cell)
    {
        // Precondition: the placed cell must actually occupy (col,row).
        if (board[col, row] != cell) return null;

        foreach (var (dc, dr) in Directions)
        {
            var line = BuildLine(board, col, row, dc, dr, cell);
            if (line.Count >= 4)
            {
                int idx = line.FindIndex(p => p.Col == col && p.Row == row);
                int start = Math.Max(0, Math.Min(idx, line.Count - 4));
                return line.GetRange(start, 4);
            }
        }
        return null;
    }

    private static List<BoardPosition> BuildLine(GameBoard board, int col, int row, int dc, int dr, Cell cell)
    {
        var line = new List<BoardPosition> { new(col, row) };
        Extend(board, col, row, dc, dr, cell, line, append: true);
        Extend(board, col, row, -dc, -dr, cell, line, append: false);
        return line;
    }

    private static void Extend(GameBoard board, int col, int row, int dc, int dr, Cell cell,
        List<BoardPosition> line, bool append)
    {
        int c = col + dc, r = row + dr;
        while (c >= 0 && c < GameBoard.Columns && r >= 0 && r < GameBoard.Rows && board[c, r] == cell)
        {
            if (append) line.Add(new(c, r));
            else line.Insert(0, new(c, r));
            c += dc; r += dr;
        }
    }
}
