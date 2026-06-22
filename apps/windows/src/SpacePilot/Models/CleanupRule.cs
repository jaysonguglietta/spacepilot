namespace SpacePilot.Models;

public enum RiskLevel
{
    Low,
    Medium,
    High
}

public sealed record CleanupRule(
    string Id,
    string Name,
    string Description,
    bool DefaultSelected,
    RiskLevel Risk,
    int MinimumAgeDays,
    IReadOnlyList<CleanupLocation> Locations);

public sealed record CleanupLocation(
    string RootPath,
    string SearchPattern,
    bool Recursive,
    bool IncludeFiles,
    bool IncludeDirectories);

public sealed class CleanupScanResult
{
    public List<CleanupCandidate> Candidates { get; } = [];
    public List<string> Warnings { get; } = [];
    public long TotalBytes => Candidates.Sum(candidate => candidate.SizeBytes);
}

public sealed class CleanupRunResult
{
    public int DeletedCount { get; set; }
    public long DeletedBytes { get; set; }
    public List<string> DeletedPaths { get; } = [];
    public List<string> Warnings { get; } = [];
}
