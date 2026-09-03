<!-- document-status: historical -->
> Tipo de documento: notas congeladas de v0.1.0-playtest; no son autoritativas para el HEAD actual.

# HRandomPlus v0.1.0-playtest

Primera distribución de playtest para osu!stable.

## Plataformas compatibles

- osu!stable en Windows x64.
- osu!stable en Linux x64 mediante osu-winello, Wine y tosu.
- osu!lazer **no es compatible**.

## Funciones incluidas

- Presets protegidos H-Random y S-Random más un perfil Custom persistente y restablecible.
- Perfiles personales con GUID estables, descripciones y seeds reproducibles.
- Importación/exportación UTF-8 `.hrp-profile.json` con validación, vista previa, resolución de conflictos y compatibilidad Windows/Linux.
- Procesamiento del mapa completo o de un rango seleccionado.
- Protección de long notes y validación del output.
- Selección automática más fallback manual de `.osu`.
- Versión de dificultad y nombre de archivo únicos al generar repetidamente.
- Importación mediante Wine en Linux con fallback nativo seguro.
- Referencia editable de BPM a milisegundos para snaps de 1/1 a 1/64.

La configuración antigua de perfiles se migra automáticamente. El último perfil personal histórico llamado Custom pasa a ser el espacio Custom persistente; los perfiles restantes se conservan con nombres únicos y GUID asignados.

## Estado de fuente corregido

El formateador de estado suponía anteriormente que toda selección automática provenía de tosu. La detección siempre fue correcta; solo era erróneo el origen mostrado.

- Windows ahora muestra `Beatmap detected automatically from osu!stable`.
- Linux sigue mostrando `Beatmap detected automatically by tosu`.
- La selección manual permanece diferenciada explícitamente.
- Una prueba de regresión específica de Windows cubre este comportamiento.

## Requisitos de Linux

- osu-winello.
- tosu ejecutándose en el mismo entorno Wine que osu!stable.
- Inicio mediante `osu-wine --tosu`.

## Artifacts

- `HRandomPlus-windows-x64.zip`
- `HRandomPlus-linux-x64.zip`
- `HRandomPlus-v0.1.0-playtest-source.zip`
- `HRandomPlus-v0.1.0-playtest-gpl-source.zip`
- `SHA256SUMS.txt`

Verifica las descargas mediante `SHA256SUMS.txt`. Los artifacts de GitHub Actions son temporales; los assets adjuntos a la GitHub Release etiquetada son las descargas estables.

## Licencias y fuentes

- HRandomPlus se distribuye bajo `GPL-3.0-or-later`; la licencia completa se incluye como `LICENSE` raíz y dentro de cada paquete binario.
- La build de Windows incorpora `OsuMemoryDataProvider 0.12.2` y `ProcessMemoryDataFinder 0.10.2`, ambos declarados `GPL-3.0-or-later` e integrados en el ejecutable único.
- Los componentes de terceros conservan sus propias licencias. Cada ZIP de plataforma contiene `THIRD_PARTY_NOTICES.md` y únicamente los archivos de licencia/aviso aplicables a esa build.
- `HRandomPlus-v0.1.0-playtest-source.zip` contiene las fuentes de HRandomPlus correspondientes a los binarios candidatos.
- `HRandomPlus-v0.1.0-playtest-gpl-source.zip` contiene el snapshot upstream completo de ProcessMemoryDataFinder en el commit `122dd102fe272de30471cf1f317805cb49ac23a4`. Los tags `osu_v0.12.2` y `process_v0.10.2` resuelven a ese commit.

Consulta `THIRD_PARTY_NOTICES.md`, `docs/current/DEPENDENCY_LICENSE_AUDIT.md` y `docs/current/GPL_SOURCE_MANIFEST.md` para la auditoría técnica y la correspondencia exacta de fuentes. El binario Windows, ambos archivos de fuentes correspondientes y `SHA256SUMS.txt` deben publicarse juntos.

**RELEASE DE WINDOWS LISTA:** HRandomPlus y sus componentes GPL enlazados de lectura de memoria se distribuyen bajo `GPL-3.0-or-later`; las fuentes correspondientes exactas y todos los avisos de terceros aplicables acompañan al candidato.
