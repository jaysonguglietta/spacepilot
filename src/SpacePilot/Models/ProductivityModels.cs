namespace SpacePilot.Models;

public sealed record ReclaimPlanItem(
    string Priority,
    string Area,
    string Recommendation,
    long EstimatedBytes,
    string Action,
    string Risk);

public sealed record WingetPackageInfo(
    string Name,
    string Id,
    string Version,
    string AvailableVersion,
    string Source,
    bool IsUpdateAvailable);

public sealed record FolderUsageInfo(
    string Path,
    string Name,
    long SizeBytes,
    int FileCount,
    int FolderCount,
    string Recommendation);

public sealed record FileTypeUsageInfo(
    string Extension,
    string Category,
    long SizeBytes,
    int FileCount);

public sealed class StorageMapResult
{
    public List<FolderUsageInfo> Folders { get; } = [];
    public List<FileTypeUsageInfo> FileTypes { get; } = [];
    public List<string> Warnings { get; } = [];
}

public sealed record BrowserProfileInfo(
    string Browser,
    string ProfileName,
    string ProfilePath,
    bool CacheSelected,
    bool CookiesSelected,
    bool HistorySelected,
    bool SessionsSelected);

public sealed record ProtectedPathEntry(
    string Path,
    string Reason,
    DateTime AddedUtc)
{
    public DateTime AddedLocal => AddedUtc.ToLocalTime();
}

public sealed class WingetOperationResult
{
    public bool Succeeded { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> OutputLines { get; } = [];
}
