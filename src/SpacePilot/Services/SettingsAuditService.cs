using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32;
using SpacePilot.Models;

namespace SpacePilot.Services;

public sealed class SettingsAuditService
{
    public Task<IReadOnlyList<SettingsCheck>> GetChecksAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<SettingsCheck>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!OperatingSystem.IsWindows())
            {
                return
                [
                    new SettingsCheck(
                        "Windows runtime",
                        "Windows-specific checks are available when the app runs on Windows.",
                        SettingsCheckStatus.Unknown)
                ];
            }

            var checks = new List<SettingsCheck>
            {
                CheckAdministrator(),
                CheckStorageSense(),
                CheckTempFolder(),
                CheckRunningBrowsers(),
                CheckPowerShellAvailability(),
                CheckLatestRestorePoint()
            };

            return checks;
        }, cancellationToken);
    }

    private static SettingsCheck CheckAdministrator()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            var isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            return new SettingsCheck(
                "Administrator mode",
                isAdmin
                    ? "The app is running with administrator rights, so system cleanup locations are more likely to be accessible."
                    : "The app is running as a standard user. User cleanup works, but system folders may be skipped.",
                isAdmin ? SettingsCheckStatus.Good : SettingsCheckStatus.Attention);
        }
        catch
        {
            return new SettingsCheck(
                "Administrator mode",
                "Could not determine the current elevation level.",
                SettingsCheckStatus.Unknown);
        }
    }

    private static SettingsCheck CheckStorageSense()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy");
            var enabledValue = key?.GetValue("01");
            var enabled = enabledValue is int intValue && intValue != 0;

            return new SettingsCheck(
                "Storage Sense",
                enabled
                    ? "Windows Storage Sense appears to be enabled for automatic maintenance."
                    : "Windows Storage Sense does not appear to be enabled for this user.",
                enabled ? SettingsCheckStatus.Good : SettingsCheckStatus.Attention);
        }
        catch
        {
            return new SettingsCheck(
                "Storage Sense",
                "Could not read the Storage Sense policy state.",
                SettingsCheckStatus.Unknown);
        }
    }

    private static SettingsCheck CheckTempFolder()
    {
        var temp = Path.GetTempPath();
        if (Directory.Exists(temp))
        {
            return new SettingsCheck(
                "User temp folder",
                $"Temp folder is available at {temp}.",
                SettingsCheckStatus.Good);
        }

        return new SettingsCheck(
            "User temp folder",
            $"Temp folder is not available at {temp}.",
            SettingsCheckStatus.Attention);
    }

    private static SettingsCheck CheckRunningBrowsers()
    {
        var running = new[]
        {
            ("msedge", "Edge"),
            ("chrome", "Chrome"),
            ("firefox", "Firefox")
        }
        .Where(browser => IsProcessRunning(browser.Item1))
        .Select(browser => browser.Item2)
        .ToList();

        if (running.Count == 0)
        {
            return new SettingsCheck(
                "Browser cache access",
                "Supported browsers do not appear to be running.",
                SettingsCheckStatus.Good);
        }

        return new SettingsCheck(
            "Browser cache access",
            $"Close {string.Join(", ", running)} before browser cache cleaning for best results.",
            SettingsCheckStatus.Attention);
    }

    private static SettingsCheck CheckPowerShellAvailability()
    {
        var systemRoot = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";
        var powershell = Path.Combine(systemRoot, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        return new SettingsCheck(
            "Restore point command",
            File.Exists(powershell)
                ? "Windows PowerShell is available for restore-point requests."
                : "Windows PowerShell was not found in the expected system location.",
            File.Exists(powershell) ? SettingsCheckStatus.Good : SettingsCheckStatus.Attention);
    }

    private static SettingsCheck CheckLatestRestorePoint()
    {
        try
        {
            var status = new SystemRestoreService().GetStatusAsync().GetAwaiter().GetResult();
            return new SettingsCheck(
                "Restore point status",
                status.Message,
                status.IsAvailable ? SettingsCheckStatus.Good : SettingsCheckStatus.Attention);
        }
        catch
        {
            return new SettingsCheck(
                "Restore point status",
                "Could not check restore-point status.",
                SettingsCheckStatus.Unknown);
        }
    }

    private static bool IsProcessRunning(string processName)
    {
        try
        {
            return Process.GetProcessesByName(processName).Length > 0;
        }
        catch
        {
            return false;
        }
    }
}
