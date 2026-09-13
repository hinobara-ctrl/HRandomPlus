<!-- document-status: current -->
# Profile system

This document describes the implemented contract. The [previous design and implementation record](../historical/2026-09-12_PROFILE_SYSTEM_DESIGN_PRE_V1.md) retains historical proposals and evidence. Parameter meanings are documented in the application's Guide.

## Catalog and actions

- H-Random and S-Random are protected presets reconstructed from code.
- Custom is a single editable profile stored in `AppSettings.CustomConfig`, with a stable `CustomProfileId`. Missing Custom configuration uses `ProfileCatalog.DefaultCustom()`.
- Personal profiles are stored in `AppSettings.CustomProfiles` with a GUID, name, description and configuration.
- Save Profile saves current parameters and seed into Custom or a selected personal profile; it cannot overwrite the protected presets.
- Duplicate captures the current editor values into a new personal profile with a new GUID.
- Export Profile exports the saved profile, not unsaved editor changes. Save first when exporting edits.
- Delete Profile is enabled only for personal profiles. Reset asks for confirmation and restores only Custom.
- Apply settings controls the Linux tosu connection, not profiles or output placement.

All profiles use the same engine. UI edits can be used for generation without changing a saved profile; changing the selected profile reloads its saved configuration.

## Persistence and migration

Windows configuration is `%LOCALAPPDATA%\HRandomPlus\config.json`. Linux uses `$XDG_CONFIG_HOME/HRandomPlus/config.json`, defaulting to `~/.config/HRandomPlus/config.json`. These personal files are not release assets.

When migrating older settings without CustomConfig, the last historical personal profile named Custom supplies the persistent Custom configuration. Earlier conflicting entries are retained under unique names. Missing IDs are assigned and migration is idempotent. Reserved names H-Random, S-Random and Custom are compared without case or surrounding whitespace.

## Portable transfer

The UTF-8 `.hrp-profile.json` format uses `format: HRandomPlus.Profile`, `formatVersion: 1`, `engineVersion: 1`, `profileId`, `name`, `description` and `config`. Engine/format versions describe compatibility and are separate from the application's release version. Config contains every randomizer field, including nullable Seed and ScoringWeights.

Export excludes platform paths, tosu settings, logs, last selected map and global preferences. Import copies validated data into local settings; the transport file is not required afterwards.

Import validates format/version, GUID, name, numeric values and `HRandomConfig.Validate()`, with a 256 KiB file limit. It rejects unsupported versions and malformed data before persisting changes. A preview precedes import. If a personal profile already has the incoming GUID, the user can update it or import a copy with a new identity. Name conflicts receive numeric suffixes; reserved names cannot replace built-ins. Local settings writes are atomic.

## Validation and scope

Automated tests cover field/seed round trips, corrupt and oversized imports, conflict handling, migrations, reserved names and protected presets. Current manual UI and cross-platform transfer checks are in [RELEASE_CHECKLIST.md](../../RELEASE_CHECKLIST.md); historical results remain separate.

Cloud synchronization, signatures and multi-profile packs are outside the current implementation. osu!lazer integration is implemented independently and uses these same profiles.
