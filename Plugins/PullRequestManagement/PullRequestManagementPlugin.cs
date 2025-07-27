using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using GadgetTools.Core.Interfaces;

namespace GadgetTools.Plugins.PullRequestManagement
{
    /// <summary>
    /// Pull Request管理プラグイン
    /// </summary>
    public class PullRequestManagementPlugin : IToolPlugin
    {
        private readonly IPullRequestSettingsService _settingsService;

        public string Id => "GadgetTools.PullRequestManagement";
        public string DisplayName => "Pull Request Management";
        public string Description => "Azure DevOps Pull Request の取得・管理・表示ツール";
        public Version Version => new Version(1, 0, 0);
        public string? IconPath => null;
        public int Priority => 3;
        public bool IsEnabled { get; set; } = true;

        public PullRequestManagementPlugin()
        {
            _settingsService = new PullRequestSettingsService();
        }

        public Task InitializeAsync()
        {
            // 初期化処理（必要に応じて）
            return Task.CompletedTask;
        }

        public Task CleanupAsync()
        {
            // クリーンアップ処理（必要に応じて）
            return Task.CompletedTask;
        }

        public UserControl CreateView()
        {
            return new PullRequestManagementView();
        }

        public object CreateViewModel()
        {
            return new PullRequestManagementViewModel(_settingsService);
        }

        public async Task SaveSettingsAsync(object settings)
        {
            if (settings is PullRequestManagementSettings prSettings)
            {
                await _settingsService.SaveSettingsAsync(prSettings);
            }
        }

        public async Task<object?> LoadSettingsAsync()
        {
            try
            {
                return await _settingsService.LoadSettingsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PullRequestManagement設定読み込みエラー: {ex.Message}");
                return null;
            }
        }
    }
}