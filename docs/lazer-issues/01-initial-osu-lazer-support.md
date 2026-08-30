# Initial osu!lazer support

## Context

HRandomPlus v0.1.0-playtest supports only osu!stable. Stable-specific detection, paths and import behavior must not be reused blindly for lazer.

## Goal

Define the top-level architecture and phased delivery plan for first-class osu!lazer support without regressing stable.

## Constraints

- Preserve the existing randomizer engine and stable integrations.
- Keep stable and lazer detection/storage/import adapters separate.
- Do not modify lazer databases directly.
- Support an explicit fallback when automatic integration is unavailable.

## Acceptance criteria

- A documented architecture identifies source, storage and importer boundaries.
- Stable and lazer can be selected or detected unambiguously when both run.
- Child issues have agreed dependencies and test strategy.
- No feature is advertised before real Windows and Linux playtests pass.

## Dependencies

Umbrella issue; depends on the six focused lazer issues in this directory.
