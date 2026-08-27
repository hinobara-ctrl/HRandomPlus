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

namespace HRandomPlus.Desktop;

public sealed class MainWindow : Window
{
    private readonly SettingsStore store = new();
    private readonly AppSettings settings;
    private IBeatmapSource source;
    private readonly BeatmapGenerationService generator = new();
    private readonly CancellationTokenSource pollingCancellation = new();
    private readonly List<RandomProfile> profiles = new();
    private readonly Dictionary<string, TextBox> editors = new();

    private readonly ComboBox profileBox = new();
    private readonly TextBlock beatmapTitle = Text("No beatmap selected", 20, FontWeight.SemiBold);
    private readonly TextBlock beatmapDetails = Text("Select a .osu manually or open osu!stable.");
    private readonly TextBlock beatmapPath = Text("");
    private readonly TextBlock status = Text("Starting...");
    private readonly RadioButton wholeMap = new() { Content = "Whole map", GroupName = "range" };
    private readonly RadioButton selectedRange = new() { Content = "Selected range", GroupName = "range" };
    private readonly TextBox rangeBox = new() { Text = "00:37:005 - 01:13:005 -" };
    private readonly TextBox seedBox = new() { PlaceholderText = "Random" };
    private readonly CheckBox dynamicThreshold = new() { Content = "Dynamic threshold" };
    private readonly CheckBox renameDifficulty = new() { Content = "Rename difficulty" };
    private readonly CheckBox outputToBeatmapFolder = new() { Content = "Write beside the original beatmap" };
    private readonly TextBox tosuHost = new();
    private readonly TextBox tosuPort = new();
    private readonly Button randomizeButton = new() { Content = "RANDOMIZE CURRENT MAP", IsEnabled = false, Height = 46 };

    private HRandomConfig activeConfig = new();
    private string? currentPath;
    private string? lastDetectedIdentity;
    private string? lastDetectionStatus;
    private bool randomizing;

