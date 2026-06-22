namespace SpacePilot.Models;

public sealed record InstalledAppInfo(
    string Name,
    string Publisher,
    string Version,
    string InstallDate,
    long EstimatedSizeBytes,
    string UninstallCommand);
