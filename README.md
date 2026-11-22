# proxer-web

Ein inoffizielles, privates Web-Frontend für [proxer.me](https://proxer.me) mit moderner Oberfläche.

Das Projekt steht in keiner Verbindung zu Proxer.Me. Es nutzt keinen API-Key, sondern liest die normalen
Seiten mit dem eigenen Proxer-Account aus und bereitet sie neu auf. Gedacht ist es für den eigenen Gebrauch
auf dem eigenen Rechner, nicht als öffentlich betriebener Dienst.

## Funktionen

- Startseite mit beliebten Anime und News
- Anime- und Manga-Listen mit Sortierung, Filtern (Serien, Filme, OVA, Airing) und Anbieter-Katalogen
- Season-Übersicht und Anime-Kalender mit Countdown
- Detailseiten mit Episodenliste, Videowiedergabe über den eingebetteten Proxer-Player
- Eigene Liste mit Fortschritt
- Suche

## Aufbau

| Ordner | Inhalt |
|---|---|
| `backend/Proxer.Api` | ASP.NET Core (.NET 10). Hält die Proxer-Session, drosselt und cacht Anfragen, parst das HTML (AngleSharp) und bietet eine JSON-API unter `/api`. |
| `frontend` | React, TypeScript, Vite, Tailwind. Spricht nur mit der eigenen JSON-API. |

Das Backend schickt höchstens eine Anfrage alle 0,6 Sekunden an Proxer, loggt nur bei Bedarf ein und
merkt sich die Session in `.proxer-session.json`.

## Einrichten

Voraussetzungen: .NET 10 SDK und Node.js 24.

1. `.env.example` nach `.env.local` kopieren und die eigenen Proxer-Zugangsdaten eintragen.
2. Backend starten:
   ```
   cd backend/Proxer.Api
   dotnet run
   ```
3. Frontend starten:
   ```
   cd frontend
   npm install
   npm run dev
   ```
4. <http://localhost:5173> öffnen.

`.env.local` und `.proxer-session.json` enthalten Zugangsdaten bzw. die Session und sind in der
`.gitignore` ausgeschlossen. Beide Dateien nie einchecken.
