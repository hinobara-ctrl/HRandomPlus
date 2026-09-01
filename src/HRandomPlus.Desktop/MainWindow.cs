using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using HRandomPlus.Beatmaps;
using HRandomPlus.Core;
using HRandomPlus.Desktop.Platform;
using HRandomPlus.Integration.Beatmaps;
using HRandomPlus.Integration.Importing;
using HRandomPlus.Integration.Lazer;

namespace HRandomPlus.Desktop;

public sealed class MainWindow : Window
{
    private readonly SettingsStore store = new();
    private readonly AppSettings settings;
    private IBeatmapSource source;
    private readonly BeatmapGenerationService generator = new();
    private readonly IProcessRunner processRunner = new SystemProcessRunner();
    private readonly DetectionStateTracker detectionState = new();
    private readonly CancellationTokenSource pollingCancellation = new();
    private readonly List<RandomProfile> profiles = new();
    private readonly Dictionary<string, TextBox> editors = new();

    private readonly ComboBox profileBox = new();
    private readonly TextBlock beatmapTitle = Text("No beatmap selected", 20, FontWeight.SemiBold);
    private readonly TextBlock beatmapDetails = Text("Select a .osu manually or open osu!stable / osu!lazer.");
    private readonly TextBlock beatmapPath = Text("");
    private readonly TextBlock status = Text("Starting...");
    private readonly RadioButton wholeMap = new() { Content = "Whole map", GroupName = "range" };
    private readonly RadioButton selectedRange = new() { Content = "Selected range", GroupName = "range" };
    private readonly TextBox rangeBox = new() { Text = "00:37:005 - 01:13:005 -" };
    private readonly TextBox seedBox = new() { PlaceholderText = "Random" };
    private readonly CheckBox dynamicThreshold = new() { Content = "Dynamic threshold" };
    private readonly CheckBox preserveDualStages = new() { Content = "Preserve dual stages (10K+)", IsEnabled = false };
    private readonly CheckBox renameDifficulty = new() { Content = "Rename difficulty" };
    private readonly CheckBox outputToBeatmapFolder = new() { Content = "Write beside the original beatmap" };
    private readonly TextBox bpmBox = new() { PlaceholderText = "Select a beatmap" };
    private readonly TextBlock detectedBpms = Text("BPM: —");
    private readonly Dictionary<int, TextBlock> snapValues = new();
    private readonly TextBox tosuHost = new();
    private readonly TextBox tosuPort = new();
    private readonly StackPanel platformSettingsPanel = new() { Spacing = 10 };
    private readonly Button randomizeButton = new() { Content = "RANDOMIZE CURRENT MAP", IsEnabled = false, Height = 46 };
    private readonly Button manualBeatmapButton = new() { Content = "Select .osu manually" };
    private readonly Button configureStableButton = new()
    {
        Content = OperatingSystem.IsWindows() ? "Configure osu!stable" : "Configure native osu! path"
    };
    private readonly Button saveProfileButton = new() { Content = "Save profile" };
    private readonly Button deleteProfileButton = new() { Content = "Delete profile" };
    private readonly Button resetCustomButton = new() { Content = "Reset Custom" };

    private HRandomConfig activeConfig = new();
    private string? currentPath;
    private LazerBeatmapSelectionContext? currentLazerContext;
    private bool randomizing;

    public MainWindow()
    {
        settings = store.Load();
        source = PlatformSourceFactory.Create(settings);
        profiles.AddRange(ProfileCatalog.CreateBuiltIns(settings.CustomConfig, settings.CustomProfileId));
        profiles.AddRange(settings.CustomProfiles);
        Title = "HRandomPlus";
        Width = 1120;
        Height = 800;
        MinWidth = 900;
        MinHeight = 650;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Content = BuildUi();
        ReloadProfiles(settings.LastProfile);
        wholeMap.IsChecked = settings.WholeMap;
        selectedRange.IsChecked = !settings.WholeMap;
        rangeBox.IsEnabled = !settings.WholeMap;
        outputToBeatmapFolder.IsChecked = settings.OutputToBeatmapFolder;
        tosuHost.Text = settings.TosuHost;
        tosuPort.Text = settings.TosuPort.ToString(CultureInfo.InvariantCulture);
        Closed += (_, _) =>
        {
            pollingCancellation.Cancel();
            DisposeSource(source);
            SaveSettings();
        };
        store.Log($"Avalonia application started on {Environment.OSVersion.Platform}");
        _ = PollLoopAsync(pollingCancellation.Token);
    }

