using System.Xml;
using SpacePilot.Services;

namespace SpacePilot.Tests;

public sealed class StartupServiceTests
{
    [Fact]
    public void ParseScheduledTaskXml_UsesInvariantElementsInsteadOfLocalizedCsvHeaders()
    {
        const string xml = """
            <Tasks xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <Task>
                <RegistrationInfo><URI>\Vendor\Updater</URI></RegistrationInfo>
                <Triggers><LogonTrigger><Enabled>true</Enabled></LogonTrigger></Triggers>
                <Settings><Enabled>true</Enabled></Settings>
                <Actions><Exec><Command>C:\Apps\Updater.exe</Command><Arguments>--quiet</Arguments></Exec></Actions>
              </Task>
              <Task>
                <RegistrationInfo><URI>\Vendor\Disabled</URI></RegistrationInfo>
                <Triggers><BootTrigger /></Triggers>
                <Settings><Enabled>false</Enabled></Settings>
                <Actions><Exec><Command>C:\Apps\Disabled.exe</Command></Exec></Actions>
              </Task>
              <Task>
                <RegistrationInfo><URI>\Microsoft\Windows\Defender</URI></RegistrationInfo>
                <Triggers><BootTrigger /></Triggers>
                <Actions><Exec><Command>C:\Windows\Defender.exe</Command></Exec></Actions>
              </Task>
              <Task>
                <RegistrationInfo><URI>\Vendor\Nightly</URI></RegistrationInfo>
                <Triggers><TimeTrigger /></Triggers>
                <Actions><Exec><Command>C:\Apps\Nightly.exe</Command></Exec></Actions>
              </Task>
            </Tasks>
            """;

        var entry = Assert.Single(StartupService.ParseScheduledTaskXml(xml));
        Assert.Equal("Vendor\\Updater", entry.Name);
        Assert.Equal("C:\\Apps\\Updater.exe --quiet", entry.Command);
        Assert.Equal("Task Scheduler", entry.Scope);
    }

    [Fact]
    public void ParseScheduledTaskXml_RejectsExternalEntityDeclarations()
    {
        const string xml = """
            <!DOCTYPE Task [<!ENTITY secret SYSTEM "file:///C:/private.txt">]>
            <Task><RegistrationInfo><URI>&secret;</URI></RegistrationInfo></Task>
            """;

        Assert.Throws<XmlException>(() => StartupService.ParseScheduledTaskXml(xml));
    }

    [Fact]
    public void ParseScheduledTaskXml_RejectsMalformedAndOversizedResults()
    {
        Assert.Throws<XmlException>(() => StartupService.ParseScheduledTaskXml("<Tasks><Task>"));
        var oversized = $"<Tasks><Task>{new string('X', 16 * 1024 * 1024)}</Task></Tasks>";
        Assert.Throws<XmlException>(() => StartupService.ParseScheduledTaskXml(oversized));
    }
}
