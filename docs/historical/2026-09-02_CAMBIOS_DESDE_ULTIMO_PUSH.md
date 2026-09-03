<!-- document-status: historical -->
> Tipo de documento: registro puntual del working tree del 2 de septiembre de 2026. Describe los cambios pendientes respecto del último push conocido y deja de ser autoritativo después del siguiente commit.

# Cambios realizados desde el último push

## Punto de comparación

- Commit publicado usado como base: `d5882cddb18bfd630406b58e1178a668059201ab` (`d5882cd`).
- Título del commit: `Fix Windows stable and lazer coexistence`.
- Fecha del commit: 1 de septiembre de 2026.
- Estado descrito: modificaciones locales posteriores, todavía sin commit ni push al crear este documento.
- Versión canónica conservada: `0.2.1-playtest`.

## Resumen ejecutivo

Después del último push se realizó una fase de mantenimiento conservadora que abarca rendimiento determinista del randomizador, robustez de archivos, gobernanza de CI/release, interfaz, seeds, perfiles y exportación de mapas. La detección de beatmaps y las dependencias no fueron sustituidas.

Los cambios de mayor impacto visible son:

1. Una seed vacía vuelve a significar modo aleatorio y obtiene una seed nueva en cada generación; la interfaz muestra la última utilizada sin convertirla en fija.
2. Se retiró la preferencia `Write beside the original beatmap`. Windows copia junto al mapa; Linux intenta Wine-side y luego copia nativa.
3. Si todos los métodos de importación/copia fallan, se crea un `.osz` portable en `Failed Imports`, junto a HRandomPlus.
4. Los controles de plataforma/tosu sólo aparecen en Linux.
5. Los botones de perfiles se reorganizaron y `Custom` recibió un nuevo preset inicial/reset.
6. El motor redujo de forma importante tiempo y allocations sin cambiar las salidas deterministas de las seeds existentes.
7. CI ahora comprueba coherencia del repositorio y genera evidencia del candidato de release.

## Cambios funcionales y de interfaz

### Seeds

- El campo vacío representa una seed automática.
- Cada generación automática obtiene una seed nueva.
- Después de generar, el campo permanece vacío y su placeholder muestra `Random — last used: <seed>`.
- El estado también muestra la seed realmente utilizada.
- Escribir una seed o pulsar `Generate fixed seed` mantiene el comportamiento fijo y reproducible.
- Se eliminó el texto explicativo adicional bajo el campo para mantener la interfaz limpia.

### Salida e importación de beatmaps

- Se eliminó de la UI y de `AppSettings` la opción `OutputToBeatmapFolder`.
- Los `config.json` antiguos pueden contener ese campo sin impedir la carga; deja de controlar el comportamiento actual.
- Toda generación Desktop crea primero una salida segura en el directorio de datos de HRandomPlus.
- En Windows stable, `NativeSideFileImporter` copia la dificultad junto al `.osu` original, usa un nombre único, verifica tamaño y SHA-256 y nunca sobrescribe el original.
- Si la verificación de la copia falla, intenta retirar la copia parcial y conserva la salida generada.
- En Linux stable, la copia Wine-side se intenta siempre. Si falla, se conserva el fallback de copia nativa y el aviso de posible F5.
- En lazer se mantiene la importación mediante `.osz` sin escribir directamente en su Realm/storage.
- `PortableFallbackArchiveImporter` envuelve todos los importadores. Si la generación terminó pero ningún método de entrega funciona, construye un `.osz` portable con la dificultad generada y los recursos disponibles.
- El fallback se guarda en `<carpeta de HRandomPlus>/Failed Imports` y usa nombres incrementales para evitar sobrescrituras.
- Para stable incluye los recursos del beatmapset, excluye otras dificultades `.osu` y evita incluir su propio directorio de fallback.
- Para lazer incluye los recursos resueltos, excluye los `.osu` previos y neutraliza `BeatmapID`/`BeatmapSetID` de la nueva dificultad.
- Los nombres de entrada del ZIP se validan contra traversal.
- Si también falla la creación del `.osz`, la UI conserva y comunica el error original junto con el fallo del fallback.
- El diagnóstico CLI ahora calcula la salida stable junto al mapa y ya no consulta la preferencia retirada.

### Controles por plataforma

