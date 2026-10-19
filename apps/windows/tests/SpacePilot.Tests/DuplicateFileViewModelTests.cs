using SpacePilot.Models;
using SpacePilot.ViewModels;

namespace SpacePilot.Tests;

public sealed class DuplicateFileViewModelTests
{
    [Fact]
    public void Constructor_DoesNotPreselectRecommendedDuplicate()
    {
        var info = new DuplicateFileInfo("Set 1", "abc", "C:\\Users\\Me\\extra.bin", "extra.bin",
            "C:\\Users\\Me", 10, DateTime.UtcNow, true);
        var viewModel = new DuplicateFileViewModel(info, null);

        Assert.False(viewModel.IsSelected);
        Assert.False(viewModel.IsKeeper);
        viewModel.IsKeeper = true;
        viewModel.IsSelected = true;
        Assert.False(viewModel.IsSelected);
    }
}
