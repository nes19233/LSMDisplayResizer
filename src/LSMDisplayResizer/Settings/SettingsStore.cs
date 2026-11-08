using System.Text.Json;
using System.Text.Json.Serialization;
using LSMDisplayResizer.Logging;

namespace LSMDisplayResizer.Settings;

/// <summary>Lädt und speichert <see cref="AppSettings"/> als JSON.</summary>
internal sealed class SettingsStore(string filePath)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string FilePath { get; } = filePath;

    public bool FileExists => File.Exists(FilePath);

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new AppSettings();

            string json = File.ReadAllText(FilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            settings.Normalize();
            return settings;
        }
        catch (Exception ex)
        {
            Log.Error($"Einstellungen konnten nicht gelesen werden ({FilePath}), Standardwerte werden verwendet", ex);
            TryBackupBrokenFile();
            return new AppSettings();
        }
    }

    public bool Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(tmp, FilePath, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Einstellungen konnten nicht gespeichert werden", ex);
            return false;
        }
    }

    private void TryBackupBrokenFile()
    {
        try
        {
            if (File.Exists(FilePath)) File.Copy(FilePath, FilePath + ".defekt", overwrite: true);
        }
        catch
        {
            // egal
        }
    }
}
