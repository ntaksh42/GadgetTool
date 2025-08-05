using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace GadgetTools.Core.Views
{
    public partial class MultipleSelectionDialog : Window, INotifyPropertyChanged
    {
        private string _searchText = string.Empty;
        private string _newItemText = string.Empty;
        private ObservableCollection<SelectableItem> _allItems = new();
        private ObservableCollection<SelectableItem> _filteredItems = new();

        public List<string> ItemsSource { get; set; } = new List<string>();
        public List<string> SelectedItems { get; set; } = new List<string>();

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                FilterItems();
            }
        }

        public string NewItemText
        {
            get => _newItemText;
            set
            {
                _newItemText = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<SelectableItem> FilteredItems
        {
            get => _filteredItems;
            set
            {
                _filteredItems = value;
                OnPropertyChanged();
            }
        }

        public string SelectionSummary
        {
            get
            {
                var selectedCount = _allItems.Count(item => item.IsSelected);
                var totalCount = _allItems.Count;
                var listSelectedCount = ItemsListBox?.SelectedItems?.Count ?? 0;
                
                var summary = $"{selectedCount} of {totalCount} items selected";
                if (listSelectedCount > 0)
                {
                    summary += $" | {listSelectedCount} highlighted for actions";
                }
                return summary;
            }
        }

        public MultipleSelectionDialog()
        {
            InitializeComponent();
            DataContext = this;
            Loaded += MultipleSelectionDialog_Loaded;
        }

        private void MultipleSelectionDialog_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeItems();
            SearchBox.Focus();
        }

        private void InitializeItems()
        {
            _allItems.Clear();

            // Add items from ItemsSource
            foreach (var item in ItemsSource)
            {
                var selectableItem = new SelectableItem
                {
                    Name = item,
                    IsSelected = SelectedItems.Contains(item)
                };
                selectableItem.PropertyChanged += SelectableItem_PropertyChanged;
                _allItems.Add(selectableItem);
            }

            // Add currently selected items that might not be in ItemsSource
            foreach (var selectedItem in SelectedItems)
            {
                if (!_allItems.Any(item => item.Name == selectedItem))
                {
                    var selectableItem = new SelectableItem
                    {
                        Name = selectedItem,
                        IsSelected = true
                    };
                    selectableItem.PropertyChanged += SelectableItem_PropertyChanged;
                    _allItems.Add(selectableItem);
                }
            }

            FilterItems();
        }

        private void SelectableItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SelectableItem.IsSelected))
            {
                OnPropertyChanged(nameof(SelectionSummary));
            }
        }

        private void FilterItems()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredItems = new ObservableCollection<SelectableItem>(_allItems);
            }
            else
            {
                var filtered = _allItems.Where(item => 
                    item.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();
                FilteredItems = new ObservableCollection<SelectableItem>(filtered);
            }
        }

        private void NewItemBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddNewItem();
            }
        }

        private void AddItem_Click(object sender, RoutedEventArgs e)
        {
            AddNewItem();
        }

        private void AddNewItem()
        {
            if (string.IsNullOrWhiteSpace(NewItemText))
                return;

            var itemName = NewItemText.Trim();
            if (_allItems.Any(item => item.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show($"Item '{itemName}' already exists.", "Duplicate Item", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var newItem = new SelectableItem
            {
                Name = itemName,
                IsSelected = true
            };
            newItem.PropertyChanged += SelectableItem_PropertyChanged;
            _allItems.Add(newItem);

            NewItemText = string.Empty;
            FilterItems();
            OnPropertyChanged(nameof(SelectionSummary));
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in FilteredItems)
            {
                item.IsSelected = true;
            }
        }

        private void ClearAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in FilteredItems)
            {
                item.IsSelected = false;
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            SelectedItems = _allItems.Where(item => item.IsSelected).Select(item => item.Name).ToList();
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        
        #region Deletion Methods
        
        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is SelectableItem item)
            {
                DeleteItems(new[] { item });
            }
        }
        
        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = ItemsListBox.SelectedItems.Cast<SelectableItem>().ToList();
            if (selectedItems.Any())
            {
                DeleteItems(selectedItems);
            }
            else
            {
                MessageBox.Show("Please select items to delete.", "No Selection", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        private void ItemsListBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Delete)
            {
                var selectedItems = ItemsListBox.SelectedItems.Cast<SelectableItem>().ToList();
                if (selectedItems.Any())
                {
                    DeleteItems(selectedItems);
                }
                e.Handled = true;
            }
        }
        
        private void DeleteItems(IEnumerable<SelectableItem> itemsToDelete)
        {
            var itemsList = itemsToDelete.ToList();
            
            if (itemsList.Count == 1)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete '{itemsList.First().Name}'?", 
                    "Confirm Deletion", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question);
                    
                if (result != MessageBoxResult.Yes)
                    return;
            }
            else if (itemsList.Count > 1)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete {itemsList.Count} selected items?", 
                    "Confirm Deletion", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question);
                    
                if (result != MessageBoxResult.Yes)
                    return;
            }
            
            // Unsubscribe from events before removing
            foreach (var item in itemsList)
            {
                item.PropertyChanged -= SelectableItem_PropertyChanged;
                _allItems.Remove(item);
            }
            
            FilterItems();
            OnPropertyChanged(nameof(SelectionSummary));
        }
        
        private void SelectAllFromContext_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in FilteredItems)
            {
                item.IsSelected = true;
            }
        }
        
        private void DeselectAllFromContext_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in FilteredItems)
            {
                item.IsSelected = false;
            }
        }
        
        private void ItemsListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(SelectionSummary));
        }
        
        #endregion

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class SelectableItem : INotifyPropertyChanged
    {
        private bool _isSelected;

        public string Name { get; set; } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}