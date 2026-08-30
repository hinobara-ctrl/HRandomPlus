# Import generated difficulty safely into osu!lazer

## Context

Copying a new `.osu` beside a stable beatmap is not a valid lazer import strategy.

## Goal

Import a generated difficulty using a supported lazer workflow while preserving the original and avoiding internal database edits.

## Constraints

- No direct Realm/database modification.
- Never overwrite the source difficulty.
- Keep unique version/filename semantics and `BeatmapID:0` policy where applicable.
- Preserve a recoverable output if automatic import fails.

## Acceptance criteria

- Generated difficulties import successfully on Windows and Linux.
- Repeated generation never overwrites or creates ambiguous versions.
- Failures retain an importable artifact and explain the manual fallback.
- End-to-end tests verify resources, timing, object count and long notes.

## Dependencies

Depends on storage resolution and platform compatibility design.