    private Control BuildUi()
    {
        var root = new Grid
        {
            Margin = new Thickness(18),
            ColumnDefinitions = new ColumnDefinitions("3*,2*")
        };
        var left = new StackPanel { Spacing = 10, Margin = new Thickness(0, 0, 16, 0) };
        var leftScroll = new ScrollViewer { Content = left };
        Grid.SetColumn(leftScroll, 0);
        root.Children.Add(leftScroll);

        left.Children.Add(Section("CURRENT BEATMAP"));
        left.Children.Add(beatmapTitle);
        left.Children.Add(beatmapDetails);
        left.Children.Add(beatmapPath);
        manualBeatmapButton.Click += async (_, _) => await SelectManualAsync();
        configureStableButton.Click += async (_, _) => await SelectOsuFolderAsync();
        var selectButtons = Row();
        selectButtons.Children.Add(manualBeatmapButton);
        selectButtons.Children.Add(configureStableButton);
        left.Children.Add(selectButtons);

        left.Children.Add(Section("PROFILE"));
        profileBox.HorizontalAlignment = HorizontalAlignment.Stretch;
        profileBox.SelectionChanged += (_, _) => LoadSelectedProfile();
        left.Children.Add(profileBox);
        var profileButtons = Row();
        saveProfileButton.Click += (_, _) => SaveProfile();
        deleteProfileButton.Click += (_, _) => DeleteProfile();
        resetCustomButton.Click += async (_, _) => await ResetCustomAsync();
        profileButtons.Children.Add(saveProfileButton);
        profileButtons.Children.Add(Button("Duplicate", DuplicateProfileAsync));
        profileButtons.Children.Add(deleteProfileButton);
        profileButtons.Children.Add(resetCustomButton);
        left.Children.Add(profileButtons);
        left.Children.Add(Row(
            Button("Import profile", ImportProfileAsync),
            Button("Export profile", ExportProfileAsync)));

        left.Children.Add(Section("RANGE"));
        wholeMap.IsCheckedChanged += (_, _) => UpdateRangeState();
        selectedRange.IsCheckedChanged += (_, _) => UpdateRangeState();
        left.Children.Add(Row(wholeMap, selectedRange));
        left.Children.Add(rangeBox);

        left.Children.Add(Section("SEED"));
        left.Children.Add(seedBox);
        left.Children.Add(Button("Generate random seed", () => seedBox.Text = SeededRandom.CreateSeed().ToString(CultureInfo.InvariantCulture)));

        left.Children.Add(Section("PLATFORM AND OUTPUT"));
        Control tosuHostSetting = Labeled("tosu host", tosuHost);
        Control tosuPortSetting = Labeled("tosu port", tosuPort);
        tosuHostSetting.IsVisible = !OperatingSystem.IsWindows();
        tosuPortSetting.IsVisible = !OperatingSystem.IsWindows();
        platformSettingsPanel.IsEnabled = !OperatingSystem.IsWindows();
        platformSettingsPanel.Children.Add(tosuHostSetting);
        platformSettingsPanel.Children.Add(tosuPortSetting);
        platformSettingsPanel.Children.Add(outputToBeatmapFolder);
        platformSettingsPanel.Children.Add(Button("Apply settings", ApplyPlatformSettings));
        left.Children.Add(platformSettingsPanel);
        randomizeButton.Click += async (_, _) => await RandomizeAsync();
        left.Children.Add(randomizeButton);
        left.Children.Add(Section("STATUS"));
        left.Children.Add(status);

        var parameters = new StackPanel { Spacing = 7, Margin = new Thickness(12) };
        parameters.Children.Add(Text("Active parameters", 18, FontWeight.SemiBold));
        parameters.Children.Add(dynamicThreshold);
        parameters.Children.Add(preserveDualStages);
        AddEditor(parameters, "MinThresholdMs", "Minimum threshold (ms)");
        AddEditor(parameters, "BaseThresholdMs", "Base threshold (ms)");
        AddEditor(parameters, "MaxThresholdMs", "Maximum threshold (ms)");
        parameters.Children.Add(Section("BPM / SNAP REFERENCE"));
        bpmBox.TextChanged += (_, _) => UpdateSnapReference();
        parameters.Children.Add(Labeled("Reference BPM", bpmBox));
        parameters.Children.Add(detectedBpms);
        parameters.Children.Add(BuildSnapReference());
        AddEditor(parameters, "RecentUsageWindow", "Recent usage window");
        AddEditor(parameters, "PatternHistoryLength", "Pattern history length");
        AddEditor(parameters, "WeightedTopCandidates", "Weighted top candidates");
        AddEditor(parameters, "WeightedTemperature", "Weighted temperature");
        AddEditor(parameters, "MaxCandidateSets", "Maximum candidate sets");
        AddEditor(parameters, "DifficultySuffix", "Difficulty suffix");
        parameters.Children.Add(renameDifficulty);
        parameters.Children.Add(Section("SCORING WEIGHTS"));
        AddEditor(parameters, "TimeSinceLastUseBonus", "Time since last use bonus");
        AddEditor(parameters, "HandBalanceBonus", "Hand balance bonus");
        AddEditor(parameters, "DistributionBonus", "Distribution bonus");
        AddEditor(parameters, "JackPenalty", "Jack penalty");
        AddEditor(parameters, "TrillPenalty", "Trill penalty");
        AddEditor(parameters, "RepeatedPatternPenalty", "Repeated pattern penalty");
        AddEditor(parameters, "SameHandPenalty", "Same hand penalty");
        AddEditor(parameters, "ExtremeJumpPenalty", "Extreme jump penalty");
        AddEditor(parameters, "RecentUsagePenalty", "Recent usage penalty");
        var parameterScroll = new ScrollViewer { Content = parameters, BorderThickness = new Thickness(1), BorderBrush = Brushes.Gray };
        Grid.SetColumn(parameterScroll, 1);
        root.Children.Add(parameterScroll);
        return root;
    }

