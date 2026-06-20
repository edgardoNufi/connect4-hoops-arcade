# Landscape view + fullscreen button — design

**Date:** 2026-06-20
**Status:** Approved (design); pending implementation plan
**Roadmap:** Advances item 1 (mobile-first responsive) and item 2a (responsive + Fullscreen API for TV mirroring).

## Problem
On phones, the in-game view is a single vertical column (scoreboard → board → narrator), used the
same way in portrait and landscape. In landscape the board is sized off the (small) viewport height,
so it ends up small and centered with large empty bands on the left/right. There is no
landscape-specific layout. The user also wants a way to put the browser into fullscreen (for
projecting/mirroring to a TV).

## Goals
1. A dedicated **phone-landscape** in-game layout that makes the **board as large as possible**, with a
   narrow left column holding the score, narrator messages, and game actions.
2. A **fullscreen button** available on every screen, with an honest fallback for iPhone Safari.

## Non-goals
- No changes to the desktop view, the portrait phone view, or 2-player vs 1-player logic.
- No landscape redesign of the setup screen (it already scrolls; revisit later if needed).
- No changes to `Core` or to game rules/flow.

## Decisions (from brainstorming)
- **Activation:** phone in landscape only. Portrait stays exactly as today. (The TV mirrors the phone,
  so phone-landscape is what shows on the TV.)
- **Layout (chosen: option B, board maximized):** single **left column**, board fills the rest on the right.
  - Column top → **scoreboard** (compact: both players + scores).
  - Column middle → **narrator bubble** (messages).
  - Column bottom → **action buttons shown directly**: Reiniciar, Rendirse, Ajustes (no tap-to-open
    ActionSheet in landscape; there is room).
  - The column is as narrow as legibility allows; the board takes all remaining width.
- **Fullscreen button:** available on **all screens** (a discreet corner icon, like `.build-tag`).
- **iPhone handling (chosen: API + iPhone trick):** use the Fullscreen API where supported
  (Android/desktop/iPad); on iPhone Safari (no Fullscreen API) the button shows an "Compartir → Agregar
  a inicio" hint, and we add PWA metadata so the home-screen-installed app runs without Safari chrome
  (real fullscreen).

