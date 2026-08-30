# Add osu!lazer compatibility on Windows and Linux

## Context

Stable has intentionally different Windows and Linux adapters. Lazer support also needs explicit platform behavior rather than implicit path conversion.

## Goal

Provide equivalent supported detection, processing and import flows for native osu!lazer on Windows x64 and Linux x64.

## Constraints

- No `sudo` for normal Linux use.
- No Wine assumptions for native lazer.
- Stable workflows must remain unchanged and testable.
- Platform-specific code stays outside the randomizer core.

## Acceptance criteria

- Both platform builds open, detect/select, generate and import a lazer difficulty.
- Settings are platform-appropriate and survive upgrades.
- Error/status text identifies the actual source and platform path.
- Packaging contains only required platform files and notices.

## Dependencies

Depends on detection, storage and safe-import issues.
