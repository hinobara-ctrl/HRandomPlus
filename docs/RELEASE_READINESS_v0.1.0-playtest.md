# Release readiness: v0.1.0-playtest

Date: 2026-08-29

## Initial and GitHub state

- Branch: `main`.
- Base HEAD: `de9250083da1d2db9a666d66b99c182204cb98ec` (`Finalize pre-lazer stable support`).
- `origin/main` tracking ref: same commit at the start and end of this preparation. A read-only GitHub API check also confirmed that public `main` still points to `de92500`; `git fetch origin` could not refresh locally because this environment had no Windows Git credentials.
- Repository visibility observed on GitHub: **Public**.
- Last known remote workflow for `de92500`: successful test, Windows artifact and Linux artifact jobs.
- Open Issues observed before preparation: 0.
- Tags/Releases observed before preparation: none.
- The workflow changed locally; its definitive remote result requires owner-authorized commit/push and a new Actions run.

## Verification

- Custom regression runner after profile implementation: **100 passed, 0 failed**.
- `dotnet test HRandomPlus.sln`: command succeeds, but this solution uses an executable custom runner rather than a test-SDK project, so that command does not discover/count the custom cases.
- Solution build: **PASS, 0 errors**.
- Windows candidate smoke start: **PASS**; process remained alive after three seconds and was stopped by the test harness.
- Linux publish: **PASS cross-publish**; real Linux behavior remains supported by the completed pre-lazer playtest record.
- Local SDK: 8.0.424; self-contained runtime packs: 8.0.30.
- CI SDK decision: retain stable SDK `10.0.x` to compile `net8.0`/`net8.0-windows` and satisfy current Avalonia analyzer expectations. No `global.json` was added because it would prevent the current SDK-8-only local environment from building.
- Persistent Custom, GUID migration and validated profile import/export are implemented locally and documented in `docs/PROFILE_SYSTEM_DESIGN.md`; real UI playtests passed on Windows and the Linux VM with no bugs reported.

## Candidate assets

- `HRandomPlus-windows-x64.zip`
- `HRandomPlus-linux-x64.zip`
- `HRandomPlus-v0.1.0-playtest-source.zip`
- `HRandomPlus-v0.1.0-playtest-gpl-source.zip`
- `SHA256SUMS.txt`

`outputs/SHA256SUMS.txt` is the authoritative manifest for the exact final files. Binary ZIPs exclude PDB, bin/obj, caches, logs, personal configuration, beatmaps and nested ZIPs. They include the executable/runtime files, README, HRandomPlus license, configuration example, third-party notice and only the license texts applicable to that platform. The source ZIPs are separate Release assets.

GitHub Actions artifacts are temporary CI outputs. GitHub Release assets are stable downloads attached to a tag. Any future Release must attach all four ZIPs and `SHA256SUMS.txt`, even after Actions succeeds.

## Gates

- **LICENSE: PASS** — HRandomPlus is `GPL-3.0-or-later`; the complete root license, exact HRandomPlus source, exact upstream GPL snapshot and applicable third-party notices are included in the release set.
- **VISIBILITY: OWNER DECISION REQUIRED** — current repository is Public. Keep it Public only if a public release is intended.
- **TESTS: PASS** — 100/100 custom tests.
- **BUILD: PASS** — 0 errors.
- **ARTIFACTS: READY LOCALLY** — Windows/Linux binary ZIPs, HRandomPlus source, GPL source and checksums were regenerated and audited as one candidate set; final remote CI verification remains pending because the workflow changed locally.

## Tag/release status

- Intended tag: `v0.1.0-playtest`.
- Intended target: the future owner-authorized release-preparation commit, not `de92500`.
- Release notes: `docs/RELEASE_NOTES_v0.1.0-playtest.md`.
- **READY TO TAG AFTER OWNER REVIEW:** the technical licensing gate is closed; repository visibility and the actual commit/push/tag/Release remain owner actions.
- No commit, push, tag or Release was created during this preparation.
