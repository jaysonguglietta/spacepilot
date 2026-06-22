using System.IO;
using Microsoft.Win32;
using SpacePilot.Models;

namespace SpacePilot.Services;

public sealed class SoftwareInventoryService
{
    private const string UninstallKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

    public Task<IReadOnlyList<InstalledAppInfo>> GetInstalledAppsAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<InstalledAppInfo>>(() =>
        {
            if (!OperatingSystem.IsWindows())
            {
                return Array.Empty<InstalledAppInfo>();
            }

            var apps = new List<InstalledAppInfo>();
            ReadHive(RegistryHive.CurrentUser, RegistryView.Registry64, apps, cancellationToken);
            ReadHive(RegistryHive.CurrentUser, RegistryView.Registry32, apps, cancellationToken);
            ReadHive(RegistryHive.LocalMachine, RegistryView.Registry64, apps, cancellationToken);
            ReadHive(RegistryHive.LocalMachine, RegistryView.Registry32, apps, cancellationToken);

            return apps
                .GroupBy(app => $"{app.Name}|{app.Publisher}|{app.Version}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }, cancellationToken);
    }

    private static void ReadHive(RegistryHive hive, RegistryView view, List<InstalledAppInfo> apps, CancellationToken cancellationToken)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var uninstallKey = baseKey.OpenSubKey(UninstallKeyPath);
            if (uninstallKey is null)
            {
                return;
            }

            foreach (var subKeyName in uninstallKey.GetSubKeyNames())
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var appKey = uninstallKey.OpenSubKey(subKeyName);
                if (appKey is null)
                {
                    continue;
                }

                var name = ReadString(appKey, "DisplayName");
                if (string.IsNullOrWhiteSpace(name) || IsHiddenSystemComponent(appKey))
                {
                    continue;
                }

                apps.Add(new InstalledAppInfo(
                    name,
                    ReadString(appKey, "Publisher"),
                    ReadString(appKey, "DisplayVersion"),
                    FormatInstallDate(ReadString(appKey, "InstallDate")),
                    ReadEstimatedSizeBytes(appKey),
                    ReadString(appKey, "UninstallString")));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // Inventory is best-effort. Individual hives can be inaccessible depending on policy.
        }
    }

    private static bool IsHiddenSystemComponent(RegistryKey key)
    {
        var value = key.GetValue("SystemComponent");
        return value is int intValue && intValue == 1;
    }

    private static string ReadString(RegistryKey key, string name)
    {
        return key.GetValue(name)?.ToString()?.Trim() ?? string.Empty;
    }

    private static long ReadEstimatedSizeBytes(RegistryKey key)
    {
        var value = key.GetValue("EstimatedSize");
        return value switch
        {
            int kb => Math.Max(0, (long)kb) * 1024L,
            long kb => Math.Max(0, kb) * 1024L,
            _ => 0
        };
    }

    private static string FormatInstallDate(string value)
    {
        if (value.Length == 8
            && DateTime.TryParseExact(value, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var date))
        {
            return date.ToShortDateString();
        }

        return value;
    }
}
