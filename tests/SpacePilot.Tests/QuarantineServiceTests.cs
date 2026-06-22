using SpacePilot.Models;
using SpacePilot.Services;

namespace SpacePilot.Tests;

public sealed class QuarantineServiceTests : IDisposable
{
    private readonly string _appDataRoot = Path.Combine(Path.GetTempPath(), "SpacePilot.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task QuarantineAsync_MovesAllowedFileAndRestoreReturnsIt()
    {
        var approvedRoot = Path.Combine(_appDataRoot, "approved");
        Directory.CreateDirectory(approvedRoot);
        var sourcePath = Path.Combine(approvedRoot, "cache.tmp");
        await File.WriteAllTextAsync(sourcePath, "temporary data");

        var candidate = new CleanupCandidate(
            "test-temp",
            "Test temp",
            "cache.tmp",
            sourcePath,
            approvedRoot,
            CleanupTargetKind.File,
            new FileInfo(sourcePath).Length,
            DateTime.UtcNow,
            RiskLevel.Low);

        var service = new QuarantineService(_appDataRoot);
        var result = await service.QuarantineAsync([candidate]);

        Assert.Empty(result.Warnings);
        var entry = Assert.Single(result.Entries);
        Assert.False(File.Exists(sourcePath));
        Assert.True(File.Exists(entry.PayloadPath));

        var restoreWarnings = await service.RestoreAsync([entry.Id]);

        Assert.Empty(restoreWarnings);
        Assert.True(File.Exists(sourcePath));
        Assert.Equal("temporary data", await File.ReadAllTextAsync(sourcePath));
        Assert.Empty(await service.GetEntriesAsync());
    }

    [Fact]
    public async Task QuarantineAsync_SkipsCandidateOutsideApprovedRoot()
    {
        var approvedRoot = Path.Combine(_appDataRoot, "approved");
        var outsideRoot = Path.Combine(_appDataRoot, "outside");
        Directory.CreateDirectory(approvedRoot);
        Directory.CreateDirectory(outsideRoot);
        var sourcePath = Path.Combine(outsideRoot, "cache.tmp");
        await File.WriteAllTextAsync(sourcePath, "keep me");

        var candidate = new CleanupCandidate(
            "test-temp",
            "Test temp",
            "cache.tmp",
            sourcePath,
            approvedRoot,
            CleanupTargetKind.File,
            new FileInfo(sourcePath).Length,
            DateTime.UtcNow,
            RiskLevel.Low);

        var service = new QuarantineService(_appDataRoot);
        var result = await service.QuarantineAsync([candidate]);

        Assert.Empty(result.Entries);
        Assert.Single(result.Warnings);
        Assert.True(File.Exists(sourcePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_appDataRoot))
        {
            Directory.Delete(_appDataRoot, recursive: true);
        }
    }
}
