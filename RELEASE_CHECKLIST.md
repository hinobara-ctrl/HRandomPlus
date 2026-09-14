<!-- document-status: current -->
# Release checklist

This is a reusable checklist for future releases. Its checkboxes do not represent the completed state of v1.0.0; start with a clean copy and record evidence for the release under review. Completed v1.0.0 evidence is stored in [V1_0_0_RELEASE_VALIDATION.md](V1_0_0_RELEASE_VALIDATION.md). Historical playtests are preserved in [the previous checklist](docs/historical/2026-09-02_PLAYTEST_CHECKLIST_v0.2.1.md); they do not certify a future build.

Use this procedure for local validation, manual tests, final CI inspection and publication of a future release. Keep completed release evidence in a separate version-specific record.

## Local code and packaging checks

- [ ] Review `git status`, `git ls-files`, `.gitignore` and the entire diff; exclude credentials, personal configuration and generated outputs.
- [ ] Run `git diff --check` and `pwsh -File scripts/check-repo-consistency.ps1`.
- [ ] Run locked restore, Release build and the executable test runner using the commands in README.
- [ ] Confirm the deterministic baseline tests pass without changing their expected hashes.
- [ ] Publish both desktop targets using the CI commands; inspect native libraries, notices and executable permissions.

## Manual tests on Windows and Linux

Record the commit, OS, game version and observed result separately. Automated tests are not manual evidence.

- [ ] Open Guide beside Active parameters. Visit all four tabs, scroll every parameter, resize, check dark-theme readability, navigate with Tab/arrow keys, and close with Escape/Close. Confirm reopening works and the main window layout is unchanged apart from the button.
- [ ] Select a manual mania `.osu` with audio/resources. Generate H-Random, S-Random and Custom variants; load them in osu! and confirm timing/LN lengths and original contents remain unchanged.
- [ ] Generate repeatedly with a fixed seed, then with an empty seed. Confirm Hold Seed/Delete Seed and reported output paths. Compare note assignments, not unique filenames.
- [ ] Exercise Whole map and Selected range, including long notes crossing both boundaries and a 10K+ map with Preserve dual stages.
- [ ] Save Custom twice, restart, duplicate a preset, export/import a saved profile (including Unicode and seed), update/import as copy, delete only a personal profile and reset Custom. Exchange a profile between Windows and Linux.
- [ ] Windows stable: detect and change maps, generate beside the original, then test manual selection and coexistence with lazer.
- [ ] Linux stable/Winello/tosu: detect/change maps, disconnect/reconnect tosu, verify Wine-side copy and native fallback/F5. Use a disposable set with spaces and Unicode in its path.
- [ ] Native lazer on each OS: detect/change maps, generate, verify imported audio/background and detached IDs, then restart the game and test a configured storage location if used.
- [ ] On disposable data, make stable destination copying fail and verify a readable `.osz` in Failed Imports beside the executable. Import it manually after restoring access.
- [ ] Open two app instances on a disposable map and generate concurrently; verify distinct output files, no overwritten generations and an unchanged original. Also exercise the Wine-side path on Linux separately.

ZIP-construction and launcher-failure injection are covered by automated regressions. Do not mark a manual checkbox solely because those regressions pass. If manually injecting these failures, use disposable data and record the method; do not corrupt the real lazer library.

## Final CI and publication

- [ ] Verify Windows and Ubuntu test jobs and both publish jobs in the same Actions run.
- [ ] Download the release candidate and verify every archive with `SHA256SUMS.txt`.
- [ ] Inspect `release-evidence.txt`: version, commit, SDK and successful upstream jobs must describe that run.
- [ ] Verify the source archive contains the exact committed changes and the GPL archive matches the pinned upstream checksum.
- [ ] Confirm release blockers are resolved and manual test results are recorded in the version-specific validation record.
- [ ] Publish binaries, corresponding sources, checksums and evidence together. A version change alone does not publish a release.
