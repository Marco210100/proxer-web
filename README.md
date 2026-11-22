# proxer-web

Ein inoffizielles, privates Web-Frontend für [proxer.me](https://proxer.me) mit moderner Oberfläche.

Das Projekt steht in keiner Verbindung zu Proxer.Me. Es nutzt keinen API-Key, sondern liest die normalen
Seiten mit dem eigenen Proxer-Account aus und bereitet sie neu auf. Gedacht ist es für den eigenen Gebrauch
auf dem eigenen Rechner, nicht als öffentlich betriebener Dienst.

## Funktionen

- Anmelden und Abmelden mit dem eigenen Proxer-Account (auch mit Zwei-Faktor-Code)
- Startseite mit beliebten Anime und News
- Startseite zusätzlich mit den zuletzt hochgeladenen Episoden
- Anime-, Manga- und Novel-Listen mit Sortierung, Buchstabenfilter, Medienfiltern und Anbieter-Katalogen
- Season-Übersicht und Anime-Kalender mit Countdown
- Detailseiten mit Tags, Charakteren, Personen, Unternehmen, Episodenliste, Verbindungen, Empfehlungen,
  Kommentaren, Musik, Diskussionen, Bildern, Artikeln, Upload-Verlauf und eigener Chronik
- Videowiedergabe über den eingebetteten Proxer-Player
- Eigene Liste: Fortschritt und Status ändern, Einträge entfernen, Lesezeichen verwalten
- Vermerken, Abschließen, Favorisieren, Abonnieren und „als gesehen markieren“ direkt aus der App
- Forum (lesen), Nachrichten (lesen und antworten), Benachrichtigungen, Profile
- Seiten und Übersichten zu Charakteren, Personen, Unternehmen und Sub-/Scanlationgruppen
- Suche mit Filtern für Typ, Sprache, Status, Sortierung und Genres (ein- und ausschließen)

Manga-Kapitel liest die App im eigenen Reader. Vor dem ersten Kapitel verlangt Proxer eine Bot-Prüfung pro
Sitzung: Die App öffnet dafür ein Browserfenster mit ihrer eigenen Sitzung, in dem man die Prüfung selbst
besteht. Dafür braucht es ein installiertes Chrome, Chromium oder Edge; ein anderer Pfad lässt sich über die
Umgebungsvariable `PROXER_BROWSER` angeben.

## Aufbau

| Ordner | Inhalt |
|---|---|
| `backend/Proxer.Api` | ASP.NET Core (.NET 10). Hält die Proxer-Session, drosselt und cacht Anfragen, parst das HTML (AngleSharp) und bietet eine JSON-API unter `/api`. |
| `frontend` | React, TypeScript, Vite, Tailwind. Spricht nur mit der eigenen JSON-API. |

Das Backend schickt höchstens eine Anfrage alle 1,2 Sekunden an Proxer. Beim Anmelden reicht es
Benutzername und Passwort einmalig an Proxer weiter und merkt sich danach nur die Session in
`.proxer-session.json`; das Passwort wird nicht gespeichert.

## Einrichten

Voraussetzungen: .NET 10 SDK und Node.js 24.

1. Backend starten:
   ```
   cd backend/Proxer.Api
   dotnet run
   ```
2. Frontend starten:
   ```
   cd frontend
   npm install
   npm run dev
   ```
3. <http://localhost:5173> öffnen und mit dem Proxer-Account anmelden.

## Wenn Proxer ein Captcha verlangt

Nach vielen Anmeldungen oder Seitenaufrufen in kurzer Zeit, etwa beim schnellen Durchblättern vieler
Kapitel, zeigt Proxer statt der Seite ein Captcha. Die App löst es nicht selbst. Sie erkennt die Captcha-Seite,
pausiert ihre Anfragen für zwei Minuten und zeigt den Knopf „Fenster öffnen“: Er öffnet die betroffene Seite in
einem Browserfenster mit der Sitzung der App. Dort das Captcha lösen, das Fenster schließen und die Seite in
der App neu laden.

Ohne den Knopf geht es auch von Hand: Die in der Fehlermeldung genannte Adresse in einem Browser öffnen, der
über dieselbe Verbindung wie das Backend geht, und das Captcha dort lösen.

Läuft das Backend in WSL, nutzt es oft eine andere Adresse (nur IPv4) als der Windows-Browser (IPv6), der
dann gar kein Captcha sieht. In dem Fall den Browser aus WSL heraus starten oder das Backend direkt unter
Windows ausführen.

Um das Captcha zu vermeiden, schickt das Backend höchstens eine Anfrage alle 1,2 Sekunden an Proxer.

`.proxer-session.json` enthält die Session und ist in der `.gitignore` ausgeschlossen. Die Datei nie
einchecken und nicht weitergeben: Wer sie hat, ist ohne Passwort im Account angemeldet.
