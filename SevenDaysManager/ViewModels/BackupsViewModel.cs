using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevenDaysManager.Models;
using SevenDaysManager.Services;

using SevenDaysManager.Views;

namespace SevenDaysManager.ViewModels;

public partial class BackupsViewModel : ObservableObject
{
    private readonly Server        _server;
    private readonly BackupService _svc = new();

    public ObservableCollection<BackupInfo> Backups { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBackupSelected))]
    private BackupInfo? _selectedBackup;

    [ObservableProperty] private bool   _isBusy;
    [ObservableProperty] private string _saveFolderSource    = "";
    [ObservableProperty] private string _saveFolderPath      = "";
    [ObservableProperty] private string _worldName           = "";
    [ObservableProperty] private string _generatedWorldPath  = "";
    [ObservableProperty] private bool   _hasGeneratedWorld;
    [ObservableProperty] private string _backupFolderPath    = "";
    [ObservableProperty] private bool   _saveFolderExists;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusText))]
    private string _statusText = "";
    public bool HasStatusText    => !string.IsNullOrEmpty(StatusText);
    public bool IsBackupSelected => SelectedBackup is not null;
    public bool IsServerRunning  => _server.Status == ServerStatus.Running;
    public bool HasManualSaveDir => !string.IsNullOrEmpty(_server.SaveDir);

    // True when saves are going to %APPDATA% because UserDataFolder is not configured
    public bool IsUsingAppData =>
        string.IsNullOrWhiteSpace(_server.SaveDir) &&
        !_server.ExtraConfig.Any(p => p.Name.Equals("UserDataFolder", StringComparison.OrdinalIgnoreCase)) &&
        SaveFolderPath.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            StringComparison.OrdinalIgnoreCase);

    public BackupsViewModel(Server server)
    {
        _server          = server;
        BackupFolderPath = BackupService.GetBackupFolder(server);
        ResolveSaveFolder();
        Refresh();
    }

    private void ResolveSaveFolder()
    {
        var (folder, world) = BackupService.FindSaveFolder(_server);

        SaveFolderPath = folder;
        WorldName      = world;

        SaveFolderSource = !string.IsNullOrWhiteSpace(_server.SaveDir) ? "manual"
                         : !string.IsNullOrWhiteSpace(world)           ? "auto-detected"
                                                                        : "fallback";

        SaveFolderExists = Directory.Exists(folder);

        var genPath = BackupService.FindGeneratedWorldFolder(_server, world);
        GeneratedWorldPath = genPath ?? "";
        HasGeneratedWorld  = genPath is not null;

        OnPropertyChanged(nameof(HasManualSaveDir));
        OnPropertyChanged(nameof(IsUsingAppData));
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private void Refresh()
    {
        SelectedBackup = null;
        Backups.Clear();
        foreach (var b in _svc.ListBackups(_server))
            Backups.Add(b);
        ResolveSaveFolder();
        StatusText = "";
    }

    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateBackupAsync()
    {
        IsBusy = true; StatusText = "";
        try
        {
            var progress = new Progress<string>(msg => StatusText = msg);
            await _svc.CreateBackupAsync(_server, progress);
            Refresh();
        }
        catch (Exception ex)
        {
            StatusText = $"Backup failed: {ex.Message}";
        }
        finally { IsBusy = false; }
    }
    private bool CanCreate() => !IsBusy && SaveFolderExists;

    partial void OnIsBusyChanged(bool value)            => CreateBackupCommand.NotifyCanExecuteChanged();
    partial void OnSaveFolderExistsChanged(bool value)  => CreateBackupCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanRestore))]
    private async Task RestoreBackupAsync()
    {
        if (SelectedBackup is not { } backup) return;

        if (IsServerRunning)
        {
            HudDialog.Show("Stop the server before restoring a backup.",
                "Server is running", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = HudDialog.Show(
            $"Restore \"{backup.FileName}\"?\n\nThis will replace save data and generated world files. This cannot be undone.",
            "Confirm Restore", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;

        IsBusy = true; StatusText = "";
        try
        {
            var progress = new Progress<string>(msg => StatusText = msg);
            await _svc.RestoreBackupAsync(_server, backup, progress);
        }
        catch (Exception ex)
        {
            StatusText = $"Restore failed: {ex.Message}";
        }
        finally { IsBusy = false; }
    }
    private bool CanRestore() => !IsBusy && IsBackupSelected;

    partial void OnSelectedBackupChanged(BackupInfo? value)
    {
        RestoreBackupCommand.NotifyCanExecuteChanged();
        DeleteBackupCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(IsBackupSelected))]
    private void DeleteBackup()
    {
        if (SelectedBackup is not { } backup) return;

        var result = HudDialog.Show($"Permanently delete \"{backup.FileName}\"?",
            "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        _svc.DeleteBackup(backup);
        Refresh();
    }

    [RelayCommand]
    private void OpenBackupFolder()
    {
        Directory.CreateDirectory(BackupFolderPath);
        Process.Start("explorer.exe", BackupFolderPath);
    }

    [RelayCommand]
    private void BrowseSaveFolder()
    {
        var dlg = new OpenFolderDialog
        {
            Title            = "Select the save folder for this game — it should contain Player\\ and Region\\ subfolders",
            InitialDirectory = Directory.Exists(SaveFolderPath) ? SaveFolderPath : ""
        };

        if (dlg.ShowDialog() != true) return;

        var chosen = dlg.FolderName;

        if (chosen.Equals(BackupFolderPath, StringComparison.OrdinalIgnoreCase))
        {
            StatusText = "That is the Backup folder — the save folder contains Player\\ and Region\\ subfolders.";
            return;
        }

        _server.SaveDir = chosen;
        App.DataStore.SaveServer(_server);
        ResolveSaveFolder();
        CreateBackupCommand.NotifyCanExecuteChanged();
        Refresh();
    }

    [RelayCommand]
    private void SetUserDataFolder()
    {
        // Suggest <InstallDir>\ServerData as the new UserDataFolder
        var suggested = Path.Combine(_server.InstallDir, "ServerData");

        var result = HudDialog.Show(
            $"This will add UserDataFolder to your serverconfig.xml:\n\n{suggested}\n\n" +
            "All future saves will go there. Restart the server after applying.\n\n" +
            "If the server has existing saves in %APPDATA%, you will need to move them manually.",
            "Set UserDataFolder", MessageBoxButton.OKCancel, MessageBoxImage.Information);

        if (result != MessageBoxResult.OK) return;

        // Remove old entry if present, add new one
        var cfg = _server.ExtraConfig.ToList();
        cfg.RemoveAll(p => p.Name.Equals("UserDataFolder", StringComparison.OrdinalIgnoreCase));
        cfg.Add(new ConfigProperty("UserDataFolder", suggested));
        _server.ExtraConfig = cfg;
        App.DataStore.SaveServer(_server);

        // Write to serverconfig.xml
        new ServerConfigService().WriteConfig(_server);

        StatusText = $"UserDataFolder set to {suggested} — restart the server to apply.";
        ResolveSaveFolder();
    }

    [RelayCommand]
    private void ResetSaveFolder()
    {
        _server.SaveDir = "";
        App.DataStore.SaveServer(_server);
        ResolveSaveFolder();
        CreateBackupCommand.NotifyCanExecuteChanged();
    }
}
