using System.Reflection;
using System.Security.Principal;

namespace LSMDisplayResizer;

/// <summary>Name, Version und Dateipfade der App.</summary>
internal static class AppInfo
{
    public const string Name = "LSM Display Resizer";

    public static string Version { get; } = ReadVersion();

    public static string DataDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Name);

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public static string LogFile => Path.Combine(DataDirectory, "log.txt");

    public static bool IsElevated { get; } = ReadIsElevated();

    private static string ReadVersion()
    {
        var asm = Assembly.GetExecutingAssembly();
        string? info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            int plus = info.IndexOf('+');
            return plus >= 0 ? info[..plus] : info;
        }
        return asm.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private static bool ReadIsElevated()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
