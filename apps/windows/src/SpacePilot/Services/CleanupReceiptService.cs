using System.IO;
using System.Text.Json;
using SpacePilot.Models;

namespace SpacePilot.Services;

public sealed class CleanupReceiptService
{
    private const int MaxReceiptFilesToInspect = 5000;
    private const long MaxReceiptBytes = 2L * 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _receiptRoot;
    public string? LastReadWarning { get; private set; }

    public CleanupReceiptService(string? appDataRoot = null)
    {
        var appData = appDataRoot ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            appData = AppContext.BaseDirectory;
        }

        _receiptRoot = Path.Combine(appData, "SpacePilot", "Receipts");
    }

    public Task SaveAsync(CleanupReceipt receipt)
    {
        return Task.Run(() =>
        {
            Directory.CreateDirectory(_receiptRoot);
            var path = GetReceiptPath(receipt.Id);
            File.WriteAllText(path, JsonSerializer.Serialize(receipt, JsonOptions));
        });
    }

    public Task<IReadOnlyList<CleanupReceipt>> GetRecentAsync(int limit = 25)
    {
        return Task.Run<IReadOnlyList<CleanupReceipt>>(() =>
        {
            if (!Directory.Exists(_receiptRoot))
            {
                LastReadWarning = null;
                return Array.Empty<CleanupReceipt>();
            }

            var files = EnumerateCandidateFiles(out var truncated);
            var skipped = 0;
            var receipts = new List<CleanupReceipt>();
            foreach (var file in files.OrderByDescending(item => item.LastModifiedUtc))
            {
                if (receipts.Count >= Math.Clamp(limit, 0, 100))
                {
                    break;
                }

                var receipt = ReadReceipt(file.Path);
                if (receipt is null)
                {
                    skipped++;
                }
                else
                {
                    receipts.Add(receipt);
                }
            }

            LastReadWarning = truncated || skipped > 0
                ? $"Receipt history is incomplete: {(truncated ? $"only {MaxReceiptFilesToInspect:N0} receipt filenames were inspected; " : string.Empty)}{skipped:N0} oversized, corrupt, or unreadable receipts were skipped."
                : null;
            return receipts.OrderByDescending(receipt => receipt.TimestampUtc).ToList();
        });
    }

    public Task<string?> ExportLatestAsync()
    {
        return Task.Run(() =>
        {
            if (!Directory.Exists(_receiptRoot))
            {
                return null;
            }

            var files = EnumerateCandidateFiles(out var truncated);
            var latest = files.OrderByDescending(item => item.LastModifiedUtc).FirstOrDefault().Path;
            LastReadWarning = truncated
                ? $"Only {MaxReceiptFilesToInspect:N0} receipt filenames were inspected; the latest receipt could not be verified across the entire folder."
                : null;

            if (latest is null)
            {
                return null;
            }

            var exportPath = Path.Combine(_receiptRoot, "exports", Path.GetFileName(latest));
            var exportDirectory = Path.GetDirectoryName(exportPath)!;
            if (PathSafety.ContainsReparsePoint(latest) || PathSafety.ContainsReparsePoint(exportDirectory))
            {
                throw new IOException("Receipt export path passes through a reparse point.");
            }
            Directory.CreateDirectory(exportDirectory);
            if (PathSafety.ContainsReparsePoint(exportDirectory))
            {
                throw new IOException("Receipt export directory changed to a reparse point.");
            }

            var tempPath = Path.Combine(exportDirectory, $"export-{Guid.NewGuid():N}.tmp");
            try
            {
                using var source = new FileStream(latest, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (source.Length > MaxReceiptBytes)
                {
                    throw new InvalidDataException("Latest receipt is too large to export safely. Review the Receipts folder manually.");
                }

                using (var destination = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var buffer = new byte[64 * 1024];
                    long copied = 0;
                    int count;
                    while ((count = source.Read(buffer)) > 0)
                    {
                        copied += count;
                        if (copied > MaxReceiptBytes)
                        {
                            throw new InvalidDataException("Latest receipt grew beyond the safe export size limit.");
                        }
                        destination.Write(buffer, 0, count);
                    }
                    destination.Flush(flushToDisk: true);
                }
                File.Move(tempPath, exportPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
            return exportPath;
        });
    }

    private string GetReceiptPath(string receiptId)
    {
        return Path.Combine(_receiptRoot, $"{receiptId}.json");
    }

    private static CleanupReceipt? ReadReceipt(string path)
    {
        try
        {
            if (PathSafety.ContainsReparsePoint(path))
            {
                return null;
            }
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaxReceiptBytes)
            {
                return null;
            }

            var data = new byte[(int)stream.Length];
            stream.ReadExactly(data);
            return JsonSerializer.Deserialize<CleanupReceipt>(data, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private List<(string Path, DateTime LastModifiedUtc)> EnumerateCandidateFiles(out bool truncated)
    {
        var files = new List<(string Path, DateTime LastModifiedUtc)>();
        truncated = false;
        foreach (var path in Directory.EnumerateFiles(_receiptRoot, "*.json", SearchOption.TopDirectoryOnly))
        {
            if (files.Count >= MaxReceiptFilesToInspect)
            {
                truncated = true;
                break;
            }

            files.Add((path, File.GetLastWriteTimeUtc(path)));
        }

        return files;
    }
}
