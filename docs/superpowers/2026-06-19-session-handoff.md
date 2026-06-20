# Session handoff — Connect 4 Hoops Arcade (2026-06-19)

Continuation note for a fresh session. Read **CLAUDE.md** first (the vault), then this. Everything below is
already merged to `main` and auto-deploys to Cloudflare on push.

## What shipped this session (all on `main`, deployed)

1. **Streak-aware CPU taunts + `NarratorTone`** (1P announcer with edge, escalates with the CPU win streak;
   Familiar/Picante/Silencioso tone in Settings). Spec: `specs/2026-06-19-cpu-taunts-announcer-design.md`.
2. **Mobile-first responsive redesign** — dedicated mobile views via `IViewportService`; `MobileGameView`
   (tap-bar scoreboard + ActionSheet + fit-to-screen board) and `MobilePlayerSetup` (J1/J2 tabs); desktop
   untouched. Spec: `specs/2026-06-19-mobile-first-redesign-design.md`.
   - Fixed a follow-up bug: mobile board cells overflowed the grid when a column filled (board now
     `aspect-ratio:7/6` + `flex:1` cells).
3. **6-level CPU difficulty** (Novato/Principiante/Amateur/Titular/Estrella/MVP = depths 0..5) with a
   `CpuLevelSelector` on the setup screen (desktop CPU card / mobile yellow box), persisted, default Amateur.
   Spec: `specs/2026-06-19-cpu-difficulty-levels-design.md`.
4. **"Who starts" toggle** (Tú/CPU, 1P) next to the level selector; persisted.
5. **Build-version corner tag** — `build.sh` stamps the commit SHA + UTC date into `index.html`; dim
   fixed-corner `.build-tag` ("dev" locally). Use it to confirm a deploy is live.
6. **Audio fix:** closing sting now plays AFTER the voice on loss/draw (was overlapping).

All 51 Core tests green. Each feature has a spec + plan under `docs/superpowers/{specs,plans}/`.

## How we work here (process that's been working well)
- Per feature: **brainstorming → spec (committed) → writing-plans → subagent-driven-development → browser
  verify → finishing-a-development-branch (merge to `main` + push = deploy)**. These are superpowers skills.
- `Core` is pure + TDD'd; UI verified by `dotnet build` + driving the app in the Claude Preview at an iPhone
  viewport (393×852) and desktop (1280×800). WASM hydration takes ~6–7s; the first scripted click often lands
  on the splash — re-click after `document.readyState==='complete'`. Prefer `preview_eval` measurements over
  screenshots (screenshots occasionally time out).
- Working in the harness-managed worktree `…/.claude/worktrees/nostalgic-pasteur-3c6c4e` on branch
  `claude/nostalgic-pasteur-3c6c4e`; publish via `git -C C:/Proyectos/Arcade merge --ff-only <branch>` then push.

## Pending / next up (roadmap)
- **Item #2 — Cast / big-screen projection.** The big one. Honest constraint (in CLAUDE.md): iOS Safari can't
  AirPlay an arbitrary app UI. Best route is likely the **companion model** (TV browser shows the game; phone
  is a controller over WebSocket) — **reuses the `IMoveSource`/`MoveRouter` abstraction** (a phone controller
  is just another move source, like the ESP32). Brainstorm before building.
- **Item #4 — Physical sensor (ESP32 / WebSocket).** `IMoveSource`/`SensorConnectionService` are ready; only
  the real transport is stubbed.
- **Item #5 (optional)** — background-music toggle / re-enable, theme switching (deferred on purpose).

## Loose ends to remember
- **CPU-taunt voice files:** user produces them (recording checklist in
  `specs/2026-06-19-cpu-taunts-announcer-design.md` §5.1). Many are in `wwwroot/audio/voice/`; until a given
  file exists its taunt path is silently skipped (`AudioService.Safe` swallows the error). Worth an ear pass.
- **6 extra recorded voice lines, not wired:** `block-move`, `chip-placed`, `match-start`, `player-ready`,
  `tap-to-start`, `thanks-for-playing` — each needs its own trigger; design separately if wanted.
- **Difficulty default** is Amateur; the user wanted to "learn on the go" — if it still feels too hard, tune
  `CpuStrategy.DepthFor` / the eval, or default lower.
- The mobile board chrome budget is the single magic number `200px` in `mobile.css` (`.mob-board-area > *`).

## Quick map of the new code
- `Core/Narration/` — `CpuTauntPolicy`, `VoicePicker` (pure, tested).
- `Core/Ai/CpuStrategy.cs` — `DepthFor` ladder.
- `Web/Services/` — `ViewportService` (+`IViewportService`), `NarratorService` (single voice-queue owner),
  `CpuTauntLines`, `CpuLevelLabels`, `AudioKeys`.
- `Web/Components/Game/Mobile/` — `MobileScoreboard`, `ActionSheet`.
- `Web/Components/Setup/` — `CpuLevelSelector`, `StarterToggle` (+ existing pickers).
- `Web/Components/Screens/` — `MobileGameView`, `MobilePlayerSetup`, `DesktopGameView`, `DesktopPlayerSetup`.
- `Web/State/GameSession.cs` — SSOT: streak state, events (`MatchEnded`/`IdleNudged`/`RoundStarted`),
  `CpuLevel`/`CpuStarts`/`NarratorTone`, `RunCpuTurn`/`StartTurnFlow`.
- `wwwroot/css/mobile.css`, `build.sh` (version stamp).
