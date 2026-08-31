# Comprobaciones manuales de Linux posteriores a la auditoría — v0.2.1

Estas comprobaciones se dejan intencionalmente al propietario en una instalación Linux real o en la VM preparada. Cada caso límite corregido cuenta con cobertura automatizada; esta lista solo reúne confirmaciones útiles a nivel de plataforma.

- [ ] Iniciar HRandomPlus después de que lazer haya producido un runtime log de más de 2 MiB. Confirmar que encuentra el mapa actual sin volver a seleccionarlo.
- [ ] Con lazer detectado, cerrarlo y reabrirlo usando el mismo almacenamiento. Confirmar que HRandomPlus se reconecta y resuelve el mapa actual en vez de conservar la sesión anterior.
- [ ] Randomizar e importar un beatmapset normal de lazer con muchos recursos. Confirmar que la dificultad se importa y su audio/recursos permanecen disponibles.
- [ ] Cancelar o cerrar HRandomPlus mientras siga activa una operación auxiliar mediante Wine. Confirmar que no queda ningún proceso auxiliar huérfano.
- [ ] Si un beatmapset real contiene recursos cuyos nombres solo difieren por mayúsculas, procesarlo y confirmar que ambos sobreviven. No crear ni modificar un set personal únicamente para esta comprobación.

No requieren prueba manual: las secuencias UTF-8 divididas, el respaldo de configuración malformada, usos Realm duplicados, rechazo de traversal, rechazo por límites de extracción y propagación de cancelación son pruebas de regresión deterministas en `HRandomPlus.Tests`.
