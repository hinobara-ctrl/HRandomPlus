<!-- document-status: historical -->
> Tipo de documento: evidencia de mantenimiento obtenida el 2026-09-01; no es autoritativa para futuros HEAD.

# Mantenimiento de rendimiento y gobernanza — septiembre de 2026

## Alcance y entorno

La baseline se tomó sobre el working tree posterior al hardening y antes de modificar el motor. Equipo Windows x64, SDK .NET `8.0.419`, configuración Release, un warmup y cinco iteraciones medidas por escenario. El benchmark usa 64 timestamps de acordes 9/18K; cada timestamp produce el límite indicado de candidatos.

No se modificaron fórmulas, defaults, perfiles, heurísticas ni llamadas al RNG.

## Rendimiento BEFORE / AFTER

| Escenario | Categoría | Total before | Total after | Media before | Media after | Diferencia | Alloc media before | Alloc media after | Diferencia |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 4096 / Top12 | Usuario soportado | 2553,836 ms | 1295,814 ms | 510,767 ms | 259,163 ms | −49,26 % | 561.837.156 B | 236.098.356 B | −57,98 % |
| 8192 / Top12 | Usuario soportado | 5707,949 ms | 3141,497 ms | 1141,590 ms | 628,299 ms | −44,96 % | 1.112.824.864 B | 452.519.912 B | −59,34 % |
| 16384 / Top12 | Diagnóstico fuera de contrato | 11642,551 ms | 7159,568 ms | 2328,510 ms | 1431,914 ms | −38,51 % | 2.319.504.624 B | 914.433.113 B | −60,58 % |
| 4096 / Top4096 | Stress | 2910,336 ms | 1532,920 ms | 582,067 ms | 306,584 ms | −47,33 % | 568.109.784 B | 249.668.712 B | −56,05 % |
| 8192 / Top4096 | Stress | 5516,250 ms | 3391,296 ms | 1103,250 ms | 678,259 ms | −38,52 % | 1.119.097.888 B | 477.612.008 B | −57,32 % |

Las colecciones totales Gen0/Gen1/Gen2 de las cinco iteraciones cambiaron respectivamente: 4096/Top12 `170/55/25 → 65/50/10`; 8192/Top12 `320/115/65 → 130/90/10`; 16384/Top12 `670/285/220 → 270/115/50`; stress 4096 `170/55/25 → 75/55/10`; stress 8192 `325/114/60 → 145/64/40`.

Los tiempos son comparaciones locales, no umbrales universales. Las allocations y salidas deterministas son la evidencia menos sensible al ruido del sistema.

## Determinismo

Las referencias SHA-256 del `.osu` completo se fijaron antes y pasaron después de cada cambio:

| Fixture | Seed | SHA-256 |
| --- | ---: | --- |
| 4k-small-stream | 101 | `73c02d24a82bc3431f64e7f594e59f2e9c9f0ef8033131c9b78bb7bdafc2f282` |
| 7k-ln-dynamic-chords | 987654321 | `b82d26bd5430822ceeffc3aed5d29b44c7e24bc3a75ed7c75b903aae11a0f711` |
| 10k-dual-stage | 20260901 | `db6a3b02b061bd369c3e6e82a30f145831af27966d178fb060c5976ebb4075a7` |
| 11k-shared-center | −20260901 | `76888e857d5b4c861cebb5e73739da593080a92c5733c166af4ada8a2001198b` |
| 18k-large-dense | 246813579 | `659d886f1b058598122c011f903e6f3ad9f290cea2278f7dfe421af6672ae19d` |

También se fijaron cuatro scores representativos: `-34.833333333333336`, `92`, `46.5` y `-51.08333333333334`.

**All deterministic reference outputs are byte-identical: YES.**

## Optimizaciones aceptadas

1. La deduplicación dejó de construir `string.Join` por candidato. Usa una identidad `ulong` y conserva la primera aparición en una lista independiente. El contrato real del parser es 1K–18K; la representación admite hasta 64 columnas sin imponer un límite inferior accidental. Complejidad asintótica equivalente, con menos conversiones y strings.
2. `CandidateScorer` precalcula una vez por timestamp el máximo de uso reciente, último patrón y timeout de trill. Antes `RecentColumnUsage.Max()` se repetía por cada columna de cada candidato. Las operaciones matemáticas permanecen en el mismo orden.
3. `WeightedChoice` usa Top-K estable con heap cuando `K < N`: `O(N log N)` pasa a `O(N log K)`. Score descendente e índice original ascendente reproducen el desempate estable de LINQ. Cuando `K == N`, conserva el ordenamiento completo, que es más apropiado para stress.
4. La ruta interna de scoring recibe candidatos ya ordenados y evita `OrderBy().ToArray()` por candidato. La API pública continúa aceptando cualquier orden.
5. El sampling reutiliza un buffer de shuffle. Restablece las mismas columnas antes de cada Fisher–Yates, consume exactamente las mismas llamadas RNG y solo asigna el candidato que se debe conservar/probar.

