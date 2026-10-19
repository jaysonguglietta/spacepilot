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
    RiskLevel Risk,
    string? ExpectedSha256 = null,
    bool RequiresScanMatch = false)
{
    public DateTime? LastModifiedLocal => LastModifiedUtc?.ToLocalTime();
}
