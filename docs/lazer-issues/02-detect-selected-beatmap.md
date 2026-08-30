# Detect selected beatmap in osu!lazer

## Context

Windows stable uses read-only memory integration and Linux stable uses tosu. Neither path is assumed valid for lazer.

## Goal

Identify and implement a reliable, read-only source of the beatmap/difficulty currently selected in osu!lazer on Windows and Linux.

## Constraints

- No unsafe memory guessing or database writes.
- Distinguish lazer from stable when both processes are open.
- Preserve manual selection and clear connected/disconnected states.

## Acceptance criteria

- Selection changes are detected on both platforms.
- Closing/reopening lazer updates connection state without losing the last-map explanation.
- Stable selection cannot be mislabeled as lazer selection or vice versa.
- Unit/integration tests cover connect, disconnect, reconnect and map change.

## Dependencies

Depends on the initial architecture issue; feeds selected identity into storage resolution.