## Optimizaciones descartadas

- Más cambios en `PatternAnalyzer`, LN, stage combination o LINQ no medido: descartados para evitar complejidad sin evidencia.
- Compresión ZIP nivel 9: produjo exactamente el mismo tamaño que nivel 6 para el candidato Linux; no se cambió CI.
- ReadyToRun y eliminación de apphost: no evaluados porque contradicen el objetivo de tamaño/UX.
- Trimming: descartado y no habilitado; véase la sección específica.

## Tamaño BEFORE / AFTER

| Métrica | Before | After | Diferencia |
| --- | ---: | ---: | ---: |
| Windows publish | 33.936.271 B | 33.944.803 B | +8.532 B (+0,0251 %) |
| Windows ZIP | 13.709.019 B | 13.712.620 B | +3.601 B (+0,0263 %) |
| Linux publish | 38.379.168 B | 38.386.164 B | +6.996 B (+0,0182 %) |
| Linux ZIP | 38.381.030 B | 38.388.026 B | +6.996 B (+0,0182 %) |
| Archivos Windows | 20 | 20 | 0 |
| Archivos Linux | 14 | 14 | 0 |

El pequeño aumento proviene del código adicional de Top-K/contexto/deduplicación dentro del single-file y de la breve documentación de gobernanza añadida al README incluido. No se añadieron dependencias ni archivos de distribución.

Archivos dominantes Windows after: `libSkiaSharp.dll` 11.628.896 B; `HRandomPlus.exe` 10.888.862 B; `av_libglesv2.dll` 5.394.096 B; `realm-wrappers.dll` 3.879.936 B; `libHarfBuzzSharp.dll` 1.816.088 B. Los otros 15 archivos son README, configuración y textos de licencia/notice, todos inferiores a 140 KiB.

Linux contiene 14 archivos: `HRandomPlus` 38.086.062 B y trece archivos requeridos de documentación/configuración/licencias, todos inferiores a 140 KiB. Las librerías nativas y managed están contenidas en el single-file.

El manifest de publish confirma Realm/Avalonia/Skia/HarfBuzz en ambas plataformas. `OsuMemoryDataProvider 0.12.2` y `ProcessMemoryDataFinder 0.10.2` solo forman parte del target Windows. Tests, benchmark, PDB y outputs internos no aparecen en los ZIP.

## Trimming

Se publicó un experimento separado con `PublishTrimmed=true` y `TrimMode=partial`. Al mantener framework-dependent, el payload sin PDB fue idéntico al normal: Windows 33.607.878 B y Linux 38.086.062 B. No hubo ahorro; Avalonia/Realm continúan siendo rutas dinámicas sensibles y no se realizaron smoke tests de una variante trimmed.

**EXPERIMENTAL — NOT ENABLED.**

## Gobernanza

- `Directory.Build.props` permanece como única fuente canónica de versión; CI deriva todos los nombres desde ella.
- CI ejecuta `scripts/check-repo-consistency.ps1` en Windows y Ubuntu.
- El release candidate genera `release-evidence.txt` con versión, SHA del run, SDK, configuración, resultado del job de tests y hashes de los cuatro assets del mismo run.
- Cada documento bajo `docs/` declara `current` o `historical`. Las notas publicadas y evidencia de v0.1/v0.2.1 quedaron explícitamente congeladas.
- `docs/templates/PRE_PUSH_CHECKLIST.md` describe el procedimiento sin SHA, hashes ni conteos mutables.
- `docs/templates/RELEASE_NOTES_TEMPLATE.md` evita reutilizar notas de una release anterior.

## Consistency checker

Comprueba versión canónica, derivación de versión en CI, ausencia de versión literal en YAML, exactamente dos publishes framework-dependent, targets/RID, ausencia de self-contained, integración de evidence/checker, targets Desktop, condición Windows de memoria, nombres/versiones/requisito .NET del README, clasificación de todos los documentos y ausencia de conteos/HEAD efímeros en documentación current.

Resultado local al cerrar este informe: `PASS`.

## Validación

- Restore locked: PASS.
- Build Release: PASS.
- Suite después de optimizar: 357 casos aprobados, 0 fallidos.
- Benchmark before/after: PASS, cinco iteraciones y warmup.
- Referencias deterministas: PASS, byte-idénticas.
- Publish Windows/Linux: PASS.
- Auditoría de contenido y licencias: PASS.
- Trimming: medido y descartado.
- GitHub Actions Windows/Ubuntu: pendiente del futuro push; no se presenta como ejecutado.
