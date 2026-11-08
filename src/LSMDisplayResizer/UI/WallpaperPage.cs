using System.Drawing.Drawing2D;
using System.Globalization;
using LSMDisplayResizer.Monitors;
using LSMDisplayResizer.Settings;
using LSMDisplayResizer.Wallpaper;

namespace LSMDisplayResizer.UI;

/// <summary>Registerkarte „Hintergrundbild“: ein großes Bild nahtlos über alle Monitore.</summary>
internal sealed class WallpaperPage : UserControl
{
    private const int PreviewSourceMaxDimension = 2400;

    private readonly MonitorManager _monitors;
    private readonly WallpaperSettings _initial;

    private readonly TextBox _txtImage;
    private readonly Label _lblImageInfo;
    private readonly RadioButton _radPhysical;
    private readonly RadioButton _radPixels;
    private readonly NumericUpDown _numGap;
    private readonly ComboBox _cmbAlign;
    private readonly DataGridView _grid;
    private readonly Button _btnRedetect;
    private readonly PictureBox _preview;
    private readonly Label _lblStatus;
    private readonly CheckBox _chkAuto;
    private readonly Button _btnApply;
    private readonly Button _btnRestore;
    private readonly System.Windows.Forms.Timer _previewTimer = new() { Interval = 250 };

    private Bitmap? _previewSource;
    private Size _imageSize;
    private int _loadGeneration;
    private string _loadedPath = string.Empty;

    private enum Col
    {
        Monitor,
        Resolution,
        Width,
        Height,
        Diagonal,
        Offset,
        Source,
    }

    public WallpaperPage(WallpaperSettings settings, MonitorManager monitors, Func<Task> applyRequested, Func<Task> restoreRequested)
    {
        _monitors = monitors;
        _initial = settings.Clone();

        Dock = DockStyle.Fill;
        var table = Ui.Table(1);
        table.AutoScroll = true;
        table.Padding = new Padding(8);
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        Ui.AddRow(table, Ui.Text("Ein großes Bild wird so auf alle Monitore verteilt, dass es nahtlos ineinander übergeht – " +
                                 "statt auf jedem Monitor doppelt zu erscheinen. Das Bild sollte dafür mindestens so breit " +
                                 "sein wie alle Monitore zusammen."));

        // --- Bild ---
        Ui.AddRow(table, Ui.Heading("Bild"));
        var imageRow = Ui.Table(2);
        imageRow.AutoSize = true;
        imageRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        imageRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _txtImage = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(3, 5, 3, 3), PlaceholderText = "Bilddatei auswählen …" };
        imageRow.Controls.Add(_txtImage, 0, 0);
        imageRow.Controls.Add(Ui.Button("Durchsuchen …", (_, _) => BrowseImage()), 1, 0);
        Ui.AddRow(table, imageRow);
        _lblImageInfo = Ui.Text(string.Empty, hint: true);
        Ui.AddRow(table, _lblImageInfo);

        // --- Aufteilung ---
        Ui.AddRow(table, Ui.Heading("Aufteilung"));
        _radPhysical = Ui.Radio("Nach physischer Größe der Monitore (empfohlen)");
        Ui.AddRow(table, _radPhysical);
        Ui.AddRow(table, Ui.Text("Übergänge stimmen auch bei unterschiedlich großen Monitoren. Die Bildflächen werden aus den " +
                                 "Monitordaten gelesen und können unten angepasst werden. Für nebeneinander stehende Monitore.",
            hint: true, indent: 20));
        _radPixels = Ui.Radio("Nach Pixeln (Anordnung aus den Windows-Anzeigeeinstellungen)");
        Ui.AddRow(table, _radPixels);
        Ui.AddRow(table, Ui.Text("Passt bei gleich großen Monitoren mit gleicher Auflösung; funktioniert für jede Anordnung.",
            hint: true, indent: 20));

