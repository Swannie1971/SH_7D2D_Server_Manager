# 7D2D Server Manager — Feature TODO

## Core
- [x] Server add / edit / delete (LiteDB)
- [x] SteamCMD install & update
- [x] Windows Defender exclusions before download
- [x] Server start / stop / restart (Process + PID tracking)
- [x] Server status polling & auto-detect crash

## Tabs
- [x] Console tab — live Telnet stream, send commands
- [x] Config tab — serverconfig.xml editor
- [x] Server Settings tab — rename, paths, ports, passwords
- [x] Overview tab — live metrics (FPS/Heap/RSS/Chunks/CGO/Items), gauges, system info, uptime
- [x] Players tab — online list, kick / ban / broadcast (`lp`, `ban`, `say`)
- [x] Backups tab — create / restore / delete zip backups (BackupService)
- [x] Schedule tab — scheduled restarts with in-game warnings + Discord (ScheduleService)

## Settings & Polish
- [x] App Settings — theme (dark/light, primary/secondary colour picker)
- [ ] App Settings — persist other settings to DataStore (e.g. backup path, schedule config)
- [ ] Remove debug DispatcherUnhandledException handler from App.xaml.cs
- [ ] Auto-start on boot (Windows Scheduled Task)
