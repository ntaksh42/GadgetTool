using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using GadgetTools.Core.Models;

namespace GadgetTools.Core.Services
{
    /// <summary>
    /// 高度なフィルタ機能を提供するサービス
    /// </summary>
    public class AdvancedFilterService
    {
        private static AdvancedFilterService? _instance;
        public static AdvancedFilterService Instance => _instance ??= new AdvancedFilterService();

        private readonly ObservableCollection<SavedFilter> _savedFilters = new();
        private readonly ObservableCollection<SearchHistory> _searchHistory = new();
        private readonly Dictionary<string, List<FilterableField>> _pluginFields = new();

        public ObservableCollection<SavedFilter> SavedFilters => _savedFilters;
        public ObservableCollection<SearchHistory> SearchHistory => _searchHistory;

        /// <summary>
        /// プラグインのフィルタ可能フィールドを登録
        /// </summary>
        public void RegisterPluginFields(string pluginId, List<FilterableField> fields)
        {
            _pluginFields[pluginId] = fields;
        }

        /// <summary>
        /// プラグインのフィルタ可能フィールドを取得
        /// </summary>
        public List<FilterableField> GetPluginFields(string pluginId)
        {
            return _pluginFields.TryGetValue(pluginId, out var fields) ? fields : new List<FilterableField>();
        }

        /// <summary>
        /// フィルタを適用
        /// </summary>
        public bool ApplyFilter<T>(T item, List<FilterCondition> conditions)
        {
            if (!conditions.Any()) return true;

            bool result = true;
            LogicalOperator? previousLogicalOp = null;

            foreach (var condition in conditions)
            {
                bool conditionResult = EvaluateCondition(item, condition);

                if (previousLogicalOp == null)
                {
                    result = conditionResult;
                }
                else if (previousLogicalOp == LogicalOperator.And)
                {
                    result = result && conditionResult;
                }
                else if (previousLogicalOp == LogicalOperator.Or)
                {
                    result = result || conditionResult;
                }

                previousLogicalOp = condition.LogicalOperator;
            }

            return result;
        }

        /// <summary>
        /// 個別条件を評価
        /// </summary>
        private bool EvaluateCondition<T>(T item, FilterCondition condition)
        {
            if (item == null) return false;

            var value = GetPropertyValue(item, condition.Field);

            return condition.Operator switch
            {
                FilterOperator.Contains => EvaluateContains(value, condition.Value),
                FilterOperator.Equals => EvaluateEquals(value, condition.Value),
                FilterOperator.NotEquals => !EvaluateEquals(value, condition.Value),
                FilterOperator.StartsWith => EvaluateStartsWith(value, condition.Value),
                FilterOperator.EndsWith => EvaluateEndsWith(value, condition.Value),
                FilterOperator.GreaterThan => EvaluateComparison(value, condition.Value, 1),
                FilterOperator.LessThan => EvaluateComparison(value, condition.Value, -1),
                FilterOperator.GreaterThanOrEqual => EvaluateComparison(value, condition.Value, 0, 1),
                FilterOperator.LessThanOrEqual => EvaluateComparison(value, condition.Value, -1, 0),
                FilterOperator.Between => EvaluateBetween(value, condition.Value, condition.Value2),
                FilterOperator.IsNull => value == null,
                FilterOperator.IsNotNull => value != null,
                FilterOperator.In => EvaluateIn(value, condition.Value),
                FilterOperator.NotIn => !EvaluateIn(value, condition.Value),
                _ => true
            };
        }

        /// <summary>
        /// リフレクションでプロパティ値を取得
        /// </summary>
        private object? GetPropertyValue<T>(T item, string propertyPath)
        {
            try
            {
                var parts = propertyPath.Split('.');
                object? current = item;

                foreach (var part in parts)
                {
                    if (current == null) return null;

                    var property = current.GetType().GetProperty(part, BindingFlags.Public | BindingFlags.Instance);
                    if (property == null) return null;

                    current = property.GetValue(current);
                }

                return current;
            }
            catch
            {
                return null;
            }
        }

        #region 条件評価メソッド

        private bool EvaluateContains(object? actual, object? expected)
        {
            if (actual == null || expected == null) return false;
            return actual.ToString()?.Contains(expected.ToString() ?? "", StringComparison.OrdinalIgnoreCase) ?? false;
        }

