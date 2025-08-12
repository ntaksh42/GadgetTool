using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using GadgetTools.Core.Views;
using GadgetTools.Core.Controls;
using GadgetTools.Core.ViewModels;
using GadgetTools.Core.Services;
using GadgetTools.Shared.Models;

namespace GadgetTools.Plugins.PullRequestManagement
{
    /// <summary>
    /// Interaction logic for PullRequestManagementView.xaml
    /// </summary>
    public partial class PullRequestManagementView : UserControl, IDisposable
    {
        private PullRequestManagementViewModel? _viewModel;
        private bool _disposed = false;
        private Popup? _currentFilterPopup;
        private readonly DataGridSettingsService _gridSettingsService;
        private const string GRID_ID = "PullRequestManagement.PullRequests";

        public PullRequestManagementView()
        {
            InitializeComponent();
            _gridSettingsService = DataGridSettingsService.Instance;
            Loaded += PullRequestManagementView_Loaded;
            Unloaded += PullRequestManagementView_Unloaded;
            DataContextChanged += PullRequestManagementView_DataContextChanged;
        }

        private void PullRequestManagementView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.ColumnFilterRequested -= ViewModel_ColumnFilterRequested;
                _viewModel.ShowColumnVisibilityRequested -= ViewModel_ShowColumnVisibilityRequested;
            }

