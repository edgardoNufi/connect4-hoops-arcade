using Connect4HoopsArcade.Core.Board;
using Connect4HoopsArcade.Core.Primitives;
using Connect4HoopsArcade.Core.Rules;
using Xunit;

namespace Connect4HoopsArcade.Core.Tests;

public class FindWinningColumnTests
{
    [Fact]
    public void Returns_the_column_that_completes_four()
    {
        var b = new GameBoard();
        b.Drop(0, Cell.Player1); b.Drop(1, Cell.Player1); b.Drop(2, Cell.Player1); // 3 in a row
        Assert.Equal(3, ThreatScanner.FindWinningColumn(b, Cell.Player1)); // col 3 wins
    }

    [Fact]
    public void Returns_minus_one_when_no_immediate_win()
    {
        var b = new GameBoard();
        b.Drop(0, Cell.Player1);
        Assert.Equal(-1, ThreatScanner.FindWinningColumn(b, Cell.Player1));
    }
}
