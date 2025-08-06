using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace GadgetTools.Core.Views
{
    /// <summary>
    /// SaveQueryDialog.xaml の相互作用ロジック
    /// </summary>
    public partial class SaveQueryDialog : Window, INotifyPropertyChanged
    {
        private string _queryName = "";
        private string _queryDescription = "";

        public SaveQueryDialog()
        {
            InitializeComponent();
            DataContext = this;
        }

        public string QueryName
        {
            get => _queryName;
            set => SetProperty(ref _queryName, value);
        }

        public string QueryDescription
        {
            get => _queryDescription;
            set => SetProperty(ref _queryDescription, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(QueryName))
            {
                MessageBox.Show("クエリ名を入力してください。", "入力エラー", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                QueryNameTextBox.Focus();
                return;
            }

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}