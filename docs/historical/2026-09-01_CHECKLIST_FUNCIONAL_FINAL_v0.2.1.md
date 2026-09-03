<!-- document-status: historical -->
> Tipo de documento: evidencia histórica de v0.2.1; no es autoritativo para el HEAD actual.

# Checklist funcional final — v0.2.1

Objetivo: confirmar que los binarios posteriores al endurecimiento siguen funcionando en integraciones reales, sin repetir comprobaciones ya demostradas explícitamente.

Base histórica del checklist: `ef9a30f`. Las comprobaciones manuales se revalidaron sobre el working tree incorporado después en `d5882cd`, incluida la alternancia Windows entre stable y lazer. La distribución vigente contiene un paquete framework-dependent por plataforma.

## Evidencia ya cerrada — no repetir

- [x] Compilación Release de toda la solución: 0 errores.
- [x] Suite automatizada histórica: 188 aprobadas, 0 fallidas; la misma suite precompilada volvió a pasar sin build durante la auditoría local.
- [x] Flujo framework-dependent sin .NET 8, enlace de instalación, instalación y reapertura validado.
- [x] ZIP sin PDB, con README, configuración, licencia y avisos aplicables.
- [x] La necesidad de conservar variantes self-contained por el flujo de instalación quedó descartada.
- [x] Motor 1K–18K, seeds, H-Random/S-Random, long notes, rangos, parser y validación cubiertos automáticamente.
- [x] Persistencia, migración, duplicación, importación/exportación y conflictos de perfiles cubiertos automáticamente y ya aprobados en playtests anteriores.
- [x] Corrupción/transitorios de `config.json`, límites de `.osz`, traversal, duplicados Realm, Unicode dividido, cancelación y sensibilidad de mayúsculas cubiertos por regresiones deterministas.

No es necesario recorrer todos los parámetros ni repetir cada perfil. Para el end-to-end basta una generación H-Random y una S-Random representativas.

## Datos que registrar

- Sistemas: Windows y Linux nativos, comprobados por el propietario.
- Paquetes: candidatos framework-dependent v0.2.1 para Windows x64 y Linux x64.
- Clientes: osu!stable y osu!lazer; tosu/osu-winello/Wine donde corresponde.
- Resultado: todos los puntos P0 y P1 aprobados. El arbitraje con stable y lazer abiertos simultáneamente fue la última comprobación manual y resultó correcto.

## P0 — gate mínimo en Windows — APROBADO

### Paquete y arranque

- [x] Extraer `HRandomPlus-v0.2.1-playtest-windows-x64-framework-dependent.zip` en una carpeta nueva, verificar su SHA-256 y abrir `HRandomPlus.exe` desde esa extracción.
- [x] Sin osu! abierto, confirmar que la UI sigue respondiendo, muestra un estado de espera y permite cerrarla normalmente.

### osu!stable

- [x] Abrir solo osu!stable, seleccionar un mapa mania y confirmar detección automática con la etiqueta de stable, nunca tosu.
- [x] Cambiar de dificultad y confirmar que HRandomPlus actualiza artista/título/dificultad.
- [x] Generar una vez con H-Random y una vez con S-Random. Confirmar dificultad nueva, nombres únicos, original intacto y detección por osu!stable.
- [x] Usar **Select .osu manually** una vez y confirmar que el selector abre, la UI no se congela y el mapa elegido se mantiene.

### osu!lazer y cambio de fuentes

- [x] Sin reiniciar HRandomPlus, cerrar stable, abrir lazer y entrar a Song Select. Confirmar detección y etiqueta explícita de osu!lazer.
- [x] Con lazer activo, confirmar que los controles exclusivos de stable quedan deshabilitados.
- [x] Generar/importar una dificultad. Confirmar que aparece en lazer, carga audio/fondo/recursos y el beatmap original permanece intacto.
- [x] Repetir una generación y confirmar nombre de dificultad único.
- [x] Cerrar y reabrir lazer usando el mismo almacenamiento. Confirmar que HRandomPlus resuelve el mapa actual y no retiene la sesión anterior.
- [x] Volver de lazer a stable sin reiniciar HRandomPlus. Confirmar que la lectura por memoria vuelve a funcionar y los controles de stable se reactivan.
- [x] Abrir stable y lazer simultáneamente; cambiar una selección en cada uno y confirmar que gana la selección modificada más recientemente con la etiqueta correcta.

