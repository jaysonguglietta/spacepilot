using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using SpacePilot.Models;

namespace SpacePilot.Services;

public sealed class StartupService
{
    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public Task<IReadOnlyList<StartupEntry>> GetStartupEntriesAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<StartupEntry>>(() =>
        {
            if (!OperatingSystem.IsWindows())
            {
                return Array.Empty<StartupEntry>();
            }

            var entries = new List<StartupEntry>();
            ReadRegistryRunKey(RegistryHive.CurrentUser, RegistryView.Registry64, "Current user", entries, cancellationToken);
            ReadRegistryRunKey(RegistryHive.CurrentUser, RegistryView.Registry32, "Current user", entries, cancellationToken);
            ReadRegistryRunKey(RegistryHive.LocalMachine, RegistryView.Registry64, "All users", entries, cancellationToken);
            ReadRegistryRunKey(RegistryHive.LocalMachine, RegistryView.Registry32, "All users", entries, cancellationToken);
            ReadStartupFolder(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Current user", entries, cancellationToken);
            ReadStartupFolder(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "All users", entries, cancellationToken);
            ReadScheduledTasks(entries, cancellationToken);

            return entries
                .GroupBy(entry => $"{entry.Name}|{entry.Command}|{entry.Location}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }, cancellationToken);
    }

    private static void ReadRegistryRunKey(
        RegistryHive hive,
        RegistryView view,
        string scope,
        List<StartupEntry> entries,
        CancellationToken cancellationToken)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var runKey = baseKey.OpenSubKey(RunKeyPath);
            if (runKey is null)
            {
                return;
            }

            foreach (var name in runKey.GetValueNames())
            {
                cancellationToken.ThrowIfCancellationRequested();
                entries.Add(new StartupEntry(
                    string.IsNullOrWhiteSpace(name) ? "(Default)" : name,
                    scope,
                    "Registry Run key",
                    runKey.GetValue(name)?.ToString() ?? string.Empty,
                    $@"{hive}\{RunKeyPath}",
                    true,
                    scope == "All users" ? "Medium" : "Low",
                    "Review if you do not need this app immediately after sign-in."));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Startup inventory is best-effort.
        }
    }

    private static void ReadStartupFolder(string folder, string scope, List<StartupEntry> entries, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return;
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(folder))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var info = new FileInfo(file);
                entries.Add(new StartupEntry(
                    Path.GetFileNameWithoutExtension(info.Name),
                    scope,
                    "Startup folder",
                    info.FullName,
                    folder,
                    true,
                    "Low",
                    "Review shortcuts for apps you rarely use at sign-in."));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Startup folder can be locked down by policy.
        }
    }

    private static void ReadScheduledTasks(List<StartupEntry> entries, CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo("schtasks.exe", "/Query /FO CSV /V")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return;
            }

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(10000);
            if (string.IsNullOrWhiteSpace(output))
            {
                return;
            }

            var rows = ParseCsv(output);
            if (rows.Count < 2)
            {
                return;
            }

            var headers = rows[0];
            var taskNameIndex = headers.FindIndex(header => header.Equals("TaskName", StringComparison.OrdinalIgnoreCase));
            var statusIndex = headers.FindIndex(header => header.Equals("Status", StringComparison.OrdinalIgnoreCase));
            var taskToRunIndex = headers.FindIndex(header => header.Equals("Task To Run", StringComparison.OrdinalIgnoreCase));
            var scheduleTypeIndex = headers.FindIndex(header => header.Equals("Schedule Type", StringComparison.OrdinalIgnoreCase));

            foreach (var row in rows.Skip(1))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var taskName = GetCsvValue(row, taskNameIndex);
                var taskToRun = GetCsvValue(row, taskToRunIndex);
                var scheduleType = GetCsvValue(row, scheduleTypeIndex);
                var status = GetCsvValue(row, statusIndex);

                if (string.IsNullOrWhiteSpace(taskName)
                    || string.IsNullOrWhiteSpace(taskToRun)
                    || taskName.StartsWith(@"\Microsoft\Windows\", StringComparison.OrdinalIgnoreCase)
                    || status.Equals("Disabled", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!scheduleType.Contains("At logon", StringComparison.OrdinalIgnoreCase)
                    && !scheduleType.Contains("At startup", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                entries.Add(new StartupEntry(
                    taskName.TrimStart('\\'),
                    "Task Scheduler",
                    "Scheduled task",
                    taskToRun,
                    taskName,
                    true,
                    "Medium",
                    "Scheduled tasks can delay sign-in; review unfamiliar entries in Task Scheduler."));
            }
        }
        catch
        {
            // Scheduled task inventory is best-effort and may be blocked by policy.
        }
    }

    private static List<List<string>> ParseCsv(string csv)
    {
        var rows = new List<List<string>>();
        using var reader = new StringReader(csv);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            rows.Add(ParseCsvLine(line));
        }

        return rows;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var values = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (character == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        values.Add(current.ToString());
        return values;
    }

    private static string GetCsvValue(List<string> row, int index)
    {
        return index >= 0 && index < row.Count ? row[index].Trim() : string.Empty;
    }
}
