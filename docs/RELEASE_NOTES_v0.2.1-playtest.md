# HRandomPlus v0.2.1-playtest

Esta es una actualización enfocada en corrección, robustez y reproducibilidad sobre v0.2.0. No cambia H-Random, S-Random, Custom, seeds, perfiles ni el output determinista.

## Corregido

- Actualiza el mapa actual de lazer tras un evento nuevo de selección con el mismo GUID/nombre y después de una importación correcta, evitando reutilizar un hash/blob Realm obsoleto.
- Selecciona el runtime log válido más reciente de lazer en vez de preferir `runtime.log` por nombre.
- Libera los handles de procesos descartados de osu!stable en Windows.
- Protege las rutas de copia mediante Wine que contienen metacaracteres de shell, espacios, apóstrofes, `!`, acentos y Unicode.
- Detiene la importación de lazer con un error claro cuando falta el blob de audio principal referenciado.
- Conserva recursos cuyos nombres ZIP solo difieren por mayúsculas.

## Endurecimiento de release

- Ejecuta la suite automatizada en Windows y Ubuntu.
- Fija las versiones mayores existentes de GitHub Actions a commits exactos de release.
- Versiona lockfiles de NuGet y restaura dependencias de CI en modo bloqueado.
- Ignora con patrones estrechos los datos locales Realm/runtime de lazer.
- Añade un índice de documentación actual/histórica y alinea la descripción de selección manual exclusiva de stable.

## Fiabilidad posterior a la auditoría

- Conserva caracteres Unicode divididos al seguir logs de lazer y realiza al iniciar una búsqueda hacia atrás acotada más allá de los últimos 2 MiB.
- Se recupera correctamente cuando lazer se cierra y reabre sobre el mismo almacenamiento, y tolera el fallo de una fuente automática si otra sigue siendo válida.
- Cancela árboles de procesos auxiliares sin cambiar el contrato existente de timeout.
- Conserva `config.json` ante fallos transitorios de lectura y respalda JSON malformado antes de restaurar defaults.
- Maneja usos duplicados de archivos Realm y nombres temporales específicos del filesystem con diagnósticos controlados.
- Añade extracción de `.osz` por streaming y acotada, además de manejo consistente de rutas/mayúsculas según el SO en extracción, hashing y validación.

La suite automatizada completa contiene ahora 188 pruebas aprobadas. El pequeño conjunto de confirmaciones de plataforma en Linux real queda a cargo del propietario y está documentado por separado.

El `storage.ini` personalizado ya fue verificado en Windows y Linux reales. Las comprobaciones manuales de regresión en Linux para las rutas modificadas de Wine/importación siguen siendo un paso del candidato a cargo del propietario.

HRandomPlus permanece bajo `GPL-3.0-or-later`. No cambió ninguna dependencia ni el modelo de licencias; los avisos de terceros y requisitos de fuentes correspondientes existentes siguen vigentes.

## Distribución vigente

La release se prepara con dos binarios principales, ambos dependientes de .NET Runtime 8 x64: Windows x64 y Linux x64. Las variantes self-contained anteriores se conservan únicamente como antecedente histórico y ya no forman parte del empaquetado normal. Los ZIP de fuentes exactas de HRandomPlus y del snapshot GPL continúan como assets adicionales obligatorios.
