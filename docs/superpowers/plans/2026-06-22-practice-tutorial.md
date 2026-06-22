# Practice / tutorial mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a practice sandbox vs the CPU — entered from the mode screen — with full-turn undo/redo to the empty board, a live CPU-level selector, soft win (no scores), a one-time intro card, and an optional hints toggle. Undo is practice-only; the normal game is untouched.

**Architecture:** Pure `Core` helpers carry the risky logic and are TDD'd: `BoardReplay.FromColumns` (rebuild a board from a column list), `MoveLog` (play/undo-turn/redo bookkeeping), and `ThreatScanner.FindWinningColumn` (hints). `GameSession` gains a `Practice` flag + a `MoveLog`; it records every drop, and on undo/redo rebuilds the board from the log and recomputes win/turn. Practice runs on `AppScreen.Game` with `Practice==true`; `AppShell` routes that to a new responsive `PracticeView`. Everything else (BoardGrid, MoveRouter, CpuStrategy) is reused.

**Tech Stack:** .NET 10, C#, xUnit (Core tests), Blazor WASM (Razor components), CSS.

**Testing note:** Per `CLAUDE.md`, `Core` is TDD'd (write failing test first); `Web`/UI is build-and-verify. Tasks 1–3 are TDD in `Core`; the rest build-and-verify with the 51 (now more) Core tests staying green.

**Spec:** `docs/superpowers/specs/2026-06-22-practice-tutorial-design.md`

**Reused Core APIs (verified):** `GameBoard.Drop(col, cell)→row`, `LowestRow(col)`, `this[col,row]`, `Columns=7`, `Rows=6`; `Cell { Empty, Player1, Player2 }`, `CellExtensions.ForPlayer(i)`; `WinDetector.FindWinningLine(board, col, row, cell)`; `ThreatScanner.HasImmediateThreat(board, cell)`. xUnit tests live in `tests/Connect4HoopsArcade.Core.Tests/`, namespace `Connect4HoopsArcade.Core.Tests`, `[Fact]`.

---

## File structure
- Create: `src/Connect4HoopsArcade.Core/Practice/BoardReplay.cs` — pure: board from a column list.
- Create: `src/Connect4HoopsArcade.Core/Practice/MoveLog.cs` — pure: played/undone bookkeeping.
- Modify: `src/Connect4HoopsArcade.Core/Rules/ThreatScanner.cs` — add `FindWinningColumn`.
- Create test files under `tests/Connect4HoopsArcade.Core.Tests/`.
- Modify: `src/Connect4HoopsArcade.Web/State/GameSession.cs` — practice state, ChoosePractice, Place hooks, undo/redo/restart, hint columns.
- Modify: `src/Connect4HoopsArcade.Web/Components/Screens/GameModeSelector.razor` — Práctica card.
- Modify: `src/Connect4HoopsArcade.Web/Components/Layout/AppShell.razor` — route Game→PracticeView when Practice.
- Create: `src/Connect4HoopsArcade.Web/Components/Screens/PracticeView.razor` — board + control bar + banners.
- Modify: `src/Connect4HoopsArcade.Web/Components/Game/GameColumn.razor` — hint highlight (reads Session).
- Modify: `src/Connect4HoopsArcade.Web/Models/GameSettings.cs` + `SettingsStore` — `PracticeIntroSeen` flag.
- Modify: `src/Connect4HoopsArcade.Web/wwwroot/css/mobile.css` (or app.css) — practice styles.

---

## Task 1: Core — `BoardReplay.FromColumns`

**Files:**
- Create: `src/Connect4HoopsArcade.Core/Practice/BoardReplay.cs`
- Create: `tests/Connect4HoopsArcade.Core.Tests/BoardReplayTests.cs`

- [ ] **Step 1: Write the failing test**

`tests/Connect4HoopsArcade.Core.Tests/BoardReplayTests.cs`:

```csharp
using Connect4HoopsArcade.Core.Board;
using Connect4HoopsArcade.Core.Practice;
using Connect4HoopsArcade.Core.Primitives;
using Xunit;

namespace Connect4HoopsArcade.Core.Tests;

public class BoardReplayTests
{
    [Fact]
    public void Alternates_players_starting_from_starter()
    {
        var b = BoardReplay.FromColumns(new[] { 3, 3, 4 }, Cell.Player1);
        Assert.Equal(Cell.Player1, b[3, 0]); // move 0 (starter)
        Assert.Equal(Cell.Player2, b[3, 1]); // move 1 (other)
        Assert.Equal(Cell.Player1, b[4, 0]); // move 2 (starter)
    }

    [Fact]
    public void Empty_list_gives_empty_board()
    {
        var b = BoardReplay.FromColumns(System.Array.Empty<int>(), Cell.Player1);
        Assert.Equal(Cell.Empty, b[0, 0]);
    }
}
```

