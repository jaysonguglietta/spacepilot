using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using SpacePilot.Models;

namespace SpacePilot.Services;

public sealed class StorageAnalysisService
{
    private readonly IReadOnlyList<string> _roots;
    private readonly StorageScanLimits _limits;

    public StorageAnalysisService() : this(GetUserStorageRoots(), StorageScanLimits.Default)
    {
    }

    internal StorageAnalysisService(IReadOnlyList<string> roots, StorageScanLimits limits)
    {
        _roots = roots;
        _limits = limits;
    }

    public Task<StorageMapResult> ScanStorageMapAsync(int maxFolders = 120, CancellationToken cancellationToken = default, IProgress<string>? progress = null)
    {
        return Task.Run(() =>
        {
            var result = new StorageMapResult();
            var fileTypes = new Dictionary<string, (long SizeBytes, int FileCount)>(StringComparer.OrdinalIgnoreCase);
            var warnings = new StorageWarningCollector(result.Warnings);

            foreach (var root in _roots)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report($"Mapping {root}...");
                ScanFolderUsage(root, result, fileTypes, warnings, cancellationToken);
            }

            result.Folders.Sort((left, right) => right.SizeBytes.CompareTo(left.SizeBytes));
            if (result.Folders.Count > maxFolders)
            {
                result.Folders.RemoveRange(maxFolders, result.Folders.Count - maxFolders);
            }

            foreach (var item in fileTypes.OrderByDescending(pair => pair.Value.SizeBytes).Take(80))
            {
                result.FileTypes.Add(new FileTypeUsageInfo(
                    string.IsNullOrWhiteSpace(item.Key) ? "(none)" : item.Key,
                    CategorizeExtension(item.Key),
                    item.Value.SizeBytes,
                    item.Value.FileCount));
            }

            return result;
        }, cancellationToken);
    }

    public Task<StorageScanResult<LargeFileInfo>> ScanLargeFilesAsync(int minimumSizeMb, int maxResults = 250, CancellationToken cancellationToken = default, IProgress<string>? progress = null)
    {
        return Task.Run(() =>
        {
            var result = new StorageScanResult<LargeFileInfo>();
            var minimumBytes = Math.Max(1, minimumSizeMb) * 1024L * 1024L;
            var budget = new StorageScanBudget(_limits);
            var warnings = new StorageWarningCollector(result.Warnings);

            foreach (var root in _roots)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report($"Finding large files in {root}...");
                if (!ScanLargeFileRoot(root, minimumBytes, result, warnings, budget, cancellationToken, progress))
                {
                    result.Warnings.Add($"Large-file scan stopped after inspecting {budget.InspectedFiles:N0} files or {_limits.MaxDuration.TotalMinutes:0} minutes. Results are partial; scan again to refresh them.");
                    break;
                }
            }

            result.Items.Sort((left, right) => right.SizeBytes.CompareTo(left.SizeBytes));
            if (result.Items.Count > maxResults)
            {
                result.Items.RemoveRange(maxResults, result.Items.Count - maxResults);
                result.Warnings.Add($"Only the largest {maxResults} files are shown. Additional matches were found; results are partial.");
            }

            return result;
        }, cancellationToken);
    }

    private static void ScanFolderUsage(
        string root,
        StorageMapResult result,
        Dictionary<string, (long SizeBytes, int FileCount)> fileTypes,
        StorageWarningCollector warnings,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        try
        {
            var rootFileBytes = 0L;
            var rootFileCount = 0;
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var info = new FileInfo(file);
                    if (!info.Exists || IsSystemOrHidden(info.Attributes)
                        || (info.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }

                    rootFileBytes += info.Length;
                    rootFileCount++;
                    var extension = info.Extension.ToLowerInvariant();
                    fileTypes.TryGetValue(extension, out var totals);
                    fileTypes[extension] = (totals.SizeBytes + info.Length, totals.FileCount + 1);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    warnings.Add($"Could not inspect {file}: {ex.Message}");
                }
            }

            if (rootFileCount > 0)
            {
                result.Folders.Add(new FolderUsageInfo(
                    root,
                    $"{Path.GetFileName(root)} (top level)",
                    rootFileBytes,
                    rootFileCount,
                    0,
                    BuildFolderRecommendation(root)));
            }

            foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var folderSize = 0L;
                var fileCount = 0;
                var folderCount = 0;

                try
                {
                    foreach (var file in Directory.EnumerateFiles(directory, "*", CreateUserFileEnumerationOptions()))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try
                        {
                            var info = new FileInfo(file);
                            if (!info.Exists || IsSystemOrHidden(info.Attributes))
                            {
                                continue;
                            }

                            folderSize += info.Length;
                            fileCount++;
                            var extension = info.Extension.ToLowerInvariant();
                            fileTypes.TryGetValue(extension, out var totals);
                            fileTypes[extension] = (totals.SizeBytes + info.Length, totals.FileCount + 1);
                        }
                        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                        {
                            warnings.Add($"Could not inspect {file}: {ex.Message}");
                        }
                    }

                    folderCount = Directory.EnumerateDirectories(directory, "*", CreateUserFileEnumerationOptions()).Count();
                    result.Folders.Add(new FolderUsageInfo(
                        directory,
                        Path.GetFileName(directory),
                        folderSize,
                        fileCount,
                        folderCount,
                        BuildFolderRecommendation(directory)));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    warnings.Add($"Could not scan folder {directory}: {ex.Message}");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            warnings.Add($"Could not scan {root}: {ex.Message}");
        }
    }

    public Task<StorageScanResult<DuplicateFileInfo>> ScanDuplicatesAsync(int minimumSizeMb, int maxGroups = 100, CancellationToken cancellationToken = default, IProgress<string>? progress = null)
    {
        return Task.Run(() =>
        {
            var result = new StorageScanResult<DuplicateFileInfo>();
            var minimumBytes = Math.Max(1, minimumSizeMb) * 1024L * 1024L;
            var filesBySize = new Dictionary<long, List<FileInfo>>();
            var budget = new StorageScanBudget(_limits);
            var warnings = new StorageWarningCollector(result.Warnings);

            foreach (var root in _roots)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report($"Finding duplicate candidates in {root}...");
                if (!CollectDuplicateCandidates(root, minimumBytes, filesBySize, warnings, budget, cancellationToken, progress))
                {
                    result.Warnings.Add($"Duplicate candidate search stopped after inspecting {budget.InspectedFiles:N0} files or {_limits.MaxDuration.TotalMinutes:0} minutes. Results are partial.");
                    break;
                }
            }

            var groupsCreated = 0;
            foreach (var group in filesBySize.Where(pair => pair.Value.Count > 1).OrderByDescending(pair => pair.Key))
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report($"Verifying {group.Value.Count} files of {group.Key / (1024 * 1024)} MB...");

                var filesByHash = new Dictionary<string, List<FileInfo>>(StringComparer.OrdinalIgnoreCase);
                var verificationStopped = false;
                foreach (var file in group.Value)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (budget.IsTimeExpired || budget.RemainingHashBytes == 0)
                    {
                        result.Warnings.Add($"Duplicate verification stopped after hashing {budget.HashedBytes:N0} bytes or {_limits.MaxDuration.TotalMinutes:0} minutes. Results are partial.");
                        verificationStopped = true;
                        break;
                    }

                    try
                    {
                        if (file.Length > budget.RemainingHashBytes)
                        {
                            budget.SkippedOversizeHashes = true;
                            continue;
                        }

                        var hash = ComputeHash(file.FullName, budget, cancellationToken);
                        if (!filesByHash.TryGetValue(hash, out var files))
                        {
                            files = [];
                            filesByHash[hash] = files;
                        }

                        files.Add(file);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                    {
                        warnings.Add($"Could not hash {file.FullName}: {ex.Message}");
                    }
                    catch (StorageScanLimitException)
                    {
                        result.Warnings.Add($"Duplicate verification stopped after hashing {budget.HashedBytes:N0} bytes or {_limits.MaxDuration.TotalMinutes:0} minutes. Results are partial.");
                        verificationStopped = true;
                        break;
                    }
                }

                foreach (var duplicateGroup in filesByHash.Where(pair => pair.Value.Count > 1))
                {
                    var orderedFiles = duplicateGroup.Value
                        .OrderByDescending(file => file.LastWriteTimeUtc)
                        .ToList();
                    var groupId = $"Duplicate set {groupsCreated + 1}";

                    for (var index = 0; index < orderedFiles.Count; index++)
                    {
                        var file = orderedFiles[index];
                        result.Items.Add(new DuplicateFileInfo(
                            groupId,
                            duplicateGroup.Key,
                            file.FullName,
                            file.Name,
                            file.DirectoryName ?? string.Empty,
                            file.Length,
                            file.LastWriteTimeUtc,
                            index > 0));
                    }

                    groupsCreated++;
                    if (groupsCreated >= maxGroups)
                    {
                        result.Warnings.Add($"Only the first {maxGroups} duplicate sets are shown. Additional sets may exist; results are partial.");
                        if (budget.SkippedOversizeHashes)
                        {
                            result.Warnings.Add("Some duplicate candidates exceeded the remaining hash budget and were not verified. Results are partial.");
                        }
                        return result;
                    }
                }

                if (verificationStopped)
                {
                    return result;
                }
            }

            if (budget.SkippedOversizeHashes)
            {
                result.Warnings.Add("Some duplicate candidates exceeded the remaining hash budget and were not verified. Results are partial.");
            }

            return result;
        }, cancellationToken);
    }

    private static bool ScanLargeFileRoot(string root, long minimumBytes, StorageScanResult<LargeFileInfo> result,
        StorageWarningCollector warnings, StorageScanBudget budget, CancellationToken cancellationToken, IProgress<string>? progress)
    {
        if (!Directory.Exists(root))
        {
            return true;
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", CreateUserFileEnumerationOptions()))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!budget.TryInspect())
                {
                    return false;
                }
                if (budget.InspectedFiles % 2000 == 0)
                {
                    progress?.Report($"Inspected {budget.InspectedFiles:N0} files for large items...");
                }

                try
                {
                    var info = new FileInfo(file);
                    if (!info.Exists || info.Length < minimumBytes || IsSystemOrHidden(info.Attributes))
                    {
                        continue;
                    }

                    result.Items.Add(new LargeFileInfo(
                        info.FullName,
                        info.Name,
                        info.DirectoryName ?? string.Empty,
                        info.Extension,
                        info.Length,
                        info.LastWriteTimeUtc,
                        BuildLargeFileRecommendation(info)));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    warnings.Add($"Could not inspect {file}: {ex.Message}");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            warnings.Add($"Could not scan {root}: {ex.Message}");
        }

        return true;
    }

    private static bool CollectDuplicateCandidates(
        string root,
        long minimumBytes,
        Dictionary<long, List<FileInfo>> filesBySize,
        StorageWarningCollector warnings,
        StorageScanBudget budget,
        CancellationToken cancellationToken,
        IProgress<string>? progress)
    {
        if (!Directory.Exists(root))
        {
            return true;
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", CreateUserFileEnumerationOptions()))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!budget.TryInspect())
                {
                    return false;
                }
                if (budget.InspectedFiles % 2000 == 0)
                {
                    progress?.Report($"Inspected {budget.InspectedFiles:N0} files for duplicates...");
                }

                try
                {
                    var info = new FileInfo(file);
                    if (!info.Exists || info.Length < minimumBytes || IsSystemOrHidden(info.Attributes))
                    {
                        continue;
                    }

                    if (!filesBySize.TryGetValue(info.Length, out var files))
                    {
                        files = [];
                        filesBySize[info.Length] = files;
                    }

                    files.Add(info);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    warnings.Add($"Could not inspect {file}: {ex.Message}");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            warnings.Add($"Could not scan {root}: {ex.Message}");
        }

        return true;
    }

    private static IReadOnlyList<string> GetUserStorageRoots()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var roots = new[]
        {
            Path.Combine(userProfile, "Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)
        };

        return roots
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static EnumerationOptions CreateUserFileEnumerationOptions()
    {
        return new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint | FileAttributes.System
        };
    }

    private static bool IsSystemOrHidden(FileAttributes attributes)
    {
        return (attributes & FileAttributes.System) != 0;
    }

    private static string BuildLargeFileRecommendation(FileInfo info)
    {
        if (info.DirectoryName?.Contains("Downloads", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "Review download; often safe to archive or remove after use.";
        }

        if (info.Extension.Equals(".iso", StringComparison.OrdinalIgnoreCase)
            || info.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase)
            || info.Extension.Equals(".7z", StringComparison.OrdinalIgnoreCase))
        {
            return "Archive/package; confirm it is no longer needed.";
        }

        if (info.Extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase)
            || info.Extension.Equals(".mov", StringComparison.OrdinalIgnoreCase))
        {
            return "Large media; move to external storage if you want local space back.";
        }

        return "Manual review recommended before cleanup.";
    }

    private static string BuildFolderRecommendation(string directory)
    {
        if (directory.Contains("Downloads", StringComparison.OrdinalIgnoreCase))
        {
            return "Downloads often contain installers, archives, and old exports worth reviewing.";
        }

        if (directory.Contains("Videos", StringComparison.OrdinalIgnoreCase))
        {
            return "Video folders can usually be moved to external storage without affecting Windows.";
        }

        if (directory.Contains("Pictures", StringComparison.OrdinalIgnoreCase))
        {
            return "Review before deleting; consider archiving rather than removing.";
        }

        return "Inspect large subfolders before taking action.";
    }

    private static string CategorizeExtension(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".mp4" or ".mov" or ".mkv" or ".avi" => "Video",
            ".jpg" or ".jpeg" or ".png" or ".heic" or ".gif" => "Images",
            ".zip" or ".7z" or ".rar" or ".iso" => "Archives",
            ".exe" or ".msi" or ".msix" => "Installers",
            ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx" => "Documents",
            ".mp3" or ".wav" or ".flac" => "Audio",
            _ => "Other"
        };
    }

    private static string ComputeHash(string path, StorageScanBudget budget, CancellationToken cancellationToken)
    {
        using var stream = File.OpenRead(path);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[1024 * 1024];
        int count;
        while ((count = stream.Read(buffer)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!budget.TryHashBytes(count))
            {
                throw new StorageScanLimitException();
            }
            hash.AppendData(buffer, 0, count);
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }
}

