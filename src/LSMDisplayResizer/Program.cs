using LSMDisplayResizer.Logging;

namespace LSMDisplayResizer;

internal static class Program
{
    private const string MutexName = @"Local\LSMDisplayResizer.SingleInstance";
    private const string ActivateEventName = @"Local\LSMDisplayResizer.Activate";

    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            SignalRunningInstance();
            return;
        }

        try
        {
            Directory.CreateDirectory(AppInfo.DataDirectory);
        }
        catch
        {
            // Ohne Datenordner läuft die App trotzdem (nur ohne Log-Datei / Speicherung).
        }
        Log.Initialize(AppInfo.LogFile);

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Log.Error("Unbehandelter Fehler (UI-Thread)", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Error("Unbehandelter Fehler", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error("Unbeobachteter Task-Fehler", e.Exception);
            e.SetObserved();
        };

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

        using var activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        using var context = new TrayApplicationContext(activateEvent);
        Application.Run(context);

        Log.Info("Beendet");
    }

    /// <summary>Eine zweite Instanz bittet die laufende, ihr Einstellungsfenster zu öffnen.</summary>
    private static void SignalRunningInstance()
    {
        try
        {
            using var evt = EventWaitHandle.OpenExisting(ActivateEventName);
            evt.Set();
        }
        catch
        {
            MessageBox.Show($"{AppInfo.Name} läuft bereits (Symbol im Infobereich unten rechts).",
                AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
