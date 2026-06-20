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
    public void Advance_cpu_win_increments_streak_and_losses()
    {
        var s = CpuTauntPolicy.Advance(prevStreak: 1, prevLosses: 3, MatchOutcome.CpuWin);
        Assert.Equal(2, s.Streak);
        Assert.Equal(4, s.PlayerLosses);
        Assert.False(s.JustBroken);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(5, true)]
    public void Advance_human_win_resets_streak_and_flags_break_when_meaningful(int prevStreak, bool expectedBroken)
    {
        var s = CpuTauntPolicy.Advance(prevStreak, prevLosses: 7, MatchOutcome.HumanWin);
        Assert.Equal(0, s.Streak);
        Assert.Equal(7, s.PlayerLosses);   // unchanged on a human win
        Assert.Equal(expectedBroken, s.JustBroken);
    }

    [Fact]
    public void Advance_draw_leaves_everything_unchanged()
    {
        var s = CpuTauntPolicy.Advance(prevStreak: 2, prevLosses: 4, MatchOutcome.Draw);
        Assert.Equal(2, s.Streak);
        Assert.Equal(4, s.PlayerLosses);
        Assert.False(s.JustBroken);
    }
}
