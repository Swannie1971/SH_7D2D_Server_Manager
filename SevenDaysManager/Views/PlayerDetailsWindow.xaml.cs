using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SevenDaysManager.Models;
using SevenDaysManager.ViewModels;

namespace SevenDaysManager.Views;

public partial class PlayerDetailsWindow : Window
{
    private readonly PlayerInfo         _player;
    private readonly PlayersViewModel   _playersVm;

    public PlayerDetailsWindow(PlayerInfo player, PlayersViewModel playersVm)
    {
        InitializeComponent();
        _player    = player;
        _playersVm = playersVm;

        TitleText.Text = player.Name;

        // Only fields we can reliably read from the server's "lp" telnet output.
        AddCell("PLAYER NAME",      player.Name);
        AddCell("ENTITY ID",        player.EntityId.ToString());
        AddCell("PLAYER ID",        string.IsNullOrEmpty(player.CrossId)    ? "—" : player.CrossId);
        AddCell("PLATFORM ID",      string.IsNullOrEmpty(player.PlatformId) ? "—" : player.PlatformId);
        AddCell("CONNECTION",       player.Connection);
        AddCell("CURRENT POSITION", string.IsNullOrEmpty(player.Position)   ? "—" : player.Position);
        AddCell("LEVEL",            player.Level.ToString());
        AddCell("HEALTH",           player.Health.ToString());
        AddCell("SCORE",            player.Score.ToString());
        AddCell("DEATHS",           player.Deaths.ToString());
        AddCell("ZOMBIE KILLS",     player.Zombies.ToString());
        AddCell("PLAYER KILLS",     player.PlayerKills.ToString());
        AddCell("PING",             $"{player.Ping} ms");
        AddCell("IP ADDRESS",       string.IsNullOrEmpty(player.Ip)         ? "—" : player.Ip);
    }

    private void AddCell(string label, string value)
    {
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text  = label,
            Style = (Style)FindResource("InfoLabel")
        });
        panel.Children.Add(new TextBlock
        {
            Text  = value,
            Style = (Style)FindResource("InfoValue")
        });

        InfoGrid.Children.Add(new Border
        {
            Background      = (Brush)FindResource("Hud.Surface"),
            BorderBrush     = (Brush)FindResource("Hud.Border"),
            BorderThickness = new Thickness(1),
            Margin          = new Thickness(0, 0, 8, 8),
            Padding         = new Thickness(12, 9, 12, 9),
            Child           = panel
        });
    }

    // Borderless window: the header bar is the only thing left to drag by.
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private async void GrantAdmin_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(AdminLevelBox.Text.Trim(), out var level) || level < 0)
        {
            SetStatus("Enter a valid admin level (0 or higher).", ok: false);
            return;
        }

        SetButtonsEnabled(false);
        var ok = await _playersVm.SetAdminLevelAsync(_player.EntityId, level);
        SetStatus(ok ? $"Sent: admin add {_player.EntityId} {level}"
                     : "Not connected to server telnet.", ok);
        SetButtonsEnabled(true);
    }

    private async void RevokeAdmin_Click(object sender, RoutedEventArgs e)
    {
        SetButtonsEnabled(false);
        var ok = await _playersVm.RemoveAdminAsync(_player.EntityId);
        SetStatus(ok ? $"Sent: admin remove {_player.EntityId}"
                     : "Not connected to server telnet.", ok);
        SetButtonsEnabled(true);
    }

    // A failure rendered in the same neutral grey as a success is easy to miss.
    private void SetStatus(string message, bool ok)
    {
        AdminStatusText.Text       = message;
        AdminStatusText.Foreground = (Brush)FindResource(ok ? "Hud.Green" : "Hud.Red");
    }

    private void SetButtonsEnabled(bool enabled)
    {
        GrantAdminButton.IsEnabled  = enabled;
        RevokeAdminButton.IsEnabled = enabled;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