## P0 — gate mínimo en Linux — APROBADO POR EL PROPIETARIO

### Paquete y arranque

- [x] Verificar `SHA256SUMS.txt`, extraer `HRandomPlus-v0.2.1-playtest-linux-x64-framework-dependent.zip` en una carpeta nueva y confirmar que `./HRandomPlus` abre sin `sudo` con .NET 8 x64 instalado.
- [x] Sin ningún osu! abierto, confirmar UI responsiva, estado de espera y cierre limpio.

### osu!stable mediante osu-winello/tosu

- [x] Abrir stable con `osu-wine --tosu`; confirmar mapa actual, cambios de dificultad y etiqueta automática de tosu.
- [x] Generar H-Random y S-Random junto al beatmap. Confirmar que Wine copia la dificultad, osu! la detecta sin F5 y el original queda intacto.
- [x] Repetir desde una ruta real con espacios, apóstrofe, `!`, acentos o Unicode.
- [x] Elegir manualmente un `.osu` mientras tosu informa el mismo mapa; confirmar que la selección manual permanece. Cambiar luego de mapa en osu! y confirmar que el modo automático recupera el control.
- [x] Cerrar y reabrir tosu y osu!stable; confirmar desconexión/reconexión sin reiniciar HRandomPlus.

### osu!lazer nativo y cambio de fuentes

- [x] Abrir lazer nativo sin tosu/Wine y confirmar detección del mapa actual sin `sudo`.
- [x] Iniciar HRandomPlus cuando el runtime log de lazer ya supere 2 MiB; confirmar que encuentra la selección sin volver a elegirla.
- [x] Generar/importar una dificultad con recursos. Confirmar audio/fondo, original intacto y nombre único al repetir.
- [x] Cerrar y reabrir lazer con el mismo almacenamiento; confirmar invalidación de la sesión anterior y resolución del mapa actual.
- [x] Cambiar entre stable/tosu y lazer sin reiniciar HRandomPlus; si ambos están abiertos, confirmar arbitraje por selección más reciente y controles correctos.
- [x] Cerrar HRandomPlus durante o después de una operación mediante Wine y confirmar que no quedan procesos auxiliares huérfanos.

## P1 — regresiones recomendadas — APROBADAS

- [x] Windows: repetir la importación de lazer usando el `storage.ini` personalizado ya conocido, especialmente cierre → reapertura sobre el mismo storage.
- [ ] Linux: confirmar copia Wine-side normal y, si también falla la copia nativa, creación del `.osz` en `Failed Imports` junto al ejecutable.
- [x] Windows/Linux: mantener HRandomPlus abierto 10–15 minutos alternando mapas; confirmar que no se congela, no pierde la fuente activa y no aumenta la memoria de forma continua.
- [x] Windows/Linux: cerrar la aplicación durante una detección normal y confirmar salida rápida sin procesos remanentes.

## P2 — solo si existe un caso real

No se dispuso de estos fixtures excepcionales durante el cierre. Según el criterio de este checklist, no bloquean la publicación y no se marcan artificialmente como ejecutados.

- [ ] Procesar un beatmapset cuyos recursos difieran únicamente por mayúsculas y confirmar que ambos sobreviven.
- [ ] Probar una ruta Wine válida con `&`, `%` o `^`. No fabricar nombres inválidos en Windows (`|`, `<`, `>`), ya cubiertos automáticamente.
- [ ] Probar un beatmapset excepcionalmente grande con muchos recursos para confirmar que los límites generosos no afectan contenido legítimo.

## Criterio de cierre

La build quedó funcionalmente confirmada por el propietario: todos los puntos P0 de Windows y Linux y los P1 fueron aprobados. Los P2 no bloquean porque no existió un fixture real legítimo. No se exigió simular el fallo artificial del launcher de lazer.
