using System.Windows;

namespace GadgetTools.Core.Views
{
    /// <summary>
    /// ColumnVisibilityDialog.xaml の相互作用ロジック
    /// </summary>
    public partial class ColumnVisibilityDialog : Window
    {
        public ColumnVisibilityDialog()
        {
            InitializeComponent();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}