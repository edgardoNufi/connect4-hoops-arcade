using Connect4HoopsArcade.Core.Narration;

namespace Connect4HoopsArcade.Web.Services;

/// <summary>Static data table: maps a taunt level to the matching AudioKeys voice array. No state, no events.</summary>
public static class CpuTauntLines
{
    public static IReadOnlyList<string> Threat(CpuTauntLevel level) => level switch
    {
        CpuTauntLevel.LightChallenge => AudioKeys.CpuThreatLight,
        CpuTauntLevel.ConfidentCpu   => AudioKeys.CpuThreatConfident,
        CpuTauntLevel.BossMode       => AudioKeys.CpuThreatBoss,
        _                            => AudioKeys.CpuThreatNeutral,
    };

    public static IReadOnlyList<string> Idle(CpuTauntLevel level) => level switch
    {
        CpuTauntLevel.LightChallenge => AudioKeys.CpuIdleLight,
        CpuTauntLevel.ConfidentCpu   => AudioKeys.CpuIdleConfident,
        CpuTauntLevel.BossMode       => AudioKeys.CpuIdleBoss,
        _                            => AudioKeys.CpuIdleNeutral,
    };

    // A CPU win always leaves streak >= 1, so Neutral collapses to Light.
    public static IReadOnlyList<string> CpuWin(CpuTauntLevel level) => level switch
    {
        CpuTauntLevel.ConfidentCpu => AudioKeys.CpuWinConfident,
        CpuTauntLevel.BossMode     => AudioKeys.CpuWinBoss,
        _                          => AudioKeys.CpuWinLight,
    };
}
