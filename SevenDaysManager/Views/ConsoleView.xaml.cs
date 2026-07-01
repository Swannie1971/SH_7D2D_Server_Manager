using System.Collections.Specialized;
using System.Windows.Controls;
using System.Windows.Input;
using SevenDaysManager.ViewModels;

namespace SevenDaysManager.Views;

public partial class ConsoleView : UserControl
{
    public ConsoleView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ConsoleViewModel vm)
                vm.Lines.CollectionChanged += Lines_CollectionChanged;
        };
    }

    private void Lines_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
            OutputScroll.ScrollToEnd();
    }

    private void CommandBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ConsoleViewModel vm) return;
        switch (e.Key)
        {
            case Key.Enter:
                if (vm.SendCommandCommand.CanExecute(null))
                    vm.SendCommandCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Up:
                vm.HistoryUp();
                CommandBox.CaretIndex = CommandBox.Text.Length;
                e.Handled = true;
                break;
            case Key.Down:
                vm.HistoryDown();
                CommandBox.CaretIndex = CommandBox.Text.Length;
                e.Handled = true;
                break;
        }
    }
}
