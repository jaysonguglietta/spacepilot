using SpacePilot.Models;

namespace SpacePilot.ViewModels;

public sealed class BrowserProfileViewModel : ObservableObject
{
    private readonly Action? _selectionChanged;
    private bool _cacheSelected;
    private bool _cookiesSelected;
    private bool _historySelected;
    private bool _sessionsSelected;

    public BrowserProfileViewModel(BrowserProfileInfo profile, Action? selectionChanged)
    {
        Profile = profile;
        _selectionChanged = selectionChanged;
        _cacheSelected = profile.CacheSelected;
        _cookiesSelected = profile.CookiesSelected;
        _historySelected = profile.HistorySelected;
        _sessionsSelected = profile.SessionsSelected;
    }

    public BrowserProfileInfo Profile { get; }
    public string Browser => Profile.Browser;
    public string ProfileName => Profile.ProfileName;
    public string ProfilePath => Profile.ProfilePath;

    public bool CacheSelected
    {
        get => _cacheSelected;
        set => SetSelection(ref _cacheSelected, value);
    }

    public bool CookiesSelected
    {
        get => _cookiesSelected;
        set => SetSelection(ref _cookiesSelected, value);
    }

    public bool HistorySelected
    {
        get => _historySelected;
        set => SetSelection(ref _historySelected, value);
    }

    public bool SessionsSelected
    {
        get => _sessionsSelected;
        set => SetSelection(ref _sessionsSelected, value);
    }

    public bool HasAnySelection => CacheSelected || CookiesSelected || HistorySelected || SessionsSelected;

    private void SetSelection(ref bool field, bool value)
    {
        if (SetProperty(ref field, value))
        {
            OnPropertyChanged(nameof(HasAnySelection));
            _selectionChanged?.Invoke();
        }
    }
}
