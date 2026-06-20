# CPU difficulty levels + setup selector (design)

> Estado: aprobado para implementación. Roadmap item #3. Hoy el CPU está **fijo en el nivel más difícil**
> (`GameSession.CpuLevel = Sharp`, minimax profundidad 5, sin UI). Esto añade una **escalera de 6 niveles**
> elegible en la pantalla de personalización, para "ir aprendiendo sobre la marcha".

## Objetivo

Dejar que el jugador elija qué tan fuerte juega el CPU (solo 1 jugador), con una curva pareja de fácil a
difícil, y que la elección se **recuerde**. El default deja de ser el máximo.

## 1. Escalera de 6 niveles (nombres temáticos, fácil → difícil)

| Nivel | Comportamiento del CPU |
|---|---|
| **Novato** | Sin búsqueda: toma una victoria inmediata si la tiene; **bloquea solo ~50%** (al azar); el resto juega columna **aleatoria**. (= el `Chill` actual.) |
| **Principiante** | Minimax profundidad **1**: solo evalúa su propia jugada (1 ply). El más débil con búsqueda — cae en trampas y no ve secuencias forzadas. (La heurística aún penaliza amenazas, así que no es un "nunca bloquea", pero es claramente ganable.) |
| **Amateur** | Minimax profundidad **2**: ya **bloquea** amenazas inmediatas; ganable con jugadas a 2. |
| **Titular** | Minimax profundidad **3**. |
| **Estrella** | Minimax profundidad **4** (= el `Normal` actual). |
| **MVP** | Minimax profundidad **5** (= el `Sharp` actual, lo que se sufre hoy). |

**Todos** toman una victoria inmediata si está disponible (nunca se siente roto). La curva de aprendizaje
sale natural: a menor profundidad, "menos ve" (los niveles bajos no bloquean / caen en trampas).

## 2. Arquitectura (respeta guardrails: `Core` puro, JS aislado, `GameSession` SSOT)

### Core
- `Core/Primitives/CpuDifficulty.cs`: el enum pasa de `{ Chill, Normal, Sharp }` a
  **`{ Novato, Principiante, Amateur, Titular, Estrella, MVP }`** (orden fácil→difícil; el ordinal coincide
  con la profundidad de búsqueda salvo Novato=0).
- `Core/Ai/CpuStrategy.cs`: profundidad por nivel vía un helper explícito:
  ```csharp
  private static int DepthFor(CpuDifficulty d) => d switch {
      CpuDifficulty.Novato => 0,
      CpuDifficulty.Principiante => 1,
      CpuDifficulty.Amateur => 2,
      CpuDifficulty.Titular => 3,
      CpuDifficulty.Estrella => 4,
      _ => 5, // MVP
  };
  ```
  `ChooseColumn`: siempre toma victoria inmediata; si `DepthFor == 0` → rama suelta actual (bloqueo 50% +
  aleatorio); si no → minimax a esa profundidad (la lógica de minimax/evaluación no cambia, solo deja de
  estar hard-codeada a 4/5).
- **TDD** (`CpuStrategyTests`, actualizar al nuevo enum + agregar curva):
  - MVP/Estrella bloquean una amenaza inmediata y toman una victoria inmediata.
  - **Amateur (prof. 2) bloquea** una amenaza inmediata (la búsqueda ve la victoria rival).
  - Novato no rompe (devuelve columna válida; toma victoria inmediata).
  - (No se testea "Principiante no bloquea": la heurística lo hace poco confiable; su debilidad — 1 ply, cae en trampas — se valida jugando.)
  - Tablero lleno → -1.

### Web
- `Models/GameSettings.cs`: `public CpuDifficulty CpuLevel { get; set; } = CpuDifficulty.Amateur;` (default medio).
- `Services/SettingsStore.cs` (`ApplyAsync`): `_session.CpuLevel = Current.CpuLevel;` (empuja como `Speed`/`NarratorTone`).
- `State/GameSession.cs`: `CpuLevel` default pasa de `Sharp` a `Amateur` (lo sobreescribe `ApplyAsync` al boot).
- **Catálogo de etiquetas** `Services/CpuLevelLabels.cs` (estático, español): `Name(CpuDifficulty)` →
  "Novato"/"Principiante"/… y `Hint(CpuDifficulty)` → una línea corta opcional (p. ej. "no bloquea",
  "bloquea lo obvio", "implacable").
- **Componente compartido** `Components/Setup/CpuLevelSelector.razor`: un **stepper** `◀ Nombre ▶`
  (deshabilita ◀ en Novato y ▶ en MVP). Params: `CpuDifficulty Level`, `EventCallback<CpuDifficulty> OnChange`.
  Compacto; cabe en la línea del nombre (desktop) y en el recuadro (mobile). Reusado por ambas vistas.

### Ubicación del selector (solo 1 jugador)
- **Desktop** (`PlayerSetupCard`): cuando `Player.IsCpu`, en la **posición del nombre** (que está deshabilitado
  para el CPU) se renderiza `<CpuLevelSelector>` en vez del input. `PlayerSetupCard` recibe params opcionales
  `CpuLevel` + `OnCpuLevelChange`; `DesktopPlayerSetup` los pasa para la tarjeta del CPU (índice 1 en 1P).
- **Mobile** (`MobilePlayerSetup`): un **recuadro amarillo arriba del botón JUGAR** con el `<CpuLevelSelector>`,
  visible solo en 1P.
- Ambas pantallas inyectan `ISettingsStore`; el `OnChange` hace `S.CpuLevel = nivel; await Store.SaveAsync();`
  (persistente → `ApplyAsync` lo manda a `GameSession.CpuLevel`).

## 3. Persistencia y default
- Primera vez: **Amateur** (medio). Después se **recuerda** el último nivel elegido (en `SettingsStore`,
  igual que `NarratorTone`/velocidad).
- Es un setting persistente; el selector del setup lo lee/escribe.

## 4. Qué NO cambia
- La lógica de minimax/evaluación de `CpuStrategy` (solo se parametriza la profundidad).
- 2 jugadores (no hay CPU → no hay selector).
- El resto de la vista desktop/mobile.

## 5. Criterios de aceptación
1. En 1P, el setup muestra el selector de nivel (desktop en la tarjeta del CPU, a la altura del nombre;
   mobile en recuadro amarillo arriba de JUGAR). En 2P no aparece.
2. Se pueden elegir los 6 niveles con nombres temáticos (Novato→MVP).
3. El nivel elegido es el que usa el CPU esa partida.
4. El nivel **persiste** entre partidas/sesiones; primera vez = Amateur.
5. Los niveles difieren de verdad: Novato es ganable/suelto; Principiante es el más débil con búsqueda;
   **Amateur bloquea amenazas inmediatas** (prof≥2); MVP = el más fuerte (prof. 5).
6. Desktop sin regresión; el setup mobile sigue con JUGAR fijo/visible y sin overflow.
7. `CpuStrategyTests` cubre la curva; todos los tests verdes.
8. 2 jugadores no se ve afectado.

## 6. Verificación
- **Core (TDD):** `CpuStrategyTests` reescrito al nuevo enum + casos de curva (sección 2).
- **Manual (navegador):** 1P desktop y mobile — selector visible donde toca; elegir un nivel y jugar;
  confirmar que persiste tras recargar; Novato es ganable; MVP no perdona; 2P sin selector.

## 7. Fuera de alcance
- Tuning fino de la evaluación/heurística del minimax (solo se expone la profundidad existente).
- Selector en la pantalla de Ajustes (se decidió ponerlo en el setup, junto al personaje).
