# HRandomPlus v0.1.1-playtest

Esta es una actualización conservadora de empaquetado para osu!stable. No cambia la randomización, detección de beatmaps, integración con tosu, importación mediante Wine, perfiles, configuración predeterminada ni comportamiento de la UI.

## Distribución

- `HRandomPlus-v0.1.1-playtest-windows-x64.zip` y `HRandomPlus-v0.1.1-playtest-linux-x64.zip` son las descargas autocontenidas recomendadas y no requieren una instalación separada de .NET.
- `HRandomPlus-v0.1.1-playtest-windows-x64-framework-dependent.zip` y `HRandomPlus-v0.1.1-playtest-linux-x64-framework-dependent.zip` son descargas opcionales más pequeñas. Requieren .NET 8 x64 Runtime y reducen la descarga aproximadamente un 72 % en Windows y un 76 % en Linux según el estudio controlado.
- La build Linux x64 dependiente del framework aprobó su smoke test final en una instalación Linux real con .NET 8 x64 Runtime.
- Los símbolos de depuración, pruebas, archivos de compilación, cachés y archivos temporales se excluyen de los ZIP binarios.
- Se evaluaron la compresión de payload de archivo único, trimming, globalización invariante y eliminación de backends de Avalonia, pero no se adoptaron porque el beneficio medido no justificaba su costo de ejecución o compatibilidad.

Las mediciones y decisiones completas están registradas en `docs/optimization-v0.1.1.md`.

## Licencias y fuentes

HRandomPlus permanece bajo `GPL-3.0-or-later`. Los componentes de terceros conservan sus licencias y avisos. El conjunto de release incluye las fuentes exactas de HRandomPlus, el snapshot exacto de fuentes GPL upstream requerido por las dependencias de lectura de memoria en Windows y `SHA256SUMS.txt`.

Los ZIP autocontenidos redistribuyen .NET 8 e incluyen la licencia y avisos aplicables del runtime. Los ZIP dependientes del framework requieren un runtime .NET 8 instalado por separado y, por ello, no incluyen sus archivos de licencia; todos los demás avisos aplicables siguen incluidos.

## Alcance conocido

Esta release admite osu!stable. osu!lazer queda reservado para v0.2.0.
