using System.Windows.Controls;
using GadgetTools.Core.Interfaces;
using GadgetTools.Services;

namespace GadgetTools.Plugins.TicketManage
{
    /// <summary>
    /// チケット管理プラグイン
    /// </summary>
    public class TicketManagePlugin : IToolPlugin
    {
        public string Id => "GadgetTools.TicketManage";
        public string DisplayName => "Ticket Management";
        public string Description => "Tool to retrieve and convert data from ticket management systems like Azure DevOps and Jira";
        public Version Version => new Version(1, 0, 0);
        public string? IconPath => null;
        public int Priority => 2;
        public bool IsEnabled { get; set; } = true;

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
            return new TicketManageView();
        }

        public object CreateViewModel()
        {
            return new TicketManageViewModel();
        }

        public async Task SaveSettingsAsync(object settings)
        {
            if (settings is TicketManagePluginSettings ticketSettings)
            {
                // プラグイン固有の設定を保存
                var appSettings = SettingsService.LoadSettings();
                
                // 設定にTicketManage設定を追加/更新
                appSettings.AzureDevOps = new SettingsService.AzureDevOpsSettings
                {
                    Organization = ticketSettings.Organization,
                    Project = ticketSettings.Project,
                    EncryptedPersonalAccessToken = SettingsService.EncryptString(ticketSettings.PersonalAccessToken),
                    WorkItemType = ticketSettings.WorkItemType,
                    State = ticketSettings.State,
                    MaxResults = ticketSettings.MaxResults,
                    DetailedMarkdown = ticketSettings.DetailedMarkdown
                };

                SettingsService.SaveSettings(appSettings);
            }
            await Task.CompletedTask;
        }

        public Task<object?> LoadSettingsAsync()
        {
            try
            {
                var appSettings = SettingsService.LoadSettings();
                
                if (appSettings.AzureDevOps != null)
                {
                    var azureSettings = appSettings.AzureDevOps;
                    
                    return Task.FromResult<object?>(new TicketManagePluginSettings
                    {
                        Organization = azureSettings.Organization,
                        Project = azureSettings.Project,
                        PersonalAccessToken = SettingsService.DecryptString(azureSettings.EncryptedPersonalAccessToken),
                        WorkItemType = azureSettings.WorkItemType,
                        State = azureSettings.State,
                        MaxResults = azureSettings.MaxResults,
                        DetailedMarkdown = azureSettings.DetailedMarkdown
                    });
                }
                
                return Task.FromResult<object?>(new TicketManagePluginSettings());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TicketManage設定読み込みエラー: {ex.Message}");
                return Task.FromResult<object?>(null);
            }
        }
    }

    /// <summary>
    /// チケット管理プラグイン固有の設定
    /// </summary>
    public class TicketManagePluginSettings
    {
        public string Organization { get; set; } = string.Empty;
        public string Project { get; set; } = string.Empty;
        public string PersonalAccessToken { get; set; } = string.Empty;
        public string WorkItemType { get; set; } = "All";
        public string State { get; set; } = "All";
        public int MaxResults { get; set; } = 50;
        public bool DetailedMarkdown { get; set; } = true;
        public string LastUsedFilter { get; set; } = string.Empty;
        public DateTime LastQueryTime { get; set; } = DateTime.MinValue;
    }
}