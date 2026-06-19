using Connect4HoopsArcade.Core.Board;
using Connect4HoopsArcade.Core.Primitives;
using Connect4HoopsArcade.Core.Rules;

namespace Connect4HoopsArcade.Core.Ai;

/// <summary>CPU is always Player2. Mirrors the imported design's heuristic.</summary>
public static class CpuStrategy
{
    private static readonly int[] SharpOrder  = { 3, 2, 4, 1, 5, 0, 6 };
    private static readonly int[] NormalOrder = { 3, 4, 2, 5, 1, 6, 0 };

    public static int ChooseColumn(GameBoard board, CpuDifficulty difficulty, Random? rng = null)
    {
        // 1) Win if possible.
        int win = WinningColumnFor(board, Cell.Player2);
        if (win >= 0) return win;

        // 2) Block opponent (unless chill).
        if (difficulty != CpuDifficulty.Chill)
        {
            int block = WinningColumnFor(board, Cell.Player1);
            if (block >= 0) return block;
        }

        // 3) Positional preference.
        var order = difficulty == CpuDifficulty.Sharp ? SharpOrder : NormalOrder;
        var available = order.Where(c => board.LowestRow(c) >= 0).ToList();
        if (available.Count == 0) return -1;

        if (difficulty == CpuDifficulty.Chill)
        {
            rng ??= Random.Shared;
            return available[rng.Next(available.Count)];
        }
        return available[0];
    }

    private static int WinningColumnFor(GameBoard board, Cell cell)
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
