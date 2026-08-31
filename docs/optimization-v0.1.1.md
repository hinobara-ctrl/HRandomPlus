# Estudio de optimización de HRandomPlus v0.1.1

Fecha de inicio: 2026-08-30

Revisión de referencia: `06d9bd5` (`Prepare GPL v0.1.0 playtest release`)

## Alcance y reglas

Este es un estudio conservador que prioriza las mediciones. `v0.1.0-playtest` permanece como referencia estable y `v0.2.0` queda reservada para osu!lazer. Los experimentos están aislados. Un cambio solo se acepta si aporta un beneficio medible, conserva el comportamiento observable y el output reproducible, supera las pruebas existentes y tiene un costo de mantenimiento razonable.

Los paquetes autocontenidos siguen siendo la distribución principal. Los paquetes dependientes del framework solo pueden añadirse como variantes opcionales. Las licencias, avisos y assets de fuentes correspondientes nunca se eliminan para ahorrar espacio.

## Método de medición

- Las mediciones binarias usan los ZIP finales de `v0.1.0-playtest` en `outputs/`, no una compilación nueva.
- El tamaño instalado es la suma de las longitudes sin comprimir de los archivos de cada ZIP.
- El inicio en Windows se mide desde `Process.Start` hasta el primer handle responsivo de la ventana principal. Siete ejecuciones usan directorios temporales separados. Un intento de sobrescribir `LOCALAPPDATA` por proceso no redirigió la API de carpetas especiales de Windows, por lo que las ejecuciones compartieron el estado existente de HRandomPlus para el usuario. La primera se conserva como observación en frío; el inicio normal informado es la mediana del conjunto completo y representa las seis ejecuciones en caliente.
- La RAM en reposo se muestrea dos segundos después de que la ventana responde. Una ejecución estabilizada separada mide memoria tras cinco segundos y CPU durante los diez segundos siguientes.
- La generación usa la ruta real de archivos de `HRandomPlus.Cli` y el parser/randomizador de producción con seed `123456`. Los mapas `.osz` deterministas 7K contienen 500, 5.000 y 50.000 notas separadas por 20 ms. Se descarta un calentamiento y se miden seis ejecuciones. Los tiempos incluyen inicio de CLI, lectura/escritura del archivo, parsing, randomización y escritura del informe.
- Los valores dependen de la máquina y sirven para comparar antes/después en el mismo host, no como afirmaciones universales de rendimiento.

## Base: v0.1.0-playtest

### Tamaño de paquetes

| Plataforma/build | Archivo de aplicación | Directorio instalado | ZIP | Archivos | Estado |
|---|---:|---:|---:|---:|---|
| Windows x64 autocontenida | 76,998,317 B | 96,262,053 B | 42,122,507 B | 20 | Referencia |
| Linux x64 autocontenida | 90,100,511 B | 90,478,119 B | 39,610,618 B | 15 | Referencia |

Los archivos externos de ejecución más grandes de Windows son `libSkiaSharp.dll` (11.628.896 B), `av_libglesv2.dll` (5.394.096 B) y `libHarfBuzzSharp.dll` (1.816.088 B). Linux integra las bibliotecas nativas seleccionadas en el archivo único de la aplicación.

### Inicio y memoria en reposo en Windows

| Métrica | Base |
|---|---:|
| Primera observación en frío | 7,304.7 ms |
| Mediana de inicio, 7 ejecuciones | 1,090.8 ms |
| Rango en caliente, excluyendo la primera | 1,064.0–1,143.8 ms |
| Working set promedio, 7 ejecuciones | 250,349,861 B |
| Bytes privados promedio, 7 ejecuciones | 212,834,011 B |
| Working set estabilizado tras 5 s | 227,561,472 B |
| Bytes privados estabilizados tras 5 s | 185,655,296 B |
| CPU consumida durante los 10 s siguientes | 375 ms, aproximadamente 3,75 % de un núcleo lógico |

