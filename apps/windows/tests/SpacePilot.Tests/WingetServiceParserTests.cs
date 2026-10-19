using SpacePilot.Services;

namespace SpacePilot.Tests;

public sealed class WingetServiceParserTests
{
    [Fact]
    public void ParseWingetTable_UsesDashRunColumnsWithLocalizedHeadings()
    {
        var packages = WingetService.ParseWingetTable(
        [
            Row("Nombre", "Identificador", "Versión", "Disponible", "Origen"),
            $"{new string('-', 26)}  {new string('-', 28)}  {new string('-', 10)}  {new string('-', 10)}  {new string('-', 6)}",
            Row("Git for Windows", "Git.Git", "2.44.0", "2.45.0", "winget"),
            Row("Visual Studio Code", "Microsoft.VisualStudioCode", "1.95", "1.96", "winget"),
            "2 upgrades available."
        ]);

        Assert.Collection(packages,
            package =>
            {
                Assert.Equal("Git for Windows", package.Name);
                Assert.Equal("Git.Git", package.Id);
                Assert.Equal("2.45.0", package.AvailableVersion);
                Assert.Equal("winget", package.Source);
            },
            package => Assert.Equal("Microsoft.VisualStudioCode", package.Id));
    }

    [Fact]
    public void ParseWingetTable_UsesHeaderSpacingForContinuousRule()
    {
        var packages = WingetService.ParseWingetTable(
        [
            Row("Name", "Id", "Version", "Available", "Source"),
            new string('-', 88),
            Row("Git for Windows", "Git.Git", "2.44.0", "2.45.0", "winget")
        ]);

        Assert.Equal("Git.Git", Assert.Single(packages).Id);
    }

    [Theory]
    [InlineData("No applicable upgrade found.")]
    [InlineData("No applicable update found.")]
    [InlineData("No upgrades available.")]
    public void ParseWingetTable_RecognizesEmptyUpdateLists(string message)
    {
        Assert.Empty(WingetService.ParseWingetTable([message]));
    }

    [Fact]
    public void ParseWingetTable_RejectsUnknownOutputAndTruncatedIds()
    {
        Assert.Throws<FormatException>(() => WingetService.ParseWingetTable(["source retrieval failed"]));
        Assert.Throws<FormatException>(() => WingetService.ParseWingetTable(
        [
            Row("Name", "Id", "Version", "Available", "Source"),
            new string('-', 88),
            Row("Git", "Git.Gi…", "2.44.0", "2.45.0", "winget")
        ]));
    }

    private static string Row(string name, string id, string version, string available, string source) =>
        $"{name,-28}{id,-30}{version,-12}{available,-12}{source}";
}
