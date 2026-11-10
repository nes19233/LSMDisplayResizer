using LSMDisplayResizer.Localization;
using LSMDisplayResizer.Monitors;
using LSMDisplayResizer.Resizing;
using LSMDisplayResizer.Settings;

namespace LSMDisplayResizer.UI;

/// <summary>Dialog zum Anlegen/Bearbeiten einer festen Regel.</summary>
internal sealed class RuleEditDialog : Form
{
    private sealed record MonitorItem(string Id, string Name, MonitorDescriptor? Monitor)
    {
        public override string ToString() => Name;
    }

    private readonly ComboBox _cmbProcess;
    private readonly ComboBox _cmbMonitor;
    private readonly NumericUpDown _numWidth;
    private readonly NumericUpDown _numHeight;
    private readonly ComboBox _cmbUnit;
    private readonly Label _lblPreview;
    private SizeUnit _currentUnit;

    public RuleEditDialog(FixedRule rule, IReadOnlyList<MonitorDescriptor> monitors, string title)
    {
        SuspendLayout();
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        Text = title;
        Icon = AppIcons.LoadForm();
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        var table = Ui.Table(2);
        table.AutoSize = true;
        table.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        table.Dock = DockStyle.None;
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _cmbProcess = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Width = 320, Anchor = AnchorStyles.Left };
        _cmbProcess.Items.Add(FixedRule.Any);
        _cmbProcess.Items.AddRange(Ui.RunningProcessNames());
        _cmbProcess.Text = rule.ProcessName;

