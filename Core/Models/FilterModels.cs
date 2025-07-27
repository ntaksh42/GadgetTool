using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace GadgetTools.Core.Models
{
    /// <summary>
    /// 高度なフィルタ機能のためのモデル群
    /// </summary>
    
    /// <summary>
    /// フィルタ条件の演算子
    /// </summary>
    public enum FilterOperator
    {
        Contains,
        Equals,
        NotEquals,
        StartsWith,
        EndsWith,
        GreaterThan,
        LessThan,
        GreaterThanOrEqual,
        LessThanOrEqual,
        Between,
        IsNull,
        IsNotNull,
        In,
        NotIn
    }

    /// <summary>
    /// フィルタ条件の論理演算子
    /// </summary>
    public enum LogicalOperator
    {
        And,
        Or
    }

    /// <summary>
    /// フィルタ条件項目
    /// </summary>
    public class FilterCondition : ViewModelBase
    {
        private string _field = string.Empty;
        private FilterOperator _operator = FilterOperator.Contains;
        private object? _value;
        private object? _value2; // Between用の2番目の値
        private LogicalOperator _logicalOperator = LogicalOperator.And;

        public string Field
        {
            get => _field;
            set => SetProperty(ref _field, value);
        }

        public FilterOperator Operator
        {
            get => _operator;
            set => SetProperty(ref _operator, value);
        }

        public object? Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        public object? Value2
        {
            get => _value2;
            set => SetProperty(ref _value2, value);
        }

        public LogicalOperator LogicalOperator
        {
            get => _logicalOperator;
            set => SetProperty(ref _logicalOperator, value);
        }

        public string DisplayText => GenerateDisplayText();

        private string GenerateDisplayText()
        {
            var operatorText = _operator switch
            {
                FilterOperator.Contains => "contains",
                FilterOperator.Equals => "equals",
                FilterOperator.NotEquals => "not equals",
                FilterOperator.StartsWith => "starts with",
                FilterOperator.EndsWith => "ends with",
                FilterOperator.GreaterThan => ">",
                FilterOperator.LessThan => "<",
                FilterOperator.GreaterThanOrEqual => ">=",
                FilterOperator.LessThanOrEqual => "<=",
                FilterOperator.Between => "between",
                FilterOperator.IsNull => "is null",
                FilterOperator.IsNotNull => "is not null",
                FilterOperator.In => "in",
                FilterOperator.NotIn => "not in",
                _ => "contains"
            };

            if (_operator == FilterOperator.Between && _value != null && _value2 != null)
            {
                return $"{_field} {operatorText} {_value} and {_value2}";
            }
            else if (_operator == FilterOperator.IsNull || _operator == FilterOperator.IsNotNull)
            {
                return $"{_field} {operatorText}";
            }
            else
            {
                return $"{_field} {operatorText} {_value}";
            }
        }
    }

    /// <summary>
    /// 保存済み検索条件
    /// </summary>
    public class SavedFilter : ViewModelBase
    {
        private string _name = string.Empty;
        private string _description = string.Empty;
        private DateTime _createdDate = DateTime.Now;
        private bool _isGlobal = false;
        private string _pluginId = string.Empty;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public DateTime CreatedDate
        {
            get => _createdDate;
            set => SetProperty(ref _createdDate, value);
        }

        public bool IsGlobal
        {
            get => _isGlobal;
            set => SetProperty(ref _isGlobal, value);
        }

        public string PluginId
        {
            get => _pluginId;
            set => SetProperty(ref _pluginId, value);
        }

        public ObservableCollection<FilterCondition> Conditions { get; } = new();

        public override string ToString() => _name;
    }

    /// <summary>
    /// クイックフィルタ項目
    /// </summary>
    public class QuickFilter : ViewModelBase
    {
        private string _name = string.Empty;
        private string _icon = string.Empty;
        private string _tooltip = string.Empty;
        private bool _isActive = false;

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        public string Tooltip
        {
            get => _tooltip;
            set => SetProperty(ref _tooltip, value);
        }

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public ObservableCollection<FilterCondition> Conditions { get; } = new();
    }

    /// <summary>
    /// 検索履歴項目
    /// </summary>
    public class SearchHistory : ViewModelBase
    {
        private string _searchText = string.Empty;
        private DateTime _timestamp = DateTime.Now;
        private string _pluginId = string.Empty;
        private int _resultCount = 0;

        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        public DateTime Timestamp
        {
            get => _timestamp;
            set => SetProperty(ref _timestamp, value);
        }

        public string PluginId
        {
            get => _pluginId;
            set => SetProperty(ref _pluginId, value);
        }

        public int ResultCount
        {
            get => _resultCount;
            set => SetProperty(ref _resultCount, value);
        }

        public string DisplayText => $"{_searchText} ({_resultCount} results) - {_timestamp:MM/dd HH:mm}";
    }

    /// <summary>
    /// フィルタ可能フィールドの定義
    /// </summary>
    public class FilterableField
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public Type DataType { get; set; } = typeof(string);
        public List<FilterOperator> SupportedOperators { get; set; } = new();
        public List<object>? PredefinedValues { get; set; }

        public static FilterableField CreateStringField(string name, string displayName)
        {
            return new FilterableField
            {
                Name = name,
                DisplayName = displayName,
                DataType = typeof(string),
                SupportedOperators = new List<FilterOperator>
                {
                    FilterOperator.Contains,
                    FilterOperator.Equals,
                    FilterOperator.NotEquals,
                    FilterOperator.StartsWith,
                    FilterOperator.EndsWith,
                    FilterOperator.IsNull,
                    FilterOperator.IsNotNull
                }
            };
        }

        public static FilterableField CreateDateField(string name, string displayName)
        {
            return new FilterableField
            {
                Name = name,
                DisplayName = displayName,
                DataType = typeof(DateTime),
                SupportedOperators = new List<FilterOperator>
                {
                    FilterOperator.Equals,
                    FilterOperator.NotEquals,
                    FilterOperator.GreaterThan,
                    FilterOperator.LessThan,
                    FilterOperator.GreaterThanOrEqual,
                    FilterOperator.LessThanOrEqual,
                    FilterOperator.Between,
                    FilterOperator.IsNull,
                    FilterOperator.IsNotNull
                }
            };
        }

        public static FilterableField CreateEnumField(string name, string displayName, List<object> values)
        {
            return new FilterableField
            {
                Name = name,
                DisplayName = displayName,
                DataType = typeof(string),
                SupportedOperators = new List<FilterOperator>
                {
                    FilterOperator.Equals,
                    FilterOperator.NotEquals,
                    FilterOperator.In,
                    FilterOperator.NotIn
                },
                PredefinedValues = values
            };
        }
    }
}