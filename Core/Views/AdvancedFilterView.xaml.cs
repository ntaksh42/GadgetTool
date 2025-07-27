using System.Windows;
using System.Windows.Controls;

namespace GadgetTools.Core.Views
{
    /// <summary>
    /// AdvancedFilterView.xaml の相互作用ロジック
    /// </summary>
    public partial class AdvancedFilterView : UserControl
    {
        // Removed unused event: FilterApplied
        public event EventHandler? FilterCancelled;

        public AdvancedFilterView()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            FilterCancelled?.Invoke(this, EventArgs.Empty);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            FilterCancelled?.Invoke(this, EventArgs.Empty);
        }
    }
}