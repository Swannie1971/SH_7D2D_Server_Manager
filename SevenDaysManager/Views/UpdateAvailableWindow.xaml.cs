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
            NotesLabel.Visibility = Visibility.Visible;
            NotesPanel.Visibility = Visibility.Visible;
        }

        // No direct download URL (release has no .exe attached) — fall back to opening GitHub.
        if (string.IsNullOrEmpty(update.DownloadUrl))
            DownloadButtonText.Text = "OPEN GITHUB";
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
        StatusText.Text          = "DOWNLOADING…";

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

            Dispatcher.Invoke(() => StatusText.Text = "INSTALLING…");

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
            // Turn the status line red — an accent-green failure message reads as success.
            StatusText.Text          = $"ERROR: {ex.Message}";
            StatusText.Foreground    = (System.Windows.Media.Brush)FindResource("Hud.Red");
            DownloadButton.IsEnabled = true;
            NotNowButton.IsEnabled   = true;
            ProgressBar.IsIndeterminate = false;
        }
    }

    private void NotNow_Click(object sender, RoutedEventArgs e) => Close();
}
