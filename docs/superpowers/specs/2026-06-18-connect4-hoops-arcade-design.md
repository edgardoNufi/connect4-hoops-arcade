# Connect 4 Hoops Arcade — Design Spec
**Date:** 2026-06-18
**Tech:** Blazor WebAssembly (.NET 10), custom CSS animations, JS interop for audio/storage/keyboard
**Fonts:** Fredoka + Nunito, **self-hosted** under `wwwroot/fonts/` (offline-capable for an arcade cabinet)
**Authoritative visual source:** [`connect4-design-source.html`](./connect4-design-source.html) (imported from Claude Design)

> This spec is a faithful port of the Claude Design component to Blazor WASM. The design is a React-style
> `DCLogic` component with `{{ }}` bindings, `<sc-if>` (conditional) and `<sc-for>` (loop) directives.
> All colors, fonts, gradients, animations, and game logic below are extracted **verbatim** from that source.

---

## 1. Project Summary

A full-screen arcade-style Connect 4 game that feels like a physical hoops machine: SVG-drawn character
tokens, narrator bubble, SFX/voice/music, 7×6 board, 1-player (vs CPU) and 2-player local modes, keyboard
input (keys 1–7) to simulate physical column sensors, plus a sensor diagnostic screen and settings.

---

## 2. Design Tokens (exact values from source)

### Fonts (Google Fonts)
- **Display / headings:** `'Fredoka'` weights 400/500/600/700
- **Body / UI:** `'Nunito'` weights 600/700/800/900
- **Self-hosted**: download `.woff2` files to `wwwroot/fonts/` and declare via `@font-face` in `app.css`
  (Fredoka 400/500/600/700, Nunito 600/700/800/900). No external CDN dependency — works offline.
- Root: `.arc-root { font-family: 'Nunito', system-ui, sans-serif; }`

### Color Palette (the 8 selectable token colors — id, hex, name, hue)
| id | hex | name | hue |
|------|-----------|----------|-----|
| pink | `#ff2d6f` | Rosa | 340 |
| cyan | `#22d3ee` | Cian | 190 |
| yellow | `#ffd23f` | Amarillo | 48 |
| green | `#2ee86e` | Verde | 140 |
| orange | `#ff8a00` | Naranja | 32 |
| purple | `#b14bff` | Morado | 275 |
| blue | `#3b82f6` | Azul | 217 |
| red | `#ff3b3b` | Rojo | 0 |

Hue values drive the "colors too similar" validation (`hueDist < 30`).

### Structural colors
| Purpose | Value |
|---------|-------|
| Page background | `radial-gradient(120% 90% at 50% 0%, #211a4d 0%, #120e2e 45%, #08060f 100%)` |
| Board surface | `linear-gradient(180deg, #2546ff, #1a30c4)` |
| Board shadow | `0 0 0 4px #16259e, 0 16px 0 #0f1a78, 0 24px 44px rgba(0,0,0,.5), inset 0 0 30px rgba(255,255,255,.12)` |
| Cell hole | `radial-gradient(circle at 38% 34%, #0a0820, #05040f)` + `inset 0 4px 10px rgba(0,0,0,.85), inset 0 -2px 4px rgba(255,255,255,.05)` |
| Ink (face/stroke) | `#1a1030` |
| Accent cyan (1P / info) | `#22d3ee` |
| Accent pink (2P / warnings) | `#ff2d6f` |
| Accent yellow (CTA / scores hl) | `#ffd23f` |
| CTA gradient | `linear-gradient(180deg, #ffd23f, #f5a700)` + shadow `0 7px 0 #c98800` |

### Decorative backdrop
Three blurred radial glow blobs (pink top-left, cyan bottom-right, yellow center) animated with
`glowPulse`, plus two thin white "court arc" circles and a faint center line — all `pointer-events:none`.

### Title styling
- "CONNECT 4": Fredoka 700, `#ffd23f`, `text-shadow: 0 6px 0 #c98800, 0 0 34px rgba(255,210,63,.55)`, `-webkit-text-stroke: 3px #1a1030`
- "HOOPS": `#ff2d6f`, `text-shadow: 0 5px 0 #a3134a, 0 0 32px rgba(255,45,111,.55)`

