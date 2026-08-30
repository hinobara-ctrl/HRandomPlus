# Add automated tests and real playtest coverage for osu!lazer

## Context

Stable was closed only after automated regression coverage and real Windows/Linux playtests. Lazer requires the same evidence standard.

## Goal

Create automated and manual verification for lazer without weakening the stable suite.

## Constraints

- Keep deterministic fixtures free of personal beatmaps or paths.
- Separate mocked integration evidence from real playtest evidence.
- Do not claim support from unit tests alone.

## Acceptance criteria

- Tests cover selected-source state, storage resolution, import success/failure and repeated names.
- Existing randomizer, stable Windows and stable Linux tests remain green.
- A documented checklist passes on real Windows x64 and Linux x64 installations.
- Results record versions and known limitations without personal data.

## Dependencies

Begins with architecture/fixtures and completes after all functional lazer integrations exist.
