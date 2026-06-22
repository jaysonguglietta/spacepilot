using System.Diagnostics;
using SpacePilot.Models;

namespace SpacePilot.Services;

public sealed class ScheduledScanService
{
    private const string TaskName = "SpacePilot Weekly Scan Reminder";

    public Task<ScheduledScanStatus> GetStatusAsync()
    {
        return Task.Run(() =>
        {
            if (!OperatingSystem.IsWindows())
            {
                return new ScheduledScanStatus(false, "Scheduled scan reminders are available on Windows.");
            }

            var result = RunSchtasks($"/Query /TN \"{TaskName}\"");
            return result.ExitCode == 0
                ? new ScheduledScanStatus(true, "Weekly scan reminder is enabled.")
                : new ScheduledScanStatus(false, "Weekly scan reminder is not enabled.");
        });
    }

    public Task<ScheduledScanStatus> EnableWeeklyAsync()
    {
        return Task.Run(() =>
        {
            if (!OperatingSystem.IsWindows())
            {
                return new ScheduledScanStatus(false, "Scheduled scan reminders are available on Windows.");
            }

            var executablePath = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(executablePath))
            {
                return new ScheduledScanStatus(false, "Could not determine the application path for scheduling.");
            }

            var taskCommand = $"\"{executablePath}\"";
            var result = RunSchtasks($"/Create /TN \"{TaskName}\" /SC WEEKLY /D SUN /ST 10:00 /TR \"{taskCommand}\" /F");
            return result.ExitCode == 0
                ? new ScheduledScanStatus(true, "Weekly scan reminder enabled for Sundays at 10:00.")
                : new ScheduledScanStatus(false, $"Could not enable weekly reminder: {result.Output}");
        });
    }

    public Task<ScheduledScanStatus> DisableAsync()
    {
        return Task.Run(() =>
        {
            if (!OperatingSystem.IsWindows())
            {
                return new ScheduledScanStatus(false, "Scheduled scan reminders are available on Windows.");
            }

            var result = RunSchtasks($"/Delete /TN \"{TaskName}\" /F");
            return result.ExitCode == 0
                ? new ScheduledScanStatus(false, "Weekly scan reminder disabled.")
                : new ScheduledScanStatus(false, $"Could not disable weekly reminder: {result.Output}");
        });
    }

    private static ProcessResult RunSchtasks(string arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo("schtasks.exe", arguments)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new ProcessResult(-1, "Could not start schtasks.exe.");
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            var exited = process.WaitForExit(10000);
            if (!exited)
            {
                try
                {
                    process.Kill();
                }
                catch
                {
                    // Best-effort cleanup for a helper process.
                }

                return new ProcessResult(-1, "schtasks.exe timed out.");
            }

            return new ProcessResult(process.ExitCode, string.IsNullOrWhiteSpace(output) ? error.Trim() : output.Trim());
        }
        catch (Exception ex)
        {
            return new ProcessResult(-1, ex.Message);
        }
    }

    private sealed record ProcessResult(int ExitCode, string Output);
}
