# HRandomPlus v0.1.0-playtest

## Windows + osu!stable

- [x] Abrir stable y detectar el mapa seleccionado.
- [x] Cambiar de dificultad y confirmar que HRandomPlus cambia de mapa.
- [x] Probar H-Random, S-Random y un perfil Custom.
- [x] Guardar/cargar un perfil con seed y otro con seed vacía.
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
