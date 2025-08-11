using System.Collections.ObjectModel;
using System.Windows.Input;
using GadgetTools.Core.Models;
using GadgetTools.Core.Services;

namespace GadgetTools.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly PluginManager _pluginManager;
        private string _statusMessage = "プラグインを読み込み中...";
        private bool _isLoading = true;
        private int _selectedTabIndex = 0;

        public ObservableCollection<PluginTabViewModel> PluginTabs { get; } = new();

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        public ICommand WindowClosingCommand { get; }

        public MainWindowViewModel()
        {
            _pluginManager = PluginManager.Instance;
            WindowClosingCommand = new AsyncRelayCommand(OnWindowClosingAsync);
            
            // パフォーマンス監視サービスを初期化
            InitializePerformanceMonitoring();
            
            // プラグインの読み込みを開始
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                StatusMessage = "プラグインを読み込み中...";
                IsLoading = true;

                // プラグインを読み込み
                await _pluginManager.LoadAllPluginsAsync();

                // プラグインタブを作成
                await CreatePluginTabsAsync();

                // 設定からタブ順序と選択タブを復元
                RestoreTabSettings();

                StatusMessage = $"{PluginTabs.Count}個のプラグインが読み込まれました";
            }
            catch (Exception ex)
            {
                StatusMessage = $"プラグインの読み込みに失敗しました: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"プラグイン初期化エラー: {ex}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task CreatePluginTabsAsync()
        {
            PluginTabs.Clear();

            foreach (var pluginInstance in _pluginManager.LoadedPlugins)
            {
                if (!pluginInstance.IsLoaded || !pluginInstance.Metadata.IsEnabled)
                    continue;

                try
                {
                    // プラグインのUIを作成
                    var (view, viewModel) = await _pluginManager.CreatePluginUIAsync(pluginInstance.Metadata.Id);
                    
                    if (view != null)
                    {
                        var tabViewModel = new PluginTabViewModel
                        {
                            Id = pluginInstance.Metadata.Id,
                            Header = pluginInstance.Metadata.DisplayName,
                            Description = pluginInstance.Metadata.Description,
                            View = view,
                            ViewModel = viewModel,
                            IsEnabled = pluginInstance.Metadata.IsEnabled,
                            Priority = pluginInstance.Metadata.Priority
                        };

                        PluginTabs.Add(tabViewModel);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"プラグインUI作成エラー {pluginInstance.Metadata.Id}: {ex.Message}");
                }
            }

            // 優先順位でソート
            var sortedTabs = PluginTabs.OrderBy(t => t.Priority).ToList();
            PluginTabs.Clear();
            foreach (var tab in sortedTabs)
            {
                PluginTabs.Add(tab);
            }
        }

        private void RestoreTabSettings()
        {
            try
            {
                var settings = Services.SettingsService.LoadSettings();
                if (settings.UI != null)
                {
                    // 保存されたタブ順序で並び替え
                    if (settings.UI.TabOrder?.Count > 0)
                    {
                        ReorderTabs(settings.UI.TabOrder);
                    }

                    // 選択されたタブインデックスを復元
                    if (settings.UI.SelectedTabIndex >= 0 && settings.UI.SelectedTabIndex < PluginTabs.Count)
                    {
                        SelectedTabIndex = settings.UI.SelectedTabIndex;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"タブ設定復元エラー: {ex.Message}");
            }
        }

        private void ReorderTabs(List<string> tabOrder)
        {
            try
            {
                var tabDictionary = PluginTabs.ToDictionary(t => t.Id, t => t);
                var reorderedTabs = new List<PluginTabViewModel>();

                // 保存された順序でタブを追加
                foreach (var tabId in tabOrder)
                {
                    if (tabDictionary.TryGetValue(tabId, out var tab))
                    {
                        reorderedTabs.Add(tab);
                        tabDictionary.Remove(tabId);
                    }
                }

                // 保存されていないタブを最後に追加
                reorderedTabs.AddRange(tabDictionary.Values.OrderBy(t => t.Priority));

                // コレクションを更新
                PluginTabs.Clear();
                foreach (var tab in reorderedTabs)
                {
                    PluginTabs.Add(tab);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"タブ並び替えエラー: {ex.Message}");
            }
        }

        private void InitializePerformanceMonitoring()
        {
            try
            {
                // メモリ監視サービスを初期化
                var memoryMonitor = MemoryMonitorService.Instance;
                memoryMonitor.MemoryWarning += OnMemoryWarning;
                
                // パフォーマンス最適化サービスを初期化
                var performanceService = PerformanceOptimizationService.Instance;
                performanceService.PerformanceWarning += OnPerformanceWarning;
                
                System.Diagnostics.Debug.WriteLine("Performance monitoring initialized");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize performance monitoring: {ex.Message}");
            }
        }
        
        private void OnMemoryWarning(object? sender, MemoryWarningEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Memory warning: {e.Message}");
            if (e.Level == MemoryWarningLevel.Critical)
            {
                StatusMessage = $"⚠️ メモリ使用量が高くなっています: {e.MemoryUsage.WorkingSetMB}MB";
            }
        }
        
        private void OnPerformanceWarning(object? sender, PerformanceWarningEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"Performance warning: {e.Message}");
            StatusMessage = $"⚠️ 処理時間が長くなっています: {e.OperationName} ({e.ActualTimeMs}ms)";
        }

        private async Task OnWindowClosingAsync()
        {
            try
            {
                // 設定を保存
                await SaveTabSettingsAsync();

                // すべてのプラグインをクリーンアップ
                await CleanupPluginsAsync();

                // プラグインマネージャーをクリーンアップ
                await _pluginManager.CleanupAllPluginsAsync();
                
                // パフォーマンス監視サービスをクリーンアップ
                MemoryMonitorService.Instance.Dispose();
                PerformanceOptimizationService.Instance.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ウィンドウクローズ処理エラー: {ex.Message}");
            }
        }

        private async Task SaveTabSettingsAsync()
        {
            try
            {
                var settings = Services.SettingsService.LoadSettings();
                
                if (settings.UI == null)
                    settings.UI = new Services.SettingsService.UISettings();

                // 現在のタブ順序を保存
                settings.UI.TabOrder = PluginTabs.Select(t => t.Id).ToList();
                settings.UI.SelectedTabIndex = SelectedTabIndex;

                Services.SettingsService.SaveSettings(settings);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"タブ設定保存エラー: {ex.Message}");
            }

            await Task.CompletedTask;
        }

        private async Task CleanupPluginsAsync()
        {
            foreach (var tab in PluginTabs)
            {
                try
                {
                    if (tab.ViewModel is PluginViewModelBase pluginViewModel)
                    {
                        await pluginViewModel.CleanupAsync();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"プラグインクリーンアップエラー {tab.Id}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 新しいプラグインを追加
        /// </summary>
        public async Task AddPluginAsync(string pluginId)
        {
            try
            {
                var pluginInstance = _pluginManager.GetPluginInstance(pluginId);
                if (pluginInstance?.IsLoaded == true)
                {
                    var (view, viewModel) = await _pluginManager.CreatePluginUIAsync(pluginId);
                    if (view != null)
                    {
                        var tabViewModel = new PluginTabViewModel
                        {
                            Id = pluginInstance.Metadata.Id,
                            Header = pluginInstance.Metadata.DisplayName,
                            Description = pluginInstance.Metadata.Description,
                            View = view,
                            ViewModel = viewModel,
                            IsEnabled = pluginInstance.Metadata.IsEnabled,
                            Priority = pluginInstance.Metadata.Priority
                        };

                        PluginTabs.Add(tabViewModel);
                        StatusMessage = $"プラグイン '{pluginInstance.Metadata.DisplayName}' が追加されました";
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"プラグインの追加に失敗しました: {ex.Message}";
            }
        }

        /// <summary>
        /// プラグインを削除
        /// </summary>
        public void RemovePlugin(string pluginId)
        {
            try
            {
                var tab = PluginTabs.FirstOrDefault(t => t.Id == pluginId);
                if (tab != null)
                {
                    PluginTabs.Remove(tab);
                    StatusMessage = $"プラグイン '{tab.Header}' が削除されました";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"プラグインの削除に失敗しました: {ex.Message}";
            }
        }

        /// <summary>
        /// タブ順序を即座に保存（ドラッグ&ドロップ用）
        /// </summary>
        public async Task SaveTabOrderAsync()
        {
            try
            {
                await SaveTabSettingsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"タブ順序保存エラー: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// プラグインタブのViewModel
    /// </summary>
    public class PluginTabViewModel : ViewModelBase
    {
        private string _id = string.Empty;
        private string _header = string.Empty;
        private string _description = string.Empty;
        private System.Windows.Controls.UserControl? _view;
        private object? _viewModel;
        private bool _isEnabled = true;
        private int _priority;

        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Header
        {
            get => _header;
            set => SetProperty(ref _header, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public System.Windows.Controls.UserControl? View
        {
            get => _view;
            set => SetProperty(ref _view, value);
        }

        public object? ViewModel
        {
            get => _viewModel;
            set => SetProperty(ref _viewModel, value);
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        public int Priority
        {
            get => _priority;
            set => SetProperty(ref _priority, value);
        }
    }
}