# Dependency and distribution license audit

Date: 2026-08-29

Status: **WINDOWS RELEASE TECHNICALLY READY**

This is a technical inventory, not legal advice. It was produced from project references, resolved `project.assets.json` files, NuGet manifests and license files, official .NET 8.0.30 distributions, upstream source revisions, and multi-file self-contained publishes for `win-x64` and `linux-x64`.

## Runtime inventory

| Dependency | Version | License | Platform | Redistributed? | Evidence/source revision |
|---|---:|---|---|---|---|
| HRandomPlus-owned code | 0.1.0-playtest | GPL-3.0-or-later | Windows/Linux | Yes | Root `LICENSE`; exact candidate source bundle |
| OsuMemoryDataProvider | 0.12.2 | GPL-3.0-or-later | Windows | **Yes**, embedded | NuGet commit `122dd102fe272de30471cf1f317805cb49ac23a4`; tag `osu_v0.12.2` |
| ProcessMemoryDataFinder | 0.10.2 | GPL-3.0-or-later | Windows | **Yes**, embedded | Same commit; tag `process_v0.10.2` |
| Avalonia runtime family | 12.1.1 | MIT | Windows/Linux | Yes, embedded | NuGet commit/tag `e33eaed9c106846b200680751022385d9cc5dc6f` / `12.1.1` |
| Avalonia.BuildServices | 11.3.2 | MIT | Build only | **No runtime asset** | Present in restore/deps metadata only |
| Avalonia.Angle.Windows.Natives | 2.1.27548.20260419 | License file in package (BSD-3-Clause-style terms) | Windows | Yes, `av_libglesv2.dll` | NuGet commit `1c89805903c1482166356d3b950d474973180e61` |
| SkiaSharp + selected native assets | 3.119.4 | MIT + official third-party notices | Windows/Linux | Yes | NuGet commit/tag `f568ac94dd768ef9a2f593537cfde2dd0d348ef5` / `v3.119.4` |
| HarfBuzzSharp + selected native assets | 8.3.1.3 | MIT + official third-party notices | Windows/Linux | Yes | NuGet commit `2888c737ad016d584c74525e2d35db5097ea8576` |
| MicroCom.Runtime | 0.11.6 | MIT | Windows/Linux | Yes, embedded | NuGet commit `76785efcafd91b5902fd19dd11145f6dd655b7b4` |
| Tmds.DBus.Protocol | 0.94.1 | MIT | Windows/Linux | Yes, embedded | NuGet commit `b4a7fed0b878f74cb54f7cca84d2889af4e596ba` |
| System.IO.Pipelines | 8.0.0 | MIT + official third-party notices | Windows/Linux | Yes, embedded | NuGet commit `5535e31a712343a63f5d7d796cd874e563e5ac14` |
| Microsoft.CSharp package | 4.7.0 | MIT | Windows restore graph | **No package runtime asset** | The publish uses the .NET runtime-pack assembly instead |
| System.Data.DataSetExtensions package | 4.5.0 | MIT license URL | Windows restore graph | **No package runtime asset** | The publish uses the .NET runtime-pack assembly instead |
| .NET Runtime/host | 8.0.30 | .NET Library license (Windows); MIT (Linux); official third-party notices | Windows/Linux | Yes, self-contained | Official 8.0.30 runtime archives; source tag `v8.0.30`, commit `a83db3e0eb2defb6220e15dae2f1a0462fdbf99f` |
| Native packages for unrelated RIDs | Resolved versions above | Varies | Restore metadata only | **No** | Absent from platform-selected runtime assets |

## Publish evidence

The Windows multi-file audit publish contains runtime assets from both GPL packages, Avalonia, ANGLE, SkiaSharp, HarfBuzzSharp, MicroCom, Tmds.DBus.Protocol, System.IO.Pipelines and the .NET 8.0.30 runtime pack. The final single-file candidate embeds managed assemblies and leaves `av_libglesv2.dll`, `libHarfBuzzSharp.dll` and `libSkiaSharp.dll` alongside the executable.

The Linux audit publish contains no OsuMemoryDataProvider, ProcessMemoryDataFinder or ANGLE Windows native runtime asset. It uses tosu over HTTP. Avalonia's dependency graph still contributes managed platform assemblies, Tmds.DBus.Protocol and System.IO.Pipelines; selected Linux Skia/HarfBuzz native libraries and .NET native libraries are embedded by the final single-file publish.

## Official license material

- GPL text: verbatim upstream `LICENSE` from ProcessMemoryDataFinder commit `122dd102fe272de30471cf1f317805cb49ac23a4`.
- Avalonia license/notice: verbatim `licence.md` and `NOTICE.md` from commit `e33eaed9c106846b200680751022385d9cc5dc6f`.
- ANGLE: verbatim `LICENSE` contained in `Avalonia.Angle.Windows.Natives 2.1.27548.20260419`.
- SkiaSharp/HarfBuzzSharp: verbatim `LICENSE.txt` and identical `THIRD-PARTY-NOTICES.txt` contained in their selected native NuGet packages.
- MicroCom: verbatim upstream `LICENSE` from commit `76785efcafd91b5902fd19dd11145f6dd655b7b4`.
- Tmds.DBus.Protocol: the NuGet manifest declares SPDX `MIT` and copyright `Tom Deseyn`; the distribution includes the official SPDX MIT text and retains the package attribution in `THIRD_PARTY_NOTICES.md`.
- System.IO.Pipelines: verbatim `LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT` from package 8.0.0.
- .NET: verbatim `LICENSE.txt` and `ThirdPartyNotices.txt` extracted separately from the official Windows x64 and Linux x64 .NET Runtime 8.0.30 archives. Their published SHA-512 values were verified against Microsoft's release metadata.

## GPL source correspondence prepared

`HRandomPlus-v0.1.0-playtest-gpl-source.zip` contains the complete ProcessMemoryDataFinder repository snapshot at commit `122dd102fe272de30471cf1f317805cb49ac23a4`, not a link to a moving branch. That single commit is declared in both NuGet manifests and is the target of both release tags. The bundle includes all repository files, project/build material and the upstream GPL license, plus a provenance manifest.

`HRandomPlus-v0.1.0-playtest-source.zip` contains the HRandomPlus source tree used to build the candidate artifacts.

## Distribution readiness

- HRandomPlus-owned code is licensed `GPL-3.0-or-later` under the complete root `LICENSE`.
- The HRandomPlus source ZIP is generated from the exact repository revision used to build the binaries and includes the solution, source, tests, build/project configuration, example configuration, workflow, documentation and license material.
- The separate GPL upstream source ZIP contains the immutable commit corresponding to both embedded memory packages.
- Windows and Linux binary ZIPs retain the applicable third-party notices and official license texts without bundling unnecessary framework source.
- `SHA256SUMS.txt` identifies the exact files in the release set.

On completion of the build, source-correspondence and ZIP-content checks documented in the release-readiness report, the Windows candidate is **TECHNICALLY READY FOR PUBLICATION**. This inventory records engineering evidence and is not legal advice.
