# Third-party notices

HRandomPlus is distributed under `GPL-3.0-or-later`; the complete terms are in the root `LICENSE`. It includes or embeds the third-party components listed below, and every third-party component retains its own copyright notices and license terms.

The versions and source revisions below come from the resolved NuGet manifests, `project.assets.json`, and the runtime assets observed in platform-specific publishes. The two distributed application packages are framework-dependent and do not redistribute the .NET Runtime or host.

## Redistributed components

| Component | Version | License | Platform | Exact source | Use in HRandomPlus |
|---|---:|---|---|---|---|
| OsuMemoryDataProvider | 0.12.2 | GPL-3.0-or-later | Windows only | [commit `122dd102fe272de30471cf1f317805cb49ac23a4`, tag `osu_v0.12.2`](https://github.com/Piotrekol/ProcessMemoryDataFinder/tree/122dd102fe272de30471cf1f317805cb49ac23a4/OsuMemoryDataProvider) | Reads the selected osu!stable beatmap from process memory. Embedded in `HRandomPlus.exe`. |
| ProcessMemoryDataFinder | 0.10.2 | GPL-3.0-or-later | Windows only | [commit `122dd102fe272de30471cf1f317805cb49ac23a4`, tag `process_v0.10.2`](https://github.com/Piotrekol/ProcessMemoryDataFinder/tree/122dd102fe272de30471cf1f317805cb49ac23a4/ProcessMemoryDataFinder) | Transitive process-memory scanner used by OsuMemoryDataProvider. Embedded in `HRandomPlus.exe`. |
| Realm .NET | 20.1.0 | Apache-2.0 | Windows and Linux | [commit `370ce596a0cf5e992b717bb199d70e55391ff2b9`](https://github.com/realm/realm-dotnet/tree/370ce596a0cf5e992b717bb199d70e55391ff2b9) | Opens osu!lazer's `client.realm` read-only. The managed Realm assembly and platform-specific `realm-wrappers` native library are redistributed. Copyright © 2025 Realm Inc. |
| MongoDB.Bson | 2.21.0 | Apache-2.0 | Windows and Linux | [commit `5a9c3311e158910b88195f290e6d4b1b2715d2b2`](https://github.com/mongodb/mongo-csharp-driver/tree/5a9c3311e158910b88195f290e6d4b1b2715d2b2) | Runtime dependency of Realm .NET. Copyright © 2010-present MongoDB Inc. |
| Remotion.Linq | 2.2.0 | Apache-2.0 | Windows and Linux | [exact NuGet package](https://www.nuget.org/packages/Remotion.Linq/2.2.0) | Runtime LINQ provider dependency of Realm .NET. Copyright © rubicon IT GmbH. |
| Avalonia runtime package family | 12.1.1 | MIT | Windows and Linux | [commit `e33eaed9c106846b200680751022385d9cc5dc6f`, tag `12.1.1`](https://github.com/AvaloniaUI/Avalonia/tree/e33eaed9c106846b200680751022385d9cc5dc6f) | Desktop UI, platform integration, rendering and Fluent theme. Managed runtime assemblies are embedded. |
| Avalonia.Angle.Windows.Natives | 2.1.27548.20260419 | BSD-3-Clause-style package license | Windows only | [commit `1c89805903c1482166356d3b950d474973180e61`](https://github.com/AvaloniaUI/angle/tree/1c89805903c1482166356d3b950d474973180e61) | ANGLE/OpenGL native backend; distributed as `av_libglesv2.dll`. |
| SkiaSharp and Win32/Linux native assets | 3.119.4 | MIT, plus licenses in its official third-party notice | Windows and Linux | [commit `f568ac94dd768ef9a2f593537cfde2dd0d348ef5`, tag `v3.119.4`](https://github.com/mono/SkiaSharp/tree/f568ac94dd768ef9a2f593537cfde2dd0d348ef5) | 2D rendering. The selected native library is external on Windows and embedded on Linux. |
| HarfBuzzSharp and Win32/Linux native assets | 8.3.1.3 | MIT, plus licenses in its official third-party notice | Windows and Linux | [commit `2888c737ad016d584c74525e2d35db5097ea8576`](https://github.com/mono/SkiaSharp/tree/2888c737ad016d584c74525e2d35db5097ea8576) | Text shaping. The selected native library is external on Windows and embedded on Linux. |
| MicroCom.Runtime | 0.11.6 | MIT | Windows and Linux | [commit `76785efcafd91b5902fd19dd11145f6dd655b7b4`](https://github.com/kekekeks/MicroCom/tree/76785efcafd91b5902fd19dd11145f6dd655b7b4) | COM interop support used by Avalonia platform code. |
| Tmds.DBus.Protocol | 0.94.1 | MIT | Windows and Linux | [commit `b4a7fed0b878f74cb54f7cca84d2889af4e596ba`](https://github.com/tmds/Tmds.DBus/tree/b4a7fed0b878f74cb54f7cca84d2889af4e596ba) | D-Bus protocol dependency of Avalonia FreeDesktop support. The managed assembly is embedded in both application publishes. Package copyright: Tom Deseyn. |
| System.IO.Pipelines | 8.0.0 | MIT | Windows and Linux | [commit `5535e31a712343a63f5d7d796cd874e563e5ac14`](https://github.com/dotnet/runtime/tree/5535e31a712343a63f5d7d796cd874e563e5ac14) | Runtime pipeline dependency of Tmds.DBus.Protocol. |

`Avalonia.BuildServices 11.3.2` is used during build only and contributes no runtime asset. Restore metadata also contains native packages for unrelated runtime identifiers; they are not copied into the platform-specific publishes. `Microsoft.CSharp 4.7.0` and `System.Data.DataSetExtensions 4.5.0` are visible in the transitive restore graph of the Windows memory packages, but their NuGet package assets are not redistributed separately; the publish uses the corresponding assemblies from the .NET 8.0.30 runtime pack.

`Fody 6.9.1` and Realm's Fody weaver are resolved build-only tools, are disabled because HRandomPlus uses Realm's dynamic API, and are not redistributed. Realm's older `System.*` package dependencies are satisfied by .NET 8 runtime assemblies in the final application rather than copied as separate legacy package binaries.

## License files packaged by platform

The distributed Windows package includes:

- `GPL-3.0-or-later.txt`
- `Apache-2.0.txt` for Realm .NET, MongoDB.Bson and Remotion.Linq
- `Avalonia-LICENSE.md` and `Avalonia-NOTICE.md`
- `ANGLE-LICENSE.txt`
- `SkiaSharp-HarfBuzzSharp-LICENSE.txt` and `SkiaSharp-HarfBuzzSharp-THIRD-PARTY-NOTICES.txt`
- `MicroCom-LICENSE.txt`
- `MIT-SPDX.txt` for packages declaring the SPDX `MIT` expression without shipping a separate license file; copyright attribution remains in this notice
- `System.IO.Pipelines-LICENSE.txt` and `System.IO.Pipelines-THIRD-PARTY-NOTICES.txt`

The distributed Linux package includes the same applicable permissive notices, including `Apache-2.0.txt`, except it omits `GPL-3.0-or-later.txt` and `ANGLE-LICENSE.txt` because those Windows-only components are absent.

Both packages require an independently installed .NET 8 x64 runtime. They do not redistribute .NET, so .NET Runtime license/notice files are not packaged. The official .NET 8.0.30 texts retained under `licenses/` document earlier audited self-contained outputs and are not selected by the current workflow.

## GPL corresponding source

The Release candidate includes `HRandomPlus-v0.2.1-playtest-gpl-source.zip`. It contains the complete upstream repository snapshot at commit `122dd102fe272de30471cf1f317805cb49ac23a4`, which is the exact commit declared by both GPL NuGet packages. The tags `osu_v0.12.2` and `process_v0.10.2` both resolve to that commit. A manifest inside the bundle records this mapping and the official archive checksum.

The separate `HRandomPlus-v0.2.1-playtest-source.zip` contains the complete HRandomPlus source corresponding to the candidate binaries, including the solution, project files, NuGet configuration, GitHub Actions workflow, tests, configuration example, documentation, notices and license texts needed to rebuild and audit it. Release binaries and both source archives are distributed together as Release assets and identified by `SHA256SUMS.txt`.
