# 7D2D Server Manager — Claude Code entry point

**Before doing anything, read [INSTRUCTIONS.md](INSTRUCTIONS.md).** It is the master
handoff doc: build/run/release steps, the two-repo model, and a
"Project State — Where We Left Off" section that says what was just done and what
still needs testing. This is how you pick up the work on any machine — the local
Claude Code memory store does not travel with git, but this file does.

## Quick facts
- C# + WPF, MVVM (CommunityToolkit.Mvvm), **.NET 9** (`net9.0-windows`).
- UI: MaterialDesignThemes 5.x, dark theme. Never use default WPF styling.
- Solution: `SH_7D2D_Manager.sln` · Main project: `SevenDaysManager/SevenDaysManager.csproj`.
- Source repo is **private**; compiled releases go to the **public**
  `Swannie1971/SH_7D2D_Manager-releases` repo (the auto-updater reads it).
- Per-project build/run rules: see `SevenDaysManager/CLAUDE.md`.

## After any code change
Run `dotnet build` to verify it compiles before considering the task done.
