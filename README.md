# HRandomPlus

Aplicación multiplataforma para crear dificultades H-Random, S-Random o Custom de osu!mania. Windows detecta osu!stable mediante lectura de memoria read-only; Linux obtiene el beatmap seleccionado desde tosu y resuelve su ruta nativa mediante osu-winello/XDG. Ambos usan exactamente el mismo motor.

## Funciones

- Perfiles H-Random, S-Random, Custom y perfiles personalizados.
- Seeds reproducibles y rangos parciales.
- Protección de long notes y validación antes de escribir.
- Detección de osu!stable en Windows aunque osu!lazer esté abierto.
- Detección Linux sin `sudo`, lector de memoria propio ni conversión de letras Wine.
- Selector manual `.osu` en ambas plataformas.
- Salida con nombre único, `BeatmapID:0` y original intacto.

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

Linux escribe de forma predeterminada en:

```text
$XDG_DATA_HOME/HRandomPlus/Generated Beatmaps
~/.local/share/HRandomPlus/Generated Beatmaps
```

**Write beside the original beatmap** permite escribir junto al mapa cuando la carpeta sea escribible. osu!stable puede requerir F5 para recargar la dificultad.

## Diagnóstico tosu

```bash
dotnet HRandomPlus.Cli.dll --diagnose
dotnet HRandomPlus.Cli.dll --diagnose --host 127.0.0.1 --port 24050
dotnet HRandomPlus.Cli.dll --diagnose --osu-path /ruta/nativa/osu
```

El diagnóstico imprime estado, metadatos, IDs, checksum y ruta nativa. Códigos: `0` correcto, `3` tosu no disponible y `4` conectado pero sin beatmap resoluble.

## Configuración

Windows conserva `%LOCALAPPDATA%\HRandomPlus`. Linux sigue XDG:

- Configuración: `$XDG_CONFIG_HOME/HRandomPlus/config.json`.
- Datos y salidas: `$XDG_DATA_HOME/HRandomPlus`.
- Log: `$XDG_STATE_HOME/HRandomPlus/logs/latest.log`.

Una configuración corrupta restaura defaults y no impide iniciar la aplicación.

## Arquitectura

```text
HRandomPlus.Core         motor, parser, archivos, perfiles y validación
HRandomPlus.Integration  tosu, osu-winello, rutas y contratos
HRandomPlus.Desktop      UI Avalonia para Windows/Linux
HRandomPlus.Cli          diagnóstico y procesamiento .osz
HRandomPlus.Windows      interfaz WinForms anterior conservada temporalmente
HRandomPlus.Tests        regresión portable e integración
```

La UI y las fuentes no contienen lógica de random. El motor no conoce el sistema operativo ni el origen del mapa.

## Compilación

Requiere .NET 8 SDK.

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

La interfaz WinForms anterior sigue compilable desde `src/HRandomPlus/HRandomPlus.csproj` durante la comprobación de paridad.

## CLI OSZ

```bash
HRandomPlus.Cli beatmap.osz --seed 123456 --config config.example.json
```

## Pruebas

El runner cubre parser, OSZ, perfiles, configuración corrupta, salida segura, rangos, seeds reproducibles, keymodes 4K–9K, long notes, JSON de tosu, ausencia de tosu, rutas nativas y configuración XDG de osu-winello.

## Licencias

HRandomPlus es MIT. `OsuMemoryDataProvider 0.12.2` se usa únicamente en builds Windows; revisa sus términos GPL-3.0-or-later al redistribuirlos. Linux consume la API HTTP de tosu y no copia código de tosu, cosutrainer ni osumem.
