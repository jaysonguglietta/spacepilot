using SpacePilot.Models;

namespace SpacePilot.ViewModels;

public sealed class CleanupCategoryViewModel : ObservableObject
{
    private bool _isSelected;
    private int _candidateCount;
    private long _lastScanBytes;

    public CleanupCategoryViewModel(CleanupRule rule, bool isSelected, Action<CleanupCategoryViewModel>? selectionChanged = null)
    {
        Rule = rule;
        _isSelected = isSelected;
        SelectionChanged = selectionChanged;
    }

    public CleanupRule Rule { get; }
    public string Id => Rule.Id;
    public string Name => Rule.Name;
    public string Description => Rule.Description;
    public RiskLevel Risk => Rule.Risk;
    private Action<CleanupCategoryViewModel>? SelectionChanged { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value))
            {
                SelectionChanged?.Invoke(this);
            }
        }
    }

    public int CandidateCount
    {
        get => _candidateCount;
        set => SetProperty(ref _candidateCount, value);
    }

    public long LastScanBytes
    {
        get => _lastScanBytes;
        set => SetProperty(ref _lastScanBytes, value);
    }
}
