using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.ComponentModel;
using System.Windows.Media;
using GadgetTools.Core.Views;
using GadgetTools.Models;

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
                System.Diagnostics.Debug.WriteLine("Unsubscribed from old ViewModel PropertyChanged");
            }

            _viewModel = DataContext as TicketManageViewModel;
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
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
    }
}