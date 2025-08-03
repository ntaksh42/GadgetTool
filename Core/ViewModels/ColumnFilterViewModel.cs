using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using GadgetTools.Core.Models;

namespace GadgetTools.Core.ViewModels
{
    public class FilterValue : INotifyPropertyChanged
    {
        private bool _isSelected = true;
        private string _displayValue = string.Empty;
        private int _count;
        private object? _value;

        public string DisplayValue 
        { 
            get => _displayValue; 
            set { _displayValue = value; OnPropertyChanged(); } 
        }
        
        public int Count 
        { 
            get => _count; 
            set { _count = value; OnPropertyChanged(); } 
        }
        
        public bool IsSelected 
        { 
            get => _isSelected; 
            set { _isSelected = value; OnPropertyChanged(); } 
        }
        
        public object? Value 
        { 
            get => _value; 
            set { _value = value; OnPropertyChanged(); } 
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ColumnFilterViewModel : INotifyPropertyChanged
    {
        private string _columnName = string.Empty;
        private string _searchText = string.Empty;
        private bool _isSelectAllChecked = true;
        private readonly ObservableCollection<FilterValue> _allFilterValues = new();
        private readonly ObservableCollection<FilterValue> _filteredValues = new();

        public string ColumnName 
        { 
            get => _columnName; 
            set { _columnName = value; OnPropertyChanged(); } 
        }

        public string SearchText 
        { 
            get => _searchText; 
            set 
            { 
                _searchText = value; 
                OnPropertyChanged(); 
                ApplySearchFilter();
            } 
        }

        public bool IsSelectAllChecked 
        { 
            get => _isSelectAllChecked; 
            set 
            { 
                _isSelectAllChecked = value; 
                OnPropertyChanged(); 
                UpdateAllSelection();
            } 
        }

        public ObservableCollection<FilterValue> FilterValues => _filteredValues;

        public ICommand ApplyFilterCommand { get; }
        public ICommand CancelFilterCommand { get; }
        public ICommand ClearAllCommand { get; }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler<ColumnFilterAppliedEventArgs>? FilterApplied;
        public event EventHandler? FilterCancelled;

        public ColumnFilterViewModel()
        {
            ApplyFilterCommand = new RelayCommand(ApplyFilter);
            CancelFilterCommand = new RelayCommand(CancelFilter);
            ClearAllCommand = new RelayCommand(ClearAll);
        }

        public void Initialize(string columnName, IEnumerable<object> values, HashSet<object>? currentSelection = null)
        {
            ColumnName = columnName;
            _allFilterValues.Clear();
            
            // Group values and count occurrences
            var valueGroups = values
                .Where(v => v != null)
                .GroupBy(v => v.ToString() ?? "(empty)")
                .OrderBy(g => g.Key)
                .ToList();

            foreach (var group in valueGroups)
            {
                var filterValue = new FilterValue
                {
                    DisplayValue = group.Key,
                    Value = group.First(),
                    Count = group.Count(),
                    IsSelected = currentSelection?.Contains(group.First()) ?? true
                };
                
                filterValue.PropertyChanged += FilterValue_PropertyChanged;
                _allFilterValues.Add(filterValue);
            }
            
            ApplySearchFilter();
            UpdateSelectAllState();
        }

        private void FilterValue_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FilterValue.IsSelected))
            {
                UpdateSelectAllState();
            }
        }

        private void ApplySearchFilter()
        {
            _filteredValues.Clear();
            
            var filtered = string.IsNullOrEmpty(SearchText) 
                ? _allFilterValues 
                : _allFilterValues.Where(v => v.DisplayValue.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
                
            foreach (var value in filtered)
            {
                _filteredValues.Add(value);
            }
        }

        private void UpdateSelectAllState()
        {
            var visibleValues = _filteredValues.ToList();
            if (visibleValues.Count == 0)
            {
                _isSelectAllChecked = false;
            }
            else
            {
                var selectedCount = visibleValues.Count(v => v.IsSelected);
                _isSelectAllChecked = selectedCount == visibleValues.Count;
            }
            OnPropertyChanged(nameof(IsSelectAllChecked));
        }

        private void UpdateAllSelection()
        {
            foreach (var value in _filteredValues)
            {
                value.IsSelected = _isSelectAllChecked;
            }
        }

        private void ApplyFilter()
        {
            var selectedValues = _allFilterValues.Where(v => v.IsSelected).Select(v => v.Value).Where(v => v != null).ToHashSet()!;
            FilterApplied?.Invoke(this, new ColumnFilterAppliedEventArgs(ColumnName, selectedValues));
        }

        private void CancelFilter()
        {
            FilterCancelled?.Invoke(this, EventArgs.Empty);
        }

        private void ClearAll()
        {
            foreach (var value in _filteredValues)
            {
                value.IsSelected = false;
            }
        }

        protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ColumnFilterAppliedEventArgs : EventArgs
    {
        public string ColumnName { get; }
        public HashSet<object> SelectedValues { get; }

        public ColumnFilterAppliedEventArgs(string columnName, HashSet<object> selectedValues)
        {
            ColumnName = columnName;
            SelectedValues = selectedValues;
        }
    }
}