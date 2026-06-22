namespace SpacePilot.Models;

public sealed record ActivityLogEntry(DateTime TimestampUtc, string Level, string Message)
{
    public DateTime TimestampLocal => TimestampUtc.ToLocalTime();
}
