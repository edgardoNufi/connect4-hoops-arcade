# Mobile-First Responsive Redesign — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the game fully playable and good-looking on phones (portrait-first) via dedicated mobile view components selected by a small `ViewportService`, without duplicating game logic or degrading the desktop view.

**Architecture:** A JS-backed `ViewportService` exposes viewport size/breakpoint/orientation + an `OnViewportChanged` event. `AppShell` branches `Game`/`Setup` to mobile vs desktop components. Mobile components are presentation-only: they reuse `GameSession`, `MoveRouter`, `BoardGrid`/`GameColumn`/`GameCell`, `AvatarSvg`, `NarratorBubble`, and the setup pickers. Simple screens + modals are fixed with responsive CSS (`100dvh`, `env(safe-area-inset-*)`).

**Tech Stack:** .NET 10 Blazor WebAssembly, JS interop (`wwwroot/js/arcade.js`), CSS. Spec: [`docs/superpowers/specs/2026-06-19-mobile-first-redesign-design.md`](../specs/2026-06-19-mobile-first-redesign-design.md).

---

## File Structure

**Create:**
- `src/Connect4HoopsArcade.Web/Models/Breakpoint.cs` — `enum Breakpoint { Mobile, Tablet, Desktop }`.
- `src/Connect4HoopsArcade.Web/Services/Abstractions/IViewportService.cs`
- `src/Connect4HoopsArcade.Web/Services/ViewportService.cs`
- `src/Connect4HoopsArcade.Web/Components/Game/Mobile/MobileScoreboard.razor`
- `src/Connect4HoopsArcade.Web/Components/Game/Mobile/ActionSheet.razor`
- `src/Connect4HoopsArcade.Web/Components/Screens/MobileGameView.razor`
- `src/Connect4HoopsArcade.Web/Components/Screens/MobilePlayerSetup.razor`
- `src/Connect4HoopsArcade.Web/wwwroot/css/mobile.css`

**Modify:**
- `src/Connect4HoopsArcade.Web/wwwroot/js/arcade.js` — append `window.ArcadeViewport`.
- `src/Connect4HoopsArcade.Web/Program.cs` — register + eager-init `ViewportService`.
- `src/Connect4HoopsArcade.Web/wwwroot/index.html` — link `mobile.css`.
- `src/Connect4HoopsArcade.Web/wwwroot/css/app.css` — remove rotate-hint forcing; `100vh`→`100dvh`.
- `src/Connect4HoopsArcade.Web/Components/Game/BoardGrid.razor`, `GameColumn.razor`, `GameCell.razor` — add a `FitContainer` parameter for mobile sizing (square cells fit to container).
- Rename `Components/Screens/GameView.razor` → `DesktopGameView.razor`; `PlayerSetup.razor` → `DesktopPlayerSetup.razor`.
- `Components/Layout/AppShell.razor` — branch by `IsMobile`; subscribe to `OnViewportChanged`.

**Verification:** No Core changes → 51 Core tests stay green. UI verified by `dotnet build` + manual browser run (mobile viewport + desktop). The 12 acceptance criteria in the spec are the manual checklist.

---

### Task 1: `ViewportService` (JS interop, isolated)

**Files:**
- Modify: `src/Connect4HoopsArcade.Web/wwwroot/js/arcade.js` (append at end)
- Create: `src/Connect4HoopsArcade.Web/Models/Breakpoint.cs`
- Create: `src/Connect4HoopsArcade.Web/Services/Abstractions/IViewportService.cs`
- Create: `src/Connect4HoopsArcade.Web/Services/ViewportService.cs`
- Modify: `src/Connect4HoopsArcade.Web/Program.cs`

- [ ] **Step 1: Add the JS module** — append to the end of `wwwroot/js/arcade.js`:

```javascript

// Viewport → .NET. Debounced; only notifies when the breakpoint or orientation changes.
window.ArcadeViewport = {
  _ref: null,
  _timer: null,
  _last: null,
  _onResize: null,
  snapshot() {
    var w = window.innerWidth, h = window.innerHeight;
    var isMobile = window.matchMedia('(max-width: 767px)').matches
                || window.matchMedia('(orientation: landscape) and (max-height: 480px)').matches;
    var bp = w < 768 ? 0 : (w < 1200 ? 1 : 2);   // 0 Mobile, 1 Tablet, 2 Desktop
    return { width: w, height: h, isMobile: isMobile, breakpoint: bp, isLandscape: w >= h };
  },
  _key(s) { return s.breakpoint + '|' + s.isMobile + '|' + s.isLandscape; },
  register(dotNetRef) {
    if (this._onResize) window.removeEventListener('resize', this._onResize);
    this._ref = dotNetRef;
    this._last = this.snapshot();
    this._onResize = () => {
      if (this._timer) clearTimeout(this._timer);
      this._timer = setTimeout(() => {
        var s = this.snapshot();
        // Always update raw size; only invoke .NET when the layout-relevant key changes.
        if (this._ref && this._key(s) !== this._key(this._last)) {
          this._last = s;
          this._ref.invokeMethodAsync('NotifyChanged', s.width, s.height, s.isMobile, s.breakpoint, s.isLandscape);
        } else {
          this._last = s;
        }
      }, 150);
    };
    window.addEventListener('resize', this._onResize);
    return this._last;
  },
  dispose() {
    if (this._onResize) window.removeEventListener('resize', this._onResize);
    if (this._timer) clearTimeout(this._timer);
    this._onResize = null; this._timer = null; this._ref = null;
  },
};
```

