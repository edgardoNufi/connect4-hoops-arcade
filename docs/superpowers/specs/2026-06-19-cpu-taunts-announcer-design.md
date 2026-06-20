# Locutor que pica — burlas del CPU conscientes de la racha + idle universal (diseño)

> Estado: aprobado para escribir plan (con ajustes v2). Idioma de las frases: español (LATAM).
> Tono: locutor arcade **picante y retador pero familiar** — sin groserías y **sin** frases que humillen
> de más (nada de "ya estás muerto", "ríndete", "estás para perder", etc.).

## Objetivo

Hacer el juego más retador y enganchador en 1 jugador dándole **filo familiar** al locutor: que pique al
jugador cuando pierde contra la máquina, y que la burla **escale con la racha de victorias del CPU**.
Además, que el locutor **apure al jugador que se tarda** en cualquier modo (1P y 2P).

La "persona" que habla es el **mismo locutor** que ya celebra hoy (no un personaje nuevo en 1ª persona),
pero ahora con actitud. Toda la lógica respeta los guardrails del vault:

- `Core` queda **puro** (solo BCL); la lógica de racha/nivel se prueba con TDD ahí.
- `GameSession` (Web/State) sigue siendo la única fuente de verdad del estado; posee la racha junto al marcador.
- `NarratorService` sigue siendo el **único dueño de la cola serial de voces** y solo mapea eventos→audios.
  No se crea un segundo servicio que reaccione a los mismos eventos (evita voces duplicadas / prioridad rota).
- En **2 jugadores** se mantiene la narración neutral actual (salvo el idle genérico, ver abajo).

## 1. Estado nuevo en `GameSession`

Parte natural del estado de sesión, junto a `Scores`:

| Miembro | Tipo | Regla |
|---|---|---|
| `CpuWinStreak` | `int` | Victorias consecutivas del CPU en 1P. `+1` cuando gana el CPU; `0` cuando gana el humano; sin cambio en empate. |
| `CpuStreakJustBroken` | `bool` | `true` solo cuando el humano gana y la racha previa era **≥ `CpuTauntPolicy.BreakThreshold` (2)**. Lo lee el narrador para `streak-break`. |
| `PlayerLossesAgainstCpu` | `int` | Derrotas acumuladas del humano vs CPU. `+1` cuando gana el CPU. Señal disponible para una línea más empática; no es el driver principal. |

Solo se actualizan en modo **1 jugador**. En 2 jugadores quedan inertes.

**Reset de racha (ajuste 4):** `BeginGame` reinicia `CpuWinStreak` **y** `PlayerLossesAgainstCpu` a 0
(sesión nueva). `Rematch` y `ResetBoard` los **conservan** (igual que `Scores`). Si en el futuro se necesita
borrar la racha sin empezar partida, debe ser una **acción explícita de "nueva sesión"**, nunca un efecto
colateral de revancha/reinicio.

### 1.1 Ajuste de usuario — `NarratorTone`

`enum NarratorTone { Familiar, Picante, Silencioso }`, **default `Familiar`**. Vive en `GameSession` (como
`Speed`/`CpuLevel`), se expone en `SettingsPanel` (control segmentado, estilo `seg-btn`). Es una
**preferencia persistente** (ajuste 2): se guarda/lee del `SettingsStore` y **NO** se reinicia con
`BeginGame`/`Rematch`/`ResetBoard` (a diferencia de la racha, que sí se resetea en `BeginGame`). Efecto en
`NarratorService`:

- **Silencioso:** el locutor no reproduce **ninguna** voz (ni burlas ni narración neutral). Los SFX
  (chip-drop, turn-change, column-full, win cheer, button-click) siguen sonando.
- **Familiar (default):** burlas activas pero con **techo** — el nivel efectivo se limita a `ConfidentCpu`
  (las líneas `boss` no suenan). Tono suave/alentador.
- **Picante:** escalada completa, incluye `BossMode`.

El recorte por tono lo aplica `NarratorService` (presentación); `Core`/`CpuTauntPolicy` siguen calculando el
nivel "crudo" por racha. **Todas** las frases grabadas son aptas para público familiar en cualquier tono;
la diferencia Familiar/Picante es de intensidad/ceiling, no de "limpias vs groseras".

## 2. Eventos nuevos / cambios en `GameSession`

