using SpacePilot.Models;

namespace SpacePilot.Services;

public static class CleanupRuleCatalog
{
    public static IReadOnlyList<CleanupRule> CreateDefaultRules()
    {
        return
        [
            new CleanupRule(
                "user-temp",
                "User temporary files",
                "Files created in your account temp folder that are at least one day old.",
                true,
                RiskLevel.Low,
                1,
                [
                    new CleanupLocation("%TEMP%", "*", true, true, true)
                ]),
            new CleanupRule(
                "windows-temp",
                "Windows temporary files",
                "System temp files older than a week. Some files may require administrator rights.",
                true,
                RiskLevel.Medium,
                7,
                [
                    new CleanupLocation("%WINDIR%\\Temp", "*", true, true, true)
                ]),
            new CleanupRule(
                "wer-reports",
                "Windows error reports",
                "Archived crash reports and diagnostic queues that Windows keeps after application failures.",
                true,
                RiskLevel.Low,
                7,
                [
                    new CleanupLocation("%LOCALAPPDATA%\\Microsoft\\Windows\\WER\\ReportArchive", "*", true, true, true),
                    new CleanupLocation("%LOCALAPPDATA%\\Microsoft\\Windows\\WER\\ReportQueue", "*", true, true, true),
                    new CleanupLocation("%ProgramData%\\Microsoft\\Windows\\WER\\ReportArchive", "*", true, true, true),
                    new CleanupLocation("%ProgramData%\\Microsoft\\Windows\\WER\\ReportQueue", "*", true, true, true)
                ]),
            new CleanupRule(
                "crash-dumps",
                "Application crash dumps",
                "Local crash dump files created when desktop apps fail.",
                true,
                RiskLevel.Low,
                7,
                [
                    new CleanupLocation("%LOCALAPPDATA%\\CrashDumps", "*.dmp", false, true, false),
                    new CleanupLocation("%LOCALAPPDATA%\\CrashDumps", "*.mdmp", false, true, false)
                ]),
            new CleanupRule(
                "delivery-optimization",
                "Delivery Optimization cache",
                "Windows Update delivery cache entries older than two weeks.",
                false,
                RiskLevel.Medium,
                14,
                [
                    new CleanupLocation("%SystemRoot%\\SoftwareDistribution\\Download", "*", true, true, true)
                ]),
            new CleanupRule(
                "thumbnail-cache",
                "Thumbnail cache",
                "Explorer thumbnail database files. Explorer may recreate these after cleaning.",
                false,
                RiskLevel.Medium,
                1,
                [
                    new CleanupLocation("%LOCALAPPDATA%\\Microsoft\\Windows\\Explorer", "thumbcache_*.db", false, true, false),
                    new CleanupLocation("%LOCALAPPDATA%\\Microsoft\\Windows\\Explorer", "iconcache_*.db", false, true, false)
                ]),
            new CleanupRule(
                "edge-cache",
                "Microsoft Edge cache",
                "Cache folders for the default Edge profile. Close Edge before cleaning for best results.",
                false,
                RiskLevel.Medium,
                0,
                [
                    new CleanupLocation("%LOCALAPPDATA%\\Microsoft\\Edge\\User Data\\Default\\Cache", "*", true, true, true),
                    new CleanupLocation("%LOCALAPPDATA%\\Microsoft\\Edge\\User Data\\Default\\Code Cache", "*", true, true, true),
                    new CleanupLocation("%LOCALAPPDATA%\\Microsoft\\Edge\\User Data\\Default\\GPUCache", "*", true, true, true)
                ]),
            new CleanupRule(
                "chrome-cache",
                "Google Chrome cache",
                "Cache folders for the default Chrome profile. Close Chrome before cleaning for best results.",
                false,
                RiskLevel.Medium,
                0,
                [
                    new CleanupLocation("%LOCALAPPDATA%\\Google\\Chrome\\User Data\\Default\\Cache", "*", true, true, true),
                    new CleanupLocation("%LOCALAPPDATA%\\Google\\Chrome\\User Data\\Default\\Code Cache", "*", true, true, true),
                    new CleanupLocation("%LOCALAPPDATA%\\Google\\Chrome\\User Data\\Default\\GPUCache", "*", true, true, true)
                ]),
            new CleanupRule(
                "firefox-cache",
                "Mozilla Firefox cache",
                "Cache folders in Firefox profiles. Close Firefox before cleaning for best results.",
                false,
                RiskLevel.Medium,
                0,
                [
                    new CleanupLocation("%LOCALAPPDATA%\\Mozilla\\Firefox\\Profiles\\*\\cache2", "*", true, true, true),
                    new CleanupLocation("%LOCALAPPDATA%\\Mozilla\\Firefox\\Profiles\\*\\startupCache", "*", true, true, true)
                ]),
            new CleanupRule(
                "teams-cache",
                "Microsoft Teams cache",
                "Teams web cache and GPU cache. Close Teams before cleaning.",
                false,
                RiskLevel.Medium,
                0,
                [
                    new CleanupLocation("%APPDATA%\\Microsoft\\Teams\\Cache", "*", true, true, true),
                    new CleanupLocation("%APPDATA%\\Microsoft\\Teams\\Code Cache", "*", true, true, true),
                    new CleanupLocation("%APPDATA%\\Microsoft\\Teams\\GPUCache", "*", true, true, true),
                    new CleanupLocation("%LOCALAPPDATA%\\Packages\\MSTeams_*\\LocalCache\\Microsoft\\MSTeams\\Cache", "*", true, true, true)
                ]),
            new CleanupRule(
                "slack-cache",
                "Slack cache",
                "Slack cache folders. Close Slack before cleaning.",
                false,
                RiskLevel.Medium,
                0,
                [
                    new CleanupLocation("%APPDATA%\\Slack\\Cache", "*", true, true, true),
                    new CleanupLocation("%APPDATA%\\Slack\\Code Cache", "*", true, true, true),
                    new CleanupLocation("%APPDATA%\\Slack\\GPUCache", "*", true, true, true)
                ]),
            new CleanupRule(
                "discord-cache",
                "Discord cache",
                "Discord cache folders. Close Discord before cleaning.",
                false,
                RiskLevel.Medium,
                0,
                [
                    new CleanupLocation("%APPDATA%\\discord\\Cache", "*", true, true, true),
                    new CleanupLocation("%APPDATA%\\discord\\Code Cache", "*", true, true, true),
                    new CleanupLocation("%APPDATA%\\discord\\GPUCache", "*", true, true, true)
                ]),
            new CleanupRule(
                "zoom-logs",
                "Zoom logs",
                "Zoom diagnostic logs older than one week.",
                true,
                RiskLevel.Low,
                7,
                [
                    new CleanupLocation("%APPDATA%\\Zoom\\logs", "*", true, true, true),
                    new CleanupLocation("%APPDATA%\\Zoom\\data", "*.log", true, true, false)
                ]),
            new CleanupRule(
                "windows-logs",
                "Windows maintenance logs",
                "CBS, DISM, and setup logs older than two weeks. Useful for support, so review before cleaning.",
                false,
                RiskLevel.Medium,
                14,
                [
                    new CleanupLocation("%WINDIR%\\Logs\\CBS", "*.log", false, true, false),
                    new CleanupLocation("%WINDIR%\\Logs\\DISM", "*.log", false, true, false),
                    new CleanupLocation("%WINDIR%\\Panther", "*.log", false, true, false)
                ]),
            new CleanupRule(
                "package-temp",
                "Microsoft Store package temp",
                "Temporary files under Store app package caches.",
                false,
                RiskLevel.Medium,
                3,
                [
                    new CleanupLocation("%LOCALAPPDATA%\\Packages\\*\\LocalCache\\Temp", "*", true, true, true),
                    new CleanupLocation("%LOCALAPPDATA%\\Packages\\*\\TempState", "*", true, true, true)
                ])
        ];
    }
}
