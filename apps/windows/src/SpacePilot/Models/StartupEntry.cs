namespace SpacePilot.Models;

public sealed record StartupEntry(
    string Name,
    string Scope,
    string SourceType,
    string Command,
    string Location,
    bool IsEnabled,
    string Impact,
    string Recommendation);
