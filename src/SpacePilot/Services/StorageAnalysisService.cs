using System.Security.Cryptography;
using SpacePilot.Models;

namespace SpacePilot.Services;

public sealed class StorageAnalysisService
{
    public Task<StorageMapResult> ScanStorageMapAsync(int maxFolders = 120, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var result = new StorageMapResult();
            var fileTypes = new Dictionary<string, (long SizeBytes, int FileCount)>(StringComparer.OrdinalIgnoreCase);

            foreach (var root in GetUserStorageRoots())
            {
                cancellationToken.ThrowIfCancellationRequested();
                ScanFolderUsage(root, result, fileTypes, cancellationToken);
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

    public Task<StorageScanResult<LargeFileInfo>> ScanLargeFilesAsync(int minimumSizeMb, int maxResults = 250, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var result = new StorageScanResult<LargeFileInfo>();
            var minimumBytes = Math.Max(1, minimumSizeMb) * 1024L * 1024L;

            foreach (var root in GetUserStorageRoots())
            {
                cancellationToken.ThrowIfCancellationRequested();
                ScanLargeFileRoot(root, minimumBytes, result, cancellationToken);
            }

            result.Items.Sort((left, right) => right.SizeBytes.CompareTo(left.SizeBytes));
            if (result.Items.Count > maxResults)
            {
                result.Items.RemoveRange(maxResults, result.Items.Count - maxResults);
            }

            return result;
        }, cancellationToken);
    }

    private static void ScanFolderUsage(
        string root,
        StorageMapResult result,
        Dictionary<string, (long SizeBytes, int FileCount)> fileTypes,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        try
        {
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
                            result.Warnings.Add($"Could not inspect {file}: {ex.Message}");
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
                    result.Warnings.Add($"Could not scan folder {directory}: {ex.Message}");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            result.Warnings.Add($"Could not scan {root}: {ex.Message}");
        }
    }

    public Task<StorageScanResult<DuplicateFileInfo>> ScanDuplicatesAsync(int minimumSizeMb, int maxGroups = 100, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var result = new StorageScanResult<DuplicateFileInfo>();
            var minimumBytes = Math.Max(1, minimumSizeMb) * 1024L * 1024L;
            var filesBySize = new Dictionary<long, List<FileInfo>>();

            foreach (var root in GetUserStorageRoots())
            {
                cancellationToken.ThrowIfCancellationRequested();
                CollectDuplicateCandidates(root, minimumBytes, filesBySize, result.Warnings, cancellationToken);
            }

            var groupsCreated = 0;
            foreach (var group in filesBySize.Where(pair => pair.Value.Count > 1).OrderByDescending(pair => pair.Key))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var filesByHash = new Dictionary<string, List<FileInfo>>(StringComparer.OrdinalIgnoreCase);
                foreach (var file in group.Value)
                {
                    try
                    {
                        var hash = ComputeHash(file.FullName);
                        if (!filesByHash.TryGetValue(hash, out var files))
                        {
                            files = [];
                            filesByHash[hash] = files;
                        }

                        files.Add(file);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                    {
                        result.Warnings.Add($"Could not hash {file.FullName}: {ex.Message}");
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
                        return result;
                    }
                }
            }

            return result;
        }, cancellationToken);
    }

    private static void ScanLargeFileRoot(string root, long minimumBytes, StorageScanResult<LargeFileInfo> result, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", CreateUserFileEnumerationOptions()))
            {
                cancellationToken.ThrowIfCancellationRequested();

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
                    result.Warnings.Add($"Could not inspect {file}: {ex.Message}");
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            result.Warnings.Add($"Could not scan {root}: {ex.Message}");
        }
    }

    private static void CollectDuplicateCandidates(
        string root,
        long minimumBytes,
        Dictionary<long, List<FileInfo>> filesBySize,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", CreateUserFileEnumerationOptions()))
            {
                cancellationToken.ThrowIfCancellationRequested();

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

    private static string ComputeHash(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash);
    }
}
