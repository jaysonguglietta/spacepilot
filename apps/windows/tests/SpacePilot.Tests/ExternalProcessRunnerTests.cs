using SpacePilot.Services;

namespace SpacePilot.Tests;

public sealed class ExternalProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_DrainsBothStreamsWithoutUnboundedBuffering()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var powershell = ExternalProcessRunner.GetSystemToolPath(
            Path.Combine("WindowsPowerShell", "v1.0", "powershell.exe"));
        var result = await ExternalProcessRunner.RunAsync(
            powershell,
            ["-NoProfile", "-NonInteractive", "-Command",
             "$text = 'X' * 200000; [Console]::Out.Write($text); [Console]::Error.Write($text)"],
            TimeSpan.FromSeconds(15),
            outputLimit: 1024);

        Assert.Equal(0, result.ExitCode);
        Assert.True(result.OutputTruncated);
        Assert.False(result.Succeeded);
        Assert.Equal(1024, result.Output.Length);
        Assert.Equal(1024, result.Error.Length);
    }

    [Fact]
    public async Task RunAsync_KillsHelperAfterDeadline()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var powershell = ExternalProcessRunner.GetSystemToolPath(
            Path.Combine("WindowsPowerShell", "v1.0", "powershell.exe"));
        await Assert.ThrowsAsync<TimeoutException>(() => ExternalProcessRunner.RunAsync(
            powershell,
            ["-NoProfile", "-NonInteractive", "-Command", "Start-Sleep -Seconds 5"],
            TimeSpan.FromMilliseconds(300)));
    }
}
