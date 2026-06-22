# Practice / tutorial mode (sandbox vs CPU with undo) — design

**Date:** 2026-06-22
**Status:** Approved (design); pending implementation plan
**Roadmap:** The agreed in-app "tutorial / learn" feature. Scope: a practice sandbox to learn by
experimenting against the CPU. The step-back (undo) is **exclusive to practice — the normal game is untouched.**

## Problem / goal
There's no way to learn or experiment in-app. The user wants a practice mode vs the CPU where you can
**step back moves** to try different columns and see how the AI responds — learning by experimentation, all
inside the webapp.

## Decisions (from brainstorming)
- **Type:** practice **sandbox** vs the CPU (not scripted lessons).
- **Undo:** reverts a **full turn** (the CPU's reply + your last drop) so it's your turn again to try a
  different column. **Undo back to the empty board, plus Redo.**
- **Entry:** a third card **🎓 Práctica** on the "Elige modo de juego" screen → straight into the board
  (no player-customization screen).
- **CPU level:** **selectable inside practice** (change it live to see how each difficulty responds).
- **No scoreboard** (it's practice). **Soft win:** connecting 4 shows a discreet banner + highlights the
  line, but does NOT open the Victory screen — you can undo and keep experimenting, or restart.

## Additions for a good tutorial (author's call, kept lean)
- **One-time intro card:** first time entering Práctica, a small dismissible card with 3 tips: "Toca una
  columna para soltar tu ficha", "Conecta 4 (horizontal, vertical o diagonal) para ganar", "Usa ↶ Deshacer
  para probar otra jugada y ver cómo responde la CPU". Re-openable via a "?" button. (Persisted "seen" flag
  in settings so it shows once.)
- **Hints toggle (off by default):** when on, highlight the column where **you** can win immediately, and
  the column where you must **block** the CPU's immediate win. Educational; reuses immediate-win detection.
- **Keyboard (desktop):** Undo = `Z` / Backspace, Redo = `Y`. (Nice-to-have; cut if it bloats the plan.)

## Non-goals
- No scripted step-by-step lessons (separate future feature if wanted).
- No scores, no player customization, no "who starts" toggle (you always start as P1 in practice).
- No changes to the normal 1P/2P game flow (undo/redo/soft-win are practice-only).

## Architecture
Reuse everything: `Core` (Board/WinDetector/ThreatScanner/CpuStrategy), `BoardGrid`, `MoveRouter`,
`GameSession`. Practice is a 1P-vs-CPU flow with a move history + soft win.

- **`GameSession.Practice`** (bool) — set by `ChoosePractice()`, cleared when leaving. Practice runs on
  `AppScreen.Game` (so existing `Place()` guards that check `Screen == Game` keep working); `AppShell`
  branches the `Game` case to render `PracticeView` when `Practice` is true.
- **Move history:** `GameSession` keeps `_history` (List<int> of dropped columns, in play order) and
  `_redo` (Stack<int>). Every drop in practice (yours and the CPU's, both go through `Place`) appends to
  `_history` and clears `_redo`.
- **`UndoTurn()`** (practice only): pop the trailing CPU move (if present) + the trailing human move, push
  them to `_redo`, rebuild the board by replaying the remaining `_history`, clear winner/winning-cells, set
  `Current` back to the human, re-render. Guarded by `CanUndo` (history not empty) and `!IsBusy`.
- **`Redo()`** (practice only): pop columns from `_redo` and re-apply them (human move, then CPU move)
  using the **stored columns** — so the CPU's move is reproduced exactly, not recomputed (CpuStrategy has
  randomness). `CanRedo` gates it.
- **`RestartPractice()`**: clear board + history + redo + winner.
- **Board replay (pure, TDD'd in `Core`):** a helper that rebuilds a `GameBoard` from a column sequence with
  strict player alternation from the starter (e.g. `BoardReplay.FromColumns(cols, starterCell)`), plus a
  helper to find an **immediate winning column** for a cell (for the Hints toggle). These are pure and unit-
  tested; `GameSession` orchestrates history/redo and calls them.
- **`ChoosePractice()`**: sets `Practice=true`, `Mode=OnePlayer`, default players (you=P1, CPU=P2),
  resets board/history, `Screen=Game`, starts turn flow (you move first).

## UI
- **`GameModeSelector`**: add a third 🎓 Práctica card (subtitle "Experimenta · deshaz jugadas") →
  `Session.ChoosePractice()`.
- **`PracticeView`** (new, responsive — one component; board via `BoardGrid FitContainer="true"`):
  - The board (taps → `MoveRouter` → `GameSession.TryDrop`, same pipeline).
  - A **practice control bar** (wraps on phone): ↶ Deshacer (disabled when `!CanUndo`), ↷ Rehacer
    (disabled when `!CanRedo`), 🔄 Reiniciar, a compact **CPU level selector** (reuse `CpuLevelSelector`),
    a **Pistas** toggle, a "?" help button (re-opens the intro card), and 🏠 Inicio.
  - A short narrator/status line (turn / soft-win banner).
  - The one-time intro card overlay on first entry.
  - Landscape: same component; controls in the side column, board maximized (reuse the landscape pattern).
- **Soft win:** on connect-4 in practice, set a `PracticeWinner`/highlight state (reuse `WinningCells`),
  show the banner, block new drops, keep Undo/Redo/Restart active. No Victory screen, no scores.

## Edge cases
- Undo when it's already your turn with only your move pending (CPU hasn't replied / CPU started): pop just
  the human move; never pop below an empty history.
- Pressing a column after an undo clears `_redo` (standard redo invalidation).
- Changing CPU level mid-practice only affects future CPU moves; history stays.
- Leaving practice (Inicio / mode change) clears `Practice`, history, redo (and `AudioStopRequested` as
  usual).
- Rapid input / `IsBusy`: Undo/Redo gated by `!IsBusy` so they can't fire mid-animation or mid-CPU-think.

## Testing / verification
- **TDD the pure `Core` helpers** (`BoardReplay.FromColumns`, immediate-winning-column finder): failing
  tests first. These carry the undo/redo correctness.
- Build-and-verify the UI: enter Práctica from mode select; drop → CPU replies → Undo returns to your turn
  with the CPU reply gone → try another column → different CPU response; Redo re-applies exactly; Restart
  clears; level selector changes responses; soft win shows banner and still allows Undo; Hints highlights
  win/block columns; intro card shows once; works portrait/landscape/desktop.
- Existing 51 Core tests stay green (plus the new replay/finder tests).

## Out of scope / future
- Scripted guided lessons, AI move explanations ("why did the CPU play here"), challenges/puzzles — possible
  later layers on top of this sandbox.
