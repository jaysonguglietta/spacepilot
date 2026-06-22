namespace SpacePilot.Models;

public enum CleanupTargetKind
{
    File,
    Directory
}

public sealed record CleanupCandidate(
    string CategoryId,
    string CategoryName,
    string DisplayName,
    string Path,
    string ApprovedRoot,
    CleanupTargetKind Kind,
    long SizeBytes,
    DateTime? LastModifiedUtc,
    RiskLevel Risk)
{
    public DateTime? LastModifiedLocal => LastModifiedUtc?.ToLocalTime();
}
