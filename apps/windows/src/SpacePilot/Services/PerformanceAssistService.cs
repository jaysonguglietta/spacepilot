using System.Diagnostics;
using System.Runtime.InteropServices;
using SpacePilot.Models;

namespace SpacePilot.Services;

public sealed class PerformanceAssistService
{
    private const long OneGigabyte = 1_073_741_824;

    public Task<PerformanceAssistResult> GetSnapshotAsync(
        IReadOnlyCollection<StartupEntry> startupEntries,
        int wingetUpdateCount,
        int browserProfileCount)
    {
        return Task.Run(() => GetSnapshot(startupEntries, wingetUpdateCount, browserProfileCount));
    }

    internal PerformanceAssistResult GetSnapshot(
        IReadOnlyCollection<StartupEntry> startupEntries,
        int wingetUpdateCount,
        int browserProfileCount)
    {
        var snapshot = CaptureSnapshot();
        var processes = GetTopProcesses();
        var recommendations = BuildRecommendations(snapshot, processes, startupEntries, wingetUpdateCount, browserProfileCount);

        return new PerformanceAssistResult(snapshot, processes, recommendations);
    }

    internal static string ClassifyMemoryPressure(double memoryUsagePercent)
    {
        return memoryUsagePercent switch
        {
            >= 90 => "Critical",
            >= 80 => "High",
            >= 70 => "Elevated",
            _ => "Good"
        };
    }

    internal static IReadOnlyList<PerformanceRecommendation> BuildRecommendations(
        SystemPerformanceSnapshot snapshot,
        IReadOnlyList<ProcessMemoryInfo> processes,
        IReadOnlyCollection<StartupEntry> startupEntries,
        int wingetUpdateCount,
        int browserProfileCount)
    {
        var recommendations = new List<PerformanceRecommendation>();

        recommendations.Add(snapshot.MemoryPressure switch
        {
            "Critical" => new PerformanceRecommendation(
                "RAM pressure",
                "Critical",
                "Save work, close or restart the largest apps, then recheck memory pressure.",
                "Can improve responsiveness immediately.",
                "Open Task Manager"),
            "High" => new PerformanceRecommendation(
                "RAM pressure",
                "Attention",
                "Review the top memory apps and close anything you are not actively using.",
                "Reduces paging and app stalls.",
                "Review top processes"),
            "Elevated" => new PerformanceRecommendation(
                "RAM pressure",
                "Watch",
                "Memory use is elevated. Keep an eye on browsers, launchers, and developer tools.",
                "Prevents slowdowns before they become disruptive.",
                "Refresh RAM Assist"),
            _ => new PerformanceRecommendation(
                "RAM pressure",
                "Good",
                "Memory pressure is healthy. Avoid force-freeing RAM; Windows uses free memory for useful cache.",
                "Keeps the system stable and fast.",
                "No action needed")
        });

        var topProcess = processes.FirstOrDefault(process => process.WorkingSetBytes >= 2 * OneGigabyte)
            ?? processes.FirstOrDefault(process => process.WorkingSetBytes >= OneGigabyte);
        if (topProcess is not null)
        {
            recommendations.Add(new PerformanceRecommendation(
                "Top memory app",
                "Review",
                $"{topProcess.Name} is using a large amount of RAM. Restart it if it feels stuck or is no longer needed.",
                "Often recovers memory without risky system tweaks.",
                "Open Task Manager"));
        }

        var highImpactStartup = startupEntries.Count(entry =>
            entry.IsEnabled && entry.Impact.Contains("High", StringComparison.OrdinalIgnoreCase));
        if (highImpactStartup > 0 || startupEntries.Count >= 10)
        {
            recommendations.Add(new PerformanceRecommendation(
                "Startup load",
                highImpactStartup > 0 ? "Attention" : "Review",
                "Trim nonessential startup apps in Windows Startup settings.",
                "Improves boot time and reduces background memory use.",
                "Review Startup"));
        }

        if (wingetUpdateCount > 0)
        {
            recommendations.Add(new PerformanceRecommendation(
                "App maintenance",
                "Updates available",
                "Update selected WinGet packages after saving work.",
                "Newer app builds can fix memory leaks and performance issues.",
                "Review Software"));
        }

        if (browserProfileCount > 0)
        {
            recommendations.Add(new PerformanceRecommendation(
                "Browser load",
                "Review",
                "Clean cache safely and restart browsers when tab or extension memory grows.",
                "Browsers are common sources of reclaimable memory and disk cache.",
                "Review Browsers"));
        }

        if (snapshot.Uptime.TotalDays >= 7)
        {
            recommendations.Add(new PerformanceRecommendation(
                "Restart cadence",
                "Review",
                "The PC has been running for a week or more. Restart after saving work if performance feels degraded.",
                "Clears stuck drivers, hung helpers, and abandoned app memory.",
                "Restart later"));
        }

        return recommendations;
    }

