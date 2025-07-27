namespace GadgetTools.Core.Models
{
    /// <summary>
    /// プラグインのメタデータ情報
    /// </summary>
    public class PluginMetadata
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Version Version { get; set; } = new Version(1, 0, 0);
        public string? IconPath { get; set; }
        public int Priority { get; set; } = 0;
        public bool IsEnabled { get; set; } = true;
        public string AssemblyPath { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public DateTime LastModified { get; set; } = DateTime.Now;
        public List<string> Dependencies { get; set; } = new List<string>();
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// プラグインの実行時情報
    /// </summary>
    public class PluginInstance
    {
        public PluginMetadata Metadata { get; set; } = new PluginMetadata();
        public object? Plugin { get; set; }
        public object? ViewModel { get; set; }
        public System.Windows.Controls.UserControl? View { get; set; }
        public bool IsLoaded { get; set; } = false;
        public Exception? LoadError { get; set; }
    }
}