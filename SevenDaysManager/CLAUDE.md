# 7 Days to Die Server Manager — Claude instructions

Native Windows desktop app. C# + WPF, .NET 8, MVVM (CommunityToolkit.Mvvm).
Built/run via `dotnet` CLI; opened in Visual Studio 2022 for XAML designer + debugging.

## Stack
- UI: WPF + MaterialDesignInXamlToolkit 5.x (dark theme, Teal primary, LightGreen secondary)
- MVVM: CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`)
- Persistence: LiteDB
- Telnet: System.Net.Sockets.TcpClient
- Config: System.Xml.Linq (XDocument)
- Process: System.Diagnostics.Process + ProcessStartInfo.ArgumentList (auto-quotes paths)
- Backups: System.IO.Compression.ZipFile

## Rules
- After any change run `dotnet build` to verify (our compile check).
- Run the app with `dotnet run --project SevenDaysManager.csproj`.
- Keep all I/O async; update ObservableCollections on the UI dispatcher.
- Always use MaterialDesign controls/styles — never default WPF styling.
- Use `ProcessStartInfo.ArgumentList` (not Arguments string) so paths with spaces work.

## Project layout
```
Models/         Server.cs, PlayerInfo, BackupInfo, ConfigProperty
Services/       SteamCmdService, ServerProcessService, TelnetClient,
                ServerConfigService, BackupService, DataStore, MetricsPoller
ViewModels/     MainViewModel, ServerViewModel, ConsoleViewModel, ...
Views/          MainWindow.xaml, (tab views)
```

## Spec
Full build plan: ../spec/7d2d_server_manager_spec.md
