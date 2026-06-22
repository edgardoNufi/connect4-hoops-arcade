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
  **Landscape (phone rotated):** `MobileGameView` branches on `IViewportService.IsLandscape` (re-renders on
  `OnViewportChanged`) — portrait is unchanged; landscape uses a `.mob-game.landscape` row layout: a **narrow left
  column** (stacked `MobileScoreboard` + narrator + direct Reiniciar/Rendirse/Ajustes buttons, no ActionSheet) and the
  **board maximized** on the right (height-driven, keeps 7:6). Same components/`GameSession`, no logic dup.
- **Fullscreen button (`Components/Shared/FullscreenButton.razor`)** mounted once in `AppShell` → shows on every screen.
  Uses `IFullscreenService` (→ `window.ArcadeFullscreen`) which calls the Fullscreen API where supported
  (Android/desktop/iPad) and syncs the icon via `fullscreenchange`. **iPhone Safari has no Fullscreen API** — there the
  button shows an "Agregar a inicio" hint, and the PWA metadata (`manifest.webmanifest` + apple-* meta in `index.html`,
  `display:standalone`) makes the home-screen-installed app run without the Safari bar (real fullscreen).

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
- **Element pooling (do NOT go back to clone-per-play):** each sound reuses a small ring of `<audio>` elements
  (`getEl`, `POOL_MAX=3` per key, round-robin) instead of `cloneNode()`-ing a fresh element every play. Cloning
  per play accumulated elements that **iOS Safari frees poorly → multi-second main-thread freezes** that grew over a
  session (diagnosed on device). Total elements are now bounded and reused. Tradeoff: a *same* sound fired in rapid
  succession restarts instead of self-overlapping (fine for these SFX; bump `POOL_MAX` if needed).
- **Stop audio on round/exit:** `GameSession` raises `AudioStopRequested` on `ResetState` (rematch/reset/begin) and
  on `GoSplash`/`GoMode`/`ChangePlayers`; `NarratorService` → `IAudioService.StopAllAsync()` → `ArcadeAudio.stopAll()`
  pauses+releases every live element and clears the voice queue/deferred SFX. Without this, a ~15s victory line kept
  playing under the next game and new voices piled up behind it.

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
centre control). Difficulty is a **6-level ladder** `CpuDifficulty { Novato, Principiante, Amateur, Titular,
Estrella, MVP }` mapped to search depth via `DepthFor` (Novato = 0 = loose: takes obvious wins, blocks ~50%,
else random; 1..5 = minimax depth). Always takes an immediate win at every level. The level is a **persisted
setting** (`GameSettings.CpuLevel`, default **Amateur**) pushed to `GameSession.CpuLevel` by
`SettingsStore.ApplyAsync`, chosen on the **setup screen** via the shared `CpuLevelSelector` stepper
(desktop: inside the CPU card at the name slot; mobile: a yellow box above PLAY; **1P only**). Not in Settings.
Note: the eval already penalises the opponent's open-3s, so even depth-1 tends to block obvious threats —
the low levels are weak by *shallow lookahead* (miss traps/forced lines), not by ignoring blocks.
**Who starts (1P):** persisted `GameSettings.CpuStarts` (default = you) → `GameSession.CpuStarts`; `ResetState`
sets `Current = CpuStarts ? 1 : 0`, and `StartTurnFlow()` (called from BeginGame/Rematch/ResetBoard) runs the
CPU's first move via the extracted `RunCpuTurn()` when the CPU leads. UI: a `StarterToggle` (Tú/CPU) next to
the level selector (same desktop/mobile spots), 1P only.

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
**Build version tag:** `build.sh` stamps `__BUILD_VERSION__` (→ `${CF_PAGES_COMMIT_SHA:0:7} · <UTC date>`,
fallback git short SHA / `local`) into a JS global in `index.html` (`window.__build` + `window.getBuild()`).
Shown **only on the splash screen**, dim (`AttractMode` reads `getBuild` via interop; `dev` locally). It used
to be an always-on fixed-corner `.build-tag` but that overlapped every screen — moved to splash-only on the
user's request. Check the splash corner to tell which build is live.
**MUST stamp BEFORE publish** (see Offline below) — it edits `index.html`, which is integrity-checked by the SW.

