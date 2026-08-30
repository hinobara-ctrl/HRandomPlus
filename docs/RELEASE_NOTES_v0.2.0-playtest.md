# HRandomPlus v0.2.0-playtest (development candidate)

This version adds first-class native osu!lazer support on Windows x64 and Linux x64 while preserving every existing osu!stable path and the v0.1.1 randomization engine.

## Added

- Native lazer process and storage discovery, including standard, `storage.ini` custom and compatible portable locations.
- Incremental, rotation-aware runtime-log monitoring with historical GUID, current text log and `<timestamp>.runtime.log` support.
- Dynamic, read-only `client.realm` lookup that follows the on-disk schema, plus SHA-256 validation of the selected `.osu` blob.
- Safe `.osz` import of a detached local difficulty with original set resources; no Realm/blob modification.
- Deterministic stable/lazer arbitration and explicit source labels.
- Automated coverage for detection primitives, ambiguity rejection, storage/blob resolution, arbitration and archive construction.

## Unchanged

- H-Random, S-Random and Custom algorithms, parameters, seeds and long-note protection.
- Profile storage/import/export.
- Windows stable memory detection.
- Linux stable tosu/osu-winello detection and Wine-side import.
- Output collision handling. Manual `.osu` selection remains available for stable and is disabled while lazer is the active source.

## Verification state

The implementation and automated suite are complete locally. Real Windows and native Linux functional playtests passed with no reported failures; the artificial launcher-failure scenario is not a release gate because it does not represent lazer's normal import flow.

Technical design, exact upstream revision and failure behavior are documented in `docs/LAZER_IMPLEMENTATION.md`.
