# Mobile-first responsive redesign (design)

> Estado: aprobado para implementación. Roadmap item #1. Enfoque: **componentes dedicados de celular +
> `ViewportService`**, sin duplicar lógica de juego y sin degradar la vista de escritorio.

## Objetivo

Hacer el juego **totalmente jugable y bien visto en teléfonos** (vertical primario), sin el muro de "gira tu
dispositivo". El escritorio se mantiene **igual o mejor**. Es la base del item #2 (proyección a TV / companion).

Problemas actuales (verificados): layout de juego horizontal de 3 columnas `[panel][tablero][panel]` que no
cabe en vertical; `100vh` que en Safari móvil corta arriba/abajo; celdas atadas a `vh` (se deforman / no
caben); `.rotate-hint` que **fuerza** girar el cel; y el botón **¡JUGAR!** del setup que se corta.

## Arquitectura (enfoque 1)

Guardrails del vault intactos: `Core` puro; todo `IJSRuntime` aislado en `Services/`; `GameSession` única
fuente de verdad; componentes finos; **un solo pipeline de input** (`IMoveSource → MoveRouter → TryDrop`);
componentes de juego heredan `SessionComponentBase`.

### 1. `ViewportService` (interop JS aislado, pequeño)
- `Services/Abstractions/IViewportService.cs` + `Services/ViewportService.cs`; helpers JS en
  `wwwroot/js/arcade.js` (`window.ArcadeViewport`).
- Expone: `int Width`, `int Height`, `bool IsMobile`, `bool IsTablet`, `bool IsLandscape`, `bool IsPortrait`,
  `Breakpoint Breakpoint` (`enum Breakpoint { Mobile, Tablet, Desktop }`), y `event Action OnViewportChanged`.
- Implementación JS: `matchMedia` + `resize` con **debounce ~150 ms**. El evento .NET se dispara **solo cuando
  cambia el breakpoint o la orientación**, no en cada pixel.
- Reglas (anchos en px): `Mobile < 768`, `Tablet 768–1199`, `Desktop ≥ 1200`. **`IsMobile`** (decisión de
  layout) = `matchMedia("(max-width: 767px)")` **o** `matchMedia("(orientation: landscape) and (max-height: 480px)")`
  — así un celular en horizontal también es "móvil" y no cae en tablet. `IsTablet` = breakpoint Tablet y no móvil.
- `IAsyncDisposable`: quita los listeners JS (vía un `DotNetObjectReference` liberado y `ArcadeViewport.dispose`).
- Registrado como singleton en `Program.cs`; se inicializa al boot (lee tamaño inicial).

### 2. `AppShell` decide la vista (único punto de branch)
- Inyecta `IViewportService`, se suscribe a `OnViewportChanged`, re-renderiza al cruzar breakpoint/orientación
  (`InvokeAsync(StateHasChanged)`), y se desuscribe en `Dispose`.
- `Game` (y overlays `Victory`/`Draw`): `IsMobile ? <MobileGameView/> : <DesktopGameView/>`.
- `Setup`: `IsMobile ? <MobilePlayerSetup/> : <DesktopPlayerSetup/>`.
- `Splash`/`Mode`/`Settings`/`Sensors`: sin cambio de componente; se resuelven con **CSS responsivo**.

### 3. Renombrados (preservar lo existente)
- `Components/Screens/GameView.razor` → **`DesktopGameView.razor`** (contenido intacto).
- `Components/Screens/PlayerSetup.razor` → **`DesktopPlayerSetup.razor`** (contenido intacto).
- Actualizar referencias en `AppShell`.

### 4. `MobileGameView.razor` (nuevo; layout A híbrido) — solo presentación
- Hereda `SessionComponentBase` (re-render en cada `StateChanged`).
- **Estructura:** barra-marcador arriba · tablero centrado · burbuja de narrador abajo.
- **Barra-marcador** (`Components/Game/Mobile/MobileScoreboard.razor`): `[avatar P0 · nombre · pts] · [indicador
  de turno] · [pts · nombre · avatar P1]`, **sin botón ⚙**. Toda la barra es tappable y abre el sheet.
- **Sheet de acciones** (`Components/Game/Mobile/ActionSheet.razor`): bottom-sheet con **Reiniciar / Rendirse /
  Ajustes**; toca el scrim (fuera) para cerrar. Llama a `Session.ResetBoard` / `Session.Resign` /
  `Session.OpenSettings`. Estado abierto/cerrado local del componente.
