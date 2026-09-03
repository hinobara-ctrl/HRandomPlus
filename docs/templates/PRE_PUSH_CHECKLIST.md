<!-- document-status: current -->
# Checklist estable antes de push

Este documento define qué comprobar. No registra el SHA, número de tests, hashes ni resultados de un build concreto; esa evidencia la genera CI como `release-evidence.txt`.

## Repositorio

- [ ] Revisar `git status` y el diff completo.
- [ ] Confirmar que no hay rutas personales, secretos, logs, dumps, outputs o artefactos accidentales.
- [ ] Ejecutar `git diff --check`.
- [ ] Ejecutar `pwsh -File scripts/check-repo-consistency.ps1`.
- [ ] Confirmar que los lockfiles solo cambian cuando cambian dependencias.

## Build y pruebas

- [ ] Ejecutar restore con `--locked-mode`.
- [ ] Compilar la solución en `Release` con `--no-restore`.
- [ ] Ejecutar la suite completa con `--no-build`.
- [ ] Ejecutar el benchmark de candidatos y comparar con una baseline obtenida en el mismo equipo.
- [ ] Confirmar que las referencias deterministas siguen siendo byte-idénticas.

## Plataformas

- [ ] Publicar Windows x64 con los mismos parámetros de CI.
- [ ] Publicar Linux x64 con los mismos parámetros de CI.
- [ ] Verificar contenido, licencias, ausencia de PDB y permisos del ejecutable Linux.
- [ ] Ejecutar los smoke tests manuales afectados en Windows.
- [ ] Ejecutar los smoke tests manuales afectados en Linux cuando el cambio toque rutas de plataforma.

## Candidato de release

- [ ] Confirmar que CI pasa en Windows y Ubuntu.
- [ ] Descargar el release candidate del mismo run.
- [ ] Verificar todos los hashes mediante `SHA256SUMS.txt`.
- [ ] Revisar `release-evidence.txt`; no sustituye los smoke tests manuales.
- [ ] Publicar conjuntamente binarios, source, GPL source, checksums y evidencia.