### Offline support (installable PWA — works with no internet after a one-time wifi load)
Blazor's built-in service worker: `wwwroot/service-worker.js` (dev no-op) + `service-worker.published.js`
(precache-all, cache-first). `.csproj` has `<ServiceWorkerAssetsManifest>` + `<ServiceWorker .../>`. Registered
in `index.html` with toasts (`✓ Listo para jugar sin conexión` on first install; update toast otherwise — the
green one only shows on a truly first install, so a device that already cached it won't see it again). Hard-won
gotchas (do NOT regress):
- **Stamp `index.html` BEFORE `dotnet publish`** (build.sh edits the SOURCE, not the published copy). The SW
  precaches `index.html` with an integrity hash generated at publish; stamping the published file afterwards
  invalidates that hash → `cache.addAll` rejects → SW install fails silently in prod (no offline, no toast).
- **Precache audio + fonts:** `offlineAssetsInclude` is extended with `.mp3`, `.m4a`, `.woff2` (the template
  omits them → game would open offline with no sound and broken fonts).
- **Clean redirected responses:** Cloudflare rewrites `/index.html → /`, so the SW caches a `redirected:true`
  response; serving it to a navigation throws "a redirected response was used for a request whose redirect mode
  is not follow" → **whole site ERR_FAILED** once the SW controls the page. `onFetch` rebuilds any redirected
  cached response as a clean `Response` (`cleanIfRedirected`).
- `onInstall`/`onActivate` call `skipWaiting()`/`clients.claim()` so a fixed worker can replace a broken one.
- Recover a broken SW: hard-reload ×2, or unregister in DevTools / clear site data (desktop) / delete the
  home-screen app + clear Safari data (iPhone).

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
mobile-first responsive redesign (dedicated mobile views via `IViewportService`),
6-level CPU difficulty selector on the setup screen, **"who starts" toggle (1P)**,
**phone-landscape layout (maximized board + left column), audio perf fix (pooled `<audio>` + stop-on-round —
cured the iOS multi-second freezes), installable PWA that works fully offline (verified on device), fullscreen
in Settings, version-on-splash, a UX cleanup batch (click sfx, mobile 1P CPU customization, in-game Inicio
button), and the PRACTICE / tutorial sandbox**. All 59 Core tests green (51 + 8 for practice).
**Practice/tutorial mode:** entered from the mode screen (🎓 Práctica) → 1P-vs-CPU sandbox with full-turn
**undo/redo to the empty board** (undo is practice-ONLY, never the normal game), live CPU-level selector, **soft
win** (banner, no scores, no Victory screen — undo still works), a one-time intro card, and an optional Hints
toggle (highlights your immediate win + a CPU block). Pure `Core` helpers carry the logic, TDD'd:
`Core/Practice/BoardReplay.FromColumns` (board from a column list) + `Core/Practice/MoveLog` (undo-turn/redo
bookkeeping) + `Rules/ThreatScanner.FindWinningColumn` (hints). `GameSession` has a `Practice` flag + `MoveLog`;
practice runs on `AppScreen.Game` with `Practice==true`, routed by `AppShell` to `PracticeView`. Spec/plan:
[`docs/superpowers/specs/2026-06-22-practice-tutorial-design.md`](docs/superpowers/specs/2026-06-22-practice-tutorial-design.md) ·
[`docs/superpowers/plans/2026-06-22-practice-tutorial.md`](docs/superpowers/plans/2026-06-22-practice-tutorial.md).
**Next focus: item 2 (cast / big-screen projection) or item 4 (ESP32 sensor) — user's call.**
Landscape view + fullscreen design/plan: [`docs/superpowers/specs/2026-06-20-landscape-view-fullscreen-design.md`](docs/superpowers/specs/2026-06-20-landscape-view-fullscreen-design.md) · [`docs/superpowers/plans/2026-06-20-landscape-view-fullscreen.md`](docs/superpowers/plans/2026-06-20-landscape-view-fullscreen.md).
**Continuing in a new session?** Read [`docs/superpowers/2026-06-19-session-handoff.md`](docs/superpowers/2026-06-19-session-handoff.md) — what shipped, how we work, pending roadmap, loose ends.
**Note:** CPU-taunt voice files are produced separately (spec §5.1); until they land, taunt paths are silent
(harmless — `AudioService` swallows missing-file errors). Manual ear-verification pending the audio files.
