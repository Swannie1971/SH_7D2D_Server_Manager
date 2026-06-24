using System.IO;
using LiteDB;
using SevenDaysManager.Models;

namespace SevenDaysManager.Services;

public sealed class DataStore : IDisposable
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<Server> _servers;
    private readonly ILiteCollection<AppSettings> _settings;

    private static readonly string DbPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "7D2DManager", "data.db");

    public DataStore()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(DbPath)!);
        _db = new LiteDatabase(DbPath);

        _servers = _db.GetCollection<Server>("servers");
        _servers.EnsureIndex(s => s.Id, unique: true);

        _settings = _db.GetCollection<AppSettings>("appsettings");
    }

    // ── Servers ──────────────────────────────────────────────────────────────

    public IReadOnlyList<Server> GetAllServers() =>
        _servers.FindAll().OrderBy(s => s.CreatedAt).ToList();

    public Server? GetServer(string id) =>
        _servers.FindOne(s => s.Id == id);

    public void SaveServer(Server server)
    {
        server.ExtraConfig ??= [];
        if (_servers.FindOne(s => s.Id == server.Id) is null)
            _servers.Insert(server);
        else
            _servers.Update(server);
    }

    public void DeleteServer(string id) =>
        _servers.DeleteMany(s => s.Id == id);

    // ── App Settings ─────────────────────────────────────────────────────────

    public AppSettings GetAppSettings() =>
        _settings.FindOne(s => s.Id == "singleton") ?? new AppSettings();

    public void SaveAppSettings(AppSettings settings)
    {
        settings.Id = "singleton";
        if (_settings.FindOne(s => s.Id == "singleton") is null)
            _settings.Insert(settings);
        else
            _settings.Update(settings);
    }

    public void Dispose() => _db.Dispose();
}
