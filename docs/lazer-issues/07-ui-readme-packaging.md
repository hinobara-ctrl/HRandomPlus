# Update UI, README and release packaging for osu!lazer

## Context

The current UI, documentation and release notes explicitly target osu!stable.

## Goal

Expose lazer support clearly after functional work and playtests are complete.

## Constraints

- Never label stable detection as lazer or vice versa.
- Keep manual mode available and identify its priority.
- Do not publish unsupported claims or merge platform instructions.
- Re-run dependency/license audit for any new integration packages.

## Acceptance criteria

- UI identifies game variant, source, connection and import result.
- README documents setup and limitations separately for stable and lazer.
- Release packages contain required notices, checksums and platform assets.
- Release notes link to completed Windows/Linux lazer playtest evidence.

## Dependencies

Depends on all functional lazer work and the tests/playtests issue.
