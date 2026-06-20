# Landscape view + fullscreen button — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a phone-landscape in-game layout (narrow left column with score/narrator/actions, board maximized on the right) and a global fullscreen button that uses the Fullscreen API where supported with an iPhone "Add to Home Screen" PWA fallback.

**Architecture:** A responsive `MobileGameView` branches on `IViewportService.IsLandscape`, reusing existing components (`MobileScoreboard`, `NarratorBubble`, `BoardGrid`) — no duplicated game logic. Fullscreen is a new `IFullscreenService` (JS interop isolated behind `Services/`, mirroring `ViewportService`) consumed by a new `FullscreenButton` mounted globally in `AppShell`. PWA metadata in `index.html` + a web manifest gives iPhone real fullscreen via the home-screen app.

**Tech Stack:** Blazor WebAssembly (.NET 10), Razor components, `IJSRuntime` interop, `wwwroot/js/arcade.js`, CSS in `wwwroot/css/mobile.css`.

**Testing note (project convention):** Per `CLAUDE.md`, `Core` is TDD'd but UI uses **build-and-verify**. This feature touches only `Web` (no `Core` changes), so tasks verify with `dotnet build` + the existing 51 Core tests staying green, plus manual on-device checks at the end. No new unit tests.

**Spec:** `docs/superpowers/specs/2026-06-20-landscape-view-fullscreen-design.md`

---

## File structure

- Create: `src/Connect4HoopsArcade.Web/Services/Abstractions/IFullscreenService.cs` — interface.
- Create: `src/Connect4HoopsArcade.Web/Services/FullscreenService.cs` — JS-interop service + `[JSInvokable]` state callback.
- Create: `src/Connect4HoopsArcade.Web/Components/Shared/FullscreenButton.razor` — global corner button + iPhone hint.
- Create: `src/Connect4HoopsArcade.Web/wwwroot/manifest.webmanifest` — PWA manifest.
- Modify: `src/Connect4HoopsArcade.Web/wwwroot/js/arcade.js` — add `window.ArcadeFullscreen`.
- Modify: `src/Connect4HoopsArcade.Web/Program.cs` — register + init `IFullscreenService`.
- Modify: `src/Connect4HoopsArcade.Web/wwwroot/index.html` — PWA meta tags + manifest/apple-touch-icon links.
- Modify: `src/Connect4HoopsArcade.Web/Components/Layout/AppShell.razor` — mount `<FullscreenButton />`.
- Modify: `src/Connect4HoopsArcade.Web/Components/Screens/MobileGameView.razor` — branch by `IsLandscape`.
- Modify: `src/Connect4HoopsArcade.Web/wwwroot/css/mobile.css` — `.mob-game.landscape` styles + `.fs-btn`/`.fs-hint`.

---

## Task 1: Fullscreen JS API (`window.ArcadeFullscreen`)

**Files:**
- Modify: `src/Connect4HoopsArcade.Web/wwwroot/js/arcade.js` (append near the other `window.*` globals, e.g. after `window.ArcadeStore`).

- [ ] **Step 1: Add the `ArcadeFullscreen` global**

Append to `arcade.js`:

```js
// Fullscreen control. The Fullscreen API works on Android Chrome, desktop, and iPad Safari, but NOT
// iPhone Safari — there the only real fullscreen is "Add to Home Screen" (PWA standalone, see index.html).
window.ArcadeFullscreen = {
  _ref: null,
  isApiSupported() { return !!(document.fullscreenEnabled || document.webkitFullscreenEnabled); },
  isActive() { return !!(document.fullscreenElement || document.webkitFullscreenElement); },
  isIOSPhone() { return /iPhone|iPod/.test(navigator.userAgent || '') && !this.isApiSupported(); },
  async toggle() {
    try {
      if (this.isActive()) {
        if (document.exitFullscreen) await document.exitFullscreen();
        else if (document.webkitExitFullscreen) document.webkitExitFullscreen();
      } else {
        const el = document.documentElement;
        if (el.requestFullscreen) await el.requestFullscreen();
        else if (el.webkitRequestFullscreen) el.webkitRequestFullscreen();
      }
    } catch (e) { /* user-gesture / unsupported: ignore, state reported below */ }
    return this.isActive();
  },
  register(dotNetRef) {
    this._ref = dotNetRef;
    const notify = () => { if (this._ref) this._ref.invokeMethodAsync('OnFullscreenChanged', this.isActive()); };
    document.addEventListener('fullscreenchange', notify);
    document.addEventListener('webkitfullscreenchange', notify);
  },
};
```

- [ ] **Step 2: Syntax-check**

