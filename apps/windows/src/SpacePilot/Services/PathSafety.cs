using System.IO;

namespace SpacePilot.Services;

public static class PathSafety
{
    public static bool IsAllowedCleanupTarget(string candidatePath, string approvedRoot)
    {
        if (string.IsNullOrWhiteSpace(candidatePath) || string.IsNullOrWhiteSpace(approvedRoot))
        {
            return false;
        }

        string fullCandidate;
        string fullRoot;
        try
        {
            fullCandidate = Normalize(candidatePath);
            fullRoot = Normalize(approvedRoot);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            return false;
        }

        if (string.Equals(fullCandidate, fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(fullCandidate, Path.GetPathRoot(fullCandidate), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rootPrefix = Path.EndsInDirectorySeparator(fullRoot)
            ? fullRoot
            : fullRoot + Path.DirectorySeparatorChar;
        return fullCandidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase);
    }

    public static string Normalize(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath);
        return string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    public static bool ContainsReparsePoint(string path)
    {
        try
        {
            for (string? current = Path.GetFullPath(path); !string.IsNullOrWhiteSpace(current); current = Path.GetDirectoryName(current))
            {
                if ((File.Exists(current) || Directory.Exists(current))
                    && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or System.Security.SecurityException)
        {
            return true;
        }
    }
}
