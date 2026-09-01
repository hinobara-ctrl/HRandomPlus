# HRandomPlus

Aplicación multiplataforma para crear dificultades H-Random, S-Random o Custom de osu!mania. Conserva la integración de osu!stable en Windows/Linux y añade detección e importación nativas para osu!lazer en ambos sistemas. Todas las fuentes usan exactamente el mismo motor de randomización.

Versión de desarrollo: **v0.2.1-playtest**.

## Funciones

- Presets protegidos H-Random/S-Random, Custom persistente y perfiles personales con identidad propia.
- Importación y exportación segura de perfiles `.hrp-profile.json` entre Windows y Linux.
- Seeds reproducibles, guardadas también en perfiles personalizados, y rangos parciales.
- Protección de long notes y validación antes de escribir.
- Modo opcional **Preserve dual stages (10K+)**: evita cruces directos entre los stages laterales y trata el centro impar como compartido; permanece deshabilitado en mapas inferiores a 10K.
- Detección de trills consistente entre scoring y estadísticas, incluidos acordes compatibles y corte tras pausas prolongadas.
- Detección de osu!stable en Windows aunque osu!lazer esté abierto.
- Detección read-only de osu!lazer mediante su log y catálogo Realm, sin tosu ni lectura de memoria.
- Importación segura a lazer mediante una variante local `.osz`; no modifica `client.realm` ni los blobs originales.
- Detección Linux sin `sudo`, lector de memoria propio ni conversión de letras Wine.
- Estado de origen inequívoco: osu!stable usa memoria en Windows y tosu en Linux; osu!lazer se detecta nativamente en ambos sistemas, y la selección manual permanece como fuente separada.
- Selector manual `.osu` para osu!stable en ambas plataformas.
- Salida con nombre único, `BeatmapID:0` y original intacto.
- Referencia compacta y editable de BPM a milisegundos para snaps de 1/1 a 1/64; los mapas con varios BPM muestran únicamente su rango.
- Versión y nombre de archivo únicos incluso al repetir el mismo perfil antes de que osu! refresque.

## Windows

La compilación `net8.0-windows` conserva `OsuMemoryDataProvider` como fuente automática. HRandomPlus y osu!stable deben ejecutarse con el mismo nivel de permisos. Como el reader disponible se vincula por nombre y no por PID, la detección automática de stable sólo continúa cuando existe un único proceso llamado `osu!`; ante ambigüedad, cierra las otras instancias o usa **Select .osu manually**. **Configure osu!stable** sólo ayuda a localizar una instalación y no reemplaza la identidad del proceso en ejecución.

La salida predeterminada de Windows continúa creándose junto al beatmap original.

## osu!lazer nativo (Windows y Linux)

1. Abre osu!lazer y entra en Song Select.
2. HRandomPlus localiza el almacenamiento oficial, lee el log runtime activo (incluidos los nombres actuales `<timestamp>.runtime.log`) incrementalmente y resuelve la dificultad contra `client.realm` en modo dinámico y read-only.
3. Pulsa **Randomize**. El `.osu` generado se conserva en la carpeta de salida de HRandomPlus y se envía a lazer dentro de un `.osz` con los recursos del set.
4. lazer importa una variante local con IDs desligados (`BeatmapID:0` y `BeatmapSetID:0`); el set y los blobs originales permanecen intactos.

No hacen falta tosu, Wine, `sudo` ni configurar una carpeta `Songs` para lazer nativo. Se reconocen las rutas estándar `%APPDATA%\osu` y `~/.local/share/osu`, el `FullPath` de `storage.ini` y almacenamientos portables compatibles junto al ejecutable. Si el log actual solo publica el nombre visible y más de una dificultad coincide, HRandomPlus deja la selección sin resolver en vez de elegir un mapa arbitrario.

La sección **Platform and output** configura exclusivamente la integración de osu!stable para Linux mediante osu!-Wine/tosu. Permanece deshabilitada en Windows y mientras osu!lazer sea el origen activo.

Mientras osu!lazer sea el origen activo, **Select .osu manually** y los controles de configuración de osu!stable permanecen deshabilitados. La selección manual es un fallback exclusivo del flujo stable.

Cuando stable y lazer están abiertos al mismo tiempo, cada adaptador conserva su identidad y gana la selección que haya cambiado más recientemente. **Select .osu manually** sigue teniendo prioridad hasta que el juego cambie realmente de mapa.

## Linux nativo con osu-winello

1. Inicia osu!stable y tosu en el mismo entorno Wine; osu-winello ofrece `osu-wine --tosu`.
2. Comprueba que `http://127.0.0.1:24050/json/v2` responde.
3. Ejecuta HRandomPlus nativamente en Linux, no bajo Wine.
4. Selecciona una dificultad en osu!stable.

Después de extraer el ZIP, marca los ejecutables como ejecutables si el gestor de archivos no conservó el permiso:

```bash
chmod +x HRandomPlus
```

La ruta de osu-winello se lee desde:

