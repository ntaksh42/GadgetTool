using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.ComponentModel;
using System.Windows.Media;
using GadgetTools.Core.Views;
using GadgetTools.Core.Controls;
using GadgetTools.Core.ViewModels;
using GadgetTools.Shared.Models;

namespace GadgetTools.Plugins.TicketManage
{
    /// <summary>
    /// TicketManageView.xaml の相互作用ロジック
    /// </summary>
    public partial class TicketManageView : UserControl, IDisposable
    {
        private bool _webViewInitialized = false;
        private TicketManageViewModel? _viewModel;
        private bool _disposed = false;
        private string _lastNavigatedContent = "";
        private bool _isNavigating = false;
        private Popup? _currentFilterPopup;

        public TicketManageView()
        {
            InitializeComponent();
            Loaded += TicketManageView_Loaded;
            Unloaded += TicketManageView_Unloaded;
            DataContextChanged += TicketManageView_DataContextChanged;
        }

        private void TicketManageView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"DataContextChanged: Old={e.OldValue?.GetType().Name}, New={e.NewValue?.GetType().Name}");
            
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                _viewModel.ColumnFilterRequested -= ViewModel_ColumnFilterRequested;
                _viewModel.ShowColumnVisibilityRequested -= ViewModel_ShowColumnVisibilityRequested;
                System.Diagnostics.Debug.WriteLine("Unsubscribed from old ViewModel PropertyChanged");
            }

            _viewModel = DataContext as TicketManageViewModel;
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
                _viewModel.ColumnFilterRequested += ViewModel_ColumnFilterRequested;
                _viewModel.ShowColumnVisibilityRequested += ViewModel_ShowColumnVisibilityRequested;
                System.Diagnostics.Debug.WriteLine($"ViewModel connected to TicketManageView. WebView initialized: {_webViewInitialized}");
                
                // WebViewが既に初期化されている場合は初期コンテンツを設定
                if (_webViewInitialized && !string.IsNullOrEmpty(_viewModel.HtmlPreview))
                {
                    System.Diagnostics.Debug.WriteLine("Updating WebView with existing content");
                    UpdateWebViewContent();
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("DataContext is not TicketManageViewModel or is null");
            }
        }
        
        private async void TicketManageView_Loaded(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("TicketManageView loaded");
            
            // DataGridの列の表示/非表示機能を初期化
            InitializeColumnVisibility();
            
            // WebView2の初期化を待機
            try
            {
                await PreviewWebView.EnsureCoreWebView2Async();
                
                // NavigationCompleted イベントを接続
                PreviewWebView.NavigationCompleted += PreviewWebView_NavigationCompleted;
                
                _webViewInitialized = true;
                System.Diagnostics.Debug.WriteLine("WebView2 initialized successfully");
                
                // ViewModelが既に設定されている場合は初期コンテンツを設定
                if (_viewModel != null && !string.IsNullOrEmpty(_viewModel.HtmlPreview))
                {
                    UpdateWebViewContent();
                }
                else
                {
                    // シンプルなテストHTMLを表示
                    var testHtml = @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <title>Test</title>
    <style>
        body { font-family: Arial, sans-serif; padding: 20px; }
        .test { color: blue; font-size: 18px; }
    </style>
</head>
<body>
    <div class='test'>
        <h2>🎯 チケットプレビュー</h2>
        <p>WebView2が正常に動作しています。</p>
        <p>チケットを選択すると、ここに詳細が表示されます。</p>
    </div>
</body>
</html>";
                    PreviewWebView.NavigateToString(testHtml);
                    System.Diagnostics.Debug.WriteLine("Set test HTML content");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebView2 initialization error: {ex.Message}");
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"PropertyChanged: {e.PropertyName}");
            if (e.PropertyName == nameof(TicketManageViewModel.HtmlPreview))
            {
                System.Diagnostics.Debug.WriteLine($"HtmlPreview property changed. WebView initialized: {_webViewInitialized}, Content length: {_viewModel?.HtmlPreview?.Length ?? 0}");
                if (_webViewInitialized)
                {
                    UpdateWebViewContent();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("WebView not initialized yet, skipping update");
                }
            }
        }

        private void UpdateWebViewContent()
        {
            if (_disposed || _viewModel == null || !_webViewInitialized || _isNavigating)
                return;

            try
            {
                var htmlContent = _viewModel.HtmlPreview;
                if (!string.IsNullOrEmpty(htmlContent))
                {
                    // 同じコンテンツの場合は再ナビゲーションをスキップ
                    if (_lastNavigatedContent == htmlContent)
                    {
                        System.Diagnostics.Debug.WriteLine("Skipping navigation - content unchanged");
                        return;
                    }
                    
                    _isNavigating = true;
                    _lastNavigatedContent = htmlContent;
                    PreviewWebView.NavigateToString(htmlContent);
                    System.Diagnostics.Debug.WriteLine("WebView content updated successfully");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("HTML content is empty");
                    ShowFallbackContent();
                }
            }
            catch (Exception ex)
            {
                _isNavigating = false;
                System.Diagnostics.Debug.WriteLine($"WebView navigation error: {ex.Message}");
                ShowFallbackContent();
                
                // Log the error (SetError is protected, so we can't call it from View)
                System.Diagnostics.Debug.WriteLine($"プレビュー表示エラー: {ex.Message}");
            }
        }

        private void ShowFallbackContent()
        {
            try
            {
                var fallbackHtml = @"<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body { font-family: Arial, sans-serif; padding: 20px; text-align: center; color: #666; }
        .fallback { background: #f8f9fa; padding: 30px; border-radius: 8px; border: 1px solid #dee2e6; }
        .icon { font-size: 48px; margin-bottom: 16px; }
    </style>
</head>
<body>
    <div class='fallback'>
        <div class='icon'>⚠️</div>
        <h3>プレビューを表示できません</h3>
        <p>チケットの詳細表示中にエラーが発生しました。</p>
        <p>別のチケットを選択するか、アプリケーションを再起動してください。</p>
    </div>
</body>
</html>";
                PreviewWebView.NavigateToString(fallbackHtml);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to show fallback content: {ex.Message}");
            }
        }

        private void PreviewWebView_NavigationCompleted(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
            _isNavigating = false;
            System.Diagnostics.Debug.WriteLine($"WebView navigation completed. Success: {e.IsSuccess}");
            if (!e.IsSuccess)
            {
                _lastNavigatedContent = ""; // Reset on failure
                System.Diagnostics.Debug.WriteLine($"Navigation failed: {e.WebErrorStatus}");
                ShowFallbackContent();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("WebView navigation succeeded");
            }
        }

        private void TicketManageView_Unloaded(object sender, RoutedEventArgs e)
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                // Unsubscribe from events
                if (_viewModel != null)
                {
                    _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
                    _viewModel.ColumnFilterRequested -= ViewModel_ColumnFilterRequested;
                    _viewModel.ShowColumnVisibilityRequested -= ViewModel_ShowColumnVisibilityRequested;
                }

                Loaded -= TicketManageView_Loaded;
                Unloaded -= TicketManageView_Unloaded;
                DataContextChanged -= TicketManageView_DataContextChanged;

                // Dispose WebView2
                if (PreviewWebView?.CoreWebView2 != null)
                {
                    PreviewWebView.NavigationCompleted -= PreviewWebView_NavigationCompleted;
                    PreviewWebView.Dispose();
                    System.Diagnostics.Debug.WriteLine("WebView2 disposed successfully");
                }

                _disposed = true;
                System.Diagnostics.Debug.WriteLine("TicketManageView disposed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during disposal: {ex.Message}");
            }
        }

        private void GlobalSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsView = new AzureDevOpsSettingsView();
            var window = new Window
            {
                Title = "Azure DevOps Settings",
                Content = settingsView,
                Width = 650,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ResizeMode = ResizeMode.CanResize,
                MinWidth = 600,
                MinHeight = 400
            };
            window.ShowDialog();
        }
        
        private void DataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenSelectedWorkItem();
        }
        
        private void DataGrid_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OpenSelectedWorkItem();
                e.Handled = true;
            }
        }
        
        private void OpenSelectedWorkItem()
        {
            if (DataContext is TicketManageViewModel viewModel && 
                viewModel.SelectedWorkItem is WorkItem workItem)
            {
                try
                {
                    var url = $"https://dev.azure.com/{viewModel.Organization}/{viewModel.Project}/_workitems/edit/{workItem.Id}";
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                catch (System.Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to open work item: {ex.Message}", "Error", 
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        private void CollapseAll_Click(object sender, RoutedEventArgs e)
        {
            // Find all Expanders in the visual tree and collapse them
            var expanders = FindVisualChildren<Expander>(this);
            foreach (var expander in expanders)
            {
                expander.IsExpanded = false;
            }
        }

        private void ExpandAll_Click(object sender, RoutedEventArgs e)
        {
            // Find all Expanders in the visual tree and expand them
            var expanders = FindVisualChildren<Expander>(this);
            foreach (var expander in expanders)
            {
                expander.IsExpanded = true;
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }

        private void ViewModel_ColumnFilterRequested(object? sender, ColumnFilterRequestedEventArgs e)
        {
            ShowColumnFilter(e.ColumnName);
        }

        private void ShowColumnFilter(string columnName)
        {
            if (_viewModel == null) return;

            CloseCurrentFilter();

            try
            {
                var filterViewModel = new ExcelStyleFilterViewModel();
                var filterControl = new ExcelStyleFilter { DataContext = filterViewModel };

                _currentFilterPopup = new Popup
                {
                    Child = filterControl,
                    PlacementTarget = this,
                    Placement = PlacementMode.Mouse,
                    StaysOpen = false,
                    AllowsTransparency = true,
                    PopupAnimation = PopupAnimation.Fade
                };

                filterViewModel.FilterApplied += (s, args) =>
                {
                    // ViewModelのフィルタマネージャーに直接適用
                    if (_viewModel != null)
                    {
                        var manager = GetExcelFilterManager();
                        manager?.ApplyFilter(args.ColumnName, args.SelectedValues);
                    }
                    CloseCurrentFilter();
                };

                filterViewModel.FilterCancelled += (s, args) =>
                {
                    CloseCurrentFilter();
                };

                // データを初期化
                var columnData = GetColumnData(columnName);
                var currentFilters = GetExcelFilterManager()?.GetActiveFilters();
                var currentSelection = currentFilters?.ContainsKey(columnName) == true ? currentFilters[columnName] : null;
                
                filterViewModel.Initialize(columnName, columnData, currentSelection);

                _currentFilterPopup.IsOpen = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing column filter: {ex.Message}");
            }
        }

        private void CloseCurrentFilter()
        {
            if (_currentFilterPopup != null)
            {
                _currentFilterPopup.IsOpen = false;
                _currentFilterPopup = null;
            }
        }

        private ExcelFilterManager? GetExcelFilterManager()
        {
            // Reflectionを使ってprivate fieldにアクセス（実際の実装では適切なアクセサーを提供すべき）
            if (_viewModel == null) return null;
            
            var fieldInfo = _viewModel.GetType().GetField("_excelFilterManager", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return fieldInfo?.GetValue(_viewModel) as ExcelFilterManager;
        }

        private IEnumerable<object> GetColumnData(string columnName)
        {
            if (_viewModel?.WorkItems == null) return Enumerable.Empty<object>();

            return columnName switch
            {
                "ID" => _viewModel.WorkItems.Select(w => (object)w.Id),
                "Type" => _viewModel.WorkItems.Select(w => (object)(w.Fields.WorkItemType ?? "")),
                "Title" => _viewModel.WorkItems.Select(w => (object)(w.Fields.Title ?? "")),
                "State" => _viewModel.WorkItems.Select(w => (object)(w.Fields.State ?? "")),
                "Assigned To" => _viewModel.WorkItems.Select(w => (object)(w.Fields.AssignedTo?.DisplayName ?? "")),
                "Priority" => _viewModel.WorkItems.Select(w => (object)w.Fields.Priority.ToString()),
                "Created" => _viewModel.WorkItems.Select(w => (object)w.Fields.CreatedDate.ToString("yyyy-MM-dd")),
                "Last Updated" => _viewModel.WorkItems.Select(w => (object)w.Fields.ChangedDate.ToString("yyyy-MM-dd HH:mm")),
                _ => Enumerable.Empty<object>()
            };
        }

        private void ViewModel_ShowColumnVisibilityRequested(object? sender, EventArgs e)
        {
            ShowColumnVisibilityDialog();
        }

        private void InitializeColumnVisibility()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("InitializeColumnVisibility called");
                
                // DataGridが完全に読み込まれるまで待機
                WorkItemsDataGrid.Loaded += WorkItemsDataGrid_Loaded;
                System.Diagnostics.Debug.WriteLine("Loaded event handler added");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in InitializeColumnVisibility: {ex.Message}");
            }
        }

        private void WorkItemsDataGrid_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"WorkItemsDataGrid_Loaded called. Columns count: {WorkItemsDataGrid.Columns.Count}");
                
                // 直接的な右クリックメニューを設定
                SetupSimpleRightClickMenu();
                System.Diagnostics.Debug.WriteLine("Simple right-click menu setup completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in WorkItemsDataGrid_Loaded: {ex.Message}");
            }
        }

        private void SetupSimpleRightClickMenu()
        {
            try
            {
                // DataGridに右クリックイベントを追加
                WorkItemsDataGrid.MouseRightButtonUp += WorkItemsDataGrid_MouseRightButtonUp;
                System.Diagnostics.Debug.WriteLine("Right-click handler added to DataGrid");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SetupSimpleRightClickMenu: {ex.Message}");
            }
        }

        private void WorkItemsDataGrid_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Right mouse button clicked on DataGrid");
                
                // ヒットテストでクリックされた要素を取得
                var element = WorkItemsDataGrid.InputHitTest(e.GetPosition(WorkItemsDataGrid)) as FrameworkElement;
                System.Diagnostics.Debug.WriteLine($"Hit element: {element?.GetType().Name}");
                
                // DataGridColumnHeaderを探す
                var header = FindColumnHeader(element);
                if (header != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Found column header: {header.Content}");
                    ShowSimpleContextMenu(header, e);
                    e.Handled = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in right click handler: {ex.Message}");
            }
        }

        private DataGridColumnHeader? FindColumnHeader(DependencyObject? element)
        {
            while (element != null)
            {
                if (element is DataGridColumnHeader header)
                    return header;
                element = VisualTreeHelper.GetParent(element);
            }
            return null;
        }

        private void ShowSimpleContextMenu(DataGridColumnHeader header, MouseButtonEventArgs e)
        {
            try
            {
                var contextMenu = new ContextMenu();
                
                // 現在の列を非表示にするメニューアイテム
                var hideColumnItem = new MenuItem { Header = "この列を非表示" };
                hideColumnItem.Click += (s, args) =>
                {
                    try
                    {
                        header.Column.Visibility = Visibility.Collapsed;
                        System.Windows.MessageBox.Show($"列 '{header.Content}' を非表示にしました。", "完了", System.Windows.MessageBoxButton.OK);
                    }
                    catch (Exception ex)
                    {
                        System.Windows.MessageBox.Show($"エラー: {ex.Message}", "エラー", System.Windows.MessageBoxButton.OK);
                    }
                };
                contextMenu.Items.Add(hideColumnItem);

                contextMenu.Items.Add(new Separator());

                // 列の表示設定ダイアログを開くメニューアイテム
                var columnsItem = new MenuItem { Header = "列の設定..." };
                columnsItem.Click += (s, args) => ShowSimpleColumnVisibilityDialog();
                contextMenu.Items.Add(columnsItem);

                // すべての列を表示するメニューアイテム
                var showAllItem = new MenuItem { Header = "すべての列を表示" };
                showAllItem.Click += (s, args) =>
                {
                    foreach (var column in WorkItemsDataGrid.Columns)
                    {
                        column.Visibility = Visibility.Visible;
                    }
                    System.Windows.MessageBox.Show("すべての列を表示しました。", "完了", System.Windows.MessageBoxButton.OK);
                };
                contextMenu.Items.Add(showAllItem);

                contextMenu.PlacementTarget = header;
                contextMenu.IsOpen = true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"コンテキストメニューエラー: {ex.Message}", "エラー", System.Windows.MessageBoxButton.OK);
            }
        }

        private void ShowColumnVisibilityDialog()
        {
            try
            {
                // 直接的なテスト：列の表示/非表示を切り替える簡単なダイアログを作成
                ShowSimpleColumnVisibilityDialog();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ShowColumnVisibilityDialog: {ex.Message}");
                System.Windows.MessageBox.Show($"Error: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK);
            }
        }

        private void ShowSimpleColumnVisibilityDialog()
        {
            try
            {
                if (WorkItemsDataGrid.Columns.Count == 0)
                {
                    System.Windows.MessageBox.Show("列が見つかりません。", "エラー", System.Windows.MessageBoxButton.OK);
                    return;
                }

                // 簡単な列選択ダイアログを作成
                var window = new Window
                {
                    Title = "列の表示/非表示",
                    Width = 300,
                    Height = 400,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Window.GetWindow(this)
                };

                var stackPanel = new StackPanel { Margin = new Thickness(10) };

                // 各列に対してチェックボックスを作成
                var checkBoxes = new List<CheckBox>();
                foreach (var column in WorkItemsDataGrid.Columns)
                {
                    var checkBox = new CheckBox
                    {
                        Content = column.Header?.ToString() ?? "Unknown",
                        IsChecked = column.Visibility == Visibility.Visible,
                        Tag = column,
                        Margin = new Thickness(0, 5, 0, 5)
                    };
                    
                    checkBox.Checked += (s, e) =>
                    {
                        if (checkBox.Tag is DataGridColumn col)
                        {
                            col.Visibility = Visibility.Visible;
                        }
                    };
                    
                    checkBox.Unchecked += (s, e) =>
                    {
                        if (checkBox.Tag is DataGridColumn col)
                        {
                            col.Visibility = Visibility.Collapsed;
                        }
                    };
                    
                    checkBoxes.Add(checkBox);
                    stackPanel.Children.Add(checkBox);
                }

                // ボタンパネル
                var buttonPanel = new StackPanel 
                { 
                    Orientation = Orientation.Horizontal, 
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 0)
                };

                var showAllButton = new Button 
                { 
                    Content = "すべて表示", 
                    Width = 80, 
                    Margin = new Thickness(5, 0, 5, 0) 
                };
                showAllButton.Click += (s, e) =>
                {
                    foreach (var checkBox in checkBoxes)
                    {
                        checkBox.IsChecked = true;
                    }
                };

                var closeButton = new Button 
                { 
                    Content = "閉じる", 
                    Width = 60, 
                    Margin = new Thickness(5, 0, 5, 0) 
                };
                closeButton.Click += (s, e) => window.Close();

                buttonPanel.Children.Add(showAllButton);
                buttonPanel.Children.Add(closeButton);
                stackPanel.Children.Add(buttonPanel);

                window.Content = stackPanel;
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"ダイアログ作成エラー: {ex.Message}", "エラー", System.Windows.MessageBoxButton.OK);
            }
        }
    }
}