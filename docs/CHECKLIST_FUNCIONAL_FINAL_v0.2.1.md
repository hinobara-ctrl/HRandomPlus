# Checklist funcional final — v0.2.1

Objetivo: confirmar que los binarios posteriores al endurecimiento siguen funcionando en integraciones reales, sin repetir comprobaciones ya demostradas explícitamente.

Código del binario: `ef9a30f`. Documentación actual: `8b6e9be`. Los cuatro paquetes que deben probarse están en `artifacts/latest/`.

## Evidencia ya cerrada — no repetir

- [x] Compilación Release de toda la solución: 0 errores.
- [x] Suite automatizada: 146 aprobadas, 0 fallidas.
- [x] Publicación de las cuatro variantes Windows/Linux completada.
- [x] Integridad de los cuatro ZIP y `SHA256SUMS.txt` verificada.
- [x] ZIP sin PDB, con README, configuración, licencia y avisos aplicables.
- [x] Permiso ejecutable almacenado en ambos ZIP Linux.
- [x] Binario Windows autocontenido iniciado y cerrado correctamente cinco veces; la UI respondió.
- [x] Motor 4K–9K, seeds, H-Random/S-Random, long notes, rangos, parser y validación cubiertos automáticamente.
- [x] Persistencia, migración, duplicación, importación/exportación y conflictos de perfiles cubiertos automáticamente y ya aprobados en playtests anteriores.
- [x] Corrupción/transitorios de `config.json`, límites de `.osz`, traversal, duplicados Realm, Unicode dividido, cancelación y sensibilidad de mayúsculas cubiertos por regresiones deterministas.

No es necesario recorrer todos los parámetros ni repetir cada perfil. Para el end-to-end basta una generación H-Random y una S-Random representativas.

## Datos que registrar

- Sistema operativo y versión:
- Paquete exacto probado:
- osu!stable / osu!lazer y versión:
- tosu / osu-winello / Wine, si aplica:
- Resultado y observaciones:

## P0 — gate mínimo en Windows

### Paquete y arranque

- [ ] Extraer `HRandomPlus-v0.2.1-playtest-windows-x64.zip` en una carpeta nueva, verificar su SHA-256 y abrir `HRandomPlus.exe` desde esa extracción.
- [ ] Sin osu! abierto, confirmar que la UI sigue respondiendo, muestra un estado de espera y permite cerrarla normalmente.
- [ ] Extraer y abrir una vez la variante `windows-x64-framework-dependent`; confirmar que funciona con .NET 8 x64 instalado o que informa claramente el requisito si falta.

### osu!stable

- [ ] Abrir solo osu!stable, seleccionar un mapa mania y confirmar detección automática con la etiqueta de stable, nunca tosu.
- [ ] Cambiar de dificultad y confirmar que HRandomPlus actualiza artista/título/dificultad.
- [ ] Generar una vez con H-Random y una vez con S-Random. Confirmar dificultad nueva, nombres únicos, original intacto y detección por osu!stable.
- [ ] Usar **Select .osu manually** una vez y confirmar que el selector abre, la UI no se congela y el mapa elegido se mantiene.

### osu!lazer y cambio de fuentes

- [ ] Sin reiniciar HRandomPlus, cerrar stable, abrir lazer y entrar a Song Select. Confirmar detección y etiqueta explícita de osu!lazer.
- [ ] Con lazer activo, confirmar que los controles exclusivos de stable quedan deshabilitados.
- [ ] Generar/importar una dificultad. Confirmar que aparece en lazer, carga audio/fondo/recursos y el beatmap original permanece intacto.
- [ ] Repetir una generación y confirmar nombre de dificultad único.
- [ ] Cerrar y reabrir lazer usando el mismo almacenamiento. Confirmar que HRandomPlus resuelve el mapa actual y no retiene la sesión anterior.
- [ ] Volver de lazer a stable sin reiniciar HRandomPlus. Confirmar que la lectura por memoria vuelve a funcionar y los controles de stable se reactivan.
- [ ] Abrir stable y lazer simultáneamente; cambiar una selección en cada uno y confirmar que gana la selección modificada más recientemente con la etiqueta correcta.