- [ ] **Step 2: Run it — expect FAIL (BoardReplay does not exist)**

Run: `dotnet test --filter BoardReplayTests`
Expected: build/compile error or FAIL — `BoardReplay` not found.

- [ ] **Step 3: Implement**

`src/Connect4HoopsArcade.Core/Practice/BoardReplay.cs`:

```csharp
using Connect4HoopsArcade.Core.Board;
using Connect4HoopsArcade.Core.Primitives;

namespace Connect4HoopsArcade.Core.Practice;

/// <summary>Rebuilds a board from a sequence of dropped columns, alternating players each move
/// starting from <paramref name="starter"/>. Used by practice undo/redo (board is recreated from the log).</summary>
public static class BoardReplay
{
    public static GameBoard FromColumns(IReadOnlyList<int> columns, Cell starter)
    {
        var other = starter == Cell.Player1 ? Cell.Player2 : Cell.Player1;
        var board = new GameBoard();
        for (int i = 0; i < columns.Count; i++)
            board.Drop(columns[i], i % 2 == 0 ? starter : other);
        return board;
    }
}
```

- [ ] **Step 4: Run it — expect PASS**

Run: `dotnet test --filter BoardReplayTests`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Connect4HoopsArcade.Core/Practice/BoardReplay.cs tests/Connect4HoopsArcade.Core.Tests/BoardReplayTests.cs
git commit -m "feat(core): BoardReplay.FromColumns for practice undo/redo"
```

---

## Task 2: Core — `ThreatScanner.FindWinningColumn` (for hints)

**Files:**
- Modify: `src/Connect4HoopsArcade.Core/Rules/ThreatScanner.cs`
- Create: `tests/Connect4HoopsArcade.Core.Tests/FindWinningColumnTests.cs`

- [ ] **Step 1: Write the failing test**

`tests/Connect4HoopsArcade.Core.Tests/FindWinningColumnTests.cs`:

```csharp
using Connect4HoopsArcade.Core.Board;
using Connect4HoopsArcade.Core.Primitives;
using Connect4HoopsArcade.Core.Rules;
using Xunit;

namespace Connect4HoopsArcade.Core.Tests;

public class FindWinningColumnTests
{
    [Fact]
    public void Returns_the_column_that_completes_four()
    {
        var b = new GameBoard();
        b.Drop(0, Cell.Player1); b.Drop(1, Cell.Player1); b.Drop(2, Cell.Player1); // 3 in a row
        Assert.Equal(3, ThreatScanner.FindWinningColumn(b, Cell.Player1)); // col 3 wins
    }

    [Fact]
    public void Returns_minus_one_when_no_immediate_win()
    {
        var b = new GameBoard();
        b.Drop(0, Cell.Player1);
        Assert.Equal(-1, ThreatScanner.FindWinningColumn(b, Cell.Player1));
    }
}
```

- [ ] **Step 2: Run it — expect FAIL**

Run: `dotnet test --filter FindWinningColumnTests`
Expected: FAIL — `FindWinningColumn` not found.

- [ ] **Step 3: Implement** — append to `ThreatScanner` (keep `HasImmediateThreat`):

```csharp
    /// <summary>The column where <paramref name="cell"/> wins immediately, or -1 if none.</summary>
    public static int FindWinningColumn(GameBoard board, Cell cell)
    {
        for (int c = 0; c < GameBoard.Columns; c++)
        {
            int r = board.LowestRow(c);
            if (r < 0) continue;
            var trial = board.Clone();
            trial.Drop(c, cell);
            if (WinDetector.FindWinningLine(trial, c, r, cell) is not null) return c;
        }
        return -1;
    }
```

- [ ] **Step 4: Run it — expect PASS**

Run: `dotnet test --filter FindWinningColumnTests`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Connect4HoopsArcade.Core/Rules/ThreatScanner.cs tests/Connect4HoopsArcade.Core.Tests/FindWinningColumnTests.cs
git commit -m "feat(core): ThreatScanner.FindWinningColumn (practice hints)"
```

