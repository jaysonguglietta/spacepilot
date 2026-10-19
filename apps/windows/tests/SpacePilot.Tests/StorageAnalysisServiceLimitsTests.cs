using SpacePilot.Services;

namespace SpacePilot.Tests;

public sealed class StorageAnalysisServiceLimitsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SpacePilot.StorageTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task LargeFileScanReportsPartialResultsWhenFileBudgetIsReached()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllBytesAsync(Path.Combine(_root, "one.bin"), new byte[1024 * 1024]);
        await File.WriteAllBytesAsync(Path.Combine(_root, "two.bin"), new byte[1024 * 1024]);
        var service = new StorageAnalysisService([_root], new StorageScanLimits(1, 4L * 1024 * 1024, TimeSpan.FromMinutes(1)));

        var result = await service.ScanLargeFilesAsync(1);

        Assert.Single(result.Items);
        Assert.Contains(result.Warnings, warning => warning.Contains("partial", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task StorageMapReportsPartialResultAfterEntryBudget()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "one.txt"), "one");
        await File.WriteAllTextAsync(Path.Combine(_root, "two.txt"), "two");
        var service = new StorageAnalysisService([_root], new StorageScanLimits(1, 0, TimeSpan.FromMinutes(1)));

        var result = await service.ScanStorageMapAsync();

        Assert.Contains(result.Warnings, warning => warning.Contains("partial", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DuplicateScanCapsHashBytesAndReportsUnverifiedCandidates()
    {
        Directory.CreateDirectory(_root);
        var contents = new byte[1024 * 1024];
        await File.WriteAllBytesAsync(Path.Combine(_root, "one.bin"), contents);
        await File.WriteAllBytesAsync(Path.Combine(_root, "two.bin"), contents);
        await File.WriteAllBytesAsync(Path.Combine(_root, "three.bin"), contents);
        var service = new StorageAnalysisService([_root], new StorageScanLimits(10, 2L * 1024 * 1024, TimeSpan.FromMinutes(1)));

        var result = await service.ScanDuplicatesAsync(1);

        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Warnings, warning => warning.Contains("partial", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WarningCollectorKeepsOnlyFiftyPathsAndAUsefulSuppressedCount()
    {
        var warnings = new List<string>();
        var collector = new StorageWarningCollector(warnings);
        for (var index = 0; index < 60; index++)
        {
            collector.Add($"Could not inspect file {index}");
        }

        Assert.Equal(51, warnings.Count);
        Assert.Contains("10 additional path warnings", warnings[^1]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