- `event Action IdleNudged` — se dispara dentro de `ArmIdle`, en el momento en que pone `IsIdle = true`.
  Ya arma en ambos modos (solo se inhibe en turno del CPU), así que cubre 1P y 2P.
- `event Action<int> ThreatRaised` — **cambia de `Action` a `Action<int>`**. Carga `ThreatOwnerIndex`,
  el índice del jugador **que acaba de mover** (ajuste 8). Se captura en una variable explícita
  `moverIndex` **antes** de cambiar `Current`, y se usa esa variable tanto para el escaneo de amenaza
  como para el evento. **No** derivar de `Current` después del flip (puede haber cambiado).
- `event Action<int?, GameMode> MatchEnded` — evento único de fin de partida: `winner` (`null` = empate)
  + `mode`. **La racha se actualiza ANTES de dispararlo** para que el narrador lea valores frescos.
  El `NarratorService` **migra su audio de cierre** a `MatchEnded`. Los eventos `Won`/`Drew` **se conservan
  por ahora** (ajuste 5): antes de eliminarlos, el plan debe **auditar** que no los consuman UI,
  animaciones, modales (`VictoryModal`/`DrawModal`) ni pruebas. Si algo los usa, se quedan; si nada los usa,
  se eliminan en un paso aparte y verificable.
- `event Action RoundStarted` — se dispara al final de `ResetState` (lo llaman `BeginGame`/`Rematch`/
  `ResetBoard`). Reinicia el estado anti-spam del narrador por partida. Independiente de `GameStarted`
  (que sigue disparando solo el "¡prepárate!" en `BeginGame`).

### Actualización de racha al terminar partida (en 1P)

En las rutas de fin de partida (`Place` con conexión, `Resign`, empate), modo 1P:
1. Determinar `MatchOutcome`: `CpuWin` si `Players[winner].IsCpu`; `HumanWin` si gana el humano; `Draw`.
2. `var s = CpuTauntPolicy.Advance(CpuWinStreak, PlayerLossesAgainstCpu, outcome);`
3. Asignar `CpuWinStreak = s.Streak; CpuStreakJustBroken = s.JustBroken; PlayerLossesAgainstCpu = s.PlayerLosses;`
4. Disparar `MatchEnded(winner, Mode)`.

En 2P se omite el paso de racha; solo se dispara `MatchEnded(winner, Mode)`.

## 3. Lógica pura en `Core` — `CpuTauntPolicy` (TDD)

Sin nada de audio ni Blazor. Es lo único que `NarratorService` consume para decidir el nivel.

```csharp
namespace Connect4HoopsArcade.Core.Ai;   // junto a CpuStrategy

public enum CpuTauntLevel { Neutral, LightChallenge, ConfidentCpu, BossMode }
public enum MatchOutcome  { CpuWin, HumanWin, Draw }   // perspectiva del CPU en 1 jugador
public readonly record struct StreakState(int Streak, bool JustBroken, int PlayerLosses);

public static class CpuTauntPolicy
{
    public const int BreakThreshold = 2;

    // 0 → Neutral, 1 → LightChallenge, 2 → ConfidentCpu, ≥3 → BossMode
    public static CpuTauntLevel LevelFor(int cpuWinStreak);

    // Transición pura al terminar una partida.
    //  CpuWin   → Streak+1, PlayerLosses+1, JustBroken=false
    //  HumanWin → Streak=0,  PlayerLosses sin cambio, JustBroken = prevStreak >= BreakThreshold
    //  Draw     → todo sin cambio, JustBroken=false
    public static StreakState Advance(int prevStreak, int prevLosses, MatchOutcome outcome);
}
```

El recorte por `NarratorTone` (Familiar → techo en `ConfidentCpu`) es de presentación y vive en
`NarratorService`/`CpuTauntLines`, **no** en `Core`. `Core` siempre calcula el nivel crudo.

**Tests (`Core.Tests/CpuTauntPolicyTests.cs`, falla primero):**
- `LevelFor`: 0→Neutral, 1→LightChallenge, 2→ConfidentCpu, 3→BossMode, 10→BossMode.
- `Advance(CpuWin)`: incrementa `Streak` y `PlayerLosses`, `JustBroken=false`.
- `Advance(HumanWin)` con prev 0/1 → `Streak=0`, `JustBroken=false`; con prev 2/3 → `Streak=0`, `JustBroken=true`; `PlayerLosses` sin cambio.
- `Advance(Draw)`: idéntico a la entrada, `JustBroken=false`.

