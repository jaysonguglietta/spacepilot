using SpacePilot.Models;

namespace SpacePilot.ViewModels;

public sealed class QuarantineEntryViewModel : ObservableObject
{
    private readonly Action? _selectionChanged;
    private bool _isSelected;

    public QuarantineEntryViewModel(QuarantineEntry entry, Action? selectionChanged)
    {
        Entry = entry;
        _selectionChanged = selectionChanged;
    }

    public QuarantineEntry Entry { get; }
    public string Id => Entry.Id;
    public string DisplayName => Entry.DisplayName;
    public string OriginalPath => Entry.OriginalPath;
    public string CategoryName => Entry.CategoryName;
    public long SizeBytes => Entry.SizeBytes;
    public DateTime QuarantinedLocal => Entry.QuarantinedLocal;

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
