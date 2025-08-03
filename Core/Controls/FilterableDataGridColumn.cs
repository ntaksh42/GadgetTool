using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using GadgetTools.Core.ViewModels;

namespace GadgetTools.Core.Controls
{
    public class FilterableDataGridColumn : DataGridTextColumn
    {
        public static readonly DependencyProperty PropertyPathProperty = 
            DependencyProperty.Register(nameof(PropertyPath), typeof(string), typeof(FilterableDataGridColumn));

        public static readonly DependencyProperty FilterManagerProperty = 
            DependencyProperty.Register(nameof(FilterManager), typeof(ColumnFilterManager), typeof(FilterableDataGridColumn));

        public string PropertyPath
        {
            get => (string)GetValue(PropertyPathProperty);
            set => SetValue(PropertyPathProperty, value);
        }

        public ColumnFilterManager? FilterManager
        {
            get => (ColumnFilterManager?)GetValue(FilterManagerProperty);
            set => SetValue(FilterManagerProperty, value);
        }

        protected override FrameworkElement GenerateElement(DataGridCell cell, object dataItem)
        {
            var element = base.GenerateElement(cell, dataItem);
            return element;
        }

        protected override FrameworkElement GenerateEditingElement(DataGridCell cell, object dataItem)
        {
            var element = base.GenerateEditingElement(cell, dataItem);
            return element;
        }
    }

    public class ColumnFilterManager
    {
        private readonly Dictionary<string, HashSet<object>> _columnFilters = new();
        private readonly Dictionary<string, List<object>> _originalData = new();
        private Popup? _currentFilterPopup;

        public event EventHandler? FilterChanged;

        public void RegisterColumn(string columnName, IEnumerable<object> data)
        {
            _originalData[columnName] = data.ToList();
        }

        public void ShowFilter(string columnName, FrameworkElement targetElement)
        {
            CloseCurrentFilter();

            if (!_originalData.ContainsKey(columnName))
                return;

            var filterViewModel = new ColumnFilterViewModel();
            var filterControl = new ColumnFilter { DataContext = filterViewModel };

            _currentFilterPopup = new Popup
            {
                Child = filterControl,
                PlacementTarget = targetElement,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade
            };

            filterViewModel.FilterApplied += (sender, e) =>
            {
                ApplyFilter(e.ColumnName, e.SelectedValues);
                CloseCurrentFilter();
            };

            filterViewModel.FilterCancelled += (sender, e) =>
            {
                CloseCurrentFilter();
            };

            var currentSelection = _columnFilters.ContainsKey(columnName) ? _columnFilters[columnName] : null;
            filterViewModel.Initialize(columnName, _originalData[columnName], currentSelection);

            _currentFilterPopup.IsOpen = true;
        }

        public void ApplyFilter(string columnName, HashSet<object> selectedValues)
        {
            if (selectedValues.Count == 0)
            {
                _columnFilters.Remove(columnName);
            }
            else
            {
                _columnFilters[columnName] = selectedValues;
            }

            FilterChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearAllFilters()
        {
            _columnFilters.Clear();
            FilterChanged?.Invoke(this, EventArgs.Empty);
        }

        public void ClearFilter(string columnName)
        {
            _columnFilters.Remove(columnName);
            FilterChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool ShouldIncludeItem(object item, Dictionary<string, Func<object, object?>> propertyGetters)
        {
            foreach (var filter in _columnFilters)
            {
                var columnName = filter.Key;
                var allowedValues = filter.Value;

                if (!propertyGetters.ContainsKey(columnName))
                    continue;

                var value = propertyGetters[columnName](item);
                if (value != null && !allowedValues.Contains(value))
                {
                    return false;
                }
            }

            return true;
        }

        public bool HasActiveFilters => _columnFilters.Count > 0;

        public IReadOnlyDictionary<string, HashSet<object>> GetActiveFilters() => _columnFilters;

        private void CloseCurrentFilter()
        {
            if (_currentFilterPopup != null)
            {
                _currentFilterPopup.IsOpen = false;
                _currentFilterPopup = null;
            }
        }
    }
}