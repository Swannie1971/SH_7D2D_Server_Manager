using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SevenDaysManager.Models;
using SevenDaysManager.ViewModels;

namespace SevenDaysManager.Views;

public partial class PlayersView : UserControl
{
    public PlayersView() => InitializeComponent();

    // Right-click selects the row, so SelectedPlayer is current when the menu opens
    private void Row_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is DataGridRow row) row.IsSelected = true;
    }

    private void PlayerInfo_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not PlayersViewModel vm || vm.SelectedPlayer is not PlayerInfo player) return;

        new PlayerDetailsWindow(player, vm)
        {
            Owner = Window.GetWindow(this)
        }.ShowDialog();
    }
}