    private static SystemPerformanceSnapshot CaptureSnapshot()
    {
        var memory = new MemoryStatusEx();
        memory.dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>();

        if (!GlobalMemoryStatusEx(ref memory))
        {
            return new SystemPerformanceSnapshot(
                0,
                0,
                0,
                null,
                null,
                SafeProcessCount(),
                TimeSpan.FromMilliseconds(Environment.TickCount64),
                "Unknown",
                "SpacePilot could not read Windows memory counters.");
        }

        var total = (long)Math.Min(memory.ullTotalPhys, (ulong)long.MaxValue);
        var available = (long)Math.Min(memory.ullAvailPhys, (ulong)long.MaxValue);
        var usagePercent = memory.dwMemoryLoad > 0
            ? memory.dwMemoryLoad
            : total <= 0 ? 0 : Math.Clamp(((double)(total - available) / total) * 100, 0, 100);
        var pressure = ClassifyMemoryPressure(usagePercent);
        var processCount = SafeProcessCount();
        var commitLimit = (long)Math.Min(memory.ullTotalPageFile, (ulong)long.MaxValue);
        var commitAvailable = (long)Math.Min(memory.ullAvailPageFile, (ulong)long.MaxValue);
        long? commitUsed = commitLimit > 0 ? Math.Max(0, commitLimit - commitAvailable) : null;
        var summary = pressure == "Good"
            ? "RAM pressure is healthy. Windows cache does not need manual clearing."
            : $"RAM pressure is {pressure.ToLowerInvariant()}. Review the largest apps before closing anything.";

        return new SystemPerformanceSnapshot(
            total,
            available,
            usagePercent,
            commitUsed,
            commitLimit > 0 ? commitLimit : null,
            processCount,
            TimeSpan.FromMilliseconds(Environment.TickCount64),
            pressure,
            summary);
    }

    private static IReadOnlyList<ProcessMemoryInfo> GetTopProcesses()
    {
        var items = new List<ProcessMemoryInfo>();

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    var workingSet = SafeGet(() => process.WorkingSet64);
                    if (workingSet <= 0)
                    {
                        continue;
                    }

                    var name = SafeGet(() => process.ProcessName) ?? "Unknown";
                    items.Add(new ProcessMemoryInfo(
                        SafeGet(() => process.Id),
                        name,
                        workingSet,
                        SafeGet(() => process.PrivateMemorySize64),
                        SafeGet(() => process.MainModule?.FileName) ?? "Protected or system process",
                        RecommendationForProcess(name, workingSet),
                        "Use Task Manager to close apps. SpacePilot never ends processes automatically."));
                }
                catch
                {
                    // Processes can exit while being inspected or deny metadata access.
                }
            }
        }

        return items
            .OrderByDescending(process => process.WorkingSetBytes)
            .Take(25)
            .ToList();
    }

    private static string RecommendationForProcess(string processName, long workingSetBytes)
    {
        var normalized = processName.ToLowerInvariant();
        if (normalized.Contains("chrome") || normalized.Contains("msedge") || normalized.Contains("firefox") || normalized.Contains("brave"))
        {
            return "Restart the browser or reduce tabs/extensions if memory keeps growing.";
        }

        if (normalized.Contains("code") || normalized.Contains("devenv") || normalized.Contains("rider"))
        {
            return "Close unused projects, terminals, and build tasks before ending the app.";
        }

        if (workingSetBytes >= 2 * OneGigabyte)
        {
            return "Restart or close this app if it is not doing active work.";
        }

        if (workingSetBytes >= OneGigabyte)
        {
            return "Review if performance feels slow; otherwise leave it alone.";
        }

        return "Monitor only.";
    }

    private static int SafeProcessCount()
    {
        Process[] processes = [];
        try
        {
            processes = Process.GetProcesses();
            return processes.Length;
        }
        catch
        {
            return 0;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static T? SafeGet<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch
        {
            return default;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }
}
