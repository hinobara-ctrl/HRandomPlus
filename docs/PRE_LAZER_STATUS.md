# Punto de congelación pre-lazer

> Base histórica de v0.1.1. El desarrollo nativo de lazer ahora está en [`LAZER_IMPLEMENTATION.md`](LAZER_IMPLEMENTATION.md); este archivo conserva intencionalmente el registro pre-lazer.

Fecha: 2026-08-29

HRandomPlus continúa dirigido exclusivamente a osu!stable. No se implementó soporte, fuente, importador ni acceso al almacenamiento de osu!lazer.

## Estado funcional

- **VERIFICADO:** Windows + osu!stable completó el playtest anterior sin regresiones reportadas.
- **VERIFICADO:** Linux VM real detecta el mapa mediante osu-winello + tosu y genera outputs válidos.
- **VERIFICADO:** la prueba A/B real mostró que la copia nativa necesitó F5 y la copia mediante Wine fue detectada sin F5.
- **PROBADO CON MOCKS:** integración `WineSideFileImporter`, `winepath`, argumentos complejos, fallos, timeout, verificación del destino y fallback.
- **VERIFICADO AUTOMÁTICAMENTE:** repetir H-Random/S-Random/Custom genera versión y nombre de archivo únicos dentro y fuera de `Songs`.
- **VERIFICADO AUTOMÁTICAMENTE:** `CONNECTED(A) → DISCONNECTED → CONNECTED(A)` actualiza conectividad aunque la identidad no cambie.
- **VERIFICADO:** la build integrada mediante Wine, la detección, la generación y la reconexión completaron el playtest real en la VM.
- **VERIFICADO AUTOMÁTICAMENTE:** el origen de selección distingue detección automática por tosu de selección manual, incluso al desconectar y reconectar con el mismo mapa.
- **VERIFICADO AUTOMÁTICAMENTE:** una selección manual conserva prioridad mientras tosu siga informando el mismo mapa; al cambiar de mapa en osu!, la detección automática recupera el control.
- **VERIFICADO:** la build Linux `r2` muestra correctamente el origen automático, respeta una selección manual ante el mismo mapa de tosu y recupera el modo automático al cambiar de mapa en osu!.
- **VERIFICADO AUTOMÁTICAMENTE EN LOCAL:** el formateador ya no atribuye toda detección automática a tosu. Windows muestra `Beatmap detected automatically from osu!stable`, Linux conserva `Beatmap detected automatically by tosu` y la selección manual mantiene su texto propio. La detección real nunca estuvo rota; era un defecto exclusivamente visual.
- **VERIFICADO EN WINDOWS Y LINUX:** H-Random/S-Random permanecen protegidos; Custom es único, persistente y restablecible; los perfiles personales tienen GUID, migración idempotente e importación/exportación `.hrp-profile.json` validada. El playtest final de esta UI terminó sin bugs reportados y con estados correctos en Windows y la VM Linux.

## Invariantes conservadas

- Original intacto; outputs nunca se sobrescriben.
- `BeatmapID:0` y `BeatmapSetID` sin cambio de política.
- HitObject count, tiempos, end times, long notes, rangos y seed reproducible cubiertos.
- Parámetros y scoring del motor H-Random/S-Random sin cambios.
- Selección manual permanece disponible.
- Sin `sudo`, edición de `osu!.db` ni rutas Wine `Z:` construidas manualmente.

## Evidencia final de cierre stable

- **VERIFICADO:** smoke test del ejecutable Windows x64 final; abrió y cerró correctamente.
- **VERIFICADO REMOTAMENTE:** `main` alcanzó `de92500` y GitHub Actions completó correctamente pruebas y artifacts Windows/Linux para ese commit.
- **VERIFICADO LOCALMENTE TRAS EL SISTEMA DE PERFILES:** 100/100 pruebas del runner propio, build Release con 0 errores e inicio smoke aislado del ejecutable Windows aprobado.
- **VERIFICADO:** `git diff --check` sin errores de espacios en blanco (solo avisos LF/CRLF).
- **VERIFICADO:** los ZIP Windows x64 y Linux x64 contienen únicamente el ejecutable, runtime nativo necesario, README, LICENSE, configuración de ejemplo y avisos/textos de licencias aplicables.
- **LISTO LOCALMENTE:** cierre pre-lazer probado, auditado y empaquetado.
- **COMPLETADO:** se subió el historial con atribución corregida; ya no hay un push de anonimización pendiente.
- **ACCIÓN DEL PROPIETARIO PENDIENTE:** revisar y hacer commit/push de esta preparación y decidir la visibilidad final.
- **LICENCIA LISTA:** HRandomPlus y los componentes enlazados de memoria se distribuyen bajo `GPL-3.0-or-later`; los binarios se acompañan de fuentes exactas, snapshot upstream, avisos y checksums.
- **PENDIENTE:** tag y Release formal `v0.1.0-playtest` después de superar todas las condiciones.
- **FUERA DE ALCANCE:** osu!lazer continúa sin fuente, importador ni acceso a su almacenamiento.
