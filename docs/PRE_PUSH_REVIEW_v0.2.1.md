# Revisión pre-push de v0.2.1-playtest

Fecha: 2026-09-01

HEAD base: `169789992ca43a246904f9c3d990b48dfaf7f8ce` (`Fix final v0.2.1 edge cases and release consistency`)

Estado revisado: working tree posterior a ese commit, todavía sin commit

SDK local: .NET SDK 8.0.419

Targets: `net8.0` y `net8.0-windows`

## Validación automatizada local

- `dotnet restore --locked-mode`: correcto; lockfiles sin cambios. El sandbox sin red emitió únicamente `NU1900` al consultar el feed de vulnerabilidades.
- `dotnet build -c Release --no-restore`: correcto, 0 errores. Advertencias conocidas: `NU1900` por red restringida y `CS9057` porque Avalonia 12.1.1 usa analizadores Roslyn 4.14 con el compilador 4.11 del SDK 8 local.
- `dotnet test -c Release --no-build`: correcto.
- Runner ejecutable: 351 aprobados, 0 fallidos, 0 omitidos.
- `git diff --check`: correcto.
- Sistema ejecutor: Windows x64.

El commit base ya había pasado CI en Windows y Ubuntu. El working tree actual incluye cambios posteriores; su ejecución en Ubuntu queda pendiente del pipeline que se iniciará después del push. No se presenta como ya ejecutada.

## Identidad de osu!stable en Windows

`OsuMemoryDataProvider 0.12.2` no ofrece binding por PID: sus constructores reciben `ProcessTargetOptions`, que sólo expone nombre, título de ventana y arquitectura. La integración adopta por ello una política fail-closed:

- sólo crea/usa el reader cuando existe un único proceso objetivo llamado `osu!`;
- conserva una identidad formada por PID, hora de inicio y directorio ejecutable;
- valida esa identidad antes y después de leer memoria;
- invalida y dispone el reader al terminar, reutilizar PID o cambiar de instancia;
- deriva `Songs` de la misma identidad, nunca de una segunda consulta a `settings.OsuPath`;
- ante ambigüedad no lee memoria y solicita cerrar instancias o seleccionar un `.osu` manualmente.

## Generación stable preservada

Las regresiones confirman que el original permanece intacto, el output usa otra ruta y un nombre único, `BeatmapID` se establece en `0`, una colisión no se sobrescribe y un cambio concurrente del original aborta antes de escribir salida.

## Timeout de procesos

Después de timeout o cancelación, `SystemProcessRunner` intenta terminar el árbol y espera como máximo dos segundos adicionales. Si la terminación falla, devuelve timeout o propaga la cancelación sin espera indefinida. stdout/stderr completos se conservan cuando el proceso alcanza a salir; si permanece vivo, sólo se devuelve contenido ya completado.

## Workflow y empaquetado

- `.github/workflows/build.yml` conserva restore locked, pruebas Windows/Ubuntu y publicaciones framework-dependent para `win-x64`/`linux-x64`.
- La versión de nombres de ZIP, source, GPL source y release candidate se lee de `Directory.Build.props`; no quedan nombres `v0.2.1-playtest` hardcodeados en el workflow.
- El YAML fue parseado correctamente y contiene los cinco jobs esperados.
- Política vigente: dos ZIP binarios framework-dependent, source ZIP, GPL source ZIP y `SHA256SUMS.txt`.
- Se generaron candidatos locales framework-dependent para pruebas: Windows x64 (13.708.840 bytes, SHA-256 `27b743f490f3a6c8ca4144c1e111cbcc2a47d223ee786f493fe6e4fee622abff`) y Linux x64 (38.380.993 bytes, SHA-256 `68ec06b06a9b90fffdce2085b19035f47f66ee54f01e501ee864215f2ab1425f`). Ambos ZIP pasaron verificación CRC/contenido y Linux conserva modo ejecutable `0755`.
- Estos ZIP locales sirven para smoke tests, pero no sustituyen los assets definitivos del pipeline. Los source ZIP, GPL source ZIP y checksums del conjunto completo permanecen pendientes de CI.
- No cambiaron dependencias, licencias, lockfiles ni configuración de packaging.

## Verificaciones manuales

Las pruebas manuales históricas documentadas de Windows y Linux/VM permanecen válidas como línea base. Esta pasada no repitió un smoke test con un proceso osu!stable real ni ejecutó Linux; no se inventa ese resultado. Antes de publicar la release conviene confirmar en Windows: una sola instancia stable detecta el mapa, dos procesos `osu!` producen ambigüedad y el fallback manual sigue operativo.

## Limitaciones conocidas

- No existe binding real por PID en la dependencia de memoria actual; la garantía se obtiene absteniéndose cuando el nombre de proceso no identifica un objetivo único.
- Las advertencias `CS9057` dependen del SDK 8 local; CI usa SDK 10 para compilar los mismos targets .NET 8.
- La validación Ubuntu del working tree y los hashes finales sólo existirán después del push/pipeline.
- Pesos de scoring finitos extremadamente grandes continúan registrados como mejora futura no bloqueante.

## Estado

READY FOR PUSH. La publicación de la release debe esperar el pipeline Windows/Ubuntu y la generación de los artefactos/checksums de esa revisión.
