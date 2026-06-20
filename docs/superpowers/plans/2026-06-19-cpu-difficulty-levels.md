# CPU Difficulty Levels + Setup Selector — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a 6-level CPU difficulty ladder (Novato→MVP) selectable on the setup screen (1P only), persisted, defaulting to Amateur.

**Architecture:** Core gets a 6-value `CpuDifficulty` enum; `CpuStrategy` is parameterized by search depth (0 = Novato/loose, 1-5 = minimax depth) — the minimax/eval logic itself is unchanged. The level is a persisted `GameSettings` value pushed to `GameSession.CpuLevel` via `SettingsStore.ApplyAsync` (like `NarratorTone`). A shared `CpuLevelSelector` stepper is mounted on the desktop CPU card (at the name slot) and in a mobile yellow box above PLAY.

**Tech Stack:** .NET 10 Blazor WASM, xUnit. Spec: [`docs/superpowers/specs/2026-06-19-cpu-difficulty-levels-design.md`](../specs/2026-06-19-cpu-difficulty-levels-design.md).

---

## File Structure

**Modify:**
- `src/Connect4HoopsArcade.Core/Primitives/CpuDifficulty.cs` — enum → 6 values.
- `src/Connect4HoopsArcade.Core/Ai/CpuStrategy.cs` — depth-per-level via `DepthFor`.
- `tests/Connect4HoopsArcade.Core.Tests/CpuStrategyTests.cs` — new enum + curve tests.
- `src/Connect4HoopsArcade.Web/State/GameSession.cs` — default `CpuLevel` → `Amateur`.
- `src/Connect4HoopsArcade.Web/Models/GameSettings.cs` — add `CpuLevel`.
- `src/Connect4HoopsArcade.Web/Services/SettingsStore.cs` — push `CpuLevel` in `ApplyAsync`.
- `src/Connect4HoopsArcade.Web/Components/Setup/PlayerSetupCard.razor` — CPU card shows the selector at the name slot.
- `src/Connect4HoopsArcade.Web/Components/Screens/DesktopPlayerSetup.razor` — pass level + persist.
- `src/Connect4HoopsArcade.Web/Components/Screens/MobilePlayerSetup.razor` — yellow box above PLAY (1P).
- `src/Connect4HoopsArcade.Web/wwwroot/css/board.css` — selector styles.
- `src/Connect4HoopsArcade.Web/wwwroot/css/mobile.css` — mobile yellow box.
- `CLAUDE.md` — vault update.

**Create:**
- `src/Connect4HoopsArcade.Web/Services/CpuLevelLabels.cs` — enum → Spanish name.
- `src/Connect4HoopsArcade.Web/Components/Setup/CpuLevelSelector.razor` — shared stepper.

**Verification:** Core TDD (CpuStrategyTests). UI by build + browser run (1P desktop + mobile). No 2P impact.

---

### Task 1: Core — 6-level enum + depth-parameterized strategy (TDD)

**Files:**
- Modify: `src/Connect4HoopsArcade.Core/Primitives/CpuDifficulty.cs`
- Modify: `src/Connect4HoopsArcade.Core/Ai/CpuStrategy.cs`
- Modify: `tests/Connect4HoopsArcade.Core.Tests/CpuStrategyTests.cs`
- Modify: `src/Connect4HoopsArcade.Web/State/GameSession.cs` (default value, to keep the solution compiling)

- [ ] **Step 1: Rewrite the tests (failing)** — replace the entire contents of `tests/Connect4HoopsArcade.Core.Tests/CpuStrategyTests.cs` with:

```csharp
using Connect4HoopsArcade.Core.Ai;
using Connect4HoopsArcade.Core.Board;
using Connect4HoopsArcade.Core.Primitives;
using Xunit;

namespace Connect4HoopsArcade.Core.Tests;

public class CpuStrategyTests
{
    [Fact]
    public void Takes_winning_move_when_available()
    {
        var b = new GameBoard();
        b.Drop(0, Cell.Player2);
        b.Drop(1, Cell.Player2);
        b.Drop(2, Cell.Player2); // CPU (Player2) can win at col 3
        Assert.Equal(3, CpuStrategy.ChooseColumn(b, CpuDifficulty.Amateur));
    }

    [Fact]
    public void Mvp_blocks_opponent_immediate_win()
    {
        var b = new GameBoard();
        b.Drop(0, Cell.Player1);
        b.Drop(1, Cell.Player1);
        b.Drop(2, Cell.Player1); // opponent threatens col 3
        Assert.Equal(3, CpuStrategy.ChooseColumn(b, CpuDifficulty.MVP));
    }

    [Fact]
    public void Prefers_center_on_empty_board_when_mvp()
        => Assert.Equal(3, CpuStrategy.ChooseColumn(new GameBoard(), CpuDifficulty.MVP));

    [Fact]
    public void Returns_a_playable_column()
    {
        var b = new GameBoard();
        int col = CpuStrategy.ChooseColumn(b, CpuDifficulty.Amateur);
        Assert.InRange(col, 0, 6);
        Assert.False(b.IsColumnFull(col));
    }

    [Fact]
    public void Novato_still_takes_its_own_winning_move()
    {
        var b = new GameBoard();
        b.Drop(0, Cell.Player2);
        b.Drop(1, Cell.Player2);
        b.Drop(2, Cell.Player2); // win check applies to every level
        Assert.Equal(3, CpuStrategy.ChooseColumn(b, CpuDifficulty.Novato));
    }

    [Fact]
    public void Amateur_blocks_a_vertical_threat_off_center()
    {
        // Player1 stacks three in col 6 (off-centre) → depth-2 search sees the win and must block at col 6.
        var b = new GameBoard();
        b.Drop(6, Cell.Player1);
        b.Drop(6, Cell.Player1);
        b.Drop(6, Cell.Player1);
        Assert.Equal(6, CpuStrategy.ChooseColumn(b, CpuDifficulty.Amateur)); // depth 2 sees the threat
    }

    [Fact]
    public void Mvp_blocks_a_developing_horizontal_threat()
    {
        var b = new GameBoard();
        b.Drop(1, Cell.Player2);  // blocks the left end
        b.Drop(2, Cell.Player1);
        b.Drop(3, Cell.Player1);
        b.Drop(4, Cell.Player1);
        Assert.Equal(5, CpuStrategy.ChooseColumn(b, CpuDifficulty.MVP));
    }

    [Fact]
    public void Returns_minus_one_when_board_full()
    {
        var b = new GameBoard();
        for (int c = 0; c < 7; c++)
            for (int r = 0; r < 6; r++)
                b.Drop(c, c % 2 == 0 ? Cell.Player1 : Cell.Player2);
        Assert.Equal(-1, CpuStrategy.ChooseColumn(b, CpuDifficulty.Amateur));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail to compile**

Run: `dotnet test --filter FullyQualifiedName~CpuStrategyTests`
Expected: FAIL — `CpuDifficulty` has no `Novato`/`Amateur`/`MVP`/`Principiante` members yet.

- [ ] **Step 3: Update the enum** — replace the contents of `src/Connect4HoopsArcade.Core/Primitives/CpuDifficulty.cs` with:

```csharp
namespace Connect4HoopsArcade.Core.Primitives;