- [ ] **Step 2: Create the breakpoint enum** — `src/Connect4HoopsArcade.Web/Models/Breakpoint.cs`:

```csharp
namespace Connect4HoopsArcade.Web.Models;

/// <summary>Width-based viewport class. Mobile &lt; 768, Tablet 768–1199, Desktop ≥ 1200.</summary>
public enum Breakpoint { Mobile, Tablet, Desktop }
```

- [ ] **Step 3: Create the interface** — `src/Connect4HoopsArcade.Web/Services/Abstractions/IViewportService.cs`:

```csharp
using Connect4HoopsArcade.Web.Models;

namespace Connect4HoopsArcade.Web.Services.Abstractions;

/// <summary>Reactive viewport info backed by JS (matchMedia + debounced resize). Isolated JS interop.</summary>
public interface IViewportService
{
    int Width { get; }
    int Height { get; }
    bool IsMobile { get; }
    bool IsTablet { get; }
    bool IsLandscape { get; }
    bool IsPortrait { get; }
    Breakpoint Breakpoint { get; }

    /// <summary>Fires only when the breakpoint or orientation changes (not on every resize pixel).</summary>
    event Action? OnViewportChanged;

    /// <summary>Registers the JS resize listener and reads the initial size. Call once at startup.</summary>
    Task InitAsync();
}
```

- [ ] **Step 4: Create the implementation** — `src/Connect4HoopsArcade.Web/Services/ViewportService.cs`:

```csharp
using Microsoft.JSInterop;
using Connect4HoopsArcade.Web.Models;
using Connect4HoopsArcade.Web.Services.Abstractions;

namespace Connect4HoopsArcade.Web.Services;

/// <summary>Tracks viewport size/orientation via arcade.js. Notifies on breakpoint/orientation change only.</summary>
public sealed class ViewportService : IViewportService, IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private DotNetObjectReference<ViewportService>? _ref;
    private bool _initialized;

    public int Width { get; private set; } = 1280;
    public int Height { get; private set; } = 800;
    public bool IsMobile { get; private set; }
    public bool IsLandscape { get; private set; } = true;
    public Breakpoint Breakpoint { get; private set; } = Breakpoint.Desktop;

    public bool IsTablet => Breakpoint == Breakpoint.Tablet && !IsMobile;
    public bool IsPortrait => !IsLandscape;

    public event Action? OnViewportChanged;

    public ViewportService(IJSRuntime js) => _js = js;

    public async Task InitAsync()
    {
        if (_initialized) return;
        _initialized = true;
        _ref = DotNetObjectReference.Create(this);
        try
        {
            var s = await _js.InvokeAsync<Snapshot>("ArcadeViewport.register", _ref);
            Apply(s.Width, s.Height, s.IsMobile, s.Breakpoint, s.IsLandscape);
        }
        catch { /* prerender / no JS: keep desktop defaults */ }
    }

    [JSInvokable]
    public void NotifyChanged(int width, int height, bool isMobile, int breakpoint, bool isLandscape)
    {
        Apply(width, height, isMobile, breakpoint, isLandscape);
        OnViewportChanged?.Invoke();
    }

    private void Apply(int width, int height, bool isMobile, int breakpoint, bool isLandscape)
    {
        Width = width; Height = height; IsMobile = isMobile; IsLandscape = isLandscape;
        Breakpoint = (Breakpoint)breakpoint;
    }

    public async ValueTask DisposeAsync()
    {
        try { await _js.InvokeVoidAsync("ArcadeViewport.dispose"); }
        catch { /* runtime tearing down */ }
        _ref?.Dispose();
    }

    private sealed record Snapshot(int Width, int Height, bool IsMobile, int Breakpoint, bool IsLandscape);
}
```

- [ ] **Step 5: Register + init in `Program.cs`** — add the registration after the `NarratorService` line (`builder.Services.AddSingleton<...NarratorService>();`):

