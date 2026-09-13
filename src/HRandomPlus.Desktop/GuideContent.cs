namespace HRandomPlus.Desktop;

public sealed record GuideEntry(string Key, string Title, string Description);
public sealed record GuideSection(string Title, string Introduction, IReadOnlyList<GuideEntry> Entries);

public static class GuideContent
{
    public static IReadOnlyList<GuideSection> Sections { get; } = new[]
    {
        new GuideSection("Getting started",
            "HRandomPlus creates a separate difficulty. The original beatmap is not modified.", new[]
        {
            new GuideEntry("Start", "Generate a variant",
                "1. Select a .osu manually, or open osu!stable / osu!lazer and select a mania difficulty. Check the displayed map and source.\n" +
                "2. Choose H-Random, S-Random, Custom or a saved profile.\n" +
                "3. Choose Whole map or Selected range. A range uses note start times; a long note must also end inside the range to be moved. Notes outside it keep their columns.\n" +
                "4. Optionally enter a seed or use Generate Seed.\n" +
                "5. Review Active parameters.\n" +
                "6. Press Randomize Current Map and read Status for the output location and import result.\n" +
                "7. Open the new difficulty in osu! and check that it loads with its resources."),
            new GuideEntry("Seed", "Seed controls",
                "An empty seed generates a new value on each run. Generate Seed fills the box; Hold Seed copies the last used value; Delete Seed clears it. " +
                "The used seed is shown in Status. The same input, selected notes, parameters and seed reproduce the column assignments. " +
                "Changing a profile can also change the seed. Output names may differ because existing files are kept."),
            new GuideEntry("Range", "Selected range",
                "Example: 00:37:005 - 01:13:005 selects 37,005 through 73,005 ms, including the endpoints. " +
                "Long notes crossing a boundary stay unchanged. Columns occupied by long notes already active at the start are respected.")
        }),
        new GuideSection("Parameters",
            "These values do not directly represent difficulty. They control how candidate column arrangements are evaluated and selected. " +
            "Column availability and long-note constraints are applied before scoring; a weight cannot force an unavailable arrangement. " +
            "Changing a value can change the result even with the same seed.", new[]
        {
            new GuideEntry("DynamicThreshold", "Dynamic threshold",
                "When enabled, the threshold is derived from note density across up to eight previous note groups and the current group. " +
                "Denser history moves it toward Maximum threshold; sparser history moves it toward Minimum threshold. " +
                "At the start or after a pause longer than eight times Maximum threshold it uses Base threshold. Disabled: always uses Base threshold. " +
                "This is based on note events, not the Reference BPM box."),
            new GuideEntry("PreserveDualStages", "Preserve dual stages (10K+)",
                "In 10K and above, restricts lateral notes to their original side instead of allowing direct moves to the opposite stage. " +
                "For odd key counts, the middle column is shared and can exchange notes with either side. " +
                "Example: in 10K, a note from columns 1–5 stays in columns 1–5. Below 10K the option is disabled and has no effect."),
            new GuideEntry("MinThresholdMs", "Minimum threshold (ms)",
                "The sparse-history end of the dynamic threshold. Raising it lengthens the reuse interval in sparse passages; lowering it shortens that interval. " +
                "Example: 40 ms prefers columns last used more than 40 ms ago. If too few are available, older reusable columns are considered. " +
                "Must be between zero and Base threshold. It has no threshold effect when Dynamic threshold is off."),
            new GuideEntry("BaseThresholdMs", "Base threshold (ms)",
                "The fixed threshold when Dynamic threshold is off, and the starting/reset value when it is on. " +
                "Higher values prefer a longer interval before reusing a column; lower values allow shorter intervals. " +
                "Example: at 100 ms, a column used 90 ms ago is not in the preferred pool. This is a preference, not an absolute no-jack rule. " +
                "Must lie between Minimum and Maximum threshold."),
            new GuideEntry("MaxThresholdMs", "Maximum threshold (ms)",
                "The dense-history end of the dynamic threshold. Raising it lengthens that reuse preference; lowering it shortens it. " +
                "It also sets the time scale of Time since last use bonus, the trill gap limit (four times this value), and the dynamic reset pause (eight times it), " +
                "even where the fixed threshold is selected. Example: 160 ms gives a 640 ms trill gap limit. Must be at least Base threshold."),
            new GuideEntry("ReferenceBpm", "Reference BPM / BPM-Snap Reference",
                "A display-only conversion from BPM to milliseconds for snaps from 1/1 to 1/64. It initially uses the first detected BPM; multiple BPMs are shown as a range. " +
                "Higher BPM shows shorter intervals; lower BPM shows longer intervals. Example: at 120 BPM, 1/4 is 125 ms. " +
                "Editing this box does not change thresholds, timing, profiles or the generated columns."),
            new GuideEntry("RecentUsageWindow", "Recent usage window",
                "Number of recent column assignments retained for column usage, and recent non-central assignments retained separately for hand usage. " +
                "A chord contributes multiple assignments. Example: 24 keeps up to 24 assignments, not 24 chords or milliseconds. " +
                "Higher values include longer history; lower values forget it sooner. This also normalizes Recent usage penalty and Hand balance bonus, " +
                "so changing it is not just a change in strength. Range: 4–256."),
            new GuideEntry("PatternHistoryLength", "Pattern history length",
                "Number of previous note groups retained. Each group contains all columns assigned at one timestamp. " +
                "Higher values let repeated-pattern and trill scoring see farther back; lower values shorten that history. " +
                "Example: 16 retains up to 16 groups. Dynamic threshold uses at most the latest eight of these. Range: 4–256."),
            new GuideEntry("WeightedTopCandidates", "Weighted top candidates",
                "Maximum number of highest-scoring generated candidate sets admitted to weighted selection. " +
                "1 restricts selection to the first best-scoring candidate; 12 allows up to the best 12. " +
                "Higher values admit more alternatives, lower values narrow the selection. The actual count may be smaller. " +
                "Must be at least 1 and no greater than Maximum candidate sets; Weighted temperature controls selection within this group."),
            new GuideEntry("WeightedTemperature", "Weighted temperature",
                "Controls how strongly score differences affect probabilities among the admitted candidates. " +
                "Higher positive values flatten probabilities; lower values concentrate them on higher scores. Equal scores remain equally weighted. " +
                "Example: for a score gap of 12, temperature 12 gives the lower candidate about 0.37 times the weight of the higher; temperature 24 gives about 0.61. " +
                "It cannot admit candidates excluded by Weighted top candidates. Must be finite and greater than zero."),
            new GuideEntry("MaxCandidateSets", "Maximum candidate sets",
                "Caps candidate enumeration or sampling. Small spaces are enumerated; larger spaces are sampled with duplicate removal and an attempt limit. " +
                "Higher values allow more candidates and more work; lower values reduce that budget. It does not guarantee that many candidates. " +
                "Example: a space larger than 4096 combinations is sampled at a limit of 4096. Range: 1–8192; default 4096. " +
                "Weighted top candidates cannot exceed this value."),
            new GuideEntry("DifficultySuffix", "Difficulty suffix",
                "Text used in the generated filename and, with Rename difficulty enabled, appended to the difficulty name. " +
                "Example: a suffix of ' CUSTOM' labels a variant of 'Hard' as 'Hard CUSTOM'. Existing names receive numeric disambiguation. " +
                "Unicode is allowed; filename separators, control characters and trailing spaces or dots are rejected. " +
                "There is no numeric higher/lower meaning and it does not affect column scoring."),
            new GuideEntry("RenameDifficulty", "Rename difficulty",
                "Enabled: updates the displayed difficulty name using Difficulty suffix and avoids repeatedly stacking a recognized generated suffix. " +
                "Disabled: keeps the original displayed difficulty name, but still writes a separate uniquely named file. " +
                "The original remains unchanged in either case."),
            new GuideEntry("ScoringWeights", "How scoring weights work",
                "Weights are not percentages and their scales are not necessarily comparable 1:1. A weight of 80 is not automatically four times as important as 20. " +
                "Each term activates under different conditions and can have internal or contextual multipliers. " +
                "For a fixed event, increasing a positive weight strengthens its described bonus or penalty; decreasing it toward zero weakens it, and zero disables that term. " +
                "Negative finite weights are accepted and reverse that term's sign. The entries below describe positive weights."),
            new GuideEntry("TimeSinceLastUseBonus", "Time since last use bonus",
                "Adds score per column according to time since its last note start, capped at twice the weight. " +
                "Example: with Maximum threshold 160 ms, a 160 ms gap earns one weight unit and a 320 ms gap reaches the cap. " +
                "Increasing the weight strengthens preference for longer-unused columns; decreasing it weakens that preference. Maximum threshold controls the time scale."),
            new GuideEntry("HandBalanceBonus", "Hand balance bonus",
                "Adds score for assignments to the less recently used hand, and subtracts it for the more used hand, normalized by Recent usage window. " +
                "Example: if the recent history has more right-hand assignments, left columns receive a bonus. " +
                "Increasing the weight strengthens this response; decreasing it weakens it. In odd key counts the center is neutral for hand accounting."),
            new GuideEntry("DistributionBonus", "Distribution bonus",
                "Adds score for each candidate column whose recent usage is below the most-used column. " +
                "Example: if the maximum recent count is 6 and a column's count is 2, it gets four times this weight. " +
                "Increasing the weight strengthens this preference; decreasing it weakens it. Counts come from Recent usage window."),
            new GuideEntry("JackPenalty", "Jack penalty",
                "Subtracts score when a candidate column was last used within the current threshold, including its boundary. " +
                "Shorter gaps receive a larger multiplier. Example: with a 100 ms threshold, reuse after 40 ms receives a stronger penalty than after 90 ms. " +
                "Increasing the weight strengthens this penalty; decreasing it weakens it. Candidate filtering already prefers columns outside the threshold, " +
                "so this only distinguishes candidates that actually reach scoring."),
            new GuideEntry("TrillPenalty", "Trill penalty",
                "Subtracts score when a candidate extends a two-column alternation to four or more consecutive groups; the multiplier increases with continuation length. " +
                "Example: A–B–A followed by B triggers it. Chords may accompany the alternating anchor, but containing both anchors breaks that sequence. " +
                "Increasing the weight strengthens this penalty; decreasing it weakens it. Pattern history length bounds the history; a gap greater than four times Maximum threshold breaks it."),
            new GuideEntry("RepeatedPatternPenalty", "Repeated pattern penalty",
                "Subtracts one weight unit for each exact match of the candidate's sorted column set in retained pattern history. " +
                "Example: if columns {1,3} occurred twice, choosing {1,3} subtracts twice this weight. " +
                "Increasing the weight strengthens the penalty; decreasing it weakens it. Pattern history length controls how many earlier groups can match."),
            new GuideEntry("SameHandPenalty", "Same hand penalty",
                "Subtracts score when a multi-note candidate lies entirely in one hand, multiplied by its note count. " +
                "Example: in 4K, {1,2} triggers it, while {1,3} does not. A neutral center in an odd key count prevents an all-one-hand match. " +
                "Increasing the weight strengthens this penalty; decreasing it weakens it. Single-note candidates do not trigger it."),
            new GuideEntry("ExtremeJumpPenalty", "Extreme jump penalty",
                "After a single-note group, subtracts score per candidate column for a jump spanning at least 75% of the full column range, " +
                "provided the gap is at most twice the current threshold. Example: column 1 to 4 in 4K triggers it; 1 to 2 does not. " +
                "Increasing the weight strengthens the distance-scaled penalty; decreasing it weakens it. A previous chord does not trigger this term."),
            new GuideEntry("RecentUsagePenalty", "Recent usage penalty",
                "Subtracts score per candidate column in proportion to its retained usage count divided by Recent usage window. " +
                "Example: 6 uses with a window of 24 subtract one quarter of the weight. " +
                "Increasing the weight strengthens this penalty; decreasing it weakens it. Distribution bonus uses the same column counts but a different scale.")
        }),
        new GuideSection("Profiles", "H-Random, S-Random, Custom and personal profiles use the same engine with different configurations.", new[]
        {
            new GuideEntry("BuiltIns", "H-Random, S-Random and Custom",
                "H-Random uses dynamic thresholds and nonzero scoring weights. S-Random uses zero thresholds and zero scoring weights, " +
                "with weighted selection across up to 4096 candidates; column availability and long-note constraints still apply. " +
                "These are protected presets. Custom is one editable, persistent configuration with its own defaults. No preset is a universal difficulty level."),
            new GuideEntry("Save", "Save Profile / Duplicate / Reset",
                "Edits in Active parameters are used for generation. Save Profile persists them and the seed for Custom or the selected personal profile. " +
                "Save before switching profiles or exporting: Export Profile uses the saved profile, not unsaved edits. " +
                "Duplicate captures the current parameters into a new personal profile with a new identity. " +
                "H-Random and S-Random cannot be overwritten; use Duplicate. Reset asks for confirmation and restores only Custom's defaults."),
            new GuideEntry("Transfer", "Import Profile / Export Profile / Delete Profile",
                "Export Profile writes a .hrp-profile.json containing the saved parameters, seed, name, description and profile identity. " +
                "It excludes game paths, connection settings and logs. Import Profile validates the file and shows a preview; an existing identity can be updated or imported as a copy. " +
                "Names are disambiguated and built-in names are reserved. The imported profile is stored locally. Delete Profile removes only personal profiles.")
        }),
        new GuideSection("Integration", "Check Status for the active source, output path and any fallback message.", new[]
        {
            new GuideEntry("StableWindows", "osu!stable on Windows",
                "Open stable and select a mania difficulty. Automatic detection reads the running stable process. Use the same permission level for both applications. " +
                "Configure osu!stable selects the installation containing Songs. Multiple eligible stable processes can prevent automatic detection: close extra instances or select a .osu manually. " +
                "Generated difficulties are copied beside the original."),
            new GuideEntry("StableLinux", "osu!stable on Linux / Winello",
                "Run HRandomPlus natively and stable with tosu in the same Wine environment; Winello provides osu-wine --tosu. " +
                "The default tosu connection is 127.0.0.1:24050. Apply settings saves the Linux connection settings. " +
                "Configure native osu! path selects the folder containing Songs if automatic location fails. " +
                "The app tries a copy through Wine, then a native copy; after a native fallback osu! may need F5."),
            new GuideEntry("Lazer", "osu!lazer on Windows / Linux",
                "Open native lazer and enter Song Select. No tosu, Wine or Songs configuration is needed. " +
                "Standard storage and compatible storage.ini / portable locations are detected. A generated variant and its set resources are sent as an .osz with detached online IDs. " +
                "HRandomPlus does not edit lazer's database or original blobs. A successful launch means the archive was sent; check that lazer actually imported it. " +
                "Stable-specific manual/configuration controls are disabled while lazer is the active source."),
            new GuideEntry("Detection", "If the beatmap is not detected",
                "Check that the selected map is osu!mania, change difficulty in the game, and read Status. For stable, verify its installation and (on Linux) tosu connection, " +
                "or use Select .osu manually. Manual selection persists until the game changes map. When stable and lazer coexist, the most recently changed selection wins. " +
                "Ambiguous lazer names are left unresolved; choose a distinguishable difficulty. For a manual stable-style workflow, close lazer and select an exported/extracted .osu with its resources."),
            new GuideEntry("Output", "Output and Failed Imports",
                "Generation first writes into HRandomPlus's Generated Beatmaps data folder. Successful stable copying removes that staging file; lazer retains it. " +
                "Files receive unique names without overwriting other generations. If copying or importing fails, a portable .osz is preserved in Failed Imports beside the HRandomPlus executable when possible. " +
                "An incomplete lazer archive is discarded and a fresh portable fallback is attempted; a completed archive is kept when only launching fails. " +
                "If Failed Imports cannot be written, read the reported paths: the generated .osu remains, and a completed lazer archive may remain temporarily in the system temp folder. " +
                "Move such a temporary archive promptly; old lazer import archives are cleaned up later.")
        })
    };
}
