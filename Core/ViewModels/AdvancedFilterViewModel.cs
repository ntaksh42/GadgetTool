using System.Collections.ObjectModel;
using System.Windows.Input;
using GadgetTools.Core.Models;
using GadgetTools.Core.Services;

namespace GadgetTools.Core.ViewModels
{
    /// <summary>
    /// 高度なフィルタ画面のViewModel
    /// </summary>
    public class AdvancedFilterViewModel : ViewModelBase
    {
        private readonly AdvancedFilterService _filterService;
        private readonly string _pluginId;
        private SavedFilter? _selectedSavedFilter;

        public AdvancedFilterViewModel(string pluginId)
        {
            _pluginId = pluginId;
            _filterService = AdvancedFilterService.Instance;

            // コマンドの初期化
            AddConditionCommand = new RelayCommand(AddCondition);
            RemoveConditionCommand = new RelayCommand<FilterCondition>(RemoveCondition);
            DuplicateConditionCommand = new RelayCommand<FilterCondition>(DuplicateCondition);
            SaveFilterCommand = new RelayCommand(SaveFilter, CanSaveFilter);
            ClearAllCommand = new RelayCommand(ClearAll);
            ApplyFilterCommand = new RelayCommand(ApplyFilter);
            ResetFilterCommand = new RelayCommand(ResetFilter);
            ToggleQuickFilterCommand = new RelayCommand<QuickFilter>(ToggleQuickFilter);

            // 初期化
            InitializeData();
        }

        #region Properties

        public ObservableCollection<FilterCondition> FilterConditions { get; } = new();
        public ObservableCollection<QuickFilter> QuickFilters { get; } = new();
        public ObservableCollection<SavedFilter> SavedFilters { get; } = new();
        public List<FilterableField> AvailableFields { get; private set; } = new();

        public SavedFilter? SelectedSavedFilter
        {
            get => _selectedSavedFilter;
            set
            {
                if (SetProperty(ref _selectedSavedFilter, value) && value != null)
                {
                    LoadSavedFilter(value);
                }
            }
        }

        public string FilterStatusText => GenerateFilterStatusText();

        #endregion

        #region Commands

        public ICommand AddConditionCommand { get; }
        public ICommand RemoveConditionCommand { get; }
        public ICommand DuplicateConditionCommand { get; }
        public ICommand SaveFilterCommand { get; }
        public ICommand ClearAllCommand { get; }
        public ICommand ApplyFilterCommand { get; }
        public ICommand ResetFilterCommand { get; }
        public ICommand ToggleQuickFilterCommand { get; }

        #endregion

        #region Events

        public event EventHandler<List<FilterCondition>>? FilterApplied;

        #endregion

        #region Private Methods

        private void InitializeData()
        {
            // フィールド定義を取得
            AvailableFields = _filterService.GetPluginFields(_pluginId);

            // 保存済みフィルタを読み込み
            var savedFilters = _filterService.GetSavedFilters(_pluginId);
            SavedFilters.Clear();
            foreach (var filter in savedFilters)
            {
                SavedFilters.Add(filter);
            }

            // クイックフィルタを初期化（プラグイン固有の実装が必要）
            InitializeQuickFilters();

            // デフォルト条件を追加
            if (!FilterConditions.Any())
            {
                AddCondition();
            }
        }

