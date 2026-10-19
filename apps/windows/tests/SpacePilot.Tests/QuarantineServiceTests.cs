using SpacePilot.Models;
using SpacePilot.Services;
using System.Text.Json;

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
    public async Task RestoreToAsync_ResolvesOriginalPathConflictWithoutOverwrite()
    {
        var approvedRoot = Path.Combine(_appDataRoot, "approved");
        Directory.CreateDirectory(approvedRoot);
        var sourcePath = Path.Combine(approvedRoot, "report.tmp");
        await File.WriteAllTextAsync(sourcePath, "old content");
        var candidate = new CleanupCandidate("test", "Test", "report.tmp", sourcePath, approvedRoot,
            CleanupTargetKind.File, 11, DateTime.UtcNow, RiskLevel.Low);
        var service = new QuarantineService(_appDataRoot);
        var entry = Assert.Single((await service.QuarantineAsync([candidate])).Entries);
        await File.WriteAllTextAsync(sourcePath, "new content");

        var conflictWarnings = await service.RestoreAsync([entry.Id]);
        Assert.Single(conflictWarnings);
        Assert.Equal("new content", await File.ReadAllTextAsync(sourcePath));

        var alternate = Path.Combine(approvedRoot, "report-restored.tmp");
        Assert.Empty(await service.RestoreToAsync(entry.Id, alternate));
        Assert.Equal("old content", await File.ReadAllTextAsync(alternate));
        Assert.Equal("new content", await File.ReadAllTextAsync(sourcePath));
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

    [Fact]
    public async Task QuarantineAsync_SkipsDirectoryWithProtectedDescendant()
    {
        var approvedRoot = Path.Combine(_appDataRoot, "approved");
        var candidatePath = Path.Combine(approvedRoot, "cache");
        Directory.CreateDirectory(candidatePath);
        var protectedFile = Path.Combine(candidatePath, "important.pdf");
        await File.WriteAllTextAsync(protectedFile, "keep me");
        var candidate = new CleanupCandidate("cache", "Cache", "cache", candidatePath, approvedRoot,
            CleanupTargetKind.Directory, 7, DateTime.UtcNow, RiskLevel.Low);

        var result = await new QuarantineService(_appDataRoot).QuarantineAsync([candidate], new UserPreferences());

        Assert.Empty(result.Entries);
        Assert.NotEmpty(result.Warnings);
        Assert.True(File.Exists(protectedFile));
    }

    [Fact]
    public async Task PurgeExpiredAsync_RejectsForgedPayloadPathWithoutDeletingOutsideFolder()
    {
        var quarantineRoot = Path.Combine(_appDataRoot, "SpacePilot", "Quarantine");
        var outsideFolder = Path.Combine(_appDataRoot, "keep-outside");
        Directory.CreateDirectory(quarantineRoot);
        Directory.CreateDirectory(outsideFolder);
        var keep = Path.Combine(outsideFolder, "keep.txt");
        await File.WriteAllTextAsync(keep, "keep me");
        var entry = new QuarantineEntry
        {
            Id = $"20261019000000000-{Guid.NewGuid():N}",
            OriginalPath = Path.Combine(_appDataRoot, "original.txt"),
            PayloadPath = Path.Combine(outsideFolder, "payload"),
            QuarantinedUtc = DateTime.UtcNow.AddDays(-30),
            Kind = CleanupTargetKind.File
        };
        await File.WriteAllTextAsync(Path.Combine(quarantineRoot, "manifest.json"), JsonSerializer.Serialize(new[] { entry }));

        await Assert.ThrowsAsync<InvalidDataException>(() => new QuarantineService(_appDataRoot).PurgeExpiredAsync(1));
        Assert.Equal("keep me", await File.ReadAllTextAsync(keep));
    }

    [Fact]
    public async Task GetEntriesAsync_DoesNotTreatCorruptManifestAsEmpty()
    {
        var quarantineRoot = Path.Combine(_appDataRoot, "SpacePilot", "Quarantine");
        Directory.CreateDirectory(quarantineRoot);
        await File.WriteAllTextAsync(Path.Combine(quarantineRoot, "manifest.json"), "{not-json");

        await Assert.ThrowsAsync<InvalidDataException>(() => new QuarantineService(_appDataRoot).GetEntriesAsync());
    }

    [Fact]
    public async Task CorruptManifest_RecoversIntactPayloadFromSidecarWithoutAutomaticPurge()
    {
        var approvedRoot = Path.Combine(_appDataRoot, "approved");
        Directory.CreateDirectory(approvedRoot);
        var sourcePath = Path.Combine(approvedRoot, "recover-me.tmp");
        await File.WriteAllTextAsync(sourcePath, "important data");
        var candidate = new CleanupCandidate("test", "Test", "recover-me.tmp", sourcePath, approvedRoot,
            CleanupTargetKind.File, new FileInfo(sourcePath).Length, DateTime.UtcNow, RiskLevel.Low);
        var service = new QuarantineService(_appDataRoot);
        var entry = Assert.Single((await service.QuarantineAsync([candidate])).Entries);
        var manifestPath = Path.Combine(_appDataRoot, "SpacePilot", "Quarantine", "manifest.json");
        await File.WriteAllTextAsync(manifestPath, "{damaged");

        Assert.Equal(0, await service.PurgeExpiredAsync(1));
        Assert.Equal(entry.Id, Assert.Single(await service.GetEntriesAsync()).Id);
        Assert.NotNull(service.RecoveryWarning);
        Assert.True(File.Exists(entry.PayloadPath));

        Assert.Empty(await service.RestoreAsync([entry.Id]));
        Assert.Equal("important data", await File.ReadAllTextAsync(sourcePath));
        Assert.Empty(await service.GetEntriesAsync());
        Assert.NotEmpty(Directory.EnumerateFiles(Path.GetDirectoryName(manifestPath)!, "manifest-corrupt-*.json"));
    }

    [Fact]
    public async Task GetEntriesAsync_WaitsForAnotherProcessManifestLockAndFailsClearly()
    {
        var quarantineRoot = Path.Combine(_appDataRoot, "SpacePilot", "Quarantine");
        Directory.CreateDirectory(quarantineRoot);
        using var heldLock = new FileStream(Path.Combine(quarantineRoot, "manifest.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var service = new QuarantineService(_appDataRoot, TimeSpan.FromMilliseconds(100));

        var error = await Assert.ThrowsAsync<IOException>(() => service.GetEntriesAsync());

        Assert.Contains("busy", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FailedManifestWriteLeavesSourceAndNoOrphanSidecar()
    {
        var approvedRoot = Path.Combine(_appDataRoot, "approved");
        Directory.CreateDirectory(approvedRoot);
        var sourcePath = Path.Combine(approvedRoot, "keep.tmp");
        await File.WriteAllTextAsync(sourcePath, "keep me");
        var quarantineRoot = Path.Combine(_appDataRoot, "SpacePilot", "Quarantine");
        Directory.CreateDirectory(Path.Combine(quarantineRoot, "manifest.json"));
        var candidate = new CleanupCandidate("test", "Test", "keep.tmp", sourcePath, approvedRoot,
            CleanupTargetKind.File, 7, DateTime.UtcNow, RiskLevel.Low);

        var result = await new QuarantineService(_appDataRoot).QuarantineAsync([candidate]);

        Assert.Empty(result.Entries);
        Assert.True(File.Exists(sourcePath));
        Assert.Empty(Directory.EnumerateFiles(quarantineRoot, "entry.json", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task QuarantineAsync_DoesNotMoveLargeFileChangedSinceScan()
    {
        var approvedRoot = Path.Combine(_appDataRoot, "approved");
        Directory.CreateDirectory(approvedRoot);
        var sourcePath = Path.Combine(approvedRoot, "large.bin");
        await File.WriteAllTextAsync(sourcePath, "old");
        var scanTime = File.GetLastWriteTimeUtc(sourcePath);
        var candidate = new CleanupCandidate("large-file-review", "Large file review", "large.bin",
            sourcePath, approvedRoot, CleanupTargetKind.File, 3, scanTime, RiskLevel.High,
            RequiresScanMatch: true);
        await File.WriteAllTextAsync(sourcePath, "changed content");

        var result = await new QuarantineService(_appDataRoot).QuarantineAsync([candidate], new UserPreferences());

        Assert.Empty(result.Entries);
        Assert.Contains(result.Warnings, warning => warning.Contains("changed", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("changed content", await File.ReadAllTextAsync(sourcePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_appDataRoot))
        {
            Directory.Delete(_appDataRoot, recursive: true);
        }
    }
}