La primera observación en frío es atípica y puede incluir efectos del SO, antivirus o extracción/caché de archivo único. No debe compararse directamente con un experimento en caliente.

### Base de generación en Windows

| Mapa | Notas | `.osz` de entrada | Tiempo mediano | Rango | Mediana del working set máximo |
|---|---:|---:|---:|---:|---:|
| Pequeño | 500 | 2,191 B | 181.47 ms | 169.96–186.06 ms | 38,604,800 B |
| Normal | 5,000 | 17,488 B | 294.41 ms | 278.82–311.79 ms | 63,352,832 B |
| Grande | 50,000 | 171,333 B | 1,108.66 ms | 1,076.81–1,154.22 ms | 182,464,512 B |

Estas cifras por sí solas no justifican optimizar el motor. El mapa sintético grande termina en aproximadamente 1,1 segundos incluyendo el overhead del proceso y archivo. Solo se trabajará en parser/caché si un perfilado posterior demuestra trabajo repetido significativo en una operación real.

### Verificación de ejecución en Linux

La base de tamaño de paquetes Linux está completa. El candidato dependiente del framework se sometió después a smoke test en una instalación Linux real con .NET 8 x64 Runtime. Inicio, UI, detección, selección manual, H-Random, S-Random, Custom, generación, reproducibilidad con seed fija, perfiles y output/importación fueron aprobados. No se dedujo ninguna métrica nueva de rendimiento de esa prueba funcional.

## Comparación de experimentos

| Base | Experimento | Diferencia | Resultado |
|---|---|---|---|
| Paquete autocontenido | Dependiente del framework | ZIP Windows −72.39%; ZIP Linux −76.26%; inicio en caliente Windows −11.4% | ACEPTADO solo como opcional |
| Payload de archivo único sin comprimir | `EnableCompressionInSingleFile=true` | ZIP Windows −1.73%, inicio en caliente +9.0%, working set +13.3%; ZIP Linux −2.25% | RECHAZADO |
| Contenido actual de publicación | Auditoría de archivos de depuración/desarrollo | Los ZIP finales ya no contienen PDB, docs XML, pruebas, temporales ni archivos de compilación | BASE ACEPTADA; sin cambios |
| Grafo actual de Avalonia | Revisión de backends de plataforma | Los backends ajenos ocupan como máximo ~1,5 MB instalados por plataforma y exigen cambios de arranque específicos | RECHAZADO para v0.1.1 |
| Comportamiento actual | Revisión de parser/caché | Generar 50.000 notas tarda ~0,5 s directo / ~1,1 s mediante archivo CLI; los parses repetidos aseguran validación de original/output | RECHAZADO para v0.1.1 |
| Polling actual de 200 ms | Investigación de trabajo en reposo | 375 ms de CPU en 10 s, ~3,75 % de un núcleo lógico o ~0,24 % de la máquina de 16 hilos | BASE ACEPTADA; sin cambios |
| Globalización actual | Globalización invariante | Sin cambio de tamaño en Windows; la aplicación Linux creció 1.152 B | RECHAZADO |
| Publicación actual sin trimming | Trimming parcial | Sin cambio efectivo en publicación aislada; reflection sigue siendo un riesgo de compatibilidad | RECHAZADO |
| JIT/runtime actual | NativeAOT / ReadyToRun / Dynamic PGO | NativeAOT excluido; el inicio no justifica ReadyToRun; Dynamic PGO lo gestiona el runtime | FUTURO |

### Experimento dependiente del framework

| Plataforma | Build | Aplicación | Directorio empaquetado | ZIP |
|---|---|---:|---:|---:|
| Windows x64 | base autocontenida | 76,998,317 B | 96,262,053 B | 42,122,507 B |
| Windows x64 | candidato final dependiente del framework | 9,674,819 B | 28,833,889 B | 11,611,873 B |
| Linux x64 | base autocontenida | 90,100,511 B | 90,478,119 B | 39,610,618 B |
| Linux x64 | candidato final dependiente del framework | 23,409,809 B | 23,692,976 B | 9,388,588 B |

