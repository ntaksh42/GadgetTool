using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using GadgetTools.Shared.Models;

namespace GadgetTools.Core.Models
{
    /// <summary>
    /// チャートデータのベースクラス
    /// </summary>
    public abstract class ChartDataBase : INotifyPropertyChanged
    {
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
    }

    /// <summary>
    /// 棒グラフ用のデータポイント
    /// </summary>
    public class BarChartDataPoint : ChartDataBase
    {
        private string _label = "";
        private int _value = 0;
        private string _color = "#4472C4";
        private string _description = "";

        public string Label
        {
            get => _label;
            set => SetProperty(ref _label, value);
        }

        public int Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        public string Color
        {
            get => _color;
            set => SetProperty(ref _color, value);
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public double Percentage { get; set; }
        
        // 優先度別の詳細データ
        public List<PrioritySegment> PrioritySegments { get; set; } = new List<PrioritySegment>();
        public bool ShowPriorityBreakdown { get; set; } = false;
    }

    /// <summary>
    /// 優先度セグメント（積み上げ棒グラフ用）
    /// </summary>
    public class PrioritySegment : ChartDataBase
    {
        private int _priority = 2;
        private int _count = 0;
        private string _color = "#4472C4";
        private string _label = "";

        public int Priority
        {
            get => _priority;
            set => SetProperty(ref _priority, value);
        }

        public int Count
        {
            get => _count;
            set => SetProperty(ref _count, value);
        }

        public string Color
        {
            get => _color;
            set => SetProperty(ref _color, value);
        }

        public string Label
        {
            get => _label;
            set => SetProperty(ref _label, value);
        }

        public string PriorityText => GetPriorityText(Priority);

        private string GetPriorityText(int priority)
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
    }

    /// <summary>
    /// 集計データ（機能別、ランク別など）
    /// </summary>
    public class AggregationData : ChartDataBase
    {
        private string _categoryPath = "";
        private string _categoryName = "";
        private int _totalCount = 0;
        private int _activeCount = 0;
        private int _resolvedCount = 0;
        private int _closedCount = 0;
        private int _bugCount = 0;
        private int _taskCount = 0;
        private int _userStoryCount = 0;
        private int _featureCount = 0;
        private double _averagePriority = 0.0;
        private int _highPriorityCount = 0;
        private int _mediumPriorityCount = 0;
        private int _lowPriorityCount = 0;

        public string CategoryPath
        {
            get => _categoryPath;
            set => SetProperty(ref _categoryPath, value);
        }

        public string CategoryName
        {
            get => _categoryName;
            set => SetProperty(ref _categoryName, value);
        }

        public int TotalCount
        {
            get => _totalCount;
            set => SetProperty(ref _totalCount, value);
        }

        public int ActiveCount
        {
            get => _activeCount;
            set => SetProperty(ref _activeCount, value);
        }

        public int ResolvedCount
        {
            get => _resolvedCount;
            set => SetProperty(ref _resolvedCount, value);
        }

        public int ClosedCount
        {
            get => _closedCount;
            set => SetProperty(ref _closedCount, value);
        }

        public int BugCount
        {
            get => _bugCount;
            set => SetProperty(ref _bugCount, value);
        }

        public int TaskCount
        {
            get => _taskCount;
            set => SetProperty(ref _taskCount, value);
        }

        public int UserStoryCount
        {
            get => _userStoryCount;
            set => SetProperty(ref _userStoryCount, value);
        }

        public int FeatureCount
        {
            get => _featureCount;
            set => SetProperty(ref _featureCount, value);
        }

        public double AveragePriority
        {
            get => _averagePriority;
            set => SetProperty(ref _averagePriority, value);
        }

        public int HighPriorityCount
        {
            get => _highPriorityCount;
            set => SetProperty(ref _highPriorityCount, value);
        }

        public int MediumPriorityCount
        {
            get => _mediumPriorityCount;
            set => SetProperty(ref _mediumPriorityCount, value);
        }

        public int LowPriorityCount
        {
            get => _lowPriorityCount;
            set => SetProperty(ref _lowPriorityCount, value);
        }

        public List<WorkItem> WorkItems { get; set; } = new List<WorkItem>();
    }

    /// <summary>
    /// チャート設定
    /// </summary>
    public class ChartConfiguration : ChartDataBase
    {
        private string _title = "";
        private string _xAxisLabel = "";
        private string _yAxisLabel = "";
        private ChartType _chartType = ChartType.Bar;
        private AggregationType _aggregationType = AggregationType.TotalCount;
        private bool _showLegend = true;
        private bool _showValues = true;
        private bool _showPercentages = false;
        private string _colorScheme = "Default";

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string XAxisLabel
        {
            get => _xAxisLabel;
            set => SetProperty(ref _xAxisLabel, value);
        }

        public string YAxisLabel
        {
            get => _yAxisLabel;
            set => SetProperty(ref _yAxisLabel, value);
        }

