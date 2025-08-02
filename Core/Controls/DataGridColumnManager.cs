using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using GadgetTools.Core.ViewModels;
using GadgetTools.Core.Views;

namespace GadgetTools.Core.Controls
{
    public static class DataGridColumnManager
    {
        public static readonly DependencyProperty EnableColumnVisibilityProperty =
            DependencyProperty.RegisterAttached("EnableColumnVisibility", typeof(bool), typeof(DataGridColumnManager),
                new PropertyMetadata(false, OnEnableColumnVisibilityChanged));

        public static readonly DependencyProperty ColumnVisibilityManagerProperty =
            DependencyProperty.RegisterAttached("ColumnVisibilityManager", typeof(ColumnVisibilityViewModel), typeof(DataGridColumnManager));

        public static bool GetEnableColumnVisibility(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnableColumnVisibilityProperty);
        }

        public static void SetEnableColumnVisibility(DependencyObject obj, bool value)
        {
            obj.SetValue(EnableColumnVisibilityProperty, value);
        }

        public static ColumnVisibilityViewModel? GetColumnVisibilityManager(DependencyObject obj)
        {
            return (ColumnVisibilityViewModel?)obj.GetValue(ColumnVisibilityManagerProperty);
        }

        public static void SetColumnVisibilityManager(DependencyObject obj, ColumnVisibilityViewModel? value)
        {
            obj.SetValue(ColumnVisibilityManagerProperty, value);
        }

