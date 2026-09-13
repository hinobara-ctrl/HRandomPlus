<!-- document-status: current -->
# Prueba A/B de importación Linux

Estado: **VERIFICADO EN VM LINUX REAL + osu-winello + Wine**.

Resultado observado:

- Copia Linux nativa: el `.osu` se creó correctamente, pero osu!stable no lo detectó al salir y volver a Song Select; F5 fue necesario.
- Copia mediante `osu-wine --wine cmd copy`: osu!stable detectó la dificultad sin F5; salir y volver a Song Select fue suficiente.
- No está probado cuál API o mecanismo de notificación explica la diferencia. No se debe afirmar como hecho que sea inotify o `ReadDirectoryChangesW`.

Registra distribución, filesystem de `Songs`, versión de osu-winello/Wine y si osu! estaba abierto en Song Select. Usa siempre un nombre de dificultad nuevo; no sobrescribas archivos.

## Preparación

```bash
SOURCE="/ruta/al/generated.osu"
DEST_DIR="$(cat "${XDG_DATA_HOME:-$HOME/.local/share}/osuconfig/osupath")/Songs/Carpeta del mapa"
DEST="$DEST_DIR/HRandomPlus AB Test.osu"
test -f "$SOURCE" && test -d "$DEST_DIR" && test ! -e "$DEST"
```

## Prueba A: copia Linux nativa

Con osu!stable abierto mediante Winello:

```bash
cp -n -- "$SOURCE" "$DEST"
```

Espera unos segundos sin pulsar F5 y anota si aparece la dificultad.

## Prueba B: copia desde el entorno Wine

Borra primero la dificultad de prueba desde osu! o usa otro nombre único. Convierte las rutas con Wine; no escribas letras `Z:` manualmente:

```bash
SOURCE_WIN="$(osu-wine --wine winepath -w "$SOURCE" | tr -d '\r')"
DEST_WIN="$(osu-wine --wine winepath -w "$DEST" | tr -d '\r')"
osu-wine --wine cmd /d /v:off /c copy /-y "$SOURCE_WIN" "$DEST_WIN"
```

Espera unos segundos sin pulsar F5 y anota si aparece la dificultad.

## Interpretación y comportamiento implementado

- La aplicación selecciona siempre la copia mediante Wine para osu!stable en Linux.
- Cada ruta se convierte mediante `winepath`; nunca se construye `Z:` manualmente.
- Si la copia mediante Wine falla, la copia nativa conserva el resultado y la UI recomienda F5.
- Si también falla la copia nativa, se intenta preservar un `.osz` portable en `Failed Imports`, junto al ejecutable de HRandomPlus.
- Los fallos y rutas complejas están **PROBADOS CON MOCKS**; la prueba manual de esta integración completa debe repetirse con cada candidato de release.

The interactive A/B command above prompts on an existing destination: answer No and choose a new name. The application passes paths through environment variables with delayed expansion disabled; this manual command is only for ordinary disposable paths. Do not use it to validate shell metacharacters.

The application reserves the destination name with a separate sidecar and asks Wine to create the final `.osu` with no-overwrite copying. It removes its sidecar after success, failure or cancellation. This preserves unique names between HRandomPlus processes without turning Wine's creation into an overwrite of a Linux-created empty file. The native fallback also creates a new file atomically. Recheck detection without F5 on real Winello after this change; simulated process tests cannot verify Wine filesystem notifications.
