using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevenDaysManager.Models;
using SevenDaysManager.Services;

namespace SevenDaysManager.ViewModels;

public partial class ScheduleViewModel : ObservableObject, IAsyncDisposable
{
    private readonly Server          _server;
    private readonly ScheduleService _svc;
    private readonly DispatcherTimer _countdownTimer;

    public ObservableCollection<string> ActivityLog { get; } = new();

    // ── Schedule settings ─────────────────────────────────────────────────────

    [ObservableProperty] private bool   _enabled;
    [ObservableProperty] private bool   _modeInterval = true;
    [ObservableProperty] private bool   _modeDaily;
    [ObservableProperty] private int    _intervalHours = 6;
    [ObservableProperty] private string _newDailyTime  = "00:00";
    public ObservableCollection<string> DailyTimes { get; } = new();

    [ObservableProperty] private bool _warn60 = false;
    [ObservableProperty] private bool _warn30 = true;
    [ObservableProperty] private bool _warn15 = false;
    [ObservableProperty] private bool _warn10 = true;
    [ObservableProperty] private bool _warn5  = true;
    [ObservableProperty] private bool _warn1  = true;

    [ObservableProperty] private string _inGameWarning = "";
    [ObservableProperty] private string _inGameNow     = "";

    // ── Status display ────────────────────────────────────────────────────────

    [ObservableProperty] private string _countdown     = "—";
    [ObservableProperty] private string _nextRestartAt = "—";
    [ObservableProperty] private bool   _isBusy;

    public ScheduleViewModel(Server server, Func<Task> stopServer, Func<Task> startServer)
    {
        _server = server;
        server.Schedule ??= new ScheduleConfig();

        _svc = new ScheduleService(server, stopServer, startServer);

        _svc.LogEntry += line => Application.Current.Dispatcher.Invoke(() =>
        {
            ActivityLog.Insert(0, line);
            if (ActivityLog.Count > 100) ActivityLog.RemoveAt(ActivityLog.Count - 1);
        });
        _svc.StateChanged += RefreshStatus;

        LoadFromConfig(server.Schedule);

        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += (_, _) => RefreshStatus();
        _countdownTimer.Start();
    }

    // ── Load / Save ───────────────────────────────────────────────────────────

    private void LoadFromConfig(ScheduleConfig cfg)
    {
        Enabled       = cfg.Enabled;
        ModeInterval  = cfg.Mode == ScheduleMode.Interval;
        ModeDaily     = cfg.Mode == ScheduleMode.Daily;
        IntervalHours = cfg.IntervalHours;

        DailyTimes.Clear();
        foreach (var t in cfg.DailyTimes) DailyTimes.Add(t);

        Warn60  = cfg.WarnMinutes.Contains(60);
        Warn30  = cfg.WarnMinutes.Contains(30);
        Warn15  = cfg.WarnMinutes.Contains(15);
        Warn10  = cfg.WarnMinutes.Contains(10);
        Warn5   = cfg.WarnMinutes.Contains(5);
        Warn1   = cfg.WarnMinutes.Contains(1);

        InGameWarning = cfg.InGameWarning;
        InGameNow     = cfg.InGameNow;

        if (cfg.Enabled) _svc.Apply(cfg);
    }

    private ScheduleConfig BuildConfig() => new()
    {
        Enabled       = Enabled,
        Mode          = ModeInterval ? ScheduleMode.Interval : ScheduleMode.Daily,
        IntervalHours = IntervalHours,
        DailyTimes    = DailyTimes.ToList(),
        WarnMinutes   = BuildWarnList(),
        InGameWarning = InGameWarning,
        InGameNow     = InGameNow,
    };

    private List<int> BuildWarnList()
    {
        var list = new List<int>();
        if (Warn60) list.Add(60);
        if (Warn30) list.Add(30);
        if (Warn15) list.Add(15);
        if (Warn10) list.Add(10);
        if (Warn5)  list.Add(5);
        if (Warn1)  list.Add(1);
        return list;
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private void ApplySchedule()
    {
        _server.Schedule = BuildConfig();
        App.DataStore.SaveServer(_server);
        _svc.Apply(_server.Schedule);
        RefreshStatus();
    }

    [RelayCommand]
    private async Task RestartNowAsync()
    {
        IsBusy = true;
        try { await _svc.TriggerRestartNowAsync(); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void AddDailyTime()
    {
        if (TimeOnly.TryParse(NewDailyTime, out _) && !DailyTimes.Contains(NewDailyTime))
        {
            var sorted = DailyTimes.Append(NewDailyTime).OrderBy(t => t).ToList();
            DailyTimes.Clear();
            foreach (var t in sorted) DailyTimes.Add(t);
        }
    }

    [RelayCommand]
    private void RemoveDailyTime(string time) => DailyTimes.Remove(time);

    // ── Status refresh ────────────────────────────────────────────────────────

    private void RefreshStatus()
    {
        if (_svc.NextRestartAt is { } next)
        {
            var rem = next - DateTime.Now;
            NextRestartAt = next.ToString("HH:mm:ss");
            Countdown = rem.TotalSeconds > 0
                ? $"{(int)rem.TotalHours:D2}:{rem.Minutes:D2}:{rem.Seconds:D2}"
                : "Restarting…";
        }
        else
        {
            Countdown     = "—";
            NextRestartAt = "—";
        }
    }

    public ValueTask DisposeAsync()
    {
        _countdownTimer.Stop();
        _svc.Dispose();
        return ValueTask.CompletedTask;
    }
}
