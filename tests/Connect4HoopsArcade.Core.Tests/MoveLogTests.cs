using Connect4HoopsArcade.Core.Practice;
using Xunit;

namespace Connect4HoopsArcade.Core.Tests;

public class MoveLogTests
{
    [Fact]
    public void UndoTurn_removes_cpu_reply_and_human_move()
    {
        var log = new MoveLog();
        log.Play(3); // you
        log.Play(2); // cpu
        log.UndoTurn();
        Assert.Empty(log.Played);
        Assert.True(log.CanRedo);
    }

    [Fact]
    public void UndoTurn_removes_just_the_human_move_when_no_cpu_reply()
    {
        var log = new MoveLog();
        log.Play(3); // you (e.g. a winning move; CPU never replied)
        log.UndoTurn();
        Assert.Empty(log.Played);
    }

    [Fact]
    public void Redo_reapplies_in_play_order()
    {
        var log = new MoveLog();
        log.Play(3); log.Play(2);
        log.UndoTurn();
        log.Redo();
        Assert.Equal(new[] { 3, 2 }, log.Played);
        Assert.False(log.CanRedo);
    }

    [Fact]
    public void Playing_a_new_move_clears_redo()
    {
        var log = new MoveLog();
        log.Play(3); log.Play(2);
        log.UndoTurn();
        log.Play(5);
        Assert.False(log.CanRedo);
    }
}
