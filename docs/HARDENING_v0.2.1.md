# Endurecimiento dirigido de v0.2.1

> **Estado histórico:** este documento registra la auditoría cerrada en el commit `ef9a30f`; las cifras de 146 pruebas y cuatro publicaciones del resumen corresponden a ese momento. El estado posterior de `v0.2.1-playtest` es 188 pruebas aprobadas y dos paquetes binarios principales framework-dependent. Consulta el [checklist funcional vigente](CHECKLIST_FUNCIONAL_FINAL_v0.2.1.md) y las [notas actuales de la release](RELEASE_NOTES_v0.2.1-playtest.md).

## Causa raíz

`LazerCurrentBeatmapSource` identificaba su resolución Realm en caché únicamente mediante el GUID y el nombre visible del beatmap. Por ello, un evento nuevo de ejecución para un mapa reimportado podía conservar el hash/blob anterior de Realm. Además, una importación correcta no invalidaba explícitamente esa resolución cuando lazer no emitía un evento útil de selección.

La caché ahora incluye la revisión de observación en ejecución. Un evento nuevo fuerza una resolución de Realm aunque el GUID y el nombre visible no cambien, mientras que un evento sin cambios permanece en caché. Una importación correcta en lazer invalida explícitamente la resolución actual una vez.

## Clasificaciones finales

| Hallazgo | Clasificación | Resultado |
|---|---|---|
| El mapa actualizado/importado de lazer queda obsoleto | CORREGIDO | La revisión del evento participa en la clave de caché; una importación correcta invalida una vez. |
| Selección del `runtime.log` activo | CORREGIDO | Todos los nombres de log válidos compiten mediante `LastWriteTimeUtc` en un único método de selección. |
| Ciclo de vida de `Process` para stable en Windows | CORREGIDO | Todo proceso inspeccionado que no se devuelve al llamador se libera; la propiedad del proceso devuelto permanece en el llamador. |
| Metacaracteres de `cmd` en Wine | CORREGIDO | Las rutas se entregan mediante variables de entorno y nunca se insertan en el texto interpretado del comando; la expansión retardada está desactivada. |
| Recursos de lazer ausentes | CORREGIDO | La ausencia del audio principal referenciado produce un error identificable; los recursos visuales/hitsounds no esenciales siguen siendo opcionales. |
| Sensibilidad a mayúsculas del ZIP | CORREGIDO | Los recursos cuyos nombres difieren por mayúsculas se conservan con `StringComparer.Ordinal`. |
| CI de Linux | CORREGIDO | El job completo de pruebas se ejecuta en `windows-latest` y `ubuntu-latest`. |
| Documentación de selección manual | CORREGIDO | La documentación coincide con la UI actual: solo stable y desactivada mientras lazer está activo. |
| `storage.ini` personalizado | ACEPTADO SIN CAMBIOS | Ya fue aprobado en Windows y Linux reales; no cambió el descubrimiento de almacenamiento. |
| Referencias de GitHub Actions | CORREGIDO | Las versiones mayores existentes están fijadas a sus commits exactos de release. |
| Lockfiles de NuGet | CORREGIDO | Los proyectos relevantes usan lockfiles versionados y CI restaura en modo bloqueado. |
| Reglas para ignorar datos de lazer | CORREGIDO | Patrones estrechos cubren datos Realm/runtime sin excluir tipos amplios de fixtures. |
| Privacidad de rutas de diagnóstico | FUTURO | Las rutas locales completas siguen siendo útiles durante el playtest; su ocultación corresponde a un futuro diseño de niveles de log. |
| Rendimiento del fallback textual de Realm | FUTURO | Medir solo si los usuarios informan selección lenta; la caché actual lo limita a cambios de selección. |

## Endurecimiento posterior a la auditoría

| Hallazgo | Clasificación | Resultado |
|---|---|---|
| UTF-8 dividido entre lecturas incrementales del log | CORREGIDO | Un decodificador persistente conserva secuencias multibyte parciales entre anexados. |
| Una fuente de beatmap falla y la otra funciona | CORREGIDO | El arbitraje conserva el resultado válido, informa el fallo de la otra fuente y nunca absorbe la cancelación del llamador. |
| El llamador cancela un proceso auxiliar en ejecución | CORREGIDO | Se intenta terminar todo el árbol, se drena su salida y se relanza `OperationCanceledException`; el timeout no cambia. |
| Lectura transitoria o corrupta de `config.json` | CORREGIDO | Los fallos transitorios de E/S o acceso dejan intacto el original; el JSON malformado se respalda antes de guardar defaults. |
| Extracción de `.osz` sin límites | CORREGIDO | La cantidad de entradas, tamaño individual, tamaño del beatmap y total expandido tienen límites explícitos generosos y conteo durante el streaming. |
| Cierre/reapertura de lazer con el mismo almacenamiento | CORREGIDO | Perder el proceso invalida la sesión y la caché de resolución antes de una reapertura posterior. |
| Escaneo inicial del log limitado a los últimos 2 MiB | CORREGIDO | El inicio busca hacia atrás por bloques hasta hallar una selección, llegar al principio o alcanzar el límite de 32 MiB. |
| Múltiples usos Realm coinciden con un hash `.osu` | CORREGIDO | Cero o múltiples coincidencias producen errores controlados de resolución en vez de una excepción LINQ no controlada. |
| Nombre inseguro de materialización temporal | CORREGIDO | El nombre físico se sanea para el filesystem actual sin cambiar el nombre lógico del recurso. |
| Comparación de contención dependiente del SO | CORREGIDO | La contención de rutas ignora mayúsculas en Windows y las distingue en Linux. |
| Entradas ZIP que solo difieren por mayúsculas | CORREGIDO | Extracción, hashing y validación conservan consistentemente entradas lógicas distintas por mayúsculas. |

Los límites son 10.000 entradas, 2 GiB por entrada, 64 MiB por `.osu` y 8 GiB de datos expandidos totales. Buscan rechazar archivos patológicos sin restringir beatmapsets grandes legítimos.

Este endurecimiento no cambió el randomizador, el output del parser, las seeds, los perfiles, el descubrimiento de almacenamiento, el arbitraje de fuentes ni el modelo de licencias.

## Resumen de validación

- Restore bloqueado: aprobado.
- Compilación Release local: aprobada con 0 errores.
- Suite automatizada: 146 aprobadas, 0 fallidas.
- Publicación Windows x64: autocontenida y dependiente del framework aprobadas.
- Publicación cruzada Linux x64: autocontenida y dependiente del framework aprobadas.
- Ejecución CI en Ubuntu: configurada y requerida por el grafo del candidato; se ejecutará después del push.
- Comprobaciones en Linux real del delta posterior a la auditoría: dejadas intencionalmente al propietario en `LINUX_POST_AUDIT_TESTS_v0.2.1.md`.
- Escaneo de secretos/datos personales del árbol y el parche actuales: aprobado; no se versiona ninguna base Realm ni runtime log.
