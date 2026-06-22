using Connect4HoopsArcade.Core.Board;
using Connect4HoopsArcade.Core.Practice;
using Connect4HoopsArcade.Core.Primitives;
using Xunit;

namespace Connect4HoopsArcade.Core.Tests;

public class BoardReplayTests
{
    [Fact]
    public void Alternates_players_starting_from_starter()
    {
        var b = BoardReplay.FromColumns(new[] { 3, 3, 4 }, Cell.Player1);
        Assert.Equal(Cell.Player1, b[3, 0]); // move 0 (starter)
        Assert.Equal(Cell.Player2, b[3, 1]); // move 1 (other)
        Assert.Equal(Cell.Player1, b[4, 0]); // move 2 (starter)
    }

    [Fact]
    public void Empty_list_gives_empty_board()
    {
        var b = BoardReplay.FromColumns(System.Array.Empty<int>(), Cell.Player1);
        Assert.Equal(Cell.Empty, b[0, 0]);
    }
}
