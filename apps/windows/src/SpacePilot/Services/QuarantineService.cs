using System.IO;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    private readonly string _manifestLockPath;
    private readonly TimeSpan _manifestLockTimeout;
    private bool _manifestNeedsBackup;
    public string? RecoveryWarning { get; private set; }
    private static readonly Regex EntryIdPattern = new("^[0-9]{17}-[0-9a-fA-F]{32}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly ProtectionPolicyService _protectionPolicy = new();

    public QuarantineService(string? appDataRoot = null) : this(appDataRoot, TimeSpan.FromSeconds(10))
    {
    }

    internal QuarantineService(string? appDataRoot, TimeSpan manifestLockTimeout)
    {
        var appData = appDataRoot ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            appData = AppContext.BaseDirectory;
        }

        _rootPath = Path.Combine(appData, "SpacePilot", "Quarantine");
        _manifestPath = Path.Combine(_rootPath, "manifest.json");
        _manifestLockPath = Path.Combine(_rootPath, "manifest.lock");
        _manifestLockTimeout = manifestLockTimeout;
    }

    public Task<IReadOnlyList<QuarantineEntry>> GetEntriesAsync()
    {
        return Task.Run<IReadOnlyList<QuarantineEntry>>(() =>
        {
            using var manifestLock = AcquireManifestLock(CancellationToken.None);
            return LoadManifest().OrderByDescending(entry => entry.QuarantinedUtc).ToList();
        });
    }

    public Task<QuarantineRunResult> QuarantineAsync(IEnumerable<CleanupCandidate> candidates, UserPreferences? preferences = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            using var manifestLock = AcquireManifestLock(cancellationToken);
            var manifest = LoadManifest();
            Directory.CreateDirectory(_rootPath);
            var result = new QuarantineRunResult();

            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!PathSafety.IsAllowedCleanupTarget(candidate.Path, candidate.ApprovedRoot))
                {
                    result.Warnings.Add($"Skipped unsafe target: {candidate.Path}");
                    continue;
                }

                if (PathSafety.ContainsReparsePoint(candidate.Path) || PathSafety.ContainsReparsePoint(_rootPath))
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
                    if (candidate.Kind == CleanupTargetKind.File && !File.Exists(candidate.Path))
                    {
                        continue;
                    }

                    if (candidate.Kind == CleanupTargetKind.Directory && !Directory.Exists(candidate.Path))
                    {
                        continue;
                    }

                    if (candidate.Kind == CleanupTargetKind.Directory)
                    {
                        result.Warnings.Add($"Skipped directory cleanup because its contents can change during a recursive move: {candidate.Path}. Select verified files instead.");
                        continue;
                    }

                    if ((File.GetAttributes(candidate.Path) & FileAttributes.ReparsePoint) != 0)
                    {
                        result.Warnings.Add($"Skipped reparse point: {candidate.Path}");
                        continue;
                    }

                    if (candidate.RequiresScanMatch)
                    {
                        var info = new FileInfo(candidate.Path);
                        if (candidate.Kind != CleanupTargetKind.File || !info.Exists
                            || info.Length != candidate.SizeBytes
                            || candidate.LastModifiedUtc is null
                            || info.LastWriteTimeUtc != candidate.LastModifiedUtc.Value)
                        {
                            result.Warnings.Add($"Skipped changed file; scan again: {candidate.Path}");
                            continue;
                        }
                    }

                    if (candidate.ExpectedSha256 is not null)
                    {
                        if (candidate.Kind != CleanupTargetKind.File)
                        {
                            result.Warnings.Add($"Skipped invalid duplicate target: {candidate.Path}");
                            continue;
                        }

                        using var stream = File.OpenRead(candidate.Path);
                        if (stream.Length != candidate.SizeBytes
                            || !Convert.ToHexString(SHA256.HashData(stream)).Equals(candidate.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                        {
                            result.Warnings.Add($"Skipped changed duplicate; scan again: {candidate.Path}");
                            continue;
                        }
                    }

                    var id = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}";
                    var entryDirectory = Path.Combine(_rootPath, id);
                    var payloadPath = Path.Combine(entryDirectory, "payload");

                    var entry = new QuarantineEntry
                    {
                        Id = id,
                        OriginalPath = candidate.Path,
                        ApprovedRoot = candidate.ApprovedRoot,
                        PayloadPath = payloadPath,
                        DisplayName = candidate.DisplayName,
                        CategoryName = candidate.CategoryName,
                        Kind = candidate.Kind,
                        SizeBytes = candidate.SizeBytes,
                        QuarantinedUtc = DateTime.UtcNow,
                        OriginalLastModifiedUtc = candidate.LastModifiedUtc
                    };

                    // Write the recovery record before moving bytes. A failed or interrupted
                    // move can leave an empty record, but never an unrecorded moved file.
                    manifest.Add(entry);
                    try
                    {
                        Directory.CreateDirectory(entryDirectory);
                        SaveEntrySidecar(entry);
                        SaveManifest(manifest);
                        MoveFile(candidate.Path, payloadPath);
                        result.Entries.Add(entry);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                    {
                        if (File.Exists(candidate.Path))
                        {
                            manifest.Remove(entry);
                            try
                            {
                                SaveManifest(manifest);
                            }
                            catch (Exception saveEx) when (saveEx is IOException or UnauthorizedAccessException)
                            {
                                result.Warnings.Add($"Recovery metadata could not be updated for {candidate.Path}: {saveEx.Message}");
                            }

                            try
                            {
                                RemoveEmptyPreMoveEntry(entry);
                            }
                            catch (Exception cleanupEx) when (cleanupEx is IOException or UnauthorizedAccessException)
                            {
                                result.Warnings.Add($"Could not remove an unused recovery record for {candidate.Path}: {cleanupEx.Message}");
                            }
                        }
                        result.Warnings.Add($"Could not quarantine {candidate.Path}: {ex.Message}");
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    result.Warnings.Add($"Could not quarantine {candidate.Path}: {ex.Message}");
                }
            }

            return result;
        }, cancellationToken);
    }

    public Task<List<string>> RestoreAsync(
        IEnumerable<string> entryIds,
        IReadOnlyDictionary<string, string>? destinationOverrides = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            using var manifestLock = AcquireManifestLock(cancellationToken);
            var warnings = new List<string>();
            var requested = entryIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var manifest = LoadManifest();

            foreach (var entry in manifest.Where(entry => requested.Contains(entry.Id)).ToList())
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var destination = destinationOverrides is not null && destinationOverrides.TryGetValue(entry.Id, out var overridePath)
                        ? overridePath
                        : entry.OriginalPath;
                    if ((destinationOverrides is null || !destinationOverrides.ContainsKey(entry.Id))
                        && string.IsNullOrWhiteSpace(entry.ApprovedRoot))
                    {
                        warnings.Add($"Legacy recovery entry for {entry.DisplayName} has no approved root. Select one item and use Restore to another location.");
                        continue;
                    }
                    if (!Path.IsPathFullyQualified(destination)
                        || destination.Equals(Path.GetPathRoot(destination), StringComparison.OrdinalIgnoreCase)
                        || PathSafety.Normalize(destination).StartsWith(PathSafety.Normalize(_rootPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                        || PathSafety.ContainsReparsePoint(destination))
                    {
                        warnings.Add($"Skipped unsafe restore destination: {destination}");
                        continue;
                    }

                    if (File.Exists(destination) || Directory.Exists(destination))
                    {
                        warnings.Add($"Skipped restore because the destination already exists: {destination}. Select one item and use Restore to another location.");
                        continue;
                    }

                    if (!File.Exists(entry.PayloadPath) && !Directory.Exists(entry.PayloadPath))
                    {
                        warnings.Add($"Quarantine payload is missing for {entry.DisplayName}; the original file may still be in place.");
                        continue;
                    }

                    var parent = Path.GetDirectoryName(destination);
                    if (!string.IsNullOrWhiteSpace(parent))
                    {
                        Directory.CreateDirectory(parent);
                    }

                    if (entry.Kind == CleanupTargetKind.File)
                    {
                        MoveFile(entry.PayloadPath, destination);
                    }
                    else
                    {
                        MoveDirectory(entry.PayloadPath, destination);
                    }

                    manifest.Remove(entry);
                    SaveManifest(manifest);
                    DeleteEntryFolder(entry);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    warnings.Add($"Could not restore {entry.DisplayName}: {ex.Message}");
                }
            }

            return warnings;
        }, cancellationToken);
    }

    public Task<List<string>> RestoreToAsync(string entryId, string destinationPath, CancellationToken cancellationToken = default)
    {
        return RestoreAsync([entryId], new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [entryId] = destinationPath
        }, cancellationToken);
    }

    public Task<List<string>> PurgeAsync(IEnumerable<string> entryIds, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            using var manifestLock = AcquireManifestLock(cancellationToken);
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
                    SaveManifest(manifest);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                    warnings.Add($"Could not purge {entry.DisplayName}: {ex.Message}");
                }
            }

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
            List<string> expired;
            using (AcquireManifestLock(cancellationToken))
            {
                var entries = LoadManifest();
                // A damaged manifest needs user review before any retention purge.
                if (RecoveryWarning is not null)
                {
                    return 0;
                }

                expired = entries
                    .Where(entry => entry.QuarantinedUtc < cutoffUtc)
                    .Select(entry => entry.Id)
                    .ToList();
            }

            if (expired.Count == 0)
            {
                return 0;
            }

            cancellationToken.ThrowIfCancellationRequested();
            PurgeAsync(expired, cancellationToken).GetAwaiter().GetResult();
            HashSet<string> remaining;
            using (AcquireManifestLock(cancellationToken))
            {
                remaining = LoadManifest().Select(entry => entry.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            return expired.Count(id => !remaining.Contains(id));
        }, cancellationToken);
    }

    private FileStream AcquireManifestLock(CancellationToken cancellationToken)
    {
        if (PathSafety.ContainsReparsePoint(_rootPath))
        {
            throw new IOException("Quarantine location passes through a reparse point; recovery was not changed.");
        }

        Directory.CreateDirectory(_rootPath);
        if (PathSafety.ContainsReparsePoint(_rootPath) || PathSafety.ContainsReparsePoint(_manifestLockPath))
        {
            throw new IOException("Quarantine lock location is unsafe; recovery was not changed.");
        }

        var deadline = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                // Keep the lock file in place. Deleting it would allow a second process
                // to lock a different file with the same name during a rename race.
                return new FileStream(_manifestLockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException ex) when (deadline.Elapsed < _manifestLockTimeout)
            {
                Thread.Sleep(50);
            }
            catch (IOException ex)
            {
                throw new IOException("Quarantine is busy in another SpacePilot process; try again in a few seconds.", ex);
            }
        }
    }

    private List<QuarantineEntry> LoadManifest()
    {
        if (PathSafety.ContainsReparsePoint(_manifestPath))
        {
            throw new InvalidDataException("Quarantine manifest passes through a reparse point; no files were changed.");
        }

        if (!File.Exists(_manifestPath))
        {
            var recovered = LoadEntrySidecars();
            RecoveryWarning = recovered.Count > 0
                ? $"Quarantine manifest is missing; {recovered.Count} payloads were recovered from per-item records. Review them before purging."
                : null;
            _manifestNeedsBackup = false;
            return recovered;
        }

        try
        {
            var entries = JsonSerializer.Deserialize<List<QuarantineEntry>>(File.ReadAllText(_manifestPath), JsonOptions)
                ?? throw new InvalidDataException("Quarantine manifest is empty or invalid.");
            if (entries.Any(entry => !IsValidEntry(entry))
                || entries.Select(entry => entry.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Count)
            {
                throw new InvalidDataException("Quarantine manifest contains an unsafe entry.");
            }

            RecoveryWarning = null;
            _manifestNeedsBackup = false;
            return entries;
        }
        catch (JsonException ex)
        {
            return RecoverDamagedManifest(ex);
        }
        catch (InvalidDataException ex)
        {
            return RecoverDamagedManifest(ex);
        }
    }

    private List<QuarantineEntry> RecoverDamagedManifest(Exception cause)
    {
        var recovered = LoadEntrySidecars();
        if (recovered.Count == 0)
        {
            throw new InvalidDataException("Quarantine manifest is corrupt and no intact per-item recovery records were found; no files were changed.", cause);
        }

        RecoveryWarning = $"Quarantine manifest is corrupt; {recovered.Count} payloads were recovered from per-item records. Review them before purging. Other payloads may need manual recovery.";
        _manifestNeedsBackup = true;
        return recovered;
    }

    private List<QuarantineEntry> LoadEntrySidecars()
    {
        if (!Directory.Exists(_rootPath))
        {
            return [];
        }

        var recovered = new List<QuarantineEntry>();
        var visited = 0;
        foreach (var directory in Directory.EnumerateDirectories(_rootPath, "*", SearchOption.TopDirectoryOnly))
        {
            if (++visited > 10_000)
            {
                throw new InvalidDataException("Too many quarantine recovery records to verify automatically.");
            }

            var id = Path.GetFileName(directory);
            if (!EntryIdPattern.IsMatch(id) || PathSafety.ContainsReparsePoint(directory))
            {
                continue;
            }

            var sidecarPath = Path.Combine(directory, "entry.json");
            if (!File.Exists(sidecarPath) || PathSafety.ContainsReparsePoint(sidecarPath))
            {
                continue;
            }

            try
            {
                var entry = JsonSerializer.Deserialize<QuarantineEntry>(File.ReadAllText(sidecarPath), JsonOptions);
                if (entry is not null && entry.Id.Equals(id, StringComparison.OrdinalIgnoreCase)
                    && entry.Kind == CleanupTargetKind.File
                    && !string.IsNullOrWhiteSpace(entry.ApprovedRoot)
                    && IsValidEntry(entry) && File.Exists(entry.PayloadPath))
                {
                    if (!recovered.Any(existing => existing.Id.Equals(entry.Id, StringComparison.OrdinalIgnoreCase)))
                    {
                        recovered.Add(entry);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                // An individual damaged record cannot grant access to a payload.
            }
        }

        return recovered;
    }

    private static void SaveEntrySidecar(QuarantineEntry entry)
    {
        var directory = Path.GetDirectoryName(entry.PayloadPath)
            ?? throw new IOException("Quarantine entry has no parent directory.");
        var sidecarPath = Path.Combine(directory, "entry.json");
        var tempPath = Path.Combine(directory, $"entry-{Guid.NewGuid():N}.tmp");
        try
        {
            WriteJsonAndFlush(tempPath, JsonSerializer.Serialize(entry, JsonOptions));
            File.Move(tempPath, sidecarPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private void SaveManifest(List<QuarantineEntry> entries)
    {
        Directory.CreateDirectory(_rootPath);
        var tempPath = Path.Combine(_rootPath, $"manifest-{Guid.NewGuid():N}.tmp");
        try
        {
            WriteJsonAndFlush(tempPath, JsonSerializer.Serialize(entries, JsonOptions));
            if (_manifestNeedsBackup && File.Exists(_manifestPath))
            {
                File.Move(_manifestPath, Path.Combine(_rootPath, $"manifest-corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.json"));
                _manifestNeedsBackup = false;
            }
            File.Move(tempPath, _manifestPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static void WriteJsonAndFlush(string path, string json)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false), 1024, leaveOpen: true))
        {
            writer.Write(json);
            writer.Flush();
        }

        stream.Flush(flushToDisk: true);
    }

    private void RemoveEmptyPreMoveEntry(QuarantineEntry entry)
    {
        var entryFolder = Path.Combine(_rootPath, entry.Id);
        if (!IsValidEntry(entry) || File.Exists(entry.PayloadPath) || Directory.Exists(entry.PayloadPath)
            || !Directory.Exists(entryFolder))
        {
            return;
        }

        var sidecar = Path.Combine(entryFolder, "entry.json");
        if (File.Exists(sidecar))
        {
            File.Delete(sidecar);
        }

        Directory.Delete(entryFolder, recursive: false);
    }

    private static void MoveFile(string sourcePath, string destinationPath)
    {
        ClearReadOnly(sourcePath);
        // Do not fall back to copy-then-delete across volumes: the source pathname
        // could be replaced before deletion, removing a different personal file.
        File.Move(sourcePath, destinationPath);
    }

    private static void MoveDirectory(string sourcePath, string destinationPath)
    {
        ClearReadOnlyRecursive(sourcePath);
        Directory.Move(sourcePath, destinationPath);
        // A cross-volume copy cannot safely prove a live directory stayed unchanged
        // while it was copied. Leave the source intact instead of deleting a partial copy.
    }

    private void DeleteEntryFolder(QuarantineEntry entry)
    {
        if (!IsValidEntry(entry))
        {
            throw new IOException($"Unsafe quarantine entry: {entry.Id}");
        }

        var entryFolder = Path.Combine(_rootPath, entry.Id);
        if (Directory.Exists(entryFolder))
        {
            if (PathSafety.ContainsReparsePoint(entryFolder))
            {
                throw new IOException($"Unsafe quarantine entry folder: {entry.Id}");
            }

            var payload = Path.Combine(entryFolder, "payload");
            if (Directory.Exists(payload))
            {
                throw new IOException($"Directory quarantine entries require manual review before purge: {entry.Id}");
            }
            if (File.Exists(payload))
            {
                File.Delete(payload);
            }
            var sidecar = Path.Combine(entryFolder, "entry.json");
            if (File.Exists(sidecar))
            {
                File.Delete(sidecar);
            }
            Directory.Delete(entryFolder, recursive: false);
        }
    }

    private bool IsValidEntry(QuarantineEntry? entry)
    {
        if (entry is null || !EntryIdPattern.IsMatch(entry.Id ?? string.Empty)
            || !Path.IsPathFullyQualified(entry.OriginalPath ?? string.Empty)
            || (entry.Kind != CleanupTargetKind.File && entry.Kind != CleanupTargetKind.Directory))
        {
            return false;
        }

        var entryFolder = Path.Combine(_rootPath, entry.Id);
        var expectedPayload = Path.Combine(entryFolder, "payload");
        try
        {
            if (!string.IsNullOrWhiteSpace(entry.ApprovedRoot)
                && !PathSafety.IsAllowedCleanupTarget(entry.OriginalPath, entry.ApprovedRoot))
            {
                return false;
            }

            if (!Path.GetFullPath(entry.PayloadPath).Equals(Path.GetFullPath(expectedPayload), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            foreach (var path in new[] { _rootPath, entryFolder, expectedPayload })
            {
                if (PathSafety.ContainsReparsePoint(path))
                {
                    return false;
                }
            }
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return false;
        }

        return true;
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
