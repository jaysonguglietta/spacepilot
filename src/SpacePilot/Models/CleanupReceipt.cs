namespace SpacePilot.Models;

public sealed class CleanupReceipt
{
    public string Id { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; }
    public string Mode { get; set; } = string.Empty;
    public int RequestedCount { get; set; }
    public int CompletedCount { get; set; }
    public long RequestedBytes { get; set; }
    public long CompletedBytes { get; set; }
    public long FreeSpaceBeforeBytes { get; set; }
    public long FreeSpaceAfterBytes { get; set; }
    public bool RestorePointAvailable { get; set; }
    public string RestorePointMessage { get; set; } = string.Empty;
    public DateTime? QuarantineExpiresUtc { get; set; }
    public List<CleanupReceiptItem> Items { get; set; } = [];
    public List<string> Warnings { get; set; } = [];

    public DateTime TimestampLocal => TimestampUtc.ToLocalTime();
    public DateTime? QuarantineExpiresLocal => QuarantineExpiresUtc?.ToLocalTime();
}

public sealed class CleanupReceiptItem
{
    public string Path { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? QuarantineId { get; set; }
    public string? Message { get; set; }
}
