using Connect4HoopsArcade.Core.Board;
using Connect4HoopsArcade.Core.Primitives;
using Connect4HoopsArcade.Core.Rules;

namespace Connect4HoopsArcade.Core.Ai;

/// <summary>
/// CPU is always Player2. Normal/Sharp use a minimax search with alpha-beta pruning and a
/// window-based evaluation (4-in-a-row potential + centre control). Chill stays deliberately weak
/// so it's beatable for casual/younger players.
/// </summary>
public static class CpuStrategy
{
    // Centre-first ordering improves alpha-beta pruning and biases play toward strong squares.
    private static readonly int[] Order = { 3, 2, 4, 1, 5, 0, 6 };
    private static readonly (int dc, int dr)[] Directions = { (1, 0), (0, 1), (1, 1), (1, -1) };

    private const int WinScore = 100_000;

    public static int ChooseColumn(GameBoard board, CpuDifficulty difficulty, Random? rng = null)
    {
        rng ??= Random.Shared;

        var available = Order.Where(c => board.LowestRow(c) >= 0).ToList();
        if (available.Count == 0) return -1;

        // Always grab an immediate win, at every difficulty.
        int win = ImmediateWin(board, Cell.Player2);
        if (win >= 0) return win;

        if (difficulty == CpuDifficulty.Chill)
        {
            // Easy mode: only block half the time, otherwise play loosely — beatable on purpose.
            int threat = ImmediateWin(board, Cell.Player1);
            if (threat >= 0 && rng.Next(2) == 0) return threat;
            return available[rng.Next(available.Count)];
        }

        // Normal / Sharp: search ahead. Depth tuned to stay snappy in WebAssembly while still
        // blocking threats and 2-move traps (a big step up from the old centre-stacking CPU).
        int depth = difficulty == CpuDifficulty.Sharp ? 5 : 4;

        int bestCol = available[0], bestScore = int.MinValue, alpha = int.MinValue;
        foreach (int c in available) // already centre-first
        {
            int r = board.LowestRow(c);
            var child = board.Clone();
            child.Drop(c, Cell.Player2);

            int score = WinDetector.FindWinningLine(child, c, r, Cell.Player2) is not null
                ? WinScore
                : Minimax(child, depth - 1, alpha, int.MaxValue, Cell.Player1);

            if (score > bestScore) { bestScore = score; bestCol = c; }
            if (bestScore > alpha) alpha = bestScore;
        }
        return bestCol;
    }

    /// <summary>Minimax (alpha-beta) score from the CPU's (Player2) perspective.</summary>
    private static int Minimax(GameBoard board, int depth, int alpha, int beta, Cell toMove)
    {
        if (depth == 0) return Evaluate(board);

        bool maximizing = toMove == Cell.Player2;
        Cell next = maximizing ? Cell.Player1 : Cell.Player2;
        int best = maximizing ? int.MinValue : int.MaxValue;
        bool anyMove = false;

        foreach (int c in Order)
        {
            int r = board.LowestRow(c);
            if (r < 0) continue;
            anyMove = true;

            var child = board.Clone();
            child.Drop(c, toMove);

            int score;
            if (WinDetector.FindWinningLine(child, c, r, toMove) is not null)
                // Deeper (sooner) wins score slightly higher so the CPU finishes quickly / stalls losses.
                score = maximizing ? WinScore + depth : -(WinScore + depth);
            else
                score = Minimax(child, depth - 1, alpha, beta, next);

            if (maximizing)
            {
                if (score > best) best = score;
                if (best > alpha) alpha = best;
            }
            else
            {
                if (score < best) best = score;
                if (best < beta) beta = best;
            }
            if (beta <= alpha) break; // prune
        }

        return anyMove ? best : 0; // no moves → full board → draw
    }

    /// <summary>Heuristic board score for the CPU (positive = good for Player2).</summary>
    private static int Evaluate(GameBoard board)
    {
        int score = 0;
        int centre = GameBoard.Columns / 2;
        for (int r = 0; r < GameBoard.Rows; r++)
        {
            if (board[centre, r] == Cell.Player2) score += 3;
            else if (board[centre, r] == Cell.Player1) score -= 3;
        }

        foreach (var (dc, dr) in Directions)
            for (int c = 0; c < GameBoard.Columns; c++)
                for (int r = 0; r < GameBoard.Rows; r++)
                {
                    int ec = c + 3 * dc, er = r + 3 * dr;
                    if (ec < 0 || ec >= GameBoard.Columns || er < 0 || er >= GameBoard.Rows) continue;

                    int cpu = 0, opp = 0, empty = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        var cell = board[c + k * dc, r + k * dr];
                        if (cell == Cell.Player2) cpu++;
                        else if (cell == Cell.Player1) opp++;
                        else empty++;
                    }
                    score += ScoreWindow(cpu, opp, empty);
                }
        return score;
    }

    // A "window" of four mixed cells is dead (no one can complete it) → 0.
    private static int ScoreWindow(int cpu, int opp, int empty) => (cpu, opp, empty) switch
    {
        (4, 0, 0) => 1000,
        (3, 0, 1) => 8,
        (2, 0, 2) => 3,
        (0, 4, 0) => -1000,
        (0, 3, 1) => -12,   // defend: penalise opponent threats a bit harder than our own
        (0, 2, 2) => -3,
        _ => 0,
    };

    private static int ImmediateWin(GameBoard board, Cell cell)
    {
        foreach (int c in Order)
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