## 4. `NarratorService` — mapeo y anti-empalme

Para que **no crezca como clase gigante**, delega en piezas chicas y conserva handlers de pocas líneas:

- Matemática de racha/nivel → `CpuTauntPolicy` (Core).
- Mapeo `(categoría, nivel) → string[]` de archivos → **tabla de datos estática** `CpuTauntLines`
  (Web/Services). No es un servicio, no se suscribe a eventos.
- Selección de variante **sin repetir la anterior** → helper chico `VoicePicker.Pick(count, lastIndex, roll)`
  (puro, fácil de testear). El narrador guarda el último índice por categoría.

### Estado interno del narrador (anti-spam)
- `_lastTauntAt` (timestamp) — momento de la última burla mid-game; base del cooldown.
- `_idleTauntUsedThisRound` (bool) — el idle del CPU suena **máx. 1 vez por partida**; reset en `RoundStarted`.
- `_lastIndexByCategory` — para no repetir variante seguida.

### Reglas de prioridad
1. **Solo 1P** usa las burlas del CPU. 2P mantiene narración neutral (almost-win neutral, victoria/empate neutrales).
2. **Tono (§1.1):** `Silencioso` → ninguna voz. `Familiar` → nivel efectivo recortado a `ConfidentCpu`.
   `Picante` → escalada completa.
3. **Cooldown mid-game: 25 s** (rango aceptable 20–30 s) entre burlas de mid-game (ajuste 6).
   El cooldown de `column-full` es **independiente** y puede seguir corto (~800 ms).
4. **No repetir** la misma frase dos veces seguidas (por categoría).
5. **Prioridad `cpu-threat` > `cpu-idle`** (ajuste 7). La amenaza es un evento de juego importante; el idle es relleno.
   - `cpu-threat`: puede sonar cada vez que el CPU arma un tres-en-línea, sujeto al cooldown de 25 s.
     **No** tiene tope por partida y **nunca** queda bloqueada por un idle previo. Pero **sí** se inhibe si
     (ajuste 3) la partida ya terminó (`Winner != null`) o hay una voz de **cierre** en cola
     (cpu-win / victoria / empate / streak-break / beat-cpu): nunca debe pisar el desenlace.
   - `cpu-idle`: suena **máx. 1 vez por partida** y se **suprime** si hubo una `cpu-threat` reciente
     (dentro del cooldown). El idle **no consume** la posibilidad de que suene una amenaza después.
6. Las líneas de **cierre** (CPU gana, romper racha, ganarle al CPU) están exentas de cooldown/tope
   (son una por partida por naturaleza).
7. El idle **genérico** (2P y cualquier contexto no-CPU) usa cooldown + no-repetir; queda espaciado por el
   timer de idle (9 s).
8. La cola serial de voces (ya existente) garantiza que nunca se solapen.

### Tabla evento → intención

| Evento | 1 jugador | 2 jugadores |
|---|---|---|
| `ThreatRaised(moverIndex)` | mover = CPU → `cpu-threat` nivel `LevelFor(CpuWinStreak)` (prioridad alta) · mover = humano → almost-win neutral | almost-win neutral |
| `IdleNudged` | `cpu-idle` nivel `LevelFor(CpuWinStreak)` (máx 1/partida, cede ante amenaza) | `idle` genérico (cooldown + no-repetir) |
| `MatchEnded` · CPU gana | `cpu-win` nivel `LevelFor(CpuWinStreak)` *post-incremento* (Light/Confident/Boss). **Sin** win cheer; opcional `loss-sting` corto | n/a |
| `MatchEnded` · humano gana | `CpuStreakJustBroken` → `streak-break` (+ cheer) · si no → `beat-cpu` (+ cheer) | `VictoryV` neutral + cheer (como hoy) |
| `MatchEnded` · empate | empate neutral (como hoy) | empate neutral (como hoy) |

> Nota: en 1P, al ganar el CPU **no** suena el "win cheer" (`WinSfx`); es una derrota del jugador (ajuste 9).
> Suena la voz `cpu-win` y, **opcionalmente**, un `loss-sting` corto (SFX breve, ver tabla). El cheer se
> conserva cuando gana el humano (incluido `streak-break`/`beat-cpu`).

## 5. Catálogo de audios (guía de grabación/búsqueda)

