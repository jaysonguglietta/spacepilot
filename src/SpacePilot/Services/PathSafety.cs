namespace SpacePilot.Services;

public static class PathSafety
{
    public static bool IsAllowedCleanupTarget(string candidatePath, string approvedRoot)
    {
        if (string.IsNullOrWhiteSpace(candidatePath) || string.IsNullOrWhiteSpace(approvedRoot))
        {
            return false;
        }

        var fullCandidate = Normalize(candidatePath);
        var fullRoot = Normalize(approvedRoot);

        if (string.Equals(fullCandidate, fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(fullCandidate, Path.GetPathRoot(fullCandidate), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return fullCandidate.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || fullCandidate.StartsWith(fullRoot + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    public static string Normalize(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
