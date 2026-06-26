using System.Diagnostics;
using System.Windows;
using SevenDaysManager.Services;

namespace SevenDaysManager.Views;

public partial class UpdateAvailableWindow : Window
{
    private readonly string _releaseUrl;

    public UpdateAvailableWindow(UpdateService.UpdateInfo update)
    {
        InitializeComponent();
        _releaseUrl = update.ReleaseUrl;

        SubtitleText.Text = $"v{UpdateService.CurrentVersion}  →  v{update.LatestVersion}";

        if (!string.IsNullOrWhiteSpace(update.ReleaseNotes))
        {
            var notes = update.ReleaseNotes.Length > 400
                ? update.ReleaseNotes[..400] + "…"
                : update.ReleaseNotes;
            NotesText.Text = notes;
            NotesPanel.Visibility = Visibility.Visible;
        }
    }

    private void Download_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(_releaseUrl) { UseShellExecute = true });
        }
        catch { /* ignore */ }
        Close();
    }

    private void NotNow_Click(object sender, RoutedEventArgs e) => Close();
}
