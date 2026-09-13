<!-- document-status: current -->
# Dependency and distribution inventory

The resolved dependency versions and per-platform notices are maintained in [THIRD_PARTY_NOTICES.md](../../THIRD_PARTY_NOTICES.md). Project references and `packages.lock.json` files are the dependency inputs.

## Current distribution

The workflow publishes exactly two framework-dependent application packages: Windows x64 and Linux x64. Both target .NET 8 and require a separately installed .NET Runtime 8 x64; neither redistributes the .NET runtime or host.

Both include the selected Realm, MongoDB.Bson, Remotion.Linq, Avalonia, SkiaSharp/HarfBuzzSharp, MicroCom, Tmds.DBus.Protocol and System.IO.Pipelines runtime assets. Windows additionally includes OsuMemoryDataProvider, ProcessMemoryDataFinder and ANGLE. Build-only Fody/Realm weaving is disabled for the dynamic Realm integration and contributes no runtime asset.

The root LICENSE and THIRD_PARTY_NOTICES accompany both packages. Applicable third-party texts are copied from `licenses/` by the workflow. The historical .NET runtime license files remain as evidence for earlier self-contained distributions, but are not packaged now.

## Corresponding sources and verification

`Directory.Build.props` defines the candidate version. CI creates `HRandomPlus-v<version>-source.zip` from the exact build commit and `HRandomPlus-v<version>-gpl-source.zip` from the pinned upstream snapshot recorded in [GPL_SOURCE_MANIFEST.md](GPL_SOURCE_MANIFEST.md). The snapshot checksum is verified before packaging. All four archives are listed in SHA256SUMS.txt and release-evidence.txt from the same workflow run.

Inspect both published package contents and their notices before release; source packages must accompany binaries. A previous audit is not evidence that a new archive has been inspected.

The [earlier detailed dependency audit](../historical/2026-09-12_DEPENDENCY_LICENSE_AUDIT_PRE_V1.md) preserves the package provenance and self-contained publish observations without presenting them as current release approval.
