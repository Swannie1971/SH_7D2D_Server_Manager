; ──────────────────────────────────────────────────────────────────────────
;  7 Days to Die — Server Manager · Inno Setup script
;
;  Build:
;    1. dotnet publish SevenDaysManager\SevenDaysManager.csproj -c Release
;    2. "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\sevendays.iss
;
;  Output:  installer\output\SevenDaysManager-Setup-{version}.exe
;
;  Why this is so much simpler than the RustPanel installer: this app publishes
;  to ONE self-contained .exe (no Node runtime, no services, no database server).
;  Its data lives in %APPDATA%\7D2DManager\, which is per-user and outside
;  {app} — so an upgrade preserves servers and settings with no migration step.
;
;  Distribution model:
;    · This installer  → clean first-time install
;    · Bare .exe on GitHub Releases → what the in-app updater downloads and
;      copies over the running file. Both must be the same build.
; ──────────────────────────────────────────────────────────────────────────

#define MyAppName      "7 Days to Die Server Manager"
#define MyAppShortName "SevenDaysManager"
#define MyAppVersion   "0.3.0"
#define MyAppPublisher "Swannie"
#define MyAppURL       "https://github.com/Swannie1971/SH_7D2D_Manager-releases"
#define MyAppExe       "SevenDaysManager.exe"

; Where `dotnet publish` leaves the single-file exe.
#define PublishDir "..\SevenDaysManager\bin\Release\net9.0-windows\win-x64\publish"
#define AssetsDir  "..\SevenDaysManager\Assets"

[Setup]
; A stable AppId is what tells Inno an existing install is an UPGRADE rather than
; a second copy. Never change it between releases.
AppId={{C7E4B92A-3F6D-4A81-9B2E-5D8A1F0C7E43}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

DefaultDirName={autopf}\{#MyAppShortName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes

; Admin: the app adds Defender exclusions and manages server files under
; C:\GameServers by default, and the updater overwrites its own exe in place.
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

OutputDir={#SourcePath}\output
OutputBaseFilename={#MyAppShortName}-Setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
SetupIconFile={#AssetsDir}\logo.ico
UninstallDisplayIcon={app}\{#MyAppExe}
UninstallDisplayName={#MyAppName} {#MyAppVersion}

; The app minimises to the tray, so it can easily be running during an upgrade.
CloseApplications=force

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut";                      GroupDescription: "Shortcuts:"
Name: "startup";     Description: "Start automatically when I sign in";             GroupDescription: "Windows integration:"; Flags: unchecked
; Defender's real-time scanner inspects every file SteamCMD writes, which turns a
; server download into a crawl. Excluding the install root and SteamCMD is the
; difference between minutes and hours.
Name: "defender";    Description: "Add Windows Defender exclusions for SteamCMD and server files (strongly recommended — downloads are far slower without this)"; GroupDescription: "Windows integration:"

[Files]
; The single self-contained exe. Everything else (WPF, LiteDB, the .NET runtime)
; is inside it.
Source: "{#PublishDir}\{#MyAppExe}"; DestDir: "{app}"; Flags: ignoreversion
; Keep the icon on disk so shortcuts survive if the exe is swapped by the updater.
Source: "{#AssetsDir}\logo.ico";     DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}";            Filename: "{app}\{#MyAppExe}"; IconFilename: "{app}\logo.ico"
Name: "{group}\Uninstall {#MyAppName}";  Filename: "{uninstallexe}"
Name: "{commondesktop}\{#MyAppName}";    Filename: "{app}\{#MyAppExe}"; IconFilename: "{app}\logo.ico"; Tasks: desktopicon

[Registry]
; Optional auto-start. The app has its own in-app toggle for this too
; (AutoStartService writes the same key), so they stay consistent.
Root: HKLM; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "{#MyAppShortName}"; ValueData: """{app}\{#MyAppExe}"""; \
    Flags: uninsdeletevalue; Tasks: startup

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]

{ Add a Defender path exclusion. Safe to re-run — Add-MpPreference de-duplicates. }
procedure AddDefenderExclusion(Path: String);
var
  Rc: Integer;
begin
  Exec('powershell.exe',
       '-NoProfile -ExecutionPolicy Bypass -Command "Add-MpPreference -ExclusionPath ''' + Path + ''' -ErrorAction SilentlyContinue"',
       '', SW_HIDE, ewWaitUntilTerminated, Rc);
end;

procedure RemoveDefenderExclusion(Path: String);
var
  Rc: Integer;
begin
  Exec('powershell.exe',
       '-NoProfile -ExecutionPolicy Bypass -Command "Remove-MpPreference -ExclusionPath ''' + Path + ''' -ErrorAction SilentlyContinue"',
       '', SW_HIDE, ewWaitUntilTerminated, Rc);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then begin
    if WizardIsTaskSelected('defender') then begin
      { The app's default install root. Users can change it in Settings, in which
        case they'll want to exclude that path too — but this covers the default. }
      AddDefenderExclusion('C:\GameServers');
      AddDefenderExclusion(ExpandConstant('{app}'));
    end;
  end;
end;

procedure CurUninstallStepChanged(CurStep: TUninstallStep);
var
  KeepData: Integer;
  DataDir: String;
begin
  if CurStep = usUninstall then begin
    RemoveDefenderExclusion('C:\GameServers');
    RemoveDefenderExclusion(ExpandConstant('{app}'));
  end;

  if CurStep = usPostUninstall then begin
    { Servers, settings, schedules and Discord config. Never delete without asking —
      and note this is only the app's OWN data; the game server files under
      C:\GameServers are left alone entirely. }
    DataDir := ExpandConstant('{userappdata}\7D2DManager');
    if DirExists(DataDir) then begin
      KeepData := MsgBox(
        'Keep your server list and settings?' + #13#10 + #13#10 +
        DataDir + #13#10 + #13#10 +
        'YES  — keep them, so reinstalling picks up where you left off.' + #13#10 +
        'NO   — delete them permanently.' + #13#10 + #13#10 +
        'Your actual game server files are NOT affected either way.',
        mbConfirmation, MB_YESNO or MB_DEFBUTTON1);
      if KeepData = IDNO then
        DelTree(DataDir, True, True, True);
    end;
  end;
end;
