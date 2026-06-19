using Connect4HoopsArcade.Core.Board;
using Connect4HoopsArcade.Core.Primitives;
using Connect4HoopsArcade.Core.Rules;
using Xunit;

namespace Connect4HoopsArcade.Core.Tests;

public class WinDetectorTests
{
    // Drops a cell and returns (board, col, row) of the last move.
    private static (GameBoard b, int col, int row) Play(params (int col, Cell cell)[] moves)
    {
        var b = new GameBoard();
        int lastCol = 0, lastRow = 0;
        foreach (var (col, cell) in moves)
        {
            lastRow = b.Drop(col, cell);
            lastCol = col;
        }
        return (b, lastCol, lastRow);
    }

    [Fact]
    public void Detects_horizontal_four()
    {
        var (b, col, row) = Play(
            (0, Cell.Player1), (1, Cell.Player1), (2, Cell.Player1), (3, Cell.Player1));
        var line = WinDetector.FindWinningLine(b, col, row, Cell.Player1);
        Assert.NotNull(line);
        Assert.Equal(4, line!.Count);
    }

    [Fact]
    public void Returns_the_exact_winning_positions()
    {
        // Build a horizontal four on the bottom row, columns 0-3.
        var (b, col, row) = Play(
            (0, Cell.Player1), (1, Cell.Player1), (2, Cell.Player1), (3, Cell.Player1));
        var line = WinDetector.FindWinningLine(b, col, row, Cell.Player1);
        Assert.NotNull(line);
        var positions = line!.OrderBy(p => p.Col).ToArray();
        Assert.Equal(new[]
        {
            new BoardPosition(0, 0), new BoardPosition(1, 0),
            new BoardPosition(2, 0), new BoardPosition(3, 0),
        }, positions);
    }

    [Fact]
    public void Returns_null_when_placed_cell_does_not_match()
    {
        // Guard: asking about a coordinate the cell does not occupy yields no win.
        var b = new GameBoard();
        b.Drop(0, Cell.Player1);
        Assert.Null(WinDetector.FindWinningLine(b, 0, 0, Cell.Player2));
    }

    [Fact]
    public void Detects_vertical_four()
    {
        var (b, col, row) = Play(
            (4, Cell.Player2), (4, Cell.Player2), (4, Cell.Player2), (4, Cell.Player2));
        var line = WinDetector.FindWinningLine(b, col, row, Cell.Player2);
        Assert.NotNull(line);
        Assert.Equal(4, line!.Count);
    }

    [Fact]
    public void Detects_diagonal_up_right()
    {
        // Build a / diagonal for Player1 at columns 0..3.
        var b = new GameBoard();
        b.Drop(0, Cell.Player1);
        b.Drop(1, Cell.Player2); b.Drop(1, Cell.Player1);
        b.Drop(2, Cell.Player2); b.Drop(2, Cell.Player2); b.Drop(2, Cell.Player1);
        b.Drop(3, Cell.Player2); b.Drop(3, Cell.Player2); b.Drop(3, Cell.Player2);
        int row = b.Drop(3, Cell.Player1);
        var line = WinDetector.FindWinningLine(b, 3, row, Cell.Player1);
        Assert.NotNull(line);
        Assert.Equal(4, line!.Count);
    }

    [Fact]
    public void Detects_diagonal_down_right()
    {
        // Build a \ diagonal for Player1 at columns 0..3.
        var b = new GameBoard();
        b.Drop(0, Cell.Player2); b.Drop(0, Cell.Player2); b.Drop(0, Cell.Player2);
        int r0 = b.Drop(0, Cell.Player1);
        b.Drop(1, Cell.Player2); b.Drop(1, Cell.Player2); b.Drop(1, Cell.Player1);
        b.Drop(2, Cell.Player2); b.Drop(2, Cell.Player1);
        b.Drop(3, Cell.Player1);
        // The top-left to bottom-right line: (0,3),(1,2),(2,1),(3,0) — detect from (0, r0).
        var line = WinDetector.FindWinningLine(b, 0, r0, Cell.Player1);
        Assert.NotNull(line);
        Assert.Equal(4, line!.Count);
    }

    [Fact]
    public void No_win_returns_null()
    {
        var (b, col, row) = Play(
            (0, Cell.Player1), (1, Cell.Player1), (2, Cell.Player1));
        Assert.Null(WinDetector.FindWinningLine(b, col, row, Cell.Player1));
    }
}
