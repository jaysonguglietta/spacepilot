using System.Text.Json;
using SpacePilot.Models;

namespace SpacePilot.Services;

public sealed class QuarantineRunResult
{
    public List<QuarantineEntry> Entries { get; } = [];
    public List<string> Warnings { get; } = [];
    public int QuarantinedCount => Entries.Count;
    public long QuarantinedBytes => Entries.Sum(entry => entry.SizeBytes);
}

public sealed class QuarantineService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _rootPath;
    private readonly string _manifestPath;

    public QuarantineService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            appData = AppContext.BaseDirectory;
        }

        _rootPath = Path.Combine(appData, "SpacePilot", "Quarantine");
        _manifestPath = Path.Combine(_rootPath, "manifest.json");
    }

    public Task<IReadOnlyList<QuarantineEntry>> GetEntriesAsync()
    {
        return Task.Run<IReadOnlyList<QuarantineEntry>>(() => LoadManifest()
            .Where(entry => File.Exists(entry.PayloadPath) || Directory.Exists(entry.PayloadPath))
            .OrderByDescending(entry => entry.QuarantinedUtc)
            .ToList());
    }

    public Task<QuarantineRunResult> QuarantineAsync(IEnumerable<CleanupCandidate> candidates, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            Directory.CreateDirectory(_rootPath);
            var manifest = LoadManifest();
            var result = new QuarantineRunResult();

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
                    if (candidate.Kind == CleanupTargetKind.File && !File.Exists(candidate.Path))
                    {
                        continue;
                    }

                    if (candidate.Kind == CleanupTargetKind.Directory && !Directory.Exists(candidate.Path))
                    {
                        continue;
                    }

                    var id = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}";
                    var entryDirectory = Path.Combine(_rootPath, id);
                    Directory.CreateDirectory(entryDirectory);
                    var payloadPath = Path.Combine(entryDirectory, "payload");

                    if (candidate.Kind == CleanupTargetKind.File)
                    {
                        MoveFile(candidate.Path, payloadPath);
                    }
                    else
                    {
                        MoveDirectory(candidate.Path, payloadPath);
                    }

                    var entry = new QuarantineEntry
                    {
                        Id = id,
                        OriginalPath = candidate.Path,
                        PayloadPath = payloadPath,
                        DisplayName = candidate.DisplayName,
                        CategoryName = candidate.CategoryName,
                        Kind = candidate.Kind,
                        SizeBytes = candidate.SizeBytes,
                        QuarantinedUtc = DateTime.UtcNow,
                        OriginalLastModifiedUtc = candidate.LastModifiedUtc
                    };

                    manifest.Add(entry);
                    result.Entries.Add(entry);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    result.Warnings.Add($"Could not quarantine {candidate.Path}: {ex.Message}");
                }
            }

            SaveManifest(manifest);
            return result;
        }, cancellationToken);
    }

    public Task<List<string>> RestoreAsync(IEnumerable<string> entryIds, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var warnings = new List<string>();
            var requested = entryIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var manifest = LoadManifest();

            foreach (var entry in manifest.Where(entry => requested.Contains(entry.Id)).ToList())
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (File.Exists(entry.OriginalPath) || Directory.Exists(entry.OriginalPath))
                    {
                        warnings.Add($"Skipped restore because the original path already exists: {entry.OriginalPath}");
                        continue;
                    }

                    var parent = Path.GetDirectoryName(entry.OriginalPath);
                    if (!string.IsNullOrWhiteSpace(parent))
                    {
                        Directory.CreateDirectory(parent);
                    }

                    if (entry.Kind == CleanupTargetKind.File)
                    {
                        MoveFile(entry.PayloadPath, entry.OriginalPath);
                    }
                    else
                    {
                        MoveDirectory(entry.PayloadPath, entry.OriginalPath);
                    }

                    DeleteEntryFolder(entry);
                    manifest.Remove(entry);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    warnings.Add($"Could not restore {entry.OriginalPath}: {ex.Message}");
                }
            }

            SaveManifest(manifest);
            return warnings;
        }, cancellationToken);
    }

    public Task<List<string>> PurgeAsync(IEnumerable<string> entryIds, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var warnings = new List<string>();
            var requested = entryIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var manifest = LoadManifest();

            foreach (var entry in manifest.Where(entry => requested.Contains(entry.Id)).ToList())
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    DeleteEntryFolder(entry);
                    manifest.Remove(entry);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    warnings.Add($"Could not purge {entry.DisplayName}: {ex.Message}");
                }
            }

            SaveManifest(manifest);
            return warnings;
        }, cancellationToken);
    }

    public Task<int> PurgeExpiredAsync(int retentionDays, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            if (retentionDays <= 0)
            {
                return 0;
            }

            var cutoffUtc = DateTime.UtcNow.AddDays(-retentionDays);
            var expired = LoadManifest()
                .Where(entry => entry.QuarantinedUtc < cutoffUtc)
                .Select(entry => entry.Id)
                .ToList();

            if (expired.Count == 0)
            {
                return 0;
            }

            cancellationToken.ThrowIfCancellationRequested();
            PurgeAsync(expired, cancellationToken).GetAwaiter().GetResult();
            return expired.Count;
        }, cancellationToken);
    }

    private List<QuarantineEntry> LoadManifest()
    {
        try
        {
            if (!File.Exists(_manifestPath))
            {
                return [];
            }

            var json = File.ReadAllText(_manifestPath);
            return JsonSerializer.Deserialize<List<QuarantineEntry>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void SaveManifest(List<QuarantineEntry> entries)
    {
        Directory.CreateDirectory(_rootPath);
        File.WriteAllText(_manifestPath, JsonSerializer.Serialize(entries, JsonOptions));
    }

    private static void MoveFile(string sourcePath, string destinationPath)
    {
        ClearReadOnly(sourcePath);
        try
        {
            File.Move(sourcePath, destinationPath);
        }
        catch (IOException)
        {
            File.Copy(sourcePath, destinationPath, overwrite: false);
            File.Delete(sourcePath);
        }
    }

    private static void MoveDirectory(string sourcePath, string destinationPath)
    {
        ClearReadOnlyRecursive(sourcePath);
        try
        {
            Directory.Move(sourcePath, destinationPath);
        }
        catch (IOException)
        {
            CopyDirectory(sourcePath, destinationPath);
            Directory.Delete(sourcePath, recursive: true);
        }
    }

    private static void CopyDirectory(string sourcePath, string destinationPath)
    {
        Directory.CreateDirectory(destinationPath);

        foreach (var file in Directory.EnumerateFiles(sourcePath, "*", new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        }))
        {
            var destinationFile = Path.Combine(destinationPath, Path.GetFileName(file));
            File.Copy(file, destinationFile, overwrite: false);
        }

        foreach (var directory in Directory.EnumerateDirectories(sourcePath, "*", new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            AttributesToSkip = FileAttributes.ReparsePoint
        }))
        {
            CopyDirectory(directory, Path.Combine(destinationPath, Path.GetFileName(directory)));
        }
    }

    private static void DeleteEntryFolder(QuarantineEntry entry)
    {
        var entryFolder = Directory.GetParent(entry.PayloadPath)?.FullName;
        if (!string.IsNullOrWhiteSpace(entryFolder) && Directory.Exists(entryFolder))
        {
            Directory.Delete(entryFolder, recursive: true);
        }
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
                // The move/delete operation reports remaining failures.
            }
        }
    }
}
