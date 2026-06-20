using Connect4HoopsArcade.Core.Ai;
using Connect4HoopsArcade.Core.Board;
using Connect4HoopsArcade.Core.Narration;
using Connect4HoopsArcade.Core.Primitives;
using Connect4HoopsArcade.Core.Players;
using Connect4HoopsArcade.Core.Rules;

namespace Connect4HoopsArcade.Web.State;

/// <summary>Single source of truth for runtime game state. Components subscribe to <see cref="StateChanged"/>.</summary>
public sealed class GameSession
{
    public event Action? StateChanged;
    // Audio/narration hooks (subscribed by NarratorService/AudioService in Phase 6).
    public event Action? ChipDropped;
    public event Action<int>? TurnChanged;     // arg: new current player index
    public event Action? ColumnFull;
    public event Action<int>? ThreatRaised;    // arg: index of the player who just moved (the threat owner)
    // Retained intentionally: superseded by MatchEnded for closing audio (no current subscribers). See spec §2/§7.
    public event Action<int>? Won;             // arg: winner index
    public event Action? Drew;
    public event Action? GameStarted;
    public event Action? RoundStarted;          // every BeginGame/Rematch/ResetBoard — resets per-round narration
    public event Action? IdleNudged;            // the idle nudge fired (any mode)
    public event Action<int?, GameMode>? MatchEnded;   // winner (null = draw) + mode; raised after streak update

    private void Notify() => StateChanged?.Invoke();

    public AppScreen Screen { get; private set; } = AppScreen.Splash;
    private AppScreen _prevScreen = AppScreen.Mode;

    public GameMode Mode { get; private set; } = GameMode.TwoPlayer;
    public CpuDifficulty CpuLevel { get; set; } = CpuDifficulty.Sharp;
    public AnimationSpeed Speed { get; set; } = AnimationSpeed.Normal;
    // Persistent announcer tone (pushed by SettingsStore.ApplyAsync). NOT reset between games.
    public Connect4HoopsArcade.Web.Models.NarratorTone NarratorTone { get; set; }
        = Connect4HoopsArcade.Web.Models.NarratorTone.Familiar;

    public Connect4HoopsArcade.Web.Models.PlayMode Mode2 { get; private set; } = Connect4HoopsArcade.Web.Models.PlayMode.Digital;
    public bool SensorConnected { get; private set; }
    public event Action? ModeChanged;

    public void SetPlayMode(Connect4HoopsArcade.Web.Models.PlayMode mode)
    {
        if (Mode2 == mode) return;
        Mode2 = mode;
        ModeChanged?.Invoke();
        Notify();
    }

    /// <summary>Reports sensor link state; autodetection promotes Digital→Physical and falls back on loss.</summary>
    public void SetSensorConnected(bool connected, bool autoSwitch = true)
    {
        SensorConnected = connected;
        if (autoSwitch) SetPlayMode(connected ? Connect4HoopsArcade.Web.Models.PlayMode.Physical
                                              : Connect4HoopsArcade.Web.Models.PlayMode.Digital);
        else Notify();
    }

    public PlayerConfig[] Players { get; private set; } =
        { PlayerConfig.DefaultP1, PlayerConfig.DefaultP2 };
    public GameBoard Board { get; private set; } = new();
    public int Current { get; private set; }
    public int[] Scores { get; private set; } = { 0, 0 };
    // Win-streak state (BOTH modes). Tracks the player currently on a consecutive-win run. Persists across
    // Rematch/ResetBoard; reset only by BeginGame (new session).
    public int StreakHolder { get; private set; } = -1;       // player index on a streak, or -1 if none
    public int WinStreak { get; private set; }                // that player's consecutive wins
    public int BrokenStreakLength { get; private set; }       // transient: opponent streak the last winner just broke
    public int PlayerLossesAgainstCpu { get; private set; }   // 1P only; informational
    // CPU's own current streak (feeds the 1P taunt level). Derived: only meaningful when the CPU leads in 1P.
    public int CpuWinStreak => Mode == GameMode.OnePlayer && StreakHolder == 1 ? WinStreak : 0;
    public string Narrator { get; private set; } = "";

    public int? Winner { get; private set; }
    public string WinBy { get; private set; } = "";   // "connect" | "resign"
    public HashSet<BoardPosition> WinningCells { get; private set; } = new();
    public BoardPosition? LastDrop { get; private set; }
    public int ErrorCol { get; private set; } = -1;
    public bool IsThinking { get; private set; }
    public bool IsIdle { get; private set; }
    public bool IsBusy { get; private set; }          // a move animation/transition in progress
    public List<ConfettiPiece> Confetti { get; private set; } = new();

