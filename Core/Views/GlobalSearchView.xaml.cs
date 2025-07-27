using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GadgetTools.Core.Views
{
    /// <summary>
    /// GlobalSearchView.xaml の相互作用ロジック
    /// </summary>
    public partial class GlobalSearchView : UserControl
    {
        public event EventHandler<object>? ItemSelected;

        public GlobalSearchView()
        {
            InitializeComponent();
            Loaded += (s, e) => SearchTextBox.Focus();
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Enterキーで検索実行
                if (DataContext is ViewModels.GlobalSearchViewModel viewModel)
                {
                    viewModel.SearchCommand.Execute(null);
                }
            }
            else if (e.Key == Key.Down)
            {
                // 下矢印キーで結果リストにフォーカス移動
                // 実装は省略（必要に応じて追加）
            }
        }

        private void ResultList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem != null)
            {
                ItemSelected?.Invoke(this, listBox.SelectedItem);
            }
        }
    }
}