/// <summary>CPU difficulty ladder, easiest → hardest. Ordinal aligns with search depth (Novato = loose).</summary>
public enum CpuDifficulty { Novato, Principiante, Amateur, Titular, Estrella, MVP }
```

- [ ] **Step 4: Parameterize the strategy by depth** — in `src/Connect4HoopsArcade.Core/Ai/CpuStrategy.cs`:

Replace the class XML doc comment:

```csharp
/// <summary>
/// CPU is always Player2. Normal/Sharp use a minimax search with alpha-beta pruning and a
/// window-based evaluation (4-in-a-row potential + centre control). Chill stays deliberately weak
/// so it's beatable for casual/younger players.
/// </summary>
```

with:

```csharp
/// <summary>
/// CPU is always Player2. Difficulty is a 6-level ladder (Novato..MVP). Novato plays loosely
/// (beatable on purpose); the rest run minimax with alpha-beta pruning + a window-based evaluation
/// (4-in-a-row potential + centre control) to depth 1..5 — deeper sees further and plays stronger.
/// </summary>
```

Then replace this block:

```csharp
        if (difficulty == CpuDifficulty.Chill)
        {
            // Easy mode: only block half the time, otherwise play loosely — beatable on purpose.
            int threat = ImmediateWin(board, Cell.Player1);
            if (threat >= 0 && rng.Next(2) == 0) return threat;
            return available[rng.Next(available.Count)];
        }

        // Normal / Sharp: search ahead. Depth tuned to stay snappy in WebAssembly while still
        // blocking threats and 2-move traps (a big step up from the old centre-stacking CPU).
        int depth = difficulty == CpuDifficulty.Sharp ? 5 : 4;
```

with:

```csharp
        int depth = DepthFor(difficulty);
        if (depth == 0)
        {
            // Novato: only block half the time, otherwise play loosely — beatable on purpose.
            int threat = ImmediateWin(board, Cell.Player1);
            if (threat >= 0 && rng.Next(2) == 0) return threat;
            return available[rng.Next(available.Count)];
        }

        // Levels 1-5: minimax to the level's depth (deeper sees further → stronger). Depth stays ≤ 5
        // to remain snappy in WebAssembly.
```

Then add this helper method right after `ChooseColumn` (before `Minimax`):

```csharp
    /// <summary>Search depth per level. 0 = Novato (no search, loose play); 1..5 = minimax depth.</summary>
    private static int DepthFor(CpuDifficulty d) => d switch
    {
        CpuDifficulty.Novato => 0,
        CpuDifficulty.Principiante => 1,
        CpuDifficulty.Amateur => 2,
        CpuDifficulty.Titular => 3,
        CpuDifficulty.Estrella => 4,
        _ => 5, // MVP
    };
```

- [ ] **Step 5: Fix the GameSession default** — in `src/Connect4HoopsArcade.Web/State/GameSession.cs`, replace:

```csharp
    public CpuDifficulty CpuLevel { get; set; } = CpuDifficulty.Sharp;
```

with:

```csharp
    public CpuDifficulty CpuLevel { get; set; } = CpuDifficulty.Amateur;
```

- [ ] **Step 6: Run the tests** — Run: `dotnet test`. Expected: all green (the 9 CpuStrategy cases + the rest = the suite passes).

- [ ] **Step 7: Build the whole solution** — Run: `dotnet build`. Expected: 0 errors (GameSession default fixed).

- [ ] **Step 8: Commit**

```bash
git add src/Connect4HoopsArcade.Core/Primitives/CpuDifficulty.cs src/Connect4HoopsArcade.Core/Ai/CpuStrategy.cs tests/Connect4HoopsArcade.Core.Tests/CpuStrategyTests.cs src/Connect4HoopsArcade.Web/State/GameSession.cs
git commit -m "feat(core): 6-level CPU difficulty ladder (Novato..MVP) by search depth"
```

---

### Task 2: Persist the level (settings plumbing)

**Files:**
- Modify: `src/Connect4HoopsArcade.Web/Models/GameSettings.cs`
- Modify: `src/Connect4HoopsArcade.Web/Services/SettingsStore.cs`

- [ ] **Step 1: Add the persisted field** — in `src/Connect4HoopsArcade.Web/Models/GameSettings.cs`, add after the `Mode` property:

```csharp
    public CpuDifficulty CpuLevel { get; set; } = CpuDifficulty.Amateur;
