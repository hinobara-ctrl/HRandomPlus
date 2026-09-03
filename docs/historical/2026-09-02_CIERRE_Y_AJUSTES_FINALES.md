<!-- document-status: historical -->
> Tipo de documento: registro puntual de los cambios realizados después de `2026-09-02_CAMBIOS_DESDE_ULTIMO_PUSH.md`. No sustituye la documentación vigente de `docs/current/`.

# Cierre y ajustes finales posteriores

## Resumen

Después del informe anterior se cerró la reorganización del repositorio, se amplió la cobertura del sistema de entrega de beatmaps y se corrigieron dos detalles descubiertos durante los smoke tests: la regeneración de una dificultad ya generada y los controles de seed. No se cambiaron el scoring, el RNG, los perfiles integrados ni las reglas del randomizador.

## Cierre técnico y documental

- La documentación quedó separada en `current/`, `historical/`, `releases/` y `templates/`.
- Los documentos históricos se fecharon usando el historial de Git o una fecha explícita del propio documento; no se usó el `mtime` local.
- `docs/README.md` quedó como índice y aclara que la documentación histórica no es autoritativa para HEAD.
- README, workflow, checklists y scripts fueron actualizados para sus nuevas rutas.
- El consistency checker pasó a recorrer `docs/` de forma recursiva y validó 24 documentos clasificados antes de este registro.
- La búsqueda de enlaces Markdown revisó 26 archivos sin encontrar enlaces rotos.
- `.gitignore` se amplió para outputs de build, benchmark, tests, cobertura, logs, dumps, staging, `Failed Imports`, archivos comprimidos y temporales.
- Se retiraron `artifacts/`, `bin/`, `obj/`, staging y otros outputs generados durante el cierre; posteriormente `artifacts/` se regeneró únicamente para los smoke tests solicitados.
- El README declara brevemente que HRandomPlus está completo para su propósito actual y entra en modo mantenimiento.

## Cobertura de salida y fallback

Se añadieron regresiones automatizadas para confirmar que:

- configuraciones antiguas con `OutputToBeatmapFolder` siguen cargando y el campo desaparece al volver a guardar;
- la copia nativa junto al beatmap nunca sobrescribe una dificultad existente;
- el fallback portable crea un `.osz` válido con nombre incremental;
- `Failed Imports` no se incluye a sí mismo ni sobrescribe archivos anteriores;
- un nombre de recurso con traversal se rechaza y el error original se conserva;
- el fallback de lazer neutraliza `BeatmapID` y `BeatmapSetID`;
- stable vuelve a un estado seleccionable después de cerrar el segundo proceso compatible.

También se sustituyeron nombres personales usados en fixtures sintéticos por valores genéricos. Esto no modificó el comportamiento probado.

## Regeneración de dificultades ya generadas

Un smoke test reveló que, si stable seleccionaba el `.osu` recién creado y el usuario volvía a generar, el filename podía terminar en `CUSTOM CUSTOM` mientras `Version:` repetía el nombre anterior.

La generación ahora reconoce únicamente el patrón exacto producido por HRandomPlus y recupera su base antes de buscar el siguiente nombre libre. La secuencia queda alineada en el filename y en `Version:`:

```text
CUSTOM
CUSTOM 2
CUSTOM 3
```

Se añadió una regresión que genera desde el original, vuelve a generar desde el primer resultado y repite desde el segundo. La protección contra overwrite se mantiene.

## Controles de seed

La fila de seed quedó así:

```text
Generate Seed | Hold Seed | Delete Seed
```

- `Generate Seed` conserva la función anterior de crear y escribir una seed fija nueva.
- `Hold Seed` se habilita después de una generación y copia al campo la última seed realmente utilizada.
- La última seed permanece disponible al cambiar de perfil durante la misma sesión.
- `Delete Seed` vacía el campo y devuelve la próxima generación al modo aleatorio.
- Borrar el campo no elimina el último valor recordado; `Hold Seed` puede recuperarlo.
- El modo automático mantiene el campo vacío y muestra la última seed usada mediante el placeholder.

## Comprobaciones manuales aclaradas

### Linux con osu-winello cerrado

Cerrar osu-winello hizo fallar la copia Wine con `Path not found`, pero la copia nativa funcionó y eliminó el staging. Por ello no apareció un `.osz` en `Failed Imports`: ese fallback final sólo se activa si también falla la copia nativa. La prueba confirmó correctamente la recuperación intermedia y no reveló un bug.

### Selected range

El log confirmó que la UI envió `Selected range 37005-73005 ms`. Se compararon los dos `.osu` usados en el smoke test:

- 4.897 notas totales;
- 688 notas dentro del rango;
- 595 cambios de columna dentro del rango;
- 0 cambios antes del rango;
- 0 cambios después del rango.

El comportamiento era correcto. La impresión contraria se produjo porque el input ya era una dificultad randomizada: fuera del rango se conserva el estado del archivo seleccionado, no se reconstruye la dificultad original.

## Validación más reciente

- Restore locked: correcto.
- Build Release Windows/Linux: correcto.
- Suite: 363 pruebas aprobadas, 0 fallidas.
- Baselines deterministas: conservadas.
- `git diff --check`: correcto; sólo avisos informativos LF/CRLF.
- Warnings conocidos: `NU1900` por acceso al feed de vulnerabilidades y `CS9057` por la versión de los analizadores de Avalonia frente al compilador local.

Artefactos de smoke test actuales, ignorados por Git:

| Archivo | Tamaño | SHA-256 |
| --- | ---: | --- |
| `HRandomPlus-v0.2.1-playtest-windows-x64-framework-dependent.zip` | 13.714.782 bytes | `e177a6db0497fe61c5eaa6540bec65e77d1c6dce15191ca7cc58aea94c8c0cb8` |
| `HRandomPlus-v0.2.1-playtest-linux-x64-framework-dependent.zip` | 14.078.221 bytes | `e7f158e18268db7e688facca0ff9491df826f202fa6d8866fb66e332b7c32218` |

No se realizó commit, push, tag ni Release.
