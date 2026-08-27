using HRandomPlus.Beatmaps;
using HRandomPlus.Core;
using HRandomPlus.Osu;

namespace HRandomPlus.UI;

public sealed class MainWindow : Form
{
    private readonly SettingsStore store = new();
    private readonly AppSettings settings;
    private readonly OsuMemoryBeatmapSource memorySource;
    private readonly ManualBeatmapSource manualSource = new();
    private readonly BeatmapGenerationService generator = new();
    private readonly CancellationTokenSource pollingCancellation = new();
    private readonly ComboBox profileBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly PropertyGrid parameterGrid = new() { Dock = DockStyle.Fill, HelpVisible = true, ToolbarVisible = false };
    private readonly RadioButton wholeMap = new() { Text = "Whole map", AutoSize = true };
    private readonly RadioButton selectedRange = new() { Text = "Selected range", AutoSize = true };
    private readonly TextBox rangeBox = new() { Text = "00:37:005 - 01:13:005 -", Dock = DockStyle.Fill };
    private readonly TextBox seedBox = new() { PlaceholderText = "Random", Dock = DockStyle.Fill };
    private readonly Label beatmapTitle = Heading("No beatmap selected");
    private readonly Label beatmapDetails = Body("Select a .osu manually or open osu!stable.");
    private readonly Label beatmapPath = Body("");
    private readonly Label status = Body("Starting...");
    private readonly Button randomizeButton = new() { Text = "RANDOMIZE CURRENT MAP", Height = 46, Dock = DockStyle.Top, Enabled = false };
    private readonly List<RandomProfile> profiles = new();
    private HRandomConfig activeConfig = new();
    private string? currentPath;
    private string? lastMemoryKey;
    private bool randomizing;
    private string? lastDetectionStatus;

    public MainWindow()
    {
        settings = store.Load();
        memorySource = new OsuMemoryBeatmapSource(settings.OsuPath);
        profiles.AddRange(ProfileCatalog.BuiltIns);
        profiles.AddRange(settings.CustomProfiles);
        Text = "HRandomPlus";
        MinimumSize = new Size(900, 650);
        Size = new Size(1050, 760);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10);
        BuildUi();
        ReloadProfiles(settings.LastProfile);
        wholeMap.Checked = settings.WholeMap;
        selectedRange.Checked = !settings.WholeMap;
        UpdateRangeState();
        store.Log("Application started");
        _ = PollLoopAsync(pollingCancellation.Token);
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 1 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        Controls.Add(root);

