using Connect4HoopsArcade.Core.Board;
using Connect4HoopsArcade.Core.Primitives;
using Connect4HoopsArcade.Core.Players;

namespace Connect4HoopsArcade.Web.State;

/// <summary>Single source of truth for runtime game state. Components subscribe to <see cref="StateChanged"/>.</summary>
public sealed class GameSession
{
    public event Action? StateChanged;
    private void Notify() => StateChanged?.Invoke();

    public AppScreen Screen { get; private set; } = AppScreen.Splash;
    private AppScreen _prevScreen = AppScreen.Mode;

    public GameMode Mode { get; private set; } = GameMode.TwoPlayer;
    public PlayerConfig[] Players { get; private set; } =
        { PlayerConfig.DefaultP1, PlayerConfig.DefaultP2 };
    public GameBoard Board { get; private set; } = new();
    public int Current { get; private set; }
    public int[] Scores { get; private set; } = { 0, 0 };
    public string Narrator { get; set; } = "";

    // ---- navigation ----
    public void GoSplash() { Screen = AppScreen.Splash; Notify(); }
    public void GoMode() { Screen = AppScreen.Mode; Notify(); }
    public void ChooseOnePlayer()
    {
        Mode = GameMode.OnePlayer;
        Players = new[] { Players[0] with { IsCpu = false }, PlayerConfig.DefaultCpu };
        Screen = AppScreen.Setup; Notify();
    }
    public void ChooseTwoPlayer()
    {
        Mode = GameMode.TwoPlayer;
        Players = new[] { PlayerConfig.DefaultP1, PlayerConfig.DefaultP2 };
        Screen = AppScreen.Setup; Notify();
    }
    public void OpenSettings() { _prevScreen = Screen; Screen = AppScreen.Settings; Notify(); }
    public void CloseSettings() { Screen = _prevScreen; Notify(); }
    public void OpenSensors() { _prevScreen = Screen; Screen = AppScreen.Sensors; Notify(); }
    public void CloseSensors() { Screen = AppScreen.Mode; Notify(); }

    // ---- setup mutation ----
    public void SetPlayer(int index, PlayerConfig config)
    {
        Players[index] = config;
        Notify();
    }

    // Move flow (TryDrop, CPU, win/draw) is implemented in Phase 4.
}
