using System.Drawing;

namespace LSMDisplayResizer;

/// <summary>Lädt die eingebetteten Icons in der passenden Größe.</summary>
internal static class AppIcons
{
    public static Icon Load(bool paused, Size size)
    {
        string name = paused ? "LSMDisplayResizer.app-paused.ico" : "LSMDisplayResizer.app.ico";
        using var stream = typeof(AppIcons).Assembly.GetManifestResourceStream(name)
                           ?? throw new InvalidOperationException($"Ressource {name} fehlt");
        return new Icon(stream, size);
    }

    /// <summary>Icon für den Infobereich (berücksichtigt die DPI-Skalierung).</summary>
    public static Icon LoadTray(bool paused) => Load(paused, SystemInformation.SmallIconSize);

    /// <summary>Icon für Fenstertitel.</summary>
    public static Icon LoadForm() => Load(false, new Size(32, 32));
}
