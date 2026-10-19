using System.Xml;
using SpacePilot.Services;

namespace SpacePilot.Tests;

public sealed class ScheduledScanServiceTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void ParseTaskEnabledXml_UsesInvariantSettingsFlag(string value, bool expected)
    {
        var xml = $"<Task xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\"><Settings><Enabled>{value}</Enabled></Settings></Task>";
        Assert.Equal(expected, ScheduledScanService.ParseTaskEnabledXml(xml));
    }

    [Fact]
    public void ParseTaskEnabledXml_RejectsMalformedResult()
    {
        Assert.Throws<XmlException>(() => ScheduledScanService.ParseTaskEnabledXml("<Task>"));
        Assert.Throws<FormatException>(() => ScheduledScanService.ParseTaskEnabledXml("<Tasks />"));
    }
}
