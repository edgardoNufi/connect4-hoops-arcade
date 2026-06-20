using Connect4HoopsArcade.Core.Narration;
using Xunit;

namespace Connect4HoopsArcade.Core.Tests;

public class CpuTauntPolicyTests
{
    [Theory]
    [InlineData(0, CpuTauntLevel.Neutral)]
    [InlineData(1, CpuTauntLevel.LightChallenge)]
    [InlineData(2, CpuTauntLevel.ConfidentCpu)]
    [InlineData(3, CpuTauntLevel.BossMode)]
    [InlineData(10, CpuTauntLevel.BossMode)]
    public void LevelFor_maps_streak_to_level(int streak, CpuTauntLevel expected)
        => Assert.Equal(expected, CpuTauntPolicy.LevelFor(streak));

    [Fact]
    public void AdvanceStreak_same_winner_extends_streak_no_break()
    {
        var s = CpuTauntPolicy.AdvanceStreak(prevHolder: 1, prevStreak: 2, winner: 1);
        Assert.Equal(1, s.Holder);
        Assert.Equal(3, s.Streak);
        Assert.Equal(0, s.BrokenLength);
    }

    [Fact]
    public void AdvanceStreak_first_winner_starts_streak_no_break()
    {
        var s = CpuTauntPolicy.AdvanceStreak(prevHolder: -1, prevStreak: 0, winner: 0);
        Assert.Equal(0, s.Holder);
        Assert.Equal(1, s.Streak);
        Assert.Equal(0, s.BrokenLength);
    }

    [Theory]
    [InlineData(1, 1)]   // breaking a streak of 1 reports length 1
    [InlineData(2, 2)]   // exactly the normal break threshold
    [InlineData(5, 5)]   // a big streak broken reports its full length
    public void AdvanceStreak_different_winner_resets_and_reports_broken_length(int prevStreak, int expectedBroken)
    {
        var s = CpuTauntPolicy.AdvanceStreak(prevHolder: 1, prevStreak: prevStreak, winner: 0);
        Assert.Equal(0, s.Holder);
        Assert.Equal(1, s.Streak);
        Assert.Equal(expectedBroken, s.BrokenLength);
    }

    [Fact]
    public void AdvanceStreak_draw_leaves_streak_untouched()
    {
        var s = CpuTauntPolicy.AdvanceStreak(prevHolder: 1, prevStreak: 3, winner: null);
        Assert.Equal(1, s.Holder);
        Assert.Equal(3, s.Streak);
        Assert.Equal(0, s.BrokenLength);
    }
}
