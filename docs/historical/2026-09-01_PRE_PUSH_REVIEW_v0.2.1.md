<!-- document-status: historical -->
> Tipo de documento: evidencia histórica de v0.2.1-playtest; no es autoritativo para el HEAD actual.

# Revisión pre-push de v0.2.1-playtest

Fecha: 2026-09-01

HEAD base: `d5882cd` (`Fix Windows stable and lazer coexistence`)

Estado revisado: working tree posterior a ese commit, todavía sin commit

SDK local: .NET SDK 8.0.419

Targets: `net8.0` y `net8.0-windows`

## Validación automatizada local

- `dotnet restore --locked-mode`: correcto; lockfiles sin cambios. El sandbox sin red emitió únicamente `NU1900` al consultar el feed de vulnerabilidades.
- `dotnet build -c Release --no-restore`: correcto, 0 errores. Advertencias conocidas: `NU1900` por red restringida y `CS9057` porque Avalonia 12.1.1 usa analizadores Roslyn 4.14 con el compilador 4.11 del SDK 8 local.
- `dotnet test -c Release --no-build`: correcto.
- Runner ejecutable: 353 aprobados, 0 fallidos, 0 omitidos.
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
- Se generaron candidatos locales framework-dependent para pruebas: Windows x64 (13.709.019 bytes, SHA-256 `346ab3cad1a0d8cc206496e0dd404d5da991c3bf4aeb2eca5e72a7fd6c3c2e4c`) y Linux x64 (38.381.030 bytes, SHA-256 `d5956d58e3874d3d34ff1fa6b0caf77db80d28221d7d7835b2a0126bd5d223d3`). Ambos ZIP pasaron verificación CRC/contenido y Linux conserva modo ejecutable `0755`.
- Estos ZIP locales sirven para smoke tests, pero no sustituyen los assets definitivos del pipeline. Los source ZIP, GPL source ZIP y checksums del conjunto completo permanecen pendientes de CI.
- No cambiaron dependencias, licencias, lockfiles ni configuración de packaging.

## Verificaciones manuales

Las pruebas manuales de Windows y Linux/VM están completas. En Windows se confirmó una instancia stable, cierre/reapertura, ruta configurada antigua, fallback y alternancia por selección más reciente con stable x86 y lazer x64 abiertos simultáneamente. En Linux se confirmó stable/tosu, lazer, generación e importación. El caso de dos procesos x86 elegibles permanece cubierto de forma automatizada y falla cerrado.

## Limitaciones conocidas

- No existe binding real por PID en la dependencia de memoria actual; la garantía se obtiene filtrando por la misma arquitectura x86 usada por el reader y absteniéndose cuando quedan varios objetivos elegibles.
- Las advertencias `CS9057` dependen del SDK 8 local; CI usa SDK 10 para compilar los mismos targets .NET 8.
- La validación Ubuntu del working tree y los hashes finales sólo existirán después del push/pipeline.
- Pesos de scoring finitos extremadamente grandes continúan registrados como mejora futura no bloqueante.

## Último barrido estático

- No quedan marcadores `TODO`, `FIXME`, `HACK` o `XXX`, rutas absolutas de desarrollo, archivos accidentales ni artefactos versionados.
- Los `catch` silenciosos restantes pertenecen a cleanup/logging best-effort, probing de procesos/rutas o cancelación esperada; los flujos principales devuelven estado o diagnóstico.
- Los lockfiles idénticos de CLI/tests y los dos `AssemblyInfo.cs` idénticos son archivos por proyecto requeridos, no duplicados accidentales.
- Defaults, perfiles, `config.example.json`, targets, paquetes, versión canónica y nombres de artefactos son consistentes.
- Se corrigió la cifra vigente obsoleta del documento histórico `HARDENING_v0.2.1.md` y se renombró una regresión del selector para no afirmar que simula la clasificación real de procesos; stable + lazer se verificó manualmente en Windows.
- Se alinearon el registro Linux, las notas de release y el documento de perfiles con los playtests ya cerrados; los únicos casos sin ejecutar son fixtures excepcionales P2 declarados no bloqueantes.
- La escritura final de `.osz` con overwrite ya no borra primero el destino: mueve el candidato validado mediante la sobrecarga de reemplazo y cuenta con una regresión que abre el ZIP resultante.
- `.gitignore` cubre también residuos estándar de Visual Studio, JetBrains, VS Code, TestResults, cobertura y archivos del sistema.
- No se detectaron otros bugs ni riesgos de regresión pequeños que justificaran cambios de producción. Las posibles mejoras de arquitectura o estilo quedaron fuera del diff.

## Estado

READY FOR PUSH. La publicación de la release debe esperar el pipeline Windows/Ubuntu y la generación de los artefactos/checksums de esa revisión.
