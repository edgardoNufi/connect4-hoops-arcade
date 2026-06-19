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

    [Fact]
    public void No_threat_when_horizontal_three_is_blocked_both_ends()
    {
        var b = new GameBoard();
        b.Drop(0, Cell.Player2);  // (0,0) blocks left end
        b.Drop(1, Cell.Player1);  // (1,0)
        b.Drop(2, Cell.Player1);  // (2,0)
        b.Drop(3, Cell.Player1);  // (3,0)  → P1 three-in-a-row at cols 1-3
        b.Drop(4, Cell.Player2);  // (4,0) blocks right end
        Assert.False(ThreatScanner.HasImmediateThreat(b, Cell.Player1));
    }

    [Fact]
    public void No_threat_when_vertical_three_is_capped()
    {
        var b = new GameBoard();
        b.Drop(3, Cell.Player1);  // (3,0)
        b.Drop(3, Cell.Player1);  // (3,1)
        b.Drop(3, Cell.Player1);  // (3,2)  → P1 vertical three
        b.Drop(3, Cell.Player2);  // (3,3) caps the column
        Assert.False(ThreatScanner.HasImmediateThreat(b, Cell.Player1));
    }

    [Fact]
    public void No_threat_when_winning_cell_is_not_yet_reachable()
    {
        // P1 has three-in-a-row on row 2 (cols 0-2); the winning cell (3,2) needs (3,0),(3,1)
        // filled first, so it is NOT immediately playable — "tapada" per the bug report.
        var b = new GameBoard();
        foreach (var c in new[] { 0, 1, 2 })
        {
            b.Drop(c, Cell.Player2);  // row 0
            b.Drop(c, Cell.Player2);  // row 1
            b.Drop(c, Cell.Player1);  // row 2  → P1 three across row 2
        }
        Assert.False(ThreatScanner.HasImmediateThreat(b, Cell.Player1));
    }

    [Fact]
    public void Detects_threat_with_one_open_playable_end()
    {
        // Control: a genuinely open three (col 3 immediately wins) must still alert.
        var b = new GameBoard();
        b.Drop(0, Cell.Player1);
        b.Drop(1, Cell.Player1);
        b.Drop(2, Cell.Player1);  // (3,0) is empty and playable → real threat
        Assert.True(ThreatScanner.HasImmediateThreat(b, Cell.Player1));
    }
}