    private async Task SelectManualAsync()
    {
        try
        {
            IStorageFolder? startFolder = await GetPickerStartFolderAsync(preferSongsFolder: true);
            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select an osu!mania beatmap",
                AllowMultiple = false,
                SuggestedStartLocation = startFolder,
                FileTypeFilter = new[] { new FilePickerFileType("osu! beatmap") { Patterns = new[] { "*.osu" } } }
            });
            string? path = files.FirstOrDefault()?.TryGetLocalPath();
            if (path is not null)
            {
                settings.LastManualDirectory = Path.GetDirectoryName(path);
                SaveSettings();
                detectionState.MarkManualSelection();
                SetBeatmap(path, "Manual beatmap selected", null);
            }
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task SelectOsuFolderAsync()
    {
        try
        {
            IStorageFolder? startFolder = await GetPickerStartFolderAsync(preferSongsFolder: false);
            IReadOnlyList<IStorageFolder> folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select the osu!stable folder that contains Songs",
                AllowMultiple = false,
                SuggestedStartLocation = startFolder
            });
            string? path = folders.FirstOrDefault()?.TryGetLocalPath();
            if (path is null) return;
            if (!Directory.Exists(Path.Combine(path, "Songs")))
                throw new InvalidDataException("The selected folder does not contain Songs.");
            if (OperatingSystem.IsWindows()) settings.OsuPath = path;
            else settings.LinuxOsuPath = path;
            SaveSettings();
            ReplaceSource(PlatformSourceFactory.Create(settings));
            SetStatus("osu! path saved");
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task<IStorageFolder?> GetPickerStartFolderAsync(bool preferSongsFolder)
    {
        var candidates = new List<string?>();
        if (preferSongsFolder)
        {
            candidates.Add(settings.LastManualDirectory);
            if (currentPath is not null) candidates.Add(Path.GetDirectoryName(currentPath));
        }

        string? osuRoot = OperatingSystem.IsWindows() ? settings.OsuPath : settings.LinuxOsuPath;
        if (!string.IsNullOrWhiteSpace(osuRoot))
        {
            string songs = Path.Combine(osuRoot, "Songs");
            candidates.Add(preferSongsFolder && Directory.Exists(songs) ? songs : osuRoot);
        }

        foreach (string? candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate) || !Directory.Exists(candidate)) continue;
            try
            {
                IStorageFolder? folder = await StorageProvider.TryGetFolderFromPathAsync(candidate);
                if (folder is not null) return folder;
            }
            catch { }
        }

        try { return await StorageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Downloads); }
        catch { return null; }
    }

    private void SetBeatmap(string path, string state, LazerBeatmapSelectionContext? lazerContext = null)
    {
        path = Path.GetFullPath(path);
        OsuBeatmapDocument document = OsuBeatmapDocument.Parse(path, File.ReadAllBytes(path));
        if (document.Mode != 3) throw new InvalidDataException("The selected file is not an osu!mania beatmap.");
        currentPath = path;
        currentLazerContext = lazerContext;
        beatmapTitle.Text = $"{document.Artist} - {document.Title}";
        beatmapDetails.Text = $"[{document.Version}]  ·  {document.Creator}  ·  {document.Keys}K";
        beatmapPath.Text = path;
        IReadOnlyList<double> bpms = document.GetBpms();
        detectedBpms.Text = BeatSnapReference.DescribeBpmRange(bpms);
        bpmBox.Text = bpms.Count == 0 ? string.Empty : FormatNumber(bpms[0]);
        preserveDualStages.IsEnabled = DualStageLayout.IsEligible(document.Keys);
        randomizeButton.IsEnabled = true;
        SetStatus(state);
        store.Log($"Beatmap selected: {path}");
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                if (randomizing) continue;
                try
                {
                    IBeatmapSource activeSource = source;
                    BeatmapSourceResult result = await activeSource.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
                    if (cancellationToken.IsCancellationRequested) break;
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        UpdateSourceSpecificControls(result);
                        BeatmapDetectionUpdate update = detectionState.Observe(result);
                        if ((update.SelectionChanged || update.OriginChanged) && result.Selection is not null)
                        {
                            try { SetBeatmap(result.Selection.NativePath, BeatmapStatusFormatter.Format(update, currentPath is not null), result.Selection.LazerContext); }
                            catch (Exception ex) { SetStatus(ex.Message); }
                        }
                        else if (update.ShouldUpdateUi)
                        {
                            SetStatus(BeatmapStatusFormatter.Format(update, currentPath is not null));
                        }
                        if (update.ConnectivityChanged)
                        {
                            store.Log(result.IsAvailable ? "Detection source connected" : "Detection source disconnected");
                        }
                        if (update.StatusChanged)
                            store.Log($"Detection status: {result.Status}");
                    });
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    string message = $"Unexpected polling error: {ex.Message}";
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            BeatmapDetectionUpdate update = detectionState.Observe(BeatmapSourceResult.Unavailable(message));
                            if (update.ConnectivityChanged) store.Log("Detection source disconnected");
                            if (update.StatusChanged) store.Log(message);
                            if (update.ShouldUpdateUi) SetStatus(BeatmapStatusFormatter.Format(update, currentPath is not null));
                        });
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    private void UpdateSourceSpecificControls(BeatmapSourceResult result)
    {
        bool lazerActive = result.DetectionSource == BeatmapDetectionSource.Lazer && result.IsAvailable;
        manualBeatmapButton.IsEnabled = !lazerActive;
        configureStableButton.IsEnabled = !lazerActive;
        platformSettingsPanel.IsEnabled = OperatingSystem.IsLinux() && !lazerActive;
    }

    private async Task RandomizeAsync()
    {
        if (currentPath is null) return;
        string snapshot = currentPath;
        try
        {
            HRandomConfig config = ReadConfig();
            config.Seed = string.IsNullOrWhiteSpace(seedBox.Text)
                ? null
                : long.Parse(seedBox.Text, CultureInfo.InvariantCulture);
            BeatmapRange? range = selectedRange.IsChecked == true ? BeatmapRange.Parse(rangeBox.Text ?? "") : null;
            string profile = profileBox.SelectedItem?.ToString() ?? "Custom";
            string rangeDescription = range is null ? "Whole map" : $"Selected range {range.Value.StartMs}-{range.Value.EndMs} ms";
            randomizing = true;
            randomizeButton.IsEnabled = false;
            SetStatus("Randomizing...");
            bool outputBeside = outputToBeatmapFolder.IsChecked == true;
            LazerBeatmapSelectionContext? lazerContext = currentLazerContext;
            bool useLazer = lazerContext is not null;
            bool useWineSide = !useLazer && BeatmapImportPolicy.ShouldUseWineSide(OperatingSystem.IsLinux(), outputBeside);
            string? outputDirectory = useLazer || useWineSide || !outputBeside ? AppPaths.OutputDirectory : null;
            IBeatmapImporter importer = useLazer
                ? new LazerArchiveImporter()
                : useWineSide ? new WineSideFileImporter(processRunner) : new DirectFileImporter();
            string importStrategy = useLazer ? "lazer-osz" : useWineSide ? "wine-side-copy" : "direct-file";
            store.Log($"Randomize started; platform={Environment.OSVersion.Platform}; beatmap={snapshot}; profile={profile}; range={rangeDescription}; seed={(config.Seed?.ToString(CultureInfo.InvariantCulture) ?? "random")}; importStrategy={importStrategy}");
            GenerationResult result = await Task.Run(() => generator.Generate(snapshot, config, range, outputDirectory));
            BeatmapImportResult import = await importer.ImportAsync(
                new BeatmapImportRequest(snapshot, result.OutputPath, AppPaths.OutputDirectory, lazerContext),
                pollingCancellation.Token);
            if (useLazer && import.Success && source is ILazerResolutionInvalidator invalidator)
                invalidator.InvalidateLazerResolution();
            seedBox.Text = result.Seed.ToString(CultureInfo.InvariantCulture);
            string importMessage = useWineSide || useLazer ? $"\n{import.Message}" : string.Empty;
            SetStatus($"Map generated: {result.OutputVersion}\nSeed: {result.Seed}\nOutput: {import.PreservedOutputPath}{importMessage}");
            store.Log($"Randomize completed; output={import.PreservedOutputPath}; seed={result.Seed}; importStrategy={import.Strategy}; automaticAttempted={import.AutomaticImportAttempted}; fallback={import.FallbackUsed}; importSuccess={import.Success}; message={import.Message}");
            if (!string.IsNullOrWhiteSpace(import.Diagnostics)) store.Log($"Import diagnostics: {import.Diagnostics}");
        }
        catch (Exception ex) { ShowError(ex); }
        finally
        {
            randomizing = false;
            randomizeButton.IsEnabled = currentPath is not null;
        }
    }

    private void ReloadProfiles(string? selected)
    {
        profileBox.ItemsSource = null;
        profileBox.ItemsSource = profiles.Select(p => p.Name).ToArray();
        profileBox.SelectedIndex = Math.Max(0, profiles.FindIndex(p => p.Name.Equals(selected, StringComparison.OrdinalIgnoreCase)));
    }

    private void LoadSelectedProfile()
    {
        if (profileBox.SelectedIndex < 0 || profileBox.SelectedIndex >= profiles.Count) return;
        activeConfig = profiles[profileBox.SelectedIndex].Config.Clone();
        LoadConfig(activeConfig);
        settings.LastProfile = profiles[profileBox.SelectedIndex].Name;
        UpdateProfileActions(profiles[profileBox.SelectedIndex]);
        SaveSettings();
    }

    private void SaveProfile()
    {
        RandomProfile? selected = SelectedProfile();
        if (selected is null) return;
        try { activeConfig = ReadConfig(); }
        catch (Exception ex) { ShowError(ex); return; }

        if (selected.BuiltIn && selected.Name.Equals(ProfileCatalog.CustomName, StringComparison.OrdinalIgnoreCase))
        {
            ProfileOperations.Save(selected, settings, activeConfig);
            SaveSettings();
            SetStatus("Custom profile saved");
            return;
        }

        if (selected.BuiltIn)
        {
            SetStatus("H-Random and S-Random are protected. Use Duplicate to create a variant.");
            return;
        }

        ProfileOperations.Save(selected, settings, activeConfig);
        SyncPersonalProfiles();
        SaveSettings();
        SetStatus($"Profile saved: {selected.Name}");
    }

    private async Task DuplicateProfileAsync()
    {
        RandomProfile? selected = SelectedProfile();
        if (selected is null) return;
        HRandomConfig config;
        try { config = ReadConfig(); }
        catch (Exception ex) { ShowError(ex); return; }

        ProfileDetails? details = await PromptProfileDetailsAsync($"{selected.Name} Copy", selected.Description);
        if (details is null) return;
        try
        {
            RandomProfile duplicate = ProfileOperations.Duplicate(
                selected,
                config,
                details.Name,
                details.Description,
                profiles.Select(profile => profile.Name));
            profiles.Add(duplicate);
            SyncPersonalProfiles();
            SaveSettings();
            ReloadProfiles(duplicate.Name);
            SetStatus($"Profile created: {duplicate.Name}");
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void DeleteProfile()
    {
        RandomProfile? selected = SelectedProfile();
        if (selected is null || selected.BuiltIn) return;
        profiles.Remove(selected);
        SyncPersonalProfiles();
        SaveSettings();
        ReloadProfiles(ProfileCatalog.HRandomName);
        SetStatus($"Profile deleted: {selected.Name}");
    }

    private async Task ResetCustomAsync()
    {
        RandomProfile? selected = SelectedProfile();
        if (selected is null || !selected.BuiltIn || !selected.Name.Equals(ProfileCatalog.CustomName, StringComparison.OrdinalIgnoreCase)) return;
        if (!await ConfirmAsync("Reset Custom", "Restore the default Custom parameters? This cannot be undone.", "Reset")) return;
        ProfileOperations.ResetCustom(selected, settings);
        activeConfig = selected.Config.Clone();
        LoadConfig(activeConfig);
        SaveSettings();
        SetStatus("Custom profile reset");
    }

    private async Task ImportProfileAsync()
    {
        try
        {
            IStorageFolder? startFolder = await StorageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Downloads);
            IReadOnlyList<IStorageFile> files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import HRandomPlus profile",
                AllowMultiple = false,
                SuggestedStartLocation = startFolder,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("HRandomPlus profile") { Patterns = new[] { "*.hrp-profile.json" } }
                }
            });
            string? path = files.FirstOrDefault()?.TryGetLocalPath();
            if (path is null) return;

            RandomProfile incoming = ProfileTransfer.Read(path);
            RandomProfile? existing = settings.CustomProfiles.FirstOrDefault(profile => profile.Id == incoming.Id);
            ProfileImportDecision decision = await PromptImportAsync(incoming, existing is not null);
            if (decision == ProfileImportDecision.Cancel) return;

            RandomProfile? imported = ProfileTransfer.Import(settings.CustomProfiles, incoming, decision);
            if (imported is null) return;
            RebuildProfiles(imported.Name);
            SaveSettings();
            SetStatus(existing is not null && decision == ProfileImportDecision.Update
                ? $"Profile updated: {imported.Name}"
                : $"Profile imported: {imported.Name}");
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task ExportProfileAsync()
    {
        RandomProfile? selected = SelectedProfile();
        if (selected is null) return;
        try
        {
            var exportProfile = selected.Clone();
            IStorageFolder? startFolder = await StorageProvider.TryGetWellKnownFolderAsync(WellKnownFolder.Downloads);
            IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export HRandomPlus profile",
                SuggestedStartLocation = startFolder,
                SuggestedFileName = ProfileTransfer.SuggestedFileName(exportProfile),
                DefaultExtension = "json",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("HRandomPlus profile") { Patterns = new[] { "*.hrp-profile.json" } }
                }
            });
            string? path = file?.TryGetLocalPath();
            if (path is null) return;
            ProfileTransfer.Export(path, exportProfile);
            SetStatus($"Profile exported: {path}");
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private async Task<ProfileDetails?> PromptProfileDetailsAsync(string initialName, string initialDescription)
    {
        var name = new TextBox { Text = initialName };
        var description = new TextBox { Text = initialDescription, AcceptsReturn = true, Height = 70, TextWrapping = TextWrapping.Wrap };
        var dialog = new Window { Title = "Profile details", Width = 460, Height = 280, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var save = new Button { Content = "Create" };
        var cancel = new Button { Content = "Cancel" };
        var panel = new StackPanel { Margin = new Thickness(16), Spacing = 8 };
        panel.Children.Add(Text("Profile name"));
        panel.Children.Add(name);
        panel.Children.Add(Text("Description (optional)"));
        panel.Children.Add(description);
        panel.Children.Add(Row(cancel, save));
        dialog.Content = panel;
        save.Click += (_, _) => dialog.Close(new ProfileDetails(name.Text ?? string.Empty, description.Text ?? string.Empty));
        cancel.Click += (_, _) => dialog.Close((ProfileDetails?)null);
        return await dialog.ShowDialog<ProfileDetails?>(this);
    }

    private async Task<ProfileImportDecision> PromptImportAsync(RandomProfile profile, bool hasIdConflict)
    {
        var dialog = new Window { Title = "Import profile", Width = 600, Height = 430, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new StackPanel { Margin = new Thickness(18), Spacing = 8 };
        panel.Children.Add(Text(profile.Name, 20, FontWeight.SemiBold));
        if (!string.IsNullOrWhiteSpace(profile.Description)) panel.Children.Add(Text(profile.Description));
        panel.Children.Add(Text($"Format: {ProfileTransfer.FormatVersion}  ·  Engine: {ProfileTransfer.EngineVersion}"));
        panel.Children.Add(Text($"Thresholds: {profile.Config.MinThresholdMs} / {profile.Config.BaseThresholdMs} / {profile.Config.MaxThresholdMs} ms"));
        panel.Children.Add(Text($"Seed: {(profile.Config.Seed?.ToString(CultureInfo.InvariantCulture) ?? "Random")}"));
        ScoringWeights weights = profile.Config.Weights;
        panel.Children.Add(Text($"Weights: jack {weights.JackPenalty}, trill {weights.TrillPenalty}, repeated {weights.RepeatedPatternPenalty}, recent {weights.RecentUsagePenalty}"));
        if (hasIdConflict) panel.Children.Add(Text("A personal profile with the same ID already exists."));

        var cancel = new Button { Content = "Cancel" };
        var copy = new Button { Content = hasIdConflict ? "Import as copy" : "Import" };
        var buttons = Row(cancel, copy);
        if (hasIdConflict)
        {
            var update = new Button { Content = "Update existing" };
            update.Click += (_, _) => dialog.Close(ProfileImportDecision.Update);
            buttons.Children.Add(update);
        }
        panel.Children.Add(buttons);
        dialog.Content = panel;
        cancel.Click += (_, _) => dialog.Close(ProfileImportDecision.Cancel);
        copy.Click += (_, _) => dialog.Close(hasIdConflict ? ProfileImportDecision.ImportAsCopy : ProfileImportDecision.Update);
        return await dialog.ShowDialog<ProfileImportDecision>(this);
    }

    private async Task<bool> ConfirmAsync(string title, string message, string acceptText)
    {
        var dialog = new Window { Title = title, Width = 440, Height = 180, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var accept = new Button { Content = acceptText };
        var cancel = new Button { Content = "Cancel" };
        var panel = new StackPanel { Margin = new Thickness(16), Spacing = 12 };
        panel.Children.Add(Text(message));
        panel.Children.Add(Row(cancel, accept));
        dialog.Content = panel;
        accept.Click += (_, _) => dialog.Close(true);
        cancel.Click += (_, _) => dialog.Close(false);
        return await dialog.ShowDialog<bool>(this);
    }

    private RandomProfile? SelectedProfile()
        => profileBox.SelectedIndex >= 0 && profileBox.SelectedIndex < profiles.Count
            ? profiles[profileBox.SelectedIndex]
            : null;

    private void UpdateProfileActions(RandomProfile profile)
    {
        bool custom = profile.BuiltIn && profile.Name.Equals(ProfileCatalog.CustomName, StringComparison.OrdinalIgnoreCase);
        saveProfileButton.Content = custom ? "Save Custom" : "Save profile";
        saveProfileButton.IsEnabled = custom || !profile.BuiltIn;
        deleteProfileButton.IsEnabled = !profile.BuiltIn;
        resetCustomButton.IsEnabled = custom;
    }

    private void SyncPersonalProfiles()
        => settings.CustomProfiles = profiles.Where(profile => !profile.BuiltIn).ToList();

    private void RebuildProfiles(string? selected)
    {
        profiles.Clear();
        profiles.AddRange(ProfileCatalog.CreateBuiltIns(settings.CustomConfig, settings.CustomProfileId));
        profiles.AddRange(settings.CustomProfiles);
        ReloadProfiles(selected);
    }

    private void ApplyPlatformSettings()
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                settings.TosuHost = string.IsNullOrWhiteSpace(tosuHost.Text) ? "127.0.0.1" : tosuHost.Text.Trim();
                settings.TosuPort = int.Parse(tosuPort.Text ?? "24050", CultureInfo.InvariantCulture);
                if (settings.TosuPort is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(settings.TosuPort));
            }
            settings.OutputToBeatmapFolder = outputToBeatmapFolder.IsChecked == true;
            ReplaceSource(PlatformSourceFactory.Create(settings));
            SaveSettings();
            SetStatus("Settings applied");
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void UpdateRangeState()
    {
        rangeBox.IsEnabled = selectedRange.IsChecked == true;
        settings.WholeMap = wholeMap.IsChecked == true;
        SaveSettings();
    }

    private void LoadConfig(HRandomConfig config)
    {
        seedBox.Text = config.Seed?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        dynamicThreshold.IsChecked = config.DynamicThreshold;
        preserveDualStages.IsChecked = config.PreserveDualStages;
        renameDifficulty.IsChecked = config.RenameDifficulty;
        Set("MinThresholdMs", config.MinThresholdMs); Set("BaseThresholdMs", config.BaseThresholdMs);
        Set("MaxThresholdMs", config.MaxThresholdMs); Set("RecentUsageWindow", config.RecentUsageWindow);
        Set("PatternHistoryLength", config.PatternHistoryLength); Set("WeightedTopCandidates", config.WeightedTopCandidates);
        Set("WeightedTemperature", config.WeightedTemperature); Set("MaxCandidateSets", config.MaxCandidateSets);
        Set("DifficultySuffix", config.DifficultySuffix);
        Set("TimeSinceLastUseBonus", config.Weights.TimeSinceLastUseBonus); Set("HandBalanceBonus", config.Weights.HandBalanceBonus);
        Set("DistributionBonus", config.Weights.DistributionBonus); Set("JackPenalty", config.Weights.JackPenalty);
        Set("TrillPenalty", config.Weights.TrillPenalty); Set("RepeatedPatternPenalty", config.Weights.RepeatedPatternPenalty);
        Set("SameHandPenalty", config.Weights.SameHandPenalty); Set("ExtremeJumpPenalty", config.Weights.ExtremeJumpPenalty);
        Set("RecentUsagePenalty", config.Weights.RecentUsagePenalty);
    }

    private HRandomConfig ReadConfig()
    {
        var values = editors.ToDictionary(pair => pair.Key, pair => pair.Value.Text ?? string.Empty,
            StringComparer.Ordinal);
        HRandomConfig config = HRandomConfigInputParser.Parse(new HRandomConfigInput(
            seedBox.Text ?? string.Empty,
            dynamicThreshold.IsChecked == true,
            preserveDualStages.IsChecked == true,
            renameDifficulty.IsChecked == true,
            values));
        activeConfig = config;
        return config.Clone();
    }

    private void AddEditor(Panel panel, string key, string label)
    {
        var editor = new TextBox();
        editors[key] = editor;
        panel.Children.Add(Labeled(label, editor));
    }

    private Control BuildSnapReference()
    {
        const int columnCount = 3;
        int rowCount = (BeatSnapReference.CommonDivisors.Count + columnCount - 1) / columnCount;
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(string.Join(',', Enumerable.Repeat("*", columnCount))),
            RowDefinitions = new RowDefinitions(string.Join(',', Enumerable.Repeat("Auto", rowCount))),
            ColumnSpacing = 8,
            RowSpacing = 3,
            Margin = new Thickness(0, 4, 0, 4)
        };
        for (int index = 0; index < BeatSnapReference.CommonDivisors.Count; index++)
        {
            int divisor = BeatSnapReference.CommonDivisors[index];
            int row = index / columnCount;
            int column = index % columnCount;
            var value = Text($"1/{divisor} · —");
            value.FontWeight = FontWeight.SemiBold;
            snapValues[divisor] = value;
            Grid.SetRow(value, row); Grid.SetColumn(value, column);
            grid.Children.Add(value);
        }
        return grid;
    }

    private void UpdateSnapReference()
    {
        string text = bpmBox.Text?.Trim() ?? string.Empty;
        bool parsed = double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double bpm) ||
                      double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out bpm);
        foreach ((int divisor, TextBlock value) in snapValues)
            value.Text = parsed && double.IsFinite(bpm) && bpm > 0
                ? $"1/{divisor} · {BeatSnapReference.Milliseconds(bpm, divisor):0.###} ms"
                : $"1/{divisor} · —";
    }

    private static string FormatNumber(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    private void Set(string key, object value) => editors[key].Text = Convert.ToString(value, CultureInfo.InvariantCulture);
    private void SaveSettings() { try { store.Save(settings); } catch (Exception ex) { store.Log($"Could not save settings: {ex.Message}"); } }
    private void SetStatus(string message) => status.Text = message;
    private void ShowError(Exception ex) { SetStatus("Error: " + ex.Message); store.Log($"ERROR {ex}"); }
    private void ReplaceSource(IBeatmapSource replacement)
    {
        IBeatmapSource previous = source;
        source = replacement;
        detectionState.Reset();
        DisposeSource(previous);
    }
    private static void DisposeSource(IBeatmapSource value)
    {
        if (value is IDisposable disposable) disposable.Dispose();
    }

    private static TextBlock Text(string value, double size = 14, FontWeight? weight = null)
        => new() { Text = value, FontSize = size, FontWeight = weight ?? FontWeight.Normal, TextWrapping = TextWrapping.Wrap };
    private static TextBlock Section(string title)
        => new() { Text = title, FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = Brushes.Gray, Margin = new Thickness(0, 12, 0, 0) };
    private static StackPanel Row(params Control[] controls)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (Control control in controls) panel.Children.Add(control);
        return panel;
    }
    private static Control Labeled(string label, Control control)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,140") };
        grid.Children.Add(Text(label));
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);
        return grid;
    }
    private static Button Button(string label, Action action)
    {
        var button = new Button { Content = label };
        button.Click += (_, _) => action();
        return button;
    }
    private static Button Button(string label, Func<Task> action)
    {
        var button = new Button { Content = label };
        button.Click += async (_, _) => await action();
        return button;
    }

    private sealed record ProfileDetails(string Name, string Description);
}
