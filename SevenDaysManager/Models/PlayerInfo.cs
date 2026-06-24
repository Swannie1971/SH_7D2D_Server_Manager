namespace SevenDaysManager.Models;

public class PlayerInfo
{
    public int    EntityId { get; set; }
    public string Name     { get; set; } = "";
    public string SteamId  { get; set; } = "";
    public string Ip       { get; set; } = "";
    public int    Ping     { get; set; }
    public int    Health   { get; set; }
    public int    Level    { get; set; }
    public int    Deaths   { get; set; }
    public int    Zombies  { get; set; }
    public int    Score    { get; set; }
}
