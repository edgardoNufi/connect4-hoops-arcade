# Locutor que pica — burlas del CPU conscientes de la racha + idle universal (diseño)

> Estado: aprobado para escribir plan. Idioma de las frases: español (LATAM). Tono: locutor arcade
> agresivo / trash-talk, **sin groserías**.

## Objetivo

Hacer el juego más retador y enganchador en 1 jugador dándole **filo** al locutor: que se burle del
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
| `CpuWinStreak` | `int` | Victorias consecutivas del CPU en 1P. `+1` cuando gana el CPU; `0` cuando gana el humano; sin cambio en empate. Reset en `BeginGame`; persiste en `Rematch`/`ResetBoard` (igual que `Scores`). |
| `CpuStreakJustBroken` | `bool` | `true` solo cuando el humano gana y la racha previa era **≥ `CpuTauntPolicy.BreakThreshold` (2)**. Lo lee el narrador para `streak-break`. |
| `PlayerLossesAgainstCpu` | `int` | Derrotas acumuladas del humano vs CPU. `+1` cuando gana el CPU; reset en `BeginGame`. Señal disponible para una línea más empática; no es el driver principal. |

Solo se actualizan en modo **1 jugador**. En 2 jugadores quedan inertes.

## 2. Eventos nuevos / cambios en `GameSession`

- `event Action IdleNudged` — se dispara dentro de `ArmIdle`, en el momento en que pone `IsIdle = true`.
  Ya arma en ambos modos (solo se inhibe en turno del CPU), así que cubre 1P y 2P.
- `event Action<int> ThreatRaised` — **cambia de `Action` a `Action<int>`**. Carga `ThreatOwnerIndex`
  (el jugador que acaba de mover y dejó el tres-en-línea = `1 - Current` al momento del chequeo).
- `event Action<int?, GameMode> MatchEnded` — evento único de fin de partida: `winner` (`null` = empate)
  + `mode`. **La racha se actualiza ANTES de dispararlo** para que el narrador lea valores frescos.
  Sustituye a `Won`/`Drew` como disparador de audio de cierre; esos dos se retiran del narrador y se
  eliminan de `GameSession` si nada más los consume.
- `event Action RoundStarted` — se dispara al final de `ResetState` (lo llaman `BeginGame`/`Rematch`/
  `ResetBoard`). Reinicia el tope "1 burla de mid-game por partida" en el narrador. Independiente de
  `GameStarted` (que sigue disparando solo el "¡prepárate!" en `BeginGame`).

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
- `_lastTauntAt` (timestamp) — cooldown global de burlas mid-game (~7 s).
- `_midGameTauntUsedThisRound` (bool) — tope de 1 burla mid-game por partida; se reinicia en `RoundStarted`.
- `_lastIndexByCategory` — para no repetir variante seguida.

### Reglas de prioridad
1. **Solo 1P** usa las burlas del CPU. 2P mantiene narración neutral (almost-win neutral, victoria/empate neutrales).
2. **Cooldown** ~7 s entre burlas de mid-game.
3. **No repetir** la misma frase dos veces seguidas (por categoría).
4. **Máximo 1 burla de mid-game por partida** (amenaza del CPU *o* idle en 1P, lo que ocurra primero); reset con `RoundStarted`. Las líneas de cierre (CPU gana, romper racha, ganarle al CPU) están exentas (son una por partida por naturaleza).
5. El idle **genérico** (2P y cualquier contexto no-CPU) usa cooldown + no-repetir, pero **no** el tope de 1/partida; queda naturalmente espaciado por el timer de idle (9 s) + cooldown.
6. La cola serial de voces (ya existente) garantiza que nunca se solapen; el cooldown/tope evitan que sea cansino.

### Tabla evento → intención