## Approach
One **responsive** `MobileGameView` that branches on `IViewportService.IsLandscape` (already exists and
raises `OnViewportChanged` on rotation). Portrait renders today's markup; landscape renders the
column+board arrangement. All children are the **existing** components — `MobileScoreboard`,
`NarratorBubble`, `BoardGrid`, and the action handlers currently in `ActionSheet` — so there is **no
duplicated game logic** (consistent with the project's "same GameSession/MoveRouter/Core, dedicated
views" rule).

*Rejected alternative — pure CSS reflow:* flipping `.mob-game` from column to row via an
`(orientation: landscape)` media query alone is not enough, because landscape replaces the tap-sheet
with directly-rendered action buttons — a structural (markup) change, not just reflow.

## Detailed design

### 1. Landscape in-game layout
- `MobileGameView` injects `IViewportService` and subscribes to `OnViewportChanged` (re-render on
  rotation). It already inherits `SessionComponentBase` for state changes.
- When `Viewport.IsLandscape`: render
  ```
  <div class="mob-game landscape">
    <div class="mob-side">           <!-- narrow left column -->
      <MobileScoreboard ... />        <!-- compact; reused -->
      <div class="mob-side-narrator"><NarratorBubble /></div>
      <div class="mob-side-actions">  <!-- direct buttons -->
        Reiniciar / Rendirse / Ajustes
      </div>
    </div>
    <div class="mob-board-area"><BoardGrid FitContainer="true" InteractionEnabled=... /></div>
  </div>
  ```
- When portrait: today's markup unchanged (scoreboard tap → `ActionSheet`).
- **Board sizing (landscape):** the board keeps its 7:6 aspect ratio. Height = available content
  height (after safe-area padding); width = height × 7/6. The left column takes a fixed/min width; the
  board area flexes to fill the rest. New CSS lives under `.mob-game.landscape` in `mobile.css`.
- **Action handlers:** reuse the same calls `ActionSheet` makes — `Session.ResetBoard()`,
  `Session.Resign()`, `Session.OpenSettings()`. To avoid copy-paste, factor the three actions into a
  small shared piece (either a tiny `GameActions` component used by both, or shared methods); the
  ActionSheet keeps working for portrait.
- **Safe areas:** landscape padding must respect `env(safe-area-inset-left/right)` (notch side).

### 2. Fullscreen button + iPhone/PWA
- **JS (`arcade.js`): `window.ArcadeFullscreen`** with:
  - `isApiSupported()` — `document.fullscreenEnabled` (and webkit prefix for iPad Safari).
  - `isActive()` — whether currently fullscreen.
  - `toggle()` — `requestFullscreen()` / `exitFullscreen()` (with webkit fallbacks). Returns a status
    so C# knows if it actually did something.
  - `isIOSPhone()` — detect iPhone Safari (no API) to drive the hint.
- **Component `FullscreenButton.razor`** (new, in `Components/Shared` or `Components/Layout`):
  - Discreet corner icon (fixed position, dim, like `.build-tag`), `z-index` above content.
  - On click: if API supported → `toggle()` and swap the icon (enter/exit). If iPhone → show a small
    transient hint bubble: "Para pantalla completa: Compartir → Agregar a inicio".
  - Mounted once in `AppShell` so it appears on every screen.
- **PWA metadata (`index.html` + manifest):**
  - `<meta name="apple-mobile-web-app-capable" content="yes">`,
    `<meta name="apple-mobile-web-app-status-bar-style" content="black-translucent">`,
    `<meta name="theme-color" content="...">`, `apple-mobile-web-app-title`.
  - A `manifest.webmanifest` with `display: standalone`, name/short_name, theme/background color, and
    icon(s) (reuse an existing app icon asset; add apple-touch-icon link).
  - Effect: "Agregar a inicio" on iPhone launches the game without the Safari toolbar = real fullscreen.

## Files touched
- `Components/Screens/MobileGameView.razor` — branch by `IsLandscape`; subscribe to `OnViewportChanged`.
- `wwwroot/css/mobile.css` — `.mob-game.landscape` (row layout), `.mob-side`, `.mob-side-actions`, board sizing.
- `Components/Shared/FullscreenButton.razor` — new global corner button.
- `Components/Game/Mobile/` — small shared game-actions piece reused by `ActionSheet` and the landscape column.
- `wwwroot/js/arcade.js` — add `window.ArcadeFullscreen`.
- `wwwroot/index.html` + `wwwroot/manifest.webmanifest` — PWA metadata + icons.
- `Components/Layout/AppShell.razor` — mount `FullscreenButton` once.
- No `Core` / `GameSession` rule changes.

## Edge cases
- **Rotate mid-game:** layout re-flows live via `OnViewportChanged`; in-flight drop/CPU turn unaffected
  (state lives in `GameSession`, components only re-render).
- **Victory/Draw modals in landscape:** remain centered overlays; existing mobile `max-height`/scroll
  rule covers short landscape height.
- **Very short landscape heights** (small phones): column actions stay reachable; board never overflows
  (height-driven sizing + `max-width:100%`).
- **Fullscreen exit via system gesture** (Esc / swipe): the button reflects state via the
  `fullscreenchange` event so the icon stays in sync.
- **iPhone already installed to home screen:** running standalone, the fullscreen button can be hidden
  or simply inert (already fullscreen).

## Testing / verification
- `dotnet build` + existing `dotnet test` (51 Core tests) stay green (no Core changes expected).
- Build-and-verify on the phone: rotate to landscape mid-game (board large, column readable, actions
  work), rotate back (portrait unchanged), tap fullscreen on Android/desktop (enters/exits), tap on
  iPhone (hint shows), and "Agregar a inicio" on iPhone launches without the Safari bar.

## Out of scope / follow-ups
- Remove the temporary perf-diagnostic probes (on-screen jank box, minimax timer, audio counters) —
  tracked separately from this feature.
- Landscape redesign of the setup screen, if it proves cramped.
