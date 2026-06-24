# 7 Days to Die Server Manager

A native Windows desktop app for managing 7 Days to Die dedicated servers — install, configure, monitor, and automate restarts without touching the command line.

---

## Features

### Server Management
- Add and manage multiple server instances
- Install and update via SteamCMD (with automatic Windows Defender exclusions to prevent slow downloads)
- Start, stop, and restart servers with one click
- PID tracking — detects crashes and unexpected shutdowns

### Dashboard (Overview Tab)
- Live player count, CPU usage, entity count, day number
- Server uptime timer and Windows version info
- Real-time metrics via Telnet

### Console
- Live log stream via Telnet
- Send commands directly to the server

### Configuration
- Full `serverconfig.xml` editor with search
- World settings, network ports, gameplay options

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

## License

MIT
