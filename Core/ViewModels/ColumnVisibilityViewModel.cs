using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using GadgetTools.Core.Models;

namespace GadgetTools.Core.ViewModels
{
    public class ColumnVisibilityItem : INotifyPropertyChanged
    {
        private bool _isVisible = true;
        private string _displayName = string.Empty;
        private string _columnName = string.Empty;

        public string DisplayName 
        { 
            get => _displayName; 
            set { _displayName = value; OnPropertyChanged(); } 
        }
        
        public string ColumnName 
        { 
            get => _columnName; 
            set { _columnName = value; OnPropertyChanged(); } 
        }
        
        public bool IsVisible 
        { 
            get => _isVisible; 
            set { _isVisible = value; OnPropertyChanged(); } 
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ColumnVisibilityViewModel : INotifyPropertyChanged
    {
        private readonly ObservableCollection<ColumnVisibilityItem> _columns = new();

        public ObservableCollection<ColumnVisibilityItem> Columns => _columns;

        public ICommand ToggleColumnCommand { get; }
        public ICommand ShowAllColumnsCommand { get; }
        public ICommand ResetToDefaultCommand { get; }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event System.EventHandler<ColumnVisibilityChangedEventArgs>? ColumnVisibilityChanged;

        public ColumnVisibilityViewModel()
        {
            ToggleColumnCommand = new RelayCommand<ColumnVisibilityItem>(ToggleColumn);
            ShowAllColumnsCommand = new RelayCommand(ShowAllColumns);
            ResetToDefaultCommand = new RelayCommand(ResetToDefault);
        }

        public void InitializeColumns(System.Collections.Generic.Dictionary<string, string> columnDefinitions)
        {
            _columns.Clear();
            foreach (var kvp in columnDefinitions)
            {
                var item = new ColumnVisibilityItem
                {
                    ColumnName = kvp.Key,
                    DisplayName = kvp.Value,
                    IsVisible = true
                };
                item.PropertyChanged += ColumnItem_PropertyChanged;
                _columns.Add(item);
            }
        }

        private void ColumnItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ColumnVisibilityItem.IsVisible) && sender is ColumnVisibilityItem item)
            {
                ColumnVisibilityChanged?.Invoke(this, new ColumnVisibilityChangedEventArgs(item.ColumnName, item.IsVisible));
            }
        }

        private void ToggleColumn(ColumnVisibilityItem? item)
        {
            if (item != null)
            {
                item.IsVisible = !item.IsVisible;
            }
        }

        public void ShowAllColumns()
        {
            foreach (var column in _columns)
            {
                column.IsVisible = true;
            }
        }

        private void ResetToDefault()
        {
            // デフォルトでは全ての列を表示
            ShowAllColumns();
        }

        public void SetColumnVisibility(string columnName, bool isVisible)
        {
            var column = _columns.FirstOrDefault(c => c.ColumnName == columnName);
            if (column != null)
            {
                column.IsVisible = isVisible;
            }
        }

        public bool GetColumnVisibility(string columnName)
        {
            var column = _columns.FirstOrDefault(c => c.ColumnName == columnName);
            return column?.IsVisible ?? true;
        }

        public System.Collections.Generic.Dictionary<string, bool> GetVisibilitySettings()
        {
            return _columns.ToDictionary(c => c.ColumnName, c => c.IsVisible);
        }

        public void LoadVisibilitySettings(System.Collections.Generic.Dictionary<string, bool> settings)
        {
            foreach (var setting in settings)
            {
                SetColumnVisibility(setting.Key, setting.Value);
            }
        }

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ColumnVisibilityChangedEventArgs : System.EventArgs
    {
        public string ColumnName { get; }
        public bool IsVisible { get; }

        public ColumnVisibilityChangedEventArgs(string columnName, bool isVisible)
        {
            ColumnName = columnName;
            IsVisible = isVisible;
        }
    }
}