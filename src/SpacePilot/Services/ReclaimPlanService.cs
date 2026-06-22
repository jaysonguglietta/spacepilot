using SpacePilot.Models;

namespace SpacePilot.Services;

public sealed class ReclaimPlanService
{
    public IReadOnlyList<ReclaimPlanItem> BuildPlan(
        IEnumerable<CleanupCandidateViewAdapter> cleanupCandidates,
        IEnumerable<LargeFileInfo> largeFiles,
        IEnumerable<DuplicateFileInfo> duplicates,
        long quarantineBytes)
    {
        var cleanup = cleanupCandidates.ToList();
        var large = largeFiles.ToList();
        var duplicate = duplicates.ToList();

        var items = new List<ReclaimPlanItem>
        {
            new(
                "1",
                "Safe cleanup",
                "Run selected low-risk temp/cache cleanup. Quarantine is recommended for easy undo.",
                cleanup.Where(candidate => candidate.Risk == RiskLevel.Low).Sum(candidate => candidate.SizeBytes),
                "Scan and clean low-risk items",
                "Low"),
            new(
                "2",
                "Recoverable cleanup",
                "Purge quarantine after the system is stable and you no longer need undo.",
                quarantineBytes,
                "Review Recovery, then purge",
                "Medium"),
            new(
                "3",
                "Duplicate files",
                "Review duplicate sets and keep one known-good copy before purging extras.",
                duplicate.Where(file => file.IsRecommendedForCleanup).Sum(file => file.SizeBytes),
                "Find duplicates",
                "Manual review"),
            new(
                "4",
                "Large files",
                "Move or quarantine personal large files only after confirming they are no longer needed.",
                large.Sum(file => file.SizeBytes),
                "Scan large files",
                "High"),
            new(
                "5",
                "Startup efficiency",
                "Review high-impact startup apps and scheduled tasks, then use Windows settings for changes.",
                0,
                "Review Startup",
                "Settings change")
        };

        return items;
    }
}

public sealed record CleanupCandidateViewAdapter(long SizeBytes, RiskLevel Risk);
