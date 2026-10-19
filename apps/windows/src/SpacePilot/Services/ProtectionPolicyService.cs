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
                || fullPath.StartsWith(protectedPath + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                // A directory selection must not sweep a protected child.
                || protectedPath.StartsWith(fullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                || protectedPath.StartsWith(fullPath + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Extension protection applies to descendants as well as the selected directory.
        // Fail closed when the tree cannot be inspected, including reparse points.
        if (Directory.Exists(fullPath))
        {
            try
            {
                var pending = new Stack<string>();
                pending.Push(fullPath);
                while (pending.Count > 0)
                {
                    var directory = pending.Pop();
                    if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                    {
                        return true;
                    }

                    foreach (var file in Directory.EnumerateFiles(directory))
                    {
                        var attributes = File.GetAttributes(file);
                        if ((attributes & FileAttributes.ReparsePoint) != 0
                            || preferences.ProtectedExtensions.Any(value => value.Equals(Path.GetExtension(file), StringComparison.OrdinalIgnoreCase)))
                        {
                            return true;
                        }
                    }

                    foreach (var child in Directory.EnumerateDirectories(directory))
                    {
                        pending.Push(child);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                return true;
            }
        }

        return false;
    }
}