- Windows ya no construye ni muestra la sección de plataforma/output.
- Linux muestra una sección `LINUX STABLE / TOSU` con `tosu host`, `tosu port` y `Apply settings`.
- El checkbox `Write beside the original beatmap` fue eliminado en todas las plataformas.
- Los controles mantienen el bloqueo ya existente cuando lazer es la fuente activa.

### Perfiles

- Primera fila de acciones: `Save Profile`, `Import Profile`, `Export Profile`, `Delete Profile`.
- Segunda fila: `Duplicate`, `Reset`.
- Los nombres permanecen iguales al cambiar de perfil; `Save Profile` mantiene las reglas previas de habilitación y guardado.
- `Reset` sólo se habilita para el Custom persistente y conserva la confirmación previa.
- H-Random y S-Random continúan protegidos e inmutables.
- Importar, exportar, duplicar y eliminar conservan sus comportamientos anteriores.

### Nuevo preset inicial de Custom

El bloque `config` entregado en `1.hrp-profile.json` se convirtió en el valor base de `Custom`. No se copiaron el nombre `1` ni el GUID del archivo personal.

| Parámetro | Nuevo valor base |
| --- | ---: |
| Seed | aleatoria (`null`) |
| DynamicThreshold | `true` |
| PreserveDualStages | `false` |
| MinThresholdMs | `35` |
| BaseThresholdMs | `80` |
| MaxThresholdMs | `80` |
| RecentUsageWindow | `24` |
| PatternHistoryLength | `16` |
| WeightedTopCandidates | `24` |
| WeightedTemperature | `12` |
| MaxCandidateSets | `4096` |
| RenameDifficulty | `true` |
| DifficultySuffix | ` CUSTOM` |
| TimeSinceLastUseBonus | `18` |
| HandBalanceBonus | `6` |
| DistributionBonus | `6` |
| JackPenalty | `10` |
| TrillPenalty | `15` |
| RepeatedPatternPenalty | `12` |
| SameHandPenalty | `8` |
| ExtremeJumpPenalty | `6` |
| RecentUsagePenalty | `14` |

Los usuarios que ya tienen un `CustomConfig` guardado conservan sus valores. El nuevo preset se aplica a instalaciones nuevas o cuando el usuario pulsa `Reset`.

## Optimización del randomizador

Esta fase no cambió fórmulas, defaults de H-Random/S-Random, heurísticas ni consumo del RNG.

- La deduplicación de candidatos dejó de crear `string.Join` por combinación y usa una identidad binaria `ulong` con preservación de la primera aparición.
- El scorer precalcula una vez por timestamp el máximo de uso reciente, el último patrón y el timeout de trill.
- Los candidatos internos ya ordenados evitan un `OrderBy().ToArray()` redundante por score.
- La selección ponderada usa un Top-K estable basado en heap cuando `K < N`, conservando score descendente y desempate por orden original.
- El sampling reutiliza el buffer de shuffle y sólo asigna la combinación candidata necesaria.
- Se añadió `StableTopK.cs` como implementación aislada.
- Se añadió una suite de baselines deterministas con hashes de `.osu`, scores representativos, orden de deduplicación y equivalencia Top-K.
- El benchmark ahora produce CSV y separa escenarios soportados, diagnósticos fuera de contrato y stress; mide tiempo, allocations y colecciones GC.

La medición local documentada mostró reducciones aproximadas de 38–49 % en tiempo y 56–61 % en allocations según el escenario. Las cinco salidas `.osu` de referencia y los scores fijados permanecieron byte-idénticos.

## Hardening y archivos

- El reemplazo de un `.osz` existente ya no lo borra antes de mover el candidato validado; usa `File.Move(..., overwrite)` para reducir la ventana sin archivo final.
- Se añadió una regresión que abre y valida el ZIP resultante después de reemplazarlo.
- Se añadió cobertura del selector stable para la transición único objetivo x86 → ambigüedad → único objetivo recuperado.
- El nombre de una prueba del selector se corrigió para no afirmar que su fixture sintético clasifica directamente stable frente a lazer.
- El fallback `.osz` evita copias parciales verificadas como inválidas, traversal, duplicados de entrada y autoinclusión accidental.

## CI, release y gobernanza

