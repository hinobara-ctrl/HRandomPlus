# Estudio de tamaño y oportunidades de optimización — v0.2.1

> Documento histórico de medición. La política vigente distribuye únicamente los dos paquetes x64 framework-dependent; las variantes self-contained citadas aquí ya no son descargas normales.

Fecha: 2026-08-30

Este estudio compara los paquetes históricos medidos de v0.1.x y v0.2.0 con una publicación local nueva del commit `ef9a30f`. No cambia código ni comportamiento. Las cifras actuales se obtuvieron con .NET SDK 8.0.424 en Windows; GitHub Actions usa SDK 10 y puede producir diferencias pequeñas de bytes.

## Evolución del tamaño descargado

| Variante | v0.1.0/v0.1.1 | v0.2.0 | v0.2.1 actual | Cambio desde v0.2.0 |
|---|---:|---:|---:|---:|
| Windows x64 autocontenida | 42.122.507 B | 47.897.994 B | 44.209.146 B | −3.688.848 B (−7,70 %) |
| Linux x64 autocontenida | 39.610.618 B | 47.944.503 B | 44.650.499 B | −3.294.004 B (−6,87 %) |
| Windows x64 dependiente de .NET | 11.611.873 B | 13.698.036 B | 13.696.879 B | −1.157 B (−0,01 %) |
| Linux x64 dependiente de .NET | 9.388.588 B | 14.430.761 B | 14.429.920 B | −841 B (−0,01 %) |

La comparación v0.1.x → v0.2.1 no representa crecimiento accidental: v0.2 incorporó soporte nativo de lazer y, con él, Realm, MongoDB.Bson y bibliotecas nativas. Frente a la base autocontenida v0.1.0, v0.2.1 creció 4,95 % en Windows y 12,72 % en Linux. Frente a v0.2.0, la publicación de archivo único actual recupera aproximadamente 7 % del ZIP sin eliminar funciones.

## Tamaño instalado actual

| Variante | Tamaño instalado | Archivo principal | Archivos |
|---|---:|---:|---:|
| Windows autocontenida | 101.340.338 B | 78.180.616 B | 22 |
| Windows dependiente de .NET | 33.911.144 B | 10.857.118 B | 20 |
| Linux autocontenida | 105.140.726 B | 104.747.068 B | 16 |
| Linux dependiente de .NET | 38.353.529 B | 38.055.342 B | 14 |

Elegir la variante dependiente de .NET reduce el ZIP un 69,02 % en Windows y un 67,68 % en Linux; reduce el tamaño instalado un 66,54 % y un 63,52 %, respectivamente. Es la optimización de espacio más grande disponible, pero traslada al usuario el requisito de instalar .NET 8 x64. Por ello debe seguir siendo opcional.

En Windows, los mayores componentes externos son `libSkiaSharp.dll` (11.628.896 B), `av_libglesv2.dll` (5.394.096 B), `realm-wrappers.dll` (3.879.936 B) y `libHarfBuzzSharp.dll` (1.816.088 B). En Linux, las bibliotecas integradas equivalentes incluyen Realm (13.473.392 B), SkiaSharp (11.170.296 B) y HarfBuzzSharp (2.808.040 B). Son dependencias funcionales del almacenamiento de lazer y del renderer; no son duplicados.

## Inicio, memoria y CPU en Windows

Se midieron cinco inicios secuenciales del binario autocontenido actual. La primera observación fue en frío y las siguientes cuatro fueron estables.

| Métrica | v0.1.0 histórica | v0.2.1 actual |
|---|---:|---:|
| Primera observación en frío | 7.304,7 ms | 4.446,2 ms |
| Mediana de inicio | 1.090,8 ms | 1.316,0 ms |
| Working set promedio tras 2 s | 250.349.861 B | 176.562.995 B |
| Bytes privados promedio | 212.834.011 B | 129.974.272 B |
| CPU promedio durante 5 s en reposo | no medida en la misma ventana; antes 375 ms/10 s | 421,9 ms/5 s |

La memoria observada disminuyó con claridad, pero el inicio en caliente fue unos 225 ms más lento y el trabajo de CPU en reposo fue mayor. Esta comparación es indicativa: comparte host, pero no una imagen idéntica del SO, caché, antivirus ni estado de procesos. Antes de optimizar se debe perfilar el ciclo de detección actual, especialmente con ningún juego abierto.

## Estructura del código

