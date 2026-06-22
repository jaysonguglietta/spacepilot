namespace SpacePilot.Models;

public sealed record LargeFileInfo(
    string Path,
    string Name,
    string Directory,
    string Extension,
    long SizeBytes,
    DateTime? LastModifiedUtc,
    string Recommendation)
{
    public DateTime? LastModifiedLocal => LastModifiedUtc?.ToLocalTime();
}

public sealed record DuplicateFileInfo(
    string GroupId,
    string Hash,
    string Path,
    string Name,
    string Directory,
    long SizeBytes,
    DateTime? LastModifiedUtc,
    bool IsRecommendedForCleanup)
{
    public DateTime? LastModifiedLocal => LastModifiedUtc?.ToLocalTime();
}

public sealed class StorageScanResult<T>
{
    public List<T> Items { get; } = [];
    public List<string> Warnings { get; } = [];
}
