using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevenDaysManager.Models;
using SevenDaysManager.Services;

namespace SevenDaysManager.ViewModels;

public partial class ModsViewModel : ObservableObject, IAsyncDisposable
{
    private readonly Server     _server;
    private readonly ModService _svc = new();

    public ObservableCollection<ModInfo> Mods { get; } = new();

    [ObservableProperty] private bool   _isBusy;
    [ObservableProperty] private string _statusText   = "";
    [ObservableProperty] private int    _enabledCount;
    [ObservableProperty] private int    _totalCount;
    [ObservableProperty] private bool   _sortAscending = true;

    partial void OnSortAscendingChanged(bool _) => ApplySort();

    public ModsViewModel(Server server)
    {
        _server = server;
        _ = LoadAsync();
    }

    // ── Load ─────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var mods = await _svc.GetModsAsync(_server.InstallDir);
            Mods.Clear();
            foreach (var m in mods) Mods.Add(m);
            ApplySort();
            UpdateCounts();
            // Only overwrite StatusText if there's nothing already showing
            if (string.IsNullOrEmpty(StatusText))
                StatusText = Mods.Count == 0 ? "No mods installed." : "";
        }
        catch (Exception ex) { StatusText = $"Error scanning mods: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private void ApplySort()
    {
        var sorted = SortAscending
            ? Mods.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList()
            : Mods.OrderByDescending(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
        Mods.Clear();
        foreach (var m in sorted) Mods.Add(m);
    }

    // ── Enable / Disable ─────────────────────────────────────────────────────

    [RelayCommand]
    private void ToggleMod(ModInfo mod)
    {
        try
        {
            _svc.SetEnabled(mod, !mod.IsEnabled);
            // Force list refresh for the item — replace in collection
            var idx = Mods.IndexOf(mod);
            if (idx >= 0) { Mods.RemoveAt(idx); Mods.Insert(idx, mod); }
            UpdateCounts();
        }
        catch (Exception ex) { StatusText = $"Could not toggle mod: {ex.Message}"; }
    }

    // ── Install from zip ─────────────────────────────────────────────────────

    [RelayCommand]
    private async Task InstallFromZipAsync()
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Select mod zip file",
            Filter = "Zip archives|*.zip",
        };
        if (dlg.ShowDialog() != true) return;

        IsBusy     = true;
        StatusText = $"Installing {System.IO.Path.GetFileName(dlg.FileName)}…";
        try
        {
            var (ok, err) = await _svc.InstallFromZipAsync(_server.InstallDir, dlg.FileName);
            StatusText = ok ? "Mod installed successfully." : $"Install failed: {err}";
            if (ok) await LoadAsync();
        }
        finally { IsBusy = false; }
    }

    // ── Delete ───────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task DeleteModAsync(ModInfo mod)
    {
        IsBusy = true;
        try
        {
            await _svc.DeleteModAsync(mod);
            Mods.Remove(mod);
            UpdateCounts();
            StatusText = $"'{mod.Name}' deleted.";
        }
        catch (Exception ex) { StatusText = $"Delete failed: {ex.Message}"; }
        finally { IsBusy = false; }
    }

    // ── Open folder ──────────────────────────────────────────────────────────

    [RelayCommand]
    private void OpenModsFolder()
    {
        _svc.EnsureModsFolder(_server.InstallDir);
        Process.Start(new ProcessStartInfo
        {
            FileName        = _svc.GetModsPath(_server.InstallDir),
            UseShellExecute = true,
        });
    }

    // ── Open website ─────────────────────────────────────────────────────────

    [RelayCommand]
    private void OpenWebsite(ModInfo mod)
    {
        if (string.IsNullOrWhiteSpace(mod.Website)) return;
        if (!Uri.TryCreate(mod.Website, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return;
        Process.Start(new ProcessStartInfo { FileName = mod.Website, UseShellExecute = true });
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private void UpdateCounts()
    {
        TotalCount   = Mods.Count;
        EnabledCount = Mods.Count(m => m.IsEnabled);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
