using SpacePilot.Models;

namespace SpacePilot.ViewModels;

public sealed class WingetPackageViewModel : ObservableObject
{
    private readonly Action? _selectionChanged;
    private bool _isSelected = true;

    public WingetPackageViewModel(WingetPackageInfo package, Action? selectionChanged)
    {
        Package = package;
        _selectionChanged = selectionChanged;
    }

    public WingetPackageInfo Package { get; }
    public string Name => Package.Name;
    public string Id => Package.Id;
    public string Version => Package.Version;
    public string AvailableVersion => Package.AvailableVersion;
    public string Source => Package.Source;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                _selectionChanged?.Invoke();
            }
        }
    }
}