Run: `node --check src/Connect4HoopsArcade.Web/wwwroot/js/arcade.js`
Expected: no output (exit 0).

- [ ] **Step 3: Commit**

```bash
git add src/Connect4HoopsArcade.Web/wwwroot/js/arcade.js
git commit -m "feat(web): ArcadeFullscreen JS API (toggle + state + iPhone detect)"
```

---

## Task 2: `IFullscreenService` + `FullscreenService` (C# interop)

**Files:**
- Create: `src/Connect4HoopsArcade.Web/Services/Abstractions/IFullscreenService.cs`
- Create: `src/Connect4HoopsArcade.Web/Services/FullscreenService.cs`

- [ ] **Step 1: Create the interface**

`Services/Abstractions/IFullscreenService.cs`:

```csharp
namespace Connect4HoopsArcade.Web.Services.Abstractions;

/// <summary>Fullscreen control. API works on Android/desktop/iPad; iPhone Safari falls back to PWA "Add to Home Screen".</summary>
public interface IFullscreenService
{
    bool IsActive { get; }
    bool IsApiSupported { get; }
    bool IsIOSPhone { get; }
    event Action? StateChanged;
    Task InitAsync();
    Task ToggleAsync();
}
```

- [ ] **Step 2: Create the service**

`Services/FullscreenService.cs`:

```csharp
using Microsoft.JSInterop;
using Connect4HoopsArcade.Web.Services.Abstractions;

namespace Connect4HoopsArcade.Web.Services;

/// <summary>Wraps window.ArcadeFullscreen. Mirrors ViewportService: a DotNetObjectReference receives
/// fullscreenchange callbacks so the button icon stays in sync even on Esc/system-gesture exit.</summary>
public sealed class FullscreenService : IFullscreenService, IDisposable
{
    private readonly IJSRuntime _js;
    private DotNetObjectReference<FullscreenService>? _ref;

    public bool IsActive { get; private set; }
    public bool IsApiSupported { get; private set; }
    public bool IsIOSPhone { get; private set; }
    public event Action? StateChanged;

    public FullscreenService(IJSRuntime js) => _js = js;

    public async Task InitAsync()
    {
        IsApiSupported = await _js.InvokeAsync<bool>("ArcadeFullscreen.isApiSupported");
        IsIOSPhone = await _js.InvokeAsync<bool>("ArcadeFullscreen.isIOSPhone");
        IsActive = await _js.InvokeAsync<bool>("ArcadeFullscreen.isActive");
        _ref = DotNetObjectReference.Create(this);
        await _js.InvokeVoidAsync("ArcadeFullscreen.register", _ref);
    }

    public async Task ToggleAsync()
    {
        IsActive = await _js.InvokeAsync<bool>("ArcadeFullscreen.toggle");
        StateChanged?.Invoke();
    }

    [JSInvokable]
    public void OnFullscreenChanged(bool active)
    {
        IsActive = active;
        StateChanged?.Invoke();
    }

    public void Dispose() => _ref?.Dispose();
}
```

- [ ] **Step 3: Build**

Run: `dotnet build src/Connect4HoopsArcade.Web -c Debug -v q`
Expected: `Compilación correcta.` / 0 Errores.

- [ ] **Step 4: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Services/Abstractions/IFullscreenService.cs src/Connect4HoopsArcade.Web/Services/FullscreenService.cs
git commit -m "feat(web): FullscreenService isolating ArcadeFullscreen interop"
```

---

## Task 3: Register + initialize the service

**Files:**
- Modify: `src/Connect4HoopsArcade.Web/Program.cs`

- [ ] **Step 1: Register as a singleton**

In `Program.cs`, after the `IViewportService` registration (around line 22-23), add:

```csharp
builder.Services.AddSingleton<Connect4HoopsArcade.Web.Services.Abstractions.IFullscreenService,
                              Connect4HoopsArcade.Web.Services.FullscreenService>();
```

- [ ] **Step 2: Initialize after build**

In `Program.cs`, after the existing `IViewportService ... InitAsync()` line (around line 31), add:

```csharp
await host.Services.GetRequiredService<Connect4HoopsArcade.Web.Services.Abstractions.IFullscreenService>().InitAsync();
```

- [ ] **Step 3: Build**

Run: `dotnet build src/Connect4HoopsArcade.Web -c Debug -v q`
Expected: `Compilación correcta.` / 0 Errores.

- [ ] **Step 4: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Program.cs
git commit -m "feat(web): register + init FullscreenService"
```

---

## Task 4: `FullscreenButton` component

**Files:**
- Create: `src/Connect4HoopsArcade.Web/Components/Shared/FullscreenButton.razor`