```csharp
builder.Services.AddSingleton<Connect4HoopsArcade.Web.Services.Abstractions.IViewportService,
                              Connect4HoopsArcade.Web.Services.ViewportService>();
```

And after the existing `await host.Services.GetRequiredService<...ISettingsStore>().LoadAsync();` line, add:

```csharp
await host.Services.GetRequiredService<Connect4HoopsArcade.Web.Services.Abstractions.IViewportService>().InitAsync();
```

- [ ] **Step 6: Build** — Run: `dotnet build`. Expected: 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/Connect4HoopsArcade.Web/wwwroot/js/arcade.js src/Connect4HoopsArcade.Web/Models/Breakpoint.cs src/Connect4HoopsArcade.Web/Services/Abstractions/IViewportService.cs src/Connect4HoopsArcade.Web/Services/ViewportService.cs src/Connect4HoopsArcade.Web/Program.cs
git commit -m "feat(web): add JS-backed ViewportService (size/breakpoint/orientation)"
```

---

### Task 2: Rename game/setup views to Desktop*

**Files:**
- Rename: `Components/Screens/GameView.razor` → `Components/Screens/DesktopGameView.razor`
- Rename: `Components/Screens/PlayerSetup.razor` → `Components/Screens/DesktopPlayerSetup.razor`
- Modify: `Components/Layout/AppShell.razor`

Pure rename — no behavior change. Blazor component class name follows the file name, so `<GameView/>` becomes `<DesktopGameView/>`.

- [ ] **Step 1: Rename the files (preserve history)**

```bash
cd "C:/Proyectos/Arcade/.claude/worktrees/nostalgic-pasteur-3c6c4e"
git mv src/Connect4HoopsArcade.Web/Components/Screens/GameView.razor src/Connect4HoopsArcade.Web/Components/Screens/DesktopGameView.razor
git mv src/Connect4HoopsArcade.Web/Components/Screens/PlayerSetup.razor src/Connect4HoopsArcade.Web/Components/Screens/DesktopPlayerSetup.razor
```

- [ ] **Step 2: Update `AppShell.razor` references** — in `Components/Layout/AppShell.razor`, replace the three `<GameView />` usages and the `<PlayerSetup />` usage:
  - `case AppScreen.Setup:` body `<PlayerSetup />` → `<DesktopPlayerSetup />`
  - `case AppScreen.Game:` body `<GameView />` → `<DesktopGameView />`
  - `case AppScreen.Victory:` body `<GameView />` → `<DesktopGameView />`
  - `case AppScreen.Draw:` body `<GameView />` → `<DesktopGameView />`

(Leave everything else unchanged.)

- [ ] **Step 3: Build** — Run: `dotnet build`. Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "refactor(web): rename GameView/PlayerSetup to Desktop* (no behavior change)"
```

---

### Task 3: Mobile board sizing — `FitContainer` parameter

**Files:**
- Modify: `Components/Game/GameCell.razor`, `GameColumn.razor`, `BoardGrid.razor`

Threads an optional `FitContainer` bool so the same board renders square cells that fit the container on mobile, while desktop keeps its current clamp-based sizing.

- [ ] **Step 1: `GameCell.razor`** — add the parameter and switch the outer div's size style.

In `@code`, add:

```csharp
    [Parameter] public bool FitContainer { get; set; }
```

Change the outer `<div>`'s `width`/`height` style. Replace:

```razor
<div style="position:relative;width:clamp(46px,9.4vh,104px);height:clamp(46px,9.4vh,104px);border-radius:50%;background:var(--cell);box-shadow:inset 0 4px 10px rgba(0,0,0,.85), inset 0 -2px 4px rgba(255,255,255,.05);">
```

with:

```razor
<div style="position:relative;@(FitContainer ? "width:100%;aspect-ratio:1;" : "width:clamp(46px,9.4vh,104px);height:clamp(46px,9.4vh,104px);")border-radius:50%;background:var(--cell);box-shadow:inset 0 4px 10px rgba(0,0,0,.85), inset 0 -2px 4px rgba(255,255,255,.05);">
```

- [ ] **Step 2: `GameColumn.razor`** — add the parameter, pass it down, and switch the column gap.

In `@code`, add:

```csharp
    [Parameter] public bool FitContainer { get; set; }
```

Change the column `<div>` style `gap` and pass `FitContainer` to each `<GameCell>`. Replace the opening div + the `<GameCell ... />`:

