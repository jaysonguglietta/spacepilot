using System.ComponentModel;
using System.Diagnostics;

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
            var powershellPath = ExternalProcessRunner.GetSystemToolPath(
                Path.Combine("WindowsPowerShell", "v1.0", "powershell.exe"));
            var startInfo = new ProcessStartInfo(powershellPath)
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Normal,
                Arguments = "-NoProfile -Command \"Checkpoint-Computer -Description 'SpacePilot Pre-Cleanup' -RestorePointType MODIFY_SETTINGS\""
            };

            if (Process.Start(startInfo) is null)
            {
                return Task.FromResult(new RestorePointRequestResult(false, "Could not launch the restore-point request."));
            }
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

    public async Task<RestorePointStatus> GetStatusAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return new RestorePointStatus(false, null, "Restore points can only be checked on Windows.");
        }

        try
        {
            var powershellPath = ExternalProcessRunner.GetSystemToolPath(
                Path.Combine("WindowsPowerShell", "v1.0", "powershell.exe"));
            var command = "$ErrorActionPreference = 'Stop'; $rp = Get-ComputerRestorePoint | Sort-Object CreationTime -Descending | Select-Object -First 1; if ($rp) { $date = [System.Management.ManagementDateTimeConverter]::ToDateTime([string]$rp.CreationTime); ([DateTimeOffset]$date).ToString('o', [System.Globalization.CultureInfo]::InvariantCulture) } else { 'NONE' }";
            var result = await ExternalProcessRunner.RunAsync(
                powershellPath,
                ["-NoProfile", "-NonInteractive", "-Command", command],
                TimeSpan.FromSeconds(15));
            if (!result.Succeeded)
            {
                return new RestorePointStatus(false, null, result.FailureMessage("Restore-point status check"));
            }

            var output = result.Output.Trim();
            if (string.Equals(output, "NONE", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(output))
            {
                return new RestorePointStatus(false, null, "No restore points were found. System Protection may be disabled.");
            }

            if (DateTimeOffset.TryParseExact(output, "O", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var parsedDate))
            {
                var localDate = parsedDate.LocalDateTime;
                return new RestorePointStatus(true, localDate, $"Latest restore point: {localDate:g}.");
            }

            return new RestorePointStatus(false, null, "The restore-point query returned an unrecognized date; availability could not be verified.");
        }
        catch (Exception ex)
        {
            return new RestorePointStatus(false, null, $"Could not read restore-point status: {ex.Message}");
        }
    }
}
