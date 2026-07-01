using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
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

        TitleText.Text = $"Player: {player.Name}";

        // Only fields we can reliably read from the server's "lp" telnet output.
        AddCell("Player Name",     player.Name);
        AddCell("Entity ID",       player.EntityId.ToString());
        AddCell("Player ID",       string.IsNullOrEmpty(player.CrossId)  ? "—" : player.CrossId);
        AddCell("Platform ID",     string.IsNullOrEmpty(player.PlatformId) ? "—" : player.PlatformId);
        AddCell("Connection",      player.Connection);
        AddCell("Current Position", string.IsNullOrEmpty(player.Position) ? "—" : player.Position);
        AddCell("Level",           player.Level.ToString());
        AddCell("Health",          player.Health.ToString());
        AddCell("Score",           player.Score.ToString());
        AddCell("Deaths",          player.Deaths.ToString());
        AddCell("Zombie Kills",    player.Zombies.ToString());
        AddCell("Player Kills",    player.PlayerKills.ToString());
        AddCell("Ping",            $"{player.Ping} ms");
        AddCell("IP Address",      string.IsNullOrEmpty(player.Ip) ? "—" : player.Ip);
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
            Background    = new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)),
            CornerRadius  = new CornerRadius(6),
            Margin        = new Thickness(4),
            Padding       = new Thickness(14, 10, 14, 10),
            Child         = panel
        });
    }

    private async void GrantAdmin_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(AdminLevelBox.Text.Trim(), out var level) || level < 0)
        {
            AdminStatusText.Text = "Enter a valid admin level (0 or higher).";
            return;
        }

        SetButtonsEnabled(false);
        var ok = await _playersVm.SetAdminLevelAsync(_player.EntityId, level);
        AdminStatusText.Text = ok
            ? $"Sent: admin add {_player.EntityId} {level}"
            : "Not connected to server telnet.";
        SetButtonsEnabled(true);
    }

    private async void RevokeAdmin_Click(object sender, RoutedEventArgs e)
    {
        SetButtonsEnabled(false);
        var ok = await _playersVm.RemoveAdminAsync(_player.EntityId);
        AdminStatusText.Text = ok
            ? $"Sent: admin remove {_player.EntityId}"
            : "Not connected to server telnet.";
        SetButtonsEnabled(true);
    }

    private void SetButtonsEnabled(bool enabled)
    {
        GrantAdminButton.IsEnabled  = enabled;
        RevokeAdminButton.IsEnabled = enabled;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
