using System.Windows;
using GadgetTools.ViewModels;

namespace GadgetTools
{
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            // ViewModelを設定
            DataContext = new MainWindowViewModel();
        }
    }
}