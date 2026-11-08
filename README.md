# LSM Display Resizer

[![Build](https://github.com/nes19233/LSMDisplayResizer/actions/workflows/build.yml/badge.svg)](https://github.com/nes19233/LSMDisplayResizer/actions/workflows/build.yml)
[![Lizenz: MIT](https://img.shields.io/badge/Lizenz-MIT-blue.svg)](LICENSE)

Kostenlose, quelloffene Windows-Tray-App, die die **Fenstergröße automatisch an den Zielmonitor anpasst**, sobald ein Fenster
auf einen anderen Monitor verschoben wird.

Hintergrund: Bei Monitoren mit unterschiedlicher Auflösung oder Skalierung wirkt ein Fenster nach dem Verschieben
oft zu groß oder zu klein und muss jedes Mal von Hand neu aufgezogen werden. LSM Display Resizer übernimmt das.

## Was die App macht

- Läuft unauffällig im Infobereich (Symbol unten rechts), ohne Fenster beim Start.
- Erkennt, wenn ein Fenster auf einem anderen Monitor landet:
  - beim Ziehen mit der Maus (nach dem Loslassen),
  - per **Win + Umschalt + Pfeil** oder wenn ein Programm sein Fenster selbst verschiebt.
- Passt dann Größe (und ggf. Position) an – nach einem von zwei Modi:

| Modus | Verhalten |
|---|---|
| **Prozentual** (Standard) | Größe und Position werden relativ zum Arbeitsbereich (Bildschirm ohne Taskleiste) übertragen. Beispiel: 60 % Breite auf Monitor A → 60 % Breite auf Monitor B. |
| **Feste Regeln** | Pro Programm (Prozessname) und Monitor wird eine Zielgröße festgelegt, wahlweise in Prozent des Arbeitsbereichs oder in Pixeln. Optional werden Fenster ohne Regel prozentual angepasst. |

Außerdem:

- Die App merkt sich die **letzte normale Größe** jedes Fensters. Wird ein Fenster maximiert, so auf einen anderen
  Monitor verschoben und erst dort verkleinert, wird diese gemerkte Größe auf den neuen Monitor umgerechnet.
- Das Fenster wird immer **vollständig im Arbeitsbereich** des Zielmonitors gehalten.
- Die unsichtbaren Fensterränder von Windows 10/11 werden herausgerechnet – Größen beziehen sich auf den sichtbaren Rahmen.
- Die App ist Per-Monitor-DPI-aware (V2), alle Größen sind echte Pixel.
- Monitore werden über ihre Geräte-ID wiedererkannt (nicht nur über „DISPLAY1“). Wird ein Monitor an einen anderen
  Anschluss gesteckt, greifen Regeln für dasselbe Monitormodell weiterhin.

### Diese Fenster werden nie angepasst

- maximierte, minimierte und per Snap angeordnete Fenster
- Fenster, die die **volle Bildschirmhöhe** nutzen: Windows zieht angedockte Fenster (z. B. linke Bildschirmhälfte)
  oft nur auf volle Höhe, statt sie zu maximieren. Sie werden wie maximierte behandelt – ihre Größe wird nicht gemerkt,
  es gilt weiter die letzte normale Größe (abschaltbar unter Allgemein → Verhalten)
- Vollbild-Fenster (z. B. Spiele, Videos)
- unsichtbare oder verborgene („cloaked“) Fenster, Tool-Fenster, Kind-Fenster, Fenster ohne Rahmen oder mit fester Größe
- Fenster der Windows-Shell (Taskleiste, Desktop …) und die eigenen Fenster der App
- Programme auf der **Ausschlussliste** (z. B. `game.exe`)

## Installation

Unter [Releases](../../releases) gibt es zwei Varianten. Voraussetzung jeweils Windows 10 oder 11, 64 Bit; eine
.NET-Installation ist nicht nötig.

| Datei | Wofür |
|---|---|
| `LSMDisplayResizer-x.y.z-win-x64.msi` | **Empfohlen.** Installer mit Startmenü-Eintrag, Autostart, Eintrag unter „Apps“ und Updates |
| `LSMDisplayResizer-x.y.z-win-x64.exe` | Portable Einzeldatei ohne Installation |

### MSI (empfohlen)

**Doppelklick** installiert für den angemeldeten Benutzer nach `%LocalAppData%\Programs\LSM Display Resizer\` –
**ohne Administratorrechte**. Der Installer richtet Startmenü-Verknüpfung und Autostart ein und bietet am Ende an,
die App gleich zu starten.

- **Update:** neueres MSI einfach installieren – die laufende App wird beendet und ersetzt, Einstellungen bleiben.
- **Deinstallieren:** Einstellungen → Apps → „LSM Display Resizer“. Einstellungen und Log in `%AppData%` bleiben erhalten.

**Für alle Benutzer / IT-Verteilung** (mit Administratorrechten, z. B. per Intune, SCCM oder Skript):

```powershell
msiexec /i LSMDisplayResizer-x.y.z-win-x64.msi ALLUSERS=1 /qn               # nach C:\Program Files, Autostart für alle
msiexec /i LSMDisplayResizer-x.y.z-win-x64.msi ALLUSERS=1 AUTOSTART=0 /qn   # dasselbe ohne Autostart
msiexec /i LSMDisplayResizer-x.y.z-win-x64.msi /qn                          # still, nur aktueller Benutzer
msiexec /x LSMDisplayResizer-x.y.z-win-x64.msi /qn                          # deinstallieren
```

Auch bei der Installation für alle Benutzer läuft die App selbst im Benutzerkontext (ohne Adminrechte).

### Portable EXE

1. Die EXE an einen festen Ort legen, z. B. `%LocalAppData%\Programs\LSM Display Resizer\`.
2. Starten – das Symbol erscheint im Infobereich (ggf. unter dem Pfeil „Ausgeblendete Symbole“).
3. Optional in den Einstellungen **„Mit Windows starten“** aktivieren.

Beim ersten Start einer heruntergeladenen Datei kann Windows SmartScreen warnen, weil sie nicht signiert ist
(„Weitere Informationen“ → „Trotzdem ausführen“).

### Einsatz im Benutzerkontext / in Domänennetzwerken

- Die App läuft als **normaler Benutzer** (`asInvoker`), braucht keine Administratorrechte.
- Einstellungen und Log liegen pro Benutzer in `%AppData%\LSM Display Resizer\` (wandert bei servergespeicherten Profilen mit).
- **Autostart** nutzt `…\CurrentVersion\Run` unter HKCU (pro Benutzer) bzw. beim MSI mit `ALLUSERS=1` unter HKLM
  (für alle Benutzer) – ohne Dienst oder geplante Aufgabe.
- **Signatur:** Zum Ausführen technisch nicht nötig. Ohne Signatur warnt aber SmartScreen bei heruntergeladenen Dateien.
  Wenn per Gruppenrichtlinie **AppLocker, WDAC oder Softwareeinschränkungen** aktiv sind, werden unsignierte Programme aus
  Benutzerordnern häufig blockiert. Am einfachsten: das MSI mit `ALLUSERS=1` nach `C:\Program Files` verteilen – die
  AppLocker-Standardregeln erlauben diesen Ordner. Sonst muss die IT eine Pfad- oder Hash-Regel freigeben oder die
  Dateien werden signiert.

## Bedienung

**Rechtsklick auf das Tray-Symbol:**

- **Aktiv / Pausiert** – automatische Anpassung ein- oder ausschalten (pausiert = graues Symbol mit rotem Punkt)
- **Einstellungen …** – auch per Doppelklick auf das Symbol oder durch erneutes Starten der EXE
- **Log anzeigen …** – letzte Aktionen, hilfreich bei der Fehlersuche
- **Beenden**

**Einstellungen:**

- *Allgemein*: Ein/Aus, Modus, Ablageposition beim Ziehen beibehalten, Tastenkürzel, Autostart, ausführliches Log
- *Feste Regeln*: Regeln hinzufügen, bearbeiten, löschen. Mit **„Aktuelles Fenster übernehmen“** bringt man ein Fenster
  in die gewünschte Größe auf dem Zielmonitor, wählt es aus der Liste – Programm, Monitor und Größe werden als Regel übernommen.
  Passen mehrere Regeln, gewinnt die genaueste (bestimmtes Programm vor `*`, bestimmter Monitor vor „Alle Monitore“).
- *Ausschlussliste*: Prozessnamen, deren Fenster nie angepasst werden.

**Tastenkürzel (optional):** Passt das aktive Fenster sofort an – mit passender fester Regel auf die Regelgröße,
sonst wird es nur vollständig in den Arbeitsbereich geholt. Standard: `Strg + Alt + R` (in den Einstellungen änderbar).

**Ablageposition beim Ziehen:** Standardmäßig bleibt beim Ziehen mit der Maus die linke obere Ecke dort, wo das Fenster
losgelassen wurde; nur die Größe ändert sich. Ohne diese Option wird – wie bei Win + Umschalt + Pfeil – auch die Position
relativ übertragen.

### Hintergrundbild über mehrere Monitore

Registerkarte **Hintergrundbild**: Ein großes Bild wird so auf alle Monitore verteilt, dass es nahtlos ineinander
übergeht, statt auf jedem Monitor doppelt zu erscheinen.

1. Bild auswählen. Es sollte mindestens so breit sein wie alle Monitore zusammen, die Vorschau warnt, wenn es zu klein ist.
2. Aufteilung wählen:
   - **Nach physischer Größe** (empfohlen): Die Größe der Bildflächen wird aus den Monitordaten (EDID) gelesen.
     So passen Übergänge auch bei unterschiedlich großen Monitoren oder unterschiedlicher Auflösung.
     *Rahmenkorrektur* (optional, Standard 0 mm = die Bilder stoßen direkt aneinander): Breite der Gehäuseränder
     zwischen den Bildflächen. Dann wird der Bildstreifen „hinter dem Rahmen“ ausgelassen, sodass schräge Linien über
     die Monitorgrenze in der Flucht weiterlaufen. Zwischen zwei Bildflächen **desselben** Monitors (Bild-neben-Bild,
     z. B. ein Ultrawide an zwei Eingängen) entfällt sie immer – erkannt an der gleichen Seriennummer in den Monitordaten.
     *Ausrichtung* und *Versatz* = wie die Monitore auf dem Tisch zueinander stehen. Alle Werte lassen sich anpassen.
     Gedacht für nebeneinander stehende Monitore.
   - **Nach Pixeln**: Aufteilung wie in den Windows-Anzeigeeinstellungen angeordnet, für jede Anordnung.
3. **Speichern und als Hintergrund setzen.** Die App erzeugt ein Gesamtbild und setzt es im Windows-Modus „Spannen“
   (nur für den angemeldeten Benutzer, ohne Adminrechte).

Optional wird das Bild **automatisch neu erstellt**, wenn sich die Monitoranordnung ändert (z. B. Laptop an der
Dockingstation). Beim ersten Setzen wird das bisherige Hintergrundbild gesichert und lässt sich mit
**„Vorherigen Hintergrund wiederherstellen“** zurückholen.

Grenzen: Gibt eine Gruppenrichtlinie das Hintergrundbild vor, überschreibt Windows es beim nächsten Anmelden (die App
weist darauf hin). Das Bild wird mittig zugeschnitten. Diashow und Windows-Blickpunkt werden durch das feste Bild ersetzt.

### Dateien

Alles liegt in `%AppData%\LSM Display Resizer\`:

| Datei | Inhalt |
|---|---|
| `settings.json` | Einstellungen (kann bei beendeter App auch von Hand bearbeitet werden) |
| `log.txt` | Log, max. ca. 1 MB, danach wird nach `log.old.txt` rotiert |
| `Hintergrund\` | erzeugtes Gesamtbild und Sicherung des vorherigen Hintergrundbilds |

Zusätzlich nur in `settings.json`: `debounceMilliseconds` (Standard 300) – Wartezeit nach einer Positionsänderung per
Tastatur/Programm, bevor geprüft wird.

## Bekannte Grenzen

- **Programme mit Administratorrechten:** Deren Fenster kann Windows-bedingt nur eine App anpassen, die selbst als
  Administrator läuft. Die App fordert diese Rechte bewusst nicht automatisch an. Bei Bedarf die EXE per Rechtsklick
  → „Als Administrator ausführen“ starten (der Autostart startet sie ohne Adminrechte). Im Log steht dann
  „Zugriff verweigert“.
- **Mindest-/Höchstgrößen:** Manche Programme erzwingen eigene Grenzen; dann wird die nächstmögliche Größe verwendet (siehe Log).
- **Neue Fenster:** In den ersten ca. 1,5 Sekunden nach dem Öffnen greift die App nicht ein, weil viele Programme ihre
  gespeicherte Position selbst wiederherstellen.
- **Anzeigeänderungen** (Laptop an-/abdocken, Monitor an-/abstecken, Standby): Windows räumt die Fenster zuerst selbst
  um, meldet aber nicht, wann es damit fertig ist. Die App erkennt das selbst: Jedes Fenster, das auf einem anderen
  Monitor gelandet ist und 1 Sekunde lang nicht mehr bewegt wird, passt sie ausgehend von seinem Zustand **vor** der
  Änderung an – Position und Größenverhältnis stimmen dann wieder, meist nach 2–3 Sekunden. Ruhen alle Fenster, prüft
  ein letzter Durchgang auch Fenster ohne eigene Bewegung (z. B. nur andere Auflösung). Spätestens nach der Höchstdauer
  (Standard 8 Sekunden, einstellbar) ist Schluss. Abschaltbar unter Allgemein → Verhalten.
- **Angeordnete (Snap-)Fenster** bleiben unangetastet, solange sie angeordnet sind.
- Im Modus *Prozentual* bewirkt das Tastenkürzel ohne passende feste Regel nur ein Einpassen in den Arbeitsbereich.
- Fenster ohne veränderbaren Rahmen (z. B. feste Dialoge) werden nicht angepasst. Warum ein Fenster ignoriert wurde,
  zeigt das Log mit der Option „Ausführliches Log“.

## Entwicklung

Voraussetzung: [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```powershell
dotnet build                     # Debug-Build
dotnet test                      # Unit-Tests (Größenberechnung, Regelauswahl)
dotnet run --project src/LSMDisplayResizer

# Release-Dateien lokal bauen
pwsh tools/Build-Release.ps1 -Version 1.2.3   # EXE + MSI nach artifacts/ (wie im Release)
```

Projektstruktur:

```
src/LSMDisplayResizer/
  Program.cs                  Einstieg, Einzelinstanz (Mutex), Fehlerbehandlung
  TrayApplicationContext.cs   Tray-Symbol und Menü
  Tracking/                   WinEvent-Hooks, Fensterfilter, Monitorwechsel-Erkennung
  Resizing/                   Größenberechnung, Regelauswahl, SetWindowPos
  Monitors/                   Monitorerkennung (EnumDisplayDevices, QueryDisplayConfig)
  Settings/                   Einstellungen (JSON), Autostart
  Wallpaper/                  Hintergrundbild über mehrere Monitore (EDID, Aufteilung, Setzen)
  UI/                         Einstellungen, Regel-Dialog, Fensterauswahl, Log-Fenster
  Interop/                    P/Invoke-Deklarationen
tests/LSMDisplayResizer.Tests/ xUnit-Tests
installer/                    MSI-Installer (WiX Toolset 5)
tools/                        Build-Release.ps1, Icons und Installer-Grafiken
```

### Release erstellen

Version taggen und pushen – der Workflow `.github/workflows/release.yml` baut MSI und EXE und legt das Release an:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

Die Version aus dem Tag erscheint in der App (Tray-Menü, Einstellungen, Log). Tags mit Bindestrich (z. B. `v1.1.0-beta1`)
werden als Vorabversion veröffentlicht.

## Fehler melden und mitmachen

Fehler und Wünsche bitte als [Issue](../../issues) melden – am besten mit Windows-Version, Monitoranordnung
(Auflösung, Skalierung) und dem Ausschnitt aus dem Log (`%AppData%\LSM Display Resizer\log.txt`, mit „Ausführliches Log“).
Pull Requests sind willkommen. Sicherheitslücken bitte nicht öffentlich melden, sondern wie in [SECURITY.md](SECURITY.md) beschrieben.

## Lizenz

LSM Display Resizer ist kostenlos und quelloffen unter der [MIT-Lizenz](LICENSE): Nutzung, Änderung und Weitergabe –
auch kommerziell – sind erlaubt, solange der Copyright- und Lizenzhinweis erhalten bleibt. Die Software wird ohne
Gewährleistung bereitgestellt.

Copyright © 2026 Sebastian Neumeyer