```

(`CpuDifficulty` resolves via the existing `using Connect4HoopsArcade.Core.Primitives;` at the top.)

- [ ] **Step 2: Push it in ApplyAsync** — in `src/Connect4HoopsArcade.Web/Services/SettingsStore.cs`, in `ApplyAsync`, add after `_session.NarratorTone = Current.Tone;`:

```csharp
        _session.CpuLevel = Current.CpuLevel;
```

- [ ] **Step 3: Build** — Run: `dotnet build`. Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Models/GameSettings.cs src/Connect4HoopsArcade.Web/Services/SettingsStore.cs
git commit -m "feat(web): persist CpuLevel setting (pushed to GameSession on apply)"
```

---

### Task 3: Labels catalog + shared selector component

**Files:**
- Create: `src/Connect4HoopsArcade.Web/Services/CpuLevelLabels.cs`
- Create: `src/Connect4HoopsArcade.Web/Components/Setup/CpuLevelSelector.razor`
- Modify: `src/Connect4HoopsArcade.Web/wwwroot/css/board.css`

- [ ] **Step 1: Create the labels** — `src/Connect4HoopsArcade.Web/Services/CpuLevelLabels.cs`:

```csharp
using Connect4HoopsArcade.Core.Primitives;

namespace Connect4HoopsArcade.Web.Services;

/// <summary>Spanish display names for the CPU difficulty ladder.</summary>
public static class CpuLevelLabels
{
    public static string Name(CpuDifficulty d) => d switch
    {
        CpuDifficulty.Novato => "Novato",
        CpuDifficulty.Principiante => "Principiante",
        CpuDifficulty.Amateur => "Amateur",
        CpuDifficulty.Titular => "Titular",
        CpuDifficulty.Estrella => "Estrella",
        _ => "MVP",
    };
}
```

- [ ] **Step 2: Create the selector** — `src/Connect4HoopsArcade.Web/Components/Setup/CpuLevelSelector.razor`:

```razor
@using Connect4HoopsArcade.Core.Primitives
@using Connect4HoopsArcade.Web.Services

<div class="cpu-level-sel">
  <button type="button" class="cpu-level-arrow" @onclick="Prev" disabled="@IsMin" aria-label="Nivel más fácil">◀</button>
  <div class="cpu-level-name">@CpuLevelLabels.Name(Level)</div>
  <button type="button" class="cpu-level-arrow" @onclick="Next" disabled="@IsMax" aria-label="Nivel más difícil">▶</button>
</div>

@code {
    [Parameter] public CpuDifficulty Level { get; set; }
    [Parameter] public EventCallback<CpuDifficulty> OnChange { get; set; }

    private bool IsMin => Level == CpuDifficulty.Novato;
    private bool IsMax => Level == CpuDifficulty.MVP;

    private Task Prev() => IsMin ? Task.CompletedTask : OnChange.InvokeAsync(Level - 1);
    private Task Next() => IsMax ? Task.CompletedTask : OnChange.InvokeAsync(Level + 1);
}
```

(`Level - 1` / `Level + 1` are valid enum arithmetic and return `CpuDifficulty`.)

- [ ] **Step 3: Add selector styles** — append to `src/Connect4HoopsArcade.Web/wwwroot/css/board.css`:

```css
.cpu-level-sel { display:flex; align-items:center; gap:8px; margin-top:4px; }
.cpu-level-arrow { cursor:pointer; width:34px; height:34px; flex:none; border-radius:10px; border:1.5px solid rgba(255,255,255,.2); background:rgba(255,255,255,.06); color:#fff; font-size:14px; font-weight:900; display:flex; align-items:center; justify-content:center; }
.cpu-level-arrow:disabled { opacity:.3; cursor:default; }
.cpu-level-name { flex:1; text-align:center; font-family:'Fredoka',sans-serif; font-weight:700; font-size:20px; color:#ffd23f; white-space:nowrap; }
```

