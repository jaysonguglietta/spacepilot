using SpacePilot.Models;
using System.IO;

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
    public bool HasPayload => File.Exists(Entry.PayloadPath) || Directory.Exists(Entry.PayloadPath);
    public bool CanPurge => !HasPayload || Entry.Kind == CleanupTargetKind.File;
    public string RecoveryStatus => !HasPayload
        ? "Payload missing; check original path"
        : Entry.Kind == CleanupTargetKind.Directory
            ? "Ready to restore; purge unavailable"
            : "Ready to restore";

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
