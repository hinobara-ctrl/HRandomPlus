# Desarrollo y releases

## Requisitos

- SDK .NET 8 o posterior capaz de compilar targets `net8.0` y `net8.0-windows`.
- Windows x64 para validar la integración de memoria con osu!stable.
- Linux x64 para las integraciones nativas de lazer y stable mediante tosu/osu-winello.

La distribución normal es framework-dependent y requiere .NET Runtime 8 x64. Las variantes self-contained no forman parte de la política vigente.

## Build local

```text
dotnet restore HRandomPlus.sln --locked-mode
dotnet build HRandomPlus.sln -c Release --no-restore
dotnet run --project tests/HRandomPlus.Tests/HRandomPlus.Tests.csproj -c Release --no-build
```

El proyecto de pruebas utiliza un runner ejecutable propio; por eso el último comando, también usado por CI, entrega el conteo efectivo de casos. La suite cubre motor, parser, archivos, perfiles, integración simulada, fuentes stable/lazer/tosu, seguridad y migraciones. Los playtests históricos en sistemas reales permanecen documentados, pero no se repiten en cada cambio de hardening cubierto automáticamente.

El benchmark reproducible de candidatos se ejecuta con:

```text
dotnet run --project tools/HRandomPlus.CandidateBenchmark/HRandomPlus.CandidateBenchmark.csproj -c Release
```

No es un test temporal de CI ni impone umbrales de rendimiento.

## Estructura

- `src/HRandomPlus.Core`: parser, configuración, perfiles y motor.
- `src/HRandomPlus.Integration`: tosu, lazer, Winello, procesos e importadores.
- `src/HRandomPlus.Desktop`: UI Avalonia y adaptación específica por plataforma.
- `src/HRandomPlus.Cli`: procesamiento de archivos y diagnóstico local compartible.
- `tests/HRandomPlus.Tests`: runner y regresiones automáticas.
- `tools/HRandomPlus.CandidateBenchmark`: medición controlada del límite de candidatos.

## Release

`.github/workflows/build.yml` restaura en modo locked y prueba en Windows y Ubuntu. Después genera los ZIP framework-dependent Windows x64 y Linux x64, el source archive exacto de HRandomPlus, el snapshot GPL correspondiente y `SHA256SUMS.txt`. Los artefactos de aplicación no sustituyen los assets de fuentes y licencias.

Antes del push:

```text
git status --short
git diff --check
dotnet build HRandomPlus.sln -c Release
dotnet run --project tests/HRandomPlus.Tests/HRandomPlus.Tests.csproj -c Release --no-build
```

Después del push, revisar GitHub Actions y confirmar:

- tests Windows y Ubuntu;
- builds framework-dependent Windows/Linux;
- source archive de HRandomPlus;
- corresponding source GPL;
- `SHA256SUMS.txt` del conjunto final.

No publicar una Release antes de que esos jobs terminen correctamente.