- [ ] **Step 4: Build** — Run: `dotnet build`. Expected: 0 errors. (Not mounted yet.)

- [ ] **Step 5: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Services/CpuLevelLabels.cs src/Connect4HoopsArcade.Web/Components/Setup/CpuLevelSelector.razor src/Connect4HoopsArcade.Web/wwwroot/css/board.css
git commit -m "feat(web): add CpuLevelLabels + shared CpuLevelSelector stepper"
```

---

### Task 4: Desktop — selector in the CPU card (name slot)

**Files:**
- Modify: `src/Connect4HoopsArcade.Web/Components/Setup/PlayerSetupCard.razor`
- Modify: `src/Connect4HoopsArcade.Web/Components/Screens/DesktopPlayerSetup.razor`

- [ ] **Step 1: PlayerSetupCard — render the selector for the CPU** — in `PlayerSetupCard.razor`, replace the name block:

```razor
    <div style="flex:1;min-width:0;">
      <div style="font-size:13px;font-weight:900;color:@Hex;letter-spacing:1px;">@Tag</div>
      <input value="@Player.Name" @onchange="OnNameChange" disabled="@Player.IsCpu" maxlength="12" placeholder="Nombre"
             class="font-display"
             style="width:100%;margin-top:4px;background:rgba(0,0,0,.25);border:2px solid rgba(255,255,255,.12);border-radius:12px;padding:10px 12px;color:#fff;font-weight:600;font-size:22px;outline:none;opacity:@(Player.IsCpu ? "0.6" : "1");" />
    </div>
```

with:

```razor
    <div style="flex:1;min-width:0;">
      <div style="font-size:13px;font-weight:900;color:@Hex;letter-spacing:1px;">@Tag</div>
      @if (Player.IsCpu && CpuLevel is not null)
      {
          <CpuLevelSelector Level="CpuLevel.Value" OnChange="OnCpuLevelChange" />
      }
      else
      {
          <input value="@Player.Name" @onchange="OnNameChange" disabled="@Player.IsCpu" maxlength="12" placeholder="Nombre"
                 class="font-display"
                 style="width:100%;margin-top:4px;background:rgba(0,0,0,.25);border:2px solid rgba(255,255,255,.12);border-radius:12px;padding:10px 12px;color:#fff;font-weight:600;font-size:22px;outline:none;opacity:@(Player.IsCpu ? "0.6" : "1");" />
      }
    </div>
```

And add to the `@code` block (alongside the existing parameters):

```csharp
    [Parameter] public CpuDifficulty? CpuLevel { get; set; }
    [Parameter] public EventCallback<CpuDifficulty> OnCpuLevelChange { get; set; }
```

(`CpuDifficulty` resolves via the existing `@using Connect4HoopsArcade.Core.Primitives` at the top of the file.)

- [ ] **Step 2: DesktopPlayerSetup — wire + persist** — in `DesktopPlayerSetup.razor`:

Add the settings-store inject after `@inject GameSession Session`:

```razor
@inject Connect4HoopsArcade.Web.Services.Abstractions.ISettingsStore Store
```

Pass the level to both `PlayerSetupCard` usages (only the CPU card renders it). Replace:

```razor
    <PlayerSetupCard Player="Session.Players[0]" Index="0"
                     TakenColorId="@(IsTwoPlayer ? Session.Players[1].ColorId : null)"
                     OnChange="@(p => Session.SetPlayer(0, p))" />
    <PlayerSetupCard Player="Session.Players[1]" Index="1"
                     TakenColorId="@(IsTwoPlayer ? Session.Players[0].ColorId : null)"
                     OnChange="@(p => Session.SetPlayer(1, p))" />