```text
$XDG_DATA_HOME/osuconfig/osupath
~/.local/share/osuconfig/osupath
```

Para instalaciones personalizadas, usa **Configure native osu! path** y selecciona la raíz que contiene `Songs`. No hace falta `sudo`.

Si eliges un `.osu` con **Select .osu manually**, esa selección conserva prioridad mientras osu! siga mostrando el mismo mapa que tosu ya había detectado. Al cambiar de mapa dentro de osu!, HRandomPlus retoma la detección automática.

Las configuraciones nuevas de Linux escriben de forma predeterminada junto al beatmap. Desmarcar **Write beside the original beatmap** usa:

```text
$XDG_DATA_HOME/HRandomPlus/Generated Beatmaps
~/.local/share/HRandomPlus/Generated Beatmaps
```

Una preferencia ya guardada se respeta al actualizar. La aplicación siempre conserva el `.osu` generado aunque falle la actualización/importación.

Cuando se escribe junto al beatmap, HRandomPlus genera primero una copia segura y usa `osu-wine --wine winepath -w` más una copia ejecutada dentro del mismo entorno Wine de osu!stable. Las rutas viajan en variables de entorno, fuera del texto interpretado por `cmd`, con expansión retardada desactivada; no se construyen rutas `Z:` manualmente. Si Wine, `winepath` o la copia fallan, usa copia Linux nativa y avisa que osu! puede requerir F5; si también falla el fallback, conserva el output central para importación manual.

La [prueba A/B real](docs/LINUX_IMPORT_AB_TEST.md) confirmó que la copia Linux nativa necesitó F5 mientras la copia Wine-side fue detectada sin F5. Esto verifica el comportamiento funcional, no la API o mecanismo interno exacto de notificación del filesystem.

## Diagnóstico tosu

```bash
dotnet HRandomPlus.Cli.dll --diagnose
dotnet HRandomPlus.Cli.dll --diagnose --host 127.0.0.1 --port 24050
dotnet HRandomPlus.Cli.dll --diagnose --osu-path /ruta/nativa/osu
```

El diagnóstico es de solo lectura. Imprime plataforma, fuentes, URL/estado de tosu, Winello, raíz osu!, `Songs`, mapa, ruta `.osu`, existencia y output previsto. No genera ni importa archivos. Códigos: `0` correcto, `3` tosu no disponible y `4` conectado pero sin beatmap resoluble.

Las rutas dentro del directorio personal se redactan como `%USERPROFILE%` en Windows o `$HOME` en Linux para que la salida pueda compartirse con menor exposición de datos locales.

## Configuración

Windows conserva `%LOCALAPPDATA%\HRandomPlus`. Linux sigue XDG:

- Configuración: `$XDG_CONFIG_HOME/HRandomPlus/config.json`.
- Datos y salidas: `$XDG_DATA_HOME/HRandomPlus`.
- Log: `$XDG_STATE_HOME/HRandomPlus/logs/latest.log`.

Una configuración con JSON corrupto se respalda antes de restaurar defaults. Los fallos transitorios de lectura o permisos no sobrescriben el archivo original ni impiden iniciar la aplicación.

`MaxCandidateSets` admite de 1 a 8192 (default 4096) y `WeightedTopCandidates` no puede superarlo. Los valores persistidos por versiones anteriores se ajustan de forma conservadora al cargar. `DifficultySuffix` permite Unicode normal, pero rechaza caracteres y terminaciones que producirían filenames no portables entre Windows y Linux.

`PreserveDualStages` es configurable por perfil. Cuando está activo en 10K o superior, las notas laterales se randomizan dentro de su stage sin cruzar directamente al opuesto. En keymodes impares la columna central es compartida: puede intercambiar notas con ambos stages, pero continúa siendo neutral únicamente para el cálculo de Hand Balance. En mapas inferiores a 10K la opción no se aplica y aparece deshabilitada.

Un acorde continúa un trill cuando contiene la columna alternante esperada pero no la columna anterior; las demás columnas actúan como acompañamiento. Un acorde con ambas columnas rompe la secuencia. Una separación mayor que `4 × MaxThresholdMs` corta el trill y una pausa mayor que `8 × MaxThresholdMs` devuelve Dynamic Threshold a `BaseThresholdMs`.

## Perfiles

- **H-Random** y **S-Random** son presets protegidos: siempre se reconstruyen desde los valores del código y no pueden sobrescribirse ni eliminarse.
- **Custom** es un único perfil editable. **Save Custom** conserva todos sus parámetros y la seed en la configuración personal; **Reset Custom** restaura sus valores iniciales después de pedir confirmación.
- **Duplicate** crea una variante personal independiente con GUID nuevo. Solo los perfiles personales pueden eliminarse.
- **Export profile** genera un archivo UTF-8 `.hrp-profile.json` con el nombre, descripción, GUID, versiones de formato/motor y todos los parámetros del randomizer.
- **Import profile** valida el archivo y muestra una previsualización. Si el GUID ya existe permite actualizarlo o importar una copia; los nombres repetidos reciben un sufijo y `H-Random`, `S-Random` y `Custom` están reservados.

