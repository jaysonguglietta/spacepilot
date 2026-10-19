using Microsoft.Win32;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using SpacePilot.Models;

namespace SpacePilot.Services;

public sealed class StartupService
{
    private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

    public string? ScheduledTaskWarning { get; private set; }
    public string? InventoryWarning { get; private set; }

    public Task<IReadOnlyList<StartupEntry>> GetStartupEntriesAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<StartupEntry>>(() =>
        {
            if (!OperatingSystem.IsWindows())
            {
                ScheduledTaskWarning = null;
                InventoryWarning = null;
                return Array.Empty<StartupEntry>();
            }

            var entries = new List<StartupEntry>();
            var warnings = new List<string>();
            ReadRegistryRunKey(RegistryHive.CurrentUser, RegistryView.Registry64, "Current user", entries, warnings, cancellationToken);
            ReadRegistryRunKey(RegistryHive.CurrentUser, RegistryView.Registry32, "Current user", entries, warnings, cancellationToken);
            ReadRegistryRunKey(RegistryHive.LocalMachine, RegistryView.Registry64, "All users", entries, warnings, cancellationToken);
            ReadRegistryRunKey(RegistryHive.LocalMachine, RegistryView.Registry32, "All users", entries, warnings, cancellationToken);
            ReadStartupFolder(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Current user", entries, warnings, cancellationToken);
            ReadStartupFolder(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "All users", entries, warnings, cancellationToken);
            ScheduledTaskWarning = ReadScheduledTasks(entries, cancellationToken);
            if (ScheduledTaskWarning is not null)
            {
                warnings.Add(ScheduledTaskWarning);
            }
            InventoryWarning = warnings.Count == 0 ? null : string.Join(" ", warnings.Take(6));

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
        List<string> warnings,
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
            warnings.Add($"{scope} registry startup entries were omitted ({view}): {ex.Message}");
        }
    }

    private static void ReadStartupFolder(string folder, string scope, List<StartupEntry> entries, List<string> warnings, CancellationToken cancellationToken)
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
            warnings.Add($"{scope} startup folder entries were omitted: {ex.Message}");
        }
    }

    private static string? ReadScheduledTasks(List<StartupEntry> entries, CancellationToken cancellationToken)
    {
        try
        {
            // XML element names are invariant across Windows UI languages. /XML ONE
            // returns one document rather than concatenated task documents.
            var result = ExternalProcessRunner.RunAsync(
                ExternalProcessRunner.GetSystemToolPath("schtasks.exe"),
                ["/Query", "/XML", "ONE"],
                TimeSpan.FromSeconds(30),
                cancellationToken,
                outputLimit: 16 * 1024 * 1024).GetAwaiter().GetResult();
            if (!result.Succeeded)
            {
                return $"Scheduled startup tasks were omitted: {result.FailureMessage("schtasks.exe")}";
            }

            entries.AddRange(ParseScheduledTaskXml(result.Output, cancellationToken));
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception
                                   or InvalidOperationException or TimeoutException or XmlException or ObjectDisposedException)
        {
            return $"Scheduled startup tasks were omitted: {ex.Message}";
        }
    }

    internal static IReadOnlyList<StartupEntry> ParseScheduledTaskXml(string xml, CancellationToken cancellationToken = default)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 16 * 1024 * 1024
        };
        using var textReader = new StringReader(xml);
        using var reader = XmlReader.Create(textReader, settings);
        var document = XDocument.Load(reader);
        IEnumerable<XElement> tasks = document.Root is { } root && root.Name.LocalName == "Task"
            ? new[] { root }
            : document.Descendants().Where(element => element.Name.LocalName == "Task");
        var entries = new List<StartupEntry>();
        foreach (var task in tasks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var taskName = task.Descendants().FirstOrDefault(element => element.Name.LocalName == "URI")?.Value
                ?? task.Attribute("name")?.Value
                ?? string.Empty;
            if (string.IsNullOrWhiteSpace(taskName)
                || taskName.StartsWith(@"\Microsoft\Windows\", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var taskEnabled = task.Descendants().FirstOrDefault(element => element.Name.LocalName == "Settings")?.Elements()
                .FirstOrDefault(element => element.Name.LocalName == "Enabled")?.Value;
            if (string.Equals(taskEnabled, "false", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var startsAtSignIn = task.Descendants().Any(element =>
                (element.Name.LocalName == "LogonTrigger" || element.Name.LocalName == "BootTrigger")
                && !string.Equals(
                    element.Elements().FirstOrDefault(child => child.Name.LocalName == "Enabled")?.Value,
                    "false", StringComparison.OrdinalIgnoreCase));
            if (!startsAtSignIn)
            {
                continue;
            }

            var action = task.Descendants().FirstOrDefault(element => element.Name.LocalName == "Exec");
            var command = action?.Elements().FirstOrDefault(element => element.Name.LocalName == "Command")?.Value;
            if (string.IsNullOrWhiteSpace(command))
            {
                continue;
            }

            var arguments = action?.Elements().FirstOrDefault(element => element.Name.LocalName == "Arguments")?.Value;
            var taskToRun = string.IsNullOrWhiteSpace(arguments) ? command : $"{command} {arguments}";
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

        return entries;
    }
}
