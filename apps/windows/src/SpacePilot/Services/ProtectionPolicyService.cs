using System.IO;
using SpacePilot.Models;

namespace SpacePilot.Services;

public sealed class ProtectionPolicyService
{
    public bool IsProtected(string path, UserPreferences preferences)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        var extension = Path.GetExtension(path);
        if (!string.IsNullOrWhiteSpace(extension)
            && preferences.ProtectedExtensions.Any(value => value.Equals(extension, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        string fullPath;
        try
        {
            fullPath = PathSafety.Normalize(path);
        }
        catch
        {
            return true;
        }

        foreach (var entry in preferences.ProtectedPaths)
        {
            if (string.IsNullOrWhiteSpace(entry.Path))
            {
                continue;
            }

            string protectedPath;
            try
            {
                protectedPath = PathSafety.Normalize(Environment.ExpandEnvironmentVariables(entry.Path));
            }
            catch
            {
                continue;
            }
            if (fullPath.Equals(protectedPath, StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(protectedPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || fullPath.StartsWith(protectedPath + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
