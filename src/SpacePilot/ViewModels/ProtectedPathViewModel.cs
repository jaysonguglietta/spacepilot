using SpacePilot.Models;

namespace SpacePilot.ViewModels;

public sealed class ProtectedPathViewModel : ObservableObject
{
    private readonly Action? _selectionChanged;
    private bool _isSelected;

    public ProtectedPathViewModel(ProtectedPathEntry entry, Action? selectionChanged)
    {
        Entry = entry;
        _selectionChanged = selectionChanged;
    }

    public ProtectedPathEntry Entry { get; }
    public string Path => Entry.Path;
    public string Reason => Entry.Reason;
    public DateTime AddedLocal => Entry.AddedLocal;

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