    public MainWindow()
    {
        settings = store.Load();
        source = PlatformSourceFactory.Create(settings);
        profiles.AddRange(ProfileCatalog.BuiltIns);
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
        var selectButtons = Row();
        selectButtons.Children.Add(Button("Select .osu manually", SelectManualAsync));
        selectButtons.Children.Add(Button(OperatingSystem.IsWindows() ? "Configure osu!stable" : "Configure native osu! path", SelectOsuFolderAsync));
        left.Children.Add(selectButtons);

        left.Children.Add(Section("PROFILE"));
        profileBox.HorizontalAlignment = HorizontalAlignment.Stretch;
        profileBox.SelectionChanged += (_, _) => LoadSelectedProfile();
        left.Children.Add(profileBox);
        var profileButtons = Row();
        profileButtons.Children.Add(Button("Save profile", () => SaveProfileAsync(false)));
        profileButtons.Children.Add(Button("Duplicate", () => SaveProfileAsync(true)));
        profileButtons.Children.Add(Button("Delete custom", DeleteProfile));
        left.Children.Add(profileButtons);

        left.Children.Add(Section("RANGE"));
        wholeMap.IsCheckedChanged += (_, _) => UpdateRangeState();
        selectedRange.IsCheckedChanged += (_, _) => UpdateRangeState();
        left.Children.Add(Row(wholeMap, selectedRange));
        left.Children.Add(rangeBox);

        left.Children.Add(Section("SEED"));
        left.Children.Add(seedBox);
        left.Children.Add(Button("Generate random seed", () => seedBox.Text = SeededRandom.CreateSeed().ToString(CultureInfo.InvariantCulture)));

        left.Children.Add(Section("PLATFORM AND OUTPUT"));
        left.Children.Add(Labeled("tosu host", tosuHost));
        left.Children.Add(Labeled("tosu port", tosuPort));
        left.Children.Add(outputToBeatmapFolder);
        left.Children.Add(Button("Apply settings", ApplyPlatformSettings));
        randomizeButton.Click += async (_, _) => await RandomizeAsync();
        left.Children.Add(randomizeButton);
        left.Children.Add(Section("STATUS"));
        left.Children.Add(status);

        var parameters = new StackPanel { Spacing = 7, Margin = new Thickness(12) };
        parameters.Children.Add(Text("Active parameters", 18, FontWeight.SemiBold));
        parameters.Children.Add(dynamicThreshold);
        AddEditor(parameters, "MinThresholdMs", "Minimum threshold (ms)");
        AddEditor(parameters, "BaseThresholdMs", "Base threshold (ms)");
        AddEditor(parameters, "MaxThresholdMs", "Maximum threshold (ms)");
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
                SetBeatmap(path, "Manual beatmap selected");
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
            source = PlatformSourceFactory.Create(settings);
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

    private void SetBeatmap(string path, string state)
    {
        path = Path.GetFullPath(path);
        OsuBeatmapDocument document = OsuBeatmapDocument.Parse(path, File.ReadAllBytes(path));
        if (document.Mode != 3) throw new InvalidDataException("The selected file is not an osu!mania beatmap.");
        currentPath = path;
        beatmapTitle.Text = $"{document.Artist} - {document.Title}";
        beatmapDetails.Text = $"[{document.Version}]  ·  {document.Creator}  ·  {document.Keys}K";
        beatmapPath.Text = path;
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
                BeatmapSourceResult result = await source.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (result.Selection is not null && result.Selection.Beatmap.Identity != lastDetectedIdentity)
                    {
                        lastDetectedIdentity = result.Selection.Beatmap.Identity;
                        try { SetBeatmap(result.Selection.NativePath, result.Status); }
                        catch (Exception ex) { SetStatus(ex.Message); }
                    }
                    else if (currentPath is null) SetStatus(result.Status);
                    if (lastDetectionStatus != result.Status)
                    {
                        lastDetectionStatus = result.Status;
                        store.Log($"Detection status: {result.Status}");
                    }
                });
            }
        }
        catch (OperationCanceledException) { }
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
            randomizing = true;
            randomizeButton.IsEnabled = false;
            SetStatus("Randomizing...");
            string? outputDirectory = outputToBeatmapFolder.IsChecked == true ? null : AppPaths.OutputDirectory;
            GenerationResult result = await Task.Run(() => generator.Generate(snapshot, config, range, outputDirectory));
            seedBox.Text = result.Seed.ToString(CultureInfo.InvariantCulture);
            SetStatus($"Map generated: {result.OutputVersion}\nSeed: {result.Seed}\nOutput: {result.OutputPath}");
            store.Log($"Generated {result.OutputPath}; seed={result.Seed}");
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
        SaveSettings();
    }

    private async Task SaveProfileAsync(bool duplicate)
    {
        try { activeConfig = ReadConfig(); }
        catch (Exception ex) { ShowError(ex); return; }
        string initial = duplicate ? $"{profileBox.SelectedItem} Copy" : "My Profile";
        string? name = await PromptNameAsync(initial);
        if (string.IsNullOrWhiteSpace(name)) return;
        RandomProfile? profile = profiles.FirstOrDefault(p => !p.BuiltIn && p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (profile is null)
        {
            profile = new RandomProfile { Name = name.Trim(), BuiltIn = false };
            profiles.Add(profile);
        }
        profile.Config = activeConfig.Clone();
        settings.CustomProfiles = profiles.Where(p => !p.BuiltIn).ToList();
        SaveSettings();
        ReloadProfiles(profile.Name);
    }

    private void DeleteProfile()
    {
        if (profileBox.SelectedIndex < 0 || profiles[profileBox.SelectedIndex].BuiltIn) return;
        profiles.RemoveAt(profileBox.SelectedIndex);
        settings.CustomProfiles = profiles.Where(p => !p.BuiltIn).ToList();
        SaveSettings();
        ReloadProfiles("H-Random");
    }

    private async Task<string?> PromptNameAsync(string initial)
    {
        var input = new TextBox { Text = initial, Margin = new Thickness(0, 8) };
        var dialog = new Window { Title = "Profile name", Width = 420, Height = 160, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var save = new Button { Content = "Save", HorizontalAlignment = HorizontalAlignment.Right };
        var panel = new StackPanel { Margin = new Thickness(16), Spacing = 8 };
        panel.Children.Add(Text("Profile name"));
        panel.Children.Add(input);
        panel.Children.Add(save);
        dialog.Content = panel;
        save.Click += (_, _) => dialog.Close(input.Text);
        return await dialog.ShowDialog<string?>(this);
    }

    private void ApplyPlatformSettings()
    {
        try
        {
            settings.TosuHost = string.IsNullOrWhiteSpace(tosuHost.Text) ? "127.0.0.1" : tosuHost.Text.Trim();
            settings.TosuPort = int.Parse(tosuPort.Text ?? "24050", CultureInfo.InvariantCulture);
            if (settings.TosuPort is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(settings.TosuPort));
            settings.OutputToBeatmapFolder = outputToBeatmapFolder.IsChecked == true;
            source = PlatformSourceFactory.Create(settings);
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
        dynamicThreshold.IsChecked = config.DynamicThreshold;
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
        var config = new HRandomConfig
        {
            DynamicThreshold = dynamicThreshold.IsChecked == true,
            RenameDifficulty = renameDifficulty.IsChecked == true,
            MinThresholdMs = Int("MinThresholdMs"), BaseThresholdMs = Int("BaseThresholdMs"), MaxThresholdMs = Int("MaxThresholdMs"),
            RecentUsageWindow = Int("RecentUsageWindow"), PatternHistoryLength = Int("PatternHistoryLength"),
            WeightedTopCandidates = Int("WeightedTopCandidates"), WeightedTemperature = Double("WeightedTemperature"),
            MaxCandidateSets = Int("MaxCandidateSets"), DifficultySuffix = Value("DifficultySuffix"),
            Weights = new ScoringWeights
            {
                TimeSinceLastUseBonus = Double("TimeSinceLastUseBonus"), HandBalanceBonus = Double("HandBalanceBonus"),
                DistributionBonus = Double("DistributionBonus"), JackPenalty = Double("JackPenalty"),
                TrillPenalty = Double("TrillPenalty"), RepeatedPatternPenalty = Double("RepeatedPatternPenalty"),
                SameHandPenalty = Double("SameHandPenalty"), ExtremeJumpPenalty = Double("ExtremeJumpPenalty"),
                RecentUsagePenalty = Double("RecentUsagePenalty")
            }
        };
        config.Validate();
        activeConfig = config;
        return config.Clone();
    }

    private void AddEditor(Panel panel, string key, string label)
    {
        var editor = new TextBox();
        editors[key] = editor;
        panel.Children.Add(Labeled(label, editor));
    }

    private void Set(string key, object value) => editors[key].Text = Convert.ToString(value, CultureInfo.InvariantCulture);
    private string Value(string key) => editors[key].Text ?? string.Empty;
    private int Int(string key) => int.Parse(Value(key), CultureInfo.InvariantCulture);
    private double Double(string key) => double.Parse(Value(key), CultureInfo.InvariantCulture);
    private void SaveSettings() { try { store.Save(settings); } catch (Exception ex) { store.Log($"Could not save settings: {ex.Message}"); } }
    private void SetStatus(string message) => status.Text = message;
    private void ShowError(Exception ex) { SetStatus("Error: " + ex.Message); store.Log($"ERROR {ex}"); }

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
}
