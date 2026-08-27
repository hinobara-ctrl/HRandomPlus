# HRandomPlus v0.1.0-playtest

## Windows + osu!stable

- [ ] Abrir stable y detectar el mapa seleccionado.
- [ ] Cambiar de dificultad y confirmar que HRandomPlus cambia de mapa.
- [ ] Probar H-Random, S-Random y un perfil Custom.
- [ ] Guardar/cargar un perfil con seed y otro con seed vacía.
- [ ] Probar Whole map y Selected range.
- [ ] Randomize crea una dificultad nueva y el original queda intacto.
- [ ] Probar **Select .osu manually**.

## Linux + osu-winello + tosu

- [ ] Abrir stable con `osu-wine --tosu` y confirmar detección.
- [ ] Cambiar de dificultad y comprobar que cambia el mapa detectado.
- [ ] Cerrar tosu: la UI debe indicar desconexión; abrirlo y reconectar.
- [ ] Probar H-Random, S-Random, seed y fallback manual.
- [ ] Confirmar output junto al mapa y output central opcional.
- [ ] Si no aparece automáticamente, usar F5 y confirmar que el output funciona.
- [ ] Probar paths con espacios/Unicode.
- [ ] Cerrar HRandomPlus y confirmar que no quedan procesos auxiliares.
- [ ] Ejecutar las pruebas A/B de [docs/LINUX_IMPORT_AB_TEST.md](docs/LINUX_IMPORT_AB_TEST.md).