### Sizing (responsive clamps, verbatim)
- Cell + arrow width: `clamp(46px, 9.4vh, 104px)`; cell height same
- Board cell gap: `clamp(5px, 1.1vw, 14px)`
- Player avatar (game): `clamp(76px, 9vw, 128px)`
- Score number: `clamp(50px, 6.5vw, 92px)`

---

## 3. SVG Avatar / Token (the `avatar(opts)` builder — must be reproduced exactly)

Tokens are **drawn SVG**, viewBox `0 0 110 120`. Layered:
1. **Disc** `circle cx55 cy62 r50` fill = player hex, stroke `rgba(0,0,0,.28)` w4
2. **Highlight** `ellipse cx40 cy44 rx22 ry13` fill `rgba(255,255,255,.32)`
3. **Rim** `circle cx55 cy62 r46` stroke `rgba(255,255,255,.45)` w2.5
4. **Face** (only when `tokenStyle != 'classic'`) — stroke = ink `#1a1030`, w5, round caps:
   - `happy`: two dot eyes (r5 @40,56 & 70,56) + smile `M40 74 Q55 90 70 74`
   - `confident`: raised-brow eyes `M34 58 Q40 52 46 58` / `M64 58 Q70 52 76 58` + smirk `M42 78 Q58 86 72 70`
   - `serious`: small dot eyes (r4.5) + flat mouth `M42 80 H68`
   - `surprised`: big dot eyes (r7 @y55) + open mouth `ellipse cx55 cy80 rx8 ry10`
   - `angry`: angled brows `M30 46 L48 53` / `M80 46 L62 53` + small eyes (r4) + frown `M42 82 Q55 74 68 82`
5. **Accessory** (overlays):
   - `none`: nothing
   - `glasses`: two `circle r13` lens @40,56 & 70,56 fill `rgba(255,255,255,.18)` stroke ink w4 + bridge `M53 56 H57`
   - `cap`: dome `M14 40 Q55 -6 96 40 Z` fill `#1f4fff` + brim `ellipse cx38 cy41 rx32 ry8` `#143bd6` + button `circle cx55 cy14 r4` `#0d2aa0`
   - `headband`: band `M10 44 Q55 30 100 44 L100 56 Q55 42 10 56 Z` fill `#ff2d6f` + highlight stroke
   - `crown`: `M28 36 L36 10 L48 28 L55 6 L62 28 L74 10 L82 36 Z` fill `#ffd23f` stroke `#d39a00` + gem `circle cx55 cy22 r3.5` `#ff2d6f`
   - `bowtie`: two triangles around `cx55 cy104` fill `#ff2d6f` + center `circle r4` `#c01250`
   - `earrings`: two `circle r5` @8,74 & 102,74 fill `#ffd23f` stroke `#d39a00`
6. Optional `glow`: wrapper `filter: drop-shadow(0 0 12px <hex>)`

**Faces list (id/label):** happy/Feliz, confident/Confiado, serious/Serio, surprised/Sorprendido, angry/Enojado
**Accessories (id/label):** none/Ninguno, glasses/Lentes, cap/Gorra, headband/Banda, crown/Corona, bowtie/Moño, earrings/Aretes

The source had a `tokenStyle` prop (`character` | `classic`) toggling whether faces are drawn. This phase
ships the default **`character`** (faces drawn); it is a single constant, not exposed in the UI.

### Basketball SVG (splash)
Orange `circle r46` `#ff8a00` stroke ink w4 + vertical/horizontal lines + two curved seams + highlight ellipse.

---

## 4. Screens (8 `<sc-if>` branches) — exact layout

State key `screen` ∈ `splash | mode | setup | game | victory | draw | sensors | settings`.

1. **Splash** — full-screen click target. Three floating avatar tokens (`floatY`), bouncing basketball
   (`ballBounce`), "CONNECT 4 / HOOPS" title, pulsing "TOCA PARA COMENZAR" pill (cyan border, `blink`),
   footer "ARCADE EDITION · 7×6 · 2 JUGADORES". Click → mode.