        var left = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(0, 0, 12, 0) };
        left.SizeChanged += (_, _) => { foreach (Control c in left.Controls) c.Width = Math.Max(100, left.ClientSize.Width - 28); };
        root.Controls.Add(left, 0, 0);
        AddSection(left, "CURRENT BEATMAP", beatmapTitle, beatmapDetails, beatmapPath);

        var selectButtons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        var manualButton = new Button { Text = "Select .osu manually", AutoSize = true };
        var settingsButton = new Button { Text = "Configure osu!stable", AutoSize = true };
        manualButton.Click += (_, _) => SelectManual();
        settingsButton.Click += (_, _) => SelectOsuFolder();
        selectButtons.Controls.AddRange(new Control[] { manualButton, settingsButton });
        left.Controls.Add(selectButtons);

        AddSection(left, "PROFILE", profileBox);
        var profileButtons = new FlowLayoutPanel { AutoSize = true };
        var saveProfile = new Button { Text = "Save profile", AutoSize = true };
        var duplicate = new Button { Text = "Duplicate", AutoSize = true };
        var delete = new Button { Text = "Delete custom", AutoSize = true };
        saveProfile.Click += (_, _) => SaveProfile(false);
        duplicate.Click += (_, _) => SaveProfile(true);
        delete.Click += (_, _) => DeleteProfile();
        profileButtons.Controls.AddRange(new Control[] { saveProfile, duplicate, delete });
        left.Controls.Add(profileButtons);

        AddSection(left, "RANGE", wholeMap, selectedRange, rangeBox);
        wholeMap.CheckedChanged += (_, _) => UpdateRangeState();
        selectedRange.CheckedChanged += (_, _) => UpdateRangeState();
        AddSection(left, "SEED", seedBox);
        var randomSeed = new Button { Text = "Generate random seed", AutoSize = true };
        randomSeed.Click += (_, _) => seedBox.Text = SeededRandom.CreateSeed().ToString();
        left.Controls.Add(randomSeed);
        left.Controls.Add(randomizeButton);
        randomizeButton.Click += async (_, _) => await RandomizeAsync();
        AddSection(left, "STATUS", status);

        var rightGroup = new GroupBox { Text = "Active parameters", Dock = DockStyle.Fill, Padding = new Padding(10) };
        rightGroup.Controls.Add(parameterGrid);
        root.Controls.Add(rightGroup, 1, 0);
        profileBox.SelectedIndexChanged += (_, _) => LoadSelectedProfile();
    }

    private static Label Heading(string text) => new() { Text = text, AutoSize = true, Font = new Font("Segoe UI Semibold", 14) };
    private static Label Body(string text) => new() { Text = text, AutoSize = true, MaximumSize = new Size(680, 0) };
    private static void AddSection(FlowLayoutPanel panel, string title, params Control[] controls)
    {
        panel.Controls.Add(new Label { Text = title, AutoSize = true, Margin = new Padding(0, 18, 0, 4), Font = new Font("Segoe UI Semibold", 9), ForeColor = Color.DimGray });
        foreach (Control control in controls) panel.Controls.Add(control);
    }

    private void ReloadProfiles(string? select)
    {
        profileBox.Items.Clear();
        profileBox.Items.AddRange(profiles.Select(p => p.Name).Cast<object>().ToArray());
        int index = profiles.FindIndex(p => p.Name.Equals(select, StringComparison.OrdinalIgnoreCase));
        profileBox.SelectedIndex = index >= 0 ? index : 0;
    }

    private void LoadSelectedProfile()
    {
        if (profileBox.SelectedIndex < 0) return;
        activeConfig = profiles[profileBox.SelectedIndex].Config.Clone();
        parameterGrid.SelectedObject = activeConfig;
        settings.LastProfile = profiles[profileBox.SelectedIndex].Name;
        SaveSettings();
    }

    private void SelectManual()
    {
        try { SetBeatmap(manualSource.Select(this), "Manual beatmap selected"); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { ShowError(ex); }
    }

    private void SetBeatmap(BeatmapSelection selection, string state)
    {
        OsuBeatmapDocument document = OsuBeatmapDocument.Parse(selection.Path, File.ReadAllBytes(selection.Path));
        if (document.Mode != 3) throw new InvalidDataException("El archivo seleccionado no es osu!mania.");
        currentPath = Path.GetFullPath(selection.Path);
        beatmapTitle.Text = $"{document.Artist} - {document.Title}";
        beatmapDetails.Text = $"[{document.Version}]  ·  {document.Creator}  ·  {document.Keys}K";
        beatmapPath.Text = currentPath;
        status.Text = state;
        randomizeButton.Enabled = true;
        store.Log($"Beatmap selected: {currentPath}");
    }

    private async Task PollLoopAsync(CancellationToken cancellation)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(200));
            while (await timer.WaitForNextTickAsync(cancellation).ConfigureAwait(false))
            {
                if (randomizing) continue;
                var result = await Task.Run(() =>
                {
                    bool ok = memorySource.TryGetCurrent(out BeatmapSelection? selection, out string state);
                    return (ok, selection, state);
                }, cancellation).ConfigureAwait(false);
                if (IsDisposed) return;
                BeginInvoke(() =>
                {
                    if (result.ok && result.selection is not null)
                    {
                        string key = result.selection.FolderName + "\0" + result.selection.OsuFileName;
                        if (key != lastMemoryKey)
                        {
                            lastMemoryKey = key;
                            try { SetBeatmap(result.selection, result.state); } catch (Exception ex) { status.Text = ex.Message; }
                        }
                    }
                    else if (currentPath is null) status.Text = result.state;
                    if (!string.Equals(lastDetectionStatus, result.state, StringComparison.Ordinal))
                    {
                        lastDetectionStatus = result.state;
                        store.Log($"Detection status: {result.state}");
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
            HRandomConfig config = activeConfig.Clone();
            config.Seed = string.IsNullOrWhiteSpace(seedBox.Text) ? null : long.Parse(seedBox.Text);
            BeatmapRange? range = selectedRange.Checked ? BeatmapRange.Parse(rangeBox.Text) : null;
            randomizing = true;
            randomizeButton.Enabled = false;
            status.Text = "Randomizing...";
            store.Log($"Randomizing {snapshot}; profile={profileBox.Text}; seed={config.Seed?.ToString() ?? "random"}; range={range?.ToString() ?? "whole"}");
            GenerationResult result = await Task.Run(() => generator.Generate(snapshot, config, range));
            seedBox.Text = result.Seed.ToString();
            status.Text = $"Map generated: {result.OutputVersion}\nSeed: {result.Seed}\nOutput: {result.OutputPath}";
            store.Log($"Generated {result.OutputPath}; seed={result.Seed}");
        }
        catch (Exception ex) { ShowError(ex); }
        finally { randomizing = false; randomizeButton.Enabled = currentPath is not null; }
    }

    private void SaveProfile(bool duplicate)
    {
        string suggested = duplicate ? profileBox.Text + " Copy" : "My Profile";
        string? name = PromptName(suggested);
        if (string.IsNullOrWhiteSpace(name)) return;
        RandomProfile? existing = profiles.FirstOrDefault(p => !p.BuiltIn && p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            existing = new RandomProfile { Name = name.Trim(), BuiltIn = false };
            profiles.Add(existing);
        }
        existing.Config = activeConfig.Clone();
        settings.CustomProfiles = profiles.Where(p => !p.BuiltIn).ToList();
        SaveSettings();
        ReloadProfiles(existing.Name);
    }

    private void DeleteProfile()
    {
        if (profileBox.SelectedIndex < 0 || profiles[profileBox.SelectedIndex].BuiltIn) return;
        profiles.RemoveAt(profileBox.SelectedIndex);
        settings.CustomProfiles = profiles.Where(p => !p.BuiltIn).ToList();
        SaveSettings();
        ReloadProfiles("H-Random");
    }

    private void SelectOsuFolder()
    {
        try
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Selecciona osu!.exe de osu!stable",
                Filter = "osu!stable (osu!.exe)|osu!.exe",
                CheckFileExists = true,
                FileName = "osu!.exe",
                InitialDirectory = settings.OsuPath is not null && Directory.Exists(settings.OsuPath)
                    ? settings.OsuPath
                    : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                // The legacy dialog does not depend on the Explorer/OneDrive shell extensions
                // which can leave the application looking as if it is loading forever.
                AutoUpgradeEnabled = false
            };
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            string selectedDirectory = Path.GetDirectoryName(dialog.FileName)!;
            if (!Directory.Exists(Path.Combine(selectedDirectory, "Songs")))
            { MessageBox.Show(this, "La carpeta seleccionada no contiene Songs.", "Ruta inválida", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            settings.OsuPath = selectedDirectory;
            memorySource.SetOsuPath(settings.OsuPath);
            SaveSettings();
            status.Text = "osu!stable path saved";
        }
        catch (Exception ex)
        {
            ShowError(new InvalidOperationException("No se pudo abrir la configuración de osu!stable.", ex));
        }
    }

    private void UpdateRangeState()
    {
        rangeBox.Enabled = selectedRange.Checked;
        settings.WholeMap = wholeMap.Checked;
        SaveSettings();
    }

    private void SaveSettings() { try { store.Save(settings); } catch (Exception ex) { store.Log($"Could not save settings: {ex.Message}"); } }
    private void ShowError(Exception ex) { status.Text = "Error: " + ex.Message; store.Log($"ERROR {ex}"); MessageBox.Show(this, ex.Message, "HRandomPlus", MessageBoxButtons.OK, MessageBoxIcon.Error); }

    private string? PromptName(string initial)
    {
        using var dialog = new Form { Text = "Profile name", Width = 400, Height = 150, StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false };
        var input = new TextBox { Text = initial, Dock = DockStyle.Top, Margin = new Padding(12) };
        var ok = new Button { Text = "Save", DialogResult = DialogResult.OK, Dock = DockStyle.Bottom };
        dialog.Controls.Add(input); dialog.Controls.Add(ok); dialog.AcceptButton = ok;
        return dialog.ShowDialog(this) == DialogResult.OK ? input.Text : null;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        pollingCancellation.Cancel();
        memorySource.Dispose();
        SaveSettings();
        base.OnFormClosed(e);
    }
}