        private bool EvaluateEquals(object? actual, object? expected)
        {
            if (actual == null && expected == null) return true;
            if (actual == null || expected == null) return false;

            // 型変換を試行
            if (actual.GetType() != expected.GetType())
            {
                try
                {
                    expected = Convert.ChangeType(expected, actual.GetType(), CultureInfo.InvariantCulture);
                }
                catch
                {
                    return false;
                }
            }

            return actual.Equals(expected);
        }

        private bool EvaluateStartsWith(object? actual, object? expected)
        {
            if (actual == null || expected == null) return false;
            return actual.ToString()?.StartsWith(expected.ToString() ?? "", StringComparison.OrdinalIgnoreCase) ?? false;
        }

        private bool EvaluateEndsWith(object? actual, object? expected)
        {
            if (actual == null || expected == null) return false;
            return actual.ToString()?.EndsWith(expected.ToString() ?? "", StringComparison.OrdinalIgnoreCase) ?? false;
        }

        private bool EvaluateComparison(object? actual, object? expected, params int[] validResults)
        {
            if (actual == null || expected == null) return false;

            try
            {
                if (actual is IComparable comparable && expected is IComparable)
                {
                    // 型変換を試行
                    if (actual.GetType() != expected.GetType())
                    {
                        expected = Convert.ChangeType(expected, actual.GetType(), CultureInfo.InvariantCulture);
                    }

                    var result = comparable.CompareTo(expected);
                    return validResults.Contains(result) || validResults.Any(v => v > 0 && result > 0) || validResults.Any(v => v < 0 && result < 0);
                }
            }
            catch
            {
                // 比較できない場合は false
            }

            return false;
        }

        private bool EvaluateBetween(object? actual, object? min, object? max)
        {
            if (actual == null || min == null || max == null) return false;

            try
            {
                if (actual is IComparable comparable)
                {
                    // 型変換を試行
                    if (actual.GetType() != min.GetType())
                    {
                        min = Convert.ChangeType(min, actual.GetType(), CultureInfo.InvariantCulture);
                    }
                    if (actual.GetType() != max.GetType())
                    {
                        max = Convert.ChangeType(max, actual.GetType(), CultureInfo.InvariantCulture);
                    }

                    return comparable.CompareTo(min) >= 0 && comparable.CompareTo(max) <= 0;
                }
            }
            catch
            {
                // 比較できない場合は false
            }

            return false;
        }

        private bool EvaluateIn(object? actual, object? expected)
        {
            if (actual == null || expected == null) return false;

            if (expected is string expectedStr)
            {
                var values = expectedStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                       .Select(v => v.Trim())
                                       .ToList();
                return values.Any(v => EvaluateEquals(actual, v));
            }

            if (expected is IEnumerable<object> enumerable)
            {
                return enumerable.Any(v => EvaluateEquals(actual, v));
            }

            return false;
        }

        #endregion

        /// <summary>
        /// 検索履歴を追加
        /// </summary>
        public void AddSearchHistory(string searchText, string pluginId, int resultCount)
        {
            if (string.IsNullOrWhiteSpace(searchText)) return;

            // 既存の同じ検索文字列があれば削除
            var existing = _searchHistory.FirstOrDefault(h => h.SearchText == searchText && h.PluginId == pluginId);
            if (existing != null)
            {
                _searchHistory.Remove(existing);
            }

            // 新しい履歴を追加
            _searchHistory.Insert(0, new SearchHistory
            {
                SearchText = searchText,
                PluginId = pluginId,
                ResultCount = resultCount,
                Timestamp = DateTime.Now
            });

            // 履歴は最大50件まで
            while (_searchHistory.Count > 50)
            {
                _searchHistory.RemoveAt(_searchHistory.Count - 1);
            }
        }

        /// <summary>
        /// プラグイン固有の検索履歴を取得
        /// </summary>
        public List<SearchHistory> GetSearchHistory(string pluginId)
        {
            return _searchHistory.Where(h => h.PluginId == pluginId).Take(10).ToList();
        }

        /// <summary>
        /// 保存済みフィルタを追加
        /// </summary>
        public void SaveFilter(SavedFilter filter)
        {
            // 同名のフィルタがあれば削除
            var existing = _savedFilters.FirstOrDefault(f => f.Name == filter.Name && f.PluginId == filter.PluginId);
            if (existing != null)
            {
                _savedFilters.Remove(existing);
            }

            _savedFilters.Add(filter);
        }

        /// <summary>
        /// プラグイン固有の保存済みフィルタを取得
        /// </summary>
        public List<SavedFilter> GetSavedFilters(string pluginId)
        {
            return _savedFilters.Where(f => f.PluginId == pluginId || f.IsGlobal).ToList();
        }
    }
}