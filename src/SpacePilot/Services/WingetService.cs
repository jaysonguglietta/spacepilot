using System.Diagnostics;
using System.IO;
using System.Text.Json;
using SpacePilot.Models;

namespace SpacePilot.Services;

public sealed class WingetService
{
    public Task<IReadOnlyList<WingetPackageInfo>> GetUpgradesAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<WingetPackageInfo>>(() =>
        {
            if (!OperatingSystem.IsWindows())
            {
                return Array.Empty<WingetPackageInfo>();
            }

            var result = RunWinget("upgrade --accept-source-agreements --output json", cancellationToken);
            if (!result.Succeeded)
            {
                return Array.Empty<WingetPackageInfo>();
            }

            return ParseWingetPackages(result.OutputLines);
        }, cancellationToken);
    }

    public Task<WingetOperationResult> UpgradeAsync(IEnumerable<string> packageIds, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var ids = packageIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (ids.Count == 0)
            {
                return new WingetOperationResult { Succeeded = false, Message = "No packages were selected." };
            }

            var final = new WingetOperationResult { Succeeded = true, Message = "Selected package updates completed." };
            foreach (var id in ids)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = RunWinget($"upgrade --id \"{id}\" --exact --accept-package-agreements --accept-source-agreements", cancellationToken);
                final.OutputLines.AddRange(result.OutputLines);
                if (!result.Succeeded)
                {
                    final.Succeeded = false;
                    final.Message = $"One or more package updates failed. Last failure: {result.Message}";
                }
            }

            return final;
        }, cancellationToken);
    }

    public Task<WingetOperationResult> ExportAsync(string exportPath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            var directory = Path.GetDirectoryName(exportPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            return RunWinget($"export --output \"{exportPath}\" --accept-source-agreements", cancellationToken);
        }, cancellationToken);
    }

    public Task<WingetOperationResult> ImportAsync(string importPath, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => RunWinget($"import --import-file \"{importPath}\" --accept-package-agreements --accept-source-agreements", cancellationToken), cancellationToken);
    }

    private static WingetOperationResult RunWinget(string arguments, CancellationToken cancellationToken)
    {
        var result = new WingetOperationResult();
        try
        {
            var startInfo = new ProcessStartInfo("winget.exe", arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                result.Message = "Could not start winget.exe.";
                return result;
            }

            while (!process.StandardOutput.EndOfStream)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = process.StandardOutput.ReadLine();
                if (line is not null)
                {
                    result.OutputLines.Add(line);
                }
            }

            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            result.Succeeded = process.ExitCode == 0;
            result.Message = result.Succeeded ? "WinGet operation completed." : string.IsNullOrWhiteSpace(error) ? "WinGet operation failed." : error.Trim();
            return result;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            result.Message = $"WinGet is unavailable or failed to run: {ex.Message}";
            return result;
        }
    }

    internal static IReadOnlyList<WingetPackageInfo> ParseWingetPackages(List<string> outputLines)
    {
        var json = string.Join(Environment.NewLine, outputLines).Trim();
        if (string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<WingetPackageInfo>();
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var packageArray = FindPackageArray(root);
            if (packageArray.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<WingetPackageInfo>();
            }

            var packages = new List<WingetPackageInfo>();
            foreach (var package in packageArray.EnumerateArray())
            {
                packages.Add(new WingetPackageInfo(
                    ReadString(package, "Name", "PackageName"),
                    ReadString(package, "Id", "PackageIdentifier"),
                    ReadString(package, "Version", "InstalledVersion"),
                    ReadString(package, "Available", "AvailableVersion"),
                    ReadString(package, "Source"),
                    true));
            }

            return packages
                .Where(package => !string.IsNullOrWhiteSpace(package.Name) || !string.IsNullOrWhiteSpace(package.Id))
                .OrderBy(package => package.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch
        {
            return ParseWingetTable(outputLines);
        }
    }

    private static JsonElement FindPackageArray(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            if (LooksLikePackageArray(root))
            {
                return root;
            }

            foreach (var item in root.EnumerateArray())
            {
                var nested = FindPackageArray(item);
                if (nested.ValueKind == JsonValueKind.Array)
                {
                    return nested;
                }
            }

            return default;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return default;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Array && LooksLikePackageArray(property.Value))
            {
                return property.Value;
            }
        }

        foreach (var property in root.EnumerateObject())
        {
            var nested = FindPackageArray(property.Value);
            if (nested.ValueKind == JsonValueKind.Array)
            {
                return nested;
            }
        }

        return default;
    }

    private static bool LooksLikePackageArray(JsonElement array)
    {
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (item.TryGetProperty("Name", out _)
                || item.TryGetProperty("PackageName", out _)
                || item.TryGetProperty("Id", out _)
                || item.TryGetProperty("PackageIdentifier", out _))
            {
                return true;
            }
        }

        return false;
    }

    private static string ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String)
            {
                return property.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    internal static IReadOnlyList<WingetPackageInfo> ParseWingetTable(List<string> lines)
    {
        var packages = new List<WingetPackageInfo>();
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)
                || line.StartsWith("Name ", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("---", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4)
            {
                continue;
            }

            packages.Add(new WingetPackageInfo(
                parts[0],
                parts.Length >= 5 ? parts[1] : string.Empty,
                parts.Length >= 5 ? parts[^3] : parts[^2],
                parts.Length >= 5 ? parts[^2] : parts[^1],
                parts[^1],
                true));
        }

        return packages;
    }
}
