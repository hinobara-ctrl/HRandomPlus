# HRandomPlus v0.1.1 optimization study

Date started: 2026-08-30

Reference revision: `06d9bd5` (`Prepare GPL v0.1.0 playtest release`)

## Scope and rules

This is a conservative, measurement-first study. `v0.1.0-playtest` remains the stable reference and `v0.2.0` remains reserved for osu!lazer. Experiments are isolated. A change is accepted only when it has a measurable benefit, preserves observable behavior and reproducible output, passes the existing tests, and has a reasonable maintenance cost.

The self-contained packages remain the primary distribution. Framework-dependent packages may only be added as optional variants. Licenses, notices and corresponding-source assets are never removed to save space.

## Measurement method

- Binary measurements use the final `v0.1.0-playtest` ZIPs from `outputs/`, not a new build.
- Installed size is the sum of uncompressed file lengths in each ZIP.
- Windows startup is measured from `Process.Start` until the first responsive main-window handle. Seven launches use separate temporary working directories. An attempted per-process `LOCALAPPDATA` override did not redirect the Windows special-folder API, so the launches shared the existing per-user HRandomPlus state. The first run is retained as a cold-start observation; the reported normal startup is the median of the full set and is representative of the six warm runs.
- Idle RAM is sampled two seconds after the window becomes responsive. A separate stabilized run samples memory after five seconds and CPU over the following ten seconds.
- Generation uses the real `HRandomPlus.Cli` archive path and the production parser/randomizer with seed `123456`. Deterministic 7K `.osz` maps contain 500, 5,000 and 50,000 notes at 20 ms spacing. One warm-up is discarded and six runs are measured. Times include CLI startup, archive read/write, parsing, randomization and report writing.
- Values are machine-specific and are intended for before/after comparisons on the same host, not as universal performance claims.

## Baseline: v0.1.0-playtest

### Package size

| Platform/build | Application file | Installed directory | ZIP | Files | Status |
|---|---:|---:|---:|---:|---|
| Windows x64 self-contained | 76,998,317 B | 96,262,053 B | 42,122,507 B | 20 | Reference |
| Linux x64 self-contained | 90,100,511 B | 90,478,119 B | 39,610,618 B | 15 | Reference |

The largest external Windows runtime files are `libSkiaSharp.dll` (11,628,896 B), `av_libglesv2.dll` (5,394,096 B) and `libHarfBuzzSharp.dll` (1,816,088 B). Linux embeds its selected native libraries into the single application file.

### Windows startup and idle memory

| Metric | Baseline |
|---|---:|
| Cold first observation | 7,304.7 ms |
| Median startup, 7 runs | 1,090.8 ms |
| Warm-run range excluding first observation | 1,064.0–1,143.8 ms |
| Average working set, 7 runs | 250,349,861 B |
| Average private bytes, 7 runs | 212,834,011 B |
| Stabilized working set after 5 s | 227,561,472 B |
| Stabilized private bytes after 5 s | 185,655,296 B |
| CPU consumed during following 10 s | 375 ms, approximately 3.75% of one logical core |

The cold first observation is an outlier and may include OS, antivirus or single-file extraction/cache effects. It must not be compared directly with a warm experiment.

### Generation baseline on Windows

| Map | Notes | Input `.osz` | Median time | Range | Median peak working set |
|---|---:|---:|---:|---:|---:|
| Small | 500 | 2,191 B | 181.47 ms | 169.96–186.06 ms | 38,604,800 B |
| Normal | 5,000 | 17,488 B | 294.41 ms | 278.82–311.79 ms | 63,352,832 B |
| Large | 50,000 | 171,333 B | 1,108.66 ms | 1,076.81–1,154.22 ms | 182,464,512 B |

No engine optimization is justified by these numbers alone. The large synthetic map completes in approximately 1.1 seconds including process and archive overhead. Parser/cache work will only proceed if further profiling demonstrates meaningful repeated work in a real application operation.

### Linux runtime verification

The Linux package-size baseline is complete. The framework-dependent candidate was subsequently smoke-tested on a real Linux installation with .NET 8 x64 Runtime installed. Application startup, UI, beatmap detection, manual selection, H-Random, S-Random, Custom, generation, fixed-seed reproducibility, profiles, and output/import behavior all passed. No new performance metric was inferred from that functional test.

