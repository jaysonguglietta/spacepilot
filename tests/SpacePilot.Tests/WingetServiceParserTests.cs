using SpacePilot.Services;

namespace SpacePilot.Tests;

public sealed class WingetServiceParserTests
{
    [Fact]
    public void ParseWingetPackages_ReadsJsonOutput()
    {
        var packages = WingetService.ParseWingetPackages(
        [
            """
            {
              "Sources": [
                {
                  "Packages": [
                    {
                      "Name": "Git",
                      "Id": "Git.Git",
                      "Version": "2.44.0",
                      "Available": "2.45.0",
                      "Source": "winget"
                    }
                  ]
                }
              ]
            }
            """
        ]);

        var package = Assert.Single(packages);
        Assert.Equal("Git", package.Name);
        Assert.Equal("Git.Git", package.Id);
        Assert.Equal("2.44.0", package.Version);
        Assert.Equal("2.45.0", package.AvailableVersion);
    }

    [Fact]
    public void ParseWingetPackages_FallsBackToTableOutput()
    {
        var packages = WingetService.ParseWingetPackages(
        [
            "Name      Id        Version Available Source",
            "------------------------------------------------",
            "Git       Git.Git   2.44.0  2.45.0    winget"
        ]);

        var package = Assert.Single(packages);
        Assert.Equal("Git", package.Name);
        Assert.Equal("Git.Git", package.Id);
        Assert.Equal("2.44.0", package.Version);
        Assert.Equal("2.45.0", package.AvailableVersion);
    }
}
