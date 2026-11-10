using System.Globalization;

namespace LSMDisplayResizer.Localization;

/// <summary>
/// Übersetzung der Oberfläche (Deutsch/Englisch). Deutsch ist die Ausgangssprache: Texte stehen auf Deutsch im Code,
/// <see cref="English"/> enthält die Übersetzungen (Schlüssel = deutscher Text). Statische Texte eines Fensters werden
/// per <see cref="Translate(Control)"/> übersetzt, dynamische Texte per <see cref="T"/> bzw. <see cref="F"/>.
/// </summary>
internal static class L
{
    /// <summary>Einstellungswerte für die Sprache.</summary>
    public const string Auto = "auto", German = "de", EnglishCode = "en";

    /// <summary>Als Control.Tag: Text ist bereits übersetzt bzw. dynamisch – nicht noch einmal übersetzen.</summary>
    public const string NoTranslate = "no-translate";

    public static bool IsEnglish { get; private set; }

    /// <summary>Sprache festlegen: "de", "en" oder "auto" (folgt der Windows-Anzeigesprache).</summary>
    public static void Configure(string? setting)
    {
        IsEnglish = setting switch
        {
            EnglishCode => true,
            German => false,
            _ => !string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "de", StringComparison.OrdinalIgnoreCase),
        };
    }

    /// <summary>Übersetzt einen deutschen Text (ohne Treffer bleibt er Deutsch).</summary>
    public static string T(string german)
    {
        if (!IsEnglish || string.IsNullOrEmpty(german)) return german;
        if (English.Texts.TryGetValue(german, out var en)) return en;
        ReportMissing(german);
        return german;
    }

    /// <summary>Übersetzt einen Formattext ("… {0} …") und setzt die Werte ein.</summary>
    public static string F(string germanFormat, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, T(germanFormat), args);

    /// <summary>Übersetzt alle statischen Texte eines Fensters bzw. Steuerelements samt Unterelementen.</summary>
    public static void Translate(Control root)
    {
        if (!IsEnglish) return;
        TranslateControl(root);
    }

    public static void Translate(ToolStripItemCollection items)
    {
        if (!IsEnglish) return;
        foreach (ToolStripItem item in items)
        {
            item.Text = T(item.Text ?? string.Empty);
            if (item is ToolStripMenuItem { DropDownItems.Count: > 0 } menu) Translate(menu.DropDownItems);
        }
    }

    private static void TranslateControl(Control c)
    {
        if (c.Tag is NoTranslate)
        {
            foreach (Control child in c.Controls) TranslateControl(child);
            return;
        }

        switch (c)
        {
            case Form or Label or ButtonBase or TabPage or GroupBox:
                if (!string.IsNullOrEmpty(c.Text)) c.Text = T(c.Text);
                break;
            case TextBox tb when !string.IsNullOrEmpty(tb.PlaceholderText):
                tb.PlaceholderText = T(tb.PlaceholderText);
                break;
            case ListView lv:
                foreach (ColumnHeader col in lv.Columns) col.Text = T(col.Text);
                break;
            case DataGridView grid:
                foreach (DataGridViewColumn col in grid.Columns) col.HeaderText = T(col.HeaderText);
                break;
            case ComboBox combo:
                TranslateComboItems(combo);
                break;
        }

        foreach (Control child in c.Controls) TranslateControl(child);
    }

    /// <summary>Feste Text-Einträge von Auswahllisten übersetzen (Objekte wie Monitore bleiben unberührt).</summary>
    private static void TranslateComboItems(ComboBox combo)
    {
        int selected = combo.SelectedIndex;
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is string s && English.Texts.TryGetValue(s, out var en)) combo.Items[i] = en;
        }
        if (selected >= 0 && selected < combo.Items.Count) combo.SelectedIndex = selected;
    }

    // --- Fehlende Übersetzungen (nur Debug-Build) ---------------------------------------------

    private static readonly HashSet<string> Missing = [];

    [System.Diagnostics.Conditional("DEBUG")]
    private static void ReportMissing(string german)
    {
        // Nur Texte mit Buchstaben melden (keine Zahlen, Größenangaben, Prozessnamen o. Ä.)
        if (german.Length <= 3 || !german.Any(char.IsLetter) || german.Contains(".exe", StringComparison.OrdinalIgnoreCase)) return;
        lock (Missing)
        {
            if (!Missing.Add(german)) return;
            try
            {
                File.AppendAllText(Path.Combine(AppInfo.DataDirectory, "fehlende-uebersetzungen.txt"),
                    german.Replace("\r", "\\r").Replace("\n", "\\n") + Environment.NewLine);
            }
            catch
            {
                // egal
            }
        }
    }
}
