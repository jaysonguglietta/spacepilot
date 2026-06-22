using SpacePilot.Services;

namespace SpacePilot.Tests;

public sealed class PathSafetyTests
{
    [Fact]
    public void IsAllowedCleanupTarget_AllowsChildPathInsideApprovedRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "spacepilot-root");
        var candidate = Path.Combine(root, "cache", "item.tmp");

        Assert.True(PathSafety.IsAllowedCleanupTarget(candidate, root));
    }

    [Fact]
    public void IsAllowedCleanupTarget_RejectsApprovedRootItself()
    {
        var root = Path.Combine(Path.GetTempPath(), "spacepilot-root");

        Assert.False(PathSafety.IsAllowedCleanupTarget(root, root));
    }

    [Fact]
    public void IsAllowedCleanupTarget_RejectsSiblingWithSamePrefix()
    {
        var root = Path.Combine(Path.GetTempPath(), "spacepilot-root");
        var sibling = root + "-sibling";
        var candidate = Path.Combine(sibling, "item.tmp");

        Assert.False(PathSafety.IsAllowedCleanupTarget(candidate, root));
    }
}
