using Connect4HoopsArcade.Core.Board;
using Connect4HoopsArcade.Core.Primitives;
using Connect4HoopsArcade.Core.Rules;
using Xunit;

namespace Connect4HoopsArcade.Core.Tests;

public class ThreatScannerTests
{
    [Fact]
    public void Detects_immediate_winning_threat()
    {
        var b = new GameBoard();
        b.Drop(0, Cell.Player1);
        b.Drop(1, Cell.Player1);
        b.Drop(2, Cell.Player1); // three in a row; col 3 (or -1 left) would win
        Assert.True(ThreatScanner.HasImmediateThreat(b, Cell.Player1));
    }

    [Fact]
    public void No_threat_on_empty_board()
    {
        Assert.False(ThreatScanner.HasImmediateThreat(new GameBoard(), Cell.Player1));
    }
}
