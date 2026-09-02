# Revisión pre-push de v0.2.1-playtest

Fecha: 2026-09-01

HEAD base: `d5c0d43` (`Finalize v0.2.1 hardening and release consistency`)

Estado revisado: working tree posterior a ese commit, todavía sin commit

SDK local: .NET SDK 8.0.419

Targets: `net8.0` y `net8.0-windows`

## Validación automatizada local

- `dotnet restore --locked-mode`: correcto; lockfiles sin cambios. El sandbox sin red emitió únicamente `NU1900` al consultar el feed de vulnerabilidades.
- `dotnet build -c Release --no-restore`: correcto, 0 errores. Advertencias conocidas: `NU1900` por red restringida y `CS9057` porque Avalonia 12.1.1 usa analizadores Roslyn 4.14 con el compilador 4.11 del SDK 8 local.
- `dotnet test -c Release --no-build`: correcto.
- Runner ejecutable: 352 aprobados, 0 fallidos, 0 omitidos.
- `git diff --check`: correcto.
- Sistema ejecutor: Windows x64.

El commit base ya había pasado CI en Windows y Ubuntu. El working tree actual incluye cambios posteriores; su ejecución en Ubuntu queda pendiente del pipeline que se iniciará después del push. No se presenta como ya ejecutada.

## Identidad de osu!stable en Windows

`OsuMemoryDataProvider 0.12.2` no ofrece binding por PID: sus constructores reciben `ProcessTargetOptions`, que sólo expone nombre, título de ventana y arquitectura. La integración adopta por ello una política fail-closed:

- replica el filtro x86 de `ProcessTargetOptions(..., Target64Bit: false)`, por lo que lazer x64 no crea una falsa ambigüedad;
- sólo crea/usa el reader cuando existe un único proceso x86 elegible como stable;
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
- Se generaron candidatos locales framework-dependent para pruebas: Windows x64 (13.709.026 bytes, SHA-256 `b84539b948fa06e7f4480a7bc2a21accad0af2fa33cbe379c51d103347d4580d`) y Linux x64 (38.381.030 bytes, SHA-256 `b0f26b4f67edd797ef767126a3bf7ac6fd0b035722f2a46195346076cd00b1ea`). Ambos ZIP pasaron verificación CRC/contenido y Linux conserva modo ejecutable `0755`.
- Estos ZIP locales sirven para smoke tests, pero no sustituyen los assets definitivos del pipeline. Los source ZIP, GPL source ZIP y checksums del conjunto completo permanecen pendientes de CI.
- No cambiaron dependencias, licencias, lockfiles ni configuración de packaging.

## Verificaciones manuales

Las pruebas manuales de Windows y Linux/VM están completas. En Windows se confirmó una instancia stable, cierre/reapertura, ruta configurada antigua, fallback y alternancia por selección más reciente con stable x86 y lazer x64 abiertos simultáneamente. En Linux se confirmó stable/tosu, lazer, generación e importación. El caso de dos procesos x86 elegibles permanece cubierto de forma automatizada y falla cerrado.

## Limitaciones conocidas

- No existe binding real por PID en la dependencia de memoria actual; la garantía se obtiene filtrando por la misma arquitectura x86 usada por el reader y absteniéndose cuando quedan varios objetivos elegibles.
- Las advertencias `CS9057` dependen del SDK 8 local; CI usa SDK 10 para compilar los mismos targets .NET 8.
- La validación Ubuntu del working tree y los hashes finales sólo existirán después del push/pipeline.
- Pesos de scoring finitos extremadamente grandes continúan registrados como mejora futura no bloqueante.

## Estado

READY FOR PUSH. La publicación de la release debe esperar el pipeline Windows/Ubuntu y la generación de los artefactos/checksums de esa revisión.