2. **Mode** (`slideUp` in) — "ELIGE MODO DE JUEGO" + two cards: **1 JUGADOR** (cyan border, glasses avatar,
   "vs. CPU") and **2 JUGADORES** (pink border, two avatars, "Local · 1 vs 1"). Hover lifts card. Bottom:
   "⚡ Prueba de sensores", "⚙ Configuración". Top-left "‹ Inicio".
3. **Setup** — header "PERSONALIZA TU FICHA" + back. Two player cards (border = player color, soft glow):
   avatar preview + name input (maxlength 12) + COLOR row (8 swatches; taken color shows 🔒 + dimmed +
   disabled, selected shows white ring) + CARA row (5 face buttons) + ACCESORIO row (7). Color-warning pill
   if same/similar color. "¡JUGAR! ▶" CTA (disabled while warning). In 1P, P2 card is the CPU (name locked).
4. **Game** — see §5.
5. **Victory** (`pop`) — radial wash in winner color, 70-piece confetti (`confettiFall`), title
   "¡CONECTA 4!" or "¡VICTORIA!" (on resign), winner avatar (drop-shadow), "¡Ganó {name}!", scoreboard,
   buttons: 🔄 Revancha / 👥 Cambiar jugadores / 🏠 Inicio.
6. **Draw** (`pop`) — cyan-bordered card, both avatars overlapped, "¡EMPATE!", flavor text, Revancha / Inicio.
7. **Sensors** — header + connection status pill (green CONECTADO / red DESCONECTADO). 7 vertical bars
   (`COLUMNAS 1–7 · TOCA PARA SIMULAR SEÑAL`); pressing lights the bar cyan for 650ms. "Último sensor
   detectado: {n}". Buttons: simulate disconnect/reconnect, Reiniciar.
8. **Settings** (`slideUp`) — header + back. Volume sliders Música/Efectos/Narrador (0–100, pink accent),
   "Voces del narrador" toggle (green when on), "Velocidad de animación" Normal/Rápida, **"Modo de juego"
   Digital/Físico** (with a live sensor-connection indicator; reflects autodetection, persists choice),
   plus "Probar sonido" and "Prueba de sensores →" buttons (per acceptance criteria). **No theme selector
   this phase** — the shipped look is the imported "classic" view. (Source had a theme swatch row; omitted.)

### Game screen layout (§5)
`[P1 panel] [arrows + board + narrator] [P2 panel]`, full bleed, landscape-first.
- **Top control bar** (centered): ⚙ Ajustes / 🔄 Reiniciar / 🏳️ Rendirse
- **Player panel**: avatar, name, turn badge (`● TU TURNO` / `EN ESPERA` / `⏳ PENSANDO…` / `👉 ¡TE TOCA!`),
  divider, "PUNTOS", score number. Active panel: colored border + glow + `turnGlow` pulse, opacity 1;
  inactive: opacity 0.42. Panel bg active = `linear-gradient(180deg, <hex>33, rgba(255,255,255,.02))`.
- **Arrows**: 7 `▼` buttons above board in current player's color; hover nudges down; `arrowBounce` when idle.
- **Board**: blue rounded rect, 7 columns each a flex column of 6 cells (rendered top→bottom, r=5..0).
  Column hover/error: pink glow + `shake`. Winning board: `boardWin` pulse. Win banner overlay
  ("¡CONECTA 4!"/"¡VICTORIA!") with `connectBadge`.
- **Narrator bubble**: 🎙️ + text, border/glow in current color, `pop` on change.
- **Rotate hint** (portrait ≤900px): hide board, show spinning 📱 "Gira tu dispositivo".

---

## 5. Game Logic (ported verbatim from source)

### Board representation
`grid[col][row]`, **7 columns × 6 rows**, value `null | 0 | 1` (player index). `row 0 = bottom`.
- `lowestRow(grid, c)`: first `r` from 0..5 where cell is null; `-1` if full.
- `isFull`: every column's top cell (`col[5]`) filled.

### Win detection
`winLine(grid, c, r, p)` checks 4 directions `[1,0],[0,1],[1,1],[1,-1]`; for each builds the contiguous
line through (c,r) in both directions; if length ≥ 4 returns the 4 winning cells (stored as `{ "c-r": true }`).

