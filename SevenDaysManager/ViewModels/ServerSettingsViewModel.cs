using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SevenDaysManager.Models;

namespace SevenDaysManager.ViewModels;

public partial class ServerSettingsViewModel : ObservableObject
{
    private readonly Server _server;
    private readonly Action<Server> _onDelete;

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _logoPath = "";
    [ObservableProperty] private bool   _autoStart;
    [ObservableProperty] private string _saveError = "";
    [ObservableProperty] private bool _hasSaveError;
    [ObservableProperty] private bool _confirmDelete;
    [ObservableProperty] private bool _deleteFiles;

    public string InstallDir => _server.InstallDir;

    public ServerSettingsViewModel(Server server, Action<Server> onDelete)
    {
        _server    = server;
        _onDelete  = onDelete;
        _name      = server.Name;
        _logoPath  = server.LogoPath;
        _autoStart = server.AutoStart;
    }

    [RelayCommand]
    private void BrowseLogo()
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Choose server banner image",
            Filter = "Images|*.jpg;*.jpeg;*.png;*.webp;*.bmp|All files|*.*"
        };
        if (dlg.ShowDialog() == true) LogoPath = dlg.FileName;
    }

    [RelayCommand]
    private void Save()
    {
        var name = Name?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(name))
        {
            SaveError    = "Server name cannot be empty.";
            HasSaveError = true;
            return;
        }
        _server.Name      = name;
        _server.LogoPath  = LogoPath?.Trim() ?? "";
        _server.AutoStart = AutoStart;
        App.DataStore.SaveServer(_server);
        HasSaveError = false;
        SaveError    = "";
    }

    [RelayCommand]
    private void Delete()
    {
        if (!ConfirmDelete)
        {
            ConfirmDelete = true;
            return;
        }

        if (DeleteFiles && Directory.Exists(_server.InstallDir))
        {
            try { Directory.Delete(_server.InstallDir, recursive: true); }
            catch { /* best-effort */ }
        }

        _onDelete(_server);
    }

    [RelayCommand]
    private void CancelDelete()
    {
        ConfirmDelete = false;
        DeleteFiles   = false;
    }
}
