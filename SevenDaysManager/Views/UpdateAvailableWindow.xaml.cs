using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using SevenDaysManager.Services;

namespace SevenDaysManager.Views;

public partial class UpdateAvailableWindow : Window
{
    private readonly UpdateService.UpdateInfo _update;

    private static readonly HttpClient _http = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "7D2D-Server-Manager" } }
    };

    public UpdateAvailableWindow(UpdateService.UpdateInfo update)
    {
        InitializeComponent();
        _update = update;

        SubtitleText.Text = $"v{UpdateService.CurrentVersion}  →  v{update.LatestVersion}";

        if (!string.IsNullOrWhiteSpace(update.ReleaseNotes))
        {
            var notes = update.ReleaseNotes.Length > 400
                ? update.ReleaseNotes[..400] + "…"
                : update.ReleaseNotes;
            NotesText.Text = notes;
            NotesPanel.Visibility = Visibility.Visible;
        }

        // If we don't have a direct download URL, label button as "Open GitHub"
        if (string.IsNullOrEmpty(update.DownloadUrl))
            DownloadButtonText.Text = "Open GitHub";
    }

    private async void Download_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_update.DownloadUrl))
        {
            try { Process.Start(new ProcessStartInfo(_update.ReleaseUrl) { UseShellExecute = true }); }
            catch { /* ignore */ }
            Close();
            return;
        }

        DownloadButton.IsEnabled = false;
        NotNowButton.IsEnabled   = false;
        ProgressPanel.Visibility = Visibility.Visible;
        StatusText.Text          = "Downloading…";

        try
        {
            var tempExe    = Path.Combine(Path.GetTempPath(), "SevenDaysManager_update.exe");
            var currentExe = Environment.ProcessPath!;

            using var response = await _http.GetAsync(_update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? -1L;
            ProgressBar.IsIndeterminate = total < 0;

            await using var src = await response.Content.ReadAsStreamAsync();
            await using var dst = File.Create(tempExe);

            var buffer     = new byte[81920];
            long downloaded = 0;
            int  read;
            while ((read = await src.ReadAsync(buffer)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, read));
                downloaded += read;
                if (total > 0)
                {
                    var pct = (double)downloaded / total * 100;
                    Dispatcher.Invoke(() =>
                    {
                        ProgressBar.Value = pct;
                        StatusText.Text   = $"{downloaded / 1_048_576.0:F1} / {total / 1_048_576.0:F1} MB";
                    });
                }
            }

            Dispatcher.Invoke(() => StatusText.Text = "Installing…");

            // Write a swap script: wait for this process to exit, copy new exe over old, relaunch
            var scriptPath = Path.Combine(Path.GetTempPath(), "7d2d_update.cmd");
            File.WriteAllText(scriptPath,
                $"""
                @echo off
                timeout /t 2 /nobreak >nul
                copy /y "{tempExe}" "{currentExe}"
                if errorlevel 1 (
                    echo Update failed - could not replace exe. & pause
                    exit /b 1
                )
                start "" "{currentExe}"
                del "{tempExe}"
                del "%~f0"
                """);

            Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{scriptPath}\"")
            {
                CreateNoWindow  = true,
                UseShellExecute = false
            });

            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            StatusText.Text          = $"Error: {ex.Message}";
            DownloadButton.IsEnabled = true;
            NotNowButton.IsEnabled   = true;
            ProgressBar.IsIndeterminate = false;
        }
    }

    private void NotNow_Click(object sender, RoutedEventArgs e) => Close();
}