Los perfiles exportados nunca incluyen rutas de osu!, tosu, preferencias de output, logs ni información del mapa actual. Los perfiles importados se copian al `config.json` personal, por lo que el archivo descargado puede eliminarse después. Las configuraciones de versiones anteriores se migran automáticamente: el último perfil histórico llamado Custom se convierte en el Custom persistente y los demás se conservan con nombres únicos.

## Arquitectura

```text
HRandomPlus.Core         motor, parser, archivos, perfiles y validación
HRandomPlus.Integration  stable/tosu, osu-winello, osu!lazer Realm/log/import y contratos
HRandomPlus.Desktop      UI Avalonia para Windows/Linux
HRandomPlus.Cli          diagnóstico y procesamiento .osz
HRandomPlus.Tests        regresión portable e integración
```

La UI y las fuentes no contienen lógica de random. El motor no conoce el sistema operativo ni el origen del mapa.

## Compilación

Los binarios apuntan a `net8.0`/`net8.0-windows`. El workflow oficial usa el SDK .NET 10 estable para compilar esos targets y mantener compatibilidad con los analizadores actuales de Avalonia. El SDK .NET 8 también puede compilar el proyecto, aunque puede mostrar advertencias `CS9057` por la versión de Roslyn.

```bash
dotnet restore HRandomPlus.sln --locked-mode
dotnet build HRandomPlus.sln -c Release
dotnet run --project tests/HRandomPlus.Tests/HRandomPlus.Tests.csproj -c Release
```

Linux x64 dependiente del framework:

```bash
dotnet publish src/HRandomPlus.Desktop/HRandomPlus.Desktop.csproj \
  -c Release -f net8.0 -r linux-x64 --self-contained false \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -o publish/linux-x64-framework-dependent

dotnet publish src/HRandomPlus.Cli/HRandomPlus.Cli.csproj \
  -c Release -r linux-x64 --self-contained false \
  -p:PublishSingleFile=true -o publish/linux-x64-cli-framework-dependent
```

Windows x64 Avalonia:

```powershell
dotnet publish src/HRandomPlus.Desktop/HRandomPlus.Desktop.csproj `
  -c Release -f net8.0-windows -r win-x64 --self-contained false `
  -p:PublishSingleFile=true -o publish/windows-x64-framework-dependent
```

### Variantes de distribución

La distribución vigente tiene exactamente dos artefactos binarios principales: `HRandomPlus-v0.2.1-playtest-windows-x64-framework-dependent.zip` y `HRandomPlus-v0.2.1-playtest-linux-x64-framework-dependent.zip`. Ambos requieren **.NET Runtime 8 x64** instalado. Los paquetes autocontenidos dejaron de formar parte de la distribución normal.

Las fuentes exactas de HRandomPlus y el snapshot GPL correspondiente se publican como assets adicionales de cumplimiento y reproducibilidad; no son variantes binarias de la aplicación.

## CLI OSZ

```bash
HRandomPlus.Cli beatmap.osz --seed 123456 --config config.example.json
```

La lectura y extracción de `.osz` usa límites generosos y copia por streaming para rechazar archivos patológicos sin cargar recursos grandes completos en memoria.

## Pruebas

El runner cubre parser, BPM/snaps, OSZ, perfiles/seed, configuración y defaults, nombres repetidos seguros, salida, rangos, seeds reproducibles, keymodes 1K–18K, long notes, JSON/reconexión/timeout de tosu, estados connected/disconnected, rutas Winello, E2E tosu simulado, copia Wine-side y sus fallbacks, logs/rotación/storage/blob/ambigüedad/arbitraje de lazer y creación segura del `.osz` de importación.

Antes de reportar resultados de una máquina real usa [PLAYTEST_CHECKLIST.md](PLAYTEST_CHECKLIST.md).

## Licencias

HRandomPlus se distribuye bajo `GPL-3.0-or-later`; el texto completo está en [LICENSE](LICENSE). Consulta [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) y la [auditoría de dependencias](docs/DEPENDENCY_LICENSE_AUDIT.md) antes de redistribuir binarios.

La compilación Windows incorpora `OsuMemoryDataProvider 0.12.2` y `ProcessMemoryDataFinder 0.10.2`, también `GPL-3.0-or-later`. Cada Release debe adjuntar tanto las fuentes exactas de HRandomPlus como el snapshot upstream del commit `122dd102fe272de30471cf1f317805cb49ac23a4`; consulta [el manifiesto de fuentes GPL](docs/GPL_SOURCE_MANIFEST.md). Los demás componentes conservan sus propias licencias y avisos. Linux consume la API HTTP de tosu y no incorpora los componentes GPL de lectura de memoria. Los dos paquetes distribuidos dependen del framework y no redistribuyen .NET.

Los artefactos de GitHub Actions son temporales. Los ZIP y `SHA256SUMS.txt` solo se convierten en descargas estables cuando el propietario crea una GitHub Release asociada a un tag.
