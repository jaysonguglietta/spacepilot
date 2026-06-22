using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace SpacePilot.Services;

public sealed record RestorePointRequestResult(bool Started, string Message);
public sealed record RestorePointStatus(bool IsAvailable, DateTime? LatestRestorePointLocal, string Message);

public sealed class SystemRestoreService
{
    public Task<RestorePointRequestResult> RequestRestorePointAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(new RestorePointRequestResult(false, "Restore points can only be requested on Windows."));
        }

        try
        {
            var systemRoot = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";
            var powershellPath = Path.Combine(systemRoot, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            var startInfo = new ProcessStartInfo(File.Exists(powershellPath) ? powershellPath : "powershell.exe")
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Normal,
                Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"Checkpoint-Computer -Description 'SpacePilot Pre-Cleanup' -RestorePointType MODIFY_SETTINGS\""
            };

            Process.Start(startInfo);
            return Task.FromResult(new RestorePointRequestResult(true, "Windows restore-point request was launched."));
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return Task.FromResult(new RestorePointRequestResult(false, "Restore-point request was cancelled."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new RestorePointRequestResult(false, $"Could not start restore-point request: {ex.Message}"));
        }
    }

    public Task<RestorePointStatus> GetStatusAsync()
    {
        return Task.Run(() =>
        {
            if (!OperatingSystem.IsWindows())
            {
                return new RestorePointStatus(false, null, "Restore points can only be checked on Windows.");
            }

            try
            {
                var systemRoot = Environment.GetEnvironmentVariable("SystemRoot") ?? @"C:\Windows";
                var powershellPath = Path.Combine(systemRoot, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
                var command = "$rp = Get-ComputerRestorePoint | Sort-Object CreationTime -Descending | Select-Object -First 1; if ($rp) { $rp.CreationTime } else { 'NONE' }";
                var startInfo = new ProcessStartInfo(File.Exists(powershellPath) ? powershellPath : "powershell.exe")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                startInfo.ArgumentList.Add("-NoProfile");
                startInfo.ArgumentList.Add("-ExecutionPolicy");
                startInfo.ArgumentList.Add("Bypass");
                startInfo.ArgumentList.Add("-Command");
                startInfo.ArgumentList.Add(command);

                using var process = Process.Start(startInfo);
                if (process is null)
                {
                    return new RestorePointStatus(false, null, "Could not start restore-point status check.");
                }

                var output = process.StandardOutput.ReadToEnd().Trim();
                var error = process.StandardError.ReadToEnd().Trim();
                var exited = process.WaitForExit(10000);
                if (!exited)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        // Best effort cleanup for a status check.
                    }

                    return new RestorePointStatus(false, null, "Restore-point status check timed out.");
                }

                if (process.ExitCode != 0)
                {
                    return new RestorePointStatus(false, null, string.IsNullOrWhiteSpace(error) ? "Could not read restore-point status." : error);
                }

                if (string.Equals(output, "NONE", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(output))
                {
                    return new RestorePointStatus(false, null, "No restore points were found. System Protection may be disabled.");
                }

                if (DateTime.TryParseExact(output, "yyyyMMddHHmmss.ffffffK", null, System.Globalization.DateTimeStyles.AssumeLocal, out var wmiDate)
                    || DateTime.TryParse(output, out wmiDate))
                {
                    return new RestorePointStatus(true, wmiDate, $"Latest restore point: {wmiDate:g}.");
                }

                return new RestorePointStatus(true, null, $"Restore points are available. Latest raw value: {output}");
            }
            catch (Exception ex)
            {
                return new RestorePointStatus(false, null, $"Could not read restore-point status: {ex.Message}");
            }
        });
    }
}
