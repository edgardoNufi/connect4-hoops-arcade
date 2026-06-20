# CPU Taunts + Universal Idle — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a streak-aware "announcer with edge" that taunts the player in 1-player mode (escalating with the CPU's win streak) plus an idle nudge for any mode, all behind a persistent `NarratorTone` setting.

**Architecture:** Pure streak/level math lives in `Core` (`CpuTauntPolicy`, `VoicePicker`) under TDD. `GameSession` owns the streak state and raises new events (`MatchEnded`, `IdleNudged`, `RoundStarted`, and `ThreatRaised` now carrying the mover index). `NarratorService` stays the single owner of the serial voice queue and maps events→audio, delegating math to `Core` and the level→file lookup to a small static table (`CpuTauntLines`). A `VoicePicker` avoids repeating the previous line.

**Tech Stack:** .NET 10, Blazor WebAssembly, xUnit (`tests/Connect4HoopsArcade.Core.Tests`). Spec: [`docs/superpowers/specs/2026-06-19-cpu-taunts-announcer-design.md`](../specs/2026-06-19-cpu-taunts-announcer-design.md).

---

## File Structure

**Create:**
- `src/Connect4HoopsArcade.Core/Narration/CpuTauntPolicy.cs` — enums (`CpuTauntLevel`, `MatchOutcome`), `StreakState`, pure policy.
- `src/Connect4HoopsArcade.Core/Narration/VoicePicker.cs` — pure non-repeating index picker.
- `tests/Connect4HoopsArcade.Core.Tests/CpuTauntPolicyTests.cs`
- `tests/Connect4HoopsArcade.Core.Tests/VoicePickerTests.cs`
- `src/Connect4HoopsArcade.Web/Models/NarratorTone.cs` — `enum NarratorTone { Familiar, Picante, Silencioso }`.
- `src/Connect4HoopsArcade.Web/Services/CpuTauntLines.cs` — static `(level)→string[]` lookup over `AudioKeys`.

**Modify:**
- `src/Connect4HoopsArcade.Web/Services/AudioKeys.cs` — new taunt voice arrays + `LossSting`.
- `src/Connect4HoopsArcade.Web/Models/GameSettings.cs` — add `Tone`.
- `src/Connect4HoopsArcade.Web/Services/SettingsStore.cs` — push `Tone` in `ApplyAsync`.
- `src/Connect4HoopsArcade.Web/Components/Screens/SettingsPanel.razor` — tone segmented control.
- `src/Connect4HoopsArcade.Web/State/GameSession.cs` — streak state, new events, `RecordMatchEnd`, `RoundStarted`, mover-index threat.
- `src/Connect4HoopsArcade.Web/Services/NarratorService.cs` — consume new events, taunts, anti-spam, tone.
- `CLAUDE.md` — update audio-design section + roadmap status.

**Note (audit done):** `Won`/`Drew`/`ThreatRaised` are consumed ONLY by `NarratorService` (no UI/modal/animation/tests). Per the spec we **keep `Won`/`Drew`** (they become unused after Task 6; deletion is a deliberate later step, not in this plan).

**Namespace note:** The pure types live in a new `Connect4HoopsArcade.Core.Narration` namespace (cohesive grouping of narration logic). This refines the spec §3 tentative `Core.Ai` location — `VoicePicker` is not AI, so `Core.Narration` reads better. Both `CpuTauntPolicy` and `VoicePicker` go there.

---

### Task 1: `CpuTauntPolicy` (pure, Core) — TDD

