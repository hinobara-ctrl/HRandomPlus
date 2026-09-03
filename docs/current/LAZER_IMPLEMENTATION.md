<!-- document-status: current -->
# Implementación de osu!lazer (v0.2.x actual)

## Base upstream auditada

La implementación se comprobó contra el commit [`48c4800e3ae4ee752452cdff83bd3787ccf3105f`](https://github.com/ppy/osu/tree/48c4800e3ae4ee752452cdff83bd3787ccf3105f) de ppy/osu, no contra una rama móvil. En esa revisión:

- `BeatmapInfo` corresponde a la tabla Realm `Beatmap`, usa una clave primaria GUID y enlaza `BeatmapSet`, `Metadata`, `Hash`, `DifficultyName` y `OnlineID`.
- `BeatmapSetInfo` corresponde a `BeatmapSet` y posee entradas `RealmNamedFileUsage`. Cada uso vincula un nombre lógico con un `RealmFile` identificado por hash SHA-256.
- `RealmFileStore` guarda contenido en `files/<primer carácter del hash>/<primeros dos caracteres>/<SHA-256 completo en minúsculas>`.
- el almacenamiento predeterminado está controlado por `storage.ini` (`FullPath`) y contiene `client.realm`, `files/` y `logs/`.
- al pasar una ruta de archivo aceptada al ejecutable de escritorio, esta se reenvía mediante `ArchiveImportIPCChannel` al importador del juego en ejecución.

Versiones antiguas de lazer registraban `Song select updating selection with beatmap:<GUID> ruleset:<ruleset>`. La revisión auditada ya no emite esa línea desde Song Select; emite `Game-wide working beatmap updated to <display name>`. Por ello, HRandomPlus admite ambos formatos. Un GUID se resuelve directamente. El texto solo se acepta si identifica exactamente un registro Realm; cero o múltiples resultados producen un estado visible sin resolver y nunca seleccionan un beatmap arbitrario.

## Flujo de ejecución

```text
log de ejecución -> identidad del beatmap seleccionado -> metadatos client.realm
                 -> hash de archivo Realm -> blob .osu files/<hash>
                 -> HRandomPlus -> dificultad generada -> importación .osz en lazer
```

El log de ejecución representa el estado de selección en vivo, `client.realm` aporta metadatos y relaciones, y `files/` contiene los blobs físicos direccionados por contenido. Realm y `files/` son entradas de solo lectura para HRandomPlus.

1. `LazerProcessDetector` distingue un proceso lazer nativo del directorio de ejecutable tradicional de stable que contiene `Songs`.
2. `LazerStorageDiscovery` comprueba la ubicación predeterminada de la plataforma, sigue `storage.ini` y revisa raíces compatibles junto al ejecutable detectado para instalaciones portables.
3. `LazerRuntimeLogMonitor` busca hacia atrás al iniciar en bloques de 2 MiB, hasta encontrar una selección, llegar al principio o alcanzar 32 MiB; luego solo sigue los bytes anexados. Maneja truncado, reemplazo, nombres antiguos `runtime*.log` y nombres actuales `<timestamp>.runtime.log`. Si hay más de un almacenamiento, selecciona el que tenga el log más reciente.
4. `RealmLazerBeatmapCatalog` abre `client.realm` con `IsReadOnly = true` e `IsDynamic = true`, usando el esquema guardado en disco en vez de asumir una versión de esquema de lazer; nunca inicia una transacción ni escribe datos Realm/almacenamiento.
5. `LazerBeatmapResolver` valida el blob `.osu` seleccionado contra su SHA-256 y materializa una entrada temporal para el parser. Las materializaciones de más de siete días se eliminan oportunísticamente.
6. El motor sin cambios de HRandomPlus produce la dificultad nueva.
7. `LazerArchiveImporter` construye un `.osz` temporal con el `.osu` generado y los recursos originales del set. Rechaza la ausencia del blob de audio principal, conserva nombres ZIP distintos por mayúsculas y mantiene la protección contra traversal. La copia del archivo recibe IDs online desvinculados, mientras el output generado conservado y todos los archivos fuente de lazer permanecen intactos.
8. El `.osz` se pasa al ejecutable lazer detectado. Si falla el lanzamiento se conserva en la carpeta de output de HRandomPlus para importarlo manualmente; los archivos temporales enviados correctamente se eliminan tras un período de gracia y los obsoletos se limpian al iniciar.

Las únicas escrituras al filesystem durante la detección ocurren en el directorio temporal del sistema. `client.realm`, `files/`, `logs/`, `storage.ini` y el beatmap original son entradas de solo lectura.

## Coexistencia e identidad de fuente

Windows conserva `WindowsMemoryBeatmapSource` para stable. Linux conserva `TosuBeatmapSource` y osu-winello para stable. `ArbitratingBeatmapSource` combina ambas con `LazerCurrentBeatmapSource`. Las selecciones correctas incluyen una marca temporal de observación; cuando ambos juegos están abiertos, gana la selección modificada más recientemente. Los estados de stable, tosu y lazer se formatean por separado. Los controles exclusivos de stable se desactivan mientras lazer es la fuente activa.

## Dependencias añadidas para lazer

- Realm 20.1.0, commit exacto del repositorio del paquete [`370ce596a0cf5e992b717bb199d70e55391ff2b9`](https://github.com/realm/realm-dotnet/tree/370ce596a0cf5e992b717bb199d70e55391ff2b9), Apache-2.0.
- MongoDB.Bson 2.21.0, commit exacto del repositorio del paquete [`5a9c3311e158910b88195f290e6d4b1b2715d2b2`](https://github.com/mongodb/mongo-csharp-driver/tree/5a9c3311e158910b88195f290e6d4b1b2715d2b2), Apache-2.0.
- Remotion.Linq 2.2.0, Apache-2.0, resuelta transitivamente por Realm.
- Fody 6.9.1 y el weaver de Realm son herramientas resueltas solo para compilación, desactivadas en esta integración dinámica de Realm y no redistribuidas.

Consulta `THIRD_PARTY_NOTICES.md` y `licenses/Apache-2.0.txt` para los avisos de distribución.

## Limitaciones conocidas y fallback seguro

- El log upstream actual, solo textual, es inherentemente menos preciso que la línea histórica con GUID. Los nombres visibles ambiguos permanecen sin resolver en vez de seleccionar un beatmap arbitrario.
- Una actualización del juego que cambie tablas/propiedades Realm, la distribución de blobs o el mensaje de log puede dejar sin resolver la detección automática. Debe fallar de forma cerrada; las integraciones de stable permanecen disponibles.
- HRandomPlus no afirma que iniciar el importador garantice que lazer completó la importación. La UI informa que se envió el archivo. Verifica la nueva dificultad local en Song Select.
- El almacenamiento real del cliente se excluye intencionalmente de los fixtures automatizados porque puede contener datos personales de cuenta/biblioteca.

## Checklist smoke en máquina real

Ejecuta la build v0.2.x correspondiente y registra la versión exacta de lazer.

### Windows x64

- [x] Iniciar HRandomPlus sin ningún juego abierto; confirmar estado no disponible/en espera y selector manual responsivo.
- [x] Abrir solo stable; confirmar su detección y una randomización sin cambios.
- [x] Abrir solo lazer y entrar a Song Select; confirmar que el estado nombra explícitamente osu!lazer.
- [x] Cambiar dificultad y set; confirmar que cada selección se actualiza sin reescaneo ni congelamiento.
- [x] Randomizar; confirmar que lazer importa una dificultad local nueva, carga audio/fondo y mantiene intacto el original.
- [x] Repetir la randomización; confirmar nombres únicos de dificultad/archivo.
- [x] Cerrar/reabrir lazer y rotar/reiniciar sus logs; confirmar recuperación.
- [x] Abrir stable y lazer a la vez; confirmar que gana la selección cambiada más recientemente con la etiqueta de fuente correcta.
- [x] Confirmar que la selección manual exclusiva de stable conserva su comportamiento establecido.

### Linux x64 nativo

- [x] Repetir todas las comprobaciones de generación/importación exclusivas de lazer usando lazer nativo; no iniciar tosu ni Wine.
- [x] Confirmar que se encuentra el almacenamiento predeterminado o personalizado mediante `storage.ini` sin `sudo`.
- [x] Repetir por separado el checklist de regresión establecido para stable + osu-winello + tosu.
- [x] Confirmar que cerrar HRandomPlus no deja procesos auxiliares y que los archivos/materializaciones temporales obsoletos se limpian finalmente.

La cobertura automatizada valida variantes del parser, seguimiento/truncado, almacenamiento estándar/personalizado/portable, distribución de blobs y SHA-256, resolución GUID, rechazo de texto ambiguo, arbitraje de fuentes, etiquetado explícito de estado y creación de `.osz` desvinculado con recursos. Los playtests funcionales reales en Windows/Linux fueron aprobados. Desactivar artificialmente el lanzador de archivos de escritorio de lazer no se considera una condición de release porque no representa el flujo normal de importación.
