using System.IO;
using System.Security.Cryptography;
using SpacePilot.Models;

namespace SpacePilot.Services;

public sealed class DuplicateReviewService
{
    public Task<IReadOnlyList<string>> ValidateAsync(
        IReadOnlyList<DuplicateFileInfo> scannedFiles,
        IReadOnlyCollection<string> selectedPaths,
        IReadOnlyCollection<string> keeperPaths,
        CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<string>>(() =>
        {
            var errors = new List<string>();
            var selected = selectedPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var keepers = keeperPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var affectedGroups = scannedFiles
                .Where(file => selected.Contains(file.Path))
                .Select(file => file.GroupId)
                .ToHashSet(StringComparer.Ordinal);

            if (affectedGroups.Count == 0)
            {
                errors.Add("Select at least one extra copy to quarantine.");
            }

            foreach (var group in scannedFiles.Where(file => affectedGroups.Contains(file.GroupId)).GroupBy(file => file.GroupId))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var members = group.ToList();
                var keeperCount = members.Count(file => keepers.Contains(file.Path));
                if (keeperCount != 1 || members.Any(file => keepers.Contains(file.Path) && selected.Contains(file.Path)))
                {
                    errors.Add($"{group.Key}: choose exactly one unselected keeper before cleanup.");
                    continue;
                }

                foreach (var file in members)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var info = new FileInfo(file.Path);
                        if (!info.Exists || (info.Attributes & FileAttributes.ReparsePoint) != 0 || info.Length != file.SizeBytes)
                        {
                            errors.Add($"{group.Key}: {file.Name} changed since the scan. Scan again.");
                            break;
                        }

                        using var stream = info.OpenRead();
                        var hash = Convert.ToHexString(SHA256.HashData(stream));
                        if (!hash.Equals(file.Hash, StringComparison.OrdinalIgnoreCase))
                        {
                            errors.Add($"{group.Key}: {file.Name} changed since the scan. Scan again.");
                            break;
                        }
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                    {
                        errors.Add($"{group.Key}: could not verify {file.Name}: {ex.Message}");
                        break;
                    }
                }
            }

            return errors;
        }, cancellationToken);
    }
}
