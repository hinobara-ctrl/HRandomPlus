# Auditoría de licencias de dependencias y distribución

Fecha: 2026-08-30

Estado: **DISTRIBUCIÓN v0.2.x TÉCNICAMENTE LISTA**

Este es un inventario técnico, no asesoría legal. Se elaboró a partir de las referencias del proyecto, archivos `project.assets.json` resueltos, manifiestos y licencias de NuGet, distribuciones oficiales de .NET 8.0.30, revisiones de fuentes upstream y publicaciones autocontenidas multifichero para `win-x64` y `linux-x64`.

## Inventario de ejecución

| Dependencia | Versión | Licencia | Plataforma | ¿Redistribuida? | Evidencia/revisión de fuentes |
|---|---:|---|---|---|---|
| Código propio de HRandomPlus | 0.2.1-playtest | GPL-3.0-or-later | Windows/Linux | Sí | `LICENSE` raíz; paquete de fuentes exacto del candidato |
| OsuMemoryDataProvider | 0.12.2 | GPL-3.0-or-later | Windows | **Sí**, integrada | Commit NuGet `122dd102fe272de30471cf1f317805cb49ac23a4`; tag `osu_v0.12.2` |
| ProcessMemoryDataFinder | 0.10.2 | GPL-3.0-or-later | Windows | **Sí**, integrada | Mismo commit; tag `process_v0.10.2` |
| Realm .NET | 20.1.0 | Apache-2.0 | Windows/Linux | **Sí**, ensamblado administrado más biblioteca nativa `realm-wrappers` seleccionada | Commit NuGet `370ce596a0cf5e992b717bb199d70e55391ff2b9` |
| MongoDB.Bson | 2.21.0 | Apache-2.0 | Windows/Linux | **Sí**, integrada | Commit NuGet `5a9c3311e158910b88195f290e6d4b1b2715d2b2` |
| Remotion.Linq | 2.2.0 | Apache-2.0 | Windows/Linux | **Sí**, integrada | Paquete NuGet exacto 2.2.0 |
| Fody / Realm weaver | 6.9.1 / 20.1.0 | MIT / Apache-2.0 | Solo compilación | **Sin recurso de ejecución** | Resuelto, pero desactivado porque la integración usa la API dinámica de Realm |
| Familia de ejecución Avalonia | 12.1.1 | MIT | Windows/Linux | Sí, integrada | Commit/tag NuGet `e33eaed9c106846b200680751022385d9cc5dc6f` / `12.1.1` |
| Avalonia.BuildServices | 11.3.2 | MIT | Solo compilación | **Sin recurso de ejecución** | Presente únicamente en metadatos de restore/dependencias |
| Avalonia.Angle.Windows.Natives | 2.1.27548.20260419 | Archivo de licencia del paquete (términos estilo BSD-3-Clause) | Windows | Sí, `av_libglesv2.dll` | Commit NuGet `1c89805903c1482166356d3b950d474973180e61` |
| SkiaSharp + recursos nativos seleccionados | 3.119.4 | MIT + avisos oficiales de terceros | Windows/Linux | Sí | Commit/tag NuGet `f568ac94dd768ef9a2f593537cfde2dd0d348ef5` / `v3.119.4` |
| HarfBuzzSharp + recursos nativos seleccionados | 8.3.1.3 | MIT + avisos oficiales de terceros | Windows/Linux | Sí | Commit NuGet `2888c737ad016d584c74525e2d35db5097ea8576` |
| MicroCom.Runtime | 0.11.6 | MIT | Windows/Linux | Sí, integrada | Commit NuGet `76785efcafd91b5902fd19dd11145f6dd655b7b4` |
| Tmds.DBus.Protocol | 0.94.1 | MIT | Windows/Linux | Sí, integrada | Commit NuGet `b4a7fed0b878f74cb54f7cca84d2889af4e596ba` |
| System.IO.Pipelines | 8.0.0 | MIT + avisos oficiales de terceros | Windows/Linux | Sí, integrada | Commit NuGet `5535e31a712343a63f5d7d796cd874e563e5ac14` |
| Paquete Microsoft.CSharp | 4.7.0 | MIT | Grafo de restore Windows | **Sin recurso de ejecución del paquete** | La publicación utiliza en su lugar el ensamblado del runtime pack de .NET |
| Paquete System.Data.DataSetExtensions | 4.5.0 | URL de licencia MIT | Grafo de restore Windows | **Sin recurso de ejecución del paquete** | La publicación utiliza en su lugar el ensamblado del runtime pack de .NET |
| Runtime/host de .NET | 8.0.30 | Licencia de biblioteca .NET (Windows); MIT (Linux); avisos oficiales de terceros | Windows/Linux | Sí en autocontenida; no en dependiente del framework | Archivos oficiales del runtime 8.0.30; tag de fuentes `v8.0.30`, commit `a83db3e0eb2defb6220e15dae2f1a0462fdbf99f` |
| Paquetes nativos para RID no relacionados | Versiones resueltas arriba | Varía | Solo metadatos de restore | **No** | Ausentes de los recursos de ejecución seleccionados por plataforma |