    public double DropSeconds => Speed == AnimationSpeed.Fast ? 0.34 : 0.6;
    private int DropMs => (int)(DropSeconds * 1000);   // pace the turn-pass to the drop animation
    public bool CpuTurn => Mode == GameMode.OnePlayer && Current == 1;

    private CancellationTokenSource? _idleCts;
    private static readonly Random Rng = new();

    // ---- navigation ----
    public void GoSplash() { CancelIdle(); Screen = AppScreen.Splash; Notify(); }
    public void GoMode() { CancelIdle(); Screen = AppScreen.Mode; Notify(); }
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
    public void SetPlayer(int index, PlayerConfig config) { Players[index] = config; Notify(); }

    // ---- game lifecycle ----
    // Note: only BeginGame raises GameStarted (the "get ready" audio cue). Rematch/ResetBoard
    // intentionally stay quieter — they don't replay the intro voice line.
    public void BeginGame()
    {
        ResetState($"¡Comienza el duelo! Turno de {Players[0].Name}", resetScores: true);
        Screen = AppScreen.Game;
        Notify();
        GameStarted?.Invoke();
        ArmIdle();
    }

    public void Rematch()
    {
        ResetState($"¡Revancha! Turno de {Players[0].Name}", resetScores: false);
        Screen = AppScreen.Game;
        Notify();
        ArmIdle();
    }

    public void ResetBoard()
    {
        ResetState($"Tablero reiniciado. Turno de {Players[0].Name}", resetScores: false);
        Notify();
        ArmIdle();
    }

    public void ChangePlayers() { CancelIdle(); Winner = null; Screen = AppScreen.Setup; Notify(); }

    private void ResetState(string narrator, bool resetScores)
    {
        CancelIdle();
        Board = new GameBoard();
        Current = 0;
        Winner = null; WinBy = "";
        WinningCells = new();
        LastDrop = null; ErrorCol = -1;
        IsThinking = false; IsBusy = false; IsIdle = false;
        Confetti = new();
        if (resetScores)
        {
            Scores = new[] { 0, 0 };                 // preserved across rematch/reset
            StreakHolder = -1;                        // streak + losses reset only on a brand-new session
            WinStreak = 0;
            PlayerLossesAgainstCpu = 0;
        }
        BrokenStreakLength = 0;                        // transient: only meaningful inside a MatchEnded handler
        Narrator = narrator;
        RoundStarted?.Invoke();
    }

    public void Resign()
    {
        if (Winner != null) return;
        CancelIdle();
        int loser = Current;
        int w = loser == 0 ? 1 : 0;
        Scores[w]++;
        Winner = w; WinBy = "resign"; WinningCells = new();
        IsBusy = true; IsIdle = false; IsThinking = false;
        Narrator = $"🏳️ {Players[loser].Name} se rindió. ¡Gana {Players[w].Name}!";
        Notify();
        Won?.Invoke(w);
        RecordMatchEnd(w);
        _ = TransitionToVictory();
    }

    // ---- move flow ----
    /// <summary>Entry point for a column trigger (click, keyboard, or sensor). Honors turn/busy guards.</summary>
    public async Task TryDrop(int col)
    {
        if (Winner != null || IsBusy) return;
        if (CpuTurn) return;                       // ignore human input during CPU turn
        CancelIdle();
        await Place(col);
    }

    private async Task Place(int col)
    {
        int r = Board.LowestRow(col);
        if (r < 0)
        {
            ErrorCol = col;
            Narrator = "¡Columna llena! Prueba otra. 🚫";
            Notify();
            ColumnFull?.Invoke();
            await Task.Delay(700);
            ErrorCol = -1;
            Notify();
            return;
        }

        IsBusy = true;
        var cell = CellExtensions.ForPlayer(Current);
        Board.Drop(col, cell);
        LastDrop = new BoardPosition(col, r);
        var line = WinDetector.FindWinningLine(Board, col, r, cell);
        string name = Players[Current].Name;
        ChipDropped?.Invoke();
        Notify();

        if (line != null)
        {
            WinningCells = line.ToHashSet();
            Winner = Current; WinBy = "connect";
            Scores[Current]++;
            IsIdle = false;
            Narrator = $"¡CONECTA 4! ¡Gana {name}! 🎉";
            Notify();
            Won?.Invoke(Current);
            RecordMatchEnd(Current);
            await TransitionToVictory();
            return;
        }

        if (Board.IsBoardFull())
        {
            Narrator = "¡Tablero lleno! Es un empate. 🤝";
            Notify();
            Drew?.Invoke();
            RecordMatchEnd(null);
            await Task.Delay(850);
            Screen = AppScreen.Draw;
            IsBusy = false;
            Notify();
            return;
        }

        // Let the chip fall and settle before handing over the turn — keeps the pace fluid
        // (IsBusy stays true, so no new move/keyboard/sensor input lands mid-fall).
        await Task.Delay(DropMs);
        if (Winner != null || Screen != AppScreen.Game) { IsBusy = false; return; }

        int moverIndex = Current;                     // who just dropped — capture BEFORE the flip
        Current = Current == 0 ? 1 : 0;
        IsIdle = false;
        Narrator = TurnPhrase(Current);
        Notify();
        TurnChanged?.Invoke(Current);
        if (ThreatScanner.HasImmediateThreat(Board, CellExtensions.ForPlayer(moverIndex)))
            ThreatRaised?.Invoke(moverIndex);

        if (CpuTurn)
        {
            IsThinking = true; Notify();
            await Task.Delay(750);
            // Bail if the player reset/resigned/navigated during the think delay, so this
            // stale continuation can't drop a CPU chip onto a fresh or abandoned board.
            if (Winner != null || Screen != AppScreen.Game)
            {
                IsThinking = false; IsBusy = false; Notify();
                return;
            }
            int cpuCol = CpuStrategy.ChooseColumn(Board, CpuLevel);
            IsThinking = false; IsBusy = false;
            if (cpuCol >= 0) await Place(cpuCol);
        }
        else
        {
            IsBusy = false; Notify();
            ArmIdle();
        }
    }