```

with:

```razor
    <PlayerSetupCard Player="Session.Players[0]" Index="0"
                     TakenColorId="@(IsTwoPlayer ? Session.Players[1].ColorId : null)"
                     OnChange="@(p => Session.SetPlayer(0, p))"
                     CpuLevel="Store.Current.CpuLevel" OnCpuLevelChange="SetCpuLevel" />
    <PlayerSetupCard Player="Session.Players[1]" Index="1"
                     TakenColorId="@(IsTwoPlayer ? Session.Players[0].ColorId : null)"
                     OnChange="@(p => Session.SetPlayer(1, p))"
                     CpuLevel="Store.Current.CpuLevel" OnCpuLevelChange="SetCpuLevel" />
```

Add the handler to the `@code` block:

```csharp
    private async Task SetCpuLevel(Connect4HoopsArcade.Core.Primitives.CpuDifficulty lvl)
    {
        Store.Current.CpuLevel = lvl;
        await Store.SaveAsync();
    }
```

- [ ] **Step 3: Build** — Run: `dotnet build`. Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Components/Setup/PlayerSetupCard.razor src/Connect4HoopsArcade.Web/Components/Screens/DesktopPlayerSetup.razor
git commit -m "feat(web): desktop CPU-level selector in the CPU setup card"
```

---

### Task 5: Mobile — yellow box above PLAY

**Files:**
- Modify: `src/Connect4HoopsArcade.Web/Components/Screens/MobilePlayerSetup.razor`
- Modify: `src/Connect4HoopsArcade.Web/wwwroot/css/mobile.css`

- [ ] **Step 1: MobilePlayerSetup — add the box** — in `MobilePlayerSetup.razor`:

Add the settings inject after the existing `@inherits SessionComponentBase` line:

```razor
@inject Connect4HoopsArcade.Web.Services.Abstractions.ISettingsStore Store
```

In the `.mob-setup-foot` block, add the CPU box as the FIRST child (above the warning and the PLAY button). Replace:

```razor
  <div class="mob-setup-foot">
    @if (Warning != ColorWarning.None)
    {
      <div class="mob-warn">⚠ @ColorWarningMessages.Message(Warning)</div>
    }
    <button @onclick="Begin" disabled="@StartDisabled" class="font-display mob-play">¡JUGAR! ▶</button>
  </div>
```

with:

```razor
  <div class="mob-setup-foot">
    @if (!IsTwoPlayer)
    {
      <div class="mob-cpu-box">
        <span class="mob-cpu-box-label">🤖 NIVEL CPU</span>
        <CpuLevelSelector Level="Store.Current.CpuLevel" OnChange="SetCpuLevel" />
      </div>
    }
    @if (Warning != ColorWarning.None)
    {
      <div class="mob-warn">⚠ @ColorWarningMessages.Message(Warning)</div>
    }
    <button @onclick="Begin" disabled="@StartDisabled" class="font-display mob-play">¡JUGAR! ▶</button>
  </div>
```

Add the handler to the `@code` block:

```csharp
    private async Task SetCpuLevel(Connect4HoopsArcade.Core.Primitives.CpuDifficulty lvl)
    {
        Store.Current.CpuLevel = lvl;
        await Store.SaveAsync();
    }
```

(`CpuLevelSelector` resolves because it lives in `Components/Setup`, already imported by the existing `@using Connect4HoopsArcade.Web.Components.Setup` in this file.)

- [ ] **Step 2: Style the box** — append to `src/Connect4HoopsArcade.Web/wwwroot/css/mobile.css`:

```css
.mob-cpu-box { flex: none; display: flex; align-items: center; gap: 10px; width: 100%; max-width: 460px;
  padding: 8px 12px; border-radius: 14px;
  background: rgba(255,210,63,.12); border: 1.5px solid rgba(255,210,63,.5); }
.mob-cpu-box-label { flex: none; font-weight: 900; font-size: 12px; color: #ffd23f; letter-spacing: .5px; }
.mob-cpu-box .cpu-level-sel { flex: 1; margin-top: 0; }
```

