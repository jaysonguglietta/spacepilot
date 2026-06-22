using SpacePilot.Models;
using SpacePilot.Services;

namespace SpacePilot.Tests;

public sealed class CleanupRuleCatalogTests
{
    [Fact]
    public void CreateDefaultRules_ReturnsUniqueRuleIdsWithCleanupLocations()
    {
        var rules = CleanupRuleCatalog.CreateDefaultRules();

        Assert.NotEmpty(rules);
        Assert.Equal(rules.Count, rules.Select(rule => rule.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(rules, rule =>
        {
            Assert.False(string.IsNullOrWhiteSpace(rule.Name));
            Assert.NotEmpty(rule.Locations);
        });
    }

    [Fact]
    public void CreateDefaultRules_DoesNotTargetBroadPersonalFolders()
    {
        var blockedFragments = new[]
        {
            "\\Desktop",
            "\\Documents",
            "\\Downloads",
            "\\Pictures",
            "\\Videos",
            "\\Music"
        };

        var locations = CleanupRuleCatalog.CreateDefaultRules()
            .SelectMany(rule => rule.Locations.Select(location => (rule, location)));

        foreach (var (rule, location) in locations)
        {
            Assert.DoesNotContain(blockedFragments, fragment =>
                location.RootPath.Contains(fragment, StringComparison.OrdinalIgnoreCase)
                && location.SearchPattern == "*"
                && rule.Risk != RiskLevel.High);
        }
    }
}