### Move flow (`place(col)`)
1. `lowestRow < 0` → set `errorCol`, narrator "¡Columna llena! Prueba otra. 🚫", clear after 700ms, **no move**.
2. Place token, check `winLine`:
   - **Win** → mark winning cells, +1 score, `winner=current`, `winBy='connect'`, narrator
     "¡CONECTA 4! ¡Gana {name}!"; confetti at 700ms; switch to victory screen at 2700ms.
   - **Full board** → narrator "¡Tablero lleno! Es un empate. 🤝"; draw screen at 850ms.
   - **Otherwise** → switch `current`, narrator via `turnPhrase`. In 1P with next=CPU, set `thinking`,
     CPU moves after 750ms.
- `lastDrop = {col,row}` drives the `drop` fall animation on that one cell.

### Narrator phrases (`turnPhrase`)
Random of: "Turno de {n}" / "¡Buena jugada! Ahora va {n}" / "Vamos {n}, tú puedes 🏀" /
"Cuidado {n}, {opp} va fuerte". If opponent has an immediate winning threat →
"¡Cuidado {n}, hay tres en línea! 😱".

### Idle nudge
9s of inactivity (human turn) → `idle=true`, narrator "¿Sigues ahí, {n}? ¡Es tu turno! 🏀",
arrows start `arrowBounce`, badge → "👉 ¡TE TOCA!".

### CPU AI (`cpuMove`, difficulty `chill | normal | sharp`)
1. Win if possible (`tryWin(1)`).
2. Block opponent win (`tryWin(0)`) unless `chill`.
3. Else center-out column preference: sharp `[3,2,4,1,5,0,6]`, normal `[3,4,2,5,1,6,0]`;
   `chill` picks random available, others pick first available.

### Other actions
- `resetBoard` / `rematch`: clear grid, current=0, narrator reset. `resign`: opponent wins (`winBy='resign'`,
  banner "¡VICTORIA!"), +1 score, confetti, victory. `changePlayers` → setup.
- Confetti: 70 pieces, colors `[#ff2d6f,#22d3ee,#ffd23f,#2ee86e,#b14bff,#ff8a00,#ffffff]`, random
  left/size/dur/delay, square or round.

### Default players
P1 **"Jugador 1"** (red, happy, cap), P2 **"Jugador 2"** (yellow, confident, crown). 1P → P2 becomes
**"CPU"** (yellow, serious, none, locked input). Names are editable; colors default red/yellow but are
configurable. (Differs from the imported source, which seeded "Edgar"/"Sofía" — generic names per user.)

### Color validation (`colorWarnState`, 2P only)
- Same color → "Mismo color: elige uno distinto para cada jugador."
- `hueDist < 30` → "Colores muy parecidos: podrían confundirse en el tablero."
- Warning disables ¡JUGAR! and dims it.

---

## 6. Animations (CSS `@keyframes` — copy verbatim from source)
`drop` (gravity fall with squash/bounce, translateY -580px→0), `land` (impact ring scale .25→2 fade),
`connectBadge`, `boardWin`, `shake`, `floatY`, `ballBounce`, `glowPulse`, `winPulse`, `confettiFall`,
`pop`, `slideUp`, `blink`, `scan`, `spin`, `turnGlow`, `arrowBounce`, `badgePulse`.
- **Drop duration** depends on speed setting: normal `0.6s`, fast `0.34s`. Land flash = `dur*0.7` at `dur*0.5` delay.

---

## 7. Architecture (clean / layered)

The imported source is one monolithic React component mixing pure rules, runtime state, presentation, and
timing. The port **separates these into layers** so the rules are pure and unit-testable and the UI never
touches `IJSRuntime` directly. Three projects in one solution:

```
Connect4HoopsArcade.sln
├── src/
│   ├── Connect4HoopsArcade.Core/        ← class library, NO Blazor/JS deps — pure domain, unit-testable
│   └── Connect4HoopsArcade.Web/         ← Blazor WASM app (UI + interop), references Core
└── tests/
    └── Connect4HoopsArcade.Core.Tests/  ← xUnit, references Core (TDD for the rules)
```

