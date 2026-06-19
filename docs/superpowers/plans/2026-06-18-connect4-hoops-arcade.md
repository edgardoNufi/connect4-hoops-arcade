# Connect 4 Hoops Arcade — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a full-screen arcade Connect 4 game (Blazor WebAssembly) that faithfully reproduces the imported Claude Design, with SVG character tokens, narrator/SFX/music audio, full Connect-4 rules, and a clean digital/physical (sensor) input architecture.

**Architecture:** Three projects — `Core` (pure, dependency-free domain: board, rules, CPU, catalogs; unit-tested with xUnit), `Web` (Blazor WASM: `GameSession` state orchestration, interop services behind interfaces, thin Razor components), and `Core.Tests`. Dependency direction is `Web → Core`; `Core` references nothing. All `IJSRuntime` access is isolated behind `Services/`. A single `IMoveSource → MoveRouter → GameSession.TryDrop` pipeline means digital clicks and physical sensors share one code path; the domain is identical in both modes.

**Tech Stack:** .NET 10, Blazor WebAssembly, xUnit, custom CSS (CSS variables + keyframes), self-hosted Fredoka + Nunito fonts, minimal JS interop (`arcade.js`) for audio/keyboard/localStorage/fullscreen.

**Authoritative design source:** [`../specs/connect4-design-source.html`](../specs/connect4-design-source.html). **Spec:** [`../specs/2026-06-18-connect4-hoops-arcade-design.md`](../specs/2026-06-18-connect4-hoops-arcade-design.md).

---

## File Structure

```
Connect4HoopsArcade.sln
├── .gitignore
├── src/
│   ├── Connect4HoopsArcade.Core/
│   │   ├── Connect4HoopsArcade.Core.csproj
│   │   ├── Primitives/Cell.cs, GameMode.cs, CpuDifficulty.cs, FaceId.cs, AccessoryId.cs, AnimationSpeed.cs
│   │   ├── Catalog/TokenColor.cs, ColorCatalog.cs, FaceCatalog.cs, AccessoryCatalog.cs
│   │   ├── Players/PlayerConfig.cs
│   │   ├── Board/GameBoard.cs, BoardPosition.cs
│   │   ├── Rules/WinDetector.cs, ThreatScanner.cs, PlayValidator.cs, ColorWarning.cs
│   │   └── Ai/CpuStrategy.cs
│   └── Connect4HoopsArcade.Web/
│       ├── Connect4HoopsArcade.Web.csproj
│       ├── Program.cs
│       ├── wwwroot/index.html, css/app.css, css/board.css, fonts/*, js/arcade.js, audio/** (existing)
│       ├── Models/PlayMode.cs, GameSettings.cs
│       ├── State/AppScreen.cs, GameSession.cs
│       ├── Input/IMoveSource.cs, MoveRouter.cs, ScreenInputSource.cs, SensorInputSource.cs
│       ├── Services/Abstractions/*.cs, AudioService.cs, NarratorService.cs,
│       │   SensorConnectionService.cs, KeyboardInputService.cs, SettingsStore.cs
│       ├── Components/App.razor, Layout/AppShell.razor
│       ├── Components/Shared/AvatarSvg.razor, BasketballSvg.razor, ArcadeButton.razor, GlowBackdrop.razor
│       ├── Components/Screens/AttractMode.razor, GameModeSelector.razor, PlayerSetup.razor,
│       │   GameView.razor, SensorTestPanel.razor, SettingsPanel.razor
│       ├── Components/Setup/PlayerSetupCard.razor, ColorPicker.razor, FacePicker.razor, AccessoryPicker.razor
│       ├── Components/Game/BoardGrid.razor, GameColumn.razor, GameCell.razor, ColumnArrows.razor,
│       │   PlayerPanel.razor, NarratorBubble.razor, WinBanner.razor
│       └── Components/Modals/VictoryModal.razor, DrawModal.razor
└── tests/
    └── Connect4HoopsArcade.Core.Tests/
        ├── Connect4HoopsArcade.Core.Tests.csproj
        └── BoardTests.cs, WinDetectorTests.cs, ThreatScannerTests.cs, PlayValidatorTests.cs, CpuStrategyTests.cs
```

**Phases (each ends in a green build + working increment):**
- **Phase 0** — Solution scaffold, git, audio/fonts assets
- **Phase 1** — `Core` domain, fully TDD'd
- **Phase 2** — Web foundations: design system, shell, splash
- **Phase 3** — Mode select + player setup screens
- **Phase 4** — Game board + full move flow + victory/draw
- **Phase 5** — Input architecture: digital/physical, keyboard, sensors
- **Phase 6** — Audio (SFX, voice, music, narrator)
- **Phase 7** — Sensor-test + settings screens
- **Phase 8** — Responsive + final polish & acceptance pass

---

## Phase 0 — Solution Scaffold & Assets

### Task 0.1: Initialize git and solution structure

**Files:**
- Create: `C:/Proyectos/Arcade/.gitignore`
- Create: `Connect4HoopsArcade.sln`

- [ ] **Step 1: Initialize git repo**

Run from `C:/Proyectos/Arcade`:
```bash
git init
```
Expected: "Initialized empty Git repository".

- [ ] **Step 2: Create .gitignore**

Create `C:/Proyectos/Arcade/.gitignore`:
```gitignore
# .NET
bin/
obj/
*.user
.vs/
# Build artifacts
[Dd]ebug/
[Rr]elease/
publish/
# Rider/VS
.idea/
# OS
Thumbs.db
.DS_Store
# Downloads of design source already tracked under docs/
~$*
```

- [ ] **Step 3: Create the solution**

Run from `C:/Proyectos/Arcade`:
```bash
dotnet new sln -n Connect4HoopsArcade
```
Expected: "The template 'Solution File' was created successfully."

- [ ] **Step 4: Commit**

```bash
git add .gitignore Connect4HoopsArcade.sln docs/
git commit -m "chore: init git, solution, and brainstorming docs"
```

### Task 0.2: Create the three projects and wire references

**Files:**
- Create: `src/Connect4HoopsArcade.Core/Connect4HoopsArcade.Core.csproj`
- Create: `src/Connect4HoopsArcade.Web/Connect4HoopsArcade.Web.csproj`
- Create: `tests/Connect4HoopsArcade.Core.Tests/Connect4HoopsArcade.Core.Tests.csproj`

- [ ] **Step 1: Create Core class library**

Run from `C:/Proyectos/Arcade`:
```bash
dotnet new classlib -n Connect4HoopsArcade.Core -o src/Connect4HoopsArcade.Core -f net10.0
```
Then delete the generated `src/Connect4HoopsArcade.Core/Class1.cs`.

- [ ] **Step 2: Create the Blazor WASM app**

```bash
dotnet new blazorwasm -n Connect4HoopsArcade.Web -o src/Connect4HoopsArcade.Web -f net10.0
```

- [ ] **Step 3: Create the xUnit test project**

```bash
dotnet new xunit -n Connect4HoopsArcade.Core.Tests -o tests/Connect4HoopsArcade.Core.Tests -f net10.0
```

- [ ] **Step 4: Add all projects to the solution**

```bash
dotnet sln add src/Connect4HoopsArcade.Core/Connect4HoopsArcade.Core.csproj
dotnet sln add src/Connect4HoopsArcade.Web/Connect4HoopsArcade.Web.csproj
dotnet sln add tests/Connect4HoopsArcade.Core.Tests/Connect4HoopsArcade.Core.Tests.csproj
```

- [ ] **Step 5: Wire project references**

```bash
dotnet add src/Connect4HoopsArcade.Web reference src/Connect4HoopsArcade.Core
dotnet add tests/Connect4HoopsArcade.Core.Tests reference src/Connect4HoopsArcade.Core
```

- [ ] **Step 6: Enable nullable + implicit usings in Core (confirm csproj)**

Ensure `src/Connect4HoopsArcade.Core/Connect4HoopsArcade.Core.csproj` `<PropertyGroup>` contains:
```xml
<TargetFramework>net10.0</TargetFramework>
<ImplicitUsings>enable</ImplicitUsings>
<Nullable>enable</Nullable>
```

- [ ] **Step 7: Build the whole solution**

```bash
dotnet build
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 8: Run the (empty) test project**

```bash
dotnet test
```
Expected: passes with the template's placeholder, or 0 tests if you removed it. (Leave template `UnitTest1.cs` for now.)

- [ ] **Step 9: Commit**

```bash
git add src tests Connect4HoopsArcade.sln
git commit -m "chore: scaffold Core, Web (Blazor WASM), and Core.Tests projects"
```

### Task 0.3: Place audio assets and self-hosted fonts

**Files:**
- Move: `audio/` → `src/Connect4HoopsArcade.Web/wwwroot/audio/`
- Create: `src/Connect4HoopsArcade.Web/wwwroot/fonts/` (Fredoka + Nunito woff2)

- [ ] **Step 1: Move the existing audio folder into wwwroot**

Run from `C:/Proyectos/Arcade`:
```bash
mv audio src/Connect4HoopsArcade.Web/wwwroot/audio
```
Verify: `ls src/Connect4HoopsArcade.Web/wwwroot/audio` shows `game music ui victory voice`.

- [ ] **Step 2: Download self-hosted fonts**

Download these woff2 files into `src/Connect4HoopsArcade.Web/wwwroot/fonts/`. Use the Google Fonts CSS API to resolve current file URLs, then fetch each weight:
```bash
mkdir -p src/Connect4HoopsArcade.Web/wwwroot/fonts
# Fetch the CSS (lists woff2 URLs per weight), then download each .woff2 it references.
curl -s "https://fonts.googleapis.com/css2?family=Fredoka:wght@400;500;600;700&family=Nunito:wght@600;700;800;900&display=swap" -H "User-Agent: Mozilla/5.0" -o /tmp/fonts.css
grep -oE "https://[^)]+\.woff2" /tmp/fonts.css | sort -u
```
Download each listed URL, naming them `fredoka-400.woff2 … fredoka-700.woff2`, `nunito-600.woff2 … nunito-900.woff2` into the `fonts/` folder. (If `curl` to Google is unavailable in the environment, fall back to the CDN `@import` in app.css and revisit self-hosting later — note this deviation in the commit message.)

- [ ] **Step 3: Verify font files exist**

```bash
ls src/Connect4HoopsArcade.Web/wwwroot/fonts
```
Expected: 4 Fredoka weights + 4 Nunito weights (`.woff2`).

- [ ] **Step 4: Commit**

```bash
git add src/Connect4HoopsArcade.Web/wwwroot/audio src/Connect4HoopsArcade.Web/wwwroot/fonts
git commit -m "chore: add audio assets and self-hosted Fredoka/Nunito fonts"
```

---

## Phase 1 — Core Domain (TDD)

> All Phase 1 code lives in `Connect4HoopsArcade.Core` and is covered by xUnit tests in `Connect4HoopsArcade.Core.Tests`. Delete the template `UnitTest1.cs` in Task 1.1 Step 1.

### Task 1.1: Primitives (enums) + remove test template

**Files:**
- Create: `src/Connect4HoopsArcade.Core/Primitives/Cell.cs`, `GameMode.cs`, `CpuDifficulty.cs`, `FaceId.cs`, `AccessoryId.cs`, `AnimationSpeed.cs`
- Delete: `tests/Connect4HoopsArcade.Core.Tests/UnitTest1.cs`

- [ ] **Step 1: Delete the test template file**

```bash
rm tests/Connect4HoopsArcade.Core.Tests/UnitTest1.cs
```

- [ ] **Step 2: Create the enums**

Create `src/Connect4HoopsArcade.Core/Primitives/Cell.cs`:
```csharp
namespace Connect4HoopsArcade.Core.Primitives;

/// <summary>Occupant of a board cell. Player1/Player2 map to player index 0/1.</summary>
public enum Cell
{
    Empty = 0,
    Player1 = 1,
    Player2 = 2,
}

public static class CellExtensions
{
    /// <summary>Cell for a 0-based player index (0 → Player1, 1 → Player2).</summary>
    public static Cell ForPlayer(int playerIndex) => playerIndex == 0 ? Cell.Player1 : Cell.Player2;
}
```

Create `src/Connect4HoopsArcade.Core/Primitives/GameMode.cs`:
```csharp
namespace Connect4HoopsArcade.Core.Primitives;

public enum GameMode { OnePlayer, TwoPlayer }
```

Create `src/Connect4HoopsArcade.Core/Primitives/CpuDifficulty.cs`:
```csharp
namespace Connect4HoopsArcade.Core.Primitives;

public enum CpuDifficulty { Chill, Normal, Sharp }
```

Create `src/Connect4HoopsArcade.Core/Primitives/FaceId.cs`:
```csharp
namespace Connect4HoopsArcade.Core.Primitives;

public enum FaceId { Happy, Confident, Serious, Surprised, Angry }
```

Create `src/Connect4HoopsArcade.Core/Primitives/AccessoryId.cs`:
```csharp
namespace Connect4HoopsArcade.Core.Primitives;

public enum AccessoryId { None, Glasses, Cap, Headband, Crown, Bowtie, Earrings }
```

Create `src/Connect4HoopsArcade.Core/Primitives/AnimationSpeed.cs`:
```csharp
namespace Connect4HoopsArcade.Core.Primitives;

public enum AnimationSpeed { Normal, Fast }
```

- [ ] **Step 3: Build**

```bash
dotnet build src/Connect4HoopsArcade.Core
```
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/Connect4HoopsArcade.Core/Primitives tests/Connect4HoopsArcade.Core.Tests/UnitTest1.cs
git commit -m "feat(core): add domain primitive enums"
```

### Task 1.2: Catalogs (colors, faces, accessories)

**Files:**
- Create: `src/Connect4HoopsArcade.Core/Catalog/TokenColor.cs`, `ColorCatalog.cs`, `FaceCatalog.cs`, `AccessoryCatalog.cs`
- Test: `tests/Connect4HoopsArcade.Core.Tests/ColorCatalogTests.cs`

- [ ] **Step 1: Write the failing test**

Create `tests/Connect4HoopsArcade.Core.Tests/ColorCatalogTests.cs`:
```csharp
using Connect4HoopsArcade.Core.Catalog;
using Xunit;

namespace Connect4HoopsArcade.Core.Tests;

public class ColorCatalogTests
{
    [Fact]
    public void Has_eight_colors_in_design_order()
    {
        Assert.Equal(8, ColorCatalog.All.Count);
        Assert.Equal("pink", ColorCatalog.All[0].Id);
        Assert.Equal("red", ColorCatalog.All[7].Id);
    }

    [Fact]
    public void ById_returns_matching_hex()
    {
        Assert.Equal("#ff3b3b", ColorCatalog.ById("red").Hex);
        Assert.Equal("#ffd23f", ColorCatalog.ById("yellow").Hex);
    }

    [Theory]
    [InlineData(0, 0, 0)]      // identical
    [InlineData(340, 0, 20)]   // pink vs red wraps to 20
    [InlineData(48, 32, 16)]   // yellow vs orange
    public void HueDistance_handles_wraparound(int a, int b, int expected)
    {
        Assert.Equal(expected, ColorCatalog.HueDistance(a, b));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test --filter ColorCatalogTests
```
Expected: FAIL — `ColorCatalog` does not exist (compile error).

- [ ] **Step 3: Implement TokenColor and catalogs**

Create `src/Connect4HoopsArcade.Core/Catalog/TokenColor.cs`:
```csharp
namespace Connect4HoopsArcade.Core.Catalog;

/// <summary>A selectable token color. Hue (0-359) drives the "too similar" validation.</summary>
public sealed record TokenColor(string Id, string Hex, string Name, int Hue);
```

Create `src/Connect4HoopsArcade.Core/Catalog/ColorCatalog.cs`:
```csharp
namespace Connect4HoopsArcade.Core.Catalog;

public static class ColorCatalog
{
    public static readonly IReadOnlyList<TokenColor> All = new[]
    {
        new TokenColor("pink",   "#ff2d6f", "Rosa",     340),
        new TokenColor("cyan",   "#22d3ee", "Cian",     190),
        new TokenColor("yellow", "#ffd23f", "Amarillo", 48),
        new TokenColor("green",  "#2ee86e", "Verde",    140),
        new TokenColor("orange", "#ff8a00", "Naranja",  32),
        new TokenColor("purple", "#b14bff", "Morado",   275),
        new TokenColor("blue",   "#3b82f6", "Azul",     217),
        new TokenColor("red",    "#ff3b3b", "Rojo",     0),
    };

    public static TokenColor ById(string id) =>
        All.FirstOrDefault(c => c.Id == id) ?? All[^1];

    public static string HexOf(string id) => ById(id).Hex;

    /// <summary>Shortest distance between two hues on the 0-359 wheel.</summary>
    public static int HueDistance(int a, int b)
    {
        int d = Math.Abs(a - b) % 360;
        return Math.Min(d, 360 - d);
    }
}
```

Create `src/Connect4HoopsArcade.Core/Catalog/FaceCatalog.cs`:
```csharp
using Connect4HoopsArcade.Core.Primitives;

namespace Connect4HoopsArcade.Core.Catalog;

public sealed record FaceOption(FaceId Id, string Label);

public static class FaceCatalog
{
    public static readonly IReadOnlyList<FaceOption> All = new[]
    {
        new FaceOption(FaceId.Happy,     "Feliz"),
        new FaceOption(FaceId.Confident, "Confiado"),
        new FaceOption(FaceId.Serious,   "Serio"),
        new FaceOption(FaceId.Surprised, "Sorprendido"),
        new FaceOption(FaceId.Angry,     "Enojado"),
    };
}
```

Create `src/Connect4HoopsArcade.Core/Catalog/AccessoryCatalog.cs`:
```csharp
using Connect4HoopsArcade.Core.Primitives;

namespace Connect4HoopsArcade.Core.Catalog;

public sealed record AccessoryOption(AccessoryId Id, string Label);

public static class AccessoryCatalog
{
    public static readonly IReadOnlyList<AccessoryOption> All = new[]
    {
        new AccessoryOption(AccessoryId.None,     "Ninguno"),
        new AccessoryOption(AccessoryId.Glasses,  "Lentes"),
        new AccessoryOption(AccessoryId.Cap,      "Gorra"),
        new AccessoryOption(AccessoryId.Headband, "Banda"),
        new AccessoryOption(AccessoryId.Crown,    "Corona"),
        new AccessoryOption(AccessoryId.Bowtie,   "Moño"),
        new AccessoryOption(AccessoryId.Earrings, "Aretes"),
    };
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
dotnet test --filter ColorCatalogTests
```
Expected: PASS (3 tests / 5 cases).

- [ ] **Step 5: Commit**

```bash
git add src/Connect4HoopsArcade.Core/Catalog tests/Connect4HoopsArcade.Core.Tests/ColorCatalogTests.cs
git commit -m "feat(core): add color/face/accessory catalogs with hue distance"
```

### Task 1.3: PlayerConfig + BoardPosition

**Files:**
- Create: `src/Connect4HoopsArcade.Core/Players/PlayerConfig.cs`, `src/Connect4HoopsArcade.Core/Board/BoardPosition.cs`

- [ ] **Step 1: Create PlayerConfig record**

Create `src/Connect4HoopsArcade.Core/Players/PlayerConfig.cs`:
```csharp
using Connect4HoopsArcade.Core.Primitives;

namespace Connect4HoopsArcade.Core.Players;

/// <summary>Immutable player setup. Mutate via `with`.</summary>
public sealed record PlayerConfig(
    string Name,
    string ColorId,
    FaceId Face,
    AccessoryId Accessory,
    bool IsCpu = false)
{
    public static PlayerConfig DefaultP1 => new("Jugador 1", "red",    FaceId.Happy,     AccessoryId.Cap);
    public static PlayerConfig DefaultP2 => new("Jugador 2", "yellow", FaceId.Confident, AccessoryId.Crown);
    public static PlayerConfig DefaultCpu => new("CPU",      "yellow", FaceId.Serious,   AccessoryId.None, IsCpu: true);
}
```

- [ ] **Step 2: Create BoardPosition struct**

Create `src/Connect4HoopsArcade.Core/Board/BoardPosition.cs`:
```csharp
namespace Connect4HoopsArcade.Core.Board;

/// <summary>A cell coordinate. Col 0-6 (left→right), Row 0-5 (0 = bottom).</summary>
public readonly record struct BoardPosition(int Col, int Row);
```

- [ ] **Step 3: Build**

```bash
dotnet build src/Connect4HoopsArcade.Core
```
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/Connect4HoopsArcade.Core/Players src/Connect4HoopsArcade.Core/Board/BoardPosition.cs
git commit -m "feat(core): add PlayerConfig and BoardPosition"
```

### Task 1.4: Board (grid + drop mechanics) — TDD

**Files:**
- Create: `src/Connect4HoopsArcade.Core/Board/GameBoard.cs`
- Test: `tests/Connect4HoopsArcade.Core.Tests/BoardTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Connect4HoopsArcade.Core.Tests/BoardTests.cs`:
```csharp
using Connect4HoopsArcade.Core.Board;
using Connect4HoopsArcade.Core.Primitives;
using Xunit;

namespace Connect4HoopsArcade.Core.Tests;

public class BoardTests
{
    [Fact]
    public void New_board_is_empty_7x6()
    {
        var b = new GameBoard();
        Assert.Equal(7, GameBoard.Columns);
        Assert.Equal(6, GameBoard.Rows);
        for (int c = 0; c < 7; c++)
            for (int r = 0; r < 6; r++)
                Assert.Equal(Cell.Empty, b[c, r]);
    }

    [Fact]
    public void LowestRow_is_zero_on_empty_column()
    {
        var b = new GameBoard();
        Assert.Equal(0, b.LowestRow(3));
    }

    [Fact]
    public void Drop_stacks_from_the_bottom_up()
    {
        var b = new GameBoard();
        Assert.Equal(0, b.Drop(2, Cell.Player1));
        Assert.Equal(1, b.Drop(2, Cell.Player2));
        Assert.Equal(Cell.Player1, b[2, 0]);
        Assert.Equal(Cell.Player2, b[2, 1]);
    }

