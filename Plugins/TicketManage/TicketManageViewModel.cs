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
        private string _project = string.Empty;
        private List<string> _projects = new List<string>();
        private string _workItemType = "All";
        private string _state = "All";
        private string _iteration = "";
        private string _area = "";
        private List<string> _iterations = new List<string>();
        private List<string> _areas = new List<string>();
        private int _maxResults = 50;
        private bool _detailedMarkdown = true;
        private int _highlightDays = 7;
        private bool _enableHighlight = true;
        private string _filterText = string.Empty;
        private string _markdownPreview = string.Empty;
        private string _htmlPreview = string.Empty;
        private WorkItem? _selectedWorkItem;
        private bool _isConnected = false;

        private readonly CollectionViewSource _workItemsViewSource = new();
        private readonly List<WorkItem> _allWorkItems = new();
        private readonly ColumnFilterManager _columnFilterManager = new();
        #endregion

        #region Collections
        public ObservableCollection<WorkItem> WorkItems { get; } = new();
        public ObservableCollection<string> WorkItemTypes { get; } = new()
        {
            "All", "Bug", "Task", "User Story", "Feature", "Epic"
        };
        public ObservableCollection<string> States { get; } = new()
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

        public ICollectionView WorkItemsView => _workItemsViewSource.View;
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
        #endregion

        public TicketManageViewModel()
        {
            _configService = AzureDevOpsConfigService.Instance;
            
            TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync, CanExecuteConnection);
            QueryWorkItemsCommand = new AsyncRelayCommand(QueryWorkItemsAsync, CanExecuteQuery);
            SaveResultCommand = new RelayCommand(SaveResult, CanSaveResult);
            ClearFilterCommand = new RelayCommand(ClearFilter);
            ShowAdvancedFilterCommand = new RelayCommand(ShowAdvancedFilter);
            ShowGlobalSearchCommand = new RelayCommand(ShowGlobalSearch);
            ShowColumnFilterCommand = new RelayCommand<string>(ShowColumnFilter);
            ClearColumnFiltersCommand = new RelayCommand(ClearColumnFilters);
            ShowColumnVisibilityCommand = new RelayCommand(ShowColumnVisibility);

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
                using var service = new AzureDevOpsService(config);

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

            try
            {
                ClearError();
                IsLoading = true;
                StatusMessage = "ワークアイテムを取得中...";

                var config = CreateAzureDevOpsConfig();
                var request = CreateQueryRequest(config);

                using var service = new AzureDevOpsService(config);
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
        }

        private void OnWorkItemsFilter(object sender, FilterEventArgs e)
        {
            if (e.Item is WorkItem workItem)
            {
                // まず基本的なテキストフィルタをチェック
                bool passesTextFilter = true;
                if (!string.IsNullOrEmpty(FilterText))
                {
                    var filterText = FilterText.ToLower();
                    var searchableText = new[]
                    {
                        workItem.Id.ToString(),
                        workItem.Fields.WorkItemType?.ToLower() ?? "",
                        workItem.Fields.Title?.ToLower() ?? "",
                        workItem.Fields.State?.ToLower() ?? "",
                        workItem.Fields.AssignedTo?.DisplayName?.ToLower() ?? "",
                        workItem.Fields.Description?.ToLower() ?? "",
                        workItem.Fields.Tags?.ToLower() ?? ""
                    };

                    passesTextFilter = searchableText.Any(text => text.Contains(filterText));
                }

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
                    using var service = new AzureDevOpsService(config);
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
                
                // Generate HTML list view asynchronously
                var htmlContent = await Task.Run(() => 
                    TicketHtmlService.GenerateListViewHtml(_allWorkItems, title));
                HtmlPreview = htmlContent;
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