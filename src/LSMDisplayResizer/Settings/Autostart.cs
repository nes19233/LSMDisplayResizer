using LSMDisplayResizer.Logging;
using Microsoft.Win32;

namespace LSMDisplayResizer.Settings;

/// <summary>
/// "Mit Windows starten" über HKCU\Software\Microsoft\Windows\CurrentVersion\Run.
/// Eine Installation pro Rechner (MSI mit ALLUSERS=1) legt den Eintrag stattdessen unter HKLM an –
/// der gilt dann für alle Benutzer und wird hier nur erkannt, nicht verändert.
/// </summary>
internal static class Autostart
{
    /// <summary>Kommandozeilenargument der Autostart-Einträge (App und MSI).</summary>
    public const string Argument = "--autostart";

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    private static string Command => $"\"{Environment.ProcessPath}\" {Argument}";

    public static bool IsEnabled() => HasEntry(Registry.CurrentUser);

    /// <summary>true, wenn die Installation den Autostart für alle Benutzer eingerichtet hat (HKLM).</summary>
    public static bool IsEnabledForAllUsers() => HasEntry(Registry.LocalMachine);

    private static bool HasEntry(RegistryKey hive)
    {
        try
        {
            using var key = hive.OpenSubKey(RunKey);
            return key?.GetValue(AppInfo.Name) is string;
        }
        catch (Exception ex)
        {
            Log.Error("Autostart-Eintrag konnte nicht gelesen werden", ex);
            return false;
        }
    }

    /// <summary>Setzt oder entfernt den Eintrag. Ein vorhandener Eintrag wird auf den aktuellen EXE-Pfad aktualisiert.</summary>
    public static bool SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (enabled)
            {
                if (!string.Equals(key.GetValue(AppInfo.Name) as string, Command, StringComparison.OrdinalIgnoreCase))
                {
                    key.SetValue(AppInfo.Name, Command, RegistryValueKind.String);
                    Log.Info($"Autostart eingerichtet: {Command}");
                }
            }
            else if (key.GetValue(AppInfo.Name) != null)
            {
                key.DeleteValue(AppInfo.Name, throwOnMissingValue: false);
                Log.Info("Autostart entfernt");
            }
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("Autostart konnte nicht geändert werden", ex);
            return false;
        }
    }
}
