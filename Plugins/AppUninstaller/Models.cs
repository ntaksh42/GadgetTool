using System;
using System.ComponentModel;

namespace GadgetTools.Plugins.AppUninstaller
{
    public class InstalledApp : INotifyPropertyChanged
    {
        private bool _isSelected;
        
        public string Name { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public DateTime? InstallDate { get; set; }
        public string InstallLocation { get; set; } = string.Empty;
        public string UninstallString { get; set; } = string.Empty;
        public long? EstimatedSize { get; set; }
        public string Icon { get; set; } = string.Empty;
        
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }
        
        public string FormattedSize => EstimatedSize.HasValue ? FormatBytes(EstimatedSize.Value) : "不明";
        
        public string FormattedInstallDate => InstallDate?.ToString("yyyy/MM/dd") ?? "不明";
        
        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            
            return $"{number:N1}{suffixes[counter]}";
        }
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    
    public enum UninstallStatus
    {
        Pending,
        InProgress,
        Success,
        Failed
    }
    
    public class UninstallResult
    {
        public InstalledApp App { get; set; } = new();
        public UninstallStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public Exception? Exception { get; set; }
    }
}