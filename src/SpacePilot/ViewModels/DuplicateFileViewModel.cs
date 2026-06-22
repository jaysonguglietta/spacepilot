using SpacePilot.Models;

namespace SpacePilot.ViewModels;

public sealed class DuplicateFileViewModel : ObservableObject
{
    private readonly Action? _selectionChanged;
    private bool _isSelected;

    public DuplicateFileViewModel(DuplicateFileInfo file, Action? selectionChanged)
    {
        File = file;
        _isSelected = file.IsRecommendedForCleanup;
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
    public string Recommendation => IsRecommendedForCleanup ? "Likely duplicate; keep the newest copy unless you know this path matters." : "Newest copy in this duplicate set.";

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
