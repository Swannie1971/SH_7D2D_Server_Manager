namespace SevenDaysManager.Models;

public class ScheduleConfig
{
    public bool         Enabled        { get; set; } = false;
    public ScheduleMode Mode           { get; set; } = ScheduleMode.Interval;
    public int          IntervalHours  { get; set; } = 6;
    public List<string> DailyTimes     { get; set; } = new();
    public List<int>    WarnMinutes    { get; set; } = new() { 30, 10, 5, 1 };

    public string InGameWarning { get; set; } = "Server restarting in {TIMEMIN} minutes! Finish up and find shelter.";
    public string InGameNow     { get; set; } = "Server restarting NOW. See you on the other side!";
}

public enum ScheduleMode { Interval, Daily }
