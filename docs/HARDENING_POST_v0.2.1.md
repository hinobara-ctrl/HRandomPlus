# Hardening posterior a v0.2.1

La primera pasada no cambió scoring ni jugabilidad. Una decisión de producto posterior aprobó explícitamente los cambios de stages, trills, pausas y presentación BPM documentados al final de este archivo. La neutralidad del centro para Hand Balance, Extreme Jump, Hand Balance y la lista de snaps permanecen sin cambios.

## Cambios implementados

- `MaxCandidateSets`: default conservado en 4.096 y máximo validado en 8.192. `WeightedTopCandidates` debe estar entre 1 y ese máximo. Configuraciones persistidas anteriores se ajustan en memoria y se guardan mediante la migración existente; entradas nuevas y perfiles importados reciben error explícito.
- Combinatoria: el conteo es exacto para 1K–18K, usa aritmética acotada y el límite de intentos se calcula en `long`, sin overflow silencioso.
- Winello: el cleanup temporal es best-effort, comprueba que la ruta sea hija del root creado por HRandomPlus y nunca enmascara el resultado principal. Un warning puede registrarse mediante el sink local.
- Múltiples stable: una ruta configurada tiene prioridad; una instancia actual válida se conserva; una única candidata se acepta; candidatas indistinguibles se reportan como ambiguas en vez de depender del orden de `Process.GetProcessesByName`.
- `DifficultySuffix`: una única política portable rechaza controles, `<>:"/\\|?*` y punto/espacio final, manteniendo Unicode normal. Los nombres reservados de dispositivo no se rechazan porque el sufijo se añade al nombre original y no constituye por sí solo el stem completo.
- Diagnóstico: la salida compartible de `--diagnose` sustituye el home por `%USERPROFILE%` o `$HOME`; los logs locales internos conservan detalle para depuración.
- `MainWindow`: el parsing y la validación del formulario de configuración se movieron a `HRandomConfigInputParser`, testeable y sin dependencia de Avalonia. El archivo pasó de 811 a 794 líneas.

## Benchmark de candidatos

Fixture: mapa sintético 18K, 64 acordes de 9 notas, misma seed y configuración, una ejecución de warm-up y cinco mediciones por límite. El harness mide directamente el motor y permite deliberadamente 16.384 para comparar ese valor rechazado por la validación pública. Equipo local Windows; los valores sirven para comparación relativa, no como umbral CI.

| Candidatos | Mediana | Mínimo | Máximo | Bytes asignados, mediana acumulada |
|---:|---:|---:|---:|---:|
| 4.096 | 497,343 ms | 489,650 ms | 506,518 ms | 568.111.752 |
| 8.192 | 987,273 ms | 981,604 ms | 1.021,750 ms | 1.119.099.792 |
| 16.384 | 2.091,401 ms | 1.967,784 ms | 2.165,829 ms | 2.325.779.576 |

La tabla se volvió a medir después de unificar la detección de trills. El coste prácticamente se duplica con cada duplicación. 8.192 conserva margen sobre el default para configuraciones existentes sin admitir el escalón 16.384, cuyo coste es desproporcionado en patrones extremos. El máximo combinatorio real soportado es `C(18,9) = 48.620`.

## Polling y procesos

| Operación | Intervalo/frecuencia | Cache existente | Decisión |
|---|---:|---|---|
| Coordinación de fuentes | 200 ms | estado y selección anterior | Sin cambio para no alterar latencia visible. |
| stable Windows | una enumeración `osu!` por tick activo | reader retenido por PID + hora de inicio | Política determinista añadida; no se comparte `Process` con lazer por ownership/disposal. |
| proceso lazer | nombres `osu!`/`osu` por tick | sesión y resultado resuelto | Sin cache temporal nueva: una demora artificial podría ocultar cierre/reapertura. |
| discovery de storage lazer | cada 5 s | storage actual | Ya acotado. |
| runtime log | cada tick | posición, decoder y selección | Sólo se leen bytes añadidos; el scan inicial está limitado y busca hacia atrás por bloques. |
| Realm | al cambiar selección/sesión | resolución cacheada | Ya invalidado por eventos y reapertura. |

No se encontró una optimización adicional inequívoca que justificara cambiar la semántica temporal. El polling se cancela al cerrar y las fuentes se disponen.

La selección externa de stable ya no depende del orden de enumeración. La librería de memoria utilizada sigue creando su watcher por nombre de proceso y no expone en esta integración un binding directo al PID elegido; por ello HRandomPlus se abstiene cuando existen dos procesos stable indistinguibles en lugar de afirmar que controla uno arbitrario.

## Cobertura añadida

Se añadieron regresiones para límites/migración/overflow, combinaciones 1K–18K, sufijos Unicode e inválidos, importación de perfiles, política multi-stable (incluido PID reutilizado), redacción Windows/Linux, parsing extraído de UI y combinaciones de resultado/cleanup Winello. La matriz dual-stage cubre explícitamente 9K como control negativo y 10K–18K con la opción activada y desactivada; los límites 4×/8× se prueban en −1, exacto y +1 con varios thresholds.

## Decisiones de producto implementadas posteriormente

- **Dual-stage 10K+:** `PreserveDualStages` permite elegir entre el comportamiento global anterior y evitar cruces directos entre stages. La UI sólo habilita la opción con un mapa 10K o superior. En keymodes impares, el centro es compartido: puede intercambiarse con cualquiera de los dos stages y sólo permanece neutral para Hand Balance.
- **Trills:** scoring y estadísticas comparten una definición de acorde compatible. El acorde debe contener la columna esperada y no la anterior; otras columnas son acompañamiento. Si contiene ambas columnas, reinicia la secuencia.
- **Pausas:** una separación superior a `4 × MaxThresholdMs` corta el trill. Una separación superior a `8 × MaxThresholdMs` devuelve Dynamic Threshold a `BaseThresholdMs`.
- **BPM/snaps:** se conservan exactamente los divisores existentes. La UI muestra los doce valores en una cuadrícula compacta y resume múltiples BPM como un rango.
- **Centro impar, Extreme Jump y Hand Balance:** comportamiento actual conservado.
- **Diagnóstico:** `--diagnose` sigue siendo una operación local opcional, sin telemetría ni networking adicional; la salida compartible redacta el home y los logs internos conservan detalle local.
- **Múltiples stable:** se conserva la selección segura y determinista ya implementada.

## Pendiente futuro

- métricas opcionales de producto o telemetría: no implementadas.
