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

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
