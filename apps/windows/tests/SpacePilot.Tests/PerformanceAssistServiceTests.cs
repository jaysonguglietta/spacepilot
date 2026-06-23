using SpacePilot.Models;
using SpacePilot.Services;

namespace SpacePilot.Tests;

public sealed class PerformanceAssistServiceTests
{
    [Theory]
    [InlineData(45, "Good")]
    [InlineData(70, "Elevated")]
    [InlineData(80, "High")]
    [InlineData(90, "Critical")]
    public void ClassifyMemoryPressureUsesExpectedThresholds(double usagePercent, string expected)
    {
        Assert.Equal(expected, PerformanceAssistService.ClassifyMemoryPressure(usagePercent));
    }

    [Fact]
    public void BuildRecommendationsIncludesStartupAndMaintenanceSignals()
    {
        var snapshot = new SystemPerformanceSnapshot(
            16L * 1_073_741_824,
            1L * 1_073_741_824,
            93,
            null,
            null,
            120,
            TimeSpan.FromDays(9),
            "Critical",
            "Critical memory pressure.");
        var process = new ProcessMemoryInfo(
            42,
            "chrome",
            3L * 1_073_741_824,
            2L * 1_073_741_824,
            "Protected or system process",
            "Restart the browser.",
            "Use Task Manager.");
        var startup = new[]
        {
            new StartupEntry("Chat client", "User", "Registry", "chat.exe", "HKCU", true, "High", "Review startup.")
        };

        var recommendations = PerformanceAssistService.BuildRecommendations(snapshot, [process], startup, wingetUpdateCount: 2, browserProfileCount: 1);

        Assert.Contains(recommendations, item => item.Area == "RAM pressure" && item.Status == "Critical");
        Assert.Contains(recommendations, item => item.Area == "Top memory app");
        Assert.Contains(recommendations, item => item.Area == "Startup load");
        Assert.Contains(recommendations, item => item.Area == "App maintenance");
        Assert.Contains(recommendations, item => item.Area == "Browser load");
        Assert.Contains(recommendations, item => item.Area == "Restart cadence");
    }
}
