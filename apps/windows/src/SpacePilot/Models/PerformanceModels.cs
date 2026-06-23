namespace SpacePilot.Models;

public sealed record SystemPerformanceSnapshot(
    long TotalMemoryBytes,
    long AvailableMemoryBytes,
    double MemoryUsagePercent,
    long? CommitUsedBytes,
    long? CommitLimitBytes,
    int ProcessCount,
    TimeSpan Uptime,
    string MemoryPressure,
    string Summary)
{
    public long UsedMemoryBytes => Math.Max(0, TotalMemoryBytes - AvailableMemoryBytes);

    public string UptimeText
    {
        get
        {
            if (Uptime.TotalDays >= 1)
            {
                return $"{(int)Uptime.TotalDays}d {Uptime.Hours}h";
            }

            return $"{Uptime.Hours}h {Uptime.Minutes}m";
        }
    }
}

public sealed record ProcessMemoryInfo(
    int ProcessId,
    string Name,
    long WorkingSetBytes,
    long PrivateMemoryBytes,
    string Path,
    string Recommendation,
    string SafetyNote);

public sealed record PerformanceRecommendation(
    string Area,
    string Status,
    string Recommendation,
    string Impact,
    string Action);

public sealed record PerformanceAssistResult(
    SystemPerformanceSnapshot Snapshot,
    IReadOnlyList<ProcessMemoryInfo> Processes,
    IReadOnlyList<PerformanceRecommendation> Recommendations);
