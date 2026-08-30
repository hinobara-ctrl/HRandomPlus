# osu!lazer implementation (v0.2.0-playtest)

## Audited upstream baseline

The implementation was checked against ppy/osu commit [`48c4800e3ae4ee752452cdff83bd3787ccf3105f`](https://github.com/ppy/osu/tree/48c4800e3ae4ee752452cdff83bd3787ccf3105f), not a moving branch. At this revision:

- `BeatmapInfo` maps to Realm table `Beatmap`, uses a GUID primary key and links `BeatmapSet`, `Metadata`, `Hash`, `DifficultyName` and `OnlineID`.
- `BeatmapSetInfo` maps to `BeatmapSet` and owns `RealmNamedFileUsage` entries. Each usage links a logical filename to a `RealmFile` identified by a SHA-256 hash.
- `RealmFileStore` stores content under `files/<first hash character>/<first two hash characters>/<full lowercase SHA-256>`.
- the default application storage is controlled by `storage.ini` (`FullPath`) and contains `client.realm`, `files/` and `logs/`.
- passing an accepted archive path to the desktop executable is forwarded through `ArchiveImportIPCChannel` to the running game importer.

Older lazer builds logged `Song select updating selection with beatmap:<GUID> ruleset:<ruleset>`. The audited revision no longer emits that line from Song Select; it emits `Game-wide working beatmap updated to <display name>`. HRandomPlus therefore supports both formats. A GUID is resolved directly. Text is accepted only when it identifies exactly one Realm record; zero or multiple results produce a visible unresolved state and never select an arbitrary beatmap.

## Runtime flow

```text
runtime log -> selected beatmap identity -> client.realm metadata
            -> Realm file hash -> files/<hash> .osu blob
            -> HRandomPlus -> generated difficulty -> lazer .osz import
```

The runtime log is live selection state, `client.realm` supplies metadata and relationships, and `files/` contains the physical content-addressed blobs. Realm and `files/` are read-only inputs to HRandomPlus.

1. `LazerProcessDetector` distinguishes a native lazer process from stable's traditional executable directory containing `Songs`.
2. `LazerStorageDiscovery` checks the platform default, follows `storage.ini`, and checks compatible roots beside the detected executable for portable installs.
3. `LazerRuntimeLogMonitor` scans at most the final 2 MiB on startup, then tails only appended bytes. It handles truncation, replacement, legacy `runtime*.log` names and current `<timestamp>.runtime.log` names. When more than one storage is present, the storage with the newest runtime log is selected.
4. `RealmLazerBeatmapCatalog` opens `client.realm` with `IsReadOnly = true` and `IsDynamic = true`, using the schema stored on disk instead of assuming a lazer schema version; it never starts a transaction or writes Realm/storage data.
5. `LazerBeatmapResolver` validates the selected `.osu` blob against its SHA-256 and materialises a temporary parser input. Materialisations older than seven days are removed opportunistically.
6. The unchanged HRandomPlus engine produces the new difficulty.
7. `LazerArchiveImporter` builds a temporary `.osz` containing the generated `.osu` and the original set resources. The archive copy receives detached online IDs while the retained generated output and every lazer source file remain untouched.
8. The `.osz` is passed to the detected lazer executable. On launch failure it is preserved in the HRandomPlus output folder for manual import; successful temporary archives are deleted after a grace period and stale archives are cleaned at startup.

The only filesystem writes in detection are under the system temporary directory. `client.realm`, `files/`, `logs/`, `storage.ini` and the original beatmap are read-only inputs.

## Coexistence and source identity

Windows keeps `WindowsMemoryBeatmapSource` for stable. Linux keeps `TosuBeatmapSource` and osu-winello for stable. Both are composed with `LazerCurrentBeatmapSource` by `ArbitratingBeatmapSource`. Successful selections carry an observation timestamp; when both games are open, the most recently changed selection wins. Stable, tosu and lazer statuses are formatted independently. Controls that only apply to stable are disabled while lazer is the active source.

## Dependencies added for lazer

- Realm 20.1.0, exact package repository commit [`370ce596a0cf5e992b717bb199d70e55391ff2b9`](https://github.com/realm/realm-dotnet/tree/370ce596a0cf5e992b717bb199d70e55391ff2b9), Apache-2.0.
- MongoDB.Bson 2.21.0, exact package repository commit [`5a9c3311e158910b88195f290e6d4b1b2715d2b2`](https://github.com/mongodb/mongo-csharp-driver/tree/5a9c3311e158910b88195f290e6d4b1b2715d2b2), Apache-2.0.
- Remotion.Linq 2.2.0, Apache-2.0, resolved transitively by Realm.
- Fody 6.9.1 and Realm's weaver are resolved build-only tools, disabled for this dynamic-Realm integration, and not redistributed.

See `THIRD_PARTY_NOTICES.md` and `licenses/Apache-2.0.txt` for distribution notices.

## Known limitations and safe fallback

- The current text-only upstream log is inherently less precise than the historical GUID line. Ambiguous display names remain unresolved instead of selecting an arbitrary beatmap.
- A game update that changes Realm table/property names, blob fanout or the log message can make automatic detection unresolved. It must fail closed; stable integrations remain available.
- HRandomPlus does not claim that starting the importer guarantees lazer completed the import. The UI reports that the archive was sent. Verify the new local difficulty in Song Select.
- Real client storage is intentionally not included in automated fixtures because it can contain personal account/library data.

## Real-machine smoke checklist

Run the matching v0.2.0 build and record the exact lazer version.

### Windows x64

- [x] Start HRandomPlus with neither game open; confirm an unavailable/waiting status and responsive manual picker.
- [x] Open stable only; confirm stable detection and one unchanged randomization.
- [x] Open lazer only and enter Song Select; confirm the status explicitly names osu!lazer.
- [x] Change difficulty and set; confirm each selection updates without rescanning/freezing.
- [x] Randomize; confirm lazer imports a new local difficulty, audio/background load, and the original remains unchanged.
- [x] Repeat randomization; confirm unique difficulty/file naming.
- [x] Close/reopen lazer and rotate/restart its logs; confirm recovery.
- [x] Open stable and lazer together; confirm the most recently changed selection wins with the correct source label.
- [x] Confirm stable-only manual selection retains its established behavior.

### Native Linux x64

- [x] Repeat all lazer-only generation/import checks using native lazer; do not start tosu or Wine.
- [x] Confirm default or custom `storage.ini` storage is found without `sudo`.
- [x] Re-run the established stable + osu-winello + tosu regression checklist separately.
- [x] Confirm closing HRandomPlus leaves no helper process and stale temporary archives/materialisations are eventually cleaned.

Automated coverage validates parser variants, tailing/truncation, standard/custom/portable storage, blob fanout and SHA-256, GUID resolution, ambiguous text rejection, source arbitration, explicit status labelling and detached `.osz` creation with resources. Real Windows/Linux functional playtests passed. Artificially disabling lazer's desktop archive launcher is not considered a release gate because it does not represent the normal import flow.