En Windows, siete inicios dependientes del framework produjeron una mediana de 966,2 ms, working set promedio de 229.394.725 B y 194.448.823 B de bytes privados promedio. Frente a la base, es aproximadamente 124,6 ms más rápido, con 20,96 MB menos de working set y 18,39 MB menos de memoria privada. El experimento abrió correctamente y usa los mismos ensamblados. Solo se acepta como descarga opcional porque traslada al usuario el requisito del runtime.

Los paquetes dependientes del framework omiten deliberadamente los archivos de licencia/aviso del .NET Runtime porque no lo redistribuyen. Todos los avisos de la aplicación y demás terceros permanecen presentes. Los paquetes autocontenidos y sus nombres siguen siendo las descargas principales.

### Experimento de compresión de archivo único

| Plataforma | Aplicación comprimida | Paquete comprimido | ZIP comprimido | Cambio del ZIP |
|---|---:|---:|---:|---:|
| Windows x64 | 38,513,596 B | 57,777,332 B | 41,394,151 B | −728,356 B (−1.73%) |
| Linux x64 | 45,113,455 B | 45,491,063 B | 38,719,705 B | −890,913 B (−2.25%) |

El inicio en caliente de Windows subió de 1.090,8 ms a 1.189,4 ms. El working set promedio subió a 283.534.482 B y los bytes privados a 236.096.366 B. El ZIP final ya está comprimido, por lo que comprimir el payload ahorra poca descarga y añade costo de descompresión. No se habilita.

### Revisión del contenido publicado y Avalonia

El empaquetador de release ya elimina los PDB. Los ZIP auditados no contienen pruebas, cachés de paquetes, temporales, documentación XML ni artifacts solo de desarrollo. El renderer requiere los archivos nativos SkiaSharp, HarfBuzzSharp y ANGLE.

No existe referencia a `Avalonia.Fonts.Inter` que eliminar. `Avalonia.Desktop 12.1.1` incorpora intencionalmente el grafo Win32, X11/FreeDesktop, nativo, Skia y HarfBuzz usado por `UsePlatformDetect()`. La auditoría multifichero estima unos 1,22 MB de ensamblados administrados X11/FreeDesktop claramente ajenos en Windows y 1,06 MB de ensamblados Win32 ajenos en Linux. Sustituir `Avalonia.Desktop` por paquetes elegidos a mano cambiaría el arranque y ampliaría la matriz de compatibilidad nativa de diálogos, portapapeles, fuentes, entrada y DPI. Se aplaza ese intercambio.

### Parser, polling, logging y rutas

El perfilado de un documento de 50.000 notas midió una mediana de ~71,7 ms de parse y ~504,4 ms de generación directa. Los tres parses tienen funciones de seguridad distintas: validar la entrada inmutable, crear el output mutable y validar el resultado serializado. Una capa de caché o clonación añadiría riesgos de corrección e invalidación por una fracción de una operación ya inferior a un segundo, así que no se modifica el motor.

La detección usa un timer de UI de 200 ms y una demora de 250 ms del lector de procesos Windows. Con osu!stable abierto, el trabajo en reposo medido es suficientemente bajo como para que aumentar estos intervalos principalmente haga sentir más lenta la detección. El logging produjo aproximadamente 175 KB durante cuatro días, incluidas ejecuciones repetidas de instrumentación; la rotación aún no se justifica.

Las rutas Linux ya respetan `XDG_CONFIG_HOME`, `XDG_DATA_HOME` y `XDG_STATE_HOME`. HRandomPlus no tiene directorio de caché persistente, por lo que `XDG_CACHE_HOME` no aplica.

## Condición de equivalencia funcional

