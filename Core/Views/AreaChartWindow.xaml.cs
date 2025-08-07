using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using GadgetTools.Core.ViewModels;
using GadgetTools.Core.Models;

namespace GadgetTools.Core.Views
{
    /// <summary>
    /// エリアごとの集計チャートウィンドウ
    /// </summary>
    public partial class AreaChartWindow : Window
    {
        private AreaChartViewModel _viewModel;

        public AreaChartWindow(List<GadgetTools.Shared.Models.WorkItem> workItems)
        {
            InitializeComponent();
            _viewModel = new AreaChartViewModel(workItems);
            DataContext = _viewModel;
            
            Loaded += AreaChartWindow_Loaded;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        private void AreaChartWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize ComboBox data sources
            CategoryTypeComboBox.ItemsSource = Enum.GetValues(typeof(CategoryType));
            AggregationTypeComboBox.ItemsSource = Enum.GetValues(typeof(AggregationType));
            ChartTypeComboBox.ItemsSource = Enum.GetValues(typeof(ChartType));
            TimePeriodComboBox.ItemsSource = Enum.GetValues(typeof(TimePeriodType));
            
            // Set default selections
            CategoryTypeComboBox.SelectedItem = CategoryType.Feature;
            TimePeriodComboBox.SelectedItem = TimePeriodType.Monthly;
            
            // Update combo box items based on current mode
            UpdateComboBoxItems();
            
            // Initial chart draw
            DrawChart();
        }

