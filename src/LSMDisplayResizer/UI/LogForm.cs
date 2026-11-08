using System.Diagnostics;
using System.Text;
using LSMDisplayResizer.Logging;

namespace LSMDisplayResizer.UI;

/// <summary>Zeigt die letzten Log-Einträge live an.</summary>
internal sealed class LogForm : Form
{
    private const int MaxTextLength = 300_000;

    private readonly TextBox _text;

    public LogForm()
    {
        SuspendLayout();
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        Text = $"{AppInfo.Name} {AppInfo.Version} – Log";
        Icon = AppIcons.LoadForm();
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(900, 480);
        MinimumSize = new Size(500, 250);
        Padding = new Padding(8);

        var table = Ui.Table(1);
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _text = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9F),
            BackColor = SystemColors.Window,
        };
        Ui.AddRow(table, _text, sizeType: SizeType.Percent, height: 100);

        var footer = Ui.Table(2);
        footer.AutoSize = true;
        footer.Margin = new Padding(0, 8, 0, 0);
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var left = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        left.Controls.Add(Ui.Button("Logdatei öffnen", (_, _) => Open(AppInfo.LogFile)));
        left.Controls.Add(Ui.Button("Ordner öffnen", (_, _) => Open(AppInfo.DataDirectory)));
        left.Controls.Add(Ui.Button("Anzeige leeren", (_, _) => _text.Clear()));
        footer.Controls.Add(left, 0, 0);
        footer.Controls.Add(Ui.ButtonRow(Ui.Button("Schließen", (_, _) => Close())), 1, 0);
        Ui.AddRow(table, footer);

        Controls.Add(table);
        ResumeLayout(true);

        var sb = new StringBuilder();
        foreach (var entry in Log.GetRecent()) sb.Append(entry).Append(Environment.NewLine);
        _text.Text = sb.ToString();

        Log.EntryAdded += OnEntryAdded;
        FormClosed += (_, _) => Log.EntryAdded -= OnEntryAdded;
        Shown += (_, _) => ScrollToEnd();
    }

    private void OnEntryAdded(LogEntry entry)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try
        {
            BeginInvoke(() =>
            {
                if (IsDisposed) return;
                if (_text.TextLength > MaxTextLength) _text.Clear();
                _text.AppendText(entry + Environment.NewLine);
            });
        }
        catch (InvalidOperationException)
        {
            // Fenster wird gerade geschlossen.
        }
    }

    private void ScrollToEnd()
    {
        _text.SelectionStart = _text.TextLength;
        _text.ScrollToCaret();
    }

    private void Open(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"{path} konnte nicht geöffnet werden:\n{ex.Message}", Text,
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
