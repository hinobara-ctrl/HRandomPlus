# HRandomPlus

HRandomPlus is a desktop application for generating H-Random, S-Random and Custom variants of osu!mania beatmaps. It reassigns notes between columns using the selected configuration and creates a separate difficulty without modifying the original beatmap.

Code candidate: **v1.0.0**. This version number does not mean a tag or GitHub Release has been published.

## What it changes and preserves

- Changes column assignments for the whole map or a selected time range.
- Preserves note start times, long-note end times, note count, key count, timing points and resource references. Long notes crossing a selected range boundary stay unchanged.
- Checks playable structure and long-note occupancy before writing output.
- Uses a separate filename and clears `BeatmapID`. Renaming the displayed difficulty is optional. Archives sent to lazer also clear `BeatmapSetID`.
- Supports a fixed seed for reproducible assignments with the same input, range and parameters. An empty seed chooses a new value on each run.

## Modes

**H-Random** uses dynamic reuse thresholds and scoring weights. **S-Random** uses zero thresholds and zero scoring weights, selecting among generated candidates while retaining column availability and long-note constraints. **Custom** is an editable, persistent configuration with its own defaults. Personal profiles store additional configurations.

All modes use the same engine. They are configurations, not universal difficulty levels. Optional **Preserve dual stages (10K+)** restricts direct moves between opposite lateral stages; the central column in odd key counts is shared.

## Supported environments

| Environment | Detection | Output/import |
|---|---|---|
| Windows x64 + osu!stable | Process-memory reader, or manual .osu selection | Copies beside the original difficulty |
| Linux x64 + stable through Winello/Wine | tosu, or manual .osu selection | Wine-side copy, then native fallback if needed |
| Windows/Linux x64 + native osu!lazer | Runtime log and read-only Realm catalog | Sends a local .osz variant with set resources |

macOS and other architectures are not packaged by the current workflow. Changes to osu! or tosu can affect detection compatibility.

## Requirements and installation

The distributed packages require **.NET Runtime 8 x64**. They are framework-dependent; the runtime is installed separately. Building from source uses the **.NET SDK 10** in CI and targets .NET 8.

1. Open the repository's [Releases page](https://github.com/hinobara-ctrl/HRandomPlus/releases) and choose an actually published version. The code candidate described here may not yet have a Release.
2. Download the package matching your system and extract the entire ZIP into a writable directory, keeping its native libraries and notices together.
3. Run `HRandomPlus.exe` on Windows or `HRandomPlus` on Linux. On Linux, use `chmod +x HRandomPlus` if extraction did not retain the executable permission.

The expected candidate binary names are:

- `HRandomPlus-v1.0.0-windows-x64-framework-dependent.zip`
- `HRandomPlus-v1.0.0-linux-x64-framework-dependent.zip`

Actions artifacts are temporary build outputs. A published Release must also include the corresponding source archives, checksums and build evidence.

## Basic usage

1. Open osu! and select a mania difficulty, or use **Select .osu manually** in the stable workflow. Check the map and source shown by HRandomPlus.
2. Choose H-Random, S-Random, Custom or a personal profile.
3. Choose **Whole map** or **Selected range**, for example `00:37:005 - 01:13:005`.
4. Leave Seed empty for a new value on every run, or enter/use **Generate Seed** for a fixed value. **Hold Seed** reuses the last generated seed; **Delete Seed** clears it.
5. Review **Active parameters**. The **Guide** button beside that heading opens the built-in manual.
6. Press **Randomize Current Map**. Read Status for the generated difficulty, seed, output path and import result.
7. Open the generated difficulty in osu! and confirm it loads. The original is preserved.

## Profiles and parameters

**Guide → Parameters** is the main parameter reference, with individual explanations and examples. Its other sections cover Getting started, Profiles and Integration. The BPM/snap table is a display reference only: editing Reference BPM does not affect the algorithm or map timing.

Scoring weights are not percentages and do not share a universal importance scale. Each term activates under its own conditions. Thresholds influence candidate availability as well as scoring; weights cannot override occupied long-note columns.

H-Random and S-Random cannot be overwritten or deleted. **Save Profile** saves Custom or a personal profile. **Duplicate** captures the current values into a new personal profile. **Reset** restores Custom after confirmation. **Delete Profile** only removes personal profiles.

**Export Profile** exports the saved profile; save edits first. **Import Profile** validates a `.hrp-profile.json` and previews it before import. An existing profile identity can be updated or imported as a copy. Exports include parameters and seed, but exclude game paths, connection settings and logs. See the [profile contract](docs/current/PROFILE_SYSTEM_DESIGN.md).

## osu!stable and osu!lazer

On Windows, run stable and HRandomPlus with the same permission level. **Configure osu!stable** locates the installation containing `Songs`; it does not select a process. Multiple eligible x86 game processes can make detection ambiguous: close extra instances or select a `.osu` manually.

On Linux, run HRandomPlus natively and stable with tosu in the same Wine environment; Winello provides `osu-wine --tosu`. The default connection is `127.0.0.1:24050`. **Apply settings** saves the Linux tosu connection. **Configure native osu! path** selects the native directory containing `Songs` if discovery fails. A native-copy fallback may require F5 in stable. See the [Linux import procedure](docs/current/LINUX_IMPORT_AB_TEST.md).