El árbol contiene 41 archivos C# de producción con 4.497 líneas y 11 archivos de pruebas con 2.352 líneas. Los archivos más grandes son `MainWindow.cs` (811 líneas), `BeatmapImporters.cs` (498), `ProfileCatalog.cs` (414), `OsuArchive.cs` (273) y `OsuBeatmapDocument.cs` (263).

La separación funcional Core / Integration / Desktop / CLI es correcta. Hay dos mejoras estructurales razonables, pero no reducen materialmente el binario:

1. Extraer de `MainWindow` un coordinador de detección y controladores de perfiles/generación. Mejoraría pruebas y mantenimiento, pero una refactorización debe preservar exactamente el flujo de UI.
2. Mover físicamente el código enlazado desde `src/HRandomPlus/` a `src/HRandomPlus.Core/`. Eliminaría la estructura de archivos enlazados del `.csproj` y haría más clara la propiedad del código, sin beneficio de ejecución y con bastante churn de Git.

Separar la integración de lazer/Realm en un módulo opcional reduciría una distribución exclusiva de stable, sobre todo en Linux. Sin embargo, fragmentaría la experiencia, multiplicaría paquetes y exigiría un contrato de carga de plugins o un proceso auxiliar. No se recomienda mientras la distribución única stable+lazer sea el objetivo.

## Procesos y uso de recursos

La UI coordina detección con un `PeriodicTimer` de 200 ms. Cuando ambos orígenes están habilitados, el arbitraje consulta stable y lazer en paralelo. Si no hay juegos abiertos, esto puede enumerar procesos repetidamente; en Linux también puede consultar tosu. El monitor de logs ya lee solo bytes nuevos, Realm se mantiene en caché entre selecciones y las operaciones de archivo están acotadas y por streaming.

La optimización con mejor relación beneficio/riesgo para investigar es una caché corta del descubrimiento negativo de procesos, por ejemplo 750–1.000 ms cuando no hay osu!, conservando sondeo rápido una vez detectado. Debe medirse antes/después y comprobar transición cerrado → abierto en Windows y Linux. No conviene aumentar globalmente el timer de 200 ms porque empeoraría la respuesta cuando el juego está activo.

También puede evaluarse evitar una consulta HTTP a tosu cuando se sabe que la plataforma o el modo activo no la necesita. Cualquier cambio debe mantener reconexión, arbitraje stable/lazer y selección manual; las pruebas funcionales Linux quedarían a cargo del propietario.

## Opciones de espacio evaluadas

| Opción | Beneficio esperado | Riesgo/costo | Decisión |
|---|---|---|---|
| Mantener variantes dependientes de .NET | 64–69 % menos | Requiere .NET 8 x64 | CONSERVAR como opcional |
| Excluir PDB de ZIP finales | Más de 100 MB en staging Windows | Ninguno para usuarios | OBLIGATORIO; ya aplicado |
| Archivo único actual | ~7 % menos ZIP que v0.2.0 multifichero | Extracción interna administrada por .NET | CONSERVAR |
| `EnableCompressionInSingleFile` | ~2 % adicional según v0.1.1 | Peor inicio y memoria | RECHAZAR |
| Trimming | Ahorro incierto | Alto riesgo con Avalonia, Realm dinámico y reflection | RECHAZAR por ahora |
| NativeAOT / ReadyToRun | Puede mejorar inicio; suele aumentar tamaño | Compatibilidad y mantenimiento altos | NO JUSTIFICADO |
| Paquetes Avalonia específicos por plataforma | ~1–1,5 MB instalados | Matriz nativa y arranque más complejos | NO JUSTIFICADO |
| Separar lazer/Realm como plugin | Ahorro importante para stable-only | Fragmenta distribución y arquitectura | FUTURO solo si se exige build stable-only |

## Conclusión

No hay archivos redundantes dentro de los cuatro paquetes finales. El aumento frente a v0.1.x corresponde principalmente al soporte lazer/Realm, no al código propio ni a residuos. El empaquetado actual ya recuperó alrededor de 7 % frente a v0.2.0 y los paquetes dependientes de .NET ofrecen la alternativa compacta.

La siguiente fase útil no es trimming ni una refactorización amplia: es perfilar y, si los datos lo confirman, reducir el sondeo de procesos cuando no hay ningún juego abierto. Después puede dividirse `MainWindow` por mantenibilidad, sin presentarlo como una optimización de tamaño o rendimiento.