internal sealed record StorageScanLimits(int MaxInspectedFiles, long MaxHashBytes, TimeSpan MaxDuration)
{
    public static StorageScanLimits Default { get; } = new(100_000, 20L * 1024 * 1024 * 1024, TimeSpan.FromMinutes(2));
}

internal sealed class StorageScanBudget(StorageScanLimits limits)
{
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    public int InspectedFiles { get; private set; }
    public long HashedBytes { get; private set; }
    public bool SkippedOversizeHashes { get; set; }
    public bool IsTimeExpired => _clock.Elapsed >= limits.MaxDuration;
    public long RemainingHashBytes => Math.Max(0, limits.MaxHashBytes - HashedBytes);

    public bool TryInspect()
    {
        if (InspectedFiles >= limits.MaxInspectedFiles || IsTimeExpired)
        {
            return false;
        }

        InspectedFiles++;
        return true;
    }

    public bool TryHashBytes(int count)
    {
        if (IsTimeExpired || count > RemainingHashBytes)
        {
            return false;
        }

        HashedBytes += count;
        return true;
    }
}

internal sealed class StorageScanLimitException : Exception
{
}

internal sealed class StorageWarningCollector(List<string> warnings)
{
    private const int DetailedLimit = 50;
    private int _suppressed;

    public void Add(string warning)
    {
        if (warnings.Count < DetailedLimit)
        {
            warnings.Add(warning);
            return;
        }

        _suppressed++;
        var summary = $"{_suppressed:N0} additional path warnings were suppressed.";
        if (warnings.Count == DetailedLimit)
        {
            warnings.Add(summary);
        }
        else
        {
            warnings[DetailedLimit] = summary;
        }
    }
}
