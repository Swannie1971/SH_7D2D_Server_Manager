using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevenDaysManager.Models;
using SevenDaysManager.Services;

namespace SevenDaysManager.ViewModels;

public partial class ConsoleViewModel : ObservableObject, IAsyncDisposable
{
    private readonly Server       _server;
    private readonly TelnetClient _telnet = new();
    private readonly List<string> _history = new();
    private int _historyIndex = -1;

    private const int MaxLines = 2000;

    public ObservableCollection<ConsoleLine> Lines { get; } = new();

    [ObservableProperty] private string _commandInput  = "";
    [ObservableProperty] private bool   _isConnected;
    [ObservableProperty] private string _statusText    = "Connecting…";

    public string ServerName => _server.Name;

    public ConsoleViewModel(Server server)
    {
        _server = server;
        _telnet.LineReceived  += OnLineReceived;
        _telnet.Disconnected  += OnDisconnected;
    }

    // ── Startup ───────────────────────────────────────────────────────────────

    public async Task StartAsync()
    {
        // Show tail of the log file while Telnet connects
        LoadLogFileTail();

        AddLine($"Connecting to {_server.Name} on 127.0.0.1:{_server.TelnetPort}…", ConsoleLineType.System);

        var ok = await _telnet.ConnectAsync("127.0.0.1", _server.TelnetPort, _server.TelnetPassword);
        if (ok)
        {
            IsConnected = true;
            StatusText  = "Connected";
            AddLine("── Telnet connected ──", ConsoleLineType.System);
        }
        else
        {
            StatusText = "Not connected — server may still be starting";
            AddLine("Could not connect via Telnet. Server may still be starting up.", ConsoleLineType.Warning);
            AddLine("Close and re-open this window once the server is running.", ConsoleLineType.Warning);
        }
    }

    // ── Send command ──────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SendCommandAsync()
    {
        var cmd = CommandInput?.Trim() ?? "";
        if (string.IsNullOrEmpty(cmd)) return;

        // Push to history (deduplicate consecutive)
        if (_history.Count == 0 || _history[0] != cmd)
            _history.Insert(0, cmd);
        _historyIndex = -1;
        CommandInput = "";

        AddLine($"> {cmd}", ConsoleLineType.System);
        await _telnet.SendAsync(cmd);
    }

    // ── Command history (Up / Down) ───────────────────────────────────────────

    public void HistoryUp()
    {
        if (_history.Count == 0) return;
        _historyIndex = Math.Min(_historyIndex + 1, _history.Count - 1);
        CommandInput = _history[_historyIndex];
    }

    public void HistoryDown()
    {
        if (_historyIndex <= 0) { _historyIndex = -1; CommandInput = ""; return; }
        _historyIndex--;
        CommandInput = _history[_historyIndex];
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void OnLineReceived(string line)
    {
        var type = ClassifyLine(line);
        Application.Current.Dispatcher.Invoke(() => AddLine(line, type));
    }

    private void OnDisconnected()
    {
        IsConnected = false;
        StatusText  = "Disconnected";
        Application.Current.Dispatcher.Invoke(() =>
            AddLine("── Telnet disconnected ──", ConsoleLineType.System));
    }

    private void AddLine(string text, ConsoleLineType type)
    {
        Lines.Add(new ConsoleLine(text, type));
        if (Lines.Count > MaxLines)
            Lines.RemoveAt(0);
    }

    private void LoadLogFileTail()
    {
        var path = _server.ServerLogPath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;
        try
        {
            var lines = File.ReadAllLines(path).TakeLast(80);
            foreach (var l in lines)
                AddLine(l, ClassifyLine(l));
            AddLine("── (end of log file) ──", ConsoleLineType.System);
        }
        catch { /* best-effort */ }
    }

    private static ConsoleLineType ClassifyLine(string line)
    {
        if (line.Contains(" ERR ", StringComparison.Ordinal))   return ConsoleLineType.Error;
        if (line.Contains(" WRN ", StringComparison.Ordinal))   return ConsoleLineType.Warning;
        if (line.Contains("CHAT", StringComparison.OrdinalIgnoreCase)) return ConsoleLineType.Chat;
        return ConsoleLineType.Info;
    }

    public async ValueTask DisposeAsync() => await _telnet.DisposeAsync();
}
