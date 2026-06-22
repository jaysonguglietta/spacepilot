namespace SpacePilot.Models;

public sealed class UserPreferences
{
    public bool IsFirstRun { get; set; } = true;
    public bool ConfirmBeforeCleanup { get; set; } = true;
    public bool RemindRestorePointBeforeCleanup { get; set; } = true;
    public bool UseQuarantine { get; set; } = true;
    public bool SelectMediumRiskByDefault { get; set; }
    public int QuarantineRetentionDays { get; set; } = 14;
    public int LargeFileMinimumMb { get; set; } = 250;
    public int DuplicateMinimumMb { get; set; } = 25;
    public bool BrowserCacheSelected { get; set; } = true;
    public bool BrowserCookiesSelected { get; set; }
    public bool BrowserHistorySelected { get; set; }
    public bool BrowserSessionsSelected { get; set; }
    public List<ProtectedPathEntry> ProtectedPaths { get; set; } = [];
    public List<string> ProtectedExtensions { get; set; } = [".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".pdf", ".key"];
    public Dictionary<string, bool> CleanupCategorySelections { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTime? LastCleanupUtc { get; set; }
}
