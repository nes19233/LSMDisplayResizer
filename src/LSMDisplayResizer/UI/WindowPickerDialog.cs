using LSMDisplayResizer.Monitors;
using LSMDisplayResizer.Tracking;

namespace LSMDisplayResizer.UI;

/// <summary>Auswahl eines offenen Fensters für "Aktuelles Fenster übernehmen".</summary>
internal sealed class WindowPickerDialog : Form
{
    private readonly MonitorManager _monitors;
    private readonly ListView _list;
    private readonly Button _ok;

    private WindowSnapshot? _selectedWindow;
    private MonitorDescriptor? _selectedMonitor;

    public WindowPickerDialog(MonitorManager monitors)
    {
        _monitors = monitors;

        SuspendLayout();
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        Text = "Fenster auswählen";
        Icon = AppIcons.LoadForm();
        ShowInTaskbar = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(820, 460);
        MinimumSize = new Size(560, 300);
        Padding = new Padding(10);

        var table = Ui.Table(1);
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        Ui.AddRow(table, Ui.Text("Fenster in die gewünschte Größe bringen, auf den Zielmonitor schieben und hier auswählen. " +
                                 "Programm, Monitor und aktuelle Größe werden als neue Regel übernommen."));

        _list = Ui.DetailsList();
        Ui.Column(_list, "Programm", 130);
        Ui.Column(_list, "Fenstertitel", 230);
        Ui.Column(_list, "Monitor", 200);
        Ui.Column(_list, "Größe (px)", 85, HorizontalAlignment.Right);
        Ui.Column(_list, "Größe (%)", 80, HorizontalAlignment.Right);
        _list.SelectedIndexChanged += (_, _) => _ok!.Enabled = _list.SelectedItems.Count == 1;
        _list.DoubleClick += (_, _) => Accept();
        Ui.AddRow(table, _list, sizeType: SizeType.Percent, height: 100);

        _ok = Ui.Button("Übernehmen", (_, _) => Accept());
        _ok.Enabled = false;
        var cancel = Ui.Button("Abbrechen", (_, _) => { DialogResult = DialogResult.Cancel; Close(); });
        var refresh = Ui.Button("Aktualisieren", (_, _) => Fill());

        var footer = Ui.Table(2);
        footer.AutoSize = true;
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.Controls.Add(refresh, 0, 0);
        footer.Controls.Add(Ui.ButtonRow(cancel, _ok), 1, 0);
        footer.Margin = new Padding(0, 8, 0, 0);
        Ui.AddRow(table, footer);

        AcceptButton = _ok;
        CancelButton = cancel;
        Controls.Add(table);
        ResumeLayout(true);

        Load += (_, _) =>
        {
            Ui.ScaleColumns(_list);
            Fill();
        };
    }

    public (WindowSnapshot Window, MonitorDescriptor Monitor)? GetSelection() =>
        _selectedWindow != null && _selectedMonitor != null ? (_selectedWindow, _selectedMonitor) : null;

    private void Fill()
    {
        _list.BeginUpdate();
        try
        {
            _list.Items.Clear();
            foreach (var w in WindowInspector.EnumerateCandidateWindows())
            {
                var monitor = _monitors.FromWindow(w.Handle);
                if (monitor == null) continue;

                var work = monitor.WorkArea;
                string percent = work.Width > 0 && work.Height > 0
                    ? $"{100.0 * w.FrameRect.Width / work.Width:0} × {100.0 * w.FrameRect.Height / work.Height:0}"
                    : "-";

                var item = new ListViewItem([
                    w.ProcessName,
                    string.IsNullOrWhiteSpace(w.Title) ? "(ohne Titel)" : w.Title,
                    monitor.DisplayName,
                    $"{w.FrameRect.Width} × {w.FrameRect.Height}",
                    percent,
                ])
                {
                    Tag = (w, monitor),
                };
                _list.Items.Add(item);
            }
        }
        finally
        {
            _list.EndUpdate();
        }
        _ok.Enabled = false;
    }

    private void Accept()
    {
        if (_list.SelectedItems.Count != 1) return;
        if (_list.SelectedItems[0].Tag is not (WindowSnapshot w, MonitorDescriptor m)) return;

        // Frisch einlesen – das Fenster könnte seit dem Füllen der Liste verändert worden sein.
        var reason = WindowInspector.Check(w.Handle, null, out var fresh);
        var monitor = _monitors.FromWindow(w.Handle);
        _selectedWindow = fresh ?? w;
        _selectedMonitor = monitor ?? m;
        if (reason == SkipReason.Invalid)
        {
            MessageBox.Show(this, "Das Fenster existiert nicht mehr.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            Fill();
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