        private static void OnEnableColumnVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DataGrid dataGrid && (bool)e.NewValue)
            {
                InitializeColumnVisibility(dataGrid);
            }
        }

        private static void InitializeColumnVisibility(DataGrid dataGrid)
        {
            // ColumnVisibilityManagerを作成
            var manager = new ColumnVisibilityViewModel();
            SetColumnVisibilityManager(dataGrid, manager);

            // 列の表示/非表示変更イベントを処理
            manager.ColumnVisibilityChanged += (sender, e) => UpdateColumnVisibility(dataGrid, e);

            // DataGridが読み込まれた後に列定義を初期化
            dataGrid.Loaded += (sender, e) => 
            {
                System.Diagnostics.Debug.WriteLine("DataGrid Loaded event fired");
                InitializeColumnDefinitions(dataGrid, manager);
                
                // Loadedイベントの後に少し遅延してからコンテキストメニューを設定
                dataGrid.Dispatcher.BeginInvoke(new Action(() =>
                {
                    System.Diagnostics.Debug.WriteLine("Setting up context menu");
                    SetupContextMenu(dataGrid, manager);
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            };
        }

        private static void InitializeColumnDefinitions(DataGrid dataGrid, ColumnVisibilityViewModel manager)
        {
            System.Diagnostics.Debug.WriteLine($"InitializeColumnDefinitions called. Columns count: {dataGrid.Columns.Count}");
            
            if (dataGrid.Columns.Count == 0) 
            {
                System.Diagnostics.Debug.WriteLine("No columns found, returning");
                return;
            }

            var columnDefinitions = new Dictionary<string, string>();
            foreach (var column in dataGrid.Columns)
            {
                var columnName = GetColumnName(column);
                var displayName = GetColumnDisplayName(column);
                columnDefinitions[columnName] = displayName;
                System.Diagnostics.Debug.WriteLine($"Added column: {columnName} -> {displayName}");
            }

            manager.InitializeColumns(columnDefinitions);
            System.Diagnostics.Debug.WriteLine($"Manager initialized with {columnDefinitions.Count} columns");
        }

        private static void SetupContextMenu(DataGrid dataGrid, ColumnVisibilityViewModel manager)
        {
            // マウス右クリックイベントを直接処理
            dataGrid.PreviewMouseRightButtonUp += (sender, e) =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("Right mouse button clicked on DataGrid");
                    
                    // ヒットテストでクリックされた要素を取得
                    var element = dataGrid.InputHitTest(e.GetPosition(dataGrid)) as FrameworkElement;
                    System.Diagnostics.Debug.WriteLine($"Hit element: {element?.GetType().Name}");
                    
                    // DataGridColumnHeaderを探す
                    var header = FindParent<DataGridColumnHeader>(element);
                    if (header != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Found column header: {header.Content}");
                        ShowContextMenuForHeader(header, manager, e);
                        e.Handled = true;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in right click handler: {ex.Message}");
                }
            };
        }

        private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null && !(child is T))
            {
                child = VisualTreeHelper.GetParent(child);
            }
            return child as T;
        }

        private static void ShowContextMenuForHeader(DataGridColumnHeader header, ColumnVisibilityViewModel manager, MouseButtonEventArgs e)
        {
            var contextMenu = new ContextMenu();
            
            // 現在の列を非表示にするメニューアイテム
            var hideColumnItem = new MenuItem { Header = "Hide This Column" };
            hideColumnItem.Click += (sender, args) =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"Hide column clicked for header: {header.Content}");
                    var columnName = GetColumnName(header.Column);
                    System.Diagnostics.Debug.WriteLine($"Column name: {columnName}");
                    manager.SetColumnVisibility(columnName, false);
                    System.Diagnostics.Debug.WriteLine($"SetColumnVisibility called for {columnName}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error hiding column: {ex.Message}");
                }
            };
            contextMenu.Items.Add(hideColumnItem);

            contextMenu.Items.Add(new Separator());

            // 列の表示設定ダイアログを開くメニューアイテム
            var columnsItem = new MenuItem { Header = "Columns..." };
            columnsItem.Click += (sender, args) => ShowColumnVisibilityDialog(manager);
            contextMenu.Items.Add(columnsItem);

            // すべての列を表示するメニューアイテム
            var showAllItem = new MenuItem { Header = "Show All Columns" };
            showAllItem.Click += (sender, args) => manager.ShowAllColumns();
            contextMenu.Items.Add(showAllItem);

            contextMenu.PlacementTarget = header;
            contextMenu.IsOpen = true;
        }

        private static Style CreateEnhancedColumnHeaderStyle(Style? baseStyle, ColumnVisibilityViewModel manager)
        {
            // 既存のスタイルをベースにして新しいスタイルを作成
            var style = new Style(typeof(DataGridColumnHeader), baseStyle);
            
            // 右クリックコンテキストメニューを設定
            var contextMenu = new ContextMenu();
            
            // 現在の列を非表示にするメニューアイテム
            var hideColumnItem = new MenuItem { Header = "Hide This Column" };
            hideColumnItem.Click += (sender, e) =>
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("Hide column clicked (Enhanced)");
                    
                    if (sender is MenuItem menuItem)
                    {
                        System.Diagnostics.Debug.WriteLine($"MenuItem found: {menuItem.Header}");
                        
                        // ContextMenuを取得する別の方法を試す
                        var contextMenuFound = FindContextMenu(menuItem);
                        if (contextMenuFound?.PlacementTarget is DataGridColumnHeader header)
                        {
                            System.Diagnostics.Debug.WriteLine($"Header found: {header.Content}");
                            var columnName = GetColumnName(header.Column);
                            System.Diagnostics.Debug.WriteLine($"Column name: {columnName}");
                            manager.SetColumnVisibility(columnName, false);
                            System.Diagnostics.Debug.WriteLine($"SetColumnVisibility called for {columnName}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"ContextMenu or Header not found. PlacementTarget: {contextMenuFound?.PlacementTarget?.GetType().Name}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Sender is not MenuItem");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in hideColumnItem.Click: {ex.Message}");
                }
            };
            contextMenu.Items.Add(hideColumnItem);

            contextMenu.Items.Add(new Separator());

            // 列の表示設定ダイアログを開くメニューアイテム
            var columnsItem = new MenuItem { Header = "Columns..." };
            columnsItem.Click += (sender, e) => ShowColumnVisibilityDialog(manager);
            contextMenu.Items.Add(columnsItem);

            // すべての列を表示するメニューアイテム
            var showAllItem = new MenuItem { Header = "Show All Columns" };
            showAllItem.Click += (sender, e) => manager.ShowAllColumns();
            contextMenu.Items.Add(showAllItem);

            style.Setters.Add(new Setter(FrameworkElement.ContextMenuProperty, contextMenu));
            
            return style;
        }

        private static Style CreateColumnHeaderStyle(ColumnVisibilityViewModel manager)
        {
            var style = new Style(typeof(DataGridColumnHeader));
            
            // 右クリックコンテキストメニューを設定
            var contextMenu = new ContextMenu();
            
            // 現在の列を非表示にするメニューアイテム
            var hideColumnItem = new MenuItem { Header = "Hide This Column" };
            hideColumnItem.Click += (sender, e) =>
            {
                if (sender is MenuItem menuItem && 
                    menuItem.Parent is ContextMenu menu &&
                    menu.PlacementTarget is DataGridColumnHeader header)
                {
                    var columnName = GetColumnName(header.Column);
                    manager.SetColumnVisibility(columnName, false);
                }
            };
            contextMenu.Items.Add(hideColumnItem);

            contextMenu.Items.Add(new Separator());

            // 列の表示設定ダイアログを開くメニューアイテム
            var columnsItem = new MenuItem { Header = "Columns..." };
            columnsItem.Click += (sender, e) => ShowColumnVisibilityDialog(manager);
            contextMenu.Items.Add(columnsItem);

            // すべての列を表示するメニューアイテム
            var showAllItem = new MenuItem { Header = "Show All Columns" };
            showAllItem.Click += (sender, e) => manager.ShowAllColumns();
            contextMenu.Items.Add(showAllItem);

            style.Setters.Add(new Setter(FrameworkElement.ContextMenuProperty, contextMenu));
            
            return style;
        }

        private static void ShowColumnVisibilityDialog(ColumnVisibilityViewModel manager)
        {
            var dialog = new ColumnVisibilityDialog
            {
                DataContext = manager,
                Owner = Application.Current.MainWindow
            };

            dialog.ShowDialog();
        }

        private static void UpdateColumnVisibility(DataGrid dataGrid, ColumnVisibilityChangedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"UpdateColumnVisibility called for column: {e.ColumnName}, IsVisible: {e.IsVisible}");
                
                var column = dataGrid.Columns.FirstOrDefault(c => GetColumnName(c) == e.ColumnName);
                if (column != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Column found: {column.Header}, current visibility: {column.Visibility}");
                    column.Visibility = e.IsVisible ? Visibility.Visible : Visibility.Collapsed;
                    System.Diagnostics.Debug.WriteLine($"Column visibility set to: {column.Visibility}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Column not found with name: {e.ColumnName}");
                    System.Diagnostics.Debug.WriteLine($"Available columns: {string.Join(", ", dataGrid.Columns.Select(c => GetColumnName(c)))}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in UpdateColumnVisibility: {ex.Message}");
            }
        }

        private static string GetColumnName(DataGridColumn column)
        {
            // まずHeaderからコラム名を取得を試みる
            if (column.Header is string headerString)
            {
                return headerString;
            }
            
            // DataGridTextColumnの場合、Bindingからプロパティ名を取得
            if (column is DataGridTextColumn textColumn && textColumn.Binding is Binding binding)
            {
                return binding.Path.Path ?? $"Column{column.DisplayIndex}";
            }

            // フォールバック：表示インデックスを使用
            return $"Column{column.DisplayIndex}";
        }

        private static string GetColumnDisplayName(DataGridColumn column)
        {
            if (column.Header is string headerString)
            {
                return headerString;
            }

            return GetColumnName(column);
        }

        // 設定の保存/復元用のヘルパーメソッド
        public static Dictionary<string, bool> SaveColumnVisibilitySettings(DataGrid dataGrid)
        {
            var manager = GetColumnVisibilityManager(dataGrid);
            return manager?.GetVisibilitySettings() ?? new Dictionary<string, bool>();
        }

        public static void LoadColumnVisibilitySettings(DataGrid dataGrid, Dictionary<string, bool> settings)
        {
            var manager = GetColumnVisibilityManager(dataGrid);
            manager?.LoadVisibilitySettings(settings);
        }

        private static ContextMenu? FindContextMenu(MenuItem menuItem)
        {
            var current = menuItem.Parent;
            while (current != null)
            {
                if (current is ContextMenu contextMenu)
                    return contextMenu;
                if (current is FrameworkElement fe)
                    current = fe.Parent;
                else
                    break;
            }
            return null;
        }
    }
}