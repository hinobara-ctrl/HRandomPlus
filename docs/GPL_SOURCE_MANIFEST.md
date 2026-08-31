# Manifiesto de fuentes correspondientes GPL

Candidato de release: `v0.2.1-playtest`

Este manifiesto vincula todo el código cubierto por GPL del candidato Windows x64 con sus fuentes correspondientes.

## Fuentes de HRandomPlus

HRandomPlus `v0.2.1-playtest` se distribuye bajo `GPL-3.0-or-later`. `HRandomPlus-v0.2.1-playtest-source.zip` se genera desde la misma revisión del repositorio usada por los jobs binarios y contiene la solución, todas las fuentes de la aplicación y pruebas, archivos de proyecto/compilación, `NuGet.Config`, configuración de ejemplo, workflow, documentación y material de licencias. Se excluyen outputs generados, cachés de paquetes, configuración personal y beatmaps.

Los comandos de recompilación están documentados en `README.md`; los comandos automatizados autoritativos están en `.github/workflows/build.yml`.

## Correspondencia de paquetes

| Paquete NuGet | Versión | SHA-256 del paquete | Licencia | Tag upstream | Commit exacto |
|---|---:|---|---|---|---|
| OsuMemoryDataProvider | 0.12.2 | `739f03b7db1510887a6266532a8e0dda2ebb56d3ee0c9f8172dab60cc42745fc` | GPL-3.0-or-later | `osu_v0.12.2` | `122dd102fe272de30471cf1f317805cb49ac23a4` |
| ProcessMemoryDataFinder | 0.10.2 | `ae25ddc53bb6ced73c975d045e79a52050cdadbe7144966f19bd8fc22e8dd9b4` | GPL-3.0-or-later | `process_v0.10.2` | `122dd102fe272de30471cf1f317805cb49ac23a4` |

Ambos tags anotados resuelven al mismo commit declarado en los dos manifiestos NuGet.

## Snapshot de fuentes incluido

- Repositorio: `https://github.com/Piotrekol/ProcessMemoryDataFinder`
- URL inmutable de fuentes: `https://github.com/Piotrekol/ProcessMemoryDataFinder/tree/122dd102fe272de30471cf1f317805cb49ac23a4`
- URL oficial del archivo: `https://codeload.github.com/Piotrekol/ProcessMemoryDataFinder/zip/122dd102fe272de30471cf1f317805cb49ac23a4`
- SHA-256 del archivo descargado: `9872dd7c18a1a8a4ec16b8d66b409f377dda9b6974057a9a889fd5c73fad0535`
- SHA-512 del archivo descargado: `b69f4cf66b7d4895b9f629d698debc080628530e711be419fe106a983268cd2d9d5f0324668a9f443b934eea255d205fd0af0f7894633c011c964cf10c0a059e`

`HRandomPlus-v0.2.1-playtest-gpl-source.zip` expande ese snapshot completo del repositorio y coloca este manifiesto junto a él. El snapshot incluye los proyectos de ambos paquetes, fuentes compartidas, archivos de solución/compilación y la licencia GPL upstream.

## Conjunto de distribución

Las dos variantes binarias de Windows, las variantes binarias de Linux, el ZIP de fuentes de HRandomPlus, el ZIP de fuentes GPL upstream y `SHA256SUMS.txt` forman un único conjunto de release. No publiques ningún binario de Windows sin ambos archivos de fuentes correspondientes y el manifiesto de checksums de la misma ejecución candidata.
