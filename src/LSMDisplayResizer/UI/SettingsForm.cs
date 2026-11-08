using LSMDisplayResizer.Monitors;
using LSMDisplayResizer.Settings;
using LSMDisplayResizer.Wallpaper;

namespace LSMDisplayResizer.UI;

/// <summary>Einstellungsfenster. Arbeitet auf einer Kopie und übergibt sie beim Speichern.</summary>
internal sealed class SettingsForm : Form
{
    private readonly AppSettings _settings;
    private readonly MonitorManager _monitors;
    private readonly Func<AppSettings, string?> _apply;

    // Allgemein
    private readonly CheckBox _chkEnabled;
    private readonly RadioButton _radPercent;
    private readonly RadioButton _radRules;
    private readonly CheckBox _chkFallback;
    private readonly CheckBox _chkKeepDrop;
    private readonly CheckBox _chkFullHeight;
    private readonly CheckBox _chkAfterDisplayChange;
    private readonly NumericUpDown _numDisplayDelay;
    private readonly CheckBox _chkHotkey;
    private readonly TextBox _txtHotkey;
    private readonly CheckBox _chkAutostart;
    private readonly CheckBox _chkVerbose;

    // Regeln
    private readonly ListView _rules;
    private readonly Label _lblRulesModeHint;
    private readonly Button _btnEdit;
    private readonly Button _btnDelete;

    // Ausschlussliste
    private readonly ListBox _excluded;
    private readonly ComboBox _cmbExclude;

    // Hintergrundbild
    private readonly WallpaperPage _wallpaperPage;
    private readonly WallpaperService _wallpaper;

    private Keys _hotkey;

