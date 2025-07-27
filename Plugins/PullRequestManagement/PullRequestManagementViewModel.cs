using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using GadgetTools.Core.Models;
using GadgetTools.Core.Services;
using GadgetTools.Core.ViewModels;
using GadgetTools.Core.Views;

namespace GadgetTools.Plugins.PullRequestManagement
{
    public class PullRequestManagementViewModel : PluginViewModelBase
    {
        private readonly IPullRequestSettingsService _settingsService;
        private readonly AzureDevOpsConfigService _configService;
        private IAzureDevOpsPullRequestService? _azureDevOpsService;
        private readonly List<PullRequest> _allPullRequests = new();
        private readonly CollectionViewSource _pullRequestsViewSource = new();
        private string _currentUserName = Environment.UserName;
        private DispatcherTimer? _filterTimer;

        // Configuration Properties  
        private string _project = string.Empty;
        private string _repository = string.Empty;

        // Filter Properties
        private string _authorFilter = string.Empty;
        private string _targetBranchFilter = string.Empty;
        private string _searchText = string.Empty;
        private string _fileExtensionFilter = string.Empty;
        private string _minChangesFilter = string.Empty;
        private DateTime? _fromDate;
        private string _selectedStatus = "All";

        // Display Properties
        private PullRequest? _selectedPullRequest;
        private bool _isDetailPaneVisible;
        private bool _isConnected;
        private SavedSearch? _selectedSavedSearch;

        public ObservableCollection<PullRequest> PullRequests { get; } = new();
        public ObservableCollection<string> StatusOptions { get; } = new() { "All", "Active", "Completed", "Abandoned", "Draft" };
        public ObservableCollection<SavedSearch> SavedSearches { get; } = new();

        public ICollectionView FilteredPullRequests => _pullRequestsViewSource.View;

        // Configuration Properties
        public string Organization => _configService.Organization;
        public string PersonalAccessToken => _configService.PersonalAccessToken;
        public bool IsSharedConfigured => _configService.IsConfigured;

        public string Project
        {
            get => _project;
            set => SetProperty(ref _project, value);
        }

        public string Repository
        {
            get => _repository;
            set => SetProperty(ref _repository, value);
        }

        // Filter Properties
        public string AuthorFilter
        {
            get => _authorFilter;
            set
            {
                if (SetProperty(ref _authorFilter, value))
                {
                    ApplyFilters();
                }
            }
        }

        public string TargetBranchFilter
        {
            get => _targetBranchFilter;
            set
            {
                if (SetProperty(ref _targetBranchFilter, value))
                {
                    ApplyFilters();
                }
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    ApplyFilters();
                }
            }
        }

        public string FileExtensionFilter
        {
            get => _fileExtensionFilter;
            set
            {
                if (SetProperty(ref _fileExtensionFilter, value))
                {
                    ApplyFilters();
                }
            }
        }

        public string MinChangesFilter
        {
            get => _minChangesFilter;
            set
            {
                if (SetProperty(ref _minChangesFilter, value))
                {
                    ApplyFilters();
                }
            }
        }

        public DateTime? FromDate
        {
            get => _fromDate;
            set
            {
                if (SetProperty(ref _fromDate, value))
                {
                    ApplyFilters();
                }
            }
        }