**Files:**
- Create: `src/Connect4HoopsArcade.Core/Narration/CpuTauntPolicy.cs`
- Test: `tests/Connect4HoopsArcade.Core.Tests/CpuTauntPolicyTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/Connect4HoopsArcade.Core.Tests/CpuTauntPolicyTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~CpuTauntPolicyTests`
Expected: FAIL — build error, `CpuTauntPolicy` / `CpuTauntLevel` / `MatchOutcome` do not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/Connect4HoopsArcade.Core/Narration/CpuTauntPolicy.cs`:

```csharp
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
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~CpuTauntPolicyTests`
Expected: PASS (8 cases).

- [ ] **Step 5: Commit**

```bash
git add src/Connect4HoopsArcade.Core/Narration/CpuTauntPolicy.cs tests/Connect4HoopsArcade.Core.Tests/CpuTauntPolicyTests.cs
git commit -m "feat(core): add pure CpuTauntPolicy streak/level rules with tests"
```

---

### Task 2: `VoicePicker` (pure, Core) — TDD

**Files:**
- Create: `src/Connect4HoopsArcade.Core/Narration/VoicePicker.cs`
- Test: `tests/Connect4HoopsArcade.Core.Tests/VoicePickerTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/Connect4HoopsArcade.Core.Tests/VoicePickerTests.cs`:

```csharp
using Connect4HoopsArcade.Core.Narration;
using Xunit;

namespace Connect4HoopsArcade.Core.Tests;

public class VoicePickerTests
{
    [Fact]
    public void Single_item_always_returns_zero()
    {
        Assert.Equal(0, VoicePicker.Pick(count: 1, lastIndex: 0, roll: 99));
        Assert.Equal(0, VoicePicker.Pick(count: 1, lastIndex: -1, roll: 0));
    }

    [Fact]
    public void Never_repeats_the_last_index()
    {
        for (int roll = 0; roll < 100; roll++)
            Assert.NotEqual(1, VoicePicker.Pick(count: 3, lastIndex: 1, roll: roll));
    }

    [Fact]
    public void First_pick_with_no_history_uses_full_range()
    {
        Assert.Equal(2, VoicePicker.Pick(count: 3, lastIndex: -1, roll: 2));
        Assert.Equal(0, VoicePicker.Pick(count: 3, lastIndex: -1, roll: 3)); // wraps
    }

