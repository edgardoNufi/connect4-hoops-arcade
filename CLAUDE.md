# Connect 4 Hoops Arcade — Project Vault (read me first)

This file is auto-loaded every session. It captures the architecture, conventions, and **decisions
already made** so work stays consistent and nothing agreed-upon gets undone or redone. When something
material changes, **update this file** (it's the single source of truth for "how we work here").

> Authoritative deeper docs: [`docs/superpowers/specs/2026-06-18-connect4-hoops-arcade-design.md`](docs/superpowers/specs/2026-06-18-connect4-hoops-arcade-design.md)
> (design spec) and [`docs/superpowers/plans/2026-06-18-connect4-hoops-arcade.md`](docs/superpowers/plans/2026-06-18-connect4-hoops-arcade.md)
> (implementation plan). The imported visual source of truth is
> [`docs/superpowers/specs/connect4-design-source.html`](docs/superpowers/specs/connect4-design-source.html).

## What this is
A full-screen arcade Connect 4 ("Hoops" basketball theme) in **Blazor WebAssembly (.NET 10)**, a faithful
port of an imported Claude Design. Built to run with or without a physical sensor board. Repo:
`https://github.com/edgardoNufi/connect4-hoops-arcade` (branch `main`). Language of the UI: **Spanish**.

## Solution layout (3 projects, dependency direction `Web → Core`)
```
src/Connect4HoopsArcade.Core/        Pure domain. NO Blazor/JS/IO deps. Fully unit-tested.
src/Connect4HoopsArcade.Web/         Blazor WASM: state, interop services, Razor components.
tests/Connect4HoopsArcade.Core.Tests/ xUnit (36 tests). TDD lives here.
```
Solution file is `Connect4HoopsArcade.slnx` (new XML format — `.slnx`, not `.sln`).

## Commands
- Build: `dotnet build` · Test: `dotnet test` · Run: `dotnet run --project src/Connect4HoopsArcade.Web`
- Publish (what Cloudflare does): `dotnet publish src/Connect4HoopsArcade.Web -c Release -o output` → site in `output/wwwroot`.

## Architecture rules (do not violate)
- **`Core` stays pure** — only the .NET BCL. No Blazor, no `IJSRuntime`, no web concerns. It's the testable
  heart (Board, WinDetector, ThreatScanner, PlayValidator, CpuStrategy, catalogs). Add rules here + TDD them.
- **All `IJSRuntime` is isolated behind `Services/`** (AudioService, SettingsStore, KeyboardInputService,
  SensorConnectionService). Components/state depend on the `Services/Abstractions/` interfaces, not JS.
- **`GameSession` (Web/State) is the single source of truth.** It raises `StateChanged` (+ audio events).
  Game flow/timing/orchestration lives here; pure rules are delegated to `Core`.
- **Thin `.razor`** — components render a view-model and raise callbacks. No game rules in markup.
- **One move pipeline:** every input (click, keyboard, sensor) goes `IMoveSource → MoveRouter → GameSession.TryDrop`.
  Never call `GameSession.TryDrop` directly from a component — go through `MoveRouter` (it gates by screen,
  play-mode, and debounce).
- **Responsive: dedicated mobile views.** `IViewportService` (JS interop in `Services/`, `window.ArcadeViewport`)
  exposes size/breakpoint/orientation + `OnViewportChanged`. `AppShell` renders `MobileGameView`/
  `MobilePlayerSetup` on phones (`IsMobile`) and `DesktopGameView`/`DesktopPlayerSetup` otherwise — **same
  `GameSession`/`MoveRouter`/`Core`, no duplicated game logic** (mobile tap-to-drop reuses `GameColumn`→`MoveRouter`).
  Mobile board = `BoardGrid FitContainer="true"` (square cells, `aspect-ratio:1`, fit to screen). Phones never get
  the "rotate your device" wall. Mobile layout/styles live in `wwwroot/css/mobile.css` (`100dvh` + `env(safe-area-inset-*)`).
  Mobile in-game = top tap-bar scoreboard (opens an `ActionSheet` with Reiniciar/Rendirse/Ajustes) + centered board + narrator.

## Key patterns / gotchas (learned the hard way)
- **The board type is `GameBoard`, NOT `Board`.** A class named `Board` inside namespace `...Core.Board`
  collides with the namespace (CS0118). Do not rename it back. Board is `grid[col, row]`, 7×6, **row 0 = bottom**.
- **Gameplay components inherit `SessionComponentBase`** (Web/Components) — it injects `Session` and
  subscribes to `StateChanged` so each re-renders on every change. AppShell's cascade alone left sibling
  components (panels, narrator) stale. New in-game components MUST inherit it (or they won't update).
- **Two "mode" concepts, kept separate:** `GameSession.Mode` = `GameMode` (OnePlayer/TwoPlayer);
  `GameSession.Mode2` = `PlayMode` (Digital/Physical). Don't merge them.
- **Digital vs Physical:** Digital = on-screen clicks + keyboard; Physical = sensor board authoritative,
  on-screen clicks disabled (keyboard 1–7 still simulates). `MoveRouter.OriginAllowed` + `Screen==Game`
  guard enforce this. No real hardware yet — `SensorConnectionService` is simulated/stubbed.
- **Pacing:** after a chip drops, `GameSession` waits the drop-animation duration (`DropMs`) before passing
  the turn, so play feels fluid. `IsBusy` is true during the fall (blocks input + disables top-bar buttons).
- **Avatars/tokens are drawn SVG** (`Components/Shared/AvatarSvg.razor`), not emoji — disc + highlight + rim
  + 5 faces + 7 accessories, path-by-path from the design source.
- **Visual design fidelity:** match the imported Claude Design (inline styles, Fredoka/Nunito self-hosted
  fonts, exact palette). Don't swap in generic/Bootstrap aesthetics.

## Audio design — CURRENT state (user tuned this carefully; do NOT revert without asking)
Engine: `wwwroot/js/arcade.js` → `window.ArcadeAudio`. SFX play immediately; **voices are a serial queue**
(one at a time, newest pending replaces older → no overlap, no pile-up). `playSfxAfterVoice` defers a sound
until voices finish. A **global click listener** plays `ui/button-click.mp3` on every menu/setup `<button>`
(excludes `.game-screen`). `NarratorService` maps `GameSession` events → sounds.

Deliberate choices the user made (keep unless they say otherwise):
- **No background music.** (`music/attract-loop.mp3` exists but is unused; the "Música" settings slider
  currently controls nothing.)
- **Removed voices:** welcome, "elige modo de juego", "elige personaje", and the "¡Conecta 4!" voice.
- **Threat (3-in-a-row):** almost-win **voice** variants only — the danger **SFX was removed**.
- **Victory:** one random `VictoryV` line (winner/victory variants) → then a **random win cheer**
  (`win-01.mp3` / `win-02.m4a`) AFTER the voice finishes. `AudioKeys.WinSfx` is an **array** (add files to
  rotate). `.m4a` is fine (served as `audio/mp4`).
- **Kept SFX:** chip-drop, turn-change, column-full. Turn voice ~40% of turns; "great move" ~1/8.
- **CPU taunts + streaks + `NarratorTone`:** the announcer taunts with streak-aware escalation. `GameSession`
  tracks a **generic** win streak for the current leader in BOTH modes (`StreakHolder`/`WinStreak`/
  `BrokenStreakLength`); `CpuWinStreak` is derived from it and feeds the 1P taunt level (`CpuTauntPolicy` in
  `Core/Narration`: levels Neutral/Light/Confident/Boss; `AdvanceStreak` is the pure transition, TDD'd).
  `NarratorTone` (Familiar default / Picante / Silencioso) is a persisted `SettingsStore` pref: Familiar caps
  at Confident, Silencioso mutes all voice (SFX stay). Mid-game taunts (1P) cooldown-gated (25s); `cpu-threat`
  outranks `cpu-idle` (idle ≤1/round, never blocks a later threat). **Closing lines by broken-streak size**
  (`OnMatchEnded`): broke ≥3 → `streak-break-big` in ANY mode + the `game/streak-break.mp3` "aleluya" sting
  after the voice (replaces the cheer); 1P broke 2 → `streak-break`; 1P no break → `beat-cpu`; 2P no big break →
  neutral `VictoryV`; CPU win (1P) → `cpu-win`, no cheer, rotated `loss-sting`. `GameSession` raises
  `MatchEnded`/`IdleNudged`/`RoundStarted`; `ThreatRaised` carries the mover index; `Won`/`Drew` retained but
  unused for audio. Voice files + addendum: spec `2026-06-19-cpu-taunts-announcer-design.md` §5.1 + §8.
  Extra recorded-but-unwired lines (block-move, chip-placed, match-start, player-ready, tap-to-start,
  thanks-for-playing) are deferred — each needs its own trigger.

> NOTE: threat detection (`ThreatScanner.HasImmediateThreat`) is **verified correct** — it does NOT fire on
> blocked/capped/unreachable 3-in-a-rows (regression tests prove it). Don't "fix" it.

## CPU AI
`Core/Ai/CpuStrategy.cs` — **minimax + alpha-beta** with a window-based evaluation (4-in-a-row potential +
centre control). Depth: **Sharp 5, Normal 4** (Sharp is the default `GameSession.CpuLevel`); **Chill** is
deliberately weak (beatable). Always takes an immediate win; the search blocks threats and never hands a
free win. There is no in-UI difficulty selector yet (possible future add).

## Conventions
- PascalCase types/methods, `I`-prefixed interfaces, file name == type name. English code identifiers;
  Spanish user-facing strings (centralized in `NarratorService` / component markup).
- **TDD the `Core`** (rules are pure → write the failing test first). UI uses build-and-verify.
- Commits: conventional style (`feat`/`fix`/`chore`/`test`/`docs(scope): …`). Co-author footer per global rules.
- **The user reviews audio/feel by ear and iterates** — don't finalize/commit audio changes as "done"
  until they confirm; when deploying, commit the current state.
- `.gitattributes` forces **LF** on `*.sh` (Cloudflare's Linux build breaks on CRLF). Keep it.

## Deployment (Cloudflare Pages, auto-deploy on push to `main`)
`build.sh` (repo root, executable, LF) installs the .NET 10 SDK then `dotnet publish -o output`.
Cloudflare settings: Framework preset **None**, Build command **`./build.sh`**, Output dir **`output/wwwroot`**,
Root **`/`**, no env vars needed. If `.m4a` doesn't play in prod, add a `_headers` to force `audio/mp4`.

## Testing in a browser (heads-up)
Driving the app via the automation browser, **WASM hydration can take >6–7s** and the first scripted click
often lands before hydration (stays on splash). Re-click after it's loaded. A human's focused browser
hydrates in ~2–3s and is fine. The console line "Debugging hotkey: Shift+Alt+D" is normal; Chrome-extension
"message channel closed" errors are noise, not app errors.

## Roadmap / agreed pending work
Ordered by the user's priority. Brainstorm/design before building each (see brainstorming skill).

1. **Mobile-first responsive redesign (TOP PRIORITY).** The phone IS the primary device — the app must be
   fully playable and good-looking on phones. Tested on an **iPhone 17 Pro Max: not adaptive** — the board
   doesn't fit, top/bottom get cut off, and the current `.rotate-hint` just forces landscape instead of
   adapting. Desktop browser is great; phone is not. Rework the layout so it adapts to phone viewports
   (portrait-playable, not a "rotate your device" wall). This also makes screen-mirroring to a TV look right.

2. **Cast / big-screen projection.** Goal: open on the phone and have the GAME fill a TV like a video app
   (phone as controller), not just mirror the small portrait phone screen.
   **Honest constraint:** iOS Safari AirPlay can only send `<video>/<audio>` elements to AirPlay — there is
   **no web API to AirPlay an arbitrary app UI** as an independent display. So today AirPlay just mirrors the
   phone. Realistic paths to evaluate:
   - (a) **Excellent responsive layout + Fullscreen API + orientation handling** so the mirrored view looks
     good on the TV (cheapest; depends on item 1).
   - (b) **Google Cast / Presentation API** (Chrome + Chromecast) for a true second screen — not AirPlay/Safari.
   - (c) **Companion model:** the TV browser runs the game *display*; the phone is a *controller* that sends
     moves over WebSocket. This **reuses the existing `IMoveSource`/sensor abstraction** — a phone controller
     is just another move source, like the ESP32 would be. Likely the most robust route for the user's vision.
   Decide the approach in a dedicated brainstorming session before building.

3. **Difficulty selector UI** (Chill / Normal / Sharp) in Settings — the AI already supports all three.
4. **Physical sensor (ESP32 / WebSocket) integration** — `IMoveSource`/`SensorConnectionService` are ready;
   only the real transport is stubbed.
5. (Optional) background-music toggle / re-enable, and theme switching — both deferred earlier on purpose.

## Status (update as you go)
MVP complete (tag `v0.1.0-mvp`) + post-MVP polish: leaner audio, global button click, minimax CPU,
full-screen draw screen, random win cheers, Cloudflare auto-deploy, streak-aware CPU taunts + `NarratorTone`,
**mobile-first responsive redesign (dedicated mobile views via `IViewportService`)**.
All 51 Core tests green.
**Next focus: item 2 (cast / big-screen projection — companion model reusing `IMoveSource`).**
**Note:** CPU-taunt voice files are produced separately (spec §5.1); until they land, taunt paths are silent
(harmless — `AudioService` swallows missing-file errors). Manual ear-verification pending the audio files.
