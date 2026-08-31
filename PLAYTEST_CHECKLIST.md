# HRandomPlus v0.2.1-playtest

Los checks de stable documentan regresiones ya verificadas. Los checks de lazer fueron completados por el propietario en equipos Windows y Linux reales; la cobertura automatizada los complementa, pero no los sustituye.

## Windows + osu!stable

- [x] Abrir stable y detectar el mapa seleccionado.
- [x] Regresión automatizada: la detección por memoria muestra **Beatmap detected automatically from osu!stable** y no menciona tosu.
- [x] Cambiar de dificultad y confirmar que HRandomPlus cambia de mapa.
- [x] Probar H-Random, S-Random y un perfil Custom.
- [x] Guardar/cargar un perfil con seed y otro con seed vacía.
- [x] Guardar Custom dos veces, reiniciar y confirmar que existe un único Custom con los últimos valores.
- [x] Duplicar H-Random, S-Random y Custom; confirmar GUID/config independiente y eliminación solo de las copias.
- [x] Exportar/importar un `.hrp-profile.json`, revisar preview y probar Update, Import as copy, nombre repetido y nombre reservado.
- [x] Probar Whole map y Selected range.
- [x] Randomize crea una dificultad nueva y el original queda intacto.
- [x] Probar **Select .osu manually**.

## Linux + osu-winello + tosu

- [x] Abrir stable con `osu-wine --tosu` y confirmar detección.
- [x] Cambiar de dificultad y comprobar que cambia el mapa detectado.
- [x] Cerrar tosu: la UI debe indicar desconexión y retener claramente el último mapa.
- [x] Abrir tosu nuevamente sin cambiar el mapa: la UI debe volver a conectado.
- [x] Cerrar/reabrir osu! y confirmar que el estado vuelve a actualizarse.
- [x] Probar H-Random, S-Random, seed y fallback manual.
- [x] Importar en Linux un perfil exportado en Windows y exportarlo nuevamente sin perder parámetros, Unicode ni seed.
- [x] Generar S-Random tres veces sobre el mismo mapa sin cambiar selección: Version y filename deben ser únicos.
- [x] Repetir el caso con H-Random y Custom.
- [x] Con output junto al mapa, confirmar que Wine-side aparece sin F5.
- [x] Con output central, confirmar que no se copia una segunda salida a `Songs`.
- [x] Fallback conserva el output y recomienda F5 (**mock automatizado**; no se provocó un fallo real en la VM).
- [x] Paths con espacios, apóstrofe, `!`, acentos y Unicode (**integración automatizada**).
- [x] Confirmar la tabla BPM/snaps y editar manualmente el BPM.
- [x] Cerrar HRandomPlus y confirmar que no quedan procesos auxiliares.
- [x] Ejecutar las pruebas A/B de [docs/LINUX_IMPORT_AB_TEST.md](docs/LINUX_IMPORT_AB_TEST.md).
- [x] En el build `r2`, confirmar que un mapa resuelto mediante la ruta configurada aparece como detección automática por tosu, no como selección manual.
- [x] Seleccionar un `.osu` manual mientras tosu detecta un mapa: la selección manual permanece mientras osu! siga en ese mismo mapa.
- [x] Después de la prueba manual anterior, cambiar de mapa dentro de osu!: la detección automática recupera el control con el mapa nuevo.

## osu!lazer nativo — Windows x64

- [x] Detectar lazer sin tosu, entrar a Song Select y mostrar **Beatmap detected automatically from osu!lazer**.
- [x] Con un `storage.ini` que apunte a otra unidad, confirmar que se usa el almacenamiento cuyo `<timestamp>.runtime.log` está activo y que carga el mapa actual.
- [x] Cambiar dificultad/set y comprobar que la selección se actualiza.
- [x] Generar e importar H-Random, S-Random y Custom; confirmar recursos, original intacto e IDs locales.
- [x] Repetir una generación y confirmar nombres únicos.
- [x] Cerrar/reabrir lazer y confirmar desconexión/reconexión sin congelar la UI.
- [x] Volver de lazer a stable sin reiniciar HRandomPlus y confirmar que el lector de memoria se reconecta.
- [x] Con lazer como origen activo, confirmar que **Select .osu manually** y **Configure osu!stable** están deshabilitados y que se reactivan al salir de lazer.
- [x] Abrir stable y lazer juntos y confirmar que gana la selección modificada más recientemente con la etiqueta de origen correcta.

## osu!lazer nativo — Linux x64

- [x] Repetir detección, cambio de mapa, generación e importación con lazer nativo, sin Wine/tosu/sudo.
- [x] Probar almacenamiento estándar y uno configurado por `storage.ini`.
- [x] Confirmar que el flujo stable + osu-winello + tosu sigue funcionando por separado.

Consulta el procedimiento y las limitaciones en [docs/LAZER_IMPLEMENTATION.md](docs/LAZER_IMPLEMENTATION.md).

## Delta v0.2.1 — comprobación manual del propietario en Linux

Las marcas anteriores registran pruebas reales ya completadas, incluido `storage.ini` personalizado en ambos sistemas. Los siguientes casos corresponden únicamente a las rutas endurecidas en v0.2.1 y quedan preparados para ejecución manual en Linux:

- [ ] Con osu!stable/osu-winello, importar junto al mapa desde una ruta con espacios, apóstrofe, `!`, acentos o Unicode y confirmar detección sin F5.
- [ ] Si existe una ruta real válida con algún metacarácter adicional (`&`, `%` o `^`), confirmar la misma copia Wine-side; `|`, `<` y `>` quedan cubiertos de forma automatizada porque no son nombres válidos en el lado Windows.
- [ ] En lazer nativo, actualizar o reimportar el mismo mapa sin reiniciar lazer y confirmar que HRandomPlus usa la revisión nueva.
- [ ] En lazer nativo, importar un mapa normal y confirmar audio/recursos; la ausencia simulada del audio principal y los nombres ZIP que sólo difieren por mayúsculas están cubiertos automáticamente.