        private void InitializeQuickFilters()
        {
            // 基本的なクイックフィルタを追加
            try
            {
                QuickFilters.Add(new QuickFilter
                {
                    Name = "Recent",
                    Icon = "🕒",
                    Tooltip = "Show items from last 7 days"
                });

                QuickFilters.Add(new QuickFilter
                {
                    Name = "Active",
                    Icon = "🔵",
                    Tooltip = "Show only active items"
                });

                QuickFilters.Add(new QuickFilter
                {
                    Name = "Mine",
                    Icon = "👤",
                    Tooltip = "Show items assigned to me"
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"QuickFilter initialization error: {ex.Message}");
            }
        }

        private void AddCondition()
        {
            var condition = new FilterCondition();
            
            // デフォルト値設定
            if (AvailableFields.Any())
            {
                condition.Field = AvailableFields.First().Name;
            }

            // 最初の条件以外は AND で接続
            if (FilterConditions.Any())
            {
                condition.LogicalOperator = LogicalOperator.And;
            }

            FilterConditions.Add(condition);
            OnPropertyChanged(nameof(FilterStatusText));
        }

        private void RemoveCondition(FilterCondition? condition)
        {
            if (condition != null)
            {
                FilterConditions.Remove(condition);
                OnPropertyChanged(nameof(FilterStatusText));
            }
        }

        private void DuplicateCondition(FilterCondition? condition)
        {
            if (condition != null)
            {
                var duplicate = new FilterCondition
                {
                    Field = condition.Field,
                    Operator = condition.Operator,
                    Value = condition.Value,
                    Value2 = condition.Value2,
                    LogicalOperator = LogicalOperator.And
                };

                var index = FilterConditions.IndexOf(condition);
                FilterConditions.Insert(index + 1, duplicate);
                OnPropertyChanged(nameof(FilterStatusText));
            }
        }

        private void SaveFilter()
        {
            // 簡単なダイアログでフィルタ名を取得（実際の実装では専用ダイアログを使用）
            var name = $"Filter_{DateTime.Now:yyyyMMdd_HHmmss}";

            var savedFilter = new SavedFilter
            {
                Name = name,
                PluginId = _pluginId,
                CreatedDate = DateTime.Now
            };

            foreach (var condition in FilterConditions)
            {
                savedFilter.Conditions.Add(new FilterCondition
                {
                    Field = condition.Field,
                    Operator = condition.Operator,
                    Value = condition.Value,
                    Value2 = condition.Value2,
                    LogicalOperator = condition.LogicalOperator
                });
            }

            _filterService.SaveFilter(savedFilter);
            SavedFilters.Add(savedFilter);
        }

        private bool CanSaveFilter()
        {
            return FilterConditions.Any(c => !string.IsNullOrEmpty(c.Field));
        }

        private void ClearAll()
        {
            FilterConditions.Clear();
            
            // クイックフィルタもリセット
            foreach (var quickFilter in QuickFilters)
            {
                quickFilter.IsActive = false;
            }

            AddCondition(); // デフォルト条件を追加
            OnPropertyChanged(nameof(FilterStatusText));
        }

        private void ApplyFilter()
        {
            var activeConditions = FilterConditions.Where(c => !string.IsNullOrEmpty(c.Field)).ToList();
            
            // クイックフィルタの条件も追加
            foreach (var quickFilter in QuickFilters.Where(qf => qf.IsActive))
            {
                activeConditions.AddRange(quickFilter.Conditions);
            }

            FilterApplied?.Invoke(this, activeConditions);
        }

        private void ResetFilter()
        {
            ClearAll();
            ApplyFilter();
        }

        private void ToggleQuickFilter(QuickFilter? quickFilter)
        {
            if (quickFilter != null)
            {
                quickFilter.IsActive = !quickFilter.IsActive;
                OnPropertyChanged(nameof(FilterStatusText));
            }
        }

        private void LoadSavedFilter(SavedFilter savedFilter)
        {
            FilterConditions.Clear();

            foreach (var condition in savedFilter.Conditions)
            {
                FilterConditions.Add(new FilterCondition
                {
                    Field = condition.Field,
                    Operator = condition.Operator,
                    Value = condition.Value,
                    Value2 = condition.Value2,
                    LogicalOperator = condition.LogicalOperator
                });
            }

            if (!FilterConditions.Any())
            {
                AddCondition();
            }

            OnPropertyChanged(nameof(FilterStatusText));
        }

        private string GenerateFilterStatusText()
        {
            var activeConditions = FilterConditions.Count(c => !string.IsNullOrEmpty(c.Field));
            var activeQuickFilters = QuickFilters.Count(qf => qf.IsActive);

            if (activeConditions == 0 && activeQuickFilters == 0)
            {
                return "No filters applied";
            }

            var parts = new List<string>();
            if (activeConditions > 0)
            {
                parts.Add($"{activeConditions} condition(s)");
            }
            if (activeQuickFilters > 0)
            {
                parts.Add($"{activeQuickFilters} quick filter(s)");
            }

            return $"Active: {string.Join(", ", parts)}";
        }

        #endregion

        #region Helper Properties for UI Binding

        public List<FilterOperator> GetOperatorsForField(string fieldName)
        {
            var field = AvailableFields.FirstOrDefault(f => f.Name == fieldName);
            return field?.SupportedOperators ?? new List<FilterOperator> { FilterOperator.Contains };
        }

        public List<object>? GetPredefinedValues(string fieldName)
        {
            var field = AvailableFields.FirstOrDefault(f => f.Name == fieldName);
            return field?.PredefinedValues;
        }

        #endregion
    }
}