### Layer 1 — `Core` (pure domain, no dependencies)
The "what are the rules" layer. Deterministic, no timers, no I/O, no UI.
```
Core/
├── Primitives/
│   ├── Cell.cs            ← enum: Empty, Player1, Player2
│   ├── GameMode.cs        ← enum: OnePlayer, TwoPlayer
│   ├── CpuDifficulty.cs   ← enum: Chill, Normal, Sharp
│   ├── FaceId.cs / AccessoryId.cs  ← enums
│   └── AnimationSpeed.cs  ← enum: Normal, Fast
├── Catalog/
│   ├── TokenColor.cs      ← record (Id, Hex, Name, Hue)
│   ├── ColorCatalog.cs    ← the 8 colors; HueDistance(a,b) helper
│   ├── FaceCatalog.cs     ← 5 faces (id/label)
│   └── AccessoryCatalog.cs← 7 accessories (id/label)
├── Players/
│   └── PlayerConfig.cs    ← Name, ColorId, FaceId, AccessoryId, IsCpu (immutable record + `with`)
├── Board/
│   ├── Board.cs           ← 7×6 grid; LowestRow(col), Drop(col,cell), IsColumnFull, IsBoardFull, Clone
│   └── BoardPosition.cs   ← readonly record struct (Col, Row)
├── Rules/
│   ├── WinDetector.cs     ← FindWinningLine(board,col,row,cell) → IReadOnlyList<BoardPosition>? (4 dirs)
│   ├── ThreatScanner.cs   ← HasImmediateThreat(board, cell)
│   └── PlayValidator.cs   ← color same / hue-similar validation (returns a typed warning)
└── Ai/
    └── CpuStrategy.cs     ← ChooseColumn(board, difficulty): win → block → center-out order
```

### Layer 2 — `Web` (Blazor WASM: state orchestration, interop, UI)
```
Web/
├── wwwroot/
│   ├── audio/                     ← user's files (left in place, served as-is)
│   ├── css/{app.css, board.css}   ← @font-face, design tokens (CSS vars), keyframes, component styles
│   ├── fonts/                     ← self-hosted Fredoka + Nunito .woff2
│   ├── js/arcade.js               ← the ONLY JS; audio + keyboard(1-7) + localStorage + fullscreen
│   └── index.html
├── State/
│   ├── GameSession.cs             ← runtime state: Screen, Mode, Players[2], Board, Current, Winner,
│   │                                 WinningCells, Scores, Narrator, IsThinking, IsIdle, LastDrop, Confetti.
│   │                                 Orchestrates Core + raises StateChanged. Owns move flow & timers.
│   └── AppScreen.cs               ← enum: Splash, Mode, Setup, Game, Victory, Draw, Sensors, Settings
├── Input/                         ← move-source abstraction (digital vs physical)
│   ├── IMoveSource.cs             ← exposes event ColumnTriggered(int col 0-6); Start/Stop
│   ├── MoveRouter.cs             ← single funnel: all sources → GameSession.TryDrop; dedup/debounce
│   ├── ScreenInputSource.cs       ← clicks/taps + keyboard 1-7 (active in Digital mode)
│   └── SensorInputSource.cs       ← sensor/WebSocket events (active in Physical mode); simulated for now
├── Services/
│   ├── Abstractions/              ← interfaces (so components & state depend on contracts, not JS)
│   │   ├── IAudioService.cs
│   │   ├── INarrator.cs
│   │   ├── ISensorConnection.cs   ← connection state + ColumnTriggered for the physical channel
│   │   ├── IKeyboardInput.cs
│   │   └── ISettingsStore.cs
│   ├── AudioService.cs            ← wraps window.ArcadeAudio; per-event cooldowns; never throws on miss
│   ├── NarratorService.cs         ← maps game events → phrase + voice key; throttles voice
│   ├── SensorConnectionService.cs ← connection status + raw column events; drives autodetection
│   ├── KeyboardInputService.cs    ← [JSInvokable] receives 1-7 keydown from arcade.js
│   └── SettingsStore.cs           ← GameSettings load/save via localStorage
├── Models/
│   ├── PlayMode.cs                ← enum: Digital, Physical
│   └── GameSettings.cs            ← MusicVol, SfxVol, NarratorVol, VoicesEnabled, Speed, PlayMode (no theme)
├── Components/
│   ├── App.razor / Layout/AppShell.razor   ← routes on GameSession.Screen
│   ├── Shared/{AvatarSvg.razor, BasketballSvg.razor, ArcadeButton.razor, GlowBackdrop.razor}
│   ├── Screens/{AttractMode, GameModeSelector, PlayerSetup, GameView, SensorTestPanel, SettingsPanel}.razor
│   ├── Setup/{PlayerSetupCard, ColorPicker, FacePicker, AccessoryPicker}.razor
│   ├── Game/{BoardGrid, GameColumn, GameCell, ColumnArrows, PlayerPanel, NarratorBubble, WinBanner}.razor
│   └── Modals/{VictoryModal, DrawModal}.razor
└── Program.cs                     ← DI registration (singletons: GameSession + services)
```