            _viewModel = DataContext as PullRequestManagementViewModel;
            if (_viewModel != null)
            {
                _viewModel.ColumnFilterRequested += ViewModel_ColumnFilterRequested;
                _viewModel.ShowColumnVisibilityRequested += ViewModel_ShowColumnVisibilityRequested;
            }
        }

        private void PullRequestManagementView_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeColumnVisibility();
        }

        private void PullRequestManagementView_Unloaded(object sender, RoutedEventArgs e)
        {
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            try
            {
                // Save DataGrid settings
                SaveDataGridSettings();

                // Cleanup DataGrid settings watcher
                CleanupDataGridSettingsWatcher();

                // Unsubscribe from events
                if (_viewModel != null)
                {
                    _viewModel.ColumnFilterRequested -= ViewModel_ColumnFilterRequested;
                    _viewModel.ShowColumnVisibilityRequested -= ViewModel_ShowColumnVisibilityRequested;
                }

                Loaded -= PullRequestManagementView_Loaded;
                Unloaded -= PullRequestManagementView_Unloaded;
                DataContextChanged -= PullRequestManagementView_DataContextChanged;

                _disposed = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during disposal: {ex.Message}");
            }
        }

        private void TitleHyperlink_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Hyperlink hyperlink && hyperlink.Tag is PullRequest pullRequest)
            {
                try
                {
                    if (!string.IsNullOrEmpty(pullRequest.Url))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = pullRequest.Url,
                            UseShellExecute = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to open URL: {ex.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
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

        #region Column Filter Methods

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
                var filterViewModel = new ColumnFilterViewModel();
                var filterControl = new ColumnFilter { DataContext = filterViewModel };

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
                    // Apply filter through ViewModel's filter manager
                    if (_viewModel != null)
                    {
                        var manager = GetColumnFilterManager();
                        manager?.ApplyFilter(args.ColumnName, args.SelectedValues);
                    }
                    CloseCurrentFilter();
                };

                filterViewModel.FilterCancelled += (s, args) =>
                {
                    CloseCurrentFilter();
                };

                // Initialize data
                var columnData = GetColumnData(columnName);
                var currentFilters = GetColumnFilterManager()?.GetActiveFilters();
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

        private ColumnFilterManager? GetColumnFilterManager()
        {
            // Use reflection to access private field (in production, provide proper accessor)
            if (_viewModel == null) return null;
            
            var fieldInfo = _viewModel.GetType().GetField("_columnFilterManager", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return fieldInfo?.GetValue(_viewModel) as ColumnFilterManager;
        }

        private IEnumerable<object> GetColumnData(string columnName)
        {
            if (_viewModel?.PullRequests == null) return Enumerable.Empty<object>();

            return columnName switch
            {
                "ID" => _viewModel.PullRequests.Select(pr => (object)pr.Id),
                "Title" => _viewModel.PullRequests.Select(pr => (object)(pr.Title ?? "")),
                "Status" => _viewModel.PullRequests.Select(pr => (object)(pr.Status ?? "")),
                "Approval" => _viewModel.PullRequests.Select(pr => (object)(pr.ApprovalStatusShort ?? "")),
                "Created By" => _viewModel.PullRequests.Select(pr => (object)(pr.CreatedBy ?? "")),
                "Created Date" => _viewModel.PullRequests.Select(pr => (object)pr.CreatedDate.ToString("yyyy-MM-dd")),
                "Source Branch" => _viewModel.PullRequests.Select(pr => (object)(pr.SourceBranch ?? "")),
                "Target Branch" => _viewModel.PullRequests.Select(pr => (object)(pr.TargetBranch ?? "")),
                "Modified Files" => _viewModel.PullRequests.Select(pr => (object)(pr.ModifiedFilesDisplay ?? "")),
                _ => Enumerable.Empty<object>()
            };
        }

        private void ViewModel_ShowColumnVisibilityRequested(object? sender, EventArgs e)
        {
            ShowColumnVisibilityDialog();
        }

        #endregion

        #region DataGrid Settings Management

        private void InitializeColumnVisibility()
        {
            try
            {
                // DataGrid loaded event setup
                PullRequestsDataGrid.Loaded += PullRequestsDataGrid_Loaded;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in InitializeColumnVisibility: {ex.Message}");
            }
        }

        private void PullRequestsDataGrid_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Setup right-click menu for column headers
                SetupRightClickMenu();
                
                // Restore DataGrid settings
                RestoreDataGridSettings();
                
                // Setup settings monitoring
                SetupDataGridSettingsWatcher();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in PullRequestsDataGrid_Loaded: {ex.Message}");
            }
        }

        private void SetupRightClickMenu()
        {
            try
            {
                PullRequestsDataGrid.MouseRightButtonUp += PullRequestsDataGrid_MouseRightButtonUp;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SetupRightClickMenu: {ex.Message}");
            }
        }

        private void PullRequestsDataGrid_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var element = PullRequestsDataGrid.InputHitTest(e.GetPosition(PullRequestsDataGrid)) as FrameworkElement;
                var header = FindColumnHeader(element);
                if (header != null)
                {
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
                
                // Hide current column menu item
                var hideColumnItem = new MenuItem { Header = "この列を非表示" };
                hideColumnItem.Click += (s, args) =>
                {
                    try
                    {
                        header.Column.Visibility = Visibility.Collapsed;
                        MessageBox.Show($"列 '{header.Content}' を非表示にしました。", "完了", MessageBoxButton.OK);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"エラー: {ex.Message}", "エラー", MessageBoxButton.OK);
                    }
                };
                contextMenu.Items.Add(hideColumnItem);

                contextMenu.Items.Add(new Separator());

                // Column settings dialog menu item
                var columnsItem = new MenuItem { Header = "列の設定..." };
                columnsItem.Click += (s, args) => ShowColumnVisibilityDialog();
                contextMenu.Items.Add(columnsItem);

                // Show all columns menu item
                var showAllItem = new MenuItem { Header = "すべての列を表示" };
                showAllItem.Click += (s, args) =>
                {
                    foreach (var column in PullRequestsDataGrid.Columns)
                    {
                        column.Visibility = Visibility.Visible;
                    }
                    MessageBox.Show("すべての列を表示しました。", "完了", MessageBoxButton.OK);
                };
                contextMenu.Items.Add(showAllItem);

                contextMenu.Items.Add(new Separator());

                // Reset column settings menu item
                var resetSettingsItem = new MenuItem { Header = "列設定をリセット" };
                resetSettingsItem.Click += (s, args) =>
                {
                    var result = MessageBox.Show(
                        "列の設定をすべてリセットしますか？\n（表示状態、幅、順序がデフォルトに戻ります）", 
                        "確認", 
                        MessageBoxButton.YesNo, 
                        MessageBoxImage.Question);
                    
                    if (result == MessageBoxResult.Yes)
                    {
                        ResetDataGridSettings();
                        MessageBox.Show("列設定をリセットしました。", "完了", MessageBoxButton.OK);
                    }
                };
                contextMenu.Items.Add(resetSettingsItem);

                contextMenu.PlacementTarget = header;
                contextMenu.IsOpen = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"コンテキストメニューエラー: {ex.Message}", "エラー", MessageBoxButton.OK);
            }
        }

        private void ShowColumnVisibilityDialog()
        {
            try
            {
                if (PullRequestsDataGrid.Columns.Count == 0)
                {
                    MessageBox.Show("列が見つかりません。", "エラー", MessageBoxButton.OK);
                    return;
                }

                var window = new Window
                {
                    Title = "列の表示/非表示",
                    Width = 300,
                    Height = 400,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = Window.GetWindow(this)
                };

                var stackPanel = new StackPanel { Margin = new Thickness(10) };

                var checkBoxes = new List<CheckBox>();
                foreach (var column in PullRequestsDataGrid.Columns)
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
                MessageBox.Show($"ダイアログ作成エラー: {ex.Message}", "エラー", MessageBoxButton.OK);
            }
        }

        private void RestoreDataGridSettings()
        {
            try
            {
                if (PullRequestsDataGrid.Columns.Count > 0)
                {
                    _gridSettingsService.RestoreGridSettings(PullRequestsDataGrid, GRID_ID);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error restoring DataGrid settings: {ex.Message}");
            }
        }

        private void SetupDataGridSettingsWatcher()
        {
            try
            {
                foreach (var column in PullRequestsDataGrid.Columns)
                {
                    var descriptor = DependencyPropertyDescriptor.FromProperty(DataGridColumn.VisibilityProperty, typeof(DataGridColumn));
                    descriptor?.AddValueChanged(column, OnColumnSettingsChanged);

                    var widthDescriptor = DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty, typeof(DataGridColumn));
                    widthDescriptor?.AddValueChanged(column, OnColumnSettingsChanged);

                    var displayIndexDescriptor = DependencyPropertyDescriptor.FromProperty(DataGridColumn.DisplayIndexProperty, typeof(DataGridColumn));
                    displayIndexDescriptor?.AddValueChanged(column, OnColumnSettingsChanged);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting up DataGrid settings watcher: {ex.Message}");
            }
        }

        private void OnColumnSettingsChanged(object? sender, EventArgs e)
        {
            try
            {
                Task.Delay(500).ContinueWith(_ =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        SaveDataGridSettings();
                    });
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnColumnSettingsChanged: {ex.Message}");
            }
        }

        private void SaveDataGridSettings()
        {
            try
            {
                if (PullRequestsDataGrid.Columns.Count > 0)
                {
                    _gridSettingsService.SaveGridSettings(PullRequestsDataGrid, GRID_ID);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving DataGrid settings: {ex.Message}");
            }
        }

        private void ResetDataGridSettings()
        {
            try
            {
                _gridSettingsService.DeleteGridSettings(GRID_ID);
                
                foreach (var column in PullRequestsDataGrid.Columns)
                {
                    column.Visibility = Visibility.Visible;
                    column.Width = DataGridLength.SizeToHeader;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error resetting DataGrid settings: {ex.Message}");
            }
        }

        private void CleanupDataGridSettingsWatcher()
        {
            try
            {
                foreach (var column in PullRequestsDataGrid.Columns)
                {
                    var descriptor = DependencyPropertyDescriptor.FromProperty(DataGridColumn.VisibilityProperty, typeof(DataGridColumn));
                    descriptor?.RemoveValueChanged(column, OnColumnSettingsChanged);

                    var widthDescriptor = DependencyPropertyDescriptor.FromProperty(DataGridColumn.WidthProperty, typeof(DataGridColumn));
                    widthDescriptor?.RemoveValueChanged(column, OnColumnSettingsChanged);

                    var displayIndexDescriptor = DependencyPropertyDescriptor.FromProperty(DataGridColumn.DisplayIndexProperty, typeof(DataGridColumn));
                    displayIndexDescriptor?.RemoveValueChanged(column, OnColumnSettingsChanged);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error cleaning up DataGrid settings watcher: {ex.Message}");
            }
        }

        #endregion

        #region Multi-selection Event Handlers

        private void RemoveProject_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string project)
            {
                if (_viewModel?.Projects != null && _viewModel.Projects.Contains(project))
                {
                    _viewModel.Projects.Remove(project);
                }
            }
        }

        private void SelectProjects_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;

            try
            {
                // Use the same MultipleSelectionDialog as TicketManage
                var availableProjects = _viewModel.AllProjects?.ToList() ?? new List<string>();
                var selectedProjects = _viewModel.Projects?.ToList() ?? new List<string>();

                var dialog = new GadgetTools.Core.Views.MultipleSelectionDialog
                {
                    Title = "Select Projects",
                    Owner = Window.GetWindow(this),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ItemsSource = availableProjects,
                    SelectedItems = selectedProjects
                };

                if (dialog.ShowDialog() == true)
                {
                    var newSelection = dialog.SelectedItems;
                    _viewModel.Projects.Clear();
                    foreach (var project in newSelection)
                    {
                        _viewModel.Projects.Add(project);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error selecting projects: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveRepository_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string repository)
            {
                if (_viewModel?.Repositories != null && _viewModel.Repositories.Contains(repository))
                {
                    _viewModel.Repositories.Remove(repository);
                }
            }
        }

        private void SelectRepositories_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel == null) return;

            try
            {
                // Use the same MultipleSelectionDialog as TicketManage
                var availableRepositories = _viewModel.AllRepositories?.ToList() ?? new List<string>();
                var selectedRepositories = _viewModel.Repositories?.ToList() ?? new List<string>();

                var dialog = new GadgetTools.Core.Views.MultipleSelectionDialog
                {
                    Title = "Select Repositories",
                    Owner = Window.GetWindow(this),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ItemsSource = availableRepositories,
                    SelectedItems = selectedRepositories
                };

                if (dialog.ShowDialog() == true)
                {
                    var newSelection = dialog.SelectedItems;
                    _viewModel.Repositories.Clear();
                    foreach (var repository in newSelection)
                    {
                        _viewModel.Repositories.Add(repository);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error selecting repositories: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void LoadProjects_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                await _viewModel.LoadProjectsAsync();
            }
        }

        private async void LoadRepositories_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null)
            {
                await _viewModel.LoadRepositoriesAsync();
            }
        }

        private void DataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenSelectedPullRequest();
        }

        private void DataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"PullRequestManager - PreviewKeyDown: {e.Key}");
            if (e.Key == Key.Enter)
            {
                System.Diagnostics.Debug.WriteLine("PullRequestManager - Enter key detected, opening PR");
                OpenSelectedPullRequest();
                e.Handled = true;
            }
        }

        private void OpenSelectedPullRequest()
        {
            System.Diagnostics.Debug.WriteLine($"OpenSelectedPullRequest called - SelectedPullRequest: {_viewModel?.SelectedPullRequest?.Id}");
            if (_viewModel?.SelectedPullRequest != null)
            {
                // Enter キーが押されたときにPRをブラウザで開く
                if (_viewModel.OpenInBrowserCommand.CanExecute(null))
                {
                    System.Diagnostics.Debug.WriteLine("Executing OpenInBrowserCommand");
                    _viewModel.OpenInBrowserCommand.Execute(null);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("OpenInBrowserCommand.CanExecute returned false");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("No pull request selected");
            }
        }

        #endregion
    }
}