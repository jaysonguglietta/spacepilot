using System.IO;
using System.IO.Enumeration;

namespace SpacePilot.Services;

internal static class BoundedFileWalker
{
    public static bool Walk(
        string root,
        string filePattern,
        bool recursive,
        StorageScanBudget budget,
        StorageWarningCollector warnings,
        CancellationToken cancellationToken,
        Func<string, bool> onFile,
        Func<string, bool>? onDirectory = null,
        FileAttributes attributesToSkip = FileAttributes.ReparsePoint)
    {
        if (!Directory.Exists(root))
        {
            return true;
        }

        if (PathSafety.ContainsReparsePoint(root))
        {
            warnings.Add($"Skipped path through a reparse point: {root}");
            return true;
        }

        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (budget.IsLimitReached)
            {
                return false;
            }

            var directory = pending.Pop();
            try
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.TopDirectoryOnly))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!budget.TryInspect())
                    {
                        return false;
                    }

                    FileAttributes attributes;
                    try
                    {
                        attributes = File.GetAttributes(entry);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                    {
                        warnings.Add($"Could not inspect {entry}: {ex.Message}");
                        continue;
                    }

                    if ((attributes & attributesToSkip) != 0)
                    {
                        continue;
                    }

                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        if (onDirectory is not null && !onDirectory(entry))
                        {
                            return false;
                        }

                        if (recursive)
                        {
                            pending.Push(entry);
                        }
                    }
                    else if (FileSystemName.MatchesSimpleExpression(filePattern, Path.GetFileName(entry), ignoreCase: true)
                             && !onFile(entry))
                    {
                        return false;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                warnings.Add($"Could not scan {directory}: {ex.Message}");
            }
        }

        return true;
    }

    public static bool MatchesName(string pattern, string name) =>
        FileSystemName.MatchesSimpleExpression(pattern, name, ignoreCase: true);
}
