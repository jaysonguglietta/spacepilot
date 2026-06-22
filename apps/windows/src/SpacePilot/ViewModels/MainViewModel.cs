using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Data;
using SpacePilot.Models;
using SpacePilot.Services;
using SpacePilot.Utilities;

namespace SpacePilot.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private const string AllCategoriesFilter = "All categories";
    private const string AllRisksFilter = "All risks";

    private readonly IReadOnlyList<CleanupRule> _rules;
    private readonly CleanerService _cleanerService = new();
    private readonly SoftwareInventoryService _softwareInventoryService = new();
    private readonly StartupService _startupService = new();
    private readonly SettingsAuditService _settingsAuditService = new();
    private readonly SystemRestoreService _systemRestoreService = new();
    private readonly QuarantineService _quarantineService = new();
    private readonly CleanupReceiptService _receiptService = new();
    private readonly StorageAnalysisService _storageAnalysisService = new();
    private readonly ScheduledScanService _scheduledScanService = new();
    private readonly WingetService _wingetService = new();
    private readonly BrowserProfileService _browserProfileService = new();
    private readonly ProtectionPolicyService _protectionPolicyService = new();
    private readonly ReclaimPlanService _reclaimPlanService = new();
    private readonly PreferencesService _preferencesService = new();
    private readonly UserPreferences _preferences;
    private string _selectedSection = "Health";
    private bool _isBusy;
    private string _statusMessage = "Ready.";
    private string _busyText = "Idle";
    private string _cleanerSearchText = string.Empty;
    private string _softwareSearchText = string.Empty;
    private string _startupSearchText = string.Empty;
    private string _selectedCategoryFilter = AllCategoriesFilter;
    private string _selectedRiskFilter = AllRisksFilter;
    private string _scheduledScanStatusText = "Checking scheduled scan reminder...";
    private string _newProtectedPath = string.Empty;
    private string _newProtectedPathReason = "User protected";
    private DateTime? _lastScanUtc;
    private bool _isBulkSelecting;

    public MainViewModel()
    {
        _preferences = _preferencesService.Load();
        _rules = CleanupRuleCatalog.CreateDefaultRules();

        foreach (var rule in _rules)
        {
            var selected = GetInitialCategorySelection(rule);
            CleanupCategories.Add(new CleanupCategoryViewModel(rule, selected, OnCleanupCategorySelectionChanged));
            CategoryFilterOptions.Add(rule.Name);
        }

        PreviewEntriesView = CollectionViewSource.GetDefaultView(PreviewEntries);
        PreviewEntriesView.Filter = FilterPreviewEntry;
        PreviewEntriesView.SortDescriptions.Add(new SortDescription(nameof(CleanupCandidateViewModel.SizeBytes), ListSortDirection.Descending));

        InstalledAppsView = CollectionViewSource.GetDefaultView(InstalledApps);
        InstalledAppsView.Filter = FilterInstalledApp;
        InstalledAppsView.SortDescriptions.Add(new SortDescription(nameof(InstalledAppInfo.Name), ListSortDirection.Ascending));

        StartupEntriesView = CollectionViewSource.GetDefaultView(StartupEntries);
        StartupEntriesView.Filter = FilterStartupEntry;
        StartupEntriesView.SortDescriptions.Add(new SortDescription(nameof(StartupEntry.Name), ListSortDirection.Ascending));

        SelectSectionCommand = new RelayCommand(parameter => SelectSection(parameter?.ToString() ?? "Health"));
        ScanCommand = new AsyncRelayCommand(ScanAsync, () => !IsBusy);
        CleanCommand = new AsyncRelayCommand(CleanAsync, () => !IsBusy && SelectedPreviewCount > 0);
        RefreshSoftwareCommand = new AsyncRelayCommand(RefreshSoftwareAsync, () => !IsBusy);
        RefreshStartupCommand = new AsyncRelayCommand(RefreshStartupAsync, () => !IsBusy);
        RefreshSettingsCommand = new AsyncRelayCommand(RefreshSettingsAsync, () => !IsBusy);
        CreateRestorePointCommand = new AsyncRelayCommand(CreateRestorePointAsync, () => !IsBusy);
        DismissFirstRunCommand = new RelayCommand(_ => DismissFirstRun());
        SelectAllPreviewCommand = new RelayCommand(_ => SetPreviewSelection(isSelected: true, onlyFiltered: false));
        SelectVisiblePreviewCommand = new RelayCommand(_ => SetPreviewSelection(isSelected: true, onlyFiltered: true));
        ClearPreviewSelectionCommand = new RelayCommand(_ => SetPreviewSelection(isSelected: false, onlyFiltered: false));
        ClearActivityCommand = new RelayCommand(_ => ActivityLog.Clear());
        OpenAppsSettingsCommand = new RelayCommand(_ => OpenUri("ms-settings:appsfeatures"));
        OpenStartupSettingsCommand = new RelayCommand(_ => OpenUri("ms-settings:startupapps"));
        OpenStorageSettingsCommand = new RelayCommand(_ => OpenUri("ms-settings:storagesense"));
        OpenDiskCleanupCommand = new RelayCommand(_ => OpenProcess("cleanmgr.exe"));
        OpenRecycleBinCommand = new RelayCommand(_ => OpenUri("shell:RecycleBinFolder"));
        ScanLargeFilesCommand = new AsyncRelayCommand(ScanLargeFilesAsync, () => !IsBusy);
        ScanDuplicatesCommand = new AsyncRelayCommand(ScanDuplicatesAsync, () => !IsBusy);
        QuarantineSelectedLargeFilesCommand = new AsyncRelayCommand(QuarantineSelectedLargeFilesAsync, () => !IsBusy && SelectedLargeFileCount > 0);
        QuarantineSelectedDuplicatesCommand = new AsyncRelayCommand(QuarantineSelectedDuplicatesAsync, () => !IsBusy && SelectedDuplicateCount > 0);
        RefreshRecoveryCommand = new AsyncRelayCommand(RefreshRecoveryAsync, () => !IsBusy);
        RestoreSelectedQuarantineCommand = new AsyncRelayCommand(RestoreSelectedQuarantineAsync, () => !IsBusy && SelectedQuarantineCount > 0);
        PurgeSelectedQuarantineCommand = new AsyncRelayCommand(PurgeSelectedQuarantineAsync, () => !IsBusy && SelectedQuarantineCount > 0);
        PurgeAllQuarantineCommand = new AsyncRelayCommand(PurgeAllQuarantineAsync, () => !IsBusy && QuarantineEntries.Count > 0);
        ExportLatestReceiptCommand = new AsyncRelayCommand(ExportLatestReceiptAsync, () => !IsBusy && CleanupReceipts.Count > 0);
        EnableWeeklyScanReminderCommand = new AsyncRelayCommand(EnableWeeklyScanReminderAsync, () => !IsBusy);
        DisableWeeklyScanReminderCommand = new AsyncRelayCommand(DisableWeeklyScanReminderAsync, () => !IsBusy);
        RefreshWingetCommand = new AsyncRelayCommand(RefreshWingetAsync, () => !IsBusy);
        UpgradeSelectedPackagesCommand = new AsyncRelayCommand(UpgradeSelectedPackagesAsync, () => !IsBusy && SelectedWingetPackageCount > 0);
        ExportWingetPackagesCommand = new AsyncRelayCommand(ExportWingetPackagesAsync, () => !IsBusy);
        ImportWingetPackagesCommand = new AsyncRelayCommand(ImportWingetPackagesAsync, () => !IsBusy);
        ScanStorageMapCommand = new AsyncRelayCommand(ScanStorageMapAsync, () => !IsBusy);
        RefreshBrowserProfilesCommand = new AsyncRelayCommand(RefreshBrowserProfilesAsync, () => !IsBusy);
        CleanSelectedBrowserDataCommand = new AsyncRelayCommand(CleanSelectedBrowserDataAsync, () => !IsBusy && SelectedBrowserProfileCount > 0);
        AddProtectedPathCommand = new RelayCommand(_ => AddProtectedPath(), _ => !string.IsNullOrWhiteSpace(NewProtectedPath));
        RemoveSelectedProtectedPathsCommand = new RelayCommand(_ => RemoveSelectedProtectedPaths(), _ => ProtectedPaths.Any(path => path.IsSelected));

        ActivityLog.Insert(0, new ActivityLogEntry(DateTime.UtcNow, "Info", "SpacePilot started."));
        _ = LoadInitialAsync();
    }

    public ObservableCollection<CleanupCategoryViewModel> CleanupCategories { get; } = [];
    public ObservableCollection<CleanupCandidateViewModel> PreviewEntries { get; } = [];
    public ObservableCollection<InstalledAppInfo> InstalledApps { get; } = [];
    public ObservableCollection<StartupEntry> StartupEntries { get; } = [];
    public ObservableCollection<SettingsCheck> SettingsChecks { get; } = [];
    public ObservableCollection<ActivityLogEntry> ActivityLog { get; } = [];
    public ObservableCollection<StorageFileViewModel> LargeFiles { get; } = [];
    public ObservableCollection<DuplicateFileViewModel> DuplicateFiles { get; } = [];
    public ObservableCollection<QuarantineEntryViewModel> QuarantineEntries { get; } = [];
    public ObservableCollection<CleanupReceipt> CleanupReceipts { get; } = [];
    public ObservableCollection<WingetPackageViewModel> WingetPackages { get; } = [];
    public ObservableCollection<FolderUsageInfo> FolderUsage { get; } = [];
    public ObservableCollection<FileTypeUsageInfo> FileTypeUsage { get; } = [];
    public ObservableCollection<BrowserProfileViewModel> BrowserProfiles { get; } = [];
    public ObservableCollection<ProtectedPathViewModel> ProtectedPaths { get; } = [];
    public ObservableCollection<ReclaimPlanItem> ReclaimPlan { get; } = [];
    public ObservableCollection<string> CategoryFilterOptions { get; } = [AllCategoriesFilter];
    public ObservableCollection<string> RiskFilterOptions { get; } = [AllRisksFilter, "Low", "Medium", "High"];

    public ICollectionView PreviewEntriesView { get; }
    public ICollectionView InstalledAppsView { get; }
    public ICollectionView StartupEntriesView { get; }

    public RelayCommand SelectSectionCommand { get; }
    public AsyncRelayCommand ScanCommand { get; }
    public AsyncRelayCommand CleanCommand { get; }
    public AsyncRelayCommand RefreshSoftwareCommand { get; }
    public AsyncRelayCommand RefreshStartupCommand { get; }
    public AsyncRelayCommand RefreshSettingsCommand { get; }
    public AsyncRelayCommand CreateRestorePointCommand { get; }
    public RelayCommand DismissFirstRunCommand { get; }
    public RelayCommand SelectAllPreviewCommand { get; }
    public RelayCommand SelectVisiblePreviewCommand { get; }
    public RelayCommand ClearPreviewSelectionCommand { get; }
    public RelayCommand ClearActivityCommand { get; }
    public RelayCommand OpenAppsSettingsCommand { get; }
    public RelayCommand OpenStartupSettingsCommand { get; }
    public RelayCommand OpenStorageSettingsCommand { get; }
    public RelayCommand OpenDiskCleanupCommand { get; }
    public RelayCommand OpenRecycleBinCommand { get; }
    public AsyncRelayCommand ScanLargeFilesCommand { get; }
    public AsyncRelayCommand ScanDuplicatesCommand { get; }
    public AsyncRelayCommand QuarantineSelectedLargeFilesCommand { get; }
    public AsyncRelayCommand QuarantineSelectedDuplicatesCommand { get; }
    public AsyncRelayCommand RefreshRecoveryCommand { get; }
    public AsyncRelayCommand RestoreSelectedQuarantineCommand { get; }
    public AsyncRelayCommand PurgeSelectedQuarantineCommand { get; }
    public AsyncRelayCommand PurgeAllQuarantineCommand { get; }
    public AsyncRelayCommand ExportLatestReceiptCommand { get; }
    public AsyncRelayCommand EnableWeeklyScanReminderCommand { get; }
    public AsyncRelayCommand DisableWeeklyScanReminderCommand { get; }
    public AsyncRelayCommand RefreshWingetCommand { get; }
    public AsyncRelayCommand UpgradeSelectedPackagesCommand { get; }
    public AsyncRelayCommand ExportWingetPackagesCommand { get; }
    public AsyncRelayCommand ImportWingetPackagesCommand { get; }
    public AsyncRelayCommand ScanStorageMapCommand { get; }
    public AsyncRelayCommand RefreshBrowserProfilesCommand { get; }
    public AsyncRelayCommand CleanSelectedBrowserDataCommand { get; }
    public RelayCommand AddProtectedPathCommand { get; }
    public RelayCommand RemoveSelectedProtectedPathsCommand { get; }

    public string SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (SetProperty(ref _selectedSection, value))
            {
                OnPropertyChanged(nameof(SelectedSectionTitle));
                OnPropertyChanged(nameof(SelectedSectionSubtitle));
            }
        }
    }

    public string SelectedSectionTitle => SelectedSection switch
    {
        "Cleaner" => "Cleaner",
        "Storage" => "Storage analyzer",
        "Browsers" => "Browser cleanup",
        "Software" => "Software maintenance",
        "Startup" => "Startup apps",
        "Recovery" => "Recovery",
        "Settings" => "Settings health",
        "Activity" => "Activity",
        _ => "System health"
    };

    public string SelectedSectionSubtitle => SelectedSection switch
    {
        "Cleaner" => "Scan, filter, review, and remove only the cleanup items you approve.",
        "Storage" => "Find large files and verified duplicates that can free real disk space.",
        "Browsers" => "Clean browser cache by default and opt into cookies, history, or sessions per profile.",
        "Software" => "Review installed apps, check WinGet updates, and export or import app lists.",
        "Startup" => "See what launches when Windows starts and jump to Windows controls.",
        "Recovery" => "Restore quarantined files, purge quarantine, and export cleanup receipts.",
        "Settings" => "Tune cleaner safety preferences and check Windows maintenance settings.",
        "Activity" => "Review scan, cleanup, inventory, and warning events from this session.",
        _ => "A quick snapshot of cleanup estimates, inventory, and safety status."
    };

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string BusyText
    {
        get => _busyText;
        set => SetProperty(ref _busyText, value);
    }

    public string CleanerSearchText
    {
        get => _cleanerSearchText;
        set
        {
            if (SetProperty(ref _cleanerSearchText, value))
            {
                RefreshPreviewFilter();
            }
        }
    }

    public string SoftwareSearchText
    {
        get => _softwareSearchText;
        set
        {
            if (SetProperty(ref _softwareSearchText, value))
            {
                InstalledAppsView.Refresh();
                OnPropertyChanged(nameof(FilteredSoftwareCount));
                OnPropertyChanged(nameof(HasNoInstalledApps));
            }
        }
    }

    public string StartupSearchText
    {
        get => _startupSearchText;
        set
        {
            if (SetProperty(ref _startupSearchText, value))
            {
                StartupEntriesView.Refresh();
                OnPropertyChanged(nameof(FilteredStartupCount));
                OnPropertyChanged(nameof(HasNoStartupEntries));
            }
        }
    }

    public string SelectedCategoryFilter
    {
        get => _selectedCategoryFilter;
        set
        {
            if (SetProperty(ref _selectedCategoryFilter, value))
            {
                RefreshPreviewFilter();
            }
        }
    }

    public string SelectedRiskFilter
    {
        get => _selectedRiskFilter;
        set
        {
            if (SetProperty(ref _selectedRiskFilter, value))
            {
                RefreshPreviewFilter();
            }
        }
    }

    public bool ConfirmBeforeCleanup
    {
        get => _preferences.ConfirmBeforeCleanup;
        set
        {
            if (_preferences.ConfirmBeforeCleanup == value)
            {
                return;
            }

            _preferences.ConfirmBeforeCleanup = value;
            SavePreferences();
            OnPropertyChanged();
            OnPropertyChanged(nameof(SafetyPreferenceSummary));
        }
    }

    public bool RemindRestorePointBeforeCleanup
    {
        get => _preferences.RemindRestorePointBeforeCleanup;
        set
        {
            if (_preferences.RemindRestorePointBeforeCleanup == value)
            {
                return;
            }

            _preferences.RemindRestorePointBeforeCleanup = value;
            SavePreferences();
            OnPropertyChanged();
            OnPropertyChanged(nameof(SafetyPreferenceSummary));
        }
    }

    public bool UseQuarantine
    {
        get => _preferences.UseQuarantine;
        set
        {
            if (_preferences.UseQuarantine == value)
            {
                return;
            }

            _preferences.UseQuarantine = value;
            SavePreferences();
            OnPropertyChanged();
            OnPropertyChanged(nameof(SafetyPreferenceSummary));
        }
    }

    public bool SelectMediumRiskByDefault
    {
        get => _preferences.SelectMediumRiskByDefault;
        set
        {
            if (_preferences.SelectMediumRiskByDefault == value)
            {
                return;
            }

            _preferences.SelectMediumRiskByDefault = value;
            SavePreferences();
            OnPropertyChanged();
            OnPropertyChanged(nameof(SafetyPreferenceSummary));
        }
    }

    public bool BrowserCacheSelected
    {
        get => _preferences.BrowserCacheSelected;
        set
        {
            if (_preferences.BrowserCacheSelected == value)
            {
                return;
            }

            _preferences.BrowserCacheSelected = value;
            SavePreferences();
            OnPropertyChanged();
        }
    }

    public bool BrowserCookiesSelected
    {
        get => _preferences.BrowserCookiesSelected;
        set
        {
            if (_preferences.BrowserCookiesSelected == value)
            {
                return;
            }

            _preferences.BrowserCookiesSelected = value;
            SavePreferences();
            OnPropertyChanged();
        }
    }

    public bool BrowserHistorySelected
    {
        get => _preferences.BrowserHistorySelected;
        set
        {
            if (_preferences.BrowserHistorySelected == value)
            {
                return;
            }

            _preferences.BrowserHistorySelected = value;
            SavePreferences();
            OnPropertyChanged();
        }
    }

    public bool BrowserSessionsSelected
    {
        get => _preferences.BrowserSessionsSelected;
        set
        {
            if (_preferences.BrowserSessionsSelected == value)
            {
                return;
            }

            _preferences.BrowserSessionsSelected = value;
            SavePreferences();
            OnPropertyChanged();
        }
    }

    public string NewProtectedPath
    {
        get => _newProtectedPath;
        set
        {
            if (SetProperty(ref _newProtectedPath, value))
            {
                AddProtectedPathCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string NewProtectedPathReason
    {
        get => _newProtectedPathReason;
        set => SetProperty(ref _newProtectedPathReason, value);
    }

    public string ProtectedExtensionsText
    {
        get => string.Join(", ", _preferences.ProtectedExtensions);
        set
        {
            var extensions = value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(extension => extension.StartsWith('.') ? extension : "." + extension)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            _preferences.ProtectedExtensions = extensions;
            SavePreferences();
            OnPropertyChanged();
        }
    }

    public int QuarantineRetentionDays
    {
        get => _preferences.QuarantineRetentionDays;
        set
        {
            var normalized = Math.Clamp(value, 1, 365);
            if (_preferences.QuarantineRetentionDays == normalized)
            {
                return;
            }

            _preferences.QuarantineRetentionDays = normalized;
            SavePreferences();
            OnPropertyChanged();
            OnPropertyChanged(nameof(SafetyPreferenceSummary));
        }
    }

    public int LargeFileMinimumMb
    {
        get => _preferences.LargeFileMinimumMb;
        set
        {
            var normalized = Math.Clamp(value, 10, 102400);
            if (_preferences.LargeFileMinimumMb == normalized)
            {
                return;
            }

            _preferences.LargeFileMinimumMb = normalized;
            SavePreferences();
            OnPropertyChanged();
        }
    }

    public int DuplicateMinimumMb
    {
        get => _preferences.DuplicateMinimumMb;
        set
        {
            var normalized = Math.Clamp(value, 1, 102400);
            if (_preferences.DuplicateMinimumMb == normalized)
            {
                return;
            }

            _preferences.DuplicateMinimumMb = normalized;
            SavePreferences();
            OnPropertyChanged();
        }
    }

    public bool ShowFirstRun => _preferences.IsFirstRun;
    public bool HasPreviewEntries => PreviewEntries.Count > 0;
    public bool HasNoPreviewEntries => PreviewEntries.Count == 0 || FilteredPreviewCount == 0;
    public bool HasNoInstalledApps => InstalledApps.Count == 0 || FilteredSoftwareCount == 0;
    public bool HasNoStartupEntries => StartupEntries.Count == 0 || FilteredStartupCount == 0;
    public int FilteredPreviewCount => PreviewEntriesView.Cast<CleanupCandidateViewModel>().Count();
    public int FilteredSoftwareCount => InstalledAppsView.Cast<InstalledAppInfo>().Count();
    public int FilteredStartupCount => StartupEntriesView.Cast<StartupEntry>().Count();
    public int SelectedPreviewCount => PreviewEntries.Count(entry => entry.IsSelected);
    public int SelectedLargeFileCount => LargeFiles.Count(entry => entry.IsSelected);
    public int SelectedDuplicateCount => DuplicateFiles.Count(entry => entry.IsSelected);
    public int SelectedQuarantineCount => QuarantineEntries.Count(entry => entry.IsSelected);
    public int SelectedWingetPackageCount => WingetPackages.Count(package => package.IsSelected);
    public int WingetUpdateCount => WingetPackages.Count;
    public int SelectedBrowserProfileCount => BrowserProfiles.Count(profile => profile.HasAnySelection);
    public int ProtectedPathCount => ProtectedPaths.Count;
    public long TotalScannedBytes => PreviewEntries.Sum(entry => entry.SizeBytes);
    public long TotalSelectedBytes => PreviewEntries.Where(entry => entry.IsSelected).Sum(entry => entry.SizeBytes);
    public long SelectedLargeFileBytes => LargeFiles.Where(entry => entry.IsSelected).Sum(entry => entry.SizeBytes);
    public long SelectedDuplicateBytes => DuplicateFiles.Where(entry => entry.IsSelected).Sum(entry => entry.SizeBytes);
    public long QuarantineBytes => QuarantineEntries.Sum(entry => entry.SizeBytes);
    public long SelectedQuarantineBytes => QuarantineEntries.Where(entry => entry.IsSelected).Sum(entry => entry.SizeBytes);
    public bool HasLargeFiles => LargeFiles.Count > 0;
    public bool HasDuplicateFiles => DuplicateFiles.Count > 0;
    public bool HasQuarantineEntries => QuarantineEntries.Count > 0;
    public bool HasFolderUsage => FolderUsage.Count > 0;
    public bool HasFileTypeUsage => FileTypeUsage.Count > 0;
    public bool HasWingetUpdates => WingetPackages.Count > 0;
    public bool HasBrowserProfiles => BrowserProfiles.Count > 0;

    public string LastScanText => _lastScanUtc is null
        ? "No scan yet"
        : $"Last scan {_lastScanUtc.Value.ToLocalTime():g}";

    public string LastCleanupText => _preferences.LastCleanupUtc is null
        ? "No cleanup run yet"
        : $"Last cleanup {_preferences.LastCleanupUtc.Value.ToLocalTime():g}";

    public string SelectedCategorySummary
    {
        get
        {
            var selectedCategories = CleanupCategories.Count(category => category.IsSelected);
            return $"{selectedCategories} of {CleanupCategories.Count} categories enabled";
        }
    }

    public string SelectedPreviewSummary
    {
        get
        {
            if (PreviewEntries.Count == 0)
            {
                return "Run a scan to review cleanup items.";
            }

            return $"{SelectedPreviewCount} of {PreviewEntries.Count} items selected";
        }
    }

    public string CleanReadinessMessage => SelectedPreviewCount == 0
        ? "Select cleanup items before running cleanup."
        : $"{SelectedPreviewCount} items selected, estimated {Formatters.FormatBytes(TotalSelectedBytes)}.";

    public string StorageSummary => LargeFiles.Count == 0 && DuplicateFiles.Count == 0
        ? "Run storage scans to map folders, find large files, and identify verified duplicates."
        : $"{LargeFiles.Count} large files, {DuplicateFiles.Count} duplicate file entries, {FolderUsage.Count} folder summaries.";

    public string StorageMapSummary => FolderUsage.Count == 0
        ? "Run storage map to see top folders and file-type usage."
        : $"{FolderUsage.Count} folders mapped, {FileTypeUsage.Count} file types summarized.";

    public string DuplicateSelectionSummary => SelectedDuplicateCount == 0
        ? "Recommended duplicates are selected after a duplicate scan."
        : $"{SelectedDuplicateCount} duplicate files selected, {Formatters.FormatBytes(SelectedDuplicateBytes)} reclaimable after purge.";

    public string LargeFileSelectionSummary => SelectedLargeFileCount == 0
        ? "Select large files only after manual review."
        : $"{SelectedLargeFileCount} large files selected, {Formatters.FormatBytes(SelectedLargeFileBytes)} targeted.";

    public string QuarantineSummary => QuarantineEntries.Count == 0
        ? "Quarantine is empty."
        : $"{QuarantineEntries.Count} items, {Formatters.FormatBytes(QuarantineBytes)} waiting for restore or purge.";

    public string SelectedQuarantineSummary => SelectedQuarantineCount == 0
        ? "Select quarantined items to restore or purge."
        : $"{SelectedQuarantineCount} selected, {Formatters.FormatBytes(SelectedQuarantineBytes)}.";

    public string InventorySummary => $"{InstalledApps.Count} apps / {StartupEntries.Count} startup";
    public string WingetSummary => WingetPackages.Count == 0
        ? "No WinGet updates loaded."
        : $"{WingetPackages.Count} updates available, {SelectedWingetPackageCount} selected.";

    public string BrowserProfileSummary => BrowserProfiles.Count == 0
        ? "Discover browser profiles before cleaning browser data."
        : $"{BrowserProfiles.Count} profiles discovered, {SelectedBrowserProfileCount} profiles selected.";

    public string ScheduledScanStatusText
    {
        get => _scheduledScanStatusText;
        set => SetProperty(ref _scheduledScanStatusText, value);
    }

    public string SafetyPreferenceSummary
    {
        get
        {
            var confirmation = ConfirmBeforeCleanup ? "confirmation on" : "confirmation off";
            var restore = RemindRestorePointBeforeCleanup ? "restore reminders on" : "restore reminders off";
            var quarantine = UseQuarantine ? $"quarantine {QuarantineRetentionDays} days" : "permanent deletion";
            return $"{confirmation}, {restore}, {quarantine}";
        }
    }

    private async Task LoadInitialAsync()
    {
        await RunBusyAsync("Loading Windows inventory...", async () =>
        {
            var purged = await _quarantineService.PurgeExpiredAsync(QuarantineRetentionDays);
            if (purged > 0)
            {
                AddActivity("Info", $"Purged {purged} expired quarantine items.");
            }

            await RefreshSettingsCoreAsync();
            await RefreshSoftwareCoreAsync();
            await RefreshStartupCoreAsync();
            await RefreshRecoveryCoreAsync();
            await RefreshBrowserProfilesCoreAsync();
            await RefreshScheduledScanStatusAsync();
            RefreshProtectedPaths();
            RefreshReclaimPlan();
            StatusMessage = "Initial inventory loaded.";
        });
    }

    private void SelectSection(string section)
    {
        SelectedSection = section;
    }

    private async Task ScanAsync()
    {
        await RunBusyAsync("Scanning cleanup locations...", async () =>
        {
            var result = await _cleanerService.ScanAsync(_rules);
            PreviewEntries.Clear();

            foreach (var candidate in result.Candidates.OrderByDescending(candidate => candidate.SizeBytes))
            {
                var category = CleanupCategories.FirstOrDefault(item => item.Id == candidate.CategoryId);
                PreviewEntries.Add(new CleanupCandidateViewModel(
                    candidate,
                    category?.IsSelected == true,
                    RefreshSelectionSummaries));
            }

            RefreshCategoryScanSummaries();
            _lastScanUtc = DateTime.UtcNow;
            PreviewEntriesView.Refresh();
            RefreshSelectionSummaries();
            OnPropertyChanged(nameof(LastScanText));
            OnPropertyChanged(nameof(HasPreviewEntries));
            OnPropertyChanged(nameof(HasNoPreviewEntries));
            OnPropertyChanged(nameof(FilteredPreviewCount));

            AddActivity("Info", $"Scan found {PreviewEntries.Count} cleanup candidates totaling {Formatters.FormatBytes(result.TotalBytes)}.");
            foreach (var warning in result.Warnings.Take(8))
            {
                AddActivity("Warn", warning);
            }

            if (result.Warnings.Count > 8)
            {
                AddActivity("Warn", $"{result.Warnings.Count - 8} additional scan warnings were hidden.");
            }

            SelectedSection = "Cleaner";
            StatusMessage = $"Scan complete: {Formatters.FormatBytes(result.TotalBytes)} found.";
        });
    }

    private async Task CleanAsync()
    {
        var selectedCandidates = PreviewEntries
            .Where(entry => entry.IsSelected)
            .Select(entry => entry.Candidate)
            .ToList();

        if (selectedCandidates.Count == 0)
        {
            MessageBox.Show("Run a scan and select at least one cleanup item.", "Nothing selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await RunCandidateCleanupAsync(
            selectedCandidates,
            "Cleanup scan",
            completedPaths =>
            {
                foreach (var candidate in PreviewEntries.Where(candidate => completedPaths.Contains(candidate.Path)).ToList())
                {
                    PreviewEntries.Remove(candidate);
                }

                RefreshCategoryScanSummaries();
                PreviewEntriesView.Refresh();
                OnPropertyChanged(nameof(HasPreviewEntries));
                OnPropertyChanged(nameof(HasNoPreviewEntries));
                OnPropertyChanged(nameof(FilteredPreviewCount));
            });
    }

    private async Task RunCandidateCleanupAsync(
        IReadOnlyList<CleanupCandidate> selectedCandidates,
        string source,
        Action<HashSet<string>> removeCompleted)
    {
        var protectedCandidates = selectedCandidates
            .Where(candidate => _protectionPolicyService.IsProtected(candidate.Path, _preferences))
            .ToList();
        var allowedCandidates = selectedCandidates
            .Where(candidate => !_protectionPolicyService.IsProtected(candidate.Path, _preferences))
            .ToList();

        foreach (var candidate in protectedCandidates.Take(12))
        {
            AddActivity("Warn", $"Protected item skipped: {candidate.Path}");
        }

        if (protectedCandidates.Count > 12)
        {
            AddActivity("Warn", $"{protectedCandidates.Count - 12} additional protected items were skipped.");
        }

        if (allowedCandidates.Count == 0)
        {
            MessageBox.Show("Every selected item is protected by path or extension policy.", "Nothing can be cleaned", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!ConfirmRestorePointReminder(allowedCandidates))
        {
            AddActivity("Info", "Cleanup paused before deletion.");
            return;
        }

        var selectedBytes = allowedCandidates.Sum(candidate => candidate.SizeBytes);
        if (ConfirmBeforeCleanup)
        {
            var action = UseQuarantine ? "Move" : "Permanently delete";
            var confirmation = MessageBox.Show(
                $"{action} {allowedCandidates.Count} selected items totaling {Formatters.FormatBytes(selectedBytes)}?",
                "Confirm cleanup",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmation != MessageBoxResult.Yes)
            {
                AddActivity("Info", "Cleanup cancelled by user.");
                return;
            }
        }

        await RunBusyAsync(UseQuarantine ? "Moving selected items to quarantine..." : "Deleting selected cleanup items...", async () =>
        {
            var freeSpaceBefore = GetSystemDriveFreeBytes();
            var restoreStatus = await _systemRestoreService.GetStatusAsync();
            var completedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var warnings = protectedCandidates
                .Select(candidate => $"Protected item skipped: {candidate.Path}")
                .ToList();
            var completedCount = 0;
            var completedBytes = 0L;
            var receiptItems = new List<CleanupReceiptItem>();

            if (UseQuarantine)
            {
                var result = await _quarantineService.QuarantineAsync(allowedCandidates);
                completedCount = result.QuarantinedCount;
                completedBytes = result.QuarantinedBytes;
                warnings.AddRange(result.Warnings);

                foreach (var entry in result.Entries)
                {
                    completedPaths.Add(entry.OriginalPath);
                    receiptItems.Add(new CleanupReceiptItem
                    {
                        Path = entry.OriginalPath,
                        CategoryName = entry.CategoryName,
                        Action = "Quarantined",
                        SizeBytes = entry.SizeBytes,
                        QuarantineId = entry.Id
                    });
                }
            }
            else
            {
                var result = await _cleanerService.CleanAsync(allowedCandidates);
                completedCount = result.DeletedCount;
                completedBytes = result.DeletedBytes;
                warnings.AddRange(result.Warnings);
                completedPaths = result.DeletedPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var candidate in allowedCandidates.Where(candidate => completedPaths.Contains(candidate.Path)))
                {
                    receiptItems.Add(new CleanupReceiptItem
                    {
                        Path = candidate.Path,
                        CategoryName = candidate.CategoryName,
                        Action = "Deleted",
                        SizeBytes = candidate.SizeBytes
                    });
                }
            }

            _preferences.LastCleanupUtc = DateTime.UtcNow;
            SavePreferences();
            removeCompleted(completedPaths);
            RefreshSelectionSummaries();
            OnPropertyChanged(nameof(LastCleanupText));

            var receipt = new CleanupReceipt
            {
                Id = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}",
                TimestampUtc = DateTime.UtcNow,
                Mode = UseQuarantine ? "Quarantine" : "Permanent delete",
                RequestedCount = allowedCandidates.Count,
                CompletedCount = completedCount,
                RequestedBytes = selectedBytes,
                CompletedBytes = completedBytes,
                FreeSpaceBeforeBytes = freeSpaceBefore,
                FreeSpaceAfterBytes = GetSystemDriveFreeBytes(),
                RestorePointAvailable = restoreStatus.IsAvailable,
                RestorePointMessage = restoreStatus.Message,
                QuarantineExpiresUtc = UseQuarantine ? DateTime.UtcNow.AddDays(QuarantineRetentionDays) : null,
                Items = receiptItems,
                Warnings = warnings
            };
            await _receiptService.SaveAsync(receipt);
            await RefreshRecoveryCoreAsync();
            RefreshReclaimPlan();

            var verb = UseQuarantine ? "Moved to quarantine" : "Deleted";
            AddActivity("Info", $"{verb}: {completedCount} items totaling {Formatters.FormatBytes(completedBytes)} from {source}.");

            foreach (var warning in warnings.Take(12))
            {
                AddActivity("Warn", warning);
            }

            if (warnings.Count > 12)
            {
                AddActivity("Warn", $"{warnings.Count - 12} additional cleanup warnings were hidden.");
            }

            StatusMessage = UseQuarantine
                ? $"Cleanup complete: {Formatters.FormatBytes(completedBytes)} moved to quarantine. Purge quarantine to free disk space."
                : $"Cleanup complete: {Formatters.FormatBytes(completedBytes)} recovered.";
        });
    }

    private async Task RefreshSoftwareAsync()
    {
        await RunBusyAsync("Reading installed software inventory...", RefreshSoftwareCoreAsync);
    }

    private async Task RefreshStartupAsync()
    {
        await RunBusyAsync("Reading startup entries...", RefreshStartupCoreAsync);
    }

    private async Task RefreshSettingsAsync()
    {
        await RunBusyAsync("Checking Windows settings...", RefreshSettingsCoreAsync);
    }

    private async Task RefreshSoftwareCoreAsync()
    {
        var apps = await _softwareInventoryService.GetInstalledAppsAsync();
        InstalledApps.Clear();
        foreach (var app in apps)
        {
            InstalledApps.Add(app);
        }

        InstalledAppsView.Refresh();
        OnPropertyChanged(nameof(InventorySummary));
        OnPropertyChanged(nameof(FilteredSoftwareCount));
        OnPropertyChanged(nameof(HasNoInstalledApps));
        AddActivity("Info", $"Software inventory loaded with {InstalledApps.Count} applications.");
        StatusMessage = "Software inventory refreshed.";
    }

    private async Task RefreshStartupCoreAsync()
    {
        var entries = await _startupService.GetStartupEntriesAsync();
        StartupEntries.Clear();
        foreach (var entry in entries)
        {
            StartupEntries.Add(entry);
        }

        StartupEntriesView.Refresh();
        OnPropertyChanged(nameof(InventorySummary));
        OnPropertyChanged(nameof(FilteredStartupCount));
        OnPropertyChanged(nameof(HasNoStartupEntries));
        AddActivity("Info", $"Startup inventory loaded with {StartupEntries.Count} entries.");
        StatusMessage = "Startup inventory refreshed.";
    }

    private async Task RefreshSettingsCoreAsync()
    {
        var checks = await _settingsAuditService.GetChecksAsync();
        SettingsChecks.Clear();
        foreach (var check in checks)
        {
            SettingsChecks.Add(check);
        }

        AddActivity("Info", $"Settings checks refreshed with {SettingsChecks.Count} results.");
        StatusMessage = "Settings checks refreshed.";
    }

    private async Task ScanLargeFilesAsync()
    {
        await RunBusyAsync("Scanning user folders for large files...", async () =>
        {
            var result = await _storageAnalysisService.ScanLargeFilesAsync(LargeFileMinimumMb);
            LargeFiles.Clear();
            foreach (var file in result.Items.Where(file => !_protectionPolicyService.IsProtected(file.Path, _preferences)))
            {
                LargeFiles.Add(new StorageFileViewModel(file, RefreshStorageSummaries));
            }

            RefreshStorageSummaries();
            AddActivity("Info", $"Large-file scan found {LargeFiles.Count} files at or above {LargeFileMinimumMb} MB.");
            foreach (var warning in result.Warnings.Take(8))
            {
                AddActivity("Warn", warning);
            }

            SelectedSection = "Storage";
            StatusMessage = $"Large-file scan complete: {LargeFiles.Count} files found.";
        });
    }

    private async Task ScanDuplicatesAsync()
    {
        await RunBusyAsync("Hashing duplicate candidates...", async () =>
        {
            var result = await _storageAnalysisService.ScanDuplicatesAsync(DuplicateMinimumMb);
            DuplicateFiles.Clear();
            foreach (var file in result.Items.Where(file => !_protectionPolicyService.IsProtected(file.Path, _preferences)))
            {
                DuplicateFiles.Add(new DuplicateFileViewModel(file, RefreshStorageSummaries));
            }

            RefreshStorageSummaries();
            AddActivity("Info", $"Duplicate scan found {DuplicateFiles.Count} duplicate file entries at or above {DuplicateMinimumMb} MB.");
            foreach (var warning in result.Warnings.Take(8))
            {
                AddActivity("Warn", warning);
            }

            SelectedSection = "Storage";
            StatusMessage = $"Duplicate scan complete: {DuplicateFiles.Count} duplicate file entries found.";
        });
    }

    private async Task QuarantineSelectedLargeFilesAsync()
    {
        var selected = LargeFiles.Where(file => file.IsSelected).ToList();
        var candidates = selected
            .Select(file => CreateManualCleanupCandidate(file.Path, file.Name, "Large file review", file.SizeBytes, file.LastModifiedLocal?.ToUniversalTime(), RiskLevel.High))
            .ToList();

        await RunCandidateCleanupAsync(
            candidates,
            "Large-file review",
            completedPaths =>
            {
                foreach (var file in LargeFiles.Where(file => completedPaths.Contains(file.Path)).ToList())
                {
                    LargeFiles.Remove(file);
                }

                RefreshStorageSummaries();
            });
    }

    private async Task QuarantineSelectedDuplicatesAsync()
    {
        var selected = DuplicateFiles.Where(file => file.IsSelected).ToList();
        var candidates = selected
            .Select(file => CreateManualCleanupCandidate(file.Path, file.Name, "Duplicate file review", file.SizeBytes, file.LastModifiedLocal?.ToUniversalTime(), RiskLevel.High))
            .ToList();

        await RunCandidateCleanupAsync(
            candidates,
            "Duplicate-file review",
            completedPaths =>
            {
                foreach (var file in DuplicateFiles.Where(file => completedPaths.Contains(file.Path)).ToList())
                {
                    DuplicateFiles.Remove(file);
                }

                RefreshStorageSummaries();
            });
    }

    private async Task RefreshRecoveryAsync()
    {
        await RunBusyAsync("Refreshing recovery data...", RefreshRecoveryCoreAsync);
    }

    private async Task RefreshRecoveryCoreAsync()
    {
        var quarantineEntries = await _quarantineService.GetEntriesAsync();
        QuarantineEntries.Clear();
        foreach (var entry in quarantineEntries)
        {
            QuarantineEntries.Add(new QuarantineEntryViewModel(entry, RefreshRecoverySummaries));
        }

        var receipts = await _receiptService.GetRecentAsync();
        CleanupReceipts.Clear();
        foreach (var receipt in receipts)
        {
            CleanupReceipts.Add(receipt);
        }

        RefreshRecoverySummaries();
    }

    private async Task RestoreSelectedQuarantineAsync()
    {
        var selectedIds = QuarantineEntries.Where(entry => entry.IsSelected).Select(entry => entry.Id).ToList();
        if (selectedIds.Count == 0)
        {
            return;
        }

        await RunBusyAsync("Restoring selected quarantine items...", async () =>
        {
            var warnings = await _quarantineService.RestoreAsync(selectedIds);
            await RefreshRecoveryCoreAsync();
            AddActivity("Info", $"Restore completed for {selectedIds.Count - warnings.Count} quarantine items.");
            foreach (var warning in warnings.Take(8))
            {
                AddActivity("Warn", warning);
            }
            StatusMessage = "Restore operation complete.";
        });
    }

    private async Task PurgeSelectedQuarantineAsync()
    {
        var selected = QuarantineEntries.Where(entry => entry.IsSelected).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        var bytes = selected.Sum(entry => entry.SizeBytes);
        var confirmation = MessageBox.Show(
            $"Permanently purge {selected.Count} quarantined items and recover {Formatters.FormatBytes(bytes)}?",
            "Confirm purge",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        await RunBusyAsync("Purging selected quarantine items...", async () =>
        {
            var warnings = await _quarantineService.PurgeAsync(selected.Select(entry => entry.Id));
            await RefreshRecoveryCoreAsync();
            AddActivity("Info", $"Purged {selected.Count - warnings.Count} quarantine items, recovering up to {Formatters.FormatBytes(bytes)}.");
            foreach (var warning in warnings.Take(8))
            {
                AddActivity("Warn", warning);
            }
            StatusMessage = $"Purge complete: up to {Formatters.FormatBytes(bytes)} recovered.";
        });
    }

    private async Task PurgeAllQuarantineAsync()
    {
        var entries = QuarantineEntries.ToList();
        if (entries.Count == 0)
        {
            return;
        }

        var bytes = entries.Sum(entry => entry.SizeBytes);
        var confirmation = MessageBox.Show(
            $"Permanently purge all {entries.Count} quarantined items and recover {Formatters.FormatBytes(bytes)}?",
            "Confirm purge all",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        await RunBusyAsync("Purging all quarantine items...", async () =>
        {
            var warnings = await _quarantineService.PurgeAsync(entries.Select(entry => entry.Id));
            await RefreshRecoveryCoreAsync();
            AddActivity("Info", $"Purged quarantine, recovering up to {Formatters.FormatBytes(bytes)}.");
            foreach (var warning in warnings.Take(8))
            {
                AddActivity("Warn", warning);
            }
            StatusMessage = $"Quarantine purged: up to {Formatters.FormatBytes(bytes)} recovered.";
        });
    }

    private async Task ExportLatestReceiptAsync()
    {
        await RunBusyAsync("Exporting latest cleanup receipt...", async () =>
        {
            var path = await _receiptService.ExportLatestAsync();
            if (path is null)
            {
                StatusMessage = "No cleanup receipt is available to export.";
                AddActivity("Warn", "No cleanup receipt is available to export.");
                return;
            }

            StatusMessage = $"Receipt exported to {path}.";
            AddActivity("Info", $"Receipt exported to {path}.");
        });
    }

    private async Task EnableWeeklyScanReminderAsync()
    {
        await RunBusyAsync("Enabling weekly scan reminder...", async () =>
        {
            var status = await _scheduledScanService.EnableWeeklyAsync();
            ScheduledScanStatusText = status.Message;
            AddActivity(status.IsEnabled ? "Info" : "Warn", status.Message);
        });
    }

    private async Task DisableWeeklyScanReminderAsync()
    {
        await RunBusyAsync("Disabling weekly scan reminder...", async () =>
        {
            var status = await _scheduledScanService.DisableAsync();
            ScheduledScanStatusText = status.Message;
            AddActivity("Info", status.Message);
        });
    }

    private async Task RefreshScheduledScanStatusAsync()
    {
        var status = await _scheduledScanService.GetStatusAsync();
        ScheduledScanStatusText = status.Message;
    }

    private async Task RefreshWingetAsync()
    {
        await RunBusyAsync("Checking WinGet package updates...", RefreshWingetCoreAsync);
    }

    private async Task RefreshWingetCoreAsync()
    {
        var packages = await _wingetService.GetUpgradesAsync();
        WingetPackages.Clear();
        foreach (var package in packages)
        {
            WingetPackages.Add(new WingetPackageViewModel(package, RefreshWingetSummaries));
        }

        RefreshWingetSummaries();
        AddActivity("Info", $"WinGet found {WingetPackages.Count} available package updates.");
        StatusMessage = WingetPackages.Count == 0 ? "No WinGet updates were found." : $"WinGet found {WingetPackages.Count} updates.";
    }

    private async Task UpgradeSelectedPackagesAsync()
    {
        var selectedIds = WingetPackages.Where(package => package.IsSelected).Select(package => package.Id).ToList();
        if (selectedIds.Count == 0)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            $"Upgrade {selectedIds.Count} selected packages with WinGet?",
            "Confirm package updates",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        await RunBusyAsync("Updating selected WinGet packages...", async () =>
        {
            var result = await _wingetService.UpgradeAsync(selectedIds);
            AddActivity(result.Succeeded ? "Info" : "Warn", result.Message);
            await RefreshWingetCoreAsync();
            StatusMessage = result.Message;
        });
    }

    private async Task ExportWingetPackagesAsync()
    {
        await RunBusyAsync("Exporting WinGet package list...", async () =>
        {
            var exportRoot = Path.Combine(GetAppDataRoot(), "Winget");
            var exportPath = Path.Combine(exportRoot, $"packages-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
            var result = await _wingetService.ExportAsync(exportPath);
            AddActivity(result.Succeeded ? "Info" : "Warn", result.Succeeded ? $"WinGet package list exported to {exportPath}." : result.Message);
            StatusMessage = result.Succeeded ? $"Package list exported to {exportPath}." : result.Message;
        });
    }

    private async Task ImportWingetPackagesAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Import WinGet package list",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var confirmation = MessageBox.Show(
            "Importing a WinGet package list can install applications. Continue?",
            "Confirm package import",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        await RunBusyAsync("Importing WinGet package list...", async () =>
        {
            var result = await _wingetService.ImportAsync(dialog.FileName);
            AddActivity(result.Succeeded ? "Info" : "Warn", result.Message);
            StatusMessage = result.Message;
        });
    }

    private async Task ScanStorageMapAsync()
    {
        await RunBusyAsync("Mapping storage usage...", async () =>
        {
            var result = await _storageAnalysisService.ScanStorageMapAsync();
            FolderUsage.Clear();
            foreach (var folder in result.Folders)
            {
                FolderUsage.Add(folder);
            }

            FileTypeUsage.Clear();
            foreach (var fileType in result.FileTypes)
            {
                FileTypeUsage.Add(fileType);
            }

            RefreshStorageSummaries();
            RefreshReclaimPlan();
            foreach (var warning in result.Warnings.Take(8))
            {
                AddActivity("Warn", warning);
            }

            SelectedSection = "Storage";
            StatusMessage = $"Storage map complete: {FolderUsage.Count} folders and {FileTypeUsage.Count} file types summarized.";
        });
    }

    private async Task RefreshBrowserProfilesAsync()
    {
        await RunBusyAsync("Discovering browser profiles...", RefreshBrowserProfilesCoreAsync);
    }

    private async Task RefreshBrowserProfilesCoreAsync()
    {
        var profiles = await _browserProfileService.DiscoverProfilesAsync(_preferences);
        BrowserProfiles.Clear();
        foreach (var profile in profiles)
        {
            BrowserProfiles.Add(new BrowserProfileViewModel(profile, RefreshBrowserSummaries));
        }

        RefreshBrowserSummaries();
        AddActivity("Info", $"Discovered {BrowserProfiles.Count} browser profiles.");
    }

    private async Task CleanSelectedBrowserDataAsync()
    {
        var candidates = BrowserProfiles
            .SelectMany(CreateBrowserCleanupCandidates)
            .ToList();

        if (candidates.Count == 0)
        {
            MessageBox.Show("Select at least one browser profile data type before cleaning.", "Nothing selected", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await RunCandidateCleanupAsync(candidates, "Browser profile cleanup", _ =>
        {
            RefreshBrowserSummaries();
        });
    }

    private void AddProtectedPath()
    {
        if (string.IsNullOrWhiteSpace(NewProtectedPath))
        {
            return;
        }

        var entry = new ProtectedPathEntry(
            Environment.ExpandEnvironmentVariables(NewProtectedPath.Trim()),
            string.IsNullOrWhiteSpace(NewProtectedPathReason) ? "User protected" : NewProtectedPathReason.Trim(),
            DateTime.UtcNow);

        _preferences.ProtectedPaths.Add(entry);
        SavePreferences();
        ProtectedPaths.Add(new ProtectedPathViewModel(entry, RefreshProtectionSummaries));
        NewProtectedPath = string.Empty;
        NewProtectedPathReason = "User protected";
        RefreshProtectionSummaries();
        AddActivity("Info", $"Protected path added: {entry.Path}");
    }

    private void RemoveSelectedProtectedPaths()
    {
        var selected = ProtectedPaths.Where(path => path.IsSelected).ToList();
        foreach (var path in selected)
        {
            ProtectedPaths.Remove(path);
            _preferences.ProtectedPaths.RemoveAll(entry => entry.Path.Equals(path.Path, StringComparison.OrdinalIgnoreCase));
        }

        SavePreferences();
        RefreshProtectionSummaries();
        AddActivity("Info", $"Removed {selected.Count} protected paths.");
    }

    private async Task CreateRestorePointAsync()
    {
        await RunBusyAsync("Requesting Windows restore point...", async () =>
        {
            var result = await _systemRestoreService.RequestRestorePointAsync();
            AddActivity(result.Started ? "Info" : "Warn", result.Message);
            StatusMessage = result.Message;
        });
    }

    private async Task RunBusyAsync(string busyText, Func<Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            BusyText = busyText;
            StatusMessage = busyText;
            await action();
        }
        catch (Exception ex)
        {
            AddActivity("Error", ex.Message);
            StatusMessage = ex.Message;
        }
        finally
        {
            BusyText = "Idle";
            IsBusy = false;
        }
    }

    private bool GetInitialCategorySelection(CleanupRule rule)
    {
        if (_preferences.CleanupCategorySelections.TryGetValue(rule.Id, out var selected))
        {
            return selected;
        }

        return rule.DefaultSelected || (_preferences.SelectMediumRiskByDefault && rule.Risk == RiskLevel.Medium);
    }

    private void OnCleanupCategorySelectionChanged(CleanupCategoryViewModel category)
    {
        _preferences.CleanupCategorySelections[category.Id] = category.IsSelected;
        SavePreferences();

        _isBulkSelecting = true;
        foreach (var candidate in PreviewEntries.Where(candidate => candidate.CategoryId == category.Id))
        {
            candidate.IsSelected = category.IsSelected;
        }
        _isBulkSelecting = false;

        RefreshSelectionSummaries();
    }

    private void RefreshCategoryScanSummaries()
    {
        foreach (var category in CleanupCategories)
        {
            var candidates = PreviewEntries.Where(candidate => candidate.CategoryId == category.Id).ToList();
            category.CandidateCount = candidates.Count;
            category.LastScanBytes = candidates.Sum(candidate => candidate.SizeBytes);
        }
    }

    private void RefreshSelectionSummaries()
    {
        if (_isBulkSelecting)
        {
            return;
        }

        OnPropertyChanged(nameof(TotalScannedBytes));
        OnPropertyChanged(nameof(TotalSelectedBytes));
        OnPropertyChanged(nameof(SelectedPreviewCount));
        OnPropertyChanged(nameof(SelectedPreviewSummary));
        OnPropertyChanged(nameof(SelectedCategorySummary));
        OnPropertyChanged(nameof(CleanReadinessMessage));
        RaiseCommandStates();
    }

    private void RefreshStorageSummaries()
    {
        OnPropertyChanged(nameof(StorageSummary));
        OnPropertyChanged(nameof(StorageMapSummary));
        OnPropertyChanged(nameof(DuplicateSelectionSummary));
        OnPropertyChanged(nameof(LargeFileSelectionSummary));
        OnPropertyChanged(nameof(SelectedLargeFileCount));
        OnPropertyChanged(nameof(SelectedDuplicateCount));
        OnPropertyChanged(nameof(SelectedLargeFileBytes));
        OnPropertyChanged(nameof(SelectedDuplicateBytes));
        OnPropertyChanged(nameof(HasLargeFiles));
        OnPropertyChanged(nameof(HasDuplicateFiles));
        OnPropertyChanged(nameof(HasFolderUsage));
        OnPropertyChanged(nameof(HasFileTypeUsage));
        RefreshReclaimPlan();
        RaiseCommandStates();
    }

    private void RefreshRecoverySummaries()
    {
        OnPropertyChanged(nameof(QuarantineSummary));
        OnPropertyChanged(nameof(SelectedQuarantineSummary));
        OnPropertyChanged(nameof(SelectedQuarantineCount));
        OnPropertyChanged(nameof(SelectedQuarantineBytes));
        OnPropertyChanged(nameof(QuarantineBytes));
        OnPropertyChanged(nameof(HasQuarantineEntries));
        RefreshReclaimPlan();
        RaiseCommandStates();
    }

    private void RefreshWingetSummaries()
    {
        OnPropertyChanged(nameof(WingetSummary));
        OnPropertyChanged(nameof(WingetUpdateCount));
        OnPropertyChanged(nameof(SelectedWingetPackageCount));
        OnPropertyChanged(nameof(HasWingetUpdates));
        RaiseCommandStates();
    }

    private void RefreshBrowserSummaries()
    {
        OnPropertyChanged(nameof(BrowserProfileSummary));
        OnPropertyChanged(nameof(SelectedBrowserProfileCount));
        OnPropertyChanged(nameof(HasBrowserProfiles));
        RaiseCommandStates();
    }

    private void RefreshProtectionSummaries()
    {
        OnPropertyChanged(nameof(ProtectedPathCount));
        RemoveSelectedProtectedPathsCommand.RaiseCanExecuteChanged();
        AddProtectedPathCommand.RaiseCanExecuteChanged();
    }

    private void RefreshProtectedPaths()
    {
        ProtectedPaths.Clear();
        foreach (var entry in _preferences.ProtectedPaths)
        {
            ProtectedPaths.Add(new ProtectedPathViewModel(entry, RefreshProtectionSummaries));
        }

        RefreshProtectionSummaries();
    }

    private void RefreshReclaimPlan()
    {
        var plan = _reclaimPlanService.BuildPlan(
            PreviewEntries.Select(entry => new CleanupCandidateViewAdapter(entry.SizeBytes, entry.Risk)),
            LargeFiles.Select(entry => entry.File),
            DuplicateFiles.Select(entry => entry.File),
            QuarantineBytes);

        ReclaimPlan.Clear();
        foreach (var item in plan)
        {
            ReclaimPlan.Add(item);
        }
    }

    private IEnumerable<CleanupCandidate> CreateBrowserCleanupCandidates(BrowserProfileViewModel profile)
    {
        if (!profile.HasAnySelection)
        {
            yield break;
        }

        var root = profile.ProfilePath;
        if (profile.CacheSelected)
        {
            foreach (var path in GetBrowserCacheTargets(profile))
            {
                if (Directory.Exists(path) || File.Exists(path))
                {
                    yield return CreateManualCleanupCandidate(path, Path.GetFileName(path), $"{profile.Browser} cache", EstimateTargetSize(path), GetTargetLastModifiedUtc(path), RiskLevel.Medium, GetBrowserApprovedRoot(path, root));
                }
            }
        }

        if (profile.CookiesSelected)
        {
            foreach (var path in GetBrowserCookieTargets(profile))
            {
                if (File.Exists(path))
                {
                    yield return CreateManualCleanupCandidate(path, Path.GetFileName(path), $"{profile.Browser} cookies", EstimateTargetSize(path), GetTargetLastModifiedUtc(path), RiskLevel.High, root);
                }
            }
        }

        if (profile.HistorySelected)
        {
            foreach (var path in GetBrowserHistoryTargets(profile))
            {
                if (File.Exists(path))
                {
                    yield return CreateManualCleanupCandidate(path, Path.GetFileName(path), $"{profile.Browser} history", EstimateTargetSize(path), GetTargetLastModifiedUtc(path), RiskLevel.High, root);
                }
            }
        }

        if (profile.SessionsSelected)
        {
            foreach (var path in GetBrowserSessionTargets(profile))
            {
                if (Directory.Exists(path) || File.Exists(path))
                {
                    yield return CreateManualCleanupCandidate(path, Path.GetFileName(path), $"{profile.Browser} sessions", EstimateTargetSize(path), GetTargetLastModifiedUtc(path), RiskLevel.High, root);
                }
            }
        }
    }

    private static IEnumerable<string> GetBrowserCacheTargets(BrowserProfileViewModel profile)
    {
        if (profile.Browser.Contains("Firefox", StringComparison.OrdinalIgnoreCase))
        {
            yield return Path.Combine(profile.ProfilePath, "cache2");
            yield return Path.Combine(profile.ProfilePath, "startupCache");
            var localProfile = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Mozilla",
                "Firefox",
                "Profiles",
                profile.ProfileName);
            yield return Path.Combine(localProfile, "cache2");
            yield return Path.Combine(localProfile, "startupCache");
            yield break;
        }

        yield return Path.Combine(profile.ProfilePath, "Cache");
        yield return Path.Combine(profile.ProfilePath, "Code Cache");
        yield return Path.Combine(profile.ProfilePath, "GPUCache");
        yield return Path.Combine(profile.ProfilePath, "Service Worker", "CacheStorage");
    }

    private static string GetBrowserApprovedRoot(string path, string profileRoot)
    {
        if (path.StartsWith(profileRoot, StringComparison.OrdinalIgnoreCase))
        {
            return profileRoot;
        }

        return Path.GetDirectoryName(path) ?? profileRoot;
    }

    private static IEnumerable<string> GetBrowserCookieTargets(BrowserProfileViewModel profile)
    {
        if (profile.Browser.Contains("Firefox", StringComparison.OrdinalIgnoreCase))
        {
            yield return Path.Combine(profile.ProfilePath, "cookies.sqlite");
            yield break;
        }

        yield return Path.Combine(profile.ProfilePath, "Network", "Cookies");
        yield return Path.Combine(profile.ProfilePath, "Cookies");
    }

    private static IEnumerable<string> GetBrowserHistoryTargets(BrowserProfileViewModel profile)
    {
        if (profile.Browser.Contains("Firefox", StringComparison.OrdinalIgnoreCase))
        {
            yield return Path.Combine(profile.ProfilePath, "places.sqlite");
            yield break;
        }

        yield return Path.Combine(profile.ProfilePath, "History");
    }

    private static IEnumerable<string> GetBrowserSessionTargets(BrowserProfileViewModel profile)
    {
        if (profile.Browser.Contains("Firefox", StringComparison.OrdinalIgnoreCase))
        {
            yield return Path.Combine(profile.ProfilePath, "sessionstore-backups");
            yield return Path.Combine(profile.ProfilePath, "sessionstore.jsonlz4");
            yield break;
        }

        yield return Path.Combine(profile.ProfilePath, "Sessions");
        yield return Path.Combine(profile.ProfilePath, "Session Storage");
    }

    private void RefreshPreviewFilter()
    {
        PreviewEntriesView.Refresh();
        OnPropertyChanged(nameof(FilteredPreviewCount));
        OnPropertyChanged(nameof(HasNoPreviewEntries));
    }

    private bool FilterPreviewEntry(object item)
    {
        if (item is not CleanupCandidateViewModel candidate)
        {
            return false;
        }

        if (!string.Equals(SelectedCategoryFilter, AllCategoriesFilter, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(candidate.CategoryName, SelectedCategoryFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(SelectedRiskFilter, AllRisksFilter, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(candidate.Risk.ToString(), SelectedRiskFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(CleanerSearchText))
        {
            return true;
        }

        return Contains(candidate.DisplayName, CleanerSearchText)
            || Contains(candidate.Path, CleanerSearchText)
            || Contains(candidate.CategoryName, CleanerSearchText);
    }

    private bool FilterInstalledApp(object item)
    {
        if (item is not InstalledAppInfo app)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SoftwareSearchText))
        {
            return true;
        }

        return Contains(app.Name, SoftwareSearchText)
            || Contains(app.Publisher, SoftwareSearchText)
            || Contains(app.Version, SoftwareSearchText)
            || Contains(app.UninstallCommand, SoftwareSearchText);
    }

    private bool FilterStartupEntry(object item)
    {
        if (item is not StartupEntry entry)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(StartupSearchText))
        {
            return true;
        }

        return Contains(entry.Name, StartupSearchText)
            || Contains(entry.Scope, StartupSearchText)
            || Contains(entry.SourceType, StartupSearchText)
            || Contains(entry.Impact, StartupSearchText)
            || Contains(entry.Recommendation, StartupSearchText)
            || Contains(entry.Command, StartupSearchText)
            || Contains(entry.Location, StartupSearchText);
    }

    private void SetPreviewSelection(bool isSelected, bool onlyFiltered)
    {
        var targets = onlyFiltered
            ? PreviewEntriesView.Cast<CleanupCandidateViewModel>().ToList()
            : PreviewEntries.ToList();

        _isBulkSelecting = true;
        foreach (var candidate in targets)
        {
            candidate.IsSelected = isSelected;
        }
        _isBulkSelecting = false;

        RefreshSelectionSummaries();
    }

    private bool ConfirmRestorePointReminder(IReadOnlyCollection<CleanupCandidate> selectedCandidates)
    {
        if (!RemindRestorePointBeforeCleanup || selectedCandidates.All(candidate => candidate.Risk == RiskLevel.Low))
        {
            return true;
        }

        var result = MessageBox.Show(
            "This cleanup includes medium-risk locations such as browser or system caches. Create a restore point first?\n\nYes opens the restore-point request and pauses cleanup. Run cleanup again after it finishes.",
            "Restore point recommended",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            CreateRestorePointCommand.Execute(null);
            return false;
        }

        return result == MessageBoxResult.No;
    }

    private static CleanupCandidate CreateManualCleanupCandidate(
        string path,
        string displayName,
        string categoryName,
        long sizeBytes,
        DateTime? lastModifiedUtc,
        RiskLevel risk,
        string? approvedRootOverride = null)
    {
        var approvedRoot = approvedRootOverride ?? Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(approvedRoot))
        {
            approvedRoot = Path.GetPathRoot(path) ?? path;
        }

        return new CleanupCandidate(
            categoryName.ToLowerInvariant().Replace(' ', '-'),
            categoryName,
            displayName,
            path,
            approvedRoot,
            File.Exists(path) ? CleanupTargetKind.File : CleanupTargetKind.Directory,
            sizeBytes,
            lastModifiedUtc,
            risk);
    }

    private static long EstimateTargetSize(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return Math.Max(0, new FileInfo(path).Length);
            }

            if (!Directory.Exists(path))
            {
                return 0;
            }

            var total = 0L;
            foreach (var file in Directory.EnumerateFiles(path, "*", new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.ReparsePoint
            }))
            {
                try
                {
                    total += Math.Max(0, new FileInfo(file).Length);
                }
                catch
                {
                    // Size is only an estimate for review and receipts.
                }
            }

            return total;
        }
        catch
        {
            return 0;
        }
    }

    private static DateTime? GetTargetLastModifiedUtc(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return File.GetLastWriteTimeUtc(path);
            }

            if (Directory.Exists(path))
            {
                return Directory.GetLastWriteTimeUtc(path);
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static long GetSystemDriveFreeBytes()
    {
        try
        {
            var root = Path.GetPathRoot(Environment.SystemDirectory);
            if (string.IsNullOrWhiteSpace(root))
            {
                root = Path.GetPathRoot(Environment.CurrentDirectory);
            }

            return string.IsNullOrWhiteSpace(root) ? 0 : new DriveInfo(root).AvailableFreeSpace;
        }
        catch
        {
            return 0;
        }
    }

    private static string GetAppDataRoot()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
        {
            root = AppContext.BaseDirectory;
        }

        return Path.Combine(root, "SpacePilot");
    }

    private void DismissFirstRun()
    {
        _preferences.IsFirstRun = false;
        SavePreferences();
        OnPropertyChanged(nameof(ShowFirstRun));
        AddActivity("Info", "First-run safety notes acknowledged.");
    }

    private void SavePreferences()
    {
        _preferencesService.Save(_preferences);
    }

    private void RaiseCommandStates()
    {
        ScanCommand.RaiseCanExecuteChanged();
        CleanCommand.RaiseCanExecuteChanged();
        RefreshSoftwareCommand.RaiseCanExecuteChanged();
        RefreshStartupCommand.RaiseCanExecuteChanged();
        RefreshSettingsCommand.RaiseCanExecuteChanged();
        CreateRestorePointCommand.RaiseCanExecuteChanged();
        ScanLargeFilesCommand.RaiseCanExecuteChanged();
        ScanDuplicatesCommand.RaiseCanExecuteChanged();
        QuarantineSelectedLargeFilesCommand.RaiseCanExecuteChanged();
        QuarantineSelectedDuplicatesCommand.RaiseCanExecuteChanged();
        RefreshRecoveryCommand.RaiseCanExecuteChanged();
        RestoreSelectedQuarantineCommand.RaiseCanExecuteChanged();
        PurgeSelectedQuarantineCommand.RaiseCanExecuteChanged();
        PurgeAllQuarantineCommand.RaiseCanExecuteChanged();
        ExportLatestReceiptCommand.RaiseCanExecuteChanged();
        EnableWeeklyScanReminderCommand.RaiseCanExecuteChanged();
        DisableWeeklyScanReminderCommand.RaiseCanExecuteChanged();
        RefreshWingetCommand.RaiseCanExecuteChanged();
        UpgradeSelectedPackagesCommand.RaiseCanExecuteChanged();
        ExportWingetPackagesCommand.RaiseCanExecuteChanged();
        ImportWingetPackagesCommand.RaiseCanExecuteChanged();
        ScanStorageMapCommand.RaiseCanExecuteChanged();
        RefreshBrowserProfilesCommand.RaiseCanExecuteChanged();
        CleanSelectedBrowserDataCommand.RaiseCanExecuteChanged();
        AddProtectedPathCommand.RaiseCanExecuteChanged();
        RemoveSelectedProtectedPathsCommand.RaiseCanExecuteChanged();
    }

    private void AddActivity(string level, string message)
    {
        ActivityLog.Insert(0, new ActivityLogEntry(DateTime.UtcNow, level, message));
        while (ActivityLog.Count > 250)
        {
            ActivityLog.RemoveAt(ActivityLog.Count - 1);
        }
    }

    private static bool Contains(string value, string searchText)
    {
        return value.Contains(searchText, StringComparison.CurrentCultureIgnoreCase);
    }

    private static void OpenUri(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Could not open Windows settings", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static void OpenProcess(string fileName)
    {
        try
        {
            Process.Start(new ProcessStartInfo(fileName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, $"Could not open {fileName}", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
