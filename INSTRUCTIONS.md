# 7D2D Server Manager — Build, Run & Release Instructions

This is the master handoff document. It explains how to pull the code onto another
machine (e.g. the gaming PC), compile it, run it, and publish a new release.
**If you are Claude Code reading this on a fresh machine: read the
"Project State — Where We Left Off" section at the bottom first.**

---

## 1. What this project is

A native Windows desktop app to manage a 7 Days to Die dedicated server.

| Thing | Value |
|-------|-------|
| Language / UI | C# + WPF, MVVM (CommunityToolkit.Mvvm) |
| Target framework | **.NET 9** (`net9.0-windows`) — *not* .NET 8 |
| UI toolkit | MaterialDesignThemes 5.3.2 (dark theme, Teal primary) |
| Solution file | `SH_7D2D_Manager.sln` |
| Main project | `SevenDaysManager/SevenDaysManager.csproj` |
| Current version | **0.2.4** |

### The two GitHub repos (important!)

| Repo | Visibility | Purpose |
|------|-----------|---------|
| `Swannie1971/SH_7D2D_Server_Manager` | **Private** | The source code. This is what you clone/pull/push. |
| `Swannie1971/SH_7D2D_Manager-releases` | **Public** | Holds ONLY the compiled `.exe` releases. The in-app auto-updater checks this repo. |

> The source stays private permanently. Releases go to the *separate public* repo
> so the auto-updater (which calls the GitHub API anonymously) can see them — the
> API returns 404 for private repos without authentication.

### Version control quick reference

| Setting | Value |
|---------|-------|
| Source remote (`origin`) | `https://github.com/Swannie1971/SH_7D2D_Server_Manager.git` |
| Default branch | `main` |
| Releases repo (exe uploads) | `Swannie1971/SH_7D2D_Manager-releases` |
| Git user | `Swannie` |

Verify the remote is correct on any machine:
```powershell
git remote -v          # both fetch + push should point at SH_7D2D_Server_Manager.git
git branch --show-current   # should print: main
```

If `origin` is missing or wrong (e.g. fresh folder), set it:
```powershell
git remote add origin https://github.com/Swannie1971/SH_7D2D_Server_Manager.git
# or, to change an existing one:
git remote set-url origin https://github.com/Swannie1971/SH_7D2D_Server_Manager.git
```

GitHub will prompt for credentials on first push/pull. Use a **Personal Access Token**
as the password (classic token with `repo` scope), or sign in once with
`gh auth login` and Git will reuse that auth.

---

## 2. Prerequisites on the gaming PC (one time)

Install these before anything else:

1. **Git for Windows** — https://git-scm.com/download/win
2. **.NET 9 SDK** (the SDK, not just the runtime) — https://dotnet.microsoft.com/download/dotnet/9.0
   - Verify after install: open a new terminal and run `dotnet --version` → should print `9.x`
3. **GitHub CLI** (only needed for *publishing* releases) — install via:
   ```powershell
   winget install --id GitHub.cli --accept-source-agreements --accept-package-agreements
   ```
   Then authenticate once: `gh auth login` (choose GitHub.com → HTTPS → browser).
4. *(Optional)* **Visual Studio 2022** (17.12 or newer) with the
   ".NET desktop development" workload — only if you want the XAML designer / debugger.
   The app builds and runs entirely from the command line without it.

---

## 3. First-time setup (clone the repo)

Pick a folder and clone the **private source** repo. To keep paths identical to the
dev machine (recommended, keeps Claude Code memory keys consistent):

```powershell
# Create the parent folders if they don't exist
New-Item -ItemType Directory -Force "C:\Users\swann\Documents\PVT\Devtest"

# Clone into the same path used on the dev machine
git clone https://github.com/Swannie1971/SH_7D2D_Server_Manager.git "C:\Users\swann\Documents\PVT\Devtest\SH_7D2D_Manager"

cd "C:\Users\swann\Documents\PVT\Devtest\SH_7D2D_Manager"
```

> You can clone anywhere you like, but using the same path means any per-project
> Claude Code settings line up.

---

## 4. Daily workflow — pull the latest code

Every time you sit down at the gaming PC, grab the newest changes first:

```powershell
cd "C:\Users\swann\Documents\PVT\Devtest\SH_7D2D_Manager"
git pull
```

If you have local uncommitted edits and `git pull` complains, either commit them or
stash them first:

```powershell
git stash          # set them aside
git pull
git stash pop      # bring them back
```

---

## 5. Build & run during development

From the repo root:

```powershell
# Restore NuGet packages + compile (debug)
dotnet build

# Run the app
dotnet run --project SevenDaysManager/SevenDaysManager.csproj
```

`dotnet build` is also the standard "did I break anything?" check after editing code.

---

## 6. Releasing a new version

There are **three** steps: bump the version, build the single-file exe, publish it.

### Step 6a — Bump the version number (TWO places, keep them equal)

1. `SevenDaysManager/Services/UpdateService.cs` → line ~10:
   ```csharp
   public const string CurrentVersion = "0.2.5";   // <-- new version
   ```
2. `SevenDaysManager/Views/MainWindow.xaml` → the sidebar badge (~line 205):
   ```xml
   <TextBlock Text="v0.2.5" ... />                 <!-- <-- new version -->
   ```

