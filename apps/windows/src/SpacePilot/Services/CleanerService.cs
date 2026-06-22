using System.Collections.Concurrent;
using System.IO;
using SpacePilot.Models;

namespace SpacePilot.Services;

public sealed class CleanerService
{
    public Task<CleanupScanResult> ScanAsync(IEnumerable<CleanupRule> rules, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var result = new CleanupScanResult();

            foreach (var rule in rules)
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var location in rule.Locations)
                {
                    foreach (var root in ResolveRoots(location.RootPath, result.Warnings))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        ScanLocation(rule, location, root, result, cancellationToken);
                    }
                }
            }

            return result;
        }, cancellationToken);
    }

    public Task<CleanupRunResult> CleanAsync(IEnumerable<CleanupCandidate> candidates, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var result = new CleanupRunResult();

            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!PathSafety.IsAllowedCleanupTarget(candidate.Path, candidate.ApprovedRoot))
                {
                    result.Warnings.Add($"Skipped unsafe target: {candidate.Path}");
                    continue;
                }

                try
                {
                    if (candidate.Kind == CleanupTargetKind.File)
                    {
                        if (!File.Exists(candidate.Path))
                        {
                            continue;
                        }

                        ClearReadOnly(candidate.Path);
                        File.Delete(candidate.Path);
                    }
                    else
                    {
                        if (!Directory.Exists(candidate.Path))
                        {
                            continue;
                        }

                        ClearReadOnlyRecursive(candidate.Path);
                        Directory.Delete(candidate.Path, recursive: true);
                    }

                    result.DeletedCount++;
                    result.DeletedBytes += candidate.SizeBytes;
                    result.DeletedPaths.Add(candidate.Path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    result.Warnings.Add($"Could not delete {candidate.Path}: {ex.Message}");
                }
            }

            return result;
        }, cancellationToken);
    }

    private static void ScanLocation(CleanupRule rule, CleanupLocation location, string root, CleanupScanResult result, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        var fileOptions = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = location.Recursive && !location.IncludeDirectories,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        var directoryOptions = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        var cutoffUtc = DateTime.UtcNow.AddDays(-rule.MinimumAgeDays);

        if (location.IncludeFiles)
        {
            foreach (var file in SafeEnumerateFiles(root, location.SearchPattern, fileOptions, result.Warnings))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var info = new FileInfo(file);
                    if (!info.Exists || info.LastWriteTimeUtc > cutoffUtc || HasReparsePoint(info.Attributes))
                    {
                        continue;
                    }

                    if (!PathSafety.IsAllowedCleanupTarget(info.FullName, root))
                    {
                        continue;
                    }

                    result.Candidates.Add(new CleanupCandidate(
                        rule.Id,
                        rule.Name,
                        info.Name,
                        info.FullName,
                        root,
                        CleanupTargetKind.File,
                        Math.Max(0, info.Length),
                        info.LastWriteTimeUtc,
                        rule.Risk));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    result.Warnings.Add($"Could not inspect {file}: {ex.Message}");
                }
            }
        }

        if (location.IncludeDirectories)
        {
            foreach (var directory in SafeEnumerateDirectories(root, location.SearchPattern, directoryOptions, result.Warnings))
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var info = new DirectoryInfo(directory);
                    if (!info.Exists || info.LastWriteTimeUtc > cutoffUtc || HasReparsePoint(info.Attributes))
                    {
                        continue;
                    }

                    if (!PathSafety.IsAllowedCleanupTarget(info.FullName, root))
                    {
                        continue;
                    }

                    result.Candidates.Add(new CleanupCandidate(
                        rule.Id,
                        rule.Name,
                        info.Name,
                        info.FullName,
                        root,
                        CleanupTargetKind.Directory,
                        GetDirectorySize(info.FullName),
                        info.LastWriteTimeUtc,
                        rule.Risk));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    result.Warnings.Add($"Could not inspect {directory}: {ex.Message}");
                }
            }
        }
    }

    private static IEnumerable<string> ResolveRoots(string rootPath, List<string> warnings)
    {
        var expanded = Environment.ExpandEnvironmentVariables(rootPath);
        expanded = expanded.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        if (!ContainsWildcard(expanded))
        {
            if (Directory.Exists(expanded))
            {
                yield return PathSafety.Normalize(expanded);
            }

            yield break;
        }

        var root = Path.GetPathRoot(expanded);
        if (string.IsNullOrWhiteSpace(root))
        {
            warnings.Add($"Could not resolve cleanup root: {rootPath}");
            yield break;
        }

        var segments = expanded[root.Length..]
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

        IEnumerable<string> current = [root];
        foreach (var segment in segments)
        {
            var next = new List<string>();
            foreach (var basePath in current)
            {
                try
                {
                    if (ContainsWildcard(segment))
                    {
                        next.AddRange(Directory.EnumerateDirectories(basePath, segment, SearchOption.TopDirectoryOnly));
                    }
                    else
                    {
                        var candidate = Path.Combine(basePath, segment);
                        if (Directory.Exists(candidate))
                        {
                            next.Add(candidate);
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    warnings.Add($"Could not resolve {basePath}: {ex.Message}");
                }
            }

            current = next;
        }

        foreach (var path in current)
        {
            if (Directory.Exists(path))
            {
                yield return PathSafety.Normalize(path);
            }
        }
    }

    private static IEnumerable<string> SafeEnumerateFiles(string root, string searchPattern, EnumerationOptions options, List<string> warnings)
    {
        try
        {
            return Directory.EnumerateFiles(root, searchPattern, options);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            warnings.Add($"Could not scan files in {root}: {ex.Message}");
            return [];
        }
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string root, string searchPattern, EnumerationOptions options, List<string> warnings)
    {
        try
        {
            return Directory.EnumerateDirectories(root, searchPattern, options);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            warnings.Add($"Could not scan folders in {root}: {ex.Message}");
            return [];
        }
    }

    private static long GetDirectorySize(string directory)
    {
        var total = 0L;
        var files = new ConcurrentBag<string>();

        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*", new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            }))
            {
                files.Add(file);
            }
        }
        catch
        {
            return 0;
        }

        foreach (var file in files)
        {
            try
            {
                var info = new FileInfo(file);
                if (info.Exists && !HasReparsePoint(info.Attributes))
                {
                    total += Math.Max(0, info.Length);
                }
            }
            catch
            {
                // Size is an estimate; inaccessible files are skipped.
            }
        }

        return total;
    }

    private static void ClearReadOnly(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReadOnly) != 0)
        {
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        }
    }

    private static void ClearReadOnlyRecursive(string path)
    {
        foreach (var file in Directory.EnumerateFiles(path, "*", new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        }))
        {
            try
            {
                ClearReadOnly(file);
            }
            catch
            {
                // The delete operation will report any remaining access errors.
            }
        }
    }

    private static bool ContainsWildcard(string value)
    {
        return value.Contains('*') || value.Contains('?');
    }

    private static bool HasReparsePoint(FileAttributes attributes)
    {
        return (attributes & FileAttributes.ReparsePoint) != 0;
    }
}
