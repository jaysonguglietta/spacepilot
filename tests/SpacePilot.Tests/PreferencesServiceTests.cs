using SpacePilot.Models;
using SpacePilot.Services;

namespace SpacePilot.Tests;

public sealed class PreferencesServiceTests : IDisposable
{
    private readonly string _appDataRoot = Path.Combine(Path.GetTempPath(), "SpacePilot.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Load_WhenFileIsMissing_ReturnsSafeDefaults()
    {
        var preferences = new PreferencesService(_appDataRoot).Load();

        Assert.True(preferences.ConfirmBeforeCleanup);
        Assert.True(preferences.UseQuarantine);
        Assert.Contains(".pdf", preferences.ProtectedExtensions);
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsPreferences()
    {
        var service = new PreferencesService(_appDataRoot);
        service.Save(new UserPreferences
        {
            ConfirmBeforeCleanup = false,
            UseQuarantine = true,
            LargeFileMinimumMb = 512,
            ProtectedExtensions = [".safe"],
            CleanupCategorySelections = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
            {
                ["user-temp"] = true
            }
        });

        var loaded = service.Load();

        Assert.False(loaded.ConfirmBeforeCleanup);
        Assert.Equal(512, loaded.LargeFileMinimumMb);
        Assert.Equal([".safe"], loaded.ProtectedExtensions);
        Assert.True(loaded.CleanupCategorySelections["USER-TEMP"]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_appDataRoot))
        {
            Directory.Delete(_appDataRoot, recursive: true);
        }
    }
}
