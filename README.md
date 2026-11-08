# LSM Display Resizer

Kleine Windows-Tray-App, die die **Fenstergröße automatisch an den Zielmonitor anpasst**, sobald ein Fenster
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

- Das Fenster wird immer **vollständig im Arbeitsbereich** des Zielmonitors gehalten.
- Die unsichtbaren Fensterränder von Windows 10/11 werden herausgerechnet – Größen beziehen sich auf den sichtbaren Rahmen.
- Die App ist Per-Monitor-DPI-aware (V2), alle Größen sind echte Pixel.
- Monitore werden über ihre Geräte-ID wiedererkannt (nicht nur über „DISPLAY1“). Wird ein Monitor an einen anderen
  Anschluss gesteckt, greifen Regeln für dasselbe Monitormodell weiterhin.

### Diese Fenster werden nie angepasst

- maximierte, minimierte und per Snap angeordnete Fenster
- Vollbild-Fenster (z. B. Spiele, Videos)
- unsichtbare oder verborgene („cloaked“) Fenster, Tool-Fenster, Kind-Fenster, Fenster ohne Rahmen oder mit fester Größe
- Fenster der Windows-Shell (Taskleiste, Desktop …) und die eigenen Fenster der App
- Programme auf der **Ausschlussliste** (z. B. `game.exe`)

## Installation

1. Unter [Releases](../../releases) die neueste `LSMDisplayResizer-x.y.z-win-x64.exe` herunterladen.
2. Die EXE an einen festen Ort legen, z. B. `C:\Tools\LSM Display Resizer\`.
3. Starten – das Symbol erscheint im Infobereich (ggf. unter dem Pfeil „Ausgeblendete Symbole“).
4. Optional in den Einstellungen **„Mit Windows starten“** aktivieren.

Die EXE ist eigenständig (self-contained), eine .NET-Installation ist nicht nötig. Voraussetzung: Windows 10 oder 11, 64 Bit.

Beim ersten Start einer heruntergeladenen EXE kann Windows SmartScreen warnen, weil die Datei nicht signiert ist
(„Weitere Informationen“ → „Trotzdem ausführen“).

### Einsatz im Benutzerkontext / in Domänennetzwerken

- Die App läuft als **normaler Benutzer** (`asInvoker`), braucht keine Administratorrechte und keine Installation.
- Einstellungen und Log liegen pro Benutzer in `%AppData%\LSM Display Resizer\` (wandert bei servergespeicherten Profilen mit).
- **Autostart** („Mit Windows starten“) nutzt `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` – nur für den
  angemeldeten Benutzer, ohne Adminrechte, ohne Dienst oder geplante Aufgabe.
- Empfohlener Ablageort ohne Adminrechte: `%LocalAppData%\Programs\LSM Display Resizer\LSMDisplayResizer.exe`.
- **Signatur:** Zum Ausführen technisch nicht nötig. Ohne Signatur warnt aber SmartScreen bei heruntergeladenen Dateien.
  Wenn per Gruppenrichtlinie **AppLocker, WDAC oder Softwareeinschränkungen** aktiv sind, werden unsignierte EXE-Dateien aus
  Benutzerordnern häufig blockiert. Dann muss die IT eine Pfad- oder Hash-Regel freigeben – oder die EXE wird signiert
  (Hash-Regeln müssen bei jeder neuen Version erneuert werden, eine Herausgeberregel für eine signierte EXE nicht).

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

### Dateien

Alles liegt in `%AppData%\LSM Display Resizer\`:

| Datei | Inhalt |
|---|---|
| `settings.json` | Einstellungen (kann bei beendeter App auch von Hand bearbeitet werden) |
| `log.txt` | Log, max. ca. 1 MB, danach wird nach `log.old.txt` rotiert |

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
- **Anzeigeänderungen:** Nach An-/Abstecken eines Monitors, Sperren/Entsperren oder Standby pausiert die Anpassung
  ca. 3 Sekunden, damit von Windows verschobene Fenster nicht verändert werden.
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

# Single-File-EXE wie im Release
dotnet publish src/LSMDisplayResizer -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
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
  UI/                         Einstellungen, Regel-Dialog, Fensterauswahl, Log-Fenster
  Interop/                    P/Invoke-Deklarationen
tests/LSMDisplayResizer.Tests/ xUnit-Tests
tools/New-AppIcon.ps1         erzeugt die App-Icons
```

### Release erstellen

Version taggen und pushen – der Workflow `.github/workflows/release.yml` baut die EXE und legt das Release an:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

Die Version aus dem Tag erscheint in der App (Tray-Menü, Einstellungen, Log). Tags mit Bindestrich (z. B. `v1.1.0-beta1`)
werden als Vorabversion veröffentlicht.
