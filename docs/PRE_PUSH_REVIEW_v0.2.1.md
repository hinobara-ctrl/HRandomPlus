# Pre-push review

Commit base: `5a47076` (`Harden final v0.2.1 integration and consistency`)
Fecha: 2026-09-01
Sistema: Windows x64, .NET SDK 8.0.419

## Baseline

Build: correcta, 0 errores; 4 advertencias conocidas `CS9057` de analizadores Avalonia 12.1.1 con Roslyn 4.11 del SDK 8.
Tests: 316 aprobados, 0 fallidos, 0 omitidos mediante el runner ejecutable del proyecto.

## Correcciones

- La identidad retenida de osu!stable ahora usa PID y hora de inicio, evitando confundir un PID reutilizado con el proceso anterior.
- Una ruta configurada de stable inválida se ignora de forma segura y no interrumpe la selección de procesos.
- El diagnóstico compartible redacta también rutas personales embebidas en mensajes de estado de Winello.
- README y hardening describen con precisión el centro compartido impar y la identidad multi-stable.
- La cobertura dual-stage explicita 9K como control negativo y toda la matriz 10K–18K, activada y desactivada.
- Se cubrieron acordes compatibles/incompatibles, cabezas de LN y límites estrictos 4×/8× en −1, exacto y +1 con varios thresholds.
- El cleanup temporal de `.osz` es best-effort, inyectable para pruebas y no enmascara el resultado principal.
- `.osu` directo y `.osz` aplican la misma validación estructural antes del randomizado.
- Los nombres sugeridos para exportar perfiles evitan dispositivos reservados de Windows sin cambiar el nombre visible.
- La redacción diagnóstica procesa todas las apariciones válidas del home y tolera puntuación adyacente sin aceptar prefijos falsos.

## Consistencia verificada

- Config: defaults 4096, máximo 8192, migración conservadora y validación estricta de input nuevo.
- Stages: 9K no aplica; 10K–18K conserva la semántica lateral y el centro compartido aprobados.
- Trills: scoring y estadísticas usan el mismo helper y la regla de acorde compatible.
- Pausas: sólo un delta superior a 4× corta el trill y sólo uno superior a 8× reinicia Dynamic Threshold.
- Suffix: una política portable común cubre config, UI, perfiles y generación.
- Stable: prioridad configurada, instancia vigente, candidata única y ambigüedad segura; PID reutilizado cubierto.
- Winello: cleanup best-effort confinado al root temporal y sin ocultar el error principal.
- Diagnostics: home redactado en salida compartible; logs internos locales sin telemetría.
- BPM: doce divisores sin cambios, formato compacto y resumen de rango.
- Multi-stable: la política externa es determinista; la limitación residual del reader por nombre, sin binding por PID en esta integración, está documentada.
- Docs: enlaces locales, versión, artefactos, .NET 8 y distinción histórica/vigente revisados.
- CI: restore locked, pruebas Windows/Ubuntu, acciones fijadas por SHA y cuatro fuentes/binarios esperados más checksums.

## Problemas encontrados pero no modificados

- El SDK 8 local usa Roslyn 4.11 y puede emitir `CS9057` con analizadores Avalonia que esperan 4.14. CI usa SDK 10 para compilar los mismos targets net8.0; no es una regresión del código.
- El sandbox sin salida de red puede emitir `NU1900` al consultar vulnerabilidades. Un restore locked con red habilitada fue correcto y no cambió lockfiles.

## Validación final

Build: correcta, 0 errores.
Tests: 343 aprobados, 0 fallidos, 0 omitidos mediante el runner ejecutable; `dotnet test -c Release --no-build` finalizó correctamente.
git diff --check: correcto, sin errores de whitespace.

## Estado

READY TO PUSH
