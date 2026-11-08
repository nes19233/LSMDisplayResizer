using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text;
using LSMDisplayResizer.Logging;
using LSMDisplayResizer.Monitors;
using LSMDisplayResizer.Settings;
using Microsoft.Win32;

namespace LSMDisplayResizer.Wallpaper;

/// <summary>Physische Monitorgröße samt Herkunft (für die Anzeige).</summary>
internal readonly record struct PhysicalSizeInfo(double WidthMm, double HeightMm, double OffsetYMm, string Source);

/// <summary>
/// Erstellt und setzt das monitorübergreifende Hintergrundbild – auf Wunsch oder automatisch, wenn sich
/// die Monitoranordnung oder die Einstellungen geändert haben.
/// </summary>
internal sealed class WallpaperService : IDisposable
{
    /// <summary>Schätzwert ohne EDID: 96 dpi.</summary>
    private const double EstimatedMmPerPixel = 25.4 / 96;

    private readonly MonitorManager _monitors;
    private readonly Func<AppSettings> _getSettings;
    private readonly System.Windows.Forms.Timer _timer = new();
    private SynchronizationContext? _uiContext;
    private bool _busy;
    private bool _disposed;
    private bool _missingImageLogged;

    public WallpaperService(MonitorManager monitors, Func<AppSettings> getSettings)
    {
        _monitors = monitors;
        _getSettings = getSettings;
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            _ = AutoUpdateAsync();
        };
    }

    private static string SignatureFile => Path.Combine(WallpaperSetter.OutputDirectory, "zuletzt-gesetzt.txt");

    public void Start()
    {
        _uiContext = SynchronizationContext.Current;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        Schedule(5000); // beim Start prüfen, ob sich seit dem letzten Mal etwas geändert hat
    }

    /// <summary>Nach dem Speichern der Einstellungen aufrufen.</summary>
    public void NotifySettingsChanged() => Schedule(1000);

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        _uiContext?.Post(_ =>
        {
            if (!_disposed) Schedule(4000); // Windows ordnet nach Anzeigeänderungen noch einige Sekunden um
        }, null);

    private void Schedule(int milliseconds)
    {
        _timer.Stop();
        _timer.Interval = milliseconds;
        _timer.Start();
    }

    private async Task AutoUpdateAsync()
    {
        try
        {
            var ws = _getSettings().Wallpaper;
            if (!ws.AutoUpdate || !ws.IsConfigured) return;
            if (!File.Exists(ws.SourceImage))
            {
                if (!_missingImageLogged) Log.Warn($"Hintergrundbild nicht gefunden: {ws.SourceImage}");
                _missingImageLogged = true;
                return;
            }
            _missingImageLogged = false;

            var inputs = BuildInputs(_monitors.GetAll(), ws);
            string signature = BuildSignature(inputs, ws);
            if (signature == ReadSignature()) return;

            Log.Info("Monitoranordnung oder Einstellungen geändert – Hintergrundbild wird neu erstellt");
            await ApplyAsync(ws, inputs, signature);
        }
        catch (Exception ex)
        {
            Log.Error("Hintergrundbild konnte nicht automatisch aktualisiert werden", ex);
        }
    }

    /// <summary>Erstellt und setzt das Hintergrundbild sofort. Rückgabe: Meldung für die Oberfläche.</summary>
    public Task<string> ApplyNowAsync(WallpaperSettings ws)
    {
        var inputs = BuildInputs(_monitors.GetAll(), ws);
        return ApplyAsync(ws, inputs, BuildSignature(inputs, ws));
    }

    private async Task<string> ApplyAsync(WallpaperSettings ws, List<MonitorLayoutInput> inputs, string signature)
    {
        if (_busy) return "Das Hintergrundbild wird gerade erstellt …";
        _busy = true;
        try
        {
            string source = ws.SourceImage;
            var mode = ws.Mode;
            double gap = ws.GapMm;
            var alignment = ws.Alignment;

            // Laden, Zusammensetzen und Speichern im Hintergrund – große Bilder brauchen ein paar Sekunden.
            var (path, layout, imageSize) = await Task.Run(() =>
            {
                using var image = WallpaperRenderer.LoadImage(source);
                var result = WallpaperLayout.Compute(inputs, image.Size, mode, gap, alignment);
                using var composed = WallpaperRenderer.Compose(image, image.Size, result);

                Directory.CreateDirectory(WallpaperSetter.OutputDirectory);
                string file = Path.Combine(WallpaperSetter.OutputDirectory, $"gespannt-{DateTime.Now:yyyyMMdd-HHmmss}.png");
                composed.Save(file, ImageFormat.Png);
                return (file, result, image.Size);
            });

            DateTime before = WallpaperSetter.GetTranscodedTimestamp();
            WallpaperSetter.ApplySpanned(path);
            WallpaperSetter.CleanupOldImages(keep: path);

            // Windows legt jedes übernommene Hintergrundbild als "TranscodedWallpaper" ab – daran ist erkennbar,
            // ob es wirklich angekommen ist (SystemParametersInfo meldet auch dann Erfolg, wenn nicht).
            bool applied = false;
            for (int i = 0; i < 20 && !applied; i++)
            {
                await Task.Delay(250);
                applied = WallpaperSetter.GetTranscodedTimestamp() != before;
            }
            if (!applied)
            {
                const string notApplied = "Windows hat das Hintergrundbild nicht übernommen. Häufige Ursachen: eine Gruppenrichtlinie, " +
                                          "oder die App wurde aus einer anderen (Store-)App heraus gestartet – dann bitte die App beenden " +
                                          "und über das Startmenü neu starten.";
                Log.Warn($"{notApplied} Bild: {path}");
                return notApplied;
            }
            WriteSignature(signature);

            var message = new StringBuilder();
            message.Append($"Hintergrundbild gesetzt: {layout.VirtualBounds.Width} × {layout.VirtualBounds.Height} px, ");
            message.Append(layout.EffectiveMode == WallpaperMode.Physical ? "nach physischer Größe" : "nach Pixeln");
            message.Append($", {inputs.Count} Monitor(e).");
            if (layout.Warning != null) message.Append(' ').Append(layout.Warning);
            if (layout.MaxUpscale > 1.05)
            {
                var rec = layout.RecommendedSourceSize(imageSize);
                message.Append($" Das Bild wird bis zu {layout.MaxUpscale:0.0}-fach vergrößert (unscharf); " +
                               $"empfohlen sind mindestens {rec.Width} × {rec.Height} px.");
            }

            Log.Info(message.ToString());
            return message.ToString();
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>Monitore mit physischer Größe: gespeicherte Werte, sonst EDID, sonst Schätzung.</summary>
    public static List<MonitorLayoutInput> BuildInputs(IReadOnlyList<MonitorDescriptor> monitors, WallpaperSettings ws) =>
        monitors.Select(m =>
        {
            var size = GetPhysicalSize(m, ws);
            return new MonitorLayoutInput(m.DeviceId, m.DisplayName, m.Bounds, size.WidthMm, size.HeightMm, size.OffsetYMm,
                EdidReader.ReadDeviceSerial(m.DeviceId));
        }).ToList();

    public static PhysicalSizeInfo GetPhysicalSize(MonitorDescriptor monitor, WallpaperSettings ws)
    {
        var stored = ws.Monitors.FirstOrDefault(s => string.Equals(s.MonitorId, monitor.DeviceId, StringComparison.OrdinalIgnoreCase));
        if (stored != null)
        {
            var (w, h) = MatchOrientation(stored.WidthMm, stored.HeightMm, monitor.Bounds.Size);
            return new PhysicalSizeInfo(w, h, stored.OffsetYMm, "angepasst");
        }

        var (ew, eh, source) = GetDetectedSize(monitor);
        return new PhysicalSizeInfo(ew, eh, 0, source);
    }

    /// <summary>Größe aus der EDID (oder geschätzt), passend zur aktuellen Ausrichtung des Monitors.</summary>
    public static (double WidthMm, double HeightMm, string Source) GetDetectedSize(MonitorDescriptor monitor)
    {
        if (EdidReader.ReadSize(monitor.DeviceId) is { } edid)
        {
            var (w, h) = MatchOrientation(edid.WidthMm, edid.HeightMm, monitor.Bounds.Size);
            return (w, h, "aus Monitor");
        }
        return (Math.Round(monitor.Bounds.Width * EstimatedMmPerPixel), Math.Round(monitor.Bounds.Height * EstimatedMmPerPixel), "geschätzt");
    }

    /// <summary>Hochkant gedrehte Monitore: Breite und Höhe tauschen, wenn die Ausrichtung nicht zu den Pixeln passt.</summary>
    private static (double W, double H) MatchOrientation(double w, double h, Size pixels) =>
        (w > h) == (pixels.Width > pixels.Height) || Math.Abs(w - h) < 1 ? (w, h) : (h, w);

    private static string BuildSignature(IEnumerable<MonitorLayoutInput> inputs, WallpaperSettings ws)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.Append(ws.SourceImage).Append('|');
        try
        {
            sb.Append(File.GetLastWriteTimeUtc(ws.SourceImage).Ticks).Append('|');
        }
        catch
        {
            // egal
        }
        sb.Append(ws.Mode).Append('|').Append(ws.GapMm.ToString(inv)).Append('|').Append(ws.Alignment);
        foreach (var m in inputs.OrderBy(i => i.Id, StringComparer.Ordinal))
        {
            sb.Append('|').Append(m.Id).Append(';').Append(m.PixelBounds)
              .Append(';').Append(m.WidthMm.ToString(inv)).Append(';').Append(m.HeightMm.ToString(inv))
              .Append(';').Append(m.OffsetYMm.ToString(inv)).Append(';').Append(m.DeviceSerial);
        }
        return sb.ToString();
    }

    private static string ReadSignature()
    {
        try
        {
            return File.Exists(SignatureFile) ? File.ReadAllText(SignatureFile) : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void WriteSignature(string signature)
    {
        try
        {
            File.WriteAllText(SignatureFile, signature);
        }
        catch (Exception ex)
        {
            Log.Debug($"Signatur nicht gespeichert: {ex.Message}");
        }
    }

    /// <summary>Nach dem Wiederherstellen des alten Hintergrunds: nächstes automatisches Setzen nicht blockieren.</summary>
    public static void ForgetLastApplied()
    {
        try
        {
            File.Delete(SignatureFile);
        }
        catch
        {
            // egal
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _timer.Dispose();
    }
}
