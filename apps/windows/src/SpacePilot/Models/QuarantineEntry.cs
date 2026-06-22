namespace SpacePilot.Models;

public sealed class QuarantineEntry
{
    public string Id { get; set; } = string.Empty;
    public string OriginalPath { get; set; } = string.Empty;
    public string PayloadPath { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public CleanupTargetKind Kind { get; set; }
    public long SizeBytes { get; set; }
    public DateTime QuarantinedUtc { get; set; }
    public DateTime? OriginalLastModifiedUtc { get; set; }

    public DateTime QuarantinedLocal => QuarantinedUtc.ToLocalTime();
    public DateTime? OriginalLastModifiedLocal => OriginalLastModifiedUtc?.ToLocalTime();
}
