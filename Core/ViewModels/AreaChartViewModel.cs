using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using GadgetTools.Core.Models;
using GadgetTools.Shared.Models;

namespace GadgetTools.Core.ViewModels
{
    /// <summary>
    /// 集計チャートのViewModel
    /// </summary>
    public class AreaChartViewModel : INotifyPropertyChanged
    {
        private readonly List<WorkItem> _originalWorkItems;
        private ObservableCollection<AggregationData> _aggregations;
        private AggregationData? _selectedItem;
        private AggregationType _selectedAggregationType = AggregationType.TotalCount;
        private CategoryType _selectedCategoryType = CategoryType.Feature;
        private ChartType _selectedChartType = ChartType.Bar;
        private bool _showValues = true;
        private bool _showPercentages = false;
        private bool _showPriorityBreakdown = false;
        private TimePeriodType _selectedTimePeriod = TimePeriodType.Monthly;
        private DateTime _startDate = DateTime.Today.AddMonths(-6);
        private DateTime _endDate = DateTime.Today;
        private bool _isTimeSeriesMode = false;

        public event PropertyChangedEventHandler? PropertyChanged;

        public AreaChartViewModel(List<WorkItem> workItems)
        {
            _originalWorkItems = workItems ?? new List<WorkItem>();
            _aggregations = new ObservableCollection<AggregationData>();
            
            InitializeCommands();
            ProcessWorkItems();
        }

        #region Properties

        public ObservableCollection<AggregationData> Aggregations
        {
            get => _aggregations;
            set => SetProperty(ref _aggregations, value);
        }

        public AggregationData? SelectedItem
        {
            get => _selectedItem;
            set => SetProperty(ref _selectedItem, value);
        }

        public CategoryType SelectedCategoryType
        {
            get => _selectedCategoryType;
            set
            {
                if (SetProperty(ref _selectedCategoryType, value))
                {
                    ProcessWorkItems();
                }
            }
        }

        public AggregationType SelectedAggregationType
        {
            get => _selectedAggregationType;
            set => SetProperty(ref _selectedAggregationType, value);
        }

        public ChartType SelectedChartType
        {
            get => _selectedChartType;
            set => SetProperty(ref _selectedChartType, value);
        }

        public bool ShowValues
        {
            get => _showValues;
            set => SetProperty(ref _showValues, value);
        }

        public bool ShowPercentages
        {
            get => _showPercentages;
            set => SetProperty(ref _showPercentages, value);
        }

        public bool ShowPriorityBreakdown
        {
            get => _showPriorityBreakdown;
            set => SetProperty(ref _showPriorityBreakdown, value);
        }

