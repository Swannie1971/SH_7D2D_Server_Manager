using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
using SevenDaysManager.Models;
using SevenDaysManager.Services;
using SevenDaysManager.ViewModels;

namespace SevenDaysManager.Views;

public partial class ConsoleWindow : Window
{
    private readonly ConsoleViewModel _vm;

    public ConsoleWindow(Server server)
    {
        InitializeComponent();
        _vm = new ConsoleViewModel(server);
        DataContext = _vm;

        _vm.Lines.CollectionChanged += Lines_CollectionChanged;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ThemeService.ApplyTitleBar(this);
    }

    protected override async void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        await _vm.StartAsync();
        CommandBox.Focus();
    }

    protected override async void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        await _vm.DisposeAsync();
    }

    // Auto-scroll to bottom when new lines arrive
    private void Lines_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
            OutputScroll.ScrollToEnd();
    }

    // Command history via Up/Down arrows; Enter submits
    private void CommandBox_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up:
                _vm.HistoryUp();
                CommandBox.CaretIndex = CommandBox.Text.Length;
                e.Handled = true;
                break;
            case Key.Down:
                _vm.HistoryDown();
                CommandBox.CaretIndex = CommandBox.Text.Length;
                e.Handled = true;
                break;
        }
    }
}