## Experiment comparison

| Baseline | Experiment | Difference | Result |
|---|---|---|---|
| Self-contained package | Framework-dependent | Windows ZIP −72.39%; Linux ZIP −76.26%; Windows warm startup −11.4% | ACCEPTED as optional only |
| Uncompressed single-file payload | `EnableCompressionInSingleFile=true` | Windows ZIP −1.73%, warm startup +9.0%, working set +13.3%; Linux ZIP −2.25% | REJECTED |
| Current publish contents | Debug/development-file audit | Final ZIPs already contain no PDB, XML docs, test, temp or build files | ACCEPTED baseline; no change |
| Current Avalonia graph | Platform-backend review | Foreign-backend candidates are at most ~1.5 MB installed per platform and require platform-specific bootstrap changes | REJECTED for v0.1.1 |
| Current runtime behavior | Parser/cache review | Extreme 50,000-note generation is ~0.5 s direct / ~1.1 s through CLI archive; repeated parses enforce original/output validation | REJECTED for v0.1.1 |
| Current 200 ms detection polling | Idle-work investigation | 375 ms CPU over 10 s, ~3.75% of one logical core or ~0.24% of the 16-thread machine | ACCEPTED baseline; no change |
| Current globalization | Invariant globalization | No Windows size change; Linux application grew 1,152 B | REJECTED |
| Current untrimmed publish | Partial trimming | No effective size change in isolated publish; reflection remains a compatibility risk | REJECTED |
| Current JIT/runtime | NativeAOT / ReadyToRun / Dynamic PGO | NativeAOT excluded; startup does not justify ReadyToRun; Dynamic PGO is runtime-managed | FUTURE |

### Framework-dependent experiment

| Platform | Build | Application | Packaged directory | ZIP |
|---|---|---:|---:|---:|
| Windows x64 | self-contained baseline | 76,998,317 B | 96,262,053 B | 42,122,507 B |
| Windows x64 | framework-dependent final candidate | 9,674,819 B | 28,833,889 B | 11,611,873 B |
| Linux x64 | self-contained baseline | 90,100,511 B | 90,478,119 B | 39,610,618 B |
| Linux x64 | framework-dependent final candidate | 23,409,809 B | 23,692,976 B | 9,388,588 B |

On Windows, seven framework-dependent startup runs produced a 966.2 ms median, 229,394,725 B average working set and 194,448,823 B average private bytes. Against the baseline this is approximately 124.6 ms faster with 20.96 MB less working set and 18.39 MB less private memory. The experiment opened successfully and uses the same application assemblies. It is accepted only as an optional download because it transfers the runtime prerequisite to the user.

The framework-dependent packages deliberately omit the .NET Runtime license/notice files because they do not redistribute that runtime. All application and other third-party notices remain present. The self-contained packages and their filenames remain the primary downloads.

### Single-file compression experiment

| Platform | Compressed application | Compressed package | Compressed ZIP | ZIP change |
|---|---:|---:|---:|---:|
| Windows x64 | 38,513,596 B | 57,777,332 B | 41,394,151 B | −728,356 B (−1.73%) |
| Linux x64 | 45,113,455 B | 45,491,063 B | 38,719,705 B | −890,913 B (−2.25%) |

Windows warm startup rose from 1,090.8 ms to 1,189.4 ms. Average working set rose to 283,534,482 B and private bytes to 236,096,366 B. The final ZIP is already compressed, so payload compression saves little download size while adding decompression cost. It is not enabled.

### Publish-content and Avalonia review

The release packager already removes PDB files. The audited ZIPs contain no tests, package caches, temporary files, XML documentation or development-only artifacts. Native SkiaSharp, HarfBuzzSharp and ANGLE files are required by the renderer.

There is no `Avalonia.Fonts.Inter` reference to remove. `Avalonia.Desktop 12.1.1` intentionally pulls the Win32, X11/FreeDesktop, native, Skia and HarfBuzz platform graph used by `UsePlatformDetect()`. Multi-file audit estimates about 1.22 MB of clearly foreign X11/FreeDesktop managed assemblies in Windows and 1.06 MB of clearly foreign Win32 managed assemblies in Linux. Replacing `Avalonia.Desktop` with hand-selected platform packages would change startup code and expand the native compatibility test matrix for dialogs, clipboard, fonts, input and DPI. That trade-off is deferred.

