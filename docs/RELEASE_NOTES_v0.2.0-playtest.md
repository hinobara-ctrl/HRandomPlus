# HRandomPlus v0.2.0-playtest (candidato de desarrollo)

Esta versión añade soporte nativo de primer nivel para osu!lazer en Windows x64 y Linux x64, conservando todas las rutas existentes de osu!stable y el motor de randomización de v0.1.1.

## Añadido

- Descubrimiento nativo del proceso y almacenamiento de lazer, incluidas ubicaciones estándar, personalizadas mediante `storage.ini` y portables compatibles.
- Monitorización incremental del runtime log, consciente de rotación, con compatibilidad para GUID histórico, log textual actual y `<timestamp>.runtime.log`.
- Consulta dinámica de solo lectura de `client.realm` que sigue el esquema en disco, más validación SHA-256 del blob `.osu` seleccionado.
- Importación segura de `.osz` de una dificultad local desvinculada con los recursos originales del set; sin modificar Realm ni blobs.
- Arbitraje determinista stable/lazer y etiquetas explícitas de fuente.
- Cobertura automatizada para primitivas de detección, rechazo de ambigüedad, resolución de almacenamiento/blob, arbitraje y construcción de archivos.

## Sin cambios

- Algoritmos, parámetros, seeds y protección de long notes de H-Random, S-Random y Custom.
- Almacenamiento/importación/exportación de perfiles.
- Detección en memoria de stable para Windows.
- Detección stable con tosu/osu-winello e importación mediante Wine en Linux.
- Manejo de colisiones de output. La selección manual de `.osu` sigue disponible para stable y se desactiva mientras lazer es la fuente activa.

## Estado de verificación

La implementación y la suite automatizada están completas localmente. Los playtests funcionales en Windows real y Linux nativo fueron aprobados sin fallos reportados; el escenario artificial de fallo del lanzador no es una condición de release porque no representa el flujo normal de importación de lazer.

El diseño técnico, la revisión upstream exacta y el comportamiento ante fallos están documentados en `docs/LAZER_IMPLEMENTATION.md`.
