using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32;
using SevenDaysManager.Models;
using SevenDaysManager.Services;

namespace SevenDaysManager.ViewModels;

public partial class OverviewViewModel : ObservableObject, IAsyncDisposable
{
    private readonly Server         _server;
    private readonly MetricsPoller  _poller;
    private readonly DispatcherTimer _uptimeTimer;

    // ── Static server info ────────────────────────────────────────────────────
    public string ServerName   => _server.Name;
    public string ServerPortStr => _server.ServerPort.ToString();
    public string GameWorld    => _server.GameWorld;
    public string GameName     => _server.GameName;
    public int    MaxPlayers   => _server.MaxPlayers;
    public int    MaxZombies   => 64;
    public int    MaxAnimals   => 50;

    [ObservableProperty] private string _serverIp      = "—";
    [ObservableProperty] private string _serverUptime  = "—";
    [ObservableProperty] private string _serverVersion = "—";
    [ObservableProperty] private string _gameTime      = "—";
    [ObservableProperty] private string _gameMode      = "Survival";
    [ObservableProperty] private string _gameDifficulty = "Adventurer";

    // ── Live metrics ──────────────────────────────────────────────────────────
    [ObservableProperty] private float  _fps;
    [ObservableProperty] private int    _playerCount;
    [ObservableProperty] private int    _zombieCount;
    [ObservableProperty] private int    _animalCount;
    [ObservableProperty] private int    _entityCount;
    [ObservableProperty] private int    _heapUsedMb;
    [ObservableProperty] private int    _heapMaxMb  = 100;
    [ObservableProperty] private int    _heapPercent;
    [ObservableProperty] private int    _rssMb;
    [ObservableProperty] private int    _chunks;
    [ObservableProperty] private int    _cgo;
    [ObservableProperty] private int    _items;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOffline))]
    private bool _isConnected;

    public bool IsOffline => !IsConnected;

    // ── System info (read once) ───────────────────────────────────────────────
    public string DeviceName      { get; }
    public string OsDescription   { get; }
    public string ProcessorName   { get; }
    public string ProcessorCount  { get; }
    public string TotalRam        { get; }

    public OverviewViewModel(Server server)
    {
        _server = server;
        _poller = new MetricsPoller(server);
        _poller.Updated      += OnMetrics;
        _poller.Connected    += () => IsConnected = true;
        _poller.Disconnected += () => IsConnected = false;

        ServerIp = ResolveLocalIp();

        GameMode       = server.ExtraConfig.FirstOrDefault(p => p.Name == "GameMode")?.Value ?? "Survival";
        GameDifficulty = DescribeDifficulty(server.SandboxCode);

        // System info
        DeviceName     = Environment.MachineName;
        OsDescription  = GetOsDescription();
        ProcessorName  = GetProcessorName();
        ProcessorCount = $"{Environment.ProcessorCount} cores";
        TotalRam       = $"{GetTotalRamGb()} GB";

        // Uptime ticker
        _uptimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _uptimeTimer.Tick += (_, _) => RefreshUptime();
        _uptimeTimer.Start();
        RefreshUptime();
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        await _poller.StartAsync(ct);
    }

    private void OnMetrics(MetricsPoller.Snapshot snap)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            Fps          = snap.Fps;
            PlayerCount  = snap.Players;
            ZombieCount  = snap.Zombies;
            EntityCount  = snap.Entities;
            AnimalCount  = Math.Max(0, snap.Entities - snap.Players - snap.Zombies);
            HeapUsedMb   = snap.HeapUsedMb;
            HeapMaxMb    = snap.HeapMaxMb > 0 ? snap.HeapMaxMb : 100;
            HeapPercent  = HeapMaxMb > 0 ? (int)Math.Round(snap.HeapUsedMb * 100.0 / HeapMaxMb) : 0;
            RssMb        = snap.RssMb;
            Chunks       = snap.Chunks;
            Cgo          = snap.Cgo;
            Items        = snap.Items;
            GameTime     = snap.GameTime;
            ServerVersion = snap.ServerVersion;
        });
    }

    private void RefreshUptime()
    {
        if (_server.Status != ServerStatus.Running) { ServerUptime = "—"; return; }

        // Prefer the real OS process start time so uptime is accurate even when
        // the server was launched before this app session.
        DateTime? startUtc = null;
        if (_server.LastPid is { } pid)
        {
            try { startUtc = System.Diagnostics.Process.GetProcessById(pid).StartTime.ToUniversalTime(); }
            catch { /* process may have just exited */ }
        }
        startUtc ??= _server.ServerStartTime; // fallback: stored in LiteDB as UTC now

        if (startUtc is not { } start) { ServerUptime = "—"; return; }

        var elapsed = DateTime.UtcNow - start;
        ServerUptime = elapsed.TotalSeconds > 0
            ? $"{(int)elapsed.TotalHours:D2}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}"
            : "—";
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Name the difficulty behind a sandbox code.
    ///
    /// V3.0 has no GameDifficulty property any more — the six difficulty levels are just
    /// presets over the damage multipliers. Two questions, so two comparisons:
    ///
    /// <list type="bullet">
    ///   <item>Which difficulty TIER is it? Compare only the six always-written fields, so a
    ///     config that picked Warrior and then raised the loot rate still reads as Warrior.</item>
    ///   <item>Has it been tweaked? Compare the WHOLE state — any difference at all, including
    ///     an option the preset never touched, means it's no longer stock.</item>
    /// </list>
    ///
    /// So a tweaked Warrior reports "Warrior (modified)": you get the tier and the fact that
    /// it isn't the stock preset.
    /// </summary>
    private static string DescribeDifficulty(string? sandboxCode)
    {
        var mine = SandboxCodeService.Decode(sandboxCode);
        if (mine.Count == 0) return "Adventurer";   // empty code = the game's default

        var keyIds = SandboxSettings.AlwaysWritten
            .Select(SandboxSettings.ByName)
            .Where(o => o is not null)
            .Select(o => o!.Id)
            .ToList();

        foreach (var preset in SandboxSettings.Presets.Where(p => p.IsDifficulty))
        {
            var theirs = SandboxCodeService.Decode(preset.Code);

            var sameTier = keyIds.All(id =>
                mine.TryGetValue(id, out var a) &&
                theirs.TryGetValue(id, out var b) &&
                a == b);

            if (!sameTier) continue;

            var stock = mine.Count == theirs.Count &&
                        mine.All(kv => theirs.TryGetValue(kv.Key, out var v) && v == kv.Value);

            return stock ? preset.Name : $"{preset.Name} (modified)";
        }

        return "Custom";
    }

    private static string ResolveLocalIp()
    {
        try
        {
            var entry = Dns.GetHostEntry(Environment.MachineName);
            var ip = entry.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            return ip?.ToString() ?? "127.0.0.1";
        }
        catch { return "127.0.0.1"; }
    }

    private static string GetOsDescription()
    {
        try
        {
            var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key is null) return RuntimeInformation.OSDescription;

            var product = key.GetValue("ProductName")?.ToString() ?? "";
            var display = key.GetValue("DisplayVersion")?.ToString() ?? "";
            var build   = key.GetValue("CurrentBuildNumber")?.ToString() ?? "";

            // ProductName often says "Windows 10" even on Win 11 OEM installs.
            // Build ≥ 22000 is definitively Windows 11.
            if (int.TryParse(build, out int buildNum) && buildNum >= 22000)
                product = product.Replace("Windows 10", "Windows 11");

            return string.IsNullOrWhiteSpace(display)
                ? $"{product} (Build {build})"
                : $"{product} {display} (Build {build})";
        }
        catch { return RuntimeInformation.OSDescription; }
    }

    private static string GetProcessorName()
    {
        try
        {
            var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            return key?.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? "Unknown";
        }
        catch { return "Unknown"; }
    }

    private static int GetTotalRamGb()
    {
        try
        {
            var s = new MEMORYSTATUSEX { dwLength = 64 };
            return GlobalMemoryStatusEx(ref s) ? (int)(s.ullTotalPhys / 1_073_741_824UL) : 0;
        }
        catch { return 0; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint  dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile,
                     ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll")]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    public async ValueTask DisposeAsync()
    {
        _uptimeTimer.Stop();
        await _poller.DisposeAsync();
    }
}
