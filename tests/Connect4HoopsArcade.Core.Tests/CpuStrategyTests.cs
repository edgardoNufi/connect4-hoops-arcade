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
}
