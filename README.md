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
  Kommentaren und Musik
- Videowiedergabe über den eingebetteten Proxer-Player
- Eigene Liste: Fortschritt und Status ändern, Einträge entfernen, Lesezeichen verwalten
- Vermerken, Abschließen, Favorisieren, Abonnieren und „als gesehen markieren“ direkt aus der App
- Forum (lesen), Nachrichten (lesen und antworten), Benachrichtigungen, Profile
- Seiten zu Charakteren, Personen, Unternehmen und Sub-/Scanlationgruppen
- Suche

Manga-Kapitel öffnen auf proxer.me: Proxer schützt die Kapitelseiten mit einer Bot-Prüfung, die die App
bewusst nicht umgeht.

## Aufbau

| Ordner | Inhalt |
|---|---|
| `backend/Proxer.Api` | ASP.NET Core (.NET 10). Hält die Proxer-Session, drosselt und cacht Anfragen, parst das HTML (AngleSharp) und bietet eine JSON-API unter `/api`. |
| `frontend` | React, TypeScript, Vite, Tailwind. Spricht nur mit der eigenen JSON-API. |

Das Backend schickt höchstens eine Anfrage alle 0,6 Sekunden an Proxer. Beim Anmelden reicht es
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

Nach vielen Anmeldungen in kurzer Zeit zeigt Proxer auf seiner Login-Seite ein Captcha. Die App kann und
soll es nicht lösen; die Login-Seite der App meldet dann, dass ein Captcha verlangt wird. Die Sperre hängt
an der Internetadresse, über die das Backend Proxer erreicht:

1. <https://proxer.me/login> in einem Browser öffnen, der über dieselbe Verbindung wie das Backend geht.
2. Das Captcha lösen und auf „Weiter“ klicken. Eine Anmeldung dort ist nicht nötig.
3. In der App erneut anmelden.

Läuft das Backend in WSL, nutzt es oft eine andere Adresse (nur IPv4) als der Windows-Browser (IPv6), der
dann gar kein Captcha sieht. In dem Fall den Browser aus WSL heraus starten oder das Backend direkt unter
Windows ausführen.

`.proxer-session.json` enthält die Session und ist in der `.gitignore` ausgeschlossen. Die Datei nie
einchecken und nicht weitergeben: Wer sie hat, ist ohne Passwort im Account angemeldet.
