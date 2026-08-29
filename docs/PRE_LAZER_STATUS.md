# Punto de congelación pre-lazer

Fecha: 2026-08-29

HRandomPlus continúa dirigido exclusivamente a osu!stable. No se implementó soporte, fuente, importador ni acceso al almacenamiento de osu!lazer.

## Estado funcional

- **VERIFIED:** Windows + osu!stable completó el playtest anterior sin regresiones reportadas.
- **VERIFIED:** Linux VM real detecta el mapa mediante osu-winello + tosu y genera outputs válidos.
- **VERIFIED:** la prueba A/B real mostró que la copia nativa necesitó F5 y la copia Wine-side fue detectada sin F5.
- **MOCK TESTED:** integración `WineSideFileImporter`, `winepath`, argumentos complejos, fallos, timeout, verificación del destino y fallback.
- **AUTOMATED VERIFIED:** repetir H-Random/S-Random/Custom genera Version y filename únicos dentro y fuera de `Songs`.
- **AUTOMATED VERIFIED:** `CONNECTED(A) → DISCONNECTED → CONNECTED(A)` actualiza conectividad aunque la identidad no cambie.
- **VERIFIED:** el build integrado Wine-side, la detección, la generación y la reconexión completaron el playtest real en la VM.
- **AUTOMATED VERIFIED:** el origen de selección ahora distingue detección automática por tosu de selección manual, incluso al desconectar y reconectar con el mismo mapa.
- **AUTOMATED VERIFIED:** una selección manual conserva prioridad mientras tosu siga informando el mismo mapa; al cambiar de mapa en osu!, la detección automática recupera el control.
- **VERIFIED:** el build Linux `r2` muestra correctamente el origen automático, respeta una selección manual ante el mismo mapa de tosu y recupera el modo automático al cambiar de mapa en osu!.

## Invariantes conservadas

- Original intacto; outputs nunca se sobrescriben.
- `BeatmapID:0` y `BeatmapSetID` sin cambio de política.
- HitObject count, tiempos, end times, long notes, rangos y seed reproducible cubiertos.
- Parámetros y scoring del motor H-Random/S-Random sin cambios.
- Selección manual permanece disponible.
- Sin `sudo`, edición de `osu!.db` ni rutas Wine `Z:` construidas manualmente.

## Evidencia final de cierre stable

- **VERIFIED:** smoke test del ejecutable Windows x64 final; abrió y cerró correctamente.
- **VERIFIED:** 77/77 pruebas y build Release con 0 errores.
- **VERIFIED:** `git diff --check` sin errores de whitespace (solo avisos LF/CRLF).
- **VERIFIED:** ZIP Windows x64 y Linux x64 contienen únicamente el ejecutable, runtime nativo necesario, README, LICENSE y configuración de ejemplo.
- **READY LOCALLY:** cierre pre-lazer probado, auditado y empaquetado.
- **PENDING OWNER ACTION:** push del historial anonimizado; tag/release solo si el propietario decide publicarlos.
