# HRandomPlus v0.1.0-playtest

First playtest distribution for osu!stable.

## Supported platforms

- osu!stable on Windows x64.
- osu!stable on Linux x64 through osu-winello, Wine and tosu.
- osu!lazer is **not supported**.

## Included features

- Protected H-Random and S-Random presets plus one persistent, resettable Custom profile.
- Personal profiles with stable GUIDs, descriptions and reproducible seeds.
- UTF-8 `.hrp-profile.json` import/export with validation, preview, conflict handling and Windows/Linux compatibility.
- Whole-map and selected-range processing.
- Long-note protection and output validation.
- Automatic selection plus manual `.osu` fallback.
- Unique difficulty version and filename on repeated generation.
- Linux Wine-side import with safe native fallback.
- Editable BPM-to-millisecond snap reference from 1/1 through 1/64.

Legacy profile settings migrate automatically. The last historical personal profile named Custom becomes the persistent Custom slot; remaining profiles are preserved with unique names and assigned GUIDs.

## Corrected source status

The status formatter previously assumed every automatic selection came from tosu. Detection itself was always correct; only the displayed origin was wrong.

- Windows now shows `Beatmap detected automatically from osu!stable`.
- Linux continues to show `Beatmap detected automatically by tosu`.
- Manual selection remains explicitly differentiated.
- A Windows-specific regression test covers this behavior.

## Linux requirements

- osu-winello.
- tosu running in the same Wine environment as osu!stable.
- Launch through `osu-wine --tosu`.

## Artifacts

- `HRandomPlus-windows-x64.zip`
- `HRandomPlus-linux-x64.zip`
- `HRandomPlus-v0.1.0-playtest-source.zip`
- `HRandomPlus-v0.1.0-playtest-gpl-source.zip`
- `SHA256SUMS.txt`

Verify downloads against `SHA256SUMS.txt`. GitHub Actions artifacts are temporary; assets attached to the tagged GitHub Release are the stable downloads.

## Licenses and source

- HRandomPlus is distributed under `GPL-3.0-or-later`; the complete license is included as the root `LICENSE` and inside each binary package.
- The Windows build incorporates `OsuMemoryDataProvider 0.12.2` and `ProcessMemoryDataFinder 0.10.2`, both declared `GPL-3.0-or-later` and embedded in the single executable.
- Third-party components retain their own licenses. Each platform ZIP contains `THIRD_PARTY_NOTICES.md` and only the license/notice files applicable to that build.
- `HRandomPlus-v0.1.0-playtest-source.zip` contains the HRandomPlus source corresponding to the candidate binaries.
- `HRandomPlus-v0.1.0-playtest-gpl-source.zip` contains the complete ProcessMemoryDataFinder upstream snapshot at commit `122dd102fe272de30471cf1f317805cb49ac23a4`. Tags `osu_v0.12.2` and `process_v0.10.2` both resolve to that commit.

See `THIRD_PARTY_NOTICES.md`, `docs/DEPENDENCY_LICENSE_AUDIT.md` and `docs/GPL_SOURCE_MANIFEST.md` for the technical audit and exact source mapping. The Windows binary, both corresponding-source archives and `SHA256SUMS.txt` must be published together.

**WINDOWS RELEASE READY:** HRandomPlus and its linked GPL memory components are distributed under `GPL-3.0-or-later`; exact corresponding source and all applicable third-party notices accompany the candidate.
