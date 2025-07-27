using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using GadgetTools.Core.Interfaces;
using GadgetTools.Core.Models;

namespace GadgetTools.Core.Services
{
    /// <summary>
    /// プラグインの管理を行うサービス
    /// </summary>
    public class PluginManager
    {
        private static PluginManager? _instance;
        private readonly Dictionary<string, PluginInstance> _loadedPlugins = new();
        private readonly List<string> _pluginDirectories = new();

        public static PluginManager Instance => _instance ??= new PluginManager();

        public ObservableCollection<PluginInstance> LoadedPlugins { get; } = new();

        private PluginManager()
        {
            // デフォルトのプラグインディレクトリを追加
            var appDir = Path.GetDirectoryName(AppContext.BaseDirectory);
            if (!string.IsNullOrEmpty(appDir))
            {
                _pluginDirectories.Add(Path.Combine(appDir, "Plugins"));
            }
        }

        /// <summary>
        /// プラグインディレクトリを追加
        /// </summary>
        public void AddPluginDirectory(string directory)
        {
            if (Directory.Exists(directory) && !_pluginDirectories.Contains(directory))
            {
                _pluginDirectories.Add(directory);
            }
        }

        /// <summary>
        /// すべてのプラグインを読み込み
        /// </summary>
        public async Task LoadAllPluginsAsync()
        {
            var pluginFiles = new List<string>();

            // 各プラグインディレクトリからDLLファイルを検索
            foreach (var directory in _pluginDirectories)
            {
                if (Directory.Exists(directory))
                {
                    pluginFiles.AddRange(Directory.GetFiles(directory, "*.dll", SearchOption.AllDirectories));
                }
            }

            // 現在のアセンブリ内のプラグインも検索（組み込みプラグイン用）
            var currentAssembly = Assembly.GetExecutingAssembly();
            await LoadPluginsFromAssemblyAsync(currentAssembly);

            // 外部DLLからプラグインを読み込み
            foreach (var file in pluginFiles)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(file);
                    await LoadPluginsFromAssemblyAsync(assembly);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"プラグイン読み込みエラー {file}: {ex.Message}");
                }
            }

            // 優先順位でソート
            var sortedPlugins = LoadedPlugins.OrderBy(p => p.Metadata.Priority).ToList();
            LoadedPlugins.Clear();
            foreach (var plugin in sortedPlugins)
            {
                LoadedPlugins.Add(plugin);
            }
        }

        /// <summary>
        /// 指定されたアセンブリからプラグインを読み込み
        /// </summary>
        private async Task LoadPluginsFromAssemblyAsync(Assembly assembly)
        {
            try
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    // 一部のタイプが読み込めない場合は読み込めたタイプのみ使用
                    types = ex.Types.Where(t => t != null).ToArray()!;
                }
                
                var pluginTypes = types
                    .Where(t => typeof(IToolPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                    .ToList();

                // プラグインを並列で初期化
                var loadTasks = pluginTypes.Select(async type =>
                {
                    try
                    {
                        var plugin = Activator.CreateInstance(type) as IToolPlugin;
                        if (plugin != null)
                        {
                            await LoadPluginAsync(plugin);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"プラグインインスタンス作成エラー {type.Name}: {ex.Message}");
                    }
                });
                
                await Task.WhenAll(loadTasks);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"アセンブリプラグイン読み込みエラー {assembly.FullName}: {ex.Message}");
            }
        }

        /// <summary>
        /// 単一プラグインを読み込み
        /// </summary>
        private async Task LoadPluginAsync(IToolPlugin plugin)
        {
            try
            {
                if (_loadedPlugins.ContainsKey(plugin.Id))
                {
                    System.Diagnostics.Debug.WriteLine($"プラグイン {plugin.Id} は既に読み込まれています");
                    return;
                }

                // プラグインを初期化
                await plugin.InitializeAsync();

                // プラグインインスタンスを作成
                var instance = new PluginInstance
                {
                    Metadata = new PluginMetadata
                    {
                        Id = plugin.Id,
                        DisplayName = plugin.DisplayName,
                        Description = plugin.Description,
                        Version = plugin.Version,
                        IconPath = plugin.IconPath,
                        Priority = plugin.Priority,
                        IsEnabled = plugin.IsEnabled
                    },
                    Plugin = plugin,
                    IsLoaded = true
                };

                _loadedPlugins[plugin.Id] = instance;
                LoadedPlugins.Add(instance);

                System.Diagnostics.Debug.WriteLine($"プラグイン {plugin.DisplayName} を読み込みました");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"プラグイン読み込みエラー {plugin.Id}: {ex.Message}");
                
                // エラー情報を含むインスタンスを作成
                var errorInstance = new PluginInstance
                {
                    Metadata = new PluginMetadata
                    {
                        Id = plugin.Id,
                        DisplayName = plugin.DisplayName,
                        Description = plugin.Description,
                        IsEnabled = false
                    },
                    Plugin = plugin,
                    IsLoaded = false,
                    LoadError = ex
                };

                _loadedPlugins[plugin.Id] = errorInstance;
                LoadedPlugins.Add(errorInstance);
            }
        }

        /// <summary>
        /// プラグインのViewとViewModelを作成
        /// </summary>
        public Task<(System.Windows.Controls.UserControl? view, object? viewModel)> CreatePluginUIAsync(string pluginId)
        {
            if (!_loadedPlugins.TryGetValue(pluginId, out var instance) || 
                instance.Plugin is not IToolPlugin plugin || 
                !instance.IsLoaded)
            {
                return Task.FromResult<(System.Windows.Controls.UserControl?, object?)>((null, null));
            }

            try
            {
                // ViewModelを作成（まだ作成されていない場合）
                if (instance.ViewModel == null)
                {
                    instance.ViewModel = plugin.CreateViewModel();
                }

                // Viewを作成（まだ作成されていない場合）
                if (instance.View == null)
                {
                    instance.View = plugin.CreateView();
                    if (instance.View != null && instance.ViewModel != null)
                    {
                        instance.View.DataContext = instance.ViewModel;
                    }
                }

                return Task.FromResult((instance.View, instance.ViewModel));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"プラグインUI作成エラー {pluginId}: {ex.Message}");
                return Task.FromResult<(System.Windows.Controls.UserControl?, object?)>((null, null));
            }
        }

        /// <summary>
        /// プラグインを取得
        /// </summary>
        public IToolPlugin? GetPlugin(string pluginId)
        {
            return _loadedPlugins.TryGetValue(pluginId, out var instance) ? instance.Plugin as IToolPlugin : null;
        }

        /// <summary>
        /// プラグインインスタンスを取得
        /// </summary>
        public PluginInstance? GetPluginInstance(string pluginId)
        {
            return _loadedPlugins.TryGetValue(pluginId, out var instance) ? instance : null;
        }

        /// <summary>
        /// すべてのプラグインをクリーンアップ
        /// </summary>
        public async Task CleanupAllPluginsAsync()
        {
            foreach (var instance in _loadedPlugins.Values)
            {
                if (instance.Plugin is IToolPlugin plugin && instance.IsLoaded)
                {
                    try
                    {
                        await plugin.CleanupAsync();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"プラグインクリーンアップエラー {plugin.Id}: {ex.Message}");
                    }
                }
            }

            _loadedPlugins.Clear();
            LoadedPlugins.Clear();
        }
    }
}