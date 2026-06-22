using System.IO;
using System.Text.Json;
using SpacePilot.Models;

namespace SpacePilot.Services;

public sealed class PreferencesService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _preferencesPath;

    public PreferencesService(string? appDataRoot = null)
    {
        var root = appDataRoot ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
        {
            root = AppContext.BaseDirectory;
        }

        _preferencesPath = Path.Combine(root, "SpacePilot", "preferences.json");
    }

    public UserPreferences Load()
    {
        try
        {
            if (!File.Exists(_preferencesPath))
            {
                return new UserPreferences();
            }

            var json = File.ReadAllText(_preferencesPath);
            var preferences = JsonSerializer.Deserialize<UserPreferences>(json, JsonOptions);
            if (preferences is null)
            {
                return new UserPreferences();
            }

            preferences.CleanupCategorySelections = new Dictionary<string, bool>(
                preferences.CleanupCategorySelections ?? new Dictionary<string, bool>(),
                StringComparer.OrdinalIgnoreCase);
            preferences.ProtectedPaths ??= [];
            preferences.ProtectedExtensions ??= [];
            if (preferences.ProtectedExtensions.Count == 0)
            {
                preferences.ProtectedExtensions.AddRange([".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".pdf", ".key"]);
            }

            return preferences;
        }
        catch
        {
            return new UserPreferences();
        }
    }

    public void Save(UserPreferences preferences)
    {
        try
        {
            var directory = Path.GetDirectoryName(_preferencesPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_preferencesPath, JsonSerializer.Serialize(preferences, JsonOptions));
        }
        catch
        {
            // Preferences should never block a cleanup workflow.
        }
    }
}
