using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using SpacePilot.Models;

namespace SpacePilot.Services;

public sealed class WingetService
{
    public async Task<IReadOnlyList<WingetPackageInfo>> GetUpgradesAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Array.Empty<WingetPackageInfo>();
        }

        // WinGet upgrade does not support --output json. Its table is a display format,
        // so parsing must fail visibly if the installed client changes that format.
        var result = await ExternalProcessRunner.RunAsync(
            GetWingetExecutablePath(),
            ["upgrade", "--accept-source-agreements", "--disable-interactivity"],
            TimeSpan.FromMinutes(2),
            cancellationToken).ConfigureAwait(false);
        var outputLines = result.Output.Replace("\r\n", "\n").Split('\n').ToList();
        if (!result.Succeeded)
        {
            if (!result.OutputTruncated && IsNoUpgradesMessage(result.Output, result.Error))
            {
                return Array.Empty<WingetPackageInfo>();
            }

            throw new InvalidOperationException(result.FailureMessage("WinGet update check"));
        }

        try
        {
            return ParseWingetTable(outputLines);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("WinGet returned an unrecognized update list. Run 'winget upgrade' directly to inspect it. " + ex.Message, ex);
        }
    }

    private static bool IsNoUpgradesMessage(string output, string error)
    {
        var combined = $"{output}\n{error}";
        var lines = combined.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return lines.Length == 1 && (lines[0].Equals("No applicable upgrade found.", StringComparison.OrdinalIgnoreCase)
                                     || lines[0].Equals("No applicable update found.", StringComparison.OrdinalIgnoreCase)
                                     || lines[0].Equals("No upgrades available.", StringComparison.OrdinalIgnoreCase));
    }

    public async Task<WingetOperationResult> UpgradeAsync(IEnumerable<string> packageIds, CancellationToken cancellationToken = default)
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
            if (id.StartsWith("-", StringComparison.Ordinal) || id.Any(char.IsControl))
            {
                final.Succeeded = false;
                final.Message = $"Invalid WinGet package ID: {id}";
                continue;
            }

            var result = await RunWingetAsync(
                ["upgrade", "--id", id, "--exact", "--accept-package-agreements", "--accept-source-agreements", "--disable-interactivity"],
                TimeSpan.FromMinutes(30),
                cancellationToken).ConfigureAwait(false);
            final.OutputLines.AddRange(result.OutputLines);
            if (!result.Succeeded)
            {
                final.Succeeded = false;
                final.Message = $"One or more package updates failed. Last failure: {result.Message}";
            }
        }

        return final;
    }

    public async Task<WingetOperationResult> ExportAsync(string exportPath, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(exportPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return await RunWingetAsync(
            ["export", "--output", exportPath, "--accept-source-agreements", "--disable-interactivity"],
            TimeSpan.FromMinutes(5),
            cancellationToken).ConfigureAwait(false);
    }

    public Task<WingetOperationResult> ImportAsync(string importPath, CancellationToken cancellationToken = default)
    {
        return RunWingetAsync(
            ["import", "--import-file", importPath, "--accept-package-agreements", "--accept-source-agreements", "--disable-interactivity"],
            TimeSpan.FromMinutes(30),
            cancellationToken);
    }

    private static async Task<WingetOperationResult> RunWingetAsync(
        IEnumerable<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var result = new WingetOperationResult();
        try
        {
            var processResult = await ExternalProcessRunner.RunAsync(
                GetWingetExecutablePath(), arguments, timeout, cancellationToken).ConfigureAwait(false);
            result.OutputLines.AddRange(processResult.Output.Replace("\r\n", "\n").Split('\n'));
            result.Succeeded = processResult.Succeeded;
            result.Message = result.Succeeded
                ? "WinGet operation completed."
                : processResult.FailureMessage("WinGet");
            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException
                                   or System.ComponentModel.Win32Exception or TimeoutException)
        {
            result.Message = $"WinGet is unavailable or failed to run: {ex.Message}";
            return result;
        }
    }

    private static string GetWingetExecutablePath()
    {
        using var identity = WindowsIdentity.GetCurrent();
        if (new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
        {
            throw new InvalidOperationException("WinGet operations are disabled while SpacePilot is elevated. Relaunch it as a standard user.");
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var aliasPath = Path.Combine(localAppData, "Microsoft", "WindowsApps", "winget.exe");
        return File.Exists(aliasPath)
            ? aliasPath
            : throw new FileNotFoundException("The WinGet app execution alias was not found. Install or enable App Installer, then try again.");
    }

    internal static IReadOnlyList<WingetPackageInfo> ParseWingetTable(List<string> lines)
    {
        var cleaned = lines
            .Select(line => Regex.Replace(line, "\\u001b\\[[0-9;]*[A-Za-z]", string.Empty).Replace("\r", string.Empty).TrimEnd())
            .ToList();
        var separatorIndex = cleaned.FindIndex(line => Regex.IsMatch(line.Trim(), "^-{2,}(?:\\s+-{2,})*$"));
        if (separatorIndex < 1)
        {
            if (IsNoUpgradesMessage(string.Join("\n", cleaned), string.Empty))
            {
                return Array.Empty<WingetPackageInfo>();
            }

            throw new FormatException("No update table or recognized empty-result message was present.");
        }

        // Prefer the dash runs that WinGet prints under each column. Some client
        // versions print one continuous rule, in which case use header spacing.
        // Neither strategy depends on localized header words.
        var dashStarts = Regex.Matches(cleaned[separatorIndex], "-{2,}")
            .Cast<Match>()
            .Select(match => match.Index)
            .ToList();
        var starts = dashStarts.Count >= 4
            ? dashStarts
            : FindColumnStarts(cleaned[separatorIndex - 1]);
        if (starts.Count < 4)
        {
            throw new FormatException("The update table columns could not be identified.");
        }

        var packages = new List<WingetPackageInfo>();
        foreach (var line in cleaned.Skip(separatorIndex + 1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                break;
            }

            var name = ReadColumn(line, starts, 0);
            var id = ReadColumn(line, starts, 1);
            var version = ReadColumn(line, starts, 2);
            var available = ReadColumn(line, starts, 3);
            var source = starts.Count >= 5 ? ReadColumn(line, starts, 4) : string.Empty;
            if (string.IsNullOrWhiteSpace(id))
            {
                if (Regex.IsMatch(line.TrimStart(), "^[0-9]+\\s"))
                {
                    break; // WinGet's summary count follows the data rows.
                }

                throw new FormatException($"A WinGet row had no package ID: {line}");
            }

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(version)
                || string.IsNullOrWhiteSpace(available) || id.Any(char.IsWhiteSpace)
                || id.Contains('…') || id.Contains("...", StringComparison.Ordinal))
            {
                throw new FormatException($"A WinGet row was incomplete or truncated: {line}");
            }

            packages.Add(new WingetPackageInfo(
                name,
                id,
                version,
                available,
                source,
                true));
        }

        return packages;
    }

    private static List<int> FindColumnStarts(string header)
    {
        var starts = new List<int>();
        var whitespace = 0;
        for (var index = 0; index < header.Length; index++)
        {
            if (char.IsWhiteSpace(header[index]))
            {
                whitespace++;
            }
            else
            {
                if (starts.Count == 0 || whitespace >= 2)
                {
                    starts.Add(index);
                }
                whitespace = 0;
            }
        }

        return starts;
    }

    private static string ReadColumn(string line, List<int> starts, int column)
    {
        if (column >= starts.Count || starts[column] >= line.Length)
        {
            return string.Empty;
        }

        var end = column + 1 < starts.Count ? Math.Min(starts[column + 1], line.Length) : line.Length;
        return line[starts[column]..end].Trim();
    }
}

internal sealed record ExternalProcessResult(int ExitCode, string Output, string Error, bool OutputTruncated)
{
    public bool Succeeded => ExitCode == 0 && !OutputTruncated;

    public string FailureMessage(string toolName)
    {
        if (OutputTruncated)
        {
            return $"{toolName} produced more output than SpacePilot can safely display.";
        }

        var detail = string.IsNullOrWhiteSpace(Error) ? Output.Trim() : Error.Trim();
        if (detail.Length > 512)
        {
            detail = detail[..512] + "…";
        }
        return string.IsNullOrWhiteSpace(detail)
            ? $"{toolName} exited with code {ExitCode}."
            : $"{toolName} failed (exit code {ExitCode}): {detail}";
    }
}

internal static class ExternalProcessRunner
{
    private const int DefaultOutputLimit = 256 * 1024;

    public static string GetSystemToolPath(string fileName)
    {
        var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
        if (string.IsNullOrWhiteSpace(systemDirectory))
        {
            throw new FileNotFoundException("Windows System32 could not be located.");
        }

        var path = Path.Combine(systemDirectory, fileName);
        return File.Exists(path) ? path : throw new FileNotFoundException($"Windows helper was not found: {path}");
    }

    public static async Task<ExternalProcessResult> RunAsync(
        string executablePath,
        IEnumerable<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default,
        int outputLimit = DefaultOutputLimit)
    {
        if (!Path.IsPathFullyQualified(executablePath) || !File.Exists(executablePath))
        {
            throw new FileNotFoundException($"Helper executable was not found at its expected absolute path: {executablePath}");
        }

        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException($"Could not start {Path.GetFileName(executablePath)}.");
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        var standardOutput = ReadBoundedAsync(process.StandardOutput, outputLimit, deadline.Token);
        var standardError = ReadBoundedAsync(process.StandardError, outputLimit, deadline.Token);
        try
        {
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            var streams = await Task.WhenAll(standardOutput, standardError).ConfigureAwait(false);
            return new ExternalProcessResult(
                process.ExitCode,
                streams[0].Text,
                streams[1].Text,
                streams[0].Truncated || streams[1].Truncated);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // The helper may have exited while cancellation was processed.
            }

            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException($"{Path.GetFileName(executablePath)} exceeded its {timeout.TotalSeconds:0}-second deadline.");
        }
        catch
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // The helper may have exited while its streams were being read.
            }

            throw;
        }
    }

    private static async Task<(string Text, bool Truncated)> ReadBoundedAsync(
        StreamReader reader,
        int limit,
        CancellationToken cancellationToken)
    {
        var text = new StringBuilder(Math.Min(limit, 4096));
        var buffer = new char[4096];
        var truncated = false;
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            var remaining = Math.Max(0, limit - text.Length);
            if (remaining > 0)
            {
                text.Append(buffer, 0, Math.Min(count, remaining));
            }
            truncated |= count > remaining;
        }

        return (text.ToString(), truncated);
    }
}