Toda build aceptada debe superar el runner existente de 100 pruebas y conservar output determinista para el mismo beatmap, modo, configuración y seed. El inicio de UI en Windows y las auditorías de contenido son obligatorios. Los cambios sensibles a Linux requieren un smoke test en Linux real antes de aceptarse.

El árbol final compila con 0 errores y el runner completo informa 100 aprobadas / 0 fallidas. Una prueba separada de equivalencia antes/después compiló la referencia `v0.1.0-playtest` desde `06d9bd5`, ejecutó ambas CLI contra el mismo archivo normal, configuración y seed `123456`, y comparó los bytes `.osu` generados. Ambos outputs produjeron SHA-256 `ed6d3353a8bc068044215de2457bb8cab6bab260e8b215dc1f89832f67784550`.

## Cambios aceptados

- Añadir ZIP opcionales dependientes del framework para Windows x64 y Linux x64.
- Mantener ambos ZIP autocontenidos como descargas principales sin requisitos previos.
- Omitir solo los avisos de .NET Runtime en los ZIP dependientes del framework porque esas builds no redistribuyen .NET.
- Versionar el candidato de empaquetado como `v0.1.1-playtest`.

## Cambios rechazados

- `EnableCompressionInSingleFile`: reducción insignificante del ZIP con peor inicio y memoria en Windows.
- Trimming y globalización invariante: sin beneficio medido de tamaño; trimming añade además riesgo por reflection.
- Cirugía de backends de Avalonia: ahorro máximo pequeño frente a un costo desproporcionado de compatibilidad y mantenimiento.
- Cambios en parser/caché, polling, rotación de logs, ReadyToRun y NativeAOT: ninguna medición actual justifica su riesgo o complejidad.

## Reproducibilidad y conclusión

Las fuentes, randomizador, fuentes de detección, comportamiento de UI, formato de perfiles y lógica de importadores permanecen sin cambios. El único cambio de producto es la versión del ensamblado; el único cambio de distribución son dos jobs opcionales dependientes del framework y su empaquetado consciente de licencias. Las builds autocontenidas normales siguen siendo reproducibles mediante `.github/workflows/build.yml`.

Para `v0.1.1-playtest`, distribuir los ZIP autocontenidos como descargas recomendadas e identificar claramente los dependientes del framework con su requisito .NET 8 x64. No se debe trasladar compresión experimental, trimming, eliminación de backends ni optimización del motor.

## Comparación final

| Build Windows | ZIP | Instalada | Inicio | Working set promedio | Decisión |
|---|---:|---:|---:|---:|---|
| Base autocontenida v0.1.0 | 42,122,507 B | 96,262,053 B | mediana 1,090.8 ms | 250,349,861 B | Referencia / principal |
| Candidato v0.1.1 dependiente del framework | 11,611,873 B | 28,833,889 B | mediana 966.2 ms | 229,394,725 B | CONSERVAR opcional |
| Autocontenida con compresión de payload | 41,394,151 B | 57,777,332 B | mediana 1,189.4 ms | 283,534,482 B | RECHAZAR |
| Experimento de trimming parcial | sin reducción efectiva | sin reducción efectiva | no promovido | no promovido | RECHAZAR |

| Build Linux | ZIP | Instalada | Resultado nativo | Decisión |
|---|---:|---:|---|---|
| Base autocontenida v0.1.0 | 39,610,618 B | 90,478,119 B | Playtest anterior correcto | Referencia / principal |
| Candidato v0.1.1 dependiente del framework | 9,388,588 B | 23,692,976 B | Smoke test aprobado con .NET 8 x64 Runtime | CONSERVAR opcional |
| Autocontenida con compresión de payload | 38,719,705 B | 45,491,063 B | No necesaria tras el escaso ahorro del ZIP | RECHAZAR |

## Resultado del smoke test de Linux

La build Linux x64 dependiente del framework aprobó su smoke test final en un sistema Linux real con .NET 8 x64 Runtime instalado. La condición Linux de v0.1.1 queda cerrada.
