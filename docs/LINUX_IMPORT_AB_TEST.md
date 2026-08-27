# Prueba A/B de importación Linux

Estado: **NOT TESTED ON REAL WINE**.

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
cp -- "$SOURCE" "$DEST"
```

Espera unos segundos sin pulsar F5 y anota si aparece la dificultad.

## Prueba B: copia desde el entorno Wine

Borra primero la dificultad de prueba desde osu! o usa otro nombre único. Convierte las rutas con Wine; no escribas letras `Z:` manualmente:

```bash
SOURCE_WIN="$(osu-wine --wine winepath -w "$SOURCE" | tr -d '\r')"
DEST_WIN="$(osu-wine --wine winepath -w "$DEST" | tr -d '\r')"
osu-wine --wine cmd /d /c copy /y "$SOURCE_WIN" "$DEST_WIN"
```

Espera unos segundos sin pulsar F5 y anota si aparece la dificultad.

## Interpretación

- Si B funciona y A no: habilitar una estrategia de copia Wine-side.
- Si ninguna funciona: validar la estrategia `.osz` preparada mediante `osu-wine --osuhandler archivo.osz`.
- Si falla cualquier importación, el `.osu` generado debe permanecer disponible y F5/importación manual debe seguir funcionando.