    private async Task TransitionToVictory()
    {
        await Task.Delay(700);
        Confetti = MakeConfetti();
        Notify();
        await Task.Delay(2000);
        Screen = AppScreen.Victory;
        IsBusy = false;
        Notify();
    }

    // Advances the win-streak (BOTH modes) and announces the end of the match. Must run BEFORE any audio reads
    // the streak state, so callers invoke it right where the win/draw is finalized.
    private void RecordMatchEnd(int? winner)
    {
        var s = CpuTauntPolicy.AdvanceStreak(StreakHolder, WinStreak, winner);
        StreakHolder = s.Holder;
        WinStreak = s.Streak;
        BrokenStreakLength = s.BrokenLength;
        if (Mode == GameMode.OnePlayer && winner is int w && Players[w].IsCpu)
            PlayerLossesAgainstCpu++;
        MatchEnded?.Invoke(winner, Mode);
    }

    private string TurnPhrase(int p)
    {
        string name = Players[p].Name;
        string opp = Players[p == 0 ? 1 : 0].Name;
        if (ThreatScanner.HasImmediateThreat(Board, CellExtensions.ForPlayer(p == 0 ? 1 : 0)))
            return $"¡Cuidado {name}, hay tres en línea! 😱";
        string[] phrases =
        {
            $"Turno de {name}",
            $"¡Buena jugada! Ahora va {name}",
            $"Vamos {name}, tú puedes 🏀",
            $"Cuidado {name}, {opp} va fuerte",
        };
        return phrases[Rng.Next(phrases.Length)];
    }

    // ---- idle nudge ----
    private void ArmIdle()
    {
        CancelIdle();
        if (Screen != AppScreen.Game || Winner != null || CpuTurn) return;
        _idleCts = new CancellationTokenSource();
        var token = _idleCts.Token;
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(9000, token); } catch { return; }
            if (token.IsCancellationRequested) return;
            if (Screen != AppScreen.Game || Winner != null || CpuTurn || IsBusy) return;
            IsIdle = true;
            Narrator = $"¿Sigues ahí, {Players[Current].Name}? ¡Es tu turno! 🏀";
            Notify();
            IdleNudged?.Invoke();
        });
    }

    private void CancelIdle()
    {
        _idleCts?.Cancel();
        _idleCts = null;
        if (IsIdle) IsIdle = false;
    }

    private static List<ConfettiPiece> MakeConfetti()
    {
        string[] cols = { "#ff2d6f", "#22d3ee", "#ffd23f", "#2ee86e", "#b14bff", "#ff8a00", "#ffffff" };
        var list = new List<ConfettiPiece>(70);
        for (int i = 0; i < 70; i++)
        {
            list.Add(new ConfettiPiece(
                Left: $"{Rng.Next(0, 1000) / 10.0:0.0}%",
                Size: $"{7 + Rng.Next(0, 12)}px",
                Color: cols[i % cols.Length],
                Radius: Rng.NextDouble() > 0.5 ? "50%" : "2px",
                Duration: $"{(2 + Rng.NextDouble() * 2.4):0.00}s",
                Delay: $"{(Rng.NextDouble() * 1.5):0.00}s"));
        }
        return list;
    }
}

public sealed record ConfettiPiece(string Left, string Size, string Color, string Radius, string Duration, string Delay);
