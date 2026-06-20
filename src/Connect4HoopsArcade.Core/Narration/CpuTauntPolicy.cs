namespace Connect4HoopsArcade.Core.Narration;

/// <summary>How spicy the announcer should be, driven by the CPU's consecutive-win streak.</summary>
public enum CpuTauntLevel { Neutral, LightChallenge, ConfidentCpu, BossMode }

/// <summary>Outcome of a finished match, from the CPU's perspective (1-player).</summary>
public enum MatchOutcome { CpuWin, HumanWin, Draw }

/// <summary>Result of advancing the streak state after a match.</summary>
public readonly record struct StreakState(int Streak, bool JustBroken, int PlayerLosses);

/// <summary>Pure rules for the CPU taunt escalation. No audio, no Blazor.</summary>
public static class CpuTauntPolicy
{
    /// <summary>A CPU streak of this many wins makes breaking it "meaningful" (special line).</summary>
    public const int BreakThreshold = 2;

    public static CpuTauntLevel LevelFor(int cpuWinStreak) => cpuWinStreak switch
    {
        <= 0 => CpuTauntLevel.Neutral,
        1 => CpuTauntLevel.LightChallenge,
        2 => CpuTauntLevel.ConfidentCpu,
        _ => CpuTauntLevel.BossMode,
    };

    public static StreakState Advance(int prevStreak, int prevLosses, MatchOutcome outcome) => outcome switch
    {
        MatchOutcome.CpuWin   => new StreakState(prevStreak + 1, false, prevLosses + 1),
        MatchOutcome.HumanWin => new StreakState(0, prevStreak >= BreakThreshold, prevLosses),
        _                     => new StreakState(prevStreak, false, prevLosses),
    };
}