### Conventions / "good development" rules applied
- **Dependency direction:** `Web → Core` only. `Core` references nothing. Tests reference `Core`.
- **Pure vs stateful:** rules (`WinDetector`, `CpuStrategy`, `Board`) are pure & side-effect-free → trivially
  testable. `GameSession` holds the mutable run state and orchestrates; timing/audio/UI live in `Web`.
- **Interop isolation:** all `IJSRuntime` calls sit behind `Services/*`; components and `GameSession`
  depend on the `Abstractions/` interfaces, never on JS directly.
- **Thin `.razor`:** components render a view-model and raise callbacks; no game rules inside markup.
  The source's `renderVals()`/`panelVM()` become small computed view-models in the relevant components.
- **Immutability where natural:** `PlayerConfig`, catalog entries, `BoardPosition` are records; `Board`
  exposes intent-named methods rather than a raw array.
- **One reactive source of truth:** `GameSession.StateChanged` event; components subscribe in
  `OnInitialized`, unsubscribe in `Dispose`, call `StateHasChanged`.
- **Naming:** PascalCase types/methods, `I`-prefixed interfaces, files match type names, English code
  identifiers with Spanish user-facing strings centralized in `NarratorService` / component markup.

### State machine
`Splash → Mode → Setup → Game`; within Game: `WaitingForMove → AnimatingDrop → CheckingWinner →
(ColumnFull: brief, same turn) → WaitingForMove | Victory | Draw`. `Sensors` and `Settings` are overlays
that remember the previous screen. Maps to the required internal states (Setup, AttractMode, SelectingMode,
SelectingPlayers, WaitingForMove, AnimatingDrop, CheckingWinner, ColumnFull, GameOver, Draw, SensorTest,
Settings).

### Input & play mode — digital vs physical (first-class from day one)
The tool must run **with or without a physical sensor board**. The only difference is **what triggers a
move and who is authoritative** — the domain (`Core`) is identical either way. A move is a move.

**`PlayMode` (Digital | Physical):**
- **Digital** — the screen *is* the game. `ScreenInputSource` (clicks/taps + keyboard 1–7) triggers moves;
  the app is authoritative and animates the falling chip as move feedback.
- **Physical** — the physical board is authoritative. `SensorInputSource` (sensor/WebSocket) triggers moves;
  the screen **mirrors** the board. **On-screen column clicks are disabled** (the board is the only move
  input); taps remain only on the Sensor-Test screen. The keyboard 1–7 still works as a sensor *simulator*.