- [ ] **Step 3: Build** — Run: `dotnet build`. Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Components/Screens/MobilePlayerSetup.razor src/Connect4HoopsArcade.Web/wwwroot/css/mobile.css
git commit -m "feat(web): mobile CPU-level selector (yellow box above PLAY, 1P)"
```

---

### Task 6: Verify in browser + update vault

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Run the app** — `dotnet run --project src/Connect4HoopsArcade.Web`. Open the URL.

- [ ] **Step 2: Desktop check (wide window)** — choose 1 player → on the CPU card, where the name would be, the `◀ Amateur ▶` stepper shows. Step it up/down (◀ disabled at Novato, ▶ at MVP). Start a game; confirm the CPU plays at the chosen level (Novato is loose; MVP is brutal). Choose 2 players → no selector appears.

- [ ] **Step 3: Mobile check (iPhone viewport ~393×852)** — 1 player → a yellow "🤖 NIVEL CPU" box with the stepper sits just above ¡JUGAR!; PLAY stays visible. 2 players → no box. Reload the page → the last-chosen level persists.

- [ ] **Step 4: Confirm Core tests** — Run: `dotnet test`. Expected: all green.

- [ ] **Step 5: Update `CLAUDE.md`** — replace the "CPU AI" section paragraph:

Find (under `## CPU AI`):

```markdown
`Core/Ai/CpuStrategy.cs` — **minimax + alpha-beta** with a window-based evaluation (4-in-a-row potential +
centre control). Depth: **Sharp 5, Normal 4** (Sharp is the default `GameSession.CpuLevel`); **Chill** is
deliberately weak (beatable). Always takes an immediate win; the search blocks threats and never hands a
free win. There is no in-UI difficulty selector yet (possible future add).
```

Replace with:

```markdown
`Core/Ai/CpuStrategy.cs` — **minimax + alpha-beta** with a window-based evaluation (4-in-a-row potential +
centre control). Difficulty is a **6-level ladder** `CpuDifficulty { Novato, Principiante, Amateur, Titular,
Estrella, MVP }` mapped to search depth via `DepthFor` (Novato = 0 = loose play: takes obvious wins, blocks
~50%, else random; 1..5 = minimax depth). Always takes an immediate win at every level. The level is a
**persisted setting** (`GameSettings.CpuLevel`, default **Amateur**) pushed to `GameSession.CpuLevel` by
`SettingsStore.ApplyAsync`, and is chosen on the **setup screen** via the shared `CpuLevelSelector` stepper
(desktop: in the CPU card at the name slot; mobile: a yellow box above PLAY; 1P only). Not in the Settings panel.
```

Then update the `## Status` line (append to the post-MVP list): `difficulty selector (6-level CPU ladder)`. And change the "Next focus" line to:

```markdown
**Next focus: item 2 (cast / big-screen projection) or item 4 (ESP32 sensor) — user's call.**
```

- [ ] **Step 6: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: record 6-level CPU difficulty + setup selector in vault"
```

---

## Notes for the implementer

- **Core stays pure** — the enum + `CpuStrategy` change is BCL-only; minimax/eval logic is unchanged, only the depth is parameterized.
- **2-player is untouched** — the selector only renders when there's a CPU (`Player.IsCpu` desktop / `!IsTwoPlayer` mobile).
- **Persistence mirrors `NarratorTone`** — `GameSettings.CpuLevel` → `SettingsStore.ApplyAsync` → `GameSession.CpuLevel`. The setup selector writes `Store.Current.CpuLevel` + `SaveAsync()`.
- **Don't over-assert the low levels.** The static evaluation already penalises the opponent's open 3-windows, so even depth-1 (Principiante) tends to neutralise obvious threats by heuristic — it is the *weakest search* level (1-ply, falls into traps, misses forced sequences), not a "never blocks" level. Tests only assert the guaranteed behaviour (take-win at every level; depth-2+ blocks an immediate win; depth-5 blocks developing threats / never hands a free win). The fine-grained curve is validated by playing, not unit tests.
