using System.IO;
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
                try
                {
                    // JSON形式でプラグイン設定を保存
                    var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                        "GadgetTools", "TicketManageSettings.json");
                    
                    var directory = Path.GetDirectoryName(settingsPath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    
                    var json = System.Text.Json.JsonSerializer.Serialize(ticketSettings, new System.Text.Json.JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    });
                    await File.WriteAllTextAsync(settingsPath, json);
                    
                    // 基本設定も共有設定に保存
                    var appSettings = SettingsService.LoadSettings();
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
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"TicketManage設定保存エラー: {ex.Message}");
                }
            }
        }

        public async Task<object?> LoadSettingsAsync()
        {
            try
            {
                var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                    "GadgetTools", "TicketManageSettings.json");
                
                TicketManagePluginSettings settings;
                
                if (File.Exists(settingsPath))
                {
                    var json = await File.ReadAllTextAsync(settingsPath);
                    settings = System.Text.Json.JsonSerializer.Deserialize<TicketManagePluginSettings>(json) ?? new TicketManagePluginSettings();
                }
                else
                {
                    // フォールバック: 既存の共有設定から読み込み
                    settings = new TicketManagePluginSettings();
                    var appSettings = SettingsService.LoadSettings();
                    
                    if (appSettings.AzureDevOps != null)
                    {
                        var azureSettings = appSettings.AzureDevOps;
                        settings.Organization = azureSettings.Organization;
                        settings.Project = azureSettings.Project;
                        settings.PersonalAccessToken = SettingsService.DecryptString(azureSettings.EncryptedPersonalAccessToken);
                        settings.WorkItemType = azureSettings.WorkItemType;
                        settings.State = azureSettings.State;
                        settings.MaxResults = azureSettings.MaxResults;
                        settings.DetailedMarkdown = azureSettings.DetailedMarkdown;
                    }
                }
                
                return settings;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TicketManage設定読み込みエラー: {ex.Message}");
                return new TicketManagePluginSettings();
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
        
        // UI State Settings
        public string Area { get; set; } = string.Empty;
        public string Iteration { get; set; } = string.Empty;
        public List<string> Projects { get; set; } = new();
        public List<string> Areas { get; set; } = new();
        public List<string> Iterations { get; set; } = new();
        public List<string> WorkItemTypes { get; set; } = new();
        public List<string> States { get; set; } = new();
        
        // Advanced Search Settings
        public string TitleSearch { get; set; } = string.Empty;
        public string DescriptionSearch { get; set; } = string.Empty;
        public DateTime? CreatedAfter { get; set; }
        public DateTime? CreatedBefore { get; set; }
        public DateTime? UpdatedAfter { get; set; }
        public DateTime? UpdatedBefore { get; set; }
        public string TagsSearch { get; set; } = string.Empty;
        public int? MinPriority { get; set; }
        public int? MaxPriority { get; set; }
        public string AssignedTo { get; set; } = string.Empty;
        
        // Display Settings
        public int HighlightDays { get; set; } = 7;
        public bool EnableHighlight { get; set; } = true;
        public string FilterText { get; set; } = string.Empty;
    }
}