using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevenDaysManager.Models;
using SevenDaysManager.Services;

namespace SevenDaysManager.ViewModels;

public partial class InstallProgressViewModel : ObservableObject
{
    private readonly SteamCmdService _steamCmd = new();
    private CancellationTokenSource? _cts;

    public ObservableCollection<string> LogLines { get; } = new();

    [ObservableProperty] private bool   _isRunning;
    [ObservableProperty] private string _statusMessage = "Starting install…";
    [ObservableProperty] private bool   _isComplete;
    [ObservableProperty] private bool   _hasFailed;

    // Download progress
    [ObservableProperty] private int    _downloadPercent;
    [ObservableProperty] private bool   _hasProgress;
    [ObservableProperty] private string _downloadStats = "";

    // SteamCMD self-update phase: "[ 71%] Downloading update (17,692 of 29,709 KB)..."
    private static readonly Regex SelfUpdateRegex =
        new(@"\[\s*(\d+)%\]\s+Downloading update \(([\d,]+) of ([\d,]+) KB\)",
            RegexOptions.Compiled);

    // Game download phase: "Update state (0x61) downloading, progress: 2.73 (465905134 / 17058703111)"
    private static readonly Regex GameDownloadRegex =
        new(@"progress:\s+([\d.]+)\s+\((\d+)\s*/\s*(\d+)\)",
            RegexOptions.Compiled);

    public string ServerName { get; }
    public string InstallDir { get; }

    public InstallProgressViewModel(Server server)
    {
        ServerName = server.Name;
        InstallDir = server.InstallDir;
    }

    public async Task StartAsync()
    {
        IsRunning = true;
        IsComplete = false;
        HasFailed = false;
        LogLines.Clear();
        _cts = new CancellationTokenSource();

        var progress = new Progress<string>(line =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                LogLines.Add(line);
                ParseProgress(line);
            });
        });

        try
        {
            StatusMessage = "Installing server files via SteamCMD…";
            var exitCode = await _steamCmd.InstallOrUpdateAsync(InstallDir, progress, _cts.Token);

            if (exitCode == 0)
            {
                LogLines.Add("");
                LogLines.Add("────────────────────────────────────────");

                var info = _steamCmd.GetInstallInfo(InstallDir);
                if (info != null)
                {
                    LogLines.Add($"  Build ID    : {info.BuildId}");
                    LogLines.Add($"  Install dir : {InstallDir}");
                    LogLines.Add(info.IsUpToDate
                        ? "  Status      : Up to date ✓"
                        : $"  Status      : Target build {info.TargetBuildId} — re-run to update");
                }
                else
                {
                    LogLines.Add($"  Install dir : {InstallDir}");
                    LogLines.Add("  Status      : Could not read build manifest");
                }

                LogLines.Add("────────────────────────────────────────");
                StatusMessage = info != null ? $"Complete — build {info.BuildId}" : "Installation complete.";
                IsComplete = true;
            }
            else
            {
                StatusMessage = $"SteamCMD exited with code {exitCode}.";
                HasFailed = true;
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Cancelled.";
            LogLines.Add("[CANCELLED] Install was cancelled.");
            HasFailed = true;
        }
        catch (Exception ex)
        {
            StatusMessage = "Install failed.";
            LogLines.Add($"[ERROR] {ex.Message}");
            HasFailed = true;
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void ParseProgress(string line)
    {
        // Game download (the big one) — report in MB.
        var g = GameDownloadRegex.Match(line);
        if (g.Success)
        {
            var pct        = double.Parse(g.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            var doneBytes  = long.Parse(g.Groups[2].Value);
            var totalBytes = long.Parse(g.Groups[3].Value);
            if (totalBytes <= 0) return;

            DownloadPercent = (int)Math.Round(pct);
            HasProgress     = true;
            DownloadStats   = $"{pct:F1}%  ·  {doneBytes / 1048576.0:F0} / {totalBytes / 1048576.0:F0} MB";
            return;
        }

        // SteamCMD self-update — values are in KB.
        var s = SelfUpdateRegex.Match(line);
        if (s.Success)
        {
            var pct       = int.Parse(s.Groups[1].Value);
            var doneKb    = long.Parse(s.Groups[2].Value.Replace(",", ""));
            var totalKb   = long.Parse(s.Groups[3].Value.Replace(",", ""));

            DownloadPercent = pct;
            HasProgress     = true;
            DownloadStats   = $"{pct}%  ·  {doneKb / 1024.0:F1} / {totalKb / 1024.0:F1} MB  (client update)";
        }
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();
}