        public TimePeriodType SelectedTimePeriod
        {
            get => _selectedTimePeriod;
            set
            {
                if (SetProperty(ref _selectedTimePeriod, value))
                {
                    if (_isTimeSeriesMode) ProcessTimeSeriesData();
                }
            }
        }

        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                if (SetProperty(ref _startDate, value))
                {
                    if (_isTimeSeriesMode) ProcessTimeSeriesData();
                }
            }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                if (SetProperty(ref _endDate, value))
                {
                    if (_isTimeSeriesMode) ProcessTimeSeriesData();
                }
            }
        }

        public bool IsTimeSeriesMode
        {
            get => _isTimeSeriesMode;
            set
            {
                if (SetProperty(ref _isTimeSeriesMode, value))
                {
                    if (value)
                    {
                        ProcessTimeSeriesData();
                    }
                    else
                    {
                        ProcessWorkItems();
                    }
                    OnPropertyChanged(nameof(ChartTitle));
                }
            }
        }

        public string ChartTitle => GetChartTitle();

        public string TotalItemsText => $"({_originalWorkItems.Count} 件のワークアイテム)";

        public string StatusText => $"{_aggregations.Count} 件 | {GetCategoryTypeDisplayName(_selectedCategoryType)} | {GetAggregationTypeDisplayName(_selectedAggregationType)}";

        public string LastUpdatedText => $"最終更新: {DateTime.Now:yyyy/MM/dd HH:mm}";

        public string SummaryText => GetSummaryText();

        #endregion

        #region Commands

        public ICommand RefreshCommand { get; private set; }
        public ICommand ExportCommand { get; private set; }
        public ICommand ResetCommand { get; private set; }

        private void InitializeCommands()
        {
            RefreshCommand = new RelayCommand(ExecuteRefresh);
            ExportCommand = new RelayCommand(ExecuteExport);
            ResetCommand = new RelayCommand(ExecuteReset);
        }

        private void ExecuteRefresh()
        {
            ProcessWorkItems();
            OnPropertyChanged(nameof(ChartTitle));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(LastUpdatedText));
            OnPropertyChanged(nameof(SummaryText));
        }

        private void ExecuteExport()
        {
            // TODO: Implement export functionality
            // CSV, Excel, Image export options
        }

        private void ExecuteReset()
        {
            SelectedAggregationType = AggregationType.TotalCount;
            SelectedChartType = ChartType.Bar;
            ShowValues = true;
            ShowPercentages = false;
            SelectedItem = null;
        }

        #endregion

        #region Data Processing

        private void ProcessWorkItems()
        {
            var categoryGroups = _originalWorkItems
                .GroupBy(wi => GetCategoryValue(wi, _selectedCategoryType))
                .OrderByDescending(g => g.Count())
                .ToList();

            _aggregations.Clear();

            foreach (var group in categoryGroups)
            {
                var aggregationData = new AggregationData
                {
                    CategoryPath = group.Key,
                    CategoryName = GetCategoryDisplayName(group.Key, _selectedCategoryType),
                    WorkItems = group.ToList()
                };

                CalculateAggregationValues(aggregationData);
                _aggregations.Add(aggregationData);
            }

            OnPropertyChanged(nameof(Aggregations));
        }

        private string GetCategoryValue(WorkItem workItem, CategoryType categoryType)
        {
            return categoryType switch
            {
                CategoryType.Area => !string.IsNullOrEmpty(workItem.Fields.AreaPath) ? workItem.Fields.AreaPath : "未分類",
                CategoryType.Feature => ExtractFeatureFromTitle(workItem.Fields.Title),
                CategoryType.Priority => GetPriorityRank(workItem.Fields.Priority),
                CategoryType.WorkItemType => !string.IsNullOrEmpty(workItem.Fields.WorkItemType) ? workItem.Fields.WorkItemType : "Unknown",
                CategoryType.State => !string.IsNullOrEmpty(workItem.Fields.State) ? workItem.Fields.State : "Unknown",
                CategoryType.AssignedTo => workItem.Fields.AssignedTo?.DisplayName ?? "未割当",
                _ => "その他"
            };
        }

        private string GetCategoryDisplayName(string categoryValue, CategoryType categoryType)
        {
            return categoryType switch
            {
                CategoryType.Area => categoryValue.Split('\\').LastOrDefault() ?? categoryValue,
                CategoryType.Feature => categoryValue,
                CategoryType.Priority => categoryValue,
                CategoryType.WorkItemType => categoryValue,
                CategoryType.State => categoryValue,
                CategoryType.AssignedTo => categoryValue,
                _ => categoryValue
            };
        }

        private string ExtractFeatureFromTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
                return "その他";

            // タイトルから機能名を推測（例：[機能名] タイトル、機能名: タイトル など）
            var patterns = new[]
            {
                @"^\[([^\]]+)\]",      // [機能名] パターン
                @"^([^:]+):",          // 機能名: パターン
                @"^([^-]+)-",          // 機能名- パターン
                @"^([^_]+)_"           // 機能名_ パターン
            };

            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(title, pattern);
                if (match.Success)
                {
                    return match.Groups[1].Value.Trim();
                }
            }

            // パターンに一致しない場合、最初の単語を機能名として使用
            var words = title.Split(new[] { ' ', '　' }, StringSplitOptions.RemoveEmptyEntries);
            return words.Length > 0 ? words[0] : "その他";
        }

        private string GetPriorityRank(int priority)
        {
            return priority switch
            {
                1 => "高優先度",
                2 => "中優先度", 
                3 => "低優先度",
                4 => "最低優先度",
                _ => "未設定"
            };
        }

        private void CalculateAggregationValues(AggregationData aggregationData)
        {
            var workItems = aggregationData.WorkItems;
            
            aggregationData.TotalCount = workItems.Count;
            aggregationData.ActiveCount = workItems.Count(wi => GetState(wi) == "Active");
            aggregationData.ResolvedCount = workItems.Count(wi => GetState(wi) == "Resolved");
            aggregationData.ClosedCount = workItems.Count(wi => GetState(wi) == "Closed");
            
            aggregationData.BugCount = workItems.Count(wi => GetWorkItemType(wi) == "Bug");
            aggregationData.TaskCount = workItems.Count(wi => GetWorkItemType(wi) == "Task");
            aggregationData.UserStoryCount = workItems.Count(wi => GetWorkItemType(wi) == "User Story");
            aggregationData.FeatureCount = workItems.Count(wi => GetWorkItemType(wi) == "Feature");
            
            // Calculate priority counts
            aggregationData.HighPriorityCount = workItems.Count(wi => GetPriority(wi) == 1);
            aggregationData.MediumPriorityCount = workItems.Count(wi => GetPriority(wi) == 2);
            aggregationData.LowPriorityCount = workItems.Count(wi => GetPriority(wi) >= 3);
            
            // Calculate average priority
            var priorities = workItems
                .Select(wi => GetPriority(wi))
                .Where(p => p > 0)
                .ToList();
            
            aggregationData.AveragePriority = priorities.Any() ? priorities.Average() : 0.0;
        }

        private string GetState(WorkItem workItem)
        {
            return !string.IsNullOrEmpty(workItem.Fields.State) ? workItem.Fields.State : "Unknown";
        }

        private string GetWorkItemType(WorkItem workItem)
        {
            return !string.IsNullOrEmpty(workItem.Fields.WorkItemType) ? workItem.Fields.WorkItemType : "Unknown";
        }

        private int GetPriority(WorkItem workItem)
        {
            return workItem.Fields.Priority;
        }

        private void ProcessTimeSeriesData()
        {
            _aggregations.Clear();
            
            var timePeriods = GenerateTimePeriods(_startDate, _endDate, _selectedTimePeriod);
            
            foreach (var period in timePeriods)
            {
                var periodStart = period.Start;
                var periodEnd = period.End;
                
                var createdInPeriod = _originalWorkItems
                    .Where(wi => wi.Fields.CreatedDate >= periodStart && wi.Fields.CreatedDate < periodEnd)
                    .ToList();
                
                var resolvedInPeriod = _originalWorkItems
                    .Where(wi => (wi.Fields.State == "Resolved" || wi.Fields.State == "Closed") && 
                               wi.Fields.ChangedDate >= periodStart && 
                               wi.Fields.ChangedDate < periodEnd)
                    .ToList();
                
                var aggregationData = new AggregationData
                {
                    CategoryPath = period.Label,
                    CategoryName = period.ShortLabel,
                    WorkItems = createdInPeriod
                };
                
                // 時系列特有の集計
                aggregationData.TotalCount = GetTimeSeriesValue(createdInPeriod, resolvedInPeriod, _selectedAggregationType);
                
                // その他の集計も計算
                CalculateAggregationValues(aggregationData);
                
                _aggregations.Add(aggregationData);
            }
            
            OnPropertyChanged(nameof(Aggregations));
        }
        
        private int GetTimeSeriesValue(List<WorkItem> createdItems, List<WorkItem> resolvedItems, AggregationType type)
        {
            return type switch
            {
                AggregationType.CreatedTrend => createdItems.Count,
                AggregationType.ResolvedTrend => resolvedItems.Count,
                AggregationType.CumulativeCreated => createdItems.Count, // これは後で累積計算する
                _ => createdItems.Count
            };
        }
        
        private List<TimePeriod> GenerateTimePeriods(DateTime start, DateTime end, TimePeriodType periodType)
        {
            var periods = new List<TimePeriod>();
            var current = start;
            
            while (current < end)
            {
                var periodEnd = periodType switch
                {
                    TimePeriodType.Daily => current.AddDays(1),
                    TimePeriodType.Weekly => current.AddDays(7),
                    TimePeriodType.Monthly => current.AddMonths(1),
                    TimePeriodType.Quarterly => current.AddMonths(3),
                    _ => current.AddMonths(1)
                };
                
                if (periodEnd > end) periodEnd = end;
                
                periods.Add(new TimePeriod
                {
                    Start = current,
                    End = periodEnd,
                    Label = FormatPeriodLabel(current, periodType),
                    ShortLabel = FormatPeriodShortLabel(current, periodType)
                });
                
                current = periodEnd;
            }
            
            return periods;
        }
        
        private string FormatPeriodLabel(DateTime date, TimePeriodType periodType)
        {
            return periodType switch
            {
                TimePeriodType.Daily => date.ToString("yyyy年MM月dd日"),
                TimePeriodType.Weekly => $"{date:yyyy年MM月dd日}週",
                TimePeriodType.Monthly => date.ToString("yyyy年MM月"),
                TimePeriodType.Quarterly => $"{date.Year}年Q{(date.Month - 1) / 3 + 1}",
                _ => date.ToString("yyyy年MM月")
            };
        }
        
        private string FormatPeriodShortLabel(DateTime date, TimePeriodType periodType)
        {
            return periodType switch
            {
                TimePeriodType.Daily => date.ToString("MM/dd"),
                TimePeriodType.Weekly => date.ToString("MM/dd"),
                TimePeriodType.Monthly => date.ToString("yyyy/MM"),
                TimePeriodType.Quarterly => $"{date.Year}Q{(date.Month - 1) / 3 + 1}",
                _ => date.ToString("yyyy/MM")
            };
        }

        #endregion

        #region Chart Data

        public List<BarChartDataPoint> GetChartDataPoints()
        {
            var colors = new[]
            {
                "#4472C4", "#E06666", "#6AA84F", "#FF9900", "#9900FF",
                "#00B8D4", "#795548", "#607D8B", "#F44336", "#4CAF50"
            };

            var priorityColors = new Dictionary<int, string>
            {
                { 1, "#FF4444" }, // 高優先度 - 赤
                { 2, "#FF9900" }, // 中優先度 - オレンジ
                { 3, "#4472C4" }, // 低優先度 - 青
                { 4, "#888888" }  // 最低優先度 - グレー
            };

            var dataPoints = new List<BarChartDataPoint>();
            var totalValue = GetTotalValue();

            for (int i = 0; i < _aggregations.Count; i++)
            {
                var item = _aggregations[i];
                var value = GetAggregationValue(item, _selectedAggregationType);
                var percentage = totalValue > 0 ? (double)value / totalValue * 100 : 0;

                var dataPoint = new BarChartDataPoint
                {
                    Label = item.CategoryName,
                    Value = value,
                    Color = colors[i % colors.Length],
                    Description = GetItemDescription(item),
                    Percentage = percentage,
                    ShowPriorityBreakdown = _showPriorityBreakdown
                };

                // 優先度別の詳細データを追加
                if (_showPriorityBreakdown)
                {
                    dataPoint.PrioritySegments = GetPrioritySegments(item, priorityColors);
                }

                dataPoints.Add(dataPoint);
            }

            return dataPoints.OrderByDescending(dp => dp.Value).ToList();
        }

        private string GetItemDescription(AggregationData item)
        {
            var description = $"{item.CategoryPath}\n";
            description += $"総数: {item.TotalCount}\n";
            description += $"高優先度: {item.HighPriorityCount}\n";
            description += $"中優先度: {item.MediumPriorityCount}\n";
            description += $"低優先度: {item.LowPriorityCount}";
            return description;
        }

        private List<PrioritySegment> GetPrioritySegments(AggregationData item, Dictionary<int, string> priorityColors)
        {
            var segments = new List<PrioritySegment>();

            if (item.HighPriorityCount > 0)
            {
                segments.Add(new PrioritySegment
                {
                    Priority = 1,
                    Count = item.HighPriorityCount,
                    Color = priorityColors[1],
                    Label = "高優先度"
                });
            }

            if (item.MediumPriorityCount > 0)
            {
                segments.Add(new PrioritySegment
                {
                    Priority = 2,
                    Count = item.MediumPriorityCount,
                    Color = priorityColors[2],
                    Label = "中優先度"
                });
            }

            // Separate low priority (3) and lowest priority (4) for better visualization
            var workItems = item.WorkItems;
            var priority3Count = workItems.Count(wi => GetPriority(wi) == 3);
            var priority4Count = workItems.Count(wi => GetPriority(wi) == 4);
            
            if (priority3Count > 0)
            {
                segments.Add(new PrioritySegment
                {
                    Priority = 3,
                    Count = priority3Count,
                    Color = priorityColors[3],
                    Label = "低優先度"
                });
            }
            
            if (priority4Count > 0)
            {
                segments.Add(new PrioritySegment
                {
                    Priority = 4,
                    Count = priority4Count,
                    Color = priorityColors[4],
                    Label = "最低優先度"
                });
            }

            return segments;
        }

        private int GetAggregationValue(AggregationData item, AggregationType type)
        {
            return type switch
            {
                AggregationType.TotalCount => item.TotalCount,
                AggregationType.ActiveCount => item.ActiveCount,
                AggregationType.ResolvedCount => item.ResolvedCount,
                AggregationType.ClosedCount => item.ClosedCount,
                AggregationType.BugCount => item.BugCount,
                AggregationType.TaskCount => item.TaskCount,
                AggregationType.UserStoryCount => item.UserStoryCount,
                AggregationType.FeatureCount => item.FeatureCount,
                AggregationType.AveragePriority => (int)Math.Round(item.AveragePriority),
                AggregationType.HighPriorityCount => item.HighPriorityCount,
                AggregationType.MediumPriorityCount => item.MediumPriorityCount,
                AggregationType.LowPriorityCount => item.LowPriorityCount,
                _ => item.TotalCount
            };
        }

        private int GetTotalValue()
        {
            return _aggregations.Sum(item => GetAggregationValue(item, _selectedAggregationType));
        }

        #endregion

        #region Display Helpers

        private string GetChartTitle()
        {
            if (_isTimeSeriesMode)
            {
                var periodName = GetTimePeriodDisplayName(_selectedTimePeriod);
                var typeName = GetAggregationTypeDisplayName(_selectedAggregationType);
                return $"{periodName}{typeName}";
            }
            else
            {
                var categoryName = GetCategoryTypeDisplayName(_selectedCategoryType);
                var typeName = GetAggregationTypeDisplayName(_selectedAggregationType);
                return $"{categoryName}{typeName}";
            }
        }

        private string GetTimePeriodDisplayName(TimePeriodType type)
        {
            return type switch
            {
                TimePeriodType.Daily => "日別",
                TimePeriodType.Weekly => "週別",
                TimePeriodType.Monthly => "月別",
                TimePeriodType.Quarterly => "四半期別",
                _ => "月別"
            };
        }

        private string GetCategoryTypeDisplayName(CategoryType type)
        {
            return type switch
            {
                CategoryType.Area => "エリア別",
                CategoryType.Feature => "機能別",
                CategoryType.Priority => "優先度別",
                CategoryType.WorkItemType => "種類別",
                CategoryType.State => "状態別",
                CategoryType.AssignedTo => "担当者別",
                _ => "集計"
            };
        }

        private string GetAggregationTypeDisplayName(AggregationType type)
        {
            return type switch
            {
                AggregationType.TotalCount => "総数",
                AggregationType.ActiveCount => "Active数",
                AggregationType.ResolvedCount => "Resolved数",
                AggregationType.ClosedCount => "Closed数",
                AggregationType.BugCount => "Bug数",
                AggregationType.TaskCount => "Task数",
                AggregationType.UserStoryCount => "User Story数",
                AggregationType.FeatureCount => "Feature数",
                AggregationType.AveragePriority => "平均優先度",
                AggregationType.HighPriorityCount => "高優先度数",
                AggregationType.MediumPriorityCount => "中優先度数",
                AggregationType.LowPriorityCount => "低優先度数",
                AggregationType.CreatedTrend => "作成トレンド",
                AggregationType.ResolvedTrend => "解決トレンド",
                AggregationType.UpdatedTrend => "更新トレンド",
                AggregationType.CumulativeCreated => "累積作成数",
                AggregationType.BurndownChart => "バーンダウン",
                _ => "総数"
            };
        }

        private string GetSummaryText()
        {
            if (!_aggregations.Any())
                return "データがありません";

            var totalItems = _originalWorkItems.Count;
            var totalCategories = _aggregations.Count;
            var avgPerCategory = totalCategories > 0 ? (double)totalItems / totalCategories : 0;

            var topItem = _aggregations.OrderByDescending(a => GetAggregationValue(a, _selectedAggregationType)).FirstOrDefault();
            var topItemName = topItem?.CategoryName ?? "N/A";
            var topItemValue = topItem != null ? GetAggregationValue(topItem, _selectedAggregationType) : 0;

            var categoryTypeName = GetCategoryTypeDisplayName(_selectedCategoryType).Replace("別", "");
            return $"合計 {totalItems} 件\n平均 {avgPerCategory:F1} 件/{categoryTypeName}\n最大: {topItemName} ({topItemValue} 件)";
        }

        #endregion

        #region INotifyPropertyChanged

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

        #endregion
    }
}