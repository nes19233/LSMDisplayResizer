using LSMDisplayResizer.Updates;

namespace LSMDisplayResizer.Tests;

/// <summary>
/// Zeigt die Update-Dialogseiten wirklich an (und schließt sie sofort), weil Windows Regelverstöße – z. B. eigene
/// Schaltflächen neben Befehlslinks – erst beim Anzeigen meldet. Ohne Desktop (Dienst, CI) wird der Test übersprungen.
/// </summary>
public class UpdatePromptTests
{
    private static readonly ReleaseInfo Release = new("9.9.9", "https://example.test/release", new Dictionary<string, string>());

    [Theory]
    [InlineData(0)] // PerUser
    [InlineData(1)] // PerMachine
    [InlineData(2)] // Portable
    public void Pages_CanBeShownAndNavigated(int kindValue)
    {
        if (!Environment.UserInteractive || Environment.GetEnvironmentVariable("CI") != null) return;

        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                Application.EnableVisualStyles();
                // Wie in der App: Fortsetzungen nach await laufen auf dem UI-Thread des Dialogs.
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                var main = UpdatePrompt.BuildMainPage(Release, (InstallKind)kindValue);
                var progress = UpdatePrompt.BuildProgressPage(Release, out _);
                var error = UpdatePrompt.BuildErrorPage(Release, "Testfehler");

                // Ablauf wie beim echten Update: Hinweis → Fortschritt → Fehler → schließen.
                main.Page.Created += async (_, _) => { await Task.Yield(); main.Page.Navigate(progress); };
                progress.Created += async (_, _) => { await Task.Yield(); progress.Navigate(error); };
                error.Created += async (_, _) => { await Task.Yield(); error.BoundDialog!.Close(); };
                TaskDialog.ShowDialog(main.Page);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(20)), "Dialog wurde nicht geschlossen");
        Assert.Null(failure);
    }
}
