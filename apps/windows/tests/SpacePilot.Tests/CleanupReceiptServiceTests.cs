using SpacePilot.Models;
using SpacePilot.Services;

namespace SpacePilot.Tests;

public sealed class CleanupReceiptServiceTests : IDisposable
{
    private readonly string _appDataRoot = Path.Combine(Path.GetTempPath(), "SpacePilot.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task SaveAsync_StoresReceiptAndGetRecentReturnsNewestFirst()
    {
        var service = new CleanupReceiptService(_appDataRoot);
        var older = new CleanupReceipt
        {
            Id = "older",
            TimestampUtc = DateTime.UtcNow.AddMinutes(-5),
            Mode = "Quarantine",
            CompletedCount = 1
        };
        var newer = new CleanupReceipt
        {
            Id = "newer",
            TimestampUtc = DateTime.UtcNow,
            Mode = "Quarantine",
            CompletedCount = 2
        };

        await service.SaveAsync(older);
        await service.SaveAsync(newer);

        var recent = await service.GetRecentAsync();

        Assert.Equal(["newer", "older"], recent.Select(receipt => receipt.Id).ToArray());
    }

    [Fact]
    public async Task ExportLatestAsync_CopiesMostRecentReceiptToExportsFolder()
    {
        var service = new CleanupReceiptService(_appDataRoot);
        await service.SaveAsync(new CleanupReceipt
        {
            Id = "receipt",
            TimestampUtc = DateTime.UtcNow,
            Mode = "Quarantine",
            CompletedCount = 1
        });

        var exportPath = await service.ExportLatestAsync();

        Assert.NotNull(exportPath);
        Assert.True(File.Exists(exportPath));
        Assert.EndsWith(Path.Combine("exports", "receipt.json"), exportPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_appDataRoot))
        {
            Directory.Delete(_appDataRoot, recursive: true);
        }
    }
}