- [ ] **Step 1: Create the component**

`Components/Shared/FullscreenButton.razor`:

```razor
@implements IDisposable
@inject Connect4HoopsArcade.Web.Services.Abstractions.IFullscreenService Fs

<button class="fs-btn" @onclick="Toggle" aria-label="Pantalla completa" title="Pantalla completa">
  @if (Fs.IsActive)
  {
    <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M9 9H5V5"/><path d="M15 9h4V5"/><path d="M9 15H5v4"/><path d="M15 15h4v4"/></svg>
  }
  else
  {
    <svg viewBox="0 0 24 24" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M4 9V4h5"/><path d="M20 9V4h-5"/><path d="M4 15v5h5"/><path d="M20 15v5h-5"/></svg>
  }
</button>
@if (_hint)
{
  <div class="fs-hint" @onclick="DismissHint">Para pantalla completa en iPhone: toca <strong>Compartir</strong> → <strong>“Agregar a inicio”</strong>. (Toca para cerrar.)</div>
}

@code {
    private bool _hint;

    protected override void OnInitialized() => Fs.StateChanged += OnChanged;

    private void OnChanged() => InvokeAsync(StateHasChanged);

    private async Task Toggle()
    {
        if (Fs.IsApiSupported) await Fs.ToggleAsync();
        else if (Fs.IsIOSPhone) { _hint = true; StateHasChanged(); }
    }

    private void DismissHint() => _hint = false;

    public void Dispose() => Fs.StateChanged -= OnChanged;
}
```

- [ ] **Step 2: Add the corner-button + hint CSS**

Append to `src/Connect4HoopsArcade.Web/wwwroot/css/mobile.css`:

```css
/* ---- Global fullscreen button ---- */
.fs-btn {
  position: fixed; z-index: 60;
  top: calc(env(safe-area-inset-top) + 6px);
  right: calc(env(safe-area-inset-right) + 6px);
  width: 34px; height: 34px; display: flex; align-items: center; justify-content: center;
  border-radius: 10px; border: 1px solid rgba(255,255,255,.18);
  background: rgba(0,0,0,.35); color: #fff; opacity: .45; cursor: pointer; transition: opacity .15s;
}
.fs-btn:hover, .fs-btn:active { opacity: 1; }
.fs-hint {
  position: fixed; z-index: 61;
  top: calc(env(safe-area-inset-top) + 46px); right: 6px; max-width: 250px;
  background: #161031; border: 1px solid rgba(255,255,255,.2); border-radius: 12px;
  padding: 10px 12px; color: #fff; font-size: 13px; font-weight: 700; line-height: 1.35;
  box-shadow: 0 10px 24px rgba(0,0,0,.5); cursor: pointer;
}
```

- [ ] **Step 3: Build**

Run: `dotnet build src/Connect4HoopsArcade.Web -c Debug -v q`
Expected: `Compilación correcta.` / 0 Errores.