Native lazer requires neither tosu nor Wine. Enter Song Select; standard storage locations, `storage.ini` overrides and compatible portable storage are supported. Ambiguous display names are left unresolved instead of selecting an arbitrary difficulty. A successful archive launch means it was sent to lazer, not that the game confirmed import. Check Song Select. See [lazer integration](docs/current/LAZER_IMPLEMENTATION.md).

When `storage.ini` defines `FullPath`, that configured storage is authoritative even if the default directory still contains an older valid Realm. With multiple remaining storages, HRandomPlus first associates the selected executable with its portable root or `storage.ini`; unresolved unrelated storages produce a waiting state instead of an arbitrary pairing.

When both clients are open, the most recently changed detected selection wins. Manual selection retains priority until the game changes map. Stable-specific manual/configuration controls are disabled while lazer is active. If detection fails, read Status, change difficulty, check the relevant connection/installation, and use manual selection in the stable workflow when available.

## Output and recovery

Generation atomically creates a uniquely named `.osu` in the app's `Generated Beatmaps` data directory. Native stable copying and portable fallback creation also reserve new names atomically. On Linux, a separate sidecar reserves the name while Wine creates the final `.osu`, preserving the file-creation notification expected by osu!stable. Existing generations are retained. Repeated sequential generation disambiguates difficulty names; simultaneous generations can have the same displayed difficulty name while their file paths remain distinct.

Successful stable import removes the staging `.osu`; lazer retains it. If copying/importing fails, the app attempts to preserve a portable `.osz` in **Failed Imports beside the HRandomPlus executable**. An incomplete lazer ZIP is removed and a fresh portable fallback is allowed. A completed ZIP is retained if only launching fails.

If resources are unreadable or Failed Imports is unwritable, fallback can also fail. Read Status for the remaining `.osu` or archive path. A completed lazer archive may remain in system temporary storage if moving it fails: copy it promptly, since old import archives are cleaned later. Never assume that an import succeeded solely because a file was generated.

Portable recovery archives reject pathological inputs before or during creation: at most 10,000 entries, 2 GiB per resource, 8 GiB expanded in total and 64 MiB for the generated beatmap. Existing `.osu`, `.osz`, `.zip` and Failed Imports content from a stable set are not recursively repackaged.

Windows data/configuration is under `%LOCALAPPDATA%\HRandomPlus`. Linux follows XDG: configuration under `$XDG_CONFIG_HOME/HRandomPlus`, data under `$XDG_DATA_HOME/HRandomPlus`, and logs under `$XDG_STATE_HOME/HRandomPlus/logs`. Defaults are `~/.config`, `~/.local/share` and `~/.local/state` respectively. Configuration corruption is backed up before defaults are restored; transient read failures do not overwrite the file.

## Building and tests

```text
dotnet restore HRandomPlus.sln --locked-mode
dotnet build HRandomPlus.sln -c Release --no-restore
dotnet test HRandomPlus.sln -c Release --no-build
dotnet run --project tests/HRandomPlus.Tests/HRandomPlus.Tests.csproj -c Release --no-build
pwsh -File scripts/check-repo-consistency.ps1
```

The tests use a custom executable runner. `dotnet test` does not execute these cases; the `dotnet run` command is required and is what CI uses on Windows and Ubuntu. SDK 8 can build locally but may warn about Avalonia analyzers requiring newer Roslyn; use SDK 10 for CI parity.

The workflow contains the authoritative publish commands for `net8.0-windows/win-x64` and `net8.0/linux-x64`. See [development and release](docs/current/DEVELOPMENT_AND_RELEASE.md), the [pending v1.0.0 checklist](V1_PENDING_CHECKLIST.md) and the [release checklist](RELEASE_CHECKLIST.md).

The optional source-built CLI supports `.osz` processing and read-only tosu diagnostics:

```text
dotnet run --project src/HRandomPlus.Cli -- beatmap.osz --seed 123456 --config config.example.json
dotnet run --project src/HRandomPlus.Cli -- --diagnose --host 127.0.0.1 --port 24050
```

CLI processing clears `BeatmapID` on every generated mania difficulty while preserving its set ID. Input, output and JSON report must resolve to three different paths; `--overwrite` applies only to a distinct output archive.

## Repository and licensing

Core owns the parser, configuration, randomizer and validation. Integration owns game detection/import. Desktop provides the Avalonia UI. CLI and the test runner consume the shared libraries. The [documentation index](docs/README.md) separates current contracts from historical evidence.

HRandomPlus is distributed under `GPL-3.0-or-later`; see [LICENSE](LICENSE), [third-party notices](THIRD_PARTY_NOTICES.md), and the [dependency inventory](docs/current/DEPENDENCY_LICENSE_AUDIT.md). Third-party components retain their respective licenses. Windows includes the GPL memory-reader packages; their exact upstream source snapshot must accompany the corresponding release. See the [GPL source manifest](docs/current/GPL_SOURCE_MANIFEST.md).

Each release set includes both binary ZIPs, `HRandomPlus-v1.0.0-source.zip`, `HRandomPlus-v1.0.0-gpl-source.zip`, `SHA256SUMS.txt` and `release-evidence.txt` from the same CI run. Publishing/tagging is a separate owner action after review and manual validation.
