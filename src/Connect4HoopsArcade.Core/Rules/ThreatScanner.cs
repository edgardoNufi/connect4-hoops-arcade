using Connect4HoopsArcade.Core.Board;
using Connect4HoopsArcade.Core.Primitives;

namespace Connect4HoopsArcade.Core.Rules;

public static class ThreatScanner
{
    /// <summary>True if <paramref name="cell"/> has a move that immediately wins.</summary>
    public static bool HasImmediateThreat(GameBoard board, Cell cell)
    {
        for (int c = 0; c < GameBoard.Columns; c++)
        {
            int r = board.LowestRow(c);
            if (r < 0) continue;
            var trial = board.Clone();
            trial.Drop(c, cell);
            if (WinDetector.FindWinningLine(trial, c, r, cell) is not null) return true;
        }
        return false;
    }

    /// <summary>The column where <paramref name="cell"/> wins immediately, or -1 if none.</summary>
    public static int FindWinningColumn(GameBoard board, Cell cell)
    {
        for (int c = 0; c < GameBoard.Columns; c++)
        {
            int r = board.LowestRow(c);
            if (r < 0) continue;
            var trial = board.Clone();
            trial.Drop(c, cell);
            if (WinDetector.FindWinningLine(trial, c, r, cell) is not null) return c;
        }
        return -1;
    }
}
