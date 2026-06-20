namespace Connect4HoopsArcade.Core.Narration;

/// <summary>How spicy the announcer should be, driven by the leader's consecutive-win streak.</summary>
public enum CpuTauntLevel { Neutral, LightChallenge, ConfidentCpu, BossMode }

/// <summary>
/// Result of advancing the generic win-streak after a match: who now leads, their streak length, and the
/// length of any opponent streak this match's winner just broke (0 if none broken).
/// </summary>
public readonly record struct StreakOutcome(int Holder, int Streak, int BrokenLength);

/// <summary>Pure rules for the announcer's streak escalation and streak-break detection. No audio, no Blazor.</summary>
public static class CpuTauntPolicy
{
    /// <summary>Breaking an opponent streak of this length triggers the normal "streak broken" line.</summary>
    public const int BreakThreshold = 2;

    /// <summary>Breaking an opponent streak of this length or more triggers the special "big break" line.</summary>
    public const int BigBreakThreshold = 3;

    /// <summary>Maps a leader's consecutive-win count to a taunt level. 0→Neutral, 1→Light, 2→Confident, ≥3→Boss.</summary>
    public static CpuTauntLevel LevelFor(int winStreak) => winStreak switch
    {
        <= 0 => CpuTauntLevel.Neutral,
        1 => CpuTauntLevel.LightChallenge,
        2 => CpuTauntLevel.ConfidentCpu,
        _ => CpuTauntLevel.BossMode,
    };

    /// <summary>
    /// Advances the win-streak after a match. <paramref name="prevHolder"/> is the player index currently on a
    /// streak (-1 if none), <paramref name="prevStreak"/> its length, <paramref name="winner"/> the match winner
    /// (null = draw). A draw leaves the streak untouched; the same winner extends it; a different winner resets
    /// it to 1 and reports the broken opponent streak length.
    /// </summary>
    public static StreakOutcome AdvanceStreak(int prevHolder, int prevStreak, int? winner)
    {
        if (winner is null) return new StreakOutcome(prevHolder, prevStreak, 0);
        int w = winner.Value;
        if (w == prevHolder) return new StreakOutcome(w, prevStreak + 1, 0);
        int broken = prevHolder >= 0 ? prevStreak : 0;
        return new StreakOutcome(w, 1, broken);
    }
}
