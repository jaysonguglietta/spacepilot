using SpacePilot.Models;
using SpacePilot.Services;

namespace SpacePilot.Tests;

public sealed class CleanerServiceSafetyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SpacePilot.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ScanAsync_ExposesNestedLeafFileAndNeverWholeDirectory()
    {
        var nested = Path.Combine(_root, "cache", "nested");
        Directory.CreateDirectory(nested);
        var file = Path.Combine(nested, "old.tmp");
        await File.WriteAllTextAsync(file, "temporary");
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-3));
        var rule = new CleanupRule("test", "Test cache", "", true, RiskLevel.Low, 1,
            [new CleanupLocation(_root, "*", true, true, true)]);

        var result = await new CleanerService().ScanAsync([rule]);

        var candidate = Assert.Single(result.Candidates);
        Assert.Equal(CleanupTargetKind.File, candidate.Kind);
        Assert.Equal(file, candidate.Path);
    }

    [Fact]
    public async Task CleanAsync_RefusesMutableDirectoryTarget()
    {
        var directory = Path.Combine(_root, "cache");
        Directory.CreateDirectory(directory);
        var file = Path.Combine(directory, "keep.txt");
        await File.WriteAllTextAsync(file, "keep");
        var candidate = new CleanupCandidate("test", "Test", "cache", directory, _root,
            CleanupTargetKind.Directory, 4, DateTime.UtcNow, RiskLevel.Low);

        var result = await new CleanerService().CleanAsync([candidate], new UserPreferences());

        Assert.Equal(0, result.DeletedCount);
        Assert.NotEmpty(result.Warnings);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public async Task ScanAsync_ReportsPartialResultAfterEntryBudget()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "one.tmp"), "one");
        await File.WriteAllTextAsync(Path.Combine(_root, "two.tmp"), "two");
        var rule = new CleanupRule("test", "Test", "", true, RiskLevel.Low, 0,
            [new CleanupLocation(_root, "*", false, true, false)]);
        var service = new CleanerService(new StorageScanLimits(1, 0, TimeSpan.FromMinutes(1)));

        var result = await service.ScanAsync([rule]);

        Assert.True(result.Candidates.Count <= 1);
        Assert.Contains(result.Warnings, warning => warning.Contains("partial", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CleanAsync_SkipsFileChangedSinceScan()
    {
        Directory.CreateDirectory(_root);
        var file = Path.Combine(_root, "changed.tmp");
        await File.WriteAllTextAsync(file, "old");
        var rule = new CleanupRule("test", "Test", "", true, RiskLevel.Low, 0,
            [new CleanupLocation(_root, "*", false, true, false)]);
        var service = new CleanerService();
        var candidate = Assert.Single((await service.ScanAsync([rule])).Candidates);
        await File.WriteAllTextAsync(file, "new contents");

        var result = await service.CleanAsync([candidate]);

        Assert.Equal(0, result.DeletedCount);
        Assert.True(File.Exists(file));
        Assert.Contains(result.Warnings, warning => warning.Contains("changed", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
