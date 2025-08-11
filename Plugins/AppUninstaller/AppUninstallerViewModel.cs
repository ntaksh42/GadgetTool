using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using GadgetTools.Core.Models;

namespace GadgetTools.Plugins.AppUninstaller
{
    public class AppUninstallerViewModel : PluginViewModelBase
    {
        private readonly InstalledAppService _appService;
        private readonly UninstallService _uninstallService;
        private CancellationTokenSource? _cancellationTokenSource;
        
        private string _searchText = string.Empty;
        private bool _selectAll = false;
        private int _selectedCount = 0;
        private int _totalApps = 0;
        private string _progressText = string.Empty;
        private int _progressValue = 0;
        private bool _isUninstalling = false;
        
        public ObservableCollection<InstalledApp> Apps { get; } = new();
        public ObservableCollection<InstalledApp> FilteredApps { get; } = new();
        public ObservableCollection<UninstallResult> UninstallResults { get; } = new();
        
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    FilterApps();
                }
            }
        }
        
        public bool SelectAll
        {
            get => _selectAll;
            set
            {
                if (SetProperty(ref _selectAll, value))
                {
                    foreach (var app in FilteredApps)
                    {
                        app.IsSelected = value;
                    }
                    UpdateSelectedCount();
                }
            }
        }
        
        public int SelectedCount
        {
            get => _selectedCount;
            private set => SetProperty(ref _selectedCount, value);
        }
        
        public int TotalApps
        {
            get => _totalApps;
            private set => SetProperty(ref _totalApps, value);
        }
        
        public string ProgressText
        {
            get => _progressText;
            private set => SetProperty(ref _progressText, value);
        }
        
        public int ProgressValue
        {
            get => _progressValue;
            private set => SetProperty(ref _progressValue, value);
        }
        
        public bool IsUninstalling
        {
            get => _isUninstalling;
            private set
            {
                if (SetProperty(ref _isUninstalling, value))
                {
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }
        
        public AsyncRelayCommand LoadAppsCommand { get; }
        public AsyncRelayCommand UninstallSelectedCommand { get; }
        public RelayCommand CancelUninstallCommand { get; }
        public RelayCommand ClearResultsCommand { get; }
        
        public AppUninstallerViewModel()
        {
            _appService = new InstalledAppService();
            _uninstallService = new UninstallService();
            
            LoadAppsCommand = new AsyncRelayCommand(LoadAppsAsync, () => !IsLoading);
            UninstallSelectedCommand = new AsyncRelayCommand(UninstallSelectedAppsAsync, 
                () => !IsUninstalling && SelectedCount > 0);
            CancelUninstallCommand = new RelayCommand(CancelUninstall, () => IsUninstalling);
            ClearResultsCommand = new RelayCommand(ClearResults, () => UninstallResults.Count > 0);
            
            _uninstallService.ProgressUpdated += OnUninstallProgressUpdated;
        }
        
        public override async Task InitializeAsync()
        {
            await LoadAppsAsync();
        }
        
        private async Task LoadAppsAsync()
        {
            try
            {
                IsLoading = true;
                ClearError();
                StatusMessage = "インストール済みアプリを読み込み中...";
                
                var apps = await _appService.GetInstalledAppsAsync();
                
                Apps.Clear();
                foreach (var app in apps)
                {
                    app.PropertyChanged += OnAppSelectionChanged;
                    Apps.Add(app);
                }
                
                TotalApps = Apps.Count;
                FilterApps();
                
                StatusMessage = $"{TotalApps}個のアプリを読み込みました";
            }
            catch (Exception ex)
            {
                SetError($"アプリの読み込みに失敗しました: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        private async Task UninstallSelectedAppsAsync()
        {
            var selectedApps = FilteredApps.Where(app => app.IsSelected).ToList();
            
            if (selectedApps.Count == 0)
            {
                MessageBox.Show("アンインストールするアプリを選択してください。", "選択エラー", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var result = MessageBox.Show(
                $"選択された{selectedApps.Count}個のアプリをアンインストールします。\n\n" +
                "この操作は元に戻せません。続行しますか？",
                "アンインストール確認",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
                
            if (result != MessageBoxResult.Yes)
                return;
            
            try
            {
                IsUninstalling = true;
                ClearError();
                UninstallResults.Clear();
                
                _cancellationTokenSource = new CancellationTokenSource();
                
                var results = await _uninstallService.UninstallAppsAsync(selectedApps, _cancellationTokenSource.Token);
                
                foreach (var uninstallResult in results)
                {
                    UninstallResults.Add(uninstallResult);
                }
                
                // 成功したアプリをリストから削除
                var successfulApps = results
                    .Where(r => r.Status == UninstallStatus.Success)
                    .Select(r => r.App)
                    .ToList();
                    
                foreach (var app in successfulApps)
                {
                    Apps.Remove(app);
                }
                
                TotalApps = Apps.Count;
                FilterApps();
                
                var successCount = results.Count(r => r.Status == UninstallStatus.Success);
                var failCount = results.Count(r => r.Status == UninstallStatus.Failed);
                
                StatusMessage = $"アンインストール完了: 成功 {successCount}個, 失敗 {failCount}個";
                
                if (failCount > 0)
                {
                    MessageBox.Show(
                        $"一部のアプリのアンインストールに失敗しました。\n成功: {successCount}個\n失敗: {failCount}個\n\n" +
                        "詳細は結果リストを確認してください。",
                        "アンインストール結果",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "アンインストールがキャンセルされました";
            }
            catch (Exception ex)
            {
                SetError($"アンインストール処理でエラーが発生しました: {ex.Message}");
            }
            finally
            {
                IsUninstalling = false;
                ProgressValue = 0;
                ProgressText = string.Empty;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }
        
        private void CancelUninstall()
        {
            _cancellationTokenSource?.Cancel();
        }
        
        private void ClearResults()
        {
            UninstallResults.Clear();
        }
        
        private void FilterApps()
        {
            FilteredApps.Clear();
            
            var filtered = string.IsNullOrEmpty(SearchText) 
                ? Apps 
                : Apps.Where(app => 
                    app.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                    app.Publisher.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            
            foreach (var app in filtered)
            {
                FilteredApps.Add(app);
            }
            
            UpdateSelectedCount();
        }
        
        private void OnAppSelectionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(InstalledApp.IsSelected))
            {
                UpdateSelectedCount();
            }
        }
        
        private void UpdateSelectedCount()
        {
            SelectedCount = FilteredApps.Count(app => app.IsSelected);
            
            // SelectAll の状態を更新
            if (FilteredApps.Count > 0)
            {
                var allSelected = FilteredApps.All(app => app.IsSelected);
                var noneSelected = FilteredApps.All(app => !app.IsSelected);
                
                if (allSelected)
                {
                    _selectAll = true;
                }
                else if (noneSelected)
                {
                    _selectAll = false;
                }
                else
                {
                    // 一部選択状態の場合は false にする
                    _selectAll = false;
                }
                
                OnPropertyChanged(nameof(SelectAll));
            }
        }
        
        private void OnUninstallProgressUpdated(object? sender, UninstallProgressEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ProgressValue = e.Progress;
                ProgressText = $"アンインストール中: {e.CurrentApp.Name} ({e.CurrentIndex}/{e.TotalCount})";
            });
        }
        
        public override Task CleanupAsync()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            
            // イベント購読解除
            foreach (var app in Apps)
            {
                app.PropertyChanged -= OnAppSelectionChanged;
            }
            
            return Task.CompletedTask;
        }
    }
}