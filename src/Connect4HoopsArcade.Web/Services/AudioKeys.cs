namespace Connect4HoopsArcade.Web.Services;

/// <summary>Audio file keys, relative to wwwroot/audio/. Matches the actual flat folder layout.</summary>
public static class AudioKeys
{
    // SFX
    public const string ButtonClick = "ui/button-click.mp3";
    public const string MenuMove    = "ui/menu-move.mp3";
    public const string Back        = "ui/back.mp3";
    public const string ChipDrop    = "game/chip-drop.mp3";
    public const string TurnChange  = "game/turn-change.mp3";
    public const string AlmostWin   = "game/almost-win.mp3";
    public const string ColumnFull  = "game/column-full.mp3";
    public const string VictorySfx  = "victory/connect-four.mp3";
    public const string DrawSfx     = "victory/draw.mp3";

    // Win cheers — picked at random so it isn't the same every time. (.m4a holds AAC/MP4 audio,
    // which the browser plays like an mp4; add more variants here to grow the pool.)
    public static readonly string[] WinSfx = { "victory/win-01.mp3", "victory/win-02.m4a" };

    // Music
    public const string AttractLoop = "music/attract-loop.mp3";

    // Voice groups
    public static readonly string[] PlayerOneTurn = { "voice/player-one-turn-01.mp3", "voice/player-one-turn-02.mp3", "voice/player-one-turn-03.mp3" };
    public static readonly string[] PlayerTwoTurn = { "voice/player-two-turn-01.mp3", "voice/player-two-turn-02.mp3", "voice/player-two-turn-03.mp3" };
    public static readonly string[] GreatMove     = { "voice/great-move-01.mp3", "voice/great-move-02.mp3", "voice/great-move-03.mp3", "voice/great-move-04.mp3", "voice/great-move-05.mp3" };
    public static readonly string[] AlmostWinV    = { "voice/almost-win-01.mp3", "voice/almost-win-02.mp3", "voice/almost-win-03.mp3", "voice/almost-win-04.mp3" };
    public static readonly string[] ColumnFullV   = { "voice/column-full-01.mp3", "voice/column-full-02.mp3", "voice/column-full-03.mp3", "voice/column-full-04.mp3" };
    public static readonly string[] VictoryV      = { "voice/winner-01.mp3", "voice/victory-01.mp3", "voice/victory-02.mp3", "voice/victory-03.mp3" };
    public static readonly string[] DrawV         = { "voice/draw-01.mp3", "voice/draw-02.mp3", "voice/draw-03.mp3" };

    // Single voices
    public const string Welcome        = "voice/welcome-01.mp3";
    public const string SelectGameMode = "voice/select-game-mode-01.mp3";
    public const string ChooseCharacter= "voice/choose-character-01.mp3";
    public const string GetReady       = "voice/get-ready-01.mp3";
    public const string ConnectFourV   = "voice/connect-four-01.mp3";
    public const string Rematch        = "voice/rematch-01.mp3";

    // ---- CPU taunts (1P) + universal idle. Arrays rotate variants; add more files + entries to grow. ----
    public static readonly string[] CpuThreatNeutral   = { "voice/cpu-threat-neutral-01.mp3", "voice/cpu-threat-neutral-02.mp3", "voice/cpu-threat-neutral-03.mp3" };
    public static readonly string[] CpuThreatLight     = { "voice/cpu-threat-light-01.mp3", "voice/cpu-threat-light-02.mp3", "voice/cpu-threat-light-03.mp3" };
    public static readonly string[] CpuThreatConfident = { "voice/cpu-threat-confident-01.mp3", "voice/cpu-threat-confident-02.mp3", "voice/cpu-threat-confident-03.mp3" };
    public static readonly string[] CpuThreatBoss      = { "voice/cpu-threat-boss-01.mp3", "voice/cpu-threat-boss-02.mp3", "voice/cpu-threat-boss-03.mp3" };

    public static readonly string[] CpuIdleNeutral     = { "voice/cpu-idle-neutral-01.mp3", "voice/cpu-idle-neutral-02.mp3", "voice/cpu-idle-neutral-03.mp3" };
    public static readonly string[] CpuIdleLight       = { "voice/cpu-idle-light-01.mp3", "voice/cpu-idle-light-02.mp3", "voice/cpu-idle-light-03.mp3" };
    public static readonly string[] CpuIdleConfident   = { "voice/cpu-idle-confident-01.mp3", "voice/cpu-idle-confident-02.mp3", "voice/cpu-idle-confident-03.mp3" };
    public static readonly string[] CpuIdleBoss        = { "voice/cpu-idle-boss-01.mp3", "voice/cpu-idle-boss-02.mp3", "voice/cpu-idle-boss-03.mp3" };

    public static readonly string[] CpuWinLight        = { "voice/cpu-win-light-01.mp3", "voice/cpu-win-light-02.mp3", "voice/cpu-win-light-03.mp3" };
    public static readonly string[] CpuWinConfident    = { "voice/cpu-win-confident-01.mp3", "voice/cpu-win-confident-02.mp3", "voice/cpu-win-confident-03.mp3" };
    public static readonly string[] CpuWinBoss         = { "voice/cpu-win-boss-01.mp3", "voice/cpu-win-boss-02.mp3", "voice/cpu-win-boss-03.mp3" };

    public static readonly string[] StreakBreak        = { "voice/streak-break-01.mp3", "voice/streak-break-02.mp3", "voice/streak-break-03.mp3" };
    public static readonly string[] StreakBreakBig     = { "voice/streak-break-big-01.mp3", "voice/streak-break-big-02.mp3", "voice/streak-break-big-03.mp3" };
    public static readonly string[] BeatCpu            = { "voice/beat-cpu-01.mp3", "voice/beat-cpu-02.mp3", "voice/beat-cpu-03.mp3" };
    public static readonly string[] IdleNudge          = { "voice/idle-01.mp3", "voice/idle-02.mp3", "voice/idle-03.mp3", "voice/idle-04.mp3", "voice/idle-05.mp3" };

    // Short SFX rotated (instead of the win cheer) when the CPU beats the player in 1P.
    public static readonly string[] LossSting          = { "game/loss-sting.mp3", "game/loss-sting-01.mp3" };
    // Celebratory "aleluya" sting played AFTER the voice when a big (+3) streak is broken — replaces the win cheer.
    public const string StreakBreakBigSting = "game/streak-break.mp3";
}
