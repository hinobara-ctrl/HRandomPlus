# HRandomPlus v0.2.1-playtest

Esta actualización cierra la fase de robustez y reproducibilidad posterior a v0.2.0. Mantiene los presets, seeds y output determinista, e incorpora las decisiones de producto aprobadas para dual-stage 10K+, trills, pausas y presentación BPM.

## Randomizador e interfaz

- Añade `PreserveDualStages` para mapas 10K–18K. En keymodes impares, la columna central se comparte entre ambos stages y permanece neutral para Hand Balance.
- Unifica scoring y estadísticas con la regla de acorde compatible para trills.
- Corta trills sólo sobre `4 × MaxThresholdMs` y reinicia Dynamic Threshold sólo sobre `8 × MaxThresholdMs`.
- Conserva los doce snaps y los presenta en una cuadrícula compacta; múltiples BPM se resumen como un rango.
- Valida `MaxCandidateSets` entre 1 y 8.192, con 4.096 como valor predeterminado.

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
- Hace best-effort la limpieza temporal de `.osz`, sin ocultar éxitos ni errores principales.
- Aplica la misma validación estructural previa a entradas `.osu` directas y a dificultades dentro de `.osz`.
- Evita nombres reservados de Windows al sugerir archivos exportados de perfil, sin cambiar su nombre visible.
- Redacta todas las apariciones válidas del directorio personal en diagnósticos compartibles.
- Evita mezclar memoria y `Songs` de instalaciones stable distintas: como el reader actual no admite binding por PID, Windows falla de forma cerrada cuando más de un proceso `osu!` podría ser objetivo y usa siempre el root de la identidad única validada.
- Acota la espera posterior a timeout/cancelación de procesos auxiliares, incluso si el intento de terminación falla.
- Obtiene los nombres de assets de CI desde la versión canónica de `Directory.Build.props`.

La suite automatizada completa contiene ahora 351 pruebas aprobadas. Las confirmaciones manuales de plataforma permanecen documentadas por separado.

El `storage.ini` personalizado ya fue verificado en Windows y Linux reales. Las comprobaciones manuales de regresión en Linux para las rutas modificadas de Wine/importación siguen siendo un paso del candidato a cargo del propietario.

HRandomPlus permanece bajo `GPL-3.0-or-later`. No cambió ninguna dependencia ni el modelo de licencias; los avisos de terceros y requisitos de fuentes correspondientes existentes siguen vigentes.

## Distribución vigente

La release se prepara con dos binarios principales, ambos dependientes de .NET Runtime 8 x64: Windows x64 y Linux x64. Las variantes self-contained anteriores se conservan únicamente como antecedente histórico y ya no forman parte del empaquetado normal. Los ZIP de fuentes exactas de HRandomPlus y del snapshot GPL continúan como assets adicionales obligatorios.
