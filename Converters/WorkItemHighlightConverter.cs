using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using GadgetTools.Models;

namespace GadgetTools.Converters
{
    public class WorkItemHighlightConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (values.Length < 3)
                {
                    System.Diagnostics.Debug.WriteLine($"HighlightConverter: Not enough values: {values.Length}");
                    return Brushes.Transparent;
                }

                if (values[0] is not WorkItem workItem)
                {
                    System.Diagnostics.Debug.WriteLine($"HighlightConverter: Value[0] is not WorkItem: {values[0]?.GetType()}");
                    return Brushes.Transparent;
                }

                if (values[1] is not bool enableHighlight)
                {
                    System.Diagnostics.Debug.WriteLine($"HighlightConverter: Value[1] is not bool: {values[1]?.GetType()}, Value: {values[1]}");
                    return Brushes.Transparent;
                }

                if (values[2] is not int highlightDays)
                {
                    System.Diagnostics.Debug.WriteLine($"HighlightConverter: Value[2] is not int: {values[2]?.GetType()}, Value: {values[2]}");
                    return Brushes.Transparent;
                }

                System.Diagnostics.Debug.WriteLine($"HighlightConverter: WorkItem {workItem.Id}, EnableHighlight: {enableHighlight}, HighlightDays: {highlightDays}");

                if (!enableHighlight)
                {
                    System.Diagnostics.Debug.WriteLine($"HighlightConverter: Highlight disabled");
                    return Brushes.Transparent;
                }

                var daysSinceUpdate = (DateTime.Now - workItem.Fields.ChangedDate).Days;
                System.Diagnostics.Debug.WriteLine($"HighlightConverter: WorkItem {workItem.Id}, ChangedDate: {workItem.Fields.ChangedDate}, DaysSinceUpdate: {daysSinceUpdate}");
                
                if (daysSinceUpdate > highlightDays)
                {
                    // ハイライト色を段階的に変更
                    if (daysSinceUpdate > highlightDays * 3)
                    {
                        System.Diagnostics.Debug.WriteLine($"HighlightConverter: Red highlight for WorkItem {workItem.Id}");
                        return new SolidColorBrush(Color.FromArgb(80, 255, 0, 0)); // 赤
                    }
                    else if (daysSinceUpdate > highlightDays * 2)
                    {
                        System.Diagnostics.Debug.WriteLine($"HighlightConverter: Orange highlight for WorkItem {workItem.Id}");
                        return new SolidColorBrush(Color.FromArgb(60, 255, 165, 0)); // オレンジ
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"HighlightConverter: Yellow highlight for WorkItem {workItem.Id}");
                        return new SolidColorBrush(Color.FromArgb(40, 255, 255, 0)); // 黄色
                    }
                }

                System.Diagnostics.Debug.WriteLine($"HighlightConverter: No highlight for WorkItem {workItem.Id}");
                return Brushes.Transparent;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"HighlightConverter Error: {ex.Message}");
                return Brushes.Transparent;
            }
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}