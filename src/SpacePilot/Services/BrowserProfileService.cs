using System.IO;
using SpacePilot.Models;

namespace SpacePilot.Services;

public sealed class BrowserProfileService
{
    public Task<IReadOnlyList<BrowserProfileInfo>> DiscoverProfilesAsync(UserPreferences preferences)
    {
        return Task.Run<IReadOnlyList<BrowserProfileInfo>>(() =>
        {
            var profiles = new List<BrowserProfileInfo>();
            AddChromiumProfiles(profiles, "Microsoft Edge", "%LOCALAPPDATA%\\Microsoft\\Edge\\User Data", preferences);
            AddChromiumProfiles(profiles, "Google Chrome", "%LOCALAPPDATA%\\Google\\Chrome\\User Data", preferences);
            AddFirefoxProfiles(profiles, preferences);
            return profiles.OrderBy(profile => profile.Browser).ThenBy(profile => profile.ProfileName).ToList();
        });
    }

    private static void AddChromiumProfiles(List<BrowserProfileInfo> profiles, string browser, string rootPath, UserPreferences preferences)
    {
        var root = Environment.ExpandEnvironmentVariables(rootPath);
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (var directory in SafeEnumerateDirectories(root))
        {
            var name = Path.GetFileName(directory);
            if (!name.Equals("Default", StringComparison.OrdinalIgnoreCase) && !name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            profiles.Add(new BrowserProfileInfo(
                browser,
                name,
                directory,
                preferences.BrowserCacheSelected,
                preferences.BrowserCookiesSelected,
                preferences.BrowserHistorySelected,
                preferences.BrowserSessionsSelected));
        }
    }

    private static void AddFirefoxProfiles(List<BrowserProfileInfo> profiles, UserPreferences preferences)
    {
        var root = Environment.ExpandEnvironmentVariables("%APPDATA%\\Mozilla\\Firefox\\Profiles");
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (var directory in SafeEnumerateDirectories(root))
        {
            profiles.Add(new BrowserProfileInfo(
                "Mozilla Firefox",
                Path.GetFileName(directory),
                directory,
                preferences.BrowserCacheSelected,
                preferences.BrowserCookiesSelected,
                preferences.BrowserHistorySelected,
                preferences.BrowserSessionsSelected));
        }
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string root)
    {
        try
        {
            return Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly).ToList();
        }
        catch
        {
            return [];
        }
    }
}