| Evento | 1 jugador | 2 jugadores |
|---|---|---|
| `ThreatRaised(owner)` | owner = CPU → `cpu-threat` nivel `LevelFor(CpuWinStreak)` (cuenta como burla mid-game) · owner = humano → almost-win neutral | almost-win neutral |
| `IdleNudged` | `cpu-idle` nivel `LevelFor(CpuWinStreak)` (cuenta como burla mid-game) | `idle` genérico (cooldown + no-repetir) |
| `MatchEnded` · CPU gana | `cpu-win` nivel `LevelFor(CpuWinStreak)` *post-incremento* (Light/Confident/Boss). **Sin** cheer de victoria | n/a |
| `MatchEnded` · humano gana | `CpuStreakJustBroken` → `streak-break` (+ cheer) · si no → `beat-cpu` (+ cheer) | `VictoryV` neutral + cheer (como hoy) |
| `MatchEnded` · empate | empate neutral (como hoy) | empate neutral (como hoy) |

> Nota: en 1P, al ganar el CPU **no** suena el "win cheer" (`WinSfx`); es una derrota del jugador, así que
> solo va la voz de burla. El cheer se conserva cuando gana el humano (incluido `streak-break`/`beat-cpu`).

## 5. Catálogo de audios (guía de grabación/búsqueda)

Carpeta `wwwroot/audio/voice/`. Convención: kebab, slug de nivel (`neutral|light|confident|boss`),
numerado, `.mp3` (`.m4a` también sirve). Arreglos en `AudioKeys` crecen agregando archivos. Listadas
3 variantes por celda como menú inicial; graba las que quieras (mínimo 1 por celda para arrancar).

### Mid-game — escalan con la racha
**`cpu-threat-<level>-NN.mp3`** (el CPU armó 3 en línea):
- neutral: «La máquina ya huele sangre.» · «Cuidado, te está cazando.» · «Mira bien… va por la tuya.»
- light: «Otra vez te acorrala.» · «¿No aprendiste? Ahí viene.» · «Ya te midió.»
- confident: «Tres en línea. Otra vez. Qué predecible.» · «Juega contigo… y tú lo permites.» · «Tu error, su victoria.»
- boss: «Ni la veas, ya ganó.» · «La máquina no suda. Tú sí.» · «Acéptalo: estás para perder.»

**`cpu-idle-<level>-NN.mp3`** (te tardas, 1P):
- neutral: «¿Y esa duda? La máquina ya decidió.» · «El miedo huele feo, ¿eh?» · «Tic-tac… no espera.»
- light: «Piénsalo todo lo que quieras, igual caes.» · «¿Buscando salida? No hay.» · «Ya sabe qué vas a hacer.»
- confident: «Tómate tu tiempo… el resultado es el mismo.» · «Dudas y dudas, ya perdiste.» · «La máquina bosteza.»
- boss: «¿Para qué piensas? Ya estás muerto.» · «Ríndete y ahórrate la pena.» · «Ni te toma en serio.»

### Cierre
**`cpu-win-<level>-NN.mp3`** (gana el CPU; niveles light/confident/boss — ganar siempre deja racha ≥1):
- light: «La máquina no perdona.» · «Demasiado fácil.» · «¿Eso era todo?»
- confident: «Van dos. ¿Vas por la humillación completa?» · «Dos seguidas, ni se esforzó.» · «Otra para la colección.»
- boss: «Ni te despeines… no le ganas.» · «Ya perdí la cuenta de tus derrotas.» · «La máquina es tu dueña.»

**`streak-break-NN.mp3`** (humano rompe racha ≥2): «¡POR FIN! Le rompiste la racha.» · «¡La humillaste de vuelta!» · «¡Milagro! La máquina mordió el polvo.»

**`beat-cpu-NN.mp3`** (humano gana, racha <2): «¡Le ganaste a la máquina! ¿Otra?» · «Perdió. Disfrútalo mientras dure.» · «La máquina quiere revancha.»

### Idle genérico — cualquier modo
**`idle-NN.mp3`** (locutor apura al lento, 2P/no-CPU): «¿Sigues ahí? El tablero espera.» · «El tiempo corre… ¡muévete!» · «¿Te dormiste? Es tu turno.» · «Menos pensar, más jugar.» · «Cualquier día de estos…»

### 5.1 Tabla de producción (checklist de grabación)

Lista autoritativa de archivos a producir. Todos en `wwwroot/audio/voice/`. Marca el progreso en la
columna ☐. Las `NN` están enumeradas según las 3 variantes del menú; **puedes grabar menos** (mínimo `-01`
por celda para arrancar) o más (solo agrega `-04`, `-05`… y se suman al arreglo).