```razor
<div @onclick="Drop"
     style="display:flex;flex-direction:column;gap:@(FitContainer ? "2.5%" : "clamp(5px,1.1vw,14px)");border-radius:14px;padding:2px;cursor:@Cursor;background:@ColBg;box-shadow:@ColGlow;@(FitContainer ? "flex:1;min-width:0;" : "")@ShakeStyle">
  @for (int row = 5; row >= 0; row--)
  {
      var r = row;
      var occ = OccupantAt(r);
      <GameCell Occupant="occ"
                FitContainer="FitContainer"
                IsWinning="Session.WinningCells.Contains(new BoardPosition(Col, r))"
                DimmedByWin="@(Session.Winner != null && !Session.WinningCells.Contains(new BoardPosition(Col, r)))"
                JustDropped="@(Session.LastDrop is { } d && d.Col == Col && d.Row == r && Session.Winner == null)"
                DropSeconds="Session.DropSeconds" />
  }
</div>
```

- [ ] **Step 3: `BoardGrid.razor`** — add the parameter, pass it down, and switch the inner row gap + board padding.

In `@code`, add:

```csharp
    [Parameter] public bool FitContainer { get; set; }
```

Change the board outer div padding and the inner flex row, passing `FitContainer` to columns. Replace the markup block:

```razor
<div style="position:relative;padding:@(FitContainer ? "2.5%" : "clamp(10px,1.8vh,20px)");border-radius:26px;background:var(--board);--wf:@WinFlashColor;box-shadow:0 0 0 4px #16259e, 0 16px 0 #0f1a78, 0 24px 44px rgba(0,0,0,.5), inset 0 0 30px rgba(255,255,255,.12);@BoardAnim">
  <div style="display:flex;gap:@(FitContainer ? "2.5%" : "clamp(5px,1.1vw,14px)");@(FitContainer ? "align-items:stretch;" : "")">
    @for (int col = 0; col < 7; col++)
    {
        <GameColumn Col="col" InteractionEnabled="InteractionEnabled" FitContainer="FitContainer" />
    }
  </div>
  @if (Session.Winner != null && Session.Screen == Connect4HoopsArcade.Web.State.AppScreen.Game)
  {
      <WinBanner Color="@WinFlashColor"
                 Text="@(Session.WinBy == "resign" ? "¡VICTORIA!" : "¡CONECTA 4!")" />
  }
</div>
```

- [ ] **Step 4: Build** — Run: `dotnet build`. Expected: 0 errors. (Desktop is unaffected: `FitContainer` defaults to `false`.)

- [ ] **Step 5: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Components/Game/GameCell.razor src/Connect4HoopsArcade.Web/Components/Game/GameColumn.razor src/Connect4HoopsArcade.Web/Components/Game/BoardGrid.razor
git commit -m "feat(web): add FitContainer board sizing for square cells that fit the screen"
```

---

### Task 4: `MobileScoreboard` + `ActionSheet`

**Files:**
- Create: `Components/Game/Mobile/MobileScoreboard.razor`
- Create: `Components/Game/Mobile/ActionSheet.razor`

- [ ] **Step 1: `MobileScoreboard.razor`** — compact both-players bar, tappable (raises `OnTap`). No ⚙ button.

```razor
@using Connect4HoopsArcade.Core.Catalog
@inherits SessionComponentBase

<button @onclick="OnTap" class="mob-scoreboard" aria-label="Abrir opciones">
  <div class="mob-sb-side @(Active(0) ? "on" : "")">
    <div class="mob-sb-ava"><AvatarSvg ColorId="@P(0).ColorId" Face="P(0).Face" Accessory="P(0).Accessory" Glow="@Active(0)" /></div>
    <div class="mob-sb-meta"><span class="mob-sb-name" style="color:@Hex(0)">@P(0).Name</span><span class="mob-sb-score">@Session.Scores[0]</span></div>
  </div>
  <div class="mob-sb-mid">@TurnText <span class="mob-sb-grip">⋯</span></div>
  <div class="mob-sb-side right @(Active(1) ? "on" : "")">
    <div class="mob-sb-meta right"><span class="mob-sb-name" style="color:@Hex(1)">@P(1).Name</span><span class="mob-sb-score">@Session.Scores[1]</span></div>
    <div class="mob-sb-ava"><AvatarSvg ColorId="@P(1).ColorId" Face="P(1).Face" Accessory="P(1).Accessory" Glow="@Active(1)" /></div>
  </div>
</button>

@code {
    [Parameter] public EventCallback OnTap { get; set; }
    private Connect4HoopsArcade.Core.Players.PlayerConfig P(int i) => Session.Players[i];
    private string Hex(int i) => ColorCatalog.HexOf(P(i).ColorId);
    private bool Active(int i) => Session.Current == i && Session.Winner == null;
    private string TurnText => Session.Winner != null ? "🏆"
        : Session.CpuTurn ? "⏳"
        : "● TU TURNO";
}
```

- [ ] **Step 2: `ActionSheet.razor`** — bottom sheet with Reiniciar/Rendirse/Ajustes; scrim closes.

```razor
@inherits SessionComponentBase