> **Why two places:** `CurrentVersion` is what the running app compares against the
> latest GitHub release to decide if an update exists. The XAML badge is just the
> label shown in the sidebar. They must match or the UI will lie.

> **Updater gotcha:** `CurrentVersion` represents the version *currently running*.
> The GitHub release tag must be **higher** than `CurrentVersion` for the update
> prompt to fire. So: ship a build, THEN tag the next release higher — or, to test
> the prompt, keep an older exe whose `CurrentVersion` is lower than the live release.

After editing, recompile to be safe: `dotnet build`.

### Step 6b — Publish a self-contained single-file exe

```powershell
dotnet publish SevenDaysManager/SevenDaysManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The exe lands at:
```
SevenDaysManager\bin\Release\net9.0-windows\win-x64\publish\SevenDaysManager.exe
```

### Step 6c — Create the GitHub release (uploads the exe to the PUBLIC repo)

Use the full path to `gh.exe` if `gh` isn't on PATH yet (e.g. right after install
in the same terminal session):

```powershell
& "C:\Program Files\GitHub CLI\gh.exe" release create v0.2.5 `
  --repo Swannie1971/SH_7D2D_Manager-releases `
  --title "v0.2.5 — short description" `
  --notes "What changed in this release." `
  "C:\Users\swann\Documents\PVT\Devtest\SH_7D2D_Manager\SevenDaysManager\bin\Release\net9.0-windows\win-x64\publish\SevenDaysManager.exe"
```

Notes:
- The tag (`v0.2.5`) **must** start with `v` and match your bumped version number.
- The release notes you type become the text shown inside the in-app update dialog.
- The attached `.exe` is what the in-app "Update Now" button downloads automatically.
- The GitHub **web UI** fails to upload the ~160 MB exe — always use `gh` from the CLI.

### Step 6d — Commit the version bump to the PRIVATE source repo

```powershell
cd "C:\Users\swann\Documents\PVT\Devtest\SH_7D2D_Manager"
git add SevenDaysManager/Services/UpdateService.cs SevenDaysManager/Views/MainWindow.xaml
git commit -m "Bump version to v0.2.5"
git push
```

---

## 7. How the auto-updater works (so you can test it)

1. On startup the app calls the GitHub API for the latest release in
   `SH_7D2D_Manager-releases`.
2. If the release tag > running `CurrentVersion`, a Material Design dialog appears
   showing `vCURRENT → vLATEST` and the release notes.
3. **Update Now** → downloads the `.exe` asset directly (progress bar shown), writes
   a small `7d2d_update.cmd` to `%TEMP%`, shuts the app down, and the script swaps the
   exe and relaunches it. **No browser, fully automatic.**
4. **Not Now** → dialog closes, app keeps running.
5. If a release has no `.exe` attached, the button falls back to "Open GitHub".

---

## 8. Project State — Where We Left Off

**Read this if you're Claude Code continuing on a new machine.**

Last working session (committed as "Add auto-download updater, fix Players tab lp
parser; bump to v0.2.4"):

- **Auto-download updater — DONE, needs real-world test.** Replaced the old
  "open browser to GitHub" behaviour with a true in-app download + self-replace +
  restart flow. Code in `Views/UpdateAvailableWindow.xaml(.cs)` and
  `Services/UpdateService.cs`. **Untested against a live release with an attached exe.**
- **Players tab fix — DONE, needs real-world test.** The Players tab wasn't showing
  online players even though the Overview tab showed the correct count. Root cause
  suspected: the `lp` (list players) telnet response header regex was too strict.
  Loosened `TotalRx` in `ViewModels/PlayersViewModel.cs` to match any
  `"Total of N..."` wording, and made the buffer commit more forgiving. **Could not
  verify** — the dev session was on a laptop with no server access.

### Tomorrow's test checklist (on the gaming PC with the server running)

1. **Players tab:** Start the server, join it, open the Players tab. Confirm online
   players now appear (name, health, ping, etc.). If still empty, open the Console
   tab, send `lp` manually, and copy the **full raw output** — the exact text is
   needed to fix the parser precisely.
2. **Auto-updater:** With a live `v0.2.4` release published (exe attached), run an
   app build whose `CurrentVersion` is *lower* (e.g. keep a 0.2.3 exe). Confirm the
   update dialog appears, click **Update Now**, and verify it downloads, swaps, and
   relaunches as 0.2.4 automatically.

### Reference: 7D2D `lp` telnet output format
```
Total of 1 in the game
1. id=171, PlayerName, pos=(X, Y, Z), rot=(X, Y, Z), remote=True, health=100, deaths=0, zombies=0, players=0, score=0, level=10, steamid=XXX, ip=XXX, ping=XX
```
(Confirmed against the reference parser at github.com/christopher-roelofs/7dtd-server-manager.
The exact "Total of ..." wording may vary by game version — that's why `TotalRx` was loosened.)

### Known constraints / preferences
- Source repo stays **private** forever; releases go to the public `-releases` repo.
- UI must always use Material Design dark theme — never default WPF styling.
- All dialog windows use a `Primary.Dark` header bar (icon + title + subtitle).
- A feature with its own main-screen card gets its own tab — never nested elsewhere.
- Keep all I/O async; update `ObservableCollection`s on the UI dispatcher.
