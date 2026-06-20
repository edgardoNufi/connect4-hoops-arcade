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
        Assert.Equal(3, CpuStrategy.ChooseColumn(b, CpuDifficulty.Amateur));
    }

    [Fact]
    public void Mvp_blocks_opponent_immediate_win()
    {
        var b = new GameBoard();
        b.Drop(0, Cell.Player1);
        b.Drop(1, Cell.Player1);
        b.Drop(2, Cell.Player1); // opponent threatens col 3
        Assert.Equal(3, CpuStrategy.ChooseColumn(b, CpuDifficulty.MVP));
    }

    [Fact]
    public void Prefers_center_on_empty_board_when_mvp()
        => Assert.Equal(3, CpuStrategy.ChooseColumn(new GameBoard(), CpuDifficulty.MVP));

    [Fact]
    public void Returns_a_playable_column()
    {
        var b = new GameBoard();
        int col = CpuStrategy.ChooseColumn(b, CpuDifficulty.Amateur);
        Assert.InRange(col, 0, 6);
        Assert.False(b.IsColumnFull(col));
    }

    [Fact]
    public void Novato_still_takes_its_own_winning_move()
    {
        var b = new GameBoard();
        b.Drop(0, Cell.Player2);
        b.Drop(1, Cell.Player2);
        b.Drop(2, Cell.Player2); // win check applies to every level
        Assert.Equal(3, CpuStrategy.ChooseColumn(b, CpuDifficulty.Novato));
    }

    [Fact]
    public void Amateur_blocks_a_vertical_threat_off_center()
    {
        // Player1 stacks three in col 6 (off-centre) → depth-2 search sees the win and must block at col 6.
        var b = new GameBoard();
        b.Drop(6, Cell.Player1);
        b.Drop(6, Cell.Player1);
        b.Drop(6, Cell.Player1);
        Assert.Equal(6, CpuStrategy.ChooseColumn(b, CpuDifficulty.Amateur));
    }

    [Fact]
    public void Mvp_blocks_a_developing_horizontal_threat()
    {
        var b = new GameBoard();
        b.Drop(1, Cell.Player2);  // blocks the left end
        b.Drop(2, Cell.Player1);
        b.Drop(3, Cell.Player1);
        b.Drop(4, Cell.Player1);
        Assert.Equal(5, CpuStrategy.ChooseColumn(b, CpuDifficulty.MVP));
    }

    [Fact]
    public void Returns_minus_one_when_board_full()
    {
        var b = new GameBoard();
        for (int c = 0; c < 7; c++)
            for (int r = 0; r < 6; r++)
                b.Drop(c, c % 2 == 0 ? Cell.Player1 : Cell.Player2);
        Assert.Equal(-1, CpuStrategy.ChooseColumn(b, CpuDifficulty.Amateur));
    }
}