## P0 — gate mínimo en Linux, a cargo del propietario

### Paquete y arranque

- [ ] Verificar `SHA256SUMS.txt`, extraer `HRandomPlus-v0.2.1-playtest-linux-x64.zip` en una carpeta nueva y confirmar que `./HRandomPlus` abre sin `sudo`.
- [ ] Abrir una vez la variante `linux-x64-framework-dependent` con .NET 8 x64 instalado.
- [ ] Sin ningún osu! abierto, confirmar UI responsiva, estado de espera y cierre limpio.

### osu!stable mediante osu-winello/tosu

- [ ] Abrir stable con `osu-wine --tosu`; confirmar mapa actual, cambios de dificultad y etiqueta automática de tosu.
- [ ] Generar H-Random y S-Random junto al beatmap. Confirmar que Wine copia la dificultad, osu! la detecta sin F5 y el original queda intacto.
- [ ] Repetir desde una ruta real con espacios, apóstrofe, `!`, acentos o Unicode.
- [ ] Elegir manualmente un `.osu` mientras tosu informa el mismo mapa; confirmar que la selección manual permanece. Cambiar luego de mapa en osu! y confirmar que el modo automático recupera el control.
- [ ] Cerrar y reabrir tosu y osu!stable; confirmar desconexión/reconexión sin reiniciar HRandomPlus.

### osu!lazer nativo y cambio de fuentes

- [ ] Abrir lazer nativo sin tosu/Wine y confirmar detección del mapa actual sin `sudo`.
- [ ] Iniciar HRandomPlus cuando el runtime log de lazer ya supere 2 MiB; confirmar que encuentra la selección sin volver a elegirla.
- [ ] Generar/importar una dificultad con recursos. Confirmar audio/fondo, original intacto y nombre único al repetir.
- [ ] Cerrar y reabrir lazer con el mismo almacenamiento; confirmar invalidación de la sesión anterior y resolución del mapa actual.
- [ ] Cambiar entre stable/tosu y lazer sin reiniciar HRandomPlus; si ambos están abiertos, confirmar arbitraje por selección más reciente y controles correctos.
- [ ] Cerrar HRandomPlus durante o después de una operación mediante Wine y confirmar que no quedan procesos auxiliares huérfanos.

## P1 — regresiones recomendadas

- [ ] Windows: repetir la importación de lazer usando el `storage.ini` personalizado ya conocido, especialmente cierre → reapertura sobre el mismo storage.
- [ ] Linux: confirmar output central; no debe copiar silenciosamente otra dificultad a `Songs`.
- [ ] Windows/Linux: mantener HRandomPlus abierto 10–15 minutos alternando mapas; confirmar que no se congela, no pierde la fuente activa y no aumenta la memoria de forma continua.
- [ ] Windows/Linux: cerrar la aplicación durante una detección normal y confirmar salida rápida sin procesos remanentes.

## P2 — solo si existe un caso real

- [ ] Procesar un beatmapset cuyos recursos difieran únicamente por mayúsculas y confirmar que ambos sobreviven.
- [ ] Probar una ruta Wine válida con `&`, `%` o `^`. No fabricar nombres inválidos en Windows (`|`, `<`, `>`), ya cubiertos automáticamente.
- [ ] Probar un beatmapset excepcionalmente grande con muchos recursos para confirmar que los límites generosos no afectan contenido legítimo.

## Criterio de cierre

La build queda funcionalmente confirmada cuando todos los puntos P0 de Windows y Linux están aprobados. Un fallo P0 bloquea la publicación; un fallo P1 requiere evaluación; P2 no bloquea si no existe un fixture real legítimo. No se exige simular el fallo artificial del launcher de lazer.
