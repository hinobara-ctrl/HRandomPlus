# Preparación de release: v0.1.0-playtest

> Documento histórico. No describe la política vigente de v0.2.1, que distribuye dos binarios framework-dependent y conserva las fuentes como assets adicionales.

Fecha: 2026-08-29

## Estado inicial y de GitHub

- Rama: `main`.
- HEAD base: `de9250083da1d2db9a666d66b99c182204cb98ec` (`Finalize pre-lazer stable support`).
- Referencia de seguimiento `origin/main`: el mismo commit al inicio y final de esta preparación. Una consulta de solo lectura a la API de GitHub también confirmó que `main` pública seguía apuntando a `de92500`; `git fetch origin` no pudo actualizar localmente porque el entorno no tenía credenciales Git de Windows.
- Visibilidad observada del repositorio en GitHub: **Público**.
- Último workflow remoto conocido para `de92500`: jobs de pruebas, artifact Windows y artifact Linux correctos.
- Issues abiertos observados antes de la preparación: 0.
- Tags/Releases observados antes de la preparación: ninguno.
- El workflow cambió localmente; su resultado remoto definitivo requiere commit/push autorizado por el propietario y una ejecución nueva de Actions.

## Verificación

- Runner personalizado de regresión después de implementar perfiles: **100 aprobadas, 0 fallidas**.
- `dotnet test HRandomPlus.sln`: el comando funciona, pero esta solución usa un runner ejecutable propio en vez de un proyecto test-SDK, por lo que el comando no descubre ni cuenta los casos personalizados.
- Compilación de la solución: **APROBADA, 0 errores**.
- Inicio smoke del candidato Windows: **APROBADO**; el proceso seguía vivo tras tres segundos y el harness de pruebas lo detuvo.
- Publicación Linux: **PUBLICACIÓN CRUZADA APROBADA**; el comportamiento en Linux real está respaldado por el registro completo del playtest pre-lazer.
- SDK local: 8.0.424; runtime packs autocontenidos: 8.0.30.
- Decisión de SDK en CI: conservar el SDK estable `10.0.x` para compilar `net8.0`/`net8.0-windows` y satisfacer las expectativas de los analizadores actuales de Avalonia. No se añadió `global.json` porque impediría compilar en el entorno local que en ese momento solo tenía SDK 8.
- Custom persistente, migración de GUID e importación/exportación validada de perfiles están implementados localmente y documentados en `docs/PROFILE_SYSTEM_DESIGN.md`; los playtests reales de UI fueron aprobados en Windows y la VM Linux sin bugs reportados.

## Assets candidatos

- `HRandomPlus-windows-x64.zip`
- `HRandomPlus-linux-x64.zip`
- `HRandomPlus-v0.1.0-playtest-source.zip`
- `HRandomPlus-v0.1.0-playtest-gpl-source.zip`
- `SHA256SUMS.txt`

`outputs/SHA256SUMS.txt` es el manifiesto autoritativo de los archivos finales exactos. Los ZIP binarios excluyen PDB, bin/obj, cachés, logs, configuración personal, beatmaps y ZIP anidados. Incluyen archivos del ejecutable/runtime, README, licencia de HRandomPlus, configuración de ejemplo, aviso de terceros y solo los textos de licencia aplicables a esa plataforma. Los ZIP de fuentes son assets separados de la Release.

Los artifacts de GitHub Actions son outputs temporales de CI. Los assets de GitHub Release son descargas estables adjuntas a un tag. Para este candidato v0.1.0 se exigían los cuatro ZIP y `SHA256SUMS.txt`, incluso después de que Actions terminara correctamente.

## Condiciones

- **LICENCIA: APROBADA** — HRandomPlus es `GPL-3.0-or-later`; la licencia raíz completa, fuentes exactas de HRandomPlus, snapshot GPL upstream exacto y avisos de terceros aplicables están incluidos en el conjunto de release.
- **VISIBILIDAD: REQUIERE DECISIÓN DEL PROPIETARIO** — el repositorio actual es público. Mantenerlo así solo si se pretende una release pública.
- **PRUEBAS: APROBADAS** — 100/100 pruebas personalizadas.
- **COMPILACIÓN: APROBADA** — 0 errores.
- **ARTIFACTS: LISTOS LOCALMENTE** — los ZIP binarios Windows/Linux, fuentes de HRandomPlus, fuentes GPL y checksums se regeneraron y auditaron como un conjunto candidato; la verificación CI remota final queda pendiente porque el workflow cambió localmente.

## Estado de tag/release

- Tag previsto: `v0.1.0-playtest`.
- Destino previsto: el futuro commit de preparación autorizado por el propietario, no `de92500`.
- Notas de release: `docs/RELEASE_NOTES_v0.1.0-playtest.md`.
- **LISTO PARA TAG TRAS REVISIÓN DEL PROPIETARIO:** la condición técnica de licencias está cerrada; la visibilidad y el commit/push/tag/Release reales siguen siendo acciones del propietario.
- Durante esta preparación no se creó ningún commit, push, tag ni Release.