## Evidencia de publicación

La publicación de auditoría multifichero para Windows contiene recursos de ejecución de ambos paquetes GPL, Realm, MongoDB.Bson, Remotion.Linq, Avalonia, ANGLE, SkiaSharp, HarfBuzzSharp, MicroCom, Tmds.DBus.Protocol, System.IO.Pipelines y el runtime pack de .NET 8.0.30. El candidato final de archivo único integra los ensamblados administrados y deja bibliotecas nativas como `realm-wrappers.dll`, `av_libglesv2.dll`, `libHarfBuzzSharp.dll` y `libSkiaSharp.dll` junto al ejecutable cuando la configuración de publicación seleccionada no las integra.

La publicación de auditoría para Linux no contiene OsuMemoryDataProvider, ProcessMemoryDataFinder ni recursos nativos de ANGLE para Windows. Stable usa tosu mediante HTTP; lazer nativo usa Realm en modo de solo lectura e incluye el wrapper de Realm para Linux x64. El grafo de dependencias de Avalonia aún aporta ensamblados administrados de plataforma, Tmds.DBus.Protocol y System.IO.Pipelines; las bibliotecas nativas seleccionadas de Realm/Skia/HarfBuzz para Linux y las bibliotecas nativas de .NET están integradas en la publicación final de archivo único.

## Material oficial de licencias

- Texto GPL: `LICENSE` upstream literal del commit `122dd102fe272de30471cf1f317805cb49ac23a4` de ProcessMemoryDataFinder.
- Texto Apache-2.0: `LICENSE.md` literal incluido con MongoDB.Bson 2.21.0; cubre las declaraciones Apache-2.0 de Realm, MongoDB.Bson y Remotion.Linq.
- Licencia/aviso de Avalonia: `licence.md` y `NOTICE.md` literales del commit `e33eaed9c106846b200680751022385d9cc5dc6f`.
- ANGLE: `LICENSE` literal incluido en `Avalonia.Angle.Windows.Natives 2.1.27548.20260419`.
- SkiaSharp/HarfBuzzSharp: `LICENSE.txt` literal y `THIRD-PARTY-NOTICES.txt` idéntico incluidos en sus paquetes NuGet nativos seleccionados.
- MicroCom: `LICENSE` upstream literal del commit `76785efcafd91b5902fd19dd11145f6dd655b7b4`.
- Tmds.DBus.Protocol: el manifiesto NuGet declara SPDX `MIT` y copyright `Tom Deseyn`; la distribución incluye el texto oficial SPDX MIT y conserva la atribución del paquete en `THIRD_PARTY_NOTICES.md`.
- System.IO.Pipelines: `LICENSE.TXT` y `THIRD-PARTY-NOTICES.TXT` literales del paquete 8.0.0.
- .NET: `LICENSE.txt` y `ThirdPartyNotices.txt` literales extraídos por separado de los archivos oficiales de .NET Runtime 8.0.30 para Windows x64 y Linux x64. Sus valores SHA-512 publicados se verificaron con los metadatos de release de Microsoft.

## Correspondencia de fuentes GPL preparada

`HRandomPlus-v0.2.1-playtest-gpl-source.zip` contiene el snapshot completo del repositorio ProcessMemoryDataFinder en el commit `122dd102fe272de30471cf1f317805cb49ac23a4`, no un enlace a una rama móvil. Ese único commit está declarado en ambos manifiestos NuGet y es el destino de ambos tags de release. El paquete incluye todos los archivos del repositorio, material de proyecto/compilación y la licencia GPL upstream, además de un manifiesto de procedencia.

`HRandomPlus-v0.2.1-playtest-source.zip` contiene el árbol de fuentes de HRandomPlus utilizado para compilar los artifacts candidatos.

## Preparación de distribución

- El código propio de HRandomPlus tiene licencia `GPL-3.0-or-later` conforme al `LICENSE` completo de la raíz.
- El ZIP de fuentes de HRandomPlus se genera desde la revisión exacta del repositorio usada para compilar los binarios e incluye la solución, fuentes, pruebas, configuración de compilación/proyecto, configuración de ejemplo, workflow, documentación y material de licencias.
- El ZIP separado de fuentes GPL upstream contiene el commit inmutable correspondiente a ambos paquetes integrados de lectura de memoria.
- Los ZIP binarios de Windows y Linux conservan los avisos de terceros y textos oficiales de licencia aplicables sin incluir fuentes innecesarias del framework.
- `SHA256SUMS.txt` identifica los archivos exactos del conjunto de release.

Tras completar las comprobaciones de compilación, correspondencia de fuentes y contenido de ZIP documentadas en el informe de preparación de release, el candidato de Windows está **TÉCNICAMENTE LISTO PARA PUBLICACIÓN**. Este inventario registra evidencia de ingeniería y no constituye asesoría legal.
