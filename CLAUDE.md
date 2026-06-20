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

## Status (update as you go)
MVP complete (tag `v0.1.0-mvp`) + post-MVP polish: leaner audio, global button click, minimax CPU,
full-screen draw screen, random win cheers, Cloudflare auto-deploy. All 36 Core tests green.
