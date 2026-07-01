namespace SevenDaysManager.Models;

public class PlayerInfo
{
    public int    EntityId { get; set; }
    public string Name     { get; set; } = "";
    public string SteamId  { get; set; } = "";   // platform id, e.g. Steam_7656...
    public string CrossId  { get; set; } = "";   // cross-platform id, e.g. EOS_...
    public string Ip       { get; set; } = "";
    public int    Ping     { get; set; }
    public int    Health   { get; set; }
    public int    Level    { get; set; }
    public int    Deaths   { get; set; }
    public int    Zombies  { get; set; }
    public int    Score    { get; set; }
    public int    PlayerKills { get; set; }
    public string Position { get; set; } = "";
    public bool   Remote   { get; set; }          // true = remote client, false = local host

    public string Connection => Remote ? "Remote" : "Local";
    public string PlatformId => SteamId;
}