@if (Open)
{
  <div class="mob-scrim" @onclick="Close"></div>
  <div class="mob-sheet" @onclick:stopPropagation="true">
    <div class="mob-sheet-grip"></div>
    <button class="mob-sheet-item" @onclick="Reset" disabled="@Session.IsBusy">🔄 Reiniciar</button>
    <button class="mob-sheet-item" @onclick="Resign" disabled="@Session.IsBusy">🏳️ Rendirse</button>
    <button class="mob-sheet-item" @onclick="Settings">⚙ Ajustes</button>
    <button class="mob-sheet-item cancel" @onclick="Close">Cancelar</button>
  </div>
}

@code {
    [Parameter] public bool Open { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    private Task Close() => OnClose.InvokeAsync();
    private async Task Reset() { Session.ResetBoard(); await Close(); }
    private async Task Resign() { Session.Resign(); await Close(); }
    private async Task Settings() { Session.OpenSettings(); await Close(); }
}
```

- [ ] **Step 3: Build** — Run: `dotnet build`. Expected: 0 errors. (Not rendered anywhere yet; just compiles.)

- [ ] **Step 4: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Components/Game/Mobile/MobileScoreboard.razor src/Connect4HoopsArcade.Web/Components/Game/Mobile/ActionSheet.razor
git commit -m "feat(web): add MobileScoreboard (tap-to-open) + ActionSheet"
```

---

### Task 5: `MobileGameView`

**Files:**
- Create: `Components/Screens/MobileGameView.razor`

Presentation-only. Reuses `MobileScoreboard`, `ActionSheet`, `BoardGrid` (with `FitContainer`), `NarratorBubble`. Tap-to-drop is already handled by `GameColumn` (routes through `MoveRouter`).

- [ ] **Step 1: Create the component**

```razor
@using Connect4HoopsArcade.Web.Components.Game
@using Connect4HoopsArcade.Web.Components.Game.Mobile
@inherits SessionComponentBase

<div class="mob-game">
  <MobileScoreboard OnTap="OpenSheet" />

  <div class="mob-board-area">
    <BoardGrid InteractionEnabled="InteractionEnabled" FitContainer="true" />
  </div>

  <div class="mob-narrator"><NarratorBubble /></div>

  <ActionSheet Open="_sheetOpen" OnClose="CloseSheet" />
</div>

@code {
    private bool _sheetOpen;
    private void OpenSheet() { _sheetOpen = true; StateHasChanged(); }
    private void CloseSheet() { _sheetOpen = false; StateHasChanged(); }
    private bool InteractionEnabled => Session.Mode2 == Connect4HoopsArcade.Web.Models.PlayMode.Digital;
}
```

- [ ] **Step 2: Build** — Run: `dotnet build`. Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Components/Screens/MobileGameView.razor
git commit -m "feat(web): add MobileGameView (scoreboard + fit board + sheet)"
```

---

### Task 6: `MobilePlayerSetup`

**Files:**
- Create: `Components/Screens/MobilePlayerSetup.razor`

Reuses `PlayerSetupCard`. Tabs J1/J2 (1P shows only J1). `¡JUGAR!` pinned at bottom.

- [ ] **Step 1: Create the component**

```razor
@using Connect4HoopsArcade.Core.Primitives
@using Connect4HoopsArcade.Core.Players
@using Connect4HoopsArcade.Core.Rules
@using Connect4HoopsArcade.Web.Components.Setup
@inject GameSession Session

<div class="mob-setup">
  <div class="mob-setup-head">
    <button @onclick="Session.GoMode" class="pill-btn">‹ Atrás</button>
    <div class="font-display mob-setup-title">PERSONALIZA</div>
    <div style="width:64px;"></div>
  </div>

  @if (IsTwoPlayer)
  {
    <div class="mob-tabs">
      <button class="mob-tab @(_tab == 0 ? "on" : "")" @onclick="@(() => _tab = 0)">🔵 @Session.Players[0].Name</button>
      <button class="mob-tab @(_tab == 1 ? "on" : "")" @onclick="@(() => _tab = 1)">🔴 @Session.Players[1].Name</button>
    </div>
  }

  <div class="mob-setup-body">
    @if (_tab == 0 || !IsTwoPlayer)
    {
      <PlayerSetupCard Player="Session.Players[0]" Index="0"
                       TakenColorId="@(IsTwoPlayer ? Session.Players[1].ColorId : null)"
                       OnChange="@(p => Session.SetPlayer(0, p))" />
    }
    else
    {
      <PlayerSetupCard Player="Session.Players[1]" Index="1"
                       TakenColorId="@Session.Players[0].ColorId"
                       OnChange="@(p => Session.SetPlayer(1, p))" />
    }
  </div>

  <div class="mob-setup-foot">
    @if (Warning != ColorWarning.None)
    {
      <div class="mob-warn">⚠ @ColorWarningMessages.Message(Warning)</div>
    }
    <button @onclick="Begin" disabled="@StartDisabled" class="font-display mob-play">¡JUGAR! ▶</button>
  </div>
</div>

@code {
    private int _tab;
    private bool IsTwoPlayer => Session.Mode == GameMode.TwoPlayer;
    private ColorWarning Warning => IsTwoPlayer
        ? PlayValidator.CheckColors(Session.Players[0].ColorId, Session.Players[1].ColorId)
        : ColorWarning.None;
    private bool StartDisabled => Warning != ColorWarning.None;
    private void Begin() { if (!StartDisabled) Session.BeginGame(); }
}
```

- [ ] **Step 2: Build** — Run: `dotnet build`. Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Components/Screens/MobilePlayerSetup.razor
git commit -m "feat(web): add MobilePlayerSetup (J1/J2 tabs, pinned PLAY)"
```

---

### Task 7: `AppShell` branching

**Files:**
- Modify: `Components/Layout/AppShell.razor`

- [ ] **Step 1: Inject the viewport service + subscribe**

In `AppShell.razor`, add the inject directive near the other `@inject` lines:

```razor
@inject Connect4HoopsArcade.Web.Services.Abstractions.IViewportService Viewport
```

In `OnInitialized`, add a subscription (alongside the existing `Session.StateChanged += OnChanged;`):

```csharp
        Viewport.OnViewportChanged += OnChanged;
```

In `Dispose`, add:

```csharp
        Viewport.OnViewportChanged -= OnChanged;
```

- [ ] **Step 2: Branch the Setup and Game screens** — replace the `Setup`, `Game`, `Victory`, `Draw` cases:

```razor
    case AppScreen.Setup:
      @if (Viewport.IsMobile) { <MobilePlayerSetup /> } else { <DesktopPlayerSetup /> }
      break;
    case AppScreen.Game:
      @if (Viewport.IsMobile) { <MobileGameView /> } else { <DesktopGameView /> }
      break;
    case AppScreen.Victory:
      @if (Viewport.IsMobile) { <MobileGameView /> } else { <DesktopGameView /> }
      <VictoryModal />
      break;
    case AppScreen.Draw:
      @if (Viewport.IsMobile) { <MobileGameView /> } else { <DesktopGameView /> }
      <DrawModal />
      break;
```

- [ ] **Step 3: Build** — Run: `dotnet build`. Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Components/Layout/AppShell.razor
git commit -m "feat(web): branch Game/Setup to mobile vs desktop views by viewport"
```

---

### Task 8: Responsive CSS (mobile.css + dvh + safe-area + remove rotate-hint)

**Files:**
- Create: `src/Connect4HoopsArcade.Web/wwwroot/css/mobile.css`
- Modify: `src/Connect4HoopsArcade.Web/wwwroot/index.html` (link the stylesheet)
- Modify: `src/Connect4HoopsArcade.Web/wwwroot/css/app.css` (remove rotate-hint forcing; `100vh`→`100dvh`)

- [ ] **Step 1: Link `mobile.css`** — in `wwwroot/index.html`, after the `board.css` link, add:

```html
    <link rel="stylesheet" href="css/mobile.css" />
```

- [ ] **Step 2: Remove the rotate-hint forcing + use dvh** — in `wwwroot/css/app.css`, replace the rotate-hint block:

```css
/* ---- Rotate hint (portrait phones) ---- */
.rotate-hint { display: none; }
@media (orientation: portrait) and (max-width: 900px) {
  .game-screen .board-wrap { display: none !important; }
  .game-screen .rotate-hint { display: flex !important; }
}
```

with:

```css
/* Rotate hint removed — phones use the dedicated MobileGameView. The element stays hidden if present. */
.rotate-hint { display: none !important; }
```

Then update the viewport-height rules in `app.css` to dynamic viewport height (with a static fallback first):

- In the `.arc-root` rule, change `height: 100vh;` to `height: 100vh; height: 100dvh;`
- In the `#app` rule, change `height: 100vh;` to `height: 100vh; height: 100dvh;`
- In the `.boot` rule, change `height: 100vh;` to `height: 100vh; height: 100dvh;`

- [ ] **Step 3: Create `mobile.css`** — the mobile layout styles. `src/Connect4HoopsArcade.Web/wwwroot/css/mobile.css`:

```css
/* ===== Mobile layout (consumed by MobileGameView / MobilePlayerSetup) ===== */

/* ---- In-game ---- */
.mob-game {
  position: absolute; inset: 0; z-index: 5;
  display: flex; flex-direction: column;
  padding: calc(env(safe-area-inset-top) + 8px) calc(env(safe-area-inset-right) + 10px)
           calc(env(safe-area-inset-bottom) + 8px) calc(env(safe-area-inset-left) + 10px);
  gap: 8px; min-height: 0;
}

.mob-scoreboard {
  flex: none; display: flex; align-items: center; gap: 8px;
  width: 100%; padding: 7px 10px; border-radius: 14px; cursor: pointer;
  border: 1.5px solid rgba(255,255,255,.14);
  background: linear-gradient(90deg, rgba(34,211,238,.14), rgba(255,45,111,.14));
  color: #fff; font-family: 'Nunito', system-ui, sans-serif;
}
.mob-scoreboard:active { filter: brightness(1.15); }
.mob-sb-side { display: flex; align-items: center; gap: 7px; flex: 1; min-width: 0; opacity: .55; transition: opacity .2s; }
.mob-sb-side.right { justify-content: flex-end; }
.mob-sb-side.on { opacity: 1; }
.mob-sb-ava { width: 34px; height: 34px; flex: none; }
.mob-sb-meta { display: flex; flex-direction: column; line-height: 1; min-width: 0; }
.mob-sb-meta.right { align-items: flex-end; }
.mob-sb-name { font-weight: 800; font-size: 13px; max-width: 90px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.mob-sb-score { font-family: 'Fredoka', sans-serif; font-weight: 700; font-size: 20px; color: #fff; }
.mob-sb-mid { flex: none; text-align: center; font-size: 10px; font-weight: 900; color: #ffd23f; letter-spacing: .5px; display: flex; flex-direction: column; align-items: center; gap: 1px; }
.mob-sb-grip { color: rgba(255,255,255,.4); font-size: 11px; }

.mob-board-area { flex: 1; min-height: 0; display: flex; align-items: center; justify-content: center; }
/* Board fits the smaller of available width and (available height → keeps 7:6, square cells). */
.mob-board-area > * {
  width: min(100%, calc((100dvh - 200px) * 7 / 6));
  max-width: 100%;
}

.mob-narrator { flex: none; display: flex; justify-content: center; }
.mob-narrator > * { margin-top: 0 !important; }
.mob-narrator :where(span) { white-space: normal !important; }

/* ---- Action sheet ---- */
.mob-scrim { position: absolute; inset: 0; z-index: 40; background: rgba(4,3,10,.5); animation: pop .15s ease; }
.mob-sheet {
  position: absolute; z-index: 41; left: 10px; right: 10px;
  bottom: calc(env(safe-area-inset-bottom) + 10px);
  background: #161031; border: 1px solid rgba(255,255,255,.2); border-radius: 16px;
  padding: 12px; display: flex; flex-direction: column; gap: 8px;
  box-shadow: 0 -12px 30px rgba(0,0,0,.6); animation: slideUp .25s ease;
}
.mob-sheet-grip { width: 38px; height: 4px; border-radius: 2px; background: rgba(255,255,255,.25); margin: 0 auto 4px; }
.mob-sheet-item { cursor: pointer; text-align: left; font-weight: 800; font-size: 16px; color: #fff;
  background: rgba(255,255,255,.07); border: 1px solid rgba(255,255,255,.12); border-radius: 11px; padding: 13px 14px; }
.mob-sheet-item:disabled { opacity: .4; }
.mob-sheet-item.cancel { text-align: center; color: rgba(255,255,255,.6); background: transparent; border-color: transparent; }

/* ---- Mobile setup ---- */
.mob-setup { position: absolute; inset: 0; z-index: 5; display: flex; flex-direction: column;
  padding: calc(env(safe-area-inset-top) + 10px) calc(env(safe-area-inset-right) + 12px)
           calc(env(safe-area-inset-bottom) + 10px) calc(env(safe-area-inset-left) + 12px);
  gap: 10px; min-height: 0; animation: slideUp .4s ease; }
.mob-setup-head { flex: none; display: flex; align-items: center; justify-content: space-between; }
.mob-setup-title { font-weight: 700; font-size: 20px; }
.mob-tabs { flex: none; display: flex; gap: 8px; }
.mob-tab { flex: 1; cursor: pointer; font-weight: 900; font-size: 13px; padding: 9px; border-radius: 11px;
  background: rgba(255,255,255,.05); border: 1.5px solid rgba(255,255,255,.12); color: rgba(255,255,255,.55); }
.mob-tab.on { background: rgba(34,211,238,.18); border-color: #22d3ee; color: #bdf3fb; }
.mob-setup-body { flex: 1; min-height: 0; overflow: auto; display: flex; justify-content: center; }
.mob-setup-body > * { width: 100%; max-width: 460px; }
.mob-setup-foot { flex: none; display: flex; flex-direction: column; align-items: center; gap: 8px; }
.mob-warn { display: flex; align-items: center; gap: 8px; padding: 8px 16px; border-radius: 999px;
  background: rgba(255,45,111,.16); border: 1.5px solid #ff2d6f; color: #ffd0de; font-weight: 800; font-size: 13px; }
.mob-play { cursor: pointer; width: 100%; max-width: 460px; padding: 15px; border-radius: 999px; border: none;
  background: linear-gradient(180deg,#ffd23f,#f5a700); box-shadow: 0 6px 0 #c98800; color: #1a1030;
  font-weight: 700; font-size: 24px; letter-spacing: 1px; }
.mob-play:disabled { background: rgba(255,255,255,.12); box-shadow: none; color: rgba(255,255,255,.5); opacity: .6; }

/* ---- Simple screens / modals on small viewports ---- */
@media (max-width: 767px), (orientation: landscape) and (max-height: 480px) {
  .modal-card { max-width: 92vw !important; max-height: 88dvh !important; overflow: auto !important; }
}
```

> Note: the `200px` chrome budget in `.mob-board-area > *` is the scoreboard + narrator + paddings estimate; if the board looks too small or too big in verification, tune that single value.

- [ ] **Step 4: Build** — Run: `dotnet build`. Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/Connect4HoopsArcade.Web/wwwroot/css/mobile.css src/Connect4HoopsArcade.Web/wwwroot/index.html src/Connect4HoopsArcade.Web/wwwroot/css/app.css
git commit -m "feat(web): mobile.css layout, dvh/safe-area, remove rotate-hint wall"
```

---

### Task 9: Verify in browser + update vault

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Run the app** — `dotnet run --project src/Connect4HoopsArcade.Web`. Open the URL.

- [ ] **Step 2: Mobile checklist** — in the browser devtools, set an iPhone viewport (e.g. 393×852). Verify the 12 acceptance criteria from the spec:
  1. Desktop (wide window) unchanged. 2. No horizontal overflow in portrait. 3. Full 7×6 board fits. 4. Cells are round (not oval). 5. Tapping a column drops a chip. 6. Keyboard 1–7 still drops on desktop. 7. Setup tabs usable + ¡JUGAR! visible. 8. Narrator doesn't cover the board. 9. Victory/Draw modals fit. 10. Settings usable. 11. Rotate portrait↔landscape mid-game: layout adjusts, board/scores preserved. 12. (code review) no duplicated game logic.
  - If the board is mis-sized, tune the `200px` value in `.mob-board-area > *`.

- [ ] **Step 3: Confirm Core tests** — Run: `dotnet test`. Expected: 51 passed.

- [ ] **Step 4: Update `CLAUDE.md`** — under "## Architecture rules" add a bullet, and update "## Status".

Add after the "One move pipeline" bullet in Architecture rules:

```markdown
- **Responsive: dedicated mobile views.** `IViewportService` (JS interop in `Services/`) exposes
  size/breakpoint/orientation + `OnViewportChanged`. `AppShell` renders `MobileGameView`/`MobilePlayerSetup`
  on phones (`IsMobile`) and `DesktopGameView`/`DesktopPlayerSetup` otherwise — **same `GameSession`/`MoveRouter`/
  `Core`, no duplicated game logic**. Mobile board uses `BoardGrid FitContainer="true"` (square cells fit the
  screen). Phones never get the "rotate your device" wall. Simple screens/modals adapt via `mobile.css`
  (`100dvh` + `env(safe-area-inset-*)`).
```

Update the Status line (replace "Next focus" line):

```markdown
**Next focus: item 2 (cast / big-screen projection — companion model reusing `IMoveSource`).**
```

- [ ] **Step 5: Commit**

```bash
git add CLAUDE.md
git commit -m "docs: record mobile-first responsive redesign in vault"
```

---

## Notes for the implementer

- **No Core changes** — keep `Core` pure; this is all Web/UI. `dotnet test` must still show 51 green.
- **Desktop must not change** — `FitContainer` defaults to `false`; desktop renders `DesktopGameView`/`DesktopPlayerSetup` exactly as before.
- **All input still flows through `MoveRouter`** — `GameColumn.Drop` already routes; mobile reuses it (criterion 5 + the one-pipeline rule).
- **Tap-to-drop, not two-tap** — a single column tap drops immediately (existing `GameColumn` behavior); no ghost-preview confirm step (deferred polish).
- The `200px` board-chrome budget is the one magic number to tune during browser verification.
