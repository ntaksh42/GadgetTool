using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
using GadgetTools.Core.Models;
using GadgetTools.Core.Services;
using GadgetTools.Core.ViewModels;
using GadgetTools.Core.Views;
using GadgetTools.Core.Controls;
using GadgetTools.Shared.Models;
using GadgetTools.Services;

namespace GadgetTools.Plugins.TicketManage
{
    public class TicketManageViewModel : PluginViewModelBase
    {
        #region Private Fields
        private readonly AzureDevOpsConfigService _configService;
        private readonly SavedQueryService _savedQueryService;
        private readonly KeyboardShortcutService _keyboardShortcutService;
        private string _project = string.Empty;
        private List<string> _projects = new List<string>();
        private string _workItemType = "All";
        private string _state = "All";
        private string _iteration = "";
        private string _area = "";
        private List<string> _iterations = new List<string>();
        private List<string> _areas = new List<string>();
        private List<string> _workItemTypes = new List<string>();
        private List<string> _states = new List<string>();
        private int _maxResults = 50;
        private bool _detailedMarkdown = true;
        private int _highlightDays = 7;
        private bool _enableHighlight = true;
        private string _filterText = string.Empty;
        private string _markdownPreview = string.Empty;
        private string _htmlPreview = string.Empty;
        private WorkItem? _selectedWorkItem;
        private bool _isConnected = false;
        private string _statusMessage = string.Empty;

        // Enhanced Search Fields
        private string _titleSearch = string.Empty;
        private string _descriptionSearch = string.Empty;
        private DateTime? _createdAfter;
        private DateTime? _createdBefore;
        private DateTime? _updatedAfter;
        private DateTime? _updatedBefore;
        private string _tagsSearch = string.Empty;
        private int? _minPriority;
        private int? _maxPriority;
        private string _assignedTo = string.Empty;

        private readonly CollectionViewSource _workItemsViewSource = new();
        private readonly List<WorkItem> _allWorkItems = new();
        private readonly ColumnFilterManager _columnFilterManager = new();
        #endregion

        #region Collections
        public ObservableCollection<WorkItem> WorkItems { get; } = new();
        public ObservableCollection<string> AllWorkItemTypes { get; } = new()
        {
            "All", "Bug", "Task", "User Story", "Feature", "Epic"
        };
        public ObservableCollection<string> AllStates { get; } = new()
        {
            "All", "Active", "New", "Resolved", "Closed"
        };
        #endregion

        #region Properties
        public string Organization => _configService.Organization;
        public string PatToken => _configService.PersonalAccessToken;
        public bool IsSharedConfigured => _configService.IsConfigured;