**Mid-game · `cpu-threat-*` — suena cuando el CPU acaba de armar un 3-en-línea (solo 1P).**

| ☐ | Archivo | Nivel (racha) | Frase |
|---|---|---|---|
| ☐ | `cpu-threat-neutral-01.mp3` | Neutral (0) | La máquina ya huele sangre. |
| ☐ | `cpu-threat-neutral-02.mp3` | Neutral (0) | Cuidado, te está cazando. |
| ☐ | `cpu-threat-neutral-03.mp3` | Neutral (0) | Mira bien… va por la tuya. |
| ☐ | `cpu-threat-light-01.mp3` | Light (1) | Otra vez te acorrala. |
| ☐ | `cpu-threat-light-02.mp3` | Light (1) | ¿No aprendiste? Ahí viene. |
| ☐ | `cpu-threat-light-03.mp3` | Light (1) | Ya te midió. |
| ☐ | `cpu-threat-confident-01.mp3` | Confident (2) | Tres en línea. Otra vez. Qué predecible. |
| ☐ | `cpu-threat-confident-02.mp3` | Confident (2) | Juega contigo… y tú lo permites. |
| ☐ | `cpu-threat-confident-03.mp3` | Confident (2) | Tu error, su victoria. |
| ☐ | `cpu-threat-boss-01.mp3` | Boss (3+) | Ni la veas, ya ganó. |
| ☐ | `cpu-threat-boss-02.mp3` | Boss (3+) | La máquina no suda. Tú sí. |
| ☐ | `cpu-threat-boss-03.mp3` | Boss (3+) | Acéptalo: estás para perder. |

**Mid-game · `cpu-idle-*` — suena cuando el jugador se tarda en su turno (solo 1P).**

| ☐ | Archivo | Nivel (racha) | Frase |
|---|---|---|---|
| ☐ | `cpu-idle-neutral-01.mp3` | Neutral (0) | ¿Y esa duda? La máquina ya decidió. |
| ☐ | `cpu-idle-neutral-02.mp3` | Neutral (0) | El miedo huele feo, ¿eh? |
| ☐ | `cpu-idle-neutral-03.mp3` | Neutral (0) | Tic-tac… no espera. |
| ☐ | `cpu-idle-light-01.mp3` | Light (1) | Piénsalo todo lo que quieras, igual caes. |
| ☐ | `cpu-idle-light-02.mp3` | Light (1) | ¿Buscando salida? No hay. |
| ☐ | `cpu-idle-light-03.mp3` | Light (1) | Ya sabe qué vas a hacer. |
| ☐ | `cpu-idle-confident-01.mp3` | Confident (2) | Tómate tu tiempo… el resultado es el mismo. |
| ☐ | `cpu-idle-confident-02.mp3` | Confident (2) | Dudas y dudas, ya perdiste. |
| ☐ | `cpu-idle-confident-03.mp3` | Confident (2) | La máquina bosteza. |
| ☐ | `cpu-idle-boss-01.mp3` | Boss (3+) | ¿Para qué piensas? Ya estás muerto. |
| ☐ | `cpu-idle-boss-02.mp3` | Boss (3+) | Ríndete y ahórrate la pena. |
| ☐ | `cpu-idle-boss-03.mp3` | Boss (3+) | Ni te toma en serio. |

**Cierre · `cpu-win-*` — suena cuando gana el CPU (solo 1P; sin "win cheer"). Sin nivel Neutral.**

| ☐ | Archivo | Nivel (racha post-victoria) | Frase |
|---|---|---|---|
| ☐ | `cpu-win-light-01.mp3` | Light (1ª victoria) | La máquina no perdona. |
| ☐ | `cpu-win-light-02.mp3` | Light (1ª victoria) | Demasiado fácil. |
| ☐ | `cpu-win-light-03.mp3` | Light (1ª victoria) | ¿Eso era todo? |
| ☐ | `cpu-win-confident-01.mp3` | Confident (2 seguidas) | Van dos. ¿Vas por la humillación completa? |
| ☐ | `cpu-win-confident-02.mp3` | Confident (2 seguidas) | Dos seguidas, ni se esforzó. |
| ☐ | `cpu-win-confident-03.mp3` | Confident (2 seguidas) | Otra para la colección. |
| ☐ | `cpu-win-boss-01.mp3` | Boss (3+ seguidas) | Ni te despeines… no le ganas. |
| ☐ | `cpu-win-boss-02.mp3` | Boss (3+ seguidas) | Ya perdí la cuenta de tus derrotas. |
| ☐ | `cpu-win-boss-03.mp3` | Boss (3+ seguidas) | La máquina es tu dueña. |