    [Fact]
    public void Result_is_always_in_range_and_not_last()
    {
        for (int roll = -5; roll < 50; roll++)
        {
            int r = VoicePicker.Pick(count: 4, lastIndex: 2, roll: roll);
            Assert.InRange(r, 0, 3);
            Assert.NotEqual(2, r);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test --filter FullyQualifiedName~VoicePickerTests`
Expected: FAIL — build error, `VoicePicker` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create `src/Connect4HoopsArcade.Core/Narration/VoicePicker.cs`:

```csharp
namespace Connect4HoopsArcade.Core.Narration;

/// <summary>
/// Pure helper: pick an index in [0,count) that differs from the previous pick, given a caller-supplied
/// random roll. Deterministic for a fixed roll so it can be unit-tested.
/// </summary>
public static class VoicePicker
{
    public static int Pick(int count, int lastIndex, int roll)
    {
        if (count <= 1) return 0;
        if (lastIndex < 0 || lastIndex >= count) return Mod(roll, count);
        int r = Mod(roll, count - 1);          // choose among the count-1 candidates that aren't lastIndex
        return r >= lastIndex ? r + 1 : r;      // skip over lastIndex
    }

    private static int Mod(int a, int m) => ((a % m) + m) % m;   // non-negative even for negative roll
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test --filter FullyQualifiedName~VoicePickerTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/Connect4HoopsArcade.Core/Narration/VoicePicker.cs tests/Connect4HoopsArcade.Core.Tests/VoicePickerTests.cs
git commit -m "feat(core): add pure VoicePicker non-repeating selector with tests"
```

---

### Task 3: Audio keys + line table + tone enum

**Files:**
- Modify: `src/Connect4HoopsArcade.Web/Services/AudioKeys.cs`
- Create: `src/Connect4HoopsArcade.Web/Models/NarratorTone.cs`
- Create: `src/Connect4HoopsArcade.Web/Services/CpuTauntLines.cs`

This task is purely additive (no behavior change yet); it must compile.

- [ ] **Step 1: Add the tone enum**

Create `src/Connect4HoopsArcade.Web/Models/NarratorTone.cs`:

```csharp
namespace Connect4HoopsArcade.Web.Models;

/// <summary>Announcer personality. Familiar caps escalation at Confident; Silencioso mutes all voice.</summary>
public enum NarratorTone { Familiar, Picante, Silencioso }
```

- [ ] **Step 2: Add taunt audio keys**

In `src/Connect4HoopsArcade.Web/Services/AudioKeys.cs`, add these inside the `AudioKeys` class, right after the `Rematch` constant (last line before the closing brace):

```csharp

    // ---- CPU taunts (1P) + universal idle. Arrays grow as more variants are recorded (add -02, -03 …). ----
    public static readonly string[] CpuThreatNeutral   = { "voice/cpu-threat-neutral-01.mp3" };
    public static readonly string[] CpuThreatLight     = { "voice/cpu-threat-light-01.mp3" };
    public static readonly string[] CpuThreatConfident = { "voice/cpu-threat-confident-01.mp3" };
    public static readonly string[] CpuThreatBoss      = { "voice/cpu-threat-boss-01.mp3" };

    public static readonly string[] CpuIdleNeutral     = { "voice/cpu-idle-neutral-01.mp3" };
    public static readonly string[] CpuIdleLight       = { "voice/cpu-idle-light-01.mp3" };
    public static readonly string[] CpuIdleConfident   = { "voice/cpu-idle-confident-01.mp3" };
    public static readonly string[] CpuIdleBoss        = { "voice/cpu-idle-boss-01.mp3" };

    public static readonly string[] CpuWinLight        = { "voice/cpu-win-light-01.mp3" };
    public static readonly string[] CpuWinConfident    = { "voice/cpu-win-confident-01.mp3" };
    public static readonly string[] CpuWinBoss         = { "voice/cpu-win-boss-01.mp3" };

    public static readonly string[] StreakBreak        = { "voice/streak-break-01.mp3" };
    public static readonly string[] BeatCpu            = { "voice/beat-cpu-01.mp3" };
    public static readonly string[] IdleNudge          = { "voice/idle-01.mp3" };

    // Optional short SFX played (instead of the win cheer) when the CPU beats the player in 1P.
    public const string LossSting = "game/loss-sting.mp3";
```

- [ ] **Step 3: Add the line lookup table**

Create `src/Connect4HoopsArcade.Web/Services/CpuTauntLines.cs`:

```csharp
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
```

- [ ] **Step 4: Build to verify it compiles**

Run: `dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Models/NarratorTone.cs src/Connect4HoopsArcade.Web/Services/AudioKeys.cs src/Connect4HoopsArcade.Web/Services/CpuTauntLines.cs
git commit -m "feat(web): add CPU taunt audio keys, line table, and NarratorTone enum"
```

---

### Task 4: Persist `NarratorTone` setting (Settings plumbing + UI)

**Files:**
- Modify: `src/Connect4HoopsArcade.Web/Models/GameSettings.cs`
- Modify: `src/Connect4HoopsArcade.Web/Services/SettingsStore.cs:62-68` (`ApplyAsync`)
- Modify: `src/Connect4HoopsArcade.Web/Components/Screens/SettingsPanel.razor`
- Modify: `src/Connect4HoopsArcade.Web/State/GameSession.cs` (add `NarratorTone` property only)

`GameSession.NarratorTone` is added here as a plain settable property (like `Speed`). It has no effect until Task 6, which is harmless.

- [ ] **Step 1: Add the persisted field**

In `src/Connect4HoopsArcade.Web/Models/GameSettings.cs`, add after the `Mode` property (same namespace already has `NarratorTone`):

```csharp
    public NarratorTone Tone { get; set; } = NarratorTone.Familiar;
```

- [ ] **Step 2: Add the runtime property on GameSession**

In `src/Connect4HoopsArcade.Web/State/GameSession.cs`, add right after the line `public AnimationSpeed Speed { get; set; } = AnimationSpeed.Normal;`:

```csharp
    // Persistent announcer tone (pushed by SettingsStore.ApplyAsync). NOT reset between games.
    public Connect4HoopsArcade.Web.Models.NarratorTone NarratorTone { get; set; }
        = Connect4HoopsArcade.Web.Models.NarratorTone.Familiar;
```

- [ ] **Step 3: Push the setting in ApplyAsync**

In `src/Connect4HoopsArcade.Web/Services/SettingsStore.cs`, in `ApplyAsync`, add a line after `_session.Speed = Current.Speed;`:

```csharp
        _session.NarratorTone = Current.Tone;
```

- [ ] **Step 4: Add the segmented control to Settings**

In `src/Connect4HoopsArcade.Web/Components/Screens/SettingsPanel.razor`, add this row immediately after the "Velocidad de animación" `set-row` block (after its closing `</div></div>`):

```razor
      <div class="set-row"><span class="set-ico">🗯️</span><span style="flex:1;font-weight:800;font-size:16px;">Tono del narrador</span>
        <div style="display:flex;gap:8px;flex-wrap:wrap;">
          <button @onclick="@(() => SetTone(NarratorTone.Familiar))" class="seg-btn @(S.Tone == NarratorTone.Familiar ? "seg-btn--on" : "")">Familiar</button>
          <button @onclick="@(() => SetTone(NarratorTone.Picante))" class="seg-btn @(S.Tone == NarratorTone.Picante ? "seg-btn--on" : "")">Picante</button>
          <button @onclick="@(() => SetTone(NarratorTone.Silencioso))" class="seg-btn @(S.Tone == NarratorTone.Silencioso ? "seg-btn--on" : "")">Silencioso</button>
        </div></div>
```

And in the `@code` block, add this method after `SetSpeed`:

```csharp
    private async Task SetTone(NarratorTone t) { S.Tone = t; await Store.SaveAsync(); }
```

- [ ] **Step 5: Build to verify it compiles**

Run: `dotnet build`
Expected: Build succeeded, 0 errors. (`NarratorTone` resolves via the existing `@using Connect4HoopsArcade.Web.Models` in SettingsPanel.)

- [ ] **Step 6: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Models/GameSettings.cs src/Connect4HoopsArcade.Web/Services/SettingsStore.cs src/Connect4HoopsArcade.Web/Components/Screens/SettingsPanel.razor src/Connect4HoopsArcade.Web/State/GameSession.cs
git commit -m "feat(web): persist NarratorTone setting and expose it in Settings"
```

---

### Task 5: GameSession — streak state + new events

**Files:**
- Modify: `src/Connect4HoopsArcade.Web/State/GameSession.cs`
- Modify: `src/Connect4HoopsArcade.Web/Services/NarratorService.cs` (one-line signature fix to keep it compiling)

After this task the build is green and behavior is unchanged (the new events exist but `NarratorService` still plays the old neutral lines). Task 6 wires the taunts.

- [ ] **Step 1: Add the `using` for Core.Narration**

In `src/Connect4HoopsArcade.Web/State/GameSession.cs`, add to the using block at the top:

```csharp
using Connect4HoopsArcade.Core.Narration;
```

- [ ] **Step 2: Change `ThreatRaised` to carry the mover index, and add the new events**

Replace this line:

```csharp
    public event Action? ThreatRaised;
```

with:

```csharp
    public event Action<int>? ThreatRaised;    // arg: index of the player who just moved (the threat owner)
```

Then, immediately after the line `public event Action? GameStarted;`, add:

```csharp
    public event Action? RoundStarted;          // every BeginGame/Rematch/ResetBoard — resets per-round narration
    public event Action? IdleNudged;            // the idle nudge fired (any mode)
    public event Action<int?, GameMode>? MatchEnded;   // winner (null = draw) + mode; raised after streak update
```

- [ ] **Step 3: Add the streak state fields**

Immediately after the line `public int[] Scores { get; private set; } = { 0, 0 };`, add:

```csharp
    // CPU taunt state (1P only). Persists across Rematch/ResetBoard; reset only by BeginGame (new session).
    public int CpuWinStreak { get; private set; }
    public bool CpuStreakJustBroken { get; private set; }
    public int PlayerLossesAgainstCpu { get; private set; }
```

- [ ] **Step 4: Add the `RecordMatchEnd` helper**

In `src/Connect4HoopsArcade.Web/State/GameSession.cs`, add this private method just before the `private string TurnPhrase(int p)` method:

```csharp
    // Updates the CPU streak (1P only) and announces the end of the match. Must run BEFORE any audio reads
    // CpuWinStreak/CpuStreakJustBroken, so callers invoke it right where the win/draw is finalized.
    private void RecordMatchEnd(int? winner)
    {
        if (Mode == GameMode.OnePlayer)
        {
            var outcome = winner is null ? MatchOutcome.Draw
                        : Players[winner.Value].IsCpu ? MatchOutcome.CpuWin
                        : MatchOutcome.HumanWin;
            var s = CpuTauntPolicy.Advance(CpuWinStreak, PlayerLossesAgainstCpu, outcome);
            CpuWinStreak = s.Streak;
            CpuStreakJustBroken = s.JustBroken;
            PlayerLossesAgainstCpu = s.PlayerLosses;
        }
        MatchEnded?.Invoke(winner, Mode);
    }
```

- [ ] **Step 5: Reset streak on new session + raise `RoundStarted`**

In `ResetState`, replace:

```csharp
        if (resetScores) Scores = new[] { 0, 0 };   // preserved across rematch/reset
        Narrator = narrator;
    }
```

with:

```csharp
        if (resetScores)
        {
            Scores = new[] { 0, 0 };                 // preserved across rematch/reset
            CpuWinStreak = 0;                         // streak + losses reset only on a brand-new session
            PlayerLossesAgainstCpu = 0;
        }
        CpuStreakJustBroken = false;                  // transient: only meaningful inside a MatchEnded handler
        Narrator = narrator;
        RoundStarted?.Invoke();
    }
```

- [ ] **Step 6: Capture `moverIndex` and pass it to `ThreatRaised`**

In `Place`, replace:

```csharp
        Current = Current == 0 ? 1 : 0;
        IsIdle = false;
        Narrator = TurnPhrase(Current);
        Notify();
        TurnChanged?.Invoke(Current);
        if (ThreatScanner.HasImmediateThreat(Board, CellExtensions.ForPlayer(Current == 0 ? 1 : 0)))
            ThreatRaised?.Invoke();
```

with:

```csharp
        int moverIndex = Current;                     // who just dropped — capture BEFORE the flip
        Current = Current == 0 ? 1 : 0;
        IsIdle = false;
        Narrator = TurnPhrase(Current);
        Notify();
        TurnChanged?.Invoke(Current);
        if (ThreatScanner.HasImmediateThreat(Board, CellExtensions.ForPlayer(moverIndex)))
            ThreatRaised?.Invoke(moverIndex);
```

- [ ] **Step 7: Call `RecordMatchEnd` on the three end paths**

In the connect-4 win path inside `Place`, replace:

```csharp
            Won?.Invoke(Current);
            await TransitionToVictory();
            return;
```

with:

```csharp
            Won?.Invoke(Current);
            RecordMatchEnd(Current);
            await TransitionToVictory();
            return;
```

In the draw path inside `Place`, replace:

```csharp
            Drew?.Invoke();
            await Task.Delay(850);
```

with:

```csharp
            Drew?.Invoke();
            RecordMatchEnd(null);
            await Task.Delay(850);
```

In `Resign`, replace:

```csharp
        Won?.Invoke(w);
        _ = TransitionToVictory();
```

with:

```csharp
        Won?.Invoke(w);
        RecordMatchEnd(w);
        _ = TransitionToVictory();
```

- [ ] **Step 8: Raise `IdleNudged` from the idle timer**

In `ArmIdle`, replace:

```csharp
            IsIdle = true;
            Narrator = $"¿Sigues ahí, {Players[Current].Name}? ¡Es tu turno! 🏀";
            Notify();
        });
```

with:

```csharp
            IsIdle = true;
            Narrator = $"¿Sigues ahí, {Players[Current].Name}? ¡Es tu turno! 🏀";
            Notify();
            IdleNudged?.Invoke();
        });
```

- [ ] **Step 9: Keep NarratorService compiling (temporary one-line fix)**

In `src/Connect4HoopsArcade.Web/Services/NarratorService.cs`, change the handler signature (Task 6 replaces the whole file). Replace:

```csharp
    private async void OnThreat()
    {
        // Danger SFX removed — the "almost win" voice variants carry the warning.
        await _audio.PlayRandomVoiceAsync(AudioKeys.AlmostWinV);
    }
```

with:

```csharp
    private async void OnThreat(int moverIndex)
    {
        // Danger SFX removed — the "almost win" voice variants carry the warning.
        await _audio.PlayRandomVoiceAsync(AudioKeys.AlmostWinV);
    }
```

- [ ] **Step 10: Build to verify it compiles**

Run: `dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 11: Run the Core tests (no regressions)**

Run: `dotnet test`
Expected: PASS — all tests green (the 36 existing + the new policy/picker tests).

- [ ] **Step 12: Commit**

```bash
git add src/Connect4HoopsArcade.Web/State/GameSession.cs src/Connect4HoopsArcade.Web/Services/NarratorService.cs
git commit -m "feat(web): GameSession streak state + MatchEnded/IdleNudged/RoundStarted events"
```

---

### Task 6: NarratorService — taunts, anti-spam, tone

**Files:**
- Modify (full rewrite): `src/Connect4HoopsArcade.Web/Services/NarratorService.cs`

- [ ] **Step 1: Replace the whole file**

Replace the entire contents of `src/Connect4HoopsArcade.Web/Services/NarratorService.cs` with:

```csharp
using Connect4HoopsArcade.Core.Narration;
using Connect4HoopsArcade.Core.Primitives;
using Connect4HoopsArcade.Web.Models;
using Connect4HoopsArcade.Web.Services.Abstractions;
using Connect4HoopsArcade.Web.State;

namespace Connect4HoopsArcade.Web.Services;

/// <summary>
/// Maps game events to SFX + voice. Single owner of the serial voice queue. In 1-player mode it adds
/// streak-aware taunts (see CpuTauntPolicy); in 2-player it stays neutral. NarratorTone gates/scales voice.
/// </summary>
public sealed class NarratorService : IDisposable
{
    private readonly GameSession _session;
    private readonly IAudioService _audio;
    private static readonly Random Rng = new();
    private static readonly TimeSpan MidTauntCooldown = TimeSpan.FromSeconds(25);

    private DateTime _lastMidTaunt = DateTime.MinValue;   // last threat/idle taunt; spacing base
    private bool _idleTauntUsedThisRound;                 // cpu-idle is filler: max one per round
    private bool _closingVoiceActive;                     // a closing line is in flight — don't talk over it
    private readonly Dictionary<string, int> _lastIndex = new();   // last variant per category (no repeats)

    public NarratorService(GameSession session, IAudioService audio)
    {
        _session = session;
        _audio = audio;
        _session.GameStarted += OnGameStarted;
        _session.RoundStarted += OnRoundStarted;
        _session.ChipDropped += OnChipDropped;
        _session.TurnChanged += OnTurnChanged;
        _session.ColumnFull  += OnColumnFull;
        _session.ThreatRaised += OnThreat;
        _session.IdleNudged  += OnIdle;
        _session.MatchEnded  += OnMatchEnded;
    }

    private bool VoiceOn => _session.NarratorTone != NarratorTone.Silencioso;
    private bool TauntsOn => _session.Mode == GameMode.OnePlayer && VoiceOn;
    private int CpuIndex =>
        _session.Players.Length > 1 && _session.Players[1].IsCpu ? 1 :
        _session.Players.Length > 0 && _session.Players[0].IsCpu ? 0 : -1;

    // Familiar caps escalation at Confident (no Boss lines); Picante uses the raw level.
    private CpuTauntLevel EffectiveLevel()
    {
        var raw = CpuTauntPolicy.LevelFor(_session.CpuWinStreak);
        return _session.NarratorTone == NarratorTone.Familiar && raw == CpuTauntLevel.BossMode
            ? CpuTauntLevel.ConfidentCpu
            : raw;
    }

    private Task Taunt(string category, IReadOnlyList<string> keys, bool interrupt = false)
    {
        if (keys.Count == 0) return Task.CompletedTask;
        int last = _lastIndex.TryGetValue(category, out var v) ? v : -1;
        int idx = VoicePicker.Pick(keys.Count, last, Rng.Next(100_000));
        _lastIndex[category] = idx;
        return _audio.PlayVoiceAsync(keys[idx], interrupt);
    }

    private bool MidTauntReady() =>
        _session.Winner == null && !_closingVoiceActive
        && DateTime.UtcNow - _lastMidTaunt >= MidTauntCooldown;

    private void OnRoundStarted()
    {
        _idleTauntUsedThisRound = false;
        _closingVoiceActive = false;
        _lastMidTaunt = DateTime.MinValue;
    }

    private async void OnGameStarted()
    {
        if (VoiceOn) await _audio.PlayVoiceAsync(AudioKeys.GetReady);
    }

    private async void OnChipDropped()
    {
        await _audio.PlaySfxAsync(AudioKeys.ChipDrop);
        // Occasional praise (~1 in 8). Serialized after any current line.
        if (VoiceOn && Rng.Next(8) == 0) await _audio.PlayRandomVoiceAsync(AudioKeys.GreatMove);
    }

    private async void OnTurnChanged(int current)
    {
        await _audio.PlaySfxAsync(AudioKeys.TurnChange, cooldownMs: 300);
        if (VoiceOn && Rng.Next(10) < 4)
            await _audio.PlayRandomVoiceAsync(current == 0 ? AudioKeys.PlayerOneTurn : AudioKeys.PlayerTwoTurn);
    }

    private async void OnColumnFull()
    {
        await _audio.PlaySfxAsync(AudioKeys.ColumnFull, cooldownMs: 800);
        if (VoiceOn) await _audio.PlayRandomVoiceAsync(AudioKeys.ColumnFullV);
    }

    private async void OnThreat(int moverIndex)
    {
        // High priority: taunt only when the CPU is the one threatening (1P). Cooldown-gated, no per-round cap.
        if (TauntsOn && CpuIndex >= 0 && moverIndex == CpuIndex)
        {
            if (!MidTauntReady()) return;
            _lastMidTaunt = DateTime.UtcNow;
            await Taunt("cpu-threat", CpuTauntLines.Threat(EffectiveLevel()));
        }
        else if (VoiceOn)
        {
            await _audio.PlayRandomVoiceAsync(AudioKeys.AlmostWinV);   // neutral warning (2P, or human threatening)
        }
    }

    private async void OnIdle()
    {
        if (!VoiceOn) return;
        if (_session.Mode == GameMode.OnePlayer)
        {
            // Filler: at most once per round, and only if no recent taunt (so it never steals a threat's slot).
            if (_idleTauntUsedThisRound || !MidTauntReady()) return;
            _idleTauntUsedThisRound = true;
            _lastMidTaunt = DateTime.UtcNow;
            await Taunt("cpu-idle", CpuTauntLines.Idle(EffectiveLevel()));
        }
        else
        {
            // Generic nudge, any non-CPU context; cooldown-spaced.
            if (DateTime.UtcNow - _lastMidTaunt < MidTauntCooldown) return;
            _lastMidTaunt = DateTime.UtcNow;
            await Taunt("idle", AudioKeys.IdleNudge);
        }
    }

    private async void OnMatchEnded(int? winner, GameMode mode)
    {
        _closingVoiceActive = true;

        // 1P, CPU won → taunt, NO win cheer, optional short loss-sting.
        if (mode == GameMode.OnePlayer && winner is int cpuW && _session.Players[cpuW].IsCpu)
        {
            await _audio.PlaySfxAsync(AudioKeys.LossSting);
            if (VoiceOn) await Taunt("cpu-win", CpuTauntLines.CpuWin(EffectiveLevel()), interrupt: true);
            return;
        }

        // Someone won (1P human, or any 2P win) → voice + win cheer (cheer plays even if voice is muted).
        if (winner is int)
        {
            if (VoiceOn)
            {
                if (mode == GameMode.OnePlayer && _session.CpuStreakJustBroken)
                    await Taunt("streak-break", AudioKeys.StreakBreak, interrupt: true);
                else if (mode == GameMode.OnePlayer)
                    await Taunt("beat-cpu", AudioKeys.BeatCpu, interrupt: true);
                else
                    await _audio.PlayRandomVoiceAsync(AudioKeys.VictoryV, interrupt: true);
            }
            await _audio.PlayRandomSfxAfterVoiceAsync(AudioKeys.WinSfx);
            return;
        }

        // Draw (either mode).
        await _audio.PlaySfxAsync(AudioKeys.DrawSfx);
        if (VoiceOn) await _audio.PlayRandomVoiceAsync(AudioKeys.DrawV, interrupt: true);
    }

    public void Dispose()
    {
        _session.GameStarted -= OnGameStarted;
        _session.RoundStarted -= OnRoundStarted;
        _session.ChipDropped -= OnChipDropped;
        _session.TurnChanged -= OnTurnChanged;
        _session.ColumnFull  -= OnColumnFull;
        _session.ThreatRaised -= OnThreat;
        _session.IdleNudged  -= OnIdle;
        _session.MatchEnded  -= OnMatchEnded;
    }
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Run the full test suite (no regressions)**

Run: `dotnet test`
Expected: PASS — all tests green.

- [ ] **Step 4: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Services/NarratorService.cs
git commit -m "feat(web): streak-aware CPU taunts + universal idle in NarratorService"
```

---

### Task 7: Verify in browser + update the vault

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Run the app**

Run: `dotnet run --project src/Connect4HoopsArcade.Web`
Open the served URL. (WASM hydration can take 6–7s; re-click if the first click lands on the splash.)

- [ ] **Step 2: Manual verification checklist**

Confirm (voice files may be missing → that path is silent but the flow must not error; watch the console):
- 1P, lose to the CPU → on each loss the streak grows; with **Picante** tone the closing line escalates Light→Confident→Boss; with **Familiar** it never reaches Boss.
- Break a CPU streak of ≥2 (win after losing 2+) → `streak-break` path fires; a win after a 0–1 streak uses `beat-cpu`. Both play the win cheer.
- 1P CPU win plays **no** win cheer (loss-sting if present).
- `cpu-threat` can fire even right after a `cpu-idle` (priority); `cpu-idle` happens at most once per game; neither repeats within 25s.
- 2P: idle still nudges (generic `idle-*`), and victory/draw stay neutral.
- Settings → **Silencioso** mutes ALL announcer voice but SFX (chip drop, turn change, win cheer) still play. The tone choice survives a page reload (persisted) and is unaffected by Rematch/Reset.

- [ ] **Step 3: Update the audio-design section of CLAUDE.md**

In `CLAUDE.md`, under "## Audio design — CURRENT state", append this bullet to the "Deliberate choices" list:

```markdown
- **CPU taunts (1P) + `NarratorTone`:** in 1-player the announcer taunts with streak-aware escalation
  (`CpuTauntPolicy` in `Core/Narration`, levels Neutral/Light/Confident/Boss from `GameSession.CpuWinStreak`).
  `NarratorTone` (Familiar default / Picante / Silencioso) is a persisted `SettingsStore` pref: Familiar caps
  at Confident, Silencioso mutes all voice (SFX stay). Mid-game taunts are cooldown-gated (25s); `cpu-threat`
  outranks `cpu-idle` (idle ≤1/round). Closing lines: CPU win → `cpu-win` (no cheer, optional `loss-sting`);
  human win → `streak-break` (if streak ≥2 broken) or `beat-cpu` (+ cheer). 2-player stays neutral.
  `GameSession` raises `MatchEnded`/`IdleNudged`/`RoundStarted`; `ThreatRaised` carries the mover index.
  Voice files listed in `docs/superpowers/specs/2026-06-19-cpu-taunts-announcer-design.md` §5.1.
```

- [ ] **Step 4: Update the status line of CLAUDE.md**

In `CLAUDE.md`, under "## Status", append to the post-MVP list: `streak-aware CPU taunts + NarratorTone`.

- [ ] **Step 5: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: record CPU taunts + NarratorTone in project vault"
```

---

## Notes for the implementer

- **Voice files are produced separately** (spec §5.1). Until they exist, taunt paths are silent — `AudioService.Safe` swallows the JS error. This is expected; verify the *logic/flow*, not the audio, if files aren't in yet.
- **No Web unit-test project exists**; `GameSession`/`NarratorService` are verified by build + the manual checklist. All pure logic that *can* be unit-tested lives in `Core` (Tasks 1–2).
- **Do not delete `Won`/`Drew`** — kept per the spec even though they're now unused by audio (audit: only `NarratorService` referenced them).
- Keep `Core` pure: `CpuTauntPolicy`/`VoicePicker` use only the BCL.