        public string SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                if (SetProperty(ref _selectedStatus, value))
                {
                    ApplyFilters();
                }
            }
        }

        // Display Properties
        public PullRequest? SelectedPullRequest
        {
            get => _selectedPullRequest;
            set
            {
                if (SetProperty(ref _selectedPullRequest, value))
                {
                    IsDetailPaneVisible = value != null;
                }
            }
        }

        public bool IsDetailPaneVisible
        {
            get => _isDetailPaneVisible;
            set => SetProperty(ref _isDetailPaneVisible, value);
        }

        public bool IsConnected
        {
            get => _isConnected;
            set => SetProperty(ref _isConnected, value);
        }

        public SavedSearch? SelectedSavedSearch
        {
            get => _selectedSavedSearch;
            set
            {
                if (SetProperty(ref _selectedSavedSearch, value) && value != null)
                {
                    LoadSavedSearch(value);
                }
            }
        }

        // Commands
        public ICommand LoadPullRequestsCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand QuickFilterMyPRsCommand { get; }
        public ICommand QuickFilterActiveCommand { get; }
        public ICommand QuickFilterNeedsReviewCommand { get; }
        public ICommand QuickFilterApprovedCommand { get; }
        public ICommand SaveSearchCommand { get; }
        public ICommand DeleteSearchCommand { get; }
        public ICommand ClearAllFiltersCommand { get; }
        public ICommand OpenInBrowserCommand { get; }
        public ICommand CopyUrlCommand { get; }
        public ICommand CopyPrIdCommand { get; }
        public ICommand ShowAdvancedFilterCommand { get; }
        public ICommand ShowGlobalSearchCommand { get; }

        public PullRequestManagementViewModel(IPullRequestSettingsService settingsService)
        {
            _settingsService = settingsService;
            _configService = AzureDevOpsConfigService.Instance;

            // Initialize commands
            LoadPullRequestsCommand = new AsyncRelayCommand(LoadPullRequestsAsync, CanExecuteLoad);
            RefreshCommand = new AsyncRelayCommand(RefreshAsync, CanExecuteRefresh);
            QuickFilterMyPRsCommand = new RelayCommand(ApplyMyPRsFilter);
            QuickFilterActiveCommand = new RelayCommand(ApplyActiveFilter);
            QuickFilterNeedsReviewCommand = new RelayCommand(ApplyNeedsReviewFilter);
            QuickFilterApprovedCommand = new RelayCommand(ApplyApprovedFilter);
            SaveSearchCommand = new RelayCommand(SaveCurrentSearch);
            DeleteSearchCommand = new RelayCommand(DeleteSelectedSearch, CanDeleteSearch);
            ClearAllFiltersCommand = new RelayCommand(ClearAllFilters);
            OpenInBrowserCommand = new RelayCommand(OpenSelectedPullRequestInBrowser, CanExecutePullRequestAction);
            CopyUrlCommand = new RelayCommand(CopySelectedPullRequestUrl, CanExecutePullRequestAction);
            CopyPrIdCommand = new RelayCommand(CopySelectedPullRequestId, CanExecutePullRequestAction);
            ShowAdvancedFilterCommand = new RelayCommand(ShowAdvancedFilter);
            ShowGlobalSearchCommand = new RelayCommand(ShowGlobalSearch);

            // Initialize collection view
            _pullRequestsViewSource.Source = PullRequests;
            _pullRequestsViewSource.Filter += OnPullRequestsFilter;

            // Load settings
            LoadSettings();
            
            // Register filterable fields
            RegisterFilterableFields();
            
            // Monitor shared config changes
            _configService.ConfigurationChanged += OnSharedConfigChanged;
        }

        private async Task LoadPullRequestsAsync()
        {
            if (!ValidateConfiguration())
                return;

            try
            {
                ClearError();
                IsLoading = true;
                StatusMessage = "Loading pull requests...";

                var config = new AzureDevOpsConfig
                {
                    Organization = Organization,
                    Project = Project,
                    Repository = Repository,
                    PersonalAccessToken = PersonalAccessToken
                };

                _azureDevOpsService?.Dispose();
                _azureDevOpsService = new AzureDevOpsPullRequestService(config);

                var searchOptions = new PullRequestSearchOptions
                {
                    FromDate = FromDate,
                    AuthorFilter = string.IsNullOrWhiteSpace(AuthorFilter) ? null : AuthorFilter.Trim(),
                    TargetBranchFilter = string.IsNullOrWhiteSpace(TargetBranchFilter) ? null : TargetBranchFilter.Trim()
                };
                
                var pullRequests = await _azureDevOpsService.GetPullRequestsAsync(Project, Repository, searchOptions);

                _allPullRequests.Clear();
                _allPullRequests.AddRange(pullRequests);

                PullRequests.Clear();
                foreach (var pr in pullRequests)
                {
                    PullRequests.Add(pr);
                }

                ApplyFilters();
                IsConnected = true;
                StatusMessage = $"Loaded {pullRequests.Count()} pull requests";
            }
            catch (Exception ex)
            {
                SetError($"Error loading pull requests: {ex.Message}");
                IsConnected = false;
                StatusMessage = "Error occurred";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RefreshAsync()
        {
            if (_azureDevOpsService != null)
            {
                await LoadPullRequestsAsync();
            }
            else
            {
                StatusMessage = "Please load pull requests first.";
            }
        }

        private void ApplyFilters()
        {
            // フィルター適用をデバウンス
            _filterTimer?.Stop();
            _filterTimer = new DispatcherTimer();
            _filterTimer.Interval = TimeSpan.FromMilliseconds(300);
            _filterTimer.Tick += (s, e) =>
            {
                _pullRequestsViewSource.View?.Refresh();
                UpdateStatusWithFilter();
                _filterTimer.Stop();
            };
            _filterTimer.Start();
        }

        private void OnPullRequestsFilter(object sender, FilterEventArgs e)
        {
            if (e.Item is not PullRequest pr)
            {
                e.Accepted = false;
                return;
            }

            // Search text filter
            if (!string.IsNullOrEmpty(SearchText))
            {
                var searchText = SearchText.ToLowerInvariant();
                if (!pr.Title.ToLowerInvariant().Contains(searchText) &&
                    !pr.CreatedBy.ToLowerInvariant().Contains(searchText) &&
                    !pr.Description.ToLowerInvariant().Contains(searchText))
                {
                    e.Accepted = false;
                    return;
                }
            }

            // Status filter
            if (SelectedStatus != "All")
            {
                if (!pr.Status.Equals(SelectedStatus, StringComparison.OrdinalIgnoreCase))
                {
                    e.Accepted = false;
                    return;
                }
            }

            // File extension filter
            if (!string.IsNullOrEmpty(FileExtensionFilter))
            {
                var extension = FileExtensionFilter.StartsWith(".") ? FileExtensionFilter : "." + FileExtensionFilter;
                if (!pr.ModifiedFiles.Any(file => file.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
                {
                    e.Accepted = false;
                    return;
                }
            }

            // Min changes filter
            if (int.TryParse(MinChangesFilter, out int minChanges) && minChanges > 0)
            {
                if (pr.ChangedFiles < minChanges)
                {
                    e.Accepted = false;
                    return;
                }
            }

            e.Accepted = true;
        }

        private void ApplyMyPRsFilter()
        {
            AuthorFilter = _currentUserName;
            SelectedStatus = "Active";
        }

        private void ApplyActiveFilter()
        {
            SelectedStatus = "Active";
            AuthorFilter = string.Empty;
        }

        private void ApplyNeedsReviewFilter()
        {
            SelectedStatus = "Active";
            SearchText = "needs review";
        }

        private void ApplyApprovedFilter()
        {
            SelectedStatus = "Active";
            SearchText = "approved";
        }

        private void SaveCurrentSearch()
        {
            // Viewでダイアログを処理するため、一時的に無効化
            // TODO: プロパティとイベントベースで実装
            var searchName = "Search " + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            
            var savedSearch = new SavedSearch
            {
                Name = searchName,
                AuthorFilter = AuthorFilter,
                TargetBranchFilter = TargetBranchFilter,
                SearchText = SearchText,
                FileExtension = FileExtensionFilter,
                Status = SelectedStatus,
                FromDate = FromDate,
                CreatedDate = DateTime.Now
            };

            if (int.TryParse(MinChangesFilter, out int minChanges))
            {
                savedSearch.MinChanges = minChanges;
            }

            SavedSearches.Add(savedSearch);
            SelectedSavedSearch = savedSearch;
        }

        private void DeleteSelectedSearch()
        {
            if (SelectedSavedSearch != null)
            {
                var result = MessageBox.Show($"Delete saved search '{SelectedSavedSearch.Name}'?",
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    SavedSearches.Remove(SelectedSavedSearch);
                    SelectedSavedSearch = null;
                }
            }
        }

        private void ClearAllFilters()
        {
            AuthorFilter = string.Empty;
            TargetBranchFilter = string.Empty;
            SearchText = string.Empty;
            FileExtensionFilter = string.Empty;
            MinChangesFilter = string.Empty;
            FromDate = null;
            SelectedStatus = "All";
            SelectedSavedSearch = null;
        }

        private void LoadSavedSearch(SavedSearch savedSearch)
        {
            AuthorFilter = savedSearch.AuthorFilter;
            TargetBranchFilter = savedSearch.TargetBranchFilter;
            SearchText = savedSearch.SearchText;
            FileExtensionFilter = savedSearch.FileExtension;
            MinChangesFilter = savedSearch.MinChanges > 0 ? savedSearch.MinChanges.ToString() : string.Empty;
            FromDate = savedSearch.FromDate;
            SelectedStatus = string.IsNullOrEmpty(savedSearch.Status) ? "All" : savedSearch.Status;
        }

        private void OpenSelectedPullRequestInBrowser()
        {
            if (SelectedPullRequest != null && !string.IsNullOrEmpty(SelectedPullRequest.Url))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = SelectedPullRequest.Url,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    SetError($"Failed to open URL: {ex.Message}");
                }
            }
        }

        private void CopySelectedPullRequestUrl()
        {
            if (SelectedPullRequest != null && !string.IsNullOrEmpty(SelectedPullRequest.Url))
            {
                try
                {
                    Clipboard.SetText(SelectedPullRequest.Url);
                    StatusMessage = "URL copied to clipboard";

                    // Reset status message after 3 seconds
                    var timer = new DispatcherTimer();
                    timer.Interval = TimeSpan.FromSeconds(3);
                    timer.Tick += (s, e) =>
                    {
                        StatusMessage = "Ready";
                        timer.Stop();
                    };
                    timer.Start();
                }
                catch (Exception ex)
                {
                    SetError($"Failed to copy URL: {ex.Message}");
                }
            }
        }

        private void CopySelectedPullRequestId()
        {
            if (SelectedPullRequest != null)
            {
                try
                {
                    Clipboard.SetText(SelectedPullRequest.Id.ToString());
                    StatusMessage = $"PR ID #{SelectedPullRequest.Id} copied to clipboard";

                    // Reset status message after 3 seconds
                    var timer = new DispatcherTimer();
                    timer.Interval = TimeSpan.FromSeconds(3);
                    timer.Tick += (s, e) =>
                    {
                        StatusMessage = "Ready";
                        timer.Stop();
                    };
                    timer.Start();
                }
                catch (Exception ex)
                {
                    SetError($"Failed to copy PR ID: {ex.Message}");
                }
            }
        }

        private void UpdateStatusWithFilter()
        {
            var filteredCount = _pullRequestsViewSource.View?.Cast<object>().Count() ?? 0;
            var totalCount = _allPullRequests.Count;

            if (filteredCount == totalCount)
            {
                StatusMessage = $"Loaded {totalCount} pull requests";
            }
            else
            {
                StatusMessage = $"Showing {filteredCount} of {totalCount} pull requests";
            }
        }

        private bool ValidateConfiguration()
        {
            var validation = _configService.ValidateConfiguration();
            if (!validation.isValid)
            {
                SetError($"Global config error: {validation.errorMessage}");
                return false;
            }

            if (string.IsNullOrWhiteSpace(Project))
            {
                SetError("Please enter project name.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(Repository))
            {
                SetError("Please enter repository name.");
                return false;
            }

            return true;
        }

        private bool CanExecuteLoad()
        {
            return !IsLoading &&
                   _configService.IsConfigured &&
                   !string.IsNullOrWhiteSpace(Project) &&
                   !string.IsNullOrWhiteSpace(Repository);
        }

        private bool CanExecuteRefresh()
        {
            return !IsLoading && _azureDevOpsService != null;
        }

        private bool CanDeleteSearch()
        {
            return SelectedSavedSearch != null;
        }

        private bool CanExecutePullRequestAction()
        {
            return SelectedPullRequest != null;
        }

        private async void LoadSettings()
        {
            try
            {
                var settings = await _settingsService.LoadSettingsAsync();
                Project = settings.Project;
                Repository = settings.Repository;
                
                // Load shared configuration
                _configService.LoadConfiguration();
                
                // Notify shared config properties
                OnPropertyChanged(nameof(Organization));
                OnPropertyChanged(nameof(PersonalAccessToken));
                OnPropertyChanged(nameof(IsSharedConfigured));
                AuthorFilter = settings.AuthorFilter;
                TargetBranchFilter = settings.TargetBranchFilter;
                FromDate = settings.FromDate;

                SavedSearches.Clear();
                foreach (var search in settings.SavedSearches)
                {
                    SavedSearches.Add(search);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Settings load error: {ex.Message}");
            }
        }

        public override async Task CleanupAsync()
        {
            try
            {
                // Save shared configuration
                _configService.SaveConfiguration();
                
                // Save plugin-specific settings
                var settings = new PullRequestManagementSettings
                {
                    Project = Project,
                    Repository = Repository,
                    AuthorFilter = AuthorFilter,
                    TargetBranchFilter = TargetBranchFilter,
                    FromDate = FromDate,
                    SavedSearches = SavedSearches.ToList()
                };

                await _settingsService.SaveSettingsAsync(settings);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Settings save error: {ex.Message}");
            }

            // Unsubscribe from shared config events
            _configService.ConfigurationChanged -= OnSharedConfigChanged;
            
            _filterTimer?.Stop();
            _azureDevOpsService?.Dispose();
            await base.CleanupAsync();
        }

        #region Advanced Filter Methods

        private void RegisterFilterableFields()
        {
            var fields = new List<FilterableField>
            {
                FilterableField.CreateStringField("Title", "Title"),
                FilterableField.CreateStringField("CreatedBy", "Created By"),
                FilterableField.CreateStringField("SourceBranch", "Source Branch"),
                FilterableField.CreateStringField("TargetBranch", "Target Branch"),
                FilterableField.CreateDateField("CreatedDate", "Created Date"),
                FilterableField.CreateEnumField("Status", "Status", new List<object> { "Active", "Completed", "Abandoned", "Draft" }),
                FilterableField.CreateStringField("Description", "Description")
            };

            AdvancedFilterService.Instance.RegisterPluginFields("GadgetTools.PullRequestManagement", fields);
        }

        private void ShowAdvancedFilter()
        {
            try
            {
                var filterViewModel = new AdvancedFilterViewModel("GadgetTools.PullRequestManagement");
                var filterView = new AdvancedFilterView { DataContext = filterViewModel };

                var window = new Window
                {
                    Title = "Advanced Filter - Pull Requests",
                    Content = filterView,
                    Width = 800,
                    Height = 600,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Application.Current.MainWindow
                };

                filterViewModel.FilterApplied += (sender, conditions) =>
                {
                    ApplyAdvancedFilter(conditions);
                    window.Close();
                };

                filterView.FilterCancelled += (sender, e) => window.Close();

                window.ShowDialog();
            }
            catch (Exception ex)
            {
                SetError($"Failed to open advanced filter: {ex.Message}");
            }
        }

        private void ShowGlobalSearch()
        {
            var searchViewModel = new GlobalSearchViewModel();
            var searchView = new GlobalSearchView { DataContext = searchViewModel };

            var window = new Window
            {
                Title = "Global Search",
                Content = searchView,
                Width = 900,
                Height = 700,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current.MainWindow
            };

            window.ShowDialog();
        }

        private void ApplyAdvancedFilter(List<FilterCondition> conditions)
        {
            if (!conditions.Any())
            {
                // フィルタをクリア
                _pullRequestsViewSource.Filter -= OnAdvancedFilter;
                _pullRequestsViewSource.View?.Refresh();
                return;
            }

            // 高度なフィルタを適用
            _pullRequestsViewSource.Filter -= OnAdvancedFilter;
            _pullRequestsViewSource.Filter += OnAdvancedFilter;
            _advancedFilterConditions = conditions;
            _pullRequestsViewSource.View?.Refresh();
            UpdateStatusWithFilter();
        }

        private List<FilterCondition> _advancedFilterConditions = new();

        private void OnAdvancedFilter(object sender, FilterEventArgs e)
        {
            if (e.Item is not PullRequest pr)
            {
                e.Accepted = false;
                return;
            }

            // 既存の基本フィルタを適用
            OnPullRequestsFilter(sender, e);
            
            if (!e.Accepted) return;

            // 高度なフィルタを適用
            e.Accepted = AdvancedFilterService.Instance.ApplyFilter(pr, _advancedFilterConditions);
        }

        private void OnSharedConfigChanged(object? sender, EventArgs e)
        {
            // Notify shared config related properties
            OnPropertyChanged(nameof(Organization));
            OnPropertyChanged(nameof(PersonalAccessToken));
            OnPropertyChanged(nameof(IsSharedConfigured));
            
            // Update command enabled status
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        #endregion
    }
}