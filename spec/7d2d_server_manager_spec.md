# 7 Days to Die — Server Manager Specification (Native Windows App)

A self-hosted **native Windows desktop application** for managing **7 Days to
Die (7D2D)** dedicated servers on the same machine. Built in **Visual Studio
2022** with **C# + WPF on .NET 8** (MVVM).

> **Why native (not web):** the manager runs on the same box as the game server,
> so it can drive everything directly — `Process.Start` for SteamCMD and the
> server, `TcpClient` for Telnet, `XDocument` for config, `FileSystemWatcher`
> for logs, `ZipFile` for backups. No web server, no HTTP API, no auth, no
> bundled runtime. Trade-off: manageable only at that machine (or via RDP), not
> from a remote browser.
>
> **Origin note:** this spec was derived from a working *web-based* Rust server
> manager ("RustPanel", a Vite/Fastify/Node 3-tier app). The **logic** transfers
> (SteamCMD args, the server lifecycle, the gotchas); the **code** is a fresh C#
> build. Where a concept came from RustPanel it's noted.

---

## 0. Working in this project (START HERE)

> This section is the orientation for both the human (new to Visual Studio) and
> for Claude Code picking up in this folder. Read it first.

### Who does what
- **Claude Code** (running in this folder, via VSCode's terminal/extension or any
  terminal) writes and edits the code — `.cs`, `.xaml`, `.csproj` files on disk.
  It can also build and run the app itself via the `dotnet` CLI to verify changes.
- **Visual Studio 2022** opens the *same folder/solution* and is used for the
  things a text editor can't do well: the **XAML visual designer** (drag-and-drop
  WPF layout) and **breakpoint debugging**. VS and Claude Code edit the same files
  — no copying, no conflict. Save in one, the other sees it.
- You do **not** need a Claude plugin for Visual Studio. Claude works on files;
  VS just opens those files.

### First-time setup (Claude can run these)
```
dotnet new wpf -n SevenDaysManager          # create the WPF project
cd SevenDaysManager
dotnet add package CommunityToolkit.Mvvm     # MVVM helpers
dotnet add package LiteDB                     # or Microsoft.Data.Sqlite + Dapper
dotnet build                                  # verify it compiles
dotnet run                                    # launch the app window
```
Then `git init` and add a `CLAUDE.md` (see below).

### The build-check discipline (same as the Rust project used)
After any change, run **`dotnet build`** — it's the C# equivalent of the
`tsc --noEmit` we used in the web project. A clean build = the code compiles.
Claude should run it after edits, before moving on. (WPF is Windows-only; you're
on Windows with the .NET SDK, so it builds locally fine.)

### Visual Studio 2022 quick-start (for the human, new to VS)
- **Open the project:** File → Open → Folder… (pick the project folder), or
  double-click the `.sln` once one exists. The **Solution Explorer** (right side)
  is your file tree.
- **Run / debug:** press **F5** (run with debugger) or **Ctrl+F5** (run without).
  The green ▶ button at the top does the same.
- **Breakpoints:** click in the left margin next to a line of C# → red dot →
  execution pauses there when hit (hover variables to inspect).
- **XAML designer:** open any `.xaml` file → VS shows a split view: the visual
  preview (top) and the XAML markup (bottom). You can drag controls from the
  **Toolbox** or edit the markup directly — both update the other.
- **NuGet packages:** right-click the project → "Manage NuGet Packages" (GUI), or
  let Claude use `dotnet add package` (same result).
- **Build:** Build menu → Build Solution (**Ctrl+Shift+B**). Errors show in the
  **Error List** / **Output** windows at the bottom.
- **Tip:** keep VSCode+Claude and VS 2022 open on the same folder at once. Use
  Claude for writing/refactoring, flip to VS for designer + debugging.

### Recommended VSCode setup (so IntelliSense works there too)
Install the **C# Dev Kit** extension → VSCode gets C# autocomplete, build, and
debugging. Then VSCode + Claude Code covers most work; VS 2022 is just the
visual-designer / heavy-debug tool when you want it.

