using SpacePilot.Models;

namespace SpacePilot.ViewModels;

public sealed class CleanupCandidateViewModel : ObservableObject
{
    private readonly Action? _selectionChanged;
    private bool _isSelected;

    public CleanupCandidateViewModel(CleanupCandidate candidate, bool isSelected, Action? selectionChanged)
    {
        Candidate = candidate;
        _isSelected = isSelected;
        _selectionChanged = selectionChanged;
    }

    public CleanupCandidate Candidate { get; }
    public string CategoryId => Candidate.CategoryId;
    public string CategoryName => Candidate.CategoryName;
    public string DisplayName => Candidate.DisplayName;
    public string Path => Candidate.Path;
    public CleanupTargetKind Kind => Candidate.Kind;
    public long SizeBytes => Candidate.SizeBytes;
    public DateTime? LastModifiedLocal => Candidate.LastModifiedLocal;
    public RiskLevel Risk => Candidate.Risk;

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
