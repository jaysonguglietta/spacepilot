using SpacePilot.Models;

namespace SpacePilot.ViewModels;

public sealed class DuplicateFileViewModel : ObservableObject
{
    private readonly Action<DuplicateFileViewModel>? _selectionChanged;
    private bool _isSelected;
    private bool _isKeeper;

    public DuplicateFileViewModel(DuplicateFileInfo file, Action<DuplicateFileViewModel>? selectionChanged)
    {
        File = file;
        _selectionChanged = selectionChanged;
    }

    public DuplicateFileInfo File { get; }
    public string GroupId => File.GroupId;
    public string Path => File.Path;
    public string Name => File.Name;
    public string Directory => File.Directory;
    public long SizeBytes => File.SizeBytes;
    public DateTime? LastModifiedLocal => File.LastModifiedLocal;
    public bool IsRecommendedForCleanup => File.IsRecommendedForCleanup;
    public string Recommendation => IsKeeper ? "Chosen keeper; this copy cannot be quarantined." : "Choose one keeper for this set, then select only extra copies.";

    public bool IsKeeper
    {
        get => _isKeeper;
        set
        {
            if (SetProperty(ref _isKeeper, value))
            {
                if (value)
                {
                    IsSelected = false;
                }
                OnPropertyChanged(nameof(CanSelectForQuarantine));
                OnPropertyChanged(nameof(Recommendation));
                _selectionChanged?.Invoke(this);
            }
        }
    }

    public bool CanSelectForQuarantine => !IsKeeper;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (value && IsKeeper)
            {
                return;
            }
            if (SetProperty(ref _isSelected, value))
            {
                _selectionChanged?.Invoke(this);
            }
        }
    }
}