**Cierre · humano gana (solo 1P; con "win cheer").**

| ☐ | Archivo | Cuándo | Frase |
|---|---|---|---|
| ☐ | `streak-break-01.mp3` | Rompes racha ≥2 | ¡POR FIN! Le rompiste la racha. |
| ☐ | `streak-break-02.mp3` | Rompes racha ≥2 | ¡La humillaste de vuelta! |
| ☐ | `streak-break-03.mp3` | Rompes racha ≥2 | ¡Milagro! La máquina mordió el polvo. |
| ☐ | `beat-cpu-01.mp3` | Le ganas, racha <2 | ¡Le ganaste a la máquina! ¿Otra? |
| ☐ | `beat-cpu-02.mp3` | Le ganas, racha <2 | Perdió. Disfrútalo mientras dure. |
| ☐ | `beat-cpu-03.mp3` | Le ganas, racha <2 | La máquina quiere revancha. |

**Idle genérico · `idle-*` — suena cuando alguien se tarda, en CUALQUIER modo (1P y 2P).**

| ☐ | Archivo | Frase |
|---|---|---|
| ☐ | `idle-01.mp3` | ¿Sigues ahí? El tablero espera. |
| ☐ | `idle-02.mp3` | El tiempo corre… ¡muévete! |
| ☐ | `idle-03.mp3` | ¿Te dormiste? Es tu turno. |
| ☐ | `idle-04.mp3` | Menos pensar, más jugar. |
| ☐ | `idle-05.mp3` | Cualquier día de estos… |

**Conteo:** 44 archivos con las 3 variantes del menú · 13 archivos si grabas solo `-01` por celda
(12 mid-game + 3 cpu-win + 1 streak-break + 1 beat-cpu + 1 idle). Formato: `.mp3` o `.m4a`, voz del mismo
locutor, mono, normalizado al nivel de las voces actuales en `wwwroot/audio/voice/`.

### Claves nuevas en `AudioKeys`
Arreglos por celda/categoría: `CpuThreat[Neutral|Light|Confident|Boss]`, `CpuIdle[…]`,
`CpuWin[Light|Confident|Boss]`, `StreakBreak`, `BeatCpu`, `IdleNudge`. (Nombres finales a definir en el plan;
el mapeo nivel→arreglo vive en la tabla `CpuTauntLines`.)

## 6. Pruebas y verificación

- **Core (TDD):** `CpuTauntPolicyTests` cubre la sección 3 (escribir primero, ver fallar, implementar).
- **`VoicePicker`:** test puro de no-repetición (último índice nunca se repite cuando `count > 1`).
- **`GameSession`/`NarratorService`:** no hay proyecto de pruebas de Web hoy. La transición pura ya queda
  cubierta por `CpuTauntPolicy`; el cableado (asignar contadores, disparar eventos, mapear) se verifica
  build-and-verify en navegador. Un proyecto de pruebas de Web queda fuera de alcance salvo petición.
- **Manual:** 1P — perder 1/2/3 seguidas y oír la escalada (light→confident→boss); romper racha y oír
  `streak-break`; confirmar tope de 1 burla mid-game/partida y cooldown; idle genérico en 2P; y que 2P
  conserva narración neutral en lo demás.

## 7. Fuera de alcance (por ahora)

- Selector de dificultad en UI (item 3 del roadmap) — la escalada aquí es por **racha**, no por dificultad.
- Burlas en 1ª persona / personaje con voz propia (se decidió locutor en 3ª persona).
- Cambiar las líneas de "turno" neutrales en 1P (oddity menor: el turno del CPU usa hoy la voz de "jugador 2").
- Música de fondo / proyecto de pruebas de Web.
