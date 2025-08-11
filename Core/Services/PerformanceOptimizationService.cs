using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GadgetTools.Core.Services
{
    /// <summary>
    /// パフォーマンス最適化サービス
    /// </summary>
    public class PerformanceOptimizationService : IDisposable
    {
        private readonly Timer _optimizationTimer;
        private readonly ConcurrentDictionary<string, PerformanceMetric> _performanceMetrics;
        private bool _disposed = false;
        
        // パフォーマンス測定用
        private readonly ConcurrentDictionary<string, Stopwatch> _activeOperations;
        
        private static readonly Lazy<PerformanceOptimizationService> _instance = new(() => new PerformanceOptimizationService());
        public static PerformanceOptimizationService Instance => _instance.Value;
        
        public event EventHandler<PerformanceWarningEventArgs>? PerformanceWarning;
        
        private PerformanceOptimizationService()
        {
            _performanceMetrics = new ConcurrentDictionary<string, PerformanceMetric>();
            _activeOperations = new ConcurrentDictionary<string, Stopwatch>();
            
            // 30秒間隔で最適化実行
            _optimizationTimer = new Timer(PerformOptimizations, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }
        
        /// <summary>
        /// 操作の開始を記録
        /// </summary>
        public void StartOperation(string operationName)
        {
            var stopwatch = Stopwatch.StartNew();
            _activeOperations.TryAdd($"{operationName}_{Thread.CurrentThread.ManagedThreadId}_{DateTime.Now.Ticks}", stopwatch);
        }
        
        /// <summary>
        /// 操作の終了を記録
        /// </summary>
        public void EndOperation(string operationName, bool succeeded = true)
        {
            var threadId = Thread.CurrentThread.ManagedThreadId;
            var key = _activeOperations.Keys.FirstOrDefault(k => k.StartsWith($"{operationName}_{threadId}_"));
            
            if (key != null && _activeOperations.TryRemove(key, out var stopwatch))
            {
                stopwatch.Stop();
                RecordPerformanceMetric(operationName, stopwatch.ElapsedMilliseconds, succeeded);
            }
        }
        
        /// <summary>
        /// パフォーマンスメトリクスを記録
        /// </summary>
        private void RecordPerformanceMetric(string operationName, long elapsedMilliseconds, bool succeeded)
        {
            _performanceMetrics.AddOrUpdate(operationName, 
                new PerformanceMetric
                {
                    OperationName = operationName,
                    TotalCalls = 1,
                    SuccessfulCalls = succeeded ? 1 : 0,
                    TotalElapsedMs = elapsedMilliseconds,
                    MinElapsedMs = elapsedMilliseconds,
                    MaxElapsedMs = elapsedMilliseconds,
                    LastCallTime = DateTime.Now
                },
                (key, existing) => new PerformanceMetric
                {
                    OperationName = operationName,
                    TotalCalls = existing.TotalCalls + 1,
                    SuccessfulCalls = existing.SuccessfulCalls + (succeeded ? 1 : 0),
                    TotalElapsedMs = existing.TotalElapsedMs + elapsedMilliseconds,
                    MinElapsedMs = Math.Min(existing.MinElapsedMs, elapsedMilliseconds),
                    MaxElapsedMs = Math.Max(existing.MaxElapsedMs, elapsedMilliseconds),
                    LastCallTime = DateTime.Now
                });
                
            // 性能警告チェック
            CheckPerformanceWarnings(operationName, elapsedMilliseconds);
        }
        
        /// <summary>
        /// パフォーマンス警告チェック
        /// </summary>
        private void CheckPerformanceWarnings(string operationName, long elapsedMs)
        {
            // 操作別しきい値
            var thresholds = new Dictionary<string, long>
            {
                {"QueryWorkItems", 5000},     // 5秒
                {"ConvertExcel", 10000},      // 10秒
                {"GenerateHTML", 3000},       // 3秒
                {"LoadComments", 8000}        // 8秒
            };
            
            if (thresholds.TryGetValue(operationName, out var threshold) && elapsedMs > threshold)
            {
                PerformanceWarning?.Invoke(this, new PerformanceWarningEventArgs(
                    operationName, elapsedMs, threshold,
                    $"Operation '{operationName}' took {elapsedMs}ms (threshold: {threshold}ms)"));
            }
        }
        
        /// <summary>
        /// 定期的な最適化処理
        /// </summary>
        private async void PerformOptimizations(object? state)
        {
            try
            {
                // 長時間実行中の操作をチェック
                var longRunningOps = _activeOperations
                    .Where(kvp => kvp.Value.ElapsedMilliseconds > 30000) // 30秒以上
                    .ToList();
                
                if (longRunningOps.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"Warning: {longRunningOps.Count} long-running operations detected");
                    foreach (var op in longRunningOps)
                    {
                        System.Diagnostics.Debug.WriteLine($"Long-running: {op.Key} - {op.Value.ElapsedMilliseconds}ms");
                    }
                }
                
                // メモリ使用量が高い場合の自動クリーンアップ
                var memoryInfo = MemoryMonitorService.Instance.GetCurrentMemoryUsage();
                if (memoryInfo.WorkingSetMB > 400) // 400MB以上
                {
                    await MemoryMonitorService.Instance.PerformCleanupAsync();
                    System.Diagnostics.Debug.WriteLine($"Auto cleanup performed at {memoryInfo.WorkingSetMB}MB usage");
                }
                
                // 古いメトリクスをクリーンアップ（1時間以上前）
                CleanupOldMetrics();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Performance optimization error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 古いメトリクスをクリーンアップ
        /// </summary>
        private void CleanupOldMetrics()
        {
            var cutoffTime = DateTime.Now.AddHours(-1);
            var keysToRemove = _performanceMetrics
                .Where(kvp => kvp.Value.LastCallTime < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();
                
            foreach (var key in keysToRemove)
            {
                _performanceMetrics.TryRemove(key, out _);
            }
            
            if (keysToRemove.Any())
            {
                System.Diagnostics.Debug.WriteLine($"Cleaned up {keysToRemove.Count} old performance metrics");
            }
        }
        
        /// <summary>
        /// パフォーマンスレポートを取得
        /// </summary>
        public List<PerformanceMetric> GetPerformanceReport()
        {
            return _performanceMetrics.Values
                .OrderByDescending(m => m.AverageElapsedMs)
                .ToList();
        }
        
        /// <summary>
        /// スコープ付きパフォーマンス測定
        /// </summary>
        public IDisposable MeasureOperation(string operationName)
        {
            return new OperationMeasurement(this, operationName);
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _optimizationTimer?.Dispose();
                _disposed = true;
            }
        }
    }
    
    /// <summary>
    /// パフォーマンスメトリクス
    /// </summary>
    public class PerformanceMetric
    {
        public string OperationName { get; set; } = "";
        public int TotalCalls { get; set; }
        public int SuccessfulCalls { get; set; }
        public long TotalElapsedMs { get; set; }
        public long MinElapsedMs { get; set; }
        public long MaxElapsedMs { get; set; }
        public DateTime LastCallTime { get; set; }
        
        public long AverageElapsedMs => TotalCalls > 0 ? TotalElapsedMs / TotalCalls : 0;
        public double SuccessRate => TotalCalls > 0 ? (double)SuccessfulCalls / TotalCalls * 100 : 0;
    }
    
    /// <summary>
    /// パフォーマンス警告イベント引数
    /// </summary>
    public class PerformanceWarningEventArgs : EventArgs
    {
        public string OperationName { get; }
        public long ActualTimeMs { get; }
        public long ThresholdMs { get; }
        public string Message { get; }
        
        public PerformanceWarningEventArgs(string operationName, long actualTimeMs, long thresholdMs, string message)
        {
            OperationName = operationName;
            ActualTimeMs = actualTimeMs;
            ThresholdMs = thresholdMs;
            Message = message;
        }
    }
    
    /// <summary>
    /// 操作測定用のDisposableラッパー
    /// </summary>
    public class OperationMeasurement : IDisposable
    {
        private readonly PerformanceOptimizationService _service;
        private readonly string _operationName;
        private readonly Stopwatch _stopwatch;
        private bool _disposed = false;
        
        public OperationMeasurement(PerformanceOptimizationService service, string operationName)
        {
            _service = service;
            _operationName = operationName;
            _stopwatch = Stopwatch.StartNew();
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _stopwatch.Stop();
                _service.EndOperation(_operationName, true);
                _disposed = true;
            }
        }
    }
}