namespace SpacePilot.Models;

public enum SettingsCheckStatus
{
    Good,
    Attention,
    Unknown
}

public sealed record SettingsCheck(
    string Name,
    string Description,
    SettingsCheckStatus Status);
