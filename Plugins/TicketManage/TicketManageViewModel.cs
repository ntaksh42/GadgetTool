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
using GadgetTools.Models;
using GadgetTools.Services;

namespace GadgetTools.Plugins.TicketManage
{
    public class TicketManageViewModel : PluginViewModelBase
    {
        #region Private Fields
        private readonly AzureDevOpsConfigService _configService;
        private string _project = string.Empty;
        private string _workItemType = "All";
        private string _state = "All";
        private string _iteration = "";
        private string _area = "";
        private int _maxResults = 50;
        private bool _detailedMarkdown = true;
        private int _highlightDays = 7;
        private bool _enableHighlight = true;
        private string _filterText = string.Empty;
        private string _markdownPreview = string.Empty;
        private WorkItem? _selectedWorkItem;
        private bool _isConnected = false;

        private readonly CollectionViewSource _workItemsViewSource = new();
        private readonly List<WorkItem> _allWorkItems = new();
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

        public string WorkItemType
        {
            get => _workItemType;
            set => SetProperty(ref _workItemType, value);
        }

        public string State
        {
            get => _state;
            set => SetProperty(ref _state, value);
        }

        public string Iteration
        {
            get => _iteration;
            set => SetProperty(ref _iteration, value);
        }

        public string Area
        {
            get => _area;
            set => SetProperty(ref _area, value);
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

        public WorkItem? SelectedWorkItem
        {
            get => _selectedWorkItem;
            set
            {
                if (SetProperty(ref _selectedWorkItem, value) && value != null)
                {
                    ShowWorkItemDetail(value);
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

            _workItemsViewSource.Source = WorkItems;
            _workItemsViewSource.Filter += OnWorkItemsFilter;

            // 設定を読み込み
            LoadSettings();
            
            // フィルタ可能フィールドを登録
            RegisterFilterableFields();
            
            // 共通設定の変更を監視
            _configService.ConfigurationChanged += OnSharedConfigChanged;
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
                if (string.IsNullOrEmpty(FilterText))
                {
                    e.Accepted = true;
                    return;
                }

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

                e.Accepted = searchableText.Any(text => text.Contains(filterText));
            }
            else
            {
                e.Accepted = false;
            }
        }

        private void ShowWorkItemDetail(WorkItem workItem)
        {
            var converter = new AzureDevOpsMarkdownConverter();
            var detailMarkdown = converter.ConvertWorkItemsToMarkdown(
                new List<WorkItem> { workItem },
                $"Work Item #{workItem.Id} Details"
            );
            MarkdownPreview = detailMarkdown;
        }

        private void GenerateMarkdownPreview()
        {
            if (WorkItems.Count == 0)
            {
                MarkdownPreview = "";
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

            return new WorkItemQueryRequest
            {
                Organization = config.Organization,
                Project = config.Project,
                WorkItemType = workItemType,
                State = state,
                IterationPath = iteration,
                AreaPath = area,
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
                    MaxResults = azureSettings.MaxResults;
                    WorkItemType = azureSettings.WorkItemType;
                    State = azureSettings.State;
                    Iteration = azureSettings.Iteration ?? "";
                    Area = azureSettings.Area ?? "";
                    DetailedMarkdown = azureSettings.DetailedMarkdown;
                    HighlightDays = azureSettings.HighlightDays;
                    EnableHighlight = azureSettings.EnableHighlight;
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
            settings.AzureDevOps.WorkItemType = WorkItemType;
            settings.AzureDevOps.State = State;
            settings.AzureDevOps.Iteration = Iteration;
            settings.AzureDevOps.Area = Area;
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
    }
}