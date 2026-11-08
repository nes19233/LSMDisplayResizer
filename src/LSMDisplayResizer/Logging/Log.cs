using System.Text;

namespace LSMDisplayResizer.Logging;

internal enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
}

internal sealed record LogEntry(DateTime Time, LogLevel Level, string Message)
{
    public override string ToString()
    {
        string level = Level switch
        {
            LogLevel.Debug => "DEBUG",
            LogLevel.Info => "INFO ",
            LogLevel.Warning => "WARN ",
            _ => "FEHLER",
        };
        return $"{Time:yyyy-MM-dd HH:mm:ss} {level} {Message}";
    }
}

/// <summary>
/// Einfaches, threadsicheres Log: Datei mit Größenbegrenzung plus Puffer der letzten Einträge
/// für das Log-Fenster. Darf nie eine Exception nach außen werfen.
/// </summary>
internal static class Log
{
    private const long MaxFileSize = 1024 * 1024; // 1 MB, danach wird nach log.old.txt rotiert
    private const int MaxRecentEntries = 500;

    private static readonly object Sync = new();
    private static readonly Queue<LogEntry> Recent = new();
    private static string? _filePath;

    /// <summary>Wenn false, werden Debug-Einträge verworfen.</summary>
    public static volatile bool VerboseEnabled;

    /// <summary>Wird bei jedem neuen Eintrag ausgelöst (beliebiger Thread).</summary>
    public static event Action<LogEntry>? EntryAdded;

    public static void Initialize(string filePath)
    {
        lock (Sync)
        {
            _filePath = filePath;
        }
    }

    public static void Debug(string message) => Write(LogLevel.Debug, message);

    public static void Info(string message) => Write(LogLevel.Info, message);

    public static void Warn(string message) => Write(LogLevel.Warning, message);

    public static void Error(string message, Exception? ex = null) =>
        Write(LogLevel.Error, ex == null ? message : $"{message}: {ex.GetType().Name}: {ex.Message}");

    public static LogEntry[] GetRecent()
    {
        lock (Sync)
        {
            return Recent.ToArray();
        }
    }

    private static void Write(LogLevel level, string message)
    {
        if (level == LogLevel.Debug && !VerboseEnabled) return;

        var entry = new LogEntry(DateTime.Now, level, message);
        lock (Sync)
        {
            Recent.Enqueue(entry);
            while (Recent.Count > MaxRecentEntries) Recent.Dequeue();
            AppendToFile(entry);
        }

        try
        {
            EntryAdded?.Invoke(entry);
        }
        catch
        {
            // Ein fehlerhafter Abonnent darf das Logging nicht stören.
        }
    }

    private static void AppendToFile(LogEntry entry)
    {
        if (_filePath == null) return;
        try
        {
            var info = new FileInfo(_filePath);
            if (info.Exists && info.Length > MaxFileSize)
            {
                string old = Path.Combine(info.DirectoryName ?? string.Empty, "log.old.txt");
                File.Move(_filePath, old, overwrite: true);
            }
            File.AppendAllText(_filePath, entry + Environment.NewLine, Encoding.UTF8);
        }
        catch
        {
            // Log-Datei nicht schreibbar (z. B. gesperrt) – Eintrag bleibt im Speicher.
        }
    }
}