Carpeta `wwwroot/audio/voice/` (el `loss-sting` va en `wwwroot/audio/game/`). Convención: kebab, slug de
nivel (`neutral|light|confident|boss`), numerado, `.mp3` (`.m4a` también sirve). Los arreglos en `AudioKeys`
crecen agregando archivos. La **tabla 5.1 es la lista autoritativa**: ahí está cada archivo, cuándo suena y
la frase. Tono familiar; los nombres son exactamente los que el código va a buscar.

### 5.1 Tabla de producción (checklist de grabación)

Marca el progreso en la columna ☐. Las `NN` están enumeradas según las 3 variantes del menú; **puedes grabar
menos** (mínimo `-01` por celda para arrancar) o más (solo agrega `-04`, `-05`… y se suman al arreglo).

**Mid-game · `cpu-threat-*` — suena cuando el CPU acaba de armar un 3-en-línea (solo 1P).**

| ☐ | Archivo | Nivel (racha) | Frase |
|---|---|---|---|
| ☐ | `cpu-threat-neutral-01.mp3` | Neutral (0) | Cuidado, la máquina va por ti. |
| ☐ | `cpu-threat-neutral-02.mp3` | Neutral (0) | Ojo, te está armando algo. |
| ☐ | `cpu-threat-neutral-03.mp3` | Neutral (0) | Mira esa jugada de la máquina. |
| ☐ | `cpu-threat-light-01.mp3` | Light (1) | Otra vez te tiene en la mira. |
| ☐ | `cpu-threat-light-02.mp3` | Light (1) | La máquina no se distrae, ¿eh? |
| ☐ | `cpu-threat-light-03.mp3` | Light (1) | Ahí va de nuevo. |
| ☐ | `cpu-threat-confident-01.mp3` | Confident (2) | ¡Tres en línea otra vez! Despierta. |
| ☐ | `cpu-threat-confident-02.mp3` | Confident (2) | La máquina trae puntería hoy. |
| ☐ | `cpu-threat-confident-03.mp3` | Confident (2) | Esa la vio venir solita. |
| ☐ | `cpu-threat-boss-01.mp3` | Boss (3+) | ¡Uy, la máquina está imparable! |
| ☐ | `cpu-threat-boss-02.mp3` | Boss (3+) | Va a estar difícil pararla. |
| ☐ | `cpu-threat-boss-03.mp3` | Boss (3+) | La máquina anda inspirada. |

**Mid-game · `cpu-idle-*` — suena cuando el jugador se tarda en su turno (solo 1P).**

| ☐ | Archivo | Nivel (racha) | Frase |
|---|---|---|---|
| ☐ | `cpu-idle-neutral-01.mp3` | Neutral (0) | ¿Y esa duda? La máquina ya decidió. |
| ☐ | `cpu-idle-neutral-02.mp3` | Neutral (0) | Tic-tac, es tu turno. |
| ☐ | `cpu-idle-neutral-03.mp3` | Neutral (0) | La máquina espera… paciente. |
| ☐ | `cpu-idle-light-01.mp3` | Light (1) | Piénsalo bien, la vas remando. |
| ☐ | `cpu-idle-light-02.mp3` | Light (1) | ¿Buscando jugada? Tómate tu tiempo… poquito. |
| ☐ | `cpu-idle-light-03.mp3` | Light (1) | La máquina ya sabe qué sigue. |
| ☐ | `cpu-idle-confident-01.mp3` | Confident (2) | Mientras piensas, la máquina sonríe. |
| ☐ | `cpu-idle-confident-02.mp3` | Confident (2) | Despacito… que la máquina no tiene prisa. |
| ☐ | `cpu-idle-confident-03.mp3` | Confident (2) | ¿Nervios? La máquina ni parpadea. |
| ☐ | `cpu-idle-boss-01.mp3` | Boss (3+) | La máquina anda en racha, ¡muévete con cuidado! |
| ☐ | `cpu-idle-boss-02.mp3` | Boss (3+) | Hoy la máquina trae suerte… o algo más. |
| ☐ | `cpu-idle-boss-03.mp3` | Boss (3+) | A ver con qué le respondes. |

**Cierre · `cpu-win-*` — suena cuando gana el CPU (solo 1P; sin "win cheer"). Sin nivel Neutral.**