---

## Task 3: Core — `MoveLog` (play / undo-turn / redo)

**Files:**
- Create: `src/Connect4HoopsArcade.Core/Practice/MoveLog.cs`
- Create: `tests/Connect4HoopsArcade.Core.Tests/MoveLogTests.cs`

Player convention: index 0 (you) plays even positions, index 1 (CPU) plays odd. "Undo a turn" removes the
trailing CPU move (odd) plus the human move before it; if the last move was the human's (no CPU reply yet),
removes just that one. So after any undo it is always the human's turn (even count).

- [ ] **Step 1: Write the failing tests**

`tests/Connect4HoopsArcade.Core.Tests/MoveLogTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run — expect FAIL**

Run: `dotnet test --filter MoveLogTests`
Expected: FAIL — `MoveLog` not found.

- [ ] **Step 3: Implement**

`src/Connect4HoopsArcade.Core/Practice/MoveLog.cs`:

```csharp
namespace Connect4HoopsArcade.Core.Practice;

/// <summary>Undo/redo bookkeeping for practice mode. "Undo a turn" pops the trailing CPU move (odd index)
/// plus the human move before it; a new Play() clears the redo stack. Pure + deterministic (stores the
/// exact columns, so a redone CPU move is reproduced rather than recomputed).</summary>
public sealed class MoveLog
{
    private readonly List<int> _played = new();
    private readonly List<int> _undone = new();   // in play order; redo consumes from the front

    public IReadOnlyList<int> Played => _played;
    public bool CanUndo => _played.Count > 0;
    public bool CanRedo => _undone.Count > 0;

    public void Play(int col) { _played.Add(col); _undone.Clear(); }

    public void UndoTurn()
    {
        if (_played.Count == 0) return;
        int lastIdx = _played.Count - 1;
        bool lastWasCpu = lastIdx % 2 == 1;
        var popped = new List<int> { _played[lastIdx] };
        _played.RemoveAt(lastIdx);
        if (lastWasCpu && _played.Count > 0)
        {
            popped.Add(_played[^1]);
            _played.RemoveAt(_played.Count - 1);
        }
        popped.Reverse();                 // back to play order: [human, (cpu)]
        _undone.InsertRange(0, popped);
    }

    public void Redo()
    {
        if (_undone.Count == 0) return;
        _played.Add(_undone[0]); _undone.RemoveAt(0);          // human move
        if (_undone.Count > 0) { _played.Add(_undone[0]); _undone.RemoveAt(0); } // its CPU reply
    }

    public void Clear() { _played.Clear(); _undone.Clear(); }
}
```

- [ ] **Step 4: Run — expect PASS**

Run: `dotnet test --filter MoveLogTests`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Connect4HoopsArcade.Core/Practice/MoveLog.cs tests/Connect4HoopsArcade.Core.Tests/MoveLogTests.cs
git commit -m "feat(core): MoveLog (practice undo-turn/redo bookkeeping)"
```

---

## Task 4: GameSession — practice state, entry, undo/redo, soft win, hints

**Files:**
- Modify: `src/Connect4HoopsArcade.Web/State/GameSession.cs`

This is the integration task. Practice reuses 1P flow; the differences are: record each drop in a `MoveLog`,
soft win (no scores/Victory), and undo/redo/restart that rebuild the board from the log.

- [ ] **Step 1: Add usings + practice fields/properties**

At the top of `GameSession.cs`, ensure these usings exist (add if missing):
`using Connect4HoopsArcade.Core.Practice;` and `using Connect4HoopsArcade.Core.Rules;` (Rules likely already present).

Add fields/properties (near the other state, e.g. after `private CancellationTokenSource? _idleCts;`):

```csharp
    // ---- practice / tutorial ----
    public bool Practice { get; private set; }
    public bool PracticeHints { get; private set; }
    private readonly MoveLog _log = new();
    public bool CanUndo => Practice && !IsBusy && _log.CanUndo;
    public bool CanRedo => Practice && !IsBusy && _log.CanRedo;
    /// <summary>Columns to highlight as hints (your immediate win, and a CPU win to block). Empty unless Practice + hints on + your turn.</summary>
    public IReadOnlyCollection<int> HintColumns { get; private set; } = System.Array.Empty<int>();
```

- [ ] **Step 2: Add `ChoosePractice()` and `ToggleHints()`**