### Parser, polling, logging and paths

Profiling a 50,000-note document measured a median ~71.7 ms parse and ~504.4 ms direct generation. The three parses have distinct safety roles: validation of the immutable input, creation of the mutable output, and validation of the serialized result. A cache or clone layer would add correctness and invalidation risk for a fraction of an already sub-second operation, so no engine change is made.

Detection uses a 200 ms UI timer and a 250 ms Windows process-reader delay. With osu!stable open, the measured idle work is low enough that increasing these intervals would mainly make detection feel slower. Logging during the study produced approximately 175 KB over four days, including repeated instrumentation launches; rotation is not justified yet.

Linux paths already honor `XDG_CONFIG_HOME`, `XDG_DATA_HOME` and `XDG_STATE_HOME`. HRandomPlus has no persistent cache directory, so `XDG_CACHE_HOME` is not applicable.

## Functional equivalence gate

Every accepted build must pass the existing 100-test runner and retain deterministic output for the same beatmap, mode, configuration and seed. Windows UI startup and package-content audits are mandatory. Linux-sensitive changes require a real Linux smoke test before acceptance.

The final source tree builds with 0 errors and the complete runner reports 100 passed / 0 failed. A separate before/after equivalence test built the `v0.1.0-playtest` reference from revision `06d9bd5`, ran both CLI builds against the same normal test archive, configuration and seed `123456`, then compared the generated `.osu` bytes. Both outputs produced SHA-256 `ed6d3353a8bc068044215de2457bb8cab6bab260e8b215dc1f89832f67784550`.

## Accepted changes

- Add optional Windows x64 and Linux x64 framework-dependent ZIPs.
- Keep both self-contained ZIPs as the primary, no-prerequisite downloads.
- Omit only .NET Runtime notices from framework-dependent ZIPs because those builds do not redistribute .NET.
- Version the packaging candidate as `v0.1.1-playtest`.

## Rejected changes

- `EnableCompressionInSingleFile`: negligible ZIP reduction with worse Windows startup and memory.
- Trimming and invariant globalization: no measured size benefit; trimming also adds reflection risk.
- Avalonia backend surgery: small upper-bound saving for disproportionate compatibility and maintenance cost.
- Parser/cache, polling, log rotation, ReadyToRun and NativeAOT changes: no current measurement justifies their risk or complexity.

## Reproducibility and conclusion

The application source, randomizer, detection sources, UI behavior, profile format and importer logic are unchanged. The only product change is the assembly version; the only distribution change is two optional framework-dependent jobs and their license-aware packaging. The normal self-contained builds remain reproducible through `.github/workflows/build.yml`.

For `v0.1.1-playtest`, ship the self-contained ZIPs as the recommended downloads and label the framework-dependent ZIPs clearly with their .NET 8 x64 prerequisite. No experimental compression, trimming, backend removal or engine optimization should be carried forward.

## Final comparison

| Windows build | ZIP | Installed | Startup | Average working set | Decision |
|---|---:|---:|---:|---:|---|
| v0.1.0 self-contained baseline | 42,122,507 B | 96,262,053 B | 1,090.8 ms median | 250,349,861 B | Reference / primary |
| v0.1.1 framework-dependent candidate | 11,611,873 B | 28,833,889 B | 966.2 ms median | 229,394,725 B | KEEP optional |
| self-contained with payload compression | 41,394,151 B | 57,777,332 B | 1,189.4 ms median | 283,534,482 B | REJECT |
| partial trimming experiment | no effective reduction | no effective reduction | not promoted | not promoted | REJECT |

| Linux build | ZIP | Installed | Native runtime result | Decision |
|---|---:|---:|---|---|
| v0.1.0 self-contained baseline | 39,610,618 B | 90,478,119 B | Previously playtested successfully | Reference / primary |
| v0.1.1 framework-dependent candidate | 9,388,588 B | 23,692,976 B | Smoke test passed with .NET 8 x64 Runtime | KEEP optional |
| self-contained with payload compression | 38,719,705 B | 45,491,063 B | Not required after poor ZIP gain | REJECT |

## Linux smoke-test result

The Linux x64 framework-dependent build passed its final smoke test on a real Linux system with .NET 8 x64 Runtime installed. The v0.1.1 Linux gate is closed.
