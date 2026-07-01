using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SevenDaysManager.Models;

namespace SevenDaysManager.Views;

public partial class PlayerDetailsWindow : Window
{
    public PlayerDetailsWindow(PlayerInfo player)
    {
        InitializeComponent();

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

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
