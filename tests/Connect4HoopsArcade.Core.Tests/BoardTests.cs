using Connect4HoopsArcade.Core.Board;
using Connect4HoopsArcade.Core.Primitives;
using Xunit;

namespace Connect4HoopsArcade.Core.Tests;

public class BoardTests
{
    [Fact]
    public void New_board_is_empty_7x6()
    {
        var b = new GameBoard();
        Assert.Equal(7, GameBoard.Columns);
        Assert.Equal(6, GameBoard.Rows);
        for (int c = 0; c < 7; c++)
            for (int r = 0; r < 6; r++)
                Assert.Equal(Cell.Empty, b[c, r]);
    }

    [Fact]
    public void LowestRow_is_zero_on_empty_column()
    {
        var b = new GameBoard();
        Assert.Equal(0, b.LowestRow(3));
    }

    [Fact]
    public void Drop_stacks_from_the_bottom_up()
    {
        var b = new GameBoard();
        Assert.Equal(0, b.Drop(2, Cell.Player1));
        Assert.Equal(1, b.Drop(2, Cell.Player2));
        Assert.Equal(Cell.Player1, b[2, 0]);
        Assert.Equal(Cell.Player2, b[2, 1]);
    }

    [Fact]
    public void Drop_returns_minus_one_when_column_full()
    {
        var b = new GameBoard();
        for (int i = 0; i < 6; i++) b.Drop(0, Cell.Player1);
        Assert.True(b.IsColumnFull(0));
        Assert.Equal(-1, b.LowestRow(0));
        Assert.Equal(-1, b.Drop(0, Cell.Player2));
    }

    [Fact]
    public void IsBoardFull_true_only_when_every_top_cell_filled()
    {
        var b = new GameBoard();
        Assert.False(b.IsBoardFull());
        for (int c = 0; c < 7; c++)
            for (int r = 0; r < 6; r++)
                b.Drop(c, Cell.Player1);
        Assert.True(b.IsBoardFull());
    }

    [Fact]
    public void Clone_is_independent()
    {
        var b = new GameBoard();
        b.Drop(1, Cell.Player1);
        var c = b.Clone();
        c.Drop(1, Cell.Player2);
        Assert.Equal(Cell.Empty, b[1, 1]);   // original unchanged
        Assert.Equal(Cell.Player2, c[1, 1]);
    }
}
