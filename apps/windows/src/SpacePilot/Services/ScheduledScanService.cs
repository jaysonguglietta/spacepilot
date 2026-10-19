using System.Xml;
using System.Xml.Linq;
using SpacePilot.Models;

namespace SpacePilot.Services;

public sealed class ScheduledScanService
{
    private const string TaskName = "SpacePilot Weekly Scan Reminder";

    public async Task<ScheduledScanStatus> GetStatusAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new ScheduledScanStatus(false, "Scheduled scan reminders are available on Windows.");
        }

        var result = await RunSchtasksAsync("/Query", "/TN", TaskName, "/XML");
        if (result.ExitCode != 0)
        {
            return new ScheduledScanStatus(false, $"Weekly scan reminder was not found or could not be read: {result.Output}");
        }

        try
        {
            return ParseTaskEnabledXml(result.Output)
                ? new ScheduledScanStatus(true, "Weekly scan reminder is enabled.")
                : new ScheduledScanStatus(false, "Weekly scan reminder exists but is disabled in Task Scheduler.");
        }
        catch (XmlException ex)
        {
            return new ScheduledScanStatus(false, $"Weekly scan reminder status could not be read: {ex.Message}");
        }
        catch (FormatException ex)
        {
            return new ScheduledScanStatus(false, $"Weekly scan reminder status could not be read: {ex.Message}");
        }
    }

    public async Task<ScheduledScanStatus> EnableWeeklyAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new ScheduledScanStatus(false, "Scheduled scan reminders are available on Windows.");
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath) || !Path.IsPathFullyQualified(executablePath))
        {
            return new ScheduledScanStatus(false, "Could not determine the application path for scheduling.");
        }

        var taskCommand = $"\"{executablePath}\"";
        var result = await RunSchtasksAsync(
            "/Create", "/TN", TaskName, "/SC", "WEEKLY", "/D", "SUN", "/ST", "10:00", "/TR", taskCommand, "/F");
        return result.ExitCode == 0
            ? new ScheduledScanStatus(true, "Weekly scan reminder enabled for Sundays at 10:00.")
            : new ScheduledScanStatus(false, $"Could not enable weekly reminder: {result.Output}");
    }

    public async Task<ScheduledScanStatus> DisableAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new ScheduledScanStatus(false, "Scheduled scan reminders are available on Windows.");
        }

        var result = await RunSchtasksAsync("/Delete", "/TN", TaskName, "/F");
        return result.ExitCode == 0
            ? new ScheduledScanStatus(false, "Weekly scan reminder disabled.")
            : new ScheduledScanStatus(false, $"Could not disable weekly reminder: {result.Output}");
    }

    private static async Task<ProcessResult> RunSchtasksAsync(params string[] arguments)
    {
        try
        {
            var result = await ExternalProcessRunner.RunAsync(
                ExternalProcessRunner.GetSystemToolPath("schtasks.exe"),
                arguments,
                TimeSpan.FromSeconds(10));
            return new ProcessResult(result.Succeeded ? 0 : -1,
                result.Succeeded ? result.Output.Trim() : result.FailureMessage("schtasks.exe"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception
                                   or InvalidOperationException or TimeoutException)
        {
            return new ProcessResult(-1, ex.Message);
        }
    }

    internal static bool ParseTaskEnabledXml(string xml)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 256 * 1024
        };
        using var textReader = new StringReader(xml);
        using var reader = XmlReader.Create(textReader, settings);
        var document = XDocument.Load(reader);
        if (document.Root?.Name.LocalName != "Task")
        {
            throw new FormatException("Task Scheduler did not return a task definition.");
        }

        var enabled = document.Root.Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "Settings")?.Elements()
            .FirstOrDefault(element => element.Name.LocalName == "Enabled")?.Value;
        if (enabled is null)
        {
            return true; // Task Scheduler's XML schema defaults Settings/Enabled to true.
        }

        return bool.TryParse(enabled, out var isEnabled)
            ? isEnabled
            : throw new FormatException("Task Scheduler returned an invalid enabled state.");
    }

    private sealed record ProcessResult(int ExitCode, string Output);
}