- Se añadió `scripts/check-repo-consistency.ps1`.
- El checker valida versión canónica, targets, RIDs, política framework-dependent, nombres de artefactos, condición Windows de memoria, referencias del README y clasificación documental.
- GitHub Actions ejecuta el checker en Windows y Ubuntu antes de build/tests.
- El job de candidato genera `release-evidence.txt` con versión, commit, SDK, configuración, resultado del job de pruebas y SHA-256 de los cuatro assets del mismo run.
- El candidato de release publica también esa evidencia.
- `.gitignore` cubre ahora residuos de Visual Studio, VS Code, JetBrains, TestResults, cobertura, archivos de usuario y metadatos del sistema operativo.
- Se añadió un checklist pre-push estable que evita fijar hashes, SHAs o conteos efímeros.
- Se añadió una plantilla de release notes reutilizable.
- `Directory.Build.props`, dependencias, lockfiles y licencias no cambiaron.
- La distribución sigue siendo framework-dependent y requiere .NET Runtime 8 x64.

## Documentación

- Se normalizaron los documentos al español cuando correspondía.
- Cada Markdown bajo `docs/` quedó clasificado como `current` o `historical`.
- Los documentos de releases y auditorías ya cerradas se marcaron como evidencia histórica para no presentarlos como estado vigente.
- Se actualizó el índice `docs/README.md` para separar documentos actuales de históricos.
- README, guía de desarrollo, notas de release, checklist funcional, diseño de perfiles y pruebas Linux se alinearon con la política framework-dependent, la seed automática, el nuevo fallback y los controles por plataforma.
- Se añadió el informe posteriormente archivado como `docs/historical/2026-09-01_MAINTENANCE_PERFORMANCE_AND_GOVERNANCE.md`, con mediciones before/after y decisiones de optimización.
- Se añadieron `docs/templates/PRE_PUSH_CHECKLIST.md` y `docs/templates/RELEASE_NOTES_TEMPLATE.md`.

## Pruebas y validaciones realizadas

- Restore/build Release local: correcto.
- Runner completo más reciente: 357 pruebas aprobadas y 0 fallidas.
- Baselines deterministas: correctas y byte-idénticas.
- Consistency checker: PASS.
- `git diff --check`: PASS; sólo aparecen avisos informativos LF/CRLF de Git para el working tree Windows.
- Publish framework-dependent Windows x64: correcto.
- Publish framework-dependent Linux x64: correcto.
- Auditoría de ambos ZIP: CRC correcto, archivos obligatorios presentes, PDB ausentes y licencias separadas por plataforma.
- Ejecutable Linux dentro del ZIP: modo `0755`.
- Advertencias locales conocidas: `NU1900` por red restringida al consultar vulnerabilidades y `CS9057` porque Avalonia 12.1.1 trae analizadores Roslyn más nuevos que el compilador del SDK 8 local. No hubo errores de compilación.

## Pruebas manuales pendientes por estos cambios

Las pruebas manuales anteriores de Windows/Linux permanecen válidas para las áreas no modificadas. Antes del push conviene confirmar únicamente:

1. Windows: la sección Linux/output no aparece.
2. Linux: sólo aparecen host/port de tosu y `Apply settings`; no aparece el checkbox retirado.
3. Perfiles: comprobar visualmente las dos filas de botones y que `Reset` restaura los valores de la tabla.
4. Seed: generar dos veces con el campo vacío y comprobar valores diferentes; después fijar una seed y confirmar reproducibilidad.
5. Fallback: provocar, cuando exista un entorno controlado, el fallo de copia/importación y verificar el `.osz` en `Failed Imports`.

El caso 5 continúa marcado como pendiente en los checklists porque los fallos se cubrieron con mocks, pero no se forzó todavía un fallo real de filesystem/plataforma.

## Artefactos locales actuales

Los artefactos están ignorados por Git y no forman parte del commit. Fueron regenerados desde el working tree descrito:

| Archivo | Tamaño | SHA-256 |
| --- | ---: | --- |
| `HRandomPlus-v0.2.1-playtest-windows-x64-framework-dependent.zip` | 13.714.177 bytes | `f33b5b60d7b4549f266ff56129f91b17af5c0cea2285a41afbf5a6b162f8d26b` |
| `HRandomPlus-v0.2.1-playtest-linux-x64-framework-dependent.zip` | 14.018.928 bytes | `295ec47bedea0350a6ce59174f2dc5a44c07965b4e539e375c2505c0954fb32f` |

