# HRandomPlus: diseño del sistema de perfiles

## Estado del documento

Este documento registra el comportamiento anterior, las decisiones de diseño y el contrato implementado para perfiles persistentes e intercambiables.

**IMPLEMENTADO LOCALMENTE:** Custom persistente, GUID, migración idempotente, nombres reservados, Duplicate, Delete protegido, Reset Custom con confirmación, escritura atómica e importación/exportación `.hrp-profile.json` con preview y resolución de conflictos. La implementación está cubierta por pruebas automatizadas y queda pendiente de playtest de UI y commit del propietario.

El motor del randomizer y los valores de H-Random/S-Random no fueron modificados. El documento se conserva porque registra el formato portable, las reglas de migración y las decisiones de compatibilidad que no conviene duplicar por completo en README.

## Contexto actual

HRandomPlus muestra tres perfiles integrados:

- `H-Random`
- `S-Random`
- `Custom`

`H-Random` y `S-Random` son presets integrados y se reconstruyen desde el código cada vez que se inicia la aplicación. Por ello, sus valores originales permanecen constantes aunque el usuario cambie parámetros en la interfaz.

`Custom` también es actualmente un preset integrado e inmutable. Sus parámetros iniciales son equivalentes a la configuración base de H-Random, con la principal diferencia visible de que utiliza el sufijo de dificultad ` CUSTOM` en lugar de ` H-RANDOM+`. No contiene un algoritmo separado.

Cuando el usuario modifica `Custom` y pulsa `Save profile`, la aplicación no actualiza el Custom integrado. Solicita un nombre y crea un perfil personal adicional. Actualmente también permite usar nombres como `Custom`, `H-Random` o `S-Random`, lo que puede producir entradas visualmente duplicadas y confusas.

Los perfiles personales se guardan en la configuración local de cada usuario:

```text
Windows: %LOCALAPPDATA%\HRandomPlus\config.json
Linux:   $XDG_CONFIG_HOME/HRandomPlus/config.json
```

En Linux, si `XDG_CONFIG_HOME` no está definido, la ruta habitual es `~/.config/HRandomPlus/config.json`.

Estos datos no forman parte del ejecutable, los ZIP de distribución ni el repositorio. Por tanto, un perfil creado por una persona no aparece en la instalación de otra salvo que se comparta deliberadamente.

## Custom persistente

Custom debe convertirse en un perfil único, modificable y persistente:

- `H-Random` continúa siendo un preset protegido e inmutable.
- `S-Random` continúa siendo un preset protegido e inmutable.
- `Custom` carga los últimos parámetros guardados por el usuario.
- Guardar Custom actualiza ese único perfil y no crea otra entrada llamada Custom.
- Custom no puede eliminarse, pero puede ofrecer una acción `Reset Custom` para recuperar sus valores iniciales.
- `Custom`, `H-Random` y `S-Random` pasan a ser nombres reservados para perfiles personales.

### Persistencia implementada

`AppSettings` contiene una propiedad específica y nullable:

```csharp
public HRandomConfig? CustomConfig { get; set; }
```

Al iniciar la aplicación:

1. H-Random y S-Random se cargan desde sus constantes del código.
2. Custom se carga desde `CustomConfig`.
3. Si `CustomConfig` todavía no existe, se utilizan los valores predeterminados actuales de Custom.

### Comportamiento de los botones

- Al seleccionar Custom, `Save profile` debe guardar directamente sus parámetros sin pedir otro nombre. Conviene cambiar temporalmente su texto a `Save Custom`.
- `Duplicate` debe crear un perfil personal independiente, pedir un nombre y asignarle una identidad nueva.
- En H-Random y S-Random, `Save profile` no debe sobrescribir el preset. El usuario puede usar `Duplicate` para guardar una variante.
- `Delete custom` solo debe habilitarse para perfiles personales con nombre, no para el Custom persistente.
- Una futura acción `Reset Custom` debe solicitar confirmación y restaurar solamente Custom.

`Apply settings` no debe usarse para guardar perfiles. Actualmente esa acción corresponde a opciones de plataforma, conexión y output, no a los parámetros del randomizer.

## Migración de instalaciones existentes

La migración debe conservar los perfiles creados con versiones anteriores:

1. Si `CustomConfig` no existe y hay un perfil personal cuyo nombre sea `Custom` —ignorando mayúsculas, minúsculas y espacios exteriores—, copiar su configuración al nuevo Custom persistente.
2. Eliminar de `CustomProfiles` únicamente la entrada migrada para evitar el duplicado visual.
3. Si hay varias entradas antiguas llamadas Custom, usar una regla determinista y documentada; preferentemente la última entrada guardada, conservando las restantes con nombres renombrados para no perder datos.
4. Asignar identificadores a todos los perfiles personales antiguos que todavía no tengan uno.
5. Guardar la configuración migrada una sola vez.