        public string Project
        {
            get => _project;
            set 
            { 
                if (SetProperty(ref _project, value))
                {
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public List<string> Projects
        {
            get => _projects;
            set 
            {
                if (SetProperty(ref _projects, value))
                {
                    OnPropertyChanged(nameof(ProjectsPreview));
                    OnPropertyChanged(nameof(SearchCriteriaSummary));
                }
            }
        }
        
        public string ProjectsPreview
        {
            get
            {
                if (!Projects.Any()) return "No projects selected";
                if (Projects.Count == 1) return Projects.First();
                return $"{Projects.Count} projects: {string.Join(", ", Projects.Take(3))}{(Projects.Count > 3 ? "..." : "")}";
            }
        }

        public string WorkItemType
        {
            get => _workItemType;
            set 
            {
                if (SetProperty(ref _workItemType, value))
                {
                    OnPropertyChanged(nameof(SearchCriteriaSummary));
                }
            }
        }

        public string State
        {
            get => _state;
            set 
            {
                if (SetProperty(ref _state, value))
                {
                    OnPropertyChanged(nameof(SearchCriteriaSummary));
                }
            }
        }

        public List<string> WorkItemTypes
        {
            get => _workItemTypes;
            set
            {
                if (SetProperty(ref _workItemTypes, value))
                {
                    OnPropertyChanged(nameof(WorkItemTypesPreview));
                    OnPropertyChanged(nameof(SearchCriteriaSummary));
                }
            }
        }

        public List<string> States
        {
            get => _states;
            set
            {
                if (SetProperty(ref _states, value))
                {
                    OnPropertyChanged(nameof(StatesPreview));
                    OnPropertyChanged(nameof(SearchCriteriaSummary));
                }
            }
        }

        public string WorkItemTypesPreview
        {
            get
            {
                if (WorkItemTypes == null || WorkItemTypes.Count == 0)
                    return "All work item types";
                return $"{WorkItemTypes.Count} types: {string.Join(", ", WorkItemTypes.Take(3))}{(WorkItemTypes.Count > 3 ? "..." : "")}";
            }
        }

        public string StatesPreview
        {
            get
            {
                if (States == null || States.Count == 0)
                    return "All states";
                return $"{States.Count} states: {string.Join(", ", States.Take(3))}{(States.Count > 3 ? "..." : "")}";
            }
        }

        public string Iteration
        {
            get => _iteration;
            set 
            {
                if (SetProperty(ref _iteration, value))
                {
                    OnPropertyChanged(nameof(SearchCriteriaSummary));
                }
            }
        }

        public List<string> Iterations
        {
            get => _iterations;
            set 
            {
                if (SetProperty(ref _iterations, value))
                {
                    OnPropertyChanged(nameof(IterationsPreview));
                    OnPropertyChanged(nameof(SearchCriteriaSummary));
                }
            }
        }
        
        public string IterationsPreview
        {
            get
            {
                if (!Iterations.Any()) return "All iterations";
                if (Iterations.Count == 1) return Iterations.First();
                return $"{Iterations.Count} iterations: {string.Join(", ", Iterations.Take(2))}{(Iterations.Count > 2 ? "..." : "")}";
            }
        }

        public string Area
        {
            get => _area;
            set 
            {
                if (SetProperty(ref _area, value))
                {
                    OnPropertyChanged(nameof(SearchCriteriaSummary));
                }
            }
        }

        public List<string> Areas
        {
            get => _areas;
            set 
            {
                if (SetProperty(ref _areas, value))
                {
                    OnPropertyChanged(nameof(AreasPreview));
                    OnPropertyChanged(nameof(SearchCriteriaSummary));
                }
            }
        }
        
        public string AreasPreview
        {
            get
            {
                if (!Areas.Any()) return "All areas";
                if (Areas.Count == 1) return Areas.First();
                return $"{Areas.Count} areas: {string.Join(", ", Areas.Take(2))}{(Areas.Count > 2 ? "..." : "")}";
            }
        }
        
        public string SearchCriteriaSummary
        {
            get
            {
                var criteria = new List<string>();
                
                if (Projects.Any())
                {
                    criteria.Add($"Projects: {string.Join(", ", Projects.Take(2))}{(Projects.Count > 2 ? $" +{Projects.Count - 2} more" : "")}");
                }
                else if (!string.IsNullOrWhiteSpace(Project))
                {
                    criteria.Add($"Project: {Project}");
                }
                
                if (!string.IsNullOrWhiteSpace(WorkItemType) && WorkItemType != "All")
                {
                    criteria.Add($"Type: {WorkItemType}");
                }
                
                if (!string.IsNullOrWhiteSpace(State) && State != "All")
                {
                    criteria.Add($"State: {State}");
                }
                
                if (Iterations.Any())
                {
                    criteria.Add($"Iterations: {string.Join(", ", Iterations.Take(2))}{(Iterations.Count > 2 ? $" +{Iterations.Count - 2} more" : "")}");
                }
                else if (!string.IsNullOrWhiteSpace(Iteration))
                {
                    criteria.Add($"Iteration: {Iteration}");
                }
                
                if (Areas.Any())
                {
                    criteria.Add($"Areas: {string.Join(", ", Areas.Take(2))}{(Areas.Count > 2 ? $" +{Areas.Count - 2} more" : "")}");
                }
                else if (!string.IsNullOrWhiteSpace(Area))
                {
                    criteria.Add($"Area: {Area}");
                }
                
                if (!criteria.Any())
                {
                    return "No specific criteria - searching all items";
                }
                
                return string.Join(" | ", criteria);
            }
        }

        public int MaxResults
        {
            get => _maxResults;
            set => SetProperty(ref _maxResults, value);
        }

        public bool DetailedMarkdown
        {
            get => _detailedMarkdown;
            set => SetProperty(ref _detailedMarkdown, value);
        }

        public int HighlightDays
        {
            get => _highlightDays;
            set
            {
                if (SetProperty(ref _highlightDays, value))
                {
                    RefreshWorkItemsView();
                }
            }
        }

        public bool EnableHighlight
        {
            get => _enableHighlight;
            set
            {
                if (SetProperty(ref _enableHighlight, value))
                {
                    RefreshWorkItemsView();
                }
            }
        }

        public string FilterText
        {
            get => _filterText;
            set
            {
                if (SetProperty(ref _filterText, value))
                {
                    RefreshWorkItemsView();
                }
            }
        }

        public string MarkdownPreview
        {
            get => _markdownPreview;
            set => SetProperty(ref _markdownPreview, value);
        }

        public string HtmlPreview
        {
            get => _htmlPreview;
            set
            {
                if (SetProperty(ref _htmlPreview, value))
                {
                    System.Diagnostics.Debug.WriteLine($"HtmlPreview updated. Length: {value?.Length ?? 0}");
                }
            }
        }

        public WorkItem? SelectedWorkItem
        {
            get => _selectedWorkItem;
            set
            {
                if (SetProperty(ref _selectedWorkItem, value))
                {
                    if (value != null)
                    {
                        ShowWorkItemDetail(value);
                    }
                    else
                    {
                        ClearWorkItemDetail();
                    }
                }
            }
        }

        public bool IsConnected
        {
            get => _isConnected;
            set => SetProperty(ref _isConnected, value);
        }

        public new string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICollectionView WorkItemsView => _workItemsViewSource.View;

        #region Enhanced Search Properties

        public string TitleSearch
        {
            get => _titleSearch;
            set => SetProperty(ref _titleSearch, value);
        }

        public string DescriptionSearch
        {
            get => _descriptionSearch;
            set => SetProperty(ref _descriptionSearch, value);
        }

        public DateTime? CreatedAfter
        {
            get => _createdAfter;
            set => SetProperty(ref _createdAfter, value);
        }

        public DateTime? CreatedBefore
        {
            get => _createdBefore;
            set => SetProperty(ref _createdBefore, value);
        }

        public DateTime? UpdatedAfter
        {
            get => _updatedAfter;
            set => SetProperty(ref _updatedAfter, value);
        }

        public DateTime? UpdatedBefore
        {
            get => _updatedBefore;
            set => SetProperty(ref _updatedBefore, value);
        }

        public string TagsSearch
        {
            get => _tagsSearch;
            set => SetProperty(ref _tagsSearch, value);
        }

        public int? MinPriority
        {
            get => _minPriority;
            set => SetProperty(ref _minPriority, value);
        }

        public int? MaxPriority
        {
            get => _maxPriority;
            set => SetProperty(ref _maxPriority, value);
        }

        public string AssignedTo
        {
            get => _assignedTo;
            set => SetProperty(ref _assignedTo, value);
        }

        // Collections for new features
        public ObservableCollection<SavedQuery> SavedQueries { get; private set; }
        public ObservableCollection<SearchQuickFilter> QuickFilters { get; private set; }

        #endregion
        #endregion

        #region Commands
        public ICommand TestConnectionCommand { get; }
        public ICommand QueryWorkItemsCommand { get; }
        public ICommand SaveResultCommand { get; }
        public ICommand ClearFilterCommand { get; }
        public ICommand ShowAdvancedFilterCommand { get; }
        public ICommand ShowGlobalSearchCommand { get; }
        public ICommand ShowColumnFilterCommand { get; }
        public ICommand ClearColumnFiltersCommand { get; }
        public ICommand ShowColumnVisibilityCommand { get; }
        
        // Enhanced Search Commands
        public ICommand SaveCurrentQueryCommand { get; }
        public ICommand LoadSavedQueryCommand { get; }
        public ICommand DeleteSavedQueryCommand { get; }
        public ICommand ApplyQuickFilterCommand { get; }
        public ICommand ClearAdvancedSearchCommand { get; }
        
        // Context Menu Commands
        public ICommand CopyWorkItemIdCommand { get; }
        public ICommand CopyWorkItemTitleCommand { get; }
        public ICommand CopyWorkItemUrlCommand { get; }
        public ICommand OpenInBrowserCommand { get; }
        public ICommand SetPriorityCommand { get; }
        public ICommand ChangeStateCommand { get; }
        public ICommand AssignToMeCommand { get; }
        public ICommand AddToWatchlistCommand { get; }
        public ICommand ShowAreaChartCommand { get; }
        public ICommand LoadSampleDataCommand { get; }
        public ICommand ClearDrillDownCommand { get; }
        #endregion

        public TicketManageViewModel()
        {
            _configService = AzureDevOpsConfigService.Instance;
            _savedQueryService = SavedQueryService.Instance;
            _keyboardShortcutService = KeyboardShortcutService.Instance;
            
            TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync, CanExecuteConnection);
            QueryWorkItemsCommand = new AsyncRelayCommand(QueryWorkItemsAsync, CanExecuteQuery);
            SaveResultCommand = new RelayCommand(SaveResult, CanSaveResult);
            ClearFilterCommand = new RelayCommand(ClearFilter);
            ShowAdvancedFilterCommand = new RelayCommand(ShowAdvancedFilter);
            ShowGlobalSearchCommand = new RelayCommand(ShowGlobalSearch);
            ShowColumnFilterCommand = new RelayCommand<string>(ShowColumnFilter);
            ClearColumnFiltersCommand = new RelayCommand(ClearColumnFilters);
            ShowColumnVisibilityCommand = new RelayCommand(ShowColumnVisibility);
            
            // Enhanced Search Commands
            SaveCurrentQueryCommand = new RelayCommand(SaveCurrentQuery, CanSaveCurrentQuery);
            LoadSavedQueryCommand = new RelayCommand<SavedQuery>(LoadSavedQuery);
            DeleteSavedQueryCommand = new RelayCommand<string>(DeleteSavedQuery);
            ApplyQuickFilterCommand = new RelayCommand<SearchQuickFilter>(ApplyQuickFilter);
            ClearAdvancedSearchCommand = new RelayCommand(ClearAdvancedSearch);
            
            // Context Menu Commands
            CopyWorkItemIdCommand = new RelayCommand(CopyWorkItemId, CanExecuteSelectedItemCommand);
            CopyWorkItemTitleCommand = new RelayCommand(CopyWorkItemTitle, CanExecuteSelectedItemCommand);
            CopyWorkItemUrlCommand = new RelayCommand(CopyWorkItemUrl, CanExecuteSelectedItemCommand);
            OpenInBrowserCommand = new RelayCommand(OpenInBrowser, CanExecuteSelectedItemCommand);
            SetPriorityCommand = new RelayCommand<string>(SetPriority, CanExecuteSelectedItemCommand);
            ChangeStateCommand = new RelayCommand<string>(ChangeState, CanExecuteSelectedItemCommand);
            AssignToMeCommand = new RelayCommand(AssignToMe, CanExecuteSelectedItemCommand);
            AddToWatchlistCommand = new RelayCommand(AddToWatchlist, CanExecuteSelectedItemCommand);
            ShowAreaChartCommand = new RelayCommand(ShowAreaChart, CanShowAreaChart);
            LoadSampleDataCommand = new RelayCommand(LoadSampleData);
            ClearDrillDownCommand = new RelayCommand(ExecuteClearDrillDown, CanClearDrillDown);
            
            // Initialize collections
            SavedQueries = new ObservableCollection<SavedQuery>();
            QuickFilters = new ObservableCollection<SearchQuickFilter>();
            
            LoadSavedQueries();
            LoadQuickFilters();
            SetupKeyboardShortcuts();

            _workItemsViewSource.Source = WorkItems;
            _workItemsViewSource.Filter += OnWorkItemsFilter;

            // 初期プレビューメッセージを設定
            MarkdownPreview = "ワークアイテムをクエリしてチケットを取得してください。\n\nPlease query work items to retrieve tickets.";
            try
            {
                var initialHtml = TicketHtmlService.GenerateEmptyStateHtml();
                HtmlPreview = initialHtml;
                System.Diagnostics.Debug.WriteLine($"Initial HTML preview set using TicketHtmlService. Length: {initialHtml?.Length ?? 0}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting initial HTML preview: {ex.Message}");
                SetError($"プレビューの初期化に失敗しました: {ex.Message}");
                
                // Fallback to basic HTML
                var fallbackHtml = @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Preview</title>
    <style>
        body { font-family: Arial, sans-serif; padding: 20px; text-align: center; color: #666; }
        .message { background: #f8f9fa; padding: 30px; border-radius: 8px; border: 1px solid #dee2e6; }
    </style>
</head>
<body>
    <div class='message'>
        <h3>📋 チケットプレビュー</h3>
        <p>ワークアイテムをクエリしてチケットを取得してください。</p>
        <p>Please query work items to retrieve tickets.</p>
    </div>
</body>
</html>";
                HtmlPreview = fallbackHtml;
                System.Diagnostics.Debug.WriteLine("Set fallback HTML preview");
            }
            
            // 設定を読み込み
            LoadSettings();
            
            // フィルタ可能フィールドを登録
            RegisterFilterableFields();
            
            // 共通設定の変更を監視
            _configService.ConfigurationChanged += OnSharedConfigChanged;
            
            // 列フィルタの変更を監視
            _columnFilterManager.FilterChanged += OnColumnFilterChanged;
        }

        private async Task TestConnectionAsync()
        {
            if (!ValidateInput())
                return;

            try
            {
                ClearError();
                IsLoading = true;
                StatusMessage = "接続をテスト中...";

                var config = CreateAzureDevOpsConfig();
                using var service = new AzureDevOpsService(config, true); // 共有HTTPクライアント使用

                // 簡単なクエリでテスト
                var testRequest = new WorkItemQueryRequest
                {
                    Organization = config.Organization,
                    Project = config.Project,
                    MaxResults = 1
                };

                await service.GetWorkItemsAsync(testRequest);

                // 接続成功

                IsConnected = true;
                StatusMessage = "✅ 接続が成功しました！";
            }
            catch (Exception ex)
            {
                IsConnected = false;
                SetError($"接続に失敗しました: {ex.Message}");
                StatusMessage = "❌ 接続に失敗しました";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task QueryWorkItemsAsync()
        {
            if (!ValidateInput())
                return;

            using var performanceMeasurement = PerformanceOptimizationService.Instance.MeasureOperation("QueryWorkItems");
            
            try
            {
                ClearError();
                IsLoading = true;
                StatusMessage = "ワークアイテムを取得中...";

                var config = CreateAzureDevOpsConfig();
                var request = CreateQueryRequest(config);

                using var service = new AzureDevOpsService(config, true); // 共有HTTPクライアント使用
                var workItems = await service.GetWorkItemsAsync(request);

                _allWorkItems.Clear();
                _allWorkItems.AddRange(workItems);

                WorkItems.Clear();
                foreach (var item in workItems)
                {
                    WorkItems.Add(item);
                }

                RefreshWorkItemsView();
                GenerateMarkdownPreview();
                
                // 列フィルタのデータを更新
                UpdateColumnFilterData();

                IsConnected = true;
                StatusMessage = $"✅ {workItems.Count}件のワークアイテムを取得しました";
            }
            catch (Exception ex)
            {
                SetError($"クエリに失敗しました: {ex.Message}");
                StatusMessage = "❌ クエリに失敗しました";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void SaveResult()
        {
            if (string.IsNullOrEmpty(MarkdownPreview))
            {
                SetError("保存するデータがありません。まずワークアイテムを取得してください。");
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Title = "Azure DevOps結果を保存",
                Filter = "Markdown Files (*.md)|*.md|All Files (*.*)|*.*",
                FilterIndex = 1,
                DefaultExt = "md",
                FileName = $"azure-devops-workitems-{DateTime.Now:yyyyMMdd-HHmmss}.md"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(saveFileDialog.FileName, MarkdownPreview, Encoding.UTF8);
                    StatusMessage = $"✅ {Path.GetFileName(saveFileDialog.FileName)}に保存しました";
                }
                catch (Exception ex)
                {
                    SetError($"ファイルの保存に失敗しました: {ex.Message}");
                }
            }
        }

        private void ClearFilter()
        {
            FilterText = string.Empty;
        }

        private void RefreshWorkItemsView()
        {
            _workItemsViewSource.View?.Refresh();
            UpdateFilterResultsDisplay();
            
            // チャートコマンドの有効状態を更新
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        private void OnWorkItemsFilter(object sender, FilterEventArgs e)
        {
            if (e.Item is WorkItem workItem)
            {
                // Enhanced text filter with multiple search criteria
                bool passesTextFilter = EvaluateEnhancedTextFilters(workItem);

                // 列フィルタもチェック
                bool passesColumnFilter = true;
                if (_columnFilterManager.HasActiveFilters)
                {
                    var propertyGetters = new Dictionary<string, Func<object, object?>>
                    {
                        ["ID"] = (item) => ((WorkItem)item).Id,
                        ["Type"] = (item) => ((WorkItem)item).Fields.WorkItemType ?? "",
                        ["Title"] = (item) => ((WorkItem)item).Fields.Title ?? "",
                        ["State"] = (item) => ((WorkItem)item).Fields.State ?? "",
                        ["Assigned To"] = (item) => ((WorkItem)item).Fields.AssignedTo?.DisplayName ?? "",
                        ["Priority"] = (item) => ((WorkItem)item).Fields.Priority.ToString(),
                        ["Created"] = (item) => ((WorkItem)item).Fields.CreatedDate.ToString("yyyy-MM-dd"),
                        ["Last Updated"] = (item) => ((WorkItem)item).Fields.ChangedDate.ToString("yyyy-MM-dd HH:mm")
                    };

                    passesColumnFilter = _columnFilterManager.ShouldIncludeItem(workItem, propertyGetters);
                }

                e.Accepted = passesTextFilter && passesColumnFilter;
            }
            else
            {
                e.Accepted = false;
            }
        }

        private WorkItem? _lastDetailedWorkItem;
        private string _lastHtmlContent = "";
        private readonly object _updateLock = new object();
        private CancellationTokenSource? _currentUpdateToken;

        private async void ShowWorkItemDetail(WorkItem workItem)
        {
            System.Diagnostics.Debug.WriteLine($"ShowWorkItemDetail called for Work Item #{workItem.Id}");
            
            // 同じワークアイテムの場合は再計算をスキップ
            if (_lastDetailedWorkItem?.Id == workItem.Id)
            {
                System.Diagnostics.Debug.WriteLine($"Skipping duplicate detail request for Work Item #{workItem.Id}");
                return;
            }
            
            // 前の更新処理をキャンセル
            _currentUpdateToken?.Cancel();
            _currentUpdateToken = new CancellationTokenSource();
            var token = _currentUpdateToken.Token;
            
            lock (_updateLock)
            {
                _lastDetailedWorkItem = workItem;
            }
            
            try
            {
                // キャンセルチェック
                if (token.IsCancellationRequested) return;
                
                // First, get comments to generate proper cache key
                List<WorkItemComment>? comments = null;
                try
                {
                    var config = CreateAzureDevOpsConfig();
                    using var service = new AzureDevOpsService(config, true); // 共有HTTPクライアント使用
                    comments = await service.GetWorkItemCommentsAsync(workItem.Id, Project);
                    System.Diagnostics.Debug.WriteLine($"Retrieved {comments.Count} comments for Work Item #{workItem.Id}");
                    
                    // キャンセルチェック
                    if (token.IsCancellationRequested) return;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to get comments for Work Item #{workItem.Id}: {ex.Message}");
                    // Continue without comments
                }

                // Generate cache key based on work item, last modified date, and comment count
                var cacheKey = HtmlContentCache.GenerateCacheKey(workItem.Id, workItem.Fields.ChangedDate, comments?.Count);
                
                // Try to get cached HTML content first
                if (HtmlContentCache.Instance.TryGetCachedContent(cacheKey, out var cachedHtml))
                {
                    System.Diagnostics.Debug.WriteLine($"Using cached HTML for Work Item #{workItem.Id}");
                    
                    // キャンセルチェック
                    if (token.IsCancellationRequested) return;
                    
                    // UIスレッドで更新（必要な場合のみ）
                    if (_lastHtmlContent != cachedHtml)
                    {
                        _lastHtmlContent = cachedHtml ?? "";
                        HtmlPreview = _lastHtmlContent;
                    }
                }
                else
                {
                    // Generate HTML asynchronously with comments for better performance
                    var htmlContent = await Task.Run(() => 
                    {
                        if (token.IsCancellationRequested) return null;
                        return TicketHtmlService.GenerateWorkItemHtml(workItem, comments, Organization);
                    }, token);
                    
                    // キャンセルチェック
                    if (token.IsCancellationRequested || htmlContent == null) return;
                    
                    // Cache the generated content
                    HtmlContentCache.Instance.CacheContent(cacheKey, htmlContent);
                    System.Diagnostics.Debug.WriteLine($"Generated and cached HTML with discussions for Work Item #{workItem.Id}, length: {htmlContent?.Length ?? 0}");
                    
                    // UIスレッドで更新（必要な場合のみ）
                    if (_lastHtmlContent != htmlContent)
                    {
                        _lastHtmlContent = htmlContent;
                        HtmlPreview = htmlContent;
                    }
                }

                // Generate Markdown for traditional preview (not cached as it's less expensive)
                var converter = new AzureDevOpsMarkdownConverter();
                var detailMarkdown = converter.ConvertWorkItemsToMarkdown(
                    new List<WorkItem> { workItem },
                    $"Work Item #{workItem.Id} - {workItem.Fields.Title}"
                );
                
                MarkdownPreview = detailMarkdown ?? "";
            }
            catch (Exception ex)
            {
                var errorMessage = $"チケット詳細の表示に失敗しました: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"Error in ShowWorkItemDetail: {ex.Message}");
                SetError(errorMessage);
                
                // Fallback to basic information
                MarkdownPreview = $"# Work Item #{workItem.Id}\n\n**Title:** {workItem.Fields.Title}\n\n**Error:** {errorMessage}";
                HtmlPreview = TicketHtmlService.GenerateEmptyStateHtml();
            }
        }

        private void ClearWorkItemDetail()
        {
            try
            {
                if (WorkItems.Count > 0)
                {
                    GenerateMarkdownPreview();
                }
                else
                {
                    MarkdownPreview = "チケットを選択すると、ここに詳細が表示されます。\n\nSelect a ticket to view its details here.";
                    HtmlPreview = TicketHtmlService.GenerateEmptyStateHtml();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ClearWorkItemDetail: {ex.Message}");
                SetError($"プレビューのクリアに失敗しました: {ex.Message}");
                HtmlPreview = TicketHtmlService.GenerateEmptyStateHtml();
            }
        }

        private async void GenerateMarkdownPreview()
        {
            try
            {
                if (WorkItems.Count == 0)
                {
                    MarkdownPreview = "";
                    HtmlPreview = TicketHtmlService.GenerateEmptyStateHtml();
                    return;
                }

                var converter = new AzureDevOpsMarkdownConverter();
                var title = $"{Organization}/{Project} Work Items";

                if (DetailedMarkdown)
                {
                    MarkdownPreview = converter.ConvertWorkItemsToMarkdown(_allWorkItems, title);
                }
                else
                {
                    MarkdownPreview = converter.ConvertToTable(_allWorkItems);
                }
                
                // Generate HTML list view asynchronously with performance monitoring
                using (PerformanceOptimizationService.Instance.MeasureOperation("GenerateHTML"))
                {
                    var htmlContent = await Task.Run(() => 
                        TicketHtmlService.GenerateListViewHtml(_allWorkItems, title));
                    HtmlPreview = htmlContent;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GenerateMarkdownPreview: {ex.Message}");
                SetError($"プレビューの生成に失敗しました: {ex.Message}");
                HtmlPreview = TicketHtmlService.GenerateEmptyStateHtml();
            }
        }

        private void UpdateFilterResultsDisplay()
        {
            var filteredCount = _workItemsViewSource.View?.Cast<object>().Count() ?? 0;
            var totalCount = _allWorkItems.Count;

            if (string.IsNullOrEmpty(FilterText))
            {
                StatusMessage = $"表示中: {totalCount}件";
            }
            else
            {
                StatusMessage = $"表示中: {filteredCount}件 / 全{totalCount}件";
            }
        }

        private bool ValidateInput()
        {
            var validation = _configService.ValidateConfiguration();
            if (!validation.isValid)
            {
                SetError($"共通設定エラー: {validation.errorMessage}");
                return false;
            }

            if (string.IsNullOrWhiteSpace(Project))
            {
                SetError("プロジェクト名を入力してください。");
                return false;
            }

            return true;
        }

        private bool CanExecuteConnection()
        {
            return !IsLoading && _configService.IsConfigured && !string.IsNullOrWhiteSpace(Project);
        }

        private bool CanExecuteQuery()
        {
            return !IsLoading && _configService.IsConfigured && !string.IsNullOrWhiteSpace(Project);
        }

        private bool CanSaveResult()
        {
            return !string.IsNullOrEmpty(MarkdownPreview);
        }

        private AzureDevOpsConfig CreateAzureDevOpsConfig()
        {
            return _configService.CreateConfig(Project.Trim());
        }

        private WorkItemQueryRequest CreateQueryRequest(AzureDevOpsConfig config)
        {
            var workItemType = WorkItemType == "All" ? "" : WorkItemType;
            var state = State == "All" ? "" : State;
            var iteration = string.IsNullOrWhiteSpace(Iteration) ? "" : Iteration.Trim();
            var area = string.IsNullOrWhiteSpace(Area) ? "" : Area.Trim();

            // 複数選択をサポート
            var projects = new List<string>();
            if (Projects.Any())
            {
                projects.AddRange(Projects.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()));
            }
            else if (!string.IsNullOrWhiteSpace(Project))
            {
                projects.Add(Project.Trim());
            }

            var iterations = new List<string>();
            if (Iterations.Any())
            {
                iterations.AddRange(Iterations.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim()));
            }
            else if (!string.IsNullOrWhiteSpace(Iteration))
            {
                iterations.Add(Iteration.Trim());
            }

            var areas = new List<string>();
            if (Areas.Any())
            {
                areas.AddRange(Areas.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()));
            }
            else if (!string.IsNullOrWhiteSpace(Area))
            {
                areas.Add(Area.Trim());
            }

            return new WorkItemQueryRequest
            {
                Organization = config.Organization,
                Project = config.Project, // 後方互換性のため
                Projects = projects,
                WorkItemType = workItemType,
                State = state,
                AssignedTo = AssignedTo,
                IterationPath = iteration, // 後方互換性のため
                AreaPath = area, // 後方互換性のため
                IterationPaths = iterations,
                AreaPaths = areas,
                MaxResults = Math.Min(MaxResults, 200) // 安全のため最大200に制限
            };
        }

        private void LoadSettings()
        {
            try
            {
                var settings = SettingsService.LoadSettings();
                if (settings.AzureDevOps != null)
                {
                    var azureSettings = settings.AzureDevOps;

                    Project = azureSettings.Project;
                    Projects = azureSettings.Projects ?? new List<string>();
                    MaxResults = azureSettings.MaxResults;
                    WorkItemType = azureSettings.WorkItemType;
                    State = azureSettings.State;
                    Iteration = azureSettings.Iteration ?? "";
                    Iterations = azureSettings.Iterations ?? new List<string>();
                    Area = azureSettings.Area ?? "";
                    Areas = azureSettings.Areas ?? new List<string>();
                    DetailedMarkdown = azureSettings.DetailedMarkdown;
                    HighlightDays = azureSettings.HighlightDays;
                    EnableHighlight = azureSettings.EnableHighlight;
                    
                    // プレビュー更新通知
                    OnPropertyChanged(nameof(ProjectsPreview));
                    OnPropertyChanged(nameof(IterationsPreview));
                    OnPropertyChanged(nameof(AreasPreview));
                    OnPropertyChanged(nameof(SearchCriteriaSummary));
                }
                
                // 共通設定をロード
                _configService.LoadConfiguration();
                
                // 共通設定関連のプロパティを更新通知
                OnPropertyChanged(nameof(Organization));
                OnPropertyChanged(nameof(PatToken));
                OnPropertyChanged(nameof(IsSharedConfigured));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AzureDevOps設定読み込みエラー: {ex.Message}");
            }
        }

        public override async Task CleanupAsync()
        {
            // 共通設定のイベントを解除
            _configService.ConfigurationChanged -= OnSharedConfigChanged;
            
            // HTMLキャッシュをクリーンアップ
            try
            {
                HtmlContentCache.Instance.RemoveExpiredEntries();
                System.Diagnostics.Debug.WriteLine("HTML cache cleanup completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HTML cache cleanup error: {ex.Message}");
            }
            
            // 設定を保存
            try
            {
                await SaveSettingsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AzureDevOps設定保存エラー: {ex.Message}");
            }

            await base.CleanupAsync();
        }

        private async Task SaveSettingsAsync()
        {
            var settings = SettingsService.LoadSettings();
            
            // 共通設定を保存
            _configService.SaveConfiguration();
            
            // プラグイン固有設定を保存
            if (settings.AzureDevOps == null)
            {
                settings.AzureDevOps = new SettingsService.AzureDevOpsSettings();
            }
            
            settings.AzureDevOps.Project = Project.Trim();
            settings.AzureDevOps.Projects = Projects;
            settings.AzureDevOps.WorkItemType = WorkItemType;
            settings.AzureDevOps.State = State;
            settings.AzureDevOps.Iteration = Iteration;
            settings.AzureDevOps.Iterations = Iterations;
            settings.AzureDevOps.Area = Area;
            settings.AzureDevOps.Areas = Areas;
            settings.AzureDevOps.MaxResults = MaxResults;
            settings.AzureDevOps.DetailedMarkdown = DetailedMarkdown;
            settings.AzureDevOps.HighlightDays = HighlightDays;
            settings.AzureDevOps.EnableHighlight = EnableHighlight;

            SettingsService.SaveSettings(settings);
            await Task.CompletedTask;
        }

        #region Advanced Filter Methods

        private void RegisterFilterableFields()
        {
            var fields = new List<FilterableField>
            {
                FilterableField.CreateStringField("Fields.Title", "Title"),
                FilterableField.CreateStringField("Fields.AssignedTo.DisplayName", "Assigned To"),
                FilterableField.CreateStringField("Fields.WorkItemType", "Work Item Type"),
                FilterableField.CreateStringField("Fields.State", "State"),
                FilterableField.CreateStringField("Fields.Priority", "Priority"),
                FilterableField.CreateDateField("Fields.CreatedDate", "Created Date"),
                FilterableField.CreateDateField("Fields.ChangedDate", "Last Updated"),
                FilterableField.CreateStringField("Id", "ID")
            };

            AdvancedFilterService.Instance.RegisterPluginFields("GadgetTools.TicketManage", fields);
        }

        private void ShowAdvancedFilter()
        {
            try
            {
                var filterViewModel = new AdvancedFilterViewModel("GadgetTools.TicketManage");
                var filterView = new AdvancedFilterView { DataContext = filterViewModel };

                var window = new System.Windows.Window
                {
                    Title = "Advanced Filter - Ticket Management",
                    Content = filterView,
                    Width = 800,
                    Height = 600,
                    WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
                    Owner = System.Windows.Application.Current.MainWindow
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

            var window = new System.Windows.Window
            {
                Title = "Global Search",
                Content = searchView,
                Width = 900,
                Height = 700,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
                Owner = System.Windows.Application.Current.MainWindow
            };

            window.ShowDialog();
        }

        private void ApplyAdvancedFilter(List<FilterCondition> conditions)
        {
            if (!conditions.Any())
            {
                // フィルタをクリア
                _workItemsViewSource.Filter -= OnAdvancedFilter;
                _workItemsViewSource.View?.Refresh();
                return;
            }

            // 高度なフィルタを適用
            _workItemsViewSource.Filter -= OnAdvancedFilter;
            _workItemsViewSource.Filter += OnAdvancedFilter;
            _advancedFilterConditions = conditions;
            _workItemsViewSource.View?.Refresh();
        }

        private List<FilterCondition> _advancedFilterConditions = new();

        private void OnAdvancedFilter(object sender, FilterEventArgs e)
        {
            if (e.Item is not WorkItem workItem)
            {
                e.Accepted = false;
                return;
            }

            // 既存の基本フィルタを適用
            OnWorkItemsFilter(sender, e);
            
            if (!e.Accepted) return;

            // 高度なフィルタを適用
            e.Accepted = AdvancedFilterService.Instance.ApplyFilter(workItem, _advancedFilterConditions);
        }


        private void OnSharedConfigChanged(object? sender, EventArgs e)
        {
            // 共通設定関連のプロパティを更新通知
            OnPropertyChanged(nameof(Organization));
            OnPropertyChanged(nameof(PatToken));
            OnPropertyChanged(nameof(IsSharedConfigured));
            
            // コマンドの有効状態を更新
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }

        #endregion

        #region Column Filter Methods

        private void ShowColumnFilter(string? columnName)
        {
            if (string.IsNullOrEmpty(columnName) || WorkItems.Count == 0)
                return;

            System.Diagnostics.Debug.WriteLine($"Showing filter for column: {columnName}");
            // この実装では、ViewでPopupを表示する必要があります
            // ViewModelからPopupを直接制御することはできないため、
            // イベントを発火してViewで処理します
            ColumnFilterRequested?.Invoke(this, new ColumnFilterRequestedEventArgs(columnName));
        }

        private void ClearColumnFilters()
        {
            _columnFilterManager.ClearAllFilters();
        }

        private void OnColumnFilterChanged(object? sender, EventArgs e)
        {
            RefreshWorkItemsView();
        }

        private void UpdateColumnFilterData()
        {
            if (WorkItems.Count == 0) return;

            try
            {
                // 各列のデータを登録
                _columnFilterManager.RegisterColumn("ID", WorkItems.Select(w => (object)w.Id));
                _columnFilterManager.RegisterColumn("Type", WorkItems.Select(w => (object)(w.Fields.WorkItemType ?? "")));
                _columnFilterManager.RegisterColumn("Title", WorkItems.Select(w => (object)(w.Fields.Title ?? "")));
                _columnFilterManager.RegisterColumn("State", WorkItems.Select(w => (object)(w.Fields.State ?? "")));
                _columnFilterManager.RegisterColumn("Assigned To", WorkItems.Select(w => (object)(w.Fields.AssignedTo?.DisplayName ?? "")));
                _columnFilterManager.RegisterColumn("Priority", WorkItems.Select(w => (object)w.Fields.Priority.ToString()));
                _columnFilterManager.RegisterColumn("Created", WorkItems.Select(w => (object)(w.Fields.CreatedDate.ToString("yyyy-MM-dd"))));
                _columnFilterManager.RegisterColumn("Last Updated", WorkItems.Select(w => (object)(w.Fields.ChangedDate.ToString("yyyy-MM-dd HH:mm"))));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating column filter data: {ex.Message}");
            }
        }

        public event EventHandler<ColumnFilterRequestedEventArgs>? ColumnFilterRequested;
        public event EventHandler? ShowColumnVisibilityRequested;

        private void ShowColumnVisibility()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("ShowColumnVisibility command executed");
                ShowColumnVisibilityRequested?.Invoke(this, EventArgs.Empty);
                System.Diagnostics.Debug.WriteLine("ShowColumnVisibilityRequested event fired");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ShowColumnVisibility: {ex.Message}");
            }
        }

        #endregion

        #region Enhanced Search Methods

        private void LoadSavedQueries()
        {
            try
            {
                SavedQueries.Clear();
                var queries = _savedQueryService.GetSavedQueries();
                foreach (var query in queries)
                {
                    SavedQueries.Add(query);
                }
            }
            catch (Exception ex)
            {
                SetError($"保存済みクエリの読み込みに失敗しました: {ex.Message}");
            }
        }

        private void LoadQuickFilters()
        {
            try
            {
                QuickFilters.Clear();
                var filters = _savedQueryService.GetQuickFilters();
                foreach (var filter in filters)
                {
                    QuickFilters.Add(filter);
                }
            }
            catch (Exception ex)
            {
                SetError($"クイックフィルタの読み込みに失敗しました: {ex.Message}");
            }
        }

        private bool CanSaveCurrentQuery()
        {
            return !string.IsNullOrWhiteSpace(Projects.FirstOrDefault()) || 
                   !string.IsNullOrWhiteSpace(TitleSearch) ||
                   !string.IsNullOrWhiteSpace(WorkItemType) ||
                   CreatedAfter.HasValue || CreatedBefore.HasValue ||
                   UpdatedAfter.HasValue || UpdatedBefore.HasValue;
        }

        private void SaveCurrentQuery()
        {
            try
            {
                var dialog = new SaveQueryDialog();
                if (dialog.ShowDialog() == true)
                {
                    var query = new SavedQuery
                    {
                        Name = dialog.QueryName,
                        Description = dialog.QueryDescription,
                        Projects = new List<string>(Projects),
                        Areas = new List<string>(Areas),
                        Iterations = new List<string>(Iterations),
                        WorkItemType = WorkItemType != "All" ? WorkItemType : "",
                        State = State != "All" ? State : "",
                        AssignedTo = AssignedTo,
                        MaxResults = MaxResults,
                        TitleSearch = TitleSearch,
                        DescriptionSearch = DescriptionSearch,
                        CreatedAfter = CreatedAfter,
                        CreatedBefore = CreatedBefore,
                        UpdatedAfter = UpdatedAfter,
                        UpdatedBefore = UpdatedBefore,
                        Tags = TagsSearch.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList(),
                        MinPriority = MinPriority,
                        MaxPriority = MaxPriority
                    };

                    _savedQueryService.SaveQuery(query);
                    LoadSavedQueries();
                    // クエリ保存成功のメッセージ（ログまたは通知で代替）
                    System.Diagnostics.Debug.WriteLine($"クエリ '{query.Name}' を保存しました。");
                }
            }
            catch (Exception ex)
            {
                SetError($"クエリの保存に失敗しました: {ex.Message}");
            }
        }

        private void LoadSavedQuery(SavedQuery? query)
        {
            if (query == null) return;

            try
            {
                Projects = new List<string>(query.Projects);
                Areas = new List<string>(query.Areas);
                Iterations = new List<string>(query.Iterations);
                WorkItemType = string.IsNullOrEmpty(query.WorkItemType) ? "All" : query.WorkItemType;
                State = string.IsNullOrEmpty(query.State) ? "All" : query.State;
                AssignedTo = query.AssignedTo;
                MaxResults = query.MaxResults;
                TitleSearch = query.TitleSearch;
                DescriptionSearch = query.DescriptionSearch;
                CreatedAfter = query.CreatedAfter;
                CreatedBefore = query.CreatedBefore;
                UpdatedAfter = query.UpdatedAfter;
                UpdatedBefore = query.UpdatedBefore;
                TagsSearch = string.Join(", ", query.Tags);
                MinPriority = query.MinPriority;
                MaxPriority = query.MaxPriority;

                _savedQueryService.UseQuery(query.Id);
                LoadSavedQueries(); // Refresh to update last used date
                System.Diagnostics.Debug.WriteLine($"クエリ '{query.Name}' を適用しました。");
            }
            catch (Exception ex)
            {
                SetError($"クエリの読み込みに失敗しました: {ex.Message}");
            }
        }

        private void DeleteSavedQuery(string? queryId)
        {
            if (string.IsNullOrEmpty(queryId)) return;

            try
            {
                var query = _savedQueryService.GetQueryById(queryId);
                if (query != null)
                {
                    var result = System.Windows.MessageBox.Show(
                        $"クエリ '{query.Name}' を削除しますか？",
                        "確認",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question);

                    if (result == System.Windows.MessageBoxResult.Yes)
                    {
                        _savedQueryService.DeleteQuery(queryId);
                        LoadSavedQueries();
                        System.Diagnostics.Debug.WriteLine($"クエリ '{query.Name}' を削除しました。");
                    }
                }
            }
            catch (Exception ex)
            {
                SetError($"クエリの削除に失敗しました: {ex.Message}");
            }
        }

        private void ApplyQuickFilter(SearchQuickFilter? filter)
        {
            if (filter == null) return;

            try
            {
                var query = filter.QueryBuilder();
                LoadSavedQuery(query);
                System.Diagnostics.Debug.WriteLine($"クイックフィルタ '{filter.Name}' を適用しました。");
            }
            catch (Exception ex)
            {
                SetError($"クイックフィルタの適用に失敗しました: {ex.Message}");
            }
        }

        private void ClearAdvancedSearch()
        {
            TitleSearch = "";
            DescriptionSearch = "";
            CreatedAfter = null;
            CreatedBefore = null;
            UpdatedAfter = null;
            UpdatedBefore = null;
            TagsSearch = "";
            MinPriority = null;
            MaxPriority = null;
            AssignedTo = "";
        }

        private bool EvaluateEnhancedTextFilters(WorkItem workItem)
        {
            // Basic text filter (global search)
            if (!string.IsNullOrEmpty(FilterText))
            {
                var filterText = FilterText.ToLowerInvariant();
                var searchableText = new[]
                {
                    workItem.Id.ToString(),
                    workItem.Fields.WorkItemType?.ToLowerInvariant() ?? "",
                    workItem.Fields.Title?.ToLowerInvariant() ?? "",
                    workItem.Fields.State?.ToLowerInvariant() ?? "",
                    workItem.Fields.AssignedTo?.DisplayName?.ToLowerInvariant() ?? "",
                    workItem.Fields.Description?.ToLowerInvariant() ?? "",
                    workItem.Fields.Tags?.ToLowerInvariant() ?? ""
                };

                if (!searchableText.Any(text => text.Contains(filterText)))
                    return false;
            }

            // Title search
            if (!string.IsNullOrEmpty(TitleSearch))
            {
                var titleText = workItem.Fields.Title?.ToLowerInvariant() ?? "";
                if (!titleText.Contains(TitleSearch.ToLowerInvariant()))
                    return false;
            }

            // Description search
            if (!string.IsNullOrEmpty(DescriptionSearch))
            {
                var descriptionText = workItem.Fields.Description?.ToLowerInvariant() ?? "";
                if (!descriptionText.Contains(DescriptionSearch.ToLowerInvariant()))
                    return false;
            }

            // Tags search
            if (!string.IsNullOrEmpty(TagsSearch))
            {
                var tagsText = workItem.Fields.Tags?.ToLowerInvariant() ?? "";
                var searchTags = TagsSearch.ToLowerInvariant().Split(',', StringSplitOptions.RemoveEmptyEntries)
                                          .Select(t => t.Trim());
                
                if (!searchTags.Any(tag => tagsText.Contains(tag)))
                    return false;
            }

            // Assigned To search
            if (!string.IsNullOrEmpty(AssignedTo))
            {
                var assignedToText = workItem.Fields.AssignedTo?.DisplayName?.ToLowerInvariant() ?? "";
                if (!assignedToText.Contains(AssignedTo.ToLowerInvariant()))
                    return false;
            }

            // Date filters
            if (CreatedAfter.HasValue && workItem.Fields.CreatedDate < CreatedAfter.Value)
                return false;

            if (CreatedBefore.HasValue && workItem.Fields.CreatedDate > CreatedBefore.Value.AddDays(1))
                return false;

            if (UpdatedAfter.HasValue && workItem.Fields.ChangedDate < UpdatedAfter.Value)
                return false;

            if (UpdatedBefore.HasValue && workItem.Fields.ChangedDate > UpdatedBefore.Value.AddDays(1))
                return false;

            // Priority filters
            if (MinPriority.HasValue && workItem.Fields.Priority < MinPriority.Value)
                return false;

            if (MaxPriority.HasValue && workItem.Fields.Priority > MaxPriority.Value)
                return false;

            return true;
        }

        private void SetupKeyboardShortcuts()
        {
            _keyboardShortcutService.ShortcutExecuted += OnShortcutExecuted;
        }

        private void OnShortcutExecuted(object? sender, ShortcutExecutedEventArgs e)
        {
            try
            {
                var handled = e.ShortcutId switch
                {
                    "Refresh" => ExecuteRefreshShortcut(),
                    "Save" => ExecuteSaveShortcut(),
                    "Find" => ExecuteFindShortcut(),
                    "ClearFilter" => ExecuteClearFilterShortcut(),
                    "SelectAll" => ExecuteSelectAllShortcut(),
                    "Copy" => ExecuteCopyShortcut(),
                    "OpenItem" => ExecuteOpenItemShortcut(),
                    "MyItems" => ExecuteMyItemsShortcut(),
                    "HighPriority" => ExecuteHighPriorityShortcut(),
                    "Bugs" => ExecuteBugsShortcut(),
                    "Active" => ExecuteActiveShortcut(),
                    "SaveQuery" => ExecuteSaveQueryShortcut(),
                    "AdvancedSearch" => ExecuteAdvancedSearchShortcut(),
                    "ColumnFilter" => ExecuteColumnFilterShortcut(),
                    "ColumnVisibility" => ExecuteColumnVisibilityShortcut(),
                    _ => false
                };

                e.Handled = handled;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error executing keyboard shortcut {e.ShortcutId}: {ex.Message}");
            }
        }

        private bool ExecuteRefreshShortcut()
        {
            if (QueryWorkItemsCommand.CanExecute(null))
            {
                QueryWorkItemsCommand.Execute(null);
                return true;
            }
            return false;
        }

        private bool ExecuteSaveShortcut()
        {
            if (SaveResultCommand.CanExecute(null))
            {
                SaveResultCommand.Execute(null);
                return true;
            }
            return false;
        }

        private bool ExecuteFindShortcut()
        {
            // Request focus on search box - this will be handled by the View
            RequestFocusOnSearch?.Invoke(this, EventArgs.Empty);
            return true;
        }

        private bool ExecuteClearFilterShortcut()
        {
            ClearFilterCommand.Execute(null);
            return true;
        }

        private bool ExecuteSelectAllShortcut()
        {
            // This would require DataGrid reference - handle in View
            RequestSelectAll?.Invoke(this, EventArgs.Empty);
            return true;
        }

        private bool ExecuteCopyShortcut()
        {
            if (SelectedWorkItem != null)
            {
                var text = $"#{SelectedWorkItem.Id}: {SelectedWorkItem.Fields.Title}";
                try
                {
                    System.Windows.Clipboard.SetText(text);
                    System.Diagnostics.Debug.WriteLine($"Copied work item to clipboard: {text}");
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to copy to clipboard: {ex.Message}");
                }
            }
            return false;
        }

        private bool ExecuteOpenItemShortcut()
        {
            if (SelectedWorkItem != null)
            {
                RequestOpenSelectedItem?.Invoke(this, EventArgs.Empty);
                return true;
            }
            return false;
        }

        private bool ExecuteMyItemsShortcut()
        {
            var myItemsFilter = QuickFilters.FirstOrDefault(f => f.Name == "自分担当");
            if (myItemsFilter != null)
            {
                ApplyQuickFilterCommand.Execute(myItemsFilter);
                return true;
            }
            return false;
        }

        private bool ExecuteHighPriorityShortcut()
        {
            var highPriorityFilter = QuickFilters.FirstOrDefault(f => f.Name == "高優先度");
            if (highPriorityFilter != null)
            {
                ApplyQuickFilterCommand.Execute(highPriorityFilter);
                return true;
            }
            return false;
        }

        private bool ExecuteBugsShortcut()
        {
            var bugsFilter = QuickFilters.FirstOrDefault(f => f.Name == "バグ");
            if (bugsFilter != null)
            {
                ApplyQuickFilterCommand.Execute(bugsFilter);
                return true;
            }
            return false;
        }

        private bool ExecuteActiveShortcut()
        {
            var activeFilter = QuickFilters.FirstOrDefault(f => f.Name == "未解決");
            if (activeFilter != null)
            {
                ApplyQuickFilterCommand.Execute(activeFilter);
                return true;
            }
            return false;
        }

        private bool ExecuteSaveQueryShortcut()
        {
            if (SaveCurrentQueryCommand.CanExecute(null))
            {
                SaveCurrentQueryCommand.Execute(null);
                return true;
            }
            return false;
        }

        private bool ExecuteAdvancedSearchShortcut()
        {
            ShowAdvancedFilterCommand.Execute(null);
            return true;
        }

        private bool ExecuteColumnFilterShortcut()
        {
            RequestColumnFilter?.Invoke(this, EventArgs.Empty);
            return true;
        }

        private bool ExecuteColumnVisibilityShortcut()
        {
            ShowColumnVisibilityCommand.Execute(null);
            return true;
        }

        // Events for View interaction
        public event EventHandler? RequestFocusOnSearch;
        public event EventHandler? RequestSelectAll;
        public event EventHandler? RequestOpenSelectedItem;
        public event EventHandler? RequestColumnFilter;

        public bool HandleKeyboardInput(System.Windows.Input.Key key, System.Windows.Input.ModifierKeys modifiers)
        {
            return _keyboardShortcutService.HandleKeyInput(key, modifiers, "TicketManage");
        }

        #endregion

        #region Context Menu Methods

        private bool CanExecuteSelectedItemCommand()
        {
            return SelectedWorkItem != null;
        }

        private bool CanExecuteSelectedItemCommand<T>(T parameter)
        {
            return SelectedWorkItem != null;
        }

        private void CopyWorkItemId()
        {
            if (SelectedWorkItem != null)
            {
                try
                {
                    System.Windows.Clipboard.SetText(SelectedWorkItem.Id.ToString());
                    System.Diagnostics.Debug.WriteLine($"Copied work item ID: {SelectedWorkItem.Id}");
                }
                catch (Exception ex)
                {
                    SetError($"IDのコピーに失敗しました: {ex.Message}");
                }
            }
        }

        private void CopyWorkItemTitle()
        {
            if (SelectedWorkItem != null)
            {
                try
                {
                    var title = SelectedWorkItem.Fields.Title ?? "";
                    System.Windows.Clipboard.SetText(title);
                    System.Diagnostics.Debug.WriteLine($"Copied work item title: {title}");
                }
                catch (Exception ex)
                {
                    SetError($"タイトルのコピーに失敗しました: {ex.Message}");
                }
            }
        }

        private void CopyWorkItemUrl()
        {
            if (SelectedWorkItem != null)
            {
                try
                {
                    var projectName = !string.IsNullOrEmpty(SelectedWorkItem.Fields.TeamProject) 
                        ? SelectedWorkItem.Fields.TeamProject 
                        : Project;
                    
                    var url = $"https://dev.azure.com/{Organization}/{projectName}/_workitems/edit/{SelectedWorkItem.Id}";
                    System.Windows.Clipboard.SetText(url);
                    System.Diagnostics.Debug.WriteLine($"Copied work item URL: {url}");
                }
                catch (Exception ex)
                {
                    SetError($"URLのコピーに失敗しました: {ex.Message}");
                }
            }
        }

        private void OpenInBrowser()
        {
            if (SelectedWorkItem != null)
            {
                try
                {
                    var projectName = !string.IsNullOrEmpty(SelectedWorkItem.Fields.TeamProject) 
                        ? SelectedWorkItem.Fields.TeamProject 
                        : Project;
                    
                    var url = $"https://dev.azure.com/{Organization}/{projectName}/_workitems/edit/{SelectedWorkItem.Id}";
                    
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    SetError($"ブラウザで開くのに失敗しました: {ex.Message}");
                }
            }
        }

        private void SetPriority(string? priority)
        {
            if (SelectedWorkItem != null && !string.IsNullOrEmpty(priority))
            {
                if (int.TryParse(priority, out var priorityValue))
                {
                    // This would require Azure DevOps API update call
                    System.Diagnostics.Debug.WriteLine($"Would set priority to {priorityValue} for work item {SelectedWorkItem.Id}");
                    // TODO: Implement actual priority update via API
                    System.Windows.MessageBox.Show(
                        "優先度の変更機能は将来のバージョンで実装予定です。",
                        "機能未実装",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
            }
        }

        private void ChangeState(string? state)
        {
            if (SelectedWorkItem != null && !string.IsNullOrEmpty(state))
            {
                // This would require Azure DevOps API update call
                System.Diagnostics.Debug.WriteLine($"Would change state to {state} for work item {SelectedWorkItem.Id}");
                // TODO: Implement actual state update via API
                System.Windows.MessageBox.Show(
                    "状態の変更機能は将来のバージョンで実装予定です。",
                    "機能未実装",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
        }

        private void AssignToMe()
        {
            if (SelectedWorkItem != null)
            {
                // This would require Azure DevOps API update call
                System.Diagnostics.Debug.WriteLine($"Would assign work item {SelectedWorkItem.Id} to current user");
                // TODO: Implement actual assignment via API
                System.Windows.MessageBox.Show(
                    "担当者の変更機能は将来のバージョンで実装予定です。",
                    "機能未実装",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
        }

        private void AddToWatchlist()
        {
            if (SelectedWorkItem != null)
            {
                // This would add to a local watchlist
                System.Diagnostics.Debug.WriteLine($"Would add work item {SelectedWorkItem.Id} to watchlist");
                // TODO: Implement watchlist functionality
                System.Windows.MessageBox.Show(
                    "ウォッチリスト機能は将来のバージョンで実装予定です。",
                    "機能未実装",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
        }

        private void ShowAreaChart()
        {
            try
            {
                // フィルタリングされたデータを取得
                var filteredWorkItems = WorkItemsView.Cast<WorkItem>().ToList();
                
                if (filteredWorkItems.Count == 0)
                {
                    System.Windows.MessageBox.Show(
                        "表示するデータがありません。フィルタ条件を確認してください。",
                        "情報",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                    return;
                }
                
                var chartWindow = new AreaChartWindow(filteredWorkItems);
                chartWindow.Title = $"ワークアイテム集計チャート ({filteredWorkItems.Count} 件)";
                
                // ドリルダウンイベントを処理
                chartWindow.ChartElementClicked += OnChartElementClicked;
                
                chartWindow.Show();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening area chart: {ex.Message}");
                System.Windows.MessageBox.Show(
                    $"チャートの表示中にエラーが発生しました: {ex.Message}",
                    "エラー",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private async void OnChartElementClicked(object sender, ChartDrillDownEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"OnChartElementClicked triggered: Label={e.Label}, WorkItems={e.WorkItems.Count}");
                
                // チャートウィンドウを閉じる（オプション）
                if (sender is AreaChartWindow chartWindow)
                {
                    System.Diagnostics.Debug.WriteLine("Closing chart window");
                    chartWindow.Close();
                }

                // ドリルダウンフィルタを適用
                System.Diagnostics.Debug.WriteLine("Applying drill-down filter");
                await ApplyChartDrillDown(e.Label, e.CategoryType, e.WorkItems);
                
                // メインウィンドウをアクティブにする
                System.Windows.Application.Current.MainWindow?.Activate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling chart drill-down: {ex.Message}");
                SetError($"ドリルダウンの処理中にエラーが発生しました: {ex.Message}");
            }
        }

        private bool CanShowAreaChart()
        {
            // フィルタリングされたデータがあるかチェック
            return WorkItemsView != null && WorkItemsView.Cast<WorkItem>().Any();
        }

        private void LoadSampleData()
        {
            try
            {
                var sampleData = GenerateSampleWorkItems();
                WorkItems.Clear();
                foreach (var item in sampleData)
                {
                    WorkItems.Add(item);
                }

                System.Diagnostics.Debug.WriteLine($"Loaded {sampleData.Count} sample work items");
                System.Windows.MessageBox.Show(
                    $"サンプルデータを{sampleData.Count}件読み込みました。\n\n機能別・優先度別分析をテストできます。",
                    "サンプルデータ読み込み完了",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading sample data: {ex.Message}");
                System.Windows.MessageBox.Show(
                    $"サンプルデータの読み込み中にエラーが発生しました: {ex.Message}",
                    "エラー",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 45件のダミーワークアイテムを生成
        /// </summary>
        private List<WorkItem> GenerateSampleWorkItems()
        {
            var features = new[]
            {
                "UserManagement", "AuthSystem", "ReportFeature", "DataSync", "UIImprovement",
                "Performance", "Security", "Backup", "NotificationSystem", "SearchFeature",
                "FileManagement", "Settings", "AuditLog", "Export", "Import"
            };

            var areas = new[]
            {
                @"PersonalProject\Frontend\UI",
                @"PersonalProject\Frontend\Components",
                @"PersonalProject\Backend\API",
                @"PersonalProject\Backend\Database",
                @"PersonalProject\Backend\Services",
                @"PersonalProject\Infrastructure\Security",
                @"PersonalProject\Infrastructure\Monitoring",
                @"PersonalProject\Testing\Automation",
                @"PersonalProject\Testing\Manual",
                @"PersonalProject\Documentation"
            };

            var titleTemplates = new[]
            {
                "[{0}] Login error occurs",
                "[{0}] Data not displayed correctly",
                "[{0}] Save button not responsive",
                "[{0}] UI layout broken",
                "[{0}] Performance degradation",
                "{0}: Memory leak detected",
                "{0}: Exception handling improper",
                "{0}: Validation error",
                "{0}: Timeout occurs",
                "{0} - UI not working correctly",
                "{0} - Data integrity error",
                "{0} - Permission check fails",
                "{0}_API call error",
                "{0}_Database connection failed",
                "{0}_File read error"
            };

            var states = new[] { "Active", "Active", "Active", "Resolved", "Closed" };
            var priorities = new[] { 1, 1, 2, 2, 2, 3, 3, 3, 4 };

            var workItems = new List<WorkItem>();
            var random = new Random();

            for (int i = 1; i <= 45; i++)
            {
                var feature = features[random.Next(features.Length)];
                var area = areas[random.Next(areas.Length)];
                var titleTemplate = titleTemplates[random.Next(titleTemplates.Length)];
                var state = states[random.Next(states.Length)];
                var priority = priorities[random.Next(priorities.Length)];

                var title = string.Format(titleTemplate, feature);

                var workItem = new WorkItem
                {
                    Id = i,
                    Rev = 1,
                    Url = $"https://dev.azure.com/aksh0402/PersonalProject/_workitems/edit/{i}",
                    Fields = new WorkItemFields
                    {
                        SystemId = i,
                        Title = title,
                        Description = $"Issue found in {feature} functionality. Priority: {GetPriorityText(priority)}",
                        WorkItemType = "Bug",
                        State = state,
                        AreaPath = area,
                        Priority = priority,
                        CreatedDate = DateTime.Now.AddDays(-random.Next(30)),
                        ChangedDate = DateTime.Now.AddDays(-random.Next(7)),
                        AssignedTo = GenerateAssignedPerson(random),
                        CreatedBy = GenerateAssignedPerson(random),
                        TeamProject = "PersonalProject",
                        Severity = GetSeverity(priority),
                        Tags = $"bug;{feature.ToLower()};test"
                    }
                };

                workItems.Add(workItem);
            }

            return workItems;
        }

        private string GetSeverity(int priority)
        {
            return priority switch
            {
                1 => "1 - Critical",
                2 => "2 - High",
                3 => "3 - Medium",
                4 => "4 - Low",
                _ => "3 - Medium"
            };
        }

        private string GetPriorityText(int priority)
        {
            return priority switch
            {
                1 => "Critical",
                2 => "High",
                3 => "Medium",
                4 => "Low",
                _ => "Medium"
            };
        }

        private AssignedPerson? GenerateAssignedPerson(Random random)
        {
            var people = new[]
            {
                new AssignedPerson { DisplayName = "Alice Johnson" },
                new AssignedPerson { DisplayName = "Bob Smith" },
                new AssignedPerson { DisplayName = "Carol Davis" },
                new AssignedPerson { DisplayName = "David Wilson" },
                new AssignedPerson { DisplayName = "Eve Brown" },
                null // 未割当
            };

            return people[random.Next(people.Length)];
        }

        #endregion

        #region Chart Drill-down functionality

        /// <summary>
        /// Apply drill-down filter from chart selection
        /// </summary>
        public async Task ApplyChartDrillDown(string categoryLabel, CategoryType categoryType, List<GadgetTools.Shared.Models.WorkItem> workItems)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"ApplyChartDrillDown started: Label={categoryLabel}, Type={categoryType}, WorkItems={workItems.Count}");
                
                // Update the work items collection with the filtered data
                System.Diagnostics.Debug.WriteLine($"Clearing _allWorkItems (currently has {_allWorkItems.Count} items)");
                _allWorkItems.Clear();
                
                System.Diagnostics.Debug.WriteLine($"Clearing WorkItems ObservableCollection (currently has {WorkItems.Count} items)");
                WorkItems.Clear();
                
                System.Diagnostics.Debug.WriteLine($"Adding {workItems.Count} filtered work items to both collections");
                foreach (var item in workItems)
                {
                    _allWorkItems.Add(item);
                    WorkItems.Add(item);
                }
                System.Diagnostics.Debug.WriteLine($"_allWorkItems now has {_allWorkItems.Count} items, WorkItems has {WorkItems.Count} items");

                // Clear any existing text filter
                System.Diagnostics.Debug.WriteLine($"Clearing FilterText (was: '{FilterText}')");
                FilterText = "";

                // Update the view
                System.Diagnostics.Debug.WriteLine("Refreshing view source");
                _workItemsViewSource.View?.Refresh();

                // Update status message to show drill-down context
                var categoryTypeName = GetCategoryTypeDisplayName(categoryType);
                var statusMsg = $"ドリルダウン: {categoryTypeName} '{categoryLabel}' ({workItems.Count}件)";
                System.Diagnostics.Debug.WriteLine($"Setting StatusMessage to: {statusMsg}");
                StatusMessage = statusMsg;

                // Generate preview for filtered data
                System.Diagnostics.Debug.WriteLine("Generating markdown preview");
                GenerateMarkdownPreview();

                System.Diagnostics.Debug.WriteLine("Triggering property change notifications");
                OnPropertyChanged(nameof(WorkItemsView));
                OnPropertyChanged(nameof(SelectedWorkItem));
                OnPropertyChanged(nameof(StatusMessage));
                
                // Force CommandManager to re-evaluate can execute for all commands
                System.Diagnostics.Debug.WriteLine("Forcing CommandManager re-evaluation");
                CommandManager.InvalidateRequerySuggested();
                
                System.Diagnostics.Debug.WriteLine("ApplyChartDrillDown completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ApplyChartDrillDown: {ex.Message}");
                SetError($"ドリルダウンの適用に失敗しました: {ex.Message}");
            }
        }

        /// <summary>
        /// Execute clear drill-down command (sync wrapper for async method)
        /// </summary>
        private async void ExecuteClearDrillDown()
        {
            await ClearDrillDown();
        }

        /// <summary>
        /// Check if drill-down can be cleared (always true when there are work items)
        /// </summary>
        private bool CanClearDrillDown()
        {
            var canExecute = WorkItems.Count > 0;
            System.Diagnostics.Debug.WriteLine($"CanClearDrillDown: {canExecute} (WorkItems.Count={WorkItems.Count})");
            return canExecute;
        }

        private string GetCategoryTypeDisplayName(CategoryType categoryType)
        {
            return categoryType switch
            {
                CategoryType.Area => "エリア",
                CategoryType.Feature => "機能",
                CategoryType.Priority => "優先度",
                CategoryType.WorkItemType => "種類",
                CategoryType.State => "状態",
                CategoryType.AssignedTo => "担当者",
                _ => "分類"
            };
        }

        /// <summary>
        /// Clear drill-down filter and return to original view
        /// </summary>
        public async Task ClearDrillDown()
        {
            try
            {
                // Reload original data
                IsLoading = true;
                await QueryWorkItemsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ClearDrillDown: {ex.Message}");
                SetError($"ドリルダウンのクリアに失敗しました: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        #endregion
    }

    public class ColumnFilterRequestedEventArgs : EventArgs
    {
        public string ColumnName { get; }

        public ColumnFilterRequestedEventArgs(string columnName)
        {
            ColumnName = columnName;
        }
    }
}