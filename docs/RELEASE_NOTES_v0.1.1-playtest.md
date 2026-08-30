# HRandomPlus v0.1.1-playtest

This is a conservative packaging update for osu!stable. It does not change randomization, beatmap detection, tosu integration, Wine importing, profiles, configuration defaults or UI behavior.

## Distribution

- `HRandomPlus-v0.1.1-playtest-windows-x64.zip` and `HRandomPlus-v0.1.1-playtest-linux-x64.zip` are the recommended self-contained downloads and require no separate .NET installation.
- `HRandomPlus-v0.1.1-playtest-windows-x64-framework-dependent.zip` and `HRandomPlus-v0.1.1-playtest-linux-x64-framework-dependent.zip` are optional smaller downloads. They require .NET 8 x64 Runtime and reduce download size by approximately 72% on Windows and 76% on Linux in the controlled study.
- The Linux x64 framework-dependent build passed its final smoke test on a real Linux installation with .NET 8 x64 Runtime.
- Debug symbols, tests, build files, caches and temporary files are excluded from binary ZIPs.
- Single-file payload compression, trimming, invariant globalization and Avalonia backend removal were evaluated but not adopted because their measured benefit did not justify their runtime or compatibility cost.

Full measurements and decisions are recorded in `docs/optimization-v0.1.1.md`.

## Licensing and source

HRandomPlus remains `GPL-3.0-or-later`. Third-party components retain their own licenses and notices. The release set includes the exact HRandomPlus source, the exact upstream GPL source snapshot required by the Windows memory-reader dependencies, and `SHA256SUMS.txt`.

Self-contained ZIPs redistribute .NET 8 and include the applicable runtime license and notices. Framework-dependent ZIPs require an independently installed .NET 8 runtime and therefore do not include .NET runtime license files; all other applicable notices remain included.

## Known scope

This release supports osu!stable. osu!lazer remains reserved for v0.2.0.
