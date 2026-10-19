using SpacePilot.Models;
using SpacePilot.ViewModels;

namespace SpacePilot.Tests;

public sealed class BrowserProfileViewModelTests
{
    [Fact]
    public void FirefoxHistoryCannotBeSelectedEvenWithOldSavedPreference()
    {
        var profile = new BrowserProfileInfo("Mozilla Firefox", "default", "C:\\Users\\Me\\Firefox", false, false, true, false);
        var viewModel = new BrowserProfileViewModel(profile, null);

        Assert.False(viewModel.CanCleanHistory);
        Assert.False(viewModel.HistorySelected);
        viewModel.HistorySelected = true;
        Assert.False(viewModel.HistorySelected);
    }
}
