using System.Security.Cryptography;
using SpacePilot.Models;
using SpacePilot.Services;

namespace SpacePilot.Tests;

public sealed class DuplicateReviewServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SpacePilot.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ValidateAsync_RequiresKeeperAndRejectsChangedCopy()
    {
        Directory.CreateDirectory(_root);
        var keeper = Path.Combine(_root, "keeper.bin");
        var extra = Path.Combine(_root, "extra.bin");
        await File.WriteAllTextAsync(keeper, "same content");
        await File.WriteAllTextAsync(extra, "same content");
        var hash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(keeper)));
        var files = new[]
        {
            new DuplicateFileInfo("Set 1", hash, keeper, "keeper.bin", _root, 12, File.GetLastWriteTimeUtc(keeper), false),
            new DuplicateFileInfo("Set 1", hash, extra, "extra.bin", _root, 12, File.GetLastWriteTimeUtc(extra), true)
        };
        var service = new DuplicateReviewService();

        Assert.Contains(await service.ValidateAsync(files, [extra], []), error => error.Contains("keeper", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(await service.ValidateAsync(files, [extra], [keeper]));

        await File.WriteAllTextAsync(extra, "new! content"); // same size, different hash
        Assert.Contains(await service.ValidateAsync(files, [extra], [keeper]), error => error.Contains("changed", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
