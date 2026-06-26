namespace SevenDaysManager.Models;

public class ModInfo
{
    public string FolderPath  { get; set; } = "";   // full path to mod folder
    public string FolderName  { get; set; } = "";   // raw folder name (may have .disabled suffix)
    public string Name        { get; set; } = "";
    public string Version     { get; set; } = "";
    public string Author      { get; set; } = "";
    public string Description { get; set; } = "";
    public string Website     { get; set; } = "";
    public bool   IsEnabled   { get; set; } = true;
    public bool   HasModInfo  { get; set; } = false; // false = folder with no ModInfo.xml
    public bool   IsSystem    { get; set; } = false; // TFP_ game-internal — show but lock
}
