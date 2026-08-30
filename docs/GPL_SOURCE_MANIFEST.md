# GPL corresponding-source manifest

Release candidate: `v0.1.0-playtest`

This manifest maps all GPL-covered code in the Windows x64 candidate to its corresponding source.

## HRandomPlus source

HRandomPlus `v0.1.0-playtest` is distributed under `GPL-3.0-or-later`. `HRandomPlus-v0.1.0-playtest-source.zip` is generated from the same repository revision used by the binary jobs and contains the solution, all application and test source, project/build files, `NuGet.Config`, configuration example, workflow, documentation and license material. Generated outputs, package caches, personal configuration and beatmaps are excluded.

Rebuild commands are documented in `README.md`; the authoritative automated commands are in `.github/workflows/build.yml`.

## Package mapping

| NuGet package | Version | Package SHA-256 | License | Upstream tag | Exact commit |
|---|---:|---|---|---|---|
| OsuMemoryDataProvider | 0.12.2 | `739f03b7db1510887a6266532a8e0dda2ebb56d3ee0c9f8172dab60cc42745fc` | GPL-3.0-or-later | `osu_v0.12.2` | `122dd102fe272de30471cf1f317805cb49ac23a4` |
| ProcessMemoryDataFinder | 0.10.2 | `ae25ddc53bb6ced73c975d045e79a52050cdadbe7144966f19bd8fc22e8dd9b4` | GPL-3.0-or-later | `process_v0.10.2` | `122dd102fe272de30471cf1f317805cb49ac23a4` |

Both annotated tags resolve to the same commit declared in both NuGet manifests.

## Included source snapshot

- Repository: `https://github.com/Piotrekol/ProcessMemoryDataFinder`
- Immutable source URL: `https://github.com/Piotrekol/ProcessMemoryDataFinder/tree/122dd102fe272de30471cf1f317805cb49ac23a4`
- Official archive URL: `https://codeload.github.com/Piotrekol/ProcessMemoryDataFinder/zip/122dd102fe272de30471cf1f317805cb49ac23a4`
- Downloaded archive SHA-256: `9872dd7c18a1a8a4ec16b8d66b409f377dda9b6974057a9a889fd5c73fad0535`
- Downloaded archive SHA-512: `b69f4cf66b7d4895b9f629d698debc080628530e711be419fe106a983268cd2d9d5f0324668a9f443b934eea255d205fd0af0f7894633c011c964cf10c0a059e`

`HRandomPlus-v0.1.0-playtest-gpl-source.zip` expands that complete repository snapshot and places this manifest beside it. The snapshot includes both package projects, shared source, solution/build files and the upstream GPL license.

## Distribution set

The Windows binary ZIP, HRandomPlus source ZIP, upstream GPL source ZIP and `SHA256SUMS.txt` form one release set. Do not publish the Windows binary without both corresponding-source archives and the checksum manifest from the same candidate run.