        _cmbMonitor = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 320, Anchor = AnchorStyles.Left };
        _cmbMonitor.Items.Add(new MonitorItem(FixedRule.Any, L.T("Alle Monitore"), null));
        foreach (var m in monitors) _cmbMonitor.Items.Add(new MonitorItem(m.DeviceId, m.DisplayName, m));
        SelectMonitor(rule);

        _numWidth = new NumericUpDown { Width = 100, Anchor = AnchorStyles.Left, Maximum = 20000, TextAlign = HorizontalAlignment.Right };
        _numHeight = new NumericUpDown { Width = 100, Anchor = AnchorStyles.Left, Maximum = 20000, TextAlign = HorizontalAlignment.Right };
        _cmbUnit = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140, Anchor = AnchorStyles.Left };
        _cmbUnit.Items.AddRange(["Prozent (%)", "Pixel (px)"]);

        _currentUnit = rule.Unit;
        _cmbUnit.SelectedIndex = rule.Unit == SizeUnit.Percent ? 0 : 1;
        ApplyUnitLimits(rule.Unit);
        _numWidth.Value = Clamp(_numWidth, (decimal)rule.Width);
        _numHeight.Value = Clamp(_numHeight, (decimal)rule.Height);

        _lblPreview = Ui.Text(string.Empty, hint: true);
        _lblPreview.Tag = L.NoTranslate;
        _lblPreview.Anchor = AnchorStyles.Left;

        AddField(table, "Programm:", _cmbProcess);
        AddHint(table, "Prozessname, z. B. chrome.exe – oder * für alle Programme.");
        AddField(table, "Monitor:", _cmbMonitor);
        AddField(table, "Breite:", _numWidth);
        AddField(table, "Höhe:", _numHeight);
        AddField(table, "Einheit:", _cmbUnit);
        AddHint(table, "Bezieht sich auf den sichtbaren Fensterrahmen. % = Anteil am Arbeitsbereich (ohne Taskleiste).");
        Ui.AddRow(table, new Label(), 1);
        table.Controls.Add(_lblPreview, 1, table.RowCount - 1);

        var ok = Ui.Button("OK", OnOk);
        var cancel = Ui.Button("Abbrechen", (_, _) => { DialogResult = DialogResult.Cancel; Close(); });
        var buttons = Ui.ButtonRow(cancel, ok);
        buttons.Margin = new Padding(0, 12, 0, 0);
        Ui.AddRow(table, buttons, 2);
        AcceptButton = ok;
        CancelButton = cancel;

        Controls.Add(table);

        _cmbUnit.SelectedIndexChanged += (_, _) => OnUnitChanged();
        _cmbMonitor.SelectedIndexChanged += (_, _) => UpdatePreview();
        _numWidth.ValueChanged += (_, _) => UpdatePreview();
        _numHeight.ValueChanged += (_, _) => UpdatePreview();
        UpdatePreview();

        ResumeLayout(true);
        L.Translate(this);
    }

    /// <summary>Die bearbeitete Regel (nur nach DialogResult.OK sinnvoll).</summary>
    public FixedRule GetRule()
    {
        var item = (MonitorItem)_cmbMonitor.SelectedItem!;
        var rule = new FixedRule
        {
            ProcessName = AppSettings.NormalizeProcessName(_cmbProcess.Text),
            MonitorId = item.Id,
            MonitorName = item.Id == FixedRule.Any ? string.Empty : item.Name.Replace(L.T(" (nicht verbunden)"), string.Empty),
            Width = (double)_numWidth.Value,
            Height = (double)_numHeight.Value,
            Unit = _currentUnit,
        };
        rule.Normalize();
        return rule;
    }

    private void SelectMonitor(FixedRule rule)
    {
        if (rule.IsAnyMonitor)
        {
            _cmbMonitor.SelectedIndex = 0;
            return;
        }

        for (int i = 0; i < _cmbMonitor.Items.Count; i++)
        {
            if (string.Equals(((MonitorItem)_cmbMonitor.Items[i]!).Id, rule.MonitorId, StringComparison.OrdinalIgnoreCase))
            {
                _cmbMonitor.SelectedIndex = i;
                return;
            }
        }

        // Monitor der Regel ist gerade nicht angeschlossen → trotzdem auswählbar lassen.
        string name = string.IsNullOrEmpty(rule.MonitorName) ? rule.MonitorId : rule.MonitorName;
        _cmbMonitor.SelectedIndex = _cmbMonitor.Items.Add(new MonitorItem(rule.MonitorId, name + L.T(" (nicht verbunden)"), null));
    }

    private void OnUnitChanged()
    {
        var newUnit = _cmbUnit.SelectedIndex == 0 ? SizeUnit.Percent : SizeUnit.Pixels;
        if (newUnit == _currentUnit) return;

        decimal w = _numWidth.Value, h = _numHeight.Value;
        var work = SelectedWorkArea();
        if (work is { } wa)
        {
            if (newUnit == SizeUnit.Pixels)
            {
                w = Math.Round(w / 100m * wa.Width);
                h = Math.Round(h / 100m * wa.Height);
            }
            else
            {
                w = Math.Round(w / wa.Width * 100m, 1);
                h = Math.Round(h / wa.Height * 100m, 1);
            }
        }

        _currentUnit = newUnit;
        ApplyUnitLimits(newUnit);
        _numWidth.Value = Clamp(_numWidth, w);
        _numHeight.Value = Clamp(_numHeight, h);
        UpdatePreview();
    }

    private void ApplyUnitLimits(SizeUnit unit)
    {
        foreach (var num in new[] { _numWidth, _numHeight })
        {
            num.Maximum = 20000;
            num.Minimum = 0;
            if (unit == SizeUnit.Percent)
            {
                num.DecimalPlaces = 1;
                num.Increment = 1;
                num.Minimum = 5;
                num.Maximum = 100;
            }
            else
            {
                num.DecimalPlaces = 0;
                num.Increment = 10;
                num.Minimum = 50;
            }
        }
    }

    private Rectangle? SelectedWorkArea() => (_cmbMonitor.SelectedItem as MonitorItem)?.Monitor?.WorkArea;

    private void UpdatePreview()
    {
        var work = SelectedWorkArea();
        if (work is not { } wa)
        {
            _lblPreview.Text = _currentUnit == SizeUnit.Percent
                ? L.T("Pixelgröße hängt vom jeweiligen Monitor ab.")
                : string.Empty;
            return;
        }

        var rule = new FixedRule { Width = (double)_numWidth.Value, Height = (double)_numHeight.Value, Unit = _currentUnit };
        var size = SizeCalculator.RuleSize(rule, wa);
        string clampNote = size.Width > wa.Width || size.Height > wa.Height ? " – wird auf den Arbeitsbereich begrenzt" : string.Empty;
        _lblPreview.Text = L.F("Ergibt {0} × {1} px (Arbeitsbereich {2} × {3}){4}", size.Width, size.Height, wa.Width, wa.Height, clampNote);
    }

    private void OnOk(object? sender, EventArgs e)
    {
        if (AppSettings.NormalizeProcessName(_cmbProcess.Text).Length == 0)
        {
            MessageBox.Show(this, L.T("Bitte einen Programmnamen eingeben (z. B. chrome.exe)."), Text,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _cmbProcess.Focus();
            return;
        }
        DialogResult = DialogResult.OK;
        Close();
    }

    private static decimal Clamp(NumericUpDown num, decimal value) => Math.Clamp(value, num.Minimum, num.Maximum);

    private static void AddField(TableLayoutPanel table, string label, Control control)
    {
        var l = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 6, 12, 3) };
        Ui.AddRow(table, l);
        control.Margin = new Padding(3, 3, 3, 3);
        table.Controls.Add(control, 1, table.RowCount - 1);
    }

    private static void AddHint(TableLayoutPanel table, string text)
    {
        Ui.AddRow(table, new Label { AutoSize = true, Margin = Padding.Empty });
        var hint = Ui.Text(text, hint: true);
        hint.MaximumSize = new Size(340, 0);
        hint.Anchor = AnchorStyles.Left;
        table.Controls.Add(hint, 1, table.RowCount - 1);
    }
}