- [ ] **Step 4: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Components/Shared/FullscreenButton.razor src/Connect4HoopsArcade.Web/wwwroot/css/mobile.css
git commit -m "feat(web): FullscreenButton corner control + iPhone hint"
```

---

## Task 5: Mount the button globally

**Files:**
- Modify: `src/Connect4HoopsArcade.Web/Components/Layout/AppShell.razor`

- [ ] **Step 1: Render the button inside `arc-root`**

In `AppShell.razor`, add `<FullscreenButton />` immediately after `<GlowBackdrop />` (line 12) so it sits above every screen:

```razor
<div class="arc-root">
  <GlowBackdrop />
  <Connect4HoopsArcade.Web.Components.Shared.FullscreenButton />
  @switch (Session.Screen)
  {
```

(Leave the rest of `AppShell.razor` unchanged.)

- [ ] **Step 2: Build**

Run: `dotnet build src/Connect4HoopsArcade.Web -c Debug -v q`
Expected: `Compilación correcta.` / 0 Errores.

- [ ] **Step 3: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Components/Layout/AppShell.razor
git commit -m "feat(web): mount FullscreenButton on every screen"
```

---

## Task 6: PWA metadata (iPhone real-fullscreen via home-screen app)

**Files:**
- Create: `src/Connect4HoopsArcade.Web/wwwroot/manifest.webmanifest`
- Modify: `src/Connect4HoopsArcade.Web/wwwroot/index.html`

- [ ] **Step 1: Create the manifest**

`wwwroot/manifest.webmanifest` (reuses the existing `icon-192.png` and `favicon.png`):

```json
{
  "name": "Connect 4 Hoops Arcade",
  "short_name": "Hoops C4",
  "lang": "es",
  "start_url": "/",
  "display": "standalone",
  "orientation": "any",
  "background_color": "#0b0a1f",
  "theme_color": "#0b0a1f",
  "icons": [
    { "src": "favicon.png", "sizes": "any", "type": "image/png" },
    { "src": "icon-192.png", "sizes": "192x192", "type": "image/png", "purpose": "any maskable" }
  ]
}
```

- [ ] **Step 2: Add PWA + apple meta to `index.html`**

In `index.html` `<head>`, after the existing stylesheet links (line 10), add:

```html
    <link rel="manifest" href="manifest.webmanifest" />
    <meta name="theme-color" content="#0b0a1f" />
    <meta name="mobile-web-app-capable" content="yes" />
    <meta name="apple-mobile-web-app-capable" content="yes" />
    <meta name="apple-mobile-web-app-status-bar-style" content="black-translucent" />
    <meta name="apple-mobile-web-app-title" content="Hoops C4" />
    <link rel="apple-touch-icon" href="icon-192.png" />
```

- [ ] **Step 3: Build and confirm the manifest is published**

Run: `dotnet build src/Connect4HoopsArcade.Web -c Debug -v q`
Expected: `Compilación correcta.` / 0 Errores. (Static `wwwroot` files are served as-is; no extra wiring needed.)

- [ ] **Step 4: Commit**

```bash
git add src/Connect4HoopsArcade.Web/wwwroot/manifest.webmanifest src/Connect4HoopsArcade.Web/wwwroot/index.html
git commit -m "feat(web): PWA manifest + apple meta for iPhone home-screen fullscreen"
```

---

## Task 7: Landscape branch in `MobileGameView`

**Files:**
- Modify: `src/Connect4HoopsArcade.Web/Components/Screens/MobileGameView.razor`

- [ ] **Step 1: Replace the component with an orientation-branched version**

Full new contents of `MobileGameView.razor`:

```razor
@using Connect4HoopsArcade.Web.Components.Game
@using Connect4HoopsArcade.Web.Components.Game.Mobile
@inherits SessionComponentBase
@inject Connect4HoopsArcade.Web.Services.Abstractions.IViewportService Viewport

@if (Viewport.IsLandscape)
{
  <div class="mob-game landscape">
    <div class="mob-side">
      <MobileScoreboard />
      <div class="mob-side-narrator"><NarratorBubble /></div>
      <div class="mob-side-actions">
        <button class="mob-side-btn" @onclick="() => Session.ResetBoard()" disabled="@Session.IsBusy">🔄 Reiniciar</button>
        <button class="mob-side-btn" @onclick="() => Session.Resign()" disabled="@Session.IsBusy">🏳️ Rendirse</button>
        <button class="mob-side-btn" @onclick="() => Session.OpenSettings()">⚙ Ajustes</button>
      </div>
    </div>
    <div class="mob-board-area">
      <BoardGrid InteractionEnabled="InteractionEnabled" FitContainer="true" />
    </div>
  </div>
}
else
{
  <div class="mob-game">
    <MobileScoreboard OnTap="OpenSheet" />
    <div class="mob-board-area">
      <BoardGrid InteractionEnabled="InteractionEnabled" FitContainer="true" />
    </div>
    <div class="mob-narrator"><NarratorBubble /></div>
    <ActionSheet Open="_sheetOpen" OnClose="CloseSheet" />
  </div>
}

@code {
    private bool _sheetOpen;

    protected override void OnInitialized()
    {
        base.OnInitialized();                       // SessionComponentBase subscribes StateChanged
        Viewport.OnViewportChanged += OnViewport;   // re-render on rotate (portrait <-> landscape)
    }

    private void OnViewport() => InvokeAsync(StateHasChanged);

    private void OpenSheet() { _sheetOpen = true; StateHasChanged(); }
    private void CloseSheet() { _sheetOpen = false; StateHasChanged(); }
    private bool InteractionEnabled => Session.Mode2 == Connect4HoopsArcade.Web.Models.PlayMode.Digital;

    public override void Dispose()
    {
        Viewport.OnViewportChanged -= OnViewport;
        base.Dispose();                             // unsubscribe StateChanged
    }
}
```

Notes:
- `MobileScoreboard` in landscape is rendered without `OnTap` (invoking an unset `EventCallback` is a
  no-op); it is restyled to stack vertically via CSS in Task 8.
- Action buttons call the same `GameSession` methods the `ActionSheet` uses (`ResetBoard`, `Resign`,
  `OpenSettings`) — the logic lives in `GameSession`, so this is not duplicated game logic.

- [ ] **Step 2: Build**

Run: `dotnet build src/Connect4HoopsArcade.Web -c Debug -v q`
Expected: `Compilación correcta.` / 0 Errores.

- [ ] **Step 3: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Components/Screens/MobileGameView.razor
git commit -m "feat(web): landscape in-game layout branch in MobileGameView"
```

---

## Task 8: Landscape CSS (column + maximized board)

**Files:**
- Modify: `src/Connect4HoopsArcade.Web/wwwroot/css/mobile.css`

- [ ] **Step 1: Add the landscape layout rules**

Append to `mobile.css` (after the existing in-game rules, before the action-sheet section is fine):

```css
/* ---- In-game: landscape (phone rotated) ---- */
.mob-game.landscape { flex-direction: row; align-items: stretch; gap: 10px; }

/* Narrow left column: as small as legibility allows so the board gets the rest. */
.mob-game.landscape .mob-side {
  flex: 0 0 auto; width: clamp(122px, 26vw, 190px);
  display: flex; flex-direction: column; gap: 8px; min-height: 0;
}

/* Compact, vertically-stacked scoreboard in the column. */
.mob-game.landscape .mob-scoreboard { flex: none; flex-direction: column; gap: 6px; padding: 8px; }
.mob-game.landscape .mob-sb-side,
.mob-game.landscape .mob-sb-side.right { justify-content: center; opacity: 1; }
.mob-game.landscape .mob-sb-meta,
.mob-game.landscape .mob-sb-meta.right { align-items: center; }
.mob-game.landscape .mob-sb-name { max-width: none; }

.mob-game.landscape .mob-side-narrator { flex: 1; min-height: 0; display: flex; align-items: center; }
.mob-game.landscape .mob-side-narrator > * { margin-top: 0 !important; }
.mob-game.landscape .mob-side-narrator :where(span) { white-space: normal !important; }

.mob-game.landscape .mob-side-actions { flex: none; display: flex; flex-direction: column; gap: 6px; }
.mob-side-btn {
  cursor: pointer; text-align: center; font-weight: 800; font-size: 13px; color: #fff;
  background: rgba(255,255,255,.07); border: 1px solid rgba(255,255,255,.12);
  border-radius: 10px; padding: 9px 8px;
}
.mob-side-btn:disabled { opacity: .4; }

/* Board fills all remaining width; height-driven so it stays as big as possible at 7:6. */
.mob-game.landscape .mob-board-area { flex: 1; min-width: 0; }
.mob-game.landscape .mob-board-area > * {
  height: 100%; width: auto; max-width: 100%; aspect-ratio: 7 / 6;
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/Connect4HoopsArcade.Web -c Debug -v q`
Expected: `Compilación correcta.` / 0 Errores.

- [ ] **Step 3: Commit**

```bash
git add src/Connect4HoopsArcade.Web/wwwroot/css/mobile.css
git commit -m "feat(web): landscape column + maximized-board CSS"
```

---

## Task 9: Verify, then deploy

- [ ] **Step 1: Full build + tests stay green**

Run: `dotnet build -v q` then `dotnet test -v q`
Expected: build `Compilación correcta.`; tests `Superado: 51`.

- [ ] **Step 2: Manual on-device verification (deploy to `main`, open on phone)**

Push to `main` (Cloudflare auto-deploys), confirm the corner build tag updates, then on the phone:
- Portrait in-game looks **unchanged** (scoreboard tap → ActionSheet still works).
- Rotate to **landscape mid-game**: board is large on the right; left column shows stacked
  scoreboard, narrator messages, and the three action buttons; buttons work (Reiniciar/Rendirse/Ajustes).
- Rotate back to portrait: reflows correctly.
- Fullscreen button (top-right) is visible on splash, menus, and in-game; on Android/desktop it
  enters/exits fullscreen and the icon swaps (incl. Esc/gesture exit). On iPhone it shows the
  "Agregar a inicio" hint.
- iPhone: "Compartir → Agregar a inicio", launch from the home screen → runs **without the Safari bar**.
- Confirm the fullscreen button does not block the in-game scoreboard's tap target in portrait; if it
  overlaps, nudge `.fs-btn` (e.g. lower opacity / smaller) — verify only.

- [ ] **Step 3: Commit (if any verify-driven tweak was needed)**

```bash
git add -A && git commit -m "fix(web): landscape/fullscreen polish from device verification"
```

---

## Notes / out of scope
- The temporary perf-diagnostic probes (on-screen jank box, minimax timer, audio counters) are **still in
  the build** and should be removed in a separate cleanup commit — not part of this plan.
- Setup-screen landscape redesign is deferred unless it proves cramped.