        public ChartType ChartType
        {
            get => _chartType;
            set => SetProperty(ref _chartType, value);
        }

        public AggregationType AggregationType
        {
            get => _aggregationType;
            set => SetProperty(ref _aggregationType, value);
        }

        public bool ShowLegend
        {
            get => _showLegend;
            set => SetProperty(ref _showLegend, value);
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

        public string ColorScheme
        {
            get => _colorScheme;
            set => SetProperty(ref _colorScheme, value);
        }
    }

    /// <summary>
    /// チャートの種類
    /// </summary>
    public enum ChartType
    {
        Bar,           // 棒グラフ
        HorizontalBar, // 横棒グラフ
        Pie,           // 円グラフ
        Line,          // 折れ線グラフ
        Area           // エリアグラフ
    }

    /// <summary>
    /// 集計の種類
    /// </summary>
    public enum AggregationType
    {
        TotalCount,        // 総数
        ActiveCount,       // Active数
        ResolvedCount,     // Resolved数
        ClosedCount,       // Closed数
        BugCount,          // Bug数
        TaskCount,         // Task数
        UserStoryCount,    // User Story数
        FeatureCount,      // Feature数
        AveragePriority,   // 平均優先度
        HighPriorityCount, // 高優先度数
        MediumPriorityCount, // 中優先度数
        LowPriorityCount,   // 低優先度数
        CreatedTrend,      // 作成日トレンド
        ResolvedTrend,     // 解決日トレンド
        UpdatedTrend,      // 更新日トレンド
        CumulativeCreated, // 累積作成数
        BurndownChart      // バーンダウン
    }

    /// <summary>
    /// 時系列の期間タイプ
    /// </summary>
    public enum TimePeriodType
    {
        Daily,    // 日別
        Weekly,   // 週別
        Monthly,  // 月別
        Quarterly // 四半期別
    }

    /// <summary>
    /// 分類の種類
    /// </summary>
    public enum CategoryType
    {
        Area,           // エリア別
        Feature,        // 機能別（タイトルから推測）
        Priority,       // 優先度別
        WorkItemType,   // ワークアイテム種類別
        State,          // 状態別
        AssignedTo      // 担当者別
    }

    /// <summary>
    /// 時系列チャート用のデータポイント
    /// </summary>
    public class TimeSeriesDataPoint : ChartDataBase
    {
        private DateTime _date = DateTime.Today;
        private int _value = 0;
        private string _label = "";
        private string _seriesName = "";
        private string _color = "#4472C4";

        public DateTime Date
        {
            get => _date;
            set => SetProperty(ref _date, value);
        }

        public int Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        public string Label
        {
            get => _label;
            set => SetProperty(ref _label, value);
        }

        public string SeriesName
        {
            get => _seriesName;
            set => SetProperty(ref _seriesName, value);
        }

        public string Color
        {
            get => _color;
            set => SetProperty(ref _color, value);
        }

        // 追加のメトリクス
        public int CreatedCount { get; set; } = 0;
        public int ResolvedCount { get; set; } = 0;
        public int ClosedCount { get; set; } = 0;
        public int ActiveCount { get; set; } = 0;
        public double CumulativeTotal { get; set; } = 0;
    }

    /// <summary>
    /// 時系列データの系列
    /// </summary>
    public class TimeSeriesCollection : ChartDataBase
    {
        private string _seriesName = "";
        private string _color = "#4472C4";
        private bool _isVisible = true;

        public string SeriesName
        {
            get => _seriesName;
            set => SetProperty(ref _seriesName, value);
        }

        public string Color
        {
            get => _color;
            set => SetProperty(ref _color, value);
        }

        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        public List<TimeSeriesDataPoint> DataPoints { get; set; } = new List<TimeSeriesDataPoint>();
    }

    /// <summary>
    /// チャートデータセット
    /// </summary>
    public class ChartDataSet : ChartDataBase
    {
        public ObservableCollection<BarChartDataPoint> DataPoints { get; } = new ObservableCollection<BarChartDataPoint>();
        public ObservableCollection<AggregationData> AggregationDataCollection { get; } = new ObservableCollection<AggregationData>();
        public ObservableCollection<TimeSeriesCollection> TimeSeriesData { get; } = new ObservableCollection<TimeSeriesCollection>();
        public ChartConfiguration Configuration { get; set; } = new ChartConfiguration();

        private int _totalItems = 0;
        private DateTime _lastUpdated = DateTime.Now;

        public int TotalItems
        {
            get => _totalItems;
            set => SetProperty(ref _totalItems, value);
        }

        public DateTime LastUpdated
        {
            get => _lastUpdated;
            set => SetProperty(ref _lastUpdated, value);
        }
    }

    /// <summary>
    /// 時間期間のヘルパークラス
    /// </summary>
    public class TimePeriod
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Label { get; set; } = "";
        public string ShortLabel { get; set; } = "";
    }
}