`artifacts/SHA256SUMS.txt` contiene esos hashes. Los source ZIP, GPL source ZIP y `release-evidence.txt` definitivos deben provenir del mismo run de CI que se use como candidato de release.

## Archivos de producción modificados o añadidos

- `src/HRandomPlus.Cli/Program.cs`
- `src/HRandomPlus.Desktop/MainWindow.cs`
- `src/HRandomPlus.Integration/Importing/BeatmapImporters.cs`
- `src/HRandomPlus/Archives/OsuArchive.cs`
- `src/HRandomPlus/Core/AppPaths.cs`
- `src/HRandomPlus/Core/ProfileCatalog.cs`
- `src/HRandomPlus/Randomization/CandidateScorer.cs`
- `src/HRandomPlus/Randomization/HRandomPlusEngine.cs`
- `src/HRandomPlus/Randomization/StableTopK.cs` — nuevo

## Pruebas y herramientas modificadas o añadidas

- `tests/HRandomPlus.Tests/ApplicationTests.cs`
- `tests/HRandomPlus.Tests/ArchiveIntegrationTests.cs`
- `tests/HRandomPlus.Tests/HardeningTests.cs`
- `tests/HRandomPlus.Tests/ImportIntegrationTests.cs`
- `tests/HRandomPlus.Tests/DeterminismBaselineTests.cs` — nuevo
- `tools/HRandomPlus.CandidateBenchmark/Program.cs`
- `scripts/check-repo-consistency.ps1` — nuevo

## Configuración y documentación modificadas o añadidas

- `.github/workflows/build.yml`
- `.gitignore`
- `PLAYTEST_CHECKLIST.md`
- `README.md`
- `docs/historical/2026-09-01_CHECKLIST_FUNCIONAL_FINAL_v0.2.1.md`
- `docs/current/DEPENDENCY_LICENSE_AUDIT.md`
- `docs/current/DEVELOPMENT_AND_RELEASE.md`
- `docs/historical/2026-08-31_ESTUDIO_TAMANO_Y_OPTIMIZACION_v0.2.1.md`
- `docs/current/GPL_SOURCE_MANIFEST.md`
- `docs/historical/2026-09-01_HARDENING_POST_v0.2.1.md`
- `docs/historical/2026-08-31_HARDENING_v0.2.1.md`
- `docs/current/LAZER_IMPLEMENTATION.md`
- `docs/current/LINUX_IMPORT_AB_TEST.md`
- `docs/historical/2026-08-30_LINUX_POST_AUDIT_TESTS_v0.2.1.md`
- `docs/historical/2026-08-30_PRE_LAZER_STATUS.md`
- `docs/historical/2026-09-01_PRE_PUSH_REVIEW_v0.2.1.md`
- `docs/current/PROFILE_SYSTEM_DESIGN.md`
- `docs/README.md`
- `docs/releases/RELEASE_NOTES_v0.1.0-playtest.md`
- `docs/releases/RELEASE_NOTES_v0.1.1-playtest.md`
- `docs/releases/RELEASE_NOTES_v0.2.0-playtest.md`
- `docs/releases/RELEASE_NOTES_v0.2.1-playtest.md`
- `docs/historical/2026-08-31_RELEASE_READINESS_v0.1.0-playtest.md`
- `docs/historical/2026-08-31_OPTIMIZATION_v0.1.1.md`
- `docs/historical/2026-09-01_MAINTENANCE_PERFORMANCE_AND_GOVERNANCE.md` — nuevo
- `docs/templates/PRE_PUSH_CHECKLIST.md` — nuevo
- `docs/templates/RELEASE_NOTES_TEMPLATE.md` — nuevo
- `docs/historical/2026-09-02_CAMBIOS_DESDE_ULTIMO_PUSH.md` — este registro

## Estado al cerrar el registro

- Código y pruebas automatizadas: correcto.
- Documentación y consistencia: correctas.
- Artefactos locales: regenerados y auditados.
- Commit/push: no realizados.
- Validación futura obligatoria: pipeline Windows/Ubuntu del commit que se publique.
- Validaciones manuales nuevas: enumeradas en la sección correspondiente.
