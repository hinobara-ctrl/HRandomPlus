# v0.2.1 directed hardening

## Root cause

`LazerCurrentBeatmapSource` keyed its cached Realm resolution only by beatmap GUID and display name. A fresh runtime event for a reimported map could therefore retain the old Realm hash/blob. In addition, a successful import did not explicitly invalidate that resolution when lazer emitted no useful selection event.

The cache now includes the runtime observation revision. A new event forces one Realm resolution even when GUID and display name are unchanged, while an unchanged event remains cached. A successful lazer import explicitly invalidates the current resolution once.

## Final classifications

| Finding | Classification | Result |
|---|---|---|
| Updated/imported lazer map remains stale | FIXED | Event revision participates in the cache key; successful import invalidates once. |
| Active `runtime.log` selection | FIXED | All valid log names compete by `LastWriteTimeUtc` through one selection method. |
| Windows stable `Process` lifetime | FIXED | Every inspected process not returned to the caller is disposed; returned ownership remains with the caller. |
| Wine `cmd` metacharacters | FIXED | Paths are supplied through environment variables, never inserted into the interpreted command text; delayed expansion is off. |
| Missing lazer resources | FIXED | Missing referenced main audio fails with a named error; nonessential visual/hitsound resources remain optional. |
| ZIP case sensitivity | FIXED | Case-distinct resource names are retained with `StringComparer.Ordinal`. |
| Linux CI | FIXED | The full test job runs on `windows-latest` and `ubuntu-latest`. |
| Manual selection documentation | FIXED | Documentation matches the current UI: stable-only, disabled while lazer is active. |
| Custom `storage.ini` | ACCEPTED AS-IS | Previously passed on real Windows and Linux; no storage-discovery behavior changed. |
| GitHub Actions references | FIXED | Existing major versions are pinned to their exact release commits. |
| NuGet lock files | FIXED | Relevant projects use committed lock files and CI restores in locked mode. |
| Lazer data ignore rules | FIXED | Narrow patterns cover Realm/runtime data without excluding broad fixture types. |
| Diagnostic path privacy | FUTURE | Full local paths remain useful during playtesting; redaction belongs with a future logging-level design. |
| Textual Realm fallback performance | FUTURE | Benchmark only if users report slow selection; current cache limits it to selection changes. |

No randomizer, parser output, seed behavior, profile behavior, storage discovery, source arbitration or licensing model changed in this hardening pass.

## Validation snapshot

- Locked restore: passed.
- Local Release build: passed with 0 errors.
- Automated suite: 134 passed, 0 failed.
- Windows x64 publish: self-contained and framework-dependent passed.
- Linux x64 cross-publish: self-contained and framework-dependent passed.
- Ubuntu CI execution: configured and required by the release-candidate dependency graph; it will run after push.
- Linux real-machine checks for the v0.2.1 delta: intentionally left to the owner in `PLAYTEST_CHECKLIST.md`.
- Secret/personal-data scan of the current tree and patch: passed; no Realm database or runtime log is tracked.