### CLAUDE.md to drop in the new folder (so a fresh Claude is oriented)
Create `CLAUDE.md` at the project root with something like:
```
# 7 Days to Die Server Manager — Claude instructions

Native Windows desktop app. C# + WPF, .NET 8, MVVM (CommunityToolkit.Mvvm).
Built/run in Visual Studio 2022; also edited via Claude Code + dotnet CLI.

- Read the spec first: spec/7d2d_server_manager_spec.md (the full build plan).
- After any change, run `dotnet build` to verify (our compile-check, like tsc).
- Run the app with `dotnet run`. WPF is Windows-only — that's fine here.
- Keep all I/O async; update bound collections on the UI dispatcher.
- Gotchas that VANISH in native vs the old web app: spaces in paths
  (ProcessStartInfo.ArgumentList auto-quotes) and apostrophes (no PowerShell
  string parsing). Still resolve the real save path from serverconfig; telnet
  attaches only after boot (combine with log tailing).
```
Copy this spec into the new project's `spec/` folder so it travels with it.

---

## 1. Tech stack & project shape

- **UI:** WPF (.NET 8), MVVM pattern. Use the VS 2022 XAML designer. Optional: [CommunityToolkit.Mvvm](https://www.nuget.org/packages/CommunityToolkit.Mvvm) for `[ObservableProperty]` / `[RelayCommand]` source generators (cuts boilerplate massively).
- **Data:** SQLite via `Microsoft.Data.Sqlite` + `Dapper` (lightweight), or `LiteDB` (single-file NoSQL, zero-setup), or plain JSON files for a v1. Recommendation: **LiteDB** for speed of development, or SQLite if you want SQL.
- **Telnet:** `System.Net.Sockets.TcpClient` + async `StreamReader`/`StreamWriter`.
- **Config XML:** `System.Xml.Linq` (`XDocument`).
- **Process:** `System.Diagnostics.Process` + `ProcessStartInfo`.
- **Backups/zip:** `System.IO.Compression.ZipFile`.
- **HTTP (downloads/version checks):** `HttpClient`.

### Suggested project structure (single WPF project, or a Core library + WPF UI)
```
SevenDaysManager/
  Models/         Server.cs, PlayerInfo, BackupInfo, ConfigProperty…
  Services/       SteamCmdService, ServerProcessService, TelnetClient,
                  ServerConfigService (XML), BackupService, DataStore,
                  MetricsPoller, DiscordNotifier, ScheduleService
  ViewModels/     MainViewModel, ServerViewModel, ConsoleViewModel,
                  PlayersViewModel, ConfigViewModel, BackupsViewModel…
  Views/          MainWindow.xaml, ServerDetailView.xaml, tabs…
  App.xaml(.cs)
```
Keep all I/O off the UI thread (`async`/`await`, `Task.Run`). Marshal UI updates back with the dispatcher (or bind to `ObservableCollection`s updated on the UI thread).

---

## 2. Key 7D2D facts (the game-specific bits)

| Thing | Value |
|---|---|
| **SteamCMD app ID** | `294420` (7 Days to Die Dedicated Server) |
| **Executable** | `7DaysToDieServer.exe` (or `startdedicated.bat` → calls it with `-configfile=serverconfig.xml`) |
| **Config** | `serverconfig.xml` — XML `<property name="X" value="Y"/>` entries (NOT command-line args) |
| **Remote admin** | **Telnet** (plain TCP), default port `8081`, optional password. No Source/WebSocket RCON exists. |
| **Web control panel** | Optional built-in dashboard (`WebDashboardEnabled`, port `8080`) — separate from our app; we may just offer a "Open web dashboard" button. |
| **Game ports** | `ServerPort` default `26900` — uses `ServerPort`, `+1`, `+2` (UDP). Plus Telnet `8081`, WebDashboard `8080`. |
| **Maps** | `GameWorld` = `Navezgane` (fixed), `Pregen…` (pre-generated), or `RWG` (random gen with `WorldGenSeed` + `WorldGenSize`). |
| **Mods** | folder-based: a `Mods/` directory in the install dir. No central API. |
| **Saves / wipe target** | World saves under `UserDataFolder`/`SaveGameFolder`; default Windows path `%APPDATA%\7DaysToDie\Saves\<GameWorld>\<GameName>`. **Resolve the real path from serverconfig at runtime** — it's configurable. |
| **Anti-cheat** | `EACEnabled` toggle in config. |

---

## 3. Data model (POCO → persisted)

```csharp
public class Server {
  public string Id { get; set; } = Guid.NewGuid().ToString("N");
  public string Name { get; set; } = "";            // ServerName
  public string Status { get; set; } = "stopped";   // stopped|starting|running
  public string InstallDir { get; set; } = "";

  // Networking
  public int ServerPort { get; set; } = 26900;
  public int TelnetPort { get; set; } = 8081;
  public string TelnetPass { get; set; } = "";
  public int WebDashPort { get; set; } = 8080;
  public string ServerIp { get; set; } = "";         // public connect address (display only)

  // Identity / visibility
  public string Password { get; set; } = "";          // ServerPassword (to join)
  public string Description { get; set; } = "";
  public string WebsiteUrl { get; set; } = "";
  public int Visibility { get; set; } = 2;            // 0/1/2
  public int MaxPlayers { get; set; } = 8;

  // World
  public string GameWorld { get; set; } = "Navezgane"; // Navezgane|RWG|Pregen*|custom
  public string GameName { get; set; } = "My Game";    // the save name
  public string WorldSeed { get; set; } = "";          // RWG only
  public int WorldSize { get; set; } = 6144;           // RWG only (2048–16384 step 1024)
  public bool EacEnabled { get; set; } = true;

  // Appearance (optional per-server icon — local file path or URL)
  public string LogoPath { get; set; } = "";

  // Extra serverconfig overrides not promoted above: list of (name,value)
  public List<ConfigProperty> ExtraConfig { get; set; } = new();

  public DateTime? LastWipedAt { get; set; }
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public record ConfigProperty(string Name, string Value);
```
Persist via `DataStore` (LiteDB collection / SQLite table / JSON). Store app-wide
settings (Discord webhook, default install root, scheduled tasks) similarly.

---

## 4. Services (the core logic)

### 4.1 SteamCmdService — install / update
- Ensure SteamCMD exists (download `steamcmd.zip` to e.g. `C:\steamcmd`, extract with `ZipFile.ExtractToDirectory` on first use).
- Run: `steamcmd.exe +force_install_dir <dir> +login anonymous +app_update 294420 validate +quit`.
- **Pass args via `ProcessStartInfo.ArgumentList`** (each arg added separately) — .NET auto-quotes, so paths with spaces "just work" (this sidesteps RustPanel's space-truncation + apostrophe headaches entirely — a real native win).
- Capture stdout/stderr async, surface progress to the UI (bind to an `ObservableCollection<string>` log).
- `GetVersion()` — read installed build from `steamapps/appmanifest_294420.acf`; compare to latest via the steamcmd.net info API (`HttpClient`).

### 4.2 ServerConfigService — serverconfig.xml (replaces Rust's bat builder)
- **Write** `serverconfig.xml` from the `Server` row before launch: load a base template (the default that ships with the dedicated server), set the promoted properties + `ExtraConfig`, and `XDocument.Save`.
- **Read/parse** an existing `serverconfig.xml` back into a `Server` (round-trip `<property name value/>`). Used by the Config editor + Import.
- **Always force** `TelnetEnabled=true` + a `TelnetPassword` so the app can connect (generate a random password if blank).
- Known properties to surface in the Config editor (subset; see full default file): `ServerName`, `ServerDescription`, `ServerWebsiteURL`, `ServerPassword`, `ServerVisibility`, `ServerMaxPlayerCount`, `ServerPort`, `TelnetEnabled`, `TelnetPort`, `TelnetPassword`, `WebDashboardEnabled`, `WebDashboardPort`, `GameWorld`, `GameName`, `WorldGenSeed`, `WorldGenSize`, `EACEnabled`, `GameDifficulty`, `DayNightLength`, `BloodMoonFrequency`, `LootAbundance`, `XPMultiplier`, `LandClaimCount`, etc.

### 4.3 ServerProcessService — launch / stop / status
- **Launch:** `Process.Start` `7DaysToDieServer.exe` with `ArgumentList`: `-configfile=serverconfig.xml`, `-logfile <dir>\logs\<ts>.log`, `-quit`, `-batchmode`, `-nographics`, `-dedicated`. Set `WorkingDirectory = installDir`.
  - **Detach so the server survives closing the app:** do NOT assign the child to a kill-on-close Job Object; by default a .NET child process keeps running after the parent exits. Track only the PID (persist it) so you can reattach/stop later. Optionally `Process.GetProcessById` on app start to recover a running server.
- **Status:** authoritative check = is a `7DaysToDieServer.exe` process alive for this install (match by PID, or by the process's command line / working dir if PID was lost). Reconcile the stored status against reality on a timer (mirrors RustPanel's PID reconciliation).
- **Stop/restart with warnings:** via Telnet — broadcast a countdown with `say`, then `saveworld`, then `shutdown`; wait for the process to exit (force-kill fallback `process.Kill()`); relaunch for restarts. Provide a cancel.

### 4.4 TelnetClient — the biggest new piece (replaces RCON)
A persistent telnet connection per running server:
- `TcpClient.ConnectAsync("127.0.0.1", telnetPort)`. Read the password prompt, write `telnetPass` + newline, read the welcome banner.
- **Console stream:** 7D2D telnet emits all server log lines. Read lines async in a loop and raise an event / push to an `ObservableCollection<ConsoleLine>` the Console tab binds to. **Also tail the log file** (`FileSystemWatcher` or periodic read) for boot/world-gen output that precedes the telnet attach.
- **Send command:** write a line to the stream. Expose `SendAsync(string cmd)`.
- **Resilience:** reconnect loop with backoff; watchdog for half-open sockets (copy RustPanel's RCON stability approach conceptually).
- **Commands to build features on:**
  - `lp` / `listplayers` — online players (parse name, id/steamId, ping, health, position)
  - `lpi` — players with extra info
  - `say "<msg>"` — broadcast
  - `kick <name/id> "<reason>"`
  - `ban add <name/id> <duration> "<reason>"`, `ban remove <name/id>`, `ban list`
  - `saveworld` — force save (before stop/restart/backup)
  - `shutdown` — graceful shutdown
  - `mem` — memory + entity/zombie counts (metrics)
  - `gettime` — in-game day/time
  - `version` — server version
  - `pm "<player>" "<msg>"` — private message

### 4.5 PlayersService — via Telnet
- `lp` → parse the online list (kick/ban/broadcast actions). 7D2D telnet shows online only; if you want an offline-history list, persist seen players yourself.

### 4.6 MetricsPoller — via Telnet
- On a timer, run `mem` + `gettime` + `lp`; parse player count, zombie/entity counts, heap memory, in-game day/time, uptime. **No server FPS** over telnet (omit / show "—"). Feed the dashboard tiles and an optional in-app player-count history chart.

### 4.7 BackupService — saves
- **Resolve the save folder** from serverconfig (`<UserDataFolder>\Saves\<GameWorld>\<GameName>`, falling back to `%APPDATA%\7DaysToDie\Saves\…`).
- Zip it with `ZipFile.CreateFromDirectory` to a `Backups/` folder (timestamped name). Restore = stop server → extract zip back. (No PowerShell — pure .NET, so the apostrophe gotcha is gone.)

### 4.8 Wipe (MANUAL only — same safety model as RustPanel)
- Server must be **stopped**; show a **preview** of what gets deleted; require a typed confirmation; offer **back-up-first**.
- Two modes: **delete save only** (fresh start, same world/seed) vs **delete save + regenerate** (new RWG seed/size — also remove the generated world under `GeneratedWorlds/`).
- Record `LastWipedAt`. Never automatic — no scheduled/auto wipes, ever.

### 4.9 ScheduleService — scheduled restarts
- In-app timer/cron that fires graceful restarts (via 4.3) with in-game warnings. Runs while the app is open. For restarts to fire when the app is **closed**, register a Windows **Scheduled Task** instead (see §6).

### 4.10 Mods (optional v2)
- List `Mods/` (each subfolder has `ModInfo.xml`). Enable/disable by moving between `Mods/` and `Mods.disabled/`. Upload = extract a zip into `Mods/`.

### 4.11 DiscordNotifier (optional, reuse RustPanel's design)
- Outbound webhooks via `HttpClient` POST of an embed JSON. Events: server start / online / offline / stop / restart / wipe, plus update available (Steam app 294420). Per-event URL, colour, role-ping, cooldown. Template tokens (`{SERVER}`, `{IPPORT}`, `{PLAYERS}`, `{MAXPLAYERS}`, etc.).

### 4.12 Import existing servers
- Scan a root (default the install-root setting) for our installs (marker: `7DaysToDieServer.exe` + `serverconfig.xml`), parse the XML into a `Server`, re-adopt into an empty DB after a reinstall / new PC.

---

## 5. UI (WPF views)

A main window with a server list (left) and a detail pane (right), or a tabbed
MDI feel — mirror your existing ShadowHunter app's look if you like.

**Server detail tabs:**
- **Overview** — identity, world (GameWorld / seed / size), ports, live tiles (players / entities / RAM / in-game day), per-server icon, Start/Stop/Restart buttons, "Copy connect" (`<ip>:<port>`), "Open web dashboard".
- **Console** — live telnet + log stream (bound `ObservableCollection`), command input box.
- **Config** — friendly editor for the promoted `serverconfig.xml` properties + an advanced (name/value) grid for `ExtraConfig`. Save writes the XML (applies on next start).
- **Players** — online list with kick/ban/broadcast; Bans via `ban list`.
- **Mods** — list/enable/disable/upload (v2).
- **Backups** — create/restore/delete save zips.
- **Schedule** — scheduled restarts.
- **Settings / Danger zone** — wipe, delete server, appearance.

**Add-server wizard** — name, install dir (folder browse dialog), ports, max
players, password, GameWorld picker (Navezgane / RWG / Pregen / custom); for RWG:
seed + size. A "Install server files" button kicks off SteamCMD with a progress log.

**App settings** — default install root, SteamCMD path, Discord webhook, theme.

---

## 6. Deployment & boot-survival

- **Publish:** `dotnet publish -c Release -r win-x64 --self-contained` → a single
  folder (or single-file with `PublishSingleFile=true`) that runs without a
  preinstalled .NET runtime. Wrap in a simple installer if desired (Inno Setup
  works for a .NET app too; or just a zip).
- **Run the game detached** so servers survive closing the UI (default child-
  process behavior — just don't kill them on exit).
- **Auto-start on boot / before login:** register a Windows **Scheduled Task**
  (`schtasks` or `Microsoft.Win32.TaskScheduler` NuGet) set to "Run whether user
  is logged on or not", triggered at startup, that launches each enabled
  server's `7DaysToDieServer.exe` (or a small headless launcher). The desktop app
  is then just the control UI; the task guarantees servers come back after a
  reboot even before anyone logs in.
- **Firewall:** open `26900-26902/UDP` (game); `8081/TCP` (telnet — usually
  local-only) and `8080/TCP` (web dashboard) only if you use them remotely. Add
  rules with `netsh advfirewall` or the TaskScheduler/firewall APIs.

---

## 7. Effort estimate

- **Conceptually reused from the RustPanel design:** server lifecycle, status
  reconciliation, scheduled restarts with warnings, Discord notifier, backups
  model, wipe safety model, import, the gotchas. (~half the *thinking* is done.)
- **New C# code:** the WPF UI, Telnet client + console/players/metrics,
  serverconfig.xml read/write, process management, SteamCMD wrapper, persistence.
- Ballpark **1–2 weeks** for a solid v1 for someone comfortable in C#/WPF, since
  the spec removes the design unknowns.

---

## 8. Gotchas (some vanish in native, some carry over)

- ✅ **Spaces in paths** — solved for free by `ProcessStartInfo.ArgumentList`
  (auto-quoting). No more `+force_install_dir` truncation.
- ✅ **Apostrophes in paths** — mostly gone: native `ZipFile`/`XDocument`/`Process`
  don't go through PowerShell string parsing. (If you ever DO shell to PowerShell,
  still escape `'`→`''`.)
- ⚠️ **Telnet attaches only after boot** — combine the telnet stream with log-file
  tailing so you don't miss world-gen/startup output.
- ⚠️ **Resolve the real save path** from serverconfig (`UserDataFolder`/
  `SaveGameFolder`) before backup/wipe — it's configurable, default is under
  `%APPDATA%\7DaysToDie\Saves`.
- ⚠️ **Detached process + boot task** — closing the UI must not kill servers;
  boot-before-login needs a Scheduled Task, not just the app.
- ⚠️ **UI thread** — keep all install/telnet/process/file I/O async; update bound
  collections on the dispatcher.
- ℹ️ **No remote access** — by design. If you later want phone/LAN access, that's
  when a thin web layer (or the original web architecture) comes back.

---

## 9. Open questions to resolve when building

- Exact default save path for the target 7D2D version, and whether
  `UserDataFolder`/`SaveGameFolder` are set (affects backup/wipe target).
- Persist an "offline players" history ourselves, or online-only?
- Mods management depth for v1 (list-only vs enable/disable vs upload).
- Persistence choice: LiteDB (fastest to build) vs SQLite (familiar SQL) vs JSON.
- Single combined app vs split (UI app + a tiny headless launcher invoked by the
  Scheduled Task) — the split is cleaner for boot-survival but a bit more work.
