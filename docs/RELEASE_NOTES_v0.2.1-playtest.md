# HRandomPlus v0.2.1-playtest

This is a focused correctness, robustness and reproducibility update to v0.2.0. It does not change H-Random, S-Random, Custom, seeds, profiles or deterministic output.

## Fixed

- Refreshes the current lazer map after a new runtime selection event for the same GUID/name and after a successful import, avoiding stale Realm hash/blob reuse.
- Selects the newest valid lazer runtime log instead of preferring `runtime.log` by filename.
- Disposes discarded osu!stable process handles on Windows.
- Protects Wine-side copy paths containing shell metacharacters, spaces, apostrophes, `!`, accents and Unicode.
- Stops lazer import with a clear error when the referenced main audio blob is missing.
- Preserves resources whose ZIP names differ only by letter case.

## Release hardening

- Runs the automated suite on Windows and Ubuntu.
- Pins existing GitHub Actions major versions to exact release commits.
- Commits NuGet lock files and restores CI dependencies in locked mode.
- Ignores narrowly scoped local lazer Realm/runtime data.
- Adds a current/historical documentation index and aligns stable-only manual-selection wording.

## Post-audit reliability

- Preserves split Unicode characters while tailing lazer logs and performs a bounded backward startup search beyond the final 2 MiB.
- Recovers cleanly when lazer closes and reopens on the same storage, and tolerates failure of one automatic beatmap source when another remains valid.
- Cancels helper process trees without changing the existing timeout contract.
- Preserves `config.json` on transient read failures and backs up malformed JSON before restoring defaults.
- Handles duplicate Realm file usages and filesystem-specific temporary filenames with controlled diagnostics.
- Adds bounded streamed `.osz` extraction and consistent OS-aware path/case handling across extraction, hashing and validation.

The complete automated suite now contains 146 passing tests. The small set of real Linux platform confirmations remains owner-run and is documented separately.

Custom `storage.ini` was already verified on real Windows and Linux. Linux manual regression checks for the changed Wine/import paths remain an owner-run release-candidate step.

HRandomPlus remains `GPL-3.0-or-later`. No dependency or license model changed; existing third-party notices and corresponding-source requirements remain in force.
