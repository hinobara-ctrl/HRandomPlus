# Punto de congelación pre-lazer

> Historical v0.1.1 baseline. Native lazer development now lives in [`LAZER_IMPLEMENTATION.md`](LAZER_IMPLEMENTATION.md); this file intentionally preserves the pre-lazer record.

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
- **AUTOMATED VERIFIED LOCALLY:** el formateador ya no atribuye toda detección automática a tosu. Windows muestra `Beatmap detected automatically from osu!stable`, Linux conserva `Beatmap detected automatically by tosu` y la selección manual mantiene su texto propio. La detección real nunca estuvo rota; era un defecto exclusivamente visual.
- **VERIFIED ON WINDOWS AND LINUX:** H-Random/S-Random permanecen protegidos; Custom es único, persistente y reseteable; perfiles personales tienen GUID, migración idempotente e import/export `.hrp-profile.json` validado. El playtest final de esta UI terminó sin bugs reportados y con los estados correctos en Windows y la VM Linux.

## Invariantes conservadas

- Original intacto; outputs nunca se sobrescriben.
- `BeatmapID:0` y `BeatmapSetID` sin cambio de política.
- HitObject count, tiempos, end times, long notes, rangos y seed reproducible cubiertos.
- Parámetros y scoring del motor H-Random/S-Random sin cambios.
- Selección manual permanece disponible.
- Sin `sudo`, edición de `osu!.db` ni rutas Wine `Z:` construidas manualmente.

## Evidencia final de cierre stable

- **VERIFIED:** smoke test del ejecutable Windows x64 final; abrió y cerró correctamente.
- **VERIFIED REMOTELY:** `main` alcanzó `de92500` y GitHub Actions completó correctamente tests y artefactos Windows/Linux para ese commit.
- **VERIFIED LOCALLY AFTER PROFILE SYSTEM:** 100/100 pruebas del runner propio, build Release con 0 errores y smoke start aislado del ejecutable Windows aprobado.
- **VERIFIED:** `git diff --check` sin errores de whitespace (solo avisos LF/CRLF).
- **VERIFIED:** ZIP Windows x64 y Linux x64 contienen únicamente el ejecutable, runtime nativo necesario, README, LICENSE, configuración de ejemplo y los avisos/textos de licencias aplicables.
- **READY LOCALLY:** cierre pre-lazer probado, auditado y empaquetado.
- **COMPLETED:** el historial con atribución corregida fue subido; ya no hay un push de anonimización pendiente.
- **PENDING OWNER ACTION:** revisar y hacer commit/push de esta preparación y decidir la visibilidad final.
- **LICENSE READY:** HRandomPlus y los componentes enlazados de memoria se distribuyen bajo `GPL-3.0-or-later`; los binarios se acompañan de las fuentes exactas, snapshot upstream, notices y checksums.
- **PENDING:** tag y Release formal `v0.1.0-playtest` después de superar todos los gates.
- **OUT OF SCOPE:** osu!lazer continúa sin fuente, importador ni acceso a su almacenamiento.