Add these methods (near `ChooseOnePlayer`):

```csharp
    public void ChoosePractice()
    {
        Practice = true;
        Mode = GameMode.OnePlayer;
        Players = new[] { Players[0] with { IsCpu = false }, PlayerConfig.DefaultCpu };
        _log.Clear();
        ResetState($"Práctica · tu turno", resetScores: true);   // you = P1, you start
        Screen = AppScreen.Game;
        RecomputeHints();
        Notify();
        StartTurnFlow();
    }

    public void ToggleHints() { PracticeHints = !PracticeHints; RecomputeHints(); Notify(); }
```

Note: `ResetState` already sets `Current = CpuStarts ? 1 : 0`; in practice we want you to start. `ChoosePractice`
does not set `CpuStarts`, and practice ignores it — to be safe, also set `Current = 0` right after `ResetState`
in `ChoosePractice` (add `Current = 0;` before `Screen = AppScreen.Game;`).

- [ ] **Step 3: Record drops + soft win in `Place`**

In `Place(int col)`, immediately after the successful drop line `Board.Drop(col, cell);` and `LastDrop = ...`,
add history recording:

```csharp
        if (Practice) _log.Play(col);
```

Then make the win and draw branches practice-aware. Replace the existing win block:

```csharp
        if (line != null)
        {
            WinningCells = line.ToHashSet();
            Winner = Current; WinBy = "connect";
            if (Practice)
            {
                Narrator = "¡Conecta 4! Deshaz para seguir probando. 🏀";
                IsBusy = false; RecomputeHints(); Notify();
                Won?.Invoke(Current);
                return;
            }
            Scores[Current]++;
            IsIdle = false;
            Narrator = $"¡CONECTA 4! ¡Gana {name}! 🎉";
            Notify();
            Won?.Invoke(Current);
            RecordMatchEnd(Current);
            await TransitionToVictory();
            return;
        }
```

And the board-full (draw) block — add a practice branch at its top:

```csharp
        if (Board.IsBoardFull())
        {
            if (Practice)
            {
                Narrator = "¡Tablero lleno! Deshaz o reinicia. 🤝";
                IsBusy = false; RecomputeHints(); Notify();
                return;
            }
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
```

