# Resolve selected beatmap data from osu!lazer storage

## Context

osu!lazer does not expose beatmaps as the traditional stable `Songs/<folder>/<file>.osu` layout.

## Goal

Obtain a complete parseable beatmap representation and required resources for the selected lazer difficulty through supported/read-only mechanisms.

## Constraints

- Do not treat lazer storage as a stable Songs folder.
- Do not mutate Realm or other internal databases.
- Validate paths, object counts, timing points and long notes before processing.

## Acceptance criteria

- A selected mania beatmap can be resolved consistently on Windows and Linux.
- Non-mania or unresolved selections produce actionable status without crashing.
- The original lazer data remains unchanged.
- Fixtures cover missing data, unusual metadata, Unicode and storage changes.

## Dependencies

Depends on selected-beatmap detection; blocks safe import and end-to-end playtests.