| ☐ | Archivo | Nivel (racha post-victoria) | Frase |
|---|---|---|---|
| ☐ | `cpu-win-light-01.mp3` | Light (1ª victoria) | ¡Punto para la máquina! |
| ☐ | `cpu-win-light-02.mp3` | Light (1ª victoria) | La máquina se adelanta. |
| ☐ | `cpu-win-light-03.mp3` | Light (1ª victoria) | Esa fue de la máquina. |
| ☐ | `cpu-win-confident-01.mp3` | Confident (2 seguidas) | ¡Van dos para la máquina! ¿Le das la vuelta? |
| ☐ | `cpu-win-confident-02.mp3` | Confident (2 seguidas) | Dos seguidas… ¡tú puedes con la próxima! |
| ☐ | `cpu-win-confident-03.mp3` | Confident (2 seguidas) | La máquina toma ventaja. |
| ☐ | `cpu-win-boss-01.mp3` | Boss (3+ seguidas) | ¡La máquina trae rachota! ¿Quién la para? |
| ☐ | `cpu-win-boss-02.mp3` | Boss (3+ seguidas) | ¡Está encendida! A ver si la frenas. |
| ☐ | `cpu-win-boss-03.mp3` | Boss (3+ seguidas) | Otra para la máquina… ¡la revancha es tuya! |

**Cierre · humano gana (solo 1P; con "win cheer").**

| ☐ | Archivo | Cuándo | Frase |
|---|---|---|---|
| ☐ | `streak-break-01.mp3` | Rompes racha ≥2 | ¡Eso! Le rompiste la racha a la máquina. |
| ☐ | `streak-break-02.mp3` | Rompes racha ≥2 | ¡Ahí está! Le quitaste el invicto. |
| ☐ | `streak-break-03.mp3` | Rompes racha ≥2 | ¡Frenaste a la máquina! Bien jugado. |
| ☐ | `beat-cpu-01.mp3` | Le ganas, racha <2 | ¡Le ganaste a la máquina! ¿Otra? |
| ☐ | `beat-cpu-02.mp3` | Le ganas, racha <2 | ¡Bien hecho! La máquina quiere revancha. |
| ☐ | `beat-cpu-03.mp3` | Le ganas, racha <2 | ¡Punto para ti! Así se juega. |

**Idle genérico · `idle-*` — suena cuando alguien se tarda, en CUALQUIER modo (1P y 2P).**

| ☐ | Archivo | Frase |
|---|---|---|
| ☐ | `idle-01.mp3` | ¿Sigues ahí? El tablero espera. |
| ☐ | `idle-02.mp3` | El tiempo corre… ¡tú puedes! |
| ☐ | `idle-03.mp3` | ¿Te dormiste? Es tu turno. |
| ☐ | `idle-04.mp3` | Menos pensar, más jugar. |
| ☐ | `idle-05.mp3` | Vamos, que se enfría el juego. |

**SFX opcional (no es voz) — `wwwroot/audio/game/`.**

| ☐ | Archivo | Cuándo | Nota |
|---|---|---|---|
| ☐ | `loss-sting.mp3` | 1P, gana el CPU | SFX corto/desinflado (≈0.5–1 s). **Opcional**; reemplaza al win cheer en la derrota. |

**Conteo (ajuste 3):** **44 archivos** de voz con las 3 variantes del menú · **14 archivos** si grabas solo
`-01` por celda (8 mid-game + 3 cpu-win + 1 streak-break + 1 beat-cpu + 1 idle genérico). El `loss-sting`
es opcional y aparte. Formato: `.mp3` o `.m4a`, voz del mismo locutor, mono, normalizado al nivel de las
voces actuales en `wwwroot/audio/voice/`.

### Claves nuevas en `AudioKeys`
Arreglos por celda/categoría: `CpuThreat[Neutral|Light|Confident|Boss]`, `CpuIdle[…]`,
`CpuWin[Light|Confident|Boss]`, `StreakBreak`, `BeatCpu`, `IdleNudge`, y `LossSting` (SFX único, opcional).
(Nombres finales a definir en el plan; el mapeo nivel→arreglo vive en la tabla `CpuTauntLines`.)

## 6. Pruebas y verificación

**Orden de implementación (ajuste 5):** primero los tests de `Core` —`CpuTauntPolicyTests` y
`VoicePickerTests`— (escribir, ver fallar, implementar), y **después** cablear `GameSession` y
`NarratorService`.

