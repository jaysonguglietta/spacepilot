using SpacePilot.Models;
using SpacePilot.Services;

namespace SpacePilot.Tests;

public sealed class ProtectionPolicyServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SpacePilot.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task IsProtected_ProtectsDirectoryContainingProtectedExtensionOrPath()
    {
        var directory = Path.Combine(_root, "cache");
        Directory.CreateDirectory(directory);
        var protectedFile = Path.Combine(directory, "taxes.pdf");
        await File.WriteAllTextAsync(protectedFile, "private");
        var service = new ProtectionPolicyService();
        var preferences = new UserPreferences();

        Assert.True(service.IsProtected(directory, preferences));

        File.Delete(protectedFile);
        var nested = Path.Combine(directory, "nested");
        Directory.CreateDirectory(nested);
        preferences.ProtectedPaths.Add(new ProtectedPathEntry(nested, "keep", DateTime.UtcNow));
        Assert.True(service.IsProtected(directory, preferences));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