    [Fact]
    public void Drop_returns_minus_one_when_column_full()
    {
        var b = new GameBoard();
        for (int i = 0; i < 6; i++) b.Drop(0, Cell.Player1);
        Assert.True(b.IsColumnFull(0));
        Assert.Equal(-1, b.LowestRow(0));
        Assert.Equal(-1, b.Drop(0, Cell.Player2));
    }

    [Fact]
    public void IsBoardFull_true_only_when_every_top_cell_filled()
    {
        var b = new GameBoard();
        Assert.False(b.IsBoardFull());
        for (int c = 0; c < 7; c++)
            for (int r = 0; r < 6; r++)
                b.Drop(c, Cell.Player1);
        Assert.True(b.IsBoardFull());
    }

    [Fact]
    public void Clone_is_independent()
    {
        var b = new GameBoard();
        b.Drop(1, Cell.Player1);
        var c = b.Clone();
        c.Drop(1, Cell.Player2);
        Assert.Equal(Cell.Empty, b[1, 1]);   // original unchanged
        Assert.Equal(Cell.Player2, c[1, 1]);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test --filter BoardTests
```
Expected: FAIL — `GameBoard` does not exist.

- [ ] **Step 3: Implement Board**

Create `src/Connect4HoopsArcade.Core/Board/GameBoard.cs`:
```csharp
using Connect4HoopsArcade.Core.Primitives;

namespace Connect4HoopsArcade.Core.Board;

/// <summary>7×6 Connect-4 grid. Indexed [col, row] with row 0 = bottom.</summary>
public sealed class GameBoard
{
    public const int Columns = 7;
    public const int Rows = 6;

    private readonly Cell[,] _cells;

    public GameBoard() => _cells = new Cell[Columns, Rows];
    private GameBoard(Cell[,] cells) => _cells = cells;

    public Cell this[int col, int row] => _cells[col, row];

    /// <summary>First empty row (0 = bottom) in a column, or -1 if full.</summary>
    public int LowestRow(int col)
    {
        for (int r = 0; r < Rows; r++)
            if (_cells[col, r] == Cell.Empty) return r;
        return -1;
    }

    public bool IsColumnFull(int col) => LowestRow(col) < 0;

    public bool IsBoardFull()
    {
        for (int c = 0; c < Columns; c++)
            if (_cells[c, Rows - 1] == Cell.Empty) return false;
        return true;
    }

    /// <summary>Places a cell at the lowest free row of the column. Returns the row, or -1 if full.</summary>
    public int Drop(int col, Cell cell)
    {
        int r = LowestRow(col);
        if (r < 0) return -1;
        _cells[col, r] = cell;
        return r;
    }

    public GameBoard Clone() => new((Cell[,])_cells.Clone());
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test --filter BoardTests
```
Expected: PASS (6 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Connect4HoopsArcade.Core/Board/GameBoard.cs tests/Connect4HoopsArcade.Core.Tests/BoardTests.cs
git commit -m "feat(core): add Board with drop/full/clone mechanics"
```

### Task 1.5: WinDetector (4-direction) — TDD

**Files:**
- Create: `src/Connect4HoopsArcade.Core/Rules/WinDetector.cs`
- Test: `tests/Connect4HoopsArcade.Core.Tests/WinDetectorTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Connect4HoopsArcade.Core.Tests/WinDetectorTests.cs`:
```csharp
using Connect4HoopsArcade.Core.Board;
using Connect4HoopsArcade.Core.Primitives;
using Connect4HoopsArcade.Core.Rules;
using Xunit;

namespace Connect4HoopsArcade.Core.Tests;

public class WinDetectorTests
{
    // Drops a cell and returns (board, col, row) of the last move.
    private static (GameBoard b, int col, int row) Play(params (int col, Cell cell)[] moves)
    {
        var b = new GameBoard();
        int lastCol = 0, lastRow = 0;
        foreach (var (col, cell) in moves)
        {
            lastRow = b.Drop(col, cell);
            lastCol = col;
        }
        return (b, lastCol, lastRow);
    }

    [Fact]
    public void Detects_horizontal_four()
    {
        var (b, col, row) = Play(
            (0, Cell.Player1), (1, Cell.Player1), (2, Cell.Player1), (3, Cell.Player1));
        var line = WinDetector.FindWinningLine(b, col, row, Cell.Player1);
        Assert.NotNull(line);
        Assert.Equal(4, line!.Count);
    }

    [Fact]
    public void Detects_vertical_four()
    {
        var (b, col, row) = Play(
            (4, Cell.Player2), (4, Cell.Player2), (4, Cell.Player2), (4, Cell.Player2));
        var line = WinDetector.FindWinningLine(b, col, row, Cell.Player2);
        Assert.NotNull(line);
        Assert.Equal(4, line!.Count);
    }

    [Fact]
    public void Detects_diagonal_up_right()
    {
        // Build a / diagonal for Player1 at columns 0..3.
        var b = new GameBoard();
        b.Drop(0, Cell.Player1);
        b.Drop(1, Cell.Player2); b.Drop(1, Cell.Player1);
        b.Drop(2, Cell.Player2); b.Drop(2, Cell.Player2); b.Drop(2, Cell.Player1);
        b.Drop(3, Cell.Player2); b.Drop(3, Cell.Player2); b.Drop(3, Cell.Player2);
        int row = b.Drop(3, Cell.Player1);
        var line = WinDetector.FindWinningLine(b, 3, row, Cell.Player1);
        Assert.NotNull(line);
        Assert.Equal(4, line!.Count);
    }

    [Fact]
    public void Detects_diagonal_down_right()
    {
        // Build a \ diagonal for Player1 at columns 0..3.
        var b = new GameBoard();
        b.Drop(0, Cell.Player2); b.Drop(0, Cell.Player2); b.Drop(0, Cell.Player2);
        int r0 = b.Drop(0, Cell.Player1);
        b.Drop(1, Cell.Player2); b.Drop(1, Cell.Player2); b.Drop(1, Cell.Player1);
        b.Drop(2, Cell.Player2); b.Drop(2, Cell.Player1);
        b.Drop(3, Cell.Player1);
        // The top-left to bottom-right line: (0,3),(1,2),(2,1),(3,0) — detect from (0, r0).
        var line = WinDetector.FindWinningLine(b, 0, r0, Cell.Player1);
        Assert.NotNull(line);
        Assert.Equal(4, line!.Count);
    }

    [Fact]
    public void No_win_returns_null()
    {
        var (b, col, row) = Play(
            (0, Cell.Player1), (1, Cell.Player1), (2, Cell.Player1));
        Assert.Null(WinDetector.FindWinningLine(b, col, row, Cell.Player1));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test --filter WinDetectorTests
```
Expected: FAIL — `WinDetector` does not exist.

- [ ] **Step 3: Implement WinDetector**

Create `src/Connect4HoopsArcade.Core/Rules/WinDetector.cs`:
```csharp
using Connect4HoopsArcade.Core.Board;
using Connect4HoopsArcade.Core.Primitives;

namespace Connect4HoopsArcade.Core.Rules;

public static class WinDetector
{
    private static readonly (int dc, int dr)[] Directions =
        { (1, 0), (0, 1), (1, 1), (1, -1) };

    /// <summary>
    /// If placing <paramref name="cell"/> at (col,row) completes 4+ in a row, returns exactly the 4
    /// winning positions (containing the placed cell); otherwise null.
    /// </summary>
    public static IReadOnlyList<BoardPosition>? FindWinningLine(GameBoard board, int col, int row, Cell cell)
    {
        foreach (var (dc, dr) in Directions)
        {
            var line = BuildLine(board, col, row, dc, dr, cell);
            if (line.Count >= 4)
            {
                int idx = line.FindIndex(p => p.Col == col && p.Row == row);
                int start = Math.Max(0, Math.Min(idx - 0, line.Count - 4));
                return line.GetRange(start, 4);
            }
        }
        return null;
    }

    private static List<BoardPosition> BuildLine(GameBoard board, int col, int row, int dc, int dr, Cell cell)
    {
        var line = new List<BoardPosition> { new(col, row) };
        Extend(board, col, row, dc, dr, cell, line, append: true);
        Extend(board, col, row, -dc, -dr, cell, line, append: false);
        return line;
    }

    private static void Extend(GameBoard board, int col, int row, int dc, int dr, Cell cell,
        List<BoardPosition> line, bool append)
    {
        int c = col + dc, r = row + dr;
        while (c >= 0 && c < GameBoard.Columns && r >= 0 && r < GameBoard.Rows && board[c, r] == cell)
        {
            if (append) line.Add(new(c, r));
            else line.Insert(0, new(c, r));
            c += dc; r += dr;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test --filter WinDetectorTests
```
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Connect4HoopsArcade.Core/Rules/WinDetector.cs tests/Connect4HoopsArcade.Core.Tests/WinDetectorTests.cs
git commit -m "feat(core): add WinDetector for horizontal/vertical/diagonal wins"
```

### Task 1.6: ThreatScanner — TDD

**Files:**
- Create: `src/Connect4HoopsArcade.Core/Rules/ThreatScanner.cs`
- Test: `tests/Connect4HoopsArcade.Core.Tests/ThreatScannerTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Connect4HoopsArcade.Core.Tests/ThreatScannerTests.cs`:
```csharp
using Connect4HoopsArcade.Core.Board;
using Connect4HoopsArcade.Core.Primitives;
using Connect4HoopsArcade.Core.Rules;
using Xunit;

namespace Connect4HoopsArcade.Core.Tests;

public class ThreatScannerTests
{
    [Fact]
    public void Detects_immediate_winning_threat()
    {
        var b = new GameBoard();
        b.Drop(0, Cell.Player1);
        b.Drop(1, Cell.Player1);
        b.Drop(2, Cell.Player1); // three in a row; col 3 (or -1 left) would win
        Assert.True(ThreatScanner.HasImmediateThreat(b, Cell.Player1));
    }

    [Fact]
    public void No_threat_on_empty_board()
    {
        Assert.False(ThreatScanner.HasImmediateThreat(new GameBoard(), Cell.Player1));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test --filter ThreatScannerTests
```
Expected: FAIL — `ThreatScanner` does not exist.

- [ ] **Step 3: Implement ThreatScanner**

Create `src/Connect4HoopsArcade.Core/Rules/ThreatScanner.cs`:
```csharp
using Connect4HoopsArcade.Core.Board;
using Connect4HoopsArcade.Core.Primitives;

namespace Connect4HoopsArcade.Core.Rules;

public static class ThreatScanner
{
    /// <summary>True if <paramref name="cell"/> has a move that immediately wins.</summary>
    public static bool HasImmediateThreat(GameBoard board, Cell cell)
    {
        for (int c = 0; c < GameBoard.Columns; c++)
        {
            int r = board.LowestRow(c);
            if (r < 0) continue;
            var trial = board.Clone();
            trial.Drop(c, cell);
            if (WinDetector.FindWinningLine(trial, c, r, cell) is not null) return true;
        }
        return false;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test --filter ThreatScannerTests
```
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Connect4HoopsArcade.Core/Rules/ThreatScanner.cs tests/Connect4HoopsArcade.Core.Tests/ThreatScannerTests.cs
git commit -m "feat(core): add ThreatScanner for 3-in-a-row detection"
```

### Task 1.7: PlayValidator (color warnings) — TDD

**Files:**
- Create: `src/Connect4HoopsArcade.Core/Rules/ColorWarning.cs`, `src/Connect4HoopsArcade.Core/Rules/PlayValidator.cs`
- Test: `tests/Connect4HoopsArcade.Core.Tests/PlayValidatorTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Connect4HoopsArcade.Core.Tests/PlayValidatorTests.cs`:
```csharp
using Connect4HoopsArcade.Core.Rules;
using Xunit;

namespace Connect4HoopsArcade.Core.Tests;

public class PlayValidatorTests
{
    [Fact]
    public void Same_color_is_a_warning()
    {
        var w = PlayValidator.CheckColors("red", "red");
        Assert.Equal(ColorWarning.Same, w);
    }

    [Fact]
    public void Similar_hues_are_a_warning()
    {
        // yellow (48) vs orange (32) → hue distance 16 < 30
        Assert.Equal(ColorWarning.Similar, PlayValidator.CheckColors("yellow", "orange"));
    }

    [Fact]
    public void Distinct_colors_are_ok()
    {
        Assert.Equal(ColorWarning.None, PlayValidator.CheckColors("red", "cyan"));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test --filter PlayValidatorTests
```
Expected: FAIL — `PlayValidator` / `ColorWarning` do not exist.

- [ ] **Step 3: Implement ColorWarning and PlayValidator**

Create `src/Connect4HoopsArcade.Core/Rules/ColorWarning.cs`:
```csharp
namespace Connect4HoopsArcade.Core.Rules;

public enum ColorWarning { None, Same, Similar }

public static class ColorWarningMessages
{
    public static string? Message(ColorWarning w) => w switch
    {
        ColorWarning.Same    => "Mismo color: elige uno distinto para cada jugador.",
        ColorWarning.Similar => "Colores muy parecidos: podrían confundirse en el tablero.",
        _ => null,
    };
}
```

Create `src/Connect4HoopsArcade.Core/Rules/PlayValidator.cs`:
```csharp
using Connect4HoopsArcade.Core.Catalog;

namespace Connect4HoopsArcade.Core.Rules;

public static class PlayValidator
{
    public const int SimilarHueThreshold = 30;

    /// <summary>Validates two players' chosen color ids (only meaningful in 2-player mode).</summary>
    public static ColorWarning CheckColors(string colorA, string colorB)
    {
        if (colorA == colorB) return ColorWarning.Same;
        int dist = ColorCatalog.HueDistance(ColorCatalog.ById(colorA).Hue, ColorCatalog.ById(colorB).Hue);
        return dist < SimilarHueThreshold ? ColorWarning.Similar : ColorWarning.None;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test --filter PlayValidatorTests
```
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/Connect4HoopsArcade.Core/Rules/ColorWarning.cs src/Connect4HoopsArcade.Core/Rules/PlayValidator.cs tests/Connect4HoopsArcade.Core.Tests/PlayValidatorTests.cs
git commit -m "feat(core): add color-conflict validation"
```

### Task 1.8: CpuStrategy — TDD

**Files:**
- Create: `src/Connect4HoopsArcade.Core/Ai/CpuStrategy.cs`
- Test: `tests/Connect4HoopsArcade.Core.Tests/CpuStrategyTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Connect4HoopsArcade.Core.Tests/CpuStrategyTests.cs`:
```csharp
using Connect4HoopsArcade.Core.Ai;
using Connect4HoopsArcade.Core.Board;
using Connect4HoopsArcade.Core.Primitives;
using Xunit;

namespace Connect4HoopsArcade.Core.Tests;

public class CpuStrategyTests
{
    [Fact]
    public void Takes_winning_move_when_available()
    {
        var b = new GameBoard();
        b.Drop(0, Cell.Player2);
        b.Drop(1, Cell.Player2);
        b.Drop(2, Cell.Player2); // CPU (Player2) can win at col 3
        int col = CpuStrategy.ChooseColumn(b, CpuDifficulty.Normal);
        Assert.Equal(3, col);
    }

    [Fact]
    public void Blocks_opponent_win_when_not_chill()
    {
        var b = new GameBoard();
        b.Drop(0, Cell.Player1);
        b.Drop(1, Cell.Player1);
        b.Drop(2, Cell.Player1); // opponent threatens col 3
        int col = CpuStrategy.ChooseColumn(b, CpuDifficulty.Sharp);
        Assert.Equal(3, col);
    }

    [Fact]
    public void Prefers_center_on_empty_board_when_sharp()
    {
        int col = CpuStrategy.ChooseColumn(new GameBoard(), CpuDifficulty.Sharp);
        Assert.Equal(3, col);
    }

    [Fact]
    public void Returns_a_playable_column()
    {
        var b = new GameBoard();
        int col = CpuStrategy.ChooseColumn(b, CpuDifficulty.Normal);
        Assert.InRange(col, 0, 6);
        Assert.False(b.IsColumnFull(col));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test --filter CpuStrategyTests
```
Expected: FAIL — `CpuStrategy` does not exist.

- [ ] **Step 3: Implement CpuStrategy**

Create `src/Connect4HoopsArcade.Core/Ai/CpuStrategy.cs`:
```csharp
using Connect4HoopsArcade.Core.Board;
using Connect4HoopsArcade.Core.Primitives;
using Connect4HoopsArcade.Core.Rules;

namespace Connect4HoopsArcade.Core.Ai;

/// <summary>CPU is always Player2. Mirrors the imported design's heuristic.</summary>
public static class CpuStrategy
{
    private static readonly int[] SharpOrder  = { 3, 2, 4, 1, 5, 0, 6 };
    private static readonly int[] NormalOrder = { 3, 4, 2, 5, 1, 6, 0 };

    public static int ChooseColumn(GameBoard board, CpuDifficulty difficulty, Random? rng = null)
    {
        // 1) Win if possible.
        int win = WinningColumnFor(board, Cell.Player2);
        if (win >= 0) return win;

        // 2) Block opponent (unless chill).
        if (difficulty != CpuDifficulty.Chill)
        {
            int block = WinningColumnFor(board, Cell.Player1);
            if (block >= 0) return block;
        }

        // 3) Positional preference.
        var order = difficulty == CpuDifficulty.Sharp ? SharpOrder : NormalOrder;
        var available = order.Where(c => board.LowestRow(c) >= 0).ToList();
        if (available.Count == 0) return -1;

        if (difficulty == CpuDifficulty.Chill)
        {
            rng ??= Random.Shared;
            return available[rng.Next(available.Count)];
        }
        return available[0];
    }

    private static int WinningColumnFor(GameBoard board, Cell cell)
    {
        for (int c = 0; c < GameBoard.Columns; c++)
        {
            int r = board.LowestRow(c);
            if (r < 0) continue;
            var trial = board.Clone();
            trial.Drop(c, cell);
            if (WinDetector.FindWinningLine(trial, c, r, cell) is not null) return c;
        }
        return -1;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test --filter CpuStrategyTests
```
Expected: PASS (4 tests).

- [ ] **Step 5: Run the full Core test suite**

```bash
dotnet test
```
Expected: ALL PASS (Board, WinDetector, ThreatScanner, PlayValidator, CpuStrategy, ColorCatalog).

- [ ] **Step 6: Commit**

```bash
git add src/Connect4HoopsArcade.Core/Ai/CpuStrategy.cs tests/Connect4HoopsArcade.Core.Tests/CpuStrategyTests.cs
git commit -m "feat(core): add CPU strategy (win/block/center-out)"
```

---

## Phase 2 — Web Foundations: Design System, Shell, Splash

> Phase 2 establishes the visual system (CSS tokens, fonts, keyframes), the SVG avatar component, the
> `GameSession` state container, screen routing, and the splash screen. After this phase the app builds
> and shows an animated attract screen that navigates to a placeholder.

### Task 2.1: index.html + CSS design system (tokens, fonts, keyframes)

**Files:**
- Modify: `src/Connect4HoopsArcade.Web/wwwroot/index.html`
- Create: `src/Connect4HoopsArcade.Web/wwwroot/css/app.css`
- Create: `src/Connect4HoopsArcade.Web/wwwroot/css/board.css`
- Delete: default `css/app.css` content from template if present (overwrite).

- [ ] **Step 1: Overwrite index.html**

Overwrite `src/Connect4HoopsArcade.Web/wwwroot/index.html`:
```html
<!DOCTYPE html>
<html lang="es">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no" />
    <title>Connect 4 Hoops Arcade</title>
    <base href="/" />
    <link rel="stylesheet" href="css/app.css" />
    <link rel="stylesheet" href="css/board.css" />
</head>
<body>
    <div id="app">
        <div class="boot">Cargando…</div>
    </div>
    <div id="blazor-error-ui">
        Ocurrió un error. <a href="" class="reload">Recargar</a>
        <a class="dismiss">🗙</a>
    </div>
    <script src="_framework/blazor.webassembly.js"></script>
    <script src="js/arcade.js"></script>
</body>
</html>
```

- [ ] **Step 2: Create app.css (fonts, tokens, base, keyframes)**

Create `src/Connect4HoopsArcade.Web/wwwroot/css/app.css`. Copy the `@keyframes` blocks **verbatim** from the design source (lines 21-38 of `docs/superpowers/specs/connect4-design-source.html`):
```css
/* ---- Self-hosted fonts ---- */
@font-face { font-family: 'Fredoka'; font-weight: 400; src: url('../fonts/fredoka-400.woff2') format('woff2'); font-display: swap; }
@font-face { font-family: 'Fredoka'; font-weight: 500; src: url('../fonts/fredoka-500.woff2') format('woff2'); font-display: swap; }
@font-face { font-family: 'Fredoka'; font-weight: 600; src: url('../fonts/fredoka-600.woff2') format('woff2'); font-display: swap; }
@font-face { font-family: 'Fredoka'; font-weight: 700; src: url('../fonts/fredoka-700.woff2') format('woff2'); font-display: swap; }
@font-face { font-family: 'Nunito'; font-weight: 600; src: url('../fonts/nunito-600.woff2') format('woff2'); font-display: swap; }
@font-face { font-family: 'Nunito'; font-weight: 700; src: url('../fonts/nunito-700.woff2') format('woff2'); font-display: swap; }
@font-face { font-family: 'Nunito'; font-weight: 800; src: url('../fonts/nunito-800.woff2') format('woff2'); font-display: swap; }
@font-face { font-family: 'Nunito'; font-weight: 900; src: url('../fonts/nunito-900.woff2') format('woff2'); font-display: swap; }

/* ---- Design tokens ---- */
:root {
  --c-pink:   #ff2d6f;
  --c-cyan:   #22d3ee;
  --c-yellow: #ffd23f;
  --c-green:  #2ee86e;
  --c-orange: #ff8a00;
  --c-purple: #b14bff;
  --c-blue:   #3b82f6;
  --c-red:    #ff3b3b;
  --ink:      #1a1030;
  --bg:       radial-gradient(120% 90% at 50% 0%, #211a4d 0%, #120e2e 45%, #08060f 100%);
  --board:    linear-gradient(180deg, #2546ff, #1a30c4);
  --cell:     radial-gradient(circle at 38% 34%, #0a0820, #05040f);
}

* { box-sizing: border-box; }
html, body { margin: 0; padding: 0; height: 100%; }
::-webkit-scrollbar { width: 0; height: 0; }
body { background: #08060f; color: #fff; font-family: 'Nunito', system-ui, sans-serif; overflow: hidden; }
.boot { display: flex; align-items: center; justify-content: center; height: 100vh; color: #22d3ee; font-weight: 800; }
#app { height: 100vh; }
.arc-root { font-family: 'Nunito', system-ui, sans-serif; position: relative; width: 100%; height: 100vh; overflow: hidden; background: var(--bg); color: #fff; }
.font-display { font-family: 'Fredoka', system-ui, sans-serif; }

#blazor-error-ui { display: none; position: fixed; bottom: 0; left: 0; right: 0; background: #ff2d6f; color: #fff; padding: 10px; text-align: center; z-index: 100; }
#blazor-error-ui .reload, #blazor-error-ui .dismiss { color: #fff; margin-left: 8px; cursor: pointer; }

/* ---- Keyframes (verbatim from design source) ---- */
@keyframes drop { 0% { transform: translateY(-580px) scaleY(1); animation-timing-function: cubic-bezier(.5,0,.85,.4); } 50% { transform: translateY(0) scaleY(1); animation-timing-function: cubic-bezier(.2,.7,.4,1); } 57% { transform: translateY(0) scale(1.16,.82); } 66% { transform: translateY(-46px) scale(.94,1.06); animation-timing-function: cubic-bezier(.5,0,.85,.4); } 80% { transform: translateY(0) scale(1.07,.93); } 90% { transform: translateY(-14px) scale(1,1); } 100% { transform: translateY(0) scale(1,1); } }
@keyframes land { 0% { transform: scale(.25); opacity: .85; } 100% { transform: scale(2); opacity: 0; } }
@keyframes connectBadge { 0% { transform: translate(-50%,-50%) scale(.3) rotate(-10deg); } 45% { transform: translate(-50%,-50%) scale(1.18) rotate(4deg); } 65% { transform: translate(-50%,-50%) scale(.94) rotate(-2deg); } 100% { transform: translate(-50%,-50%) scale(1) rotate(0); } }
@keyframes boardWin { 0%,100% { box-shadow: 0 0 0 4px #16259e, 0 16px 0 #0f1a78, 0 24px 44px rgba(0,0,0,.5), inset 0 0 30px rgba(255,255,255,.12); } 50% { box-shadow: 0 0 0 4px #16259e, 0 16px 0 #0f1a78, 0 0 70px 10px var(--wf), inset 0 0 30px rgba(255,255,255,.12); } }
@keyframes shake { 0%,100% { transform: translateX(0); } 20% { transform: translateX(-7px); } 40% { transform: translateX(7px); } 60% { transform: translateX(-5px); } 80% { transform: translateX(5px); } }
@keyframes floatY { 0%,100% { transform: translateY(0) rotate(-6deg); } 50% { transform: translateY(-28px) rotate(6deg); } }
@keyframes ballBounce { 0%,100% { transform: translateY(0) scale(1,1); } 45% { transform: translateY(-130px) scale(.94,1.06); } 50% { transform: translateY(-138px) scale(1.04,.96); } 90% { transform: translateY(0) scale(1.08,.92); } }
@keyframes glowPulse { 0%,100% { opacity: .55; transform: scale(1); } 50% { opacity: 1; transform: scale(1.06); } }
@keyframes winPulse { 0%,100% { box-shadow: 0 0 0 0 rgba(255,255,255,.95), 0 0 22px 6px var(--wg); transform: scale(1); } 50% { box-shadow: 0 0 0 5px rgba(255,255,255,1), 0 0 40px 14px var(--wg); transform: scale(1.07); } }
@keyframes confettiFall { 0% { transform: translateY(-30px) rotate(0); opacity: 1; } 100% { transform: translateY(105vh) rotate(720deg); opacity: 1; } }
@keyframes pop { 0% { transform: scale(.55); } 60% { transform: scale(1.12); } 100% { transform: scale(1); } }
@keyframes slideUp { 0% { transform: translateY(34px); } 100% { transform: translateY(0); } }
@keyframes blink { 0%,100% { opacity: 1; } 50% { opacity: .15; } }
@keyframes scan { 0% { background-position: 0 0; } 100% { background-position: 0 22px; } }
@keyframes spin { to { transform: rotate(360deg); } }
@keyframes turnGlow { 0%,100% { box-shadow: 0 0 0 2px var(--tc), 0 0 22px -2px var(--tc); } 50% { box-shadow: 0 0 0 4px var(--tc), 0 0 48px 2px var(--tc); } }
@keyframes arrowBounce { 0%,100% { transform: translateY(0); } 50% { transform: translateY(9px); } }
@keyframes badgePulse { 0%,100% { transform: scale(1); } 50% { transform: scale(1.08); } }

/* ---- Rotate hint (portrait phones) ---- */
.rotate-hint { display: none; }
@media (orientation: portrait) and (max-width: 900px) {
  .game-screen .board-wrap { display: none !important; }
  .game-screen .rotate-hint { display: flex !important; }
}
```

- [ ] **Step 3: Create board.css (placeholder for board-specific styles, filled in Phase 4)**

Create `src/Connect4HoopsArcade.Web/wwwroot/css/board.css`:
```css
/* Board, cell, panel styles — populated in Phase 4. */
```

- [ ] **Step 4: Remove the template's default styles that conflict**

Delete the Blazor template's default `wwwroot/css/` bootstrap files and the `<link>` lines they came with were already removed in Step 1. Verify no `bootstrap` folder is referenced:
```bash
rm -rf src/Connect4HoopsArcade.Web/wwwroot/css/bootstrap
```
(If the template put `app.css` elsewhere, this overwrite supersedes it.)

- [ ] **Step 5: Build**

```bash
dotnet build src/Connect4HoopsArcade.Web
```
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add src/Connect4HoopsArcade.Web/wwwroot/index.html src/Connect4HoopsArcade.Web/wwwroot/css
git commit -m "feat(web): design-system CSS (tokens, fonts, keyframes) and index.html"
```

### Task 2.2: arcade.js interop stub

**Files:**
- Create: `src/Connect4HoopsArcade.Web/wwwroot/js/arcade.js`

- [ ] **Step 1: Create arcade.js with audio + storage + keyboard + fullscreen stubs**

Create `src/Connect4HoopsArcade.Web/wwwroot/js/arcade.js`:
```javascript
// Connect 4 Hoops Arcade — browser interop. Audio is fully implemented in Phase 6.
window.ArcadeAudio = (function () {
  const cache = {};
  let sfxVol = 0.8, voiceVol = 0.6, musicVol = 0.7, muted = false;
  let music = null;
  const lastPlayed = {};

  function url(key) { return 'audio/' + key; }

  function load(key) {
    if (!cache[key]) {
      const a = new Audio(url(key));
      a.addEventListener('error', () => console.warn('[ArcadeAudio] missing:', key));
      cache[key] = a;
    }
    return cache[key];
  }

  return {
    init() { /* called after first user gesture; preload in Phase 6 */ },
    preload(keys) { (keys || []).forEach(load); },
    playSfx(key, cooldownMs) {
      if (muted) return;
      const now = Date.now();
      if (cooldownMs && lastPlayed[key] && now - lastPlayed[key] < cooldownMs) return;
      lastPlayed[key] = now;
      try { const a = load(key).cloneNode(); a.volume = sfxVol; a.play().catch(() => {}); }
      catch (e) { console.warn('[ArcadeAudio] sfx failed', key, e); }
    },
    playVoice(key) {
      if (muted) return;
      try { const a = load(key).cloneNode(); a.volume = voiceVol; a.play().catch(() => {}); }
      catch (e) { console.warn('[ArcadeAudio] voice failed', key, e); }
    },
    playMusic(key, loop) {
      if (muted) return;
      try { this.stopMusic(); music = load(key).cloneNode(); music.loop = loop !== false; music.volume = musicVol; music.play().catch(() => {}); }
      catch (e) { console.warn('[ArcadeAudio] music failed', key, e); }
    },
    stopMusic() { if (music) { try { music.pause(); } catch {} music = null; } },
    setVolumes(s, v, m) { sfxVol = s; voiceVol = v; musicVol = m; if (music) music.volume = m; },
    mute() { muted = true; this.stopMusic(); },
    unmute() { muted = false; },
  };
})();

// localStorage helpers
window.ArcadeStore = {
  get(key) { try { return localStorage.getItem(key); } catch { return null; } },
  set(key, val) { try { localStorage.setItem(key, val); } catch {} },
};

// Keyboard 1-7 → .NET. Wired to a DotNetObjectReference in Phase 5.
window.ArcadeKeyboard = {
  _ref: null,
  register(dotNetRef) {
    this._ref = dotNetRef;
    window.addEventListener('keydown', (e) => {
      if (e.key >= '1' && e.key <= '7' && this._ref) {
        this._ref.invokeMethodAsync('OnColumnKey', parseInt(e.key, 10) - 1);
      }
    });
  },
};

window.ArcadeFullscreen = {
  toggle() {
    if (!document.fullscreenElement) document.documentElement.requestFullscreen?.().catch(() => {});
    else document.exitFullscreen?.().catch(() => {});
  },
};
```

- [ ] **Step 2: Build (no C# change; just confirm asset serves)**

```bash
dotnet build src/Connect4HoopsArcade.Web
```
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/Connect4HoopsArcade.Web/wwwroot/js/arcade.js
git commit -m "feat(web): arcade.js interop (audio/storage/keyboard/fullscreen)"
```

### Task 2.3: Web models (PlayMode, GameSettings) + AppScreen

**Files:**
- Create: `src/Connect4HoopsArcade.Web/Models/PlayMode.cs`, `GameSettings.cs`
- Create: `src/Connect4HoopsArcade.Web/State/AppScreen.cs`

- [ ] **Step 1: Create PlayMode**

Create `src/Connect4HoopsArcade.Web/Models/PlayMode.cs`:
```csharp
namespace Connect4HoopsArcade.Web.Models;

/// <summary>How moves are produced. Digital = on-screen input; Physical = sensor board is authoritative.</summary>
public enum PlayMode { Digital, Physical }
```

- [ ] **Step 2: Create GameSettings**

Create `src/Connect4HoopsArcade.Web/Models/GameSettings.cs`:
```csharp
using Connect4HoopsArcade.Core.Primitives;

namespace Connect4HoopsArcade.Web.Models;

public sealed class GameSettings
{
    public int MusicVolume { get; set; } = 70;
    public int SfxVolume { get; set; } = 80;
    public int NarratorVolume { get; set; } = 60;
    public bool VoicesEnabled { get; set; } = true;
    public AnimationSpeed Speed { get; set; } = AnimationSpeed.Normal;
    public PlayMode Mode { get; set; } = PlayMode.Digital;
}
```

- [ ] **Step 3: Create AppScreen enum**

Create `src/Connect4HoopsArcade.Web/State/AppScreen.cs`:
```csharp
namespace Connect4HoopsArcade.Web.State;

public enum AppScreen { Splash, Mode, Setup, Game, Victory, Draw, Sensors, Settings }
```

- [ ] **Step 4: Build**

```bash
dotnet build src/Connect4HoopsArcade.Web
```
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Models src/Connect4HoopsArcade.Web/State/AppScreen.cs
git commit -m "feat(web): add PlayMode, GameSettings, AppScreen"
```

### Task 2.4: AvatarSvg component (the SVG token builder)

**Files:**
- Create: `src/Connect4HoopsArcade.Web/Components/Shared/AvatarSvg.razor`

- [ ] **Step 1: Implement AvatarSvg.razor**

Reproduce the design's `avatar()` SVG path-by-path (spec §3 / source lines 678-745). Create
`src/Connect4HoopsArcade.Web/Components/Shared/AvatarSvg.razor`:
```razor
@using Connect4HoopsArcade.Core.Primitives
@using Connect4HoopsArcade.Core.Catalog

<div style="width:100%;height:100%;@(Glow ? $"filter:drop-shadow(0 0 12px {Hex});" : "")">
  <svg viewBox="0 0 110 120" style="width:100%;height:100%;display:block;overflow:visible;">
    <circle cx="55" cy="62" r="50" fill="@Hex" stroke="rgba(0,0,0,.28)" stroke-width="4" />
    <ellipse cx="40" cy="44" rx="22" ry="13" fill="rgba(255,255,255,.32)" />
    <circle cx="55" cy="62" r="46" fill="none" stroke="rgba(255,255,255,.45)" stroke-width="2.5" />
    @FaceMarkup
    @AccessoryMarkup
  </svg>
</div>

@code {
    [Parameter, EditorRequired] public string ColorId { get; set; } = "red";
    [Parameter] public FaceId Face { get; set; } = FaceId.Happy;
    [Parameter] public AccessoryId Accessory { get; set; } = AccessoryId.None;
    [Parameter] public bool Glow { get; set; }
    [Parameter] public bool DrawFace { get; set; } = true; // tokenStyle 'character'

    private const string Ink = "#1a1030";
    private string Hex => ColorCatalog.HexOf(ColorId);

    private MarkupString FaceMarkup => new(DrawFace ? Face switch
    {
        FaceId.Happy =>
            $"<circle cx='40' cy='56' r='5' fill='{Ink}'/><circle cx='70' cy='56' r='5' fill='{Ink}'/>" +
            Stroke("<path d='M40 74 Q55 90 70 74'/>"),
        FaceId.Confident =>
            Stroke("<path d='M34 58 Q40 52 46 58'/><path d='M64 58 Q70 52 76 58'/><path d='M42 78 Q58 86 72 70'/>"),
        FaceId.Serious =>
            $"<circle cx='40' cy='56' r='4.5' fill='{Ink}'/><circle cx='70' cy='56' r='4.5' fill='{Ink}'/>" +
            Stroke("<path d='M42 80 H68'/>"),
        FaceId.Surprised =>
            $"<circle cx='40' cy='55' r='7' fill='{Ink}'/><circle cx='70' cy='55' r='7' fill='{Ink}'/>" +
            $"<ellipse cx='55' cy='80' rx='8' ry='10' fill='{Ink}'/>",
        FaceId.Angry =>
            Stroke("<path d='M30 46 L48 53'/><path d='M80 46 L62 53'/>") +
            $"<circle cx='42' cy='60' r='4' fill='{Ink}'/><circle cx='68' cy='60' r='4' fill='{Ink}'/>" +
            Stroke("<path d='M42 82 Q55 74 68 82'/>"),
        _ => ""
    } : "");

    private MarkupString AccessoryMarkup => new(Accessory switch
    {
        AccessoryId.Glasses =>
            $"<circle cx='40' cy='56' r='13' fill='rgba(255,255,255,.18)' stroke='{Ink}' stroke-width='4'/>" +
            $"<circle cx='70' cy='56' r='13' fill='rgba(255,255,255,.18)' stroke='{Ink}' stroke-width='4'/>" +
            $"<path d='M53 56 H57' stroke='{Ink}' stroke-width='4'/>",
        AccessoryId.Cap =>
            "<path d='M14 40 Q55 -6 96 40 Z' fill='#1f4fff'/>" +
            "<ellipse cx='38' cy='41' rx='32' ry='8' fill='#143bd6'/>" +
            "<circle cx='55' cy='14' r='4' fill='#0d2aa0'/>",
        AccessoryId.Headband =>
            "<path d='M10 44 Q55 30 100 44 L100 56 Q55 42 10 56 Z' fill='#ff2d6f'/>" +
            "<path d='M10 50 Q55 38 100 50' stroke='rgba(255,255,255,.5)' stroke-width='2' fill='none'/>",
        AccessoryId.Crown =>
            "<path d='M28 36 L36 10 L48 28 L55 6 L62 28 L74 10 L82 36 Z' fill='#ffd23f' stroke='#d39a00' stroke-width='2' stroke-linejoin='round'/>" +
            "<circle cx='55' cy='22' r='3.5' fill='#ff2d6f'/>",
        AccessoryId.Bowtie =>
            "<path d='M55 104 L38 96 L38 114 Z' fill='#ff2d6f'/>" +
            "<path d='M55 104 L72 96 L72 114 Z' fill='#ff2d6f'/>" +
            "<circle cx='55' cy='105' r='4' fill='#c01250'/>",
        AccessoryId.Earrings =>
            "<circle cx='8' cy='74' r='5' fill='#ffd23f' stroke='#d39a00' stroke-width='1.5'/>" +
            "<circle cx='102' cy='74' r='5' fill='#ffd23f' stroke='#d39a00' stroke-width='1.5'/>",
        _ => ""
    });

    // Wraps path markup with the shared stroke style used for facial features.
    private static string Stroke(string paths)
    {
        // Inject stroke attributes into each <path .../> by replacing the self-closing tag.
        return paths.Replace("/>", $" stroke='{Ink}' stroke-width='5' fill='none' stroke-linecap='round'/>");
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build src/Connect4HoopsArcade.Web
```
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Components/Shared/AvatarSvg.razor
git commit -m "feat(web): AvatarSvg token component (faces + accessories)"
```

### Task 2.5: GameSession skeleton + DI + AppShell routing

**Files:**
- Create: `src/Connect4HoopsArcade.Web/State/GameSession.cs`
- Create: `src/Connect4HoopsArcade.Web/Components/Shared/BasketballSvg.razor`, `GlowBackdrop.razor`
- Create: `src/Connect4HoopsArcade.Web/Components/Layout/AppShell.razor`
- Modify: `src/Connect4HoopsArcade.Web/Program.cs`
- Modify: `src/Connect4HoopsArcade.Web/Components/App.razor` (or `App.razor` at root) and remove default sample pages

- [ ] **Step 1: Remove the Blazor template sample pages**

```bash
rm -rf src/Connect4HoopsArcade.Web/Pages src/Connect4HoopsArcade.Web/Layout
rm -f src/Connect4HoopsArcade.Web/Components/Pages/*.razor 2>/dev/null || true
```
(Template layout varies; ensure `Counter`, `Weather`, `Home`, `MainLayout`, `NavMenu` are gone. Keep `App.razor`, `_Imports.razor`, `Program.cs`.)

- [ ] **Step 2: Create GameSession skeleton**

Create `src/Connect4HoopsArcade.Web/State/GameSession.cs` (move flow added in Phase 4; navigation + state container now):
```csharp
using Connect4HoopsArcade.Core.Board;
using Connect4HoopsArcade.Core.Primitives;
using Connect4HoopsArcade.Core.Players;

namespace Connect4HoopsArcade.Web.State;

/// <summary>Single source of truth for runtime game state. Components subscribe to <see cref="StateChanged"/>.</summary>
public sealed class GameSession
{
    public event Action? StateChanged;
    private void Notify() => StateChanged?.Invoke();

    public AppScreen Screen { get; private set; } = AppScreen.Splash;
    private AppScreen _prevScreen = AppScreen.Mode;

    public GameMode Mode { get; private set; } = GameMode.TwoPlayer;
    public PlayerConfig[] Players { get; private set; } =
        { PlayerConfig.DefaultP1, PlayerConfig.DefaultP2 };
    public GameBoard Board { get; private set; } = new();
    public int Current { get; private set; }
    public int[] Scores { get; private set; } = { 0, 0 };
    public string Narrator { get; set; } = "";

    // ---- navigation ----
    public void GoSplash() { Screen = AppScreen.Splash; Notify(); }
    public void GoMode() { Screen = AppScreen.Mode; Notify(); }
    public void ChooseOnePlayer()
    {
        Mode = GameMode.OnePlayer;
        Players = new[] { Players[0] with { IsCpu = false }, PlayerConfig.DefaultCpu };
        Screen = AppScreen.Setup; Notify();
    }
    public void ChooseTwoPlayer()
    {
        Mode = GameMode.TwoPlayer;
        Players = new[] { PlayerConfig.DefaultP1, PlayerConfig.DefaultP2 };
        Screen = AppScreen.Setup; Notify();
    }
    public void OpenSettings() { _prevScreen = Screen; Screen = AppScreen.Settings; Notify(); }
    public void CloseSettings() { Screen = _prevScreen; Notify(); }
    public void OpenSensors() { _prevScreen = Screen; Screen = AppScreen.Sensors; Notify(); }
    public void CloseSensors() { Screen = AppScreen.Mode; Notify(); }

    // ---- setup mutation ----
    public void SetPlayer(int index, PlayerConfig config)
    {
        Players[index] = config;
        Notify();
    }

    // Move flow (TryDrop, CPU, win/draw) is implemented in Phase 4.
}
```

- [ ] **Step 3: Create BasketballSvg + GlowBackdrop**

Create `src/Connect4HoopsArcade.Web/Components/Shared/BasketballSvg.razor`:
```razor
<svg viewBox="0 0 100 100" style="width:100%;height:100%;">
  <circle cx="50" cy="50" r="46" fill="#ff8a00" stroke="#1a1030" stroke-width="4" />
  <path d="M50 4 V96" stroke="#1a1030" stroke-width="4" fill="none" />
  <path d="M4 50 H96" stroke="#1a1030" stroke-width="4" fill="none" />
  <path d="M14 18 Q50 50 14 82" stroke="#1a1030" stroke-width="4" fill="none" />
  <path d="M86 18 Q50 50 86 82" stroke="#1a1030" stroke-width="4" fill="none" />
  <ellipse cx="38" cy="34" rx="14" ry="9" fill="rgba(255,255,255,.35)" />
</svg>
```

Create `src/Connect4HoopsArcade.Web/Components/Shared/GlowBackdrop.razor` (the decorative backdrop, source lines 50-59):
```razor
<div style="position:absolute;inset:0;pointer-events:none;overflow:hidden;">
  <div style="position:absolute;top:-18%;left:-10%;width:55%;height:55%;border-radius:50%;background:radial-gradient(circle,rgba(255,45,111,.32),transparent 65%);filter:blur(8px);animation:glowPulse 6s ease-in-out infinite;"></div>
  <div style="position:absolute;bottom:-22%;right:-8%;width:58%;height:58%;border-radius:50%;background:radial-gradient(circle,rgba(34,211,238,.28),transparent 65%);filter:blur(8px);animation:glowPulse 7.5s ease-in-out infinite;"></div>
  <div style="position:absolute;top:40%;left:46%;width:30%;height:40%;border-radius:50%;background:radial-gradient(circle,rgba(255,210,63,.18),transparent 70%);filter:blur(10px);animation:glowPulse 9s ease-in-out infinite;"></div>
  <div style="position:absolute;left:50%;bottom:-46%;width:130%;height:90%;transform:translateX(-50%);border-radius:50%;border:3px solid rgba(255,255,255,.06);border-bottom:none;"></div>
  <div style="position:absolute;left:50%;bottom:-30%;width:62%;height:62%;transform:translateX(-50%);border-radius:50%;border:3px solid rgba(255,255,255,.05);border-bottom:none;"></div>
  <div style="position:absolute;left:50%;top:0;bottom:0;width:3px;transform:translateX(-50%);background:rgba(255,255,255,.04);"></div>
</div>
```

- [ ] **Step 4: Create AppShell router**

Create `src/Connect4HoopsArcade.Web/Components/Layout/AppShell.razor`:
```razor
@using Connect4HoopsArcade.Web.State
@using Connect4HoopsArcade.Web.Components.Screens
@implements IDisposable
@inject GameSession Session

<div class="arc-root">
  <GlowBackdrop />
  @switch (Session.Screen)
  {
    case AppScreen.Splash:   <AttractMode />; break;
    case AppScreen.Mode:     <GameModeSelector />; break;
    case AppScreen.Setup:    <PlayerSetup />; break;
    case AppScreen.Game:     <GameView />; break;
    case AppScreen.Sensors:  <SensorTestPanel />; break;
    case AppScreen.Settings: <SettingsPanel />; break;
    default:                 <AttractMode />; break;
  }
  @* Victory/Draw render as overlays on top of GameView in Phase 4. *@
</div>

@code {
    protected override void OnInitialized() => Session.StateChanged += OnChanged;
    public void Dispose() => Session.StateChanged -= OnChanged;
    private void OnChanged() => InvokeAsync(StateHasChanged);
}
```

- [ ] **Step 5: Replace App.razor body to render AppShell**

Overwrite `src/Connect4HoopsArcade.Web/App.razor` (or `Components/App.razor` per template):
```razor
@using Connect4HoopsArcade.Web.Components.Layout
<AppShell />
```

- [ ] **Step 6: Register DI in Program.cs**

Overwrite `src/Connect4HoopsArcade.Web/Program.cs`:
```csharp
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Connect4HoopsArcade.Web;
using Connect4HoopsArcade.Web.State;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddSingleton<GameSession>();
// Interop services registered in later phases.

await builder.Build().RunAsync();
```

- [ ] **Step 7: Update _Imports.razor**

Ensure `src/Connect4HoopsArcade.Web/_Imports.razor` contains:
```razor
@using System.Net.Http
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.JSInterop
@using Connect4HoopsArcade.Web
@using Connect4HoopsArcade.Web.Components.Shared
@using Connect4HoopsArcade.Web.Components.Layout
@using Connect4HoopsArcade.Web.State
@using Connect4HoopsArcade.Web.Models
```

- [ ] **Step 8: Create placeholder screen components so the switch compiles**

Create empty-but-valid stubs (filled in later phases). For each of
`Components/Screens/GameModeSelector.razor`, `PlayerSetup.razor`, `GameView.razor`,
`SensorTestPanel.razor`, `SettingsPanel.razor`, create:
```razor
<div style="position:absolute;inset:0;display:flex;align-items:center;justify-content:center;">
  <div class="font-display" style="font-size:32px;color:#22d3ee;">@Title</div>
</div>
@code { private const string Title = "TODO"; }
```
Give each a distinct `Title` constant — `GameModeSelector.razor` → `"MODO"`, `PlayerSetup.razor` → `"SETUP"`,
`GameView.razor` → `"JUEGO"`, `SensorTestPanel.razor` → `"SENSORES"`, `SettingsPanel.razor` → `"AJUSTES"`
(e.g. `@code { private const string Title = "MODO"; }`). Each is overwritten with its real implementation in
a later phase. `AttractMode.razor` is created fully in Task 2.6.

- [ ] **Step 9: Build**

```bash
dotnet build src/Connect4HoopsArcade.Web
```
Expected: Build succeeded (AttractMode referenced — create a temporary stub too if needed before Task 2.6, then overwrite).

- [ ] **Step 10: Commit**

```bash
git add src/Connect4HoopsArcade.Web
git commit -m "feat(web): GameSession state container, AppShell routing, DI"
```

### Task 2.6: Splash / Attract screen

**Files:**
- Create: `src/Connect4HoopsArcade.Web/Components/Screens/AttractMode.razor`

- [ ] **Step 1: Implement AttractMode**

Reproduce source lines 61-79. Create `src/Connect4HoopsArcade.Web/Components/Screens/AttractMode.razor`:
```razor
@using Connect4HoopsArcade.Core.Primitives
@inject GameSession Session

<div @onclick="Start" style="position:absolute;inset:0;display:flex;flex-direction:column;align-items:center;justify-content:center;cursor:pointer;padding:24px;z-index:5;">
  <div style="position:absolute;top:7%;left:14%;width:62px;height:62px;opacity:.9;animation:floatY 5s ease-in-out infinite;">
    <AvatarSvg ColorId="pink" Face="FaceId.Happy" Accessory="AccessoryId.None" Glow="true" />
  </div>
  <div style="position:absolute;top:16%;right:13%;width:54px;height:54px;opacity:.9;animation:floatY 6.4s ease-in-out infinite .6s;">
    <AvatarSvg ColorId="cyan" Face="FaceId.Confident" Accessory="AccessoryId.None" Glow="true" />
  </div>
  <div style="position:absolute;bottom:16%;left:9%;width:48px;height:48px;opacity:.85;animation:floatY 7s ease-in-out infinite 1.1s;">
    <AvatarSvg ColorId="yellow" Face="FaceId.Surprised" Accessory="AccessoryId.None" Glow="true" />
  </div>

  <div style="display:flex;align-items:center;gap:18px;margin-bottom:6px;">
    <div style="width:84px;height:84px;animation:ballBounce 1.9s cubic-bezier(.5,0,.5,1) infinite;">
      <BasketballSvg />
    </div>
  </div>
  <div class="font-display" style="font-weight:700;letter-spacing:1px;text-align:center;line-height:.94;">
    <div style="font-size:clamp(40px,9vw,118px);color:#ffd23f;text-shadow:0 6px 0 #c98800,0 0 34px rgba(255,210,63,.55);-webkit-text-stroke:3px #1a1030;">CONNECT&nbsp;4</div>
    <div style="font-size:clamp(34px,7.6vw,96px);color:#ff2d6f;text-shadow:0 5px 0 #a3134a,0 0 32px rgba(255,45,111,.55);-webkit-text-stroke:3px #1a1030;margin-top:-4px;">HOOPS</div>
  </div>
  <div style="margin-top:30px;display:flex;align-items:center;gap:14px;padding:16px 34px;border-radius:999px;background:rgba(255,255,255,.06);border:2px solid rgba(34,211,238,.55);box-shadow:0 0 26px rgba(34,211,238,.35);animation:blink 1.6s ease-in-out infinite;">
    <span class="font-display" style="font-weight:600;font-size:clamp(18px,2.6vw,28px);color:#22d3ee;letter-spacing:.5px;">TOCA PARA COMENZAR</span>
  </div>
  <div style="position:absolute;bottom:22px;font-size:13px;color:rgba(255,255,255,.4);letter-spacing:2px;font-weight:700;">ARCADE&nbsp;EDITION · 7×6 · 2 JUGADORES</div>
</div>

@code {
    private void Start() => Session.GoMode();
}
```

- [ ] **Step 2: Run the app and visually verify**

```bash
dotnet run --project src/Connect4HoopsArcade.Web
```
Open the served URL. Expected: dark arcade backdrop with floating tokens, bouncing basketball, glowing "CONNECT 4 HOOPS" title, blinking "TOCA PARA COMENZAR". Clicking advances to the "MODO" placeholder.

- [ ] **Step 3: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Components/Screens/AttractMode.razor
git commit -m "feat(web): splash / attract screen"
```

---

## Phase 3 — Mode Select & Player Setup

### Task 3.1: GameModeSelector screen

**Files:**
- Create (overwrite stub): `src/Connect4HoopsArcade.Web/Components/Screens/GameModeSelector.razor`

- [ ] **Step 1: Implement GameModeSelector** (source lines 82-104)

Overwrite `src/Connect4HoopsArcade.Web/Components/Screens/GameModeSelector.razor`:
```razor
@using Connect4HoopsArcade.Core.Primitives
@inject GameSession Session

<div style="position:absolute;inset:0;display:flex;flex-direction:column;align-items:center;justify-content:center;padding:24px;z-index:5;animation:slideUp .4s ease;">
  <div class="font-display" style="font-weight:700;font-size:clamp(26px,4.6vw,52px);color:#fff;text-shadow:0 4px 0 rgba(0,0,0,.4);margin-bottom:34px;text-align:center;">ELIGE MODO DE JUEGO</div>
  <div style="display:flex;gap:28px;flex-wrap:wrap;justify-content:center;max-width:880px;">
    <button @onclick="Session.ChooseOnePlayer" class="mode-card mode-card--cyan">
      <div style="width:96px;height:96px;margin:0 auto 16px;">
        <AvatarSvg ColorId="cyan" Face="FaceId.Confident" Accessory="AccessoryId.Glasses" />
      </div>
      <div class="font-display" style="font-weight:700;font-size:34px;">1 JUGADOR</div>
      <div style="font-size:16px;font-weight:700;color:rgba(255,255,255,.6);margin-top:4px;">vs. CPU</div>
    </button>
    <button @onclick="Session.ChooseTwoPlayer" class="mode-card mode-card--pink">
      <div style="position:relative;width:96px;height:96px;margin:0 auto 16px;">
        <div style="position:absolute;left:0;top:8%;width:62%;height:62%;"><AvatarSvg ColorId="pink" Face="FaceId.Happy" Accessory="AccessoryId.Cap" /></div>
        <div style="position:absolute;right:0;bottom:4%;width:62%;height:62%;"><AvatarSvg ColorId="yellow" Face="FaceId.Confident" Accessory="AccessoryId.Crown" /></div>
      </div>
      <div class="font-display" style="font-weight:700;font-size:34px;">2 JUGADORES</div>
      <div style="font-size:16px;font-weight:700;color:rgba(255,255,255,.6);margin-top:4px;">Local · 1 vs 1</div>
    </button>
  </div>
  <div style="display:flex;gap:16px;margin-top:40px;">
    <button @onclick="Session.OpenSensors" class="pill-btn">⚡ Prueba de sensores</button>
    <button @onclick="Session.OpenSettings" class="pill-btn">⚙ Configuración</button>
  </div>
  <button @onclick="Session.GoSplash" class="pill-btn" style="position:absolute;top:22px;left:22px;">‹ Inicio</button>
</div>
```

- [ ] **Step 2: Add mode-card + pill-btn styles to board.css**

Append to `src/Connect4HoopsArcade.Web/wwwroot/css/board.css`:
```css
.mode-card { cursor:pointer; width:330px; max-width:42vw; min-width:240px; padding:34px 24px; border-radius:28px; background:linear-gradient(180deg,#15294a,#0d1830); color:#fff; box-shadow:inset 0 2px 0 rgba(255,255,255,.08); transition:transform .15s, box-shadow .15s; }
.mode-card--cyan { border:3px solid #22d3ee; box-shadow:0 0 30px rgba(34,211,238,.3), inset 0 2px 0 rgba(255,255,255,.08); }
.mode-card--pink { border:3px solid #ff2d6f; background:linear-gradient(180deg,#3a1330,#1f0a1c); box-shadow:0 0 30px rgba(255,45,111,.3), inset 0 2px 0 rgba(255,255,255,.08); }
.mode-card--cyan:hover { transform:translateY(-6px); box-shadow:0 0 46px rgba(34,211,238,.6); }
.mode-card--pink:hover { transform:translateY(-6px); box-shadow:0 0 46px rgba(255,45,111,.6); }
.pill-btn { cursor:pointer; display:flex; align-items:center; gap:8px; padding:11px 20px; border-radius:999px; border:1.5px solid rgba(255,255,255,.2); background:rgba(255,255,255,.04); color:rgba(255,255,255,.85); font-weight:800; font-size:14px; }
.pill-btn:hover { background:rgba(255,255,255,.1); }
```

- [ ] **Step 3: Build + visually verify**

```bash
dotnet build src/Connect4HoopsArcade.Web
```
Run the app; click through splash → mode. Expected: two glowing cards (cyan 1P, pink 2P) that lift on hover; bottom pills; back to splash works.

- [ ] **Step 4: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Components/Screens/GameModeSelector.razor src/Connect4HoopsArcade.Web/wwwroot/css/board.css
git commit -m "feat(web): game mode selector screen"
```

### Task 3.2: Picker components (Color/Face/Accessory)

**Files:**
- Create: `src/Connect4HoopsArcade.Web/Components/Setup/ColorPicker.razor`, `FacePicker.razor`, `AccessoryPicker.razor`

- [ ] **Step 1: Implement ColorPicker** (source lines 125-134)

Create `src/Connect4HoopsArcade.Web/Components/Setup/ColorPicker.razor`:
```razor
@using Connect4HoopsArcade.Core.Catalog

<div>
  <div class="setup-label">COLOR</div>
  <div style="display:flex;flex-wrap:wrap;gap:9px;">
    @foreach (var c in ColorCatalog.All)
    {
        var taken = TakenColorId is not null && c.Id == TakenColorId;
        var selected = c.Id == SelectedId;
        var ring = selected ? $"0 0 0 4px #fff, 0 0 16px {c.Hex}"
                            : (taken ? "none" : "0 0 0 2px rgba(255,255,255,.25)");
        <button disabled="@taken" @onclick="() => Pick(c.Id)"
                style="cursor:@(taken ? "not-allowed" : "pointer");position:relative;width:42px;height:42px;border-radius:50%;border:none;background:@c.Hex;box-shadow:@ring;opacity:@(taken ? "0.4" : "1");transition:transform .12s;">
          @if (taken)
          {
              <span style="position:absolute;inset:0;display:flex;align-items:center;justify-content:center;font-size:18px;">🔒</span>
          }
        </button>
    }
  </div>
</div>

@code {
    [Parameter] public string SelectedId { get; set; } = "red";
    [Parameter] public string? TakenColorId { get; set; }
    [Parameter] public EventCallback<string> OnPick { get; set; }
    private Task Pick(string id) => OnPick.InvokeAsync(id);
}
```

- [ ] **Step 2: Implement FacePicker** (source lines 135-142)

Create `src/Connect4HoopsArcade.Web/Components/Setup/FacePicker.razor`:
```razor
@using Connect4HoopsArcade.Core.Primitives
@using Connect4HoopsArcade.Core.Catalog

<div>
  <div class="setup-label">CARA</div>
  <div style="display:flex;flex-wrap:wrap;gap:8px;">
    @foreach (var f in FaceCatalog.All)
    {
        var border = f.Id == SelectedId ? Hex : "transparent";
        <button title="@f.Label" @onclick="() => OnPick.InvokeAsync(f.Id)"
                style="cursor:pointer;width:52px;height:52px;padding:4px;border-radius:14px;border:2px solid @border;background:rgba(0,0,0,.2);">
          <AvatarSvg ColorId="@ColorId" Face="f.Id" Accessory="AccessoryId.None" />
        </button>
    }
  </div>
</div>

@code {
    [Parameter, EditorRequired] public string ColorId { get; set; } = "red";
    [Parameter] public FaceId SelectedId { get; set; }
    [Parameter] public EventCallback<FaceId> OnPick { get; set; }
    private string Hex => ColorCatalog.HexOf(ColorId);
}
```

- [ ] **Step 3: Implement AccessoryPicker** (source lines 143-150)

Create `src/Connect4HoopsArcade.Web/Components/Setup/AccessoryPicker.razor`:
```razor
@using Connect4HoopsArcade.Core.Primitives
@using Connect4HoopsArcade.Core.Catalog

<div>
  <div class="setup-label">ACCESORIO</div>
  <div style="display:flex;flex-wrap:wrap;gap:8px;">
    @foreach (var a in AccessoryCatalog.All)
    {
        var border = a.Id == SelectedId ? Hex : "transparent";
        <button title="@a.Label" @onclick="() => OnPick.InvokeAsync(a.Id)"
                style="cursor:pointer;width:52px;height:52px;padding:4px;border-radius:14px;border:2px solid @border;background:rgba(0,0,0,.2);">
          <AvatarSvg ColorId="@ColorId" Face="Face" Accessory="a.Id" />
        </button>
    }
  </div>
</div>

@code {
    [Parameter, EditorRequired] public string ColorId { get; set; } = "red";
    [Parameter] public FaceId Face { get; set; }
    [Parameter] public AccessoryId SelectedId { get; set; }
    [Parameter] public EventCallback<AccessoryId> OnPick { get; set; }
    private string Hex => ColorCatalog.HexOf(ColorId);
}
```

- [ ] **Step 4: Add setup-label style to board.css**

Append to `src/Connect4HoopsArcade.Web/wwwroot/css/board.css`:
```css
.setup-label { font-size:12px; font-weight:900; color:rgba(255,255,255,.5); letter-spacing:1.5px; margin-bottom:8px; }
```

- [ ] **Step 5: Build**

```bash
dotnet build src/Connect4HoopsArcade.Web
```
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Components/Setup src/Connect4HoopsArcade.Web/wwwroot/css/board.css
git commit -m "feat(web): color/face/accessory picker components"
```

### Task 3.3: PlayerSetupCard + PlayerSetup screen

**Files:**
- Create: `src/Connect4HoopsArcade.Web/Components/Setup/PlayerSetupCard.razor`
- Create (overwrite stub): `src/Connect4HoopsArcade.Web/Components/Screens/PlayerSetup.razor`

- [ ] **Step 1: Implement PlayerSetupCard** (source lines 116-151)

Create `src/Connect4HoopsArcade.Web/Components/Setup/PlayerSetupCard.razor`:
```razor
@using Connect4HoopsArcade.Core.Primitives
@using Connect4HoopsArcade.Core.Catalog
@using Connect4HoopsArcade.Core.Players

<div style="flex:1 1 340px;max-width:440px;min-width:300px;border-radius:24px;border:3px solid @Hex;background:linear-gradient(180deg,rgba(255,255,255,.05),rgba(255,255,255,.01));box-shadow:0 0 26px @(Hex)55;padding:18px;display:flex;flex-direction:column;gap:14px;">
  <div style="display:flex;align-items:center;gap:14px;">
    <div style="width:92px;height:92px;flex:none;">
      <AvatarSvg ColorId="@Player.ColorId" Face="Player.Face" Accessory="Player.Accessory" Glow="true" />
    </div>
    <div style="flex:1;min-width:0;">
      <div style="font-size:13px;font-weight:900;color:@Hex;letter-spacing:1px;">@Tag</div>
      <input value="@Player.Name" @onchange="OnNameChange" disabled="@Player.IsCpu" maxlength="12" placeholder="Nombre"
             class="font-display"
             style="width:100%;margin-top:4px;background:rgba(0,0,0,.25);border:2px solid rgba(255,255,255,.12);border-radius:12px;padding:10px 12px;color:#fff;font-weight:600;font-size:22px;outline:none;opacity:@(Player.IsCpu ? "0.6" : "1");" />
    </div>
  </div>
  <ColorPicker SelectedId="@Player.ColorId" TakenColorId="@TakenColorId" OnPick="PickColor" />
  <FacePicker ColorId="@Player.ColorId" SelectedId="Player.Face" OnPick="PickFace" />
  <AccessoryPicker ColorId="@Player.ColorId" Face="Player.Face" SelectedId="Player.Accessory" OnPick="PickAccessory" />
</div>

@code {
    [Parameter, EditorRequired] public PlayerConfig Player { get; set; } = default!;
    [Parameter] public int Index { get; set; }
    [Parameter] public string? TakenColorId { get; set; }
    [Parameter] public EventCallback<PlayerConfig> OnChange { get; set; }

    private string Hex => ColorCatalog.HexOf(Player.ColorId);
    private string Tag => Player.IsCpu ? "CPU" : $"JUGADOR {Index + 1}";

    private Task PickColor(string id) => OnChange.InvokeAsync(Player with { ColorId = id });
    private Task PickFace(FaceId f) => OnChange.InvokeAsync(Player with { Face = f });
    private Task PickAccessory(AccessoryId a) => OnChange.InvokeAsync(Player with { Accessory = a });
    private Task OnNameChange(ChangeEventArgs e)
    {
        var name = e.Value?.ToString();
        if (string.IsNullOrWhiteSpace(name)) name = $"Jugador {Index + 1}";
        return OnChange.InvokeAsync(Player with { Name = name });
    }
}
```

- [ ] **Step 2: Implement PlayerSetup screen** (source lines 106-198)

Overwrite `src/Connect4HoopsArcade.Web/Components/Screens/PlayerSetup.razor`:
```razor
@using Connect4HoopsArcade.Core.Primitives
@using Connect4HoopsArcade.Core.Players
@using Connect4HoopsArcade.Core.Rules
@inject GameSession Session

<div style="position:absolute;inset:0;display:flex;flex-direction:column;z-index:5;animation:slideUp .4s ease;">
  <div style="flex:none;display:flex;align-items:center;justify-content:space-between;padding:18px 24px;">
    <button @onclick="Session.GoMode" class="pill-btn">‹ Atrás</button>
    <div class="font-display" style="font-weight:700;font-size:clamp(20px,3vw,34px);">PERSONALIZA TU FICHA</div>
    <div style="width:84px;"></div>
  </div>

  <div style="flex:1;min-height:0;overflow:auto;display:flex;gap:20px;padding:6px 24px 18px;justify-content:center;align-items:stretch;flex-wrap:wrap;">
    <PlayerSetupCard Player="Session.Players[0]" Index="0"
                     TakenColorId="@(IsTwoPlayer ? Session.Players[1].ColorId : null)"
                     OnChange="@(p => Session.SetPlayer(0, p))" />
    <PlayerSetupCard Player="Session.Players[1]" Index="1"
                     TakenColorId="@(IsTwoPlayer ? Session.Players[0].ColorId : null)"
                     OnChange="@(p => Session.SetPlayer(1, p))" />
  </div>

  <div style="flex:none;display:flex;flex-direction:column;align-items:center;gap:10px;padding:6px 24px 22px;">
    @if (Warning != ColorWarning.None)
    {
        <div style="display:flex;align-items:center;gap:10px;padding:9px 18px;border-radius:999px;background:rgba(255,45,111,.16);border:1.5px solid #ff2d6f;color:#ffd0de;font-weight:800;font-size:14px;">⚠ @ColorWarningMessages.Message(Warning)</div>
    }
    <button @onclick="Begin" disabled="@StartDisabled" class="font-display"
            style="cursor:pointer;padding:16px 56px;border-radius:999px;border:none;background:@(StartDisabled ? "rgba(255,255,255,.12)" : "linear-gradient(180deg,#ffd23f,#f5a700)");box-shadow:@(StartDisabled ? "none" : "0 7px 0 #c98800, 0 0 30px rgba(255,210,63,.5)");color:#1a1030;font-weight:700;font-size:26px;letter-spacing:1px;opacity:@(StartDisabled ? "0.5" : "1");transition:transform .15s;">¡JUGAR! ▶</button>
  </div>
</div>

@code {
    private bool IsTwoPlayer => Session.Mode == GameMode.TwoPlayer;
    private ColorWarning Warning => IsTwoPlayer
        ? PlayValidator.CheckColors(Session.Players[0].ColorId, Session.Players[1].ColorId)
        : ColorWarning.None;
    private bool StartDisabled => Warning != ColorWarning.None;

    private void Begin()
    {
        if (StartDisabled) return;
        Session.BeginGame();
    }
}
```

- [ ] **Step 3: Add BeginGame to GameSession (temporary; full move flow in Phase 4 supersedes)**

Add to `src/Connect4HoopsArcade.Web/State/GameSession.cs` (inside the class):
```csharp
public void BeginGame()
{
    Board = new GameBoard();
    Current = 0;
    Scores = new[] { 0, 0 };
    Narrator = $"¡Comienza el duelo! Turno de {Players[0].Name}";
    Screen = AppScreen.Game;
    Notify();
}
```

- [ ] **Step 4: Build + visually verify color validation**

```bash
dotnet build src/Connect4HoopsArcade.Web
```
Run; go to 2-player setup. Set both players to red → warning pill appears, ¡JUGAR! disabled. Pick distinct colors → warning clears, button enabled. Taken color shows 🔒 on the other card.

- [ ] **Step 5: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Components/Setup/PlayerSetupCard.razor src/Connect4HoopsArcade.Web/Components/Screens/PlayerSetup.razor src/Connect4HoopsArcade.Web/State/GameSession.cs
git commit -m "feat(web): player setup screen with live color validation"
```

---

## Phase 4 — Game Board & Full Move Flow

> This phase makes the game playable end-to-end with mouse/touch. The move flow lives in `GameSession`
> and delegates rules to the (already-tested) `Core`. `GameSession` raises C# events (`ChipDropped`,
> `TurnChanged`, `ColumnFull`, `ThreatRaised`, `Won`, `Drew`) that audio subscribes to in Phase 6.

### Task 4.1: GameSession full move flow

**Files:**
- Modify: `src/Connect4HoopsArcade.Web/State/GameSession.cs`

- [ ] **Step 1: Add state fields, events, confetti model, and move flow**

Replace the entire body of `src/Connect4HoopsArcade.Web/State/GameSession.cs` with the full version
(this supersedes the Phase 2/3 skeleton, keeping all existing navigation methods):
```csharp
using Connect4HoopsArcade.Core.Ai;
using Connect4HoopsArcade.Core.Board;
using Connect4HoopsArcade.Core.Primitives;
using Connect4HoopsArcade.Core.Players;
using Connect4HoopsArcade.Core.Rules;

namespace Connect4HoopsArcade.Web.State;

/// <summary>Single source of truth for runtime game state. Components subscribe to <see cref="StateChanged"/>.</summary>
public sealed class GameSession
{
    public event Action? StateChanged;
    // Audio/narration hooks (subscribed by NarratorService/AudioService in Phase 6).
    public event Action? ChipDropped;
    public event Action<int>? TurnChanged;     // arg: new current player index
    public event Action? ColumnFull;
    public event Action? ThreatRaised;
    public event Action<int>? Won;             // arg: winner index
    public event Action? Drew;
    public event Action? GameStarted;

    private void Notify() => StateChanged?.Invoke();

    public AppScreen Screen { get; private set; } = AppScreen.Splash;
    private AppScreen _prevScreen = AppScreen.Mode;

    public GameMode Mode { get; private set; } = GameMode.TwoPlayer;
    public CpuDifficulty CpuLevel { get; set; } = CpuDifficulty.Sharp;
    public AnimationSpeed Speed { get; set; } = AnimationSpeed.Normal;

    public PlayerConfig[] Players { get; private set; } =
        { PlayerConfig.DefaultP1, PlayerConfig.DefaultP2 };
    public GameBoard Board { get; private set; } = new();
    public int Current { get; private set; }
    public int[] Scores { get; private set; } = { 0, 0 };
    public string Narrator { get; private set; } = "";

    public int? Winner { get; private set; }
    public string WinBy { get; private set; } = "";   // "connect" | "resign"
    public HashSet<BoardPosition> WinningCells { get; private set; } = new();
    public BoardPosition? LastDrop { get; private set; }
    public int ErrorCol { get; private set; } = -1;
    public bool IsThinking { get; private set; }
    public bool IsIdle { get; private set; }
    public bool IsBusy { get; private set; }          // a move animation/transition in progress
    public List<ConfettiPiece> Confetti { get; private set; } = new();

    public double DropSeconds => Speed == AnimationSpeed.Fast ? 0.34 : 0.6;
    public bool CpuTurn => Mode == GameMode.OnePlayer && Current == 1;

    private CancellationTokenSource? _idleCts;
    private static readonly Random Rng = new();

    // ---- navigation ----
    public void GoSplash() { CancelIdle(); Screen = AppScreen.Splash; Notify(); }
    public void GoMode() { CancelIdle(); Screen = AppScreen.Mode; Notify(); }
    public void ChooseOnePlayer()
    {
        Mode = GameMode.OnePlayer;
        Players = new[] { Players[0] with { IsCpu = false }, PlayerConfig.DefaultCpu };
        Screen = AppScreen.Setup; Notify();
    }
    public void ChooseTwoPlayer()
    {
        Mode = GameMode.TwoPlayer;
        Players = new[] { PlayerConfig.DefaultP1, PlayerConfig.DefaultP2 };
        Screen = AppScreen.Setup; Notify();
    }
    public void OpenSettings() { _prevScreen = Screen; Screen = AppScreen.Settings; Notify(); }
    public void CloseSettings() { Screen = _prevScreen; Notify(); }
    public void OpenSensors() { _prevScreen = Screen; Screen = AppScreen.Sensors; Notify(); }
    public void CloseSensors() { Screen = AppScreen.Mode; Notify(); }
    public void SetPlayer(int index, PlayerConfig config) { Players[index] = config; Notify(); }

    // ---- game lifecycle ----
    public void BeginGame()
    {
        ResetState($"¡Comienza el duelo! Turno de {Players[0].Name}", resetScores: true);
        Screen = AppScreen.Game;
        Notify();
        GameStarted?.Invoke();
        ArmIdle();
    }

    public void Rematch()
    {
        ResetState($"¡Revancha! Turno de {Players[0].Name}", resetScores: false);
        Screen = AppScreen.Game;
        Notify();
        ArmIdle();
    }

    public void ResetBoard()
    {
        ResetState($"Tablero reiniciado. Turno de {Players[0].Name}", resetScores: false);
        Notify();
        ArmIdle();
    }

    public void ChangePlayers() { CancelIdle(); Winner = null; Screen = AppScreen.Setup; Notify(); }

    private void ResetState(string narrator, bool resetScores)
    {
        CancelIdle();
        Board = new GameBoard();
        Current = 0;
        Winner = null; WinBy = "";
        WinningCells = new();
        LastDrop = null; ErrorCol = -1;
        IsThinking = false; IsBusy = false; IsIdle = false;
        Confetti = new();
        if (resetScores) Scores = new[] { 0, 0 };   // preserved across rematch/reset
        Narrator = narrator;
    }

    public void Resign()
    {
        if (Winner != null) return;
        CancelIdle();
        int loser = Current;
        int w = loser == 0 ? 1 : 0;
        Scores[w]++;
        Winner = w; WinBy = "resign"; WinningCells = new();
        IsIdle = false; IsThinking = false;
        Narrator = $"🏳️ {Players[loser].Name} se rindió. ¡Gana {Players[w].Name}!";
        Notify();
        Won?.Invoke(w);
        _ = TransitionToVictory();
    }

    // ---- move flow ----
    /// <summary>Entry point for a column trigger (click, keyboard, or sensor). Honors turn/busy guards.</summary>
    public async Task TryDrop(int col)
    {
        if (Winner != null || IsBusy) return;
        if (CpuTurn) return;                       // ignore human input during CPU turn
        CancelIdle();
        await Place(col);
    }

    private async Task Place(int col)
    {
        int r = Board.LowestRow(col);
        if (r < 0)
        {
            ErrorCol = col;
            Narrator = "¡Columna llena! Prueba otra. 🚫";
            Notify();
            ColumnFull?.Invoke();
            await Task.Delay(700);
            ErrorCol = -1;
            Notify();
            return;
        }

        IsBusy = true;
        var cell = CellExtensions.ForPlayer(Current);
        Board.Drop(col, cell);
        LastDrop = new BoardPosition(col, r);
        var line = WinDetector.FindWinningLine(Board, col, r, cell);
        string name = Players[Current].Name;
        ChipDropped?.Invoke();
        Notify();

        if (line != null)
        {
            WinningCells = line.ToHashSet();
            Winner = Current; WinBy = "connect";
            Scores[Current]++;
            IsIdle = false;
            Narrator = $"¡CONECTA 4! ¡Gana {name}! 🎉";
            Notify();
            Won?.Invoke(Current);
            await TransitionToVictory();
            return;
        }

        if (Board.IsBoardFull())
        {
            Narrator = "¡Tablero lleno! Es un empate. 🤝";
            Notify();
            Drew?.Invoke();
            await Task.Delay(850);
            Screen = AppScreen.Draw;
            IsBusy = false;
            Notify();
            return;
        }

        Current = Current == 0 ? 1 : 0;
        IsIdle = false;
        Narrator = TurnPhrase(Current);
        IsBusy = false;
        Notify();
        TurnChanged?.Invoke(Current);
        if (ThreatScanner.HasImmediateThreat(Board, CellExtensions.ForPlayer(Current == 0 ? 1 : 0)))
            ThreatRaised?.Invoke();

        if (CpuTurn)
        {
            IsThinking = true; IsBusy = true; Notify();
            await Task.Delay(750);
            int cpuCol = CpuStrategy.ChooseColumn(Board, CpuLevel);
            IsThinking = false; IsBusy = false;
            if (cpuCol >= 0) await Place(cpuCol);
        }
        else
        {
            ArmIdle();
        }
    }

    private async Task TransitionToVictory()
    {
        await Task.Delay(700);
        Confetti = MakeConfetti();
        Notify();
        await Task.Delay(2000);
        Screen = AppScreen.Victory;
        IsBusy = false;
        Notify();
    }

    private string TurnPhrase(int p)
    {
        string name = Players[p].Name;
        string opp = Players[p == 0 ? 1 : 0].Name;
        if (ThreatScanner.HasImmediateThreat(Board, CellExtensions.ForPlayer(p == 0 ? 1 : 0)))
            return $"¡Cuidado {name}, hay tres en línea! 😱";
        string[] phrases =
        {
            $"Turno de {name}",
            $"¡Buena jugada! Ahora va {name}",
            $"Vamos {name}, tú puedes 🏀",
            $"Cuidado {name}, {opp} va fuerte",
        };
        return phrases[Rng.Next(phrases.Length)];
    }

    // ---- idle nudge ----
    private void ArmIdle()
    {
        CancelIdle();
        if (Screen != AppScreen.Game || Winner != null || CpuTurn) return;
        _idleCts = new CancellationTokenSource();
        var token = _idleCts.Token;
        _ = Task.Run(async () =>
        {
            try { await Task.Delay(9000, token); } catch { return; }
            if (token.IsCancellationRequested) return;
            if (Screen != AppScreen.Game || Winner != null || CpuTurn) return;
            IsIdle = true;
            Narrator = $"¿Sigues ahí, {Players[Current].Name}? ¡Es tu turno! 🏀";
            Notify();
        });
    }

    private void CancelIdle()
    {
        _idleCts?.Cancel();
        _idleCts = null;
        if (IsIdle) IsIdle = false;
    }

    private static List<ConfettiPiece> MakeConfetti()
    {
        string[] cols = { "#ff2d6f", "#22d3ee", "#ffd23f", "#2ee86e", "#b14bff", "#ff8a00", "#ffffff" };
        var list = new List<ConfettiPiece>(70);
        for (int i = 0; i < 70; i++)
        {
            list.Add(new ConfettiPiece(
                Left: $"{Rng.Next(0, 1000) / 10.0:0.0}%",
                Size: $"{7 + Rng.Next(0, 12)}px",
                Color: cols[i % cols.Length],
                Radius: Rng.NextDouble() > 0.5 ? "50%" : "2px",
                Duration: $"{(2 + Rng.NextDouble() * 2.4):0.00}s",
                Delay: $"{(Rng.NextDouble() * 1.5):0.00}s"));
        }
        return list;
    }
}

public sealed record ConfettiPiece(string Left, string Size, string Color, string Radius, string Duration, string Delay);
```

- [ ] **Step 2: Build**

```bash
dotnet build src/Connect4HoopsArcade.Web
```
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/Connect4HoopsArcade.Web/State/GameSession.cs
git commit -m "feat(web): full game move flow in GameSession (win/draw/full/CPU/idle)"
```

### Task 4.2: Board cell, column, grid, and arrows

**Files:**
- Create: `src/Connect4HoopsArcade.Web/Components/Game/GameCell.razor`, `GameColumn.razor`, `BoardGrid.razor`, `ColumnArrows.razor`

- [ ] **Step 1: Implement GameCell** (source lines 239-244)

Create `src/Connect4HoopsArcade.Web/Components/Game/GameCell.razor`:
```razor
@using Connect4HoopsArcade.Core.Primitives
@using Connect4HoopsArcade.Core.Players
@using Connect4HoopsArcade.Core.Catalog

<div style="position:relative;width:clamp(46px,9.4vh,104px);height:clamp(46px,9.4vh,104px);border-radius:50%;background:var(--cell);box-shadow:inset 0 4px 10px rgba(0,0,0,.85), inset 0 -2px 4px rgba(255,255,255,.05);">
  @if (Occupant is not null)
  {
      var hex = ColorCatalog.HexOf(Occupant.ColorId);
      <div style="position:absolute;inset:6%;opacity:@Dim;transition:opacity .3s;@DropStyle">
        <div style="width:100%;height:100%;border-radius:50%;--wg:@hex;@(IsWinning ? "animation:winPulse .85s ease-in-out infinite;" : "")">
          <AvatarSvg ColorId="@Occupant.ColorId" Face="Occupant.Face" Accessory="Occupant.Accessory" />
        </div>
      </div>
  }
</div>

@code {
    [Parameter] public PlayerConfig? Occupant { get; set; }
    [Parameter] public bool IsWinning { get; set; }
    [Parameter] public bool DimmedByWin { get; set; }   // true when a win exists and this cell is not part of it
    [Parameter] public bool JustDropped { get; set; }
    [Parameter] public double DropSeconds { get; set; } = 0.6;

    private string Dim => DimmedByWin ? "0.22" : "1";
    private string DropStyle => (JustDropped && !DimmedByWin && !IsWinning)
        ? $"animation:drop {DropSeconds:0.##}s;"
        : "";
}
```

- [ ] **Step 2: Implement GameColumn** (source lines 237-246)

Create `src/Connect4HoopsArcade.Web/Components/Game/GameColumn.razor`:
```razor
@using Connect4HoopsArcade.Core.Board
@using Connect4HoopsArcade.Core.Players
@inject GameSession Session

<div @onclick="Drop"
     style="display:flex;flex-direction:column;gap:clamp(5px,1.1vw,14px);border-radius:14px;padding:2px;cursor:@Cursor;background:@ColBg;box-shadow:@ColGlow;@ShakeStyle">
  @for (int row = 5; row >= 0; row--)
  {
      var r = row;
      var occ = OccupantAt(r);
      <GameCell Occupant="occ"
                IsWinning="Session.WinningCells.Contains(new BoardPosition(Col, r))"
                DimmedByWin="@(Session.Winner != null && !Session.WinningCells.Contains(new BoardPosition(Col, r)))"
                JustDropped="@(Session.LastDrop is { } d && d.Col == Col && d.Row == r && Session.Winner == null)"
                DropSeconds="Session.DropSeconds" />
  }
</div>

@code {
    [Parameter] public int Col { get; set; }
    [Parameter] public bool InteractionEnabled { get; set; } = true;

    private bool Full => Session.Board.IsColumnFull(Col);
    private bool Playable => InteractionEnabled && Session.Winner == null && !Session.CpuTurn && !Full;
    private bool Error => Session.ErrorCol == Col;

    private string Cursor => Playable ? "pointer" : "not-allowed";
    private string ColBg => Error ? "rgba(255,45,111,.22)" : "transparent";
    private string ColGlow => Error ? "0 0 18px rgba(255,45,111,.7), inset 0 0 14px rgba(255,45,111,.4)" : "none";
    private string ShakeStyle => Error ? "animation:shake .42s ease;" : "";

    private PlayerConfig? OccupantAt(int row)
    {
        var cell = Session.Board[Col, row];
        return cell == Connect4HoopsArcade.Core.Primitives.Cell.Empty
            ? null
            : Session.Players[(int)cell - 1];
    }

    private async Task Drop()
    {
        if (!Playable) return;
        await Session.TryDrop(Col);
    }
}
```

- [ ] **Step 3: Implement ColumnArrows** (source lines 230-234)

Create `src/Connect4HoopsArcade.Web/Components/Game/ColumnArrows.razor`:
```razor
@using Connect4HoopsArcade.Core.Catalog
@inject GameSession Session

<div style="display:flex;gap:clamp(5px,1.1vw,14px);">
  @for (int col = 0; col < 7; col++)
  {
      var c = col;
      var full = Session.Board.IsColumnFull(c);
      var op = full ? "0.15" : (Session.Winner == null && !Session.CpuTurn ? "0.9" : "0.25");
      <button @onclick="() => Drop(c)"
              style="cursor:@(Playable(c) ? "pointer" : "not-allowed");width:clamp(46px,9.4vh,104px);height:34px;border:none;background:none;color:@CurrentHex;font-size:28px;line-height:1;opacity:@op;@ArrowAnim;transition:transform .15s;">▼</button>
  }
</div>

@code {
    [Parameter] public bool InteractionEnabled { get; set; } = true;
    private string CurrentHex => ColorCatalog.HexOf(Session.Players[Session.Current].ColorId);
    private string ArrowAnim => (Session.IsIdle && Session.Winner == null && !Session.CpuTurn)
        ? "animation:arrowBounce .8s ease-in-out infinite;" : "";
    private bool Playable(int c) => InteractionEnabled && Session.Winner == null && !Session.CpuTurn && !Session.Board.IsColumnFull(c);
    private async Task Drop(int c) { if (Playable(c)) await Session.TryDrop(c); }
}
```

- [ ] **Step 4: Implement BoardGrid** (source lines 235-253, board surface + WinBanner slot)

Create `src/Connect4HoopsArcade.Web/Components/Game/BoardGrid.razor`:
```razor
@inject GameSession Session

<div style="position:relative;padding:clamp(10px,1.8vh,20px);border-radius:26px;background:var(--board);--wf:@WinFlashColor;box-shadow:0 0 0 4px #16259e, 0 16px 0 #0f1a78, 0 24px 44px rgba(0,0,0,.5), inset 0 0 30px rgba(255,255,255,.12);@BoardAnim">
  <div style="display:flex;gap:clamp(5px,1.1vw,14px);">
    @for (int col = 0; col < 7; col++)
    {
        <GameColumn Col="col" InteractionEnabled="InteractionEnabled" />
    }
  </div>
  @if (Session.Winner != null && Session.Screen == Connect4HoopsArcade.Web.State.AppScreen.Game)
  {
      <WinBanner Color="@WinFlashColor"
                 Text="@(Session.WinBy == "resign" ? "¡VICTORIA!" : "¡CONECTA 4!")" />
  }
</div>

@code {
    [Parameter] public bool InteractionEnabled { get; set; } = true;
    private string WinFlashColor => Session.Winner is int w
        ? Connect4HoopsArcade.Core.Catalog.ColorCatalog.HexOf(Session.Players[w].ColorId)
        : "#ffd23f";
    private string BoardAnim => (Session.Winner != null && Session.Screen == Connect4HoopsArcade.Web.State.AppScreen.Game)
        ? "animation:boardWin .9s ease-in-out infinite;" : "";
}
```

- [ ] **Step 5: Build (WinBanner stub needed)** — create a temporary WinBanner stub to compile

Create `src/Connect4HoopsArcade.Web/Components/Game/WinBanner.razor` (final version in Task 4.3):
```razor
<div></div>
@code {
    [Parameter] public string Color { get; set; } = "#ffd23f";
    [Parameter] public string Text { get; set; } = "";
}
```
Then:
```bash
dotnet build src/Connect4HoopsArcade.Web
```
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Components/Game
git commit -m "feat(web): board grid, column, cell, and drop arrows"
```

### Task 4.3: PlayerPanel, NarratorBubble, WinBanner

**Files:**
- Create: `src/Connect4HoopsArcade.Web/Components/Game/PlayerPanel.razor`, `NarratorBubble.razor`
- Modify: `src/Connect4HoopsArcade.Web/Components/Game/WinBanner.razor`

- [ ] **Step 1: Implement PlayerPanel** (source lines 218-226 + panelVM lines 791-817)

Create `src/Connect4HoopsArcade.Web/Components/Game/PlayerPanel.razor`:
```razor
@using Connect4HoopsArcade.Core.Catalog
@inject GameSession Session

<div style="flex:1 1 0;min-width:0;max-width:320px;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:clamp(8px,1.4vh,16px);padding:22px 16px;border-radius:26px;border:2.5px solid @Hex;background:@PanelBg;box-shadow:@PanelGlow;opacity:@PanelOpacity;--tc:@Hex;@PanelAnim;transition:opacity .3s;">
  <div style="width:clamp(76px,9vw,128px);height:clamp(76px,9vw,128px);">
    <AvatarSvg ColorId="@P.ColorId" Face="P.Face" Accessory="P.Accessory" Glow="@Active" />
  </div>
  <div class="font-display" style="font-weight:600;font-size:clamp(22px,2.4vw,36px);line-height:1;text-align:center;max-width:100%;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;">@P.Name</div>
  <div style="font-size:clamp(13px,1.3vw,16px);font-weight:900;letter-spacing:1px;white-space:nowrap;padding:8px 20px;border-radius:999px;background:@BadgeBg;color:@BadgeColor;box-shadow:@BadgeShadow;@BadgeAnim;">@BadgeText</div>
  <div style="width:54%;height:2px;background:rgba(255,255,255,.12);"></div>
  <div style="font-size:11px;font-weight:900;color:rgba(255,255,255,.4);letter-spacing:2px;">PUNTOS</div>
  <div class="font-display" style="font-weight:700;font-size:clamp(50px,6.5vw,92px);line-height:.8;color:@Hex;">@Session.Scores[Index]</div>
</div>

@code {
    [Parameter] public int Index { get; set; }

    private Connect4HoopsArcade.Core.Players.PlayerConfig P => Session.Players[Index];
    private string Hex => ColorCatalog.HexOf(P.ColorId);
    private bool Active => Session.Current == Index && Session.Winner == null;
    private bool Thinking => Active && P.IsCpu;
    private bool Idle => Active && Session.IsIdle && !P.IsCpu;

    private string BadgeText => Thinking ? "⏳ PENSANDO…"
        : Idle ? "👉 ¡TE TOCA!"
        : Active ? "● TU TURNO"
        : "EN ESPERA";
    private string PanelBg => Active ? $"linear-gradient(180deg, {Hex}33, rgba(255,255,255,.02))" : "rgba(255,255,255,.03)";
    private string PanelGlow => Active ? $"0 0 0 3px {Hex}, 0 0 32px {Hex}aa, inset 0 0 16px {Hex}44" : "none";
    private string PanelOpacity => Active ? "1" : "0.42";
    private string PanelAnim => (Active && !Thinking) ? "animation:turnGlow 1.6s ease-in-out infinite;" : "";
    private string BadgeBg => Active ? Hex : "rgba(255,255,255,.08)";
    private string BadgeColor => Active ? "#1a1030" : "rgba(255,255,255,.45)";
    private string BadgeShadow => Active ? $"0 0 18px {Hex}99" : "none";
    private string BadgeAnim => Idle ? "animation:badgePulse .7s ease-in-out infinite;" : "";
}
```

- [ ] **Step 2: Implement NarratorBubble** (source lines 254-257)

Create `src/Connect4HoopsArcade.Web/Components/Game/NarratorBubble.razor`:
```razor
@using Connect4HoopsArcade.Core.Catalog
@inject GameSession Session

@key="Session.Narrator"
<div style="margin-top:clamp(14px,3.4vh,40px);display:flex;align-items:center;gap:12px;max-width:100%;padding:11px 22px;border-radius:18px;background:rgba(255,255,255,.07);border:2px solid @Hex;box-shadow:0 0 22px @(Hex)66;animation:pop .35s ease;">
  <span style="font-size:24px;">🎙️</span>
  <span class="font-display" style="font-weight:600;font-size:clamp(16px,2vw,26px);color:#fff;white-space:nowrap;">@Session.Narrator</span>
</div>

@code {
    private string Hex => ColorCatalog.HexOf(Session.Players[Session.Current].ColorId);
}
```

- [ ] **Step 3: Implement WinBanner** (source line 250)

Overwrite `src/Connect4HoopsArcade.Web/Components/Game/WinBanner.razor`:
```razor
<div style="position:absolute;inset:0;display:flex;align-items:center;justify-content:center;pointer-events:none;z-index:4;">
  <div class="font-display" style="position:absolute;top:50%;left:50%;padding:14px 32px;border-radius:18px;background:rgba(10,8,20,.8);border:3px solid @Color;box-shadow:0 0 46px @Color;font-weight:700;font-size:clamp(30px,5vw,64px);color:#ffd23f;-webkit-text-stroke:2px #1a1030;white-space:nowrap;animation:connectBadge .6s cubic-bezier(.2,.8,.3,1.2) both;">@Text</div>
</div>

@code {
    [Parameter] public string Color { get; set; } = "#ffd23f";
    [Parameter] public string Text { get; set; } = "";
}
```

- [ ] **Step 4: Build**

```bash
dotnet build src/Connect4HoopsArcade.Web
```
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Components/Game/PlayerPanel.razor src/Connect4HoopsArcade.Web/Components/Game/NarratorBubble.razor src/Connect4HoopsArcade.Web/Components/Game/WinBanner.razor
git commit -m "feat(web): player panel, narrator bubble, win banner"
```

### Task 4.4: GameView screen (assemble the board layout)

**Files:**
- Create (overwrite stub): `src/Connect4HoopsArcade.Web/Components/Screens/GameView.razor`

- [ ] **Step 1: Implement GameView** (source lines 200-271)

Overwrite `src/Connect4HoopsArcade.Web/Components/Screens/GameView.razor`:
```razor
@using Connect4HoopsArcade.Web.Components.Game
@using Connect4HoopsArcade.Web.Components.Modals
@inject GameSession Session

<div class="game-screen" style="position:absolute;inset:0;z-index:5;">
  <div class="rotate-hint" style="position:absolute;inset:0;flex-direction:column;align-items:center;justify-content:center;gap:18px;text-align:center;padding:30px;background:#0c0922;">
    <div style="font-size:64px;animation:spin 3s linear infinite;">📱</div>
    <div class="font-display" style="font-weight:700;font-size:26px;color:#22d3ee;">Gira tu dispositivo</div>
    <div style="font-weight:700;color:rgba(255,255,255,.6);max-width:300px;">El tablero se ve mejor en horizontal. Pon tu teléfono de lado para jugar.</div>
  </div>

  <div class="board-wrap" style="position:absolute;inset:0;display:flex;align-items:stretch;gap:clamp(12px,2vw,28px);padding:clamp(58px,8vh,84px) clamp(16px,2.4vw,38px) clamp(14px,2.2vh,26px);">
    <div style="position:absolute;top:14px;left:50%;transform:translateX(-50%);z-index:3;display:flex;gap:10px;align-items:center;">
      <button @onclick="Session.OpenSettings" class="top-btn">⚙ Ajustes</button>
      <button @onclick="Session.ResetBoard" class="top-btn top-btn--yellow">🔄 Reiniciar</button>
      <button @onclick="Session.Resign" class="top-btn">🏳️ Rendirse</button>
    </div>

    <PlayerPanel Index="0" />

    <div style="flex:0 1 auto;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:clamp(8px,1.4vh,16px);min-width:0;">
      <ColumnArrows InteractionEnabled="InteractionEnabled" />
      <BoardGrid InteractionEnabled="InteractionEnabled" />
      <NarratorBubble />
    </div>

    <PlayerPanel Index="1" />
  </div>
</div>

@code {
    // In Physical mode, on-screen clicks are disabled (board is the only input). Wired in Phase 5;
    // defaults to enabled (Digital) for now.
    [CascadingParameter] public bool InteractionEnabled { get; set; } = true;
}
```

- [ ] **Step 2: Add top-btn styles to board.css**

Append to `src/Connect4HoopsArcade.Web/wwwroot/css/board.css`:
```css
.top-btn { cursor:pointer; display:flex; align-items:center; gap:7px; height:42px; padding:0 18px; border-radius:999px; border:1.5px solid rgba(255,255,255,.16); background:rgba(255,255,255,.06); color:#fff; font-weight:800; font-size:14px; }
.top-btn--yellow { border:1.5px solid rgba(255,210,63,.5); background:rgba(255,210,63,.12); color:#ffd23f; }
```

- [ ] **Step 3: Build + play a full game (manual)**

```bash
dotnet run --project src/Connect4HoopsArcade.Web
```
Play 2-player to a win. Verify: chips fall with the drop animation, turns alternate (panel glow swaps), narrator updates, column-full shows shake + message and keeps the turn, a 4-in-a-row triggers the win banner + board pulse, then the victory screen appears (~2.7s later). Also force a column full and a near-win (3 in a row) to confirm the narrator threat line.

- [ ] **Step 4: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Components/Screens/GameView.razor src/Connect4HoopsArcade.Web/wwwroot/css/board.css
git commit -m "feat(web): assemble game board screen with top controls"
```

### Task 4.5: Victory & Draw modals (confetti, rematch, reset)

**Files:**
- Create: `src/Connect4HoopsArcade.Web/Components/Modals/VictoryModal.razor`, `DrawModal.razor`
- Modify: `src/Connect4HoopsArcade.Web/Components/Layout/AppShell.razor`

- [ ] **Step 1: Implement VictoryModal** (source lines 273-293)

Create `src/Connect4HoopsArcade.Web/Components/Modals/VictoryModal.razor`:
```razor
@using Connect4HoopsArcade.Core.Catalog
@inject GameSession Session

<div style="position:absolute;inset:0;z-index:20;display:flex;flex-direction:column;align-items:center;justify-content:center;padding:24px;background:radial-gradient(110% 90% at 50% 30%, @(WinnerHex)44 0%, rgba(8,6,15,.92) 70%);">
  <div style="position:absolute;inset:0;overflow:hidden;pointer-events:none;">
    @foreach (var cf in Session.Confetti)
    {
        <div style="position:absolute;top:-30px;left:@cf.Left;width:@cf.Size;height:@cf.Size;background:@cf.Color;border-radius:@cf.Radius;animation:confettiFall @cf.Duration linear @cf.Delay infinite;"></div>
    }
  </div>
  <div style="position:relative;display:flex;flex-direction:column;align-items:center;animation:pop .5s ease;">
    <div class="font-display" style="font-weight:700;font-size:clamp(36px,8vw,96px);color:#ffd23f;text-shadow:0 6px 0 #c98800,0 0 40px rgba(255,210,63,.6);-webkit-text-stroke:3px #1a1030;letter-spacing:1px;">@Title</div>
    <div style="width:clamp(150px,26vw,230px);height:clamp(150px,26vw,230px);margin:14px 0;filter:drop-shadow(0 0 40px @WinnerHex);">
      <AvatarSvg ColorId="@Winner.ColorId" Face="Winner.Face" Accessory="Winner.Accessory" Glow="true" />
    </div>
    <div class="font-display" style="font-weight:700;font-size:clamp(26px,5vw,52px);color:#fff;text-shadow:0 4px 0 rgba(0,0,0,.4);">¡Ganó @Winner.Name!</div>
    <div style="font-weight:800;font-size:18px;color:rgba(255,255,255,.65);margin-top:6px;">Marcador @Session.Scores[0] — @Session.Scores[1]</div>
    <div style="display:flex;gap:14px;margin-top:30px;flex-wrap:wrap;justify-content:center;">
      <button @onclick="Session.Rematch" class="font-display" style="cursor:pointer;padding:15px 34px;border-radius:999px;border:none;background:linear-gradient(180deg,#ffd23f,#f5a700);color:#1a1030;font-weight:700;font-size:22px;box-shadow:0 6px 0 #c98800;">🔄 Revancha</button>
      <button @onclick="Session.ChangePlayers" class="font-display" style="cursor:pointer;padding:15px 30px;border-radius:999px;border:2px solid rgba(255,255,255,.3);background:rgba(255,255,255,.06);color:#fff;font-weight:600;font-size:22px;">👥 Cambiar jugadores</button>
      <button @onclick="Session.GoSplash" class="font-display" style="cursor:pointer;padding:15px 30px;border-radius:999px;border:2px solid rgba(255,255,255,.3);background:rgba(255,255,255,.06);color:#fff;font-weight:600;font-size:22px;">🏠 Inicio</button>
    </div>
  </div>
</div>

@code {
    private Connect4HoopsArcade.Core.Players.PlayerConfig Winner => Session.Players[Session.Winner ?? 0];
    private string WinnerHex => ColorCatalog.HexOf(Winner.ColorId);
    private string Title => Session.WinBy == "resign" ? "¡VICTORIA!" : "¡CONECTA 4!";
}
```

- [ ] **Step 2: Implement DrawModal** (source lines 295-312)

Create `src/Connect4HoopsArcade.Web/Components/Modals/DrawModal.razor`:
```razor
@inject GameSession Session

<div style="position:absolute;inset:0;z-index:20;display:flex;align-items:center;justify-content:center;padding:24px;background:rgba(8,6,15,.85);">
  <div style="display:flex;flex-direction:column;align-items:center;text-align:center;padding:40px 44px;border-radius:28px;border:3px solid #22d3ee;background:linear-gradient(180deg,#15294a,#0d1830);box-shadow:0 0 40px rgba(34,211,238,.35);animation:pop .45s ease;max-width:480px;">
    <div style="display:flex;margin-bottom:8px;">
      <div style="width:88px;height:88px;"><AvatarSvg ColorId="@Session.Players[0].ColorId" Face="Session.Players[0].Face" Accessory="Session.Players[0].Accessory" /></div>
      <div style="width:88px;height:88px;margin-left:-14px;"><AvatarSvg ColorId="@Session.Players[1].ColorId" Face="Session.Players[1].Face" Accessory="Session.Players[1].Accessory" /></div>
    </div>
    <div class="font-display" style="font-weight:700;font-size:clamp(30px,6vw,56px);color:#22d3ee;text-shadow:0 4px 0 rgba(0,0,0,.4);">¡EMPATE!</div>
    <div style="font-weight:800;font-size:18px;color:rgba(255,255,255,.75);margin:10px 0 4px;">Tablero lleno. ¡Nadie cede! 🏀</div>
    <div style="font-weight:700;font-size:15px;color:rgba(255,255,255,.5);">Buen duelo. ¿La revancha define todo?</div>
    <div style="display:flex;gap:12px;margin-top:26px;flex-wrap:wrap;justify-content:center;">
      <button @onclick="Session.Rematch" class="font-display" style="cursor:pointer;padding:14px 32px;border-radius:999px;border:none;background:linear-gradient(180deg,#ffd23f,#f5a700);color:#1a1030;font-weight:700;font-size:21px;box-shadow:0 5px 0 #c98800;">🔄 Revancha</button>
      <button @onclick="Session.GoSplash" class="font-display" style="cursor:pointer;padding:14px 28px;border-radius:999px;border:2px solid rgba(255,255,255,.3);background:rgba(255,255,255,.06);color:#fff;font-weight:600;font-size:21px;">🏠 Inicio</button>
    </div>
  </div>
</div>
```

- [ ] **Step 3: Wire modals into AppShell**

In `src/Connect4HoopsArcade.Web/Components/Layout/AppShell.razor`, add `@using` and the two cases.
Update the `@using` block and the switch:
```razor
@using Connect4HoopsArcade.Web.Components.Modals
```
Add cases inside the `switch`:
```razor
    case AppScreen.Victory:  <GameView />; <VictoryModal />; break;
    case AppScreen.Draw:     <GameView />; <DrawModal />; break;
```
(Rendering `GameView` behind the modal preserves the board backdrop.)

- [ ] **Step 4: Build + verify victory and draw**

```bash
dotnet build src/Connect4HoopsArcade.Web
```
Run; win a game → confetti + winner avatar + scoreboard + Revancha/Cambiar/Inicio. Use Rendirse → "¡VICTORIA!" title. Fill the board without a winner → draw modal.

- [ ] **Step 5: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Components/Modals src/Connect4HoopsArcade.Web/Components/Layout/AppShell.razor
git commit -m "feat(web): victory and draw modals with confetti"
```

---

## Phase 5 — Input Architecture (Digital / Physical)

> Establishes the `IMoveSource → MoveRouter → GameSession.TryDrop` pipeline so clicks, keyboard, and
> (future) sensors share one code path. Physical mode disables on-screen clicks; keyboard 1–7 always works
> as a sensor simulator. Mode is persisted + autodetected from sensor connection.

> **IMPORTANT (added during Phase 4):** the gameplay components (`GameView`, `GameColumn`, `ColumnArrows`,
> `BoardGrid`, `PlayerPanel`, `NarratorBubble`, `VictoryModal`, `DrawModal`) now use
> `@inherits SessionComponentBase` instead of `@inject GameSession Session`. The base
> (`Components/SessionComponentBase.cs`) injects `Session` (as a `protected` property) AND subscribes to
> `StateChanged` so each component re-renders on every state change (AppShell's cascade alone left siblings
> stale). When Phase 5 says "add `@inject MoveRouter Router` to GameColumn/ColumnArrows", ADD that line
> alongside the existing `@inherits SessionComponentBase` (do not remove `@inherits`, and do not re-add
> `@inject GameSession Session` — `Session` already comes from the base). GameView's `@code` replacement
> (computed `InteractionEnabled`) stays as-is under `@inherits`.

### Task 5.1: PlayMode in GameSession + MoveRouter

**Files:**
- Modify: `src/Connect4HoopsArcade.Web/State/GameSession.cs`
- Create: `src/Connect4HoopsArcade.Web/Input/MoveOrigin.cs`, `IMoveSource.cs`, `MoveRouter.cs`
- Modify: `src/Connect4HoopsArcade.Web/Program.cs`

- [ ] **Step 1: Add PlayMode + sensor connection to GameSession**

Add these members to `src/Connect4HoopsArcade.Web/State/GameSession.cs` (inside the class, near the other
public properties):
```csharp
    public Connect4HoopsArcade.Web.Models.PlayMode Mode2 { get; private set; } = Connect4HoopsArcade.Web.Models.PlayMode.Digital;
    public bool SensorConnected { get; private set; }
    public event Action? ModeChanged;

    public void SetPlayMode(Connect4HoopsArcade.Web.Models.PlayMode mode)
    {
        if (Mode2 == mode) return;
        Mode2 = mode;
        ModeChanged?.Invoke();
        Notify();
    }

    /// <summary>Reports sensor link state; autodetection promotes Digital→Physical and falls back on loss.</summary>
    public void SetSensorConnected(bool connected, bool autoSwitch = true)
    {
        SensorConnected = connected;
        if (autoSwitch) SetPlayMode(connected ? Connect4HoopsArcade.Web.Models.PlayMode.Physical
                                              : Connect4HoopsArcade.Web.Models.PlayMode.Digital);
        else Notify();
    }
```
> Note: the property is named `Mode2` to avoid colliding with the existing `GameMode Mode` (1P/2P). It
> represents `PlayMode` (Digital/Physical). Keep both — they are orthogonal concepts.

- [ ] **Step 2: Create MoveOrigin + IMoveSource**

Create `src/Connect4HoopsArcade.Web/Input/MoveOrigin.cs`:
```csharp
namespace Connect4HoopsArcade.Web.Input;

public enum MoveOrigin { Screen, Keyboard, Sensor }
```

Create `src/Connect4HoopsArcade.Web/Input/IMoveSource.cs`:
```csharp
namespace Connect4HoopsArcade.Web.Input;

/// <summary>A producer of column triggers (clicks, keyboard, or physical sensors).</summary>
public interface IMoveSource
{
    /// <summary>Raised with a 0-based column index (0-6) when this source detects a drop.</summary>
    event Action<int>? ColumnTriggered;
}
```

- [ ] **Step 3: Create MoveRouter (single funnel with dedup + mode gating)**

Create `src/Connect4HoopsArcade.Web/Input/MoveRouter.cs`:
```csharp
using Connect4HoopsArcade.Web.Models;
using Connect4HoopsArcade.Web.State;

namespace Connect4HoopsArcade.Web.Input;

/// <summary>Funnels every move source into <see cref="GameSession.TryDrop"/> with debounce + mode rules.</summary>
public sealed class MoveRouter
{
    private readonly GameSession _session;
    private int _lastCol = -1;
    private DateTime _lastAt = DateTime.MinValue;
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromMilliseconds(250);

    public MoveRouter(GameSession session) => _session = session;

    public async Task Route(int col, MoveOrigin origin)
    {
        if (col < 0 || col > 6) return;
        if (!OriginAllowed(origin)) return;

        var now = DateTime.UtcNow;
        if (col == _lastCol && now - _lastAt < DebounceWindow) return;   // ignore double sensor / rapid repeat
        _lastCol = col; _lastAt = now;

        await _session.TryDrop(col);
    }

    private bool OriginAllowed(MoveOrigin origin) => _session.Mode2 switch
    {
        // Digital: screen + keyboard play; sensor events ignored (no physical board).
        PlayMode.Digital  => origin is MoveOrigin.Screen or MoveOrigin.Keyboard,
        // Physical: sensors are authoritative; keyboard simulates; on-screen clicks disabled.
        PlayMode.Physical => origin is MoveOrigin.Sensor or MoveOrigin.Keyboard,
        _ => true,
    };
}
```

- [ ] **Step 4: Register MoveRouter in DI**

In `src/Connect4HoopsArcade.Web/Program.cs`, after the `GameSession` registration add:
```csharp
builder.Services.AddSingleton<Connect4HoopsArcade.Web.Input.MoveRouter>();
```

- [ ] **Step 5: Build**

```bash
dotnet build src/Connect4HoopsArcade.Web
```
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Input src/Connect4HoopsArcade.Web/State/GameSession.cs src/Connect4HoopsArcade.Web/Program.cs
git commit -m "feat(web): MoveRouter pipeline and PlayMode in GameSession"
```

### Task 5.2: Route board clicks through MoveRouter + disable clicks in Physical mode

**Files:**
- Modify: `src/Connect4HoopsArcade.Web/Components/Game/GameColumn.razor`, `ColumnArrows.razor`
- Modify: `src/Connect4HoopsArcade.Web/Components/Screens/GameView.razor`

- [ ] **Step 1: Route GameColumn clicks through MoveRouter**

In `src/Connect4HoopsArcade.Web/Components/Game/GameColumn.razor`, add the injection and replace the
`Drop` method:
```razor
@inject Connect4HoopsArcade.Web.Input.MoveRouter Router
```
Replace the `Drop` method body:
```csharp
    private async Task Drop()
    {
        if (!Playable) return;
        await Router.Route(Col, Connect4HoopsArcade.Web.Input.MoveOrigin.Screen);
    }
```

- [ ] **Step 2: Route ColumnArrows clicks through MoveRouter**

In `src/Connect4HoopsArcade.Web/Components/Game/ColumnArrows.razor`, add:
```razor
@inject Connect4HoopsArcade.Web.Input.MoveRouter Router
```
Replace the `Drop` method:
```csharp
    private async Task Drop(int c) { if (Playable(c)) await Router.Route(c, Connect4HoopsArcade.Web.Input.MoveOrigin.Screen); }
```

- [ ] **Step 3: Drive InteractionEnabled from PlayMode in GameView**

In `src/Connect4HoopsArcade.Web/Components/Screens/GameView.razor`, replace the `@code` block so clicks
are off in Physical mode:
```csharp
@code {
    private bool InteractionEnabled => Session.Mode2 == Connect4HoopsArcade.Web.Models.PlayMode.Digital;
}
```
(The `InteractionEnabled` is already passed to `ColumnArrows` and `BoardGrid` in the markup.)

- [ ] **Step 4: Build + verify**

```bash
dotnet build src/Connect4HoopsArcade.Web
```
Run; play normally (Digital) — clicks still work. (Physical-mode disabling is verified after Task 5.3 wires the toggle.)

- [ ] **Step 5: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Components/Game/GameColumn.razor src/Connect4HoopsArcade.Web/Components/Game/ColumnArrows.razor src/Connect4HoopsArcade.Web/Components/Screens/GameView.razor
git commit -m "feat(web): route board input through MoveRouter; disable clicks in physical mode"
```

### Task 5.3: Keyboard input (1–7) + sensor connection service

**Files:**
- Create: `src/Connect4HoopsArcade.Web/Services/Abstractions/ISensorConnection.cs`
- Create: `src/Connect4HoopsArcade.Web/Services/SensorConnectionService.cs`
- Create: `src/Connect4HoopsArcade.Web/Services/KeyboardInputService.cs`
- Modify: `src/Connect4HoopsArcade.Web/Components/Layout/AppShell.razor`
- Modify: `src/Connect4HoopsArcade.Web/Program.cs`

- [ ] **Step 1: Create ISensorConnection + SensorConnectionService**

Create `src/Connect4HoopsArcade.Web/Services/Abstractions/ISensorConnection.cs`:
```csharp
using Connect4HoopsArcade.Web.Input;

namespace Connect4HoopsArcade.Web.Services.Abstractions;

/// <summary>Physical-board channel: connection state + column events. Simulated until ESP32 is wired.</summary>
public interface ISensorConnection : IMoveSource
{
    bool Connected { get; }
    event Action<bool>? ConnectionChanged;
    void Pulse(int col);              // simulate a sensor firing (sensor-test / keyboard sim)
    void SetConnected(bool connected);
}
```

Create `src/Connect4HoopsArcade.Web/Services/SensorConnectionService.cs`:
```csharp
using Connect4HoopsArcade.Web.Services.Abstractions;

namespace Connect4HoopsArcade.Web.Services;

/// <summary>
/// In-memory sensor channel. A future WebSocket/ESP32 client replaces the internals and raises the same
/// events — game logic is untouched. Default disconnected (no hardware in this phase).
/// </summary>
public sealed class SensorConnectionService : ISensorConnection
{
    public bool Connected { get; private set; }
    public event Action<int>? ColumnTriggered;
    public event Action<bool>? ConnectionChanged;

    public void Pulse(int col) => ColumnTriggered?.Invoke(col);

    public void SetConnected(bool connected)
    {
        if (Connected == connected) return;
        Connected = connected;
        ConnectionChanged?.Invoke(connected);
    }
}
```

- [ ] **Step 2: Create KeyboardInputService**

Create `src/Connect4HoopsArcade.Web/Services/KeyboardInputService.cs`:
```csharp
using Microsoft.JSInterop;
using Connect4HoopsArcade.Web.Input;

namespace Connect4HoopsArcade.Web.Services;

/// <summary>Bridges window keydown (1-7) from arcade.js into the MoveRouter as a Keyboard origin.</summary>
public sealed class KeyboardInputService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly MoveRouter _router;
    private readonly ISensorConnectionProxy _sensor;
    private DotNetObjectReference<KeyboardInputService>? _ref;

    public KeyboardInputService(IJSRuntime js, MoveRouter router, ISensorConnectionProxy sensor)
    {
        _js = js; _router = router; _sensor = sensor;
    }

    public async Task RegisterAsync()
    {
        _ref = DotNetObjectReference.Create(this);
        await _js.InvokeVoidAsync("ArcadeKeyboard.register", _ref);
    }

    [JSInvokable]
    public async Task OnColumnKey(int col)
    {
        // Keyboard doubles as a sensor simulator (lights the sensor-test panel) and a move source.
        _sensor.Pulse(col);
        await _router.Route(col, MoveOrigin.Keyboard);
    }

    public ValueTask DisposeAsync()
    {
        _ref?.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>Thin proxy so the keyboard service can flash the sensor panel without a hard dependency cycle.</summary>
public interface ISensorConnectionProxy { void Pulse(int col); }
```

> Add `ISensorConnectionProxy` implementation to `SensorConnectionService`: append `, ISensorConnectionProxy`
> to its class declaration interface list (it already has a `Pulse` method, satisfying the proxy).

- [ ] **Step 3: Register services + start keyboard in AppShell**

In `src/Connect4HoopsArcade.Web/Program.cs` add:
```csharp
builder.Services.AddSingleton<Connect4HoopsArcade.Web.Services.SensorConnectionService>();
builder.Services.AddSingleton<Connect4HoopsArcade.Web.Services.Abstractions.ISensorConnection>(
    sp => sp.GetRequiredService<Connect4HoopsArcade.Web.Services.SensorConnectionService>());
builder.Services.AddSingleton<Connect4HoopsArcade.Web.Services.ISensorConnectionProxy>(
    sp => sp.GetRequiredService<Connect4HoopsArcade.Web.Services.SensorConnectionService>());
builder.Services.AddScoped<Connect4HoopsArcade.Web.Services.KeyboardInputService>();
```

In `src/Connect4HoopsArcade.Web/Components/Layout/AppShell.razor`, inject and wire on first render, and
subscribe the sensor channel to the router. Replace the `@code` block:
```razor
@inject Connect4HoopsArcade.Web.Services.KeyboardInputService Keyboard
@inject Connect4HoopsArcade.Web.Services.SensorConnectionService Sensor
@inject Connect4HoopsArcade.Web.Input.MoveRouter Router
```
```csharp
@code {
    protected override void OnInitialized()
    {
        Session.StateChanged += OnChanged;
        Session.ModeChanged += OnChanged;
        Sensor.ConnectionChanged += OnSensorConn;
        Sensor.ColumnTriggered += OnSensorColumn;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) await Keyboard.RegisterAsync();
    }

    private async void OnSensorColumn(int col) =>
        await Router.Route(col, Connect4HoopsArcade.Web.Input.MoveOrigin.Sensor);

    private void OnSensorConn(bool connected) => Session.SetSensorConnected(connected);

    public void Dispose()
    {
        Session.StateChanged -= OnChanged;
        Session.ModeChanged -= OnChanged;
        Sensor.ConnectionChanged -= OnSensorConn;
        Sensor.ColumnTriggered -= OnSensorColumn;
    }

    private void OnChanged() => InvokeAsync(StateHasChanged);
}
```

- [ ] **Step 4: Build + verify keyboard play**

```bash
dotnet build src/Connect4HoopsArcade.Web
```
Run a game; press keys 1–7 to drop chips into the matching columns. Confirm a chip falls per keypress and the move flow proceeds identically to clicks.

- [ ] **Step 5: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Services src/Connect4HoopsArcade.Web/Components/Layout/AppShell.razor src/Connect4HoopsArcade.Web/Program.cs
git commit -m "feat(web): keyboard 1-7 input and sensor connection channel"
```

---

## Phase 6 — Audio (SFX, Voice, Music, Narrator)

> Implements `AudioService` (over `window.ArcadeAudio`) and `NarratorService`, then subscribes them to
> `GameSession` events. Audio init happens on the first user gesture (splash tap) to satisfy autoplay rules.
> Missing files never throw (the JS layer `console.warn`s).

### Task 6.1: IAudioService + AudioService (interop wrapper with cooldowns)

**Files:**
- Create: `src/Connect4HoopsArcade.Web/Services/Abstractions/IAudioService.cs`
- Create: `src/Connect4HoopsArcade.Web/Services/AudioService.cs`
- Modify: `src/Connect4HoopsArcade.Web/Program.cs`

- [ ] **Step 1: Create IAudioService**

Create `src/Connect4HoopsArcade.Web/Services/Abstractions/IAudioService.cs`:
```csharp
namespace Connect4HoopsArcade.Web.Services.Abstractions;

public interface IAudioService
{
    Task InitAsync();
    Task PlaySfxAsync(string key, int cooldownMs = 0);
    Task PlayVoiceAsync(string key);
    Task PlayRandomVoiceAsync(IReadOnlyList<string> keys);
    Task PlayMusicAsync(string key, bool loop = true);
    Task StopMusicAsync();
    Task SetVolumesAsync(int sfx, int voice, int music);
    Task MuteAsync();
    Task UnmuteAsync();
    bool VoicesEnabled { get; set; }
}
```

- [ ] **Step 2: Create AudioService**

Create `src/Connect4HoopsArcade.Web/Services/AudioService.cs`:
```csharp
using Microsoft.JSInterop;
using Connect4HoopsArcade.Web.Services.Abstractions;

namespace Connect4HoopsArcade.Web.Services;

public sealed class AudioService : IAudioService
{
    private readonly IJSRuntime _js;
    private bool _initialized;
    private static readonly Random Rng = new();

    public bool VoicesEnabled { get; set; } = true;

    public AudioService(IJSRuntime js) => _js = js;

    public async Task InitAsync()
    {
        if (_initialized) return;
        _initialized = true;
        await Safe("ArcadeAudio.init");
    }

    public Task PlaySfxAsync(string key, int cooldownMs = 0) => Safe("ArcadeAudio.playSfx", key, cooldownMs);

    public Task PlayVoiceAsync(string key) => VoicesEnabled ? Safe("ArcadeAudio.playVoice", key) : Task.CompletedTask;

    public Task PlayRandomVoiceAsync(IReadOnlyList<string> keys)
    {
        if (!VoicesEnabled || keys.Count == 0) return Task.CompletedTask;
        return PlayVoiceAsync(keys[Rng.Next(keys.Count)]);
    }

    public Task PlayMusicAsync(string key, bool loop = true) => Safe("ArcadeAudio.playMusic", key, loop);
    public Task StopMusicAsync() => Safe("ArcadeAudio.stopMusic");
    public Task SetVolumesAsync(int sfx, int voice, int music) =>
        Safe("ArcadeAudio.setVolumes", sfx / 100.0, voice / 100.0, music / 100.0);
    public Task MuteAsync() => Safe("ArcadeAudio.mute");
    public Task UnmuteAsync() => Safe("ArcadeAudio.unmute");

    private async Task Safe(string fn, params object[] args)
    {
        try { await _js.InvokeVoidAsync(fn, args); }
        catch (Exception e) { Console.WriteLine($"[Audio] {fn} failed: {e.Message}"); }
    }
}
```

- [ ] **Step 3: Register in DI**

In `src/Connect4HoopsArcade.Web/Program.cs` add:
```csharp
builder.Services.AddSingleton<Connect4HoopsArcade.Web.Services.Abstractions.IAudioService,
                              Connect4HoopsArcade.Web.Services.AudioService>();
```

- [ ] **Step 4: Build**

```bash
dotnet build src/Connect4HoopsArcade.Web
```
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Services/Abstractions/IAudioService.cs src/Connect4HoopsArcade.Web/Services/AudioService.cs src/Connect4HoopsArcade.Web/Program.cs
git commit -m "feat(web): AudioService interop wrapper with cooldown support"
```

### Task 6.2: AudioKeys catalog + NarratorService (event → sound mapping)

**Files:**
- Create: `src/Connect4HoopsArcade.Web/Services/AudioKeys.cs`
- Create: `src/Connect4HoopsArcade.Web/Services/NarratorService.cs`
- Modify: `src/Connect4HoopsArcade.Web/Program.cs`

- [ ] **Step 1: Create AudioKeys (real on-disk paths)**

Create `src/Connect4HoopsArcade.Web/Services/AudioKeys.cs`:
```csharp
namespace Connect4HoopsArcade.Web.Services;

/// <summary>Audio file keys, relative to wwwroot/audio/. Matches the actual flat folder layout.</summary>
public static class AudioKeys
{
    // SFX
    public const string ButtonClick = "ui/button-click.mp3";
    public const string MenuMove    = "ui/menu-move.mp3";
    public const string Back        = "ui/back.mp3";
    public const string ChipDrop    = "game/chip-drop.mp3";
    public const string TurnChange  = "game/turn-change.mp3";
    public const string AlmostWin   = "game/almost-win.mp3";
    public const string ColumnFull  = "game/column-full.mp3";
    public const string VictorySfx  = "victory/connect-four.mp3";
    public const string WinSfx      = "victory/win.mp3";
    public const string DrawSfx     = "victory/draw.mp3";

    // Music
    public const string AttractLoop = "music/attract-loop.mp3";

    // Voice groups
    public static readonly string[] PlayerOneTurn = { "voice/player-one-turn-01.mp3", "voice/player-one-turn-02.mp3", "voice/player-one-turn-03.mp3" };
    public static readonly string[] PlayerTwoTurn = { "voice/player-two-turn-01.mp3", "voice/player-two-turn-02.mp3", "voice/player-two-turn-03.mp3" };
    public static readonly string[] GreatMove     = { "voice/great-move-01.mp3", "voice/great-move-02.mp3", "voice/great-move-03.mp3", "voice/great-move-04.mp3", "voice/great-move-05.mp3" };
    public static readonly string[] AlmostWinV    = { "voice/almost-win-01.mp3", "voice/almost-win-02.mp3", "voice/almost-win-03.mp3", "voice/almost-win-04.mp3" };
    public static readonly string[] ColumnFullV   = { "voice/column-full-01.mp3", "voice/column-full-02.mp3", "voice/column-full-03.mp3", "voice/column-full-04.mp3" };
    public static readonly string[] VictoryV      = { "voice/winner-01.mp3", "voice/victory-01.mp3", "voice/victory-02.mp3", "voice/victory-03.mp3" };
    public static readonly string[] DrawV         = { "voice/draw-01.mp3", "voice/draw-02.mp3", "voice/draw-03.mp3" };

    // Single voices
    public const string Welcome        = "voice/welcome-01.mp3";
    public const string SelectGameMode = "voice/select-game-mode-01.mp3";
    public const string ChooseCharacter= "voice/choose-character-01.mp3";
    public const string GetReady       = "voice/get-ready-01.mp3";
    public const string ConnectFourV   = "voice/connect-four-01.mp3";
    public const string Rematch        = "voice/rematch-01.mp3";
}
```

- [ ] **Step 2: Create NarratorService (subscribes to GameSession events)**

Create `src/Connect4HoopsArcade.Web/Services/NarratorService.cs`:
```csharp
using Connect4HoopsArcade.Web.Services.Abstractions;
using Connect4HoopsArcade.Web.State;

namespace Connect4HoopsArcade.Web.Services;

/// <summary>
/// Maps game events to SFX + (sparingly) voice lines. Voice is reserved for key moments; ordinary moves
/// use SFX only. Cooldowns prevent physical double-triggers from spamming sound.
/// </summary>
public sealed class NarratorService : IDisposable
{
    private readonly GameSession _session;
    private readonly IAudioService _audio;
    private static readonly Random Rng = new();

    public NarratorService(GameSession session, IAudioService audio)
    {
        _session = session;
        _audio = audio;
        _session.GameStarted += OnGameStarted;
        _session.ChipDropped += OnChipDropped;
        _session.TurnChanged += OnTurnChanged;
        _session.ColumnFull  += OnColumnFull;
        _session.ThreatRaised += OnThreat;
        _session.Won += OnWon;
        _session.Drew += OnDrew;
    }

    private async void OnGameStarted() => await _audio.PlayVoiceAsync(AudioKeys.GetReady);

    private async void OnChipDropped()
    {
        await _audio.PlaySfxAsync(AudioKeys.ChipDrop);
        // Occasional praise (~1 in 6), never on every move.
        if (Rng.Next(6) == 0) await _audio.PlayRandomVoiceAsync(AudioKeys.GreatMove);
    }

    private async void OnTurnChanged(int current)
    {
        await _audio.PlaySfxAsync(AudioKeys.TurnChange, cooldownMs: 300);
        await _audio.PlayRandomVoiceAsync(current == 0 ? AudioKeys.PlayerOneTurn : AudioKeys.PlayerTwoTurn);
    }

    private async void OnColumnFull()
    {
        await _audio.PlaySfxAsync(AudioKeys.ColumnFull, cooldownMs: 800);
        await _audio.PlayRandomVoiceAsync(AudioKeys.ColumnFullV);
    }

    private async void OnThreat()
    {
        await _audio.PlaySfxAsync(AudioKeys.AlmostWin, cooldownMs: 800);
        await _audio.PlayRandomVoiceAsync(AudioKeys.AlmostWinV);
    }

    private async void OnWon(int winner)
    {
        await _audio.PlaySfxAsync(AudioKeys.VictorySfx);
        await _audio.PlayVoiceAsync(AudioKeys.ConnectFourV);
        await _audio.PlayRandomVoiceAsync(AudioKeys.VictoryV);
    }

    private async void OnDrew()
    {
        await _audio.PlaySfxAsync(AudioKeys.DrawSfx);
        await _audio.PlayRandomVoiceAsync(AudioKeys.DrawV);
    }

    public void Dispose()
    {
        _session.GameStarted -= OnGameStarted;
        _session.ChipDropped -= OnChipDropped;
        _session.TurnChanged -= OnTurnChanged;
        _session.ColumnFull  -= OnColumnFull;
        _session.ThreatRaised -= OnThreat;
        _session.Won -= OnWon;
        _session.Drew -= OnDrew;
    }
}
```

- [ ] **Step 3: Register NarratorService and force eager creation**

In `src/Connect4HoopsArcade.Web/Program.cs` add the registration **and** resolve it once after build so it
subscribes:
```csharp
builder.Services.AddSingleton<Connect4HoopsArcade.Web.Services.NarratorService>();

var host = builder.Build();
host.Services.GetRequiredService<Connect4HoopsArcade.Web.Services.NarratorService>(); // eager: wire event subscriptions
await host.RunAsync();
```
Replace the existing `await builder.Build().RunAsync();` line with the three lines above. Add
`using Microsoft.Extensions.DependencyInjection;` at the top if not already present.

- [ ] **Step 4: Build**

```bash
dotnet build src/Connect4HoopsArcade.Web
```
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Services/AudioKeys.cs src/Connect4HoopsArcade.Web/Services/NarratorService.cs src/Connect4HoopsArcade.Web/Program.cs
git commit -m "feat(web): NarratorService maps game events to SFX/voice"
```

### Task 6.3: Audio init on first gesture + music + UI clicks

**Files:**
- Modify: `src/Connect4HoopsArcade.Web/Components/Screens/AttractMode.razor`
- Modify: `src/Connect4HoopsArcade.Web/Components/Screens/GameModeSelector.razor`

- [ ] **Step 1: Init audio + welcome + attract music on splash tap**

In `src/Connect4HoopsArcade.Web/Components/Screens/AttractMode.razor`, inject audio and update `Start`:
```razor
@inject Connect4HoopsArcade.Web.Services.Abstractions.IAudioService Audio
```
Replace the `@code` block:
```csharp
@code {
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) await Audio.PlayMusicAsync(Connect4HoopsArcade.Web.Services.AudioKeys.AttractLoop);
    }

    private async Task Start()
    {
        await Audio.InitAsync();
        await Audio.PlayVoiceAsync(Connect4HoopsArcade.Web.Services.AudioKeys.Welcome);
        await Audio.PlaySfxAsync(Connect4HoopsArcade.Web.Services.AudioKeys.ButtonClick);
        Session.GoMode();
    }
}
```
> Note: browsers block audio before a gesture; `PlayMusicAsync` on first render may be deferred until the
> tap. `InitAsync` + the click in `Start` guarantee unlock. This is acceptable.

- [ ] **Step 2: Play voice cues on mode/character screens**

In `src/Connect4HoopsArcade.Web/Components/Screens/GameModeSelector.razor`, inject audio and play
`SelectGameMode` on first render; play `ChooseCharacter` from `PlayerSetup`. Add to GameModeSelector:
```razor
@inject Connect4HoopsArcade.Web.Services.Abstractions.IAudioService Audio
```
```csharp
@code {
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) await Audio.PlayVoiceAsync(Connect4HoopsArcade.Web.Services.AudioKeys.SelectGameMode);
    }
}
```
And in `src/Connect4HoopsArcade.Web/Components/Screens/PlayerSetup.razor` add the same injection and an
`OnAfterRenderAsync` playing `AudioKeys.ChooseCharacter`, plus stop attract music. Add inside its `@code`:
```csharp
    [Inject] public Connect4HoopsArcade.Web.Services.Abstractions.IAudioService Audio { get; set; } = default!;
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await Audio.StopMusicAsync();
            await Audio.PlayVoiceAsync(Connect4HoopsArcade.Web.Services.AudioKeys.ChooseCharacter);
        }
    }
```

- [ ] **Step 3: Build + verify audio**

```bash
dotnet build src/Connect4HoopsArcade.Web
```
Run. On splash, attract music starts (after first interaction if blocked). Tap → welcome voice + click. Mode screen plays the cue. Play a game: chip-drop SFX per move, turn-change + turn voice on switch, column-full sound, threat sound on 3-in-a-row, victory stinger + voice on win, draw sound on draw. Confirm no console errors crash the app if a file is missing (only `console.warn`).

- [ ] **Step 4: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Components/Screens/AttractMode.razor src/Connect4HoopsArcade.Web/Components/Screens/GameModeSelector.razor src/Connect4HoopsArcade.Web/Components/Screens/PlayerSetup.razor
git commit -m "feat(web): wire audio init, music, and voice cues to screens"
```

---

## Phase 7 — Sensor-Test & Settings Screens

### Task 7.1: SettingsStore (localStorage persistence)

**Files:**
- Create: `src/Connect4HoopsArcade.Web/Services/Abstractions/ISettingsStore.cs`
- Create: `src/Connect4HoopsArcade.Web/Services/SettingsStore.cs`
- Modify: `src/Connect4HoopsArcade.Web/Program.cs`
- Modify: `src/Connect4HoopsArcade.Web/Components/Layout/AppShell.razor`

- [ ] **Step 1: Create ISettingsStore**

Create `src/Connect4HoopsArcade.Web/Services/Abstractions/ISettingsStore.cs`:
```csharp
using Connect4HoopsArcade.Web.Models;

namespace Connect4HoopsArcade.Web.Services.Abstractions;

public interface ISettingsStore
{
    GameSettings Current { get; }
    event Action? Changed;
    Task LoadAsync();
    Task SaveAsync();
    Task ApplyAsync();   // push current settings into GameSession + AudioService
}
```

- [ ] **Step 2: Create SettingsStore**

Create `src/Connect4HoopsArcade.Web/Services/SettingsStore.cs`:
```csharp
using System.Text.Json;
using Microsoft.JSInterop;
using Connect4HoopsArcade.Web.Models;
using Connect4HoopsArcade.Web.Services.Abstractions;
using Connect4HoopsArcade.Web.State;

namespace Connect4HoopsArcade.Web.Services;

public sealed class SettingsStore : ISettingsStore
{
    private const string Key = "c4h.settings";
    private readonly IJSRuntime _js;
    private readonly GameSession _session;
    private readonly IAudioService _audio;

    public GameSettings Current { get; private set; } = new();
    public event Action? Changed;

    public SettingsStore(IJSRuntime js, GameSession session, IAudioService audio)
    {
        _js = js; _session = session; _audio = audio;
    }

    public async Task LoadAsync()
    {
        try
        {
            var json = await _js.InvokeAsync<string?>("ArcadeStore.get", Key);
            if (!string.IsNullOrWhiteSpace(json))
                Current = JsonSerializer.Deserialize<GameSettings>(json) ?? new();
        }
        catch { Current = new(); }
        await ApplyAsync();
        Changed?.Invoke();
    }

    public async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(Current);
            await _js.InvokeVoidAsync("ArcadeStore.set", Key, json);
        }
        catch { /* storage may be unavailable; ignore */ }
        await ApplyAsync();
        Changed?.Invoke();
    }

    public async Task ApplyAsync()
    {
        _session.Speed = Current.Speed;
        _session.SetPlayMode(Current.Mode);
        _audio.VoicesEnabled = Current.VoicesEnabled;
        await _audio.SetVolumesAsync(Current.SfxVolume, Current.NarratorVolume, Current.MusicVolume);
    }
}
```

- [ ] **Step 3: Register + load on startup**

In `src/Connect4HoopsArcade.Web/Program.cs` add before `var host = builder.Build();`:
```csharp
builder.Services.AddSingleton<Connect4HoopsArcade.Web.Services.Abstractions.ISettingsStore,
                              Connect4HoopsArcade.Web.Services.SettingsStore>();
```
And after building the host, load settings before run:
```csharp
host.Services.GetRequiredService<Connect4HoopsArcade.Web.Services.NarratorService>();
await host.Services.GetRequiredService<Connect4HoopsArcade.Web.Services.Abstractions.ISettingsStore>().LoadAsync();
await host.RunAsync();
```
(Replace the prior eager-resolve + RunAsync lines with these.)

- [ ] **Step 4: Build**

```bash
dotnet build src/Connect4HoopsArcade.Web
```
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Services/Abstractions/ISettingsStore.cs src/Connect4HoopsArcade.Web/Services/SettingsStore.cs src/Connect4HoopsArcade.Web/Program.cs
git commit -m "feat(web): settings persistence in localStorage with live apply"
```

### Task 7.2: SensorTestPanel screen

**Files:**
- Create (overwrite stub): `src/Connect4HoopsArcade.Web/Components/Screens/SensorTestPanel.razor`

- [ ] **Step 1: Implement SensorTestPanel** (source lines 314-345)

Overwrite `src/Connect4HoopsArcade.Web/Components/Screens/SensorTestPanel.razor`:
```razor
@implements IDisposable
@inject GameSession Session
@inject Connect4HoopsArcade.Web.Services.SensorConnectionService Sensor

<div style="position:absolute;inset:0;z-index:6;display:flex;flex-direction:column;padding:20px 24px;animation:slideUp .4s ease;">
  <div style="flex:none;display:flex;align-items:center;justify-content:space-between;margin-bottom:14px;">
    <button @onclick="Back" class="pill-btn">‹ Atrás</button>
    <div class="font-display" style="font-weight:700;font-size:clamp(20px,3vw,32px);display:flex;align-items:center;gap:10px;">⚡ PRUEBA DE SENSORES</div>
    <div style="display:flex;align-items:center;gap:8px;padding:8px 16px;border-radius:999px;border:1.5px solid @ConnBorder;background:rgba(0,0,0,.25);">
      <span style="width:11px;height:11px;border-radius:50%;background:@ConnDot;box-shadow:0 0 10px @ConnDot;"></span>
      <span style="font-weight:900;font-size:13px;color:@ConnDot;letter-spacing:.5px;">@ConnLabel</span>
    </div>
  </div>

  <div style="flex:1;min-height:0;border-radius:22px;border:2px solid rgba(34,211,238,.3);background:repeating-linear-gradient(0deg, rgba(34,211,238,.04) 0 1px, transparent 1px 22px), linear-gradient(180deg,#0c1430,#080a1c);box-shadow:inset 0 0 40px rgba(34,211,238,.08);padding:22px;display:flex;flex-direction:column;">
    <div style="font-size:13px;font-weight:900;letter-spacing:2px;color:rgba(34,211,238,.7);margin-bottom:6px;">COLUMNAS 1–7 · TOCA PARA SIMULAR SEÑAL</div>
    <div style="flex:1;display:flex;gap:14px;align-items:stretch;">
      @for (int i = 0; i < 7; i++)
      {
          var idx = i;
          var lit = _lit[idx];
          <button @onclick="() => Pulse(idx)"
                  style="cursor:pointer;flex:1;display:flex;flex-direction:column;align-items:center;justify-content:flex-end;gap:10px;padding:14px 6px;border-radius:16px;border:2px solid @(lit ? "#22d3ee" : "rgba(34,211,238,.25)");background:@(lit ? "rgba(34,211,238,.2)" : "rgba(255,255,255,.03)");box-shadow:@(lit ? "0 0 26px rgba(34,211,238,.8), inset 0 0 20px rgba(34,211,238,.5)" : "none");transition:all .12s;">
            <div style="flex:1;width:100%;border-radius:10px;background:@(lit ? "linear-gradient(180deg,#7df0ff,#22d3ee)" : "rgba(255,255,255,.06)");box-shadow:@(lit ? "0 0 18px rgba(34,211,238,.9)" : "none");"></div>
            <span class="font-display" style="font-weight:700;font-size:28px;color:@(lit ? "#fff" : "rgba(255,255,255,.5)");">@(idx + 1)</span>
          </button>
      }
    </div>
    <div style="display:flex;align-items:center;justify-content:space-between;margin-top:18px;gap:14px;flex-wrap:wrap;">
      <div style="font-weight:800;font-size:16px;color:rgba(255,255,255,.8);">Último sensor detectado: <span class="font-display" style="font-weight:700;font-size:24px;color:#ffd23f;">@_last</span></div>
      <div style="display:flex;gap:10px;">
        <button @onclick="ToggleConn" class="square-btn">@(Sensor.Connected ? "Simular desconexión" : "Reconectar")</button>
        <button @onclick="Clear" class="square-btn">Reiniciar</button>
      </div>
    </div>
  </div>
</div>

@code {
    private readonly bool[] _lit = new bool[7];
    private string _last = "—";

    private string ConnBorder => Sensor.Connected ? "rgba(46,232,110,.5)" : "rgba(255,59,59,.5)";
    private string ConnDot => Sensor.Connected ? "#2ee86e" : "#ff3b3b";
    private string ConnLabel => Sensor.Connected ? "CONECTADO" : "DESCONECTADO";

    protected override void OnInitialized() => Sensor.ColumnTriggered += OnColumn;
    public void Dispose() => Sensor.ColumnTriggered -= OnColumn;

    private async void OnColumn(int col)
    {
        if (col < 0 || col > 6) return;
        _lit[col] = true;
        _last = $"Columna {col + 1}";
        await InvokeAsync(StateHasChanged);
        await Task.Delay(650);
        _lit[col] = false;
        await InvokeAsync(StateHasChanged);
    }

    private void Pulse(int i) => Sensor.Pulse(i);     // raises ColumnTriggered → OnColumn lights the bar
    private void ToggleConn() => Sensor.SetConnected(!Sensor.Connected);
    private void Clear() { for (int i = 0; i < 7; i++) _lit[i] = false; _last = "—"; StateHasChanged(); }
    private void Back() => Session.CloseSensors();
}
```

- [ ] **Step 2: Add square-btn style to board.css**

Append to `src/Connect4HoopsArcade.Web/wwwroot/css/board.css`:
```css
.square-btn { cursor:pointer; padding:11px 20px; border-radius:12px; border:1.5px solid rgba(255,255,255,.2); background:rgba(255,255,255,.05); color:#fff; font-weight:800; font-size:14px; }
.square-btn:hover { background:rgba(255,255,255,.1); }
```

- [ ] **Step 3: Build + verify**

```bash
dotnet build src/Connect4HoopsArcade.Web
```
Run; from Mode screen open "⚡ Prueba de sensores". Click a column → it lights cyan ~650ms; "Último sensor" updates. Press keyboard 1–7 → the matching bar lights (keyboard feeds the sensor channel). Toggle connection → status pill flips green/red. "Reiniciar" clears.

- [ ] **Step 4: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Components/Screens/SensorTestPanel.razor src/Connect4HoopsArcade.Web/wwwroot/css/board.css
git commit -m "feat(web): sensor test/diagnostic screen"
```

### Task 7.3: SettingsPanel screen

**Files:**
- Create (overwrite stub): `src/Connect4HoopsArcade.Web/Components/Screens/SettingsPanel.razor`

- [ ] **Step 1: Implement SettingsPanel** (source lines 347-399, theme row replaced by play-mode + test buttons)

Overwrite `src/Connect4HoopsArcade.Web/Components/Screens/SettingsPanel.razor`:
```razor
@using Connect4HoopsArcade.Core.Primitives
@using Connect4HoopsArcade.Web.Models
@inject GameSession Session
@inject Connect4HoopsArcade.Web.Services.Abstractions.ISettingsStore Store
@inject Connect4HoopsArcade.Web.Services.Abstractions.IAudioService Audio

<div style="position:absolute;inset:0;z-index:6;display:flex;flex-direction:column;padding:20px 24px;animation:slideUp .4s ease;">
  <div style="flex:none;display:flex;align-items:center;justify-content:space-between;margin-bottom:16px;">
    <button @onclick="Session.CloseSettings" class="pill-btn">‹ Volver</button>
    <div class="font-display" style="font-weight:700;font-size:clamp(20px,3vw,32px);display:flex;align-items:center;gap:10px;">⚙ CONFIGURACIÓN</div>
    <div style="width:84px;"></div>
  </div>
  <div style="flex:1;min-height:0;overflow:auto;display:flex;justify-content:center;">
    <div style="width:100%;max-width:620px;display:flex;flex-direction:column;gap:14px;">

      <div class="set-row"><span class="set-ico">🎵</span><span class="set-name">Música</span>
        <input type="range" min="0" max="100" value="@S.MusicVolume" @oninput="@(e => SetVol(v => v.MusicVolume = ParseInt(e)))" style="flex:1;accent-color:#ff2d6f;height:6px;" />
        <span class="set-val">@S.MusicVolume</span></div>

      <div class="set-row"><span class="set-ico">🔊</span><span class="set-name">Efectos</span>
        <input type="range" min="0" max="100" value="@S.SfxVolume" @oninput="@(e => SetVol(v => v.SfxVolume = ParseInt(e)))" style="flex:1;accent-color:#ff2d6f;height:6px;" />
        <span class="set-val">@S.SfxVolume</span></div>

      <div class="set-row"><span class="set-ico">🎙️</span><span class="set-name">Narrador</span>
        <input type="range" min="0" max="100" value="@S.NarratorVolume" @oninput="@(e => SetVol(v => v.NarratorVolume = ParseInt(e)))" style="flex:1;accent-color:#ff2d6f;height:6px;" />
        <span class="set-val">@S.NarratorVolume</span></div>

      <div class="set-row"><span class="set-ico">🗣️</span><span style="flex:1;font-weight:800;font-size:16px;">Voces del narrador</span>
        <button @onclick="ToggleVoices" style="cursor:pointer;width:64px;height:34px;border-radius:999px;border:none;background:@(S.VoicesEnabled ? "#2ee86e" : "rgba(255,255,255,.18)");position:relative;transition:background .2s;">
          <span style="position:absolute;top:3px;left:@(S.VoicesEnabled ? "33px" : "3px");width:28px;height:28px;border-radius:50%;background:#fff;transition:left .2s;"></span>
        </button></div>

      <div class="set-row"><span class="set-ico">⚡</span><span style="flex:1;font-weight:800;font-size:16px;">Velocidad de animación</span>
        <div style="display:flex;gap:8px;">
          <button @onclick="@(() => SetSpeed(AnimationSpeed.Normal))" class="seg-btn @(S.Speed == AnimationSpeed.Normal ? "seg-btn--on" : "")">Normal</button>
          <button @onclick="@(() => SetSpeed(AnimationSpeed.Fast))" class="seg-btn @(S.Speed == AnimationSpeed.Fast ? "seg-btn--on" : "")">Rápida</button>
        </div></div>

      <div class="set-row"><span class="set-ico">🎮</span><span style="flex:1;font-weight:800;font-size:16px;">Modo de juego</span>
        <div style="display:flex;align-items:center;gap:10px;">
          <span style="width:9px;height:9px;border-radius:50%;background:@(Session.SensorConnected ? "#2ee86e" : "#ff3b3b");"></span>
          <button @onclick="@(() => SetMode(PlayMode.Digital))" class="seg-btn @(S.Mode == PlayMode.Digital ? "seg-btn--on" : "")">Digital</button>
          <button @onclick="@(() => SetMode(PlayMode.Physical))" class="seg-btn @(S.Mode == PlayMode.Physical ? "seg-btn--on" : "")">Físico</button>
        </div></div>

      <div style="display:flex;gap:12px;margin-top:8px;flex-wrap:wrap;">
        <button @onclick="TestSound" class="square-btn">🔈 Probar sonido</button>
        <button @onclick="Session.OpenSensors" class="square-btn">⚡ Prueba de sensores →</button>
      </div>
    </div>
  </div>
</div>

@code {
    private GameSettings S => Store.Current;

    private static int ParseInt(ChangeEventArgs e) => int.TryParse(e.Value?.ToString(), out var v) ? v : 0;

    private async Task SetVol(Action<GameSettings> mutate) { mutate(S); await Store.SaveAsync(); }
    private async Task ToggleVoices() { S.VoicesEnabled = !S.VoicesEnabled; await Store.SaveAsync(); }
    private async Task SetSpeed(AnimationSpeed sp) { S.Speed = sp; await Store.SaveAsync(); }
    private async Task SetMode(PlayMode m) { S.Mode = m; await Store.SaveAsync(); }
    private async Task TestSound() => await Audio.PlaySfxAsync(Connect4HoopsArcade.Web.Services.AudioKeys.ChipDrop);
}
```

- [ ] **Step 2: Add settings styles to board.css**

Append to `src/Connect4HoopsArcade.Web/wwwroot/css/board.css`:
```css
.set-row { display:flex; align-items:center; gap:16px; padding:16px 20px; border-radius:18px; background:rgba(255,255,255,.05); border:1.5px solid rgba(255,255,255,.1); }
.set-ico { font-size:26px; width:32px; text-align:center; }
.set-name { flex:none; width:140px; font-weight:800; font-size:16px; }
.set-val { flex:none; width:48px; text-align:right; font-family:'Fredoka',sans-serif; font-weight:700; font-size:20px; color:#ffd23f; }
.seg-btn { cursor:pointer; padding:9px 18px; border-radius:12px; border:2px solid rgba(255,255,255,.15); background:rgba(255,255,255,.03); color:#fff; font-weight:800; font-size:14px; }
.seg-btn--on { border-color:#ffd23f; background:rgba(255,210,63,.18); color:#ffd23f; }
```

- [ ] **Step 3: Build + verify settings**

```bash
dotnet build src/Connect4HoopsArcade.Web
```
Run; open Configuración. Move sliders → values update; "Probar sonido" plays chip-drop at the set SFX volume. Toggle voices → narrator voices stop/resume in game. Switch speed → drop animation faster in next game. Toggle Digital/Físico → in Físico, in-game column clicks are disabled (board is display-only); keyboard 1–7 still drops. Reload the page → settings persist (localStorage).

- [ ] **Step 4: Commit**

```bash
git add src/Connect4HoopsArcade.Web/Components/Screens/SettingsPanel.razor src/Connect4HoopsArcade.Web/wwwroot/css/board.css
git commit -m "feat(web): settings panel (volumes, voices, speed, play mode)"
```

---

## Phase 8 — Responsive & Final Polish

### Task 8.1: Responsive verification + transitions

**Files:**
- Modify: `src/Connect4HoopsArcade.Web/wwwroot/css/board.css` (if gaps found)

- [ ] **Step 1: Verify landscape scaling**

Run the app; resize the browser from laptop (1366×768) up to a large 16:9 window. Expected: the
`clamp()`-based board, panels, and fonts scale smoothly; the 3-column layout stays centered and never
overflows horizontally.

- [ ] **Step 2: Verify portrait-phone rotate hint**

In browser dev tools, switch to a portrait phone profile (e.g. 390×844). Expected: on the Game screen the
board is hidden and the "Gira tu dispositivo" hint (spinning 📱) shows (driven by the existing
`.rotate-hint` media query in `app.css`). Splash/Mode/Setup remain usable in portrait.

- [ ] **Step 3: Verify tablet portrait wrap**

Switch to ~820×1180. Setup cards wrap; settings list scrolls. No horizontal overflow anywhere.

- [ ] **Step 4: Fix any overflow/spacing gaps found**

If any screen overflows horizontally on a tested viewport, add a targeted rule to
`src/Connect4HoopsArcade.Web/wwwroot/css/board.css` (e.g. `.board-wrap { overflow-x:hidden; }`) rather than
changing the clamp scales. Record what was changed in the commit message.

- [ ] **Step 5: Commit (only if changes were made)**

```bash
git add src/Connect4HoopsArcade.Web/wwwroot/css/board.css
git commit -m "fix(web): responsive overflow adjustments"
```

### Task 8.2: Acceptance pass + full test suite

**Files:** none (verification only)

- [ ] **Step 1: Run the full unit-test suite**

```bash
dotnet test
```
Expected: ALL PASS (Board, WinDetector, ThreatScanner, PlayValidator, CpuStrategy, ColorCatalog).

- [ ] **Step 2: Walk the acceptance criteria manually**

Run `dotnet run --project src/Connect4HoopsArcade.Web` and confirm each:
  - Splash → Mode → Setup → Game flow works; visuals match the imported design (colors, fonts, board, tokens).
  - A full 2-player game can be played with mouse/touch to a win.
  - Keyboard 1–7 drops chips (works in both Digital and Físico).
  - Horizontal, vertical, and both diagonal wins are detected and highlighted (winning chips pulse, board flashes, banner shows).
  - Draw detected when the board fills with no winner.
  - Column-full is rejected with shake + message, same player keeps the turn.
  - Falling-chip animation plays on every drop (and in Físico mode mirroring a sensor pulse).
  - SFX + voices play from `wwwroot/audio`; a deliberately renamed/missing file only `console.warn`s (app keeps working).
  - Sensor-test screen lights columns 1–7 on click and keyboard.
  - Settings screen adjusts volumes/voices/speed/mode and persists across reload.
  - 1-player vs CPU plays; CPU wins/blocks/prefers center.

- [ ] **Step 3: Build a Release publish to confirm production build**

```bash
dotnet publish src/Connect4HoopsArcade.Web -c Release -o publish
```
Expected: publish succeeds; `publish/wwwroot` contains `audio/`, `fonts/`, `css/`, `js/`, `_framework/`.

- [ ] **Step 4: Final commit + tag**

```bash
git add -A
git commit -m "chore: acceptance pass — Connect 4 Hoops Arcade MVP complete"
git tag v0.1.0-mvp
```

---

## Self-Review Notes (for the implementer)

- **`GameSession` is not unit-tested directly** (it lives in `Web` and is timing/JS-adjacent). Its rules are
  delegated to the fully-tested `Core`. If you want belt-and-suspenders coverage, add a `Web.Tests` project
  and test `MoveRouter.OriginAllowed`/debounce and `GameSession` transitions with the idle timer stubbed —
  optional, not required for the acceptance criteria.
- **Fonts:** if Google could not be reached during Task 0.3, the `@font-face` URLs will 404 and the UI falls
  back to system fonts. Re-run the download later; the layout still works.
- **`Mode` vs `Mode2`:** `GameMode Mode` = 1P/2P; `PlayMode Mode2` = Digital/Physical. Kept separate on
  purpose. If the naming bothers you, rename `Mode2` → `Input` consistently across `GameSession`,
  `MoveRouter`, `SettingsStore`, `GameView`, and `SettingsPanel` in one pass.
- **CPU is Digital-only** by design (no actuator). In Físico mode choose 2-player; the mode selector and
  setup already support this since 1P seeds a CPU that can't be driven physically.
