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
}
