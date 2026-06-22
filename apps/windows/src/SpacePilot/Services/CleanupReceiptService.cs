using System.IO;
using System.Text.Json;
using SpacePilot.Models;

namespace SpacePilot.Services;

public sealed class CleanupReceiptService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _receiptRoot;

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
                return Array.Empty<CleanupReceipt>();
            }

            return Directory.EnumerateFiles(_receiptRoot, "*.json", SearchOption.TopDirectoryOnly)
                .Select(ReadReceipt)
                .Where(receipt => receipt is not null)
                .Cast<CleanupReceipt>()
                .OrderByDescending(receipt => receipt.TimestampUtc)
                .Take(limit)
                .ToList();
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

            var latest = Directory.EnumerateFiles(_receiptRoot, "*.json", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();

            if (latest is null)
            {
                return null;
            }

            var exportPath = Path.Combine(_receiptRoot, "exports", Path.GetFileName(latest));
            Directory.CreateDirectory(Path.GetDirectoryName(exportPath)!);
            File.Copy(latest, exportPath, overwrite: true);
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
            return JsonSerializer.Deserialize<CleanupReceipt>(File.ReadAllText(path), JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
