namespace LSMDisplayResizer.Localization;

/// <summary>Englische Übersetzungen. Schlüssel = deutscher Text genau so, wie er im Code steht.</summary>
internal static class English
{
    public static readonly Dictionary<string, string> Texts = new(StringComparer.Ordinal)
    {
        // --- Tray ---
        ["Aktiv"] = "Active",
        ["Pausiert"] = "Paused",
        ["Einstellungen …"] = "Settings …",
        ["Log anzeigen …"] = "Show log …",
        ["Beenden"] = "Exit",
        ["{0} – aktiv"] = "{0} – active",
        ["{0} – pausiert"] = "{0} – paused",
        ["Läuft im Hintergrund. Rechtsklick auf das Symbol für Einstellungen."] = "Running in the background. Right-click the icon for settings.",
        ["{0} läuft bereits (Symbol im Infobereich unten rechts)."] = "{0} is already running (icon in the notification area, bottom right).",

        // --- Einstellungen: Rahmen ---
        ["{0} – Einstellungen"] = "{0} – Settings",
        ["Allgemein"] = "General",
        ["Feste Regeln"] = "Fixed rules",
        ["Ausschlussliste"] = "Exclusions",
        ["Hintergrundbild"] = "Wallpaper",
        ["OK"] = "OK",
        ["Abbrechen"] = "Cancel",
        ["Übernehmen"] = "Apply",
        ["Version {0}"] = "Version {0}",
        ["MIT-Lizenz · GitHub"] = "MIT license · GitHub",

        // --- Allgemein ---
        ["Automatische Anpassung aktiv"] = "Automatic adjustment enabled",
        ["Modus"] = "Mode",
        ["Prozentual"] = "Percentage",
        ["Höhe mitskalieren:"] = "Scale height:",
        ["Fenster ohne passende Regel prozentual anpassen"] = "Adjust windows without a matching rule by percentage",
        ["Verhalten"] = "Behavior",
        ["Beim Ziehen mit der Maus die Ablageposition beibehalten"] = "Keep drop position when dragging with the mouse",
        ["Fenster mit voller Bildschirmhöhe wie angedockte behandeln"] = "Treat full-height windows like snapped windows",
        ["Fensteranordnung pro Monitorkonstellation merken und wiederherstellen"] = "Remember and restore window layout per monitor setup",
        ["Nach An-/Abdocken und Monitorwechseln Fenster anpassen, höchstens warten:"] = "Adjust windows after docking/undocking and monitor changes, wait at most:",
        ["Sekunden"] = "seconds",
        ["Tastenkürzel für das aktive Fenster:"] = "Hotkey for the active window:",
        ["Monitortrennlinie"] = "Monitor separator line",
        ["Feine Linie zwischen den Monitoren anzeigen"] = "Show a thin line between monitors",
        ["Farbe:"] = "Color:",
        ["Stärke:"] = "Thickness:",
        ["Deckkraft:"] = "Opacity:",
        ["System"] = "System",
        ["Mit Windows starten"] = "Start with Windows",
        ["Mit Windows starten (von der Installation für alle Benutzer eingerichtet)"] = "Start with Windows (set up for all users by the installation)",
        ["Ausführliches Log (auch ignorierte Fenster protokollieren)"] = "Verbose log (also log ignored windows)",
        ["Sprache:"] = "Language:",
        ["Automatisch (Windows-Sprache)"] = "Automatic (Windows language)",
        ["Log anzeigen … "] = "Show log …",
        ["Modus ist „Prozentual“: Die Regeln werden derzeit nur für das Tastenkürzel verwendet."] = "Mode is “Percentage”: rules are currently only used for the hotkey.",
        ["Aktuell: läuft als Administrator."] = "Currently running as administrator.",
        ["Aktuell: läuft ohne Administratorrechte."] = "Currently running without administrator rights.",
        ["(keins)"] = "(none)",
        ["Mit Strg, Alt oder Umschalt kombinieren"] = "Combine with Ctrl, Alt or Shift",

        // --- Tastennamen ---
        ["Strg"] = "Ctrl",
        ["Alt"] = "Alt",
        ["Umschalt"] = "Shift",
        ["Leertaste"] = "Space",
        ["Eingabe"] = "Enter",
        ["Einfg"] = "Insert",
        ["Entf"] = "Delete",
        ["Pos1"] = "Home",
        ["Ende"] = "End",
        ["Bild auf"] = "Page Up",
        ["Bild ab"] = "Page Down",
        ["Pfeil links"] = "Left",
        ["Pfeil rechts"] = "Right",
        ["Pfeil oben"] = "Up",
        ["Pfeil unten"] = "Down",
        ["Num "] = "Num ",

        // --- Feste Regeln ---
        ["Programm"] = "Program",
        ["Monitor"] = "Monitor",
        ["Breite"] = "Width",
        ["Höhe"] = "Height",
        ["Einheit"] = "Unit",
        ["Hinzufügen …"] = "Add …",
        ["Bearbeiten …"] = "Edit …",
        ["Löschen"] = "Delete",
        ["Aktuelles Fenster übernehmen …"] = "Use current window …",
        ["Alle Monitore"] = "All monitors",
        [" (nicht verbunden)"] = " (not connected)",
        ["Neue Regel"] = "New rule",
        ["Neue Regel aus Fenster"] = "New rule from window",
        ["Regel bearbeiten"] = "Edit rule",
        ["Für {0} auf „{1}“ gibt es schon eine Regel. Ersetzen?"] = "There is already a rule for {0} on “{1}”. Replace it?",
        ["Regel für {0} auf „{1}“ löschen?"] = "Delete the rule for {0} on “{1}”?",
        ["Bitte eine Tastenkombination festlegen oder das Tastenkürzel ausschalten."] = "Please set a key combination or turn the hotkey off.",

        // --- Regel-Dialog ---
        ["Programm:"] = "Program:",
        ["Monitor:"] = "Monitor:",
        ["Breite:"] = "Width:",
        ["Höhe:"] = "Height:",
        ["Einheit:"] = "Unit:",
        ["Prozent (%)"] = "Percent (%)",
        ["Pixel (px)"] = "Pixels (px)",
        ["Prozessname, z. B. chrome.exe – oder * für alle Programme."] = "Process name, e.g. chrome.exe – or * for all programs.",
        ["Bezieht sich auf den sichtbaren Fensterrahmen. % = Anteil am Arbeitsbereich (ohne Taskleiste)."] = "Refers to the visible window frame. % = share of the work area (without taskbar).",
        ["Pixelgröße hängt vom jeweiligen Monitor ab."] = "The pixel size depends on the monitor.",
        ["Ergibt {0} × {1} px (Arbeitsbereich {2} × {3}){4}"] = "Results in {0} × {1} px (work area {2} × {3}){4}",
        [" – wird auf den Arbeitsbereich begrenzt"] = " – limited to the work area",
        ["Bitte einen Programmnamen eingeben (z. B. chrome.exe)."] = "Please enter a program name (e.g. chrome.exe).",

        // --- Fensterauswahl ---
        ["Fenster auswählen"] = "Select window",
        ["Fenstertitel"] = "Window title",
        ["Größe (px)"] = "Size (px)",
        ["Größe (%)"] = "Size (%)",
        ["Aktualisieren"] = "Refresh",
        ["(ohne Titel)"] = "(untitled)",
        ["Das Fenster existiert nicht mehr."] = "The window no longer exists.",

        // --- Ausschlussliste ---
        ["Hinzufügen"] = "Add",
        ["Entfernen"] = "Remove",

        // --- Hintergrundbild ---
        ["Bild"] = "Image",
        ["Durchsuchen …"] = "Browse …",
        ["Bilddatei auswählen …"] = "Select image file …",
        ["Aufteilung"] = "Layout",
        ["Nach physischer Größe der Monitore (empfohlen)"] = "By physical monitor size (recommended)",
        ["Nach Pixeln (Anordnung aus den Windows-Anzeigeeinstellungen)"] = "By pixels (arrangement from Windows display settings)",
        ["Rahmenkorrektur (optional):"] = "Bezel correction (optional):",
        ["    Ausrichtung:"] = "    Alignment:",
        ["Oben bündig"] = "Top aligned",
        ["Mittig"] = "Centered",
        ["Unten bündig"] = "Bottom aligned",
        ["Breite (mm)"] = "Width (mm)",
        ["Höhe (mm)"] = "Height (mm)",
        ["Diagonale"] = "Diagonal",
        ["Versatz (mm)"] = "Offset (mm)",
        ["Quelle"] = "Source",
        ["aus Monitor"] = "from monitor",
        ["geschätzt"] = "estimated",
        ["angepasst"] = "custom",
        ["Größen neu auslesen"] = "Re-read sizes",
        ["Sichtbare Bildfläche ohne Rahmen. Versatz: positiv = Monitor steht tiefer."] = "Visible display area without bezel. Offset: positive = monitor sits lower.",
        ["Vorschau"] = "Preview",
        ["Automatisch neu erstellen, wenn sich die Monitoranordnung ändert"] = "Recreate automatically when the monitor arrangement changes",
        ["Übernehmen und als Hintergrund setzen"] = "Apply and set as wallpaper",
        ["Vorherigen Hintergrund wiederherstellen"] = "Restore previous wallpaper",
        ["Hintergrundbild auswählen"] = "Select wallpaper image",
        ["Bilder"] = "Images",
        ["Alle Dateien"] = "All files",
        ["Kein Bild ausgewählt – die Funktion ist nicht aktiv."] = "No image selected – the feature is not active.",
        ["Datei nicht gefunden."] = "File not found.",
        ["Bild wird geladen …"] = "Loading image …",
        ["Bildgröße: {0} × {1} px"] = "Image size: {0} × {1} px",
        ["Bild kann nicht geladen werden: {0}"] = "Cannot load image: {0}",
        ["Die Bildauflösung reicht für alle Monitore."] = "The image resolution is sufficient for all monitors.",
        ["Das Bild ist zu klein und wird bis zu {0:0.0}-fach vergrößert (unscharf). Empfohlen: mindestens {1} × {2} px."] = "The image is too small and is enlarged up to {0:0.0}× (blurry). Recommended: at least {1} × {2} px.",
        ["{0} sind Bildflächen desselben Monitors (Bild-neben-Bild) – dazwischen wird kein Rahmen abgezogen."] = "{0} are display areas of the same monitor (picture-by-picture) – no bezel is subtracted between them.",
        ["{0} und {1}"] = "{0} and {1}",
        ["Wert zwischen 50 und 5000 mm"] = "Value between 50 and 5000 mm",
        ["Versatz zwischen -2000 und 2000 mm"] = "Offset between -2000 and 2000 mm",
        ["Bitte zuerst eine vorhandene Bilddatei auswählen."] = "Please select an existing image file first.",
        ["Hintergrundbild wird erstellt …"] = "Creating wallpaper …",
        ["Hintergrundbild konnte nicht gesetzt werden: {0}"] = "Could not set wallpaper: {0}",
        ["Vorheriges Hintergrundbild wiederhergestellt. Die automatische Aktualisierung wurde ausgeschaltet."] = "Previous wallpaper restored. Automatic updating has been turned off.",
        ["Es ist kein gesichertes Hintergrundbild vorhanden."] = "There is no saved wallpaper.",
        ["Wiederherstellen fehlgeschlagen: {0}"] = "Restore failed: {0}",
        ["Das Hintergrundbild wird gerade erstellt …"] = "The wallpaper is currently being created …",
        ["Hintergrundbild gesetzt: {0} × {1} px, {2}, {3} Monitor(e)."] = "Wallpaper set: {0} × {1} px, {2}, {3} monitor(s).",
        ["nach physischer Größe"] = "by physical size",
        ["nach Pixeln"] = "by pixels",
        ["Das Bild wird bis zu {0:0.0}-fach vergrößert (unscharf); empfohlen sind mindestens {1} × {2} px."] = "The image is enlarged up to {0:0.0}× (blurry); at least {1} × {2} px is recommended.",
        ["Windows hat das Hintergrundbild nicht übernommen – der bisherige Hintergrund bleibt. Häufige Ursachen: eine Gruppenrichtlinie, oder die App wurde aus einer anderen (Store-)App heraus gestartet – dann bitte die App beenden und über das Startmenü neu starten."] = "Windows did not accept the wallpaper – the previous wallpaper is kept. Common causes: a group policy, or the app was started from another (Store) app – in that case exit the app and start it again from the Start menu.",
        ["Eine Gruppenrichtlinie gibt das Hintergrundbild vor – es wird beim nächsten Anmelden überschrieben."] = "A group policy sets the wallpaper – it will be overwritten at the next sign-in.",
        ["Eine Gruppenrichtlinie verbietet das Ändern des Hintergrundbilds."] = "A group policy prevents changing the wallpaper.",
        ["Die Aufteilung nach physischer Größe unterstützt nur nebeneinander stehende Monitore. Es wird nach Pixeln aufgeteilt."] = "Layout by physical size only supports side-by-side monitors. The image is split by pixels.",

        // --- Log-Fenster ---
        ["{0} {1} – Log"] = "{0} {1} – Log",
        ["Logdatei öffnen"] = "Open log file",
        ["Ordner öffnen"] = "Open folder",
        ["Anzeige leeren"] = "Clear view",
        ["Schließen"] = "Close",
        ["{0} konnte nicht geöffnet werden:\n{1}"] = "{0} could not be opened:\n{1}",

        // --- Meldungen ---
        ["Der Autostart-Eintrag konnte nicht geändert werden (Details im Log)."] = "The autostart entry could not be changed (see log for details).",
        ["Die Einstellungen konnten nicht gespeichert werden (Details im Log). Sie gelten bis zum Beenden."] = "The settings could not be saved (see log for details). They apply until the app exits.",
        ["Das Tastenkürzel {0} ist bereits von einem anderen Programm belegt. Bitte ein anderes wählen."] = "The hotkey {0} is already used by another program. Please choose a different one.",
        [", Hauptmonitor"] = ", primary",
        ["Auflösung"] = "Resolution",

        // --- Lange Hinweistexte (genau so zusammengesetzt wie im Code) ---
        ["Hinweis: Fenster von Programmen, die mit Administratorrechten laufen, kann die App nur anpassen, wenn sie selbst als Administrator gestartet wurde (Rechtsklick auf die EXE → „Als Administrator ausführen“)."] =
            "Note: windows of programs running with administrator rights can only be adjusted if the app itself was started as administrator (right-click the EXE → “Run as administrator”).",
        ["Wird ein Fenster auf einen anderen Monitor verschoben, passt die App seine Größe an. Maximierte, minimierte und Vollbild-Fenster bleiben unverändert."] =
            "When a window is moved to another monitor, the app adjusts its size. Maximized, minimized and full-screen windows stay unchanged.",
        ["Größe und Position relativ zum Arbeitsbereich (ohne Taskleiste) übertragen – z. B. 60 % Breite auf Monitor A → 60 % Breite auf Monitor B."] =
            "Transfer size and position relative to the work area (without taskbar) – e.g. 60 % width on monitor A → 60 % width on monitor B.",
        ["100 % = Höhe wächst/schrumpft voll mit dem Arbeitsbereich, 0 % = sichtbare Höhe bleibt gleich. Standard 67 %: Die Höhe passt sich etwa zu zwei Dritteln an. Unterschiedliche Skalierung (z. B. Laptop 200 %, Monitor 100 %) wird berücksichtigt. Die Breite skaliert immer voll."] =
            "100 % = height grows/shrinks fully with the work area, 0 % = visible height stays the same. Default 67 %: the height adapts by about two thirds. Different display scaling (e.g. laptop 200 %, monitor 100 %) is taken into account. The width always scales fully.",
        ["Zielgröße pro Programm und Monitor, siehe Registerkarte „Feste Regeln“."] =
            "Target size per program and monitor, see the “Fixed rules” tab.",
        ["Nur die Größe ändert sich, die linke obere Ecke bleibt, wo das Fenster losgelassen wurde. Bei Win+Umschalt+Pfeil wird die Position immer relativ übertragen."] =
            "Only the size changes; the top-left corner stays where the window was dropped. With Win+Shift+Arrow the position is always transferred relatively.",
        ["Windows zieht angedockte Fenster (z. B. linke Bildschirmhälfte) oft nur auf volle Höhe, statt sie zu maximieren. Solche Fenster werden dann wie maximierte behandelt: Ihre Größe wird nicht gemerkt, es gilt weiter die letzte normale Größe."] =
            "Windows often only stretches snapped windows (e.g. left half of the screen) to full height instead of maximizing them. Such windows are then treated like maximized ones: their size is not remembered, the last normal size still applies.",
        ["Wechselt man z. B. vom Laptop allein wieder auf Laptop + zwei Monitore, kommt jedes Fenster auf den Monitor zurück, auf dem es in dieser Konstellation zuletzt war – normale Fenster mit Position und Größe, maximierte bleiben maximiert. Monitore werden an ihrer Seriennummer erkannt. Gilt für Fenster, die zwischendurch geöffnet bleiben."] =
            "When switching e.g. from the laptop alone back to laptop + two monitors, every window returns to the monitor it was last on in that setup – normal windows with position and size, maximized ones stay maximized. Monitors are recognized by their serial number. Applies to windows that stay open in between.",
        ["Wird z. B. der Laptop von der Dockingstation abgezogen, räumt Windows die Fenster auf die verbliebenen Monitore um. Sobald ein Fenster nicht mehr bewegt wird, passt die App es ausgehend von seinem Zustand vorher an, sodass Position und Größenverhältnis wieder stimmen – meist nach 2–3 Sekunden. Die Höchstdauer greift nur, wenn Windows nicht zur Ruhe kommt."] =
            "When e.g. the laptop is undocked, Windows moves the windows to the remaining monitors. As soon as a window stops moving, the app adjusts it based on its previous state so that position and proportions are right again – usually after 2–3 seconds. The maximum only applies if Windows does not settle.",
        ["In das Feld klicken und die Tastenkombination drücken (z. B. Strg + Alt + R). Wendet eine passende feste Regel an, sonst wird das Fenster in den Arbeitsbereich eingepasst."] =
            "Click into the field and press the key combination (e.g. Ctrl + Alt + R). Applies a matching fixed rule, otherwise fits the window into the work area.",
        ["Zeigt bei mehreren Monitoren eine dünne Linie an der Grenze, damit man sieht, wo ein Bildschirm endet – hilfreich z. B. bei einem geteilten Ultrawide-Monitor. Die Linie lässt Mausklicks durch und wird bei Vollbildprogrammen ausgeblendet."] =
            "With multiple monitors, shows a thin line at the border so you can see where a screen ends – helpful e.g. with a split ultrawide monitor. The line lets mouse clicks through and is hidden for full-screen programs.",
        ["Breite und Höhe beziehen sich auf den sichtbaren Fensterrahmen; % = Anteil am Arbeitsbereich des Monitors. Passen mehrere Regeln, gewinnt die genaueste (bestimmtes Programm und Monitor vor „*“ bzw. „Alle Monitore“)."] =
            "Width and height refer to the visible window frame; % = share of the monitor’s work area. If several rules match, the most specific one wins (a specific program and monitor before “*” or “All monitors”).",
        ["Fenster dieser Programme werden nie angepasst (Prozessname, z. B. game.exe). Die Liste schlägt Programme vor, die gerade ein Fenster geöffnet haben."] =
            "Windows of these programs are never adjusted (process name, e.g. game.exe). The list suggests programs that currently have a window open.",
        ["Fenster in die gewünschte Größe bringen, auf den Zielmonitor schieben und hier auswählen. Programm, Monitor und aktuelle Größe werden als neue Regel übernommen."] =
            "Bring the window to the desired size, move it to the target monitor and select it here. Program, monitor and current size are taken over as a new rule.",
        ["Ein großes Bild wird so auf alle Monitore verteilt, dass es nahtlos ineinander übergeht – statt auf jedem Monitor doppelt zu erscheinen. Das Bild sollte dafür mindestens so breit sein wie alle Monitore zusammen."] =
            "A large image is spread across all monitors so that it flows seamlessly – instead of appearing twice. The image should be at least as wide as all monitors together.",
        ["Übergänge stimmen auch bei unterschiedlich großen Monitoren. Die Bildflächen werden aus den Monitordaten gelesen und können unten angepasst werden. Für nebeneinander stehende Monitore."] =
            "Transitions are correct even with monitors of different sizes. The display areas are read from the monitor data and can be adjusted below. For side-by-side monitors.",
        ["Passt bei gleich großen Monitoren mit gleicher Auflösung; funktioniert für jede Anordnung."] =
            "Suitable for monitors of the same size and resolution; works for any arrangement.",
        ["Rahmenkorrektur: Standard 0 mm – die Bilder stoßen direkt aneinander. Optional lässt sich die Breite der Gehäuseränder angeben; dann wird der Bildstreifen „hinter dem Rahmen“ ausgelassen, sodass schräge Linien über die Monitorgrenze in der Flucht weiterlaufen. Zwischen Hälften desselben Monitors (Bild-neben-Bild) entfällt sie immer."] =
            "Bezel correction: default 0 mm – the images meet directly. Optionally enter the width of the bezels; the strip of the image “behind the bezel” is then skipped so that diagonal lines continue in line across the monitor border. Between halves of the same monitor (picture-by-picture) it never applies.",

        // --- Updates ---
        ["Nach Updates suchen …"] = "Check for updates …",
        ["Update auf Version {0} installieren …"] = "Install update to version {0} …",
        ["Version {0} ist verfügbar. Zum Aktualisieren hier klicken."] = "Version {0} is available. Click here to update.",
        ["Einmal täglich nach Updates suchen"] = "Check for updates once a day",
        ["Jetzt suchen …"] = "Check now …",
        ["Nach Updates suchen (von der Installation abgeschaltet)"] = "Check for updates (turned off by the installation)",
        ["Fragt bei GitHub nach der neuesten Version (es werden keine Daten über Sie gesendet). Bei einer Installation für den eigenen Benutzer lässt sich das Update mit einem Klick installieren – die Datei wird vorher anhand ihrer Prüfsumme kontrolliert."] =
            "Asks GitHub for the latest version (no data about you is sent). With an installation for your own user account, the update installs with one click – the file is verified by its checksum first.",
        ["Update-Prüfung fehlgeschlagen"] = "Update check failed",
        ["GitHub ist nicht erreichbar: {0}"] = "GitHub cannot be reached: {0}",
        ["Die App ist aktuell"] = "The app is up to date",
        ["Version {0} ist die neueste Version."] = "Version {0} is the latest version.",
        ["Jetzt aktualisieren"] = "Update now",
        ["Herunterladen, Prüfsumme prüfen, installieren und neu starten (dauert etwa eine Minute)."] =
            "Download, verify the checksum, install and restart (takes about a minute).",
        ["Release-Seite öffnen"] = "Open release page",
        ["Später"] = "Later",
        ["Änderungen ansehen und Dateien selbst herunterladen."] = "See what changed and download the files yourself.",
        ["Erinnerung bei der nächsten täglichen Prüfung."] = "Reminder at the next daily check.",
        ["Installiert ist Version {0}."] = "Installed version: {0}.",
        ["Diese Installation gilt für alle Benutzer des Computers und wird von der IT bzw. einem Administrator aktualisiert."] =
            "This installation is for all users of the computer and is updated by IT or an administrator.",
        ["Diese EXE wurde nicht mit dem Installer installiert – die neue Version gibt es auf der Release-Seite."] =
            "This EXE was not installed with the installer – get the new version from the release page.",
        ["Die Einstellungen bleiben erhalten."] = "Your settings are kept.",
        ["Version {0} ist verfügbar"] = "Version {0} is available",
        ["Version {0} wird heruntergeladen …"] = "Downloading version {0} …",
        ["Danach wird die Datei geprüft und die Installation gestartet. Die App startet anschließend neu."] =
            "The file is then verified and the installation starts. The app restarts afterwards.",
        ["Update fehlgeschlagen"] = "Update failed",
        ["Fehler: {0}"] = "Error: {0}",
        ["Im Release fehlt die Datei {0}."] = "The release is missing the file {0}.",
        ["Für {0} ist keine Prüfsumme hinterlegt."] = "No checksum is listed for {0}.",
        ["Die heruntergeladene Datei ist beschädigt oder wurde verändert (Prüfsumme stimmt nicht). Sie wurde verworfen."] =
            "The downloaded file is damaged or was modified (checksum mismatch). It was discarded.",
    };
}
