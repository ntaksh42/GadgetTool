using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GadgetTools.ViewModels;

namespace GadgetTools.Controls
{
    public class DraggableTabControl : TabControl
    {
        private bool _isDragging = false;
        private Point _startPoint;
        private TabItem? _draggedTab = null;
        private object? _draggedItem = null;

        public DraggableTabControl()
        {
            AllowDrop = true;
        }

        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonDown(e);

            _startPoint = e.GetPosition(null);
            var tabItem = FindTabItem(e.OriginalSource as DependencyObject);
            
            if (tabItem != null)
            {
                _draggedTab = tabItem;
                _draggedItem = tabItem.DataContext;
            }
        }

        protected override void OnPreviewMouseMove(MouseEventArgs e)
        {
            base.OnPreviewMouseMove(e);

            if (e.LeftButton == MouseButtonState.Pressed && !_isDragging && _draggedTab != null && _draggedItem != null)
            {
                Point mousePos = e.GetPosition(null);
                Vector diff = _startPoint - mousePos;

                if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    _isDragging = true;
                    DragDrop.DoDragDrop(_draggedTab, _draggedItem, DragDropEffects.Move);
                    _isDragging = false;
                }
            }
        }

        protected override void OnDragOver(DragEventArgs e)
        {
            base.OnDragOver(e);

            // PluginTabViewModelかどうかをチェック
            var draggedData = e.Data.GetData(typeof(PluginTabViewModel));
            if (draggedData is PluginTabViewModel)
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }
        }

        protected override void OnDrop(DragEventArgs e)
        {
            base.OnDrop(e);

            // ドラッグされたデータを取得
            var draggedData = e.Data.GetData(typeof(PluginTabViewModel)) as PluginTabViewModel;
            var targetTab = FindTabItem(e.OriginalSource as DependencyObject);
            var targetData = targetTab?.DataContext as PluginTabViewModel;

            if (draggedData != null && targetData != null && draggedData != targetData && ItemsSource is ObservableCollection<PluginTabViewModel> collection)
            {
                int draggedIndex = collection.IndexOf(draggedData);
                int targetIndex = collection.IndexOf(targetData);

                if (draggedIndex >= 0 && targetIndex >= 0)
                {
                    // ObservableCollectionで要素を移動
                    collection.RemoveAt(draggedIndex);
                    collection.Insert(targetIndex, draggedData);
                    
                    // 選択されたタブを更新
                    SelectedIndex = targetIndex;
                    
                    // ViewModelに移動を通知（設定保存のため）
                    var mainWindow = Application.Current.MainWindow;
                    if (mainWindow?.DataContext is MainWindowViewModel mainViewModel)
                    {
                        mainViewModel.SelectedTabIndex = targetIndex;
                        _ = mainViewModel.SaveTabOrderAsync();
                    }
                }
            }

            e.Handled = true;
        }

        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonUp(e);
            _draggedTab = null;
            _draggedItem = null;
        }

        private TabItem? FindTabItem(DependencyObject? source)
        {
            while (source != null && source != this)
            {
                if (source is TabItem tabItem)
                    return tabItem;
                source = VisualTreeHelper.GetParent(source);
            }
            return null;
        }
    }
}