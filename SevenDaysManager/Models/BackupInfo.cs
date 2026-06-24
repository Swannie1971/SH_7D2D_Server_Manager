namespace SevenDaysManager.Models;

public class BackupInfo
{
    public string   FilePath  { get; set; } = "";
    public string   FileName  { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public long     SizeBytes { get; set; }

    public string SizeLabel => SizeBytes switch
    {
        >= 1_073_741_824 => $"{SizeBytes / 1_073_741_824.0:F1} GB",
        >= 1_048_576     => $"{SizeBytes / 1_048_576.0:F1} MB",
        _                => $"{SizeBytes / 1_024.0:F0} KB"
    };
}