- **Core (TDD):** `CpuTauntPolicyTests` cubre la sección 3 (escribir primero, ver fallar, implementar).
- **`VoicePicker`:** test puro de no-repetición (último índice nunca se repite cuando `count > 1`).
- **`GameSession`/`NarratorService`:** no hay proyecto de pruebas de Web hoy. La transición pura ya queda
  cubierta por `CpuTauntPolicy`; el cableado (asignar contadores, disparar eventos, mapear, recorte por tono)
  se verifica build-and-verify en navegador. Un proyecto de pruebas de Web queda fuera de alcance salvo petición.
- **Manual:** 1P — perder 1/2/3 seguidas y oír la escalada (light→confident→boss en Picante; con techo en
  Familiar); romper racha y oír `streak-break`; confirmar prioridad `cpu-threat` sobre `cpu-idle`, cooldown
  de 25 s e idle ≤1/partida; idle genérico en 2P; tono `Silencioso` calla las voces pero deja SFX; y que 2P
  conserva narración neutral en lo demás.

## 7. Fuera de alcance (por ahora)

- Selector de dificultad en UI (item 3 del roadmap) — la escalada aquí es por **racha**, no por dificultad.
- Burlas en 1ª persona / personaje con voz propia (se decidió locutor en 3ª persona).
- Cambiar las líneas de "turno" neutrales en 1P (oddity menor: el turno del CPU usa hoy la voz de "jugador 2").
- Música de fondo / proyecto de pruebas de Web.
- Eliminar `Won`/`Drew` (solo tras auditar consumidores, ver §2).

## 8. Addendum v3 — rachas generalizadas + rotura grande (+3)

Extiende el diseño (aprobado por el usuario) tras grabar los audios.

**Rachas en ambos modos.** Se generaliza el rastreo: en vez de solo la racha del CPU, `GameSession` lleva la
racha de victorias consecutivas del **líder actual**, en 1P **y** 2P:
- `StreakHolder` (índice del líder, `-1` si nadie), `WinStreak` (sus victorias seguidas), `BrokenStreakLength`
  (transitorio: la racha del rival que el ganador de esta partida acaba de romper, `0` si no rompió ninguna).
- `CpuWinStreak` ahora es **derivado** (`Mode==1P && StreakHolder==1 ? WinStreak : 0`) y sigue alimentando el
  nivel de burla del CPU sin cambios. Se retira `CpuStreakJustBroken`.
- Reset: `StreakHolder`/`WinStreak`/`PlayerLossesAgainstCpu` solo en `BeginGame`; `BrokenStreakLength` cada `ResetState`.
- Transición pura en `Core`: `CpuTauntPolicy.AdvanceStreak(prevHolder, prevStreak, winner)` → `StreakOutcome`
  (empate = sin cambio; mismo ganador = +1; ganador distinto = reset a 1 y reporta la racha rota). TDD.

**Rotura por niveles** (en `OnMatchEnded`, cuando gana un humano / cualquiera en 2P):
- `BrokenStreakLength ≥ 3` (`BigBreakThreshold`) → **`streak-break-big`** (en **cualquier modo**, texto neutral)
  y, al final, el sting **aleluya** `game/streak-break.mp3` *después* de la voz (reemplaza al win cheer).
- `BrokenStreakLength == 2` (`BreakThreshold`) y 1P → `streak-break` (como antes) + win cheer.
- Si no rompió racha: 1P → `beat-cpu`; 2P → `VictoryV` neutral. + win cheer.
- Gana el CPU (1P) → `cpu-win` + `loss-sting` (ahora **rota** entre `loss-sting.mp3` y `loss-sting-01.mp3`); sin cheer.

**Audios nuevos / cambios de `AudioKeys`:**
- Arreglos expandidos a las 3 variantes grabadas (idle a 5).
- `StreakBreakBig` = `voice/streak-break-big-01..03.mp3`.
- `StreakBreakBigSting` = `game/streak-break.mp3` (SFX aleluya, post-voz, solo +3).
- `LossSting` pasa a arreglo `{ game/loss-sting.mp3, game/loss-sting-01.mp3 }` (rota).

**Frases `streak-break-big` (neutral, 1P y 2P):** «¡Se acabó el dominio!» · «¡Frenaste una rachota!» ·
«¡Increíble, rompiste la racha!» (3 variantes grabadas).

**Fuera de alcance (extras grabados, pendientes de cablear):** `block-move`, `chip-placed`, `match-start`,
`player-ready`, `tap-to-start`, `thanks-for-playing` — cada uno necesita su propio disparador; se diseñan aparte.
