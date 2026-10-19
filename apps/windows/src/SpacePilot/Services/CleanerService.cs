using System.Collections.Concurrent;
using System.IO;
using SpacePilot.Models;

namespace SpacePilot.Services;

public sealed class CleanerService
{
    private const int MaxScanCandidates = 5000;
    private readonly ProtectionPolicyService _protectionPolicy = new();
    private readonly StorageScanLimits _scanLimits;

    public CleanerService() : this(StorageScanLimits.Default)
    {
    }

    internal CleanerService(StorageScanLimits scanLimits)
    {
        _scanLimits = scanLimits;
    }

    public Task<CleanupScanResult> ScanAsync(IEnumerable<CleanupRule> rules, CancellationToken cancellationToken = default, IProgress<string>? progress = null)
    {
        return Task.Run(() =>
        {
            var result = new CleanupScanResult();
            var budget = new StorageScanBudget(_scanLimits);
            var warnings = new StorageWarningCollector(result.Warnings);
            var stopped = false;

            foreach (var rule in rules)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (result.Candidates.Count >= MaxScanCandidates || budget.IsLimitReached || stopped)
                {
                    break;
                }
                progress?.Report($"Scanning {rule.Name}...");

                foreach (var location in rule.Locations)
                {
                    foreach (var root in ResolveRoots(location.RootPath, warnings, budget))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (result.Candidates.Count >= MaxScanCandidates || budget.IsLimitReached)
                        {
                            stopped = true;
                            break;
                        }
                        if (!ScanLocation(rule, location, root, result, warnings, budget, cancellationToken))
                        {
                            stopped = true;
                            break;
                        }
                    }
                    if (stopped || budget.IsLimitReached)
                    {
                        break;
                    }
                }
            }

            if (result.Candidates.Count >= MaxScanCandidates
                && !result.Warnings.Any(warning => warning.StartsWith("Cleanup scan stopped at ", StringComparison.Ordinal)))
            {
                result.Warnings.Add($"Cleanup scan stopped at {MaxScanCandidates} files. Clean reviewed items, then scan again.");
            }
            if (budget.IsLimitReached)
            {
                result.Warnings.Add($"Cleanup scan stopped after inspecting {budget.InspectedFiles:N0} entries or {_scanLimits.MaxDuration.TotalMinutes:0} minutes. Results are partial; review the scanned files, then scan again.");
            }

            return result;
        }, cancellationToken);
    }

    public Task<CleanupRunResult> CleanAsync(IEnumerable<CleanupCandidate> candidates, UserPreferences? preferences = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var result = new CleanupRunResult();

            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (candidate.Kind != CleanupTargetKind.File)
                {
                    result.Warnings.Add($"Skipped directory cleanup because its contents can change during recursive deletion: {candidate.Path}. Select verified files instead.");
                    continue;
                }

                if (!PathSafety.IsAllowedCleanupTarget(candidate.Path, candidate.ApprovedRoot))
                {
                    result.Warnings.Add($"Skipped unsafe target: {candidate.Path}");
                    continue;
                }

                if (PathSafety.ContainsReparsePoint(candidate.Path))
                {
                    result.Warnings.Add($"Skipped path through a reparse point: {candidate.Path}");
                    continue;
                }

                if (preferences is not null && _protectionPolicy.IsProtected(candidate.Path, preferences))
                {
                    result.Warnings.Add($"Skipped protected target or descendant: {candidate.Path}");
                    continue;
                }

                try
                {
                    if ((File.GetAttributes(candidate.Path) & FileAttributes.ReparsePoint) != 0)
                    {
                        result.Warnings.Add($"Skipped reparse point: {candidate.Path}");
                        continue;
                    }
                    if (candidate.Kind == CleanupTargetKind.File)
                    {
                        if (!File.Exists(candidate.Path))
                        {
                            continue;
                        }

                        if (candidate.RequiresScanMatch)
                        {
                            var current = new FileInfo(candidate.Path);
                            if (!current.Exists || candidate.LastModifiedUtc is null
                                || current.Length != candidate.SizeBytes
                                || current.LastWriteTimeUtc != candidate.LastModifiedUtc.Value)
                            {
                                result.Warnings.Add($"Skipped changed file; scan again: {candidate.Path}");
                                continue;
                            }
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

                        result.Warnings.Add($"Skipped directory cleanup because its contents can change during recursive deletion: {candidate.Path}. Select verified files instead.");
                        continue;
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

    private static bool ScanLocation(CleanupRule rule, CleanupLocation location, string root, CleanupScanResult result,
        StorageWarningCollector warnings, StorageScanBudget budget, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(root))
        {
            return true;
        }

        var cutoffUtc = DateTime.UtcNow.AddDays(-rule.MinimumAgeDays);
        bool AddCandidate(string file)
        {
            if (result.Candidates.Count >= MaxScanCandidates)
            {
                return false;
            }

            try
            {
                var info = new FileInfo(file);
                if (!info.Exists || info.LastWriteTimeUtc > cutoffUtc || HasReparsePoint(info.Attributes)
                    || !PathSafety.IsAllowedCleanupTarget(info.FullName, root)
                    || PathSafety.ContainsReparsePoint(info.FullName))
                {
                    return true;
                }

                result.Candidates.Add(new CleanupCandidate(rule.Id, rule.Name, info.Name, info.FullName, root,
                    CleanupTargetKind.File, Math.Max(0, info.Length), info.LastWriteTimeUtc, rule.Risk,
                    RequiresScanMatch: true));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                warnings.Add($"Could not inspect {file}: {ex.Message}");
            }

            return result.Candidates.Count < MaxScanCandidates;
        }

        if (location.IncludeFiles)
        {
            if (!BoundedFileWalker.Walk(root, location.SearchPattern, location.Recursive && !location.IncludeDirectories,
                    budget, warnings, cancellationToken, AddCandidate))
            {
                return false;
            }
        }

        if (location.IncludeDirectories)
        {
            if (!BoundedFileWalker.Walk(root, "*", false, budget, warnings, cancellationToken,
                    _ => true,
                    directory =>
                    {
                        if (!BoundedFileWalker.MatchesName(location.SearchPattern, Path.GetFileName(directory)))
                        {
                            return true;
                        }

                        return BoundedFileWalker.Walk(directory, "*", true, budget, warnings, cancellationToken, AddCandidate);
                    }))
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<string> ResolveRoots(string rootPath, StorageWarningCollector warnings, StorageScanBudget budget)
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
                        foreach (var directory in Directory.EnumerateDirectories(basePath, "*", SearchOption.TopDirectoryOnly))
                        {
                            if (!budget.TryInspect())
                            {
                                break;
                            }
                            if (!PathSafety.ContainsReparsePoint(directory)
                                && BoundedFileWalker.MatchesName(segment, Path.GetFileName(directory)))
                            {
                                next.Add(directory);
                            }
                        }
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
                if (budget.IsLimitReached)
                {
                    yield break;
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