- **Tablero:** **reusa `BoardGrid`** (que renderiza `GameColumn`/`GameCell`/`AvatarSvg`). El toque de columna
  va por **`MoveRouter.Route(col, MoveOrigin.Click)`** (misma tubería que flechas/teclado/sensor) — sin lógica
  de drop nueva. Se muestra una **ficha-fantasma** (vista previa translúcida del color activo) sobre la columna
  enfocada. Las flechas (`ColumnArrows`) **no** se usan en móvil.
- **Narrador:** reusa `NarratorBubble`, compacto y posicionado para **no tapar la jugada** (criterio 8).
- **Sin reglas de juego en el markup.** Toda la lógica vive en `GameSession`/`Core`/`MoveRouter`/`NarratorService`.

### 5. `MobilePlayerSetup.razor` (nuevo; pestañas) — reusa los pickers
- `DesktopPlayerSetup` = el actual renombrado.
- Móvil: **pestañas J1/J2** (en 1P solo J1; la ficha del CPU es automática). Cada pestaña reusa
  **`PlayerSetupCard`** (y por dentro `ColorPicker`/`FacePicker`/`AccessoryPicker`). Una tarjeta a la vez,
  cabe sin scroll.
- **¡JUGAR!** fijo abajo dentro del área segura (`env(safe-area-inset-bottom)`), **siempre visible**. Reusa la
  validación de color y `Session.BeginGame` existentes.

### 6. Dimensionado del tablero (CSS, sin JS)
- Celdas con **`aspect-ratio: 1`** (perfectamente redondas). El tablero se ajusta a los **píxeles disponibles**:
  ancho = `min(100%, (alto-disponible) * 7/6)` aprox., usando `100dvh`/`svh` para el alto real y
  `env(safe-area-inset-*)` para los bordes. Nunca overflow horizontal ni corte vertical.
- Sustituir `100vh`→`100dvh` (con fallback) en `.arc-root`/pantallas.

### 7. Pantallas simples + modales (solo CSS)
- `AttractMode`, `GameModeSelector`, `SettingsPanel`, `SensorTestPanel`, `VictoryModal`, `DrawModal`:
  `100vh`→`100dvh`, `env(safe-area-inset-*)`, `max-width`, `overflow:auto` donde aplique, tamaños touch
  (objetivos ≥ ~44px). Que **quepan y sean usables con touch**.
- **Eliminar** el `.rotate-hint` y su `@media (orientation: portrait)` que oculta el tablero y fuerza girar
  (en `app.css` y `GameView`/`DesktopGameView`).

## Qué NO cambia
- `Core`, `GameSession`, `MoveRouter`, `NarratorService`, `AudioService`, `SettingsStore`: sin cambios de
  lógica (el rediseño es de UI). Los 51 tests de Core siguen verdes.
- La vista de escritorio (`DesktopGameView`/`DesktopPlayerSetup`) queda **idéntica**.
- El teclado 1–7 (KeyboardInputService) sigue igual (aplica en desktop).

## Criterios de aceptación (checklist verificable)
1. Desktop se ve igual o mejor que antes.
2. En celular vertical no hay overflow horizontal.
3. El tablero completo 7×6 cabe en pantalla.
4. Las celdas mantienen proporción cuadrada (círculos redondos).
5. Se puede jugar tocando columnas.
6. Teclado 1–7 sigue funcionando en desktop.
7. La selección de jugadores es usable en celular (pestañas + JUGAR visible).
8. La burbuja del narrador no tapa la jugada.
9. Los modales de victoria/empate caben en móvil.
10. Settings es usable con touch.
11. Al rotar el dispositivo, la vista se ajusta sin romper el estado de la partida.
12. No se duplica lógica de juego entre `MobileGameView` y `DesktopGameView`.

## Verificación
- **Build** (`dotnet build`) + **Core tests** (`dotnet test`, 51 verdes — UI no toca Core).
- **Manual en navegador**, viewport iPhone (~393×852) y escritorio: recorrer los 12 criterios. Rotar
  portrait↔landscape a mitad de partida y confirmar que el estado se conserva (criterio 11).

## Fuera de alcance
- Proyección a TV / companion (item #2) — este rediseño es su prerequisito.
- Cambios de lógica de juego, audio, dificultad o sensores.
- Rediseño visual de la identidad (paleta/fuentes/avatares) — se conserva.
