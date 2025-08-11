using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace GadgetTools.Core.Services
{
    /// <summary>
    /// メモリ使用量監視サービス
    /// </summary>
    public class MemoryMonitorService : IDisposable
    {
        private readonly Timer _monitorTimer;
        private bool _disposed = false;
        
        // メモリ使用量警告しきい値（MB）
        private const long WarningThresholdMB = 500;
        private const long CriticalThresholdMB = 800;
        
        public event EventHandler<MemoryUsageEventArgs>? MemoryUsageChanged;
        public event EventHandler<MemoryWarningEventArgs>? MemoryWarning;
        
        private static readonly Lazy<MemoryMonitorService> _instance = new(() => new MemoryMonitorService());
        public static MemoryMonitorService Instance => _instance.Value;
        
        private MemoryMonitorService()
        {
            // 10秒間隔でメモリ監視
            _monitorTimer = new Timer(MonitorMemoryUsage, null, TimeSpan.Zero, TimeSpan.FromSeconds(10));
        }
        
        private void MonitorMemoryUsage(object? state)
        {
            try
            {
                var currentProcess = Process.GetCurrentProcess();
                var workingSetMB = currentProcess.WorkingSet64 / (1024 * 1024);
                var privateMemoryMB = currentProcess.PrivateMemorySize64 / (1024 * 1024);
                
                var memoryUsage = new MemoryUsageInfo
                {
                    WorkingSetMB = workingSetMB,
                    PrivateMemoryMB = privateMemoryMB,
                    Timestamp = DateTime.Now
                };
                
                // イベント発火
                MemoryUsageChanged?.Invoke(this, new MemoryUsageEventArgs(memoryUsage));
                
                // 警告チェック
                if (workingSetMB > CriticalThresholdMB)
                {
                    MemoryWarning?.Invoke(this, new MemoryWarningEventArgs(
                        MemoryWarningLevel.Critical, 
                        $"Critical memory usage: {workingSetMB} MB",
                        memoryUsage));
                        
                    // ガベージコレクション強制実行
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }
                else if (workingSetMB > WarningThresholdMB)
                {
                    MemoryWarning?.Invoke(this, new MemoryWarningEventArgs(
                        MemoryWarningLevel.Warning,
                        $"High memory usage: {workingSetMB} MB",
                        memoryUsage));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Memory monitoring error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 現在のメモリ使用量を取得
        /// </summary>
        public MemoryUsageInfo GetCurrentMemoryUsage()
        {
            var currentProcess = Process.GetCurrentProcess();
            return new MemoryUsageInfo
            {
                WorkingSetMB = currentProcess.WorkingSet64 / (1024 * 1024),
                PrivateMemoryMB = currentProcess.PrivateMemorySize64 / (1024 * 1024),
                Timestamp = DateTime.Now
            };
        }
        
        /// <summary>
        /// メモリクリーンアップ実行
        /// </summary>
        public async Task PerformCleanupAsync()
        {
            try
            {
                // キャッシュクリア
                GadgetTools.Services.AzureDevOpsService.ClearCache();
                
                // ガベージコレクション実行
                await Task.Run(() =>
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                });
                
                System.Diagnostics.Debug.WriteLine("Memory cleanup completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Memory cleanup error: {ex.Message}");
            }
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _monitorTimer?.Dispose();
                _disposed = true;
            }
        }
    }
    
    /// <summary>
    /// メモリ使用量情報
    /// </summary>
    public class MemoryUsageInfo
    {
        public long WorkingSetMB { get; set; }
        public long PrivateMemoryMB { get; set; }
        public DateTime Timestamp { get; set; }
    }
    
    /// <summary>
    /// メモリ使用量変更イベント引数
    /// </summary>
    public class MemoryUsageEventArgs : EventArgs
    {
        public MemoryUsageInfo MemoryUsage { get; }
        
        public MemoryUsageEventArgs(MemoryUsageInfo memoryUsage)
        {
            MemoryUsage = memoryUsage;
        }
    }
    
    /// <summary>
    /// メモリ警告イベント引数
    /// </summary>
    public class MemoryWarningEventArgs : EventArgs
    {
        public MemoryWarningLevel Level { get; }
        public string Message { get; }
        public MemoryUsageInfo MemoryUsage { get; }
        
        public MemoryWarningEventArgs(MemoryWarningLevel level, string message, MemoryUsageInfo memoryUsage)
        {
            Level = level;
            Message = message;
            MemoryUsage = memoryUsage;
        }
    }
    
    /// <summary>
    /// メモリ警告レベル
    /// </summary>
    public enum MemoryWarningLevel
    {
        Normal,
        Warning,
        Critical
    }
}