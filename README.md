# 7 Days to Die Server Manager

A native Windows desktop app for managing 7 Days to Die dedicated servers — install, configure, monitor, and automate restarts without touching the command line.

---

## Features

### Server Management
- Add and manage multiple server instances
- Install and update via SteamCMD (with automatic Windows Defender exclusions to prevent slow downloads)
- **Async version check on startup** — compares your installed build against the latest on Steam; shows a live spinner while checking, then a colour-coded badge (Up to date / Update available)
- Start, stop, and restart servers with one click
- **Per-server auto-start** — toggle in Server Settings so selected servers start automatically when the app opens
- PID tracking — detects crashes and unexpected shutdowns

### Dashboard (Overview Tab)
- Live player count, CPU usage, entity count, day number
- Server uptime timer and Windows version info
- Real-time metrics via Telnet

### Console
- Live log stream via Telnet
- Send commands directly to the server

### Players
- Live online player list refreshed every 15 seconds via Telnet
- Shows name, level, health, score, deaths, zombies killed, ping, Steam ID and IP
- Kick and ban with custom reason
- Server-wide broadcast message
- Robust field-by-field parsing — survives 7D2D 1.x format changes

### Configuration
- Full `serverconfig.xml` editor with search
- World settings, network ports, gameplay options

### Game Settings
Collapsible sections for all key server-side gameplay settings — each with a plain-English description:

| Section | Settings |
|---|---|
| Difficulty & Progression | Game difficulty, XP multiplier, PvP mode |
| Time & World | Game day length (minutes), daylight hours |
| Zombies | Day/night/feral/Blood Moon speed, max spawned zombies & animals |
| Blood Moon | Frequency, day variance (±), enemy count |
| Loot & Drops | Loot abundance, loot respawn days, air drops, drop on death/quit |
| Land Claims | Claim size, expiry time, offline raid protection |
| New Player Protection | Safe zone level, safe zone hours |
| Performance | Max view distance |

All changes write to `serverconfig.xml` via a confirmation popup. Server restart required for changes to take effect.

### Backups
- Creates timestamped zip archives containing:
  - Save game folder (handles RWG generated world names automatically)
  - Generated world data
  - `serverconfig.xml`
- One-click restore

### Scheduled Restarts
- **Interval mode** — restart every N hours
- **Daily mode** — restart at specific times (e.g. 06:00, 18:00)
- In-game warning messages via Telnet at configurable intervals (60/30/15/10/5/1 min)
- Activity log showing all schedule events

### Discord Notifications
Per-event webhook notifications with Discord embeds:

| Event | Default colour |
|---|---|
| Server online | Green |
| Server offline | Orange |
| Server restarted | Blue |
| Server crashed | Red |
| Server updated | Purple |
| Restart warning | Yellow |

Each event is individually configurable: enable/disable, per-event webhook URL override, custom message template, embed colour, role mention ID, thumbnail URL, and cooldown.

**Variables:** `{SERVER}` `{IPPORT}` `{PLAYERS}` `{MAXPLAYERS}` `{TIMEMIN}` `{REASON}`

### Mod Management
- Browse installed mods (reads `ModInfo.xml` from the server's `Mods/` folder)
- Install mods from a `.zip` file
- Enable / disable individual mods
- Open mod website link

### App Settings
- **Theme** — dark/light toggle, primary and accent colour pickers
- **Card background** — choose from 18 curated dark colour swatches and set opacity with a slider; changes apply live across all cards
- Default server install root path
- Start minimised on launch
- Auto-start on Windows login (requires published build)

---

## Requirements

- Windows 10/11
- [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0)
- SteamCMD (downloaded automatically on first install)

---

## Building from Source

```
git clone <repo>
cd SH_7D2D_Manager/SevenDaysManager
dotnet build
dotnet run
```

Requires the [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

### Publishing (single-file self-contained exe)

```
dotnet publish SevenDaysManager/SevenDaysManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishReadyToRun=true
```

Output: `SevenDaysManager/bin/Release/net9.0-windows/win-x64/publish/SevenDaysManager.exe` (~160 MB, no install required)

---

## Tech Stack

| Layer | Library |
|---|---|
| UI | WPF + [MaterialDesignInXamlToolkit](https://github.com/MaterialDesignInXAML/MaterialDesignInXamlToolkit) 5.3 |
| MVVM | [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) |
| Database | LiteDB |
| Server comms | TcpClient (Telnet) |
| Config parsing | System.Xml.Linq |
| Backups | System.IO.Compression |

---

## Security

- Telnet commands sanitise user input (quote/newline stripping) before sending
- Zip extraction validates destination paths against zip slip attacks
- URL launching validates `http`/`https` scheme before calling `Process.Start`
- Server launch uses `ProcessStartInfo.ArgumentList` (no shell string injection)

---

## License

MIT
