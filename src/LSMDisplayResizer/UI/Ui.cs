namespace LSMDisplayResizer.UI;

/// <summary>Kleine Helfer, damit die per Code gebauten Formulare einheitlich aussehen.</summary>
internal static class Ui
{
    public static TableLayoutPanel Table(int columns, int rows = 0)
    {
        var t = new TableLayoutPanel
        {
            ColumnCount = columns,
            RowCount = rows,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
        };
        return t;
    }

    /// <summary>Beschriftung; umbricht in einer TableLayoutPanel-Spalte mit Prozentbreite automatisch.</summary>
    public static Label Text(string text, bool hint = false, int indent = 0) => new()
    {
        Text = text,
        AutoSize = true,
        Anchor = AnchorStyles.Left | AnchorStyles.Right,
        Margin = new Padding(3 + indent, hint ? 0 : 4, 3, 4),
        ForeColor = hint ? SystemColors.GrayText : SystemColors.ControlText,
    };

    public static Label Heading(string text)
    {
        var label = Text(text);
        label.Font = new Font(label.Font, FontStyle.Bold);
        label.Margin = new Padding(3, 12, 3, 2);
        return label;
    }

    public static CheckBox Check(string text, int indent = 0) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(3 + indent, 4, 3, 2),
    };

    public static RadioButton Radio(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(3, 4, 3, 0),
    };

    public static Button Button(string text, EventHandler onClick)
    {
        var b = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10, 2, 10, 2),
            Margin = new Padding(3),
            UseVisualStyleBackColor = true,
        };
        b.Click += onClick;
        return b;
    }

    /// <summary>Buttons rechtsbündig in einer Zeile (erste Angabe steht ganz rechts).</summary>
    public static FlowLayoutPanel ButtonRow(params Control[] buttons)
    {
        var flow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
        };
        flow.Controls.AddRange(buttons);
        return flow;
    }

    public static void AddRow(TableLayoutPanel table, Control control, int columnSpan = 1, SizeType sizeType = SizeType.AutoSize, float height = 0)
    {
        int row = table.RowCount;
        table.RowCount = row + 1;
        table.RowStyles.Add(new RowStyle(sizeType, height));
        table.Controls.Add(control, 0, row);
        if (columnSpan > 1) table.SetColumnSpan(control, columnSpan);
    }

    /// <summary>Nimmt den restlichen Platz einer Tabelle ein.</summary>
    public static void AddFiller(TableLayoutPanel table) =>
        AddRow(table, new Panel { Margin = Padding.Empty, Height = 1 }, Math.Max(1, table.ColumnCount), SizeType.Percent, 100);

    public static ColumnHeader Column(ListView list, string text, int width, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var col = list.Columns.Add(text, width, align);
        col.Tag = width; // logische Breite bei 96 DPI, wird in ScaleColumns umgerechnet
        return col;
    }

    /// <summary>Spaltenbreiten an die DPI des Formulars anpassen (ListView skaliert sie nicht selbst).</summary>
    public static void ScaleColumns(ListView list)
    {
        foreach (ColumnHeader col in list.Columns)
        {
            if (col.Tag is int logical) col.Width = list.LogicalToDeviceUnits(logical);
        }
    }

    public static ListView DetailsList() => new()
    {
        View = View.Details,
        FullRowSelect = true,
        MultiSelect = false,
        HideSelection = false,
        GridLines = false,
        Dock = DockStyle.Fill,
    };

    /// <summary>Laufende Programme mit Fenstern (für Vorschlagslisten).</summary>
    public static string[] RunningProcessNames()
    {
        try
        {
            return Tracking.WindowInspector.EnumerateCandidateWindows()
                .Select(w => w.ProcessName)
                .Where(p => !p.StartsWith("pid", StringComparison.Ordinal))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }
}