**Unified pipeline.** Every source implements `IMoveSource` and raises the same `ColumnTriggered(int col)`
(0-indexed; shown as 1–7). `MoveRouter` funnels all active sources into `GameSession.TryDrop(col)` with
debounce/dedup (so one physical ball can't double-register, per the audio cooldown rules). `GameSession`
validates via `Core` exactly the same in both modes (full column → column-full feedback, no move).

**Chip movement is identical on screen in both modes.** The `drop` animation is driven purely by the
*committed* move (`GameSession.LastDrop = {col,row}`), regardless of whether a click or a sensor caused it.
So in Physical mode the screen **replays the same falling animation** to mirror the real chip (confirmed
decision), not just a static final state. "How the chip moves" visually is one code path; only the *trigger*
and the *click affordance* differ by mode.

**Mode selection — persistent setting + autodetection (confirmed):**
- Default **Digital**. User can pin the mode in Settings; persisted to localStorage.
- `SensorConnectionService` reports connection state. When sensors connect, mode can auto-switch to
  **Physical**; on disconnect it falls back to **Digital** so the game is always playable. The Sensor-Test
  screen shows/forces connection state for diagnostics.

**CPU constraint.** 1-player vs CPU is **Digital-only** (no physical actuator to drop the CPU's chip).
In Physical mode the mode selector offers 2-player only; choosing 1P implies Digital. (Documented assumption.)

---

## 8. Audio Integration

`wwwroot/js/arcade.js` exposes `window.ArcadeAudio`: `init()` (first gesture), `playSfx(key)`,
`playVoice(key)`, `playRandomVoice(group)`, `playMusic(key, loop)`, `stopMusic()`,
`setVolumes(sfx, voice, music)`, `mute()`, `unmute()`. Missing files → `console.warn`, never throw.

Cooldowns: `column-full` ≥800ms, `turn-change` ≥300ms, no rapid duplicate `chip-drop`.

`NarratorService` plays voice only on key events + occasional "great-move"; normal moves use SFX only.

### Event → real-file mapping (against the actual flat `audio/` tree)
| Game event | SFX | Voice (random of group) |
|------------|-----|--------------------------|
| Button click | `ui/button-click.mp3` | — |
| Confirm selection | `ui/menu-move.mp3` | — |
| Back | `ui/back.mp3` | — |
| Splash (first tap) | music `music/attract-loop.mp3` (loop) | `voice/welcome-01.mp3` |
| Enter mode select | — | `voice/select-game-mode-01.mp3` |
| Enter player setup | — | `voice/choose-character-01.mp3` |
| Begin game | — | `voice/get-ready-01.mp3` |
| Turn → Player 1 | `game/turn-change.mp3` | `voice/player-one-turn-{01..03}.mp3` |
| Turn → Player 2 | `game/turn-change.mp3` | `voice/player-two-turn-{01..03}.mp3` |
| Chip dropped | `game/chip-drop.mp3` | — |
| Good move (occasional) | — | `voice/great-move-{01..05}.mp3` |
| 3-in-a-row threat | `game/almost-win.mp3` | `voice/almost-win-{01..04}.mp3` |
| Column full | `game/column-full.mp3` | `voice/column-full-{01..04}.mp3` |
| Victory (connect 4) | `victory/connect-four.mp3` then `victory/win.mp3` | `voice/connect-four-01.mp3` then one of `voice/{winner-01,victory-01,victory-02,victory-03}.mp3` |
| Draw | `victory/draw.mp3` | `voice/draw-{01..03}.mp3` |
| Rematch | — | `voice/rematch-01.mp3` |

Extra voice files present on disk (`match-start-01`, `tap-to-start-01`, `player-ready-01`, `chip-placed-01`,
`block-move-01`, `thanks-for-playing-01`) are available as optional flavor (e.g. `block-move` when CPU
blocks, `thanks-for-playing` on returning to splash). Missing keys → `console.warn`, never throw.

> The provided `audio/` is flat (`audio/{ui,game,victory,voice,music}/`), which differs from the deeper
> `sfx/ui`, `voice/setup`, `voice/gameplay` nesting described in the prompt. The mapping above keys off the
> **actual on-disk paths**, which are the files that will be used.

---

## 9. Responsive
- Landscape / wide: full 3-column arcade layout (clamps handle scaling laptop→TV).
- Portrait phone (≤900px): rotate-device hint replaces the board (matches source `.rotate-hint`).
- Setup/settings screens scroll vertically and wrap on narrow widths.

---

## 10. Out of Scope (this phase)
- Real WebSocket/ESP32 sensor wiring: the `IMoveSource`/`SensorInputSource`/`SensorConnectionService`
  abstractions and the Digital/Physical mode switch are built now; the actual hardware transport
  (WebSocket client to an ESP32) is stubbed/simulated and wired later without touching game logic.
- Online multiplayer, persistent leaderboards.
- Theme switching (classic/neon/court) — dropped this phase; classic is the shipped look.
