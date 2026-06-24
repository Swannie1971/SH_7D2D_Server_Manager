namespace SevenDaysManager.Models;

public record ConsoleLine(string Text, ConsoleLineType Type);

public enum ConsoleLineType { Info, Warning, Error, Chat, System }