        private void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AreaChartViewModel.Aggregations) ||
                e.PropertyName == nameof(AreaChartViewModel.SelectedCategoryType) ||
                e.PropertyName == nameof(AreaChartViewModel.SelectedAggregationType) ||
                e.PropertyName == nameof(AreaChartViewModel.SelectedChartType) ||
                e.PropertyName == nameof(AreaChartViewModel.ShowValues) ||
                e.PropertyName == nameof(AreaChartViewModel.ShowPercentages) ||
                e.PropertyName == nameof(AreaChartViewModel.ShowPriorityBreakdown))
            {
                Dispatcher.BeginInvoke(new Action(DrawChart));
            }
            
            if (e.PropertyName == nameof(AreaChartViewModel.IsTimeSeriesMode))
            {
                UpdateComboBoxItems();
            }
        }
        
        private void UpdateComboBoxItems()
        {
            if (_viewModel.IsTimeSeriesMode)
            {
                // Time series mode - show time series specific aggregations
                var timeSeriesAggregations = new[]
                {
                    AggregationType.CreatedTrend,
                    AggregationType.ResolvedTrend,
                    AggregationType.CumulativeCreated,
                    AggregationType.TotalCount,
                    AggregationType.ActiveCount,
                    AggregationType.ResolvedCount,
                    AggregationType.ClosedCount
                };
                AggregationTypeComboBox.ItemsSource = timeSeriesAggregations;
                
                // Add Line chart for time series
                var timeSeriesChartTypes = new[]
                {
                    ChartType.Line,
                    ChartType.Bar,
                    ChartType.HorizontalBar
                };
                ChartTypeComboBox.ItemsSource = timeSeriesChartTypes;
                
                // Set default to Line chart for time series
                if (_viewModel.SelectedChartType != ChartType.Line)
                {
                    _viewModel.SelectedChartType = ChartType.Line;
                }
                
                // Set default to CreatedTrend
                if (!timeSeriesAggregations.Contains(_viewModel.SelectedAggregationType))
                {
                    _viewModel.SelectedAggregationType = AggregationType.CreatedTrend;
                }
            }
            else
            {
                // Regular mode - show all aggregations except time series specific ones
                var regularAggregations = Enum.GetValues(typeof(AggregationType))
                    .Cast<AggregationType>()
                    .Where(t => t != AggregationType.CreatedTrend && 
                               t != AggregationType.ResolvedTrend && 
                               t != AggregationType.UpdatedTrend &&
                               t != AggregationType.CumulativeCreated &&
                               t != AggregationType.BurndownChart)
                    .ToArray();
                AggregationTypeComboBox.ItemsSource = regularAggregations;
                
                // Regular chart types (no Line chart)
                var regularChartTypes = new[]
                {
                    ChartType.Bar,
                    ChartType.HorizontalBar
                };
                ChartTypeComboBox.ItemsSource = regularChartTypes;
                
                // Reset to Bar chart if Line was selected
                if (_viewModel.SelectedChartType == ChartType.Line)
                {
                    _viewModel.SelectedChartType = ChartType.Bar;
                }
                
                // Set default to TotalCount
                if (!regularAggregations.Contains(_viewModel.SelectedAggregationType))
                {
                    _viewModel.SelectedAggregationType = AggregationType.TotalCount;
                }
            }
        }

        private void DrawChart()
        {
            ChartCanvas.Children.Clear();
            
            if (_viewModel.Aggregations == null || !_viewModel.Aggregations.Any())
            {
                DrawNoDataMessage();
                return;
            }

            var data = _viewModel.GetChartDataPoints();
            if (!data.Any())
            {
                DrawNoDataMessage();
                return;
            }

            if (_viewModel.SelectedChartType == ChartType.HorizontalBar)
            {
                DrawHorizontalBarChart(data);
            }
            else
            {
                DrawVerticalBarChart(data);
            }
        }

        private void DrawNoDataMessage()
        {
            var textBlock = new TextBlock
            {
                Text = "表示するデータがありません",
                FontSize = 16,
                Foreground = Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            Canvas.SetLeft(textBlock, (ChartCanvas.ActualWidth - 200) / 2);
            Canvas.SetTop(textBlock, (ChartCanvas.ActualHeight - 30) / 2);
            ChartCanvas.Children.Add(textBlock);
        }

        private void DrawVerticalBarChart(List<BarChartDataPoint> data)
        {
            const double margin = 50;
            const double barSpacing = 10;
            
            var canvasWidth = Math.Max(500, data.Count * 80 + margin * 2);
            var canvasHeight = 400;
            
            ChartCanvas.Width = canvasWidth;
            ChartCanvas.Height = canvasHeight;
            
            // Add modern background
            ChartCanvas.Background = new LinearGradientBrush(
                Color.FromArgb(255, 250, 252, 255),
                Color.FromArgb(255, 245, 248, 252),
                90);
            
            var chartWidth = canvasWidth - margin * 2;
            var chartHeight = canvasHeight - margin * 2;
            var barWidth = (chartWidth - barSpacing * (data.Count - 1)) / data.Count;
            
            var maxValue = data.Max(d => d.Value);
            if (maxValue == 0) maxValue = 1;
            
            // Draw axes
            DrawAxes(margin, chartWidth, chartHeight, true);
            
            // Draw bars
            for (int i = 0; i < data.Count; i++)
            {
                var dataPoint = data[i];
                var barHeight = (dataPoint.Value / (double)maxValue) * (chartHeight - 40);
                var x = margin + i * (barWidth + barSpacing);
                var y = canvasHeight - margin - barHeight;
                
                // Draw stacked or single bar based on priority breakdown setting
                if (dataPoint.ShowPriorityBreakdown && dataPoint.PrioritySegments.Any())
                {
                    DrawStackedVerticalBar(x, y, barWidth, barHeight, dataPoint.PrioritySegments);
                }
                else
                {
                    // Single bar with modern styling
                    var bar = new Rectangle
                    {
                        Width = barWidth,
                        Height = barHeight,
                        Fill = CreateModernBarBrush(dataPoint.Color, true),
                        Stroke = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                        StrokeThickness = 1,
                        RadiusX = 4,
                        RadiusY = 4,
                        Effect = CreateDropShadowEffect()
                    };
                    
                    Canvas.SetLeft(bar, x);
                    Canvas.SetTop(bar, y);
                    
                    // Add click handler for drill-down
                    AddClickHandler(bar, dataPoint);
                    
                    ChartCanvas.Children.Add(bar);
                    
                    // Add subtle hover effect
                    AddHoverEffect(bar, dataPoint);
                }
                
                // Value label
                if (_viewModel.ShowValues)
                {
                    var valueText = _viewModel.ShowPercentages ? $"{dataPoint.Percentage:F1}%" : dataPoint.Value.ToString();
                    var valueLabel = new TextBlock
                    {
                        Text = valueText,
                        FontSize = 10,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromArgb(200, 60, 60, 60)),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Effect = new DropShadowEffect
                        {
                            Color = Color.FromArgb(100, 255, 255, 255),
                            Direction = 0,
                            ShadowDepth = 0,
                            BlurRadius = 2
                        }
                    };
                    
                    Canvas.SetLeft(valueLabel, x + barWidth / 2 - 15);
                    Canvas.SetTop(valueLabel, y - 20);
                    ChartCanvas.Children.Add(valueLabel);
                }
                
                // X-axis label
                var label = new TextBlock
                {
                    Text = dataPoint.Label,
                    FontSize = 9,
                    Foreground = Brushes.Black,
                    Width = barWidth + 20,
                    TextAlignment = TextAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                
                Canvas.SetLeft(label, x - 10);
                Canvas.SetTop(label, canvasHeight - margin + 5);
                ChartCanvas.Children.Add(label);
            }
            
            // Draw Y-axis labels
            DrawYAxisLabels(margin, chartHeight, maxValue, true);
        }

        private void DrawHorizontalBarChart(List<BarChartDataPoint> data)
        {
            const double margin = 50;
            const double barSpacing = 5;
            const double labelWidth = 120;
            
            var canvasWidth = 600;
            var canvasHeight = Math.Max(300, data.Count * 40 + margin * 2);
            
            ChartCanvas.Width = canvasWidth;
            ChartCanvas.Height = canvasHeight;
            
            // Add modern background
            ChartCanvas.Background = new LinearGradientBrush(
                Color.FromArgb(255, 250, 252, 255),
                Color.FromArgb(255, 245, 248, 252),
                90);
            
            var chartWidth = canvasWidth - margin * 2 - labelWidth;
            var chartHeight = canvasHeight - margin * 2;
            var barHeight = (chartHeight - barSpacing * (data.Count - 1)) / data.Count;
            
            var maxValue = data.Max(d => d.Value);
            if (maxValue == 0) maxValue = 1;
            
            // Draw axes
            DrawAxes(margin + labelWidth, chartWidth, chartHeight, false);
            
            // Draw bars
            for (int i = 0; i < data.Count; i++)
            {
                var dataPoint = data[i];
                var barWidth = (dataPoint.Value / (double)maxValue) * chartWidth;
                var x = margin + labelWidth;
                var y = margin + i * (barHeight + barSpacing);
                
                // Draw stacked or single bar based on priority breakdown setting
                if (dataPoint.ShowPriorityBreakdown && dataPoint.PrioritySegments.Any())
                {
                    DrawStackedHorizontalBar(x, y, barWidth, barHeight, dataPoint.PrioritySegments);
                }
                else
                {
                    // Single bar with modern styling
                    var bar = new Rectangle
                    {
                        Width = barWidth,
                        Height = barHeight,
                        Fill = CreateModernBarBrush(dataPoint.Color, false),
                        Stroke = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
                        StrokeThickness = 1,
                        RadiusX = 4,
                        RadiusY = 4,
                        Effect = CreateDropShadowEffect()
                    };
                    
                    Canvas.SetLeft(bar, x);
                    Canvas.SetTop(bar, y);
                    
                    // Add click handler for drill-down
                    AddClickHandler(bar, dataPoint);
                    
                    ChartCanvas.Children.Add(bar);
                    
                    // Add subtle hover effect
                    AddHoverEffect(bar, dataPoint);
                }
                
                // Value label
                if (_viewModel.ShowValues)
                {
                    var valueText = _viewModel.ShowPercentages ? $"{dataPoint.Percentage:F1}%" : dataPoint.Value.ToString();
                    var valueLabel = new TextBlock
                    {
                        Text = valueText,
                        FontSize = 10,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromArgb(200, 60, 60, 60)),
                        VerticalAlignment = VerticalAlignment.Center,
                        Effect = new DropShadowEffect
                        {
                            Color = Color.FromArgb(100, 255, 255, 255),
                            Direction = 0,
                            ShadowDepth = 0,
                            BlurRadius = 2
                        }
                    };
                    
                    Canvas.SetLeft(valueLabel, x + barWidth + 5);
                    Canvas.SetTop(valueLabel, y + barHeight / 2 - 7);
                    ChartCanvas.Children.Add(valueLabel);
                }
                
                // Y-axis label
                var label = new TextBlock
                {
                    Text = dataPoint.Label,
                    FontSize = 10,
                    Foreground = Brushes.Black,
                    Width = labelWidth - 10,
                    TextAlignment = TextAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                
                Canvas.SetLeft(label, margin);
                Canvas.SetTop(label, y + barHeight / 2 - 7);
                ChartCanvas.Children.Add(label);
            }
            
            // Draw X-axis labels
            DrawXAxisLabels(margin + labelWidth, chartWidth, maxValue);
        }

        private void DrawAxes(double margin, double chartWidth, double chartHeight, bool isVertical)
        {
            var axisColor = new SolidColorBrush(Color.FromArgb(120, 80, 80, 80));
            var axisThickness = 2;
            
            if (isVertical)
            {
                // Y-axis
                var yAxis = new Line
                {
                    X1 = margin,
                    Y1 = margin,
                    X2 = margin,
                    Y2 = margin + chartHeight,
                    Stroke = axisColor,
                    StrokeThickness = axisThickness
                };
                ChartCanvas.Children.Add(yAxis);
                
                // X-axis
                var xAxis = new Line
                {
                    X1 = margin,
                    Y1 = margin + chartHeight,
                    X2 = margin + chartWidth,
                    Y2 = margin + chartHeight,
                    Stroke = axisColor,
                    StrokeThickness = axisThickness
                };
                ChartCanvas.Children.Add(xAxis);
            }
            else
            {
                // Y-axis
                var yAxis = new Line
                {
                    X1 = margin,
                    Y1 = margin,
                    X2 = margin,
                    Y2 = margin + chartHeight,
                    Stroke = axisColor,
                    StrokeThickness = axisThickness
                };
                ChartCanvas.Children.Add(yAxis);
                
                // X-axis
                var xAxis = new Line
                {
                    X1 = margin,
                    Y1 = margin + chartHeight,
                    X2 = margin + chartWidth,
                    Y2 = margin + chartHeight,
                    Stroke = axisColor,
                    StrokeThickness = axisThickness
                };
                ChartCanvas.Children.Add(xAxis);
            }
        }

        private void DrawYAxisLabels(double margin, double chartHeight, int maxValue, bool isVertical)
        {
            var labelCount = 5;
            var interval = maxValue / labelCount;
            if (interval == 0) interval = 1;
            
            for (int i = 0; i <= labelCount; i++)
            {
                var value = i * interval;
                var y = margin + chartHeight - (i * chartHeight / labelCount);
                
                var label = new TextBlock
                {
                    Text = value.ToString(),
                    FontSize = 9,
                    Foreground = Brushes.DarkGray,
                    TextAlignment = TextAlignment.Right
                };
                
                Canvas.SetLeft(label, margin - 30);
                Canvas.SetTop(label, y - 7);
                ChartCanvas.Children.Add(label);
                
                // Modern grid line
                var gridLine = new Line
                {
                    X1 = margin,
                    Y1 = y,
                    X2 = margin + ChartCanvas.Width - margin * 2,
                    Y2 = y,
                    Stroke = new SolidColorBrush(Color.FromArgb(30, 120, 120, 120)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 4, 6 }
                };
                ChartCanvas.Children.Add(gridLine);
            }
        }

        private void DrawXAxisLabels(double margin, double chartWidth, int maxValue)
        {
            var labelCount = 5;
            var interval = maxValue / labelCount;
            if (interval == 0) interval = 1;
            
            for (int i = 0; i <= labelCount; i++)
            {
                var value = i * interval;
                var x = margin + (i * chartWidth / labelCount);
                
                var label = new TextBlock
                {
                    Text = value.ToString(),
                    FontSize = 9,
                    Foreground = Brushes.DarkGray,
                    TextAlignment = TextAlignment.Center
                };
                
                Canvas.SetLeft(label, x - 15);
                Canvas.SetTop(label, ChartCanvas.Height - 40);
                ChartCanvas.Children.Add(label);
                
                // Modern grid line
                var gridLine = new Line
                {
                    X1 = x,
                    Y1 = margin,
                    X2 = x,
                    Y2 = ChartCanvas.Height - 50,
                    Stroke = new SolidColorBrush(Color.FromArgb(30, 120, 120, 120)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 4, 6 }
                };
                ChartCanvas.Children.Add(gridLine);
            }
        }

        private void DrawStackedVerticalBar(double x, double y, double barWidth, double totalBarHeight, List<PrioritySegment> segments)
        {
            double currentY = y + totalBarHeight; // Start from bottom
            
            foreach (var segment in segments)
            {
                var segmentHeight = (segment.Count / (double)segments.Sum(s => s.Count)) * totalBarHeight;
                currentY -= segmentHeight;
                
                var segmentBar = new Rectangle
                {
                    Width = barWidth,
                    Height = segmentHeight,
                    Fill = CreateModernBarBrush(segment.Color, true),
                    Stroke = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                    StrokeThickness = 0.5,
                    RadiusX = 2,
                    RadiusY = 2
                };
                
                Canvas.SetLeft(segmentBar, x);
                Canvas.SetTop(segmentBar, currentY);
                ChartCanvas.Children.Add(segmentBar);
                
                // Add priority label if segment is large enough
                if (segmentHeight > 15)
                {
                    var priorityLabel = new TextBlock
                    {
                        Text = segment.Count.ToString(),
                        FontSize = 8,
                        Foreground = Brushes.White,
                        FontWeight = FontWeights.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    
                    Canvas.SetLeft(priorityLabel, x + barWidth / 2 - 10);
                    Canvas.SetTop(priorityLabel, currentY + segmentHeight / 2 - 6);
                    ChartCanvas.Children.Add(priorityLabel);
                }
            }
        }

        private void DrawStackedHorizontalBar(double x, double y, double totalBarWidth, double barHeight, List<PrioritySegment> segments)
        {
            double currentX = x; // Start from left
            
            foreach (var segment in segments)
            {
                var segmentWidth = (segment.Count / (double)segments.Sum(s => s.Count)) * totalBarWidth;
                
                var segmentBar = new Rectangle
                {
                    Width = segmentWidth,
                    Height = barHeight,
                    Fill = CreateModernBarBrush(segment.Color, false),
                    Stroke = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                    StrokeThickness = 0.5,
                    RadiusX = 2,
                    RadiusY = 2
                };
                
                Canvas.SetLeft(segmentBar, currentX);
                Canvas.SetTop(segmentBar, y);
                ChartCanvas.Children.Add(segmentBar);
                
                // Add priority label if segment is large enough
                if (segmentWidth > 20)
                {
                    var priorityLabel = new TextBlock
                    {
                        Text = segment.Count.ToString(),
                        FontSize = 8,
                        Foreground = Brushes.White,
                        FontWeight = FontWeights.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    
                    Canvas.SetLeft(priorityLabel, currentX + segmentWidth / 2 - 5);
                    Canvas.SetTop(priorityLabel, y + barHeight / 2 - 6);
                    ChartCanvas.Children.Add(priorityLabel);
                }
                
                currentX += segmentWidth;
            }
        }

        #region Modern Styling Methods

        private Brush CreateModernBarBrush(string colorString, bool isVertical)
        {
            var baseColor = (Color)ColorConverter.ConvertFromString(colorString);
            
            var gradient = new LinearGradientBrush()
            {
                StartPoint = isVertical ? new Point(0, 0) : new Point(0, 0),
                EndPoint = isVertical ? new Point(1, 0) : new Point(0, 1)
            };
            
            // Create a subtle gradient effect
            var lighterColor = Color.FromArgb(
                baseColor.A,
                (byte)Math.Min(255, baseColor.R + 30),
                (byte)Math.Min(255, baseColor.G + 30),
                (byte)Math.Min(255, baseColor.B + 30)
            );
            
            var darkerColor = Color.FromArgb(
                baseColor.A,
                (byte)Math.Max(0, baseColor.R - 20),
                (byte)Math.Max(0, baseColor.G - 20),
                (byte)Math.Max(0, baseColor.B - 20)
            );
            
            gradient.GradientStops.Add(new GradientStop(lighterColor, 0.0));
            gradient.GradientStops.Add(new GradientStop(baseColor, 0.5));
            gradient.GradientStops.Add(new GradientStop(darkerColor, 1.0));
            
            return gradient;
        }

        private DropShadowEffect CreateDropShadowEffect()
        {
            return new DropShadowEffect
            {
                Color = Color.FromArgb(80, 0, 0, 0),
                Direction = 315,
                ShadowDepth = 3,
                BlurRadius = 5,
                Opacity = 0.5
            };
        }

        private void AddHoverEffect(Rectangle bar, BarChartDataPoint dataPoint)
        {
            var originalBrush = bar.Fill;
            var originalOpacity = bar.Opacity;
            
            bar.MouseEnter += (s, e) =>
            {
                // Brighten on hover
                var animation = new DoubleAnimation
                {
                    To = 0.8,
                    Duration = TimeSpan.FromMilliseconds(150),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                bar.BeginAnimation(Rectangle.OpacityProperty, animation);
                
                // Add glow effect
                var glowEffect = new DropShadowEffect
                {
                    Color = Color.FromArgb(120, 255, 255, 255),
                    Direction = 0,
                    ShadowDepth = 0,
                    BlurRadius = 8,
                    Opacity = 0.7
                };
                bar.Effect = glowEffect;
                
                // Show tooltip
                bar.ToolTip = CreateModernToolTip(dataPoint);
            };
            
            bar.MouseLeave += (s, e) =>
            {
                // Restore original appearance
                var animation = new DoubleAnimation
                {
                    To = originalOpacity,
                    Duration = TimeSpan.FromMilliseconds(150),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                bar.BeginAnimation(Rectangle.OpacityProperty, animation);
                
                // Restore original shadow
                bar.Effect = CreateDropShadowEffect();
            };
        }

        private ToolTip CreateModernToolTip(BarChartDataPoint dataPoint)
        {
            var tooltip = new ToolTip
            {
                Background = new SolidColorBrush(Color.FromArgb(240, 40, 40, 40)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(12, 8, 12, 8),
                HasDropShadow = true
            };
            
            var panel = new StackPanel();
            
            var titleBlock = new TextBlock
            {
                Text = dataPoint.Label,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 4)
            };
            
            var valueBlock = new TextBlock
            {
                Text = $"件数: {dataPoint.Value}",
                FontSize = 11
            };
            
            var percentageBlock = new TextBlock
            {
                Text = $"割合: {dataPoint.Percentage:F1}%",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255))
            };
            
            var clickHintBlock = new TextBlock
            {
                Text = "🖱️ クリックしてドリルダウン",
                FontSize = 10,
                FontStyle = FontStyles.Italic,
                Margin = new Thickness(0, 4, 0, 0),
                Foreground = new SolidColorBrush(Color.FromArgb(180, 173, 216, 255))
            };
            
            panel.Children.Add(titleBlock);
            panel.Children.Add(valueBlock);
            panel.Children.Add(percentageBlock);
            panel.Children.Add(clickHintBlock);
            
            if (!string.IsNullOrEmpty(dataPoint.Description))
            {
                var descBlock = new TextBlock
                {
                    Text = dataPoint.Description,
                    FontSize = 10,
                    Margin = new Thickness(0, 4, 0, 0),
                    Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 250
                };
                panel.Children.Add(descBlock);
            }
            
            tooltip.Content = panel;
            return tooltip;
        }

        #endregion

        #region Drill-down functionality

        /// <summary>
        /// Event fired when user clicks on a chart element for drill-down
        /// </summary>
        public event EventHandler<ChartDrillDownEventArgs>? ChartElementClicked;

        private void AddClickHandler(Rectangle bar, BarChartDataPoint dataPoint)
        {
            bar.Cursor = Cursors.Hand;
            bar.MouseLeftButtonUp += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"Chart bar clicked: {dataPoint.Label} ({dataPoint.Value} items)");
                OnChartElementClicked(dataPoint);
            };
        }

        private void OnChartElementClicked(BarChartDataPoint dataPoint)
        {
            var eventArgs = new ChartDrillDownEventArgs
            {
                Label = dataPoint.Label,
                Value = dataPoint.Value,
                CategoryType = _viewModel.SelectedCategoryType,
                AggregationType = _viewModel.SelectedAggregationType,
                IsTimeSeriesMode = _viewModel.IsTimeSeriesMode,
                WorkItems = GetWorkItemsForDataPoint(dataPoint)
            };

            System.Diagnostics.Debug.WriteLine($"Firing ChartElementClicked event for {dataPoint.Label} with {eventArgs.WorkItems.Count} work items");
            ChartElementClicked?.Invoke(this, eventArgs);
        }

        private List<GadgetTools.Shared.Models.WorkItem> GetWorkItemsForDataPoint(BarChartDataPoint dataPoint)
        {
            // Find the aggregation data that matches this data point
            var aggregationData = _viewModel.Aggregations
                .FirstOrDefault(a => a.CategoryName == dataPoint.Label);

            return aggregationData?.WorkItems ?? new List<GadgetTools.Shared.Models.WorkItem>();
        }

        #endregion
    }

    /// <summary>
    /// Event arguments for chart drill-down functionality
    /// </summary>
    public class ChartDrillDownEventArgs : EventArgs
    {
        public string Label { get; set; } = "";
        public int Value { get; set; }
        public CategoryType CategoryType { get; set; }
        public AggregationType AggregationType { get; set; }
        public bool IsTimeSeriesMode { get; set; }
        public List<GadgetTools.Shared.Models.WorkItem> WorkItems { get; set; } = new List<GadgetTools.Shared.Models.WorkItem>();
    }
}