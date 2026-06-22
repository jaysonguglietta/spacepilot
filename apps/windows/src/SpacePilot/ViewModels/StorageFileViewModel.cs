using SpacePilot.Models;

namespace SpacePilot.ViewModels;

public sealed class StorageFileViewModel : ObservableObject
{
    private readonly Action? _selectionChanged;
    private bool _isSelected;

    public StorageFileViewModel(LargeFileInfo file, Action? selectionChanged)
    {
        File = file;
        _selectionChanged = selectionChanged;
    }

    public LargeFileInfo File { get; }
    public string Path => File.Path;
    public string Name => File.Name;
    public string Directory => File.Directory;
    public string Extension => File.Extension;
    public long SizeBytes => File.SizeBytes;
    public DateTime? LastModifiedLocal => File.LastModifiedLocal;
    public string Recommendation => File.Recommendation;

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
