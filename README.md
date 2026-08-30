# HRandomPlus

Aplicación multiplataforma para crear dificultades H-Random, S-Random o Custom de osu!mania. Windows detecta osu!stable mediante lectura de memoria read-only; Linux obtiene el beatmap seleccionado desde tosu y resuelve su ruta nativa mediante osu-winello/XDG. Ambos usan exactamente el mismo motor.

Versión actual: **v0.1.0-playtest**. Está dirigida a osu!stable. **osu!lazer todavía no está soportado.**

## Funciones

- Presets protegidos H-Random/S-Random, Custom persistente y perfiles personales con identidad propia.
- Importación y exportación segura de perfiles `.hrp-profile.json` entre Windows y Linux.
- Seeds reproducibles, guardadas también en perfiles personalizados, y rangos parciales.
- Protección de long notes y validación antes de escribir.
- Detección de osu!stable en Windows aunque osu!lazer esté abierto.
- Detección Linux sin `sudo`, lector de memoria propio ni conversión de letras Wine.
- Estado de origen inequívoco: Windows indica osu!stable, Linux indica tosu y la selección manual permanece diferenciada.
- Selector manual `.osu` en ambas plataformas.
- Salida con nombre único, `BeatmapID:0` y original intacto.
- Referencia visual editable de BPM a milisegundos para snaps de 1/1 a 1/64.
- Version y filename únicos incluso al repetir el mismo perfil antes de que osu! refresque.

## Windows

La compilación `net8.0-windows` conserva `OsuMemoryDataProvider` como fuente automática. HRandomPlus y osu!stable deben ejecutarse con el mismo nivel de permisos. Si la detección falla, usa **Select .osu manually** o **Configure osu!stable** y selecciona la raíz que contiene `Songs`.

La salida predeterminada de Windows continúa creándose junto al beatmap original.

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

Cuando se escribe junto al beatmap, HRandomPlus genera primero una copia segura y usa `osu-wine --wine winepath -w` más `osu-wine --wine cmd copy` para materializarla desde el mismo entorno Wine de osu!stable. No construye rutas `Z:` manualmente. Si Wine, `winepath` o la copia fallan, usa copia Linux nativa y avisa que osu! puede requerir F5; si también falla el fallback, conserva el output central para importación manual.

La [prueba A/B real](docs/LINUX_IMPORT_AB_TEST.md) confirmó que la copia Linux nativa necesitó F5 mientras la copia Wine-side fue detectada sin F5. Esto verifica el comportamiento funcional, no la API o mecanismo interno exacto de notificación del filesystem.

## Diagnóstico tosu

```bash
dotnet HRandomPlus.Cli.dll --diagnose
dotnet HRandomPlus.Cli.dll --diagnose --host 127.0.0.1 --port 24050
dotnet HRandomPlus.Cli.dll --diagnose --osu-path /ruta/nativa/osu
```

El diagnóstico es de solo lectura. Imprime plataforma, fuentes, URL/estado de tosu, Winello, raíz osu!, `Songs`, mapa, ruta `.osu`, existencia y output previsto. No genera ni importa archivos. Códigos: `0` correcto, `3` tosu no disponible y `4` conectado pero sin beatmap resoluble.

## Configuración

Windows conserva `%LOCALAPPDATA%\HRandomPlus`. Linux sigue XDG:

- Configuración: `$XDG_CONFIG_HOME/HRandomPlus/config.json`.
- Datos y salidas: `$XDG_DATA_HOME/HRandomPlus`.
- Log: `$XDG_STATE_HOME/HRandomPlus/logs/latest.log`.

Una configuración corrupta restaura defaults y no impide iniciar la aplicación.

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
HRandomPlus.Integration  tosu, osu-winello, rutas y contratos
HRandomPlus.Desktop      UI Avalonia para Windows/Linux
HRandomPlus.Cli          diagnóstico y procesamiento .osz
HRandomPlus.Tests        regresión portable e integración
```

La UI y las fuentes no contienen lógica de random. El motor no conoce el sistema operativo ni el origen del mapa.

## Compilación

Los binarios apuntan a `net8.0`/`net8.0-windows`. El workflow oficial usa el SDK .NET 10 estable para compilar esos targets y mantener compatibilidad con los analizadores actuales de Avalonia. El SDK .NET 8 también puede compilar el proyecto, aunque puede mostrar advertencias `CS9057` por la versión de Roslyn.

```bash
dotnet restore HRandomPlus.sln
dotnet build HRandomPlus.sln -c Release
dotnet run --project tests/HRandomPlus.Tests/HRandomPlus.Tests.csproj -c Release
```

Linux x64 autocontenido:

```bash
dotnet publish src/HRandomPlus.Desktop/HRandomPlus.Desktop.csproj \
  -c Release -f net8.0 -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -o publish/linux-x64

dotnet publish src/HRandomPlus.Cli/HRandomPlus.Cli.csproj \
  -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -o publish/linux-x64-cli
```

Windows x64 Avalonia:

```powershell
dotnet publish src/HRandomPlus.Desktop/HRandomPlus.Desktop.csproj `
  -c Release -f net8.0-windows -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -o publish/win-x64-avalonia
```

## CLI OSZ

```bash
HRandomPlus.Cli beatmap.osz --seed 123456 --config config.example.json
```

## Pruebas

El runner cubre parser, BPM/snaps, OSZ, perfiles/seed, configuración y defaults, nombres repetidos seguros, salida, rangos, seeds reproducibles, keymodes 4K–9K, long notes, JSON/reconexión/timeout de tosu, estados connected/disconnected, rutas Winello, E2E tosu simulado, copia Wine-side simulada y sus fallbacks.

Antes de reportar resultados de una máquina real usa [PLAYTEST_CHECKLIST.md](PLAYTEST_CHECKLIST.md).

## Licencias

HRandomPlus se distribuye bajo `GPL-3.0-or-later`; el texto completo está en [LICENSE](LICENSE). Consulta [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) y la [auditoría de dependencias](docs/DEPENDENCY_LICENSE_AUDIT.md) antes de redistribuir binarios.

La compilación Windows incorpora `OsuMemoryDataProvider 0.12.2` y `ProcessMemoryDataFinder 0.10.2`, también `GPL-3.0-or-later`. Cada Release debe adjuntar tanto las fuentes exactas de HRandomPlus como el snapshot upstream del commit `122dd102fe272de30471cf1f317805cb49ac23a4`; consulta [el manifiesto de fuentes GPL](docs/GPL_SOURCE_MANIFEST.md). Los demás componentes conservan sus propias licencias y avisos. Linux consume la API HTTP de tosu y no incorpora los componentes GPL de lectura de memoria.

Los artefactos de GitHub Actions son temporales. Los ZIP y `SHA256SUMS.txt` solo se convierten en descargas estables cuando el propietario crea una GitHub Release asociada a un tag.