        var physicalRow = new FlowLayoutPanel { AutoSize = true, WrapContents = true, Margin = new Padding(0, 6, 0, 0) };
        _numGap = new NumericUpDown { Width = 70, Minimum = 0, Maximum = 300, DecimalPlaces = 0, TextAlign = HorizontalAlignment.Right };
        _cmbAlign = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
        _cmbAlign.Items.AddRange(["Oben bündig", "Mittig", "Unten bündig"]);
        physicalRow.Controls.Add(FlowLabel("Abstand zwischen den Bildflächen (Rahmen):"));
        physicalRow.Controls.Add(_numGap);
        physicalRow.Controls.Add(FlowLabel("mm"));
        physicalRow.Controls.Add(FlowLabel("    Ausrichtung:"));
        physicalRow.Controls.Add(_cmbAlign);
        Ui.AddRow(table, physicalRow);

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.CellSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = SystemColors.Window,
            BorderStyle = BorderStyle.FixedSingle,
            EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2,
            Margin = new Padding(3, 6, 3, 3),
        };
        AddColumn("Monitor", 34, readOnly: true);
        AddColumn("Auflösung", 13, readOnly: true);
        AddColumn("Breite (mm)", 11, readOnly: false);
        AddColumn("Höhe (mm)", 11, readOnly: false);
        AddColumn("Diagonale", 10, readOnly: true);
        AddColumn("Versatz (mm)", 11, readOnly: false);
        AddColumn("Quelle", 11, readOnly: true);
        Ui.AddRow(table, _grid, sizeType: SizeType.Absolute, height: 120);

        var gridButtons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        _btnRedetect = Ui.Button("Größen neu auslesen", (_, _) => FillGrid(useStored: false));
        gridButtons.Controls.Add(_btnRedetect);
        gridButtons.Controls.Add(FlowLabel("Sichtbare Bildfläche ohne Rahmen. Versatz: positiv = Monitor steht tiefer."));
        Ui.AddRow(table, gridButtons);

        // --- Vorschau ---
        Ui.AddRow(table, Ui.Heading("Vorschau"));
        _preview = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(32, 32, 32),
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(3),
        };
        Ui.AddRow(table, _preview, sizeType: SizeType.Absolute, height: 190);
        _lblStatus = Ui.Text(string.Empty);
        Ui.AddRow(table, _lblStatus);

        _chkAuto = Ui.Check("Automatisch neu erstellen, wenn sich die Monitoranordnung ändert");
        _chkAuto.Margin = new Padding(3, 10, 3, 2);
        Ui.AddRow(table, _chkAuto);

        var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 6, 0, 0) };
        _btnApply = Ui.Button("Speichern und als Hintergrund setzen", async (_, _) => await RunBusy(applyRequested));
        _btnApply.Font = new Font(_btnApply.Font, FontStyle.Bold);
        _btnRestore = Ui.Button("Vorherigen Hintergrund wiederherstellen", async (_, _) => await RunBusy(restoreRequested));
        actions.Controls.Add(_btnApply);
        actions.Controls.Add(_btnRestore);
        Ui.AddRow(table, actions);

        Controls.Add(table);

        // Ereignisse
        _txtImage.Leave += (_, _) => LoadImageIfChanged();
        _txtImage.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                LoadImageIfChanged();
            }
        };
        _radPhysical.CheckedChanged += (_, _) => OnOptionsChanged();
        _radPixels.CheckedChanged += (_, _) => OnOptionsChanged();
        _numGap.ValueChanged += (_, _) => SchedulePreview();
        _cmbAlign.SelectedIndexChanged += (_, _) => SchedulePreview();
        _grid.CellValidating += OnCellValidating;
        _grid.CellEndEdit += OnCellEndEdit;
        _grid.DataError += (_, e) => e.ThrowException = false;
        _preview.SizeChanged += (_, _) => SchedulePreview();
        _previewTimer.Tick += (_, _) =>
        {
            _previewTimer.Stop();
            UpdatePreview();
        };

        LoadValues(settings);
    }

    /// <summary>Überträgt die Eingaben in <paramref name="target"/>.</summary>
    public void SaveTo(WallpaperSettings target)
    {
        target.SourceImage = _txtImage.Text.Trim().Trim('"');
        target.Mode = _radPixels.Checked ? WallpaperMode.Pixels : WallpaperMode.Physical;
        target.GapMm = (double)_numGap.Value;
        target.Alignment = (WallpaperAlignment)Math.Max(0, _cmbAlign.SelectedIndex);
        target.AutoUpdate = _chkAuto.Checked;

        // Angepasste Werte speichern; Monitore, die gerade nicht angeschlossen sind, behalten ihre Werte.
        var connected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<MonitorPhysicalSize>();
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.Tag is not MonitorDescriptor m) continue;
            connected.Add(m.DeviceId);
            var (dw, dh, _) = WallpaperService.GetDetectedSize(m);
            double w = Cell(row, Col.Width), h = Cell(row, Col.Height), offset = Cell(row, Col.Offset);
            bool custom = Math.Abs(w - dw) > 0.5 || Math.Abs(h - dh) > 0.5 || Math.Abs(offset) > 0.05;
            if (custom)
                list.Add(new MonitorPhysicalSize { MonitorId = m.DeviceId, MonitorName = m.DisplayName, WidthMm = w, HeightMm = h, OffsetYMm = offset });
        }
        list.AddRange(_initial.Monitors.Where(s => !connected.Contains(s.MonitorId)).Select(s => s.Clone()));
        target.Monitors = list;
    }

    public void SetStatus(string message) => _lblStatus.Text = message;

    public void SetAutoUpdate(bool enabled) => _chkAuto.Checked = enabled;

    private void LoadValues(WallpaperSettings ws)
    {
        _txtImage.Text = ws.SourceImage;
        _radPhysical.Checked = ws.Mode == WallpaperMode.Physical;
        _radPixels.Checked = ws.Mode == WallpaperMode.Pixels;
        _numGap.Value = (decimal)Math.Clamp(ws.GapMm, 0, 300);
        _cmbAlign.SelectedIndex = (int)ws.Alignment;
        _chkAuto.Checked = ws.AutoUpdate;
        FillGrid(useStored: true);
        UpdateEnabledState();
        UpdateRestoreButton();
        LoadImageIfChanged();
        SchedulePreview();
    }

    // --- Monitortabelle -------------------------------------------------------------------

    private void AddColumn(string header, float weight, bool readOnly)
    {
        var col = new DataGridViewTextBoxColumn
        {
            HeaderText = header,
            FillWeight = weight,
            ReadOnly = readOnly,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        };
        if (!readOnly) col.DefaultCellStyle.Format = "0.#";
        if (header != "Monitor" && header != "Quelle") col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        if (readOnly) col.DefaultCellStyle.ForeColor = SystemColors.GrayText;
        _grid.Columns.Add(col);
    }

    private void FillGrid(bool useStored)
    {
        var ws = useStored ? _initial : new WallpaperSettings();
        _grid.Rows.Clear();
        foreach (var m in _monitors.GetAll().OrderBy(m => m.Bounds.Left))
        {
            var size = WallpaperService.GetPhysicalSize(m, ws);
            int i = _grid.Rows.Add(m.DisplayName, $"{m.Bounds.Width} × {m.Bounds.Height}",
                size.WidthMm, size.HeightMm, string.Empty, size.OffsetYMm, size.Source);
            _grid.Rows[i].Tag = m;
            UpdateDiagonal(_grid.Rows[i]);
        }
        SchedulePreview();
    }

    private void OnCellValidating(object? sender, DataGridViewCellValidatingEventArgs e)
    {
        var col = (Col)e.ColumnIndex;
        if (col is not (Col.Width or Col.Height or Col.Offset)) return;
        string text = Convert.ToString(e.FormattedValue, CultureInfo.CurrentCulture) ?? string.Empty;
        bool ok = double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double v) && double.IsFinite(v) &&
                  (col == Col.Offset ? v is >= -2000 and <= 2000 : v is >= 50 and <= 5000);
        _grid.Rows[e.RowIndex].ErrorText = ok ? string.Empty
            : col == Col.Offset ? "Versatz zwischen -2000 und 2000 mm" : "Wert zwischen 50 und 5000 mm";
        e.Cancel = !ok;
    }

    private void OnCellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        var row = _grid.Rows[e.RowIndex];
        row.ErrorText = string.Empty;
        // Eingabe als Zahl ablegen (nicht als Text), damit das Format greift.
        if (double.TryParse(Convert.ToString(row.Cells[e.ColumnIndex].Value, CultureInfo.CurrentCulture),
                NumberStyles.Float, CultureInfo.CurrentCulture, out double v))
            row.Cells[e.ColumnIndex].Value = v;
        row.Cells[(int)Col.Source].Value = "angepasst";
        UpdateDiagonal(row);
        SchedulePreview();
    }

    private static void UpdateDiagonal(DataGridViewRow row)
    {
        double w = Cell(row, Col.Width), h = Cell(row, Col.Height);
        row.Cells[(int)Col.Diagonal].Value = w > 0 && h > 0 ? $"{Math.Sqrt(w * w + h * h) / 25.4:0.0}″" : "-";
    }

    private static double Cell(DataGridViewRow row, Col col)
    {
        object? v = row.Cells[(int)col].Value;
        return v switch
        {
            double d => d,
            null => 0,
            _ => double.TryParse(Convert.ToString(v, CultureInfo.CurrentCulture), NumberStyles.Float, CultureInfo.CurrentCulture, out double r) ? r : 0,
        };
    }

    private List<MonitorLayoutInput> CurrentInputs() =>
        _grid.Rows.Cast<DataGridViewRow>()
            .Where(r => r.Tag is MonitorDescriptor)
            .Select(r =>
            {
                var m = (MonitorDescriptor)r.Tag!;
                return new MonitorLayoutInput(m.DeviceId, m.DisplayName, m.Bounds,
                    Math.Max(50, Cell(r, Col.Width)), Math.Max(50, Cell(r, Col.Height)), Cell(r, Col.Offset),
                    EdidReader.ReadDeviceSerial(m.DeviceId));
            })
            .ToList();

    // --- Bild ------------------------------------------------------------------------------

    private void BrowseImage()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Hintergrundbild auswählen",
            Filter = "Bilder|" + string.Join(";", WallpaperRenderer.SupportedExtensions.Select(e => "*" + e)) + "|Alle Dateien|*.*",
        };
        try
        {
            string current = _txtImage.Text.Trim().Trim('"');
            if (File.Exists(current)) dlg.InitialDirectory = Path.GetDirectoryName(current);
            else dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        }
        catch
        {
            // egal
        }

        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _txtImage.Text = dlg.FileName;
        LoadImageIfChanged();
    }

    private async void LoadImageIfChanged()
    {
        string path = _txtImage.Text.Trim().Trim('"');
        if (string.Equals(path, _loadedPath, StringComparison.OrdinalIgnoreCase)) return;
        _loadedPath = path;
        int generation = ++_loadGeneration;

        _previewSource?.Dispose();
        _previewSource = null;
        _imageSize = Size.Empty;

        if (path.Length == 0)
        {
            _lblImageInfo.Text = "Kein Bild ausgewählt – die Funktion ist nicht aktiv.";
            SchedulePreview();
            return;
        }
        if (!File.Exists(path))
        {
            _lblImageInfo.Text = "Datei nicht gefunden.";
            SchedulePreview();
            return;
        }

        _lblImageInfo.Text = "Bild wird geladen …";
        try
        {
            var (preview, size) = await Task.Run(() =>
            {
                using var full = WallpaperRenderer.LoadImage(path);
                return (WallpaperRenderer.CreateScaledCopy(full, PreviewSourceMaxDimension), full.Size);
            });

            if (generation != _loadGeneration || IsDisposed)
            {
                preview.Dispose();
                return;
            }
            _previewSource = preview;
            _imageSize = size;
            _lblImageInfo.Text = $"Bildgröße: {size.Width} × {size.Height} px";
        }
        catch (Exception ex)
        {
            if (generation != _loadGeneration) return;
            _lblImageInfo.Text = $"Bild kann nicht geladen werden: {ex.Message}";
        }
        SchedulePreview();
    }

    // --- Vorschau --------------------------------------------------------------------------

    private void OnOptionsChanged()
    {
        UpdateEnabledState();
        SchedulePreview();
    }

    private void UpdateEnabledState()
    {
        bool physical = _radPhysical.Checked;
        _numGap.Enabled = physical;
        _cmbAlign.Enabled = physical;
        _grid.Enabled = physical;
        _btnRedetect.Enabled = physical;
    }

    private void UpdateRestoreButton() => _btnRestore.Enabled = WallpaperSetter.HasBackup;

    private void SchedulePreview()
    {
        _previewTimer.Stop();
        _previewTimer.Start();
    }

    private void UpdatePreview()
    {
        if (IsDisposed || _preview.Width < 20 || _preview.Height < 20) return;

        var inputs = CurrentInputs();
        if (inputs.Count == 0) return;

        var mode = _radPixels.Checked ? WallpaperMode.Pixels : WallpaperMode.Physical;
        var alignment = (WallpaperAlignment)Math.Max(0, _cmbAlign.SelectedIndex);
        Size layoutSize = _previewSource != null ? _imageSize : new Size(16, 9);
        var layout = WallpaperLayout.Compute(inputs, layoutSize, mode, (double)_numGap.Value, alignment);
        Rectangle vb = layout.VirtualBounds;
        if (vb.Width <= 0 || vb.Height <= 0) return;

        double scale = Math.Min((double)_preview.Width / vb.Width, (double)_preview.Height / vb.Height);
        Bitmap bmp = _previewSource != null
            ? WallpaperRenderer.Compose(_previewSource, _imageSize, layout, scale)
            : new Bitmap(Math.Max(1, (int)(vb.Width * scale)), Math.Max(1, (int)(vb.Height * scale)));

        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(Color.FromArgb(220, 255, 255, 255), 1.5f) { DashStyle = DashStyle.Dash };
            using var font = new Font(Font.FontFamily, 8.5f);
            using var shadow = new SolidBrush(Color.FromArgb(160, 0, 0, 0));
            int index = 1;
            foreach (var slice in layout.Slices.OrderBy(s => s.Monitor.PixelBounds.Left))
            {
                var pb = slice.Monitor.PixelBounds;
                var r = new RectangleF((float)((pb.Left - vb.Left) * scale), (float)((pb.Top - vb.Top) * scale),
                    (float)(pb.Width * scale) - 1, (float)(pb.Height * scale) - 1);
                if (_previewSource == null) g.FillRectangle(Brushes.DimGray, r);
                g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
                string label = $"{index++}: {pb.Width}×{pb.Height}";
                var size = g.MeasureString(label, font);
                g.FillRectangle(shadow, r.X + 4, r.Y + 4, size.Width + 4, size.Height + 2);
                g.DrawString(label, font, Brushes.White, r.X + 6, r.Y + 5);
            }
        }

        var old = _preview.Image;
        _preview.Image = bmp;
        old?.Dispose();

        UpdateStatus(layout);
    }

    private void UpdateStatus(WallpaperLayoutResult layout)
    {
        var parts = new List<string>();
        bool warn = false;

        if (_previewSource != null)
        {
            if (layout.MaxUpscale > 1.05)
            {
                var rec = layout.RecommendedSourceSize(_imageSize);
                parts.Add($"Das Bild ist zu klein und wird bis zu {layout.MaxUpscale:0.0}-fach vergrößert (unscharf). " +
                          $"Empfohlen: mindestens {rec.Width} × {rec.Height} px.");
                warn = true;
            }
            else
            {
                parts.Add("Die Bildauflösung reicht für alle Monitore.");
            }
        }
        if (layout.Warning != null)
        {
            parts.Add(layout.Warning);
            warn = true;
        }
        if (layout.EffectiveMode == WallpaperMode.Physical)
        {
            var sorted = layout.Slices.Select(s => s.Monitor).OrderBy(m => m.PixelBounds.Left).ToList();
            var shared = sorted.Zip(sorted.Skip(1))
                .Where(p => p.First.IsSameDeviceAs(p.Second))
                .Select(p => $"{ShortName(p.First)} und {ShortName(p.Second)}")
                .ToList();
            if (shared.Count > 0)
                parts.Add($"{string.Join(", ", shared)} sind Bildflächen desselben Monitors (Bild-neben-Bild) – dazwischen wird kein Rahmen abgezogen.");
        }
        if (WallpaperSetter.GetPolicyHint() is { } policy)
        {
            parts.Add(policy);
            warn = true;
        }

        _lblStatus.Text = string.Join(" ", parts);
        _lblStatus.ForeColor = warn ? Color.DarkGoldenrod : SystemColors.GrayText;
    }

    /// <summary>"DELL U3425WE – DISPLAY1 (…)" → "DISPLAY1"</summary>
    private static string ShortName(MonitorLayoutInput m)
    {
        int dash = m.Name.IndexOf(" – ", StringComparison.Ordinal);
        string rest = dash >= 0 ? m.Name[(dash + 3)..] : m.Name;
        int paren = rest.IndexOf(" (", StringComparison.Ordinal);
        return paren >= 0 ? rest[..paren] : rest;
    }

    private async Task RunBusy(Func<Task> action)
    {
        LoadImageIfChanged();
        _btnApply.Enabled = false;
        _btnRestore.Enabled = false;
        UseWaitCursor = true;
        try
        {
            await action();
        }
        finally
        {
            UseWaitCursor = false;
            _btnApply.Enabled = true;
            UpdateRestoreButton();
        }
    }

    private static Label FlowLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(3, 7, 3, 3),
        ForeColor = SystemColors.ControlText,
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _previewTimer.Dispose();
            _previewSource?.Dispose();
            _preview.Image?.Dispose();
        }
        base.Dispose(disposing);
    }
}
