using System.Collections.ObjectModel;
using System.Windows.Input;
using GadgetTools.Core.Models;
using GadgetTools.Core.Services;

namespace GadgetTools.Core.ViewModels
{
    /// <summary>
    /// グローバル検索結果項目
    /// </summary>
    public class SearchResultItem : ViewModelBase
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string PluginId { get; set; } = string.Empty;
        public string PluginName { get; set; } = string.Empty;
        public string PluginColor { get; set; } = "#007ACC";
        public string Icon { get; set; } = "📄";
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string StatusColor { get; set; } = "#28A745";
        public bool HasStatus => !string.IsNullOrEmpty(Status);
        public object? OriginalItem { get; set; }
    }

    /// <summary>
    /// プラグインフィルタ項目
    /// </summary>
    public class PluginFilterItem : ViewModelBase
    {
        private bool _isEnabled = true;

        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Color { get; set; } = "#007ACC";

        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }
    }

    /// <summary>
    /// グローバル検索のViewModel
    /// </summary>
    public class GlobalSearchViewModel : ViewModelBase
    {
        private readonly AdvancedFilterService _filterService;
        private string _searchText = string.Empty;
        private bool _isSearching = false;
        private SearchResultItem? _selectedResult;

        public GlobalSearchViewModel()
        {
            _filterService = AdvancedFilterService.Instance;

            // コマンドの初期化
            SearchCommand = new AsyncRelayCommand(PerformSearchAsync, CanPerformSearch);
            ClearSearchCommand = new RelayCommand(ClearSearch);
            ShowAdvancedFilterCommand = new RelayCommand(ShowAdvancedFilter);
            LoadHistoryCommand = new RelayCommand<SearchHistory>(LoadFromHistory);

            // プラグインフィルタの初期化
            InitializePluginFilters();

            // 検索履歴の読み込み
            LoadRecentSearches();
        }

        #region Properties

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    // リアルタイム検索（デバウンス付き）
                    ScheduleSearch();
                }
            }
        }

        public bool IsSearching
        {
            get => _isSearching;
            set => SetProperty(ref _isSearching, value);
        }

        public SearchResultItem? SelectedResult
        {
            get => _selectedResult;
            set => SetProperty(ref _selectedResult, value);
        }

        public string StatusText => GenerateStatusText();

        public bool HasNoResults => !SearchResults.Any() && !IsSearching && !string.IsNullOrEmpty(SearchText);

        public ObservableCollection<SearchResultItem> SearchResults { get; } = new();
        public ObservableCollection<PluginFilterItem> PluginFilters { get; } = new();
        public ObservableCollection<SearchHistory> RecentSearches { get; } = new();

        #endregion

        #region Commands

        public ICommand SearchCommand { get; }
        public ICommand ClearSearchCommand { get; }
        public ICommand ShowAdvancedFilterCommand { get; }
        public ICommand LoadHistoryCommand { get; }

        #endregion

        #region Events

        // Removed unused event: ResultSelected

        #endregion

        #region Private Fields

        private Timer? _searchTimer;
        private readonly object _searchLock = new object();

        #endregion

        #region Private Methods

        private void InitializePluginFilters()
        {
            // プラグインマネージャーから登録されているプラグインを取得
            var pluginManager = PluginManager.Instance;
            
            foreach (var plugin in pluginManager.LoadedPlugins)
            {
                PluginFilters.Add(new PluginFilterItem
                {
                    Id = plugin.Metadata.Id,
                    Name = plugin.Metadata.DisplayName,
                    Color = GetPluginColor(plugin.Metadata.Id),
                    IsEnabled = true
                });
            }
        }

        private string GetPluginColor(string pluginId)
        {
            return pluginId switch
            {
                "GadgetTools.TicketManage" => "#0078D4",
                "GadgetTools.PullRequestManagement" => "#28A745",
                "GadgetTools.ExcelConverter" => "#17A2B8",
                _ => "#6F42C1"
            };
        }

        private void LoadRecentSearches()
        {
            var allHistory = _filterService.SearchHistory.Take(10).ToList();
            RecentSearches.Clear();
            foreach (var history in allHistory)
            {
                RecentSearches.Add(history);
            }
        }

        private void ScheduleSearch()
        {
            lock (_searchLock)
            {
                _searchTimer?.Dispose();
                _searchTimer = new Timer(OnSearchTimerElapsed, null, 300, Timeout.Infinite);
            }
        }

        private void OnSearchTimerElapsed(object? state)
        {
            App.Current.Dispatcher.Invoke(async () =>
            {
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    await PerformSearchAsync();
                }
            });
        }

        private async Task PerformSearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                SearchResults.Clear();
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(HasNoResults));
                return;
            }

            IsSearching = true;
            SearchResults.Clear();

            try
            {
                var enabledPlugins = PluginFilters.Where(p => p.IsEnabled).Select(p => p.Id).ToList();
                var results = await SearchAcrossPluginsAsync(SearchText, enabledPlugins);

                foreach (var result in results.Take(100)) // 最大100件
                {
                    SearchResults.Add(result);
                }

                // 検索履歴に追加
                _filterService.AddSearchHistory(SearchText, "Global", SearchResults.Count);
                LoadRecentSearches();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Search error: {ex.Message}");
            }
            finally
            {
                IsSearching = false;
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(HasNoResults));
            }
        }

        private async Task<List<SearchResultItem>> SearchAcrossPluginsAsync(string searchText, List<string> enabledPlugins)
        {
            var results = new List<SearchResultItem>();
            var pluginManager = PluginManager.Instance;

            foreach (var pluginId in enabledPlugins)
            {
                try
                {
                    var pluginResults = await SearchInPluginAsync(pluginId, searchText);
                    results.AddRange(pluginResults);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Plugin search error ({pluginId}): {ex.Message}");
                }
            }

            // 関連性でソート（タイトルマッチを優先）
            return results.OrderByDescending(r => CalculateRelevance(r, searchText))
                         .ThenByDescending(r => r.CreatedDate)
                         .ToList();
        }

        private async Task<List<SearchResultItem>> SearchInPluginAsync(string pluginId, string searchText)
        {
            var results = new List<SearchResultItem>();

            switch (pluginId)
            {
                case "GadgetTools.TicketManage":
                    results.AddRange(await SearchTicketsAsync(searchText));
                    break;
                case "GadgetTools.PullRequestManagement":
                    results.AddRange(await SearchPullRequestsAsync(searchText));
                    break;
                case "GadgetTools.ExcelConverter":
                    results.AddRange(await SearchExcelDataAsync(searchText));
                    break;
            }

            return results;
        }

        private async Task<List<SearchResultItem>> SearchTicketsAsync(string searchText)
        {
            var results = new List<SearchResultItem>();
            
            // プラグインのViewModelからデータを取得（実際の実装では適切な方法で取得）
            var pluginManager = PluginManager.Instance;
            var ticketPlugin = pluginManager.GetPlugin("GadgetTools.TicketManage");
            
            if (ticketPlugin != null)
            {
                // 簡易実装：実際にはプラグインのデータアクセス層を呼び出す
                await Task.Delay(100); // API呼び出しのシミュレーション
                
                // ダミーデータ（実際の実装では実データを検索）
                if (searchText.Contains("bug", StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new SearchResultItem
                    {
                        Id = "12345",
                        Title = "Bug in user authentication",
                        Description = "Users are unable to log in after password reset",
                        Type = "Bug",
                        PluginId = "GadgetTools.TicketManage",
                        PluginName = "Ticket Management",
                        PluginColor = "#0078D4",
                        Icon = "🐛",
                        CreatedBy = "John Doe",
                        CreatedDate = DateTime.Now.AddDays(-2),
                        Status = "Active"
                    });
                }
            }

            return results;
        }

        private async Task<List<SearchResultItem>> SearchPullRequestsAsync(string searchText)
        {
            var results = new List<SearchResultItem>();
            
            // 同様にPRプラグインから検索
            await Task.Delay(100);
            
            if (searchText.Contains("feature", StringComparison.OrdinalIgnoreCase))
            {
                results.Add(new SearchResultItem
                {
                    Id = "67890",
                    Title = "Add new feature for user profiles",
                    Description = "Implements user profile customization functionality",
                    Type = "Pull Request",
                    PluginId = "GadgetTools.PullRequestManagement",
                    PluginName = "Pull Request Management",
                    PluginColor = "#28A745",
                    Icon = "🔀",
                    CreatedBy = "Jane Smith",
                    CreatedDate = DateTime.Now.AddDays(-1),
                    Status = "Active"
                });
            }

            return results;
        }

        private async Task<List<SearchResultItem>> SearchExcelDataAsync(string searchText)
        {
            var results = new List<SearchResultItem>();
            
            // Excel変換履歴から検索
            await Task.Delay(50);
            
            // 実装は省略（実際にはファイル履歴から検索）
            
            return results;
        }

        private double CalculateRelevance(SearchResultItem item, string searchText)
        {
            var relevance = 0.0;
            var lowerSearchText = searchText.ToLowerInvariant();

            // タイトル完全一致
            if (item.Title.Equals(searchText, StringComparison.OrdinalIgnoreCase))
                relevance += 100;
            
            // タイトル開始一致
            else if (item.Title.StartsWith(searchText, StringComparison.OrdinalIgnoreCase))
                relevance += 80;
            
            // タイトル部分一致
            else if (item.Title.Contains(lowerSearchText, StringComparison.OrdinalIgnoreCase))
                relevance += 60;

            // 説明部分一致
            if (item.Description.Contains(lowerSearchText, StringComparison.OrdinalIgnoreCase))
                relevance += 30;

            // ID一致
            if (item.Id.Contains(lowerSearchText, StringComparison.OrdinalIgnoreCase))
                relevance += 40;

            // 作成者一致
            if (item.CreatedBy.Contains(lowerSearchText, StringComparison.OrdinalIgnoreCase))
                relevance += 20;

            // 最近作成されたものを優遇
            var daysSinceCreated = (DateTime.Now - item.CreatedDate).TotalDays;
            if (daysSinceCreated < 7)
                relevance += 10;

            return relevance;
        }

        private bool CanPerformSearch()
        {
            return !IsSearching;
        }

        private void ClearSearch()
        {
            SearchText = string.Empty;
            SearchResults.Clear();
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(HasNoResults));
        }

        private void ShowAdvancedFilter()
        {
            // 高度なフィルタダイアログを表示
            // 実装は省略
        }

        private void LoadFromHistory(SearchHistory? history)
        {
            if (history != null)
            {
                SearchText = history.SearchText;
            }
        }

        private string GenerateStatusText()
        {
            if (IsSearching)
                return "Searching...";
            
            if (string.IsNullOrEmpty(SearchText))
                return "Enter search terms to find items across all plugins";
            
            if (!SearchResults.Any())
                return "No results found";
            
            var enabledPluginCount = PluginFilters.Count(p => p.IsEnabled);
            return $"Found {SearchResults.Count} results across {enabledPluginCount} plugin(s)";
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            _searchTimer?.Dispose();
        }

        #endregion
    }
}