La migración debe ser idempotente: iniciar varias veces la aplicación no debe duplicar, renombrar nuevamente ni sobrescribir perfiles ya migrados.

## Perfiles compartibles

Los perfiles deben poder exportarse a un archivo independiente, legible y compatible entre Windows y Linux.

Extensión recomendada:

```text
.hrp-profile.json
```

El archivo exportado es un medio de transporte. Después de importarlo, la aplicación copia el perfil a la configuración personal; el usuario puede borrar el archivo descargado sin perder el perfil importado.

### Estructura propuesta

```json
{
  "format": "HRandomPlus.Profile",
  "formatVersion": 1,
  "profileId": "3fe1e469-91a6-4d16-b469-c808d2844bcc",
  "name": "Jacks 1/4 moderados",
  "description": "Permite algunos jacks a 1/4 con comportamiento similar a H-Random.",
  "engineVersion": 1,
  "config": {
    "DynamicThreshold": true,
    "PreserveDualStages": false,
    "MinThresholdMs": 40,
    "BaseThresholdMs": 100,
    "MaxThresholdMs": 160,
    "Seed": null
  }
}
```

`config` debe contener todos los campos de `HRandomConfig`, incluidos los pesos de scoring y la seed nullable.

### Datos que nunca deben exportarse

- Ruta de osu! u osu!winello.
- Directorios personales o rutas del beatmap.
- Host o puerto de tosu.
- Logs.
- Último mapa seleccionado.
- Preferencias de output.
- Cualquier otra configuración global de la aplicación.

## Identidad de perfiles

Cada perfil personal debe tener un identificador estable:

```csharp
public Guid Id { get; set; }
```

Reglas:

- Un perfil nuevo recibe un GUID nuevo.
- `Duplicate` siempre genera otro GUID.
- Exportar conserva el GUID.
- Importar conserva el GUID del archivo.
- Volver a importar el mismo GUID permite reconocer una actualización del mismo perfil, aunque el nombre haya cambiado.
- Los presets integrados pueden usar identificadores constantes internos si resulta útil, pero nunca deben ser reemplazados directamente mediante una importación.

## Interfaz de importación y exportación

Acciones recomendadas:

- `Save Custom` o `Save profile`, según la selección.
- `Duplicate`.
- `Import profile`.
- `Export profile`.
- `Delete profile`.
- Opcionalmente, `Reset Custom`.

### Exportación

- Permitir exportar Custom y cualquier perfil personal.
- H-Random y S-Random también pueden exportarse, pero el archivo representa una copia transportable de sus parámetros.
- Usar UTF-8 y nombres de archivo saneados.
- No modificar el perfil local al exportarlo.

### Importación

Antes de guardar, mostrar una previsualización con:

- Nombre y descripción.
- Versión del formato y del motor.
- Thresholds principales.
- Seed fija o aleatoria.
- Resumen de los pesos.

Si ya existe el mismo `profileId`, ofrecer:

- Actualizar el perfil existente.
- Importar como copia con GUID nuevo.
- Cancelar.

Si solamente coincide el nombre, pero el GUID es diferente, importar con un nombre único, por ejemplo:

```text
Jacks 1/4 moderados (2)
```

Si un archivo externo usa un nombre reservado, renombrarlo de forma explícita:

```text
H-Random (Imported)
S-Random (Imported)
Custom (Imported)
```

Nunca se debe sobrescribir silenciosamente un preset integrado ni el Custom persistente.

## Versionado y compatibilidad

El formato debe declarar:

- `formatVersion`: versión de la estructura del archivo.
- `engineVersion`: versión de la semántica del randomizer para la que fue creado.

Una versión futura puede añadir campos opcionales manteniendo `formatVersion`. Si cambia la estructura o la interpretación de forma incompatible, debe aumentar la versión correspondiente.

La aplicación debe rechazar formatos futuros incompatibles con un mensaje claro, en vez de intentar interpretarlos parcialmente. Los perfiles entre Windows y Linux deben producir exactamente la misma configuración del motor.

## Seguridad y robustez

La importación debe:

- Aceptar únicamente JSON de perfil, no código ejecutable.
- Limitar el tamaño del archivo, por ejemplo a 256 KB.
- Verificar `format`, `formatVersion`, `profileId`, nombre y configuración.
- Validar límites numéricos y valores no finitos.
- Ejecutar `HRandomConfig.Validate()` antes de mostrar o guardar el perfil.
- Rechazar nombres vacíos o excesivamente largos.
- No interpretar rutas incluidas en propiedades desconocidas.
- No modificar configuraciones globales.
- Escribir la configuración local de forma atómica para evitar corrupción.
- Mostrar errores comprensibles sin cerrar la aplicación.

Una firma criptográfica no es necesaria para la primera versión porque los archivos contienen solamente datos validados. En el futuro podría agregarse un checksum informativo, pero no debe presentarse como prueba de confianza o autoría.

## Almacenamiento interno

Para la primera implementación, los perfiles importados deben continuar dentro de `CustomProfiles` en el `config.json` personal. Esto minimiza cambios y conserva el comportamiento actual.

No es necesario mantener una copia del `.hrp-profile.json` dentro de la aplicación. Separar cada perfil en archivos internos podría evaluarse posteriormente si el catálogo crece mucho, pero no aporta una ventaja suficiente para la primera versión.

## Pruebas requeridas

### Custom persistente

- Custom conserva todos sus campos después de cerrar y abrir la aplicación.
- Guardar Custom repetidamente no crea perfiles duplicados.
- Reset Custom restaura únicamente sus valores predeterminados.
- Editar parámetros mientras se usa H-Random o S-Random no modifica los presets.
- Los nombres reservados se comparan sin distinguir mayúsculas y espacios exteriores.

### Migración

- Una configuración antigua sin `CustomConfig` sigue cargando.
- Un perfil antiguo llamado Custom se migra sin perder campos ni seed.
- La migración es idempotente.
- Múltiples perfiles antiguos con nombres conflictivos no se pierden.
- Los perfiles antiguos reciben GUID válidos.

### Exportación

- Todos los campos del randomizer sobreviven un ciclo exportar/importar.
- Seed fija y seed aleatoria se conservan correctamente.
- Unicode funciona en nombres y descripciones.
- El archivo no contiene rutas, tosu, logs ni preferencias globales.
- Un perfil exportado en Windows se importa en Linux y viceversa.

### Importación

- Importar un perfil nuevo lo agrega al catálogo y lo persiste.
- Reimportar el mismo GUID permite actualizar o crear una copia.
- Los nombres duplicados reciben un sufijo único.
- Los nombres reservados no reemplazan presets.
- JSON corrupto, demasiado grande o incompatible es rechazado.
- Configuraciones fuera de rango son rechazadas sin modificar el archivo local.

### Regresión

- Todas las pruebas existentes del randomizer continúan pasando.
- H-Random y S-Random mantienen exactamente sus valores actuales.
- La generación de mapas produce los mismos resultados para la misma configuración y seed.
- El flujo manual y la detección automática de beatmaps no cambian.

## Orden de implementación completado

1. Identidad estable y `CustomConfig`: completado.
2. Migración idempotente y pruebas: completado.
3. Custom persistente único: completado.
4. Nombres reservados y acciones protegidas: completado.
5. Formato `.hrp-profile.json` en Core: completado.
6. Validación, límite de 256 KB y escritura atómica: completado.
7. Importación/exportación con preview y conflictos: completado.
8. Pruebas automatizadas: completado; smoke start Windows aprobado y playtests interactivos Windows/Linux pendientes en el checklist.
9. Documentación: completada localmente.
10. Artefactos: regenerados y auditados al final de esta intervención.

## Criterios de aceptación

El trabajo se considera completo cuando:

- Custom es único, editable y persiste entre sesiones.
- Guardar Custom nunca crea otro Custom.
- H-Random y S-Random permanecen inmutables.
- Los perfiles personales pueden exportarse e importarse entre Windows y Linux.
- No se comparte información personal ni configuración de plataforma.
- Conflictos de ID y nombre se resuelven de forma visible y sin pérdida de datos.
- Las configuraciones antiguas migran automáticamente.
- El motor del randomizer y sus resultados no cambian.
- Las pruebas automatizadas y los playtests de ambas plataformas son satisfactorios.

## Fuera de alcance

- Implementar soporte para osu!lazer.
- Cambiar el algoritmo H-Random/S-Random.
- Sincronización en la nube o catálogo público de perfiles.
- Firmas digitales o cuentas de usuario.
- Packs de múltiples perfiles en la primera versión.

Los packs podrían añadirse posteriormente mediante un formato `.hrp-pack.json` que contenga una colección de los mismos objetos de perfil, sin cambiar el formato individual.