Leave the rest of `Place` (turn flip + `RunCpuTurn` for the next turn) unchanged — in practice it still hands
the turn to the CPU, which replies via `RunCpuTurn`→`Place` (recording the CPU column too). After the CPU
replies and the turn returns to you, add a hint refresh: in the `else` branch where `ArmIdle()` is called
(human's turn resumes), add `RecomputeHints();` right before `Notify();`. Also after the CPU's `RunCpuTurn`
completes the board is yours again — `RecomputeHints` there covers it.

- [ ] **Step 4: Add undo/redo/restart + rebuild + hints helper**

Add these methods to `GameSession`:

```csharp
    public void UndoTurn()
    {
        if (!CanUndo) return;
        CancelIdle();
        _log.UndoTurn();
        RebuildPracticeBoard();
    }

    public void Redo()
    {
        if (!CanRedo) return;
        CancelIdle();
        _log.Redo();
        RebuildPracticeBoard();
    }

    public void RestartPractice()
    {
        if (!Practice) return;
        CancelIdle();
        _log.Clear();
        ResetState("Práctica · tu turno", resetScores: true);
        Current = 0;
        RecomputeHints();
        Notify();
    }

    // Recreate the board from the move log and recompute winner/turn (used after undo/redo).
    private void RebuildPracticeBoard()
    {
        Board = BoardReplay.FromColumns(_log.Played, Cell.Player1);
        Winner = null; WinBy = ""; WinningCells = new(); LastDrop = null;
        IsBusy = false; IsThinking = false; IsIdle = false;
        int n = _log.Played.Count;
        if (n > 0)
        {
            int lastCol = _log.Played[n - 1];
            int lastRow = Board.LowestRow(lastCol) - 1;          // top filled cell of that column
            Cell lastCell = CellExtensions.ForPlayer((n - 1) % 2 == 0 ? 0 : 1);
            LastDrop = new BoardPosition(lastCol, lastRow);
            var line = WinDetector.FindWinningLine(Board, lastCol, lastRow, lastCell);
            if (line != null) { Winner = (n - 1) % 2 == 0 ? 0 : 1; WinBy = "connect"; WinningCells = line.ToHashSet(); }
        }
        Current = n % 2 == 0 ? 0 : 1;                            // even count → your turn
        Narrator = Winner != null ? "¡Conecta 4! Deshaz para seguir probando. 🏀" : "Práctica · tu turno";
        RecomputeHints();
        Notify();
    }

    private void RecomputeHints()
    {
        if (!Practice || !PracticeHints || Winner != null || Current != 0)
        {
            HintColumns = System.Array.Empty<int>();
            return;
        }
        var cols = new List<int>();
        int win = ThreatScanner.FindWinningColumn(Board, Cell.Player1);   // you can win here
        if (win >= 0) cols.Add(win);
        int block = ThreatScanner.FindWinningColumn(Board, Cell.Player2); // CPU would win here → block
        if (block >= 0 && block != win) cols.Add(block);
        HintColumns = cols;
    }
```

- [ ] **Step 5: Clear practice on navigation**

In `GoSplash`, `GoMode`, and `ChangePlayers`, set `Practice = false;` (and `PracticeHints = false; _log.Clear();`)
so leaving practice resets it. Example for `GoSplash`:

```csharp
    public void GoSplash() { CancelIdle(); AudioStopRequested?.Invoke(); Practice = false; PracticeHints = false; _log.Clear(); HintColumns = System.Array.Empty<int>(); Screen = AppScreen.Splash; Notify(); }
```

Apply the same `Practice = false; PracticeHints = false; _log.Clear(); HintColumns = System.Array.Empty<int>();`
insertion to `GoMode` and `ChangePlayers`.

- [ ] **Step 6: Build**

Run: `dotnet build src/Connect4HoopsArcade.Web -c Debug -v q`
Expected: `Compilación correcta.` / 0 Errores.

- [ ] **Step 7: Commit**

```bash
git add src/Connect4HoopsArcade.Web/State/GameSession.cs
git commit -m "feat(web): GameSession practice mode (history, undo/redo, soft win, hints)"
```

---

## Task 5: Mode-select — add the Práctica card

**Files:**
- Modify: `src/Connect4HoopsArcade.Web/Components/Screens/GameModeSelector.razor`

- [ ] **Step 1: Add the card** after the 2-player card's closing `</button>` (before the `<div>` with the sensors/config pills):

```razor
    <button @onclick="Session.ChoosePractice" class="mode-card mode-card--cyan">
      <div style="width:96px;height:96px;margin:0 auto 16px;">
        <AvatarSvg ColorId="green" Face="FaceId.Happy" Accessory="AccessoryId.Glasses" />
      </div>
      <div class="font-display" style="font-weight:700;font-size:34px;">🎓 PRÁCTICA</div>
      <div style="font-size:16px;font-weight:700;color:rgba(255,255,255,.6);margin-top:4px;">Experimenta · deshaz jugadas</div>
    </button>
```

- [ ] **Step 2: Build**

Run: `dotnet build src/Connect4HoopsArcade.Web -c Debug -v q`
Expected: 0 Errores. (If `ColorId="green"` isn't a valid color id, use `"cyan"`.)

- [ ] **Step 3: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Components/Screens/GameModeSelector.razor
git commit -m "feat(web): Práctica card on the mode-select screen"
```

---

## Task 6: Settings flag for the one-time intro card

**Files:**
- Modify: `src/Connect4HoopsArcade.Web/Models/GameSettings.cs`

- [ ] **Step 1: Add the property** to `GameSettings` (a bool, default false):

```csharp
    public bool PracticeIntroSeen { get; set; }
```

- [ ] **Step 2: Build**

Run: `dotnet build src/Connect4HoopsArcade.Web -c Debug -v q`
Expected: 0 Errores. (GameSettings is persisted by `SettingsStore`; a new auto-property serializes with the rest — no other change needed unless SettingsStore uses an explicit DTO; if it does, add the field there too.)

- [ ] **Step 3: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Models/GameSettings.cs
git commit -m "feat(web): persist PracticeIntroSeen flag"
```

---

## Task 7: `PracticeView` component (board + controls + banners + intro)

**Files:**
- Create: `src/Connect4HoopsArcade.Web/Components/Screens/PracticeView.razor`

- [ ] **Step 1: Create the component**

```razor
@using Connect4HoopsArcade.Web.Components.Game
@using Connect4HoopsArcade.Web.Components.Setup
@inherits SessionComponentBase
@inject Connect4HoopsArcade.Web.Services.Abstractions.ISettingsStore Store

<div class="practice-view">
  <div class="practice-bar">
    <button class="practice-btn" @onclick="Session.UndoTurn" disabled="@(!Session.CanUndo)">↶ Deshacer</button>
    <button class="practice-btn" @onclick="Session.Redo" disabled="@(!Session.CanRedo)">↷ Rehacer</button>
    <button class="practice-btn" @onclick="Session.RestartPractice">🔄 Reiniciar</button>
    <div class="practice-level"><span>🤖</span><CpuLevelSelector Level="Store.Current.CpuLevel" OnChange="SetLevel" /></div>
    <button class="practice-btn @(Session.PracticeHints ? "on" : "")" @onclick="Session.ToggleHints">💡 Pistas</button>
    <button class="practice-btn" @onclick="ShowIntro">❔</button>
    <button class="practice-btn" @onclick="Session.GoSplash">🏠 Inicio</button>
  </div>

  <div class="practice-board"><BoardGrid InteractionEnabled="true" FitContainer="true" /></div>

  <div class="practice-status">@Session.Narrator</div>

  @if (_showIntro)
  {
    <div class="practice-scrim" @onclick="DismissIntro"></div>
    <div class="practice-intro" @onclick:stopPropagation="true">
      <div class="font-display" style="font-size:22px;color:#ffd23f;">🎓 Modo práctica</div>
      <ul style="text-align:left;line-height:1.5;margin:10px 0;font-weight:700;">
        <li>Toca una columna para soltar tu ficha.</li>
        <li>Conecta 4 (horizontal, vertical o diagonal) para ganar.</li>
        <li>Usa <b>↶ Deshacer</b> para probar otra jugada y ver cómo responde la CPU.</li>
      </ul>
      <button class="practice-btn" @onclick="DismissIntro">¡Entendido!</button>
    </div>
  }
</div>

@code {
    private bool _showIntro;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        if (!Store.Current.PracticeIntroSeen) _showIntro = true;
    }

    private void ShowIntro() { _showIntro = true; StateHasChanged(); }
    private async Task DismissIntro()
    {
        _showIntro = false;
        if (!Store.Current.PracticeIntroSeen) { Store.Current.PracticeIntroSeen = true; await Store.SaveAsync(); }
        StateHasChanged();
    }

    private async Task SetLevel(Connect4HoopsArcade.Core.Primitives.CpuDifficulty lvl)
    {
        Store.Current.CpuLevel = lvl;
        await Store.ApplyAsync();   // pushes CpuLevel into GameSession (same path the setup screen uses)
    }
}
```

Note: confirm `ISettingsStore` has `ApplyAsync()` (the setup screen uses it to push `CpuLevel` into
`GameSession`). If it's named differently, use that method; if only `SaveAsync` exists, also set
`Session.CpuLevel = lvl;` directly before saving.

- [ ] **Step 2: Build**

Run: `dotnet build src/Connect4HoopsArcade.Web -c Debug -v q`
Expected: 0 Errores.

- [ ] **Step 3: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Components/Screens/PracticeView.razor
git commit -m "feat(web): PracticeView (board + undo/redo/level/hints/intro)"
```

---

## Task 8: Route practice in AppShell

**Files:**
- Modify: `src/Connect4HoopsArcade.Web/Components/Layout/AppShell.razor`

- [ ] **Step 1: Branch the Game case** — replace the `case AppScreen.Game:` block:

```razor
    case AppScreen.Game:
      @if (Session.Practice) { <Connect4HoopsArcade.Web.Components.Screens.PracticeView /> }
      else if (Viewport.IsMobile) { <MobileGameView /> } else { <DesktopGameView /> }
      break;
```

- [ ] **Step 2: Build**

Run: `dotnet build src/Connect4HoopsArcade.Web -c Debug -v q`
Expected: 0 Errores.

- [ ] **Step 3: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Components/Layout/AppShell.razor
git commit -m "feat(web): route Game screen to PracticeView when Practice"
```

---

## Task 9: Hint highlight on the board

**Files:**
- Modify: `src/Connect4HoopsArcade.Web/Components/Game/GameColumn.razor`

- [ ] **Step 1: Add a hint class** to the column when its index is in `Session.HintColumns`. Open
`GameColumn.razor`, find the outer element of the column, and add `@(Session.HintColumns.Contains(Col) ? " practice-hint" : "")`
to its `class` attribute. If the outer element uses an inline `style` and no class, add a class attribute:
`class="@(Session.HintColumns.Contains(Col) ? "practice-hint" : null)"`. `GameColumn` already inherits
`SessionComponentBase` (has `Session`); `Col` is its existing column-index parameter.

- [ ] **Step 2: Build**

Run: `dotnet build src/Connect4HoopsArcade.Web -c Debug -v q`
Expected: 0 Errores.

- [ ] **Step 3: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Components/Game/GameColumn.razor
git commit -m "feat(web): highlight hint columns in practice"
```

---

## Task 10: Practice CSS

**Files:**
- Modify: `src/Connect4HoopsArcade.Web/wwwroot/css/mobile.css`

- [ ] **Step 1: Append styles** (responsive: bar wraps on phones; board fills; works portrait/landscape/desktop):

```css
/* ---- Practice / tutorial mode ---- */
.practice-view {
  position: absolute; inset: 0; z-index: 5; display: flex; flex-direction: column;
  padding: calc(env(safe-area-inset-top) + 8px) calc(env(safe-area-inset-right) + 10px)
           calc(env(safe-area-inset-bottom) + 8px) calc(env(safe-area-inset-left) + 10px);
  gap: 8px; min-height: 0;
}
.practice-bar { flex: none; display: flex; flex-wrap: wrap; gap: 8px; align-items: center; justify-content: center; }
.practice-btn {
  cursor: pointer; font-weight: 800; font-size: 14px; color: #fff; padding: 9px 14px; border-radius: 11px;
  background: rgba(255,255,255,.07); border: 1.5px solid rgba(255,255,255,.14);
}
.practice-btn:disabled { opacity: .35; }
.practice-btn.on { background: rgba(255,210,63,.18); border-color: #ffd23f; color: #ffd23f; }
.practice-level { display: flex; align-items: center; gap: 6px; }
.practice-board { flex: 1; min-height: 0; display: flex; align-items: center; justify-content: center; }
.practice-board > * { width: min(100%, calc((100dvh - 220px) * 7 / 6)); max-width: 100%; aspect-ratio: 7 / 6; max-height: 100%; }
.practice-status { flex: none; text-align: center; font-weight: 800; color: #fff; min-height: 22px; }
.practice-hint { outline: 3px dashed #ffd23f; outline-offset: -3px; border-radius: 10px; }
.practice-scrim { position: absolute; inset: 0; z-index: 40; background: rgba(4,3,10,.55); }
.practice-intro {
  position: absolute; z-index: 41; left: 50%; top: 50%; transform: translate(-50%,-50%);
  width: min(90vw, 380px); background: #161031; border: 1.5px solid rgba(255,255,255,.2);
  border-radius: 16px; padding: 18px; text-align: center; box-shadow: 0 12px 30px rgba(0,0,0,.6);
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/Connect4HoopsArcade.Web -c Debug -v q`
Expected: 0 Errores.

- [ ] **Step 3: Commit**

```bash
git add src/Connect4HoopsArcade.Web/wwwroot/css/mobile.css
git commit -m "feat(web): practice mode styles"
```

---

## Task 11: Verify + deploy

- [ ] **Step 1: Full build + tests**

Run: `dotnet build -v q` then `dotnet test -v q`
Expected: build OK; tests `Superado: 59` (51 existing + 8 new from Tasks 1–3).

- [ ] **Step 2: Deploy + on-device verify**

Push to `main`; once live: Mode → 🎓 Práctica → intro card shows once → drop a chip → CPU replies →
↶ Deshacer returns to your turn with the CPU reply gone → try another column → different CPU response →
↷ Rehacer re-applies exactly → 🔄 Reiniciar clears → change CPU level and see different play → 💡 Pistas
highlights a winning/blocking column → connect 4 shows the soft banner and Undo still works → 🏠 Inicio exits
(and a normal 1P game has NO undo/redo). Check portrait, landscape, desktop.

- [ ] **Step 3: Commit any verify tweak**

```bash
git add -A && git commit -m "fix(web): practice polish from device verification"
```

---

## Notes / out of scope
- Keyboard shortcuts for undo/redo (desktop) were deferred from the spec to keep this plan focused; add later if wanted.
- Scripted lessons / AI move explanations / puzzles are future layers on top of this sandbox.