    public SettingsForm(AppSettings settings, MonitorManager monitors, WallpaperService wallpaper,
        Func<AppSettings, string?> apply, Action showLog)
    {
        _settings = settings;
        _wallpaper = wallpaper;
        _monitors = monitors;
        _apply = apply;
        _hotkey = settings.Hotkey;

        SuspendLayout();
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        Text = $"{AppInfo.Name} – Einstellungen";
        Icon = AppIcons.LoadForm();
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(800, 680);
        MinimumSize = new Size(600, 480);
        Padding = new Padding(10);

        var tabs = new TabControl { Dock = DockStyle.Fill };

        // ---------------- Allgemein ----------------
        var general = Ui.Table(1);
        general.AutoScroll = true;
        general.Padding = new Padding(8);
        general.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _chkEnabled = Ui.Check("Automatische Anpassung aktiv");
        _chkEnabled.Font = new Font(_chkEnabled.Font, FontStyle.Bold);
        Ui.AddRow(general, _chkEnabled);
        Ui.AddRow(general, Ui.Text("Wird ein Fenster auf einen anderen Monitor verschoben, passt die App seine Größe an. " +
                                   "Maximierte, minimierte und Vollbild-Fenster bleiben unverändert.", hint: true, indent: 20));

        Ui.AddRow(general, Ui.Heading("Modus"));
        _radPercent = Ui.Radio("Prozentual");
        Ui.AddRow(general, _radPercent);
        Ui.AddRow(general, Ui.Text("Größe und Position relativ zum Arbeitsbereich (ohne Taskleiste) übertragen – " +
                                   "z. B. 60 % Breite auf Monitor A → 60 % Breite auf Monitor B.", hint: true, indent: 20));
        _radRules = Ui.Radio("Feste Regeln");
        Ui.AddRow(general, _radRules);
        Ui.AddRow(general, Ui.Text("Zielgröße pro Programm und Monitor, siehe Registerkarte „Feste Regeln“.", hint: true, indent: 20));
        _chkFallback = Ui.Check("Fenster ohne passende Regel prozentual anpassen", indent: 20);
        Ui.AddRow(general, _chkFallback);

        Ui.AddRow(general, Ui.Heading("Verhalten"));
        _chkKeepDrop = Ui.Check("Beim Ziehen mit der Maus die Ablageposition beibehalten");
        Ui.AddRow(general, _chkKeepDrop);
        Ui.AddRow(general, Ui.Text("Nur die Größe ändert sich, die linke obere Ecke bleibt, wo das Fenster losgelassen wurde. " +
                                   "Bei Win+Umschalt+Pfeil wird die Position immer relativ übertragen.", hint: true, indent: 20));

        _chkFullHeight = Ui.Check("Fenster mit voller Bildschirmhöhe wie angedockte behandeln");
        Ui.AddRow(general, _chkFullHeight);
        Ui.AddRow(general, Ui.Text("Windows zieht angedockte Fenster (z. B. linke Bildschirmhälfte) oft nur auf volle Höhe, statt sie zu " +
                                   "maximieren. Solche Fenster werden dann wie maximierte behandelt: Ihre Größe wird nicht gemerkt, es gilt " +
                                   "weiter die letzte normale Größe.", hint: true, indent: 20));

        var dockRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        _chkAfterDisplayChange = Ui.Check("Nach An-/Abdocken und Monitorwechseln Fenster anpassen, höchstens warten:");
        _chkAfterDisplayChange.Margin = new Padding(3, 6, 6, 3);
        _numDisplayDelay = new NumericUpDown { Width = 60, Minimum = 2, Maximum = 60, TextAlign = HorizontalAlignment.Right, Margin = new Padding(3, 3, 3, 3) };
        dockRow.Controls.Add(_chkAfterDisplayChange);
        dockRow.Controls.Add(_numDisplayDelay);
        dockRow.Controls.Add(new Label { Text = "Sekunden", AutoSize = true, Margin = new Padding(3, 6, 3, 3) });
        Ui.AddRow(general, dockRow);
        Ui.AddRow(general, Ui.Text("Wird z. B. der Laptop von der Dockingstation abgezogen, räumt Windows die Fenster auf die verbliebenen " +
                                   "Monitore um. Sobald ein Fenster nicht mehr bewegt wird, passt die App es ausgehend von seinem Zustand " +
                                   "vorher an, sodass Position und Größenverhältnis wieder stimmen – meist nach 2–3 Sekunden. Die Höchstdauer " +
                                   "greift nur, wenn Windows nicht zur Ruhe kommt.",
            hint: true, indent: 20));

        var hotkeyRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        _chkHotkey = Ui.Check("Tastenkürzel für das aktive Fenster:");
        _chkHotkey.Margin = new Padding(3, 6, 6, 3);
        _txtHotkey = new TextBox { Width = 200, ReadOnly = true, BackColor = SystemColors.Window, Cursor = Cursors.Hand };
        _txtHotkey.Margin = new Padding(3, 3, 3, 3);
        hotkeyRow.Controls.Add(_chkHotkey);
        hotkeyRow.Controls.Add(_txtHotkey);
        Ui.AddRow(general, hotkeyRow);
        Ui.AddRow(general, Ui.Text("In das Feld klicken und die Tastenkombination drücken (z. B. Strg + Alt + R). " +
                                   "Wendet eine passende feste Regel an, sonst wird das Fenster in den Arbeitsbereich eingepasst.",
            hint: true, indent: 20));

        Ui.AddRow(general, Ui.Heading("System"));
        _chkAutostart = Ui.Check("Mit Windows starten");
        Ui.AddRow(general, _chkAutostart);
        _chkVerbose = Ui.Check("Ausführliches Log (auch ignorierte Fenster protokollieren)");
        Ui.AddRow(general, _chkVerbose);

        var adminHint = Ui.Text(
            "Hinweis: Fenster von Programmen, die mit Administratorrechten laufen, kann die App nur anpassen, " +
            "wenn sie selbst als Administrator gestartet wurde (Rechtsklick auf die EXE → „Als Administrator ausführen“). " +
            (AppInfo.IsElevated ? "Aktuell: läuft als Administrator." : "Aktuell: läuft ohne Administratorrechte."),
            hint: true);
        adminHint.Margin = new Padding(3, 16, 3, 3);
        Ui.AddRow(general, adminHint);

        tabs.TabPages.Add(MakeTab("Allgemein", general));

        // ---------------- Feste Regeln ----------------
        var rulesTable = Ui.Table(2);
        rulesTable.Padding = new Padding(8);
        rulesTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        rulesTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        Ui.AddRow(rulesTable, Ui.Text("Breite und Höhe beziehen sich auf den sichtbaren Fensterrahmen; % = Anteil am Arbeitsbereich " +
                                      "des Monitors. Passen mehrere Regeln, gewinnt die genaueste (bestimmtes Programm und Monitor vor „*“ " +
                                      "bzw. „Alle Monitore“)."), 2);
        _lblRulesModeHint = Ui.Text(string.Empty);
        _lblRulesModeHint.ForeColor = Color.DarkGoldenrod;
        Ui.AddRow(rulesTable, _lblRulesModeHint, 2);

        _rules = Ui.DetailsList();
        Ui.Column(_rules, "Programm", 130);
        Ui.Column(_rules, "Monitor", 195);
        Ui.Column(_rules, "Breite", 60, HorizontalAlignment.Right);
        Ui.Column(_rules, "Höhe", 60, HorizontalAlignment.Right);
        Ui.Column(_rules, "Einheit", 55);
        _rules.SelectedIndexChanged += (_, _) => UpdateRuleButtons();
        _rules.DoubleClick += (_, _) => EditSelectedRule();
        _rules.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete) DeleteSelectedRule();
        };
        Ui.AddRow(rulesTable, _rules, sizeType: SizeType.Percent, height: 100);

        var ruleButtons = Ui.Table(1);
        ruleButtons.AutoSize = true;
        ruleButtons.Dock = DockStyle.Top;
        ruleButtons.Margin = new Padding(6, 0, 0, 0);
        _btnEdit = Ui.Button("Bearbeiten …", (_, _) => EditSelectedRule());
        _btnDelete = Ui.Button("Löschen", (_, _) => DeleteSelectedRule());
        foreach (var b in new[]
                 {
                     Ui.Button("Hinzufügen …", (_, _) => AddRule()),
                     _btnEdit,
                     _btnDelete,
                     Ui.Button("Aktuelles Fenster übernehmen …", (_, _) => AddRuleFromWindow()),
                 })
        {
            b.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            Ui.AddRow(ruleButtons, b);
        }
        rulesTable.Controls.Add(ruleButtons, 1, rulesTable.RowCount - 1);

        tabs.TabPages.Add(MakeTab("Feste Regeln", rulesTable));

        // ---------------- Ausschlussliste ----------------
        var exclTable = Ui.Table(3);
        exclTable.Padding = new Padding(8);
        exclTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        exclTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        exclTable.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        Ui.AddRow(exclTable, Ui.Text("Fenster dieser Programme werden nie angepasst (Prozessname, z. B. game.exe). " +
                                     "Die Liste schlägt Programme vor, die gerade ein Fenster geöffnet haben."), 3);

        _excluded = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false, Sorted = true };
        _excluded.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete) RemoveExcluded();
        };
        Ui.AddRow(exclTable, _excluded, 3, SizeType.Percent, 100);

        _cmbExclude = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Dock = DockStyle.Fill, Margin = new Padding(3, 5, 3, 3) };
        _cmbExclude.Items.AddRange(Ui.RunningProcessNames());
        _cmbExclude.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                AddExcluded();
                e.SuppressKeyPress = true;
            }
        };
        Ui.AddRow(exclTable, _cmbExclude);
        exclTable.Controls.Add(Ui.Button("Hinzufügen", (_, _) => AddExcluded()), 1, exclTable.RowCount - 1);
        exclTable.Controls.Add(Ui.Button("Entfernen", (_, _) => RemoveExcluded()), 2, exclTable.RowCount - 1);

        tabs.TabPages.Add(MakeTab("Ausschlussliste", exclTable));

        // ---------------- Hintergrundbild ----------------
        _wallpaperPage = new WallpaperPage(settings.Wallpaper, monitors, ApplyWallpaperAsync, RestoreWallpaperAsync);
        tabs.TabPages.Add(MakeTab("Hintergrundbild", _wallpaperPage));

        // ---------------- Fußzeile ----------------
        var root = Ui.Table(1);
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        Ui.AddRow(root, tabs, sizeType: SizeType.Percent, height: 100);

        var footer = Ui.Table(2);
        footer.AutoSize = true;
        footer.Margin = new Padding(0, 8, 0, 0);
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var version = Ui.Text($"Version {AppInfo.Version}", hint: true);
        version.Anchor = AnchorStyles.Left;
        var leftButtons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty, Anchor = AnchorStyles.Left };
        leftButtons.Controls.Add(Ui.Button("Log anzeigen …", (_, _) => showLog()));
        leftButtons.Controls.Add(version);
        var projectLink = new LinkLabel
        {
            Text = "MIT-Lizenz · GitHub",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 4, 3, 4),
        };
        projectLink.LinkClicked += (_, _) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(AppInfo.ProjectUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Logging.Log.Error("Projektseite konnte nicht geöffnet werden", ex);
            }
        };
        leftButtons.Controls.Add(projectLink);
        footer.Controls.Add(leftButtons, 0, 0);
        var save = Ui.Button("Speichern", (_, _) => Save());
        var cancel = Ui.Button("Abbrechen", (_, _) => Close());
        footer.Controls.Add(Ui.ButtonRow(cancel, save), 1, 0);
        Ui.AddRow(root, footer);

        CancelButton = cancel;
        Controls.Add(root);
        ResumeLayout(true);

        // Hotkey-Erfassung
        _txtHotkey.PreviewKeyDown += (_, e) => e.IsInputKey = e.KeyCode is not (Keys.Tab or Keys.Escape);
        _txtHotkey.KeyDown += OnHotkeyKeyDown;
        _txtHotkey.KeyUp += (_, _) => ShowHotkey();
        _txtHotkey.Leave += (_, _) => ShowHotkey();

        _radPercent.CheckedChanged += (_, _) => UpdateModeDependentControls();
        _radRules.CheckedChanged += (_, _) => UpdateModeDependentControls();
        _chkHotkey.CheckedChanged += (_, _) => _txtHotkey.Enabled = _chkHotkey.Checked;

        LoadValues();
        Load += (_, _) => Ui.ScaleColumns(_rules);
    }

    private static TabPage MakeTab(string title, Control content)
    {
        var page = new TabPage(title) { UseVisualStyleBackColor = true, Padding = Padding.Empty };
        page.Controls.Add(content);
        return page;
    }

    private void LoadValues()
    {
        _chkEnabled.Checked = _settings.Enabled;
        _radPercent.Checked = _settings.Mode == ResizeMode.Percent;
        _radRules.Checked = _settings.Mode == ResizeMode.FixedRules;
        _chkFallback.Checked = _settings.PercentFallbackWithoutRule;
        _chkKeepDrop.Checked = _settings.KeepDropPositionOnDrag;
        _chkFullHeight.Checked = _settings.TreatFullHeightAsSnapped;
        _chkAfterDisplayChange.Checked = _settings.AdjustAfterDisplayChange;
        _numDisplayDelay.Value = Math.Clamp(_settings.DisplayChangeDelaySeconds, 2, 60);
        _numDisplayDelay.Enabled = _chkAfterDisplayChange.Checked;
        _chkAfterDisplayChange.CheckedChanged += (_, _) => _numDisplayDelay.Enabled = _chkAfterDisplayChange.Checked;
        _chkHotkey.Checked = _settings.HotkeyEnabled;
        _txtHotkey.Enabled = _settings.HotkeyEnabled;
        ShowHotkey();
        if (Autostart.IsEnabledForAllUsers())
        {
            // Von der Installation pro Rechner eingerichtet (HKLM) – hier nicht änderbar.
            _chkAutostart.Checked = true;
            _chkAutostart.Enabled = false;
            _chkAutostart.Text = "Mit Windows starten (von der Installation für alle Benutzer eingerichtet)";
        }
        else
        {
            _chkAutostart.Checked = Autostart.IsEnabled();
        }
        _chkVerbose.Checked = _settings.VerboseLog;

        FillRules();
        foreach (var p in _settings.ExcludedProcesses) _excluded.Items.Add(p);
        UpdateModeDependentControls();
    }

    private void UpdateModeDependentControls()
    {
        _chkFallback.Enabled = _radRules.Checked;
        _lblRulesModeHint.Text = _radRules.Checked
            ? string.Empty
            : "Modus ist „Prozentual“: Die Regeln werden derzeit nur für das Tastenkürzel verwendet.";
        _lblRulesModeHint.Visible = !_radRules.Checked;
    }

    // ---------------- Tastenkürzel ----------------

    private void OnHotkeyKeyDown(object? sender, KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        e.Handled = true;

        if (e.KeyCode is Keys.Back or Keys.Delete && e.Modifiers == Keys.None)
        {
            _hotkey = Keys.None;
            ShowHotkey();
            return;
        }

        if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LWin or Keys.RWin)
        {
            _txtHotkey.Text = HotkeyManager.Format(e.Modifiers) + " + …";
            return;
        }

        Keys combo = e.KeyCode | e.Modifiers;
        if (!HotkeyManager.IsValid(combo))
        {
            _txtHotkey.Text = "Mit Strg, Alt oder Umschalt kombinieren";
            return;
        }

        _hotkey = combo;
        ShowHotkey();
    }

    private void ShowHotkey() => _txtHotkey.Text = HotkeyManager.IsValid(_hotkey) ? HotkeyManager.Format(_hotkey) : "(keins)";

    // ---------------- Regeln ----------------

    private void FillRules()
    {
        var monitors = _monitors.GetAll();
        _rules.BeginUpdate();
        try
        {
            _rules.Items.Clear();
            foreach (var rule in _settings.Rules)
            {
                bool connected = rule.IsAnyMonitor || monitors.Any(m =>
                    string.Equals(m.DeviceId, rule.MonitorId, StringComparison.OrdinalIgnoreCase) ||
                    (m.ModelKey.Length > 0 && m.ModelKey == MonitorDescriptor.ExtractModelKey(rule.MonitorId)));

                var item = new ListViewItem([
                    rule.ProcessName,
                    rule.MonitorText + (connected ? string.Empty : " (nicht verbunden)"),
                    rule.Width.ToString(rule.Unit == SizeUnit.Percent ? "0.#" : "0"),
                    rule.Height.ToString(rule.Unit == SizeUnit.Percent ? "0.#" : "0"),
                    rule.UnitText,
                ])
                {
                    Tag = rule,
                };
                if (!connected) item.ForeColor = SystemColors.GrayText;
                _rules.Items.Add(item);
            }
        }
        finally
        {
            _rules.EndUpdate();
        }
        UpdateRuleButtons();
    }

    private FixedRule? SelectedRule => _rules.SelectedItems.Count == 1 ? _rules.SelectedItems[0].Tag as FixedRule : null;

    private void UpdateRuleButtons()
    {
        _btnEdit.Enabled = SelectedRule != null;
        _btnDelete.Enabled = SelectedRule != null;
    }

    private void AddRule()
    {
        var monitors = _monitors.GetAll();
        var template = new FixedRule { ProcessName = string.Empty, MonitorId = FixedRule.Any, Width = 60, Height = 70 };
        using var dlg = new RuleEditDialog(template, monitors, "Neue Regel");
        if (dlg.ShowDialog(this) == DialogResult.OK) InsertRule(dlg.GetRule(), replace: null);
    }

    private void AddRuleFromWindow()
    {
        using var picker = new WindowPickerDialog(_monitors);
        if (picker.ShowDialog(this) != DialogResult.OK || picker.GetSelection() is not { } sel) return;

        var work = sel.Monitor.WorkArea;
        var rule = new FixedRule
        {
            ProcessName = sel.Window.ProcessName,
            MonitorId = sel.Monitor.DeviceId,
            MonitorName = sel.Monitor.DisplayName,
            Unit = SizeUnit.Percent,
            Width = Math.Round(100.0 * sel.Window.FrameRect.Width / work.Width, 1),
            Height = Math.Round(100.0 * sel.Window.FrameRect.Height / work.Height, 1),
        };
        rule.Normalize();

        using var dlg = new RuleEditDialog(rule, _monitors.GetAll(), "Neue Regel aus Fenster");
        if (dlg.ShowDialog(this) == DialogResult.OK) InsertRule(dlg.GetRule(), replace: null);
    }

    private void EditSelectedRule()
    {
        if (SelectedRule is not { } rule) return;
        using var dlg = new RuleEditDialog(rule, _monitors.GetAll(), "Regel bearbeiten");
        if (dlg.ShowDialog(this) == DialogResult.OK) InsertRule(dlg.GetRule(), replace: rule);
    }

    private void InsertRule(FixedRule rule, FixedRule? replace)
    {
        var duplicate = _settings.Rules.FirstOrDefault(r => !ReferenceEquals(r, replace) &&
            string.Equals(r.ProcessName, rule.ProcessName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(r.MonitorId, rule.MonitorId, StringComparison.OrdinalIgnoreCase));

        if (duplicate != null)
        {
            var answer = MessageBox.Show(this,
                $"Für {rule.ProcessName} auf „{rule.MonitorText}“ gibt es schon eine Regel. Ersetzen?",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return;
            _settings.Rules.Remove(duplicate);
        }

        int index = replace != null ? _settings.Rules.IndexOf(replace) : -1;
        if (index >= 0)
            _settings.Rules[index] = rule;
        else
            _settings.Rules.Add(rule);

        FillRules();
        foreach (ListViewItem item in _rules.Items)
        {
            if (ReferenceEquals(item.Tag, rule))
            {
                item.Selected = true;
                item.EnsureVisible();
            }
        }
    }

    private void DeleteSelectedRule()
    {
        if (SelectedRule is not { } rule) return;
        var answer = MessageBox.Show(this, $"Regel für {rule.ProcessName} auf „{rule.MonitorText}“ löschen?",
            Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (answer != DialogResult.Yes) return;
        _settings.Rules.Remove(rule);
        FillRules();
    }

    // ---------------- Ausschlussliste ----------------

    private void AddExcluded()
    {
        string name = AppSettings.NormalizeProcessName(_cmbExclude.Text);
        if (name.Length == 0 || name == FixedRule.Any) return;
        if (!_excluded.Items.Cast<string>().Contains(name, StringComparer.OrdinalIgnoreCase))
            _excluded.Items.Add(name);
        _cmbExclude.Text = string.Empty;
    }

    private void RemoveExcluded()
    {
        if (_excluded.SelectedItem != null) _excluded.Items.Remove(_excluded.SelectedItem);
    }

    // ---------------- Speichern ----------------

    private void Save()
    {
        if (SaveSettings()) Close();
    }

    /// <summary>Übernimmt und speichert alle Eingaben. Rückgabe: true ohne Probleme.</summary>
    private bool SaveSettings()
    {
        if (_chkHotkey.Checked && !HotkeyManager.IsValid(_hotkey))
        {
            MessageBox.Show(this, "Bitte eine Tastenkombination festlegen oder das Tastenkürzel ausschalten.", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtHotkey.Focus();
            return false;
        }

        // Noch nicht hinzugefügten Eintrag im Ausschlussfeld mitnehmen.
        if (!string.IsNullOrWhiteSpace(_cmbExclude.Text)) AddExcluded();

        _settings.Enabled = _chkEnabled.Checked;
        _settings.Mode = _radRules.Checked ? ResizeMode.FixedRules : ResizeMode.Percent;
        _settings.PercentFallbackWithoutRule = _chkFallback.Checked;
        _settings.KeepDropPositionOnDrag = _chkKeepDrop.Checked;
        _settings.TreatFullHeightAsSnapped = _chkFullHeight.Checked;
        _settings.AdjustAfterDisplayChange = _chkAfterDisplayChange.Checked;
        _settings.DisplayChangeDelaySeconds = (int)_numDisplayDelay.Value;
        _settings.HotkeyEnabled = _chkHotkey.Checked;
        if (HotkeyManager.IsValid(_hotkey)) _settings.Hotkey = _hotkey;
        _settings.VerboseLog = _chkVerbose.Checked;
        _settings.ExcludedProcesses = _excluded.Items.Cast<string>().ToList();
        _wallpaperPage.SaveTo(_settings.Wallpaper);

        var problems = new List<string>();
        if (_chkAutostart.Enabled && !Autostart.SetEnabled(_chkAutostart.Checked))
            problems.Add("Der Autostart-Eintrag konnte nicht geändert werden (Details im Log).");

        string? applyError = _apply(_settings.Clone());
        if (applyError != null) problems.Add(applyError);

        if (problems.Count > 0)
        {
            MessageBox.Show(this, string.Join(Environment.NewLine + Environment.NewLine, problems), Text,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false; // Fenster offen lassen, damit man korrigieren kann – gespeichert ist trotzdem.
        }
        return true;
    }

    // ---------------- Hintergrundbild ----------------

    private async Task ApplyWallpaperAsync()
    {
        _wallpaperPage.SaveTo(_settings.Wallpaper);
        if (!_settings.Wallpaper.IsConfigured || !File.Exists(_settings.Wallpaper.SourceImage))
        {
            MessageBox.Show(this, "Bitte zuerst eine vorhandene Bilddatei auswählen.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        SaveSettings();

        _wallpaperPage.SetStatus("Hintergrundbild wird erstellt …");
        try
        {
            _wallpaperPage.SetStatus(await _wallpaper.ApplyNowAsync(_settings.Wallpaper.Clone()));
        }
        catch (Exception ex)
        {
            Logging.Log.Error("Hintergrundbild konnte nicht gesetzt werden", ex);
            _wallpaperPage.SetStatus($"Hintergrundbild konnte nicht gesetzt werden: {ex.Message}");
        }
    }

    private Task RestoreWallpaperAsync()
    {
        try
        {
            // Automatik aus, sonst würde das gespannte Bild bei der nächsten Anzeigeänderung wieder gesetzt.
            _wallpaperPage.SetAutoUpdate(false);
            _wallpaperPage.SaveTo(_settings.Wallpaper);
            SaveSettings();

            bool restored = WallpaperSetter.RestoreBackup();
            WallpaperService.ForgetLastApplied();
            _wallpaperPage.SetStatus(restored
                ? "Vorheriges Hintergrundbild wiederhergestellt. Die automatische Aktualisierung wurde ausgeschaltet."
                : "Es ist kein gesichertes Hintergrundbild vorhanden.");
        }
        catch (Exception ex)
        {
            Logging.Log.Error("Hintergrundbild konnte nicht wiederhergestellt werden", ex);
            _wallpaperPage.SetStatus($"Wiederherstellen fehlgeschlagen: {ex.Message}");
        }
        return Task.CompletedTask;
    }
}
