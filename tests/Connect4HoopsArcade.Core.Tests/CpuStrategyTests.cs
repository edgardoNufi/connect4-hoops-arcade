using Connect4HoopsArcade.Core.Ai;
using Connect4HoopsArcade.Core.Board;
using Connect4HoopsArcade.Core.Primitives;
using Xunit;

namespace Connect4HoopsArcade.Core.Tests;

public class CpuStrategyTests
{
    [Fact]
    public void Takes_winning_move_when_available()
    {
        var b = new GameBoard();
        b.Drop(0, Cell.Player2);
        b.Drop(1, Cell.Player2);
        b.Drop(2, Cell.Player2); // CPU (Player2) can win at col 3
        int col = CpuStrategy.ChooseColumn(b, CpuDifficulty.Normal);
        Assert.Equal(3, col);
    }

    [Fact]
    public void Blocks_opponent_win_when_not_chill()
    {
        var b = new GameBoard();
        b.Drop(0, Cell.Player1);
        b.Drop(1, Cell.Player1);
        b.Drop(2, Cell.Player1); // opponent threatens col 3
        int col = CpuStrategy.ChooseColumn(b, CpuDifficulty.Sharp);
        Assert.Equal(3, col);
    }

    [Fact]
    public void Prefers_center_on_empty_board_when_sharp()
    {
        int col = CpuStrategy.ChooseColumn(new GameBoard(), CpuDifficulty.Sharp);
        Assert.Equal(3, col);
    }

    [Fact]
    public void Returns_a_playable_column()
    {
        var b = new GameBoard();
        int col = CpuStrategy.ChooseColumn(b, CpuDifficulty.Normal);
        Assert.InRange(col, 0, 6);
        Assert.False(b.IsColumnFull(col));
    }

    [Fact]
    public void Chill_still_takes_its_own_winning_move()
    {
        var b = new GameBoard();
        b.Drop(0, Cell.Player2);
        b.Drop(1, Cell.Player2);
        b.Drop(2, Cell.Player2); // CPU can win at col 3 — win check applies to every difficulty
        int col = CpuStrategy.ChooseColumn(b, CpuDifficulty.Chill);
        Assert.Equal(3, col);
    }

    [Fact]
    public void Returns_minus_one_when_board_full()
    {
        var b = new GameBoard();
        for (int c = 0; c < 7; c++)
            for (int r = 0; r < 6; r++)
                b.Drop(c, c % 2 == 0 ? Cell.Player1 : Cell.Player2); // fill without a 4-in-a-row
        Assert.Equal(-1, CpuStrategy.ChooseColumn(b, CpuDifficulty.Normal));
    }
}
